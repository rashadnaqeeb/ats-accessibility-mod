use std::path::Path;

use super::paths;

/// Copy the running installer into the game root as
/// `ATSAccessibilityInstaller.exe` so the mod can relaunch it for future
/// in-place updates. No-op (Ok) when the running exe already *is* that copy,
/// which is the case when the mod launched us with `--update` — Windows locks a
/// running exe and copying onto itself would fail.
pub fn copy_into_game_dir(game_dir: &Path) -> Result<(), String> {
    let target = paths::installer_copy_path(game_dir);
    let current = std::env::current_exe()
        .map_err(|e| format!("Could not locate the running installer: {e}"))?;

    if same_file(&current, &target) {
        return Ok(());
    }

    if let Some(parent) = target.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| format!("Failed to prepare game directory: {e}"))?;
    }
    std::fs::copy(&current, &target)
        .map_err(|e| format!("Failed to copy installer into game folder: {e}"))?;
    Ok(())
}

fn same_file(a: &Path, b: &Path) -> bool {
    match (std::fs::canonicalize(a), std::fs::canonicalize(b)) {
        (Ok(ca), Ok(cb)) => ca == cb,
        _ => false,
    }
}
