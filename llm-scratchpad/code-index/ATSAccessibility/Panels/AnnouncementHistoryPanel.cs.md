# AnnouncementHistoryPanel.cs
Stores recent announcements and provides a panel to review them.
Opened with Alt+N during settlement gameplay.

## class AnnouncementHistoryPanel: MenuBase (line 12)

### Fields
- private const int MAX_HISTORY (line 13)
- private static readonly List<HistoryEntry> _history (line 21)
- private static readonly object _lock (line 22)
- private readonly MapNavigator _mapNavigator (line 24)
- private bool _suppressCloseAnnouncement (line 25)

### Properties
- protected override string OverlayName { get; } (line 66)
- protected override string EmptyMessage { get; } (line 67)
- protected override int SearchItemCount { get; } (line 126)

### Methods
- public AnnouncementHistoryPanel(MapNavigator mapNavigator) (line 27)
- public static void AddMessage(string message, Vector2Int? location = null) (line 39)
  Adds a message to the front of the history list (most recent first); trims to MAX_HISTORY.
- public static void ClearHistory() (line 56)
- protected override int GetItemCount() (line 69)
- protected override string GetLabel(int index) (line 73)
- protected override void RefreshData() (line 79)
- protected override EnterAction OnEnter(int index) (line 81)
- protected override void OnAction(int index) (line 87)
  Delegates to GoToEventLocation().
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 89)
  N key closes the panel.
- protected override EscapeAction OnEscape() (line 97)
- protected override string GetOpenAnnouncement() (line 99)
- protected override void OnOpened() (line 107)
  If history is empty, immediately closes and suppresses the close announcement.
- protected override void OnClosed() (line 117)
- protected override string GetSearchName(int index) (line 128)
- private void GoToEventLocation() (line 138)
  Closes the panel (suppressing close announcement) and moves the map cursor to the current item's location.
- public void JumpToLatestEventLocation() (line 161)
  Finds the most recent history entry with a location and moves the map cursor there, without opening the panel. Called via Shift+N from SettlementKeyHandler.
