using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to Daily Challenge popup internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class DailyExpeditionReflection {
		// Compiled regex for time pattern extraction
		private static readonly Regex TimePattern = new Regex(@"\d{2}:\d{2}:\d{2}", RegexOptions.Compiled);

		// Type info
		private static Type _dailyChallengePopupType = null;
		private static Type _dailyChallengeDataType = null;
		private static Type _dailyDifficultyPickerType = null;
		private static Type _difficultyModelType = null;
		private static Type _metaCurrencyType = null;
		private static Type _goodStructType = null;

		// DailyChallengePopup fields - UI elements for direct reading
		private static FieldInfo _popupBiomeTextField = null;
		private static FieldInfo _popupTimeLeftField = null;
		private static FieldInfo _popupChallangeField = null;
		private static FieldInfo _popupDifficultyPickerField = null;
		private static FieldInfo _popupDifficultyField = null;
		private static FieldInfo _popupEmbarkButtonField = null;
		private static FieldInfo _popupCompletedMarkerField = null;

		// TMP_Text.text property
		private static PropertyInfo _tmpTextProperty = null;

		// DailyChallengeData fields
		private static FieldInfo _dataBiomeField = null;
		private static FieldInfo _dataInitialVillagersField = null;
		private static FieldInfo _dataEmbarkGoodsField = null;
		private static FieldInfo _dataEmbarkEffectsField = null;
		private static FieldInfo _dataEarlyModifiersField = null;
		private static FieldInfo _dataLateModifiersField = null;
		private static FieldInfo _dataBaseRewardsField = null;

		// DifficultyPicker methods/fields
		private static MethodInfo _pickerGetDifficultiesMethod = null;
		private static MethodInfo _pickerGetPickedDifficultyMethod = null;
		private static MethodInfo _pickerSetDifficultyMethod = null;

		// DifficultyModel fields
		private static FieldInfo _dmPositiveEffectsField = null;
		private static FieldInfo _dmNegativeEffectsField = null;
		private static FieldInfo _dmEffectsMagnitudeField = null;
		private static FieldInfo _dmIndexField = null;
		private static MethodInfo _dmGetDisplayNameMethod = null;

		// MetaCurrency struct fields
		private static FieldInfo _mcNameField = null;
		private static FieldInfo _mcAmountField = null;

		// Good struct fields
		private static FieldInfo _goodNameField = null;
		private static FieldInfo _goodAmountField = null;

		// MetaCurrencyModel access
		private static MethodInfo _settingsGetMetaCurrencyMethod = null;
		private static PropertyInfo _mcModelDisplayNameProperty = null;

		// DailyService access
		private static PropertyInfo _msDailyServiceProperty = null;
		private static MethodInfo _dailyIsCompletedTodayMethod = null;
		private static MethodInfo _dailyGetRewardsForMethod = null;

		// Popup.Hide method
		private static MethodInfo _popupHideMethod = null;

		private static bool _typesCached = false;

		private static void EnsureTypes() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("DailyExpeditionReflection", assembly => {
				// Cache DailyChallengePopup type and fields
				_dailyChallengePopupType = assembly.GetType("Eremite.WorldMap.UI.DailyChallengePopup");
				if (_dailyChallengePopupType != null) {
					_popupBiomeTextField = _dailyChallengePopupType.GetField("biomeText",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupTimeLeftField = _dailyChallengePopupType.GetField("timeLeft",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupChallangeField = _dailyChallengePopupType.GetField("challange",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupDifficultyPickerField = _dailyChallengePopupType.GetField("difficultyPicker",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupDifficultyField = _dailyChallengePopupType.GetField("difficulty",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupEmbarkButtonField = _dailyChallengePopupType.GetField("embarkButton",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_popupCompletedMarkerField = _dailyChallengePopupType.GetField("completedMarker",
						BindingFlags.NonPublic | BindingFlags.Instance);
				}

				// Cache TMP_Text.text property
				var tmpTextType = typeof(TMPro.TMP_Text);
				_tmpTextProperty = tmpTextType.GetProperty("text",
					BindingFlags.Public | BindingFlags.Instance);

				// Cache DailyChallengeData type and fields
				_dailyChallengeDataType = assembly.GetType("Eremite.WorldMap.DailyChallengeData");
				if (_dailyChallengeDataType != null) {
					_dataBiomeField = _dailyChallengeDataType.GetField("biome",
						BindingFlags.Public | BindingFlags.Instance);
					_dataInitialVillagersField = _dailyChallengeDataType.GetField("initialVillagers",
						BindingFlags.Public | BindingFlags.Instance);
					_dataEmbarkGoodsField = _dailyChallengeDataType.GetField("embarkGoods",
						BindingFlags.Public | BindingFlags.Instance);
					_dataEmbarkEffectsField = _dailyChallengeDataType.GetField("embarkEffects",
						BindingFlags.Public | BindingFlags.Instance);
					_dataEarlyModifiersField = _dailyChallengeDataType.GetField("earlyModifiers",
						BindingFlags.Public | BindingFlags.Instance);
					_dataLateModifiersField = _dailyChallengeDataType.GetField("lateModifiers",
						BindingFlags.Public | BindingFlags.Instance);
					_dataBaseRewardsField = _dailyChallengeDataType.GetField("baseRewards",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache DailyDifficultyPicker type (inherits from DifficultyPicker)
				_dailyDifficultyPickerType = assembly.GetType("Eremite.WorldMap.UI.DailyDifficultyPicker");
				var difficultyPickerBaseType = assembly.GetType("Eremite.WorldMap.UI.DifficultyPicker");
				if (difficultyPickerBaseType != null) {
					_pickerGetDifficultiesMethod = difficultyPickerBaseType.GetMethod("GetDifficulties",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_pickerGetPickedDifficultyMethod = difficultyPickerBaseType.GetMethod("GetPickedDifficulty",
						BindingFlags.Public | BindingFlags.Instance);
					_pickerSetDifficultyMethod = difficultyPickerBaseType.GetMethod("SetDifficulty",
						BindingFlags.NonPublic | BindingFlags.Instance);
				}

				// Cache DifficultyModel type and fields
				_difficultyModelType = assembly.GetType("Eremite.Model.DifficultyModel");
				if (_difficultyModelType != null) {
					_dmPositiveEffectsField = _difficultyModelType.GetField("positiveEffects",
						BindingFlags.Public | BindingFlags.Instance);
					_dmNegativeEffectsField = _difficultyModelType.GetField("negativeEffects",
						BindingFlags.Public | BindingFlags.Instance);
					_dmEffectsMagnitudeField = _difficultyModelType.GetField("effectsMagnitude",
						BindingFlags.Public | BindingFlags.Instance);
					_dmIndexField = _difficultyModelType.GetField("index",
						BindingFlags.Public | BindingFlags.Instance);
					_dmGetDisplayNameMethod = _difficultyModelType.GetMethod("GetDisplayName",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache Settings.GetMetaCurrency method
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetMetaCurrencyMethod = settingsType.GetMethod("GetMetaCurrency",
						BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
				}

				// Cache MetaCurrency struct fields
				_metaCurrencyType = assembly.GetType("Eremite.Model.MetaCurrency");
				if (_metaCurrencyType != null) {
					_mcNameField = _metaCurrencyType.GetField("name",
						BindingFlags.Public | BindingFlags.Instance);
					_mcAmountField = _metaCurrencyType.GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache Good struct fields
				_goodStructType = assembly.GetType("Eremite.Model.Good");
				if (_goodStructType != null) {
					_goodNameField = _goodStructType.GetField("name",
						BindingFlags.Public | BindingFlags.Instance);
					_goodAmountField = _goodStructType.GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache MetaCurrencyModel.DisplayName
				var currencyModelType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
				if (currencyModelType != null) {
					_mcModelDisplayNameProperty = currencyModelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache DailyService access
				var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
				if (metaServicesType != null) {
					_msDailyServiceProperty = metaServicesType.GetProperty("DailyService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				var dailyServiceType = assembly.GetType("Eremite.Services.IDailyService");
				if (dailyServiceType != null) {
					_dailyIsCompletedTodayMethod = dailyServiceType.GetMethod("IsCompletedToday",
						BindingFlags.Public | BindingFlags.Instance);
					_dailyGetRewardsForMethod = dailyServiceType.GetMethod("GetRewardsFor",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache Popup.Hide method
				var popupType = assembly.GetType("Eremite.View.Popups.Popup");
				if (popupType != null) {
					_popupHideMethod = popupType.GetMethod("Hide",
						BindingFlags.Public | BindingFlags.Instance);
				}
			});
		}

		// ========================================
		// DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a DailyChallengePopup.
		/// </summary>
		public static bool IsDailyChallengePopup(object popup) {
			if (popup == null) return false;
			EnsureTypes();
			return _dailyChallengePopupType != null && _dailyChallengePopupType.IsInstanceOfType(popup);
		}

		/// <summary>
		/// Find the active DailyChallengePopup instance.
		/// </summary>
		public static object FindDailyChallengePopup() {
			var topPopup = GameReflection.GetTopActivePopup();
			if (topPopup != null && IsDailyChallengePopup(topPopup))
				return topPopup;
			return null;
		}

		// ========================================
		// DATA ACCESS - Read from UI fields
		// ========================================

		/// <summary>
		/// Get the challenge data from a DailyChallengePopup.
		/// </summary>
		public static object GetChallengeData(object popup) {
			EnsureTypes();
			return ReflectionHelper.GetField(_popupChallangeField, popup);
		}

		/// <summary>
		/// Get the biome display name directly from the popup's biomeText UI element.
		/// </summary>
		public static string GetBiomeName(object popup) {
			EnsureTypes();
			var biomeText = ReflectionHelper.GetField(_popupBiomeTextField, popup);
			string text = ReflectionHelper.GetPropString(_tmpTextProperty, biomeText);
			return !string.IsNullOrEmpty(text) ? text : "Unknown";
		}

		/// <summary>
		/// Calculate fallback time until midnight UTC.
		/// </summary>
		private static string GetFallbackTimeLeft() {
			var now = DateTime.UtcNow;
			var tomorrow = now.Date.AddDays(1);
			var timeLeft = tomorrow - now;
			return timeLeft.ToString(@"hh\:mm\:ss");
		}

		/// <summary>
		/// Get time left directly from the popup's timeLeft UI element.
		/// </summary>
		public static string GetTimeLeft(object popup) {
			EnsureTypes();
			var timeLeftText = ReflectionHelper.GetField(_popupTimeLeftField, popup);
			string text = ReflectionHelper.GetPropString(_tmpTextProperty, timeLeftText);

			// The text may contain localized prefix, try to extract just the time
			if (!string.IsNullOrEmpty(text)) {
				// Look for time pattern (HH:MM:SS)
				var match = TimePattern.Match(text);
				if (match.Success)
					return match.Value;
				return text;
			}

			return GetFallbackTimeLeft();
		}

		/// <summary>
		/// Get the initial villagers (races) list.
		/// </summary>
		public static List<string> GetRaces(object popup) {
			EnsureTypes();
			var result = new List<string>();

			var challenge = GetChallengeData(popup);
			var villagersList = ReflectionHelper.GetList(_dataInitialVillagersField, challenge);
			if (villagersList == null) return result;

			// Get distinct races
			var distinctRaces = new HashSet<string>();
			foreach (var raceName in villagersList) {
				if (raceName is string name && !string.IsNullOrEmpty(name)) {
					distinctRaces.Add(name);
				}
			}

			foreach (var raceName in distinctRaces) {
				string displayName = EmbarkReflection.GetRaceDisplayName(raceName);
				result.Add(displayName);
			}

			return result;
		}

		/// <summary>
		/// Get embark goods as formatted strings "amount displayName".
		/// </summary>
		public static List<string> GetEmbarkGoods(object popup) {
			EnsureTypes();
			var result = new List<string>();

			var challenge = GetChallengeData(popup);
			var goodsList = ReflectionHelper.GetList(_dataEmbarkGoodsField, challenge);
			if (goodsList == null) return result;

			foreach (var good in goodsList) {
				if (good == null) continue;

				string goodName = ReflectionHelper.GetString(_goodNameField, good);
				int amount = ReflectionHelper.GetInt(_goodAmountField, good);

				if (!string.IsNullOrEmpty(goodName) && amount > 0) {
					string displayName = GameReflection.GetGoodDisplayName(goodName);
					result.Add(Strings.Get("common.amount_and_name", amount, displayName));
				}
			}

			return result;
		}

		/// <summary>
		/// Get embark effects as display names.
		/// </summary>
		public static List<string> GetEmbarkEffects(object popup) {
			EnsureTypes();
			var result = new List<string>();

			var challenge = GetChallengeData(popup);
			var effectsList = ReflectionHelper.GetList(_dataEmbarkEffectsField, challenge);
			if (effectsList == null) return result;

			foreach (var effectName in effectsList) {
				if (effectName is string name && !string.IsNullOrEmpty(name)) {
					string displayName = EmbarkReflection.GetEffectDisplayName(name);
					result.Add(displayName);
				}
			}

			return result;
		}

		/// <summary>
		/// Get modifiers (early + late) as display names.
		/// </summary>
		public static List<string> GetModifiers(object popup) {
			var detailed = GetModifiersDetailed(popup);
			var result = new List<string>();
			foreach (var (name, _) in detailed) {
				result.Add(name);
			}
			return result;
		}

		/// <summary>
		/// Get modifiers (early + late) with both display name and description.
		/// Returns list of (displayName, description) tuples.
		/// </summary>
		public static List<(string name, string description)> GetModifiersDetailed(object popup) {
			EnsureTypes();
			var result = new List<(string, string)>();

			var challenge = GetChallengeData(popup);
			if (challenge == null) return result;

			// Get early modifiers
			var earlyList = ReflectionHelper.GetList(_dataEarlyModifiersField, challenge);
			if (earlyList != null) {
				foreach (var modName in earlyList) {
					if (modName is string name && !string.IsNullOrEmpty(name)) {
						var (displayName, description) = GetEffectNameAndDescription(name);
						result.Add((displayName, description));
					}
				}
			}

			// Get late modifiers
			var lateList = ReflectionHelper.GetList(_dataLateModifiersField, challenge);
			if (lateList != null) {
				foreach (var modName in lateList) {
					if (modName is string name && !string.IsNullOrEmpty(name)) {
						var (displayName, description) = GetEffectNameAndDescription(name);
						result.Add((displayName, description));
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Get both display name and description for an effect by its internal name.
		/// </summary>
		private static (string name, string description) GetEffectNameAndDescription(string effectName) {
			if (string.IsNullOrEmpty(effectName))
				return ("Unknown", "");

			var effectModel = GameReflection.GetEffectModel(effectName);
			if (effectModel == null)
				return (effectName, "");

			// Get DisplayName property
			var displayNameProp = effectModel.GetType().GetProperty("DisplayName",
				BindingFlags.Public | BindingFlags.Instance);
			string displayName = ReflectionHelper.GetProp(displayNameProp, effectModel)?.ToString() ?? effectName;

			// Get Description property
			var descProp = effectModel.GetType().GetProperty("Description",
				BindingFlags.Public | BindingFlags.Instance);
			string description = ReflectionHelper.GetProp(descProp, effectModel)?.ToString() ?? "";

			return (displayName, description);
		}

		// ========================================
		// DIFFICULTY ACCESS
		// ========================================

		/// <summary>
		/// Get the difficulty picker from the popup.
		/// </summary>
		private static object GetDifficultyPicker(object popup) {
			EnsureTypes();
			return ReflectionHelper.GetField(_popupDifficultyPickerField, popup);
		}

		/// <summary>
		/// Get the currently picked difficulty from the popup's difficulty field.
		/// </summary>
		public static object GetCurrentDifficulty(object popup) {
			EnsureTypes();

			// First try to get from the popup's difficulty field (already resolved)
			var difficulty = ReflectionHelper.GetField(_popupDifficultyField, popup);
			if (difficulty != null) return difficulty;

			// Fallback to picker
			var picker = GetDifficultyPicker(popup);
			return ReflectionHelper.Invoke(_pickerGetPickedDifficultyMethod, picker);
		}

		/// <summary>
		/// Get the display name for a difficulty.
		/// </summary>
		public static string GetDifficultyDisplayName(object difficulty) {
			EnsureTypes();
			return ReflectionHelper.Invoke(_dmGetDisplayNameMethod, difficulty)?.ToString() ?? "Unknown";
		}

		/// <summary>
		/// Get the index of a difficulty.
		/// </summary>
		public static int GetDifficultyIndex(object difficulty) {
			EnsureTypes();
			if (difficulty == null || _dmIndexField == null) return -1;
			return ReflectionHelper.GetInt(_dmIndexField, difficulty);
		}

		/// <summary>
		/// Get available difficulties from the picker.
		/// </summary>
		public static List<object> GetAvailableDifficulties(object popup) {
			EnsureTypes();
			var result = new List<object>();

			var picker = GetDifficultyPicker(popup);
			var difficultiesList = ReflectionHelper.Invoke(_pickerGetDifficultiesMethod, picker) as IList;
			if (difficultiesList == null) return result;

			foreach (var diff in difficultiesList) {
				if (diff != null)
					result.Add(diff);
			}

			return result;
		}

		/// <summary>
		/// Set the difficulty on the picker.
		/// </summary>
		public static bool SetDifficulty(object popup, object difficulty) {
			EnsureTypes();
			var picker = GetDifficultyPicker(popup);
			return ReflectionHelper.InvokeVoid(_pickerSetDifficultyMethod, picker, difficulty);
		}

		/// <summary>
		/// Get seasonal effects counts (positive, negative) for a difficulty.
		/// </summary>
		public static (int positive, int negative) GetSeasonalEffectsCounts(object difficulty) {
			EnsureTypes();
			if (difficulty == null) return (0, 0);

			int positive = ReflectionHelper.GetInt(_dmPositiveEffectsField, difficulty);
			int negative = ReflectionHelper.GetInt(_dmNegativeEffectsField, difficulty);
			return (positive, negative);
		}

		/// <summary>
		/// Get the effects magnitude text for a difficulty.
		/// </summary>
		public static string GetEffectsMagnitude(object difficulty) {
			EnsureTypes();
			return ReflectionHelper.GetLocaString(_dmEffectsMagnitudeField, difficulty) ?? "";
		}

		// ========================================
		// REWARDS AND COMPLETION
		// ========================================

		/// <summary>
		/// Check if the challenge is completed today for the current difficulty.
		/// Reads from the popup's completedMarker visibility.
		/// </summary>
		public static bool IsCompleted(object popup) {
			EnsureTypes();

			// Try reading from completedMarker.activeSelf
			var marker = ReflectionHelper.GetField(_popupCompletedMarkerField, popup) as GameObject;
			if (marker != null)
				return marker.activeSelf;

			// Fallback to DailyService check
			var difficulty = GetCurrentDifficulty(popup);
			if (difficulty == null) return false;

			var metaServices = GameReflection.GetMetaServices();
			var dailyService = ReflectionHelper.GetProp(_msDailyServiceProperty, metaServices);
			return ReflectionHelper.InvokeBool(_dailyIsCompletedTodayMethod, dailyService, difficulty);
		}

		/// <summary>
		/// Get the meta currency rewards for the current difficulty.
		/// Returns formatted strings "amount displayName".
		/// </summary>
		public static List<string> GetRewards(object popup) {
			EnsureTypes();

			var challenge = GetChallengeData(popup);
			var difficulty = GetCurrentDifficulty(popup);
			if (challenge == null || difficulty == null) return new List<string>();

			// Get base rewards from challenge data
			var baseRewards = ReflectionHelper.GetList(_dataBaseRewardsField, challenge);
			if (baseRewards == null) return new List<string>();

			// Get DailyService.GetRewardsFor
			var metaServices = GameReflection.GetMetaServices();
			var dailyService = ReflectionHelper.GetProp(_msDailyServiceProperty, metaServices);
			var adjustedRewards = ReflectionHelper.Invoke(_dailyGetRewardsForMethod, dailyService,
				difficulty, baseRewards) as IList;

			return FormatRewardsFromList(adjustedRewards ?? baseRewards);
		}

		private static List<string> FormatRewardsFromList(IList rewardsList) {
			var result = new List<string>();
			if (rewardsList == null) return result;

			var settings = GameReflection.GetSettings();

			foreach (var reward in rewardsList) {
				if (reward == null) continue;

				string currencyName = ReflectionHelper.GetString(_mcNameField, reward);
				int amount = ReflectionHelper.GetInt(_mcAmountField, reward);

				if (string.IsNullOrEmpty(currencyName) || amount <= 0) continue;

				// Get display name from MetaCurrencyModel
				string displayName = currencyName;
				var model = ReflectionHelper.Invoke(_settingsGetMetaCurrencyMethod, settings, currencyName);
				if (model != null) {
					displayName = ReflectionHelper.GetProp(_mcModelDisplayNameProperty, model)?.ToString() ?? currencyName;
				}

				result.Add(Strings.Get("common.amount_and_name", amount, displayName));
			}

			return result;
		}

		// ========================================
		// ACTIONS
		// ========================================

		/// <summary>
		/// Trigger the embark action.
		/// </summary>
		public static bool TriggerEmbark(object popup) {
			EnsureTypes();
			var button = ReflectionHelper.GetField(_popupEmbarkButtonField, popup);
			if (button == null) return false;

			// Get the onClick UnityEvent and invoke it
			var onClickProp = button.GetType().GetProperty("onClick",
				BindingFlags.Public | BindingFlags.Instance);
			var onClick = ReflectionHelper.GetProp(onClickProp, button);
			if (onClick == null) return false;

			var invokeMethod = onClick.GetType().GetMethod("Invoke",
				BindingFlags.Public | BindingFlags.Instance);
			return ReflectionHelper.InvokeVoid(invokeMethod, onClick);
		}

		/// <summary>
		/// Hide the popup.
		/// </summary>
		public static bool HidePopup(object popup) {
			EnsureTypes();
			return ReflectionHelper.InvokeVoid(_popupHideMethod, popup);
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(DailyExpeditionReflection), "DailyExpeditionReflection");
		}
	}
}
