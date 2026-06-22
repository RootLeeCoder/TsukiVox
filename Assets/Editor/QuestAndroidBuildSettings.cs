using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

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
            PlayerSettings.productName = "TsukiVox Audio Prototype";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.tsukivox.audio");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
        }
    }
}
