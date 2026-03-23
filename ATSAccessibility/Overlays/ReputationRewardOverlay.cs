using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the ReputationRewardsPopup (mid-game blueprint reward selection).
	/// Provides flat list navigation through building choices plus extend and reroll options.
	/// </summary>
	public class ReputationRewardOverlay: MenuBase {
		// Navigation item types
		private enum ItemType { Building, Extend, Reroll }

		private class NavItem {
			public ItemType Type;
			public object Model;       // BuildingModel (for Building type only)
			public string Label;       // Announcement text
			public string SearchName;  // Name for type-ahead (buildings only)
		}

		// Data
		private object _popup;
		private List<NavItem> _items = new List<NavItem>();

		// Encyclopedia navigation
		private EncyclopediaNavigator _encyclopediaNavigator;

		// Flag to suppress EventAnnouncer's "New blueprint available" when we announce description
		public static bool SuppressBlueprintAnnouncement { get; private set; }

		/// <summary>Defensive reset for scene transitions.</summary>
		public static void ResetSuppression() {
			SuppressBlueprintAnnouncement = false;
		}

		public void SetEncyclopediaNavigator(EncyclopediaNavigator navigator) {
			_encyclopediaNavigator = navigator;
		}

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Reputation reward";
		protected override string EmptyMessage => "No options available";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override string GetSearchName(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Type == ItemType.Building ? _items[index].SearchName : null;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			// Add buildings
			var options = ReputationRewardReflection.GetCurrentOptions();
			if (options != null) {
				foreach (var option in options) {
					string label = BuildBuildingLabel(option);

					_items.Add(new NavItem {
						Type = ItemType.Building,
						Model = option.Model,
						Label = label,
						SearchName = option.DisplayName
					});
				}
			}

			// Add extend option if available
			if (ReputationRewardReflection.CanExtend()) {
				var (extAmount, extGoodName) = ReputationRewardReflection.GetExtendCost();
				string extendLabel = ReputationRewardReflection.CanAffordExtend()
					? $"Extend, {extAmount} {extGoodName}"
					: $"Extend, {extAmount} {extGoodName}, cannot afford";

				_items.Add(new NavItem {
					Type = ItemType.Extend,
					Label = extendLabel
				});
			}

			// Add reroll option if unlocked via meta progression
			if (ConstructionReflection.IsBlueprintRerollUnlocked()) {
				var (rerollAmount, rerollGoodName) = ReputationRewardReflection.GetRerollCost();
				string rerollLabel = ReputationRewardReflection.CanAffordReroll()
					? $"Reroll, {rerollAmount} {rerollGoodName}"
					: $"Reroll, {rerollAmount} {rerollGoodName}, cannot afford";

				_items.Add(new NavItem {
					Type = ItemType.Reroll,
					Label = rerollLabel
				});
			}

			Debug.Log($"[ATSAccessibility] ReputationRewardOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			switch (item.Type) {
				case ItemType.Building:
					ActivateBuilding(item);
					break;
				case ItemType.Extend:
					ActivateExtend();
					break;
				case ItemType.Reroll:
					ActivateReroll();
					break;
			}
		}

		// Escape passes to game to close popup (OnPopupHidden will close our overlay)
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// Shift+W: Open encyclopedia for focused building
			if (keyCode == KeyCode.W && modifiers.Shift && _encyclopediaNavigator != null) {
				if (CurrentIndex >= 0 && CurrentIndex < _items.Count && _items[CurrentIndex].Type == ItemType.Building) {
					var model = _items[CurrentIndex].Model;
					_encyclopediaNavigator.SetPendingBuilding(model);
					WikiReflection.OpenBuildingInEncyclopedia(model);
					return true;
				}
			}
			return null;
		}

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			// Get popup description (includes tutorial text if in tutorial)
			string description = ReputationRewardReflection.GetPopupDescription(_popup);

			if (!string.IsNullOrEmpty(description)) {
				// Suppress the EventAnnouncer's "New blueprint available" message
				SuppressBlueprintAnnouncement = true;
				return description;
			}

			if (_items.Count > 0)
				return $"{OverlayName}. {_items[0].Label}";

			return EmptyMessage;
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
			SuppressBlueprintAnnouncement = false;
		}

		// ========================================
		// ACTIVATION
		// ========================================

		private void ActivateBuilding(NavItem item) {
			if (!ReputationRewardReflection.PickBuilding(_popup, item.Model)) {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayButtonClick();

			var newOptions = ReputationRewardReflection.GetCurrentOptions();
			if (newOptions != null && newOptions.Count > 0) {
				Speech.Say("Unlocked");
				RefreshData();
				CurrentIndex = 0;
				AnnounceCurrentItem();
			} else {
				Speech.Say("Unlocked");
				// Popup hides -> OnPopupHidden -> Close()
			}
		}

		private void ActivateExtend() {
			if (!ReputationRewardReflection.CanAffordExtend()) {
				var (amount, goodName) = ReputationRewardReflection.GetExtendCost();
				Speech.Say($"Cannot afford, need {amount} {goodName}");
				SoundManager.PlayFailed();
				return;
			}

			int prevBuildingCount = CountBuildings();

			if (!ReputationRewardReflection.Extend()) {
				Speech.Say("Cannot extend");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayButtonClick();
			RefreshData();

			int newBuildingCount = CountBuildings();
			if (newBuildingCount > prevBuildingCount) {
				CurrentIndex = GetLastBuildingIndex();
				AnnounceCurrentItem();
			} else {
				Speech.Say("No new option available");
			}
		}

		private void ActivateReroll() {
			if (!ReputationRewardReflection.CanAffordReroll()) {
				var (amount, goodName) = ReputationRewardReflection.GetRerollCost();
				Speech.Say($"Cannot afford, need {amount} {goodName}");
				SoundManager.PlayFailed();
				return;
			}

			if (!ReputationRewardReflection.Reroll(_popup)) {
				Speech.Say("Cannot reroll");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayReroll();
			RefreshData();
			CurrentIndex = 0;
			AnnounceCurrentItem();
		}

		// ========================================
		// HELPERS
		// ========================================

		private string BuildBuildingLabel(ReputationRewardReflection.RewardOption option) {
			// Try to get per-recipe comparison info
			var comparisons = RecipesReflection.GetRecipeComparisons(option.InternalName);

			if (comparisons != null && comparisons.Count > 0) {
				// Build label with per-recipe status
				var parts = new System.Text.StringBuilder(option.DisplayName);
				foreach (var comp in comparisons) {
					parts.Append(". ");
					parts.Append(comp.GoodDisplayName);
					parts.Append($", {comp.GradeLevel} star, ");
					switch (comp.Status) {
						case RecipesReflection.RecipeCompareStatus.New:
							parts.Append("new recipe");
							break;
						case RecipesReflection.RecipeCompareStatus.Better:
							parts.Append($"plus {comp.LevelDifference} better");
							break;
						case RecipesReflection.RecipeCompareStatus.Worse:
							parts.Append($"{comp.LevelDifference} worse");
							break;
						case RecipesReflection.RecipeCompareStatus.Same:
							parts.Append("already unlocked");
							break;
					}
					if (!comp.CanProduce)
						parts.Append(", cannot produce");
				}
				return parts.ToString();
			}

			// Non-workshop building: use existing description
			return !string.IsNullOrEmpty(option.Description)
				? $"{option.DisplayName}. {option.Description}"
				: option.DisplayName;
		}

		private int CountBuildings() {
			int count = 0;
			for (int i = 0; i < _items.Count; i++) {
				if (_items[i].Type == ItemType.Building)
					count++;
			}
			return count;
		}

		private int GetLastBuildingIndex() {
			for (int i = _items.Count - 1; i >= 0; i--) {
				if (_items[i].Type == ItemType.Building)
					return i;
			}
			return 0;
		}
	}
}
