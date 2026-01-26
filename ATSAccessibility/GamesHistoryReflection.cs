using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing Games History popup data.
    /// Accesses WorldStateService for cycle stats, MetaStateService for perks and history.
    /// </summary>
    public static class GamesHistoryReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // GamesHistoryPopup type for detection
        private static Type _gamesHistoryPopupType;

        // MetaStateService access: MetaController.Instance.MetaServices.MetaStateService
        private static PropertyInfo _msMetaStateServiceProperty;

        // MetaStateService.Perks → MetaPerksState
        private static PropertyInfo _mssPerksProperty;

        // MetaStateService.GamesHistory → GamesHistoryState
        private static PropertyInfo _mssGamesHistoryProperty;

        // MetaStateService.Stats → MetaStats
        private static PropertyInfo _mssStatsProperty;

        // MetaStateService.Goals → MetaGoalsState (for counting completed goals)
        private static PropertyInfo _mssGoalsProperty;
        private static FieldInfo _mgsGoalsField;
        private static FieldInfo _goalStateCompletedField;

        // MetaStats fields
        private static FieldInfo _statsGamesWonField;
        private static FieldInfo _statsGamesLostField;
        private static FieldInfo _statsTimeSpentInGameField;

        // GamesHistoryState.records → List<GameHistoryState>
        private static FieldInfo _ghsRecordsField;

        // GameHistoryState fields
        private static FieldInfo _ghsNameField;
        private static FieldInfo _ghsHasStaticNameField;
        private static FieldInfo _ghsHasWonField;
        private static FieldInfo _ghsDifficultyField;
        private static FieldInfo _ghsBiomeField;
        private static FieldInfo _ghsLevelField;
        private static FieldInfo _ghsUpgradesField;
        private static FieldInfo _ghsYearsField;
        private static FieldInfo _ghsGameTimeField;
        private static FieldInfo _ghsRacesField;
        private static FieldInfo _ghsCornerstonesField;
        private static FieldInfo _ghsModifiersField;
        private static FieldInfo _ghsBuildingsField;
        private static FieldInfo _ghsSeasonalEffectsField;

        // MetaPerksState fields (26 upgrades)
        private static FieldInfo _perksBonusReputationRewardsPicksField;
        private static FieldInfo _perksBonusPreparationPointsField;
        private static FieldInfo _perksBonusSeasonRewardsAmountField;
        private static FieldInfo _perksBonusCaravansField;
        private static FieldInfo _perksBonusTradeRoutesLimitField;
        private static FieldInfo _perksBonusCapitalVisionField;
        private static FieldInfo _perksBonusTownsVisionField;
        private static FieldInfo _perksBonusEmbarkRangeField;
        private static FieldInfo _perksBonusTraderMerchSlotsField;
        private static FieldInfo _perksRawDepositsChargesBonusField;
        private static FieldInfo _perksGlobalBuildingStorageBonusField;
        private static FieldInfo _perksBonusCornerstonesRerollsField;
        private static FieldInfo _perksBonusGracePeriodField;
        private static FieldInfo _perksGlobalCapacityBonusField;
        private static FieldInfo _perksBonusFarmAreaField;
        private static FieldInfo _perksCurrencyMultiplayerField;
        private static FieldInfo _perksTraderMerchandisePriceBonusRatesField;
        private static FieldInfo _perksTradersIntervalBonusRateField;
        private static FieldInfo _perksReputationPenaltyBonusRateField;
        private static FieldInfo _perksGlobalSpeedBonusRateField;
        private static FieldInfo _perksFuelConsumptionBonusRateField;
        private static FieldInfo _perksNewcommersGoodsBonusRateField;
        private static FieldInfo _perksGlobalProductionSpeedBonusRateField;
        private static FieldInfo _perksHearthSacraficeTimeBonusRateField;
        private static FieldInfo _perksBonusEmbarkGoodsAmountField;
        private static FieldInfo _perksGlobalExtraProductionChanceBonusField;

        // WorldStateService access (from MetaServices)
        private static PropertyInfo _msWorldStateServiceProperty;

        // WorldStateService.Cycle → CycleState
        private static PropertyInfo _wssCycleProperty;

        // CycleState fields
        private static FieldInfo _cycleGamesWonInCycleField;
        private static FieldInfo _cycleGamesPlayedInCycleField;
        private static FieldInfo _cycleSealFragmentsField;
        private static FieldInfo _cycleTotalSealFragmentsField;
        private static FieldInfo _cycleFinishedModifiersField;

        // Settings methods for resolving names
        private static MethodInfo _settingsGetDifficultyMethod;
        private static MethodInfo _settingsGetBiomeMethod;
        private static MethodInfo _settingsGetRaceMethod;
        private static MethodInfo _settingsGetEffectMethod;
        private static MethodInfo _settingsGetModifierMethod;
        private static MethodInfo _settingsGetBuildingMethod;
        private static MethodInfo _settingsGetSeasonalEffectMethod;
        private static MethodInfo _settingsContainsBiomeMethod;
        private static MethodInfo _settingsGetTextMethod;

        // Model display name methods/properties
        private static MethodInfo _difficultyRawDisplayNameMethod;
        private static FieldInfo _biomeDisplayNameField;
        private static FieldInfo _raceDisplayNameField;
        private static PropertyInfo _effectDisplayNameProperty;
        private static FieldInfo _buildingDisplayNameField;

        // RichTextService for time formatting
        private static MethodInfo _richTextServiceGetMinSecTimerMethod;

        // TextsService for localization
        private static PropertyInfo _servicesTextsServiceProperty;
        private static MethodInfo _textsServiceGetLocaTextMethod;
        private static MethodInfo _textsServiceShouldUseRomanMethod;

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
                    Debug.LogWarning("[ATSAccessibility] GamesHistoryReflection: Game assembly not available");
                    return;
                }

                CachePopupType(assembly);
                CacheMetaStateTypes(assembly);
                CacheWorldStateTypes(assembly);
                CacheGameHistoryTypes(assembly);
                CachePerksTypes(assembly);
                CacheSettingsTypes(assembly);
                CacheModelTypes(assembly);
                CacheServiceTypes(assembly);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupType(Assembly assembly)
        {
            _gamesHistoryPopupType = assembly.GetType("Eremite.WorldMap.UI.History.GamesHistoryPopup");
        }

        private static void CacheMetaStateTypes(Assembly assembly)
        {
            var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
            if (metaServicesType != null)
            {
                _msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
                    GameReflection.PublicInstance);
                _msWorldStateServiceProperty = metaServicesType.GetProperty("WorldStateService",
                    GameReflection.PublicInstance);
            }

            var metaStateServiceType = assembly.GetType("Eremite.Services.IMetaStateService");
            if (metaStateServiceType != null)
            {
                _mssPerksProperty = metaStateServiceType.GetProperty("Perks",
                    GameReflection.PublicInstance);
                _mssGamesHistoryProperty = metaStateServiceType.GetProperty("GamesHistory",
                    GameReflection.PublicInstance);
                _mssStatsProperty = metaStateServiceType.GetProperty("Stats",
                    GameReflection.PublicInstance);
                _mssGoalsProperty = metaStateServiceType.GetProperty("Goals",
                    GameReflection.PublicInstance);
            }

            // MetaStats fields
            var metaStatsType = assembly.GetType("Eremite.Model.State.MetaStats");
            if (metaStatsType != null)
            {
                _statsGamesWonField = metaStatsType.GetField("gamesWon", GameReflection.PublicInstance);
                _statsGamesLostField = metaStatsType.GetField("gamesLost", GameReflection.PublicInstance);
                _statsTimeSpentInGameField = metaStatsType.GetField("timeSpentInGame", GameReflection.PublicInstance);
            }

            // MetaGoalsState for counting completed goals
            var metaGoalsStateType = assembly.GetType("Eremite.Model.State.MetaGoalsState")
                ?? assembly.GetType("Eremite.Model.Goals.MetaGoalsState");
            if (metaGoalsStateType != null)
            {
                _mgsGoalsField = metaGoalsStateType.GetField("goals", GameReflection.PublicInstance);
            }

            // GoalState for checking completion
            var goalStateType = assembly.GetType("Eremite.Model.Goals.GoalState");
            if (goalStateType != null)
            {
                _goalStateCompletedField = goalStateType.GetField("completed", GameReflection.PublicInstance);
            }
        }

        private static void CacheWorldStateTypes(Assembly assembly)
        {
            var worldStateServiceType = assembly.GetType("Eremite.Services.IWorldStateService");
            if (worldStateServiceType != null)
            {
                _wssCycleProperty = worldStateServiceType.GetProperty("Cycle",
                    GameReflection.PublicInstance);
            }

            var cycleStateType = assembly.GetType("Eremite.WorldMap.CycleState");
            if (cycleStateType != null)
            {
                _cycleGamesWonInCycleField = cycleStateType.GetField("gamesWonInCycle", GameReflection.PublicInstance);
                _cycleGamesPlayedInCycleField = cycleStateType.GetField("gamesPlayedInCycle", GameReflection.PublicInstance);
                _cycleSealFragmentsField = cycleStateType.GetField("sealFragments", GameReflection.PublicInstance);
                _cycleTotalSealFragmentsField = cycleStateType.GetField("totalSealFragments", GameReflection.PublicInstance);
                _cycleFinishedModifiersField = cycleStateType.GetField("finishedModifiers", GameReflection.PublicInstance);
            }
        }

        private static void CacheGameHistoryTypes(Assembly assembly)
        {
            var gamesHistoryStateType = assembly.GetType("Eremite.Model.State.GamesHistoryState");
            if (gamesHistoryStateType != null)
            {
                _ghsRecordsField = gamesHistoryStateType.GetField("records", GameReflection.PublicInstance);
            }

            var gameHistoryStateType = assembly.GetType("Eremite.Model.State.GameHistoryState");
            if (gameHistoryStateType != null)
            {
                _ghsNameField = gameHistoryStateType.GetField("name", GameReflection.PublicInstance);
                _ghsHasStaticNameField = gameHistoryStateType.GetField("hasStaticName", GameReflection.PublicInstance);
                _ghsHasWonField = gameHistoryStateType.GetField("hasWon", GameReflection.PublicInstance);
                _ghsDifficultyField = gameHistoryStateType.GetField("difficulty", GameReflection.PublicInstance);
                _ghsBiomeField = gameHistoryStateType.GetField("biome", GameReflection.PublicInstance);
                _ghsLevelField = gameHistoryStateType.GetField("level", GameReflection.PublicInstance);
                _ghsUpgradesField = gameHistoryStateType.GetField("upgrades", GameReflection.PublicInstance);
                _ghsYearsField = gameHistoryStateType.GetField("years", GameReflection.PublicInstance);
                _ghsGameTimeField = gameHistoryStateType.GetField("gameTime", GameReflection.PublicInstance);
                _ghsRacesField = gameHistoryStateType.GetField("races", GameReflection.PublicInstance);
                _ghsCornerstonesField = gameHistoryStateType.GetField("cornerstones", GameReflection.PublicInstance);
                _ghsModifiersField = gameHistoryStateType.GetField("modifiers", GameReflection.PublicInstance);
                _ghsBuildingsField = gameHistoryStateType.GetField("buildings", GameReflection.PublicInstance);
                _ghsSeasonalEffectsField = gameHistoryStateType.GetField("seasonalEffects", GameReflection.PublicInstance);
            }
        }

        private static void CachePerksTypes(Assembly assembly)
        {
            var perksStateType = assembly.GetType("Eremite.Model.State.MetaPerksState");
            if (perksStateType != null)
            {
                _perksBonusReputationRewardsPicksField = perksStateType.GetField("bonusReputationRewardsPicks", GameReflection.PublicInstance);
                _perksBonusPreparationPointsField = perksStateType.GetField("bonusPreparationPoints", GameReflection.PublicInstance);
                _perksBonusSeasonRewardsAmountField = perksStateType.GetField("bonusSeasonRewardsAmount", GameReflection.PublicInstance);
                _perksBonusCaravansField = perksStateType.GetField("bonusCaravans", GameReflection.PublicInstance);
                _perksBonusTradeRoutesLimitField = perksStateType.GetField("bonusTradeRoutesLimit", GameReflection.PublicInstance);
                _perksBonusCapitalVisionField = perksStateType.GetField("bonusCapitalVision", GameReflection.PublicInstance);
                _perksBonusTownsVisionField = perksStateType.GetField("bonusTownsVision", GameReflection.PublicInstance);
                _perksBonusEmbarkRangeField = perksStateType.GetField("bonusEmbarkRange", GameReflection.PublicInstance);
                _perksBonusTraderMerchSlotsField = perksStateType.GetField("bonusTraderMerchSlots", GameReflection.PublicInstance);
                _perksRawDepositsChargesBonusField = perksStateType.GetField("rawDepositsChargesBonus", GameReflection.PublicInstance);
                _perksGlobalBuildingStorageBonusField = perksStateType.GetField("globalBuildingStorageBonus", GameReflection.PublicInstance);
                _perksBonusCornerstonesRerollsField = perksStateType.GetField("bonusCornerstonesRerolls", GameReflection.PublicInstance);
                _perksBonusGracePeriodField = perksStateType.GetField("bonusGracePeriod", GameReflection.PublicInstance);
                _perksGlobalCapacityBonusField = perksStateType.GetField("globalCapacityBonus", GameReflection.PublicInstance);
                _perksBonusFarmAreaField = perksStateType.GetField("bonusFarmArea", GameReflection.PublicInstance);
                _perksCurrencyMultiplayerField = perksStateType.GetField("currencyMultiplayer", GameReflection.PublicInstance);
                _perksTraderMerchandisePriceBonusRatesField = perksStateType.GetField("traderMerchandisePriceBonusRates", GameReflection.PublicInstance);
                _perksTradersIntervalBonusRateField = perksStateType.GetField("tradersIntervalBonusRate", GameReflection.PublicInstance);
                _perksReputationPenaltyBonusRateField = perksStateType.GetField("reputationPenaltyBonusRate", GameReflection.PublicInstance);
                _perksGlobalSpeedBonusRateField = perksStateType.GetField("globalSpeedBonusRate", GameReflection.PublicInstance);
                _perksFuelConsumptionBonusRateField = perksStateType.GetField("fuelConsumptionBonusRate", GameReflection.PublicInstance);
                _perksNewcommersGoodsBonusRateField = perksStateType.GetField("newcommersGoodsBonusRate", GameReflection.PublicInstance);
                _perksGlobalProductionSpeedBonusRateField = perksStateType.GetField("globalProductionSpeedBonusRate", GameReflection.PublicInstance);
                _perksHearthSacraficeTimeBonusRateField = perksStateType.GetField("hearthSacraficeTimeBonusRate", GameReflection.PublicInstance);
                _perksBonusEmbarkGoodsAmountField = perksStateType.GetField("bonusEmbarkGoodsAmount", GameReflection.PublicInstance);
                _perksGlobalExtraProductionChanceBonusField = perksStateType.GetField("globalExtraProductionChanceBonus", GameReflection.PublicInstance);
            }
        }

        private static void CacheSettingsTypes(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsGetDifficultyMethod = settingsType.GetMethod("GetDifficulty", new[] { typeof(string) });
                _settingsGetBiomeMethod = settingsType.GetMethod("GetBiome", new[] { typeof(string) });
                _settingsGetRaceMethod = settingsType.GetMethod("GetRace", new[] { typeof(string) });
                _settingsGetEffectMethod = settingsType.GetMethod("GetEffect", new[] { typeof(string) });
                _settingsGetModifierMethod = settingsType.GetMethod("GetModifier", new[] { typeof(string) });
                _settingsGetBuildingMethod = settingsType.GetMethod("GetBuilding", new[] { typeof(string) });
                _settingsGetSeasonalEffectMethod = settingsType.GetMethod("GetSeasonalEffect", new[] { typeof(string) });
                _settingsContainsBiomeMethod = settingsType.GetMethod("ContainsBiome", new[] { typeof(string) });
                _settingsGetTextMethod = settingsType.GetMethod("GetText", new[] { typeof(string) });
            }
        }

        private static void CacheModelTypes(Assembly assembly)
        {
            // DifficultyModel
            var difficultyModelType = assembly.GetType("Eremite.Model.DifficultyModel");
            if (difficultyModelType != null)
            {
                _difficultyRawDisplayNameMethod = difficultyModelType.GetMethod("GetRawDisplayName",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // BiomeModel
            var biomeModelType = assembly.GetType("Eremite.Model.BiomeModel");
            if (biomeModelType != null)
            {
                _biomeDisplayNameField = biomeModelType.GetField("displayName", GameReflection.PublicInstance);
            }

            // RaceModel
            var raceModelType = assembly.GetType("Eremite.Model.RaceModel");
            if (raceModelType != null)
            {
                _raceDisplayNameField = raceModelType.GetField("displayName", GameReflection.PublicInstance);
            }

            // EffectModel (namespace is Eremite.Model, not Eremite.Model.Effects)
            var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
            if (effectModelType != null)
            {
                _effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
            }

            // BuildingModel
            var buildingModelType = assembly.GetType("Eremite.Model.BuildingModel");
            if (buildingModelType != null)
            {
                _buildingDisplayNameField = buildingModelType.GetField("displayName", GameReflection.PublicInstance);
            }

            // ISeasonalEffectModel - DisplayName is accessed dynamically via reflection
            // in GetSettlementSeasonalEffects since the concrete type varies
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            // RichTextService for time formatting
            var richTextServiceType = assembly.GetType("Eremite.Services.IRichTextService");
            if (richTextServiceType != null)
            {
                _richTextServiceGetMinSecTimerMethod = richTextServiceType.GetMethod("GetMinSecTimer",
                    new[] { typeof(float) });
            }

            // IServices.TextsService
            var servicesType = assembly.GetType("Eremite.Services.IServices");
            if (servicesType != null)
            {
                _servicesTextsServiceProperty = servicesType.GetProperty("TextsService",
                    GameReflection.PublicInstance);
            }

            // TextsService methods
            var textsServiceType = assembly.GetType("Eremite.Services.ITextsService");
            if (textsServiceType != null)
            {
                _textsServiceGetLocaTextMethod = textsServiceType.GetMethod("GetLocaText",
                    new[] { typeof(string) });
                _textsServiceShouldUseRomanMethod = textsServiceType.GetMethod("ShouldUseRoman",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // ========================================
        // META SERVICE ACCESS
        // ========================================

        private static object GetMetaServices()
        {
            try
            {
                var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;
                return GameReflection.McMetaServicesProperty?.GetValue(metaController);
            }
            catch { return null; }
        }

        private static object GetMetaStateService()
        {
            var metaServices = GetMetaServices();
            if (metaServices == null || _msMetaStateServiceProperty == null) return null;
            try { return _msMetaStateServiceProperty.GetValue(metaServices); }
            catch { return null; }
        }

        private static object GetWorldStateService()
        {
            var metaServices = GetMetaServices();
            if (metaServices == null || _msWorldStateServiceProperty == null) return null;
            try { return _msWorldStateServiceProperty.GetValue(metaServices); }
            catch { return null; }
        }

        /// <summary>
        /// Get localized text for a key using TextsService.
        /// </summary>
        private static string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (_textsServiceGetLocaTextMethod == null || _servicesTextsServiceProperty == null) return key;

            try
            {
                var appServices = GameReflection.GetAppServices();
                if (appServices == null) return key;

                var textsService = _servicesTextsServiceProperty.GetValue(appServices);
                if (textsService == null) return key;

                var result = _textsServiceGetLocaTextMethod.Invoke(textsService, new object[] { key }) as string;
                return !string.IsNullOrEmpty(result) ? result : key;
            }
            catch
            {
                return key;
            }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        public static bool IsGamesHistoryPopup(object popup)
        {
            if (popup == null) return false;
            EnsureTypesCached();
            if (_gamesHistoryPopupType == null) return false;
            return _gamesHistoryPopupType.IsInstanceOfType(popup);
        }

        // ========================================
        // CYCLE STATS
        // ========================================

        /// <summary>
        /// Get career statistics as label-value pairs (matches GoalsStatsPanel UI).
        /// </summary>
        public static List<(string label, string value)> GetCycleStats()
        {
            EnsureTypesCached();
            var result = new List<(string, string)>();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null) return result;

                // Get Stats (MetaStats)
                var stats = _mssStatsProperty?.GetValue(metaStateService);
                if (stats == null) return result;

                int gamesWon = _statsGamesWonField?.GetValue(stats) as int? ?? 0;
                int gamesLost = _statsGamesLostField?.GetValue(stats) as int? ?? 0;
                int gamesStarted = gamesWon + gamesLost;
                double timeSpentSeconds = _statsTimeSpentInGameField?.GetValue(stats) as double? ?? 0;

                // Completed goals count
                int completedGoals = CountCompletedGoals(metaStateService);

                // Win ratio
                float winRatio = gamesStarted > 0 ? (float)gamesWon / gamesStarted : 0f;
                string winRatioStr = $"{(int)(winRatio * 100)}%";

                // Time spent formatted as d:hh:mm:ss
                TimeSpan timeSpan = TimeSpan.FromSeconds(timeSpentSeconds);
                string timeStr = $"{(int)timeSpan.TotalDays}:{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";

                // Add stats in the same order as GoalsStatsPanel
                result.Add(("Completed Deeds", completedGoals.ToString()));
                result.Add(("Games Started", gamesStarted.ToString()));
                result.Add(("Games Won", gamesWon.ToString()));
                result.Add(("Games Lost", gamesLost.ToString()));
                result.Add(("Win Ratio", winRatioStr));
                result.Add(("Time Played", timeStr));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetCycleStats failed: {ex.Message}");
            }

            return result;
        }

        private static int CountCompletedGoals(object metaStateService)
        {
            try
            {
                var goalsState = _mssGoalsProperty?.GetValue(metaStateService);
                if (goalsState == null || _mgsGoalsField == null) return 0;

                var goalsList = _mgsGoalsField.GetValue(goalsState) as IList;
                if (goalsList == null) return 0;

                int count = 0;
                foreach (var goalState in goalsList)
                {
                    if (goalState == null) continue;
                    bool completed = _goalStateCompletedField?.GetValue(goalState) as bool? ?? false;
                    if (completed) count++;
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // UPGRADES (META PERKS)
        // ========================================

        /// <summary>
        /// Get meta perk upgrades as label-value pairs.
        /// </summary>
        public static List<(string label, string value)> GetUpgrades()
        {
            EnsureTypesCached();
            var result = new List<(string, string)>();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null || _mssPerksProperty == null) return result;

                var perks = _mssPerksProperty.GetValue(metaStateService);
                if (perks == null) return result;

                // Helper to get int field
                int GetInt(FieldInfo field) => field?.GetValue(perks) as int? ?? 0;
                float GetFloat(FieldInfo field) => field?.GetValue(perks) as float? ?? 0f;

                // Format helpers
                string FormatInt(int val) => val >= 0 ? $"+{val}" : val.ToString();
                string FormatPercent(float val) => val >= 0 ? $"+{(int)(val * 100)}%" : $"{(int)(val * 100)}%";

                // Add all 26 upgrades matching MetaPerksSummaryPanel order (using localized labels)
                result.Add((GetLocalizedText("Label_MetaReward_ReputationReward"), FormatInt(GetInt(_perksBonusReputationRewardsPicksField))));
                result.Add((GetLocalizedText("Label_MetaReward_PreparationPoint"), FormatInt(GetInt(_perksBonusPreparationPointsField))));
                result.Add((GetLocalizedText("Label_MetaReward_CornerstoneChoices"), FormatInt(GetInt(_perksBonusSeasonRewardsAmountField))));
                result.Add((GetLocalizedText("Label_Upgrade_CaravanSlot"), FormatInt(GetInt(_perksBonusCaravansField))));
                result.Add((GetLocalizedText("Label_Upgrade_TradeRoutesLimit"), FormatInt(GetInt(_perksBonusTradeRoutesLimitField))));
                result.Add((GetLocalizedText("Label_Upgrade_CitadelVision"), FormatInt(GetInt(_perksBonusCapitalVisionField))));
                result.Add((GetLocalizedText("Label_Upgrade_TownVision"), FormatInt(GetInt(_perksBonusTownsVisionField))));
                result.Add((GetLocalizedText("Label_Upgrade_EmbarkRange"), FormatInt(GetInt(_perksBonusEmbarkRangeField))));
                result.Add((GetLocalizedText("Label_MetaReward_Merch"), FormatInt(GetInt(_perksBonusTraderMerchSlotsField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_DepositsCharges"), FormatInt(GetInt(_perksRawDepositsChargesBonusField))));
                result.Add((GetLocalizedText("Label_MetaReward_BuildingStorage"), FormatInt(GetInt(_perksGlobalBuildingStorageBonusField))));
                result.Add((GetLocalizedText("Label_MetaReward_CornerstoneReroll"), FormatInt(GetInt(_perksBonusCornerstonesRerollsField))));
                result.Add((GetLocalizedText("Label_MetaReward_GracePeriod"), FormatGracePeriod(GetInt(_perksBonusGracePeriodField))));
                result.Add((GetLocalizedText("Label_MetaReward_WorkerCapacity"), FormatInt(GetInt(_perksGlobalCapacityBonusField))));
                result.Add((GetLocalizedText("Label_MetaReward_FarmRange"), FormatInt(GetInt(_perksBonusFarmAreaField))));
                result.Add((GetLocalizedText("Label_Reward_MetaGoods"), FormatPercent(GetFloat(_perksCurrencyMultiplayerField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_MerchandisePrice"), FormatPercent(GetFloat(_perksTraderMerchandisePriceBonusRatesField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_TraderSpeed"), FormatPercent(GetFloat(_perksTradersIntervalBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_ReputationPenaltyRate"), FormatPercent(GetFloat(_perksReputationPenaltyBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_GlobalSpeed"), FormatPercent(GetFloat(_perksGlobalSpeedBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_FuelBurningTime"), FormatPercent(GetFloat(_perksFuelConsumptionBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_NewcomerGoods"), FormatPercent(GetFloat(_perksNewcommersGoodsBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_GlobalProductionSpeed"), FormatPercent(GetFloat(_perksGlobalProductionSpeedBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_SacrificeCost"), FormatPercent(GetFloat(_perksHearthSacraficeTimeBonusRateField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_EmbarkGoodsAmount"), FormatPercent(GetFloat(_perksBonusEmbarkGoodsAmountField))));
                result.Add((GetLocalizedText("Label_MetaReward_Stat_GlobalExtraProductionChance"), FormatPercent(GetFloat(_perksGlobalExtraProductionChanceBonusField))));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetUpgrades failed: {ex.Message}");
            }

            return result;
        }

        private static string FormatGracePeriod(int seconds)
        {
            if (seconds == 0) return "+0:00";
            int mins = seconds / 60;
            int secs = seconds % 60;
            return $"+{mins}:{secs:D2}";
        }

        // ========================================
        // HISTORY RECORDS
        // ========================================

        /// <summary>
        /// Get all game history records.
        /// </summary>
        public static List<object> GetHistoryRecords()
        {
            EnsureTypesCached();
            var result = new List<object>();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null || _mssGamesHistoryProperty == null) return result;

                var gamesHistory = _mssGamesHistoryProperty.GetValue(metaStateService);
                if (gamesHistory == null || _ghsRecordsField == null) return result;

                var records = _ghsRecordsField.GetValue(gamesHistory) as IList;
                if (records == null) return result;

                foreach (var record in records)
                {
                    if (record != null)
                        result.Add(record);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetHistoryRecords failed: {ex.Message}");
            }

            return result;
        }

        // ========================================
        // SETTLEMENT DATA EXTRACTION
        // ========================================

        public static string GetSettlementName(object record)
        {
            if (record == null) return "Unknown";
            try
            {
                string name = _ghsNameField?.GetValue(record) as string;

                // If name is empty, return localized "Missing Town Name"
                if (string.IsNullOrEmpty(name))
                {
                    return GetLocalizedText("MenuUI_GamesHistory_MissingTownName");
                }

                bool hasStaticName = _ghsHasStaticNameField?.GetValue(record) as bool? ?? false;
                if (hasStaticName)
                {
                    // Static name means it's a localization key
                    return GetLocalizedText(name);
                }

                // Non-static name is a user-entered name
                return name;
            }
            catch { return "Unknown"; }
        }

        public static bool GetSettlementWon(object record)
        {
            if (record == null) return false;
            try { return _ghsHasWonField?.GetValue(record) as bool? ?? false; }
            catch { return false; }
        }

        public static string GetSettlementBiome(object record)
        {
            if (record == null) return "Unknown";
            try
            {
                string biomeKey = _ghsBiomeField?.GetValue(record) as string;
                if (string.IsNullOrEmpty(biomeKey)) return "Unknown";

                var settings = GameReflection.GetSettings();
                if (settings == null) return biomeKey;

                // Check if biome exists
                bool containsBiome = _settingsContainsBiomeMethod?.Invoke(settings, new object[] { biomeKey }) as bool? ?? false;
                if (!containsBiome) return "Unknown";

                var biomeModel = _settingsGetBiomeMethod?.Invoke(settings, new object[] { biomeKey });
                if (biomeModel == null) return biomeKey;

                var locaText = _biomeDisplayNameField?.GetValue(biomeModel);
                return GameReflection.GetLocaText(locaText) ?? biomeKey;
            }
            catch { return "Unknown"; }
        }

        public static string GetSettlementDifficulty(object record)
        {
            if (record == null) return "Unknown";
            try
            {
                string difficultyKey = _ghsDifficultyField?.GetValue(record) as string;
                if (string.IsNullOrEmpty(difficultyKey)) return "Unknown";

                var settings = GameReflection.GetSettings();
                if (settings == null) return difficultyKey;

                var difficultyModel = _settingsGetDifficultyMethod?.Invoke(settings, new object[] { difficultyKey });
                if (difficultyModel == null) return difficultyKey;

                // Call GetRawDisplayName() method
                if (_difficultyRawDisplayNameMethod != null)
                {
                    var name = _difficultyRawDisplayNameMethod.Invoke(difficultyModel, null) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }

                return difficultyKey;
            }
            catch { return "Unknown"; }
        }

        public static float GetSettlementGameTime(object record)
        {
            if (record == null) return 0f;
            try { return _ghsGameTimeField?.GetValue(record) as float? ?? 0f; }
            catch { return 0f; }
        }

        public static string FormatGameTime(float seconds)
        {
            int totalSeconds = (int)seconds;
            int mins = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{mins}:{secs:D2}";
        }

        public static int GetSettlementYears(object record)
        {
            if (record == null) return 0;
            try { return _ghsYearsField?.GetValue(record) as int? ?? 0; }
            catch { return 0; }
        }

        public static int GetSettlementLevel(object record)
        {
            if (record == null) return 0;
            try { return _ghsLevelField?.GetValue(record) as int? ?? 0; }
            catch { return 0; }
        }

        public static int GetSettlementUpgrades(object record)
        {
            if (record == null) return 0;
            try { return _ghsUpgradesField?.GetValue(record) as int? ?? 0; }
            catch { return 0; }
        }

        public static List<(string name, int count)> GetSettlementRaces(object record)
        {
            var result = new List<(string, int)>();
            if (record == null) return result;

            try
            {
                var racesDict = _ghsRacesField?.GetValue(record);
                if (racesDict == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                // Iterate dictionary using reflection
                var keysProperty = racesDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(racesDict) as IEnumerable;
                var indexer = racesDict.GetType().GetMethod("get_Item");

                if (keys == null || indexer == null) return result;

                foreach (var key in keys)
                {
                    string raceKey = key as string;
                    if (string.IsNullOrEmpty(raceKey)) continue;

                    int count = (int)indexer.Invoke(racesDict, new[] { key });

                    var raceModel = _settingsGetRaceMethod?.Invoke(settings, new object[] { raceKey });
                    string name = raceKey;

                    if (raceModel != null && _raceDisplayNameField != null)
                    {
                        var locaText = _raceDisplayNameField.GetValue(raceModel);
                        name = GameReflection.GetLocaText(locaText) ?? raceKey;
                    }

                    result.Add((name, count));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetSettlementRaces failed: {ex.Message}");
            }

            return result;
        }

        public static List<string> GetSettlementCornerstones(object record)
        {
            return GetEffectList(record, _ghsCornerstonesField);
        }

        public static List<string> GetSettlementModifiers(object record)
        {
            return GetEffectList(record, _ghsModifiersField);
        }

        private static List<string> GetEffectList(object record, FieldInfo field)
        {
            var result = new List<string>();
            if (record == null || field == null) return result;

            try
            {
                var list = field.GetValue(record) as IList;
                if (list == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                foreach (var item in list)
                {
                    string key = item as string;
                    if (string.IsNullOrEmpty(key)) continue;

                    var effectModel = _settingsGetEffectMethod?.Invoke(settings, new object[] { key });
                    string name = key;

                    if (effectModel != null && _effectDisplayNameProperty != null)
                    {
                        name = _effectDisplayNameProperty.GetValue(effectModel) as string ?? key;
                    }

                    result.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetEffectList failed: {ex.Message}");
            }

            return result;
        }

        public static List<string> GetSettlementBuildings(object record)
        {
            var result = new List<string>();
            if (record == null) return result;

            try
            {
                var list = _ghsBuildingsField?.GetValue(record) as IList;
                if (list == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                foreach (var item in list)
                {
                    string key = item as string;
                    if (string.IsNullOrEmpty(key)) continue;

                    var buildingModel = _settingsGetBuildingMethod?.Invoke(settings, new object[] { key });
                    string name = key;

                    if (buildingModel != null && _buildingDisplayNameField != null)
                    {
                        var locaText = _buildingDisplayNameField.GetValue(buildingModel);
                        name = GameReflection.GetLocaText(locaText) ?? key;
                    }

                    result.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetSettlementBuildings failed: {ex.Message}");
            }

            return result;
        }

        public static List<string> GetSettlementSeasonalEffects(object record)
        {
            var result = new List<string>();
            if (record == null) return result;

            try
            {
                var list = _ghsSeasonalEffectsField?.GetValue(record) as IList;
                if (list == null) return result;

                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                foreach (var item in list)
                {
                    string key = item as string;
                    if (string.IsNullOrEmpty(key)) continue;

                    var seasonalModel = _settingsGetSeasonalEffectMethod?.Invoke(settings, new object[] { key });
                    string name = key;

                    if (seasonalModel != null)
                    {
                        // Use DisplayName property for localized name (from ISeasonalEffectModel)
                        var displayNameProp = seasonalModel.GetType().GetProperty("DisplayName", GameReflection.PublicInstance);
                        if (displayNameProp != null)
                        {
                            name = displayNameProp.GetValue(seasonalModel) as string ?? key;
                        }
                    }

                    result.Add(name);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GamesHistoryReflection: GetSettlementSeasonalEffects failed: {ex.Message}");
            }

            return result;
        }
    }
}
