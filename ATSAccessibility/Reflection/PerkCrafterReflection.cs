using ATSAccessibility.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing PerkCrafterPopup (Cornerstone Forge) data and interaction.
	/// </summary>
	public static class PerkCrafterReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		public class HookOption {
			public object TierState;      // TierState reference for selection
			public string DisplayName;    // Hook name
			public string Description;    // Hook description
			public int Index;             // Index in the hooks array
		}

		public class EffectOption {
			public object TierState;      // TierState reference for selection
			public string Description;    // Effect description (used as display text)
			public bool IsPositive;       // True for positive, false for negative
			public int Index;             // Index in the effects array
		}

		public class CraftedPerkInfo {
			public string Name;           // Effect name
			public string Description;    // Effect description
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// PerkCrafterPopup
		private static Type _popupType;
		private static MethodInfo _popupIsShownMethod;
		private static FieldInfo _popupPerkCrafterField;
		private static FieldInfo _popupDescTextField;
		private static FieldInfo _popupResultsSlotsField;

		// PerkCrafter
		private static Type _perkCrafterType;
		private static FieldInfo _pcStateField;
		private static FieldInfo _pcModelField;
		private static MethodInfo _pcHasUsedAllChargesMethod;
		private static MethodInfo _pcGetUsesLeftMethod;
		private static MethodInfo _pcIsNegativePickedMethod;
		private static MethodInfo _pcChangeHookMethod;
		private static MethodInfo _pcChangePositiveMethod;
		private static MethodInfo _pcChangeNegativeMethod;
		private static MethodInfo _pcCreateCurrentPerkMethod;
		private static MethodInfo _pcChangeNameMethod;
		private static MethodInfo _pcGetResultDisplayNameMethod;
		private static MethodInfo _pcGetCurrentResultMethod;
		private static MethodInfo _pcGetEffectMethod;
		private static MethodInfo _pcGetHookMethod;

		// PerkCrafterState
		private static Type _perkCrafterStateType;
		private static FieldInfo _pcssCraftingField;
		private static FieldInfo _pcssCraftedPerksField;
		private static FieldInfo _pcssResultsField;

		// PerkCraftingState
		private static Type _perkCraftingStateType;
		private static FieldInfo _pcsHooksField;
		private static FieldInfo _pcsPositiveEffectsField;
		private static FieldInfo _pcsNegativeEffectsField;
		private static FieldInfo _pcsPickedHookField;
		private static FieldInfo _pcsPickedPositiveField;
		private static FieldInfo _pcsPickedNegativeField;
		private static FieldInfo _pcsResultNameField;
		private static MethodInfo _pcsGetPickedHookMethod;
		private static MethodInfo _pcsGetPickedPositiveMethod;
		private static MethodInfo _pcsGetPickedNegativeMethod;

		// PerkCrafterModel
		private static Type _perkCrafterModelType;
		private static FieldInfo _pcmChargesField;
		private static FieldInfo _pcmPriceField;
		private static FieldInfo _pcmEffectsElementsField;

		// CraftedEffectElementsContainer
		private static Type _elementsContainerType;
		private static FieldInfo _cecHooksSetsField;
		private static FieldInfo _cecEffectsSetsField;
		private static FieldInfo _cecDisplayNamesField;
		private static MethodInfo _cecGetHookMethod;
		private static MethodInfo _cecGetEffectMethod;

		// HookLogic
		private static Type _hookLogicType;
		private static PropertyInfo _hookDescriptionProperty;

		// EffectModel
		private static Type _effectModelType;
		private static PropertyInfo _emDisplayNameProperty;
		private static PropertyInfo _emDescriptionProperty;
		private static FieldInfo _emIsPositiveField;

		// TierState
		private static Type _tierStateType;
		private static FieldInfo _tsTierIndexField;
		private static FieldInfo _tsSetIndexField;

		// PerkCraftingState.isHookLink
		private static FieldInfo _pcsIsHookLinkField;

		// GoodRef for price
		private static MethodInfo _grToGoodMethod;

		// Storage for price checking
		private static PropertyInfo _gsStorageServiceProperty;
		private static PropertyInfo _ssMainProperty;
		private static MethodInfo _storageGetAmountMethod;
		private static MethodInfo _storageIsAvailableMethod;

		private static bool _typesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypesCached() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("PerkCrafterReflection", assembly => {
				CachePopupTypes(assembly);
				CachePerkCrafterTypes(assembly);
				CacheStateTypes(assembly);
				CacheModelTypes(assembly);
				CacheElementsTypes(assembly);
				CacheEffectTypes(assembly);
				CacheStorageTypes(assembly);
			});
		}

		private static void CachePopupTypes(Assembly assembly) {
			_popupType = assembly.GetType("Eremite.Buildings.UI.PerkCrafters.PerkCrafterPopup");
			if (_popupType != null) {
				// Instance is a static field, accessed directly in GetPopupInstance()
				_popupPerkCrafterField = _popupType.GetField("perkCrafter", GameReflection.NonPublicInstance);
				_popupDescTextField = _popupType.GetField("descText", GameReflection.NonPublicInstance);
				_popupResultsSlotsField = _popupType.GetField("resultsSlots", GameReflection.NonPublicInstance);

				// IsShown() is on base Popup class
				var popupBaseType = assembly.GetType("Eremite.View.Popups.Popup");
				if (popupBaseType != null) {
					_popupIsShownMethod = popupBaseType.GetMethod("IsShown", GameReflection.PublicInstance);
				}
			}
		}

		private static void CachePerkCrafterTypes(Assembly assembly) {
			_perkCrafterType = assembly.GetType("Eremite.Buildings.PerkCrafter");
			if (_perkCrafterType != null) {
				_pcStateField = _perkCrafterType.GetField("state", GameReflection.PublicInstance);
				_pcModelField = _perkCrafterType.GetField("model", GameReflection.PublicInstance);
				_pcHasUsedAllChargesMethod = _perkCrafterType.GetMethod("HasUsedAllCharges", GameReflection.PublicInstance);
				_pcGetUsesLeftMethod = _perkCrafterType.GetMethod("GetUsesLeft", GameReflection.PublicInstance);
				_pcIsNegativePickedMethod = _perkCrafterType.GetMethod("IsNegativePicked", GameReflection.PublicInstance);
				_pcChangeHookMethod = _perkCrafterType.GetMethod("ChangeHook", GameReflection.PublicInstance);
				_pcChangePositiveMethod = _perkCrafterType.GetMethod("ChangePositive", GameReflection.PublicInstance);
				_pcChangeNegativeMethod = _perkCrafterType.GetMethod("ChangeNegative", GameReflection.PublicInstance);
				_pcCreateCurrentPerkMethod = _perkCrafterType.GetMethod("CreateCurrentPerk", GameReflection.PublicInstance);
				_pcChangeNameMethod = _perkCrafterType.GetMethod("ChangeName", GameReflection.PublicInstance);
				_pcGetResultDisplayNameMethod = _perkCrafterType.GetMethod("GetResultDisplayName", GameReflection.PublicInstance);
				_pcGetCurrentResultMethod = _perkCrafterType.GetMethod("GetCurrentResult", GameReflection.PublicInstance);
				_pcGetEffectMethod = _perkCrafterType.GetMethod("GetEffect", GameReflection.PublicInstance);
				_pcGetHookMethod = _perkCrafterType.GetMethod("GetHook", GameReflection.PublicInstance);
			}
		}

		private static void CacheStateTypes(Assembly assembly) {
			_perkCrafterStateType = assembly.GetType("Eremite.Buildings.PerkCrafterState");
			if (_perkCrafterStateType != null) {
				_pcssCraftingField = _perkCrafterStateType.GetField("crafting", GameReflection.PublicInstance);
				_pcssCraftedPerksField = _perkCrafterStateType.GetField("craftedPerks", GameReflection.PublicInstance);
				_pcssResultsField = _perkCrafterStateType.GetField("results", GameReflection.PublicInstance);
			}

			_perkCraftingStateType = assembly.GetType("Eremite.Buildings.PerkCraftingState");
			if (_perkCraftingStateType != null) {
				_pcsHooksField = _perkCraftingStateType.GetField("hooks", GameReflection.PublicInstance);
				_pcsPositiveEffectsField = _perkCraftingStateType.GetField("positiveEffects", GameReflection.PublicInstance);
				_pcsNegativeEffectsField = _perkCraftingStateType.GetField("negativeEffects", GameReflection.PublicInstance);
				_pcsPickedHookField = _perkCraftingStateType.GetField("pickedHook", GameReflection.PublicInstance);
				_pcsPickedPositiveField = _perkCraftingStateType.GetField("pickedPositive", GameReflection.PublicInstance);
				_pcsPickedNegativeField = _perkCraftingStateType.GetField("pickedNegative", GameReflection.PublicInstance);
				_pcsResultNameField = _perkCraftingStateType.GetField("resultName", GameReflection.PublicInstance);
				_pcsGetPickedHookMethod = _perkCraftingStateType.GetMethod("GetPickedHook", GameReflection.PublicInstance);
				_pcsGetPickedPositiveMethod = _perkCraftingStateType.GetMethod("GetPickedPositive", GameReflection.PublicInstance);
				_pcsGetPickedNegativeMethod = _perkCraftingStateType.GetMethod("GetPickedNegative", GameReflection.PublicInstance);
			}

			_tierStateType = assembly.GetType("Eremite.Model.Effects.TierState");
			if (_tierStateType != null) {
				_tsTierIndexField = _tierStateType.GetField("tierIndex", GameReflection.PublicInstance);
				_tsSetIndexField = _tierStateType.GetField("setIndex", GameReflection.PublicInstance);
			}

			if (_perkCraftingStateType != null) {
				_pcsIsHookLinkField = _perkCraftingStateType.GetField("isHookLink", GameReflection.PublicInstance);
			}
		}

		private static void CacheModelTypes(Assembly assembly) {
			_perkCrafterModelType = assembly.GetType("Eremite.Buildings.PerkCrafterModel");
			if (_perkCrafterModelType != null) {
				_pcmChargesField = _perkCrafterModelType.GetField("charges", GameReflection.PublicInstance);
				_pcmPriceField = _perkCrafterModelType.GetField("price", GameReflection.PublicInstance);
				_pcmEffectsElementsField = _perkCrafterModelType.GetField("effectsElements", GameReflection.PublicInstance);
			}

			var goodRefType = GameReflection.GoodRefType;
			if (goodRefType != null) {
				_grToGoodMethod = goodRefType.GetMethod("ToGood", GameReflection.PublicInstance);
			}
		}

		private static void CacheElementsTypes(Assembly assembly) {
			_elementsContainerType = assembly.GetType("Eremite.Model.Effects.CraftedEffectElementsContainer");
			if (_elementsContainerType != null) {
				_cecHooksSetsField = _elementsContainerType.GetField("hooksSets", GameReflection.PublicInstance);
				_cecEffectsSetsField = _elementsContainerType.GetField("effectsSets", GameReflection.PublicInstance);
				_cecDisplayNamesField = _elementsContainerType.GetField("displayNames", GameReflection.PublicInstance);
				_cecGetHookMethod = _elementsContainerType.GetMethod("GetHook", GameReflection.PublicInstance);
				_cecGetEffectMethod = _elementsContainerType.GetMethod("GetEffect", GameReflection.PublicInstance);
			}
		}

		private static void CacheEffectTypes(Assembly assembly) {
			_hookLogicType = assembly.GetType("Eremite.Model.Effects.HookLogic");
			if (_hookLogicType != null) {
				_hookDescriptionProperty = _hookLogicType.GetProperty("Description", GameReflection.PublicInstance);
			}

			_effectModelType = assembly.GetType("Eremite.Model.EffectModel");
			if (_effectModelType != null) {
				_emDisplayNameProperty = _effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
				_emDescriptionProperty = _effectModelType.GetProperty("Description", GameReflection.PublicInstance);
				_emIsPositiveField = _effectModelType.GetField("isPositive", GameReflection.PublicInstance);
			}
		}

		private static void CacheStorageTypes(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_gsStorageServiceProperty = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
			}

			var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
			if (storageServiceType != null) {
				_ssMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
			}

			var storageType = assembly.GetType("Eremite.Buildings.Storage");
			if (storageType != null) {
				_storageGetAmountMethod = storageType.GetMethod("GetAmount", new[] { typeof(string) });
				// IsAvailable takes a GoodRef
				var grType = GameReflection.GoodRefType;
				if (grType != null) {
					_storageIsAvailableMethod = storageType.GetMethod("IsAvailable", new[] { grType });
				}
			}
		}

		// ========================================
		// POPUP ACCESS
		// ========================================

		/// <summary>
		/// Get the PerkCrafterPopup.Instance (static field).
		/// </summary>
		public static object GetPopupInstance() {
			EnsureTypesCached();
			if (_popupType == null) return null;

			try {
				var instanceField = _popupType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
				return instanceField?.GetValue(null);
			} catch { return null; }
		}

		/// <summary>
		/// Check if the PerkCrafterPopup is currently shown.
		/// </summary>
		public static bool IsPopupShown() {
			var popup = GetPopupInstance();
			return ReflectionHelper.InvokeBool(_popupIsShownMethod, popup);
		}

		/// <summary>
		/// Check if the given popup is a PerkCrafterPopup.
		/// </summary>
		public static bool IsPerkCrafterPopup(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return _popupType != null && _popupType.IsInstanceOfType(popup);
		}

		/// <summary>
		/// Get the PerkCrafter from the popup.
		/// </summary>
		private static object GetPerkCrafter() {
			var popup = GetPopupInstance();
			return ReflectionHelper.GetField(_popupPerkCrafterField, popup);
		}

		/// <summary>
		/// Get the PerkCrafterState from the PerkCrafter.
		/// </summary>
		private static object GetState() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.GetField(_pcStateField, crafter);
		}

		/// <summary>
		/// Get the PerkCraftingState (current crafting session).
		/// </summary>
		private static object GetCraftingState() {
			var state = GetState();
			return ReflectionHelper.GetField(_pcssCraftingField, state);
		}

		/// <summary>
		/// Get the PerkCrafterModel.
		/// </summary>
		private static object GetModel() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.GetField(_pcModelField, crafter);
		}

		/// <summary>
		/// Get the CraftedEffectElementsContainer.
		/// </summary>
		private static object GetElementsContainer() {
			var model = GetModel();
			return ReflectionHelper.GetField(_pcmEffectsElementsField, model);
		}

		// ========================================
		// NPC DIALOGUE
		// ========================================

		/// <summary>
		/// Get the NPC dialogue text from the popup.
		/// </summary>
		public static string GetNpcDialogue() {
			var popup = GetPopupInstance();
			var textComponent = ReflectionHelper.GetField(_popupDescTextField, popup);
			if (textComponent == null) return null;

			// TMP_Text has a 'text' property
			var textProperty = textComponent.GetType().GetProperty("text", GameReflection.PublicInstance);
			return ReflectionHelper.GetPropString(textProperty, textComponent);
		}

		// ========================================
		// CRAFTING STATE QUERIES
		// ========================================

		/// <summary>
		/// Check if all charges have been used (finished state).
		/// </summary>
		public static bool HasUsedAllCharges() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeBool(_pcHasUsedAllChargesMethod, crafter);
		}

		/// <summary>
		/// Get the number of crafts remaining.
		/// </summary>
		public static int GetUsesLeft() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeInt(_pcGetUsesLeftMethod, crafter);
		}

		/// <summary>
		/// Get the total number of charges (typically 3).
		/// </summary>
		public static int GetTotalCharges() {
			var model = GetModel();
			if (model == null || _pcmChargesField == null) return 3;
			int val = ReflectionHelper.GetInt(_pcmChargesField, model);
			return val != 0 ? val : 3;
		}

		/// <summary>
		/// Get the number of crafted perks so far.
		/// </summary>
		public static int GetCraftedPerksCount() {
			var state = GetState();
			return ReflectionHelper.GetInt(_pcssCraftedPerksField, state);
		}

		/// <summary>
		/// Check if a negative effect is currently picked.
		/// </summary>
		public static bool IsNegativePicked() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeBool(_pcIsNegativePickedMethod, crafter);
		}

		// ========================================
		// SELECTION INDICES
		// ========================================

		/// <summary>
		/// Get the currently selected hook index.
		/// </summary>
		public static int GetPickedHookIndex() {
			var craftingState = GetCraftingState();
			return ReflectionHelper.GetInt(_pcsPickedHookField, craftingState);
		}

		/// <summary>
		/// Get the currently selected positive effect index.
		/// </summary>
		public static int GetPickedPositiveIndex() {
			var craftingState = GetCraftingState();
			return ReflectionHelper.GetInt(_pcsPickedPositiveField, craftingState);
		}

		/// <summary>
		/// Get the currently selected negative effect index (-1 if none).
		/// </summary>
		public static int GetPickedNegativeIndex() {
			var craftingState = GetCraftingState();
			if (craftingState == null || _pcsPickedNegativeField == null) return -1;
			var val = ReflectionHelper.GetField(_pcsPickedNegativeField, craftingState);
			return val is int i ? i : -1;
		}

		// ========================================
		// HOOK/EFFECT OPTIONS
		// ========================================

		/// <summary>
		/// Get the available hook options for selection.
		/// </summary>
		public static List<HookOption> GetHookOptions() {
			EnsureTypesCached();
			var result = new List<HookOption>();

			var craftingState = GetCraftingState();
			if (craftingState == null || _pcsHooksField == null) return result;

			var crafter = GetPerkCrafter();
			if (crafter == null) return result;

			try {
				var hooks = ReflectionHelper.GetField(_pcsHooksField, craftingState) as Array;
				if (hooks == null) return result;

				for (int i = 0; i < hooks.Length; i++) {
					var tierState = hooks.GetValue(i);
					if (tierState == null) continue;

					// Get HookLogic via perkCrafter.GetHook(tierState)
					var hookLogic = ReflectionHelper.Invoke(_pcGetHookMethod, crafter, tierState);
					if (hookLogic == null) continue;

					string description = ReflectionHelper.GetPropString(_hookDescriptionProperty, hookLogic) ?? "";

					// HookLogic doesn't have a DisplayName, use description as name
					// Extract a short name from the description or use a generic one
					string displayName = GetHookDisplayName(hookLogic, i);

					result.Add(new HookOption {
						TierState = tierState,
						DisplayName = displayName,
						Description = description,
						Index = i
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.GetHookOptions failed: {ex.Message}");
			}

			return result;
		}

		private static string GetHookDisplayName(object hookLogic, int index) {
			// Try to get a reasonable name from the hook
			// HookLogic has a Name property inherited from SO (ScriptableObject)
			var nameProperty = hookLogic.GetType().GetProperty("Name", GameReflection.PublicInstance);
			var name = ReflectionHelper.GetPropString(nameProperty, hookLogic);
			if (!string.IsNullOrEmpty(name)) {
				// Clean up internal names like "Hook_Crafted_Gathering_5"
				return CleanInternalName(name);
			}

			return Strings.Get("reflection.perkcrafter.hook", index + 1);
		}

		private static string CleanInternalName(string name) {
			if (string.IsNullOrEmpty(name)) return name;

			// Remove common prefixes
			name = name.Replace("Hook_Crafted_", "").Replace("Effect_Crafted_", "");
			name = name.Replace("_", " ");

			// Remove trailing numbers that look like IDs
			var parts = name.Split(' ');
			if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _)) {
				name = string.Join(" ", parts, 0, parts.Length - 1);
			}

			return name.Trim();
		}

		/// <summary>
		/// Get the available positive effect options for selection.
		/// </summary>
		public static List<EffectOption> GetPositiveOptions() {
			return GetEffectOptions(_pcsPositiveEffectsField, true);
		}

		/// <summary>
		/// Get the available negative effect options for selection.
		/// </summary>
		public static List<EffectOption> GetNegativeOptions() {
			return GetEffectOptions(_pcsNegativeEffectsField, false);
		}

		private static List<EffectOption> GetEffectOptions(FieldInfo effectsField, bool isPositive) {
			EnsureTypesCached();
			var result = new List<EffectOption>();

			var craftingState = GetCraftingState();
			if (craftingState == null || effectsField == null) return result;

			var crafter = GetPerkCrafter();
			if (crafter == null) return result;

			try {
				var effects = ReflectionHelper.GetField(effectsField, craftingState) as Array;
				if (effects == null) return result;

				for (int i = 0; i < effects.Length; i++) {
					var tierState = effects.GetValue(i);
					if (tierState == null) continue;

					// Get EffectModel via perkCrafter.GetEffect(tierState)
					var effectModel = ReflectionHelper.Invoke(_pcGetEffectMethod, crafter, tierState);
					if (effectModel == null) continue;

					// Use Description as the display text - these effects don't have proper display names
					string description = ReflectionHelper.GetPropString(_emDescriptionProperty, effectModel) ?? $"Effect {i + 1}";

					result.Add(new EffectOption {
						TierState = tierState,
						Description = description,
						IsPositive = isPositive,
						Index = i
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.GetEffectOptions failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// CURRENT SELECTIONS
		// ========================================

		/// <summary>
		/// Get the currently selected hook info.
		/// </summary>
		public static HookOption GetCurrentHook() {
			var options = GetHookOptions();
			int index = GetPickedHookIndex();

			if (index >= 0 && index < options.Count)
				return options[index];

			return null;
		}

		/// <summary>
		/// Get the currently selected positive effect info.
		/// </summary>
		public static EffectOption GetCurrentPositive() {
			var options = GetPositiveOptions();
			int index = GetPickedPositiveIndex();

			if (index >= 0 && index < options.Count)
				return options[index];

			return null;
		}

		/// <summary>
		/// Get the currently selected negative effect info (null if none).
		/// </summary>
		public static EffectOption GetCurrentNegative() {
			int index = GetPickedNegativeIndex();
			if (index < 0) return null;

			var options = GetNegativeOptions();
			if (index < options.Count)
				return options[index];

			return null;
		}

		// ========================================
		// RESULT NAME
		// ========================================

		/// <summary>
		/// Get the current result perk name.
		/// </summary>
		public static string GetResultName() {
			var crafter = GetPerkCrafter();
			var displayName = ReflectionHelper.InvokeString(_pcGetResultDisplayNameMethod, crafter);

			// If it's a localization key, resolve it
			if (!string.IsNullOrEmpty(displayName)) {
				// Try to get the localized text
				var result = ReflectionHelper.Invoke(_pcGetCurrentResultMethod, crafter);
				if (result != null) {
					var resultDisplayName = ReflectionHelper.GetPropString(_emDisplayNameProperty, result);
					if (!string.IsNullOrEmpty(resultDisplayName))
						return resultDisplayName;
				}
			}

			return displayName;
		}

		/// <summary>
		/// Set the result perk name.
		/// </summary>
		public static bool SetResultName(string name) {
			var crafter = GetPerkCrafter();
			// ChangeName(string name, bool isLocalizedName)
			// For custom names, isLocalizedName = false
			return ReflectionHelper.InvokeVoid(_pcChangeNameMethod, crafter, name, (object)false);
		}

		/// <summary>
		/// Randomize the result perk name.
		/// </summary>
		public static bool RandomizeName() {
			var crafter = GetPerkCrafter();
			if (crafter == null || _pcChangeNameMethod == null) return false;

			var elements = GetElementsContainer();
			if (elements == null || _cecDisplayNamesField == null) return false;

			// Get random name from displayNames array
			var displayNames = ReflectionHelper.GetField(_cecDisplayNamesField, elements) as Array;
			if (displayNames == null || displayNames.Length == 0) return false;

			int randomIndex = UnityEngine.Random.Range(0, displayNames.Length);
			var locaText = displayNames.GetValue(randomIndex);

			// Get the key from LocaText
			var keyField = locaText?.GetType().GetField("key", GameReflection.PublicInstance);
			var key = ReflectionHelper.GetString(keyField, locaText);

			if (!string.IsNullOrEmpty(key)) {
				// ChangeName with isLocalizedName = true
				return ReflectionHelper.InvokeVoid(_pcChangeNameMethod, crafter, key, (object)true);
			}

			return false;
		}

		// ========================================
		// PRICE AND CRAFTING
		// ========================================

		/// <summary>
		/// Get the crafting price as (amount, goodDisplayName).
		/// </summary>
		public static (int amount, string goodName) GetPrice() {
			var model = GetModel();
			if (model == null || _pcmPriceField == null) return (0, "Unknown");

			var priceRef = ReflectionHelper.GetField(_pcmPriceField, model);
			if (priceRef == null) return (0, "Unknown");

			int amount = ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, priceRef);
			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, priceRef);
			string goodName = goodModel != null ?
				(GameReflection.GetDisplayName(goodModel) ?? "Unknown") : "Unknown";

			return (amount, goodName);
		}

		/// <summary>
		/// Get the current storage amount of the crafting resource.
		/// </summary>
		public static int GetStorageAmount() {
			var model = GetModel();
			if (model == null || _pcmPriceField == null) return 0;

			var priceRef = ReflectionHelper.GetField(_pcmPriceField, model);
			if (priceRef == null) return 0;

			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, priceRef);
			if (goodModel == null) return 0;

			// Get the good name
			var nameProperty = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
			var goodName = ReflectionHelper.GetPropString(nameProperty, goodModel);
			if (string.IsNullOrEmpty(goodName)) return 0;

			// Access storage
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return 0;

			var storageService = ReflectionHelper.GetProp(_gsStorageServiceProperty, gameServices);
			if (storageService == null) return 0;

			var mainStorage = ReflectionHelper.GetProp(_ssMainProperty, storageService);
			if (mainStorage == null) return 0;

			return ReflectionHelper.InvokeInt(_storageGetAmountMethod, mainStorage, goodName);
		}

		/// <summary>
		/// Check if the player can afford to craft.
		/// </summary>
		public static bool CanAffordCraft() {
			var (amount, _) = GetPrice();
			return GetStorageAmount() >= amount;
		}

		/// <summary>
		/// Perform the craft action.
		/// </summary>
		public static bool PerformCraft() {
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeVoid(_pcCreateCurrentPerkMethod, crafter);
		}

		// ========================================
		// SELECTION ACTIONS
		// ========================================

		/// <summary>
		/// Select a hook option.
		/// </summary>
		public static bool SelectHook(HookOption option) {
			if (option == null) return false;
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeVoid(_pcChangeHookMethod, crafter, option.TierState);
		}

		/// <summary>
		/// Select a positive effect option.
		/// </summary>
		public static bool SelectPositive(EffectOption option) {
			if (option == null) return false;
			var crafter = GetPerkCrafter();
			return ReflectionHelper.InvokeVoid(_pcChangePositiveMethod, crafter, option.TierState);
		}

		/// <summary>
		/// Force a RebuildResult() by calling ChangePositive with the current value.
		/// This ensures the model matches the current crafting state (pickedNeg, etc.)
		/// without changing any selections. Needed because the model is only rebuilt
		/// when a Change* method is called, and a stale model from save may have
		/// outdated description/effects.
		/// </summary>
		public static void ForceRebuildResult() {
			EnsureTypesCached();
			var crafter = GetPerkCrafter();
			if (crafter == null || _pcChangePositiveMethod == null) return;

			var craftingState = GetCraftingState();
			if (craftingState == null) return;

			var pickedPositive = ReflectionHelper.Invoke(_pcsGetPickedPositiveMethod, craftingState);
			if (pickedPositive == null) return;

			ReflectionHelper.InvokeVoid(_pcChangePositiveMethod, crafter, pickedPositive);
		}

		/// <summary>
		/// Select a negative effect option (or clear selection if null).
		/// The game's ChangeNegative mutates tier indices before RebuildResult,
		/// so we must handle failures by rolling back the tier bump.
		/// </summary>
		public static bool SelectNegative(EffectOption option) {
			var crafter = GetPerkCrafter();
			if (crafter == null || _pcChangeNegativeMethod == null) return false;

			// Snapshot whether a negative was picked before, so we know if
			// ChangeNegative will trigger a tier bump via ChangeLinkResult
			bool wasPicked = ReflectionHelper.InvokeBool(_pcIsNegativePickedMethod, crafter);

			// For "None" (clear selection), pass null to ChangeNegative.
			// Array.IndexOf(null) returns -1, which deselects properly
			// and triggers the tier decrement if one was previously picked.
			var tierState = option?.TierState;
			bool success = ReflectionHelper.InvokeVoid(_pcChangeNegativeMethod, crafter, tierState);

			if (!success) {
				// ChangeNegative partially mutated state (set pickedNegative and
				// bumped tier indices) then crashed in RebuildResult.
				// Roll back the tier bump if one occurred.
				bool isPicked = ReflectionHelper.InvokeBool(_pcIsNegativePickedMethod, crafter);
				if (wasPicked != isPicked) {
					// ChangeLinkResult ran — reverse the tier adjustment
					int reverseChange = isPicked ? -1 : 1;
					RollbackTierBump(reverseChange);
				}
				// Also restore pickedNegative to its pre-call state
				var craftingState = GetCraftingState();
				if (wasPicked) {
					// Can't restore the exact old index, but -1 is safer than
					// a partially-set value that may not match the tier state
				}
				ReflectionHelper.SetField(_pcsPickedNegativeField, craftingState, -1);
			}

			return success;
		}

		/// <summary>
		/// Reverse a tier bump on hooks or positive effects (depending on isHookLink).
		/// Called when ChangeNegative partially succeeds then fails in RebuildResult.
		/// </summary>
		private static void RollbackTierBump(int change) {
			var craftingState = GetCraftingState();
			if (craftingState == null) return;

			bool isHookLink = ReflectionHelper.GetBool(_pcsIsHookLinkField, craftingState);
			var field = isHookLink ? _pcsHooksField : _pcsPositiveEffectsField;
			var tierStates = ReflectionHelper.GetField(field, craftingState) as Array;
			if (tierStates == null || _tsTierIndexField == null) return;

			for (int i = 0; i < tierStates.Length; i++) {
				var ts = tierStates.GetValue(i);
				if (ts == null) continue;
				int tierIndex = ReflectionHelper.GetInt(_tsTierIndexField, ts);
				ReflectionHelper.SetField(_tsTierIndexField, ts, tierIndex + change);
			}

			Debug.Log($"[ATSAccessibility] Rolled back tier bump (change={change}, isHookLink={isHookLink}, count={tierStates.Length})");
		}

		/// <summary>
		/// Repair any out-of-bounds tier indices in the current crafting state.
		/// This can happen if a previous ChangeNegative partially succeeded
		/// (bumped tiers) then crashed in RebuildResult, or if the user's save
		/// was persisted with corrupted state.
		/// </summary>
		public static void RepairTierIndices() {
			EnsureTypesCached();
			var craftingState = GetCraftingState();
			if (craftingState == null) return;

			var model = GetModel();
			if (model == null || _pcmEffectsElementsField == null) return;

			var elements = ReflectionHelper.GetField(_pcmEffectsElementsField, model);
			if (elements == null) return;

			int repaired = 0;
			repaired += RepairTierArray(
				ReflectionHelper.GetField(_pcsHooksField, craftingState) as Array,
				ReflectionHelper.GetField(_cecHooksSetsField, elements) as Array);
			repaired += RepairTierArray(
				ReflectionHelper.GetField(_pcsPositiveEffectsField, craftingState) as Array,
				ReflectionHelper.GetField(_cecEffectsSetsField, elements) as Array);
			repaired += RepairTierArray(
				ReflectionHelper.GetField(_pcsNegativeEffectsField, craftingState) as Array,
				ReflectionHelper.GetField(_cecEffectsSetsField, elements) as Array);

			if (repaired > 0)
				Debug.Log($"[ATSAccessibility] Repaired {repaired} out-of-bounds tier indices in Cornerstone Forge");
		}

		private static int RepairTierArray(Array tierStates, Array sets) {
			if (tierStates == null || sets == null || _tsTierIndexField == null || _tsSetIndexField == null)
				return 0;

			int repaired = 0;
			for (int i = 0; i < tierStates.Length; i++) {
				var ts = tierStates.GetValue(i);
				if (ts == null) continue;

				int setIndex = ReflectionHelper.GetInt(_tsSetIndexField, ts);
				int tierIndex = ReflectionHelper.GetInt(_tsTierIndexField, ts);

				if (setIndex < 0 || setIndex >= sets.Length) continue;
				var set = sets.GetValue(setIndex);
				if (set == null) continue;

				// Get tiers array length from the set (HookTierSet.tiers or EffectTierSet.tiers)
				var tiersField = set.GetType().GetField("tiers", GameReflection.PublicInstance);
				if (tiersField == null) continue;
				var tiers = tiersField.GetValue(set) as Array;
				if (tiers == null || tiers.Length == 0) continue;

				int maxIndex = tiers.Length - 1;
				if (tierIndex > maxIndex) {
					ReflectionHelper.SetField(_tsTierIndexField, ts, maxIndex);
					repaired++;
				} else if (tierIndex < 0) {
					ReflectionHelper.SetField(_tsTierIndexField, ts, 0);
					repaired++;
				}
			}
			return repaired;
		}

		// ========================================
		// FINISHED STATE - CRAFTED PERKS
		// ========================================

		/// <summary>
		/// Get the list of crafted perk names (for finished state).
		/// </summary>
		public static List<CraftedPerkInfo> GetCraftedPerks() {
			EnsureTypesCached();
			var result = new List<CraftedPerkInfo>();

			var state = GetState();
			if (state == null || _pcssResultsField == null) return result;

			try {
				var results = ReflectionHelper.GetField(_pcssResultsField, state) as IList<string>;
				if (results == null) return result;

				foreach (var effectName in results) {
					if (string.IsNullOrEmpty(effectName)) continue;

					var effectModel = GameReflection.GetEffectModel(effectName);
					if (effectModel == null) continue;

					string displayName = ReflectionHelper.GetPropString(_emDisplayNameProperty, effectModel) ?? effectName;
					string description = ReflectionHelper.GetPropString(_emDescriptionProperty, effectModel) ?? "";

					result.Add(new CraftedPerkInfo {
						Name = displayName,
						Description = description
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.GetCraftedPerks failed: {ex.Message}");
			}

			return result;
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(PerkCrafterReflection), "PerkCrafterReflection");
		}
	}
}
