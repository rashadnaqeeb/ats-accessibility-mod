# WorkerInfoHelper.cs
Helper class for quick worker management from settlement map view.
Used by W, +/-, Shift++/- keys to view and manage workers without opening building panel.

## class WorkerInfoHelper (line 11)

### Fields
- private static int _selectedRaceIndex (line 12)
- private static List<(string raceName, int freeCount)> _cachedRaces (line 13)
- private static float _lastRaceRefreshTime (line 14)
- private const float RACE_CACHE_DURATION = 1f (line 15)

### Methods
- public static void Reset() (line 18)
  Resets state between games to prevent stale race selection.
- public static string GetWorkerSummary(object building) (line 33)
  Returns "No building", "Under construction", "No worker slots", "Workers not needed", or "N/Max: 2 beavers, 1 harpy, SpecialtyName Type (Race)". Special case for Hearth: shows firekeeper effect for assigned race. Specialty info only shown when matching races are present in settlement.
- private static (string specialty, string bonusType, List<string> matchingRaces) GetBuildingSpecialtyInfo(object building) (line 97)
  Checks all present races for a bonus on this building. Returns first found specialty name, bonus type, and all matching race names.
- private static string FormatSpecialtyInfo(object building) (line 121)
  Returns ", SpecialtyName BonusType (Race1, Race2)" or empty string.
- private static List<(string raceName, int count)> GetWorkerRaceCounts(object building) (line 133)
- private static string GetWorkerRace(int workerId) (line 155)
- private static string Pluralize(string name, int count) (line 165)
  Handles irregular plurals: -y -> -ies, -x/-s -> -es, else +s.
- public static string CycleRace(int direction) (line 179)
  Cycles selected race index through present races with free workers. Returns "RaceName, N free" or "No free workers".
- public static string GetSelectedRace() (line 194)
- private static List<(string raceName, int freeCount)> GetPresentRacesWithFreeCounts() (line 208)
  Unlike GetRacesWithFreeWorkers, includes races with 0 free workers (all present races).
- private static void RefreshRacesIfNeeded() (line 221)
  Refreshes cache after RACE_CACHE_DURATION seconds. Clamps selected index if race count changed.
- public static string AddWorker(object building) (line 240)
  Assigns a worker of the selected race to the first empty slot. Returns "Assigned: WorkerName, RaceName" or error message.
- public static string RemoveWorker(object building) (line 309)
  Removes worker matching selected race (prefers race match, falls back to any worker). Single pass bottom-up scan. Returns "Removed: WorkerName" or error message.
