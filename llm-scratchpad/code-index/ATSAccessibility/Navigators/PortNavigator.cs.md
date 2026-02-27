# PortNavigator.cs
Navigator for Port buildings (expeditions).
Provides phase-based navigation:
- Phase 1 (Planning): Pick goods, adjust level, pick category, confirm
- Phase 2 (Collecting): View delivery progress, cancel
- Phase 3 (In Progress): View progress/time (read-only)
- Phase 4 (Rewards): View rewards, accept

## class PortNavigator: BuildingSectionNavigator (line 16)

### Fields (private enum SectionType, line 21)
- `Level`
- `Workers`
- `Goods`
- `Category`
- `RewardsPreview`
- `Confirm`
- `GoodsProgress`
- `Cancel`
- `Status`
- `Rewards`
- `AcceptRewards`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 40)
- `private` `SectionType[]` `_sectionTypes` (line 41)
- `private` `bool` `_wasDecisionMade` (line 44)
- `private` `bool` `_expeditionStarted` (line 45)
- `private` `bool` `_rewardsWaiting` (line 46)
- `private` `int` `_expeditionLevel` (line 49)
- `private` `int` `_maxLevel` (line 50)
- `private` `float` `_duration` (line 51)
- `private` `GoodsSetData[]` `_striderSets` (line 63)
- `private` `int` `_striderSetCount` (line 64)
- `private` `GoodsSetData[]` `_crewSets` (line 65)
- `private` `int` `_crewSetCount` (line 66)
- `private` `DeliveryItem[]` `_deliveryItems` (line 75)
- `private` `int` `_deliveryItemCount` (line 76)
- `private` `List<string>` `_categoryDisplayNames` (line 79)
- `private` `List<string>` `_categoryInternalNames` (line 80)
- `private` `string` `_pickedCategory` (line 81)
- `private` `bool` `_hasBlueprintReward` (line 82)
- `private` `List<(string rarity, int chance)>` `_rewardChances` (line 85)
- `private` `string` `_blueprintReward` (line 88)
- `private` `string` `_perkReward` (line 89)
- `private` `float` `_progress` (line 92)
- `private` `float` `_timeLeft` (line 93)

### Fields (private struct GoodsSetData, line 54)
- `int` `alternativeCount`
- `string[]` `goodDisplayNames`
- `string[]` `goodNames`
- `int[]` `goodAmounts`
- `int` `pickedIndex`

### Fields (private struct DeliveryItem, line 68)
- `string` `displayName`
- `string` `name`
- `int` `delivered`
- `int` `needed`

### Properties
- `protected override` `string` `NavigatorName` (line 98) - Returns `"PortNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 100)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 104) - Level/Confirm/Cancel/Status/AcceptRewards: 0 (section-level actions); Goods: strider+crew set count
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 138)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 154) - Level: level N of M + duration; Status: progress + time; Confirm: checks if blocked by category; Cancel/AcceptRewards: fixed strings
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 186)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 215)
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 232) - Confirm: lock decision; Cancel: cancel decision; AcceptRewards: accept
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 248) - Category: set picked category
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 260)
- `protected override` `void` `AdjustSectionValue(int sectionIndex, int delta, KeyboardManager.KeyModifiers modifiers)` (line 280) - Level section: changes expedition level via `PortChangeLevel`
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 303)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 309)
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 333)
- `protected override` `void` `RefreshData()` (line 353) - Detects phase via flags; conditionally refreshes goods/categories/rewards/delivery
- `protected override` `void` `ClearData()` (line 387)
- `private` `string` `GetPhaseString()` (line 405)
- `private` `void` `BuildSections()` (line 416) - Builds phase-appropriate section list; Upgrades always appended

#### Goods data
- `private` `void` `RefreshGoodsSets()` (line 481)
- `private` `GoodsSetData` `FetchGoodsSetData(bool isStrider, int setIndex)` (line 505)
- `private` `void` `RefreshDeliveryItems()` (line 542) - Gathers picked strider and crew goods with delivery progress
- `private` `void` `RefreshCategories()` (line 591)
- `private` `void` `RefreshRewardChances()` (line 608)

#### Goods section (Phase 1)
- `private` `bool` `ResolveGoodsIndex(int itemIndex, out GoodsSetData[] sets, out int localIndex, out bool isStrider)` (line 620) - Resolves combined item index (strider first, then crew) to the correct set array and local index
- `private` `int` `GetGoodsSubItemCount(int itemIndex)` (line 642) - Returns alternative count only if > 1
- `private` `void` `AnnounceGoodsItemCombined(int itemIndex)` (line 650)
- `private` `void` `AnnounceGoodsSubItemCombined(int itemIndex, int subItemIndex)` (line 672)
- `private` `bool` `PerformGoodsSubItemAction(int itemIndex, int subItemIndex)` (line 690) - Picks a good alternative and returns to Level 1
- `private` `string` `GetGoodsItemName(int itemIndex)` (line 713)
- `private` `string` `GetGoodsSubItemName(int itemIndex, int subItemIndex)` (line 723)

#### Category section (Phase 1)
- `private` `void` `AnnounceCategoryItem(int itemIndex)` (line 736)
- `private` `bool` `PerformCategoryAction(int itemIndex)` (line 753)

#### Rewards preview section (Phase 1)
- `private` `void` `AnnounceRewardsPreviewItem(int itemIndex)` (line 774)

#### Goods progress section (Phase 2)
- `private` `void` `AnnounceGoodsProgressItem(int itemIndex)` (line 786)

#### Status section (Phase 3)
- `private` `string` `FormatProgress()` (line 798)
- `private` `string` `FormatTimeLeft()` (line 803)

#### Rewards section (Phase 4)
- `private` `int` `GetRewardsItemCount()` (line 821) - Min 1 (for "No rewards" message)
- `private` `void` `AnnounceRewardsItem(int itemIndex)` (line 828) - Lists blueprint and perk rewards

#### Actions
- `private` `bool` `PerformConfirmAction()` (line 854) - Checks for unpicked category block; calls `PortLockDecision`; resets to section 0 on success
- `private` `bool` `PerformCancelAction()` (line 876) - Calls `PortCancelDecision`; resets to section 0 on success
- `private` `bool` `PerformAcceptRewardsAction()` (line 892) - Calls `PortAcceptRewards`; resets to section 0 on success

#### Helpers
- `private` `string` `FormatDuration(float seconds)` (line 912)
- `private` `string` `GetBuildingDisplayName(string buildingName)` (line 924) - Looks up localized name via reflection; falls back to `CleanupName`
- `private` `string` `GetEffectDisplayName(string effectName)` (line 943)
- `private` `string` `CleanupName(string name)` (line 948) - Removes brackets, inserts spaces before uppercase after lowercase, replaces underscores
