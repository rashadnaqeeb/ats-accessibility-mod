using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Hearth buildings (Ancient Hearth, Small Hearth).
    /// Provides navigation through Info, Fire, Hub, Blight, and Workers sections.
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
            Hub,
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

        // Hub data
        private int _hubIndex;
        private float _hubRange;

        // Blight data
        private float _corruptionRate;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

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
                case SectionType.Hub:
                    return GetHubItemCount();
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
                case SectionType.Fire:
                    AnnounceFireItem(itemIndex);
                    break;
                case SectionType.Hub:
                    AnnounceHubItem(itemIndex);
                    break;
                case SectionType.Blight:
                    AnnounceBlightItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }
            return false;
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

            // Hub data
            _hubIndex = BuildingReflection.GetHearthHubIndex(_building);
            _hubRange = BuildingReflection.GetHearthRange(_building);

            // Blight data
            _corruptionRate = BuildingReflection.GetHearthCorruptionRate(_building);

            // Worker data
            _workerIds = BuildingReflection.GetHearthWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            sectionNames.Add("Fire");
            sectionTypes.Add(SectionType.Fire);

            sectionNames.Add("Hub");
            sectionTypes.Add(SectionType.Hub);

            // Blight section only shown for main hearth when blight is active
            if (_isMainHearth && GameReflection.IsBlightActive())
            {
                sectionNames.Add("Blight");
                sectionTypes.Add(SectionType.Blight);
            }

            if (_maxWorkers > 0)
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
            return 3;  // Fuel level, Time remaining, Warning status
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
                    if (_isFireOut)
                        Speech.Say("Warning: Fire is out. Villagers cannot warm themselves");
                    else if (_isFireLow)
                        Speech.Say("Warning: Fire is low. Add fuel soon");
                    else
                        Speech.Say("No warnings");
                    break;
            }
        }

        // ========================================
        // HUB SECTION
        // ========================================

        private int GetHubItemCount()
        {
            return 2;  // Service status, Range
        }

        private void AnnounceHubItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    if (_hubIndex < 0)
                        Speech.Say("Hub service: None active");
                    else
                        Speech.Say($"Hub service: Service {_hubIndex + 1} active");
                    break;
                case 1:
                    Speech.Say($"Range: {_hubRange:F1} tiles");
                    break;
            }
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
                        case 2: return "Warning";
                        default: return null;
                    }
                case SectionType.Hub:
                    return itemIndex == 0 ? "Service" : "Range";
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
