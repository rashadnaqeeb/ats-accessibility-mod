# StorageNavigator.cs
Navigator for the main Storage building (warehouse).
Provides navigation through Goods, Workers, Abilities, and Upgrades sections.

## class StorageNavigator: BuildingSectionNavigator (line 11)

### Fields (private enum SectionType, line 16)
- `Goods`
- `Workers`
- `Abilities`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 27)
- `private` `SectionType[]` `_sectionTypes` (line 28)
- `private` `List<(string goodName, string displayName, int amount)>` `_goods` (line 31) - From global storage
- `private` `int` `_abilityCount` (line 34)

### Properties
- `protected override` `string` `NavigatorName` (line 40) - Returns `"StorageNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 42)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 46) - Goods/Abilities: min 1 for empty message; Workers/Upgrades: delegate to sections
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 64)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 81)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 86)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 106)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 117) - Workers: assign/unassign and return to Level 1; Upgrades: purchase
- `protected override` `void` `RefreshData()` (line 136)
- `protected override` `void` `ClearData()` (line 144)
- `private` `void` `RefreshGoodsData()` (line 156) - Gets goods from global storage; sorts alphabetically by display name
- `private` `void` `RefreshAbilityData()` (line 170)
- `private` `void` `BuildSections()` (line 174) - Conditionally includes Abilities, Workers, Upgrades
- `private` `void` `AnnounceGoodItem(int itemIndex)` (line 208) - Re-fetches goods data before announcing
- `private` `void` `AnnounceAbilityItem(int itemIndex)` (line 227) - Announces ability name, description, and charge count
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 248) - Abilities section: `UseAbility`
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 259)
- `private` `bool` `UseAbility(int abilityIndex)` (line 265) - Returns true even when no charges (still handled); announces charges remaining
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 293)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 299)
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 317)
