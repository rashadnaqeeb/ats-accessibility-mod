using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the TradeRoutesPopup.
    /// Provides multi-level navigation:
    /// - MainMenu: Active routes summary, towns, toggles
    /// - ActiveRoutes: Navigate and collect completed routes
    /// - TownOffers: Navigate offers, adjust amount, accept
    /// </summary>
    public class TradeRoutesOverlay : IKeyHandler
    {
        // ========================================
        // NAVIGATION STATE
        // ========================================

        private enum Level { MainMenu, ActiveRoutes, TownOffers }
        private enum MainMenuItemType { ActiveRoutes, Town, AutoCollect, OnlyAvailable }

        // Navigation item for main menu
        private class MainMenuItem
        {
            public MainMenuItemType Type;
            public string Label;
            public string SearchName;
            public int TownIndex;  // For Town items - index into _towns list
        }

        // State
        private bool _isOpen;
        private Level _level;
        private int _currentIndex;
        private int _currentTownIndex;  // Which town we're viewing offers for

        // Data caches
        private List<MainMenuItem> _mainMenuItems = new List<MainMenuItem>();
        private List<TradeRoutesReflection.RouteInfo> _routes = new List<TradeRoutesReflection.RouteInfo>();
        private List<TradeRoutesReflection.TownInfo> _towns = new List<TradeRoutesReflection.TownInfo>();
        private List<TradeRoutesReflection.OfferInfo> _offers = new List<TradeRoutesReflection.OfferInfo>();

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

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
                    return ProcessMainMenuKey(keyCode, modifiers);
                case Level.ActiveRoutes:
                    return ProcessActiveRoutesKey(keyCode, modifiers);
                case Level.TownOffers:
                    return ProcessTownOffersKey(keyCode, modifiers);
                default:
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when TradeRoutesPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _level = Level.MainMenu;
            _currentIndex = 0;
            _currentTownIndex = 0;
            _search.Clear();

            RefreshAllData();
            RefreshMainMenu();

            if (_mainMenuItems.Count > 0)
            {
                Speech.Say($"Trade Routes. {_mainMenuItems[0].Label}");
            }
            else
            {
                Speech.Say("Trade Routes. No data available");
            }

            Debug.Log($"[ATSAccessibility] TradeRoutesOverlay opened");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _search.Clear();
            ClearData();

            Debug.Log("[ATSAccessibility] TradeRoutesOverlay closed");
        }

        private void ClearData()
        {
            _mainMenuItems.Clear();
            _routes.Clear();
            _towns.Clear();
            _offers.Clear();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshAllData()
        {
            _routes = TradeRoutesReflection.GetActiveRoutes();
            _towns = TradeRoutesReflection.GetTradeTowns();
        }

        private void RefreshMainMenu()
        {
            _mainMenuItems.Clear();

            // Active routes summary
            int activeCount = _routes.Count;
            int maxRoutes = TradeRoutesReflection.GetMaxRoutes();
            int readyCount = 0;
            foreach (var r in _routes)
                if (r.CanCollect) readyCount++;

            string activeLabel = readyCount > 0
                ? $"Active Routes, {activeCount} of {maxRoutes}, {readyCount} ready"
                : $"Active Routes, {activeCount} of {maxRoutes}";

            _mainMenuItems.Add(new MainMenuItem
            {
                Type = MainMenuItemType.ActiveRoutes,
                Label = activeLabel,
                SearchName = "Active"
            });

            // Town entries
            for (int i = 0; i < _towns.Count; i++)
            {
                var town = _towns[i];
                string townLabel = BuildTownLabel(town);

                _mainMenuItems.Add(new MainMenuItem
                {
                    Type = MainMenuItemType.Town,
                    Label = townLabel,
                    SearchName = town.Name,
                    TownIndex = i
                });
            }

            // Auto-collect toggle
            bool autoCollect = TradeRoutesReflection.IsAutoCollectEnabled();
            _mainMenuItems.Add(new MainMenuItem
            {
                Type = MainMenuItemType.AutoCollect,
                Label = autoCollect ? "Auto-Collect, enabled" : "Auto-Collect, disabled",
                SearchName = "Auto"
            });

            // Only available toggle - filters to show only affordable offers
            bool onlyAvailable = TradeRoutesReflection.IsOnlyAvailableEnabled();
            _mainMenuItems.Add(new MainMenuItem
            {
                Type = MainMenuItemType.OnlyAvailable,
                Label = onlyAvailable ? "Show affordable only, enabled" : "Show affordable only, disabled",
                SearchName = "Show"
            });
        }

        private void RefreshActiveRoutes()
        {
            _routes = TradeRoutesReflection.GetActiveRoutes();
        }

        private void RefreshTownOffers(int townIndex)
        {
            if (townIndex < 0 || townIndex >= _towns.Count)
            {
                _offers.Clear();
                return;
            }

            var town = _towns[townIndex];
            _offers = TradeRoutesReflection.GetTownOffers(town.State);
        }

        /// <summary>
        /// Check if the current index in TownOffers is the Extend Offers item (always index 0).
        /// </summary>
        private bool IsExtendOffersItem => _level == Level.TownOffers && _currentIndex == 0;

        /// <summary>
        /// Get the actual offer index (accounting for Extend Offers item at index 0).
        /// </summary>
        private int GetOfferIndex(int menuIndex) => menuIndex - 1;

        /// <summary>
        /// Get the total item count in TownOffers (offers + extend item).
        /// </summary>
        private int GetTownOffersItemCount() => _offers.Count + 1;

        // ========================================
        // MAIN MENU LEVEL
        // ========================================

        private bool ProcessMainMenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
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
                    if (_mainMenuItems.Count > 0)
                    {
                        _currentIndex = 0;
                        Speech.Say(_mainMenuItems[_currentIndex].Label);
                    }
                    return true;

                case KeyCode.End:
                    if (_mainMenuItems.Count > 0)
                    {
                        _currentIndex = _mainMenuItems.Count - 1;
                        Speech.Say(_mainMenuItems[_currentIndex].Label);
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateMainMenuItem();
                    return true;

                case KeyCode.RightArrow:
                    DrillIntoSubmenu();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup
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
                        HandleMainMenuSearch(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private void NavigateMainMenu(int direction)
        {
            if (_mainMenuItems.Count == 0) return;
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _mainMenuItems.Count);
            Speech.Say(_mainMenuItems[_currentIndex].Label);
        }

        private void ActivateMainMenuItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _mainMenuItems.Count) return;

            var item = _mainMenuItems[_currentIndex];
            switch (item.Type)
            {
                case MainMenuItemType.ActiveRoutes:
                    EnterActiveRoutes();
                    break;

                case MainMenuItemType.Town:
                    EnterTownOffers(item);
                    break;

                case MainMenuItemType.AutoCollect:
                    ToggleAutoCollect();
                    break;

                case MainMenuItemType.OnlyAvailable:
                    ToggleOnlyAvailable();
                    break;
            }
        }

        /// <summary>
        /// Right arrow only drills into submenus (Active Routes, Towns), not toggles.
        /// </summary>
        private void DrillIntoSubmenu()
        {
            if (_currentIndex < 0 || _currentIndex >= _mainMenuItems.Count) return;

            var item = _mainMenuItems[_currentIndex];
            switch (item.Type)
            {
                case MainMenuItemType.ActiveRoutes:
                    EnterActiveRoutes();
                    break;

                case MainMenuItemType.Town:
                    EnterTownOffers(item);
                    break;

                // Toggles do nothing on right arrow
                case MainMenuItemType.AutoCollect:
                case MainMenuItemType.OnlyAvailable:
                    break;
            }
        }

        private void ToggleAutoCollect()
        {
            bool current = TradeRoutesReflection.IsAutoCollectEnabled();
            bool newState = !current;
            TradeRoutesReflection.SetAutoCollect(newState);
            SoundManager.PlayButtonClick();

            // Update label
            var item = _mainMenuItems[_currentIndex];
            item.Label = newState ? "Auto-Collect, enabled" : "Auto-Collect, disabled";

            // When enabling auto-collect, immediately collect all ready routes (matches game behavior)
            if (newState)
            {
                int collected = TradeRoutesReflection.AutoCollectAllReady();
                if (collected > 0)
                {
                    RefreshAllData();
                    RefreshMainMenu();
                    Speech.Say($"{item.Label}, collected {collected} routes");
                    return;
                }
            }

            Speech.Say(item.Label);
        }

        private string BuildTownLabel(TradeRoutesReflection.TownInfo town)
        {
            // Format: "Name, Faction (if any), distance X, Standing Y, label, progress"
            var parts = new System.Collections.Generic.List<string>();
            parts.Add(town.Name);

            // Add faction if present
            if (!string.IsNullOrEmpty(town.Faction))
            {
                parts.Add(town.Faction);
            }

            // Add distance
            parts.Add($"distance {town.Distance}");

            // Add standing info
            parts.Add($"Standing {town.StandingLevel}");
            parts.Add(town.StandingLabel);

            // Add progress or max
            if (town.IsMaxStanding)
            {
                parts.Add("max");
            }
            else
            {
                parts.Add($"{town.CurrentStandingValue} of {town.ValueForLevelUp}");
            }

            return string.Join(", ", parts);
        }

        private void ToggleOnlyAvailable()
        {
            bool current = TradeRoutesReflection.IsOnlyAvailableEnabled();
            TradeRoutesReflection.SetOnlyAvailable(!current);
            SoundManager.PlayButtonClick();

            // Update label
            var item = _mainMenuItems[_currentIndex];
            item.Label = !current ? "Show affordable only, enabled" : "Show affordable only, disabled";
            Speech.Say(item.Label);
        }

        private void HandleMainMenuSearch(char c)
        {
            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _mainMenuItems.Count; i++)
            {
                var item = _mainMenuItems[i];
                if (!string.IsNullOrEmpty(item.SearchName) &&
                    item.SearchName.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    Speech.Say(item.Label);
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }

        // ========================================
        // ACTIVE ROUTES LEVEL
        // ========================================

        private void EnterActiveRoutes()
        {
            RefreshActiveRoutes();

            if (_routes.Count == 0)
            {
                Speech.Say("No active routes");
                SoundManager.PlayFailed();
                return;
            }

            _level = Level.ActiveRoutes;
            _currentIndex = 0;
            _search.Clear();

            Speech.Say($"Active Routes. {BuildRouteLabel(_routes[0])}");
        }

        private bool ProcessActiveRoutesKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateRoutes(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateRoutes(1);
                    return true;

                case KeyCode.Home:
                    if (_routes.Count > 0)
                    {
                        _currentIndex = 0;
                        Speech.Say(BuildRouteLabel(_routes[_currentIndex]));
                    }
                    return true;

                case KeyCode.End:
                    if (_routes.Count > 0)
                    {
                        _currentIndex = _routes.Count - 1;
                        Speech.Say(BuildRouteLabel(_routes[_currentIndex]));
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    CollectCurrentRoute();
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToMainMenu();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Return to main menu
                    ReturnToMainMenu();
                    return true;

                default:
                    // Consume all keys
                    return true;
            }
        }

        private void NavigateRoutes(int direction)
        {
            if (_routes.Count == 0) return;
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _routes.Count);
            Speech.Say(BuildRouteLabel(_routes[_currentIndex]));
        }

        private string BuildRouteLabel(TradeRoutesReflection.RouteInfo route)
        {
            if (route.CanCollect)
            {
                return $"{route.GoodName}, {route.GoodAmount}, to {route.TownName}, ready to collect, {route.PriceAmount} {route.PriceName}";
            }
            else
            {
                int percent = Mathf.RoundToInt(route.Progress * 100);
                string time = TradeRoutesReflection.FormatTime(route.TimeRemaining);
                return $"{route.GoodName}, {route.GoodAmount}, to {route.TownName}, {percent}%, {time}";
            }
        }

        private void CollectCurrentRoute()
        {
            if (_currentIndex < 0 || _currentIndex >= _routes.Count) return;

            var route = _routes[_currentIndex];
            if (!route.CanCollect)
            {
                int percent = Mathf.RoundToInt(route.Progress * 100);
                Speech.Say($"Not ready, {percent}%");
                SoundManager.PlayFailed();
                return;
            }

            if (TradeRoutesReflection.Collect(route.State))
            {
                SoundManager.PlayButtonClick();
                Speech.Say($"Collected {route.PriceAmount} {route.PriceName}");

                // Refresh routes
                RefreshActiveRoutes();

                if (_routes.Count == 0)
                {
                    // Return to main menu - data changed since we collected a route
                    ReturnToMainMenu(true);
                }
                else
                {
                    // Clamp index and announce current
                    _currentIndex = Mathf.Min(_currentIndex, _routes.Count - 1);
                    Speech.Say(BuildRouteLabel(_routes[_currentIndex]));
                }
            }
            else
            {
                Speech.Say("Failed to collect");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // TOWN OFFERS LEVEL
        // ========================================

        private void EnterTownOffers(MainMenuItem item)
        {
            // Use the stored town index directly
            if (item.TownIndex < 0 || item.TownIndex >= _towns.Count)
            {
                Speech.Say("Town not found");
                SoundManager.PlayFailed();
                return;
            }

            _currentTownIndex = item.TownIndex;
            RefreshTownOffers(_currentTownIndex);

            _level = Level.TownOffers;
            _currentIndex = 0;
            _search.Clear();

            // Build header with town info and extend option
            var town = _towns[_currentTownIndex];
            AnnounceTownHeader(town);
        }

        private void AnnounceTownHeader(TradeRoutesReflection.TownInfo town)
        {
            // First announce town standing info, then the Extend Offers item (index 0)
            string standingInfo = town.IsMaxStanding
                ? $"{town.Name}, {town.StandingLabel}"
                : $"{town.Name}, {town.StandingLabel}, {town.CurrentStandingValue} of {town.ValueForLevelUp}";

            Speech.Say($"{standingInfo}. {BuildExtendOffersLabel(town)}");
        }

        private string BuildExtendOffersLabel(TradeRoutesReflection.TownInfo town)
        {
            if (town.CanExtend)
            {
                return $"Extend Offers, costs {town.ExtendCost}, available";
            }
            else if (town.ReachedMaxOffers)
            {
                return "Extend Offers, maximum reached";
            }
            else
            {
                return $"Extend Offers, costs {town.ExtendCost}, not enough resources";
            }
        }

        private bool ProcessTownOffersKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateOffers(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateOffers(1);
                    return true;

                case KeyCode.Home:
                    {
                        int itemCount = GetTownOffersItemCount();
                        if (itemCount > 0)
                        {
                            _currentIndex = 0;
                            AnnounceTownOffersItem();
                        }
                    }
                    return true;

                case KeyCode.End:
                    {
                        int itemCount = GetTownOffersItemCount();
                        if (itemCount > 0)
                        {
                            _currentIndex = itemCount - 1;
                            AnnounceTownOffersItem();
                        }
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateTownOffersItem();
                    return true;

                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals:
                    AdjustAmount(1);
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    AdjustAmount(-1);
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToMainMenu();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Return to main menu
                    ReturnToMainMenu();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleOfferSearch(c);
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateOffers(int direction)
        {
            int itemCount = GetTownOffersItemCount();
            if (itemCount == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, itemCount);
            AnnounceTownOffersItem();
        }

        private void AnnounceTownOffersItem()
        {
            if (IsExtendOffersItem)
            {
                // Index 0 is Extend Offers
                var town = _towns[_currentTownIndex];
                Speech.Say(BuildExtendOffersLabel(town));
            }
            else
            {
                // Actual offer
                int offerIndex = GetOfferIndex(_currentIndex);
                if (offerIndex >= 0 && offerIndex < _offers.Count)
                {
                    Speech.Say(BuildOfferLabel(_offers[offerIndex]));
                }
            }
        }

        private void ActivateTownOffersItem()
        {
            if (IsExtendOffersItem)
            {
                ExtendOffers();
            }
            else
            {
                AcceptCurrentOffer();
            }
        }

        private string BuildOfferLabel(TradeRoutesReflection.OfferInfo offer)
        {
            // Base: "Planks, 10"
            string baseLabel = $"{offer.GoodName}, {offer.GoodAmount * offer.Multiplier}";

            // Add multiplier if > 1
            if (offer.Multiplier > 1)
            {
                baseLabel += $", x{offer.Multiplier}";
            }

            // Add reward
            baseLabel += $", sells for {offer.PriceAmount} {offer.PriceName}";

            // Add travel time
            baseLabel += $", time {TradeRoutesReflection.FormatTime(offer.TravelTime)}";

            // Add fuel requirement
            baseLabel += $", requires {offer.FuelAmount} {offer.FuelName}";

            // Add availability
            if (offer.Accepted)
            {
                baseLabel += ", already accepted";
            }
            else if (!string.IsNullOrEmpty(offer.BlockedReason))
            {
                // Use fuel name in the blocked reason for clarity
                string reason = offer.BlockedReason;
                if (reason == "not enough fuel")
                {
                    reason = $"not enough {offer.FuelName}";
                }
                baseLabel += $", {reason}";
            }
            else
            {
                baseLabel += ", available";
            }

            return baseLabel;
        }

        private void AdjustAmount(int delta)
        {
            // +/- only works on actual offers, not on Extend Offers item
            if (IsExtendOffersItem)
            {
                Speech.Say("Navigate to an offer to adjust amount");
                return;
            }

            int offerIndex = GetOfferIndex(_currentIndex);
            if (offerIndex < 0 || offerIndex >= _offers.Count) return;

            var offer = _offers[offerIndex];
            if (offer.Accepted)
            {
                Speech.Say("Already accepted");
                SoundManager.PlayFailed();
                return;
            }

            int current = TradeRoutesReflection.GetOfferAmount(offer.State);
            int newAmount = Mathf.Clamp(current + delta, 1, offer.MaxMultiplier);

            if (newAmount != current)
            {
                TradeRoutesReflection.SetOfferAmount(offer.State, newAmount);
                SoundManager.PlayButtonClick();

                // Refresh the offer data to get new calculations
                RefreshTownOffers(_currentTownIndex);
                // Announce updated offer
                if (offerIndex < _offers.Count)
                {
                    Speech.Say(BuildOfferLabel(_offers[offerIndex]));
                }
            }
            else
            {
                // At limit
                Speech.Say(newAmount == 1 ? "Minimum amount" : "Maximum amount");
            }
        }

        private void AcceptCurrentOffer()
        {
            int offerIndex = GetOfferIndex(_currentIndex);
            if (offerIndex < 0 || offerIndex >= _offers.Count) return;

            var offer = _offers[offerIndex];

            if (offer.Accepted)
            {
                Speech.Say("Already accepted");
                SoundManager.PlayFailed();
                return;
            }

            if (!offer.CanAccept)
            {
                Speech.Say(offer.BlockedReason ?? "Cannot accept");
                SoundManager.PlayFailed();
                return;
            }

            if (TradeRoutesReflection.AcceptOffer(offer.State))
            {
                SoundManager.PlayButtonClick();
                Speech.Say($"Accepted, {offer.GoodAmount * offer.Multiplier} {offer.GoodName} to {offer.TownName}");

                // Refresh data
                RefreshAllData();
                RefreshTownOffers(_currentTownIndex);

                // Check if we should stay or go back
                if (_offers.Count == 0)
                {
                    ReturnToMainMenu();
                }
                else
                {
                    // Clamp to valid range (index 0 is Extend Offers, so max is _offers.Count)
                    _currentIndex = Mathf.Min(_currentIndex, _offers.Count);
                    AnnounceTownOffersItem();
                }
            }
            else
            {
                Speech.Say("Failed to accept");
                SoundManager.PlayFailed();
            }
        }

        private void ExtendOffers()
        {
            if (_currentTownIndex < 0 || _currentTownIndex >= _towns.Count) return;

            var town = _towns[_currentTownIndex];
            if (!town.CanExtend)
            {
                Speech.Say("Cannot extend offers");
                SoundManager.PlayFailed();
                return;
            }

            if (TradeRoutesReflection.ExtendOffer(town.State))
            {
                SoundManager.PlayButtonClick();

                // Refresh data
                _towns = TradeRoutesReflection.GetTradeTowns();
                RefreshTownOffers(_currentTownIndex);

                if (_offers.Count > 0)
                {
                    // Go to new offer (last one). Index 0 is Extend Offers, so last offer is at _offers.Count
                    _currentIndex = _offers.Count;
                    Speech.Say($"Offers extended. {BuildOfferLabel(_offers[_offers.Count - 1])}");
                }
                else
                {
                    Speech.Say("Offers extended");
                }
            }
            else
            {
                Speech.Say("Failed to extend");
                SoundManager.PlayFailed();
            }
        }

        private void HandleOfferSearch(char c)
        {
            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                if (offer.GoodName.ToLowerInvariant().StartsWith(prefix))
                {
                    // Offer index i maps to menu index i+1 (index 0 is Extend Offers)
                    _currentIndex = i + 1;
                    Speech.Say(BuildOfferLabel(offer));
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }

        // ========================================
        // NAVIGATION HELPERS
        // ========================================

        private void ReturnToMainMenu(bool dataChanged = false)
        {
            _level = Level.MainMenu;
            _search.Clear();

            if (dataChanged)
            {
                RefreshAllData();
                RefreshMainMenu();
            }

            _currentIndex = 0;
            InputBlocker.BlockCancelOnce = true;
            Speech.Say($"Main menu. {_mainMenuItems[0].Label}");
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
            }
        }
    }
}
