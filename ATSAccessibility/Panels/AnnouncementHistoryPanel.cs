using ATSAccessibility.Handlers;
using ATSAccessibility.Utils;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Stores recent announcements and provides a panel to review them.
	/// Opened with Alt+N during settlement gameplay.
	/// </summary>
	public class AnnouncementHistoryPanel: MenuBase {
		private const int MAX_HISTORY = 10;

		private struct HistoryEntry {
			public string Message;
			public Vector2Int? Location;
			public HistoryEntry(string message, Vector2Int? location) { Message = message; Location = location; }
		}

		private static readonly List<HistoryEntry> _history = new List<HistoryEntry>();
		private static readonly object _lock = new object();

		// Per-open snapshot of _history. New announcements arrive constantly and
		// AddMessage inserts at index 0 — navigating the live list would shift
		// every index under the cursor, so Enter could jump to a different event
		// than the one just read.
		private readonly List<HistoryEntry> _snapshot = new List<HistoryEntry>();

		private readonly MapNavigator _mapNavigator;

		public AnnouncementHistoryPanel(MapNavigator mapNavigator) {
			_mapNavigator = mapNavigator;
		}

		// ========================================
		// STATIC API
		// ========================================

		/// <summary>
		/// Add a message to the history.
		/// Called by EventAnnouncer when an announcement is made.
		/// </summary>
		public static void AddMessage(string message, Vector2Int? location = null) {
			if (string.IsNullOrEmpty(message)) return;

			lock (_lock) {
				// Add to the beginning (most recent first)
				_history.Insert(0, new HistoryEntry(message, location));

				// Trim to max size
				while (_history.Count > MAX_HISTORY) {
					_history.RemoveAt(_history.Count - 1);
				}
			}
		}

		/// <summary>
		/// Clear all history.
		/// </summary>
		public static void ClearHistory() {
			lock (_lock) {
				_history.Clear();
			}
		}

		// ========================================
		// MENUBASE ABSTRACTS
		// ========================================

		protected override string OverlayName => Strings.Get("panel.announcement_history.title");
		protected override string EmptyMessage => Strings.Get("panel.announcement_history.empty");

		protected override int GetItemCount() => _snapshot.Count;

		protected override string GetLabel(int index) {
			return index >= 0 && index < _snapshot.Count ? _snapshot[index].Message : null;
		}

		protected override void RefreshData() {
			lock (_lock) {
				_snapshot.Clear();
				_snapshot.AddRange(_history);
			}
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override void OnAction(int index) => GoToEventLocation();

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.N) {
				Close();
				return true;
			}
			return null;
		}

		protected override EscapeAction OnEscape() => EscapeAction.Close;

		protected override string GetOpenAnnouncement() {
			if (_snapshot.Count == 0)
				return EmptyMessage;
			return Strings.Get("panel.announcement_history.open", _snapshot[0].Message);
		}

		protected override void OnOpened() {
			if (_snapshot.Count == 0)
				CloseSilently();
		}

		protected override void OnClosed() {
			_snapshot.Clear();
			if (!IsClosingSilently) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say(Strings.Get("panel.announcement_history.closed"));
			}
		}

		protected override int SearchItemCount => _snapshot.Count;

		protected override string GetSearchName(int index) {
			return index >= 0 && index < _snapshot.Count ? _snapshot[index].Message : null;
		}

		// ========================================
		// NAVIGATION TO EVENTS
		// ========================================

		private void GoToEventLocation() {
			if (CurrentIndex < 0 || CurrentIndex >= _snapshot.Count) return;
			Vector2Int? location = _snapshot[CurrentIndex].Location;

			if (!location.HasValue) {
				Speech.Say(Strings.Get("panel.announcement_history.no_location"));
				return;
			}

			var pos = location.Value;
			CloseSilently();
			_mapNavigator.SetCursorPosition(pos.x, pos.y);
			_mapNavigator.MoveCursor(0, 0);
		}

		/// <summary>
		/// Jump to the most recent event that has a location.
		/// Called via Shift+N from SettlementKeyHandler.
		/// </summary>
		public void JumpToLatestEventLocation() {
			string message = null;
			Vector2Int? location = null;

			lock (_lock) {
				for (int i = 0; i < _history.Count; i++) {
					if (_history[i].Location.HasValue) {
						message = _history[i].Message;
						location = _history[i].Location;
						break;
					}
				}
			}

			if (!location.HasValue) {
				Speech.Say(Strings.Get("panel.announcement_history.no_event_locations"));
				return;
			}

			var pos = location.Value;
			CloseSilently();
			_mapNavigator.SetCursorPosition(pos.x, pos.y);
			_mapNavigator.MoveCursor(0, 0);  // Announces tile
			Speech.Say(message, interrupt: false);  // Queue after tile announcement
		}
	}
}
