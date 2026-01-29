using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to Black Market building internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, buildings) - they are destroyed on scene change
    /// </summary>
    public static class BlackMarketReflection
    {
        // ========================================
        // OFFER INFO STRUCT
        // ========================================

        public struct OfferInfo
        {
            public object State;           // BlackMarketOfferState
            public string GoodName;        // Display name of the good
            public int GoodAmount;
            public int BuyPrice;           // Amber amount for buy
            public int CreditPrice;        // Amber amount for credit
            public string BuyRating;       // "good deal" / "regular price" / "bad deal"
            public string CreditRating;
            public bool Bought;
            public float TimeLeft;
            public string PaymentTerms;    // e.g., "Year III Clearance"
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // Popup type detection
        private static Type _blackMarketPopupType = null;

        // BlackMarketPopup fields
        private static FieldInfo _bmpBlackMarketField = null;

        // BlackMarket fields/methods
        private static FieldInfo _bmStateField = null;
        private static FieldInfo _bmModelField = null;
        private static MethodInfo _bmBuyMethod = null;
        private static MethodInfo _bmBuyOnCreditMethod = null;
        private static MethodInfo _bmRerollMethod = null;
        private static MethodInfo _bmIsRerollOnCooldownMethod = null;
        private static MethodInfo _bmGetTimeLeftForMethod = null;

        // BlackMarketState fields
        private static FieldInfo _bmsOffersField = null;
        private static FieldInfo _bmsLastRerollField = null;
        private static FieldInfo _bmsAmberSpentField = null;

        // BlackMarketModel fields
        private static FieldInfo _bmmRerollPriceField = null;
        private static FieldInfo _bmmRerollCooldownField = null;

        // BlackMarketOfferState fields
        private static FieldInfo _bmosBoughtField = null;
        private static FieldInfo _bmosGoodField = null;
        private static FieldInfo _bmosBuyPriceField = null;
        private static FieldInfo _bmosCreditPriceField = null;
        private static FieldInfo _bmosBuyRatingField = null;
        private static FieldInfo _bmosCreditRatingField = null;
        private static FieldInfo _bmosPaymentModelField = null;
        private static FieldInfo _bmosEndTimeField = null;

        // Good struct fields
        private static FieldInfo _goodNameField = null;
        private static FieldInfo _goodAmountField = null;

        // GoodRef fields/methods
        private static MethodInfo _goodRefToGoodMethod = null;

        // PaymentEffectModel fields
        private static FieldInfo _pemSeasonsToPayField = null;

        // Settings methods
        private static MethodInfo _settingsGetEffectMethod = null;

        // CalendarService fields
        private static PropertyInfo _calGameDateProperty = null;

        // GameDate fields
        private static FieldInfo _gdYearField = null;
        private static FieldInfo _gdSeasonField = null;
        private static MethodInfo _gdAddSeasonsMethod = null;

        // IStorageService / Storage for affordability check
        private static PropertyInfo _ssMainProperty = null;
        private static MethodInfo _storageIsAvailableMethod = null;

        // GameServices property caches
        private static PropertyInfo _gsStorageServiceProperty = null;
        private static PropertyInfo _gsCalendarServiceProperty = null;
        private static PropertyInfo _gsGameTimeServiceProperty = null;

        // GameTime property
        private static PropertyInfo _gtsTimeProperty = null;

        // Pre-allocated args
        private static readonly object[] _args1 = new object[1];

        // Rating labels
        private static readonly string[] _ratingLabels = { "good deal", "regular price", "bad deal" };

        // Season names
        private static readonly string[] _seasonNames = { "Drizzle", "Clearance", "Storm" };

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
                    Debug.LogWarning("[ATSAccessibility] BlackMarketReflection: Game assembly not available");
                    return;
                }

                CachePopupTypes(assembly);
                CacheBlackMarketTypes(assembly);
                CacheBlackMarketStateTypes(assembly);
                CacheBlackMarketModelTypes(assembly);
                CacheOfferStateTypes(assembly);
                CacheGoodTypes(assembly);
                CachePaymentTypes(assembly);
                CacheServiceProperties(assembly);
                CacheCalendarTypes(assembly);
                CacheStorageTypes(assembly);
                CacheGameTimeTypes(assembly);

                Debug.Log("[ATSAccessibility] BlackMarketReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BlackMarketReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            _blackMarketPopupType = assembly.GetType("Eremite.Buildings.UI.BlackMarkets.BlackMarketPopup");
            if (_blackMarketPopupType != null)
            {
                _bmpBlackMarketField = _blackMarketPopupType.GetField("blackMarket", GameReflection.NonPublicInstance);
            }
        }

        private static void CacheBlackMarketTypes(Assembly assembly)
        {
            var blackMarketType = assembly.GetType("Eremite.Buildings.BlackMarket");
            if (blackMarketType != null)
            {
                _bmStateField = blackMarketType.GetField("state", GameReflection.PublicInstance);
                _bmModelField = blackMarketType.GetField("model", GameReflection.PublicInstance);
                _bmRerollMethod = blackMarketType.GetMethod("Reroll", Type.EmptyTypes);
                _bmIsRerollOnCooldownMethod = blackMarketType.GetMethod("IsRerollOnCooldown", Type.EmptyTypes);

                var offerStateType = assembly.GetType("Eremite.Buildings.BlackMarketOfferState");
                if (offerStateType != null)
                {
                    _bmBuyMethod = blackMarketType.GetMethod("Buy", new Type[] { offerStateType });
                    _bmBuyOnCreditMethod = blackMarketType.GetMethod("BuyOnCredit", new Type[] { offerStateType });
                    _bmGetTimeLeftForMethod = blackMarketType.GetMethod("GetTimeLeftFor", new Type[] { offerStateType });
                }
            }
        }

        private static void CacheBlackMarketStateTypes(Assembly assembly)
        {
            var stateType = assembly.GetType("Eremite.Buildings.BlackMarketState");
            if (stateType != null)
            {
                _bmsOffersField = stateType.GetField("offers", GameReflection.PublicInstance);
                _bmsLastRerollField = stateType.GetField("lastReroll", GameReflection.PublicInstance);
                _bmsAmberSpentField = stateType.GetField("amberSpent", GameReflection.PublicInstance);
            }
        }

        private static void CacheBlackMarketModelTypes(Assembly assembly)
        {
            var modelType = assembly.GetType("Eremite.Buildings.BlackMarketModel");
            if (modelType != null)
            {
                _bmmRerollPriceField = modelType.GetField("rerollPrice", GameReflection.PublicInstance);
                _bmmRerollCooldownField = modelType.GetField("rerollCooldown", GameReflection.PublicInstance);
            }

            // GoodRef.ToGood()
            var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
            if (goodRefType != null)
            {
                _goodRefToGoodMethod = goodRefType.GetMethod("ToGood", Type.EmptyTypes);
            }
        }

        private static void CacheOfferStateTypes(Assembly assembly)
        {
            var offerStateType = assembly.GetType("Eremite.Buildings.BlackMarketOfferState");
            if (offerStateType != null)
            {
                _bmosBoughtField = offerStateType.GetField("bought", GameReflection.PublicInstance);
                _bmosGoodField = offerStateType.GetField("good", GameReflection.PublicInstance);
                _bmosBuyPriceField = offerStateType.GetField("buyPrice", GameReflection.PublicInstance);
                _bmosCreditPriceField = offerStateType.GetField("creditPrice", GameReflection.PublicInstance);
                _bmosBuyRatingField = offerStateType.GetField("buyRating", GameReflection.PublicInstance);
                _bmosCreditRatingField = offerStateType.GetField("creditRating", GameReflection.PublicInstance);
                _bmosPaymentModelField = offerStateType.GetField("paymentModel", GameReflection.PublicInstance);
                _bmosEndTimeField = offerStateType.GetField("endTime", GameReflection.PublicInstance);
            }
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

        private static void CachePaymentTypes(Assembly assembly)
        {
            var paymentEffectModelType = assembly.GetType("Eremite.Model.Effects.Payment.PaymentEffectModel");
            if (paymentEffectModelType != null)
            {
                _pemSeasonsToPayField = paymentEffectModelType.GetField("seasonsToPay", GameReflection.PublicInstance);
            }

            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsGetEffectMethod = settingsType.GetMethod("GetEffect", new Type[] { typeof(string) });
            }
        }

        private static void CacheServiceProperties(Assembly assembly)
        {
            var gsType = assembly.GetType("Eremite.Services.IGameServices");
            if (gsType != null)
            {
                _gsStorageServiceProperty = gsType.GetProperty("StorageService", GameReflection.PublicInstance);
                _gsCalendarServiceProperty = gsType.GetProperty("CalendarService", GameReflection.PublicInstance);
                _gsGameTimeServiceProperty = gsType.GetProperty("GameTimeService", GameReflection.PublicInstance);
            }
        }

        private static void CacheCalendarTypes(Assembly assembly)
        {
            var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
            if (calendarServiceType != null)
            {
                _calGameDateProperty = calendarServiceType.GetProperty("GameDate", GameReflection.PublicInstance);
            }

            var gameDateType = assembly.GetType("Eremite.Model.State.GameDate");
            if (gameDateType != null)
            {
                _gdYearField = gameDateType.GetField("year", GameReflection.PublicInstance);
                _gdSeasonField = gameDateType.GetField("season", GameReflection.PublicInstance);
                _gdAddSeasonsMethod = gameDateType.GetMethod("AddSeasons", new Type[] { typeof(int) });
            }
        }

        private static void CacheStorageTypes(Assembly assembly)
        {
            var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
            if (storageServiceType != null)
            {
                _ssMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
            }

            var storageType = assembly.GetType("Eremite.Buildings.Storage");
            if (storageType != null)
            {
                var goodType = assembly.GetType("Eremite.Model.Good");
                if (goodType != null)
                {
                    _storageIsAvailableMethod = storageType.GetMethod("IsAvailable", new Type[] { goodType });
                }
            }
        }

        private static void CacheGameTimeTypes(Assembly assembly)
        {
            var gameTimeServiceType = assembly.GetType("Eremite.Services.IGameTimeService");
            if (gameTimeServiceType != null)
            {
                _gtsTimeProperty = gameTimeServiceType.GetProperty("Time", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // SERVICE ACCESSORS (fresh each call)
        // ========================================

        private static object GetStorageService()
        {
            EnsureCached();
            return GameReflection.GetService(_gsStorageServiceProperty);
        }

        private static object GetCalendarService()
        {
            EnsureCached();
            return GameReflection.GetService(_gsCalendarServiceProperty);
        }

        private static float GetGameTime()
        {
            EnsureCached();
            var gts = GameReflection.GetService(_gsGameTimeServiceProperty);
            if (gts == null || _gtsTimeProperty == null) return 0f;
            try { return (float)_gtsTimeProperty.GetValue(gts); }
            catch { return 0f; }
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Check if the given popup is a BlackMarketPopup.
        /// </summary>
        public static bool IsBlackMarketPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_blackMarketPopupType == null) return false;
            return _blackMarketPopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Get the BlackMarket instance from the popup.
        /// </summary>
        public static object GetBlackMarket(object popup)
        {
            EnsureCached();
            if (popup == null || _bmpBlackMarketField == null) return null;
            try { return _bmpBlackMarketField.GetValue(popup); }
            catch { return null; }
        }

        /// <summary>
        /// Get the NPC flavor text for the Black Market.
        /// </summary>
        public static string GetFlavorText()
        {
            // Static text as per plan
            return "Fain, Syndicate Representative: \"Many greetings, Viceroy. Running low on wood again, are we? I'm sure we can arrange something...\"";
        }

        /// <summary>
        /// Get all offers from the Black Market.
        /// </summary>
        public static List<OfferInfo> GetOffers(object blackMarket)
        {
            EnsureCached();
            var result = new List<OfferInfo>();

            if (blackMarket == null) return result;

            try
            {
                var state = _bmStateField?.GetValue(blackMarket);
                if (state == null) return result;

                var offers = _bmsOffersField?.GetValue(state) as IList;
                if (offers == null) return result;

                foreach (var offer in offers)
                {
                    if (offer == null) continue;

                    var info = new OfferInfo { State = offer };

                    // Get bought status
                    var boughtObj = _bmosBoughtField?.GetValue(offer);
                    info.Bought = boughtObj is bool b && b;

                    if (!info.Bought)
                    {
                        // Get good info
                        var good = _bmosGoodField?.GetValue(offer);
                        if (good != null)
                        {
                            var goodNameRaw = _goodNameField?.GetValue(good) as string;
                            info.GoodName = GameReflection.GetGoodDisplayName(goodNameRaw);
                            var amountObj = _goodAmountField?.GetValue(good);
                            info.GoodAmount = amountObj is int amt ? amt : 0;
                        }

                        // Get buy price
                        var buyPrice = _bmosBuyPriceField?.GetValue(offer);
                        if (buyPrice != null)
                        {
                            var amountObj = _goodAmountField?.GetValue(buyPrice);
                            info.BuyPrice = amountObj is int amt ? amt : 0;
                        }

                        // Get credit price
                        var creditPrice = _bmosCreditPriceField?.GetValue(offer);
                        if (creditPrice != null)
                        {
                            var amountObj = _goodAmountField?.GetValue(creditPrice);
                            info.CreditPrice = amountObj is int amt ? amt : 0;
                        }

                        // Get ratings
                        var buyRatingObj = _bmosBuyRatingField?.GetValue(offer);
                        if (buyRatingObj != null)
                        {
                            int ratingInt = (int)buyRatingObj;
                            info.BuyRating = ratingInt >= 0 && ratingInt < _ratingLabels.Length
                                ? _ratingLabels[ratingInt] : "unknown";
                        }

                        var creditRatingObj = _bmosCreditRatingField?.GetValue(offer);
                        if (creditRatingObj != null)
                        {
                            int ratingInt = (int)creditRatingObj;
                            info.CreditRating = ratingInt >= 0 && ratingInt < _ratingLabels.Length
                                ? _ratingLabels[ratingInt] : "unknown";
                        }

                        // Get time left
                        if (_bmGetTimeLeftForMethod != null)
                        {
                            _args1[0] = offer;
                            var timeLeftObj = _bmGetTimeLeftForMethod.Invoke(blackMarket, _args1);
                            info.TimeLeft = timeLeftObj is float t ? t : 0f;
                        }

                        // Get payment terms
                        info.PaymentTerms = GetPaymentTerms(offer);
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.GetOffers failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get payment terms for an offer (e.g., "Year III Clearance").
        /// </summary>
        private static string GetPaymentTerms(object offer)
        {
            try
            {
                var paymentModelName = _bmosPaymentModelField?.GetValue(offer) as string;
                if (string.IsNullOrEmpty(paymentModelName)) return "";

                var settings = GameReflection.GetSettings();
                if (settings == null || _settingsGetEffectMethod == null) return "";

                _args1[0] = paymentModelName;
                var paymentModel = _settingsGetEffectMethod.Invoke(settings, _args1);
                if (paymentModel == null) return "";

                var seasonsToPayObj = _pemSeasonsToPayField?.GetValue(paymentModel);
                int seasonsToPay = seasonsToPayObj is int s ? s : 1;

                // Get current date and add seasons
                var calendarService = GetCalendarService();
                if (calendarService == null || _calGameDateProperty == null) return "";

                var gameDate = _calGameDateProperty.GetValue(calendarService);
                if (gameDate == null) return "";

                // Clone the date by getting its values
                var yearObj = _gdYearField?.GetValue(gameDate);
                var seasonObj = _gdSeasonField?.GetValue(gameDate);

                int year = yearObj is int y ? y : 1;
                int season = seasonObj is int se ? se : 0;

                // Add seasons + 1 (as per game logic in BlackMarketOfferSlot.GetPaymentDate)
                int totalSeasons = season + seasonsToPay + 1;
                year += totalSeasons / 3;
                season = totalSeasons % 3;

                string seasonName = season >= 0 && season < _seasonNames.Length ? _seasonNames[season] : "Unknown";
                string yearRoman = YearToRoman(year);

                return $"Year {yearRoman} {seasonName}";
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.GetPaymentTerms failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Buy an offer with immediate payment.
        /// </summary>
        public static bool Buy(object blackMarket, object offer)
        {
            EnsureCached();
            if (blackMarket == null || offer == null || _bmBuyMethod == null) return false;

            try
            {
                _args1[0] = offer;
                _bmBuyMethod.Invoke(blackMarket, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.Buy failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Buy an offer on credit.
        /// </summary>
        public static bool BuyOnCredit(object blackMarket, object offer)
        {
            EnsureCached();
            if (blackMarket == null || offer == null || _bmBuyOnCreditMethod == null) return false;

            try
            {
                _args1[0] = offer;
                _bmBuyOnCreditMethod.Invoke(blackMarket, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.BuyOnCredit failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reroll all offers.
        /// </summary>
        public static bool Reroll(object blackMarket)
        {
            EnsureCached();
            if (blackMarket == null || _bmRerollMethod == null) return false;

            try
            {
                _bmRerollMethod.Invoke(blackMarket, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.Reroll failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if reroll is on cooldown.
        /// </summary>
        public static bool IsRerollOnCooldown(object blackMarket)
        {
            EnsureCached();
            if (blackMarket == null || _bmIsRerollOnCooldownMethod == null) return true;

            try
            {
                var result = _bmIsRerollOnCooldownMethod.Invoke(blackMarket, null);
                return result is bool b && b;
            }
            catch { return true; }
        }

        /// <summary>
        /// Get time left until reroll is available.
        /// </summary>
        public static float GetRerollTimeLeft(object blackMarket)
        {
            EnsureCached();
            if (blackMarket == null) return 0f;

            try
            {
                var state = _bmStateField?.GetValue(blackMarket);
                var model = _bmModelField?.GetValue(blackMarket);
                if (state == null || model == null) return 0f;

                var lastRerollObj = _bmsLastRerollField?.GetValue(state);
                var cooldownObj = _bmmRerollCooldownField?.GetValue(model);

                float lastReroll = lastRerollObj is float lr ? lr : 0f;
                float cooldown = cooldownObj is float cd ? cd : 120f;

                float gameTime = GetGameTime();
                float endTime = lastReroll + cooldown;

                return Mathf.Max(0f, endTime - gameTime);
            }
            catch { return 0f; }
        }

        /// <summary>
        /// Get reroll price (amber amount).
        /// </summary>
        public static int GetRerollPrice(object blackMarket)
        {
            EnsureCached();
            if (blackMarket == null) return 0;

            try
            {
                var model = _bmModelField?.GetValue(blackMarket);
                if (model == null) return 0;

                var rerollPrice = _bmmRerollPriceField?.GetValue(model);
                if (rerollPrice == null || _goodRefToGoodMethod == null) return 0;

                var good = _goodRefToGoodMethod.Invoke(rerollPrice, null);
                if (good == null) return 0;

                var amountObj = _goodAmountField?.GetValue(good);
                return amountObj is int amt ? amt : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Check if player can afford the reroll price.
        /// </summary>
        public static bool CanAffordReroll(object blackMarket)
        {
            EnsureCached();
            if (blackMarket == null) return false;

            try
            {
                var model = _bmModelField?.GetValue(blackMarket);
                if (model == null) return false;

                var rerollPrice = _bmmRerollPriceField?.GetValue(model);
                if (rerollPrice == null || _goodRefToGoodMethod == null) return false;

                var good = _goodRefToGoodMethod.Invoke(rerollPrice, null);
                return CanAffordGood(good);
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if player can afford a specific offer's buy price.
        /// </summary>
        public static bool CanAffordBuy(object offer)
        {
            EnsureCached();
            if (offer == null) return false;

            try
            {
                var buyPrice = _bmosBuyPriceField?.GetValue(offer);
                return CanAffordGood(buyPrice);
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if player can afford a Good.
        /// </summary>
        private static bool CanAffordGood(object good)
        {
            if (good == null) return false;

            try
            {
                var storageService = GetStorageService();
                if (storageService == null || _ssMainProperty == null) return false;

                var mainStorage = _ssMainProperty.GetValue(storageService);
                if (mainStorage == null || _storageIsAvailableMethod == null) return false;

                _args1[0] = good;
                var result = _storageIsAvailableMethod.Invoke(mainStorage, _args1);
                return result is bool b && b;
            }
            catch { return false; }
        }

        // ========================================
        // FORMATTING
        // ========================================

        /// <summary>
        /// Format seconds into mm:ss or hh:mm:ss string.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0:00";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            return string.Format("{0}:{1:D2}", (int)ts.TotalMinutes, ts.Seconds);
        }

        /// <summary>
        /// Convert year number to Roman numeral string.
        /// </summary>
        public static string YearToRoman(int year)
        {
            if (year <= 0) return year.ToString();

            var result = new System.Text.StringBuilder();
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] numerals = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

            for (int i = 0; i < values.Length; i++)
            {
                while (year >= values[i])
                {
                    result.Append(numerals[i]);
                    year -= values[i];
                }
            }
            return result.ToString();
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(BlackMarketReflection), "BlackMarketReflection");
        }
    }
}
