# Installs SubtitleCompare.exe to %LOCALAPPDATA%\SubtitleCompare
# and creates Desktop + Start Menu shortcuts.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Url = "https://github.com/arostad/subtitle-compare/releases/download/latest/SubtitleCompare.exe"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"

Write-Host "Installing Subtitle Compare..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

Write-Host "Downloading SubtitleCompare.exe from the Latest release..."
Invoke-WebRequest -Uri $Url -OutFile $ExePath -UseBasicParsing

if (-not (Test-Path $ExePath) -or (Get-Item $ExePath).Length -lt 1MB) {
    throw "Download failed. SubtitleCompare.exe is missing or too small in $InstallDir"
}

$Wsh = New-Object -ComObject WScript.Shell
$startDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Force -Path $startDir | Out-Null

$startLnk = Join-Path $startDir "Subtitle Compare.lnk"
$deskLnk = Join-Path ([Environment]::GetFolderPath("Desktop")) "Subtitle Compare.lnk"
foreach ($path in @($startLnk, $deskLnk)) {
    $s = $Wsh.CreateShortcut($path)
    $s.TargetPath = $ExePath
    $s.WorkingDirectory = $InstallDir
    $s.IconLocation = $ExePath
    $s.Save()
}

Write-Host ""
Write-Host "Installed: $ExePath"
Write-Host "Shortcuts: Desktop and Start Menu."
Write-Host "Open the app from either one. When a new version is published, use Update in the app."
