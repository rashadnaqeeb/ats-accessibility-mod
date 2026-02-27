# CornerstoneOverlay.cs
Accessible overlay for the RewardPickPopup (mid-game cornerstone/perk selection).
Provides flat list navigation through NPC dialogue, cornerstone choices, extend, reroll, and skip.

## class CornerstoneOverlay: MenuBase (line 12)

### Fields
- private enum ItemType (line 14)
  Values: Dialogue, Cornerstone, Extend, Reroll, Skip
- private class NavItem (line 16)
  Fields: ItemType Type, object Model, string Label, string SearchName
- private object _popup (line 24)
- private List<NavItem> _items (line 25)

### Properties
- protected override string OverlayName { get; } (line 31)
- protected override string EmptyMessage { get; } (line 32)

### Methods
- protected override int GetItemCount() (line 34)
- protected override string GetLabel(int index) (line 36)
- protected override string GetSearchName(int index) (line 42)
  Returns SearchName only for Cornerstone items; other item types return null.
- protected override void RefreshData() (line 48)
  Builds: NPC dialogue, cornerstone options (with ethereal flag), extend (if available), reroll (if remaining), skip.
- protected override EnterAction OnEnter(int index) (line 119)
- protected override void OnAction(int index) (line 121)
  Dispatches by ItemType; Dialogue re-announces, others call the corresponding activate method.
- protected override EscapeAction OnEscape() (line 145)
  PassThrough: game closes the popup, which triggers OnPopupHidden -> Close().
- protected override void StorePopup(object popup) (line 147)
- protected override void OnClosed() (line 151)
- public void RefreshAfterLimit() (line 164)
  Called by external code after CornerstoneLimitOverlay closes; refreshes data and moves to first cornerstone.
- private void ActivateCornerstone(NavItem item) (line 178)
  After picking, checks if new options loaded (next tier) and stays open, otherwise waits for popup hide.
- private void ActivateExtend() (line 200)
  Compares cornerstone count before and after to detect if extend added a new option.
- private void ActivateReroll() (line 228)
- private void ActivateSkip() (line 241)
  After skipping, checks if new options loaded (next pick) and stays open, otherwise waits for popup hide.
- private int GetFirstCornerstoneIndex() (line 266)
- private int GetLastCornerstoneIndex() (line 272)
- private int CountCornerstones() (line 278)
