using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// F3 Rewards panel for quick access to pending rewards.
	/// Always shows all three reward categories (Blueprints, Cornerstones, Newcomers).
	/// Available rewards open the game's popup when selected.
	/// Unavailable rewards show when they will next be available.
	/// </summary>
	public class RewardsPanel: MenuBase {
		private enum RewardType {
			Blueprints,
			Cornerstones,
			Newcomers
		}

		private struct RewardItem {
			public RewardType Type;
			public bool Available;
			public string Label;
		}

		private List<RewardItem> _items = new List<RewardItem>();
		private bool _closingForPopup;

		// ========================================
		// PUBLIC API
		// ========================================

		/// <summary>
		/// Toggle the rewards panel open/closed.
		/// Called from SettlementKeyHandler on F3.
		/// </summary>
		public void Toggle() {
			if (IsOpen) {
				SoundManager.PlayButtonClick();
				Close();
				return;
			}
			Open();
		}

		// ========================================
		// MENUBASE ABSTRACTS
		// ========================================

		protected override string OverlayName => Strings.Get("common.rewards");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].Label;
		}

		protected override void RefreshData() => RefreshItems();

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override void OnAction(int index) => ActivateSelected();

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// F-key panel switching - close self and pass through to open target panel
			if (keyCode == KeyCode.F1 || keyCode == KeyCode.F2) {
				Close();
				return false;  // Let SettlementKeyHandler open the target panel
			}
			// F3 toggles self closed
			if (keyCode == KeyCode.F3) {
				SoundManager.PlayButtonClick();
				Close();
				return true;
			}
			// Consume LeftArrow - flat list, no drill-out
			if (keyCode == KeyCode.LeftArrow)
				return true;
			return null;
		}

		protected override EscapeAction OnEscape() {
			SoundManager.PlayButtonClick();
			return EscapeAction.Close;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count == 0) return OverlayName;
			return _items[0].Label;
		}

		protected override void OnOpened() {
			SoundManager.PlayPopupShow();
		}

		protected override void OnClosed() {
			_items.Clear();
			if (!_closingForPopup) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say(Strings.Get("common.closed"));
			}
			_closingForPopup = false;
		}

		// Search uses short names, not full labels
		private static readonly string[] _searchNameKeys = {
			"panel.rewards.blueprints",
			"common.cornerstones",
			"common.newcomers"
		};

		protected override string GetSearchName(int index) {
			return index >= 0 && index < _searchNameKeys.Length ? Strings.Get(_searchNameKeys[index]) : null;
		}

		// ========================================
		// PRIVATE
		// ========================================

		private void RefreshItems() {
			_items.Clear();

			// Blueprints
			if (RewardsReflection.HasPendingBlueprints()) {
				int count = RewardsReflection.GetPendingBlueprintCount();
				_items.Add(new RewardItem { Type = RewardType.Blueprints, Available = true, Label = Strings.Get("panel.rewards.blueprints_remaining", count) });
			} else {
				var threshold = RewardsReflection.GetNextBlueprintThreshold();
				string label = threshold.HasValue
					? Strings.Get("panel.rewards.blueprints_next", threshold.Value.nextThreshold)
					: Strings.Get("panel.rewards.blueprints_unavailable");
				_items.Add(new RewardItem { Type = RewardType.Blueprints, Available = false, Label = label });
			}

			// Cornerstones
			if (RewardsReflection.HasPendingCornerstones()) {
				_items.Add(new RewardItem { Type = RewardType.Cornerstones, Available = true, Label = Strings.Get("common.cornerstones") });
			} else {
				var nextDate = RewardsReflection.GetNextCornerstoneDate();
				string label = nextDate.HasValue
					? Strings.Get("panel.rewards.cornerstones_next", nextDate.Value.season, nextDate.Value.year)
					: Strings.Get("panel.rewards.cornerstones_unavailable");
				_items.Add(new RewardItem { Type = RewardType.Cornerstones, Available = false, Label = label });
			}

			// Newcomers
			if (RewardsReflection.HasPendingNewcomers()) {
				_items.Add(new RewardItem { Type = RewardType.Newcomers, Available = true, Label = Strings.Get("common.newcomers") });
			} else {
				float time = RewardsReflection.GetTimeToNextNewcomers();
				string label = time > 0
					? Strings.Get("panel.rewards.newcomers_next", RewardsReflection.FormatGameTime(time))
					: Strings.Get("panel.rewards.newcomers_unavailable");
				_items.Add(new RewardItem { Type = RewardType.Newcomers, Available = false, Label = label });
			}

			Debug.Log($"[ATSAccessibility] Rewards panel refreshed: {_items.Count} items");
		}

		private void ActivateSelected() {
			if (_items.Count == 0 || CurrentIndex >= _items.Count) return;

			var item = _items[CurrentIndex];

			if (!item.Available) {
				// Re-announce the label for read-only items
				Speech.Say(item.Label);
				return;
			}

			Debug.Log($"[ATSAccessibility] Opening {item.Label} from Rewards panel");

			bool success = false;

			switch (item.Type) {
				case RewardType.Blueprints:
					success = RewardsReflection.OpenBlueprintsPopup();
					break;

				case RewardType.Cornerstones:
					success = RewardsReflection.OpenCornerstonesPopup();
					break;

				case RewardType.Newcomers:
					success = RewardsReflection.OpenNewcomersPopup();
					break;
			}

			if (success) {
				_closingForPopup = true;
				Close();
				Debug.Log($"[ATSAccessibility] Successfully opened {item.Label}");
			} else {
				Speech.Say(Strings.Get("panel.rewards.unavailable", item.Label));
				Debug.Log($"[ATSAccessibility] Failed to open {item.Label}");
			}
		}
	}
}
