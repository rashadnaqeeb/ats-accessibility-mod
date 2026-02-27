# CapitalReflection.cs

## class CapitalReflection (line 13)

### Fields (private static cached)
- private static PropertyInfo _metaControllerProp (line 16)
- private static PropertyInfo _mcMetaServicesProp (line 17)
- private static PropertyInfo _capitalServiceProp (line 18)
- private static PropertyInfo _metaStateServiceProp (line 19)
- private static PropertyInfo _narrationStateProp (line 20)
- private static PropertyInfo _narrationHandTypeProp (line 21)
- private static PropertyInfo _blackboardServiceProp (line 22)
- private static FieldInfo _capitalEnabledSubjectField (line 23)
- private static FieldInfo _capitalClosedSubjectField (line 24)
- private static FieldInfo _capitalUpgradePanelRequestedField (line 25)
- private static FieldInfo _goalsPopupRequestedField (line 26)
- private static FieldInfo _gamesHistoryPopupRequestedField (line 27)
- private static FieldInfo _genderPickPopupRequestedField (line 28)
- private static FieldInfo _homePopupRequestedField (line 29)
- private static FieldInfo _dailyChallengePopupRequestedField (line 30)
- private static FieldInfo _customGamePopupRequestedField (line 31)
- private static MethodInfo _isDailyExpeditionUnlockedMethod (line 32)
- private static MethodInfo _isTrainingExpeditionUnlockedMethod (line 33)
- private static MethodInfo _isHomeEnabledMethod (line 34)
- private static MethodInfo _areGoalsEnabledMethod (line 35)
- private static PropertyInfo _metaPerksServiceProp (line 36)
- private static MethodInfo _isMetaPerksUnlockedMethod (line 37)
- private static bool _typesCached (line 39)

### Methods
- private static void EnsureTypes() (line 41)
- public static IDisposable SubscribeToCapitalEnabled(Action<object> callback) (line 109)
- public static IDisposable SubscribeToCapitalClosed(Action<object> callback) (line 129)
- public static void OpenUpgrades() (line 149)
  Fires CapitalUpgradePanelRequested subject.
- public static void OpenDeeds() (line 161)
  Fires GoalsPopupRequested subject.
- public static void OpenHistory() (line 173)
  Fires GamesHistoryPopupRequested subject.
- public static bool IsGenderPicked() (line 185)
  Returns true if NarrationState.handType >= 0 (queen or king selected).
- public static void OpenGenderPickPopup() (line 204)
- public static void OpenHome() (line 235)
  Checks gender first; falls back to OpenGenderPickPopup() if gender not yet picked.
- public static void OpenDailyExpedition() (line 271)
  Fires DailyChallengePopupRequested subject.
- public static void OpenTrainingExpedition() (line 283)
  Fires CustomGamePopupRequested subject.
- public static bool IsDailyExpeditionUnlocked() (line 295)
- public static bool IsTrainingExpeditionUnlocked() (line 307)
- public static bool IsHomeUnlocked() (line 319)
  Note: wraps game method named `IsHomeEnbabled` (typo in game code).
- public static bool IsDeedsUnlocked() (line 331)
  Wraps AreGoalsEnabled.
- private static object GetBlackboardService() (line 343)
- private static object GetMetaPerksService() (line 353)
- public static int LogCacheStatus() (line 355)
