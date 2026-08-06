using System;
using System.Reflection;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Loosely coupled bridge that reads the Missile Disaster mod's nuclear impact beacon
    /// (MissileDisaster.Game.NuclearImpactBeacon) by reflection. Neither mod references the
    /// other's DLL, so the type is looked up by name in the AppDomain and only its published
    /// contract is used: the CurrentId property and the Snapshot() method.
    /// Without the Missile mod installed, Available is false and toppling from a direct nuclear
    /// hit is disabled entirely.
    ///
    /// The lookup happens once, on first access. By the time this is called - with tripods out
    /// and moving - every mod's assembly is loaded, so detecting it in one direction is enough.
    /// Every method here is called on the main thread.
    /// </summary>
    public static class NuclearImpactReader
    {
        private const string BeaconTypeName = "MissileDisaster.Game.NuclearImpactBeacon";
        private static readonly float[] Empty = new float[0];

        private static bool _resolved;
        private static bool _available;
        private static MethodInfo _snapshot;
        private static PropertyInfo _currentId;

        /// <summary>Whether the Missile mod's nuclear impact beacon is available; false if that mod is not installed.</summary>
        public static bool Available
        {
            get { Resolve(); return _available; }
        }

        /// <summary>The most recently issued nuclear impact ID; 0 means none yet, or the mod is absent. A cheap way to check for anything new.</summary>
        public static long CurrentId()
        {
            Resolve();
            if (!_available) return 0L;
            try
            {
                object v = _currentId.GetValue(null, null);
                return v is long ? (long)v : Convert.ToInt64(v);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearImpactReader.CurrentId error: " + e);
                _available = false;
                return 0L;
            }
        }

        /// <summary>The recent nuclear impacts as {id, x, z} triples, newest first. Empty when the mod is absent or nothing has landed.</summary>
        public static float[] Snapshot()
        {
            Resolve();
            if (!_available) return Empty;
            try
            {
                object v = _snapshot.Invoke(null, null);
                float[] arr = v as float[];
                return arr ?? Empty;
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearImpactReader.Snapshot error: " + e);
                _available = false;
                return Empty;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Type t = FindType(BeaconTypeName);
                if (t == null) { _available = false; return; }

                _snapshot = t.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                _currentId = t.GetProperty("CurrentId", BindingFlags.Public | BindingFlags.Static);
                _available = _snapshot != null && _currentId != null;

                if (_available)
                    ModConfig.Log("Missile Disaster mod detected: tripods will topple from a direct nuclear hit");
                else
                    ModConfig.Log("NuclearImpactBeacon was found but its members do not match the contract, so it is disabled");
            }
            catch (Exception e)
            {
                _available = false;
                ModConfig.LogError("NuclearImpactReader.Resolve error: " + e);
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    Type t = asms[i].GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { /* dynamic assemblies and the like - skip */ }
            }
            return null;
        }
    }
}
