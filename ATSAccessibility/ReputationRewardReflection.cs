using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing ReputationRewardsPopup data and interaction.
    /// Provides methods to query current reward options, pick buildings, reroll, and extend.
    /// </summary>
    public static class ReputationRewardReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public class RewardOption
        {
            public object Model;         // BuildingModel
            public string DisplayName;
            public string Description;   // ListDescription
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // IReputationRewardsService methods
        private static MethodInfo _rrsGetCurrentPicksMethod = null;
        private static MethodInfo _rrsCanAffordRerollMethod = null;
        private static MethodInfo _rrsRerollMethod = null;
        private static MethodInfo _rrsGetRerollPriceMethod = null;
        private static MethodInfo _rrsCanExtendMethod = null;
        private static MethodInfo _rrsCanAffordExtendMethod = null;
        private static MethodInfo _rrsExtendMethod = null;

        // ReputationReward.building (public field)
        private static FieldInfo _rrBuildingField = null;

        // Settings.GetBuilding(string)
        private static MethodInfo _settingsGetBuildingMethod = null;

        // BuildingModel.ListDescription (public virtual property)
        private static PropertyInfo _bmListDescriptionProperty = null;

        // Good struct fields (for reroll price)
        private static FieldInfo _goodNameField = null;
        private static FieldInfo _goodAmountField = null;

        // BiomeService/Blueprints for extend cost
        private static PropertyInfo _gsBiomeServiceProperty = null;
        private static PropertyInfo _bsBlueprintsProperty = null;
        private static FieldInfo _bbcExtendCostField = null;

        // GoodRef fields (for extend cost)
        private static FieldInfo _grGoodField = null;
        private static FieldInfo _grAmountField = null;

        // ReputationRewardsPopup methods (private)
        private static MethodInfo _rpOnBuildingPickedMethod = null;
        private static MethodInfo _rpRerollMethod = null;

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
                    Debug.LogWarning("[ATSAccessibility] ReputationRewardReflection: Game assembly not available");
                    return;
                }

                CacheServiceMethods(assembly);
                CacheRewardTypes(assembly);
                CacheSettingsMethods(assembly);
                CacheBuildingModelTypes(assembly);
                CacheGoodTypes(assembly);
                CacheBiomeTypes(assembly);
                CachePopupTypes(assembly);

                Debug.Log("[ATSAccessibility] ReputationRewardReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheServiceMethods(Assembly assembly)
        {
            var serviceType = assembly.GetType("Eremite.Services.IReputationRewardsService");
            if (serviceType == null) return;

            _rrsGetCurrentPicksMethod = serviceType.GetMethod("GetCurrentPicks",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsCanAffordRerollMethod = serviceType.GetMethod("CanAffordReroll",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsRerollMethod = serviceType.GetMethod("Reroll",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsGetRerollPriceMethod = serviceType.GetMethod("GetRerollPrice",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsCanExtendMethod = serviceType.GetMethod("CanExtend",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsCanAffordExtendMethod = serviceType.GetMethod("CanAffordExtend",
                BindingFlags.Public | BindingFlags.Instance);
            _rrsExtendMethod = serviceType.GetMethod("Extend",
                BindingFlags.Public | BindingFlags.Instance);
        }

        private static void CacheRewardTypes(Assembly assembly)
        {
            var rewardType = assembly.GetType("Eremite.Model.State.ReputationReward");
            if (rewardType != null)
            {
                _rrBuildingField = rewardType.GetField("building",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheSettingsMethods(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsGetBuildingMethod = settingsType.GetMethod("GetBuilding",
                    new[] { typeof(string) });
            }
        }

        private static void CacheBuildingModelTypes(Assembly assembly)
        {
            var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
            if (buildingModelType != null)
            {
                _bmListDescriptionProperty = buildingModelType.GetProperty("ListDescription",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheGoodTypes(Assembly assembly)
        {
            var goodType = assembly.GetType("Eremite.Model.Good");
            if (goodType != null)
            {
                _goodNameField = goodType.GetField("name",
                    BindingFlags.Public | BindingFlags.Instance);
                _goodAmountField = goodType.GetField("amount",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheBiomeTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
            if (biomeServiceType != null)
            {
                _bsBlueprintsProperty = biomeServiceType.GetProperty("Blueprints",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            var blueprintsConfigType = assembly.GetType("Eremite.Model.Configs.BiomeBlueprintsConfig");
            if (blueprintsConfigType != null)
            {
                _bbcExtendCostField = blueprintsConfigType.GetField("extendCost",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
            if (goodRefType != null)
            {
                _grGoodField = goodRefType.GetField("good",
                    BindingFlags.Public | BindingFlags.Instance);
                _grAmountField = goodRefType.GetField("amount",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            var popupType = assembly.GetType("Eremite.View.HUD.ReputationRewardsPopup");
            if (popupType != null)
            {
                _rpOnBuildingPickedMethod = popupType.GetMethod("OnBuildingPicked",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _rpRerollMethod = popupType.GetMethod("Reroll",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetService()
        {
            return GameReflection.GetReputationRewardsService();
        }

        private static object GetBiomeService()
        {
            EnsureTypesCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsBiomeServiceProperty == null) return null;
            try { return _gsBiomeServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a ReputationRewardsPopup.
        /// </summary>
        public static bool IsReputationRewardsPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "ReputationRewardsPopup";
        }

        // ========================================
        // DATA ACCESS
        // ========================================

        /// <summary>
        /// Get the current reward options (buildings to choose from).
        /// Returns null/empty if no current pick is available.
        /// </summary>
        public static List<RewardOption> GetCurrentOptions()
        {
            EnsureTypesCached();

            var result = new List<RewardOption>();
            var service = GetService();
            if (service == null || _rrsGetCurrentPicksMethod == null) return result;

            try
            {
                var picks = _rrsGetCurrentPicksMethod.Invoke(service, null) as IList;
                if (picks == null || picks.Count == 0) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null || _settingsGetBuildingMethod == null) return result;

                foreach (var pick in picks)
                {
                    if (pick == null) continue;

                    var buildingName = _rrBuildingField?.GetValue(pick) as string;
                    if (string.IsNullOrEmpty(buildingName)) continue;

                    var buildingModel = _settingsGetBuildingMethod.Invoke(settings, new object[] { buildingName });
                    if (buildingModel == null) continue;

                    var displayName = GameReflection.GetDisplayName(buildingModel) ?? buildingName;
                    var description = _bmListDescriptionProperty?.GetValue(buildingModel) as string;

                    result.Add(new RewardOption
                    {
                        Model = buildingModel,
                        DisplayName = displayName,
                        Description = description
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetCurrentOptions failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Pick a building from the popup. Invokes the popup's OnBuildingPicked method
        /// which calls service.RewardPicked and then either shows next reward or hides popup.
        /// </summary>
        public static bool PickBuilding(object popup, object buildingModel)
        {
            if (popup == null || buildingModel == null) return false;
            EnsureTypesCached();

            if (_rpOnBuildingPickedMethod == null) return false;

            try
            {
                _rpOnBuildingPickedMethod.Invoke(popup, new[] { buildingModel });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: PickBuilding failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // REROLL
        // ========================================

        /// <summary>
        /// Check if the player can afford to reroll.
        /// </summary>
        public static bool CanAffordReroll()
        {
            EnsureTypesCached();
            var service = GetService();
            if (service == null || _rrsCanAffordRerollMethod == null) return false;

            try
            {
                return (bool)_rrsCanAffordRerollMethod.Invoke(service, null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Reroll the current reward options via the popup's own method.
        /// This keeps the popup's UI in sync (price slot updates with increasing cost).
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
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: Reroll failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the reroll cost as (amount, good display name).
        /// </summary>
        public static (int amount, string goodDisplayName) GetRerollCost()
        {
            EnsureTypesCached();
            var service = GetService();
            if (service == null || _rrsGetRerollPriceMethod == null) return (0, "Unknown");

            try
            {
                var good = _rrsGetRerollPriceMethod.Invoke(service, null);
                if (good == null) return (0, "Unknown");

                var name = _goodNameField?.GetValue(good) as string ?? "";
                var amount = _goodAmountField != null ? (int)_goodAmountField.GetValue(good) : 0;
                var displayName = GameReflection.GetGoodDisplayName(name);

                return (amount, displayName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetRerollCost failed: {ex.Message}");
                return (0, "Unknown");
            }
        }

        // ========================================
        // EXTEND
        // ========================================

        /// <summary>
        /// Check if extending is available (effect enabled + not already extended).
        /// </summary>
        public static bool CanExtend()
        {
            EnsureTypesCached();
            var service = GetService();
            if (service == null || _rrsCanExtendMethod == null) return false;

            try
            {
                return (bool)_rrsCanExtendMethod.Invoke(service, null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if the player can afford to extend.
        /// </summary>
        public static bool CanAffordExtend()
        {
            EnsureTypesCached();
            var service = GetService();
            if (service == null || _rrsCanAffordExtendMethod == null) return false;

            try
            {
                return (bool)_rrsCanAffordExtendMethod.Invoke(service, null);
            }
            catch { return false; }
        }

        /// <summary>
        /// Extend the current reward options (add one more building choice).
        /// </summary>
        public static bool Extend()
        {
            EnsureTypesCached();
            var service = GetService();
            if (service == null || _rrsExtendMethod == null) return false;

            try
            {
                _rrsExtendMethod.Invoke(service, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: Extend failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the extend cost as (amount, good display name).
        /// Reads from BiomeService.Blueprints.extendCost (GoodRef).
        /// </summary>
        public static (int amount, string goodDisplayName) GetExtendCost()
        {
            EnsureTypesCached();

            try
            {
                var biomeService = GetBiomeService();
                if (biomeService == null || _bsBlueprintsProperty == null) return (0, "Unknown");

                var blueprints = _bsBlueprintsProperty.GetValue(biomeService);
                if (blueprints == null || _bbcExtendCostField == null) return (0, "Unknown");

                var extendCost = _bbcExtendCostField.GetValue(blueprints);
                if (extendCost == null) return (0, "Unknown");

                var amount = _grAmountField != null ? (int)_grAmountField.GetValue(extendCost) : 0;
                var goodModel = _grGoodField?.GetValue(extendCost);
                var displayName = goodModel != null ? (GameReflection.GetDisplayName(goodModel) ?? "Unknown") : "Unknown";

                return (amount, displayName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetExtendCost failed: {ex.Message}");
                return (0, "Unknown");
            }
        }
    }
}
