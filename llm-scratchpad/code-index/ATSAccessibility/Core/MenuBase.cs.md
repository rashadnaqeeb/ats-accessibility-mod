# MenuBase.cs

Abstract base class for keyboard-navigable menus and overlays. Provides level-based navigation
(Up/Down/Enter/Escape/Left/Right), type-ahead search via ISearchable, and a ProcessKey flow
that subclasses customize through abstract and virtual methods.

Navigation levels use a flat index array (_indices[level]). Level 0 is always the root.
Subclasses define what each level means.

## class MenuBase: ISearchable, IKeyHandler, IHelpProvider (abstract) (line 15)

### enum EnterAction (nested, protected) (line 20)
- DrillDown (line 22) - Drill down to next navigation level.
- Action (line 24) - Perform an action (toggle, activate, etc.).
- None (line 26) - Do nothing.

### enum EscapeAction (nested, protected) (line 29)
- GoBack (line 31) - Go back one level.
- Close (line 33) - Close the menu.
- PassThrough (line 35) - Pass Escape through to the game.

### Events
- public static event System.Action OnAnyMenuOpened (line 43)
  - Fired when any MenuBase opens. Used by exclusive modes to auto-close.

### Fields
- protected int[] _indices = new int[8] (line 54)
  - Per-level navigation indices (up to 8 levels deep).
- protected readonly TypeAheadSearch _search (line 55)
- private bool _isOpen (line 56)
- private bool _suspended (line 57)
- private int _level (line 58)
- private static readonly List<HelpEntry> _menuBaseHelpEntries (line 427)
  - Default help entries: "+/-" = "Adjust value", "Shift+/-" = "Larger increment".

### Properties
- protected int Level => _level (line 64)
- protected int CurrentIndex { get; set; } (line 66)
  - Reads/writes _indices[_level].
- public bool IsOpen => _isOpen (line 71)
- public bool IsSuspended => _suspended (line 72)
- public virtual bool IsActive => IsOpen && !IsSuspended (line 73)
- protected int ItemCount => GetItemCount() (line 74)
- protected virtual int SearchItemCount => GetItemCount() (line 196)
  - Number of searchable items. Override to disable search at certain levels.
- protected virtual int SearchCurrentIndex => CurrentIndex (line 199)
  - Current search index. Override for custom search cursor.
- protected static IReadOnlyList<HelpEntry> MenuBaseHelpEntries => _menuBaseHelpEntries (line 433)
  - Standard MenuBase help entries. Subclasses can call this to include base entries.

### Abstract Members
- protected abstract string OverlayName { get; } (line 81)
  - Name for logging and open announcement.
- protected abstract string EmptyMessage { get; } (line 84)
  - Message when no items at open.
- protected abstract int GetItemCount() (line 87)
  - Count of items at the current navigation level.
- protected abstract string GetLabel(int index) (line 90)
  - Label for the item at the given index at the current level. Used for search and default announce.
- protected abstract void RefreshData() (line 93)
  - Populate data from game state.
- protected abstract EnterAction OnEnter(int index) (line 96)
  - What Enter does at the current level for the given index.

### Virtual Methods — Actions
- protected virtual void OnAction(int index) (line 103)
  - Perform action at current item (Enter at leaf, or Space default). Default: no-op.
- protected virtual void OnSpace(int index) => OnAction(index) (line 106)
  - Space key handler. Defaults to OnAction.
- protected virtual void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) (line 109)
  - +/- key handler. dir is +1 or -1. Default: no-op.

### Virtual Methods — Level Transitions
- protected virtual void OnDrillDown(int index) (line 116)
  - Prepare next level's data before drilling down. Default: no-op.
- protected virtual void OnGoBack() (line 119)
  - Clean up current level when going back. Default: no-op.
- protected virtual EscapeAction OnEscape() (line 122)
  - Escape behavior. Default: GoBack if level > 0, else PassThrough.
- protected virtual bool CanDrillDown(int index) (line 130)
  - Whether Right arrow can drill down at the given index. Default checks OnEnter. Override for Pattern B (Enter=action, Right=drill).

### Virtual Methods — Special Keys
- protected virtual bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 142)
  - Per-overlay custom key handling. Called before search and standard navigation. Return true if handled, false to pass through, null to continue to base processing.

### Virtual Methods — Lifecycle Hooks
- protected virtual void StorePopup(object popup) (line 151)
  - Save popup reference. Called before Open() lifecycle.
- protected virtual void OnOpened() (line 155)
  - Extra setup after open (after RefreshData and open announcement). Use Speech.Say(msg, false) here to queue speech after the announcement.
- protected virtual void OnClosed() (line 158)
  - Extra teardown on close.
- protected virtual string GetOpenAnnouncement() (line 161)
  - Opening speech. Default: OverlayName + first label.

### Virtual Methods — Announcements & Search
- protected virtual void AnnounceCurrentItem() (line 177)
  - Announce the item at CurrentIndex. Override for custom formatting.
- protected virtual string GetSearchName(int index) (line 187)
  - Search text for the given index. Defaults to GetLabel. Override to customize search text.

### Programmatic Level Control
- protected void SetLevel(int level) (line 212)
  - Set level directly. No callbacks, no announce. For subclasses that force level changes after actions. Does not clear search.

### Lifecycle Methods
- public void Open() (line 221)
  - Open the menu. Guards re-open. Fires OnAnyMenuOpened, resets indices and search, calls RefreshData, speaks announcement, calls OnOpened.
- protected void OpenSilently() (line 229)
  - Open without firing OnAnyMenuOpened. Used by HelpOverlay to avoid cancelling active modes.
- private void OpenCore() (line 237)
  - Shared body for Open() and OpenSilently().
- public void Open(object popup) (line 255)
  - Open with a popup reference. Calls StorePopup then Open().
- public void Close() (line 261)
  - Close the menu. Guards not open. Clears suspended state, calls OnClosed, clears search.
- public void Suspend() (line 273)
  - Suspend input processing (e.g., when a sub-overlay opens).
- public void Resume() (line 278)
  - Resume input processing and re-announce current item.

### Navigation Methods
- protected void Navigate(int direction) (line 288)
  - Navigate by direction (wrapping via NavigationUtils.WrapIndex). Clears search on navigation. Announces result.
- protected void NavigateTo(int index) (line 297)
  - Navigate to a specific index (clamped). Announces result.
- private void DrillDown() (line 305)
  - Calls OnDrillDown, increments level, resets child index, clears search, announces.
- private void GoBack() (line 315)
  - Calls OnGoBack, decrements level, clears search, announces.

### Key Processing
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 331)
  - Main key processing. Flow: HandleSpecialKey → _search.HandleKey → standard navigation switch. Up/Down navigate, Home/End jump to bounds, Enter dispatches OnEnter result, Right drills if CanDrillDown, Left goes back if level > 0, Space calls OnSpace, +/- call OnAdjust, Escape dispatches OnEscape result, default consumes.

### IHelpProvider Implementation
- public virtual HelpBehavior HelpBehavior => HelpBehavior.Terminator (line 435)
- public virtual string HelpContextName => OverlayName (line 436)
- public virtual IReadOnlyList<HelpEntry> GetHelpEntries() => _menuBaseHelpEntries (line 437)
- public virtual IReadOnlyList<string> GetPassthroughKeys() => null (line 438)

### ISearchable Explicit Interface
- int ISearchable.SearchItemCount => SearchItemCount (line 444)
- int ISearchable.SearchCurrentIndex => SearchCurrentIndex (line 446)
- string ISearchable.GetSearchLabel(int index) => GetSearchName(index) (line 448)
- protected virtual void SearchMoveTo(int index) => NavigateTo(index) (line 451)
  - Move to index on search match. Override for custom search navigation (e.g., flat cross-category).
- void ISearchable.SearchMoveTo(int index) => SearchMoveTo(index) (line 453)

### Static Methods
- public static void ClearStaticState() (line 46)
  - Clear static event to prevent stale subscribers after mod reload.
