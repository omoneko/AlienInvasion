# Alien Invasion (Cities: Skylines Mod)

UFO母船が飛来し、雷を連打して地面にクレーターを形成、周辺の建物を破壊、放射能汚染(赤)をゲーム内1年残す。手動発動キー: **F7**（`Game/ModConfig.cs`の`ManualTriggerKey`で変更可）。ランダムでも低確率発生する。

## AssetBundle の作り方（Blenderモデル→ゲームで使える形式）

1. **Unity Editor 5.6.6f2** をインストール（[Unity Archive](https://unity3d.com/get-unity/download/archive) から取得。Cities: Skylines のエンジン(Unity 5.6.7)と互換のバージョン）。
2. `models/source/models.blend` を Blender で開き、`MotherShip` と `TriPod`（本フェーズでは未使用）、赤いデカール用の平面オブジェクトをそれぞれ **FBX でエクスポート**。
   - 注: `MotherShip` と `TriPod` には既にマテリアル（`MetallicGray` ベースマテリアルとカスタムアクセント色マテリアル）が設定済みです。Blender の FBX エクスポーターはこれらのマテリアル割り当てを自動的に含めるため、Unity インポート後はマテリアルがそのまま引き継がれます。このステップは、FBX をクリーンにエクスポートし、Unity でマテリアルが正しくインポートされたことを確認することが目標です（ゼロから作成する必要はありません）。
   - `MotherShip` のピボットは幾何中心（確認済み・修正不要）。
   - デカール用オブジェクトは中心/接地面にピボットを置く。
   - エクスポート前に `Ctrl+A → Scale` で各オブジェクトのスケールを適用しておく（未適用スケールが残ったままだとFBXインポート後の挙動が予測しにくくなるため）。
3. `unity-project` を Unity Editor 5.6.6f2 で開く。
4. エクスポートしたFBXを `unity-project/Assets/` にインポートし、マテリアルが正しく適用されていることを確認（`_d`色/`_n`ノーマル/`_s`スペキュラ/`_i`自発光/`_a`透明）。
5. 各モデルを Prefab 化し、**Prefab名を正確に** `Mothership` / `ContaminationDecal` にする（`Game/ModConfig.cs` の `MothershipPrefabName`/`RedDecalPrefabName` と一致させる）。
6. 各Prefabの Inspector で **AssetBundle名** を `alieninvasion` に設定（Prefab選択 → Inspector右下の AssetBundle ドロップダウン）。
7. Unity メニュー **AlienInvasion → Build AssetBundle** を実行。
8. `unity-project/AssetBundles/alieninvasion` というファイル（拡張子なし）が生成されるので、**`alieninvasion.bundle`** にリネームして `src/AlienInvasion/Assets/alieninvasion.bundle` に配置。
9. `build.ps1` を再実行するとModフォルダへ自動配置される。

AssetBundleが無い状態でもModはビルド・起動でき、母船/デカールの視覚演出のみスキップされる（ログに `AssetBundle not found` と出る）。

## ビルドと配置
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion\` に配置される。

## ゲーム内動作確認手順
1. Content Manager → Mods で "Alien Invasion" を有効化。
2. ゲーム内で **F7** を押す（または待ってランダム発生を確認）。
3. 母船が降下 → 雷を連打しながらクレーターが形成される → 周辺建物が破壊される → 上昇して消える。
4. 跡地に赤い汚染が残ることを確認（AssetBundle未配置の場合は標準の土壌汚染のみ）。
5. ゲーム内1年経過で汚染が自動消滅することを確認。
6. セーブ→ロードで汚染が維持されることを確認。

## 設定
定数は `Game/ModConfig.cs`（発動キー・確率・半径・時間等）。

## ログ
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\` の output_log で `[AlienInvasion]` を検索。
