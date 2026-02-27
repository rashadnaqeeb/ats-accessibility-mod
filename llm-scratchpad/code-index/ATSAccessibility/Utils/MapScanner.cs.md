# MapScanner.cs
3-level hierarchical scanner for quick map object location.
Categories: Glades / Resources / Buildings
Groups: Types within category (e.g., "Clay Deposit", "Small Warehouse")
Items: Individual instances within a group

## class MapScanner (line 16)

### Nested Types

#### class ItemGroup (line 24)
A group of items of the same type (e.g., all "Clay Deposits").
- public string TypeName (line 25)
- public string BuildingTypeName (line 26)
  Runtime type name for subcategory lookup (e.g., "Hearth", "Workshop").
- public List<ScannedItem> Items (line 27)
  Sorted by distance at scan time.
- public ItemGroup(string typeName) (line 29)

#### class ScannedItem (line 38)
A single scanned item with position and distance.
- public Vector2Int Position (line 39)
- public int Distance (line 40)
  Manhattan distance from cursor at scan time.
- public ScannedItem(Vector2Int position, int distance) (line 42)

#### enum ScanCategory (private, line 52)
- Glades = 0
- Resources = 1
- Buildings = 2
- SearchResults = 3

### Fields
- private ScanCategory _currentCategory (line 57)
- private int _currentGroupIndex (line 60)
- private int _currentItemIndex (line 61)
- private List<ItemGroup> _cachedGroups (line 62)
- private int _currentSubcategoryIndex (line 65)
- private Dictionary<int, List<ItemGroup>> _cachedBuildingsBySubcategory (line 66)
- private Dictionary<int, List<ItemGroup>> _cachedResourcesBySubcategory (line 67)
- private readonly MapNavigator _mapNavigator (line 69)
- private List<ItemGroup> _searchResultGroups (line 72)
- private ScanCategory _categoryBeforeSearch (line 73)
- private int _scanOriginX (line 81)
- private int _scanOriginY (line 82)
- private bool _hasScanOrigin (line 83)
- private int _lastAutoMoveX (line 84)
- private int _lastAutoMoveY (line 85)
- private static readonly string[] SubcategoryNames (line 91)
  11 entries: "All", "Essential", "Gathering", "Production", "Trade", "Housing and Services", "Special Buildings", "Blight Fighting", "Decorations", "Ruins", "Roads".
- private static readonly Dictionary<string, int> BuildingTypeToSubcategory (line 99)
  Maps runtime type names (e.g., "Hearth", "Workshop") to subcategory indices 1-10.
- private static readonly string[] ResourceSubcategoryNames (line 124)
  5 entries: "All", "Natural Resources", "Extracted Resources", "Nodes Small", "Nodes Large".
- private bool _reflectionCached (line 134)
- private HashSet<Vector2Int> _unrevealedGladeTiles (line 137)
  Rebuilt each scan; used for O(1) lookup to skip items in unrevealed glades.

### Properties
- public bool IsInSearchResults { get; } (line 78)

### Methods
- public MapScanner(MapNavigator mapNavigator) (line 143)
- private static int CompareGroupsByDistance(ItemGroup a, ItemGroup b) (line 151)
- private static int CompareItemsByDistance(ScannedItem a, ScannedItem b) (line 157)
- private static int CalculateDistance(Vector2Int pos, int cursorX, int cursorY) (line 164)
  Chebyshev distance (max of |dx|, |dy|).
- private static List<ItemGroup> FinalizeGroups(Dictionary<string, ItemGroup> groups) (line 171)
  Sorts items within each group by distance.
- public void ChangeCategory(int direction) (line 186)
  Full rescan (Ctrl+PageUp/Down). Resets subcategory and group/item indices. For Buildings/Resources, finds first non-empty subcategory. Announces "Category, SubcategoryName, GroupName, N of Total".
- public void ChangeGroup(int direction) (line 284)
  Always rescans for fresh data (PageUp/Down). Navigates within current subcategory. SearchResults mode skips rescan.
- public void ChangeItem(int direction) (line 353)
  No rescan (Alt+PageUp/Down). Navigates items within current group.
- public void AnnounceDistance() (line 374)
  Announces distance/direction from current cursor to current item (read-only).
- public void AnnounceDistanceFrom(int fromX, int fromY, string suffix) (line 382)
  Announces distance from arbitrary position with optional suffix (e.g., "of bookmark").
- public void ReadCurrentItemInfo() (line 415)
  Delegates to TileInfoReader.ReadCurrentTile for the current item's position.
- public void MoveCursorToItem() (line 435)
  Moves cursor to current item, announces "moved to TypeName, coords".
- private void ScanCurrentCategory() (line 462)
- private void BuildUnrevealedGladeTilesMap() (line 491)
  Builds _unrevealedGladeTiles HashSet for O(1) glade membership tests during scan.
- private List<ItemGroup> ScanGlades() (line 518)
  Groups unrevealed glades by danger level and contents. Also scans location markers and seal candidate glades.
- private void ScanSealCandidateGlades(List<(object glade, Vector2Int firstField)> unrevealedGlades, Dictionary<string, ItemGroup> groups, int cursorX, int cursorY) (line 592)
  Triangulates seal location from discovered guiding stone bearings. A glade is a candidate if all rays pass within tolerance.
- private void ScanLocationMarkerType(Dictionary<string, ItemGroup> groups, ...) (line 685)
- private void ScanLocationMarkers(Dictionary<string, ItemGroup> groups, int cursorX, int cursorY) (line 709)
- private void ScanRewardChaseRelics(Dictionary<string, ItemGroup> groups, int cursorX, int cursorY) (line 744)
- private List<ItemGroup> ScanResources() (line 802)
- private List<ItemGroup> ScanBuildings() (line 959)
- private void AnnounceCurrentItem() (line 1000)
- private void AutoMoveCursorSilent() (line 1018)
  Moves cursor to current item without announcing if ScannerAutoMove plugin setting is on.
- private void UpdateScanOrigin() (line 1033)
  Updates scan origin when cursor has moved since last auto-move (to prevent distance drift during navigation).
- private void GetScanOrigin(out int x, out int y) (line 1047)
- private void AnnounceEmpty() (line 1057)
- private string GetDirection(int dx, int dy) (line 1068)
- private void EnsureReflectionCache() (line 1090)
- private string GetGladeDangerLevel(object glade) (line 1098)
- private bool IsInsideUnrevealedGlade(Vector2Int pos) (line 1110)
- private bool IsInsideUnrevealedGlade(int x, int y) (line 1120)
- private string GetBuildingTypeName(object building) (line 1131)
- private int GetBuildingSubcategoryIndex(object building) (line 1152)
- private void ScanBuildingsWithSubcategories() (line 1167)
  Populates _cachedBuildingsBySubcategory. Subcategory 0 ("All") excludes Decorations and Roads.
- private void ScanResourcesWithSubcategories() (line 1248)
  Populates _cachedResourcesBySubcategory.
- public void ChangeSubcategory(int direction) (line 1458)
  Cycles subcategory within Buildings or Resources category (Ctrl+Alt+PageUp/Down). No-op for other categories.
- private void ChangeSubcategoryInternal(int direction, string[] subcategoryNames, Dictionary<int, List<ItemGroup>> cache, string emptyMessage) (line 1483)
- public void CommitSearch(string query) (line 1519)
  Searches all categories for items matching query. Switches to SearchResults mode with results sorted by distance.
- public void ClearSearchResults() (line 1590)
  Restores category and state from before search.
