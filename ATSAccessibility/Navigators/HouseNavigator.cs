using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for House buildings (Shelters).
    /// Provides navigation through Info, Residents, and Capacity sections.
    /// </summary>
    public class HouseNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Residents,
            Capacity,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private bool _isFinished;

        // Residents data
        private List<int> _residentIds = new List<int>();
        private int _currentCapacity;
        private int _maxCapacity;
        private bool _isFull;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "HouseNavigator";

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
                    return 2;  // Name, Status
                case SectionType.Residents:
                    return _residentIds.Count > 0 ? _residentIds.Count : 1;  // At least "Empty" message
                case SectionType.Capacity:
                    return 2;  // Occupancy, Available
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemCount();
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
                case SectionType.Residents:
                    AnnounceResidentItem(itemIndex);
                    break;
                case SectionType.Capacity:
                    AnnounceCapacityItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemCount(itemIndex);

            return 0;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "House";
            _isFinished = BuildingReflection.IsBuildingFinished(_building);

            // Resident data
            _residentIds = BuildingReflection.GetHouseResidents(_building);
            _currentCapacity = BuildingReflection.GetHouseCapacity(_building);
            _maxCapacity = BuildingReflection.GetHouseMaxCapacity(_building);
            _isFull = BuildingReflection.IsHouseFull(_building);

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            sectionNames.Add("Residents");
            sectionTypes.Add(SectionType.Residents);

            sectionNames.Add("Capacity");
            sectionTypes.Add(SectionType.Capacity);

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] HouseNavigator: Refreshed data for {_buildingName}, {_residentIds.Count} residents");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _residentIds.Clear();
            ClearUpgradesSection();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private void AnnounceInfoItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    Speech.Say($"Name: {_buildingName}");
                    break;
                case 1:
                    Speech.Say($"Status: {GetStatusText()}");
                    break;
            }
        }

        private string GetStatusText()
        {
            if (!_isFinished) return "Under construction";
            if (_isFull) return "Full";
            if (_residentIds.Count > 0) return "Occupied";
            return "Empty";
        }

        // ========================================
        // RESIDENTS SECTION
        // ========================================

        private void AnnounceResidentItem(int itemIndex)
        {
            if (_residentIds.Count == 0)
            {
                Speech.Say("No residents");
                return;
            }

            if (itemIndex >= _residentIds.Count)
            {
                Speech.Say("Invalid resident");
                return;
            }

            int residentId = _residentIds[itemIndex];
            var actor = BuildingReflection.GetActor(residentId);

            if (actor != null)
            {
                string name = BuildingReflection.GetActorName(actor) ?? "Unknown";
                string race = BuildingReflection.GetActorRace(actor);

                if (!string.IsNullOrEmpty(race))
                    Speech.Say($"{itemIndex + 1}. {name}, {race}");
                else
                    Speech.Say($"{itemIndex + 1}. {name}");
            }
            else
            {
                Speech.Say($"{itemIndex + 1}. Unknown villager");
            }
        }

        // ========================================
        // CAPACITY SECTION
        // ========================================

        private void AnnounceCapacityItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    Speech.Say($"Occupancy: {_residentIds.Count} of {_currentCapacity}");
                    break;
                case 1:
                    int available = _currentCapacity - _residentIds.Count;
                    if (available <= 0)
                        Speech.Say("Available: None (full)");
                    else
                        Speech.Say($"Available: {available} spaces");
                    break;
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
                    return itemIndex == 0 ? "Name" : "Status";
                case SectionType.Residents:
                    return GetResidentItemName(itemIndex);
                case SectionType.Capacity:
                    return itemIndex == 0 ? "Occupancy" : "Available";
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);

            return null;
        }

        private string GetResidentItemName(int itemIndex)
        {
            if (_residentIds.Count == 0)
                return null;

            if (itemIndex >= _residentIds.Count)
                return null;

            int residentId = _residentIds[itemIndex];
            var actor = BuildingReflection.GetActor(residentId);

            if (actor != null)
            {
                string name = BuildingReflection.GetActorName(actor) ?? "Unknown";
                return name;
            }
            return null;
        }
    }
}
