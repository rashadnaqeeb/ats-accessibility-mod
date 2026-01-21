using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard input for settlement map navigation.
    /// This is the fallback handler when no popups/menus are open during gameplay.
    /// </summary>
    public class SettlementKeyHandler : IKeyHandler
    {
        private readonly MapNavigator _mapNavigator;
        private readonly MapScanner _mapScanner;
        private readonly InfoPanelMenu _infoPanelMenu;
        private readonly MenuHub _menuHub;
        private readonly BuildingMenuPanel _buildingMenuPanel;
        private readonly MoveModeController _moveModeController;
        private readonly AnnouncementHistoryPanel _announcementHistoryPanel;

        public SettlementKeyHandler(
            MapNavigator mapNavigator,
            MapScanner mapScanner,
            InfoPanelMenu infoPanelMenu,
            MenuHub menuHub,
            BuildingMenuPanel buildingMenuPanel,
            MoveModeController moveModeController,
            AnnouncementHistoryPanel announcementHistoryPanel)
        {
            _mapNavigator = mapNavigator;
            _mapScanner = mapScanner;
            _infoPanelMenu = infoPanelMenu;
            _menuHub = menuHub;
            _buildingMenuPanel = buildingMenuPanel;
            _moveModeController = moveModeController;
            _announcementHistoryPanel = announcementHistoryPanel;
        }

        /// <summary>
        /// Active when the settlement game is running.
        /// </summary>
        public bool IsActive => GameReflection.GetIsGameActive();

        /// <summary>
        /// Process settlement map key events.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!IsActive) return false;

            switch (keyCode)
            {
                // Arrow key navigation
                case KeyCode.UpArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(0, 1);
                    else
                        _mapNavigator.MoveCursor(0, 1);
                    return true;
                case KeyCode.DownArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(0, -1);
                    else
                        _mapNavigator.MoveCursor(0, -1);
                    return true;
                case KeyCode.LeftArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(-1, 0);
                    else
                        _mapNavigator.MoveCursor(-1, 0);
                    return true;
                case KeyCode.RightArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(1, 0);
                    else
                        _mapNavigator.MoveCursor(1, 0);
                    return true;

                // Position announcement
                case KeyCode.K:
                    _mapNavigator.AnnounceCurrentPosition();
                    return true;

                // Game speed controls
                case KeyCode.Space:
                    GameReflection.TogglePause();
                    return true;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    GameReflection.SetSpeed(1);
                    return true;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    GameReflection.SetSpeed(2);
                    return true;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    GameReflection.SetSpeed(3);
                    return true;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    GameReflection.SetSpeed(4);
                    return true;

                // Stats hotkeys
                case KeyCode.S:
                    StatsReader.AnnounceQuickSummary();
                    return true;
                case KeyCode.V:
                    StatsReader.AnnounceNextSpeciesResolve();
                    return true;
                case KeyCode.T:
                    StatsReader.AnnounceTimeSummary();
                    return true;

                // Map Scanner controls
                case KeyCode.PageUp:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(-1);
                    else if (modifiers.Shift)
                        _mapScanner?.ChangeSubcategory(-1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(-1);
                    else
                        _mapScanner?.ChangeGroup(-1);
                    return true;
                case KeyCode.PageDown:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(1);
                    else if (modifiers.Shift)
                        _mapScanner?.ChangeSubcategory(1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(1);
                    else
                        _mapScanner?.ChangeGroup(1);
                    return true;
                case KeyCode.Home:
                    _mapScanner?.MoveCursorToItem();
                    return true;
                case KeyCode.End:
                    _mapScanner?.AnnounceDistance();
                    return true;

                // Tile info
                case KeyCode.I:
                    TileInfoReader.ReadCurrentTile(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    return true;
                case KeyCode.E:
                    _mapNavigator.AnnounceEntrance();
                    return true;
                case KeyCode.R:
                    _mapNavigator.RotateBuilding();
                    return true;

                // Building activation (opens building panel)
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _mapNavigator.ActivateBuilding();
                    return true;

                // Panel/menu openers
                case KeyCode.F1:
                    _infoPanelMenu?.Open();
                    return true;
                case KeyCode.F2:
                    _menuHub?.Open();
                    return true;
                case KeyCode.Tab:
                    _buildingMenuPanel?.Open();
                    return true;
                case KeyCode.H:
                    if (modifiers.Alt)
                    {
                        _announcementHistoryPanel?.Open();
                        return true;
                    }
                    return false;

                // Move building mode
                case KeyCode.M:
                    var building = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    if (building != null)
                        _moveModeController?.EnterMoveMode(building);
                    else
                        Speech.Say("No building here");
                    return true;

                default:
                    // Consume all keys - mod has full keyboard control in settlement
                    return true;
            }
        }
    }
}
