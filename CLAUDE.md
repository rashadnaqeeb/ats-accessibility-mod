# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BepInEx accessibility mod for "Against the Storm" providing screen reader support via Tolk library. Uses Harmony for runtime patching and reflection for game API access.

## Build & Deploy

```bash
# Build the mod
dotnet build ATSAccessibility/ATSAccessibility.csproj

# Copy to game plugins folder (use cp with Unix-style paths, NOT Windows copy command)
cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "/c/Program Files (x86)/Steam/steamapps/common/Against the Storm/BepInEx/plugins/ATSAccessibility/"
```

**Important**: Use `cp` with forward slashes and `/c/` prefix for drive letter. Do NOT use Windows `copy` command or `cmd /c copy` - they don't produce visible output and are unreliable in this environment.

## Important File Locations

| Path | Description |
|------|-------------|
| `ATSAccessibility/` | Mod source code |
| `game-source/` | Decompiled game source (read-only reference) |
| `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm\` | Game install directory |
| `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm\BepInEx\plugins\ATSAccessibility\` | Plugin deployment folder |

### Log Files

**When debugging issues, check Player.log first** - it contains Unity engine errors, exceptions, and all mod Debug.Log output (prefixed with `[ATSAccessibility]`). BepInEx log only shows mod loading and Harmony patch info.

| Path | Description |
|------|-------------|
| `C:\Users\rasha\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` | **Check first** - Unity player log with all `[ATSAccessibility]` debug output, engine errors, crashes |
| `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm\BepInEx\LogOutput.log` | BepInEx log (mod loading, Harmony patches only) |

## Architecture

### Core Components

- **Plugin.cs** - BepInEx entry point. Sets DLL directory for Tolk, creates persistent AccessibilityCore GameObject
- **AccessibilityCore.cs** - Main MonoBehaviour coordinating all features, survives scene transitions via DontDestroyOnLoad
- **GameReflection.cs** - Centralized reflection access to game internals (services, controllers, map data)
- **Speech.cs** - Tolk wrapper for screen reader output with SAPI fallback
- **KeyboardManager.cs** - Input handling, routes keys to appropriate navigator based on context

### Navigation Components

- **UINavigator.cs** - Popup/menu navigation with panel and element cycling
- **MapNavigator.cs** - Settlement map grid navigation (70x70 tiles, arrow keys)
- **MapScanner.cs** - Hierarchical object scanner (PageUp/Down for groups, Ctrl for categories, Alt for items)
- **EncyclopediaNavigator.cs** - In-game wiki/encyclopedia navigation
- **TutorialHandler.cs** - Tutorial tooltip and decision popup handling

### Support Components

- **UIElementFinder.cs** - Finds navigable UI elements in Unity hierarchy
- **WikiReflection.cs** - Reflection helpers for encyclopedia/wiki system
- **TileInfoReader.cs** - Reads detailed tile info (I key) for buildings, resources, deposits with per-type reflection caching
- **InputBlocker.cs** - Prevents game input during accessibility navigation
- **InputPatches.cs** - Harmony patches for input handling

### Key Patterns

**Reflection caching**: Cache type metadata (PropertyInfo, MethodInfo, FieldInfo) but never cache service instances (destroyed on scene change). For methods handling multiple object types, use per-call reflection. See `GAME-INTERNALS.md` for details.

**Native DLL loading**: Tolk.dll and helpers (nvdaControllerClient64.dll, SAAPI64.dll) must stay in plugins folder. SetDllDirectory is called in Plugin.Awake() before any P/Invoke.

## Keyboard Controls

### Map Navigation
- **Arrow keys** - Move cursor on map grid
- **Ctrl+Arrow** - Skip to next different tile type
- **Space** - Interact with tile under cursor
- **I** - Read detailed info about object under cursor (description, charges, products)

### Map Scanner
- **PageUp/Down** - Cycle groups (e.g., "Clay Deposit" → "Copper Deposit")
- **Ctrl+PageUp/Down** - Cycle categories (Glades → Resources → Buildings)
- **Alt+PageUp/Down** - Cycle items within group
- **Home** - Announce distance/direction to current item
- **End** - Move cursor to current item

### UI Navigation
- **Tab/Shift+Tab** - Cycle panels
- **Up/Down** - Cycle elements within panel
- **Enter** - Activate element
- **Escape** - Close popup

## Game Internals Reference

See `GAME-INTERNALS.md` for detailed game API documentation including:
- Controller hierarchy and service containers
- Map system (70x70 grid, GladesService, ResourcesService, etc.)
- UI hierarchy and popup structure
- Key class names and reflection patterns

**Update that file as new game internals are discovered.**
