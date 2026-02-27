# WorldMapInfoHandler.cs
High-priority handler for world map info hotkeys (Alt+L, Alt+R, Alt+S, Alt+T).
Registered above menus/overlays so these work even inside popups
without interfering with typeahead search.

## class WorldMapInfoHandler: IKeyHandler, IHelpProvider (line 13)

### Fields
- private static readonly List<HelpEntry> _helpEntries (line 18)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 25)
- public string HelpContextName { get; } (line 26)
- public bool IsActive { get; } (line 30)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 27)
- public IReadOnlyList<string> GetPassthroughKeys() (line 28)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 32)
  Handles Alt+L (level info), Alt+R (meta resources), Alt+S (seal info), Alt+T (cycle info). Returns false for all non-Alt keys, allowing lower-priority handlers to process them.
