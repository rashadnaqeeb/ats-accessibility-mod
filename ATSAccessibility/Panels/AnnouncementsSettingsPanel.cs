using ATSAccessibility.Core;
using ATSAccessibility.Utils;
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

		// ========================================
		// MENUBASE ABSTRACTS
		// ========================================

		protected override string OverlayName => Strings.Get("panel.announcements_settings.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			var item = _items[index];
			return Strings.Get("panel.announcements_settings.item", item.Label, item.ConfigEntry.Value ? Strings.Get("panel.announcements_settings.on") : Strings.Get("common.off"));
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
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.game_alerts"), ConfigEntry = Plugin.AnnounceGameAlerts });

			// Buildings (not covered by game alerts)
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.construction_complete"), ConfigEntry = Plugin.AnnounceConstructionComplete });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.hearth_level_change"), ConfigEntry = Plugin.AnnounceHearthLevelChange });
			_items.Add(new SettingItem { Label = Strings.Get("common.hearth_ignited"), ConfigEntry = Plugin.AnnounceHearthIgnited });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.hearth_corrupted"), ConfigEntry = Plugin.AnnounceHearthCorrupted });
			_items.Add(new SettingItem { Label = Strings.Get("common.sacrifice_stopped"), ConfigEntry = Plugin.AnnounceSacrificeStopped });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.building_idle"), ConfigEntry = Plugin.AnnounceBuildingIdle });

			// Exploration
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.glade_revealed"), ConfigEntry = Plugin.AnnounceGladeRevealed });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.relic_resolved"), ConfigEntry = Plugin.AnnounceRelicResolved });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.reward_chase"), ConfigEntry = Plugin.AnnounceRewardChase });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.locate_markers"), ConfigEntry = Plugin.AnnounceLocateMarkers });

			// Villagers
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.newcomers_waiting"), ConfigEntry = Plugin.AnnounceNewcomersWaiting });
			_items.Add(new SettingItem { Label = Strings.Get("common.villager_lost"), ConfigEntry = Plugin.AnnounceVillagerLost });

			// Time
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.season_changed"), ConfigEntry = Plugin.AnnounceSeasonChanged });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.year_changed"), ConfigEntry = Plugin.AnnounceYearChanged });

			// Trade (trader departed not covered by game alerts)
			_items.Add(new SettingItem { Label = Strings.Get("common.trader_departed"), ConfigEntry = Plugin.AnnounceTraderDeparted });

			// Orders (order available and failed not covered by game alerts)
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.order_available"), ConfigEntry = Plugin.AnnounceOrderAvailable });
			_items.Add(new SettingItem { Label = Strings.Get("common.order_completed"), ConfigEntry = Plugin.AnnounceOrderCompleted });
			_items.Add(new SettingItem { Label = Strings.Get("common.order_failed"), ConfigEntry = Plugin.AnnounceOrderFailed });

			// Threats (hostility level change gives more detail than game's deadly-only alert)
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.hostility_level_change"), ConfigEntry = Plugin.AnnounceHostilityLevelChange });

			// Progression
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.reputation_changed"), ConfigEntry = Plugin.AnnounceReputationChanged });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.good_discovered"), ConfigEntry = Plugin.AnnounceGoodDiscovered });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.game_result"), ConfigEntry = Plugin.AnnounceGameResult });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.blueprint_available"), ConfigEntry = Plugin.AnnounceBlueprintAvailable });
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.cornerstone_available"), ConfigEntry = Plugin.AnnounceCornerstoneAvailable });

			// Resources
			_items.Add(new SettingItem { Label = Strings.Get("common.expedition_departed"), ConfigEntry = Plugin.AnnouncePortExpeditionStarted });

			// News/Warnings
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.game_warnings"), ConfigEntry = Plugin.AnnounceGameWarnings });

			// Sealed Forest
			_items.Add(new SettingItem { Label = Strings.Get("panel.announcements_settings.plague_events"), ConfigEntry = Plugin.AnnouncePlagueEvents });
		}
	}
}
