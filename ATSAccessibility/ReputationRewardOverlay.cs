using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the ReputationRewardsPopup (mid-game blueprint reward selection).
    /// Provides flat list navigation through building choices plus extend and reroll options.
    /// </summary>
    public class ReputationRewardOverlay : IKeyHandler
    {
        // Navigation item types
        private enum ItemType { Building, Extend, Reroll }

        private class NavItem
        {
            public ItemType Type;
            public object Model;       // BuildingModel (for Building type only)
            public string Label;       // Announcement text
            public string SearchName;  // Name for type-ahead (buildings only)
        }

        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;

        // Navigation list
        private List<NavItem> _items = new List<NavItem>();
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

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
                case KeyCode.Space:
                    ActivateCurrent();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup (OnPopupHidden will close our overlay)
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when a ReputationRewardsPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;
            _search.Clear();

            RefreshData();

            if (_items.Count > 0)
            {
                Speech.Say($"Reputation reward. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Reputation reward. No options available");
            }

            Debug.Log($"[ATSAccessibility] ReputationRewardOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _search.Clear();
            _items.Clear();

            Debug.Log("[ATSAccessibility] ReputationRewardOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // Add buildings
            var options = ReputationRewardReflection.GetCurrentOptions();
            if (options != null)
            {
                foreach (var option in options)
                {
                    string label = !string.IsNullOrEmpty(option.Description)
                        ? $"{option.DisplayName}. {option.Description}"
                        : option.DisplayName;

                    _items.Add(new NavItem
                    {
                        Type = ItemType.Building,
                        Model = option.Model,
                        Label = label,
                        SearchName = option.DisplayName
                    });
                }
            }

            // Add extend option if available
            if (ReputationRewardReflection.CanExtend())
            {
                var (extAmount, extGoodName) = ReputationRewardReflection.GetExtendCost();
                string extendLabel = ReputationRewardReflection.CanAffordExtend()
                    ? $"Extend, {extAmount} {extGoodName}"
                    : $"Extend, {extAmount} {extGoodName}, cannot afford";

                _items.Add(new NavItem
                {
                    Type = ItemType.Extend,
                    Model = null,
                    Label = extendLabel,
                    SearchName = null
                });
            }

            // Add reroll option
            {
                var (rerollAmount, rerollGoodName) = ReputationRewardReflection.GetRerollCost();
                string rerollLabel = ReputationRewardReflection.CanAffordReroll()
                    ? $"Reroll, {rerollAmount} {rerollGoodName}"
                    : $"Reroll, {rerollAmount} {rerollGoodName}, cannot afford";

                _items.Add(new NavItem
                {
                    Type = ItemType.Reroll,
                    Model = null,
                    Label = rerollLabel,
                    SearchName = null
                });
            }

            Debug.Log($"[ATSAccessibility] ReputationRewardOverlay refreshed: {_items.Count} items");
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
        // ACTIVATION
        // ========================================

        private void ActivateCurrent()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            switch (item.Type)
            {
                case ItemType.Building:
                    ActivateBuilding(item);
                    break;

                case ItemType.Extend:
                    ActivateExtend();
                    break;

                case ItemType.Reroll:
                    ActivateReroll();
                    break;
            }
        }

        private void ActivateBuilding(NavItem item)
        {
            if (!ReputationRewardReflection.PickBuilding(_popup, item.Model))
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayButtonClick();

            // After pick: popup either refreshed (more rewards) or hiding (done)
            var newOptions = ReputationRewardReflection.GetCurrentOptions();
            if (newOptions != null && newOptions.Count > 0)
            {
                Speech.Say("Unlocked");
                RefreshData();
                _currentIndex = 0;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say("Unlocked");
                // Popup hides → OnPopupHidden → Close()
            }
        }

        private void ActivateExtend()
        {
            if (!ReputationRewardReflection.CanAffordExtend())
            {
                var (amount, goodName) = ReputationRewardReflection.GetExtendCost();
                Speech.Say($"Cannot afford, need {amount} {goodName}");
                SoundManager.PlayFailed();
                return;
            }

            int prevBuildingCount = CountBuildings();

            if (!ReputationRewardReflection.Extend())
            {
                Speech.Say("Cannot extend");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayButtonClick();
            RefreshData();

            int newBuildingCount = CountBuildings();
            if (newBuildingCount > prevBuildingCount)
            {
                // Navigate to the newly added building (last one)
                _currentIndex = GetLastBuildingIndex();
                AnnounceCurrentItem();
            }
            else
            {
                // Extend succeeded (cost paid) but no building was available to add
                Speech.Say("No new option available");
            }
        }

        private void ActivateReroll()
        {
            if (!ReputationRewardReflection.CanAffordReroll())
            {
                var (amount, goodName) = ReputationRewardReflection.GetRerollCost();
                Speech.Say($"Cannot afford, need {amount} {goodName}");
                SoundManager.PlayFailed();
                return;
            }

            if (!ReputationRewardReflection.Reroll(_popup))
            {
                Speech.Say("Cannot reroll");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayButtonClick();
            RefreshData();
            _currentIndex = 0;
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Count the number of building items in the current nav list.
        /// </summary>
        private int CountBuildings()
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Type == ItemType.Building)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Find the index of the last building item (before extend/reroll).
        /// </summary>
        private int GetLastBuildingIndex()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Type == ItemType.Building)
                    return i;
            }
            return 0;
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindBuildingMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
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

            int matchIndex = FindBuildingMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        /// <summary>
        /// Find the first building item whose name starts with the search buffer.
        /// Only searches building items, not extend/reroll.
        /// </summary>
        private int FindBuildingMatch()
        {
            if (!_search.HasBuffer || _items.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Type != ItemType.Building) continue;
                if (string.IsNullOrEmpty(_items[i].SearchName)) continue;

                if (_items[i].SearchName.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
