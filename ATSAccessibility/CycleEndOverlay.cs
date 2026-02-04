using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Overlay for WorldCycleEndPopup (shown when ending a Blightstorm cycle on world map).
	/// Provides navigation through XP summary and unlocked capital upgrades.
	/// Single-level flat list of strings.
	/// </summary>
	public class CycleEndOverlay: MenuBase {
		// Data
		private object _popup;
		private List<string> _items = new List<string>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "End Cycle";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index];
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			// Get XP summary
			string xpSummary = GetXpSummary();
			if (!string.IsNullOrEmpty(xpSummary)) {
				_items.Add(xpSummary);
			}

			// Get unlocked upgrades
			var upgradeNames = GetUnlockedUpgradeNames();
			foreach (var name in upgradeNames) {
				_items.Add(name);
			}

			Debug.Log($"[ATSAccessibility] CycleEndOverlay: {_items.Count} items refreshed");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			ConfirmCycleEnd();
		}

		protected override void OnSpace(int index) {
			ConfirmCycleEnd();
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.Escape) {
				CancelAndClose();
				Close();
				InputBlocker.BlockCancelOnce = true;
				return true;
			}

			return null;
		}

		protected override int SearchItemCount => 0; // No search

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count > 0)
				return $"End Cycle. {_items[0]}";
			return "End Cycle";
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// DETECTION
		// ========================================

		public static bool IsWorldCycleEndPopup(object popup) {
			if (popup == null) return false;
			return popup.GetType().Name == "WorldCycleEndPopup";
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void ConfirmCycleEnd() {
			if (_popup == null) return;

			// Trigger cycle end animation via WorldMapReflection
			WorldMapReflection.TriggerCycleEndAnimation();

			// Hide the popup
			if (PopupReflection.HidePopup(_popup)) {
				SoundManager.PlayButtonClick();
				Speech.Say("Confirmed");
			}
		}

		private void CancelAndClose() {
			if (_popup == null) return;

			// Hide the popup without triggering cycle end
			if (PopupReflection.HidePopup(_popup)) {
				Speech.Say("Cancelled");
			}
		}

		// ========================================
		// DATA
		// ========================================

		private string GetXpSummary() {
			try {
				int currentCycleExp = WorldMapReflection.GetCurrentCycleExp();
				var (currentLevel, currentExp, targetExp) = WorldMapReflection.GetLevelInfo();

				// Handle max level case (targetExp == 0 or currentExp >= targetExp)
				if (targetExp <= 0 || currentExp >= targetExp) {
					return $"Gained {currentCycleExp} experience, Level {currentLevel}, max level";
				}

				return $"Gained {currentCycleExp} experience, Level {currentLevel}, {currentExp} of {targetExp} to next level";
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CycleEndOverlay: GetXpSummary failed: {ex.Message}");
				return "Experience summary unavailable";
			}
		}

		private List<string> GetUnlockedUpgradeNames() {
			var names = new List<string>();

			try {
				var upgrades = WorldMapReflection.GetCurrentCycleUnlockedUpgrades();
				if (upgrades == null) return names;

				bool isIronman = WorldMapReflection.IsIronmanMode();

				var settings = GameReflection.GetSettings();
				if (settings == null) return names;

				foreach (var upgradeId in upgrades) {
					if (upgradeId == null) continue;

					string id = upgradeId.ToString();
					string displayName = WorldMapReflection.GetCapitalUpgradeDisplayName(settings, id, isIronman);
					if (!string.IsNullOrEmpty(displayName)) {
						names.Add(displayName);
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CycleEndOverlay: GetUnlockedUpgradeNames failed: {ex.Message}");
			}

			return names;
		}
	}
}
