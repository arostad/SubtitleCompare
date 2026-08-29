# Installs SubtitleCompare.exe to %LOCALAPPDATA%\SubtitleCompare
# and creates Desktop + Start Menu shortcuts.
# Requires: GitHub CLI signed in (`gh auth login`).
$ErrorActionPreference = "Stop"
$Repo = "arostad/subtitle-compare"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"

Write-Host "Installing Subtitle Compare..."

$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
            [System.Environment]::GetEnvironmentVariable("Path", "User")

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) not found. In a new PowerShell window run: winget install --id GitHub.cli -e"
}

gh auth status 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Sign in to GitHub (browser will open)..."
    gh auth login --hostname github.com --git-protocol https --web
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

Write-Host "Downloading SubtitleCompare.exe from the Latest release..."
gh release download latest --repo $Repo --pattern "SubtitleCompare.exe" --clobber --dir $InstallDir

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
