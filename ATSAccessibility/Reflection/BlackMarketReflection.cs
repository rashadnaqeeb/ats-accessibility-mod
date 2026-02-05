using ATSAccessibility.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to Black Market building internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, buildings) - they are destroyed on scene change
	/// </summary>
	public static class BlackMarketReflection {
		// ========================================
		// OFFER INFO STRUCT
		// ========================================

		public struct OfferInfo {
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

		// Rating labels
		private static readonly string[] _ratingLabels = { "good deal", "regular price", "bad deal" };

		// Season names
		private static readonly string[] _seasonNames = { "Drizzle", "Clearance", "Storm" };

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("BlackMarketReflection", assembly => {
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
			});
		}

		private static void CachePopupTypes(Assembly assembly) {
			_blackMarketPopupType = assembly.GetType("Eremite.Buildings.UI.BlackMarkets.BlackMarketPopup");
			if (_blackMarketPopupType != null) {
				_bmpBlackMarketField = _blackMarketPopupType.GetField("blackMarket", GameReflection.NonPublicInstance);
			}
		}

		private static void CacheBlackMarketTypes(Assembly assembly) {
			var blackMarketType = assembly.GetType("Eremite.Buildings.BlackMarket");
			if (blackMarketType != null) {
				_bmStateField = blackMarketType.GetField("state", GameReflection.PublicInstance);
				_bmModelField = blackMarketType.GetField("model", GameReflection.PublicInstance);
				_bmRerollMethod = blackMarketType.GetMethod("Reroll", Type.EmptyTypes);
				_bmIsRerollOnCooldownMethod = blackMarketType.GetMethod("IsRerollOnCooldown", Type.EmptyTypes);

				var offerStateType = assembly.GetType("Eremite.Buildings.BlackMarketOfferState");
				if (offerStateType != null) {
					_bmBuyMethod = blackMarketType.GetMethod("Buy", new Type[] { offerStateType });
					_bmBuyOnCreditMethod = blackMarketType.GetMethod("BuyOnCredit", new Type[] { offerStateType });
					_bmGetTimeLeftForMethod = blackMarketType.GetMethod("GetTimeLeftFor", new Type[] { offerStateType });
				}
			}
		}

		private static void CacheBlackMarketStateTypes(Assembly assembly) {
			var stateType = assembly.GetType("Eremite.Buildings.BlackMarketState");
			if (stateType != null) {
				_bmsOffersField = stateType.GetField("offers", GameReflection.PublicInstance);
				_bmsLastRerollField = stateType.GetField("lastReroll", GameReflection.PublicInstance);
				_bmsAmberSpentField = stateType.GetField("amberSpent", GameReflection.PublicInstance);
			}
		}

		private static void CacheBlackMarketModelTypes(Assembly assembly) {
			var modelType = assembly.GetType("Eremite.Buildings.BlackMarketModel");
			if (modelType != null) {
				_bmmRerollPriceField = modelType.GetField("rerollPrice", GameReflection.PublicInstance);
				_bmmRerollCooldownField = modelType.GetField("rerollCooldown", GameReflection.PublicInstance);
			}

			// GoodRef.ToGood()
			var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
			if (goodRefType != null) {
				_goodRefToGoodMethod = goodRefType.GetMethod("ToGood", Type.EmptyTypes);
			}
		}

		private static void CacheOfferStateTypes(Assembly assembly) {
			var offerStateType = assembly.GetType("Eremite.Buildings.BlackMarketOfferState");
			if (offerStateType != null) {
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

		private static void CacheGoodTypes(Assembly assembly) {
			var goodType = assembly.GetType("Eremite.Model.Good");
			if (goodType != null) {
				_goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
				_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
			}
		}

		private static void CachePaymentTypes(Assembly assembly) {
			var paymentEffectModelType = assembly.GetType("Eremite.Model.Effects.Payment.PaymentEffectModel");
			if (paymentEffectModelType != null) {
				_pemSeasonsToPayField = paymentEffectModelType.GetField("seasonsToPay", GameReflection.PublicInstance);
			}

			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetEffectMethod = settingsType.GetMethod("GetEffect", new Type[] { typeof(string) });
			}
		}

		private static void CacheServiceProperties(Assembly assembly) {
			var gsType = assembly.GetType("Eremite.Services.IGameServices");
			if (gsType != null) {
				_gsStorageServiceProperty = gsType.GetProperty("StorageService", GameReflection.PublicInstance);
				_gsCalendarServiceProperty = gsType.GetProperty("CalendarService", GameReflection.PublicInstance);
				_gsGameTimeServiceProperty = gsType.GetProperty("GameTimeService", GameReflection.PublicInstance);
			}
		}

		private static void CacheCalendarTypes(Assembly assembly) {
			var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
			if (calendarServiceType != null) {
				_calGameDateProperty = calendarServiceType.GetProperty("GameDate", GameReflection.PublicInstance);
			}

			var gameDateType = assembly.GetType("Eremite.Model.State.GameDate");
			if (gameDateType != null) {
				_gdYearField = gameDateType.GetField("year", GameReflection.PublicInstance);
				_gdSeasonField = gameDateType.GetField("season", GameReflection.PublicInstance);
				_gdAddSeasonsMethod = gameDateType.GetMethod("AddSeasons", new Type[] { typeof(int) });
			}
		}

		private static void CacheStorageTypes(Assembly assembly) {
			var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
			if (storageServiceType != null) {
				_ssMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
			}

			var storageType = assembly.GetType("Eremite.Buildings.Storage");
			if (storageType != null) {
				var goodType = assembly.GetType("Eremite.Model.Good");
				if (goodType != null) {
					_storageIsAvailableMethod = storageType.GetMethod("IsAvailable", new Type[] { goodType });
				}
			}
		}

		private static void CacheGameTimeTypes(Assembly assembly) {
			var gameTimeServiceType = assembly.GetType("Eremite.Services.IGameTimeService");
			if (gameTimeServiceType != null) {
				_gtsTimeProperty = gameTimeServiceType.GetProperty("Time", GameReflection.PublicInstance);
			}
		}

		// ========================================
		// SERVICE ACCESSORS (fresh each call)
		// ========================================

		private static object GetStorageService() {
			EnsureCached();
			return GameReflection.GetService(_gsStorageServiceProperty);
		}

		private static object GetCalendarService() {
			EnsureCached();
			return GameReflection.GetService(_gsCalendarServiceProperty);
		}

		private static float GetGameTime() {
			EnsureCached();
			var gts = GameReflection.GetService(_gsGameTimeServiceProperty);
			return ReflectionHelper.GetPropFloat(_gtsTimeProperty, gts);
		}

		// ========================================
		// PUBLIC API
		// ========================================

		/// <summary>
		/// Check if the given popup is a BlackMarketPopup.
		/// </summary>
		public static bool IsBlackMarketPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			if (_blackMarketPopupType == null) return false;
			return _blackMarketPopupType.IsInstanceOfType(popup);
		}

		/// <summary>
		/// Get the BlackMarket instance from the popup.
		/// </summary>
		public static object GetBlackMarket(object popup) {
			EnsureCached();
			return ReflectionHelper.GetField(_bmpBlackMarketField, popup);
		}

		/// <summary>
		/// Get the NPC flavor text for the Black Market.
		/// </summary>
		public static string GetFlavorText() {
			// Static text as per plan
			return "Fain, Syndicate Representative: \"Many greetings, Viceroy. Running low on wood again, are we? I'm sure we can arrange something...\"";
		}

		/// <summary>
		/// Get all offers from the Black Market.
		/// </summary>
		public static List<OfferInfo> GetOffers(object blackMarket) {
			EnsureCached();
			var result = new List<OfferInfo>();

			if (blackMarket == null) return result;

			try {
				var state = ReflectionHelper.GetField(_bmStateField, blackMarket);
				if (state == null) return result;

				var offers = ReflectionHelper.GetList(_bmsOffersField, state);
				if (offers == null) return result;

				foreach (var offer in offers) {
					if (offer == null) continue;

					var info = new OfferInfo { State = offer };

					// Get bought status
					info.Bought = ReflectionHelper.GetBool(_bmosBoughtField, offer);

					if (!info.Bought) {
						// Get good info
						var good = ReflectionHelper.GetField(_bmosGoodField, offer);
						if (good != null) {
							var goodNameRaw = ReflectionHelper.GetString(_goodNameField, good);
							info.GoodName = GameReflection.GetGoodDisplayName(goodNameRaw);
							info.GoodAmount = ReflectionHelper.GetInt(_goodAmountField, good);
						}

						// Get buy price
						var buyPrice = ReflectionHelper.GetField(_bmosBuyPriceField, offer);
						if (buyPrice != null) {
							info.BuyPrice = ReflectionHelper.GetInt(_goodAmountField, buyPrice);
						}

						// Get credit price
						var creditPrice = ReflectionHelper.GetField(_bmosCreditPriceField, offer);
						if (creditPrice != null) {
							info.CreditPrice = ReflectionHelper.GetInt(_goodAmountField, creditPrice);
						}

						// Get ratings
						int buyRatingInt = ReflectionHelper.GetEnum(_bmosBuyRatingField, offer);
						info.BuyRating = buyRatingInt >= 0 && buyRatingInt < _ratingLabels.Length
							? _ratingLabels[buyRatingInt] : "unknown";

						int creditRatingInt = ReflectionHelper.GetEnum(_bmosCreditRatingField, offer);
						info.CreditRating = creditRatingInt >= 0 && creditRatingInt < _ratingLabels.Length
							? _ratingLabels[creditRatingInt] : "unknown";

						// Get time left
						info.TimeLeft = ReflectionHelper.InvokeFloat(_bmGetTimeLeftForMethod, blackMarket, offer);

						// Get payment terms
						info.PaymentTerms = GetPaymentTerms(offer);
					}

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.GetOffers failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get payment terms for an offer (e.g., "Year III Clearance").
		/// </summary>
		private static string GetPaymentTerms(object offer) {
			try {
				var paymentModelName = ReflectionHelper.GetString(_bmosPaymentModelField, offer);
				if (string.IsNullOrEmpty(paymentModelName)) return "";

				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsGetEffectMethod == null) return "";

				var paymentModel = ReflectionHelper.Invoke(_settingsGetEffectMethod, settings, paymentModelName);
				if (paymentModel == null) return "";

				int seasonsToPay = ReflectionHelper.GetInt(_pemSeasonsToPayField, paymentModel);
				if (seasonsToPay == 0) seasonsToPay = 1;

				// Get current date and add seasons
				var calendarService = GetCalendarService();
				if (calendarService == null || _calGameDateProperty == null) return "";

				var gameDate = ReflectionHelper.GetProp(_calGameDateProperty, calendarService);
				if (gameDate == null) return "";

				// Clone the date by getting its values
				int year = ReflectionHelper.GetInt(_gdYearField, gameDate);
				if (year == 0) year = 1;
				int season = ReflectionHelper.GetInt(_gdSeasonField, gameDate);

				// Add seasons + 1 (as per game logic in BlackMarketOfferSlot.GetPaymentDate)
				int totalSeasons = season + seasonsToPay + 1;
				year += totalSeasons / 3;
				season = totalSeasons % 3;

				string seasonName = season >= 0 && season < _seasonNames.Length ? _seasonNames[season] : "Unknown";
				string yearRoman = FormattingUtils.YearToRoman(year);

				return $"Year {yearRoman} {seasonName}";
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] BlackMarketReflection.GetPaymentTerms failed: {ex.Message}");
				return "";
			}
		}

		/// <summary>
		/// Buy an offer with immediate payment.
		/// </summary>
		public static bool Buy(object blackMarket, object offer) {
			EnsureCached();
			if (blackMarket == null || offer == null || _bmBuyMethod == null) return false;
			return ReflectionHelper.InvokeVoid(_bmBuyMethod, blackMarket, offer);
		}

		/// <summary>
		/// Buy an offer on credit.
		/// </summary>
		public static bool BuyOnCredit(object blackMarket, object offer) {
			EnsureCached();
			if (blackMarket == null || offer == null || _bmBuyOnCreditMethod == null) return false;
			return ReflectionHelper.InvokeVoid(_bmBuyOnCreditMethod, blackMarket, offer);
		}

		/// <summary>
		/// Reroll all offers.
		/// </summary>
		public static bool Reroll(object blackMarket) {
			EnsureCached();
			if (blackMarket == null || _bmRerollMethod == null) return false;
			return ReflectionHelper.InvokeVoid(_bmRerollMethod, blackMarket);
		}

		/// <summary>
		/// Check if reroll is on cooldown.
		/// </summary>
		public static bool IsRerollOnCooldown(object blackMarket) {
			EnsureCached();
			if (blackMarket == null || _bmIsRerollOnCooldownMethod == null) return true;
			return ReflectionHelper.InvokeBool(_bmIsRerollOnCooldownMethod, blackMarket);
		}

		/// <summary>
		/// Get time left until reroll is available.
		/// </summary>
		public static float GetRerollTimeLeft(object blackMarket) {
			EnsureCached();
			if (blackMarket == null) return 0f;

			try {
				var state = ReflectionHelper.GetField(_bmStateField, blackMarket);
				var model = ReflectionHelper.GetField(_bmModelField, blackMarket);
				if (state == null || model == null) return 0f;

				float lastReroll = ReflectionHelper.GetFloat(_bmsLastRerollField, state);
				float cooldown = ReflectionHelper.GetFloat(_bmmRerollCooldownField, model);
				if (cooldown == 0f) cooldown = 120f;

				float gameTime = GetGameTime();
				float endTime = lastReroll + cooldown;

				return Mathf.Max(0f, endTime - gameTime);
			} catch { return 0f; }
		}

		/// <summary>
		/// Get reroll price (amber amount).
		/// </summary>
		public static int GetRerollPrice(object blackMarket) {
			EnsureCached();
			if (blackMarket == null) return 0;

			try {
				var model = ReflectionHelper.GetField(_bmModelField, blackMarket);
				if (model == null) return 0;

				var rerollPrice = ReflectionHelper.GetField(_bmmRerollPriceField, model);
				if (rerollPrice == null) return 0;

				var good = ReflectionHelper.Invoke(_goodRefToGoodMethod, rerollPrice);
				if (good == null) return 0;

				return ReflectionHelper.GetInt(_goodAmountField, good);
			} catch { return 0; }
		}

		/// <summary>
		/// Check if player can afford the reroll price.
		/// </summary>
		public static bool CanAffordReroll(object blackMarket) {
			EnsureCached();
			if (blackMarket == null) return false;

			try {
				var model = ReflectionHelper.GetField(_bmModelField, blackMarket);
				if (model == null) return false;

				var rerollPrice = ReflectionHelper.GetField(_bmmRerollPriceField, model);
				if (rerollPrice == null) return false;

				var good = ReflectionHelper.Invoke(_goodRefToGoodMethod, rerollPrice);
				return CanAffordGood(good);
			} catch { return false; }
		}

		/// <summary>
		/// Check if player can afford a specific offer's buy price.
		/// </summary>
		public static bool CanAffordBuy(object offer) {
			EnsureCached();
			if (offer == null) return false;

			try {
				var buyPrice = ReflectionHelper.GetField(_bmosBuyPriceField, offer);
				return CanAffordGood(buyPrice);
			} catch { return false; }
		}

		/// <summary>
		/// Check if player can afford a Good.
		/// </summary>
		private static bool CanAffordGood(object good) {
			if (good == null) return false;

			try {
				var storageService = GetStorageService();
				if (storageService == null) return false;

				var mainStorage = ReflectionHelper.GetProp(_ssMainProperty, storageService);
				if (mainStorage == null) return false;

				return ReflectionHelper.InvokeBool(_storageIsAvailableMethod, mainStorage, good);
			} catch { return false; }
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(BlackMarketReflection), "BlackMarketReflection");
		}
	}
}
