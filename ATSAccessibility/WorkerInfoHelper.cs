using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Helper class for quick worker management from settlement map view.
    /// Used by W, +/-, Shift++/- keys to view and manage workers without opening building panel.
    /// </summary>
    public static class WorkerInfoHelper
    {
        private static int _selectedRaceIndex = 0;
        private static List<(string raceName, int freeCount)> _cachedRaces = new List<(string, int)>();
        private static float _lastRaceRefreshTime = 0f;
        private const float RACE_CACHE_DURATION = 1f;

        /// <summary>
        /// Get worker summary for a building.
        /// On building with workers: "3/3: 2 beavers, 1 harpy"
        /// On building with no workers: "0/3"
        /// On non-production building: "No worker slots"
        /// </summary>
        public static string GetWorkerSummary(object building)
        {
            if (building == null)
            {
                return "No building";
            }

            if (!BuildingReflection.IsProductionBuilding(building))
            {
                return "No worker slots";
            }

            int maxWorkers = BuildingReflection.GetMaxWorkers(building);
            if (maxWorkers == 0)
            {
                return "No worker slots";
            }

            int currentWorkers = BuildingReflection.GetWorkerCount(building);

            if (currentWorkers == 0)
            {
                return $"0/{maxWorkers}";
            }

            // Count workers by race
            var raceCounts = GetWorkerRaceCounts(building);
            if (raceCounts.Count == 0)
            {
                return $"{currentWorkers}/{maxWorkers}";
            }

            // Format: "3/3: 2 beavers, 1 harpy"
            var raceStrings = raceCounts.Select(rc =>
                $"{rc.count} {Pluralize(rc.raceName.ToLowerInvariant(), rc.count)}");
            return $"{currentWorkers}/{maxWorkers}: {string.Join(", ", raceStrings)}";
        }

        /// <summary>
        /// Count workers by race for a building.
        /// </summary>
        private static List<(string raceName, int count)> GetWorkerRaceCounts(object building)
        {
            var counts = new Dictionary<string, int>();
            var workerIds = BuildingReflection.GetWorkerIds(building);

            foreach (var workerId in workerIds)
            {
                if (workerId <= 0) continue;

                string race = GetWorkerRace(workerId);
                if (string.IsNullOrEmpty(race)) race = "Unknown";

                if (counts.ContainsKey(race))
                    counts[race]++;
                else
                    counts[race] = 1;
            }

            return counts.Select(kv => (kv.Key, kv.Value)).ToList();
        }

        /// <summary>
        /// Get the race of a worker by ID.
        /// </summary>
        private static string GetWorkerRace(int workerId)
        {
            if (workerId <= 0) return null;

            var actor = BuildingReflection.GetActor(workerId);
            return BuildingReflection.GetActorRace(actor);
        }

        /// <summary>
        /// Pluralize a race name. Handles irregular plurals (harpy->harpies, fox->foxes).
        /// </summary>
        private static string Pluralize(string name, int count)
        {
            if (count == 1) return name;

            if (name.EndsWith("y"))
                return name.Substring(0, name.Length - 1) + "ies";
            if (name.EndsWith("x") || name.EndsWith("s"))
                return name + "es";
            return name + "s";
        }

        /// <summary>
        /// Cycle to next/previous race with free workers.
        /// Returns announcement like "Beaver, 5 free" or "No free workers"
        /// </summary>
        public static string CycleRace(int direction)
        {
            RefreshRacesIfNeeded();

            if (_cachedRaces.Count == 0)
            {
                return "No free workers";
            }

            _selectedRaceIndex = NavigationUtils.WrapIndex(_selectedRaceIndex, direction, _cachedRaces.Count);
            var selected = _cachedRaces[_selectedRaceIndex];
            return $"{selected.raceName}, {selected.freeCount} free";
        }

        /// <summary>
        /// Get currently selected race name.
        /// </summary>
        public static string GetSelectedRace()
        {
            RefreshRacesIfNeeded();

            if (_cachedRaces.Count == 0 || _selectedRaceIndex >= _cachedRaces.Count)
            {
                return null;
            }

            return _cachedRaces[_selectedRaceIndex].raceName;
        }

        /// <summary>
        /// Refresh the race list if the cache has expired.
        /// </summary>
        private static void RefreshRacesIfNeeded()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastRaceRefreshTime > RACE_CACHE_DURATION)
            {
                _cachedRaces = BuildingReflection.GetRacesWithFreeWorkers();
                _lastRaceRefreshTime = now;

                // Clamp selected index if race count changed
                if (_cachedRaces.Count > 0)
                {
                    _selectedRaceIndex = Mathf.Clamp(_selectedRaceIndex, 0, _cachedRaces.Count - 1);
                }
                else
                {
                    _selectedRaceIndex = 0;
                }
            }
        }

        /// <summary>
        /// Add worker of selected race to first empty slot.
        /// Returns "Assigned: {WorkerName}, {RaceName}" or error message.
        /// </summary>
        public static string AddWorker(object building)
        {
            if (building == null)
            {
                return "No building";
            }

            if (!BuildingReflection.IsProductionBuilding(building))
            {
                return "No worker slots";
            }

            // Find first empty slot
            var workerIds = BuildingReflection.GetWorkerIds(building);
            int emptySlot = -1;
            for (int i = 0; i < workerIds.Length; i++)
            {
                if (workerIds[i] <= 0)
                {
                    emptySlot = i;
                    break;
                }
            }

            if (emptySlot < 0)
            {
                return "Building full";
            }

            // Refresh race list and get selected race
            RefreshRacesIfNeeded();
            string raceName = GetSelectedRace();

            if (string.IsNullOrEmpty(raceName))
            {
                return "No free workers";
            }

            // Check if this race has free workers
            int freeCount = BuildingReflection.GetFreeWorkerCount(raceName);
            if (freeCount <= 0)
            {
                return $"No free {raceName.ToLowerInvariant()}s";
            }

            // Assign the worker
            bool success = BuildingReflection.AssignWorkerToSlot(building, emptySlot, raceName);
            if (!success)
            {
                return "Assignment failed";
            }

            // Force cache refresh on next query
            _lastRaceRefreshTime = 0f;

            // Get the assigned worker's name
            var newWorkerIds = BuildingReflection.GetWorkerIds(building);
            if (emptySlot < newWorkerIds.Length && newWorkerIds[emptySlot] > 0)
            {
                var actor = BuildingReflection.GetActor(newWorkerIds[emptySlot]);
                string workerName = BuildingReflection.GetActorName(actor) ?? "Worker";
                return $"Assigned: {workerName}, {raceName}";
            }

            return $"Assigned: {raceName}";
        }

        /// <summary>
        /// Remove worker from building. Prefers selected race, falls back to any worker.
        /// Returns "Removed: {WorkerName}" or error message.
        /// </summary>
        public static string RemoveWorker(object building)
        {
            if (building == null)
            {
                return "No building";
            }

            if (!BuildingReflection.IsProductionBuilding(building))
            {
                return "No worker slots";
            }

            var workerIds = BuildingReflection.GetWorkerIds(building);
            string selectedRace = GetSelectedRace();

            // Single pass: find selected race match and track fallback (bottom-up)
            int slotToRemove = -1;
            int fallbackSlot = -1;
            string workerName = null;
            string fallbackName = null;

            for (int i = workerIds.Length - 1; i >= 0; i--)
            {
                if (workerIds[i] <= 0) continue;

                var actor = BuildingReflection.GetActor(workerIds[i]);
                string name = BuildingReflection.GetActorName(actor);

                // Track first occupied slot as fallback
                if (fallbackSlot < 0)
                {
                    fallbackSlot = i;
                    fallbackName = name;
                }

                // Check for selected race match
                if (slotToRemove < 0 && !string.IsNullOrEmpty(selectedRace))
                {
                    string race = BuildingReflection.GetActorRace(actor);
                    if (race == selectedRace)
                    {
                        slotToRemove = i;
                        workerName = name;
                    }
                }
            }

            // Use selected race slot if found, otherwise fallback
            if (slotToRemove < 0)
            {
                slotToRemove = fallbackSlot;
                workerName = fallbackName;
            }

            if (slotToRemove < 0)
            {
                return "No workers";
            }

            // Unassign the worker
            bool success = BuildingReflection.UnassignWorkerFromSlot(building, slotToRemove);
            if (!success)
            {
                return "Removal failed";
            }

            // Force cache refresh on next query
            _lastRaceRefreshTime = 0f;

            return $"Removed: {workerName ?? "Worker"}";
        }
    }
}
