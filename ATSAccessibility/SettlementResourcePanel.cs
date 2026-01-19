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

            Debug.Log($"[ATSAccessibility] Resource panel refreshed: {_categories.Count} categories, {storedGoods.Count} goods");
        }

        protected override void ClearData()
        {
            _categories.Clear();
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
    }
}
