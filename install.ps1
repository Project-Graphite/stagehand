param([switch]$Uninstall, [switch]$Startup)
$ErrorActionPreference = "Stop"

if ($Uninstall) {
    $installed = Join-Path $env:LOCALAPPDATA "Programs\Stagehand\Stagehand.exe"
    if (-not (Test-Path $installed)) { throw "Stagehand is not installed." }
    Start-Process $installed -ArgumentList "--uninstall", "--quiet" -Wait
    Write-Output "Removed Stagehand. Settings and the lyrics cache stay in $(Join-Path $env:LOCALAPPDATA 'Stagehand')."
    return
}

& (Join-Path $PSScriptRoot "build.ps1")
$arguments = @("--install") + $(if ($Startup) { @("--startup") } else { @() })
Start-Process (Join-Path $PSScriptRoot "bin\Stagehand.exe") -ArgumentList $arguments -Wait
Write-Output "Installed Stagehand. Open it from the Start menu."
