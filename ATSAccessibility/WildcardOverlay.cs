using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Accessible overlay for the WildcardPopup (mid-game blueprint selection).
	/// Provides two-level category/building navigation with type-ahead search,
	/// Space to toggle selection, and Enter to confirm picks.
	///
	/// Level 0 = categories, Level 1 = buildings within the selected category.
	/// </summary>
	public class WildcardOverlay: MenuBase {
		/// <summary>
		/// Represents a building category with its buildings.
		/// </summary>
		private class Category {
			public string Name { get; set; }
			public int Order { get; set; }
			public List<BuildingItem> Buildings { get; set; } = new List<BuildingItem>();
		}

		/// <summary>
		/// Represents an individual building within a category.
		/// </summary>
		private class BuildingItem {
			public string Name { get; set; }
			public int Order { get; set; }
			public object Model { get; set; }
		}

		// Data
		private object _popup;
		private List<Category> _categories = new List<Category>();
		private List<(int catIdx, int bldIdx, string name)> _allBuildings =
			new List<(int, int, string)>();

		// Picks
		private int _picksRequired;
		private HashSet<object> _selectedModels = new HashSet<object>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Wildcard";
		protected override string EmptyMessage => "No buildings available";

		protected override int GetItemCount() {
			switch (Level) {
				case 0: return _categories.Count;
				case 1:
					if (_categories.Count > 0 && _indices[0] >= 0 && _indices[0] < _categories.Count)
						return _categories[_indices[0]].Buildings.Count;
					return 0;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0:
					if (index < 0 || index >= _categories.Count) return null;
					return _categories[index].Name;

				case 1:
					if (_categories.Count == 0 || _indices[0] < 0 || _indices[0] >= _categories.Count) return null;
					var category = _categories[_indices[0]];
					if (index < 0 || index >= category.Buildings.Count) return null;

					var building = category.Buildings[index];
					bool isSelected = _selectedModels.Contains(building.Model);
					return isSelected ? $"{building.Name}, selected" : building.Name;

				default: return null;
			}
		}

		protected override void RefreshData() {
			_categories.Clear();
			_allBuildings.Clear();

			var buildings = WildcardReflection.GetAvailableBuildings();
			if (buildings == null || buildings.Count == 0) return;

			// Group by category, filtering to HUD categories
			var categoryDict = new Dictionary<string, Category>();
			foreach (var building in buildings) {
				if (!categoryDict.TryGetValue(building.CategoryName, out var category)) {
					category = new Category {
						Name = building.CategoryName,
						Order = building.CategoryOrder
					};
					categoryDict[building.CategoryName] = category;
				}

				category.Buildings.Add(new BuildingItem {
					Name = building.DisplayName,
					Order = building.BuildingOrder,
					Model = building.Model
				});
			}

			// Sort categories by order then name
			_categories = categoryDict.Values
				.OrderBy(c => c.Order)
				.ThenBy(c => c.Name)
				.ToList();

			// Sort buildings within each category
			foreach (var category in _categories) {
				category.Buildings = category.Buildings
					.OrderBy(b => b.Order)
					.ThenBy(b => b.Name)
					.ToList();
			}

			// Build flat list for cross-category type-ahead search
			for (int ci = 0; ci < _categories.Count; ci++) {
				var cat = _categories[ci];
				for (int bi = 0; bi < cat.Buildings.Count; bi++) {
					_allBuildings.Add((ci, bi, cat.Buildings[bi].Name));
				}
			}

			Debug.Log($"[ATSAccessibility] WildcardOverlay data refreshed: {_categories.Count} categories, {_allBuildings.Count} buildings");
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					if (_categories.Count == 0 || index < 0 || index >= _categories.Count) return EnterAction.None;
					if (_categories[index].Buildings.Count == 0) {
						Speech.Say("No buildings in this category");
						return EnterAction.None;
					}
					return EnterAction.DrillDown;

				case 1:
					return EnterAction.Action;

				default:
					return EnterAction.None;
			}
		}

		protected override void OnAction(int index) {
			if (Level == 1)
				ConfirmPicks();
		}

		protected override void OnSpace(int index) {
			if (Level == 1)
				ToggleCurrentBuilding();
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		protected override void StorePopup(object popup) {
			_popup = popup;
			_picksRequired = WildcardReflection.GetPicksRequired();

			var existingPicks = WildcardReflection.GetCurrentPicks(popup);
			foreach (var pick in existingPicks)
				_selectedModels.Add(pick);
		}

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0)
				return $"Wildcard pick, select {_picksRequired}. {EmptyMessage}";
			return $"Wildcard pick, select {_picksRequired}. {_categories[0].Name}";
		}

		protected override void OnClosed() {
			_popup = null;
			_selectedModels.Clear();
			_categories.Clear();
			_allBuildings.Clear();
		}

		// ========================================
		// CROSS-CATEGORY SEARCH
		// ========================================

		protected override int SearchItemCount => _allBuildings.Count;

		protected override int SearchCurrentIndex {
			get {
				if (Level != 1 || _categories.Count == 0) return 0;

				// Compute flat index from category + building indices
				int flatIndex = 0;
				for (int ci = 0; ci < _indices[0] && ci < _categories.Count; ci++)
					flatIndex += _categories[ci].Buildings.Count;
				flatIndex += CurrentIndex;
				return flatIndex;
			}
		}

		protected override string GetSearchName(int index) {
			if (index >= 0 && index < _allBuildings.Count)
				return _allBuildings[index].name;
			return null;
		}

		protected override void SearchMoveTo(int index) {
			if (index < 0 || index >= _allBuildings.Count) return;
			var match = _allBuildings[index];
			_indices[0] = match.catIdx;
			SetLevel(1);
			_indices[1] = match.bldIdx;
			AnnounceCurrentItem();
		}

		// ========================================
		// SELECTION
		// ========================================

		private void ToggleCurrentBuilding() {
			if (_categories.Count == 0 || _indices[0] < 0 || _indices[0] >= _categories.Count) return;
			var category = _categories[_indices[0]];
			if (CurrentIndex < 0 || CurrentIndex >= category.Buildings.Count) return;

			var building = category.Buildings[CurrentIndex];

			bool toggled = WildcardReflection.ToggleSlot(_popup, building.Model);
			if (!toggled) {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayButtonClick();

			// Update local selection tracking
			if (_selectedModels.Contains(building.Model))
				_selectedModels.Remove(building.Model);
			else
				_selectedModels.Add(building.Model);

			// Read authoritative count from popup
			int currentCount = WildcardReflection.GetCurrentPickCount(_popup);

			if (_selectedModels.Contains(building.Model))
				Speech.Say($"Selected. {currentCount} of {_picksRequired}");
			else
				Speech.Say($"Deselected. {currentCount} of {_picksRequired}");
		}

		// ========================================
		// CONFIRM
		// ========================================

		private void ConfirmPicks() {
			int currentCount = WildcardReflection.GetCurrentPickCount(_popup);

			if (currentCount != _picksRequired) {
				Speech.Say($"Select {_picksRequired} blueprints, {currentCount} selected");
				SoundManager.PlayFailed();
				return;
			}

			bool confirmed = WildcardReflection.Confirm(_popup);
			if (confirmed) {
				SoundManager.PlayButtonClick();
				Speech.Say("Blueprints unlocked");
				// Popup hides itself -> OnPopupHidden -> Close()
			} else {
				Speech.Say("Could not confirm");
				SoundManager.PlayFailed();
			}
		}
	}
}
