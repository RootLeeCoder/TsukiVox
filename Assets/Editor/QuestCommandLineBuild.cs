using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TsukiVox.AudioPrototype;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TsukiVox.AudioPrototype.Editor
{
    public static class QuestCommandLineBuild
    {
        private const string DefaultOutputPath = "build/TsukiVox-Quest.apk";
        private const string BuildInfoAssetPath = "Assets/Resources/TsukiVoxBuildInfo.json";
        private const string BuildRequestPath = "build/quest-build.request.json";
        private static readonly Dictionary<string, long> RequiredPreloadedAssets = new Dictionary<string, long>
        {
            { "25879fd5fce4e244989dfa25c19c02de", 4082574872352023478L },
            { "1dc8666de9868e44ab076d66232e973d", 6054584990866881122L },
        };

        private static bool isProcessingRequest;
        private static double nextRequestPollAt;

        [Serializable]
        private sealed class BuildRequest
        {
            public string outputPath;
            public string buildId;
            public string buildTimeUtc;
            public bool releaseBuild;
            public string resultPath;
            public bool scriptsRefreshed;
        }

        [Serializable]
        private sealed class BuildRequestResult
        {
            public string buildId;
            public bool succeeded;
            public string error;
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string productVersion;
            public string buildId;
            public string buildTimeUtc;
            public string gitCommit;
            public bool gitDirty;
            public string unityVersion;
            public string androidVersionName;
            public string apkPath;
            public long apkBytes;
            public string apkSha256;
            public bool developmentBuild;
        }

        [MenuItem("TsukiVox/Build Quest APK")]
        public static void BuildFromMenu()
        {
            var now = DateTimeOffset.UtcNow;
            BuildAndroid(DefaultOutputPath, now.ToString("yyyyMMdd-HHmmss"), now.ToString("O"), false);
        }

        public static void BuildAndroid()
        {
            BuildAndroid(
                GetArgument("-tsukivoxOutput"),
                GetArgument("-tsukivoxBuildId"),
                GetArgument("-tsukivoxBuildTimeUtc"),
                HasArgument("-tsukivoxRelease"));
        }

        [InitializeOnLoadMethod]
        private static void RegisterBuildRequestWatcher()
        {
            if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            EditorApplication.update -= ProcessPendingBuildRequest;
            EditorApplication.update += ProcessPendingBuildRequest;
        }

        private static void ProcessPendingBuildRequest()
        {
            if (isProcessingRequest || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                BuildPipeline.isBuildingPlayer || EditorApplication.timeSinceStartup < nextRequestPollAt)
            {
                return;
            }

            nextRequestPollAt = EditorApplication.timeSinceStartup + 0.5d;
            var requestPath = Path.GetFullPath(BuildRequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            var pendingRequest = JsonUtility.FromJson<BuildRequest>(File.ReadAllText(requestPath));
            if (pendingRequest == null || string.IsNullOrWhiteSpace(pendingRequest.resultPath))
            {
                throw new InvalidOperationException("The Quest build request is invalid.");
            }

            if (!pendingRequest.scriptsRefreshed)
            {
                pendingRequest.scriptsRefreshed = true;
                File.WriteAllText(requestPath, JsonUtility.ToJson(pendingRequest, true), new UTF8Encoding(false));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CompilationPipeline.RequestScriptCompilation();
                return;
            }

            isProcessingRequest = true;
            BuildRequest request = null;
            var result = new BuildRequestResult();
            try
            {
                File.Delete(requestPath);
                request = pendingRequest;

                result.buildId = request.buildId;
                BuildAndroid(request.outputPath, request.buildId, request.buildTimeUtc, request.releaseBuild);
                result.succeeded = true;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
                Debug.LogException(exception);
            }
            finally
            {
                var resultPath = request?.resultPath;
                if (!string.IsNullOrWhiteSpace(resultPath))
                {
                    var absoluteResultPath = Path.GetFullPath(resultPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(absoluteResultPath)
                        ?? throw new InvalidOperationException("Could not resolve the build result directory."));
                    File.WriteAllText(absoluteResultPath, JsonUtility.ToJson(result, true), new UTF8Encoding(false));
                }

                isProcessingRequest = false;
            }
        }

        private static void BuildAndroid(string outputPath, string buildId, string buildTimeUtc, bool releaseBuild)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            outputPath = string.IsNullOrWhiteSpace(outputPath) ? DefaultOutputPath : outputPath;
            var absoluteOutputPath = Path.GetFullPath(Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.Combine(projectRoot, outputPath));
            if (string.IsNullOrWhiteSpace(buildId))
            {
                buildId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            }

            if (string.IsNullOrWhiteSpace(buildTimeUtc))
            {
                buildTimeUtc = DateTimeOffset.UtcNow.ToString("O");
            }

            var gitCommit = RunGit(projectRoot, "rev-parse --short HEAD", "unavailable");
            var gitDirty = !string.IsNullOrWhiteSpace(RunGit(projectRoot, "status --porcelain --untracked-files=normal", string.Empty));
            var buildInfo = new QuestBuildInfoData
            {
                productVersion = QuestBuildInfo.ProductVersion,
                buildId = buildId,
                buildTimeUtc = buildTimeUtc,
                gitCommit = gitCommit,
                gitDirty = gitDirty,
            };

            WriteBuildInfoAsset(buildInfo);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)
                ?? throw new InvalidOperationException("Could not resolve the APK output directory."));
            var androidVersionName = $"{QuestBuildInfo.ProductVersion}-build.{buildId}";
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in EditorBuildSettings.");
            }

            var developmentBuild = !releaseBuild;
            var options = developmentBuild ? BuildOptions.Development : BuildOptions.None;
            EnsureRequiredPreloadedAssets();
            PlayerSettings.bundleVersion = androidVersionName;
            EditorUserBuildSettings.buildAppBundle = false;
            try
            {
                Debug.Log($"[TsukiVox Build] Building {absoluteOutputPath} with id={buildId}");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = absoluteOutputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options,
                });

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(absoluteOutputPath))
                {
                    throw new InvalidOperationException(
                        $"Quest APK build failed: {report.summary.result}, " +
                        $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}.");
                }

                var receipt = new BuildReceipt
                {
                    productVersion = buildInfo.productVersion,
                    buildId = buildInfo.buildId,
                    buildTimeUtc = buildInfo.buildTimeUtc,
                    gitCommit = buildInfo.gitCommit,
                    gitDirty = buildInfo.gitDirty,
                    unityVersion = Application.unityVersion,
                    androidVersionName = androidVersionName,
                    apkPath = absoluteOutputPath,
                    apkBytes = new FileInfo(absoluteOutputPath).Length,
                    apkSha256 = ComputeSha256(absoluteOutputPath),
                    developmentBuild = developmentBuild,
                };
                var receiptPath = Path.ChangeExtension(absoluteOutputPath, ".build.json");
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
                Debug.Log(
                    $"[TsukiVox Build] APK complete id={buildId} bytes={receipt.apkBytes} " +
                    $"versionName={androidVersionName} sha256={receipt.apkSha256} receipt={receiptPath}");
            }
            finally
            {
                PlayerSettings.bundleVersion = QuestBuildInfo.ProductVersion;
                EnsureRequiredPreloadedAssets();
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureRequiredPreloadedAssets()
        {
            var existingAssets = PlayerSettings.GetPreloadedAssets().Where(asset => asset != null).ToArray();
            var normalizedAssets = new List<UnityEngine.Object>(existingAssets.Length);
            foreach (var asset in existingAssets)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId) &&
                    RequiredPreloadedAssets.TryGetValue(guid, out var requiredLocalId))
                {
                    if (localId == requiredLocalId && !normalizedAssets.Contains(asset))
                    {
                        normalizedAssets.Add(asset);
                    }

                    continue;
                }

                normalizedAssets.Add(asset);
            }

            foreach (var requiredAsset in RequiredPreloadedAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(requiredAsset.Key);
                var asset = string.IsNullOrWhiteSpace(path)
                    ? null
                    : AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(candidate =>
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long localId) &&
                        localId == requiredAsset.Value);
                if (asset != null && !normalizedAssets.Contains(asset))
                {
                    normalizedAssets.Add(asset);
                }
            }

            if (!existingAssets.SequenceEqual(normalizedAssets))
            {
                PlayerSettings.SetPreloadedAssets(normalizedAssets.ToArray());
            }
        }

        private static void WriteBuildInfoAsset(QuestBuildInfoData buildInfo)
        {
            var absolutePath = Path.GetFullPath(BuildInfoAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)
                ?? throw new InvalidOperationException("Could not resolve the build info directory."));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(buildInfo, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(BuildInfoAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static string GetArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }

        private static bool HasArgument(string name)
        {
            return Environment.GetCommandLineArgs().Any(
                argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string RunGit(string projectRoot, string arguments, string fallback)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return fallback;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return process.ExitCode == 0 ? output : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
