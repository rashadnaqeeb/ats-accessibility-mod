# HarvestMarkHandler.cs
Handles keyboard-based tree marking/unmarking with rectangle and single selection modes.
Active only when in mark/unmark mode (entered via Enter on NaturalResource).

## class HarvestMarkHandler: IKeyHandler, IHelpProvider (line 12)

### Fields
- private Mode _mode (line 17)
  None / Mark / Unmark
- private SelMode _selMode (line 18)
  Rectangle / Single
- private RectPhase _rectPhase (line 19)
  Idle / WaitingForSecond (for two-corner rectangle selection)
- private Vector2Int _firstCorner (line 20)
- private readonly HashSet<Vector2Int> _selectedPositions (line 21)
- private bool _awaitingGladeConfirm (line 22)
  True after CommitSelection detects glade-edge trees; next Enter confirms.
- private readonly MapNavigator _mapNavigator (line 24)
- private static readonly List<HelpEntry> _helpEntries (line 30)
- private static readonly List<string> _passthroughKeys (line 38)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 42)
- public string HelpContextName { get; } (line 43)
- public bool IsActive { get; } (line 47)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 44)
- public IReadOnlyList<string> GetPassthroughKeys() (line 45)
- public HarvestMarkHandler(MapNavigator mapNavigator) (line 49)
  Subscribes to MenuBase.OnAnyMenuOpened to auto-exit mark mode when a menu is opened.
- public void EnterMode(bool isUnmark) (line 58)
  Sets mode and selection state, registers AnnouncementPrefix callback on MapNavigator to annotate tile announcements with "selected" or "first corner".
- private void ExitMode(bool announce = true) (line 70)
  Clears all state, removes MapNavigator's AnnouncementPrefix, and blocks cancel-key-once.
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 81)
  Arrow/scanner/info keys pass through; Space selects, Tab toggles mode, Enter commits (or confirms glade-edge warning), Escape cancels, C selects all marked (unmark mode only), B passes through.
- private void HandleSpace() (line 146)
  Dispatches to HandleSpaceRectangle or HandleSpaceSingle based on current _selMode.
- private void HandleSpaceRectangle() (line 153)
  First Space sets first corner (must be on a valid/marked resource); second Space finds all resources within the bounding rectangle matching mode criteria, adds them to _selectedPositions, and switches to Single mode.
- private void HandleSpaceSingle() (line 214)
  Toggles individual positions in _selectedPositions; validates that the tile has a NaturalResource and (in unmark mode) that it is currently marked.
- private void ToggleSelectionMode() (line 246)
  Switches between Rectangle and Single modes; resets _rectPhase to Idle.
- private void SelectAllMarked() (line 259)
  Adds all currently-marked NaturalResource positions to _selectedPositions (unmark mode only, C key).
- private void CommitSelection() (line 276)
  Checks for glade-edge trees in mark mode and prompts for confirmation if found; otherwise calls DoCommit directly.
- private void DoCommit() (line 301)
  Applies mark/unmark to all _selectedPositions via GameReflection, announces count, then exits mode without announcement.
- private string GetAnnouncementPrefix(int x, int y) (line 325)
  Callback for MapNavigator.AnnouncementPrefix: returns "first corner" if this tile is the first rectangle corner, "selected" if in _selectedPositions, or null.
- private bool IsMarkedAt(Vector2Int pos) (line 337)
  Gets NaturalResource at pos via reflection and checks its marked state.
