# Pulls the latest SubtitleCompare.exe from the public GitHub release.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Add-Type -AssemblyName System.Net.Http
$ReleaseBase = "https://github.com/arostad/SubtitleCompare/releases/download/latest"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
$PendingPath = Join-Path $InstallDir "SubtitleCompare.exe.$([Guid]::NewGuid().ToString('N')).new"
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

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$running = Get-Process -Name "SubtitleCompare" -ErrorAction SilentlyContinue
Write-Host "Downloading SubtitleCompare.exe..."
$ExpectedHash = Get-ReleaseChecksum
try {
    Save-TrustedDownload ([Uri]"$ReleaseBase/SubtitleCompare.exe") $PendingPath
    if ((Get-FileHash -LiteralPath $PendingPath -Algorithm SHA256).Hash -ne $ExpectedHash) {
        throw "The downloaded executable failed its integrity check."
    }
    if ((Get-Item -LiteralPath $PendingPath).Length -lt 1MB) {
        throw "Download failed. File is too small."
    }
} catch {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
    throw
}

if ($running) {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
    throw "Subtitle Compare is running. Close it before using this script, or use Update in the app."
} else {
    Move-Item -LiteralPath $PendingPath -Destination $ExePath -Force
    Write-Host "Updated: $ExePath"
}
