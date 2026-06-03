using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class VisionOSPostBuild
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        // Guard: only run for visionOS. Also catches the case where
        // BuildTarget.VisionOS has a different internal value across
        // Unity 6 minor versions by checking the name string as a fallback.
#if UNITY_VISIONOS
        bool isVisionOS = (target == BuildTarget.VisionOS);
#else
        bool isVisionOS = target.ToString().Contains("VisionOS");
#endif
        if (!isVisionOS) return;

        // PBXProject.GetPBXProjectPath() hardcodes "Unity-iPhone.xcodeproj"
        // which does not exist in visionOS build output — scan for the real name.
        string[] projs = Directory.GetDirectories(buildPath, "*.xcodeproj",
                                                  SearchOption.TopDirectoryOnly);
        if (projs.Length == 0)
        {
            Debug.LogError($"[FRESTAS] No .xcodeproj found in: {buildPath}");
            return;
        }

        string xcodeproj   = projs[0];
        string projectName = Path.GetFileNameWithoutExtension(xcodeproj);
        string pbxPath     = Path.Combine(xcodeproj, "project.pbxproj");

        var project = new PBXProject();
        project.ReadFromFile(pbxPath);

        string targetGuid = project.GetUnityMainTargetGuid();

        string entitlementsRelative = $"{projectName}/Frestas.entitlements";
        string entitlementsFull     = Path.Combine(buildPath, entitlementsRelative);

        Directory.CreateDirectory(Path.GetDirectoryName(entitlementsFull));

        var plist = new PlistDocument();
        if (File.Exists(entitlementsFull))
            plist.ReadFromFile(entitlementsFull);

        plist.root.SetBoolean("com.apple.security.network.client", true);
        plist.WriteToFile(entitlementsFull);

        project.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsRelative);
        project.WriteToFile(pbxPath);

        Debug.Log($"[FRESTAS] Network entitlement written → {entitlementsFull}");
    }
}
