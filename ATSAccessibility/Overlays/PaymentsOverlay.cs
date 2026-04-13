using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the PaymentsPopup (pending payments/obligations).
	/// Flat list navigation with static header text.
	/// </summary>
	public class PaymentsOverlay: MenuBase {
		private enum ItemType { Header, Payment }

		private class NavItem {
			public ItemType Type;
			public PaymentsReflection.PaymentInfo? Payment;  // For Payment type
			public string Label;
		}

		// Data
		private object _popup;
		private List<NavItem> _items = new List<NavItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.payments");
		protected override string EmptyMessage => Strings.Get("common.no_payments_due");

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			// Add header item — resolved from the game's own loca keys so NPC
			// name/title/dialogue match the transliterated form shown in the popup UI.
			_items.Add(new NavItem {
				Type = ItemType.Header,
				Label = BuildHeaderLabel()
			});

			// Get payments
			var payments = PaymentsReflection.GetPayments();
			foreach (var payment in payments) {
				_items.Add(new NavItem {
					Type = ItemType.Payment,
					Payment = payment,
					Label = BuildPaymentLabel(payment)
				});
			}

			Debug.Log($"[ATSAccessibility] PaymentsOverlay refreshed: {payments.Count} payments");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			if (item.Type != ItemType.Payment || !item.Payment.HasValue) {
				AnnounceCurrentItem();
				return;
			}

			var payment = item.Payment.Value;
			if (!payment.CanPay) {
				Speech.Say(Strings.Get("overlay.payments.cannot_pay"));
				SoundManager.PlayFailed();
				return;
			}

			if (PaymentsReflection.Pay(payment.State)) {
				SoundManager.PlayTraderTransactionCompleted();
				Speech.Say(Strings.Get("overlay.payments.paid"));

				RefreshData();

				if (CurrentIndex >= _items.Count)
					CurrentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

				if (_items.Count > 1)
					AnnounceCurrentItem();
				else
					Speech.Say(Strings.Get("common.no_payments_due"));
			} else {
				Speech.Say(Strings.Get("overlay.payments.pay_failed"));
				SoundManager.PlayFailed();
			}
		}

		protected override void OnSpace(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			if (item.Type != ItemType.Payment || !item.Payment.HasValue) {
				AnnounceCurrentItem();
				return;
			}

			var payment = item.Payment.Value;

			// Cycle: None (0) -> Instant (1) -> End (2) -> None (0)
			int newType = (payment.AutoPaymentType + 1) % 3;

			if (PaymentsReflection.SetAutoPaymentType(payment.State, newType)) {
				SoundManager.PlayButtonClick();
				string newLabel = PaymentsReflection.GetAutoPaymentLabel(newType);
				Speech.Say(Strings.Get("overlay.payments.auto_label", newLabel));

				// Update the cached payment info
				var updatedPayment = payment;
				updatedPayment.AutoPaymentType = newType;
				item.Payment = updatedPayment;
				item.Label = BuildPaymentLabel(updatedPayment);
			} else {
				Speech.Say(Strings.Get("overlay.payments.auto_cannot"));
				SoundManager.PlayFailed();
			}
		}

		protected override int SearchItemCount => 0; // No search for payments

		// Escape passes to game to close popup
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// DATA HELPERS
		// ========================================

		private static string BuildHeaderLabel() {
			const string nameKey = "GameUI_PaymentsPopup_Dialogue_Name";
			const string titleKey = "GameUI_PaymentsPopup_Dialogue_Title";
			const string textKey = "GameUI_PaymentsPopup_Dialogue_Text";

			string name = GameReflection.ResolveLocaKey(nameKey);
			string title = GameReflection.ResolveLocaKey(titleKey);
			string text = GameReflection.ResolveLocaKey(textKey);

			// ResolveLocaKey returns the key itself on lookup failure.
			bool resolved =
				!string.IsNullOrEmpty(name) && name != nameKey
				&& !string.IsNullOrEmpty(title) && title != titleKey
				&& !string.IsNullOrEmpty(text) && text != textKey;

			if (!resolved)
				return Strings.Get("overlay.payments.header_fallback");

			// Game wraps the dialogue line in literal quote marks; ru/fr use guillemets.
			text = text.Trim().Trim('"', '\u201C', '\u201D', '«', '»');
			return Strings.Get("overlay.payments.header_format", name, title, text);
		}

		private string BuildPaymentLabel(PaymentsReflection.PaymentInfo payment) {
			var parts = new List<string>();

			if (!string.IsNullOrEmpty(payment.TypeLabel))
				parts.Add(payment.TypeLabel);
			else
				parts.Add(Strings.Get("overlay.payments.default_type"));

			parts.Add(Strings.Get("overlay.payments.amount", payment.GoodAmount, payment.GoodName));

			string yearStr = FormattingUtils.YearToRoman(payment.DueYear);
			parts.Add(Strings.Get("overlay.payments.due", yearStr, payment.DueSeason));

			string timeStr = FormattingUtils.FormatTime(payment.TimeRemaining);
			parts.Add(timeStr);

			string autoLabel = PaymentsReflection.GetAutoPaymentLabel(payment.AutoPaymentType);
			parts.Add(Strings.Get("overlay.payments.auto", autoLabel));

			parts.Add(Strings.Get(payment.CanPay ? "overlay.payments.can_pay" : "overlay.payments.cannot_pay_row"));

			return string.Join(", ", parts);
		}
	}
}
