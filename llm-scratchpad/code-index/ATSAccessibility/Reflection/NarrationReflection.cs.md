# NarrationReflection.cs
Reflection helpers for narration/dialogue popup internals. Provides access to NPC dialogue, topic detection, choice navigation, and event subscription for dialogue state changes.

## class NarrationReflection (line 7)

### Nested Classes
- public class `ChoiceInfo` (line 319): fields `Choice` (object), `Text` (string)

### Fields
- private static Type `_homePopupType` (line ~15)
- private static FieldInfo `_currentBranchField` (line ~16)
- private static MethodInfo `_getBranchDisplayTextMethod` (line ~17)
- private static PropertyInfo `_worldServicesProperty` (line ~22)
- private static PropertyInfo `_narrationBlackboardServiceProperty` (line ~23)
- private static PropertyInfo `_narrationServiceProperty` (line ~24)
- private static PropertyInfo `_npcNameProperty` (line ~28)
- private static PropertyInfo `_npcTitleProperty` (line ~29)
- private static MethodInfo `_hasAnyImportantTopicsMethod` (line ~32)
- private static PropertyInfo `_currentDialogueTextProperty` (line ~35)
- private static PropertyInfo `_hasTransitionProperty` (line ~36)
- private static MethodInfo `_executeTransitionMethod` (line ~37)
- private static FieldInfo `_choicesField` (line ~40)
- private static MethodInfo `_selectChoiceMethod` (line ~41)
- private static PropertyInfo `_onDialogueChangedProperty` (line ~44)
- private static PropertyInfo `_onBranchChangedProperty` (line ~45)
- private static bool `_cached` (line ~48)

### Methods
- private static void `EnsureCached()` (line 69)
- public static bool `IsHomePopup(object popup)` (line 145)
- public static string `GetCurrentDisplayedText(object popup)` (line 156)
- private static object `GetWorldServices()` (line 208)
- private static object `GetNarrationBlackboardService()` (line 222)
- private static object `GetNarrationService()` (line 231)
- public static string `GetNPCName()` (line 244)
- public static string `GetNPCTitle()` (line 256)
- public static bool `HasAnyImportantTopics()` (line 268)
- public static string `GetDialogueText(object branch)` (line 282)
- public static bool `HasTransition(object branch)` (line 297)
- public static bool `ExecuteTransition(object branch)` (line 306)
- public static List<ChoiceInfo> `GetChoices(object branch)` (line 328)
- public static bool `SelectChoice(ChoiceInfo choice)` (line 382)
- public static IDisposable `SubscribeToDialogue(Action<object> callback)` (line 395)
  Subscribes to dialogue branch change events.
- public static IDisposable `SubscribeToBranch(Action<object> callback)` (line 415)
  Subscribes to narration branch change events.
- public static int `LogCacheStatus()` (line 432)
