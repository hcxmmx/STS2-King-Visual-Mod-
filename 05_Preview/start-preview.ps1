param(
    [string]$GodotPath = 'D:\STS2\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe',
    [switch]$Prepare
)
$ErrorActionPreference = 'Stop'
$requiredAssets = @('Generated\battle_data.json', 'Generated\king_source.pck', 'Generated\ScarfRig.gd', 'Generated\BattleAudio')
$missingAssets = @($requiredAssets | Where-Object { -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $_)) })
if ($Prepare -or $missingAssets.Count -gt 0) {
    python (Join-Path $PSScriptRoot 'prepare_battle_preview.py')
    if ($LASTEXITCODE -ne 0) { throw 'Preview asset preparation failed.' }
}
if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) { throw "Godot not found: $GodotPath" }
# The visible window is the requested deliverable; no game or mod deployment occurs.
Start-Process -FilePath $GodotPath -ArgumentList @('--path', ('"' + $PSScriptRoot + '"'), '--position', '90,70', '--log-file', ('"' + (Join-Path $PSScriptRoot 'Generated\preview.log') + '"')) -WorkingDirectory $PSScriptRoot
