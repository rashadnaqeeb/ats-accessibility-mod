using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Interface for handlers that support type-ahead search via TypeAheadSearch.HandleKey.
	/// Handlers implement this to describe their searchable list at the current navigation level.
	/// </summary>
	public interface ISearchable {
		/// <summary>
		/// Number of searchable items at the current navigation level.
		/// Return 0 to disable search (A-Z keys pass through to handler).
		/// </summary>
		int SearchItemCount { get; }

		/// <summary>
		/// Current cursor position (reserved for future use).
		/// </summary>
		int SearchCurrentIndex { get; }

		/// <summary>
		/// Searchable label for the item at the given index.
		/// Return null to skip an item in search results.
		/// </summary>
		string GetSearchLabel(int index);

		/// <summary>
		/// Move cursor to index and announce. Called during search navigation
		/// and when search results are found. The move is permanent.
		/// </summary>
		void SearchMoveTo(int index);
	}

	/// <summary>
	/// Reusable type-ahead search helper for keyboard navigation.
	/// Builds a filtered results list (word-start matching) that can be navigated with Up/Down.
	/// Use HandleKey() with an ISearchable for centralized search behavior,
	/// or the lower-level API (AddChar/Search/NavigateResults) for custom handling.
	/// </summary>
	public class TypeAheadSearch {
		private string _buffer = "";
		private float _lastTime = 0f;

		// Filtered results state
		private bool _isSearchActive;
		private List<int> _resultIndices = new List<int>();
		private List<string> _resultNames = new List<string>();
		private int _resultCursor;

		// Working lists for search (swapped into result lists on match, avoids allocation)
		private List<int> _workIndices = new List<int>();
		private List<string> _workNames = new List<string>();

		// Optional callback for full announcements (called with original index)
		private Action<int> _announceResult;

		// Cached delegates for RunSearch (avoids allocation per call)
		private readonly Func<int, string> _getLabelCached;
		private readonly Action<int> _moveToIndexCached;

		public TypeAheadSearch() {
			_getLabelCached = i => _searchable.GetSearchLabel(i);
			_moveToIndexCached = i => _searchable.SearchMoveTo(i);
		}

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
		public string AddChar(char c) {
			if (Time.time - _lastTime > Timeout)
				_buffer = "";

			_buffer += c;
			_lastTime = Time.time;
			return _buffer;
		}

		/// <summary>
		/// Remove the last character from the search buffer (backspace).
		/// </summary>
		public bool RemoveChar() {
			if (string.IsNullOrEmpty(_buffer))
				return false;

			_buffer = _buffer.Substring(0, _buffer.Length - 1);
			_lastTime = Time.time;
			return true;
		}

		/// <summary>
		/// Clear the search buffer and all results state.
		/// </summary>
		public void Clear() {
			_buffer = "";
			_isSearchActive = false;
			_resultIndices.Clear();
			_resultNames.Clear();
			_resultCursor = 0;
			_announceResult = null;
		}

		// ========================================
		// HANDLEKEY - CENTRALIZED SEARCH BEHAVIOR
		// ========================================

		// Stored reference to the current searchable context, set each HandleKey call
		private ISearchable _searchable;

		/// <summary>
		/// Handle all search-related keyboard behavior.
		/// Call this from ProcessKey after any modifier-key shortcuts (Ctrl+T, Alt+I, etc.).
		/// Returns true if the key was consumed by search.
		/// </summary>
		public bool HandleKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers, ISearchable searchable) {
			_searchable = searchable;

			if (_isSearchActive) {
				switch (keyCode) {
					case KeyCode.UpArrow:
						NavigateResults(-1);
						return true;
					case KeyCode.DownArrow:
						NavigateResults(1);
						return true;
					case KeyCode.Home:
						JumpToFirstResult();
						return true;
					case KeyCode.End:
						JumpToLastResult();
						return true;
					case KeyCode.Escape:
						Clear();
						InputBlocker.BlockCancelOnce = true;
						Speech.Say("Search cleared");
						return true;
					case KeyCode.Backspace:
						if (!RemoveChar())
							return true;
						if (!HasBuffer) {
							Clear();
							Speech.Say("Search cleared");
							return true;
						}
						RunSearch();
						return true;
					default:
						// A-Z without Ctrl/Alt: add to search buffer
						if (!modifiers.Control && !modifiers.Alt &&
							keyCode >= KeyCode.A && keyCode <= KeyCode.Z) {
							char c = (char)('a' + (keyCode - KeyCode.A));
							AddChar(c);
							RunSearch();
							return true;
						}
						// Non-search key: cursor is already at search result from SearchMoveTo.
						// Just clear search and let handler process the key normally.
						Clear();
						return false;
				}
			}

			// Search inactive: start search on A-Z (no Ctrl/Alt)
			if (!modifiers.Control && !modifiers.Alt &&
				keyCode >= KeyCode.A && keyCode <= KeyCode.Z) {
				if (searchable.SearchItemCount == 0)
					return false;

				char c = (char)('a' + (keyCode - KeyCode.A));
				AddChar(c);
				RunSearch();
				return true;
			}

			// Search inactive but has leftover buffer: handle Backspace
			if (keyCode == KeyCode.Backspace && HasBuffer) {
				if (!RemoveChar()) return true;
				if (!HasBuffer) {
					Clear();
					Speech.Say("Search cleared");
					return true;
				}
				RunSearch();
				return true;
			}

			return false;
		}

		private void RunSearch() {
			if (_searchable == null) return;
			Search(_searchable.SearchItemCount, _getLabelCached, _moveToIndexCached);
		}

		/// <summary>
		/// Perform a word-start-match search and announce results.
		/// </summary>
		/// <param name="itemCount">Number of items to search.</param>
		/// <param name="nameByIndex">Function returning the searchable name for an index, or null to skip.</param>
		/// <param name="announceResult">Optional callback for full announcements. Called with the original
		/// index of the matched item. When null, falls back to announcing the search name.</param>
		public void Search(int itemCount, Func<int, string> nameByIndex, Action<int> announceResult = null) {
			// Repeat single-letter: typing the same letter again cycles through results
			// e.g., b → Beaver, b → Bat, b → Brewery
			if (_isSearchActive && _resultIndices.Count > 0 && _buffer.Length > 1 && IsAllSameChar(_buffer)) {
				_buffer = _buffer.Substring(0, 1);
				if (announceResult != null)
					_announceResult = announceResult;
				NavigateResults(1);
				return;
			}

			if (announceResult != null)
				_announceResult = announceResult;

			if (!HasBuffer || itemCount == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.Say($"No match for {_buffer}");
				return;
			}

			// Search into working lists
			_workIndices.Clear();
			_workNames.Clear();
			string lowerBuffer = _buffer.ToLowerInvariant();

			for (int i = 0; i < itemCount; i++) {
				string name = nameByIndex(i);
				if (!string.IsNullOrEmpty(name) && StartsAnyWord(name.ToLowerInvariant(), lowerBuffer)) {
					_workIndices.Add(i);
					_workNames.Add(name);
				}
			}

			if (_workIndices.Count == 0) {
				// No match — clear results but keep the buffer
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.Say($"No match for {_buffer}");
			} else {
				// Swap working lists into result lists (no allocation)
				var tempIndices = _resultIndices;
				var tempNames = _resultNames;
				_resultIndices = _workIndices;
				_resultNames = _workNames;
				_workIndices = tempIndices;
				_workNames = tempNames;
				_resultCursor = 0;
				_isSearchActive = true;
				AnnounceCurrentResult();
			}
		}

		/// <summary>
		/// Navigate within filtered results (wrapping).
		/// </summary>
		/// <param name="direction">1 for next, -1 for previous.</param>
		public void NavigateResults(int direction) {
			if (_resultIndices.Count == 0) return;

			_resultCursor = NavigationUtils.WrapIndex(_resultCursor, direction, _resultIndices.Count);
			AnnounceCurrentResult();
		}

		/// <summary>
		/// Jump to the first filtered result.
		/// </summary>
		public void JumpToFirstResult() {
			if (_resultIndices.Count == 0) return;

			_resultCursor = 0;
			AnnounceCurrentResult();
		}

		/// <summary>
		/// Jump to the last filtered result.
		/// </summary>
		public void JumpToLastResult() {
			if (_resultIndices.Count == 0) return;

			_resultCursor = _resultIndices.Count - 1;
			AnnounceCurrentResult();
		}

		private void AnnounceCurrentResult() {
			if (_resultIndices.Count == 0) return;

			if (_announceResult != null)
				_announceResult(_resultIndices[_resultCursor]);
			else
				Speech.Say(_resultNames[_resultCursor]);
		}

		private static bool IsAllSameChar(string s) {
			char first = s[0];
			for (int i = 1; i < s.Length; i++) {
				if (s[i] != first) return false;
			}
			return true;
		}

		private static bool StartsAnyWord(string lowerName, string lowerPrefix) {
			if (lowerPrefix.Length <= lowerName.Length &&
				string.Compare(lowerName, 0, lowerPrefix, 0, lowerPrefix.Length, StringComparison.Ordinal) == 0)
				return true;

			for (int i = 1; i < lowerName.Length; i++) {
				if (lowerName[i - 1] == ' ' && lowerName.Length - i >= lowerPrefix.Length &&
					string.Compare(lowerName, i, lowerPrefix, 0, lowerPrefix.Length, StringComparison.Ordinal) == 0) {
					return true;
				}
			}

			return false;
		}
	}
}
