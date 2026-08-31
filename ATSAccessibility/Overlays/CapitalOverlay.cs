using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the capital (Smoldering City) screen.
	/// Flat list navigation: Buy Upgrades, Deeds, Game History, Home (if unlocked).
	/// </summary>
	public class CapitalOverlay: MenuBase {
		private List<(string name, Func<bool> action)> _items = new List<(string, Func<bool>)>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.capital");
		protected override string EmptyMessage => Strings.Get("overlay.capital.empty");

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;

			string name = _items[index].name;
			string lockSuffix = GetLockSuffix(name);
			return Strings.Get("overlay.capital.item.label", name, lockSuffix);
		}

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].name;
		}

		protected override void RefreshData() {
			_items.Clear();

			_items.Add((Strings.Get("overlay.capital.item.buy_upgrades"), () => CapitalReflection.OpenUpgrades()));
			_items.Add((Strings.Get("common.deeds"), () => CapitalReflection.OpenDeeds()));
			_items.Add((Strings.Get("overlay.capital.item.game_history"), () => CapitalReflection.OpenHistory()));
			_items.Add((Strings.Get("common.daily_expedition"), () => CapitalReflection.OpenDailyExpedition()));
			_items.Add((Strings.Get("overlay.capital.item.training_expedition"), () => CapitalReflection.OpenTrainingExpedition()));

			if (CapitalReflection.IsHomeUnlocked()) {
				_items.Add((Strings.Get("overlay.capital.item.home"), () => CapitalReflection.OpenHome()));
			}
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];

			// Check if item is locked before suspending
			if (IsItemLocked(item.name)) {
				Speech.Say(Strings.Get("overlay.capital.locked_activation", item.name));
				SoundManager.PlayFailed();
				return;
			}

			Debug.Log($"[ATSAccessibility] Capital overlay: activating {item.name}");
			SoundManager.PlayButtonClick();
			Suspend();
			bool opened = false;
			try {
				opened = item.action != null && item.action();
			} finally {
				if (!opened) {
					// The reflected open failed, so no popup will ever appear and no
					// popup-hidden event will resume this overlay — resume now
					// instead of leaving the arrows dead until some other popup
					// happens to open and close.
					Debug.LogWarning($"[ATSAccessibility] Capital overlay: {item.name} failed to open");
					SoundManager.PlayFailed();
					Resume();
				}
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.Escape) {
				Close();
				return false; // Pass to game to close capital screen
			}
			return null;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count == 0) return EmptyMessage;
			return GetLabel(0); // No prefix, just the item
		}

		protected override void OnClosed() {
			_items.Clear();
		}

		// ========================================
		// HELPERS
		// ========================================

		private bool IsItemLocked(string name) {
			if (name == Strings.Get("common.deeds")) return !CapitalReflection.IsDeedsUnlocked();
			if (name == Strings.Get("common.daily_expedition")) return !CapitalReflection.IsDailyExpeditionUnlocked();
			if (name == Strings.Get("overlay.capital.item.training_expedition")) return !CapitalReflection.IsTrainingExpeditionUnlocked();
			return false;
		}

		private string GetLockSuffix(string itemName) {
			if (itemName == Strings.Get("common.deeds"))
				return CapitalReflection.IsDeedsUnlocked() ? "" : Strings.Get("common.suffix_locked");
			if (itemName == Strings.Get("common.daily_expedition"))
				return CapitalReflection.IsDailyExpeditionUnlocked() ? "" : Strings.Get("common.suffix_locked");
			if (itemName == Strings.Get("overlay.capital.item.training_expedition"))
				return CapitalReflection.IsTrainingExpeditionUnlocked() ? "" : Strings.Get("common.suffix_locked");
			if (itemName == Strings.Get("overlay.capital.item.home"))
				return NarrationReflection.HasAnyImportantTopics() ? Strings.Get("overlay.capital.suffix.new_dialogue") : "";
			return "";
		}
	}
}
