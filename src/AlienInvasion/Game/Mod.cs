using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Alien Invasion";
        public string Description => "A UFO mothership descends, wrecks the city with lightning and a crater, then deploys roaming tripods that fire lasers and leave red contamination. Trigger it with the \"UFO !\" button or the F7 key (up to 5 at once).";

        public void OnEnabled()
        {
            // Assembly.GetExecutingAssembly().Location はCSのMod読み込み環境下で
            // 空文字等を返すことがあり Path.GetDirectoryName が例外を投げる(実際に発生した既知の不具合)。
            // 代わりにゲーム自身が管理する PluginManager から確実な modPath を取得する。
            try
            {
                PluginManager.PluginInfo info = Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                if (info != null && !string.IsNullOrEmpty(info.modPath))
                {
                    AssetLoader.Initialize(info.modPath);
                    ModelProvider.Initialize(info.modPath);
                    SoundManager.Initialize(info.modPath);
                }
                else
                {
                    ModConfig.LogError("OnEnabled: PluginManager から modPath を取得できませんでした");
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnEnabled error: " + e);
            }
        }
    }
}
