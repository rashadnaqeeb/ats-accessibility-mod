using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Virtual speech-only panel for selecting buildings to place.
	/// Two-panel system: left panel has categories, right panel has buildings in category.
	/// Extends MenuBase for level-based navigation with cross-category building navigation.
	/// </summary>
	public class BuildingMenuPanel: MenuBase {
		/// <summary>
		/// Represents a building category (e.g., Housing, Food Production).
		/// </summary>
		private class Category {
			public string Name { get; set; }
			public int Order { get; set; }
			public object Model { get; set; }
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

		private List<Category> _categories = new List<Category>();

		// Flat list of all buildings for cross-category search
		private List<(int categoryIndex, int buildingIndex, string name)> _allBuildings =
			new List<(int, int, string)>();

		// Reference to build mode controller for entering build mode
		private BuildModeController _buildModeController;

		// When closing for build placement, suppress "Building menu closed" speech
		private bool _closingForBuild;

		// ========================================
		// PUBLIC API
		// ========================================

		/// <summary>
		/// Set the build mode controller reference.
		/// </summary>
		public void SetBuildModeController(BuildModeController controller) {
			_buildModeController = controller;
		}

		/// <summary>
		/// Toggle the building menu open/closed.
		/// </summary>
		public void Toggle() {
			if (IsOpen) {
				SoundManager.PlayButtonClick();
				Close();
				return;
			}
			Open();
		}

		// ========================================
		// MENUBASE ABSTRACTS
		// ========================================

		protected override string OverlayName => "Building Menu";
		protected override string EmptyMessage => "No buildings available";

		protected override int GetItemCount() {
			if (Level == 0) return _categories.Count;
			if (Level == 1) return _indices[0] < _categories.Count ? _categories[_indices[0]].Buildings.Count : 0;
			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _categories.Count) return null;
				return _categories[index].Name;
			}
			if (Level == 1) {
				if (_indices[0] < 0 || _indices[0] >= _categories.Count) return null;
				var buildings = _categories[_indices[0]].Buildings;
				if (index < 0 || index >= buildings.Count) return null;
				return buildings[index].Name;
			}
			return null;
		}

		protected override void RefreshData() {
			RefreshCategories();
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _categories.Count) return EnterAction.None;
				var category = _categories[index];
				if (category.Buildings.Count == 0) {
					Speech.Say("No buildings in this category");
					return EnterAction.None;
				}
				return EnterAction.DrillDown;
			}
			return EnterAction.Action; // Level 1: select building
		}

		// ========================================
		// MENUBASE VIRTUAL OVERRIDES
		// ========================================

		protected override void OnAction(int index) {
			if (Level == 1)
				SelectBuilding();
		}

		protected override bool CanDrillDown(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _categories.Count) return false;
				return _categories[index].Buildings.Count > 0;
			}
			return false; // Level 1: no deeper drilling
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// Cross-category Up/Down at building level
			if (Level == 1) {
				if (keyCode == KeyCode.UpArrow) {
					NavigateBuildingAcrossCategories(-1);
					return true;
				}
				if (keyCode == KeyCode.DownArrow) {
					NavigateBuildingAcrossCategories(1);
					return true;
				}
				if (keyCode == KeyCode.Home) {
					JumpToBuilding(0);
					return true;
				}
				if (keyCode == KeyCode.End) {
					var cat = _categories[_indices[0]];
					JumpToBuilding(cat.Buildings.Count - 1);
					return true;
				}
			}

			if (keyCode == KeyCode.Escape) {
				SoundManager.PlayButtonClick();
				Close();
				return true;
			}

			return null;
		}

		protected override EscapeAction OnEscape() {
			// Handled in HandleSpecialKey
			return EscapeAction.PassThrough;
		}

		protected override void AnnounceCurrentItem() {
			if (Level == 0) {
				AnnounceCurrentCategory();
				return;
			}
			if (Level == 1) {
				AnnounceCurrentBuilding();
				return;
			}
		}

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0) return EmptyMessage;
			var category = _categories[0];
			return $"Building Menu. {category.Name}: {category.Buildings.Count}";
		}

		protected override void OnOpened() {
			SoundManager.PlayPopupShow();
		}

		protected override void OnClosed() {
			_categories.Clear();
			if (!_closingForBuild) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say("Building menu closed");
			}
			_closingForBuild = false;
		}

		// ========================================
		// SEARCH (cross-category via ISearchable)
		// ========================================

		protected override int SearchItemCount => _allBuildings.Count;

		protected override int SearchCurrentIndex {
			get {
				for (int i = 0; i < _allBuildings.Count; i++) {
					if (_allBuildings[i].categoryIndex == _indices[0] &&
						_allBuildings[i].buildingIndex == CurrentIndex)
						return i;
				}
				return 0;
			}
		}

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _allBuildings.Count) return null;
			return _allBuildings[index].name;
		}

		protected override void SearchMoveTo(int index) {
			if (index < 0 || index >= _allBuildings.Count) return;
			_indices[0] = _allBuildings[index].categoryIndex;
			SetLevel(1);
			CurrentIndex = _allBuildings[index].buildingIndex;
			AnnounceCurrentBuilding();
		}

		// ========================================
		// CROSS-CATEGORY NAVIGATION
		// ========================================

		/// <summary>
		/// Navigate buildings, flowing into the next/previous category at boundaries.
		/// Announces category name when crossing into a new category.
		/// </summary>
		private void NavigateBuildingAcrossCategories(int direction) {
			var category = _categories[_indices[0]];
			if (category.Buildings.Count == 0) return;

			int newIndex = CurrentIndex + direction;

			if (newIndex >= category.Buildings.Count) {
				// Past end of category - move to next category's first building
				_indices[0] = (_indices[0] + 1) % _categories.Count;
				CurrentIndex = 0;
				AnnounceCategoryAndBuilding();
			} else if (newIndex < 0) {
				// Before start of category - move to previous category's last building
				_indices[0] = (_indices[0] - 1 + _categories.Count) % _categories.Count;
				CurrentIndex = _categories[_indices[0]].Buildings.Count - 1;
				AnnounceCategoryAndBuilding();
			} else {
				CurrentIndex = newIndex;
				AnnounceCurrentBuilding();
			}
		}

		/// <summary>
		/// Jump to a specific building index (Home/End).
		/// </summary>
		private void JumpToBuilding(int index) {
			var category = _categories[_indices[0]];
			if (category.Buildings.Count == 0) return;
			CurrentIndex = Mathf.Clamp(index, 0, category.Buildings.Count - 1);
			AnnounceCurrentBuilding();
		}

		// ========================================
		// ACTIONS
		// ========================================

		/// <summary>
		/// Select the current building and enter build mode.
		/// </summary>
		private void SelectBuilding() {
			var category = _categories[_indices[0]];
			if (CurrentIndex < 0 || CurrentIndex >= category.Buildings.Count) return;

			var building = category.Buildings[CurrentIndex];

			// Check if building can still be constructed
			if (!GameReflection.CanConstructBuilding(building.Model)) {
				Speech.Say($"{building.Name} cannot be built, at maximum");
				return;
			}

			// Close menu and enter build mode
			SoundManager.PlayButtonClick();
			_closingForBuild = true;
			Close();

			if (_buildModeController != null) {
				_buildModeController.EnterBuildMode(building.Model, building.Name);
			} else {
				Debug.LogError("[ATSAccessibility] BuildModeController not set");
				Speech.Say("Build mode unavailable");
			}
		}

		// ========================================
		// DATA
		// ========================================

		/// <summary>
		/// Refresh the category list with current game data.
		/// Groups unlocked buildings by their category, sorted by category order.
		/// </summary>
		private void RefreshCategories() {
			_categories.Clear();

			// Get all building categories
			var allCategories = GameReflection.GetBuildingCategories();
			if (allCategories == null) {
				Debug.LogWarning("[ATSAccessibility] Could not get BuildingCategories from Settings");
				return;
			}

			// Build category lookup
			var categoryDict = new Dictionary<object, Category>();
			foreach (var catModel in allCategories) {
				if (!GameReflection.IsCategoryOnHUD(catModel)) continue;

				var name = GameReflection.GetDisplayName(catModel) ?? "Unknown";
				var order = GameReflection.GetModelOrder(catModel);

				categoryDict[catModel] = new Category {
					Name = name,
					Order = order,
					Model = catModel,
					Buildings = new List<BuildingItem>()
				};
			}

			// Get all building models
			var allBuildings = GameReflection.GetAllBuildingModels();
			if (allBuildings == null) {
				Debug.LogWarning("[ATSAccessibility] Could not get BuildingModels from Settings");
				return;
			}

			// Filter and group buildings
			int unlockedCount = 0;
			foreach (var buildingModel in allBuildings) {
				// Skip inactive, not in shop, or locked buildings
				if (!GameReflection.IsBuildingActive(buildingModel)) continue;
				if (!GameReflection.IsBuildingInShop(buildingModel)) continue;
				if (!GameReflection.IsBuildingUnlocked(buildingModel)) continue;

				var category = GameReflection.GetBuildingCategory(buildingModel);
				if (category == null || !categoryDict.ContainsKey(category)) continue;

				var name = GameReflection.GetDisplayName(buildingModel) ?? GameReflection.GetModelName(buildingModel) ?? "Unknown";
				var order = GameReflection.GetModelOrder(buildingModel);

				categoryDict[category].Buildings.Add(new BuildingItem {
					Name = name,
					Order = order,
					Model = buildingModel
				});
				unlockedCount++;
			}

			// Filter to categories with buildings and sort
			_categories = categoryDict.Values
				.Where(c => c.Buildings.Count > 0)
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

			// Build flat list of all buildings for cross-category search
			_allBuildings.Clear();
			for (int ci = 0; ci < _categories.Count; ci++) {
				var cat = _categories[ci];
				for (int bi = 0; bi < cat.Buildings.Count; bi++) {
					_allBuildings.Add((ci, bi, cat.Buildings[bi].Name));
				}
			}

			Debug.Log($"[ATSAccessibility] Building menu refreshed: {_categories.Count} categories, {unlockedCount} buildings");
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		/// <summary>
		/// Announce the current category (left panel).
		/// </summary>
		private void AnnounceCurrentCategory() {
			if (_indices[0] < 0 || _indices[0] >= _categories.Count) return;

			var category = _categories[_indices[0]];
			int buildingCount = category.Buildings.Count;

			Speech.Say($"{category.Name}: {buildingCount}");
			Debug.Log($"[ATSAccessibility] Category {_indices[0] + 1}/{_categories.Count}: {category.Name}, {buildingCount} buildings");
		}

		/// <summary>
		/// Announce the current building (right panel).
		/// </summary>
		private void AnnounceCurrentBuilding() {
			var category = _categories[_indices[0]];
			if (CurrentIndex < 0 || CurrentIndex >= category.Buildings.Count) return;

			var building = category.Buildings[CurrentIndex];

			// Get building size
			var size = GameReflection.GetBuildingSize(building.Model);
			string sizeText = $"{size.x}x{size.y}";

			// Get building costs (includes "not enough" annotations for insufficient goods)
			string costs = GameReflection.GetBuildingCosts(building.Model);
			string costsText = !string.IsNullOrEmpty(costs) ? $" {costs}." : "";

			// Get building description
			string description = GameReflection.GetBuildingDescription(building.Model) ?? "";

			// Check if can be constructed
			bool canConstruct = GameReflection.CanConstructBuilding(building.Model);
			string status = canConstruct ? "" : ", at maximum";

			// Format: "Name, size. 5 Planks, not enough, 3 Bricks. Description"
			string announcement = $"{building.Name}{status}, {sizeText}.{costsText} {description}";
			Speech.Say(announcement);
			Debug.Log($"[ATSAccessibility] Building: {building.Name}{status}, {sizeText}");
		}

		/// <summary>
		/// Announce category name followed by current building when crossing category boundaries.
		/// </summary>
		private void AnnounceCategoryAndBuilding() {
			if (_indices[0] < 0 || _indices[0] >= _categories.Count) return;

			var category = _categories[_indices[0]];
			if (CurrentIndex < 0 || CurrentIndex >= category.Buildings.Count) return;

			var building = category.Buildings[CurrentIndex];
			var size = GameReflection.GetBuildingSize(building.Model);
			string sizeText = $"{size.x}x{size.y}";
			string costs = GameReflection.GetBuildingCosts(building.Model);
			string costsText = !string.IsNullOrEmpty(costs) ? $" {costs}." : "";
			string description = GameReflection.GetBuildingDescription(building.Model) ?? "";
			bool canConstruct = GameReflection.CanConstructBuilding(building.Model);
			string status = canConstruct ? "" : ", at maximum";

			string announcement = $"{category.Name}. {building.Name}{status}, {sizeText}.{costsText} {description}";
			Speech.Say(announcement);
			Debug.Log($"[ATSAccessibility] Building (cross-category): {category.Name} > {building.Name}{status}, {sizeText}");
		}
	}
}
