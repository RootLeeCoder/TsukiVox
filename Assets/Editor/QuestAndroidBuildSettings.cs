using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TsukiVox.AudioPrototype.Editor
{
    public sealed class QuestAndroidBuildSettings : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
            {
                return;
            }

            PlayerSettings.companyName = "TsukiVox";
            PlayerSettings.productName = "TsukiVox Quest";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tsukivox.audio");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            ForceAndroidGameActivity();
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            ForceNewInputSystem();
            TryEnableOpenXrLoader();
            TryConfigureOpenXrQuestFeatures();
            TryApplyHelperHostToBuildScenes();
        }

        private static void ForceNewInputSystem()
        {
            TrySetPlayerSettingsSerializedInt("activeInputHandler", 1);
            RewriteProjectSettingsValue("activeInputHandler", "1");
        }

        private static void ForceAndroidGameActivity()
        {
            var gameActivity = ((int)AndroidApplicationEntry.GameActivity).ToString();
            TrySetPlayerSettingsSerializedInt("androidApplicationEntry", (int)AndroidApplicationEntry.GameActivity);
            RewriteProjectSettingsValue("androidApplicationEntry", gameActivity);
        }

        private static void TryApplyHelperHostToBuildScenes()
        {
            try
            {
                var activeScenePath = SceneManager.GetActiveScene().path;
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled || string.IsNullOrWhiteSpace(scene.path))
                    {
                        continue;
                    }

                    var openedScene = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
                    var prototype = CreateAudioPrototypeScene.EnsurePlaylistPrototypeInCurrentScene();
                    CreateAudioPrototypeScene.ApplyCurrentHelperHost(prototype);
                    var videoScreen = CreateAudioPrototypeScene.EnsureVideoScreenPrototypeInCurrentScene(prototype);
                    CreateAudioPrototypeScene.EnsureAppShellInCurrentScene(prototype, videoScreen);
                    CreateAudioPrototypeScene.EnsureKtvRoomInCurrentScene(videoScreen);
                    EditorSceneManager.MarkSceneDirty(openedScene);
                    EditorSceneManager.SaveScene(openedScene);
                }

                if (!string.IsNullOrWhiteSpace(activeScenePath))
                {
                    EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox Build] Could not apply helper host to build scenes: {exception.Message}");
            }
        }

        private static void TryEnableOpenXrLoader()
        {
            try
            {
                var generalSettingsType = Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                var managerSettingsType = Type.GetType("UnityEngine.XR.Management.XRManagerSettings, Unity.XR.Management");
                var metadataStoreType = Type.GetType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore, Unity.XR.Management.Editor");
                if (generalSettingsType == null || managerSettingsType == null || metadataStoreType == null)
                {
                    UnityEngine.Debug.LogWarning("[TsukiVox XR] XR Management/OpenXR packages are not imported yet; open the project once so Package Manager can resolve them.");
                    return;
                }

                var getOrCreateMethod = generalSettingsType.GetMethod("GetOrCreate", BindingFlags.NonPublic | BindingFlags.Static);
                var settingsPerBuildTarget = getOrCreateMethod?.Invoke(null, null);
                if (settingsPerBuildTarget == null)
                {
                    UnityEngine.Debug.LogWarning("[TsukiVox XR] XR settings asset was not available.");
                    return;
                }

                var setSettingsMethod = generalSettingsType.GetMethod("SetSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
                var settingsForBuildTargetMethod = generalSettingsType.GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
                var androidSettings = settingsForBuildTargetMethod?.Invoke(settingsPerBuildTarget, new object[] { BuildTargetGroup.Android });
                if (androidSettings == null)
                {
                    androidSettings = ScriptableObjectUtility.CreateInstance("UnityEngine.XR.Management.XRGeneralSettings, Unity.XR.Management");
                    setSettingsMethod?.Invoke(settingsPerBuildTarget, new object[] { BuildTargetGroup.Android, androidSettings });
                }

                var managerProperty = androidSettings.GetType().GetProperty("Manager", BindingFlags.Public | BindingFlags.Instance);
                var manager = managerProperty?.GetValue(androidSettings);
                if (manager == null)
                {
                    var managerAsset = UnityEngine.ScriptableObject.CreateInstance(managerSettingsType);
                    managerAsset.name = "Android XR Manager Settings";
                    manager = managerAsset;
                    managerProperty?.SetValue(androidSettings, manager);
                    AssetDatabase.AddObjectToAsset(managerAsset, AssetDatabase.GetAssetPath((UnityEngine.Object)androidSettings));
                }

                const string openXrLoaderTypeName = "UnityEngine.XR.OpenXR.OpenXRLoader";
                var assignLoaderMethod = metadataStoreType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                var isAssigned = (bool)(metadataStoreType.GetMethod("IsLoaderAssigned", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { openXrLoaderTypeName, BuildTargetGroup.Android }) ?? false);
                if (!isAssigned && !(bool)(assignLoaderMethod?.Invoke(null, new object[] { manager, openXrLoaderTypeName, BuildTargetGroup.Android }) ?? false))
                {
                    UnityEngine.Debug.LogWarning("[TsukiVox XR] Could not auto-assign the OpenXR loader for Android. Enable it in Project Settings > XR Plug-in Management > Android.");
                }

                EditorUtility.SetDirty((UnityEngine.Object)androidSettings);
                EditorUtility.SetDirty((UnityEngine.Object)manager);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox XR] Could not auto-enable OpenXR loader: {exception.Message}");
            }
        }

        private static void TryConfigureOpenXrQuestFeatures()
        {
            try
            {
                var settingsType = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
                if (settingsType == null)
                {
                    return;
                }

                var getSettingsMethod = settingsType.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
                var settings = getSettingsMethod?.Invoke(null, new object[] { BuildTargetGroup.Android });
                if (settings == null)
                {
                    return;
                }

                SetEnumProperty(settings, "latencyOptimization", 1);
                SetEnumProperty(settings, "foveatedRenderingApi", 0);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.FoveatedRenderingFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.DebugUtilsFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.ApiLayersFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.SpaceWarpFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Extensions.PerformanceSettings.XrPerformanceSettingsFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Mock.MockRuntime", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.RuntimeDebugger.RuntimeDebuggerOpenXRFeature", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.HandCommonPosesInteraction", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.HandInteractionProfile", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.EyeGazeInteraction", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.PalmPoseInteraction", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.DPadInteraction", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchProControllerProfile", false);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.MetaQuestTouchPlusControllerProfile", true);
                SetOpenXrFeatureEnabled(settings, "UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile", true);
                ConfigureMetaQuestTargetDevices(settings);
                EditorUtility.SetDirty((UnityEngine.Object)settings);
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox XR] Could not auto-configure Quest OpenXR features: {exception.Message}");
            }
        }

        private static void SetEnumProperty(object target, string propertyName, int value)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            var propertyValue = property.PropertyType.IsEnum
                ? Enum.ToObject(property.PropertyType, value)
                : Convert.ChangeType(value, property.PropertyType);
            property.SetValue(target, propertyValue);
        }

        private static void SetOpenXrFeatureEnabled(object settings, string featureTypeName, bool enabled)
        {
            var featureType = ResolveType(featureTypeName);
            if (featureType == null)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox XR] OpenXR feature type was not found: {featureTypeName}");
                return;
            }

            var getFeatureMethod = settings.GetType().GetMethod("GetFeature", new[] { typeof(Type) });
            var feature = getFeatureMethod?.Invoke(settings, new object[] { featureType });
            if (feature == null)
            {
                return;
            }

            var enabledProperty = feature.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabledProperty != null && enabledProperty.CanWrite)
            {
                enabledProperty.SetValue(feature, enabled);
                EditorUtility.SetDirty((UnityEngine.Object)feature);
            }
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var assemblyQualifiedNames = new[]
            {
                $"{typeName}, Unity.XR.OpenXR",
                $"{typeName}, Unity.XR.OpenXR.Features.MockRuntime",
                $"{typeName}, Unity.XR.OpenXR.Features.RuntimeDebugger",
                $"{typeName}, Unity.XR.OpenXR.Features.MetaQuestSupport"
            };

            foreach (var assemblyQualifiedName in assemblyQualifiedNames)
            {
                type = Type.GetType(assemblyQualifiedName);
                if (type != null)
                {
                    return type;
                }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void ConfigureMetaQuestTargetDevices(object settings)
        {
            var featureType = ResolveType("UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature");
            if (featureType == null)
            {
                return;
            }

            var getFeatureMethod = settings.GetType().GetMethod("GetFeature", new[] { typeof(Type) });
            var feature = getFeatureMethod?.Invoke(settings, new object[] { featureType });
            if (feature == null)
            {
                return;
            }

            SetTargetDeviceEnabled(feature, "quest", true);
            SetTargetDeviceEnabled(feature, "quest2", true);
            SetTargetDeviceEnabled(feature, "cambria", false);
            SetTargetDeviceEnabled(feature, "eureka", true);
            SetTargetDeviceEnabled(feature, "quest3s", true);
            EditorUtility.SetDirty((UnityEngine.Object)feature);
        }

        private static void SetTargetDeviceEnabled(object metaQuestFeature, string manifestName, bool enabled)
        {
            var method = metaQuestFeature.GetType().GetMethod("EnableTargetDevice", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(metaQuestFeature, new object[] { manifestName, enabled });
        }

        public static void RemoveQuestProEyeTrackingManifestRequirements(string gradleProjectPath)
        {
            var manifestPaths = new[]
            {
                Path.Combine(gradleProjectPath, "src", "main", "AndroidManifest.xml"),
                Path.Combine(gradleProjectPath, "xrmanifest.androidlib", "AndroidManifest.xml"),
                Path.Combine(gradleProjectPath, "unityLibrary", "src", "main", "AndroidManifest.xml"),
                Path.Combine(gradleProjectPath, "unityLibrary", "xrmanifest.androidlib", "AndroidManifest.xml"),
            };

            foreach (var manifestPath in manifestPaths)
            {
                if (File.Exists(manifestPath))
                {
                    RemoveEyeTrackingManifestEntries(manifestPath);
                }
            }
        }

        private static void RemoveEyeTrackingManifestEntries(string manifestPath)
        {
            try
            {
                var document = new XmlDocument();
                document.Load(manifestPath);
                var namespaceManager = new XmlNamespaceManager(document.NameTable);
                namespaceManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                var changed = false;

                changed |= RemoveNodes(document, namespaceManager, "/manifest/uses-feature[@android:name='oculus.software.eye_tracking']");
                changed |= RemoveNodes(document, namespaceManager, "/manifest/uses-permission[@android:name='com.oculus.permission.EYE_TRACKING']");

                if (changed)
                {
                    document.Save(manifestPath);
                    UnityEngine.Debug.Log($"[TsukiVox XR] Removed Quest Pro eye-tracking manifest entries from {manifestPath}");
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox XR] Could not clean Android manifest {manifestPath}: {exception.Message}");
            }
        }

        private static bool RemoveNodes(XmlDocument document, XmlNamespaceManager namespaceManager, string xpath)
        {
            var nodes = document.SelectNodes(xpath, namespaceManager);
            if (nodes == null || nodes.Count == 0)
            {
                return false;
            }

            foreach (XmlNode node in nodes)
            {
                node.ParentNode?.RemoveChild(node);
            }

            return true;
        }

        private static void RewriteProjectSettingsValue(string key, string value)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");
            if (!File.Exists(path))
            {
                return;
            }

            var text = File.ReadAllText(path);
            var pattern = $@"(?m)^(\s*{Regex.Escape(key)}:\s*)\S+";
            var regex = new Regex(pattern);
            var updated = regex.Replace(text, $"${{1}}{value}", 1);
            if (updated != text)
            {
                File.WriteAllText(path, updated);
            }
        }

        private static void TrySetPlayerSettingsSerializedInt(string key, int value)
        {
            try
            {
                var playerSettings = GetCurrentPlayerSettingsObject();
                if (playerSettings == null)
                {
                    return;
                }

                var serializedObject = new SerializedObject(playerSettings);
                var property = serializedObject.FindProperty(key);
                if (property == null)
                {
                    return;
                }

                property.intValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[TsukiVox Build] Could not update PlayerSettings.{key}: {exception.Message}");
            }
        }

        private static PlayerSettings GetCurrentPlayerSettingsObject()
        {
            var buildProfileType = Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor.BuildProfileModule");
            var globalPlayerSettingsField = buildProfileType?.GetField("s_GlobalPlayerSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var playerSettings = globalPlayerSettingsField?.GetValue(null) as PlayerSettings;

            var getActiveProfileMethod = buildProfileType?.GetMethod("GetActiveBuildProfile", BindingFlags.Public | BindingFlags.Static);
            var activeBuildProfile = getActiveProfileMethod?.Invoke(null, null);
            var playerSettingsOverrideField = buildProfileType?.GetField("m_PlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            var playerSettingsOverride = activeBuildProfile == null ? null : playerSettingsOverrideField?.GetValue(activeBuildProfile) as PlayerSettings;

            return playerSettingsOverride != null ? playerSettingsOverride : playerSettings;
        }

        private static class ScriptableObjectUtility
        {
            public static UnityEngine.ScriptableObject CreateInstance(string assemblyQualifiedTypeName)
            {
                var type = Type.GetType(assemblyQualifiedTypeName);
                if (type == null)
                {
                    throw new InvalidOperationException($"Type not found: {assemblyQualifiedTypeName}");
                }

                return UnityEngine.ScriptableObject.CreateInstance(type);
            }
        }
    }

    public sealed class QuestAndroidManifestPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            QuestAndroidBuildSettings.RemoveQuestProEyeTrackingManifestRequirements(path);
        }
    }
}
