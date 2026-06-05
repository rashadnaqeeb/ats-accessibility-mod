use std::fs;
use std::path::Path;

use super::manifest::InstallManifest;
use super::paths;

pub fn uninstall(game_dir: &Path, manifest: &InstallManifest) -> Result<(), String> {
    for rel in manifest.installed_files.iter().rev() {
        let path = game_dir.join(rel);
        if path.exists() {
            fs::remove_file(&path)
                .map_err(|e| format!("Failed to remove {}: {e}", path.display()))?;
        }
        remove_empty_parents(game_dir, path.parent());
    }

    for (target_rel, backup_rel) in &manifest.backups {
        let backup = game_dir.join(backup_rel);
        if !backup.exists() {
            continue;
        }
        let target = game_dir.join(target_rel);
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent)
                .map_err(|e| format!("Failed to create restore parent: {e}"))?;
        }
        fs::copy(&backup, &target)
            .map_err(|e| format!("Failed to restore {}: {e}", target.display()))?;
        fs::remove_file(&backup)
            .map_err(|e| format!("Failed to remove backup {}: {e}", backup.display()))?;
        remove_empty_parents(game_dir, backup.parent());
    }

    // Remove the installer copy we dropped into the game root, unless it's the
    // exe currently running (the user may have launched it from there).
    let installer_copy = paths::installer_copy_path(game_dir);
    if installer_copy.exists() && !is_current_exe(&installer_copy) {
        let _ = fs::remove_file(&installer_copy);
    }

    let manifest_path = paths::manifest_path(game_dir);
    if manifest_path.exists() {
        fs::remove_file(&manifest_path).map_err(|e| format!("Failed to remove manifest: {e}"))?;
    }
    remove_empty_parents(game_dir, manifest_path.parent());
    Ok(())
}

fn is_current_exe(path: &Path) -> bool {
    match (std::env::current_exe(), fs::canonicalize(path)) {
        (Ok(cur), Ok(p)) => fs::canonicalize(cur).map(|c| c == p).unwrap_or(false),
        _ => false,
    }
}

fn remove_empty_parents(game_dir: &Path, mut current: Option<&Path>) {
    while let Some(dir) = current {
        if dir == game_dir {
            break;
        }
        match fs::remove_dir(dir) {
            Ok(_) => current = dir.parent(),
            Err(_) => break,
        }
    }
}
