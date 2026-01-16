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
        /// Navigation context determines which keys do what.
        /// </summary>
        public enum NavigationContext
        {
            None,           // No special handling
            Popup,          // Navigating a popup/menu
            Map,            // Navigating settlement map
            Dialogue,       // Future: reading dialogue
            Encyclopedia    // Navigating wiki/encyclopedia popup
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
        public void ProcessKeyEvent(KeyCode keyCode)
        {
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
                    ProcessMapKeyEvent(keyCode);
                    break;
                case NavigationContext.Dialogue:
                    // Future: ProcessDialogueKeyEvent(keyCode);
                    break;
                case NavigationContext.Encyclopedia:
                    ProcessEncyclopediaKeyEvent(keyCode);
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
        private void ProcessMapKeyEvent(KeyCode keyCode)
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
