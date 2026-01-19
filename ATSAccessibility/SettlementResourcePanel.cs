using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating settlement resources by category.
    /// Two-panel system: left panel has categories, right panel has items in category.
    /// </summary>
    public class SettlementResourcePanel
    {
        /// <summary>
        /// Represents a resource category (e.g., Food, Building Materials).
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public int TotalAmount { get; set; }
            public int Order { get; set; }
            public List<ResourceItem> Items { get; set; } = new List<ResourceItem>();
        }

        /// <summary>
        /// Represents an individual resource item within a category.
        /// </summary>
        private class ResourceItem
        {
            public string Name { get; set; }
            public int Amount { get; set; }
            public int Order { get; set; }
        }

        private bool _isOpen = false;
        private List<Category> _categories = new List<Category>();
        private int _currentCategoryIndex = 0;
        private int _currentItemIndex = 0;
        private bool _focusOnItems = false;  // Left panel (categories) vs right panel (items)

        /// <summary>
        /// Whether the resource panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the resource panel and announce the first category.
        /// Toggles closed if already open.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            // Build category list from game data
            RefreshCategories();

            if (_categories.Count == 0)
            {
                Speech.Say("No resources in storage");
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentItemIndex = 0;
            _focusOnItems = false;

            AnnounceCurrentCategory();
            Debug.Log("[ATSAccessibility] Resource panel opened");
        }

        /// <summary>
        /// Close the resource panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;  // Block the Cancel action that will fire this frame
            _categories.Clear();
            Speech.Say("Resource panel closed");
            Debug.Log("[ATSAccessibility] Resource panel closed");
        }

        /// <summary>
        /// Process a key event for the resource panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

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
                    return false;  // At root level, let parent handle

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return true;  // Consume all other keys while panel is open
            }
        }

        /// <summary>
        /// Navigate categories (left panel) with Up/Down.
        /// </summary>
        private void NavigateCategory(int direction)
        {
            if (!_isOpen || _categories.Count == 0) return;

            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, _categories.Count);
            _currentItemIndex = 0;  // Reset item index when changing category
            AnnounceCurrentCategory();
        }

        /// <summary>
        /// Navigate items (right panel) with Up/Down when in items mode.
        /// </summary>
        private void NavigateItem(int direction)
        {
            if (!_isOpen || !_focusOnItems) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Items.Count == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, category.Items.Count);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Enter items mode (Enter or Right arrow).
        /// </summary>
        private void EnterItems()
        {
            if (!_isOpen) return;

            var category = _categories[_currentCategoryIndex];

            if (category.Items.Count == 0)
            {
                Speech.Say("No items in this category");
                return;
            }

            _focusOnItems = true;
            _currentItemIndex = 0;
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Return to categories (Left arrow).
        /// </summary>
        private void ReturnToCategories()
        {
            if (!_isOpen) return;

            if (_focusOnItems)
            {
                _focusOnItems = false;
                AnnounceCurrentCategory();
            }
        }

        /// <summary>
        /// Refresh the category list with current game data.
        /// Groups stored goods by their category, sorted by category order.
        /// </summary>
        private void RefreshCategories()
        {
            _categories.Clear();

            // Get all stored goods (only those with amount > 0)
            var storedGoods = GameReflection.GetAllStoredGoods();
            if (storedGoods.Count == 0)
            {
                Debug.Log("[ATSAccessibility] No stored goods found");
                return;
            }

            // Get all GoodModel definitions
            var allGoods = GameReflection.GetAllGoodModels();
            if (allGoods == null)
            {
                Debug.LogWarning("[ATSAccessibility] Could not get GoodModels from Settings");
                return;
            }

            // Build a lookup: goodName -> (displayName, category, categoryName, order)
            var goodInfoLookup = new Dictionary<string, (string displayName, object category, string categoryName, int categoryOrder, int goodOrder)>();
            foreach (var goodModel in allGoods)
            {
                if (!GameReflection.IsGoodActive(goodModel)) continue;

                var goodName = GameReflection.GetModelName(goodModel);
                if (string.IsNullOrEmpty(goodName)) continue;

                var displayName = GameReflection.GetDisplayName(goodModel) ?? goodName;
                var category = GameReflection.GetGoodCategory(goodModel);
                var categoryName = category != null ? (GameReflection.GetDisplayName(category) ?? "Other") : "Other";
                var categoryOrder = category != null ? GameReflection.GetModelOrder(category) : 999;
                var goodOrder = GameReflection.GetModelOrder(goodModel);

                goodInfoLookup[goodName] = (displayName, category, categoryName, categoryOrder, goodOrder);
            }

            // Group stored goods by category
            var categoryDict = new Dictionary<string, Category>();
            foreach (var kvp in storedGoods)
            {
                var goodName = kvp.Key;
                var amount = kvp.Value;

                if (!goodInfoLookup.TryGetValue(goodName, out var info))
                {
                    // Good not found in models, use raw name
                    info = (goodName, null, "Other", 999, 0);
                }

                if (!categoryDict.TryGetValue(info.categoryName, out var category))
                {
                    category = new Category
                    {
                        Name = info.categoryName,
                        TotalAmount = 0,
                        Order = info.categoryOrder,
                        Items = new List<ResourceItem>()
                    };
                    categoryDict[info.categoryName] = category;
                }

                category.TotalAmount += amount;
                category.Items.Add(new ResourceItem
                {
                    Name = info.displayName,
                    Amount = amount,
                    Order = info.goodOrder
                });
            }

            // Sort categories by order, then by name
            _categories = categoryDict.Values
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Name)
                .ToList();

            // Sort items within each category by order, then by name
            foreach (var category in _categories)
            {
                category.Items = category.Items
                    .OrderBy(i => i.Order)
                    .ThenBy(i => i.Name)
                    .ToList();
            }

            Debug.Log($"[ATSAccessibility] Resource panel refreshed: {_categories.Count} categories, {storedGoods.Count} goods");
        }

        /// <summary>
        /// Announce the current category (left panel).
        /// </summary>
        private void AnnounceCurrentCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            int itemCount = category.Items.Count;
            string typeWord = itemCount == 1 ? "type" : "types";

            Speech.Say($"{category.Name}: {itemCount} {typeWord}");
            Debug.Log($"[ATSAccessibility] Category {_currentCategoryIndex + 1}/{_categories.Count}: {category.Name}, {itemCount} {typeWord}");
        }

        /// <summary>
        /// Announce the current item (right panel).
        /// </summary>
        private void AnnounceCurrentItem()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

            var item = category.Items[_currentItemIndex];
            Speech.Say($"{item.Name}, {item.Amount}");
            Debug.Log($"[ATSAccessibility] Item: {item.Name} x{item.Amount}");
        }
    }
}
