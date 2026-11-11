param(
    [Parameter(Mandatory)][string]$JMdict,
    [string]$Out
)
$ErrorActionPreference = "Stop"
if (-not $Out) { $Out = Join-Path (Split-Path $PSScriptRoot) "assets\kanji-readings.gz" }
Add-Type -AssemblyName System.Xml.Linq

$tables = @{
    "" = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    "~" = New-Object 'System.Collections.Generic.Dictionary[string,string]'
    "^" = New-Object 'System.Collections.Generic.Dictionary[string,string]'
}
$ranks = New-Object 'System.Collections.Generic.Dictionary[string,int]'

function Remember([string]$kind, [string]$key, [string]$reading, [int]$rank) {
    $id = $kind + $key
    if (-not $ranks.ContainsKey($id) -or $rank -lt $ranks[$id]) {
        $ranks[$id] = $rank
        $tables[$kind][$key] = $reading
    }
}

function Rank($priorities) {
    $best = 100
    foreach ($priority in $priorities) {
        $value = $priority.Value
        $rank = if ($value -match '^nf(\d+)$') { [int]$Matches[1] } elseif ($value -match '1$') { 49 } else { 60 }
        if ($rank -lt $best) { $best = $rank }
    }
    $best
}

$settings = New-Object System.Xml.XmlReaderSettings
$settings.DtdProcessing = [System.Xml.DtdProcessing]::Parse
$settings.MaxCharactersFromEntities = 0
$stream = New-Object System.IO.Compression.GZipStream([System.IO.File]::OpenRead($JMdict), [System.IO.Compression.CompressionMode]::Decompress)
$reader = [System.Xml.XmlReader]::Create($stream, $settings)
$han = [regex]("[" + [char]0x4E00 + "-" + [char]0x9FFF + [char]0x3005 + "]")
$trailingKana = [regex]("[" + [char]0x3041 + "-" + [char]0x3096 + "]$")
$phrase = [regex]("[" + [char]0x4E00 + "-" + [char]0x9FFF + "][" + [char]0x3041 + "-" + [char]0x3096 + "]+[" + [char]0x4E00 + "-" + [char]0x9FFF + "]")

while ($reader.ReadToFollowing("entry")) {
    $entry = [System.Xml.Linq.XElement]::ReadFrom($reader)
    $senses = @($entry.Elements("sense"))
    if (@($senses | Where-Object { -not ($_.Elements("misc") | Where-Object { $_.Value -match 'archaic' }) }).Count -eq 0) { continue }
    $partsOfSpeech = @($senses.Elements("pos") | ForEach-Object Value)
    if (@($partsOfSpeech | Where-Object { $_ -notmatch '^(expressions|interjection)' }).Count -eq 0) { continue }
    $isExpression = @($partsOfSpeech | Where-Object { $_ -match '^expressions' }).Count -gt 0
    $kind = if (@($partsOfSpeech | Where-Object { $_ -match '\bverb' }).Count -gt 0) { "~" } elseif (@($partsOfSpeech | Where-Object { $_ -match 'adjective \(keiyoushi\)' }).Count -gt 0) { "^" } else { $null }
    $readings = @($entry.Elements("r_ele") | Where-Object { -not $_.Element("re_nokanji") })
    foreach ($kanji in $entry.Elements("k_ele")) {
        $spelling = $kanji.Element("keb").Value
        if (-not $han.IsMatch($spelling) -or $kanji.Element("ke_inf") -or ($isExpression -and $phrase.IsMatch($spelling))) { continue }
        $match = $readings | Where-Object { $restrictions = @($_.Elements("re_restr") | ForEach-Object Value); $restrictions.Count -eq 0 -or $restrictions -contains $spelling } | Select-Object -First 1
        if (-not $match) { continue }
        $reading = $match.Element("reb").Value
        $rank = Rank @($kanji.Elements("ke_pri"))
        $suffix = $trailingKana.Match($spelling).Value
        if ($rank -lt 100 -or -not $suffix) { Remember "" $spelling $reading $rank }
        if ($kind -and $rank -lt 100 -and $suffix -and $suffix.Length -lt $spelling.Length -and $reading.EndsWith($suffix) -and $reading.Length -gt $suffix.Length) {
            Remember $kind $spelling.Substring(0, $spelling.Length - 1) $reading.Substring(0, $reading.Length - 1) $rank
        }
    }
}
$reader.Dispose()

$lines = New-Object System.Text.StringBuilder
foreach ($kind in "", "~", "^") {
    foreach ($pair in $tables[$kind].GetEnumerator()) { [void]$lines.Append($kind).Append($pair.Key).Append("`t").Append($pair.Value).Append("`n") }
}
$bytes = [System.Text.Encoding]::UTF8.GetBytes($lines.ToString())
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$output = [System.IO.File]::Create($Out)
$gzip = New-Object System.IO.Compression.GZipStream($output, [System.IO.Compression.CompressionLevel]::Optimal)
$gzip.Write($bytes, 0, $bytes.Length)
$gzip.Dispose()
Write-Output ("{0} words, {1} verb stems, {2} adjective stems, {3:N1} MB -> {4}" -f $tables[""].Count, $tables["~"].Count, $tables["^"].Count, ((Get-Item $Out).Length / 1MB), $Out)
