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

### AppServices

| Service | Purpose |
|---------|---------|
| PopupsService | Popup management, show/hide events |

### MetaServices

| Service | Purpose |
|---------|---------|
| TutorialService | Tutorial state and progression |

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
