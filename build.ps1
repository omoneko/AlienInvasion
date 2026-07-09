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
Write-Host "配置完了: $modDir"
