# PaymentsReflection.cs
Reflection helpers for the payments/obligations popup. Provides access to current payment state, payment actions, auto-payment configuration, and effect descriptions.

## class PaymentsReflection (line 7)

### Nested Struct
- public struct `PaymentInfo` (line 20): `State` (object), `DisplayName`, `Amount`, `GoodName`, `SeasonName`, `IsOverdue`, `AutoPaymentType`, `CanPay`, `EffectName`

### Fields
- private static string[] `_seasonNames` (line ~42): `["Drizzle", "Clearance", "Storm"]`
- private static string[] `_autoPaymentLabels` (line ~43): labels for auto-payment modes

**Popup cache**
- private static Type `_paymentsPopupType` (line ~48)

**Service cache**
- private static PropertyInfo `_gsPaymentsServiceProperty` (line ~52)
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~53)
- private static PropertyInfo `_gsStateServiceProperty` (line ~54)
- private static PropertyInfo `_gsGameModelServiceProperty` (line ~55)
- private static MethodInfo `_getPaymentsMethod` (line ~58)
- private static MethodInfo `_payMethod` (line ~59)
- private static MethodInfo `_canPayMethod` (line ~60)
- private static MethodInfo `_setAutoPaymentTypeMethod` (line ~61)

**Payment state cache**
- private static FieldInfo `_paymentGoodField` (line ~66)
- private static FieldInfo `_paymentIsOverdueField` (line ~67)
- private static FieldInfo `_paymentAutoPaymentTypeField` (line ~68)
- private static FieldInfo `_paymentEffectField` (line ~69)
- private static FieldInfo `_paymentSeasonField` (line ~70)

**Good/display name cache**
- private static FieldInfo `_goodNameField` (line ~75)
- private static FieldInfo `_goodAmountField` (line ~76)
- private static FieldInfo `_goodModelDisplayNameField` (line ~77)
- private static MethodInfo `_getGoodMethod` (line ~78)

**Calendar/date cache**
- private static PropertyInfo `_calendarSeasonProperty` (line ~83)

**Payment model cache**
- private static FieldInfo `_paymentModelDisplayNameField` (line ~88)

**State/effect cache**
- private static FieldInfo `_effectNameField` (line ~93)
- private static PropertyInfo `_effectDisplayNameProperty` (line ~94)
- private static MethodInfo `_getEffectMethod` (line ~95)
- private static bool `_cached` (line ~98)

### Methods
- private static void `EnsureCached()` (line 101)
- private static void `CachePopupTypes(Assembly)` (line ~110)
- private static void `CacheServiceTypes(Assembly)` (line ~120)
- private static void `CachePaymentStateTypes(Assembly)` (line ~130)
- private static void `CacheGoodTypes(Assembly)` (line ~140)
- private static void `CacheGameDateTypes(Assembly)` (line ~150)
- private static void `CachePaymentModelTypes(Assembly)` (line ~160)
- private static void `CacheStateTypes(Assembly)` (line ~170)
- private static void `CacheEffectTypes(Assembly)` (line ~180)
- private static object `GetPaymentsService()` (line 222)
- private static object `GetCalendarService()` (line 224)
- private static object `GetStateService()` (line 226)
- private static object `GetGameModelService()` (line 228)
- public static bool `IsPaymentsPopup(object popup)` (line 237)
- public static List<PaymentInfo> `GetPayments()` (line 247)
  Returns all current payment obligations with full display data.
- public static bool `Pay(object paymentState)` (line 339)
- public static bool `CanPay(object paymentState)` (line 352)
- public static bool `SetAutoPaymentType(object paymentState, int type)` (line 365)
  Sets auto-payment mode: 0 = off, 1 = pay before storm, 2 = always pay.
- public static string `GetAutoPaymentLabel(int type)` (line 386)
- public static string `GetEffectDescription(string effectName)` (line 399)
- public static int `LogCacheStatus()` (line 411)
