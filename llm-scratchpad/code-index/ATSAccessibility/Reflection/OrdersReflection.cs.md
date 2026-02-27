# OrdersReflection.cs
Reflection helpers for the orders system. Covers the OrdersPopup, OrderPickPopup, order state queries, objective text generation, reward text generation, and order actions (complete, pick, toggle tracking).

## class OrdersReflection (line 10)

### Fields
**Static regex utilities**
- private static Regex `RichTextRegex` (line 21)
- private static Regex `ProductionBonusRegex` (line 22)
- private static Regex `ForTimeSecondsRegex` (line 25)

**Popup type cache**
- private static Type `_ordersPopupType` (line ~35)
- private static Type `_orderPickPopupType` (line ~36)
- private static MethodInfo `_hidePopupMethod` (line ~37)
- private static FieldInfo `_orderPickPopupOrderField` (line ~38)

**Service cache**
- private static PropertyInfo `_gsOrdersServiceProperty` (line ~45)
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~46)
- private static MethodInfo `_getOrdersMethod` (line ~47)
- private static MethodInfo `_getOrderModelMethod` (line ~48)
- private static MethodInfo `_getOrderPicksMethod` (line ~49)
- private static MethodInfo `_completeOrderMethod` (line ~50)
- private static MethodInfo `_pickOrderMethod` (line ~51)
- private static MethodInfo `_toggleTrackingMethod` (line ~52)
- private static MethodInfo `_fireOrderPickPopupMethod` (line ~53)
- private static MethodInfo `_getGameTimeMethod` (line ~54)

**Order state cache**
- private static FieldInfo `_isStartedField` (line ~60)
- private static FieldInfo `_isPickedField` (line ~61)
- private static FieldInfo `_isCompletedField` (line ~62)
- private static FieldInfo `_isFailedField` (line ~63)
- private static FieldInfo `_isTrackedField` (line ~64)
- private static FieldInfo `_timeLeftField` (line ~65)
- private static FieldInfo `_startTimeField` (line ~66)
- private static FieldInfo `_canBeFailedField` (line ~67)
- private static FieldInfo `_shouldBeFailableField` (line ~68)
- private static FieldInfo `_timeToFailField` (line ~69)

**Order model cache**
- private static FieldInfo `_orderDisplayNameField` (line ~75)
- private static FieldInfo `_orderObjectivesField` (line ~76)
- private static FieldInfo `_orderRewardField` (line ~77)
- private static FieldInfo `_orderHasUnlockAfterField` (line ~78)
- private static FieldInfo `_orderUnlockAfterField` (line ~79)

**Order pick state cache**
- private static FieldInfo `_pickSetIndexField` (line ~85)
- private static FieldInfo `_pickOptionsField` (line ~86)
- private static FieldInfo `_pickIsFailedField` (line ~87)

**Effect model cache**
- private static PropertyInfo `_effectDisplayNameProperty` (line ~93)
- private static PropertyInfo `_effectDescriptionProperty` (line ~94)

**Settings / blackboard cache**
- private static MethodInfo `_getEffectMethod` (line ~100)
- private static PropertyInfo `_blackboardOrderPickPopupRequestedProperty` (line ~105)
- private static bool `_cached` (line ~108)

### Methods
**Static text utilities**
- public static string `StripRichText(string text)` (line 27)
- public static string `TrimObjectiveText(string text)` (line 35)
  Strips rich text and normalises whitespace for screen reader output.
- public static string `ParseTimerSeconds(string text)` (line 44)
  Extracts seconds value from "for X seconds" pattern in objective text.
- public static string `Pluralize(string noun)` (line 59)
  Simple English pluraliser for unit names.

**Initialization**
- private static void `EnsureCached()` (line ~115)
- private static void `CachePopupTypes(Assembly)` (line ~125)
- private static void `CacheServiceTypes(Assembly)` (line ~135)
- private static void `CacheOrderStateTypes(Assembly)` (line ~145)
- private static void `CacheOrderPickStateTypes(Assembly)` (line ~155)
- private static void `CacheOrderModelTypes(Assembly)` (line ~165)
- private static void `CacheOrderLogicTypes(Assembly)` (line ~175)
- private static void `CacheEffectModelTypes(Assembly)` (line ~185)
- private static void `CacheSettingsTypes(Assembly)` (line ~195)
- private static void `CacheBlackboardTypes(Assembly)` (line ~205)

**Popup detection**
- public static bool `IsOrdersPopup(object popup)` (line 359)
- public static bool `IsOrderPickPopup(object popup)` (line 365)

**Service access**
- private static object `GetOrdersService()` (line ~375)
- private static object `GetCalendarService()` (line ~380)

**Data access**
- public static object `GetOrders()` (line 386)
  Returns the current list of order state objects.
- public static object `GetOrderModel(object orderState)` (line 395)
- public static string `GetOrderDisplayName(object orderModel)` (line 411)
- public static bool `IsStarted(object orderState)` (line 420)
- public static bool `IsPicked(object orderState)` (line 424)
- public static bool `IsCompleted(object orderState)` (line 428)
- public static bool `IsFailed(object orderState)` (line 432)
- public static bool `IsTracked(object orderState)` (line 436)
- public static float `GetTimeLeft(object orderState)` (line 440)
- public static float `GetStartTime(object orderState)` (line 444)
- public static bool `CanBeFailed(object orderState)` (line 448)
- public static bool `IsShouldBeFailable(object orderModel)` (line 452)
- public static float `GetTimeToFail(object orderModel)` (line 456)
- public static bool `HasUnlockAfter(object orderModel)` (line 460)
- public static string `GetUnlockAfterName(object orderModel)` (line 464)
- public static float `GetGameTime()` (line 454)
- public static List<string> `GetObjectiveTexts(object orderState, object orderModel)` (line 469)
  Generates human-readable objective strings, resolving effect names and timer values.
- private static string `ReplaceAmount(string text, string singular, string plural)` (line 621)
- private static string `GetReputationSourceText(object rewardSource)` (line 640)
- public static List<string> `GetPickObjectiveTexts(object orderState, int pickIndex)` (line 664)
- public static List<(string good, int stored, int required)> `GetPickStoredAmounts(object orderState, int pickIndex)` (line 753)
- public static List<string> `GetPickWarningTexts(object orderState, int pickIndex)` (line 788)
- public static List<string> `GetRewardTexts(object orderModel)` (line 823)
- public static List<string> `GetPickRewardTexts(object orderState)` (line 831)
- public static string `GetReputationRewardText(object rewardData)` (line 840)
- private static object `GetRewardsList(object rewardData)` (line ~855)
- private static List<string> `ResolveEffectNames(object effectsList)` (line ~870)
- private static string `GetEffectDisplayText(string effectName)` (line ~885)

**Actions**
- public static bool `CanComplete(object orderState, object orderModel)` (line 912)
- public static bool `CompleteOrder(object orderState, object orderModel)` (line 922)
- public static bool `PickOrder(object orderState, object orderModel)` (line 932)
- public static bool `ToggleTracking(object orderState)` (line 942)
- public static bool `FireOrderPickPopupRequested(object orderState)` (line 953)
  Fires the blackboard event to open the order pick popup.

**Pick popup**
- public static object `GetPopupOrder(object popup)` (line 973)
  Returns the order state associated with an OrderPickPopup instance.
- public static bool `HidePopup(object popup)` (line 981)
- public static object `GetPicksFor(object orderState)` (line 993)
- public static object `GetPickModel(object pickState)` (line 1004)
- public static int `GetPickSetIndex(object pickState)` (line 1008)
- public static bool `IsPickFailed(object pickState)` (line 1012)
- public static object `GetPickOrderModel(object pickState)` (line 1013)
- public static int `LogCacheStatus()` (line 1024)
