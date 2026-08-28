# Subtitle Compare

MKV subtitle comparison tool.

## Install on Windows (auto-updates)

One-time setup on the Windows PC, in PowerShell:

```
winget install --id GitHub.cli -e
gh auth login
gh api -H "Accept: application/vnd.github.raw" repos/arostad/subtitle-compare/contents/scripts/Install-SubtitleCompare.ps1 | powershell -NoProfile -ExecutionPolicy Bypass -
```

That puts `SubtitleCompare.exe` in `%LOCALAPPDATA%\SubtitleCompare`, adds Start Menu + Desktop shortcuts, and registers a sign-in task that pulls a new exe whenever GitHub publishes one.

You still need ffmpeg:

```
winget install Gyan.FFmpeg
```

Manual download: https://github.com/arostad/subtitle-compare/releases/latest

A small Windows WPF app that lines up subtitle tracks from one MKV so you can compare wording the way Beyond Compare lines up files.

Drop an `.mkv` on the window. Three equal panes each pick a subtitle track. Cues are aligned by overlapping timestamps, scroll together, and differ at the word level (green = only in this track, yellow = wording changed, pink = this track has no cue at that moment).

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `ffmpeg` and `ffprobe` on PATH

```
winget install Gyan.FFmpeg
```

or `winget install ffmpeg`. Builds from https://www.gyan.dev/ffmpeg/builds/ work too — add the `bin` folder to PATH and restart the app.

## Build

From a Windows machine (Visual Studio or the SDK):

```
dotnet build
```

The WPF project (`net10.0-windows`) will not compile on Linux. The Core library and tests will.

Publish a runnable exe:

```
dotnet publish src/SubtitleCompare.App/SubtitleCompare.App.csproj -c Release -r win-x64 --self-contained false -o ./publish
```

## Run

Open `publish/SubtitleCompare.exe`. Drop an MKV (or use Open…). The first three text tracks fill the panes; change any dropdown to compare a different pair or trio.

Image subtitles (PGS, VobSub / DVD, DVB) are listed but cannot be compared as text.

## Tests

```
dotnet test tests/SubtitleCompare.Tests/SubtitleCompare.Tests.csproj
```
