using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Virtual speech-only panel for navigating settlement resources by category.
	/// Level 0 = categories, Level 1 = items in category.
	/// Cross-category item navigation flows between categories on Up/Down at boundaries.
	/// Flat cross-category search across all resources.
	/// </summary>
	public class SettlementResourcePanel: MenuBase {
		/// <summary>
		/// Represents a resource category (e.g., Food, Building Materials).
		/// </summary>
		private class Category {
			public string Name { get; set; }
			public int TotalAmount { get; set; }
			public int Order { get; set; }
			public List<ResourceItem> Items { get; set; } = new List<ResourceItem>();
		}

		/// <summary>
		/// Represents an individual resource item within a category.
		/// </summary>
		private class ResourceItem {
			public string Name { get; set; }
			public string GoodName { get; set; }  // Internal name for lookups
			public int Amount { get; set; }
			public int Order { get; set; }
		}

		private List<Category> _categories = new List<Category>();

		// Flat list of all resources for cross-category search
		private List<(int categoryIndex, int itemIndex, string name)> _allResources =
			new List<(int, int, string)>();

		// Compatibility aliases for readability
		private int _currentCategoryIndex { get => _indices[0]; set => _indices[0] = value; }
		private int _currentItemIndex { get => _indices[1]; set => _indices[1] = value; }

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("panel.resource.title");
		protected override string EmptyMessage => Strings.Get("panel.resource.empty");

		protected override int GetItemCount() {
			if (Level == 0)
				return _categories.Count;

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
				return _categories[_currentCategoryIndex].Items.Count;

			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count) {
					var cat = _categories[index];
					int itemCount = cat.Items.Count;
					string typeWord = itemCount == 1
						? Strings.Get("panel.resource.type_word_one")
						: Strings.Get("panel.resource.type_word_other");
					return Strings.Get("panel.resource.category", cat.Name, itemCount, typeWord);
				}
				return null;
			}

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count) {
				var items = _categories[_currentCategoryIndex].Items;
				if (index >= 0 && index < items.Count)
					return Strings.Get("panel.resource.item", items[index].Name, items[index].Amount);
			}
			return null;
		}

		protected override void RefreshData() {
			_categories.Clear();

			// Get all stored goods (only those with amount > 0)
			var storedGoods = GameReflection.GetAllStoredGoods();
			if (storedGoods.Count == 0) {
				Debug.Log("[ATSAccessibility] No stored goods found");
				return;
			}

			// Get all GoodModel definitions
			var allGoods = GameReflection.GetAllGoodModels();
			if (allGoods == null) {
				Debug.LogWarning("[ATSAccessibility] Could not get GoodModels from Settings");
				return;
			}

			// Build a lookup: goodName -> (displayName, category, categoryName, order)
			var goodInfoLookup = new Dictionary<string, (string displayName, object category, string categoryName, int categoryOrder, int goodOrder)>();
			foreach (var goodModel in allGoods) {
				if (!GameReflection.IsGoodActive(goodModel)) continue;

				var goodName = GameReflection.GetModelName(goodModel);
				if (string.IsNullOrEmpty(goodName)) continue;

				var displayName = GameReflection.GetDisplayName(goodModel) ?? goodName;
				var category = GameReflection.GetGoodCategory(goodModel);
				var categoryName = category != null ? (GameReflection.GetDisplayName(category) ?? Strings.Get("common.other")) : Strings.Get("common.other");
				var categoryOrder = category != null ? GameReflection.GetModelOrder(category) : 999;
				var goodOrder = GameReflection.GetModelOrder(goodModel);

				goodInfoLookup[goodName] = (displayName, category, categoryName, categoryOrder, goodOrder);
			}

			// Group stored goods by category
			var categoryDict = new Dictionary<string, Category>();
			foreach (var kvp in storedGoods) {
				var goodName = kvp.Key;
				var amount = kvp.Value;

				if (!goodInfoLookup.TryGetValue(goodName, out var info)) {
					// Good not found in models, use raw name
					info = (goodName, null, Strings.Get("common.other"), 999, 0);
				}

				if (!categoryDict.TryGetValue(info.categoryName, out var category)) {
					category = new Category {
						Name = info.categoryName,
						TotalAmount = 0,
						Order = info.categoryOrder,
						Items = new List<ResourceItem>()
					};
					categoryDict[info.categoryName] = category;
				}

				category.TotalAmount += amount;
				category.Items.Add(new ResourceItem {
					Name = info.displayName,
					GoodName = goodName,
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
			foreach (var category in _categories) {
				category.Items = category.Items
					.OrderBy(i => i.Order)
					.ThenBy(i => i.Name)
					.ToList();
			}

			// Build flat list of all resources for cross-category search
			_allResources.Clear();
			for (int ci = 0; ci < _categories.Count; ci++) {
				var cat = _categories[ci];
				for (int ii = 0; ii < cat.Items.Count; ii++) {
					_allResources.Add((ci, ii, cat.Items[ii].Name));
				}
			}

			Debug.Log($"[ATSAccessibility] Resource panel refreshed: {_categories.Count} categories, {storedGoods.Count} goods");
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count
					&& _categories[_currentCategoryIndex].Items.Count > 0)
					return EnterAction.DrillDown;

				Speech.Say(Strings.Get("common.empty_category"));
				return EnterAction.None;
			}
			return EnterAction.None;
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.LeftArrow && Level == 0)
				return false; // Pass to InfoPanelMenu to close child panel

			// Cross-category item navigation at Level 1 (yield to search when active)
			if (Level == 1 && !_search.IsSearchActive && (keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow)) {
				NavigateItemAcrossCategories(keyCode == KeyCode.DownArrow ? 1 : -1);
				return true;
			}

			return null;
		}

		private static readonly List<HelpEntry> _resourceHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			HelpEntry.Loca("Alt+I", "panel.settlement_resource.help.alt_i"),
		};
		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _resourceHelpEntries;

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0) return EmptyMessage;
			return GetLabel(0);
		}

		protected override void OnClosed() {
			_categories.Clear();
			_allResources.Clear();
			if (!IsClosingSilently) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say(Strings.Get("panel.resource.closed", OverlayName));
			}
		}

		// ========================================
		// RESOURCE DESCRIPTION (Alt+I)
		// ========================================

		/// <summary>
		/// Announce the description/tooltip of the currently focused resource.
		/// Called from InfoPanelMenu on Alt+I.
		/// </summary>
		public void AnnounceCurrentItemDescription() {
			if (Level != 1) return;
			if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

			var category = _categories[_currentCategoryIndex];
			if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

			var item = category.Items[_currentItemIndex];
			string description = GameReflection.GetGoodDescription(item.GoodName);

			if (string.IsNullOrEmpty(description)) {
				Speech.Say(Strings.Get("panel.resource.no_description", item.Name));
			} else {
				Speech.Say(Strings.Get("panel.resource.with_description", item.Name, description));
			}
		}

		// ========================================
		// CROSS-CATEGORY ITEM NAVIGATION
		// ========================================

		/// <summary>
		/// Navigate items, flowing into the next/previous category at boundaries.
		/// Announces category name when crossing into a new category.
		/// </summary>
		private void NavigateItemAcrossCategories(int direction) {
			int itemCount = GetItemCount();
			if (itemCount == 0) return;

			int newIndex = _currentItemIndex + direction;

			if (newIndex >= itemCount) {
				// Past end of category - find next non-empty category
				int originalCategory = _currentCategoryIndex;
				do {
					_currentCategoryIndex = (_currentCategoryIndex + 1) % _categories.Count;
				} while (_categories[_currentCategoryIndex].Items.Count == 0 && _currentCategoryIndex != originalCategory);

				if (_categories[_currentCategoryIndex].Items.Count == 0) return;
				_currentItemIndex = 0;
				AnnounceCategoryAndItem();
			} else if (newIndex < 0) {
				// Before start of category - find previous non-empty category
				int originalCategory = _currentCategoryIndex;
				do {
					_currentCategoryIndex = (_currentCategoryIndex - 1 + _categories.Count) % _categories.Count;
				} while (_categories[_currentCategoryIndex].Items.Count == 0 && _currentCategoryIndex != originalCategory);

				if (_categories[_currentCategoryIndex].Items.Count == 0) return;
				_currentItemIndex = _categories[_currentCategoryIndex].Items.Count - 1;
				AnnounceCategoryAndItem();
			} else {
				_currentItemIndex = newIndex;
				AnnounceCurrentItem();
			}
		}

		/// <summary>
		/// Announce category name followed by current item when crossing category boundaries.
		/// </summary>
		private void AnnounceCategoryAndItem() {
			if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

			var category = _categories[_currentCategoryIndex];
			if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

			var item = category.Items[_currentItemIndex];
			Speech.Say(Strings.Get("panel.resource.cross_category", category.Name, item.Name, item.Amount));
		}

		// ========================================
		// ISEARCHABLE OVERRIDES (flat cross-category search)
		// ========================================

		protected override int SearchItemCount => _allResources.Count;

		protected override string GetSearchName(int index) {
			if (index >= 0 && index < _allResources.Count)
				return _allResources[index].name;
			return null;
		}

		protected override void SearchMoveTo(int index) {
			if (index < 0 || index >= _allResources.Count) return;

			var match = _allResources[index];
			_currentCategoryIndex = match.categoryIndex;
			_currentItemIndex = match.itemIndex;
			if (Level == 0) SetLevel(1);
			AnnounceCurrentItem();
		}
	}
}
