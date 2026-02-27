# BuildingSectionNavigator.cs
Base class for building navigators with section-based navigation.
Extends MenuBase to provide 4-level navigation mapped to building concepts:
- Level 0: Sections (Info, Workers, Recipes, Storage, etc.)
- Level 1: Items within section (individual recipes, workers, goods)
- Level 2: Sub-items (recipe settings, worker details) - optional
- Level 3: Sub-sub-items (ingredient options) - optional

All existing subclasses (ProductionNavigator, HouseNavigator, etc.) work
without changes through compatibility properties that map to MenuBase's
level-based index array.

## class BuildingSectionNavigator: MenuBase, IBuildingNavigator (line 20)

### Fields
- `protected` `object` `_building` (line 25)
- `protected readonly` `BuildingUpgradesSection` `_upgradesSection` (line 28)
- `protected readonly` `BuildingWorkerSection` `_workersSection` (line 29)
- `private static readonly` `List<HelpEntry>` `_buildingHelpEntries` (line 211)

### Properties
- `protected` `int` `_navigationLevel` `{ get; set; }` (line 36) - Compatibility shim mapping to MenuBase `Level`/`SetLevel()`
- `protected` `int` `_currentSectionIndex` `{ get; set; }` (line 41) - Maps to `_indices[0]`
- `protected` `int` `_currentItemIndex` `{ get; set; }` (line 46) - Maps to `_indices[1]`
- `protected` `int` `_currentSubItemIndex` `{ get; set; }` (line 51) - Maps to `_indices[2]`
- `protected` `int` `_currentSubSubItemIndex` `{ get; set; }` (line 56) - Maps to `_indices[3]`
- `protected override` `string` `OverlayName` (line 83) - Returns `NavigatorName`
- `protected override` `string` `EmptyMessage` (line 85) - Returns `"No sections"`

### Methods

#### IBuildingNavigator explicit implementations
- `void` `IBuildingNavigator.Open(object building)` (line 65)
- `void` `IBuildingNavigator.Close()` (line 70)
- `bool` `IBuildingNavigator.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)` (line 74) - Returns false if `_building` is null

#### MenuBase sealed overrides (dispatch to level-indexed virtuals)
- `protected sealed override` `int` `GetItemCount()` (line 87) - Dispatches to `GetSections().Length`, `GetItemCount(s)`, `GetSubItemCount(s,i)`, `GetSubSubItemCount(s,i,si)` by level
- `protected sealed override` `string` `GetLabel(int index)` (line 103) - Dispatches to `GetSectionName`/`GetItemName`/`GetSubItemName`/`GetSubSubItemName` by level
- `protected sealed override` `EnterAction` `OnEnter(int index)` (line 113) - Returns `DrillDown` if child count > 0, else `Action`

#### MenuBase virtual overrides
- `protected override` `void` `AnnounceCurrentItem()` (line 132) - Dispatches to level-specific Announce* methods
- `protected override` `void` `OnAction(int index)` (line 149) - Level 0: `PerformSectionAction`; Level 1: `PerformItemAction`; Levels 2-3: Perform Sub/SubSub
- `protected override` `void` `OnSpace(int index)` (line 173) - Level 0: `ToggleBuildingSleep`; Levels 1-3: Perform Item/SubItem/SubSubItem action
- `protected override` `void` `OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers)` (line 190)
- `protected override` `bool?` `HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)` (line 197) - Handles Alt+Space for pause toggle
- `public override` `IReadOnlyList<HelpEntry>` `GetHelpEntries()` (line 215)
- `protected override` `string` `GetOpenAnnouncement()` (line 217) - Returns building name + status ("under construction" / "paused")
- `protected override` `void` `OnOpened()` (line 228) - Logs open, queues first section name via `Speech.Say(..., false)`
- `protected override` `void` `OnClosed()` (line 240) - Calls `ClearData()` and nulls `_building`
- `protected override` `EscapeAction` `OnEscape()` (line 245) - Level > 0: `GoBack`; Level 0: `PassThrough` to close building panel

#### BSN-specific abstracts (subclasses must implement)
- `protected abstract` `string` `NavigatorName` (line 258)
- `protected abstract` `string[]` `GetSections()` (line 259)
- `protected abstract` `int` `GetItemCount(int sectionIndex)` (line 260)
- `protected abstract` `void` `AnnounceSection(int sectionIndex)` (line 261)
- `protected abstract` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 262)
- `protected abstract` `void` `ClearData()` (line 264)

#### BSN-specific virtuals (subclasses may override; default no-ops or 0)
- `protected virtual` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 270)
- `protected virtual` `int` `GetSubSubItemCount(int sectionIndex, int itemIndex, int subItemIndex)` (line 271)
- `protected virtual` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 273)
- `protected virtual` `void` `AnnounceSubSubItem(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 274)
- `protected virtual` `bool` `ToggleBuildingSleep()` (line 276)
- `protected virtual` `bool` `PerformSectionAction(int sectionIndex)` (line 277)
- `protected virtual` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 278)
- `protected virtual` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 279)
- `protected virtual` `bool` `PerformSubSubItemAction(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 280)
- `protected virtual` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 282)
- `protected virtual` `void` `AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)` (line 284)
- `protected virtual` `void` `AdjustSectionValue(int sectionIndex, int delta, KeyboardManager.KeyModifiers modifiers)` (line 285)
- `protected virtual` `string` `GetSectionName(int sectionIndex)` (line 287) - Default: looks up index in `GetSections()` array
- `protected virtual` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 294)
- `protected virtual` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 295)
- `protected virtual` `string` `GetSubSubItemName(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 296)

#### Building helpers (private)
- `private` `string` `GetBuildingStatus()` (line 302) - Returns "under construction", "paused", or null

#### Upgrades/Workers section helpers
- `protected` `bool` `TryInitializeUpgradesSection()` (line 314) - Returns false if no upgrades available
- `protected` `void` `ClearUpgradesSection()` (line 322)
- `protected` `bool` `TryInitializeWorkersSection()` (line 330) - Returns false if worker management not allowed
- `protected` `void` `ClearWorkersSection()` (line 338)
