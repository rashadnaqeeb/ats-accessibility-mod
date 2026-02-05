using ATSAccessibility.Core;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Settings panel for toggling event announcements.
	/// Accessed from the F1 Information Panels menu.
	/// Not an IKeyHandler - called by InfoPanelMenu via ProcessKeyEvent(KeyCode).
	/// </summary>
	public class AnnouncementsSettingsPanel: MenuBase {
		private class SettingItem {
			public string Label;
			public ConfigEntry<bool> ConfigEntry;
		}

		private List<SettingItem> _items = new List<SettingItem>();

		// ========================================
		// BRIDGE
		// ========================================

		/// <summary>
		/// Bridge for InfoPanelMenu which calls ProcessKeyEvent(KeyCode).
		/// </summary>
		public bool ProcessKeyEvent(KeyCode keyCode) =>
			ProcessKey(keyCode, default(KeyboardManager.KeyModifiers));

		// ========================================
		// MENUBASE ABSTRACTS
		// ========================================

		protected override string OverlayName => "Announcement settings";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			var item = _items[index];
			return $"{item.Label}, {(item.ConfigEntry.Value ? "On" : "Off")}";
		}

		protected override void RefreshData() => BuildItemList();

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override void OnAction(int index) => ToggleCurrentSetting();

		protected override void OnSpace(int index) => ToggleCurrentSetting();

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.LeftArrow)
				return false;  // Signal parent to close this panel and return to menu
			return null;
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;  // Let parent handle closing

		protected override void OnClosed() {
			_items.Clear();
		}

		protected override string GetSearchName(int index) {
			return index >= 0 && index < _items.Count ? _items[index].Label : null;
		}

		// ========================================
		// PRIVATE
		// ========================================

		private void ToggleCurrentSetting() {
			if (_items.Count == 0 || CurrentIndex >= _items.Count) return;

			var item = _items[CurrentIndex];
			item.ConfigEntry.Value = !item.ConfigEntry.Value;
			AnnounceCurrentItem();
		}

		private void BuildItemList() {
			_items.Clear();

			// Game Alerts (uses game's built-in alert system)
			// This covers: newcomers waiting, villager loss, trader arrived, building destroyed,
			// hearth fire died, blight, order completed, low food, starvation, and many more
			_items.Add(new SettingItem { Label = "Game alerts", ConfigEntry = Plugin.AnnounceGameAlerts });

			// Buildings (not covered by game alerts)
			_items.Add(new SettingItem { Label = "Construction complete", ConfigEntry = Plugin.AnnounceConstructionComplete });
			_items.Add(new SettingItem { Label = "Hearth level change", ConfigEntry = Plugin.AnnounceHearthLevelChange });
			_items.Add(new SettingItem { Label = "Hearth ignited", ConfigEntry = Plugin.AnnounceHearthIgnited });
			_items.Add(new SettingItem { Label = "Hearth corrupted", ConfigEntry = Plugin.AnnounceHearthCorrupted });
			_items.Add(new SettingItem { Label = "Sacrifice stopped", ConfigEntry = Plugin.AnnounceSacrificeStopped });

			// Exploration
			_items.Add(new SettingItem { Label = "Glade revealed", ConfigEntry = Plugin.AnnounceGladeRevealed });
			_items.Add(new SettingItem { Label = "Relic resolved", ConfigEntry = Plugin.AnnounceRelicResolved });
			_items.Add(new SettingItem { Label = "Reward chase", ConfigEntry = Plugin.AnnounceRewardChase });
			_items.Add(new SettingItem { Label = "Locate markers", ConfigEntry = Plugin.AnnounceLocateMarkers });

			// Villagers
			_items.Add(new SettingItem { Label = "Newcomers waiting", ConfigEntry = Plugin.AnnounceNewcomersWaiting });
			_items.Add(new SettingItem { Label = "Villager lost", ConfigEntry = Plugin.AnnounceVillagerLost });

			// Time
			_items.Add(new SettingItem { Label = "Season changed", ConfigEntry = Plugin.AnnounceSeasonChanged });
			_items.Add(new SettingItem { Label = "Year changed", ConfigEntry = Plugin.AnnounceYearChanged });

			// Trade (trader departed not covered by game alerts)
			_items.Add(new SettingItem { Label = "Trader departed", ConfigEntry = Plugin.AnnounceTraderDeparted });

			// Orders (order available and failed not covered by game alerts)
			_items.Add(new SettingItem { Label = "Order available", ConfigEntry = Plugin.AnnounceOrderAvailable });
			_items.Add(new SettingItem { Label = "Order completed", ConfigEntry = Plugin.AnnounceOrderCompleted });
			_items.Add(new SettingItem { Label = "Order failed", ConfigEntry = Plugin.AnnounceOrderFailed });

			// Threats (hostility level change gives more detail than game's deadly-only alert)
			_items.Add(new SettingItem { Label = "Hostility level change", ConfigEntry = Plugin.AnnounceHostilityLevelChange });

			// Progression
			_items.Add(new SettingItem { Label = "Reputation changed", ConfigEntry = Plugin.AnnounceReputationChanged });
			_items.Add(new SettingItem { Label = "Good discovered", ConfigEntry = Plugin.AnnounceGoodDiscovered });
			_items.Add(new SettingItem { Label = "Game result", ConfigEntry = Plugin.AnnounceGameResult });
			_items.Add(new SettingItem { Label = "Blueprint available", ConfigEntry = Plugin.AnnounceBlueprintAvailable });
			_items.Add(new SettingItem { Label = "Cornerstone available", ConfigEntry = Plugin.AnnounceCornerstoneAvailable });

			// Resources
			_items.Add(new SettingItem { Label = "Expedition departed", ConfigEntry = Plugin.AnnouncePortExpeditionStarted });

			// News/Warnings
			_items.Add(new SettingItem { Label = "Game warnings", ConfigEntry = Plugin.AnnounceGameWarnings });

			// Sealed Forest
			_items.Add(new SettingItem { Label = "Plague events", ConfigEntry = Plugin.AnnouncePlagueEvents });
		}
	}
}
