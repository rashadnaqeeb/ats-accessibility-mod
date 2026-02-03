using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the CornerstonesLimitPickPopup (choose-one-to-remove sub-popup).
    /// Provides flat list navigation through active cornerstones with selection and confirm/cancel.
    /// </summary>
    public class CornerstoneLimitOverlay : MenuBase, IKeyHandler
    {
        private class NavItem
        {
            public object Model;       // EffectModel
            public string Label;       // "Name, Rarity"
            public string SearchName;  // Name for type-ahead
        }

        // Data
        private object _popup;
        private int _selectedIndex = -1;  // Which cornerstone is marked for removal
        private List<NavItem> _items = new List<NavItem>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Choose cornerstone to remove";
        protected override string EmptyMessage => "No cornerstones found";

        protected override int GetItemCount() => _items.Count;

        protected override string GetLabel(int index)
        {
            if (index < 0 || index >= _items.Count) return null;

            string announcement = _items[index].Label;
            if (index == _selectedIndex)
                announcement += ", selected";

            return announcement;
        }

        protected override string GetSearchName(int index)
        {
            if (index >= 0 && index < _items.Count)
                return _items[index].SearchName;
            return null;
        }

        protected override void RefreshData()
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
                        Label = $"{option.DisplayName}, {option.Rarity}",
                        SearchName = option.DisplayName
                    });
                }
            }

            Debug.Log($"[ATSAccessibility] CornerstoneLimitOverlay refreshed: {_items.Count} items");
        }

        protected override EnterAction OnEnter(int index) => EnterAction.Action;

        protected override void OnAction(int index)
        {
            ConfirmRemoval();
        }

        protected override void OnSpace(int index)
        {
            ToggleSelection();
        }

        protected override EscapeAction OnEscape() => EscapeAction.Close;

        protected override void StorePopup(object popup)
        {
            _popup = popup;
        }

        protected override void OnClosed()
        {
            // Cancel if closing without confirming
            if (_popup != null)
            {
                CornerstoneReflection.CancelLimitPopup(_popup);
            }
            _popup = null;
            _selectedIndex = -1;
            _items.Clear();
        }

        protected override void OnOpened()
        {
            _selectedIndex = -1;
        }

        // ========================================
        // SELECTION AND CONFIRMATION
        // ========================================

        private void ToggleSelection()
        {
            if (_items.Count == 0 || CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

            if (_selectedIndex == CurrentIndex)
            {
                _selectedIndex = -1;
                Speech.Say($"{_items[CurrentIndex].Label} deselected");
            }
            else
            {
                _selectedIndex = CurrentIndex;
                Speech.Say($"{_items[CurrentIndex].Label} selected for removal");
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
            var popup = _popup;
            _popup = null; // Prevent OnClosed from cancelling
            CornerstoneReflection.RemoveAndConfirm(popup, item.Model);
            // Popup hides -> OnPopupHidden -> Close()
        }
    }
}
