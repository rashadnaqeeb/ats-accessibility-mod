use std::collections::HashSet;
use std::fs;
use std::path::{Path, PathBuf};

use regex::Regex;

use super::paths::GAME_NAME_PREFIX;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum GameSource {
    Steam,
    Epic,
    Gog,
    Manual,
}

impl GameSource {
    pub fn as_manifest_str(&self) -> &'static str {
        match self {
            GameSource::Steam => "steam",
            GameSource::Epic => "epic",
            GameSource::Gog => "gog",
            GameSource::Manual => "manual",
        }
    }
}

#[derive(Debug, Clone)]
pub struct GameInstall {
    pub path: PathBuf,
    pub source: GameSource,
}

/// Auto-detect a moddable Against the Storm install (Steam, Epic, or GOG).
/// Microsoft Store / Xbox copies are intentionally not returned here; see
/// [`detect_store_warning`].
pub fn detect_game() -> Option<GameInstall> {
    for candidate in game_candidates() {
        if validate_game_dir(&candidate.path) {
            return Some(candidate);
        }
    }
    None
}

pub fn validate_game_dir(path: &Path) -> bool {
    let entries = dir_entries(path);
    let prefix = GAME_NAME_PREFIX.to_lowercase();

    let has_exe = entries.iter().any(|n| {
        let l = n.to_lowercase();
        l.starts_with(&prefix) && l.ends_with(".exe")
    });
    if !has_exe {
        return false;
    }

    // The Unity managed assembly lives under "<name>_Data/Managed". Matching by
    // prefix accepts the full game ("Against the Storm_Data") and the demo
    // ("Against the Storm Demo_Data") without hardcoding the demo's exact names.
    entries.iter().any(|n| {
        let l = n.to_lowercase();
        l.starts_with(&prefix)
            && l.ends_with("_data")
            && path.join(n).join("Managed").join("Assembly-CSharp.dll").exists()
    })
}

fn dir_entries(path: &Path) -> Vec<String> {
    match fs::read_dir(path) {
        Ok(rd) => rd
            .flatten()
            .filter_map(|e| e.file_name().into_string().ok())
            .collect(),
        Err(_) => Vec::new(),
    }
}

pub fn game_candidates() -> Vec<GameInstall> {
    let mut result = Vec::new();
    let mut seen = HashSet::new();

    for var in ["ATS_GAME_DIR", "AGAINST_THE_STORM_DIR"] {
        if let Ok(value) = std::env::var(var) {
            add_candidate(
                &mut result,
                &mut seen,
                PathBuf::from(value),
                GameSource::Manual,
            );
        }
    }

    for path in steam_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Steam);
    }
    for path in epic_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Epic);
    }
    for path in gog_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Gog);
    }

    result
}

/// Returns the path of a detected Microsoft Store / Xbox install, if any. These
/// live under `WindowsApps` (or the Xbox `Content` layout) and cannot be modded
/// with BepInEx, so the UI surfaces this as a warning rather than a candidate.
pub fn detect_store_warning() -> Option<PathBuf> {
    for path in xbox_candidates() {
        if validate_game_dir(&path) {
            return Some(path);
        }
    }
    None
}

fn add_candidate(
    result: &mut Vec<GameInstall>,
    seen: &mut HashSet<String>,
    path: PathBuf,
    source: GameSource,
) {
    let normalized = path.to_string_lossy().trim().trim_matches('"').to_string();
    if normalized.is_empty() {
        return;
    }
    let key = normalized.to_lowercase();
    if seen.insert(key) {
        result.push(GameInstall {
            path: PathBuf::from(normalized),
            source,
        });
    }
}

// ---------------------------------------------------------------------------
// Steam
// ---------------------------------------------------------------------------

pub fn parse_steam_library_paths(content: &str) -> Vec<PathBuf> {
    let re = Regex::new(r#""path"\s+"([^"]+)""#).unwrap();
    re.captures_iter(content)
        .map(|cap| PathBuf::from(cap[1].replace("\\\\", "\\")))
        .collect()
}

fn steam_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();
    for steam_root in steam_roots() {
        push_steam_common(&mut candidates, &steam_root);
        let vdf = steam_root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(content) = fs::read_to_string(vdf) {
            for lib in parse_steam_library_paths(&content) {
                push_steam_common(&mut candidates, &lib);
            }
        }
    }
    candidates
}

/// Push both the full game and the demo (Steam app 1400860, installdir
/// "Against the Storm Demo") under a library root's common folder.
fn push_steam_common(candidates: &mut Vec<PathBuf>, library_root: &Path) {
    let common = library_root.join("steamapps").join("common");
    candidates.push(common.join("Against the Storm"));
    candidates.push(common.join("Against the Storm Demo"));
}

fn steam_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    #[cfg(windows)]
    {
        use winreg::RegKey;
        use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

        if let Ok(key) = RegKey::predef(HKEY_CURRENT_USER).open_subkey("Software\\Valve\\Steam") {
            if let Ok(path) = key.get_value::<String, _>("SteamPath") {
                roots.push(PathBuf::from(path.replace('/', "\\")));
            }
        }
        if let Ok(key) =
            RegKey::predef(HKEY_LOCAL_MACHINE).open_subkey("SOFTWARE\\WOW6432Node\\Valve\\Steam")
        {
            if let Ok(path) = key.get_value::<String, _>("InstallPath") {
                roots.push(PathBuf::from(path));
            }
        }
    }

    roots.push(PathBuf::from("C:\\Program Files (x86)\\Steam"));
    roots
}

// ---------------------------------------------------------------------------
// Epic Games Store
// ---------------------------------------------------------------------------

/// Epic records each installed app as a JSON `.item` manifest under
/// `%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests`. We scan those for the
/// game by display name and read its `InstallLocation`.
fn epic_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();
    let manifests_dir = epic_manifests_dir();
    let Ok(entries) = fs::read_dir(&manifests_dir) else {
        return candidates;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if path.extension().and_then(|e| e.to_str()) != Some("item") {
            continue;
        }
        let Ok(text) = fs::read_to_string(&path) else {
            continue;
        };
        if let Some(install) = parse_epic_manifest(&text) {
            candidates.push(PathBuf::from(install));
        }
    }
    candidates
}

fn epic_manifests_dir() -> PathBuf {
    let program_data =
        std::env::var("ProgramData").unwrap_or_else(|_| "C:\\ProgramData".to_string());
    PathBuf::from(program_data)
        .join("Epic")
        .join("EpicGamesLauncher")
        .join("Data")
        .join("Manifests")
}

/// Pull `InstallLocation` from an Epic `.item` manifest whose `DisplayName`
/// names Against the Storm. Returns None for unrelated manifests.
pub fn parse_epic_manifest(text: &str) -> Option<String> {
    let value: serde_json::Value = serde_json::from_str(text).ok()?;
    let display = value.get("DisplayName")?.as_str()?.to_lowercase();
    if !display.contains("against the storm") {
        return None;
    }
    value
        .get("InstallLocation")?
        .as_str()
        .map(|s| s.to_string())
}

// ---------------------------------------------------------------------------
// GOG
// ---------------------------------------------------------------------------

fn gog_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    #[cfg(windows)]
    {
        candidates.extend(gog_registry_candidates());
    }

    candidates.push(PathBuf::from("C:\\GOG Games\\Against the Storm"));
    candidates.push(PathBuf::from(
        "C:\\Program Files (x86)\\GOG Galaxy\\Games\\Against the Storm",
    ));
    candidates.push(PathBuf::from(
        "C:\\Program Files\\GOG Galaxy\\Games\\Against the Storm",
    ));

    candidates
}

#[cfg(windows)]
fn gog_registry_candidates() -> Vec<PathBuf> {
    use winreg::RegKey;
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

    let mut candidates = Vec::new();
    for hive in [HKEY_LOCAL_MACHINE, HKEY_CURRENT_USER] {
        let root = RegKey::predef(hive);
        for subkey in [
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
        ] {
            if let Ok(key) = root.open_subkey(subkey) {
                for name in key.enum_keys().flatten() {
                    if let Ok(app) = key.open_subkey(name) {
                        let display = app
                            .get_value::<String, _>("DisplayName")
                            .unwrap_or_default();
                        if !display.to_lowercase().contains("against the storm") {
                            continue;
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallLocation") {
                            candidates.push(PathBuf::from(path));
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallDir") {
                            candidates.push(PathBuf::from(path));
                        }
                    }
                }
            }
        }
    }
    candidates
}

// ---------------------------------------------------------------------------
// Microsoft Store / Xbox (detect-and-warn only)
// ---------------------------------------------------------------------------

fn xbox_candidates() -> Vec<PathBuf> {
    // The Xbox app's default library layout, on any fixed drive (the user can
    // pick the install drive in the Xbox app; the folder is always XboxGames).
    // The packaged `WindowsApps` location is ACL-locked and unreadable, so
    // there's nothing to validate there — we only detect the visible XboxGames
    // layout to warn the user that the Game Pass / Store build isn't moddable.
    let mut candidates = Vec::new();
    for letter in b'A'..=b'Z' {
        let drive = letter as char;
        candidates.push(PathBuf::from(format!(
            "{drive}:\\XboxGames\\Against the Storm\\Content"
        )));
    }
    candidates
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_steam_library_paths() {
        let content = r#"
        "0" { "path" "C:\\Program Files (x86)\\Steam" }
        "1" { "path" "D:\\SteamLibrary" }
        "#;
        let paths = parse_steam_library_paths(content);
        assert_eq!(paths.len(), 2);
        assert_eq!(paths[1], PathBuf::from("D:\\SteamLibrary"));
    }

    #[test]
    fn validates_full_game_dir() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("Against the Storm.exe"), "").unwrap();
        let managed = dir.path().join("Against the Storm_Data").join("Managed");
        fs::create_dir_all(&managed).unwrap();
        fs::write(managed.join("Assembly-CSharp.dll"), "").unwrap();
        assert!(validate_game_dir(dir.path()));
    }

    #[test]
    fn validates_demo_game_dir() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("Against the Storm Demo.exe"), "").unwrap();
        let managed = dir.path().join("Against the Storm Demo_Data").join("Managed");
        fs::create_dir_all(&managed).unwrap();
        fs::write(managed.join("Assembly-CSharp.dll"), "").unwrap();
        assert!(validate_game_dir(dir.path()));
    }

    #[test]
    fn rejects_dir_without_managed_assembly() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("Against the Storm.exe"), "").unwrap();
        assert!(!validate_game_dir(dir.path()));
    }

    #[test]
    fn parses_epic_manifest_for_the_game() {
        let text = r#"{
            "DisplayName": "Against the Storm",
            "InstallLocation": "D:\\Epic\\AgainstTheStorm"
        }"#;
        assert_eq!(
            parse_epic_manifest(text).as_deref(),
            Some("D:\\Epic\\AgainstTheStorm")
        );
    }

    #[test]
    fn ignores_unrelated_epic_manifest() {
        let text = r#"{ "DisplayName": "Some Other Game", "InstallLocation": "X:\\Y" }"#;
        assert!(parse_epic_manifest(text).is_none());
    }
}
