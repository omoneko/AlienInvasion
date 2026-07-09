# Alien Invasion Mod 設計書

- 日付: 2026-07-09
- 対象: Cities: Skylines（初代 / Unity / .NET Framework 3.5）
- ステータス: 設計承認済み（実APIは計画フェーズで逆コンパイル検証）

## 1. 概要

SimCity 4 のエイリアン襲来をオマージュした災害Mod。**手動（召喚ツール）**または**ランダム**で発動し、以下を順に演出する:

1. UFO母船が上空から目標地点へ降下
2. 母船が雷を連打し、直下に陥没（クレーター）を形成
3. 母船が上昇して消滅
4. トライポッド型小型宇宙船が3体出現
5. トライポッドがランダムに自由移動し、進行方向付近の建物をビームで局所破壊（1回で数棟程度）
6. トライポッドは一定時間後に消滅
7. クレーターおよびトライポッドの通過跡に**赤い汚染**を**ゲーム内1年**残す（赤い地面＋赤い発光エフェクト）

自作3Dモデル（UFO・トライポッド・赤デカール）は **AssetBundle** としてMODに同梱する。

## 2. 方針

**Mod主導方式（方式A）**: 自作モデルを AssetBundle から読み込み GameObject として生成し、**Mod が毎tick 位置・向きを直接制御**する。道路や経路探索に縛られない自由な飛行/歩行を実現。破壊・エフェクト・汚染は Nuclear Meltdown で確立した `DisasterHelpers` / エフェクト / `NaturalResourceManager` を流用。

Vehicle/VehicleAI 方式（方式B）は道路前提の経路探索が絡み不採用。

## 3. 技術要件

- 言語/FW: C# / .NET Framework 3.5、MSBuild でビルド
- 参照: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll`, `ColossalManaged.dll`
- Harmony: NuGet `CitiesHarmony.API`
- **AssetBundle**: CS と同一の Unity バージョンでビルド（バージョンは計画時に特定）。UFO・トライポッド・赤デカールの prefab を格納
- 配置先: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\`
- インターフェース: `ICities.IUserMod`

## 4. アーキテクチャ

```
AlienInvasion/
├─ AlienInvasion.csproj
├─ Assets/alieninvasion.bundle           # UFO・トライポッド・赤デカールのprefab
├─ Core/  (Unity非依存・xUnitでテスト)
│   ├─ InvasionState.cs                   # 状態機械 enum ＋遷移ロジック
│   ├─ MovementMath.cs                    # ランダム歩行方向・座標補間・境界クランプ
│   ├─ GridMath.cs                        # ワールド→汚染セル変換・半径/軌跡のセル列挙
│   ├─ ContaminationZone.cs               # 汚染ゾーン(中心/半径/開始時刻)
│   └─ ZoneSerializer.cs                  # 汚染ゾーンの byte[] 直列化
└─ Game/  (ゲーム統合層)
    ├─ Mod.cs                             # IUserMod + Harmony + AssetBundleロード
    ├─ ModConfig.cs                       # 定数(時間/半径/確率/1年/3体 等・調整可)
    ├─ AssetLoader.cs                     # AssetBundleから prefab 取得
    ├─ InvasionTrigger.cs                 # 手動ツール(ボタン) ＋ ランダム発生タイマー
    ├─ InvasionManager.cs                 # アクティブな襲来の統括(状態機械の駆動)
    ├─ Mothership.cs                      # 降下→雷連打→クレーター→上昇消滅
    ├─ Tripod.cs                          # 出現→ランダム移動→ビーム破壊→消滅
    ├─ Effects.cs                         # 雷/ビーム/爆発/赤発光エフェクトの解決・再生
    ├─ RedContaminationVisual.cs          # 赤デカール(プロップ)の配置/撤去
    ├─ ContaminationManager.cs            # 汚染ゾーン台帳＋適用/維持/クリア
    ├─ PollutionField.cs                  # NaturalResourceManagerへの汚染読み書き
    ├─ Simulation/InvasionThreadingExtension.cs   # 毎tickで襲来と汚染を駆動
    └─ Serialization/InvasionDataExtension.cs     # 進行中の襲来＋汚染をセーブ/ロード
```

依存の向き: `Game/* → Core/*`（一方向）。`Core/*` は他に依存しない。

## 5. イベントの流れ（状態機械）

`InvasionState`:
```
Descending → Bombarding → Ascending → TripodDeploy → TripodsActive → Done
```
`InvasionThreadingExtension` が毎tickで `InvasionManager.Update()` を呼び、タイマー・位置補間・各処理を進める。フェーズ1では `Descending→Bombarding→Ascending→Done`（トライポッドは Done へスキップ）、フェーズ2でトライポッド段階を実装。

## 6. 発動（手動＋ランダム）

- **手動**: 専用ボタン/ツールで地図上の地点を選び発動（`InvasionManager.StartInvasion(position)`）。
- **ランダム**: `InvasionThreadingExtension` が一定期間ごとに低確率で抽選し、マップ上のランダム地点で発動。頻度・ON/OFF は `ModConfig` で設定。乱数はゲームの決定論RNG(`SimulationManager.m_randomizer`)。
- ※CSの正規ランダム災害枠への登録は複雑なため、Mod主導の抽選で実現。

## 7. 母船（フェーズ1）

- AssetBundle の UFO prefab を上空（目標地点の高高度）に GameObject 生成。
- **降下**: Y座標を hover 高度まで補間（`ModConfig.DescendSeconds`）。
- **雷連打（Bombarding）**: 一定tickごとに直下のランダム点へ雷エフェクトを再生し、少しずつ陥没を形成（累積で `DisasterHelpers.MakeCrater`）＋直下範囲の建物を破壊。継続時間 `ModConfig.BombardSeconds`。
- 終了後 **上昇**（Y補間）して GameObject 破棄。
- クレーター地点に **赤い汚染ゾーン（1年）** を登録。

## 8. トライポッド（フェーズ2）

- 母船消滅後、クレーター付近に **3体**（`ModConfig.TripodCount`）を GameObject 生成。
- 毎tick: 各体を現在のランダム方向へ `ModConfig.TripodSpeed` で移動。一定間隔でランダムに方向転換。マップ境界でクランプ/反射。
- **ビーム破壊**: `ModConfig.BeamIntervalTicks` ごとに、進行方向付近の建物を **局所的に破壊**（`DisasterHelpers.DestroyBuildings` を小半径 or `CollapseBuilding`、1回で数棟）＋ビームエフェクト。
- **軌跡汚染**: 一定間隔で現在位置に小さな赤い汚染ゾーンをスタンプ。
- 活動時間 `ModConfig.TripodActiveSeconds` 後に消滅（GameObject破棄）。

## 9. 汚染と赤い演出

- `ContaminationManager` / `PollutionField` / `ZoneSerializer` を Nuclear Meltdown から流用。
- **消滅条件**: 「ゲーム内1年経過」のみ（`ModConfig.ExpiryYears = 1`）。**除染施設は無し**。
- 自然減衰に抗って毎tick再アサートし、1年で解除。
- **赤い地面**: CS標準の土壌汚染は色固定のため、AssetBundle の **赤い汚染デカール(平面プロップ)** を汚染セル上に `RedContaminationVisual` が配置し、ゾーン解除時に撤去。
- **赤いエフェクト**: 汚染域に赤い発光/もやのエフェクトを持続表示。
- ゲーム的効果（地価下落等）が必要なら、赤デカールと併せて標準の土壌汚染も裏で適用（`PollutionField`）。
- ※赤化の正確なレンダリング手法（プロップ/デカール配置API、エフェクトの色指定）は計画フェーズで実API検証。

## 10. 永続化・安全性

- 進行中の襲来状態と汚染ゾーンを `ISerializableData` でセーブ/ロード保持。
- 全 tick / 生成 / エフェクト / 直列化処理は try/catch で保護し、例外をゲーム本体へ伝播させない。
- **AssetBundle / prefab が読めない場合**: ログを出して該当演出をスキップ（ゲームを巻き込まない）。
- console 出力は残さず、ログは `Debug.Log` に接頭辞 `[AlienInvasion]` を付けてのみ。

## 11. 3Dモデル要件（Blender作業向け）

- **UFO母船**: 巨大円盤（インディペンデンス・デイ級の“サイズ感”）、ローポリ＋LOD、`_d/_n/_s/_i/_a` テクスチャ、ピボット中心。
- **トライポッド**: 三脚歩行体、ローポリ＋LOD、ピボット接地点。
- **赤い汚染デカール**: 単純な赤い平面（半透明可）、1枚。
- 制作: Blender → FBX → （Vehicle系は Asset Editor 参考、ただし本Modは AssetBundle 同梱）→ Unity で **AssetBundle 化**（CSと同一Unityバージョン）。

## 12. 実装フェーズ

- **フェーズ1（母船）**: プロジェクト骨組み＋AssetBundleロード → 発動(手動＋ランダム) → 母船降下 → 雷連打＋クレーター → 上昇消滅 → 赤い汚染(1年) → セーブ/ロード。
- **フェーズ2（トライポッド）**: 3体出現 → ランダム移動＋局所ビーム破壊 → 軌跡の赤い汚染 → 消滅。

## 13. テスト方針

- Core（状態遷移・ランダム歩行・境界クランプ・セル計算・直列化）は xUnit で TDD。
- ゲーム統合層は MSBuild ビルド成功＋実機確認（ユニットテスト不可）。
- 逆コンパイルで検証する実API: `DisasterHelpers.MakeCrater/DestroyBuildings`、`NaturalResourceManager`、エフェクト再生、AssetBundle ロード、プロップ/デカール配置、`ISerializableData`/`IThreadingExtension`、雷エフェクトの入手元。

## 14. 未確定事項（計画フェーズで解消）

- ~~CS の正確な Unity バージョン（AssetBundle ビルド用）。~~ **解消**: `Cities.exe` 実体は Unity 5.6.7 ビルド（FileVersion 5.6.7.3267）。コミュニティ標準(cslmodding.info)は AssetBundle 作成に Unity Editor 5.6.6 を案内。5.6.x系は相互互換のため **Unity 5.6.6f2** を採用。
- 雷エフェクトの入手元（既存 EffectInfo にあるか、自作が必要か）。
- 赤デカール/プロップの配置・撤去API（`PropManager` 経由等）。
- エフェクトの色指定（赤）が可能か、または赤い自作エフェクトが必要か。
- トライポッドのビーム破壊の具体半径・棟数のバランス。

## 15. スコープ外（YAGNI）

- トライポッドをゲームの正規ユニット（選択・情報パネル）にすること。
- 除染施設による浄化（時間のみ）。
- マルチ言語UI（初期は日本語/英語の最小表記）。
