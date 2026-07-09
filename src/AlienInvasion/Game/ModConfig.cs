using UnityEngine;

namespace AlienInvasion.Game
{
    /// <summary>Mod全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[AlienInvasion] ";

        // --- AssetBundle ---
        public const string AssetBundleFileName = "alieninvasion.bundle";
        public const string MothershipPrefabName = "Mothership";
        public const string RedDecalPrefabName = "ContaminationDecal";

        // --- 母船の飛行 ---
        public const float MothershipStartAltitude = 800f;   // 出現高度(地表からの相対高さ)
        public const float MothershipHoverAltitude = 220f;   // 降下後のホバリング高度
        public const float DescendSeconds = 6f;
        public const float BombardSeconds = 10f;
        public const float StrikeIntervalSeconds = 0.6f;
        public const float AscendSeconds = 5f;

        // --- 陥没穴(シンクホール)/破壊(累積値。Bombarding中に徐々に成長し、終了時に確定) ---
        public const float CraterRadiusMax = 55f;
        public const float CraterDepthMax = 45f;
        public const float StrikeScatterRadius = 15f;   // 落雷点を中心からランダムにずらす範囲
        public const float DestructionRadius = 70f;     // Bombarding終了時に建物破壊する半径

        // --- 汚染(赤) ---
        public const int ExpiryYears = 1;
        public const float ContaminationRadius = 90f;   // 陥没穴跡の汚染半径
        public const byte MaxPollution = 255;
        public const float RedDecalYOffset = 0.3f;

        // --- 発動 ---
        public const KeyCode ManualTriggerKey = KeyCode.F7;
        public const int RandomCheckIntervalTicks = 4096;
        public const int RandomChancePer10000 = 1;

        public static void Log(string msg)
        {
            Debug.Log(LogPrefix + msg);
        }

        public static void LogError(string msg)
        {
            Debug.LogError(LogPrefix + msg);
        }
    }
}
