using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Menu Hub for quick access to game popups.
    /// Opened with F2 from the settlement map.
    /// Isolated in a single file for easy removal if needed.
    /// </summary>
    public class MenuHub
    {
        private static readonly string[] _menuLabels = {
            "Recipes",
            "Orders",
            "Trade Routes",
            "Consumption Control",
            "Trends",
            "Villagers",
            "Trader"
        };

        private bool _isOpen;
        private int _currentIndex;

        /// <summary>
        /// Whether the menu hub is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the menu hub. If already open, closes it.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            _isOpen = true;
            _currentIndex = 0;

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
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Closed");
            Debug.Log("[ATSAccessibility] Menu Hub closed");
        }

        /// <summary>
        /// Process a key event for the menu hub.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenSelectedMenu();
                    return true;

                case KeyCode.Escape:
                    Close();
                    return true;

                case KeyCode.F2:
                    Close();
                    return true;

                default:
                    return true; // Consume other keys while menu is open
            }
        }

        private void Navigate(int direction)
        {
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _menuLabels.Length);
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
                    break;
                case 1: // Orders
                    success = GameReflection.OpenOrdersPopup();
                    break;
                case 2: // Trade Routes
                    success = GameReflection.OpenTradeRoutesPopup();
                    break;
                case 3: // Consumption Control
                    success = GameReflection.OpenConsumptionPopup();
                    break;
                case 4: // Trends
                    success = GameReflection.OpenTrendsPopup();
                    break;
                case 5: // Villagers
                    success = GameReflection.OpenVillagersPopup();
                    break;
                case 6: // Trader
                    success = GameReflection.OpenTraderPanel();
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
            string message = withPrefix ? $"Menu Hub. {label}" : label;
            Speech.Say(message);
        }
    }
}
