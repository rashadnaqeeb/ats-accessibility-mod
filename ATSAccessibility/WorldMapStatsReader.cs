using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reads meta-level game statistics for world map announcements.
    /// Provides level, meta resources, seal info, and cycle data.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references - they are destroyed on scene change
    /// </summary>
    public static class WorldMapStatsReader
    {
        // ========================================
        // META STATE SERVICE REFLECTION
        // ========================================
        // Path: MetaController.Instance.MetaServices.MetaStateService

        private static PropertyInfo _msMetaStateServiceProperty = null;
        private static PropertyInfo _mssLevelProperty = null;          // MetaStateService.Level -> LevelState
        private static PropertyInfo _mssEconomyProperty = null;        // MetaStateService.Economy -> MetaEconomyState
        private static bool _metaStateTypesCached = false;

        // LevelState fields
        private static FieldInfo _levelStateLevel = null;
        private static FieldInfo _levelStateExp = null;
        private static FieldInfo _levelStateTargetExp = null;

        // MetaEconomyState fields
        private static FieldInfo _economyMetaCurrencies = null;

        // WorldStateService Cycle access
        private static PropertyInfo _wssCycleProperty = null;
        private static PropertyInfo _wssSealsProperty = null;
        private static bool _worldStateTypesCached = false;

        // CycleState fields
        private static FieldInfo _cycleYear = null;
        private static FieldInfo _cycleYearsInCycle = null;
        private static FieldInfo _cycleGamesPlayed = null;
        private static FieldInfo _cycleGamesWon = null;
        private static FieldInfo _cycleSealFragments = null;

        // Settings methods for meta currency lookup
        private static MethodInfo _settingsGetMetaCurrency = null;

        // World seals service
        private static PropertyInfo _wssWorldSealsServiceProperty = null;
        private static MethodInfo _sealsWasAnyCompleted = null;
        private static MethodInfo _sealsGetHighestWon = null;

        private static void EnsureMetaStateTypes()
        {
            if (_metaStateTypesCached) return;
            GameReflection.EnsureTutorialTypesInternal();

            var gameAssembly = GameReflection.GameAssembly;
            if (gameAssembly == null)
            {
                _metaStateTypesCached = true;
                return;
            }

            try
            {
                // Get MetaStateService property from IMetaServices
                var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Level and Economy from IMetaStateService
                var metaStateServiceType = gameAssembly.GetType("Eremite.Services.IMetaStateService");
                if (metaStateServiceType != null)
                {
                    _mssLevelProperty = metaStateServiceType.GetProperty("Level",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mssEconomyProperty = metaStateServiceType.GetProperty("Economy",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get LevelState fields
                var levelStateType = gameAssembly.GetType("Eremite.Model.State.LevelState");
                if (levelStateType != null)
                {
                    _levelStateLevel = levelStateType.GetField("level",
                        BindingFlags.Public | BindingFlags.Instance);
                    _levelStateExp = levelStateType.GetField("exp",
                        BindingFlags.Public | BindingFlags.Instance);
                    _levelStateTargetExp = levelStateType.GetField("targetExp",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get MetaEconomyState fields
                var metaEconomyStateType = gameAssembly.GetType("Eremite.Model.State.MetaEconomyState");
                if (metaEconomyStateType != null)
                {
                    _economyMetaCurrencies = metaEconomyStateType.GetField("metaCurrencies",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Settings.GetMetaCurrency method
                var settingsType = gameAssembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetMetaCurrency = settingsType.GetMethod("GetMetaCurrency",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(string) },
                        null);
                }

                Debug.Log("[ATSAccessibility] Cached MetaStateService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] MetaStateService type caching failed: {ex.Message}");
            }

            _metaStateTypesCached = true;
        }

        private static void EnsureWorldStateTypes()
        {
            if (_worldStateTypesCached) return;
            GameReflection.EnsureTutorialTypesInternal();

            var gameAssembly = GameReflection.GameAssembly;
            if (gameAssembly == null)
            {
                _worldStateTypesCached = true;
                return;
            }

            try
            {
                // Get Cycle property from IWorldStateService
                var worldStateServiceType = gameAssembly.GetType("Eremite.Services.IWorldStateService");
                if (worldStateServiceType != null)
                {
                    _wssCycleProperty = worldStateServiceType.GetProperty("Cycle",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wssSealsProperty = worldStateServiceType.GetProperty("Seals",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get CycleState fields
                var cycleStateType = gameAssembly.GetType("Eremite.WorldMap.CycleState");
                if (cycleStateType != null)
                {
                    _cycleYear = cycleStateType.GetField("year",
                        BindingFlags.Public | BindingFlags.Instance);
                    _cycleYearsInCycle = cycleStateType.GetField("yearsInCycle",
                        BindingFlags.Public | BindingFlags.Instance);
                    _cycleGamesPlayed = cycleStateType.GetField("gamesPlayedInCycle",
                        BindingFlags.Public | BindingFlags.Instance);
                    _cycleGamesWon = cycleStateType.GetField("gamesWonInCycle",
                        BindingFlags.Public | BindingFlags.Instance);
                    _cycleSealFragments = cycleStateType.GetField("sealFragments",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get WorldSealsService from IWorldServices
                var worldServicesType = gameAssembly.GetType("Eremite.Services.World.IWorldServices");
                if (worldServicesType != null)
                {
                    _wssWorldSealsServiceProperty = worldServicesType.GetProperty("WorldSealsService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get seal service methods
                var worldSealsServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldSealsService");
                if (worldSealsServiceType != null)
                {
                    _sealsWasAnyCompleted = worldSealsServiceType.GetMethod("WasAnySealEverCompleted",
                        BindingFlags.Public | BindingFlags.Instance);
                    _sealsGetHighestWon = worldSealsServiceType.GetMethod("GetHighestWonSeal",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached WorldStateService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WorldStateService type caching failed: {ex.Message}");
            }

            _worldStateTypesCached = true;
        }

        /// <summary>
        /// Get MetaStateService from MetaController.
        /// </summary>
        private static object GetMetaStateService()
        {
            EnsureMetaStateTypes();

            try
            {
                var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null) return null;

                return _msMetaStateServiceProperty?.GetValue(metaServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get player level info.
        /// Returns (level, currentXP, targetXP).
        /// </summary>
        public static (int level, int currentXP, int targetXP) GetLevelInfo()
        {
            EnsureMetaStateTypes();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null) return (0, 0, 0);

                var levelState = _mssLevelProperty?.GetValue(metaStateService);
                if (levelState == null) return (0, 0, 0);

                var level = (int)(_levelStateLevel?.GetValue(levelState) ?? 0);
                var exp = (int)(_levelStateExp?.GetValue(levelState) ?? 0);
                var targetExp = (int)(_levelStateTargetExp?.GetValue(levelState) ?? 0);

                return (level, exp, targetExp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetLevelInfo failed: {ex.Message}");
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// Get meta resources (Food, Machinery, Artifacts, etc.) with their amounts.
        /// Returns list of (displayName, amount) tuples.
        /// </summary>
        public static List<(string name, int amount)> GetMetaResources()
        {
            EnsureMetaStateTypes();
            var result = new List<(string name, int amount)>();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null) return result;

                var economyState = _mssEconomyProperty?.GetValue(metaStateService);
                if (economyState == null) return result;

                var currencies = _economyMetaCurrencies?.GetValue(economyState) as System.Collections.IDictionary;
                if (currencies == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                foreach (System.Collections.DictionaryEntry entry in currencies)
                {
                    var currencyName = entry.Key as string;
                    var amount = (int)entry.Value;

                    if (string.IsNullOrEmpty(currencyName) || amount <= 0) continue;

                    // Get display name from MetaCurrencyModel
                    var displayName = currencyName;
                    if (_settingsGetMetaCurrency != null)
                    {
                        var model = _settingsGetMetaCurrency.Invoke(settings, new object[] { currencyName });
                        if (model != null)
                        {
                            var displayNameProp = model.GetType().GetProperty("DisplayName",
                                BindingFlags.Public | BindingFlags.Instance);
                            displayName = displayNameProp?.GetValue(model) as string ?? currencyName;
                        }
                    }

                    result.Add((displayName, amount));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetMetaResources failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get info about the highest reforged seal.
        /// Returns (sealName, rewardsMultiplier, bonusYears, currentFragments).
        /// Returns empty values if no seals reforged.
        /// </summary>
        public static (string sealName, float rewardsMult, int bonusYears, int fragments) GetHighestSealInfo()
        {
            EnsureWorldStateTypes();

            try
            {
                var worldServices = WorldMapReflection.GetWorldServices();
                if (worldServices == null) return (null, 0f, 0, 0);

                // Get WorldSealsService
                var sealsService = _wssWorldSealsServiceProperty?.GetValue(worldServices);
                if (sealsService == null) return (null, 0f, 0, 0);

                // Check if any seal was completed
                var wasCompleted = _sealsWasAnyCompleted?.Invoke(sealsService, null);
                if (wasCompleted == null || !(bool)wasCompleted)
                    return (null, 0f, 0, 0);

                // Get highest won seal
                var highestSeal = _sealsGetHighestWon?.Invoke(sealsService, null);
                if (highestSeal == null) return (null, 0f, 0, 0);

                // Get seal displayName
                var displayNameField = highestSeal.GetType().GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                var displayName = displayNameField?.GetValue(highestSeal);
                var sealName = GameReflection.GetLocaText(displayName) ?? "";

                // Get rewardsMultiplier
                var rewardsMulField = highestSeal.GetType().GetField("rewardsMultiplier",
                    BindingFlags.Public | BindingFlags.Instance);
                var rewardsMult = (float)(rewardsMulField?.GetValue(highestSeal) ?? 0f);

                // Get bonusYearsPerCycle
                var bonusYearsField = highestSeal.GetType().GetField("bonusYearsPerCycle",
                    BindingFlags.Public | BindingFlags.Instance);
                var bonusYears = (int)(bonusYearsField?.GetValue(highestSeal) ?? 0);

                // Get current seal fragments from CycleState
                var worldStateService = WorldMapReflection.GetWorldStateService();
                int fragments = 0;
                if (worldStateService != null)
                {
                    var cycleState = _wssCycleProperty?.GetValue(worldStateService);
                    if (cycleState != null)
                    {
                        fragments = (int)(_cycleSealFragments?.GetValue(cycleState) ?? 0);
                    }
                }

                return (sealName, rewardsMult, bonusYears, fragments);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetHighestSealInfo failed: {ex.Message}");
                return (null, 0f, 0, 0);
            }
        }

        /// <summary>
        /// Get cycle/storm information.
        /// Returns (year, yearsInCycle, gamesWon, gamesPlayed, sealFragments).
        /// </summary>
        public static (int year, int yearsInCycle, int gamesWon, int gamesPlayed, int sealFragments) GetCycleInfo()
        {
            EnsureWorldStateTypes();

            try
            {
                var worldStateService = WorldMapReflection.GetWorldStateService();
                if (worldStateService == null) return (0, 0, 0, 0, 0);

                var cycleState = _wssCycleProperty?.GetValue(worldStateService);
                if (cycleState == null) return (0, 0, 0, 0, 0);

                var year = (int)(_cycleYear?.GetValue(cycleState) ?? 0);
                var yearsInCycle = (int)(_cycleYearsInCycle?.GetValue(cycleState) ?? 0);
                var gamesPlayed = (int)(_cycleGamesPlayed?.GetValue(cycleState) ?? 0);
                var gamesWon = (int)(_cycleGamesWon?.GetValue(cycleState) ?? 0);
                var sealFragments = (int)(_cycleSealFragments?.GetValue(cycleState) ?? 0);

                return (year, yearsInCycle, gamesWon, gamesPlayed, sealFragments);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCycleInfo failed: {ex.Message}");
                return (0, 0, 0, 0, 0);
            }
        }

        // ========================================
        // ANNOUNCEMENT METHODS (called by KeyboardManager)
        // ========================================

        /// <summary>
        /// Announce player level and XP to next level.
        /// </summary>
        public static void AnnounceLevel()
        {
            var (level, currentXP, targetXP) = GetLevelInfo();
            int remaining = targetXP - currentXP;
            Speech.Say($"Level {level}, {remaining} experience to next level");
        }

        /// <summary>
        /// Announce meta resources with counts.
        /// </summary>
        public static void AnnounceMetaResources()
        {
            var resources = GetMetaResources();
            if (resources.Count == 0)
            {
                Speech.Say("No meta resources");
                return;
            }

            var parts = new List<string>();
            foreach (var (name, amount) in resources)
            {
                parts.Add($"{amount} {name}");
            }
            Speech.Say(string.Join(", ", parts));
        }

        /// <summary>
        /// Announce highest seal info or "No seals reforged".
        /// </summary>
        public static void AnnounceSealInfo()
        {
            var (name, mult, years, frags) = GetHighestSealInfo();
            if (string.IsNullOrEmpty(name))
            {
                Speech.Say("No seals reforged");
                return;
            }

            int rewardsPercent = (int)(mult * 100);
            Speech.Say($"{name}, {rewardsPercent} percent rewards, {years} bonus years, {frags} fragments");
        }

        /// <summary>
        /// Announce cycle/storm info.
        /// Seal fragments are reported by S key, not repeated here.
        /// </summary>
        public static void AnnounceCycleInfo()
        {
            var (year, yearsInCycle, won, played, _) = GetCycleInfo();
            int yearsLeft = yearsInCycle - year;

            string cycleStatus;
            if (yearsLeft <= 0)
                cycleStatus = "Blightstorm approaching, press E to end cycle";
            else
                cycleStatus = $"{yearsLeft} years left in cycle";

            Speech.Say($"Year {year}, {cycleStatus}, {won} of {played} games won");
        }

        /// <summary>
        /// Check if the Blightstorm is approaching (cycle can be finished).
        /// </summary>
        public static bool IsBlightstormApproaching()
        {
            var (year, yearsInCycle, _, _, _) = GetCycleInfo();
            return year > yearsInCycle - 1;
        }

        /// <summary>
        /// Open the cycle end popup to trigger the Blightstorm.
        /// </summary>
        public static bool OpenCycleEndPopup()
        {
            if (!IsBlightstormApproaching())
            {
                Speech.Say("Cannot end cycle yet");
                return false;
            }

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null) return false;

            if (GameReflection.InvokeSubjectOnNext(wbb, "CycleEndPopupRequested", null))
            {
                return true;
            }

            Speech.Say("Failed to open cycle end");
            return false;
        }
    }
}
