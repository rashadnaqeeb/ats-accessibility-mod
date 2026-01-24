using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for the main Storage building (warehouse).
    /// Provides navigation through Info, Goods, and Workers sections.
    /// Storage buildings extend ProductionBuilding so they have workers.
    /// </summary>
    public class StorageNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Goods,
            Workers,
            Abilities,
            Upgrades
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

        // Goods data (from global storage)
        private List<(string goodName, string displayName, int amount)> _goods = new List<(string, string, int)>();

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // Abilities data
        private int _abilityCount;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "StorageNavigator";

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
                case SectionType.Goods:
                    return _goods.Count > 0 ? _goods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Workers:
                    return _maxWorkers;
                case SectionType.Abilities:
                    return _abilityCount > 0 ? _abilityCount : 1;  // At least 1 for "No abilities" message
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

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return GetWorkerSubItemCount(itemIndex);
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
                case SectionType.Goods:
                    AnnounceGoodItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Abilities:
                    AnnounceAbilityItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Storage";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            RefreshGoodsData();
            RefreshWorkerData();
            RefreshAbilityData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] StorageNavigator: Refreshed data - {_goods.Count} goods, {_maxWorkers} worker slots, {_abilityCount} abilities");
        }

        protected override void ClearData()
        {
            _goods.Clear();
            _workerIds = null;
            _sectionNames = null;
            _sectionTypes = null;
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            ClearUpgradesSection();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshGoodsData()
        {
            _goods.Clear();

            // Get goods from the global storage
            var storageGoods = GameReflection.GetStorageGoods();
            foreach (var kvp in storageGoods)
            {
                string displayName = GameReflection.GetGoodDisplayName(kvp.Key) ?? kvp.Key;
                _goods.Add((kvp.Key, displayName, kvp.Value));
            }

            // Sort by display name for easier navigation
            _goods.Sort((a, b) => string.Compare(a.displayName, b.displayName));
        }

        private void RefreshWorkerData()
        {
            _workerIds = BuildingReflection.GetWorkerIds(_building);
            _maxWorkers = BuildingReflection.GetMaxWorkers(_building);
        }

        private void RefreshAbilityData()
        {
            _abilityCount = BuildingReflection.GetCycleAbilityCount();
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

            // Goods section
            sections.Add("Goods");
            types.Add(SectionType.Goods);

            // Abilities section (only if abilities exist)
            if (_abilityCount > 0)
            {
                sections.Add("Abilities");
                types.Add(SectionType.Abilities);
            }

            // Workers section (only if building currently accepts worker assignment)
            if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
            }

            // Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sections.Add("Upgrades");
                types.Add(SectionType.Upgrades);
            }

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
            count++;  // Status
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

            // Description (if present)
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
                if (!_isFinished)
                {
                    Speech.Say("Status: Under construction");
                }
                else if (_isSleeping)
                {
                    Speech.Say("Status: Paused");
                }
                else
                {
                    Speech.Say("Status: Active");
                }
            }
        }

        // ========================================
        // GOODS SECTION
        // ========================================

        private void AnnounceGoodItem(int itemIndex)
        {
            if (_goods.Count == 0)
            {
                Speech.Say("Storage is empty");
                return;
            }

            if (itemIndex < _goods.Count)
            {
                var good = _goods[itemIndex];
                Speech.Say($"{good.displayName}: {good.amount}");
            }
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
            if (itemIndex >= _maxWorkers) return;

            bool isEmpty = BuildingReflection.IsWorkerSlotEmpty(_building, itemIndex);

            if (isEmpty)
            {
                Speech.Say($"Slot {itemIndex + 1} of {_maxWorkers}: Empty");
            }
            else
            {
                int workerId = _workerIds[itemIndex];
                string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
                Speech.Say($"Slot {itemIndex + 1} of {_maxWorkers}: {workerDesc ?? "Assigned"}");
            }
        }

        // ========================================
        // WORKER SUB-ITEMS (ASSIGNMENT)
        // ========================================

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers();
            _racesRefreshedForWorkerSection = true;
            Debug.Log($"[ATSAccessibility] StorageNavigator: Found {_availableRaces.Count} races with free workers");
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
        // ABILITIES SECTION
        // ========================================

        private void AnnounceAbilityItem(int itemIndex)
        {
            if (_abilityCount == 0)
            {
                Speech.Say("No abilities available");
                return;
            }

            if (itemIndex >= _abilityCount) return;

            string abilityName = BuildingReflection.GetCycleAbilityName(itemIndex) ?? "Unknown ability";
            int charges = BuildingReflection.GetCycleAbilityCharges(itemIndex);

            if (charges > 0)
            {
                Speech.Say($"{abilityName}: {charges} charges");
            }
            else
            {
                Speech.Say($"{abilityName}: No charges remaining");
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Abilities)
            {
                return UseAbility(itemIndex);
            }

            return false;
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        private bool UseAbility(int abilityIndex)
        {
            if (abilityIndex >= _abilityCount) return false;

            int charges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
            if (charges <= 0)
            {
                Speech.Say("No charges remaining");
                return true;  // Still handled the action
            }

            string abilityName = BuildingReflection.GetCycleAbilityName(abilityIndex) ?? "ability";

            if (BuildingReflection.UseCycleAbility(abilityIndex))
            {
                int newCharges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
                Speech.Say($"Used {abilityName}. {newCharges} charges remaining");

                // Refresh ability data in case charges changed
                RefreshAbilityData();
                return true;
            }
            else
            {
                Speech.Say($"Cannot use {abilityName}");
                return true;
            }
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
                    return GetInfoItemName(itemIndex);
                case SectionType.Goods:
                    return itemIndex < _goods.Count ? _goods[itemIndex].displayName : null;
                case SectionType.Workers:
                    return GetWorkerItemName(itemIndex);
                case SectionType.Abilities:
                    return itemIndex < _abilityCount ? BuildingReflection.GetCycleAbilityName(itemIndex) : null;
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        private string GetInfoItemName(int itemIndex)
        {
            int index = 0;
            if (itemIndex == index) return "Name";
            index++;
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index) return "Description";
                index++;
            }
            if (itemIndex == index) return "Status";
            return null;
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

            // Upgrades have sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);

            // Only workers have sub-items
            if (_sectionTypes[sectionIndex] != SectionType.Workers)
                return null;

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
    }
}
