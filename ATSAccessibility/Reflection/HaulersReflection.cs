using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

		// HaulersPrioritySlot - one building-type row of the game's panel
		private static Type _slotType = null;
		private static FieldInfo _slotLabelField = null;      // TMP_Text
		private static FieldInfo _slotPrioField = null;       // int
		private static FieldInfo _slotInputField = null;      // TMP_InputField
		private static FieldInfo _slotBlendField = null;      // GameObject, shown when the group toggle is off
		private static FieldInfo _slotBuildingTypeField = null;  // BuildingType, the priority dictionary key
		private static FieldInfo _slotIsProductField = null;     // bool, which of the panel's two dictionaries

		// HaulersPriorityPanel - owns the two dictionaries the slots edit
		private static Type _panelType = null;
		private static FieldInfo _panelProductsPriosField = null;
		private static FieldInfo _panelIngredientsPriosField = null;

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

				_slotType = assembly.GetType("Eremite.Buildings.UI.HaulersPrioritySlot");
				if (_slotType != null) {
					_slotLabelField = _slotType.GetField("label", GameReflection.NonPublicInstance);
					_slotPrioField = _slotType.GetField("prio", GameReflection.NonPublicInstance);
					_slotInputField = _slotType.GetField("input", GameReflection.NonPublicInstance);
					_slotBlendField = _slotType.GetField("blend", GameReflection.NonPublicInstance);
					_slotBuildingTypeField = _slotType.GetField("type", GameReflection.NonPublicInstance);
					_slotIsProductField = _slotType.GetField("isProduct", GameReflection.NonPublicInstance);
				}

				_panelType = assembly.GetType("Eremite.Buildings.UI.HaulersPriorityPanel");
				if (_panelType != null) {
					_panelProductsPriosField = _panelType.GetField("productsPrios", GameReflection.NonPublicInstance);
					_panelIngredientsPriosField = _panelType.GetField("ingredientsPrios", GameReflection.NonPublicInstance);
				}
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

				if (!string.IsNullOrEmpty(key)) {
					string text = GameReflection.ResolveLocaKey(key);
					if (!string.IsNullOrEmpty(text) && text != key) return text;
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingTypeDisplayName failed: {ex.Message}");
			}

			return typeKey?.ToString();
		}

		// ========================================
		// HAULERS PRIORITY SLOT (options menu / generic UI navigation)
		// ========================================

		/// <summary>
		/// Get the HaulersPrioritySlot component owning this element, or null if the
		/// element is not part of a priority row. The game's row is three Selectables
		/// (minus button, input field, plus button) under one slot component.
		///
		/// Searches inactive parents too: HaulersPriorityPanel.HideRest deactivates the
		/// slot root of surplus pooled rows while leaving the child Selectables active,
		/// so a default GetComponentInParent would miss them.
		/// </summary>
		public static Component GetSlotFor(Selectable element) {
			if (element == null) return null;
			EnsureCached();
			if (_slotType == null) return null;

			return element.GetComponentInParent(_slotType, true);
		}

		/// <summary>
		/// Whether this slot is a live row rather than a hidden pooled one.
		/// </summary>
		public static bool IsSlotVisible(Component slot) {
			return slot != null && slot.gameObject.activeInHierarchy;
		}

		/// <summary>
		/// Whether this element is the slot's input field - the one Selectable we keep
		/// as the row's representative when collapsing it for navigation.
		/// </summary>
		public static bool IsSlotRepresentative(Component slot, Selectable element) {
			if (slot == null || element == null) return false;
			return ReferenceEquals(ReflectionHelper.GetField(_slotInputField, slot), element);
		}

		/// <summary>
		/// Whether the slot is greyed out because its group's haul toggle is off.
		/// </summary>
		public static bool IsSlotInactive(Component slot) {
			var blend = ReflectionHelper.GetField(_slotBlendField, slot) as GameObject;
			return blend != null && blend.activeSelf;
		}

		/// <summary>
		/// Read a slot's building-type label and current priority.
		/// </summary>
		public static bool TryGetSlotValues(Component slot, out string label, out int priority) {
			label = null;
			priority = 0;
			if (slot == null) return false;

			var labelText = ReflectionHelper.GetField(_slotLabelField, slot) as TMP_Text;
			label = labelText != null ? labelText.text : null;
			priority = ReflectionHelper.GetInt(_slotPrioField, slot);

			return !string.IsNullOrEmpty(label);
		}

		/// <summary>
		/// Change a slot's priority by delta, clamped to the game's range.
		/// Returns the new priority, or the old one if nothing changed.
		///
		/// Writes straight into the dictionary the owning panel was set up with - the
		/// settlement's PrefsState in the warehouse, the client-prefs defaults in the
		/// options menu - which is exactly what HaulersPriorityPanel.ChangePrio does.
		/// Driving the slot's input field instead would only work on rows that have been
		/// active at least once, since the game registers its onValueChanged listener in
		/// Start(), and the options menu keeps its haulers rows inactive.
		/// </summary>
		public static int AdjustSlot(Component slot, int delta) {
			if (slot == null) return 0;

			int priority = ReflectionHelper.GetInt(_slotPrioField, slot);
			int newPriority = Mathf.Clamp(priority + delta, MinPriority, MaxPriority);
			if (newPriority == priority) return priority;

			var dict = GetSlotDictionary(slot);
			var key = ReflectionHelper.GetField(_slotBuildingTypeField, slot);
			if (dict == null || key == null) return priority;

			if (!ReflectionHelper.DictSet(dict, key, newPriority)) return priority;

			// Keep the slot and its visible text in step with the dictionary. The panel
			// would normally do this in Rebuild().
			ReflectionHelper.SetField(_slotPrioField, slot, newPriority);
			var input = ReflectionHelper.GetField(_slotInputField, slot) as TMP_InputField;
			input?.SetTextWithoutNotify(newPriority.ToString());

			return newPriority;
		}

		/// <summary>
		/// Get the priority dictionary this slot edits, taken from its owning panel so it
		/// is correct in both the warehouse and the options menu.
		/// </summary>
		private static object GetSlotDictionary(Component slot) {
			if (_panelType == null) return null;

			var panel = slot.GetComponentInParent(_panelType, true);
			if (panel == null) return null;

			bool isProduct = ReflectionHelper.GetBool(_slotIsProductField, slot);
			return ReflectionHelper.GetField(isProduct ? _panelProductsPriosField : _panelIngredientsPriosField, panel);
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(HaulersReflection), "HaulersReflection");
		}
	}
}
