# Subtitle Compare

Windows app for comparing subtitle tracks inside an MKV (or similar container). Drop a file on the window, pick up to three tracks, and read them side by side the way Beyond Compare lines up text.

GitHub Actions builds the Windows exe on each push. The latest build is on the [Latest release](https://github.com/arostad/subtitle-compare/releases/tag/latest).

## Features

- **Three compare panes.** Each column has its own track dropdown, including `(none)` so you can compare two tracks or look at one alone.
- **Drop or Open.** Accepts `.mkv`, `.mka`, `.mks`, `.mp4`, and `.m4v`. `Ctrl+O` opens a file picker.
- **Timestamp alignment.** Cues are lined up by overlapping start/end times, not by cue number, so tracks that split lines differently still sit on the same row.
- **Word-level diffs.** Shared wording stays plain. Changed words are highlighted, wording unique to one track is marked, and a missing cue shows a muted “no cue” row instead of a loud empty hole.
- **Jump differences.** Previous / Next difference buttons, plus `F7` / `F8`.
- **Synced scrolling.** All three columns move together.
- **SDH hints.** A quiet line under the dropdown (and a matching status-bar note) when a track looks like SDH:
  - hearing-impaired flag and/or an SDH-style title (`SDH subtitle track (from track title & flag)`, or title-only / flag-only)
  - otherwise a text scan for a lot of `[brackets]`, `(parentheses)`, or music notes (`Potential SDH subtitle track detected`)
- **Forced hints.** The same title / flag / both wording for forced tracks (`Forced subtitle track (from track flag)`, and so on). A track can show both SDH and forced.
- **Windows light and dark.** Follows the OS theme, including the title bar.
- **In-app updates.** On launch (and from About → Check for updates) the app offers to download the new exe and restart.
- **Text tracks only.** Image subtitles (PGS, VobSub, DVB) cannot be compared as text; the pane says so.

Vibe coded by [Andy Rostad](https://github.com/arostad).

## Requirements

`ffmpeg` and `ffprobe` must be on PATH. If a new PC does not have them:

```
winget install Gyan.FFmpeg
```

Then open a new PowerShell window or restart the app.

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

You can also download `SubtitleCompare.exe` from the [Latest release](https://github.com/arostad/subtitle-compare/releases/tag/latest) if you would rather skip the installer.

## Updates

After the first install, use **Update** in the app when it says a new version is available. You do not move the exe by hand.

## Use

Drop an `.mkv` (or Open…). Each pane picks a subtitle track. Image tracks cannot be compared as text. `F7` / `F8` jump differences.
