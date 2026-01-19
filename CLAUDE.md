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
- **GameReflection.cs** - Centralized reflection access to settlement game internals (services, controllers, map data, dynamic map bounds)
- **WorldMapReflection.cs** - Reflection access to world map internals (hex grid, nodes, biomes)
- **EmbarkReflection.cs** - Reflection access to embark/expedition setup screen
- **Speech.cs** - Tolk wrapper for screen reader output with SAPI fallback
- **KeyboardManager.cs** - Input handling, routes keys to appropriate navigator based on context

### Settlement Navigation

- **MapNavigator.cs** - Settlement map grid navigation (dynamic map size, cursor starts at Ancient Hearth). Announces building state (under construction, ruin) when navigating.
- **MapScanner.cs** - Hierarchical object scanner (PageUp/Down for groups, Ctrl for categories, Alt for items)
- **TileInfoReader.cs** - Reads detailed tile info (I key) for buildings, resources, deposits
- **StatsReader.cs** - Reads game statistics (Reputation, Impatience, Hostility, Resolve)
- **StatsPanel.cs** - Virtual speech-only panel for detailed stats navigation
- **MysteriesPanel.cs** - Virtual speech panel for forest mysteries and world modifiers
- **SettlementResourcePanel.cs** - Virtual speech panel for browsing settlement resources by category
- **InfoPanelMenu.cs** - Unified F1 menu for accessing information panels (Stats, Resources, Mysteries)
- **MenuHub.cs** - Quick access menu (F2) for game popups (Recipes, Orders, Trade Routes, etc.)
- **BuildingMenuPanel.cs** - Virtual speech panel for building selection (Tab key, two-panel: categories → buildings)
- **BuildModeController.cs** - Handles building placement, rotation, and removal in build mode
- **MoveModeController.cs** - Handles relocating existing buildings (M key)

### World Map Navigation

- **WorldMapNavigator.cs** - World map hex grid navigation with arrow keys and camera following
- **WorldMapScanner.cs** - Quick navigation to world map features by type (PageUp/Down)
- **WorldMapEffectsPanel.cs** - Navigation for world map modifier effects panel
- **WorldMapStatsReader.cs** - Meta-level stats (player level, meta resources, seals, cycle info)

### UI Navigation

- **UINavigator.cs** - Popup/menu navigation with panel and element cycling
- **EncyclopediaNavigator.cs** - In-game wiki/encyclopedia navigation (3-panel: categories, articles, content)
- **EmbarkPanel.cs** - Embark/expedition setup screen navigation
- **TutorialHandler.cs** - Tutorial tooltip and decision popup handling
- **MetaRewardsPopupReader.cs** - Reputation rewards popup with polling navigation

### Support Components

- **UIElementFinder.cs** - Finds navigable UI elements in Unity hierarchy
- **WikiReflection.cs** - Reflection helpers for encyclopedia/wiki system
- **InputBlocker.cs** - Prevents game input during accessibility navigation
- **InputPatches.cs** - Harmony patches for input handling
- **NavigationUtils.cs** - Shared utilities for index wrapping in navigation

### Key Patterns

**Reflection caching**: Cache type metadata (PropertyInfo, MethodInfo, FieldInfo) but never cache service instances (destroyed on scene change). For methods handling multiple object types, use per-call reflection. See `GAME-INTERNALS.md` for details.

**Lazy initialization**: Game services aren't fully loaded when scene becomes active. Defer initialization that requires game data (e.g., hearth position, map size) until first user interaction. MapNavigator uses this pattern - cursor initializes on first arrow key press.

**Native DLL loading**: Tolk.dll and helpers (nvdaControllerClient64.dll, SAAPI64.dll) must stay in plugins folder. SetDllDirectory is called in Plugin.Awake() before any P/Invoke.

**Navigation priority**: KeyboardManager checks contexts in order: InfoPanelMenu (manages Stats/Resources/Mysteries panels) → MenuHub → BuildingMenuPanel → BuildModeController → MoveModeController → Encyclopedia → Popup → EmbarkPanel → Tutorial → Context-based (Map/WorldMap). Encyclopedia takes priority over generic popup handling.

## Keyboard Controls

### Settlement Map Navigation
- **Arrow keys** - Move cursor on map grid (starts at Ancient Hearth)
- **Ctrl+Arrow** - Skip to next different tile type
- **K** - Announce current cursor coordinates
- **I** - Read detailed info about object under cursor (description, charges, products)
- **M** - Enter move mode for building under cursor (relocate existing buildings)

### Settlement Map Scanner
- **PageUp/Down** - Cycle groups (e.g., "Clay Deposit" → "Copper Deposit")
- **Ctrl+PageUp/Down** - Cycle categories (Glades → Resources → Buildings)
- **Alt+PageUp/Down** - Cycle items within group
- **Home** - Move cursor to current item
- **End** - Announce distance/direction to current item

### Game Stats (Settlement)
- **S** - Announce quick summary (Reputation, Impatience, Hostility)
- **V** - Announce resolve for next species (cycles through present species)
- **T** - Announce current season, time remaining, and settlement year
- **F1** - Open information panels menu
- **F2** - Open menu hub (quick access to game popups)
- **Space** - Toggle pause
- **1-4** - Set game speed

### Information Panels Menu (F1)
- **F1** - Open menu (announces "Information panels. Stats")
- **Up/Down** - Navigate menu items (Stats, Resources, Mysteries)
- **Enter/Right Arrow** - Open selected panel
- **Left Arrow** - Return from panel to menu
- **Escape** - Close menu and return to map

### Menu Hub (F2)
- **F2** - Open menu hub (quick access to game popups)
- **Up/Down** - Navigate menu items (Recipes, Orders, Trade Routes, Consumption, Villagers, Trends, Trader)
- **Enter** - Open selected popup
- **Escape** - Close menu hub

### Stats Panel (when open)
- **Up/Down** - Navigate categories (left panel) or details (right panel)
- **Enter** - View breakdown details for current category
- **Left Arrow** - Return from details to categories
- **Escape** - Close stats panel

### Mysteries Panel (when open)
- **Up/Down** - Navigate categories or items within current category
- **Enter** - View items in current category
- **Left Arrow** - Return from items to categories
- **Escape** - Close mysteries panel

### Resource Panel (when open)
- **Up/Down** - Navigate categories (left panel) or items (right panel)
- **Enter/Right Arrow** - View items in current category
- **Left Arrow** - Return from items to categories
- **Escape** - Close resource panel

### Building Menu (Tab key)
- **Tab** - Open building menu (from settlement map)
- **Up/Down** - Navigate categories (left panel) or buildings (right panel)
- **Enter/Right Arrow** - View buildings in category, or select building to place
- **Left Arrow** - Return from buildings to categories
- **Escape** - Close building menu

### Build Mode (after selecting a building)
- **Arrow keys** - Move cursor on map (same as normal navigation)
- **R** - Rotate building (announces cardinal direction: North/East/South/West)
- **Space** - Place building at cursor position
- **Shift+Space** - Remove unfinished building at cursor
- **Tab** - Return to building menu
- **Escape/Enter** - Exit build mode

### Move Mode (M key on existing building)
- **Arrow keys** - Move cursor to new location
- **R** - Rotate building
- **Space** - Confirm move to current location
- **Escape** - Cancel move mode

### World Map Navigation
- **Arrow keys** - Navigate hex grid (camera follows cursor)
- **Enter** - Select/interact with current tile
- **I** - Read full tooltip content for current tile
- **D** - Read embark status and distance to capital
- **M** - Open effects panel (modifiers for selected tile)
- **L** - Announce player level and XP to next level
- **R** - Announce meta resources (Food, Machinery, Artifacts, etc.)
- **S** - Announce highest reforged seal info
- **T** - Announce storm cycle info (year, games won/played, seal fragments)

### World Map Scanner
- **PageUp/Down** - Cycle feature types (settlements, resources, etc.)
- **Alt+PageUp/Down** - Cycle items within current type
- **Home** - Jump cursor to current item
- **End** - Announce direction to current item

### Embark Panel (pre-expedition setup)
- **Up/Down** - Navigate within current section
- **Left/Right** - Switch between sections (menu, resources, effects, embark button)
- **Enter** - Activate selected item

### UI/Popup Navigation
- **Tab/Shift+Tab** - Cycle panels
- **Up/Down** - Cycle elements within panel
- **Shift+Up/Down** - Adjust slider value by 10%
- **Left/Right** - Navigate between panels (encyclopedia)
- **Enter** - Activate element (or enter text field edit mode)
- **Escape** - Close popup (or cancel text field editing)

### Text Field Editing (in popups)
- **Enter** on text field - Enter edit mode (announces current text)
- While editing: Type normally (keys pass through to Unity)
- **Enter** - Submit text and exit edit mode
- **Escape** - Cancel editing and exit edit mode

### Encyclopedia
- **Left/Right** - Switch panels (Categories → Articles → Content)
- **Up/Down** - Navigate within current panel
- **Enter** - Select category/article or re-read content line

## Game Internals Reference

See `GAME-INTERNALS.md` for detailed game API documentation including:
- Controller hierarchy and service containers
- Settlement map system (dynamic size via MapService, GladesService, ResourcesService, BuildingsService)
- World map system (hex grid, WorldMapService, nodes and biomes)
- UI hierarchy and popup structure
- Key class names and reflection patterns

**Update that file as new game internals are discovered.**
