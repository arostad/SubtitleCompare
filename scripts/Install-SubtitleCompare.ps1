# Installs SubtitleCompare.exe to %LOCALAPPDATA%\SubtitleCompare
# and creates Desktop + Start Menu shortcuts.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.Net.Http
$ReleaseBase = "https://github.com/arostad/SubtitleCompare/releases/download/latest"
$InstallDir = Join-Path $env:LOCALAPPDATA "SubtitleCompare"
$ExePath = Join-Path $InstallDir "SubtitleCompare.exe"
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

Write-Host "Installing Subtitle Compare..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$PendingPath = Join-Path $InstallDir "SubtitleCompare.exe.$([Guid]::NewGuid().ToString('N')).new"
Write-Host "Downloading SubtitleCompare.exe from the Latest release..."
try {
    Save-VerifiedRelease $PendingPath

    if (Get-Process -Name SubtitleCompare -ErrorAction SilentlyContinue) {
        throw "Subtitle Compare is running. Close it before installing."
    }
    Move-Item -LiteralPath $PendingPath -Destination $ExePath -Force
} finally {
    Remove-Item -LiteralPath $PendingPath -Force -ErrorAction SilentlyContinue
}

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
