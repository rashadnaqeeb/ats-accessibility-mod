using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Games History popup.
    /// Three-level navigation: Main Menu -> Submenu -> Settlement Details (flat list).
    /// </summary>
    public class GamesHistoryOverlay : MenuBase, IKeyHandler
    {
        private enum MainMenuItem { CycleStats, Upgrades, History }

        // Main menu items
        private static readonly string[] MainMenuItems = { "Cycle Stats", "Upgrades", "History" };

        // Cached data
        private List<(string label, string value)> _cycleStats;
        private List<(string label, string value)> _upgrades;
        private List<object> _settlements;

        // Current settlement detail items (flat list)
        private List<string> _settlementDetailItems;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Games History";

        protected override string EmptyMessage => "";

        protected override int GetItemCount()
        {
            switch (Level)
            {
                case 0: return MainMenuItems.Length;
                case 1: return GetCurrentSubmenuCount();
                case 2: return _settlementDetailItems?.Count ?? 0;
                default: return 0;
            }
        }

        protected override string GetLabel(int index)
        {
            switch (Level)
            {
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

        protected override void RefreshData()
        {
            _cycleStats = GamesHistoryReflection.GetCycleStats();
            _upgrades = GamesHistoryReflection.GetUpgrades();
            _settlements = GamesHistoryReflection.GetHistoryRecords();
        }

        protected override EnterAction OnEnter(int index)
        {
            switch (Level)
            {
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

        protected override void OnDrillDown(int index)
        {
            // When drilling from Level 1 into Level 2, build settlement detail items
            if (Level == 1)
            {
                if (_settlements != null && index >= 0 && index < _settlements.Count)
                    BuildSettlementDetailItems(_settlements[index]);
            }
        }

        protected override void OnGoBack()
        {
            // Clean up detail items when going back from Level 2
            if (Level == 2)
                _settlementDetailItems?.Clear();
        }

        protected override string GetOpenAnnouncement()
        {
            return $"Games History. {MainMenuItems[0]}";
        }

        protected override void OnClosed()
        {
            _cycleStats?.Clear();
            _upgrades?.Clear();
            _settlements?.Clear();
            _settlementDetailItems?.Clear();
        }

        // ========================================
        // SEARCH OVERRIDES
        // ========================================

        protected override int SearchItemCount
        {
            get
            {
                switch (Level)
                {
                    case 0: return MainMenuItems.Length;
                    case 1: return GetCurrentSubmenuCount();
                    case 2: return _settlementDetailItems?.Count ?? 0;
                    default: return 0;
                }
            }
        }

        protected override string GetSearchName(int index)
        {
            switch (Level)
            {
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

        private int GetCurrentSubmenuCount()
        {
            switch ((MainMenuItem)_indices[0])
            {
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

        private string GetSubmenuLabel(int index)
        {
            switch ((MainMenuItem)_indices[0])
            {
                case MainMenuItem.CycleStats:
                    if (_cycleStats != null && index >= 0 && index < _cycleStats.Count)
                    {
                        var stat = _cycleStats[index];
                        return $"{stat.label}, {stat.value}";
                    }
                    return null;

                case MainMenuItem.Upgrades:
                    if (_upgrades != null && index >= 0 && index < _upgrades.Count)
                    {
                        var upgrade = _upgrades[index];
                        return $"{upgrade.label}, {upgrade.value}";
                    }
                    return null;

                case MainMenuItem.History:
                    if (_settlements != null && index >= 0 && index < _settlements.Count)
                    {
                        string name = GamesHistoryReflection.GetSettlementName(_settlements[index]);
                        bool won = GamesHistoryReflection.GetSettlementWon(_settlements[index]);
                        return $"{name}, {(won ? "Won" : "Lost")}";
                    }
                    return null;

                default:
                    return null;
            }
        }

        private string GetSubmenuItemName(int index)
        {
            switch ((MainMenuItem)_indices[0])
            {
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

        private void BuildSettlementDetailItems(object settlement)
        {
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

            _settlementDetailItems.Add($"Summary: {name}, {(won ? "Won" : "Lost")}, {biome}, {difficulty}, {timeStr}, Year {years}, Level {level}, {upgrades} upgrades");

            // Races
            var races = GamesHistoryReflection.GetSettlementRaces(settlement);
            if (races.Count > 0)
            {
                var raceParts = new List<string>();
                foreach (var (raceName, count) in races)
                {
                    raceParts.Add($"{raceName} {count}");
                }
                _settlementDetailItems.Add($"Races: {string.Join(", ", raceParts)}");
            }
            else
            {
                _settlementDetailItems.Add("Races: none");
            }

            // Cornerstones
            var cornerstones = GamesHistoryReflection.GetSettlementCornerstones(settlement);
            if (cornerstones.Count > 0)
            {
                _settlementDetailItems.Add($"Cornerstones: {string.Join(", ", cornerstones)}");
            }
            else
            {
                _settlementDetailItems.Add("Cornerstones: none");
            }

            // Modifiers
            var modifiers = GamesHistoryReflection.GetSettlementModifiers(settlement);
            if (modifiers.Count > 0)
            {
                _settlementDetailItems.Add($"Modifiers: {string.Join(", ", modifiers)}");
            }
            else
            {
                _settlementDetailItems.Add("Modifiers: none");
            }

            // Buildings
            var buildings = GamesHistoryReflection.GetSettlementBuildings(settlement);
            if (buildings.Count > 0)
            {
                _settlementDetailItems.Add($"Buildings: {string.Join(", ", buildings)}");
            }
            else
            {
                _settlementDetailItems.Add("Buildings: none");
            }

            // Seasonal Effects
            var seasonalEffects = GamesHistoryReflection.GetSettlementSeasonalEffects(settlement);
            if (seasonalEffects.Count > 0)
            {
                _settlementDetailItems.Add($"Seasonal Effects: {string.Join(", ", seasonalEffects)}");
            }
            else
            {
                _settlementDetailItems.Add("Seasonal Effects: none");
            }
        }
    }
}
