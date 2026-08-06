$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\AlienInvasion\AlienInvasion.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\AlienInvasion\bin\Release\AlienInvasion.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\AlienInvasion"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force

$bundleDir = Join-Path $modDir "Assets"
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null
$bundleSrc = "src\AlienInvasion\Assets\alieninvasion.bundle"
if (Test-Path $bundleSrc) {
    Copy-Item $bundleSrc $bundleDir -Force
    Write-Host "Deployed the AssetBundle"
} else {
    Write-Host "Note: $bundleSrc not found; the mothership and decal visuals will be skipped at startup."
}

$modelsDir = Join-Path $modDir "Models"
New-Item -ItemType Directory -Force -Path $modelsDir | Out-Null
$modelsSrcDir = "src\AlienInvasion\Models"
if (Test-Path $modelsSrcDir) {
    Copy-Item (Join-Path $modelsSrcDir "*") $modelsDir -Force
    Write-Host "Deployed OBJ/MTL models"
} else {
    Write-Host "Note: $modelsSrcDir not found; without the AssetBundle no models will appear."
}

$soundsDir = Join-Path $modDir "Sounds"
New-Item -ItemType Directory -Force -Path $soundsDir | Out-Null
$soundsSrcDir = "src\AlienInvasion\Sounds"
if (Test-Path $soundsSrcDir) {
    Copy-Item (Join-Path $soundsSrcDir "*") $soundsDir -Force
    Write-Host "Deployed sounds (wav)"
} else {
    Write-Host "Note: $soundsSrcDir not found; no sounds will play."
}

# Icon for the disasters panel. Deploys the pre-processed transparent icon_tab.png, which has
# had its checkerboard background keyed out, been cropped to the subject and sized to 512px.
# Falls back to the raw icon.png, and then to the procedural silhouette.
$iconDst = Join-Path $modDir "icon.png"
if (Test-Path "icon_tab.png") {
    Copy-Item "icon_tab.png" $iconDst -Force
    Write-Host "Deployed icon_tab.png (transparent panel icon)"
} elseif (Test-Path "icon.png") {
    Copy-Item "icon.png" $iconDst -Force
    Write-Host "Deployed icon.png (raw; no processed icon_tab.png found)"
} else {
    Write-Host "Note: no icon found; the panel icon falls back to the procedural silhouette."
}

# Workshop preview image. CS uses PreviewImage.png at the root of the mod folder when sharing.
# Without it the blue placeholder is used and cannot be changed, so it is deployed every time.
$previewSrc = "docs\workshop\PreviewImage.png"
if (Test-Path $previewSrc) {
    Copy-Item $previewSrc (Join-Path $modDir "PreviewImage.png") -Force
    Write-Host "Deployed PreviewImage.png"
} else {
    Write-Host "Note: $previewSrc not found; the Workshop preview stays as the placeholder."
}
Write-Host "Deploy complete: $modDir"
