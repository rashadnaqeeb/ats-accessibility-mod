# TutorialReflection.cs
Reflection helpers for TutorialTooltip and WorldTutorialsHUD internals. Two separate cache sections handle the in-game tooltip overlay and the world tutorials list panel.

## class TutorialReflection (line 7)

### Nested Classes
- public class `TutorialInfo` (line 473): properties `Config` (object), `DisplayName`, `IsCompleted`, `IsUnlocked`, `LockedReason`

### Fields
**Tooltip cache section**
- private static Type `_tutorialTooltipType` (line ~15)
- private static PropertyInfo `_ttipsServiceProperty` (line ~16)
- private static MethodInfo `_ttipsGetTooltipMethod` (line ~17)
- private static FieldInfo `_tooltipIsVisibleField` (line ~20)
- private static FieldInfo `_tooltipHasMoreTextField` (line ~21)
- private static FieldInfo `_tooltipCurrentTextField` (line ~22)
- private static FieldInfo `_tooltipIsButtonExpectedField` (line ~23)
- private static FieldInfo `_tooltipIsButtonActiveField` (line ~24)
- private static MethodInfo `_triggerContinueMethod` (line ~25)
- private static object `_cachedTooltip` (line ~28)
- private static bool `_tooltipCached` (line ~29)

**World tutorials cache section**
- private static Type `_worldTutorialsHUDType` (line ~35)
- private static PropertyInfo `_wthudInstanceProperty` (line ~36)
- private static FieldInfo `_wthudIsVisibleField` (line ~37)
- private static MethodInfo `_wthudToggleMethod` (line ~38)
- private static MethodInfo `_getAllTutorialsMethod` (line ~41)
- private static FieldInfo `_tutorialConfigField` (line ~42)
- private static FieldInfo `_tutorialIsCompletedField` (line ~43)
- private static FieldInfo `_tutorialIsUnlockedField` (line ~44)
- private static FieldInfo `_tutorialLockedReasonField` (line ~45)
- private static FieldInfo `_tutorialDisplayNameField` (line ~46)
- private static MethodInfo `_startTutorialMethod` (line ~47)
- private static PropertyInfo `_gsWorldServicesProperty` (line ~50)
- private static PropertyInfo `_wsCurrentPhaseProperty` (line ~51)
- private static PropertyInfo `_tutorialServiceProperty` (line ~52)
- private static MethodInfo `_getTutorialRewardsMethod` (line ~53)
- private static bool `_worldTutorialsCached` (line ~56)

### Methods
**Tooltip section**
- private static void `EnsureCached()` (line 55)
- private static object `GetTooltipsService()` (line 107)
- public static object `GetTutorialTooltip()` (line 117)
  Returns the cached tooltip instance, refreshing if null.
- public static void `ClearCachedTooltip()` (line 150)
  Clears the cached tooltip reference so it will be re-fetched next call.
- public static bool `IsTooltipVisible()` (line 160)
- public static bool `HasMoreText()` (line 182)
- public static string `GetCurrentText()` (line 196)
- public static bool `IsButtonExpected()` (line 229)
  Returns true when the tutorial is waiting for the player to press a specific button.
- public static bool `IsButtonActive()` (line 249)
- public static bool `TriggerContinue()` (line 274)

**World tutorials section**
- private static void `EnsureWorldTutorialsCached()` (line 354)
- public static object `GetWorldTutorialsHUD()` (line 425)
- public static bool `IsWorldTutorialsHUDVisible()` (line 439)
- public static bool `ToggleWorldTutorialsHUD()` (line 449)
- public static List<TutorialInfo> `GetAllTutorials()` (line 484)
- private static string `GetTutorialDisplayName(object tutorialConfig)` (line 545)
- private static bool `IsTutorialCompleted(object tutorialState)` (line 554)
- private static bool `IsTutorialUnlocked(object tutorialState)` (line 578)
- private static string `GetTutorialLockedReason(object tutorialState)` (line 605)
- public static bool `StartTutorial(object tutorialConfig)` (line 614)
- public static object `GetCurrentPhase()` (line 639)
  Returns the current tutorial phase enum value.
- public static List<object> `GetTutorialRewardsForCurrentBiome()` (line 672)
  Returns effect models for tutorial completion rewards in the current biome.
- public static int `LogCacheStatus()` (line 730)
