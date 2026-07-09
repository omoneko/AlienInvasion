using System.Collections.Generic;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>汚染ゾーン台帳と、グリッドへの適用/維持/除去。</summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();
        private static readonly object _lock = new object();

        /// <summary>
        /// _zones はメインスレッド(Sync読取)とシミュレーションスレッド(AddZone/RemoveZoneAt書込)の
        /// 双方から触られるため、List&lt;T&gt;の非スレッドセーフ性への対策として _lock で保護する。
        /// ロック範囲は _zones の読み書きのみに限定し、GridMath/PollutionField/NaturalResourceManager
        /// 呼び出しはロック外で行う。
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
