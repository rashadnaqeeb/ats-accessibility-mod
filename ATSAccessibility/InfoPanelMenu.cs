using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Unified menu for accessing information panels (Stats, Resources, Mysteries, Villagers, Announcements).
    /// Opened with F1 from the settlement map.
    /// </summary>
    public class InfoPanelMenu : IKeyHandler
    {
        private enum MenuPanel
        {
            Resources,
            Villagers,
            Stats,
            Mysteries,
            Announcements
        }

        private static readonly string[] _menuLabels = { "Resources", "Villagers", "Stats", "Mysteries", "Announcements" };

        private readonly StatsPanel _statsPanel;
        private readonly SettlementResourcePanel _resourcePanel;
        private readonly MysteriesPanel _mysteriesPanel;
        private readonly VillagersPanel _villagersPanel;
        private readonly AnnouncementsSettingsPanel _announcementsPanel;

        private bool _isOpen;
        private int _currentIndex;
        private MenuPanel? _activeChildPanel;

        /// <summary>
        /// Whether the info panel menu is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Whether a child panel (Stats, Resources, or Mysteries) is currently open.
        /// </summary>
        public bool IsInChildPanel => _activeChildPanel.HasValue;

        public InfoPanelMenu(StatsPanel statsPanel, SettlementResourcePanel resourcePanel, MysteriesPanel mysteriesPanel, VillagersPanel villagersPanel, AnnouncementsSettingsPanel announcementsPanel)
        {
            _statsPanel = statsPanel;
            _resourcePanel = resourcePanel;
            _mysteriesPanel = mysteriesPanel;
            _villagersPanel = villagersPanel;
            _announcementsPanel = announcementsPanel;
        }

        /// <summary>
        /// Open the info panel menu. If already open, closes it.
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
            _activeChildPanel = null;

            AnnounceCurrentItem(includePrefix: true);
            Debug.Log("[ATSAccessibility] Info panel menu opened");
        }

        /// <summary>
        /// Close the info panel menu and any active child panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            // Close any active child panel
            CloseActiveChildPanel();

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Information panels closed");
            Debug.Log("[ATSAccessibility] Info panel menu closed");
        }

        /// <summary>
        /// Process a key event for the info panel menu (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // If a child panel is open, handle navigation
            if (_activeChildPanel.HasValue)
            {
                switch (keyCode)
                {
                    case KeyCode.LeftArrow:
                        // Try to let child panel handle Left first (for internal navigation)
                        if (ProcessChildPanelKey(keyCode))
                        {
                            return true;  // Child handled it (was in nested view)
                        }
                        // Child returned false (at root level), return to menu
                        CloseActiveChildPanel();
                        AnnounceCurrentItem(includePrefix: false);
                        return true;

                    case KeyCode.Escape:
                        // Close everything
                        Close();
                        return true;

                    default:
                        // Delegate to child panel
                        return ProcessChildPanelKey(keyCode);
                }
            }

            // Menu navigation (no child panel open)
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
                case KeyCode.RightArrow:
                    OpenSelectedPanel();
                    return true;

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return true; // Consume other keys while menu is open
            }
        }

        private void Navigate(int direction)
        {
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _menuLabels.Length);
            AnnounceCurrentItem(includePrefix: false);
        }

        private void OpenSelectedPanel()
        {
            var panel = (MenuPanel)_currentIndex;

            switch (panel)
            {
                case MenuPanel.Stats:
                    _statsPanel?.Open();
                    break;
                case MenuPanel.Resources:
                    _resourcePanel?.Open();
                    break;
                case MenuPanel.Mysteries:
                    _mysteriesPanel?.Open();
                    break;
                case MenuPanel.Villagers:
                    _villagersPanel?.Open();
                    break;
                case MenuPanel.Announcements:
                    _announcementsPanel?.Open();
                    break;
            }

            _activeChildPanel = panel;
            Debug.Log($"[ATSAccessibility] Opened {panel} panel from info menu");
        }

        private void CloseActiveChildPanel()
        {
            if (!_activeChildPanel.HasValue) return;

            switch (_activeChildPanel.Value)
            {
                case MenuPanel.Stats:
                    if (_statsPanel?.IsOpen == true)
                        _statsPanel.Close();
                    break;
                case MenuPanel.Resources:
                    if (_resourcePanel?.IsOpen == true)
                        _resourcePanel.Close();
                    break;
                case MenuPanel.Mysteries:
                    if (_mysteriesPanel?.IsOpen == true)
                        _mysteriesPanel.Close();
                    break;
                case MenuPanel.Villagers:
                    if (_villagersPanel?.IsOpen == true)
                        _villagersPanel.Close();
                    break;
                case MenuPanel.Announcements:
                    if (_announcementsPanel?.IsOpen == true)
                        _announcementsPanel.Close();
                    break;
            }

            _activeChildPanel = null;
        }

        private bool ProcessChildPanelKey(KeyCode keyCode)
        {
            if (!_activeChildPanel.HasValue) return false;

            switch (_activeChildPanel.Value)
            {
                case MenuPanel.Stats:
                    return _statsPanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Resources:
                    return _resourcePanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Mysteries:
                    return _mysteriesPanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Villagers:
                    return _villagersPanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Announcements:
                    return _announcementsPanel?.ProcessKeyEvent(keyCode) ?? false;
            }

            return false;
        }

        private void AnnounceCurrentItem(bool includePrefix)
        {
            string label = _menuLabels[_currentIndex];

            string message = includePrefix ? $"Information panels. {label}" : label;
            Speech.Say(message);
        }
    }
}
