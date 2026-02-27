# HearthNavigator.cs
Navigator for Hearth buildings (Ancient Hearth, Small Hearth).
Provides navigation through Fire, Sacrifice, Upgrades, Blight, and Workers sections.

## class HearthNavigator: BuildingSectionNavigator (line 12)

### Fields (private enum SectionType, line 17)
- `Fire`
- `Sacrifice`
- `Services` - The Commons (hearth services)
- `Upgrades`
- `Blight`
- `Workers`

### Fields
- `private` `string[]` `_sectionNames` (line 31)
- `private` `SectionType[]` `_sectionTypes` (line 32)
- `private` `bool` `_isMainHearth` (line 33)
- `private` `float` `_fuelLevel` (line 36) - 0-1
- `private` `float` `_fuelTimeRemaining` (line 37)
- `private` `bool` `_isFireLow` (line 38)
- `private` `bool` `_isFireOut` (line 39)
- `private` `List<BuildingReflection.HearthUpgradeInfo>` `_upgradeInfo` (line 42)
- `private` `float` `_corruptionRate` (line 45)
- `private` `List<object>` `_sacrificeRecipes` (line 48)
- `private` `List<BuildingReflection.SacrificeRecipeInfo>` `_sacrificeInfo` (line 49)
- `private` `List<BuildingReflection.FuelInfo>` `_fuelTypes` (line 52)
- `private` `bool` `_servicesMetaUnlocked` (line 55)
- `private` `bool` `_servicesSettlementUnlocked` (line 56)
- `private` `List<BuildingReflection.HearthServiceInfo>` `_serviceRecipes` (line 57)

### Properties
- `protected override` `string` `NavigatorName` (line 66) - Returns `"HearthNavigator"`

### Methods
- `public` `HearthNavigator()` (line 62) - Sets `_workersSection.GetWorkerIdsFunc` to `BuildingReflection.GetHearthWorkerIds`
- `protected override` `string[]` `GetSections()` (line 68)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 72)
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 96) - Fire: fuel types item (index 2) has fuel-type sub-items; Workers: races; Sacrifice: 0
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 114)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 119)
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 145)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 151)
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 163) - Sacrifice: announces usage hint; Services (not unlocked): unlock action
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 197) - Fire+index2: toggle fuel; Workers: assign/unassign and return to Level 1
- `protected override` `void` `AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)` (line 213) - Fire at Level 2: fuel priority; Sacrifice: sacrifice level
- `protected override` `void` `RefreshData()` (line 229) - Builds section list dynamically; conditionally includes Sacrifice, Services, Upgrades, Blight, Workers
- `protected override` `void` `ClearData()` (line 302)

#### Fire section
- `private` `int` `GetFireItemCount()` (line 319) - Always 3 (fuel level, time remaining, fuel types)
- `private` `void` `AnnounceFireItem(int itemIndex)` (line 323) - Index 0: fuel %; Index 1: time remaining; Index 2: enabled fuel count summary

#### Upgrades section
- `private` `void` `AnnounceUpgradeItem(int itemIndex)` (line 359) - Re-fetches upgrade info; announces name + status/requirements/effect
- `private` `string` `GetUpgradeItemName(int itemIndex)` (line 414)

#### Blight section
- `private` `void` `AnnounceBlightItem(int itemIndex)` (line 425)

#### Sacrifice section
- `private` `void` `RefreshSacrificeInfo()` (line 437)
- `private` `void` `AnnounceSacrificeItem(int recipeIndex)` (line 445) - Re-fetches live data; announces good name, level, consumption/min, effect per level
- `private` `void` `AdjustSacrificeLevel(int recipeIndex, int delta)` (line 485) - Clamps to 0..maxLevel; checks afford before enabling from 0
- `private` `string` `GetSacrificeItemName(int recipeIndex)` (line 535)

#### Services section (The Commons)
- `private` `void` `AnnounceServiceItem(int itemIndex)` (line 551) - If locked: shows unlock cost; if unlocked: announces need name + good requirement + stars
- `private` `string` `GetServiceItemName(int itemIndex)` (line 580)

#### Fuel sub-items
- `private` `void` `AnnounceFuelSubItem(int subItemIndex)` (line 594) - Re-fetches fuel state before announcing
- `private` `bool` `ToggleFuel(int subItemIndex)` (line 611)
- `private` `void` `AdjustFuelPriority(int subItemIndex, int delta)` (line 630) - Clamps to 0-3
- `private` `string` `FormatPriority(int priority)` (line 648) - Returns "0 (lowest)" / "3 (highest)" / number
- `private` `string` `GetFuelSubItemName(int subItemIndex)` (line 656)

#### Search name methods
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 667)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 673)
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 700)
