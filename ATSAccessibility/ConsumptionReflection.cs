using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to consumption control internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class ConsumptionReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // Popup type detection
        private static Type _consumptionPopupType = null;

        // IGameServices properties
        private static PropertyInfo _gsNeedsServiceProperty = null;
        private static PropertyInfo _gsEffectsServiceProperty = null;
        private static PropertyInfo _gsRacesServiceProperty = null;
        private static PropertyInfo _gsStateServiceProperty = null;

        // INeedsService methods
        private static MethodInfo _nsIsPermitedRawFoodMethod = null;      // IsPermited(string rawFood)
        private static MethodInfo _nsSetPermisionRawFoodMethod = null;    // SetPermision(string rawFood, bool)
        private static MethodInfo _nsIsPermitedRaceNeedMethod = null;     // IsPermited(RaceModel, NeedModel)
        private static MethodInfo _nsSetPermisionRaceNeedMethod = null;   // SetPermision(RaceModel, NeedModel, bool)
        private static MethodInfo _nsIsAllRawFoodPermitedMethod = null;
        private static MethodInfo _nsIsAllRawFoodProhibitedMethod = null;
        private static MethodInfo _nsGetCurrentResolveImpactMethod = null;
        private static MethodInfo _nsGetMaxResolveImpactMethod = null;

        // IEffectsService methods
        private static MethodInfo _esIsConsumptionControlBlockedMethod = null;
        private static MethodInfo _esGetEffectsDisplayListMethod = null;

        // StateService.Effects access for blocking effects list
        private static PropertyInfo _ssEffectsProperty = null;
        private static FieldInfo _effectsConsumptionControlLocksField = null;

        // IRacesService properties/methods
        private static PropertyInfo _rsRacesProperty = null;
        private static MethodInfo _rsIsRevealedMethod = null;

        // StateService.Actors access
        private static PropertyInfo _ssActorsProperty = null;
        private static FieldInfo _actorsRawFoodPermitsField = null;

        // NeedModel fields/properties
        private static FieldInfo _nmCanBeProhibitedField = null;
        private static FieldInfo _nmCategoryField = null;
        private static PropertyInfo _nmDisplayNameProperty = null;

        // NeedCategoryModel fields
        private static FieldInfo _ncmIsHouseBasedField = null;
        private static FieldInfo _ncmDisplayNameField = null;

        // RaceModel fields/methods
        private static FieldInfo _rmDisplayNameField = null;
        private static FieldInfo _rmNeedsField = null;
        private static MethodInfo _rmHasNeedMethod = null;

        // Settings access
        private static FieldInfo _settingsNeedsField = null;

        // Cached types for method resolution
        private static Type _raceModelType = null;
        private static Type _needModelType = null;

        // Pre-allocated args arrays
        private static readonly object[] _args1 = new object[1];
        private static readonly object[] _args2 = new object[2];
        private static readonly object[] _args3 = new object[3];

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
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] ConsumptionReflection: Game assembly not available");
                    return;
                }

                CachePopupTypes(assembly);
                CacheServiceTypes(assembly);
                CacheNeedModelTypes(assembly);
                CacheNeedCategoryTypes(assembly);
                CacheRaceModelTypes(assembly);
                CacheSettingsTypes(assembly);
                CacheStateTypes(assembly);

                Debug.Log("[ATSAccessibility] ConsumptionReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ConsumptionReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            _consumptionPopupType = assembly.GetType("Eremite.View.Popups.Consumption.ConsumptionPopup");
            if (_consumptionPopupType == null)
                Debug.LogWarning("[ATSAccessibility] ConsumptionReflection: ConsumptionPopup type not found");
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsNeedsServiceProperty = gameServicesType.GetProperty("NeedsService", GameReflection.PublicInstance);
                _gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService", GameReflection.PublicInstance);
                _gsRacesServiceProperty = gameServicesType.GetProperty("RacesService", GameReflection.PublicInstance);
                _gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
            }

            // INeedsService methods
            var needsServiceType = assembly.GetType("Eremite.Services.INeedsService");
            if (needsServiceType != null)
            {
                _raceModelType = assembly.GetType("Eremite.Model.RaceModel");
                _needModelType = assembly.GetType("Eremite.Model.NeedModel");

                // IsPermited(string rawFood)
                _nsIsPermitedRawFoodMethod = needsServiceType.GetMethod("IsPermited",
                    new Type[] { typeof(string) });

                // SetPermision(string rawFood, bool isOn)
                _nsSetPermisionRawFoodMethod = needsServiceType.GetMethod("SetPermision",
                    new Type[] { typeof(string), typeof(bool) });

                // IsPermited(RaceModel, NeedModel)
                if (_raceModelType != null && _needModelType != null)
                {
                    _nsIsPermitedRaceNeedMethod = needsServiceType.GetMethod("IsPermited",
                        new Type[] { _raceModelType, _needModelType });

                    // SetPermision(RaceModel, NeedModel, bool)
                    _nsSetPermisionRaceNeedMethod = needsServiceType.GetMethod("SetPermision",
                        new Type[] { _raceModelType, _needModelType, typeof(bool) });

                    // GetCurrentResolveImpact(RaceModel, NeedModel)
                    _nsGetCurrentResolveImpactMethod = needsServiceType.GetMethod("GetCurrentResolveImpact",
                        new Type[] { _raceModelType, _needModelType });

                    // GetMaxResolveImpact(RaceModel, NeedModel)
                    _nsGetMaxResolveImpactMethod = needsServiceType.GetMethod("GetMaxResolveImpact",
                        new Type[] { _raceModelType, _needModelType });
                }

                _nsIsAllRawFoodPermitedMethod = needsServiceType.GetMethod("IsAllRawFoodPermited", Type.EmptyTypes);
                _nsIsAllRawFoodProhibitedMethod = needsServiceType.GetMethod("IsAllRawFoodProhibited", Type.EmptyTypes);
            }

            // IEffectsService
            var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
            if (effectsServiceType != null)
            {
                _esIsConsumptionControlBlockedMethod = effectsServiceType.GetMethod("IsConsumptionControlBlocked", Type.EmptyTypes);
                _esGetEffectsDisplayListMethod = effectsServiceType.GetMethod("GetEffectsDisplayList",
                    new Type[] { typeof(List<string>) });
            }

            // IRacesService
            var racesServiceType = assembly.GetType("Eremite.Services.IRacesService");
            if (racesServiceType != null)
            {
                _rsRacesProperty = racesServiceType.GetProperty("Races", GameReflection.PublicInstance);

                if (_raceModelType != null)
                {
                    _rsIsRevealedMethod = racesServiceType.GetMethod("IsRevealed",
                        new Type[] { _raceModelType });
                }
            }
        }

        private static void CacheNeedModelTypes(Assembly assembly)
        {
            var needModelType = assembly.GetType("Eremite.Model.NeedModel");
            if (needModelType == null) return;

            _nmCanBeProhibitedField = needModelType.GetField("canBeProhibited", GameReflection.PublicInstance);
            _nmCategoryField = needModelType.GetField("category", GameReflection.PublicInstance);
            _nmDisplayNameProperty = needModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
        }

        private static void CacheNeedCategoryTypes(Assembly assembly)
        {
            var categoryType = assembly.GetType("Eremite.Model.NeedCategoryModel");
            if (categoryType == null) return;

            _ncmIsHouseBasedField = categoryType.GetField("isHouseBased", GameReflection.PublicInstance);
            _ncmDisplayNameField = categoryType.GetField("displayName", GameReflection.PublicInstance);
        }

        private static void CacheRaceModelTypes(Assembly assembly)
        {
            var raceModelType = assembly.GetType("Eremite.Model.RaceModel");
            if (raceModelType == null) return;

            _rmDisplayNameField = raceModelType.GetField("displayName", GameReflection.PublicInstance);
            _rmNeedsField = raceModelType.GetField("needs", GameReflection.PublicInstance);

            var needModelType = assembly.GetType("Eremite.Model.NeedModel");
            if (needModelType != null)
            {
                _rmHasNeedMethod = raceModelType.GetMethod("HasNeed",
                    new Type[] { needModelType });
            }
        }

        private static void CacheSettingsTypes(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType == null) return;

            _settingsNeedsField = settingsType.GetField("Needs", GameReflection.PublicInstance);
        }

        private static void CacheStateTypes(Assembly assembly)
        {
            var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
            if (stateServiceType != null)
            {
                _ssActorsProperty = stateServiceType.GetProperty("Actors", GameReflection.PublicInstance);
                _ssEffectsProperty = stateServiceType.GetProperty("Effects", GameReflection.PublicInstance);
            }

            var actorsStateType = assembly.GetType("Eremite.Model.State.ActorsState");
            if (actorsStateType != null)
            {
                _actorsRawFoodPermitsField = actorsStateType.GetField("rawFoodConsumptionPermits", GameReflection.PublicInstance);
            }

            var effectsStateType = assembly.GetType("Eremite.Model.State.EffectsState");
            if (effectsStateType != null)
            {
                _effectsConsumptionControlLocksField = effectsStateType.GetField("consumptionControlLocks", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // SERVICE ACCESSORS (fresh each call)
        // ========================================

        private static object GetNeedsService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsNeedsServiceProperty == null) return null;
            try { return _gsNeedsServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetEffectsService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsEffectsServiceProperty == null) return null;
            try { return _gsEffectsServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetRacesService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsRacesServiceProperty == null) return null;
            try { return _gsRacesServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetStateService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsStateServiceProperty == null) return null;
            try { return _gsStateServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Check if the given popup is a ConsumptionPopup.
        /// </summary>
        public static bool IsConsumptionPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_consumptionPopupType == null) return false;
            return _consumptionPopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Check if consumption control is blocked by effects.
        /// </summary>
        public static bool IsBlocked()
        {
            EnsureCached();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esIsConsumptionControlBlockedMethod == null) return false;
            try { return (bool)_esIsConsumptionControlBlockedMethod.Invoke(effectsService, null); }
            catch { return false; }
        }

        /// <summary>
        /// Get all need categories that are not house-based (for consumption popup).
        /// Returns list of NeedCategoryModel objects.
        /// </summary>
        public static List<object> GetCategories()
        {
            EnsureCached();
            var result = new List<object>();
            var needs = GetAllNeeds();
            if (needs == null) return result;

            var seen = new HashSet<object>();
            foreach (var need in needs)
            {
                if (need == null) continue;
                var category = _nmCategoryField?.GetValue(need);
                if (category == null) continue;
                if (seen.Contains(category)) continue;

                // Skip house-based categories
                var isHouseBased = _ncmIsHouseBasedField?.GetValue(category);
                if (isHouseBased is bool hb && hb) continue;

                seen.Add(category);
                result.Add(category);
            }

            return result;
        }

        /// <summary>
        /// Get display name of a NeedCategoryModel.
        /// </summary>
        public static string GetCategoryName(object category)
        {
            EnsureCached();
            if (category == null) return "Unknown";
            var locaText = _ncmDisplayNameField?.GetValue(category);
            return GameReflection.GetLocaText(locaText) ?? "Unknown";
        }

        /// <summary>
        /// Get raw food items with their permission status.
        /// Returns list of (id, name) tuples. Use IsRawFoodPermitted to check status.
        /// </summary>
        public static List<string> GetRawFoods()
        {
            EnsureCached();
            var result = new List<string>();

            var stateService = GetStateService();
            if (stateService == null || _ssActorsProperty == null) return result;

            try
            {
                var actors = _ssActorsProperty.GetValue(stateService);
                if (actors == null || _actorsRawFoodPermitsField == null) return result;

                var dict = _actorsRawFoodPermitsField.GetValue(actors);
                if (dict == null) return result;

                // Use reflection iteration pattern for the dictionary
                var keysProperty = dict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(dict) as IEnumerable;
                if (keys == null) return result;

                foreach (var key in keys)
                {
                    if (key is string id)
                        result.Add(id);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ConsumptionReflection.GetRawFoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get display name for a raw food ID via Settings.GetGood(id).displayName.
        /// </summary>
        public static string GetRawFoodName(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;

            var settings = GameReflection.GetSettings();
            if (settings == null) return id;

            try
            {
                // Try Settings.GetGood(id)
                var getGoodMethod = settings.GetType().GetMethod("GetGood", new Type[] { typeof(string) });
                if (getGoodMethod == null) return id;

                _args1[0] = id;
                var good = getGoodMethod.Invoke(settings, _args1);
                if (good == null) return id;

                var displayNameField = good.GetType().GetField("displayName", GameReflection.PublicInstance);
                if (displayNameField == null) return id;

                var locaText = displayNameField.GetValue(good);
                return GameReflection.GetLocaText(locaText) ?? id;
            }
            catch
            {
                return id;
            }
        }

        /// <summary>
        /// Check if a raw food is permitted.
        /// </summary>
        public static bool IsRawFoodPermitted(string id)
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsIsPermitedRawFoodMethod == null) return true;

            try
            {
                _args1[0] = id;
                return (bool)_nsIsPermitedRawFoodMethod.Invoke(needsService, _args1);
            }
            catch { return true; }
        }

        /// <summary>
        /// Set permission for a raw food.
        /// </summary>
        public static bool SetRawFoodPermission(string id, bool isOn)
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsSetPermisionRawFoodMethod == null) return false;

            try
            {
                _args2[0] = id;
                _args2[1] = isOn;
                _nsSetPermisionRawFoodMethod.Invoke(needsService, _args2);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Set all raw food permissions to the given value.
        /// </summary>
        public static void SetAllRawFoodPermission(bool isOn)
        {
            var foods = GetRawFoods();
            foreach (var id in foods)
            {
                SetRawFoodPermission(id, isOn);
            }
        }

        /// <summary>
        /// Check if all raw food is permitted.
        /// </summary>
        public static bool IsAllRawFoodPermitted()
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsIsAllRawFoodPermitedMethod == null) return true;
            try { return (bool)_nsIsAllRawFoodPermitedMethod.Invoke(needsService, null); }
            catch { return true; }
        }

        /// <summary>
        /// Check if all raw food is prohibited.
        /// </summary>
        public static bool IsAllRawFoodProhibited()
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsIsAllRawFoodProhibitedMethod == null) return false;
            try { return (bool)_nsIsAllRawFoodProhibitedMethod.Invoke(needsService, null); }
            catch { return false; }
        }

        /// <summary>
        /// Get needs for a specific category that can be prohibited.
        /// Returns list of NeedModel objects.
        /// </summary>
        public static List<object> GetNeedsForCategory(object category)
        {
            EnsureCached();
            var result = new List<object>();
            var needs = GetAllNeeds();
            if (needs == null || category == null) return result;

            foreach (var need in needs)
            {
                if (need == null) continue;

                // Check category matches
                var needCategory = _nmCategoryField?.GetValue(need);
                if (needCategory == null || !ReferenceEquals(needCategory, category)) continue;

                // Check canBeProhibited
                var canBeProhibited = _nmCanBeProhibitedField?.GetValue(need);
                if (canBeProhibited is bool cbp && cbp)
                    result.Add(need);
            }

            return result;
        }

        /// <summary>
        /// Get display name of a NeedModel.
        /// </summary>
        public static string GetNeedName(object need)
        {
            EnsureCached();
            if (need == null) return "Unknown";
            try
            {
                return _nmDisplayNameProperty?.GetValue(need) as string ?? "Unknown";
            }
            catch { return "Unknown"; }
        }

        /// <summary>
        /// Check if a need is permitted for ALL revealed races.
        /// Returns true only if all are permitted.
        /// </summary>
        public static bool IsNeedBlanketPermitted(object need)
        {
            var races = GetAllRevealedRaces();
            if (races.Count == 0) return true;

            foreach (var race in races)
            {
                if (!IsNeedPermittedForRace(race, need))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if a need is prohibited for ALL revealed races.
        /// Returns true only if all are prohibited.
        /// </summary>
        public static bool IsNeedBlanketProhibited(object need)
        {
            var races = GetAllRevealedRaces();
            if (races.Count == 0) return false;

            foreach (var race in races)
            {
                if (IsNeedPermittedForRace(race, need))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Set permission for a need across all revealed races.
        /// Matches game behavior: permissions are stored regardless of whether
        /// the race currently has the need, acting as pre-configuration.
        /// </summary>
        public static void SetNeedBlanketPermission(object need, bool isOn)
        {
            var races = GetAllRevealedRaces();
            foreach (var race in races)
            {
                SetNeedPermissionForRace(race, need, isOn);
            }
        }

        /// <summary>
        /// Get all revealed races regardless of whether they have a specific need.
        /// Used by blanket permission methods to match game behavior.
        /// </summary>
        public static List<object> GetAllRevealedRaces()
        {
            EnsureCached();
            var result = new List<object>();

            var racesService = GetRacesService();
            if (racesService == null || _rsRacesProperty == null) return result;

            try
            {
                var races = _rsRacesProperty.GetValue(racesService) as Array;
                if (races == null) return result;

                foreach (var race in races)
                {
                    if (race == null) continue;

                    if (_rsIsRevealedMethod != null)
                    {
                        _args1[0] = race;
                        var revealed = _rsIsRevealedMethod.Invoke(racesService, _args1);
                        if (revealed is bool r && !r) continue;
                    }

                    result.Add(race);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ConsumptionReflection.GetAllRevealedRaces failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get revealed races that have a specific need.
        /// Returns list of RaceModel objects.
        /// </summary>
        public static List<object> GetRacesForNeed(object need)
        {
            EnsureCached();
            var result = new List<object>();
            if (need == null) return result;

            var racesService = GetRacesService();
            if (racesService == null || _rsRacesProperty == null) return result;

            try
            {
                var races = _rsRacesProperty.GetValue(racesService) as Array;
                if (races == null) return result;

                foreach (var race in races)
                {
                    if (race == null) continue;

                    // Check if revealed
                    if (_rsIsRevealedMethod != null)
                    {
                        _args1[0] = race;
                        var revealed = _rsIsRevealedMethod.Invoke(racesService, _args1);
                        if (revealed is bool r && !r) continue;
                    }

                    // Check if race has this need
                    if (_rmHasNeedMethod != null)
                    {
                        _args1[0] = need;
                        var hasNeed = _rmHasNeedMethod.Invoke(race, _args1);
                        if (hasNeed is bool hn && !hn) continue;
                    }

                    result.Add(race);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ConsumptionReflection.GetRacesForNeed failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get display name of a RaceModel.
        /// </summary>
        public static string GetRaceName(object race)
        {
            EnsureCached();
            if (race == null) return "Unknown";
            var locaText = _rmDisplayNameField?.GetValue(race);
            return GameReflection.GetLocaText(locaText) ?? "Unknown";
        }

        /// <summary>
        /// Check if a need is permitted for a specific race.
        /// </summary>
        public static bool IsNeedPermittedForRace(object race, object need)
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsIsPermitedRaceNeedMethod == null) return true;

            try
            {
                _args2[0] = race;
                _args2[1] = need;
                return (bool)_nsIsPermitedRaceNeedMethod.Invoke(needsService, _args2);
            }
            catch { return true; }
        }

        /// <summary>
        /// Set permission for a specific race and need.
        /// </summary>
        public static bool SetNeedPermissionForRace(object race, object need, bool isOn)
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null || _nsSetPermisionRaceNeedMethod == null) return false;

            try
            {
                _args3[0] = race;
                _args3[1] = need;
                _args3[2] = isOn;
                _nsSetPermisionRaceNeedMethod.Invoke(needsService, _args3);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Get resolve impact for a race/need combination.
        /// Returns (current, max) tuple.
        /// </summary>
        public static (int current, int max) GetResolveImpact(object race, object need)
        {
            EnsureCached();
            var needsService = GetNeedsService();
            if (needsService == null) return (0, 0);

            int current = 0;
            int max = 0;

            try
            {
                if (_nsGetCurrentResolveImpactMethod != null)
                {
                    _args2[0] = race;
                    _args2[1] = need;
                    current = (int)_nsGetCurrentResolveImpactMethod.Invoke(needsService, _args2);
                }

                if (_nsGetMaxResolveImpactMethod != null)
                {
                    _args2[0] = race;
                    _args2[1] = need;
                    max = (int)_nsGetMaxResolveImpactMethod.Invoke(needsService, _args2);
                }
            }
            catch { }

            return (current, max);
        }

        /// <summary>
        /// Set all needs in a category to the given permission for all revealed races.
        /// </summary>
        public static void SetAllNeedsPermissionForCategory(object category, bool isOn)
        {
            var needs = GetNeedsForCategory(category);
            foreach (var need in needs)
            {
                SetNeedBlanketPermission(need, isOn);
            }
        }

        /// <summary>
        /// Get the summary status for a need category.
        /// Returns "all permitted", "all prohibited", or "mixed".
        /// </summary>
        public static string GetCategoryStatus(object category, bool isRawFood)
        {
            if (isRawFood)
            {
                if (IsAllRawFoodPermitted()) return "all permitted";
                if (IsAllRawFoodProhibited()) return "all prohibited";
                return "mixed";
            }

            var needs = GetNeedsForCategory(category);
            if (needs.Count == 0) return "all permitted";

            bool anyPermitted = false;
            bool anyProhibited = false;

            foreach (var need in needs)
            {
                if (IsNeedBlanketPermitted(need))
                    anyPermitted = true;
                else if (IsNeedBlanketProhibited(need))
                    anyProhibited = true;
                else
                {
                    // Mixed within this need
                    anyPermitted = true;
                    anyProhibited = true;
                }

                if (anyPermitted && anyProhibited) return "mixed";
            }

            if (anyPermitted && !anyProhibited) return "all permitted";
            if (anyProhibited && !anyPermitted) return "all prohibited";
            return "mixed";
        }

        /// <summary>
        /// Get the status string for a need item.
        /// For raw food: "permitted" or "prohibited".
        /// For needs: "all permitted", "all prohibited", or "mixed".
        /// </summary>
        public static string GetNeedStatus(object need)
        {
            if (IsNeedBlanketPermitted(need)) return "all permitted";
            if (IsNeedBlanketProhibited(need)) return "all prohibited";
            return "mixed";
        }

        /// <summary>
        /// Get the comma-separated list of effects blocking consumption control.
        /// Returns null if not blocked or if reflection fails.
        /// </summary>
        public static string GetBlockingEffectsList()
        {
            EnsureCached();
            var effectsService = GetEffectsService();
            var stateService = GetStateService();
            if (effectsService == null || stateService == null) return null;

            try
            {
                // Get StateService.Effects (EffectsState)
                if (_ssEffectsProperty == null) return null;
                var effectsState = _ssEffectsProperty.GetValue(stateService);
                if (effectsState == null) return null;

                // Get consumptionControlLocks (List<string>)
                if (_effectsConsumptionControlLocksField == null) return null;
                var locks = _effectsConsumptionControlLocksField.GetValue(effectsState) as List<string>;
                if (locks == null || locks.Count == 0) return null;

                // Call EffectsService.GetEffectsDisplayList(List<string>)
                if (_esGetEffectsDisplayListMethod == null) return null;
                _args1[0] = locks;
                return _esGetEffectsDisplayListMethod.Invoke(effectsService, _args1) as string;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ConsumptionReflection.GetBlockingEffectsList failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the permission status for all prohibitable needs for a specific race.
        /// Returns "all permitted", "all prohibited", or "mixed".
        /// </summary>
        public static string GetRaceNeedsStatus(object race)
        {
            EnsureCached();
            if (race == null) return "unknown";

            var allNeeds = GetAllNeeds();
            if (allNeeds == null) return "unknown";

            bool anyPermitted = false;
            bool anyProhibited = false;

            foreach (var need in allNeeds)
            {
                if (need == null) continue;
                var canBeProhibited = _nmCanBeProhibitedField?.GetValue(need);
                if (!(canBeProhibited is bool cbp && cbp)) continue;

                if (IsNeedPermittedForRace(race, need))
                    anyPermitted = true;
                else
                    anyProhibited = true;

                if (anyPermitted && anyProhibited) return "mixed";
            }

            if (anyPermitted && !anyProhibited) return "all permitted";
            if (anyProhibited && !anyPermitted) return "all prohibited";
            return "mixed";
        }

        /// <summary>
        /// Set permission for all prohibitable needs for a specific race.
        /// Matches game behavior: iterates Settings.Needs where canBeProhibited.
        /// </summary>
        public static void SetAllNeedsPermissionForRace(object race, bool isOn)
        {
            EnsureCached();
            if (race == null) return;

            var allNeeds = GetAllNeeds();
            if (allNeeds == null) return;

            foreach (var need in allNeeds)
            {
                if (need == null) continue;
                var canBeProhibited = _nmCanBeProhibitedField?.GetValue(need);
                if (!(canBeProhibited is bool cbp && cbp)) continue;

                SetNeedPermissionForRace(race, need, isOn);
            }
        }

        // ========================================
        // INTERNAL HELPERS
        // ========================================

        /// <summary>
        /// Get all NeedModel objects from Settings.Needs.
        /// </summary>
        private static Array GetAllNeeds()
        {
            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsNeedsField == null) return null;

            try
            {
                return _settingsNeedsField.GetValue(settings) as Array;
            }
            catch { return null; }
        }
    }
}
