# Build and deploy ATSAccessibility mod
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Against the Storm",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Validate localization strings before building (missing keys, arity mismatches).
Write-Host "Validating localization strings..." -ForegroundColor Cyan
& "$ScriptDir\ATSAccessibility\Tools\ValidateStrings.ps1" -Root "$ScriptDir\ATSAccessibility"
if ($LASTEXITCODE -ne 0) {
    Write-Host "String validation failed" -ForegroundColor Red
    exit $LASTEXITCODE
}

# Build the project
Write-Host "Building ATSAccessibility ($Configuration)..." -ForegroundColor Cyan
dotnet build "$ScriptDir\ATSAccessibility\ATSAccessibility.csproj" -c $Configuration -p:GamePath="$GamePath"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "Build succeeded" -ForegroundColor Green

# Copy to game plugins folder
$SourceDll = "$ScriptDir\ATSAccessibility\bin\$Configuration\net472\ATSAccessibility.dll"
$DestFolder = "$GamePath\BepInEx\plugins\ATSAccessibility"

if (-not (Test-Path $DestFolder)) {
    Write-Host "Creating plugin folder: $DestFolder" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $DestFolder -Force | Out-Null
}

Write-Host "Copying to $DestFolder..." -ForegroundColor Cyan
Copy-Item -Path $SourceDll -Destination $DestFolder -Force

# Deploy Prism native library
$PrismSrc = "$ScriptDir\prism\native\win-x64\prism.dll"
if (Test-Path $PrismSrc) {
    Copy-Item -Path $PrismSrc -Destination $DestFolder -Force
    Write-Host "Deployed prism.dll" -ForegroundColor Green
} else {
    Write-Host "WARNING: prism.dll not found at $PrismSrc" -ForegroundColor Yellow
}

Write-Host "Deploy complete" -ForegroundColor Green
