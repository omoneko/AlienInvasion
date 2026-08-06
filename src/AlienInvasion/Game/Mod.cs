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
            // Assembly.GetExecutingAssembly().Location can come back empty the way CS loads
            // mods, and Path.GetDirectoryName then throws - a bug that really did happen here.
            // The mod path is taken from the game's own PluginManager instead, which is
            // reliable.
            try
            {
                PluginManager.PluginInfo info = Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                if (info != null && !string.IsNullOrEmpty(info.modPath))
                {
                    AssetLoader.Initialize(info.modPath);
                    ModelProvider.Initialize(info.modPath);
                    SoundManager.Initialize(info.modPath);
                    UI.UfoIcon.SetModDirectory(info.modPath); // so the tab icon can use icon.png
                }
                else
                {
                    ModConfig.LogError("OnEnabled: could not get modPath from PluginManager");
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnEnabled error: " + e);
            }
        }
    }
}
