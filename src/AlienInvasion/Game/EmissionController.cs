using System.Collections.Generic;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Makes a model's coloured materials - everything but the metallic grey base - glow at
    /// night.
    ///
    /// How it works: while the model is built, ObjMeshBuilder registers each non-base material
    /// along with its own colour. Every frame, on the main thread, a factor is interpolated
    /// smoothly between 0 by day and 1 by night according to whether it is currently night
    /// (SimulationManager's m_enableDayNight and m_isNightTime), and each material's
    /// _EmissionColor is set to the registered colour times that factor times the intensity.
    ///
    /// ModelProvider caches and shares materials per model, so each kind of model registers
    /// exactly once regardless of how many instances exist, and one update here lights up every
    /// mothership or tripod of that kind at the same time.
    /// It touches GameObjects and Materials, so all of it is main thread only.
    /// </summary>
    public static class EmissionController
    {
        private struct Entry
        {
            public Material Mat;
            public Color Color;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static float _current; // 0 by day (dark) .. 1 by night (glowing)

        /// <summary>Registers a material to glow. Called once while the model is built. Main thread only.</summary>
        public static void Register(Material mat, Color emissionColor)
        {
            if (mat == null) return;
            try
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", Color.black); // starts dark, as during the day
                }
                _entries.Add(new Entry { Mat = mat, Color = emissionColor });
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("EmissionController.Register error: " + e);
            }
        }

        /// <summary>Updates the glow each frame from the time of day. The pause is ignored, because it should still glow at night while paused. Main thread only.</summary>
        public static void Update(float realTimeDelta)
        {
            if (_entries.Count == 0) return;
            try
            {
                float target = IsNight() ? 1f : 0f;
                _current = Mathf.MoveTowards(_current, target, ModConfig.EmissionFadePerSecond * realTimeDelta);
                float k = _current * ModConfig.NightEmissionIntensity;
                for (int i = 0; i < _entries.Count; i++)
                {
                    Material m = _entries[i].Mat;
                    if (m == null) continue;
                    if (m.HasProperty("_EmissionColor"))
                    {
                        Color c = _entries[i].Color;
                        m.SetColor("_EmissionColor", new Color(c.r * k, c.g * k, c.b * k, 1f));
                    }
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("EmissionController.Update error: " + e);
            }
        }

        private static bool IsNight()
        {
            try
            {
                SimulationManager sm = SimulationManager.instance;
                return sm != null && sm.m_enableDayNight && sm.m_isNightTime;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
