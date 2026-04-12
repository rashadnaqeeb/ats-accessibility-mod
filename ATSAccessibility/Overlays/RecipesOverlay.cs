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

		protected override string OverlayName => Strings.Get("common.recipes");
		protected override string EmptyMessage => Strings.Get("overlay.recipes.empty");

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
					var limitInfo = good.Limit > 0 ? Strings.Get("overlay.recipes.limit.value", good.Limit) : Strings.Get("overlay.recipes.limit.none");
					return Strings.Get("overlay.recipes.good", good.DisplayName, good.StorageAmount, limitInfo);

				case 1:
					var parentGood = GetCurrentGood();
					if (parentGood == null || index < 0 || index >= parentGood.Recipes.Count) return null;
					var recipe = parentGood.Recipes[index];

					string workshopPart = recipe.IsBuilt && recipe.WorkshopIndex > 0
						? Strings.Get("overlay.recipes.workshop.built", recipe.WorkshopName, recipe.WorkshopIndex)
						: recipe.WorkshopName;

					int gradeLevel = RecipesReflection.GetRecipeGradeLevel(recipe.RecipeModel);
					string stars = gradeLevel == 1 ? Strings.Get("overlay.recipes.stars.one") : Strings.Get("overlay.recipes.stars.many", gradeLevel);

					string status = recipe.IsBuilt
						? Strings.Get(recipe.IsActive ? "common.active_lower" : "overlay.recipes.status.inactive")
						: Strings.Get("overlay.recipes.status.not_built");

					if (_ingredientMode) {
						string outputName = RecipesReflection.GetRecipeOutputName(recipe.RecipeModel);
						int outputAmount = RecipesReflection.GetRecipeOutputAmount(recipe.RecipeModel);
						return Strings.Get("overlay.recipes.recipe.ingredient", workshopPart, outputName, outputAmount, stars, status);
					}

					return Strings.Get("overlay.recipes.recipe", workshopPart, stars, status);

				case 2:
					if (_relatedGoods == null || index < 0 || index >= _relatedGoods.Count) return null;
					var related = _relatedGoods[index];
					return Strings.Get("overlay.recipes.related", related.DisplayName, related.Amount);

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
						Speech.Say(Strings.Get("overlay.recipes.no_recipes"));
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
			HelpEntry.Loca("Tab", "overlay.recipes.help.toggle_mode"),
			HelpEntry.Loca("Ctrl+T", "overlay.recipes.help.toggle_show_all"),
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
			var viewLabel = Strings.Get(_ingredientMode ? "overlay.recipes.ingredient_mode" : "overlay.recipes.producer_mode");
			var filterLabel = Strings.Get(_showAllGoods ? "overlay.recipes.filter.all" : "overlay.recipes.filter.available");

			if (_goods == null || _goods.Count == 0)
				return $"{viewLabel}, {filterLabel}. {EmptyMessage}";

			var good = _goods[0];
			var limitInfo = good.Limit > 0 ? Strings.Get("overlay.recipes.limit.value", good.Limit) : Strings.Get("overlay.recipes.limit.none");
			string goodAnnouncement = Strings.Get("overlay.recipes.good", good.DisplayName, good.StorageAmount, limitInfo);
			return Strings.Get("overlay.recipes.open", viewLabel, filterLabel, goodAnnouncement);
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
				Speech.Say(Strings.Get("overlay.recipes.limit_none"));
			} else {
				Speech.Say(Strings.Get("overlay.recipes.limit_set", newLimit));
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
				Speech.Say(Strings.Get("overlay.recipes.cannot_toggle"));
				SoundManager.PlayFailed();
				return;
			}

			bool newState = RecipesReflection.ToggleRecipe(recipe);

			if (newState) {
				Speech.Say(Strings.Get("common.active"));
				SoundManager.PlayRecipeOn();
			} else {
				Speech.Say(Strings.Get("common.inactive"));
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

			var modeLabel = Strings.Get(_showAllGoods ? "overlay.recipes.mode.show_all" : "overlay.recipes.mode.show_available");

			if (_goods != null && _goods.Count > 0) {
				var good = _goods[0];
				var limitInfo = good.Limit > 0 ? Strings.Get("overlay.recipes.limit.value", good.Limit) : Strings.Get("overlay.recipes.limit.none");
				Speech.Say(Strings.Get("overlay.recipes.mode.announce", modeLabel, good.DisplayName, good.StorageAmount, limitInfo));
			} else {
				Speech.Say(Strings.Get("overlay.recipes.mode.empty", modeLabel));
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
				Speech.Say(Strings.Get("overlay.recipes.no_recipe_selected"));
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
			string stars = gradeLevel == 1 ? Strings.Get("overlay.recipes.full.stars.one") : Strings.Get("overlay.recipes.full.stars.many", gradeLevel);

			Speech.Say(Strings.Get("overlay.recipes.full", outputName, outputAmount, inputs, time, stars));
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

			var viewLabel = Strings.Get(_ingredientMode ? "overlay.recipes.ingredient_mode" : "overlay.recipes.producer_mode");

			if (_goods != null && _goods.Count > 0) {
				var good = _goods[0];
				var limitInfo = good.Limit > 0 ? Strings.Get("overlay.recipes.limit.value", good.Limit) : Strings.Get("overlay.recipes.limit.none");
				Speech.Say(Strings.Get("overlay.recipes.mode.announce", viewLabel, good.DisplayName, good.StorageAmount, limitInfo));
			} else {
				Speech.Say(Strings.Get("overlay.recipes.mode.empty", viewLabel));
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
				Speech.Say(Strings.Get("overlay.recipes.chain.end", target.DisplayName));
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
