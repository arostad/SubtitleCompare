# Subtitle Compare

MKV subtitle comparison tool. Drop an MKV on the window, pick up to three subtitle tracks, and compare them side by side.

The repo is private. GitHub Actions builds the exe. You install it with GitHub CLI (`gh`) so the download stays authenticated.

## Install

Once:

```
winget install --id GitHub.cli -e
```

Close PowerShell, open a new window, then:

```
gh auth login
```

Then run the installer (saves it to a file first, then runs it):

```
gh api -H "Accept: application/vnd.github.raw" repos/arostad/subtitle-compare/contents/scripts/Install-SubtitleCompare.ps1 | Set-Content -Encoding utf8 $env:TEMP\Install-SubtitleCompare.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File $env:TEMP\Install-SubtitleCompare.ps1
```

That puts the exe in `%LOCALAPPDATA%\SubtitleCompare` and creates Desktop and Start Menu shortcuts. Later versions: use Update in the app.

You need ffmpeg on PATH (`winget install Gyan.FFmpeg` if you do not already have it).

## Use

Drop an `.mkv` (or Open…). Each pane picks a subtitle track. Image tracks (PGS, VobSub, DVB) cannot be compared as text. F7 / F8 jump differences.
