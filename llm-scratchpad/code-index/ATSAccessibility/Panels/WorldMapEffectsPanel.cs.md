# WorldMapEffectsPanel.cs
Virtual speech-only panel for navigating world map tile effects.
Shows biome name/description and all effects with descriptions.
Not an IKeyHandler - called by WorldMapNavigator via ProcessKeyEvent().

## class WorldMapEffectsPanel: MenuBase (line 13)

### Fields
- private List<(string name, string description)> _items (line 14)
- private Vector3Int _tilePos (line 15)
  The tile position currently displayed; used for toggle detection and data loading.

### Properties
- protected override string OverlayName { get; } (line 61)
- protected override string EmptyMessage { get; } (line 62)

### Methods
- public bool ProcessKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers = default) (line 24)
  Bridge method for WorldMapNavigator; returns false immediately if not open.
- public void Open(Vector3Int tilePos) (line 36)
  Custom open: toggles closed if same tile already shown, says "Unexplored" and skips if tile unrevealed, otherwise closes any existing panel and calls base Open() for the new tile.
- protected override int GetItemCount() (line 64)
- protected override string GetLabel(int index) (line 66)
  Returns just the effect name (description is announced separately in AnnounceCurrentItem).
- protected override void RefreshData() (line 71)
  Delegates to RefreshItems().
- protected override EnterAction OnEnter(int index) (line 73)
  Always None (read-only panel).
- protected override EscapeAction OnEscape() (line 79)
- protected override string GetOpenAnnouncement() (line 81)
  Returns null so OnOpened() can announce the first item with its description.
- protected override void OnOpened() (line 88)
  If empty, immediately closes. Otherwise calls AnnounceCurrentItem() directly.
- protected override void OnClosed() (line 96)
- protected override void AnnounceCurrentItem() (line 102)
  Announces "{name}. {description}" or just "{name}" if no description.
- private void RefreshItems() (line 123)
  Adds biome as first item, then all field effects with descriptions from WorldMapReflection.
