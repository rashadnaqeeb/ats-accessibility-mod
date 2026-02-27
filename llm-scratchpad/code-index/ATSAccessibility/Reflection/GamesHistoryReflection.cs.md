# GamesHistoryReflection.cs

Reflection helpers for accessing Games History popup data.
Accesses WorldStateService for cycle stats, MetaStateService for perks and history.

## class GamesHistoryReflection (line 11)

### Fields (private static cached)

#### Detection
- private static Type _gamesHistoryPopupType (line 17)

#### MetaStateService access
- private static PropertyInfo _msMetaStateServiceProperty (line 20)
- private static PropertyInfo _mssPerksProperty (line 23)
- private static PropertyInfo _mssGamesHistoryProperty (line 26)
- private static PropertyInfo _mssStatsProperty (line 29)
- private static PropertyInfo _mssGoalsProperty (line 32)
- private static FieldInfo _mgsGoalsField (line 33)
- private static FieldInfo _goalStateCompletedField (line 34)

#### MetaStats
- private static FieldInfo _statsGamesWonField (line 37)
- private static FieldInfo _statsGamesLostField (line 38)
- private static FieldInfo _statsTimeSpentInGameField (line 39)

#### GamesHistoryState / GameHistoryState
- private static FieldInfo _ghsRecordsField (line 42)
- private static FieldInfo _ghsNameField (line 45)
- private static FieldInfo _ghsHasStaticNameField (line 46)
- private static FieldInfo _ghsHasWonField (line 47)
- private static FieldInfo _ghsDifficultyField (line 48)
- private static FieldInfo _ghsBiomeField (line 49)
- private static FieldInfo _ghsLevelField (line 50)
- private static FieldInfo _ghsUpgradesField (line 51)
- private static FieldInfo _ghsYearsField (line 52)
- private static FieldInfo _ghsGameTimeField (line 53)
- private static FieldInfo _ghsRacesField (line 54)
- private static FieldInfo _ghsCornerstonesField (line 55)
- private static FieldInfo _ghsModifiersField (line 56)
- private static FieldInfo _ghsBuildingsField (line 57)
- private static FieldInfo _ghsSeasonalEffectsField (line 58)

#### MetaPerksState fields (26 perk upgrade fields)
- private static FieldInfo _perksBonusReputationRewardsPicksField (line 61)
- private static FieldInfo _perksBonusPreparationPointsField (line 62)
- private static FieldInfo _perksBonusSeasonRewardsAmountField (line 63)
- private static FieldInfo _perksBonusCaravansField (line 64)
- private static FieldInfo _perksBonusTradeRoutesLimitField (line 65)
- private static FieldInfo _perksBonusCapitalVisionField (line 66)
- private static FieldInfo _perksBonusTownsVisionField (line 67)
- private static FieldInfo _perksBonusEmbarkRangeField (line 68)
- private static FieldInfo _perksBonusTraderMerchSlotsField (line 69)
- private static FieldInfo _perksRawDepositsChargesBonusField (line 70)
- private static FieldInfo _perksGlobalBuildingStorageBonusField (line 71)
- private static FieldInfo _perksBonusCornerstonesRerollsField (line 72)
- private static FieldInfo _perksBonusGracePeriodField (line 73)
- private static FieldInfo _perksGlobalCapacityBonusField (line 74)
- private static FieldInfo _perksBonusFarmAreaField (line 75)
- private static FieldInfo _perksCurrencyMultiplayerField (line 76)
- private static FieldInfo _perksTraderMerchandisePriceBonusRatesField (line 77)
- private static FieldInfo _perksTradersIntervalBonusRateField (line 78)
- private static FieldInfo _perksReputationPenaltyBonusRateField (line 79)
- private static FieldInfo _perksGlobalSpeedBonusRateField (line 80)
- private static FieldInfo _perksFuelConsumptionBonusRateField (line 81)
- private static FieldInfo _perksNewcommersGoodsBonusRateField (line 82)
- private static FieldInfo _perksGlobalProductionSpeedBonusRateField (line 83)
- private static FieldInfo _perksHearthSacraficeTimeBonusRateField (line 84)
- private static FieldInfo _perksBonusEmbarkGoodsAmountField (line 85)
- private static FieldInfo _perksGlobalExtraProductionChanceBonusField (line 86)

#### WorldStateService
- private static PropertyInfo _msWorldStateServiceProperty (line 89)
- private static PropertyInfo _wssCycleProperty (line 92)

#### CycleState
- private static FieldInfo _cycleGamesWonInCycleField (line 95)
- private static FieldInfo _cycleGamesPlayedInCycleField (line 96)
- private static FieldInfo _cycleSealFragmentsField (line 97)
- private static FieldInfo _cycleTotalSealFragmentsField (line 98)
- private static FieldInfo _cycleFinishedModifiersField (line 99)

#### Settings methods
- private static MethodInfo _settingsGetDifficultyMethod (line 102)
- private static MethodInfo _settingsGetBiomeMethod (line 103)
- private static MethodInfo _settingsGetRaceMethod (line 104)
- private static MethodInfo _settingsGetEffectMethod (line 105)
- private static MethodInfo _settingsGetModifierMethod (line 106)
- private static MethodInfo _settingsGetBuildingMethod (line 107)
- private static MethodInfo _settingsGetSeasonalEffectMethod (line 108)
- private static MethodInfo _settingsContainsBiomeMethod (line 109)

#### Model display name
- private static MethodInfo _difficultyRawDisplayNameMethod (line 111)
- private static FieldInfo _biomeDisplayNameField (line 112)
- private static FieldInfo _raceDisplayNameField (line 113)
- private static PropertyInfo _effectDisplayNameProperty (line 114)
- private static FieldInfo _buildingDisplayNameField (line 115)

#### RichTextService / TextsService
- private static MethodInfo _richTextServiceGetMinSecTimerMethod (line 118)
- private static PropertyInfo _servicesTextsServiceProperty (line 121)
- private static MethodInfo _textsServiceGetLocaTextMethod (line 122)
- private static MethodInfo _textsServiceShouldUseRomanMethod (line 123)
- private static bool _typesCached (line 125)

### Methods
- private static void EnsureTypesCached() (line 131)
- private static object GetMetaStateService() (line 347)
- private static object GetWorldStateService() (line 349)
- private static string GetLocalizedText(string key) (line 354)
  Via AppServices.TextsService.

#### Detection
- public static bool IsGamesHistoryPopup(object popup) (line 376)

#### Cycle stats
- public static List<(string label, string value)> GetCycleStats() (line 390)
  Returns current cycle statistics (games won, played, seal fragments, etc.).

#### Meta upgrades
- public static List<(string label, string value)> GetUpgrades() (line 459)
  Returns all 26 meta perk values with localized labels.

#### History records
- public static List<object> GetHistoryRecords() (line 526)
  Returns raw GameHistoryState objects.
- public static string GetSettlementName(object record) (line 555)
  Handles both static (localization key) and user-entered settlement names.
- public static bool GetSettlementWon(object record) (line 576)
- public static string GetSettlementBiome(object record) (line 581)
- public static string GetSettlementDifficulty(object record) (line 601)
- public static float GetSettlementGameTime(object record) (line 621)
  Returns seconds as float.
- public static string FormatGameTime(float seconds) (line 626)
  Returns `"m:ss"` formatted string.
- public static int GetSettlementYears(object record) (line 633)
- public static int GetSettlementLevel(object record) (line 638)
- public static int GetSettlementUpgrades(object record) (line 643)
- public static List<(string name, int count)> GetSettlementRaces(object record) (line 648)
- public static List<string> GetSettlementCornerstones(object record) (line 680)
- public static List<string> GetSettlementModifiers(object record) (line 684)
- public static List<string> GetSettlementBuildings(object record) (line 715)
- public static List<string> GetSettlementSeasonalEffects(object record) (line 742)
  Uses dynamic reflection on ISeasonalEffectModel for DisplayName.

#### Private helpers
- private static string FormatGracePeriod(int seconds) (line ~)
- private static int CountCompletedGoals(object metaGoalsState) (line ~)
- private static List<string> GetEffectList(object record, FieldInfo field) (line ~)

#### Cache
- public static int LogCacheStatus() (line 777)
