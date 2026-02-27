# WildcardReflection.cs
Reflection helpers for the WildcardPopup (building selection screen). Provides access to available buildings, pick state, and interaction methods.

## class WildcardReflection (line 7)

### Nested Classes
- public class `BuildingInfo` (line 18): `BuildingModel` (object), `DisplayName`, `Description`, `IsUnlocked`

### Fields
**Service property cache**
- private static PropertyInfo `_gsBiomeServiceProperty` (line ~30)
- private static PropertyInfo `_gsEffectsServiceProperty` (line ~31)
- private static PropertyInfo `_gsMetaServicesProperty` (line ~32)

**Wildcard data types**
- private static Type `_wildcardPopupType` (line ~38)
- private static FieldInfo `_wcPopupBuildingsField` (line ~39)
- private static MethodInfo `_getPicksRequiredMethod` (line ~40)
- private static FieldInfo `_wcPopupCurrentPicksField` (line ~41)
- private static MethodInfo `_toggleSlotMethod` (line ~42)
- private static MethodInfo `_confirmMethod` (line ~43)
- private static MethodInfo `_getCurrentPickCountMethod` (line ~44)

**Popup type cache**
- private static FieldInfo `_buildingModelField` (line ~50)
- private static FieldInfo `_buildingDisplayNameField` (line ~51)
- private static PropertyInfo `_buildingDescriptionProperty` (line ~52)

**Meta conditions cache**
- private static PropertyInfo `_msMetaConditionsServiceProperty` (line ~58)
- private static MethodInfo `_isMetaUnlockedMethod` (line ~59)
- private static bool `_typesCached` (line ~62)

### Methods
- private static void `EnsureTypesCached()` (line 75)
- private static void `CacheServiceProperties(Assembly)` (line ~85)
- private static void `CacheWildcardDataTypes(Assembly)` (line ~95)
- private static void `CachePopupTypes(Assembly)` (line ~105)
- private static void `CacheMetaTypes(Assembly)` (line ~115)
- private static object `GetBiomeService()` (line 169)
- private static object `GetEffectsService()` (line 174)
- private static object `GetMetaConditionsService()` (line 179)
- public static bool `IsWildcardPopup(object popup)` (line 191)
- public static List<BuildingInfo> `GetAvailableBuildings()` (line 204)
  Returns buildings available to choose from the BiomeService.
- public static int `GetPicksRequired()` (line 254)
- public static bool `IsMetaUnlocked(object buildingModel)` (line 264)
  Checks if a building has any meta-progression lock preventing selection.
- public static bool `ToggleSlot(object popup, object buildingModel)` (line 279)
  Toggles a building's selection in the wildcard popup.
- public static int `GetCurrentPickCount(object popup)` (line 314)
- public static List<object> `GetCurrentPicks(object popup)` (line 324)
- public static bool `Confirm(object popup)` (line 350)
- public static int `LogCacheStatus()` (line 361)
