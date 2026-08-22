# Copies this repo's game code into a freshly created, empty Unity project.
#
# Unity project folders need ProjectSettings/Packages that only Unity itself
# can generate correctly (they're version-specific). This repo ships only the
# Assets subfolders (Scripts, Editor, Plugins) — no ProjectSettings — so a
# fresh Unity project is created first (empty, so Unity Hub accepts it), and
# this script drops our code into its Assets folder afterward.
#
# Usage:
#   1. Unity Hub -> New Project -> "3D (Built-in Render Pipeline)" ->
#      name it EpicBattle3D, put it wherever you like (e.g. Desktop) -> Create.
#      Wait for Unity to finish opening, then close Unity.
#   2. powershell -ExecutionPolicy Bypass -File setup.ps1 -UnityProject "$env:USERPROFILE\Desktop\EpicBattle3D"
#   3. Reopen that project in Unity Hub, run Tools > Epic Battle 3D > BUILD EVERYTHING.

param(
    [Parameter(Mandatory = $true)]
    [string]$UnityProject
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $repoRoot "EpicBattle3D\Assets"

if (-not (Test-Path $UnityProject)) {
    throw "Unity project not found: $UnityProject. Create it in Unity Hub first (see the usage notes at the top of this script)."
}

$destinationAssets = Join-Path $UnityProject "Assets"
if (-not (Test-Path $destinationAssets)) {
    throw "$UnityProject does not look like a Unity project (no Assets folder). Create it in Unity Hub first."
}

foreach ($folder in @("Scripts", "Editor", "Plugins")) {
    $sourceFolder = Join-Path $source $folder
    $destinationFolder = Join-Path $destinationAssets $folder

    if (Test-Path $destinationFolder) {
        Write-Host "$folder already exists in the Unity project - skipping. Delete it first if you want a clean copy."
        continue
    }

    Copy-Item -Path $sourceFolder -Destination $destinationFolder -Recurse
    Write-Host "Copied $folder"
}

Write-Host ""
Write-Host "Done. Now open Unity Hub -> open the project at:"
Write-Host "  $UnityProject"
Write-Host "Then run the menu command: Tools > Epic Battle 3D > BUILD EVERYTHING"
