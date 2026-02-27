# ATS Accessibility Mod

A BepInEx mod adding screen reader support to Against the Storm via Tolk.

**The game and its DLCs are fully playable with this mod.** All core gameplay mechanics are accessible.

If this is your first time playing, there is another file included with this mod named tutorial.HTML. It will guide you in detail through the first tutorial mission. I highly recommend you read it as it will explain what keyboard commands you need as you need them. The remainder of this Read me is meant as a quick reference list in case you forget a hotkey. You can also press f12 at any time from within the game to get a list of hotkeys you can use at any given moment.

## Installation

1. Locate your Against the Storm installation folder:
   - Steam: Right-click the game > Manage > Browse local files
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm`

2. Extract the contents of the release zip directly into the game folder.
3. Launch the game. That's it!

### Requirements

- Against the Storm (Any version, including demos).
- windows.

### Uninstallation

Delete the BepInEx folder, winhttp.dll, and doorstop_config.ini from your game folder.

---

## Navigation Model

All menus use arrow key navigation. Up/Down moves through items, Right/Enter drills into submenus, Left/Escape backs out. Most lists support type-ahead search: start typing a name to jump to the first match, arrow to scroll through results. You can type single letters back to back for first letter navigation. Backspace removes the last character, and the buffer auto-clears on arrow key navigation.
There are only two exceptions to this: the options menu and the encyclopedia. Both require you to press Enter on section headers to switch to them. This is a limitation of the game's UI.

- Space performs the contextual action (toggle recipe, check checkbox, etc.)
- +/- adjusts values (limits, levels, sliders). Shift+/- for larger increments.

---

## Settlement Map

### Basics

- Arrows: Move cursor one tile
- Ctrl+Arrows: Skip to next different tile in direction, announcing tile count
- K: Announce cursor coordinates, with the main hearth as 0, 0.
- Alt+K: Toggle coordinate reading.
- I: Get more info about object at tile.
- Space: Pause/Resume
- 1-4: Set speed

### Quick Stats

- S: Settlement summary (reputation, resources, resolve)
- V: Species resolve breakdown, press multiple times to cycle.
- T: Time summary (year, season, time remaining)
- O: Announce tracked order objectives
Alt+S, Alt+V, and Alt+O also work from inside building panels and game menus, to accomplish the same result without interfering with menu search.

### Building Interaction

- Enter: Open building panel. On a lake, retrieves stored fish with confirmation.
- M: Pick up and move building at cursor.
- R/Shift+R: Rotate building clockwise/counterclockwise
- Shift+Space: Destroy building or remove resource node at cursor (with confirmation)
- Alt+Space: Toggle pause from inside building panels and menus
- E: Announce focused building entrance location. Where workers will enter and exit from.
- W: Announce status of worker slots at focused building.

Quickly change worker slots without opening building settings:
- +/-: Cycle between worker races.
- Shift+/-: Add/remove worker at building

Cycle between buildings with worker slots:
- Comma/Period: Jump to previous/next worker building
- Shift+Comma/Shift+Period: Change worker category filter (All, Gathering, Production, Service, Events)

#### Marking Trees

To mark trees for harvesting:

1. Move your cursor to any tree and press Enter to enter tree marking mode.
2. **Rectangle mode (default)**: Press Space on one tree to set the first corner, move to another tree and press Space to set the opposite corner. This selects all trees in the rectangle. Press Enter to confirm the selection.
3. **Single select mode**: Press Tab while in tree marking mode to switch to single select. Move to individual trees and press Space to toggle each one. Press Enter when finished.

To unmark trees, press Enter on an already-marked tree. This enters unmarking mode, which works the same way—use rectangle or single select to choose which trees to unmark.

You can also press Backspace on any tree to quickly toggle its mark without entering marking mode.

#### Priority Adjustment on Map

For buildings under construction, resource deposits, and lakes, you can adjust priority directly from the map cursor without opening any panel:

- +/-: Adjust priority (-5 to +5)
- Shift+/-: Adjust global priority (applies to all of that type)
- I: Announce current priority

### Bookmarks
The mod gives you access to 11 bookmarks. A quick-use one, and 10 others controlled through the number row.

- Shift+B: Set quick bookmark at cursor position
- B: Jump to bookmark
- Alt+B: Announce direction and distance to bookmark

Numbered bookmarks (slots 0-9):
- Ctrl+0-9: Set numbered bookmark at cursor position
- Shift+0-9: Jump to numbered bookmark
- Alt+0-9: Announce direction to numbered bookmark

### Helpers

- D: Building range guide. Press with cursor on a building or while in build/move mode: contextually announces what current building connects to. For resource gatherers, shows you what resources will be in range. For producers, shows you the nearest warehouse and other suppliers. You can also press it on a resource patch for the reverse effect.
- Alt+D: Blight helper: directs you to the nearest Blight Cyst. If on a building with Blight Cysts, tells you how many. Also reports total cyst count and corruption percentage.
- Alt+H: Reset cursor to hearth
- Shift+N: Jump to latest event location
- P: Rainpunk helper: directs you to the nearest Rainpunk engine that's running. If on a building with running engines, Shift+P quickly turns them off without needing to open the panel.

### Settlement Scanner

The settlement scanner finds things on the map organized into a three-level hierarchy: categories, subcategories/groups, and individual items. Items are sorted by distance from cursor.

- Ctrl+PageUp/Down: Change category (Glades, Resources, Buildings)
- Shift+PageUp/Down: Change subcategory
- PageUp/Down: Change group within subcategory
- Alt+PageUp/Down: Cycle through individual items in group
- Home: Move cursor to current item
- End: Announce distance and direction to current item
- Alt+I: Read detailed info about current scanner item
- Alt+Home: Toggle auto-move cursor mode

Glades category - Groups by danger level (Small, Dangerous, Forbidden). If glade info modifiers are active, contents are shown. Only unrevealed glades are listed. Also includes revealed tiles inside glades (for example the nearest patch of fertile soil revealed by the unlockable human starting ability). On a sealed forest map, it will also show you candidate glades for the location of the seal as you uncover guidance stones.

Resources category - Five subcategories:
- All: All resource types combined
- Natural Resources: trees, plants, fertile soil
- Extracted Resources: e.g., copper, iron, coal, geysers
- Nodes Small: e.g., clay, stone, bird nests, fish ponds.
- Nodes Large: same as above, but bigger.

Buildings category - Eleven subcategories: All, Essential, Gathering, Production, Trade, Housing and Services, Special Buildings, Blight Fighting, Decorations, Ruins, Roads. The "All" subcategory combines everything except Decorations and Roads.

Scanner search - Press Ctrl+F, type what you're looking for, and press Enter. This searches across all categories and creates a temporary Search Results category. Navigate results with the same keys as the regular scanner (PageUp/Down for groups, Alt+PageUp/Down for instances). Results are cleared if you switch categories with Ctrl+PageUp/Down or start a new search with Ctrl+F.

### Menus

- F1: Info panels (Resources, Villagers, Workers, Stats, Modifiers, Announcements)
- F2: Menu hub (Recipes, Orders, Trade Routes, Payments, Consumption, Trends, Trader)
- F3: Pending rewards
- Tab: Building menu (construction). Buildings organised into categories or can type building name directly. In build mode, press Space to place building, Shift+Space to remove. Enter places and exits, Escape exits without placing.
- Alt+N: Announcement history. Enter on an entry jumps to the event location on the map. Shift+N jumps to location of most recent event.

Direct panel shortcuts:
- Shift+S: Stats
- Shift+V: Villagers
- Shift+W: Workers
- Shift+M: Modifiers
- Shift+O: Orders

In the Resources panel, Alt+I reads the description of the focused resource.

### Trends

Opened via Menu Hub (F2 > Trends). Shows storage operations (gains and losses) for each good.

- Left/Right: Navigate goods
- Up/Down: Navigate operations (gains listed first, then losses)
- 1: Last 10 seconds
- 2: Last minute
- 3: Last 5 minutes

---

## World Map

- Arrows: Move hex cursor
- I: Read hex tooltip
- F1: Open tutorial hub
- D: Embark status and distance from embark point
- M: Descriptions of modifiers at tile.
- L: Current level
- R: Meta resources
- S: Seal info
- T: Cycle info
- E: End cycle.
Alt+L, Alt+R, Alt+S, and Alt+T also work from inside world map popups and menus.
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

- T: Toggle tracking on the current order. Tracked orders can be checked from the map with O.

### Order Pick Overlay

When picking which order to fulfill, this overlay shows the available options with their objectives and rewards.

- S: Announce how much of each required good you currently have in storage.

### Recipes Overlay (via F2 > Recipes)

Used to look up recipes and set global production limits.

- Ctrl+T: Toggle showing all recipes vs. only recipes you've unlocked.

### Recipe Priority

In building recipe settings, navigate to a recipe and use +/- to adjust its priority (0-3). Higher priority recipes are processed first.

When a recipe has multiple ingredient options, drill into an individual ingredient and use +/- to adjust its priority (0-3) as well. This also works for fuel types in the hearth's Fire section.

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

### Hearth

Open a hearth building (Ancient Hearth or Small Hearth) with Enter. Sections:

- **Fire**: Fuel level, time remaining, and fuel types. Drill into Fuel Types to toggle individual fuels with Space and adjust fuel priority (0-3) with +/-.
- **Sacrifice**: Lists goods you can sacrifice for bonuses. Each sacrifice has a level that determines consumption rate and effect strength. +/- to adjust level. Level 0 is off.
- **Services** (main hearth only, requires meta unlock): The Commons. Lists needs the hearth can fulfill. Press Enter to unlock when available.
- **Upgrades**: Shows hearth upgrade tiers and their requirements (housed population, institutions, decorations). Upgrades unlock sequentially as you progress — you start with only the first tier available.
- **Blight** (main hearth only, when blight is active): Shows corruption percentage.

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

---

## Training Expeditions

Access from the main menu. A hierarchical menu for configuring custom game settings.

- Up/Down: Navigate options
- Enter/Right: Enter submenu or activate option
- Left/Escape: Exit submenu
- Space: Toggle options (blight on/off, seasonal effects random/manual) or randomize seed
- +/-: Adjust sliders and embark good quantities. Shift for larger increments.
- Tab: Cycle modifier categories (when in Modifiers section)
- Type-ahead search works in Modifiers section

### Sections

- Difficulty: Select preset difficulty
- Seed: Space randomizes, Enter allows manual text entry
- Biome: Select map biome
- Races: Toggle which species are available
- Reputation/Seasons/Blight: Adjust sliders for game parameters
- Seasonal Effects: Random mode shows counts; manual mode lists individual effects to toggle
- Modifiers: Browse by category (World Map, Daily, Difficulty, All) with search
- Trade Towns: Toggle available trading partners
- Embark Goods: Adjust starting resource quantities
- Embark Effects: Toggle starting perks
- Embark: Start the game
# starting cutscene.
Below you will find a transcript of the starting cut scene, if you are a non-English player wishing to translate it. I am unable to get the localised subtitles to read.
The world is plagued by the Blightstorm, a vile cycle of destruction ravaging everything in its path. The only safe haven is the smoldering city, where the mysterious Scorched queen reins. You are one of her viceroys, a pioneer sent into the wilds, tasked with scouting new settlements and acquiring resources for the crown. Your goal is to help rebuild the smoldering city and secure the future for the queens subjects.