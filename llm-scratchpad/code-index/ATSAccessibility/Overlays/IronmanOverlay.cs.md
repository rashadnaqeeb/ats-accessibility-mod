# IronmanOverlay.cs
Accessible overlay for the IronmanUpgradePopup (Queen's Hand Trial upgrades).
Three-level navigation: sections -> items -> rewards.
Sections: Pick Options (3 choices), Core Upgrades, and Unlocked.
Pattern B at Level 1: Enter/Space=buy (Action), Right=view rewards (CanDrillDown).

## class IronmanOverlay: MenuBase (line 14)

### Fields
- private enum SectionType { PickOptions, CoreUpgrades, Unlocked } (line 15)
- private SectionType[] _sections (line 18)
- private string[] _sectionNames (line 19)
- private List<IronmanReflection.UpgradeInfo> _currentItems (line 22)
- private List<IronmanReflection.RewardInfo> _rewards (line 23)

### Properties
- protected override string OverlayName { get; } (line 29)
- protected override string EmptyMessage { get; } (line 30)

### Methods
- protected override int GetItemCount() (line 32)
- protected override string GetLabel(int index) (line 41)
- protected override void AnnounceCurrentItem() (line 55)
  Dispatches to AnnounceSection/AnnounceItem/AnnounceReward based on current level.
- protected override void RefreshData() (line 69)
- protected override EnterAction OnEnter(int index) (line 73)
  At Level 0: loads items for section, returns DrillDown. At Level 1: returns Action (buy).
- protected override void OnAction(int index) (line 93)
- protected override void OnSpace(int index) (line 98)
  Same as OnAction at Level 1 — both trigger ActivateItem (buy).
- protected override bool CanDrillDown(int index) (line 103)
  At Level 0: loads items for Right-arrow drill. At Level 1: loads rewards for Right-arrow drill.
- protected override void OnDrillDown(int index) (line 132)
  No-op: data is already loaded by OnEnter or CanDrillDown before this is called.
- protected override void OnGoBack() (line 136)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 143)
  At Level 0: Escape and LeftArrow both close and pass through to game.
- protected override string GetOpenAnnouncement() (line 156)
  Returns null; custom announcement is done in OnOpened instead.
- protected override void OnOpened() (line 161)
- protected override void OnClosed() (line 165)
- protected override string GetSearchName(int index) (line 176)
- private void BuildSections() (line 194)
  Conditionally includes PickOptions (if picks remain) and Unlocked (if any exist).
- private List<IronmanReflection.UpgradeInfo> LoadItemsForSection(SectionType sectionType) (line 222)
- private void AnnounceOpen() (line 239)
- private void AnnounceSection() (line 252)
- private void AnnounceItem() (line 274)
- private void AnnounceReward() (line 288)
- private void ActivateItem() (line 303)
  Validates affordability then calls IronmanReflection.Pick; on success calls RefreshAfterPurchase.
- private void RefreshAfterPurchase() (line 331)
  Rebuilds sections and re-syncs item list; handles edge cases where the PickOptions section disappears after max picks.
