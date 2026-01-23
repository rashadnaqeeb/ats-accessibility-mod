using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing WildcardPopup data and interaction.
    /// Provides methods to query available wildcard buildings, toggle selections,
    /// and confirm picks.
    /// </summary>
    public static class WildcardReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public class BuildingInfo
        {
            public object Model;
            public string DisplayName;
            public string CategoryName;
            public int CategoryOrder;
            public int BuildingOrder;
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // BiomeService access: IGameServices.BiomeService
        private static PropertyInfo _gsBiomeServiceProperty = null;

        // IBiomeService.Blueprints
        private static PropertyInfo _bsBlueprintsProperty = null;

        // BiomeBlueprintsConfig.wildcards (public field)
        private static FieldInfo _bbcWildcardsField = null;

        // BuildingWeightedChance.building (public field)
        private static FieldInfo _bwcBuildingField = null;

        // EffectsService access: IGameServices.EffectsService
        private static PropertyInfo _gsEffectsServiceProperty = null;

        // IEffectsService.GetWildcardPicksLeft()
        private static MethodInfo _esGetWildcardPicksLeftMethod = null;

        // MetaConditionsService access: IMetaServices.MetaConditionsService
        private static PropertyInfo _msMetaConditionsServiceProperty = null;

        // IMetaConditionsService.IsUnlocked(BuildingModel) - cached at runtime
        private static MethodInfo _mcsIsUnlockedMethod = null;

        // WildcardPopup.slots (private serialized field)
        private static FieldInfo _wpSlotsField = null;

        // WildcardPopup.picks (private field)
        private static FieldInfo _wpPicksField = null;

        // WildcardPopup.OnSlotClicked (private method)
        private static MethodInfo _wpOnSlotClickedMethod = null;

        // WildcardPopup.Confirm (private method)
        private static MethodInfo _wpConfirmMethod = null;

        // WildcardSlot.GetModel (public method)
        private static MethodInfo _wsGetModelMethod = null;

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
                    Debug.LogWarning("[ATSAccessibility] WildcardReflection: Game assembly not available");
                    return;
                }

                CacheServiceProperties(assembly);
                CacheWildcardDataTypes(assembly);
                CachePopupTypes(assembly);
                CacheMetaTypes(assembly);

                Debug.Log("[ATSAccessibility] WildcardReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheServiceProperties(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
            if (effectsServiceType != null)
            {
                _esGetWildcardPicksLeftMethod = effectsServiceType.GetMethod("GetWildcardPicksLeft",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheWildcardDataTypes(Assembly assembly)
        {
            // IBiomeService.Blueprints
            var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
            if (biomeServiceType != null)
            {
                _bsBlueprintsProperty = biomeServiceType.GetProperty("Blueprints",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // BiomeBlueprintsConfig.wildcards
            var blueprintsConfigType = assembly.GetType("Eremite.Model.Configs.BiomeBlueprintsConfig");
            if (blueprintsConfigType != null)
            {
                _bbcWildcardsField = blueprintsConfigType.GetField("wildcards",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // BuildingWeightedChance.building
            var bwcType = assembly.GetType("Eremite.Model.BuildingWeightedChance");
            if (bwcType != null)
            {
                _bwcBuildingField = bwcType.GetField("building",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            // WildcardPopup fields and methods
            var wildcardPopupType = assembly.GetType("Eremite.View.HUD.WildcardPopup");
            if (wildcardPopupType != null)
            {
                _wpSlotsField = wildcardPopupType.GetField("slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _wpPicksField = wildcardPopupType.GetField("picks",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _wpOnSlotClickedMethod = wildcardPopupType.GetMethod("OnSlotClicked",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                _wpConfirmMethod = wildcardPopupType.GetMethod("Confirm",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            // WildcardSlot.GetModel
            var wildcardSlotType = assembly.GetType("Eremite.View.HUD.WildcardSlot");
            if (wildcardSlotType != null)
            {
                _wsGetModelMethod = wildcardSlotType.GetMethod("GetModel",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheMetaTypes(Assembly assembly)
        {
            // IMetaServices.MetaConditionsService
            var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
            if (metaServicesType != null)
            {
                _msMetaConditionsServiceProperty = metaServicesType.GetProperty("MetaConditionsService",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // IMetaConditionsService.IsUnlocked(BuildingModel)
            var metaConditionsServiceType = assembly.GetType("Eremite.Services.IMetaConditionsService");
            var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
            if (metaConditionsServiceType != null && buildingModelType != null)
            {
                _mcsIsUnlockedMethod = metaConditionsServiceType.GetMethod("IsUnlocked",
                    new[] { buildingModelType });
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetBiomeService()
        {
            EnsureTypesCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsBiomeServiceProperty == null) return null;
            try { return _gsBiomeServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetEffectsService()
        {
            EnsureTypesCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsEffectsServiceProperty == null) return null;
            try { return _gsEffectsServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetMetaConditionsService()
        {
            EnsureTypesCached();

            try
            {
                var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null) return null;

                return _msMetaConditionsServiceProperty?.GetValue(metaServices);
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a WildcardPopup.
        /// </summary>
        public static bool IsWildcardPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "WildcardPopup";
        }

        // ========================================
        // DATA ACCESS
        // ========================================

        /// <summary>
        /// Get available buildings for wildcard selection.
        /// Filters by CanBePicked: not already unlocked in game AND unlocked in meta progression.
        /// </summary>
        public static List<BuildingInfo> GetAvailableBuildings()
        {
            EnsureTypesCached();

            var result = new List<BuildingInfo>();

            var biomeService = GetBiomeService();
            if (biomeService == null || _bsBlueprintsProperty == null) return result;

            try
            {
                var blueprintsConfig = _bsBlueprintsProperty.GetValue(biomeService);
                if (blueprintsConfig == null) return result;

                var wildcards = _bbcWildcardsField?.GetValue(blueprintsConfig) as Array;
                if (wildcards == null) return result;

                foreach (var bwc in wildcards)
                {
                    if (bwc == null) continue;

                    var buildingModel = _bwcBuildingField?.GetValue(bwc);
                    if (buildingModel == null) continue;

                    // CanBePicked: NOT already unlocked in game AND IS unlocked in meta
                    if (GameReflection.IsBuildingUnlocked(buildingModel)) continue;
                    if (!IsMetaUnlocked(buildingModel)) continue;

                    var displayName = GameReflection.GetDisplayName(buildingModel) ??
                                      GameReflection.GetModelName(buildingModel) ?? "Unknown";
                    var category = GameReflection.GetBuildingCategory(buildingModel);
                    var categoryName = category != null ? (GameReflection.GetDisplayName(category) ?? "Other") : "Other";
                    var categoryOrder = category != null ? GameReflection.GetModelOrder(category) : 999;
                    var buildingOrder = GameReflection.GetModelOrder(buildingModel);

                    result.Add(new BuildingInfo
                    {
                        Model = buildingModel,
                        DisplayName = displayName,
                        CategoryName = categoryName,
                        CategoryOrder = categoryOrder,
                        BuildingOrder = buildingOrder
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: GetAvailableBuildings failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the number of picks required (remaining wildcard picks).
        /// </summary>
        public static int GetPicksRequired()
        {
            EnsureTypesCached();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esGetWildcardPicksLeftMethod == null) return 0;

            try
            {
                var result = _esGetWildcardPicksLeftMethod.Invoke(effectsService, null);
                return result is int picks ? picks : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: GetPicksRequired failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Check if a building model is unlocked in meta progression.
        /// </summary>
        public static bool IsMetaUnlocked(object buildingModel)
        {
            if (buildingModel == null) return false;

            var metaConditionsService = GetMetaConditionsService();
            if (metaConditionsService == null || _mcsIsUnlockedMethod == null) return false;

            try
            {
                var result = _mcsIsUnlockedMethod.Invoke(metaConditionsService, new[] { buildingModel });
                return result is bool unlocked && unlocked;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // POPUP INTERACTION
        // ========================================

        /// <summary>
        /// Toggle selection of a building in the popup by finding its slot and clicking it.
        /// Returns true if the toggle was performed.
        /// </summary>
        public static bool ToggleSlot(object popup, object buildingModel)
        {
            if (popup == null || buildingModel == null) return false;
            EnsureTypesCached();

            if (_wpSlotsField == null || _wpOnSlotClickedMethod == null || _wsGetModelMethod == null)
                return false;

            try
            {
                var slots = _wpSlotsField.GetValue(popup) as IList;
                if (slots == null) return false;

                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    // Check if this slot's GameObject is active
                    var slotComponent = slot as Component;
                    if (slotComponent != null && !slotComponent.gameObject.activeInHierarchy)
                        continue;

                    var slotModel = _wsGetModelMethod.Invoke(slot, null);
                    if (slotModel == buildingModel)
                    {
                        _wpOnSlotClickedMethod.Invoke(popup, new[] { slot });
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: ToggleSlot failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get the current number of picks in the popup.
        /// </summary>
        public static int GetCurrentPickCount(object popup)
        {
            if (popup == null) return 0;
            EnsureTypesCached();

            if (_wpPicksField == null) return 0;

            try
            {
                var picks = _wpPicksField.GetValue(popup) as IList;
                return picks?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the list of currently picked building models from the popup.
        /// </summary>
        public static List<object> GetCurrentPicks(object popup)
        {
            var result = new List<object>();
            if (popup == null) return result;
            EnsureTypesCached();

            if (_wpPicksField == null) return result;

            try
            {
                var picks = _wpPicksField.GetValue(popup) as IList;
                if (picks == null) return result;

                foreach (var pick in picks)
                {
                    if (pick != null)
                        result.Add(pick);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: GetCurrentPicks failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Confirm the wildcard picks. Only succeeds if pick count matches required.
        /// Returns true if confirm was called.
        /// </summary>
        public static bool Confirm(object popup)
        {
            if (popup == null) return false;
            EnsureTypesCached();

            if (_wpConfirmMethod == null) return false;

            try
            {
                int currentCount = GetCurrentPickCount(popup);
                int required = GetPicksRequired();

                if (currentCount != required) return false;

                _wpConfirmMethod.Invoke(popup, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WildcardReflection: Confirm failed: {ex.Message}");
                return false;
            }
        }
    }
}
