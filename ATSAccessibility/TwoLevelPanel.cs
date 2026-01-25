using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Abstract base class for two-level navigation panels (categories → items).
    /// Provides consistent keyboard handling: Up/Down navigate, Enter/Right enter items,
    /// Left returns to categories, Escape closes.
    /// </summary>
    public abstract class TwoLevelPanel
    {
        // ========================================
        // SHARED STATE
        // ========================================

        protected bool _isOpen;
        protected int _currentCategoryIndex;
        protected int _currentItemIndex;
        protected bool _focusOnItems;

        // Type-ahead search
        protected readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Whether the panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the panel and announce the first category.
        /// Toggle behavior - if already open, close it.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            // Build data from game state
            RefreshData();

            if (!HasAnyItems())
            {
                Speech.Say(EmptyMessage);
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentItemIndex = 0;
            _focusOnItems = false;
            _search.Clear();

            AnnounceCategory();
            Debug.Log($"[ATSAccessibility] {PanelName} opened");
        }

        /// <summary>
        /// Close the panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;
            _search.Clear();
            ClearData();
            Speech.Say($"{PanelName} closed");
            Debug.Log($"[ATSAccessibility] {PanelName} closed");
        }

        /// <summary>
        /// Process a key event for the panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_focusOnItems)
                        NavigateItem(-1);
                    else
                        NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_focusOnItems)
                        NavigateItem(1);
                    else
                        NavigateCategory(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    EnterItems();
                    return true;

                case KeyCode.LeftArrow:
                    if (_focusOnItems)
                    {
                        ReturnToCategories();
                        return true;
                    }
                    // Pass to parent (InfoPanelMenu) to close this panel
                    return false;

                case KeyCode.Backspace:
                    if (_focusOnItems)
                        HandleItemBackspace();
                    else
                        HandleCategoryBackspace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to parent to handle panel closing
                    return false;

                default:
                    // Handle A-Z keys for type-ahead search
                    // Always consume A-Z keys to prevent bubbling to other handlers
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        if (_focusOnItems)
                            HandleItemSearchKey(c);
                        else
                            HandleCategorySearchKey(c);
                        return true;
                    }
                    return true;  // Consume all other keys while panel is open
            }
        }

        // ========================================
        // ABSTRACT MEMBERS - DERIVED CLASSES IMPLEMENT
        // ========================================

        /// <summary>
        /// Display name for the panel (e.g., "Stats panel", "Mysteries panel").
        /// Used in open/close messages.
        /// </summary>
        protected abstract string PanelName { get; }

        /// <summary>
        /// Message to show when panel has no data (e.g., "No stats available").
        /// </summary>
        protected abstract string EmptyMessage { get; }

        /// <summary>
        /// Number of categories in the panel.
        /// </summary>
        protected abstract int CategoryCount { get; }

        /// <summary>
        /// Number of items in the current category.
        /// </summary>
        protected abstract int CurrentItemCount { get; }

        /// <summary>
        /// Refresh panel data from game state.
        /// Called when panel opens.
        /// </summary>
        protected abstract void RefreshData();

        /// <summary>
        /// Clear panel data.
        /// Called when panel closes.
        /// </summary>
        protected abstract void ClearData();

        /// <summary>
        /// Announce the current category.
        /// </summary>
        protected abstract void AnnounceCategory();

        /// <summary>
        /// Announce the current item.
        /// </summary>
        protected abstract void AnnounceItem();

        /// <summary>
        /// Check if the panel has any items to display.
        /// Default checks CategoryCount > 0, override for custom logic.
        /// </summary>
        protected virtual bool HasAnyItems()
        {
            return CategoryCount > 0;
        }

        /// <summary>
        /// Message to show when entering an empty category.
        /// Default: "No items in this category"
        /// </summary>
        protected virtual string NoItemsMessage => "No items in this category";

        // ========================================
        // SHARED NAVIGATION LOGIC
        // ========================================

        /// <summary>
        /// Navigate categories with Up/Down.
        /// </summary>
        protected void NavigateCategory(int direction)
        {
            if (CategoryCount == 0) return;

            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, CategoryCount);
            _currentItemIndex = 0;  // Reset item index when changing category
            AnnounceCategory();
        }

        /// <summary>
        /// Navigate items within current category.
        /// </summary>
        protected void NavigateItem(int direction)
        {
            int itemCount = CurrentItemCount;
            if (itemCount == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, itemCount);
            AnnounceItem();
        }

        /// <summary>
        /// Enter items view (Enter key or Right arrow).
        /// </summary>
        protected void EnterItems()
        {
            if (CurrentItemCount == 0)
            {
                Speech.Say(NoItemsMessage);
                return;
            }

            _focusOnItems = true;
            _currentItemIndex = 0;
            AnnounceItem();
        }

        /// <summary>
        /// Return to categories (Left arrow).
        /// </summary>
        protected void ReturnToCategories()
        {
            if (_focusOnItems)
            {
                _focusOnItems = false;
                _search.Clear();
                AnnounceCategory();
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Get the searchable name for an item at the given index.
        /// Subclasses must override this to enable type-ahead search.
        /// </summary>
        /// <param name="index">The item index within the current category.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetCurrentItemName(int index)
        {
            return null;  // Default: search not supported
        }

        /// <summary>
        /// Get the searchable name for a category at the given index.
        /// Subclasses must override this to enable type-ahead search at category level.
        /// </summary>
        /// <param name="index">The category index.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetCategoryName(int index)
        {
            return null;  // Default: search not supported
        }

        // ----------------------------------------
        // Category Search
        // ----------------------------------------

        private int FindMatchingCategory()
        {
            if (!_search.HasBuffer) return -1;

            int count = CategoryCount;
            if (count == 0) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < count; i++)
            {
                string name = GetCategoryName(i);
                if (!string.IsNullOrEmpty(name) &&
                    name.ToLowerInvariant().StartsWith(lowerBuffer))
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleCategorySearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindMatchingCategory();
            if (matchIndex >= 0)
            {
                _currentCategoryIndex = matchIndex;
                _currentItemIndex = 0;
                AnnounceCategory();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleCategoryBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindMatchingCategory();
            if (matchIndex >= 0)
            {
                _currentCategoryIndex = matchIndex;
                _currentItemIndex = 0;
                AnnounceCategory();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        // ----------------------------------------
        // Item Search
        // ----------------------------------------

        private int FindMatchingItem()
        {
            if (!_search.HasBuffer) return -1;

            int itemCount = CurrentItemCount;
            if (itemCount == 0) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < itemCount; i++)
            {
                string name = GetCurrentItemName(i);
                if (!string.IsNullOrEmpty(name) &&
                    name.ToLowerInvariant().StartsWith(lowerBuffer))
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleItemSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindMatchingItem();
            if (matchIndex >= 0)
            {
                _currentItemIndex = matchIndex;
                AnnounceItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleItemBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindMatchingItem();
            if (matchIndex >= 0)
            {
                _currentItemIndex = matchIndex;
                AnnounceItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }
    }
}
