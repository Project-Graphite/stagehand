param([switch]$Uninstall, [switch]$Startup)
$ErrorActionPreference = "Stop"

if ($Uninstall) {
    $installed = Join-Path $env:LOCALAPPDATA "Programs\Graphite Lyrics\GraphiteLyrics.exe"
    if (-not (Test-Path $installed)) { throw "Graphite Lyrics is not installed." }
    Start-Process $installed -ArgumentList "--uninstall", "--quiet" -Wait
    Write-Output "Removed Graphite Lyrics. Settings and the lyrics cache stay in $(Join-Path $env:LOCALAPPDATA 'GraphiteLyrics')."
    return
}

& (Join-Path $PSScriptRoot "build.ps1")
$arguments = @("--install") + $(if ($Startup) { @("--startup") } else { @() })
Start-Process (Join-Path $PSScriptRoot "bin\GraphiteLyrics.exe") -ArgumentList $arguments -Wait
Write-Output "Installed Graphite Lyrics. Open it from the Start menu."
