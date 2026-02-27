# WorldEventReflection.cs
Reflection helpers for WorldEventPopup and WorldEventModel. Used for navigating world event decision screens on the world map.

## class WorldEventReflection (line 7)

### Fields
- private static Type `_worldEventPopupType` (line ~12)
- private static FieldInfo `_popupEventField` (line ~13)
- private static FieldInfo `_eventModelField` (line ~16)
- private static FieldInfo `_eventStateField` (line ~17)
- private static FieldInfo `_eventDisplayNameField` (line ~20)
- private static FieldInfo `_eventDescriptionField` (line ~21)
- private static FieldInfo `_eventOptionsField` (line ~24)
- private static PropertyInfo `_optionDescriptionProperty` (line ~25)
- private static MethodInfo `_canExecuteOptionMethod` (line ~28)
- private static MethodInfo `_getExecutionBlockReasonMethod` (line ~29)
- private static MethodInfo `_executeDecisionMethod` (line ~30)
- private static bool `_cached` (line ~33)

### Methods
- public static bool `IsWorldEventPopup(object popup)` (line 49)
- public static object `GetWorldEvent(object popup)` (line 63)
- public static object `GetModel(object worldEvent)` (line 72)
- public static object `GetState(object worldEvent)` (line 81)
- public static string `GetEventName(object worldEventModel)` (line 94)
- public static string `GetEventDescription(object worldEventModel)` (line 103)
- public static int `GetOptionCount(object worldEventModel)` (line 112)
- public static string `GetOptionDescription(object worldEventModel, int index)` (line 122)
- public static bool `CanExecuteOption(object worldEvent, int index)` (line 132)
- public static string `GetExecutionBlockReason(object worldEvent, int index)` (line 142)
- public static bool `ExecuteDecision(object popup, object worldEvent, int index)` (line 153)
- private static void `EnsureCached()` (line 166)
- public static int `LogCacheStatus()` (line 218)
