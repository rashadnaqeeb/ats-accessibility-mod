# PaymentsOverlay.cs
Accessible overlay for the PaymentsPopup (pending payments/obligations).
Flat list navigation with static header text.

## class PaymentsOverlay: MenuBase (line 12)

### Fields
- private enum ItemType { Header, Payment } (line 13)
- private class NavItem (line 15)
  - public ItemType Type (line 16)
  - public PaymentsReflection.PaymentInfo? Payment (line 17)
  - public string Label (line 18)
- private object _popup (line 22)
- private List<NavItem> _items (line 23)
- private const string HEADER_TEXT (line 26)
  Hardcoded NPC quote from Zhera Mossback.

### Properties
- protected override string OverlayName { get; } (line 35)
- protected override string EmptyMessage { get; } (line 36)
- protected override int SearchItemCount { get; } (line 135)

### Methods
- protected override int GetItemCount() (line 38)
- protected override string GetLabel(int index) (line 40)
- protected override void RefreshData() (line 46)
  Inserts the hardcoded header item first, then appends payment items from reflection.
- protected override EnterAction OnEnter(int index) (line 68)
- protected override void OnAction(int index) (line 70)
  Header and non-payable items re-announce; payable items call PaymentsReflection.Pay, then refresh.
- protected override void OnSpace(int index) (line 105)
  Cycles auto-payment type (None -> Instant -> End -> None) and announces the new label.
- protected override EscapeAction OnEscape() (line 138)
- protected override void StorePopup(object popup) (line 140)
- protected override void OnClosed() (line 144)
- private string BuildPaymentLabel(PaymentsReflection.PaymentInfo payment) (line 153)
  Assembles comma-separated label: type, amount+good, due date, time remaining, auto-payment mode, can/cannot pay.
