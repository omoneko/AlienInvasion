# Alien Invasion — Cities: Skylines (初代) Mod

SimCity 4 のエイリアン襲来をオマージュした災害Mod。UFO母船が飛来して雷で陥没穴を作り街を破壊、
続いて3体のトライポッドがランダムに歩き回りレーザーで建物を局所破壊し、跡地に赤い汚染を残す。

- 対象: Cities: Skylines 初代 / .NET Framework 3.5
- 発動: 手動（**F7キー** または **UFO召喚ボタン**で地点をクリック指定）＋ 低確率のランダム発生
- 汚染: ゲーム内 **2か月** で自然消滅（除染施設なし）

## 演出の流れ

1. 母船が上空から降下し、回転しながらホバリング
2. 雷を連打して直下に**災害規模5.5相当の陥没穴**を形成＋範囲内の建物を破壊
3. 母船が上昇して消滅
4. 陥没跡付近に**トライポッド3体**が出現、ランダムに自由移動
5. 各トライポッドが一定間隔でレーザーを発射し、足元付近の建物を**局所破壊**＋軌跡に赤い汚染をスタンプ
6. 活動時間（既定40秒）後にトライポッド消滅、汚染は2か月残留

数値（時間・半径・体数・確率など）はすべて `src/AlienInvasion/Game/ModConfig.cs` の定数で調整可能。

## プロジェクト構成

```
src/AlienInvasion/
  Core/    Unity非依存の純粋ロジック（状態機械・歩行数学・汚染ゾーン・直列化）… xUnitでテスト
  Game/    ゲーム統合層（母船/トライポッド/エフェクト/汚染/発動/セーブ）
tests/AlienInvasion.Core.Tests/   Core のユニットテスト（52件）
models/                            Blenderソース(.blend) と 書き出しFBX(models/export/)
unity-project/                     AssetBundle ビルド用 Unityプロジェクト
docs/specs, docs/plans             設計書・実装計画
```

## Mod のビルドと配置

```powershell
.\build.ps1
```
MSBuild で `AlienInvasion.dll` をビルドし、
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\` へ配置する。
`src\AlienInvasion\Assets\alieninvasion.bundle` が存在すれば同時に配置される（無ければビジュアルは
スキップされ、ロジックのみ動作する — 下記のAssetBundle手順で生成する）。

Core のテスト:
```powershell
dotnet test tests\AlienInvasion.Core.Tests\AlienInvasion.Core.Tests.csproj
```

## AssetBundle（UFO・トライポッド・赤デカール）の作成

モデル本体は AssetBundle に同梱する。CS 本体と同じ Unity バージョンでビルドする必要がある。

- 必要: **Unity Editor 5.6.6f2**（CS本体は Unity 5.6.7。5.6.x系は相互互換）
- モデルFBXは書き出し済み: `models/export/Mothership.fbx`, `models/export/Tripod.fbx`
  （Blenderで各オブジェクトを原点配置・-Z forward/Y up・接地ピボットで書き出したもの）

手順:
1. Unity 5.6.6f2 で `unity-project/` を開く。
2. `Mothership.fbx` と `Tripod.fbx` を `Assets/` にインポート。
   - スケールが大きい/小さい場合は Import Settings の Scale Factor か、ゲーム側 `ModConfig.MothershipScale` /
     `TripodScale` で調整（母船 約199m径、トライポッド 約65m高が既定）。
3. 各FBXから**プレハブ**を作成し、**プレハブ名を厳密に**次のとおりにする（コードが名前で読み込む）:
   - UFO母船 → `Mothership`
   - トライポッド → `Tripod`
   - 赤い汚染デカール → `ContaminationDecal`
     （FBX不要。Unityで Quad を作り赤い半透明マテリアルを割り当てただけの平面プレハブでよい）
4. 3つのプレハブを選択し、Inspector 最下部の **AssetBundle** 欄で新規バンドル名 `alieninvasion.bundle` を割り当てる。
   - 対応する `ModConfig` の定数: `MothershipPrefabName="Mothership"`, `TripodPrefabName="Tripod"`,
     `RedDecalPrefabName="ContaminationDecal"`, `AssetBundleFileName="alieninvasion.bundle"`。
5. メニュー **AlienInvasion → Build AssetBundle** を実行（`unity-project/Assets/Editor/BuildAssetBundles.cs`）。
   `unity-project/AssetBundles/alieninvasion.bundle` が生成される。
6. 生成物を `src/AlienInvasion/Assets/alieninvasion.bundle` にコピーし、`.\build.ps1` を再実行。
   （`build.ps1` がバンドルを Mod フォルダへ配置する。）

バンドル配置後にゲームを再起動すると、母船・トライポッド・赤デカールが表示される。
バンドルが無くても Mod はクラッシュせず、ビジュアルのみスキップして全ロジックが動作する。

## 操作

| 操作 | 内容 |
|------|------|
| **F7** | UFO配置ツールを起動（地点を左クリックで襲来開始） |
| **UFO召喚ボタン** | 同上（画面のボタンからツール起動） |
| 自動 | 一定周期で低確率抽選し、ランダム地点で襲来（`ModConfig` でON/OFF・頻度調整） |
