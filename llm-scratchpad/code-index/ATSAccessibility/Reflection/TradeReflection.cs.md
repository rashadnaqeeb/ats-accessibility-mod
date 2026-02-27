# TradeReflection.cs
Reflection helpers for the trade panel (TraderPanel). Provides trader state, goods trading, perk buying, force arrival, assault, and trader info. Supports both main traders and glade event traders (isExtra visits).

## class TradeReflection (line 7)

### Nested Structs
- public struct `TradingGoodInfo` (line 17): `Name`, `DisplayName`, `StorageAmount`, `OfferedAmount`, `UnitValue`
- public struct `PerkInfo` (line 25): `Name`, `DisplayName`, `Description`, `Price`, `Discounted`, `DiscountRatio`, `Sold`, `EffectState`
- public struct `AssaultResult` (line 36): `Success`, `GoodsStolen`, `PerksStolen`, `VillagersLost`

### Fields
**Service property cache**
- private static PropertyInfo `_gsTradeServiceProperty` (line ~50)
- private static PropertyInfo `_gsStorageServiceProperty` (line ~51)
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~52)
- private static PropertyInfo `_gsEffectsServiceProperty` (line ~53)
- private static PropertyInfo `_gsGameBlackboardServiceProperty` (line ~54)

**ITradeService methods**
- private static MethodInfo `_isMainTraderInTheVillageMethod` (line ~60)
- private static MethodInfo `_getCurrentMainTraderMethod` (line ~61)
- private static MethodInfo `_getCurrentMainVisitMethod` (line ~62)
- private static MethodInfo `_getNextMainTraderMethod` (line ~63)
- private static MethodInfo `_getVillageOfferMethod` (line ~64)
- private static MethodInfo `_getTimeLeftToMethod` (line ~65)
- private static MethodInfo `_getStayingTimeLeftMethod` (line ~66)
- private static MethodInfo `_canForceArrivalMethod` (line ~67)
- private static MethodInfo `_getForceArrivalPriceMethod` (line ~68)
- private static MethodInfo `_forceArrivalMethod` (line ~69)
- private static MethodInfo `_isTradingBlockedMethod` (line ~70)
- private static MethodInfo `_isStormTooCloseToForceMethod` (line ~71)
- private static MethodInfo `_canPayForceArrivalPriceMethod` (line ~72)
- private static MethodInfo `_hasAnyTradePostMethod` (line ~73)
- private static MethodInfo `_assaultTraderMethod` (line ~74)
- private static MethodInfo `_getValueInCurrencyGoodNameMethod` (line ~78)  `GetValueInCurrency(string, int)`
- private static MethodInfo `_getValueInCurrencyMethod` (line ~79)  `GetValueInCurrency(TradingOffer)`
- private static MethodInfo `_getValueInCurrencyEffectStateMethod` (line ~80)  `GetValueInCurrency(TraderEffectState)`
- private static MethodInfo `_getBuyValueInCurrencyGoodNameMethod` (line ~83)
- private static MethodInfo `_getBuyValueInCurrencyMethod` (line ~84)
- private static MethodInfo `_completeTradeMethod` (line ~87)  `CompleteTrade(visit, villageOffer, traderOffer)`
- private static MethodInfo `_completeTradeEffectMethod` (line ~88)  `CompleteTrade(visit, TraderEffectState)`

**IStorageService methods**
- private static MethodInfo `_getAmountMethod` (line ~93)

**TraderVisitState fields**
- private static FieldInfo `_visitGoodsField` (line ~97)
- private static FieldInfo `_visitOfferedEffectsField` (line ~98)
- private static FieldInfo `_visitTravelProgressField` (line ~99)
- private static FieldInfo `_visitForcedField` (line ~100)
- private static FieldInfo `_visitIsExtraField` (line ~101)
- private static FieldInfo `_visitTraderField` (line ~102)

**TraderModel fields**
- private static FieldInfo `_traderDisplayNameField` (line ~108)
- private static FieldInfo `_traderDescriptionField` (line ~109)
- private static FieldInfo `_traderDialogueField` (line ~110)
- private static FieldInfo `_traderLabelField` (line ~111)
- private static FieldInfo `_traderCanAssaultField` (line ~112)
- private static FieldInfo `_traderTransactionSoundField` (line ~113)

**SoundRef / LabelModel**
- private static MethodInfo `_soundRefGetNextMethod` (line ~117)
- private static FieldInfo `_labelDisplayNameField` (line ~118)

**Good / TradingGood / TradingOffer**
- private static Type `_goodType` (line ~122)
- private static FieldInfo `_goodNameField` (line ~123)
- private static FieldInfo `_goodAmountField` (line ~124)
- private static ConstructorInfo `_goodCtor` (line ~125)
- private static FieldInfo `_tradingGoodNameField` (line ~128)
- private static FieldInfo `_tradingGoodStorageAmountField` (line ~129)
- private static FieldInfo `_tradingGoodOfferedAmountField` (line ~130)
- private static Type `_tradingOfferType` (line ~133)
- private static ConstructorInfo `_tradingOfferCtor` (line ~134)
- private static FieldInfo `_tradingOfferGoodsField` (line ~135)

**TraderEffectState fields**
- private static FieldInfo `_effectStateEffectField` (line ~140)
- private static FieldInfo `_effectStateSoldField` (line ~141)
- private static FieldInfo `_effectStateDiscountedField` (line ~142)
- private static FieldInfo `_effectStatePriceRatioField` (line ~143)

**EffectModel / Settings / GoodModel**
- private static PropertyInfo `_effectDisplayNameProperty` (line ~148)
- private static PropertyInfo `_effectDescriptionProperty` (line ~149)
- private static MethodInfo `_getGoodMethod` (line ~152)
- private static MethodInfo `_getEffectMethod` (line ~153)
- private static MethodInfo `_getTraderFromSettingsMethod` (line ~154)
- private static FieldInfo `_tradeCurrencyField` (line ~155)
- private static FieldInfo `_goodDisplayNameField` (line ~157)

**Assault / blackboard**
- private static FieldInfo `_assaultVillagersKilledField` (line ~161)
- private static MethodInfo `_canAttackTraderMethod` (line ~162)
- private static PropertyInfo `_assaultResultPopupRequestedProperty` (line ~163)
- private static PropertyInfo `_traderPanelInstanceProperty` (line ~164)
- private static MethodInfo `_traderPanelHideMethod` (line ~165)

**ICalendarService**
- private static PropertyInfo `_calendarSeasonProperty` (line ~168)
- private static MethodInfo `_getTimeTillNextSeasonChangeMethod` (line ~169)

**Panel visit field (lazily cached per-instance)**
- private static object `_currentTraderPanel` (line ~173)
- private static FieldInfo `_panelVisitField` (line ~174)
- private static bool `_cached` (line ~177)

### Methods
- private static void `EnsureCached()` (line 167)
  Uses overload-scanning loops for GetValueInCurrency, GetBuyValueInCurrency, and CompleteTrade.
- private static object `GetTradeService()` (line 372)
- private static object `GetStorageService()` (line 377)
- private static object `GetSettings()` (line 382)
- private static string `GetLocaText(object locaText)` (line 387)

**Popup detection**
- public static bool `IsTraderPanel(object popup)` (line 398)

**Glade trader panel support**
- public static void `SetCurrentPanel(object traderPanel)` (line 411)
  Stores the panel reference and lazily caches the visit field from its type.
- public static void `ClearCurrentPanel()` (line 422)
- private static object `GetPanelVisit()` (line 426)
- private static bool `IsVisitExtra(object visit)` (line 430)
- private static object `GetTraderFromVisit(object visit)` (line 438)
- private static object `GetCurrentTrader()` (line 450)
  Prefers panel visit for correct glade trader resolution.

**Trader state**
- public static bool `IsTraderPresent()` (line 469)
  Returns true for both main and glade event traders.
- public static bool `IsTradingBlocked()` (line 485)
- public static object `GetCurrentVisit()` (line 494)
- public static float `GetTravelProgress()` (line 507)
- public static float `GetStayingTimeLeft()` (line 605)
- public static float `GetTimeToArrival()` (line 594)

**Trader info**
- public static string `GetTraderName()` (line 520)
  Returns current trader's name, or next expected trader if none present.
- public static string `GetTraderLabel()` (line 536)
- public static string `GetTraderDescription()` (line 555)
- public static string `GetTraderDialogue()` (line 571)
- public static object `GetTraderTransactionSound()` (line 580)

**Force arrival**
- public static bool `CanForceArrival()` (line 620)
- public static float `GetForceArrivalCost()` (line 629)
- public static bool `ForceTraderArrival()` (line 638)
- public static bool `IsStormTooCloseToForce()` (line 647)
- public static bool `CanPayForceArrivalPrice()` (line 656)
- public static bool `HasAnyTradePost()` (line 665)
- public static bool `IsVisitAlreadyForced()` (line 674)
- public static string `GetForceArrivalUnavailableReason()` (line 684)
  Returns a human-readable explanation for why force arrival is blocked, or null if available.

**Season info**
- private static object `GetCalendarService()` (line 715)
- public static bool `IsStormSeason()` (line 723)
- public static float `GetTimeTillSeasonChange()` (line 734)

**Goods trading**
- public static List<TradingGoodInfo> `GetVillageGoods()` (line 748)
- public static List<TradingGoodInfo> `GetTraderGoods()` (line 800)
- public static int `GetAmberInStorage()` (line 851)
- private static string `GetTradeCurrencyName()` (line 867)
- public static float `GetGoodSellValue(string goodName, int amount)` (line 888)
- public static float `GetGoodBuyValue(string goodName, int amount)` (line 898)
- public static bool `ExecuteTrade(List<KeyValuePair<string, int>> sellGoods, List<KeyValuePair<string, int>> buyGoods)` (line 911)
  Builds TradingOffer objects and calls CompleteTrade via reflection.
- private static void `SetOfferedAmountsFromStorage(object goodsDict)` (line 977)

**Perks**
- public static List<PerkInfo> `GetPerks()` (line 1005)
- public static bool `BuyPerk(object effectState)` (line 1066)

**Assault**
- public static bool `CanAssaultTrader()` (line 1080)
- public static AssaultResult `AssaultTrader()` (line 1100)
- private static void `TriggerAssaultResultPopup(object assaultResult)` (line 1145)
  Fires the blackboard Subject<TraderAssaultResult>.OnNext to trigger the result popup.
- private static void `HideTraderPanel()` (line 1182)
- public static int `LogCacheStatus()` (line 1203)
