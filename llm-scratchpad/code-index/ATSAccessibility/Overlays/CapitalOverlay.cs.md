# CapitalOverlay.cs
Accessible overlay for the capital (Smoldering City) screen.
Flat list navigation: Buy Upgrades, Deeds, Game History, Home (if unlocked).

## class CapitalOverlay: MenuBase (line 13)

### Fields
- private List<(string name, Action action)> _items (line 14)

### Properties
- protected override string OverlayName { get; } (line 20)
- protected override string EmptyMessage { get; } (line 21)

### Methods
- protected override int GetItemCount() (line 23)
- protected override string GetLabel(int index) (line 25)
  Appends a lock or "new dialogue" suffix via GetLockSuffix.
- protected override string GetSearchName(int index) (line 33)
- protected override void RefreshData() (line 38)
  Builds fixed list of capital actions; conditionally adds "Home" if unlocked.
- protected override EnterAction OnEnter(int index) (line 52)
- protected override void OnAction(int index) (line 54)
  Checks lock state before suspending and invoking the action lambda.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 72)
  On Escape: closes overlay and returns false to pass through to game to close the capital screen.
- protected override string GetOpenAnnouncement() (line 80)
- protected override void OnClosed() (line 85)
- private bool IsItemLocked(string name) (line 93)
- private string GetLockSuffix(string itemName) (line 102)
  Returns ", locked" for locked items; returns ", new dialogue" for Home when NPC topics are available.
