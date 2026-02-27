# BuildModeController.cs
Controls building placement mode: rotation, placement, and removal.
Works with MapNavigator for cursor position.

## class BuildModeController: IKeyHandler, IHelpProvider (line 13)

### Fields
- private bool _isActive (line 14)
- private object _selectedBuildingModel (line 15)
- private string _selectedBuildingName (line 16)
- private int _rotation (line 17)
  Values 0-3 mapping to North/West/South/East.
- private readonly MapNavigator _mapNavigator (line 20)
- private readonly BuildingMenuPanel _buildingMenuPanel (line 23)
- private static readonly List<HelpEntry> _helpEntries (line 29)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 41)
- public string HelpContextName { get; } (line 42)
- public bool IsActive { get; } (line 49)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 43)
- public IReadOnlyList<string> GetPassthroughKeys() (line 44)
- public BuildModeController(MapNavigator mapNavigator, BuildingMenuPanel buildingMenuPanel) (line 51)
  Subscribes to MenuBase.OnAnyMenuOpened to auto-exit build mode when a menu is opened.
- public void EnterBuildMode(object buildingModel, string buildingName) (line 60)
  Sets active state, stores model/name/rotation, announces building name and footprint extension.
- public void ExitBuildMode(bool queue = false) (line 84)
  Clears state, plays panel-hide sound, blocks cancel-key-once, and announces "Exited build mode". `queue` controls whether the speech interrupts or is queued.
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 102)
  Handles R (rotate), Space (place/remove), Tab (return to menu), Enter (place+exit), Escape (exit), E (entrance preview), D (range preview), M (consumed to prevent move-mode activation). Returns false for unhandled keys.
- private void RotateBuilding(bool clockwise) (line 176)
  Increments/decrements _rotation modulo 4 (clockwise increments by 3 to step backward through 0→3→2→1→0), then announces direction and footprint extension.
- private string GetExtensionAnnouncement(int east, int north) (line 203)
  Returns "1 tile" for 1x1 footprint, otherwise "extends N east, N north".
- private void PlaceBuilding() (line 224)
  Creates a temporary building instance, optionally removes springs from grid (for extractors), checks CanPlaceBuilding, finalizes placement, then returns springs. Announces result and exits build mode if max count reached.
- private void RemoveBuildingAtCursor() (line 287)
  Removes an unfinished (under-construction) building at the cursor position with a refund. Refuses to remove completed buildings.
- private void ReturnToMenu() (line 322)
  Clears active state and selected model without a full exit announcement, then toggles the BuildingMenuPanel.
- private string GetCardinalDirection(int rotation) (line 334)
- private bool CanPlaceAtCursor() (line 348)
  Creates a temporary building, sets its position, checks CanPlaceBuilding, then removes the temporary building. Returns false without attempting if max count already reached.
