# KeyboardManager.cs

Centralized keyboard input handling with handler chain pattern. Handlers are processed in
priority order; the first active handler that returns true from ProcessKey() consumes the key event.

## class KeyboardManager (line 11)

### struct KeyModifiers (nested) (line 15)
Key modifiers state (Ctrl, Alt, Shift).

#### Properties
- public bool Control { get; } (line 16)
- public bool Alt { get; } (line 17)
- public bool Shift { get; } (line 18)

#### Methods
- public KeyModifiers(bool control, bool alt, bool shift) (line 20)

---

### enum NavigationContext (nested) (line 31)
Navigation context for debugging and logging purposes. With the full handler chain, this is purely informational.

- None (line 33)
- Popup (line 34)
- Map (line 35)
- WorldMap (line 36)
- Dialogue (line 37)
- Encyclopedia (line 38)
- Embark (line 39)

---

### Properties
- public NavigationContext CurrentContext { get; private set; } = NavigationContext.None (line 42)

### Fields
- private readonly List<IKeyHandler> _handlers (line 45)
- public IReadOnlyList<IKeyHandler> Handlers => _handlers (line 48)
  - Ordered handler list for help collection.
- private HelpOverlay _helpOverlay (line 51)

### Methods
- public void RegisterHandler(IKeyHandler handler) (line 56)
  - Adds handler to chain if not already registered. Logs registration.
- public void SetHelpOverlay(HelpOverlay overlay) (line 66)
  - Set the help overlay reference for F12 interception.
- public void SetContext(NavigationContext context) (line 73)
  - Set the current navigation context (informational only). Logs transitions.
- public void ProcessKeyEvent(KeyCode keyCode, KeyModifiers modifiers = default) (line 84)
  - Ignores modifier-only keys. Intercepts F12 to toggle help overlay (calls HelpCollector.Collect). Otherwise walks handler chain until a handler returns true.
- private static bool IsModifierKey(KeyCode keyCode) (line 110)
  - Returns true for Left/Right Alt, Control, Shift, Command keys.
