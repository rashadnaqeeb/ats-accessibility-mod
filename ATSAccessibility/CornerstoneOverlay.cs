using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the RewardPickPopup (mid-game cornerstone/perk selection).
    /// Provides flat list navigation through NPC dialogue, cornerstone choices, extend, reroll, and skip.
    /// </summary>
    public class CornerstoneOverlay : IKeyHandler
    {
        // Navigation item types
        private enum ItemType { Dialogue, Cornerstone, Extend, Reroll, Skip }

        private class NavItem
        {
            public ItemType Type;
            public object Model;       // EffectModel (for Cornerstone type only)
            public string Label;       // Announcement text
            public string SearchName;  // Name for type-ahead (cornerstones only)
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

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(_items.Count - 1);
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
        /// Open the overlay when a RewardPickPopup is shown.
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
                Speech.Say($"Cornerstone. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Cornerstone. No options available");
            }

            Debug.Log($"[ATSAccessibility] CornerstoneOverlay opened, {_items.Count} items");
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

            Debug.Log("[ATSAccessibility] CornerstoneOverlay closed");
        }

        /// <summary>
        /// Refresh data after the limit popup closes.
        /// If options changed (new pick loaded), announce the new state.
        /// </summary>
        public void RefreshAfterLimit()
        {
            if (!_isOpen) return;

            RefreshData();
            _currentIndex = GetFirstCornerstoneIndex();
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

            // 1. NPC dialogue item
            var (npcName, dialogue) = CornerstoneReflection.GetNpcDialogue(_popup);
            if (!string.IsNullOrEmpty(npcName) || !string.IsNullOrEmpty(dialogue))
            {
                string dialogueLabel = !string.IsNullOrEmpty(npcName)
                    ? $"{npcName}: {dialogue}"
                    : dialogue;

                _items.Add(new NavItem
                {
                    Type = ItemType.Dialogue,
                    Model = null,
                    Label = dialogueLabel,
                    SearchName = null
                });
            }

            // 2. Cornerstone options
            var options = CornerstoneReflection.GetCurrentOptions();
            if (options != null)
            {
                foreach (var option in options)
                {
                    string rarityText = option.Rarity;
                    if (option.IsEthereal)
                        rarityText += ", ethereal";

                    string label = !string.IsNullOrEmpty(option.Description)
                        ? $"{option.DisplayName}, {rarityText}. {option.Description}"
                        : $"{option.DisplayName}, {rarityText}";

                    _items.Add(new NavItem
                    {
                        Type = ItemType.Cornerstone,
                        Model = option.Model,
                        Label = label,
                        SearchName = option.DisplayName
                    });
                }
            }

            // 3. Extend option (if available)
            if (CornerstoneReflection.CanExtend())
            {
                var (extAmount, extGoodName) = CornerstoneReflection.GetExtendCost();
                string extendLabel = CornerstoneReflection.CanAffordExtend()
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

            // 4. Reroll option (if rerolls remaining)
            int rerolls = CornerstoneReflection.GetRerollsLeft();
            if (rerolls > 0)
            {
                _items.Add(new NavItem
                {
                    Type = ItemType.Reroll,
                    Model = null,
                    Label = $"Reroll, {rerolls} remaining",
                    SearchName = null
                });
            }

            // 5. Skip option (always available)
            {
                var (skipAmount, skipGoodName) = CornerstoneReflection.GetDeclinePayoff();
                _items.Add(new NavItem
                {
                    Type = ItemType.Skip,
                    Model = null,
                    Label = $"Skip, receive {skipAmount} {skipGoodName}",
                    SearchName = null
                });
            }

            Debug.Log($"[ATSAccessibility] CornerstoneOverlay refreshed: {_items.Count} items");
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

        private void NavigateTo(int index)
        {
            if (_items.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _items.Count - 1);
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
                case ItemType.Dialogue:
                    // Read-only, re-announce for clarity
                    AnnounceCurrentItem();
                    break;

                case ItemType.Cornerstone:
                    ActivateCornerstone(item);
                    break;

                case ItemType.Extend:
                    ActivateExtend();
                    break;

                case ItemType.Reroll:
                    ActivateReroll();
                    break;

                case ItemType.Skip:
                    ActivateSkip();
                    break;
            }
        }

        private void ActivateCornerstone(NavItem item)
        {
            if (!CornerstoneReflection.PickCornerstone(_popup, item.Model))
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayButtonClick();

            // After pick: popup either rebuilt (more picks), hidden (done),
            // or limit popup opened (at limit).
            var newOptions = CornerstoneReflection.GetCurrentOptions();
            if (newOptions != null && newOptions.Count > 0)
            {
                Speech.Say("Picked");
                RefreshData();
                _currentIndex = GetFirstCornerstoneIndex();
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say("Picked");
                // Popup hides → OnPopupHidden → Close()
                // OR limit popup opened → handled by CornerstoneLimitOverlay
            }
        }

        private void ActivateExtend()
        {
            if (!CornerstoneReflection.CanAffordExtend())
            {
                var (amount, goodName) = CornerstoneReflection.GetExtendCost();
                Speech.Say($"Cannot afford, need {amount} {goodName}");
                SoundManager.PlayFailed();
                return;
            }

            int prevCount = CountCornerstones();

            if (!CornerstoneReflection.Extend())
            {
                Speech.Say("Cannot extend");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayButtonClick();
            RefreshData();

            int newCount = CountCornerstones();
            if (newCount > prevCount)
            {
                _currentIndex = GetLastCornerstoneIndex();
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say("No new option available");
            }
        }

        private void ActivateReroll()
        {
            if (!CornerstoneReflection.Reroll(_popup))
            {
                Speech.Say("Cannot reroll");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayReroll();
            RefreshData();
            _currentIndex = GetFirstCornerstoneIndex();
            AnnounceCurrentItem();
        }

        private void ActivateSkip()
        {
            if (!CornerstoneReflection.Skip(_popup))
            {
                Speech.Say("Cannot skip");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayDecline();

            // After skip: popup either rebuilt (more picks) or hidden (done)
            var afterSkip = CornerstoneReflection.GetCurrentOptions();
            if (afterSkip != null && afterSkip.Count > 0)
            {
                Speech.Say("Skipped");
                RefreshData();
                _currentIndex = GetFirstCornerstoneIndex();
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say("Skipped");
                // Popup hides → Close()
            }
        }

        // ========================================
        // HELPERS
        // ========================================

        private int GetFirstCornerstoneIndex()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Type == ItemType.Cornerstone) return i;
            return 0;
        }

        private int GetLastCornerstoneIndex()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i].Type == ItemType.Cornerstone) return i;
            return 0;
        }

        private int CountCornerstones()
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Type == ItemType.Cornerstone) count++;
            return count;
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindCornerstoneMatch();
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

            int matchIndex = FindCornerstoneMatch();
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
        /// Find the first cornerstone item whose name starts with the search buffer.
        /// </summary>
        private int FindCornerstoneMatch()
        {
            if (!_search.HasBuffer || _items.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Type != ItemType.Cornerstone) continue;
                if (string.IsNullOrEmpty(_items[i].SearchName)) continue;

                if (_items[i].SearchName.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
