using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Handles keyboard-based tree marking/unmarking with rectangle and single selection modes.
	/// Active only when in mark/unmark mode (entered via Enter on NaturalResource).
	/// </summary>
	public class HarvestMarkHandler: IKeyHandler, IHelpProvider {
		private enum Mode { None, Mark, Unmark }
		private enum SelMode { Rectangle, Single }
		private enum RectPhase { Idle, WaitingForSecond }

		private Mode _mode = Mode.None;
		private SelMode _selMode = SelMode.Rectangle;
		private RectPhase _rectPhase = RectPhase.Idle;
		private Vector2Int _firstCorner;
		private readonly HashSet<Vector2Int> _selectedPositions = new HashSet<Vector2Int>();
		private bool _awaitingGladeConfirm = false;

		private readonly MapNavigator _mapNavigator;

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Space", Strings.Get("handler.harvest_mark.help.select_deselect")),
			new HelpEntry("Tab", Strings.Get("handler.harvest_mark.help.toggle_mode")),
			new HelpEntry("Enter", Strings.Get("handler.harvest_mark.help.commit")),
			new HelpEntry("Escape", Strings.Get("handler.harvest_mark.help.cancel")),
			new HelpEntry("C", Strings.Get("handler.harvest_mark.help.select_all_marked")),
		};

		private static readonly List<string> _passthroughKeys = new List<string> {
			"Arrows", "Ctrl+Arrows", "PageUp/Down", "Alt+PageUp/Down", "Home", "End", "K", "I", "B",
		};

		public HelpBehavior HelpBehavior => HelpBehavior.SelectivePassthrough;
		public string HelpContextName => "Tree Marking";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => _passthroughKeys;

		public bool IsActive => _mode != Mode.None;

		public HarvestMarkHandler(MapNavigator mapNavigator) {
			_mapNavigator = mapNavigator;
			MenuBase.OnAnyMenuOpened += () => { if (IsActive) ExitMode(); };
		}

		/// <summary>
		/// Enter mark or unmark mode.
		/// </summary>
		/// <param name="isUnmark">True for unmark mode, false for mark mode</param>
		public void EnterMode(bool isUnmark) {
			_mode = isUnmark ? Mode.Unmark : Mode.Mark;
			_selMode = SelMode.Rectangle;
			_rectPhase = RectPhase.Idle;
			_awaitingGladeConfirm = false;
			_selectedPositions.Clear();
			_mapNavigator.AnnouncementPrefix = GetAnnouncementPrefix;

			string modeStr = isUnmark ? Strings.Get("handler.harvestmark.mode_unmark") : Strings.Get("handler.harvestmark.mode_mark");
			Speech.Say(Strings.Get("handler.harvestmark.mode_entered", modeStr));
		}

		private void ExitMode(bool announce = true) {
			if (announce)
				Speech.Say(Strings.Get("common.cancelled"));
			_mode = Mode.None;
			_rectPhase = RectPhase.Idle;
			_awaitingGladeConfirm = false;
			_selectedPositions.Clear();
			_mapNavigator.AnnouncementPrefix = null;
			InputBlocker.BlockCancelOnce = true;
		}

		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				// Pass through arrow keys for navigation
				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
				case KeyCode.LeftArrow:
				case KeyCode.RightArrow:
					_awaitingGladeConfirm = false;
					return false; // Pass to SettlementKeyHandler -> MapNavigator

				// Pass through scanner keys
				case KeyCode.PageUp:
				case KeyCode.PageDown:
				case KeyCode.Home:
				case KeyCode.End:
					_awaitingGladeConfirm = false;
					return false; // Pass through for scanner

				// Pass through info keys
				case KeyCode.K:
				case KeyCode.I:
					_awaitingGladeConfirm = false;
					return false; // Pass through for position/info

				case KeyCode.Space:
					_awaitingGladeConfirm = false;
					HandleSpace();
					return true;

				case KeyCode.Tab:
					_awaitingGladeConfirm = false;
					ToggleSelectionMode();
					return true;

				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (_awaitingGladeConfirm)
						DoCommit();
					else
						CommitSelection();
					return true;

				case KeyCode.Escape:
					ExitMode();
					return true;

				case KeyCode.C:
					_awaitingGladeConfirm = false;
					if (_mode == Mode.Unmark) {
						SelectAllMarked();
						return true;
					}
					// Consume in mark mode
					return true;

				// Pass B/Shift+B to game
				case KeyCode.B:
					return false;

				default:
					// Consume unknown keys without clearing glade confirm
					return true;
			}
		}

		private void HandleSpace() {
			if (_selMode == SelMode.Rectangle)
				HandleSpaceRectangle();
			else
				HandleSpaceSingle();
		}

		private void HandleSpaceRectangle() {
			var cursorPos = new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY);

			// Deselect if already selected, regardless of rect phase
			if (_selectedPositions.Contains(cursorPos)) {
				_selectedPositions.Remove(cursorPos);
				// Reset rect phase when deselecting during WaitingForSecond
				if (_rectPhase == RectPhase.WaitingForSecond)
					_rectPhase = RectPhase.Idle;
				Speech.Say(Strings.Get("handler.harvestmark.deselected"));
				return;
			}

			if (_rectPhase == RectPhase.Idle) {
				// Setting first corner - must be on a valid resource
				if (!MapReflection.HasNaturalResourceAt(cursorPos)) {
					Speech.Say(Strings.Get("common.no_tree_here"));
					return;
				}

				if (_mode == Mode.Unmark && !IsMarkedAt(cursorPos)) {
					Speech.Say(Strings.Get("handler.harvestmark.not_marked"));
					return;
				}

				_firstCorner = cursorPos;
				_rectPhase = RectPhase.WaitingForSecond;
				Speech.Say(Strings.Get("handler.harvestmark.first_corner"));
			} else {
				// WaitingForSecond - calculate rectangle and select resources within
				int minX = Mathf.Min(_firstCorner.x, cursorPos.x);
				int maxX = Mathf.Max(_firstCorner.x, cursorPos.x);
				int minY = Mathf.Min(_firstCorner.y, cursorPos.y);
				int maxY = Mathf.Max(_firstCorner.y, cursorPos.y);

				int count = 0;
				var allPositions = MapReflection.GetAllNaturalResourcePositions();
				foreach (var pos in allPositions) {
					if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY)
						continue;

					if (_mode == Mode.Unmark && !IsMarkedAt(pos))
						continue;

					if (_mode == Mode.Mark && IsMarkedAt(pos))
						continue;

					_selectedPositions.Add(pos);
					count++;
				}

				_rectPhase = RectPhase.Idle;
				_selMode = SelMode.Single;

				if (count > 0)
					Speech.Say(Strings.Get("handler.harvestmark.rect_selected", count));
				else
					Speech.Say(Strings.Get("handler.harvestmark.rect_empty"));
			}
		}

		private void HandleSpaceSingle() {
			var cursorPos = new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY);

			// Toggle if already selected
			if (_selectedPositions.Contains(cursorPos)) {
				_selectedPositions.Remove(cursorPos);
				Speech.Say(Strings.Get("handler.harvestmark.deselected"));
				return;
			}

			// Must be on a NaturalResource
			if (!MapReflection.HasNaturalResourceAt(cursorPos)) {
				Speech.Say(Strings.Get("common.no_tree_here"));
				return;
			}

			// Unmark mode: must be marked
			if (_mode == Mode.Unmark && !IsMarkedAt(cursorPos)) {
				Speech.Say(Strings.Get("handler.harvestmark.not_marked"));
				return;
			}

			// Mark mode: skip already marked
			if (_mode == Mode.Mark && IsMarkedAt(cursorPos)) {
				Speech.Say(Strings.Get("handler.harvestmark.already_marked"));
				return;
			}

			_selectedPositions.Add(cursorPos);
			Speech.Say(Strings.Get("common.selected"));
		}

		private void ToggleSelectionMode() {
			if (_selMode == SelMode.Rectangle) {
				_selMode = SelMode.Single;
				Speech.Say(Strings.Get("handler.harvestmark.single_select"));
			} else {
				_selMode = SelMode.Rectangle;
				Speech.Say(Strings.Get("handler.harvestmark.rectangle_select"));
			}

			// Reset rect phase when switching modes
			_rectPhase = RectPhase.Idle;
		}

		private void SelectAllMarked() {
			var allPositions = MapReflection.GetAllNaturalResourcePositions();
			int count = 0;

			foreach (var pos in allPositions) {
				if (IsMarkedAt(pos)) {
					_selectedPositions.Add(pos);
					count++;
				}
			}

			if (count > 0)
				Speech.Say(Strings.Get("handler.harvestmark.c_selected", count));
			else
				Speech.Say(Strings.Get("handler.harvestmark.none_marked"));
		}

		private void CommitSelection() {
			if (_selectedPositions.Count == 0) {
				Speech.Say(Strings.Get("handler.harvestmark.nothing_selected"));
				return;
			}

			// Check for glade edge trees (mark mode only)
			if (_mode == Mode.Mark) {
				int gladeEdgeCount = 0;
				foreach (var pos in _selectedPositions) {
					if (MapReflection.IsNaturalResourceGladeEdge(pos))
						gladeEdgeCount++;
				}

				if (gladeEdgeCount > 0) {
					_awaitingGladeConfirm = true;
					string treeWord = gladeEdgeCount == 1 ? Strings.Get("handler.harvestmark.tree") : Strings.Get("handler.harvestmark.trees");
					Speech.Say(Strings.Get("handler.harvestmark.glade_edge_warning", gladeEdgeCount, treeWord));
					return;
				}
			}

			DoCommit();
		}

		private void DoCommit() {
			int count = 0;

			if (_mode == Mode.Mark) {
				foreach (var pos in _selectedPositions) {
					if (MapReflection.MarkNaturalResourceAt(pos))
						count++;
				}

				string treeWord = count == 1 ? Strings.Get("handler.harvestmark.tree") : Strings.Get("handler.harvestmark.trees");
				Speech.Say(Strings.Get("handler.harvestmark.marked", count, treeWord));
			} else {
				foreach (var pos in _selectedPositions) {
					if (MapReflection.UnmarkNaturalResourceAt(pos))
						count++;
				}

				string treeWord = count == 1 ? Strings.Get("handler.harvestmark.tree") : Strings.Get("handler.harvestmark.trees");
				Speech.Say(Strings.Get("handler.harvestmark.unmarked", count, treeWord));
			}

			ExitMode(announce: false);
		}

		private string GetAnnouncementPrefix(int x, int y) {
			var pos = new Vector2Int(x, y);

			if (_rectPhase == RectPhase.WaitingForSecond && pos == _firstCorner)
				return Strings.Get("handler.harvestmark.prefix_first_corner");

			if (_selectedPositions.Contains(pos))
				return Strings.Get("common.selected_lower");

			return null;
		}

		private bool IsMarkedAt(Vector2Int pos) {
			var resource = MapReflection.GetNaturalResourceAt(pos);
			if (resource == null) return false;
			return MapReflection.IsNaturalResourceMarked(resource);
		}
	}
}
