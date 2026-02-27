# GameResultOverlay.cs
Accessible overlay for the GameResultPopup (victory/defeat screen).
Level 0 = top-level items, Level 1 = sub-items within Section items.

## class GameResultOverlay: MenuBase (line 14)

### Fields
- private enum ItemType { ReadOnly, Section, Button } (line 15)
- private class TopLevelItem (line 17)
  - public ItemType Type (line 18)
  - public string Label (line 19)
  - public Action OnActivate (line 20)
  - public List<string> SubItems (line 21)
- private object _popup (line 25)
- private List<TopLevelItem> _items (line 26)

### Properties
- public override bool IsActive { get; } (line 28)
  Custom activation: also requires the popup MonoBehaviour's GameObject to be active.

### Methods
- private bool IsPopupVisible() (line 30)
- protected override string OverlayName { get; } (line 41)
- protected override string EmptyMessage { get; } (line 42)
- protected override int GetItemCount() (line 44)
  Returns top-level item count at Level 0, or sub-item count of the current Section at Level 1.
- protected override string GetLabel(int index) (line 55)
- protected override void RefreshData() (line 72)
  Builds items in order: summary, progression, score (non-tutorial), tutorial rewards (tutorial only), world event, action buttons.
- protected override EnterAction OnEnter(int index) (line 94)
  DrillDown for Section items with sub-items; Action otherwise; None at Level 1.
- protected override void OnAction(int index) (line 108)
  ReadOnly re-announces; Section with no sub-items says "Empty"; Button invokes OnActivate.
- protected override EscapeAction OnEscape() (line 134)
- protected override int SearchItemCount { get; } (line 142)
- protected override void StorePopup(object popup) (line 144)
- protected override string GetOpenAnnouncement() (line 148)
  Returns the first item's label (the victory/defeat header) as the open announcement.
- protected override void OnClosed() (line 154)
- private void AddSummaryItem() (line 163)
- private void AddProgressionSection() (line 180)
- private void AddScoreSection() (line 226)
- private void AddTutorialRewardsSection() (line 251)
- private void AddWorldEventSection() (line 270)
- private void AddActionButtons() (line 301)
  Always adds "Return to world map"; conditionally adds "Continue playing".
