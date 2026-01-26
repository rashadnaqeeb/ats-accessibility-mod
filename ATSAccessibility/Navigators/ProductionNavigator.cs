using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for production buildings (Workshop, Farm, Mine, Camp, etc.).
    /// Top section shows Status (Active/Paused) with Enter/Space to toggle.
    /// Followed by Workers, Recipes, and other sections based on building type.
    /// </summary>
    public class ProductionNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Status,   // Active/Paused toggle at top
            Workers,
            Recipes,
            Rainpunk, // Rainpunk engine control (workshops only)
            Inputs,   // Ingredients storage (input goods)
            Outputs,  // Production storage (output goods)
            Settings, // Camp mode settings
            Fields,   // Farm field capacity
            Upgrades  // Building upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;  // Maps index to section type
        private string _buildingName;
        private bool _isSleeping;
        private bool _canSleep;  // Whether building supports pausing
        private bool _isCamp;  // Camp/gathering buildings have simple recipes (no submenu)

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // Recipe data
        private List<RecipeInfo> _recipes = new List<RecipeInfo>();

        // Storage data
        private List<(string goodName, string displayName, int amount)> _inputGoods = new List<(string, string, int)>();   // Ingredients
        private List<(string goodName, string displayName, int amount)> _outputGoods = new List<(string, string, int)>();  // Products
        private bool _hasInputStorage = false;   // Building has ingredients storage capability
        private bool _hasOutputStorage = false;  // Building has production storage capability

        // Farm-specific data
        private bool _isFarm = false;
        private int _farmSownFields = 0;
        private int _farmPlowedFields = 0;
        private int _farmTotalFields = 0;

        // Camp mode data
        private int _campMode = 0;
        private string[] _campModeNames;

        // Rainpunk data
        private bool _hasRainpunk = false;
        private bool _rainpunkUnlocked = false;
        private int _engineCount = 0;

        // ========================================
        // RECIPE INFO STRUCT
        // ========================================

        private struct RecipeInfo
        {
            public object RecipeState;
            public string ModelName;
            public string ProductName;  // The good being produced
            public bool IsActive;
            public int Limit;
            public bool IsLimitLocal;
            public int Priority;
        }

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "ProductionNavigator";

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
                case SectionType.Workers:
                    return _maxWorkers;
                case SectionType.Recipes:
                    return _recipes.Count;
                case SectionType.Rainpunk:
                    return GetRainpunkItemCount();
                case SectionType.Inputs:
                    return _inputGoods.Count > 0 ? _inputGoods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Outputs:
                    return _outputGoods.Count > 0 ? _outputGoods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Settings:
                    return 1;  // Single item showing current mode
                case SectionType.Fields:
                    return 2;  // Sown fields and Plowed fields
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

            // Outputs have sub-items (force transport, auto-deliver)
            if (_sectionTypes[sectionIndex] == SectionType.Outputs && itemIndex < _outputGoods.Count)
            {
                return 2;  // Force transport now, Auto-deliver when produced
            }

            // Inputs have one sub-item (return to warehouse confirmation)
            if (_sectionTypes[sectionIndex] == SectionType.Inputs && itemIndex < _inputGoods.Count)
            {
                return 1;  // Return to warehouse
            }

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return GetWorkerSubItemCount(itemIndex);
            }

            // Only recipes have sub-items, and only for non-Camp buildings
            // Camp/gathering buildings have simple recipes that just toggle on/off
            if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                if (_isCamp)
                    return 0;  // No submenu for Camp buildings
                return GetRecipeSubItemCount(itemIndex);
            }

            // Settings section has sub-items for mode selection
            if (_sectionTypes[sectionIndex] == SectionType.Settings)
            {
                return _campModeNames?.Length ?? 0;
            }

            // Upgrades section has sub-items (perks)
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
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Recipes:
                    AnnounceRecipeItem(itemIndex);
                    break;
                case SectionType.Rainpunk:
                    AnnounceRainpunkItem(itemIndex);
                    break;
                case SectionType.Inputs:
                    AnnounceInputItem(itemIndex);
                    break;
                case SectionType.Outputs:
                    AnnounceOutputItem(itemIndex);
                    break;
                case SectionType.Settings:
                    AnnounceSettingsItem(itemIndex);
                    break;
                case SectionType.Fields:
                    AnnounceFieldsItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                ToggleRecipe(itemIndex);
                return true;
            }

            // Rainpunk unlock
            if (_sectionTypes[sectionIndex] == SectionType.Rainpunk && !_rainpunkUnlocked && itemIndex == 0)
            {
                if (!BuildingReflection.CanAffordRainpunkUnlock(_building))
                {
                    Speech.Say("Not enough resources");
                    SoundManager.PlayFailed();
                    return false;
                }

                if (BuildingReflection.UnlockRainpunk(_building))
                {
                    _rainpunkUnlocked = true;
                    _engineCount = BuildingReflection.GetEngineCount(_building);
                    SoundManager.PlayRainpunkUnlock();
                    Speech.Say("Rainpunk unlocked");
                    return true;
                }
                else
                {
                    Speech.Say("Cannot unlock rainpunk");
                    SoundManager.PlayFailed();
                    return false;
                }
            }

            return false;
        }

        protected override bool PerformSectionAction(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Status)
            {
                // Toggle sleep/active state
                if (!_canSleep)
                {
                    Speech.Say("Cannot pause this building");
                    return false;
                }

                bool wasSleeping = _isSleeping;
                if (BuildingReflection.ToggleBuildingSleep(_building))
                {
                    _isSleeping = !wasSleeping;
                    // Refresh worker data since workers get unassigned on pause
                    if (!wasSleeping)
                    {
                        _workerIds = BuildingReflection.GetWorkerIds(_building);
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

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Outputs && itemIndex < _outputGoods.Count)
            {
                AnnounceOutputSubItem(itemIndex, subItemIndex);
                return;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Inputs && itemIndex < _inputGoods.Count)
            {
                AnnounceInputSubItem(itemIndex, subItemIndex);
                return;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                AnnounceRecipeSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Settings)
            {
                AnnounceSettingsSubItem(subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)
        {
            // At sub-item level, +/- adjusts limit
            if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                // Shift modifier increases increment to 10
                int increment = modifiers.Shift ? delta * 10 : delta;
                AdjustRecipeLimit(itemIndex, increment);
            }
            // Rainpunk engine level adjustment (only for engine items, not info items)
            else if (_sectionTypes[sectionIndex] == SectionType.Rainpunk)
            {
                AdjustEngineLevel(itemIndex, delta);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Outputs && itemIndex < _outputGoods.Count)
            {
                return PerformOutputSubItemAction(itemIndex, subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Inputs && itemIndex < _inputGoods.Count)
            {
                return PerformInputSubItemAction(itemIndex, subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Recipes && itemIndex < _recipes.Count)
            {
                // Only status sub-item is actionable (toggle)
                if (subItemIndex == RECIPE_SUBITEM_STATUS)
                {
                    ToggleRecipe(itemIndex);
                    return true;
                }
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Settings)
            {
                return PerformSettingsSubItemAction(subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void RefreshData()
        {
            // Cache basic info first
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Unknown building";
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _canSleep = BuildingReflection.CanBuildingSleep(_building);
            _isCamp = BuildingReflection.IsCamp(_building);  // Camp buildings have simple recipes
            _isFarm = BuildingReflection.IsFarm(_building);

            // Cache worker data
            _workerIds = BuildingReflection.GetWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

            // Cache recipe data
            RefreshRecipes();

            // Cache storage data
            RefreshStorage();

            // Cache Farm-specific data
            if (_isFarm)
            {
                _farmSownFields = BuildingReflection.GetFarmSownFields(_building);
                _farmPlowedFields = BuildingReflection.GetFarmPlowedFields(_building);
                _farmTotalFields = BuildingReflection.GetFarmTotalFields(_building);
            }

            // Cache Camp mode data
            if (_isCamp)
            {
                _campMode = BuildingReflection.GetCampMode(_building);
                _campModeNames = BuildingReflection.GetCampModeNames();
            }

            // Cache Rainpunk data (workshops only)
            _hasRainpunk = BuildingReflection.HasRainpunkCapability(_building);
            _rainpunkUnlocked = BuildingReflection.IsRainpunkUnlocked(_building);
            _engineCount = _rainpunkUnlocked ? BuildingReflection.GetEngineCount(_building) : 0;

            // Build sections list dynamically based on what's available
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            // Always have Status section at top (announced dynamically as "Status: Active/Paused")
            sectionNames.Add("Status");
            sectionTypes.Add(SectionType.Status);

            // Only add Workers if building currently accepts worker assignment
            if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            // Only add Recipes if building has recipes
            if (_recipes.Count > 0)
            {
                sectionNames.Add("Recipes");
                sectionTypes.Add(SectionType.Recipes);
            }

            // Add Rainpunk section if workshop has rainpunk capability (unlocked or not)
            if (_hasRainpunk)
            {
                sectionNames.Add("Rainpunk");
                sectionTypes.Add(SectionType.Rainpunk);
            }

            // Add Inputs section if building has IngredientsStorage capability
            if (_hasInputStorage)
            {
                sectionNames.Add("Inputs");
                sectionTypes.Add(SectionType.Inputs);
            }

            // Add Outputs section if building has ProductionStorage capability
            if (_hasOutputStorage)
            {
                sectionNames.Add("Outputs");
                sectionTypes.Add(SectionType.Outputs);
            }

            // Add Settings section for Camp buildings (mode selection)
            if (_isCamp)
            {
                sectionNames.Add("Settings");
                sectionTypes.Add(SectionType.Settings);
            }

            // Add Fields section for Farm buildings
            if (_isFarm)
            {
                sectionNames.Add("Fields");
                sectionTypes.Add(SectionType.Fields);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _workerIds = null;
            _recipes.Clear();
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            _inputGoods.Clear();
            _outputGoods.Clear();
            _hasInputStorage = false;
            _hasOutputStorage = false;
            _isFarm = false;
            _farmSownFields = 0;
            _farmPlowedFields = 0;
            _farmTotalFields = 0;
            _campMode = 0;
            _campModeNames = null;
            _hasRainpunk = false;
            _rainpunkUnlocked = false;
            _engineCount = 0;
            ClearUpgradesSection();
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

        // ========================================
        // WORKER SUB-ITEMS (ASSIGNMENT)
        // ========================================

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers(includeZeroFree: true);
            _racesRefreshedForWorkerSection = true;
        }

        private int GetWorkerSubItemCount(int workerIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return 0;

            // Refresh available races when entering worker submenu (only if not already cached)
            RefreshAvailableRaces();

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);

            // If slot is occupied: "Unassign" + available races
            // If slot is empty: just available races
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
                // Unassign option
                Speech.Say("Unassign worker");
                return;
            }

            // Race options
            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                var (raceName, freeCount) = _availableRaces[raceIndex];
                string bonus = BuildingReflection.GetRaceBonusForBuilding(_building, raceName);
                if (!string.IsNullOrEmpty(bonus))
                {
                    // If bonus contains a comma, it already has a description - don't add "specialist"
                    if (bonus.Contains(","))
                    {
                        Speech.Say($"{raceName}: {freeCount} available, {bonus}");
                    }
                    else
                    {
                        Speech.Say($"{raceName}: {freeCount} available, {bonus} specialist");
                    }
                }
                else
                {
                    Speech.Say($"{raceName}: {freeCount} available");
                }
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
                    // Refresh worker data
                    _workerIds = BuildingReflection.GetWorkerIds(_building);
                    RefreshAvailableRaces(force: true);
                    Speech.Say("Worker unassigned");

                    // Exit submenu back to worker slot level
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
                var (raceName, freeCount) = _availableRaces[raceIndex];

                // Check if race has free workers
                if (freeCount == 0)
                {
                    Speech.Say($"No free {raceName} workers");
                    SoundManager.PlayFailed();
                    return false;
                }

                // If slot is occupied, unassign first
                if (slotOccupied)
                {
                    BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex);
                }

                if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName))
                {
                    // Refresh worker data
                    _workerIds = BuildingReflection.GetWorkerIds(_building);
                    RefreshAvailableRaces(force: true);

                    // Announce the new worker
                    if (IsValidWorkerIndex(workerIndex))
                    {
                        string workerDesc = BuildingReflection.GetWorkerDescription(_workerIds[workerIndex]);
                        Speech.Say($"Assigned: {workerDesc ?? raceName}");
                    }
                    else
                    {
                        Speech.Say($"Assigned: {raceName}");
                    }

                    // Exit submenu back to worker slot level
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

        // ========================================
        // RECIPES SECTION
        // ========================================

        private void RefreshRecipes()
        {
            _recipes.Clear();

            var recipeStates = BuildingReflection.GetRecipes(_building);

            foreach (var recipeState in recipeStates)
            {
                var info = new RecipeInfo
                {
                    RecipeState = recipeState,
                    ModelName = BuildingReflection.GetRecipeModelName(recipeState) ?? "Unknown",
                    ProductName = BuildingReflection.GetRecipeProductName(recipeState),
                    IsActive = BuildingReflection.IsRecipeActive(recipeState),
                    Limit = BuildingReflection.GetRecipeLimit(recipeState),
                    IsLimitLocal = BuildingReflection.IsRecipeLimitLocal(recipeState),
                    Priority = GetRecipePriority(recipeState)
                };
                _recipes.Add(info);
            }
        }

        private int GetRecipePriority(object recipeState)
        {
            // Try to get priority from recipe state
            // Priority field is "prio" in RecipeState
            try
            {
                var prioField = recipeState.GetType().GetField("prio", GameReflection.PublicInstance);
                if (prioField != null)
                {
                    return (int?)prioField.GetValue(recipeState) ?? 0;
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetRecipePriority failed: {ex.Message}"); }
            return 0;
        }

        // ========================================
        // STORAGE SECTION
        // ========================================

        private void RefreshStorage()
        {
            _inputGoods.Clear();
            _outputGoods.Clear();
            _hasInputStorage = false;
            _hasOutputStorage = false;

            // Check and get input goods (ingredients storage) - only for IWorkshop buildings
            _hasInputStorage = BuildingReflection.HasIngredientsStorage(_building);
            if (_hasInputStorage)
            {
                var inputs = BuildingReflection.GetIngredientsStorageGoods(_building);
                foreach (var (goodName, amount) in inputs)
                {
                    string displayName = BuildingReflection.GetGoodDisplayName(goodName);
                    if (string.IsNullOrEmpty(displayName))
                        displayName = CleanupName(goodName);
                    _inputGoods.Add((goodName, displayName, amount));
                }
            }

            // Check and get output goods (production storage)
            _hasOutputStorage = BuildingReflection.HasProductionStorage(_building);
            if (_hasOutputStorage)
            {
                var outputs = BuildingReflection.GetProductionStorageGoods(_building);
                foreach (var (goodName, amount) in outputs)
                {
                    string displayName = BuildingReflection.GetGoodDisplayName(goodName);
                    if (string.IsNullOrEmpty(displayName))
                        displayName = CleanupName(goodName);
                    _outputGoods.Add((goodName, displayName, amount));
                }
            }

        }

        private void AnnounceInputItem(int itemIndex)
        {
            if (_inputGoods.Count == 0)
            {
                Speech.Say("Empty");
                return;
            }

            if (itemIndex >= _inputGoods.Count)
            {
                Speech.Say("Invalid input item");
                return;
            }

            var (goodName, displayName, amount) = _inputGoods[itemIndex];
            Speech.Say($"{amount} {displayName}");
        }

        private void AnnounceInputSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex >= _inputGoods.Count)
            {
                Speech.Say("Invalid input item");
                return;
            }

            var (goodName, displayName, amount) = _inputGoods[itemIndex];

            if (subItemIndex == 0)
            {
                Speech.Say($"Return {amount} {displayName} to warehouse");
            }
            else
            {
                Speech.Say("Invalid option");
            }
        }

        private bool PerformInputSubItemAction(int itemIndex, int subItemIndex)
        {
            if (itemIndex >= _inputGoods.Count)
                return false;

            if (subItemIndex != 0)
                return false;

            var (goodName, displayName, amount) = _inputGoods[itemIndex];

            if (BuildingReflection.ReturnIngredientToWarehouse(_building, goodName, amount))
            {
                Speech.Say($"Returned {amount} {displayName} to warehouse");

                // Refresh storage data and go back to item level
                RefreshStorage();
                _navigationLevel = 1;

                // If inputs are now empty, announce that
                if (_inputGoods.Count == 0)
                {
                    Speech.Say("Inputs now empty");
                }

                return true;
            }

            Speech.Say($"Failed to return {displayName}");
            return false;
        }

        private void AnnounceOutputItem(int itemIndex)
        {
            if (_outputGoods.Count == 0)
            {
                Speech.Say("Empty");
                return;
            }

            if (itemIndex >= _outputGoods.Count)
            {
                Speech.Say("Invalid output item");
                return;
            }

            var (goodName, displayName, amount) = _outputGoods[itemIndex];

            // Get delivery state
            var (isForced, isConstantForced) = BuildingReflection.GetOutputDeliveryState(_building, goodName);
            string status = "";
            if (isConstantForced)
                status = ", auto-deliver on";
            else if (isForced)
                status = ", transport queued";

            Speech.Say($"{amount} {displayName}{status}");
        }

        private void AnnounceOutputSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex >= _outputGoods.Count)
            {
                Speech.Say("Invalid output item");
                return;
            }

            var (goodName, displayName, amount) = _outputGoods[itemIndex];
            var (isForced, isConstantForced) = BuildingReflection.GetOutputDeliveryState(_building, goodName);

            switch (subItemIndex)
            {
                case 0:
                    if (isForced)
                        Speech.Say("Transport queued");
                    else
                        Speech.Say("Force transport now");
                    break;
                case 1:
                    if (isConstantForced)
                        Speech.Say("Auto-deliver when produced: On");
                    else
                        Speech.Say("Auto-deliver when produced: Off");
                    break;
                default:
                    Speech.Say("Invalid option");
                    break;
            }
        }

        private bool PerformOutputSubItemAction(int itemIndex, int subItemIndex)
        {
            if (itemIndex >= _outputGoods.Count)
                return false;

            var (goodName, displayName, amount) = _outputGoods[itemIndex];

            switch (subItemIndex)
            {
                case 0:
                    // Toggle force transport
                    if (BuildingReflection.ToggleForceDelivery(_building, goodName))
                    {
                        var (isForced, _) = BuildingReflection.GetOutputDeliveryState(_building, goodName);
                        if (isForced)
                            Speech.Say($"Transport queued for {displayName}");
                        else
                            Speech.Say($"Transport cancelled for {displayName}");
                        return true;
                    }
                    Speech.Say("Failed to toggle transport");
                    return false;

                case 1:
                    // Toggle auto-deliver
                    if (BuildingReflection.ToggleConstantDelivery(_building, goodName))
                    {
                        var (_, isConstantForced) = BuildingReflection.GetOutputDeliveryState(_building, goodName);
                        if (isConstantForced)
                            Speech.Say($"Auto-deliver enabled for {displayName}");
                        else
                            Speech.Say($"Auto-deliver disabled for {displayName}");
                        return true;
                    }
                    Speech.Say("Failed to toggle auto-deliver");
                    return false;

                default:
                    return false;
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

            // Use product name if available, otherwise fall back to cleaned model name
            string displayName = GetRecipeDisplayName(recipe);

            string status = recipe.IsActive ? "enabled" : "disabled";

            string limitText = "";
            if (recipe.Limit > 0)
            {
                limitText = recipe.IsLimitLocal ? $", limit {recipe.Limit}" : $", global limit {recipe.Limit}";
            }

            Speech.Say($"{displayName}: {status}{limitText}");
        }

        private string GetRecipeDisplayName(RecipeInfo recipe)
        {
            // Prefer product name (good name) if available
            if (!string.IsNullOrEmpty(recipe.ProductName))
            {
                // ProductName is the internal good name, need to look up display name
                // For now, just clean it up
                return CleanupName(recipe.ProductName);
            }

            // Fall back to model name
            if (!string.IsNullOrEmpty(recipe.ModelName))
            {
                return CleanupName(recipe.ModelName);
            }

            return "Unknown Recipe";
        }

        private string CleanupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Remove common prefixes and replace underscores
            string display = name;
            display = display.Replace("_Recipe_", ": ");
            display = display.Replace("Recipe_", "");
            display = display.Replace("[Mat Processed]", "");
            display = display.Replace("[Mat Raw]", "");
            display = display.Replace("_", " ");

            return display.Trim();
        }

        private void ToggleRecipe(int itemIndex)
        {
            if (itemIndex >= _recipes.Count) return;

            var recipe = _recipes[itemIndex];

            // Use IWorkshop.SwitchProductionOf to toggle recipe
            if (BuildingReflection.ToggleRecipe(_building, recipe.RecipeState))
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

        // ========================================
        // SETTINGS SECTION (Camp modes)
        // ========================================

        private void AnnounceSettingsItem(int itemIndex)
        {
            if (itemIndex != 0) return;

            string modeName = _campModeNames != null && _campMode < _campModeNames.Length
                ? _campModeNames[_campMode]
                : $"Mode {_campMode}";

            Speech.Say($"Cutting mode: {modeName}");
        }

        private void AnnounceSettingsSubItem(int subItemIndex)
        {
            if (_campModeNames == null || subItemIndex >= _campModeNames.Length)
            {
                Speech.Say("Invalid mode");
                return;
            }

            string modeName = _campModeNames[subItemIndex];
            bool isSelected = subItemIndex == _campMode;

            if (isSelected)
                Speech.Say($"{modeName}, selected");
            else
                Speech.Say(modeName);
        }

        private bool PerformSettingsSubItemAction(int subItemIndex)
        {
            if (_campModeNames == null || subItemIndex >= _campModeNames.Length)
                return false;

            if (subItemIndex == _campMode)
            {
                // Already selected
                Speech.Say("Already selected");
                return false;
            }

            if (BuildingReflection.SetCampMode(_building, subItemIndex))
            {
                _campMode = subItemIndex;
                string modeName = _campModeNames[subItemIndex];
                Speech.Say($"{modeName} selected");

                // Exit submenu back to item level
                _navigationLevel = 1;
                return true;
            }

            Speech.Say("Cannot change mode");
            return false;
        }

        // ========================================
        // FIELDS SECTION (Farm)
        // ========================================

        private void AnnounceFieldsItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    Speech.Say($"Sown fields: {_farmSownFields} of {_farmTotalFields}");
                    break;
                case 1:
                    Speech.Say($"Plowed fields: {_farmPlowedFields} of {_farmTotalFields}");
                    break;
                default:
                    Speech.Say("Unknown field info");
                    break;
            }
        }

        // ========================================
        // RAINPUNK SECTION
        // ========================================

        // Rainpunk item layout:
        // 0: Water stored
        // 1: Water use per second
        // 2: Blightrot meter (only if blight is active and spawning)
        // 3+: Engines

        private const int RAINPUNK_ITEM_WATER_STORED = 0;
        private const int RAINPUNK_ITEM_WATER_USE = 1;
        private const int RAINPUNK_ITEM_BLIGHT = 2;

        private int GetRainpunkItemCount()
        {
            // If not unlocked, just show the unlock item
            if (!_rainpunkUnlocked)
            {
                return 1;  // Unlock item only
            }

            int count = 2;  // Water stored + Water use
            if (BuildingReflection.GetBlightProgress(_building) >= 0)
            {
                count++;  // Blightrot meter
            }
            count += _engineCount;  // Engines
            return count;
        }

        private int GetRainpunkEngineStartIndex()
        {
            // Engines start after water stored, water use, and optionally blight meter
            if (BuildingReflection.GetBlightProgress(_building) >= 0)
            {
                return 3;  // After water stored, water use, blight
            }
            return 2;  // After water stored, water use
        }

        private void AnnounceRainpunkItem(int itemIndex)
        {
            // If not unlocked, only item is the unlock option
            if (!_rainpunkUnlocked)
            {
                var price = BuildingReflection.GetRainpunkUnlockPrice(_building);
                if (price != null)
                {
                    bool canAfford = BuildingReflection.CanAffordRainpunkUnlock(_building);
                    string affordText = canAfford ? "" : ", not enough resources";
                    Speech.Say($"Locked, costs {price.Value.amount} {price.Value.displayName}{affordText}");
                }
                else
                {
                    Speech.Say("Locked");
                }
                return;
            }

            int engineStartIndex = GetRainpunkEngineStartIndex();

            if (itemIndex == RAINPUNK_ITEM_WATER_STORED)
            {
                int current = BuildingReflection.GetWaterTankCurrent(_building);
                int capacity = BuildingReflection.GetWaterTankCapacity(_building);
                Speech.Say($"Water stored: {current} of {capacity}");
            }
            else if (itemIndex == RAINPUNK_ITEM_WATER_USE)
            {
                float usePerSec = BuildingReflection.GetTotalWaterUsePerSecond(_building);
                float usePerMin = usePerSec * 60f;
                if (usePerMin > 0)
                {
                    Speech.Say($"Water use: {usePerMin:F1} per minute");
                }
                else
                {
                    Speech.Say("Water use: None");
                }
            }
            else if (itemIndex == RAINPUNK_ITEM_BLIGHT && engineStartIndex == 3)
            {
                int blightProgress = BuildingReflection.GetBlightProgress(_building);
                Speech.Say($"Blightrot: {blightProgress}%");
            }
            else if (itemIndex >= engineStartIndex)
            {
                int engineIndex = itemIndex - engineStartIndex;
                AnnounceEngine(engineIndex);
            }
            else
            {
                Speech.Say("Unknown");
            }
        }

        private string GetRainpunkItemName(int itemIndex)
        {
            // If not unlocked, only item is unlock
            if (!_rainpunkUnlocked)
            {
                return itemIndex == 0 ? "Unlock" : null;
            }

            int engineStartIndex = GetRainpunkEngineStartIndex();

            if (itemIndex == RAINPUNK_ITEM_WATER_STORED)
                return "Water stored";
            if (itemIndex == RAINPUNK_ITEM_WATER_USE)
                return "Water use";
            if (itemIndex == RAINPUNK_ITEM_BLIGHT && engineStartIndex == 3)
                return "Blightrot";
            if (itemIndex >= engineStartIndex)
                return $"Engine {itemIndex - engineStartIndex + 1}";
            return null;
        }

        private void AnnounceEngine(int engineIndex)
        {
            if (engineIndex >= _engineCount)
            {
                Speech.Say("Invalid engine");
                return;
            }

            int currentLevel = BuildingReflection.GetEngineCurrentLevel(_building, engineIndex);
            int requestedLevel = BuildingReflection.GetEngineRequestedLevel(_building, engineIndex);
            int maxLevel = BuildingReflection.GetEngineMaxLevel(_building, engineIndex);

            string engineName = $"Engine {engineIndex + 1}";

            if (requestedLevel == 0)
            {
                Speech.Say($"{engineName}: Off, max {maxLevel}");
            }
            else if (currentLevel < requestedLevel)
            {
                Speech.Say($"{engineName}: {requestedLevel} of {maxLevel}, low water");
            }
            else
            {
                Speech.Say($"{engineName}: {requestedLevel} of {maxLevel}");
            }
        }

        private void AdjustEngineLevel(int itemIndex, int delta)
        {
            int engineStartIndex = GetRainpunkEngineStartIndex();
            if (itemIndex < engineStartIndex) return;  // Not an engine item

            int engineIndex = itemIndex - engineStartIndex;
            if (engineIndex >= _engineCount) return;

            int maxLevel = BuildingReflection.GetEngineMaxLevel(_building, engineIndex);
            int currentRequested = BuildingReflection.GetEngineRequestedLevel(_building, engineIndex);
            int newLevel = Mathf.Clamp(currentRequested + delta, 0, maxLevel);

            if (newLevel == currentRequested)
            {
                // At limit
                Speech.Say(delta > 0 ? "Maximum" : "Minimum");
                return;
            }

            bool success = false;
            if (delta > 0)
            {
                success = BuildingReflection.IncreaseEngineLevel(_building, engineIndex);
                if (success) BuildingReflection.PlayEngineUpSound(_building, engineIndex);
            }
            else
            {
                success = BuildingReflection.DecreaseEngineLevel(_building, engineIndex);
                if (success) BuildingReflection.PlayEngineDownSound(_building, engineIndex);
            }

            if (success)
            {
                if (newLevel == 0)
                {
                    Speech.Say("Off");
                }
                else
                {
                    string effect = BuildingReflection.GetEngineLevelEffect(_building, engineIndex, newLevel);
                    if (!string.IsNullOrEmpty(effect))
                    {
                        Speech.Say($"{newLevel}, {effect}");
                    }
                    else
                    {
                        Speech.Say($"{newLevel}");
                    }
                }
            }
        }

        // ========================================
        // RECIPE SUB-ITEMS
        // ========================================

        // Sub-item indices for recipes:
        // 0: Status (enabled/disabled)
        // 1: Production info (combined: produces X items every Y seconds. Z stars)
        // 2: Limit
        // 3+: Ingredient slots

        private const int RECIPE_SUBITEM_STATUS = 0;
        private const int RECIPE_SUBITEM_PRODUCTION = 1;
        private const int RECIPE_SUBITEM_LIMIT = 2;
        private const int RECIPE_SUBITEM_INGREDIENTS_START = 3;

        private int GetRecipeSubItemCount(int recipeIndex)
        {
            if (recipeIndex >= _recipes.Count) return 0;

            var recipe = _recipes[recipeIndex];
            int ingredientSlots = BuildingReflection.GetRecipeIngredientSlotCount(recipe.RecipeState);

            // Status + Production info + Limit + Ingredient slots
            return 3 + ingredientSlots;
        }

        private void AnnounceRecipeSubItem(int recipeIndex, int subItemIndex)
        {
            if (recipeIndex >= _recipes.Count) return;

            var recipe = _recipes[recipeIndex];

            switch (subItemIndex)
            {
                case RECIPE_SUBITEM_STATUS:
                    bool isActive = BuildingReflection.IsRecipeActive(recipe.RecipeState);
                    Speech.Say($"Status: {(isActive ? "enabled" : "disabled")}. Space to toggle");
                    break;

                case RECIPE_SUBITEM_PRODUCTION:
                    AnnounceProductionInfo(recipe.RecipeState);
                    break;

                case RECIPE_SUBITEM_LIMIT:
                    int limit = BuildingReflection.GetRecipeLimit(recipe.RecipeState);
                    bool isLocal = BuildingReflection.IsRecipeLimitLocal(recipe.RecipeState);
                    string limitText;
                    if (limit > 0)
                        limitText = isLocal ? limit.ToString() : $"global {limit}";
                    else
                        limitText = "unlimited";
                    Speech.Say($"Limit: {limitText}. Plus/minus to adjust");
                    break;

                default:
                    // Ingredient slots
                    int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
                    AnnounceIngredientSlot(recipe.RecipeState, slotIndex);
                    break;
            }
        }

        private void AnnounceProductionInfo(object recipeState)
        {
            int amount = BuildingReflection.GetRecipeProducedAmount(recipeState);
            string productName = BuildingReflection.GetRecipeProducedGoodDisplayName(recipeState);
            if (string.IsNullOrEmpty(productName))
            {
                productName = BuildingReflection.GetRecipeProductName(recipeState);
                if (!string.IsNullOrEmpty(productName))
                    productName = CleanupName(productName);
            }
            productName = productName ?? "items";

            float time = BuildingReflection.GetRecipeProductionTime(recipeState);
            int grade = BuildingReflection.GetRecipeGrade(recipeState);

            // Format time to remove .0 if whole number
            string timeText = time % 1 == 0 ? $"{(int)time}" : $"{time:F1}";

            Speech.Say($"Produces: {amount} {productName} every {timeText} seconds. {grade} stars");
        }

        private void AnnounceIngredientSlot(object recipeState, int slotIndex)
        {
            var options = BuildingReflection.GetIngredientSlotOptions(recipeState, slotIndex);
            if (options.Length == 0)
            {
                Speech.Say($"Ingredient {slotIndex + 1}: Unknown");
                return;
            }

            var enabledItems = new List<string>();
            var disabledItems = new List<string>();

            foreach (var option in options)
            {
                string name = BuildingReflection.GetIngredientGoodName(option) ?? "Unknown";
                name = CleanupName(name);
                int amount = BuildingReflection.GetIngredientAmount(option);
                bool allowed = BuildingReflection.IsIngredientAllowed(option);

                // Format as "X name" (e.g., "3 wood")
                string itemText = $"{amount} {name}";

                if (allowed)
                    enabledItems.Add(itemText);
                else
                    disabledItems.Add(itemText);
            }

            string announcement = $"Ingredient {slotIndex + 1}: ";
            if (enabledItems.Count > 0)
            {
                announcement += string.Join(", ", enabledItems);
            }
            else
            {
                announcement += "none enabled";
            }

            if (disabledItems.Count > 0)
            {
                announcement += $". Disabled: {string.Join(", ", disabledItems)}";
            }

            Speech.Say(announcement);
        }

        private void AdjustRecipeLimit(int recipeIndex, int delta)
        {
            if (recipeIndex >= _recipes.Count) return;

            var recipe = _recipes[recipeIndex];
            int currentLimit = BuildingReflection.GetRecipeLimit(recipe.RecipeState);

            // Calculate new limit (0 = unlimited, can't go below 0)
            int newLimit;
            if (currentLimit == 0)
            {
                // Currently unlimited, +1 sets to 1, -1 stays at 0
                newLimit = delta > 0 ? delta : 0;
            }
            else
            {
                newLimit = System.Math.Max(0, currentLimit + delta);
            }

            BuildingReflection.SetRecipeLimit(recipe.RecipeState, newLimit);

            // Update cached values (SetRecipeLimit marks as local)
            var updatedRecipe = recipe;
            updatedRecipe.Limit = newLimit;
            updatedRecipe.IsLimitLocal = true;
            _recipes[recipeIndex] = updatedRecipe;

            string limitText = newLimit > 0 ? newLimit.ToString() : "unlimited";
            Speech.Say($"Limit: {limitText}");
        }

        // ========================================
        // INGREDIENT OPTIONS (LEVEL 3)
        // ========================================

        protected override int GetSubSubItemCount(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Only recipe ingredient slots have sub-sub-items
            if (_sectionTypes[sectionIndex] != SectionType.Recipes)
                return 0;

            if (itemIndex >= _recipes.Count)
                return 0;

            // Only ingredient slots (subItemIndex >= 4) have options
            if (subItemIndex < RECIPE_SUBITEM_INGREDIENTS_START)
                return 0;

            int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
            var recipe = _recipes[itemIndex];
            var options = BuildingReflection.GetIngredientSlotOptions(recipe.RecipeState, slotIndex);

            // Only show Level 3 if there are multiple options to choose from
            return options.Length > 1 ? options.Length : 0;
        }

        protected override void AnnounceSubSubItem(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            if (_sectionTypes[sectionIndex] != SectionType.Recipes)
                return;

            if (itemIndex >= _recipes.Count)
                return;

            int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
            if (slotIndex < 0) return;

            var recipe = _recipes[itemIndex];
            var options = BuildingReflection.GetIngredientSlotOptions(recipe.RecipeState, slotIndex);

            if (subSubItemIndex >= options.Length)
            {
                Speech.Say("Invalid option");
                return;
            }

            var option = options[subSubItemIndex];
            string name = BuildingReflection.GetIngredientGoodName(option) ?? "Unknown";
            name = CleanupName(name);
            int amount = BuildingReflection.GetIngredientAmount(option);
            bool allowed = BuildingReflection.IsIngredientAllowed(option);

            Speech.Say($"{amount} {name}: {(allowed ? "enabled" : "disabled")}. Space to toggle");
        }

        protected override bool PerformSubSubItemAction(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            if (_sectionTypes[sectionIndex] != SectionType.Recipes)
                return false;

            if (itemIndex >= _recipes.Count)
                return false;

            int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
            if (slotIndex < 0) return false;

            var recipe = _recipes[itemIndex];
            var options = BuildingReflection.GetIngredientSlotOptions(recipe.RecipeState, slotIndex);

            if (subSubItemIndex >= options.Length)
                return false;

            var option = options[subSubItemIndex];

            // Toggle the ingredient option
            BuildingReflection.ToggleIngredientAllowed(option);

            // Announce new state
            string name = BuildingReflection.GetIngredientGoodName(option) ?? "Unknown";
            name = CleanupName(name);
            int amount = BuildingReflection.GetIngredientAmount(option);
            bool newAllowed = BuildingReflection.IsIngredientAllowed(option);
            Speech.Say($"{amount} {name}: {(newAllowed ? "enabled" : "disabled")}");

            return true;
        }

        // ========================================
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
            // Use section names array
            if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
                return _sectionNames[sectionIndex];
            return null;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    return GetWorkerItemName(itemIndex);
                case SectionType.Recipes:
                    return GetRecipeItemName(itemIndex);
                case SectionType.Rainpunk:
                    return GetRainpunkItemName(itemIndex);
                case SectionType.Inputs:
                    return itemIndex < _inputGoods.Count ? _inputGoods[itemIndex].displayName : null;
                case SectionType.Outputs:
                    return itemIndex < _outputGoods.Count ? _outputGoods[itemIndex].displayName : null;
                case SectionType.Settings:
                    return "Cutting mode";
                case SectionType.Fields:
                    return itemIndex == 0 ? "Sown" : "Plowed";
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        private string GetWorkerItemName(int itemIndex)
        {
            if (!IsValidWorkerIndex(itemIndex))
                return null;

            int workerId = _workerIds[itemIndex];
            if (workerId <= 0)
                return $"Slot {itemIndex + 1}";

            // Return worker race/name for search
            string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
            return !string.IsNullOrEmpty(workerDesc) ? workerDesc : $"Slot {itemIndex + 1}";
        }

        private string GetRecipeItemName(int itemIndex)
        {
            if (itemIndex >= _recipes.Count)
                return null;

            var recipe = _recipes[itemIndex];
            return GetRecipeDisplayName(recipe);
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    return GetWorkerSubItemName(itemIndex, subItemIndex);
                case SectionType.Recipes:
                    return GetRecipeSubItemName(itemIndex, subItemIndex);
                case SectionType.Outputs:
                    return subItemIndex == 0 ? "Transport" : "Auto-deliver";
                case SectionType.Inputs:
                    return "Return";
                case SectionType.Settings:
                    return GetSettingsSubItemName(subItemIndex);
                case SectionType.Upgrades:
                    return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);
                default:
                    return null;
            }
        }

        private string GetWorkerSubItemName(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex))
                return null;

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                return "Unassign";
            }

            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                return _availableRaces[raceIndex].raceName;
            }
            return null;
        }

        private string GetRecipeSubItemName(int recipeIndex, int subItemIndex)
        {
            if (recipeIndex >= _recipes.Count)
                return null;

            switch (subItemIndex)
            {
                case RECIPE_SUBITEM_STATUS:
                    return "Status";
                case RECIPE_SUBITEM_PRODUCTION:
                    return "Production";
                case RECIPE_SUBITEM_LIMIT:
                    return "Limit";
                default:
                    // Ingredient slots
                    int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
                    return $"Ingredient {slotIndex + 1}";
            }
        }

        private string GetSettingsSubItemName(int subItemIndex)
        {
            if (_campModeNames != null && subItemIndex >= 0 && subItemIndex < _campModeNames.Length)
            {
                return _campModeNames[subItemIndex];
            }
            return null;
        }

        protected override string GetSubSubItemName(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            // Only recipe ingredient slots have sub-sub-items
            if (_sectionTypes[sectionIndex] != SectionType.Recipes)
                return null;

            if (itemIndex >= _recipes.Count)
                return null;

            if (subItemIndex < RECIPE_SUBITEM_INGREDIENTS_START)
                return null;

            int slotIndex = subItemIndex - RECIPE_SUBITEM_INGREDIENTS_START;
            var recipe = _recipes[itemIndex];
            var options = BuildingReflection.GetIngredientSlotOptions(recipe.RecipeState, slotIndex);

            if (subSubItemIndex >= options.Length)
                return null;

            var option = options[subSubItemIndex];
            string name = BuildingReflection.GetIngredientGoodName(option);
            return !string.IsNullOrEmpty(name) ? CleanupName(name) : null;
        }
    }
}
