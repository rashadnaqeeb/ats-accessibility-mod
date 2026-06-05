# Build the Rust installer (ATSAccessibilityInstaller.exe).
#
# Requires the Rust toolchain (https://rustup.rs) and libclang (shipped with
# Visual Studio's C++ tools or a standalone LLVM install) for wxdragon's bindgen.
#
#   powershell -ExecutionPolicy Bypass -File build-installer.ps1
param(
    [string]$Configuration = "release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallerDir = Join-Path $ScriptDir "installer"
$ReleasesDir = Join-Path $ScriptDir "releases"
$OutputName = "ATSAccessibilityInstaller.exe"

if (-not (Test-Path (Join-Path $InstallerDir "Cargo.toml"))) {
    Write-Host "Installer project not found at $InstallerDir" -ForegroundColor Red
    exit 1
}

# Ensure cargo is on PATH (rustup installs it under ~/.cargo/bin).
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    $cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
    if (Test-Path (Join-Path $cargoBin "cargo.exe")) {
        $env:PATH = "$cargoBin;$env:PATH"
        Write-Host "Added to PATH: $cargoBin" -ForegroundColor Cyan
    } else {
        Write-Host "cargo not found. Install Rust from https://rustup.rs" -ForegroundColor Red
        exit 1
    }
}

# wxdragon-sys builds wxWidgets from source via CMake + Ninja. When not running
# from a Visual Studio Developer shell, Ninja (and the VS-bundled CMake) aren't
# on PATH. Discover them via vswhere and prepend them.
$vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
if ((-not (Get-Command ninja -ErrorAction SilentlyContinue)) -and (Test-Path $vswhere)) {
    $vsPath = & $vswhere -latest -products * -property installationPath 2>$null | Select-Object -First 1
    if ($vsPath) {
        $cmakeExt = Join-Path $vsPath "Common7\IDE\CommonExtensions\Microsoft\CMake"
        $ninjaDir = Join-Path $cmakeExt "Ninja"
        $cmakeBin = Join-Path $cmakeExt "CMake\bin"
        foreach ($d in @($ninjaDir, $cmakeBin)) {
            if (Test-Path $d) {
                $env:PATH = "$d;$env:PATH"
                Write-Host "Added to PATH: $d" -ForegroundColor Cyan
            }
        }
    }
}
if (-not (Get-Command ninja -ErrorAction SilentlyContinue)) {
    Write-Host "WARNING: ninja not found. The wxWidgets build needs it (install via VS C++ tools or 'winget install Ninja-build.Ninja')." -ForegroundColor Yellow
}

# Locate libclang for bindgen if not already configured.
if (-not $env:LIBCLANG_PATH) {
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\LLVM\bin"
    )
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "libclang.dll")) {
            $env:LIBCLANG_PATH = $c
            Write-Host "Using LIBCLANG_PATH = $c" -ForegroundColor Cyan
            break
        }
    }
    if (-not $env:LIBCLANG_PATH) {
        Write-Host "WARNING: libclang.dll not found. Set LIBCLANG_PATH if the build fails." -ForegroundColor Yellow
    }
}

Write-Host "Building installer ($Configuration)..." -ForegroundColor Cyan
Push-Location $InstallerDir
try {
    if ($Configuration -eq "release") {
        cargo build --release
    } else {
        cargo build
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "cargo build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}

$BuiltExe = Join-Path $InstallerDir "target\$Configuration\$OutputName"
if (-not (Test-Path $BuiltExe)) {
    Write-Host "Build output not found at $BuiltExe" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
}
$DestExe = Join-Path $ReleasesDir $OutputName
Copy-Item -Path $BuiltExe -Destination $DestExe -Force

Write-Host "Installer built: $DestExe" -ForegroundColor Green
