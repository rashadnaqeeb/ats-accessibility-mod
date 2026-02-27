# EntranceInfoHelper.cs
Helper class for getting building entrance information.
Used by E key in settlement map, build mode, and move mode.

## class EntranceInfoHelper (line 9)

### Fields
- private static readonly Vector2Int[] ApproachOffsets (line 15)
  4-element array: approach tile offsets indexed by rotation (0=south, 1=east, 2=north, 3=west).
- private static readonly string[] ApproachDirections (line 22)

### Methods
- public static string GetEntranceInfo(int cursorX, int cursorY) (line 27)
  For a placed building on the grid. Returns "At entrance", "No entrance", "No building here", or "Entrance D tile(s) direction, facing side".
- public static string GetEntrancePreview(object building, int cursorX, int cursorY, object buildingModel, int rotation) (line 49)
  For a building being placed or moved (not yet on grid). Uses geometric footprint check instead of GetObjectOn since building is not grid-placed.
- private static bool IsInsideFootprint(int tileX, int tileY, int cursorX, int cursorY, object buildingModel, int rotation) (line 86)
- private static bool TryGetApproachTile(object building, out int approachX, out int approachY, out int rotation) (line 101)
  Handles the edge case where the entrance Transform can be pushed outside the footprint for rotated buildings.
- private static bool IsAtAnyApproachTile(int cursorX, int cursorY) (line 143)
  Checks 4 cardinal neighbors to see if cursor is at any neighbor building's approach tile.
- private static string FormatEntrance(int cursorX, int cursorY, int approachX, int approachY, int rotation) (line 165)
- private static string GetDirection(int dx, int dy) (line 182)
  Returns strict 8-point compass direction (no 2:1 ratio logic, unlike BlightInfoHelper.GetDirection).
