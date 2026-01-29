using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing trade routes service, popup detection, and data extraction.
    /// Provides methods for navigating towns, offers, routes, and performing actions like accept/collect.
    /// </summary>
    public static class TradeRoutesReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public struct TownInfo
        {
            public object State;           // TradeTownState object for actions
            public int Id;
            public string Name;
            public string Biome;
            public string Faction;         // Faction name (e.g., "Lizard Merchants") or null
            public int Distance;           // Distance from capital
            public int StandingLevel;
            public string StandingLabel;
            public bool IsMaxStanding;
            public int CurrentStandingValue;
            public int ValueForLevelUp;
            public int OfferCount;
            public bool CanExtend;
            public bool ReachedMaxOffers;  // True if no more extends allowed
            public string ExtendCost;      // e.g., "5 Provisions"
        }

        public struct OfferInfo
        {
            public object State;           // TownOfferState object for actions
            public int TownId;
            public string TownName;
            public string GoodName;        // Display name
            public int GoodAmount;         // Per unit
            public string FuelName;        // Display name
            public int FuelAmount;         // Full amount for current multiplier
            public string PriceName;       // Display name (Amber)
            public int PriceAmount;        // Full amount for current multiplier
            public float TravelTime;       // Full travel time
            public int Multiplier;         // Current amount (1-5)
            public int MaxMultiplier;      // Max amount (5)
            public bool Accepted;
            public bool CanAccept;
            public string BlockedReason;   // "not enough goods", "not enough fuel", "route limit reached", "already accepted"
        }

        public struct RouteInfo
        {
            public object State;           // RouteState object for actions
            public int TownId;
            public string TownName;
            public string GoodName;        // Display name
            public int GoodAmount;
            public string PriceName;       // Display name (Amber)
            public int PriceAmount;
            public float Progress;         // 0-1
            public float TimeRemaining;    // Seconds
            public bool CanCollect;
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // IGameServices service properties
        private static PropertyInfo _gsTradeRoutesServiceProperty = null;
        private static PropertyInfo _gsStateServiceProperty = null;
        private static PropertyInfo _gsEffectsServiceProperty = null;
        private static PropertyInfo _gsCalendarServiceProperty = null;
        private static PropertyInfo _gsStorageServiceProperty = null;

        // IStateService
        private static PropertyInfo _stateTradeProperty = null;
        private static PropertyInfo _statePrefsProperty = null;

        // TradeState fields
        private static FieldInfo _tradeTradeTownsField = null;
        private static FieldInfo _tradeRoutesField = null;

        // PrefsState fields
        private static FieldInfo _prefsAutoCollectField = null;
        private static FieldInfo _prefsOnlyAvailableField = null;

        // TradeTownState fields
        private static FieldInfo _townIdField = null;
        private static FieldInfo _townNameField = null;
        private static FieldInfo _townBiomeField = null;
        private static FieldInfo _townFactionField = null;
        private static FieldInfo _townDistanceField = null;
        private static FieldInfo _townStandingLevelField = null;
        private static FieldInfo _townIsMaxStandingField = null;
        private static FieldInfo _townCurrentStandingField = null;
        private static FieldInfo _townValueForLevelUpField = null;
        private static FieldInfo _townOffersField = null;
        private static FieldInfo _townHasStaticNameField = null;

        // TownOfferState fields
        private static FieldInfo _offerTownIdField = null;
        private static FieldInfo _offerTownNameField = null;
        private static FieldInfo _offerGoodField = null;
        private static FieldInfo _offerFuelField = null;
        private static FieldInfo _offerPriceField = null;
        private static FieldInfo _offerAmountField = null;
        private static FieldInfo _offerTravelTimeField = null;
        private static FieldInfo _offerAcceptedField = null;
        private static FieldInfo _offerHasStaticNameField = null;

        // RouteState fields
        private static FieldInfo _routeTownIdField = null;
        private static FieldInfo _routeTownNameField = null;
        private static FieldInfo _routeGoodField = null;
        private static FieldInfo _routeFuelField = null;
        private static FieldInfo _routePriceField = null;
        private static FieldInfo _routeTravelTimeField = null;
        private static FieldInfo _routeStartTimeField = null;
        private static FieldInfo _routeProgressField = null;
        private static FieldInfo _routeOfferAmountField = null;
        private static FieldInfo _routeHasStaticNameField = null;

        // Good struct fields
        private static FieldInfo _goodNameField = null;
        private static FieldInfo _goodAmountField = null;

        // ITradeRoutesService methods
        private static MethodInfo _canCollectMethod = null;
        private static MethodInfo _collectMethod = null;
        private static MethodInfo _acceptOfferMethod = null;
        private static MethodInfo _canAcceptMethod = null;
        private static MethodInfo _canAcceptAnyAmountMethod = null;
        private static MethodInfo _getOfferExtendingPriceMethod = null;
        private static MethodInfo _reachedMaxOffersMethod = null;
        private static MethodInfo _canExtendOfferMethod = null;
        private static MethodInfo _extendOfferMethod = null;
        private static MethodInfo _getStandingLabelForMethod = null;
        private static MethodInfo _getFullGoodMethod = null;
        private static MethodInfo _getFullPriceMethod = null;
        private static MethodInfo _getFullFuelMethod = null;
        private static MethodInfo _getFullTravelTimeMethod = null;
        private static MethodInfo _haveEnoughGoodsForMethod = null;
        private static MethodInfo _haveEnoughFuelForMethod = null;
        private static MethodInfo _hasReachedLimitMethod = null;
        private static MethodInfo _countMaxRoutesToStartMethod = null;

        // IEffectsService methods
        private static MethodInfo _getTradeRoutesAmountMethod = null;

        // Settings access
        private static MethodInfo _getGoodMethod = null;
        private static FieldInfo _goodDisplayNameField = null;
        private static FieldInfo _tradeCurrencyField = null;
        private static FieldInfo _tradeRoutesConfigField = null;
        private static FieldInfo _configFuelField = null;
        private static FieldInfo _configMaxOfferAmountField = null;
        private static MethodInfo _settingsGetBiomeMethod = null;
        private static FieldInfo _biomeDisplayNameField = null;
        private static MethodInfo _settingsGetFactionMethod = null;
        private static FieldInfo _factionDisplayNameField = null;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TradeRoutesReflection: Game assembly not available");
                    return;
                }

                CacheServiceTypes(assembly);
                CacheStateTypes(assembly);
                CacheTradeRoutesServiceMethods(assembly);
                CacheGoodTypes(assembly);
                CacheSettingsTypes(assembly);

                Debug.Log("[ATSAccessibility] TradeRoutesReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TradeRoutesReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsTradeRoutesServiceProperty = gameServicesType.GetProperty("TradeRoutesService", GameReflection.PublicInstance);
                _gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
                _gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService", GameReflection.PublicInstance);
                _gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService", GameReflection.PublicInstance);
                _gsStorageServiceProperty = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
            }

            var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
            if (effectsServiceType != null)
            {
                _getTradeRoutesAmountMethod = effectsServiceType.GetMethod("GetTradeRoutesAmount", GameReflection.PublicInstance);
            }
        }

        private static void CacheStateTypes(Assembly assembly)
        {
            // IStateService
            var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
            if (stateServiceType != null)
            {
                _stateTradeProperty = stateServiceType.GetProperty("Trade", GameReflection.PublicInstance);
                _statePrefsProperty = stateServiceType.GetProperty("Prefs", GameReflection.PublicInstance);
            }

            // TradeState
            var tradeStateType = assembly.GetType("Eremite.Model.State.TradeState");
            if (tradeStateType != null)
            {
                _tradeTradeTownsField = tradeStateType.GetField("tradeTowns", GameReflection.PublicInstance);
                _tradeRoutesField = tradeStateType.GetField("routes", GameReflection.PublicInstance);
            }

            // PrefsState
            var prefsStateType = assembly.GetType("Eremite.Model.State.PrefsState");
            if (prefsStateType != null)
            {
                _prefsAutoCollectField = prefsStateType.GetField("autoCollectTradeRoutes", GameReflection.PublicInstance);
                _prefsOnlyAvailableField = prefsStateType.GetField("onlyAvailableTradeRoutes", GameReflection.PublicInstance);
            }

            // TradeTownState
            var townStateType = assembly.GetType("Eremite.Model.State.TradeTownState");
            if (townStateType != null)
            {
                _townIdField = townStateType.GetField("id", GameReflection.PublicInstance);
                _townNameField = townStateType.GetField("townName", GameReflection.PublicInstance);
                _townBiomeField = townStateType.GetField("biome", GameReflection.PublicInstance);
                _townFactionField = townStateType.GetField("faction", GameReflection.PublicInstance);
                _townDistanceField = townStateType.GetField("distance", GameReflection.PublicInstance);
                _townStandingLevelField = townStateType.GetField("standingLevel", GameReflection.PublicInstance);
                _townIsMaxStandingField = townStateType.GetField("isMaxStanding", GameReflection.PublicInstance);
                _townCurrentStandingField = townStateType.GetField("currentStandingValue", GameReflection.PublicInstance);
                _townValueForLevelUpField = townStateType.GetField("valueForLevelUp", GameReflection.PublicInstance);
                _townOffersField = townStateType.GetField("offers", GameReflection.PublicInstance);
                _townHasStaticNameField = townStateType.GetField("hasStaticName", GameReflection.PublicInstance);
            }

            // TownOfferState
            var offerStateType = assembly.GetType("Eremite.Model.State.TownOfferState");
            if (offerStateType != null)
            {
                _offerTownIdField = offerStateType.GetField("townId", GameReflection.PublicInstance);
                _offerTownNameField = offerStateType.GetField("townName", GameReflection.PublicInstance);
                _offerGoodField = offerStateType.GetField("good", GameReflection.PublicInstance);
                _offerFuelField = offerStateType.GetField("fuel", GameReflection.PublicInstance);
                _offerPriceField = offerStateType.GetField("price", GameReflection.PublicInstance);
                _offerAmountField = offerStateType.GetField("amount", GameReflection.PublicInstance);
                _offerTravelTimeField = offerStateType.GetField("travelTime", GameReflection.PublicInstance);
                _offerAcceptedField = offerStateType.GetField("accpeted", GameReflection.PublicInstance);  // Typo in game code
                _offerHasStaticNameField = offerStateType.GetField("hasStaticName", GameReflection.PublicInstance);
            }

            // RouteState
            var routeStateType = assembly.GetType("Eremite.Model.State.RouteState");
            if (routeStateType != null)
            {
                _routeTownIdField = routeStateType.GetField("townId", GameReflection.PublicInstance);
                _routeTownNameField = routeStateType.GetField("townName", GameReflection.PublicInstance);
                _routeGoodField = routeStateType.GetField("good", GameReflection.PublicInstance);
                _routeFuelField = routeStateType.GetField("fuel", GameReflection.PublicInstance);
                _routePriceField = routeStateType.GetField("price", GameReflection.PublicInstance);
                _routeTravelTimeField = routeStateType.GetField("travelTime", GameReflection.PublicInstance);
                _routeStartTimeField = routeStateType.GetField("startTime", GameReflection.PublicInstance);
                _routeProgressField = routeStateType.GetField("progress", GameReflection.PublicInstance);
                _routeOfferAmountField = routeStateType.GetField("offerAmount", GameReflection.PublicInstance);
                _routeHasStaticNameField = routeStateType.GetField("hasStaticName", GameReflection.PublicInstance);
            }
        }

        private static void CacheTradeRoutesServiceMethods(Assembly assembly)
        {
            var serviceType = assembly.GetType("Eremite.Services.ITradeRoutesService");
            if (serviceType == null) return;

            _canCollectMethod = serviceType.GetMethod("CanCollect", GameReflection.PublicInstance);
            _collectMethod = serviceType.GetMethod("Collect", GameReflection.PublicInstance);
            _acceptOfferMethod = serviceType.GetMethod("AcceptOffer", GameReflection.PublicInstance);
            _canAcceptMethod = serviceType.GetMethod("CanAccept", GameReflection.PublicInstance);
            _canAcceptAnyAmountMethod = serviceType.GetMethod("CanAcceptAnyAmount", GameReflection.PublicInstance);
            _getOfferExtendingPriceMethod = serviceType.GetMethod("GetOfferExtendingPrice", GameReflection.PublicInstance);
            _reachedMaxOffersMethod = serviceType.GetMethod("ReachedMaxOffers", GameReflection.PublicInstance);
            _canExtendOfferMethod = serviceType.GetMethod("CanExtendOffer", GameReflection.PublicInstance);
            _extendOfferMethod = serviceType.GetMethod("ExtendOffer", GameReflection.PublicInstance);
            _getStandingLabelForMethod = serviceType.GetMethod("GetStandingLabelFor", GameReflection.PublicInstance);
            _getFullGoodMethod = serviceType.GetMethod("GetFullGood", GameReflection.PublicInstance);
            _getFullPriceMethod = serviceType.GetMethod("GetFullPrice", GameReflection.PublicInstance);
            _getFullFuelMethod = serviceType.GetMethod("GetFullFuel", GameReflection.PublicInstance);
            _getFullTravelTimeMethod = serviceType.GetMethod("GetFullTravelTime", GameReflection.PublicInstance);
            _haveEnoughGoodsForMethod = serviceType.GetMethod("HaveEnoughGoodsFor", GameReflection.PublicInstance);
            _haveEnoughFuelForMethod = serviceType.GetMethod("HaveEnoughFuelFor", GameReflection.PublicInstance);
            _hasReachedLimitMethod = serviceType.GetMethod("HasReachedLimit", GameReflection.PublicInstance);
            _countMaxRoutesToStartMethod = serviceType.GetMethod("CountMaxRoutesToStart", GameReflection.PublicInstance);
        }

        private static void CacheGoodTypes(Assembly assembly)
        {
            var goodType = assembly.GetType("Eremite.Model.Good");
            if (goodType != null)
            {
                _goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
                _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
            }
        }

        private static void CacheSettingsTypes(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _getGoodMethod = settingsType.GetMethod("GetGood", new[] { typeof(string) });
                _tradeCurrencyField = settingsType.GetField("tradeCurrency", GameReflection.PublicInstance);
                _tradeRoutesConfigField = settingsType.GetField("tradeRoutesConfig", GameReflection.PublicInstance);
            }

            var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
            if (goodModelType != null)
            {
                _goodDisplayNameField = goodModelType.GetField("displayName", GameReflection.PublicInstance);
            }

            var configType = assembly.GetType("Eremite.Model.Configs.TradeRoutesConfig");
            if (configType != null)
            {
                _configFuelField = configType.GetField("fuel", GameReflection.PublicInstance);
                _configMaxOfferAmountField = configType.GetField("maxOfferAmount", GameReflection.PublicInstance);
            }

            // Biome access
            _settingsGetBiomeMethod = settingsType?.GetMethod("GetBiome", new[] { typeof(string) });
            var biomeModelType = assembly.GetType("Eremite.Model.BiomeModel");
            _biomeDisplayNameField = biomeModelType?.GetField("displayName", GameReflection.PublicInstance);

            // Faction access
            _settingsGetFactionMethod = settingsType?.GetMethod("GetFaction", new[] { typeof(string) });
            var factionModelType = assembly.GetType("Eremite.Model.FactionModel");
            _factionDisplayNameField = factionModelType?.GetField("displayName", GameReflection.PublicInstance);
        }

        // ========================================
        // SERVICE ACCESS (fresh each call)
        // ========================================

        private static object GetTradeRoutesService()
        {
            EnsureCached();
            return GameReflection.GetService(_gsTradeRoutesServiceProperty);
        }

        private static object GetStateService()
        {
            EnsureCached();
            return GameReflection.GetService(_gsStateServiceProperty);
        }

        private static object GetEffectsService()
        {
            EnsureCached();
            return GameReflection.GetService(_gsEffectsServiceProperty);
        }

        private static object GetTradeState()
        {
            var stateService = GetStateService();
            if (stateService == null || _stateTradeProperty == null) return null;
            try { return _stateTradeProperty.GetValue(stateService); }
            catch { return null; }
        }

        private static object GetPrefsState()
        {
            var stateService = GetStateService();
            if (stateService == null || _statePrefsProperty == null) return null;
            try { return _statePrefsProperty.GetValue(stateService); }
            catch { return null; }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a TradeRoutesPopup.
        /// </summary>
        public static bool IsTradeRoutesPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "TradeRoutesPopup";
        }

        // ========================================
        // TOGGLE SETTINGS
        // ========================================

        /// <summary>
        /// Check if auto-collect is enabled.
        /// </summary>
        public static bool IsAutoCollectEnabled()
        {
            var prefs = GetPrefsState();
            if (prefs == null || _prefsAutoCollectField == null) return false;
            try { return (bool)_prefsAutoCollectField.GetValue(prefs); }
            catch { return false; }
        }

        /// <summary>
        /// Set auto-collect enabled state.
        /// </summary>
        public static void SetAutoCollect(bool enabled)
        {
            var prefs = GetPrefsState();
            if (prefs == null || _prefsAutoCollectField == null) return;
            try { _prefsAutoCollectField.SetValue(prefs, enabled); }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetAutoCollect failed: {ex.Message}"); }
        }

        /// <summary>
        /// Auto-collect all ready routes. Called when enabling auto-collect to match game behavior.
        /// Returns number of routes collected.
        /// </summary>
        public static int AutoCollectAllReady()
        {
            var routes = GetActiveRoutes();
            int collected = 0;

            foreach (var route in routes)
            {
                if (route.CanCollect && Collect(route.State))
                {
                    collected++;
                }
            }

            return collected;
        }

        /// <summary>
        /// Check if "only available" filter is enabled.
        /// </summary>
        public static bool IsOnlyAvailableEnabled()
        {
            var prefs = GetPrefsState();
            if (prefs == null || _prefsOnlyAvailableField == null) return false;
            try { return (bool)_prefsOnlyAvailableField.GetValue(prefs); }
            catch { return false; }
        }

        /// <summary>
        /// Set "only available" filter enabled state.
        /// </summary>
        public static void SetOnlyAvailable(bool enabled)
        {
            var prefs = GetPrefsState();
            if (prefs == null || _prefsOnlyAvailableField == null) return;
            try { _prefsOnlyAvailableField.SetValue(prefs, enabled); }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetOnlyAvailable failed: {ex.Message}"); }
        }

        // ========================================
        // ROUTE LIMIT
        // ========================================

        /// <summary>
        /// Check if route limit has been reached.
        /// </summary>
        public static bool HasReachedLimit()
        {
            var service = GetTradeRoutesService();
            if (service == null || _hasReachedLimitMethod == null) return true;
            try { return (bool)_hasReachedLimitMethod.Invoke(service, null); }
            catch { return true; }
        }

        /// <summary>
        /// Get the maximum number of active routes.
        /// </summary>
        public static int GetMaxRoutes()
        {
            var effectsService = GetEffectsService();
            if (effectsService == null || _getTradeRoutesAmountMethod == null) return 0;
            try { return (int)_getTradeRoutesAmountMethod.Invoke(effectsService, null); }
            catch { return 0; }
        }

        // ========================================
        // DATA EXTRACTION - TOWNS
        // ========================================

        /// <summary>
        /// Get list of all trade towns.
        /// </summary>
        public static List<TownInfo> GetTradeTowns()
        {
            EnsureCached();
            var result = new List<TownInfo>();

            var tradeState = GetTradeState();
            if (tradeState == null || _tradeTradeTownsField == null) return result;

            try
            {
                var towns = _tradeTradeTownsField.GetValue(tradeState) as IList;
                if (towns == null) return result;

                var service = GetTradeRoutesService();

                foreach (var town in towns)
                {
                    if (town == null) continue;

                    // Handle hasStaticName - if true, townName is a localization key
                    string townName = GetString(_townNameField, town);
                    bool hasStaticName = GetBool(_townHasStaticNameField, town);
                    if (hasStaticName && !string.IsNullOrEmpty(townName))
                    {
                        townName = GetLocalizedText(townName) ?? townName;
                    }

                    var info = new TownInfo
                    {
                        State = town,
                        Id = GetInt(_townIdField, town),
                        Name = townName,
                        Biome = GetBiomeDisplayName(GetString(_townBiomeField, town)),
                        Faction = GetFactionDisplayName(GetString(_townFactionField, town)),
                        Distance = GetInt(_townDistanceField, town),
                        StandingLevel = GetInt(_townStandingLevelField, town),
                        IsMaxStanding = GetBool(_townIsMaxStandingField, town),
                        CurrentStandingValue = GetInt(_townCurrentStandingField, town),
                        ValueForLevelUp = GetInt(_townValueForLevelUpField, town),
                        OfferCount = GetOfferCount(town),
                        StandingLabel = GetStandingLabel(service, town),
                        CanExtend = CanExtendOffer(service, town),
                        ReachedMaxOffers = HasReachedMaxOffers(service, town),
                        ExtendCost = GetExtendCost(service, town)
                    };

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetTradeTowns failed: {ex.Message}");
            }

            return result;
        }

        private static int GetOfferCount(object town)
        {
            if (town == null || _townOffersField == null) return 0;
            try
            {
                var offers = _townOffersField.GetValue(town) as IList;
                return offers?.Count ?? 0;
            }
            catch { return 0; }
        }

        private static string GetStandingLabel(object service, object town)
        {
            if (service == null || town == null || _getStandingLabelForMethod == null) return "Unknown";
            try { return _getStandingLabelForMethod.Invoke(service, new[] { town }) as string ?? "Unknown"; }
            catch { return "Unknown"; }
        }

        private static bool CanExtendOffer(object service, object town)
        {
            if (service == null || town == null || _canExtendOfferMethod == null) return false;
            try { return (bool)_canExtendOfferMethod.Invoke(service, new[] { town }); }
            catch { return false; }
        }

        private static bool HasReachedMaxOffers(object service, object town)
        {
            if (service == null || town == null || _reachedMaxOffersMethod == null) return false;
            try { return (bool)_reachedMaxOffersMethod.Invoke(service, new[] { town }); }
            catch { return false; }
        }

        private static string GetExtendCost(object service, object town)
        {
            if (service == null || town == null || _getOfferExtendingPriceMethod == null) return "";
            try
            {
                var good = _getOfferExtendingPriceMethod.Invoke(service, new[] { town });
                if (good == null) return "";
                return FormatGood(good);
            }
            catch { return ""; }
        }

        // ========================================
        // DATA EXTRACTION - OFFERS
        // ========================================

        /// <summary>
        /// Get offers for a specific town.
        /// </summary>
        public static List<OfferInfo> GetTownOffers(object townState)
        {
            EnsureCached();
            var result = new List<OfferInfo>();

            if (townState == null || _townOffersField == null) return result;

            try
            {
                var offers = _townOffersField.GetValue(townState) as IList;
                if (offers == null) return result;

                var service = GetTradeRoutesService();
                bool onlyAvailable = IsOnlyAvailableEnabled();

                foreach (var offer in offers)
                {
                    if (offer == null) continue;

                    // Skip if only available filter is on and this offer can't be accepted
                    if (onlyAvailable && !CanAcceptAnyAmount(service, offer))
                        continue;

                    var info = BuildOfferInfo(service, offer);
                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetTownOffers failed: {ex.Message}");
            }

            return result;
        }

        private static OfferInfo BuildOfferInfo(object service, object offer)
        {
            // Handle hasStaticName for offer town name
            string offerTownName = GetString(_offerTownNameField, offer);
            bool hasStaticName = GetBool(_offerHasStaticNameField, offer);
            if (hasStaticName && !string.IsNullOrEmpty(offerTownName))
            {
                offerTownName = GetLocalizedText(offerTownName) ?? offerTownName;
            }

            var info = new OfferInfo
            {
                State = offer,
                TownId = GetInt(_offerTownIdField, offer),
                TownName = offerTownName,
                Multiplier = GetInt(_offerAmountField, offer),
                MaxMultiplier = GetMaxOfferAmount(),
                Accepted = GetBool(_offerAcceptedField, offer)
            };

            // Base good (per unit)
            var baseGood = _offerGoodField?.GetValue(offer);
            if (baseGood != null)
            {
                info.GoodName = GetGoodDisplayName(GetString(_goodNameField, baseGood));
                info.GoodAmount = GetInt(_goodAmountField, baseGood);
            }

            // Full calculations from service
            if (service != null)
            {
                // Full fuel - use service method, fallback to config-based calculation
                var fullFuel = GetFullFuel(service, offer);
                if (fullFuel != null)
                {
                    info.FuelName = GetGoodDisplayName(ExtractGoodName(fullFuel));
                    info.FuelAmount = ExtractGoodAmount(fullFuel);
                }

                // Fallback: Get fuel from config if service call returned empty
                if (string.IsNullOrEmpty(info.FuelName) || info.FuelName == "Unknown")
                {
                    info.FuelName = GetFuelGoodName();
                    int baseFuel = GetInt(_offerFuelField, offer);
                    info.FuelAmount = baseFuel * info.Multiplier;
                }

                // Full price
                var fullPrice = GetFullPrice(service, offer);
                if (fullPrice != null)
                {
                    info.PriceName = GetGoodDisplayName(ExtractGoodName(fullPrice));
                    info.PriceAmount = ExtractGoodAmount(fullPrice);
                }

                // Full travel time
                info.TravelTime = GetFullTravelTime(service, offer);

                // Can accept and blocked reason
                info.CanAccept = CanAccept(service, offer);
                info.BlockedReason = GetBlockedReason(service, offer, info.Accepted);
            }

            return info;
        }

        /// <summary>
        /// Extract the name from a boxed Good struct.
        /// </summary>
        private static string ExtractGoodName(object goodObj)
        {
            if (goodObj == null || _goodNameField == null) return "";
            try { return _goodNameField.GetValue(goodObj) as string ?? ""; }
            catch { return ""; }
        }

        /// <summary>
        /// Extract the amount from a boxed Good struct.
        /// </summary>
        private static int ExtractGoodAmount(object goodObj)
        {
            if (goodObj == null || _goodAmountField == null) return 0;
            try
            {
                var val = _goodAmountField.GetValue(goodObj);
                return val is int i ? i : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Get the fuel good name from the trade routes config.
        /// </summary>
        private static string GetFuelGoodName()
        {
            EnsureCached();
            var settings = GameReflection.GetSettings();
            if (settings == null || _tradeRoutesConfigField == null) return "Provisions";

            try
            {
                var config = _tradeRoutesConfigField.GetValue(settings);
                if (config == null || _configFuelField == null) return "Provisions";

                var fuelGoodModel = _configFuelField.GetValue(config);
                if (fuelGoodModel == null) return "Provisions";

                // Get the Name property from the GoodModel
                var nameProperty = fuelGoodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
                if (nameProperty != null)
                {
                    var name = nameProperty.GetValue(fuelGoodModel) as string;
                    return GetGoodDisplayName(name ?? "Provisions");
                }

                return "Provisions";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetFuelGoodName failed: {ex.Message}");
                return "Provisions";
            }
        }

        private static string GetBlockedReason(object service, object offer, bool accepted)
        {
            if (accepted) return "already accepted";
            if (HasReachedLimit()) return "route limit reached";
            if (!HaveEnoughGoods(service, offer)) return "not enough goods";
            if (!HaveEnoughFuel(service, offer)) return "not enough fuel";
            return null;
        }

        private static bool CanAccept(object service, object offer)
        {
            if (service == null || offer == null || _canAcceptMethod == null) return false;
            try { return (bool)_canAcceptMethod.Invoke(service, new[] { offer }); }
            catch { return false; }
        }

        private static bool CanAcceptAnyAmount(object service, object offer)
        {
            if (service == null || offer == null || _canAcceptAnyAmountMethod == null) return false;
            try { return (bool)_canAcceptAnyAmountMethod.Invoke(service, new[] { offer }); }
            catch { return false; }
        }

        private static bool HaveEnoughGoods(object service, object offer)
        {
            if (service == null || offer == null || _haveEnoughGoodsForMethod == null) return false;
            try { return (bool)_haveEnoughGoodsForMethod.Invoke(service, new[] { offer }); }
            catch { return false; }
        }

        private static bool HaveEnoughFuel(object service, object offer)
        {
            if (service == null || offer == null || _haveEnoughFuelForMethod == null) return false;
            try { return (bool)_haveEnoughFuelForMethod.Invoke(service, new[] { offer }); }
            catch { return false; }
        }

        private static object GetFullFuel(object service, object offer)
        {
            if (service == null || offer == null || _getFullFuelMethod == null) return null;
            try { return _getFullFuelMethod.Invoke(service, new[] { offer }); }
            catch { return null; }
        }

        private static object GetFullPrice(object service, object offer)
        {
            if (service == null || offer == null || _getFullPriceMethod == null) return null;
            try { return _getFullPriceMethod.Invoke(service, new[] { offer }); }
            catch { return null; }
        }

        private static float GetFullTravelTime(object service, object offer)
        {
            if (service == null || offer == null || _getFullTravelTimeMethod == null) return 0f;
            try { return (float)_getFullTravelTimeMethod.Invoke(service, new[] { offer }); }
            catch { return 0f; }
        }

        // ========================================
        // DATA EXTRACTION - ACTIVE ROUTES
        // ========================================

        /// <summary>
        /// Get list of active routes.
        /// </summary>
        public static List<RouteInfo> GetActiveRoutes()
        {
            EnsureCached();
            var result = new List<RouteInfo>();

            var tradeState = GetTradeState();
            if (tradeState == null || _tradeRoutesField == null) return result;

            try
            {
                var routes = _tradeRoutesField.GetValue(tradeState) as IList;
                if (routes == null) return result;

                var service = GetTradeRoutesService();

                foreach (var route in routes)
                {
                    if (route == null) continue;

                    // Handle hasStaticName for route town name
                    string routeTownName = GetString(_routeTownNameField, route);
                    bool hasStaticName = GetBool(_routeHasStaticNameField, route);
                    if (hasStaticName && !string.IsNullOrEmpty(routeTownName))
                    {
                        routeTownName = GetLocalizedText(routeTownName) ?? routeTownName;
                    }

                    var info = new RouteInfo
                    {
                        State = route,
                        TownId = GetInt(_routeTownIdField, route),
                        TownName = routeTownName,
                        Progress = GetFloat(_routeProgressField, route)
                    };

                    // Good
                    var good = _routeGoodField?.GetValue(route);
                    if (good != null)
                    {
                        info.GoodName = GetGoodDisplayName(GetString(_goodNameField, good));
                        info.GoodAmount = GetInt(_goodAmountField, good);
                    }

                    // Price (reward)
                    var price = _routePriceField?.GetValue(route);
                    if (price != null)
                    {
                        info.PriceName = GetGoodDisplayName(GetString(_goodNameField, price));
                        info.PriceAmount = GetInt(_goodAmountField, price);
                    }

                    // Calculate time remaining
                    float travelTime = GetFloat(_routeTravelTimeField, route);
                    float progress = info.Progress;
                    if (progress < 1f && travelTime > 0)
                    {
                        info.TimeRemaining = travelTime * (1f - progress);
                    }

                    // Can collect
                    info.CanCollect = CanCollect(service, route);

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetActiveRoutes failed: {ex.Message}");
            }

            return result;
        }

        private static bool CanCollect(object service, object route)
        {
            if (service == null || route == null || _canCollectMethod == null) return false;
            try { return (bool)_canCollectMethod.Invoke(service, new[] { route }); }
            catch { return false; }
        }

        // ========================================
        // ACTIONS
        // ========================================

        /// <summary>
        /// Collect a completed route.
        /// </summary>
        public static bool Collect(object routeState)
        {
            var service = GetTradeRoutesService();
            if (service == null || routeState == null || _collectMethod == null) return false;

            try
            {
                _collectMethod.Invoke(service, new[] { routeState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Collect failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Accept a trade offer.
        /// </summary>
        public static bool AcceptOffer(object offerState)
        {
            var service = GetTradeRoutesService();
            if (service == null || offerState == null || _acceptOfferMethod == null) return false;

            try
            {
                _acceptOfferMethod.Invoke(service, new[] { offerState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AcceptOffer failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extend offers for a town (adds one more offer slot).
        /// </summary>
        public static bool ExtendOffer(object townState)
        {
            var service = GetTradeRoutesService();
            if (service == null || townState == null || _extendOfferMethod == null) return false;

            try
            {
                _extendOfferMethod.Invoke(service, new[] { townState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ExtendOffer failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set the offer amount (multiplier 1-5).
        /// </summary>
        public static bool SetOfferAmount(object offerState, int amount)
        {
            if (offerState == null || _offerAmountField == null) return false;

            int maxAmount = GetMaxOfferAmount();
            amount = Mathf.Clamp(amount, 1, maxAmount);

            try
            {
                _offerAmountField.SetValue(offerState, amount);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetOfferAmount failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current offer amount (multiplier).
        /// </summary>
        public static int GetOfferAmount(object offerState)
        {
            if (offerState == null || _offerAmountField == null) return 1;
            try { return (int)_offerAmountField.GetValue(offerState); }
            catch { return 1; }
        }

        // ========================================
        // HELPER METHODS
        // ========================================

        private static int GetMaxOfferAmount()
        {
            EnsureCached();
            var settings = GameReflection.GetSettings();
            if (settings == null || _tradeRoutesConfigField == null) return 5;

            try
            {
                var config = _tradeRoutesConfigField.GetValue(settings);
                if (config == null || _configMaxOfferAmountField == null) return 5;
                return (int)_configMaxOfferAmountField.GetValue(config);
            }
            catch { return 5; }
        }

        private static string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return "Unknown";

            var settings = GameReflection.GetSettings();
            if (settings == null || _getGoodMethod == null) return goodName;

            try
            {
                var goodModel = _getGoodMethod.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return goodName;

                var locaText = _goodDisplayNameField?.GetValue(goodModel);
                return GameReflection.GetLocaText(locaText) ?? goodName;
            }
            catch { return goodName; }
        }

        private static string GetBiomeDisplayName(string biomeName)
        {
            if (string.IsNullOrEmpty(biomeName)) return "Unknown";

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsGetBiomeMethod == null) return biomeName;

            try
            {
                var biomeModel = _settingsGetBiomeMethod.Invoke(settings, new object[] { biomeName });
                if (biomeModel == null || _biomeDisplayNameField == null) return biomeName;

                var locaText = _biomeDisplayNameField.GetValue(biomeModel);
                return GameReflection.GetLocaText(locaText) ?? biomeName;
            }
            catch { return biomeName; }
        }

        private static string GetFactionDisplayName(string factionName)
        {
            // Faction can be empty/null for player towns
            if (string.IsNullOrEmpty(factionName)) return null;

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsGetFactionMethod == null) return null;

            try
            {
                var factionModel = _settingsGetFactionMethod.Invoke(settings, new object[] { factionName });
                if (factionModel == null || _factionDisplayNameField == null) return null;

                var locaText = _factionDisplayNameField.GetValue(factionModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch { return null; }
        }

        /// <summary>
        /// Resolve a localization key to its text value.
        /// Uses MainController.Instance.AppServices.TextsService.GetLocaText(key)
        /// </summary>
        private static string GetLocalizedText(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            try
            {
                var mainController = GameReflection.GetMainControllerInstance();
                if (mainController == null) return key;

                var appServicesProp = mainController.GetType().GetProperty("AppServices", GameReflection.PublicInstance);
                if (appServicesProp == null) return key;

                var appServices = appServicesProp.GetValue(mainController);
                if (appServices == null) return key;

                var textsServiceProp = appServices.GetType().GetProperty("TextsService", GameReflection.PublicInstance);
                if (textsServiceProp == null) return key;

                var textsService = textsServiceProp.GetValue(appServices);
                if (textsService == null) return key;

                var getLocaTextMethod = textsService.GetType().GetMethod("GetLocaText", new[] { typeof(string) });
                if (getLocaTextMethod == null) return key;

                var result = getLocaTextMethod.Invoke(textsService, new object[] { key }) as string;
                return result ?? key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetLocalizedText failed for key '{key}': {ex.Message}");
                return key;
            }
        }

        private static string FormatGood(object good)
        {
            if (good == null) return "";
            var name = GetGoodDisplayName(GetString(_goodNameField, good));
            var amount = GetInt(_goodAmountField, good);
            return $"{amount} {name}";
        }

        private static string GetString(FieldInfo field, object obj)
        {
            if (field == null || obj == null) return "";
            try { return field.GetValue(obj) as string ?? ""; }
            catch { return ""; }
        }

        private static int GetInt(FieldInfo field, object obj)
        {
            if (field == null || obj == null) return 0;
            try { return (int)field.GetValue(obj); }
            catch { return 0; }
        }

        private static float GetFloat(FieldInfo field, object obj)
        {
            if (field == null || obj == null) return 0f;
            try { return (float)field.GetValue(obj); }
            catch { return 0f; }
        }

        private static bool GetBool(FieldInfo field, object obj)
        {
            if (field == null || obj == null) return false;
            try { return (bool)field.GetValue(obj); }
            catch { return false; }
        }

        // ========================================
        // TIME FORMATTING
        // ========================================

        /// <summary>
        /// Format time in seconds to a readable string (e.g., "1:30").
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0:00";
            int totalSeconds = Mathf.RoundToInt(seconds);
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes}:{secs:D2}";
        }


        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(TradeRoutesReflection), "TradeRoutesReflection");
        }
    }
}
