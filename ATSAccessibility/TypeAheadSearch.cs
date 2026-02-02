using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reusable type-ahead search helper for keyboard navigation.
    /// Builds a filtered results list (word-start matching) that can be navigated with Up/Down.
    /// Caller updates its position only on Enter (selection).
    /// </summary>
    public class TypeAheadSearch
    {
        private string _buffer = "";
        private float _lastTime = 0f;

        // Filtered results state
        private bool _isSearchActive;
        private List<int> _resultIndices = new List<int>();
        private List<string> _resultNames = new List<string>();
        private int _resultCursor;

        // Optional callback for full announcements (called with original index)
        private Action<int> _announceResult;

        /// <summary>
        /// Time in seconds before the search buffer resets on new input.
        /// </summary>
        public float Timeout { get; set; } = 1.5f;

        /// <summary>
        /// Current search buffer contents.
        /// </summary>
        public string Buffer => _buffer;

        /// <summary>
        /// Whether there is an active search buffer.
        /// </summary>
        public bool HasBuffer => !string.IsNullOrEmpty(_buffer);

        /// <summary>
        /// Whether filtered results are currently being navigated.
        /// True after Search() is called, false after Clear().
        /// </summary>
        public bool IsSearchActive => _isSearchActive;

        /// <summary>
        /// Number of filtered results.
        /// </summary>
        public int ResultCount => _resultIndices.Count;

        /// <summary>
        /// The original-list index of the currently selected result, or -1 if no results.
        /// </summary>
        public int SelectedOriginalIndex =>
            _isSearchActive && _resultCursor >= 0 && _resultCursor < _resultIndices.Count
                ? _resultIndices[_resultCursor]
                : -1;

        /// <summary>
        /// Add a character to the search buffer.
        /// Resets the buffer if timeout has elapsed since last input.
        /// </summary>
        public string AddChar(char c)
        {
            if (Time.time - _lastTime > Timeout)
                _buffer = "";

            _buffer += c;
            _lastTime = Time.time;
            return _buffer;
        }

        /// <summary>
        /// Remove the last character from the search buffer (backspace).
        /// </summary>
        public bool RemoveChar()
        {
            if (string.IsNullOrEmpty(_buffer))
                return false;

            _buffer = _buffer.Substring(0, _buffer.Length - 1);
            _lastTime = Time.time;
            return true;
        }

        /// <summary>
        /// Clear the search buffer and all results state.
        /// </summary>
        public void Clear()
        {
            _buffer = "";
            _isSearchActive = false;
            _resultIndices.Clear();
            _resultNames.Clear();
            _resultCursor = 0;
            _announceResult = null;
        }

        /// <summary>
        /// Silently clear the search buffer if a level-change key is pressed (Left/Right only).
        /// Up/Down are NOT included because they navigate search results when active.
        /// </summary>
        public void ClearOnLevelChangeKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    Clear();
                    break;
            }
        }

        /// <summary>
        /// Perform a word-start-match search and announce results.
        /// </summary>
        /// <param name="itemCount">Number of items to search.</param>
        /// <param name="nameByIndex">Function returning the searchable name for an index, or null to skip.</param>
        /// <param name="announceResult">Optional callback for full announcements. Called with the original
        /// index of the matched item. When null, falls back to announcing the search name.</param>
        public void Search(int itemCount, Func<int, string> nameByIndex, Action<int> announceResult = null)
        {
            _resultIndices.Clear();
            _resultNames.Clear();
            _resultCursor = 0;
            _isSearchActive = true;
            _announceResult = announceResult;

            if (!HasBuffer || itemCount == 0)
            {
                Speech.Say($"No match for {_buffer}");
                return;
            }

            string lowerBuffer = _buffer.ToLowerInvariant();

            for (int i = 0; i < itemCount; i++)
            {
                string name = nameByIndex(i);
                if (!string.IsNullOrEmpty(name) && StartsAnyWord(name.ToLowerInvariant(), lowerBuffer))
                {
                    _resultIndices.Add(i);
                    _resultNames.Add(name);
                }
            }

            if (_resultIndices.Count == 0)
            {
                Speech.Say($"No match for {_buffer}");
            }
            else
            {
                AnnounceCurrentResult();
            }
        }

        /// <summary>
        /// Navigate within filtered results (wrapping).
        /// </summary>
        /// <param name="direction">1 for next, -1 for previous.</param>
        public void NavigateResults(int direction)
        {
            if (_resultIndices.Count == 0) return;

            _resultCursor = NavigationUtils.WrapIndex(_resultCursor, direction, _resultIndices.Count);
            AnnounceCurrentResult();
        }

        /// <summary>
        /// Jump to the first filtered result.
        /// </summary>
        public void JumpToFirstResult()
        {
            if (_resultIndices.Count == 0) return;

            _resultCursor = 0;
            AnnounceCurrentResult();
        }

        /// <summary>
        /// Jump to the last filtered result.
        /// </summary>
        public void JumpToLastResult()
        {
            if (_resultIndices.Count == 0) return;

            _resultCursor = _resultIndices.Count - 1;
            AnnounceCurrentResult();
        }

        private void AnnounceCurrentResult()
        {
            if (_resultIndices.Count == 0) return;

            if (_announceResult != null)
                _announceResult(_resultIndices[_resultCursor]);
            else
                Speech.Say(_resultNames[_resultCursor]);
        }

        private static bool StartsAnyWord(string lowerName, string lowerPrefix)
        {
            if (lowerName.StartsWith(lowerPrefix))
                return true;

            for (int i = 1; i < lowerName.Length; i++)
            {
                if (lowerName[i - 1] == ' ' && lowerName.Length - i >= lowerPrefix.Length &&
                    lowerName.Substring(i).StartsWith(lowerPrefix))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
