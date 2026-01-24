using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Hearth buildings (Ancient Hearth, Small Hearth).
    /// Provides navigation through Info, Fire, Sacrifice, Upgrades, Blight, and Workers sections.
    /// </summary>
    public class HearthNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Fire,
            Sacrifice,
            Upgrades,
            Blight,
            Workers
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private bool _isFinished;
        private bool _isSleeping;
        private bool _isMainHearth;

        // Fire data
        private float _fuelLevel;  // 0-1
        private float _fuelTimeRemaining;
        private bool _isFireLow;
        private bool _isFireOut;

        // Upgrades data
        private List<BuildingReflection.HearthUpgradeInfo> _upgradeInfo = new List<BuildingReflection.HearthUpgradeInfo>();

        // Blight data
        private float _corruptionRate;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // Sacrifice data
        private List<object> _sacrificeRecipes = new List<object>();
        private List<BuildingReflection.SacrificeRecipeInfo> _sacrificeInfo = new List<BuildingReflection.SacrificeRecipeInfo>();

        // Fuel data
        private List<BuildingReflection.FuelInfo> _fuelTypes = new List<BuildingReflection.FuelInfo>();

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "HearthNavigator";

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
                case SectionType.Fire:
                    return GetFireItemCount();
                case SectionType.Sacrifice:
                    return _sacrificeRecipes.Count;
                case SectionType.Upgrades:
                    return _upgradeInfo.Count;
                case SectionType.Blight:
                    return 1;  // Just corruption level
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

            // Fire section: Fuel types item (index 2) has sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return _fuelTypes.Count;
            }

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return GetWorkerSubItemCount(itemIndex);
            }

            // Sacrifice uses +/- keys, no sub-items
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
                case SectionType.Fire:
                    AnnounceFireItem(itemIndex);
                    break;
                case SectionType.Sacrifice:
                    AnnounceSacrificeItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    AnnounceUpgradeItem(itemIndex);
                    break;
                case SectionType.Blight:
                    AnnounceBlightItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
            }
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            // Fire section: Fuel types sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                AnnounceFuelSubItem(subItemIndex);
                return;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            // Fire section: Toggle fuel type
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return ToggleFuel(subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            // Sacrifice section uses +/- to adjust level
            if (_sectionTypes[sectionIndex] == SectionType.Sacrifice)
            {
                AdjustSacrificeLevel(itemIndex, delta);
            }
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Hearth";
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _isMainHearth = BuildingReflection.IsMainHearth(_building);

            // Fire data
            _fuelLevel = BuildingReflection.GetHearthFireLevel(_building);
            _fuelTimeRemaining = BuildingReflection.GetHearthFuelTimeRemaining(_building);
            _isFireLow = BuildingReflection.IsHearthFireLow(_building);
            _isFireOut = BuildingReflection.IsHearthFireOut(_building);

            // Upgrades data
            _upgradeInfo = BuildingReflection.GetHearthUpgradeInfo(_building);

            // Blight data
            _corruptionRate = BuildingReflection.GetHearthCorruptionRate(_building);

            // Worker data
            _workerIds = BuildingReflection.GetHearthWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

            // Sacrifice data
            _sacrificeRecipes = BuildingReflection.GetHearthSacrificeRecipes(_building);
            RefreshSacrificeInfo();

            // Fuel data
            _fuelTypes = BuildingReflection.GetAllFuelTypes();

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            sectionNames.Add("Fire");
            sectionTypes.Add(SectionType.Fire);

            // Sacrifice section only shown if there are sacrifice recipes
            if (_sacrificeRecipes.Count > 0)
            {
                sectionNames.Add("Sacrifice");
                sectionTypes.Add(SectionType.Sacrifice);
            }

            // Upgrades section only shown if there are upgrade tiers
            if (_upgradeInfo.Count > 0)
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            // Blight section only shown for main hearth when blight is active
            if (_isMainHearth && GameReflection.IsBlightActive())
            {
                sectionNames.Add("Blight");
                sectionTypes.Add(SectionType.Blight);
            }

            if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] HearthNavigator: Refreshed data for {_buildingName}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _workerIds = null;
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            _sacrificeRecipes.Clear();
            _sacrificeInfo.Clear();
            _fuelTypes.Clear();
            _upgradeInfo.Clear();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            return 2;  // Name, Status
        }

        private void AnnounceInfoItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    string hearthType = _isMainHearth ? "Ancient Hearth" : "Small Hearth";
                    Speech.Say($"Name: {_buildingName} ({hearthType})");
                    break;
                case 1:
                    Speech.Say($"Status: {GetStatusText()}");
                    break;
            }
        }

        private string GetStatusText()
        {
            if (!_isFinished) return "Under construction";
            if (_isSleeping) return "Paused";
            if (_isFireOut) return "Fire out";
            if (_isFireLow) return "Fire low";
            return "Active";
        }

        // ========================================
        // FIRE SECTION
        // ========================================

        private int GetFireItemCount()
        {
            return 3;  // Fuel level, Time remaining, Fuel types
        }

        private void AnnounceFireItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    int percentage = Mathf.RoundToInt(_fuelLevel * 100f);
                    Speech.Say($"Fuel level: {percentage} percent");
                    break;
                case 1:
                    int seconds = Mathf.RoundToInt(_fuelTimeRemaining);
                    if (seconds <= 0)
                    {
                        Speech.Say("Time remaining: Fire is out");
                    }
                    else if (seconds < 60)
                    {
                        Speech.Say($"Time remaining: {seconds} seconds");
                    }
                    else
                    {
                        int minutes = seconds / 60;
                        int remainingSecs = seconds % 60;
                        if (remainingSecs > 0)
                            Speech.Say($"Time remaining: {minutes} minutes {remainingSecs} seconds");
                        else
                            Speech.Say($"Time remaining: {minutes} minutes");
                    }
                    break;
                case 2:
                    // Fuel types submenu
                    int enabledCount = 0;
                    foreach (var fuel in _fuelTypes)
                    {
                        if (fuel.isEnabled) enabledCount++;
                    }
                    Speech.Say($"Fuel types: {enabledCount} of {_fuelTypes.Count} enabled");
                    break;
            }
        }

        // ========================================
        // UPGRADES SECTION
        // ========================================

        private void AnnounceUpgradeItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count)
            {
                Speech.Say("Invalid upgrade");
                return;
            }

            // Refresh upgrade info to get current state
            _upgradeInfo = BuildingReflection.GetHearthUpgradeInfo(_building);
            if (itemIndex >= _upgradeInfo.Count)
            {
                Speech.Say("Invalid upgrade");
                return;
            }

            var info = _upgradeInfo[itemIndex];

            // Build status string
            string status = info.isAchieved ? "Achieved" : "Available";

            // Build requirements string
            var reqParts = new List<string>();

            // Housed population
            if (info.minPopulation > 0)
            {
                reqParts.Add($"Housed population {info.currentPopulation} of {info.minPopulation}");
            }

            // Institutions
            if (info.minInstitutions > 0)
            {
                reqParts.Add($"Institutions {info.currentInstitutions} of {info.minInstitutions}");
            }

            // Decorations (tier name already includes "decorations" suffix)
            foreach (var decorReq in info.decorationRequirements)
            {
                reqParts.Add($"{decorReq.tierName} {decorReq.current} of {decorReq.required}");
            }

            string requirements = reqParts.Count > 0 ? string.Join(", ", reqParts) : "None";

            // Build announcement
            string announcement = $"{info.displayName}: {status}. Requirements: {requirements}";

            // Add effect
            if (!string.IsNullOrEmpty(info.effectDescription))
            {
                announcement += $". Effect: {info.effectDescription}";
            }

            Speech.Say(announcement);
        }

        private string GetUpgradeItemName(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count)
                return null;

            return _upgradeInfo[itemIndex].displayName;
        }

        // ========================================
        // BLIGHT SECTION
        // ========================================

        private void AnnounceBlightItem(int itemIndex)
        {
            int percentage = Mathf.RoundToInt(_corruptionRate * 100f);
            if (percentage <= 0)
                Speech.Say("Corruption: None");
            else
                Speech.Say($"Corruption: {percentage} percent");
        }

        // ========================================
        // WORKERS SECTION
        // ========================================

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers();
            _racesRefreshedForWorkerSection = true;
        }

        private bool IsValidWorkerIndex(int workerIndex)
        {
            return _workerIds != null && workerIndex >= 0 && workerIndex < _workerIds.Length;
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
                Speech.Say("Unassign worker");
                return;
            }

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
                    _workerIds = BuildingReflection.GetHearthWorkerIds(_building);
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
                    _workerIds = BuildingReflection.GetHearthWorkerIds(_building);
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

        // ========================================
        // SACRIFICE SECTION
        // ========================================

        private void RefreshSacrificeInfo()
        {
            _sacrificeInfo.Clear();
            foreach (var recipe in _sacrificeRecipes)
            {
                var info = BuildingReflection.GetSacrificeRecipeInfo(_building, recipe);
                _sacrificeInfo.Add(info);
            }
        }

        private void AnnounceSacrificeItem(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count)
            {
                Speech.Say("Invalid sacrifice recipe");
                return;
            }

            // Refresh the info for this recipe to get current state
            if (recipeIndex < _sacrificeRecipes.Count)
            {
                _sacrificeInfo[recipeIndex] = BuildingReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
            }

            var info = _sacrificeInfo[recipeIndex];

            // Use good name as primary identifier
            string name = info.goodName;
            if (string.IsNullOrEmpty(name) || name == "Unknown")
            {
                name = info.recipeName;
            }

            // Get effect description
            string effect = info.effectDescription;
            if (string.IsNullOrEmpty(effect))
            {
                effect = info.effectName;
            }
            if (!string.IsNullOrEmpty(effect))
            {
                effect = effect + " per level";
            }

            if (info.level > 0)
            {
                // Active: "{Good}: Level X, {total consumption} per minute, {effect} per level"
                float totalConsumption = info.consumptionPerMin * info.level;
                int consumptionRounded = Mathf.RoundToInt(totalConsumption);
                Speech.Say($"{name}: Level {info.level}, {consumptionRounded} per minute, {effect}");
            }
            else
            {
                // Off: "{Good}: Off, {effect} per level"
                Speech.Say($"{name}: Off, {effect}");
            }
        }

        private void AdjustSacrificeLevel(int recipeIndex, int delta)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeRecipes.Count)
                return;

            // Refresh info to get current state
            _sacrificeInfo[recipeIndex] = BuildingReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
            var info = _sacrificeInfo[recipeIndex];
            var recipeState = _sacrificeRecipes[recipeIndex];

            int currentLevel = info.level;
            int newLevel = currentLevel + delta;

            // Clamp to valid range (0 to maxLevel)
            if (newLevel < 0) newLevel = 0;
            if (newLevel > info.maxLevel) newLevel = info.maxLevel;

            // No change needed
            if (newLevel == currentLevel)
            {
                if (delta > 0 && currentLevel == info.maxLevel)
                {
                    Speech.Say("Maximum level");
                }
                else if (delta < 0 && currentLevel == 0)
                {
                    Speech.Say("Already off");
                }
                return;
            }

            // Check if can afford when increasing from 0
            if (currentLevel == 0 && newLevel > 0 && !info.canAfford)
            {
                SoundManager.PlayFailed();
                Speech.Say($"Not enough {info.goodName}");
                return;
            }

            // Apply the change
            if (BuildingReflection.SetHearthSacrificeLevel(_building, recipeState, newLevel))
            {
                if (newLevel == 0)
                {
                    SoundManager.PlayButtonClick();
                    Speech.Say("Off");
                }
                else if (currentLevel == 0)
                {
                    // Enabling from off
                    SoundManager.PlayBuildingFireButtonStart();
                    Speech.Say($"Level {newLevel}");
                }
                else
                {
                    SoundManager.PlayButtonClick();
                    Speech.Say($"Level {newLevel}");
                }
                RefreshSacrificeInfo();
            }
        }

        private string GetSacrificeItemName(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count)
                return null;

            var info = _sacrificeInfo[recipeIndex];
            // Use good name for search
            if (!string.IsNullOrEmpty(info.goodName) && info.goodName != "Unknown")
            {
                return info.goodName;
            }
            return info.recipeName;
        }

        // ========================================
        // FUEL SUB-ITEMS (inside Fire section)
        // ========================================

        private void AnnounceFuelSubItem(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
            {
                Speech.Say("Invalid fuel type");
                return;
            }

            // Refresh the fuel state
            _fuelTypes = BuildingReflection.GetAllFuelTypes();

            var fuel = _fuelTypes[subItemIndex];
            string status = fuel.isEnabled ? "Enabled" : "Disabled";
            Speech.Say($"{fuel.displayName}: {status}");
        }

        private bool ToggleFuel(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
                return false;

            var fuel = _fuelTypes[subItemIndex];
            bool newState = !fuel.isEnabled;

            if (BuildingReflection.SetFuelEnabled(fuel.name, newState))
            {
                SoundManager.PlayButtonClick();
                Speech.Say(newState ? "Enabled" : "Disabled");
                _fuelTypes = BuildingReflection.GetAllFuelTypes();
                return true;
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Cannot change fuel setting");
                return false;
            }
        }

        private string GetFuelSubItemName(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
                return null;

            return _fuelTypes[subItemIndex].displayName;
        }

        // ========================================
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
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
                case SectionType.Info:
                    return itemIndex == 0 ? "Name" : "Status";
                case SectionType.Fire:
                    switch (itemIndex)
                    {
                        case 0: return "Fuel";
                        case 1: return "Time";
                        case 2: return "Fuel types";
                        default: return null;
                    }
                case SectionType.Sacrifice:
                    return GetSacrificeItemName(itemIndex);
                case SectionType.Upgrades:
                    return GetUpgradeItemName(itemIndex);
                case SectionType.Blight:
                    return "Corruption";
                case SectionType.Workers:
                    return GetWorkerItemName(itemIndex);
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

            string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
            return !string.IsNullOrEmpty(workerDesc) ? workerDesc : $"Slot {itemIndex + 1}";
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            // Fire section: Fuel types sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return GetFuelSubItemName(subItemIndex);
            }

            // Workers have sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                if (!IsValidWorkerIndex(itemIndex))
                    return null;

                bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, itemIndex);
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

            return null;
        }
    }
}
