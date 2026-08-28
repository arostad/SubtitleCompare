# Subtitle Compare

MKV subtitle comparison tool. Drop an MKV on the window, pick up to three subtitle tracks, and compare them side by side (timestamps line up, wording diffs highlight).

GitHub Actions builds the Windows exe. The repo is private, so downloads go through your GitHub login (`gh`), not an anonymous link.

## Install

Once, in PowerShell (new window after installing `gh`):

```
winget install --id GitHub.cli -e
gh auth login
```

Then, whenever you want the latest exe:

```
gh release download latest -R arostad/subtitle-compare -p SubtitleCompare.exe --clobber -D $env:LOCALAPPDATA\SubtitleCompare
```

Open `%LOCALAPPDATA%\SubtitleCompare\SubtitleCompare.exe`. After 1.0.03, the app can also offer Update when a newer release is up.

You also need ffmpeg:

```
winget install Gyan.FFmpeg
```

## Use

Drop an `.mkv` (or use Open…). Each pane has a dropdown for a subtitle track. Image tracks (PGS, VobSub, DVB) are listed but cannot be compared as text. F7 / F8 jump to the previous / next difference.
