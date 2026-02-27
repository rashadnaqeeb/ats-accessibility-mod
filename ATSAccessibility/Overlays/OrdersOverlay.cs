using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the OrdersPopup (order list navigation).
	/// Provides flat list navigation through all orders with front-loaded announcements.
	/// </summary>
	public class OrdersOverlay: MenuBase {
		// Order status for sorting and announcement
		private enum OrderStatus { ToPick, Completable, Active, Locked, Completed, Failed }

		private class OrderItem {
			public object State;       // OrderState
			public object Model;       // OrderModel
			public string Label;       // Announcement text
			public OrderStatus Status;
			public bool Tracked;
		}

		// Data
		private List<OrderItem> _items = new List<OrderItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Orders";
		protected override string EmptyMessage => "No orders available";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			var orders = OrdersReflection.GetOrders();
			if (orders == null) return;

			int slotNum = 0;
			foreach (var orderState in orders) {
				slotNum++;
				if (orderState == null) continue;

				var model = OrdersReflection.GetOrderModel(orderState);
				var status = DetermineStatus(orderState, model);
				var statusLabel = BuildLabel(orderState, model, status);
				var tracked = OrdersReflection.IsTracked(orderState);

				_items.Add(new OrderItem {
					State = orderState,
					Model = model,
					Label = $"{slotNum}: {statusLabel}",
					Status = status,
					Tracked = tracked
				});
			}

			Debug.Log($"[ATSAccessibility] OrdersOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			switch (item.Status) {
				case OrderStatus.ToPick:
					if (OrdersReflection.FireOrderPickPopupRequested(item.State)) {
						SoundManager.PlayButtonClick();
					} else {
						Speech.Say("Cannot open picks");
						SoundManager.PlayFailed();
					}
					break;

				case OrderStatus.Completable:
					string name = OrdersReflection.GetOrderDisplayName(item.Model) ?? "order";
					if (OrdersReflection.CompleteOrder(item.State, item.Model)) {
						SoundManager.PlayButtonClick();
						Speech.Say($"Delivered, {name}");
						RefreshData();
						if (CurrentIndex >= _items.Count)
							CurrentIndex = _items.Count > 0 ? _items.Count - 1 : 0;
						if (_items.Count > 0)
							AnnounceCurrentItem();
					} else {
						Speech.Say("Cannot deliver");
						SoundManager.PlayFailed();
					}
					break;

				default:
					AnnounceCurrentItem();
					break;
			}
		}

		protected override int SearchItemCount => 0; // No search for orders

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.T) {
				ToggleTracking();
				return true;
			}
			return null;
		}

		// Escape passes to game to close popup (OnPopupHidden will close our overlay)
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		private static readonly List<HelpEntry> _ordersHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			new HelpEntry("T", "Toggle tracking"),
		};
		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _ordersHelpEntries;

		protected override void OnClosed() {
			_items.Clear();
		}

		// ========================================
		// PUBLIC METHODS
		// ========================================

		/// <summary>
		/// Refresh data after the OrderPickPopup closes.
		/// The picked order is now Active, so re-announce current.
		/// </summary>
		public void RefreshAfterPick() {
			if (!IsOpen) return;

			RefreshData();
			if (CurrentIndex >= _items.Count)
				CurrentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

			if (_items.Count > 0) {
				AnnounceCurrentItem();
			}
		}

		/// <summary>
		/// Refresh data when a new order becomes available.
		/// Called by EventAnnouncer when OnOrderStarted fires.
		/// </summary>
		public void RefreshOnNewOrder() {
			if (!IsOpen) return;

			RefreshData();
			if (CurrentIndex >= _items.Count)
				CurrentIndex = _items.Count > 0 ? _items.Count - 1 : 0;

			Speech.Say("Orders updated");
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void ToggleTracking() {
			if (_items.Count == 0 || CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

			var item = _items[CurrentIndex];

			if (item.Status != OrderStatus.Active && item.Status != OrderStatus.Completable) {
				Speech.Say("Cannot track");
				SoundManager.PlayFailed();
				return;
			}

			if (OrdersReflection.ToggleTracking(item.State)) {
				bool nowTracked = OrdersReflection.IsTracked(item.State);
				item.Tracked = nowTracked;
				Speech.Say(nowTracked ? "Tracked" : "Untracked");
				SoundManager.PlayButtonClick();
			} else {
				Speech.Say("Cannot toggle tracking");
			}
		}

		// ========================================
		// DATA HELPERS
		// ========================================

		private OrderStatus DetermineStatus(object orderState, object orderModel) {
			if (OrdersReflection.IsFailed(orderState))
				return OrderStatus.Failed;
			if (OrdersReflection.IsCompleted(orderState))
				return OrderStatus.Completed;
			if (!OrdersReflection.IsStarted(orderState))
				return OrderStatus.Locked;
			if (!OrdersReflection.IsPicked(orderState))
				return OrderStatus.ToPick;
			if (orderModel == null)
				return OrderStatus.Active;
			if (OrdersReflection.CanComplete(orderState, orderModel))
				return OrderStatus.Completable;
			return OrderStatus.Active;
		}

		private string BuildLabel(object orderState, object orderModel, OrderStatus status) {
			string name = orderModel != null
				? (OrdersReflection.GetOrderDisplayName(orderModel) ?? "Unknown order")
				: null;

			switch (status) {
				case OrderStatus.Locked:
					return BuildLockedLabel(orderState, orderModel);

				case OrderStatus.ToPick:
					return "Ready to pick";

				case OrderStatus.Completable:
					return $"{name}, ready to deliver. {BuildRewardText(orderState, orderModel)}";

				case OrderStatus.Active:
					return BuildActiveLabel(name, orderState, orderModel);

				case OrderStatus.Completed:
					return $"{name}, completed. {BuildRewardText(orderState, orderModel)}";

				case OrderStatus.Failed:
					return $"{name}, failed";

				default:
					return name ?? "Unknown order";
			}
		}

		private string BuildLockedLabel(object orderState, object orderModel) {
			if (orderModel != null && OrdersReflection.HasUnlockAfter(orderModel)) {
				string prereqName = OrdersReflection.GetUnlockAfterName(orderModel) ?? "another order";
				return $"Locked, requires {prereqName}";
			}

			float startTime = OrdersReflection.GetStartTime(orderState);
			float gameTime = OrdersReflection.GetGameTime();
			float remaining = startTime - gameTime;
			if (remaining > 0) {
				return $"Locked, unlocks in {FormattingUtils.FormatTime(remaining)}";
			}

			return "Locked";
		}

		private string BuildActiveLabel(string name, object orderState, object orderModel) {
			var objectives = OrdersReflection.GetObjectiveTexts(orderModel, orderState);
			string objText = objectives.Count > 0 ? string.Join(", ", objectives) : "";

			var rewards = OrdersReflection.GetRewardTexts(orderState);
			string repReward = OrdersReflection.GetReputationRewardText(orderModel);
			if (!string.IsNullOrEmpty(repReward))
				rewards.Add(repReward);
			string rewardText = rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";

			var parts = new List<string>();

			bool timed = OrdersReflection.CanBeFailed(orderModel);
			if (timed) {
				float timeLeft = OrdersReflection.GetTimeLeft(orderState);
				string timeStr = FormattingUtils.FormatTime(timeLeft);
				parts.Add($"{name}, {timeStr}");
			} else {
				parts.Add(name);
			}

			if (!string.IsNullOrEmpty(objText))
				parts.Add(objText);
			if (!string.IsNullOrEmpty(rewardText))
				parts.Add(rewardText);

			return string.Join(". ", parts);
		}

		private string BuildRewardText(object orderState, object orderModel) {
			var rewards = OrdersReflection.GetRewardTexts(orderState);
			string repReward = OrdersReflection.GetReputationRewardText(orderModel);
			if (!string.IsNullOrEmpty(repReward))
				rewards.Add(repReward);
			return rewards.Count > 0 ? "Rewards: " + string.Join(", ", rewards) : "";
		}
	}
}
