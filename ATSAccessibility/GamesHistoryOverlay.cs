using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Games History popup.
    /// Three-level navigation: Main Menu -> Submenu -> Settlement Details (flat list).
    /// </summary>
    public class GamesHistoryOverlay : IKeyHandler
    {
        // Navigation levels
        private enum Level { MainMenu, Submenu, SettlementDetails }
        private enum MainMenuItem { CycleStats, Upgrades, History }

        // State
        private bool _isOpen;
        private Level _level;
        private int _mainMenuIndex;
        private int _submenuIndex;
        private int _detailIndex;

        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Cached data
        private List<(string label, string value)> _cycleStats;
        private List<(string label, string value)> _upgrades;
        private List<object> _settlements;

        // Current settlement detail items (flat list)
        private List<string> _settlementDetailItems;

        // Main menu items
        private static readonly string[] MainMenuItems = { "Cycle Stats", "Upgrades", "History" };

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (_level)
            {
                case Level.MainMenu:
                    return ProcessMainMenuKey(keyCode);
                case Level.Submenu:
                    return ProcessSubmenuKey(keyCode);
                case Level.SettlementDetails:
                    return ProcessSettlementDetailsKey(keyCode);
                default:
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _level = Level.MainMenu;
            _mainMenuIndex = 0;
            _submenuIndex = 0;
            _detailIndex = 0;
            _search.Clear();

            RefreshData();

            Speech.Say($"Games History. {MainMenuItems[0]}");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _level = Level.MainMenu;
            _search.Clear();
            ClearData();
        }

        private void RefreshData()
        {
            _cycleStats = GamesHistoryReflection.GetCycleStats();
            _upgrades = GamesHistoryReflection.GetUpgrades();
            _settlements = GamesHistoryReflection.GetHistoryRecords();
        }

        private void ClearData()
        {
            _cycleStats?.Clear();
            _upgrades?.Clear();
            _settlements?.Clear();
            _settlementDetailItems?.Clear();
        }

        // ========================================
        // MAIN MENU LEVEL
        // ========================================

        private bool ProcessMainMenuKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateMainMenu(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateMainMenu(1);
                    return true;

                case KeyCode.Home:
                    _mainMenuIndex = 0;
                    AnnounceMainMenuItem();
                    return true;

                case KeyCode.End:
                    _mainMenuIndex = MainMenuItems.Length - 1;
                    AnnounceMainMenuItem();
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    EnterSubmenu();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleMainMenuBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleMainMenuSearch(c);
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateMainMenu(int direction)
        {
            _mainMenuIndex = NavigationUtils.WrapIndex(_mainMenuIndex, direction, MainMenuItems.Length);
            AnnounceMainMenuItem();
        }

        private void AnnounceMainMenuItem()
        {
            Speech.Say(MainMenuItems[_mainMenuIndex]);
        }

        private void EnterSubmenu()
        {
            _level = Level.Submenu;
            _submenuIndex = 0;
            _search.Clear();
            AnnounceSubmenuItem();
        }

        private void HandleMainMenuSearch(char c)
        {
            _search.AddChar(c);
            int match = FindMainMenuMatch();
            if (match >= 0)
            {
                _mainMenuIndex = match;
                AnnounceMainMenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleMainMenuBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindMainMenuMatch();
            if (match >= 0)
            {
                _mainMenuIndex = match;
                AnnounceMainMenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMainMenuMatch()
        {
            if (!_search.HasBuffer) return -1;
            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < MainMenuItems.Length; i++)
            {
                if (MainMenuItems[i].ToLowerInvariant().StartsWith(lowerBuffer))
                    return i;
            }
            return -1;
        }

        // ========================================
        // SUBMENU LEVEL
        // ========================================

        private bool ProcessSubmenuKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateSubmenu(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateSubmenu(1);
                    return true;

                case KeyCode.Home:
                    {
                        int count = GetCurrentSubmenuCount();
                        if (count > 0)
                        {
                            _submenuIndex = 0;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.End:
                    {
                        int count = GetCurrentSubmenuCount();
                        if (count > 0)
                        {
                            _submenuIndex = count - 1;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToMainMenu();
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // For History, enter settlement details
                    if ((MainMenuItem)_mainMenuIndex == MainMenuItem.History)
                    {
                        EnterSettlementDetails();
                    }
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    ReturnToMainMenu();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleSubmenuBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSubmenuSearch(c);
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateSubmenu(int direction)
        {
            int count = GetCurrentSubmenuCount();
            if (count == 0)
            {
                Speech.Say("Empty");
                return;
            }

            _submenuIndex = NavigationUtils.WrapIndex(_submenuIndex, direction, count);
            AnnounceSubmenuItem();
        }

        private int GetCurrentSubmenuCount()
        {
            switch ((MainMenuItem)_mainMenuIndex)
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

        private void AnnounceSubmenuItem()
        {
            int count = GetCurrentSubmenuCount();
            if (count == 0 || _submenuIndex < 0 || _submenuIndex >= count)
            {
                Speech.Say("Empty");
                return;
            }

            switch ((MainMenuItem)_mainMenuIndex)
            {
                case MainMenuItem.CycleStats:
                    var stat = _cycleStats[_submenuIndex];
                    Speech.Say($"{stat.label}, {stat.value}");
                    break;

                case MainMenuItem.Upgrades:
                    var upgrade = _upgrades[_submenuIndex];
                    Speech.Say($"{upgrade.label}, {upgrade.value}");
                    break;

                case MainMenuItem.History:
                    var settlement = _settlements[_submenuIndex];
                    string name = GamesHistoryReflection.GetSettlementName(settlement);
                    bool won = GamesHistoryReflection.GetSettlementWon(settlement);
                    Speech.Say($"{name}, {(won ? "Won" : "Lost")}");
                    break;
            }
        }

        private void ReturnToMainMenu()
        {
            _level = Level.MainMenu;
            _search.Clear();
            AnnounceMainMenuItem();
        }

        private void HandleSubmenuSearch(char c)
        {
            _search.AddChar(c);
            int match = FindSubmenuMatch();
            if (match >= 0)
            {
                _submenuIndex = match;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleSubmenuBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindSubmenuMatch();
            if (match >= 0)
            {
                _submenuIndex = match;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindSubmenuMatch()
        {
            if (!_search.HasBuffer) return -1;
            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            int count = GetCurrentSubmenuCount();
            for (int i = 0; i < count; i++)
            {
                string name = "";
                switch ((MainMenuItem)_mainMenuIndex)
                {
                    case MainMenuItem.CycleStats:
                        name = _cycleStats[i].label;
                        break;
                    case MainMenuItem.Upgrades:
                        name = _upgrades[i].label;
                        break;
                    case MainMenuItem.History:
                        name = GamesHistoryReflection.GetSettlementName(_settlements[i]);
                        break;
                }

                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerBuffer))
                    return i;
            }

            return -1;
        }

        // ========================================
        // SETTLEMENT DETAILS LEVEL (FLAT LIST)
        // ========================================

        private void EnterSettlementDetails()
        {
            if (_settlements == null || _submenuIndex < 0 || _submenuIndex >= _settlements.Count)
                return;

            var settlement = _settlements[_submenuIndex];
            BuildSettlementDetailItems(settlement);

            if (_settlementDetailItems == null || _settlementDetailItems.Count == 0)
            {
                Speech.Say("No details available");
                return;
            }

            _level = Level.SettlementDetails;
            _detailIndex = 0;
            _search.Clear();

            AnnounceDetailItem();
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

        private bool ProcessSettlementDetailsKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateDetails(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateDetails(1);
                    return true;

                case KeyCode.Home:
                    if (_settlementDetailItems != null && _settlementDetailItems.Count > 0)
                    {
                        _detailIndex = 0;
                        AnnounceDetailItem();
                    }
                    return true;

                case KeyCode.End:
                    if (_settlementDetailItems != null && _settlementDetailItems.Count > 0)
                    {
                        _detailIndex = _settlementDetailItems.Count - 1;
                        AnnounceDetailItem();
                    }
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToSubmenu();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    ReturnToSubmenu();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleDetailsBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleDetailsSearch(c);
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateDetails(int direction)
        {
            if (_settlementDetailItems == null || _settlementDetailItems.Count == 0)
            {
                Speech.Say("Empty");
                return;
            }

            _detailIndex = NavigationUtils.WrapIndex(_detailIndex, direction, _settlementDetailItems.Count);
            AnnounceDetailItem();
        }

        private void AnnounceDetailItem()
        {
            if (_settlementDetailItems == null || _detailIndex < 0 || _detailIndex >= _settlementDetailItems.Count)
            {
                Speech.Say("Empty");
                return;
            }

            Speech.Say(_settlementDetailItems[_detailIndex]);
        }

        private void ReturnToSubmenu()
        {
            _level = Level.Submenu;
            _search.Clear();
            _settlementDetailItems?.Clear();
            AnnounceSubmenuItem();
        }

        private void HandleDetailsSearch(char c)
        {
            _search.AddChar(c);
            int match = FindDetailsMatch();
            if (match >= 0)
            {
                _detailIndex = match;
                AnnounceDetailItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleDetailsBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindDetailsMatch();
            if (match >= 0)
            {
                _detailIndex = match;
                AnnounceDetailItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindDetailsMatch()
        {
            if (!_search.HasBuffer || _settlementDetailItems == null) return -1;
            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _settlementDetailItems.Count; i++)
            {
                if (_settlementDetailItems[i].ToLowerInvariant().StartsWith(lowerBuffer))
                    return i;
            }

            return -1;
        }
    }
}
