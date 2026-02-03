# CLAUDE.md

BepInEx accessibility mod for "Against the Storm" - screen reader support via Tolk. Uses Harmony patching and reflection.

## Build & Deploy

```powershell
powershell -ExecutionPolicy Bypass -File "C:\Users\rasha\Documents\ATS-Accessibility-Mod\build.ps1"                          # Release build + deploy to game folder
powershell -ExecutionPolicy Bypass -File "C:\Users\rasha\Documents\ATS-Accessibility-Mod\build.ps1" -Configuration Debug     # Debug build + deploy
```

**Note**: The path must be quoted and `-ExecutionPolicy Bypass` is required when running from bash.

For release packaging, see `RELEASE-INSTRUCTIONS.md`.

## Changelog

After each commit, append a one-line summary to the appropriate section in `changes.md` (New features / Bug fixes / Internal). Keep entries concise and user-facing where possible. On release, the file is cleared and restarted with a fresh `# Changes since vX.Y.Z` heading. **Always include `changes.md` in the commit itself** — do not leave it as a separate follow-up.

## Key Locations

- **Source**: `ATSAccessibility/`
- **Game reference**: `game-source/` (read-only decompiled)
- **Debug log**: `C:\Users\rasha\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` - check first for `[ATSAccessibility]` output

## Code Organization

**Reflection** (game API access): `*Reflection.cs` files — one per game system (e.g., `OrdersReflection.cs`, `BuildingReflection.cs`). Core game access via `GameReflection.cs`.

**Key handlers**: `KeyboardManager.cs` - priority chain, first active handler wins. Register in `AccessibilityCore.Start()`.

**Base classes**: `MenuBase` (all navigable menus/overlays), `BuildingSectionNavigator` (building panels, extends MenuBase)

**Overlays** (popup navigation): `*Overlay.cs` files — one per game popup. All extend `MenuBase`.

**Building navigators** (`Navigators/`): `*Navigator.cs` files — one per building type. All extend `BuildingSectionNavigator`.

**Tile Info**: `TileInfoReader.cs` - detailed info for I key on buildings, natural resources, deposits

**Events**: `EventAnnouncer.cs` - game event subscriptions with grace period and deduplication

**Audio**: `SoundManager.cs` - centralized game sound playback via reflection

---

## Design Patterns

### 1. Key Handler Pattern (IKeyHandler)

Priority chain where first active handler consumes the key.

```csharp
public class MyHandler: IKeyHandler {
	public bool IsActive => /* side-effect free check */;

	public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
		switch (keyCode) {
			case KeyCode.UpArrow:
				DoSomething();
				return true;
			case KeyCode.Escape:
				// Pass to game to close popup
				return false;
			default:
				// Consume all other keys while active
				return true;
		}
	}
}
```

- `IsActive` must be side-effect free - move cleanup to `ProcessKey()`
- Register in `AccessibilityCore.Start()` in priority order (highest first)
- **Consume by default**: Return `true` for all keys unless intentionally passing through
- **Document pass-throughs**: When returning `false`, add a comment explaining why (e.g., `// Pass to game to close popup`)

### 2. MenuBase Pattern

Base class for all keyboard-navigable menus and overlays. See `MenuBase.cs` for the full API (well-documented with doc comments).

- **Abstract members**: `OverlayName`, `EmptyMessage`, `GetItemCount()`, `GetLabel(int)`, `RefreshData()`, `OnEnter(int)` → `EnterAction`
- **Key virtuals**: `OnAction`, `OnSpace`, `OnAdjust`, `OnDrillDown`, `OnGoBack`, `OnEscape`, `HandleSpecialKey`, `AnnounceCurrentItem`
- **Lifecycle**: `Open()` → `RefreshData()` → `GetOpenAnnouncement()` → `OnOpened()` → navigation → `Close()` → `OnClosed()`
- **ProcessKey flow**: `HandleSpecialKey` → `_search.HandleKey` → standard navigation → consume by default
- **Navigation**: `_indices[level]` tracks position at each level (up to 8). `CurrentIndex` reads/writes current level. `Navigate(direction)` wraps via `NavigationUtils.WrapIndex()`.
- **Search**: Automatic via `ISearchable` — override `GetSearchName()` or `SearchItemCount` to customize
- **Nesting**: `Suspend()`/`Resume()` for nested popup handling

### 3. BuildingSectionNavigator Pattern

Extends `MenuBase` for building panels. Maps 4 navigation levels to building concepts:
- Level 0: Sections (Info, Workers, Recipes, Storage, etc.)
- Level 1: Items within section
- Level 2: Sub-items (recipe settings, worker details)
- Level 3: Sub-sub-items (ingredient options)

Provides compatibility properties (`_currentSectionIndex`, `_currentItemIndex`, `_currentSubItemIndex`) that map to MenuBase's `_indices` array. All building navigators in `Navigators/` extend this class.

### 4. Event Subscription Pattern

Grace period + FIFO deduplication for game events.

```csharp
private float _gracePeriodEndTime;  // Pre-calculated for consistent checks
private const float GRACE_PERIOD = 2f;
private HashSet<string> _announced = new HashSet<string>();
private Queue<string> _announcedOrder = new Queue<string>();

// Calculate end time once at subscription for consistent concurrent event handling
private void Subscribe() {
	_gracePeriodEndTime = Time.realtimeSinceStartup + GRACE_PERIOD;
	// ... subscribe to events
}

private bool IsInGracePeriod() => Time.realtimeSinceStartup < _gracePeriodEndTime;

private void OnEvent(object data) {
	if (IsInGracePeriod()) return;  // Skip initialization noise

	string key = GetUniqueKey(data);
	if (_announced.Contains(key)) return;  // Deduplicate

	_announced.Add(key);
	_announcedOrder.Enqueue(key);

	// FIFO eviction to prevent memory growth (never use Clear())
	while (_announced.Count > 100 && _announcedOrder.Count > 0)
		_announced.Remove(_announcedOrder.Dequeue());

	Speech.Say(FormatMessage(data));
}

public void Dispose() {
	foreach (var sub in _subscriptions) sub?.Dispose();
	_subscriptions.Clear();
	_announced.Clear();
	_announcedOrder.Clear();
}
```

### 5. Reflection Caching Pattern

Cache type metadata, never cache service instances (destroyed on scene change).

```csharp
// SAFE to cache (survives scene changes)
private static PropertyInfo _serviceProp;
private static bool _cached = false;

private void EnsureCached() {
	if (_cached) return;
	var type = GameReflection.GameAssembly.GetType("Eremite.Services.IGameServices");
	_serviceProp = type?.GetProperty("CalendarService");
	_cached = true;
}

// NEVER cache the result of this - get fresh each time
var service = _serviceProp?.GetValue(gameServices);
```

### 6. Reflection Dictionary Iteration

Direct cast to `Dictionary<K,V>` fails at runtime. Use reflection iteration:

```csharp
var keysProperty = dictObj.GetType().GetProperty("Keys");
var keys = keysProperty?.GetValue(dictObj) as IEnumerable;
var indexer = dictObj.GetType().GetMethod("get_Item");

foreach (var key in keys) {
	var value = indexer?.Invoke(dictObj, new[] { key });
	// Process key/value
}
```

---

## Conventions

- **Formatting**: All source files use tabs, K&R braces (opening brace on same line), and no space before `:` in inheritance (e.g., `class Foo: IBar`). See `.editorconfig`. When using the Edit tool, match the file's actual tab characters — the Read tool's display can be misleading.
- **Logging**: Prefix all with `[ATSAccessibility]`
- **Regex**: Use `new Regex(pattern, RegexOptions.Compiled)` as static fields
- **Navigation**: Use `NavigationUtils.WrapIndex()` for circular index wrapping
- **Null safety**: Always check reflection results; game API may change
- **Memory**: Limit deduplication sets to ~100 items; evict oldest
- **Key consumption**: Consume all keys by default (`return true`); document any pass-throughs with comments

## Announcement Style

Keep announcements **concise** - users are experienced screen reader users who prefer minimal verbosity.

**Avoid:**
- Item counts ("5 items", "3 of 10")
- Navigation hints ("press Enter to select", "use arrows to navigate")
- Redundant context ("You are now in...", "Currently viewing...")
- Type suffixes when obvious from context ("Lumber button", "Workers section")

**Prefer:**
- Just the essential information: name, state, value
- Format: `"Item name, relevant state"` not `"Item name, button, 3 of 10, press Enter to activate"`

**Examples:**
```csharp
// Good
Speech.Say("Lumber Mill");
Speech.Say("Planks recipe, active");
Speech.Say($"Slot 2: {workerName}");

// Avoid
Speech.Say("Lumber Mill, 1 of 5 buildings, press Enter to open");
Speech.Say("Planks recipe, active, 2 of 3 recipes");
Speech.Say($"Worker slot 2 of 4: {workerName}, press Enter to manage");
```

Users already know how navigation works - announce what they need to make decisions, not how to use the interface.

---

## Design Decisions

### Sounds

`SoundManager.cs` provides access to game sounds via reflection. Available methods include:
- `PlayButtonClick()` - standard UI click
- `PlayFailed()` - error/warning sound
- `PlayRecipeOn()`/`PlayRecipeOff()` - recipe toggle
- `PlayBuildingFireButtonStart()` - sacrifice enable
- `PlayBuildingSleep()`/`PlayBuildingWakeUp()` - pause toggle

**Policy**: Only add sounds when explicitly requested. Do not proactively add sounds to new features - let the user decide if audio feedback is needed for a particular action.

### Static Instance Management

Classes like `EventAnnouncer` that use static `_instance` for Harmony patch callbacks must clear the reference in `Dispose()` to prevent stale references after scene changes:

```csharp
public void Dispose() {
	// ... cleanup ...
	if (_instance == this)
		_instance = null;
}
```

### Reflection Method Return Values

Methods that invoke reflected game methods should return `false` if the method wasn't found, not `true`:

```csharp
// Correct
if (_someMethod == null) return false;
_someMethod.Invoke(...);
return true;

// Wrong - returns true even if nothing happened
_someMethod?.Invoke(...);
return true;
```
