using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for WorldEventPopup (decision screen for world events on the world map).
    /// Provides flat list navigation: header (event name + description), then decision options.
    /// </summary>
    public class WorldEventOverlay : IKeyHandler
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

            // Clear search on navigation keys
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
                    ActivateCurrent();
                    return true;

                case KeyCode.Backspace:
                    if (_search.RemoveChar())
                    {
                        if (_search.HasBuffer)
                            Speech.Say($"Search: {_search.Buffer}");
                        else
                            Speech.Say("Search cleared");
                    }
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                default:
                    // Type-ahead search for options (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        _search.AddChar(c);

                        int match = FindMatchingOption();
                        if (match >= 0)
                        {
                            _currentIndex = match;
                            AnnounceCurrentItem();
                        }
                        else
                        {
                            Speech.Say($"No match for {_search.Buffer}");
                        }
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

        private int FindMatchingOption()
        {
            if (!_search.HasBuffer) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                // Only match Option items
                if (item.Type != ItemType.Option) continue;

                if (!string.IsNullOrEmpty(item.Text) &&
                    item.Text.ToLowerInvariant().StartsWith(lowerBuffer))
                {
                    return i;
                }
            }

            return -1;
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
