# MapNavigator.cs
Handles keyboard-based map navigation in settlement view.
Arrow keys move a virtual cursor on the map grid, announcing tile contents.
Map size is dynamically determined from the game's MapService.

## class MapNavigator (line 15)

### Fields
- private int _cursorX (line 17)
- private int _cursorY (line 18)
- private int _originX (line 22)
- private int _originY (line 23)
- private bool _originSet (line 24)
- private static readonly string[] RotationDirections (line 628)

### Properties
- public int CursorX { get; } (line 30)
- public int CursorY { get; } (line 35)
- public Func<int, int, string> AnnouncementPrefix { get; set; } (line 41)
  Optional callback invoked on each tile announcement to prepend a context prefix (e.g. "selected"). Set to null when no prefix is needed.

### Methods
- private void EnsureCursorInitialized() (line 46)
- private void EnsureOriginSet() (line 55)
  Lazily resolves the Ancient Hearth grid position as the coordinate origin; snaps cursor to hearth on first successful discovery.
- public void MoveCursor(int dx, int dy) (line 71)
  Moves the virtual cursor by a delta, bounds-checks via MapService, fetches the field object once, and reuses it for both tile announcement and camera sync.
- public void SetCursorPosition(int x, int y) (line 98)
  Moves cursor to an absolute position without announcing; camera is synced. Used by scanner End key.
- public void SkipToNextChange(int dx, int dy) (line 114)
  Steps in a direction tile-by-tile until the tile announcement string (excluding villagers) differs from the starting tile, then announces how many tiles were skipped and the new tile. Stays put and announces "no change till edge" if the edge is reached first.
- public void AnnounceCurrentPosition() (line 171)
  Announces cursor position as hearth-relative X, Y coordinates (K key).
- public string GetRelativeCoordinates(int x, int y) (line 187)
  Returns a "relX, relY" string relative to hearth for an arbitrary position, or null if origin not yet known.
- public string GetCoordinateSuffix() (line 196)
  Returns hearth-relative coordinate string only when the AnnounceCoordinates config toggle is on; otherwise null.
- public void ClearCursor() (line 208)
  Resets cursor to uninitialized state (-1,-1) and clears origin. Call on session exit.
- public void ResetCursor() (line 217)
  Moves cursor to Ancient Hearth position, falling back to map center if hearth is not found.
- private void AnnounceTile(object field) (line 233)
  Builds the tile announcement string, prepends AnnouncementPrefix if set, appends coordinate suffix if enabled, and speaks the result.
- private string GetTileAnnouncement(int x, int y, object field, bool includeVillagers = true) (line 250)
  Builds the full announcement string for a tile: unrevealed glade info, object/terrain/passability, and optionally villager counts. The `includeVillagers` flag is false during skip-comparison to avoid the performance cost of iterating all villagers.
- private string GetFieldType(object field) (line 354)
  Wraps MapReflection.GetFieldTypeName and remaps game-internal names ("Grass" → "Fertile Soil", "Sand" → "Soil").
- private bool GetFieldIsTraversable(object field) (line 365)
- private bool GetGladeWasDiscovered(object glade) (line 373)
  Defaults to true (discovered) when glade is null, to avoid hiding content when reflection hasn't cached yet.
- private string GetGladeDangerLevel(object glade) (line 379)
  Maps raw game enum values ("None", "Dangerous", "Forbidden") to human-readable strings.
- private string GetObjectName(object obj) (line 395)
  Reflection-heavy name lookup: tries Model.displayName, Model.label.displayName, Model.name, Name property, DisplayName property, then falls back to type name. Returns null on exception.
- private string GetVillagersOnTile(int x, int y) (line 479)
  Iterates all villagers (dict), matches by floor(position.x/z) to tile coords, groups by race, and returns a summary like "2 humans, 1 beaver". Returns null if none found or on error.
- private void SyncCameraToTile(object field) (line 538)
  Reads the Field's transform via reflection and calls GameReflection.SetCameraTarget for smooth camera follow.
- public bool ActivateBuilding() (line 561)
  Opens the building panel for the building at the cursor, or announces construction progress for unfinished buildings. Returns true if a building was activated (not necessarily panel-opened).
- private void AnnounceConstruction(object building) (line 592)
  Announces construction progress as a percentage, or lists remaining materials if 0%.
- public void AnnounceEntrance() (line 618)
  Delegates to EntranceInfoHelper for the tile at the current cursor position (E key).
- public void RotateBuilding(bool clockwise = true) (line 633)
  Rotates a placed building at the cursor and announces the new cardinal direction. Checks rotatability, movability, and obstruction before rotating.
