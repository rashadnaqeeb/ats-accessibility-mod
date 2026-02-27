# NewcomersReflection.cs
Reflection helpers for the NewcomersPopup internals. Provides methods to query newcomer group options and pick a group.

## class NewcomersReflection (line 7)

### Fields
- private static Type `_newcomersPopupType` (line ~12)
- private static FieldInfo `_groupsField` (line ~15)
- private static MethodInfo `_pickGroupMethod` (line ~16)
- private static FieldInfo `_groupRaceField` (line ~19)
- private static FieldInfo `_groupAmountField` (line ~20)
- private static FieldInfo `_groupBonusField` (line ~21)
- private static PropertyInfo `_raceDisplayNameProperty` (line ~24)
- private static bool `_cached` (line ~27)

### Methods
- private static void `EnsureCached()` (line 48)
- public static bool `IsNewcomersPopup(object popup)` (line 98)
- public static object `GetNewcomersGroups()` (line 113)
  Returns the list of newcomer group options from the current newcomers state.
- public static bool `PickGroup(object popup, object group)` (line 132)
  Invokes the popup's pick action for the chosen group.
- public static string `FormatGroup(object group)` (line 160)
  Formats a group as "N RaceName (bonus description)" for speech output.
- public static int `LogCacheStatus()` (line 210)
