# FarmfieldNavigator.cs
Navigator for individual farm field tiles.
Provides a flat list of information: Name, Status (Empty/Plowed/Seeded), Expected Yield.
Uses sections as the flat list items so Escape closes directly.

## class FarmfieldNavigator: BuildingSectionNavigator (line 11)

### Fields
- `private` `string` `_buildingName` (line 16)
- `private` `bool` `_isPlowed` (line 17)
- `private` `bool` `_isSeeded` (line 18)
- `private` `string` `_cropName` (line 19)

### Properties
- `protected override` `string` `NavigatorName` (line 25) - Returns `"FarmfieldNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 27) - Returns `new[] { "Name", "Status" }`
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 29) - Always returns 0 (flat list, no items under sections)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 34) - Index 0: building name; Index 1: "Seeded with X" / "Plowed" / "Empty"
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 54) - No-op; flat list uses sections only
- `protected override` `void` `RefreshData()` (line 58)
- `protected override` `void` `ClearData()` (line 72)
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 83) - Returns "Name" or "Status"
