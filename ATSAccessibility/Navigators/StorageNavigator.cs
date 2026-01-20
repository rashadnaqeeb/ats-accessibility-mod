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
            Abilities
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
                default:
                    return 0;
            }
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
            }
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

            // Workers section (only if building has workplaces)
            if (_maxWorkers > 0)
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
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
    }
}
