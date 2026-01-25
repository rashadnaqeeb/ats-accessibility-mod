# ATS Accessibility Mod

A BepInEx mod adding screen reader support to Against the Storm via Tolk.

## Navigation Model

All menus use arrow key navigation. Up/Down moves through items, Right/Enter drills into submenus, Left/Escape backs out. Most lists support type-ahead search: start typing a name to jump to the first match. Backspace removes the last character, and the buffer auto-clears on arrow key navigation.

### Panels (F1)

F1 opens a root menu with five panels: Resources, Villagers, Stats, Modifiers, and Announcements. Each panel uses a two-level structure: categories on the first level, items within a category on the second. Right or Enter enters a category, Left returns to the category list.

### Building Panels

Opening a building (Enter on the map) presents a section-based navigator. Sections are the top level (e.g., Info, Workers, Recipes, Storage, Upgrades). Within each section, items form the second level, and sub-items the third. Some buildings have a fourth level for ingredient alternatives.

- Space performs the contextual action (toggle recipe, assign worker, etc.)
- +/- adjusts values (limits, levels). Shift+/- for larger increments.

### Overlays

When a game popup appears (cornerstone picks, newcomer selection, blueprint choices, etc.), an overlay captures keyboard input. Navigate with Up/Down, confirm with Enter or Space. Escape dismisses the popup.

---

## Settlement Map

### Movement

- **Arrows**: Move cursor one tile
- **Ctrl+Arrows**: Skip to next occupied tile in direction
- **K**: Announce cursor coordinates
- **I**: Read tile info (terrain, resources, building)
- **E**: Announce focused building entrance location
- **D**: Building range guide
- **W**: Worker summary at building
- **B**: Blight info at tile
- **P**: Rainpunk info (Shift+P stops all engines at building)
- **O**: Announce tracked order objectives

### Game Speed

- **Space**: Pause/Resume
- **1-4**: Set speed

### Quick Stats

- **S**: Settlement summary (reputation, resources, resolve)
- **V**: Next species resolve breakdown
- **T**: Time summary (year, season, time remaining)

### Building Interaction

- **Enter**: Open building panel / enter harvest mark mode on resources
- **M**: Enter move mode for building at cursor
- **R**: Rotate building 
- **Shift+Space**: Destroy building (with confirmation)
- **+/-**: Cycle villager race filter
- **Shift+/-**: Add/remove worker at building

### Settlement Scanner

The settlement scanner finds things on the map organized into a three-level hierarchy: categories, subcategories/groups, and individual items. Items are sorted by distance from cursor.

- **Ctrl+PageUp/Down**: Change category (Glades, Resources, Buildings)
- **Shift+PageUp/Down**: Change subcategory
- **PageUp/Down**: Change group within subcategory
- **Alt+PageUp/Down**: Cycle through individual items in group
- **Home**: Move cursor to current item
- **End**: Announce distance and direction to current item

**Glades category** - Groups by danger level (Small, Dangerous, Forbidden). If glade info modifiers are active, contents are shown. Only unrevealed glades are listed.

**Resources category** - Three subcategories:
- Natural Resources: trees, plants, marked trees, fertile soil
- Extracted Resources: copper, iron, coal, geysers.
- Collected Resources: clay, stone, fish ponds

**Buildings category** - Ten subcategories: Essential, Gathering, Production, Trade, Housing and Services, Special Buildings, Blight Fighting, Decorations, Ruins, Roads.

### Menus

- **F1**: Info panels (Resources, Villagers, Stats, Modifiers, Announcements)
- **F2**: Menu hub (Recipes, Orders, Trade Routes, Payments, Consumption, Trends, Trader)
- **F3**: Pending rewards
- **Tab**: Building menu (construction)
- **Alt+H**: Announcement history

---

## World Map

- **Arrows**: Move hex cursor
- **I**: Read hex tooltip
- **D**: Embark status and distance to capital
- **L**: Current level
- **R**: Meta resources
- **S**: Seal info
- **T**: Cycle info
- **M**: World modifiers panel
- **Enter**: Select tile / embark

### World Map Scanner

Two-level hierarchy (type and item within type). Items are sorted by distance from cursor.

- **PageUp/Down**: Change type
- **Alt+PageUp/Down**: Cycle items within type
- **Home**: Jump to scanner item
- **End**: Direction to scanner item

**Types:**
- **Settlement** - Your capital and all player settlements
- **Seal** - Seal locations
- **Revealed Modifier** - World modifiers you can see
- **Unknown Modifier** - Undiscovered modifier locations
- **Revealed Event** - Visible world events
- **Unknown Event** - Undiscovered event locations

---

## Menus with Special Keys

### Orders Overlay (via F2 > Orders)

Orders are sorted by status: To Pick, Completable, Active, Locked, Completed, Failed. Each announcement includes name, status, objectives, rewards, and time remaining if timed.

- **T** - Toggle tracking on the current order. Tracked orders can be checked from the map with O.

### Order Pick Overlay

When picking which order to fulfill, this overlay shows the available options with their objectives and rewards.

- **S** - Announce how much of each required good you currently have in storage.

### Recipes Overlay (via F2 > Recipes)

Two levels: buildings on the first, recipes within a building on the second.

- **Space** - Toggle recipe on/off (at recipe level)
- **+/-** - Adjust production limit (at goods level, Shift for larger increments)
- **Ctrl+T** - Toggle showing all buildings vs. only buildings with active recipes

### Consumption Control (via F2 > Consumption Control)

Three navigation levels:

1. **Categories** - Raw Food, each species need category, and each race. Space toggles the entire category on/off.
2. **Items** - Individual foods or needs within a category. Space toggles the item.
3. **Races** - Per-race permissions for an item. Space toggles permission for that race.

### Cornerstone Limit Overlay

When you've hit your perk limit and must remove one to accept a new cornerstone:

- **Space** - Select/deselect a perk for removal
- **Delete** - Confirm removal of the selected perk
This is likely to change.
---

## Glade Events (Relics)

Glade events are multi-phase interactions found when opening glades. Opening a relic building from the map presents a section-based navigator whose sections change as you progress.

### Phase A: Before Investigation

Sections available:
- **Info** - Building name and description
- **Decisions** - If multiple options exist, navigate between them with Up/Down and select with Enter. The selected decision determines what goods are required and what effects/rewards apply.
- **Requirements** - Goods needed for the selected decision. Each requirement is a goods set that may have alternatives. Right arrow shows the alternatives; Enter picks one.
- **Effects** - Working effects (apply during investigation), active effects (apply now), and permanent effects.
- **Rewards** - What you receive on completion.
- **Status** - Shows "Start Investigation". Enter begins the investigation.

### Phase B: During Investigation

Sections change to:
- **Status** - Progress percentage, time remaining. Enter on the cancel option stops the investigation.
- **Workers** - Assign workers to speed up investigation. Each slot shows its current occupant. Right arrow on a slot shows available races with free worker counts. Enter assigns.
- **Requirements** - Current delivery progress.
- **Effects** - Active effects tracking.

### Phase C: Investigation Complete

Reduced to read-only sections:
- **Info** - Building name
- **Status** - "Investigation complete" with rewards received

### Worker Assignment (shared pattern)

Worker sections in relics, ports, and production buildings all follow the same pattern:

1. Navigate to a worker slot with Up/Down
2. Right arrow or Enter expands the slot to show available races
3. Each race shows how many free workers of that type are available
4. Enter assigns a worker of that race to the slot
5. If the slot is occupied, the first sub-item is "Unassign", followed by race options (assigning replaces the current worker)

---

## Port Building

The port navigator has four phases:

### Phase 1: Planning

- **Level** - Adjust expedition level with +/- (affects duration, cost, and reward quality). Shows current and max level.
- **Strider/Crew Goods** - Each goods set has a picked alternative. Right arrow expands to show all alternatives; Enter selects one. The announcement shows "N other options" when alternatives exist.
- **Category** - Select a destination category for blueprint rewards (if applicable).
- **Rewards Preview** - Expected reward chances by rarity.
- **Confirm** - Locks in selections and begins goods collection.

### Phase 2: Collecting

- **Delivery Progress** - Each required good shows delivered/needed amounts.
- **Cancel** - Aborts the expedition with confirmation.

### Phase 3: In Progress

Read-only status showing progress percentage and time remaining.

### Phase 4: Rewards

- **Rewards** - Blueprint and perk rewards received.
- **Accept** - Claims rewards and completes the expedition.

---

## Automatic Announcements

The mod announces game events as they occur. These can be individually toggled in F1 > Announcements:

- Season and year changes
- Newcomer arrivals
- Order availability and completion
- Reputation changes
- Trader arrivals
- Villager deaths and leaving
- Hostility level changes

---

## Installation

Requires BepInEx 5.x and the Tolk screen reader bridge library.

1. Install BepInEx for Against the Storm
2. Place `ATSAccessibility.dll` in `BepInEx/plugins/ATSAccessibility/`
3. Ensure Tolk.dll and your screen reader's bridge DLL are accessible
