using System;
using System.Collections.Generic;
using System.IO;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// モデルGameObject生成の単一窓口。AssetBundle(AssetLoader)が使える場合はそれを優先し、
    /// 使えない場合は Mod配置フォルダ/Models/&lt;name&gt;.obj(+.mtl) から実行時にメッシュを構築して
    /// キャッシュする。赤デカールのみ、OBJも無い場合に手続き生成のQuadへフォールバックする。
    /// GameObject/Mesh/Material の生成を伴うため、必ずメインスレッドから呼ぶこと
    /// (Mothership/Tripodのコンストラクタ、RedContaminationVisual.Syncと同じスレッド)。
    /// </summary>
    public static class ModelProvider
    {
        private class BuiltModel
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        private static string _modDirectory;
        private static bool _initialized;
        private static readonly Dictionary<string, BuiltModel> _cache = new Dictionary<string, BuiltModel>();
        private static Material _decalMaterial;

        public static void Initialize(string modDirectory)
        {
            if (_initialized) return;
            _initialized = true;
            _modDirectory = modDirectory;
        }

        /// <summary>指定名のモデルの新しいインスタンスを返す。生成できなければ null(呼び出し側は既存通りnull安全)。</summary>
        public static GameObject CreateInstance(string name)
        {
            try
            {
                if (AssetLoader.IsAvailable)
                {
                    GameObject prefab = AssetLoader.GetPrefab(name);
                    if (prefab != null)
                    {
                        return UnityEngine.Object.Instantiate(prefab);
                    }
                }

                BuiltModel cached;
                if (!_cache.TryGetValue(name, out cached))
                {
                    cached = BuildFromObj(name);
                    if (cached != null) _cache[name] = cached;
                }

                if (cached != null)
                {
                    return InstantiateBuilt(name, cached);
                }

                if (name == ModConfig.RedDecalPrefabName)
                {
                    return CreateProceduralDecal();
                }

                return null;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.CreateInstance(" + name + ") error: " + e);
                return null;
            }
        }

        private static GameObject InstantiateBuilt(string name, BuiltModel model)
        {
            try
            {
                var go = new GameObject("AlienInvasion_" + name);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = model.Mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = model.Materials;
                return go;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.InstantiateBuilt(" + name + ") error: " + e);
                return null;
            }
        }

        private static BuiltModel BuildFromObj(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(_modDirectory))
                {
                    ModConfig.LogError("ModelProvider.BuildFromObj: modDirectory 未初期化 (ModelProvider.Initialize未呼び出し)");
                    return null;
                }

                string modelsDir = Path.Combine(_modDirectory, ModConfig.ModelsFolderName);
                string objPath = Path.Combine(modelsDir, name + ".obj");
                if (!File.Exists(objPath))
                {
                    return null;
                }

                string objText = File.ReadAllText(objPath);
                ObjData data = ObjParser.Parse(objText);

                Dictionary<string, MtlColor> mtl = null;
                string mtlPath = Path.Combine(modelsDir, name + ".mtl");
                if (File.Exists(mtlPath))
                {
                    string mtlText = File.ReadAllText(mtlPath);
                    mtl = MtlParser.Parse(mtlText);
                }

                Mesh mesh;
                Material[] materials;
                if (!ObjMeshBuilder.TryBuild(data, mtl, ModConfig.ObjFallbackColor, out mesh, out materials))
                {
                    ModConfig.LogError("ModelProvider: OBJからのメッシュ構築に失敗 name=" + name + " path=" + objPath);
                    return null;
                }

                ModConfig.Log("ModelProvider: OBJからモデルを構築しました name=" + name);
                var built = new BuiltModel();
                built.Mesh = mesh;
                built.Materials = materials;
                return built;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.BuildFromObj(" + name + ") error: " + e);
                return null;
            }
        }

        private static GameObject CreateProceduralDecal()
        {
            try
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "AlienInvasion_" + ModConfig.RedDecalPrefabName;

                Collider collider = quad.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);

                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material mat = GetDecalMaterial();
                    if (mat != null) renderer.sharedMaterial = mat;
                }

                return quad;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.CreateProceduralDecal error: " + e);
                return null;
            }
        }

        private static Material GetDecalMaterial()
        {
            if (_decalMaterial != null) return _decalMaterial;

            try
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader == null) return null;

                Material mat = new Material(shader);
                Color c = ModConfig.RedDecalColor;
                mat.color = c;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

                if (c.a < 1f)
                {
                    ObjMeshBuilder.ApplyTransparency(mat, c.a);
                }

                _decalMaterial = mat;
                return _decalMaterial;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.GetDecalMaterial error: " + e);
                return null;
            }
        }
    }
}
