using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Interface for building-specific panel navigators.
    /// Each building type can have a specialized navigator that knows
    /// how to read and interact with that building's unique features.
    /// </summary>
    public interface IBuildingNavigator
    {
        /// <summary>
        /// Open the navigator for a specific building.
        /// Called when building panel is shown.
        /// </summary>
        void Open(object building);

        /// <summary>
        /// Close the navigator.
        /// Called when building panel is hidden.
        /// </summary>
        void Close();

        /// <summary>
        /// Process a key event for navigation.
        /// </summary>
        /// <param name="keyCode">The key pressed</param>
        /// <param name="modifiers">Modifier keys state</param>
        /// <returns>True if the key was handled, false to pass through</returns>
        bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers);
    }
}
