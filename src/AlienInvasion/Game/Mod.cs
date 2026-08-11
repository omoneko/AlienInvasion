using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        // The mod's name is its Workshop title, so it stays in English; the description is
        // localizable. The getter loads the locale because the Content Manager reads this, and
        // this mod has no options screen to load it from.
        public string Name => "Alien Invasion";
        public string Description
        {
            get { LocaleLoader.EnsureLoaded(); return AlienStrings.Mod_Description; }
        }

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
