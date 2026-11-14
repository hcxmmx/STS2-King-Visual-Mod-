param(
    [string]$GodotPath = 'D:\STS2\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe',
    [switch]$Prepare,
    [switch]$Settings
)
$ErrorActionPreference = 'Stop'
$requiredAssets = @('Generated\battle_data.json', 'Generated\king_source.pck', 'Generated\ScarfRig.gd', 'Generated\BattleAudio', 'Generated\ExpansionAssets', 'Generated\ShieldAssets', 'ShieldPresentation.gd')
$generationSources = @('prepare_preview.py', 'prepare_battle_preview.py', 'prepare_extra_weapons.py', 'draft_weapons.json', '..\02_Scripts_Tools\weapon_sources.py', '..\03-godot-project\Hcxmmx_King\Presentation\weapons.json', '..\03-godot-project\Hcxmmx_King\Presentation\weapon_overrides.json', '..\03-godot-project\Hcxmmx_King\Presentation\fx_alignment.json', '..\03-godot-project\Hcxmmx_King\Presentation\ScarfRig.gd')
$generationSources += @('..\02_Scripts_Tools\prepare_shield_presentation.py', '..\03-godot-project\Hcxmmx_King\Presentation\ShieldPresentation.gd')
$generationSources += @('prepare_ranged_preview.py')
$requiredAssets += @('Generated\ranged_data.json', 'Generated\RangedAssets')
$requiredAssets += @('Generated\ImpactAssets', 'ImpactPresentation.gd')
$requiredAssets += @('Generated\OriginalHitAssets', 'Generated\original_hit_data.json')
$generationSources += @('prepare_original_hits.py')
$generationSources += @('..\02_Scripts_Tools\prepare_impact_presentation.py', '..\03-godot-project\Hcxmmx_King\Presentation\ImpactPresentation.gd')
$missingAssets = @($requiredAssets | Where-Object { -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $_)) })
$previewData = Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Generated\battle_data.json') -ErrorAction SilentlyContinue
$staleSources = @($generationSources | Where-Object { $previewData -and (Get-Item -LiteralPath (Join-Path $PSScriptRoot $_)).LastWriteTimeUtc -gt $previewData.LastWriteTimeUtc })
if ($Prepare -or $missingAssets.Count -gt 0 -or $staleSources.Count -gt 0) {
    python (Join-Path $PSScriptRoot 'prepare_battle_preview.py')
    if ($LASTEXITCODE -ne 0) { throw 'Preview asset preparation failed.' }
}
if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) { throw "Godot not found: $GodotPath" }
if ($Settings) {
    Start-Process -FilePath $GodotPath -ArgumentList @('--path', ('"' + $PSScriptRoot + '"'), '--script', 'res://SettingsPreview.gd', '--position', '90,70', '--log-file', ('"' + (Join-Path $PSScriptRoot 'Generated\settings-preview.log') + '"')) -WorkingDirectory $PSScriptRoot
    return
}
# The visible window is the requested deliverable; no game or mod deployment occurs.
Start-Process -FilePath $GodotPath -ArgumentList @('--path', ('"' + $PSScriptRoot + '"'), '--position', '90,70', '--log-file', ('"' + (Join-Path $PSScriptRoot 'Generated\preview.log') + '"')) -WorkingDirectory $PSScriptRoot
