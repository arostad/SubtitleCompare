# One-time install: download the exe, Start Menu shortcut, auto-update at logon.
$ErrorActionPreference = "Stop"
$Repo = "arostad/subtitle-compare"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
$UpdateScript = Join-Path $InstallDir "Update-SubtitleCompare.ps1"
$TaskName = "SubtitleCompare Update"

Write-Host "Installing Subtitle Compare..."

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "Installing GitHub CLI..."
    winget install --id GitHub.cli -e --accept-package-agreements --accept-source-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path", "User")
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "Could not find gh after install. Close this window, open a new PowerShell, and run this installer again."
}

gh auth status 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Sign in to GitHub (browser will open)..."
    gh auth login --hostname github.com --git-protocol https --web
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

# Copy updater next to the exe (from this script's folder, or fetch from the repo).
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcUpdate = Join-Path $here "Update-SubtitleCompare.ps1"
if (Test-Path $srcUpdate) {
    Copy-Item -Force $srcUpdate $UpdateScript
} else {
    gh api -H "Accept: application/vnd.github.raw" "repos/$Repo/contents/scripts/Update-SubtitleCompare.ps1" |
        Set-Content -Path $UpdateScript -Encoding UTF8
}

& $UpdateScript

$Wsh = New-Object -ComObject WScript.Shell
$startDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Force -Path $startDir | Out-Null
$lnk = $Wsh.CreateShortcut((Join-Path $startDir "Subtitle Compare.lnk"))
$lnk.TargetPath = $ExePath
$lnk.WorkingDirectory = $InstallDir
$lnk.IconLocation = $ExePath
$lnk.Save()

$desk = [Environment]::GetFolderPath("Desktop")
$deskLnk = $Wsh.CreateShortcut((Join-Path $desk "Subtitle Compare.lnk"))
$deskLnk.TargetPath = $ExePath
$deskLnk.WorkingDirectory = $InstallDir
$deskLnk.IconLocation = $ExePath
$deskLnk.Save()

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$UpdateScript`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null

Write-Host ""
Write-Host "Done. Subtitle Compare is in the Start Menu and on your Desktop."
Write-Host "It will check GitHub for a new exe when you sign in to Windows."
Write-Host "You still need ffmpeg:  winget install Gyan.FFmpeg"
