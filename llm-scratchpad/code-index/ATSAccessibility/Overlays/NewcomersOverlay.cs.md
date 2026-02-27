# NewcomersOverlay.cs
Accessible overlay for the NewcomersPopup (newcomers arrival group selection).
Provides flat list navigation: dialogue, group 1, group 2.

## class NewcomersOverlay: MenuBase (line 12)

### Fields
- private enum ItemType { Dialogue, Group } (line 14)
- private class NavItem (line 16)
  - public ItemType Type (line 17)
  - public object GroupData (line 18)
  - public string Label (line 19)
- private object _popup (line 23)
- private List<NavItem> _items (line 24)

### Properties
- protected override string OverlayName { get; } (line 30)
- protected override string EmptyMessage { get; } (line 31)
- protected override int SearchItemCount { get; } (line 85)

### Methods
- protected override int GetItemCount() (line 33)
- protected override string GetLabel(int index) (line 35)
- protected override void RefreshData() (line 41)
  Always inserts a hardcoded NPC dialogue string first, then appends formatted group options from reflection.
- protected override EnterAction OnEnter(int index) (line 69)
- protected override void OnAction(int index) (line 71)
  Dialogue items re-announce; Group items call ActivateGroup.
- protected override EscapeAction OnEscape() (line 88)
- protected override void StorePopup(object popup) (line 90)
- protected override void OnClosed() (line 94)
- private void ActivateGroup(NavItem item) (line 103)
  Calls NewcomersReflection.PickGroup; popup closing triggers OnPopupHidden -> Close().
