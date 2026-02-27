# TradeRoutesReflection.cs
Reflection helpers for the trade routes system. Provides popup detection, town/offer enumeration, route state, and all interaction methods.

## class TradeRoutesReflection (line 7)

### Nested Structs
- public struct `TownInfo` (line 16): `TownState` (object), `TownName`, `FactionName`, `BiomeName`, `Offers` (List<OfferInfo>), `TravelTimeTicks`, `FuelCost`
- public struct `OfferInfo` (line 34): `OfferState` (object), `GoodName`, `GoodDisplayName`, `Amount`, `Price`, `PriceGoodName`, `PriceGoodDisplayName`, `TravelTimeTicks`, `FuelCost`, `IsReady`, `IsAccepted`, `IsBlocked`, `BlockedReason`, `MaxAmount`
  Note: `IsAccepted` reads from field named `accpeted` (typo in game code).
- public struct `RouteInfo` (line 52): `RouteState` (object), `TownName`, `GoodDisplayName`, `Amount`, `IsReady`, `CanCollect`, `ArrivalTimeTick`

### Fields
**Service cache**
- private static PropertyInfo `_gsTradeRoutesServiceProperty` (line ~80)
- private static PropertyInfo `_gsStateServiceProperty` (line ~81)
- private static PropertyInfo `_gsEffectsServiceProperty` (line ~82)

**ITradeRoutesService methods**
- private static MethodInfo `_trsGetTownsMethod` (line ~88)
- private static MethodInfo `_trsGetTownOffersMethod` (line ~89)
- private static MethodInfo `_trsAcceptOfferMethod` (line ~90)
- private static MethodInfo `_trsExtendOfferMethod` (line ~91)
- private static MethodInfo `_trsSetAmountMethod` (line ~92)
- private static MethodInfo `_trsGetAmountMethod` (line ~93)
- private static MethodInfo `_trsGetMaxAmountMethod` (line ~94)
- private static MethodInfo `_trsCanAcceptMethod` (line ~95)
- private static MethodInfo `_trsCanAcceptAnyAmountMethod` (line ~96)
- private static MethodInfo `_trsHaveEnoughGoodsMethod` (line ~97)
- private static MethodInfo `_trsHaveEnoughFuelMethod` (line ~98)
- private static MethodInfo `_trsGetFullFuelMethod` (line ~99)
- private static MethodInfo `_trsGetFullPriceMethod` (line ~100)
- private static MethodInfo `_trsGetFullTravelTimeMethod` (line ~101)
- private static MethodInfo `_trsGetActiveRoutesMethod` (line ~102)
- private static MethodInfo `_trsCanCollectMethod` (line ~103)
- private static MethodInfo `_trsCollectMethod` (line ~104)

**State/prefs fields**
- private static PropertyInfo `_stateServiceTradeStateProperty` (line ~110)
- private static PropertyInfo `_stateServicePrefsStateProperty` (line ~111)
- private static FieldInfo `_tradeStateAutoCollectField` (line ~112)
- private static FieldInfo `_prefsOnlyAvailableField` (line ~113)
- private static FieldInfo `_tradeStateRouteCountField` (line ~114)
- private static FieldInfo `_tradeStateMaxRoutesField` (line ~115)

**Offer fields**
- private static FieldInfo `_offerGoodField` (line ~121)
- private static FieldInfo `_offerPriceField` (line ~122)
- private static FieldInfo `_offerTravelTimeField` (line ~123)
- private static FieldInfo `_offerFuelCostField` (line ~124)
- private static FieldInfo `_offerIsReadyField` (line ~125)
- private static FieldInfo `_offerAcceptedField` (line ~126)
  Note: field name is "accpeted" in game code (typo).

**Town state fields**
- private static FieldInfo `_townNameField` (line ~132)
- private static FieldInfo `_townFactionField` (line ~133)
- private static FieldInfo `_townBiomeField` (line ~134)
- private static FieldInfo `_townTravelTimeField` (line ~135)
- private static FieldInfo `_townFuelCostField` (line ~136)

**Route state fields**
- private static FieldInfo `_routeStateGoodField` (line ~142)
- private static FieldInfo `_routeStateAmountField` (line ~143)
- private static FieldInfo `_routeStateIsReadyField` (line ~144)
- private static FieldInfo `_routeStateArrivalTimeField` (line ~145)
- private static FieldInfo `_routeStateTownField` (line ~146)

**Good / settings / display name fields**
- private static FieldInfo `_goodNameField` (line ~152)
- private static FieldInfo `_goodAmountField` (line ~153)
- private static MethodInfo `_getGoodMethod` (line ~154)
- private static MethodInfo `_getBiomeMethod` (line ~155)
- private static MethodInfo `_getFactionMethod` (line ~156)
- private static FieldInfo `_goodModelDisplayNameField` (line ~157)
- private static FieldInfo `_biomeDisplayNameField` (line ~158)
- private static FieldInfo `_factionDisplayNameField` (line ~159)
- private static bool `_cached` (line ~162)

**Popup type cache**
- private static Type `_tradeRoutesPopupType` (line ~166)

### Methods
- private static void `EnsureCached()` (line 169)
- private static void `CacheServiceTypes(Assembly)` (line ~180)
- private static void `CacheStateTypes(Assembly)` (line ~190)
- private static void `CacheTradeRoutesServiceMethods(Assembly)` (line ~200)
- private static void `CacheGoodTypes(Assembly)` (line ~210)
- private static void `CacheSettingsTypes(Assembly)` (line ~220)
- private static object `GetTradeRoutesService()` (line 332)
- private static object `GetStateService()` (line 337)
- private static object `GetEffectsService()` (line 342)
- private static object `GetTradeState()` (line 347)
- private static object `GetPrefsState()` (line 351)
- public static bool `IsTradeRoutesPopup(object popup)` (line 362)
- public static bool `IsAutoCollectEnabled()` (line 374)
- public static bool `SetAutoCollect(bool enabled)` (line 381)
- public static bool `AutoCollectAllReady()` (line 389)
  Collects all ready routes and returns whether any were collected.
- public static bool `IsOnlyAvailableEnabled()` (line 405)
- public static bool `SetOnlyAvailable(bool enabled)` (line 412)
- public static bool `HasReachedLimit()` (line 423)
- public static int `GetMaxRoutes()` (line 432)
- public static List<TownInfo> `GetTradeTowns()` (line 443)
- public static List<OfferInfo> `GetTownOffers(object townState)` (line 523)
- private static OfferInfo `BuildOfferInfo(object offerState, object townState)` (line 553)
- private static string `ExtractGoodName(object good)` (line 614)
- private static int `ExtractGoodAmount(object good)` (line 622)
- private static string `GetFuelGoodName()` (line 628)
- private static string `GetBlockedReason(object tradeService, object offerState, object townState)` (line 647)
- private static bool `CanAccept(object tradeService, object offerState, object townState)` (line 655)
- private static bool `CanAcceptAnyAmount(object tradeService, object offerState, object townState)` (line 659)
- private static bool `HaveEnoughGoods(object tradeService, object offerState, object townState)` (line 663)
- private static bool `HaveEnoughFuel(object tradeService, object offerState, object townState)` (line 667)
- private static int `GetFullFuel(object tradeService, object offerState, object townState)` (line 671)
- private static int `GetFullPrice(object tradeService, object offerState, object townState)` (line 675)
- private static int `GetFullTravelTime(object tradeService, object offerState, object townState)` (line 679)
- public static List<RouteInfo> `GetActiveRoutes()` (line 690)
- private static bool `CanCollect(object tradeService, object routeState)` (line 753)
- public static bool `Collect(object routeState)` (line 764)
- public static bool `AcceptOffer(object offerState)` (line 771)
- public static bool `ExtendOffer(object offerState)` (line 778)
- public static bool `SetOfferAmount(object offerState, int amount)` (line 785)
- public static int `GetOfferAmount(object offerState)` (line 794)
- public static int `GetMaxOfferAmount()` (line 803)
- public static string `GetGoodDisplayName(string goodName)` (line 814)
- public static string `GetBiomeDisplayName(string biomeName)` (line 824)
- public static string `GetFactionDisplayName(string factionName)` (line 834)
- public static string `GetLocalizedText(string internalName)` (line 849)
  Generic localization helper used for any named model.
- public static string `FormatGood(object good)` (line 879)
  Formats a Good struct as "N DisplayName".
- public static int `LogCacheStatus()` (line 886)
