using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Single entry point for loading and playing the sounds; Mod.OnEnabled calls Initialize.
    /// The actual loading and playback is done by SoundLoaderBehaviour, on a long-lived
    /// GameObject. Main thread only.
    /// </summary>
    public static class SoundManager
    {
        private static SoundLoaderBehaviour _behaviour;

        public static void Initialize(string modDirectory)
        {
            try
            {
                if (_behaviour != null) return;
                string dir = Path.Combine(modDirectory, ModConfig.SoundsFolderName);

                var go = new GameObject("AlienInvasion_SoundManager");
                Object.DontDestroyOnLoad(go);
                _behaviour = go.AddComponent<SoundLoaderBehaviour>();
                _behaviour.BeginLoad(dir);
                ModConfig.Log("SoundManager initialized: " + dir);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("SoundManager.Initialize error: " + e);
            }
        }

        /// <summary>Played once when the mothership arrives. It is 2D, so it reads as an announcement.</summary>
        public static void PlayUfoArrival(Vector3 pos)
        {
            if (_behaviour != null) _behaviour.PlayUfoArrival();
        }

        /// <summary>
        /// Drives the tripod movement sound every frame (main thread only). A single long-lived
        /// AudioSource plays it, so it never overlaps, and it stops while the game is paused.
        /// hasTripod says whether any tripods are active, pos is a representative position,
        /// paused is whether the game is paused, and dt is the real-time delta.
        /// </summary>
        public static void UpdateTripodAmbience(bool hasTripod, Vector3 pos, bool paused, float dt)
        {
            if (_behaviour != null) _behaviour.UpdateTripodAmbience(hasTripod, pos, paused, dt);
        }
    }
}
