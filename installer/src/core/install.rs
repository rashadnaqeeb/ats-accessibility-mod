use std::collections::{HashMap, HashSet};
use std::fs;
use std::io::{Read, Seek};
use std::path::{Component, Path, PathBuf};

use semver::Version;
use sha2::{Digest, Sha256};
use time::OffsetDateTime;
use time::format_description::well_known::Rfc3339;
use zip::ZipArchive;

use super::bepinex_config;
use super::detect::{GameInstall, GameSource};
use super::github::{self, Asset};
use super::manifest::{InstallManifest, ManifestRead, SUPPORTED_SCHEMA};
use super::paths;
use super::selfcopy;

#[derive(Debug, Clone)]
pub enum InstallState {
    /// Nothing of ours is present.
    Fresh,
    /// A valid installer manifest exists; we track this install.
    Managed(InstallManifest),
    /// Mod/loader files exist but there's no manifest (e.g. a manual install
    /// from before the installer existed). "Repair" adopts it.
    Unmanaged,
    /// A manifest exists but is unreadable/invalid, with files present.
    DamagedState(String),
}

pub fn classify_install(game_dir: &Path) -> InstallState {
    match InstallManifest::read(game_dir) {
        ManifestRead::Valid(manifest) => InstallState::Managed(manifest),
        ManifestRead::Missing => {
            if has_installed_mod_files(game_dir) {
                InstallState::Unmanaged
            } else {
                InstallState::Fresh
            }
        }
        ManifestRead::Invalid(reason) => {
            if has_installed_mod_files(game_dir) {
                InstallState::DamagedState(reason)
            } else {
                InstallState::Fresh
            }
        }
    }
}

pub fn has_installed_mod_files(game_dir: &Path) -> bool {
    // Key on OUR plugin DLL specifically, not BepInEx in general, so a BepInEx
    // setup for other mods isn't misclassified as an unmanaged install of ours.
    game_dir.join(paths::PLUGIN_REL).exists()
}

/// Whether the installed (managed) version is older than the latest release
/// asset. Single source of truth for the "is an update available" decision
/// used by the GUI button state. Non-managed states are never "update
/// available" (they're Install/Repair, not Update).
pub fn update_available(state: &InstallState, asset: Option<&Asset>) -> bool {
    let Some(asset) = asset else {
        return false;
    };
    match (
        installed_version(state),
        asset.version().and_then(|v| Version::parse(&v).ok()),
    ) {
        (Some(installed), Some(latest)) => installed < latest,
        _ => false,
    }
}

pub fn installed_version(state: &InstallState) -> Option<Version> {
    match state {
        InstallState::Managed(manifest) => Version::parse(&manifest.mod_version).ok(),
        _ => None,
    }
}

pub fn verify_sha256(path: &Path, expected: &str) -> Result<(), String> {
    let actual = sha256_file(path)?;
    if !actual.eq_ignore_ascii_case(expected) {
        return Err(format!(
            "Downloaded zip digest mismatch. Expected {expected}, got {actual}."
        ));
    }
    Ok(())
}

pub fn sha256_file(path: &Path) -> Result<String, String> {
    let mut file =
        fs::File::open(path).map_err(|e| format!("Failed to open file for hashing: {e}"))?;
    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 81920];
    loop {
        let read = file
            .read(&mut buffer)
            .map_err(|e| format!("Failed to read file for hashing: {e}"))?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    let digest = hasher.finalize();
    Ok(digest.iter().map(|b| format!("{b:02x}")).collect())
}

pub fn install_from_zip(
    zip_path: &Path,
    game_dir: &Path,
    source: &GameSource,
    asset: &Asset,
    prior_state: &InstallState,
) -> Result<InstallManifest, String> {
    let version = asset
        .version()
        .ok_or_else(|| format!("Release asset name is not a mod zip: {}", asset.name))?;
    let prior_manifest = match prior_state {
        InstallState::Managed(manifest) => Some(manifest),
        _ => None,
    };
    let mut backups = prior_manifest
        .map(|m| m.backups.clone())
        .unwrap_or_else(HashMap::new);
    let prior_owned: HashSet<String> = prior_manifest
        .map(|m| m.installed_files.iter().cloned().collect())
        .unwrap_or_default();
    let backup_stamp = backup_stamp()?;
    let mut installed_files = Vec::new();
    let bepinex_config_existed_before = paths::bepinex_config_path(game_dir).exists();

    // When adopting an existing install of OUR mod that we don't yet track
    // (Unmanaged manual install, or Damaged manifest), the files we're about to
    // overwrite ARE the mod, not user data. Don't back them up — otherwise a
    // later uninstall would restore them and leave the mod in place. A genuine
    // Fresh install still backs up any foreign file it overwrites.
    let adopting = matches!(
        prior_state,
        InstallState::Unmanaged | InstallState::DamagedState(_)
    );

    let file = fs::File::open(zip_path).map_err(|e| format!("Failed to open zip: {e}"))?;
    let mut archive = ZipArchive::new(file).map_err(|e| format!("Failed to read zip: {e}"))?;
    extract_archive(
        &mut archive,
        game_dir,
        &prior_owned,
        adopting,
        &mut backups,
        &backup_stamp,
        &mut installed_files,
    )?;

    ensure_bepinex_config(
        game_dir,
        &mut backups,
        &backup_stamp,
        bepinex_config_existed_before,
    )?;

    // Remove files we installed previously that are absent from the new release,
    // so a later uninstall stays complete and stale (e.g. renamed) DLLs don't
    // linger and get loaded by BepInEx.
    if let Some(prior) = prior_manifest {
        let new_set: HashSet<&String> = installed_files.iter().collect();
        for old in &prior.installed_files {
            // Never delete the shared BepInEx config, even if an older manifest
            // recorded it as owned.
            if old.eq_ignore_ascii_case(paths::BEPINEX_CONFIG_REL) {
                continue;
            }
            if !new_set.contains(old) {
                let stale = game_dir.join(old);
                if stale.exists() {
                    let _ = fs::remove_file(&stale);
                }
            }
        }
    }

    let sha256 = asset.sha256_digest().or_else(|| sha256_file(zip_path).ok());
    let manifest = InstallManifest {
        schema_version: SUPPORTED_SCHEMA,
        mod_version: version,
        installed_at: OffsetDateTime::now_utc()
            .format(&Rfc3339)
            .unwrap_or_else(|_| "unknown".to_string()),
        source: source.as_manifest_str().to_string(),
        release_asset: asset.name.clone(),
        sha256,
        installed_files,
        backups,
        // Preserve a previously chosen language across updates; otherwise record
        // the currently active one (the --lang the mod passed, or the user's pick).
        language: prior_manifest
            .and_then(|m| m.language.clone())
            .or_else(|| Some(crate::i18n::active_language())),
    };
    manifest.write(game_dir)?;

    // Best-effort: drop a copy of ourselves into the game root so the mod can
    // relaunch us for future updates. Failure here doesn't fail the install —
    // the mod falls back to opening the download page.
    let _ = selfcopy::copy_into_game_dir(game_dir);

    Ok(manifest)
}

fn extract_archive<R: Read + Seek>(
    archive: &mut ZipArchive<R>,
    game_dir: &Path,
    prior_owned: &HashSet<String>,
    adopting: bool,
    backups: &mut HashMap<String, String>,
    backup_stamp: &str,
    installed_files: &mut Vec<String>,
) -> Result<(), String> {
    for i in 0..archive.len() {
        let mut entry = archive
            .by_index(i)
            .map_err(|e| format!("Failed to read zip entry: {e}"))?;
        let raw_name = entry.name().to_string();
        let Some(rel) = safe_zip_entry_name(&raw_name) else {
            return Err(format!("Unsafe zip entry path: {raw_name}"));
        };
        if rel.as_os_str().is_empty() {
            continue;
        }
        let dest = game_dir.join(&rel);
        if entry.is_dir() {
            fs::create_dir_all(&dest)
                .map_err(|e| format!("Failed to create directory {}: {e}", dest.display()))?;
            continue;
        }

        let rel_key = paths::normalize_rel(rel.to_string_lossy().as_ref());
        let is_bepinex_config = rel_key.eq_ignore_ascii_case(paths::BEPINEX_CONFIG_REL);
        // Never clobber an existing BepInEx config — it may hold other mods'
        // settings. We only ensure our one required key (see ensure_bepinex_config).
        if is_bepinex_config && dest.exists() {
            continue;
        }

        // Back up any pre-existing file we're about to overwrite that we don't
        // already own and haven't already backed up (protects user/other-mod
        // files). Skipped when adopting an untracked install of our own mod —
        // those overwritten files are ours, and backing them up would make
        // uninstall restore them.
        if !adopting
            && dest.exists()
            && !prior_owned.contains(&rel_key)
            && !backups.contains_key(&rel_key)
        {
            backup_file(game_dir, &rel_key, backups, backup_stamp)?;
        }

        if let Some(parent) = dest.parent() {
            fs::create_dir_all(parent)
                .map_err(|e| format!("Failed to create parent directory: {e}"))?;
        }
        let mut output = fs::File::create(&dest)
            .map_err(|e| format!("Failed to create {}: {e}", dest.display()))?;
        std::io::copy(&mut entry, &mut output)
            .map_err(|e| format!("Failed to write {}: {e}", dest.display()))?;
        // Never record the BepInEx config as an owned file: it's shared with
        // other mods, so uninstall and orphan cleanup must never remove it.
        if !is_bepinex_config {
            installed_files.push(rel_key);
        }
    }
    Ok(())
}

fn ensure_bepinex_config(
    game_dir: &Path,
    backups: &mut HashMap<String, String>,
    backup_stamp: &str,
    existed_before_install: bool,
) -> Result<(), String> {
    let config_path = paths::bepinex_config_path(game_dir);
    if !config_path.exists() {
        return Ok(());
    }
    let key = paths::normalize_rel(paths::BEPINEX_CONFIG_REL);
    if existed_before_install && !backups.contains_key(&key) {
        backup_file(game_dir, &key, backups, backup_stamp)?;
    }
    bepinex_config::ensure_hide_manager_game_object(&config_path)?;
    Ok(())
}

fn backup_file(
    game_dir: &Path,
    rel_key: &str,
    backups: &mut HashMap<String, String>,
    backup_stamp: &str,
) -> Result<(), String> {
    let src = game_dir.join(rel_key);
    if !src.exists() {
        return Ok(());
    }
    let backup_rel = paths::normalize_rel(
        Path::new(paths::BACKUPS_REL)
            .join(backup_stamp)
            .join(rel_key)
            .to_string_lossy()
            .as_ref(),
    );
    let backup_abs = game_dir.join(&backup_rel);
    if let Some(parent) = backup_abs.parent() {
        fs::create_dir_all(parent).map_err(|e| format!("Failed to create backup directory: {e}"))?;
    }
    fs::copy(&src, &backup_abs).map_err(|e| format!("Failed to back up {}: {e}", src.display()))?;
    backups.insert(rel_key.to_string(), backup_rel);
    Ok(())
}

fn backup_stamp() -> Result<String, String> {
    let now = OffsetDateTime::now_utc()
        .format(&Rfc3339)
        .map_err(|e| format!("Failed to format backup timestamp: {e}"))?;
    Ok(now
        .replace(':', "")
        .replace('-', "")
        .replace('T', "_")
        .replace('Z', "Z"))
}

pub fn temp_session_dir() -> PathBuf {
    let nanos = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    std::env::temp_dir()
        .join("ATSAccessibilityInstaller")
        .join(format!("{}-{nanos}", std::process::id()))
}

pub fn safe_zip_entry_name(name: &str) -> Option<PathBuf> {
    let normalized = name.replace('\\', "/");
    let path = Path::new(&normalized);
    let mut out = PathBuf::new();
    for component in path.components() {
        match component {
            Component::Normal(part) => out.push(part),
            Component::CurDir => {}
            Component::ParentDir | Component::RootDir | Component::Prefix(_) => return None,
        }
    }
    Some(out)
}

/// Progress steps reported by [`perform_install`] so each front-end can render
/// them in its own way (localized log line, console print) without duplicating
/// the install sequence.
pub enum InstallProgress {
    Downloading(String),
    Verifying,
    Installing,
}

pub enum InstallOutcome {
    Installed,
    AlreadyUpToDate,
}

/// The full download-verify-install sequence, shared by the GUI and CLI.
/// Version-gates unless `force`. Does NOT close the game — call
/// [`super::process::kill_game_and_wait`] first. Cleans up its temp dir.
pub fn perform_install<F: FnMut(InstallProgress)>(
    game: &GameInstall,
    asset: &Asset,
    force: bool,
    mut progress: F,
) -> Result<InstallOutcome, String> {
    let state = classify_install(&game.path);
    if !force {
        if let (Some(installed), Some(latest)) = (
            installed_version(&state),
            asset.version().and_then(|v| Version::parse(&v).ok()),
        ) {
            if installed >= latest {
                return Ok(InstallOutcome::AlreadyUpToDate);
            }
        }
    }

    let temp_dir = temp_session_dir();
    let zip_path = temp_dir.join(&asset.name);
    let result = (|| {
        progress(InstallProgress::Downloading(asset.name.clone()));
        github::download_asset(asset, &zip_path)?;
        if let Some(expected) = asset.sha256_digest() {
            progress(InstallProgress::Verifying);
            verify_sha256(&zip_path, &expected)?;
        }
        progress(InstallProgress::Installing);
        install_from_zip(&zip_path, &game.path, &game.source, asset, &state)?;
        Ok::<(), String>(())
    })();
    let _ = std::fs::remove_dir_all(&temp_dir);
    result.map(|_| InstallOutcome::Installed)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    use crate::core::github::Asset;
    use crate::core::uninstall;

    #[test]
    fn rejects_zip_slip_paths() {
        assert!(safe_zip_entry_name("../../evil.dll").is_none());
        assert!(safe_zip_entry_name("C:/evil.dll").is_none());
        assert!(safe_zip_entry_name("/evil.dll").is_none());
        assert!(safe_zip_entry_name("BepInEx/plugins/mod.dll").is_some());
    }

    #[test]
    fn classifies_unmanaged_by_file_presence_only() {
        let dir = tempfile::tempdir().unwrap();
        let plugin = dir.path().join(paths::PLUGIN_REL);
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(plugin, "not a real dll").unwrap();
        assert!(matches!(classify_install(dir.path()), InstallState::Unmanaged));
    }

    #[test]
    fn invalid_manifest_with_files_is_damaged_state() {
        let dir = tempfile::tempdir().unwrap();
        let manifest = paths::manifest_path(dir.path());
        fs::create_dir_all(manifest.parent().unwrap()).unwrap();
        fs::write(&manifest, "{not json").unwrap();
        let plugin = dir.path().join(paths::PLUGIN_REL);
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(plugin, "").unwrap();
        assert!(matches!(
            classify_install(dir.path()),
            InstallState::DamagedState(_)
        ));
    }

    #[test]
    fn existing_bepinex_config_is_restored_and_backup_is_removed() {
        let dir = tempfile::tempdir().unwrap();
        let original_config = "HideManagerGameObject = false\nExistingSetting = original\n";
        let config_path = paths::bepinex_config_path(dir.path());
        fs::create_dir_all(config_path.parent().unwrap()).unwrap();
        fs::write(&config_path, original_config).unwrap();

        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[
                (
                    paths::BEPINEX_CONFIG_REL,
                    "HideManagerGameObject = false\nZipSetting = should-not-overwrite\n",
                ),
                (paths::PLUGIN_REL, "plugin"),
                ("winhttp.dll", "loader"),
            ],
        );

        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();

        let backup_rel = manifest
            .backups
            .get(paths::BEPINEX_CONFIG_REL)
            .expect("existing config should be backed up")
            .clone();
        let backup_path = dir.path().join(&backup_rel);
        assert!(backup_path.exists());
        assert!(
            fs::read_to_string(&config_path)
                .unwrap()
                .contains("HideManagerGameObject = true")
        );

        uninstall::uninstall(dir.path(), &manifest).unwrap();

        assert_eq!(fs::read_to_string(&config_path).unwrap(), original_config);
        assert!(!backup_path.exists());
    }

    #[test]
    fn repair_then_uninstall_fully_removes_an_adopted_install() {
        // Simulate a manual (unmanaged) install: our plugin + a loader file
        // present, no installer manifest.
        let dir = tempfile::tempdir().unwrap();
        let plugin = dir.path().join(paths::PLUGIN_REL);
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(&plugin, "old plugin").unwrap();
        fs::write(dir.path().join("winhttp.dll"), "old loader").unwrap();
        assert!(matches!(classify_install(dir.path()), InstallState::Unmanaged));

        // Repair: install over it from a zip shipping the same paths.
        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[(paths::PLUGIN_REL, "new plugin"), ("winhttp.dll", "new loader")],
        );
        let state = classify_install(dir.path());
        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &state,
        )
        .unwrap();

        assert!(matches!(classify_install(dir.path()), InstallState::Managed(_)));
        // Adoption must not back up the pre-existing mod files, or uninstall
        // would restore them.
        assert!(manifest.backups.is_empty());

        uninstall::uninstall(dir.path(), &manifest).unwrap();

        // Everything is gone -> Fresh, so the UI offers Install, not Repair.
        assert!(!plugin.exists());
        assert!(!dir.path().join("winhttp.dll").exists());
        assert!(matches!(classify_install(dir.path()), InstallState::Fresh));
    }

    #[test]
    fn bepinex_config_is_never_owned_or_orphan_deleted() {
        // A zip that ships a BepInEx.cfg (as a future release might), installed
        // fresh with no pre-existing config.
        let dir = tempfile::tempdir().unwrap();
        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[
                (paths::BEPINEX_CONFIG_REL, "HideManagerGameObject = false\n"),
                (paths::PLUGIN_REL, "plugin"),
                ("winhttp.dll", "loader"),
            ],
        );

        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();

        // The config exists but must NOT be recorded as an owned file.
        assert!(paths::bepinex_config_path(dir.path()).exists());
        assert!(
            !manifest
                .installed_files
                .iter()
                .any(|f| f.eq_ignore_ascii_case(paths::BEPINEX_CONFIG_REL))
        );

        // A subsequent update (config now present) must not delete it.
        install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Managed(manifest),
        )
        .unwrap();
        assert!(paths::bepinex_config_path(dir.path()).exists());
    }

    fn create_zip(path: &Path, entries: &[(&str, &str)]) {
        let file = fs::File::create(path).unwrap();
        let mut zip = zip::ZipWriter::new(file);
        let options = zip::write::SimpleFileOptions::default();
        for (name, content) in entries {
            zip.start_file(*name, options).unwrap();
            zip.write_all(content.as_bytes()).unwrap();
        }
        zip.finish().unwrap();
    }

    fn test_asset() -> Asset {
        Asset {
            name: "ATSAccessibility-v1.2.3-with-BepInEx.zip".to_string(),
            browser_download_url: "https://example.invalid/release.zip".to_string(),
            digest: None,
        }
    }
}
