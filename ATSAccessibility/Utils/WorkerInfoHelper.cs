using ATSAccessibility.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Helper class for quick worker management from settlement map view.
	/// Used by W, +/-, Shift++/- keys to view and manage workers without opening building panel.
	/// </summary>
	public static class WorkerInfoHelper {
		private static int _selectedRaceIndex = 0;
		private static List<(string raceName, int freeCount)> _cachedRaces = new List<(string, int)>();
		private static float _lastRaceRefreshTime = 0f;
		private const float RACE_CACHE_DURATION = 1f;

		/// <summary>Reset state between games to prevent stale race selection.</summary>
		public static void Reset() {
			_selectedRaceIndex = 0;
			_cachedRaces.Clear();
			_lastRaceRefreshTime = 0f;
		}

		/// <summary>
		/// Get worker summary for a building.
		/// On building with workers: "3/3: 2 beavers, 1 harpy, Woodworking Efficiency (Beavers)"
		/// On building with no workers: "0/3, Woodworking Efficiency (Beavers)"
		/// On non-production building: "No worker slots"
		/// Specialty info only shown when matching races are present in settlement.
		/// Bonus type is "Efficiency" (speed bonus) or "Comfort" (resolve bonus).
		/// Special case for Hearth: shows firekeeper effect for assigned race only.
		/// </summary>
		public static string GetWorkerSummary(object building) {
			if (building == null) {
				return Strings.Get("common.no_building");
			}

			if (ConstructionReflection.IsBuildingUnfinished(building)) {
				return Strings.Get("util.worker_info.under_construction");
			}

			if (!BuildingReflection.IsProductionBuilding(building)) {
				return Strings.Get("util.worker_info.no_worker_slots");
			}

			int maxWorkers = BuildingReflection.GetMaxWorkers(building);
			if (maxWorkers == 0) {
				return Strings.Get("util.worker_info.no_worker_slots");
			}

			if (!BuildingReflection.ShouldAllowWorkerManagement(building)) {
				return Strings.Get("util.worker_info.workers_not_needed");
			}

			int currentWorkers = BuildingReflection.GetWorkerCount(building);
			string looseInfo = FormatLooseAutomatons(building);

			// Special case for Hearth - show firekeeper effect for assigned race only
			if (BuildingReflection.IsHearth(building)) {
				if (currentWorkers == 0) {
					return Strings.Get("util.worker_info.hearth_zero", maxWorkers, looseInfo);
				}

				// Get the assigned worker's race and their firekeeper effect
				var hearthWorkers = GetWorkerRaceCounts(building);
				if (hearthWorkers.Count > 0) {
					string raceName = hearthWorkers[0].raceName;
					string raceDisplay = EmbarkReflection.GetRaceDisplayName(raceName);
					// Use GetRaceBonusForBuilding - same path as worker menu
					string effect = BuildingReflection.GetRaceBonusForBuilding(building, raceName);
					if (!string.IsNullOrEmpty(effect)) {
						return Strings.Get("util.worker_info.hearth_with_effect", currentWorkers, maxWorkers, raceDisplay, effect, looseInfo);
					}
					return Strings.Get("util.worker_info.hearth_no_effect", currentWorkers, maxWorkers, raceDisplay, looseInfo);
				}
				return Strings.Get("util.worker_info.hearth_plain", currentWorkers, maxWorkers, looseInfo);
			}

			if (currentWorkers == 0) {
				return Strings.Get("util.worker_info.empty", maxWorkers, FormatSpecialtyInfo(building), looseInfo);
			}

			// Count workers by race
			var raceCounts = GetWorkerRaceCounts(building);
			if (raceCounts.Count == 0) {
				return Strings.Get("util.worker_info.no_races", currentWorkers, maxWorkers, FormatSpecialtyInfo(building), looseInfo);
			}

			// Format: "3/3: 2 Beaver, 1 Harpy, Woodworking Efficiency (Beavers)"
			var raceStrings = raceCounts.Select(rc =>
				Strings.Get(rc.count == 1 ? "util.worker_info.race_count_singular" : "util.worker_info.race_count_plural",
					rc.count, EmbarkReflection.GetRaceDisplayName(rc.raceName)));
			return Strings.Get("util.worker_info.with_workers", currentWorkers, maxWorkers, string.Join(", ", raceStrings), FormatSpecialtyInfo(building), looseInfo);
		}

		/// <summary>
		/// Get building specialty and which present races match it.
		/// Returns (specialty name, bonus type, list of matching race names) or (null, null, empty) if none.
		/// </summary>
		private static (string specialty, string bonusType, List<string> matchingRaces) GetBuildingSpecialtyInfo(object building) {
			var presentRaces = StatsReader.GetPresentRaces();
			string specialty = null;
			string bonusType = null;
			var matchingRaces = new List<string>();

			foreach (var race in presentRaces) {
				var (bonus, type) = BuildingReflection.GetRaceBonusWithType(building, race);
				if (!string.IsNullOrEmpty(bonus)) {
					if (specialty == null) {
						specialty = bonus;
						bonusType = type;
					}
					matchingRaces.Add(race);
				}
			}

			return (specialty, bonusType, matchingRaces);
		}

		/// <summary>
		/// Format specialty info for announcement.
		/// Returns ", Woodworking Efficiency (Beavers)" or empty string if no matching races.
		/// </summary>
		private static string FormatSpecialtyInfo(object building) {
			var (specialty, bonusType, matchingRaces) = GetBuildingSpecialtyInfo(building);
			if (specialty == null || matchingRaces.Count == 0) {
				return "";
			}
			string typeStr = !string.IsNullOrEmpty(bonusType) ? $" {bonusType}" : "";
			var localizedRaces = matchingRaces.Select(r => EmbarkReflection.GetRaceDisplayName(r));
			return Strings.Get("util.worker_info.specialty", specialty, typeStr, string.Join(", ", localizedRaces));
		}

		/// <summary>
		/// Format loose automaton info for appending to worker summary.
		/// Returns ", 1 hauler automaton" or empty string if none.
		/// </summary>
		private static string FormatLooseAutomatons(object building) {
			var looseIds = AutomatonReflection.GetLooseAutomatonIds(building);
			if (looseIds.Count == 0) return "";

			var counts = new Dictionary<string, int>();
			foreach (var id in looseIds) {
				if (!AutomatonReflection.IsAlive(id)) continue;
				var actor = AutomatonReflection.GetAutomaton(id);
				string displayName = AutomatonReflection.GetAutomatonDisplayName(actor);
				string key = displayName != null ? Strings.Get("util.worker_info.auto_named", displayName.ToLowerInvariant()) : Strings.Get("util.worker_info.auto_unnamed");
				if (counts.ContainsKey(key))
					counts[key]++;
				else
					counts[key] = 1;
			}

			if (counts.Count == 0) return "";

			var parts = counts.Select(kv =>
				Strings.Get(kv.Value == 1 ? "util.worker_info.auto_count_singular" : "util.worker_info.auto_count_plural", kv.Value, kv.Key));
			return Strings.Get("util.worker_info.loose_suffix", string.Join(", ", parts));
		}

		/// <summary>
		/// Count workers by race for a building.
		/// Automatons in worker slots are grouped by display name with " automaton" suffix.
		/// </summary>
		private static List<(string raceName, int count)> GetWorkerRaceCounts(object building) {
			var counts = new Dictionary<string, int>();
			var workerIds = BuildingReflection.GetWorkerIds(building);

			foreach (var workerId in workerIds) {
				if (workerId <= 0) continue;

				var actor = BuildingReflection.GetActor(workerId);
				string key;
				if (AutomatonReflection.IsAutomaton(actor)) {
					string displayName = AutomatonReflection.GetAutomatonDisplayName(actor);
					key = displayName != null ? Strings.Get("util.worker_info.auto_named", displayName.ToLowerInvariant()) : Strings.Get("util.worker_info.auto_unnamed");
				} else {
					key = BuildingReflection.GetActorRace(actor);
					if (string.IsNullOrEmpty(key)) key = Strings.Get("common.unknown");
				}

				if (counts.ContainsKey(key))
					counts[key]++;
				else
					counts[key] = 1;
			}

			return counts.Select(kv => (kv.Key, kv.Value)).ToList();
		}

		/// <summary>
		/// Cycle to next/previous race with free workers.
		/// Returns announcement like "Beaver, 5 free" or "No free workers"
		/// </summary>
		public static string CycleRace(int direction) {
			RefreshRacesIfNeeded();

			if (_cachedRaces.Count == 0) {
				return Strings.Get("common.no_free_workers");
			}

			_selectedRaceIndex = NavigationUtils.WrapIndex(_selectedRaceIndex, direction, _cachedRaces.Count);
			var selected = _cachedRaces[_selectedRaceIndex];
			return Strings.Get("util.worker_info.race_free", EmbarkReflection.GetRaceDisplayName(selected.raceName), selected.freeCount);
		}

		/// <summary>
		/// Get currently selected race name.
		/// </summary>
		public static string GetSelectedRace() {
			RefreshRacesIfNeeded();

			if (_cachedRaces.Count == 0 || _selectedRaceIndex >= _cachedRaces.Count) {
				return null;
			}

			return _cachedRaces[_selectedRaceIndex].raceName;
		}

		/// <summary>
		/// Get all races present in the settlement with their free worker counts.
		/// Unlike GetRacesWithFreeWorkers, this includes races with 0 free workers.
		/// </summary>
		private static List<(string raceName, int freeCount)> GetPresentRacesWithFreeCounts() {
			var presentRaces = StatsReader.GetPresentRaces();
			var result = new List<(string, int)>();
			foreach (var race in presentRaces) {
				int freeCount = BuildingReflection.GetFreeWorkerCount(race);
				result.Add((race, freeCount));
			}
			return result;
		}

		/// <summary>
		/// Refresh the race list if the cache has expired.
		/// </summary>
		private static void RefreshRacesIfNeeded() {
			float now = Time.realtimeSinceStartup;
			if (now - _lastRaceRefreshTime > RACE_CACHE_DURATION) {
				_cachedRaces = GetPresentRacesWithFreeCounts();
				_lastRaceRefreshTime = now;

				// Clamp selected index if race count changed
				if (_cachedRaces.Count > 0) {
					_selectedRaceIndex = Mathf.Clamp(_selectedRaceIndex, 0, _cachedRaces.Count - 1);
				} else {
					_selectedRaceIndex = 0;
				}
			}
		}

		/// <summary>
		/// Add worker of selected race to first empty slot.
		/// Returns "Assigned: {WorkerName}, {RaceName}" or error message.
		/// </summary>
		public static string AddWorker(object building) {
			if (building == null) {
				return Strings.Get("common.no_building");
			}

			if (ConstructionReflection.IsBuildingUnfinished(building)) {
				return Strings.Get("util.worker_info.under_construction");
			}

			if (!BuildingReflection.IsProductionBuilding(building)) {
				return Strings.Get("util.worker_info.no_worker_slots");
			}

			if (!BuildingReflection.ShouldAllowWorkerManagement(building)) {
				return Strings.Get("util.worker_info.workers_not_needed");
			}

			// Find first empty slot
			var workerIds = BuildingReflection.GetWorkerIds(building);
			int emptySlot = -1;
			for (int i = 0; i < workerIds.Length; i++) {
				if (workerIds[i] <= 0) {
					emptySlot = i;
					break;
				}
			}

			if (emptySlot < 0) {
				return Strings.Get("util.worker_info.building_full");
			}

			// Refresh race list and get selected race
			RefreshRacesIfNeeded();
			string raceName = GetSelectedRace();

			if (string.IsNullOrEmpty(raceName)) {
				return Strings.Get("common.no_free_workers");
			}

			// Check if this race has free workers
			int freeCount = BuildingReflection.GetFreeWorkerCount(raceName);
			string raceDisplay = EmbarkReflection.GetRaceDisplayName(raceName);
			if (freeCount <= 0) {
				return Strings.Get("util.worker_info.no_free_race", raceDisplay);
			}

			// Assign the worker
			bool success = BuildingReflection.AssignWorkerToSlot(building, emptySlot, raceName);
			if (!success) {
				return Strings.Get("util.worker_info.assignment_failed");
			}

			// Force cache refresh on next query
			_lastRaceRefreshTime = 0f;

			// Get the assigned worker's name
			var newWorkerIds = BuildingReflection.GetWorkerIds(building);
			if (emptySlot < newWorkerIds.Length && newWorkerIds[emptySlot] > 0) {
				var actor = BuildingReflection.GetActor(newWorkerIds[emptySlot]);
				string workerName = BuildingReflection.GetActorName(actor) ?? Strings.Get("common.worker");
				return Strings.Get("util.worker_info.assigned_with_race", workerName, raceDisplay);
			}

			return Strings.Get("util.worker_info.assigned_race_only", raceDisplay);
		}

		/// <summary>
		/// Remove worker from building. Prefers selected race, falls back to any worker.
		/// Returns "Removed: {WorkerName}" or error message.
		/// </summary>
		public static string RemoveWorker(object building) {
			if (building == null) {
				return Strings.Get("common.no_building");
			}

			if (ConstructionReflection.IsBuildingUnfinished(building)) {
				return Strings.Get("util.worker_info.under_construction");
			}

			if (!BuildingReflection.IsProductionBuilding(building)) {
				return Strings.Get("util.worker_info.no_worker_slots");
			}

			if (!BuildingReflection.ShouldAllowWorkerManagement(building)) {
				return Strings.Get("util.worker_info.workers_not_needed");
			}

			var workerIds = BuildingReflection.GetWorkerIds(building);
			string selectedRace = GetSelectedRace();

			// Single pass: find selected race match and track fallback (bottom-up)
			int slotToRemove = -1;
			int fallbackSlot = -1;
			string workerName = null;
			string fallbackName = null;

			for (int i = workerIds.Length - 1; i >= 0; i--) {
				if (workerIds[i] <= 0) continue;

				var actor = BuildingReflection.GetActor(workerIds[i]);

				// Skip automaton-occupied slots — cannot reassign via VillagersService
				if (AutomatonReflection.IsAutomaton(actor)) continue;

				string name = BuildingReflection.GetActorName(actor);

				// Track last occupied non-automaton slot as fallback (bottom-up scan, set once)
				if (fallbackSlot < 0) {
					fallbackSlot = i;
					fallbackName = name;
				}

				// Check for selected race match
				if (slotToRemove < 0 && !string.IsNullOrEmpty(selectedRace)) {
					string race = BuildingReflection.GetActorRace(actor);
					if (race == selectedRace) {
						slotToRemove = i;
						workerName = name;
					}
				}
			}

			// Use selected race slot if found, otherwise fallback
			if (slotToRemove < 0) {
				slotToRemove = fallbackSlot;
				workerName = fallbackName;
			}

			if (slotToRemove < 0) {
				return Strings.Get("util.worker_info.no_workers");
			}

			// Unassign the worker
			bool success = BuildingReflection.UnassignWorkerFromSlot(building, slotToRemove);
			if (!success) {
				return Strings.Get("common.removal_failed");
			}

			// Force cache refresh on next query
			_lastRaceRefreshTime = 0f;

			return Strings.Get("util.worker_info.removed", workerName ?? Strings.Get("common.worker"));
		}
	}
}
