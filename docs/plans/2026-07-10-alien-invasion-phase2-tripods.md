# Alien Invasion Phase 2 実装計画（トライポッド）

- 日付: 2026-07-10
- ブランチ: `feature/phase2-tripods`（base: `12403b3`）
- 対象: Cities: Skylines 初代 / .NET Framework 3.5 mod（Core は Unity 非依存・xUnit）
- 設計根拠: `docs/specs/2026-07-09-alien-invasion-design.md` §8, §9

## Purpose

Phase 1（母船）完了後に続く演出をユーザー希望の順で実装する:
1. **UFOモデル演出の仕上げ**（回転・スケール）
2. **トライポッドの召喚**（母船上昇後、クレーター付近に3体出現・ランダム自由移動）
3. **レーザー光線での建物破壊**（進行方向付近を局所破壊＋軌跡に赤い汚染）

ビジュアル本体（Tripod prefab）は AssetBundle 依存。バンドル未生成でも母船と同様に
「prefab が無ければ生成をスキップして動作継続」する設計を厳守する。

## Global Constraints（全タスク共通・レビューの遵守基準）

- **スレッド境界（厳守）**: `InvasionManager` の既存規律に従う。
  - メインスレッド（`InvasionThreadingExtension.OnUpdate` → `InvasionManager.UpdateVisual`）:
    GameObject/Transform 生成・破棄・移動、`_state`・フェーズタイマー・トライポッド座標の**書き込み**、
    エフェクト再生（`Effects.*`）。
  - シミュレーションスレッド（`OnAfterSimulationTick` → `InvasionManager.UpdateSimulation`）:
    `DisasterHelpers`・`NaturalResourceManager`（汚染）への書き込みのみ。ここから
    GameObject/Transform/`Effects.*`/`_state` 書き込みを**呼んではならない**。
  - 状態・座標は single-writer（常にメインスレッド）。sim スレッドは読むだけ（既存 `_target`/`_craterProgress` と同じ許容範囲の良性レース）。
- **immutability / 命名**: 既存コードのスタイルに合わせる。Core は net35-safe（タプル/Span/`UnityEngine.*` 禁止。座標は `float x, z` で扱う）。
- **バンドル非依存で動く**: prefab 取得は `AssetLoader.GetPrefab(name)` が null を返しうる前提で null 安全に。
- **例外を本体に伝播させない**: 生成/移動/破壊/エフェクトは try/catch で保護しログのみ。
- **console 出力禁止**: ログは `ModConfig.Log`/`LogError` のみ。
- **調整可能値は必ず `ModConfig` 定数**（マジックナンバー禁止）。
- **既存の Phase 1 挙動を壊さない**: 母船フロー・汚染セーブ/ロード・レベルリセットは不変。
- ビルド確認は `build.ps1`（MSBuild）。Core は `dotnet test`。ゲーム統合層はユニットテスト不可＝ビルド成功で可とする。

## 状態機械（変更後）

```
Idle → Descending → Bombarding → Ascending → TripodDeploy → TripodsActive → Done → Idle
```
Phase 1 は `Ascending → Done` だったが、間に `TripodDeploy`（3体生成の1フレーム処理）と
`TripodsActive`（移動＋ビーム破壊＋軌跡汚染の継続）を挿入する。

---

## Task 1 — Core: トライポッド歩行の純粋数学（TDD）

**新規** `src/AlienInvasion/Core/TripodWalk.cs`（Unity非依存）。xUnit でテスト先行。

必要な純粋関数（すべて static、`float` のみ、`out` 可・タプル不可）:

1. `void Rotate(float dx, float dz, float angleRad, out float ndx, out float ndz)`
   2D 単位方向 (dx,dz) を angleRad だけ回転。`ndx = dx*cos - dz*sin`, `ndz = dx*sin + dz*cos`。
2. `void BounceAxis(float pos, float dir, float half, out float newPos, out float newDir)`
   1軸の境界反射。`pos > half` → `newPos=half, newDir=-|dir|`（内向き）。
   `pos < -half` → `newPos=-half, newDir=+|dir|`。範囲内 → そのまま。
3. `float StepComponent(float pos, float dirComponent, float speed, float dt)`
   `= pos + dirComponent*speed*dt`。

テスト観点（`tests/AlienInvasion.Core.Tests/TripodWalkTests.cs`）:
- Rotate: 90°回転で (1,0)→(0,1) 近似（許容誤差 1e-4）、360°で不変。
- BounceAxis: 右境界超過で half にクランプ＆dir 反転（負に）、左境界も対称、範囲内は不変。
- StepComponent: 既知値の前進。

`AlienInvasion.Core.csproj` の `<Compile Include>` リンク（テストプロジェクトが Core を参照する既存方式）に新ファイルが載ることを確認（既存の他 Core ファイルと同じ登録方法に倣う）。

---

## Task 2 — Core: 状態機械へトライポッド段階を追加（TDD）

`src/AlienInvasion/Core/InvasionState.cs` を変更:
- `enum InvasionState` に `Ascending` と `Done` の間へ `TripodDeploy`, `TripodsActive` を追加。
  最終: `Idle, Descending, Bombarding, Ascending, TripodDeploy, TripodsActive, Done`。
- `InvasionStateMachine.Next`:
  `Ascending → TripodDeploy`、`TripodDeploy → TripodsActive`、`TripodsActive → Done`、`Done → Idle`。

`tests/AlienInvasion.Core.Tests/`（既存の状態機械テストがあれば更新、無ければ追加）:
- 新しい一巡 `Idle→…→TripodsActive→Done→Idle` を Next で辿るテスト。
- `CanTransition` の正当遷移／不正遷移。

---

## Task 3 — Game: UFOモデル演出の仕上げ（回転・スケール）

Added to `ModConfig`:
```
public const float MothershipSpinDegPerSec = 20f;  // 母船の水平回転速度(度/秒)
public const float MothershipScale = 1f;           // prefab 生成時のスケール(実機で調整)
```
`src/AlienInvasion/Game/Mothership.cs`:
- 生成時に `_gameObject.transform.localScale = Vector3.one * ModConfig.MothershipScale`。
- `public void Spin(float dt)` を追加: `_gameObject != null` のとき
  `transform.Rotate(0f, ModConfig.MothershipSpinDegPerSec * dt, 0f, Space.World)`（null 安全）。
`src/AlienInvasion/Game/InvasionManager.cs`:
- `UpdateVisual` の Descending/Bombarding/Ascending いずれでも毎フレーム `_ship.Spin(realTimeDelta)` を呼ぶ
  （各 Update ヘルパー内、`_ship != null` の箇所で）。
- 既存の高度補間・タイマー・遷移は不変。

ユニットテスト不可。`build.ps1` 成功で可。バンドル未生成時は Spin/Scale とも null 安全に no-op。

---

## Task 4 — Game: トライポッド実体と召喚（3体・ランダム移動）

Added to `ModConfig`:
```
public const string TripodPrefabName = "Tripod";
public const int   TripodCount = 3;
public const float TripodSpeed = 30f;               // 水平移動速度(units/秒)
public const float TripodActiveSeconds = 40f;       // 出現から消滅までの活動時間
public const float TripodTurnIntervalSeconds = 2.5f;// 方向転換の間隔
public const float TripodTurnMaxDeg = 60f;          // 1回の方向転換の最大角(±)
public const float TripodScale = 1f;
public const float TripodSpawnScatter = 40f;        // クレーター中心からの初期散布半径
public const float TripodMapHalfExtent = 8500f;     // 移動境界(マップ半径目安。既存ランダム発動と同値)
```
**新規** `src/AlienInvasion/Game/Tripod.cs`（母船と同じ null 安全な GameObject ラッパ）:
- コンストラクタ `Tripod(Vector3 groundPos)`: `AssetLoader.GetPrefab(TripodPrefabName)` を Instantiate（null ならスキップ）、
  scale 適用、初期 heading（単位方向 dx,dz）をランダムに設定。位置は地表にクランプ。
- 内部状態: `float _dirX, _dirZ`（単位方向）, `Vector3 Position { get; }`。
- `public void Advance(float dt)`（メインスレッド）: `TripodWalk.StepComponent` で x,z を前進 →
  `TripodWalk.BounceAxis` で `TripodMapHalfExtent` 境界反射 → 地表高さを
  `TerrainManager.instance.SampleRawHeightSmoothWithWater(pos, false, 0f)` で合わせ、`transform.position` 更新。
- `public void Turn(float angleRad)`: `TripodWalk.Rotate` で方向転換。
- `public void Destroy()`: GameObject 破棄（null 安全）。

**新規** `src/AlienInvasion/Game/TripodManager.cs`（静的。母船同様メインスレッド専用の生成/移動/破棄）:
- `static Tripod[] _tripods`、`static float _activeElapsed`、`static float _turnTimer`。
- `Spawn(Vector3 craterCenter)`（メイン）: `TripodCount` 体を散布生成。`_activeElapsed=0`。
- `UpdateVisual(float dt)`（メイン）: 各 Tripod.Advance。`_turnTimer` が閾値超えで各体を
  `Random.Range(-TripodTurnMaxDeg, +TripodTurnMaxDeg)` 分 Turn。`_activeElapsed += dt`。
- `bool IsFinished { get { return _activeElapsed >= TripodActiveSeconds; } }`。
- `DespawnAll()`（メイン）: 全 Destroy、配列 null。
- `IReadOnly` 的にsimが読むための `static Vector3[] SnapshotPositions()`（sim が破壊に使う。Task 5）。
  → Task 4 では位置配列を保持し、Task 5 で参照する（構造だけ用意）。
- `ResetForNewLevel()`: DespawnAll 相当（`InvasionManager.ResetForNewLevel` から呼ぶ）。

`InvasionManager` 配線:
- `UpdateVisual` の状態分岐に追加:
  - `Ascending` 完了時は現状どおり `Next()`（→ TripodDeploy）。ただし `_ship.Destroy()` は据え置き。
  - `case TripodDeploy`: `TripodManager.Spawn(_target)` を呼び、即 `Next()`（→ TripodsActive）、`_phaseElapsed=0`。
  - `case TripodsActive`: `TripodManager.UpdateVisual(realTimeDelta)`。`TripodManager.IsFinished` で
    `TripodManager.DespawnAll()` → `Next()`（→ Done）。
- `ResetForNewLevel` に `TripodManager.ResetForNewLevel()` を追加。
- `Done` 到達で Idle へ戻る既存処理は不変。

ユニットテスト不可。`build.ps1` 成功で可。バンドル未生成でも移動ロジック（Advance/Turn/タイマー）は
GameObject なしで進行し、`TripodActiveSeconds` 後に正しく Done へ遷移すること（コードレビューで確認）。

---

## Task 5 — Game: レーザー破壊＋軌跡の赤い汚染

Added to `ModConfig`:
```
public const float BeamIntervalSeconds = 1.5f;        // ビーム発射(=局所破壊)の間隔
public const float BeamDestroyRadius = 25f;           // 1回のビームで破壊する半径(局所)
public const float TripodTrailContamRadius = 30f;     // 軌跡に残す汚染半径
public const float TripodTrailContamIntervalSeconds = 3f; // 軌跡汚染をスタンプする間隔
public const float BeamSkyOffset = 60f;               // ビーム描画の上端(トライポッド頭上の高さ)
```
`src/AlienInvasion/Game/Effects.cs` に追加:
- `public static void PlayBeam(Vector3 groundPoint, Vector3 from)`:
  既存 `PlayLightningStrike` の LineRenderer 実装パターンを流用し、赤系の細いビームを一瞬描画
  （色は赤 `new Color(1f,0.1f,0.1f)`。既存のマテリアルキャッシュ手法に倣う）。メインスレッド専用。

`TripodManager`:
- メイン側 `UpdateVisual` で `_beamTimer` を進め、閾値超えで各 Tripod 頭上→現在地へ `Effects.PlayBeam`（描画のみ）。
- sim 側 `UpdateSimulation`（**新規** `TripodManager.UpdateSimulation()`、sim スレッド）:
  - `_beamDestroyTimer`（sim 側の間隔カウンタ、`BeamIntervalSeconds` 相当を tick 換算 or 経過秒で）で
    各トライポッド現在地（メインが書いた位置のスナップショット読取）付近を
    `DisasterHelpers.DestroyStuff(seed, null, pos, BeamDestroyRadius, BeamDestroyRadius, 0f, …)` で局所破壊
    （**preRadius=totalRadius の罠回避**を Phase 1 と同様に厳守。0 を渡さない）。
  - `_trailTimer` で `TripodTrailContamIntervalSeconds` ごとに各現在地へ
    `ContaminationZone(x, z, TripodTrailContamRadius, nowTicks)` を `ContaminationManager.AddZone`。
    → 既存の維持/期限（2か月）/デカール同期がそのまま効く。

`InvasionManager.UpdateSimulation` 配線:
- `case TripodsActive`（sim）: `TripodManager.UpdateSimulation()` を呼ぶ（DisasterHelpers/汚染のみ）。
- 位置スナップショットは、メインが書いた `Tripod.Position` を sim が読む形（single-writer=メイン）。
  母船 `_target` と同じ良性レース扱いで可（コメントで明示）。

ユニットテスト不可。`build.ps1` 成功で可。ビーム破壊が確実に建物を消す（preRadius 罠回避）ことを
コードレビューで確認。

---

## 完了条件

- 全タスク review clean、`build.ps1` 成功・mod 配置成功、`dotnet test` 全緑。
- 全体レビュー（最上位モデル）で Critical/Important 無し（あれば1回の fix wave で解消）。
- AssetBundle 未生成でも起動〜襲来〜トライポッド活動〜消滅〜汚染2か月 が例外なく通ること。

## スコープ外（Phase 2 では作らない）

- AssetBundle の実ビルド（Unity 5.6.6f2 作業。別途 FBX 書き出し＋手順書で対応）。
- トライポッドをゲーム正規ユニット化（選択・情報パネル）。
- ビーム破壊の棟数厳密バランス調整（実機で ModConfig 調整）。
