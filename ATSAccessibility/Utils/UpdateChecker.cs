using ATSAccessibility.Reflection;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace ATSAccessibility.Utils {
	public static class UpdateChecker {
		private static readonly string API_URL = "https://api.github.com/repos/rashadnaqeeb/ats-accessibility-mod/releases/latest";

		// Direct permalink to the latest installer exe. Used as a fallback to get
		// manually-installed users onto the installer when no local copy exists.
		private static readonly string INSTALLER_DOWNLOAD_URL = "https://github.com/rashadnaqeeb/ats-accessibility-mod/releases/latest/download/ATSAccessibilityInstaller.exe";

		// The installer drops a copy of itself in the game root during install.
		private const string INSTALLER_EXE_NAME = "ATSAccessibilityInstaller.exe";

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
							_result = IsNewer(latestVersion, currentVersion) ? "update-available" : "up-to-date";
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
		/// True when <paramref name="latest"/> is a strictly newer version than
		/// <paramref name="current"/>. Uses real version comparison so a different
		/// tag format (e.g. "v1.06.3" or a re-tag) doesn't spuriously report an
		/// update. Falls back to a trimmed string inequality if either side can't
		/// be parsed.
		/// </summary>
		private static bool IsNewer(string latest, string current) {
			if (Version.TryParse(latest, out var latestVersion) && Version.TryParse(current, out var currentVersion))
				return latestVersion > currentVersion;
			return !string.Equals(latest?.Trim(), current?.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Call from Update(). Returns true once the check has been handled (no further polling needed).
		/// </summary>
		public static bool TryAnnounceResult() {
			if (_result == null) return false;

			if (_result == "up-to-date") {
				Speech.Say(Strings.Get("util.update_checker.up_to_date"), interrupt: false);
			} else if (_result == "update-available") {
				HandleUpdateAvailable();
			} else {
				// error - log but don't bother the user
				UnityEngine.Debug.Log($"[ATSAccessibility] Update check failed: {_result}");
			}

			return true;
		}

		/// <summary>
		/// On an available update, hand off to the local installer (which downloads,
		/// verifies, and installs in place). If no local installer copy exists — e.g.
		/// the user set the mod up manually before the installer existed — open the
		/// installer download instead so they can migrate.
		/// </summary>
		private static void HandleUpdateAvailable() {
			string gameRoot = GetGameRoot();
			string installerPath = gameRoot != null ? Path.Combine(gameRoot, INSTALLER_EXE_NAME) : null;

			if (installerPath != null && File.Exists(installerPath)) {
				Speech.Say(Strings.Get("util.update_checker.launching_installer"), interrupt: false);
				LaunchInstaller(installerPath, gameRoot);
			} else {
				Speech.Say(Strings.Get("util.update_checker.get_installer"), interrupt: false);
				OpenUrl(INSTALLER_DOWNLOAD_URL);
			}
		}

		private static void LaunchInstaller(string installerPath, string gameRoot) {
			try {
				string lang = LocalizationReflection.GetCurrentLocaCode();
				if (string.IsNullOrEmpty(lang)) lang = "en";
				string dir = gameRoot.TrimEnd('\\', '/');

				Process.Start(new ProcessStartInfo {
					FileName = installerPath,
					Arguments = $"--update --game-dir \"{dir}\" --lang {lang}",
					// UseShellExecute is required so the installer's
					// requireAdministrator manifest triggers a UAC elevation prompt.
					UseShellExecute = true
				});
			} catch (Exception ex) {
				UnityEngine.Debug.Log($"[ATSAccessibility] Failed to launch installer: {ex.Message}. Opening download page instead.");
				OpenUrl(INSTALLER_DOWNLOAD_URL);
			}
		}

		private static void OpenUrl(string url) {
			try {
				Process.Start(new ProcessStartInfo {
					FileName = url,
					UseShellExecute = true
				});
			} catch (Exception ex) {
				UnityEngine.Debug.Log($"[ATSAccessibility] Failed to open URL {url}: {ex.Message}");
			}
		}

		/// <summary>
		/// The game root (where Against the Storm.exe lives). For a Unity game the
		/// process base directory is the game root.
		/// </summary>
		private static string GetGameRoot() {
			try {
				string baseDir = AppDomain.CurrentDomain.BaseDirectory;
				if (!string.IsNullOrEmpty(baseDir)) return baseDir;
			} catch (Exception ex) {
				UnityEngine.Debug.Log($"[ATSAccessibility] Could not resolve game root: {ex.Message}");
			}
			return null;
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
