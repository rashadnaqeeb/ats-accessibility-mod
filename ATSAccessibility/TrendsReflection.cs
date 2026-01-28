using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing TrendsPopup and storage operations data.
    /// </summary>
    public static class TrendsReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public struct AggregatedOperation
        {
            public int TotalAmount;    // Sum of all amounts for this source
            public string DisplayName; // Localized display name (building name, "Consumption", etc.)
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // TrendsPopup
        private static Type _trendsPopupType = null;
        private static FieldInfo _currentGoodField = null;

        // IGameServices properties
        private static PropertyInfo _gsStateServiceProperty = null;
        private static PropertyInfo _gsStorageOperationsServiceProperty = null;

        // IStateService
        private static PropertyInfo _stateServiceTrendsProperty = null;

        // TrendsState
        private static FieldInfo _trendsGoodsOperationsField = null;
        private static FieldInfo _trendsTotalTicksField = null;

        // StorageOperation fields
        private static FieldInfo _opAmountField = null;
        private static FieldInfo _opTrendTickField = null;

        // IStorageOperationsService
        private static MethodInfo _getDisplayNameMethod = null;

        // GoodModel for display names
        private static MethodInfo _getGoodMethod = null;
        private static FieldInfo _goodDisplayNameField = null;
        private static PropertyInfo _locaTextProperty = null;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // TrendsPopup
                _trendsPopupType = assembly.GetType("Eremite.View.Trends.TrendsPopup");
                if (_trendsPopupType != null)
                {
                    _currentGoodField = _trendsPopupType.GetField("currentGood",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                // IGameServices
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsStateServiceProperty = gameServicesType.GetProperty("StateService");
                    _gsStorageOperationsServiceProperty = gameServicesType.GetProperty("StorageOperationsService");
                }

                // IStateService
                var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
                if (stateServiceType != null)
                {
                    _stateServiceTrendsProperty = stateServiceType.GetProperty("Trends");
                }

                // TrendsState
                var trendsStateType = assembly.GetType("Eremite.Model.State.TrendsState");
                if (trendsStateType != null)
                {
                    _trendsGoodsOperationsField = trendsStateType.GetField("goodsOperations");
                    _trendsTotalTicksField = trendsStateType.GetField("totalTicks");
                }

                // StorageOperation
                var storageOpType = assembly.GetType("Eremite.Model.StorageOperation");
                if (storageOpType != null)
                {
                    _opAmountField = storageOpType.GetField("amount");
                    _opTrendTickField = storageOpType.GetField("trendTick");
                }

                // IStorageOperationsService
                var storageOpsServiceType = assembly.GetType("Eremite.Services.IStorageOperationsService");
                if (storageOpsServiceType != null)
                {
                    _getDisplayNameMethod = storageOpsServiceType.GetMethod("GetDisplayName");
                }

                // Settings for good display names
                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _getGoodMethod = settingsType.GetMethod("GetGood", new[] { typeof(string) });
                }

                // GoodModel
                var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
                if (goodModelType != null)
                {
                    _goodDisplayNameField = goodModelType.GetField("displayName");
                }

                // LocaText
                var locaTextType = assembly.GetType("Eremite.Model.LocaText");
                if (locaTextType != null)
                {
                    _locaTextProperty = locaTextType.GetProperty("Text");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TrendsReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetStateService()
        {
            EnsureCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null) return null;
            return _gsStateServiceProperty?.GetValue(gameServices);
        }

        private static object GetTrendsState()
        {
            var stateService = GetStateService();
            if (stateService == null) return null;
            return _stateServiceTrendsProperty?.GetValue(stateService);
        }

        private static object GetStorageOperationsService()
        {
            EnsureCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null) return null;
            return _gsStorageOperationsServiceProperty?.GetValue(gameServices);
        }

        private static string GetLocaText(object locaText)
        {
            if (locaText == null) return null;
            return _locaTextProperty?.GetValue(locaText) as string;
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a TrendsPopup.
        /// </summary>
        public static bool IsTrendsPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_trendsPopupType == null) return false;
            return _trendsPopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Get the currently selected good from the TrendsPopup.
        /// </summary>
        public static string GetCurrentGood(object popup)
        {
            if (popup == null) return null;
            EnsureCached();
            if (_currentGoodField == null) return null;

            try
            {
                return _currentGoodField.GetValue(popup) as string;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetCurrentGood failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // GOODS LIST
        // ========================================

        /// <summary>
        /// Get list of all goods that have trend data.
        /// </summary>
        public static List<string> GetAllGoods()
        {
            var result = new List<string>();

            try
            {
                var trendsState = GetTrendsState();
                if (trendsState == null) return result;

                var opsDict = _trendsGoodsOperationsField?.GetValue(trendsState);
                if (opsDict == null) return result;

                // Get Keys from the dictionary
                var keysProperty = opsDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(opsDict) as IEnumerable;
                if (keys == null) return result;

                foreach (var key in keys)
                {
                    if (key is string goodName)
                    {
                        result.Add(goodName);
                    }
                }

                result.Sort();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the localized display name for a good.
        /// </summary>
        public static string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return goodName;
            EnsureCached();

            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null || _getGoodMethod == null) return goodName;

                var goodModel = _getGoodMethod.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return goodName;

                var locaText = _goodDisplayNameField?.GetValue(goodModel);
                return GetLocaText(locaText) ?? goodName;
            }
            catch
            {
                return goodName;
            }
        }

        // ========================================
        // OPERATIONS DATA
        // ========================================

        /// <summary>
        /// Get the current total ticks count.
        /// </summary>
        public static int GetTotalTicks()
        {
            try
            {
                var trendsState = GetTrendsState();
                if (trendsState == null) return 0;

                var result = _trendsTotalTicksField?.GetValue(trendsState);
                return result is int i ? i : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get aggregated operations for a good within a time range.
        /// Uses the game's StorageOperationsService.GetDisplayName for labels.
        /// </summary>
        /// <param name="goodName">The good to get operations for</param>
        /// <param name="tickCount">Number of ticks to look back (1, 6, or 30)</param>
        /// <returns>List of operations aggregated by display name, sorted by amount</returns>
        public static List<AggregatedOperation> GetAggregatedOperations(string goodName, int tickCount)
        {
            var result = new List<AggregatedOperation>();
            if (string.IsNullOrEmpty(goodName)) return result;

            EnsureCached();

            try
            {
                var trendsState = GetTrendsState();
                if (trendsState == null) return result;

                var opsDict = _trendsGoodsOperationsField?.GetValue(trendsState);
                if (opsDict == null) return result;

                // Get the operations list for this good
                var indexer = opsDict.GetType().GetMethod("get_Item");
                var containsKey = opsDict.GetType().GetMethod("ContainsKey");
                if (indexer == null || containsKey == null) return result;

                var hasKey = containsKey.Invoke(opsDict, new object[] { goodName });
                if (!(hasKey is bool b && b)) return result;

                var opsList = indexer.Invoke(opsDict, new object[] { goodName }) as IEnumerable;
                if (opsList == null) return result;

                int totalTicks = GetTotalTicks();
                int minTick = totalTicks - tickCount;

                // Get the display name service
                var opsService = GetStorageOperationsService();

                // Aggregate by display name
                var nameAmounts = new Dictionary<string, int>();

                foreach (var op in opsList)
                {
                    if (op == null) continue;

                    var tickVal = _opTrendTickField?.GetValue(op);
                    int tick = tickVal is int t ? t : 0;

                    if (tick < minTick) continue;

                    var amountVal = _opAmountField?.GetValue(op);
                    int amount = amountVal is int a ? a : 0;

                    // Get the game's display name for this operation
                    string displayName = GetOperationDisplayName(opsService, op);

                    if (nameAmounts.ContainsKey(displayName))
                        nameAmounts[displayName] += amount;
                    else
                        nameAmounts[displayName] = amount;
                }

                foreach (var kvp in nameAmounts)
                {
                    if (kvp.Value == 0) continue; // Skip zero-sum operations

                    result.Add(new AggregatedOperation
                    {
                        TotalAmount = kvp.Value,
                        DisplayName = kvp.Key
                    });
                }

                // Sort: gains first (descending), then losses (ascending by absolute value)
                result.Sort((a, b) =>
                {
                    bool aPositive = a.TotalAmount > 0;
                    bool bPositive = b.TotalAmount > 0;

                    if (aPositive && !bPositive) return -1;
                    if (!aPositive && bPositive) return 1;

                    // Both same sign - sort by absolute amount descending
                    return Math.Abs(b.TotalAmount).CompareTo(Math.Abs(a.TotalAmount));
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAggregatedOperations failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the game's display name for a storage operation via reflection.
        /// Falls back to "Unknown" if the service isn't available.
        /// </summary>
        private static string GetOperationDisplayName(object opsService, object operation)
        {
            if (opsService == null || _getDisplayNameMethod == null || operation == null)
                return "Unknown";

            try
            {
                var name = _getDisplayNameMethod.Invoke(opsService, new[] { operation }) as string;
                return !string.IsNullOrEmpty(name) ? name : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
