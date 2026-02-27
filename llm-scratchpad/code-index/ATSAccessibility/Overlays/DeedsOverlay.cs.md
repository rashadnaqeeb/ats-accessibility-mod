# DeedsOverlay.cs
Accessible overlay for the GoalsPopup (Deeds menu).
Two-level navigation: categories -> goals.
Level 0 = categories, Level 1 = goals within current category.

## class DeedsOverlay: MenuBase (line 14)

### Fields
- private class GoalEntry (line 16)
  Fields: string Name, object State, object Model, bool Completed, bool Rewarded
- private class CategoryEntry (line 25)
  Fields: string Name, List<GoalEntry> Goals
- private bool _captureNextPopup (line 31)
- private object _childPopup (line 32)
- private List<CategoryEntry> _categories (line 35)

### Properties
- public bool ShouldCaptureNextPopup { get; } (line 40)
  One-shot flag: returns true and resets to false; used by PopupRouter to route the reward popup as a child.
- public bool HasChildPopup { get; } (line 47)
- protected override string OverlayName { get; } (line 73)
- protected override string EmptyMessage { get; } (line 74)
- protected override int SearchItemCount { get; } (line 223)
  At Level 1 returns current category goal count; at Level 0 returns flat cross-category total.
- protected override int SearchCurrentIndex { get; } (line 232)

### Methods
- public void SetChildPopup(object popup) (line 57)
- public void ClearChildPopup() (line 65)
- protected override int GetItemCount() (line 76)
- protected override string GetLabel(int index) (line 83)
  Level 1: combines goal name, description, and progress/status; formats "ready to collect" vs progress text.
- protected override void RefreshData() (line 116)
  Groups goals by category, prepends synthetic "Ready to Collect" category for claimable goals, sorts by category order.
- protected override EnterAction OnEnter(int index) (line 174)
  Level 0: DrillDown into category (or announce "No goals"). Level 1: Action to claim.
- protected override void OnAction(int index) (line 188)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 193)
  When _childPopup is set, passes Escape through to game to close the reward popup.
- protected override EscapeAction OnEscape() (line 205)
  PassThrough at both levels (game closes parent popup).
- protected override string GetOpenAnnouncement() (line 207)
- protected override void OnClosed() (line 213)
- protected override string GetSearchName(int index) (line 240)
  Level 1: goal name in current category. Level 0: flat cross-category goal name.
- protected override void SearchMoveTo(int index) (line 249)
  Level 1: normal navigation. Level 0: resolves flat index to (category, item), enters Level 1 directly.
- private void ActivateCurrentItem(int index) (line 267)
  Claims a completed-but-unrewarded goal; sets _captureNextPopup so the reward popup is routed as child.
- private List<GoalEntry> GetCurrentGoals() (line 300)
- private int GetFlatGoalCount() (line 310)
- private string GetFlatGoalName(int flatIndex) (line 320)
- private void ResolveFlatGoalIndex(int flatIndex, out int categoryIndex, out int itemIndex) (line 334)
