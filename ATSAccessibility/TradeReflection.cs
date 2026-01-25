using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing trade service and trader panel data.
    /// Provides methods to query trader state, goods, perks, and execute trades.
    /// </summary>
    public static class TradeReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public struct TradingGoodInfo
        {
            public string Name;           // Good name/key
            public string DisplayName;    // Localized display name
            public int StorageAmount;     // Amount in storage (not offered)
            public int OfferedAmount;     // Amount being offered
            public float UnitValue;       // Amber per unit (sell or buy)
        }

        public struct PerkInfo
        {
            public string Name;           // Effect name/key
            public string DisplayName;    // Localized display name
            public string Description;    // Effect description
            public float Price;           // Amber cost
            public bool Discounted;       // Whether has discount
            public float DiscountRatio;   // Price ratio (e.g., 0.8 = 20% off)
            public bool Sold;             // Whether already purchased
            public object EffectState;    // TraderEffectState for purchase
        }

        public struct AssaultResult
        {
            public bool Success;
            public int GoodsStolen;
            public int PerksStolen;
            public int VillagersLost;
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

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
        private static MethodInfo _getTraderMethod = null;
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

        // LocaText property
        private static PropertyInfo _locaTextProperty = null;

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
                if (assembly == null) return;

                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType == null) return;

                // Cache service property accessors
                _gsTradeServiceProperty = gameServicesType.GetProperty("TradeService");
                _gsStorageServiceProperty = gameServicesType.GetProperty("StorageService");
                _gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService");
                _gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService");

                // ICalendarService
                var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
                if (calendarServiceType != null)
                {
                    _calendarSeasonProperty = calendarServiceType.GetProperty("Season");
                    _getTimeTillNextSeasonChangeMethod = calendarServiceType.GetMethod("GetTimeTillNextSeasonChange");
                }

                // ITradeService methods
                var tradeServiceType = assembly.GetType("Eremite.Services.ITradeService");
                if (tradeServiceType != null)
                {
                    _isMainTraderInTheVillageMethod = tradeServiceType.GetMethod("IsMainTraderInTheVillage");
                    _getCurrentMainVisitMethod = tradeServiceType.GetMethod("GetCurrentMainVisit");
                    _getCurrentMainTraderMethod = tradeServiceType.GetMethod("GetCurrentMainTrader");
                    _getNextMainTraderMethod = tradeServiceType.GetMethod("GetNextMainTrader");
                    _getTraderMethod = tradeServiceType.GetMethod("GetTrader");
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
                    foreach (var m in methods)
                    {
                        if (m.Name == "GetValueInCurrency")
                        {
                            var ps = m.GetParameters();
                            if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(int))
                                _getValueInCurrencyGoodNameMethod = m;
                            else if (ps.Length == 1 && ps[0].ParameterType.Name == "TradingOffer")
                                _getValueInCurrencyMethod = m;
                            else if (ps.Length == 1 && ps[0].ParameterType.Name == "TraderEffectState")
                                _getValueInCurrencyEffectStateMethod = m;
                        }
                        else if (m.Name == "GetBuyValueInCurrency")
                        {
                            var ps = m.GetParameters();
                            if (ps.Length == 2 && ps[0].ParameterType == typeof(string) && ps[1].ParameterType == typeof(int))
                                _getBuyValueInCurrencyGoodNameMethod = m;
                            else if (ps.Length == 1 && ps[0].ParameterType.Name == "TradingOffer")
                                _getBuyValueInCurrencyMethod = m;
                        }
                        else if (m.Name == "CompleteTrade")
                        {
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
                if (storageServiceType != null)
                {
                    _getAmountMethod = storageServiceType.GetMethod("GetAmount", new[] { typeof(string) });
                }

                // TraderVisitState fields
                var visitStateType = assembly.GetType("Eremite.Model.State.TraderVisitState");
                if (visitStateType != null)
                {
                    _visitGoodsField = visitStateType.GetField("goods");
                    _visitOfferedEffectsField = visitStateType.GetField("offeredEffects");
                    _visitTravelProgressField = visitStateType.GetField("travelProgress");
                    _visitForcedField = visitStateType.GetField("forced");
                }

                // TraderModel fields
                var traderModelType = assembly.GetType("Eremite.Model.Trade.TraderModel");
                if (traderModelType != null)
                {
                    _traderDisplayNameField = traderModelType.GetField("displayName");
                    _traderDescriptionField = traderModelType.GetField("description");
                    _traderDialogueField = traderModelType.GetField("dialogue");
                    _traderLabelField = traderModelType.GetField("label");
                    _traderCanAssaultField = traderModelType.GetField("canAssault");
                    _traderTransactionSoundField = traderModelType.GetField("transactionSound");
                }

                // SoundRef GetNext method
                var soundRefType = assembly.GetType("Eremite.Model.Sound.SoundRef");
                if (soundRefType != null)
                {
                    _soundRefGetNextMethod = soundRefType.GetMethod("GetNext");
                }

                // LabelModel fields
                var labelModelType = assembly.GetType("Eremite.Model.LabelModel");
                if (labelModelType != null)
                {
                    _labelDisplayNameField = labelModelType.GetField("displayName");
                }

                // LocaText
                var locaTextType = assembly.GetType("Eremite.Model.LocaText");
                if (locaTextType != null)
                {
                    _locaTextProperty = locaTextType.GetProperty("Text");
                }

                // Good struct
                _goodType = assembly.GetType("Eremite.Model.Good");
                if (_goodType != null)
                {
                    _goodNameField = _goodType.GetField("name");
                    _goodAmountField = _goodType.GetField("amount");
                    _goodCtor = _goodType.GetConstructor(new[] { typeof(string), typeof(int) });
                }

                // TradingGood
                var tradingGoodType = assembly.GetType("Eremite.Model.Trade.TradingGood");
                if (tradingGoodType != null)
                {
                    // It's a readonly field, try different access patterns
                    _tradingGoodNameField = tradingGoodType.GetField("goodName", BindingFlags.Public | BindingFlags.Instance);
                    if (_tradingGoodNameField == null)
                    {
                        foreach (var f in tradingGoodType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (f.Name == "goodName" || f.Name == "<goodName>k__BackingField")
                            {
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
                if (effectStateType != null)
                {
                    _effectStateEffectField = effectStateType.GetField("effect");
                    _effectStateSoldField = effectStateType.GetField("sold");
                    _effectStateDiscountedField = effectStateType.GetField("discounted");
                    _effectStatePriceRatioField = effectStateType.GetField("priceRatio");
                }

                // TradingOffer
                _tradingOfferType = assembly.GetType("Eremite.Model.Trade.TradingOffer");
                if (_tradingOfferType != null)
                {
                    var goodArrayType = assembly.GetType("Eremite.Model.Good").MakeArrayType();
                    _tradingOfferCtor = _tradingOfferType.GetConstructor(new[] { goodArrayType });
                    _tradingOfferGoodsField = _tradingOfferType.GetField("goods");
                }

                // EffectModel
                var effectModelType = assembly.GetType("Eremite.Model.Effects.EffectModel");
                if (effectModelType == null)
                    effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    // Use properties to get formatted display name and description
                    _effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    _effectDescriptionProperty = effectModelType.GetProperty("Description", BindingFlags.Public | BindingFlags.Instance);
                }

                // Settings access (use GameReflection.GetSettings() for MB.Settings)
                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _getGoodMethod = settingsType.GetMethod("GetGood", new[] { typeof(string) });
                    _getEffectMethod = settingsType.GetMethod("GetEffect", new[] { typeof(string) });
                    _tradeCurrencyField = settingsType.GetField("tradeCurrency");
                }

                var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
                if (goodModelType != null)
                {
                    _goodDisplayNameField = goodModelType.GetField("displayName");
                }

                // TraderAssaultResult
                var assaultResultType = assembly.GetType("Eremite.Services.TraderAssaultResult");
                if (assaultResultType != null)
                {
                    _assaultVillagersKilledField = assaultResultType.GetField("villagersKilled");
                }

                // EffectsService for assault check
                var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
                if (effectsServiceType != null)
                {
                    _canAttackTraderMethod = effectsServiceType.GetMethod("CanAttackTrader");
                }

                // GameBlackboardService for triggering assault result popup
                _gsGameBlackboardServiceProperty = gameServicesType.GetProperty("GameBlackboardService");
                var blackboardServiceType = assembly.GetType("Eremite.Services.IGameBlackboardService");
                if (blackboardServiceType != null)
                {
                    _assaultResultPopupRequestedProperty = blackboardServiceType.GetProperty("TraderAssaultResultPopupRequested");
                }

                // TraderPanel for closing after assault
                var traderPanelType = assembly.GetType("Eremite.Buildings.UI.Trade.TraderPanel");
                if (traderPanelType != null)
                {
                    _traderPanelInstanceProperty = traderPanelType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _traderPanelHideMethod = traderPanelType.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] TradeReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TradeReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetTradeService()
        {
            EnsureCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null) return null;
            return _gsTradeServiceProperty?.GetValue(gameServices);
        }

        private static object GetStorageService()
        {
            EnsureCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null) return null;
            return _gsStorageServiceProperty?.GetValue(gameServices);
        }

        private static object GetSettings()
        {
            // Use GameReflection's method which has the correct type and binding flags
            return GameReflection.GetSettings();
        }

        private static string GetLocaText(object locaText)
        {
            if (locaText == null) return null;
            return _locaTextProperty?.GetValue(locaText) as string;
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a TraderPanel.
        /// </summary>
        public static bool IsTraderPanel(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "TraderPanel";
        }

        // ========================================
        // TRADER STATE
        // ========================================

        /// <summary>
        /// Check if a trader is currently in the village.
        /// </summary>
        public static bool IsTraderPresent()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;

            try
            {
                var result = _isMainTraderInTheVillageMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] IsTraderPresent failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if trading is blocked (e.g., by effects).
        /// </summary>
        public static bool IsTradingBlocked()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return true;

            try
            {
                var result = _isTradingBlockedMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get the current trader visit state.
        /// </summary>
        public static object GetCurrentVisit()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                return _getCurrentMainVisitMethod?.Invoke(tradeService, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the trader travel progress (0-1).
        /// </summary>
        public static float GetTravelProgress()
        {
            var visit = GetCurrentVisit();
            if (visit == null) return 0f;

            try
            {
                var val = _visitTravelProgressField?.GetValue(visit);
                return val is float f ? f : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        // ========================================
        // TRADER INFO
        // ========================================

        /// <summary>
        /// Get the current trader's display name.
        /// </summary>
        public static string GetTraderName()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                object trader;
                if (IsTraderPresent())
                    trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                else
                    trader = _getNextMainTraderMethod?.Invoke(tradeService, null);

                if (trader == null) return null;

                var locaText = _traderDisplayNameField?.GetValue(trader);
                return GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTraderName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the trader's label (category like "General Goods").
        /// </summary>
        public static string GetTraderLabel()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                object trader;
                if (IsTraderPresent())
                    trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                else
                    trader = _getNextMainTraderMethod?.Invoke(tradeService, null);

                if (trader == null) return null;

                var label = _traderLabelField?.GetValue(trader);
                if (label == null) return null;

                var locaText = _labelDisplayNameField?.GetValue(label);
                return GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the trader's description.
        /// </summary>
        public static string GetTraderDescription()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                object trader;
                if (IsTraderPresent())
                    trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                else
                    trader = _getNextMainTraderMethod?.Invoke(tradeService, null);

                if (trader == null) return null;

                var locaText = _traderDescriptionField?.GetValue(trader);
                return GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the trader's dialogue text.
        /// </summary>
        public static string GetTraderDialogue()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                var trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                if (trader == null) return null;

                var locaText = _traderDialogueField?.GetValue(trader);
                return GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current trader's transaction sound model (for trader-specific sound).
        /// Returns null if not available.
        /// </summary>
        public static object GetTraderTransactionSound()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return null;

            try
            {
                var trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                if (trader == null) return null;

                var soundRef = _traderTransactionSoundField?.GetValue(trader);
                if (soundRef == null) return null;

                // Call GetNext() on the SoundRef to get the SoundModel
                return _soundRefGetNextMethod?.Invoke(soundRef, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTraderTransactionSound failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the time remaining until trader arrives (in seconds).
        /// </summary>
        public static float GetTimeToArrival()
        {
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null) return -1f;

            try
            {
                var result = _getTimeLeftToMethod?.Invoke(tradeService, new[] { visit });
                return result is float f ? f : -1f;
            }
            catch
            {
                return -1f;
            }
        }

        /// <summary>
        /// Get the time remaining that trader will stay (in seconds).
        /// </summary>
        public static float GetStayingTimeLeft()
        {
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null) return -1f;

            try
            {
                var result = _getStayingTimeLeftMethod?.Invoke(tradeService, new[] { visit });
                return result is float f ? f : -1f;
            }
            catch
            {
                return -1f;
            }
        }

        // ========================================
        // FORCE ARRIVAL
        // ========================================

        /// <summary>
        /// Check if trader arrival can be forced.
        /// </summary>
        public static bool CanForceArrival()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;

            try
            {
                var result = _canForceArrivalMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the impatience cost to force trader arrival.
        /// </summary>
        public static float GetForceArrivalCost()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return 0f;

            try
            {
                var result = _getForceArrivalPriceMethod?.Invoke(tradeService, null);
                return result is float f ? f : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Force the trader to arrive immediately.
        /// </summary>
        public static bool ForceTraderArrival()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;
            if (_forceArrivalMethod == null) return false;

            try
            {
                _forceArrivalMethod.Invoke(tradeService, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ForceTraderArrival failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if storm is preventing force arrival.
        /// </summary>
        public static bool IsStormTooCloseToForce()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return true;

            try
            {
                var result = _isStormTooCloseToForceMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Check if user can afford the force arrival cost.
        /// </summary>
        public static bool CanPayForceArrivalPrice()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;

            try
            {
                var result = _canPayForceArrivalPriceMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if there's any trading post in the settlement.
        /// </summary>
        public static bool HasAnyTradePost()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;

            try
            {
                var result = _hasAnyTradePostMethod?.Invoke(tradeService, null);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the current visit was already forced.
        /// </summary>
        public static bool IsVisitAlreadyForced()
        {
            var visit = GetCurrentVisit();
            if (visit == null) return false;

            try
            {
                var result = _visitForcedField?.GetValue(visit);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get human-readable reason why force arrival is unavailable.
        /// Returns null if force arrival IS available.
        /// </summary>
        public static string GetForceArrivalUnavailableReason()
        {
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

            if (!CanPayForceArrivalPrice())
            {
                float cost = GetForceArrivalCost();
                return $"Not enough Impatience to spare, costs {cost:F1}";
            }

            return "Force arrival unavailable";
        }

        // ========================================
        // SEASON INFO
        // ========================================

        private static object GetCalendarService()
        {
            EnsureCached();
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null) return null;
            return _gsCalendarServiceProperty?.GetValue(gameServices);
        }

        /// <summary>
        /// Check if it's currently Storm season.
        /// </summary>
        public static bool IsStormSeason()
        {
            var calendarService = GetCalendarService();
            if (calendarService == null) return false;

            try
            {
                var season = _calendarSeasonProperty?.GetValue(calendarService);
                // Season enum: Drizzle = 0, Clearance = 1, Storm = 2
                return season != null && season.ToString() == "Storm";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get time until the current season ends (in seconds).
        /// </summary>
        public static float GetTimeTillSeasonChange()
        {
            var calendarService = GetCalendarService();
            if (calendarService == null) return -1f;

            try
            {
                var result = _getTimeTillNextSeasonChangeMethod?.Invoke(calendarService, null);
                return result is float f ? f : -1f;
            }
            catch
            {
                return -1f;
            }
        }

        // ========================================
        // GOODS TRADING
        // ========================================

        /// <summary>
        /// Get the list of village goods that can be sold.
        /// </summary>
        public static List<TradingGoodInfo> GetVillageGoods()
        {
            var result = new List<TradingGoodInfo>();
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null) return result;

            try
            {
                // Get village offer - this returns List<Good>
                var villageOffer = _getVillageOfferMethod?.Invoke(tradeService, new[] { visit });
                if (villageOffer == null) return result;

                var enumerable = villageOffer as IEnumerable;
                if (enumerable == null) return result;

                var settings = GetSettings();

                foreach (var good in enumerable)
                {
                    if (good == null) continue;

                    var name = _goodNameField?.GetValue(good) as string;
                    var amount = _goodAmountField?.GetValue(good);
                    if (string.IsNullOrEmpty(name)) continue;

                    int storageAmount = amount is int a ? a : 0;

                    // Get display name from settings
                    string displayName = name;
                    if (settings != null && _getGoodMethod != null)
                    {
                        var goodModel = _getGoodMethod.Invoke(settings, new object[] { name });
                        if (goodModel != null)
                        {
                            var locaText = _goodDisplayNameField?.GetValue(goodModel);
                            displayName = GetLocaText(locaText) ?? name;
                        }
                    }

                    // Get sell value
                    float unitValue = 0f;
                    if (_getValueInCurrencyGoodNameMethod != null)
                    {
                        var val = _getValueInCurrencyGoodNameMethod.Invoke(tradeService, new object[] { name, 1 });
                        unitValue = val is float f ? f : 0f;
                    }

                    result.Add(new TradingGoodInfo
                    {
                        Name = name,
                        DisplayName = displayName,
                        StorageAmount = storageAmount,
                        OfferedAmount = 0,
                        UnitValue = unitValue
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetVillageGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the list of trader goods that can be bought.
        /// </summary>
        public static List<TradingGoodInfo> GetTraderGoods()
        {
            var result = new List<TradingGoodInfo>();
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null) return result;

            try
            {
                var goodsArray = _visitGoodsField?.GetValue(visit);
                if (goodsArray == null) return result;

                var settings = GetSettings();
                var arr = goodsArray as Array;
                if (arr == null) return result;

                foreach (var good in arr)
                {
                    if (good == null) continue;

                    var name = _goodNameField?.GetValue(good) as string;
                    var amount = _goodAmountField?.GetValue(good);
                    if (string.IsNullOrEmpty(name)) continue;

                    int availableAmount = amount is int a ? a : 0;
                    if (availableAmount <= 0) continue;

                    // Get display name from settings
                    string displayName = name;
                    if (settings != null && _getGoodMethod != null)
                    {
                        var goodModel = _getGoodMethod.Invoke(settings, new object[] { name });
                        if (goodModel != null)
                        {
                            var locaText = _goodDisplayNameField?.GetValue(goodModel);
                            displayName = GetLocaText(locaText) ?? name;
                        }
                    }

                    // Get buy value
                    float unitValue = 0f;
                    if (_getBuyValueInCurrencyGoodNameMethod != null)
                    {
                        var val = _getBuyValueInCurrencyGoodNameMethod.Invoke(tradeService, new object[] { name, 1 });
                        unitValue = val is float f ? f : 0f;
                    }

                    result.Add(new TradingGoodInfo
                    {
                        Name = name,
                        DisplayName = displayName,
                        StorageAmount = availableAmount,
                        OfferedAmount = 0,
                        UnitValue = unitValue
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTraderGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the current amount of Amber (trade currency) in storage.
        /// </summary>
        public static int GetAmberInStorage()
        {
            var storageService = GetStorageService();
            if (storageService == null) return 0;

            try
            {
                // Get trade currency name from Settings
                string currencyName = GetTradeCurrencyName();
                if (string.IsNullOrEmpty(currencyName))
                {
                    Debug.LogWarning("[ATSAccessibility] Could not get trade currency name");
                    return 0;
                }

                var result = _getAmountMethod?.Invoke(storageService, new object[] { currencyName });
                return result is int a ? a : 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAmberInStorage failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get the name of the trade currency (typically Amber).
        /// </summary>
        private static string GetTradeCurrencyName()
        {
            EnsureCached();
            var settings = GetSettings();
            if (settings == null) return null;

            try
            {
                // Get Settings.tradeCurrency (GoodModel)
                var tradeCurrency = _tradeCurrencyField?.GetValue(settings);
                if (tradeCurrency == null) return null;

                // Use runtime type to get Name property - handles inheritance properly
                var nameProperty = tradeCurrency.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProperty == null)
                {
                    // Fallback to Unity Object.name property
                    nameProperty = tradeCurrency.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                }
                if (nameProperty == null) return null;

                return nameProperty.GetValue(tradeCurrency) as string;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTradeCurrencyName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the sell value for a good amount.
        /// </summary>
        public static float GetGoodSellValue(string goodName, int amount)
        {
            var tradeService = GetTradeService();
            if (tradeService == null || _getValueInCurrencyGoodNameMethod == null) return 0f;

            try
            {
                var result = _getValueInCurrencyGoodNameMethod.Invoke(tradeService, new object[] { goodName, amount });
                return result is float f ? f : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the buy value for a good amount.
        /// </summary>
        public static float GetGoodBuyValue(string goodName, int amount)
        {
            var tradeService = GetTradeService();
            if (tradeService == null || _getBuyValueInCurrencyGoodNameMethod == null) return 0f;

            try
            {
                var result = _getBuyValueInCurrencyGoodNameMethod.Invoke(tradeService, new object[] { goodName, amount });
                return result is float f ? f : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Execute a trade by selling and buying specified goods.
        /// </summary>
        /// <param name="sellGoods">List of (goodName, amount) pairs to sell</param>
        /// <param name="buyGoods">List of (goodName, amount) pairs to buy</param>
        /// <returns>True if trade succeeded, false otherwise</returns>
        public static bool ExecuteTrade(List<KeyValuePair<string, int>> sellGoods, List<KeyValuePair<string, int>> buyGoods)
        {
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null)
            {
                Debug.LogError("[ATSAccessibility] ExecuteTrade failed: no trade service or visit");
                return false;
            }

            if (_completeTradeMethod == null || _tradingOfferType == null)
            {
                Debug.LogError("[ATSAccessibility] ExecuteTrade failed: methods not cached");
                return false;
            }

            try
            {
                EnsureCached();
                if (_goodType == null || _goodCtor == null)
                {
                    Debug.LogError("[ATSAccessibility] ExecuteTrade failed: Good type not cached");
                    return false;
                }

                // Create Good arrays for village (sell) and trader (buy)
                var sellGoodArray = Array.CreateInstance(_goodType, sellGoods.Count);
                for (int i = 0; i < sellGoods.Count; i++)
                {
                    var good = _goodCtor.Invoke(new object[] { sellGoods[i].Key, sellGoods[i].Value });
                    sellGoodArray.SetValue(good, i);
                }

                var buyGoodArray = Array.CreateInstance(_goodType, buyGoods.Count);
                for (int i = 0; i < buyGoods.Count; i++)
                {
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
                var goodsDictField = _tradingOfferType.GetField("goods");
                if (goodsDictField != null)
                {
                    // For village offer, move storageAmount to offeredAmount
                    var villageDict = goodsDictField.GetValue(villageOffer);
                    SetOfferedAmountsFromStorage(villageDict);

                    // For trader offer, move storageAmount to offeredAmount
                    var traderDict = goodsDictField.GetValue(traderOffer);
                    SetOfferedAmountsFromStorage(traderDict);
                }

                // Call CompleteTrade(visit, villageOffer, traderOffer)
                _completeTradeMethod.Invoke(tradeService, new object[] { visit, villageOffer, traderOffer });

                Debug.Log("[ATSAccessibility] Trade executed successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ExecuteTrade failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Helper to set offeredAmount = storageAmount for all goods in a TradingOffer dictionary.
        /// </summary>
        private static void SetOfferedAmountsFromStorage(object goodsDict)
        {
            if (goodsDict == null) return;

            try
            {
                // goodsDict is Dictionary<string, TradingGood>
                var valuesProperty = goodsDict.GetType().GetProperty("Values");
                var values = valuesProperty?.GetValue(goodsDict) as IEnumerable;
                if (values == null) return;

                foreach (var tradingGood in values)
                {
                    if (tradingGood == null) continue;

                    // Get storageAmount and set offeredAmount to it
                    var storageVal = _tradingGoodStorageAmountField?.GetValue(tradingGood);
                    int storage = storageVal is int s ? s : 0;

                    _tradingGoodOfferedAmountField?.SetValue(tradingGood, storage);
                    _tradingGoodStorageAmountField?.SetValue(tradingGood, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] SetOfferedAmountsFromStorage failed: {ex.Message}");
            }
        }

        // ========================================
        // PERKS
        // ========================================

        /// <summary>
        /// Get the list of perks (effects) offered by the trader.
        /// </summary>
        public static List<PerkInfo> GetPerks()
        {
            var result = new List<PerkInfo>();
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null) return result;

            try
            {
                var effectsList = _visitOfferedEffectsField?.GetValue(visit);
                if (effectsList == null) return result;

                var enumerable = effectsList as IEnumerable;
                if (enumerable == null) return result;

                var settings = GetSettings();

                foreach (var effectState in enumerable)
                {
                    if (effectState == null) continue;

                    var effectName = _effectStateEffectField?.GetValue(effectState) as string;
                    var sold = _effectStateSoldField?.GetValue(effectState);
                    var discounted = _effectStateDiscountedField?.GetValue(effectState);
                    var priceRatio = _effectStatePriceRatioField?.GetValue(effectState);

                    if (string.IsNullOrEmpty(effectName)) continue;

                    bool isSold = sold is bool b && b;
                    bool isDiscounted = discounted is bool d && d;
                    float ratio = priceRatio is float f ? f : 1f;

                    // Get display name and description from settings
                    // Use properties (not fields) to get formatted values
                    string displayName = effectName;
                    string description = "";
                    if (settings != null && _getEffectMethod != null)
                    {
                        var effectModel = _getEffectMethod.Invoke(settings, new object[] { effectName });
                        if (effectModel != null)
                        {
                            displayName = _effectDisplayNameProperty?.GetValue(effectModel) as string ?? effectName;
                            description = _effectDescriptionProperty?.GetValue(effectModel) as string ?? "";
                        }
                    }

                    // Get price
                    float price = 0f;
                    if (_getValueInCurrencyEffectStateMethod != null)
                    {
                        var val = _getValueInCurrencyEffectStateMethod.Invoke(tradeService, new[] { effectState });
                        price = val is float pf ? pf : 0f;
                    }

                    result.Add(new PerkInfo
                    {
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
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetPerks failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Buy a perk from the trader.
        /// </summary>
        public static bool BuyPerk(object effectState)
        {
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            if (tradeService == null || visit == null || effectState == null) return false;
            if (_completeTradeEffectMethod == null) return false;

            try
            {
                _completeTradeEffectMethod.Invoke(tradeService, new[] { visit, effectState });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuyPerk failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // ASSAULT
        // ========================================

        /// <summary>
        /// Check if the trader can be assaulted.
        /// </summary>
        public static bool CanAssaultTrader()
        {
            var tradeService = GetTradeService();
            if (tradeService == null) return false;

            try
            {
                // Get current trader
                var trader = _getCurrentMainTraderMethod?.Invoke(tradeService, null);
                if (trader == null) return false;

                // Check canAssault field on trader model
                var canAssault = _traderCanAssaultField?.GetValue(trader);
                if (canAssault is bool b && !b) return false;

                // Also check EffectsService.CanAttackTrader
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var effectsService = _gsEffectsServiceProperty?.GetValue(gameServices);
                if (effectsService == null) return true; // Assume yes if we can't check

                var result = _canAttackTraderMethod?.Invoke(effectsService, new[] { trader });
                return result is bool r && r;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Assault the trader.
        /// </summary>
        public static AssaultResult AssaultTrader()
        {
            var tradeService = GetTradeService();
            var visit = GetCurrentVisit();
            var result = new AssaultResult { Success = false };

            if (tradeService == null || visit == null) return result;
            if (_assaultTraderMethod == null) return result;

            try
            {
                var assaultResult = _assaultTraderMethod.Invoke(tradeService, new[] { visit });
                if (assaultResult == null) return result;

                result.Success = true;

                // Extract counts from result
                // goods is List<Good>
                var goodsField = assaultResult.GetType().GetField("goods");
                if (goodsField != null)
                {
                    var goods = goodsField.GetValue(assaultResult) as IList;
                    result.GoodsStolen = goods?.Count ?? 0;
                }

                // stolenEffects is List<EffectModel>
                var effectsField = assaultResult.GetType().GetField("stolenEffects");
                if (effectsField != null)
                {
                    var effects = effectsField.GetValue(assaultResult) as IList;
                    result.PerksStolen = effects?.Count ?? 0;
                }

                // villagersKilled
                var villagersField = assaultResult.GetType().GetField("villagersKilled");
                if (villagersField != null)
                {
                    var villagers = villagersField.GetValue(assaultResult);
                    result.VillagersLost = villagers is int v ? v : 0;
                }

                // Trigger the assault result popup (like the game does)
                TriggerAssaultResultPopup(assaultResult);

                // Hide the trader panel (like the game does)
                HideTraderPanel();

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AssaultTrader failed: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Trigger the assault result popup by sending the result to GameBlackboardService.
        /// </summary>
        private static void TriggerAssaultResultPopup(object assaultResult)
        {
            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: gameServices is null");
                    return;
                }

                var blackboardService = _gsGameBlackboardServiceProperty?.GetValue(gameServices);
                if (blackboardService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: blackboardService is null");
                    return;
                }

                var observable = _assaultResultPopupRequestedProperty?.GetValue(blackboardService);
                if (observable == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: observable is null");
                    return;
                }

                // Get the OnNext method from the Subject
                var onNextMethod = observable.GetType().GetMethod("OnNext");
                if (onNextMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TriggerAssaultResultPopup: OnNext method not found");
                    return;
                }

                onNextMethod.Invoke(observable, new[] { assaultResult });
                Debug.Log("[ATSAccessibility] Triggered assault result popup");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TriggerAssaultResultPopup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hide the trader panel (called after assault to match game behavior).
        /// </summary>
        private static void HideTraderPanel()
        {
            try
            {
                if (_traderPanelInstanceProperty == null || _traderPanelHideMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] HideTraderPanel: Methods not cached");
                    return;
                }

                var instance = _traderPanelInstanceProperty.GetValue(null);
                if (instance == null)
                {
                    Debug.LogWarning("[ATSAccessibility] HideTraderPanel: Instance is null");
                    return;
                }

                _traderPanelHideMethod.Invoke(instance, null);
                Debug.Log("[ATSAccessibility] Hid trader panel after assault");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] HideTraderPanel failed: {ex.Message}");
            }
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
    }
}
