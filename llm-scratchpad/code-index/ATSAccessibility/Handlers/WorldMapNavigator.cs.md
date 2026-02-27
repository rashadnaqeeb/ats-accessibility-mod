# WorldMapNavigator.cs
Handles keyboard navigation on the world map hex grid.
Uses arrow keys for navigation with zigzag pattern for up/down.

## class WorldMapNavigator (line 13)

### Fields
- private static readonly Vector3Int[] HexDirections (line 16)
  Cubic coordinate deltas for the 6 hex directions in order: NW, NE, E, SE, SW, W.
- private static readonly string[] DirectionNames (line 26)
- private Vector3Int _cursorPos (line 38)
  Current cursor in cubic coordinates; (0,0,0) is the capital.
- private string _cachedBriefInfo (line 41)
- private WorldMapEffectsPanel _effectsPanel (line 44)
- private TileType _cachedTileType (line 58)

### Properties
- public Vector3Int CursorPosition { get; } (line 63)

### Methods
- public void MoveCursor(int directionIndex) (line 69)
  Moves cursor one hex in the given direction (0-5), bounds-checks, syncs camera, caches tile info, and announces.
- public void MoveArrow(int dx, int dy) (line 91)
  Maps arrow key dx/dy to a hex direction index and delegates to MoveCursor. Up/Down use z-coordinate parity (bitwise AND handles negatives) to produce a consistent zigzag pattern.
- public void SetCursorPosition(Vector3Int pos) (line 124)
  Moves cursor to an absolute cubic position; syncs camera and announces. Used by scanner.
- public void Interact() (line 138)
  Triggers the field click at the current cursor (embark/event activation).
- public void ReadTooltip() (line 146)
  Speaks detailed tooltip content for the current tile; content varies by tile type.
- public void ReadEmbarkAndDistance() (line 154)
  Reads embark status, embark range from last town/capital, and distance+direction to that town (D key).
- public void Reset() (line 192)
  Resets cursor to capital (0,0,0).
- public void OpenEffectsPanel() (line 201)
  Opens the WorldMapEffectsPanel for the current tile. Blocked for capital/city tiles.
- public bool ProcessPanelKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers = default) (line 213)
  Forwards key events to the effects panel. Returns true if the panel consumed the key.
- private void CacheTileInfo() (line 221)
  Computes and caches _cachedTileType and _cachedBriefInfo for the current cursor position. Called once per cursor move to avoid repeated reflection calls during announcement and tooltip building.
- private string BuildTooltip() (line 302)
  Dispatches to the appropriate Build*Tooltip helper based on _cachedTileType.
- private string BuildCityTooltip() (line 334)
- private string BuildSealTooltip() (line 362)
  Shows full seal info (difficulty, fragment requirement, rewards, completion) even for unexplored tiles, since seals are visible through fog.
- private string BuildModifierTooltip() (line 397)
- private string BuildEventTooltip() (line 420)
- private string BuildPlayableFieldTooltip() (line 434)
- private string GetUnpickableReason() (line 467)
  Checks all conditions that prevent embarking (final game played, seal fragments, blightstorm) and returns a comma-joined reasons string. Named "Unpickable" but also used in BuildOutOfReachTooltip.
- private string BuildOutOfReachTooltip() (line 483)
- private void AnnounceTile() (line 498)
  Speaks _cachedBriefInfo.
- private void SyncCameraToTile() (line 506)
  Calls WorldMapReflection.SetWorldCameraTarget for smooth camera follow.
- private int GetHexDistance(Vector3Int from, Vector3Int to) (line 514)
  Chebyshev distance in cubic hex coordinates: max(|dx|, |dy|, |dz|).
- private string GetDirectionTo(Vector3Int from, Vector3Int to) (line 523)
  Returns the closest direction name toward a target. First checks for near-pure north/south (within 2:1 x/y ratio), then falls back to dot-product matching against HexDirections.
