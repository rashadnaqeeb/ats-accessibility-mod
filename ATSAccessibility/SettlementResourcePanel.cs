using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating settlement resources by category.
    /// Two-panel system: left panel has categories, right panel has items in category.
    /// </summary>
    public class SettlementResourcePanel : TwoLevelPanel
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

        private List<Category> _categories = new List<Category>();

        // Flat list of all resources for cross-category search
        private List<(int categoryIndex, int itemIndex, string name)> _allResources =
            new List<(int, int, string)>();

        // ========================================
        // ABSTRACT MEMBER IMPLEMENTATIONS
        // ========================================

        protected override string PanelName => "Resource panel";
        protected override string EmptyMessage => "No resources in storage";
        protected override int CategoryCount => _categories.Count;
        protected override int CurrentItemCount =>
            _currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count
                ? _categories[_currentCategoryIndex].Items.Count
                : 0;

        protected override void RefreshData()
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

            // Build flat list of all resources for cross-category search
            _allResources.Clear();
            for (int ci = 0; ci < _categories.Count; ci++)
            {
                var cat = _categories[ci];
                for (int ii = 0; ii < cat.Items.Count; ii++)
                {
                    _allResources.Add((ci, ii, cat.Items[ii].Name));
                }
            }

            Debug.Log($"[ATSAccessibility] Resource panel refreshed: {_categories.Count} categories, {storedGoods.Count} goods");
        }

        protected override void ClearData()
        {
            _categories.Clear();
            _allResources.Clear();
        }

        protected override void AnnounceCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            int itemCount = category.Items.Count;
            string typeWord = itemCount == 1 ? "type" : "types";

            Speech.Say($"{category.Name}: {itemCount} {typeWord}");
            Debug.Log($"[ATSAccessibility] Category {_currentCategoryIndex + 1}/{_categories.Count}: {category.Name}, {itemCount} {typeWord}");
        }

        protected override void AnnounceItem()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

            var item = category.Items[_currentItemIndex];
            Speech.Say($"{item.Name}, {item.Amount}");
            Debug.Log($"[ATSAccessibility] Item: {item.Name} x{item.Amount}");
        }

        // ========================================
        // CROSS-CATEGORY ITEM NAVIGATION
        // ========================================

        /// <summary>
        /// Navigate items, flowing into the next/previous category at boundaries.
        /// Announces category name when crossing into a new category.
        /// </summary>
        private void NavigateItemAcrossCategories(int direction)
        {
            int itemCount = CurrentItemCount;
            if (itemCount == 0) return;

            int newIndex = _currentItemIndex + direction;

            if (newIndex >= itemCount)
            {
                // Past end of category - move to next category's first item
                _currentCategoryIndex = (_currentCategoryIndex + 1) % CategoryCount;
                _currentItemIndex = 0;
                AnnounceCategoryAndItem();
            }
            else if (newIndex < 0)
            {
                // Before start of category - move to previous category's last item
                _currentCategoryIndex = (_currentCategoryIndex - 1 + CategoryCount) % CategoryCount;
                _currentItemIndex = CurrentItemCount - 1;
                AnnounceCategoryAndItem();
            }
            else
            {
                _currentItemIndex = newIndex;
                AnnounceItem();
            }
        }

        /// <summary>
        /// Announce category name followed by current item when crossing category boundaries.
        /// </summary>
        private void AnnounceCategoryAndItem()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

            var item = category.Items[_currentItemIndex];
            Speech.Say($"{category.Name}. {item.Name}, {item.Amount}");
        }

        // ========================================
        // CROSS-CATEGORY SEARCH (OVERRIDE BASE)
        // ========================================

        /// <summary>
        /// Override ProcessKeyEvent to handle cross-category search.
        /// Resource panel searches ALL resources across all categories.
        /// </summary>
        public new bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_focusOnItems)
                        NavigateItemAcrossCategories(-1);
                    else
                        NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_focusOnItems)
                        NavigateItemAcrossCategories(1);
                    else
                        NavigateCategory(1);
                    return true;

                case KeyCode.Home:
                    if (_focusOnItems)
                        JumpToItem(0);
                    else
                        JumpToCategory(0);
                    return true;

                case KeyCode.End:
                    if (_focusOnItems)
                        JumpToItem(CurrentItemCount - 1);
                    else
                        JumpToCategory(CategoryCount - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    EnterItems();
                    return true;

                case KeyCode.LeftArrow:
                    if (_focusOnItems)
                    {
                        _focusOnItems = false;
                        _search.Clear();
                        AnnounceCategory();
                        return true;
                    }
                    // Pass to parent (InfoPanelMenu) to close this panel
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
                    // Pass to parent to handle panel closing
                    return false;

                default:
                    // Handle A-Z keys for cross-category type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleCrossSearchKey(c);
                    }
                    return true;  // Consume all keys while panel is open
            }
        }

        /// <summary>
        /// Handle a search key (A-Z) for cross-category type-ahead navigation.
        /// Searches all resources across all categories.
        /// </summary>
        private void HandleCrossSearchKey(char c)
        {
            if (_allResources.Count == 0) return;

            _search.AddChar(c);

            // Find first resource starting with buffer (case-insensitive)
            int matchIndex = _search.FindMatch(_allResources, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allResources[matchIndex];
                _currentCategoryIndex = match.categoryIndex;
                _currentItemIndex = match.itemIndex;
                _focusOnItems = true;  // Auto-enter items panel
                AnnounceItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        /// <summary>
        /// Handle backspace key for cross-category search.
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
            int matchIndex = _search.FindMatch(_allResources, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allResources[matchIndex];
                _currentCategoryIndex = match.categoryIndex;
                _currentItemIndex = match.itemIndex;
                _focusOnItems = true;
                AnnounceItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }
    }
}
