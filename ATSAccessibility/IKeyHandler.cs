using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Interface for components that handle keyboard input.
    /// Handlers are processed in priority order; the first active handler
    /// that returns true from ProcessKey() consumes the key event.
    /// </summary>
    public interface IKeyHandler
    {
        /// <summary>
        /// Whether this handler is currently active and should receive input.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Process a key event.
        /// </summary>
        /// <param name="keyCode">The key pressed</param>
        /// <param name="modifiers">Modifier keys state</param>
        /// <returns>True if the key was handled, false to pass to next handler</returns>
        bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers);
    }
}
