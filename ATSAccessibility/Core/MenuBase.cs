using ATSAccessibility.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Core {
	/// <summary>
	/// Abstract base class for keyboard-navigable menus and overlays.
	/// Provides level-based navigation (Up/Down/Enter/Escape/Left/Right),
	/// type-ahead search via ISearchable, and a ProcessKey flow that
	/// subclasses customize through abstract and virtual methods.
	///
	/// Navigation levels use a flat index array (_indices[level]).
	/// Level 0 is always the root. Subclasses define what each level means.
	/// </summary>
	public abstract class MenuBase: ISearchable, IKeyHandler, IHelpProvider {
		// ========================================
		// ENUMS
		// ========================================

		protected enum EnterAction {
			/// <summary>Drill down to next navigation level.</summary>
			DrillDown,
			/// <summary>Perform an action (toggle, activate, etc.).</summary>
			Action,
			/// <summary>Do nothing.</summary>
			None
		}

		protected enum EscapeAction {
			/// <summary>Go back one level.</summary>
			GoBack,
			/// <summary>Close the menu.</summary>
			Close,
			/// <summary>Pass Escape through to the game.</summary>
			PassThrough
		}

		// ========================================
		// STATIC EVENTS
		// ========================================

		/// <summary>Fired when any MenuBase opens. Used by exclusive modes to auto-close.</summary>
		public static event System.Action OnAnyMenuOpened;

		/// <summary>Clear static event to prevent stale subscribers after mod reload.</summary>
		public static void ClearStaticState() {
			OnAnyMenuOpened = null;
		}

		// ========================================
		// STATE
		// ========================================

		protected int[] _indices = new int[8];
		protected readonly TypeAheadSearch _search = new TypeAheadSearch();
		private bool _isOpen;
		private bool _suspended;
		private int _level;
		private bool _closingSilently;

		// ========================================
		// PROPERTIES
		// ========================================

		protected int Level => _level;

		protected int CurrentIndex {
			get => _indices[_level];
			set => _indices[_level] = value;
		}

		public bool IsOpen => _isOpen;
		public bool IsSuspended => _suspended;
		public virtual bool IsActive => IsOpen && !IsSuspended;
		protected int ItemCount => GetItemCount();

		// ========================================
		// ABSTRACT MEMBERS
		// ========================================

		/// <summary>Name for logging and open announcement.</summary>
		protected abstract string OverlayName { get; }

		/// <summary>Message when no items at open.</summary>
		protected abstract string EmptyMessage { get; }

		/// <summary>Count of items at the current navigation level.</summary>
		protected abstract int GetItemCount();

		/// <summary>Label for the item at the given index at the current level. Used for search and default announce.</summary>
		protected abstract string GetLabel(int index);

		/// <summary>Populate data from game state.</summary>
		protected abstract void RefreshData();

		/// <summary>What Enter does at the current level for the given index.</summary>
		protected abstract EnterAction OnEnter(int index);

		// ========================================
		// VIRTUAL: ACTIONS
		// ========================================

		/// <summary>Perform action at current item (Enter at leaf, or Space default).</summary>
		protected virtual void OnAction(int index) { }

		/// <summary>Space key handler. Defaults to OnAction.</summary>
		protected virtual void OnSpace(int index) => OnAction(index);

		/// <summary>+/- key handler. dir is +1 or -1.</summary>
		protected virtual void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) { }

		// ========================================
		// VIRTUAL: LEVEL TRANSITIONS
		// ========================================

		/// <summary>Prepare next level's data before drilling down.</summary>
		protected virtual void OnDrillDown(int index) { }

		/// <summary>Clean up current level when going back.</summary>
		protected virtual void OnGoBack() { }

		/// <summary>Escape behavior. Default: GoBack if level > 0, else PassThrough.</summary>
		protected virtual EscapeAction OnEscape() {
			return _level > 0 ? EscapeAction.GoBack : EscapeAction.PassThrough;
		}

		/// <summary>
		/// Whether Right arrow can drill down at the given index.
		/// Default checks OnEnter. Override for Pattern B (Enter=action, Right=drill).
		/// </summary>
		protected virtual bool CanDrillDown(int index) {
			return OnEnter(index) == EnterAction.DrillDown;
		}

		// ========================================
		// VIRTUAL: SPECIAL KEYS
		// ========================================

		/// <summary>
		/// Per-overlay custom key handling. Called before search and standard navigation.
		/// Return true if handled, false to pass through, null to continue to base processing.
		/// </summary>
		protected virtual bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			return null;
		}

		// ========================================
		// VIRTUAL: LIFECYCLE HOOKS
		// ========================================

		/// <summary>Save popup reference. Called before Open() lifecycle.</summary>
		protected virtual void StorePopup(object popup) { }

		/// <summary>Extra setup after open (after RefreshData and open announcement).
		/// Use Speech.Say(msg, false) here to queue speech after the announcement.</summary>
		protected virtual void OnOpened() { }

		/// <summary>Extra teardown on close.</summary>
		protected virtual void OnClosed() { }

		/// <summary>Opening speech. Default: OverlayName + first label.</summary>
		protected virtual string GetOpenAnnouncement() {
			int count = GetItemCount();
			if (count == 0)
				return !string.IsNullOrEmpty(EmptyMessage) ? EmptyMessage : OverlayName;

			string firstLabel = GetLabel(0);
			if (!string.IsNullOrEmpty(firstLabel))
				return OverlayName + ". " + firstLabel;
			return OverlayName;
		}

		// ========================================
		// VIRTUAL: ANNOUNCEMENTS & SEARCH
		// ========================================

		/// <summary>Announce the item at CurrentIndex. Override for custom formatting.</summary>
		protected virtual void AnnounceCurrentItem() {
			int count = GetItemCount();
			if (count == 0) return;

			// The list can shrink while suspended (a child popup's action changed
			// it); clamp so a subclass GetLabel that indexes a List with the raw
			// value doesn't throw and silently lose the announcement.
			ClampCurrentIndex(count);

			string label = GetLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		/// <summary>
		/// Clamp CurrentIndex into range for the given item count. No-op when the
		/// list is empty or the index is already valid.
		/// </summary>
		protected void ClampCurrentIndex(int count) {
			if (count > 0 && CurrentIndex >= count)
				CurrentIndex = count - 1;
		}

		/// <summary>Search text for the given index. Defaults to GetLabel. Override to customize search text.</summary>
		protected virtual string GetSearchName(int index) {
			return GetLabel(index);
		}

		// ========================================
		// VIRTUAL: ISEARCHABLE BACKING
		// ========================================

		/// <summary>Number of searchable items. Override to disable search at certain levels.</summary>
		protected virtual int SearchItemCount => GetItemCount();

		/// <summary>Current search index. Override for custom search cursor.</summary>
		protected virtual int SearchCurrentIndex => CurrentIndex;

		// ========================================
		// PROGRAMMATIC LEVEL CONTROL
		// ========================================

		/// <summary>
		/// Set level directly. No callbacks, no announce.
		/// For subclasses that force level changes after actions.
		/// Search is not cleared — callers in action handlers don't need it
		/// (TypeAheadSearch already exits search before actions run), and
		/// callers in SearchMoveTo must not clear mid-search.
		/// </summary>
		protected void SetLevel(int level) {
			_level = level;
		}

		// ========================================
		// LIFECYCLE
		// ========================================

		/// <summary>Open the menu. Guards re-open.</summary>
		public void Open() {
			if (_isOpen) return;

			_isOpen = true;
			OnAnyMenuOpened?.Invoke();
			OpenCore();
		}

		/// <summary>Open without firing OnAnyMenuOpened. Used by HelpOverlay to avoid cancelling active modes.</summary>
		protected void OpenSilently() {
			if (_isOpen) return;

			_isOpen = true;
			OpenCore();
		}

		private void OpenCore() {
			_level = 0;
			for (int i = 0; i < _indices.Length; i++)
				_indices[i] = 0;
			_search.Clear();

			RefreshData();

			string announcement = GetOpenAnnouncement();
			if (!string.IsNullOrEmpty(announcement))
				Speech.Say(announcement);

			OnOpened();

			Debug.Log($"[ATSAccessibility] {OverlayName}: Opened");
		}

		/// <summary>Open with a popup reference.</summary>
		public void Open(object popup) {
			StorePopup(popup);
			Open();
		}

		/// <summary>Close the menu. Guards not open.</summary>
		public void Close() {
			if (!_isOpen) return;

			_isOpen = false;
			_suspended = false;
			OnClosed();
			_search.Clear();

			Debug.Log($"[ATSAccessibility] {OverlayName}: Closed");
		}

		/// <summary>
		/// True while a CloseSilently() call is in progress. OnClosed overrides
		/// that announce the close (speech, InputBlocker.BlockCancelOnce) must
		/// check this and stay quiet.
		/// </summary>
		protected bool IsClosingSilently => _closingSilently;

		/// <summary>
		/// Close without any close announcement. For scene-teardown cleanup,
		/// where the thing being closed no longer exists and speech is noise.
		/// </summary>
		public void CloseSilently() {
			if (!_isOpen) return;
			_closingSilently = true;
			try {
				Close();
			} finally {
				_closingSilently = false;
			}
		}

		/// <summary>Suspend input processing (e.g., when a sub-overlay opens).</summary>
		public void Suspend() {
			_suspended = true;
		}

		/// <summary>Resume input processing and re-announce current item.</summary>
		public void Resume() {
			_suspended = false;
			AnnounceCurrentItem();
		}

		// ========================================
		// NAVIGATION
		// ========================================

		/// <summary>Navigate by direction (wrapping). Clears search on navigation.</summary>
		protected void Navigate(int direction) {
			int count = GetItemCount();
			if (count == 0) return;

			CurrentIndex = NavigationUtils.WrapIndex(CurrentIndex, direction, count);
			AnnounceCurrentItem();
		}

		/// <summary>Navigate to a specific index (clamped).</summary>
		protected void NavigateTo(int index) {
			int count = GetItemCount();
			if (count == 0) return;

			CurrentIndex = Mathf.Clamp(index, 0, count - 1);
			AnnounceCurrentItem();
		}

		private void DrillDown() {
			if (_level + 1 >= _indices.Length) return;

			OnDrillDown(CurrentIndex);
			_level++;
			_indices[_level] = 0;
			_search.Clear();
			AnnounceCurrentItem();
		}

		private void GoBack() {
			if (_level <= 0) return;

			OnGoBack();
			_level--;
			_search.Clear();
			AnnounceCurrentItem();
		}

		// ========================================
		// PROCESSKEY
		// ========================================

		/// <summary>
		/// Main key processing. Returns true if consumed, false to pass through.
		/// </summary>
		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// 1. Special keys
			bool? special = HandleSpecialKey(keyCode, modifiers);
			if (special.HasValue)
				return special.Value;

			// 2. Type-ahead search
			if (_search.HandleKey(keyCode, modifiers, this))
				return true;

			// 3. Standard navigation
			int count = GetItemCount();

			// Clamp a stale index once for every dispatch below: the list can
			// shrink while suspended (a child popup's action changed it), and
			// OnEnter/OnAction/OnSpace/OnAdjust/CanDrillDown must all receive an
			// index that points at a real item.
			ClampCurrentIndex(count);

			switch (keyCode) {
				case KeyCode.UpArrow:
					Navigate(-1);
					return true;

				case KeyCode.DownArrow:
					Navigate(1);
					return true;

				case KeyCode.Home:
					NavigateTo(0);
					return true;

				case KeyCode.End:
					if (count > 0)
						NavigateTo(count - 1);
					return true;

				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (count > 0) {
						switch (OnEnter(CurrentIndex)) {
							case EnterAction.DrillDown:
								DrillDown();
								break;
							case EnterAction.Action:
								OnAction(CurrentIndex);
								break;
						}
					}
					return true;

				case KeyCode.RightArrow:
					if (count > 0 && CanDrillDown(CurrentIndex))
						DrillDown();
					return true;

				case KeyCode.LeftArrow:
					if (_level > 0)
						GoBack();
					return true;

				case KeyCode.Space:
					if (count > 0)
						OnSpace(CurrentIndex);
					return true;

				case KeyCode.KeypadPlus:
				case KeyCode.Equals:
					if (count > 0)
						OnAdjust(CurrentIndex, 1, modifiers);
					return true;

				case KeyCode.KeypadMinus:
				case KeyCode.Minus:
					if (count > 0)
						OnAdjust(CurrentIndex, -1, modifiers);
					return true;

				case KeyCode.Escape:
					switch (OnEscape()) {
						case EscapeAction.GoBack:
							GoBack();
							InputBlocker.BlockCancelOnce = true;
							return true;
						case EscapeAction.Close:
							Close();
							return true;
						case EscapeAction.PassThrough:
							return false;
					}
					return true;

				default:
					// Consume all other keys while menu is open
					return true;
			}
		}

		/// <summary>
		/// Convenience bridge for callers that only pass a KeyCode without modifiers.
		/// Use the overload that threads KeyModifiers when available so type-ahead
		/// search can see the typed character (modifiers.TypedChar).
		/// </summary>
		public bool ProcessKeyEvent(KeyCode keyCode) {
			if (!IsOpen) return false;
			return ProcessKey(keyCode, default(KeyboardManager.KeyModifiers));
		}

		/// <summary>
		/// Bridge for nested panels that want to forward modifiers (including TypedChar).
		/// </summary>
		public bool ProcessKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!IsOpen) return false;
			return ProcessKey(keyCode, modifiers);
		}

		// ========================================
		// IHELPPROVIDER (default implementation)
		// ========================================

		private static readonly List<HelpEntry> _menuBaseHelpEntries = new List<HelpEntry> {
			HelpEntry.Loca("+/-", "menu.base.help.adjust_value"),
			HelpEntry.Loca("Shift+/-", "menu.base.help.larger_increment"),
		};

		/// <summary>Standard MenuBase help entries. Subclasses can call this to include base entries.</summary>
		protected static IReadOnlyList<HelpEntry> MenuBaseHelpEntries => _menuBaseHelpEntries;

		public virtual HelpBehavior HelpBehavior => HelpBehavior.Terminator;
		public virtual string HelpContextName => OverlayName;
		public virtual IReadOnlyList<HelpEntry> GetHelpEntries() => _menuBaseHelpEntries;
		public virtual IReadOnlyList<string> GetPassthroughKeys() => null;

		// ========================================
		// ISEARCHABLE (explicit interface)
		// ========================================

		int ISearchable.SearchItemCount => SearchItemCount;

		int ISearchable.SearchCurrentIndex => SearchCurrentIndex;

		string ISearchable.GetSearchLabel(int index) => GetSearchName(index);

		/// <summary>Move to index on search match. Override for custom search navigation (e.g., flat cross-category).</summary>
		protected virtual void SearchMoveTo(int index) => NavigateTo(index);

		void ISearchable.SearchMoveTo(int index) => SearchMoveTo(index);
	}
}
