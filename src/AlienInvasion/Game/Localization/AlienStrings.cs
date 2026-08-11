namespace AlienInvasion.Game
{
    /// <summary>
    /// Every player-facing string, as a public static field whose initializer is the built-in
    /// English default.
    ///
    /// There are only two, because this mod has no options screen at all: the Content Manager
    /// description and the tooltip on the summon button. The full scheme is still used rather
    /// than translating those two in place, so that anything added later is localizable for free
    /// and the file layout matches the author's other mods.
    ///
    /// How localization works:
    ///  - The field name is the key in Locales/&lt;lang&gt;.txt.
    ///  - LocaleLoader.EnsureLoaded() detects the game language and overwrites these fields by
    ///    reflection from the matching file. A missing file or an unknown key leaves the English
    ///    default in place, so a half-finished translation is always safe to ship.
    ///
    /// Nothing here may be copied into a `static readonly string[]`: that array would be built
    /// once at class load and keep the language it was built in.
    ///
    /// Log messages are deliberately NOT here. Logs should stay grep-able in English, and a bug
    /// report is far easier to read when the log says the same thing whoever sent it.
    ///
    /// To add a language: copy Locales/en.txt to Locales/&lt;code&gt;.txt using the code the game
    /// reports (de, fr, es, zh, ja, ...), translate the values, and open a pull request at
    /// https://github.com/omoneko/AlienInvasion - or just drop the file in the mod folder.
    /// </summary>
    public static class AlienStrings
    {
        // --- Content Manager -------------------------------------------------------------------
        public static string Mod_Description =
            "A UFO mothership descends, wrecks the city with lightning and a crater, then deploys " +
            "roaming tripods that fire lasers and leave red contamination. Trigger it with the " +
            "UFO ! button or the F7 key (up to 5 at once).";

        // --- In-game button ------------------------------------------------------------------------
        public static string Button_Tooltip = "Select a location to summon the UFO mothership";
    }
}
