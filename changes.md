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
- Royal Resupply overlay for world map cycle effects pick popup

- Seal candidate glades shown in scanner via triangulation from discovered guiding stones
- Guiding stone I key now announces exact degree bearing followed by description

## Bug fixes

- Seal and order objectives now show reputation source (e.g. "from Orders") and use localization-aware number placement
- Fix guiding stone direction calculation (was reporting wrong compass direction)
- Type-ahead search no longer clears on modifier keys or arrow navigation
- Favouring now stops old race before starting new one
- WorkersPanel profession display fixed
- I key tile info blocked on unrevealed glades

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
