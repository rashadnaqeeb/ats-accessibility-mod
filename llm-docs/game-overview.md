# Against the Storm — Game Information Reference

Research gathered to inform accessibility mod development. Focused on UI structure, game flow, and elements that need screen reader accessibility.

---

## 1. Game Overview

**Title:** Against the Storm
**Developer:** Eremite Games
**Publisher:** Hooded Horse
**Release Date:** December 8, 2023 (PC, Windows)
**Genre:** Roguelite city-builder
**Reception:** Universal acclaim on Metacritic; 95% recommendation rate on OpenCritic; over 1 million copies sold as of May 2024.

**Elevator pitch:** A fantasy roguelite city-builder set in a world of perpetual rain. Players act as a Viceroy dispatched by the Queen from the Smoldering City to build settlements in the wilderness, gather resources, keep villagers happy, and earn reputation before losing to Impatience. Each settlement run is a single "mission" that lasts 30–120 minutes. The meta-game is the World Map, where completing missions chains together into a "Cycle" that ends when the Blightstorm wipes the map.

---

## 2. Grid/Space Type

### Settlement Map
- **Tile-based, rectangular grid** (not hex). Dynamic grid size — varies by mission type (e.g., 70×70 or 125×125 tiles).
- The map is initially mostly covered by forest. Players expand by clearing trees, revealing Glades.
- `Vector2Int` positions; bounds checked via `MapService.InBounds(x, y)`.
- Buildings are placed on tiles. Each building has a working range (shown as a circle on placement). Red squares mark obstacles.
- The camera is a top-down isometric perspective with free pan and zoom.

### World Map
- **Hex-grid**. The overworld is a large hex-tiled map centered on the Smoldering City.
- Fog of war hides unvisited tiles. The farther a tile is from the center, the higher the difficulty.
- Each hex tile shows: biome, difficulty modifiers, world events, and seal locations.
- The player's caravan has a physical position on the world map and can only move from the last successful settlement.
- 16 special "Seal" fields are distributed across the map, one at each of several difficulty levels.

---

## 3. Core Mechanics

### Win/Loss Conditions (Per Settlement)
- **Reputation (blue bar):** Fill it before the Impatience bar fills → win.
- **Impatience (red bar):** Filled when villagers leave, needs go unmet, or the Queen's demands are ignored → loss.
- Gaining Reputation also reduces Impatience.

### Resources
Resources fall into two broad tiers:
- **Raw/basic goods:** Wood, stone, clay, copper ore, food staples (berries, insects, mushrooms, roots, etc.), fiber, resin, sea marrow.
- **Processed/complex goods:** Planks, bricks, fabric, pottery, ale, pie, jerky, coats, tools, training gear, packs of provisions, etc.
- **Special resources:** Amber (currency), Crystalized Dew (meta currency), Seal Fragments, Blightrot Cysts.

### Buildings
Buildings fall into these categories (as shown in the in-game build menu):
- **Roads** — improve movement speed.
- **Camps** — gather raw resources from nodes on the map (Woodcutters' Camp, Stonecutters' Camp, Harvesters' Camp, Small Gathering Camps, Fishing Hut, Geyser Pump, Rain Collector, Mine).
- **Food Production** — farm food from Fertile Soil; cook complex foods.
- **Housing** — satisfy villager shelter needs (basic housing + species-specific housing).
- **Industry** — process raw goods into complex goods using Recipes.
- **City Buildings** — Hearths, Warehouses, Trading Post, and unlockable service buildings.
- **Decorations** — used to upgrade Hearths.

**Building panel structure (3 tabs):**
- **Production tab:** Recipes list (with on/off toggles), worker slots, production rates.
- **Storage tab:** Ingredients section showing goods stored for recipes; storage limits.
- **Effects tab:** Active bonuses and penalties applied to the building.

Players can cycle between buildings of the same type using arrow buttons next to the building name in the panel.

### Recipes
- Industry buildings produce complex goods using Recipes. Each recipe specifies input goods and output goods.
- Players can enable/disable individual recipes in a building.
- Holding Ctrl shows recipe status overlays on all buildings at once.
- The Recipes Cookbook (accessible via HUD button) lists all available recipes, organized into 7 categories: All, Food, Building Materials, Consumable Items, Crafting Resources, Trade Goods, Fuel & Exploration. It has two modes: ingredient mode (what can be made from X) and product mode (how to make X).

### Workers / Species
Seven playable species (Humans, Beavers, Harpies, Lizards, Foxes, and DLC species Frogs and Bats). Each settlement run uses exactly 3 species.
- Each species has unique production bonuses (e.g., Beavers are faster at woodcutting and engineering; Lizards are better at tending animals).
- Each species has unique needs: specific housing, food preferences, and services.
- Workers are assigned to buildings via a radial menu (click the worker slot icon).
- Shift+click on a slot fills all remaining slots of that type in one action.
- Holding Alt shows worker assignments and empty slots on all buildings simultaneously.

### Villager Needs and Resolve
- **Resolve** = villager morale. Once it drops to 0, villagers leave, filling the Impatience bar.
- Resolve is affected by: housing, complex food, services, species-specific bonuses, and working conditions.
- **Housing needs:** Basic Housing (small resolve bonus) + species-specific Housing (additional bonus).
- **Complex Food needs:** Satisfied when villagers rest at a Hearth and eat available complex foods. Each satisfied complex food need gives a Resolve and yield bonus.
- **Services needs:** Require both a service building and a good consumed there (e.g., an apothecary consuming herbs).
- **Hearth:** Central building where villagers rest and eat. Needs fuel (wood, coal, oil, etc.) to stay lit. Upgradeable with Decorations. The main Hearth also defines the settlement's perimeter.

### Seasons (Per Settlement Year)
Each in-game year has 3 seasons:
- **Drizzle:** Mild weather. Positive forest mysteries are active. Farmers plant crops. Rainwater collectors gather Drizzle Water. At the start of each Drizzle, a new Cornerstone is offered.
- **Clearance:** Short "good weather" window. Crops grow. Clearance Water collected.
- **Storm:** Harsh weather, production penalties, special events. Storm Water collected. Hostility events can occur.

Season durations range from 2 to 4 minutes of real time depending on Prestige level.

### Reputation and Impatience
- **Reputation sources:** Completing Orders (each earns 1 Reputation point), solving Glade Events, reaching Resolve thresholds, completing Trade Routes, fulfilling Payments.
- **Impatience sources:** Villagers leaving (Resolve → 0), unresolved dangerous glade events, hostile events, Orders declining.
- The Reputation bar has thresholds that trigger **Reputation Rewards** popups when crossed.

### Orders
- The Queen sends 9–12 Orders per run. Each Order offers 2–3 choice options; the player picks one.
- Completing an Order grants 1 Reputation.
- Orders typically involve delivering specific goods or reaching population/building goals.

### Cornerstones and Perks
- **Cornerstones:** Powerful bonuses offered once per year at the start of Drizzle. Rarity alternates by year: odd years = Epic (purple), even years = Legendary (gold). A new Cornerstone can be chosen through year 10.
- **Perks:** Similar bonuses but purchased with Amber at the Trading Post or earned from events. Come in 4 rarities: Uncommon (green), Rare (blue), Epic (purple), Legendary (gold).
- **Wildcards:** Special picks that allow choosing any available perk from a pool (governed by `EffectsService.GetWildcardPicksLeft`).

### Glades and Events
- The settlement map starts surrounded by forest. Cutting trees reveals **Glades** — open areas that contain resource nodes and events.
- **Small Glades:** Safe but less rewarding. Opening too many adds +15 Hostility.
- **Dangerous Glades:** Trigger a Dangerous Event — a mini-quest requiring specific goods or workers to resolve.
- **Forbidden Glades:** Extremely dangerous; can destroy the settlement quickly if unresolved.
- **Glade Events:** Recognizable by overhead exclamation marks. Most offer two resolution choices (e.g., deconstruct for resources vs. repair for a bonus). Some events pose ongoing threats with timers.

### Hostility
- A measure of how much the wilderness opposes the settlement.
- Raised by: opening too many glades, negative modifiers, certain events.
- Higher hostility increases the frequency/severity of dangerous events.

### Trade and Amber
- **Trading Post:** Allows buying goods from visiting Traders using Amber. Traders also sell building Blueprints and Perks.
- **Trade Routes:** Exportable goods to nearby settlements in exchange for Amber. The HUD button shows two counters: routes ready to collect and routes available to start.
- **Black Market:** A special trading mechanic with its own panel.

### Payments
- Periodic tribute payments the Queen demands. Missing payments increases Impatience.

### Meta-Progression (World Map / Cycle)
- **Cycle:** The world map resets (Blightstorm) every ~30–35 in-game years. Each run contributes progress toward the Cycle.
- **Seals:** End-game objectives. Collecting enough Seal Fragments (earned by winning settlements) allows entering the Sealed Forest and reforging a Seal, completing the Cycle.
- **Deeds:** Goals and achievements tied to specific play conditions; visible in the Deeds panel.
- **Prestige:** Optional difficulty scaling. Higher Prestige unlocks harder challenges and tile types on the world map.
- **Capital/Citadel upgrades:** Meta resources (Crystalized Dew, Machinery, etc.) spent in the Citadel to unlock permanent bonuses and new building blueprints.

---

## 4. Control Scheme

### Primary Input: Mouse-Driven
Against the Storm is fundamentally mouse-driven. Nearly all actions require clicks (building placement, worker assignment, recipe toggle, panel tabs). There is **no controller support** on PC; the console version was redesigned separately by a porting studio.

### Keyboard Shortcuts (Default Bindings)
- **Space** — Pause/resume game.
- **1 / 2 / 3** — Set game speed (1x, 2x, 3x).
- **Escape** — Close current panel/popup.
- **B** — Toggle resource node highlights on the map.
- **Alt (hold)** — Show building overlays: worker assignments, empty slots, specializations.
- **Ctrl (hold)** — Show recipe overlays on all buildings: which recipes are enabled/disabled.
- **Tab** — Show Rainpunk Engine overlays on buildings.
- **Shift+click** — Copy building (when a building panel is open) or fill all worker slots of a type.
- **Arrow keys** — Cycle between buildings of the same type when a building panel is open.
- **Building Shortcuts (configurable)** — Hotkey shortcuts to select specific building blueprints for placement. Customizable in the options menu.
- All keybindings are rebindable via the in-game options menu.

### Accessibility Notes from the Mod's Perspective
The game has **no native screen reader support**. All UI interaction is point-and-click. The mod intercepts input before it reaches the game engine using BepInEx's InputSystem patches and provides keyboard navigation + Prism speech output.

---

## 5. Game Screens / Views

### Main Menu
- Play, Settings, Credits, Quit.
- Likely includes New Game / Continue / Load options.

### World Map
- Hex grid view of the Cycle's map.
- Player's caravan position shown. Fog of war on unvisited tiles.
- Click a hex to view tile info (biome, modifiers, world events) and embark.
- **Embark Panel:** Pre-run setup — choose caravan goods, view tile details, confirm embark.
- Active World Events and seasonal modifiers shown on tiles.
- Accessible via `WorldMapKeyHandler`, `WorldMapNavigator`, `WorldMapInfoHandler`.

### Settlement (In-Game / Main Gameplay)
The primary gameplay screen. Contains:

**HUD elements (always visible):**
- Population counter (total / working / homeless).
- Species panel (shows each species count and resolve bars).
- Reputation bar and Impatience bar (at top of screen).
- Year/season indicator and timer.
- Resource panel (scrollable list of all goods and their current stockpiles).
- Alerts panel (left side — notifications about problems).
- Speed controls (1x/2x/3x/pause).
- Trade Routes button (with ready/available counters).
- Newcomers button (with timer tooltip).
- Build menu (bottom toolbar — blueprint categories and building shortcuts).
- Orders button / panel toggle.
- Cornerstone/Perk button (when available).
- Recipes Cookbook button (top-right area).

**Building Panel** (shown when a building is selected):
- Building name with cycle-through arrows (navigate same-type buildings).
- Worker slots (click to assign species via radial menu).
- Three tabs: Production, Storage, Effects.
- Move / Demolish / Deactivate controls (top-left of panel).
- Accessible via `BuildingPanelHandler` and the various `*Navigator.cs` classes.

**Map Overlay Modes** (triggered by hotkeys, not separate screens):
- Alt overlay: worker assignments.
- Ctrl overlay: recipe status.
- Tab overlay: Rainpunk engines.
- B overlay: resource nodes.

### Popups / Overlays (During Settlement or World Map)
These open as modal popups over the main view. The mod has an overlay file for each:

| Popup | Description | Overlay File |
|-------|-------------|--------------|
| Cornerstone Pick | Choose a cornerstone at year start | `CornerstoneOverlay.cs` |
| Perk Crafter (Cornerstone Forge) | Craft custom cornerstones | `PerkCrafterOverlay.cs` |
| Reputation Reward | Choose a bonus when rep threshold hit | `ReputationRewardOverlay.cs` |
| Order Pick | Choose between Order options | `OrderPickOverlay.cs` |
| Orders List | View all active Orders | `OrdersOverlay.cs` |
| Newcomers | Choose arriving species group | `NewcomersOverlay.cs` |
| Trader | Buy goods/blueprints/perks with Amber | `TraderOverlay.cs` |
| Trade Routes | Manage export routes for Amber | `TradeRoutesOverlay.cs` |
| Black Market | Special trading panel | `BlackMarketOverlay.cs` |
| Recipes Cookbook | Browse all recipes by category | `RecipesOverlay.cs` |
| Consumption Control | Adjust consumption priorities | `ConsumptionOverlay.cs` |
| Trends | View production/consumption trends | `TrendsOverlay.cs` |
| Payments | Queen's tribute demand popup | `PaymentsOverlay.cs` |
| World Event | Hostile event choice popup | `WorldEventOverlay.cs` |
| Wildcard Pick | Choose a perk from wildcard pool | `WildcardOverlay.cs` |
| Rewards Pack | Choose post-run rewards | `RewardsPackOverlay.cs` |
| Encyclopedia (Wiki) | In-game reference for all content | `EncyclopediaNavigator.cs` |
| Dialogue | NPC/event narrative choices | `DialogueOverlay.cs` |
| Forsaken Altar | Sacrifice buildings for effects | `AltarOverlay.cs` |
| Seal | Seal reforging / Sealed Forest | `SealOverlay.cs` |
| Game Result | Win/loss end screen | `GameResultOverlay.cs` |
| Deeds / Goals | View goals and achievements | `DeedsOverlay.cs` |
| Resupply | Resupply caravan options | `ResupplyOverlay.cs` |

### Capital / Citadel
- Meta-progression screen for spending Crystalized Dew, Machinery, etc.
- Accessible via `CapitalOverlay.cs`, `CapitalUpgradeOverlay.cs`.

### World Map Meta Panels
- **Cycle End** — shown when a Seal is reforged / Cycle completes (`CycleEndOverlay.cs`).
- **Meta Rewards** — awards from completing a Cycle (`MetaRewardsOverlay.cs`).
- **Daily Expedition** — special daily run mode (`DailyExpeditionOverlay.cs`).
- **Custom Games / Training** — custom game setup (`CustomGamesOverlay.cs`).
- **Games History** — history of past runs (`GamesHistoryOverlay.cs`).
- **Ironman / Queen's Hand Trial** — special hardcore mode setup (`IronmanOverlay.cs`).
- **Profiles** — player profile selection (`ProfilesOverlay.cs`).

### Panels (Settlement Info)
Opened via keyboard in the mod (not always distinct game screens):
- **Info Panel Menu** — hub for the settlement's status panels (`InfoPanelMenu.cs`).
- **Villagers Panel** — all villagers, their needs, resolve, housing (`VillagersPanel.cs`).
- **Workers Panel** — worker assignments by building (`WorkersPanel.cs`).
- **Settlement Resource Panel** — current stockpile overview (`SettlementResourcePanel.cs`).
- **Stats Panel** — production/consumption statistics (`StatsPanel.cs`).
- **Mysteries Panel** — active mysteries/seasonal events (`MysteriesPanel.cs`).
- **World Map Effects Panel** — active modifiers from the world map tile (`WorldMapEffectsPanel.cs`).
- **Rewards Panel** — pending reward choices (`RewardsPanel.cs`).
- **Announcement History** — log of past announcements/events (`AnnouncementHistoryPanel.cs`).
- **Build Menu Panel** — keyboard-accessible building construction menu (`BuildingMenuPanel.cs`).
- **Embark Panel** — pre-run setup when leaving on a new expedition (`EmbarkPanel.cs`).
- **Help Overlay** — F12 context-sensitive help (`HelpOverlay.cs`).

---

## 6. UI Elements Summary

### Panels (persistent, docked to screen edges)
- Top HUD: reputation bar, impatience bar, year/season timer.
- Left: alerts / notifications.
- Right: resource list (scrollable).
- Bottom: build menu toolbar with blueprint shortcuts.
- Species bar: population counts with resolve indicators.

### Popups (modal, block interaction)
- Most game decisions happen in modal popups: cornerstones, orders, newcomers, trade, rewards, events.
- Popups are managed by `PopupService` (game-side) and routed by the mod's `PopupRouter`.
- Each popup is detected via Harmony patches on `PopupService.Show/Hide` methods.

### Building Panel (right-side panel)
- Opens when clicking any building.
- Has tabs (Production/Storage/Effects), worker slot radial menu, and navigation arrows.
- Different building types have different panel content — hence the multiple `*Navigator.cs` files.

### Tooltips
- Hovering over most UI elements shows tooltips. Tooltips contain the detailed information that sighted players rely on heavily — not directly accessible to screen readers without the mod.

### Overlays (non-modal, triggered by holding keys)
- Alt / Ctrl / Tab overlays appear as icon-labels over buildings on the map. Not separate UI screens.

### Encyclopedia
- In-game reference for all buildings, goods, species, and glade events.
- Initially locked for glade event entries until the event is experienced.
- Accessible via `EncyclopediaNavigator.cs` in the mod.

---

## 7. BepInEx Modding

### Setup
- **BepInEx version:** 5.4.x (x86 compatible). Install via Thunderstore r2modman or Gale Mod Manager, or manual drop into `Against the Storm/BepInEx/plugins/`.
- **Game assembly:** `Against the Storm_Data/Managed/Assembly-CSharp.dll`. Inspect with dnSpy or ILSpy.
- **Main namespace:** `Eremite` (all game code lives here). Key sub-namespaces:
  - `Eremite.Controller` — GameController, MainController, MetaController (singletons).
  - `Eremite.Services` — all game service classes.
  - `Eremite.Model` — data/model classes (Settings, BuildingModel, GoodModel, etc.).
  - `Eremite.MB` — MonoBehaviour base class with static `Settings` property.
  - `Eremite.View` — UI/view classes (building panels, popups, etc.).

### Patching Approach
- **Harmony / HarmonyX** — used for runtime method patching (prefix, postfix, transpiler patches).
- The mod patches `PopupService` show/hide methods to intercept popup lifecycle events.
- The mod patches the Unity `InputSystem` to intercept keyboard input before the game processes it (`InputPatches.cs`, `InputBlocker.cs`).

### Community Resources
- **Official modding wiki:** https://wiki.hoodedhorse.com/Against_the_Storm/Modding
- **Thunderstore mod database:** https://thunderstore.io/c/against-the-storm/
- **ATS API (community):** https://github.com/JamesVeug/AgainstTheStormAPI — helper layer for content mods; not used by this mod (which is a pure reflection/patching mod).
- **Example mod template:** https://github.com/ats-mods/ModTemplate
- **Stormwalker (QoL mod, good code reference):** https://github.com/ats-mods/Stormwalker
- **Modding Discord:** Official Against the Storm Discord + unofficial ATS modding Discord.
- **Nexus Mods:** https://www.nexusmods.com/againstthestorm (secondary community hub).

### Reflection Pattern Used by This Mod
Because the game updates can break direct references, this mod uses reflection to access all game internals:
- Type metadata (FieldInfo, PropertyInfo, MethodInfo) is cached at startup — safe because type structure survives scene changes.
- Service instances are **never** cached — retrieved fresh each use because services are destroyed on scene change.
- Access goes: `GameController.Instance → GameServices → [service] → [method/field]`.
- Helper: `ReflectionHelper.cs` provides null-safe wrappers for all access patterns.

---

## 8. Accessibility

### Native Game Accessibility Features
According to the Family Gaming Database accessibility report, Against the Storm has 19 documented accessibility features. Strengths include:
- **Text size:** Large, clear text throughout (at least 1/20 screen height, ~46px on 1080p).
- **Audio balance:** Separate music and SFX volume sliders.
- **Game speed:** Adjustable speed (pause, 1x, 2x, 3x) — reduces time pressure.
- **Auto-save:** Automatic saves so the player can stop at any time.
- **No motion sickness triggers:** No 3D movement; motion blur/depth of field can be disabled.
- **Input remapping:** Full mouse/keyboard rebinding.

### What Is NOT Accessible Natively
- **No screen reader support.** All information is conveyed visually — building names, resource counts, recipe status, villager needs, event descriptions.
- **No keyboard navigation.** All panels and popups require mouse clicks to interact with.
- **No controller support on PC.** Console version redesigned separately.
- **Tooltips are hover-only.** Critical information (recipes, worker bonuses, effect descriptions) lives in tooltips that require mouse hover.
- **Colorblind mode:** Not documented as supported (unconfirmed).

### This Mod's Accessibility Coverage
The mod provides screen reader support via Prism for the following (each with a dedicated overlay/navigator/handler):

**Settlement gameplay:**
- Building panel navigation (all building types: production, farm, fishing, hearth, house, institution, hydrant, storage, relic, shrine, port, water, farmfield, poro).
- Map navigation — tile info, glades, buildings on the map (`MapNavigator.cs`).
- Settlement key handler — resource overview, HUD reading (`SettlementKeyHandler.cs`).
- Settlement info handler — population, resolve, season info.
- Build mode — navigating the build menu to place buildings.

**Popups/overlays (all major modal decisions):**
- All 20+ modal popups listed in Section 5 above.

**World map:**
- World map tile navigation and info reading (`WorldMapNavigator.cs`, `WorldMapKeyHandler.cs`).
- Embark panel for pre-run setup.

**Event announcements:**
- Resource production/consumption events.
- Villager arrival/departure events.
- Season changes.
- Building completion and destruction.

### Known Accessibility Issues (from Steam discussions)
- One Steam thread explicitly requested accessibility improvements; community response noted the game is "fully accessible on PC with a mod" (referring to this or a similar community mod).
- The game UI is highly visual with color-coded status indicators (reputation bar blue vs. impatience bar red, resolve bars per species) that cannot be read without the mod's text announcements.

---

## Sources

- [Against the Storm — Wikipedia](https://en.wikipedia.org/wiki/Against_the_Storm_(video_game))
- [Against the Storm on Steam](https://store.steampowered.com/app/1336490/Against_the_Storm/)
- [Official Wiki — Buildings](https://wiki.hoodedhorse.com/Against_the_Storm/Buildings)
- [Official Wiki — World Map](https://wiki.hoodedhorse.com/Against_the_Storm/World_Map)
- [Official Wiki — Seasons](https://wiki.hoodedhorse.com/Against_the_Storm/Seasons)
- [Official Wiki — Cornerstones, Perks and Effects](https://wiki.hoodedhorse.com/Against_the_Storm/Cornerstones,_Perks_and_Effects)
- [Official Wiki — Trading](https://wiki.hoodedhorse.com/Against_the_Storm/Trading)
- [Official Wiki — Glade Events](https://wiki.hoodedhorse.com/Against_the_Storm/Glade_Events)
- [Official Wiki — Glades](https://wiki.hoodedhorse.com/Against_the_Storm/Glades)
- [Official Wiki — Villagers](https://wiki.hoodedhorse.com/Against_the_Storm/Villagers)
- [Official Wiki — Resolve](https://wiki.hoodedhorse.com/Against_the_Storm/Resolve)
- [Official Wiki — Modding](https://wiki.hoodedhorse.com/Against_the_Storm/Modding)
- [Official Wiki — List of Keybindings](https://wiki.hoodedhorse.com/Against_the_Storm/List_of_Keybindings)
- [Official Wiki — Beginner's Guide](https://wiki.hoodedhorse.com/Against_the_Storm/Beginner's_Guide)
- [Thunderstore — Against the Storm Mods](https://thunderstore.io/c/against-the-storm/)
- [ATS API — GitHub](https://github.com/JamesVeug/AgainstTheStormAPI)
- [ModTemplate — GitHub](https://github.com/ats-mods/ModTemplate)
- [Stormwalker mod — GitHub](https://github.com/ats-mods/Stormwalker)
- [Family Gaming Database — Accessibility Report](https://www.familygamingdatabase.com/accessibility/Against+the+Storm)
- [PCGamingWiki — Against the Storm](https://www.pcgamingwiki.com/wiki/Against_the_Storm)
- [Devlog — World Map, Deeds and Phases](https://eremitegames.com/devlog-world-map-deeds-and-phases/)
- [Experimental Update — World Map Overhaul](https://eremitegames.com/experimental-cycle-and-seals/)
- [Blightstorm Cycle — Fandom Wiki](https://against-the-storm.fandom.com/wiki/Blightstorm_Cycle)
- [All Controls and Hotkeys — Magic Game World](https://www.magicgameworld.com/all-controls-and-hotkeys-in-against-the-storm-for-pc/)
