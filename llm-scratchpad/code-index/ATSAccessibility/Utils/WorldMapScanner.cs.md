# WorldMapScanner.cs
Scanner for quick navigation to different types of world map features.
Cycles through types with PageUp/Down, items within type with Alt+PageUp/Down.

## class WorldMapScanner (line 12)

### Nested Types

#### enum ScanType (line 17)
- Settlement = 0 (includes capital and player cities)
- Seal = 1
- RevealedModifier = 2
- UnknownModifier = 3
- RevealedEvent = 4
- UnknownEvent = 5

#### class ScannedItem (line 26)
- public Vector3Int Position (line 27)
- public int Distance (line 28)
- public string Name (line 29)
- public ScannedItem(Vector3Int position, int distance, string name) (line 31)

### Fields
- private ScanType _currentType (line 42)
- private int _currentItemIndex (line 43)
- private List<ScannedItem> _cachedItems (line 44)
- private readonly WorldMapNavigator _navigator (line 45)
- private static readonly Vector3Int[] HexDirections (line 49)
  6 cubic hex directions (NW, NE, E, SE, SW, W). Duplicated from WorldMapNavigator for encapsulation.
- private static readonly string[] DirectionNames (line 59)
- private static int CompareByDistance(ScannedItem a, ScannedItem b) (line 69)

### Methods
- public WorldMapScanner(WorldMapNavigator navigator) (line 77)
- public void ChangeType(int direction) (line 88)
  Cycles scan type (PageUp/Down). Rescans and resets item index on type change.
- public void ChangeItem(int direction) (line 102)
  Cycles items within current type (Alt+PageUp/Down). No rescan.
- public void AnnounceDirection() (line 115)
  Announces distance and direction from current cursor to current item (Home key).
- public void JumpToItem() (line 142)
  Moves cursor to current item's position (End key).
- private void ScanCurrentType() (line 158)
  Iterates WorldMapReflection.GetWorldMapPositions(), collects matching items, sorts by distance.
- private ScannedItem CheckPosition(Vector3Int pos, Vector3Int cursorPos) (line 174)
  Returns a ScannedItem if pos matches current scan type, otherwise null. Capital is only included at position (0,0,0) to avoid duplicates.
- private void AnnounceTypeChange() (line 238)
  Announces "TypeName, ItemName, N of Total".
- private void AnnounceItem() (line 252)
  Announces "ItemName, N of Total".
- private void AnnounceEmpty() (line 265)
- private string GetTypeName(ScanType type) (line 270)
- private int GetHexDistance(Vector3Int from, Vector3Int to) (line 286)
  Chebyshev distance in cubic hex coordinates: max(|dx|, |dy|, |dz|).
- private string GetDirectionTo(Vector3Int from, Vector3Int to) (line 291)
  Returns "north"/"south" for near-axial directions using 2:1 ratio check, then falls back to dot-product with the 6 hex direction vectors.
