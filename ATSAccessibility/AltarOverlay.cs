using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Forsaken Altar panel.
    /// Provides multi-level navigation: Main Menu → Resources/Cornerstones → Currencies/Races.
    /// </summary>
    public class AltarOverlay : IKeyHandler
    {
        // ========================================
        // MENU LEVELS
        // ========================================

        private enum MenuLevel
        {
            Main,           // Resources, Cornerstones, Skip
            Resources,      // Currencies, Villagers
            Currencies,     // Individual currency toggles
            Races,          // Individual race toggles
            Cornerstones    // Cornerstone options
        }

        // Main menu item types
        private enum MainItem { Resources, Cornerstones, Skip }

        // Resources submenu item types
        private enum ResourceItem { Currencies, Villagers }

        // ========================================
        // STATE
        // ========================================

        private bool _isOpen;
        private bool _isActive;  // Whether altar is active (Storm + has pick)

        // Navigation indices
        private MenuLevel _level;
        private int _mainIndex;
        private int _resourceIndex;
        private int _currencyIndex;
        private int _raceIndex;
        private int _cornerstoneIndex;

        // Cached data
        private List<AltarReflection.CurrencyInfo> _currencies;
        private List<AltarReflection.RaceInfo> _races;
        private List<AltarReflection.EffectInfo> _cornerstones;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // If inactive, only allow Escape
            if (!_isActive)
            {
                if (keyCode == KeyCode.Escape)
                {
                    // Pass to game to close popup
                    return false;
                }
                // Consume all other keys while overlay is active
                return true;
            }

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(GetCurrentItemCount() - 1);
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    DrillDown();
                    return true;

                case KeyCode.LeftArrow:
                    GoBack();
                    return true;

                case KeyCode.Space:
                    ToggleCurrent();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    if (_level != MenuLevel.Main)
                    {
                        GoBack();
                        return true;
                    }
                    // At main level - pass to game to close popup
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when the altar panel is shown.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _level = MenuLevel.Main;
            _mainIndex = 0;
            _resourceIndex = 0;
            _currencyIndex = 0;
            _raceIndex = 0;
            _cornerstoneIndex = 0;
            _search.Clear();

            // Check if altar is active
            _isActive = AltarReflection.IsAltarActive();

            if (_isActive)
            {
                RefreshData();
                Speech.Say("Forsaken Altar. Resources");
                Debug.Log("[ATSAccessibility] AltarOverlay opened (active)");
            }
            else
            {
                AnnounceInactive();
                Debug.Log("[ATSAccessibility] AltarOverlay opened (inactive)");
            }
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _search.Clear();
            _currencies?.Clear();
            _races?.Clear();
            _cornerstones?.Clear();

            Debug.Log("[ATSAccessibility] AltarOverlay closed");
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshData()
        {
            _currencies = AltarReflection.GetCurrencies();
            _races = AltarReflection.GetRaces();
            _cornerstones = AltarReflection.GetCurrentPick();
        }

        /// <summary>
        /// Announce inactive state with next charge info.
        /// </summary>
        private void AnnounceInactive()
        {
            var nextCharge = AltarReflection.GetNextChargeThreshold();
            string message = "Altar inactive. Requires Storm season and activation charge.";

            if (nextCharge.HasValue)
            {
                message += $" Next activation at {nextCharge.Value} reputation.";
            }
            else
            {
                message += " No more activations available this run.";
            }

            Speech.Say(message);
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void NavigateTo(int index)
        {
            int count = GetCurrentItemCount();
            if (count == 0) return;
            SetCurrentIndex(Mathf.Clamp(index, 0, count - 1));
            AnnounceCurrent();
        }

        private void Navigate(int direction)
        {
            int count = GetCurrentItemCount();
            if (count == 0) return;

            switch (_level)
            {
                case MenuLevel.Main:
                    _mainIndex = NavigationUtils.WrapIndex(_mainIndex, direction, 3);
                    break;
                case MenuLevel.Resources:
                    _resourceIndex = NavigationUtils.WrapIndex(_resourceIndex, direction, 2);
                    break;
                case MenuLevel.Currencies:
                    _currencyIndex = NavigationUtils.WrapIndex(_currencyIndex, direction, _currencies?.Count ?? 0);
                    break;
                case MenuLevel.Races:
                    _raceIndex = NavigationUtils.WrapIndex(_raceIndex, direction, _races?.Count ?? 0);
                    break;
                case MenuLevel.Cornerstones:
                    _cornerstoneIndex = NavigationUtils.WrapIndex(_cornerstoneIndex, direction, _cornerstones?.Count ?? 0);
                    break;
            }

            AnnounceCurrent();
        }

        private int GetCurrentItemCount()
        {
            switch (_level)
            {
                case MenuLevel.Main: return 3;
                case MenuLevel.Resources: return 2;
                case MenuLevel.Currencies: return _currencies?.Count ?? 0;
                case MenuLevel.Races: return _races?.Count ?? 0;
                case MenuLevel.Cornerstones: return _cornerstones?.Count ?? 0;
                default: return 0;
            }
        }

        private void AnnounceCurrent()
        {
            switch (_level)
            {
                case MenuLevel.Main:
                    AnnounceMainItem();
                    break;
                case MenuLevel.Resources:
                    AnnounceResourceItem();
                    break;
                case MenuLevel.Currencies:
                    AnnounceCurrency();
                    break;
                case MenuLevel.Races:
                    AnnounceRace();
                    break;
                case MenuLevel.Cornerstones:
                    AnnounceCornerstone();
                    break;
            }
        }

        private void AnnounceMainItem()
        {
            switch ((MainItem)_mainIndex)
            {
                case MainItem.Resources:
                    Speech.Say("Resources");
                    break;
                case MainItem.Cornerstones:
                    Speech.Say("Cornerstones");
                    break;
                case MainItem.Skip:
                    Speech.Say("Skip this pick");
                    break;
            }
        }

        private void AnnounceResourceItem()
        {
            switch ((ResourceItem)_resourceIndex)
            {
                case ResourceItem.Currencies:
                    int totalValue = AltarReflection.GetTotalMetaValue();
                    Speech.Say($"Currencies, {totalValue} total value");
                    break;
                case ResourceItem.Villagers:
                    int totalVillagers = AltarReflection.GetTotalVillagers();
                    bool villagersEnabled = AltarReflection.AreVillagersAllowed();
                    Speech.Say($"Villagers, {totalVillagers} available, {(villagersEnabled ? "enabled" : "disabled")}");
                    break;
            }
        }

        private void AnnounceCurrency()
        {
            if (_currencies == null || _currencyIndex < 0 || _currencyIndex >= _currencies.Count)
            {
                Speech.Say("No currencies");
                return;
            }

            var currency = _currencies[_currencyIndex];
            string state = currency.Enabled ? "enabled" : "disabled";
            Speech.Say($"{currency.DisplayName}: {currency.Amount}, {state}");
        }

        private void AnnounceRace()
        {
            if (_races == null || _raceIndex < 0 || _raceIndex >= _races.Count)
            {
                Speech.Say("No races");
                return;
            }

            var race = _races[_raceIndex];
            string state = race.Enabled ? "enabled" : "disabled";
            Speech.Say($"{race.DisplayName}: {race.Count}, {state}");
        }

        private void AnnounceCornerstone()
        {
            if (_cornerstones == null || _cornerstoneIndex < 0 || _cornerstoneIndex >= _cornerstones.Count)
            {
                Speech.Say("No cornerstones");
                return;
            }

            var cornerstone = _cornerstones[_cornerstoneIndex];

            // Build price string
            string priceStr = $"{cornerstone.MetaPrice} value";

            // Add villager cost if villagers are allowed and there's a cost
            if (AltarReflection.AreVillagersAllowed() && cornerstone.VillagersPrice > 0)
            {
                priceStr += $" + {cornerstone.VillagersPrice} villagers";
            }

            // Affordability
            string affordStr = cornerstone.CanAfford ? "can afford" : "cannot afford";

            // Upgrade indicator
            string upgradeStr = cornerstone.IsUpgrade ? ", upgrade" : "";

            Speech.Say($"{cornerstone.DisplayName}, {priceStr}, {affordStr}{upgradeStr}");
        }

        // ========================================
        // DRILL DOWN / GO BACK
        // ========================================

        private void DrillDown()
        {
            switch (_level)
            {
                case MenuLevel.Main:
                    switch ((MainItem)_mainIndex)
                    {
                        case MainItem.Resources:
                            _level = MenuLevel.Resources;
                            _resourceIndex = 0;
                            AnnounceResourceItem();
                            break;
                        case MainItem.Cornerstones:
                            _level = MenuLevel.Cornerstones;
                            _cornerstoneIndex = 0;
                            if (_cornerstones != null && _cornerstones.Count > 0)
                                AnnounceCornerstone();
                            else
                                Speech.Say("No cornerstones available");
                            break;
                        case MainItem.Skip:
                            ExecuteSkip();
                            break;
                    }
                    break;

                case MenuLevel.Resources:
                    switch ((ResourceItem)_resourceIndex)
                    {
                        case ResourceItem.Currencies:
                            _level = MenuLevel.Currencies;
                            _currencyIndex = 0;
                            _currencies = AltarReflection.GetCurrencies();
                            if (_currencies != null && _currencies.Count > 0)
                                AnnounceCurrency();
                            else
                                Speech.Say("No currencies available");
                            break;
                        case ResourceItem.Villagers:
                            _level = MenuLevel.Races;
                            _raceIndex = 0;
                            _races = AltarReflection.GetRaces();
                            if (_races != null && _races.Count > 0)
                                AnnounceRace();
                            else
                                Speech.Say("No races available");
                            break;
                    }
                    break;

                case MenuLevel.Currencies:
                    // Toggle on Enter as well
                    ToggleCurrency();
                    break;

                case MenuLevel.Races:
                    // Toggle on Enter as well
                    ToggleRace();
                    break;

                case MenuLevel.Cornerstones:
                    PurchaseCornerstone();
                    break;
            }
        }

        private void GoBack()
        {
            switch (_level)
            {
                case MenuLevel.Main:
                    // Already at top - do nothing (Escape will close)
                    break;
                case MenuLevel.Resources:
                    _level = MenuLevel.Main;
                    AnnounceMainItem();
                    break;
                case MenuLevel.Currencies:
                case MenuLevel.Races:
                    _level = MenuLevel.Resources;
                    AnnounceResourceItem();
                    break;
                case MenuLevel.Cornerstones:
                    _level = MenuLevel.Main;
                    AnnounceMainItem();
                    break;
            }
        }

        // ========================================
        // TOGGLE ACTIONS
        // ========================================

        private void ToggleCurrent()
        {
            switch (_level)
            {
                case MenuLevel.Currencies:
                    ToggleCurrency();
                    break;
                case MenuLevel.Races:
                    ToggleRace();
                    break;
                case MenuLevel.Resources:
                    if ((ResourceItem)_resourceIndex == ResourceItem.Villagers)
                    {
                        ToggleVillagersMaster();
                    }
                    break;
                default:
                    // No toggle action for other levels
                    break;
            }
        }

        private void ToggleCurrency()
        {
            if (_currencies == null || _currencyIndex < 0 || _currencyIndex >= _currencies.Count) return;

            if (AltarReflection.ToggleCurrency(_currencyIndex))
            {
                SoundManager.PlayButtonClick();
                // Refresh and re-announce
                _currencies = AltarReflection.GetCurrencies();
                AnnounceCurrency();
            }
            else
            {
                Speech.Say("Cannot toggle");
                SoundManager.PlayFailed();
            }
        }

        private void ToggleRace()
        {
            if (_races == null || _raceIndex < 0 || _raceIndex >= _races.Count) return;

            if (AltarReflection.ToggleRace(_raceIndex))
            {
                SoundManager.PlayButtonClick();
                // Refresh and re-announce
                _races = AltarReflection.GetRaces();
                AnnounceRace();
            }
            else
            {
                Speech.Say("Cannot toggle");
                SoundManager.PlayFailed();
            }
        }

        private void ToggleVillagersMaster()
        {
            if (AltarReflection.ToggleVillagersAllowed())
            {
                SoundManager.PlayButtonClick();
                // Re-announce with new state
                AnnounceResourceItem();
            }
            else
            {
                Speech.Say("Cannot toggle");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // PURCHASE / SKIP
        // ========================================

        private void PurchaseCornerstone()
        {
            if (_cornerstones == null || _cornerstoneIndex < 0 || _cornerstoneIndex >= _cornerstones.Count)
            {
                Speech.Say("No cornerstone selected");
                return;
            }

            var cornerstone = _cornerstones[_cornerstoneIndex];

            if (!cornerstone.CanAfford)
            {
                Speech.Say("Cannot afford");
                SoundManager.PlayFailed();
                return;
            }

            if (AltarReflection.PickEffect(cornerstone.Model))
            {
                Speech.Say($"Purchased {cornerstone.DisplayName}");
                SoundManager.PlayButtonClick();

                // Check if there's another pick
                if (AltarReflection.HasActivePick())
                {
                    // Refresh and return to cornerstones
                    RefreshData();
                    _cornerstoneIndex = 0;
                    if (_cornerstones != null && _cornerstones.Count > 0)
                    {
                        AnnounceCornerstone();
                    }
                }
                // Otherwise panel will close via game, our Close() will be called
            }
            else
            {
                Speech.Say("Purchase failed");
                SoundManager.PlayFailed();
            }
        }

        private void ExecuteSkip()
        {
            if (AltarReflection.Skip())
            {
                Speech.Say("Skipped");
                SoundManager.PlayDecline();

                // Check if there's another pick
                if (AltarReflection.HasActivePick())
                {
                    // Refresh and return to main
                    RefreshData();
                    _level = MenuLevel.Main;
                    _mainIndex = 0;
                    AnnounceMainItem();
                }
                // Otherwise panel will close via game, our Close() will be called
            }
            else
            {
                Speech.Say("Skip failed");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            // Only search in list levels
            if (_level != MenuLevel.Currencies && _level != MenuLevel.Races && _level != MenuLevel.Cornerstones)
            {
                return;
            }

            _search.AddChar(c);

            int matchIndex = FindMatch();
            if (matchIndex >= 0)
            {
                SetCurrentIndex(matchIndex);
                AnnounceCurrent();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindMatch();
            if (matchIndex >= 0)
            {
                SetCurrentIndex(matchIndex);
                AnnounceCurrent();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatch()
        {
            if (!_search.HasBuffer) return -1;
            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            switch (_level)
            {
                case MenuLevel.Currencies:
                    if (_currencies != null)
                    {
                        for (int i = 0; i < _currencies.Count; i++)
                        {
                            if (_currencies[i].DisplayName.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i;
                        }
                    }
                    break;

                case MenuLevel.Races:
                    if (_races != null)
                    {
                        for (int i = 0; i < _races.Count; i++)
                        {
                            if (_races[i].DisplayName.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i;
                        }
                    }
                    break;

                case MenuLevel.Cornerstones:
                    if (_cornerstones != null)
                    {
                        for (int i = 0; i < _cornerstones.Count; i++)
                        {
                            if (_cornerstones[i].DisplayName.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i;
                        }
                    }
                    break;
            }

            return -1;
        }

        private void SetCurrentIndex(int index)
        {
            switch (_level)
            {
                case MenuLevel.Main:
                    _mainIndex = index;
                    break;
                case MenuLevel.Resources:
                    _resourceIndex = index;
                    break;
                case MenuLevel.Currencies:
                    _currencyIndex = index;
                    break;
                case MenuLevel.Races:
                    _raceIndex = index;
                    break;
                case MenuLevel.Cornerstones:
                    _cornerstoneIndex = index;
                    break;
            }
        }
    }
}
