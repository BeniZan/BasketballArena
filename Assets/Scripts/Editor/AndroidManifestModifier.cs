using System.Linq;
using Unity.Android.Gradle;
using Unity.Android.Gradle.Manifest;
using UnityEditor.Android;
using UnityEngine;
using UnityEngine.Rendering;

public class AndroidManifestModifier : AndroidProjectFilesModifier {
    public override int callbackOrder => 9999999;
    static readonly string[] QuestAndXrKeywords = {
        "quest",
        "xr",
        "openxr",
        "oculus",
        "meta xr",
        "meta-xr",
        "metaxr"
    };

    CustomLogger _logger;
    public override AndroidProjectFilesModifierContext Setup() {
        return new AndroidProjectFilesModifierContext();
    }

    public override void OnModifyAndroidProjectFiles(AndroidProjectFiles projectFiles) {
        _logger = new CustomLogger(null, Color.magenta, "");
        _logger.Log("Editing manifest file");

        var str = "";
        foreach(var file in projectFiles.AdditionalLibrariesBuildGradleFiles) {
            str = "name:" + file.Key;
            var android = file.Value;
            LogLoop(android, ref str);
        }
        _logger.Log(str);
    }

    string LogLoop(Unity.Android.Gradle.BaseElement block, ref string str) {
        str += "\n "+ block.GetUniqueName() + ":";
        if(block is BaseBlock bb)
        foreach (var element in block.GetElementDependenciesIDs()) { 
                LogLoop(bb.GetElement(element), ref str);
        }
        return str;
    }

    static bool ContainsQuestOrXrDependency(string value) {
        if (string.IsNullOrEmpty(value)) {
            return false;
        }

        var lower = value.ToLowerInvariant();
        foreach (var keyword in QuestAndXrKeywords) {
            if (lower.Contains(keyword)) {
                return true;
            }
        }

        return false;
    } 
}
