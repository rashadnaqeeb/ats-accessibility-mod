# HouseNavigator.cs
Navigator for House buildings (Shelters).
Provides navigation through Residents and Upgrades sections.

## class HouseNavigator: BuildingSectionNavigator (line 11)

### Fields (private enum SectionType, line 16)
- `Residents`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 25)
- `private` `SectionType[]` `_sectionTypes` (line 26)
- `private` `List<int>` `_residentIds` (line 29)
- `private` `int` `_currentCapacity` (line 30)

### Properties
- `protected override` `string` `NavigatorName` (line 36) - Returns `"HouseNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 38)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 42) - Residents: 1 (capacity) + resident count (minimum 1 for "None"); Upgrades: delegates to `_upgradesSection`
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 57)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 62)
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 76) - Only Upgrades has sub-items
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 86)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 92)
- `protected override` `void` `RefreshData()` (line 99)
- `protected override` `void` `ClearData()` (line 123)
- `private` `void` `AnnounceResidentItem(int itemIndex)` (line 134) - Index 0: capacity "X of Y"; Index 1+: resident name + race (or "None" / "Unknown villager")
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 174)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 180)
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 194)
- `private` `string` `GetResidentItemName(int itemIndex)` (line 204) - Index 0: "Capacity"; Index 1+: actor name
