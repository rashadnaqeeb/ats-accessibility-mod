# CLAUDE.md

BepInEx 5 accessibility mod for "Against the Storm" (roguelite city-builder by Eremite Games) — screen reader support via Prism (`prism.dll`, vendored under `prism/native/win-x64/`). Uses HarmonyX patching and reflection against `Assembly-CSharp.dll` (namespace `Eremite`).

## Game Overview

Against the Storm is a roguelite city-builder. Players act as a Viceroy building settlements on a rectangular tile grid, managing resources, buildings, workers (7 species), and recipes. The win condition is filling the Reputation bar before Impatience fills. Each year has 3 seasons (Drizzle, Clearance, Storm). The overworld is a hex-grid World Map centered on the Smoldering City, with meta-progression via Seals, Deeds, and Capital upgrades. The game is fundamentally mouse-driven with no native screen reader or keyboard navigation support — this mod provides both. See `llm-docs/` for detailed game mechanics and API reference.

## Build & Deploy

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1                          # Release build + deploy to game folder
powershell -ExecutionPolicy Bypass -File build.ps1 -Configuration Debug     # Debug build + deploy
```

**Note**: Run from the repo root. `-ExecutionPolicy Bypass` is required when running from bash.

For release packaging, see `RELEASE-INSTRUCTIONS.md`.

## Changelog

`changes.md` is the full project changelog, ordered latest version first. The top section (`## Changes since vX.Y.Z`) tracks unreleased changes. After each commit, append a one-line summary to the appropriate subsection there (New features / Bug fixes / Internal). Keep entries concise and user-facing where possible. On release, rename that section to the new version number and add a fresh `## Changes since vX.Y.Z` section above it. **Always include `changes.md` in the commit itself** — do not leave it as a separate follow-up.

## Key Locations

- **Source**: `ATSAccessibility/`
- **Game reference**: `game-source/` (read-only decompiled)
- **Debug log**: `%USERPROFILE%\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` - check first for `[ATSAccessibility]` output
- **Game API reference**: `llm-docs/game-api-reference.md` — reflected game types, services, and members

## Code Organization

The codebase is organized into subdirectories by responsibility:

```
ATSAccessibility/
├── Core/        - Entry point, managers, base classes, interfaces
├── Overlays/    - Game popup navigation (*Overlay.cs, UINavigator, EncyclopediaNavigator)
├── Reflection/  - Game API access via reflection (*Reflection.cs, ReflectionHelper)
├── Handlers/    - Key handlers, mode controllers (*Handler.cs, *Controller.cs, MapNavigator)
├── Navigators/  - Building navigators + BuildingSectionNavigator base class
├── Utils/       - Utilities, formatters, readers, scanners, helpers
├── Panels/      - Information panels and menu hubs (*Panel.cs, MenuHub)
```

**Core/** (`ATSAccessibility.Core`): Entry point (`Plugin.cs`, `AccessibilityCore.cs`), managers (`KeyboardManager.cs`, `PopupRouter.cs`), base classes (`MenuBase.cs`), interfaces (`IKeyHandler.cs`, `IBuildingNavigator.cs`, `IHelpProvider.cs`), input handling (`InputPatches.cs`, `InputBlocker.cs`), help system (`HelpCollector.cs`), scene constants (`SceneConstants.cs`).

**Reflection/** (`ATSAccessibility.Reflection`): Game API access — one `*Reflection.cs` per game system. Core access via `GameReflection.cs`. All use `ReflectionHelper` for null-safe field/property/method access.

**Overlays/** (`ATSAccessibility.Overlays`): Popup navigation — one `*Overlay.cs` per game popup. Most extend `MenuBase`; `UINavigator`, `EncyclopediaNavigator`, and `MetaRewardsOverlay` implement `IKeyHandler` directly.

**Handlers/** (`ATSAccessibility.Handlers`): Key handlers and mode controllers. `MapNavigator.cs` and `WorldMapNavigator.cs` handle map-level navigation. `TutorialTooltipHandler.cs` auto-announces tutorial tooltips.

**Navigators/** (`ATSAccessibility.Navigators`): Building navigators — one per building type. All extend `BuildingSectionNavigator` (also in this directory). Includes shared helpers `BuildingWorkerSection.cs` and `BuildingUpgradesSection.cs`.

**Utils/** (`ATSAccessibility.Utils`): `Speech.cs`, `SoundManager.cs`, `EventAnnouncer.cs`, `TypeAheadSearch.cs`, `*Helper.cs`, `*Reader.cs`, `*Scanner.cs`, `FormattingUtils.cs`, `NavigationUtils.cs`.

**Panels/** (`ATSAccessibility.Panels`): Information panels (`*Panel.cs`), `MenuHub.cs`, `ConfirmationDialog.cs`, `HelpOverlay.cs` (F12 context-sensitive help).

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
- `ProcessKey()` is only called while `IsActive` is true — cleanup for "the game closed my thing without an event" cannot live in a `!IsActive` branch there; put it in a method polled from `AccessibilityCore.Update()` (see `BuildingPanelHandler.CleanupIfPanelClosed`)
- `KeyboardManager` isolates each handler in try/catch (one throwing handler can't kill the key pipeline), but treat that as a safety net — handlers must still not throw
- Register in `AccessibilityCore.Start()` via `KeyboardManager.RegisterHandler()` in priority order (highest first)
- Handlers holding per-settlement state (bookmarks, input modes, cached indices) need a `Reset()` wired into `AccessibilityCore.OnSceneUnloaded` — a settlement can end while any mode is armed, and stale mode state eats keys on the next map
- Key handlers are **separate** from popup routing — a popup overlay registers with both `PopupRouter` (for show/hide events) and `KeyboardManager` (for key input)
- **Consume by default**: Return `true` for all keys unless intentionally passing through
- **Document pass-throughs**: When returning `false`, add a comment explaining why (e.g., `// Pass to game to close popup`)

### 2. MenuBase Pattern

Base class for all keyboard-navigable menus and overlays. Implements `IKeyHandler` with a default `IsActive => IsOpen && !IsSuspended` — subclasses don't need to implement `IKeyHandler` separately. Override `IsActive` only for custom activation logic (e.g., `GameResultOverlay`). See `MenuBase.cs` for the full API (well-documented with doc comments).

- **Abstract members**: `OverlayName`, `EmptyMessage`, `GetItemCount()`, `GetLabel(int)`, `RefreshData()`, `OnEnter(int)` → `EnterAction`
- **Overlay-local state**: any navigation state a subclass keeps on top of MenuBase's `Level`/`_indices` (its own menu-level enum, mode flags, cached lists) MUST be reset in `OnClosed()` — MenuBase only resets its own state, and a survivor corrupts every future open (see AltarOverlay `_menuLevel`)
- **Text-field editing**: an overlay that hands focus to a real `TMP_InputField` and sets `InputBlocker.IsBlocking = false` MUST restore the flag and clear its editing state in `OnClosed()`/`ClearNavigationState()` too — the game can force-close the popup mid-edit, and a destroyed field must not early-return past that cleanup (see UINavigator `EndTextFieldEdit`)
- **Key virtuals**: `OnAction`, `OnSpace`, `OnAdjust`, `OnDrillDown`, `OnGoBack`, `OnEscape`, `HandleSpecialKey`, `AnnounceCurrentItem`
- **Lifecycle**: `Open()` → `RefreshData()` → `GetOpenAnnouncement()` → `OnOpened()` → navigation → `Close()` → `OnClosed()`
- **ProcessKey flow**: `HandleSpecialKey` → `_search.HandleKey` → standard navigation → consume by default
- **Navigation**: `_indices[level]` tracks position at each level (up to 8). `CurrentIndex` reads/writes current level. `Navigate(direction)` wraps via `NavigationUtils.WrapIndex()`.
- **Search**: Automatic via `ISearchable` — override `GetSearchName()` or `SearchItemCount` to customize
- **Nesting**: `Suspend()`/`Resume()` for nested popup handling

### 3. PopupRouter Pattern

Delegate-based routing for game popup show/hide events (via Harmony patches on `PopupService`). Each popup type registers a predicate and callbacks in `AccessibilityCore.Start()`.

```csharp
// Convenience form: predicate + onShow + MenuBase overlay (auto-wires onHide/forceClose)
_popupRouter.Register(RecipesReflection.IsRecipesPopup, _ => recipesOverlay.Open(), recipesOverlay);

// Full control form: explicit callbacks for each event
_popupRouter.Register(canHandle, onShow, onHide, forceClose, context);
```

- **Registration order matters** — first matching predicate wins (like key handlers)
- `HandlePopupShown(popup)` routes to `OnShow`, sets navigation context
- `HandlePopupHidden(popup)` routes to `OnHide`, returns whether caller should restore context
- `CloseAll()` force-closes all registered overlays (used on scene unload)
- Fallback chain: deeds child-capture → deeds suspend → generic UINavigator

### 4. BuildingSectionNavigator Pattern

Extends `MenuBase` for building panels. Maps 4 navigation levels to building concepts:
- Level 0: Sections (Info, Workers, Recipes, Storage, etc.)
- Level 1: Items within section
- Level 2: Sub-items (recipe settings, worker details)
- Level 3: Sub-sub-items (ingredient options)

Provides compatibility properties (`_currentSectionIndex`, `_currentItemIndex`, `_currentSubItemIndex`) that map to MenuBase's `_indices` array. All building navigators in `Navigators/` extend this class.

### 5. Event Subscription Pattern

Grace period + FIFO deduplication for game events.

**Retry-safe subscription is mandatory.** Subscription is polled from `Update()` until it succeeds, so a partial failure must dispose whatever half succeeded before returning — otherwise the next poll overwrites the live subscription without disposing it and stacks a duplicate every 0.5s (thousands per hour). Never set the `_subscribed` flag unless every subscription in the group is non-null, and never leave a successful subscription behind when it isn't.

```csharp
_subA = SubscribeToA(OnA);
_subB = SubscribeToB(OnB);
if (_subA != null && _subB != null) {
	_subscribed = true;
} else {
	// Partial failure: release the half that succeeded before the retry
	_subA?.Dispose(); _subB?.Dispose();
	_subA = null; _subB = null;
}
```

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

### 6. Reflection Caching Pattern

Cache type metadata via `ReflectionHelper.InitCache`. Never cache service instances (destroyed on scene change). Access field/property/method values through `ReflectionHelper` accessors.

```csharp
// SAFE to cache (survives scene changes)
private static PropertyInfo _serviceProp;
private static FieldInfo _nameField;
private static bool _cached = false;

private static void EnsureCached() {
	if (_cached) return;
	_cached = true;

	ReflectionHelper.InitCache("MyReflection", assembly => {
		var type = assembly.GetType("Eremite.Services.IGameServices");
		_serviceProp = type?.GetProperty("CalendarService");
		_nameField = type?.GetField("name");
	});
}

// NEVER cache the result of this - get fresh each time
var service = ReflectionHelper.GetProp(_serviceProp, gameServices);
string name = ReflectionHelper.GetString(_nameField, instance);
int count = ReflectionHelper.GetPropInt(_countProp, instance);
```

**ReflectionHelper accessor families** (all null-safe, return sensible defaults on failure — but not silently: every accessor logs a failing member once per session via `LogAccessError`, so game-API drift shows up in Player.log instead of reading as a confident 0/false/null to a user who cannot cross-check the screen; keep that property when adding accessors):
- **Fields**: `GetField`, `GetBool`, `GetInt`, `GetFloat`, `GetString`, `GetEnum`, `SetField`
- **Properties**: `GetProp`, `GetPropBool`, `GetPropInt`, `GetPropFloat`, `GetPropString`
- **Methods**: `Invoke`, `InvokeBool`, `InvokeInt`, `InvokeFloat`, `InvokeString`, `InvokeVoid` (`Invoke`/`InvokeVoid` have 0–3 arg overloads; others have fewer)
- **Collections**: `GetList`, `IterateKeys`, `DictGet`, `DictGetInt`
- **Localization**: `GetLocaString` (combines `GetField` + `GameReflection.GetLocaText`)

### 7. Reflection Dictionary Iteration

Direct cast to `Dictionary<K,V>` fails at runtime. Use `ReflectionHelper`:

```csharp
var keys = ReflectionHelper.IterateKeys(dictObj);
if (keys == null) return;

foreach (var key in keys) {
	var value = ReflectionHelper.DictGet(dictObj, key);
	int count = ReflectionHelper.DictGetInt(dictObj, key);
	// Process key/value
}
```

---

## Conventions

- **Formatting**: All source files use tabs, K&R braces (opening brace on same line), and no space before `:` in inheritance (e.g., `class Foo: IBar`). See `.editorconfig`. Always use tab characters in Edit tool `old_string`/`new_string` — the Read tool displays them as spaces but the file contains real tabs. If an Edit fails due to whitespace mismatch, run `dotnet format` and retry rather than using shell commands to inspect whitespace.
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

### Harmony Patching

`Plugin.Awake` applies each patch class individually via `ApplyPatchClass` (not `PatchAll`) so one failed patch — e.g. a game update renaming a type resolved by string in a `TargetMethod` — cannot abort the remaining patches or prevent `AccessibilityCore` (speech, keyboard) from starting. Register new patch classes there, each in its own guarded call. Manual patches (EventAnnouncer) get their own try/catch too.

### Prism (speech library)

- Vendored `prism.dll` is **v0.18.2** (`prism/native/win-x64/`). On a version bump: replace the dll from the release's `dynamic/release/bin/`, refresh `prism/LICENSES` + `prism/NOTICE` from the release zip, and diff the release's `include/prism.h` against the P/Invoke declarations in `Utils/Speech.cs`.
- `prism_init` is passed NULL (no `PrismConfig` struct is declared in C#) — the struct's layout changes between Prism versions and a mismatched declaration silently corrupts memory. Keep it that way unless config is genuinely needed, and then pin the exact struct for the vendored version.
- All strings crossing the Prism boundary are **UTF-8**, never ANSI: text goes out through `ToUtf8()` (ANSI input is rejected as `PRISM_ERROR_INVALID_UTF8`, which silently drops all non-English speech), and returned `const char*` strings come back through `FromUtf8()`.

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

Methods that invoke reflected game methods should return `false` if the method wasn't found. `ReflectionHelper.InvokeVoid` handles this automatically — it returns `false` when the method is null and `true` on success. For custom wrappers, follow the same pattern:

```csharp
// Correct — use ReflectionHelper
return ReflectionHelper.InvokeVoid(_someMethod, instance, arg);

// Correct — manual version
if (_someMethod == null) return false;
_someMethod.Invoke(...);
return true;

// Wrong - returns true even if nothing happened
_someMethod?.Invoke(...);
return true;
```
