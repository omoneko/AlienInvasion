using UnityEditor;
using UnityEngine;
using System.IO;

// Editor script that builds the Alien Invasion AssetBundle headlessly, in batchmode.
// The Unity 5.6 GUI hangs on online licence activation, so this never opens it and runs
//   Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod BuildAssetBundles.BuildHeadless -quit
// from the command line instead. It assigns the bundle name to the FBX files, creates the
// red decal prefab and builds the bundle, all in one go.
public static class BuildAssetBundles
{
    private const string BundleName = "alieninvasion.bundle";
    private const string OutDir = "AssetBundles";

    private const string MothershipFbx = "Assets/Models/Mothership.fbx";
    private const string TripodFbx = "Assets/Models/Tripod.fbx";
    private const string DecalPrefab = "Assets/Models/ContaminationDecal.prefab";

    // For running by hand from the menu, where the GUI does work.
    [MenuItem("AlienInvasion/Build AssetBundle")]
    public static void Build()
    {
        BuildHeadless();
    }

    // The entry point called from batchmode with -executeMethod.
    public static void BuildHeadless()
    {
        Debug.Log("[AI-Build] start");

        AssetDatabase.Refresh();

        // 1) Assign the bundle name to the FBX files; the prefab name is the file name,
        //    Mothership or Tripod.
        AssignBundle(MothershipFbx);
        AssignBundle(TripodFbx);

        // 2) Create the red contamination decal prefab. No FBX is needed: it is a translucent
        //    red quad laid flat on the ground.
        CreateDecalPrefab();
        AssignBundle(DecalPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3) Build the bundle.
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
        BuildPipeline.BuildAssetBundles(OutDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);

        string built = Path.Combine(OutDir, BundleName);
        Debug.Log("[AI-Build] done. exists=" + File.Exists(built) + " path=" + Path.GetFullPath(built));
    }

    private static void AssignBundle(string assetPath)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            Debug.LogError("[AI-Build] asset not found: " + assetPath);
            return;
        }
        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();
        Debug.Log("[AI-Build] bundle assigned: " + assetPath);
    }

    private static void CreateDecalPrefab()
    {
        if (File.Exists(DecalPrefab)) return; // reuse it if it already exists

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ContaminationDecal";
        // A quad lies in the XY plane by default, so it is rotated +90 degrees about X to lie
        // flat on the ground, in the XZ plane.
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // No collider is needed.
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        // A translucent red material, Standard in its Transparent mode.
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = new Color(1f, 0.05f, 0.05f, 0.5f);
        AssetDatabase.CreateAsset(mat, "Assets/Models/ContaminationDecalMat.mat");
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;

        PrefabUtility.CreatePrefab(DecalPrefab, go);
        Object.DestroyImmediate(go);
        Debug.Log("[AI-Build] decal prefab created: " + DecalPrefab);
    }
}
