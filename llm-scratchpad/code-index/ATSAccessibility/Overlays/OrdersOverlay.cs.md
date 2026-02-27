# OrdersOverlay.cs
Accessible overlay for the OrdersPopup (order list navigation).
Provides flat list navigation through all orders with front-loaded announcements.

## class OrdersOverlay: MenuBase (line 12)

### Fields
- private enum OrderStatus { ToPick, Completable, Active, Locked, Completed, Failed } (line 14)
- private class OrderItem (line 16)
  - public object State (line 17)
  - public object Model (line 18)
  - public string Label (line 19)
  - public OrderStatus Status (line 20)
  - public bool Tracked (line 21)
- private List<OrderItem> _items (line 25)
- private static readonly List<HelpEntry> _ordersHelpEntries (line 121)

### Properties
- protected override string OverlayName { get; } (line 31)
- protected override string EmptyMessage { get; } (line 33)
- protected override int SearchItemCount { get; } (line 108)

### Methods
- protected override int GetItemCount() (line 35)
- protected override string GetLabel(int index) (line 37)
- protected override void RefreshData() (line 42)
- protected override EnterAction OnEnter(int index) (line 70)
- protected override void OnAction(int index) (line 72)
  ToPick: fires the pick popup; Completable: delivers the order; others: re-announce.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 110)
  T key triggers ToggleTracking.
- protected override EscapeAction OnEscape() (line 119)
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 124)
- protected override void OnClosed() (line 126)
- public void RefreshAfterPick() (line 138)
  Called when the OrderPickPopup closes; re-reads order list and re-announces current item.
- public void RefreshOnNewOrder() (line 154)
  Called by EventAnnouncer when OnOrderStarted fires; re-reads list and announces "Orders updated".
- private void ToggleTracking() (line 168)
  Only allows tracking on Active or Completable orders; announces new tracking state.
- private OrderStatus DetermineStatus(object orderState, object orderModel) (line 193)
- private string BuildLabel(object orderState, object orderModel, OrderStatus status) (line 209)
  Dispatches to specialized label builders per status.
- private string BuildLockedLabel(object orderState, object orderModel) (line 238)
  Shows prerequisite name or countdown timer; falls back to "Locked".
- private string BuildActiveLabel(string name, object orderState, object orderModel) (line 254)
  Assembles label with optional time-to-fail, objectives, and rewards.
- private string BuildRewardText(object orderState, object orderModel) (line 283)
