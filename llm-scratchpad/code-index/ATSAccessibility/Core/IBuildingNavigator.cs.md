# IBuildingNavigator.cs

Interface for building-specific panel navigators. Each building type can have a specialized
navigator that knows how to read and interact with that building's unique features.

## interface IBuildingNavigator (line 9)

### Methods
- void Open(object building) (line 14)
  - Open the navigator for a specific building. Called when building panel is shown.
- void Close() (line 20)
  - Close the navigator. Called when building panel is hidden.
- bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 27)
  - Process a key event for navigation. Returns true if handled, false to pass through.
