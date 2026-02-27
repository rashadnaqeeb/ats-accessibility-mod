# BuildingWorkerSection.cs
Shared handler for building worker section.
Any building navigator can use this to handle the Workers section.
Provides slot listing, race-based assignment, sub-item navigation, and refresh logic.

## class BuildingWorkerSection (line 12)

### Fields
- `private` `object` `_building` (line 28)
- `private` `int[]` `_workerIds` (line 29)
- `private` `int` `_maxWorkers` (line 30)
- `private` `List<(string raceName, int freeCount)>` `_availableRaces` (line 31)
- `private` `bool` `_racesRefreshed` (line 32)

### Properties
- `public` `Func<object, int[]>` `GetWorkerIdsFunc` `{ get; set; }` (line 22) - Delegate to fetch worker IDs; defaults to `BuildingReflection.GetWorkerIds`; override for Hearth or Relic
- `public` `int` `MaxWorkers` `{ get; }` (line 38)
- `public` `int[]` `WorkerIds` `{ get; }` (line 39)

### Methods
- `public` `BuildingWorkerSection()` (line 45) - Sets `GetWorkerIdsFunc` to `BuildingReflection.GetWorkerIds`
- `public` `void` `Initialize(object building)` (line 56) - Fetches worker IDs and available races; resets refresh flag
- `public` `void` `Clear()` (line 67)
- `public` `bool` `HasWorkers()` (line 78)
- `public` `int` `GetItemCount()` (line 89) - Returns `_maxWorkers`
- `public` `int` `GetSubItemCount(int workerIndex)` (line 96) - Returns race count + 1 if slot is occupied (for Unassign option)
- `public` `void` `AnnounceItem(int itemIndex)` (line 112) - Force-refreshes race data before announcing; announces slot number + worker description or "Empty"
- `public` `void` `AnnounceSubItem(int workerIndex, int subItemIndex)` (line 141) - Announces "Unassign worker" or race name with available count and bonus
- `public` `bool` `PerformSubItemAction(int workerIndex, int subItemIndex)` (line 174) - Unassigns or assigns worker; returns true if caller should set `_navigationLevel = 1`
- `public` `string` `GetItemName(int itemIndex)` (line 240) - Returns worker description or slot label for type-ahead search
- `public` `string` `GetSubItemName(int workerIndex, int subItemIndex)` (line 255) - Returns "Unassign" or race name for type-ahead search
- `public` `void` `RefreshWorkerIds()` (line 280) - Re-fetches worker IDs via `GetWorkerIdsFunc`
- `public` `void` `RefreshAvailableRaces(bool force = false)` (line 289) - Fetches races with free workers (including zero-free); skips if already refreshed unless forced
- `private` `bool` `IsValidWorkerIndex(int index)` (line 296)
