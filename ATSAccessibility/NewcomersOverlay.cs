using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the NewcomersPopup (newcomers arrival group selection).
    /// Provides flat list navigation: dialogue, group 1, group 2.
    /// </summary>
    public class NewcomersOverlay : IKeyHandler
    {
        // Navigation item types
        private enum ItemType { Dialogue, Group }

        private class NavItem
        {
            public ItemType Type;
            public object GroupData;   // NewcomersGroup object (for Group type only)
            public string Label;       // Announcement text
        }

        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;

        // Navigation list
        private List<NavItem> _items = new List<NavItem>();

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
        /// Open the overlay when a NewcomersPopup is shown.
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
                Speech.Say($"Newcomers. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Newcomers. No groups available");
            }

            Debug.Log($"[ATSAccessibility] NewcomersOverlay opened, {_items.Count} items");
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

            Debug.Log("[ATSAccessibility] NewcomersOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // 1. Dialogue item (hardcoded NPC text - popup TMPro text is not reliably readable)
            _items.Add(new NavItem
            {
                Type = ItemType.Dialogue,
                GroupData = null,
                Label = "Pervun Runebeak, Royal Stormwalker: These people have been sent here by the Crown. Which group do you want to stay, Viceroy? The other will continue on to the next settlement."
            });

            // 2. Group options
            var groups = NewcomersReflection.GetNewcomersGroups();
            if (groups != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group == null) continue;

                    string groupLabel = NewcomersReflection.FormatGroup(group);
                    _items.Add(new NavItem
                    {
                        Type = ItemType.Group,
                        GroupData = group,
                        Label = $"Group {i + 1}: {groupLabel}"
                    });
                }
            }

            Debug.Log($"[ATSAccessibility] NewcomersOverlay refreshed: {_items.Count} items");
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
                    // Read-only, re-announce
                    AnnounceCurrentItem();
                    break;

                case ItemType.Group:
                    ActivateGroup(item);
                    break;
            }
        }

        private void ActivateGroup(NavItem item)
        {
            if (!NewcomersReflection.PickGroup(_popup, item.GroupData))
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            SoundManager.PlayNewcomersBannerAccept();
            // Popup hides -> OnPopupHidden -> Close()
        }
    }
}
