# Game Internals Reference

Reference documentation for "Against the Storm" game internals discovered through reflection. Update this file as new patterns are discovered.

## How to Use This Document

This is a **reference**, not a tutorial. Use it when you need:
- Type names for reflection caching
- Field/property/method signatures
- Service access patterns

**For implementation patterns**, see `CLAUDE.md` — it documents how to structure reflection code, key handlers, overlays, and other mod patterns.

**For working examples**, see the `*Reflection.cs` files — each system documented here has a corresponding reflection file (e.g., `OrdersReflection.cs`, `TradeReflection.cs`).

---

## Table of Contents

- [Controller Hierarchy](#controller-hierarchy)
- [Service Containers](#service-containers)
- [Settings Access](#settings-access)
- [Map System](#map-system)
- [Events and Observables](#events-and-observables)
- [UI Hierarchy](#ui-hierarchy)
- [Key Class Names](#key-class-names)
- [Input System](#input-system)
- [UI Element Visibility](#ui-element-visibility)
- [Reflection Notes](#reflection-notes)
- [Building Panel System](#building-panel-system)
- [Building Upgrade System](#building-upgrade-system)
- [World Map System](#world-map-system)
- [Embark System](#embark-system)
- [Capital/Citadel System](#capitalcitadel-system)
- [Orders System](#orders-system)
- [Recipes/Workshop System](#recipesworkshop-system)
- [Cornerstones System](#cornerstones-system)
- [Reputation Rewards System](#reputation-rewards-system)
- [Newcomers System](#newcomers-system)
- [Wildcard System](#wildcard-system)
- [Wiki/Encyclopedia System](#wikiencyclopedia-system)
- [Trade System](#trade-system)
- [Trade Routes System](#trade-routes-system)
- [Black Market System](#black-market-system)
- [Altar System](#altar-system-forsaken-altar)
- [Seal System](#seal-system)
- [Game Result System](#game-result-system)
- [PerkCrafter System](#perkcrafter-system-cornerstone-forge)
- [Deeds/Goals System](#deedsgoals-system)
- [Consumption Control System](#consumption-control-system)
- [Trends System](#trends-system)
- [Daily Expedition System](#daily-expedition-system)
- [Custom Games System](#custom-games-system-training-expeditions)
- [Payments System](#payments-system)
- [World Event System](#world-event-system)
- [Games History System](#games-history-system)
- [Stats System](#stats-system)
- [Ironman System](#ironman-system-queens-hand-trial)

---

## Controller Hierarchy

```
Eremite.Controller.GameController
  - Static: Instance (singleton)
  - Static: IsGameActive (bool)
  - Instance: GameServices

Eremite.Controller.MainController
  - Static: Instance (singleton)
  - Instance: AppServices

Eremite.Controller.MetaController
  - Static: Instance (singleton)
  - Instance: MetaServices
```

**Access pattern:**
```
GameController.Instance → GameServices → MapService/GladesService/etc.
MainController.Instance → AppServices → PopupsService
MetaController.Instance → MetaServices → TutorialService
```

---

## Service Containers

### GameServices

| Service | Purpose |
|---------|---------|
| MapService | Field/tile access, object lookup, map dimensions |
| GladesService | Fog of war, glade danger levels |
| ResourcesService | NaturalResources dictionary (trees, etc.) |
| DepositsService | Deposits dictionary (clay, copper, etc.) |
| BuildingsService | Buildings dictionary, GetMainHearth() |
| VillagersService | Villagers dictionary |
| ModeService | Game mode state (Idle property) |
| InputService | Input handling, lock mechanism |
| ReputationRewardsService | Reputation rewards popup, RequestPopup() |
| OrdersService | Orders list, completion, tracking, picks |
| GameTimeService | Current game time (Time property) |
| GameBlackboardService | Observables: OnBuildingPanelShown, OrderPickPopupRequested |
| WorkshopsService | Global production limits (GetGlobalLimitFor, SetGlobalLimitFor) |
| StorageService | Warehouse amounts (GetAmount) |
| RecipesService | Recipe lookup (GetRecipesFor) |
| GameContentService | Unlock checking (IsUnlocked) |
| ConstructionService | Building display order (GetShowIndex) |
| BiomeService | Current biome, blueprints config |
| EffectsService | Wildcard picks remaining (GetWildcardPicksLeft) |
| CornerstonesService | Cornerstone picks, reroll, extend, decline |
| NewcomersService | Newcomer group arrival and selection |
| TimeScaleService | Game speed control |
| CalendarService | Season tracking, time of year |
| StateService | Game state, active effects |
| ReputationService | Reputation points and thresholds |
| HostilityService | Hostility level and events |
| ResolveService | Villager resolve/morale |
| RacesService | Race data and species info |
| BlightService | Blight state and management |
| RelicsService | Relic investigation state |
| TradeService | Trade routes and deals |
| PerksService | Active perks |
| OreService | Ore deposits dictionary |
| SpringsService | Springs dictionary |
| LakesService | Lakes dictionary |
| ConditionsService | Game condition checks |
| ActorsService | Worker task descriptions (GetActor) |
| GoodsService | Goods management |
| HearthService | Hearth fuel and fire management |
| GameModelService | Game model access |
| RainpunkService | Rainpunk/engine state |
| NewsService | Game event notifications |
| MonitorsService | Condition monitoring and alerts |

### AppServices

| Service | Purpose |
|---------|---------|
| PopupsService | Popup management, show/hide events |

### MetaServices

| Service | Purpose |
|---------|---------|
| TutorialService | Tutorial state and progression |
| MetaConditionsService | Unlock checks for buildings (IsUnlocked) |
| MetaPerksService | Upgrade unlock gating, preparation points |

---

## Settings Access

Static model data is accessed via `Eremite.MB.Settings` (protected static property).

**Reflection access:**
```csharp
var mbType = assembly.GetType("Eremite.MB");
var settingsProperty = mbType.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Static);
var settings = settingsProperty.GetValue(null);  // Eremite.Model.Settings
```

**Common lookup methods on Settings:**
```csharp
GetOrder(string name)                        // OrderModel
GetEffect(string name)                       // EffectModel
GetBuilding(string name)                     // BuildingModel
GetGood(string name)                         // GoodModel
GetWorkshopRecipe(string name)               // WorkshopRecipeModel
GetCornerstonesViewConfiguration(string name) // CornerstonesViewConfiguration
```

**Settings fields:**
```csharp
workshops           // BuildingModel[] - all workshop building models
blightPosts         // BuildingModel[] - all blight post models
goods               // GoodModel[] - all good models
```

---

## Map System

### Coordinates
- Dynamic grid size (varies by mission type, e.g., 70x70, 125x125)
- `Vector2Int` for positions
- Use `MapService.InBounds(x, y)` for bounds checking

### MapService Methods
```csharp
// Get map dimensions
var fields = mapService.Fields;  // Map<Field> object
int width = fields.width;        // public field
int height = fields.height;      // public field

// Check bounds
bool valid = mapService.InBounds(x, y);

// Get field at coordinate
var field = mapService.GetField(x, y);

// Field properties
string terrain = field.Type.ToString();  // "Water", "Forest", etc.
bool passable = field.IsTraversable;

// Get object on tile (building/resource)
var objectOnTile = mapService.GetObjectOn(x, y);
```

### GladesService
```csharp
// Check if coordinate is in a glade
var glade = gladesService.GetGlade(new Vector2Int(x, y));

// GladeState fields (use FieldInfo, not PropertyInfo)
glade.fields        // List<Vector2Int> - all tiles in glade
glade.dangerLevel   // enum: None, Dangerous, Forbidden
glade.wasDiscovered // bool - true if revealed
```

### Service Dictionaries
```csharp
// All use Dictionary<Vector2Int, T> or Dictionary<int, T>
ResourcesService.NaturalResources  // Dict<Vector2Int, NaturalResource>
DepositsService.Deposits           // Dict<Vector2Int, Deposit>
BuildingsService.Buildings         // Dict<int, Building>
BuildingsService.Hearths           // Dict<int, Hearth>
```

### BuildingsService Methods
```csharp
// Get main hearth (Ancient Hearth) - WARNING: throws if no hearths registered yet
var mainHearth = buildingsService.GetMainHearth();

// Safer: access Hearths dictionary directly
var hearthsDict = buildingsService.Hearths;  // Dict<int, Hearth>
if (hearthsDict.Count > 0)
{
    var firstHearth = hearthsDict.Values.First();  // Main hearth is first
    var position = firstHearth.Field;              // Vector2Int map position
}
```

### Object Properties
```csharp
// Get display name from any game object (resource, deposit, building)
var model = obj.Model;              // PropertyInfo
var displayName = model.displayName; // FieldInfo - LocaText object
var name = model.name;              // PropertyInfo - internal name (fallback)

// Building position
var position = building.Field;      // PropertyInfo → Vector2Int
```

### LocaText (Localized Strings)
Many game objects store display names as `LocaText` objects (type: `Eremite.Model.LocaText`).

```csharp
// Manual extraction (don't do this - use helper)
var locaText = displayNameField.GetValue(model);
var text = locaText.GetType().GetProperty("Text").GetValue(locaText) as string;

// Use the helper instead:
string text = GameReflection.GetLocaText(locaText);  // Handles null, caches PropertyInfo
```

### Building State
```csharp
// Building has a BuildingState property with construction info
var state = building.BuildingState;  // PropertyInfo

// BuildingState fields (Eremite.Model.State.BuildingState)
state.finished           // bool - true when construction complete
state.buildingProgress   // float - construction progress (0-1)
state.builders           // int - number of villagers constructing
state.placed             // bool - true if placed on map
state.rotation           // int - rotation value (0-3)
```

### Relic/Ruin Detection
```csharp
// Ruins are buildings whose model is a RelicModel
// RelicModel inherits from UpgradableBuildingModel

var buildingModel = building.BuildingModel;  // PropertyInfo
// Check if model type is Eremite.Buildings.RelicModel
bool isRelic = typeof(RelicModel).IsInstanceOfType(buildingModel);

// RelicModel includes:
// - Destroyed buildings turned into ruins (via BuildingModel.ruin field)
// - Glade events/mysteries that need investigation
```

---

## Events and Observables

### PopupsService
```csharp
AnyPopupShown   // IReadOnlyReactiveProperty<object>
AnyPopupHidden  // IReadOnlyReactiveProperty<object>
```

### Opening Popups Programmatically

**Reputation Rewards Popup** (pick reward from reputation milestone):
```csharp
// Access via GameServices
var reputationRewardsService = gameServices.ReputationRewardsService;
reputationRewardsService.RequestPopup();  // Opens the reward selection popup
```

**Reflection pattern:**
```csharp
var gameServices = GameReflection.GetGameServices();
var rewardsServiceProp = gameServices.GetType().GetProperty("ReputationRewardsService");
var rewardsService = rewardsServiceProp.GetValue(gameServices);
var requestPopupMethod = rewardsService.GetType().GetMethod("RequestPopup");
requestPopupMethod.Invoke(rewardsService, null);
```

### ModeService
```csharp
Idle  // IReadOnlyReactiveProperty<bool> - true = normal mode
```

### Unity Scene Events
```csharp
SceneManager.sceneLoaded
SceneManager.sceneUnloaded
```

---

## UI Hierarchy

### Popup Structure
```
PopupsService (manages all popups)
  → Popup (base class)
     → AnimateShow() / Hide()
     → Contains child panels/elements
```

### Tab System
```
TabsPanel (tab container)
  → current (TabsButton - active tab)
  → TabsButton.content (GameObject - tab content)
```

### Element Types (Unity UI)
All inherit from `Selectable`:
- Button, Toggle, Slider, Dropdown (TMP_Dropdown), InputField, Scrollbar

### Text Components
- `TMPro.TMP_Text` (TextMeshPro)
- `UnityEngine.UI.Text` (legacy)

---

## Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Game controller | `Eremite.Controller.GameController` |
| Main controller | `Eremite.Controller.MainController` |
| Meta controller | `Eremite.Controller.MetaController` |
| Popup service | `Eremite.Services.PopupsService` |
| Popup base | `Eremite.View.Popups.Popup` |
| Tutorial tooltip | `Eremite.Tutorial.Views.TutorialTooltip` |
| Decision popup | `Eremite.View.DecisionPopup` |
| Tab panel | `Eremite.View.UI.TabsPanel` |
| Tab button | `Eremite.View.UI.TabsButton` |
| Map service | `Eremite.Services.MapService` |
| Glades service | `Eremite.Services.GladesService` |
| Resources service | `Eremite.Services.IResourcesService` |
| Deposits service | `Eremite.Services.IDepositsService` |
| Buildings service | `Eremite.Services.IBuildingsService` |
| Villagers service | `Eremite.Services.IVillagersService` |
| Mode service | `Eremite.Services.ModeService` |
| Input service | `Eremite.Services.InputService` |
| Input config | `Eremite.InputConfig` |
| Demo element | `DemoElement` (check by name) |
| Camera controller | `Eremite.View.CameraController` |
| Building state | `Eremite.Model.State.BuildingState` |
| Relic model | `Eremite.Buildings.RelicModel` |
| Building model | `Eremite.Buildings.BuildingModel` |
| Building base | `Eremite.Buildings.Building` |

---

## Input System

The game uses **two independent input systems** in parallel:

### Legacy Pipeline (UI Navigation)
```
StandaloneInputModule (from UserReportingScript.cs)
  → Input.GetAxis("Horizontal"/"Vertical")
  → ExecuteEvents.Execute(OnMove)
  → Selectable.OnMove() → finds next button
  → EventSystem.SetSelectedGameObject()
```
- Arrow keys cycle UI buttons via this pipeline
- **Not affected by InputService locks**
- To block: disable `StandaloneInputModule.enabled`

### New InputSystem Pipeline (Game Actions)
```
InputConfig actions (camera, buildings, shortcuts)
  → InputService.WasTriggered() / IsTriggering()
  → Game systems (CameraController, BuildingMode, etc.)
```
- Respects `InputService.IsLocked()`
- Can be blocked via `InputService.LockInput(principal)`

### InputService Lock Mechanism
```csharp
InputService.LockInput(object principal)   // Lock input
InputService.ReleaseInput(object principal) // Release
InputService.IsLocked()                     // Check state

// These methods respect the lock:
WasTriggered(InputAction action, bool ignoreLock = false)
IsTriggering(InputAction action, bool ignoreLock = false)
GetAxisValue(InputAction action, bool ignoreLock = false)
```

### Key Input Classes

| Class | Purpose |
|-------|---------|
| `Eremite.InputConfig` | All InputAction definitions |
| `Eremite.Services.InputService` | Central input service with lock mechanism |
| `StandaloneInputModule` | Legacy UI navigation (Unity) |

---

## UI Element Visibility

The game uses multiple mechanisms to hide UI elements:

### 1. GameObject.SetActive(false)
Most common. Check via `activeSelf` or `activeInHierarchy`.

### 2. CanvasGroup Alpha
```csharp
canvasGroup.alpha = 0  // Invisible but keeps layout
```

### 3. DemoElement Component
Marks demo-only UI elements:
```csharp
// Component: Eremite.View.Utils.DemoElement (or similar)
// Field: inFullGame (bool, private)
//   true  = visible in both demo and full game
//   false = hidden in full game (demo-only)
```
Detection via reflection:
```csharp
if (comp.GetType().Name == "DemoElement")
{
    var field = comp.GetType().GetField("inFullGame",
        BindingFlags.NonPublic | BindingFlags.Instance);
    bool inFullGame = (bool)field.GetValue(comp);
    // if !inFullGame, element is demo-only
}
```

### 4. Scale to Zero (potential)
```csharp
transform.localScale = Vector3.zero  // Could be used
```

---

## Reflection Notes

### Safe to Cache (type metadata)
```csharp
Type, PropertyInfo, MethodInfo, FieldInfo
```

### Never Cache (instance data)
```csharp
// Services are destroyed on scene transitions
var gameServices = GetGameServices();  // Get fresh each time

// Map/game state changes constantly
var tileContents = mapService.GetObjectOn(x, y);  // Always live lookup
```

### Multi-Type Methods
When a method handles multiple object types (NaturalResource, Deposit, Building), do per-call reflection:
```csharp
// These are different types with different PropertyInfo
var modelProp = obj.GetType().GetProperty("Model");  // Per-call, not cached
```

---

## Building Panel System

### Detecting Panel Open/Close

**Static Field:** `BuildingPanel.currentBuilding` holds the currently shown building (or null)

**Events (via GameMB.GameBlackboardService):**
- `OnBuildingPanelShown` - Fires when panel opens, passes Building
- `OnBuildingPanelClosed` - Fires when panel closes, passes Building

### Building Class Hierarchy

```
Building (base)
├── ProductionBuilding (has workers, recipes)
│   ├── Workshop, Farm, Mine, GathererHut, Camp
│   ├── Collector, BlightPost, FishingHut
│   └── RainCatcher, Extractor
├── Hearth
├── House
├── Storage
├── Institution
├── Decoration
├── Hydrant
├── Relic
├── Shrine
├── Port
└── Poro
```

### Common Building Data

```csharp
// Identity
building.BuildingModel.displayName.Text  // Localized name
building.BuildingModel.Name              // Internal name
building.Id                              // Unique instance ID

// State
building.BuildingState.finished          // Construction complete
building.BuildingState.isSleeping        // Is paused
building.CanSleep()                      // Can be paused (virtual)
building.Sleep() / building.WakeUp()     // Pause/resume

// Position
building.Field                           // Vector2Int map position
```

### Production Building Data

All production buildings have workers and recipes:

```csharp
// Workers
building.state.workers[]                 // int[] - villager IDs per slot (0 = empty)
building.Workplaces                      // WorkplaceModel[] - slot definitions

// Recipes (varies by building type)
building.state.recipes                   // List<RecipeState> or specialized type
building.SwitchProductionOf(recipe)      // Toggle recipe on/off

// Storage (if applicable)
building.ProductionStorage.goods         // Output goods
building.IngredientsStorage.goods        // Input goods (Workshop, BlightPost)
```

### Recipe Data Access

```csharp
// Recipe state (common fields)
recipeState.model                        // Recipe name
recipeState.active                       // Is enabled
recipeState.prio                         // Priority (some buildings)

// Recipe model lookup
MB.Settings.GetWorkshopRecipe(name)
MB.Settings.GetFarmRecipe(name)
MB.Settings.GetMineRecipe(name)
// etc.

// Recipe model fields
recipe.producedGood                      // GoodRef - output
recipe.requiredGoods                     // GoodsSet[] - ingredient slots
recipe.productionTime                    // Base production time
recipe.grade                             // RecipeGradeModel
```

### Building-Specific Data

**Camp:** `camp.state.mode` (CampMode enum) - tree-cutting behavior

**FishingHut:** `hut.state.baitMode` (FishmanBaitMode), `baitChargesLeft`

**Hearth:** Fire panel, fuel selection, hub effects, sacrifice recipes, blight (main only)

**Relic:** Investigation state machine - not started / in progress / complete

**Port:** Expedition state machine - idle / in progress / rewards waiting

**Poro:** Needs system with satisfaction levels

**Shrine:** Tiered effects that unlock progressively

### Worker Assignment

```csharp
// Get villager details
var villager = GameMB.VillagersService.GetVillager(workerId);
villager.Model.displayName.Text          // Villager name
villager.Model.race                      // RaceModel

// Get free workers by race
var races = GameMB.RacesService.Races.Values;
foreach (var race in races) {
    int free = GameMB.WorkersService.GetFreeWorkersAmount(race.Name);
}
```

### Storage/Goods Access

```csharp
// Building storage
storage.goods                            // Dictionary access via reflection
storage.GetFullAmount(goodName)          // Amount including reserved
storage.GetDeliveryState(goodName)       // Delivery toggle state

// Global storage
GameMB.StorageService.Main.GetAmount(goodName)
GameMB.StorageService.Main.Goods.goods   // All goods

// Good display name
good.displayName.Text                    // Localized name
goodRef.DisplayName                      // Shortcut
```

---

## Building Upgrade System

### Overview

Many buildings support upgrades that add perks/bonuses. The upgrade system uses `UpgradableBuilding` as a base class.

### Class Hierarchy

```
Building (base)
└── UpgradableBuilding
    ├── ProductionBuilding (Workshop, Farm, Mine, Camp, etc.)
    ├── House
    ├── Storage
    ├── Port
    ├── FishingHut
    ├── Relic
    ├── RainCatcher
    ├── Extractor
    ├── Institution
    └── Decoration
```

**NOT upgradable (extend Building directly):**
- Hearth (uses separate hub tier system)
- Shrine (uses tiered effects system)
- Poro (uses needs/happiness system)
- Hydrant (blight management only)

### Data Model

```csharp
// UpgradableBuilding
building.UpgradableModel               // UpgradableBuildingModel
building.UpgradableState               // UpgradableBuildingState
building.HasUpgrades                   // bool - true if upgrades available AND unlocked

// UpgradableBuildingModel
model.levels                           // BuildingLevelModel[] - upgrade tiers

// UpgradableBuildingState
state.level                            // int - current level (0 = base, 1 = Level I, etc.)
state.upgrades                         // bool[][] - jagged array: upgrades[level][perkIndex]

// BuildingLevelModel
levelModel.requiredGoods               // GoodsSet[] - cost (each GoodsSet is OR options)
levelModel.options                     // BuildingPerkModel[] - perk choices (pick exactly 1)

// BuildingPerkModel
perk.DisplayName                       // string - localized name
perk.GetDescription(building)          // string - localized description with context
```

### Upgrade Unlock Gating

Different building types have different unlock requirements:

| Gating Type | Check | Buildings |
|-------------|-------|-----------|
| Event-based | `StateService.Effects.campsUpgradesActive` | Camps |
| Per-building meta | `MetaPerksService.AreHouseUpgradesUnlocked(model)` | Houses |
| Global meta | `MetaPerksService.AreMineUpgradesUnlocked()` | Mines |
| Global meta | `MetaPerksService.AreBlightPostUpgradesUnlocked()` | BlightPosts |
| Always true | Default `AreUpgradesUnlocked()` | Everything else |

The `HasUpgrades` property on `UpgradableBuilding` handles all these checks internally.

### The goodPicker Problem

**Problem:** The game's `Upgrade()` method requires a `Func<int, Good>` delegate parameter. Creating this delegate via reflection fails due to type mismatches - you can't directly cast a reflected delegate to the game's internal `Func<int, Good>` type.

**Solution:** Use `System.Linq.Expressions.Expression.Lambda()` to create a strongly-typed delegate at runtime:

```csharp
// Build expression: (int index) => new Good(goodNames[index], amounts[index])
var indexParam = Expression.Parameter(typeof(int), "index");
var goodNamesConst = Expression.Constant(goodNames);  // string[]
var amountsConst = Expression.Constant(amounts);       // int[]

var nameAccess = Expression.ArrayIndex(goodNamesConst, indexParam);
var amountAccess = Expression.ArrayIndex(amountsConst, indexParam);

var goodConstructor = goodType.GetConstructor(new[] { typeof(string), typeof(int) });
var newGood = Expression.New(goodConstructor, nameAccess, amountAccess);

var funcType = typeof(Func<,>).MakeGenericType(typeof(int), goodType);
var lambda = Expression.Lambda(funcType, newGood, indexParam);
var compiledDelegate = lambda.Compile();  // This can be passed to Upgrade()
```

**Why this approach:**
- Let the game handle all upgrade logic (removes goods, applies perks, fires events)
- Single reflection call instead of reimplementing 6+ steps manually
- Game manages its own UI and state updates
- No risk of missing steps or state desync

### Affordability Check

Instead of calling the game's `CanUpgrade()` (which also needs the delegate), check affordability directly:

```csharp
// Get required goods from BuildingLevelModel.requiredGoods
// Check warehouse via GetMainStorageAmount(goodName)
// Compare amounts to determine if player can afford
```

### Timing Issue with State Updates

**Problem:** After calling `Upgrade()`, the game's `UpgradableBuildingState.level` may not update synchronously. If you immediately re-read the state, you may see the old value.

**Solution:** Track purchases locally in addition to reading game state:

```csharp
private HashSet<int> _purchasedThisSession = new HashSet<int>();

// When checking if level is achieved:
bool isAchieved = level.isAchieved || _purchasedThisSession.Contains(levelIndex);

// After successful purchase:
_purchasedThisSession.Add(levelIndex);
```

### Good Type

The `Good` struct represents a quantity of a specific good:

```csharp
// Eremite.Model.Good
var good = Activator.CreateInstance(goodType, new object[] { goodName, amount });
// Constructor: Good(string name, int amount)
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Upgradable building base | `Eremite.Buildings.UpgradableBuilding` |
| Upgradable model | `Eremite.Buildings.UpgradableBuildingModel` |
| Upgradable state | `Eremite.Buildings.UpgradableBuildingState` |
| Level model | `Eremite.Buildings.BuildingLevelModel` |
| Perk model | `Eremite.Model.BuildingPerkModel` |
| Goods set | `Eremite.Model.GoodsSet` |
| Good struct | `Eremite.Model.Good` |
| Good reference | `Eremite.Model.GoodRef` |

---

## World Map System

### Controller Hierarchy

```
Eremite.Controller.WorldController
  - Static: Instance (singleton)
  - Instance: WorldServices
  - Instance: CameraController (WorldCameraController)
```

**Access pattern:**
```
WorldController.Instance → WorldServices → WorldMapService/WorldStateService/etc.
```

### World Map Services

| Service | Purpose |
|---------|---------|
| WorldMapService | Field access, bounds checking, biome info |
| WorldStateService | Modifiers, events, seals, city names |
| WorldBlackboardService | Observables: OnFieldClicked, OnFieldPreviewShown |
| WorldEmbarkService | Bonus preparation points |
| WorldSealsService | Seal completion tracking |

### WorldMapService Methods

```csharp
// Coordinates use Vector3Int (cubic hex)
GetField(Vector3Int pos)              // Get WorldField
InBounds(Vector3Int pos)              // Bounds check
IsRevealed(Vector3Int pos, int dist)  // Fog of war check
CanBePicked(Vector3Int pos)           // Can embark here
IsCapital(Vector3Int pos)             // Is (0,0,0)
IsCity(Vector3Int pos)                // Has settlement
GetDistanceToStartTown(Vector3Int)    // Distance from capital
GetMinDifficultyFor(Vector3Int)       // Min difficulty for field
```

### WorldStateService Methods

```csharp
HasModifier(Vector3Int pos)           // Has world modifier
HasEvent(Vector3Int pos)              // Has world event
HasSeal(Vector3Int pos)               // Has seal nearby
GetModifierModel(Vector3Int pos)      // Get modifier details
GetEventModel(Vector3Int pos)         // Get event details
GetSealModel(Vector3Int pos)          // Get seal details
GetDisplayNameFor(Vector3Int pos)     // City name
GetModifiersInfluencing(Vector3Int)   // List of modifier names affecting field
Fields                                // Dictionary<Vector3Int, WorldFieldState>
```

### WorldField Properties

```csharp
field.Biome                           // BiomeModel
field.transform                       // Unity Transform for world position
```

### BiomeModel Fields

```csharp
biome.displayName                     // LocaText
biome.description                     // LocaText
biome.effects                         // EffectModel[] - biome effects
biome.wantedGoods                     // GoodModel[] - for trade routes
biome.GetDepositsGoods()              // Available deposit goods
biome.GetTreesGoods()                 // Available tree/natural goods
biome.seasons                         // SeasonsConfig - seasonal effects
```

### Cubic Hex Coordinates

World map uses cubic coordinates (Vector3Int where x + y + z = 0).

```csharp
// Convert cubic to world position
const float HexSize = 0.62f;
int q = cubic.x;  // CubicToAxial
int r = cubic.z;
float x = HexSize * (1.5f * q);
float y = HexSize * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);
```

---

## Embark System

### State Access

```
MetaController.Instance.MetaServices.MetaStateService.EmbarkBonuses
  → EmbarkBonusesState
```

### EmbarkBonusesState Fields

```csharp
caravans            // List<EmbarkCaravanState> - 3 caravan options
effectsOptions      // List<ConditionPickState> - available effect bonuses
rewardsPicked       // List<ConditionPickState> - selected effect bonuses
goodsOptions        // List<GoodPickState> - available goods bonuses
goodsPicked         // List<GoodPickState> - selected goods bonuses
```

### EmbarkCaravanState Fields

```csharp
revealedRaces       // int - number of races revealed
races               // List<string> - race internal names
villagers           // List<string> - one entry per villager (race name)
embarkGoods         // List<Good> - base starting goods
bonusEmbarkGoods    // List<Good> - bonus goods
embarkEffects       // List<string> - base starting effects
bonusEmbarkEffects  // List<string> - bonus effects
```

### ConditionPickState (Effect Bonuses)

```csharp
name                // string - effect internal name
cost                // int - preparation points cost
```

### GoodPickState (Goods Bonuses)

```csharp
name                // string - good internal name
amount              // int - quantity
cost                // int - preparation points cost
```

### Caravan Selection

```csharp
// Via WorldBlackboardService
PickedCaravan       // ReactiveProperty<EmbarkCaravanState>
```

### Difficulty System

```csharp
// DifficultyModel fields
index                       // int - difficulty level (0-20)
canBePicked                 // bool - available for selection
positiveEffects             // int - seasonal mysteries (positive)
negativeEffects             // int - seasonal mysteries (negative)
rewardsMultiplier           // float - meta currency multiplier
preparationPointsPenalty    // int - negative modifier to base points
minEffectCost               // int - min seasonal effect severity
maxEffectCost               // int - max seasonal effect severity
sealFramentsForWin          // int - fragments needed to win
modifiers                   // AscensionModifierModel[] - ascension modifiers

// Methods
GetDisplayName()            // Localized name (e.g., "Prestige 5")
```

### Preparation Points

```csharp
// MetaPerksService
GetBasePreparationPoints()  // Base points from upgrades

// WorldEmbarkService
GetBonusPreparationPoints() // Bonus from cycle effects

// Calculation (game uses min difficulty penalty, not selected)
Total = Max(0, Base + MinDifficultyPenalty) + Bonus
```

### EmbarkDifficultyPicker

Found via `FindObjectOfType<EmbarkDifficultyPicker>()`.

```csharp
SetDifficulty(DifficultyModel)   // Set selected difficulty
GetPickedDifficulty()            // Get current selection
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| World controller | `Eremite.Controller.WorldController` |
| World services | `Eremite.Services.World.IWorldServices` |
| World map service | `Eremite.Services.World.IWorldMapService` |
| World state service | `Eremite.Services.IWorldStateService` |
| World blackboard | `Eremite.Services.World.IWorldBlackboardService` |
| World embark service | `Eremite.Services.World.IWorldEmbarkService` |
| World field | `Eremite.WorldMap.WorldField` |
| Biome model | `Eremite.Model.BiomeModel` |
| Embark bonuses state | `Eremite.Model.State.EmbarkBonusesState` |
| Embark caravan state | `Eremite.Model.State.EmbarkCaravanState` |
| Condition pick state | `Eremite.Model.State.ConditionPickState` |
| Good pick state | `Eremite.Model.State.GoodPickState` |
| Difficulty model | `Eremite.Model.DifficultyModel` |
| Ascension modifier | `Eremite.Model.AscensionModifierModel` |
| Difficulty picker | `Eremite.WorldMap.UI.EmbarkDifficultyPicker` |
| Buildings pick screen | `Eremite.View.Menu.Pick.BuildingsPickScreen` |

---

## Orders System

### Service Access

```
GameController.Instance → GameServices → OrdersService
GameController.Instance → GameServices → GameTimeService
```

### IOrdersService Methods/Properties

```csharp
Orders                              // IList<OrderState> - all current orders
CanComplete(OrderState)             // bool - all objectives met
CompleteOrder(OrderState, OrderModel, bool force)  // Complete an order
OrderPicked(OrderState, OrderPickState)            // Confirm a pick selection
GetPicksFor(OrderState)             // IList<OrderPickState> - pick options
SwitchOrderTracking(OrderState)     // Toggle tracking on/off
GetCurrentlyPickedOrder()           // OrderState - order pending pick
```

### OrderState Fields (Eremite.Model.Orders.OrderState)

Inherits from `BaseOrderState`.

```csharp
// BaseOrderState fields
started             // bool - order has been activated
objectives          // ObjectiveState[] - progress per objective
startTime           // float - game time when started

// OrderState fields
model               // string - internal name (resolve via Settings.GetOrder)
picked              // bool - player has chosen an option from picks
completed           // bool - order fulfilled
isFailed            // bool - order expired
timeLeft            // float - remaining time (if failable)
tracked             // bool - pinned to HUD
picks               // IList<OrderPickState> - pick options
rewards             // string[] - effect names (resolve via Settings.GetEffect)
shouldBeFailable    // bool - timer active
```

### OrderModel Fields (Eremite.Model.Orders.OrderModel)

```csharp
displayName         // LocaText
canBeFailed         // bool - has failure timer
timeToFail          // float - duration before failure
reputationReward    // float - rep gained on completion
unlockAfter         // OrderModel - prerequisite order (nullable)
logicsSets          // OrderLogicsSet[] - objective definitions

// Methods
GetLogics(OrderState)   // OrderLogic[] - resolved objectives for state
GetLogics(int setIndex) // OrderLogic[] - objectives for a specific set
```

### OrderLogic (Eremite.Model.Orders.OrderLogic)

Base class for objective types. Concrete subclasses determine behavior.

```csharp
// Properties
DisplayName         // string - short name (e.g. "Amber", "Shelter")
Description         // string - may contain full sentence with amount placement

// Methods
GetObjectiveText(ObjectiveState)  // string - formatted progress text
GetAmountText()                   // string - required amount (e.g. "10")
IsCompleted(ObjectiveState)       // bool
```

**Key subclass types** (by `GetType().Name`):
- Types containing `"Building"` - building construction objectives (e.g. "Build 3 Shelter")
- `"GoodLogic"` - goods delivery objectives
- Others - verb+noun patterns (e.g. "Produce Pipes", "Complete events")

**Description property caveat**: For Building and GoodLogic types, `Description` returns unrelated flavor text (building/good descriptions), not objective text. Skip it and use fallback formatting for these types.

### OrderPickState Fields (Eremite.Model.Orders.OrderPickState)

```csharp
model               // string - order model name
setIndex            // int - which logics set to use
failed              // bool - this pick option has expired
rewards             // string[] - effect names for this pick
```

### OrderLogicsSet (Eremite.Model.Orders.OrderLogicsSet)

```csharp
logics              // OrderLogic[] - objectives in this set
```

### Popup Types

```csharp
Eremite.View.HUD.Orders.OrdersPopup      // Main orders list
Eremite.View.HUD.Orders.OrderPickPopup    // Pick selection popup
```

`OrderPickPopup` fields:
```csharp
order               // OrderState (private) - the order being picked
```

### Events (via GameBlackboardService)

```csharp
OrderPickPopupRequested  // Observable - fires when pick popup should open
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Orders service | `Eremite.Services.IOrdersService` |
| Game time service | `Eremite.Services.IGameTimeService` |
| Order state | `Eremite.Model.Orders.OrderState` |
| Base order state | `Eremite.Model.Orders.BaseOrderState` |
| Order model | `Eremite.Model.Orders.OrderModel` |
| Order logic (base) | `Eremite.Model.Orders.OrderLogic` |
| Objective state | `Eremite.Model.Orders.ObjectiveState` |
| Order logics set | `Eremite.Model.Orders.OrderLogicsSet` |
| Order pick state | `Eremite.Model.Orders.OrderPickState` |
| Orders popup | `Eremite.View.HUD.Orders.OrdersPopup` |
| Order pick popup | `Eremite.View.HUD.Orders.OrderPickPopup` |
| Effect model | `Eremite.Model.EffectModel` |

---

## Recipes/Workshop System

### IWorkshop Interface (Eremite.Buildings.IWorkshop)

Implemented by all production buildings (Workshop, Farm, Mine, Camp, etc.).

```csharp
Recipes             // IList<WorkshopRecipeState> - current recipe states
BaseModel           // BuildingModel
Base                // Building instance
SwitchProductionOf(WorkshopRecipeState)  // Toggle recipe on/off
```

### WorkshopRecipeState (Eremite.Buildings.WorkshopRecipeState)

```csharp
model               // string - recipe internal name
active              // bool - is production enabled
```

### WorkshopRecipeModel (Eremite.Buildings.WorkshopRecipeModel)

Extends `RecipeModel`.

```csharp
producedGood        // GoodRef - output good and amount
requiredGoods       // GoodsSet[] - ingredient slots (each GoodsSet is OR options)
productionTime      // float - base production time
```

### RecipeModel (Eremite.Buildings.RecipeModel)

```csharp
grade               // RecipeGradeModel - recipe tier/quality
```

### RecipeGradeModel (Eremite.Buildings.RecipeGradeModel)

```csharp
level               // int - grade level (0 = zero star, 1 = one star, 2 = two star, 3 = three star)
```

### IWorkshopsService

```csharp
GetGlobalLimitFor(string goodName)           // int - global production limit (-1 = unlimited)
SetGlobalLimitFor(string goodName, int limit) // Set global limit
```

### IRecipesService

```csharp
GetRecipesFor(string goodName)  // WorkshopRecipeModel[] - all recipes that produce this good
```

### IBuildingsService Additional Properties

```csharp
Workshops           // Dictionary<int, IWorkshop> - all workshop buildings
BlightPosts         // Dictionary<int, IWorkshop> - all blight posts
```

### Settings Recipe Access

```csharp
Settings.GetWorkshopRecipe(string name)  // WorkshopRecipeModel lookup
Settings.GetGood(string name)            // GoodModel lookup
Settings.workshops                       // BuildingModel[] - all workshop models
Settings.blightPosts                     // BuildingModel[] - all blight post models
Settings.goods                           // GoodModel[] - all goods
```

### GoodModel (Eremite.Model.GoodModel)

```csharp
displayName         // LocaText
Name                // string (property) - internal name
category            // GoodCategoryModel
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Workshop interface | `Eremite.Buildings.IWorkshop` |
| Recipe state | `Eremite.Buildings.WorkshopRecipeState` |
| Recipe model | `Eremite.Buildings.WorkshopRecipeModel` |
| Recipe base model | `Eremite.Buildings.RecipeModel` |
| Recipe grade | `Eremite.Buildings.RecipeGradeModel` |
| Good model | `Eremite.Model.GoodModel` |
| Workshops service | `Eremite.Services.IWorkshopsService` |
| Recipes service | `Eremite.Services.IRecipesService` |
| Storage service | `Eremite.Services.IStorageService` |
| Game content service | `Eremite.Services.IGameContentService` |
| Construction service | `Eremite.Services.IConstructionService` |

---

## Cornerstones System

### ICornerstonesService

```csharp
GetCurrentPick()            // RewardPickState - current pick options
GetRerollsLeft()            // int - remaining rerolls
CanExtend()                 // bool - extension available
CanAffordExtend()           // bool - can pay extend cost
Extend()                    // Execute extend
GetDeclinePayoff()          // Good - reward for declining
RemoveFromActive(EffectModel)  // Remove a cornerstone (limit popup)
```

### RewardPickState (Eremite.Model.RewardPickState)

```csharp
options             // EffectModel[] - available cornerstone choices
viewConfiguration   // string - NPC dialogue config name
```

### EffectModel (Eremite.Model.EffectModel)

```csharp
// Properties
DisplayName         // string - localized name
Description         // string - localized description

// Fields
rarity              // RarityModel - Common, Uncommon, Rare, Epic, Legendary
isEthereal          // bool - temporary cornerstone (removed after season)

// Methods
Remove()            // Remove this effect
GetAmountText()     // string - amount/intensity text
```

### NPC Dialogue (CornerstonesViewConfiguration)

```csharp
Settings.GetCornerstonesViewConfiguration(string name)  // Lookup by name
// Fields:
npcName             // LocaText
npcDialogue         // LocaText
```

### Extend Cost Path

```
BiomeService.CurrentBiome.seasons.seasonRewardsExtendPrice  // GoodRef
```

### Popup Types

```csharp
Eremite.View.HUD.Windfalls.RewardPickPopup       // Cornerstone pick popup
Eremite.View.HUD.CornerstonesLimitPickPopup      // Choose-one-to-remove popup
```

`RewardPickPopup` methods (private):
```csharp
OnRewardPicked(int index)    // Pick cornerstone by index
Reroll()                     // Reroll options
Skip()                       // Decline/skip
```

`CornerstonesLimitPickPopup` methods (private):
```csharp
FinishTask(int index)        // Remove cornerstone by index
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Cornerstones service | `Eremite.Services.ICornerstonesService` |
| Reward pick state | `Eremite.Model.RewardPickState` |
| Effect model | `Eremite.Model.EffectModel` |
| View configuration | `Eremite.Model.ViewsConfigurations.CornerstonesViewConfiguration` |
| Biome service | `Eremite.Services.IBiomeService` |
| Seasons config | `Eremite.Model.Configs.SeasonsConfig` |
| Reward pick popup | `Eremite.View.HUD.Windfalls.RewardPickPopup` |
| Limit pick popup | `Eremite.View.HUD.CornerstonesLimitPickPopup` |

---

## Reputation Rewards System

### IReputationRewardsService

```csharp
RewardsToCollect            // ReactiveProperty<int> - pending blueprint count
RequestPopup()              // Open the reward selection popup
GetCurrentPicks()           // ReputationReward[] - current blueprint options
CanAffordReroll()           // bool
Reroll()                    // Reroll options
GetRerollPrice()            // Good - reroll cost
CanExtend()                 // bool
CanAffordExtend()           // bool
Extend()                    // Add more options
```

### ReputationReward

```csharp
building            // string - building model name
```

Resolve via `Settings.GetBuilding(string name)` → `BuildingModel`.

### BuildingModel Additional Properties

```csharp
ListDescription     // string (virtual property) - description for selection lists
```

### Extend Cost Path

```
BiomeService.Blueprints.extendCost  // GoodRef
```

### Popup Type

```csharp
Eremite.View.HUD.ReputationRewardsPopup
```

Methods (private):
```csharp
OnBuildingPicked(BuildingModel)  // Pick a building
Reroll()                         // Reroll options
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Rewards service | `Eremite.Services.IReputationRewardsService` |
| Reputation reward | `Eremite.Model.ReputationReward` |
| Rewards popup | `Eremite.View.HUD.ReputationRewardsPopup` |

---

## Newcomers System

### INewcomersService

```csharp
AreNewcomersWaitning()      // bool - note: typo in game API ("Waitning")
GetCurrentNewcomers()       // NewcomersGroup[] - available group choices
PickGroup(NewcomersGroup)   // Select a group
```

### NewcomersGroup (Eremite.Model.State.NewcomersGroup)

```csharp
races               // string[] - race internal names in this group
goods               // Good[] - goods this group brings
```

### Popup Type

```csharp
Eremite.View.HUD.NewcomersPopup
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Newcomers service | `Eremite.Services.INewcomersService` |
| Newcomers group | `Eremite.Model.State.NewcomersGroup` |
| Newcomers popup | `Eremite.View.HUD.NewcomersPopup` |

---

## Wildcard System

### Overview

Wildcards let the player choose additional blueprints during a settlement. The available pool comes from the biome's wildcard config.

### BiomeBlueprintsConfig

```
BiomeService.Blueprints.wildcards  // BuildingWeightedChance[] - available wildcard pool
```

### BuildingWeightedChance

```csharp
building            // string - building model name
```

### IEffectsService

```csharp
GetWildcardPicksLeft()  // int - remaining wildcard selections
```

### WildcardPopup (Eremite.View.HUD.WildcardPopup)

Fields (private):
```csharp
slots               // WildcardSlot[] - UI slot components
picks               // List<BuildingModel> - current selections
```

Methods (private):
```csharp
OnSlotClicked(int index)  // Toggle selection of a slot
Confirm()                  // Confirm and apply picks
```

### WildcardSlot

```csharp
GetModel()          // BuildingModel - the building in this slot
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Effects service | `Eremite.Services.IEffectsService` |
| Meta conditions service | `Eremite.Services.IMetaConditionsService` |
| Wildcard popup | `Eremite.View.HUD.WildcardPopup` |
| Wildcard slot | `Eremite.View.HUD.WildcardSlot` |
| Building weighted chance | `Eremite.Model.BuildingWeightedChance` |

---

## Wiki/Encyclopedia System

### WikiPopup (Eremite.View.UI.Wiki.WikiPopup)

Fields (private):
```csharp
categoryButtons     // List<WikiCategoryButton> - category tab buttons
current             // WikiCategoryPanel - currently active panel
panels              // WikiCategoryPanel[] - all category panels
```

### WikiCategoryButton (Eremite.View.UI.Wiki.WikiCategoryButton)

```csharp
button              // Button (private) - Unity UI button
Panel               // WikiCategoryPanel (property) - associated panel
```

### WikiSlot (Eremite.View.UI.Wiki.WikiSlot)

Base class for encyclopedia entries.

```csharp
button              // Button (private) - Unity UI button
IsUnlocked()        // bool - entry has been discovered
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Wiki popup | `Eremite.View.UI.Wiki.WikiPopup` |
| Category button | `Eremite.View.UI.Wiki.WikiCategoryButton` |
| Wiki slot | `Eremite.View.UI.Wiki.WikiSlot` |

---

## Trade System

### ITradeService

```csharp
IsMainTraderInTheVillage()       // bool - trader currently present
GetCurrentMainVisit()            // TraderVisitState - current or incoming visit
GetCurrentMainTrader()           // TraderModel - current trader info
GetNextMainTrader()              // TraderModel - next scheduled trader
GetTimeLeftTo(TraderVisitState)  // float - time until arrival
GetStayingTimeLeft()             // float - time until departure
CanForceArrival()                // bool
GetForceArrivalPrice()           // GoodRef - cost to summon trader
ForceArrival()                   // Summon trader early
IsTradingBlocked()               // bool - storm or other block
GetValueInCurrency(good, amount) // float - sell value in amber
GetBuyValueInCurrency(good, amount) // float - buy value in amber
CompleteTrade(good, amount)      // Execute sell
CompleteTradeEffect(effectState) // Purchase perk
AssaultTrader()                  // Assault action
```

### TraderVisitState

```csharp
goods               // TraderGood[] - goods for trade
offeredEffects      // TraderEffectState[] - perks for sale
travelProgress      // float - arrival progress (0-1)
forced              // bool - trader was summoned early
```

### TraderModel

```csharp
displayName         // LocaText
description         // LocaText
dialogue            // LocaText - trader greeting
wantedGoods         // GoodModel[] - goods trader buys at premium
icon                // Sprite
```

### TraderEffectState (Perk)

```csharp
effect              // string - effect model name
sold                // bool - already purchased
discounted          // bool - has discount
discountRatio       // float - price multiplier (e.g., 0.8)
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Trade service | `Eremite.Services.ITradeService` |
| Visit state | `Eremite.Model.Trade.TraderVisitState` |
| Trader model | `Eremite.Model.Trade.TraderModel` |
| Effect state | `Eremite.Model.Trade.TraderEffectState` |
| Trader popup | `Eremite.View.HUD.TraderPopup` |

---

## Black Market System

### Overview

The Black Market is a special building that offers goods for purchase with amber. Offers can be bought outright or on credit (with payment due in future seasons).

### BlackMarket (Building)

```csharp
state               // BlackMarketState
model               // BlackMarketModel
Buy(offer)          // Purchase offer outright
BuyOnCredit(offer)  // Purchase with deferred payment
Reroll()            // Refresh available offers
IsRerollOnCooldown() // bool
GetTimeLeftFor(offer) // float - time until offer expires
```

### BlackMarketState

```csharp
offers              // BlackMarketOfferState[] - current offers
lastReroll          // float - game time of last reroll
amberSpent          // int - total amber spent
```

### BlackMarketOfferState

```csharp
good                // Good - item and amount
buyPrice            // int - amber cost for buy
creditPrice         // int - amber cost for credit
buyRating           // DealRating - good/regular/bad
creditRating        // DealRating
bought              // bool - already purchased
paymentModel        // PaymentEffectModel - credit terms
endTime             // float - offer expiration
```

### BlackMarketModel

```csharp
rerollPrice         // GoodRef - cost to reroll
rerollCooldown      // float - time between rerolls
```

### DealRating Enum

```csharp
Good                // Better than average price
Regular             // Normal price
Bad                 // Worse than average price
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Black market building | `Eremite.Buildings.BlackMarket` |
| State | `Eremite.Model.State.BlackMarketState` |
| Offer state | `Eremite.Model.State.BlackMarketOfferState` |
| Model | `Eremite.Buildings.BlackMarketModel` |
| Popup | `Eremite.View.HUD.BlackMarketPopup` |

---

## Altar System (Forsaken Altar)

### Overview

The Forsaken Altar allows sacrificing resources/villagers in exchange for upgraded cornerstones. Players configure what to sacrifice, then choose from available effects.

### IAltarService

```csharp
HasActivePick()              // bool - altar pick is available
AreVillagersAllowed()        // bool - villagers included in sacrifice
SwitchVillagersAllowed()     // Toggle villager sacrifice
SumAllowedMetaValue()        // int - total meta value of enabled items
SumAllowedRaces()            // int - count of enabled races
IsAllowedRace(string)        // bool - race enabled for sacrifice
IsAllowedCurrency(string)    // bool - currency enabled
SwitchRace(string)           // Toggle race
SwitchCurrency(string)       // Toggle currency
GetFullMetaPriceFor(effect)  // int - total cost
GetVillagersPriceFor(effect) // int - villagers required
CanBuy(effect)               // bool - can afford
IsUpgrade(effect)            // bool - upgrading existing cornerstone
Pick(effect)                 // Execute selection
```

### AltarChargesState

```csharp
lastPickedCharge    // int - index of last used charge
currentPick         // AltarPickState - current pick options
```

### AltarPickState

```csharp
options             // AltarEffectModel[] - available effects
```

### AltarEffectModel

```csharp
effect              // EffectModel - the cornerstone effect
metaPrice           // int - meta currency cost
villagersPrice      // int - villagers required
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Altar service | `Eremite.Services.IAltarService` |
| Charges state | `Eremite.Model.State.AltarChargesState` |
| Pick state | `Eremite.Model.State.AltarPickState` |
| Effect model | `Eremite.Model.AltarEffectModel` |
| Panel | `Eremite.View.HUD.AltarPanel` |

---

## Game Result System

### Overview

Handles victory/defeat screen display, progression data, score breakdown, and world event completion info.

### State Access

```csharp
// Win/Loss detection
StateService.GameObjectives.hasWon   // bool
StateService.GameObjectives.hasLost  // bool

// Sealed biome check
GameSealService.IsSealedBiome()      // bool - playing sealed biome

// Tutorial check
TutorialService.IsAnyTutorial        // bool
```

### Score Calculation

```csharp
ScoreCalculator.GetScore()           // ScoreData[] - breakdown of score components
```

### ScoreData

```csharp
label               // string - category name (e.g., "Reputation", "Population")
points              // int - points earned
amount              // int - raw value (e.g., rep earned, villagers)
```

### Progression Data

```csharp
// MetaStateService.Economy
currentCycleExp     // int - XP earned this game

// MetaStateService.Level
level               // int - current citadel level
exp                 // int - current XP
targetExp           // int - XP needed for next level
```

### World Event Goals

```csharp
// WorldStateService.Cycle
activeCycleGoals    // GoalState[] - world event objectives

// GoalState
model               // string - goal model name
completed           // bool
GetObjectivesBreakdown() // string - progress text
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| State service | `Eremite.Services.IStateService` |
| Game seal service | `Eremite.Services.IGameSealService` |
| Score calculator | `Eremite.Services.ScoreCalculator` |
| Meta state service | `Eremite.Services.IMetaStateService` |
| World state service | `Eremite.Services.IWorldStateService` |
| Game result popup | `Eremite.View.Popups.GameResultPopup` |

---

## PerkCrafter System (Cornerstone Forge)

### Overview

The Cornerstone Forge allows crafting custom cornerstones by combining hooks (triggers), positive effects, and optionally negative effects. Each forge provides 3 crafting charges.

### PerkCrafter (Building)

```csharp
state               // PerkCrafterState
model               // PerkCrafterModel
HasUsedAllCharges() // bool - all 3 crafts done
GetUsesLeft()       // int - remaining crafts
IsNegativePicked()  // bool - negative effect selected
ChangeHook(tierState)       // Select hook
ChangePositive(tierState)   // Select positive effect
ChangeNegative(tierState)   // Select negative effect
CreateCurrentPerk()         // Execute craft
ChangeName(name, isLocalized) // Set result name
GetResultDisplayName()      // string - current result name
```

### PerkCrafterState

```csharp
crafting            // PerkCraftingState - current session
craftedPerks        // int - number completed
results             // List<string> - effect names of crafted perks
```

### PerkCraftingState

```csharp
hooks               // TierState[] - available hook options
positiveEffects     // TierState[] - available positive options
negativeEffects     // TierState[] - available negative options
pickedHook          // int - selected hook index
pickedPositive      // int - selected positive index
pickedNegative      // int - selected negative index (-1 = none)
resultName          // string - custom name
```

### PerkCrafterModel

```csharp
charges             // int - total crafts allowed (3)
price               // GoodRef - cost per craft
effectsElements     // CraftedEffectElementsContainer - hook/effect pools
```

### CraftedEffectElementsContainer

```csharp
hooksSets           // HookLogic[][] - hook pools by tier
effectsSets         // EffectModel[][] - effect pools by tier
displayNames        // LocaText[] - random name options
GetHook(tierState)  // HookLogic
GetEffect(tierState) // EffectModel
```

### TierState

Represents a selection in the crafting system (combines tier index and item index).

### HookLogic

```csharp
Description         // string - trigger description (e.g., "During Storm")
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Perk crafter building | `Eremite.Buildings.PerkCrafter` |
| State | `Eremite.Buildings.PerkCrafterState` |
| Crafting state | `Eremite.Buildings.PerkCraftingState` |
| Model | `Eremite.Buildings.PerkCrafterModel` |
| Elements container | `Eremite.Model.Effects.CraftedEffectElementsContainer` |
| Tier state | `Eremite.Model.Effects.TierState` |
| Hook logic | `Eremite.Model.Effects.HookLogic` |
| Popup | `Eremite.Buildings.UI.PerkCrafters.PerkCrafterPopup` |

---

## Capital/Citadel System

### Overview

The Capital screen is the hub between settlements. From here players access upgrades, deeds, game history, daily expeditions, and training expeditions.

### WorldBlackboardService Subjects

```csharp
OnCapitalEnabled             // Observable - capital screen opened
OnCapitalClosed              // Observable - capital screen closed
CapitalUpgradePanelRequested // Subject<bool> - open upgrades panel
HomePopupRequested           // Subject<Unit> - open home popup
GenderPickPopupRequested     // Subject<Unit> - open gender pick popup
DailyChallengePopupRequested // Subject<bool> - open daily expedition
CustomGamePopupRequested     // Subject<bool> - open training expedition
```

### MetaPerksService Unlock Checks

```csharp
IsHomeEnbabled()             // bool - note typo in game API
IsDailyChallengeEnabled()    // bool
IsCustomGameEnabled()        // bool
AreGoalsEnabled()            // bool - deeds unlocked
```

### BlackboardService (from AppServices)

```csharp
GoalsPopupRequested          // Subject<bool> - open deeds popup
GamesHistoryPopupRequested   // Subject<bool> - open history popup
```

### Gender/Narration State

```csharp
MetaStateService.Narration.handType  // int - -1 if not picked, >= 0 if picked
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| World blackboard | `Eremite.Services.World.IWorldBlackboardService` |
| Meta perks service | `Eremite.Services.IMetaPerksService` |
| Meta state service | `Eremite.Services.IMetaStateService` |
| Narration state | `Eremite.Model.State.NarrationState` |
| App blackboard | `Eremite.Services.IBlackboardService` |

---

## Deeds/Goals System

### Overview

Deeds are meta-progression achievements with rewards. They're accessed via the GoalsPopup from the Capital screen.

### Service Access

```
MetaController.Instance → MetaServices → MetaStateService → Goals (MetaGoalsState)
MetaController.Instance → MetaServices → GoalsService
```

### MetaGoalsState Fields

```csharp
goals                  // List<GoalState> - all goal states
```

### GoalState Fields

```csharp
model                  // string - internal name (resolve via Settings.GetGoal)
completed              // bool - objectives met
rewarded               // bool - reward claimed
```

### GoalModel Fields/Properties

```csharp
label                  // GoalCategoryModel - category reference
displayName            // LocaText
Description            // string (property) - localized
isActive               // bool - goal is active
isCycleGoal            // bool - world event goal (filter out in deeds)
rewards                // MetaRewardModel[] - rewards to claim
HasAccessTo()          // bool - DLC/demo access check
GetMetaProgressText(GoalState)  // string - progress text
```

### GoalCategoryModel Fields

```csharp
displayName            // LocaText (inherited from LabelModel)
order                  // int - sort order
isHiddenCategory       // bool - only show completed goals
```

### IGoalsService Methods

```csharp
RewardGoal(GoalState, GoalModel)  // Claim reward
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Goals popup | `Eremite.WorldMap.UI.Goals.GoalsPopup` |
| Goal state | `Eremite.Model.Goals.GoalState` |
| Goal model | `Eremite.Model.Goals.GoalModel` |
| Meta goals state | `Eremite.Model.State.MetaGoalsState` |
| Category model | `Eremite.Model.Goals.GoalCategoryModel` |
| Goals service | `Eremite.Services.IGoalsService` |
| Meta reward model | `Eremite.Model.Meta.MetaRewardModel` |

---

## Trade Routes System

### Overview

Trade routes are persistent goods-for-amber exchanges with discovered towns. Separate from the Trader visits system.

### Service Access

```
GameController.Instance → GameServices → TradeRoutesService
GameController.Instance → GameServices → StateService → Trade (TradeState)
```

### TradeState Fields

```csharp
tradeTowns             // List<TradeTownState> - discovered towns
routes                 // List<RouteState> - active routes
```

### TradeTownState Fields

```csharp
id                     // int - unique ID
townName               // string - display name (or loca key if hasStaticName)
hasStaticName          // bool - townName is a localization key
biome                  // string - biome internal name
faction                // string - faction name (nullable)
distance               // int - distance from capital
standingLevel          // int - reputation level
isMaxStanding          // bool
currentStandingValue   // int
valueForLevelUp        // int
offers                 // List<TownOfferState>
```

### TownOfferState Fields

```csharp
townId                 // int
townName               // string
good                   // Good - base good per unit
fuel                   // int - base fuel amount
price                  // Good - amber reward
amount                 // int - current multiplier (1-5)
travelTime             // float - base travel time
accpeted               // bool - note typo in game API
hasStaticName          // bool
```

### RouteState Fields

```csharp
townId                 // int
townName               // string
good                   // Good - goods being traded
fuel                   // Good
price                  // Good - amber reward
travelTime             // float
startTime              // float
progress               // float - 0-1
offerAmount            // int - multiplier used
hasStaticName          // bool
```

### ITradeRoutesService Methods

```csharp
CanCollect(RouteState)           // bool
Collect(RouteState)              // void
AcceptOffer(TownOfferState)      // void
CanAccept(TownOfferState)        // bool
CanAcceptAnyAmount(TownOfferState) // bool - for "only available" filter
GetOfferExtendingPrice(TradeTownState)  // Good - cost to add offer slot
ReachedMaxOffers(TradeTownState) // bool
CanExtendOffer(TradeTownState)   // bool
ExtendOffer(TradeTownState)      // void
GetStandingLabelFor(TradeTownState)  // string - e.g., "Friendly"
GetFullGood(TownOfferState)      // Good - scaled by multiplier
GetFullPrice(TownOfferState)     // Good
GetFullFuel(TownOfferState)      // Good
GetFullTravelTime(TownOfferState) // float
HaveEnoughGoodsFor(TownOfferState) // bool
HaveEnoughFuelFor(TownOfferState)  // bool
HasReachedLimit()                // bool - max active routes
```

### IEffectsService

```csharp
GetTradeRoutesAmount()           // int - max routes allowed
```

### Preferences (PrefsState)

```csharp
autoCollectTradeRoutes           // bool
onlyAvailableTradeRoutes         // bool - filter to affordable offers
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Trade routes service | `Eremite.Services.ITradeRoutesService` |
| Trade state | `Eremite.Model.State.TradeState` |
| Town state | `Eremite.Model.State.TradeTownState` |
| Offer state | `Eremite.Model.State.TownOfferState` |
| Route state | `Eremite.Model.State.RouteState` |
| Popup | `Eremite.View.HUD.TradeRoutesPopup` |

---

## Seal System

### Overview

The Seal is a special building with 4 stages. Each stage has multiple offering choices, each with an order to complete. Completing offerings triggers plague effects during Storm season.

### Building Access

```csharp
BuildingsService.Seals           // Dictionary<int, Seal>
```

### Seal Methods

```csharp
IsSealCompleted()                // bool - all 4 stages done
GetFirstUncompletedKit()         // SealKitState - current stage
GetModelFor(SealKitState)        // SealKitModel
IsKitCompleted(SealKitState)     // bool
GetCompletedPartFor(SealKitState) // SealPartModel - chosen offering
```

### SealKitState (Stage State)

```csharp
completedIndex         // int - -1 if not completed
orders                 // OrderState[] - one per offering option
```

### SealKitModel (Stage Model)

```csharp
dialogue               // LocaText - NPC text
parts                  // SealPartModel[] - offering options
reward                 // EffectModel - granted on completion
```

### SealPartModel (Offering Model)

```csharp
displayName            // LocaText
description            // LocaText
order                  // OrderModel - requirements to complete
```

### Plague State (SealGameState)

```csharp
// Via StateService.SealGame
currentEffect          // string - active plague (empty if not in storm)
nextEffect             // string - plague for next storm
```

### IGameSealService Methods

```csharp
CompletePart(SealKitState, SealKitModel, int index)  // Complete offering
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Seal building | `Eremite.Buildings.Seal` |
| Seal state | `Eremite.Buildings.SealState` |
| Stage state | `Eremite.Buildings.SealKitState` |
| Stage model | `Eremite.Buildings.SealKitModel` |
| Offering model | `Eremite.Buildings.SealPartModel` |
| Game seal state | `Eremite.Model.State.SealGameState` |
| Game seal service | `Eremite.Services.IGameSealService` |
| Panel | `Eremite.Buildings.UI.Seals.SealPanel` |

---

## Consumption Control System

### Overview

Consumption control lets players enable/disable food types and services (needs) per race. Can be blocked by certain effects.

### Service Access

```
GameController.Instance → GameServices → NeedsService
GameController.Instance → GameServices → EffectsService
GameController.Instance → GameServices → RacesService
```

### INeedsService Methods

```csharp
// Raw food permissions
IsPermited(string rawFood)       // bool
SetPermision(string rawFood, bool isOn)  // void
IsAllRawFoodPermited()           // bool
IsAllRawFoodProhibited()         // bool

// Race+Need permissions
IsPermited(RaceModel, NeedModel) // bool
SetPermision(RaceModel, NeedModel, bool isOn)  // void
GetCurrentResolveImpact(RaceModel, NeedModel)  // int
GetMaxResolveImpact(RaceModel, NeedModel)      // int
```

### IEffectsService

```csharp
IsConsumptionControlBlocked()    // bool
GetEffectsDisplayList(List<string>)  // string - comma-separated names
```

### Blocking Effects

```csharp
StateService.Effects.consumptionControlLocks  // List<string> - effect names
```

### NeedModel Fields

```csharp
canBeProhibited        // bool
category               // NeedCategoryModel
DisplayName            // string (property)
```

### NeedCategoryModel Fields

```csharp
displayName            // LocaText
isHouseBased           // bool - filter out for consumption popup
```

### RaceModel Fields/Methods

```csharp
displayName            // LocaText
needs                  // NeedModel[]
HasNeed(NeedModel)     // bool
```

### IRacesService

```csharp
Races                  // RaceModel[] (property)
IsRevealed(RaceModel)  // bool
```

### Raw Food Access

```csharp
StateService.Actors.rawFoodConsumptionPermits  // Dictionary<string, bool>
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Needs service | `Eremite.Services.INeedsService` |
| Effects service | `Eremite.Services.IEffectsService` |
| Races service | `Eremite.Services.IRacesService` |
| Need model | `Eremite.Model.NeedModel` |
| Need category | `Eremite.Model.NeedCategoryModel` |
| Race model | `Eremite.Model.RaceModel` |
| Consumption popup | `Eremite.View.Popups.Consumption.ConsumptionPopup` |

---

## Trends System

### Overview

Trends track goods flow over time — production, consumption, trade. Accessed via the TrendsPopup.

### Service Access

```
GameController.Instance → GameServices → StateService → Trends (TrendsState)
GameController.Instance → GameServices → StorageOperationsService
```

### TrendsState Fields

```csharp
goodsOperations        // Dictionary<string, List<StorageOperation>>
totalTicks             // int - current tick count
```

### StorageOperation Fields

```csharp
amount                 // int - positive = gain, negative = loss
trendTick              // int - when this operation occurred
```

### IStorageOperationsService Methods

```csharp
GetDisplayName(StorageOperation)  // string - source label (building name, "Consumption", etc.)
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Trends popup | `Eremite.View.Trends.TrendsPopup` |
| Trends state | `Eremite.Model.State.TrendsState` |
| Storage operation | `Eremite.Model.StorageOperation` |
| Operations service | `Eremite.Services.IStorageOperationsService` |

---

## Daily Expedition System

### Overview

Daily Expeditions are timed challenges that reset at midnight UTC. Players select a difficulty and embark with fixed starting conditions (biome, races, goods, effects, modifiers).

### Popup Type

```csharp
Eremite.WorldMap.UI.DailyChallengePopup
```

### DailyChallengeData Fields

Contains the fixed challenge parameters:

```csharp
biome               // BiomeModel - the biome for today's challenge
initialVillagers    // List<string> - race names for starting villagers
embarkGoods         // List<Good> - starting goods
embarkEffects       // List<string> - starting effect names
earlyModifiers      // List<string> - early game modifier effect names
lateModifiers       // List<string> - late game modifier effect names
baseRewards         // List<MetaCurrency> - base meta currency rewards
```

### DailyDifficultyPicker (extends DifficultyPicker)

```csharp
GetDifficulties()           // IList<DifficultyModel> - available difficulties
GetPickedDifficulty()       // DifficultyModel - current selection
SetDifficulty(DifficultyModel)  // Change selection
```

### DifficultyModel Fields (for Daily)

```csharp
index               // int - difficulty level
positiveEffects     // int - positive seasonal mystery count
negativeEffects     // int - negative seasonal mystery count
effectsMagnitude    // LocaText - severity description
GetDisplayName()    // string - localized name (e.g., "Pioneer")
```

### IDailyService (via MetaServices)

```csharp
IsCompletedToday(DifficultyModel)   // bool - already completed at this difficulty
GetRewardsFor(difficulty, baseRewards)  // MetaCurrency[] - adjusted rewards
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Daily popup | `Eremite.WorldMap.UI.DailyChallengePopup` |
| Challenge data | `Eremite.WorldMap.DailyChallengeData` |
| Difficulty picker | `Eremite.WorldMap.UI.DailyDifficultyPicker` |
| Daily service | `Eremite.Services.IDailyService` |
| Meta currency | `Eremite.Model.MetaCurrency` |
| Meta currency model | `Eremite.Model.MetaCurrencyModel` |

---

## Custom Games System (Training Expeditions)

### Overview

Training Expeditions allow full customization of game parameters: difficulty, biome, races, season durations, seasonal effects, blight settings, modifiers, trade towns, embark goods, and embark effects.

### Popup Type

```csharp
Eremite.WorldMap.UI.CustomGames.CustomGamePopup
```

### Panel Types

The popup contains multiple sub-panels for different configuration areas:

| Panel | Type | Purpose |
|-------|------|---------|
| Difficulty | `DifficultyPicker` | Prestige level selection |
| Seed | `CustomGameSeedPanel` | Map seed input |
| Biome | `CustomGameBiomePanel` | Biome dropdown selection |
| Races | `CustomGameRacesPanel` | Starting race selection (multi-select) |
| Reputation | `CustomGameReputationPanel` | Win/lose thresholds, impatience rate |
| Seasons | `CustomGameSeasonsDurationPanel` | Season duration sliders |
| Seasonal Effects | `CustomGameSeasonalEffectsPanel` | Random or manual effect picks |
| Blight | `CustomGameBlightPanel` | Blight toggle, footprint, corruption |
| Modifiers | `CustomGameModifiersPanel` | World/difficulty modifier toggles |
| Trade Towns | `CustomGameTradeTownsPanel` | Trade town selection |
| Goods | `CustomGameEmbarkGoodsPanel` | Starting goods amounts |
| Effects | `CustomGameEmbarkEffectsPanel` | Starting effect selection |

### FloatOptionsSliderPanel

Used for sliders with discrete options (reputation, seasons, blight):

```csharp
options             // FloatOption[] - available values
currentIndex        // int - selected option index
GetPickedIndex()    // int
SetIndex(int)       // void
```

### FloatOption

```csharp
label               // LocaText - display label
amount              // float - the value
```

### ModifierData

```csharp
model               // ModifierModel
effect              // string - effect internal name
isPositive          // bool
isPicked            // bool - currently selected
type                // ModifierType enum (WorldMap=0, Daily=1, Difficulty=2)
```

### SeasonalEffect Types

Two types of seasonal effects available in manual mode:

```csharp
Settings.simpleSeasonalEffects      // Simple perk effects
Settings.conditionalSeasonalEffects // Conditional trigger effects
```

Each effect has:
```csharp
IsInCustomMode      // bool - available in training expeditions
Name                // string - internal name
DisplayName         // string - localized name
Description         // string - localized description
IsPositive          // bool
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Custom game popup | `Eremite.WorldMap.UI.CustomGames.CustomGamePopup` |
| Seed panel | `Eremite.WorldMap.UI.CustomGames.CustomGameSeedPanel` |
| Biome panel | `Eremite.WorldMap.UI.CustomGames.CustomGameBiomePanel` |
| Races panel | `Eremite.WorldMap.UI.CustomGames.CustomGameRacesPanel` |
| Reputation panel | `Eremite.WorldMap.UI.CustomGames.CustomGameReputationPanel` |
| Seasons panel | `Eremite.WorldMap.UI.CustomGames.CustomGameSeasonsDurationPanel` |
| Seasonal effects panel | `Eremite.WorldMap.UI.CustomGames.CustomGameSeasonalEffectsPanel` |
| Blight panel | `Eremite.WorldMap.UI.CustomGames.CustomGameBlightPanel` |
| Modifiers panel | `Eremite.WorldMap.UI.CustomGames.CustomGameModifiersPanel` |
| Trade towns panel | `Eremite.WorldMap.UI.CustomGames.CustomGameTradeTownsPanel` |
| Goods panel | `Eremite.WorldMap.UI.CustomGames.CustomGameEmbarkGoodsPanel` |
| Effects panel | `Eremite.WorldMap.UI.CustomGames.CustomGameEmbarkEffectsPanel` |
| Slider panel | `Eremite.WorldMap.UI.CustomGames.FloatOptionsSliderPanel` |
| Float option | `Eremite.Model.Configs.CustomGame.FloatOption` |
| Modifier data | `Eremite.WorldMap.ConditionsCreator.ModifierData` |
| Layouts popup | `Eremite.WorldMap.UI.CustomGames.CustomGameLayoutsPopup` |

---

## Payments System

### Overview

Payments are obligations (taxes, tithes) that must be paid by a due date. Each payment has a good cost, due date, auto-payment setting, and penalty for non-payment.

### Popup Type

```csharp
Eremite.View.Popups.Recipes.PaymentsPopup
```

### Service Access

```
GameController.Instance → GameServices → PaymentsService
GameController.Instance → GameServices → StateService → Effects → payments
```

### IPaymentsService Methods

```csharp
Pay(PaymentState)           // Execute payment
CanPay(PaymentState)        // bool - can afford
GetModel(PaymentState)      // PaymentEffectModel - type/source labels
```

### PaymentState Fields

```csharp
payment             // Good - amount owed
dueDate             // GameDate - when due
autoPaymentType     // AutoPaymentType enum
model               // string - effect model name
penaltyModel        // string - penalty effect name
```

### AutoPaymentType Enum

```csharp
None = 0            // Manual payment only
Instant = 1         // Pay immediately when goods available
End = 2             // Pay at last minute before due
```

### GameDate Fields

```csharp
year                // int
season              // int (0=Drizzle, 1=Clearance, 2=Storm)
```

### PaymentEffectModel Fields

```csharp
typeLabel           // LabelModel - "Tax", "Tithe", etc.
sourceLabel         // LabelModel - source description
```

### ICalendarService

```csharp
GetSecondsLeftTo(GameDate)  // float - time remaining
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Payments popup | `Eremite.View.Popups.Recipes.PaymentsPopup` |
| Payments service | `Eremite.Services.IPaymentsService` |
| Payment state | `Eremite.Model.State.PaymentState` |
| Payment effect model | `Eremite.Model.Effects.Payment.PaymentEffectModel` |
| Game date | `Eremite.Model.State.GameDate` |
| Auto payment type | `Eremite.Model.State.AutoPaymentType` |
| Calendar service | `Eremite.Services.ICalendarService` |

---

## World Event System

### Overview

World Events are decision popups on the world map with multiple choice options. Each option may have requirements and consequences.

### Popup Type

```csharp
Eremite.WorldMap.UI.WorldEvents.WorldEventPopup
```

### WorldEventPopup Fields

```csharp
worldEvent          // WorldEvent instance (private)
```

### WorldEvent Fields

```csharp
model               // WorldEventModel
state               // WorldEventState
```

### WorldEventModel Fields/Methods

```csharp
displayName         // LocaText - event title
description         // LocaText - event description
options             // WorldEventLogic[] - decision options

GetDescriptionForOption(int index)  // string - option description
CanExecute(int index)               // bool - option available
GetExecutionBlockReason(int index)  // string - why blocked
ExecuteDecision(WorldEventState, int index)  // UniTask<bool> - execute choice
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| World event popup | `Eremite.WorldMap.UI.WorldEvents.WorldEventPopup` |
| World event controller | `Eremite.WorldMap.Controllers.WorldEvent` |
| World event model | `Eremite.Model.WorldEventModel` |
| World event state | `Eremite.WorldMap.WorldEventState` |
| World event logic | `Eremite.Model.WorldEventLogic` |

---

## Games History System

### Overview

Tracks play history with detailed records of past settlements, career stats, and meta perk state.

### Popup Type

```csharp
Eremite.WorldMap.UI.History.GamesHistoryPopup
```

### Service Access

```
MetaController.Instance → MetaServices → MetaStateService
  → GamesHistory (GamesHistoryState)
  → Stats (MetaStats)
  → Perks (MetaPerksState)
  → Goals (MetaGoalsState)
```

### GamesHistoryState Fields

```csharp
records             // List<GameHistoryState> - past settlements
```

### GameHistoryState Fields

```csharp
name                // string - settlement name (or loca key if hasStaticName)
hasStaticName       // bool - name is a localization key
hasWon              // bool - victory or defeat
difficulty          // string - difficulty model name
biome               // string - biome model name
level               // int - citadel level at end
upgrades            // int - building upgrades count
years               // int - years survived
gameTime            // float - real time in seconds
races               // Dictionary<string, int> - race counts
cornerstones        // List<string> - effect names
modifiers           // List<string> - modifier effect names
buildings           // List<string> - building model names
seasonalEffects     // List<string> - seasonal effect names
```

### MetaStats Fields

```csharp
gamesWon            // int - total victories
gamesLost           // int - total defeats
timeSpentInGame     // double - total play time in seconds
```

### MetaPerksState Fields (26 Upgrade Categories)

Integer bonuses:
```csharp
bonusReputationRewardsPicks    bonusPreparationPoints
bonusSeasonRewardsAmount       bonusCaravans
bonusTradeRoutesLimit          bonusCapitalVision
bonusTownsVision               bonusEmbarkRange
bonusTraderMerchSlots          rawDepositsChargesBonus
globalBuildingStorageBonus     bonusCornerstonesRerolls
bonusGracePeriod               globalCapacityBonus
bonusFarmArea
```

Float rate bonuses:
```csharp
currencyMultiplayer                    traderMerchandisePriceBonusRates
tradersIntervalBonusRate               reputationPenaltyBonusRate
globalSpeedBonusRate                   fuelConsumptionBonusRate
newcommersGoodsBonusRate               globalProductionSpeedBonusRate
hearthSacraficeTimeBonusRate           bonusEmbarkGoodsAmount
globalExtraProductionChanceBonus
```

### CycleState Fields (via WorldStateService)

```csharp
gamesWonInCycle         // int
gamesPlayedInCycle      // int
sealFragments           // int - current fragments
totalSealFragments      // int - fragments needed
finishedModifiers       // List<string>
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| History popup | `Eremite.WorldMap.UI.History.GamesHistoryPopup` |
| Games history state | `Eremite.Model.State.GamesHistoryState` |
| Game history state | `Eremite.Model.State.GameHistoryState` |
| Meta stats | `Eremite.Model.State.MetaStats` |
| Meta perks state | `Eremite.Model.State.MetaPerksState` |
| Cycle state | `Eremite.WorldMap.CycleState` |
| Meta state service | `Eremite.Services.IMetaStateService` |

---

## Stats System

### Overview

Game statistics services track reputation, hostility, resolve, and villagers during a settlement. Used for status announcements and HUD data.

### Service Access

```
GameController.Instance → GameServices → ReputationService
GameController.Instance → GameServices → HostilityService
GameController.Instance → GameServices → ResolveService
GameController.Instance → GameServices → VillagersService
```

### ReputationService

```csharp
Reputation                      // ReactiveProperty<float> - current reputation
ReputationPenalty               // ReactiveProperty<float> - current impatience
State                           // GameObjectivesState

GetReputationToWin()            // float - reputation threshold for victory
GetReputationPenaltyToLoose()   // float - impatience threshold for defeat
GetReputationGainedFrom(source) // float - rep from specific source
GetReputationPenaltyPerSec()    // float - current impatience rate
GetBaseReputationPenaltyPerSec() // float - base impatience rate
```

### ReputationChangeSource Enum

Sources of reputation gain:
```csharp
Orders, Perks, CornerstoneRerolls, ResolvePoints
```

### GameObjectivesState Fields

```csharp
gracePeriodLeft     // float - seconds remaining in grace period
```

### HostilityService

```csharp
Points                          // ReactiveProperty<int> - current hostility points
Level                           // ReactiveProperty<int> - current hostility level

GetSourceAmount(source)         // int - count from source type
GetPointsFor(source)            // int - points from source type
GetPointsLeftToNextLevel()      // int
```

### HostilitySource Enum

```csharp
Glade, Dangerous, Forbidden, Cyst, Villager, Time
```

### ResolveService

```csharp
GetResolveFor(race)             // float - current resolve for race
GetMinResolveForReputation(race) // float - resolve threshold for rep bonus
GetTargetResolveFor(race)       // float - settling point
Effects                         // Dictionary - resolve effects
```

### VillagersService

```csharp
Races                           // Dictionary - race population data
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Reputation service | `Eremite.Services.ReputationService` |
| Hostility service | `Eremite.Services.HostilityService` |
| Resolve service | `Eremite.Services.ResolveService` |
| Villagers service | `Eremite.Services.VillagersService` |
| Game objectives state | `Eremite.Model.State.GameObjectivesState` |
| Reputation change source | `Eremite.Services.ReputationChangeSource` |
| Hostility source | `Eremite.Model.State.HostilitySource` |

---

## Ironman System (Queen's Hand Trial)

### Overview

Queen's Hand Trial is an ironman mode with unique upgrades. Players choose from 3 random pick options after completing milestones, plus core upgrades available any time.

### Popup Type

```csharp
Eremite.WorldMap.UI.IronmanUpgradePopup
```

### Service Access

```
MetaController.Instance → MetaServices → IronmanService
```

### IIronmanService Methods

```csharp
GetCompletedPicks()             // int - picks completed
HasReachedMaxPicks()            // bool
GetCurrentPick()                // IronmanPickState - current 3 options
```

### IronmanService Methods (concrete class)

```csharp
CanAfford(CapitalUpgradeModel)  // bool
IsUnlocked(CapitalUpgradeModel) // bool - already purchased
IsCore(CapitalUpgradeModel)     // bool - is a core upgrade
Pick(CapitalUpgradeModel)       // void - purchase upgrade
```

### IronmanConfig Fields (via Settings)

```csharp
coreUpgrades        // CapitalUpgradeModel[] - always-available upgrades
picks               // IronmanPickConfig[] - milestone pick configs
```

### IronmanPickState Fields

```csharp
options             // IronmanPickOption[] - 3 random options
```

### IronmanPickOption Fields

```csharp
model               // string - CapitalUpgradeModel name
```

### CapitalUpgradeModel Fields (Ironman-specific)

```csharp
ironmanDisplayName  // LocaText - name in ironman context
ironmanPrice        // MetaCurrencyRef[] - ironman-specific cost
rewards             // MetaRewardModel[] - unlock rewards
```

### MetaCurrencyRef Fields

```csharp
currency            // MetaCurrencyModel
amount              // int
```

### MetaRewardModel Properties

```csharp
DisplayName         // string
Description         // string
```

### Unlocked Upgrades Access

```
MetaStateService.Capital.unlockedUpgrades  // HashSet<string> - upgrade names
```

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Ironman popup | `Eremite.WorldMap.UI.IronmanUpgradePopup` |
| Ironman service | `Eremite.Services.IIronmanService` |
| Ironman service (impl) | `Eremite.Services.IronmanService` |
| Ironman config | `Eremite.Model.Configs.IronmanConfig` |
| Pick state | `Eremite.Model.State.IronmanPickState` |
| Pick option | `Eremite.Model.State.IronmanPickOption` |
| Capital upgrade model | `Eremite.WorldMap.CapitalUpgradeModel` |
| Meta currency ref | `Eremite.Model.MetaCurrencyRef` |
| Meta reward model | `Eremite.Model.Meta.MetaRewardModel` |
| Capital state | `Eremite.Model.State.CapitalState` |
