using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for FishingHut buildings.
	/// Top section shows Status (Active/Paused) with Enter/Space to toggle.
	/// Followed by Bait, Recipes, Workers, and Upgrades sections.
	/// </summary>
	public class FishingHutNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Status,   // Active/Paused toggle at top
			Bait,     // Bait mode settings
			Recipes,  // Fish types to catch
			Workers,
			Upgrades
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;
		// Bait data
		private int _baitMode;
		private int _baitCharges;
		private string _baitIngredient;
		private string[] _baitModeNames;

		// Recipe data
		private List<RecipeInfo> _recipes = new List<RecipeInfo>();

		// ========================================
		// RECIPE INFO STRUCT
		// ========================================

		private struct RecipeInfo {
			public object RecipeState;
			public string ModelName;
			public string ProductName;
			public bool IsActive;
		}

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		protected override string NavigatorName => "FishingHutNavigator";

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Status:
					return 0;  // No items, just section-level toggle
				case SectionType.Bait:
					return 3;  // Mode, Charges, Ingredient
				case SectionType.Recipes:
					return _recipes.Count;
				case SectionType.Workers:
					return _workersSection.GetItemCount();
				case SectionType.Upgrades:
					return _upgradesSection.GetItemCount();
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			// Bait mode has sub-items for mode selection
			if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0) {
				return _baitModeNames?.Length ?? 0;
			}

			// Workers have sub-items (races to assign, plus unassign if occupied)
			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return _workersSection.GetSubItemCount(itemIndex);
			}

			// Upgrades have sub-items (perks)
			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.GetSubItemCount(itemIndex);
			}

			return 0;
		}

		protected override void AnnounceSection(int sectionIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Status) {
				string status = _isSleeping ? Strings.Get("common.paused") : Strings.Get("common.active");
				Speech.Say(Strings.Get("nav.common.status_line", status));
				return;
			}

			string sectionName = _sectionNames[sectionIndex];
			Speech.Say(sectionName);
		}

		protected override void AnnounceItem(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Bait:
					AnnounceBaitItem(itemIndex);
					break;
				case SectionType.Recipes:
					AnnounceRecipeItem(itemIndex);
					break;
				case SectionType.Workers:
					_workersSection.AnnounceItem(itemIndex);
					break;
				case SectionType.Upgrades:
					_upgradesSection.AnnounceItem(itemIndex);
					break;
			}
		}

		protected override bool PerformSectionAction(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return false;

			if (_sectionTypes[sectionIndex] == SectionType.Status)
				return ToggleBuildingSleep();

			return false;
		}

		protected override bool PerformItemAction(int sectionIndex, int itemIndex) {
			// Recipes toggle directly on Enter (like Camp buildings)
			if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count) {
				ToggleRecipe(itemIndex);
				return true;
			}

			return false;
		}

		protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Workers)
				return Strings.Get("common.no_free_workers");
			return null;
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0) {
				AnnounceBaitModeSubItem(subItemIndex);
			} else if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				_workersSection.AnnounceSubItem(itemIndex, subItemIndex);
			} else if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				_upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
			}
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0) {
				return PerformBaitModeSubItemAction(subItemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return PerformWorkerSubItemAction(itemIndex, subItemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
			}

			return false;
		}

		protected override void RefreshData() {
			// Cache status info
			_isSleeping = BuildingReflection.IsBuildingSleeping(_building);
			_canSleep = BuildingReflection.CanBuildingSleep(_building);

			// Cache bait data
			_baitMode = BuildingReflection.GetFishingBaitMode(_building);
			_baitCharges = BuildingReflection.GetFishingBaitCharges(_building);
			_baitIngredient = BuildingReflection.GetFishingBaitIngredient(_building);
			_baitModeNames = BuildingReflection.GetFishingBaitModeNames();

			// Cache recipe data
			RefreshRecipes();

			// Build sections list
			var sectionNames = new List<string>();
			var sectionTypes = new List<SectionType>();

			// Always have Status section at top (announced dynamically)
			sectionNames.Add(Strings.Get("common.status"));
			sectionTypes.Add(SectionType.Status);

			// Always have Bait section for FishingHut
			sectionNames.Add(Strings.Get("nav.fishinghut.section.bait"));
			sectionTypes.Add(SectionType.Bait);

			// Add Recipes if available
			if (_recipes.Count > 0) {
				sectionNames.Add(Strings.Get("common.recipes"));
				sectionTypes.Add(SectionType.Recipes);
			}

			// Add Workers if building currently accepts worker assignment
			if (TryInitializeWorkersSection()) {
				sectionNames.Add(Strings.Get("common.workers"));
				sectionTypes.Add(SectionType.Workers);
			}

			// Add Upgrades section if available
			if (TryInitializeUpgradesSection()) {
				sectionNames.Add(Strings.Get("common.upgrades"));
				sectionTypes.Add(SectionType.Upgrades);
			}

			_sectionNames = sectionNames.ToArray();
			_sectionTypes = sectionTypes.ToArray();

			Debug.Log($"[ATSAccessibility] FishingHutNavigator: Refreshed data, {_recipes.Count} recipes");
		}

		protected override void ClearData() {
			_sectionNames = null;
			_sectionTypes = null;
			_recipes.Clear();
			ClearWorkersSection();
			_baitMode = 0;
			_baitCharges = 0;
			_baitIngredient = null;
			_baitModeNames = null;
			ClearUpgradesSection();
		}

		// ========================================
		// BAIT SECTION
		// ========================================

		private void RefreshBaitData() {
			_baitMode = BuildingReflection.GetFishingBaitMode(_building);
			_baitCharges = BuildingReflection.GetFishingBaitCharges(_building);
		}

		private void AnnounceBaitItem(int itemIndex) {
			// Refresh bait data to get current values
			RefreshBaitData();

			switch (itemIndex) {
				case 0:
					// Bait mode
					string modeName = _baitModeNames != null && _baitMode < _baitModeNames.Length
						? _baitModeNames[_baitMode]
						: Strings.Get("nav.fishinghut.mode_default", _baitMode);
					Speech.Say(Strings.Get("nav.fishinghut.bait_mode", modeName));
					break;

				case 1:
					// Bait charges
					Speech.Say(Strings.Get("nav.fishinghut.bait_charges", _baitCharges));
					break;

				case 2:
					// Bait ingredient
					string ingredient;
					if (!string.IsNullOrEmpty(_baitIngredient)) {
						string localized = BuildingReflection.GetGoodDisplayName(_baitIngredient);
						ingredient = (!string.IsNullOrEmpty(localized) && localized != _baitIngredient)
							? localized
							: CleanupName(_baitIngredient);
					} else {
						ingredient = Strings.Get("common.unknown");
					}
					Speech.Say(Strings.Get("nav.fishinghut.bait_ingredient", ingredient));
					break;

				default:
					Speech.Say(Strings.Get("nav.fishinghut.unknown_bait_info"));
					break;
			}
		}

		private void AnnounceBaitModeSubItem(int subItemIndex) {
			if (_baitModeNames == null || subItemIndex >= _baitModeNames.Length) {
				Speech.Say(Strings.Get("nav.common.invalid_mode"));
				return;
			}

			string modeName = _baitModeNames[subItemIndex];
			bool isSelected = subItemIndex == _baitMode;

			if (isSelected)
				Speech.Say(Strings.Get("nav.common.mode_selected_suffix", modeName));
			else
				Speech.Say(modeName);
		}

		private bool PerformBaitModeSubItemAction(int subItemIndex) {
			if (_baitModeNames == null || subItemIndex >= _baitModeNames.Length)
				return false;

			if (subItemIndex == _baitMode) {
				Speech.Say(Strings.Get("nav.common.already_selected"));
				return false;
			}

			if (BuildingReflection.SetFishingBaitMode(_building, subItemIndex)) {
				_baitMode = subItemIndex;
				string modeName = _baitModeNames[subItemIndex];
				Speech.Say(Strings.Get("nav.common.mode_was_selected", modeName));

				// Exit submenu back to item level
				_navigationLevel = 1;
				return true;
			}

			Speech.Say(Strings.Get("nav.common.cannot_change_mode"));
			return false;
		}

		// ========================================
		// RECIPES SECTION
		// ========================================

		private void RefreshRecipes() {
			_recipes.Clear();

			var recipeStates = BuildingReflection.GetFishingHutRecipes(_building);
			foreach (var recipeState in recipeStates) {
				var info = new RecipeInfo {
					RecipeState = recipeState,
					ModelName = BuildingReflection.GetRecipeModelName(recipeState) ?? Strings.Get("common.unknown"),
					ProductName = BuildingReflection.GetRecipeProductName(recipeState),
					IsActive = BuildingReflection.IsRecipeActive(recipeState)
				};
				_recipes.Add(info);
			}
		}

		private void AnnounceRecipeItem(int itemIndex) {
			if (itemIndex >= _recipes.Count) {
				Speech.Say(Strings.Get("nav.common.invalid_recipe"));
				return;
			}

			var recipe = _recipes[itemIndex];
			string displayName = GetRecipeDisplayName(recipe);
			string status = recipe.IsActive ? Strings.Get("common.enabled_lower") : Strings.Get("common.disabled_lower");

			Speech.Say(Strings.Get("nav.common.recipe_status", displayName, status));
		}

		private string GetRecipeDisplayName(RecipeInfo recipe) {
			if (!string.IsNullOrEmpty(recipe.ProductName)) {
				string localized = BuildingReflection.GetGoodDisplayName(recipe.ProductName);
				if (!string.IsNullOrEmpty(localized) && localized != recipe.ProductName)
					return localized;
				return CleanupName(recipe.ProductName);
			}

			if (!string.IsNullOrEmpty(recipe.ModelName)) {
				return CleanupName(recipe.ModelName);
			}

			return Strings.Get("nav.common.unknown_recipe");
		}

		private void ToggleRecipe(int itemIndex) {
			if (itemIndex >= _recipes.Count) return;

			var recipe = _recipes[itemIndex];

			if (BuildingReflection.ToggleFishingHutRecipe(_building, recipe.RecipeState)) {
				// Refresh to get new active state
				bool newActive = BuildingReflection.IsRecipeActive(recipe.RecipeState);

				// Update cached value
				var updatedRecipe = recipe;
				updatedRecipe.IsActive = newActive;
				_recipes[itemIndex] = updatedRecipe;

				// Play appropriate sound
				if (newActive)
					SoundManager.PlayRecipeOn();
				else
					SoundManager.PlayRecipeOff();

				string displayName = GetRecipeDisplayName(recipe);
				Speech.Say(Strings.Get("nav.common.recipe_status", displayName, newActive ? Strings.Get("common.enabled_lower") : Strings.Get("common.disabled_lower")));
			} else {
				Speech.Say(Strings.Get("nav.common.cannot_toggle_recipe"));
			}
		}

		private string CleanupName(string name) => FormattingUtils.CleanupRecipeName(name);

	}
}
