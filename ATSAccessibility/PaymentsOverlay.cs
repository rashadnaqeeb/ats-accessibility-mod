using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the PaymentsPopup (pending payments/obligations).
    /// Flat list navigation with static header text.
    /// </summary>
    public class PaymentsOverlay : IKeyHandler
    {
        private enum ItemType { Header, Payment }

        private class NavItem
        {
            public ItemType Type;
            public PaymentsReflection.PaymentInfo? Payment;  // For Payment type
            public string Label;
        }

        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<NavItem> _items = new List<NavItem>();

        // Header text (Zhera Mossback quote)
        private const string HEADER_TEXT = "Zhera Mossback, Assistant to the Royal Treasurer: " +
            "\"May the sun shine on you, Viceroy! I was sent here to help you keep track of all " +
            "those annoying payments and obligations. Another pair of eyes on your Exploration Tax " +
            "forms might come in handy.\"";

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    PayCurrent();
                    return true;

                case KeyCode.Space:
                    CycleAutoPayment();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when PaymentsPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;

            RefreshData();

            if (_items.Count > 1)
            {
                // Announce header first
                Speech.Say($"Payments. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Payments. No payments due");
            }

            Debug.Log($"[ATSAccessibility] PaymentsOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] PaymentsOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
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

        private string BuildPaymentLabel(PaymentsReflection.PaymentInfo payment)
        {
            var parts = new List<string>();

            // Type (e.g., "Tax")
            if (!string.IsNullOrEmpty(payment.TypeLabel))
                parts.Add(payment.TypeLabel);
            else
                parts.Add("Payment");

            // Amount and good name
            parts.Add($"{payment.GoodAmount} {payment.GoodName}");

            // Due date
            string yearStr = PaymentsReflection.YearToRoman(payment.DueYear);
            parts.Add($"due Year {yearStr} {payment.DueSeason}");

            // Time remaining
            string timeStr = PaymentsReflection.FormatTime(payment.TimeRemaining);
            parts.Add(timeStr);

            // Auto-payment setting
            string autoLabel = PaymentsReflection.GetAutoPaymentLabel(payment.AutoPaymentType);
            parts.Add($"auto: {autoLabel}");

            // Can pay status
            parts.Add(payment.CanPay ? "can pay" : "cannot pay");

            return string.Join(", ", parts);
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
            Speech.Say(_items[_currentIndex].Label);
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void PayCurrent()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type != ItemType.Payment || !item.Payment.HasValue)
            {
                // Header or invalid - just re-announce
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

                // Refresh data and adjust index if needed
                RefreshData();

                if (_currentIndex >= _items.Count)
                    _currentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

                // Announce new current item
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

        private void CycleAutoPayment()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type != ItemType.Payment || !item.Payment.HasValue)
            {
                // Header or invalid - just re-announce
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
    }
}
