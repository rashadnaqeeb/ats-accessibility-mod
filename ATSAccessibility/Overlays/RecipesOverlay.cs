using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the Recipes popup (F2 Menu Hub -> Recipes).
	/// Provides keyboard navigation of recipes organized by produced good,
	/// with controls for global production limits and recipe toggling.
	///
	/// Level 0 = goods, Level 1 = recipes for the selected good.
	/// </summary>
	public class RecipesOverlay: MenuBase {
		// Data
		private List<RecipesReflection.GoodInfo> _goods;
		private bool _showAllGoods;  // false = unlocked buildings only, true = include locked buildings
		private bool _ingredientMode;  // false = producers (default), true = ingredients

		// Level 2: related goods for chain navigation
		private struct RelatedGoodItem {
			public string InternalName;
			public string DisplayName;
			public int Amount;
		}
		private List<RelatedGoodItem> _relatedGoods;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Recipes";
		protected override string EmptyMessage => "No goods available";

		protected override int GetItemCount() {
			switch (Level) {
				case 0: return _goods?.Count ?? 0;
				case 1: return GetCurrentGood()?.Recipes.Count ?? 0;
				case 2: return _relatedGoods?.Count ?? 0;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0:
					if (_goods == null || index < 0 || index >= _goods.Count) return null;
					var good = _goods[index];
					var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
					return $"{good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}";

				case 1:
					var parentGood = GetCurrentGood();
					if (parentGood == null || index < 0 || index >= parentGood.Recipes.Count) return null;
					var recipe = parentGood.Recipes[index];

					string workshopPart = recipe.IsBuilt && recipe.WorkshopIndex > 0
						? $"{recipe.WorkshopName} #{recipe.WorkshopIndex}"
						: recipe.WorkshopName;

					int gradeLevel = RecipesReflection.GetRecipeGradeLevel(recipe.RecipeModel);
					string stars = gradeLevel == 1 ? ", 1 star" : $", {gradeLevel} stars";

					string status = recipe.IsBuilt
						? (recipe.IsActive ? "active" : "inactive")
						: "not built";

					if (_ingredientMode) {
						string outputName = RecipesReflection.GetRecipeOutputName(recipe.RecipeModel);
						int outputAmount = RecipesReflection.GetRecipeOutputAmount(recipe.RecipeModel);
						return $"{workshopPart}, produces {outputName} x {outputAmount}{stars}, {status}";
					}

					return $"{workshopPart}{stars}, {status}";

				case 2:
					if (_relatedGoods == null || index < 0 || index >= _relatedGoods.Count) return null;
					var related = _relatedGoods[index];
					return $"{related.DisplayName} x {related.Amount}";

				default: return null;
			}
		}

		protected override void RefreshData() {
			_goods = _ingredientMode
				? RecipesReflection.GetAllGoodsAsIngredients(_showAllGoods)
				: RecipesReflection.GetAllGoods(_showAllGoods);
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					var good = GetCurrentGood();
					if (good == null || good.Recipes.Count == 0) {
						Speech.Say("No recipes for this good");
						return EnterAction.None;
					}
					return EnterAction.DrillDown;

				case 1:
					return EnterAction.Action;

				case 2:
					JumpToGood(index);
					return EnterAction.None;

				default:
					return EnterAction.None;
			}
		}

		protected override void OnAction(int index) {
			if (Level == 1)
				AnnounceRecipeFull();
		}

		protected override void OnSpace(int index) {
			if (Level == 1)
				ToggleCurrentRecipe();
		}

		protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) {
			if (Level == 0)
				AdjustLimit(dir * (modifiers.Shift ? 10 : 1));
		}

		protected override bool CanDrillDown(int index) {
			if (Level == 0) return base.CanDrillDown(index);
			// Allow Right arrow at Level 1 to drill to related goods
			if (Level == 1) return true;
			return false;
		}

		protected override void OnDrillDown(int index) {
			if (Level == 1)
				PopulateRelatedGoods(index);
		}

		// ========================================
		// SPECIAL KEYS
		// ========================================

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (modifiers.Control && keyCode == KeyCode.T) {
				ToggleShowAll();
				return true;
			}

			if (keyCode == KeyCode.Tab) {
				ToggleIngredientMode();
				return true;
			}

			// Right arrow at Level 2 = jump (same as Enter)
			if (keyCode == KeyCode.RightArrow && Level == 2 && _relatedGoods != null && _relatedGoods.Count > 0) {
				JumpToGood(CurrentIndex);
				return true;
			}

			// Plus key (non-keypad) handled here because base only maps Equals for +
			if (keyCode == KeyCode.Plus && Level == 0) {
				AdjustLimit(modifiers.Shift ? 10 : 1);
				return true;
			}

			return null;
		}

		private static readonly List<HelpEntry> _recipesHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			new HelpEntry("Tab", "Toggle producers/ingredients mode"),
			new HelpEntry("Ctrl+T", "Toggle show all"),
		};
		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _recipesHelpEntries;

		// ========================================
		// SEARCH
		// ========================================

		protected override int SearchItemCount =>
			Level == 0 ? (_goods?.Count ?? 0) : 0;

		protected override string GetSearchName(int index) {
			if (Level == 0 && _goods != null && index >= 0 && index < _goods.Count)
				return _goods[index].DisplayName;
			return null;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		protected override string GetOpenAnnouncement() {
			var viewLabel = _ingredientMode ? "Ingredients mode" : "Producers mode";
			var filterLabel = _showAllGoods ? "showing all" : "showing available";

			if (_goods == null || _goods.Count == 0)
				return $"{viewLabel}, {filterLabel}. {EmptyMessage}";

			var good = _goods[0];
			var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
			return $"{viewLabel}, {filterLabel}. {good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}";
		}

		protected override void OnClosed() {
			_goods?.Clear();
			_goods = null;
			_relatedGoods?.Clear();
			_relatedGoods = null;
		}

		// ========================================
		// LIMIT CONTROL
		// ========================================

		private void AdjustLimit(int delta) {
			var currentGood = GetCurrentGood();
			if (currentGood == null) return;

			int newLimit = Math.Max(0, currentGood.Limit + delta);
			RecipesReflection.SetGlobalLimit(currentGood.Name, newLimit);
			currentGood.Limit = newLimit;

			// Push to all built workshops' recipe states that follow the global limit
			foreach (var recipe in currentGood.Recipes) {
				if (recipe.IsBuilt && recipe.RecipeState != null &&
					!BuildingReflection.IsRecipeLimitLocal(recipe.RecipeState)) {
					BuildingReflection.SetRecipeLimitFromGlobal(recipe.RecipeState, newLimit);
				}
			}

			if (newLimit == 0) {
				Speech.Say("No limit");
			} else {
				Speech.Say($"Limit {newLimit}");
			}

			SoundManager.PlayButtonClick();
		}

		// ========================================
		// RECIPE TOGGLING
		// ========================================

		private void ToggleCurrentRecipe() {
			var recipe = GetCurrentRecipe();
			if (recipe == null) return;

			if (!recipe.IsBuilt) {
				Speech.Say("Cannot toggle, workshop not built");
				SoundManager.PlayFailed();
				return;
			}

			bool newState = RecipesReflection.ToggleRecipe(recipe);

			if (newState) {
				Speech.Say("Active");
				SoundManager.PlayRecipeOn();
			} else {
				Speech.Say("Inactive");
				SoundManager.PlayRecipeOff();
			}
		}

		// ========================================
		// SHOW ALL TOGGLE
		// ========================================

		private void ToggleShowAll() {
			_showAllGoods = !_showAllGoods;
			RefreshData();

			// Reset navigation
			SetLevel(0);
			_indices[0] = 0;
			_indices[1] = 0;
			_indices[2] = 0;
			_relatedGoods?.Clear();
			_relatedGoods = null;

			var modeLabel = _showAllGoods ? "Showing all recipes" : "Showing available recipes";

			if (_goods != null && _goods.Count > 0) {
				var good = _goods[0];
				var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
				Speech.Say($"{modeLabel}. {good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}");
			} else {
				Speech.Say($"{modeLabel}. No goods available");
			}
		}

		// ========================================
		// DATA ACCESS
		// ========================================

		private RecipesReflection.GoodInfo GetCurrentGood() {
			if (_goods == null || _indices[0] < 0 || _indices[0] >= _goods.Count)
				return null;
			return _goods[_indices[0]];
		}

		private RecipesReflection.RecipeInfo GetCurrentRecipe() {
			var good = GetCurrentGood();
			if (good == null || CurrentIndex < 0 || CurrentIndex >= good.Recipes.Count)
				return null;
			return good.Recipes[CurrentIndex];
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		/// <summary>
		/// Announce the current recipe in full encyclopedia format.
		/// Format: "{OutputName} x {Amount}: {Inputs} {Time}{Stars}"
		/// </summary>
		private void AnnounceRecipeFull() {
			var recipe = GetCurrentRecipe();
			if (recipe == null) {
				Speech.Say("No recipe selected");
				return;
			}

			var good = GetCurrentGood();
			string outputName = _ingredientMode
				? RecipesReflection.GetRecipeOutputName(recipe.RecipeModel)
				: (good?.DisplayName ?? RecipesReflection.GetRecipeOutputName(recipe.RecipeModel));
			int outputAmount = RecipesReflection.GetRecipeOutputAmount(recipe.RecipeModel);
			float productionTime = RecipesReflection.GetRecipeProductionTime(recipe.RecipeModel);
			int gradeLevel = RecipesReflection.GetRecipeGradeLevel(recipe.RecipeModel);
			var requiredGoods = RecipesReflection.GetRecipeRequiredGoods(recipe.RecipeModel);

			string inputs = RecipeFormatter.FormatIngredients(requiredGoods,
				RecipesReflection.GetGoodsSetGoods, RecipesReflection.GetGoodRefDisplayName, RecipesReflection.GetGoodRefAmount);
			string time = RecipeFormatter.FormatTime(productionTime);
			string stars = gradeLevel == 1 ? " 1 star." : $" {gradeLevel} stars.";

			Speech.Say($"{outputName} x {outputAmount}: {inputs} {time}{stars}");
		}

		// ========================================
		// INGREDIENT MODE
		// ========================================

		private void ToggleIngredientMode() {
			_ingredientMode = !_ingredientMode;
			RefreshData();

			SetLevel(0);
			_indices[0] = 0;
			_indices[1] = 0;
			_indices[2] = 0;
			_relatedGoods?.Clear();
			_relatedGoods = null;

			var viewLabel = _ingredientMode ? "Ingredients mode" : "Producers mode";

			if (_goods != null && _goods.Count > 0) {
				var good = _goods[0];
				var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
				Speech.Say($"{viewLabel}. {good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}");
			} else {
				Speech.Say($"{viewLabel}. No goods available");
			}
		}

		// ========================================
		// CHAIN NAVIGATION
		// ========================================

		private void PopulateRelatedGoods(int recipeIndex) {
			_relatedGoods = new List<RelatedGoodItem>();

			var good = GetCurrentGood();
			if (good == null || recipeIndex < 0 || recipeIndex >= good.Recipes.Count) return;

			var recipe = good.Recipes[recipeIndex];
			var seen = new HashSet<string>();

			if (_ingredientMode) {
				// In ingredient mode, show the output product
				var outputName = RecipesReflection.GetRecipeOutputInternalName(recipe.RecipeModel);
				var outputDisplayName = RecipesReflection.GetRecipeOutputName(recipe.RecipeModel);
				int outputAmount = RecipesReflection.GetRecipeOutputAmount(recipe.RecipeModel);
				if (!string.IsNullOrEmpty(outputName) && seen.Add(outputName)) {
					_relatedGoods.Add(new RelatedGoodItem {
						InternalName = outputName,
						DisplayName = outputDisplayName,
						Amount = outputAmount
					});
				}
			} else {
				// In producers mode, show the input ingredients (flattened, deduplicated)
				var requiredGoods = RecipesReflection.GetRecipeRequiredGoods(recipe.RecipeModel);
				if (requiredGoods != null) {
					foreach (var goodsSet in requiredGoods) {
						if (goodsSet == null) continue;
						var goods = RecipesReflection.GetGoodsSetGoods(goodsSet);
						if (goods == null) continue;
						foreach (var goodRef in goods) {
							if (goodRef == null) continue;
							var internalName = RecipesReflection.GetGoodRefInternalName(goodRef);
							if (string.IsNullOrEmpty(internalName) || !seen.Add(internalName)) continue;
							_relatedGoods.Add(new RelatedGoodItem {
								InternalName = internalName,
								DisplayName = RecipesReflection.GetGoodRefDisplayName(goodRef),
								Amount = RecipesReflection.GetGoodRefAmount(goodRef)
							});
						}
					}
				}
			}
		}

		private void JumpToGood(int index) {
			if (_relatedGoods == null || index < 0 || index >= _relatedGoods.Count) return;

			var target = _relatedGoods[index];

			// Find the target good in the Level 0 list
			int targetIndex = -1;
			if (_goods != null) {
				for (int i = 0; i < _goods.Count; i++) {
					if (_goods[i].Name == target.InternalName) {
						targetIndex = i;
						break;
					}
				}
			}

			if (targetIndex < 0) {
				Speech.Say($"{target.DisplayName}, end of chain, no recipes available");
				SoundManager.PlayFailed();
				return;
			}

			_relatedGoods?.Clear();
			_relatedGoods = null;
			SetLevel(0);
			_indices[0] = targetIndex;
			AnnounceCurrentItem();
		}
	}
}
