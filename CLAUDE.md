# CLAUDE.md

BepInEx accessibility mod for "Against the Storm" providing screen reader support via Tolk library. Uses Harmony for runtime patching and reflection for game API access.

## Build & Deploy

```bash
dotnet build ATSAccessibility/ATSAccessibility.csproj
cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "/c/Program Files (x86)/Steam/steamapps/common/Against the Storm/BepInEx/plugins/ATSAccessibility/"
```

**Important**: Use `cp` with forward slashes and `/c/` prefix. Do NOT use Windows `copy` command.

## File Locations

| Path | Description |
|------|-------------|
| `ATSAccessibility/` | Mod source code |
| `game-source/` | Decompiled game source (read-only reference) |
| `C:\Users\rasha\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` | **Check first** for debugging - Unity log with `[ATSAccessibility]` output |

## Code Organization

**Reflection files** (where to add new game API access):
- `GameReflection.cs` - Settlement game internals
- `WorldMapReflection.cs` - World map internals
- `EmbarkReflection.cs` - Embark/expedition setup
- `BuildingReflection.cs` - Building panels (all types, recipes, workers)

**Key handlers** in `KeyboardManager.cs` - priority chain where first active handler consumes key. Register in `AccessibilityCore.Start()`.

**Base classes**:
- `TwoLevelPanel` - Virtual panels with category→item navigation (F1 menu panels)
- `BuildingSectionNavigator` - Building panel navigation with sections→items→sub-items

## Critical Patterns

**Reflection caching**: Cache type metadata (PropertyInfo, MethodInfo, FieldInfo) but never cache service instances (destroyed on scene change).

**Reflection dictionary iteration**: Use reflection-based iteration (get Keys, iterate, use indexer) instead of direct cast to `Dictionary<K,V>` which fails at runtime.

**Handler chain**: `IsActive` property must be side-effect free. Move cleanup logic to `ProcessKey()`.

**Worker navigation**: Use `IsValidWorkerIndex(int)` helper to bounds-check before accessing `_workerIds[index]`. Use `BuildingReflection.IsWorkerSlotEmpty()` consistently.

**Lazy initialization**: Game services aren't ready at scene load. Defer until first user interaction.

**Event subscriptions**: Use grace period check (`IsInGracePeriod()`) in event handlers to avoid announcing pre-existing state on game load. Track announced items in HashSet to prevent duplicates.

**Compiled regex**: Use `new Regex(pattern, RegexOptions.Compiled)` as static field for frequently-called string operations.
