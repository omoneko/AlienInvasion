using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Plugins;
using AlienInvasion.Core;

namespace AlienInvasion.Game
{
    /// <summary>
    /// Loads Locales/&lt;lang&gt;.txt from the mod folder and overwrites AlienStrings' fields by
    /// reflection. See AlienStrings for the scheme.
    ///
    ///  - Language: LocaleManager.instance.language, the game's own code. "en", or anything that
    ///    cannot be resolved, keeps the built-in English defaults.
    ///  - Idempotent per language: calling it again does nothing unless the language changed,
    ///    in which case the new file is applied on top of a fresh English baseline. Without that
    ///    reset, switching from a complete translation to a partial one would leave the first
    ///    language's strings showing through the gaps.
    ///  - Locales/en.txt is written from the current defaults when missing, so a translator always
    ///    has an up-to-date template sitting next to the mod.
    ///  - Never throws. Any failure logs once and leaves English in place.
    ///
    /// <para>
    /// Call it before the first string is read on each path: Mod.Description, Mod.OnSettingsUI
    /// and the summon button's tooltip in InvasionUI. The options page picks up a language
    /// change on its own - OptionsMainPanel.OnLocaleChanged calls CreateCategories, which re-runs
    /// OnSettingsUI, and this runs again at the top of it. The in-game button does not: its
    /// tooltip is set when the button is built, so it keeps the language the city was loaded in
    /// until the next load. That is one tooltip, and rebuilding the button on a locale change
    /// would cost more than it is worth.
    /// </para>
    /// </summary>
    internal static class LocaleLoader
    {
        private const string LocalesFolder = "Locales";

        private static string _loadedLanguage;                       // last applied, null = never
        private static Dictionary<string, string> _englishDefaults;  // field name -> built-in default

        /// <summary>Idempotent per game language. Safe to call from any UI entry point.</summary>
        public static void EnsureLoaded()
        {
            try
            {
                string language = CurrentLanguage();
                if (language == _loadedLanguage) return;

                CaptureEnglishDefaultsOnce();
                RestoreEnglishDefaults();

                string modPath = ResolveModPath();
                if (!string.IsNullOrEmpty(modPath))
                {
                    string dir = Path.Combine(modPath, LocalesFolder);
                    EnsureTemplate(dir);

                    if (language != "en")
                    {
                        string path = Path.Combine(dir, language + ".txt");
                        if (File.Exists(path))
                        {
                            int applied = Apply(LocaleFileParser.Parse(File.ReadAllText(path)));
                            ModConfig.Log("LocaleLoader: applied " + applied + " string(s) from " + path);
                        }
                    }
                }

                _loadedLanguage = language;
            }
            catch (Exception e)
            {
                // Latch to "en" so a persistent failure is not retried on every call.
                _loadedLanguage = "en";
                ModConfig.LogError("LocaleLoader.EnsureLoaded error (using built-in English): " + e);
            }
        }

        private static string CurrentLanguage()
        {
            try
            {
                if (LocaleManager.exists)
                {
                    string lang = LocaleManager.instance.language;
                    if (!string.IsNullOrEmpty(lang)) return lang;
                }
            }
            catch (Exception)
            {
                // Too early for LocaleManager: fall through to English rather than log every call.
            }
            return "en";
        }

        private static FieldInfo[] StringFields()
        {
            FieldInfo[] fields = typeof(AlienStrings).GetFields(BindingFlags.Public | BindingFlags.Static);
            var result = new List<FieldInfo>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].FieldType == typeof(string)) result.Add(fields[i]);
            return result.ToArray();
        }

        private static void CaptureEnglishDefaultsOnce()
        {
            if (_englishDefaults != null) return;
            _englishDefaults = new Dictionary<string, string>();
            foreach (FieldInfo f in StringFields())
                _englishDefaults[f.Name] = (string)f.GetValue(null);
        }

        private static void RestoreEnglishDefaults()
        {
            foreach (FieldInfo f in StringFields())
            {
                string value;
                if (_englishDefaults.TryGetValue(f.Name, out value)) f.SetValue(null, value);
            }
        }

        private static int Apply(Dictionary<string, string> map)
        {
            int applied = 0;
            foreach (FieldInfo f in StringFields())
            {
                string value;
                if (map.TryGetValue(f.Name, out value) && !string.IsNullOrEmpty(value))
                {
                    f.SetValue(null, value);
                    applied++;
                }
            }
            return applied;
        }

        /// <summary>Writes Locales/en.txt from the current defaults when it is missing.</summary>
        private static void EnsureTemplate(string dir)
        {
            try
            {
                string path = Path.Combine(dir, "en.txt");
                if (File.Exists(path)) return;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                CaptureEnglishDefaultsOnce();
                using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
                {
                    w.WriteLine("# Alien Invasion UI strings (English template).");
                    w.WriteLine("# To translate: copy this file to <language code>.txt using the code the game");
                    w.WriteLine("# reports (de/fr/es/zh/ja/...), translate the values, and keep the \\n line-break");
                    w.WriteLine("# escapes. Missing keys fall back to English, so a partial file is fine.");
                    w.WriteLine("# Contributions welcome: https://github.com/omoneko/AlienInvasion");
                    w.WriteLine();
                    foreach (FieldInfo f in StringFields())
                        w.WriteLine(f.Name + " = " + LocaleFileParser.Escape(_englishDefaults[f.Name]));
                }
                ModConfig.Log("LocaleLoader: wrote template " + path);
            }
            catch (Exception e)
            {
                ModConfig.LogError("LocaleLoader.EnsureTemplate error: " + e);
            }
        }

        private static string ResolveModPath()
        {
            try
            {
                PluginManager.PluginInfo info =
                    Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                return info != null ? info.modPath : null;
            }
            catch (Exception e)
            {
                ModConfig.LogError("LocaleLoader.ResolveModPath error: " + e);
                return null;
            }
        }
    }
}
