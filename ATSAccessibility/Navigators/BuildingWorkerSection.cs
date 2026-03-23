using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Shared handler for building worker section.
	/// Any building navigator can use this to handle the Workers section.
	/// Provides slot listing, race-based assignment, sub-item navigation, and refresh logic.
	/// Loose automatons are appended after worker slots as read-only items.
	/// </summary>
	public class BuildingWorkerSection {
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
		private List<int> _looseAutomatonIds = new List<int>();
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

		public BuildingWorkerSection() {
			GetWorkerIdsFunc = BuildingReflection.GetWorkerIds;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		/// <summary>
		/// Initialize the section with a building. Fetches worker IDs and available races.
		/// </summary>
		public void Initialize(object building) {
			_building = building;
			_workerIds = GetWorkerIdsFunc(building);
			_maxWorkers = _workerIds?.Length ?? 0;
			_racesRefreshed = false;
			RefreshLooseAutomatons();
			RefreshAvailableRaces();
		}

		/// <summary>
		/// Clear cached data.
		/// </summary>
		public void Clear() {
			_building = null;
			_workerIds = null;
			_maxWorkers = 0;
			_looseAutomatonIds.Clear();
			_availableRaces.Clear();
			_racesRefreshed = false;
		}

		/// <summary>
		/// Check if the building has any worker slots.
		/// </summary>
		public bool HasWorkers() {
			return _maxWorkers > 0;
		}

		// ========================================
		// SECTION INTERFACE
		// ========================================

		/// <summary>
		/// Get the number of items (worker slots + loose automatons).
		/// </summary>
		public int GetItemCount() {
			return _maxWorkers + _looseAutomatonIds.Count;
		}

		/// <summary>
		/// Get the number of sub-items for a worker slot (races + optional unassign).
		/// Returns 0 for automaton-occupied slots and loose automaton items.
		/// </summary>
		public int GetSubItemCount(int workerIndex) {
			// Loose automatons and automaton-occupied slots have no sub-items
			if (workerIndex >= _maxWorkers) return 0;
			if (!IsValidWorkerIndex(workerIndex)) return 0;
			if (IsAutomatonSlot(workerIndex)) return 0;

			RefreshAvailableRaces();

			bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);

			int count = _availableRaces.Count;
			if (slotOccupied) count++;  // Add "Unassign" option

			return count;
		}

		/// <summary>
		/// Announce a worker slot or loose automaton.
		/// </summary>
		public void AnnounceItem(int itemIndex) {
			// Force-refresh races on each slot announcement for current data
			RefreshAvailableRaces(force: true);

			// Loose automaton item
			if (itemIndex >= _maxWorkers) {
				int looseIndex = itemIndex - _maxWorkers;
				if (looseIndex >= 0 && looseIndex < _looseAutomatonIds.Count) {
					int id = _looseAutomatonIds[looseIndex];
					var actor = AutomatonReflection.GetAutomaton(id);
					string displayName = AutomatonReflection.GetAutomatonDisplayName(actor);
					string label = displayName != null ? $"{displayName} automaton" : "Automaton";
					string task = BuildingReflection.GetActorTaskDescription(actor);
					Speech.Say(!string.IsNullOrEmpty(task) ? $"{label}, {task}" : label);
				} else {
					Speech.Say("Invalid automaton");
				}
				return;
			}

			// Worker slot
			if (!IsValidWorkerIndex(itemIndex)) {
				Speech.Say("Invalid worker slot");
				return;
			}

			int workerId = _workerIds[itemIndex];
			int slotNum = itemIndex + 1;

			if (workerId <= 0) {
				Speech.Say($"Worker slot {slotNum}: Empty");
				return;
			}

			string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
			if (string.IsNullOrEmpty(workerDesc)) {
				Speech.Say($"Worker slot {slotNum}: Assigned");
				return;
			}

			Speech.Say($"Worker slot {slotNum}: {workerDesc}");
		}

		/// <summary>
		/// Announce a worker sub-item (unassign option or race with bonus).
		/// </summary>
		public void AnnounceSubItem(int workerIndex, int subItemIndex) {
			if (!IsValidWorkerIndex(workerIndex)) {
				Speech.Say("Invalid worker slot");
				return;
			}

			bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
			int raceOffset = slotOccupied ? 1 : 0;

			if (slotOccupied && subItemIndex == 0) {
				Speech.Say("Unassign worker");
				return;
			}

			int raceIndex = subItemIndex - raceOffset;
			if (raceIndex >= 0 && raceIndex < _availableRaces.Count) {
				var (raceName, freeCount) = _availableRaces[raceIndex];
				var (bonus, bonusType) = BuildingReflection.GetRaceBonusWithType(_building, raceName);
				if (!string.IsNullOrEmpty(bonus)) {
					string typeStr = !string.IsNullOrEmpty(bonusType) ? $" {bonusType}" : "";
					Speech.Say($"{raceName}: {freeCount} available, {bonus}{typeStr}");
				} else {
					Speech.Say($"{raceName}: {freeCount} available");
				}
			} else {
				Speech.Say("Invalid option");
			}
		}

		/// <summary>
		/// Perform action on a worker sub-item (assign or unassign).
		/// Returns true if action was performed (caller should set _navigationLevel = 1).
		/// </summary>
		public bool PerformSubItemAction(int workerIndex, int subItemIndex) {
			// Block interaction for loose automatons and automaton-occupied slots
			if (workerIndex >= _maxWorkers) {
				Speech.Say("Automaton, cannot reassign");
				return false;
			}
			if (!IsValidWorkerIndex(workerIndex)) return false;
			if (IsAutomatonSlot(workerIndex)) {
				Speech.Say("Automaton, cannot reassign");
				return false;
			}

			bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
			int raceOffset = slotOccupied ? 1 : 0;

			if (slotOccupied && subItemIndex == 0) {
				// Unassign
				if (BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex)) {
					RefreshWorkerIds();
					RefreshAvailableRaces(force: true);
					Speech.Say("Worker unassigned");
					return true;
				} else {
					Speech.Say("Cannot unassign worker");
					return false;
				}
			}

			// Assign race
			int raceIndex = subItemIndex - raceOffset;
			if (raceIndex >= 0 && raceIndex < _availableRaces.Count) {
				var (raceName, freeCount) = _availableRaces[raceIndex];

				// Check if race has free workers
				if (freeCount == 0) {
					Speech.Say($"No free {raceName} workers");
					SoundManager.PlayFailed();
					return false;
				}

				// If slot is occupied, unassign first
				if (slotOccupied) {
					BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex);
					RefreshWorkerIds();
				}

				if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName)) {
					RefreshWorkerIds();
					RefreshAvailableRaces(force: true);

					// Announce the new worker
					if (IsValidWorkerIndex(workerIndex)) {
						string workerDesc = BuildingReflection.GetWorkerDescription(_workerIds[workerIndex]);
						Speech.Say($"Assigned: {workerDesc ?? raceName}");
					} else {
						Speech.Say($"Assigned: {raceName}");
					}

					return true;
				} else {
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
		/// Get the searchable name for a worker slot or loose automaton (item level).
		/// </summary>
		public string GetItemName(int itemIndex) {
			// Loose automaton
			if (itemIndex >= _maxWorkers) {
				int looseIndex = itemIndex - _maxWorkers;
				if (looseIndex >= 0 && looseIndex < _looseAutomatonIds.Count) {
					var actor = AutomatonReflection.GetAutomaton(_looseAutomatonIds[looseIndex]);
					string displayName = AutomatonReflection.GetAutomatonDisplayName(actor);
					return displayName != null ? $"{displayName} automaton" : "Automaton";
				}
				return null;
			}

			// Worker slot
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
		public string GetSubItemName(int workerIndex, int subItemIndex) {
			if (!IsValidWorkerIndex(workerIndex))
				return null;

			bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
			int raceOffset = slotOccupied ? 1 : 0;

			if (slotOccupied && subItemIndex == 0) {
				return "Unassign";
			}

			int raceIndex = subItemIndex - raceOffset;
			if (raceIndex >= 0 && raceIndex < _availableRaces.Count) {
				return _availableRaces[raceIndex].raceName;
			}
			return null;
		}

		// ========================================
		// HELPERS
		// ========================================

		/// <summary>
		/// Re-fetch worker IDs and loose automaton IDs using the configured delegate.
		/// </summary>
		public void RefreshWorkerIds() {
			if (_building == null) return;
			_workerIds = GetWorkerIdsFunc(_building);
			_maxWorkers = _workerIds?.Length ?? 0;
			RefreshLooseAutomatons();
		}

		/// <summary>
		/// Refresh available races data.
		/// </summary>
		public void RefreshAvailableRaces(bool force = false) {
			if (!force && _racesRefreshed) return;

			_availableRaces = BuildingReflection.GetRacesWithFreeWorkers(includeZeroFree: true);
			_racesRefreshed = true;
		}

		private bool IsValidWorkerIndex(int index) {
			return _workerIds != null && index >= 0 && index < _workerIds.Length;
		}

		/// <summary>
		/// Check if a worker slot is occupied by an automaton.
		/// </summary>
		private bool IsAutomatonSlot(int workerIndex) {
			if (!IsValidWorkerIndex(workerIndex)) return false;
			int workerId = _workerIds[workerIndex];
			if (workerId <= 0) return false;
			var actor = BuildingReflection.GetActor(workerId);
			return AutomatonReflection.IsAutomaton(actor);
		}

		/// <summary>
		/// Refresh the list of live loose automaton IDs for the current building.
		/// </summary>
		private void RefreshLooseAutomatons() {
			_looseAutomatonIds.Clear();
			if (_building == null) return;
			var ids = AutomatonReflection.GetLooseAutomatonIds(_building);
			foreach (var id in ids) {
				if (AutomatonReflection.IsAlive(id)) {
					_looseAutomatonIds.Add(id);
				}
			}
		}
	}
}
