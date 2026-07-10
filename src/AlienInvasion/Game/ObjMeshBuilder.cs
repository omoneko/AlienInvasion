using System;
using System.Collections.Generic;
using AlienInvasion.Core;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// AlienInvasion.Core が解析した ObjData/MtlColor から、実行時に Unity の Mesh/Material を
    /// 構築する。Mesh/Material/Shader の生成は Unity のメインスレッドでのみ許可されるため、
    /// このクラスの呼び出しは必ずメインスレッド(GameObjectを生成する箇所と同じスレッド)から行うこと。
    /// </summary>
    public static class ObjMeshBuilder
    {
        public static bool TryBuild(ObjData obj, Dictionary<string, MtlColor> mtl, Color fallbackColor, out Mesh mesh, out Material[] materials)
        {
            mesh = null;
            materials = null;

            try
            {
                if (obj == null || obj.Positions == null || obj.Submeshes == null) return false;
                int vertexCount = obj.VertexCount;
                if (vertexCount <= 0 || obj.Submeshes.Count == 0) return false;

                var vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vector3(
                        obj.Positions[i * 3],
                        obj.Positions[i * 3 + 1],
                        obj.Positions[i * 3 + 2]);
                }

                var builtMesh = new Mesh();
                builtMesh.vertices = vertices;
                builtMesh.subMeshCount = obj.Submeshes.Count;

                var mats = new Material[obj.Submeshes.Count];

                for (int s = 0; s < obj.Submeshes.Count; s++)
                {
                    ObjSubmesh sub = obj.Submeshes[s];
                    List<int> validTriangles = FilterValidTriangles(sub != null ? sub.Triangles : null, vertexCount);
                    builtMesh.SetTriangles(validTriangles, s);
                    mats[s] = BuildMaterial(sub != null ? sub.Material : null, mtl, fallbackColor);
                }

                builtMesh.RecalculateNormals();
                builtMesh.RecalculateBounds();

                mesh = builtMesh;
                materials = mats;
                return true;
            }
            catch (Exception e)
            {
                ModConfig.LogError("ObjMeshBuilder.TryBuild error: " + e);
                mesh = null;
                materials = null;
                return false;
            }
        }

        /// <summary>破損/範囲外インデックスの三角形を除去する。Unity の SetTriangles は範囲外
        /// インデックスがあると例外を投げるため、必ずこのフィルタを通してから渡す。</summary>
        private static List<int> FilterValidTriangles(List<int> triangles, int vertexCount)
        {
            if (triangles == null || triangles.Count == 0) return new List<int>();

            var valid = new List<int>(triangles.Count);
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || a >= vertexCount) continue;
                if (b < 0 || b >= vertexCount) continue;
                if (c < 0 || c >= vertexCount) continue;

                valid.Add(a);
                valid.Add(b);
                valid.Add(c);
            }
            return valid;
        }

        private static Material BuildMaterial(string materialName, Dictionary<string, MtlColor> mtl, Color fallbackColor)
        {
            Material mat = CreateBaseMaterial();
            if (mat == null) return null;

            try
            {
                float r = fallbackColor.r, g = fallbackColor.g, b = fallbackColor.b, a = 1f;

                MtlColor found;
                if (mtl != null && !string.IsNullOrEmpty(materialName) && mtl.TryGetValue(materialName, out found) && found != null)
                {
                    r = found.R;
                    g = found.G;
                    b = found.B;
                    a = found.Alpha;
                }

                Color color = new Color(r, g, b, a);
                mat.color = color;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", ModConfig.ObjMetallic);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", ModConfig.ObjGlossiness);

                if (a < 1f)
                {
                    ApplyTransparency(mat, a);
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("ObjMeshBuilder.BuildMaterial error: " + e);
            }

            return mat;
        }

        private static Material CreateBaseMaterial()
        {
            try
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader == null) return null;
                return new Material(shader);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ObjMeshBuilder.CreateBaseMaterial error: " + e);
                return null;
            }
        }

        /// <summary>Standardシェーダのマテリアルを半透明(Transparent)モードに設定する。
        /// 手続き生成デカール等、他クラスからも再利用できるよう public にしてある。</summary>
        public static void ApplyTransparency(Material mat, float alpha)
        {
            if (mat == null) return;
            try
            {
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3f); // Standard: Transparent
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;

                Color c = mat.color;
                c.a = alpha;
                mat.color = c;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ObjMeshBuilder.ApplyTransparency error: " + e);
            }
        }
    }
}
