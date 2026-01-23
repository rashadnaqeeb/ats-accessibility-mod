using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing RewardPickPopup (cornerstone selection) and
    /// CornerstonesLimitPickPopup (choose-one-to-remove) data and interaction.
    /// </summary>
    public static class CornerstoneReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public class CornerstoneOption
        {
            public object Model;         // EffectModel
            public string DisplayName;   // Includes Mythic suffix
            public string Description;
            public string Rarity;        // "Common", "Rare", etc.
            public bool IsEthereal;
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // IGameServices.CornerstonesService
        private static PropertyInfo _gsCornerstonesServiceProperty;

        // ICornerstonesService methods
        private static MethodInfo _csGetCurrentPickMethod;
        private static MethodInfo _csGetRerollsLeftMethod;
        private static MethodInfo _csCanExtendMethod;
        private static MethodInfo _csCanAffordExtendMethod;
        private static MethodInfo _csExtendMethod;
        private static MethodInfo _csGetDeclinePayoffMethod;
        private static MethodInfo _csRemoveFromActiveMethod;

        // RewardPickState fields
        private static FieldInfo _rpsOptionsField;
        private static FieldInfo _rpsViewConfigurationField;

        // EffectModel properties/fields
        private static PropertyInfo _emDisplayNameProperty;
        private static PropertyInfo _emDescriptionProperty;
        private static FieldInfo _emRarityField;
        private static FieldInfo _emIsEtherealField;
        private static MethodInfo _emRemoveMethod;

        // NPC Dialogue
        private static MethodInfo _settingsGetCornerstonesViewConfigMethod;
        private static FieldInfo _cvcNpcNameField;
        private static FieldInfo _cvcNpcDialogueField;

        // Extend cost (BiomeService → CurrentBiome → seasons → seasonRewardsExtendPrice)
        private static PropertyInfo _gsBiomeServiceProperty;
        private static PropertyInfo _bsCurrentBiomeProperty;
        private static FieldInfo _bmSeasonsField;
        private static FieldInfo _scExtendPriceField;

        // GoodRef fields
        private static FieldInfo _grGoodField;
        private static FieldInfo _grAmountField;

        // Good struct fields
        private static FieldInfo _goodNameField;
        private static FieldInfo _goodAmountField;

        // Popup methods
        private static MethodInfo _rpOnRewardPickedMethod;
        private static MethodInfo _rpRerollMethod;
        private static MethodInfo _rpSkipMethod;
        private static FieldInfo _rpDefaultConfigurationField;
        private static MethodInfo _clpFinishTaskMethod;
        private static MethodInfo _popupHideMethod;

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
                    Debug.LogWarning("[ATSAccessibility] CornerstoneReflection: Game assembly not available");
                    return;
                }

                CacheServiceTypes(assembly);
                CacheRewardPickStateTypes(assembly);
                CacheEffectModelTypes(assembly);
                CacheNpcDialogueTypes(assembly);
                CacheBiomeTypes(assembly);
                CacheGoodTypes(assembly);
                CachePopupTypes(assembly);

                Debug.Log("[ATSAccessibility] CornerstoneReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsCornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService",
                    GameReflection.PublicInstance);
                _gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
                    GameReflection.PublicInstance);
            }

            var csType = assembly.GetType("Eremite.Services.ICornerstonesService");
            if (csType != null)
            {
                _csGetCurrentPickMethod = csType.GetMethod("GetCurrentPick", GameReflection.PublicInstance);
                _csGetRerollsLeftMethod = csType.GetMethod("GetRerollsLeft", GameReflection.PublicInstance);
                _csCanExtendMethod = csType.GetMethod("CanExtend", GameReflection.PublicInstance);
                _csCanAffordExtendMethod = csType.GetMethod("CanAffordExtend", GameReflection.PublicInstance);
                _csExtendMethod = csType.GetMethod("Extend", GameReflection.PublicInstance);
                _csGetDeclinePayoffMethod = csType.GetMethod("GetDeclinePayoff", GameReflection.PublicInstance);

                // RemoveFromActive takes an EffectModel parameter
                var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    _csRemoveFromActiveMethod = csType.GetMethod("RemoveFromActive",
                        new[] { effectModelType });
                }
            }
        }

        private static void CacheRewardPickStateTypes(Assembly assembly)
        {
            var rpsType = assembly.GetType("Eremite.Model.RewardPickState");
            if (rpsType != null)
            {
                _rpsOptionsField = rpsType.GetField("options", GameReflection.PublicInstance);
                _rpsViewConfigurationField = rpsType.GetField("viewConfiguration", GameReflection.PublicInstance);
            }
        }

        private static void CacheEffectModelTypes(Assembly assembly)
        {
            var emType = assembly.GetType("Eremite.Model.EffectModel");
            if (emType != null)
            {
                _emDisplayNameProperty = emType.GetProperty("DisplayName", GameReflection.PublicInstance);
                _emDescriptionProperty = emType.GetProperty("Description", GameReflection.PublicInstance);
                _emRarityField = emType.GetField("rarity", GameReflection.PublicInstance);
                _emIsEtherealField = emType.GetField("isEthereal", GameReflection.PublicInstance);
                _emRemoveMethod = emType.GetMethod("Remove", GameReflection.PublicInstance);
            }
        }

        private static void CacheNpcDialogueTypes(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsGetCornerstonesViewConfigMethod = settingsType.GetMethod(
                    "GetCornerstonesViewConfiguration", new[] { typeof(string) });
            }

            var cvcType = assembly.GetType("Eremite.Model.ViewsConfigurations.CornerstonesViewConfiguration");
            if (cvcType != null)
            {
                _cvcNpcNameField = cvcType.GetField("npcName", GameReflection.PublicInstance);
                _cvcNpcDialogueField = cvcType.GetField("npcDialogue", GameReflection.PublicInstance);
            }
        }

        private static void CacheBiomeTypes(Assembly assembly)
        {
            var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
            if (biomeServiceType != null)
            {
                _bsCurrentBiomeProperty = biomeServiceType.GetProperty("CurrentBiome",
                    GameReflection.PublicInstance);
            }

            var biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
            if (biomeModelType != null)
            {
                _bmSeasonsField = biomeModelType.GetField("seasons", GameReflection.PublicInstance);
            }

            var seasonsType = assembly.GetType("Eremite.Model.Configs.SeasonsConfig");
            if (seasonsType != null)
            {
                _scExtendPriceField = seasonsType.GetField("seasonRewardsExtendPrice",
                    GameReflection.PublicInstance);
            }
        }

        private static void CacheGoodTypes(Assembly assembly)
        {
            var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
            if (goodRefType != null)
            {
                _grGoodField = goodRefType.GetField("good", GameReflection.PublicInstance);
                _grAmountField = goodRefType.GetField("amount", GameReflection.PublicInstance);
            }

            var goodType = assembly.GetType("Eremite.Model.Good");
            if (goodType != null)
            {
                _goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
                _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            var rpType = assembly.GetType("Eremite.View.HUD.RewardPickPopup");
            if (rpType != null)
            {
                _rpOnRewardPickedMethod = rpType.GetMethod("OnRewardPicked",
                    GameReflection.NonPublicInstance);
                _rpRerollMethod = rpType.GetMethod("Reroll",
                    GameReflection.NonPublicInstance);
                _rpSkipMethod = rpType.GetMethod("Skip",
                    GameReflection.NonPublicInstance);
                _rpDefaultConfigurationField = rpType.GetField("defaultConfiguration",
                    GameReflection.NonPublicInstance);
            }

            var clpType = assembly.GetType("Eremite.View.Popups.CornerstonesLimitPick.CornerstonesLimitPickPopup");
            if (clpType != null)
            {
                _clpFinishTaskMethod = clpType.GetMethod("FinishTask",
                    GameReflection.NonPublicInstance);
            }

            // Popup.Hide() is on the base Popup class
            var popupType = assembly.GetType("Eremite.View.Popups.Popup");
            if (popupType != null)
            {
                _popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetCornerstonesService()
        {
            EnsureTypesCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsCornerstonesServiceProperty == null) return null;
            try { return _gsCornerstonesServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetBiomeService()
        {
            EnsureTypesCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsBiomeServiceProperty == null) return null;
            try { return _gsBiomeServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetCurrentPick()
        {
            var service = GetCornerstonesService();
            if (service == null || _csGetCurrentPickMethod == null) return null;
            try { return _csGetCurrentPickMethod.Invoke(service, null); }
            catch { return null; }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        public static bool IsRewardPickPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "RewardPickPopup";
        }

        public static bool IsCornerstonesLimitPickPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "CornerstonesLimitPickPopup";
        }

        // ========================================
        // EFFECT MODEL HELPERS
        // ========================================

        private static string GetEffectDisplayName(object effectModel)
        {
            if (effectModel == null || _emDisplayNameProperty == null) return null;
            try { return _emDisplayNameProperty.GetValue(effectModel) as string; }
            catch { return null; }
        }

        private static string GetEffectDescription(object effectModel)
        {
            if (effectModel == null || _emDescriptionProperty == null) return null;
            try { return _emDescriptionProperty.GetValue(effectModel) as string; }
            catch { return null; }
        }

        private static string GetEffectRarity(object effectModel)
        {
            if (effectModel == null || _emRarityField == null) return "Unknown";
            try
            {
                var rarity = _emRarityField.GetValue(effectModel);
                return rarity?.ToString() ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        private static bool GetEffectIsEthereal(object effectModel)
        {
            if (effectModel == null || _emIsEtherealField == null) return false;
            try { return (bool)_emIsEtherealField.GetValue(effectModel); }
            catch { return false; }
        }

        private static CornerstoneOption BuildOption(object effectModel)
        {
            if (effectModel == null) return null;

            return new CornerstoneOption
            {
                Model = effectModel,
                DisplayName = GetEffectDisplayName(effectModel) ?? "Unknown",
                Description = GetEffectDescription(effectModel) ?? "",
                Rarity = GetEffectRarity(effectModel),
                IsEthereal = GetEffectIsEthereal(effectModel)
            };
        }

        // ========================================
        // CURRENT OPTIONS
        // ========================================

        /// <summary>
        /// Get the current cornerstone options from the active pick state.
        /// </summary>
        public static List<CornerstoneOption> GetCurrentOptions()
        {
            EnsureTypesCached();

            var result = new List<CornerstoneOption>();
            var pickState = GetCurrentPick();
            if (pickState == null || _rpsOptionsField == null) return result;

            try
            {
                var options = _rpsOptionsField.GetValue(pickState) as List<string>;
                if (options == null || options.Count == 0) return result;

                foreach (var effectName in options)
                {
                    if (string.IsNullOrEmpty(effectName)) continue;

                    var effectModel = GameReflection.GetEffectModel(effectName);
                    if (effectModel == null) continue;

                    var option = BuildOption(effectModel);
                    if (option != null)
                        result.Add(option);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetCurrentOptions failed: {ex.Message}");
            }

            return result;
        }

        // ========================================
        // NPC DIALOGUE
        // ========================================

        /// <summary>
        /// Get the NPC name and dialogue text for the current pick.
        /// Falls back to the popup's defaultConfiguration if viewConfiguration is empty.
        /// </summary>
        public static (string npcName, string dialogue) GetNpcDialogue(object popup)
        {
            EnsureTypesCached();

            try
            {
                var pickState = GetCurrentPick();
                object viewConfig = null;

                if (pickState != null && _rpsViewConfigurationField != null)
                {
                    var configName = _rpsViewConfigurationField.GetValue(pickState) as string;
                    if (!string.IsNullOrEmpty(configName))
                    {
                        var settings = GameReflection.GetSettings();
                        if (settings != null && _settingsGetCornerstonesViewConfigMethod != null)
                        {
                            viewConfig = _settingsGetCornerstonesViewConfigMethod.Invoke(
                                settings, new object[] { configName });
                        }
                    }
                }

                // Fallback to popup's defaultConfiguration
                if (viewConfig == null && popup != null && _rpDefaultConfigurationField != null)
                {
                    viewConfig = _rpDefaultConfigurationField.GetValue(popup);
                }

                if (viewConfig == null) return ("", "");

                string npcName = "";
                string dialogue = "";

                if (_cvcNpcNameField != null)
                {
                    var nameLocaText = _cvcNpcNameField.GetValue(viewConfig);
                    npcName = GameReflection.GetLocaText(nameLocaText) ?? "";
                }

                if (_cvcNpcDialogueField != null)
                {
                    var dialogueLocaText = _cvcNpcDialogueField.GetValue(viewConfig);
                    dialogue = GameReflection.GetLocaText(dialogueLocaText) ?? "";
                }

                return (npcName, dialogue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetNpcDialogue failed: {ex.Message}");
                return ("", "");
            }
        }

        // ========================================
        // PICKING
        // ========================================

        /// <summary>
        /// Pick a cornerstone by invoking the popup's OnRewardPicked method.
        /// This triggers the async Pick flow (including limit check if needed).
        /// </summary>
        public static bool PickCornerstone(object popup, object effectModel)
        {
            if (popup == null || effectModel == null) return false;
            EnsureTypesCached();
            if (_rpOnRewardPickedMethod == null) return false;

            try
            {
                _rpOnRewardPickedMethod.Invoke(popup, new[] { effectModel });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: PickCornerstone failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // SKIP / DECLINE
        // ========================================

        /// <summary>
        /// Skip the current cornerstone pick (decline).
        /// </summary>
        public static bool Skip(object popup)
        {
            if (popup == null) return false;
            EnsureTypesCached();
            if (_rpSkipMethod == null) return false;

            try
            {
                _rpSkipMethod.Invoke(popup, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: Skip failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the decline payoff (good name and amount received for skipping).
        /// </summary>
        public static (int amount, string goodDisplayName) GetDeclinePayoff()
        {
            EnsureTypesCached();
            var service = GetCornerstonesService();
            if (service == null || _csGetDeclinePayoffMethod == null) return (0, "Unknown");

            try
            {
                var good = _csGetDeclinePayoffMethod.Invoke(service, null);
                if (good == null) return (0, "Unknown");

                var name = _goodNameField?.GetValue(good) as string ?? "";
                var amount = _goodAmountField != null ? (int)_goodAmountField.GetValue(good) : 0;
                var displayName = GameReflection.GetGoodDisplayName(name);

                return (amount, displayName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetDeclinePayoff failed: {ex.Message}");
                return (0, "Unknown");
            }
        }

        // ========================================
        // REROLL
        // ========================================

        /// <summary>
        /// Get the number of rerolls remaining.
        /// </summary>
        public static int GetRerollsLeft()
        {
            EnsureTypesCached();
            var service = GetCornerstonesService();
            if (service == null || _csGetRerollsLeftMethod == null) return 0;

            try
            {
                return (int)_csGetRerollsLeftMethod.Invoke(service, null);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Reroll the current options via the popup's Reroll method.
        /// This keeps the popup UI in sync (updates slots and reroll button).
        /// </summary>
        public static bool Reroll(object popup)
        {
            if (popup == null) return false;
            EnsureTypesCached();
            if (_rpRerollMethod == null) return false;

            try
            {
                _rpRerollMethod.Invoke(popup, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: Reroll failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // EXTEND
        // ========================================

        /// <summary>
        /// Check if extending is available.
        /// </summary>
        public static bool CanExtend()
        {
            EnsureTypesCached();
            var service = GetCornerstonesService();
            if (service == null || _csCanExtendMethod == null) return false;

            try { return (bool)_csCanExtendMethod.Invoke(service, null); }
            catch { return false; }
        }

        /// <summary>
        /// Check if the player can afford to extend.
        /// </summary>
        public static bool CanAffordExtend()
        {
            EnsureTypesCached();
            var service = GetCornerstonesService();
            if (service == null || _csCanAffordExtendMethod == null) return false;

            try { return (bool)_csCanAffordExtendMethod.Invoke(service, null); }
            catch { return false; }
        }

        /// <summary>
        /// Extend the current options (add one more cornerstone choice).
        /// </summary>
        public static bool Extend()
        {
            EnsureTypesCached();
            var service = GetCornerstonesService();
            if (service == null || _csExtendMethod == null) return false;

            try
            {
                _csExtendMethod.Invoke(service, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: Extend failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the extend cost as (amount, good display name).
        /// Reads from BiomeService.CurrentBiome.seasons.seasonRewardsExtendPrice.
        /// </summary>
        public static (int amount, string goodDisplayName) GetExtendCost()
        {
            EnsureTypesCached();

            try
            {
                var biomeService = GetBiomeService();
                if (biomeService == null || _bsCurrentBiomeProperty == null) return (0, "Unknown");

                var biome = _bsCurrentBiomeProperty.GetValue(biomeService);
                if (biome == null || _bmSeasonsField == null) return (0, "Unknown");

                var seasons = _bmSeasonsField.GetValue(biome);
                if (seasons == null || _scExtendPriceField == null) return (0, "Unknown");

                var extendPrice = _scExtendPriceField.GetValue(seasons);
                if (extendPrice == null) return (0, "Unknown");

                var amount = _grAmountField != null ? (int)_grAmountField.GetValue(extendPrice) : 0;
                var goodModel = _grGoodField?.GetValue(extendPrice);
                var displayName = goodModel != null
                    ? (GameReflection.GetDisplayName(goodModel) ?? "Unknown")
                    : "Unknown";

                return (amount, displayName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetExtendCost failed: {ex.Message}");
                return (0, "Unknown");
            }
        }

        // ========================================
        // LIMIT POPUP - ACTIVE CORNERSTONES
        // ========================================

        /// <summary>
        /// Get all active cornerstones as CornerstoneOption objects.
        /// </summary>
        public static List<CornerstoneOption> GetActiveCornerstones()
        {
            EnsureTypesCached();

            var result = new List<CornerstoneOption>();
            var names = GameReflection.GetActiveCornerstones();
            if (names == null) return result;

            foreach (var name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;

                var effectModel = GameReflection.GetEffectModel(name);
                if (effectModel == null) continue;

                var option = BuildOption(effectModel);
                if (option != null)
                    result.Add(option);
            }

            return result;
        }

        /// <summary>
        /// Remove a cornerstone and confirm the limit popup (resolves the async task as true).
        /// </summary>
        public static bool RemoveAndConfirm(object limitPopup, object effectModel)
        {
            if (limitPopup == null || effectModel == null) return false;
            EnsureTypesCached();

            try
            {
                // service.RemoveFromActive(effectModel)
                var service = GetCornerstonesService();
                if (service != null && _csRemoveFromActiveMethod != null)
                {
                    _csRemoveFromActiveMethod.Invoke(service, new[] { effectModel });
                }

                // effectModel.Remove() - has optional params, pass defaults
                if (_emRemoveMethod != null)
                {
                    var removeParams = _emRemoveMethod.GetParameters();
                    var removeArgs = new object[removeParams.Length];
                    for (int i = 0; i < removeParams.Length; i++)
                        removeArgs[i] = removeParams[i].DefaultValue;
                    _emRemoveMethod.Invoke(effectModel, removeArgs);
                }

                // popup.FinishTask(true)
                if (_clpFinishTaskMethod != null)
                {
                    _clpFinishTaskMethod.Invoke(limitPopup, new object[] { true });
                }

                // popup.Hide()
                if (_popupHideMethod != null)
                {
                    _popupHideMethod.Invoke(limitPopup, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: RemoveAndConfirm failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancel the limit popup (resolves the async task as false).
        /// </summary>
        public static bool CancelLimitPopup(object limitPopup)
        {
            if (limitPopup == null) return false;
            EnsureTypesCached();

            try
            {
                // popup.FinishTask(false)
                if (_clpFinishTaskMethod != null)
                {
                    _clpFinishTaskMethod.Invoke(limitPopup, new object[] { false });
                }

                // popup.Hide()
                if (_popupHideMethod != null)
                {
                    _popupHideMethod.Invoke(limitPopup, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CornerstoneReflection: CancelLimitPopup failed: {ex.Message}");
                return false;
            }
        }
    }
}
