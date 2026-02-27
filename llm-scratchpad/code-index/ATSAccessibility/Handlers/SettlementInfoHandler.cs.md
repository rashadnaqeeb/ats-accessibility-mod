# SettlementInfoHandler.cs
High-priority handler for settlement info hotkeys (Alt+S, Alt+V, Alt+O).
Registered above menus/overlays so these work even inside popups
without interfering with typeahead search.

## class SettlementInfoHandler: IKeyHandler, IHelpProvider (line 13)

### Fields
- private static readonly List<HelpEntry> _helpEntries (line 18)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 24)
- public string HelpContextName { get; } (line 25)
- public bool IsActive { get; } (line 29)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 26)
- public IReadOnlyList<string> GetPassthroughKeys() (line 27)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 31)
  Handles Alt+S (quick summary), Alt+V (species resolve), Alt+O (tracked orders). Returns false for all non-Alt keys, allowing lower-priority handlers to process them.
- public static void AnnounceTrackedOrders() (line 49)
  Filters orders to those that are tracked, started, picked, and not completed or failed, then announces each as "name: objectives" or just "name". Static so it can be called from SettlementKeyHandler (O key) as well.
