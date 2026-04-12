using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Dev utility: dump every language's game-canon translation table to JSON
	/// files on disk, so a translator can grep the game's own rendering of
	/// brand-critical terms (Viceroy / Hearth / Resolve / species names / etc.)
	/// in the target language before translating the mod's own <c>Strings</c>
	/// table.
	///
	/// Uses the same <c>Resources.Load&lt;TextAsset&gt;("Texts/&lt;code&gt;")</c> path
	/// the game's own <c>TextsFilesLoader</c> uses, so it's stateless and does
	/// not touch the currently-active language.
	///
	/// Enabled by the <c>[Localization] DumpGameLocalization</c> BepInEx config
	/// entry. See <c>Strings/TRANSLATION.md</c> for the full procedure.
	/// </summary>
	public static class LocaDumper {
		// Language codes from Against the Storm_Data/StreamingAssets/aa/catalog.json.
		// Mirror of the TRANSLATION.md table; keep in sync if the game adds languages.
		private static readonly string[] LanguageCodes = {
			"en", "cs", "de", "es", "es-LATAM", "fr", "hu", "it",
			"ja", "ko", "pl", "pt", "ru", "th", "tr", "ua", "zh-CN", "zh-CT",
		};

		private static bool _alreadyRan;

		/// <summary>
		/// Dump every game language's raw loca JSON to
		/// <c>Documents/ATSAccessibility-Locas/&lt;code&gt;.json</c>. Idempotent
		/// per mod session (only runs once per launch).
		/// </summary>
		public static void DumpAll() {
			if (_alreadyRan) return;
			_alreadyRan = true;

			string outDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				"ATSAccessibility-Locas");

			try {
				Directory.CreateDirectory(outDir);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] LocaDumper: cannot create {outDir}: {ex.Message}");
				return;
			}

			int success = 0;
			List<string> failed = new List<string>();

			foreach (string code in LanguageCodes) {
				try {
					var ta = Resources.Load<TextAsset>("Texts/" + code);
					if (ta == null || string.IsNullOrEmpty(ta.text)) {
						failed.Add(code);
						continue;
					}
					string path = Path.Combine(outDir, code + ".json");
					File.WriteAllText(path, ta.text);
					success++;
				} catch (Exception ex) {
					failed.Add(code + " (" + ex.Message + ")");
				}
			}

			string msg = $"[ATSAccessibility] LocaDumper: wrote {success}/{LanguageCodes.Length} language tables to {outDir}";
			if (failed.Count > 0) msg += "; failed: " + string.Join(", ", failed);
			Debug.Log(msg);

			// Surface in-game so the user knows to flip the config back off.
			if (Speech.IsAvailable) {
				Speech.Say($"Localization dump complete. {success} files written to Documents, ATSAccessibility-Locas.");
			}
		}
	}
}
