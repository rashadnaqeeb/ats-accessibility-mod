using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
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

		private readonly MapNavigator _mapNavigator;
		private bool _suppressCloseAnnouncement;

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

		protected override string OverlayName => "Notifications";
		protected override string EmptyMessage => "No messages";

		protected override int GetItemCount() {
			lock (_lock) { return _history.Count; }
		}

		protected override string GetLabel(int index) {
			lock (_lock) {
				return index >= 0 && index < _history.Count ? _history[index].Message : null;
			}
		}

		protected override void RefreshData() { }  // Static list, no refresh needed

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
			lock (_lock) {
				if (_history.Count == 0)
					return EmptyMessage;
				return $"Notifications. {_history[0].Message}";
			}
		}

		protected override void OnOpened() {
			lock (_lock) {
				if (_history.Count == 0) {
					_suppressCloseAnnouncement = true;
					Close();
					return;
				}
			}
		}

		protected override void OnClosed() {
			if (!_suppressCloseAnnouncement) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say("Notifications closed");
			}
			_suppressCloseAnnouncement = false;
		}

		// Search overrides with locking
		protected override int SearchItemCount { get { lock (_lock) { return _history.Count; } } }

		protected override string GetSearchName(int index) {
			lock (_lock) {
				return index >= 0 && index < _history.Count ? _history[index].Message : null;
			}
		}

		// ========================================
		// NAVIGATION TO EVENTS
		// ========================================

		private void GoToEventLocation() {
			Vector2Int? location;
			lock (_lock) {
				if (_history.Count == 0) return;
				location = _history[CurrentIndex].Location;
			}

			if (!location.HasValue) {
				Speech.Say("No location");
				return;
			}

			var pos = location.Value;
			_suppressCloseAnnouncement = true;
			Close();
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
				Speech.Say("No event locations");
				return;
			}

			var pos = location.Value;
			if (IsOpen) {
				_suppressCloseAnnouncement = true;
				Close();
			}
			_mapNavigator.SetCursorPosition(pos.x, pos.y);
			_mapNavigator.MoveCursor(0, 0);  // Announces tile
			Speech.Say(message, interrupt: false);  // Queue after tile announcement
		}
	}
}
