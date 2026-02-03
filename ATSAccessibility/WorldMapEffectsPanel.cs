using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating world map tile effects.
    /// Shows biome name/description and all effects with descriptions.
    /// Not an IKeyHandler - called by WorldMapNavigator via ProcessKeyEvent().
    /// </summary>
    public class WorldMapEffectsPanel : MenuBase
    {
        private List<(string name, string description)> _items = new List<(string, string)>();
        private Vector3Int _tilePos;

        // ========================================
        // BRIDGE
        // ========================================

        /// <summary>
        /// Bridge for WorldMapNavigator which calls ProcessKeyEvent(KeyCode, KeyModifiers).
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers = default)
        {
            if (!IsOpen) return false;
            return ProcessKey(keyCode, modifiers);
        }

        // ========================================
        // CUSTOM OPEN
        // ========================================

        /// <summary>
        /// Open the effects panel for the given tile position.
        /// </summary>
        public void Open(Vector3Int tilePos)
        {
            // If same tile, close the panel (toggle off)
            if (IsOpen && _tilePos == tilePos)
            {
                Close();
                return;
            }

            // Don't reveal effects on unexplored tiles
            if (!WorldMapReflection.WorldMapIsRevealed(tilePos))
            {
                Speech.Say("Unexplored");
                if (IsOpen) Close();  // Close if was open showing different tile
                return;
            }

            // If already open with different tile, close first
            if (IsOpen) Close();

            _tilePos = tilePos;
            Open();  // MenuBase.Open() -> RefreshData() -> GetOpenAnnouncement() -> OnOpened()
        }

        // ========================================
        // MENUBASE ABSTRACTS
        // ========================================

        protected override string OverlayName => "Effects";
        protected override string EmptyMessage => "No effects available";

        protected override int GetItemCount() => _items.Count;

        protected override string GetLabel(int index)
        {
            if (index < 0 || index >= _items.Count) return null;
            return _items[index].name;
        }

        protected override void RefreshData() => RefreshItems();

        protected override EnterAction OnEnter(int index) => EnterAction.None;

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override EscapeAction OnEscape() => EscapeAction.Close;

        protected override string GetOpenAnnouncement()
        {
            if (_items.Count == 0)
                return EmptyMessage;
            // Return null so OnOpened() can announce with description
            return null;
        }

        protected override void OnOpened()
        {
            if (_items.Count == 0)
            {
                Close();
                return;
            }
            AnnounceCurrentItem();
        }

        protected override void OnClosed()
        {
            _items.Clear();
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Effects panel closed");
        }

        protected override void AnnounceCurrentItem()
        {
            if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

            var item = _items[CurrentIndex];

            string message;
            if (string.IsNullOrEmpty(item.description))
                message = item.name;
            else
                message = $"{item.name}. {item.description}";

            Speech.Say(message);
        }

        // ========================================
        // PRIVATE
        // ========================================

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
