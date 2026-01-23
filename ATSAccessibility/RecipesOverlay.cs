using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Recipes popup (F2 Menu Hub → Recipes).
    /// Provides keyboard navigation of recipes organized by produced good,
    /// with controls for global production limits and recipe toggling.
    /// </summary>
    public class RecipesOverlay : IKeyHandler
    {
        // Navigation levels
        private const int LEVEL_GOODS = 0;
        private const int LEVEL_RECIPES = 1;

        // Navigation state
        private bool _isOpen;
        private int _navigationLevel;
        private int _goodIndex;
        private int _recipeIndex;
        private bool _showAllGoods;  // false = unlocked buildings only, true = include locked buildings

        // Data
        private List<RecipesReflection.GoodInfo> _goods;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        /// <summary>
        /// Whether this handler is currently active.
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Process a key event.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OnEnter();
                    return true;

                case KeyCode.RightArrow:
                    if (_navigationLevel == LEVEL_GOODS)
                    {
                        OnEnter();
                    }
                    return true;

                case KeyCode.LeftArrow:
                    NavigateBack();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        ClearSearch();
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    if (_navigationLevel == LEVEL_RECIPES)
                    {
                        NavigateBack();
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup (OnPopupHidden will close our overlay)
                    return false;

                case KeyCode.Space:
                    if (_navigationLevel == LEVEL_RECIPES)
                    {
                        ToggleCurrentRecipe();
                    }
                    return true;

                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals:
                    if (_navigationLevel == LEVEL_GOODS)
                    {
                        AdjustLimit(GetLimitDelta(modifiers));
                    }
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    if (_navigationLevel == LEVEL_GOODS)
                    {
                        AdjustLimit(-GetLimitDelta(modifiers));
                    }
                    return true;

                case KeyCode.T:
                    if (modifiers.Control)
                        ToggleShowAll();
                    else if (_navigationLevel == LEVEL_GOODS)
                        HandleSearchKey('t');
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                    {
                        HandleBackspace();
                    }
                    return true;

                default:
                    // Type-ahead search (A-Z) only at goods level
                    if (_navigationLevel == LEVEL_GOODS && keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _navigationLevel = LEVEL_GOODS;
            _goodIndex = 0;
            _recipeIndex = 0;
            _search.Clear();

            RefreshData();

            if (_goods == null || _goods.Count == 0)
            {
                Speech.Say("Showing available recipes. Press Control T for all recipes. No goods available");
            }
            else
            {
                var good = GetCurrentGood();
                var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
                Speech.Say($"Showing available recipes. Press Control T for all recipes. {good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}");
            }
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _search.Clear();
            ClearData();

            Debug.Log("[ATSAccessibility] RecipesOverlay closed");
        }

        /// <summary>
        /// Refresh the data from the game.
        /// </summary>
        private void RefreshData()
        {
            _goods = RecipesReflection.GetAllGoods(_showAllGoods);
        }

        /// <summary>
        /// Clear cached data.
        /// </summary>
        private void ClearData()
        {
            _goods?.Clear();
            _goods = null;
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_navigationLevel == LEVEL_GOODS)
            {
                NavigateGoods(direction);
            }
            else
            {
                NavigateRecipes(direction);
            }
        }

        private void NavigateGoods(int direction)
        {
            if (_goods == null || _goods.Count == 0) return;

            _goodIndex = NavigationUtils.WrapIndex(_goodIndex, direction, _goods.Count);
            AnnounceGood();
        }

        private void NavigateRecipes(int direction)
        {
            var currentGood = GetCurrentGood();
            if (currentGood == null || currentGood.Recipes.Count == 0) return;

            _recipeIndex = NavigationUtils.WrapIndex(_recipeIndex, direction, currentGood.Recipes.Count);
            AnnounceRecipe();
        }

        private void NavigateBack()
        {
            if (_navigationLevel == LEVEL_RECIPES)
            {
                _navigationLevel = LEVEL_GOODS;
                Speech.Say("Goods");
                AnnounceGood();
            }
            // At top level - do nothing, let popup handle its own closing
        }

        private void OnEnter()
        {
            if (_navigationLevel == LEVEL_GOODS)
            {
                // Expand to recipes
                var currentGood = GetCurrentGood();
                if (currentGood == null)
                {
                    Speech.Say("No good selected");
                    return;
                }

                if (currentGood.Recipes.Count == 0)
                {
                    Speech.Say("No recipes for this good");
                    return;
                }

                _navigationLevel = LEVEL_RECIPES;
                _recipeIndex = 0;
                Speech.Say("Recipes");
                AnnounceRecipe();
            }
            else
            {
                // Announce full recipe details
                AnnounceRecipeFull();
            }
        }

        // ========================================
        // LIMIT CONTROL
        // ========================================

        private int GetLimitDelta(KeyboardManager.KeyModifiers modifiers)
        {
            if (modifiers.Shift)
                return 10;
            return 1;
        }

        private void AdjustLimit(int delta)
        {
            var currentGood = GetCurrentGood();
            if (currentGood == null) return;

            int newLimit = Math.Max(0, currentGood.Limit + delta);
            RecipesReflection.SetGlobalLimit(currentGood.Name, newLimit);
            currentGood.Limit = newLimit;

            // Push to all built workshops' recipe states that follow the global limit
            foreach (var recipe in currentGood.Recipes)
            {
                if (recipe.IsBuilt && recipe.RecipeState != null &&
                    !BuildingReflection.IsRecipeLimitLocal(recipe.RecipeState))
                {
                    BuildingReflection.SetRecipeLimitFromGlobal(recipe.RecipeState, newLimit);
                }
            }

            if (newLimit == 0)
            {
                Speech.Say("No limit");
            }
            else
            {
                Speech.Say($"Limit {newLimit}");
            }

            SoundManager.PlayButtonClick();
        }

        // ========================================
        // RECIPE TOGGLING
        // ========================================

        private void ToggleCurrentRecipe()
        {
            var recipe = GetCurrentRecipe();
            if (recipe == null) return;

            if (!recipe.IsBuilt)
            {
                Speech.Say("Cannot toggle, workshop not built");
                SoundManager.PlayFailed();
                return;
            }

            bool newState = RecipesReflection.ToggleRecipe(recipe);

            if (newState)
            {
                Speech.Say("Active");
                SoundManager.PlayRecipeOn();
            }
            else
            {
                Speech.Say("Inactive");
                SoundManager.PlayRecipeOff();
            }
        }

        // ========================================
        // SHOW ALL TOGGLE
        // ========================================

        private void ToggleShowAll()
        {
            _showAllGoods = !_showAllGoods;
            RefreshData();

            // Reset navigation
            _goodIndex = 0;
            _recipeIndex = 0;
            _navigationLevel = LEVEL_GOODS;

            var modeLabel = _showAllGoods ? "Showing all recipes" : "Showing available recipes";

            if (_goods != null && _goods.Count > 0)
            {
                var good = GetCurrentGood();
                var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
                Speech.Say($"{modeLabel}. {good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}");
            }
            else
            {
                Speech.Say($"{modeLabel}. No goods available");
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            if (_goods == null || _goods.Count == 0) return;

            _search.AddChar(c);

            int matchIndex = _search.FindMatch(_goods, g => g.DisplayName);

            if (matchIndex >= 0)
            {
                _goodIndex = matchIndex;
                AnnounceGood();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
            }
            else
            {
                int matchIndex = _search.FindMatch(_goods, g => g.DisplayName);

                if (matchIndex >= 0)
                {
                    _goodIndex = matchIndex;
                    AnnounceGood();
                }
                else
                {
                    Speech.Say($"No match for {_search.Buffer}");
                }
            }
        }

        private void ClearSearch()
        {
            _search.Clear();
            Speech.Say("Search cleared");
        }

        // ========================================
        // DATA ACCESS
        // ========================================

        private RecipesReflection.GoodInfo GetCurrentGood()
        {
            if (_goods == null || _goodIndex < 0 || _goodIndex >= _goods.Count)
                return null;
            return _goods[_goodIndex];
        }

        private RecipesReflection.RecipeInfo GetCurrentRecipe()
        {
            var good = GetCurrentGood();
            if (good == null || _recipeIndex < 0 || _recipeIndex >= good.Recipes.Count)
                return null;
            return good.Recipes[_recipeIndex];
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        /// <summary>
        /// Announce the current good.
        /// Format: "{GoodName}, {StorageAmount} in storage, {LimitInfo}"
        /// </summary>
        private void AnnounceGood()
        {
            var good = GetCurrentGood();
            if (good == null)
            {
                Speech.Say("No good selected");
                return;
            }

            var limitInfo = good.Limit > 0 ? $"limit {good.Limit}" : "no limit";
            Speech.Say($"{good.DisplayName}, {good.StorageAmount} in storage, {limitInfo}");
        }

        /// <summary>
        /// Announce the current recipe (brief).
        /// Format: "{WorkshopName}#{Index}: {Status}"
        /// </summary>
        private void AnnounceRecipe()
        {
            var recipe = GetCurrentRecipe();
            if (recipe == null)
            {
                Speech.Say("No recipe selected");
                return;
            }

            string workshopPart = recipe.IsBuilt && recipe.WorkshopIndex > 0
                ? $"{recipe.WorkshopName} #{recipe.WorkshopIndex}"
                : recipe.WorkshopName;

            int gradeLevel = RecipesReflection.GetRecipeGradeLevel(recipe.RecipeModel);
            string stars = gradeLevel == 1 ? ", 1 star" : $", {gradeLevel} stars";

            string status = recipe.IsBuilt
                ? (recipe.IsActive ? "active" : "inactive")
                : "not built";

            Speech.Say($"{workshopPart}{stars}, {status}");
        }

        /// <summary>
        /// Announce the current recipe in full encyclopedia format.
        /// Format: "{WorkshopName}: {Output} x {Amount}: {Inputs} {Time}{Stars}. {Status}"
        /// </summary>
        private void AnnounceRecipeFull()
        {
            var recipe = GetCurrentRecipe();
            if (recipe == null)
            {
                Speech.Say("No recipe selected");
                return;
            }

            var good = GetCurrentGood();
            string outputName = good?.DisplayName ?? RecipesReflection.GetRecipeOutputName(recipe.RecipeModel);
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

    }
}
