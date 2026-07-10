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
        public const string TripodPrefabName = "Tripod";

        // --- OBJ実行時ロード(AssetBundleが使えない場合のフォールバック) ---
        // Mod配置フォルダ直下の Models サブフォルダに <name>.obj / <name>.mtl を置く。
        public const string ModelsFolderName = "Models";
        public const float ObjMetallic = 0.7f;     // Standardシェーダの金属質パラメータ
        public const float ObjGlossiness = 0.6f;   // Standardシェーダの滑らかさパラメータ
        public static readonly Color ObjFallbackColor = new Color(0.2f, 0.2f, 0.2f, 1f); // MTLに無い場合の既定色(金属グレー)
        // 汚染デカールのハイライト色(クリムゾン〜オレンジ赤)。アルファは中心のピーク不透明度
        // (外周へ放射状にフェード)。レッドウィード風テクスチャの濃い部分の色に使う。
        public static readonly Color RedDecalColor = new Color(1f, 0.22f, 0.05f, 0.5f);

        // --- 母船の飛行 ---
        public const float MothershipStartAltitude = 800f;   // 出現高度(地表からの相対高さ)
        public const float MothershipHoverAltitude = 220f;   // 降下後のホバリング高度
        public const float DescendSeconds = 6f;
        public const float BombardSeconds = 10f;
        public const float StrikeIntervalSeconds = 0.6f;
        public const float AscendSeconds = 5f;
        public const float MothershipSpinDegPerSec = 60f;  // 母船の水平回転速度(度/秒)。速いほど明確に回る
        public const float MothershipScale = 1f;           // prefab 生成時のスケール(実機で調整)

        // --- 陥没穴(シンクホール)/破壊 ---
        // バニラの災害規模5.5(内部intensity=55)のシンクホールと同等。SinkholeAI の式:
        //   width = m_holeWidth(50) * (55*0.01) + 16 = 43.5,  depth = m_holeDepth(50) * (55*0.01) + 16 = 43.5
        //   MakeCrater(pos, radius = width*0.5 = 21.75, depth = 43.5, raiseEdges:false)。
        // MakeCrater は「現在の地表高さから相対的に掘り下げ、絶対0mでクランプ」する。毎tick呼ぶと
        // 累積して地形最下限まで掘れて異常に深くなるため、Bombarding終了時にちょうど1回だけ適用する
        // (= バニラ level5.5 と同じ1回ぶんの陥没量)。
        public const float SinkholeRadius = 21.75f;
        public const float SinkholeDepth = 43.5f;
        public const float StrikeScatterRadius = 15f;   // 落雷点を中心からランダムにずらす範囲
        public const float DestructionRadius = 70f;     // Bombarding終了時に建物破壊する半径

        // --- トライポッド(召喚・自由移動) ---
        public const int TripodCount = 3;
        public const float TripodSpeed = 30f;                // 水平移動速度(units/秒)
        public const float TripodActiveSeconds = 60f;        // 出現から消滅までの活動時間(40->60, 1.5倍)
        public const float TripodTurnIntervalSeconds = 2.5f; // 方向転換の間隔
        public const float TripodTurnMaxDeg = 60f;           // 1回の方向転換の最大角(±)
        public const float TripodScale = 1f;
        public const float TripodSpawnScatter = 40f;         // クレーター中心からの初期散布半径
        public const float TripodMapHalfExtent = 8500f;      // 移動境界(マップ半径目安。既存ランダム発動と同値)
        // 進行方向への向き: LookRotation(heading) でモデルの前方(=Blenderの-Y側)を進行方向へ向ける。
        // モデル前方がUnityのどのローカル軸かに応じた微調整用のヨー(度)。実機で向きがズレたらここを調整。
        public const float TripodYawOffsetDeg = 0f;
        // 浮遊感の上下動(見た目のみ。ロジック上のPositionは地表のまま)。
        public const float TripodBobAmplitude = 2.5f;        // 上下動の振幅(m)
        public const float TripodBobFreqHz = 1.1f;           // 上下動の周波数(Hz)

        // --- トライポッド(レーザー破壊・軌跡汚染) ---
        public const float BeamIntervalSeconds = 1.5f;             // ビーム発射(=着弾破壊)の間隔
        public const float BeamDestroyRadius = 25f;                // 1回のビーム着弾で破壊する半径(局所)
        public const float TripodTrailContamRadius = 30f;          // 軌跡に残す汚染半径
        public const float TripodTrailContamIntervalSeconds = 3f;  // 軌跡汚染をスタンプする間隔
        public const float TripodHeadHeight = 60f;                 // ビーム発射源(頭)の高さ(接地点からの相対)
        public const float BeamMinAngleDeg = 20f;                  // ビームの俯角の最小(水平からの下向き角)
        public const float BeamMaxAngleDeg = 60f;                  // ビームの俯角の最大
        public const float BeamMaxRange = 180f;                    // ビーム着弾までの水平距離の上限

        // --- エフェクトの色(雷・レーザーは青白い発光) ---
        public static readonly Color BoltColor = new Color(0.55f, 0.8f, 1f);  // 母船の雷(青白)
        public static readonly Color BeamColor = new Color(0.6f, 0.85f, 1f);  // トライポッドのレーザー(青白)

        // --- 着弾爆発エフェクト(UFOの雷着弾・トライポッドのレーザー着弾で共用) ---
        // 既定はゲーム標準の中規模爆発(m_mediumExplosion)。magnitudeで強度/スケールを調整可。
        public const float ImpactEffectMagnitude = 0.7f;

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
