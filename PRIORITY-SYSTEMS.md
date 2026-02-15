# Unsupported Priority Systems

Priority systems in the game that the mod does not yet expose. Recipe priorities (0-3, per-building) are already implemented in `ProductionNavigator`.

## 1. Construction Priority

Controls which buildings get constructed first in the worker queue.

- **Range**: -5 to +5 (clamped in `BasePriorityPanel.RefreshButtons`)
- **State field**: `BuildingState.constructionPriority` (`Eremite.Buildings/BuildingState.cs:40`)
- **Service methods**:
  - `BuildingsService.ChangePriorityTo(building, newPrio)` — individual (`Eremite.Services/BuildingsService.cs:587`)
  - `BuildingsService.ChangeGlobalPriorityTo(building, newPrio)` — all buildings of same model (`Eremite.Services/BuildingsService.cs:592`)
- **Game UI**: `ConstructionPriorityPanel` extends `BasePriorityPanel` (`Eremite.Buildings.UI/ConstructionPriorityPanel.cs`)
- **Queue effect**: `ConstructionQueue` sorts by `constructionPriority * -1` (`Eremite.Services/ConstructionQueue.cs:69`)

## 2. Resource Deposit Priority

Controls which resource nodes (trees, ore, etc.) workers gather from first.

- **Range**: -5 to +5
- **State field**: `ResourceDepositState.prio` (`Eremite.MapObjects/ResourceDepositState.cs:19`)
- **Service methods**:
  - `DepositsService.ChangePriorityTo(deposit, newValue)` — individual (`Eremite.Services/DepositsService.cs:184`)
  - `DepositsService.ChangeGlobalPriorityTo(deposit, newValue)` — all deposits of same resource (`Eremite.Services/DepositsService.cs:173`)
- **Game UI**: `DepositPriorityPanel` extends `BasePriorityPanel` (`Eremite.MapObjects.UI/DepositPriorityPanel.cs`)

## 3. Lake Priority

Controls which fishing spots workers prioritize. Same pattern as deposits.

- **Range**: -5 to +5
- **State field**: `LakeState.prio` (`Eremite.MapObjects/LakeState.cs:19`)
- **Service methods**:
  - `LakesService.ChangePriorityTo(lake, newValue)` — individual (`Eremite.Services/LakesService.cs:184`)
  - `LakesService.ChangeGlobalPriorityTo(lake, newValue)` — all lakes of same resource (`Eremite.Services/LakesService.cs:173`)
- **Game UI**: `LakePriorityPanel` extends `BasePriorityPanel` (`Eremite.MapObjects.UI/LakePriorityPanel.cs`)

## 4. Hearth Fuel Priority

Controls which fuel type the hearth burns first. Global per fuel type (not per building).

- **Range**: 0 to 3 (clamped in `HearthFuelPrioPanel.RefreshButtons`)
- **Storage**: `PrefsState.fuelsPriority` dictionary (keyed by fuel name)
- **Service methods**:
  - `HearthService.GetPriority(fuelName)` (`Eremite.Services/HearthService.cs:41`)
  - `HearthService.SetPriority(fuelName, prio)` (`Eremite.Services/HearthService.cs:46`)
- **Game UI**: `HearthFuelPrioPanel` (`Eremite.Buildings.UI/HearthFuelPrioPanel.cs`)

## 5. Ingredient Priority (**Implemented**)

Controls which ingredient workers prefer when a recipe accepts multiple options. Per-ingredient per-recipe. Implemented in `ProductionNavigator` — use +/- keys at ingredient level (level 3) to adjust priority 0-3.
