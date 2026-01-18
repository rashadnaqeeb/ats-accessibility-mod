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
                Close();
                return;
            }

            // Don't reveal effects on unexplored tiles
            if (!WorldMapReflection.WorldMapIsRevealed(tilePos))
            {
                Speech.Say("Unexplored");
                return;
            }

            _tilePos = tilePos;
            RefreshItems();

            if (_items.Count == 0)
            {
                Speech.Say("No effects available");
                return;
            }

            _isOpen = true;
            _currentIndex = 0;

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

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateItem(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateItem(1);
                    return true;

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
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
        /// Announce the current item with position.
        /// Format: "Name. Description. X of Y"
        /// </summary>
        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            string position = $"{_currentIndex + 1} of {_items.Count}";

            string message;
            if (string.IsNullOrEmpty(item.description))
                message = $"{item.name}. {position}";
            else
                message = $"{item.name}. {item.description} {position}";

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
    }
}
