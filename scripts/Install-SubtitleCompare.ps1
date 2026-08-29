# Installs SubtitleCompare.exe to %LOCALAPPDATA%\SubtitleCompare
# and creates Desktop + Start Menu shortcuts.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Add-Type -AssemblyName System.Net.Http
$ReleaseBase = "https://github.com/arostad/SubtitleCompare/releases/download/latest"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
$TrustedHosts = @("github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com")
$DownloadHandler = [System.Net.Http.HttpClientHandler]::new()
$DownloadHandler.AllowAutoRedirect = $false
$DownloadClient = [System.Net.Http.HttpClient]::new($DownloadHandler)
$DownloadClient.Timeout = [TimeSpan]::FromMinutes(15)

function Get-TrustedResponse([Uri]$Uri) {
    for ($redirects = 0; $redirects -le 5; $redirects++) {
        if ($Uri.Scheme -ne "https" -or $TrustedHosts -notcontains $Uri.DnsSafeHost -or $Uri.UserInfo) {
            throw "Download redirected to an untrusted location."
        }
        $response = $DownloadClient.GetAsync($Uri, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if ([int]$response.StatusCode -notin @(301, 302, 303, 307, 308)) {
            return $response
        }
        if ($redirects -eq 5 -or -not $response.Headers.Location) {
            $response.Dispose()
            throw "Download used an invalid redirect."
        }
        $next = $response.Headers.Location
        $response.Dispose()
        $Uri = if ($next.IsAbsoluteUri) { $next } else { [Uri]::new($Uri, $next) }
    }
}

function Get-ReleaseChecksum {
    $response = Get-TrustedResponse ([Uri]"$ReleaseBase/SubtitleCompare.exe.sha256")
    try {
        $response.EnsureSuccessStatusCode()
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim()
        if ($text -notmatch '^(?<hash>[0-9a-fA-F]{64})(?:\s+[* ]?SubtitleCompare\.exe)?$') {
            throw "Release checksum was not readable."
        }
        return $Matches.hash.ToUpperInvariant()
    } finally {
        $response.Dispose()
    }
}

function Save-TrustedDownload([Uri]$Uri, [string]$Path) {
    $response = Get-TrustedResponse $Uri
    try {
        $response.EnsureSuccessStatusCode()
        if ($response.Content.Headers.ContentLength -gt 1GB) {
            throw "Download was unexpectedly large."
        }
        $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $output = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $buffer = [byte[]]::new(81920)
            [long]$total = 0
            while (($read = $input.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $total += $read
                if ($total -gt 1GB) { throw "Download was unexpectedly large." }
                $output.Write($buffer, 0, $read)
            }
        } finally {
            $output.Dispose()
            $input.Dispose()
        }
    } finally {
        $response.Dispose()
    }
}

Write-Host "Installing Subtitle Compare..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$PendingPath = Join-Path $InstallDir "SubtitleCompare.exe.$([Guid]::NewGuid().ToString('N')).new"
Write-Host "Downloading SubtitleCompare.exe from the Latest release..."
$ExpectedHash = Get-ReleaseChecksum
try {
    Save-TrustedDownload ([Uri]"$ReleaseBase/SubtitleCompare.exe") $PendingPath
    if ((Get-FileHash -LiteralPath $PendingPath -Algorithm SHA256).Hash -ne $ExpectedHash) {
        throw "The downloaded executable failed its integrity check."
    }
} catch {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
    throw
}

if (-not (Test-Path $PendingPath) -or (Get-Item $PendingPath).Length -lt 1MB) {
    throw "Download failed. SubtitleCompare.exe is missing or too small: $PendingPath"
}

if (Get-Process -Name SubtitleCompare -ErrorAction SilentlyContinue) {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
    throw "Subtitle Compare is running. Close it before installing."
}
Move-Item -LiteralPath $PendingPath -Destination $ExePath -Force

if (-not (Test-Path $ExePath) -or (Get-Item $ExePath).Length -lt 1MB) {
    throw "Install failed. SubtitleCompare.exe is missing or too small in $InstallDir"
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
