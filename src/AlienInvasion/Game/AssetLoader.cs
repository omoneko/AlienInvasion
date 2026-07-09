using System.IO;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Mod同梱の AssetBundle から prefab をロードする。見つからない場合は静かにスキップ。</summary>
    public static class AssetLoader
    {
        private static AssetBundle _bundle;
        private static bool _initialized;

        public static bool IsAvailable
        {
            get { return _bundle != null; }
        }

        public static void Initialize(string modAssemblyDirectory)
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                string path = Path.Combine(Path.Combine(modAssemblyDirectory, "Assets"), ModConfig.AssetBundleFileName);
                if (!File.Exists(path))
                {
                    ModConfig.Log("AssetBundle not found at " + path + " — visuals will be skipped");
                    return;
                }
                _bundle = AssetBundle.LoadFromFile(path);
                if (_bundle == null)
                {
                    ModConfig.LogError("AssetBundle.LoadFromFile returned null for " + path);
                    return;
                }
                ModConfig.Log("AssetBundle loaded from " + path);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("AssetLoader.Initialize error: " + e);
            }
        }

        public static GameObject GetPrefab(string name)
        {
            if (_bundle == null) return null;
            try
            {
                return _bundle.LoadAsset<GameObject>(name);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("GetPrefab(" + name + ") error: " + e);
                return null;
            }
        }
    }
}
