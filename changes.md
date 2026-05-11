# Changelog



## Changes since v1.5.4

### New features

### Bug fixes

### Internal

## v1.5.4

### New features
- Added Thai (`th`) translation.

### Bug fixes
- Fixed Smoldering City "Home" menu item being mistranslated as "start/start page/homepage" in es, es-LATAM, pt, de, fr, and zh-CN; now refers to a place of residence in each language.

## v1.5.3

### Bug fixes
- Building menu is now labelled as "construction menu" (or equivalent) in es, es-LATAM, pt, fr, de, pl, ru, and zh-CN, matching the act of building rather than already-constructed structures.

## v1.5.2

### Bug fixes
- Fixed a bug preventing you from reseting or deleting saves introduced by my type ahead changes.

## v1.5.1

### Bug fixes
- Several localisation fixes.

## v1.5

### New features
- Type-ahead search now works in all languages, not just English.
- Vastly improved result sorting: start-of-name matches beat word-starts, which beat substrings; shorter names rank ahead of longer ones.
- Multi-token search with spaces: typing "wo ca" finds "Woodcutter's Camp".

### Bug fixes
- Many localisation-related fixes.

## v1.4.5

### Bug fixes
- Many localisation-related fixes.

## v1.4.4

### Bug fixes
- Localize recipe names in camp-type production buildings (Woodcutters' Camp, etc.) via game loca table.

## v1.4.3

### Bug fixes
- Many localisation-related fixes.

## v1.4.2

### Bug fixes
- Many localisation-related fixes.

## v1.4.1

### Bug fixes
- Many localisation-related fixes.

## v1.4.0

### New features
- Mine building panel now announces underlying ore type and currently-available charges as a new top section above Status
- Add Simplified Chinese translation (zh-CN.properties)
- Add Spanish (Spain) translation (es.properties)
- Add Spanish (Latin America) translation (es-LATAM.properties)
- Add Portuguese translation (pt.properties)

### Bug fixes
- Scanner now announces underlying ore type and remaining charges when hovering a mine
- Fixed English leaks in French speech for cornerstone / port rarity names, payment-due season and auto-payment labels, rewards-panel next-cornerstone season, mysteries-panel per-mystery season, season-changed event announcements, racial bonus type ("Comfort"/"Efficiency") and 8-point compass directions; producers now emit loca keys or enum ints that resolve at the speech site.
- Fixed relic threat-level leak: `RelicReflection.GetRelicDangerLevel` and `WikiReflection.GetRelicDangerLevel` were returning the raw `DangerLevel` enum name (e.g. "Dangerous") directly into the relic navigator "Threat level" line and the encyclopedia relic entry; now resolve through `common.danger_{none,negative,dangerous,forbidden}`.
- Dropped the invariant-`Name` SO-asset fallback in `StatsReader.GetResolveModifiers` effect-name extraction (would have leaked an English identifier for any effect with a null `displayName` LocaText); falls straight through to `common.unknown_effect`.

### Internal
- Trade Routes offer blocked-reason lines now localize correctly: `TradeRoutesReflection.GetBlockedReason` returns a new `BlockedReason` enum (was a pre-resolved string that the overlay compared against the English literal `"not enough fuel"`, silently skipping the fuel-specific formatting in any non-English language). Overlay formats the enum via `FormatBlockedReason`. Dropped the now-unused `reflection.traderoutes.not_enough_fuel` key.
- Fertile-soil / sand terrain announcements now fire in non-English languages: `MapReflection.GetFieldTypeName` no longer prefers the game's localized `displayName` property — callers in `MapNavigator` and `MapScanner` compare it against `"Grass"` / `"Sand"` as a stable identifier, which previously broke silently when the game was in French.
- Replaced English `"Worker"` fallback in `WorkersPanel.GetVillagerProfession` with `Strings.Get("common.worker")` (reached when reflection fails to resolve a villager's profession display name).
- Consumption Control now localizes its category/need/race status summaries: `ConsumptionReflection.GetCategoryStatus` / `GetNeedStatus` / `GetRaceNeedsStatus` now return a `ConsumptionStatus` enum rather than English literals; `ConsumptionOverlay` formats them via new `overlay.consumption.status.{all_permitted,all_prohibited,mixed,unknown}` keys (FR table updated).
- F1 / F2 / F12 menus now render in the active language: switched all `HelpEntry` descriptions and the `InfoPanelMenu` / `MenuHub` / `RewardsPanel` label arrays from class-load-time `Strings.Get("…")` resolution to lazy key-based resolution (`HelpEntry.Loca(keyName, locaKey)` + `_menuLabelKeys`), so non-English tables applied after class load now take effect. Updated `ValidateStrings` to recognise `HelpEntry.Loca` call sites.
- Localization groundwork: add `Utils/Strings.cs` + `LocalizationReflection.cs` + embedded `Strings/English.properties` table. Detects game language via `TextsService.CurrentLocaCode` and falls back to English. Migrated `ConfirmationDialog`, `HelpOverlay`, and `WorkersPanel` as proof-of-concept; remaining files still use English literals.
- Migrated `Overlays/` English literals to the localization table (`overlay.*` keys)
- Migrated `Navigators/` English literals to the localization table (`nav.*` keys)
- Migrated `Handlers/` English literals to the localization table (`handler.*` keys)
- Migrated `Utils/` English literals to the localization table (`util.*` keys)
- Migrated remaining user-facing strings in `Reflection/` to the localization table (`reflection.*` keys)
- Migrated remaining `Panels/` files to the localization table (`panel.*` keys)
- Localization dedupe pass: consolidated 443 per-subsystem keys with byte-identical values into 159 shared `common.*` keys in `en.properties` (e.g. `common.unknown`, `common.cancelled`, `common.locked`, direction/season names).
- Migrated all 117 `HelpEntry` description literals (F12 help overlay) to the localization table across `Handlers/`, `Overlays/`, `Navigators/`, `Panels/`, and `Core/MenuBase`, wiring the 14 pre-existing orphan help keys to their call sites.
- Migrated remaining `Core/` announcements (`Speech.Say("Main menu" / "World map" / "Game started")`) and defensive fallback literals in `BlackMarketReflection` to the localization table (`core.*` keys and `common.unknown*`).
- Moved trailing-space concatenations out of code and into template values via a new `\s` escape in `Strings` (editor-trim-safe), so translators control inter-phrase spacing.
- Fixed arity bug in `WorkersPanel.OnClosed` that passed an unused arg to the zero-placeholder `common.closed` template.
- Added `Tools/ValidateStrings.ps1` and wired it into `build.ps1` to validate every `Strings.Get` / `Strings.Plural` call site against `en.properties` for missing keys and placeholder/arg arity mismatches before `dotnet build` runs.
- Documented `Strings.Plural` as English-only (`n == 1 → one` / `other`) so it isn't used as-is once a second-language table ships.
- Translation-readiness pass: added `Strings/TRANSLATION.md` (ships-with-game language list, glossary of brand-critical terms, tone guide, file-format cheatsheet), `Tools/ValidateTranslation.ps1` (key-set / placeholder-drift checker for translation files), a `[Localization] ForceLanguage` BepInEx config so translators can smoke-test a new table without changing the game's UI language, and disambiguating context comments on the new F12 help-overlay key block in `en.properties`.
- Added `Utils/LocaDumper` and a `[Localization] DumpGameLocalization` config flag so a translator can write every game language's canonical loca JSON to `Documents/ATSAccessibility-Locas/` in one launch, then grep the game's own rendering of brand terms (Viceroy, Hearth, species names…) when translating the mod.
- Rewrote `Strings/TRANSLATION.md` for LLM-agent use: expanded glossary with missing brand terms (Amber, Ironman, Newcomer, Order, Perk, Mystery, Queen, Trade Route, Biome, Cyst, Capital), added a concrete 3-phase orchestration (resolve glossary once, fan out 7 deterministic scope-based chunks to subagents, reassemble and validate), trimmed user-facing and justification content that wasn't useful to a translator.
- Pruned `Strings/TRANSLATION.md` Phase 2/3 procedural detail (chunk extraction, subagent dispatch, reassembly) — TRANSLATION.md now describes *what* each phase produces; the *how* belongs to the orchestrator invoking it.

## v1.3.4

### New features
- Add effect and good descriptions to embarkation bonus announcements (spend embark points section)
- Include seal fragment requirements in world map brief announcement when moving over seal tiles
- Add scanner auto-move to world map (Alt+Home to toggle, shared setting with in-game map)
- Add cutting distance to scanner glade entries (e.g. "Small glade, 3 deep") showing how many trees to cut from the cleared area to reach each glade

### Bug fixes

### Internal

## v1.3.3

### New features
- Add ingredient mode (Tab) and chain navigation (Right arrow at recipe level) to recipes overlay for following production chains
- Announce "Unmovable" in build menu for buildings that cannot be moved after placement
- Add Alt+M in reputation reward overlay to read construction material costs (with "not enough" indicators)
- Add F12 help entries for reputation reward overlay (Alt+M, Shift+W)

### Bug fixes
- Fix villager death/leave announcements showing raw localization keys (e.g. "Effect RelicRitual Name") instead of resolved text

### Internal

## v1.3.2

### Bug fixes
- Fix Cornerstone Forge shard check always reporting 0 (wrong namespace for Storage type in reflection)
- Fix Cornerstone Forge negative effect selection corrupting tier indices and breaking the building
- Fix crafted cornerstones showing as internal names (e.g. CHE_233) in the modifiers panel
- Fix crafted cornerstones including a stale negative effect when user didn't select one

## v1.3.1

### New features
- Add Shift+W in blueprint pick popups (reputation reward and wildcard) to open the encyclopedia article for the focused building
- Add automaton awareness: worker summaries (W key) and building navigator now show automatons in worker slots and loose automatons attached to buildings

### Bug fixes
- Fix rainpunk water use showing "None" when water storage is empty instead of showing the requested consumption rate
- Fix rainpunk water stored showing 0 of 0 for workshops; append water type name (e.g. "Water stored: 5 of 50 (Clearance Water)")

### Internal

## v1.3

### New features
- Add settlement name support on embark screen: view, edit, and randomize the settlement name before embarking
- Add Biome Resources category to modifiers panel (Shift+M): shows soil grade, deposits, and resources from trees
- Add dynamic state info to modifiers panel: mystery stack counts, cornerstone stacks, hook progress (e.g. "7/15"), and retroactive previews (e.g. "Gained so far: 50 wood")
- Add warehouse storage reserve (minimum) support: view and adjust per-good reserves with +/- keys (Shift for ±10) in the Goods section
- Add hint to trade route offers about using plus/minus to adjust level
- Add per-recipe comparison status to blueprint selection: each recipe shows new, upgrade level, or already unlocked
- Add recipe producibility check to blueprint selection: recipes that cannot be produced on the current map (no obtainable ingredients for a slot) are marked "cannot produce"
- Add Shift+T shortcut to open the trends popup from the settlement map
- Add building idle announcement for workshops and other unmonitored production buildings

### Bug fixes
- Fix hooked effect dynamic preview (e.g. hook progress, retroactive gains) not showing for non-composite effects like Woodcutter's Song

### Internal

## v1.2.6

### New features

### Bug fixes
- Fix farm range preview showing fertile soil in unrevealed glades when pressing D during placement
- Fix glade events with order requirements (e.g. ghost decorations) always blocking investigation start even when objectives are met

### Internal

## v1.2.5

### New features

* Check for mod updates on game launch; announces if up to date or opens the latest GitHub release page if an update is available

### Bug fixes

* Soil values are now read for biomes on the world map in their info panels

### Internal

* Extract version string into Plugin.ModVersion constant (was hardcoded as "1.2.3", corrected to "1.2.4")

## v1.2.4

### New features

* Relic "Start Investigation" now shows order objective progress when an order is the blocker (e.g., cursed woodlands ghost events)

### Bug fixes

* Archaeologist's Office now appears in Special Buildings instead of Decorations in the scanner

## v1.2.3

### Bug fixes

* Range finder (D key) now filters deposits and lakes by recipe grade, so small gathering camps and fishing huts no longer report nodes they can't harvest
* Embark Points in mission info now shows full breakdown (base, difficulty penalty, bonus) and omits zero values

## v1.2.2

### Bug fixes

* Move preparation points penalty from embark difficulty details to world map tile tooltip where it's more accurate

## v1.2.1

### Bug fixes

* Fix world event popup not closing after selecting a decision, leaving the handler stuck consuming all keys
* Announce reward/result description when selecting a world event decision

## v1.2

### New features

* Add F12 context-sensitive help overlay showing all available keybindings for the current screen
* Add offline reflection validation script (validate-reflection.ps1) to check reflected types/members against decompiled game-source

### Bug fixes

* Fix TutorialReflection cached tooltip not being cleared on scene change, preventing stale reference
* Fix GoodModel.displayName using GetProperty instead of GetField, causing recipe produced good names to silently fail
* Fix wiki building category names using wrong accessor for LabelModel.displayName
* Fix game result screen not showing currency rewards due to renamed ConditionsState and MetaCurrency types

### Internal

* Centralize scene index constants into SceneConstants.cs (previously duplicated in AccessibilityCore and MetaRewardsPopupReader)
* Move building construction, placement, range info, supply chain, and priority reflection code from GameReflection into BuildingReflection
* Remove unused Settings.GetText reflection cache in GamesHistoryReflection
* Deduplicate GoodRef and LocaText reflection caches across 8 files into shared GameReflection properties
* Extract HearthReflection.cs from BuildingReflection.cs (hearth-specific reflection code)
* Extract RelicReflection.cs from BuildingReflection.cs (relic/glade-event-specific reflection code)
* Extract PortReflection.cs from BuildingReflection.cs (port/expedition-specific reflection code)
* Move glade info, location markers, relics highlight, harvest mark/unmark, farm range, and seal/guidepost reflection from GameReflection into MapReflection
* Extract ConstructionReflection.cs from BuildingReflection.cs (construction, placement, range info, lake, supply chain, and building enumeration code)
* Consolidate duplicate strider/crew method pairs in PortReflection into unified parameterized API
* Move shared ToggleBuildingSleep implementation from 4 navigators into BuildingSectionNavigator base class
* Deduplicate AdjustNodePriority and AdjustConstructionPriority in SettlementKeyHandler via shared helper
* Remove duplicate GetDirection methods from BlightInfoHelper, RainpunkHelper, EntranceInfoHelper, and MapScanner in favor of NavigationUtils.GetDirection
* Move shared GetCardinalDirection and GetExtensionAnnouncement from BuildModeController and MoveModeController into NavigationUtils
* Add Reset() methods to BuildModeController, MoveModeController, and TutorialTooltipHandler; call them on scene unload to prevent stale state between sessions
* Replace 15 string-based popup type checks (GetType().Name == "X") with cached Type.IsInstanceOfType for earlier breakage detection on game updates
* Extract duplicate Space/Enter placement confirm logic in MoveModeController into shared TryConfirmPlacement method
* Move duplicate FormatTimeLeft from PortNavigator and RelicNavigator into FormattingUtils.FormatTimeRemaining
* Move duplicate CleanupName from ProductionNavigator and FishingHutNavigator into FormattingUtils.CleanupRecipeName
* Move duplicate ProcessKeyEvent bridge from 6 panels into MenuBase
* Move duplicate worker sub-item action pattern from 8 navigators into BuildingSectionNavigator.PerformWorkerSubItemAction
* Extract InstitutionReflection.cs from BuildingReflection.cs (institution/tavern/temple-specific reflection code)
* Extract ShrineReflection.cs from BuildingReflection.cs (shrine/beacon-tower-specific reflection code)
* Extract PoroReflection.cs from BuildingReflection.cs (poro companion creature-specific reflection code)

## v1.1.6

### New features

* Scanner search: Press Ctrl+F, type what you're looking for, and press Enter. This searches across all categories and creates a temporary Search Results category. Navigate results with the same keys as the regular scanner (PageUp/Down for groups, Alt+PageUp/Down for instances). Results are cleared if you switch categories with Ctrl+PageUp/Down or start a new search with Ctrl+F.

## v1.1.5

### Bug fixes

* Fix Extractor (Geyser Pump) placement failing with "Cannot place here" — springs were not removed from the grid before the placement check

## v1.1.4

### New features

* Pressing Enter on hearth sacrifice items now hints to use + and - to adjust levels
* Hearth upgrade section now shows meta-locked tiers with "unlocked through meta progression upgrade" message
* World map stat keys (Alt+L, Alt+R, Alt+S, Alt+T) now work through popups and overlays

### Bug fixes

* Strip trailing period from sacrifice effect descriptions

## v1.1.2

* Add descriptions to shrine ability options (for example the beacon tower)
* Add a confirmation step that properly plays the game's charging sounds when activating abilities
* Fix "keep above" orders showing only the timer instead of met threshold with countdown (e.g. "20/20 for 56 seconds")
* Fix completed objectives dropping the target number and making it unclear what you completed
* Enter key now works to finalise the new location of a building when moving it, like space does
* There is now a transcript of the first cutscene at the bottom of the readme, for those needing to use translation tools

## v1.1.1

### New features

* W key worker summary now includes building specialty with bonus type and matching races (e.g., "Woodworking Efficiency (Beavers)")

### Bug fixes

* Type-ahead search no longer rolls back characters when no matches found; keeps full search term

## v1.1.0

### New features

* Type-ahead search is now a proper search with navigable results
* Alt+H has been replaced with Alt+N. You can now press Enter in notification history to jump to event location
* Shift+N to jump to latest event location
* Comma/Period to cycle buildings with worker slots; Shift+Comma/Period to apply category filters
* Stored count shown in recipe ingredient choices
* Reward Chase relics added to Glades scanner category (these are called stags by the game)
* Alt+K to toggle appending coordinates to tile announcements
* Coordinate system changed so that the origin point (0, 0) is the Ancient Hearth
* Enter in build mode now places a single building and exits
* Royal Resupply is now supported
* Sealed Forest: Guiding stone I key now announces exact degree bearing followed by description
* Sealed Forest: When you find guiding stones, the scanner updates the glade section with candidate glades based on the direction the arrow is pointing. Find more guiding stones to narrow down the candidates.
* Added numbered bookmarks (Ctrl+0-9 set, Shift+0-9 jump, Alt+0-9 direction); Alt+B now gives you the direction to your main (B) bookmark
* The blight helper has been moved to Alt+D

### Bug fixes

* Options menu fuel toggles now recognized as checkboxes
* Options menu section headings announced when navigating into a new section (e.g. Gameplay, Autopause, Video)
* World map tiles that can't be embarked now show the actual reason (blightstorm approaching, not enough seal fragments, seal already attempted) instead of generic "Out of reach"
* D key on world map now shows embark range and distance from actual embark point (last town) instead of capital
* City tiles on world map now read the localized settlement name instead of "City"
* Fix hex distance formula using wrong Mathf.Max overload (float params instead of nested int)
* Fix embark range off-by-one: game uses strict less-than in pathfinding, so effective range is one less than raw value
* Seal and order objectives now show reputation source (e.g. "from Orders") and use localization-aware number placement
* Fix number doubling in objectives when good names contain amounts (e.g. "Deliver 50 Pack of Luxury Goods")
* Completed objectives prefixed with checkmark instead of awkward inline "Done" text
* Fix double period in seal offering description
* Order pick objectives now show reputation source and use consistent localization logic
* Fix guiding stone direction calculation (was reporting wrong compass direction)
* Type-ahead search no longer clears on modifier keys or arrow navigation
* Favouring now stops old race before starting new one
* WorkersPanel profession display fixed
* I key tile info blocked on unrevealed glades
* Fix cross-category navigation getting stuck on empty categories in modifiers and resources panels
* Fix relic and poro panels announcing "Info" on open that doesn't appear during navigation
* Fix building pause/unpause sound playing twice (game already plays it internally)
* Space key now toggles building pause/active from any section in building panels

## v1.0.5

### New features

* Shift+key shortcuts to open panels directly from settlement map: Shift+S (Stats), Shift+V (Villagers), Shift+W (Workers), Shift+M (Modifiers), Shift+O (Orders)
* F1/F2/F3 panel switching - press any F-key while in another panel to switch directly
* Workers panel (F1 menu) showing profession counts by race
* Shift+B to set bookmark at cursor position, B to jump to bookmark, Alt+B for blight info
* Alt+Space to toggle pause from inside building panels and menus
* Alt+S/V/O to check stats summary, species resolve, and tracked orders while in game menus and popups without closing them
* Alt+I to read resource description in the resource panel and scanner focus
* Alt+End to announce scanner item distance from bookmark
* Alt+Home to toggle scanner auto-move cursor mode
* Shift+R for counterclockwise building rotation
* Backspace to directly toggle tree mark at cursor
* Home/End navigation added to all menus, panels, and overlays
* Enter on lake to retrieve stored fish with confirmation dialog
* Shift+Space expanded to remove resource nodes (deposits, lakes, springs)
* Ctrl+Arrow skip navigation now announces tile count
* Typeahead search enabled on encyclopedia categories
* Pause state and game speed changes now announced

### Improvements

* Modifiers panel items flow across category boundaries instead of wrapping
* Resource list navigation flows across category boundaries
* Building menu navigation flows across category boundaries
* Villagers panel simplified: removed individual villagers, added shared needs and favoring sound
* Scanner resources split into Nodes Small/Large categories
* Dropped redundant "one of" from same-amount recipe ingredients

### Bug fixes

* Fix glade trader detection by reading visit from TraderPanel
* Fix entrance announcement to report correct approach tile
* Fix game result overlay not opening on second game
* Fix duplicate tutorial announcements for custom messages
* Refresh relic status and effects data on announce

## v1.0.4

### Bug fixes

* Fixed glade events and trader overlay
* Fixed seal fragments display when no seals have been reforged
* Fixed WaterNavigator worker slots and water type display

## v1.0.3

### New features

* Added the Trends menu (F2 > Trends) - view storage operations (gains and losses) for each good
* Changed the range finder (D key) to also work when used on a resource patch to find nearby exploiters
* Added new announcements for completed orders and newcomers waiting

### Bug fixes

* Fixed glade event effects display
* Fixed service buildings (institution navigator)
* Hopefully fixed fishing huts

## v1.0.1

### New features

* Added Custom Game screen accessibility

### Bug fixes

* Fixed the mod to properly detect NVDA screen reader
* Assorted bug fixes and improvements

## v1.0.0

* Initial release
