# Changes since v1.0.5

## New features

- Plain S/V/O keys restored in settlement map viewer for quick stats, species resolve, and tracked orders
- Type-ahead search: repeat-letter cycling, filtered results, rollback on failed characters
- Shift+N to jump to latest event location; Enter in notification history to jump to event location
- Comma/Period to cycle worker buildings with category filter
- Stored count shown in production ingredient announcements
- Reward chase relics added to Glades scanner category
- Alt+N for notifications panel, Alt+H for hearth reset
- Alt+K to toggle appending coordinates to tile announcements (Ancient Hearth as origin)
- E key for entrance preview in build and move modes
- D key for range info in move mode
- Enter in build mode places building and exits; build/move/mark modes auto-close when a menu opens
- Royal Resupply overlay for world map cycle effects pick popup

- Seal candidate glades shown in scanner via triangulation from discovered guiding stones
- Guiding stone I key now announces exact degree bearing followed by description
- Numbered bookmarks (Ctrl+0-9 set, Shift+0-9 jump, Alt+0-9 direction); Alt+B for B-bookmark direction; blight info moved to Alt+D

## Bug fixes

- Options menu fuel toggles now recognized as checkboxes with proper checked/unchecked state feedback
- Options menu section headings announced when navigating into a new section (e.g. Gameplay, Autopause, Video)
- World map tiles that can't be embarked now show the actual reason (blightstorm approaching, not enough seal fragments, seal already attempted) instead of generic "Out of reach"
- D key on world map now shows embark range and distance from actual embark point (last town) instead of capital
- City tiles on world map now read the localized settlement name instead of "City"
- Fix hex distance formula using wrong Mathf.Max overload (float params instead of nested int)
- Fix embark range off-by-one: game uses strict less-than in pathfinding, so effective range is one less than raw value
- Seal and order objectives now show reputation source (e.g. "from Orders") and use localization-aware number placement
- Fix number doubling in objectives when good names contain amounts (e.g. "Deliver 50 Pack of Luxury Goods")
- Completed objectives prefixed with ✓ instead of awkward inline "Done" text
- Fix double period in seal offering description
- Order pick objectives now show reputation source and use consistent localization logic
- Fix guiding stone direction calculation (was reporting wrong compass direction)
- Type-ahead search no longer clears on modifier keys or arrow navigation
- Favouring now stops old race before starting new one
- WorkersPanel profession display fixed
- I key tile info blocked on unrevealed glades
- Fix cross-category navigation getting stuck on empty categories in modifiers and resources panels
- Fix relic and poro panels announcing "Info" on open that doesn't appear during navigation
- Fix building pause/unpause sound playing twice (game already plays it internally)
- Space key now toggles building pause/active from any section in building panels
- Fix operator precedence bug in WrapIndex causing incorrect index wrapping
- Fix static state bugs: stale cached instances, stuck flags, and per-game state persisting across scene changes
- Fix ReflectionHelper.GetEnum silently failing on boxed enums (InvalidCastException)
- Fix FormattingUtils.FormatTime crash on NaN/Infinity inputs

## Internal / refactoring

- MenuBase introduced; all 40 overlay and panel classes migrated from TwoLevelPanel and BuildingSectionNavigator
- Type-ahead search centralized via ISearchable interface
- Unhandled keys pass through in build and move mode by default
- "Slot" prefix removed from orders overlay labels
- Plain S/V/O keys removed from settlement handler; B key passed through build, move, and tree mark modes
- Overlays closed on scene unload to prevent stale state
- Scanner coordinates always appended to Home/End key announcements; rescan on subcategory change
- Dead code and redundant log removed from ResupplyOverlay
- .editorconfig added; dotnet format applied to all source files
- PowerShell build and deploy script added
- Extract PopupRouter from AccessibilityCore, replacing ~600 lines of mirrored if/else popup routing
- Add ReflectionHelper and FormattingUtils; migrate all 33 reflection files to eliminate _args arrays, try/catch boilerplate, and duplicated FormatTime/YearToRoman
- Extract scattered reflection from 9 files into PopupReflection, MapReflection, TileInfoReflection, StatsReflection, EventReflection; extend WorldMapReflection with MetaState/CycleState sections
- Add ReflectionHelper.DictGetInt; migrate remaining manual dictionary iteration in StatsReader, TrendsReflection, WorldMapReflection; remove duplicate helpers from TileInfoReflection and SealOverlay
- Move IKeyHandler into MenuBase; remove duplicate IsActive/ProcessKey boilerplate from 39 subclass files
- Refactor TraderOverlay to use MenuBase level system; remove custom parallel navigation (~150 lines)
