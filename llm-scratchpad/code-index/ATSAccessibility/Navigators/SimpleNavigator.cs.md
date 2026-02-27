# SimpleNavigator.cs
Simple navigator for buildings without specialized navigation needs.
Provides basic Info section with building name, description, and status.
Used for: Storage, Decoration, and other non-production buildings.

## class SimpleNavigator: BuildingSectionNavigator (line 11)

### Fields
- `private` `string[]` `_sectionNames` (line 16)
- `private` `string` `_buildingName` (line 17)
- `private` `string` `_buildingDescription` (line 18)
- `private` `bool` `_isFinished` (line 19)
- `private` `bool` `_isSleeping` (line 20)
- `private` `bool` `_hasUpgrades` (line 21)

### Properties
- `protected override` `string` `NavigatorName` (line 27) - Returns `"SimpleNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 29)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 33) - Info section: 1 (name) + optional description + 1 (status); Upgrades: delegates to `_upgradesSection`
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 50) - Only Upgrades section has sub-items (perks)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 58)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 64)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 73)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 79)
- `protected override` `void` `RefreshData()` (line 86)
- `protected override` `void` `ClearData()` (line 107)
- `private` `string` `GetInfoItem(int itemIndex)` (line 119) - Returns "Name: X", "Description: X", or "Status: ..." based on index offset; description item is conditionally present
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 150)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 156) - Returns "Name", "Description", or "Status" for Info section
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 176)
