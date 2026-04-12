using ATSAccessibility.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing trade routes service, popup detection, and data extraction.
	/// Provides methods for navigating towns, offers, routes, and performing actions like accept/collect.
	/// </summary>
	public static class TradeRoutesReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		public struct TownInfo {
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

		public struct OfferInfo {
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
			public BlockedReason BlockedReason;
		}

		public enum BlockedReason {
			None,
			AlreadyAccepted,
			LimitReached,
			NotEnoughGoods,
			NotEnoughFuel
		}

		public struct RouteInfo {
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

		// Popup type detection
		private static Type _tradeRoutesPopupType;

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

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("TradeRoutesReflection", assembly => {
				CacheServiceTypes(assembly);
				CacheStateTypes(assembly);
				CacheTradeRoutesServiceMethods(assembly);
				CacheGoodTypes(assembly);
				CacheSettingsTypes(assembly);
				_tradeRoutesPopupType = assembly.GetType("Eremite.View.HUD.TradeRoutes.TradeRoutesPopup");
			});
		}

		private static void CacheServiceTypes(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_gsTradeRoutesServiceProperty = gameServicesType.GetProperty("TradeRoutesService", GameReflection.PublicInstance);
				_gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
				_gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService", GameReflection.PublicInstance);
				_gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService", GameReflection.PublicInstance);
				_gsStorageServiceProperty = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
			}

			var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
			if (effectsServiceType != null) {
				_getTradeRoutesAmountMethod = effectsServiceType.GetMethod("GetTradeRoutesAmount", GameReflection.PublicInstance);
			}
		}

		private static void CacheStateTypes(Assembly assembly) {
			// IStateService
			var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
			if (stateServiceType != null) {
				_stateTradeProperty = stateServiceType.GetProperty("Trade", GameReflection.PublicInstance);
				_statePrefsProperty = stateServiceType.GetProperty("Prefs", GameReflection.PublicInstance);
			}

			// TradeState
			var tradeStateType = assembly.GetType("Eremite.Model.State.TradeState");
			if (tradeStateType != null) {
				_tradeTradeTownsField = tradeStateType.GetField("tradeTowns", GameReflection.PublicInstance);
				_tradeRoutesField = tradeStateType.GetField("routes", GameReflection.PublicInstance);
			}

			// PrefsState
			var prefsStateType = assembly.GetType("Eremite.Model.State.PrefsState");
			if (prefsStateType != null) {
				_prefsAutoCollectField = prefsStateType.GetField("autoCollectTradeRoutes", GameReflection.PublicInstance);
				_prefsOnlyAvailableField = prefsStateType.GetField("onlyAvailableTradeRoutes", GameReflection.PublicInstance);
			}

			// TradeTownState
			var townStateType = assembly.GetType("Eremite.Model.State.TradeTownState");
			if (townStateType != null) {
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
			if (offerStateType != null) {
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
			if (routeStateType != null) {
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

		private static void CacheTradeRoutesServiceMethods(Assembly assembly) {
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

		private static void CacheGoodTypes(Assembly assembly) {
			var goodType = assembly.GetType("Eremite.Model.Good");
			if (goodType != null) {
				_goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
				_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
			}
		}

		private static void CacheSettingsTypes(Assembly assembly) {
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_getGoodMethod = settingsType.GetMethod("GetGood", new[] { typeof(string) });
				_tradeCurrencyField = settingsType.GetField("tradeCurrency", GameReflection.PublicInstance);
				_tradeRoutesConfigField = settingsType.GetField("tradeRoutesConfig", GameReflection.PublicInstance);
			}

			var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
			if (goodModelType != null) {
				_goodDisplayNameField = goodModelType.GetField("displayName", GameReflection.PublicInstance);
			}

			var configType = assembly.GetType("Eremite.Model.Configs.TradeRoutesConfig");
			if (configType != null) {
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

		private static object GetTradeRoutesService() {
			EnsureCached();
			return GameReflection.GetService(_gsTradeRoutesServiceProperty);
		}

		private static object GetStateService() {
			EnsureCached();
			return GameReflection.GetService(_gsStateServiceProperty);
		}

		private static object GetEffectsService() {
			EnsureCached();
			return GameReflection.GetService(_gsEffectsServiceProperty);
		}

		private static object GetTradeState() {
			return ReflectionHelper.GetProp(_stateTradeProperty, GetStateService());
		}

		private static object GetPrefsState() {
			return ReflectionHelper.GetProp(_statePrefsProperty, GetStateService());
		}

		// ========================================
		// POPUP DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a TradeRoutesPopup.
		/// </summary>
		public static bool IsTradeRoutesPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			return _tradeRoutesPopupType != null && _tradeRoutesPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// TOGGLE SETTINGS
		// ========================================

		/// <summary>
		/// Check if auto-collect is enabled.
		/// </summary>
		public static bool IsAutoCollectEnabled() {
			return ReflectionHelper.GetBool(_prefsAutoCollectField, GetPrefsState());
		}

		/// <summary>
		/// Set auto-collect enabled state.
		/// </summary>
		public static void SetAutoCollect(bool enabled) {
			ReflectionHelper.SetField(_prefsAutoCollectField, GetPrefsState(), enabled);
		}

		/// <summary>
		/// Auto-collect all ready routes. Called when enabling auto-collect to match game behavior.
		/// Returns number of routes collected.
		/// </summary>
		public static int AutoCollectAllReady() {
			var routes = GetActiveRoutes();
			int collected = 0;

			foreach (var route in routes) {
				if (route.CanCollect && Collect(route.State)) {
					collected++;
				}
			}

			return collected;
		}

		/// <summary>
		/// Check if "only available" filter is enabled.
		/// </summary>
		public static bool IsOnlyAvailableEnabled() {
			return ReflectionHelper.GetBool(_prefsOnlyAvailableField, GetPrefsState());
		}

		/// <summary>
		/// Set "only available" filter enabled state.
		/// </summary>
		public static void SetOnlyAvailable(bool enabled) {
			ReflectionHelper.SetField(_prefsOnlyAvailableField, GetPrefsState(), enabled);
		}

		// ========================================
		// ROUTE LIMIT
		// ========================================

		/// <summary>
		/// Check if route limit has been reached.
		/// </summary>
		public static bool HasReachedLimit() {
			var service = GetTradeRoutesService();
			if (service == null || _hasReachedLimitMethod == null) return true;
			return ReflectionHelper.InvokeBool(_hasReachedLimitMethod, service);
		}

		/// <summary>
		/// Get the maximum number of active routes.
		/// </summary>
		public static int GetMaxRoutes() {
			return ReflectionHelper.InvokeInt(_getTradeRoutesAmountMethod, GetEffectsService());
		}

		// ========================================
		// DATA EXTRACTION - TOWNS
		// ========================================

		/// <summary>
		/// Get list of all trade towns.
		/// </summary>
		public static List<TownInfo> GetTradeTowns() {
			EnsureCached();
			var result = new List<TownInfo>();

			var tradeState = GetTradeState();
			if (tradeState == null || _tradeTradeTownsField == null) return result;

			try {
				var towns = ReflectionHelper.GetList(_tradeTradeTownsField, tradeState);
				if (towns == null) return result;

				var service = GetTradeRoutesService();

				foreach (var town in towns) {
					if (town == null) continue;

					// Handle hasStaticName - if true, townName is a localization key
					string townName = ReflectionHelper.GetString(_townNameField, town) ?? "";
					bool hasStaticName = ReflectionHelper.GetBool(_townHasStaticNameField, town);
					if (hasStaticName && !string.IsNullOrEmpty(townName)) {
						townName = GetLocalizedText(townName) ?? townName;
					}

					var info = new TownInfo {
						State = town,
						Id = ReflectionHelper.GetInt(_townIdField, town),
						Name = townName,
						Biome = GetBiomeDisplayName(ReflectionHelper.GetString(_townBiomeField, town) ?? ""),
						Faction = GetFactionDisplayName(ReflectionHelper.GetString(_townFactionField, town)),
						Distance = ReflectionHelper.GetInt(_townDistanceField, town),
						StandingLevel = ReflectionHelper.GetInt(_townStandingLevelField, town),
						IsMaxStanding = ReflectionHelper.GetBool(_townIsMaxStandingField, town),
						CurrentStandingValue = ReflectionHelper.GetInt(_townCurrentStandingField, town),
						ValueForLevelUp = ReflectionHelper.GetInt(_townValueForLevelUpField, town),
						OfferCount = GetOfferCount(town),
						StandingLabel = GetStandingLabel(service, town),
						CanExtend = CanExtendOffer(service, town),
						ReachedMaxOffers = HasReachedMaxOffers(service, town),
						ExtendCost = GetExtendCost(service, town)
					};

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetTradeTowns failed: {ex.Message}");
			}

			return result;
		}

		private static int GetOfferCount(object town) {
			var offers = ReflectionHelper.GetList(_townOffersField, town);
			return offers?.Count ?? 0;
		}

		private static string GetStandingLabel(object service, object town) {
			return ReflectionHelper.InvokeString(_getStandingLabelForMethod, service, town) ?? "Unknown";
		}

		private static bool CanExtendOffer(object service, object town) {
			return ReflectionHelper.InvokeBool(_canExtendOfferMethod, service, town);
		}

		private static bool HasReachedMaxOffers(object service, object town) {
			return ReflectionHelper.InvokeBool(_reachedMaxOffersMethod, service, town);
		}

		private static string GetExtendCost(object service, object town) {
			var good = ReflectionHelper.Invoke(_getOfferExtendingPriceMethod, service, town);
			if (good == null) return "";
			return FormatGood(good);
		}

		// ========================================
		// DATA EXTRACTION - OFFERS
		// ========================================

		/// <summary>
		/// Get offers for a specific town.
		/// </summary>
		public static List<OfferInfo> GetTownOffers(object townState) {
			EnsureCached();
			var result = new List<OfferInfo>();

			if (townState == null || _townOffersField == null) return result;

			try {
				var offers = ReflectionHelper.GetList(_townOffersField, townState);
				if (offers == null) return result;

				var service = GetTradeRoutesService();
				bool onlyAvailable = IsOnlyAvailableEnabled();

				foreach (var offer in offers) {
					if (offer == null) continue;

					// Skip if only available filter is on and this offer can't be accepted
					if (onlyAvailable && !CanAcceptAnyAmount(service, offer))
						continue;

					var info = BuildOfferInfo(service, offer);
					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetTownOffers failed: {ex.Message}");
			}

			return result;
		}

		private static OfferInfo BuildOfferInfo(object service, object offer) {
			// Handle hasStaticName for offer town name
			string offerTownName = ReflectionHelper.GetString(_offerTownNameField, offer) ?? "";
			bool hasStaticName = ReflectionHelper.GetBool(_offerHasStaticNameField, offer);
			if (hasStaticName && !string.IsNullOrEmpty(offerTownName)) {
				offerTownName = GetLocalizedText(offerTownName) ?? offerTownName;
			}

			var info = new OfferInfo {
				State = offer,
				TownId = ReflectionHelper.GetInt(_offerTownIdField, offer),
				TownName = offerTownName,
				Multiplier = ReflectionHelper.GetInt(_offerAmountField, offer),
				MaxMultiplier = GetMaxOfferAmount(),
				Accepted = ReflectionHelper.GetBool(_offerAcceptedField, offer)
			};

			// Base good (per unit)
			var baseGood = ReflectionHelper.GetField(_offerGoodField, offer);
			if (baseGood != null) {
				info.GoodName = GetGoodDisplayName(ReflectionHelper.GetString(_goodNameField, baseGood) ?? "");
				info.GoodAmount = ReflectionHelper.GetInt(_goodAmountField, baseGood);
			}

			// Full calculations from service
			if (service != null) {
				// Full fuel - use service method, fallback to config-based calculation
				var fullFuel = GetFullFuel(service, offer);
				if (fullFuel != null) {
					info.FuelName = GetGoodDisplayName(ExtractGoodName(fullFuel));
					info.FuelAmount = ExtractGoodAmount(fullFuel);
				}

				// Fallback: Get fuel from config if service call returned empty
				if (string.IsNullOrEmpty(info.FuelName)) {
					info.FuelName = GetFuelGoodName();
					int baseFuel = ReflectionHelper.GetInt(_offerFuelField, offer);
					info.FuelAmount = baseFuel * info.Multiplier;
				}

				// Full price
				var fullPrice = GetFullPrice(service, offer);
				if (fullPrice != null) {
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
		private static string ExtractGoodName(object goodObj) {
			return ReflectionHelper.GetString(_goodNameField, goodObj) ?? "";
		}

		/// <summary>
		/// Extract the amount from a boxed Good struct.
		/// </summary>
		private static int ExtractGoodAmount(object goodObj) {
			return ReflectionHelper.GetInt(_goodAmountField, goodObj);
		}

		/// <summary>
		/// Get the fuel good name from the trade routes config.
		/// </summary>
		private static string GetFuelGoodName() {
			EnsureCached();
			var settings = GameReflection.GetSettings();
			if (settings == null || _tradeRoutesConfigField == null) return Strings.Get("reflection.traderoutes.provisions");

			var config = ReflectionHelper.GetField(_tradeRoutesConfigField, settings);
			if (config == null) return Strings.Get("reflection.traderoutes.provisions");

			var fuelGoodModel = ReflectionHelper.GetField(_configFuelField, config);
			if (fuelGoodModel == null) return Strings.Get("reflection.traderoutes.provisions");

			// Get the Name property from the GoodModel
			var nameProperty = fuelGoodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
			if (nameProperty == null) return Strings.Get("reflection.traderoutes.provisions");

			var name = ReflectionHelper.GetPropString(nameProperty, fuelGoodModel);
			return GetGoodDisplayName(name ?? Strings.Get("reflection.traderoutes.provisions"));
		}

		private static BlockedReason GetBlockedReason(object service, object offer, bool accepted) {
			if (accepted) return BlockedReason.AlreadyAccepted;
			if (HasReachedLimit()) return BlockedReason.LimitReached;
			if (!HaveEnoughGoods(service, offer)) return BlockedReason.NotEnoughGoods;
			if (!HaveEnoughFuel(service, offer)) return BlockedReason.NotEnoughFuel;
			return BlockedReason.None;
		}

		private static bool CanAccept(object service, object offer) {
			return ReflectionHelper.InvokeBool(_canAcceptMethod, service, offer);
		}

		private static bool CanAcceptAnyAmount(object service, object offer) {
			return ReflectionHelper.InvokeBool(_canAcceptAnyAmountMethod, service, offer);
		}

		private static bool HaveEnoughGoods(object service, object offer) {
			return ReflectionHelper.InvokeBool(_haveEnoughGoodsForMethod, service, offer);
		}

		private static bool HaveEnoughFuel(object service, object offer) {
			return ReflectionHelper.InvokeBool(_haveEnoughFuelForMethod, service, offer);
		}

		private static object GetFullFuel(object service, object offer) {
			return ReflectionHelper.Invoke(_getFullFuelMethod, service, offer);
		}

		private static object GetFullPrice(object service, object offer) {
			return ReflectionHelper.Invoke(_getFullPriceMethod, service, offer);
		}

		private static float GetFullTravelTime(object service, object offer) {
			return ReflectionHelper.InvokeFloat(_getFullTravelTimeMethod, service, offer);
		}

		// ========================================
		// DATA EXTRACTION - ACTIVE ROUTES
		// ========================================

		/// <summary>
		/// Get list of active routes.
		/// </summary>
		public static List<RouteInfo> GetActiveRoutes() {
			EnsureCached();
			var result = new List<RouteInfo>();

			var tradeState = GetTradeState();
			if (tradeState == null || _tradeRoutesField == null) return result;

			try {
				var routes = ReflectionHelper.GetList(_tradeRoutesField, tradeState);
				if (routes == null) return result;

				var service = GetTradeRoutesService();

				foreach (var route in routes) {
					if (route == null) continue;

					// Handle hasStaticName for route town name
					string routeTownName = ReflectionHelper.GetString(_routeTownNameField, route) ?? "";
					bool hasStaticName = ReflectionHelper.GetBool(_routeHasStaticNameField, route);
					if (hasStaticName && !string.IsNullOrEmpty(routeTownName)) {
						routeTownName = GetLocalizedText(routeTownName) ?? routeTownName;
					}

					var info = new RouteInfo {
						State = route,
						TownId = ReflectionHelper.GetInt(_routeTownIdField, route),
						TownName = routeTownName,
						Progress = ReflectionHelper.GetFloat(_routeProgressField, route)
					};

					// Good
					var good = ReflectionHelper.GetField(_routeGoodField, route);
					if (good != null) {
						info.GoodName = GetGoodDisplayName(ReflectionHelper.GetString(_goodNameField, good) ?? "");
						info.GoodAmount = ReflectionHelper.GetInt(_goodAmountField, good);
					}

					// Price (reward)
					var price = ReflectionHelper.GetField(_routePriceField, route);
					if (price != null) {
						info.PriceName = GetGoodDisplayName(ReflectionHelper.GetString(_goodNameField, price) ?? "");
						info.PriceAmount = ReflectionHelper.GetInt(_goodAmountField, price);
					}

					// Calculate time remaining
					float travelTime = ReflectionHelper.GetFloat(_routeTravelTimeField, route);
					float progress = info.Progress;
					if (progress < 1f && travelTime > 0) {
						info.TimeRemaining = travelTime * (1f - progress);
					}

					// Can collect
					info.CanCollect = CanCollect(service, route);

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetActiveRoutes failed: {ex.Message}");
			}

			return result;
		}

		private static bool CanCollect(object service, object route) {
			return ReflectionHelper.InvokeBool(_canCollectMethod, service, route);
		}

		// ========================================
		// ACTIONS
		// ========================================

		/// <summary>
		/// Collect a completed route.
		/// </summary>
		public static bool Collect(object routeState) {
			return ReflectionHelper.InvokeVoid(_collectMethod, GetTradeRoutesService(), routeState);
		}

		/// <summary>
		/// Accept a trade offer.
		/// </summary>
		public static bool AcceptOffer(object offerState) {
			return ReflectionHelper.InvokeVoid(_acceptOfferMethod, GetTradeRoutesService(), offerState);
		}

		/// <summary>
		/// Extend offers for a town (adds one more offer slot).
		/// </summary>
		public static bool ExtendOffer(object townState) {
			return ReflectionHelper.InvokeVoid(_extendOfferMethod, GetTradeRoutesService(), townState);
		}

		/// <summary>
		/// Set the offer amount (multiplier 1-5).
		/// </summary>
		public static bool SetOfferAmount(object offerState, int amount) {
			int maxAmount = GetMaxOfferAmount();
			amount = Mathf.Clamp(amount, 1, maxAmount);
			return ReflectionHelper.SetField(_offerAmountField, offerState, amount);
		}

		/// <summary>
		/// Get the current offer amount (multiplier).
		/// </summary>
		public static int GetOfferAmount(object offerState) {
			if (offerState == null || _offerAmountField == null) return 1;
			return ReflectionHelper.GetInt(_offerAmountField, offerState);
		}

		// ========================================
		// HELPER METHODS
		// ========================================

		private static int GetMaxOfferAmount() {
			EnsureCached();
			var settings = GameReflection.GetSettings();
			if (settings == null || _tradeRoutesConfigField == null) return 5;

			var config = ReflectionHelper.GetField(_tradeRoutesConfigField, settings);
			if (config == null || _configMaxOfferAmountField == null) return 5;
			var val = ReflectionHelper.GetInt(_configMaxOfferAmountField, config);
			return val > 0 ? val : 5;
		}

		private static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return "";

			var settings = GameReflection.GetSettings();
			var goodModel = ReflectionHelper.Invoke(_getGoodMethod, settings, goodName);
			if (goodModel == null) return goodName;

			return ReflectionHelper.GetLocaString(_goodDisplayNameField, goodModel) ?? goodName;
		}

		private static string GetBiomeDisplayName(string biomeName) {
			if (string.IsNullOrEmpty(biomeName)) return "Unknown";

			var settings = GameReflection.GetSettings();
			var biomeModel = ReflectionHelper.Invoke(_settingsGetBiomeMethod, settings, biomeName);
			if (biomeModel == null) return biomeName;

			return ReflectionHelper.GetLocaString(_biomeDisplayNameField, biomeModel) ?? biomeName;
		}

		private static string GetFactionDisplayName(string factionName) {
			// Faction can be empty/null for player towns
			if (string.IsNullOrEmpty(factionName)) return null;

			var settings = GameReflection.GetSettings();
			var factionModel = ReflectionHelper.Invoke(_settingsGetFactionMethod, settings, factionName);
			if (factionModel == null) return null;

			return ReflectionHelper.GetLocaString(_factionDisplayNameField, factionModel);
		}

		/// <summary>
		/// Resolve a localization key to its text value.
		/// Uses MainController.Instance.AppServices.TextsService.GetLocaText(key)
		/// </summary>
		private static string GetLocalizedText(string key) {
			if (string.IsNullOrEmpty(key)) return key;

			try {
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
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetLocalizedText failed for key '{key}': {ex.Message}");
				return key;
			}
		}

		private static string FormatGood(object good) {
			if (good == null) return "";
			var name = GetGoodDisplayName(ReflectionHelper.GetString(_goodNameField, good) ?? "");
			var amount = ReflectionHelper.GetInt(_goodAmountField, good);
			return $"{amount} {name}";
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(TradeRoutesReflection), "TradeRoutesReflection");
		}
	}
}
