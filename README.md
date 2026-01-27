# ATS Accessibility Mod

A BepInEx mod adding screen reader support to Against the Storm via Tolk.

## Installation

1. Locate your Against the Storm installation folder:
   - Steam: Right-click the game > Manage > Browse local files
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm`

2. Extract the contents of the release zip directly into the game folder.
3. Launch the game. That's it!

### Requirements

- Against the Storm (Steam version)
- A screen reader (NVDA, JAWS, sapi).

### Uninstallation

Delete the BepInEx folder, winhttp.dll, and doorstop_config.ini from your game folder.

---

## Navigation Model

All menus use arrow key navigation. Up/Down moves through items, Right/Enter drills into submenus, Left/Escape backs out. Most lists support type-ahead search: start typing a name to jump to the first match. Backspace removes the last character, and the buffer auto-clears on arrow key navigation.
There are only two exceptions to this: the options menu and the encyclopedia. Both require you to press Enter on section headers to switch to them. This is a limitation of the game's UI.

- Space performs the contextual action (toggle recipe, assign worker, etc.)
- +/- adjusts values (limits, levels, sliders). Shift+/- for larger increments.

---

## Settlement Map

### Basics

- Arrows: Move cursor one tile
- Ctrl+Arrows: Skip to next different tile in direction
- K: Announce cursor coordinates
- I: Get more info about object at tile.
- Space: Pause/Resume
- 1-4: Set speed

### Quick Stats

- S: Settlement summary (reputation, resources, resolve)
- V: Species resolve breakdown, press multiple times to cycle.
- T: Time summary (year, season, time remaining)
- O: Announce tracked order objectives

### Building Interaction

- Enter: Open building panel
- M: Pick up and move building at cursor.
- R: Rotate building
- Shift+Space: Destroy building (with confirmation)
- E: Announce focused building entrance location. Where workers will enter and exit from. Changed with rotation.
- W: Announce status of worker slots at focused building.

Quickly change worker slots without opening building settings:
- +/-: Cycle between worker races.
- Shift+/-: Add/remove worker at building

### Helpers

- D: Building range guide. Press with cursor on a building or while in build mode: contextually announces what current building connects to. For resource gatherers, shows you what resources will be in range. For producers, shows you the nearest warehouse and other suppliers.
- B: Blight helper: directs you to the nearest Blight Cyst. If on a building with Blight Cysts, tells you how many.
- P: Rainpunk helper: directs you to the nearest Rainpunk engine that's running. If on a building with running engines, Shift+P quickly turns them off without needing to open the panel.

### Settlement Scanner

The settlement scanner finds things on the map organized into a three-level hierarchy: categories, subcategories/groups, and individual items. Items are sorted by distance from cursor.

- Ctrl+PageUp/Down: Change category (Glades, Resources, Buildings)
- Shift+PageUp/Down: Change subcategory
- PageUp/Down: Change group within subcategory
- Alt+PageUp/Down: Cycle through individual items in group
- Home: Move cursor to current item
- End: Announce distance and direction to current item

Glades category - Groups by danger level (Small, Dangerous, Forbidden). If glade info modifiers are active, contents are shown. Only unrevealed glades are listed.

Resources category - Three subcategories:
- Natural Resources: trees, plants, fertile soil
- Extracted Resources, e.g., copper, iron, coal, geysers.
- Collected Resources, e.g., clay, stone, fish ponds

Buildings category - Ten subcategories: Essential, Gathering, Production, Trade, Housing and Services, Special Buildings, Blight Fighting, Decorations, Ruins, Roads.

### Menus

- F1: Info panels (Resources, Villagers, Stats, Modifiers, Announcements)
- F2: Menu hub (Recipes, Orders, Trade Routes, Payments, Consumption, Trends, Trader)
- F3: Pending rewards
- Tab: Building menu (construction). Buildings organised into categories or can type building name directly. In build mode, press Space to place building, Shift+Space to remove. Enter or Escape to exit mode.
- Alt+H: Announcement history

---

## World Map

- Arrows: Move hex cursor
- I: Read hex tooltip
- F1: Open tutorial hub
- D: Embark status and distance to capital
- M: Descriptions of modifiers at tile.
- L: Current level
- R: Meta resources
- S: Seal info
- T: Cycle info
- E: End cycle.
- Enter: Select tile / embark

### World Map Scanner

Same as settlement map, just with no categories.
- PageUp/Down: Change type
- Alt+PageUp/Down: Cycle items within type
- Home: Jump to scanner item
- End: Direction to scanner item

---

## Menus with Special Keys

### Orders Overlay (via F2 > Orders)

Orders are sorted by status: To Pick, Completable, Active, Locked, Completed, Failed. Each announcement includes name, status, objectives, rewards, and time remaining if timed.

- T: Toggle tracking on the current order. Tracked orders can be checked from the map with O.

### Order Pick Overlay

When picking which order to fulfill, this overlay shows the available options with their objectives and rewards.

- S: Announce how much of each required good you currently have in storage.

### Recipes Overlay (via F2 > Recipes)

Used to look up recipes and set global production limits.

- Ctrl+T: Toggle showing all recipes vs. only recipes you've unlocked.

### Trader Overlay (via F2 > Trader)

When no trader is present, announces the time until next arrival. When a trader is visiting, provides a three-option menu: Goods Trade, Perks, and Assault (if unlocked via meta progression).

Goods Trade has two tabs: Sell and Buy. Navigate between tabs with Left/Right. Each tab lists available goods with current stock and prices.

- +/-: Adjust quantity to sell or buy. Shift for 10x increments.
- Alt+B: Announce current trade balance (how much you'll gain or spend).
- Alt+A: Accept and finalize the trade.
Note: trading is unfair. Just because something says it's worth 2.5 Amber does not mean you will get 2.5 Amber for it.
The Perks section lists purchasable perks from the trader, which you can only buy with Amber directly. The Assault option lets you attack the trader for their goods if you have the meta upgrade. This is likely to get villagers killed.

### Trade Routes Overlay (via F2 > Trade Routes)

Requires Trade Routes meta unlock. The main menu has four options: Active Routes, Towns, Auto-Collect toggle, and Show Affordable toggle.

Active Routes shows completed trade routes ready for collection. Navigate to a route and press Enter to collect the goods.

Towns lists available trading partners. Each town offers goods at varying prices. Enter a town to see its available offers.

- +/-: Adjust offer multiplier (buy more or fewer goods).
- Enter: Accept the current offer.
- Extend Offers: Pay to get more offer options.

Auto-Collect automatically collects completed routes. Show Affordable filters to only display offers you can currently afford.

### Cornerstone Limit Overlay

When you've hit your perk limit and must remove one to accept a new cornerstone:

- Space: Select/deselect a perk for removal
- Enter: Confirm removal of the selected perk

This is likely to change, I just haven't actually ever seen this menu yet.

---

## Glade Events (Relics)

Glade events are multi-phase interactions found when opening glades. Opening a relic building from the map presents a section-based navigator whose sections change as you progress.

### Phase A: Before Investigation

Sections available:
- Decisions: If multiple options exist, navigate between them with Up/Down and select with Enter. The selected decision determines what goods are required and what effects/rewards apply, and will change the remaining options in the menu. You can pick, check requirements and rewards, then come back to this submenu.
- Requirements: Goods needed for the selected decision. Each requirement is a goods set that may have alternatives. Right arrow shows the alternatives; Enter picks one.
- Effects: Working effects (apply during investigation), active effects (apply now), and permanent effects.
- Rewards: What you receive on completion.
- Start Investigation: Enter begins the investigation.

### Phase B: During Investigation

Sections change to:
- Status: Progress percentage, time remaining. Enter on the cancel option stops the investigation.
- Workers: Assign workers to perform the chosen decision.
- Requirements: Current delivery progress of the goods required.
- Effects: Active effects.

### Phase C: Investigation Complete

- Status: Shows goods left for pickup, if any.
- Workers: Allows you to assign workers to come remove the goods.

Most glade events disappear once all goods have been removed from them.

---

## Strider Port
Similar to glade events, with minor differences.

### Phase 1: Planning

- Level: Adjust expedition level with +/- (affects duration, cost, and reward quality). Shows current and max level.
- Strider/Crew Goods: Each goods set has a picked alternative. Right arrow expands to show all alternatives; Enter selects one.
- Blueprint Category: Select a building category for blueprint rewards (if applicable). You can get a maximum of 8 blueprints per run, one on every other expedition.
- Rewards Preview: Expected reward chances by rarity.
- Confirm: Locks in selections and begins goods collection.

### Phase 2: Delivery

Assign workers to deliver the goods. You can check the progress of delivery under the goods section, and cancel at any time.

### Phase 3: In Progress

Strider departs, worker slots are automatically cleared, menu switches to Read-only status showing progress percentage and time remaining.

### Phase 4: Rewards

- Rewards: Blueprint and perk rewards received.
- Accept: Claims rewards and completes the expedition.

Note that you cannot see the blueprint in the rewards acceptance popup. You must check what it is before accepting the rewards.

---

## Automatic Announcements

The mod announces game events as they occur. These can be individually toggled in F1 > Announcements.
Some announcements are handled by the game's settings. These can be toggled in the game's Options > Alerts menu.
