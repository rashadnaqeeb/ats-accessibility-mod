using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for WorldEventPopup (decision screen for world events on the world map).
    /// Provides flat list navigation: header (event name + description), then decision options.
    /// </summary>
    public class WorldEventOverlay : MenuBase, IKeyHandler
    {
        // Item types in the flat list
        private enum ItemType { Header, Option }

        private class ListItem
        {
            public ItemType Type;
            public string Text;
            public int OptionIndex;  // Only for Option type
        }

        // Data
        private List<ListItem> _items = new List<ListItem>();

        // Cached instance data (extracted from popup on open)
        private object _model;
        private object _state;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "World Event";
        protected override string EmptyMessage => "";

        protected override int GetItemCount() => _items.Count;

        protected override string GetLabel(int index)
        {
            if (index >= 0 && index < _items.Count)
                return _items[index].Text;
            return null;
        }

        protected override string GetSearchName(int index)
        {
            if (index >= 0 && index < _items.Count)
                return _items[index].Type == ItemType.Option ? _items[index].Text : null;
            return null;
        }

        protected override void RefreshData()
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

        protected override EnterAction OnEnter(int index) => EnterAction.Action;

        protected override void OnAction(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            var item = _items[index];
            switch (item.Type)
            {
                case ItemType.Header:
                    AnnounceCurrentItem();
                    break;
                case ItemType.Option:
                    ExecuteOption(item.OptionIndex);
                    break;
            }
        }

        // Escape passes to game to close popup
        protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

        protected override string GetOpenAnnouncement()
        {
            // Announce header text (event name + description)
            if (_items.Count > 0)
                return _items[0].Text;
            return OverlayName;
        }

        protected override void StorePopup(object popup)
        {
            var worldEvent = WorldEventReflection.GetWorldEvent(popup);
            _model = WorldEventReflection.GetModel(worldEvent);
            _state = WorldEventReflection.GetState(worldEvent);
        }

        protected override void OnClosed()
        {
            _items.Clear();
            _model = null;
            _state = null;
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void ExecuteOption(int index)
        {
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

        // ========================================
        // HELPERS
        // ========================================

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
    }
}
