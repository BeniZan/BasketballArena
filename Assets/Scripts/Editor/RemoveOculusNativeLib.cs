using UnityEditor.Build;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Android; 
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR;


public class RemoveOculusNativeLib : IPreprocessBuildWithReport, IActiveBuildTargetChanged {
    public int callbackOrder => -100;

    public const bool IS_QUEST_TARGET =
#if UNITY_META_QUEST
        true;
#else
        false;
#endif

    [RuntimeInitializeOnLoadMethod]
    static void OnRuntimeMethodLoad() => ValidateARR();

    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget) {
        ValidateARR();
    }

    public void OnPreprocessBuild(BuildReport report) {
        ValidateARR();
    }

    [MenuItem("Tools/Validate Meta .aar")]
    static void ValidateARR() { 
        var relativePath = 
            "Packages/com.meta.xr.sdk.core/Plugins/Android/RuntimeOptimizer_Plugin.aar";
        var aarPath = Path.GetFullPath(relativePath); 
        if (File.Exists(aarPath)) {
            ModifyAAR(aarPath);
            Debug.Log(LogUtil.ColorGreen("Modified AAR file: " + aarPath));
        } else {
            Debug.LogWarning($"File not found: {aarPath}");
        }

    }


    static void ModifyAAR(string zipPath) {
        const string entryName = "AndroidManifest.xml";
        using (FileStream fs = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite))
        using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Update)) {
            ZipArchiveEntry entry = archive.GetEntry(entryName);

            if (entry == null)
                throw new FileNotFoundException(entryName);

            // Read current contents
            string text;
            using (var reader = new StreamReader(entry.Open())) {
                text = reader.ReadToEnd();
            }

            // Modify
            text = text.Replace("android:required=\"true\"", 
                $"android:required=\"{IS_QUEST_TARGET.ToString().ToLower()}\"");

            // Delete old entry
            entry.Delete();

            // Create new entry
            ZipArchiveEntry newEntry = archive.CreateEntry(entryName);

            using (var writer = new StreamWriter(newEntry.Open())) {
                writer.Write(text);
            }
        }
    }

}