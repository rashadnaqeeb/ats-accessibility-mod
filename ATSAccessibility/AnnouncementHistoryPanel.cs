using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Stores recent announcements and provides a panel to review them.
    /// Opened with Alt+N during settlement gameplay.
    /// </summary>
    public class AnnouncementHistoryPanel : IKeyHandler
    {
        private const int MAX_HISTORY = 10;

        private struct HistoryEntry
        {
            public string Message;
            public Vector2Int? Location;
            public HistoryEntry(string message, Vector2Int? location) { Message = message; Location = location; }
        }

        private static readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private static readonly object _lock = new object();

        private int _currentIndex = 0;
        private bool _isOpen = false;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        private readonly MapNavigator _mapNavigator;

        public AnnouncementHistoryPanel(MapNavigator mapNavigator)
        {
            _mapNavigator = mapNavigator;
        }

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Whether the history panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Add a message to the history.
        /// Called by EventAnnouncer when an announcement is made.
        /// </summary>
        public static void AddMessage(string message, Vector2Int? location = null)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (_lock)
            {
                // Add to the beginning (most recent first)
                _history.Insert(0, new HistoryEntry(message, location));

                // Trim to max size
                while (_history.Count > MAX_HISTORY)
                {
                    _history.RemoveAt(_history.Count - 1);
                }
            }
        }

        /// <summary>
        /// Clear all history.
        /// </summary>
        public static void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
        }

        /// <summary>
        /// Process a key event (IKeyHandler).
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            _search.ClearOnLevelChangeKey(keyCode);

            if (_search.IsSearchActive)
            {
                switch (keyCode)
                {
                    case KeyCode.UpArrow:
                        _search.NavigateResults(-1);
                        return true;
                    case KeyCode.DownArrow:
                        _search.NavigateResults(1);
                        return true;
                    case KeyCode.Home:
                        _search.JumpToFirstResult();
                        return true;
                    case KeyCode.End:
                        _search.JumpToLastResult();
                        return true;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        // Apply selection, clear search, then fall through to normal Enter
                        {
                            int idx = _search.SelectedOriginalIndex;
                            if (idx >= 0) _currentIndex = idx;
                        }
                        _search.Clear();
                        break;  // Fall through to main switch for normal Enter handling
                    case KeyCode.Escape:
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                }
            }

            // Panel is open - handle navigation
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Home:
                    _currentIndex = 0;
                    AnnounceCurrentItem(includeHeader: false);
                    return true;

                case KeyCode.End:
                    lock (_lock)
                    {
                        _currentIndex = _history.Count > 0 ? _history.Count - 1 : 0;
                    }
                    AnnounceCurrentItem(includeHeader: false);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    GoToEventLocation();
                    return true;

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.Escape:
                    Close();
                    return true;

                case KeyCode.N:
                    Close();
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume other keys while panel is open
                    return true;
            }
        }

        /// <summary>
        /// Open the history panel.
        /// </summary>
        public void Open()
        {
            lock (_lock)
            {
                if (_history.Count == 0)
                {
                    Speech.Say("No messages");
                    return;
                }
            }

            _isOpen = true;
            _currentIndex = 0;
            _search.Clear();
            AnnounceCurrentItem(includeHeader: true);
        }

        /// <summary>
        /// Close the history panel.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            _search.Clear();
            InputBlocker.BlockCancelOnce = true;  // Prevent game from opening pause menu
            Speech.Say("Notifications closed");
        }

        private void Navigate(int direction)
        {
            lock (_lock)
            {
                if (_history.Count == 0) return;
                _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _history.Count);
            }
            AnnounceCurrentItem(includeHeader: false);
        }

        private void AnnounceCurrentItem(bool includeHeader)
        {
            lock (_lock)
            {
                if (_history.Count == 0)
                {
                    Speech.Say("No messages");
                    return;
                }

                string message = _history[_currentIndex].Message;
                if (includeHeader)
                    Speech.Say($"Notifications. {message}");
                else
                    Speech.Say(message);
            }
        }

        private void GoToEventLocation()
        {
            Vector2Int? location;
            lock (_lock)
            {
                if (_history.Count == 0) return;
                location = _history[_currentIndex].Location;
            }

            if (!location.HasValue)
            {
                Speech.Say("No location");
                return;
            }

            var pos = location.Value;
            _isOpen = false;
            _search.Clear();
            _mapNavigator.SetCursorPosition(pos.x, pos.y);
            _mapNavigator.MoveCursor(0, 0);
        }

        /// <summary>
        /// Jump to the most recent event that has a location.
        /// Called via Shift+N from SettlementKeyHandler.
        /// </summary>
        public void JumpToLatestEventLocation()
        {
            string message = null;
            Vector2Int? location = null;

            lock (_lock)
            {
                for (int i = 0; i < _history.Count; i++)
                {
                    if (_history[i].Location.HasValue)
                    {
                        message = _history[i].Message;
                        location = _history[i].Location;
                        break;
                    }
                }
            }

            if (!location.HasValue)
            {
                Speech.Say("No event locations");
                return;
            }

            var pos = location.Value;
            _isOpen = false;
            _search.Clear();
            _mapNavigator.SetCursorPosition(pos.x, pos.y);
            _mapNavigator.MoveCursor(0, 0);  // Announces tile
            Speech.Say(message, interrupt: false);  // Queue after tile announcement
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle a search key (A-Z) for type-ahead navigation.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            lock (_lock)
            {
                if (_history.Count == 0) return;

                _search.AddChar(c);
                _search.Search(_history.Count, i => _history[i].Message, i => {
                    int save = _currentIndex;
                    _currentIndex = i;
                    AnnounceCurrentItem(includeHeader: false);
                    _currentIndex = save;
                });
            }
        }

        /// <summary>
        /// Handle backspace key to remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                _search.Clear();
                Speech.Say("Search cleared");
                return;
            }

            lock (_lock)
            {
                _search.Search(_history.Count, i => _history[i].Message, i => {
                    int save = _currentIndex;
                    _currentIndex = i;
                    AnnounceCurrentItem(includeHeader: false);
                    _currentIndex = save;
                });
            }
        }
    }
}
