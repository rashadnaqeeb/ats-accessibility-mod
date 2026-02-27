# WaterNavigator.cs
Navigator for water-producing buildings (RainCatcher, Extractor).
Both extend ProductionBuilding and have workers.
Provides Status (toggle), Water, and Workers sections.

## class WaterNavigator: BuildingSectionNavigator (line 11)

### Fields (private enum SectionType, line 16)
- `Status`
- `Water`
- `Workers`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 27)
- `private` `SectionType[]` `_sectionTypes` (line 28)
- `private` `bool` `_isSleeping` (line 29)
- `private` `bool` `_canSleep` (line 30)
- `private` `string` `_waterTypeName` (line 33)
- `private` `bool` `_isRainCatcher` (line 34)
- `private` `bool` `_isExtractor` (line 35)
- `private` `float` `_productionTime` (line 36)
- `private` `int` `_producedAmount` (line 37)
- `private` `int` `_tankCurrent` (line 38)
- `private` `int` `_tankCapacity` (line 39)

### Properties
- `protected override` `string` `NavigatorName` (line 45) - Returns `"WaterNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 47)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 51) - Status: 0; Water: `GetWaterItemCount()`; Workers/Upgrades: delegate
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 69) - Status: announces "Active" or "Paused"
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 80)
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 97)
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 114)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 120)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 128) - Workers: assign/unassign and return to Level 1; Upgrades: purchase
- `protected override` `bool` `ToggleBuildingSleep()` (line 144) - Refreshes worker IDs after waking
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 165) - Status section: `ToggleBuildingSleep`
- `protected override` `void` `RefreshData()` (line 172)
- `protected override` `void` `ClearData()` (line 182)
- `private` `void` `RefreshWaterData()` (line 194) - Determines building sub-type (RainCatcher vs Extractor); fetches type-appropriate fields
- `private` `void` `BuildSections()` (line 211) - Always: Status, Water; conditionally: Workers, Upgrades
- `private` `int` `GetWaterItemCount()` (line 243) - Base: 2 (water type + tank level); Extractor adds 2 more (production time + amount/cycle)
- `private` `void` `AnnounceWaterItem(int itemIndex)` (line 252) - Index 0: water type name; Index 1: storage N of M (with %); Index 2 (extractor): production time; Index 3 (extractor): amount per cycle
- `private` `string` `FormatTime(float seconds)` (line 292) - Returns "N seconds" or "M minute(s) S second(s)" with correct pluralization
