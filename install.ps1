param([switch]$Uninstall)
$ErrorActionPreference = "Stop"

$target = Join-Path $env:LOCALAPPDATA "Programs\Graphite Lyrics"
$menuShortcut = Join-Path ([Environment]::GetFolderPath("Programs")) "Graphite Lyrics.lnk"
$startupShortcut = Join-Path ([Environment]::GetFolderPath("Startup")) "Graphite Lyrics.lnk"
$exe = Join-Path $target "GraphiteLyrics.exe"

if (Test-Path $exe) {
    Get-Process -Name GraphiteLyrics -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | Stop-Process
}

if ($Uninstall) {
    Remove-Item $menuShortcut, $startupShortcut -ErrorAction SilentlyContinue
    if (Test-Path $target) { Remove-Item $target -Recurse }
    Write-Output "Removed Graphite Lyrics. Settings and the lyrics cache stay in $(Join-Path $env:LOCALAPPDATA 'GraphiteLyrics')."
    return
}

& (Join-Path $PSScriptRoot "build.ps1")
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item (Join-Path $PSScriptRoot "bin\GraphiteLyrics.exe") $exe -Force

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($menuShortcut)
$link.TargetPath = $exe
$link.WorkingDirectory = $target
$link.Description = "Synced lyrics for a Lian Li 9.2-inch case screen"
$link.Save()
if (Test-Path $startupShortcut) {
    $startup = $shell.CreateShortcut($startupShortcut)
    $startup.TargetPath = $exe
    $startup.Arguments = "--agent"
    $startup.WorkingDirectory = $target
    $startup.Save()
}
Write-Output "Installed to $target. Open Graphite Lyrics from the Start menu."
