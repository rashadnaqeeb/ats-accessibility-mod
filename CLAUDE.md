# CLAUDE.md

BepInEx accessibility mod for "Against the Storm" - screen reader support via Tolk. Uses Harmony patching and reflection.

## Build & Deploy

```bash
dotnet build ATSAccessibility/ATSAccessibility.csproj
cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "/c/Program Files (x86)/Steam/steamapps/common/Against the Storm/BepInEx/plugins/ATSAccessibility/"
```

Use `cp` with forward slashes and `/c/` prefix. Do NOT use Windows `copy`.

## Key Locations

- **Source**: `ATSAccessibility/`
- **Game reference**: `game-source/` (read-only decompiled)
- **Debug log**: `C:\Users\rasha\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` - check first for `[ATSAccessibility]` output

## Code Organization

**Reflection** (game API access): `GameReflection.cs`, `WorldMapReflection.cs`, `EmbarkReflection.cs`, `BuildingReflection.cs`

**Key handlers**: `KeyboardManager.cs` - priority chain, first active handler wins. Register in `AccessibilityCore.Start()`.

**Base classes**: `TwoLevelPanel` (F1 menu panels), `BuildingSectionNavigator` (building panels)

---

## Design Patterns

### 1. Key Handler Pattern (IKeyHandler)

Priority chain where first active handler consumes the key.

```csharp
public class MyHandler : IKeyHandler
{
    public bool IsActive => /* side-effect free check */;

    public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
    {
        switch (keyCode)
        {
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

### 2. TwoLevelPanel Pattern

For category→item navigation (F1 menu panels like Stats, Villagers, Resources).

**Override these:**
```csharp
protected abstract string PanelName { get; }
protected abstract string EmptyMessage { get; }
protected abstract int CategoryCount { get; }
protected abstract int CurrentItemCount { get; }
protected abstract void RefreshData();
protected abstract void ClearData();
protected abstract void AnnounceCategory();
protected abstract void AnnounceItem();
protected virtual string GetCurrentItemName(int index) => null;  // For type-ahead search
```

**Lifecycle**: `Open()` → `RefreshData()` → navigation → `Close()` → `ClearData()`

### 3. BuildingSectionNavigator Pattern

For multi-level building panels (sections→items→sub-items→sub-sub-items).

**Dynamic sections pattern:**
```csharp
private enum SectionType { Info, Workers, Recipes, Storage }
private string[] _sectionNames;
private SectionType[] _sectionTypes;

private void RefreshSections()
{
    var sections = new List<(string name, SectionType type)>();
    sections.Add(("Info", SectionType.Info));
    if (_maxWorkers > 0) sections.Add(("Workers", SectionType.Workers));
    if (_recipes.Count > 0) sections.Add(("Recipes", SectionType.Recipes));
    _sectionNames = sections.Select(s => s.name).ToArray();
    _sectionTypes = sections.Select(s => s.type).ToArray();
}

protected override string[] GetSections() => _sectionNames;

protected override int GetItemCount(int sectionIndex)
{
    switch (_sectionTypes[sectionIndex])
    {
        case SectionType.Workers: return _maxWorkers;
        case SectionType.Recipes: return _recipes.Count;
        default: return 0;
    }
}
```

### 4. Type-Ahead Search Pattern

Integrated via `TypeAheadSearch` helper class.

```csharp
protected readonly TypeAheadSearch _search = new TypeAheadSearch();

public bool ProcessKeyEvent(KeyCode keyCode)
{
    _search.ClearOnNavigationKey(keyCode);  // Auto-clear on arrow keys

    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
    {
        char c = (char)('a' + (keyCode - KeyCode.A));
        _search.AddChar(c);
        int match = FindMatchingItem();
        if (match >= 0) { _currentIndex = match; AnnounceItem(); }
        else { Speech.Say($"No match for {_search.Buffer}"); }
        return true;
    }
    // ...
}

private int FindMatchingItem()
{
    if (!_search.HasBuffer) return -1;
    string lowerBuffer = _search.Buffer.ToLowerInvariant();  // Cache once
    for (int i = 0; i < Count; i++)
    {
        string name = GetItemName(i);
        if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerBuffer))
            return i;
    }
    return -1;
}
```

### 5. Event Subscription Pattern

Grace period + deduplication for game events.

```csharp
private float _subscriptionTime;
private const float GRACE_PERIOD = 2f;
private HashSet<string> _announced = new HashSet<string>();
private Queue<string> _announcedOrder = new Queue<string>();

private bool IsInGracePeriod() => Time.realtimeSinceStartup - _subscriptionTime < GRACE_PERIOD;

private void OnEvent(object data)
{
    if (IsInGracePeriod()) return;  // Skip initialization noise

    string key = GetUniqueKey(data);
    if (_announced.Contains(key)) return;  // Deduplicate

    _announced.Add(key);
    _announcedOrder.Enqueue(key);

    // Evict oldest to prevent memory growth
    while (_announced.Count > 100)
        _announced.Remove(_announcedOrder.Dequeue());

    Speech.Say(FormatMessage(data));
}

public void Dispose()
{
    foreach (var sub in _subscriptions) sub?.Dispose();
    _subscriptions.Clear();
    _announced.Clear();
}
```

### 6. Reflection Caching Pattern

Cache type metadata, never cache service instances (destroyed on scene change).

```csharp
// SAFE to cache (survives scene changes)
private static PropertyInfo _serviceProp;
private static bool _cached = false;

private void EnsureCached()
{
    if (_cached) return;
    var type = GameReflection.GameAssembly.GetType("Eremite.Services.IGameServices");
    _serviceProp = type?.GetProperty("CalendarService");
    _cached = true;
}

// NEVER cache the result of this - get fresh each time
var service = _serviceProp?.GetValue(gameServices);
```

### 7. Reflection Dictionary Iteration

Direct cast to `Dictionary<K,V>` fails at runtime. Use reflection iteration:

```csharp
var keysProperty = dictObj.GetType().GetProperty("Keys");
var keys = keysProperty?.GetValue(dictObj) as IEnumerable;
var indexer = dictObj.GetType().GetMethod("get_Item");

foreach (var key in keys)
{
    var value = indexer?.Invoke(dictObj, new[] { key });
    // Process key/value
}
```

### 8. Worker Validation Pattern

Always bounds-check before accessing worker arrays:

```csharp
private bool IsValidWorkerIndex(int index) =>
    index >= 0 && index < _maxWorkers && _workerIds != null;

protected void AnnounceWorker(int index)
{
    if (!IsValidWorkerIndex(index)) { Speech.Say("Slot not available"); return; }

    int id = _workerIds[index];
    if (BuildingReflection.IsWorkerSlotEmpty(id))
        Speech.Say($"Slot {index + 1}: Empty");
    else
        Speech.Say($"Slot {index + 1}: {BuildingReflection.GetWorkerName(id)}");
}
```

### 9. Lazy Initialization Pattern

Game services aren't ready at scene load. Defer until first user interaction:

```csharp
private bool _dataRefreshed = false;

private void EnsureDataRefreshed()
{
    if (_dataRefreshed) return;
    _data = FetchFromGame();
    _dataRefreshed = true;
}

protected override void ClearData()
{
    _data?.Clear();
    _dataRefreshed = false;
}
```

---

## Conventions

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
