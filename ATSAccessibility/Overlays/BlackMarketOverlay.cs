using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the BlackMarketPopup.
	/// Level 0 = main list (header + reroll + offers).
	/// Level 1 = sub-menu (buy now / buy on credit).
	/// </summary>
	public class BlackMarketOverlay: MenuBase {
		private enum ItemType { Header, Reroll, Offer }

		private class NavItem {
			public ItemType Type;
			public BlackMarketReflection.OfferInfo? Offer;  // For Offer type
			public string Label;
			public string SearchName;  // Name for type-ahead (offers only)
		}

		// State
		private object _blackMarket;

		// Data
		private List<NavItem> _items = new List<NavItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.black_market.title");
		protected override string EmptyMessage => Strings.Get("overlay.black_market.empty");

		protected override int GetItemCount() {
			if (Level == 0)
				return _items.Count;
			else
				return 2;  // Buy now / Buy on credit
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _items.Count)
					return _items[index].Label;
				return null;
			} else {
				// Sub-menu: format buy/credit info from parent offer
				int parentIdx = _indices[0];
				if (parentIdx < 0 || parentIdx >= _items.Count) return null;

				var item = _items[parentIdx];
				if (item.Type != ItemType.Offer || !item.Offer.HasValue) return null;

				var offer = item.Offer.Value;
				if (index == 0) {
					// Buy now
					bool canAfford = BlackMarketReflection.CanAffordBuy(offer.State);
					string affordStr = canAfford ? "" : Strings.Get("common.suffix_cannot_afford");
					return Strings.Get("overlay.black_market.buy_now", offer.BuyPrice, offer.BuyRating, affordStr);
				} else {
					// Buy on credit
					string paymentTerms = !string.IsNullOrEmpty(offer.PaymentTerms)
						? Strings.Get("overlay.black_market.buy_credit.terms", offer.PaymentTerms)
						: "";
					return Strings.Get("overlay.black_market.buy_credit", offer.CreditPrice, offer.CreditRating, paymentTerms);
				}
			}
		}

		protected override void RefreshData() {
			_items.Clear();

			// Add header item (NPC flavor text)
			_items.Add(new NavItem {
				Type = ItemType.Header,
				Label = BlackMarketReflection.GetFlavorText()
			});

			// Add reroll button
			_items.Add(new NavItem {
				Type = ItemType.Reroll,
				Label = BuildRerollLabel()
			});

			// Get offers
			var offers = BlackMarketReflection.GetOffers(_blackMarket);
			foreach (var offer in offers) {
				if (!offer.Bought) {
					_items.Add(new NavItem {
						Type = ItemType.Offer,
						Offer = offer,
						Label = BuildOfferLabel(offer),
						SearchName = offer.GoodName
					});
				}
			}

			Debug.Log($"[ATSAccessibility] BlackMarketOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _items.Count) return EnterAction.None;

				var item = _items[index];
				switch (item.Type) {
					case ItemType.Header:
						return EnterAction.Action;
					case ItemType.Reroll:
						return EnterAction.Action;
					case ItemType.Offer:
						return EnterAction.DrillDown;
					default:
						return EnterAction.None;
				}
			} else {
				return EnterAction.Action;
			}
		}

		protected override void OnAction(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _items.Count) return;

				var item = _items[index];
				switch (item.Type) {
					case ItemType.Header:
						AnnounceCurrentItem();
						break;
					case ItemType.Reroll:
						ExecuteReroll();
						break;
				}
			} else {
				ExecuteSubMenuAction(index);
			}
		}

		// Default OnEscape behavior: GoBack if level > 0, PassThrough if level 0

		protected override void StorePopup(object popup) {
			_blackMarket = BlackMarketReflection.GetBlackMarket(popup);
			if (_blackMarket == null) {
				Debug.LogWarning("[ATSAccessibility] BlackMarketOverlay: Could not get BlackMarket from popup");
			}
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count > 0)
				return Strings.Get("overlay.black_market.open", _items[0].Label);
			return Strings.Get("overlay.black_market.open", EmptyMessage);
		}

		protected override void OnClosed() {
			_blackMarket = null;
			_items.Clear();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override int SearchItemCount => Level == 0 ? _items.Count : 0;

		protected override string GetSearchName(int index) {
			if (Level == 0 && index >= 0 && index < _items.Count)
				return _items[index].Type == ItemType.Offer ? _items[index].SearchName : null;
			return null;
		}

		// ========================================
		// DATA HELPERS
		// ========================================

		private string BuildRerollLabel() {
			int price = BlackMarketReflection.GetRerollPrice(_blackMarket);

			if (BlackMarketReflection.IsRerollOnCooldown(_blackMarket)) {
				float timeLeft = BlackMarketReflection.GetRerollTimeLeft(_blackMarket);
				string timeStr = FormattingUtils.FormatTime(timeLeft);
				return Strings.Get("overlay.black_market.reroll_cooldown", timeStr);
			}

			if (!BlackMarketReflection.CanAffordReroll(_blackMarket)) {
				return Strings.Get("overlay.black_market.reroll_cannot_afford", price);
			}

			return Strings.Get("overlay.black_market.reroll_normal", price);
		}

		private string BuildOfferLabel(BlackMarketReflection.OfferInfo offer) {
			var parts = new List<string>();

			// Good name and amount
			parts.Add(Strings.Get("overlay.black_market.offer.name", offer.GoodName, offer.GoodAmount));

			// Buy price with rating
			parts.Add(Strings.Get("overlay.black_market.offer.buy", offer.BuyPrice, offer.BuyRating));

			// Credit price with rating
			parts.Add(Strings.Get("overlay.black_market.offer.credit", offer.CreditPrice, offer.CreditRating));

			// Time left
			string timeStr = FormattingUtils.FormatTime(offer.TimeLeft);
			parts.Add(Strings.Get("overlay.black_market.offer.time", timeStr));

			return string.Join(", ", parts);
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void ExecuteReroll() {
			if (BlackMarketReflection.IsRerollOnCooldown(_blackMarket)) {
				float timeLeft = BlackMarketReflection.GetRerollTimeLeft(_blackMarket);
				string timeStr = FormattingUtils.FormatTime(timeLeft);
				Speech.Say(Strings.Get("overlay.black_market.reroll.cooldown_msg", timeStr));
				SoundManager.PlayFailed();
				return;
			}

			if (!BlackMarketReflection.CanAffordReroll(_blackMarket)) {
				Speech.Say(Strings.Get("common.cannot_afford"));
				SoundManager.PlayFailed();
				return;
			}

			if (BlackMarketReflection.Reroll(_blackMarket)) {
				SoundManager.PlayReroll();
				RefreshData();

				// Announce "Rerolled" then first offer
				if (_items.Count > 2)  // Header + Reroll + at least one offer
				{
					CurrentIndex = 2;  // First offer
					Speech.Say(Strings.Get("overlay.black_market.reroll.done_with_offer", _items[2].Label));
				} else {
					Speech.Say(Strings.Get("overlay.black_market.reroll.done"));
				}
			} else {
				Speech.Say(Strings.Get("overlay.black_market.reroll.failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// SUB-MENU
		// ========================================

		private void ExitSubMenuAfterPurchase() {
			SetLevel(0);
			RefreshData();
			if (_indices[0] >= _items.Count)
				_indices[0] = _items.Count > 0 ? _items.Count - 1 : 0;
			if (_items.Count > 0)
				AnnounceCurrentItem();
		}

		private void ExecuteSubMenuAction(int index) {
			if (_blackMarket == null) return;

			int parentIdx = _indices[0];
			if (parentIdx < 0 || parentIdx >= _items.Count) return;

			var item = _items[parentIdx];
			if (item.Type != ItemType.Offer || !item.Offer.HasValue) return;

			var offer = item.Offer.Value;

			if (offer.State == null) {
				Speech.Say(Strings.Get("overlay.black_market.buy.invalid_offer"));
				SoundManager.PlayFailed();
				return;
			}

			if (index == 0) {
				// Buy now
				if (!BlackMarketReflection.CanAffordBuy(offer.State)) {
					Speech.Say(Strings.Get("common.cannot_afford"));
					SoundManager.PlayFailed();
					return;
				}

				if (BlackMarketReflection.Buy(_blackMarket, offer.State)) {
					SoundManager.PlayTraderTransactionCompleted();
					Speech.Say(Strings.Get("overlay.black_market.buy.purchased", offer.GoodName));
					ExitSubMenuAfterPurchase();
				} else {
					Speech.Say(Strings.Get("common.purchase_failed"));
					SoundManager.PlayFailed();
				}
			} else {
				// Buy on credit
				if (BlackMarketReflection.BuyOnCredit(_blackMarket, offer.State)) {
					SoundManager.PlayTraderTransactionCompleted();
					Speech.Say(Strings.Get("overlay.black_market.buy.purchased_credit", offer.GoodName));
					ExitSubMenuAfterPurchase();
				} else {
					Speech.Say(Strings.Get("common.purchase_failed"));
					SoundManager.PlayFailed();
				}
			}
		}
	}
}
