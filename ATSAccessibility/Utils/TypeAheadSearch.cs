using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Interface for handlers that support type-ahead search via TypeAheadSearch.HandleKey.
	/// Handlers implement this to describe their searchable list at the current navigation level.
	/// </summary>
	public interface ISearchable {
		/// <summary>
		/// Number of searchable items at the current navigation level.
		/// Return 0 to disable search (letter keys pass through to handler).
		/// </summary>
		int SearchItemCount { get; }

		/// <summary>
		/// Current cursor position. Reserved for overlays that want to align search
		/// with a custom cursor; no longer used by TypeAheadSearch itself.
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
	/// Builds a filtered, ranked results list that can be navigated with Up/Down.
	///
	/// Match priority (tiers, best first):
	///   0 = start of string, whole word ("wood" in "wood club")
	///   1 = start of string, prefix ("wood" in "wooden club")
	///   2 = mid-string whole word ("wood" in "pine wood")
	///   3 = mid-string word prefix ("wood" in "a wooden thing")
	///   4 = substring anywhere ("wood" in "plywood")
	///   5 = space-delimited word-prefix abbreviation ("ga pi" in "gas pipe")
	///
	/// Labels may contain metadata after the first comma (e.g. "Beaver, happy").
	/// Matches inside the pre-comma segment outrank matches inside the appended
	/// metadata regardless of tier. Within a segment+tier, shorter names rank first.
	///
	/// Use HandleKey() with an ISearchable for centralized search behavior,
	/// or the lower-level API (HandleChar/Search/NavigateResults) for custom handling.
	/// </summary>
	public class TypeAheadSearch {
		private StringBuilder _buffer = new StringBuilder(32);

		// Filtered results state
		private bool _isSearchActive;
		private List<int> _resultIndices = new List<int>();
		private List<string> _resultNames = new List<string>();
		private int _resultCursor;

		// Working lists for search, one pair per match tier (avoids allocation)
		private const int TierCount = 6;
		private List<int>[] _tierIndices;
		private List<string>[] _tierNames;
		private List<int>[] _tierPositions;
		private List<int>[] _tierSortLengths;
		private List<int>[] _tierInSegment;
		private List<int> _workIndices = new List<int>();
		private List<string> _workNames = new List<string>();

		// Optional callback for full announcements (called with original index)
		private Action<int> _announceResult;

		/// <summary>
		/// Optional grouping function: maps an original item index to a group number.
		/// During merge, all group-0 items (sorted by tier+position) appear before
		/// group-1 items, etc. When null, all items are in a single group.
		/// </summary>
		public Func<int, int> GroupOf { get; set; }

		// Cached delegates for RunSearch (avoids allocation per call)
		private readonly Func<int, string> _getLabelCached;
		private readonly Action<int> _moveToIndexCached;

		// Stored reference to the current searchable context, set each HandleKey call
		private ISearchable _searchable;

		public TypeAheadSearch() {
			_getLabelCached = i => _searchable.GetSearchLabel(i);
			_moveToIndexCached = i => _searchable.SearchMoveTo(i);
			_tierIndices = new List<int>[TierCount];
			_tierNames = new List<string>[TierCount];
			_tierPositions = new List<int>[TierCount];
			_tierSortLengths = new List<int>[TierCount];
			_tierInSegment = new List<int>[TierCount];
			for (int t = 0; t < TierCount; t++) {
				_tierIndices[t] = new List<int>();
				_tierNames[t] = new List<string>();
				_tierPositions[t] = new List<int>();
				_tierSortLengths[t] = new List<int>();
				_tierInSegment[t] = new List<int>();
			}
		}

		/// <summary>Current search buffer contents.</summary>
		public string Buffer => _buffer.ToString();

		/// <summary>Whether there is an active search buffer.</summary>
		public bool HasBuffer => _buffer.Length > 0;

		/// <summary>Whether filtered results are currently being navigated.</summary>
		public bool IsSearchActive => _isSearchActive;

		/// <summary>Number of filtered results.</summary>
		public int ResultCount => _resultIndices.Count;

		/// <summary>Original-list index of the currently selected result, or -1 if none.</summary>
		public int SelectedOriginalIndex =>
			_isSearchActive && _resultCursor >= 0 && _resultCursor < _resultIndices.Count
				? _resultIndices[_resultCursor]
				: -1;

		/// <summary>Add a character to the search buffer.</summary>
		public string AddChar(char c) {
			_buffer.Append(c);
			return _buffer.ToString();
		}

		/// <summary>Remove the last character from the search buffer (backspace).</summary>
		public bool RemoveChar() {
			if (_buffer.Length == 0)
				return false;

			_buffer.Length--;
			return true;
		}

		/// <summary>Clear the search buffer and all results state.</summary>
		public void Clear() {
			_buffer.Clear();
			_isSearchActive = false;
			_resultIndices.Clear();
			_resultNames.Clear();
			_resultCursor = 0;
			_announceResult = null;
		}

		// ========================================
		// HANDLECHAR / HANDLEKEY
		// ========================================

		/// <summary>
		/// Handle a typed character. Accepts any Unicode letter so searches work in
		/// localized label sets (Latin, Cyrillic, CJK, etc.). Returns true if consumed.
		/// </summary>
		public bool HandleChar(char c, ISearchable searchable) {
			_searchable = searchable;

			if (!_isSearchActive && searchable.SearchItemCount == 0)
				return false;

			AddChar(c);
			RunSearch();
			return true;
		}

		/// <summary>
		/// Handle all search-related keyboard behavior. Call from ProcessKey after
		/// any modifier-key shortcuts (Ctrl+T, Alt+I, etc.). Returns true if consumed.
		/// Letter input is taken from <paramref name="modifiers"/>.TypedChar so any
		/// Unicode letter works, not just A-Z.
		/// </summary>
		public bool HandleKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers, ISearchable searchable) {
			_searchable = searchable;
			bool hasModifier = modifiers.Control || modifiers.Alt;
			char typed = modifiers.TypedChar;

			// Fallback for callers that pass only KeyCode (nested panel bridges).
			// Lets A-Z search still work when TypedChar wasn't threaded through.
			if (typed == '\0' && keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
				typed = (char)('a' + (keyCode - KeyCode.A));

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
						Speech.Say(Strings.Get("util.type_ahead.search_cleared"));
						return true;
					case KeyCode.Backspace:
						if (!RemoveChar())
							return true;
						if (!HasBuffer) {
							Clear();
							Speech.Say(Strings.Get("util.type_ahead.search_cleared"));
							return true;
						}
						RunSearch();
						return true;
					case KeyCode.Space:
						// Space is a valid search separator (enables abbreviation tier,
						// e.g. "ga pi" matching "gas pipe"). Only consume it while
						// search is active so OnSpace still works outside search.
						if (!hasModifier) {
							AddChar(' ');
							RunSearch();
							return true;
						}
						return false;
				}

				// Any letter (any script) while search is active: extend the buffer.
				if (!hasModifier && typed != '\0' && char.IsLetter(typed)) {
					AddChar(typed);
					RunSearch();
					return true;
				}

				// Non-search key while active: cursor already moved via SearchMoveTo.
				// Clear search and let the handler process the key normally.
				Clear();
				return false;
			}

			// Search inactive: start a new search on a letter keystroke.
			if (!hasModifier && typed != '\0' && char.IsLetter(typed)) {
				if (searchable.SearchItemCount == 0)
					return false;

				AddChar(typed);
				RunSearch();
				return true;
			}

			// Search inactive but buffer has leftover characters: handle Backspace.
			if (keyCode == KeyCode.Backspace && HasBuffer) {
				if (!RemoveChar())
					return true;
				if (!HasBuffer) {
					Clear();
					Speech.Say(Strings.Get("util.type_ahead.search_cleared"));
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
		/// Perform a tiered search and announce the top result.
		/// </summary>
		/// <param name="itemCount">Number of items to search.</param>
		/// <param name="nameByIndex">Function returning the searchable name for an index, or null to skip.</param>
		/// <param name="announceResult">Optional callback for full announcements. Called with the original
		/// index of the matched item. When null, falls back to announcing the search name.</param>
		public void Search(int itemCount, Func<int, string> nameByIndex, Action<int> announceResult = null) {
			// Repeat single-letter cycles start-of-string matches only (tiers 0-1),
			// so hammering "b" goes Beaver → Bat → Brewery and doesn't wrap into
			// substring matches like "Herbalist".
			string bufferStr = _buffer.ToString();
			if (_isSearchActive && _resultIndices.Count > 0 && _buffer.Length > 1 && IsAllSameChar(bufferStr)) {
				_buffer.Length = 1;
				if (announceResult != null)
					_announceResult = announceResult;
				CycleStartsWithResults();
				return;
			}

			if (announceResult != null)
				_announceResult = announceResult;

			string trimmed = bufferStr.TrimEnd();
			if (!HasBuffer || itemCount == 0 || trimmed.Length == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.Say(Strings.Get("util.type_ahead.no_match", bufferStr));
				return;
			}

			for (int t = 0; t < TierCount; t++) {
				_tierIndices[t].Clear();
				_tierNames[t].Clear();
				_tierPositions[t].Clear();
				_tierSortLengths[t].Clear();
				_tierInSegment[t].Clear();
			}

			string lowerBuffer = trimmed.ToLowerInvariant();

			for (int i = 0; i < itemCount; i++) {
				string name = nameByIndex(i);
				if (string.IsNullOrEmpty(name)) continue;
				int tier = MatchTier(name.ToLowerInvariant(), lowerBuffer, out int pos);
				if (tier < 0) continue;

				_tierIndices[tier].Add(i);
				_tierNames[tier].Add(name);
				_tierPositions[tier].Add(pos);
				// Matches inside the name (before the first comma) rank ahead of
				// matches inside appended metadata. Sort length is name-only so
				// description text doesn't muddy within-segment ordering.
				int comma = name.IndexOf(',');
				int nameLen = comma >= 0 ? comma : name.Length;
				_tierSortLengths[tier].Add(nameLen);
				_tierInSegment[tier].Add(pos < nameLen ? 0 : 1);
			}

			for (int t = 0; t < TierCount; t++) {
				if (_tierIndices[t].Count > 1)
					SortByLength(_tierIndices[t], _tierNames[t], _tierPositions[t], _tierSortLengths[t], _tierInSegment[t]);
			}

			_workIndices.Clear();
			_workNames.Clear();
			if (GroupOf == null) {
				for (int inSeg = 0; inSeg <= 1; inSeg++)
					for (int t = 0; t < TierCount; t++)
						for (int i = 0; i < _tierIndices[t].Count; i++)
							if (_tierInSegment[t][i] == inSeg) {
								_workIndices.Add(_tierIndices[t][i]);
								_workNames.Add(_tierNames[t][i]);
							}
			} else {
				int maxGroup = 0;
				for (int t = 0; t < TierCount; t++)
					for (int i = 0; i < _tierIndices[t].Count; i++) {
						int g = GroupOf(_tierIndices[t][i]);
						if (g > maxGroup) maxGroup = g;
					}
				for (int g = 0; g <= maxGroup; g++)
					for (int inSeg = 0; inSeg <= 1; inSeg++)
						for (int t = 0; t < TierCount; t++)
							for (int i = 0; i < _tierIndices[t].Count; i++)
								if (GroupOf(_tierIndices[t][i]) == g && _tierInSegment[t][i] == inSeg) {
									_workIndices.Add(_tierIndices[t][i]);
									_workNames.Add(_tierNames[t][i]);
								}
			}

			if (_workIndices.Count == 0) {
				_resultIndices.Clear();
				_resultNames.Clear();
				_resultCursor = 0;
				_isSearchActive = true;
				Speech.Say(Strings.Get("util.type_ahead.no_match", bufferStr));
				return;
			}

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

		/// <summary>
		/// Cycle forward within start-of-string results (tiers 0-1) only.
		/// Used for single-letter repeat navigation so holding a key doesn't
		/// wrap into mid-string or substring matches.
		/// </summary>
		private void CycleStartsWithResults() {
			if (_resultIndices.Count == 0) return;

			char letter = char.ToLowerInvariant(_buffer[0]);
			int count = 0;
			for (int i = 0; i < _resultNames.Count; i++) {
				if (_resultNames[i].Length > 0 && char.ToLowerInvariant(_resultNames[i][0]) == letter)
					count++;
				else
					break;
			}

			if (count == 0) return;

			_resultCursor = (_resultCursor + 1) % count;
			AnnounceCurrentResult();
		}

		/// <summary>Navigate within filtered results (wrapping). direction is +1 or -1.</summary>
		public void NavigateResults(int direction) {
			if (_resultIndices.Count == 0) return;

			_resultCursor = NavigationUtils.WrapIndex(_resultCursor, direction, _resultIndices.Count);
			AnnounceCurrentResult();
		}

		/// <summary>Jump to the first filtered result.</summary>
		public void JumpToFirstResult() {
			if (_resultIndices.Count == 0) return;

			_resultCursor = 0;
			AnnounceCurrentResult();
		}

		/// <summary>Jump to the last filtered result.</summary>
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

		/// <summary>
		/// Insertion-sort parallel lists by sort length ascending, with position as
		/// tiebreaker (stable, in-place).
		/// </summary>
		private static void SortByLength(List<int> indices, List<string> names, List<int> positions, List<int> sortLengths, List<int> inSegment) {
			for (int i = 1; i < positions.Count; i++) {
				int pos = positions[i];
				int idx = indices[i];
				string name = names[i];
				int len = sortLengths[i];
				int seg = inSegment[i];
				int j = i - 1;
				while (j >= 0 && (sortLengths[j] > len || (sortLengths[j] == len && positions[j] > pos))) {
					positions[j + 1] = positions[j];
					indices[j + 1] = indices[j];
					names[j + 1] = names[j];
					sortLengths[j + 1] = sortLengths[j];
					inSegment[j + 1] = inSegment[j];
					j--;
				}
				positions[j + 1] = pos;
				indices[j + 1] = idx;
				names[j + 1] = name;
				sortLengths[j + 1] = len;
				inSegment[j + 1] = seg;
			}
		}

		/// <summary>
		/// Returns the match tier for a prefix against a name (both lowercase), or -1 for no match.
		/// See class docs for tier descriptions.
		/// </summary>
		internal static int MatchTier(string lowerName, string lowerPrefix) {
			return MatchTier(lowerName, lowerPrefix, out _);
		}

		internal static int MatchTier(string lowerName, string lowerPrefix, out int position) {
			position = -1;
			lowerName = StringUtil.RemoveDiacritics(lowerName);
			lowerPrefix = StringUtil.RemoveDiacritics(lowerPrefix);
			int prefixLen = lowerPrefix.Length;
			if (prefixLen == 0 || prefixLen > lowerName.Length)
				return -1;

			// Start of string
			if (string.Compare(lowerName, 0, lowerPrefix, 0, prefixLen, StringComparison.Ordinal) == 0) {
				position = 0;
				bool wholeWord = lowerName.Length == prefixLen || lowerName[prefixLen] == ' ' || lowerName[prefixLen] == ',';
				return wholeWord ? 0 : 1;
			}

			// Word starts after spaces or commas
			for (int i = 1; i < lowerName.Length; i++) {
				char prev = lowerName[i - 1];
				if (prev != ' ' && prev != ',') continue;
				if (lowerName[i] == ' ') continue;
				if (lowerName.Length - i < prefixLen) break;
				if (string.Compare(lowerName, i, lowerPrefix, 0, prefixLen, StringComparison.Ordinal) == 0) {
					int afterMatch = i + prefixLen;
					bool wholeWord = afterMatch >= lowerName.Length || lowerName[afterMatch] == ' ' || lowerName[afterMatch] == ',';
					position = i;
					return wholeWord ? 2 : 3;
				}
			}

			// Substring anywhere
			int idx = lowerName.IndexOf(lowerPrefix, StringComparison.Ordinal);
			if (idx >= 0) {
				position = idx;
				return 4;
			}

			// Space-delimited word-prefix abbreviation ("ga pi" in "gas pipe")
			if (lowerPrefix.IndexOf(' ') >= 0) {
				int abbrevPos = MatchWordPrefixTokens(lowerName, lowerPrefix);
				if (abbrevPos >= 0) {
					position = abbrevPos;
					return 5;
				}
			}

			return -1;
		}

		/// <summary>
		/// Returns the position of the first matched word if every space-delimited token in
		/// <paramref name="lowerPrefix"/> is a prefix of a distinct word in <paramref name="lowerName"/>,
		/// consumed in order and all within a single comma-delimited segment. Returns -1 otherwise.
		/// All tokens must fall within the same segment so the returned position meaningfully
		/// identifies where the match is (pre-comma or post-comma).
		/// </summary>
		private static int MatchWordPrefixTokens(string lowerName, string lowerPrefix) {
			string[] rawTokens = lowerPrefix.Split(' ');
			int tokenCount = 0;
			for (int t = 0; t < rawTokens.Length; t++)
				if (rawTokens[t].Length > 0) rawTokens[tokenCount++] = rawTokens[t];
			if (tokenCount == 0) return -1;

			int tokenIdx = 0;
			int firstPos = -1;
			int i = 0;
			while (i < lowerName.Length) {
				char c = lowerName[i];
				if (c == ',') {
					// End of segment: if not all tokens matched, restart in next segment
					tokenIdx = 0;
					firstPos = -1;
					i++;
					continue;
				}
				if (c == ' ') { i++; continue; }

				if (tokenIdx < tokenCount) {
					string token = rawTokens[tokenIdx];
					bool fits = i + token.Length <= lowerName.Length
						&& string.Compare(lowerName, i, token, 0, token.Length, StringComparison.Ordinal) == 0;
					if (fits) {
						if (tokenIdx == 0) firstPos = i;
						tokenIdx++;
						if (tokenIdx == tokenCount) return firstPos;
						i += token.Length;
					}
				}
				// Advance past the current word to its terminating space or comma
				while (i < lowerName.Length && lowerName[i] != ' ' && lowerName[i] != ',') i++;
			}

			return -1;
		}
	}
}
