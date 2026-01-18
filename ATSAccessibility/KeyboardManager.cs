using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Centralized keyboard input handling with context-based navigation.
    /// All hotkeys are processed here for future extensibility.
    /// </summary>
    public class KeyboardManager
    {
        /// <summary>
        /// Key modifiers state (Ctrl, Alt, Shift).
        /// </summary>
        public struct KeyModifiers
        {
            public bool Control { get; }
            public bool Alt { get; }
            public bool Shift { get; }

            public KeyModifiers(bool control, bool alt, bool shift)
            {
                Control = control;
                Alt = alt;
                Shift = shift;
            }
        }

        /// <summary>
        /// Navigation context determines which keys do what.
        /// </summary>
        public enum NavigationContext
        {
            None,           // No special handling
            Popup,          // Navigating a popup/menu
            Map,            // Navigating settlement map
            WorldMap,       // Navigating world map hex grid
            Dialogue,       // Future: reading dialogue
            Encyclopedia,   // Navigating wiki/encyclopedia popup
            Embark          // Navigating embark screen (pre-expedition setup)
        }

        // Current navigation context
        public NavigationContext CurrentContext { get; private set; } = NavigationContext.None;

        // Reference to UI navigator for popup input handling
        private readonly UINavigator _uiNavigator;

        // Reference to tutorial handler for tutorial re-reading
        private TutorialHandler _tutorialHandler;

        // Reference to map navigator for settlement navigation
        private MapNavigator _mapNavigator;

        // Reference to encyclopedia navigator for wiki popup
        private EncyclopediaNavigator _encyclopediaNavigator;

        // Reference to map scanner for quick object finding
        private MapScanner _mapScanner;

        // Reference to stats panel for game statistics
        private StatsPanel _statsPanel;

        // Reference to world map navigator for hex grid navigation
        private WorldMapNavigator _worldMapNavigator;

        // Reference to world map scanner for quick feature finding
        private WorldMapScanner _worldMapScanner;

        // Reference to embark panel for pre-expedition setup
        private EmbarkPanel _embarkPanel;

        public KeyboardManager(UINavigator uiNavigator)
        {
            _uiNavigator = uiNavigator;
        }

        /// <summary>
        /// Set the tutorial handler reference.
        /// </summary>
        public void SetTutorialHandler(TutorialHandler handler)
        {
            _tutorialHandler = handler;
        }

        /// <summary>
        /// Set the map navigator reference.
        /// </summary>
        public void SetMapNavigator(MapNavigator navigator)
        {
            _mapNavigator = navigator;
        }

        /// <summary>
        /// Set the encyclopedia navigator reference.
        /// </summary>
        public void SetEncyclopediaNavigator(EncyclopediaNavigator navigator)
        {
            _encyclopediaNavigator = navigator;
        }

        /// <summary>
        /// Set the map scanner reference.
        /// </summary>
        public void SetMapScanner(MapScanner scanner)
        {
            _mapScanner = scanner;
        }

        /// <summary>
        /// Set the stats panel reference.
        /// </summary>
        public void SetStatsPanel(StatsPanel panel)
        {
            _statsPanel = panel;
        }

        /// <summary>
        /// Set the world map navigator reference.
        /// </summary>
        public void SetWorldMapNavigator(WorldMapNavigator navigator)
        {
            _worldMapNavigator = navigator;
        }

        /// <summary>
        /// Set the world map scanner reference.
        /// </summary>
        public void SetWorldMapScanner(WorldMapScanner scanner)
        {
            _worldMapScanner = scanner;
        }

        /// <summary>
        /// Set the embark panel reference.
        /// </summary>
        public void SetEmbarkPanel(EmbarkPanel panel)
        {
            _embarkPanel = panel;
        }

        /// <summary>
        /// Set the current navigation context.
        /// </summary>
        public void SetContext(NavigationContext context)
        {
            if (CurrentContext != context)
            {
                Debug.Log($"[ATSAccessibility] Navigation context changed: {CurrentContext} -> {context}");
                CurrentContext = context;
            }
        }

        /// <summary>
        /// Process a key event from OnGUI.
        /// Called from AccessibilityCore.OnGUI().
        /// </summary>
        public void ProcessKeyEvent(KeyCode keyCode, KeyModifiers modifiers = default)
        {
            // Check if stats panel is open - highest priority
            if (_statsPanel != null && _statsPanel.IsOpen)
            {
                if (_statsPanel.ProcessKeyEvent(keyCode))
                {
                    return; // Key was handled by stats panel
                }
            }

            // Check if embark panel is open - second priority
            if (_embarkPanel != null && _embarkPanel.IsOpen)
            {
                if (_embarkPanel.ProcessKeyEvent(keyCode))
                {
                    return; // Key was handled by embark panel
                }
            }

            // Check if tutorial is active - takes priority over other contexts
            if (_tutorialHandler != null && _tutorialHandler.IsTutorialActive)
            {
                if (ProcessTutorialKeyEvent(keyCode))
                {
                    return; // Key was handled by tutorial
                }
            }

            // ============================================================
            // TEMPORARY DEBUG HOTKEYS - Remove this section when done
            // ============================================================
            if (ProcessTemporaryDebugKeys(keyCode))
            {
                return;
            }
            // ============================================================

            switch (CurrentContext)
            {
                case NavigationContext.Popup:
                    ProcessPopupKeyEvent(keyCode);
                    break;
                case NavigationContext.Map:
                    ProcessMapKeyEvent(keyCode, modifiers);
                    break;
                case NavigationContext.WorldMap:
                    ProcessWorldMapKeyEvent(keyCode, modifiers);
                    break;
                case NavigationContext.Dialogue:
                    // Future: ProcessDialogueKeyEvent(keyCode);
                    break;
                case NavigationContext.Encyclopedia:
                    ProcessEncyclopediaKeyEvent(keyCode);
                    break;
                case NavigationContext.Embark:
                    // Embark panel handles its own keys via IsOpen check above
                    // This case is for when embark panel is open but key wasn't handled
                    break;
                case NavigationContext.None:
                default:
                    // No special input handling (pause/speed only in Map context)
                    break;
            }
        }

        /// <summary>
        /// Handle key event when a tutorial is active.
        /// Returns true if key was handled.
        /// </summary>
        private bool ProcessTutorialKeyEvent(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F1:
                    _tutorialHandler.ReannounceCurrentTutorial();
                    return true;

                default:
                    // Let other keys pass through (arrow keys, game's continue key, etc.)
                    return false;
            }
        }

        /// <summary>
        /// Handle key event when navigating popups/menus.
        /// </summary>
        private void ProcessPopupKeyEvent(KeyCode keyCode)
        {
            // If a dropdown is open, handle it first
            if (_uiNavigator.IsDropdownOpen)
            {
                if (ProcessDropdownKeyEvent(keyCode))
                {
                    return; // Key was handled by dropdown
                }
                // Dropdown was closed externally, fall through to normal handling
            }

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    _uiNavigator.NavigateElement(-1);
                    break;
                case KeyCode.DownArrow:
                    _uiNavigator.NavigateElement(1);
                    break;
                case KeyCode.LeftArrow:
                    _uiNavigator.NavigatePanel(-1);
                    break;
                case KeyCode.RightArrow:
                    _uiNavigator.NavigatePanel(1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _uiNavigator.ActivateCurrentElement();
                    break;
                case KeyCode.Space:
                    _uiNavigator.ActivateCurrentElement();
                    break;
                // Note: Escape is handled by the game's native handler, not here
                // (our handler would conflict with the game's toggle logic)
            }
        }

        /// <summary>
        /// Handle key event when a dropdown is open.
        /// Returns true if the key was handled, false if dropdown was closed externally.
        /// </summary>
        private bool ProcessDropdownKeyEvent(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    return _uiNavigator.NavigateDropdownOption(-1);

                case KeyCode.DownArrow:
                    return _uiNavigator.NavigateDropdownOption(1);

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _uiNavigator.SelectCurrentDropdownOption();
                    return true;

                case KeyCode.Escape:
                    _uiNavigator.CloseActiveDropdown();
                    return true;

                default:
                    // Other keys - let dropdown stay open but don't handle
                    return true;
            }
        }

        /// <summary>
        /// Handle key event when navigating the settlement map.
        /// </summary>
        private void ProcessMapKeyEvent(KeyCode keyCode, KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    _mapNavigator.MoveCursor(0, 1);
                    break;
                case KeyCode.DownArrow:
                    _mapNavigator.MoveCursor(0, -1);
                    break;
                case KeyCode.LeftArrow:
                    _mapNavigator.MoveCursor(-1, 0);
                    break;
                case KeyCode.RightArrow:
                    _mapNavigator.MoveCursor(1, 0);
                    break;
                case KeyCode.K:
                    _mapNavigator.AnnounceCurrentPosition();
                    break;
                case KeyCode.Space:
                    GameReflection.TogglePause();
                    break;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    GameReflection.SetSpeed(1);
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    GameReflection.SetSpeed(2);
                    break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    GameReflection.SetSpeed(3);
                    break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    GameReflection.SetSpeed(4);
                    break;

                // Stats hotkeys
                case KeyCode.S:
                    if (modifiers.Alt)
                        _statsPanel?.Open();
                    else
                        StatsReader.AnnounceQuickSummary();
                    break;
                case KeyCode.R:
                    StatsReader.AnnounceResolveSummary();
                    break;

                // Map Scanner controls
                case KeyCode.PageUp:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(-1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(-1);
                    else
                        _mapScanner?.ChangeGroup(-1);
                    break;
                case KeyCode.PageDown:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(1);
                    else
                        _mapScanner?.ChangeGroup(1);
                    break;
                case KeyCode.Home:
                    _mapScanner?.AnnounceDistance();
                    break;
                case KeyCode.End:
                    _mapScanner?.MoveCursorToItem();
                    break;
                case KeyCode.I:
                    TileInfoReader.ReadCurrentTile(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    break;
            }
        }

        /// <summary>
        /// Handle key event when navigating the world map hex grid.
        /// </summary>
        private void ProcessWorldMapKeyEvent(KeyCode keyCode, KeyModifiers modifiers)
        {
            if (_worldMapNavigator == null) return;

            // Check if effects panel is open first
            if (_worldMapNavigator.ProcessPanelKeyEvent(keyCode))
                return;

            switch (keyCode)
            {
                // Arrow key navigation (zigzag pattern for up/down)
                case KeyCode.RightArrow:
                    _worldMapNavigator.MoveArrow(1, 0);
                    break;
                case KeyCode.LeftArrow:
                    _worldMapNavigator.MoveArrow(-1, 0);
                    break;
                case KeyCode.UpArrow:
                    _worldMapNavigator.MoveArrow(0, 1);
                    break;
                case KeyCode.DownArrow:
                    _worldMapNavigator.MoveArrow(0, -1);
                    break;

                // Scanner controls
                case KeyCode.PageUp:
                    if (modifiers.Alt)
                        _worldMapScanner?.ChangeItem(-1);
                    else
                        _worldMapScanner?.ChangeType(-1);
                    break;
                case KeyCode.PageDown:
                    if (modifiers.Alt)
                        _worldMapScanner?.ChangeItem(1);
                    else
                        _worldMapScanner?.ChangeType(1);
                    break;
                case KeyCode.Home:
                    _worldMapScanner?.AnnounceDirection();
                    break;
                case KeyCode.End:
                    _worldMapScanner?.JumpToItem();
                    break;

                // Select tile (embark)
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _worldMapNavigator.Interact();
                    break;

                // Read full tooltip content
                case KeyCode.I:
                    _worldMapNavigator.ReadTooltip();
                    break;

                // Read embark status and distance to capital
                case KeyCode.D:
                    _worldMapNavigator.ReadEmbarkAndDistance();
                    break;

                // Open effects panel
                case KeyCode.M:
                    _worldMapNavigator.OpenEffectsPanel();
                    break;
            }
        }

        /// <summary>
        /// Handle key event when navigating the encyclopedia/wiki popup.
        /// </summary>
        private void ProcessEncyclopediaKeyEvent(KeyCode keyCode)
        {
            if (_encyclopediaNavigator == null || !_encyclopediaNavigator.IsActive) return;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    _encyclopediaNavigator.NavigateElement(-1);
                    break;
                case KeyCode.DownArrow:
                    _encyclopediaNavigator.NavigateElement(1);
                    break;
                case KeyCode.LeftArrow:
                    _encyclopediaNavigator.NavigatePanel(-1);
                    break;
                case KeyCode.RightArrow:
                    _encyclopediaNavigator.NavigatePanel(1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    _encyclopediaNavigator.ActivateCurrentElement();
                    break;
            }
        }

        // ============================================================
        // TEMPORARY DEBUG HOTKEYS - Remove this entire section when done
        // ============================================================

        /// <summary>
        /// Process temporary debug hotkeys for testing.
        /// Returns true if a key was handled.
        /// TODO: Remove this method and its call when proper UI is implemented.
        /// </summary>
        private bool ProcessTemporaryDebugKeys(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.B:
                    Debug.Log("[ATSAccessibility] DEBUG: B pressed - opening reputation rewards popup");
                    OpenReputationRewardsPopup();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Open the reputation rewards popup via reflection.
        /// TODO: Remove when proper UI navigation is implemented.
        /// </summary>
        private void OpenReputationRewardsPopup()
        {
            var reputationRewardsService = GameReflection.GetReputationRewardsService();
            if (reputationRewardsService == null)
            {
                Debug.LogWarning("[ATSAccessibility] DEBUG: ReputationRewardsService not available");
                return;
            }

            try
            {
                var requestPopupMethod = reputationRewardsService.GetType().GetMethod("RequestPopup");
                if (requestPopupMethod != null)
                {
                    requestPopupMethod.Invoke(reputationRewardsService, null);
                    Debug.Log("[ATSAccessibility] DEBUG: RequestPopup() called");
                }
                else
                {
                    Debug.LogWarning("[ATSAccessibility] DEBUG: RequestPopup method not found");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] DEBUG: Failed to open reputation popup: {ex.Message}");
            }
        }

        // ============================================================
        // END TEMPORARY DEBUG HOTKEYS
        // ============================================================
    }
}
