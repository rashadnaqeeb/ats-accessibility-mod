using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Accessible overlay for the OrderPickPopup (order pick option selection).
	/// Provides flat list navigation through pick options with objectives and rewards.
	/// </summary>
	public class OrderPickOverlay: MenuBase, IKeyHandler {
		private class PickItem {
			public object PickState;    // OrderPickState
			public object OrderModel;   // OrderModel resolved from pick.model
			public bool Failed;
			public string Label;        // Announcement text
		}

		// Data
		private object _popup;         // The OrderPickPopup instance
		private object _orderState;    // The order being picked
		private List<PickItem> _items = new List<PickItem>();

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		public bool IsActive => IsOpen;

		bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
			ProcessKey(keyCode, modifiers);

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Pick order";
		protected override string EmptyMessage => "No options available";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			_orderState = OrdersReflection.GetPopupOrder(_popup);
			if (_orderState == null) {
				Debug.LogWarning("[ATSAccessibility] OrderPickOverlay: Could not read order from popup");
				return;
			}

			var picks = OrdersReflection.GetPicksFor(_orderState);
			if (picks == null || picks.Count == 0) return;

			foreach (var pick in picks) {
				if (pick == null) continue;

				var orderModel = OrdersReflection.GetPickOrderModel(pick);
				if (orderModel == null) continue;

				bool failed = OrdersReflection.IsPickFailed(pick) && OrdersReflection.CanBeFailed(orderModel);
				string label = BuildPickLabel(pick, orderModel, failed);

				_items.Add(new PickItem {
					PickState = pick,
					OrderModel = orderModel,
					Failed = failed,
					Label = label
				});
			}

			Debug.Log($"[ATSAccessibility] OrderPickOverlay refreshed: {_items.Count} picks");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];

			if (item.Failed) {
				Speech.Say("Expired");
				SoundManager.PlayFailed();
				return;
			}

			if (_orderState == null) {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
				return;
			}

			if (OrdersReflection.PickOrder(_orderState, item.PickState)) {
				SoundManager.PlayButtonClick();
				Speech.Say("Selected");
				// Hide the popup (mirrors OrderPickPopup.OnPicked behavior)
				OrdersReflection.HidePopup(_popup);
			} else {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
			}
		}

		protected override int SearchItemCount => 0; // No search for order picks

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.S) {
				AnnounceStoredAmounts();
				return true;
			}
			return null;
		}

		// Escape passes to game to close popup (OnPopupHidden will close our overlay)
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count == 0) return EmptyMessage;

			// Skip to first non-failed item for initial announcement
			int firstValid = GetFirstNonFailedIndex();
			if (firstValid >= 0) {
				CurrentIndex = firstValid;
				return $"{OverlayName}. {_items[firstValid].Label}";
			}
			return $"{OverlayName}. {_items[0].Label}";
		}

		protected override void OnClosed() {
			_popup = null;
			_orderState = null;
			_items.Clear();
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void AnnounceStoredAmounts() {
			if (_items.Count == 0 || CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

			var item = _items[CurrentIndex];
			if (item.Failed) {
				Speech.Say("Expired");
				return;
			}

			int setIndex = OrdersReflection.GetPickSetIndex(item.PickState);
			var amounts = OrdersReflection.GetPickStoredAmounts(item.OrderModel, setIndex);

			if (amounts.Count == 0) {
				Speech.Say("No storage info");
				return;
			}

			Speech.Say(string.Join(", ", amounts));
		}

		// ========================================
		// HELPERS
		// ========================================

		private int GetFirstNonFailedIndex() {
			for (int i = 0; i < _items.Count; i++) {
				if (!_items[i].Failed) return i;
			}
			return -1;
		}

		private string BuildPickLabel(object pickState, object orderModel, bool failed) {
			string name = OrdersReflection.GetOrderDisplayName(orderModel) ?? "Unknown";

			if (failed) {
				return $"{name}, expired";
			}

			int setIndex = OrdersReflection.GetPickSetIndex(pickState);
			bool timed = OrdersReflection.CanBeFailed(orderModel);

			var objectives = OrdersReflection.GetPickObjectiveTexts(orderModel, setIndex);
			string objText = objectives.Count > 0 ? "Requirements: " + string.Join(", ", objectives) : "";

			var rewards = OrdersReflection.GetPickRewardTexts(pickState);
			string repReward = OrdersReflection.GetReputationRewardText(orderModel);
			if (!string.IsNullOrEmpty(repReward))
				rewards.Add(repReward);
			string rewardText = rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";

			var parts = new List<string>();
			if (timed) {
				float timeToFail = OrdersReflection.GetTimeToFail(orderModel);
				parts.Add($"{name}, timed {OrdersReflection.FormatTime(timeToFail)}");
			} else {
				parts.Add(name);
			}

			if (!string.IsNullOrEmpty(objText))
				parts.Add(objText);
			if (!string.IsNullOrEmpty(rewardText))
				parts.Add(rewardText);

			var warnings = OrdersReflection.GetPickWarningTexts(orderModel, setIndex);
			if (warnings.Count > 0)
				parts.Add("Warning: " + string.Join(", ", warnings));

			return string.Join(". ", parts);
		}
	}
}
