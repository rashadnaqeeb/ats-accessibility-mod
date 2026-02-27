# HelpOverlay.cs
Read-only overlay that displays context-sensitive help entries.
Opened via F12, shows all key bindings available in the current context.

## class HelpOverlay: MenuBase (line 10)

### Fields
- private string _contextName (line 11)
  Displayed as the overlay name; set by ShowHelp().
- private List<HelpEntry> _entries (line 12)

### Properties
- protected override string OverlayName { get; } (line 14)
  Returns _contextName (dynamic, set per context).
- protected override string EmptyMessage { get; } (line 15)

### Methods
- protected override int GetItemCount() (line 17)
- protected override string GetLabel(int index) (line 19)
  Returns "{KeyName}: {Description}".
- protected override void RefreshData() (line 24)
  No-op; data is set externally via ShowHelp().
- protected override EnterAction OnEnter(int index) (line 28)
  Always None (read-only).
- protected override void OnClosed() (line 30)
- protected override EscapeAction OnEscape() (line 34)
  Blocks cancel (to prevent Escape from reaching game and closing underlying panels), then closes.
- public void ShowHelp(string contextName, List<HelpEntry> entries) (line 45)
  Sets context name and entries, then calls OpenSilently() to avoid firing OnAnyMenuOpened (which would cancel active modes like build mode, tree marking, or move mode).
