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
var displayName = model.displayName; // FieldInfo - localized name
var name = model.name;              // PropertyInfo - internal name (fallback)

// Building position
var position = building.Field;      // PropertyInfo → Vector2Int
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
