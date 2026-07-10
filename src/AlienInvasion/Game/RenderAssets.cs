using System;
using System.Text;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// CSのUnityランタイムで「実際に利用可能なシェーダー」を見つけるためのユーティリティ。
    ///
    /// CSは参照されていないUnity組み込みシェーダー(例: "Particles/Additive" や Standardの
    /// 透過バリアント)をビルドから除去していることが多く、Shader.Find がそれらに対して null を
    /// 返す。すると:
    ///   - マテリアルが付かず「マゼンタ(ピンク紫)」のエラー色で描画される(レーザー/雷)
    ///   - 透過設定が効かず、テクスチャのアルファ(隙間)が無視されて「べた塗り」になる(赤い汚染)
    /// といった不具合になる。
    ///
    /// そこで:
    ///   - FindFirst: 候補シェーダー名を順に Shader.Find し、最初に見つかったものを返す
    ///   - FindLoadedContaining: ロード済みシェーダー群を名前部分一致で拾うフォールバック
    ///   - DumpAvailableShadersOnce: 初回に、利用可能なシェーダー名と主要候補の可否をログ出力し、
    ///     実機で「どのシェーダーが使えるか」を正確に特定できるようにする
    /// を提供する。全てGameObject/Shaderに触れるためメインスレッド専用。
    /// </summary>
    public static class RenderAssets
    {
        private static bool _dumped;

        /// <summary>候補名を順に Shader.Find し、最初に見つかった(非null)シェーダーを返す。全滅なら null。</summary>
        public static Shader FindFirst(params string[] names)
        {
            if (names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    Shader s = Shader.Find(names[i]);
                    if (s != null) return s;
                }
                catch (Exception) { /* 次の候補へ */ }
            }
            return null;
        }

        /// <summary>ロード済みシェーダーから、名前に substrsLower のいずれか(小文字)を含む最初のものを返す。</summary>
        public static Shader FindLoadedContaining(params string[] substrsLower)
        {
            try
            {
                Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || string.IsNullOrEmpty(all[i].name)) continue;
                    string lower = all[i].name.ToLowerInvariant();
                    for (int j = 0; j < substrsLower.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(substrsLower[j]) && lower.Contains(substrsLower[j])) return all[i];
                    }
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.FindLoadedContaining error: " + e);
            }
            return null;
        }

        /// <summary>初回のみ、利用可能なシェーダー名(関連するもの)と主要候補の Shader.Find 可否をログ出力する。</summary>
        public static void DumpAvailableShadersOnce()
        {
            if (_dumped) return;
            _dumped = true;
            try
            {
                Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
                var sb = new StringBuilder();
                sb.Append("RenderAssets: loaded shader count=").Append(all.Length).Append("; relevant names: ");
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null) continue;
                    string nm = all[i].name;
                    if (string.IsNullOrEmpty(nm)) continue;
                    string l = nm.ToLowerInvariant();
                    if (l.Contains("particle") || l.Contains("additive") || l.Contains("unlit") ||
                        l.Contains("transparent") || l.Contains("decal") || l.Contains("sprite") ||
                        l.Contains("standard") || l.Contains("glow") || l.Contains("line") ||
                        l.Contains("blend"))
                    {
                        sb.Append('[').Append(nm).Append(']');
                        n++;
                    }
                }
                sb.Append(" (").Append(n).Append(" relevant)");
                ModConfig.Log(sb.ToString());

                ModConfig.Log("RenderAssets Shader.Find checks: " +
                    "Standard=" + (Shader.Find("Standard") != null) +
                    ", Particles/Additive=" + (Shader.Find("Particles/Additive") != null) +
                    ", Particles/Alpha Blended=" + (Shader.Find("Particles/Alpha Blended") != null) +
                    ", Sprites/Default=" + (Shader.Find("Sprites/Default") != null) +
                    ", Unlit/Transparent=" + (Shader.Find("Unlit/Transparent") != null) +
                    ", Unlit/Color=" + (Shader.Find("Unlit/Color") != null) +
                    ", Legacy Shaders/Transparent/Diffuse=" + (Shader.Find("Legacy Shaders/Transparent/Diffuse") != null));
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.DumpAvailableShadersOnce error: " + e);
            }
        }
    }
}
