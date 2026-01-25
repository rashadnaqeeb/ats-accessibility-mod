using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides access to game statistics (Reputation, Impatience, Hostility, Resolve)
    /// via reflection and announces them via speech.
    /// </summary>
    public static class StatsReader
    {
        // Cached reflection metadata for ReputationService
        private static PropertyInfo _repReputationProperty = null;       // ReactiveProperty<float>
        private static PropertyInfo _repPenaltyProperty = null;          // ReactiveProperty<float> (impatience)
        private static MethodInfo _repGetToWinMethod = null;             // GetReputationToWin()
        private static MethodInfo _repGetPenaltyToLooseMethod = null;    // GetReputationPenaltyToLoose()
        private static MethodInfo _repGetGainedFromMethod = null;        // GetReputationGainedFrom(source)
        private static MethodInfo _repGetPenaltyPerSecMethod = null;     // GetReputationPenaltyPerSec()
        private static MethodInfo _repGetBasePenaltyPerSecMethod = null; // GetBaseReputationPenaltyPerSec()
        private static PropertyInfo _repStateProperty = null;            // State (GameObjectivesState)
        private static FieldInfo _gracePeriodLeftField = null;           // gracePeriodLeft field
        private static bool _repTypesCached = false;

        // Cached reflection metadata for HostilityService
        private static PropertyInfo _hostPointsProperty = null;          // ReactiveProperty<int>
        private static PropertyInfo _hostLevelProperty = null;           // ReactiveProperty<int>
        private static MethodInfo _hostGetSourceAmountMethod = null;     // GetSourceAmount(source)
        private static MethodInfo _hostGetPointsForMethod = null;        // GetPointsFor(source)
        private static MethodInfo _hostGetPointsLeftToNextLevelMethod = null; // GetPointsLeftToNextLevel()
        private static bool _hostTypesCached = false;

        // Cached reflection metadata for ResolveService
        private static MethodInfo _resGetResolveForMethod = null;        // GetResolveFor(race)
        private static MethodInfo _resGetMinResolveForRepMethod = null;  // GetMinResolveForReputation(race)
        private static MethodInfo _resGetTargetResolveForMethod = null; // GetTargetResolveFor(race) - settling point
        private static PropertyInfo _resEffectsProperty = null;          // Effects dictionary
        private static bool _resTypesCached = false;

        // Cached reflection metadata for VillagersService.Races
        private static PropertyInfo _villRacesProperty = null;           // Races dictionary
        private static bool _villTypesCached = false;

        // Cached enum type for ReputationChangeSource
        private static Type _reputationChangeSourceType = null;
        private static Type _hostilitySourceType = null;
        private static bool _enumTypesCached = false;

        // Reusable object array for single-argument method invocations (avoid allocations in loops)
        private static readonly object[] _singleArgArray = new object[1];

        // Species cycling state for V key
        private static int _currentSpeciesIndex = 0;
        private static List<string> _cachedPresentRaces = null;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureReputationTypes()
        {
            if (_repTypesCached) return;

            var repService = GameReflection.GetReputationService();
            if (repService == null)
            {
                return;
            }

            try
            {
                var type = repService.GetType();
                _repReputationProperty = type.GetProperty("Reputation", BindingFlags.Public | BindingFlags.Instance);
                _repPenaltyProperty = type.GetProperty("ReputationPenalty", BindingFlags.Public | BindingFlags.Instance);
                _repGetToWinMethod = type.GetMethod("GetReputationToWin", BindingFlags.Public | BindingFlags.Instance);
                _repGetPenaltyToLooseMethod = type.GetMethod("GetReputationPenaltyToLoose", BindingFlags.Public | BindingFlags.Instance);
                _repGetGainedFromMethod = type.GetMethod("GetReputationGainedFrom", BindingFlags.Public | BindingFlags.Instance);
                _repGetPenaltyPerSecMethod = type.GetMethod("GetReputationPenaltyPerSec", BindingFlags.Public | BindingFlags.Instance);
                _repGetBasePenaltyPerSecMethod = type.GetMethod("GetBaseReputationPenaltyPerSec", BindingFlags.Public | BindingFlags.Instance);
                _repStateProperty = type.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);

                // Get gracePeriodLeft field from GameObjectivesState
                var assembly = GameReflection.GameAssembly;
                var stateType = assembly?.GetType("Eremite.Model.State.GameObjectivesState");
                if (stateType != null)
                {
                    _gracePeriodLeftField = stateType.GetField("gracePeriodLeft", BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached ReputationService types");
                _repTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReputationService type caching failed: {ex.Message}");
            }
        }

        private static void EnsureHostilityTypes()
        {
            if (_hostTypesCached) return;

            var hostService = GameReflection.GetHostilityService();
            if (hostService == null)
            {
                return;
            }

            try
            {
                var type = hostService.GetType();
                _hostPointsProperty = type.GetProperty("Points", BindingFlags.Public | BindingFlags.Instance);
                _hostLevelProperty = type.GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
                _hostGetSourceAmountMethod = type.GetMethod("GetSourceAmount", BindingFlags.Public | BindingFlags.Instance);
                _hostGetPointsForMethod = type.GetMethod("GetPointsFor", BindingFlags.Public | BindingFlags.Instance);
                _hostGetPointsLeftToNextLevelMethod = type.GetMethod("GetPointsLeftToNextLevel", BindingFlags.Public | BindingFlags.Instance);

                Debug.Log("[ATSAccessibility] Cached HostilityService types");
                _hostTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] HostilityService type caching failed: {ex.Message}");
            }
        }

        private static void EnsureResolveTypes()
        {
            if (_resTypesCached) return;

            var resService = GameReflection.GetResolveService();
            if (resService == null)
            {
                return;
            }

            try
            {
                var type = resService.GetType();
                _resGetResolveForMethod = type.GetMethod("GetResolveFor", BindingFlags.Public | BindingFlags.Instance);
                _resGetMinResolveForRepMethod = type.GetMethod("GetMinResolveForReputation",
                    BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                _resGetTargetResolveForMethod = type.GetMethod("GetTargetResolveFor",
                    BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null);
                _resEffectsProperty = type.GetProperty("Effects", BindingFlags.Public | BindingFlags.Instance);

                Debug.Log("[ATSAccessibility] Cached ResolveService types");
                _resTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ResolveService type caching failed: {ex.Message}");
            }
        }

        private static void EnsureVillagersTypes()
        {
            if (_villTypesCached) return;

            var villService = GameReflection.GetVillagersService();
            if (villService == null)
            {
                return;
            }

            try
            {
                var type = villService.GetType();
                _villRacesProperty = type.GetProperty("Races", BindingFlags.Public | BindingFlags.Instance);

                Debug.Log("[ATSAccessibility] Cached VillagersService types");
                _villTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] VillagersService type caching failed: {ex.Message}");
            }
        }

        private static void EnsureEnumTypes()
        {
            if (_enumTypesCached) return;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        _reputationChangeSourceType = assembly.GetType("Eremite.Services.ReputationChangeSource");
                        break;
                    }
                }
                _enumTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Enum type caching failed: {ex.Message}");
            }
        }

        // ========================================
        // DATA ACCESS METHODS
        // ========================================

        /// <summary>
        /// Get reputation summary as (current, target).
        /// </summary>
        public static (float current, int target) GetReputationSummary()
        {
            EnsureReputationTypes();

            var repService = GameReflection.GetReputationService();
            if (repService == null) return (0, 0);

            try
            {
                // Get Reputation.Value from ReactiveProperty
                var repProp = _repReputationProperty?.GetValue(repService);
                float current = 0;
                if (repProp != null)
                {
                    var valueProp = repProp.GetType().GetProperty("Value");
                    current = (float)(valueProp?.GetValue(repProp) ?? 0f);
                }

                // Get target
                int target = (int)(_repGetToWinMethod?.Invoke(repService, null) ?? 0);

                return (current, target);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetReputationSummary failed: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Get impatience (reputation penalty) summary as (current, max).
        /// </summary>
        public static (float current, int max) GetImpatienceSummary()
        {
            EnsureReputationTypes();

            var repService = GameReflection.GetReputationService();
            if (repService == null) return (0, 0);

            try
            {
                // Get ReputationPenalty.Value from ReactiveProperty
                var penaltyProp = _repPenaltyProperty?.GetValue(repService);
                float current = 0;
                if (penaltyProp != null)
                {
                    var valueProp = penaltyProp.GetType().GetProperty("Value");
                    current = (float)(valueProp?.GetValue(penaltyProp) ?? 0f);
                }

                // Get max
                int max = (int)(_repGetPenaltyToLooseMethod?.Invoke(repService, null) ?? 0);

                return (current, max);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetImpatienceSummary failed: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// Format a float to 2 decimal places, stripping trailing zeros.
        /// E.g., 7.50 -> "7.5", 7.25 -> "7.25", 7.00 -> "7"
        /// </summary>
        private static string FormatDecimal(float value)
        {
            return value.ToString("0.##");
        }

        /// <summary>
        /// Get hostility points, level, and points to next level.
        /// </summary>
        public static (int points, int level, int pointsToNext) GetHostilitySummary()
        {
            EnsureHostilityTypes();

            var hostService = GameReflection.GetHostilityService();
            if (hostService == null) return (0, 0, 0);

            try
            {
                // Get Points.Value from ReactiveProperty
                var pointsProp = _hostPointsProperty?.GetValue(hostService);
                int points = 0;
                if (pointsProp != null)
                {
                    var valueProp = pointsProp.GetType().GetProperty("Value");
                    points = (int)(valueProp?.GetValue(pointsProp) ?? 0);
                }

                // Get Level.Value from ReactiveProperty
                var levelProp = _hostLevelProperty?.GetValue(hostService);
                int level = 0;
                if (levelProp != null)
                {
                    var valueProp = levelProp.GetType().GetProperty("Value");
                    level = (int)(valueProp?.GetValue(levelProp) ?? 0);
                }

                // Get points left to next level
                int pointsToNext = 0;
                if (_hostGetPointsLeftToNextLevelMethod != null)
                {
                    var result = _hostGetPointsLeftToNextLevelMethod.Invoke(hostService, null);
                    pointsToNext = result is int p ? p : 0;
                }

                return (points, level, pointsToNext);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetHostilitySummary failed: {ex.Message}");
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// Get resolve for a specific race as (currentResolve, thresholdForReputation, settlingPoint).
        /// </summary>
        public static (float resolve, int threshold, int settling) GetResolveSummary(string race)
        {
            EnsureResolveTypes();

            var resService = GameReflection.GetResolveService();
            if (resService == null) return (0, 0, 0);

            try
            {
                float resolve = (float)(_resGetResolveForMethod?.Invoke(resService, new object[] { race }) ?? 0f);
                int threshold = (int)(_resGetMinResolveForRepMethod?.Invoke(resService, new object[] { race }) ?? 0);
                int settling = (int)(_resGetTargetResolveForMethod?.Invoke(resService, new object[] { race }) ?? 0);

                return (resolve, threshold, settling);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetResolveSummary failed: {ex.Message}");
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// Get list of present races (those with at least one villager).
        /// </summary>
        public static List<string> GetPresentRaces()
        {
            EnsureVillagersTypes();

            var result = new List<string>();
            var villService = GameReflection.GetVillagersService();
            if (villService == null) return result;

            try
            {
                // Get Races dictionary: Dictionary<string, List<Villager>>
                var racesDict = _villRacesProperty?.GetValue(villService);
                if (racesDict == null) return result;

                // Iterate via reflection
                var keysProperty = racesDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(racesDict) as IEnumerable<string>;
                if (keys == null) return result;

                var indexer = racesDict.GetType().GetProperty("Item");

                foreach (var race in keys)
                {
                    var villagerList = indexer?.GetValue(racesDict, new object[] { race });
                    if (villagerList != null)
                    {
                        var countProp = villagerList.GetType().GetProperty("Count");
                        int count = (int)(countProp?.GetValue(villagerList) ?? 0);
                        if (count > 0)
                        {
                            result.Add(race);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetPresentRaces failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get villager count for a specific race.
        /// </summary>
        public static int GetRaceCount(string race)
        {
            EnsureVillagersTypes();

            var villService = GameReflection.GetVillagersService();
            if (villService == null) return 0;

            try
            {
                var racesDict = _villRacesProperty?.GetValue(villService);
                if (racesDict == null) return 0;

                var indexer = racesDict.GetType().GetProperty("Item");
                var villagerList = indexer?.GetValue(racesDict, new object[] { race });
                if (villagerList != null)
                {
                    var countProp = villagerList.GetType().GetProperty("Count");
                    return (int)(countProp?.GetValue(villagerList) ?? 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRaceCount failed: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get base resolve and resilience label for a race.
        /// </summary>
        public static (float baseResolve, string resilience) GetRaceBaseInfo(string raceName)
        {
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return (0, null);

                var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
                if (getRaceMethod == null) return (0, null);

                var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
                if (raceModel == null) return (0, null);

                // Get initialResolve
                var initialResolveField = raceModel.GetType().GetField("initialResolve", GameReflection.PublicInstance);
                float baseResolve = 0;
                if (initialResolveField != null)
                {
                    var val = initialResolveField.GetValue(raceModel);
                    baseResolve = val is float f ? f : 0;
                }

                // Get resilienceLabel
                var resilienceLabelField = raceModel.GetType().GetField("resilienceLabel", GameReflection.PublicInstance);
                string resilience = null;
                if (resilienceLabelField != null)
                {
                    var locaText = resilienceLabelField.GetValue(raceModel);
                    resilience = GameReflection.GetLocaText(locaText);
                }

                return (baseResolve, resilience);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRaceBaseInfo failed: {ex.Message}");
                return (0, null);
            }
        }

        /// <summary>
        /// Get reputation breakdown by source.
        /// Returns list of strings like "+2 from Orders".
        /// </summary>
        public static List<string> GetReputationBreakdown()
        {
            EnsureReputationTypes();
            EnsureEnumTypes();

            var result = new List<string>();
            var repService = GameReflection.GetReputationService();
            if (repService == null || _reputationChangeSourceType == null) return result;

            try
            {
                // ReputationChangeSource enum: Other=0, Order=1, Resolve=2, Relics=3
                string[] sourceNames = { "Other", "Orders", "Resolve", "Relics" };

                for (int i = 0; i < 4; i++)
                {
                    var enumValue = Enum.ToObject(_reputationChangeSourceType, i);
                    _singleArgArray[0] = enumValue;
                    float amount = (float)(_repGetGainedFromMethod?.Invoke(repService, _singleArgArray) ?? 0f);

                    if (amount > 0.01f)
                    {
                        result.Add($"+{amount:F1} from {sourceNames[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetReputationBreakdown failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get impatience breakdown showing rate and grace period.
        /// </summary>
        public static List<string> GetImpatienceBreakdown()
        {
            EnsureReputationTypes();

            var result = new List<string>();
            var repService = GameReflection.GetReputationService();
            if (repService == null) return result;

            try
            {
                // Get current rate
                float ratePerSec = 0f;
                if (_repGetPenaltyPerSecMethod != null)
                {
                    var val = _repGetPenaltyPerSecMethod.Invoke(repService, null);
                    ratePerSec = val is float f ? f : 0f;
                }

                // Get base rate
                float baseRatePerSec = 0f;
                if (_repGetBasePenaltyPerSecMethod != null)
                {
                    var val = _repGetBasePenaltyPerSecMethod.Invoke(repService, null);
                    baseRatePerSec = val is float f ? f : 0f;
                }

                // Format rate per minute for readability
                float ratePerMin = ratePerSec * 60f;
                result.Add($"{ratePerMin:0.##} per minute");

                // Show if rate is modified from base
                if (Mathf.Abs(ratePerSec - baseRatePerSec) > 0.001f)
                {
                    float basePerMin = baseRatePerSec * 60f;
                    float diff = ratePerMin - basePerMin;
                    string prefix = diff > 0 ? "+" : "";
                    result.Add($"{prefix}{diff:0.##} from effects");
                }

                // Get grace period
                if (_repStateProperty != null && _gracePeriodLeftField != null)
                {
                    var state = _repStateProperty.GetValue(repService);
                    if (state != null)
                    {
                        var graceVal = _gracePeriodLeftField.GetValue(state);
                        float grace = graceVal is float g ? g : 0f;
                        if (grace > 0)
                        {
                            int graceSec = Mathf.FloorToInt(grace);
                            result.Add($"{graceSec} seconds grace period");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetImpatienceBreakdown failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get hostility breakdown by source.
        /// Returns list of strings describing hostility sources.
        /// </summary>
        public static List<string> GetHostilityBreakdown()
        {
            EnsureHostilityTypes();

            var result = new List<string>();
            var hostService = GameReflection.GetHostilityService();
            if (hostService == null) return result;

            try
            {
                // Cache HostilitySource type outside loop
                if (_hostilitySourceType == null)
                {
                    _hostilitySourceType = hostService.GetType().Assembly.GetType("Eremite.Model.State.HostilitySource");
                }

                if (_hostilitySourceType == null) return result;

                // HostilitySource enum values and their meanings
                var sources = new (int value, string name)[]
                {
                    (10, "Years"),
                    (20, "Glades"),
                    (30, "Dangerous Glades"),
                    (40, "Forbidden Glades"),
                    (50, "Villagers"),
                    (70, "Woodcutters"),
                    (80, "Burning Hearths"),
                    (90, "Reputation Penalty"),
                    (100, "Resources Removed"),
                    (1000, "Effects (negative)"),
                    (1001, "Effects (positive)")
                };

                foreach (var (value, name) in sources)
                {
                    try
                    {
                        var enumValue = Enum.ToObject(_hostilitySourceType, value);
                        _singleArgArray[0] = enumValue;
                        int points = (int)(_hostGetPointsForMethod?.Invoke(hostService, _singleArgArray) ?? 0);

                        if (points != 0)
                        {
                            string prefix = points > 0 ? "+" : "";
                            result.Add($"{prefix}{points} from {name}");
                        }
                    }
                    catch
                    {
                        // Source not configured for this biome, skip it
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetHostilityBreakdown failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get resolve breakdown for a race (all effects affecting resolve).
        /// </summary>
        public static List<string> GetResolveBreakdown(string race)
        {
            EnsureResolveTypes();

            var result = new List<string>();
            var resService = GameReflection.GetResolveService();
            if (resService == null) return result;

            try
            {
                // Add base resolve and resilience at the top
                var raceInfo = GetRaceBaseInfo(race);
                if (raceInfo.baseResolve > 0)
                {
                    result.Add($"Base resolve: {raceInfo.baseResolve}");
                }
                if (!string.IsNullOrEmpty(raceInfo.resilience))
                {
                    result.Add($"Resilience: {raceInfo.resilience}");
                }

                // Effects is Dictionary<string, Dictionary<ResolveEffectModel, int>>
                var effectsDict = _resEffectsProperty?.GetValue(resService);
                if (effectsDict == null) return result;

                // Get the race's effects dictionary
                var indexer = effectsDict.GetType().GetProperty("Item");
                var raceEffects = indexer?.GetValue(effectsDict, new object[] { race });
                if (raceEffects == null) return result;

                // Get total population for this race
                int totalPopulation = GetRaceCount(race);

                // Iterate through the effects
                var enumerator = raceEffects.GetType().GetMethod("GetEnumerator")?.Invoke(raceEffects, null);
                if (enumerator == null) return result;

                var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
                var currentProp = enumerator.GetType().GetProperty("Current");

                while ((bool)moveNextMethod.Invoke(enumerator, null))
                {
                    var kvp = currentProp.GetValue(enumerator);
                    var keyProp = kvp.GetType().GetProperty("Key");
                    var valueProp = kvp.GetType().GetProperty("Value");

                    var effectModel = keyProp?.GetValue(kvp);
                    int count = (int)(valueProp?.GetValue(kvp) ?? 0);

                    if (effectModel != null && count > 0)
                    {
                        // Get effect name
                        var displayNameField = effectModel.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                        var nameProp = effectModel.GetType().GetProperty("Name");

                        var locaText = displayNameField?.GetValue(effectModel);
                        string name = GameReflection.GetLocaText(locaText)
                            ?? nameProp?.GetValue(effectModel)?.ToString()
                            ?? "Unknown effect";

                        // Get per-villager resolve value
                        int perVillager = 0;
                        var resProp = effectModel.GetType().GetProperty("resolve");
                        var resField = effectModel.GetType().GetField("resolve", BindingFlags.Public | BindingFlags.Instance);
                        if (resProp != null)
                            perVillager = (int)(resProp.GetValue(effectModel) ?? 0);
                        else if (resField != null)
                            perVillager = (int)(resField.GetValue(effectModel) ?? 0);

                        // Get actual average impact from ResolveService
                        int actualImpact = 0;
                        try
                        {
                            // Must specify parameter types due to method overloads
                            var getRoundedAvgMethod = resService.GetType().GetMethod("GetRoundedAverageResolveImpact",
                                BindingFlags.Public | BindingFlags.Instance,
                                null,
                                new Type[] { typeof(string), effectModel.GetType() },
                                null);
                            if (getRoundedAvgMethod != null)
                            {
                                actualImpact = (int)getRoundedAvgMethod.Invoke(resService, new object[] { race, effectModel });
                            }
                            else
                            {
                                Debug.LogWarning($"[ATSAccessibility] GetRoundedAverageResolveImpact method not found for {name}");
                                actualImpact = perVillager;  // Fallback
                            }
                        }
                        catch (Exception avgEx)
                        {
                            Debug.LogWarning($"[ATSAccessibility] GetRoundedAverageResolveImpact failed for {name}: {avgEx.Message}");
                            actualImpact = perVillager;  // Fallback
                        }

                        // Format: "Biscuits: +3 (+5 for 5/9 villagers)"
                        string avgPrefix = actualImpact >= 0 ? "+" : "";
                        string perPrefix = perVillager >= 0 ? "+" : "";
                        result.Add($"{name}: {avgPrefix}{actualImpact} ({perPrefix}{perVillager} for {count}/{totalPopulation} villagers)");
                    }
                }

                // Dispose if IDisposable
                if (enumerator is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetResolveBreakdown failed: {ex.Message}");
            }

            return result;
        }

        // ========================================
        // QUICK HOTKEY HANDLERS
        // ========================================

        /// <summary>
        /// Announce quick summary: Reputation, Impatience, Hostility (S key).
        /// </summary>
        public static void AnnounceQuickSummary()
        {
            var rep = GetReputationSummary();
            var imp = GetImpatienceSummary();
            var host = GetHostilitySummary();

            int hostilityThreshold = host.points + host.pointsToNext;
            string message = $"Reputation {FormatDecimal(rep.current)} of {rep.target}, " +
                           $"Impatience {FormatDecimal(imp.current)} of {imp.max}, " +
                           $"Hostility level {host.level}, {host.points}/{hostilityThreshold}";

            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Stats: {message}");
        }

        /// <summary>
        /// Announce resolve summary for all present species (R key).
        /// </summary>
        public static void AnnounceResolveSummary()
        {
            var races = GetPresentRaces();

            if (races.Count == 0)
            {
                Speech.Say("No species present");
                return;
            }

            var parts = new List<string>();
            foreach (var race in races)
            {
                var (resolve, threshold, _) = GetResolveSummary(race);

                // Format: "Humans 24/30" (current resolve / threshold)
                parts.Add($"{race} {Mathf.FloorToInt(resolve)} of {threshold}");
            }

            string message = string.Join(", ", parts);
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Resolve: {message}");
        }

        /// <summary>
        /// Announce next species in rotation with population and resolve (V key).
        /// Cycles through present species one at a time.
        /// </summary>
        public static void AnnounceNextSpeciesResolve()
        {
            // Refresh the list of present races
            _cachedPresentRaces = GetPresentRaces();

            if (_cachedPresentRaces.Count == 0)
            {
                Speech.Say("No species present");
                return;
            }

            // Wrap index if needed
            if (_currentSpeciesIndex >= _cachedPresentRaces.Count)
            {
                _currentSpeciesIndex = 0;
            }

            string race = _cachedPresentRaces[_currentSpeciesIndex];
            int population = GetRaceCount(race);
            var (resolve, threshold, _) = GetResolveSummary(race);

            // Pluralize species name if more than 1
            string raceName = population == 1 ? race : race + "s";

            // Format: "7 Humans, resolve 8 of 15"
            string message = $"{population} {raceName}, resolve {Mathf.FloorToInt(resolve)} of {threshold}";
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Species resolve: {message}");

            // Advance to next species for next press
            _currentSpeciesIndex++;
            if (_currentSpeciesIndex >= _cachedPresentRaces.Count)
            {
                _currentSpeciesIndex = 0;
            }
        }

        // ========================================
        // TIME/SEASON (T key)
        // ========================================

        // Season names for announcement
        private static readonly string[] SeasonNames = { "Drizzle", "Clearance", "Storm" };

        /// <summary>
        /// Get time summary as (year, seasonName, secondsToNextSeason).
        /// </summary>
        public static (int year, string season, float secondsRemaining) GetTimeSummary()
        {
            int year = GameReflection.GetYear();
            int seasonIndex = GameReflection.GetSeason();
            string season = seasonIndex >= 0 && seasonIndex < SeasonNames.Length
                ? SeasonNames[seasonIndex] : "Unknown";
            float seconds = GameReflection.GetTimeTillNextSeason();
            return (year, season, seconds);
        }

        /// <summary>
        /// Announce current season, time remaining, and year (T key).
        /// </summary>
        public static void AnnounceTimeSummary()
        {
            var (year, season, seconds) = GetTimeSummary();

            // Format time remaining as "X minutes Y seconds" or just "X seconds"
            string timeRemaining;
            if (seconds >= 60)
            {
                int minutes = Mathf.FloorToInt(seconds / 60);
                int secs = Mathf.FloorToInt(seconds % 60);
                timeRemaining = secs > 0 ? $"{minutes} minutes {secs} seconds" : $"{minutes} minutes";
            }
            else
            {
                timeRemaining = $"{Mathf.FloorToInt(seconds)} seconds";
            }

            string message = $"{season}, {timeRemaining} remaining, Year {year}";
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Time: {message}");
        }
    }
}
