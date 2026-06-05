use std::fs;
use std::path::Path;

use regex::Regex;

/// Ensure `HideManagerGameObject = true` in BepInEx.cfg. Without it BepInEx
/// leaves a manager GameObject in the scene that the game's UI scanning can trip
/// over; the mod expects it hidden.
pub fn ensure_hide_manager_game_object(path: &Path) -> Result<bool, String> {
    let input = fs::read_to_string(path)
        .map_err(|e| format!("Failed to read BepInEx.cfg at {}: {e}", path.display()))?;
    let (output, changed) = patch_content(&input);
    if changed {
        fs::write(path, output)
            .map_err(|e| format!("Failed to write BepInEx.cfg at {}: {e}", path.display()))?;
    }
    Ok(changed)
}

pub fn patch_content(input: &str) -> (String, bool) {
    let line_re = Regex::new(r"(?i)^(\s*HideManagerGameObject\s*=\s*)(\S+)(.*)$").unwrap();
    let mut changed = false;
    let mut found = false;
    let mut lines: Vec<String> = Vec::new();

    for line in input.lines() {
        if let Some(caps) = line_re.captures(line) {
            found = true;
            if caps.get(2).map(|m| m.as_str()).unwrap_or("") != "true" {
                changed = true;
                lines.push(format!(
                    "{}true{}",
                    caps.get(1).unwrap().as_str(),
                    caps.get(3).map(|m| m.as_str()).unwrap_or("")
                ));
            } else {
                lines.push(line.to_string());
            }
        } else {
            lines.push(line.to_string());
        }
    }

    if !found {
        changed = true;
        if !lines.is_empty() && !lines.last().unwrap().is_empty() {
            lines.push(String::new());
        }
        lines.push("[Chainloader]".to_string());
        lines.push("HideManagerGameObject = true".to_string());
    }

    let mut output = lines.join("\n");
    if input.ends_with('\n') {
        output.push('\n');
    }
    (output, changed)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn changes_false_to_true_and_preserves_other_lines() {
        let input = "[Chainloader]\nOther = value\nHideManagerGameObject = false\n";
        let (output, changed) = patch_content(input);
        assert!(changed);
        assert!(output.contains("Other = value"));
        assert!(output.contains("HideManagerGameObject = true"));
    }

    #[test]
    fn does_not_change_when_already_true() {
        let input = "[Chainloader]\nHideManagerGameObject = true\n";
        let (output, changed) = patch_content(input);
        assert!(!changed);
        assert_eq!(output, input);
    }

    #[test]
    fn appends_key_when_missing() {
        let input = "[Logging]\nEnabled = true\n";
        let (output, changed) = patch_content(input);
        assert!(changed);
        assert!(output.contains("[Chainloader]"));
        assert!(output.contains("HideManagerGameObject = true"));
    }
}
