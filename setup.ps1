# Copies the Unity service folders (ProjectSettings, Packages) from a freshly
# created empty Unity project into this repository, turning the checked-in
# Assets folder into a project Unity Hub can open.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File setup.ps1 -TempProject "$env:USERPROFILE\Desktop\PoolFPS_temp"

param(
    [Parameter(Mandatory = $true)]
    [string]$TempProject
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path $repoRoot "PoolFPS"

if (-not (Test-Path $TempProject)) {
    throw "Temp project not found: $TempProject"
}

foreach ($folder in @("ProjectSettings", "Packages")) {
    $source = Join-Path $TempProject $folder
    if (-not (Test-Path $source)) {
        throw "$folder not found in $TempProject. Did Unity finish creating the project?"
    }

    $destination = Join-Path $target $folder
    if (Test-Path $destination) {
        Write-Host "$folder already exists in PoolFPS - skipping."
        continue
    }

    Copy-Item -Path $source -Destination $destination -Recurse
    Write-Host "Copied $folder"
}

Write-Host ""
Write-Host "Done. Now open Unity Hub -> Add -> select:"
Write-Host "  $target"
Write-Host "Then run the menu command: Tools > Pool FPS > BUILD EVERYTHING"
