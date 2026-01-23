using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the CornerstonesLimitPickPopup (choose-one-to-remove sub-popup).
    /// Provides flat list navigation through active cornerstones with selection and confirm/cancel.
    /// </summary>
    public class CornerstoneLimitOverlay : IKeyHandler
    {
        private class NavItem
        {
            public object Model;       // EffectModel
            public string Label;       // "Name, Rarity"
        }

        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private int _selectedIndex = -1;  // Which cornerstone is marked for removal

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

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ToggleSelection();
                    return true;

                case KeyCode.Delete:
                    ConfirmRemoval();
                    return true;

                case KeyCode.Escape:
                    Cancel();
                    return true;

                default:
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when a CornerstonesLimitPickPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;
            _selectedIndex = -1;

            RefreshData();

            if (_items.Count > 0)
            {
                Speech.Say($"Choose cornerstone to remove. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Choose cornerstone to remove. No cornerstones found");
            }

            Debug.Log($"[ATSAccessibility] CornerstoneLimitOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _selectedIndex = -1;
            _items.Clear();

            Debug.Log("[ATSAccessibility] CornerstoneLimitOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            var cornerstones = CornerstoneReflection.GetActiveCornerstones();
            if (cornerstones != null)
            {
                foreach (var option in cornerstones)
                {
                    _items.Add(new NavItem
                    {
                        Model = option.Model,
                        Label = $"{option.DisplayName}, {option.Rarity}"
                    });
                }
            }

            Debug.Log($"[ATSAccessibility] CornerstoneLimitOverlay refreshed: {_items.Count} items");
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

            string announcement = _items[_currentIndex].Label;
            if (_currentIndex == _selectedIndex)
                announcement += ", selected";

            Speech.Say(announcement);
        }

        // ========================================
        // SELECTION AND CONFIRMATION
        // ========================================

        private void ToggleSelection()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            if (_selectedIndex == _currentIndex)
            {
                // Deselect
                _selectedIndex = -1;
                Speech.Say($"{_items[_currentIndex].Label} deselected");
            }
            else
            {
                _selectedIndex = _currentIndex;
                Speech.Say($"{_items[_currentIndex].Label} selected for removal");
            }
        }

        private void ConfirmRemoval()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            {
                Speech.Say("Select a cornerstone first");
                SoundManager.PlayFailed();
                return;
            }

            var item = _items[_selectedIndex];
            SoundManager.PlayButtonClick();
            Speech.Say($"Removed {item.Label}");
            CornerstoneReflection.RemoveAndConfirm(_popup, item.Model);
            // Popup hides → OnPopupHidden → Close()
        }

        private void Cancel()
        {
            Speech.Say("Cancelled");
            CornerstoneReflection.CancelLimitPopup(_popup);
            // Popup hides → OnPopupHidden → Close()
        }
    }
}
