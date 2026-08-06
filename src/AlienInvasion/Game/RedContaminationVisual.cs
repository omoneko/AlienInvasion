using System.Collections.Generic;
using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Places and removes the red decal GameObject that goes with each contamination zone.
    /// It works with GameObjects directly, so it must always be called from the main thread,
    /// via OnUpdate.
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
            // The decal is laid flat on the ground with Euler(90,0,0), so after that rotation
            // its local X maps to world X and its local Y maps to world Z. Covering the ground
            // therefore means a scale of (diameter, diameter, thickness), i.e.
            // (Radius*2, Radius*2, 1).
            // It used to be (Radius*2, 1, Radius*2), which left local Y - world Z - at 1 m and
            // drew a thin red bar instead.
            instance.transform.localScale = new Vector3(zone.Radius * 2f, zone.Radius * 2f, 1f);
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
