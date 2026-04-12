using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to newcomers popup internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// </summary>
	public static class NewcomersReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// NewcomersPopup type check
		private static Type _newcomersPopupType = null;

		// INewcomersService.PickGroup method
		private static MethodInfo _nsPickGroupMethod = null;

		// IGameServices.NewcomersService property (duplicated from RewardsReflection for independence)
		private static PropertyInfo _gsNewcomersServiceProperty = null;

		// INewcomersService.GetCurrentNewcomers method
		private static MethodInfo _nsGetCurrentNewcomersMethod = null;

		// NewcomersGroup fields
		private static FieldInfo _ngRacesField = null;
		private static FieldInfo _ngGoodsField = null;

		// Good struct fields
		private static FieldInfo _goodNameField = null;
		private static FieldInfo _goodAmountField = null;

		// Popup.Hide method
		private static MethodInfo _popupHideMethod = null;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("NewcomersReflection", assembly => {
				// NewcomersPopup type
				_newcomersPopupType = assembly.GetType("Eremite.View.HUD.NewcomersPopup");

				// IGameServices.NewcomersService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsNewcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");
				}

				// INewcomersService methods
				var nsType = assembly.GetType("Eremite.Services.INewcomersService");
				if (nsType != null) {
					_nsPickGroupMethod = nsType.GetMethod("PickGroup");
					_nsGetCurrentNewcomersMethod = nsType.GetMethod("GetCurrentNewcomers");
				}

				// NewcomersGroup fields
				var ngType = assembly.GetType("Eremite.Model.State.NewcomersGroup");
				if (ngType != null) {
					_ngRacesField = ngType.GetField("races", GameReflection.PublicInstance);
					_ngGoodsField = ngType.GetField("goods", GameReflection.PublicInstance);
				}

				// Good struct fields
				var goodType = assembly.GetType("Eremite.Model.Good");
				if (goodType != null) {
					_goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
					_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
				}

				// Popup.Hide method
				var popupType = assembly.GetType("Eremite.View.Popups.Popup");
				if (popupType != null) {
					_popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// TYPE DETECTION
		// ========================================

		/// <summary>
		/// Check if a popup object is a NewcomersPopup.
		/// </summary>
		public static bool IsNewcomersPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			if (_newcomersPopupType == null) return false;
			return _newcomersPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// GROUP ACCESS
		// ========================================

		/// <summary>
		/// Get the current newcomers groups from the service.
		/// Returns null if service is unavailable or no newcomers waiting.
		/// </summary>
		public static IList GetNewcomersGroups() {
			EnsureCached();

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return null;

			var newcomersService = ReflectionHelper.GetProp(_gsNewcomersServiceProperty, gameServices);
			if (newcomersService == null) return null;

			return ReflectionHelper.Invoke(_nsGetCurrentNewcomersMethod, newcomersService) as IList;
		}

		// ========================================
		// PICKING
		// ========================================

		/// <summary>
		/// Pick a newcomers group and hide the popup.
		/// </summary>
		public static bool PickGroup(object popup, object group) {
			if (group == null) return false;
			EnsureCached();

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return false;

			var newcomersService = ReflectionHelper.GetProp(_gsNewcomersServiceProperty, gameServices);
			if (newcomersService == null) return false;

			if (!ReflectionHelper.InvokeVoid(_nsPickGroupMethod, newcomersService, group)) return false;

			// Hide the popup (mirrors NewcomersPopup.OnGroupPicked behavior)
			if (popup != null) {
				ReflectionHelper.InvokeVoid(_popupHideMethod, popup);
			}

			return true;
		}

		// ========================================
		// GROUP FORMATTING
		// ========================================

		/// <summary>
		/// Format a newcomers group as an announcement string.
		/// Format: "3 Humans, 2 Beavers. Bonus: 5 Planks, 3 Mushrooms"
		/// </summary>
		public static string FormatGroup(object group) {
			if (group == null) return Strings.Get("reflection.newcomers.unknown_group");
			EnsureCached();

			var parts = new List<string>();

			// Read races dictionary
			var racesDict = ReflectionHelper.GetField(_ngRacesField, group);
			if (racesDict != null) {
				var keys = ReflectionHelper.IterateKeys(racesDict);
				if (keys != null) {
					foreach (var key in keys) {
						var raceName = key as string;
						if (string.IsNullOrEmpty(raceName)) continue;

						int count = ReflectionHelper.DictGetInt(racesDict, key);

						var displayName = EmbarkReflection.GetRaceDisplayName(raceName);
						parts.Add(Strings.Get("reflection.newcomers.race_entry", count, displayName));
					}
				}
			}

			string raceText = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : Strings.Get("reflection.newcomers.no_villagers");

			// Read goods list
			var goodsList = ReflectionHelper.GetList(_ngGoodsField, group);
			if (goodsList != null && goodsList.Count > 0) {
				var goodParts = new List<string>();

				foreach (var good in goodsList) {
					if (good == null) continue;

					var name = ReflectionHelper.GetString(_goodNameField, good) ?? "";
					int amount = ReflectionHelper.GetInt(_goodAmountField, good);

					if (amount <= 0 || string.IsNullOrEmpty(name)) continue;

					var displayName = GameReflection.GetGoodDisplayName(name);
					goodParts.Add(Strings.Get("reflection.newcomers.good_entry", amount, displayName));
				}

				if (goodParts.Count > 0) {
					return Strings.Get("reflection.newcomers.group_with_bonus", raceText, string.Join(", ", goodParts.ToArray()));
				}
			}

			return raceText;
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(NewcomersReflection), "NewcomersReflection");
		}
	}
}
