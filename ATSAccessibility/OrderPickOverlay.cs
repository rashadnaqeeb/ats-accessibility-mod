using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the OrderPickPopup (order pick option selection).
    /// Provides flat list navigation through pick options with objectives and rewards.
    /// </summary>
    public class OrderPickOverlay : IKeyHandler
    {
        private class PickItem
        {
            public object PickState;    // OrderPickState
            public object OrderModel;   // OrderModel resolved from pick.model
            public bool Failed;
            public string Label;        // Announcement text
        }

        // State
        private bool _isOpen;
        private object _popup;         // The OrderPickPopup instance
        private object _orderState;    // The order being picked
        private int _currentIndex;
        private List<PickItem> _items = new List<PickItem>();

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
                    ActivateCurrent();
                    return true;

                case KeyCode.S:
                    AnnounceStoredAmounts();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup (OnPopupHidden will close our overlay)
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
        /// Open the overlay when OrderPickPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;

            RefreshData();

            if (_items.Count > 0)
            {
                // Skip to first non-failed item for initial announcement
                int firstValid = GetFirstNonFailedIndex();
                _currentIndex = firstValid >= 0 ? firstValid : 0;
                Speech.Say($"Pick order. {_items[_currentIndex].Label}");
            }
            else
            {
                Speech.Say("Pick order. No options available");
            }

            Debug.Log($"[ATSAccessibility] OrderPickOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _orderState = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] OrderPickOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // Get the order from the popup's own field (set in its Show method)
            _orderState = OrdersReflection.GetPopupOrder(_popup);
            if (_orderState == null)
            {
                Debug.LogWarning("[ATSAccessibility] OrderPickOverlay: Could not read order from popup");
                return;
            }

            var picks = OrdersReflection.GetPicksFor(_orderState);
            if (picks == null || picks.Count == 0) return;

            foreach (var pick in picks)
            {
                if (pick == null) continue;

                var orderModel = OrdersReflection.GetPickOrderModel(pick);
                if (orderModel == null) continue;

                bool failed = OrdersReflection.IsPickFailed(pick) && OrdersReflection.CanBeFailed(orderModel);
                string label = BuildPickLabel(pick, orderModel, failed);

                _items.Add(new PickItem
                {
                    PickState = pick,
                    OrderModel = orderModel,
                    Failed = failed,
                    Label = label
                });
            }

            Debug.Log($"[ATSAccessibility] OrderPickOverlay refreshed: {_items.Count} picks");
        }

        private string BuildPickLabel(object pickState, object orderModel, bool failed)
        {
            string name = OrdersReflection.GetOrderDisplayName(orderModel) ?? "Unknown";

            if (failed)
            {
                return $"{name}, expired";
            }

            int setIndex = OrdersReflection.GetPickSetIndex(pickState);
            bool timed = OrdersReflection.CanBeFailed(orderModel);

            // Build objectives text
            var objectives = OrdersReflection.GetPickObjectiveTexts(orderModel, setIndex);
            string objText = objectives.Count > 0 ? "Requirements: " + string.Join(", ", objectives) : "";

            // Build rewards text
            var rewards = OrdersReflection.GetPickRewardTexts(pickState);
            string repReward = OrdersReflection.GetReputationRewardText(orderModel);
            if (!string.IsNullOrEmpty(repReward))
                rewards.Add(repReward);
            string rewardText = rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";

            // Combine
            var parts = new List<string>();
            if (timed)
            {
                float timeToFail = OrdersReflection.GetTimeToFail(orderModel);
                parts.Add($"{name}, timed {OrdersReflection.FormatTime(timeToFail)}");
            }
            else
            {
                parts.Add(name);
            }

            if (!string.IsNullOrEmpty(objText))
                parts.Add(objText);
            if (!string.IsNullOrEmpty(rewardText))
                parts.Add(rewardText);

            // Add warnings (e.g. "Missing building")
            var warnings = OrdersReflection.GetPickWarningTexts(orderModel, setIndex);
            if (warnings.Count > 0)
                parts.Add("Warning: " + string.Join(", ", warnings));

            return string.Join(". ", parts);
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

        private void ActivateCurrent()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            if (item.Failed)
            {
                Speech.Say("Expired");
                SoundManager.PlayFailed();
                return;
            }

            if (_orderState == null)
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            if (OrdersReflection.PickOrder(_orderState, item.PickState))
            {
                SoundManager.PlayButtonClick();
                Speech.Say("Selected");
                // Hide the popup (mirrors OrderPickPopup.OnPicked behavior)
                OrdersReflection.HidePopup(_popup);
            }
            else
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
            }
        }

        private void AnnounceStoredAmounts()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Failed)
            {
                Speech.Say("Expired");
                return;
            }

            int setIndex = OrdersReflection.GetPickSetIndex(item.PickState);
            var amounts = OrdersReflection.GetPickStoredAmounts(item.OrderModel, setIndex);

            if (amounts.Count == 0)
            {
                Speech.Say("No storage info");
                return;
            }

            Speech.Say(string.Join(", ", amounts));
        }

        // ========================================
        // HELPERS
        // ========================================

        private int GetFirstNonFailedIndex()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].Failed) return i;
            }
            return -1;
        }
    }
}
