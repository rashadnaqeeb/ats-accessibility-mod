# OrderPickOverlay.cs
Accessible overlay for the OrderPickPopup (order pick option selection).
Provides flat list navigation through pick options with objectives and rewards.

## class OrderPickOverlay: MenuBase (line 12)

### Fields
- private class PickItem (line 13)
  - public object PickState (line 14)
  - public object OrderModel (line 15)
  - public bool Failed (line 16)
  - public string Label (line 17)
- private object _popup (line 21)
- private object _orderState (line 22)
- private List<PickItem> _items (line 23)
- private static readonly List<HelpEntry> _orderPickHelpEntries (line 115)

### Properties
- protected override string OverlayName { get; } (line 29)
- protected override string EmptyMessage { get; } (line 30)
- protected override int SearchItemCount { get; } (line 102)

### Methods
- protected override int GetItemCount() (line 32)
- protected override string GetLabel(int index) (line 34)
- protected override void RefreshData() (line 40)
  Reads the order from the popup, then iterates its picks; filters out picks with null OrderModel.
- protected override EnterAction OnEnter(int index) (line 72)
- protected override void OnAction(int index) (line 74)
  Fails fast for expired picks; calls OrdersReflection.PickOrder then hides the popup on success.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 104)
  S key triggers AnnounceStoredAmounts.
- protected override EscapeAction OnEscape() (line 113)
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 118)
- protected override void StorePopup(object popup) (line 120)
- protected override string GetOpenAnnouncement() (line 124)
  Skips to first non-failed item and sets CurrentIndex to it before announcing.
- protected override void OnClosed() (line 136)
- private void AnnounceStoredAmounts() (line 146)
  Reads current stored amounts for the selected pick option and announces them; triggered by S key.
- private int GetFirstNonFailedIndex() (line 170)
- private string BuildPickLabel(object pickState, object orderModel, bool failed) (line 177)
  Assembles full pick announcement: name, timed indicator, objectives, rewards, and warnings.
