# IKeyHandler.cs

Interface for components that handle keyboard input. Handlers are processed in priority order;
the first active handler that returns true from ProcessKey() consumes the key event.

## interface IKeyHandler (line 9)

### Properties
- bool IsActive { get; } (line 13)
  - Whether this handler is currently active and should receive input.

### Methods
- bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 21)
  - Process a key event. Returns true if handled, false to pass to next handler.
