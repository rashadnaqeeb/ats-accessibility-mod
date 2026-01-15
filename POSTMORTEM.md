# ATS Accessibility Mod - Postmortem

## Project Summary

An accessibility mod for "Against the Storm" using BepInEx and Harmony to provide screen reader support via Tolk library. The mod was functional but broke during a modularization refactor.

---

## 1. What Worked Well

### Features That Were Functional Before Refactor

#### UI/Popup Navigation
- **Panel-based navigation**: Hierarchical navigation through panels within popups
- **Element cycling**: Up/Down arrows to navigate buttons, sliders, toggles, dropdowns, input fields
- **Panel switching**: Left/Right arrows to switch between panels/tabs
- **Dropdown support**: Full navigation within dropdown menus
- **Element activation**: Enter/Space to activate buttons, toggle checkboxes
- **Tab detection**: Automatic refresh when tabs change

#### Map Navigation
- **Grid-based cursor**: 70x70 coordinate system for the game world
- **Arrow key movement**: Navigate map with bounds checking (0-69)
- **Tile announcements**: Real-time speech for terrain type
- **Fog of war detection**: Announces unexplored glades with danger levels
- **Building/resource detection**: Announces what's on current tile
- **Detailed info mode**: K key for position + terrain + passability + contents
- **Tooltip mode**: I key for building/resource tooltip information

#### Dialogue/Tutorial Handling
- **Automatic detection**: Via Harmony patches on TutorialTooltip and DecisionPopup
- **Multi-position navigation**: Cycle through title -> body -> OK button
- **Rich text stripping**: Removes formatting tags before announcing
- **Dismissal**: Space/Enter to close dialogues

#### Speech Output
- **Tolk integration**: Multi-screen reader support (JAWS, NVDA, etc.)
- **SAPI fallback**: Works without screen reader installed
- **Speech control**: Interrupt capability with Stop()

#### Wiki/Encyclopedia
- **Line-by-line reading**: Navigate wiki entries with Up/Down
- **Priority mode**: Wiki reading takes precedence over normal navigation

### Approaches That Worked

1. **Event-driven architecture** with AccessibilityEventManager as central hub
2. **Harmony postfix patches** - never modify behavior, only observe
3. **Direct `assembly.GetType()` lookups** instead of iterating all types
4. **Scene loading guards** (`Time.timeScale == 0` checks) in patches
5. **DontDestroyOnLoad** for persistent manager across scenes
6. **Live lookups for map data** - never cache tile state
7. **Type/method caching** for reflection results (PropertyInfo, MethodInfo)

---

## 2. What Broke and Why

### Final Crash Cause

**Root cause**: Combination of two issues during debugging:
1. `SetDllDirectory(ModFolder)` was removed from Plugin.cs
2. Tolk.dll was copied to game root directory (triggers file validation)

The game's file validation system detected a foreign DLL in the monitored game root directory, causing "FILE VALIDATION REQUIRED" error and freeze.

### Architectural Problems

1. **Modularization complexity**: Splitting into multiple files (Patches/, Managers/) made initialization order harder to track

2. **Service caching timing**: Cached services too early or too late, causing null references during scene transitions

3. **Flag logic errors**: `_subscribedToGameEvents = true` was set even when GameController was null, preventing proper re-subscription later

4. **Multiple initialization paths**: Speech.Initialize() called in different places during debugging (Awake vs Start)

### Hacky/Fragile Parts

1. **Reflection everywhere**: All game access via reflection is fragile - game updates can break it silently

2. **Polling fallbacks**: Some events required polling every 0.25s as backup for missed events

3. **UI element filtering**: Hardcoded list of ignored element names ("scrollbar", "background", etc.)

4. **Popup validation loop**: Periodic checks if tracked popup is still visible (detects missed close events)

5. **Multiple patch points**: Both PopupsService methods AND Popup base class methods patched as "backup"

---

## 3. Game Internals Discovered

### Controller Hierarchy

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

### Service Containers

```
Eremite.Services.GameServices
  - MapService
  - GladesService
  - ResourcesService
  - ModeService

Eremite.Services.AppServices
  - PopupsService
```

### Map Access (Fields/Tiles by Coordinate)

```csharp
// Get MapService from GameServices
var mapService = gameServices.MapService;

// Get field at coordinate (70x70 grid, 0-69)
var field = mapService.GetField(x, y);

// Field properties
string terrain = field.Type.ToString();  // "Water", "Forest", etc.
bool passable = field.IsTraversable;

// Get object on tile (building/resource)
var objectOnTile = mapService.GetObjectOn(x, y);

// Check if in glade (fog of war)
var gladesService = gameServices.GladesService;
var glade = gladesService.GetGlade(new Vector2Int(x, y));
if (glade != null) {
    uint dangerLevel = glade.Danger;
}
```

### Events and Hooks

**PopupsService Observables**:
- `AnyPopupShown` - IReadOnlyReactiveProperty<object>
- `AnyPopupHidden` - IReadOnlyReactiveProperty<object>

**ModeService**:
- `Idle` - IReadOnlyReactiveProperty<bool> (true = normal mode)

**GameController**:
- `OnGameStarted` - Observable for game start events

**Unity Scene Events**:
- `SceneManager.sceneLoaded`
- `SceneManager.sceneUnloaded`

### UI Hierarchy

**Popup Structure**:
```
PopupsService (manages all popups)
  -> Popup (base class)
     -> AnimateShow() / Hide()
     -> Contains child panels/elements

TabsPanel (tab container)
  -> current (TabsButton - active tab)
  -> TabsButton.content (GameObject - tab content)
```

**Element Types** (Unity UI):
- Button, Toggle, Slider, Dropdown (TMP_Dropdown), InputField, Scrollbar
- All inherit from Selectable base class

**Text Components**:
- TMPro.TMP_Text (TextMeshPro)
- UnityEngine.UI.Text (legacy)

### Key Class Names

| Purpose | Full Type Name |
|---------|----------------|
| Popup service | `Eremite.Services.PopupsService` |
| Popup base | `Eremite.View.Popups.Popup` |
| Tutorial tooltip | `Eremite.Tutorial.Views.TutorialTooltip` |
| Decision popup | `Eremite.View.DecisionPopup` |
| Tab panel | `Eremite.View.UI.TabsPanel` |
| Tab button | `Eremite.View.UI.TabsButton` |
| Game controller | `Eremite.Controller.GameController` |
| Main controller | `Eremite.Controller.MainController` |
| Map service | `Eremite.Services.MapService` |
| Glades service | `Eremite.Services.GladesService` |
| Mode service | `Eremite.Services.ModeService` |

---

## 4. Mistakes to Avoid Next Time

### Caching Issues

**DO**: Cache type references, PropertyInfo, MethodInfo (reflection metadata)
```csharp
// Good - cache once, use many times
_gameControllerType = assembly.GetType("Eremite.Controller.GameController");
_instanceProperty = _gameControllerType.GetProperty("Instance");
```

**DON'T**: Cache service instances across scene transitions
```csharp
// Bad - services are destroyed on scene unload
_cachedGameServices = GetGameServices(); // Will be null after scene change
```

**DON'T**: Cache map/game state data
```csharp
// Bad - stale data
_cachedTileContents[x,y] = GetTileContents(x, y);

// Good - always live lookup
var contents = mapService.GetObjectOn(x, y);
```

### Polling vs Events

**Use events for**:
- Popup open/close (Harmony patches on Show/Hide)
- Tab changes (Harmony patch on OnButtonClicked)
- Dialogue open/close (Harmony patches)
- Scene transitions (SceneManager events)

**Use polling only as fallback**:
- Settlement state (GameController.IsGameActive) - check every 0.25s
- Popup still visible validation - catch missed close events

### Initialization Order

1. **SetDllDirectory FIRST** - before any other code that might load DLLs
2. **Harmony patches in Awake()** - before game systems initialize
3. **Event subscriptions in Start()** - after game objects exist
4. **Service caching DEFERRED** - wait until scene is fully loaded

```csharp
private void Awake()
{
    // 1. DLL path setup
    SetDllDirectory(ModFolder);

    // 2. Harmony patches
    HarmonyInstance.PatchAll();

    // 3. Create persistent objects
    var go = new GameObject("Manager");
    DontDestroyOnLoad(go);
}

// In manager's Start():
private void Start()
{
    // 4. Subscribe to events
    SceneManager.sceneLoaded += OnSceneLoaded;

    // 5. DON'T cache services here - wait for scene load
}

private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    // 6. NOW cache services for this scene
    if (scene.name == "Game")
        CacheGameServices();
}
```

### Scene Transition Handling

**Clear on scene unload**:
- Cached service references
- Current popup/dialogue state
- Subscription disposables for scene-specific observables

**Preserve across scenes**:
- Manager GameObject (DontDestroyOnLoad)
- Type/reflection caches
- User preferences

**Re-initialize on scene load**:
- Service references
- Observable subscriptions
- State flags (IsInSettlement, etc.)

### DontDestroyOnLoad Usage

**DO persist**:
- Main manager GameObject
- Event hub (AccessibilityEventManager)
- Speech system
- Input manager

**DON'T persist references to**:
- Game services (scene-dependent)
- UI elements (destroyed on scene change)
- PopupsService instance

```csharp
// Correct pattern
var managerGO = new GameObject("ATSAccessibilityManager");
managerGO.hideFlags = HideFlags.HideAndDontSave;
Object.DontDestroyOnLoad(managerGO);
managerGO.AddComponent<AccessibilityManager>();

// In AccessibilityManager - DON'T store service refs as fields
// Instead, get fresh reference each time:
private GameServices GetGameServices()
{
    var gc = GetGameControllerInstance();
    return gc?.GetType().GetProperty("GameServices")?.GetValue(gc);
}
```

### Native DLL Loading

**Tolk.dll requirements**:
1. Must be in a directory Windows can find
2. SetDllDirectory points to plugins folder
3. NEVER put in game root (triggers file validation)
4. Helper DLLs (nvdaControllerClient64.dll, SAAPI64.dll) must be alongside Tolk.dll

```csharp
// Correct setup
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
private static extern bool SetDllDirectory(string lpPathName);

private void Awake()
{
    ModFolder = Path.GetDirectoryName(Info.Location); // plugins folder
    SetDllDirectory(ModFolder);
    // Now Tolk P/Invoke calls will find Tolk.dll
}
```

---

## 5. Recommended Architecture for Fresh Start

### Simpler Structure

```
ATSAccessibility/
  Plugin.cs              # Entry point, minimal code
  Speech.cs              # Tolk wrapper (keep as-is)
  AccessibilityCore.cs   # Single MonoBehaviour with all logic
  GameReflection.cs      # All reflection helpers in one place
```

### Key Principles

1. **Single MonoBehaviour** - avoid complex manager hierarchy
2. **Lazy service access** - get services when needed, don't cache
3. **Harmony patches minimal** - only patch what's absolutely needed
4. **Event-first, poll-backup** - prefer events, poll only for missed events
5. **Clear scene boundaries** - explicit cleanup/reinit on scene changes

### Startup Sequence

```
1. Plugin.Awake()
   - SetDllDirectory (FIRST!)
   - Create DontDestroyOnLoad GameObject
   - Apply Harmony patches

2. AccessibilityCore.Start()
   - Subscribe to SceneManager events
   - Initialize Speech

3. On "Game" scene load
   - Get fresh service references
   - Subscribe to game observables
   - Start accessibility features

4. On scene unload
   - Clear all cached references
   - Dispose subscriptions
   - Reset state flags
```

---

## Appendix: File Locations

- **Mod source**: `C:\Users\rasha\Documents\ATS-Accessibility-Mod\ATSAccessibility\`
- **Game install**: `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm\`
- **BepInEx plugins**: `...\Against the Storm\BepInEx\plugins\`
- **BepInEx log**: `...\Against the Storm\BepInEx\LogOutput.log`
