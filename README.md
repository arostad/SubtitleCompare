# Subtitle Compare

MKV subtitle comparison tool. Drop an MKV on the window, pick up to three subtitle tracks, and compare them side by side (timestamps line up, wording diffs highlight).

The Windows exe is built automatically. You do not need the .NET SDK.

## Install

On the Windows PC, in PowerShell:

```
winget install --id GitHub.cli -e
gh auth login
gh api -H "Accept: application/vnd.github.raw" repos/arostad/subtitle-compare/contents/scripts/Install-SubtitleCompare.ps1 | powershell -NoProfile -ExecutionPolicy Bypass -
```

That installs the app, adds Desktop and Start Menu shortcuts, and checks GitHub for a new exe when you sign in to Windows. After the first install, the app also shows an Update banner when a new version is out.

You also need ffmpeg:

```
winget install Gyan.FFmpeg
```

Or download the exe yourself from [Latest release](https://github.com/arostad/subtitle-compare/releases/latest).

## Use

Drop an `.mkv` (or use Open…). Each pane has a dropdown for a subtitle track. Image tracks (PGS, VobSub, DVB) are listed but cannot be compared as text. F7 / F8 jump to the previous / next difference.
