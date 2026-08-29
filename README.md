# Subtitle Compare

MKV subtitle comparison tool. Drop an MKV on the window, pick up to three subtitle tracks, and compare them side by side.

The repo is private. GitHub Actions builds the Windows exe. Install uses GitHub CLI (`gh`) so the download stays signed in as you.

## Fresh Windows install

Do these in order. One command at a time. Do not paste the next line until the current one finishes.

1. Open PowerShell and install GitHub CLI:

```
winget install --id GitHub.cli -e
```

2. Close that PowerShell window and open a new one. `gh` will not be found if you skip this.

3. Sign in to GitHub (browser opens):

```
gh auth login
```

Use GitHub.com, HTTPS. The “unable to find git executable” warning is fine if you do not have Git for Windows.

4. Save the installer, then run it:

```
gh api -H "Accept: application/vnd.github.raw" repos/arostad/subtitle-compare/contents/scripts/Install-SubtitleCompare.ps1 | Set-Content -Encoding utf8 $env:TEMP\Install-SubtitleCompare.ps1
```

```
powershell -NoProfile -ExecutionPolicy Bypass -File $env:TEMP\Install-SubtitleCompare.ps1
```

The second command downloads `SubtitleCompare.exe` (about 140 MB) to `%LOCALAPPDATA%\SubtitleCompare` and creates Desktop and Start Menu shortcuts.

5. Open **Subtitle Compare** from the Desktop or Start Menu.

ffmpeg must already be on PATH. If a new PC does not have it:

```
winget install Gyan.FFmpeg
```

Then open a new PowerShell / restart the app.

## Updates

After this first install, use **Update** in the app when it says a new version is available. You do not move the exe by hand.

## Use

Drop an `.mkv` (or Open…). Each pane picks a subtitle track. Image tracks (PGS, VobSub, DVB) cannot be compared as text. F7 / F8 jump differences.
