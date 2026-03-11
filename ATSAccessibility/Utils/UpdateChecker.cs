using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace ATSAccessibility.Utils {
	public static class UpdateChecker {
		private static readonly string RELEASES_URL = "https://github.com/rashadnaqeeb/ats-accessibility-mod/releases/latest";
		private static readonly string API_URL = "https://api.github.com/repos/rashadnaqeeb/ats-accessibility-mod/releases/latest";
		private static readonly Regex TagRegex = new Regex(@"""tag_name""\s*:\s*""v?([^""]+)""", RegexOptions.Compiled);

		private static volatile string _result; // null = pending, "up-to-date", "update-available", or "error:..."
		private static bool _checked = false;

		public static void Check(string currentVersion) {
			if (_checked) return;
			_checked = true;
			_result = null;

			UnityEngine.Debug.Log($"[ATSAccessibility] Update check starting (current version: {currentVersion})");

			var thread = new Thread(() => {
				try {
					var request = (HttpWebRequest)WebRequest.Create(API_URL);
					request.UserAgent = "ATSAccessibility";
					request.Timeout = 10000;

					using (var response = (HttpWebResponse)request.GetResponse())
					using (var reader = new StreamReader(response.GetResponseStream())) {
						string json = reader.ReadToEnd();
						var match = TagRegex.Match(json);
						if (match.Success) {
							string latestVersion = match.Groups[1].Value;
							UnityEngine.Debug.Log($"[ATSAccessibility] Latest release version: {latestVersion}");
							if (latestVersion == currentVersion)
								_result = "up-to-date";
							else
								_result = "update-available";
						} else {
							_result = "error:Could not parse release version";
						}
					}
				} catch (Exception ex) {
					_result = $"error:{ex.Message}";
				}
			});
			thread.IsBackground = true;
			thread.Start();
		}

		/// <summary>
		/// Call from Update(). Returns true once the check has been handled (no further polling needed).
		/// </summary>
		public static bool TryAnnounceResult() {
			if (_result == null) return false;

			if (_result == "up-to-date") {
				Speech.Say("Mod is up to date", interrupt: false);
			} else if (_result == "update-available") {
				Speech.Say("A mod update is available. Opening releases page.", interrupt: false);
				try {
					Process.Start(new ProcessStartInfo {
						FileName = RELEASES_URL,
						UseShellExecute = true
					});
				} catch (Exception ex) {
					UnityEngine.Debug.Log($"[ATSAccessibility] Failed to open releases page: {ex.Message}");
				}
			} else {
				// error - log but don't bother the user
				UnityEngine.Debug.Log($"[ATSAccessibility] Update check failed: {_result}");
			}

			return true;
		}

		/// <summary>
		/// Reset state for next session (called on scene unload if needed).
		/// </summary>
		public static void Reset() {
			_checked = false;
			_result = null;
		}
	}
}
