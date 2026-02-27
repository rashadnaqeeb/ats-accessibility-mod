# CapitalUpgradeOverlay.cs
Accessible overlay for the CapitalUpgradePopup (Buy Upgrades screen).
Three-level navigation: structures -> upgrades -> rewards.
Pattern B at Level 1: Enter=buy (Action), Right=view rewards (CanDrillDown).

## class CapitalUpgradeOverlay: MenuBase (line 14)

### Fields
- private static readonly Regex NumberPattern (line 15)
  Compiled regex matching numeric values with optional sign and percent (e.g. "+3%") for reward total computation.
- private List<CapitalUpgradeReflection.StructureInfo> _structures (line 18)
- private List<CapitalUpgradeReflection.UpgradeInfo> _upgrades (line 19)
- private List<CapitalUpgradeReflection.RewardInfo> _rewards (line 20)

### Properties
- protected override string OverlayName { get; } (line 26)
- protected override string EmptyMessage { get; } (line 27)

### Methods
- protected override int GetItemCount() (line 29)
- protected override string GetLabel(int index) (line 38)
- protected override void AnnounceCurrentItem() (line 51)
  Delegates to level-specific announce helpers rather than the default label announcement.
- protected override void RefreshData() (line 65)
  Only loads structures; upgrades and rewards are loaded lazily on navigation.
- protected override EnterAction OnEnter(int index) (line 69)
  Level 0: loads upgrades and returns DrillDown. Level 1: returns Action (buy).
- protected override void OnAction(int index) (line 89)
- protected override bool CanDrillDown(int index) (line 94)
  Level 0: loads upgrades for Right arrow. Level 1: loads rewards for Right arrow.
  Data loading here mirrors OnEnter to support both Enter and Right drill-down paths.
- protected override void OnDrillDown(int index) (line 123)
  No-op: data already loaded by OnEnter or CanDrillDown before this is called.
- protected override void OnGoBack() (line 127)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 134)
  On Escape: closes overlay and passes through to game.
- protected override string GetOpenAnnouncement() (line 143)
  Returns null; custom announcement is handled in OnOpened instead.
- protected override void OnOpened() (line 148)
- protected override void OnClosed() (line 152)
- protected override string GetSearchName(int index) (line 162)
- private void AnnounceStructure() (line 179)
  Announces structure name with unlocked/total upgrade count.
- private void AnnounceUpgrade() (line 186)
  Announces upgrade name, level number, and status (unlocked/price/can't afford/level required/locked).
- private void AnnounceReward() (line 215)
  Appends a cumulative total suffix for the first reward of a stacking upgrade.
- private void ActivateUpgrade() (line 232)
  Purchases the current upgrade if Buyable; reports failure reason for other statuses.
- private void RefreshCurrentUpgrades() (line 271)
  Re-fetches structures and upgrades after a purchase to reflect new unlock state; clamps index.
- private string GetRewardTotalSuffix(string description, int level) (line 295)
  Extracts the per-level numeric value from a reward description and computes the cumulative total for the given upgrade level.
