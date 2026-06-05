use std::collections::HashMap;
use std::fs;
use std::path::Path;

use serde::{Deserialize, Serialize};

use super::paths;

pub const SUPPORTED_SCHEMA: u32 = 1;

/// Record of what this installer placed into the game directory, so updates can
/// avoid re-backing-up its own files and uninstall can cleanly reverse it.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct InstallManifest {
    pub schema_version: u32,
    pub mod_version: String,
    pub installed_at: String,
    pub source: String,
    pub release_asset: String,
    pub sha256: Option<String>,
    pub installed_files: Vec<String>,
    pub backups: HashMap<String, String>,
    /// The user's chosen installer UI language (a game loca code). Optional and
    /// defaulted so manifests written before this field parse fine.
    #[serde(default)]
    pub language: Option<String>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ManifestRead {
    Missing,
    Valid(InstallManifest),
    Invalid(String),
}

impl InstallManifest {
    pub fn validate(&self) -> Result<(), String> {
        if self.schema_version != SUPPORTED_SCHEMA {
            return Err(format!("Unsupported manifest schema {}", self.schema_version));
        }
        if self.mod_version.trim().is_empty() {
            return Err("Manifest is missing mod_version".to_string());
        }
        if self.installed_at.trim().is_empty() {
            return Err("Manifest is missing installed_at".to_string());
        }
        if self.source.trim().is_empty() {
            return Err("Manifest is missing source".to_string());
        }
        if self.release_asset.trim().is_empty() {
            return Err("Manifest is missing release_asset".to_string());
        }
        Ok(())
    }

    pub fn read(game_dir: &Path) -> ManifestRead {
        let path = paths::manifest_path(game_dir);
        if !path.exists() {
            return ManifestRead::Missing;
        }
        let text = match fs::read_to_string(&path) {
            Ok(t) => t,
            Err(e) => return ManifestRead::Invalid(format!("Failed to read manifest: {e}")),
        };
        let manifest: InstallManifest = match serde_json::from_str(&text) {
            Ok(m) => m,
            Err(e) => return ManifestRead::Invalid(format!("Invalid manifest JSON: {e}")),
        };
        if let Err(e) = manifest.validate() {
            return ManifestRead::Invalid(e);
        }
        ManifestRead::Valid(manifest)
    }

    pub fn write(&self, game_dir: &Path) -> Result<(), String> {
        self.validate()?;
        let path = paths::manifest_path(game_dir);
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|e| format!("Failed to create manifest dir: {e}"))?;
        }
        let json = serde_json::to_string_pretty(self)
            .map_err(|e| format!("Failed to serialize manifest: {e}"))?;
        fs::write(path, json).map_err(|e| format!("Failed to write manifest: {e}"))
    }
}

/// The UI language saved in this install's manifest, if any.
pub fn saved_language(game_dir: &Path) -> Option<String> {
    match InstallManifest::read(game_dir) {
        ManifestRead::Valid(m) => m.language,
        _ => None,
    }
}

/// Persist the chosen UI language into an existing manifest. No-op if there's no
/// valid manifest yet — a fresh install records the language when it first
/// writes the manifest.
pub fn set_saved_language(game_dir: &Path, language: &str) {
    if let ManifestRead::Valid(mut m) = InstallManifest::read(game_dir) {
        m.language = Some(language.to_string());
        let _ = m.write(game_dir);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn rejects_missing_required_field() {
        let json = r#"{
            "schema_version": 1,
            "installed_at": "2026-06-02T00:00:00Z",
            "source": "manual",
            "release_asset": "ATSAccessibility-v1.0.0-with-BepInEx.zip",
            "sha256": null,
            "installed_files": [],
            "backups": {}
        }"#;
        let parsed: Result<InstallManifest, _> = serde_json::from_str(json);
        assert!(parsed.is_err());
    }

    #[test]
    fn rejects_unsupported_schema() {
        let manifest = InstallManifest {
            schema_version: 999,
            mod_version: "1.0.0".to_string(),
            installed_at: "2026-06-02T00:00:00Z".to_string(),
            source: "manual".to_string(),
            release_asset: "ATSAccessibility-v1.0.0-with-BepInEx.zip".to_string(),
            sha256: None,
            installed_files: Vec::new(),
            backups: HashMap::new(),
            language: None,
        };
        assert!(manifest.validate().is_err());
    }
}
