# WorldMapKeyHandler.cs
Handles keyboard input for world map hex grid navigation.
This is the fallback handler when no popups/menus are open on the world map.

## class WorldMapKeyHandler: IKeyHandler, IHelpProvider (line 13)

### Fields
- private readonly WorldMapNavigator _worldMapNavigator (line 14)
- private readonly WorldMapScanner _worldMapScanner (line 15)
- private WorldTutorialsOverlay _tutorialsOverlay (line 16)
- private static readonly List<HelpEntry> _helpEntries (line 34)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 50)
- public string HelpContextName { get; } (line 51)
- public bool IsActive { get; } (line 58)

### Methods
- public WorldMapKeyHandler(WorldMapNavigator worldMapNavigator, WorldMapScanner worldMapScanner) (line 18)
- public void SetTutorialsOverlay(WorldTutorialsOverlay overlay) (line 26)
  Setter for the tutorials overlay reference; called after construction since the overlay is created after this handler.
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 52)
- public IReadOnlyList<string> GetPassthroughKeys() (line 53)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 63)
  First checks if the effects panel should handle the key. Then handles: arrow navigation (zigzag), scanner controls (PageUp/Down with Alt/plain modifiers, Home, End), Enter (embark), I (tooltip), D (embark+distance), M (effects panel), L/R/S/T (meta stats), E (cycle end popup), F1 (tutorials). Consumes all other keys by default.
