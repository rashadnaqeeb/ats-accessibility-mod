using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for production buildings (Workshop, Farm, Mine, Camp, etc.).
    /// Provides navigation through Info, Workers, and Recipes sections.
    /// Only shows sections that have content.
    /// </summary>
    public class ProductionNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Workers,
            Recipes,
            Inputs,   // Ingredients storage (input goods)
            Outputs,  // Production storage (output goods)
            Settings, // Camp mode settings
            Fields    // Farm field capacity
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;  // Maps index to section type
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
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
                case SectionType.Info:
                    return GetInfoItemCount();
                case SectionType.Workers:
                    return _maxWorkers;
                case SectionType.Recipes:
                    return _recipes.Count;
                case SectionType.Inputs:
                    return _inputGoods.Count > 0 ? _inputGoods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Outputs:
                    return _outputGoods.Count > 0 ? _outputGoods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Settings:
                    return 1;  // Single item showing current mode
                case SectionType.Fields:
                    return 2;  // Sown fields and Plowed fields
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Info section has sub-items for Status (pause/resume)
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return GetInfoSubItemCount(itemIndex);
            }

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
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Recipes:
                    AnnounceRecipeItem(itemIndex);
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
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            // For Camp/gathering buildings, toggle recipe directly (no submenu)
            if (_sectionTypes[sectionIndex] == SectionType.Recipes && _isCamp && itemIndex < _recipes.Count)
            {
                ToggleRecipe(itemIndex);
                return true;
            }

            // For other buildings, recipes have sub-items so Enter enters sub-items
            // (handled by base class)
            return false;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                AnnounceInfoSubItem(itemIndex, subItemIndex);
                return;
            }

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
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return PerformInfoSubItemAction(itemIndex, subItemIndex);
            }

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
            return false;
        }

        protected override void RefreshData()
        {
            // Cache basic info first
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Unknown building";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
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

            // Build sections list dynamically based on what's available
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            // Always have Info section
            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            // Only add Workers if building has worker slots
            if (_maxWorkers > 0)
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

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] ProductionNavigator: Refreshed data for {_buildingName}");
            Debug.Log($"[ATSAccessibility] ProductionNavigator: {_maxWorkers} worker slots, {_recipes.Count} recipes, {_inputGoods.Count} inputs, {_outputGoods.Count} outputs, {_sectionNames.Length} sections");
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

        private int GetStatusItemIndex()
        {
            // Status is after Name and optional Description
            return string.IsNullOrEmpty(_buildingDescription) ? 1 : 2;
        }

        private int GetInfoSubItemCount(int itemIndex)
        {
            // Status item has a sub-item for pause/resume if building supports it
            if (itemIndex == GetStatusItemIndex() && _canSleep)
            {
                return 1;  // Pause/Resume toggle
            }
            return 0;
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
                string announcement = $"Status: {status}";
                if (_canSleep)
                {
                    announcement += _isSleeping ? ", Enter to resume" : ", Enter to pause";
                }
                Speech.Say(announcement);
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

        private void AnnounceInfoSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep && subItemIndex == 0)
            {
                if (_isSleeping)
                {
                    Speech.Say("Resume building, Enter to confirm");
                }
                else
                {
                    Speech.Say("Pause building, workers will be unassigned, Enter to confirm");
                }
            }
        }

        private bool PerformInfoSubItemAction(int itemIndex, int subItemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep && subItemIndex == 0)
            {
                bool wasSleeping = _isSleeping;
                if (BuildingReflection.ToggleBuildingSleep(_building))
                {
                    _isSleeping = !wasSleeping;
                    // Refresh worker data since workers get unassigned on pause
                    if (!wasSleeping)
                    {
                        _workerIds = BuildingReflection.GetWorkerIds(_building);
                    }
                    Speech.Say(_isSleeping ? "Building paused" : "Building resumed");
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

        private string GetStatusText()
        {
            if (!_isFinished) return "Under construction";
            if (_isSleeping) return "Paused";
            return "Active";
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

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers();
            _racesRefreshedForWorkerSection = true;
            Debug.Log($"[ATSAccessibility] ProductionNavigator: Found {_availableRaces.Count} races with free workers");
        }

        private void InvalidateRaceCache()
        {
            _racesRefreshedForWorkerSection = false;
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
                Speech.Say("Unassign worker. Enter to confirm");
                return;
            }

            // Race options
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
                var (raceName, _) = _availableRaces[raceIndex];

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
            Debug.Log($"[ATSAccessibility] ProductionNavigator: RefreshRecipes got {recipeStates.Count} recipes");

            foreach (var recipeState in recipeStates)
            {
                var info = new RecipeInfo
                {
                    RecipeState = recipeState,
                    ModelName = BuildingReflection.GetRecipeModelName(recipeState) ?? "Unknown",
                    ProductName = BuildingReflection.GetRecipeProductName(recipeState),
                    IsActive = BuildingReflection.IsRecipeActive(recipeState),
                    Limit = BuildingReflection.GetRecipeLimit(recipeState),
                    Priority = GetRecipePriority(recipeState)
                };
                _recipes.Add(info);
                Debug.Log($"[ATSAccessibility] ProductionNavigator: Recipe {info.ModelName}, product={info.ProductName}, active={info.IsActive}");
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
            catch { }
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

            Debug.Log($"[ATSAccessibility] ProductionNavigator: RefreshStorage - hasInput={_hasInputStorage}, hasOutput={_hasOutputStorage}, {_inputGoods.Count} inputs, {_outputGoods.Count} outputs");
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
                Speech.Say($"Return {amount} {displayName} to warehouse. Enter to confirm");
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
                        Speech.Say("Transport queued. Enter to cancel");
                    else
                        Speech.Say("Force transport now. Enter to queue");
                    break;
                case 1:
                    if (isConstantForced)
                        Speech.Say("Auto-deliver when produced: On. Enter to turn off");
                    else
                        Speech.Say("Auto-deliver when produced: Off. Enter to turn on");
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

            // In the game, limit 0 means "no limit" (unlimited)
            // limit > 0 means a specific production cap
            string limitText = "";
            if (recipe.Limit > 0)
            {
                limitText = $", limit {recipe.Limit}";
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

                string displayName = GetRecipeDisplayName(recipe);
                Speech.Say($"{displayName}: {(newActive ? "enabled" : "disabled")}");
                Debug.Log($"[ATSAccessibility] ProductionNavigator: Toggled recipe {recipe.ModelName} to {newActive}");
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
                Speech.Say($"{modeName}. Enter to select");
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
                    string limitText = limit > 0 ? limit.ToString() : "unlimited";
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

            // If there are multiple options, mention that Enter opens the options
            if (options.Length > 1)
            {
                announcement += ". Enter to edit options";
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

            // Update cached value
            var updatedRecipe = recipe;
            updatedRecipe.Limit = newLimit;
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
    }
}
