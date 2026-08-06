using System;
using System.Text;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Utility for finding shaders that actually exist in the CS Unity runtime.
    ///
    /// CS usually strips built-in Unity shaders it does not reference itself -
    /// "Particles/Additive", or the transparent variants of Standard - so Shader.Find returns
    /// null for them. That shows up as:
    ///   - no material at all, and the object rendering in the magenta error colour, which is
    ///     what happened to the laser and the lightning
    ///   - transparency not working, so the texture's alpha is ignored and the result is a
    ///     flat block of colour, which is what happened to the red contamination
    ///
    /// This class works around it with:
    ///   - FindFirst: Shader.Find over the candidate names in order, returning the first hit
    ///   - FindLoadedContaining: a fallback that matches loaded shaders by substring
    ///   - DumpAvailableShadersOnce: logs the available shader names and whether each main
    ///     candidate resolved, so it is possible to establish exactly which shaders work in-game
    /// All of it touches GameObjects and Shaders, so it is main thread only.
    /// </summary>
    public static class RenderAssets
    {
        private static bool _dumped;

        /// <summary>Shader.Find over the candidate names in order, returning the first that exists, or null if none do.</summary>
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
                catch (Exception) { /* try the next candidate */ }
            }
            return null;
        }

        /// <summary>The first loaded shader whose name contains any of substrsLower, which are lowercase.</summary>
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

        /// <summary>Logs the relevant available shader names, and whether Shader.Find resolved each main candidate. Runs once.</summary>
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
