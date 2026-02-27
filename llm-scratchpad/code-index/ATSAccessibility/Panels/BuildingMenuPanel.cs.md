# BuildingMenuPanel.cs
Virtual speech-only panel for selecting buildings to place.
Two-panel system: left panel has categories, right panel has buildings in category.
Extends MenuBase for level-based navigation with cross-category building navigation.

## class BuildingMenuPanel: MenuBase (line 15)

### Fields
- private List<Category> _categories (line 35)
- private List<(int categoryIndex, int buildingIndex, string name)> _allBuildings (line 38)
  Flat list of all buildings across all categories, used for cross-category search.
- private BuildModeController _buildModeController (line 42)
- private bool _closingForBuild (line 45)
  When true, suppresses "Building menu closed" speech on close.

### Properties
- protected override string OverlayName { get; } (line 74)
- protected override string EmptyMessage { get; } (line 75)
- protected override int SearchItemCount { get; } (line 201)
- protected override int SearchCurrentIndex { get; } (line 203)
  Finds the flat _allBuildings index matching the current category/building indices.

### Methods
- public void SetBuildModeController(BuildModeController controller) (line 54)
- public void Toggle() (line 61)
  Opens the panel, or closes it with a click sound if already open.
- protected override int GetItemCount() (line 77)
  Returns category count at Level 0, building count for current category at Level 1.
- protected override string GetLabel(int index) (line 83)
- protected override void RefreshData() (line 97)
  Delegates to RefreshCategories().
- protected override EnterAction OnEnter(int index) (line 101)
  Level 0: DrillDown if category has buildings, else None. Level 1: Action (select building).
- protected override void OnAction(int index) (line 118)
  Level 1 only: delegates to SelectBuilding().
- protected override bool CanDrillDown(int index) (line 123)
  Level 0: true if category has buildings. Level 1: always false.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 131)
  At Level 1: Up/Down navigate across category boundaries, Home/End jump within category. Escape closes.
- protected override EscapeAction OnEscape() (line 162)
- protected override void AnnounceCurrentItem() (line 167)
  Dispatches to AnnounceCurrentCategory() at Level 0 or AnnounceCurrentBuilding() at Level 1.
- protected override string GetOpenAnnouncement() (line 178)
- protected override void OnOpened() (line 184)
- protected override void OnClosed() (line 188)
- protected override string GetSearchName(int index) (line 214)
  Returns name from flat _allBuildings list.
- protected override void SearchMoveTo(int index) (line 219)
  Sets category and building indices from flat list position, then announces building.
- private void NavigateBuildingAcrossCategories(int direction) (line 235)
  Moves to next/previous building, flowing into the adjacent category when at a boundary. Announces category name on category change.
- private void JumpToBuilding(int index) (line 260)
  Clamps index to valid range and announces the building. Used for Home/End keys.
- private void SelectBuilding() (line 274)
  Checks if building can still be placed (CanConstructBuilding), closes panel, and calls BuildModeController.EnterBuildMode().
- private void RefreshCategories() (line 307)
  Fetches all building categories and models from game settings, filters to active/in-shop/unlocked buildings, groups and sorts, builds flat list.
- private void AnnounceCurrentCategory() (line 396)
  Announces "{CategoryName}: {buildingCount}".
- private void AnnounceCurrentBuilding() (line 409)
  Announces building name, size, costs (with "not enough" annotation), description, and at-maximum status.
- private void AnnounceCategoryAndBuilding() (line 439)
  Like AnnounceCurrentBuilding but prepends the category name. Used when crossing category boundaries.
