using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to reward services for the F3 Rewards panel.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// - All public methods return fresh values by querying through cached PropertyInfo
    /// </summary>
    public static class RewardsReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // IGameServices service properties
        private static PropertyInfo _gsReputationRewardsServiceProperty = null;
        private static PropertyInfo _gsCornerstonesServiceProperty = null;
        private static PropertyInfo _gsNewcomersServiceProperty = null;

        // IReputationRewardsService properties/methods
        private static PropertyInfo _rrsRewardsToCollectProperty = null;
        private static MethodInfo _rrsRequestPopupMethod = null;

        // ICornerstonesService methods
        private static MethodInfo _csGetCurrentPickMethod = null;

        // INewcomersService methods
        private static MethodInfo _nsAreNewcomersWaitningMethod = null;  // Note: typo in game
        private static MethodInfo _nsGetCurrentNewcomersMethod = null;

        // ReactiveProperty<int>.Value property
        private static PropertyInfo _reactivePropertyValueProperty = null;

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

                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType == null) return;

                // Cache service property accessors
                _gsReputationRewardsServiceProperty = gameServicesType.GetProperty("ReputationRewardsService");
                _gsCornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService");
                _gsNewcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");

                // Cache ReputationRewardsService members
                var rrsType = assembly.GetType("Eremite.Services.IReputationRewardsService");
                if (rrsType != null)
                {
                    _rrsRewardsToCollectProperty = rrsType.GetProperty("RewardsToCollect");
                    _rrsRequestPopupMethod = rrsType.GetMethod("RequestPopup");
                }

                // Cache CornerstonesService members
                var csType = assembly.GetType("Eremite.Services.ICornerstonesService");
                if (csType != null)
                {
                    _csGetCurrentPickMethod = csType.GetMethod("GetCurrentPick");
                }

                // Cache NewcomersService members
                var nsType = assembly.GetType("Eremite.Services.INewcomersService");
                if (nsType != null)
                {
                    _nsAreNewcomersWaitningMethod = nsType.GetMethod("AreNewcomersWaitning");  // Note: typo in game
                    _nsGetCurrentNewcomersMethod = nsType.GetMethod("GetCurrentNewcomers");
                }

                Debug.Log("[ATSAccessibility] RewardsReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RewardsReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // REWARD DETECTION
        // ========================================

        /// <summary>
        /// Check if there are pending blueprints to pick.
        /// Uses ReputationRewardsService.RewardsToCollect.Value > 0.
        /// </summary>
        public static bool HasPendingBlueprints()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var rewardsService = _gsReputationRewardsServiceProperty?.GetValue(gameServices);
                if (rewardsService == null) return false;

                var rewardsToCollect = _rrsRewardsToCollectProperty?.GetValue(rewardsService);
                if (rewardsToCollect == null) return false;

                // Get the Value property from ReactiveProperty<int>
                if (_reactivePropertyValueProperty == null)
                {
                    _reactivePropertyValueProperty = rewardsToCollect.GetType().GetProperty("Value");
                }

                var value = _reactivePropertyValueProperty?.GetValue(rewardsToCollect);
                if (value is int count)
                {
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingBlueprints failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if there are pending cornerstones to pick.
        /// Uses CornerstonesService.GetCurrentPick() != null.
        /// </summary>
        public static bool HasPendingCornerstones()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var cornerstonesService = _gsCornerstonesServiceProperty?.GetValue(gameServices);
                if (cornerstonesService == null) return false;

                var currentPick = _csGetCurrentPickMethod?.Invoke(cornerstonesService, null);
                return currentPick != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingCornerstones failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if there are newcomers waiting.
        /// Uses NewcomersService.AreNewcomersWaitning() (note: typo in game).
        /// </summary>
        public static bool HasPendingNewcomers()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null) return false;

                var result = _nsAreNewcomersWaitningMethod?.Invoke(newcomersService, null);
                if (result is bool waiting)
                {
                    return waiting;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingNewcomers failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // POPUP TRIGGERS
        // ========================================

        /// <summary>
        /// Open the blueprints popup.
        /// Uses ReputationRewardsService.RequestPopup().
        /// </summary>
        public static bool OpenBlueprintsPopup()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: GameServices not available");
                    return false;
                }

                var rewardsService = _gsReputationRewardsServiceProperty?.GetValue(gameServices);
                if (rewardsService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: ReputationRewardsService not available");
                    return false;
                }

                if (_rrsRequestPopupMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: RequestPopup method not found");
                    return false;
                }

                _rrsRequestPopupMethod.Invoke(rewardsService, null);
                Debug.Log("[ATSAccessibility] Opened blueprints popup");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenBlueprintsPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the cornerstones popup.
        /// Fires GameBlackboardService.OnRewardsPopupRequested.
        /// </summary>
        public static bool OpenCornerstonesPopup()
        {
            try
            {
                var blackboardService = GameReflection.GetGameBlackboardService();
                if (blackboardService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: GameBlackboardService not available");
                    return false;
                }

                // Get Unit.Default for Subject<Unit>
                var unitDefault = GameReflection.GetUnitDefault();
                if (unitDefault == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: Could not get Unit.Default");
                    return false;
                }

                // Use the shared helper to fire OnRewardsPopupRequested.OnNext(Unit.Default)
                bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnRewardsPopupRequested", unitDefault);
                if (result)
                {
                    Debug.Log("[ATSAccessibility] Opened cornerstones popup");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenCornerstonesPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the newcomers popup.
        /// Fires GameBlackboardService.OnNewcomersPopupRequested with current newcomers.
        /// </summary>
        public static bool OpenNewcomersPopup()
        {
            EnsureCached();

            try
            {
                // First get the current newcomers
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameServices not available");
                    return false;
                }

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: NewcomersService not available");
                    return false;
                }

                var currentNewcomers = _nsGetCurrentNewcomersMethod?.Invoke(newcomersService, null);
                if (currentNewcomers == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: No current newcomers");
                    return false;
                }

                // Now fire the event using the shared helper
                var blackboardService = GameReflection.GetGameBlackboardService();
                if (blackboardService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameBlackboardService not available");
                    return false;
                }

                bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnNewcomersPopupRequested", currentNewcomers);
                if (result)
                {
                    Debug.Log("[ATSAccessibility] Opened newcomers popup");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenNewcomersPopup failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // UNAVAILABILITY INFO
        // ========================================

        private static bool _unavailTypesCached = false;

        // IReputationService.Reputation (ReactiveProperty<float>)
        private static PropertyInfo _repReputationProperty = null;

        // HasRewardFor(int) on concrete ReputationRewardsService
        private static MethodInfo _rrsHasRewardForMethod = null;

        // INewcomersService.GetTimeToNextVisit()
        private static MethodInfo _nsGetTimeToNextVisitMethod = null;

        // BiomeService access for cornerstone dates
        private static PropertyInfo _gsBiomeServiceProperty = null;
        private static PropertyInfo _bsCurrentBiomeProperty = null;
        private static FieldInfo _bmSeasonsField = null;
        private static FieldInfo _scSeasonRewardsField = null;

        // CalendarService Quarter property
        private static PropertyInfo _calQuarterProperty = null;

        // SeasonRewardModel fields
        private static FieldInfo _srmYearField = null;
        private static FieldInfo _srmSeasonField = null;
        private static FieldInfo _srmQuarterField = null;

        private static void EnsureUnavailTypeCached()
        {
            if (_unavailTypesCached) return;
            _unavailTypesCached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // IReputationService.Reputation property
                var repServiceType = assembly.GetType("Eremite.Services.IReputationService");
                if (repServiceType != null)
                {
                    _repReputationProperty = repServiceType.GetProperty("Reputation",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // HasRewardFor on concrete ReputationRewardsService (public method)
                var rrsConcreteType = assembly.GetType("Eremite.Services.ReputationRewardsService");
                if (rrsConcreteType != null)
                {
                    _rrsHasRewardForMethod = rrsConcreteType.GetMethod("HasRewardFor",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // INewcomersService.GetTimeToNextVisit
                var nsType = assembly.GetType("Eremite.Services.INewcomersService");
                if (nsType != null)
                {
                    _nsGetTimeToNextVisitMethod = nsType.GetMethod("GetTimeToNextVisit",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // BiomeService from IGameServices
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // IBiomeService.CurrentBiome
                var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
                if (biomeServiceType != null)
                {
                    _bsCurrentBiomeProperty = biomeServiceType.GetProperty("CurrentBiome",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // BiomeModel.seasons field
                var biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
                if (biomeModelType != null)
                {
                    _bmSeasonsField = biomeModelType.GetField("seasons",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // SeasonsConfig.SeasonRewards field
                var seasonsConfigType = assembly.GetType("Eremite.Model.Configs.SeasonsConfig");
                if (seasonsConfigType != null)
                {
                    _scSeasonRewardsField = seasonsConfigType.GetField("SeasonRewards",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // CalendarService Quarter property
                var calServiceType = assembly.GetType("Eremite.Services.ICalendarService");
                if (calServiceType != null)
                {
                    _calQuarterProperty = calServiceType.GetProperty("Quarter",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // SeasonRewardModel fields
                var srmType = assembly.GetType("Eremite.Model.SeasonRewardModel");
                if (srmType != null)
                {
                    _srmYearField = srmType.GetField("year", BindingFlags.Public | BindingFlags.Instance);
                    _srmSeasonField = srmType.GetField("season", BindingFlags.Public | BindingFlags.Instance);
                    _srmQuarterField = srmType.GetField("quarter", BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] RewardsReflection unavailability types cached");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Unavailability type caching failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the next reputation threshold that grants a blueprint reward.
        /// Returns (nextThreshold, currentRep) or null if not determinable.
        /// </summary>
        public static (int nextThreshold, int currentRep)? GetNextBlueprintThreshold()
        {
            EnsureCached();
            EnsureUnavailTypeCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return null;

                // Get current reputation value
                var repService = GameReflection.GetReputationService();
                if (repService == null) return null;

                var repReactive = _repReputationProperty?.GetValue(repService);
                if (repReactive == null) return null;

                // Get Value from ReactiveProperty<float>
                var valueProp = repReactive.GetType().GetProperty("Value");
                var repValue = valueProp?.GetValue(repReactive);
                if (!(repValue is float repFloat)) return null;

                int currentRep = (int)repFloat;

                // Get ReputationRewardsService instance
                var rewardsService = _gsReputationRewardsServiceProperty?.GetValue(gameServices);
                if (rewardsService == null || _rrsHasRewardForMethod == null) return null;

                // Search for next threshold (reputation rewards are typically 1-20)
                var args = new object[1];
                for (int i = currentRep + 1; i <= 30; i++)
                {
                    args[0] = i;
                    var result = _rrsHasRewardForMethod.Invoke(rewardsService, args);
                    if (result is bool hasReward && hasReward)
                    {
                        return (i, currentRep);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetNextBlueprintThreshold failed: {ex.Message}");
            }

            return null;
        }

        private static readonly string[] SeasonNames = { "Drizzle", "Clearance", "Storm" };

        /// <summary>
        /// Get the next cornerstone reward date.
        /// Returns (season name, year) or null if not determinable.
        /// </summary>
        public static (string season, int year)? GetNextCornerstoneDate()
        {
            EnsureUnavailTypeCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return null;

                // Get current game date from CalendarService
                var calService = GameReflection.GetCalendarService();
                if (calService == null) return null;

                int curYear = GameReflection.GetYear();
                int curSeason = GameReflection.GetSeason();
                int curQuarter = -1;

                if (_calQuarterProperty != null)
                {
                    var quarterVal = _calQuarterProperty.GetValue(calService);
                    if (quarterVal != null)
                        curQuarter = (int)quarterVal;
                }

                if (curYear <= 0 || curSeason < 0 || curQuarter < 0) return null;

                // Get biome's SeasonRewards list
                var biomeService = _gsBiomeServiceProperty?.GetValue(gameServices);
                if (biomeService == null) return null;

                var currentBiome = _bsCurrentBiomeProperty?.GetValue(biomeService);
                if (currentBiome == null) return null;

                var seasonsConfig = _bmSeasonsField?.GetValue(currentBiome);
                if (seasonsConfig == null) return null;

                var seasonRewardsList = _scSeasonRewardsField?.GetValue(seasonsConfig);
                if (seasonRewardsList == null) return null;

                // Iterate the list to find next reward date after current date
                var enumerable = seasonRewardsList as IEnumerable;
                if (enumerable == null) return null;

                int bestYear = int.MaxValue;
                int bestSeason = int.MaxValue;
                int bestQuarter = int.MaxValue;
                bool found = false;

                foreach (var srm in enumerable)
                {
                    if (srm == null) continue;

                    var yVal = _srmYearField?.GetValue(srm);
                    var sVal = _srmSeasonField?.GetValue(srm);
                    var qVal = _srmQuarterField?.GetValue(srm);

                    if (yVal == null || sVal == null || qVal == null) continue;

                    int y = (int)yVal;
                    int s = (int)sVal;
                    int q = (int)qVal;

                    // Check if this date is in the future
                    if (!IsDateAfter(y, s, q, curYear, curSeason, curQuarter)) continue;

                    // Check if this is earlier than current best
                    if (!found || IsDateAfter(bestYear, bestSeason, bestQuarter, y, s, q))
                    {
                        bestYear = y;
                        bestSeason = s;
                        bestQuarter = q;
                        found = true;
                    }
                }

                if (found && bestSeason >= 0 && bestSeason < SeasonNames.Length)
                {
                    return (SeasonNames[bestSeason], bestYear);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetNextCornerstoneDate failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Returns true if date (y1,s1,q1) is strictly after (y2,s2,q2).
        /// </summary>
        private static bool IsDateAfter(int y1, int s1, int q1, int y2, int s2, int q2)
        {
            if (y1 != y2) return y1 > y2;
            if (s1 != s2) return s1 > s2;
            return q1 > q2;
        }

        /// <summary>
        /// Get time in seconds until next newcomers visit, or -1 if unavailable.
        /// </summary>
        public static float GetTimeToNextNewcomers()
        {
            EnsureCached();
            EnsureUnavailTypeCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return -1f;

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null) return -1f;

                if (_nsGetTimeToNextVisitMethod == null) return -1f;

                var result = _nsGetTimeToNextVisitMethod.Invoke(newcomersService, null);
                if (result is float time)
                {
                    return time;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTimeToNextNewcomers failed: {ex.Message}");
            }

            return -1f;
        }

        /// <summary>
        /// Format game time in seconds to a readable string.
        /// Rounds to nearest 5 seconds for cleaner output.
        /// </summary>
        public static string FormatGameTime(float seconds)
        {
            if (seconds <= 0) return "soon";

            // Round to nearest 5 seconds
            int totalSeconds = (int)(Mathf.Round(seconds / 5f) * 5f);
            if (totalSeconds <= 0) totalSeconds = 5;

            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;

            if (minutes > 0 && secs > 0)
                return $"{minutes} minute{(minutes != 1 ? "s" : "")} {secs} seconds";
            if (minutes > 0)
                return $"{minutes} minute{(minutes != 1 ? "s" : "")}";
            return $"{secs} seconds";
        }
    }
}
