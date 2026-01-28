using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Institution buildings (Tavern, Temple, etc.).
    /// Institutions extend ProductionBuilding and have workers.
    /// Provides Status (toggle), Services (recipes), Storage, and Workers sections.
    /// </summary>
    public class InstitutionNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Status,
            Effects,
            Services,
            Storage,
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

        // Service/Recipe data
        private List<ServiceInfo> _services = new List<ServiceInfo>();

        // Storage data
        private List<(string goodName, string displayName, int amount)> _storageGoods = new List<(string, string, int)>();

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // Effects data
        private int _effectCount;

        // ========================================
        // SERVICE INFO STRUCT
        // ========================================

        private struct ServiceInfo
        {
            public int RecipeIndex;
            public string ServedNeedName;
            public bool ConsumesGood;
            public string CurrentGoodName;
            public int AvailableGoodsCount;
        }

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "InstitutionNavigator";

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
                    return 0;
                case SectionType.Effects:
                    return _effectCount;
                case SectionType.Services:
                    return _services.Count > 0 ? _services.Count : 1;
                case SectionType.Storage:
                    return _storageGoods.Count > 0 ? _storageGoods.Count : 1;
                case SectionType.Workers:
                    return _maxWorkers;
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

            // Services have sub-items (available ingredients to choose from)
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                // Only if it consumes goods and has multiple options
                if (service.ConsumesGood && service.AvailableGoodsCount > 1)
                {
                    return service.AvailableGoodsCount;
                }
            }

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
                case SectionType.Effects:
                    AnnounceEffectItem(itemIndex);
                    break;
                case SectionType.Services:
                    AnnounceServiceItem(itemIndex);
                    break;
                case SectionType.Storage:
                    AnnounceStorageItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
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
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                string goodName = BuildingReflection.GetInstitutionAvailableGoodName(_building, service.RecipeIndex, subItemIndex);
                Speech.Say($"Option {subItemIndex + 1}: {goodName ?? "Unknown"}");
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
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
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                if (BuildingReflection.ChangeInstitutionIngredient(_building, service.RecipeIndex, subItemIndex))
                {
                    string goodName = BuildingReflection.GetInstitutionAvailableGoodName(_building, service.RecipeIndex, subItemIndex);
                    Speech.Say($"Changed to {goodName ?? "Unknown"}");
                    RefreshServiceData();  // Refresh to update current good
                    return true;
                }
            }

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

        protected override bool PerformSectionAction(int sectionIndex)
        {
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
                        // Workers were unassigned when pausing
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
                    SoundManager.PlayFailed();
                    Speech.Say("Cannot change building state");
                    return false;
                }
            }

            return false;
        }

        protected override void RefreshData()
        {
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _canSleep = BuildingReflection.CanBuildingSleep(_building);
            _effectCount = BuildingReflection.GetInstitutionEffectCount(_building);

            RefreshServiceData();
            RefreshStorageData();
            RefreshWorkerData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] InstitutionNavigator: Refreshed data - {_effectCount} effects, {_services.Count} services, {_storageGoods.Count} storage goods");
        }

        protected override void ClearData()
        {
            _services.Clear();
            _storageGoods.Clear();
            _workerIds = null;
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            _sectionNames = null;
            _sectionTypes = null;
            ClearUpgradesSection();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshServiceData()
        {
            _services.Clear();

            int recipeCount = BuildingReflection.GetInstitutionRecipeCount(_building);
            for (int i = 0; i < recipeCount; i++)
            {
                var service = new ServiceInfo
                {
                    RecipeIndex = i,
                    ServedNeedName = BuildingReflection.GetInstitutionServedNeedName(_building, i),
                    ConsumesGood = BuildingReflection.IsInstitutionRecipeGoodConsumed(_building, i),
                    CurrentGoodName = BuildingReflection.GetInstitutionCurrentGoodName(_building, i),
                    AvailableGoodsCount = BuildingReflection.GetInstitutionAvailableGoodsCount(_building, i)
                };
                _services.Add(service);
            }
        }

        private void RefreshStorageData()
        {
            _storageGoods.Clear();

            var storageGoods = BuildingReflection.GetInstitutionStorageGoods(_building);
            foreach (var kvp in storageGoods)
            {
                string displayName = GameReflection.GetGoodDisplayName(kvp.Key) ?? kvp.Key;
                _storageGoods.Add((kvp.Key, displayName, kvp.Value));
            }

            // Sort by display name
            _storageGoods.Sort((a, b) => string.Compare(a.displayName, b.displayName));
        }

        private void RefreshWorkerData()
        {
            _workerIds = BuildingReflection.GetWorkerIds(_building);
            _maxWorkers = BuildingReflection.GetMaxWorkers(_building);
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Status
            sections.Add("Status");
            types.Add(SectionType.Status);

            // Effects section (only if institution has effects)
            if (_effectCount > 0)
            {
                sections.Add("Effects");
                types.Add(SectionType.Effects);
            }

            // Services section
            sections.Add("Services");
            types.Add(SectionType.Services);

            // Storage section
            sections.Add("Storage");
            types.Add(SectionType.Storage);

            // Workers section (only if building currently accepts worker assignment)
            if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sections.Add("Upgrades");
                types.Add(SectionType.Upgrades);
            }

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // EFFECTS SECTION
        // ========================================

        private void AnnounceEffectItem(int itemIndex)
        {
            if (itemIndex >= _effectCount) return;

            string name = BuildingReflection.GetInstitutionEffectName(_building, itemIndex);
            int minWorkers = BuildingReflection.GetInstitutionEffectMinWorkers(_building, itemIndex);
            bool isActive = BuildingReflection.IsInstitutionEffectActive(_building, itemIndex);
            string description = BuildingReflection.GetInstitutionEffectDescription(_building, itemIndex);

            string activation = isActive ? "active" : $"requires {minWorkers} workers";

            if (!string.IsNullOrEmpty(description))
                Speech.Say($"{name}, {activation}, {description}");
            else
                Speech.Say($"{name}, {activation}");
        }

        // ========================================
        // SERVICES SECTION
        // ========================================

        private void AnnounceServiceItem(int itemIndex)
        {
            if (_services.Count == 0)
            {
                Speech.Say("No services available");
                return;
            }

            if (itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                string needName = service.ServedNeedName ?? "Unknown need";

                if (service.ConsumesGood)
                {
                    string goodName = service.CurrentGoodName ?? "Unknown";
                    Speech.Say($"{needName}: using {goodName}");
                }
                else
                {
                    Speech.Say($"{needName}: Free service (no consumption)");
                }
            }
        }

        // ========================================
        // STORAGE SECTION
        // ========================================

        private void AnnounceStorageItem(int itemIndex)
        {
            // Refresh storage data to get current amounts
            RefreshStorageData();

            if (_storageGoods.Count == 0)
            {
                Speech.Say("Storage is empty");
                return;
            }

            if (itemIndex < _storageGoods.Count)
            {
                var good = _storageGoods[itemIndex];
                Speech.Say($"{good.displayName}: {good.amount}");
            }
        }

        // ========================================
        // WORKERS SECTION
        // ========================================

        private void AnnounceWorkerItem(int itemIndex)
        {
            // Refresh races on each worker announcement to catch changes during panel session
            RefreshAvailableRaces(force: true);

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

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers(includeZeroFree: true);
            _racesRefreshedForWorkerSection = true;
        }

        private bool IsValidWorkerIndex(int index) =>
            index >= 0 && index < _maxWorkers && _workerIds != null;

        private int GetWorkerSubItemCount(int workerIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return 0;

            // Refresh available races when entering worker submenu
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

            // First option is "Unassign" if slot is occupied
            if (slotOccupied && subItemIndex == 0)
            {
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
                    if (bonus.Contains(","))
                    {
                        Speech.Say($"{raceName}: {freeCount} available, {bonus}");
                    }
                    else
                    {
                        Speech.Say($"{raceName}: {freeCount} available, specialist: {bonus}");
                    }
                }
                else
                {
                    Speech.Say($"{raceName}: {freeCount} available");
                }
            }
        }

        private bool PerformWorkerSubItemAction(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex))
            {
                Speech.Say("Invalid worker slot");
                return false;
            }

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            // "Unassign" option
            if (slotOccupied && subItemIndex == 0)
            {
                if (BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex))
                {
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
                    SoundManager.PlayFailed();
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

                // Assign worker of the selected race
                if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName))
                {
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
                        Speech.Say($"{raceName} assigned");
                    }

                    // Exit submenu back to worker slot level
                    _navigationLevel = 1;
                    return true;
                }
                else
                {
                    Speech.Say($"Failed to assign {raceName}");
                    SoundManager.PlayFailed();
                    return false;
                }
            }

            return false;
        }
    }
}
