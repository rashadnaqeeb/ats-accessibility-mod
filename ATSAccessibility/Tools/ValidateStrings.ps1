# Validate Strings.Get / Strings.Plural call sites against the en.properties table.
#
# - Parses every .cs file under ATSAccessibility/ for `Strings.Get("literal", ...)` and
#   `Strings.Plural(count, "lit1", "lit2", ...)` call sites with literal first/second args.
# - Parses Strings/en.properties using the same rules as Utils/Strings.cs.
# - Reports:
#     * missing keys (error, exit 1)
#     * placeholder/arg arity mismatches (error, exit 1)
#     * orphan keys — defined but not referenced via a literal call site (warning only;
#       some keys are referenced dynamically, e.g. `"common." + direction`).
#
# Wired into build.ps1; runs before `dotnet build`.

param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$PropertiesPath = Join-Path $Root "Strings\en.properties"
if (-not (Test-Path $PropertiesPath)) {
    Write-Host "ValidateStrings: en.properties not found at $PropertiesPath" -ForegroundColor Red
    exit 1
}

# ---------- Parse en.properties ----------
$keys = @{}
$lineNo = 0
Get-Content -LiteralPath $PropertiesPath -Encoding UTF8 | ForEach-Object {
    $lineNo++
    $line = $_
    if ($line.Length -eq 0) { return }
    $c = $line[0]
    if ($c -eq '#' -or $c -eq '!') { return }
    $eq = $line.IndexOf('=')
    if ($eq -le 0) { return }
    $key = $line.Substring(0, $eq).Trim()
    $value = $line.Substring($eq + 1)
    if ($key.Length -eq 0) { return }
    $keys[$key] = $value
}

# Compute max placeholder index in a template. Returns -1 if no {N} found.
function Get-MaxPlaceholder([string]$template) {
    $max = -1
    $rx = [regex]'\{(\d+)(?:[,:][^}]*)?\}'
    foreach ($m in $rx.Matches($template)) {
        $n = [int]$m.Groups[1].Value
        if ($n -gt $max) { $max = $n }
    }
    return $max
}

# ---------- Scan .cs files ----------
$referenced = @{}
$errors = @()

$csFiles = Get-ChildItem -Path $Root -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

# Match Strings.Get("literal", ... )   — capture literal + remaining args up to closing paren.
# Use a balanced-ish approach: capture text up to the outermost close. We approximate by
# counting arg commas at depth 0 inside the call. Good enough for simple calls; string
# literals inside args don't contain unescaped parens or commas often, but we handle
# quoted strings and nested parens.
$getPattern  = [regex]'Strings\.Get\s*\(\s*"((?:[^"\\]|\\.)*)"\s*(,|\))'
$pluralPattern = [regex]'Strings\.Plural\s*\(\s*[^,]+,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*(,|\))'
$locaPattern = [regex]'HelpEntry\.Loca\s*\(\s*"(?:[^"\\]|\\.)*"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\)'

function Count-Args([string]$text, [int]$startIndex) {
    # Given the source text and the index of a ',' or ')' right after the literal,
    # count the remaining top-level args in the Strings.Get(...) call.
    if ($text[$startIndex] -eq ')') { return 0 }
    $depth = 1
    $i = $startIndex + 1
    $argCount = 1
    $inStr = $false
    $inChar = $false
    $escaped = $false
    while ($i -lt $text.Length -and $depth -gt 0) {
        $ch = $text[$i]
        if ($escaped) { $escaped = $false; $i++; continue }
        if ($inStr) {
            if ($ch -eq '\') { $escaped = $true }
            elseif ($ch -eq '"') { $inStr = $false }
        } elseif ($inChar) {
            if ($ch -eq '\') { $escaped = $true }
            elseif ($ch -eq "'") { $inChar = $false }
        } else {
            if ($ch -eq '"') { $inStr = $true }
            elseif ($ch -eq "'") { $inChar = $true }
            elseif ($ch -eq '(') { $depth++ }
            elseif ($ch -eq ')') { $depth-- }
            elseif ($ch -eq ',' -and $depth -eq 1) { $argCount++ }
        }
        $i++
    }
    return $argCount
}

foreach ($file in $csFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ($null -eq $text) { continue }

    foreach ($m in $getPattern.Matches($text)) {
        $key = $m.Groups[1].Value
        $sep = $m.Groups[2].Value
        $sepIdx = $m.Groups[2].Index
        $referenced[$key] = $true

        if (-not $keys.ContainsKey($key)) {
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $errors += "MISSING KEY: '$key' at $($file.FullName):$line"
            continue
        }

        $argCount = Count-Args $text $sepIdx
        $max = Get-MaxPlaceholder $keys[$key]
        $required = $max + 1
        if ($argCount -ne $required) {
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $errors += "ARITY: '$key' template expects $required args, call has $argCount at $($file.FullName):$line"
        }
    }

    foreach ($m in $locaPattern.Matches($text)) {
        $key = $m.Groups[1].Value
        $referenced[$key] = $true
        if (-not $keys.ContainsKey($key)) {
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $errors += "MISSING KEY (HelpEntry.Loca): '$key' at $($file.FullName):$line"
        }
    }

    foreach ($m in $pluralPattern.Matches($text)) {
        $k1 = $m.Groups[1].Value
        $k2 = $m.Groups[2].Value
        $referenced[$k1] = $true
        $referenced[$k2] = $true
        if (-not $keys.ContainsKey($k1)) {
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $errors += "MISSING KEY (Plural one): '$k1' at $($file.FullName):$line"
        }
        if (-not $keys.ContainsKey($k2)) {
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $errors += "MISSING KEY (Plural other): '$k2' at $($file.FullName):$line"
        }
    }
}

# ---------- Report ----------
if ($errors.Count -gt 0) {
    Write-Host "ValidateStrings: $($errors.Count) error(s):" -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  $e" -ForegroundColor Red }
    exit 1
}

$orphans = @($keys.Keys | Where-Object { -not $referenced.ContainsKey($_) })
if ($orphans.Count -gt 0) {
    Write-Host "ValidateStrings: $($orphans.Count) orphan key(s) (not referenced via literal Strings.Get):" -ForegroundColor Yellow
    foreach ($o in ($orphans | Sort-Object)) { Write-Host "  $o" -ForegroundColor Yellow }
    Write-Host "  (Some orphans are expected: keys consumed via concatenation like `"common.`" + dir.)" -ForegroundColor DarkGray
}

Write-Host "ValidateStrings: OK ($($keys.Count) keys, $($referenced.Count) referenced, $($orphans.Count) orphan)" -ForegroundColor Green
exit 0
