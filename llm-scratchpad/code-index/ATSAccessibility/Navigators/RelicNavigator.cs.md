# RelicNavigator.cs
Navigator for Relic buildings (glade events).
Provides phase-based navigation with name/description header at top:
- Phase A (not started): Decisions, Choose Requirements, Effects, Workers, Preview Rewards, Start Investigation
- Phase B (in progress): Status (in progress), Workers, Requirements, Effects, Rewards, Cancel Investigation
- Phase C (finished): Status (resolved), Workers, Storage

## class RelicNavigator: BuildingSectionNavigator (line 14)

### Fields (private enum SectionType, line 19)
- `Info`
- `Decisions`
- `Requirements`
- `Effects`
- `Rewards`
- `Status`
- `Workers`
- `Storage`
- `Upgrades`
- `Cancel`

### Fields
- `private` `string[]` `_sectionNames` (line 36)
- `private` `SectionType[]` `_sectionTypes` (line 37)
- `private` `string` `_buildingName` (line 38)
- `private` `string` `_buildingDescription` (line 39)
- `private` `string` `_threatLevel` (line 40)
- `private` `bool` `_investigationStarted` (line 43)
- `private` `bool` `_investigationFinished` (line 44)
- `private` `float` `_progress` (line 45)
- `private` `float` `_timeLeft` (line 46)
- `private` `int` `_decisionCount` (line 49)
- `private` `bool` `_hasMultipleDecisions` (line 50)
- `private` `int` `_selectedDecisionIndex` (line 51)
- `private` `GoodsSetData[]` `_goodsSets` (line 61)
- `private` `int` `_goodsSetCount` (line 62)
- `private` `BuildingReflection.RelicEffectInfo[]` `_workingEffects` (line 65)
- `private` `BuildingReflection.RelicEffectInfo[]` `_activeEffects` (line 66)
- `private` `BuildingReflection.RelicEffectInfo[]` `_dynamicEffects` (line 67) - Current tier effects
- `private` `BuildingReflection.RelicEffectInfo[]` `_nextTierEffects` (line 68) - Next tier effects (preview)
- `private` `bool` `_areEffectsPermanent` (line 69)
- `private` `bool` `_hasDynamicEffects` (line 72)
- `private` `int` `_currentEffectTier` (line 73)
- `private` `int` `_totalEffectTiers` (line 74)
- `private` `float` `_timeToNextTier` (line 75)
- `private` `bool` `_isLastTierReached` (line 76)
- `private` `BuildingReflection.RelicRewardInfo[]` `_rewards` (line 79)
- `private` `bool` `_hasRewards` (line 80)
- `private` `bool` `_canStart` (line 83)
- `private` `string` `_startBlockingReason` (line 84)
- `private` `bool` `_canCancel` (line 85)
- `private` `bool` `_hasAnyWorkplace` (line 86)
- `private` `List<(string goodName, string displayName, int amount)>` `_storageItems` (line 89)
- `private` `int` `_storageTotalSum` (line 90)

### Fields (private struct GoodsSetData, line 54)
- `int` `alternativeCount`
- `string[]` `goodNames`
- `string[]` `goodDisplayNames`
- `int[]` `goodAmounts`
- `int` `pickedIndex`

### Properties
- `protected override` `string` `NavigatorName` (line 96) - Returns `"RelicNavigator"`

### Methods
- `protected override` `string` `GetOpenAnnouncement()` (line 98) - Returns "Name. Threat level: X. Description"
- `public` `RelicNavigator()` (line 106) - Sets `_workersSection.GetWorkerIdsFunc` to `BuildingReflection.GetRelicWorkerIds`
- `protected override` `string[]` `GetSections()` (line 110)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 114)
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 140) - Requirements: alternative count if > 1; Workers: races; Upgrades: perks
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 162) - Status: calls `RefreshLiveData` then announces phase-appropriate message (resolved/in-progress/start)
- `private` `string` `FormatDynamicEffectTime(float seconds)` (line 202)
- `private` `string` `FormatTimeLeft()` (line 215)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 228)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 257)
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 267) - Decisions: select; Effects: read description; Rewards: no-op
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 283)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 289) - Requirements: pick good; Workers: assign and return to Level 1; Upgrades: purchase
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 306)
- `protected override` `void` `RefreshData()` (line 329)
- `protected override` `void` `ClearData()` (line 369)

#### Section building
- `private` `void` `BuildSections()` (line 398) - Builds phase-appropriate section list; Upgrades always appended

#### Decision-dependent data refresh
- `private` `void` `RefreshDecisionDetails()` (line 491) - Refreshes goods sets, effects (working/active/dynamic/nextTier), and rewards for the safe decision index
- `private` `void` `RefreshGoodsSets(int decisionIndex)` (line 515)
- `private` `void` `RefreshRewards(int decisionIndex)` (line 547)
- `private` `void` `RefreshStatusData()` (line 560)
- `private` `void` `RefreshLiveData()` (line 571) - Refreshes progress, timers, and dynamic effects for live updates while unpaused; called before Status/Effects announcements

#### Decisions section
- `private` `void` `AnnounceDecisionItem(int itemIndex)` (line 588) - Announces label, work time, selected state, requirements summary, effects summary, rewards summary
- `private` `string` `GetDecisionRequirementsSummary(int decisionIndex)` (line 621)
- `private` `string` `GetEffectsSummary()` (line 648)
- `private` `string` `GetDecisionRewardsSummary(int decisionIndex)` (line 661)
- `private` `bool` `PerformDecisionAction(int itemIndex)` (line 671) - Calls `SetRelicDecisionIndex`; refreshes decision details and rebuilds sections

#### Requirements section
- `private` `void` `AnnounceRequirementItem(int itemIndex)` (line 695) - Phase B: shows delivery progress; Phase A: shows requirement + storage amount + alternative count
- `private` `void` `AnnounceRequirementSubItem(int itemIndex, int subItemIndex)` (line 723)
- `private` `bool` `PerformRequirementSubItemAction(int itemIndex, int subItemIndex)` (line 740) - Cannot change during investigation (Phase B); calls `SetRelicPickedGoodIndex`; returns to Level 1

#### Effects section
- `private` `int` `GetEffectsItemCount()` (line 768) - Sum of working + active + dynamic + next tier effect arrays
- `private` `void` `AnnounceEffectItem(int itemIndex)` (line 777) - Calls `RefreshLiveData`; announces effect name, positive/negative, type (during investigation / in N seconds / pending), description
- `private` `bool` `PerformEffectAction(int itemIndex)` (line 802) - Reads full description; returns false if no description
- `private` `BuildingReflection.RelicEffectInfo?` `GetEffectAtIndex(int itemIndex)` (line 813) - Resolves flat index into working/active/dynamic/nextTier arrays in order
- `private` `bool` `IsWorkingEffect(int itemIndex)` (line 839)
- `private` `bool` `IsNextTierEffect(int itemIndex)` (line 844)

#### Rewards section
- `private` `void` `AnnounceRewardItem(int itemIndex)` (line 855)
- `private` `bool` `PerformRewardAction(int itemIndex)` (line 864) - No-op; description shown inline in announcement

#### Status section / actions
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 872) - Cancel section (Phase B): `PerformCancelAction`; Status section (Phase A): start investigation
- `private` `bool` `PerformCancelAction()` (line 916) - Cancels investigation; plays working-effects sound if applicable; resets to section 0
- `private` `int` `GetStatusItemCount()` (line 940) - Always 0 (section-level only)

#### Storage section (Phase C)
- `private` `void` `RefreshStorageData()` (line 949) - Only runs if investigation finished
- `private` `void` `AnnounceStorageItem(int itemIndex)` (line 959) - Re-fetches storage before announcing
