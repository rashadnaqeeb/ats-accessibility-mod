using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Shared handler for building worker section.
    /// Any building navigator can use this to handle the Workers section.
    /// Provides slot listing, race-based assignment, sub-item navigation, and refresh logic.
    /// </summary>
    public class BuildingWorkerSection
    {
        // ========================================
        // CONFIGURATION
        // ========================================

        /// <summary>
        /// Function to fetch worker IDs from a building.
        /// Defaults to BuildingReflection.GetWorkerIds.
        /// Override for Hearth (GetHearthWorkerIds) or Relic (GetRelicWorkerIds).
        /// </summary>
        public Func<object, int[]> GetWorkerIdsFunc { get; set; }

        // ========================================
        // STATE
        // ========================================

        private object _building;
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshed;

        // ========================================
        // PUBLIC PROPERTIES
        // ========================================

        public int MaxWorkers => _maxWorkers;
        public int[] WorkerIds => _workerIds;

        // ========================================
        // CONSTRUCTOR
        // ========================================

        public BuildingWorkerSection()
        {
            GetWorkerIdsFunc = BuildingReflection.GetWorkerIds;
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Initialize the section with a building. Fetches worker IDs and available races.
        /// </summary>
        public void Initialize(object building)
        {
            _building = building;
            _workerIds = GetWorkerIdsFunc(building);
            _maxWorkers = _workerIds?.Length ?? 0;
            _racesRefreshed = false;
            RefreshAvailableRaces();
        }

        /// <summary>
        /// Clear cached data.
        /// </summary>
        public void Clear()
        {
            _building = null;
            _workerIds = null;
            _maxWorkers = 0;
            _availableRaces.Clear();
            _racesRefreshed = false;
        }

        /// <summary>
        /// Check if the building has any worker slots.
        /// </summary>
        public bool HasWorkers()
        {
            return _maxWorkers > 0;
        }

        // ========================================
        // SECTION INTERFACE
        // ========================================

        /// <summary>
        /// Get the number of worker slots (items at Level 1 navigation).
        /// </summary>
        public int GetItemCount()
        {
            return _maxWorkers;
        }

        /// <summary>
        /// Get the number of sub-items for a worker slot (races + optional unassign).
        /// </summary>
        public int GetSubItemCount(int workerIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return 0;

            RefreshAvailableRaces();

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);

            int count = _availableRaces.Count;
            if (slotOccupied) count++;  // Add "Unassign" option

            return count;
        }

        /// <summary>
        /// Announce a worker slot. Force-refreshes race data for current info.
        /// </summary>
        public void AnnounceItem(int itemIndex)
        {
            // Force-refresh races on each slot announcement for current data
            RefreshAvailableRaces(force: true);

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

        /// <summary>
        /// Announce a worker sub-item (unassign option or race with bonus).
        /// </summary>
        public void AnnounceSubItem(int workerIndex, int subItemIndex)
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

        /// <summary>
        /// Perform action on a worker sub-item (assign or unassign).
        /// Returns true if action was performed (caller should set _navigationLevel = 1).
        /// </summary>
        public bool PerformSubItemAction(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return false;

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                // Unassign
                if (BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex))
                {
                    RefreshWorkerIds();
                    RefreshAvailableRaces(force: true);
                    Speech.Say("Worker unassigned");
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
                    RefreshWorkerIds();
                }

                if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName))
                {
                    RefreshWorkerIds();
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

        /// <summary>
        /// Get the searchable name for a worker slot (item level).
        /// </summary>
        public string GetItemName(int itemIndex)
        {
            if (!IsValidWorkerIndex(itemIndex))
                return null;

            int workerId = _workerIds[itemIndex];
            if (workerId <= 0)
                return $"Slot {itemIndex + 1}";

            string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
            return !string.IsNullOrEmpty(workerDesc) ? workerDesc : $"Slot {itemIndex + 1}";
        }

        /// <summary>
        /// Get the searchable name for a worker sub-item (race or unassign).
        /// </summary>
        public string GetSubItemName(int workerIndex, int subItemIndex)
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

        // ========================================
        // HELPERS
        // ========================================

        /// <summary>
        /// Re-fetch worker IDs using the configured delegate.
        /// </summary>
        public void RefreshWorkerIds()
        {
            if (_building == null) return;
            _workerIds = GetWorkerIdsFunc(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
        }

        /// <summary>
        /// Refresh available races data.
        /// </summary>
        public void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshed) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers(includeZeroFree: true);
            _racesRefreshed = true;
        }

        private bool IsValidWorkerIndex(int index)
        {
            return _workerIds != null && index >= 0 && index < _workerIds.Length;
        }
    }
}
