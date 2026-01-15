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
            None,       // No special handling
            Popup,      // Navigating a popup/menu
            Map,        // Future: navigating game map
            Dialogue    // Future: reading dialogue
        }

        // Current navigation context
        public NavigationContext CurrentContext { get; private set; } = NavigationContext.None;

        // Reference to UI navigator for popup input handling
        private readonly UINavigator _uiNavigator;

        public KeyboardManager(UINavigator uiNavigator)
        {
            _uiNavigator = uiNavigator;
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
            switch (CurrentContext)
            {
                case NavigationContext.Popup:
                    ProcessPopupKeyEvent(keyCode);
                    break;
                case NavigationContext.Map:
                    // Future: ProcessMapKeyEvent(keyCode);
                    break;
                case NavigationContext.Dialogue:
                    // Future: ProcessDialogueKeyEvent(keyCode);
                    break;
                case NavigationContext.None:
                default:
                    // No special input handling
                    break;
            }
        }

        /// <summary>
        /// Handle key event when navigating popups/menus.
        /// </summary>
        private void ProcessPopupKeyEvent(KeyCode keyCode)
        {
            if (_uiNavigator == null)
            {
                Debug.LogWarning("[ATSAccessibility] DEBUG: ProcessPopupKeyEvent - UINavigator is null");
                return;
            }

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Debug.Log("[ATSAccessibility] DEBUG: UpArrow pressed");
                    _uiNavigator.NavigateElement(-1);
                    break;
                case KeyCode.DownArrow:
                    Debug.Log("[ATSAccessibility] DEBUG: DownArrow pressed");
                    _uiNavigator.NavigateElement(1);
                    break;
                case KeyCode.LeftArrow:
                    Debug.Log("[ATSAccessibility] DEBUG: LeftArrow pressed");
                    _uiNavigator.NavigatePanel(-1);
                    break;
                case KeyCode.RightArrow:
                    Debug.Log("[ATSAccessibility] DEBUG: RightArrow pressed");
                    _uiNavigator.NavigatePanel(1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Debug.Log("[ATSAccessibility] DEBUG: Enter pressed");
                    _uiNavigator.ActivateCurrentElement();
                    break;
                case KeyCode.Space:
                    Debug.Log("[ATSAccessibility] DEBUG: Space pressed");
                    _uiNavigator.ActivateCurrentElement();
                    break;
                case KeyCode.Escape:
                    Debug.Log("[ATSAccessibility] DEBUG: Escape pressed");
                    _uiNavigator.DismissPopup();
                    break;
            }
        }
    }
}
