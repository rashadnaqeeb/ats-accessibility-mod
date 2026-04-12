using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the TrendsPopup.
	/// Provides navigation through goods and their storage operations.
	/// Number keys toggle time frame for aggregating operations.
	///
	/// This overlay has parallel navigation axes: Left/Right navigates goods,
	/// Up/Down navigates operations for the current good. MenuBase Level 0
	/// tracks the operation index; _goodIndex is a separate axis.
	/// </summary>
	public class TrendsOverlay: MenuBase {
		// Time frame options (in ticks)
		private const int TICKS_10_SECONDS = 1;
		private const int TICKS_1_MINUTE = 6;
		private const int TICKS_5_MINUTES = 30;

		// State
		private object _popup;
		private int _timeFrameTicks = TICKS_1_MINUTE;  // Default: 1 minute

		// Goods list (separate navigation axis, not managed by MenuBase)
		private List<string> _goods = new List<string>();
		private int _goodIndex;

		// Operations for current good (navigated via MenuBase Level 0)
		private List<TrendsReflection.AggregatedOperation> _operations = new List<TrendsReflection.AggregatedOperation>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.trends");

		protected override string EmptyMessage => Strings.Get("overlay.trends.empty");

		protected override int GetItemCount() => _operations.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _operations.Count) return null;
			var op = _operations[index];
			return Strings.Get("overlay.trends.operation", op.DisplayName, FormatAmount(op.TotalAmount));
		}

		protected override void RefreshData() {
			_goods = TrendsReflection.GetAllGoods();

			if (_goods.Count > 0) {
				// Try to start with the good selected in the popup
				string currentGood = _popup != null ? TrendsReflection.GetCurrentGood(_popup) : null;
				if (!string.IsNullOrEmpty(currentGood)) {
					int idx = _goods.IndexOf(currentGood);
					if (idx >= 0)
						_goodIndex = idx;
				}

				RefreshOperations();
			}
		}

		protected override EnterAction OnEnter(int index) => EnterAction.None;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			if (_goods.Count == 0) return EmptyMessage;

			string goodName = GetCurrentGoodDisplayName();
			string changeText = FormatNetChange(GetNetChangeFromOperations());
			return Strings.Get("overlay.trends.announce", goodName, changeText);
		}

		protected override void OnClosed() {
			_popup = null;
			_goods.Clear();
			_operations.Clear();
		}

		private static readonly List<HelpEntry> _trendsHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			HelpEntry.Loca("Left/Right", "overlay.trends.help.goods"),
			HelpEntry.Loca("1", "overlay.trends.help.10sec"),
			HelpEntry.Loca("2", "overlay.trends.help.1min"),
			HelpEntry.Loca("3", "overlay.trends.help.5min"),
		};
		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _trendsHelpEntries;

		// ========================================
		// SPECIAL KEY HANDLING
		// ========================================

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				// Time frame toggles
				case KeyCode.Alpha1:
				case KeyCode.Keypad1:
					SetTimeFrame(TICKS_10_SECONDS, Strings.Get("overlay.trends.range.10sec"));
					return true;

				case KeyCode.Alpha2:
				case KeyCode.Keypad2:
					SetTimeFrame(TICKS_1_MINUTE, Strings.Get("overlay.trends.range.1min"));
					return true;

				case KeyCode.Alpha3:
				case KeyCode.Keypad3:
					SetTimeFrame(TICKS_5_MINUTES, Strings.Get("overlay.trends.range.5min"));
					return true;

				// Goods navigation (separate axis)
				case KeyCode.LeftArrow:
					if (_goods.Count > 0) {
						_goodIndex = NavigationUtils.WrapIndex(_goodIndex, -1, _goods.Count);
						RefreshOperations();
						AnnounceCurrentGood();
					}
					return true;

				case KeyCode.RightArrow:
					if (_goods.Count > 0) {
						_goodIndex = NavigationUtils.WrapIndex(_goodIndex, 1, _goods.Count);
						RefreshOperations();
						AnnounceCurrentGood();
					}
					return true;

				case KeyCode.Escape:
					// Pass to game to close popup
					return false;

				default:
					// Let Up/Down/Home/End go through standard nav for operations
					return null;
			}
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		// ========================================
		// SEARCH OVERRIDES (searches goods, not operations)
		// ========================================

		protected override int SearchItemCount => _goods.Count;

		protected override int SearchCurrentIndex => _goodIndex;

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _goods.Count) return null;
			return TrendsReflection.GetGoodDisplayName(_goods[index]);
		}

		protected override void SearchMoveTo(int index) {
			_goodIndex = index;
			RefreshOperations();
			AnnounceCurrentGood();
		}

		// ========================================
		// OPERATIONS
		// ========================================

		private void RefreshOperations() {
			CurrentIndex = 0;

			if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count) {
				_operations = new List<TrendsReflection.AggregatedOperation>();
				return;
			}

			_operations = TrendsReflection.GetAggregatedOperations(_goods[_goodIndex], _timeFrameTicks);
		}

		// ========================================
		// TIME FRAME
		// ========================================

		private void SetTimeFrame(int ticks, string label) {
			if (_timeFrameTicks == ticks) {
				// Already on this time frame, just announce
				AnnounceTimeFrameAndGood(label);
				return;
			}

			_timeFrameTicks = ticks;
			RefreshOperations();
			AnnounceTimeFrameAndGood(label);
		}

		private void AnnounceTimeFrameAndGood(string timeFrameLabel) {
			if (_goods.Count == 0) {
				Speech.Say(Strings.Get("overlay.trends.time_frame_empty", timeFrameLabel));
				return;
			}

			string goodName = GetCurrentGoodDisplayName();
			string changeText = FormatNetChange(GetNetChangeFromOperations());
			Speech.Say(Strings.Get("overlay.trends.time_frame", timeFrameLabel, goodName, changeText));
		}

		// ========================================
		// GOODS HELPERS
		// ========================================

		private void AnnounceCurrentGood() {
			if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count) {
				Speech.Say(Strings.Get("overlay.trends.no_goods"));
				return;
			}

			string goodName = GetCurrentGoodDisplayName();
			string changeText = FormatNetChange(GetNetChangeFromOperations());
			Speech.Say(Strings.Get("overlay.trends.announce", goodName, changeText));
		}

		private string GetCurrentGoodDisplayName() {
			if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count)
				return Strings.Get("common.unknown");

			return TrendsReflection.GetGoodDisplayName(_goods[_goodIndex]);
		}

		private int GetNetChangeFromOperations() {
			int net = 0;
			for (int i = 0; i < _operations.Count; i++)
				net += _operations[i].TotalAmount;
			return net;
		}

		// ========================================
		// FORMATTING
		// ========================================

		private string FormatNetChange(int amount) {
			if (amount == 0)
				return Strings.Get("overlay.trends.net.zero");
			else if (amount > 0)
				return Strings.Get("overlay.trends.net.pos", amount);
			else
				return Strings.Get("overlay.trends.net.neg", amount);
		}

		private string FormatAmount(int amount) {
			if (amount > 0)
				return Strings.Get("overlay.trends.amount.pos", amount);
			else
				return amount.ToString();
		}
	}
}
