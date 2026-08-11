namespace AlienInvasion.Game
{
    /// <summary>
    /// Every player-facing string, as a public static field whose initializer is the built-in
    /// English default.
    ///
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

        // --- Options: summoning --------------------------------------------------------------------
        public static string Options_InvasionGroup = "Invasion";
        public static string Options_InvasionHelp =
            "Open the Disasters info-view and click the UFO button (or press the hotkey below), " +
            "then click a spot on the map to aim. The mothership descends there, tears a crater " +
            "open, and deploys tripods that roam and fire until they leave.";
        public static string Options_Hotkey = "Summon hotkey";
        public static string Options_MaxConcurrent = "Maximum invasions at once";

        // --- Options: random invasions ---------------------------------------------------------------
        public static string Options_RandomGroup = "Random invasions (DESTRUCTIVE - off by default)";
        public static string Options_RandomEnable =
            "Enable random invasions - motherships WILL arrive on their own and destroy " +
            "buildings, like a natural disaster. Leave this off to only summon them yourself.";
        public static string Options_RandomAverageDays = "Average in-game days between invasions";
        public static string Options_RandomHelp =
            "Once switched on, one invasion arrives every so many in-game days on average - it is " +
            "a roll once a day, not a schedule, so the actual gaps vary. Measured on the game " +
            "clock, so it stops while the game is paused and follows the game speed. Nothing ever " +
            "starts while another invasion is still running.";

        // --- Options: aftermath ----------------------------------------------------------------------
        public static string Options_AftermathGroup = "Aftermath";
        public static string Options_Contamination = "Leave red contamination behind";
        public static string Options_AftermathHelp =
            "The red weed spreads around the crater and along the tripods' trail, and lifts by " +
            "itself after a couple of in-game months. Turning this off stops new contamination; " +
            "it does not clear what a previous invasion already left in this save.";

        // --- Options: sound --------------------------------------------------------------------------
        public static string Options_SoundGroup = "Sound";
        public static string Options_UfoSound = "UFO arrival sound";
        public static string Options_TripodSound = "Tripod footstep sound";
        public static string Options_SoundVolume = "Sound volume";
    }
}
