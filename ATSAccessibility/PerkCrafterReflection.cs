using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing PerkCrafterPopup (Cornerstone Forge) data and interaction.
    /// </summary>
    public static class PerkCrafterReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public class HookOption
        {
            public object TierState;      // TierState reference for selection
            public string DisplayName;    // Hook name
            public string Description;    // Hook description
            public int Index;             // Index in the hooks array
        }

        public class EffectOption
        {
            public object TierState;      // TierState reference for selection
            public string Description;    // Effect description (used as display text)
            public bool IsPositive;       // True for positive, false for negative
            public int Index;             // Index in the effects array
        }

        public class CraftedPerkInfo
        {
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

        // GoodRef for price
        private static Type _goodRefType;
        private static FieldInfo _grGoodField;
        private static FieldInfo _grAmountField;
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

        private static void EnsureTypesCached()
        {
            if (_typesCached) return;
            _typesCached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] PerkCrafterReflection: Game assembly not available");
                    return;
                }

                CachePopupTypes(assembly);
                CachePerkCrafterTypes(assembly);
                CacheStateTypes(assembly);
                CacheModelTypes(assembly);
                CacheElementsTypes(assembly);
                CacheEffectTypes(assembly);
                CacheStorageTypes(assembly);

                Debug.Log("[ATSAccessibility] PerkCrafterReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            _popupType = assembly.GetType("Eremite.Buildings.UI.PerkCrafters.PerkCrafterPopup");
            if (_popupType != null)
            {
                // Instance is a static field, accessed directly in GetPopupInstance()
                _popupPerkCrafterField = _popupType.GetField("perkCrafter", GameReflection.NonPublicInstance);
                _popupDescTextField = _popupType.GetField("descText", GameReflection.NonPublicInstance);
                _popupResultsSlotsField = _popupType.GetField("resultsSlots", GameReflection.NonPublicInstance);

                // IsShown() is on base Popup class
                var popupBaseType = assembly.GetType("Eremite.View.Popups.Popup");
                if (popupBaseType != null)
                {
                    _popupIsShownMethod = popupBaseType.GetMethod("IsShown", GameReflection.PublicInstance);
                }
            }
        }

        private static void CachePerkCrafterTypes(Assembly assembly)
        {
            _perkCrafterType = assembly.GetType("Eremite.Buildings.PerkCrafter");
            if (_perkCrafterType != null)
            {
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

        private static void CacheStateTypes(Assembly assembly)
        {
            _perkCrafterStateType = assembly.GetType("Eremite.Buildings.PerkCrafterState");
            if (_perkCrafterStateType != null)
            {
                _pcssCraftingField = _perkCrafterStateType.GetField("crafting", GameReflection.PublicInstance);
                _pcssCraftedPerksField = _perkCrafterStateType.GetField("craftedPerks", GameReflection.PublicInstance);
                _pcssResultsField = _perkCrafterStateType.GetField("results", GameReflection.PublicInstance);
            }

            _perkCraftingStateType = assembly.GetType("Eremite.Buildings.PerkCraftingState");
            if (_perkCraftingStateType != null)
            {
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
        }

        private static void CacheModelTypes(Assembly assembly)
        {
            _perkCrafterModelType = assembly.GetType("Eremite.Buildings.PerkCrafterModel");
            if (_perkCrafterModelType != null)
            {
                _pcmChargesField = _perkCrafterModelType.GetField("charges", GameReflection.PublicInstance);
                _pcmPriceField = _perkCrafterModelType.GetField("price", GameReflection.PublicInstance);
                _pcmEffectsElementsField = _perkCrafterModelType.GetField("effectsElements", GameReflection.PublicInstance);
            }

            _goodRefType = assembly.GetType("Eremite.Model.GoodRef");
            if (_goodRefType != null)
            {
                _grGoodField = _goodRefType.GetField("good", GameReflection.PublicInstance);
                _grAmountField = _goodRefType.GetField("amount", GameReflection.PublicInstance);
                _grToGoodMethod = _goodRefType.GetMethod("ToGood", GameReflection.PublicInstance);
            }
        }

        private static void CacheElementsTypes(Assembly assembly)
        {
            _elementsContainerType = assembly.GetType("Eremite.Model.Effects.CraftedEffectElementsContainer");
            if (_elementsContainerType != null)
            {
                _cecHooksSetsField = _elementsContainerType.GetField("hooksSets", GameReflection.PublicInstance);
                _cecEffectsSetsField = _elementsContainerType.GetField("effectsSets", GameReflection.PublicInstance);
                _cecDisplayNamesField = _elementsContainerType.GetField("displayNames", GameReflection.PublicInstance);
                _cecGetHookMethod = _elementsContainerType.GetMethod("GetHook", GameReflection.PublicInstance);
                _cecGetEffectMethod = _elementsContainerType.GetMethod("GetEffect", GameReflection.PublicInstance);
            }
        }

        private static void CacheEffectTypes(Assembly assembly)
        {
            _hookLogicType = assembly.GetType("Eremite.Model.Effects.HookLogic");
            if (_hookLogicType != null)
            {
                _hookDescriptionProperty = _hookLogicType.GetProperty("Description", GameReflection.PublicInstance);
            }

            _effectModelType = assembly.GetType("Eremite.Model.EffectModel");
            if (_effectModelType != null)
            {
                _emDisplayNameProperty = _effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                _emDescriptionProperty = _effectModelType.GetProperty("Description", GameReflection.PublicInstance);
                _emIsPositiveField = _effectModelType.GetField("isPositive", GameReflection.PublicInstance);
            }
        }

        private static void CacheStorageTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsStorageServiceProperty = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
            }

            var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
            if (storageServiceType != null)
            {
                _ssMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
            }

            var storageType = assembly.GetType("Eremite.Services.Storage");
            if (storageType != null)
            {
                _storageGetAmountMethod = storageType.GetMethod("GetAmount", new[] { typeof(string) });
                // IsAvailable takes a GoodRef
                if (_goodRefType != null)
                {
                    _storageIsAvailableMethod = storageType.GetMethod("IsAvailable", new[] { _goodRefType });
                }
            }
        }

        // ========================================
        // POPUP ACCESS
        // ========================================

        /// <summary>
        /// Get the PerkCrafterPopup.Instance (static field).
        /// </summary>
        public static object GetPopupInstance()
        {
            EnsureTypesCached();
            if (_popupType == null) return null;

            try
            {
                var instanceField = _popupType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                return instanceField?.GetValue(null);
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if the PerkCrafterPopup is currently shown.
        /// </summary>
        public static bool IsPopupShown()
        {
            var popup = GetPopupInstance();
            if (popup == null || _popupIsShownMethod == null) return false;

            try
            {
                return (bool)_popupIsShownMethod.Invoke(popup, null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if the given popup is a PerkCrafterPopup.
        /// </summary>
        public static bool IsPerkCrafterPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "PerkCrafterPopup";
        }

        /// <summary>
        /// Get the PerkCrafter from the popup.
        /// </summary>
        private static object GetPerkCrafter()
        {
            var popup = GetPopupInstance();
            if (popup == null || _popupPerkCrafterField == null) return null;

            try
            {
                return _popupPerkCrafterField.GetValue(popup);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the PerkCrafterState from the PerkCrafter.
        /// </summary>
        private static object GetState()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcStateField == null) return null;

            try
            {
                return _pcStateField.GetValue(crafter);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the PerkCraftingState (current crafting session).
        /// </summary>
        private static object GetCraftingState()
        {
            var state = GetState();
            if (state == null || _pcssCraftingField == null) return null;

            try
            {
                return _pcssCraftingField.GetValue(state);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the PerkCrafterModel.
        /// </summary>
        private static object GetModel()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcModelField == null) return null;

            try
            {
                return _pcModelField.GetValue(crafter);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the CraftedEffectElementsContainer.
        /// </summary>
        private static object GetElementsContainer()
        {
            var model = GetModel();
            if (model == null || _pcmEffectsElementsField == null) return null;

            try
            {
                return _pcmEffectsElementsField.GetValue(model);
            }
            catch { return null; }
        }

        // ========================================
        // NPC DIALOGUE
        // ========================================

        /// <summary>
        /// Get the NPC dialogue text from the popup.
        /// </summary>
        public static string GetNpcDialogue()
        {
            var popup = GetPopupInstance();
            if (popup == null || _popupDescTextField == null) return null;

            try
            {
                var textComponent = _popupDescTextField.GetValue(popup);
                if (textComponent == null) return null;

                // TMP_Text has a 'text' property
                var textProperty = textComponent.GetType().GetProperty("text", GameReflection.PublicInstance);
                return textProperty?.GetValue(textComponent) as string;
            }
            catch { return null; }
        }

        // ========================================
        // CRAFTING STATE QUERIES
        // ========================================

        /// <summary>
        /// Check if all charges have been used (finished state).
        /// </summary>
        public static bool HasUsedAllCharges()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcHasUsedAllChargesMethod == null) return false;

            try
            {
                return (bool)_pcHasUsedAllChargesMethod.Invoke(crafter, null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Get the number of crafts remaining.
        /// </summary>
        public static int GetUsesLeft()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcGetUsesLeftMethod == null) return 0;

            try
            {
                return (int)_pcGetUsesLeftMethod.Invoke(crafter, null);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Get the total number of charges (typically 3).
        /// </summary>
        public static int GetTotalCharges()
        {
            var model = GetModel();
            if (model == null || _pcmChargesField == null) return 3;

            try
            {
                return (int)_pcmChargesField.GetValue(model);
            }
            catch { return 3; }
        }

        /// <summary>
        /// Get the number of crafted perks so far.
        /// </summary>
        public static int GetCraftedPerksCount()
        {
            var state = GetState();
            if (state == null || _pcssCraftedPerksField == null) return 0;

            try
            {
                return (int)_pcssCraftedPerksField.GetValue(state);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Check if a negative effect is currently picked.
        /// </summary>
        public static bool IsNegativePicked()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcIsNegativePickedMethod == null) return false;

            try
            {
                return (bool)_pcIsNegativePickedMethod.Invoke(crafter, null);
            }
            catch { return false; }
        }

        // ========================================
        // SELECTION INDICES
        // ========================================

        /// <summary>
        /// Get the currently selected hook index.
        /// </summary>
        public static int GetPickedHookIndex()
        {
            var craftingState = GetCraftingState();
            if (craftingState == null || _pcsPickedHookField == null) return 0;

            try
            {
                return (int)_pcsPickedHookField.GetValue(craftingState);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Get the currently selected positive effect index.
        /// </summary>
        public static int GetPickedPositiveIndex()
        {
            var craftingState = GetCraftingState();
            if (craftingState == null || _pcsPickedPositiveField == null) return 0;

            try
            {
                return (int)_pcsPickedPositiveField.GetValue(craftingState);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Get the currently selected negative effect index (-1 if none).
        /// </summary>
        public static int GetPickedNegativeIndex()
        {
            var craftingState = GetCraftingState();
            if (craftingState == null || _pcsPickedNegativeField == null) return -1;

            try
            {
                return (int)_pcsPickedNegativeField.GetValue(craftingState);
            }
            catch { return -1; }
        }

        // ========================================
        // HOOK/EFFECT OPTIONS
        // ========================================

        /// <summary>
        /// Get the available hook options for selection.
        /// </summary>
        public static List<HookOption> GetHookOptions()
        {
            EnsureTypesCached();
            var result = new List<HookOption>();

            var craftingState = GetCraftingState();
            if (craftingState == null || _pcsHooksField == null) return result;

            var crafter = GetPerkCrafter();
            if (crafter == null) return result;

            try
            {
                var hooks = _pcsHooksField.GetValue(craftingState) as Array;
                if (hooks == null) return result;

                for (int i = 0; i < hooks.Length; i++)
                {
                    var tierState = hooks.GetValue(i);
                    if (tierState == null) continue;

                    // Get HookLogic via perkCrafter.GetHook(tierState)
                    var hookLogic = _pcGetHookMethod?.Invoke(crafter, new[] { tierState });
                    if (hookLogic == null) continue;

                    string description = _hookDescriptionProperty?.GetValue(hookLogic) as string ?? "";

                    // HookLogic doesn't have a DisplayName, use description as name
                    // Extract a short name from the description or use a generic one
                    string displayName = GetHookDisplayName(hookLogic, i);

                    result.Add(new HookOption
                    {
                        TierState = tierState,
                        DisplayName = displayName,
                        Description = description,
                        Index = i
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.GetHookOptions failed: {ex.Message}");
            }

            return result;
        }

        private static string GetHookDisplayName(object hookLogic, int index)
        {
            // Try to get a reasonable name from the hook
            // HookLogic has a Name property inherited from SO (ScriptableObject)
            try
            {
                var nameProperty = hookLogic.GetType().GetProperty("Name", GameReflection.PublicInstance);
                var name = nameProperty?.GetValue(hookLogic) as string;
                if (!string.IsNullOrEmpty(name))
                {
                    // Clean up internal names like "Hook_Crafted_Gathering_5"
                    return CleanInternalName(name);
                }
            }
            catch { }

            return $"Hook {index + 1}";
        }

        private static string CleanInternalName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Remove common prefixes
            name = name.Replace("Hook_Crafted_", "").Replace("Effect_Crafted_", "");
            name = name.Replace("_", " ");

            // Remove trailing numbers that look like IDs
            var parts = name.Split(' ');
            if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out _))
            {
                name = string.Join(" ", parts, 0, parts.Length - 1);
            }

            return name.Trim();
        }

        /// <summary>
        /// Get the available positive effect options for selection.
        /// </summary>
        public static List<EffectOption> GetPositiveOptions()
        {
            return GetEffectOptions(_pcsPositiveEffectsField, true);
        }

        /// <summary>
        /// Get the available negative effect options for selection.
        /// </summary>
        public static List<EffectOption> GetNegativeOptions()
        {
            return GetEffectOptions(_pcsNegativeEffectsField, false);
        }

        private static List<EffectOption> GetEffectOptions(FieldInfo effectsField, bool isPositive)
        {
            EnsureTypesCached();
            var result = new List<EffectOption>();

            var craftingState = GetCraftingState();
            if (craftingState == null || effectsField == null) return result;

            var crafter = GetPerkCrafter();
            if (crafter == null) return result;

            try
            {
                var effects = effectsField.GetValue(craftingState) as Array;
                if (effects == null) return result;

                for (int i = 0; i < effects.Length; i++)
                {
                    var tierState = effects.GetValue(i);
                    if (tierState == null) continue;

                    // Get EffectModel via perkCrafter.GetEffect(tierState)
                    var effectModel = _pcGetEffectMethod?.Invoke(crafter, new[] { tierState });
                    if (effectModel == null) continue;

                    // Use Description as the display text - these effects don't have proper display names
                    string description = _emDescriptionProperty?.GetValue(effectModel) as string ?? $"Effect {i + 1}";

                    result.Add(new EffectOption
                    {
                        TierState = tierState,
                        Description = description,
                        IsPositive = isPositive,
                        Index = i
                    });
                }
            }
            catch (Exception ex)
            {
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
        public static HookOption GetCurrentHook()
        {
            var options = GetHookOptions();
            int index = GetPickedHookIndex();

            if (index >= 0 && index < options.Count)
                return options[index];

            return null;
        }

        /// <summary>
        /// Get the currently selected positive effect info.
        /// </summary>
        public static EffectOption GetCurrentPositive()
        {
            var options = GetPositiveOptions();
            int index = GetPickedPositiveIndex();

            if (index >= 0 && index < options.Count)
                return options[index];

            return null;
        }

        /// <summary>
        /// Get the currently selected negative effect info (null if none).
        /// </summary>
        public static EffectOption GetCurrentNegative()
        {
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
        public static string GetResultName()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcGetResultDisplayNameMethod == null) return null;

            try
            {
                var displayName = _pcGetResultDisplayNameMethod.Invoke(crafter, null) as string;

                // If it's a localization key, resolve it
                if (!string.IsNullOrEmpty(displayName))
                {
                    // Try to get the localized text
                    var result = _pcGetCurrentResultMethod?.Invoke(crafter, null);
                    if (result != null)
                    {
                        var resultDisplayName = _emDisplayNameProperty?.GetValue(result) as string;
                        if (!string.IsNullOrEmpty(resultDisplayName))
                            return resultDisplayName;
                    }
                }

                return displayName;
            }
            catch { return null; }
        }

        /// <summary>
        /// Set the result perk name.
        /// </summary>
        public static bool SetResultName(string name)
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcChangeNameMethod == null) return false;

            try
            {
                // ChangeName(string name, bool isLocalizedName)
                // For custom names, isLocalizedName = false
                _pcChangeNameMethod.Invoke(crafter, new object[] { name, false });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.SetResultName failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Randomize the result perk name.
        /// </summary>
        public static bool RandomizeName()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcChangeNameMethod == null) return false;

            var elements = GetElementsContainer();
            if (elements == null || _cecDisplayNamesField == null) return false;

            try
            {
                // Get random name from displayNames array
                var displayNames = _cecDisplayNamesField.GetValue(elements) as Array;
                if (displayNames == null || displayNames.Length == 0) return false;

                int randomIndex = UnityEngine.Random.Range(0, displayNames.Length);
                var locaText = displayNames.GetValue(randomIndex);

                // Get the key from LocaText
                var keyField = locaText.GetType().GetField("key", GameReflection.PublicInstance);
                var key = keyField?.GetValue(locaText) as string;

                if (!string.IsNullOrEmpty(key))
                {
                    // ChangeName with isLocalizedName = true
                    _pcChangeNameMethod.Invoke(crafter, new object[] { key, true });
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.RandomizeName failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // PRICE AND CRAFTING
        // ========================================

        /// <summary>
        /// Get the crafting price as (amount, goodDisplayName).
        /// </summary>
        public static (int amount, string goodName) GetPrice()
        {
            var model = GetModel();
            if (model == null || _pcmPriceField == null) return (0, "Unknown");

            try
            {
                var priceRef = _pcmPriceField.GetValue(model);
                if (priceRef == null) return (0, "Unknown");

                int amount = _grAmountField != null ? (int)_grAmountField.GetValue(priceRef) : 0;
                var goodModel = _grGoodField?.GetValue(priceRef);
                string goodName = goodModel != null ?
                    (GameReflection.GetDisplayName(goodModel) ?? "Unknown") : "Unknown";

                return (amount, goodName);
            }
            catch { return (0, "Unknown"); }
        }

        /// <summary>
        /// Get the current storage amount of the crafting resource.
        /// </summary>
        public static int GetStorageAmount()
        {
            var model = GetModel();
            if (model == null || _pcmPriceField == null) return 0;

            try
            {
                var priceRef = _pcmPriceField.GetValue(model);
                if (priceRef == null) return 0;

                var goodModel = _grGoodField?.GetValue(priceRef);
                if (goodModel == null) return 0;

                // Get the good name
                var nameProperty = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
                var goodName = nameProperty?.GetValue(goodModel) as string;
                if (string.IsNullOrEmpty(goodName)) return 0;

                // Access storage
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return 0;

                var storageService = _gsStorageServiceProperty?.GetValue(gameServices);
                if (storageService == null) return 0;

                var mainStorage = _ssMainProperty?.GetValue(storageService);
                if (mainStorage == null) return 0;

                if (_storageGetAmountMethod != null)
                {
                    return (int)_storageGetAmountMethod.Invoke(mainStorage, new object[] { goodName });
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// Check if the player can afford to craft.
        /// </summary>
        public static bool CanAffordCraft()
        {
            var (amount, _) = GetPrice();
            return GetStorageAmount() >= amount;
        }

        /// <summary>
        /// Perform the craft action.
        /// </summary>
        public static bool PerformCraft()
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcCreateCurrentPerkMethod == null) return false;

            try
            {
                _pcCreateCurrentPerkMethod.Invoke(crafter, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.PerformCraft failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // SELECTION ACTIONS
        // ========================================

        /// <summary>
        /// Select a hook option.
        /// </summary>
        public static bool SelectHook(HookOption option)
        {
            if (option == null) return false;

            var crafter = GetPerkCrafter();
            if (crafter == null || _pcChangeHookMethod == null) return false;

            try
            {
                _pcChangeHookMethod.Invoke(crafter, new[] { option.TierState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.SelectHook failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Select a positive effect option.
        /// </summary>
        public static bool SelectPositive(EffectOption option)
        {
            if (option == null) return false;

            var crafter = GetPerkCrafter();
            if (crafter == null || _pcChangePositiveMethod == null) return false;

            try
            {
                _pcChangePositiveMethod.Invoke(crafter, new[] { option.TierState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.SelectPositive failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Select a negative effect option (or clear selection if null).
        /// </summary>
        public static bool SelectNegative(EffectOption option)
        {
            var crafter = GetPerkCrafter();
            if (crafter == null || _pcChangeNegativeMethod == null) return false;

            try
            {
                // If option is null, we need to clear the selection
                // This is done by calling ChangeNegative with a non-existent index
                // The game handles this by setting pickedNegative to -1
                if (option == null)
                {
                    // Get the crafting state and directly set pickedNegative to -1
                    var craftingState = GetCraftingState();
                    if (craftingState == null || _pcsPickedNegativeField == null)
                        return false;

                    _pcsPickedNegativeField.SetValue(craftingState, -1);
                    return true;
                }

                _pcChangeNegativeMethod.Invoke(crafter, new[] { option.TierState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.SelectNegative failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // FINISHED STATE - CRAFTED PERKS
        // ========================================

        /// <summary>
        /// Get the list of crafted perk names (for finished state).
        /// </summary>
        public static List<CraftedPerkInfo> GetCraftedPerks()
        {
            EnsureTypesCached();
            var result = new List<CraftedPerkInfo>();

            var state = GetState();
            if (state == null || _pcssResultsField == null) return result;

            try
            {
                var results = _pcssResultsField.GetValue(state) as IList<string>;
                if (results == null) return result;

                foreach (var effectName in results)
                {
                    if (string.IsNullOrEmpty(effectName)) continue;

                    var effectModel = GameReflection.GetEffectModel(effectName);
                    if (effectModel == null) continue;

                    string displayName = _emDisplayNameProperty?.GetValue(effectModel) as string ?? effectName;
                    string description = _emDescriptionProperty?.GetValue(effectModel) as string ?? "";

                    result.Add(new CraftedPerkInfo
                    {
                        Name = displayName,
                        Description = description
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PerkCrafterReflection.GetCraftedPerks failed: {ex.Message}");
            }

            return result;
        }
    }
}
