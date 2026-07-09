using UnityEditor;
using System.IO;

public static class BuildAssetBundles
{
    [MenuItem("AlienInvasion/Build AssetBundle")]
    public static void Build()
    {
        string outDir = "AssetBundles";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        BuildPipeline.BuildAssetBundles(outDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
