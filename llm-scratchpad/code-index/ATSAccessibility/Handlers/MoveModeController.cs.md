# MoveModeController.cs
Controls building move mode: relocating existing buildings.
Works with MapNavigator for cursor position.

## class MoveModeController: IKeyHandler, IHelpProvider (line 12)

### Fields
- private bool _isActive (line 13)
- private object _movingBuilding (line 14)
- private string _buildingName (line 15)
- private Vector2Int _originalPosition (line 16)
- private int _originalRotation (line 17)
- private int _currentRotation (line 18)
- private bool _pricePaid (line 19)
  True when moving cost has been deducted; triggers confirmation prompt before placing, and refund on cancel.
- private bool _awaitingPlaceConfirm (line 20)
  True after a paid-move validity check passes; next Space/Enter commits the move.
- private readonly MapNavigator _mapNavigator (line 23)
- private static readonly List<HelpEntry> _helpEntries (line 29)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 39)
- public string HelpContextName { get; } (line 40)
- public bool IsActive { get; } (line 47)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 41)
- public IReadOnlyList<string> GetPassthroughKeys() (line 42)
- public MoveModeController(MapNavigator mapNavigator) (line 49)
  Subscribes to MenuBase.OnAnyMenuOpened to auto-cancel move mode when a menu is opened.
- public void EnterMoveMode(object building) (line 57)
  Validates movability and affordability, pays moving cost if applicable, stores original position/rotation, lifts the building from the grid, and announces footprint + cost note.
- public void ExitMoveMode(bool cancel) (line 122)
  If cancel: restores original position and rotation, places building back on grid, refunds cost if paid. If not cancel: sets position to cursor, validates placement (returns without exiting if invalid), places on grid. In both cases clears all state.
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 180)
  Handles R (rotate), Space/Enter (place with confirm flow for paid moves), Escape (cancel), D (range preview), E (entrance preview), M/Tab (consumed). Arrow/scanner/info keys pass through to lower handlers.
- private void RotateBuilding(bool clockwise) (line 318)
  Checks model rotatability, increments _currentRotation modulo 4 (clockwise adds 3), applies rotation via GameReflection, announces direction and footprint extension.
- private string GetExtensionAnnouncement(int east, int north) (line 352)
  Returns "1 tile" for 1x1 footprint, otherwise "extends N east, N north".
- private string GetCardinalDirection(int rotation) (line 372)
