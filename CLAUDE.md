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

## Critical Patterns

- **Reflection**: Cache metadata (PropertyInfo, MethodInfo, FieldInfo), never cache service instances
- **Dictionaries**: Use reflection iteration (get Keys, iterate, use indexer) - direct cast fails at runtime
- **Handlers**: `IsActive` must be side-effect free; cleanup in `ProcessKey()`
- **Workers**: Use `IsValidWorkerIndex(int)` before accessing `_workerIds[index]`
- **Lazy init**: Game services aren't ready at scene load - defer to first interaction
- **Events**: Use `IsInGracePeriod()` to avoid announcing pre-existing state; track in HashSet to prevent duplicates
- **Regex**: Use compiled static fields for frequent string operations
