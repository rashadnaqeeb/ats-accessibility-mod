using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating world map tile effects.
    /// Shows biome name/description and all effects with descriptions.
    /// </summary>
    public class WorldMapEffectsPanel
    {
        private bool _isOpen = false;
        private List<(string name, string description)> _items = new List<(string, string)>();
        private int _currentIndex = 0;
        private Vector3Int _tilePos;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether the effects panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the effects panel for the given tile position.
        /// </summary>
        public void Open(Vector3Int tilePos)
        {
            if (_isOpen)
            {
                // If same tile, close the panel (toggle off)
                if (_tilePos == tilePos)
                {
                    Close();
                    return;
                }
                // Different tile - refresh items for new position (fall through)
            }

            // Don't reveal effects on unexplored tiles
            if (!WorldMapReflection.WorldMapIsRevealed(tilePos))
            {
                Speech.Say("Unexplored");
                if (_isOpen) Close();  // Close if was open showing different tile
                return;
            }

            _tilePos = tilePos;
            RefreshItems();

            if (_items.Count == 0)
            {
                Speech.Say("No effects available");
                if (_isOpen) Close();  // Close if was open showing different tile
                return;
            }

            _isOpen = true;
            _currentIndex = 0;
            _search.Clear();

            AnnounceCurrentItem();
        }

        /// <summary>
        /// Close the effects panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;
            _search.Clear();
            _items.Clear();
            Speech.Say("Effects panel closed");
        }

        /// <summary>
        /// Process a key event for the effects panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateItem(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateItem(1);
                    return true;

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
                    Close();
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    return true;  // Consume all other keys while panel is open
            }
        }

        /// <summary>
        /// Navigate to the next or previous item with wrapping.
        /// </summary>
        private void NavigateItem(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Announce the current item.
        /// Format: "Name. Description"
        /// </summary>
        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            string message;
            if (string.IsNullOrEmpty(item.description))
                message = item.name;
            else
                message = $"{item.name}. {item.description}";

            Speech.Say(message);
        }

        /// <summary>
        /// Build the list of items from biome and effects.
        /// </summary>
        private void RefreshItems()
        {
            _items.Clear();

            // Add biome as first item
            var biomeName = WorldMapReflection.WorldMapGetBiomeName(_tilePos);
            var biomeDescription = WorldMapReflection.WorldMapGetBiomeDescription(_tilePos);

            if (!string.IsNullOrEmpty(biomeName))
            {
                _items.Add((biomeName, biomeDescription ?? ""));
            }

            // Add field effects
            var effects = WorldMapReflection.WorldMapGetFieldEffectsWithDescriptions(_tilePos);
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    _items.Add(effect);
                }
            }

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
                if (_items[i].name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem();
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
                if (_items[i].name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem();
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }
    }
}
