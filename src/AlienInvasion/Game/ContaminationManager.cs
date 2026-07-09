using System.Collections.Generic;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>汚染ゾーン台帳と、グリッドへの適用/維持/除去。</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();

        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        public static void AddZone(ContaminationZone zone)
        {
            _zones.Add(zone);
            ReassertZone(zone);
        }

        public static void RemoveZoneAt(int index)
        {
            if (index >= 0 && index < _zones.Count) _zones.RemoveAt(index);
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
