# Compare a translation .properties file against en.properties and report coverage issues.
#
# Reports (all non-fatal unless -Strict):
#   - MISSING keys: present in en.properties, absent in the translation
#   - EXTRA   keys: present in the translation, absent in en.properties (likely typo/renamed)
#   - PLACEHOLDER DRIFT: the {0},{1},... set differs between en value and translated value
#   - UNTRANSLATED: translated value is byte-identical to English (acceptable for many
#     short tokens like "OK" but worth listing)
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Tools\ValidateTranslation.ps1 -Language ru
#   powershell -ExecutionPolicy Bypass -File Tools\ValidateTranslation.ps1 -Language ru -Strict

param(
    [Parameter(Mandatory = $true)]
    [string]$Language,
    [switch]$Strict,
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
if ([string]::IsNullOrEmpty($Root)) { $Root = Split-Path -Parent $PSScriptRoot }

$enPath = Join-Path $Root "Strings\en.properties"
$xxPath = Join-Path $Root "Strings\$Language.properties"

if (-not (Test-Path $enPath)) { Write-Host "en.properties not found: $enPath" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $xxPath)) { Write-Host "$Language.properties not found: $xxPath" -ForegroundColor Red; exit 1 }

function Load-Table([string]$path) {
    $map = [ordered]@{}
    Get-Content -LiteralPath $path -Encoding UTF8 | ForEach-Object {
        $line = $_
        if ($line.Length -eq 0) { return }
        $c = $line[0]
        if ($c -eq '#' -or $c -eq '!') { return }
        $eq = $line.IndexOf('=')
        if ($eq -le 0) { return }
        $key = $line.Substring(0, $eq).Trim()
        $value = $line.Substring($eq + 1)
        if ($key.Length -gt 0) { $map[$key] = $value }
    }
    return $map
}

function Placeholders([string]$template) {
    $set = New-Object System.Collections.Generic.SortedSet[int]
    [regex]::Matches($template, '\{(\d+)(?:[,:][^}]*)?\}') |
        ForEach-Object { [void]$set.Add([int]$_.Groups[1].Value) }
    return ($set -join ',')
}

$en = Load-Table $enPath
$xx = Load-Table $xxPath

$missing      = @($en.Keys | Where-Object { -not $xx.Contains($_) })
$extra        = @($xx.Keys | Where-Object { -not $en.Contains($_) })
$drift        = @()
$untranslated = @()

foreach ($k in $en.Keys) {
    if (-not $xx.Contains($k)) { continue }
    $enPh = Placeholders $en[$k]
    $xxPh = Placeholders $xx[$k]
    if ($enPh -ne $xxPh) {
        $drift += "$k  en:[$enPh]  ${Language}:[$xxPh]"
    }
    if ($en[$k] -eq $xx[$k] -and $en[$k].Length -gt 3) {
        $untranslated += $k
    }
}

Write-Host "Translation coverage: $Language.properties vs en.properties" -ForegroundColor Cyan
Write-Host "  en:          $($en.Count) keys"
Write-Host "  ${Language}:`t$($xx.Count) keys"
Write-Host "  missing:     $($missing.Count)"   -ForegroundColor $(if ($missing.Count)     { 'Yellow' } else { 'Green' })
Write-Host "  extra:       $($extra.Count)"     -ForegroundColor $(if ($extra.Count)       { 'Yellow' } else { 'Green' })
Write-Host "  drift:       $($drift.Count)"     -ForegroundColor $(if ($drift.Count)       { 'Red' }    else { 'Green' })
Write-Host "  untranslated:$($untranslated.Count) (values > 3 chars unchanged from English)" -ForegroundColor DarkGray

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "MISSING keys (present in en.properties, absent in ${Language}.properties):" -ForegroundColor Yellow
    foreach ($k in ($missing | Sort-Object)) { Write-Host "  $k" }
}
if ($extra.Count -gt 0) {
    Write-Host ""
    Write-Host "EXTRA keys (likely typo/renamed -- not in en.properties):" -ForegroundColor Yellow
    foreach ($k in ($extra | Sort-Object)) { Write-Host "  $k" }
}
if ($drift.Count -gt 0) {
    Write-Host ""
    Write-Host "PLACEHOLDER DRIFT (must fix -- runtime Format will throw or misalign):" -ForegroundColor Red
    foreach ($d in $drift) { Write-Host "  $d" }
}
if ($untranslated.Count -gt 0 -and $VerbosePreference -ne 'SilentlyContinue') {
    Write-Host ""
    Write-Host "UNTRANSLATED (sample -- not necessarily wrong):" -ForegroundColor DarkGray
    foreach ($k in ($untranslated | Select-Object -First 20)) { Write-Host "  $k" }
    if ($untranslated.Count -gt 20) { Write-Host "  ... +$($untranslated.Count - 20) more" }
}

$hasFatal = $drift.Count -gt 0
if ($Strict -and ($missing.Count -gt 0 -or $extra.Count -gt 0)) { $hasFatal = $true }

if ($hasFatal) { exit 1 } else { exit 0 }
