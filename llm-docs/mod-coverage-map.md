# ATS Accessibility Mod - Coverage Map

Generated from codebase analysis. All file paths are under
`C:\Users\rasha\Documents\ATS-Accessibility-Mod\ATSAccessibility\`.

---

## Game Screens / Popups Handled (Overlays)

### Popup-Routed Overlays (via PopupRouter)

These overlays receive events from `PopupService.AnyPopupShown` / `AnyPopupHidden` and extend `MenuBase`.

| Class | OverlayName | Description |
|---|---|---|
| `RecipesOverlay` | "Recipes" | Building recipe management: toggle recipes active/inactive, adjust priority |
| `WildcardOverlay` | "Wildcard" | Wildcard building pick popup; multi-level: pick count -> building selection |
| `ReputationRewardOverlay` | "Reputation reward" | Reputation milestone reward selection (flat list of options) |
| `CornerstoneOverlay` | "Cornerstone" | Cornerstone pick: NPC dialogue, choices, extend/reroll/skip |
| `CornerstoneLimitOverlay` | "Choose cornerstone to remove" | Removes a cornerstone when at limit (flat list) |
| `NewcomersOverlay` | "Newcomers" | Newcomer group selection dialogue + groups (flat list) |
| `OrdersOverlay` | "Orders" | Order list navigation with front-loaded announcements |
| `OrderPickOverlay` | "Pick order" | Sub-popup for selecting an order pick option |
| `IronmanOverlay` | "Ironman Upgrades" | Queen's Hand Trial upgrades: 3-level (sections -> items -> rewards) |
| `CapitalUpgradeOverlay` | "Capital Upgrades" | Smoldering City buy upgrades: 3-level (structures -> upgrades -> rewards) |
| `ConsumptionOverlay` | "Consumption" | Consumption control popup: categories -> goods -> toggle on/off |
| `DeedsOverlay` | "Deeds" | GoalsPopup (Deeds menu): 2-level (categories -> goals within category) |
| `RewardsPackOverlay` | "Rewards" | Port rewards pack popup: flat list of reward items |
| `ResupplyOverlay` | "Royal Resupply" | World map Royal Resupply popup: flat list of supply options |
| `AssaultResultOverlay` | "Assault result" | TraderAssaultResultPopup: stolen goods, perks, consequences, villagers lost |
| `TraderOverlay` | "Trader" | TraderPanel: no-trader info / trader present multi-level (goods/perks/assault) |
| `DialogueOverlay` | "Dialogue" | HomePopup NPC dialogue: header + dialogue text + choices/continue |
| `TradeRoutesOverlay` | "Trade Routes" | Trade routes overview: main menu -> route details |
| `SealOverlay` | "Seal" | Sealed Forest seal panel |
| `WorldEventOverlay` | "World Event" | World map world event popup |
| `TrendsOverlay` | "Trends" | Storage trends popup: flat list of goods with trend data |
| `CycleEndOverlay` | "End Cycle" | World map cycle end popup: XP summary + unlocked capital upgrades |
| `PaymentsOverlay` | "Payments" | Payments popup: flat list of pending payments |
| `MetaRewardsOverlay` | (MetaRewards / MetaLevelUp) | MetaRewardsPopup and MetaLevelUpPopup — raw GameObject handler, delegates to `MetaRewardsPopupReader`; does NOT extend MenuBase |
| `GameResultOverlay` | "Game Result" | Victory/defeat screen: 2-level (top-level items + sub-items) |
| `BlackMarketOverlay` | "Black Market" | BlackMarketPopup: NPC flavor text, reroll, offers with buy/credit sub-menu |
| `AltarOverlay` | "Forsaken Altar" | Forsaken Altar: multi-level (Resources/Cornerstones -> Currencies/Races) |
| `PerkCrafterOverlay` | "Cornerstone Forge" | PerkCrafterPopup: 3-level (main items -> hook/positive/negative -> name editing) |
| `GamesHistoryOverlay` | "Games History" | Games History popup: flat list of past runs |
| `DailyExpeditionOverlay` | "Daily Expedition" | Daily Challenge popup: info items + sub-menus for completion/start |
| `ProfilesOverlay` | "Saves" | Profiles (save selection) popup: flat list of save slots |
| `CustomGamesOverlay` | "Training Expeditions" | Custom Games popup: sections of expedition options |

### Manually-Subscribed Overlays (not via PopupRouter)

These use direct event subscriptions on non-popup game services rather than PopupService.

| Class | OverlayName | Trigger | Description |
|---|---|---|---|
| `CapitalOverlay` | "Capital" | `CapitalReflection.SubscribeToCapitalEnabled/Closed` | Smoldering City screen: Buy Upgrades, Deeds, Game History, Daily/Training Expedition, Home (if unlocked) |
| `WorldTutorialsOverlay` | "Tutorials" | `WorldMapKeyHandler` F1 key | World map tutorial selection HUD |

### Special Overlays (Popup-Routed but handled separately)

| Class | OverlayName | Description |
|---|---|---|
| `EncyclopediaNavigator` | (Encyclopedia) | WikiPopup: 3-panel navigation (Categories / Articles / Content). Does NOT extend MenuBase due to fundamentally different panel model |

### Generic Popup Navigation (Fallback)

| Class | Description |
|---|---|
| `UINavigator` | Generic panel/element navigation for any popup not handled by a specific overlay. Also navigates the main menu via Canvas scanning. Handles tabbed popups, dropdowns, and text input fields |

---

## Navigation Modes (Handlers)

### Key Handlers — Priority Order (highest first)

Registered in `AccessibilityCore.Start()` via `KeyboardManager.RegisterHandler()`. First active handler wins.

| Priority | Class | Context | Description |
|---|---|---|---|
| 1 | `HelpOverlay` | Any | F12 context-sensitive help overlay; when open, captures all keys |
| 2 | `TutorialTooltipHandler` | Settlement/WorldMap | Auto-announces tutorial tooltip text; captures keys while engaged (blocks game interaction during tutorial) |
| 3 | `ConfirmationDialog` | Settlement | Confirmation dialog for destructive actions (destroy building, remove resource, retrieve lake); blocks all input when active |
| 4 | `MetaRewardsOverlay` | Settlement/WorldMap | MetaRewards/MetaLevelUp popup — above GameResult so player can close it first |
| 5 | `GameResultOverlay` | Settlement | Victory/defeat screen — high priority terminal state |
| 6 | `SettlementInfoHandler` | Settlement | Alt+S (quick summary), Alt+V (species resolve), Alt+O (tracked orders) — works even inside other popups |
| 7 | `WorldMapInfoHandler` | WorldMap | Alt+L (level), Alt+R (meta resources), Alt+S (seal), Alt+T (cycle info) — works even inside other popups |
| 8 | `InfoPanelMenu` | Settlement | F1 key: opens info panel menu with sub-panels |
| 9 | `MenuHub` | Settlement | F2 key: quick access menu for popups |
| 10 | `RewardsPanel` | Settlement | F3 key: rewards panel |
| 11 | `AnnouncementHistoryPanel` | Settlement | Alt+H announcement history / Alt+N latest event jump |
| 12 | `BuildingPanelHandler` | Settlement | Building panel accessibility (routes to specific navigator) |
| 13 | `BuildingMenuPanel` | Settlement | Tab key: building construction menu |
| 14 | `BuildModeController` | Settlement | Building placement mode (R=rotate, Space=place, Enter=place+exit, Escape=exit, E=entrance preview, D=range preview) |
| 15 | `MoveModeController` | Settlement | Building relocation mode (R=rotate, Space/Enter=place, Escape=cancel, D=range, E=entrance) |
| 16 | `HarvestMarkHandler` | Settlement | Tree mark/unmark mode (rectangle and single selection) |
| 17 | `EncyclopediaNavigator` | Any | WikiPopup keyboard navigation |
| 18 | `RecipesOverlay` | Settlement | Recipes popup |
| 19 | `WildcardOverlay` | Any | Wildcard popup |
| 20 | `CornerstoneLimitOverlay` | Any | Cornerstone limit popup (child, higher priority than parent) |
| 21 | `CornerstoneOverlay` | Any | Cornerstone pick popup |
| 22 | `NewcomersOverlay` | Any | Newcomers popup |
| 23 | `OrderPickOverlay` | Any | Order pick popup (child, higher than orders) |
| 24 | `OrdersOverlay` | Any | Orders popup |
| 25 | `ConsumptionOverlay` | Any | Consumption control popup |
| 26 | `DeedsOverlay` | Any | Deeds (goals) popup |
| 27 | `ReputationRewardOverlay` | Any | Reputation reward popup |
| 28 | `RewardsPackOverlay` | Any | Rewards pack popup |
| 29 | `ResupplyOverlay` | WorldMap | Royal Resupply popup |
| 30 | `AssaultResultOverlay` | Any | Trader assault result popup (before TraderOverlay so it takes priority) |
| 31 | `TraderOverlay` | Settlement | Trader panel |
| 32 | `DialogueOverlay` | Any | NPC dialogue popup |
| 33 | `SealOverlay` | Settlement | Seal panel |
| 34 | `WorldEventOverlay` | WorldMap | World event popup |
| 35 | `TrendsOverlay` | Settlement | Trends popup |
| 36 | `TradeRoutesOverlay` | Any | Trade routes popup |
| 37 | `CycleEndOverlay` | WorldMap | Cycle end popup |
| 38 | `PaymentsOverlay` | Any | Payments popup |
| 39 | `BlackMarketOverlay` | Settlement | Black Market popup |
| 40 | `AltarOverlay` | Settlement | Forsaken Altar panel |
| 41 | `PerkCrafterOverlay` | Settlement | Cornerstone Forge popup |
| 42 | `GamesHistoryOverlay` | Any | Games History popup |
| 43 | `DailyExpeditionOverlay` | WorldMap/Capital | Daily Expedition popup |
| 44 | `CustomGamesOverlay` | WorldMap/Capital | Training Expeditions popup |
| 45 | `ProfilesOverlay` | MainMenu | Profiles (save selection) popup |
| 46 | `UINavigator` | Any | Generic popup/menu navigation (fallback) |
| 47 | `EmbarkPanel` | WorldMap | Pre-expedition setup panel |
| 48 | `IronmanOverlay` | WorldMap | Ironman upgrade popup (low priority — very context-specific) |
| 49 | `CapitalUpgradeOverlay` | WorldMap | Capital upgrade popup |
| 50 | `CapitalOverlay` | WorldMap | Capital screen |
| 51 | `SettlementKeyHandler` | Settlement | Settlement map navigation (fallback; active when `GameReflection.GetIsGameActive()`) |
| 52 | `WorldTutorialsOverlay` | WorldMap | World tutorials HUD |
| 53 | `WorldMapKeyHandler` | WorldMap | World map navigation (fallback; active when `WorldMapReflection.IsWorldMapActive()`) |

### Additional Handler Details

**SettlementKeyHandler** (`Handlers/SettlementKeyHandler.cs`) — the settlement map fallback:
- Arrow keys: move virtual cursor on grid (Ctrl+Arrow: skip to next change)
- K: announce position; Alt+K: toggle coordinates mode
- I: tile info; Alt+I: scanner item info
- S: quick summary; V: species resolve; T: time summary
- Space: pause/unpause; Shift+Space: destroy building or remove resource (with confirmation)
- 1-4 / Keypad1-4: game speed
- E: entrance info; R: rotate building; M: move building; Shift+M: modifiers panel
- W: worker summary; Shift+W: workers panel; +/-: cycle race or adjust priority; Shift++/-: add/remove worker or global priority
- Period/Comma: cycle worker buildings; Shift+Period/Comma: cycle worker category
- Backspace: toggle tree mark; D: range/blight info; P: rainpunk info; Shift+P: stop engines
- B/Shift+B/Alt+B: jump to / set / direction to bookmark; Ctrl/Shift/Alt+0-9: numbered bookmarks
- F1: info panels; F2: quick access menu; F3: rewards panel; Tab: building menu
- Shift+S/V/O: stats/villagers/orders panels; Shift+N/Alt+N: latest event / announcement history
- Alt+H: reset cursor to hearth; PageUp/Down/Home/End: scanner navigation; Ctrl+F: scanner search

**WorldMapKeyHandler** (`Handlers/WorldMapKeyHandler.cs`) — world map fallback:
- Arrow keys: navigate hex grid
- I: tooltip info; D: embark status and distance; M: effects panel
- L: level info; R: meta resources; S: seal info; T: cycle info; E: open cycle end
- F1: tutorials; Enter: select/embark
- PageUp/Down: scanner type; Alt+PageUp/Down: scanner item; Home: jump; End: direction

**TutorialTooltipHandler** (`Handlers/TutorialTooltipHandler.cs`):
- Auto-announces tooltip text on visibility and text changes
- Has hardcoded accessibility messages for specific tutorial phases (10, 20, 30, 35, 40, 50, 340)
- Enter: advance tutorial; Escape: disengage; Arrows: re-read text

**HarvestMarkHandler** (`Handlers/HarvestMarkHandler.cs`):
- Two selection modes: Rectangle (default) and Single
- Space: select/deselect; Tab: toggle selection mode; Enter: commit; Escape: cancel; C: select all marked (unmark mode)
- Passes through: Arrow keys, Ctrl+Arrow, PageUp/Down, Alt+PageUp/Down, Home, End, K, I, B

---

## Building Navigators

All extend `BuildingSectionNavigator` (which extends `MenuBase`).
Selection logic in `BuildingPanelHandler.SelectNavigator()`.

| Class | Game Building Type(s) | Sections |
|---|---|---|
| `ProductionNavigator` | Workshop, Farm, Mine, Camp, GathererHut, Extractor (generic fallback for all ProductionBuilding) | Status (toggle), Workers, Recipes, Rainpunk (workshops), Inputs, Outputs, Settings (camp modes), Fields (farm capacity), Upgrades |
| `HearthNavigator` | Ancient Hearth, Small Hearth | Fire (fuel priority, fuel types sub-items), Sacrifice (optional), Services (optional), Blight (optional), Workers, Upgrades |
| `HouseNavigator` | House/Shelter | Residents, Upgrades |
| `RelicNavigator` | Relic buildings (glade events) | Phase A (not started): Info/Desc, Decisions, Requirements, Effects, Preview Rewards, Start Investigation. Phase B (in progress): Info/Desc, Status, Workers, Requirements, Effects, Rewards, Cancel. Phase C (finished): Info/Desc, Status, Workers, Storage |
| `PortNavigator` | Port | Phase-based: Planning (goods/level/category/confirm), Collecting (progress/cancel), In Progress (read-only), Rewards (accept) |
| `FishingHutNavigator` | FishingHut | Status (toggle), Bait, Recipes, Workers, Upgrades |
| `StorageNavigator` | Storage (main warehouse) | Goods, Workers, Abilities, Upgrades |
| `InstitutionNavigator` | Tavern, Temple (Institution buildings) | Status (toggle), Effects, Services (recipes), Storage, Workers, Upgrades |
| `ShrineNavigator` | Shrine | Effects (tiered shrine abilities) |
| `PoroNavigator` | Poro (creature care) | Info, Happiness, Needs, Product |
| `WaterNavigator` | RainCatcher, Extractor (water) | Status (toggle), Water, Workers, Upgrades |
| `HydrantNavigator` | Hydrant (blight fuel) | Fuel |
| `FarmfieldNavigator` | Individual farm field tiles | Flat info: Name, Status (Empty/Plowed/Seeded), Expected Yield |
| `SimpleNavigator` | Decorations, unrecognized building types (fallback) | Info (name, description, status) |
| `BuildingWorkerSection` | (shared helper, not standalone) | Worker slot management shared across multiple navigators |
| `BuildingUpgradesSection` | (shared helper, not standalone) | Upgrade listing and purchase shared across multiple navigators |

---

## Information Panels

All extend `MenuBase` (via `InfoPanelMenu` hierarchy) except `ConfirmationDialog`.

### Settlement Panels

| Class | OverlayName | Key | Description |
|---|---|---|---|
| `InfoPanelMenu` | "Information panels" | F1 | Top-level menu giving access to Stats, Resources, Modifiers, Villagers, Workers, Announcement Settings |
| `StatsPanel` | "Stats panel" | F1 -> Stats (or Shift+S) | Settlement statistics: reputation, hostility, residents, time, weather, etc., organized by categories |
| `SettlementResourcePanel` | "Resource panel" | F1 -> Resources | Settlement inventory: resources by category with amounts |
| `MysteriesPanel` | "Modifiers" | F1 -> Modifiers (or Shift+M) | Active forest mysteries and cornerstones by category |
| `VillagersPanel` | "Villagers" | F1 -> Villagers (or Shift+V) | Individual villager details: name, species, resolve, needs, happiness |
| `WorkersPanel` | "Workers panel" | F1 -> Workers (or Shift+W) | Worker counts by profession and race |
| `AnnouncementsSettingsPanel` | "Announcement settings" | F1 -> Announcement settings | Toggle which categories of game events get announced |
| `MenuHub` | "Menu Hub" | F2 | Quick access to open game popups: Orders, Consumption, Deeds, Trade Routes, etc. |
| `RewardsPanel` | "Rewards" | F3 | Pending rewards: flat list of reward slots ready to pick |
| `BuildingMenuPanel` | "Building Menu" | Tab | Construction menu: categories -> buildings -> select for build mode |
| `AnnouncementHistoryPanel` | "Notifications" | Alt+N | Recent game event announcement history; also supports jumping to latest event location (Shift+N) |
| `ConfirmationDialog` | (n/a — not MenuBase) | Shift+Space / lake retrieval | Confirmation prompt for building destruction, resource removal, lake retrieval |

### World Map Panels

| Class | OverlayName | Key | Description |
|---|---|---|---|
| `WorldMapEffectsPanel` | "Effects" | M (on world map) | World map effects active on selected hex tile |

### Pre-Expedition Panel

| Class | OverlayName | Trigger | Description |
|---|---|---|---|
| `EmbarkPanel` | "Embark screen" | `EmbarkReflection.OnFieldPreviewShown` | Pre-expedition embark setup: map name, difficulty, modifiers, available starting perks/goods |

### Help Panel

| Class | OverlayName | Key | Description |
|---|---|---|---|
| `HelpOverlay` | (context name) | F12 | Context-sensitive help listing all active key bindings for the current context; captures all keys while open |

---

## Popup Routing Order (PopupRouter Registration Sequence)

Registered in `AccessibilityCore.Start()`. First matching predicate wins when a popup is shown.

1. `GameReflection.IsWikiPopup` -> `EncyclopediaNavigator` (context: Encyclopedia)
2. `RecipesReflection.IsRecipesPopup` -> `RecipesOverlay`
3. `WildcardReflection.IsWildcardPopup` -> `WildcardOverlay`
4. `ReputationRewardReflection.IsReputationRewardsPopup` -> `ReputationRewardOverlay`
5. `CornerstoneReflection.IsRewardPickPopup` -> `CornerstoneOverlay`
6. `CornerstoneReflection.IsCornerstonesLimitPickPopup` -> `CornerstoneLimitOverlay` (on close: also refreshes CornerstoneOverlay)
7. `NewcomersReflection.IsNewcomersPopup` -> `NewcomersOverlay`
8. `OrdersReflection.IsOrdersPopup` -> `OrdersOverlay`
9. `OrdersReflection.IsOrderPickPopup` -> `OrderPickOverlay` (on close: refreshes OrdersOverlay)
10. `IronmanReflection.IsIronmanUpgradePopup` -> `IronmanOverlay`
11. `CapitalUpgradeReflection.IsCapitalUpgradePopup` -> `CapitalUpgradeOverlay`
12. `ConsumptionReflection.IsConsumptionPopup` -> `ConsumptionOverlay`
13. `DeedsReflection.IsGoalsPopup` -> `DeedsOverlay`
14. `RewardsPackOverlay.IsRewardsPackPopup` -> `RewardsPackOverlay`
15. `ResupplyOverlay.IsCycleEffectsPickPopup` -> `ResupplyOverlay`
16. `AssaultResultOverlay.IsAssaultResultPopup` -> `AssaultResultOverlay`
17. `TradeReflection.IsTraderPanel` -> `TraderOverlay` (also sets/clears `TradeReflection.CurrentPanel`)
18. `NarrationReflection.IsHomePopup` -> `DialogueOverlay`
19. `TradeRoutesReflection.IsTradeRoutesPopup` -> `TradeRoutesOverlay`
20. `SealReflection.IsSealPanel` -> `SealOverlay`
21. `WorldEventReflection.IsWorldEventPopup` -> `WorldEventOverlay`
22. `TrendsReflection.IsTrendsPopup` -> `TrendsOverlay`
23. `CycleEndOverlay.IsWorldCycleEndPopup` -> `CycleEndOverlay`
24. `PaymentsReflection.IsPaymentsPopup` -> `PaymentsOverlay`
25. `IsMetaRewardsOrLevelUpPopup` (by GameObject name: "MetaRewards" or "MetaLevelUp") -> `MetaRewardsOverlay` (with tutorial tooltip save/restore logic)
26. `GameResultReflection.IsGameResultPopup` -> `GameResultOverlay` (also closes OrdersOverlay)
27. `BlackMarketReflection.IsBlackMarketPopup` -> `BlackMarketOverlay`
28. `AltarReflection.IsAltarPanel` -> `AltarOverlay`
29. `PerkCrafterReflection.IsPerkCrafterPopup` -> `PerkCrafterOverlay`
30. `GamesHistoryReflection.IsGamesHistoryPopup` -> `GamesHistoryOverlay`
31. `DailyExpeditionReflection.IsDailyChallengePopup` -> `DailyExpeditionOverlay`
32. `ProfilesReflection.IsProfilesPopup` -> `ProfilesOverlay`
33. `CustomGamesReflection.IsCustomGamePopup` -> `CustomGamesOverlay`

Fallback chain (unmatched popups): deeds child-capture -> deeds suspend -> generic `UINavigator`

---

## Automatic Event Announcements (EventAnnouncer)

`EventAnnouncer.cs` subscribes to game service events and announces them automatically during gameplay.

| Event Source | What is announced |
|---|---|
| `CalendarService` | Season changes, new day |
| `HostilityService` | Hostility level up/down |
| `TradeService` | Trader departed (arrival covered by game alerts) |
| `OrdersService` | New order available, order completed, order failed |
| `GladesService` | Glade revealed (with danger level) |
| `ReputationService` | Reputation changes (with new tier info) |
| `NewsService` (IMonitorsService) | Game alerts/news (fires for various in-game warnings, trader arrival, newcomer alerts) |
| `NewcomersService` | Newcomers arrival (ready to pick) |
| `VillagersService` | Villager death or departure |
| `GameBlackboard` | Various game state changes (subscribed via game blackboard observables) |
| `ReputationRewardsService` | Reputation rewards becoming available |
| `CornerstonesService` | Cornerstone-related events |
| Harmony patch on `BuildingService` | Sacrifice stops (transitions from on to off) |
| Relic locate events | Relic highlight events (Short Range Scanner, etc.) |

Batching: messages are queued in a 150ms window and combined to prevent interruption when multiple events fire simultaneously. Grace period of 2 seconds after subscription suppresses initialization noise.

---

## Scene Coverage

| Scene | Coverage |
|---|---|
| Main Menu (index 0) | UINavigator via Canvas scanning, deferred setup on first key press after popup closes |
| Settlement (index 1) | Full: map navigation, building panels, all settlement popups, event announcements |
| World Map (index 2) | Full: hex grid navigation, scanner, all world map popups, capital screen, embark panel, tutorials |

---

## TODO Comments and Partial Implementations

No explicit `// TODO`, `// FIXME`, or `// not implemented` comments were found in any source file under `ATSAccessibility/`.

The only items that could be considered partial:
- `EncyclopediaNavigator._helpEntries` is an empty list (`new List<HelpEntry>()`), so the Encyclopedia context shows no entries in the F12 help overlay.
- `TutorialTooltipHandler._accessibilityMessages` only covers tutorial phases 10, 20, 30, 35, 40, 50 (Tutorial 1: Basics) and phase 340 (Tutorial 4: The Cycle). Other tutorial phases fall back to raw game text.

---

## Notes on Architecture

- Overlays that live on non-popup game systems (CapitalOverlay, EmbarkPanel, WorldTutorialsOverlay) use direct UniRx observable subscriptions rather than PopupRouter.
- MetaRewardsOverlay does not extend MenuBase — it works directly on GameObjects and delegates to MetaRewardsPopupReader.
- EncyclopediaNavigator does not extend MenuBase — it uses a custom 3-panel model incompatible with MenuBase's level model.
- BuildingPanelHandler is the routing layer for all building panels; it holds all navigator instances and selects the correct one based on building type.
- SettlementKeyHandler and WorldMapKeyHandler are the lowest-priority handlers (true fallbacks) in their respective scenes.
- The confirmation dialog (ConfirmationDialog) is a handler at priority 3 but is not a MenuBase overlay — it shows in-speech output and captures Enter/Escape only.
