using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Institution buildings (Tavern, Temple, etc.).
    /// Institutions extend ProductionBuilding and have workers.
    /// Provides Info, Services (recipes), Storage, and Workers sections.
    /// </summary>
    public class InstitutionNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
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
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;
        private bool _canSleep;

        // Service/Recipe data
        private List<ServiceInfo> _services = new List<ServiceInfo>();

        // Storage data
        private List<(string goodName, string displayName, int amount)> _storageGoods = new List<(string, string, int)>();

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;

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
                case SectionType.Info:
                    return GetInfoItemCount();
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

            // Info section has sub-items for Status (pause/resume)
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return GetInfoSubItemCount(itemIndex);
            }

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
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                AnnounceInfoSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                string goodName = BuildingReflection.GetInstitutionAvailableGoodName(_building, service.RecipeIndex, subItemIndex);
                Speech.Say($"Option {subItemIndex + 1}: {goodName ?? "Unknown"}");
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return PerformInfoSubItemAction(itemIndex, subItemIndex);
            }

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

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Institution";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _canSleep = BuildingReflection.CanBuildingSleep(_building);

            RefreshServiceData();
            RefreshStorageData();
            RefreshWorkerData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] InstitutionNavigator: Refreshed data - {_services.Count} services, {_storageGoods.Count} storage goods");
        }

        protected override void ClearData()
        {
            _services.Clear();
            _storageGoods.Clear();
            _workerIds = null;
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

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

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
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
            count++;  // Status
            return count;
        }

        private int GetStatusItemIndex()
        {
            return string.IsNullOrEmpty(_buildingDescription) ? 1 : 2;
        }

        private int GetInfoSubItemCount(int itemIndex)
        {
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
                string status;
                if (!_isFinished)
                {
                    status = "Under construction";
                }
                else if (_isSleeping)
                {
                    status = "Paused";
                }
                else
                {
                    status = "Active";
                }

                Speech.Say($"Status: {status}");
            }
        }

        private void AnnounceInfoSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep && subItemIndex == 0)
            {
                if (_isSleeping)
                {
                    Speech.Say("Resume building");
                }
                else
                {
                    Speech.Say("Pause building, workers will be unassigned");
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
    }
}
