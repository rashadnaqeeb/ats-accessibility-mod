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
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

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

            _search.ClearOnNavigationKey(keyCode);

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

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
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
            _search.Clear();
            AnnounceCurrentItem();
            Debug.Log("[ATSAccessibility] Capital overlay opened");
        }

        public void Close()
        {
            _isOpen = false;
            _suspended = false;
            _items.Clear();
            _currentIndex = 0;
            _search.Clear();
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
            _items.Add(("Deeds", () => OpenIfUnlocked("Deeds", CapitalReflection.IsDeedsUnlocked, CapitalReflection.OpenDeeds)));
            _items.Add(("Game History", () => CapitalReflection.OpenHistory()));
            _items.Add(("Daily Expedition", () => OpenIfUnlocked("Daily Expedition", CapitalReflection.IsDailyExpeditionUnlocked, CapitalReflection.OpenDailyExpedition)));
            _items.Add(("Training Expedition", () => OpenIfUnlocked("Training Expedition", CapitalReflection.IsTrainingExpeditionUnlocked, CapitalReflection.OpenTrainingExpedition)));

            if (CapitalReflection.IsHomeUnlocked())
            {
                _items.Add(("Home", () => CapitalReflection.OpenHome()));
            }
        }

        private void OpenIfUnlocked(string name, Func<bool> unlockCheck, Func<bool> openAction)
        {
            if (!unlockCheck())
            {
                Speech.Say($"{name} locked. Unlock via meta progression");
                SoundManager.PlayFailed();
                _suspended = false;  // Don't suspend if we didn't open anything
                return;
            }
            openAction();
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
            SoundManager.PlayButtonClick();
            _suspended = true;
            item.action?.Invoke();
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int match = FindMatch();
            if (match >= 0)
            {
                _currentIndex = match;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindMatch();
            if (match >= 0)
            {
                _currentIndex = match;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatch()
        {
            if (!_search.HasBuffer) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].name.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            string name = _items[_currentIndex].name;
            string lockSuffix = GetLockSuffix(name);
            Speech.Say($"{name}{lockSuffix}");
        }

        private string GetLockSuffix(string itemName)
        {
            switch (itemName)
            {
                case "Deeds":
                    return CapitalReflection.IsDeedsUnlocked() ? "" : ", locked";
                case "Daily Expedition":
                    return CapitalReflection.IsDailyExpeditionUnlocked() ? "" : ", locked";
                case "Training Expedition":
                    return CapitalReflection.IsTrainingExpeditionUnlocked() ? "" : ", locked";
                default:
                    return "";
            }
        }
    }
}
