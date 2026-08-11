using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Static coordinator that runs several invasions at once, holding up to
    /// MaxConcurrentInvasions of them in a fixed-length array of slots.
    ///
    /// Thread discipline, the single-invasion design extended to an array:
    /// - Every write to the _slots array - creating, removing, clearing - happens on the main
    ///   thread and nowhere else, so there is exactly one writer. StartInvasion, UpdateVisual
    ///   (which nulls a finished slot) and ResetForNewLevel are all main thread.
    /// - UpdateSimulation, on the simulation thread, only copies each slot reference to a local
    ///   and reads that. Reference assignment is atomic, so even if the main thread nulls the
    ///   same slot in between, the copied reference cannot throw a NullReferenceException; the
    ///   worst case is processing an already finished Invasion for one more tick, which is a
    ///   benign race.
    /// No lock is needed, because there is only ever one writer.
    /// </summary>
    public static class InvasionManager
    {
        private static readonly Invasion[] _slots = new Invasion[ModConfig.MaxConcurrentInvasions];

        /// <summary>Whether any invasion is underway, used to hold back the random trigger among other things.</summary>
        public static bool IsActive
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null) return true;
                }
                return false;
            }
        }

        /// <summary>How many invasions are currently underway.</summary>
        public static int ActiveCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null) n++;
                }
                return n;
            }
        }

        /// <summary>
        /// Whether another invasion can start, i.e. whether it is below the cap.
        /// The cap is the player's setting, not the array length: _slots is sized once at static
        /// init and never grows, so ModSettings.MaxConcurrent is clamped to it and only ever
        /// restricts how many of those slots may be used.
        /// </summary>
        public static bool CanStartMore
        {
            get { return ActiveCount < ModSettings.MaxConcurrent; }
        }

        /// <summary>
        /// Main thread only. Starts a new invasion if the cap allows it, and does nothing
        /// otherwise. It constructs a Mothership - Object.Instantiate plus transform work - so it
        /// must never be called from the simulation thread.
        /// <para>
        /// Lowering the cap while invasions are running never invalidates them: they finish
        /// normally, and the next request is what gets refused.
        /// </para>
        /// </summary>
        public static void StartInvasion(Vector3 targetPosition)
        {
            int cap = ModSettings.MaxConcurrent;
            if (ActiveCount >= cap)
            {
                ModConfig.Log("Invasion request ignored: already at max concurrent (" + cap + ")");
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = new Invasion(targetPosition);
                    ModConfig.Log("Invasion started at " + targetPosition + " (" + ActiveCount + "/" + cap + ")");
                    return;
                }
            }
            ModConfig.Log("Invasion request ignored: no free slot (" + _slots.Length + ")");
        }

        /// <summary>Main thread only. Advances every slot's visuals by one frame and removes the ones that have finished.</summary>
        public static void UpdateVisual(float simTimeDelta)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Invasion inv = _slots[i];
                if (inv == null) continue;
                bool stillActive = inv.UpdateVisual(simTimeDelta);
                if (!stillActive) _slots[i] = null;
            }
        }

        /// <summary>
        /// A representative position of the active tripods, if any invasion has them out, for
        /// placing the movement sound. Main thread only, since it reads the slots and the state
        /// of each Invasion and TripodGroup.
        /// </summary>
        public static bool TryGetAnyTripodPosition(out Vector3 pos)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Invasion inv = _slots[i];
                if (inv != null && inv.TryGetTripodPosition(out pos)) return true;
            }
            pos = default(Vector3);
            return false;
        }

        /// <summary>Simulation thread only. Advances the destruction and contamination for every slot.</summary>
        public static void UpdateSimulation()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                Invasion inv = _slots[i]; // copied to a local; see the class comment on the benign race
                if (inv == null) continue;
                inv.UpdateSimulation();
            }
        }

        /// <summary>
        /// Called only on level load, from InvasionDataExtension.OnLoadData, on the main
        /// thread. Switching to a different save would otherwise leave this class's static
        /// state behind to interfere with the new level, so every invasion in progress is
        /// discarded and the slots are emptied. An invasion is not persisted to the save, so
        /// resetting - rather than resuming - is the correct behaviour.
        /// </summary>
        public static void ResetForNewLevel()
        {
            try
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        _slots[i].ForceCleanup();
                        _slots[i] = null;
                    }
                }
                BeamStrikeLog.ResetForNewLevel();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionManager.ResetForNewLevel error: " + e);
            }
        }

        /// <summary>
        /// Public API for other mods (CS:WARFRONT) to repel an invasion. If any invasion is in
        /// progress, every slot is cleaned up exactly as ResetForNewLevel does (ForceCleanup)
        /// and true is returned to say they were repelled; with nothing in progress it does
        /// nothing and returns false. Main thread only, because it destroys GameObjects.
        /// </summary>
        public static bool Defeat()
        {
            if (!IsActive) return false;
            try
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        _slots[i].ForceCleanup();
                        _slots[i] = null;
                    }
                }
                ModConfig.Log("InvasionManager.Defeat: every tripod was repelled.");
                return true;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InvasionManager.Defeat error: " + e);
                return false;
            }
        }
    }
}
