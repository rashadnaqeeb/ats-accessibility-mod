# CornerstoneLimitOverlay.cs
Accessible overlay for the CornerstonesLimitPickPopup (choose-one-to-remove sub-popup).
Provides flat list navigation through active cornerstones with selection and confirm/cancel.

## class CornerstoneLimitOverlay: MenuBase (line 12)

### Fields
- private class NavItem (line 13)
  Fields: object Model, string Label, string SearchName
- private object _popup (line 20)
- private int _selectedIndex (line 21)
  Tracks which cornerstone is currently marked for removal; -1 means none selected.
- private List<NavItem> _items (line 22)

### Properties
- protected override string OverlayName { get; } (line 28)
- protected override string EmptyMessage { get; } (line 29)

### Methods
- protected override int GetItemCount() (line 31)
- protected override string GetLabel(int index) (line 33)
  Appends ", selected" suffix to the currently selected item.
- protected override string GetSearchName(int index) (line 43)
- protected override void RefreshData() (line 49)
- protected override EnterAction OnEnter(int index) (line 66)
- protected override void OnAction(int index) (line 68)
  Calls ConfirmRemoval(); Enter confirms the currently selected cornerstone.
- protected override void OnSpace(int index) (line 72)
  Calls ToggleSelection() to mark/unmark a cornerstone for removal.
- protected override EscapeAction OnEscape() (line 76)
- protected override void StorePopup(object popup) (line 78)
- protected override void OnClosed() (line 82)
  Calls CancelLimitPopup if _popup is non-null (i.e., closed without confirming).
- protected override void OnOpened() (line 92)
- private void ToggleSelection() (line 100)
  Toggles _selectedIndex between CurrentIndex and -1; announces selected/deselected state.
- private void ConfirmRemoval() (line 112)
  Clears _popup before calling RemoveAndConfirm to prevent OnClosed from cancelling the action.
