# InstitutionNavigator.cs
Navigator for Institution buildings (Tavern, Temple, etc.).
Institutions extend ProductionBuilding and have workers.
Provides Status (toggle), Services (recipes), Storage, and Workers sections.

## class InstitutionNavigator: BuildingSectionNavigator (line 12)

### Fields (private enum SectionType, line 17)
- `Status`
- `Effects`
- `Services`
- `Storage`
- `Workers`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 30)
- `private` `SectionType[]` `_sectionTypes` (line 31)
- `private` `bool` `_isSleeping` (line 32)
- `private` `bool` `_canSleep` (line 33)
- `private` `List<ServiceInfo>` `_services` (line 36)
- `private` `List<(string goodName, string displayName, int amount)>` `_storageGoods` (line 39)
- `private` `int` `_effectCount` (line 42)

### Fields (private struct ServiceInfo, line 48)
- `int` `RecipeIndex`
- `string` `ServedNeedName`
- `bool` `ConsumesGood`
- `string` `CurrentGoodName`
- `int` `AvailableGoodsCount`

### Properties
- `protected override` `string` `NavigatorName` (line 60) - Returns `"InstitutionNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 62)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 66) - Status: 0; Services/Storage: min 1 for empty message; Effects: effect count
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 88) - Services: sub-items only if consumes good and >1 option; Workers: races; Upgrades: perks
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 114) - Status: announces "Active" or "Paused"
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 125)
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 148)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 154) - Services: "Option N: goodName"; Workers: delegate; Upgrades: delegate
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 166) - Services: `ChangeInstitutionIngredient` and refresh; Workers: assign and return to Level 1; Upgrades: purchase
- `protected override` `bool` `ToggleBuildingSleep()` (line 192) - Refreshes worker IDs after waking
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 213) - Status section: `ToggleBuildingSleep`
- `protected override` `void` `RefreshData()` (line 220)
- `protected override` `void` `ClearData()` (line 232)
- `private` `void` `RefreshServiceData()` (line 245)
- `private` `void` `RefreshStorageData()` (line 261) - Sorted alphabetically by display name
- `private` `void` `BuildSections()` (line 274) - Conditionally includes Effects, Workers, Upgrades
- `private` `void` `AnnounceEffectItem(int itemIndex)` (line 316) - Announces effect name, activation requirement (active / requires N workers), and description
- `private` `void` `AnnounceServiceItem(int itemIndex)` (line 336) - Announces need name + current good (or "Free service")
- `private` `void` `AnnounceStorageItem(int itemIndex)` (line 359) - Re-fetches storage before announcing
