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
- **KeyboardManager.cs** - Handler chain coordinator, iterates through registered IKeyHandler implementations
- **IKeyHandler.cs** - Interface for keyboard input handlers (IsActive, ProcessKey)

### Key Handlers (processed in priority order)

- **InfoPanelMenu.cs** - F1 menu for information panels (highest priority)
- **MenuHub.cs** - F2 quick access menu
- **BuildingMenuPanel.cs** - Tab building selection menu
- **BuildModeController.cs** - Building placement mode (selective passthrough for arrows)
- **MoveModeController.cs** - Building relocation mode (selective passthrough for arrows)
- **EncyclopediaNavigator.cs** - Wiki/encyclopedia popup
- **UINavigator.cs** - Generic popup/menu navigation
- **EmbarkPanel.cs** - Pre-expedition setup screen
- **TutorialHandler.cs** - Tutorial tooltips (passthrough, doesn't consume keys)
- **SettlementKeyHandler.cs** - Settlement map context (fallback)
- **WorldMapKeyHandler.cs** - World map context (fallback)

### Settlement Navigation

- **MapNavigator.cs** - Settlement map grid navigation (dynamic map size, cursor starts at Ancient Hearth)
- **MapScanner.cs** - Hierarchical object scanner (PageUp/Down for groups, Ctrl for categories, Alt for items)
- **TileInfoReader.cs** - Reads detailed tile info (I key) for buildings, resources, deposits
- **StatsReader.cs** - Reads game statistics (Reputation, Impatience, Hostility, Resolve)
- **StatsPanel.cs** - Virtual speech-only panel for detailed stats navigation
- **MysteriesPanel.cs** - Virtual speech panel for forest mysteries and world modifiers
- **SettlementResourcePanel.cs** - Virtual speech panel for browsing settlement resources by category

### World Map Navigation

- **WorldMapNavigator.cs** - World map hex grid navigation with arrow keys and camera following
- **WorldMapScanner.cs** - Quick navigation to world map features by type (PageUp/Down)
- **WorldMapEffectsPanel.cs** - Navigation for world map modifier effects panel
- **WorldMapStatsReader.cs** - Meta-level stats (player level, meta resources, seals, cycle info)

### UI Support

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

**Handler chain pattern**: All keyboard input flows through a chain of IKeyHandler implementations. KeyboardManager iterates through handlers in registration order; the first handler where `IsActive` is true AND `ProcessKey()` returns true consumes the key. This makes priority explicit and easy to modify.

- Handlers are registered in AccessibilityCore.Start() in priority order
- Higher priority handlers (menus, popups) are registered first
- Context handlers (SettlementKeyHandler, WorldMapKeyHandler) are registered last as fallbacks
- Handlers can do "selective passthrough" by returning false for keys they don't handle (e.g., BuildModeController returns false for arrow keys so they fall through to SettlementKeyHandler)

**Adding a new handler**:
1. Create a class implementing `IKeyHandler` with `IsActive` property and `ProcessKey(KeyCode, KeyModifiers)` method
2. Register it in AccessibilityCore.Start() at the appropriate priority position
3. `IsActive` should return true when the handler should receive input (e.g., when a menu is open)
4. `ProcessKey` should return true if the key was handled, false to pass to the next handler

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
