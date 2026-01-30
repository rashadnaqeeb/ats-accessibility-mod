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
            Workers,
            Stats,
            Modifiers,
            Announcements
        }

        private static readonly string[] _menuLabels = { "Resources", "Villagers", "Workers", "Stats", "Modifiers", "Announcements" };

        private readonly StatsPanel _statsPanel;
        private readonly SettlementResourcePanel _resourcePanel;
        private readonly MysteriesPanel _mysteriesPanel;
        private readonly VillagersPanel _villagersPanel;
        private readonly WorkersPanel _workersPanel;
        private readonly AnnouncementsSettingsPanel _announcementsPanel;

        private bool _isOpen;
        private int _currentIndex;
        private MenuPanel? _activeChildPanel;

        // Type-ahead search for root menu
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

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

        public InfoPanelMenu(StatsPanel statsPanel, SettlementResourcePanel resourcePanel, MysteriesPanel mysteriesPanel, VillagersPanel villagersPanel, WorkersPanel workersPanel, AnnouncementsSettingsPanel announcementsPanel)
        {
            _statsPanel = statsPanel;
            _resourcePanel = resourcePanel;
            _mysteriesPanel = mysteriesPanel;
            _villagersPanel = villagersPanel;
            _workersPanel = workersPanel;
            _announcementsPanel = announcementsPanel;
        }

        /// <summary>
        /// Open the info panel menu. If already open, closes it.
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
            _activeChildPanel = null;
            _search.Clear();

            SoundManager.PlayPopupShow();
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
            _search.Clear();
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Closed");
            Debug.Log("[ATSAccessibility] Info panel menu closed");
        }

        /// <summary>
        /// Process a key event for the info panel menu (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

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
                        // Let child panel handle Escape first (e.g., to clear search)
                        if (ProcessChildPanelKey(keyCode))
                        {
                            return true;  // Child handled it (cleared search)
                        }
                        // Close entire overlay
                        SoundManager.PlayButtonClick();
                        Close();
                        return true;

                    default:
                        // Alt+I: announce resource description when in resource panel
                        if (modifiers.Alt && keyCode == KeyCode.I
                            && _activeChildPanel == MenuPanel.Resources)
                        {
                            _resourcePanel?.AnnounceCurrentItemDescription();
                            return true;
                        }
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

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(_menuLabels.Length - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    OpenSelectedPanel();
                    return true;

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.F1:
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                    }
                    return true; // Consume all keys while menu is open
            }
        }

        private void Navigate(int direction)
        {
            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _menuLabels.Length);
            AnnounceCurrentItem(includePrefix: false);
        }

        private void NavigateTo(int index)
        {
            if (_menuLabels.Length == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _menuLabels.Length - 1);
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
                case MenuPanel.Modifiers:
                    _mysteriesPanel?.Open();
                    break;
                case MenuPanel.Villagers:
                    _villagersPanel?.Open();
                    break;
                case MenuPanel.Workers:
                    _workersPanel?.Open();
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
                case MenuPanel.Modifiers:
                    if (_mysteriesPanel?.IsOpen == true)
                        _mysteriesPanel.Close();
                    break;
                case MenuPanel.Villagers:
                    if (_villagersPanel?.IsOpen == true)
                        _villagersPanel.Close();
                    break;
                case MenuPanel.Workers:
                    if (_workersPanel?.IsOpen == true)
                        _workersPanel.Close();
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
                case MenuPanel.Modifiers:
                    return _mysteriesPanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Villagers:
                    return _villagersPanel?.ProcessKeyEvent(keyCode) ?? false;
                case MenuPanel.Workers:
                    return _workersPanel?.ProcessKeyEvent(keyCode) ?? false;
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

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < _menuLabels.Length; i++)
            {
                if (_menuLabels[i].ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem(includePrefix: false);
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
                return;
            }

            // Re-search with shortened buffer
            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < _menuLabels.Length; i++)
            {
                if (_menuLabels[i].ToLowerInvariant().StartsWith(prefix))
                {
                    _currentIndex = i;
                    AnnounceCurrentItem(includePrefix: false);
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
        }
    }
}
