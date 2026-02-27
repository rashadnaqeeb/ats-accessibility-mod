# BuildingUpgradesSection.cs
Shared handler for building upgrades section.
Any building navigator can use this to handle the Upgrades section.

## class BuildingUpgradesSection (line 10)

### Fields
- `private` `object` `_building` (line 11)
- `private` `List<BuildingReflection.UpgradeLevelInfo>` `_levels` (line 12)
- `private` `int` `_nextAvailableIndex` (line 13) - Index in `_levels` of the next purchasable level; -1 if all done
- `private` `bool` `_initialized` (line 14)
- `private` `HashSet<int>` `_purchasedThisSession` (line 17) - Tracks purchases locally to prevent duplicates due to game state timing delays

### Methods
- `public` `void` `Initialize(object building)` (line 22) - Clears purchase tracking only if a different building; finds first non-achieved level
- `public` `void` `Clear()` (line 53)
- `public` `bool` `HasUpgrades()` (line 64)
- `public` `int` `GetItemCount()` (line 71) - Returns total number of upgrade levels
- `public` `int` `GetSubItemCount(int levelIndex)` (line 78) - Returns perk count for a given level
- `public` `void` `AnnounceItem(int levelIndex)` (line 89) - Announces level as "Achieved + chosen perk", "Available + cost", or "Locked"
- `public` `void` `AnnounceSubItem(int levelIndex, int perkIndex)` (line 119) - Announces perk name and status (Chosen/Available/Locked) + description
- `public` `bool` `PerformSubItemAction(int levelIndex, int perkIndex)` (line 159) - Purchases an upgrade perk; calls `Initialize` after success to refresh state
- `public` `string` `GetItemName(int levelIndex)` (line 205) - Returns level name for type-ahead search
- `public` `string` `GetSubItemName(int levelIndex, int perkIndex)` (line 215) - Returns perk display name for type-ahead search
- `private` `bool` `IsLevelAchieved(int levelIndex)` (line 229) - Checks game state OR local `_purchasedThisSession`
- `private` `string` `GetChosenPerkName(BuildingReflection.UpgradeLevelInfo level)` (line 238)
- `private` `string` `GetCostText(BuildingReflection.UpgradeLevelInfo level)` (line 249) - Formats cost as "Free" or comma-separated "N GoodName" parts
