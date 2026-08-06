using System.Collections.Generic;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>The ledger of contamination zones, and applying, holding and clearing them on the grid.</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();
        private static readonly object _lock = new object();

        /// <summary>
        /// _zones is touched from the main thread, which reads it in Sync, and from the
        /// simulation thread, which writes it in AddZone and RemoveZoneAt. List&lt;T&gt; is not
        /// thread safe, so _lock guards it.
        /// The lock covers only reading and writing _zones; the calls into GridMath,
        /// PollutionField and NaturalResourceManager are made outside it.
        /// </summary>
        public static List<ContaminationZone> Zones
        {
            get { lock (_lock) { return new List<ContaminationZone>(_zones); } }
        }

        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            List<ContaminationZone> newZones = zones ?? new List<ContaminationZone>();
            lock (_lock) { _zones = newZones; }
            for (int i = 0; i < newZones.Count; i++) ReassertZone(newZones[i]);
        }

        public static void AddZone(ContaminationZone zone)
        {
            lock (_lock) { _zones.Add(zone); }
            ReassertZone(zone);
        }

        public static void RemoveZoneAt(int index)
        {
            lock (_lock)
            {
                if (index >= 0 && index < _zones.Count) _zones.RemoveAt(index);
            }
        }

        public static void ReassertZone(ContaminationZone zone)
        {
            var cells = GridMath.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius);
            for (int i = 0; i < cells.Count; i++) PollutionField.ApplyMax(cells[i], ModConfig.MaxPollution);
            RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            var cells = GridMath.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius);
            for (int i = 0; i < cells.Count; i++) PollutionField.ClearCell(cells[i]);
            RefreshZoneTexture(zone);
        }

        public static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / GridMath.CellSize) + 1;
            int cx = GridMath.WorldToCell(zone.CenterX);
            int cz = GridMath.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > GridMath.Resolution - 1) return GridMath.Resolution - 1;
            return v;
        }
    }
}
