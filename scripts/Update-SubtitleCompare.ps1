# Pulls the latest SubtitleCompare.exe from the public GitHub release.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Url = "https://github.com/arostad/subtitle-compare/releases/download/latest/SubtitleCompare.exe"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
$PendingPath = Join-Path $InstallDir "SubtitleCompare.exe.new"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$running = Get-Process -Name "SubtitleCompare" -ErrorAction SilentlyContinue
if ((Test-Path $PendingPath) -and -not $running) {
    Move-Item -Force $PendingPath $ExePath
}

$dest = if ($running) { $PendingPath } else { $ExePath }
Write-Host "Downloading SubtitleCompare.exe..."
Invoke-WebRequest -Uri $Url -OutFile $dest -UseBasicParsing

if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 1MB) {
    throw "Download failed. File is missing or too small: $dest"
}

if ($running) {
    Write-Host "App is running. Close it and the new exe will replace it, or use Update in the app."
} else {
    Write-Host "Updated: $ExePath"
}
