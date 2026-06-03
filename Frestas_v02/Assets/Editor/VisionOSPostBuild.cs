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
        if (target != BuildTarget.VisionOS) return;

        string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(pbxPath);

        string targetGuid = project.GetUnityMainTargetGuid();

        // Create or update the entitlements plist
        string entitlementsRelative = "Unity-iPhone/Frestas.entitlements";
        string entitlementsFull     = Path.Combine(buildPath, entitlementsRelative);

        Directory.CreateDirectory(Path.GetDirectoryName(entitlementsFull));

        var plist = new PlistDocument();
        if (File.Exists(entitlementsFull))
            plist.ReadFromFile(entitlementsFull);

        plist.root.SetBoolean("com.apple.security.network.client", true);
        plist.WriteToFile(entitlementsFull);

        // Point the Xcode target at the entitlements file
        project.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsRelative);

        project.WriteToFile(pbxPath);

        Debug.Log("[FRESTAS] Network client entitlement added to Xcode project.");
    }
}
