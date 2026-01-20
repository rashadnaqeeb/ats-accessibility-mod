using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for FishingHut buildings.
    /// Provides navigation through Info, Bait, Recipes, and Workers sections.
    /// </summary>
    public class FishingHutNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Bait,     // Bait mode settings
            Recipes,  // Fish types to catch
            Workers
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

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
                case SectionType.Info:
                    return GetInfoItemCount();
                case SectionType.Bait:
                    return 3;  // Mode, Charges, Ingredient
                case SectionType.Recipes:
                    return _recipes.Count;
                case SectionType.Workers:
                    return _maxWorkers;
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
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return GetWorkerSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    AnnounceInfoItem(itemIndex);
                    break;
                case SectionType.Bait:
                    AnnounceBaitItem(itemIndex);
                    break;
                case SectionType.Recipes:
                    AnnounceRecipeItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
            }
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

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0)
            {
                AnnounceBaitModeSubItem(subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Bait && itemIndex == 0)
            {
                return PerformBaitModeSubItemAction(subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
            // Cache basic info
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Fishing Hut";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            // Cache worker data
            _workerIds = BuildingReflection.GetWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

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

            // Always have Info section
            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            // Always have Bait section for FishingHut
            sectionNames.Add("Bait");
            sectionTypes.Add(SectionType.Bait);

            // Add Recipes if available
            if (_recipes.Count > 0)
            {
                sectionNames.Add("Recipes");
                sectionTypes.Add(SectionType.Recipes);
            }

            // Add Workers if building has worker slots
            if (_maxWorkers > 0)
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] FishingHutNavigator: Refreshed data for {_buildingName}");
            Debug.Log($"[ATSAccessibility] FishingHutNavigator: {_maxWorkers} workers, {_recipes.Count} recipes, bait mode {_baitMode}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _buildingDescription = null;
            _workerIds = null;
            _recipes.Clear();
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            _baitMode = 0;
            _baitCharges = 0;
            _baitIngredient = null;
            _baitModeNames = null;
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
            count++;  // Status
            count++;  // Worker summary
            return count;
        }

        private void AnnounceInfoItem(int itemIndex)
        {
            int index = 0;

            // Name
            if (itemIndex == index)
            {
                Speech.Say($"Name: {_buildingName}");
                return;
            }
            index++;

            // Description
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index)
                {
                    Speech.Say($"Description: {_buildingDescription}");
                    return;
                }
                index++;
            }

            // Status
            if (itemIndex == index)
            {
                string status = GetStatusText();
                Speech.Say($"Status: {status}");
                return;
            }
            index++;

            // Worker summary
            if (itemIndex == index)
            {
                int workerCount = BuildingReflection.GetWorkerCount(_building);
                Speech.Say($"Workers: {workerCount} of {_maxWorkers}");
                return;
            }

            Speech.Say("Unknown item");
        }

        private string GetStatusText()
        {
            if (!_isFinished) return "Under construction";
            if (_isSleeping) return "Paused";
            return "Active";
        }

        // ========================================
        // BAIT SECTION
        // ========================================

        private void AnnounceBaitItem(int itemIndex)
        {
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
                Speech.Say($"{modeName}. Enter to select");
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

        // ========================================
        // WORKERS SECTION
        // ========================================

        private bool IsValidWorkerIndex(int workerIndex)
        {
            return _workerIds != null && workerIndex >= 0 && workerIndex < _workerIds.Length;
        }

        private void AnnounceWorkerItem(int itemIndex)
        {
            if (!IsValidWorkerIndex(itemIndex))
            {
                Speech.Say("Invalid worker slot");
                return;
            }

            int workerId = _workerIds[itemIndex];
            int slotNum = itemIndex + 1;

            if (workerId <= 0)
            {
                Speech.Say($"Worker slot {slotNum}: Empty");
                return;
            }

            string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
            if (string.IsNullOrEmpty(workerDesc))
            {
                Speech.Say($"Worker slot {slotNum}: Assigned");
                return;
            }

            Speech.Say($"Worker slot {slotNum}: {workerDesc}");
        }

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers();
            _racesRefreshedForWorkerSection = true;
        }

        private int GetWorkerSubItemCount(int workerIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return 0;

            RefreshAvailableRaces();

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);

            int count = _availableRaces.Count;
            if (slotOccupied) count++;  // Add "Unassign" option

            return count;
        }

        private void AnnounceWorkerSubItem(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex))
            {
                Speech.Say("Invalid worker slot");
                return;
            }

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                Speech.Say("Unassign worker. Enter to confirm");
                return;
            }

            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                var (raceName, freeCount) = _availableRaces[raceIndex];
                Speech.Say($"{raceName}: {freeCount} available. Enter to assign");
            }
            else
            {
                Speech.Say("Invalid option");
            }
        }

        private bool PerformWorkerSubItemAction(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return false;

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                // Unassign
                if (BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex))
                {
                    _workerIds = BuildingReflection.GetWorkerIds(_building);
                    RefreshAvailableRaces(force: true);
                    Speech.Say("Worker unassigned");

                    _navigationLevel = 1;
                    return true;
                }
                else
                {
                    Speech.Say("Cannot unassign worker");
                    return false;
                }
            }

            // Assign race
            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                var (raceName, _) = _availableRaces[raceIndex];

                if (slotOccupied)
                {
                    BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex);
                }

                if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName))
                {
                    _workerIds = BuildingReflection.GetWorkerIds(_building);
                    RefreshAvailableRaces(force: true);

                    if (IsValidWorkerIndex(workerIndex))
                    {
                        string workerDesc = BuildingReflection.GetWorkerDescription(_workerIds[workerIndex]);
                        Speech.Say($"Assigned: {workerDesc ?? raceName}");
                    }
                    else
                    {
                        Speech.Say($"Assigned: {raceName}");
                    }

                    _navigationLevel = 1;
                    return true;
                }
                else
                {
                    Speech.Say($"Cannot assign {raceName}");
                    return false;
                }
            }

            return false;
        }
    }
}
