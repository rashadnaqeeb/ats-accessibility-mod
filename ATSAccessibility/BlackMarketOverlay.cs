using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the BlackMarketPopup.
    /// Flat list navigation with NPC flavor text header and offer buttons.
    /// </summary>
    public class BlackMarketOverlay : IKeyHandler
    {
        private enum ItemType { Header, Reroll, Offer }

        private class NavItem
        {
            public ItemType Type;
            public BlackMarketReflection.OfferInfo? Offer;  // For Offer type
            public string Label;
            public string SearchName;  // Name for type-ahead (offers only)
        }

        // State
        private bool _isOpen;
        private object _blackMarket;
        private int _currentIndex;
        private List<NavItem> _items = new List<NavItem>();
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Sub-menu state (for offer buy/credit selection)
        private bool _inSubMenu;
        private int _subMenuIndex;  // 0=Buy, 1=Credit

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Sub-menu navigation
            if (_inSubMenu)
            {
                return ProcessSubMenuKey(keyCode);
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

                case KeyCode.RightArrow:
                    // Enter sub-menu for offers only
                    TryEnterSubMenu();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrent();
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
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private bool ProcessSubMenuKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    _subMenuIndex = 0;
                    AnnounceSubMenuItem();
                    return true;

                case KeyCode.DownArrow:
                    _subMenuIndex = 1;
                    AnnounceSubMenuItem();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExecuteSubMenuAction();
                    return true;

                case KeyCode.Escape:
                    // Exit sub-menu, return to main list
                    _inSubMenu = false;
                    AnnounceCurrentItem();
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.LeftArrow:
                    // Exit sub-menu, return to main list
                    _inSubMenu = false;
                    AnnounceCurrentItem();
                    return true;

                default:
                    // Consume all other keys
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when BlackMarketPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _blackMarket = BlackMarketReflection.GetBlackMarket(popup);
            if (_blackMarket == null)
            {
                Debug.LogWarning("[ATSAccessibility] BlackMarketOverlay: Could not get BlackMarket from popup");
                return;
            }

            _isOpen = true;
            _currentIndex = 0;
            _inSubMenu = false;
            _subMenuIndex = 0;
            _search.Clear();

            RefreshData();

            // Announce panel with header
            if (_items.Count > 0)
            {
                Speech.Say($"Black Market. {_items[0].Label}");
            }
            else
            {
                Speech.Say("Black Market. No offers available");
            }

            Debug.Log($"[ATSAccessibility] BlackMarketOverlay opened, {_items.Count} items");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _blackMarket = null;
            _items.Clear();
            _inSubMenu = false;
            _search.Clear();

            Debug.Log("[ATSAccessibility] BlackMarketOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // Add header item (NPC flavor text)
            _items.Add(new NavItem
            {
                Type = ItemType.Header,
                Label = BlackMarketReflection.GetFlavorText()
            });

            // Add reroll button
            _items.Add(new NavItem
            {
                Type = ItemType.Reroll,
                Label = BuildRerollLabel()
            });

            // Get offers
            var offers = BlackMarketReflection.GetOffers(_blackMarket);
            foreach (var offer in offers)
            {
                if (!offer.Bought)
                {
                    _items.Add(new NavItem
                    {
                        Type = ItemType.Offer,
                        Offer = offer,
                        Label = BuildOfferLabel(offer),
                        SearchName = offer.GoodName
                    });
                }
            }

            Debug.Log($"[ATSAccessibility] BlackMarketOverlay refreshed: {_items.Count} items");
        }

        private string BuildRerollLabel()
        {
            int price = BlackMarketReflection.GetRerollPrice(_blackMarket);

            if (BlackMarketReflection.IsRerollOnCooldown(_blackMarket))
            {
                float timeLeft = BlackMarketReflection.GetRerollTimeLeft(_blackMarket);
                string timeStr = BlackMarketReflection.FormatTime(timeLeft);
                return $"Reroll, {timeStr} remaining";
            }

            if (!BlackMarketReflection.CanAffordReroll(_blackMarket))
            {
                return $"Reroll, {price} Amber, cannot afford";
            }

            return $"Reroll, {price} Amber";
        }

        private string BuildOfferLabel(BlackMarketReflection.OfferInfo offer)
        {
            var parts = new List<string>();

            // Good name and amount
            parts.Add($"{offer.GoodName}, {offer.GoodAmount}");

            // Buy price with rating
            parts.Add($"Buy {offer.BuyPrice} Amber {offer.BuyRating}");

            // Credit price with rating
            parts.Add($"Credit {offer.CreditPrice} Amber {offer.CreditRating}");

            // Time left
            string timeStr = BlackMarketReflection.FormatTime(offer.TimeLeft);
            parts.Add($"{timeStr} remaining");

            return string.Join(", ", parts);
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
            Speech.Say(_items[_currentIndex].Label);
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void TryEnterSubMenu()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type == ItemType.Offer && item.Offer.HasValue)
            {
                _inSubMenu = true;
                _subMenuIndex = 0;
                AnnounceSubMenuItem();
            }
        }

        private void ActivateCurrent()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            switch (item.Type)
            {
                case ItemType.Header:
                    // Just re-announce
                    AnnounceCurrentItem();
                    break;

                case ItemType.Reroll:
                    ExecuteReroll();
                    break;

                case ItemType.Offer:
                    // Enter sub-menu
                    if (item.Offer.HasValue)
                    {
                        _inSubMenu = true;
                        _subMenuIndex = 0;
                        AnnounceSubMenuItem();
                    }
                    break;
            }
        }

        private void ExecuteReroll()
        {
            if (BlackMarketReflection.IsRerollOnCooldown(_blackMarket))
            {
                float timeLeft = BlackMarketReflection.GetRerollTimeLeft(_blackMarket);
                string timeStr = BlackMarketReflection.FormatTime(timeLeft);
                Speech.Say($"On cooldown, {timeStr} remaining");
                SoundManager.PlayFailed();
                return;
            }

            if (!BlackMarketReflection.CanAffordReroll(_blackMarket))
            {
                Speech.Say("Cannot afford");
                SoundManager.PlayFailed();
                return;
            }

            if (BlackMarketReflection.Reroll(_blackMarket))
            {
                SoundManager.PlayReroll();
                RefreshData();

                // Announce "Rerolled" then first offer
                if (_items.Count > 2)  // Header + Reroll + at least one offer
                {
                    _currentIndex = 2;  // First offer
                    Speech.Say($"Rerolled. {_items[2].Label}");
                }
                else
                {
                    Speech.Say("Rerolled");
                }
            }
            else
            {
                Speech.Say("Reroll failed");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // SUB-MENU
        // ========================================

        private void ExitSubMenuAfterPurchase()
        {
            _inSubMenu = false;
            RefreshData();
            if (_currentIndex >= _items.Count)
                _currentIndex = _items.Count > 0 ? _items.Count - 1 : 0;
            if (_items.Count > 0)
                AnnounceCurrentItem();
        }

        private void AnnounceSubMenuItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type != ItemType.Offer || !item.Offer.HasValue) return;

            var offer = item.Offer.Value;

            if (_subMenuIndex == 0)
            {
                // Buy now
                bool canAfford = BlackMarketReflection.CanAffordBuy(offer.State);
                string affordStr = canAfford ? "" : ", cannot afford";
                Speech.Say($"Buy now, {offer.BuyPrice} Amber, {offer.BuyRating}{affordStr}");
            }
            else
            {
                // Buy on credit
                string paymentTerms = !string.IsNullOrEmpty(offer.PaymentTerms)
                    ? $", payment due {offer.PaymentTerms}"
                    : "";
                Speech.Say($"Buy on credit, {offer.CreditPrice} Amber, {offer.CreditRating}{paymentTerms}");
            }
        }

        private void ExecuteSubMenuAction()
        {
            if (_blackMarket == null) return;
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            if (item.Type != ItemType.Offer || !item.Offer.HasValue) return;

            var offer = item.Offer.Value;

            if (offer.State == null)
            {
                Speech.Say("Invalid offer");
                SoundManager.PlayFailed();
                return;
            }

            if (_subMenuIndex == 0)
            {
                // Buy now
                if (!BlackMarketReflection.CanAffordBuy(offer.State))
                {
                    Speech.Say("Cannot afford");
                    SoundManager.PlayFailed();
                    return;
                }

                if (BlackMarketReflection.Buy(_blackMarket, offer.State))
                {
                    SoundManager.PlayTraderTransactionCompleted();
                    Speech.Say($"Purchased {offer.GoodName}");
                    ExitSubMenuAfterPurchase();
                }
                else
                {
                    Speech.Say("Purchase failed");
                    SoundManager.PlayFailed();
                }
            }
            else
            {
                // Buy on credit
                if (BlackMarketReflection.BuyOnCredit(_blackMarket, offer.State))
                {
                    SoundManager.PlayTraderTransactionCompleted();
                    Speech.Say($"Purchased {offer.GoodName} on credit");
                    ExitSubMenuAfterPurchase();
                }
                else
                {
                    Speech.Say("Purchase failed");
                    SoundManager.PlayFailed();
                }
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindOfferMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentItem();
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

            int matchIndex = FindOfferMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindOfferMatch()
        {
            if (!_search.HasBuffer || _items.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Type != ItemType.Offer) continue;
                if (string.IsNullOrEmpty(_items[i].SearchName)) continue;

                if (_items[i].SearchName.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
