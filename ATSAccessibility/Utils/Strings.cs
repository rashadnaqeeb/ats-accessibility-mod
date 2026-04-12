using ATSAccessibility.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Mod-owned localized strings for screen-reader announcements.
	///
	/// Game content (building names, recipes, effect descriptions) is already translated
	/// by the game — keep using <see cref="GameReflection.GetLocaText"/> for those. This
	/// table is only for mod-authored text: section labels, status phrases, format strings.
	///
	/// Files live in <c>ATSAccessibility/Strings/*.properties</c> as embedded resources.
	/// The active language is picked from <see cref="LocalizationReflection.GetCurrentLocaCode"/>
	/// with English as the fallback.
	///
	/// Key convention: <c>scope.area.name</c> (e.g. <c>panel.workers.title</c>,
	/// <c>dialog.confirm.destroy</c>). Positional args use <c>{0}</c>, <c>{1}</c>, … so
	/// translators can reorder them when word order differs.
	/// </summary>
	public static class Strings {
		private const string FallbackLanguage = "en";
		private const string ResourcePrefix = "ATSAccessibility.Strings.";

		private static Dictionary<string, string> _current = new Dictionary<string, string>();
		private static Dictionary<string, string> _fallback = new Dictionary<string, string>();
		private static string _activeLanguage = FallbackLanguage;
		private static readonly HashSet<string> _warnedKeys = new HashSet<string>();

		/// <summary>
		/// Load fallback (English) strings. Call once at mod startup. The game's language
		/// isn't known this early, so <see cref="ApplyGameLanguage"/> must be called later
		/// once services are available to switch to the user's preferred language.
		/// </summary>
		public static void Initialize() {
			_fallback = LoadTable(FallbackLanguage) ?? new Dictionary<string, string>();
			_current = _fallback;
			_activeLanguage = FallbackLanguage;
			Debug.Log($"[ATSAccessibility] Strings initialized: {_fallback.Count} entries (fallback: {FallbackLanguage})");
		}

		/// <summary>
		/// Switch to the game's current language if a matching table ships with the mod.
		/// Honours the <c>Plugin.ForceLanguage</c> config override when set (used for
		/// translation testing without relying on the game's language setting).
		/// Safe to call repeatedly; no-op if the language hasn't changed.
		/// </summary>
		public static void ApplyGameLanguage() {
			string forced = ATSAccessibility.Core.Plugin.ForceLanguage?.Value;
			string code = !string.IsNullOrEmpty(forced) ? forced : LocalizationReflection.GetCurrentLocaCode();
			if (string.IsNullOrEmpty(code) || code == _activeLanguage) return;

			var table = LoadTable(code);
			if (table == null) {
				Debug.Log($"[ATSAccessibility] No mod translation for '{code}', staying on {_activeLanguage}");
				return;
			}

			_current = table;
			_activeLanguage = code;
			Debug.Log($"[ATSAccessibility] Strings language set to '{code}' ({table.Count} entries)");
		}

		/// <summary>
		/// Look up a key. If the current language is missing the key, falls back to English;
		/// if English is missing too, returns the key wrapped in angle brackets (easy to spot in logs).
		/// </summary>
		public static string Get(string key) {
			if (string.IsNullOrEmpty(key)) return key;

			if (_current.TryGetValue(key, out var text)) return text;
			if (_current != _fallback && _fallback.TryGetValue(key, out text)) return text;

			WarnOnce(key);
			return $">{key}<";
		}

		/// <summary>
		/// Look up a key and format it with positional args (<c>{0}</c>, <c>{1}</c>, …).
		/// Uses the game's current culture for number formatting so translators get the
		/// right decimal separators, plural forms, etc.
		/// </summary>
		public static string Get(string key, params object[] args) {
			string template = Get(key);
			if (args == null || args.Length == 0) return template;

			try {
				return string.Format(CultureInfo.CurrentCulture, template, args);
			} catch (FormatException ex) {
				Debug.LogWarning($"[ATSAccessibility] Strings.Get format error for '{key}': {ex.Message}");
				return template;
			}
		}

		/// <summary>
		/// Pick between singular and plural keys based on count.
		///
		/// **English-only plural rule** (`n == 1 → one`, else `other`). This method is NOT
		/// safe to use once a non-English language table is added — Russian, Polish, Arabic,
		/// and many other languages have 3–6 plural forms and will produce grammatically
		/// wrong output under this rule. Before shipping a second language, extend this
		/// to consult a per-language plural resolver (CLDR rules are the standard).
		///
		/// Currently has zero call sites, so left in place as a guardrail comment.
		/// </summary>
		public static string Plural(int count, string oneKey, string otherKey, params object[] args) {
			string key = count == 1 ? oneKey : otherKey;
			if (args == null || args.Length == 0) return Get(key);
			return Get(key, args);
		}

		// ========================================
		// LOADING
		// ========================================

		private static Dictionary<string, string> LoadTable(string language) {
			string resourceName = ResourcePrefix + language + ".properties";
			var assembly = Assembly.GetExecutingAssembly();

			using (var stream = assembly.GetManifestResourceStream(resourceName)) {
				if (stream == null) return null;

				using (var reader = new StreamReader(stream)) {
					return Parse(reader);
				}
			}
		}

		private static Dictionary<string, string> Parse(TextReader reader) {
			var map = new Dictionary<string, string>();
			string line;
			int lineNum = 0;

			while ((line = reader.ReadLine()) != null) {
				lineNum++;
				if (line.Length == 0) continue;
				if (line[0] == '#' || line[0] == '!') continue;  // Comment lines

				int eq = line.IndexOf('=');
				if (eq <= 0) continue;

				string key = line.Substring(0, eq).Trim();
				string value = line.Substring(eq + 1);
				if (key.Length == 0) continue;

				map[key] = Unescape(value);
			}

			return map;
		}

		private static string Unescape(string s) {
			if (s.IndexOf('\\') < 0) return s;

			var sb = new System.Text.StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (c == '\\' && i + 1 < s.Length) {
					char next = s[++i];
					switch (next) {
						case 'n': sb.Append('\n'); break;
						case 't': sb.Append('\t'); break;
						case 'r': sb.Append('\r'); break;
						case 's': sb.Append(' '); break;  // visible marker for significant trailing spaces (editor-trim-safe)
						case '\\': sb.Append('\\'); break;
						case '=': sb.Append('='); break;
						default: sb.Append(next); break;
					}
				} else {
					sb.Append(c);
				}
			}
			return sb.ToString();
		}

		private static void WarnOnce(string key) {
			if (_warnedKeys.Add(key)) {
				Debug.LogWarning($"[ATSAccessibility] Missing string key: '{key}'");
			}
		}
	}
}
