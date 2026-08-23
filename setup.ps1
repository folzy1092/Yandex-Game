# Copies one of this repo's games into a freshly created, empty Unity project.
#
# Unity project folders need ProjectSettings/Packages that only Unity itself can
# generate correctly (they're version-specific). This repo ships only the Assets
# subfolders (Scripts, Editor, Plugins) — no ProjectSettings — so a fresh Unity
# project is created first (empty, so Unity Hub accepts it), and this script
# drops the game's code into its Assets folder afterward.
#
# Usage:
#   1. Unity Hub -> New Project -> "3D (Built-in Render Pipeline)" -> create it
#      somewhere outside this repo (e.g. the Desktop). Wait for Unity to finish
#      opening, then close Unity.
#   2. powershell -ExecutionPolicy Bypass -File setup.ps1 -Game DroneStrike -UnityProject "$env:USERPROFILE\Desktop\DroneStrike"
#   3. Reopen that project in Unity Hub, then run its BUILD EVERYTHING menu command:
#        DroneStrike   -> Tools > Drone Strike > BUILD EVERYTHING
#        EpicBattle3D  -> Tools > Epic Battle 3D > BUILD EVERYTHING

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("DroneStrike", "EpicBattle3D")]
    [string]$Game,

    [Parameter(Mandatory = $true)]
    [string]$UnityProject
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $repoRoot "$Game\Assets"

if (-not (Test-Path $source)) {
    throw "Game not found in this repo: $source"
}

if (-not (Test-Path $UnityProject)) {
    throw "Unity project not found: $UnityProject. Create it in Unity Hub first (see the usage notes at the top of this script)."
}

$destinationAssets = Join-Path $UnityProject "Assets"
if (-not (Test-Path $destinationAssets)) {
    throw "$UnityProject does not look like a Unity project (no Assets folder). Create it in Unity Hub first."
}

foreach ($folder in @("Scripts", "Editor", "Plugins")) {
    $sourceFolder = Join-Path $source $folder
    if (-not (Test-Path $sourceFolder)) { continue }

    $destinationFolder = Join-Path $destinationAssets $folder

    if (Test-Path $destinationFolder) {
        Write-Host "$folder already exists in the Unity project - replacing it."
        Remove-Item -Path $destinationFolder -Recurse -Force
    }

    Copy-Item -Path $sourceFolder -Destination $destinationFolder -Recurse
    Write-Host "Copied $folder"
}

# Downloaded model files (Resources/Models only, not the whole Resources tree):
# generated textures and materials also live under Assets/Resources in the
# Unity project, and those are created locally by the "Generate Materials"
# menu command rather than tracked in the repo. Replacing all of Resources on
# every sync would delete that generated content and force a full rebuild.
$modelsSource = Join-Path $source "Resources\Models"
if (Test-Path $modelsSource) {
    $modelsDestination = Join-Path $destinationAssets "Resources\Models"

    if (Test-Path $modelsDestination) {
        Write-Host "Resources/Models already exists in the Unity project - replacing it."
        Remove-Item -Path $modelsDestination -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $destinationAssets "Resources") -Force | Out-Null
    Copy-Item -Path $modelsSource -Destination $modelsDestination -Recurse
    Write-Host "Copied Resources/Models"
}

Write-Host ""
Write-Host "Done. Now open Unity Hub -> open the project at:"
Write-Host "  $UnityProject"
Write-Host "Then run the BUILD EVERYTHING command for $Game from the Tools menu."
