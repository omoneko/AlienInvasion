$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild が見つかりません" }

& $msbuild "src\AlienInvasion\AlienInvasion.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "ビルド失敗" }

$dll = "src\AlienInvasion\bin\Release\AlienInvasion.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

$bundleDir = Join-Path $modDir "Assets"
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
$bundleSrc = "src\AlienInvasion\Assets\alieninvasion.bundle"
if (Test-Path $bundleSrc) {
    Copy-Item $bundleSrc $bundleDir -Force
    Write-Host "AssetBundle を配置しました"
} else {
    Write-Host "警告: $bundleSrc が見つかりません。ビジュアル(母船/赤デカール)は起動時スキップされます。"
}

$modelsDir = Join-Path $modDir "Models"
New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null
$modelsSrcDir = "src\AlienInvasion\Models"
if (Test-Path $modelsSrcDir) {
    Copy-Item (Join-Path $modelsSrcDir "*") $modelsDir -Force
    Write-Host "OBJ/MTLモデルを配置しました"
} else {
    Write-Host "警告: $modelsSrcDir が見つかりません。AssetBundle無しの場合モデルは表示されません。"
}

$soundsDir = Join-Path $modDir "Sounds"
New-Item -ItemType Directory -Force -Path $soundsDir | Out-Null
$soundsSrcDir = "src\AlienInvasion\Sounds"
if (Test-Path $soundsSrcDir) {
    Copy-Item (Join-Path $soundsSrcDir "*") $soundsDir -Force
    Write-Host "効果音(wav)を配置しました"
} else {
    Write-Host "警告: $soundsSrcDir が見つかりません。効果音は再生されません。"
}

# タブ内アイコン(災害パネル)。前処理済みの透過アイコン icon_tab.png(格子背景を除去・被写体に切り抜き・512px)を配置する。
# 無ければ原画 icon.png、それも無ければ手続き生成シルエットにフォールバックする。
$iconDst = Join-Path $modDir "icon.png"
if (Test-Path "icon_tab.png") {
    Copy-Item "icon_tab.png" $iconDst -Force
    Write-Host "icon_tab.png(透過タブアイコン)を配置しました"
} elseif (Test-Path "icon.png") {
    Copy-Item "icon.png" $iconDst -Force
    Write-Host "icon.png(原画)を配置しました(icon_tab.png なし)"
} else {
    Write-Host "警告: アイコンが見つかりません。タブアイコンは手続き生成シルエットになります。"
}

# Workshop用プレビュー画像。Mod共有時にCSはMODフォルダ直下の PreviewImage.png を使う。
# 無いと既定のプレースホルダ(青写真)のままになり画像を変更できないため、毎回配置する。
$previewSrc = "docs\workshop\PreviewImage.png"
if (Test-Path $previewSrc) {
    Copy-Item $previewSrc (Join-Path $modDir "PreviewImage.png") -Force
    Write-Host "PreviewImage.png を配置しました"
} else {
    Write-Host "警告: $previewSrc が見つかりません。Workshopプレビューはプレースホルダのままになります。"
}
Write-Host "配置完了: $modDir"
