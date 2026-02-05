using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the RewardPickPopup (mid-game cornerstone/perk selection).
	/// Provides flat list navigation through NPC dialogue, cornerstone choices, extend, reroll, and skip.
	/// </summary>
	public class CornerstoneOverlay: MenuBase {
		// Navigation item types
		private enum ItemType { Dialogue, Cornerstone, Extend, Reroll, Skip }

		private class NavItem {
			public ItemType Type;
			public object Model;       // EffectModel (for Cornerstone type only)
			public string Label;       // Announcement text
			public string SearchName;  // Name for type-ahead (cornerstones only)
		}

		// Data
		private object _popup;
		private List<NavItem> _items = new List<NavItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Cornerstone";
		protected override string EmptyMessage => "No options available";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override string GetSearchName(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Type == ItemType.Cornerstone ? _items[index].SearchName : null;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			// 1. NPC dialogue item
			var (npcName, dialogue) = CornerstoneReflection.GetNpcDialogue(_popup);
			if (!string.IsNullOrEmpty(npcName) || !string.IsNullOrEmpty(dialogue)) {
				string dialogueLabel = !string.IsNullOrEmpty(npcName)
					? $"{npcName}: {dialogue}"
					: dialogue;

				_items.Add(new NavItem {
					Type = ItemType.Dialogue,
					Label = dialogueLabel
				});
			}

			// 2. Cornerstone options
			var options = CornerstoneReflection.GetCurrentOptions();
			if (options != null) {
				foreach (var option in options) {
					string rarityText = option.Rarity;
					if (option.IsEthereal)
						rarityText += ", ethereal";

					string label = !string.IsNullOrEmpty(option.Description)
						? $"{option.DisplayName}, {rarityText}. {option.Description}"
						: $"{option.DisplayName}, {rarityText}";

					_items.Add(new NavItem {
						Type = ItemType.Cornerstone,
						Model = option.Model,
						Label = label,
						SearchName = option.DisplayName
					});
				}
			}

			// 3. Extend option (if available)
			if (CornerstoneReflection.CanExtend()) {
				var (extAmount, extGoodName) = CornerstoneReflection.GetExtendCost();
				string extendLabel = CornerstoneReflection.CanAffordExtend()
					? $"Extend, {extAmount} {extGoodName}"
					: $"Extend, {extAmount} {extGoodName}, cannot afford";

				_items.Add(new NavItem {
					Type = ItemType.Extend,
					Label = extendLabel
				});
			}

			// 4. Reroll option (if rerolls remaining)
			int rerolls = CornerstoneReflection.GetRerollsLeft();
			if (rerolls > 0) {
				_items.Add(new NavItem {
					Type = ItemType.Reroll,
					Label = $"Reroll, {rerolls} remaining"
				});
			}

			// 5. Skip option (always available)
			{
				var (skipAmount, skipGoodName) = CornerstoneReflection.GetDeclinePayoff();
				_items.Add(new NavItem {
					Type = ItemType.Skip,
					Label = $"Skip, receive {skipAmount} {skipGoodName}"
				});
			}

			Debug.Log($"[ATSAccessibility] CornerstoneOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			switch (item.Type) {
				case ItemType.Dialogue:
					AnnounceCurrentItem();
					break;
				case ItemType.Cornerstone:
					ActivateCornerstone(item);
					break;
				case ItemType.Extend:
					ActivateExtend();
					break;
				case ItemType.Reroll:
					ActivateReroll();
					break;
				case ItemType.Skip:
					ActivateSkip();
					break;
			}
		}

		// Escape passes to game to close popup (OnPopupHidden will close our overlay)
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// PUBLIC METHODS
		// ========================================

		/// <summary>
		/// Refresh data after the limit popup closes.
		/// If options changed (new pick loaded), announce the new state.
		/// </summary>
		public void RefreshAfterLimit() {
			if (!IsOpen) return;

			RefreshData();
			CurrentIndex = GetFirstCornerstoneIndex();
			if (_items.Count > 0) {
				AnnounceCurrentItem();
			}
		}

		// ========================================
		// ACTIVATION
		// ========================================

		private void ActivateCornerstone(NavItem item) {
			if (!CornerstoneReflection.PickCornerstone(_popup, item.Model)) {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayButtonClick();

			var newOptions = CornerstoneReflection.GetCurrentOptions();
			if (newOptions != null && newOptions.Count > 0) {
				Speech.Say("Picked");
				RefreshData();
				CurrentIndex = GetFirstCornerstoneIndex();
				AnnounceCurrentItem();
			} else {
				Speech.Say("Picked");
				// Popup hides -> OnPopupHidden -> Close()
				// OR limit popup opened -> handled by CornerstoneLimitOverlay
			}
		}

		private void ActivateExtend() {
			if (!CornerstoneReflection.CanAffordExtend()) {
				var (amount, goodName) = CornerstoneReflection.GetExtendCost();
				Speech.Say($"Cannot afford, need {amount} {goodName}");
				SoundManager.PlayFailed();
				return;
			}

			int prevCount = CountCornerstones();

			if (!CornerstoneReflection.Extend()) {
				Speech.Say("Cannot extend");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayButtonClick();
			RefreshData();

			int newCount = CountCornerstones();
			if (newCount > prevCount) {
				CurrentIndex = GetLastCornerstoneIndex();
				AnnounceCurrentItem();
			} else {
				Speech.Say("No new option available");
			}
		}

		private void ActivateReroll() {
			if (!CornerstoneReflection.Reroll(_popup)) {
				Speech.Say("Cannot reroll");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayReroll();
			RefreshData();
			CurrentIndex = GetFirstCornerstoneIndex();
			AnnounceCurrentItem();
		}

		private void ActivateSkip() {
			if (!CornerstoneReflection.Skip(_popup)) {
				Speech.Say("Cannot skip");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayDecline();

			var afterSkip = CornerstoneReflection.GetCurrentOptions();
			if (afterSkip != null && afterSkip.Count > 0) {
				Speech.Say("Skipped");
				RefreshData();
				CurrentIndex = GetFirstCornerstoneIndex();
				AnnounceCurrentItem();
			} else {
				Speech.Say("Skipped");
				// Popup hides -> Close()
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private int GetFirstCornerstoneIndex() {
			for (int i = 0; i < _items.Count; i++)
				if (_items[i].Type == ItemType.Cornerstone) return i;
			return 0;
		}

		private int GetLastCornerstoneIndex() {
			for (int i = _items.Count - 1; i >= 0; i--)
				if (_items[i].Type == ItemType.Cornerstone) return i;
			return 0;
		}

		private int CountCornerstones() {
			int count = 0;
			for (int i = 0; i < _items.Count; i++)
				if (_items[i].Type == ItemType.Cornerstone) count++;
			return count;
		}
	}
}
