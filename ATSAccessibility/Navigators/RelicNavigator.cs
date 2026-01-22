using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Relic buildings (glade events).
    /// Provides navigation through Info, Status, and Workers sections.
    /// </summary>
    public class RelicNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Status,
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

        // Status data
        private bool _investigationStarted;
        private bool _investigationFinished;
        private float _progress;  // 0-1
        private float _timeLeft;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "RelicNavigator";

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
                case SectionType.Status:
                    return GetStatusItemCount();
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
                case SectionType.Status:
                    AnnounceStatusItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
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
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Relic";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);

            // Status data
            _investigationStarted = BuildingReflection.IsRelicInvestigationStarted(_building);
            _investigationFinished = BuildingReflection.IsRelicInvestigationFinished(_building);
            _progress = BuildingReflection.GetRelicProgress(_building);
            _timeLeft = BuildingReflection.GetRelicTimeLeft(_building);

            // Worker data
            _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            sectionNames.Add("Status");
            sectionTypes.Add(SectionType.Status);

            // Only show workers if there are workplace slots and investigation started
            if (_maxWorkers > 0 && _investigationStarted && !_investigationFinished)
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] RelicNavigator: Refreshed data for {_buildingName}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _buildingDescription = null;
            _workerIds = null;
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            ClearUpgradesSection();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
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
            if (!string.IsNullOrEmpty(_buildingDescription) && itemIndex == index)
            {
                Speech.Say($"Description: {_buildingDescription}");
                return;
            }

            Speech.Say("Unknown item");
        }

        // ========================================
        // STATUS SECTION
        // ========================================

        private int GetStatusItemCount()
        {
            if (_investigationFinished)
                return 1;  // Just "Resolved"
            if (_investigationStarted)
                return 3;  // State, Progress, Time left
            return 1;  // Just "Not started"
        }

        private void AnnounceStatusItem(int itemIndex)
        {
            if (_investigationFinished)
            {
                Speech.Say("Investigation: Resolved");
                return;
            }

            if (!_investigationStarted)
            {
                Speech.Say("Investigation: Not started. Open relic panel to begin");
                return;
            }

            // Investigation in progress
            switch (itemIndex)
            {
                case 0:
                    Speech.Say("Investigation: In progress");
                    break;
                case 1:
                    int percentage = Mathf.RoundToInt(_progress * 100f);
                    Speech.Say($"Progress: {percentage} percent");
                    break;
                case 2:
                    AnnounceTimeLeft();
                    break;
            }
        }

        private void AnnounceTimeLeft()
        {
            int seconds = Mathf.RoundToInt(_timeLeft);
            if (seconds <= 0)
            {
                Speech.Say("Time remaining: Almost done");
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
                Speech.Say($"{raceName}: {freeCount} available");
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
                    _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
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
                    _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
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
