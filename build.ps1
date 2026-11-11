$ErrorActionPreference = "Stop"

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$csc = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\Roslyn\csc.exe" | Select-Object -First 1
if (-not $csc) { throw "No C# compiler found. Install Visual Studio Build Tools with the MSBuild component." }

$unionMetadata = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\UnionMetadata"
$winmd = Get-ChildItem $unionMetadata -Directory | Where-Object { $_.Name -match '^\d+\.' } | Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName "Windows.winmd" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $winmd) { throw "No Windows SDK metadata found. Install a Windows 10 or 11 SDK." }

$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$references = @(
    "mscorlib.dll", "System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll",
    "System.Net.Http.dll", "System.Web.Extensions.dll", "System.Xaml.dll",
    "System.Runtime.dll", "System.Threading.Tasks.dll", "System.ObjectModel.dll",
    "System.Runtime.WindowsRuntime.dll", "System.Runtime.InteropServices.WindowsRuntime.dll",
    "WPF\PresentationCore.dll", "WPF\PresentationFramework.dll", "WPF\WindowsBase.dll"
) | ForEach-Object { "-reference:" + (Join-Path $framework $_) }
$references += "-reference:$winmd"

$out = Join-Path $PSScriptRoot "bin"
New-Item -ItemType Directory -Force $out | Out-Null
& $csc -nologo -noconfig -nostdlib -langversion:latest -optimize+ -platform:x64 -target:winexe -warnaserror `
    "-win32icon:$(Join-Path $PSScriptRoot 'assets\app.ico')" "-resource:$(Join-Path $PSScriptRoot 'assets\kanji-readings.gz'),kanji-readings.gz" "-out:$(Join-Path $out 'Stagehand.exe')" `
    @references (Get-ChildItem (Join-Path $PSScriptRoot "src") -Filter *.cs | ForEach-Object FullName)
if ($LASTEXITCODE) { throw "Build failed." }
Write-Output "Built $(Join-Path $out 'Stagehand.exe')"
