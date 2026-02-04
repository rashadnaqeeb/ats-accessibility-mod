using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
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

		protected override string OverlayName => "Trends";

		protected override string EmptyMessage => "No goods with trend data";

		protected override int GetItemCount() => _operations.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _operations.Count) return null;
			var op = _operations[index];
			return $"{op.DisplayName}: {FormatAmount(op.TotalAmount)}";
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
			return $"{goodName}, {changeText}";
		}

		protected override void OnClosed() {
			_popup = null;
			_goods.Clear();
			_operations.Clear();
		}

		// ========================================
		// SPECIAL KEY HANDLING
		// ========================================

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				// Time frame toggles
				case KeyCode.Alpha1:
				case KeyCode.Keypad1:
					SetTimeFrame(TICKS_10_SECONDS, "Last 10 seconds");
					return true;

				case KeyCode.Alpha2:
				case KeyCode.Keypad2:
					SetTimeFrame(TICKS_1_MINUTE, "Last minute");
					return true;

				case KeyCode.Alpha3:
				case KeyCode.Keypad3:
					SetTimeFrame(TICKS_5_MINUTES, "Last 5 minutes");
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
				Speech.Say($"{timeFrameLabel}. No goods");
				return;
			}

			string goodName = GetCurrentGoodDisplayName();
			string changeText = FormatNetChange(GetNetChangeFromOperations());
			Speech.Say($"{timeFrameLabel}. {goodName}, {changeText}");
		}

		// ========================================
		// GOODS HELPERS
		// ========================================

		private void AnnounceCurrentGood() {
			if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count) {
				Speech.Say("No goods");
				return;
			}

			string goodName = GetCurrentGoodDisplayName();
			string changeText = FormatNetChange(GetNetChangeFromOperations());
			Speech.Say($"{goodName}, {changeText}");
		}

		private string GetCurrentGoodDisplayName() {
			if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count)
				return "Unknown";

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
				return "no changes";
			else if (amount > 0)
				return $"net +{amount}";
			else
				return $"net {amount}";
		}

		private string FormatAmount(int amount) {
			if (amount > 0)
				return $"+{amount}";
			else
				return amount.ToString();
		}
	}
}
