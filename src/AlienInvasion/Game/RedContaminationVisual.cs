using System.Collections.Generic;
using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// 汚染ゾーンに対応する赤いデカールGameObjectを配置/撤去する。
    /// GameObjectを直接操作するため、必ずメインスレッド(OnUpdate)から呼ぶこと。
    /// </summary>
    public static class RedContaminationVisual
    {
        private static readonly Dictionary<int, GameObject> _decals = new Dictionary<int, GameObject>();

        public static void Sync(List<ContaminationZone> activeZones)
        {
            try
            {
                var wanted = new HashSet<int>();
                for (int i = 0; i < activeZones.Count; i++)
                {
                    ContaminationZone zone = activeZones[i];
                    int key = ZoneKey(zone);
                    wanted.Add(key);
                    if (!_decals.ContainsKey(key))
                    {
                        GameObject decal = SpawnDecal(zone);
                        if (decal != null) _decals[key] = decal;
                    }
                }

                var toRemove = new List<int>();
                foreach (var kv in _decals)
                {
                    if (!wanted.Contains(kv.Key)) toRemove.Add(kv.Key);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    DestroyDecal(toRemove[i]);
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("RedContaminationVisual.Sync error: " + e);
            }
        }

        public static void Clear()
        {
            var keys = new List<int>(_decals.Keys);
            for (int i = 0; i < keys.Count; i++) DestroyDecal(keys[i]);
        }

        private static GameObject SpawnDecal(ContaminationZone zone)
        {
            GameObject instance = ModelProvider.CreateInstance(ModConfig.RedDecalPrefabName);
            if (instance == null) return null;

            float y = Singleton<TerrainManager>.instance.SampleDetailHeight(new Vector3(zone.CenterX, 0f, zone.CenterZ));
            instance.transform.position = new Vector3(zone.CenterX, y + ModConfig.RedDecalYOffset, zone.CenterZ);
            instance.transform.localScale = new Vector3(zone.Radius * 2f, 1f, zone.Radius * 2f);
            return instance;
        }

        private static void DestroyDecal(int key)
        {
            GameObject go;
            if (_decals.TryGetValue(key, out go))
            {
                if (go != null) Object.Destroy(go);
                _decals.Remove(key);
            }
        }

        private static int ZoneKey(ContaminationZone zone)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + zone.CenterX.GetHashCode();
                hash = hash * 31 + zone.CenterZ.GetHashCode();
                hash = hash * 31 + zone.StartTicks.GetHashCode();
                return hash;
            }
        }
    }
}
