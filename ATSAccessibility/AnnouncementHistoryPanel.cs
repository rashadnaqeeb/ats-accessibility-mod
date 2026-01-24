using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Stores recent announcements and provides a panel to review them.
    /// Opened with Alt+H during settlement gameplay.
    /// </summary>
    public class AnnouncementHistoryPanel : IKeyHandler
    {
        private const int MAX_HISTORY = 10;

        private static readonly List<string> _history = new List<string>();
        private static readonly object _lock = new object();

        private int _currentIndex = 0;
        private bool _isOpen = false;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

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
        public static void AddMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            lock (_lock)
            {
                // Add to the beginning (most recent first)
                _history.Insert(0, message);

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
            // Alt+H to open (when not already open)
            if (!_isOpen && keyCode == KeyCode.H && modifiers.Alt && !modifiers.Control && !modifiers.Shift)
            {
                // Only open if we're in a game
                if (GameReflection.GetIsGameActive())
                {
                    Open();
                    return true;
                }
                return false;
            }

            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

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

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    Close();
                    return true;

                case KeyCode.H:
                    Close();
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search (except H which closes panel)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z && keyCode != KeyCode.H)
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
            Debug.Log("[ATSAccessibility] Announcement history panel opened");
        }

        /// <summary>
        /// Close the history panel.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            _search.Clear();
            InputBlocker.BlockCancelOnce = true;  // Prevent game from opening pause menu
            Speech.Say("History closed");
            Debug.Log("[ATSAccessibility] Announcement history panel closed");
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

                string message = _history[_currentIndex];
                Speech.Say(message);
            }
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

                // Search for first matching item
                string prefix = _search.Buffer.ToLowerInvariant();
                for (int i = 0; i < _history.Count; i++)
                {
                    if (_history[i].ToLowerInvariant().StartsWith(prefix))
                    {
                        _currentIndex = i;
                        AnnounceCurrentItem(includeHeader: false);
                        Debug.Log($"[ATSAccessibility] History search '{_search.Buffer}' matched at index {i}");
                        return;
                    }
                }

                Speech.Say($"No match for {_search.Buffer}");
                Debug.Log($"[ATSAccessibility] History search '{_search.Buffer}' found no match");
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
                Speech.Say("Search cleared");
                Debug.Log("[ATSAccessibility] History search buffer cleared via backspace");
                return;
            }

            // Re-search with shortened buffer
            lock (_lock)
            {
                string prefix = _search.Buffer.ToLowerInvariant();
                for (int i = 0; i < _history.Count; i++)
                {
                    if (_history[i].ToLowerInvariant().StartsWith(prefix))
                    {
                        _currentIndex = i;
                        AnnounceCurrentItem(includeHeader: false);
                        Debug.Log($"[ATSAccessibility] History search '{_search.Buffer}' matched at index {i}");
                        return;
                    }
                }

                Speech.Say($"No match for {_search.Buffer}");
                Debug.Log($"[ATSAccessibility] History search '{_search.Buffer}' found no match");
            }
        }
    }
}
