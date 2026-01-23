using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to newcomers popup internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class NewcomersReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // NewcomersPopup type check
        private static Type _newcomersPopupType = null;

        // INewcomersService.PickGroup method
        private static MethodInfo _nsPickGroupMethod = null;

        // IGameServices.NewcomersService property (duplicated from RewardsReflection for independence)
        private static PropertyInfo _gsNewcomersServiceProperty = null;

        // INewcomersService.GetCurrentNewcomers method
        private static MethodInfo _nsGetCurrentNewcomersMethod = null;

        // NewcomersGroup fields
        private static FieldInfo _ngRacesField = null;
        private static FieldInfo _ngGoodsField = null;

        // Good struct fields
        private static FieldInfo _goodNameField = null;
        private static FieldInfo _goodAmountField = null;

        // Popup.Hide method
        private static MethodInfo _popupHideMethod = null;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // NewcomersPopup type
                _newcomersPopupType = assembly.GetType("Eremite.View.HUD.NewcomersPopup");

                // IGameServices.NewcomersService
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsNewcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");
                }

                // INewcomersService methods
                var nsType = assembly.GetType("Eremite.Services.INewcomersService");
                if (nsType != null)
                {
                    _nsPickGroupMethod = nsType.GetMethod("PickGroup");
                    _nsGetCurrentNewcomersMethod = nsType.GetMethod("GetCurrentNewcomers");
                }

                // NewcomersGroup fields
                var ngType = assembly.GetType("Eremite.Model.State.NewcomersGroup");
                if (ngType != null)
                {
                    _ngRacesField = ngType.GetField("races", GameReflection.PublicInstance);
                    _ngGoodsField = ngType.GetField("goods", GameReflection.PublicInstance);
                }

                // Good struct fields
                var goodType = assembly.GetType("Eremite.Model.Good");
                if (goodType != null)
                {
                    _goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
                    _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
                }

                // Popup.Hide method
                var popupType = assembly.GetType("Eremite.View.Popups.Popup");
                if (popupType != null)
                {
                    _popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
                }

                _cached = true;
                Debug.Log("[ATSAccessibility] NewcomersReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NewcomersReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // TYPE DETECTION
        // ========================================

        /// <summary>
        /// Check if a popup object is a NewcomersPopup.
        /// </summary>
        public static bool IsNewcomersPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_newcomersPopupType == null) return false;
            return _newcomersPopupType.IsInstanceOfType(popup);
        }

        // ========================================
        // GROUP ACCESS
        // ========================================

        /// <summary>
        /// Get the current newcomers groups from the service.
        /// Returns null if service is unavailable or no newcomers waiting.
        /// </summary>
        public static IList GetNewcomersGroups()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return null;

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null) return null;

                if (_nsGetCurrentNewcomersMethod == null) return null;

                var result = _nsGetCurrentNewcomersMethod.Invoke(newcomersService, null);
                return result as IList;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NewcomersReflection: GetNewcomersGroups failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // PICKING
        // ========================================

        /// <summary>
        /// Pick a newcomers group and hide the popup.
        /// </summary>
        public static bool PickGroup(object popup, object group)
        {
            if (group == null) return false;
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null) return false;

                if (_nsPickGroupMethod == null) return false;

                _nsPickGroupMethod.Invoke(newcomersService, new[] { group });

                // Hide the popup (mirrors NewcomersPopup.OnGroupPicked behavior)
                if (popup != null && _popupHideMethod != null)
                {
                    _popupHideMethod.Invoke(popup, null);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NewcomersReflection: PickGroup failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // GROUP FORMATTING
        // ========================================

        /// <summary>
        /// Format a newcomers group as an announcement string.
        /// Format: "3 Humans, 2 Beavers. Bonus: 5 Planks, 3 Mushrooms"
        /// </summary>
        public static string FormatGroup(object group)
        {
            if (group == null) return "Unknown group";
            EnsureCached();

            try
            {
                var parts = new List<string>();

                // Read races dictionary
                var racesDict = _ngRacesField?.GetValue(group);
                if (racesDict != null)
                {
                    // Use reflection iteration pattern for Dictionary<string, int>
                    var keysProperty = racesDict.GetType().GetProperty("Keys");
                    var keys = keysProperty?.GetValue(racesDict) as IEnumerable;
                    var indexer = racesDict.GetType().GetMethod("get_Item");

                    if (keys != null && indexer != null)
                    {
                        foreach (var key in keys)
                        {
                            var raceName = key as string;
                            if (string.IsNullOrEmpty(raceName)) continue;

                            var countObj = indexer.Invoke(racesDict, new[] { key });
                            int count = countObj is int c ? c : 0;

                            var displayName = EmbarkReflection.GetRaceDisplayName(raceName);
                            parts.Add($"{count} {displayName}");
                        }
                    }
                }

                string raceText = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "No villagers";

                // Read goods list
                var goodsList = _ngGoodsField?.GetValue(group) as IList;
                if (goodsList != null && goodsList.Count > 0)
                {
                    var goodParts = new List<string>();

                    foreach (var good in goodsList)
                    {
                        if (good == null) continue;

                        var name = _goodNameField?.GetValue(good) as string ?? "";
                        var amount = _goodAmountField != null ? (int)_goodAmountField.GetValue(good) : 0;

                        if (amount <= 0 || string.IsNullOrEmpty(name)) continue;

                        var displayName = GameReflection.GetGoodDisplayName(name);
                        goodParts.Add($"{amount} {displayName}");
                    }

                    if (goodParts.Count > 0)
                    {
                        return $"{raceText}. Bonus: {string.Join(", ", goodParts.ToArray())}";
                    }
                }

                return raceText;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NewcomersReflection: FormatGroup failed: {ex.Message}");
                return "Unknown group";
            }
        }

    }
}
