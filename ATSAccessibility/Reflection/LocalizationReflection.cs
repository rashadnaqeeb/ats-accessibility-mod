using System.Reflection;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reads the game's current language from TextsService.CurrentLocaCode.
	/// Used by the mod's own Strings table to pick the matching translation.
	/// </summary>
	public static class LocalizationReflection {
		private static PropertyInfo _currentLocaCodeProperty;
		private static bool _cached;

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("LocalizationReflection", assembly => {
				var type = assembly.GetType("Eremite.Services.ITextsService");
				if (type != null) {
					_currentLocaCodeProperty = type.GetProperty("CurrentLocaCode",
						BindingFlags.Public | BindingFlags.Instance);
				}
			});
		}

		/// <summary>
		/// Get the game's current language code (e.g. "English", "German") or null if unavailable.
		/// The game uses full-name codes, not ISO codes — see LocaConfig for the list.
		/// </summary>
		public static string GetCurrentLocaCode() {
			EnsureCached();
			if (_currentLocaCodeProperty == null) return null;

			var appServices = GameReflection.GetAppServices();
			if (appServices == null) return null;

			var textsServiceProp = appServices.GetType().GetProperty("TextsService",
				BindingFlags.Public | BindingFlags.Instance);
			var textsService = textsServiceProp?.GetValue(appServices);
			if (textsService == null) return null;

			return ReflectionHelper.GetProp(_currentLocaCodeProperty, textsService) as string;
		}
	}
}
