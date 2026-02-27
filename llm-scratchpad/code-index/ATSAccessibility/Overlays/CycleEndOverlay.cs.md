# CycleEndOverlay.cs
Overlay for WorldCycleEndPopup (shown when ending a Blightstorm cycle on world map).
Provides navigation through XP summary and unlocked capital upgrades.
Single-level flat list of strings.

## class CycleEndOverlay: MenuBase (line 14)

### Fields
- private object _popup (line 16)
- private List<string> _items (line 17)

### Properties
- protected override string OverlayName { get; } (line 23)
- protected override string EmptyMessage { get; } (line 24)
- protected override int SearchItemCount { get; } (line 73)

### Methods
- protected override int GetItemCount() (line 26)
- protected override string GetLabel(int index) (line 28)
- protected override void RefreshData() (line 34)
  Builds list with XP summary first, followed by names of upgrades unlocked this cycle.
- protected override EnterAction OnEnter(int index) (line 52)
- protected override void OnAction(int index) (line 54)
  Calls ConfirmCycleEnd() regardless of which item is activated.
- protected override void OnSpace(int index) (line 58)
  Also calls ConfirmCycleEnd().
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 62)
  On Escape: calls CancelAndClose, closes overlay, blocks cancel, returns true (consume).
- protected override void StorePopup(object popup) (line 75)
- protected override string GetOpenAnnouncement() (line 79)
- protected override void OnClosed() (line 85)
- public static bool IsWorldCycleEndPopup(object popup) (line 94)
- private void ConfirmCycleEnd() (line 103)
  Triggers cycle end animation via WorldMapReflection before hiding popup.
- private void CancelAndClose() (line 116)
  Hides popup without triggering the cycle end animation.
- private string GetXpSummary() (line 129)
  Reads current cycle XP and level info; handles max-level case where targetExp is 0.
- private List<string> GetUnlockedUpgradeNames() (line 146)
  Resolves upgrade IDs from WorldMapReflection to display names; checks ironman mode flag.
