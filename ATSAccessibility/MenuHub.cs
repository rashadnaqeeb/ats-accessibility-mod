using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Menu Hub for quick access to game popups.
    /// Opened with F2 from the settlement map.
    /// Isolated in a single file for easy removal if needed.
    /// </summary>
    public class MenuHub : IKeyHandler
    {
        private static readonly string[] _menuLabels = {
            "Recipes",
            "Orders",
            "Trade Routes",
            "Payments",
            "Consumption Control",
            "Trends",
            "Trader"
        };

        private bool _isOpen;
        private int _currentIndex;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether the menu hub is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Open the menu hub. If already open, closes it.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                SoundManager.PlayButtonClick();
                Close();
                return;
            }

            _isOpen = true;
            _currentIndex = 0;
            _search.Clear();

            SoundManager.PlayPopupShow();
            AnnounceCurrentItem(withPrefix: true);
            Debug.Log("[ATSAccessibility] Menu Hub opened");
        }

        /// <summary>
        /// Close the menu hub.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _search.Clear();
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Closed");
            Debug.Log("[ATSAccessibility] Menu Hub closed");
        }

        /// <summary>
        /// Process a key event for the menu hub (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

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
                    NavigateTo(_menuLabels.Length - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenSelectedMenu();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                case KeyCode.F2:
                    SoundManager.PlayButtonClick();
                    Close();
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
                        HandleSearchKey(c);
                        return true;
                    }
                    return true; // Consume other keys while menu is open
            }
        }

        private void Navigate(int direction)
        {
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _menuLabels.Length);
            AnnounceCurrentItem(withPrefix: false);
        }

        private void NavigateTo(int index)
        {
            if (_menuLabels.Length == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _menuLabels.Length - 1);
            AnnounceCurrentItem(withPrefix: false);
        }

        private void OpenSelectedMenu()
        {
            string menuName = _menuLabels[_currentIndex];
            Debug.Log($"[ATSAccessibility] Opening {menuName} from Menu Hub");

            bool success = false;

            switch (_currentIndex)
            {
                case 0: // Recipes
                    success = GameReflection.OpenRecipesPopup();
                    if (success) SoundManager.PlayMenuRecipes();
                    break;
                case 1: // Orders
                    success = GameReflection.OpenOrdersPopup();
                    if (success) SoundManager.PlayMenuOrders();
                    break;
                case 2: // Trade Routes
                    if (!GameReflection.AreTradeRoutesUnlocked())
                    {
                        Speech.Say("Trade Routes locked. Unlock via meta progression");
                        SoundManager.PlayFailed();
                        return;
                    }
                    success = GameReflection.OpenTradeRoutesPopup();
                    if (success) SoundManager.PlayMenuTradeRoutes();
                    break;
                case 3: // Payments
                    success = GameReflection.OpenPaymentsPopup();
                    if (success) SoundManager.PlayMenuRecipes();  // Shares sound with Recipes
                    break;
                case 4: // Consumption Control
                    if (!GameReflection.IsConsumptionControlUnlocked())
                    {
                        Speech.Say("Consumption Control locked. Unlock via meta progression");
                        SoundManager.PlayFailed();
                        return;
                    }
                    success = GameReflection.OpenConsumptionPopup();
                    if (success) SoundManager.PlayConsumptionPopupShow();
                    break;
                case 5: // Trends
                    success = GameReflection.OpenTrendsPopup();
                    if (success) SoundManager.PlayMenuTrends();
                    break;
                case 6: // Trader
                    success = GameReflection.OpenTraderPanel();
                    if (!success)
                    {
                        // Give specific feedback for trader panel
                        Speech.Say("Trader unavailable. Build a Trading Post first");
                        SoundManager.PlayFailed();
                        Debug.Log("[ATSAccessibility] Trader panel unavailable - no Trading Post");
                        return;
                    }
                    break;
            }

            if (success)
            {
                // Close menu hub after opening popup
                _isOpen = false;
                Debug.Log($"[ATSAccessibility] Successfully opened {menuName}");
            }
            else
            {
                Speech.Say($"{menuName} unavailable");
                Debug.Log($"[ATSAccessibility] Failed to open {menuName}");
            }
        }

        private void AnnounceCurrentItem(bool withPrefix)
        {
            string label = _menuLabels[_currentIndex];

            // Check if item is locked
            string lockSuffix = "";
            if (_currentIndex == 2 && !GameReflection.AreTradeRoutesUnlocked())
                lockSuffix = ", locked";
            else if (_currentIndex == 4 && !GameReflection.IsConsumptionControlUnlocked())
                lockSuffix = ", locked";

            string message = withPrefix ? $"Menu Hub. {label}{lockSuffix}" : $"{label}{lockSuffix}";
            Speech.Say(message);
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentItem(withPrefix: false);
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
                _currentIndex = matchIndex;
                AnnounceCurrentItem(withPrefix: false);
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

            for (int i = 0; i < _menuLabels.Length; i++)
            {
                if (_menuLabels[i].ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
