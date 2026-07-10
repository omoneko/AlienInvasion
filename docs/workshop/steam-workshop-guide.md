# Steam Workshop 公開ガイド — Alien Invasion

Cities: Skylines（初代）のコードModをWorkshopに公開するための素材と手順をまとめています。

## 1. タイトル / 基本情報

- **Workshop タイトル:** `Alien Invasion — War of the Worlds`
  （ゲーム内の Mod 名は `Alien Invasion`。Workshop タイトルはこちらの副題付きを推奨）
- **可視性:** まずは `Friends only` か `Unlisted` でテスト公開 → 問題なければ `Public`
- **タグ:** `Mod`
- **必要 DLC の記載:** 説明文に明記済み（Natural Disasters 推奨・After Dark で夜間発光）

## 2. プレビュー画像（サムネイル）

- **採用:** [`preview.png`](preview.png) — 原発Mod（NuclearMeltdown）のプレビューに合わせて自作したカバー画像（1024×1024・2パネル構成）。
  - 上: UFO母船の俯瞰（[`cover-ufo.png`](cover-ufo.png) ベース）＋タイトル「ALIEN / INVASION」＋UFOアイコン
  - 中: 黄黒のハザード帯
  - 下: 3体トライポッドの市街地襲撃（[`cover-tripods.png`](cover-tripods.png) ベース）＋「TRIPOD ASSAULT」ラベル
  - 同作者の原発Modと同じトーンでシリーズ感を出しています。
- **再生成:** [`make_preview.py`](make_preview.py)（Pillow使用）。上下の画像・タイトル色・ラベルを変えて作り直せます。
  実行: `python docs/workshop/make_preview.py`
- Steam のプレビューは小さめ＆一部で正方形にクロップされます。本画像は正方形なので問題ありません。

## 3. ギャラリー画像（Workshopページに追加する順番）

1. [`screenshot-1-mothership.png`](screenshot-1-mothership.png) — 母船が都心上空に飛来（クレーター・落雷つき）
2. [`screenshot-2-night-tripod.png`](screenshot-2-night-tripod.png) — 夜、赤く発光するトライポッド（夜間発光の見せ場）
3. [`screenshot-3-tripod-highway.png`](screenshot-3-tripod-highway.png) — インターチェンジを跨ぐトライポッドと炎上
4. [`screenshot-4-street-attack.png`](screenshot-4-street-attack.png) — 市街地での母船＋トライポッドの破壊

## 4. 説明文

- 本文（BBCode・そのまま貼り付け可）: [`steam-description.txt`](steam-description.txt)
- 英語。見出し/箇条書きは Steam の BBCode（`[h1]` `[list]` `[b]` `[i]`）で記述済み。

## 5. 公開手順（ゲーム内 Content Manager から）

1. ビルド＆配置済みであることを確認（`build.ps1` 実行 → `...\Addons\Mods\AlienInvasion` に配置）。
2. Cities: Skylines を起動 → メインメニューの **Content Manager → Mods**。
3. `Alien Invasion` の行にある **Share（共有）** ボタンをクリック。
4. アップロード画面で:
   - **Title / Description** を入力（上記タイトル、`steam-description.txt` の本文を貼付）
   - **Preview image** に `docs/workshop/preview.png` を指定
   - **Visibility** を選択（テストは Friends only 推奨）
5. アップロード。反映後、Steam の Workshop ページで **ギャラリー画像**（上記4枚）を追加。

> 補足: コードModは実体（`AlienInvasion.dll` と `Models/` `Sounds/`）が Mod フォルダに揃っていれば、
> Content Manager からそのまま共有できます。更新時は同じ Share ボタンから再アップロードされます。

## 6. 公開前チェックリスト

- [ ] 実機でひと通り動作確認（召喚・破壊・トライポッド・汚染・夜間発光・効果音・一時停止）
- [ ] `preview.png` が意図した画像になっている
- [ ] 説明文の DLC 注記（Natural Disasters / After Dark）が正しい
- [ ] まず Friends only / Unlisted で試験公開 → 問題なければ Public
