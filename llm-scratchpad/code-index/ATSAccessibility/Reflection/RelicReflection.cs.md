# RelicReflection.cs

Reflection-based access to Relic building internals (glade events).
Follows the same caching pattern as BuildingReflection.cs.
`BuildingReflection.IsRelic()` is kept there for routing use by BuildingPanelHandler.

## class RelicReflection (line 12)

### Structs

#### RelicEffectInfo (line 203)
- public string Name
- public string Description
- public bool IsPositive

#### RelicRewardInfo (line 209)
- public string Name
- public string Description

### Fields (private static cached)

#### Relic base (state, model, methods)
- private static FieldInfo _relicStateField (line 18)  — Relic.state
- private static FieldInfo _relicModelField (line 19)  — Relic.model
- private static FieldInfo _relicStateInvestigationStartedField (line 20)
- private static FieldInfo _relicStateInvestigationFinishedField (line 21)
- private static FieldInfo _relicStateWorkProgressField (line 22)
- private static FieldInfo _relicStateRelicGoodsField (line 23)
- private static FieldInfo _relicStateRewardsField (line 24)
- private static FieldInfo _relicStateWorkersField (line 25)
- private static MethodInfo _relicGetExpectedWorkingTimeLeftMethod (line 26)
- private static PropertyInfo _relicDifficultyProperty (line 27)  — Relic.Difficulty

#### Relic action methods
- private static MethodInfo _relicStartInvestigationMethod (line 30)
- private static MethodInfo _relicCancelMethod (line 31)
- private static MethodInfo _relicCanCancelMethod (line 32)

(Additional private fields for decisions, goods sets, effects, rewards, and services are cached inline within EnsureRelicBaseFields and related Ensure methods.)

### Methods (private)

- private static void EnsureRelicBaseFields() — caches base Relic type metadata
- private static object GetRelicDecisionObject(object building, int decisionIndex) (line 1191)
- private static object GetRelicGoodsSetObject(object building, int decisionIndex, int setIndex) (line 1201)
- private static object GetRelicGoodRefObject(object building, int decisionIndex, int setIndex, int goodIndex) (line 1214)

### Methods (public)

#### Status
- public static string GetRelicDangerLevel(object building) (line 222)  — "None", "Negative", "Dangerous", or "Forbidden"
- public static bool IsRelicInvestigationStarted(object building) (line 241)
- public static bool IsRelicInvestigationFinished(object building) (line 259)
- public static float GetRelicProgress(object building) (line 277)
- public static float GetRelicTimeLeft(object building) (line 295)
- public static int[] GetRelicWorkerIds(object building) (line 310)

#### Decisions / goods selection
- public static bool RelicHasMultipleDecisions(object building) (line 332)
- public static int GetRelicDecisionCount(object building) (line 348)
- public static int GetRelicDecisionIndex(object building) (line 366)
- public static bool SetRelicDecisionIndex(object building, int index) (line 382)
- public static string GetRelicDecisionLabel(object building, int decisionIndex) (line 399)
- public static float GetRelicDecisionWorkingTime(object building, int decisionIndex) (line 442)
- public static int GetRelicGoodsSetCount(object building, int decisionIndex) (line 465)
- public static int GetRelicGoodsAlternativeCount(object building, int decisionIndex, int setIndex) (line 486)
- public static string GetRelicGoodDisplayName(object building, int decisionIndex, int setIndex, int goodIndex) (line 504)
- public static string GetRelicGoodName(object building, int decisionIndex, int setIndex, int goodIndex) (line 521)  — internal name
- public static int GetRelicGoodAmount(object building, int decisionIndex, int setIndex, int goodIndex) (line 538)
- public static int GetRelicPickedGoodIndex(object building, int decisionIndex, int setIndex) (line 555)
- public static bool SetRelicPickedGoodIndex(object building, int decisionIndex, int setIndex, int goodIndex) (line 582)

#### Effects / rewards
- public static bool RelicAreEffectsPermanent(object building) (line 644)
- public static bool RelicHasDynamicRewards(object building) (line 660)
- public static bool RelicHasDecisionRewards(object building) (line 719)
- public static int GetRelicDeliveredAmount(object building, string goodName) (line 737)

#### Start / cancel
- public static bool RelicHasAnyWorkplace(object building) (line 760)
- public static bool RelicCanStart(object building, out string blockingReason) (line 775)
- public static bool RelicStartInvestigation(object building) (line 838)
- public static bool RelicCanCancel(object building) (line 854)
- public static bool RelicCancelInvestigation(object building) (line 869)
- public static object GetRelicInvestigationStartSoundModel(object building) (line 886)

#### Working effects / tiers
- public static bool RelicHasWorkingEffects(object building) (line 908)
- public static bool RelicHasDynamicEffects(object building) (line 923)
- public static int GetRelicCurrentEffectTier(object building) (line 939)
- public static int GetRelicEffectTierCount(object building) (line 955)
- public static bool RelicIsLastEffectTierReached(object building) (line 972)
- public static float GetRelicTimeToNextEffectTier(object building) (line 993)

#### Reward storage
- public static int GetRelicSafeDecisionIndex(object building) (line 1098)  — decision index that won't harm
- public static List<(string goodName, string displayName, int amount)> GetRelicRewardStorageItems(object building) (line 1122)
- public static int GetRelicRewardStorageFullSum(object building) (line 1167)
