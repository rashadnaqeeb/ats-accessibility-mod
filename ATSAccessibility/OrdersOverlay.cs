using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the OrdersPopup (order list navigation).
    /// Provides flat list navigation through all orders with front-loaded announcements.
    /// </summary>
    public class OrdersOverlay : IKeyHandler
    {
        // Order status for sorting and announcement
        private enum OrderStatus { ToPick, Completable, Active, Locked, Completed, Failed }

        private class OrderItem
        {
            public object State;       // OrderState
            public object Model;       // OrderModel
            public string Label;       // Announcement text
            public OrderStatus Status;
            public bool Tracked;
        }

        // State
        private bool _isOpen;
        private int _currentIndex;
        private List<OrderItem> _items = new List<OrderItem>();

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

                case KeyCode.T:
                    ToggleTracking();
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
        /// Open the overlay when OrdersPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _currentIndex = 0;

            RefreshData();

            if (_items.Count > 0)
            {
                Speech.Say($"Orders. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Orders. No orders available");
            }

            Debug.Log($"[ATSAccessibility] OrdersOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _items.Clear();

            Debug.Log("[ATSAccessibility] OrdersOverlay closed");
        }

        /// <summary>
        /// Refresh data after the OrderPickPopup closes.
        /// The picked order is now Active, so re-announce current.
        /// </summary>
        public void RefreshAfterPick()
        {
            if (!_isOpen) return;

            RefreshData();
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

            if (_items.Count > 0)
            {
                AnnounceCurrentItem();
            }
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            var orders = OrdersReflection.GetOrders();
            if (orders == null) return;

            int slotNum = 0;
            foreach (var orderState in orders)
            {
                slotNum++;
                if (orderState == null) continue;

                var model = OrdersReflection.GetOrderModel(orderState);
                var status = DetermineStatus(orderState, model);
                var statusLabel = BuildLabel(orderState, model, status);
                var tracked = OrdersReflection.IsTracked(orderState);

                _items.Add(new OrderItem
                {
                    State = orderState,
                    Model = model,
                    Label = $"Slot {slotNum}: {statusLabel}",
                    Status = status,
                    Tracked = tracked
                });
            }

            Debug.Log($"[ATSAccessibility] OrdersOverlay refreshed: {_items.Count} items");
        }

        private OrderStatus DetermineStatus(object orderState, object orderModel)
        {
            if (OrdersReflection.IsFailed(orderState))
                return OrderStatus.Failed;
            if (OrdersReflection.IsCompleted(orderState))
                return OrderStatus.Completed;
            if (!OrdersReflection.IsStarted(orderState))
                return OrderStatus.Locked;
            if (!OrdersReflection.IsPicked(orderState))
                return OrderStatus.ToPick;
            // Active orders should always have a model; guard just in case
            if (orderModel == null)
                return OrderStatus.Active;
            if (OrdersReflection.CanComplete(orderState, orderModel))
                return OrderStatus.Completable;
            return OrderStatus.Active;
        }

        private string BuildLabel(object orderState, object orderModel, OrderStatus status)
        {
            string name = orderModel != null
                ? (OrdersReflection.GetOrderDisplayName(orderModel) ?? "Unknown order")
                : null;

            switch (status)
            {
                case OrderStatus.Locked:
                    return BuildLockedLabel(orderState, orderModel);

                case OrderStatus.ToPick:
                    return "Ready to pick";

                case OrderStatus.Completable:
                    return $"{name}, ready to deliver. {BuildRewardText(orderState, orderModel)}";

                case OrderStatus.Active:
                    return BuildActiveLabel(name, orderState, orderModel);

                case OrderStatus.Completed:
                    return $"{name}, completed. {BuildRewardText(orderState, orderModel)}";

                case OrderStatus.Failed:
                    return $"{name}, failed";

                default:
                    return name ?? "Unknown order";
            }
        }

        private string BuildLockedLabel(object orderState, object orderModel)
        {
            if (orderModel != null && OrdersReflection.HasUnlockAfter(orderModel))
            {
                string prereqName = OrdersReflection.GetUnlockAfterName(orderModel) ?? "another order";
                return $"Locked, requires {prereqName}";
            }

            // Timer-based lock: startTime - gameTime
            float startTime = OrdersReflection.GetStartTime(orderState);
            float gameTime = OrdersReflection.GetGameTime();
            float remaining = startTime - gameTime;
            if (remaining > 0)
            {
                return $"Locked, unlocks in {OrdersReflection.FormatTime(remaining)}";
            }

            return "Locked";
        }

        private string BuildActiveLabel(string name, object orderState, object orderModel)
        {
            var objectives = OrdersReflection.GetObjectiveTexts(orderModel, orderState);
            string objText = objectives.Count > 0 ? string.Join(", ", objectives) : "";

            var rewards = OrdersReflection.GetRewardTexts(orderState);
            string repReward = OrdersReflection.GetReputationRewardText(orderModel);
            if (!string.IsNullOrEmpty(repReward))
                rewards.Add(repReward);
            string rewardText = rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";

            var parts = new List<string>();

            bool timed = OrdersReflection.CanBeFailed(orderModel);
            if (timed)
            {
                float timeLeft = OrdersReflection.GetTimeLeft(orderState);
                string timeStr = OrdersReflection.FormatTime(timeLeft);
                parts.Add($"{name}, {timeStr}");
            }
            else
            {
                parts.Add(name);
            }

            if (!string.IsNullOrEmpty(objText))
                parts.Add(objText);
            if (!string.IsNullOrEmpty(rewardText))
                parts.Add(rewardText);

            return string.Join(". ", parts);
        }

        private string BuildRewardText(object orderState, object orderModel)
        {
            var rewards = OrdersReflection.GetRewardTexts(orderState);
            string repReward = OrdersReflection.GetReputationRewardText(orderModel);
            if (!string.IsNullOrEmpty(repReward))
                rewards.Add(repReward);
            return rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";
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
            switch (item.Status)
            {
                case OrderStatus.ToPick:
                    // Open pick popup
                    if (OrdersReflection.FireOrderPickPopupRequested(item.State))
                    {
                        SoundManager.PlayButtonClick();
                    }
                    else
                    {
                        Speech.Say("Cannot open picks");
                        SoundManager.PlayFailed();
                    }
                    break;

                case OrderStatus.Completable:
                    // Deliver the order
                    string name = OrdersReflection.GetOrderDisplayName(item.Model) ?? "order";
                    if (OrdersReflection.CompleteOrder(item.State, item.Model))
                    {
                        SoundManager.PlayButtonClick();
                        Speech.Say($"Delivered, {name}");
                        RefreshData();
                        if (_currentIndex >= _items.Count)
                            _currentIndex = _items.Count > 0 ? _items.Count - 1 : 0;
                        if (_items.Count > 0)
                            AnnounceCurrentItem();
                    }
                    else
                    {
                        Speech.Say("Cannot deliver");
                        SoundManager.PlayFailed();
                    }
                    break;

                default:
                    // Re-announce for all other states
                    AnnounceCurrentItem();
                    break;
            }
        }

        private void ToggleTracking()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            // Only active orders can be tracked
            if (item.Status != OrderStatus.Active && item.Status != OrderStatus.Completable)
            {
                Speech.Say("Cannot track");
                SoundManager.PlayFailed();
                return;
            }

            if (OrdersReflection.ToggleTracking(item.State))
            {
                bool nowTracked = OrdersReflection.IsTracked(item.State);
                item.Tracked = nowTracked;
                Speech.Say(nowTracked ? "Tracked" : "Untracked");
                SoundManager.PlayButtonClick();
            }
            else
            {
                Speech.Say("Cannot toggle tracking");
            }
        }
    }
}
