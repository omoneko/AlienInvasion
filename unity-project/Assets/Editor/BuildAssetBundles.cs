using UnityEditor;
using UnityEngine;
using System.IO;

// Alien Invasion 用 AssetBundle をヘッドレス(batchmode)で生成するエディタスクリプト。
// Unity 5.6 の GUI はオンラインライセンス認証でハングするため、GUIを開かず
//   Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod BuildAssetBundles.BuildHeadless -quit
// で実行する。FBXへのバンドル名割当・赤デカールprefab生成・ビルドまでを一括で行う。
public static class BuildAssetBundles
{
    private const string BundleName = "alieninvasion.bundle";
    private const string OutDir = "AssetBundles";

    private const string MothershipFbx = "Assets/Models/Mothership.fbx";
    private const string TripodFbx = "Assets/Models/Tripod.fbx";
    private const string DecalPrefab = "Assets/Models/ContaminationDecal.prefab";

    // メニューからの手動実行用(GUIが使える場合)。
    [MenuItem("AlienInvasion/Build AssetBundle")]
    public static void Build()
    {
        BuildHeadless();
    }

    // batchmode / -executeMethod から呼ぶ本体。
    public static void BuildHeadless()
    {
        Debug.Log("[AI-Build] start");

        AssetDatabase.Refresh();

        // 1) FBX にバンドル名を割り当てる(プレハブ名 = ファイル名 Mothership / Tripod)。
        AssignBundle(MothershipFbx);
        AssignBundle(TripodFbx);

        // 2) 赤い汚染デカール prefab を生成(FBX不要。地面に寝かせた赤い半透明Quad)。
        CreateDecalPrefab();
        AssignBundle(DecalPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3) バンドルをビルド。
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
        if (File.Exists(DecalPrefab)) return; // 既存なら再利用

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ContaminationDecal";
        // Quad は既定でXY平面。地面(XZ平面)に寝かせるためX軸+90度回転。
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // 当たり判定は不要。
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        // 赤い半透明マテリアル(Standard, Transparent)。
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
