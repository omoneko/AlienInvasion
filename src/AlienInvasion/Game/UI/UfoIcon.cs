using System.IO;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// Supplies the texture for the icon in the panel. If icon.png is present in the mod
    /// folder it is loaded; otherwise a simple flying saucer is generated procedurally, on a
    /// transparent background.
    /// </summary>
    public static class UfoIcon
    {
        private static string _modDir;

        /// <summary>Sets the mod folder, from Mod.OnEnabled. Used to look for icon.png.</summary>
        public static void SetModDirectory(string dir) { _modDir = dir; }

        /// <summary>Loads icon.png if it exists, otherwise null.</summary>
        private static Texture2D TryLoadPng()
        {
            try
            {
                if (string.IsNullOrEmpty(_modDir)) return null;
                string path = Path.Combine(_modDir, "icon.png");
                if (!File.Exists(path)) return null;
                var t = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                t.wrapMode = TextureWrapMode.Clamp;
                if (!t.LoadImage(File.ReadAllBytes(path))) { Object.Destroy(t); return null; }
                ModConfig.Log("using icon.png for the panel icon");
                return t;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("UfoIcon.TryLoadPng error: " + e);
                return null;
            }
        }

        public static Texture2D Build(int size)
        {
            Texture2D png = TryLoadPng();
            if (png != null) return png;

            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color disc = new Color32(200, 206, 216, 255);   // the saucer, in silver
            Color dome = new Color32(140, 205, 235, 255);   // the dome, in pale blue
            Color light = new Color32(255, 226, 90, 255);   // the lights, in yellow
            float cx = (size - 1) * 0.5f;

            // Centres of the lights, normalised, and their radius
            float[] lightX = { -0.28f, 0f, 0.28f };
            const float discCy = 0.40f, discRx = 0.46f, discRy = 0.12f;
            const float domeRx = 0.22f, domeRy = 0.30f;
            const float lightR2 = 0.0016f; // (0.04)^2

            for (int yy = 0; yy < size; yy++)
            {
                for (int xx = 0; xx < size; xx++)
                {
                    float fx = (xx - cx) / size; // -0.5..0.5
                    float fy = (float)yy / size; // 0 at the bottom .. 1 at the top
                    Color c = clear;

                    // The saucer, a flattened ellipse
                    float bx = fx / discRx, by = (fy - discCy) / discRy;
                    bool inDisc = bx * bx + by * by <= 1f;
                    if (inDisc) c = disc;

                    // The dome, an ellipse above the centre of the saucer
                    float dx = fx / domeRx, dy = (fy - discCy) / domeRy;
                    if (fy >= discCy && dx * dx + dy * dy <= 1f) c = dome;

                    // The lights, set into the saucer
                    if (inDisc)
                    {
                        for (int k = 0; k < lightX.Length; k++)
                        {
                            float ex = fx - lightX[k], ey = fy - discCy;
                            if (ex * ex + ey * ey <= lightR2) { c = light; break; }
                        }
                    }

                    px[yy * size + xx] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
