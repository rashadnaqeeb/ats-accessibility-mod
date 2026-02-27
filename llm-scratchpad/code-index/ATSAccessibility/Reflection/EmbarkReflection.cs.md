# EmbarkReflection.cs

Provides reflection-based access to embark screen game internals.

CRITICAL RULES:
- Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
- NEVER cache instance references (services, controllers) - they are destroyed on scene change
- All public methods return fresh values by querying through cached PropertyInfo

## class EmbarkReflection (line 17)

### Fields (private static cached)

#### MetaStateService access
- private static PropertyInfo _msMetaStateServiceProperty (line 23)
- private static PropertyInfo _mssEmbarkBonusesProperty (line 26)

#### EmbarkBonusesState
- private static FieldInfo _ebsCaravansField (line 29)
- private static FieldInfo _ebsEffectsOptionsField (line 30)
- private static FieldInfo _ebsRewardsPickedField (line 31)
- private static FieldInfo _ebsGoodsOptionsField (line 32)
- private static FieldInfo _ebsGoodsPickedField (line 33)

#### EmbarkCaravanState
- private static FieldInfo _ecsRevealedRacesField (line 36)
- private static FieldInfo _ecsRacesField (line 37)
- private static FieldInfo _ecsVillagersField (line 38)
- private static FieldInfo _ecsEmbarkGoodsField (line 39)
- private static FieldInfo _ecsBonusEmbarkGoodsField (line 40)
- private static FieldInfo _ecsEmbarkEffectsField (line 41)
- private static FieldInfo _ecsBonusEmbarkEffectsField (line 42)

#### ConditionPickState / GoodPickState
- private static FieldInfo _cpsNameField (line 45)
- private static FieldInfo _cpsCostField (line 46)
- private static FieldInfo _gpsNameField (line 49)
- private static FieldInfo _gpsAmountField (line 50)
- private static FieldInfo _gpsCostField (line 51)

#### Good struct
- private static FieldInfo _goodNameField (line 54)
- private static FieldInfo _goodAmountField (line 55)

#### WorldBlackboardService
- private static PropertyInfo _wbbOnFieldPreviewShownProperty (line 58)
- private static PropertyInfo _wbbOnFieldPreviewClosedProperty (line 59)
- private static PropertyInfo _wbbPickedCaravanProperty (line 60)

#### CaravanPickPanel
- private static Type _caravanPickPanelType (line 63)
- private static FieldInfo _cppSlotsField (line 64)
- private static FieldInfo _cppCurrentField (line 65)
- private static MethodInfo _cppPickMethod (line 66)

#### DifficultyModel
- private static Type _difficultyModelType (line 69)
- private static PropertyInfo _dmIndexProperty (line 70)
- private static MethodInfo _dmGetDisplayNameMethod (line 71)
- private static FieldInfo _dmPositiveEffectsField (line 72)
- private static FieldInfo _dmNegativeEffectsField (line 73)
- private static FieldInfo _dmRewardsMultiplierField (line 74)
- private static FieldInfo _dmPreparationPointsPenaltyField (line 75)
- private static FieldInfo _dmMinEffectCostField (line 76)
- private static FieldInfo _dmMaxEffectCostField (line 77)

#### MetaPerksService
- private static PropertyInfo _msMetaPerksServiceProperty (line 80)
- private static MethodInfo _mpsGetBasePreparationPointsMethod (line 81)

#### MetaConditionsService
- private static PropertyInfo _msMetaConditionsServiceProperty (line 84)
- private static MethodInfo _mcsGetMaxUnlockedDifficultyMethod (line 85)

#### WorldMapService
- private static MethodInfo _wmsGetMinDifficultyForMethod (line 88)

#### Settings
- private static FieldInfo _settingsDifficultiesField (line 91)

#### EmbarkDifficultyPicker
- private static Type _embarkDifficultyPickerType (line 94)
- private static MethodInfo _edpSetDifficultyMethod (line 95)
- private static MethodInfo _edpGetPickedDifficultyMethod (line 96)
- private static FieldInfo _dpDifficultyField (line 97)

#### AscensionModifierModel
- private static FieldInfo _ammShortDescField (line 100)
- private static FieldInfo _ammEffectField (line 101)
- private static FieldInfo _ammIsShownField (line 102)

#### WorldEmbarkService
- private static PropertyInfo _wsWorldEmbarkServiceProperty (line 105)
- private static MethodInfo _wesGetBonusPreparationPointsMethod (line 106)

#### Settings model lookups
- private static MethodInfo _settingsGetEffectMethod (line 109)
- private static MethodInfo _settingsGetGoodMethod (line 110)
- private static MethodInfo _settingsGetRaceMethod (line 111)
- private static bool _typesCached (line 113)

#### Instance caches (cleared on panel close — NOT safe to hold across scenes)
- private static object _cachedDifficultyPicker (line 120)
- private static int _cachedMinDifficultyPenalty (line 123)

### Methods
- private static void EnsureTypes() (line 129)
- private static void CacheMetaStateServiceTypes(Assembly gameAssembly) (line 146)
- private static void CacheEmbarkBonusesTypes(Assembly gameAssembly) (line 182)
- private static void CacheCaravanTypes(Assembly gameAssembly) (line 200)
- private static void CacheConditionPickTypes(Assembly gameAssembly) (line 230)
- private static void CacheWorldBlackboardTypes(Assembly gameAssembly) (line 254)
- private static void CacheDifficultyTypes(Assembly gameAssembly) (line 294)
- private static void CacheSettingsTypes(Assembly gameAssembly) (line 352)

#### Instance cache management
- public static void CacheInstancesOnOpen(Vector3Int fieldPos) (line 375)
  Caches EmbarkDifficultyPicker and min difficulty penalty. Call when entering embark screen.
- public static void ClearInstanceCaches() (line 392)
  Clears all instance caches. Call when leaving embark screen.

#### Service access
- public static object GetMetaStateService() (line 406)

#### Embark state
- public static object GetEmbarkBonuses() (line 449)
- public static List<object> GetCaravans() (line 458)
- public static List<object> GetEffectsAvailable() (line 468)
- public static List<object> GetEffectsPicked() (line 478)
- public static List<object> GetGoodsAvailable() (line 488)
- public static List<object> GetGoodsPicked() (line 498)

#### Caravan info
- public static int GetCaravanRevealedCount(object caravan) (line 512)
- public static List<string> GetCaravanRaces(object caravan) (line 519)
- public static List<string> GetCaravanVillagers(object caravan) (line 527)
- public static List<(string name, int amount)> GetCaravanGoods(object caravan) (line 536)
- public static List<(string name, int amount)> GetCaravanBonusGoods(object caravan) (line 552)
- public static List<string> GetCaravanEffects(object caravan) (line 568)
- public static (Dictionary<string, int> raceCounts, int unknownRaceCount) GetCaravanRaceCounts(object caravan) (line 580)
- public static string GetCaravanDisplayString(object caravan, int index) (line 620)

#### Display name lookups
- public static string GetRaceDisplayName(string raceName) (line 643)
- public static string GetConditionPickName(object conditionPick) (line 666)
- public static int GetConditionPickCost(object conditionPick) (line 673)
- public static string GetGoodPickName(object goodPick) (line 680)
- public static int GetGoodPickAmount(object goodPick) (line 687)
- public static int GetGoodPickCost(object goodPick) (line 694)
- public static string GetEffectDisplayName(string effectName) (line 701)
- public static string GetGoodDisplayName(string goodName) (line 719)

#### Caravan selection
- public static object GetPickedCaravan() (line 741)
- public static void SetPickedCaravan(object caravanState) (line 768)
  Triggers CaravanPickPanel.Pick to keep UI in sync.
- public static int GetPickedCaravanIndex() (line 873)

#### Preparation points
- public static int GetBasePreparationPoints() (line 901)
- public static int GetBonusPreparationPoints() (line 910)
- public static int GetTotalPreparationPoints() (line 921)
- public static int CalculatePointsUsed() (line 931)
- public static int CalculatePointsRemaining() (line 950)

#### Bonus toggles
- public static (bool success, bool added) ToggleEffectBonus(object effectPick) (line 962)
- public static (bool success, bool added) ToggleGoodBonus(object goodPick) (line 1001)

#### Difficulty
- public static List<object> GetAllDifficulties() (line 1115)
- public static object GetMaxUnlockedDifficulty() (line 1126)
- public static object GetMinDifficultyFor(Vector3Int fieldPos) (line 1135)
- public static List<object> GetAvailableDifficulties(Vector3Int fieldPos) (line 1144)
  Returns difficulties between min and max unlocked.
- public static object GetCurrentDifficulty() (line 1183)
- public static void SetDifficulty(object difficultyModel) (line 1192)
- public static string GetDifficultyDisplayName(object difficulty) (line 1208)
- public static int GetDifficultyIndex(object difficulty) (line 1215)
- public static List<string> GetDifficultyModifiers(object difficulty, Vector3Int? fieldPos = null) (line 1229)
- public static int GetDifficultyPreparationPenalty(object difficulty) (line 1307)
- public static float GetDifficultyRewardsMultiplier(object difficulty) (line 1314)
- public static bool IsDifficultyUnlocked(object difficulty) (line 1321)
- public static int GetDifficultySealFragments(object difficulty) (line 1336)
- public static (int positive, int negative) GetDifficultySeasonalEffects(object difficulty) (line 1346)
- public static (int min, int max) GetDifficultyEffectCostRange(object difficulty) (line 1356)
- public static string GetDifficultyEffectCostRangeLabel(object difficulty, Vector3Int fieldPos) (line 1443)
- public static List<string> GetMetaCurrenciesForDifficulty(Vector3Int fieldPos, object difficulty) (line 1501)

#### Event subscriptions
- public static IDisposable SubscribeToFieldPreviewShown(Action<object> callback) (line 1571)
- public static IDisposable SubscribeToFieldPreviewClosed(Action<object> callback) (line 1585)

#### Action
- public static void TriggerEmbark() (line 1603)

#### Cache
- public static int LogCacheStatus() (line 1666)
