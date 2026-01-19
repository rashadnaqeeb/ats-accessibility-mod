# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BepInEx accessibility mod for "Against the Storm" providing screen reader support via Tolk library. Uses Harmony for runtime patching and reflection for game API access.

## Build & Deploy

```bash
dotnet build ATSAccessibility/ATSAccessibility.csproj
cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "/c/Program Files (x86)/Steam/steamapps/common/Against the Storm/BepInEx/plugins/ATSAccessibility/"
```

**Important**: Use `cp` with forward slashes and `/c/` prefix. Do NOT use Windows `copy` command.

## Important File Locations

| Path | Description |
|------|-------------|
| `ATSAccessibility/` | Mod source code |
| `game-source/` | Decompiled game source (read-only reference) |
| `C:\Users\rasha\AppData\LocalLow\Eremite Games\Against the Storm\Player.log` | **Check first** for debugging - Unity log with `[ATSAccessibility]` output |
| `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm\BepInEx\LogOutput.log` | BepInEx log (mod loading only) |

## Architecture

### Core Components

- **Plugin.cs** - BepInEx entry point, sets DLL directory for Tolk
- **AccessibilityCore.cs** - Main MonoBehaviour, survives scene transitions via DontDestroyOnLoad
- **Speech.cs** - Tolk wrapper for screen reader output with SAPI fallback
- **KeyboardManager.cs** - Handler chain coordinator for IKeyHandler implementations
- **GameReflection.cs** - Settlement game internals (services, map data)
- **WorldMapReflection.cs** - World map internals (hex grid, nodes, biomes)
- **EmbarkReflection.cs** - Embark/expedition setup screen
- **WikiReflection.cs** - Encyclopedia/wiki system

### Key Handlers (priority order)

Handlers registered in AccessibilityCore.Start(). First handler where `IsActive` is true AND `ProcessKey()` returns true consumes the key.

- **InfoPanelMenu.cs** - F1 menu (highest priority)
- **MenuHub.cs** - F2 quick access menu
- **BuildingMenuPanel.cs** - Tab building selection
- **BuildModeController.cs** - Building placement (passthrough for arrows)
- **MoveModeController.cs** - Building relocation (passthrough for arrows)
- **EncyclopediaNavigator.cs** - Wiki/encyclopedia popup
- **UINavigator.cs** - Generic popup/menu navigation
- **EmbarkPanel.cs** - Pre-expedition setup
- **TutorialHandler.cs** - Tutorial tooltips (passthrough)
- **SettlementKeyHandler.cs** - Settlement map context (fallback)
- **WorldMapKeyHandler.cs** - World map context (fallback)

### Two-Level Panels (TwoLevelPanel base class)

Virtual speech-only panels with category→item navigation. Base class provides shared key handling (Up/Down/Enter/Right/Left/Escape) and navigation logic.

| Panel | Purpose | Key |
|-------|---------|-----|
| StatsPanel | Game stats (Reputation, Resolve, etc.) | F3 |
| MysteriesPanel | Forest mysteries and modifiers | F4 |
| SettlementResourcePanel | Resources by category | F5 |

To create a new two-level panel, inherit from `TwoLevelPanel` and implement:
- `PanelName`, `EmptyMessage` - strings for announcements
- `CategoryCount`, `CurrentItemCount` - navigation bounds
- `RefreshData()`, `ClearData()` - data lifecycle
- `AnnounceCategory()`, `AnnounceItem()` - speech output

### Other Navigation Components

- **MapNavigator.cs** - Settlement grid navigation (cursor starts at Ancient Hearth)
- **MapScanner.cs** - Object scanner (PageUp/Down for groups, Ctrl for categories)
- **WorldMapNavigator.cs** - World map hex grid navigation
- **WorldMapScanner.cs** - World map feature navigation
- **WorldMapEffectsPanel.cs** - Single-level panel (not TwoLevelPanel)

### Support Components

- **UIElementFinder.cs** - Finds navigable UI elements
- **InputBlocker.cs** - Prevents game input during navigation
- **InputPatches.cs** - Harmony patches for input
- **NavigationUtils.cs** - Index wrapping utilities

## Key Patterns

**Reflection caching**: Cache type metadata (PropertyInfo, MethodInfo, FieldInfo) but never cache service instances (destroyed on scene change).

**Reflection helpers** in GameReflection.cs: `PublicInstance`, `NonPublicInstance`, `PublicStatic`, `GetLocaText(object)`.

**Lazy initialization**: Game services aren't ready at scene load. Defer initialization until first user interaction (e.g., MapNavigator initializes cursor on first arrow key).

**Handler chain**: Implement `IKeyHandler` with `IsActive` property and `ProcessKey(KeyCode, KeyModifiers)`. Register in AccessibilityCore.Start() at appropriate priority. Return true to consume key, false to pass through.

**Reflection files**: Add settlement code to GameReflection.cs, world map to WorldMapReflection.cs, embark to EmbarkReflection.cs, wiki to WikiReflection.cs. Create new file only for new major game screen with 500+ lines.

## Game Internals Reference

See `GAME-INTERNALS.md` for detailed game API documentation. Update that file as new internals are discovered.
