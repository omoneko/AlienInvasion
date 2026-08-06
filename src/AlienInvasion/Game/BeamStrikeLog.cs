using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// A log of the tripods' laser shots, published for other mods (CS:WARFRONT among them) to
    /// read by reflection. It uses the same "monotonic ID plus a float[] snapshot" pattern as
    /// GodzillaDisaster's RayStrikeLog.
    ///
    /// Record format: N records of five elements, {id, startX, startZ, endX, endZ}, newest
    /// first. start is where the tripod stands and end is the impact point, both as world X and
    /// Z. The ID rises monotonically for the life of the process and is never reset, not even
    /// on reloading a level, so a reader only has to ignore anything at or below the last ID it
    /// saw.
    ///
    /// Threading: Record is called from Tripod.FireBeam on the main thread, while CurrentId and
    /// Snapshot may be called from another mod's simulation thread, so every public member is
    /// lock-protected.
    /// </summary>
    public static class BeamStrikeLog
    {
        private const int MaxKept = 16; // more than the kaiju keeps, since several tripods fire at regular intervals

        private static readonly object _lock = new object();
        private static readonly List<float[]> _strikes = new List<float[]>(); // newest first
        private static long _currentId;

        /// <summary>The latest shot ID; 0 means nothing has fired yet.</summary>
        public static long CurrentId()
        {
            lock (_lock) { return _currentId; }
        }

        /// <summary>A snapshot of the log: N records of {id, startX, startZ, endX, endZ}, newest first.</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] arr = new float[_strikes.Count * 5];
                for (int i = 0; i < _strikes.Count; i++)
                    Array.Copy(_strikes[i], 0, arr, i * 5, 5);
                return arr;
            }
        }

        /// <summary>Records a laser shot. Called from Tripod.FireBeam on the main thread.</summary>
        public static void Record(Vector3 from, Vector3 to)
        {
            lock (_lock)
            {
                _currentId++;
                _strikes.Insert(0, new float[] { _currentId, from.x, from.z, to.x, to.z });
                if (_strikes.Count > MaxKept) _strikes.RemoveAt(_strikes.Count - 1);
            }
        }

        /// <summary>Called on level load from InvasionManager.ResetForNewLevel. The records are cleared but the ID is left where it is.</summary>
        public static void ResetForNewLevel()
        {
            lock (_lock) { _strikes.Clear(); }
        }
    }
}
