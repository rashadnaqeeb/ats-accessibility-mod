using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for WorldEventPopup (decision screen for world events on the world map).
    /// Provides flat list navigation: header (event name + description), then decision options.
    /// </summary>
    public class WorldEventOverlay : IKeyHandler, ISearchable
    {
        // Item types in the flat list
        private enum ItemType { Header, Option }

        private class ListItem
        {
            public ItemType Type;
            public string Text;
            public int OptionIndex;  // Only for Option type
        }

        // State
        private bool _isOpen;
        private int _currentIndex;
        private List<ListItem> _items = new List<ListItem>();

        // Cached instance data (extracted from popup on open)
        private object _model;
        private object _state;

        // Type-ahead for options
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Search handles A-Z, Backspace, and all active-search navigation
            if (_search.HandleKey(keyCode, modifiers, this))
                return true;

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
                    ActivateCurrent();
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
        // ISearchable Implementation
        // ========================================

        public int SearchItemCount => _items.Count;
        public int SearchCurrentIndex => _currentIndex;

        public string GetSearchLabel(int index)
        {
            if (index < 0 || index >= _items.Count) return null;
            return _items[index].Type == ItemType.Option ? _items[index].Text : null;
        }

        public void SearchMoveTo(int index)
        {
            _currentIndex = index;
            AnnounceCurrentItem();
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when WorldEventPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _currentIndex = 0;
            _search.Clear();

            // Extract model and state from popup
            var worldEvent = WorldEventReflection.GetWorldEvent(popup);
            _model = WorldEventReflection.GetModel(worldEvent);
            _state = WorldEventReflection.GetState(worldEvent);

            // Build the list
            BuildList();

            // Announce event name and description
            if (_items.Count > 0)
            {
                Speech.Say(_items[0].Text);
            }

            Debug.Log($"[ATSAccessibility] WorldEventOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _items.Clear();
            _search.Clear();
            _model = null;
            _state = null;

            Debug.Log("[ATSAccessibility] WorldEventOverlay closed");
        }

        // ========================================
        // LIST BUILDING
        // ========================================

        private void BuildList()
        {
            _items.Clear();

            // [0] Header: event name and description
            string eventName = WorldEventReflection.GetEventName(_model) ?? "World Event";
            string eventDesc = WorldEventReflection.GetEventDescription(_model);

            string headerText = eventName;
            if (!string.IsNullOrEmpty(eventDesc))
            {
                headerText += ". " + eventDesc;
            }

            _items.Add(new ListItem
            {
                Type = ItemType.Header,
                Text = headerText,
                OptionIndex = -1
            });

            // [1+] Options
            int optionCount = WorldEventReflection.GetOptionCount(_model);
            for (int i = 0; i < optionCount; i++)
            {
                string optionText = BuildOptionText(i);
                _items.Add(new ListItem
                {
                    Type = ItemType.Option,
                    Text = optionText,
                    OptionIndex = i
                });
            }

            Debug.Log($"[ATSAccessibility] WorldEventOverlay: Built {_items.Count} items ({optionCount} options)");
        }

        private string BuildOptionText(int index)
        {
            string desc = WorldEventReflection.GetOptionDescription(_model, index) ?? $"Option {index + 1}";
            bool canExecute = WorldEventReflection.CanExecuteOption(_model, index);

            if (!canExecute)
            {
                string blockReason = WorldEventReflection.GetExecutionBlockReason(_model, index);
                if (!string.IsNullOrEmpty(blockReason))
                {
                    return $"{desc}, disabled, {blockReason}";
                }
                return $"{desc}, disabled";
            }

            return desc;
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

            var item = _items[_currentIndex];
            Speech.Say(item.Text);
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void ActivateCurrent()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            switch (item.Type)
            {
                case ItemType.Header:
                    // Re-announce header (read-only)
                    AnnounceCurrentItem();
                    break;

                case ItemType.Option:
                    ExecuteOption(item.OptionIndex);
                    break;
            }
        }

        private void ExecuteOption(int index)
        {
            // Check if option can be executed
            if (!WorldEventReflection.CanExecuteOption(_model, index))
            {
                string blockReason = WorldEventReflection.GetExecutionBlockReason(_model, index);
                if (!string.IsNullOrEmpty(blockReason))
                {
                    Speech.Say($"Cannot select. {blockReason}");
                }
                else
                {
                    Speech.Say("Cannot select");
                }
                SoundManager.PlayFailed();
                return;
            }

            // Execute the decision
            if (WorldEventReflection.ExecuteDecision(_model, _state, index))
            {
                SoundManager.PlayButtonClick();
                // Game will close the popup on success
            }
            else
            {
                Speech.Say("Failed to execute");
                SoundManager.PlayFailed();
            }
        }
    }
}
