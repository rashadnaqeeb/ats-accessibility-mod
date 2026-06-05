use std::path::{Path, PathBuf};

/// GitHub repository the mod (and this installer) are released from.
pub const GITHUB_API_URL: &str =
    "https://api.github.com/repos/rashadnaqeeb/ats-accessibility-mod/releases/latest";

/// Release asset naming for the mod zip. Matches the existing
/// `ATSAccessibility-vX.Y.Z-with-BepInEx.zip` convention so the regex in
/// `github::parse_mod_zip_version` can pull the version out of the filename.
pub const MOD_ZIP_PREFIX: &str = "ATSAccessibility-v";
pub const MOD_ZIP_SUFFIX: &str = "-with-BepInEx.zip";

/// Game-directory validation prefix. A valid install has an executable and a
/// Unity "<name>_Data/Managed/Assembly-CSharp.dll" whose names start with this.
/// The prefix matches both the full game ("Against the Storm.exe" /
/// "Against the Storm_Data") and the demo ("Against the Storm Demo.exe" /
/// "Against the Storm Demo_Data").
pub const GAME_NAME_PREFIX: &str = "Against the Storm";

/// The mod plugin DLL — presence of this (not just BepInEx) is what marks an
/// existing install of *our* mod, so a BepInEx setup for other mods isn't
/// misclassified as an unmanaged ATSAccessibility install.
pub const PLUGIN_REL: &str = "BepInEx/plugins/ATSAccessibility/ATSAccessibility.dll";

/// Installer-owned bookkeeping, kept under the mod's own config folder.
pub const MANIFEST_REL: &str = "BepInEx/config/ATSAccessibility/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/ATSAccessibility/backups";
pub const BEPINEX_CONFIG_REL: &str = "BepInEx/config/BepInEx.cfg";

/// Name of the installer copy dropped into the game root so the mod can launch
/// it for in-place updates.
pub const INSTALLER_EXE_NAME: &str = "ATSAccessibilityInstaller.exe";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn bepinex_config_path(game_dir: &Path) -> PathBuf {
    game_dir.join(BEPINEX_CONFIG_REL)
}

pub fn installer_copy_path(game_dir: &Path) -> PathBuf {
    game_dir.join(INSTALLER_EXE_NAME)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}
