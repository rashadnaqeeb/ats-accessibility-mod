using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to the hauler priority settings shown on the
	/// main storage's Haulers tab (HaulersPriorityPanel).
	///
	/// The priorities live on PrefsState (per-settlement), not on the Storage building.
	/// The game's panel mutates those dictionaries in place, so writing to them directly
	/// is equivalent to clicking the plus/minus buttons.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, buildings) - they are destroyed on scene change
	/// </summary>
	public static class HaulersReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		/// <summary>
		/// One building-type row of the haulers panel.
		/// </summary>
		public struct PriorityInfo {
			public object TypeKey;      // BuildingType enum value (dictionary key)
			public string DisplayName;
			public int Priority;
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// IGameServices.StateService -> IStateService.Prefs -> PrefsState
		private static PropertyInfo _gsStateServiceProperty = null;
		private static PropertyInfo _ssPrefsProperty = null;
		private static FieldInfo _prefsHaulProductsField = null;
		private static FieldInfo _prefsHaulIngredientsField = null;
		private static FieldInfo _prefsProductsPrioritiesField = null;
		private static FieldInfo _prefsIngredientsPrioritiesField = null;

		// IGameServices.StorageService -> IStorageService.Main
		private static PropertyInfo _gsStorageServiceProperty = null;
		private static PropertyInfo _storageServiceMainProperty = null;

		// IMetaServices.MetaPerksService -> IMetaPerksService.AreAnyStorageHaulersUnlocked()
		private static PropertyInfo _msMetaPerksServiceProperty = null;
		private static MethodInfo _mpsAreAnyStorageHaulersUnlockedMethod = null;

		// Settings.clientPrefsConfig -> ClientPrefsConfig.GetDisplayNameKeyFor(BuildingType)
		private static FieldInfo _settingsClientPrefsConfigField = null;
		private static MethodInfo _cpcGetDisplayNameKeyForMethod = null;

		// TextsGatekeeper.GetText(string) - resolves a loca key to display text
		private static MethodInfo _getTextMethod = null;

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("HaulersReflection", assembly => {
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
					_gsStorageServiceProperty = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
				}

				var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
				if (stateServiceType != null)
					_ssPrefsProperty = stateServiceType.GetProperty("Prefs", GameReflection.PublicInstance);

				var prefsStateType = assembly.GetType("Eremite.Model.State.PrefsState");
				if (prefsStateType != null) {
					_prefsHaulProductsField = prefsStateType.GetField("haulersHaulProducts", GameReflection.PublicInstance);
					_prefsHaulIngredientsField = prefsStateType.GetField("haulersHaulIngredients", GameReflection.PublicInstance);
					_prefsProductsPrioritiesField = prefsStateType.GetField("haulersProductsPriorities", GameReflection.PublicInstance);
					_prefsIngredientsPrioritiesField = prefsStateType.GetField("haulersIngredientsPriorities", GameReflection.PublicInstance);
				}

				var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
				if (storageServiceType != null)
					_storageServiceMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);

				var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
				if (metaServicesType != null)
					_msMetaPerksServiceProperty = metaServicesType.GetProperty("MetaPerksService", GameReflection.PublicInstance);

				var metaPerksServiceType = assembly.GetType("Eremite.Services.IMetaPerksService");
				if (metaPerksServiceType != null)
					_mpsAreAnyStorageHaulersUnlockedMethod = metaPerksServiceType.GetMethod("AreAnyStorageHaulersUnlocked", GameReflection.PublicInstance);

				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null)
					_settingsClientPrefsConfigField = settingsType.GetField("clientPrefsConfig", GameReflection.PublicInstance);

				var clientPrefsConfigType = assembly.GetType("Eremite.Model.Configs.ClientPrefsConfig");
				if (clientPrefsConfigType != null)
					_cpcGetDisplayNameKeyForMethod = clientPrefsConfigType.GetMethod("GetDisplayNameKeyFor", GameReflection.PublicInstance);

				var textsGatekeeperType = assembly.GetType("Eremite.Services.TextsGatekeeper");
				if (textsGatekeeperType != null)
					_getTextMethod = textsGatekeeperType.GetMethod("GetText", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
			});
		}

		// ========================================
		// SERVICE ACCESS (never cache the results)
		// ========================================

		private static object GetPrefs() {
			var stateService = GameReflection.GetService(_gsStateServiceProperty);
			return ReflectionHelper.GetProp(_ssPrefsProperty, stateService);
		}

		private static object GetMetaPerksService() => GameReflection.GetMetaService(_msMetaPerksServiceProperty);

		private static object GetPrioritiesDict(bool isProduct) {
			return ReflectionHelper.GetField(isProduct ? _prefsProductsPrioritiesField : _prefsIngredientsPrioritiesField, GetPrefs());
		}

		// ========================================
		// AVAILABILITY
		// ========================================

		/// <summary>
		/// Check whether either storage hauler meta perk has been unlocked.
		/// Mirrors the game's own check for showing the Haulers tab.
		/// </summary>
		public static bool AreAnyStorageHaulersUnlocked() {
			EnsureCached();
			return ReflectionHelper.InvokeBool(_mpsAreAnyStorageHaulersUnlockedMethod, GetMetaPerksService());
		}

		/// <summary>
		/// Check whether the given storage building is the settlement's main storage.
		/// The Haulers tab only appears on the main storage.
		/// </summary>
		public static bool IsMainStorage(object building) {
			if (building == null) return false;
			EnsureCached();

			var storageService = GameReflection.GetService(_gsStorageServiceProperty);
			var main = ReflectionHelper.GetProp(_storageServiceMainProperty, storageService);
			return main != null && ReferenceEquals(main, building);
		}

		// ========================================
		// HAUL TOGGLES
		// ========================================

		/// <summary>
		/// Whether haulers currently collect finished products from production buildings.
		/// </summary>
		public static bool GetHaulProducts() {
			EnsureCached();
			return ReflectionHelper.GetBool(_prefsHaulProductsField, GetPrefs());
		}

		/// <summary>
		/// Whether haulers currently deliver ingredients to production buildings.
		/// </summary>
		public static bool GetHaulIngredients() {
			EnsureCached();
			return ReflectionHelper.GetBool(_prefsHaulIngredientsField, GetPrefs());
		}

		/// <summary>
		/// Set the haul-products toggle. Returns false if the field was not found.
		/// </summary>
		public static bool SetHaulProducts(bool value) {
			EnsureCached();
			return ReflectionHelper.SetField(_prefsHaulProductsField, GetPrefs(), value);
		}

		/// <summary>
		/// Set the haul-ingredients toggle. Returns false if the field was not found.
		/// </summary>
		public static bool SetHaulIngredients(bool value) {
			EnsureCached();
			return ReflectionHelper.SetField(_prefsHaulIngredientsField, GetPrefs(), value);
		}

		// ========================================
		// PRIORITIES
		// ========================================

		/// <summary>
		/// Get the priority rows for one group, in the game's dictionary order.
		/// Returns an empty list if the prefs are unavailable.
		/// </summary>
		public static List<PriorityInfo> GetPriorities(bool isProduct) {
			EnsureCached();

			var result = new List<PriorityInfo>();
			var dict = GetPrioritiesDict(isProduct);
			var keys = ReflectionHelper.IterateKeys(dict);
			if (keys == null) return result;

			foreach (var key in keys) {
				result.Add(new PriorityInfo {
					TypeKey = key,
					DisplayName = GetBuildingTypeDisplayName(key),
					Priority = ReflectionHelper.DictGetInt(dict, key)
				});
			}

			return result;
		}

		/// <summary>
		/// Write a priority back into the settlement prefs. The value is clamped to the
		/// game's own input range. Returns false if the dictionary was not found.
		/// </summary>
		public static bool SetPriority(bool isProduct, object typeKey, int value) {
			EnsureCached();
			if (typeKey == null) return false;

			var dict = GetPrioritiesDict(isProduct);
			if (dict == null) return false;

			return ReflectionHelper.DictSet(dict, typeKey, Mathf.Clamp(value, MinPriority, MaxPriority));
		}

		/// <summary>Lowest priority the game's input field accepts.</summary>
		public const int MinPriority = -99;

		/// <summary>Highest priority the game's input field accepts.</summary>
		public const int MaxPriority = 99;

		/// <summary>
		/// Resolve a BuildingType enum value to its localized display name via the
		/// game's own client prefs config. Falls back to the enum name.
		/// </summary>
		private static string GetBuildingTypeDisplayName(object typeKey) {
			try {
				var settings = GameReflection.GetSettings();
				var config = ReflectionHelper.GetField(_settingsClientPrefsConfigField, settings);
				string key = ReflectionHelper.InvokeString(_cpcGetDisplayNameKeyForMethod, config, typeKey);

				if (!string.IsNullOrEmpty(key) && _getTextMethod != null) {
					string text = _getTextMethod.Invoke(null, new object[] { key }) as string;
					if (!string.IsNullOrEmpty(text)) return text;
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingTypeDisplayName failed: {ex.Message}");
			}

			return typeKey?.ToString();
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(HaulersReflection), "HaulersReflection");
		}
	}
}
