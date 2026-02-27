# ConfirmationDialog.cs
Speech-only confirmation dialog handler.
Blocks all input while active, confirms with Enter, cancels with Escape.

## class ConfirmationDialog: IKeyHandler, IHelpProvider (line 13)

### Fields
- private bool _isOpen (line 14)
- private Action _onConfirm (line 15)
- private string _itemName (line 16)
- private static readonly List<HelpEntry> _helpEntries (line 22)
  Empty list; confirmation dialog has no navigable help entries.

### Properties
- public HelpBehavior HelpBehavior { get; } (line 24)
  Returns HelpBehavior.Terminator (stops help chain traversal).
- public string HelpContextName { get; } (line 25)
- public bool IsActive { get; } (line 29)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 26)
- public IReadOnlyList<string> GetPassthroughKeys() (line 27)
- public void Show(string itemName, Action onConfirm, List<(string name, int amount)> refundGoods = null) (line 37)
  Opens the dialog and announces "Destroy {itemName}? [Refund: ...] Enter to confirm, Escape to cancel".
- public void ShowMessage(string message, Action onConfirm) (line 65)
  Opens the dialog with a fully custom message instead of the standard "Destroy X?" format.
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 72)
  Enter/KeypadEnter invokes _onConfirm and closes. Escape announces "Cancelled" and closes. All other keys consumed.
- private void Close() (line 91)
  Sets _isOpen to false, clears callbacks, blocks cancel once.
