# DailyExpeditionReflection.cs

Provides reflection-based access to Daily Challenge popup internals.

CRITICAL RULES:
- Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
- NEVER cache instance references (services, controllers) - they are destroyed on scene change
- All public methods return fresh values by querying through cached PropertyInfo

## class DailyExpeditionReflection (line 17)

### Fields (private static cached)
- private static readonly Regex TimePattern (line 19)
  Compiled regex `\d{2}:\d{2}:\d{2}` for extracting HH:MM:SS from the UI time-left label.

#### Types
- private static Type _dailyChallengePopupType (line 22)
- private static Type _dailyChallengeDataType (line 23)
- private static Type _dailyDifficultyPickerType (line 24)
- private static Type _difficultyModelType (line 25)
- private static Type _metaCurrencyType (line 26)
- private static Type _goodStructType (line 27)

#### DailyChallengePopup UI fields
- private static FieldInfo _popupBiomeTextField (line 30)
- private static FieldInfo _popupTimeLeftField (line 31)
- private static FieldInfo _popupChallangeField (line 32)
- private static FieldInfo _popupDifficultyPickerField (line 33)
- private static FieldInfo _popupDifficultyField (line 34)
- private static FieldInfo _popupEmbarkButtonField (line 35)
- private static FieldInfo _popupCompletedMarkerField (line 36)

#### UI helpers
- private static PropertyInfo _tmpTextProperty (line 39)

#### DailyChallengeData fields
- private static FieldInfo _dataBiomeField (line 42)
- private static FieldInfo _dataInitialVillagersField (line 43)
- private static FieldInfo _dataEmbarkGoodsField (line 44)
- private static FieldInfo _dataEmbarkEffectsField (line 45)
- private static FieldInfo _dataEarlyModifiersField (line 46)
- private static FieldInfo _dataLateModifiersField (line 47)
- private static FieldInfo _dataBaseRewardsField (line 48)

#### DifficultyPicker
- private static MethodInfo _pickerGetDifficultiesMethod (line 51)
- private static MethodInfo _pickerGetPickedDifficultyMethod (line 52)
- private static MethodInfo _pickerSetDifficultyMethod (line 53)

#### DifficultyModel
- private static FieldInfo _dmPositiveEffectsField (line 56)
- private static FieldInfo _dmNegativeEffectsField (line 57)
- private static FieldInfo _dmEffectsMagnitudeField (line 58)
- private static FieldInfo _dmIndexField (line 59)
- private static MethodInfo _dmGetDisplayNameMethod (line 60)

#### MetaCurrency struct
- private static FieldInfo _mcNameField (line 63)
- private static FieldInfo _mcAmountField (line 64)

#### Good struct
- private static FieldInfo _goodNameField (line 67)
- private static FieldInfo _goodAmountField (line 68)

#### MetaCurrencyModel
- private static MethodInfo _settingsGetMetaCurrencyMethod (line 71)
- private static PropertyInfo _mcModelDisplayNameProperty (line 72)

#### DailyService
- private static PropertyInfo _msDailyServiceProperty (line 75)
- private static MethodInfo _dailyIsCompletedTodayMethod (line 76)
- private static MethodInfo _dailyGetRewardsForMethod (line 77)

#### Popup
- private static MethodInfo _popupHideMethod (line 80)
- private static bool _typesCached (line 82)

### Methods
- private static void EnsureTypes() (line 84)
- public static bool IsDailyChallengePopup(object popup) (line 222)
  Uses `GetType().Name` comparison.
- public static object FindDailyChallengePopup() (line 230)
- public static object GetChallengeData(object popup) (line 244)
- public static string GetBiomeName(object popup) (line 252)
  Reads from UI `biomeText` TMP field.
- public static string GetTimeLeft(object popup) (line 272)
  Reads from UI `timeLeft` field, extracts HH:MM:SS via TimePattern regex, falls back to time until midnight UTC.
- public static List<string> GetRaces(object popup) (line 292)
  Returns distinct race display names from initialVillagers.
- public static List<string> GetEmbarkGoods(object popup) (line 319)
  Returns `"amount displayName"` formatted strings.
- public static List<string> GetEmbarkEffects(object popup) (line 345)
- public static List<string> GetModifiers(object popup) (line 366)
  Returns display names only.
- public static List<(string name, string description)> GetModifiersDetailed(object popup) (line 379)
- public static object GetCurrentDifficulty(object popup) (line 450)
- public static string GetDifficultyDisplayName(object difficulty) (line 465)
- public static int GetDifficultyIndex(object difficulty) (line 473)
- public static List<object> GetAvailableDifficulties(object popup) (line 482)
- public static void SetDifficulty(object popup, object difficulty) (line 501)
- public static (int positive, int negative) GetSeasonalEffectsCounts(object difficulty) (line 510)
- public static float GetEffectsMagnitude(object difficulty) (line 522)
- public static bool IsCompleted(object popup) (line 535)
  Reads from `completedMarker.activeSelf`; falls back to DailyService.IsCompletedToday.
- public static List<string> GetRewards(object popup) (line 556)
  Returns `"amount displayName"` formatted strings.
- public static void TriggerEmbark(object popup) (line 610)
  Invokes embarkButton.onClick.
- public static void HidePopup(object popup) (line 629)
- private static string GetFallbackTimeLeft() (line ~)
- private static (string name, string description) GetEffectNameAndDescription(string effectName) (line ~)
- private static object GetDifficultyPicker(object popup) (line ~)
- private static List<string> FormatRewardsFromList(IList rewards) (line ~)
- public static int LogCacheStatus() (line 634)
