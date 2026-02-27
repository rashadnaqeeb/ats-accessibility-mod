# PoroNavigator.cs
Navigator for Poro buildings (creature care).
Poros extend Building (not ProductionBuilding) and have no workers.
Provides Info, Happiness, Needs, and Product sections.

## class PoroNavigator: BuildingSectionNavigator (line 12)

### Fields (private enum SectionType, line 17)
- `Info`
- `Happiness`
- `Needs`
- `Product`

### Fields
- `private` `string[]` `_sectionNames` (line 28)
- `private` `SectionType[]` `_sectionTypes` (line 29)
- `private` `string` `_buildingName` (line 30)
- `private` `string` `_buildingDescription` (line 31)
- `private` `float` `_happiness` (line 34)
- `private` `float` `_productionProgress` (line 35)
- `private` `List<NeedInfo>` `_needs` (line 38)
- `private` `string` `_productName` (line 41)
- `private` `int` `_productAmount` (line 42)
- `private` `int` `_maxProducts` (line 43)
- `private` `bool` `_canGather` (line 44)

### Fields (private struct NeedInfo, line 50)
- `int` `NeedIndex`
- `string` `NeedName`
- `float` `Level`
- `string` `CurrentGoodName`
- `int` `AvailableGoodsCount`
- `bool` `CanFulfill`

### Properties
- `protected override` `string` `NavigatorName` (line 63) - Returns `"PoroNavigator"`

### Methods
- `protected override` `string` `GetOpenAnnouncement()` (line 65) - Returns "Name: Description" if description present, else just name
- `protected override` `string[]` `GetSections()` (line 71)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 75) - Info: 0; Happiness: 2; Needs: count or 1; Product: 1
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 93) - Needs: Feed action (if can fulfill) + good options (if >1); Product: 1 if can gather
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 114)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 119)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 136) - Needs: "Feed" or "Change to goodName"; Product: "Collect N productName"
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 160) - Needs: fulfill or change good; Product: gather and refresh
- `protected override` `void` `RefreshData()` (line 202)
- `protected override` `void` `ClearData()` (line 214)
- `private` `void` `RefreshHappinessData()` (line 224)
- `private` `void` `RefreshNeedData()` (line 229)
- `private` `void` `RefreshProductData()` (line 246)
- `private` `void` `BuildSections()` (line 253) - Always: Happiness, Needs, Product (no conditional sections)
- `private` `void` `AnnounceHappinessItem(int itemIndex)` (line 277) - Index 0: happiness %; Index 1: production progress %
- `private` `void` `AnnounceNeedItem(int itemIndex)` (line 289) - Announces need name, level %, and current good
- `private` `void` `AnnounceProductItem(int itemIndex)` (line 309) - Announces "productName: N of M ready"; appends "(none ready)" if 0
