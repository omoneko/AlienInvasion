using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;

namespace AlienInvasion.Game
{
    public class Mod : IUserMod
    {
        // The mod's name is its Workshop title, so it stays in English; everything else is
        // localizable. The getter loads the locale because the Content Manager can read this
        // before the options screen has ever been opened.
        public string Name => "Alien Invasion";
        public string Description
        {
            get { LocaleLoader.EnsureLoaded(); return AlienStrings.Mod_Description; }
        }

        /// <summary>The mod's options screen. The game finds and calls this itself.</summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                LocaleLoader.EnsureLoaded();
                ModSettings.Ensure();

                UIHelperBase inv = helper.AddGroup(AlienStrings.Options_InvasionGroup);
                inv.AddButton(AlienStrings.Options_InvasionHelp, () => { });

                // The stored value is the KeyCode, not the dropdown position, so the position is
                // resolved here and mapped back in the callback.
                string[] keyNames = new string[ModSettings.KeyOptions.Length];
                int keyIndex = 0;
                for (int i = 0; i < ModSettings.KeyOptions.Length; i++)
                {
                    keyNames[i] = ModSettings.KeyOptions[i].ToString();
                    if (ModSettings.KeyOptions[i] == ModSettings.Hotkey) keyIndex = i;
                }
                inv.AddDropdown(AlienStrings.Options_Hotkey, keyNames, keyIndex, i =>
                {
                    if (i >= 0 && i < ModSettings.KeyOptions.Length)
                        ModSettings.HotkeySetting.value = (int)ModSettings.KeyOptions[i];
                });

                inv.AddSlider(AlienStrings.Options_MaxConcurrent,
                    1f, ModConfig.MaxConcurrentInvasions, 1f, ModSettings.MaxConcurrent,
                    v => ModSettings.MaxConcurrentSetting.value = (int)v);

                UIHelperBase rnd = helper.AddGroup(AlienStrings.Options_RandomGroup);
                rnd.AddCheckbox(AlienStrings.Options_RandomEnable, ModSettings.RandomEnabled,
                    b => ModSettings.RandomEnabledSetting.value = b ? 1 : 0);
                rnd.AddSlider(AlienStrings.Options_RandomAverageDays,
                    AverageDaysMin, AverageDaysMax, 5f, ModSettings.RandomAverageDays,
                    v => ModSettings.RandomAverageDaysSetting.value = (int)v);
                rnd.AddButton(AlienStrings.Options_RandomHelp, () => { });

                UIHelperBase after = helper.AddGroup(AlienStrings.Options_AftermathGroup);
                after.AddCheckbox(AlienStrings.Options_Contamination, ModSettings.ContaminationEnabled,
                    b => ModSettings.ContaminationSetting.value = b ? 1 : 0);
                after.AddButton(AlienStrings.Options_AftermathHelp, () => { });

                UIHelperBase snd = helper.AddGroup(AlienStrings.Options_SoundGroup);
                snd.AddCheckbox(AlienStrings.Options_UfoSound, ModSettings.UfoSoundEnabled,
                    b => ModSettings.UfoSoundSetting.value = b ? 1 : 0);
                snd.AddCheckbox(AlienStrings.Options_TripodSound, ModSettings.TripodSoundEnabled,
                    b => ModSettings.TripodSoundSetting.value = b ? 1 : 0);
                snd.AddSlider(AlienStrings.Options_SoundVolume, 0f, 100f, 1f,
                    ModSettings.SoundVolumeSetting.value,
                    v => ModSettings.SoundVolumeSetting.value = (int)v);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnSettingsUI error: " + e);
            }
        }

        // The slider bounds come from Core, so the setting, the clamp and the slider cannot drift.
        private const float AverageDaysMin = AlienInvasion.Core.RandomInvasionSchedule.MinAverageDays;
        private const float AverageDaysMax = AlienInvasion.Core.RandomInvasionSchedule.MaxAverageDays;

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
