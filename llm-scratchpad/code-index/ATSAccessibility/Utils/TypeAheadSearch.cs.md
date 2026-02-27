# TypeAheadSearch.cs

## interface ISearchable (line 12)
Interface for handlers that support type-ahead search via TypeAheadSearch.HandleKey.
Handlers implement this to describe their searchable list at the current navigation level.

### Properties
- int SearchItemCount { get; } (line 17)
  Return 0 to disable search (A-Z keys pass through to handler).
- int SearchCurrentIndex { get; } (line 22)
  Current cursor position (reserved for future use).

### Methods
- string GetSearchLabel(int index) (line 28)
  Return null to skip an item in search results.
- void SearchMoveTo(int index) (line 34)
  Move cursor to index and announce. Called during search navigation. The move is permanent.

---

## class TypeAheadSearch (line 43)
Reusable type-ahead search helper for keyboard navigation.
Builds a filtered results list (word-start matching) that can be navigated with Up/Down.

### Fields
- private StringBuilder _buffer (line 44)
- private float _lastTime (line 45)
- private bool _isSearchActive (line 48)
- private List<int> _resultIndices (line 49)
- private List<string> _resultNames (line 50)
- private int _resultCursor (line 51)
- private List<int> _workIndices (line 54)
  Working lists swapped into result lists on match; avoids allocation per search.
- private List<string> _workNames (line 55)
- private Action<int> _announceResult (line 58)
- private readonly Func<int, string> _getLabelCached (line 61)
- private readonly Action<int> _moveToIndexCached (line 62)
- private ISearchable _searchable (line 145)
  Stored reference to current searchable context; set on each HandleKey call.

### Properties
- public float Timeout { get; set; } = 1.5f (line 72)
- public string Buffer { get; } (line 77)
- public bool HasBuffer { get; } (line 82)
- public bool IsSearchActive { get; } (line 88)
- public int ResultCount { get; } (line 93)
- public int SelectedOriginalIndex { get; } (line 98)
  Returns original-list index of currently selected result, or -1 if no results.

### Methods
- public TypeAheadSearch() (line 64)
- public string AddChar(char c) (line 107)
  Resets buffer if timeout elapsed since last input. Returns new buffer string.
- public bool RemoveChar() (line 119)
  Removes last character. Returns false if buffer was already empty.
- public void Clear() (line 131)
- public bool HandleKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers, ISearchable searchable) (line 152)
  Centralized search keyboard handler. Call from ProcessKey after modifier shortcuts. A-Z starts/continues search; Escape clears; Backspace removes char; arrows navigate results. Typing same letter repeatedly cycles results. Non-search keys clear search state and return false (pass-through).
- private void RunSearch() (line 227)
- public void Search(int itemCount, Func<int, string> nameByIndex, Action<int> announceResult = null) (line 239)
  Word-start match search. Repeated single-letter typing cycles results without rebuilding list. Swaps working lists into result lists on match (no allocation).
- public void NavigateResults(int direction) (line 301)
- public void JumpToFirstResult() (line 311)
- public void JumpToLastResult() (line 321)
- private void AnnounceCurrentResult() (line 328)
- private static bool IsAllSameChar(string s) (line 337)
- private static bool StartsAnyWord(string lowerName, string lowerPrefix) (line 345)
  Returns true if lowerPrefix matches at the start of any word (space-delimited) in lowerName.
