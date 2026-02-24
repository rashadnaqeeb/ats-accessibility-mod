# validate-reflection.ps1
# Validates reflected types and members against decompiled game-source/ files.
# Usage: powershell -ExecutionPolicy Bypass -File validate-reflection.ps1

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$reflectionDir = Join-Path (Join-Path $scriptDir "ATSAccessibility") "Reflection"
$gameSourceDir = Join-Path $scriptDir "game-source"

if (-not (Test-Path $reflectionDir)) {
    Write-Host "ERROR: Reflection directory not found: $reflectionDir" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $gameSourceDir)) {
    Write-Host "ERROR: Game source directory not found: $gameSourceDir" -ForegroundColor Red
    exit 1
}

# --- Phase 1: Parse reflection files ---

Write-Host "Scanning ATSAccessibility/Reflection/*.cs..."

$reflectionFiles = Get-ChildItem -Path $reflectionDir -Filter "*.cs" |
    Where-Object { $_.Name -ne "ReflectionHelper.cs" -and $_.Name -ne "ReflectionValidator.cs" }

# Track: type name -> { File, Members: @( @{Name, Kind} ) }
$typeMap = [ordered]@{}
# Track: short name -> source file
$shortNameLookups = @()

foreach ($file in $reflectionFiles) {
    $lines = Get-Content -Path $file.FullName

    # Map variable names to fully-qualified type names
    $varToType = @{}

    foreach ($line in $lines) {
        # Track GetType assignments: var foo = assembly.GetType("X") or _field = assembly.GetType("X")
        if ($line -match '(?:var\s+)?(\w+)\s*=\s*(?:assembly|_gameAssembly)\.GetType\(\s*"([^"]+)"\s*\)') {
            $varName = $Matches[1]
            $typeName = $Matches[2]
            $varToType[$varName] = $typeName

            if (-not $typeMap.Contains($typeName)) {
                $typeMap[$typeName] = @{
                    File = $file.Name
                    Members = [System.Collections.Generic.List[hashtable]]::new()
                }
            }
        }

        # Track member lookups: varName.GetProperty("Name", ...) etc.
        if ($line -match '(\w+)\.Get(Property|Field|Method)\(\s*"([^"]+)"') {
            $varName = $Matches[1]
            $kind = $Matches[2]
            $memberName = $Matches[3]

            $ownerType = $varToType[$varName]
            if ($ownerType -and $typeMap.Contains($ownerType)) {
                $typeMap[$ownerType].Members.Add(@{ Name = $memberName; Kind = $kind })
            }
        }

        # Track FindTypeByName("X") calls
        if ($line -match 'FindTypeByName\(\s*"([^"]+)"\s*\)') {
            $shortName = $Matches[1]
            $shortNameLookups += @{
                Name = $shortName
                File = $file.Name
            }

            # Also track the variable assignment for member lookups
            if ($line -match '(\w+)\s*=\s*FindTypeByName') {
                $varName = $Matches[1]
                $placeholderType = "__FindTypeByName__$shortName"
                $varToType[$varName] = $placeholderType
                $typeMap[$placeholderType] = @{
                    File = $file.Name
                    Members = [System.Collections.Generic.List[hashtable]]::new()
                    ShortName = $shortName
                }
            }
        }
    }
}

$totalTypes = ($typeMap.Keys | Where-Object { -not $_.StartsWith("__FindTypeByName__") }).Count
$totalMembers = 0
foreach ($entry in $typeMap.Values) {
    $totalMembers += $entry.Members.Count
}

Write-Host "Parsed $($reflectionFiles.Count) files, found $totalTypes types, $totalMembers members"
Write-Host ""

# --- Phase 2: Build helpers ---

Write-Host "Checking against game-source/..."

$okTypes = 0
$missingTypes = 0
$okMembers = 0
$missingMembers = 0
$missingDetails = @()

# Cache: source file path -> content (avoid re-reading)
$sourceContentCache = @{}

function Get-SourceContent {
    param([string]$FilePath)
    if (-not $sourceContentCache.ContainsKey($FilePath)) {
        $sourceContentCache[$FilePath] = Get-Content -Path $FilePath -Raw
    }
    return $sourceContentCache[$FilePath]
}

# Resolve type name to source file path. First try namespace convention,
# then fall back to filename search. Skip non-game types (UniRx, etc.).
function Find-SourceFile {
    param([string]$TypeName)

    # Skip third-party types
    if (-not $TypeName.StartsWith("Eremite")) { return $null }

    $lastDot = $TypeName.LastIndexOf('.')
    if ($lastDot -lt 0) {
        $className = $TypeName
        $expectedPath = Join-Path $gameSourceDir "$className.cs"
    } else {
        $namespacePath = $TypeName.Substring(0, $lastDot)
        $className = $TypeName.Substring($lastDot + 1)
        $expectedPath = Join-Path (Join-Path $gameSourceDir $namespacePath) "$className.cs"
    }

    # Try exact namespace path first
    if (Test-Path $expectedPath) { return $expectedPath }

    # Fallback: search by filename across all game-source directories
    $found = Get-ChildItem -Path $gameSourceDir -Recurse -Filter "$className.cs" | Select-Object -First 1
    if ($found) { return $found.FullName }

    return $null
}

# Check if a member exists in source content
function Test-Member {
    param([string]$SourceContent, [string]$Name, [string]$Kind)

    $escaped = [regex]::Escape($Name)

    switch ($Kind) {
        "Property" {
            if ($SourceContent -match "(?m)\b$escaped\b\s*\{[^}]*get") { return $true }
            if ($SourceContent -match "(?m)\b$escaped\b\s*=>") { return $true }
        }
        "Field" {
            if ($SourceContent -match "(?m)\b$escaped\b\s*[;=]") { return $true }
        }
        "Method" {
            if ($SourceContent -match "(?m)\b$escaped\b\s*[\(<]") { return $true }
        }
    }
    return $false
}

# Extract base class/interface names from a class declaration line
function Get-BaseTypes {
    param([string]$SourceContent)

    $bases = @()
    # Match: class Foo : Bar, IBaz, IQux  or  interface IFoo : IBar
    if ($SourceContent -match '(?m)^\s*public\s+(?:abstract\s+)?(?:class|struct|interface)\s+\w+(?:<[^>]+>)?\s*:\s*(.+)$') {
        $baseList = $Matches[1] -replace '\{.*$', ''  # Remove anything after opening brace
        $baseList = $baseList.Trim()
        foreach ($b in $baseList -split ',') {
            $name = $b.Trim() -replace '<.*$', ''  # Remove generic params
            if ($name) { $bases += $name }
        }
    }
    return $bases
}

# Check a member against a type and its inheritance chain (up to 5 levels deep)
function Test-MemberWithInheritance {
    param([string]$StartFile, [string]$MemberName, [string]$MemberKind)

    $visited = @{}
    $queue = @($StartFile)
    $depth = 0

    while ($queue.Count -gt 0 -and $depth -lt 6) {
        $nextQueue = @()
        foreach ($file in $queue) {
            if ($visited.ContainsKey($file)) { continue }
            $visited[$file] = $true

            $content = Get-SourceContent $file
            if (Test-Member $content $MemberName $MemberKind) {
                return $true
            }

            # Find base types and queue them
            $bases = Get-BaseTypes $content
            foreach ($baseName in $bases) {
                $baseFile = Get-ChildItem -Path $gameSourceDir -Recurse -Filter "$baseName.cs" | Select-Object -First 1
                if ($baseFile -and -not $visited.ContainsKey($baseFile.FullName)) {
                    $nextQueue += $baseFile.FullName
                }
            }
        }
        $queue = $nextQueue
        $depth++
    }
    return $false
}

# --- Phase 3: Validate types and members ---

foreach ($typeName in $typeMap.Keys) {
    # Skip FindTypeByName placeholders (handled later)
    if ($typeName.StartsWith("__FindTypeByName__")) { continue }

    $entry = $typeMap[$typeName]

    # Skip third-party types
    if (-not $typeName.StartsWith("Eremite")) {
        if ($Verbose) {
            Write-Host "[SKIP] $typeName (third-party)" -ForegroundColor DarkGray
        }
        continue
    }

    $sourceFile = Find-SourceFile $typeName

    if (-not $sourceFile) {
        $missingTypes++
        $missingDetails += "[MISSING TYPE] $typeName (from $($entry.File))"
        Write-Host "[MISSING TYPE] $typeName" -ForegroundColor Red
        Write-Host "  Source: $($entry.File)" -ForegroundColor DarkGray
        $missingMembers += $entry.Members.Count
        continue
    }

    $sourceContent = Get-SourceContent $sourceFile
    $typeMissingMembers = @()

    foreach ($member in $entry.Members) {
        if (Test-Member $sourceContent $member.Name $member.Kind) {
            $okMembers++
        } elseif (Test-MemberWithInheritance $sourceFile $member.Name $member.Kind) {
            $okMembers++
        } else {
            $missingMembers++
            $typeMissingMembers += $member
        }
    }

    if ($typeMissingMembers.Count -eq 0) {
        $okTypes++
        if ($Verbose) {
            Write-Host "[OK] $typeName ($($entry.Members.Count) members)" -ForegroundColor Green
        }
    } else {
        $okTypes++
        $okCount = $entry.Members.Count - $typeMissingMembers.Count
        Write-Host "[PARTIAL] $typeName ($okCount/$($entry.Members.Count) members)" -ForegroundColor Yellow
        foreach ($mm in $typeMissingMembers) {
            $missingDetails += "[MISSING MEMBER] $typeName.$($mm.Name) ($($mm.Kind), from $($entry.File))"
            Write-Host "  Missing $($mm.Kind): $($mm.Name)" -ForegroundColor Red
        }
    }
}

# --- Phase 4: Validate FindTypeByName lookups + their members ---

foreach ($lookup in $shortNameLookups) {
    $shortName = $lookup.Name
    $found = Get-ChildItem -Path $gameSourceDir -Recurse -Filter "$shortName.cs" | Select-Object -First 1
    if ($found) {
        $placeholderKey = "__FindTypeByName__$shortName"
        if ($typeMap.Contains($placeholderKey) -and $typeMap[$placeholderKey].Members.Count -gt 0) {
            $entry = $typeMap[$placeholderKey]
            $typeMissingMembers = @()

            foreach ($member in $entry.Members) {
                if (Test-MemberWithInheritance $found.FullName $member.Name $member.Kind) {
                    $okMembers++
                } else {
                    $missingMembers++
                    $typeMissingMembers += $member
                }
            }

            if ($typeMissingMembers.Count -eq 0) {
                $okTypes++
                if ($Verbose) {
                    Write-Host "[OK] FindTypeByName(`"$shortName`") ($($entry.Members.Count) members)" -ForegroundColor Green
                }
            } else {
                $okTypes++
                $okCount = $entry.Members.Count - $typeMissingMembers.Count
                Write-Host "[PARTIAL] FindTypeByName(`"$shortName`") ($okCount/$($entry.Members.Count) members)" -ForegroundColor Yellow
                foreach ($mm in $typeMissingMembers) {
                    $missingDetails += "[MISSING MEMBER] $shortName.$($mm.Name) ($($mm.Kind), from $($entry.File))"
                    Write-Host "  Missing $($mm.Kind): $($mm.Name)" -ForegroundColor Red
                }
            }
        } else {
            $okTypes++
            if ($Verbose) {
                Write-Host "[OK] FindTypeByName(`"$shortName`")" -ForegroundColor Green
            }
        }
    } else {
        $missingDetails += "[MISSING TYPE] FindTypeByName(`"$shortName`") (from $($lookup.File))"
        Write-Host "[MISSING TYPE] FindTypeByName(`"$shortName`")" -ForegroundColor Red
        Write-Host "  Source: $($lookup.File)" -ForegroundColor DarkGray
        $missingTypes++
        $placeholderKey = "__FindTypeByName__$shortName"
        if ($typeMap.Contains($placeholderKey)) {
            $missingMembers += $typeMap[$placeholderKey].Members.Count
        }
    }
}

# --- Summary ---

Write-Host ""
$totalTypeChecks = $okTypes + $missingTypes
$totalMemberChecks = $okMembers + $missingMembers
$totalMissing = $missingTypes + $missingMembers

if ($totalMissing -eq 0) {
    Write-Host "Summary: $okTypes/$totalTypeChecks types OK, $okMembers/$totalMemberChecks members OK, 0 missing" -ForegroundColor Green
} else {
    Write-Host "Summary: $okTypes/$totalTypeChecks types OK, $okMembers/$totalMemberChecks members OK, $totalMissing missing" -ForegroundColor Red
    Write-Host ""
    Write-Host "Missing items:" -ForegroundColor Red
    foreach ($detail in $missingDetails) {
        Write-Host "  $detail" -ForegroundColor Red
    }
}

exit $totalMissing
