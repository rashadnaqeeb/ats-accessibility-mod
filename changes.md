# Changelog



## Changes since v1.2.3

### New features

### Bug fixes

### Internal

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
