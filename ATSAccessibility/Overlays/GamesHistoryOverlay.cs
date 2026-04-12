using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using ATSAccessibility.Utils;
using System.Collections.Generic;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the Games History popup.
	/// Three-level navigation: Main Menu -> Submenu -> Settlement Details (flat list).
	/// </summary>
	public class GamesHistoryOverlay: MenuBase {
		private enum MainMenuItem { CycleStats, Upgrades, History }

		// Main menu items
		private static string[] MainMenuItems => new[] {
			Strings.Get("overlay.games_history.item.cycle_stats"),
			Strings.Get("common.upgrades"),
			Strings.Get("overlay.games_history.item.history")
		};

		// Cached data
		private List<(string label, string value)> _cycleStats;
		private List<(string label, string value)> _upgrades;
		private List<object> _settlements;

		// Current settlement detail items (flat list)
		private List<string> _settlementDetailItems;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.games_history.title");

		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			switch (Level) {
				case 0: return MainMenuItems.Length;
				case 1: return GetCurrentSubmenuCount();
				case 2: return _settlementDetailItems?.Count ?? 0;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0:
					return (index >= 0 && index < MainMenuItems.Length) ? MainMenuItems[index] : null;

				case 1:
					return GetSubmenuLabel(index);

				case 2:
					return (_settlementDetailItems != null && index >= 0 && index < _settlementDetailItems.Count)
						? _settlementDetailItems[index] : null;

				default:
					return null;
			}
		}

		protected override void RefreshData() {
			_cycleStats = GamesHistoryReflection.GetCycleStats();
			_upgrades = GamesHistoryReflection.GetUpgrades();
			_settlements = GamesHistoryReflection.GetHistoryRecords();
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					return EnterAction.DrillDown;

				case 1:
					if (_indices[0] == (int)MainMenuItem.History && _settlements != null && _settlements.Count > 0)
						return EnterAction.DrillDown;
					return EnterAction.None;

				case 2:
					return EnterAction.None;

				default:
					return EnterAction.None;
			}
		}

		protected override void OnDrillDown(int index) {
			// When drilling from Level 1 into Level 2, build settlement detail items
			if (Level == 1) {
				if (_settlements != null && index >= 0 && index < _settlements.Count)
					BuildSettlementDetailItems(_settlements[index]);
			}
		}

		protected override void OnGoBack() {
			// Clean up detail items when going back from Level 2
			if (Level == 2)
				_settlementDetailItems?.Clear();
		}

		protected override string GetOpenAnnouncement() {
			return Strings.Get("overlay.games_history.open", MainMenuItems[0]);
		}

		protected override void OnClosed() {
			_cycleStats?.Clear();
			_upgrades?.Clear();
			_settlements?.Clear();
			_settlementDetailItems?.Clear();
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override int SearchItemCount {
			get {
				switch (Level) {
					case 0: return MainMenuItems.Length;
					case 1: return GetCurrentSubmenuCount();
					case 2: return _settlementDetailItems?.Count ?? 0;
					default: return 0;
				}
			}
		}

		protected override string GetSearchName(int index) {
			switch (Level) {
				case 0:
					return (index >= 0 && index < MainMenuItems.Length) ? MainMenuItems[index] : null;

				case 1:
					return GetSubmenuItemName(index);

				case 2:
					return (_settlementDetailItems != null && index >= 0 && index < _settlementDetailItems.Count)
						? _settlementDetailItems[index] : null;

				default:
					return null;
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private int GetCurrentSubmenuCount() {
			switch ((MainMenuItem)_indices[0]) {
				case MainMenuItem.CycleStats:
					return _cycleStats?.Count ?? 0;
				case MainMenuItem.Upgrades:
					return _upgrades?.Count ?? 0;
				case MainMenuItem.History:
					return _settlements?.Count ?? 0;
				default:
					return 0;
			}
		}

		private string GetSubmenuLabel(int index) {
			switch ((MainMenuItem)_indices[0]) {
				case MainMenuItem.CycleStats:
					if (_cycleStats != null && index >= 0 && index < _cycleStats.Count) {
						var stat = _cycleStats[index];
						return Strings.Get("overlay.games_history.submenu", stat.label, stat.value);
					}
					return null;

				case MainMenuItem.Upgrades:
					if (_upgrades != null && index >= 0 && index < _upgrades.Count) {
						var upgrade = _upgrades[index];
						return Strings.Get("overlay.games_history.submenu", upgrade.label, upgrade.value);
					}
					return null;

				case MainMenuItem.History:
					if (_settlements != null && index >= 0 && index < _settlements.Count) {
						string name = GamesHistoryReflection.GetSettlementName(_settlements[index]);
						bool won = GamesHistoryReflection.GetSettlementWon(_settlements[index]);
						return Strings.Get("overlay.games_history.history_row", name, Strings.Get(won ? "common.won" : "common.lost"));
					}
					return null;

				default:
					return null;
			}
		}

		private string GetSubmenuItemName(int index) {
			switch ((MainMenuItem)_indices[0]) {
				case MainMenuItem.CycleStats:
					return index < (_cycleStats?.Count ?? 0) ? _cycleStats[index].label : null;
				case MainMenuItem.Upgrades:
					return index < (_upgrades?.Count ?? 0) ? _upgrades[index].label : null;
				case MainMenuItem.History:
					return index < (_settlements?.Count ?? 0) ? GamesHistoryReflection.GetSettlementName(_settlements[index]) : null;
				default:
					return null;
			}
		}

		private void BuildSettlementDetailItems(object settlement) {
			if (_settlementDetailItems == null)
				_settlementDetailItems = new List<string>();
			else
				_settlementDetailItems.Clear();

			// Summary
			string name = GamesHistoryReflection.GetSettlementName(settlement);
			bool won = GamesHistoryReflection.GetSettlementWon(settlement);
			string biome = GamesHistoryReflection.GetSettlementBiome(settlement);
			string difficulty = GamesHistoryReflection.GetSettlementDifficulty(settlement);
			float gameTime = GamesHistoryReflection.GetSettlementGameTime(settlement);
			int years = GamesHistoryReflection.GetSettlementYears(settlement);
			int level = GamesHistoryReflection.GetSettlementLevel(settlement);
			int upgrades = GamesHistoryReflection.GetSettlementUpgrades(settlement);
			string timeStr = GamesHistoryReflection.FormatGameTime(gameTime);

			_settlementDetailItems.Add(Strings.Get("overlay.games_history.summary", name, Strings.Get(won ? "common.won" : "common.lost"), biome, difficulty, timeStr, years, level, upgrades));

			// Races
			var races = GamesHistoryReflection.GetSettlementRaces(settlement);
			if (races.Count > 0) {
				var raceParts = new List<string>();
				foreach (var (raceName, count) in races) {
					raceParts.Add(Strings.Get("overlay.games_history.race_count", raceName, count));
				}
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.races", string.Join(", ", raceParts)));
			} else {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.races.none"));
			}

			// Cornerstones
			var cornerstones = GamesHistoryReflection.GetSettlementCornerstones(settlement);
			if (cornerstones.Count > 0) {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.cornerstones", string.Join(", ", cornerstones)));
			} else {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.cornerstones.none"));
			}

			// Modifiers
			var modifiers = GamesHistoryReflection.GetSettlementModifiers(settlement);
			if (modifiers.Count > 0) {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.modifiers", string.Join(", ", modifiers)));
			} else {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.modifiers.none"));
			}

			// Buildings
			var buildings = GamesHistoryReflection.GetSettlementBuildings(settlement);
			if (buildings.Count > 0) {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.buildings", string.Join(", ", buildings)));
			} else {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.buildings.none"));
			}

			// Seasonal Effects
			var seasonalEffects = GamesHistoryReflection.GetSettlementSeasonalEffects(settlement);
			if (seasonalEffects.Count > 0) {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.seasonal", string.Join(", ", seasonalEffects)));
			} else {
				_settlementDetailItems.Add(Strings.Get("overlay.games_history.seasonal.none"));
			}
		}
	}
}
