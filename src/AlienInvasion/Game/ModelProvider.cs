using System;
using System.Collections.Generic;
using System.IO;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Single entry point for creating model GameObjects. The AssetBundle, through AssetLoader,
    /// is preferred where it works; otherwise the mesh is built at runtime from
    /// Models/&lt;name&gt;.obj (and its .mtl) inside the mod folder and cached. The red decal alone
    /// has one more fallback: a procedurally generated quad, used when even the OBJ is missing.
    /// It creates GameObjects, Meshes and Materials, so it must be called from the main thread -
    /// the same thread as the Mothership and Tripod constructors and RedContaminationVisual.Sync.
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

        /// <summary>A new instance of the named model, or null if it could not be created. Every caller is null-safe.</summary>
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
                    ModConfig.LogError("ModelProvider.BuildFromObj: modDirectory is not set (ModelProvider.Initialize was never called)");
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
                    ModConfig.LogError("ModelProvider: failed to build the mesh from the OBJ, name=" + name + " path=" + objPath);
                    return null;
                }

                ModConfig.Log("ModelProvider: built the model from its OBJ, name=" + name);
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
                // The red weed needs its transparency to work - the gaps come from the
                // texture's alpha - so a shader that alpha-blends straightforwardly is
                // preferred. Standard's transparency usually fails in the CS runtime because
                // the keywords were stripped, leaving a flat red rectangle, so it comes last.
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

                // Styled after the red weed from The War of the Worlds: Perlin noise grows an
                // organic red spread of veins and creepers, fading radially outwards, with the
                // thin parts left transparent so the ground shows through the gaps.
                Texture2D tex = BuildContaminationTexture(c, 256);
                mat.mainTexture = tex;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

                // The transparency lives in the texture's alpha, so the colour tint is left
                // white and opaque.
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
        /// Generates the contamination texture, styled after the red weed from The War of the
        /// Worlds. The density combines two forms of Perlin noise - fbm for the mottling and a
        /// ridged variant for the veins and creepers - and drives a colour interpolation from
        /// deep crimson to the tint colour, an orange-leaning red. The alpha is the radial
        /// fade towards the edge multiplied by that density, so the thin parts come out
        /// transparent and leave organic gaps for the ground to show through. tint.a is the
        /// peak opacity at the centre.
        /// </summary>
        private static Texture2D BuildContaminationTexture(Color tint, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = (size - 1) * 0.5f;

            Color baseCol = new Color(tint.r * 0.35f, tint.g * 0.12f, tint.b * 0.10f); // deep crimson
            Color highCol = new Color(tint.r, tint.g, tint.b);                          // orange-leaning red
            float peak = tint.a;

            const float freq = 4.5f;   // coarseness of the pattern
            const float off = 13.37f;  // offset that avoids artefacts on the noise lattice

            var pixels = new Color[size * size]; // filled in one SetPixels call, far faster than looping SetPixel
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    // A square falloff, using the Chebyshev distance max(|dx|,|dy|). A circular
                    // one - the Euclidean distance - cuts the corners off and reads as a disc;
                    // this reaches every edge evenly and keeps the square outline.
                    float dmax = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    float radial = Mathf.Clamp01(1f - dmax);
                    radial = radial * radial; // fades more softly towards the edges, blurring the outline

                    float fb = Fbm(u * freq + off, v * freq + off);                                   // the mottling
                    float rg = 1f - Mathf.Abs(2f * Fbm(u * freq * 2f + off * 2f, v * freq * 2f + off * 2f) - 1f); // the ridges, the veins and creepers
                    float density = Mathf.Clamp01(fb * 0.55f + rg * 0.55f);

                    Color col = Color.Lerp(baseCol, highCol, Mathf.Clamp01(density * 1.2f));

                    // Low density means transparent, leaving gaps for the ground. It is densest
                    // at the centre and fades outwards.
                    float a = radial * peak * Mathf.Clamp01((density - 0.25f) * 1.8f);

                    pixels[y * size + x] = new Color(col.r, col.g, col.b, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Four octaves of Perlin noise as fbm, roughly in 0..1.</summary>
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
