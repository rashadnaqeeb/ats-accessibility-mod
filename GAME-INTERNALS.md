# Game Internals Reference

Reference documentation for "Against the Storm" game internals discovered through reflection. Update this file as new patterns are discovered.

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
