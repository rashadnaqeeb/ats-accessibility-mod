# MenuHub.cs
Menu Hub for quick access to game popups.
Opened with F2 from the settlement map.
Isolated in a single file for easy removal if needed.

## class MenuHub: MenuBase (line 12)

### Fields
- private static readonly string[] _menuLabels (line 13)
  Static array: { "Recipes", "Orders", "Trade Routes", "Payments", "Consumption Control", "Trends", "Trader" }
- private bool _closingForPopup (line 24)
  When true, suppresses "Closed" speech on close (popup will announce itself).

### Properties
- protected override string OverlayName { get; } (line 30)
- protected override string EmptyMessage { get; } (line 31)

### Methods
- protected override int GetItemCount() (line 33)
- protected override string GetLabel(int index) (line 35)
  Appends ", locked" to Trade Routes (index 2) or Consumption Control (index 4) if not unlocked.
- protected override string GetSearchName(int index) (line 48)
  Returns plain label without lock suffix (for search matching).
- protected override void RefreshData() (line 53)
- protected override EnterAction OnEnter(int index) (line 55)
- protected override void OnAction(int index) (line 57)
  Delegates to OpenSelectedMenu(index).
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 62)
  F1/F3 close self and pass through for other panel to open. F2 closes self (toggle).
- protected override EscapeAction OnEscape() (line 76)
- protected override void OnOpened() (line 81)
- protected override void OnClosed() (line 85)
- public void Toggle() (line 101)
  Opens, or closes with click sound if already open.
- private void OpenSelectedMenu(int index) (line 115)
  Opens the game popup for the given index. Handles lock checks for Trade Routes and Consumption Control. Sets _closingForPopup and closes self on success. Announces failure with specific messages (e.g., "Trader unavailable. Build a Trading Post first").
