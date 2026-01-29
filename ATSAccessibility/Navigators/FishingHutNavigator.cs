using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for FishingHut buildings.
    /// Top section shows Status (Active/Paused) with Enter/Space to toggle.
    /// Followed by Bait, Recipes, Workers, and Upgrades sections.
    /// </summary>
    public class FishingHutNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
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
        private bool _isSleeping;
        private bool _canSleep;

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

        private struct RecipeInfo
        {
            public object RecipeState;
            public string ModelName;
            public string ProductName;
            public bool IsActive;
        }

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "FishingHutNavigator";

        protected override string[] GetSections()
        {
            return _sectionNames;
        }

        protected override int GetItemCount(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            switch (_sectionTypes[sectionIndex])
            {
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

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Bait mode has sub-items for mode selection
            if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0)
            {
                return _baitModeNames?.Length ?? 0;
            }

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                return _workersSection.GetSubItemCount(itemIndex);
            }

            // Upgrades have sub-items (perks)
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.GetSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Status)
            {
                string status = _isSleeping ? "Paused" : "Active";
                Speech.Say($"Status: {status}");
                return;
            }

            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
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

        protected override bool PerformSectionAction(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Status)
            {
                if (!_canSleep)
                {
                    Speech.Say("Cannot pause this building");
                    return false;
                }

                bool wasSleeping = _isSleeping;
                if (BuildingReflection.ToggleBuildingSleep(_building))
                {
                    _isSleeping = !wasSleeping;
                    if (!wasSleeping)
                    {
                        _workersSection.RefreshWorkerIds();
                    }
                    if (_isSleeping)
                    {
                        SoundManager.PlayBuildingSleep();
                        Speech.Say("Paused");
                    }
                    else
                    {
                        SoundManager.PlayBuildingWakeUp();
                        Speech.Say("Active");
                    }
                    return true;
                }
                else
                {
                    Speech.Say("Cannot change building state");
                    return false;
                }
            }

            return false;
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            // Recipes toggle directly on Enter (like Camp buildings)
            if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                ToggleRecipe(itemIndex);
                return true;
            }

            return false;
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0)
            {
                AnnounceBaitModeSubItem(subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                _workersSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0)
            {
                return PerformBaitModeSubItemAction(subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                if (_workersSection.PerformSubItemAction(itemIndex, subItemIndex))
                {
                    _navigationLevel = 1;
                    return true;
                }
                return false;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
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
            sectionNames.Add("Status");
            sectionTypes.Add(SectionType.Status);

            // Always have Bait section for FishingHut
            sectionNames.Add("Bait");
            sectionTypes.Add(SectionType.Bait);

            // Add Recipes if available
            if (_recipes.Count > 0)
            {
                sectionNames.Add("Recipes");
                sectionTypes.Add(SectionType.Recipes);
            }

            // Add Workers if building currently accepts worker assignment
            if (TryInitializeWorkersSection())
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] FishingHutNavigator: Refreshed data, {_recipes.Count} recipes");
        }

        protected override void ClearData()
        {
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

        private void RefreshBaitData()
        {
            _baitMode = BuildingReflection.GetFishingBaitMode(_building);
            _baitCharges = BuildingReflection.GetFishingBaitCharges(_building);
        }

        private void AnnounceBaitItem(int itemIndex)
        {
            // Refresh bait data to get current values
            RefreshBaitData();

            switch (itemIndex)
            {
                case 0:
                    // Bait mode
                    string modeName = _baitModeNames != null && _baitMode < _baitModeNames.Length
                        ? _baitModeNames[_baitMode]
                        : $"Mode {_baitMode}";
                    Speech.Say($"Bait mode: {modeName}");
                    break;

                case 1:
                    // Bait charges
                    Speech.Say($"Bait charges: {_baitCharges}");
                    break;

                case 2:
                    // Bait ingredient
                    string ingredient = !string.IsNullOrEmpty(_baitIngredient)
                        ? CleanupName(_baitIngredient)
                        : "Unknown";
                    Speech.Say($"Bait ingredient: {ingredient}");
                    break;

                default:
                    Speech.Say("Unknown bait info");
                    break;
            }
        }

        private void AnnounceBaitModeSubItem(int subItemIndex)
        {
            if (_baitModeNames == null || subItemIndex >= _baitModeNames.Length)
            {
                Speech.Say("Invalid mode");
                return;
            }

            string modeName = _baitModeNames[subItemIndex];
            bool isSelected = subItemIndex == _baitMode;

            if (isSelected)
                Speech.Say($"{modeName}, selected");
            else
                Speech.Say(modeName);
        }

        private bool PerformBaitModeSubItemAction(int subItemIndex)
        {
            if (_baitModeNames == null || subItemIndex >= _baitModeNames.Length)
                return false;

            if (subItemIndex == _baitMode)
            {
                Speech.Say("Already selected");
                return false;
            }

            if (BuildingReflection.SetFishingBaitMode(_building, subItemIndex))
            {
                _baitMode = subItemIndex;
                string modeName = _baitModeNames[subItemIndex];
                Speech.Say($"{modeName} selected");

                // Exit submenu back to item level
                _navigationLevel = 1;
                return true;
            }

            Speech.Say("Cannot change mode");
            return false;
        }

        // ========================================
        // RECIPES SECTION
        // ========================================

        private void RefreshRecipes()
        {
            _recipes.Clear();

            var recipeStates = BuildingReflection.GetFishingHutRecipes(_building);
            foreach (var recipeState in recipeStates)
            {
                var info = new RecipeInfo
                {
                    RecipeState = recipeState,
                    ModelName = BuildingReflection.GetRecipeModelName(recipeState) ?? "Unknown",
                    ProductName = BuildingReflection.GetRecipeProductName(recipeState),
                    IsActive = BuildingReflection.IsRecipeActive(recipeState)
                };
                _recipes.Add(info);
            }
        }

        private void AnnounceRecipeItem(int itemIndex)
        {
            if (itemIndex >= _recipes.Count)
            {
                Speech.Say("Invalid recipe");
                return;
            }

            var recipe = _recipes[itemIndex];
            string displayName = GetRecipeDisplayName(recipe);
            string status = recipe.IsActive ? "enabled" : "disabled";

            Speech.Say($"{displayName}: {status}");
        }

        private string GetRecipeDisplayName(RecipeInfo recipe)
        {
            if (!string.IsNullOrEmpty(recipe.ProductName))
            {
                return CleanupName(recipe.ProductName);
            }

            if (!string.IsNullOrEmpty(recipe.ModelName))
            {
                return CleanupName(recipe.ModelName);
            }

            return "Unknown Recipe";
        }

        private void ToggleRecipe(int itemIndex)
        {
            if (itemIndex >= _recipes.Count) return;

            var recipe = _recipes[itemIndex];

            if (BuildingReflection.ToggleFishingHutRecipe(_building, recipe.RecipeState))
            {
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
                Speech.Say($"{displayName}: {(newActive ? "enabled" : "disabled")}");
            }
            else
            {
                Speech.Say("Cannot toggle recipe");
            }
        }

        private string CleanupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            string display = name;
            display = display.Replace("_Recipe_", ": ");
            display = display.Replace("Recipe_", "");
            display = display.Replace("[Mat Processed]", "");
            display = display.Replace("[Mat Raw]", "");
            display = display.Replace("_", " ");

            return display.Trim();
        }

    }
}
