using System.IO;
using UnityEngine;

namespace AlienInvasion.Game.UI
{
    /// <summary>
    /// タブ内アイコンのテクスチャを用意する。Mod配置フォルダに icon.png があればそれを読み込み、
    /// 無ければ空飛ぶ円盤(UFO)の簡易アイコン（透明背景）を手続き生成する。
    /// </summary>
    public static class UfoIcon
    {
        private static string _modDir;

        /// <summary>Mod配置フォルダを設定する（Mod.OnEnabled から）。icon.png の探索に使う。</summary>
        public static void SetModDirectory(string dir) { _modDir = dir; }

        /// <summary>icon.png があれば読み込んで返す。無ければ null。</summary>
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
                ModConfig.Log("タブアイコンに icon.png を使用します");
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
            Color disc = new Color32(200, 206, 216, 255);   // 円盤(シルバー)
            Color dome = new Color32(140, 205, 235, 255);   // ドーム(水色)
            Color light = new Color32(255, 226, 90, 255);   // 灯り(黄)
            float cx = (size - 1) * 0.5f;

            // 灯りの中心(正規化)と半径
            float[] lightX = { -0.28f, 0f, 0.28f };
            const float discCy = 0.40f, discRx = 0.46f, discRy = 0.12f;
            const float domeRx = 0.22f, domeRy = 0.30f;
            const float lightR2 = 0.0016f; // (0.04)^2

            for (int yy = 0; yy < size; yy++)
            {
                for (int xx = 0; xx < size; xx++)
                {
                    float fx = (xx - cx) / size; // -0.5..0.5
                    float fy = (float)yy / size; // 0(下)..1(上)
                    Color c = clear;

                    // 円盤（平たい楕円）
                    float bx = fx / discRx, by = (fy - discCy) / discRy;
                    bool inDisc = bx * bx + by * by <= 1f;
                    if (inDisc) c = disc;

                    // ドーム（円盤中心より上側の楕円）
                    float dx = fx / domeRx, dy = (fy - discCy) / domeRy;
                    if (fy >= discCy && dx * dx + dy * dy <= 1f) c = dome;

                    // 灯り（円盤上に）
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
