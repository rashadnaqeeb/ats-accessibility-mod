using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the TraderPanel.
    /// Provides multi-level navigation for trader interaction:
    /// - Mode 1 (No Trader): Flat list with next trader info and force arrival
    /// - Mode 2 (Trader Present): Main menu with goods trading, perks, and assault
    /// </summary>
    public class TraderOverlay : IKeyHandler
    {
        // ========================================
        // NAVIGATION STATE
        // ========================================

        private enum Mode { NoTrader, TraderPresent }
        private enum Level { MainMenu, GoodsTrade, Perks, AssaultConfirm, TradeConfirm }
        private enum Tab { Sell, Buy }

        // Navigation item for flat lists
        private class NavItem
        {
            public string Label;
            public string SearchName;
            public Action OnActivate;
        }

        // Trading good with current offer state
        private class TradeGoodItem
        {
            public string Name;
            public string DisplayName;
            public int MaxAmount;
            public int OfferedAmount;
            public float UnitValue;
            public bool IsSell;  // true = selling to trader, false = buying from trader
        }

        // Perk item
        private class PerkItem
        {
            public string Name;
            public string DisplayName;
            public string Description;
            public float Price;
            public bool Discounted;
            public float DiscountRatio;
            public bool Sold;
            public object EffectState;
        }

        // State
        private bool _isOpen;
        private Mode _mode;
        private Level _level;
        private Tab _currentTab;
        private int _currentIndex;
        private bool _inConfirmation;

        // Mode 1 (No Trader) data
        private List<NavItem> _noTraderItems = new List<NavItem>();

        // Mode 2 (Trader Present) data
        private List<NavItem> _mainMenuItems = new List<NavItem>();
        private List<TradeGoodItem> _sellGoods = new List<TradeGoodItem>();
        private List<TradeGoodItem> _buyGoods = new List<TradeGoodItem>();
        private List<PerkItem> _perks = new List<PerkItem>();

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

            // Confirmation mode (trade or assault)
            if (_inConfirmation)
            {
                return ProcessConfirmationKey(keyCode);
            }

            // Mode-specific handling
            if (_mode == Mode.NoTrader)
            {
                return ProcessNoTraderKey(keyCode, modifiers);
            }
            else
            {
                return ProcessTraderPresentKey(keyCode, modifiers);
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when TraderPanel is shown.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _currentIndex = 0;
            _level = Level.MainMenu;
            _currentTab = Tab.Sell;
            _inConfirmation = false;
            _search.Clear();

            // Determine mode based on trader presence
            if (TradeReflection.IsTraderPresent())
            {
                _mode = Mode.TraderPresent;
                RefreshTraderData();
                AnnounceTraderPresent();
            }
            else
            {
                _mode = Mode.NoTrader;
                RefreshNoTraderData();
                AnnounceNoTrader();
            }

            Debug.Log($"[ATSAccessibility] TraderOverlay opened, mode: {_mode}");
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

            Debug.Log("[ATSAccessibility] TraderOverlay closed");
        }

        private void ClearData()
        {
            _noTraderItems.Clear();
            _mainMenuItems.Clear();
            _sellGoods.Clear();
            _buyGoods.Clear();
            _perks.Clear();
        }

        // ========================================
        // MODE 1: NO TRADER
        // ========================================

        private void RefreshNoTraderData()
        {
            _noTraderItems.Clear();

            string traderName = TradeReflection.GetTraderName();
            string traderLabel = TradeReflection.GetTraderLabel() ?? "";

            // Check if no trader is selected yet
            if (string.IsNullOrEmpty(traderName))
            {
                // No trader selected - show appropriate message
                if (TradeReflection.IsTradingBlocked())
                {
                    _noTraderItems.Add(new NavItem
                    {
                        Label = "Trading is blocked",
                        SearchName = null,
                        OnActivate = null
                    });
                }
                else
                {
                    // Traders are scared (high impatience/fear)
                    _noTraderItems.Add(new NavItem
                    {
                        Label = "No trader on the way. Traders may be too scared to visit",
                        SearchName = null,
                        OnActivate = null
                    });
                }
                return;
            }

            // Item 0: Next trader info
            float progress = TradeReflection.GetTravelProgress();
            float timeToArrival = TradeReflection.GetTimeToArrival();
            bool isStorm = TradeReflection.IsStormSeason();
            string arrivalInfo;

            if (TradeReflection.IsTradingBlocked())
            {
                arrivalInfo = "Trading blocked";
            }
            else if (isStorm)
            {
                // During storm, travel is paused
                float stormEnds = TradeReflection.GetTimeTillSeasonChange();
                if (progress >= 1f)
                {
                    // Trader is ready but waiting for storm to end
                    arrivalInfo = $"waiting for storm to end, {TradeReflection.FormatTime(stormEnds)} remaining";
                }
                else
                {
                    // Trader travel paused during storm
                    arrivalInfo = $"travel paused during storm, {Mathf.RoundToInt(progress * 100)}% traveled, storm ends in {TradeReflection.FormatTime(stormEnds)}";
                }
            }
            else if (timeToArrival > 0)
            {
                arrivalInfo = $"arriving in {TradeReflection.FormatTime(timeToArrival)}";
            }
            else if (progress < 1f)
            {
                arrivalInfo = $"{Mathf.RoundToInt(progress * 100)}% traveled";
            }
            else
            {
                arrivalInfo = "arriving soon";
            }

            string headerLabel = !string.IsNullOrEmpty(traderLabel)
                ? $"Next trader: {traderName}, {traderLabel}, {arrivalInfo}"
                : $"Next trader: {traderName}, {arrivalInfo}";

            _noTraderItems.Add(new NavItem
            {
                Label = headerLabel,
                SearchName = null,
                OnActivate = null
            });

            // Item 1: Description
            string description = TradeReflection.GetTraderDescription();
            if (!string.IsNullOrEmpty(description))
            {
                _noTraderItems.Add(new NavItem
                {
                    Label = description,
                    SearchName = null,
                    OnActivate = null
                });
            }

            // Item 2: Force arrival option
            // Don't show at all if trading is blocked (out of user control)
            if (!TradeReflection.IsTradingBlocked())
            {
                if (TradeReflection.CanForceArrival())
                {
                    float cost = TradeReflection.GetForceArrivalCost();
                    _noTraderItems.Add(new NavItem
                    {
                        Label = $"Force arrival, costs {cost:F1} Impatience",
                        SearchName = "Force",
                        OnActivate = ActivateForceArrival
                    });
                }
                else
                {
                    // Show specific reason why force arrival is unavailable
                    string reason = TradeReflection.GetForceArrivalUnavailableReason() ?? "unavailable";
                    _noTraderItems.Add(new NavItem
                    {
                        Label = $"Force arrival: {reason}",
                        SearchName = null,
                        OnActivate = null
                    });
                }
            }
        }

        private void AnnounceNoTrader()
        {
            if (_noTraderItems.Count > 0)
            {
                Speech.Say($"Trader. {_noTraderItems[0].Label}");
            }
            else
            {
                Speech.Say("Trader. No trader information available");
            }
        }

        private bool ProcessNoTraderKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateNoTrader(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateNoTrader(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateNoTraderItem();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close panel
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
                        HandleSearch(c, _noTraderItems, item => item.SearchName);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private void NavigateNoTrader(int direction)
        {
            if (_noTraderItems.Count == 0) return;
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _noTraderItems.Count);
            Speech.Say(_noTraderItems[_currentIndex].Label);
        }

        private void ActivateNoTraderItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _noTraderItems.Count) return;

            var item = _noTraderItems[_currentIndex];
            if (item.OnActivate != null)
            {
                item.OnActivate();
            }
            else
            {
                // Re-announce read-only item
                Speech.Say(item.Label);
            }
        }

        private void ActivateForceArrival()
        {
            if (!TradeReflection.CanForceArrival())
            {
                Speech.Say("Cannot force arrival");
                SoundManager.PlayFailed();
                return;
            }

            if (TradeReflection.ForceTraderArrival())
            {
                SoundManager.PlayButtonClick();
                Speech.Say("Trader arrival forced");
                RefreshNoTraderData();
                _currentIndex = 0;
                if (_noTraderItems.Count > 0)
                    Speech.Say(_noTraderItems[0].Label);
            }
            else
            {
                Speech.Say("Failed to force arrival");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // MODE 2: TRADER PRESENT
        // ========================================

        private void RefreshTraderData()
        {
            // Refresh data first, then build menu (which counts perks)
            RefreshSellGoods();
            RefreshBuyGoods();
            RefreshPerks();
            RefreshMainMenu();
        }

        private void RefreshMainMenu()
        {
            _mainMenuItems.Clear();

            string traderName = TradeReflection.GetTraderName() ?? "Unknown";
            string traderLabel = TradeReflection.GetTraderLabel() ?? "";
            float timeLeft = TradeReflection.GetStayingTimeLeft();
            string dialogue = TradeReflection.GetTraderDialogue() ?? "";

            // Item 0: Trader info
            string infoLabel = !string.IsNullOrEmpty(traderLabel)
                ? $"{traderName}, {traderLabel}, {TradeReflection.FormatTime(timeLeft)} remaining"
                : $"{traderName}, {TradeReflection.FormatTime(timeLeft)} remaining";

            if (!string.IsNullOrEmpty(dialogue))
                infoLabel += $". {dialogue}";

            _mainMenuItems.Add(new NavItem
            {
                Label = infoLabel,
                SearchName = null,
                OnActivate = null
            });

            // Item 1: Goods Trade
            _mainMenuItems.Add(new NavItem
            {
                Label = "Goods Trade",
                SearchName = "Goods",
                OnActivate = () => EnterGoodsTrade()
            });

            // Item 2: Perks
            int unsoldPerks = 0;
            foreach (var p in _perks)
                if (!p.Sold) unsoldPerks++;

            _mainMenuItems.Add(new NavItem
            {
                Label = unsoldPerks > 0 ? $"Perks, {unsoldPerks} available" : "Perks, none available",
                SearchName = "Perks",
                OnActivate = () => EnterPerks()
            });

            // Item 3: Assault (if available)
            if (TradeReflection.CanAssaultTrader())
            {
                _mainMenuItems.Add(new NavItem
                {
                    Label = "Assault Trader",
                    SearchName = "Assault",
                    OnActivate = () => EnterAssaultConfirm()
                });
            }
        }

        private void RefreshSellGoods()
        {
            _sellGoods.Clear();
            var goods = TradeReflection.GetVillageGoods();
            foreach (var g in goods)
            {
                _sellGoods.Add(new TradeGoodItem
                {
                    Name = g.Name,
                    DisplayName = g.DisplayName,
                    MaxAmount = g.StorageAmount,
                    OfferedAmount = 0,
                    UnitValue = g.UnitValue,
                    IsSell = true
                });
            }
        }

        private void RefreshBuyGoods()
        {
            _buyGoods.Clear();
            var goods = TradeReflection.GetTraderGoods();
            foreach (var g in goods)
            {
                _buyGoods.Add(new TradeGoodItem
                {
                    Name = g.Name,
                    DisplayName = g.DisplayName,
                    MaxAmount = g.StorageAmount,
                    OfferedAmount = 0,
                    UnitValue = g.UnitValue,
                    IsSell = false
                });
            }
        }

        private void RefreshPerks()
        {
            _perks.Clear();
            var perks = TradeReflection.GetPerks();
            foreach (var p in perks)
            {
                _perks.Add(new PerkItem
                {
                    Name = p.Name,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    Price = p.Price,
                    Discounted = p.Discounted,
                    DiscountRatio = p.DiscountRatio,
                    Sold = p.Sold,
                    EffectState = p.EffectState
                });
            }
        }

        private void AnnounceTraderPresent()
        {
            if (_mainMenuItems.Count > 0)
            {
                Speech.Say($"Trader. {_mainMenuItems[0].Label}");
            }
            else
            {
                Speech.Say("Trader");
            }
        }

        private bool ProcessTraderPresentKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (_level)
            {
                case Level.MainMenu:
                    return ProcessMainMenuKey(keyCode, modifiers);
                case Level.GoodsTrade:
                    return ProcessGoodsTradeKey(keyCode, modifiers);
                case Level.Perks:
                    return ProcessPerksKey(keyCode, modifiers);
                case Level.AssaultConfirm:
                    return ProcessAssaultConfirmKey(keyCode);
                default:
                    return true;
            }
        }

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

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateMainMenuItem();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close panel
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
                        HandleSearch(c, _mainMenuItems, item => item.SearchName);
                        return true;
                    }
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
            if (item.OnActivate != null)
            {
                item.OnActivate();
            }
            else
            {
                // Re-announce read-only item
                Speech.Say(item.Label);
            }
        }

        // ========================================
        // GOODS TRADE LEVEL
        // ========================================

        private void EnterGoodsTrade()
        {
            _level = Level.GoodsTrade;
            _currentTab = Tab.Sell;
            _currentIndex = 0;
            _search.Clear();

            SoundManager.PlayButtonClick();
            AnnounceSellTab();
        }

        private void AnnounceSellTab()
        {
            if (_sellGoods.Count > 0)
            {
                Speech.Say($"Sell tab. {BuildGoodLabel(_sellGoods[0])}");
            }
            else
            {
                Speech.Say("Sell tab. No goods to sell");
            }
        }

        private void AnnounceBuyTab()
        {
            if (_buyGoods.Count > 0)
            {
                Speech.Say($"Buy tab. {BuildGoodLabel(_buyGoods[0])}");
            }
            else
            {
                Speech.Say("Buy tab. No goods available");
            }
        }

        private string BuildGoodLabel(TradeGoodItem good)
        {
            if (good.OfferedAmount > 0)
            {
                float totalValue = good.IsSell
                    ? TradeReflection.GetGoodSellValue(good.Name, good.OfferedAmount)
                    : TradeReflection.GetGoodBuyValue(good.Name, good.OfferedAmount);

                if (good.IsSell)
                    return $"{good.DisplayName}, {good.MaxAmount} stored, {good.OfferedAmount} offered, {totalValue:F2} Amber";
                else
                    return $"{good.DisplayName}, {good.MaxAmount} available, {good.OfferedAmount} requested, {totalValue:F2} Amber";
            }
            else
            {
                if (good.IsSell)
                    return $"{good.DisplayName}, {good.MaxAmount} stored, {good.UnitValue:F2} each";
                else
                    return $"{good.DisplayName}, {good.MaxAmount} available, {good.UnitValue:F2} each";
            }
        }

        private bool ProcessGoodsTradeKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;

            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                    if (_currentTab != Tab.Sell)
                    {
                        _currentTab = Tab.Sell;
                        _currentIndex = 0;
                        _search.Clear();
                        AnnounceSellTab();
                    }
                    return true;

                case KeyCode.RightArrow:
                    if (_currentTab != Tab.Buy)
                    {
                        _currentTab = Tab.Buy;
                        _currentIndex = 0;
                        _search.Clear();
                        AnnounceBuyTab();
                    }
                    return true;

                case KeyCode.UpArrow:
                    NavigateGoods(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateGoods(1);
                    return true;

                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals:
                    AdjustQuantity(modifiers.Shift ? 10 : 1);
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    AdjustQuantity(modifiers.Shift ? -10 : -1);
                    return true;

                case KeyCode.B:
                    if (modifiers.Alt)
                    {
                        AnnounceBalance();
                        return true;
                    }
                    break;

                case KeyCode.A:
                    if (modifiers.Alt)
                    {
                        TryAcceptTrade();
                        return true;
                    }
                    break;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Go back to main menu
                    _level = Level.MainMenu;
                    _currentIndex = 1; // Goods Trade item
                    Speech.Say("Main menu. Goods Trade");
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;
            }

            // Type-ahead search (A-Z) - only if not Alt modifier
            if (!modifiers.Alt && keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
            {
                char c = (char)('a' + (keyCode - KeyCode.A));
                HandleGoodsSearch(c);
                return true;
            }

            return true;
        }

        private void NavigateGoods(int direction)
        {
            var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
            if (currentList.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, currentList.Count);
            Speech.Say(BuildGoodLabel(currentList[_currentIndex]));
        }

        private void AdjustQuantity(int delta)
        {
            var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
            if (_currentIndex < 0 || _currentIndex >= currentList.Count) return;

            var good = currentList[_currentIndex];
            int oldAmount = good.OfferedAmount;
            good.OfferedAmount = Mathf.Clamp(good.OfferedAmount + delta, 0, good.MaxAmount);

            if (good.OfferedAmount != oldAmount)
            {
                SoundManager.PlayButtonClick();
            }

            // Announce just the new quantity and balance
            float balance = CalculateBalance();
            Speech.Say($"{good.OfferedAmount}, balance {balance:F2}");
        }

        /// <summary>
        /// Calculate current trade balance (positive = fair, negative = unfair).
        /// </summary>
        private float CalculateBalance()
        {
            CalculateTradeTotals(out float sellTotal, out float buyTotal);
            return sellTotal - buyTotal;
        }

        private void AnnounceBalance()
        {
            CalculateTradeTotals(out float sellTotal, out float buyTotal);
            float balance = sellTotal - buyTotal;
            string fairness = balance >= 0 ? "fair" : "unfair";

            Speech.Say($"Selling {sellTotal:F2}, Buying {buyTotal:F2}, Balance {balance:F2}, {fairness}");
        }

        /// <summary>
        /// Calculate sell and buy totals for the current trade.
        /// </summary>
        private void CalculateTradeTotals(out float sellTotal, out float buyTotal)
        {
            sellTotal = 0f;
            foreach (var g in _sellGoods)
            {
                if (g.OfferedAmount > 0)
                    sellTotal += TradeReflection.GetGoodSellValue(g.Name, g.OfferedAmount);
            }

            buyTotal = 0f;
            foreach (var g in _buyGoods)
            {
                if (g.OfferedAmount > 0)
                    buyTotal += TradeReflection.GetGoodBuyValue(g.Name, g.OfferedAmount);
            }
        }

        private void TryAcceptTrade()
        {
            // Check if buying anything
            bool buyingAnything = false;
            foreach (var g in _buyGoods)
            {
                if (g.OfferedAmount > 0)
                {
                    buyingAnything = true;
                    break;
                }
            }

            if (!buyingAnything)
            {
                Speech.Say("Not buying anything");
                SoundManager.PlayFailed();
                return;
            }

            // Calculate balance
            float sellTotal = 0f;
            var sellList = new List<string>();
            foreach (var g in _sellGoods)
            {
                if (g.OfferedAmount > 0)
                {
                    sellTotal += TradeReflection.GetGoodSellValue(g.Name, g.OfferedAmount);
                    sellList.Add($"{g.OfferedAmount} {g.DisplayName}");
                }
            }

            float buyTotal = 0f;
            var buyList = new List<string>();
            foreach (var g in _buyGoods)
            {
                if (g.OfferedAmount > 0)
                {
                    buyTotal += TradeReflection.GetGoodBuyValue(g.Name, g.OfferedAmount);
                    buyList.Add($"{g.OfferedAmount} {g.DisplayName}");
                }
            }

            float balance = sellTotal - buyTotal;

            if (balance < 0)
            {
                Speech.Say($"Trade unfair, need {Mathf.Abs(balance):F2} more");
                SoundManager.PlayFailed();
                return;
            }

            // Build confirmation message
            string sellText = sellList.Count > 0 ? string.Join(", ", sellList) : "nothing";
            string buyText = buyList.Count > 0 ? string.Join(", ", buyList) : "nothing";

            _inConfirmation = true;
            _level = Level.TradeConfirm;
            Speech.Say($"Selling: {sellText}. Buying: {buyText}. Balance: {balance:F2}. Enter to confirm, Escape to cancel");
        }

        private void HandleGoodsSearch(char c)
        {
            var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
            if (currentList.Count == 0) return;

            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < currentList.Count; i++)
            {
                if (currentList[i].DisplayName.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    Speech.Say(BuildGoodLabel(currentList[i]));
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }

        // ========================================
        // PERKS LEVEL
        // ========================================

        private void EnterPerks()
        {
            _level = Level.Perks;
            _currentIndex = 0;
            _search.Clear();

            SoundManager.PlayButtonClick();

            if (_perks.Count > 0)
            {
                Speech.Say($"Perks. {BuildPerkLabel(_perks[0])}");
            }
            else
            {
                Speech.Say("Perks. No perks available");
            }
        }

        private string BuildPerkLabel(PerkItem perk)
        {
            // Format: "Name. Description, Price" (similar to CornerstoneOverlay)
            string nameAndDesc = !string.IsNullOrEmpty(perk.Description)
                ? $"{perk.DisplayName}. {perk.Description}"
                : perk.DisplayName;

            if (perk.Sold)
            {
                return $"{nameAndDesc}, sold";
            }

            if (perk.Discounted)
            {
                int discountPercent = Mathf.RoundToInt((1f - perk.DiscountRatio) * 100);
                return $"{nameAndDesc}, {perk.Price:F0} Amber, {discountPercent}% off";
            }

            return $"{nameAndDesc}, {perk.Price:F0} Amber";
        }

        private bool ProcessPerksKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigatePerks(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigatePerks(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    BuyCurrentPerk();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Go back to main menu
                    _level = Level.MainMenu;
                    _currentIndex = 2; // Perks item
                    RefreshMainMenu();
                    Speech.Say($"Main menu. {_mainMenuItems[_currentIndex].Label}");
                    InputBlocker.BlockCancelOnce = true;
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
                        HandlePerksSearch(c);
                        return true;
                    }
                    return true;
            }
        }

        private void NavigatePerks(int direction)
        {
            if (_perks.Count == 0) return;
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _perks.Count);
            Speech.Say(BuildPerkLabel(_perks[_currentIndex]));
        }

        private void BuyCurrentPerk()
        {
            if (_currentIndex < 0 || _currentIndex >= _perks.Count) return;

            var perk = _perks[_currentIndex];
            if (perk.Sold)
            {
                Speech.Say("Already sold");
                SoundManager.PlayFailed();
                return;
            }

            int amber = TradeReflection.GetAmberInStorage();
            if (amber < perk.Price)
            {
                Speech.Say($"Not enough Amber, have {amber}, need {perk.Price:F0}");
                SoundManager.PlayFailed();
                return;
            }

            if (TradeReflection.BuyPerk(perk.EffectState))
            {
                perk.Sold = true;
                SoundManager.PlayTraderTransactionCompleted();
                // Also play trader-specific transaction sound
                var traderSound = TradeReflection.GetTraderTransactionSound();
                if (traderSound != null)
                    SoundManager.PlaySoundEffect(traderSound);
                Speech.Say($"Purchased {perk.DisplayName}");
                RefreshPerks();  // Refresh perks list in case user re-enters submenu
                RefreshMainMenu();
            }
            else
            {
                Speech.Say("Purchase failed");
                SoundManager.PlayFailed();
            }
        }

        private void HandlePerksSearch(char c)
        {
            if (_perks.Count == 0) return;

            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _perks.Count; i++)
            {
                if (_perks[i].DisplayName.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    Speech.Say(BuildPerkLabel(_perks[i]));
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }

        // ========================================
        // ASSAULT CONFIRM LEVEL
        // ========================================

        private void EnterAssaultConfirm()
        {
            _level = Level.AssaultConfirm;
            _inConfirmation = true;
            SoundManager.PlayButtonClick();
            Speech.Say("Assault trader? May lose villagers and reputation. Enter to confirm, Escape to cancel");
        }

        private bool ProcessAssaultConfirmKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteAssault();
                    return true;

                case KeyCode.Escape:
                    _level = Level.MainMenu;
                    _inConfirmation = false;
                    // Find Assault item dynamically (it's conditionally added)
                    _currentIndex = Math.Min(_mainMenuItems.Count - 1, 3);
                    Speech.Say($"Cancelled. Main menu. {_mainMenuItems[_currentIndex].Label}");
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                default:
                    return true;
            }
        }

        private void ExecuteAssault()
        {
            var result = TradeReflection.AssaultTrader();
            _inConfirmation = false;

            if (result.Success)
            {
                SoundManager.PlayButtonClick();
                Speech.Say($"Assault successful. Stole {result.GoodsStolen} goods, {result.PerksStolen} perks. Lost {result.VillagersLost} villagers");
                // Panel will close
            }
            else
            {
                Speech.Say("Assault failed");
                SoundManager.PlayFailed();
                _level = Level.MainMenu;
                _currentIndex = 0;
            }
        }

        // ========================================
        // TRADE CONFIRM
        // ========================================

        private bool ProcessConfirmationKey(KeyCode keyCode)
        {
            if (_level == Level.AssaultConfirm)
            {
                return ProcessAssaultConfirmKey(keyCode);
            }

            // Trade confirmation
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteTrade();
                    return true;

                case KeyCode.Escape:
                    _inConfirmation = false;
                    _level = Level.GoodsTrade;
                    Speech.Say("Cancelled");
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                default:
                    return true;
            }
        }

        private void ExecuteTrade()
        {
            _inConfirmation = false;

            // Build lists of goods to sell and buy
            var sellList = new List<KeyValuePair<string, int>>();
            foreach (var g in _sellGoods)
            {
                if (g.OfferedAmount > 0)
                    sellList.Add(new KeyValuePair<string, int>(g.Name, g.OfferedAmount));
            }

            var buyList = new List<KeyValuePair<string, int>>();
            foreach (var g in _buyGoods)
            {
                if (g.OfferedAmount > 0)
                    buyList.Add(new KeyValuePair<string, int>(g.Name, g.OfferedAmount));
            }

            // Execute the trade via reflection
            if (TradeReflection.ExecuteTrade(sellList, buyList))
            {
                SoundManager.PlayTraderTransactionCompleted();
                // Also play trader-specific transaction sound
                var traderSound = TradeReflection.GetTraderTransactionSound();
                if (traderSound != null)
                    SoundManager.PlaySoundEffect(traderSound);
                Speech.Say("Trade complete");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Trade failed");
            }

            // Reset offered amounts
            foreach (var g in _sellGoods)
                g.OfferedAmount = 0;
            foreach (var g in _buyGoods)
                g.OfferedAmount = 0;

            _level = Level.GoodsTrade;
            _currentIndex = 0;

            // Refresh goods data since inventory changed
            RefreshSellGoods();
            RefreshBuyGoods();

            if (_currentTab == Tab.Sell)
                AnnounceSellTab();
            else
                AnnounceBuyTab();
        }

        // ========================================
        // SEARCH HELPERS
        // ========================================

        private void HandleSearch<T>(char c, List<T> items, Func<T, string> nameSelector)
        {
            if (items.Count == 0) return;

            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < items.Count; i++)
            {
                string name = nameSelector(items[i]);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    // Announce via the level-specific method
                    if (items is List<NavItem> navItems)
                        Speech.Say(navItems[i].Label);
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
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
