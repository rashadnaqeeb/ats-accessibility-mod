# Against the Storm — Game API Sketch

This document catalogs the game types, services, and members that the accessibility mod
accesses via reflection.  Each section describes a namespace/system, lists the key classes,
and notes exactly how the mod uses them.  Field/property access bindings are quoted so you
can search the game source directly.

---

## Table of Contents

1. [Controller Layer](#controller-layer)
2. [IGameServices — In-Game Service Registry](#igameservices--in-game-service-registry)
3. [IMetaServices — Meta-game Service Registry](#imetaservices--meta-game-service-registry)
4. [IWorldServices — World-Map Service Registry](#iworldservices--world-map-service-registry)
5. [Buildings System](#buildings-system)
6. [Recipe System](#recipe-system)
7. [Storage and Goods](#storage-and-goods)
8. [Orders System](#orders-system)
9. [Reputation and Impatience](#reputation-and-impatience)
10. [Calendar and Time](#calendar-and-time)
11. [Payments / Obligations](#payments--obligations)
12. [Cornerstones (Seasonal Perks)](#cornerstones-seasonal-perks)
13. [Reputation Rewards (Building Blueprints)](#reputation-rewards-building-blueprints)
14. [Trade System](#trade-system)
15. [Trade Routes](#trade-routes)
16. [Newcomers](#newcomers)
17. [Consumption Control (Needs)](#consumption-control-needs)
18. [Trends (Storage Operations)](#trends-storage-operations)
19. [Map and Terrain](#map-and-terrain)
20. [Glades System](#glades-system)
21. [Resources and Deposits](#resources-and-deposits)
22. [Blight System](#blight-system)
23. [Villagers and Actors](#villagers-and-actors)
24. [Narration / NPC Dialogue](#narration--npc-dialogue)
25. [World Map (Overworld)](#world-map-overworld)
26. [Meta State (Progression)](#meta-state-progression)
27. [Deeds / Goals](#deeds--goals)
28. [World Events / Seals](#world-events--seals)
29. [Game Result Popup](#game-result-popup)
30. [Popup Infrastructure](#popup-infrastructure)
31. [UI Components](#ui-components)
32. [Effect Model](#effect-model)
33. [Game Settings (Static Data)](#game-settings-static-data)

---

## Controller Layer

### `Eremite.Controller.GameController`
Entry point for all in-game state.  Lives on a GameObject, destroyed on scene change.

| Member | Kind | Notes |
|---|---|---|
| `static IsGameActive` | `bool` property | True while a settlement game is running |
| `static Instance` | `IGameController` property | The active controller; null between scenes |
| `GameServices` | `IGameServices` property | All in-game service instances |
| `CameraController` | `CameraController` property | Exposes `MoveTarget(Transform)` and related |

Mod access: `GameReflection.GetIsGameActive()`, `GetGameServices()`.

### `Eremite.Controller.MainController`
Persistent controller (survives scene changes via DontDestroyOnLoad).

| Member | Kind | Notes |
|---|---|---|
| `static Instance` | `MainController` property | |
| `AppServices` | `IAppServices` property | Contains `PopupsService` |

Mod access: `GameReflection.GetMainControllerInstance()`, `GetAppServices()`.

### `Eremite.Controller.MetaController`
Manages world-map / meta-game state.

| Member | Kind | Notes |
|---|---|---|
| `static Instance` | `MetaController` property | |
| `MetaServices` | `IMetaServices` property | All meta-game services |

Mod access: `GameReflection.GetMetaServices()`.

### `Eremite.Controller.WorldController`
Manages world-map scene.

| Member | Kind | Notes |
|---|---|---|
| `static Instance` | `WorldController` property | |
| `WorldServices` | `IWorldServices` property | |
| `CameraController` | `WorldCameraController` property | Has `target` field (Transform) |

Mod access: `WorldMapReflection` and `NarrationReflection`.

### `Eremite.MB` (base MonoBehaviour)

| Member | Kind | Notes |
|---|---|---|
| `protected static Settings` | property | The global static data asset |

Mod accesses this with `NonPublic | Static` binding to get game-wide model data.

---

## IGameServices — In-Game Service Registry

**Full type:** `Eremite.Services.IGameServices`
**Implementation:** `Eremite.Services.GameServices`

All properties below are `public instance` properties.  The mod retrieves each via
`GameReflection.GetService(PropertyInfo)`.

| Property | Service Type | Used for |
|---|---|---|
| `MapService` | `IMapService` | Tile queries, in-bounds checks |
| `VillagersService` | `IVillagersService` | Villager dictionary, race data |
| `BuildingsService` | `IBuildingsService` | Workshops, hearths, blights, houses |
| `BuildingsFinderService` | `IBuildingsFinderService` | (not directly used by mod) |
| `RoadsService` | `IRoadsService` | (not directly used) |
| `StorageService` | `IStorageService` | Good amounts in main storage |
| `NewsService` | `INewsService` | Alert announcements |
| `GoodsService` | `IGoodsService` | Fuel list (hearth) |
| `StateService` | `IStateService` | Game effects state, payments, trends |
| `EffectsService` | `IEffectsService` | Consumption control blocking, effects list |
| `GameTimeService` | `IGameTimeService` | Current game time float |
| `CalendarService` | `ICalendarService` | Season, year, seconds to date |
| `ResolveService` | `IResolveService` | Villager resolve values by race |
| `OrdersService` | `IOrdersService` | Active orders, pick/complete |
| `ResourcesService` | `IResourcesService` | Natural resources on map |
| `DepositsService` | `IDepositsService` | Resource deposit nodes |
| `LakesService` | `ILakesService` | Fishing spots |
| `SpringsService` | `IspringsService` | Water spring nodes |
| `GladesService` | `IGladesService` | Glade discovery, danger levels |
| `ConstructionService` | `IConstructionService` | Show index for buildings |
| `TradeService` | `ITradeService` | Trader panel, assault, buy/sell |
| `GameBlackboardService` | `IGameBlackboardService` | `OrderPickPopupRequested` observable |
| `MonitorsService` | `IMonitorsService` | (event subscription) |
| `GameContentService` | `IGameContentService` | `IsUnlocked(BuildingModel)` |
| `ReputationService` | `IReputationService` | Reputation value, penalty, sources |
| `BiomeService` | `IBiomeService` | Current biome model |
| `TradeRoutesService` | `ITradeRoutesService` | Town standings, offers, active routes |
| `ReputationRewardsService` | `IReputationRewardsService` | Building blueprint picks |
| `RecipesService` | `IRecipesService` | Recipe list by building |
| `WorkplacesService` | `IWorkplacesService` | (not directly used) |
| `NewcomersService` | `INewcomersService` | Newcomer group choices |
| `OreService` | `IOreService` | Ore veins on map |
| `NeedsService` | `INeedsService` | Raw-food / need permissions |
| `TimeScaleService` | `ITimeScaleService` | Pause, unpause, game speed |
| `HostilityService` | `IHostilityService` | Impatience points, level |
| `RacesService` | `IRacesService` | Race models, characteristics |
| `WorkshopsService` | `IWorkshopsService` | Global production limits |
| `CornerstonesService` | `ICornerstonesService` | Seasonal perk picks, reroll, extend |
| `HearthService` | `IHearthService` | Fuel burn, fuel enable/priority |
| `BlightService` | `IBlightService` | Global cysts, predicted corruption |
| `RelicsService` | `IRelicsService` | (not directly used; relics via building) |
| `ConditionsService` | `IConditionsService` | `IsBlightActive()` |
| `RainpunkService` | `IRainpunkService` | Water tank levels, engine control |
| `PaymentsService` | `IPaymentsService` | `Pay`, `CanPay`, `GetModel` |
| `StorageOperationsService` | `IStorageOperationsService` | Display names for storage ops |
| `ActorsService` | `IActorsService` | `GetActor(id)` for worker details |
| `GameModelService` | `IGameModelService` | `GetEffect(name)` by string key |
| `GameGoalsService` | `IGameGoalsService` | Completed goals (game result) |

### Key `ITimeScaleService` Methods

| Method | Signature | Notes |
|---|---|---|
| `IsPaused` | `() → bool` | |
| `Pause` | `(bool userBased)` | |
| `Unpause` | `(bool userBased)` | |
| `Change` | `(float scale, bool userBased, bool force = false)` | Speeds: 0=paused, 1=1x, 1.5, 2, 3 |

### Key `IMapService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetField` | `(int x, int y) → Field` | Returns tile data |
| `GetObjectOn` | `(int x, int y) → IMapObject` | Building/resource on tile |
| `InBounds` | `(int x, int y) → bool` | |
| `Fields` | property `→ Map<Field>` | Has `.width` and `.height` fields |

### Key `IBuildingsService` Members

| Member | Kind | Notes |
|---|---|---|
| `Workshops` | `Dictionary<int,Workshop>` property | All built workshops |
| `BlightPosts` | `Dictionary<...>` property | Built blight posts |
| `Hearths` | `Dictionary<int,Hearth>` property | All hearths |
| `Houses` | property | |
| `Institutions` | property | |
| `Decorations` | property | |
| `BuildingsBlights` | property | `List<BuildingBlight>` |
| `GetMainHearth()` | method | Returns the Ancient Hearth |
| `HasBuilding(int id)` | method | |
| `GetBuilding(int id)` | method | |

### Key `IWorkshopsService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetGlobalLimitFor` | `(string goodName) → int` | 0 = no limit |
| `SetGlobalLimitFor` | `(string goodName, int limit)` | |

### Key `IStorageService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetAmount` | `(string goodName) → int` | Amount in main storage |
| `Main` | property `→ MainStorage` | Has its own `GetAmount` |

### Key `IOrdersService` Members

| Member | Kind | Notes |
|---|---|---|
| `Orders` | `List<OrderState>` property | |
| `CanComplete(state, model)` | method `→ bool` | |
| `CompleteOrder(state, model, bool force)` | method | |
| `OrderPicked(state, pick)` | method | |
| `GetPicksFor(state)` | method `→ List<OrderPickState>` | |
| `SwitchOrderTracking(state, bool)` | method | |

### Key `IPaymentsService` Methods

| Method | Signature | Notes |
|---|---|---|
| `Pay(PaymentState)` | void | |
| `CanPay(PaymentState)` | `→ bool` | |
| `GetModel(PaymentState)` | `→ PaymentEffectModel` | Type/source label |

### Key `IHearthService` Methods

| Method | Signature | Notes |
|---|---|---|
| `CanBeBurned(string goodName)` | `→ bool` | |
| `SetCanBeBurned(string, bool)` | void | |
| `GetPriority(string goodName)` | `→ int` | |
| `SetPriority(string, int)` | void | |

### Key `IReputationRewardsService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetCurrentPicks()` | `→ List<ReputationReward>` | |
| `CanAffordReroll()` | `→ bool` | |
| `Reroll()` | void | |
| `GetRerollPrice()` | `→ Good` | |
| `CanExtend()` | `→ bool` | |
| `CanAffordExtend()` | `→ bool` | |
| `Extend()` | void | |

### Key `ICornerstonesService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetCurrentPick()` | `→ RewardPickState` | Has `.options` (EffectModel[]) |
| `GetRerollsLeft()` | `→ int` | |
| `CanExtend()` | `→ bool` | |
| `CanAffordExtend()` | `→ bool` | |
| `Extend()` | void | |
| `GetDeclinePayoff()` | `→ Good` | |
| `RemoveFromActive(EffectModel)` | void | Remove a cornerstone |

### Key `INewcomersService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetCurrentNewcomers()` | `→ List<NewcomersGroup>` | |
| `PickGroup(...)` | void | Accept a group |

### Key `INeedsService` Methods

| Method | Signature | Notes |
|---|---|---|
| `IsPermited(string rawFood)` | `→ bool` | |
| `SetPermision(string rawFood, bool)` | void | |
| `IsPermited(RaceModel, NeedModel)` | `→ bool` | |
| `SetPermision(RaceModel, NeedModel, bool)` | void | |
| `IsAllRawFoodPermited()` | `→ bool` | |
| `IsAllRawFoodProhibited()` | `→ bool` | |
| `GetCurrentResolveImpact(...)` | `→ float` | |
| `GetMaxResolveImpact(...)` | `→ float` | |

### Key `IRacesService` Members

| Member | Kind | Notes |
|---|---|---|
| `Races` | `RaceModel[]` property | |
| `IsRevealed(RaceModel)` | method `→ bool` | |

### Key `IEffectsService` Methods

| Method | Signature | Notes |
|---|---|---|
| `IsConsumptionControlBlocked()` | `→ bool` | |
| `GetEffectsDisplayList(...)` | | |

### Key `ICalendarService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetSecondsLeftTo(GameDate)` | `→ float` | Seconds until a date |

### Key `IResolveService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetResolveFor(string race)` | `→ float` | |
| `GetMinResolveForReputation(string race)` | `→ float` | |
| `GetTargetResolveFor(string race)` | `→ float` | |
| `Effects` | property `→ Dictionary` | |

### Key `ITradeService` Members

See [Trade System](#trade-system) section.

### Key `IRainpunkService` Methods

| Method | Signature | Notes |
|---|---|---|
| `CountWaterLeft(WaterModel)` | `→ int` | |
| `CountTanksCapacity(WaterModel)` | `→ int` | |
| `GetWaterPerCysts(Workshop)` | `→ float` | |
| `IsWaterSpawningBlight(Workshop)` | `→ bool` | |

---

## IMetaServices — Meta-game Service Registry

**Type:** `Eremite.Services.IMetaServices`
Path: `MetaController.Instance.MetaServices`

| Property | Service Type | Used for |
|---|---|---|
| `MetaStateService` | `IMetaStateService` | Goals, economy, level data |
| `GoalsService` | `IGoalsService` | `RewardGoal(GoalState)` |
| `WorldStateService` | `IWorldStateService` | Cycle year, seals |

### Key `IMetaStateService` Properties

| Property | Type | Notes |
|---|---|---|
| `Goals` | `MetaGoalsState` | Has `.goals` (List<GoalState>) |
| `Economy` | `MetaEconomyState` | Has `.currentCycleExp`, `.metaCurrencies` |
| `Level` | `LevelState` | Has `.level`, `.exp`, `.targetExp` |
| `Capital` | `CapitalState` | Has `.currentCycleUpgrades` |
| `State` | `MetaState` | Has `.isIronman` |

---

## IWorldServices — World-Map Service Registry

**Type:** `Eremite.Services.World.IWorldServices`
Path: `WorldController.Instance.WorldServices`

| Property | Service Type | Used for |
|---|---|---|
| `WorldMapService` | `IWorldMapService` | Field queries, path-finding |
| `WorldBlackboardService` | `IWorldBlackboardService` | `OnFieldClicked` observable |
| `WorldCalendarService` | `IWorldCalendarService` | `IsStormAboutToCome()` |
| `WorldSealsService` | `IWorldSealsService` | Seal affordability, completions |
| `NarrationBlackboardService` | `INarrationBlackboardService` | Dialogue / branch observables |
| `NarrationService` | `INarrationService` | NPC data, important topics |

### Key `IWorldMapService` Methods

| Method | Signature | Notes |
|---|---|---|
| `GetField(Vector2Int)` | `→ WorldField` | |
| `IsRevealed(Vector2Int)` | `→ bool` | |
| `CanBePicked(Vector2Int)` | `→ bool` | |
| `InBounds(Vector2Int)` | `→ bool` | |
| `IsCapital(Vector2Int)` | `→ bool` | |
| `IsCity(Vector2Int)` | `→ bool` | |
| `GetDistanceToStartTown(...)` | `→ int` | |
| `FindLastTown(...)` | `→ WorldField` | |
| `Fields` | property `→ Map` | |

### Key `IWorldStateService` Members

| Member | Kind | Notes |
|---|---|---|
| `HasModifier(Vector2Int, string)` | method `→ bool` | |
| `HasEvent(Vector2Int, string)` | method `→ bool` | |
| `HasSeal(Vector2Int, string)` | method `→ bool` | |
| `GetModifierModel(...)` | method | |
| `GetEventModel(...)` | method | |
| `GetSealModel(...)` | method | |
| `GetDisplayNameFor(...)` | method `→ string` | |
| `Fields` | property | |
| `Cycle` | property `→ CycleState` | |
| `Seals` | property | |

### Key `CycleState` Fields

| Field | Type | Notes |
|---|---|---|
| `year` | `int` | |
| `yearsInCycle` | `int` | |
| `gamesPlayed` | `int` | |
| `gamesWon` | `int` | |
| `sealFragments` | `int` | |

---

## Buildings System

### `Eremite.Buildings.Building`
Base class for all buildings.

| Member | Kind | Notes |
|---|---|---|
| `BuildingModel` | property | Static data model |
| `BuildingState` | property | Runtime state |
| `Id` | `int` property | Unique instance ID |
| `DisplayName` | `string` property | Localized name |
| `IsFinished()` | method `→ bool` | Construction complete |
| `Field` | `Vector2Int` property | Map position |

### `Eremite.Buildings.BuildingModel`
ScriptableObject — static configuration.

| Member | Kind | Notes |
|---|---|---|
| `displayName` | `LocaText` field | Localized name |
| `Name` | `string` property | Internal key |
| `Description` | `string` property | |
| `ListDescription` | `string` property | Short description (virtual) |
| `tags` | `BuildingTagModel[]` field | Building category tags |
| `category` | `BuildingCategoryModel` field | |
| `HasAccessTo()` | method `→ bool` | DLC/progression check |

### `Eremite.Buildings.BuildingState`
Runtime state persisted in save.

| Field | Type | Notes |
|---|---|---|
| `finished` | `bool` | Construction done |
| `isSleeping` | `bool` | Paused/sleeping |

### `Eremite.Buildings.ProductionBuilding`
Extends `Building` — all buildings that produce goods.

| Member | Kind | Notes |
|---|---|---|
| `Workers` | property `→ IReadOnlyList<Worker>` | Assigned worker slots |
| `ProductionStorage` | property `→ BuildingStorage` | Output storage |
| `ProductionBuildingState` | property | Cast of base state |
| `Profession` | property | `ProfessionModel` |
| `Workplaces` | property | List of `Workplace` |

### `Eremite.Buildings.IWorkshop`
Interface for workshop buildings with recipes.

| Member | Kind | Notes |
|---|---|---|
| `Recipes` | `IReadOnlyList<WorkshopRecipeState>` property | |
| `BaseModel` | `BuildingModel` property | |
| `Base` | `Building` property | |
| `IngredientsStorage` | `BuildingIngredientsStorage` property | Input storage |
| `SwitchProductionOf(WorkshopRecipeState)` | void method | Toggle recipe on/off |

### `Eremite.Buildings.WorkshopRecipeState`
Runtime state for a single recipe slot.

| Field | Type | Notes |
|---|---|---|
| `model` | `string` | Internal key → `Settings.GetWorkshopRecipe(name)` |
| `active` | `bool` | Recipe enabled/disabled |
| `limit` | `int` | Per-building recipe limit |
| `isLimitLocal` | `bool` | |
| `ingredients` | `IngredientState[][]` | Per-ingredient priority options |

### `Eremite.Buildings.WorkshopRecipeModel`
Static configuration for a workshop recipe.

| Field | Type | Notes |
|---|---|---|
| `producedGood` | `GoodRef` | Output good + amount |
| `requiredGoods` | `GoodsSet[]` | Input ingredient sets |
| `productionTime` | `float` | |
| `grade` | `RecipeGradeModel` | Has `.level` (stars) |

### `Eremite.Buildings.RecipeModel` (base)

| Field | Type | Notes |
|---|---|---|
| `grade` | `RecipeGradeModel` | |

### `Eremite.Buildings.IngredientState`

| Field | Type | Notes |
|---|---|---|
| `good` | `Good` struct | |
| `allowed` | `bool` | |
| `priority` | `int` | |

### `Eremite.Buildings.BuildingStorage` / `ProductionStorage`
Output goods container.

| Member | Kind | Notes |
|---|---|---|
| `Goods` | property `→ BuildingGoodsCollection` | |
| `GetDeliveryState(string goodName)` | method `→ GoodDeliveryState` | |
| `SwitchForceDelivery(string, GoodDeliveryState)` | method | |
| `SwitchConstantForceDelivery(string, GoodDeliveryState)` | method | |

### `Eremite.Buildings.BuildingIngredientsStorage`

| Field | Type | Notes |
|---|---|---|
| `goods` | `GoodsCollection` | Has `.goods` dict `Dictionary<string, int>` |

### `Eremite.Buildings.BuildingPanel`
UI component with the currently-open building.

| Field | Type | Notes |
|---|---|---|
| `currentBuilding` | `static Building` | The building shown in the panel |

Events (observable properties):
- `OnBuildingPanelShown` → called when panel opens
- `OnBuildingPanelClosed` → called when panel closes

### Building Subtypes accessed by mod

| Type | Full Name | Key members accessed |
|---|---|---|
| `Hearth` | `Eremite.Buildings.Hearth` | `state` (HearthState), `model` (HearthModel), `IsMainHearth()`, `GetRange()`, `GetCorruptionRate()`, `IsInRange(Building)`, sacrifice recipes |
| `Camp` | `Eremite.Buildings.Camp` | `state` (CampState), `SwitchProductionOf`, `SetMode(CampMode)` |
| `Farm` | `Eremite.Buildings.Farm` | `state`, `CountSownFieldsInRange()`, `CountPlownFieldsInRange()`, `CountAllReaveleadFieldsInRange()`, `SwitchProductionOf` |
| `FishingHut` | `Eremite.Buildings.FishingHut` | `state` (FishingHutState), `model` (FishingHutModel), `ChangeMode(FishmanBaitMode)` |
| `Relic` | `Eremite.Buildings.Relic` | `state` (RelicState), `model` (RelicModel), `StartInvestigation()`, `Cancel()`, `CanCancel()` |
| `Port` | `Eremite.Buildings.Port` | `state` (PortState), expedition level/rewards/goods |
| `House` | `Eremite.Buildings.House` | `state` (HouseState), `GetHousingPlaces()`, `IsFull()` |
| `Shrine` | `Eremite.Buildings.Shrine` | `state` (ShrineState), `model` (ShrineModel), `UseEffect(...)` |
| `Poro` | `Eremite.Buildings.Poro` | `state`, needs, happiness, `CanFulfill`, `Fulfill`, `GatherProducts` |
| `RainCatcher` | `Eremite.Buildings.RainCatcher` | `GetCurrentWaterType()` |
| `Extractor` | `Eremite.Buildings.Extractor` | `state`, `model`, `GetWaterType()` |
| `Hydrant` | `Eremite.Buildings.Hydrant` | `state`, `model` |
| `Institution` | `Eremite.Buildings.Institution` | `state` (InstitutionState), `model` (InstitutionModel), `ChangeIngredientFor(...)` |
| `Workshop` | `Eremite.Buildings.Workshop` | `state` (WorkshopState), rainpunk engine fields |
| `UpgradableBuilding` | `Eremite.Buildings.UpgradableBuilding` | upgrade levels, options, required goods |
| `BlightPost` | `Eremite.Buildings.BlightPost` | treated as workshop with recipes |

### `Eremite.Buildings.BuildingBlight`

| Member | Kind | Notes |
|---|---|---|
| `Owner` | `Building` property | |
| `GetActiveCysts()` | method `→ int` | |

### `Eremite.Buildings.Hearth` Key State

**HearthState** fields:
- `burningTimeLeft` (float)
- `corruption` (float)
- `hubIndex` (int) — which hearth hub tier
- `workers` — assigned workers
- `sacrificeRecipes` — `List<HearthSacrificeState>`

**HearthModel** fields:
- `maxBurningTime` (float)
- `minTimeToShowNoFuel` (float)
- `extraRecipes` — `HearthNeedRecipeModel[]` (The Commons)
- `extraRecipesUnlockPrice` — `GoodRef`

### UpgradableBuilding Members

**UpgradableBuildingModel** fields:
- `levels` — `BuildingLevelModel[]`

**BuildingLevelModel** fields:
- `requiredGoods` — `GoodsSet[]`
- `options` — `BuildingPerkModel[]`

**UpgradableBuildingState** fields:
- `level` (int)
- `upgrades` — `bool[][]`

---

## Recipe System

### `Eremite.Services.IRecipesService`

| Method | Signature | Notes |
|---|---|---|
| `GetRecipesFor(string buildingName)` | `→ List<string>` | Recipe keys for a building |

### `Eremite.Services.IConstructionService`

| Method | Signature | Notes |
|---|---|---|
| `GetShowIndex(Building)` | `→ int` | Ordinal "#1", "#2" for same-type buildings |

### `Eremite.Buildings.IWorkshop`
See [Buildings System](#buildings-system).

---

## Storage and Goods

### `Eremite.Model.Good` (struct)

| Field | Type | Notes |
|---|---|---|
| `name` | `string` | Internal key |
| `amount` | `int` | Quantity |

### `Eremite.Model.GoodModel`

| Field/Property | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` field | Localized name → `GetLocaText()` |
| `Name` | `string` property | Internal key |
| `category` | `GoodCategoryModel` field | |

### `Eremite.Model.GoodRef`

| Field | Type | Notes |
|---|---|---|
| `good` | `GoodModel` | (cached as `GameReflection.GoodRefGoodField`) |
| `amount` | `int` | (cached as `GameReflection.GoodRefAmountField`) |
| `Name` | `string` property | |

### `Eremite.Model.GoodsSet`

| Field | Type | Notes |
|---|---|---|
| `goods` | `GoodRef[]` | One entry = alternative ingredient option |

### `Eremite.Model.GoodsSetTable`

| Field | Type | Notes |
|---|---|---|
| `sets` | `GoodsSet[]` | |

### `Eremite.Model.LocaText`
Localization wrapper.

| Property | Type | Notes |
|---|---|---|
| `Text` | `string` | The localized string |

Accessed via `GameReflection.GetLocaText(object)`.

### `Eremite.Services.IStorageService`

| Member | Kind | Notes |
|---|---|---|
| `GetAmount(string goodName)` | method `→ int` | |
| `Main` | property `→ MainStorage` | |

---

## Orders System

### `Eremite.Model.Orders.OrderState`
Runtime state for an active order.

| Field | Type | Notes |
|---|---|---|
| `model` | `string` | Key → `Settings.GetOrder(name)` |
| `picked` | `bool` | Player selected option |
| `completed` | `bool` | |
| `isFailed` | `bool` | |
| `timeLeft` | `float` | Remaining seconds |
| `tracked` | `bool` | Pin in HUD |
| `picks` | list | Available pick options |
| `rewards` | `List<string>` | Effect name keys |
| `shouldBeFailable` | `bool` | |

Inherited from `BaseOrderState`:
- `started` (bool)
- `objectives` — `ObjectiveState[]`
- `startTime` (float)

### `Eremite.Model.Orders.OrderPickState`

| Field | Type | Notes |
|---|---|---|
| `model` | `string` | |
| `setIndex` | `int` | Index into OrderModel.logicsSets |
| `failed` | `bool` | |
| `rewards` | `List<string>` | |

### `Eremite.Model.Orders.OrderModel`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |
| `canBeFailed` | `bool` | |
| `timeToFail` | `float` | |
| `reputationReward` | `EffectModel` | |
| `unlockAfter` | `OrderModel` | |
| `logicsSets` | `OrderLogicsSet[]` | |

Methods:
- `GetLogics(OrderState) → OrderLogic[]`
- `GetLogics(int setIndex) → OrderLogic[]`

### `Eremite.Model.Orders.OrderLogic`

| Member | Kind | Notes |
|---|---|---|
| `DisplayName` | `string` property | Localized objective name |
| `Description` | `string` property | |
| `HasStoredAmount` | `bool` property | |
| `GetStoredAmount` | `int` property | |
| `GetObjectiveText(ObjectiveState)` | method `→ string` | |
| `GetAmountText()` | method `→ string` | Total quantity (localized) |
| `GetAmountText(ObjectiveState)` | method `→ string` | Progress quantity |
| `IsCompleted(ObjectiveState)` | method `→ bool` | |
| `GetWarningText()` | method `→ string` | Missing-building warning |

### `Eremite.Model.Orders.ReputationGainedFromSourceLogic`
Subtype of `OrderLogic`.

| Field | Type | Notes |
|---|---|---|
| `source` | enum (int) | 1=Orders, 2=Resolve, 3=Relics |

### Popup Types for Orders

| Type | Full Name | Notes |
|---|---|---|
| `OrdersPopup` | `Eremite.View.HUD.Orders.OrdersPopup` | Main orders list |
| `OrderPickPopup` | `Eremite.View.HUD.Orders.OrderPickPopup` | Option selection |

`OrderPickPopup` private field `order` → the `OrderState` being shown.

---

## Reputation and Impatience

### `Eremite.Services.ReputationService`
Accessed by concrete class name (not interface) for these properties.

| Member | Kind | Notes |
|---|---|---|
| `Reputation` | `ReactiveProperty<float>` property | Current rep |
| `ReputationPenalty` | `ReactiveProperty<float>` property | Impatience |
| `State` | `GameObjectivesState` property | Has `gracePeriodLeft` field |
| `GetReputationToWin()` | method `→ float` | |
| `GetReputationPenaltyToLoose()` | method `→ float` | |
| `GetReputationGainedFrom(source)` | method `→ float` | |
| `GetReputationPenaltyPerSec()` | method `→ float` | |
| `GetBaseReputationPenaltyPerSec()` | method `→ float` | |

### `Eremite.Services.HostilityService`

| Member | Kind | Notes |
|---|---|---|
| `Points` | `ReactiveProperty<int>` property | |
| `Level` | `ReactiveProperty<int>` property | |
| `GetSourceAmount(source)` | method `→ int` | |
| `GetPointsFor(source)` | method `→ int` | |
| `GetPointsLeftToNextLevel()` | method `→ int` | |

### Enum `Eremite.Services.ReputationChangeSource`
Values accessed as int. Used with `RepGetGainedFromMethod`.

### Enum `Eremite.Services.HostilitySource`
Values accessed as int. Used with `HostGetSourceAmountMethod`.

---

## Calendar and Time

### `Eremite.Services.ICalendarService`

| Method | Signature | Notes |
|---|---|---|
| `GetSecondsLeftTo(GameDate)` | `→ float` | |

### `Eremite.Model.State.GameDate`

| Field | Type | Notes |
|---|---|---|
| `year` | `int` | |
| `season` | enum (int) | 0=Drizzle, 1=Clearance, 2=Storm |

### `Eremite.Services.IGameTimeService`

| Property | Type | Notes |
|---|---|---|
| `Time` | `float` | Monotonic game time |

---

## Payments / Obligations

### `Eremite.Model.State.PaymentState`

| Field | Type | Notes |
|---|---|---|
| `payment` | `Good` struct | Name + amount |
| `dueDate` | `GameDate` | |
| `autoPaymentType` | `AutoPaymentType` enum | 0=None, 1=Instant, 2=End |
| `model` | `string` | Key → PaymentEffectModel |
| `penaltyModel` | `string` | Key → EffectModel |

### `Eremite.Model.Effects.Payment.PaymentEffectModel`

| Field | Type | Notes |
|---|---|---|
| `typeLabel` | `LabelModel` | "Tax", "Tithe", etc. |
| `sourceLabel` | `LabelModel` | Source description |

### `Eremite.Model.LabelModel`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |

### `Eremite.Model.State.AutoPaymentType` (enum)
Values: 0=None, 1=Instant, 2=LastMinute.

### `Eremite.Services.IStateService`

| Property | Type | Notes |
|---|---|---|
| `Effects` | `EffectsState` | Has `.payments` (List<PaymentState>) |
| `Trends` | `TrendsState` | Storage operation history |
| `Actors` | `ActorsState` | Has `.rawFoodPermits` |

### `Eremite.View.Popups.Recipes.PaymentsPopup`
Popup type for detection. Matched via `IsInstanceOfType`.

---

## Cornerstones (Seasonal Perks)

### `Eremite.Model.RewardPickState`

| Field | Type | Notes |
|---|---|---|
| `options` | `EffectModel[]` | The 3-4 options to pick from |
| `viewConfiguration` | `string` | Key for NPC dialogue config |

### `Eremite.Model.ViewsConfigurations.CornerstonesViewConfiguration`

| Field | Type | Notes |
|---|---|---|
| `npcName` | `LocaText` | |
| `npcDialogue` | `LocaText` | |

### Seasonal perk popup types

| Type | Full Name | Notes |
|---|---|---|
| `RewardPickPopup` | `Eremite.View.Popups.RewardPickPopup` | Normal cornerstone pick |
| `CornerstonesLimitPickPopup` | `Eremite.View.Popups.CornerstonesLimitPick.CornerstonesLimitPickPopup` | Choose-one-to-remove |

`RewardPickPopup` private methods:
- `OnRewardPicked(EffectModel)` — select a cornerstone
- `Reroll()` — spend resources to reroll
- `Skip()` — decline all
- `defaultConfiguration` field

`CornerstonesLimitPickPopup` private method:
- `FinishTask(EffectModel)` — remove a cornerstone

### Season extend cost
Path: `BiomeService.CurrentBiome.seasons[n].seasonRewardsExtendPrice` (Good struct).

---

## Reputation Rewards (Building Blueprints)

### `Eremite.Model.State.ReputationReward`

| Field | Type | Notes |
|---|---|---|
| `building` | `string` | Key → `Settings.GetBuilding(name)` |

### Popup type: `Eremite.View.Popups.ReputationRewardsPopup`
Private methods used:
- `OnBuildingPicked(BuildingModel)` — select a blueprint
- `Reroll()` — reroll

---

## Trade System

### `Eremite.Services.ITradeService`
Accessed via `EventReflection.TradeServiceProperty`.

Fields accessed through the trader panel UI and service.

**Trader structure** (accessed by `TradeReflection`):

| Data type | Accessed via | Notes |
|---|---|---|
| `TradingGoodInfo` | mod struct | Selling goods |
| `PerkInfo` | mod struct | Tradeable perks/effects |
| `AssaultResult` | mod struct | Assault outcome |

Key trader operations:
- Get trader goods (buy side, sell side)
- Get available perks/effects with prices
- Execute trades
- Attempt assault

### Popup: `Eremite.View.Popups.Recipes.TraderPanel`
The trade popup.

---

## Trade Routes

### `Eremite.Services.ITradeRoutesService`
All data flows via `IStateService.State.Trade`.

### `Eremite.Model.State.TradeState`

| Field | Type | Notes |
|---|---|---|
| `tradeTowns` | `List<TradeTownState>` | |
| `routes` | `List<RouteState>` | Active in-progress routes |

### `Eremite.Model.State.TradeTownState`

| Field | Type | Notes |
|---|---|---|
| `id` | `int` | |
| `name` | `string` | |
| `biome` | `string` | |
| `faction` | `string` | |
| `distance` | `int` | |
| `standingLevel` | `int` | |
| `isMaxStanding` | `bool` | |
| `currentStanding` | `int` | |
| `valueForLevelUp` | `int` | |
| `offers` | `List<TownOfferState>` | |

### `Eremite.Model.State.TownOfferState`
Fields: good name, amount, fuel name/amount, price, travel time, multiplier, accepted, etc.

### `Eremite.Model.State.RouteState`
Fields: town id/name, good, price, progress (0-1), time remaining, `CanCollect`.

### `Eremite.Model.State.PrefsState`

| Field | Type | Notes |
|---|---|---|
| `autoCollect` | `bool` | |
| `onlyAvailable` | `bool` | |

---

## Newcomers

### `Eremite.Model.State.NewcomersGroup`

| Field | Type | Notes |
|---|---|---|
| `races` | `string[]` | Race names in the group |
| `goods` | `Good[]` | Welcome gifts |

### Popup: `Eremite.View.HUD.NewcomersPopup`

---

## Consumption Control (Needs)

### `Eremite.Model.NeedModel`

| Member | Kind | Notes |
|---|---|---|
| `canBeProhibited` | `bool` field | |
| `category` | `NeedCategoryModel` field | |
| `DisplayName` | `string` property | |

### `Eremite.Model.NeedCategoryModel`

| Field | Type | Notes |
|---|---|---|
| `isHouseBased` | `bool` | |
| `displayName` | `LocaText` | |

### `Eremite.Model.RaceModel`

| Member | Kind | Notes |
|---|---|---|
| `displayName` | `LocaText` field | |
| `needs` | `NeedModel[]` field | |
| `characteristics` | `RaceCharacteristicModel[]` field | Building bonuses |
| `passiveEffectLongDesc` | `LocaText` field | Firekeeper effect |
| `HasNeed(NeedModel)` | method `→ bool` | |

### `Eremite.Model.RaceCharacteristicModel`

| Field | Type | Notes |
|---|---|---|
| `tag` | `BuildingTagModel` | |
| `effect` | `VillagerPerkModel` | |
| `globalEffect` | `EffectModel` | |
| `buildingPerk` | `BuildingPerkModel` | |

### Popup: `Eremite.View.Popups.Consumption.ConsumptionPopup`

---

## Trends (Storage Operations)

### `Eremite.Model.State.TrendsState`

| Field | Type | Notes |
|---|---|---|
| `goodsOperations` | dict `string → List<StorageOperation>` | |
| `totalTicks` | `int` | |

### `Eremite.Model.StorageOperation`

| Field | Type | Notes |
|---|---|---|
| `amount` | `int` | |
| `trendTick` | `int` | |

### `Eremite.Services.IStorageOperationsService`

| Method | Signature | Notes |
|---|---|---|
| `GetDisplayName(...)` | `→ string` | Source display name |

### Popup: `Eremite.View.Trends.TrendsPopup`
Private field `currentGood` — the good currently being displayed.

---

## Map and Terrain

### `Eremite.Services.IMapService`
See [IGameServices](#igameservices--in-game-service-registry) section for methods.

### Field (tile) object (accessed lazily from `GetField`)

| Property | Notes |
|---|---|
| `Type` | FieldType model (has `displayName`, `name`) |
| `IsTraversable` | bool |

### `Eremite.Services.SpringsService`

| Method | Signature | Notes |
|---|---|---|
| `RemoveSpringsFromGrid()` | void | Before extractor placement check |
| `ReturnSpringsOnGrid()` | void | After extractor placement check |

---

## Glades System

### `Eremite.Services.IGladesService`

| Member | Kind | Notes |
|---|---|---|
| `Glades` | property `→ List<Glade>` | |
| `GetGlade(Vector2Int)` | method `→ Glade` | |

### Glade object (accessed lazily)

| Field | Type | Notes |
|---|---|---|
| `wasDiscovered` | `bool` | |
| `dangerLevel` | enum | |
| `fields` | list | |
| `hasRewardChase` | `bool` | |
| `rewardChaseEnd` | float | |
| `relics` | list | |

---

## Resources and Deposits

### Services
- `IResourcesService` — natural resources; has `NaturalResources` dict
- `IDepositsService` — deposit nodes; has `Deposits` dict
- `IOreService` — ore veins; has `Ores` dict
- `ILakesService` — fishing spots; has `Lakes` dict
- `ISpringsService` — water springs; has `Springs` dict

All accessed via lazy property reflection on the service instance.

---

## Blight System

### `Eremite.Services.IBlightService`

| Method | Signature | Notes |
|---|---|---|
| `GetGlobalActiveCysts()` | `→ int` | |
| `GetPredictedPercentageCorruption()` | `→ float` | 0-1 |
| `CountGlobalFreeCysts()` | `→ int` | |

### `Eremite.Services.IConditionsService`

| Method | Signature | Notes |
|---|---|---|
| `IsBlightActive()` | `→ bool` | |

### `Eremite.Buildings.Hearth` (blight side)

| Method | Signature | Notes |
|---|---|---|
| `GetCorruptionRate()` | `→ float` | 0-1 |

---

## Villagers and Actors

### `Eremite.Services.IVillagersService`

| Member | Kind | Notes |
|---|---|---|
| `Villagers` | `Dictionary<int, Villager>` property | All villagers |
| `Races` | `Dictionary<string, ...>` property | (VillagersService concrete) |
| `GetDefaultProfessionAmount(race)` | method | |
| `GetDefaultProfessionVillager(race, building)` | method | |
| `SetProfession(villager, profession, building, workplace)` | method | |
| `ReleaseFromProfession(villager)` | method | |
| `GetVillager(id)` | method | |

### `Eremite.Services.IActorsService`

| Method | Signature | Notes |
|---|---|---|
| `GetActor(int id)` | `→ Actor` | |

### Actor / Villager object (accessed lazily)

| Member | Kind | Notes |
|---|---|---|
| `GetDisplayName()` | method `→ string` | |
| `ActorState` | property | |
| `state` | field | VillagerState |
| `GetTaskDescription()` | method `→ string` | |

### `VillagerState` fields

| Field | Type | Notes |
|---|---|---|
| `name` | `string` | |
| `race` | `string` | |
| `lossType` | enum | |
| `lossReasonKey` | `string` | |
| `lastWorkId` | `int` | |

---

## Narration / NPC Dialogue

### `Eremite.Services.Narration.INarrationService`

| Method | Signature | Notes |
|---|---|---|
| `GetNPC()` | `→ NPCModel` | |
| `HasAnyImportantTopics()` | `→ bool` | |

### `Eremite.Services.Narration.INarrationBlackboardService`

| Property | Type | Notes |
|---|---|---|
| `OnDialogueRequested` | `IObservable<DialogueModel>` | |
| `OnBranchRequested` | `IObservable<BranchModel>` | |

### `Eremite.Model.Narration.NPCModel`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |
| `title` | `LocaText` | |

### `Eremite.Model.Narration.DialogueModel`

| Member | Kind | Notes |
|---|---|---|
| `text` | `LocaText` field | |
| `HasTransition` | `bool` property | |
| `ExecuteTransition()` | method | Advance dialogue |
| `GetText()` | method `→ string` | |

### `Eremite.Model.Narration.BranchModel`

| Field | Type | Notes |
|---|---|---|
| `choices` | `ChoiceModel[]` | |

### `Eremite.Model.Narration.ChoiceModel`

| Member | Kind | Notes |
|---|---|---|
| `text` | `LocaText` field | |
| `CanExecute()` | method `→ bool` | |
| `Execute()` | method | Select this choice |
| `GetText()` | method `→ string` | |

### Popup: `Eremite.Narration.UI.HomePopup`

---

## World Map (Overworld)

### `Eremite.WorldMap.WorldField`

| Property | Notes |
|---|---|
| `Biome` | `BiomeModel` |
| `Transform` | Unity Transform (for camera) |

### `Eremite.WorldMap.Model.BiomeModel`

| Member | Kind | Notes |
|---|---|---|
| `displayName` | `LocaText` field | |
| `description` | `LocaText` field | |
| `effects` | `EffectModel[]` field | |
| `wantedGoods` | `GoodRef[]` field | |
| `GetDepositsGoods()` | method `→ string[]` | |
| `GetTreesGoods()` | method `→ string[]` | |

### `Eremite.WorldMap.Model.BiomeModel` (Season data)

| Field | Notes |
|---|---|
| `seasons` | `SeasonModel[]` — each has `seasonRewardsExtendPrice` (Good struct) |

### `Eremite.Services.World.IWorldCalendarService`

| Method | Signature | Notes |
|---|---|---|
| `IsStormAboutToCome()` | `→ bool` | |
| `HasPlayedFinalGame()` | `→ bool` | |

---

## Meta State (Progression)

### `Eremite.Model.State.MetaEconomyState`

| Field | Type | Notes |
|---|---|---|
| `currentCycleExp` | `int` | |
| `metaCurrencies` | dict | `Dictionary<MetaCurrencyType, int>` |

### `Eremite.Model.State.LevelState`

| Field | Type | Notes |
|---|---|---|
| `level` | `int` | Player prestige level |
| `exp` | `int` | Current XP |
| `targetExp` | `int` | XP needed for next level |

### `Eremite.Model.State.CapitalState`

| Field | Type | Notes |
|---|---|---|
| `currentCycleUpgrades` | list | Capital upgrades active this cycle |

### `Eremite.Model.MetaCurrencyModel`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |

---

## Deeds / Goals

### `Eremite.Model.Goals.GoalModel`

| Member | Kind | Notes |
|---|---|---|
| `label` | field | Category key |
| `displayName` | `LocaText` field | |
| `Description` | `string` property | |
| `isActive` | `bool` field | |
| `isCycleGoal` | `bool` field | |
| `rewards` | `MetaRewardModel[]` field | |
| `HasAccessTo()` | method `→ bool` | |
| `GetMetaProgressText(GoalState)` | method `→ string` | Progress string |

### `Eremite.Model.Goals.GoalState`

| Field | Type | Notes |
|---|---|---|
| `model` | `string` | Key → `Settings.GetGoal(name)` |
| `completed` | `bool` | |
| `rewarded` | `bool` | |

### `Eremite.Model.Goals.GoalCategoryModel` (LabelModel subclass)

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |
| `order` | `int` | Sort order |
| `isHidden` | `bool` | |

### `Eremite.Model.Goals.MetaGoalsState`

| Field | Type | Notes |
|---|---|---|
| `goals` | `List<GoalState>` | |

### `Eremite.Services.IGoalsService`

| Method | Signature | Notes |
|---|---|---|
| `RewardGoal(GoalState)` | void | Claim a completed deed's reward |

### Popup: `Eremite.WorldMap.UI.Goals.GoalsPopup`

---

## World Events / Seals

### `Eremite.Services.World.IWorldSealsService`

| Method | Signature | Notes |
|---|---|---|
| `CanAffordSeal(...)` | `→ bool` | |
| `WasAnyCompleted()` | `→ bool` | |
| `GetHighestWon()` | `→ int` | |

### `Eremite.Services.IWorldStateService` / `IMetaServices.WorldStateService`

| Method | Signature | Notes |
|---|---|---|
| `HasModifier(...)` | `→ bool` | |
| `HasEvent(...)` | `→ bool` | |
| `HasSeal(...)` | `→ bool` | |
| `GetNearbySeal(...)` | `→ WorldSealModel` | |

---

## Game Result Popup

### `Eremite.Model.State.GameObjectivesState`

| Field | Type | Notes |
|---|---|---|
| `hasWon` | `bool` | |
| `hasLost` | `bool` | |
| `gracePeriodLeft` | `float` | |

### `ScoreCalculator` (in-game type)

| Method | Signature | Notes |
|---|---|---|
| `GetScore(...)` | `→ ScoreData[]` | Score breakdown data |

**ScoreData** fields: `label` (LocaText), `points` (int), `amount` (int).

### Popup: `Eremite.View.Popups.GameResultPopup`
Private fields: `headerText`, `descText`, `menuButton`, `continueButton`.

---

## Popup Infrastructure

### `Eremite.View.Popups.Popup`
Base class for all popups.

| Method | Signature | Notes |
|---|---|---|
| `Hide()` | void | Close the popup |

### `Eremite.Services.AppServices.PopupsService`
Available via `MainController.Instance.AppServices.PopupsService`.

| Field | Notes |
|---|---|
| `activePopups` (private) | `List<Popup>` — index 0 is topmost |

The mod uses `GameReflection.GetTopActivePopup()` to get the current popup for routing.

---

## UI Components

### Tab system

#### `TabsPanel`
Private fields used:
- `buttons` — `TabsButton[]`
- `current` — `TabsButton`

#### `TabsButton`
Private fields used:
- `button` — Unity `Button`
- `content` — `GameObject` (panel content)

### `Eremite.View.ToggleButton`
Custom game toggle wrapping a Unity Button.

| Method | Signature | Notes |
|---|---|---|
| `IsOn()` | `→ bool` | |

### HUD slots

#### `Eremite.View.HUD.GoodSlot`
Private field `good` → `Good` struct.

#### `Eremite.View.HUD.EffectSlot`
Private field `model` → `EffectModel`.

### Wiki / Encyclopedia

#### `Eremite.View.UI.Wiki.WikiPopup`
Private fields: `categoryButtons`, `current`, `panels`.

#### `Eremite.View.UI.Wiki.WikiCategoryButton`
Private field `button` (Button), property `Panel` (WikiCategoryPanel).

#### `Eremite.View.UI.Wiki.WikiSlot`
Private field `button`, method `IsUnlocked()`.

---

## Effect Model

### `Eremite.Model.EffectModel`
Base for all effects, perks, cornerstones.

| Member | Kind | Notes |
|---|---|---|
| `DisplayName` | `string` property | |
| `Description` | `string` property | |
| `rarity` | `EffectRarity` field | "Common", "Rare", etc. |
| `isEthereal` | `bool` field | Ethereal cornerstones |
| `IsPositive` | `bool` property | |
| `displayName` | `LocaText` field | For `GetLocaString` pattern |
| `Remove(...)` | method | Remove effect from game state |
| `Apply(context, source, sourceId)` | method | Apply the effect |
| `CanBeDrawn()` | method `→ bool` | |
| `GetAmountText()` | method `→ string` | Quantity suffix |

### `Eremite.Model.BuildingPerkModel`

| Member | Kind | Notes |
|---|---|---|
| `DisplayName` | `string` property | |
| `description` | `LocaText` field | |
| `GetDescription(building)` | method `→ string` | Context-aware description |

### `Eremite.Model.VillagerPerkModel`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `LocaText` | |

### `Eremite.Services.IGameModelService`

| Method | Signature | Notes |
|---|---|---|
| `GetEffect(string name)` | `→ EffectModel` | Look up effect by internal key |

---

## Game Settings (Static Data)

### `Eremite.Model.Settings`
Accessed via `MB.Settings` (protected static — `NonPublic | Static`).

This is the master ScriptableObject containing all static game data.

Key methods:

| Method | Return type | Notes |
|---|---|---|
| `GetGood(string name)` | `GoodModel` | |
| `GetBuilding(string name)` | `BuildingModel` | |
| `GetWorkshopRecipe(string name)` | `WorkshopRecipeModel` | |
| `GetRecipe(string name)` | `RecipeModel` | Base recipe |
| `GetOrder(string name)` | `OrderModel` | |
| `GetEffect(string name)` | `EffectModel` | |
| `GetGoal(string name)` | `GoalModel` | |
| `GetHearthSacrificeRecipe(string name)` | `HearthSacrificeRecipeModel` | |
| `GetCapitalUpgrade(string name)` | `CapitalUpgradeModel` | |
| `GetMetaCurrency(string name)` | `MetaCurrencyModel` | |
| `GetCornerstonesViewConfiguration(string name)` | `CornerstonesViewConfiguration` | |

Key array fields:

| Field | Type | Notes |
|---|---|---|
| `workshops` | `BuildingModel[]` | All workshop building models |
| `blightPosts` | `BuildingModel[]` | All blight post models |
| `goods` | `GoodModel[]` | All good types |
| `needs` | `NeedModel[]` | |
| `hubsTiers` | array | Hub tier progression data |
| `blightConfig` | `BlightConfig` | Has `blightPostFuel` (GoodRef) |

---

## Reflection Access Patterns

### Controller Hierarchy (In-Game)
```
GameController.Instance          (static, Eremite.Controller.GameController)
  └── GameServices               (IGameServices → GameServices)
        ├── MapService           (IMapService)
        ├── BuildingsService     (IBuildingsService)
        ├── StorageService       (IStorageService)
        └── ... (all listed above)
```

### Controller Hierarchy (Meta/World)
```
MetaController.Instance          (static, Eremite.Controller.MetaController)
  └── MetaServices               (IMetaServices)
        ├── MetaStateService     (IMetaStateService)
        └── GoalsService         (IGoalsService)

WorldController.Instance         (static, Eremite.Controller.WorldController)
  └── WorldServices              (IWorldServices)
        ├── WorldMapService      (IWorldMapService)
        ├── WorldCalendarService (IWorldCalendarService)
        ├── NarrationService     (INarrationService)
        └── WorldSealsService    (IWorldSealsService)
```

### Static Data
```
MB.Settings (protected static)   → Eremite.Model.Settings
```

### PopupsService
```
MainController.Instance.AppServices.PopupsService
  └── activePopups (private List<Popup>) — index 0 is topmost
```

---

## Notes on Reflection Binding Conventions

1. **Public instance** = `BindingFlags.Public | BindingFlags.Instance`
   Used for most service interface methods and properties.

2. **NonPublic instance** = `BindingFlags.NonPublic | BindingFlags.Instance`
   Used for private UI fields (e.g., `BuildingPanel.currentBuilding`, popup fields).

3. **Public static** = `BindingFlags.Public | BindingFlags.Static`
   Used for singleton `Instance` properties and `IsGameActive`.

4. **NonPublic static** = `BindingFlags.NonPublic | BindingFlags.Static`
   Used for `MB.Settings`.

5. **LocaText pattern**: any `LocaText` field is read via
   `GameReflection.GetLocaText(object)` which reads the `Text` property.

6. **GoodRef pattern**: `GameReflection.GoodRefGoodField` / `GoodRefAmountField`
   are cached once and reused everywhere GoodRef appears.

7. **Never cache instances** — only cache `Type`, `PropertyInfo`, `FieldInfo`, `MethodInfo`.
   Service instances are retrieved fresh through the cached PropertyInfo each time.
