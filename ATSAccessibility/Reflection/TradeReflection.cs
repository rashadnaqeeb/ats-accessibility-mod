using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing trade service and trader panel data.
	/// Provides methods to query trader state, goods, perks, and execute trades.
	/// </summary>
	public static class TradeReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		public struct TradingGoodInfo {
			public string Name;           // Good name/key
			public string DisplayName;    // Localized display name
			public int StorageAmount;     // Amount in storage (not offered)
			public int OfferedAmount;     // Amount being offered
			public float UnitValue;       // Amber per unit (sell or buy)
		}

		public struct PerkInfo {
			public string Name;           // Effect name/key
			public string DisplayName;    // Localized display name
			public string Description;    // Effect description
			public float Price;           // Amber cost
			public bool Discounted;       // Whether has discount
			public float DiscountRatio;   // Price ratio (e.g., 0.8 = 20% off)
			public bool Sold;             // Whether already purchased
			public object EffectState;    // TraderEffectState for purchase
		}

		public struct AssaultResult {
			public bool Success;
			public int GoodsStolen;
			public int PerksStolen;
			public int VillagersLost;
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// Popup type detection
		private static Type _traderPanelType;

		// IGameServices service properties
		private static PropertyInfo _gsTradeServiceProperty = null;
		private static PropertyInfo _gsStorageServiceProperty = null;
		private static PropertyInfo _gsCalendarServiceProperty = null;

		// ICalendarService
		private static PropertyInfo _calendarSeasonProperty = null;
		private static MethodInfo _getTimeTillNextSeasonChangeMethod = null;

		// ITradeService methods
		private static MethodInfo _isMainTraderInTheVillageMethod = null;
		private static MethodInfo _getCurrentMainVisitMethod = null;
		private static MethodInfo _getCurrentMainTraderMethod = null;
		private static MethodInfo _getNextMainTraderMethod = null;
		private static MethodInfo _getVillageOfferMethod = null;
		private static MethodInfo _getTimeLeftToMethod = null;
		private static MethodInfo _getStayingTimeLeftMethod = null;
		private static MethodInfo _canForceArrivalMethod = null;
		private static MethodInfo _getForceArrivalPriceMethod = null;
		private static MethodInfo _forceArrivalMethod = null;
		private static MethodInfo _isTradingBlockedMethod = null;
		private static MethodInfo _isStormTooCloseToForceMethod = null;
		private static MethodInfo _canPayForceArrivalPriceMethod = null;
		private static MethodInfo _hasAnyTradePostMethod = null;
		private static MethodInfo _getValueInCurrencyMethod = null;
		private static MethodInfo _getBuyValueInCurrencyMethod = null;
		private static MethodInfo _getValueInCurrencyGoodNameMethod = null;
		private static MethodInfo _getBuyValueInCurrencyGoodNameMethod = null;
		private static MethodInfo _getValueInCurrencyEffectStateMethod = null;
		private static MethodInfo _completeTradeMethod = null;
		private static MethodInfo _completeTradeEffectMethod = null;
		private static MethodInfo _assaultTraderMethod = null;

		// IStorageService methods
		private static MethodInfo _getAmountMethod = null;

		// TraderVisitState fields
		private static FieldInfo _visitGoodsField = null;
		private static FieldInfo _visitOfferedEffectsField = null;
		private static FieldInfo _visitTravelProgressField = null;
		private static FieldInfo _visitForcedField = null;
		private static FieldInfo _visitTraderField = null;

		// TraderModel fields
		private static FieldInfo _traderDisplayNameField = null;
		private static FieldInfo _traderDescriptionField = null;
		private static FieldInfo _traderDialogueField = null;
		private static FieldInfo _traderLabelField = null;
		private static FieldInfo _traderCanAssaultField = null;
		private static FieldInfo _traderTransactionSoundField = null;

		// SoundRef method
		private static MethodInfo _soundRefGetNextMethod = null;

		// LabelModel fields
		private static FieldInfo _labelDisplayNameField = null;

		// LocaText (delegated to GameReflection.GetLocaText)

		// Good struct fields
		private static FieldInfo _goodNameField = null;
		private static FieldInfo _goodAmountField = null;

		// TradingGood fields
		private static FieldInfo _tradingGoodNameField = null;
		private static FieldInfo _tradingGoodStorageAmountField = null;
		private static FieldInfo _tradingGoodOfferedAmountField = null;

		// TraderEffectState fields
		private static FieldInfo _effectStateEffectField = null;
		private static FieldInfo _effectStateSoldField = null;
		private static FieldInfo _effectStateDiscountedField = null;
		private static FieldInfo _effectStatePriceRatioField = null;

		// TradingOffer
		private static Type _tradingOfferType = null;
		private static ConstructorInfo _tradingOfferCtor = null;
		private static FieldInfo _tradingOfferGoodsField = null;

		// EffectModel properties (use properties, not fields, to get formatted values)
		private static PropertyInfo _effectDisplayNameProperty = null;
		private static PropertyInfo _effectDescriptionProperty = null;

		// Good type and constructor for ExecuteTrade
		private static Type _goodType = null;
		private static ConstructorInfo _goodCtor = null;

		// Settings access for good models
		private static MethodInfo _getGoodMethod = null;
		private static MethodInfo _getEffectMethod = null;
		private static MethodInfo _getTraderFromSettingsMethod = null;
		private static FieldInfo _goodDisplayNameField = null;
		private static FieldInfo _tradeCurrencyField = null;

		// TraderAssaultResult fields
		private static FieldInfo _assaultVillagersKilledField = null;

		// EffectsService for assault check
		private static PropertyInfo _gsEffectsServiceProperty = null;
		private static MethodInfo _canAttackTraderMethod = null;

		// GameBlackboardService for assault result popup
		private static PropertyInfo _gsGameBlackboardServiceProperty = null;
		private static PropertyInfo _assaultResultPopupRequestedProperty = null;

		// TraderPanel for closing after assault
		private static PropertyInfo _traderPanelInstanceProperty = null;
		private static MethodInfo _traderPanelHideMethod = null;

		// Glade trader support: panel visit field and isExtra flag
		private static FieldInfo _panelVisitField = null;
		private static FieldInfo _visitIsExtraField = null;
		private static object _currentTraderPanel = null;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("TradeReflection", assembly => {
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType == null) return;

				// Cache service property accessors
				_gsTradeServiceProperty = gameServicesType.GetProperty("TradeService");
				_gsStorageServiceProperty = gameServicesType.GetProperty("StorageService");
				_gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService");
				_gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService");

				// ICalendarService
				var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
				if (calendarServiceType != null) {
					_calendarSeasonProperty = calendarServiceType.GetProperty("Season");
					_getTimeTillNextSeasonChangeMethod = calendarServiceType.GetMethod("GetTimeTillNextSeasonChange");
				}

				// ITradeService methods
				var tradeServiceType = assembly.GetType("Eremite.Services.ITradeService");
				if (tradeServiceType != null) {
					_isMainTraderInTheVillageMethod = tradeServiceType.GetMethod("IsMainTraderInTheVillage");
					_getCurrentMainVisitMethod = tradeServiceType.GetMethod("GetCurrentMainVisit");
					_getCurrentMainTraderMethod = tradeServiceType.GetMethod("GetCurrentMainTrader");
					_getNextMainTraderMethod = tradeServiceType.GetMethod("GetNextMainTrader");
					_getVillageOfferMethod = tradeServiceType.GetMethod("GetVillageOffer");
					_getTimeLeftToMethod = tradeServiceType.GetMethod("GetTimeLeftTo");
					_getStayingTimeLeftMethod = tradeServiceType.GetMethod("GetStayingTimeLeft");
					_canForceArrivalMethod = tradeServiceType.GetMethod("CanForceArrival");
					_getForceArrivalPriceMethod = tradeServiceType.GetMethod("GetForceArrivalPrice");
					_forceArrivalMethod = tradeServiceType.GetMethod("ForceArrival");
					_isTradingBlockedMethod = tradeServiceType.GetMethod("IsTradingBlocked");
					_isStormTooCloseToForceMethod = tradeServiceType.GetMethod("IsStormTooCloseToForce");
					_canPayForceArrivalPriceMethod = tradeServiceType.GetMethod("CanPayForceArrivalPrice");
					_hasAnyTradePostMethod = tradeServiceType.GetMethod("HasAnyTradePost");
					_assaultTraderMethod = tradeServiceType.GetMethod("AssaultTrader");

					// Value calculation methods - need to find the right overloads
					var methods = tradeServiceType.GetMethods();
					foreach (var m in methods) {
						if (m.Name == "GetValueInCurrency") {
							var ps = m.GetParameters();
							if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(int))
								_getValueInCurrencyGoodNameMethod = m;
							else if (ps.Length == 1 && ps[0].ParameterType.Name == "TradingOffer")
								_getValueInCurrencyMethod = m;
							else if (ps.Length == 1 && ps[0].ParameterType.Name == "TraderEffectState")
								_getValueInCurrencyEffectStateMethod = m;
						} else if (m.Name == "GetBuyValueInCurrency") {
							var ps = m.GetParameters();
							if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(int))
								_getBuyValueInCurrencyGoodNameMethod = m;
							else if (ps.Length == 1 && ps[0].ParameterType.Name == "TradingOffer")
								_getBuyValueInCurrencyMethod = m;
						} else if (m.Name == "CompleteTrade") {
							var ps = m.GetParameters();
							if (ps.Length == 3)
								_completeTradeMethod = m;
							else if (ps.Length == 2 && ps[1].ParameterType.Name == "TraderEffectState")
								_completeTradeEffectMethod = m;
						}
					}
				}

				// IStorageService methods
				var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
				if (storageServiceType != null) {
					_getAmountMethod = storageServiceType.GetMethod("GetAmount", new[] { typeof(string) });
				}

				// TraderVisitState fields
				var visitStateType = assembly.GetType("Eremite.Model.State.TraderVisitState");
				if (visitStateType != null) {
					_visitGoodsField = visitStateType.GetField("goods");
					_visitOfferedEffectsField = visitStateType.GetField("offeredEffects");
					_visitTravelProgressField = visitStateType.GetField("travelProgress");
					_visitForcedField = visitStateType.GetField("forced");
					_visitIsExtraField = visitStateType.GetField("isExtra");
					_visitTraderField = visitStateType.GetField("trader");
				}

				// TraderModel fields
				var traderModelType = assembly.GetType("Eremite.Model.Trade.TraderModel");
				if (traderModelType != null) {
					_traderDisplayNameField = traderModelType.GetField("displayName");
					_traderDescriptionField = traderModelType.GetField("description");
					_traderDialogueField = traderModelType.GetField("dialogue");
					_traderLabelField = traderModelType.GetField("label");
					_traderCanAssaultField = traderModelType.GetField("canAssault");
					_traderTransactionSoundField = traderModelType.GetField("transactionSound");
				}

				// SoundRef GetNext method
				var soundRefType = assembly.GetType("Eremite.Model.Sound.SoundRef");
				if (soundRefType != null) {
					_soundRefGetNextMethod = soundRefType.GetMethod("GetNext");
				}

				// LabelModel fields
				var labelModelType = assembly.GetType("Eremite.Model.LabelModel");
				if (labelModelType != null) {
					_labelDisplayNameField = labelModelType.GetField("displayName");
				}

				// Good struct (LocaText delegated to GameReflection.GetLocaText)
				_goodType = assembly.GetType("Eremite.Model.Good");
				if (_goodType != null) {
					_goodNameField = _goodType.GetField("name");
					_goodAmountField = _goodType.GetField("amount");
					_goodCtor = _goodType.GetConstructor(new[] { typeof(string), typeof(int) });
				}

				// TradingGood
				var tradingGoodType = assembly.GetType("Eremite.Model.Trade.TradingGood");
				if (tradingGoodType != null) {
					// It's a readonly field, try different access patterns
					_tradingGoodNameField = tradingGoodType.GetField("goodName", BindingFlags.Public | BindingFlags.Instance);
					if (_tradingGoodNameField == null) {
						foreach (var f in tradingGoodType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
							if (f.Name == "goodName" || f.Name == "<goodName>k__BackingField") {
								_tradingGoodNameField = f;
								break;
							}
						}
					}
					_tradingGoodStorageAmountField = tradingGoodType.GetField("storageAmount");
					_tradingGoodOfferedAmountField = tradingGoodType.GetField("offeredAmount");
				}

				// TraderEffectState
				var effectStateType = assembly.GetType("Eremite.Model.State.TraderEffectState");
				if (effectStateType != null) {
					_effectStateEffectField = effectStateType.GetField("effect");
					_effectStateSoldField = effectStateType.GetField("sold");
					_effectStateDiscountedField = effectStateType.GetField("discounted");
					_effectStatePriceRatioField = effectStateType.GetField("priceRatio");
				}

				// TradingOffer
				_tradingOfferType = assembly.GetType("Eremite.Model.Trade.TradingOffer");
				if (_tradingOfferType != null) {
					var goodArrayType = assembly.GetType("Eremite.Model.Good").MakeArrayType();
					_tradingOfferCtor = _tradingOfferType.GetConstructor(new[] { goodArrayType });
					_tradingOfferGoodsField = _tradingOfferType.GetField("goods");
				}

				// EffectModel
				var effectModelType = assembly.GetType("Eremite.Model.Effects.EffectModel");
				if (effectModelType == null)
					effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					// Use properties to get formatted display name and description
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
				}

				// Settings access (use GameReflection.GetSettings() for MB.Settings)
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_getGoodMethod = settingsType.GetMethod("GetGood", new[] { typeof(string) });
					_getEffectMethod = settingsType.GetMethod("GetEffect", new[] { typeof(string) });
					_getTraderFromSettingsMethod = settingsType.GetMethod("GetTrader", new[] { typeof(string) });
					_tradeCurrencyField = settingsType.GetField("tradeCurrency");
				}

				var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
				if (goodModelType != null) {
					_goodDisplayNameField = goodModelType.GetField("displayName");
				}

				// TraderAssaultResult
				var assaultResultType = assembly.GetType("Eremite.Services.TraderAssaultResult");
				if (assaultResultType != null) {
					_assaultVillagersKilledField = assaultResultType.GetField("villagersKilled");
				}

				// EffectsService for assault check
				var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_canAttackTraderMethod = effectsServiceType.GetMethod("CanAttackTrader");
				}

				// GameBlackboardService for triggering assault result popup
				_gsGameBlackboardServiceProperty = gameServicesType.GetProperty("GameBlackboardService");
				var blackboardServiceType = assembly.GetType("Eremite.Services.IGameBlackboardService");
				if (blackboardServiceType != null) {
					_assaultResultPopupRequestedProperty = blackboardServiceType.GetProperty("TraderAssaultResultPopupRequested");
				}

				// TraderPanel for closing after assault and popup detection
				_traderPanelType = assembly.GetType("Eremite.Buildings.UI.Trade.TraderPanel");
				var traderPanelType = _traderPanelType;
				if (traderPanelType != null) {
					_traderPanelInstanceProperty = traderPanelType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
					_traderPanelHideMethod = traderPanelType.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
				}
			});
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		private static object GetTradeService() {
			EnsureCached();
			return GameReflection.GetService(_gsTradeServiceProperty);
		}

		private static object GetStorageService() {
			EnsureCached();
			return GameReflection.GetService(_gsStorageServiceProperty);
		}

		private static object GetSettings() {
			// Use GameReflection's method which has the correct type and binding flags
			return GameReflection.GetSettings();
		}

		private static string GetLocaText(object locaText) {
			return GameReflection.GetLocaText(locaText);
		}

		// ========================================
		// POPUP DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a TraderPanel.
		/// </summary>
		public static bool IsTraderPanel(object popup) {
			if (popup == null) return false;
			EnsureCached();
			return _traderPanelType != null && _traderPanelType.IsInstanceOfType(popup);
		}

		// ========================================
		// GLADE TRADER PANEL SUPPORT
		// ========================================

		/// <summary>
		/// Store the current TraderPanel instance so we can read its visit field.
		/// Called when the trader popup opens.
		/// </summary>
		public static void SetCurrentPanel(object traderPanel) {
			_currentTraderPanel = traderPanel;
			if (traderPanel != null && _panelVisitField == null) {
				_panelVisitField = traderPanel.GetType().GetField("visit",
					BindingFlags.NonPublic | BindingFlags.Instance);
			}
		}

		/// <summary>
		/// Clear the stored TraderPanel reference. Called when the trader popup closes.
		/// </summary>
		public static void ClearCurrentPanel() {
			_currentTraderPanel = null;
		}

		private static object GetPanelVisit() {
			return ReflectionHelper.GetField(_panelVisitField, _currentTraderPanel);
		}

		private static bool IsVisitExtra(object visit) {
			return ReflectionHelper.GetBool(_visitIsExtraField, visit);
		}

		/// <summary>
		/// Resolve a TraderModel from a visit's trader key string via Settings.GetTrader().
		/// Matches what the game's TraderPanel.GetCurrentTrader() does.
		/// </summary>
		private static object GetTraderFromVisit(object visit) {
			var traderKey = ReflectionHelper.GetString(_visitTraderField, visit);
			if (string.IsNullOrEmpty(traderKey)) return null;
			var settings = GetSettings();
			if (settings == null) return null;
			return ReflectionHelper.Invoke(_getTraderFromSettingsMethod, settings, traderKey);
		}

		/// <summary>
		/// Get the TraderModel for the currently active trader.
		/// Prefers the panel visit (correct for glade traders), falls back to main trader.
		/// </summary>
		private static object GetCurrentTrader() {
			// Try panel visit first — works for both main and glade traders
			var panelTrader = GetTraderFromVisit(GetPanelVisit());
			if (panelTrader != null) return panelTrader;

			// Fall back to main trader
			var tradeService = GetTradeService();
			if (tradeService == null) return null;
			return ReflectionHelper.Invoke(_getCurrentMainTraderMethod, tradeService);
		}

		// ========================================
		// TRADER STATE
		// ========================================

		/// <summary>
		/// Check if a trader is currently in the village.
		/// Also returns true for glade event traders (isExtra visits).
		/// </summary>
		public static bool IsTraderPresent() {
			var tradeService = GetTradeService();
			if (tradeService == null) return false;

			if (ReflectionHelper.InvokeBool(_isMainTraderInTheVillageMethod, tradeService))
				return true;

			// Glade event traders have isExtra = true on their visit
			if (IsVisitExtra(GetPanelVisit())) return true;

			return false;
		}

		/// <summary>
		/// Check if trading is blocked (e.g., by effects).
		/// </summary>
		public static bool IsTradingBlocked() {
			var tradeService = GetTradeService();
			if (tradeService == null) return true;
			return ReflectionHelper.InvokeBool(_isTradingBlockedMethod, tradeService);
		}

		/// <summary>
		/// Get the current trader visit state.
		/// </summary>
		public static object GetCurrentVisit() {
			// Prefer the panel's visit — correct for both main and glade traders
			var panelVisit = GetPanelVisit();
			if (panelVisit != null) return panelVisit;

			var tradeService = GetTradeService();
			if (tradeService == null) return null;
			return ReflectionHelper.Invoke(_getCurrentMainVisitMethod, tradeService);
		}

		/// <summary>
		/// Get the trader travel progress (0-1).
		/// </summary>
		public static float GetTravelProgress() {
			var visit = GetCurrentVisit();
			if (visit == null) return 0f;
			return ReflectionHelper.GetFloat(_visitTravelProgressField, visit);
		}

		// ========================================
		// TRADER INFO
		// ========================================

		/// <summary>
		/// Get the current trader's display name.
		/// </summary>
		public static string GetTraderName() {
			object trader;
			if (IsTraderPresent()) {
				trader = GetCurrentTrader();
			} else {
				var tradeService = GetTradeService();
				if (tradeService == null) return null;
				trader = ReflectionHelper.Invoke(_getNextMainTraderMethod, tradeService);
			}

			return ReflectionHelper.GetLocaString(_traderDisplayNameField, trader);
		}

		/// <summary>
		/// Get the trader's label (category like "General Goods").
		/// </summary>
		public static string GetTraderLabel() {
			object trader;
			if (IsTraderPresent()) {
				trader = GetCurrentTrader();
			} else {
				var tradeService = GetTradeService();
				if (tradeService == null) return null;
				trader = ReflectionHelper.Invoke(_getNextMainTraderMethod, tradeService);
			}

			if (trader == null) return null;

			var label = ReflectionHelper.GetField(_traderLabelField, trader);
			return ReflectionHelper.GetLocaString(_labelDisplayNameField, label);
		}

		/// <summary>
		/// Get the trader's description.
		/// </summary>
		public static string GetTraderDescription() {
			object trader;
			if (IsTraderPresent()) {
				trader = GetCurrentTrader();
			} else {
				var tradeService = GetTradeService();
				if (tradeService == null) return null;
				trader = ReflectionHelper.Invoke(_getNextMainTraderMethod, tradeService);
			}

			return ReflectionHelper.GetLocaString(_traderDescriptionField, trader);
		}

		/// <summary>
		/// Get the trader's dialogue text.
		/// </summary>
		public static string GetTraderDialogue() {
			var trader = GetCurrentTrader();
			return ReflectionHelper.GetLocaString(_traderDialogueField, trader);
		}

		/// <summary>
		/// Get the current trader's transaction sound model (for trader-specific sound).
		/// Returns null if not available.
		/// </summary>
		public static object GetTraderTransactionSound() {
			var trader = GetCurrentTrader();
			if (trader == null) return null;

			var soundRef = ReflectionHelper.GetField(_traderTransactionSoundField, trader);
			if (soundRef == null) return null;

			// Call GetNext() on the SoundRef to get the SoundModel
			return ReflectionHelper.Invoke(_soundRefGetNextMethod, soundRef);
		}

		/// <summary>
		/// Get the time remaining until trader arrives (in seconds).
		/// </summary>
		public static float GetTimeToArrival() {
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) return -1f;
			var result = ReflectionHelper.Invoke(_getTimeLeftToMethod, tradeService, visit);
			return result is float f ? f : -1f;
		}

		/// <summary>
		/// Get the time remaining that trader will stay (in seconds).
		/// </summary>
		public static float GetStayingTimeLeft() {
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) return -1f;
			var result = ReflectionHelper.Invoke(_getStayingTimeLeftMethod, tradeService, visit);
			return result is float f ? f : -1f;
		}

		// ========================================
		// FORCE ARRIVAL
		// ========================================

		/// <summary>
		/// Check if trader arrival can be forced.
		/// </summary>
		public static bool CanForceArrival() {
			var tradeService = GetTradeService();
			if (tradeService == null) return false;
			return ReflectionHelper.InvokeBool(_canForceArrivalMethod, tradeService);
		}

		/// <summary>
		/// Get the impatience cost to force trader arrival.
		/// </summary>
		public static float GetForceArrivalCost() {
			var tradeService = GetTradeService();
			if (tradeService == null) return 0f;
			return ReflectionHelper.InvokeFloat(_getForceArrivalPriceMethod, tradeService);
		}

		/// <summary>
		/// Force the trader to arrive immediately.
		/// </summary>
		public static bool ForceTraderArrival() {
			var tradeService = GetTradeService();
			if (tradeService == null) return false;
			return ReflectionHelper.InvokeVoid(_forceArrivalMethod, tradeService);
		}

		/// <summary>
		/// Check if storm is preventing force arrival.
		/// </summary>
		public static bool IsStormTooCloseToForce() {
			var tradeService = GetTradeService();
			if (tradeService == null) return true;
			return ReflectionHelper.InvokeBool(_isStormTooCloseToForceMethod, tradeService);
		}

		/// <summary>
		/// Check if user can afford the force arrival cost.
		/// </summary>
		public static bool CanPayForceArrivalPrice() {
			var tradeService = GetTradeService();
			if (tradeService == null) return false;
			return ReflectionHelper.InvokeBool(_canPayForceArrivalPriceMethod, tradeService);
		}

		/// <summary>
		/// Check if there's any trading post in the settlement.
		/// </summary>
		public static bool HasAnyTradePost() {
			var tradeService = GetTradeService();
			if (tradeService == null) return false;
			return ReflectionHelper.InvokeBool(_hasAnyTradePostMethod, tradeService);
		}

		/// <summary>
		/// Check if the current visit was already forced.
		/// </summary>
		public static bool IsVisitAlreadyForced() {
			var visit = GetCurrentVisit();
			if (visit == null) return false;
			return ReflectionHelper.GetBool(_visitForcedField, visit);
		}

		/// <summary>
		/// Get human-readable reason why force arrival is unavailable.
		/// Returns null if force arrival IS available.
		/// </summary>
		public static string GetForceArrivalUnavailableReason() {
			// If it's available, no reason needed
			if (CanForceArrival()) return null;

			// Check each condition in order of user-actionability
			if (IsTradingBlocked())
				return "Trading is blocked";

			if (IsStormTooCloseToForce())
				return "Storm is too close";

			if (IsVisitAlreadyForced())
				return "Already forced this trader";

			// Check if trader has progressed too far (past force point)
			float progress = GetTravelProgress();
			if (progress >= 0.9f)  // forceArrivalProgress is typically 0.9
				return "Trader is almost here";

			if (!CanPayForceArrivalPrice()) {
				float cost = GetForceArrivalCost();
				return $"Not enough Impatience to spare, costs {cost:F1}";
			}

			return "Force arrival unavailable";
		}

		// ========================================
		// SEASON INFO
		// ========================================

		private static object GetCalendarService() {
			EnsureCached();
			return GameReflection.GetService(_gsCalendarServiceProperty);
		}

		/// <summary>
		/// Check if it's currently Storm season.
		/// </summary>
		public static bool IsStormSeason() {
			var calendarService = GetCalendarService();
			if (calendarService == null) return false;
			// Season enum: Drizzle = 0, Clearance = 1, Storm = 2
			var season = ReflectionHelper.GetProp(_calendarSeasonProperty, calendarService);
			return season != null && season.ToString() == "Storm";
		}

		/// <summary>
		/// Get time until the current season ends (in seconds).
		/// </summary>
		public static float GetTimeTillSeasonChange() {
			var calendarService = GetCalendarService();
			if (calendarService == null) return -1f;
			var result = ReflectionHelper.Invoke(_getTimeTillNextSeasonChangeMethod, calendarService);
			return result is float f ? f : -1f;
		}

		// ========================================
		// GOODS TRADING
		// ========================================

		/// <summary>
		/// Get the list of village goods that can be sold.
		/// </summary>
		public static List<TradingGoodInfo> GetVillageGoods() {
			var result = new List<TradingGoodInfo>();
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) return result;

			try {
				// Get village offer - this returns List<Good>
				var villageOffer = ReflectionHelper.Invoke(_getVillageOfferMethod, tradeService, visit);
				if (villageOffer == null) return result;

				var enumerable = villageOffer as IEnumerable;
				if (enumerable == null) return result;

				var settings = GetSettings();

				foreach (var good in enumerable) {
					if (good == null) continue;

					var name = ReflectionHelper.GetString(_goodNameField, good);
					if (string.IsNullOrEmpty(name)) continue;

					int storageAmount = ReflectionHelper.GetInt(_goodAmountField, good);

					// Get display name from settings
					string displayName = name;
					var goodModel = ReflectionHelper.Invoke(_getGoodMethod, settings, name);
					if (goodModel != null)
						displayName = ReflectionHelper.GetLocaString(_goodDisplayNameField, goodModel) ?? name;

					// Get sell value
					var val = ReflectionHelper.Invoke(_getValueInCurrencyGoodNameMethod, tradeService, name, 1);
					float unitValue = val is float f ? f : 0f;

					result.Add(new TradingGoodInfo {
						Name = name,
						DisplayName = displayName,
						StorageAmount = storageAmount,
						OfferedAmount = 0,
						UnitValue = unitValue
					});
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetVillageGoods failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get the list of trader goods that can be bought.
		/// </summary>
		public static List<TradingGoodInfo> GetTraderGoods() {
			var result = new List<TradingGoodInfo>();
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) return result;

			try {
				var goodsArray = ReflectionHelper.GetField(_visitGoodsField, visit);
				if (goodsArray == null) return result;

				var settings = GetSettings();
				var arr = goodsArray as Array;
				if (arr == null) return result;

				foreach (var good in arr) {
					if (good == null) continue;

					var name = ReflectionHelper.GetString(_goodNameField, good);
					if (string.IsNullOrEmpty(name)) continue;

					int availableAmount = ReflectionHelper.GetInt(_goodAmountField, good);
					if (availableAmount <= 0) continue;

					// Get display name from settings
					string displayName = name;
					var goodModel = ReflectionHelper.Invoke(_getGoodMethod, settings, name);
					if (goodModel != null)
						displayName = ReflectionHelper.GetLocaString(_goodDisplayNameField, goodModel) ?? name;

					// Get buy value
					var val = ReflectionHelper.Invoke(_getBuyValueInCurrencyGoodNameMethod, tradeService, name, 1);
					float unitValue = val is float f ? f : 0f;

					result.Add(new TradingGoodInfo {
						Name = name,
						DisplayName = displayName,
						StorageAmount = availableAmount,
						OfferedAmount = 0,
						UnitValue = unitValue
					});
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetTraderGoods failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get the current amount of Amber (trade currency) in storage.
		/// </summary>
		public static int GetAmberInStorage() {
			var storageService = GetStorageService();
			if (storageService == null) return 0;

			string currencyName = GetTradeCurrencyName();
			if (string.IsNullOrEmpty(currencyName)) {
				Debug.LogWarning("[ATSAccessibility] Could not get trade currency name");
				return 0;
			}

			return ReflectionHelper.InvokeInt(_getAmountMethod, storageService, currencyName);
		}

		/// <summary>
		/// Get the name of the trade currency (typically Amber).
		/// </summary>
		private static string GetTradeCurrencyName() {
			EnsureCached();
			var settings = GetSettings();
			if (settings == null) return null;

			var tradeCurrency = ReflectionHelper.GetField(_tradeCurrencyField, settings);
			if (tradeCurrency == null) return null;

			// Use runtime type to get Name property - handles inheritance properly
			var nameProperty = tradeCurrency.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
			if (nameProperty == null) {
				// Fallback to Unity Object.name property
				nameProperty = tradeCurrency.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
			}

			return ReflectionHelper.GetPropString(nameProperty, tradeCurrency);
		}

		/// <summary>
		/// Get the sell value for a good amount.
		/// </summary>
		public static float GetGoodSellValue(string goodName, int amount) {
			var tradeService = GetTradeService();
			if (tradeService == null) return 0f;
			var result = ReflectionHelper.Invoke(_getValueInCurrencyGoodNameMethod, tradeService, goodName, amount);
			return result is float f ? f : 0f;
		}

		/// <summary>
		/// Get the buy value for a good amount.
		/// </summary>
		public static float GetGoodBuyValue(string goodName, int amount) {
			var tradeService = GetTradeService();
			if (tradeService == null) return 0f;
			var result = ReflectionHelper.Invoke(_getBuyValueInCurrencyGoodNameMethod, tradeService, goodName, amount);
			return result is float f ? f : 0f;
		}

		/// <summary>
		/// Execute a trade by selling and buying specified goods.
		/// </summary>
		/// <param name="sellGoods">List of (goodName, amount) pairs to sell</param>
		/// <param name="buyGoods">List of (goodName, amount) pairs to buy</param>
		/// <returns>True if trade succeeded, false otherwise</returns>
		public static bool ExecuteTrade(List<KeyValuePair<string, int>> sellGoods, List<KeyValuePair<string, int>> buyGoods) {
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) {
				Debug.LogError("[ATSAccessibility] ExecuteTrade failed: no trade service or visit");
				return false;
			}

			if (_completeTradeMethod == null || _tradingOfferType == null) {
				Debug.LogError("[ATSAccessibility] ExecuteTrade failed: methods not cached");
				return false;
			}

			try {
				EnsureCached();
				if (_goodType == null || _goodCtor == null) {
					Debug.LogError("[ATSAccessibility] ExecuteTrade failed: Good type not cached");
					return false;
				}

				// Create Good arrays for village (sell) and trader (buy)
				var sellGoodArray = Array.CreateInstance(_goodType, sellGoods.Count);
				for (int i = 0; i < sellGoods.Count; i++) {
					var good = _goodCtor.Invoke(new object[] { sellGoods[i].Key, sellGoods[i].Value });
					sellGoodArray.SetValue(good, i);
				}

				var buyGoodArray = Array.CreateInstance(_goodType, buyGoods.Count);
				for (int i = 0; i < buyGoods.Count; i++) {
					var good = _goodCtor.Invoke(new object[] { buyGoods[i].Key, buyGoods[i].Value });
					buyGoodArray.SetValue(good, i);
				}

				// Create TradingOffer objects
				// TradingOffer needs Good[] but the goods inside need offeredAmount set
				// Actually, looking at the code, TradingOffer(Good[]) creates TradingGood with storageAmount
				// We need to manipulate offeredAmount after creation

				var villageOffer = _tradingOfferCtor.Invoke(new object[] { sellGoodArray });
				var traderOffer = _tradingOfferCtor.Invoke(new object[] { buyGoodArray });

				// Get the goods dictionary from each offer and set offeredAmount
				if (_tradingOfferGoodsField != null) {
					// For village offer, move storageAmount to offeredAmount
					var villageDict = ReflectionHelper.GetField(_tradingOfferGoodsField, villageOffer);
					SetOfferedAmountsFromStorage(villageDict);

					// For trader offer, move storageAmount to offeredAmount
					var traderDict = ReflectionHelper.GetField(_tradingOfferGoodsField, traderOffer);
					SetOfferedAmountsFromStorage(traderDict);
				}

				// Call CompleteTrade(visit, villageOffer, traderOffer)
				ReflectionHelper.Invoke(_completeTradeMethod, tradeService, visit, villageOffer, traderOffer);

				Debug.Log("[ATSAccessibility] Trade executed successfully");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ExecuteTrade failed: {ex.Message}\n{ex.StackTrace}");
				return false;
			}
		}

		/// <summary>
		/// Helper to set offeredAmount = storageAmount for all goods in a TradingOffer dictionary.
		/// </summary>
		private static void SetOfferedAmountsFromStorage(object goodsDict) {
			if (goodsDict == null) return;

			try {
				// goodsDict is Dictionary<string, TradingGood> - iterate via reflection
				var valuesProperty = goodsDict.GetType().GetProperty("Values");
				var values = valuesProperty?.GetValue(goodsDict) as IEnumerable;
				if (values == null) return;

				foreach (var tradingGood in values) {
					if (tradingGood == null) continue;

					int storage = ReflectionHelper.GetInt(_tradingGoodStorageAmountField, tradingGood);
					ReflectionHelper.SetField(_tradingGoodOfferedAmountField, tradingGood, storage);
					ReflectionHelper.SetField(_tradingGoodStorageAmountField, tradingGood, 0);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] SetOfferedAmountsFromStorage failed: {ex.Message}");
			}
		}

		// ========================================
		// PERKS
		// ========================================

		/// <summary>
		/// Get the list of perks (effects) offered by the trader.
		/// </summary>
		public static List<PerkInfo> GetPerks() {
			var result = new List<PerkInfo>();
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null) return result;

			try {
				var effectsList = ReflectionHelper.GetField(_visitOfferedEffectsField, visit);
				if (effectsList == null) return result;

				var enumerable = effectsList as IEnumerable;
				if (enumerable == null) return result;

				var settings = GetSettings();

				foreach (var effectState in enumerable) {
					if (effectState == null) continue;

					var effectName = ReflectionHelper.GetString(_effectStateEffectField, effectState);
					if (string.IsNullOrEmpty(effectName)) continue;

					bool isSold = ReflectionHelper.GetBool(_effectStateSoldField, effectState);
					bool isDiscounted = ReflectionHelper.GetBool(_effectStateDiscountedField, effectState);
					float ratio = ReflectionHelper.GetFloat(_effectStatePriceRatioField, effectState);
					if (ratio == 0f) ratio = 1f;

					// Get display name and description from settings
					// Use properties (not fields) to get formatted values
					string displayName = effectName;
					string description = "";
					var effectModel = ReflectionHelper.Invoke(_getEffectMethod, settings, effectName);
					if (effectModel != null) {
						displayName = ReflectionHelper.GetPropString(_effectDisplayNameProperty, effectModel) ?? effectName;
						description = ReflectionHelper.GetPropString(_effectDescriptionProperty, effectModel) ?? "";
					}

					// Get price
					var val = ReflectionHelper.Invoke(_getValueInCurrencyEffectStateMethod, tradeService, effectState);
					float price = val is float pf ? pf : 0f;

					result.Add(new PerkInfo {
						Name = effectName,
						DisplayName = displayName,
						Description = description,
						Price = price,
						Discounted = isDiscounted,
						DiscountRatio = ratio,
						Sold = isSold,
						EffectState = effectState
					});
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetPerks failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Buy a perk from the trader.
		/// </summary>
		public static bool BuyPerk(object effectState) {
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			if (tradeService == null || visit == null || effectState == null) return false;
			return ReflectionHelper.InvokeVoid(_completeTradeEffectMethod, tradeService, visit, effectState);
		}

		// ========================================
		// ASSAULT
		// ========================================

		/// <summary>
		/// Check if the trader can be assaulted.
		/// </summary>
		public static bool CanAssaultTrader() {
			var trader = GetCurrentTrader();
			if (trader == null) return false;

			// Check canAssault field on trader model
			if (!ReflectionHelper.GetBool(_traderCanAssaultField, trader)) return false;

			// Also check EffectsService.CanAttackTrader
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return false;

			var effectsService = ReflectionHelper.GetProp(_gsEffectsServiceProperty, gameServices);
			if (effectsService == null) return true; // Assume yes if we can't check

			return ReflectionHelper.InvokeBool(_canAttackTraderMethod, effectsService, trader);
		}

		/// <summary>
		/// Assault the trader.
		/// </summary>
		public static AssaultResult AssaultTrader() {
			var tradeService = GetTradeService();
			var visit = GetCurrentVisit();
			var result = new AssaultResult { Success = false };

			if (tradeService == null || visit == null) return result;
			if (_assaultTraderMethod == null) return result;

			try {
				var assaultResult = ReflectionHelper.Invoke(_assaultTraderMethod, tradeService, visit);
				if (assaultResult == null) return result;

				result.Success = true;

				// Extract counts from result
				// goods is List<Good>
				var goodsField = assaultResult.GetType().GetField("goods");
				var goods = ReflectionHelper.GetList(goodsField, assaultResult);
				result.GoodsStolen = goods?.Count ?? 0;

				// stolenEffects is List<EffectModel>
				var effectsField = assaultResult.GetType().GetField("stolenEffects");
				var effects = ReflectionHelper.GetList(effectsField, assaultResult);
				result.PerksStolen = effects?.Count ?? 0;

				// villagersKilled
				var villagersField = assaultResult.GetType().GetField("villagersKilled");
				result.VillagersLost = ReflectionHelper.GetInt(villagersField, assaultResult);

				// Trigger the assault result popup (like the game does)
				TriggerAssaultResultPopup(assaultResult);

				// Hide the trader panel (like the game does)
				HideTraderPanel();

				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] AssaultTrader failed: {ex.Message}");
				return result;
			}
		}

		/// <summary>
		/// Trigger the assault result popup by sending the result to GameBlackboardService.
		/// </summary>
		private static void TriggerAssaultResultPopup(object assaultResult) {
			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) {
					Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: gameServices is null");
					return;
				}

				var blackboardService = ReflectionHelper.GetProp(_gsGameBlackboardServiceProperty, gameServices);
				if (blackboardService == null) {
					Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: blackboardService is null");
					return;
				}

				var observable = ReflectionHelper.GetProp(_assaultResultPopupRequestedProperty, blackboardService);
				if (observable == null) {
					Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: observable is null");
					return;
				}

				// Get the OnNext method from the Subject
				var onNextMethod = observable.GetType().GetMethod("OnNext");
				if (onNextMethod == null) {
					Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: OnNext method not found");
					return;
				}

				onNextMethod.Invoke(observable, new[] { assaultResult });
				Debug.Log("[ATSAccessibility] Triggered assault result popup");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] TriggerAssaultResultPopup failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Hide the trader panel (called after assault to match game behavior).
		/// </summary>
		private static void HideTraderPanel() {
			try {
				if (_traderPanelInstanceProperty == null || _traderPanelHideMethod == null) {
					Debug.LogWarning("[ATSAccessibility] HideTraderPanel: Methods not cached");
					return;
				}

				// Static property - keep GetValue(null) for static access
				var instance = _traderPanelInstanceProperty.GetValue(null);
				if (instance == null) {
					Debug.LogWarning("[ATSAccessibility] HideTraderPanel: Instance is null");
					return;
				}

				ReflectionHelper.InvokeVoid(_traderPanelHideMethod, instance);
				Debug.Log("[ATSAccessibility] Hid trader panel after assault");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] HideTraderPanel failed: {ex.Message}");
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(TradeReflection), "TradeReflection");
		}
	}
}
