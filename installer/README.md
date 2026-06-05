# Against the Storm Accessibility Installer

A small standalone Windows installer for the [ATS Accessibility mod](https://github.com/rashadnaqeeb/ats-accessibility-mod).
It auto-detects the game, installs/updates BepInEx + the mod in one accessible
window, backs up overwritten files, and can repair or uninstall.

Written in Rust with a native (wxWidgets, screen-reader friendly) GUI, mirroring
the design of [soc-access](https://github.com/Neurrone/soc-access)'s installer.

## What it does

- Detects the game (including the demo) on Steam, Epic, and GOG. Microsoft Store
  / Xbox (Game Pass) copies are detected on any drive and reported as unmoddable.
- Fetches the latest release from GitHub, downloads the mod zip, verifies its
  SHA256 (when GitHub supplies a digest), and extracts it with zip-slip
  protection.
- Tracks what it installed in `BepInEx/config/ATSAccessibility/install.json` so
  updates avoid clobbering user files and uninstall is reversible.
- Classifies the existing install and shows one primary button accordingly:
  Install (fresh), Repair (manual/unmanaged or damaged), or Update (managed).
  Reinstall forces a re-lay; Uninstall reverses a managed install.
- Copies itself into the game root as `ATSAccessibilityInstaller.exe` so the mod
  can relaunch it for in-place updates.
- Requests administrator rights (manifest) so writes into the game folder
  succeed regardless of where the game is installed.
- Localized UI: auto-detects language from `--lang` (passed by the mod) or the OS
  locale, with a switcher. Language codes match the game's loca codes.

## Usage

- GUI (default): run with no arguments.
- Text mode: `--cli`.
- Update mode (used by the mod): `--update --game-dir "<path>" --lang <code>`.

## Building

Requires the [Rust toolchain](https://rustup.rs) and libclang (Visual Studio C++
tools or a standalone LLVM install) for wxdragon's bindgen.

```powershell
# from the repo root
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```

Output: `releases/ATSAccessibilityInstaller.exe`.

Run the tests with `cargo test` inside `installer/`.

## Adding a UI language

Drop a `<code>.properties` file in `src/i18n/` (same key=value format as
`en.properties`), then register it in `TABLES` and `DISPLAY_NAMES` in
`src/i18n.rs`. Missing keys fall back to English.
