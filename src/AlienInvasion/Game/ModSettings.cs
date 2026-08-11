using AlienInvasion.Core;
using ColossalFramework;
using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Persisted settings, stored through ColossalFramework's SavedInt.
    /// <para>
    /// The settings file must NOT be named after the mod/assembly ("AlienInvasion"): an identical
    /// name collides with the mod's own key in the CS settings dictionary, which throws
    /// "an item with the same key already exists", puts the game into a loop of deleting the
    /// settings file on every launch, and flags the mod errored.
    /// </para>
    /// Every key here is new, with no predecessor in a shipped version, so there is no migration
    /// and nothing that has to read provenance off an older key.
    /// </summary>
    public static class ModSettings
    {
        public const string FileName = "AlienInvasionSettings";

        /// <summary>
        /// The hotkeys offered for summoning. The <b>KeyCode value</b> is stored, not the
        /// position in this array, so reordering or extending the list later cannot re-point a
        /// setting a player has already made.
        /// </summary>
        public static readonly KeyCode[] KeyOptions =
        {
            KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9,
            KeyCode.F10, KeyCode.F11, KeyCode.F12,
        };

        public const int VolumeDefault = 100;   // 0-100

        private static SavedInt _randomEnabled;
        private static SavedInt _randomAverageDays;
        private static SavedInt _maxConcurrent;
        private static SavedInt _hotkey;
        private static SavedInt _contamination;
        private static SavedInt _ufoSound;
        private static SavedInt _tripodSound;
        private static SavedInt _soundVolume;
        private static bool _fileRegistered;

        public static void Ensure()
        {
            // AddSettingsFile exactly once. Registering a duplicate makes the internal dictionary
            // Add throw, which GameSettings treats as a failed load - and it then deletes the
            // settings file from disk.
            if (!_fileRegistered)
            {
                _fileRegistered = true;
                try
                {
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = FileName });
                }
                catch (System.Exception e)
                {
                    ModConfig.LogError("ModSettings.Ensure AddSettingsFile error: " + e);
                }
            }

            // Random invasions are off by default. They destroy buildings without warning, and a
            // player who has not asked for that should never meet one.
            if (_randomEnabled == null)
                _randomEnabled = new SavedInt("randomEnabled", FileName, 0, true);
            if (_randomAverageDays == null)
                _randomAverageDays = new SavedInt("randomAverageDays", FileName,
                    RandomInvasionSchedule.DefaultAverageDays, true);

            if (_maxConcurrent == null)
                _maxConcurrent = new SavedInt("maxConcurrent", FileName, ModConfig.MaxConcurrentInvasions, true);
            if (_hotkey == null)
                _hotkey = new SavedInt("hotkey", FileName, (int)ModConfig.ManualTriggerKey, true);

            if (_contamination == null)
                _contamination = new SavedInt("contamination", FileName, 1, true);   // on by default

            if (_ufoSound == null) _ufoSound = new SavedInt("ufoSound", FileName, 1, true);
            if (_tripodSound == null) _tripodSound = new SavedInt("tripodSound", FileName, 1, true);
            if (_soundVolume == null) _soundVolume = new SavedInt("soundVolume", FileName, VolumeDefault, true);
        }

        public static SavedInt RandomEnabledSetting { get { Ensure(); return _randomEnabled; } }
        public static SavedInt RandomAverageDaysSetting { get { Ensure(); return _randomAverageDays; } }
        public static SavedInt MaxConcurrentSetting { get { Ensure(); return _maxConcurrent; } }
        public static SavedInt HotkeySetting { get { Ensure(); return _hotkey; } }
        public static SavedInt ContaminationSetting { get { Ensure(); return _contamination; } }
        public static SavedInt UfoSoundSetting { get { Ensure(); return _ufoSound; } }
        public static SavedInt TripodSoundSetting { get { Ensure(); return _tripodSound; } }
        public static SavedInt SoundVolumeSetting { get { Ensure(); return _soundVolume; } }

        /// <summary>Whether invasions may start on their own.</summary>
        public static bool RandomEnabled { get { return RandomEnabledSetting.value != 0; } }

        /// <summary>Mean in-game days between random invasions, clamped to the offered range.</summary>
        public static int RandomAverageDays
        {
            get { return RandomInvasionSchedule.ClampAverageDays(RandomAverageDaysSetting.value); }
        }

        /// <summary>
        /// How many invasions may run at once. Clamped to the number of slots InvasionManager
        /// actually has - that array is sized at static init and never grows, so a larger setting
        /// would silently promise capacity that does not exist.
        /// </summary>
        public static int MaxConcurrent
        {
            get
            {
                int v = MaxConcurrentSetting.value;
                if (v < 1) return 1;
                if (v > ModConfig.MaxConcurrentInvasions) return ModConfig.MaxConcurrentInvasions;
                return v;
            }
        }

        /// <summary>
        /// The summon hotkey. An unknown stored value falls back to the shipped default rather
        /// than to whatever KeyCode happens to share that number.
        /// </summary>
        public static KeyCode Hotkey
        {
            get
            {
                int v = HotkeySetting.value;
                for (int i = 0; i < KeyOptions.Length; i++)
                    if ((int)KeyOptions[i] == v) return KeyOptions[i];
                return ModConfig.ManualTriggerKey;
            }
        }

        /// <summary>Whether an invasion leaves red contamination behind.</summary>
        public static bool ContaminationEnabled { get { return ContaminationSetting.value != 0; } }

        public static bool UfoSoundEnabled { get { return UfoSoundSetting.value != 0; } }
        public static bool TripodSoundEnabled { get { return TripodSoundSetting.value != 0; } }

        /// <summary>Sound volume as 0.0-1.0, multiplying each sound's own balance constant.</summary>
        public static float SoundVolume
        {
            get
            {
                int v = SoundVolumeSetting.value;
                if (v < 0) v = 0;
                if (v > 100) v = 100;
                return v / 100f;
            }
        }
    }
}
