using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to Ironman (Queen's Hand Trial) upgrade popup.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class IronmanReflection
    {
        public struct UpgradeInfo
        {
            public string Name;
            public string PriceText;
            public bool CanAfford;
            public bool IsUnlocked;
            public bool IsCore;
            public object UpgradeObj;
        }

        public struct RewardInfo
        {
            public string Name;
            public string Description;
        }

        // Popup detection
        private static Type _ironmanUpgradePopupType = null;

        // IronmanService access (via MetaServices, not WorldServices)
        private static PropertyInfo _msIronmanServiceProperty = null;
        private static MethodInfo _getCompletedPicksMethod = null;
        private static MethodInfo _hasReachedMaxPicksMethod = null;
        private static MethodInfo _getCurrentPickMethod = null;
        private static MethodInfo _canAffordMethod = null;
        private static MethodInfo _pickMethod = null;
        private static MethodInfo _isUnlockedMethod = null;
        private static MethodInfo _isCoreMethod = null;

        // IronmanConfig access
        private static FieldInfo _settingsIronmanConfigField = null;
        private static FieldInfo _configCoreUpgradesField = null;
        private static FieldInfo _configPicksField = null;

        // IronmanPickState access
        private static FieldInfo _pickStateOptionsField = null;

        // IronmanPickOption access
        private static FieldInfo _optionModelField = null;

        // Settings.GetCapitalUpgrade method
        private static MethodInfo _getCapitalUpgradeMethod = null;

        // CapitalUpgradeModel fields
        private static FieldInfo _ironmanDisplayNameField = null;
        private static FieldInfo _ironmanPriceField = null;
        private static FieldInfo _upgradeRewardsField = null;

        // MetaCurrencyRef fields (reuse from CapitalUpgradeReflection pattern)
        private static FieldInfo _currencyRefCurrencyField = null;
        private static FieldInfo _currencyRefAmountField = null;
        private static PropertyInfo _currencyModelDisplayNameProperty = null;

        // MetaRewardModel properties
        private static PropertyInfo _rewardDisplayNameProperty = null;
        private static PropertyInfo _rewardDescriptionProperty = null;

        // MetaStateService.Capital.unlockedUpgrades access
        private static PropertyInfo _msMetaStateServiceProperty = null;
        private static PropertyInfo _metaStateCapitalProperty = null;
        private static FieldInfo _capitalUnlockedUpgradesField = null;

        private static bool _typesCached = false;

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var gameAssembly = GameReflection.GameAssembly;
            if (gameAssembly == null)
            {
                _typesCached = true;
                return;
            }

            try
            {
                // Cache IronmanUpgradePopup type for popup detection
                _ironmanUpgradePopupType = gameAssembly.GetType("Eremite.WorldMap.UI.IronmanUpgradePopup");

                // Cache IMetaServices properties (IronmanService and MetaStateService)
                var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msIronmanServiceProperty = metaServicesType.GetProperty("IronmanService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache IIronmanService methods
                var ironmanServiceType = gameAssembly.GetType("Eremite.Services.IIronmanService");
                if (ironmanServiceType != null)
                {
                    _getCompletedPicksMethod = ironmanServiceType.GetMethod("GetCompletedPicks",
                        BindingFlags.Public | BindingFlags.Instance);
                    _hasReachedMaxPicksMethod = ironmanServiceType.GetMethod("HasReachedMaxPicks",
                        BindingFlags.Public | BindingFlags.Instance);
                    _getCurrentPickMethod = ironmanServiceType.GetMethod("GetCurrentPick",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Some methods are on the concrete IronmanService class
                var ironmanServiceImplType = gameAssembly.GetType("Eremite.Services.IronmanService");
                if (ironmanServiceImplType != null)
                {
                    var upgradeModelType = gameAssembly.GetType("Eremite.WorldMap.CapitalUpgradeModel");
                    if (upgradeModelType != null)
                    {
                        _canAffordMethod = ironmanServiceImplType.GetMethod("CanAfford",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { upgradeModelType }, null);
                        _pickMethod = ironmanServiceImplType.GetMethod("Pick",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { upgradeModelType }, null);
                        _isUnlockedMethod = ironmanServiceImplType.GetMethod("IsUnlocked",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { upgradeModelType }, null);
                        _isCoreMethod = ironmanServiceImplType.GetMethod("IsCore",
                            BindingFlags.Public | BindingFlags.Instance, null, new[] { upgradeModelType }, null);
                    }
                }

                // Cache Settings.ironmanConfig field
                var settingsType = gameAssembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsIronmanConfigField = settingsType.GetField("ironmanConfig",
                        BindingFlags.Public | BindingFlags.Instance);

                    // Settings.GetCapitalUpgrade(string name) method
                    _getCapitalUpgradeMethod = settingsType.GetMethod("GetCapitalUpgrade",
                        BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                }

                // Cache IronmanConfig fields
                var ironmanConfigType = gameAssembly.GetType("Eremite.Model.Configs.IronmanConfig");
                if (ironmanConfigType != null)
                {
                    _configCoreUpgradesField = ironmanConfigType.GetField("coreUpgrades",
                        BindingFlags.Public | BindingFlags.Instance);
                    _configPicksField = ironmanConfigType.GetField("picks",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache IronmanPickState.options field
                var pickStateType = gameAssembly.GetType("Eremite.Model.State.IronmanPickState");
                if (pickStateType != null)
                {
                    _pickStateOptionsField = pickStateType.GetField("options",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache IronmanPickOption.model field
                var pickOptionType = gameAssembly.GetType("Eremite.Model.State.IronmanPickOption");
                if (pickOptionType != null)
                {
                    _optionModelField = pickOptionType.GetField("model",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache CapitalUpgradeModel fields
                var capitalUpgradeModelType = gameAssembly.GetType("Eremite.WorldMap.CapitalUpgradeModel");
                if (capitalUpgradeModelType != null)
                {
                    _ironmanDisplayNameField = capitalUpgradeModelType.GetField("ironmanDisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _ironmanPriceField = capitalUpgradeModelType.GetField("ironmanPrice",
                        BindingFlags.Public | BindingFlags.Instance);
                    _upgradeRewardsField = capitalUpgradeModelType.GetField("rewards",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaCurrencyRef fields
                var currencyRefType = gameAssembly.GetType("Eremite.Model.MetaCurrencyRef");
                if (currencyRefType != null)
                {
                    _currencyRefCurrencyField = currencyRefType.GetField("currency",
                        BindingFlags.Public | BindingFlags.Instance);
                    _currencyRefAmountField = currencyRefType.GetField("amount",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaCurrencyModel.DisplayName
                var currencyModelType = gameAssembly.GetType("Eremite.Model.MetaCurrencyModel");
                if (currencyModelType != null)
                {
                    _currencyModelDisplayNameProperty = currencyModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaRewardModel properties
                var rewardModelType = gameAssembly.GetType("Eremite.Model.Meta.MetaRewardModel");
                if (rewardModelType != null)
                {
                    _rewardDisplayNameProperty = rewardModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _rewardDescriptionProperty = rewardModelType.GetProperty("Description",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaStateService.Capital.unlockedUpgrades access
                var metaStateServiceType = gameAssembly.GetType("Eremite.Services.IMetaStateService");
                if (metaStateServiceType != null)
                {
                    _metaStateCapitalProperty = metaStateServiceType.GetProperty("Capital",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                var capitalStateType = gameAssembly.GetType("Eremite.Model.State.CapitalState");
                if (capitalStateType != null)
                {
                    _capitalUnlockedUpgradesField = capitalStateType.GetField("unlockedUpgrades",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached IronmanReflection types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] IronmanReflection type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }

        /// <summary>
        /// Check if a popup object is an IronmanUpgradePopup.
        /// </summary>
        public static bool IsIronmanUpgradePopup(object popup)
        {
            if (popup == null) return false;
            EnsureTypes();
            if (_ironmanUpgradePopupType == null) return false;
            return _ironmanUpgradePopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Get the IronmanService from MetaServices.
        /// </summary>
        private static object GetIronmanService()
        {
            EnsureTypes();
            var ms = GameReflection.GetMetaServices();
            if (ms == null || _msIronmanServiceProperty == null)
                return null;

            try
            {
                return _msIronmanServiceProperty.GetValue(ms);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetIronmanService failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the number of completed picks.
        /// </summary>
        public static int GetCompletedPicks()
        {
            EnsureTypes();
            var service = GetIronmanService();
            if (service == null || _getCompletedPicksMethod == null) return 0;

            try
            {
                return (int)_getCompletedPicksMethod.Invoke(service, null);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the maximum number of picks allowed.
        /// </summary>
        public static int GetMaxPicks()
        {
            EnsureTypes();
            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsIronmanConfigField == null) return 0;

            try
            {
                var config = _settingsIronmanConfigField.GetValue(settings);
                if (config == null || _configPicksField == null) return 0;

                var picks = _configPicksField.GetValue(config) as Array;
                return picks?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if player has reached max picks.
        /// </summary>
        public static bool HasReachedMaxPicks()
        {
            EnsureTypes();
            var service = GetIronmanService();
            if (service == null || _hasReachedMaxPicksMethod == null) return true;

            try
            {
                return (bool)_hasReachedMaxPicksMethod.Invoke(service, null);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get the current 3 pick options.
        /// Returns empty list if max picks reached.
        /// </summary>
        public static List<UpgradeInfo> GetCurrentPickOptions()
        {
            EnsureTypes();
            var result = new List<UpgradeInfo>();

            if (HasReachedMaxPicks()) return result;

            var service = GetIronmanService();
            if (service == null || _getCurrentPickMethod == null) return result;

            try
            {
                var pickState = _getCurrentPickMethod.Invoke(service, null);
                if (pickState == null || _pickStateOptionsField == null) return result;

                var options = _pickStateOptionsField.GetValue(pickState) as IList;
                if (options == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null || _getCapitalUpgradeMethod == null) return result;

                foreach (var option in options)
                {
                    if (option == null || _optionModelField == null) continue;

                    string modelName = _optionModelField.GetValue(option) as string;
                    if (string.IsNullOrEmpty(modelName)) continue;

                    var upgradeModel = _getCapitalUpgradeMethod.Invoke(settings, new object[] { modelName });
                    if (upgradeModel == null) continue;

                    result.Add(CreateUpgradeInfo(upgradeModel, service, false));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCurrentPickOptions failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get all core upgrades.
        /// </summary>
        public static List<UpgradeInfo> GetCoreUpgrades()
        {
            EnsureTypes();
            var result = new List<UpgradeInfo>();

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsIronmanConfigField == null) return result;

            var service = GetIronmanService();
            if (service == null) return result;

            try
            {
                var config = _settingsIronmanConfigField.GetValue(settings);
                if (config == null || _configCoreUpgradesField == null) return result;

                var coreUpgrades = _configCoreUpgradesField.GetValue(config) as Array;
                if (coreUpgrades == null) return result;

                foreach (var upgradeModel in coreUpgrades)
                {
                    if (upgradeModel == null) continue;
                    result.Add(CreateUpgradeInfo(upgradeModel, service, true));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCoreUpgrades failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get all unlocked upgrades (both picks and core).
        /// </summary>
        public static List<UpgradeInfo> GetUnlockedUpgrades()
        {
            EnsureTypes();
            var result = new List<UpgradeInfo>();

            var ms = GameReflection.GetMetaServices();
            if (ms == null || _msMetaStateServiceProperty == null) return result;

            var service = GetIronmanService();

            try
            {
                var metaStateService = _msMetaStateServiceProperty.GetValue(ms);
                if (metaStateService == null || _metaStateCapitalProperty == null) return result;

                var capitalState = _metaStateCapitalProperty.GetValue(metaStateService);
                if (capitalState == null || _capitalUnlockedUpgradesField == null) return result;

                var unlockedUpgrades = _capitalUnlockedUpgradesField.GetValue(capitalState);
                if (unlockedUpgrades == null) return result;

                // unlockedUpgrades is a HashSet<string> of upgrade names
                var enumerable = unlockedUpgrades as IEnumerable;
                if (enumerable == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null || _getCapitalUpgradeMethod == null) return result;

                foreach (var upgradeName in enumerable)
                {
                    string name = upgradeName as string;
                    if (string.IsNullOrEmpty(name)) continue;

                    var upgradeModel = _getCapitalUpgradeMethod.Invoke(settings, new object[] { name });
                    if (upgradeModel == null) continue;

                    bool isCore = false;
                    if (service != null && _isCoreMethod != null)
                    {
                        try { isCore = (bool)_isCoreMethod.Invoke(service, new[] { upgradeModel }); }
                        catch { }
                    }

                    result.Add(CreateUpgradeInfo(upgradeModel, service, isCore));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetUnlockedUpgrades failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Create an UpgradeInfo struct from an upgrade model.
        /// </summary>
        private static UpgradeInfo CreateUpgradeInfo(object upgradeModel, object service, bool isCore)
        {
            string name = GetIronmanDisplayName(upgradeModel);
            string priceText = GetIronmanPriceText(upgradeModel);
            bool canAfford = CanAfford(upgradeModel, service);
            bool isUnlocked = IsUnlocked(upgradeModel, service);

            return new UpgradeInfo
            {
                Name = name,
                PriceText = priceText,
                CanAfford = canAfford,
                IsUnlocked = isUnlocked,
                IsCore = isCore,
                UpgradeObj = upgradeModel
            };
        }

        /// <summary>
        /// Get the ironman display name for an upgrade.
        /// </summary>
        public static string GetIronmanDisplayName(object upgradeModel)
        {
            if (upgradeModel == null || _ironmanDisplayNameField == null) return "";

            try
            {
                var displayName = _ironmanDisplayNameField.GetValue(upgradeModel);
                return GameReflection.GetLocaText(displayName) ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Get formatted price text for ironman upgrade.
        /// </summary>
        public static string GetIronmanPriceText(object upgradeModel)
        {
            if (upgradeModel == null || _ironmanPriceField == null) return "";

            try
            {
                var priceArray = _ironmanPriceField.GetValue(upgradeModel) as Array;
                if (priceArray == null || priceArray.Length == 0) return "";

                var parts = new List<string>();

                foreach (var currencyRef in priceArray)
                {
                    if (currencyRef == null) continue;

                    var amount = _currencyRefAmountField?.GetValue(currencyRef);
                    var currencyModel = _currencyRefCurrencyField?.GetValue(currencyRef);

                    if (amount == null || currencyModel == null) continue;

                    string displayName = "";
                    if (_currencyModelDisplayNameProperty != null)
                    {
                        displayName = _currencyModelDisplayNameProperty.GetValue(currencyModel) as string ?? "";
                    }

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        parts.Add($"{(int)amount} {displayName}");
                    }
                }

                return string.Join(", ", parts.ToArray());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetIronmanPriceText failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Check if player can afford an upgrade.
        /// </summary>
        public static bool CanAfford(object upgradeModel, object service = null)
        {
            EnsureTypes();
            service = service ?? GetIronmanService();
            if (service == null || upgradeModel == null || _canAffordMethod == null) return false;

            try
            {
                return (bool)_canAffordMethod.Invoke(service, new[] { upgradeModel });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if an upgrade is already unlocked.
        /// </summary>
        public static bool IsUnlocked(object upgradeModel, object service = null)
        {
            EnsureTypes();
            service = service ?? GetIronmanService();
            if (service == null || upgradeModel == null || _isUnlockedMethod == null) return false;

            try
            {
                return (bool)_isUnlockedMethod.Invoke(service, new[] { upgradeModel });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Purchase an upgrade via IronmanService.Pick().
        /// Returns true if successful.
        /// </summary>
        public static bool Pick(object upgradeModel)
        {
            EnsureTypes();
            var service = GetIronmanService();
            if (service == null || upgradeModel == null || _pickMethod == null) return false;

            try
            {
                _pickMethod.Invoke(service, new[] { upgradeModel });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Pick failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all rewards for an upgrade.
        /// </summary>
        public static List<RewardInfo> GetRewards(object upgradeModel)
        {
            EnsureTypes();
            var result = new List<RewardInfo>();

            if (upgradeModel == null || _upgradeRewardsField == null) return result;

            try
            {
                var rewards = _upgradeRewardsField.GetValue(upgradeModel) as Array;
                if (rewards == null) return result;

                foreach (var reward in rewards)
                {
                    if (reward == null) continue;

                    string name = "";
                    string description = "";

                    if (_rewardDisplayNameProperty != null)
                    {
                        try { name = _rewardDisplayNameProperty.GetValue(reward) as string ?? ""; }
                        catch { }
                    }

                    if (_rewardDescriptionProperty != null)
                    {
                        try { description = _rewardDescriptionProperty.GetValue(reward) as string ?? ""; }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(name))
                    {
                        result.Add(new RewardInfo { Name = name, Description = description });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRewards failed: {ex.Message}");
            }

            return result;
        }
    }
}
