# RewardsPanel.cs
F3 Rewards panel for quick access to pending rewards.
Always shows all three reward categories (Blueprints, Cornerstones, Newcomers).
Available rewards open the game's popup when selected.
Unavailable rewards show when they will next be available.

## class RewardsPanel: MenuBase (line 14)

### Fields
- private List<RewardItem> _items (line 27)
- private bool _closingForPopup (line 28)
  When true, suppresses "Closed" speech on close.
- private static readonly string[] _searchNames (line 113)
  Short names for search: { "Blueprints", "Cornerstones", "Newcomers" }

### Properties
- protected override string OverlayName { get; } (line 51)
- protected override string EmptyMessage { get; } (line 52)

### Methods
- public void Toggle() (line 38)
  Opens, or closes with click sound if already open. Called from SettlementKeyHandler on F3.
- protected override int GetItemCount() (line 54)
- protected override string GetLabel(int index) (line 56)
- protected override void RefreshData() (line 61)
  Delegates to RefreshItems().
- protected override EnterAction OnEnter(int index) (line 63)
- protected override void OnAction(int index) (line 69)
  Delegates to ActivateSelected().
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 71)
  F1/F2 close and pass through. F3 closes (toggle). LeftArrow consumed (flat list, no drill-out).
- protected override EscapeAction OnEscape() (line 89)
- protected override string GetOpenAnnouncement() (line 94)
  Returns first item's label directly (skips "Rewards." prefix).
- protected override void OnOpened() (line 99)
- protected override void OnClosed() (line 103)
- protected override string GetSearchName(int index) (line 115)
  Returns short name (e.g., "Blueprints"), not the full label with availability info.
- private void RefreshItems() (line 123)
  Builds three RewardItems: checks availability via RewardsReflection; unavailable items get a label with next-available timing (reputation threshold, season/year, or game time).
- private void ActivateSelected() (line 162)
  Re-announces unavailable items. For available items, calls the appropriate RewardsReflection.Open*Popup(), sets _closingForPopup, and closes self.
