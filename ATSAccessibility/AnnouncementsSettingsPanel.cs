using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Settings panel for toggling event announcements.
    /// Accessed from the F1 Information Panels menu.
    /// </summary>
    public class AnnouncementsSettingsPanel
    {
        private class SettingItem
        {
            public string Label;
            public ConfigEntry<bool> ConfigEntry;
        }

        private List<SettingItem> _items = new List<SettingItem>();
        private int _currentIndex = 0;
        private bool _isOpen = false;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether the announcements panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the announcements settings panel.
        /// </summary>
        public void Open()
        {
            _isOpen = true;
            BuildItemList();
            _currentIndex = 0;
            _search.Clear();
            AnnounceCurrentItem(includeHeader: true);
            Debug.Log("[ATSAccessibility] Announcements settings panel opened");
        }

        /// <summary>
        /// Close the announcements settings panel.
        /// Config is automatically saved by BepInEx.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            _search.Clear();
            Debug.Log("[ATSAccessibility] Announcements settings panel closed");
        }

        /// <summary>
        /// Process a key event for the announcements panel.
        /// Returns true if the key was handled, false if parent should handle it.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
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
                    ToggleCurrentSetting();
                    return true;

                case KeyCode.LeftArrow:
                    // Signal to parent to close this panel and return to menu
                    return false;

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // No search to clear - let parent handle closing
                    return false;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume other keys while panel is open
                    return true;
            }
        }

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem(includeHeader: false);
        }

        private void ToggleCurrentSetting()
        {
            if (_items.Count == 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            item.ConfigEntry.Value = !item.ConfigEntry.Value;
            AnnounceCurrentItem(includeHeader: false);
        }

        private void AnnounceCurrentItem(bool includeHeader)
        {
            if (_items.Count == 0) return;

            var item = _items[_currentIndex];
            string state = item.ConfigEntry.Value ? "On" : "Off";
            string position = $"{_currentIndex + 1} of {_items.Count}";

            string message = includeHeader
                ? $"Announcement settings. {item.Label}, {state}. {position}"
                : $"{item.Label}, {state}";
            Speech.Say(message);
        }

        private void BuildItemList()
        {
            _items.Clear();

            // Game Alerts (uses game's built-in alert system)
            // This covers: newcomers waiting, villager loss, trader arrived, building destroyed,
            // hearth fire died, blight, order completed, low food, starvation, and many more
            _items.Add(new SettingItem { Label = "Game alerts", ConfigEntry = Plugin.AnnounceGameAlerts });

            // Buildings (not covered by game alerts)
            _items.Add(new SettingItem { Label = "Construction complete", ConfigEntry = Plugin.AnnounceConstructionComplete });
            _items.Add(new SettingItem { Label = "Hearth level change", ConfigEntry = Plugin.AnnounceHearthLevelChange });
            _items.Add(new SettingItem { Label = "Hearth ignited", ConfigEntry = Plugin.AnnounceHearthIgnited });
            _items.Add(new SettingItem { Label = "Hearth corrupted", ConfigEntry = Plugin.AnnounceHearthCorrupted });
            _items.Add(new SettingItem { Label = "Sacrifice stopped", ConfigEntry = Plugin.AnnounceSacrificeStopped });

            // Exploration
            _items.Add(new SettingItem { Label = "Glade revealed", ConfigEntry = Plugin.AnnounceGladeRevealed });
            _items.Add(new SettingItem { Label = "Relic resolved", ConfigEntry = Plugin.AnnounceRelicResolved });
            _items.Add(new SettingItem { Label = "Reward chase", ConfigEntry = Plugin.AnnounceRewardChase });
            _items.Add(new SettingItem { Label = "Locate markers", ConfigEntry = Plugin.AnnounceLocateMarkers });

            // Villagers
            _items.Add(new SettingItem { Label = "Newcomers waiting", ConfigEntry = Plugin.AnnounceNewcomersWaiting });
            _items.Add(new SettingItem { Label = "Villager lost", ConfigEntry = Plugin.AnnounceVillagerLost });

            // Time
            _items.Add(new SettingItem { Label = "Season changed", ConfigEntry = Plugin.AnnounceSeasonChanged });
            _items.Add(new SettingItem { Label = "Year changed", ConfigEntry = Plugin.AnnounceYearChanged });

            // Trade (trader departed not covered by game alerts)
            _items.Add(new SettingItem { Label = "Trader departed", ConfigEntry = Plugin.AnnounceTraderDeparted });

            // Orders (order available and failed not covered by game alerts)
            _items.Add(new SettingItem { Label = "Order available", ConfigEntry = Plugin.AnnounceOrderAvailable });
            _items.Add(new SettingItem { Label = "Order completed", ConfigEntry = Plugin.AnnounceOrderCompleted });
            _items.Add(new SettingItem { Label = "Order failed", ConfigEntry = Plugin.AnnounceOrderFailed });

            // Threats (hostility level change gives more detail than game's deadly-only alert)
            _items.Add(new SettingItem { Label = "Hostility level change", ConfigEntry = Plugin.AnnounceHostilityLevelChange });

            // Progression
            _items.Add(new SettingItem { Label = "Reputation changed", ConfigEntry = Plugin.AnnounceReputationChanged });
            _items.Add(new SettingItem { Label = "Good discovered", ConfigEntry = Plugin.AnnounceGoodDiscovered });
            _items.Add(new SettingItem { Label = "Game result", ConfigEntry = Plugin.AnnounceGameResult });
            _items.Add(new SettingItem { Label = "Blueprint available", ConfigEntry = Plugin.AnnounceBlueprintAvailable });
            _items.Add(new SettingItem { Label = "Cornerstone available", ConfigEntry = Plugin.AnnounceCornerstoneAvailable });

            // Resources
            _items.Add(new SettingItem { Label = "Expedition departed", ConfigEntry = Plugin.AnnouncePortExpeditionStarted });

            // News/Warnings
            _items.Add(new SettingItem { Label = "Game warnings", ConfigEntry = Plugin.AnnounceGameWarnings });

            // Sealed Forest
            _items.Add(new SettingItem { Label = "Plague events", ConfigEntry = Plugin.AnnouncePlagueEvents });
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle a search key (A-Z) for type-ahead navigation.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            if (_items.Count == 0) return;

            _search.AddChar(c);

            // Search for first matching item
            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Label.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem(includeHeader: false);
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }

        /// <summary>
        /// Handle backspace key to remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            // Re-search with shortened buffer
            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Label.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem(includeHeader: false);
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }
    }
}
