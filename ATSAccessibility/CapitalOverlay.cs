using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the capital (Smoldering City) screen.
    /// Flat list navigation: Buy Upgrades, Deeds, Game History, Home (if unlocked).
    /// </summary>
    public class CapitalOverlay : IKeyHandler
    {
        private bool _isOpen;
        private bool _suspended;
        private int _currentIndex;
        private List<(string name, Action action)> _items = new List<(string, Action)>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen && !_suspended;

        /// <summary>
        /// Whether the overlay is open but temporarily suspended (sub-panel active).
        /// </summary>
        public bool IsSuspended => _isOpen && _suspended;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrentItem();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close capital screen
                    Close();
                    return false;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // Public Methods
        // ========================================

        public void Open()
        {
            BuildItemList();

            if (_items.Count == 0)
            {
                Debug.LogWarning("[ATSAccessibility] Capital overlay: no items available");
                return;
            }

            _isOpen = true;
            _suspended = false;
            _currentIndex = 0;
            AnnounceCurrentItem();
            Debug.Log("[ATSAccessibility] Capital overlay opened");
        }

        public void Close()
        {
            _isOpen = false;
            _suspended = false;
            _items.Clear();
            _currentIndex = 0;
            Debug.Log("[ATSAccessibility] Capital overlay closed");
        }

        /// <summary>
        /// Resume the overlay after a sub-panel closes.
        /// Re-announces the current item.
        /// </summary>
        public void Resume()
        {
            if (!_isOpen) return;

            _suspended = false;
            AnnounceCurrentItem();
            Debug.Log("[ATSAccessibility] Capital overlay resumed");
        }

        // ========================================
        // Private Methods
        // ========================================

        private void BuildItemList()
        {
            _items.Clear();

            _items.Add(("Buy Upgrades", () => CapitalReflection.OpenUpgrades()));
            _items.Add(("Deeds", () => CapitalReflection.OpenDeeds()));
            _items.Add(("Game History", () => CapitalReflection.OpenHistory()));

            if (CapitalReflection.IsDailyExpeditionUnlocked())
            {
                _items.Add(("Daily Expedition", () => CapitalReflection.OpenDailyExpedition()));
            }

            if (CapitalReflection.IsTrainingExpeditionUnlocked())
            {
                _items.Add(("Training Expedition", () => CapitalReflection.OpenTrainingExpedition()));
            }

            if (CapitalReflection.IsHomeUnlocked())
            {
                _items.Add(("Home", () => CapitalReflection.OpenHome()));
            }
        }

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            Debug.Log($"[ATSAccessibility] Capital overlay: activating {item.name}");
            _suspended = true;
            item.action?.Invoke();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            Speech.Say(_items[_currentIndex].name);
        }
    }
}
