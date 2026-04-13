using ATSAccessibility.Reflection;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Resolves localized singular/plural race names through mod-shipped keys
	/// (<c>common.race.&lt;id&gt;_singular</c> / <c>_plural</c>) so non-English
	/// plurals don't depend on English <c>-s</c> inflection. Falls back to the
	/// game's singular display name for unknown race IDs (e.g. modded or
	/// future additions).
	/// </summary>
	public static class RaceNameHelper {
		public static string GetRaceName(string raceId, bool plural) {
			if (string.IsNullOrEmpty(raceId)) return Strings.Get("common.unknown");
			string suffix = plural ? "_plural" : "_singular";
			string key = "common.race." + raceId.ToLowerInvariant() + suffix;
			string localized = Strings.GetOrNull(key);
			if (!string.IsNullOrEmpty(localized)) return localized;
			return EmbarkReflection.GetRaceDisplayName(raceId);
		}
	}
}
