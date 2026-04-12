# Verify that zero-arg Strings.Get("key") calls in the working tree resolve to
# byte-identical English output compared to the pre-migration baseline commit.
#
# How it works:
#   1. For each .cs file in scope, load the working-tree version.
#   2. Replace every `Strings.Get("key")` (zero-arg form only) with the literal
#      English value from en.properties, quoted.
#   3. Diff the resolved file against `git show <BaselineRef>:<path>`.
#
# If a file's only changes were literal→Strings.Get migrations, the diff is empty.
# Calls with runtime args cannot be mechanically compared (the pre-migration
# form was string.Format / concatenation / different syntax) -- those call sites
# are skipped and reported, so you can review them manually.
#
# Usage:
#   # Compare working tree against HEAD (verifies just the current session's work)
#   powershell -ExecutionPolicy Bypass -File Tools\VerifyAgainstBaseline.ps1
#
#   # Compare against the commit before the localization migration started
#   powershell -ExecutionPolicy Bypass -File Tools\VerifyAgainstBaseline.ps1 -BaselineRef 46b444a
#
# Exit code is 1 if any file has a non-empty semantic diff.

param(
    [string]$BaselineRef = "HEAD",
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

# Force UTF-8 on both Console.OutputEncoding and [Console]::OutputEncoding so git show
# output isn't silently re-decoded as cp1252 on Windows.
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# Keys renamed in this session. Baseline references the old name; current uses the new.
# Treat baseline lookups of the old name as if they hit the new name's current value.
$renames = @{
    'common.cycle_modifier_category'  = 'panel.embark.help.cycle_modifier_category'
    'panel.resource.help.alt_i'       = 'panel.settlement_resource.help.alt_i'
    'nav.bsn.help.pause_unpause'      = 'nav.building_section.help.pause_unpause'
}

$repoRoot = (& git -C $Root rev-parse --show-toplevel).Trim()
$PropertiesPath = Join-Path $Root "Strings\en.properties"

# ---------- Load en.properties ----------
$keys = @{}
Get-Content -LiteralPath $PropertiesPath -Encoding UTF8 | ForEach-Object {
    $line = $_
    if ($line.Length -eq 0) { return }
    $c = $line[0]
    if ($c -eq '#' -or $c -eq '!') { return }
    $eq = $line.IndexOf('=')
    if ($eq -le 0) { return }
    $key = $line.Substring(0, $eq).Trim()
    $value = $line.Substring($eq + 1)
    $keys[$key] = $value
}

# Unescape values the same way Utils/Strings.cs does.
function Unescape-Value([string]$s) {
    if ($s.IndexOf('\') -lt 0) { return $s }
    $sb = New-Object System.Text.StringBuilder $s.Length
    for ($i = 0; $i -lt $s.Length; $i++) {
        $c = $s[$i]
        if ($c -eq '\' -and ($i + 1) -lt $s.Length) {
            $next = $s[++$i]
            switch ($next) {
                'n' { [void]$sb.Append("`n") }
                't' { [void]$sb.Append("`t") }
                'r' { [void]$sb.Append("`r") }
                's' { [void]$sb.Append(' ') }
                '\' { [void]$sb.Append('\') }
                '=' { [void]$sb.Append('=') }
                default { [void]$sb.Append($next) }
            }
        } else {
            [void]$sb.Append($c)
        }
    }
    return $sb.ToString()
}

# C#-escape a string so it can be placed inside `"..."` in source.
function CSharp-Escape([string]$s) {
    $s = $s -replace '\\', '\\'
    $s = $s -replace '"', '\"'
    $s = $s -replace "`n", '\n'
    $s = $s -replace "`r", '\r'
    $s = $s -replace "`t", '\t'
    return $s
}

# ---------- Scan changed .cs files ----------
# Files excluded because they have intentional non-migration changes in this session
# (not literal->key rewrites, so byte-diff isn't meaningful).
$excluded = @(
    'ATSAccessibility/Utils/Strings.cs'  # doc comment update + new \s escape
)

# Known intentional diffs that aren't pure literal<->key rewrites. The verifier
# can't mechanically prove byte-equivalence for these (their templates have {N}
# placeholders that take runtime args). Each entry documents the rationale.
# If a diff for one of these files contains ONLY these lines, the file is
# considered OK. If any other line differs, it's still a failure.
$knownIntentionalDiffs = @{
    'ATSAccessibility/Handlers/MoveModeController.cs' = @(
        # Trailing-space fix: removed `+ " "`; the space now lives in the .properties
        # value `handler.movemode.cost_prefix` as a `\s` escape. Behaviourally identical.
        'costNote = Strings.Get("handler.movemode.cost_prefix", costInfo.Value.amount, costInfo.Value.displayName) + " ";'
        'costNote = Strings.Get("handler.movemode.cost_prefix", costInfo.Value.amount, costInfo.Value.displayName);'
    )
    'ATSAccessibility/Handlers/WorldMapNavigator.cs' = @(
        # Same trailing-space fix against `handler.worldmap.biome_prefix`.
        'var prefix = !string.IsNullOrEmpty(biome) ? Strings.Get("handler.worldmap.biome_prefix", biome) + " " : "";'
        'var prefix = !string.IsNullOrEmpty(biome) ? Strings.Get("handler.worldmap.biome_prefix", biome) : "";'
    )
}

$changedFiles = & git -C $repoRoot diff --name-only $BaselineRef -- 'ATSAccessibility/**/*.cs' |
    Where-Object { $_ -match '\.cs$' -and $_ -notmatch '/(obj|bin)/' -and ($excluded -notcontains $_) }

if (-not $changedFiles) {
    Write-Host "No .cs files changed vs $BaselineRef" -ForegroundColor Green
    exit 0
}

$anyFail = $false
$skippedArgCalls = New-Object System.Collections.Generic.List[string]

# Matches `Strings.Get("key")` with no extra args. Allows whitespace around parens.
$zeroArgRx = [regex]'Strings\.Get\s*\(\s*"((?:[^"\\]|\\.)*)"\s*\)'
# Matches `Strings.Get("key", ...)` -- we skip these (can't compare args syntactically).
$argRx = [regex]'Strings\.Get\s*\(\s*"((?:[^"\\]|\\.)*)"\s*,'

foreach ($relPath in $changedFiles) {
    $absPath = Join-Path $repoRoot $relPath
    if (-not (Test-Path $absPath)) {
        Write-Host "  [deleted] $relPath -- skipping" -ForegroundColor DarkGray
        continue
    }

    $current = Get-Content -LiteralPath $absPath -Raw -Encoding UTF8
    if ($null -eq $current) { $current = "" }

    # Record arg-form call sites that we can't verify mechanically.
    foreach ($m in $argRx.Matches($current)) {
        $line = ($current.Substring(0, $m.Index) -split "`n").Count
        $skippedArgCalls.Add("${relPath}:${line}  $($m.Value)")
    }

    $resolver = {
        param($m)
        $k = $m.Groups[1].Value
        if ($renames.ContainsKey($k)) { $k = $renames[$k] }
        if (-not $keys.ContainsKey($k)) { return $m.Value }
        $v = Unescape-Value $keys[$k]
        return '"' + (CSharp-Escape $v) + '"'
    }

    # Resolver for Strings.Get("key", ...args). Only rewrites when the template
    # has no {N} placeholders (so dropped args were silently ignored on both
    # sides, or the arity-fix removed a dropped arg). Uses a non-capturing
    # heuristic to find the closing ')'.
    $anyArgResolver = {
        param($m)
        $k = $m.Groups[1].Value
        if ($renames.ContainsKey($k)) { $k = $renames[$k] }
        if (-not $keys.ContainsKey($k)) { return $m.Value }
        $v = Unescape-Value $keys[$k]
        if ($v -match '\{\d+') { return $m.Value }   # has placeholders — can't compare
        return '"' + (CSharp-Escape $v) + '"'
    }
    # Match `Strings.Get("key" , ... )` with balanced outer parens (one level).
    $anyArgRx = [regex]'Strings\.Get\s*\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*[^()]*?\)'

    # Resolve current: first zero-arg, then any-arg-with-zero-placeholder-template.
    $resolvedCurrent = $zeroArgRx.Replace($current, $resolver)
    $resolvedCurrent = $anyArgRx.Replace($resolvedCurrent, $anyArgResolver)

    # Fetch baseline version as UTF-8 bytes (git show otherwise garbles non-ASCII on Windows).
    $baselineBytes = & git -C $repoRoot show "${BaselineRef}:${relPath}" 2>$null | Out-Null
    $tmpShow = [System.IO.Path]::GetTempFileName()
    try {
        & git -C $repoRoot show "${BaselineRef}:${relPath}" | Out-File -LiteralPath $tmpShow -Encoding UTF8
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  [new] $relPath -- no baseline version" -ForegroundColor DarkYellow
            continue
        }
        $baselineText = Get-Content -LiteralPath $tmpShow -Raw -Encoding UTF8
    } finally {
        Remove-Item $tmpShow -ErrorAction SilentlyContinue
    }
    if ($null -eq $baselineText) { $baselineText = "" }

    # Also resolve baseline: already-migrated call sites should produce the same literal
    # on both sides, so they cancel out and only this-session migrations show up as diffs.
    $resolvedBaseline = $zeroArgRx.Replace($baselineText, $resolver)
    $resolvedBaseline = $anyArgRx.Replace($resolvedBaseline, $anyArgResolver)

    # Normalize line endings + trailing whitespace/newline differences.
    $normCurrent  = ($resolvedCurrent  -replace "`r`n", "`n").TrimEnd()
    $normBaseline = ($resolvedBaseline -replace "`r`n", "`n").TrimEnd()

    # Apply known intentional-diff allowlist: if the only differing lines on
    # either side appear in the allowlist, treat the file as OK.
    if ($normCurrent -ne $normBaseline -and $knownIntentionalDiffs.ContainsKey($relPath)) {
        $allowed = $knownIntentionalDiffs[$relPath]
        $curLines = $normCurrent -split "`n"
        $baseLines = $normBaseline -split "`n"
        $curSet  = New-Object System.Collections.Generic.HashSet[string]
        foreach ($ln in $curLines)  { [void]$curSet.Add($ln.Trim()) }
        $baseSet = New-Object System.Collections.Generic.HashSet[string]
        foreach ($ln in $baseLines) { [void]$baseSet.Add($ln.Trim()) }
        $onlyInCur  = @($curLines  | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $baseSet.Contains($_) })
        $onlyInBase = @($baseLines | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $curSet.Contains($_)  })
        $unique = @($onlyInCur + $onlyInBase | Sort-Object -Unique)
        $allAllowed = $true
        foreach ($u in $unique) {
            if ($allowed -notcontains $u) { $allAllowed = $false; break }
        }
        if ($allAllowed -and $unique.Count -gt 0) {
            Write-Host "OK*  $relPath  (intentional: trailing-space fix)" -ForegroundColor DarkGreen
            continue
        }
    }

    if ($normCurrent -eq $normBaseline) {
        Write-Host "OK   $relPath" -ForegroundColor Green
        continue
    }

    # Diff: write both to temp and run git diff --no-index for readable output.
    $tmpResolved = [System.IO.Path]::GetTempFileName()
    $tmpBaseline = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllText($tmpBaseline, $normBaseline)
        [System.IO.File]::WriteAllText($tmpResolved, $normCurrent)
        Write-Host "DIFF $relPath" -ForegroundColor Yellow
        & git --no-pager diff --no-index --no-color -U1 $tmpBaseline $tmpResolved |
            Select-Object -Skip 4 |
            ForEach-Object { Write-Host "  $_" }
        $anyFail = $true
    } finally {
        Remove-Item $tmpResolved -ErrorAction SilentlyContinue
        Remove-Item $tmpBaseline -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "---- Skipped (Strings.Get with runtime args -- review manually): $($skippedArgCalls.Count) sites ----" -ForegroundColor Cyan
if ($skippedArgCalls.Count -gt 0 -and $skippedArgCalls.Count -le 40) {
    foreach ($s in $skippedArgCalls) { Write-Host "  $s" -ForegroundColor DarkGray }
} elseif ($skippedArgCalls.Count -gt 40) {
    Write-Host "  (list suppressed; $($skippedArgCalls.Count) entries -- too many to show)" -ForegroundColor DarkGray
}

if ($anyFail) {
    Write-Host ""
    Write-Host "FAIL: at least one file's resolved-inline form does not match baseline." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "PASS: all zero-arg Strings.Get call sites resolve to byte-identical baseline." -ForegroundColor Green
    exit 0
}
