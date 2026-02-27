# WorldMapReflection.cs
Reflection helpers for world map game internals. Extracted from GameReflection for maintainability. Has two cache sections: world map types (services, field/biome/effect models) and meta state types (level, economy, capital upgrades, cycle state, seals).

## class WorldMapReflection (line 7)

### Fields
**World map service cache**
- private static PropertyInfo `_wcInstanceProperty` (line ~20)  WorldController.Instance (static)
- private static PropertyInfo `_wcWorldServicesProperty` (line ~21)
- private static PropertyInfo `_wcCameraControllerProperty` (line ~22)
- private static PropertyInfo `_wsWorldMapServiceProperty` (line ~25)
- private static PropertyInfo `_wsWorldBlackboardServiceProperty` (line ~26)
- private static PropertyInfo `_wsWorldCalendarServiceProperty` (line ~27)
- private static PropertyInfo `_wsWorldSealsServiceProperty` (line ~28)
- private static MethodInfo `_wcsIsStormAboutToComeMethod` (line ~31)
- private static MethodInfo `_wcsHasPlayedFinalGameMethod` (line ~32)
- private static MethodInfo `_wsealsCanAffordSealMethod` (line ~35)
- private static MethodInfo `_wmsGetFieldMethod` (line ~38)
- private static MethodInfo `_wmsIsRevealedMethod` (line ~39)
- private static MethodInfo `_wmsCanBePickedMethod` (line ~40)
- private static MethodInfo `_wmsInBoundsMethod` (line ~41)
- private static MethodInfo `_wmsIsCapitalMethod` (line ~42)
- private static MethodInfo `_wmsIsCityMethod` (line ~43)
- private static MethodInfo `_wmsGetDistanceToStartTownMethod` (line ~44)
- private static MethodInfo `_wmsFindLastTownMethod` (line ~45)
- private static PropertyInfo `_wmsFieldsMapProperty` (line ~46)
- private static PropertyInfo `_msWorldStateServiceProperty` (line ~49)
- private static MethodInfo `_wssHasModifierMethod` (line ~52)
- private static MethodInfo `_wssHasEventMethod` (line ~53)
- private static MethodInfo `_wssHasSealMethod` (line ~54)
- private static MethodInfo `_wssGetModifierModelMethod` (line ~55)
- private static bool `_wssGetModifierModelMethodLookedUp` (line ~56)
- private static MethodInfo `_wssGetEventModelMethod` (line ~57)
- private static MethodInfo `_wssGetSealModelMethod` (line ~58)
- private static MethodInfo `_wssGetDisplayNameForMethod` (line ~59)
- private static PropertyInfo `_wssFieldsProperty` (line ~60)
- private static MethodInfo `_wssGetNearbySealMethod` (line ~61)
- private static PropertyInfo `_wssCycleProperty` (line ~62)
- private static PropertyInfo `_wbbOnFieldClickedProperty` (line ~65)
- private static PropertyInfo `_worldFieldBiomeProperty` (line ~68)
- private static PropertyInfo `_worldFieldTransformProperty` (line ~69)
- private static FieldInfo `_biomeDisplayNameField` (line ~72)
- private static FieldInfo `_biomeDescriptionField` (line ~73)
- private static FieldInfo `_biomeEffectsField` (line ~74)
- private static FieldInfo `_biomeWantedGoodsField` (line ~75)
- private static MethodInfo `_biomeGetDepositsGoodsMethod` (line ~76)
- private static MethodInfo `_biomeGetTreesGoodsMethod` (line ~77)
- private static PropertyInfo `_effectDisplayNameProperty` (line ~80)
- private static PropertyInfo `_effectDescriptionProperty` (line ~81)
- private static PropertyInfo `_effectIsPositiveProperty` (line ~82)
- private static FieldInfo `_goodDisplayNameField` (line ~85)
- private static FieldInfo `_wccTargetField` (line ~88)  lazily cached per WorldCameraController type
- private static bool `_worldMapTypesCached` (line ~91)

**Meta state cache**
- private static PropertyInfo `_msMetaStateServiceProperty` (line ~97)
- private static PropertyInfo `_mssEconomyProperty` (line ~98)
- private static PropertyInfo `_mssLevelProperty` (line ~99)
- private static PropertyInfo `_mssCapitalProperty` (line ~100)
- private static PropertyInfo `_mssStateProperty` (line ~101)
- private static FieldInfo `_economyCurrentCycleExpField` (line ~104)
- private static FieldInfo `_economyMetaCurrenciesField` (line ~105)
- private static FieldInfo `_levelLevelField` (line ~108)
- private static FieldInfo `_levelExpField` (line ~109)
- private static FieldInfo `_levelTargetExpField` (line ~110)
- private static FieldInfo `_capitalCurrentCycleUpgradesField` (line ~113)
- private static FieldInfo `_stateIsIronmanField` (line ~116)
- private static MethodInfo `_settingsGetCapitalUpgradeMethod` (line ~119)
- private static MethodInfo `_settingsGetMetaCurrencyMethod` (line ~120)
- private static FieldInfo `_upgradeDisplayNameField` (line ~123)
- private static FieldInfo `_upgradeIronmanDisplayNameField` (line ~124)
- private static PropertyInfo `_wbbOnCycleEndPhaseProperty` (line ~127)
- private static object `_animationRequestedValue` (line ~130)
- private static FieldInfo `_cycleYearField` (line ~133)
- private static FieldInfo `_cycleYearsInCycleField` (line ~134)
- private static FieldInfo `_cycleGamesPlayedField` (line ~135)
- private static FieldInfo `_cycleGamesWonField` (line ~136)
- private static FieldInfo `_cycleSealFragmentsField` (line ~137)
- private static PropertyInfo `_wssSealsProperty` (line ~140)
- private static MethodInfo `_sealsWasAnyCompletedMethod` (line ~143)
- private static MethodInfo `_sealsGetHighestWonMethod` (line ~144)
- private static bool `_metaStateCached` (line ~147)

### Properties
- public static PropertyInfo `CycleProperty` (line 1885)
  Exposes `_wssCycleProperty` after ensuring world map types are cached.
- public static PropertyInfo `WorldSealsServiceProperty` (line 1926)
- public static MethodInfo `SealsWasAnyCompleted` (line 1936)
- public static MethodInfo `SealsGetHighestWon` (line 1946)

### Methods
**Initialization**
- private static void `EnsureWorldMapTypes()` (line 158)
- private static void `EnsureMetaStateTypes()` (line 328)

**World controller / services access**
- public static bool `IsWorldMapActive()` (line 471)
- public static object `GetWorldController()` (line 487)
- public static object `GetWorldServices()` (line 502)
- public static object `GetWorldMapService()` (line 516)
- public static object `GetWorldStateService()` (line 531)
- public static IEnumerable<Vector3Int> `GetWorldMapPositions()` (line 550)
  Iterates WorldStateService.Fields keys.
- public static object `GetWorldBlackboardService()` (line 570)
- public static object `GetWorldCalendarService()` (line 585)
- public static object `GetWorldSealsService()` (line 600)
- public static bool `IsStormAboutToCome()` (line 615)
- public static bool `HasPlayedFinalGame()` (line 623)

**World map field queries**
- public static (int current, int required) `GetSealFragmentStatus(Vector3Int cubicPos)` (line 632)
- public static (Vector3Int position, string name) `GetLastTownInfo()` (line 669)
- public static int `GetEmbarkRange(Vector3Int from)` (line 700)
  Returns raw range - 1 (effective max distance) due to game's strict less-than path check.
- public static bool `WorldMapInBounds(Vector3Int cubicPos)` (line 716)
- public static bool `WorldMapIsRevealed(Vector3Int cubicPos)` (line 724)
- public static bool `WorldMapIsCapital(Vector3Int cubicPos)` (line 732)
- public static bool `WorldMapIsCity(Vector3Int cubicPos)` (line 740)
- public static bool `WorldMapCanBePicked(Vector3Int cubicPos)` (line 748)
- public static bool `WorldMapHasModifier(Vector3Int cubicPos)` (line 756)
- public static bool `WorldMapHasEvent(Vector3Int cubicPos)` (line 764)
- public static bool `WorldMapHasSeal(Vector3Int cubicPos)` (line 772)
- public static int `WorldMapGetDistanceToCapital(Vector3Int cubicPos)` (line 780)
- public static string `WorldMapGetBiomeName(Vector3Int cubicPos)` (line 795)
- public static string `WorldMapGetCityName(Vector3Int cubicPos)` (line 821)
- public static string `WorldMapGetModifierName(Vector3Int cubicPos)` (line 836)
  Falls back to concrete type lookup if interface method is null.
- public static string `WorldMapGetEventName(Vector3Int cubicPos)` (line 870)
- public static string `WorldMapGetSealName(Vector3Int cubicPos)` (line 892)
- public static void `WorldMapTriggerFieldClick(Vector3Int cubicPos)` (line 915)
  Fires the WorldBlackboardService.OnFieldClicked Subject to open the embark screen.
- public static void `SetWorldCameraPosition(Vector3Int cubicPos)` (line 938)
- public static void `SetWorldCameraTarget(Vector3Int cubicPos)` (line 968)
  Sets WorldCameraController.target to the field's Transform for smooth camera following.

**Coordinate conversion**
- public static Vector3 `CubicToWorld(Vector3Int cubic)` (line 1004)
  Converts cubic hex coordinates to Unity world position (hex size = 0.62f).

**Tooltip data methods**
- public static string `WorldMapGetMinDifficultyName(Vector3Int cubicPos)` (line 1026)
- public static int `WorldMapGetDifficultyPreparationPenalty(Vector3Int cubicPos)` (line 1054)
- public static int `WorldMapGetSealFragmentsForWin(Vector3Int cubicPos)` (line 1082)
- private static List<(string name, string description, bool isPositive)> `GetFieldEffectsInternal(Vector3Int cubicPos)` (line 1112)
  Returns biome effects + modifier effects, sorted with negative first.
- public static string[] `WorldMapGetFieldEffects(Vector3Int cubicPos)` (line 1205)
- public static string[] `WorldMapGetMetaCurrencies(Vector3Int cubicPos)` (line 1223)
  Returns "N DisplayName" strings for meta currency rewards at this position.
- public static (string sealName, string difficultyName, int minFragments, int rewardsPercent, int bonusYears, bool isCompleted) `WorldMapGetSealInfo(Vector3Int cubicPos)` (line 1300)
- public static (string effectName, string labelName, string description, bool isPositive) `WorldMapGetModifierInfo(Vector3Int cubicPos)` (line 1388)
- public static bool `WorldMapCanReachEvent(Vector3Int cubicPos)` (line 1470)
- public static bool `WorldMapHasAnyPathTo(Vector3Int cubicPos)` (line 1490)
- public static string[] `WorldMapGetWantedGoods(Vector3Int cubicPos)` (line 1515)
  Only returns goods if trade routes are enabled (MetaPerksService.AreTradeRoutesEnabled).
- public static string `WorldMapGetBiomeDescription(Vector3Int cubicPos)` (line 1575)
- public static List<string> `WorldMapGetBiomeDepositsGoods(Vector3Int cubicPos)` (line 1601)
- public static List<string> `WorldMapGetBiomeTreesGoods(Vector3Int cubicPos)` (line 1640)
- public static List<(string name, string description)> `WorldMapGetFieldEffectsWithDescriptions(Vector3Int cubicPos)` (line 1680)

**Meta state API**
- public static object `GetMetaStateService()` (line 1701)
- public static (int level, int exp, int targetExp) `GetLevelInfo()` (line 1718)
- public static int `GetCurrentCycleExp()` (line 1742)
- public static IEnumerable `GetCurrentCycleUnlockedUpgrades()` (line 1764)
- public static string `GetCapitalUpgradeDisplayName(object settings, string upgradeId, bool isIronman)` (line 1784)
  Uses ironman display name if available and isIronman=true.
- public static bool `IsIronmanMode()` (line 1816)
- public static System.Collections.IDictionary `GetMetaCurrencies()` (line 1838)
- public static string `GetMetaCurrencyDisplayName(string currencyName)` (line 1857)

**Cycle state API**
- public static (int year, int yearsInCycle, int gamesWon, int gamesPlayed, int sealFragments) `GetCycleInfo()` (line 1896)

**Cycle end API**
- public static bool `TriggerCycleEndAnimation()` (line 1962)
  Fires WorldBlackboardService.OnCycleEndPhase.OnNext(CycleEndPhase.AnimationRequested).
- public static int `LogCacheStatus()` (line 1985)
