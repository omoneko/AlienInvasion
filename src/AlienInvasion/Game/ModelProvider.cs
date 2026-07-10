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
                RenderAssets.DumpAvailableShadersOnce();
                // 透過(テクスチャのアルファで隙間を作る有機的なレッドウィード)を確実に出すため、
                // アルファブレンドが素直に効くシェーダーを優先する。Standardの透過はCSランタイムでは
                // キーワード除去により効かず「べた塗りの赤い矩形」になりがちなので後回しにする。
                Shader shader = RenderAssets.FindFirst(
                    "Unlit/Transparent", "Sprites/Default", "Particles/Alpha Blended",
                    "Legacy Shaders/Transparent/Diffuse", "Transparent/Diffuse");
                bool transparentCapable = shader != null;
                if (shader == null) shader = RenderAssets.FindLoadedContaining("transparent", "sprite", "unlit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader == null) return null;
                ModConfig.Log("Decal shader = " + shader.name + " (transparent-capable=" + transparentCapable + ")");

                Material mat = new Material(shader);
                Color c = ModConfig.RedDecalColor;

                // 宇宙戦争の"レッドウィード"風。Perlinノイズで血管/ツタ状の有機的な赤い繁茂を生成し、
                // 外周へ放射状にフェード、薄い部分は透明にして地面が透ける隙間を作る。
                Texture2D tex = BuildContaminationTexture(c, 256);
                mat.mainTexture = tex;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

                // 透明度はテクスチャのアルファで表現するので、色ティントは白(不透明)にする。
                Color white = Color.white;
                mat.color = white;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", white);

                ObjMeshBuilder.ApplyTransparency(mat, 1f);

                _decalMaterial = mat;
                return _decalMaterial;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ModelProvider.GetDecalMaterial error: " + e);
                return null;
            }
        }

        /// <summary>
        /// 宇宙戦争の"レッドウィード"風の汚染テクスチャを生成する。
        /// Perlinノイズのfbm(斑)とridged(血管/ツタ状の稜線)を合成した密度で、深いクリムゾンから
        /// tint色(オレンジ寄り赤)へ色を補間。アルファは外周への放射状フェード×密度で、薄い部分は
        /// 透明にして地面が透ける有機的な隙間を作る。tint.a を中心のピーク不透明度として使う。
        /// </summary>
        private static Texture2D BuildContaminationTexture(Color tint, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = (size - 1) * 0.5f;

            Color baseCol = new Color(tint.r * 0.35f, tint.g * 0.12f, tint.b * 0.10f); // 深いクリムゾン
            Color highCol = new Color(tint.r, tint.g, tint.b);                          // オレンジ寄り赤
            float peak = tint.a;

            const float freq = 4.5f;   // 模様の粗さ
            const float off = 13.37f;  // ラティス格子アーティファクト回避のオフセット

            var pixels = new Color[size * size]; // SetPixels一括(SetPixelのループより高速)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    // 四角ベースのフォールオフ(チェビシェフ距離=max(|dx|,|dy|))。円形(ユークリッド距離)だと
                    // 正方形の四隅が切れて円形に見えるが、これだと四辺まで均等に届き四角い外形になる。
                    float dmax = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    float radial = Mathf.Clamp01(1f - dmax);
                    radial = radial * radial; // 外周(四辺)ほど柔らかくフェード=輪郭ぼんやり

                    float fb = Fbm(u * freq + off, v * freq + off);                                   // 斑
                    float rg = 1f - Mathf.Abs(2f * Fbm(u * freq * 2f + off * 2f, v * freq * 2f + off * 2f) - 1f); // 稜線(血管/ツタ状)
                    float density = Mathf.Clamp01(fb * 0.55f + rg * 0.55f);

                    Color col = Color.Lerp(baseCol, highCol, Mathf.Clamp01(density * 1.2f));

                    // 密度が低い所は透明(地面が透ける隙間)。中心ほど濃く、外周へフェード。
                    float a = radial * peak * Mathf.Clamp01((density - 0.25f) * 1.8f);

                    pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>4オクターブの Perlin ノイズによる fbm(概ね 0..1)。</summary>
        private static float Fbm(float x, float y)
        {
            float sum = 0f;
            float amp = 0.5f;
            float freq = 1f;
            for (int i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                freq *= 2f;
                amp *= 0.5f;
            }
            return sum;
        }
    }
}
