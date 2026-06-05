# Package a mod release: build, assemble release-package, zip, and emit a
# SHA256 sidecar so the installer can verify the download.
#
#   powershell -ExecutionPolicy Bypass -File build_release.ps1
#
# Produces, under releases/:
#   ATSAccessibility-v<version>-with-BepInEx.zip
#   ATSAccessibility-v<version>-with-BepInEx.zip.sha256
#
# This packages the mod only. Build the installer separately with
# build-installer.ps1, then upload both (zip, sidecar, installer exe) as release
# assets. The installer exe must keep the name ATSAccessibilityInstaller.exe so
# the releases/latest/download permalink in the mod resolves.
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Against the Storm"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Pkg = Join-Path $ScriptDir "release-package"
$ReleasesDir = Join-Path $ScriptDir "releases"

# --- Read the version from the single source of truth in Plugin.cs ---
$PluginCs = Join-Path $ScriptDir "ATSAccessibility\Core\Plugin.cs"
$VersionMatch = Select-String -Path $PluginCs -Pattern 'ModVersion\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"'
if (-not $VersionMatch) {
    Write-Host "Could not find ModVersion in $PluginCs" -ForegroundColor Red
    exit 1
}
$Version = $VersionMatch.Matches[0].Groups[1].Value
Write-Host "Packaging version $Version" -ForegroundColor Cyan

if (-not (Test-Path $Pkg)) {
    Write-Host "release-package not found at $Pkg" -ForegroundColor Red
    exit 1
}

# --- Build the mod (also validates strings) ---
& "$ScriptDir\build.ps1" -GamePath $GamePath -Configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Mod build failed" -ForegroundColor Red
    exit $LASTEXITCODE
}

# --- Refresh files inside release-package ---
$PluginDest = Join-Path $Pkg "BepInEx\plugins\ATSAccessibility"
Copy-Item (Join-Path $ScriptDir "ATSAccessibility\bin\Release\net472\ATSAccessibility.dll") $PluginDest -Force
Copy-Item (Join-Path $ScriptDir "prism\native\win-x64\prism.dll") $PluginDest -Force
Copy-Item (Join-Path $ScriptDir "README.md") $Pkg -Force
Copy-Item (Join-Path $ScriptDir "LICENSE") (Join-Path $Pkg "LICENSE.txt") -Force

# --- Zip it ---
if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
}
$ZipName = "ATSAccessibility-v$Version-with-BepInEx.zip"
$ZipPath = Join-Path $ReleasesDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $Pkg "*") -DestinationPath $ZipPath -Force

# --- SHA256 sidecar (lowercase hex + filename, the sha256sum convention) ---
$Hash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash.ToLower()
$SidecarPath = "$ZipPath.sha256"
"$Hash  $ZipName" | Set-Content -Path $SidecarPath -NoNewline -Encoding ascii

Write-Host "Release zip:    $ZipPath" -ForegroundColor Green
Write-Host "SHA256 sidecar: $SidecarPath" -ForegroundColor Green
Write-Host "SHA256:         $Hash" -ForegroundColor Green
