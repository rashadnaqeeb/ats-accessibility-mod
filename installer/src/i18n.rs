//! Installer UI localization.
//!
//! Strings live in `src/i18n/<code>.properties` (same key=value format as the
//! mod's own string tables) and are embedded at build time. Language codes are
//! the *game's* loca codes (en, de, es, es-LATAM, fr, pl, pt, ru, th, zh-CN,
//! ...), so the `--lang` value the mod passes maps straight through.
//!
//! English is the always-present fallback. To add a language: drop a
//! `<code>.properties` file in `src/i18n/`, add it to `TABLES`, and add a
//! display name in `DISPLAY_NAMES`. Missing keys fall back to English.

use std::collections::HashMap;
use std::sync::RwLock;
use std::sync::OnceLock;

/// (code, embedded properties text). English must be present. Codes are the
/// game's own loca codes so the `--lang` value the mod passes maps straight
/// through. We ship a table for every language the game supports; es-LATAM is
/// served by the single `es` table via ALIASES (one Spanish, by design).
const TABLES: &[(&str, &str)] = &[
    ("en", include_str!("i18n/en.properties")),
    ("cs", include_str!("i18n/cs.properties")),
    ("de", include_str!("i18n/de.properties")),
    ("es", include_str!("i18n/es.properties")),
    ("fr", include_str!("i18n/fr.properties")),
    ("hu", include_str!("i18n/hu.properties")),
    ("it", include_str!("i18n/it.properties")),
    ("ja", include_str!("i18n/ja.properties")),
    ("ko", include_str!("i18n/ko.properties")),
    ("pl", include_str!("i18n/pl.properties")),
    ("pt", include_str!("i18n/pt.properties")),
    ("ru", include_str!("i18n/ru.properties")),
    ("th", include_str!("i18n/th.properties")),
    ("tr", include_str!("i18n/tr.properties")),
    ("ua", include_str!("i18n/ua.properties")),
    ("zh-CN", include_str!("i18n/zh-CN.properties")),
    ("zh-CT", include_str!("i18n/zh-CT.properties")),
];

/// Game loca codes that are served by another table. es-LATAM uses the single
/// Spanish table.
const ALIASES: &[(&str, &str)] = &[("es-LATAM", "es")];

/// Human-readable names for the language switcher, in the language itself.
const DISPLAY_NAMES: &[(&str, &str)] = &[
    ("en", "English"),
    ("cs", "Čeština"),
    ("de", "Deutsch"),
    ("es", "Español"),
    ("fr", "Français"),
    ("hu", "Magyar"),
    ("it", "Italiano"),
    ("ja", "日本語"),
    ("ko", "한국어"),
    ("pl", "Polski"),
    ("pt", "Português"),
    ("ru", "Русский"),
    ("th", "ไทย"),
    ("tr", "Türkçe"),
    ("ua", "Українська"),
    ("zh-CN", "简体中文"),
    ("zh-CT", "繁體中文"),
];

const FALLBACK: &str = "en";

/// Resolve a requested code through ALIASES (e.g. es-LATAM -> es).
fn resolve_alias(code: &str) -> &str {
    for (from, to) in ALIASES {
        if from.eq_ignore_ascii_case(code) {
            return to;
        }
    }
    code
}

struct State {
    active: String,
    current: HashMap<String, String>,
    fallback: HashMap<String, String>,
}

static STATE: OnceLock<RwLock<State>> = OnceLock::new();

fn parse(text: &str) -> HashMap<String, String> {
    let mut map = HashMap::new();
    for line in text.lines() {
        let line = line.trim_end_matches(['\r']);
        if line.is_empty() {
            continue;
        }
        let first = line.as_bytes()[0];
        if first == b'#' || first == b'!' {
            continue;
        }
        if let Some(eq) = line.find('=') {
            if eq == 0 {
                continue;
            }
            let key = line[..eq].trim().to_string();
            let value = unescape(&line[eq + 1..]);
            if !key.is_empty() {
                map.insert(key, value);
            }
        }
    }
    map
}

fn unescape(s: &str) -> String {
    if !s.contains('\\') {
        return s.to_string();
    }
    let mut out = String::with_capacity(s.len());
    let mut chars = s.chars();
    while let Some(c) = chars.next() {
        if c == '\\' {
            match chars.next() {
                Some('n') => out.push('\n'),
                Some('t') => out.push('\t'),
                Some('r') => out.push('\r'),
                Some('s') => out.push(' '),
                Some('\\') => out.push('\\'),
                Some('=') => out.push('='),
                Some(other) => out.push(other),
                None => out.push('\\'),
            }
        } else {
            out.push(c);
        }
    }
    out
}

fn table_for(code: &str) -> Option<HashMap<String, String>> {
    TABLES
        .iter()
        .find(|(c, _)| c.eq_ignore_ascii_case(code))
        .map(|(_, text)| parse(text))
}

/// Initialize the active language. `requested` is the `--lang` value passed by
/// the mod (a game loca code); when absent, auto-detect from the OS UI language.
/// Falls back to English when there's no matching table.
pub fn init(requested: Option<&str>) {
    let fallback = table_for(FALLBACK).unwrap_or_default();

    let requested_code = requested
        .map(|s| s.to_string())
        .unwrap_or_else(detect_os_language);
    let code = resolve_alias(&requested_code).to_string();

    let (active, current) = match table_for(&code) {
        Some(t) => (code, t),
        None => (FALLBACK.to_string(), fallback.clone()),
    };

    let state = State {
        active,
        current,
        fallback,
    };
    let _ = STATE.set(RwLock::new(state));
}

/// Switch the active language at runtime (language switcher in the GUI).
pub fn set_language(code: &str) {
    let Some(lock) = STATE.get() else {
        return;
    };
    let resolved = resolve_alias(code);
    let table = table_for(resolved);
    let mut state = lock.write().unwrap();
    match table {
        Some(t) => {
            state.active = resolved.to_string();
            state.current = t;
        }
        None => {
            state.active = FALLBACK.to_string();
            state.current = state.fallback.clone();
        }
    }
}

pub fn active_language() -> String {
    STATE
        .get()
        .map(|l| l.read().unwrap().active.clone())
        .unwrap_or_else(|| FALLBACK.to_string())
}

/// The display name for a code (e.g. "en" -> "English"), in that language.
pub fn display_name(code: &str) -> String {
    DISPLAY_NAMES
        .iter()
        .find(|(c, _)| c.eq_ignore_ascii_case(code))
        .map(|(_, n)| n.to_string())
        .unwrap_or_else(|| code.to_string())
}

/// Languages available in the picker: those we actually ship a table for.
/// Returns (code, display_name) pairs.
pub fn available_languages() -> Vec<(String, String)> {
    TABLES
        .iter()
        .map(|(code, _)| {
            let name = DISPLAY_NAMES
                .iter()
                .find(|(c, _)| c.eq_ignore_ascii_case(code))
                .map(|(_, n)| n.to_string())
                .unwrap_or_else(|| code.to_string());
            (code.to_string(), name)
        })
        .collect()
}

/// Look up a key. Falls back to English, then to the key itself wrapped in
/// angle brackets so a missing string is obvious.
pub fn t(key: &str) -> String {
    if let Some(lock) = STATE.get() {
        let state = lock.read().unwrap();
        if let Some(v) = state.current.get(key) {
            return v.clone();
        }
        if let Some(v) = state.fallback.get(key) {
            return v.clone();
        }
    }
    format!(">{key}<")
}

/// Look up a key and substitute positional args `{0}`, `{1}`, ...
pub fn tf(key: &str, args: &[&str]) -> String {
    let mut text = t(key);
    for (i, arg) in args.iter().enumerate() {
        text = text.replace(&format!("{{{i}}}"), arg);
    }
    text
}

/// Map the OS UI language to a game loca code. Only used for standalone launch;
/// when the mod launches us it passes `--lang` explicitly.
fn detect_os_language() -> String {
    let locale = os_locale().unwrap_or_default().to_lowercase();
    map_locale_to_code(&locale)
}

pub fn map_locale_to_code(locale: &str) -> String {
    // locale looks like "en-us", "zh-hans", "es-419", "uk-ua", etc.
    let lower = locale.to_lowercase();
    let primary = lower.split(['-', '_']).next().unwrap_or("");
    match primary {
        "cs" => "cs",
        "de" => "de",
        "es" => "es", // one Spanish; es-LATAM aliases here anyway
        "fr" => "fr",
        "hu" => "hu",
        "it" => "it",
        "ja" => "ja",
        "ko" => "ko",
        "pl" => "pl",
        "pt" => "pt",
        "ru" => "ru",
        "th" => "th",
        "tr" => "tr",
        "uk" | "ua" => "ua", // game uses "ua"; OS uses ISO "uk"
        "zh" => {
            if lower.contains("hant")
                || lower.contains("tw")
                || lower.contains("hk")
                || lower.contains("mo")
            {
                "zh-CT"
            } else {
                "zh-CN"
            }
        }
        _ => "en",
    }
    .to_string()
}

#[cfg(windows)]
fn os_locale() -> Option<String> {
    use windows_sys::Win32::Globalization::GetUserDefaultLocaleName;
    // LOCALE_NAME_MAX_LENGTH is 85.
    let mut buf = [0u16; 85];
    let len = unsafe { GetUserDefaultLocaleName(buf.as_mut_ptr(), buf.len() as i32) };
    if len <= 1 {
        return None;
    }
    let slice = &buf[..(len as usize - 1)];
    Some(String::from_utf16_lossy(slice))
}

#[cfg(not(windows))]
fn os_locale() -> Option<String> {
    std::env::var("LANG").ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_common_locales() {
        assert_eq!(map_locale_to_code("en-US"), "en");
        assert_eq!(map_locale_to_code("de-DE"), "de");
        assert_eq!(map_locale_to_code("es-ES"), "es");
        assert_eq!(map_locale_to_code("es-419"), "es");
        assert_eq!(map_locale_to_code("zh-Hans"), "zh-CN");
        assert_eq!(map_locale_to_code("zh-Hant-TW"), "zh-CT");
        assert_eq!(map_locale_to_code("ja-JP"), "ja");
        assert_eq!(map_locale_to_code("uk-UA"), "ua");
        assert_eq!(map_locale_to_code("pt-BR"), "pt");
        assert_eq!(map_locale_to_code("xx-YY"), "en");
    }

    #[test]
    fn all_tables_parse_and_have_title() {
        for (code, _) in TABLES {
            let table = table_for(code).unwrap_or_else(|| panic!("{code} table missing"));
            assert!(table.contains_key("app.title"), "{code} missing app.title");
        }
    }

    #[test]
    fn es_latam_aliases_to_spanish() {
        assert_eq!(resolve_alias("es-LATAM"), "es");
        assert!(table_for(resolve_alias("es-LATAM")).is_some());
    }
}
