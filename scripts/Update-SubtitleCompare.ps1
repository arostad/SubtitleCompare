# Pulls the latest SubtitleCompare.exe from the public GitHub release.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
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
$DownloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("SubtitleCompare")
$DownloadClient.DefaultRequestHeaders.CacheControl = [System.Net.Http.Headers.CacheControlHeaderValue]::new()
$DownloadClient.DefaultRequestHeaders.CacheControl.NoCache = $true
$DownloadClient.DefaultRequestHeaders.CacheControl.NoStore = $true
$DownloadClient.DefaultRequestHeaders.Pragma.ParseAdd("no-cache")

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

function Get-ReleaseChecksum([Uri]$Uri) {
    $response = Get-TrustedResponse $Uri
    try {
        [void]$response.EnsureSuccessStatusCode()
        if ($response.Content.Headers.ContentLength -gt 1024) {
            throw "Release checksum was not readable."
        }
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim()
        if ($text.Length -gt 1024 -or $text -notmatch '^(?<hash>[0-9a-fA-F]{64})(?:\s+[* ]?SubtitleCompare\.exe)?$') {
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
        [void]$response.EnsureSuccessStatusCode()
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

function Save-VerifiedRelease([string]$Path) {
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        $verified = $false
        try {
            $nonce = [Guid]::NewGuid().ToString("N")
            $expectedHash = Get-ReleaseChecksum ([Uri]"$ReleaseBase/SubtitleCompare.exe.sha256?t=$nonce")
            Save-TrustedDownload ([Uri]"$ReleaseBase/SubtitleCompare.exe?t=$nonce") $Path
            if ((Get-Item -LiteralPath $Path).Length -ge 1MB -and
                (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $expectedHash) {
                $verified = $true
                return
            }
        } finally {
            if (-not $verified) {
                Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
            }
        }
    }
    throw "The downloaded executable failed its integrity check."
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

Write-Host "Downloading SubtitleCompare.exe..."
try {
    Save-VerifiedRelease $PendingPath

    if (Get-Process -Name "SubtitleCompare" -ErrorAction SilentlyContinue) {
        throw "Subtitle Compare is running. Close it before using this script, or use Update in the app."
    }
    Move-Item -LiteralPath $PendingPath -Destination $ExePath -Force
} finally {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
}
Write-Host "Updated: $ExePath"
