using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the PaymentsPopup (pending payments/obligations).
    /// Flat list navigation with static header text.
    /// </summary>
    public class PaymentsOverlay : MenuBase, IKeyHandler
    {
        private enum ItemType { Header, Payment }

        private class NavItem
        {
            public ItemType Type;
            public PaymentsReflection.PaymentInfo? Payment;  // For Payment type
            public string Label;
        }

        // Data
        private object _popup;
        private List<NavItem> _items = new List<NavItem>();

        // Header text (Zhera Mossback quote)
        private const string HEADER_TEXT = "Zhera Mossback, Assistant to the Royal Treasurer: " +
            "\"May the sun shine on you, Viceroy! I was sent here to help you keep track of all " +
            "those annoying payments and obligations. Another pair of eyes on your Exploration Tax " +
            "forms might come in handy.\"";

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Payments";
        protected override string EmptyMessage => "No payments due";

        protected override int GetItemCount() => _items.Count;

        protected override string GetLabel(int index)
        {
            if (index >= 0 && index < _items.Count)
                return _items[index].Label;
            return null;
        }

        protected override void RefreshData()
        {
            _items.Clear();

            // Add header item
            _items.Add(new NavItem
            {
                Type = ItemType.Header,
                Label = HEADER_TEXT
            });

            // Get payments
            var payments = PaymentsReflection.GetPayments();
            foreach (var payment in payments)
            {
                _items.Add(new NavItem
                {
                    Type = ItemType.Payment,
                    Payment = payment,
                    Label = BuildPaymentLabel(payment)
                });
            }

            Debug.Log($"[ATSAccessibility] PaymentsOverlay refreshed: {payments.Count} payments");
        }

        protected override EnterAction OnEnter(int index) => EnterAction.Action;

        protected override void OnAction(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            var item = _items[index];
            if (item.Type != ItemType.Payment || !item.Payment.HasValue)
            {
                AnnounceCurrentItem();
                return;
            }

            var payment = item.Payment.Value;
            if (!payment.CanPay)
            {
                Speech.Say("Cannot pay");
                SoundManager.PlayFailed();
                return;
            }

            if (PaymentsReflection.Pay(payment.State))
            {
                SoundManager.PlayTraderTransactionCompleted();
                Speech.Say("Paid");

                RefreshData();

                if (CurrentIndex >= _items.Count)
                    CurrentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

                if (_items.Count > 1)
                    AnnounceCurrentItem();
                else
                    Speech.Say("No payments due");
            }
            else
            {
                Speech.Say("Failed to pay");
                SoundManager.PlayFailed();
            }
        }

        protected override void OnSpace(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            var item = _items[index];
            if (item.Type != ItemType.Payment || !item.Payment.HasValue)
            {
                AnnounceCurrentItem();
                return;
            }

            var payment = item.Payment.Value;

            // Cycle: None (0) -> Instant (1) -> End (2) -> None (0)
            int newType = (payment.AutoPaymentType + 1) % 3;

            if (PaymentsReflection.SetAutoPaymentType(payment.State, newType))
            {
                SoundManager.PlayButtonClick();
                string newLabel = PaymentsReflection.GetAutoPaymentLabel(newType);
                Speech.Say($"auto: {newLabel}");

                // Update the cached payment info
                var updatedPayment = payment;
                updatedPayment.AutoPaymentType = newType;
                item.Payment = updatedPayment;
                item.Label = BuildPaymentLabel(updatedPayment);
            }
            else
            {
                Speech.Say("Cannot change auto-payment");
                SoundManager.PlayFailed();
            }
        }

        protected override int SearchItemCount => 0; // No search for payments

        // Escape passes to game to close popup
        protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

        protected override void StorePopup(object popup)
        {
            _popup = popup;
        }

        protected override void OnClosed()
        {
            _popup = null;
            _items.Clear();
        }

        // ========================================
        // DATA HELPERS
        // ========================================

        private string BuildPaymentLabel(PaymentsReflection.PaymentInfo payment)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(payment.TypeLabel))
                parts.Add(payment.TypeLabel);
            else
                parts.Add("Payment");

            parts.Add($"{payment.GoodAmount} {payment.GoodName}");

            string yearStr = PaymentsReflection.YearToRoman(payment.DueYear);
            parts.Add($"due Year {yearStr} {payment.DueSeason}");

            string timeStr = PaymentsReflection.FormatTime(payment.TimeRemaining);
            parts.Add(timeStr);

            string autoLabel = PaymentsReflection.GetAutoPaymentLabel(payment.AutoPaymentType);
            parts.Add($"auto: {autoLabel}");

            parts.Add(payment.CanPay ? "can pay" : "cannot pay");

            return string.Join(", ", parts);
        }
    }
}
