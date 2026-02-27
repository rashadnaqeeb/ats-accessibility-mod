# TrendsReflection.cs
Reflection helpers for TrendsPopup and storage operations data. Provides access to trend data aggregated by source (building, consumption, etc.) for the goods storage trends overlay.

## class TrendsReflection (line 11)

### Nested Structs
- public struct `AggregatedOperation` (line 16): `TotalAmount` (int), `DisplayName` (string)

### Fields
**TrendsPopup**
- private static Type `_trendsPopupType` (line 28)
- private static FieldInfo `_currentGoodField` (line 29)

**IGameServices properties**
- private static PropertyInfo `_gsStateServiceProperty` (line 32)
- private static PropertyInfo `_gsStorageOperationsServiceProperty` (line 33)

**IStateService**
- private static PropertyInfo `_stateServiceTrendsProperty` (line 36)

**TrendsState fields**
- private static FieldInfo `_trendsGoodsOperationsField` (line 39)
- private static FieldInfo `_trendsTotalTicksField` (line 40)

**StorageOperation fields**
- private static FieldInfo `_opAmountField` (line 43)
- private static FieldInfo `_opTrendTickField` (line 44)

**IStorageOperationsService**
- private static MethodInfo `_getDisplayNameMethod` (line 47)

**GoodModel for display names**
- private static MethodInfo `_getGoodMethod` (line 50)
- private static FieldInfo `_goodDisplayNameField` (line 51)
- private static bool `_cached` (line 25)

### Methods
- private static void `EnsureCached()` (line 57)
- private static object `GetStateService()` (line 121)
- private static object `GetTrendsState()` (line 127)
- private static object `GetStorageOperationsService()` (line 132)
- public static bool `IsTrendsPopup(object popup)` (line 143)
- public static string `GetCurrentGood(object popup)` (line 153)
  Returns the internal name of the currently selected good in the TrendsPopup.
- public static List<string> `GetAllGoods()` (line 166)
  Returns sorted list of all goods that have trend data.
- public static string `GetGoodDisplayName(string goodName)` (line 196)
- public static int `GetTotalTicks()` (line 221)
- public static List<AggregatedOperation> `GetAggregatedOperations(string goodName, int tickCount)` (line 234)
  Returns operations for a good within the last `tickCount` ticks (1, 6, or 30), aggregated by display name. Sorted: gains first (descending), then losses (ascending by absolute value). Skips zero-sum entries.
- private static string `GetOperationDisplayName(object opsService, object operation)` (line 309)
- public static int `LogCacheStatus()` (line 317)
