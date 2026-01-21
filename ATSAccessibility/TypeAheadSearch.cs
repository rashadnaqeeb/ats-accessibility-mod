using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reusable type-ahead search helper for keyboard navigation.
    /// Manages search buffer with timeout and provides generic prefix matching.
    /// </summary>
    public class TypeAheadSearch
    {
        private string _buffer = "";
        private float _lastTime = 0f;

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
        /// Add a character to the search buffer.
        /// Resets the buffer if timeout has elapsed since last input.
        /// </summary>
        /// <param name="c">Character to add (typically lowercase a-z).</param>
        /// <returns>The current buffer after adding the character.</returns>
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
        /// <returns>True if a character was removed, false if buffer was empty.</returns>
        public bool RemoveChar()
        {
            if (string.IsNullOrEmpty(_buffer))
                return false;

            _buffer = _buffer.Substring(0, _buffer.Length - 1);
            _lastTime = Time.time;
            return true;
        }

        /// <summary>
        /// Clear the search buffer.
        /// </summary>
        public void Clear()
        {
            _buffer = "";
        }

        /// <summary>
        /// Silently clear the search buffer if a navigation key is pressed.
        /// Call at the start of ProcessKeyEvent to reset search on navigation.
        /// </summary>
        public void ClearOnNavigationKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _buffer = "";
                    break;
            }
        }

        /// <summary>
        /// Find the first item whose name starts with the current buffer (case-insensitive).
        /// </summary>
        /// <typeparam name="T">Type of items in the list.</typeparam>
        /// <param name="items">List of items to search.</param>
        /// <param name="nameSelector">Function to extract the searchable name from an item.</param>
        /// <returns>Index of the first match, or -1 if no match found.</returns>
        public int FindMatch<T>(IList<T> items, Func<T, string> nameSelector)
        {
            if (!HasBuffer || items == null || items.Count == 0)
                return -1;

            string lowerPrefix = _buffer.ToLowerInvariant();

            for (int i = 0; i < items.Count; i++)
            {
                string name = nameSelector(items[i]);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
