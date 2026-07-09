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
        // バニラの災害規模5.5(内部intensity=55)のシンクホールと同等になるよう SinkholeAI の式で算出:
        //   width = m_holeWidth(50) * (55*0.01) + 16 = 43.5,  depth = m_holeDepth(50) * (55*0.01) + 16 = 43.5
        //   実際の MakeCrater 呼び出しは radius = width * 0.5 = 21.75, depth = 43.5 (raiseEdges:false)。
        public const float CraterRadiusMax = 21.75f;
        public const float CraterDepthMax = 43.5f;
        public const float StrikeScatterRadius = 15f;   // 落雷点を中心からランダムにずらす範囲
        public const float DestructionRadius = 70f;     // Bombarding終了時に建物破壊する半径

        // --- 汚染(赤) ---
        public const int ExpiryMonths = 2;              // 汚染が消滅するまでのゲーム内時間(月)
        public const float ContaminationRadius = 90f;   // 陥没穴跡の汚染半径
        public const byte MaxPollution = 255;
        public const float RedDecalYOffset = 0.3f;

        // --- 発動 ---
        public const KeyCode ManualTriggerKey = KeyCode.F7;
        public const int RandomCheckIntervalTicks = 4096;
        public const int RandomChancePer10000 = 1;

        // --- UFO召喚ボタン(災害パネル横に取り付け) ---
        // ボタンは災害パネル(DisastersPanel)のルート直下に子として貼り、パネルの表示に自動追従する。
        // 位置は実機でしか正確に分からないため定数化: パネル内相対座標での配置オフセット。
        // OffsetX/Y はパネル左上(0,0)を基準にした相対座標。既定は右上寄り。
        // 実機ログにパネルサイズを出力するので、ズレたらここを調整する。
        public const float SummonButtonWidth = 128f;
        public const float SummonButtonHeight = 32f;
        public const float SummonButtonOffsetX = 8f;    // パネル右端からの内側マージン(右寄せ計算に使用)
        public const float SummonButtonOffsetY = -40f;  // パネル上端からの相対Y(負=上にはみ出して災害アイコン列の上に置く)
        // 災害パネルが一定時間見つからない場合(例: Natural Disasters DLC無し)のフォールバックとして
        // 画面左上に常時ボタンを出すまでの猶予フレーム数。
        public const int SummonButtonFallbackFrames = 600;

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
