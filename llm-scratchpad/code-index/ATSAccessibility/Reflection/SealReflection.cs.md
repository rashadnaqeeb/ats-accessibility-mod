# SealReflection.cs
Reflection helpers for seal building data. Uses game-facing terminology in the API: Stage (internally "kit"), Offering (internally "part"), Plague. Provides access to seal progression, stage/offering data, effects, and actions.

## class SealReflection (line 7)

### Fields
**Seal panel type cache**
- private static Type `_sealPanelType` (line ~15)

**Seal (SealModel) types**
- private static FieldInfo `_sealKitsField` (line ~20)
- private static FieldInfo `_sealCurrentEffectField` (line ~21)
- private static FieldInfo `_sealNextEffectField` (line ~22)
- private static MethodInfo `_sealGetCurrentEffectMethod` (line ~23)

**Seal kit (stage) types**
- private static FieldInfo `_kitOrdersField` (line ~29)
- private static FieldInfo `_kitDialogueField` (line ~30)
- private static FieldInfo `_kitPartsField` (line ~31)
- private static FieldInfo `_kitRewardField` (line ~32)

**Seal part (offering) types**
- private static FieldInfo `_partDisplayNameField` (line ~38)
- private static FieldInfo `_partDescriptionField` (line ~39)
- private static FieldInfo `_partOrderField` (line ~40)

**Seal game state types**
- private static PropertyInfo `_sealGameStateProperty` (line ~46)
- private static FieldInfo `_sgsCompletedKitsField` (line ~47)
- private static FieldInfo `_sgsCompletedPartsField` (line ~48)
- private static MethodInfo `_sgsGetTrackedPartMethod` (line ~49)

**Calendar types**
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~55)
- private static PropertyInfo `_calendarSecondsUntilStormProperty` (line ~56)

**IGameSealService methods**
- private static PropertyInfo `_gsGameSealServiceProperty` (line ~62)
- private static MethodInfo `_completePartMethod` (line ~63)
- private static MethodInfo `_toggleTrackingMethod` (line ~64)
- private static MethodInfo `_isPartTrackedMethod` (line ~65)

**Blackboard types**
- private static PropertyInfo `_gsBlackboardServiceProperty` (line ~71)
- private static PropertyInfo `_bbGetFirstSealProperty` (line ~72)
- private static bool `_cached` (line ~75)

### Methods
- private static void `EnsureCached()` (line 80)
- private static void `CacheSealPanelType(Assembly)` (line ~90)
- private static void `CacheSealTypes(Assembly)` (line ~100)
- private static void `CacheSealKitTypes(Assembly)` (line ~110)
- private static void `CacheSealPartTypes(Assembly)` (line ~120)
- private static void `CacheSealGameStateTypes(Assembly)` (line ~130)
- private static void `CacheCalendarTypes(Assembly)` (line ~140)
- private static void `CacheGameSealServiceTypes(Assembly)` (line ~150)
- private static void `CacheBlackboardTypes(Assembly)` (line ~160)
- public static bool `IsSealPanel(object panel)` (line 216)
- public static object `GetFirstSeal()` (line 229)
  Returns the first seal model from the game blackboard.
- public static bool `IsSealCompleted(object sealState)` (line 253)
- public static object `GetFirstUncompletedStage(object sealModel)` (line 265)
  Returns the first kit/stage that has not been completed.
- public static object `GetStageModel(object sealModel, object stageState)` (line 273)
- public static bool `IsStageCompleted(object sealModel, object stageState)` (line 281)
- public static object `GetCompletedOfferingFor(object sealModel, object stageState)` (line 289)
- public static int `GetStageCompletedIndex(object stageState)` (line 297)
- public static object `GetStageOrders(object kitModel)` (line 307)
- public static object `GetAllStages(object sealModel)` (line 314)
- public static string `GetStageDialogue(object kitModel)` (line 326)
- public static object `GetStageOfferings(object kitModel)` (line 333)
- public static object `GetStageReward(object kitModel)` (line 340)
- public static string `GetOfferingDisplayName(object partModel)` (line 351)
- public static string `GetOfferingDescription(object partModel)` (line 358)
- public static object `GetOfferingOrder(object partModel)` (line 365)
- private static object `GetSealGameState()` (line 376)
- public static object `GetCurrentEffect(object sealModel)` (line 385)
- public static object `GetNextEffect(object sealModel)` (line 393)
- public static bool `IsEffectActive(object sealModel)` (line 399)
- public static float `GetSecondsUntilStorm()` (line 407)
- public static bool `CompleteOffering(object sealModel, object offeringModel, int offeringIndex)` (line 435)
- public static bool `ToggleOfferingTracking(object offeringModel)` (line 453)
- public static bool `IsOfferingTracked(object offeringModel)` (line 483)
- public static (int completed, int total) `GetProgress(object sealModel)` (line 494)
- public static int `LogCacheStatus()` (line 530)
