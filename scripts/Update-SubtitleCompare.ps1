# Pulls the latest SubtitleCompare.exe from the private GitHub release.
$ErrorActionPreference = "Stop"
$Repo = "arostad/subtitle-compare"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
$PendingPath = Join-Path $InstallDir "SubtitleCompare.exe.new"
$TempPath = Join-Path $InstallDir "SubtitleCompare.exe.download"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is not on PATH. Install it with: winget install --id GitHub.cli -e"
}

gh auth status 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not signed in. Run: gh auth login"
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$running = Get-Process -Name "SubtitleCompare" -ErrorAction SilentlyContinue
if ((Test-Path $PendingPath) -and -not $running) {
    Move-Item -Force $PendingPath $ExePath
}

$release = gh api "repos/$Repo/releases/tags/latest" | ConvertFrom-Json
$asset = $release.assets | Where-Object { $_.name -eq "SubtitleCompare.exe" } | Select-Object -First 1
if (-not $asset) {
    throw "No SubtitleCompare.exe on the Latest release."
}

if (Test-Path $ExePath) {
    $local = Get-Item $ExePath
    if ([int64]$local.Length -eq [int64]$asset.size) {
        Write-Host "Subtitle Compare is already up to date."
        exit 0
    }
}

Write-Host "Downloading SubtitleCompare.exe ($([math]::Round($asset.size/1MB,1)) MB)..."
if (Test-Path $TempPath) { Remove-Item -Force $TempPath }
gh release download latest --repo $Repo --pattern "SubtitleCompare.exe" --clobber --output $TempPath

$dest = $(if ($running) { $PendingPath } else { $ExePath })
Move-Item -Force $TempPath $dest

if ($dest -eq $PendingPath) {
    Write-Host "Update downloaded. It will apply the next time Subtitle Compare is not running."
} else {
    Write-Host "Installed to $ExePath"
}
