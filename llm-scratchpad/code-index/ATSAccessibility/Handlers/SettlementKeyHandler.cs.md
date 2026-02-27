# SettlementKeyHandler.cs
Handles keyboard input for settlement map navigation.
This is the fallback handler when no popups/menus are open during gameplay.

## class SettlementKeyHandler: IKeyHandler, IHelpProvider (line 15)

### Fields
- private readonly MapNavigator _mapNavigator (line 16)
- private readonly MapScanner _mapScanner (line 17)
- private readonly InfoPanelMenu _infoPanelMenu (line 18)
- private readonly MenuHub _menuHub (line 19)
- private readonly RewardsPanel _rewardsPanel (line 20)
- private readonly BuildingMenuPanel _buildingMenuPanel (line 21)
- private readonly MoveModeController _moveModeController (line 22)
- private readonly AnnouncementHistoryPanel _announcementHistoryPanel (line 23)
- private readonly ConfirmationDialog _confirmationDialog (line 24)
- private readonly HarvestMarkHandler _harvestMarkHandler (line 25)
- private bool _hasBookmark (line 27)
- private int _bookmarkX (line 28)
- private int _bookmarkY (line 29)
- private readonly bool[] _numberedBookmarkSet (line 31)
  10-element array (slots 0-9).
- private readonly int[] _numberedBookmarkX (line 32)
- private readonly int[] _numberedBookmarkY (line 33)
- private bool _searchInputActive (line 36)
- private readonly StringBuilder _searchBuffer (line 37)
- private int _workerBuildingIndex (line 40)
  -1 means uninitialized; reset when category changes.
- private int _workerCategoryIndex (line 41)
  0=All, 1=Gathering, 2=Production, 3=Service, 4=Events
- private static readonly string[] WorkerCategories (line 42)
- private static readonly Dictionary<string, int> BuildingTypeToWorkerCategory (line 44)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 135)
- public string HelpContextName { get; } (line 136)
- public bool IsActive { get; } (line 143)

### Methods
- public SettlementKeyHandler(MapNavigator, MapScanner, InfoPanelMenu, MenuHub, RewardsPanel, BuildingMenuPanel, MoveModeController, AnnouncementHistoryPanel, ConfirmationDialog, HarvestMarkHandler) (line 53)
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 137)
- public IReadOnlyList<string> GetPassthroughKeys() (line 138)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 148)
  Large switch covering the full settlement hotkey set. Delegates to _searchInputActive path during scanner text search. Handles: arrows (move/skip), K (position/coordinates toggle), Space (pause or Shift+Space destroy/remove), Keypad1-4/Alpha1-4 (game speed), Alpha0-9 (bookmarks with modifiers), S/V/T (stats), PageUp/Down (scanner with Ctrl/Shift/Alt/plain modifiers), Home/End (scanner jump/distance), I/E/R/D (tile info, entrance, rotate, range/blight), B (bookmark set/jump/direction), O (orders), P (rainpunk), W (workers), +/- (priority/worker adjust), Enter (activate building/harvest mode/lake retrieve), F1-F3/Tab (open panels), N (history/latest event), H (reset cursor), Backspace (tree mark toggle), M (move mode), F (scanner search), Period/Comma (cycle worker buildings/category). Consumes all unmatched keys by default.
- private bool HandleSearchInput(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 594)
  Text-entry loop for scanner search: Enter commits, Escape cancels, Backspace deletes, Space appends space, alphanumeric characters append. All keys consumed.
- private static char? KeyCodeToChar(KeyCode keyCode) (line 632)
  Converts A-Z, Alpha0-9, Keypad0-9 to lowercase char; returns null for anything else.
- private void SetNumberedBookmark(int slot) (line 642)
- private void JumpToNumberedBookmark(int slot) (line 649)
  Jumps to saved position and re-announces tile (calls MoveCursor(0,0) to trigger announcement at current position without actually moving).
- private void AnnounceDirectionTo(int targetX, int targetY) (line 658)
  Announces Chebyshev distance and 8-directional name toward a target tile.
- private void CycleWorkerCategory(int direction) (line 671)
  Wraps _workerCategoryIndex and resets _workerBuildingIndex to -1.
- private void CycleWorkerBuilding(int direction) (line 677)
  Filters all buildings to production buildings with worker slots, filters by category if non-zero, sorts alphabetically then by position, and wraps through the list. Moves cursor to selected building and announces name + worker summary.
- private void ToggleTreeMark() (line 733)
  Directly marks/unmarks the NaturalResource at the cursor (Backspace key), with "glade edge" warning appended when marking a tree near a glade boundary.
- private void AdjustNodePriority(object node, int delta, bool global) (line 753)
  Clamps new priority to [-5, 5]. If global (Shift held), sets all nodes of same type; otherwise sets just the focused node.
- private void AdjustConstructionPriority(object building, int delta, bool global) (line 774)
  Same clamping logic as AdjustNodePriority but for under-construction buildings.
- private static string FormatNodePriority(int priority) (line 795)
  Returns priority with descriptive label for extremes and default: "-5 (lowest)", "5 (highest)", "0 (default)", or the number as string.
