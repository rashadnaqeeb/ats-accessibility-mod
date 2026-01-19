using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Centralized keyboard input handling with handler chain pattern.
    /// Handlers are processed in priority order; the first active handler
    /// that returns true from ProcessKey() consumes the key event.
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
        /// Navigation context for debugging and logging purposes.
        /// With the full handler chain, this is purely informational.
        /// </summary>
        public enum NavigationContext
        {
            None,
            Popup,
            Map,
            WorldMap,
            Dialogue,
            Encyclopedia,
            Embark
        }

        // Current navigation context (informational only)
        public NavigationContext CurrentContext { get; private set; } = NavigationContext.None;

        // Handler chain in priority order
        private readonly List<IKeyHandler> _handlers = new List<IKeyHandler>();

        /// <summary>
        /// Register a key handler. Handlers are processed in registration order.
        /// </summary>
        public void RegisterHandler(IKeyHandler handler)
        {
            if (handler != null && !_handlers.Contains(handler))
            {
                _handlers.Add(handler);
                Debug.Log($"[ATSAccessibility] Registered key handler: {handler.GetType().Name}");
            }
        }

        /// <summary>
        /// Set the current navigation context (informational only).
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
        /// Iterates through handlers in priority order until one handles the key.
        /// </summary>
        public void ProcessKeyEvent(KeyCode keyCode, KeyModifiers modifiers = default)
        {
            foreach (var handler in _handlers)
            {
                if (handler.IsActive && handler.ProcessKey(keyCode, modifiers))
                {
                    return; // Key was handled
                }
            }

            // Key was not handled by any handler - let it pass through to the game
        }
    }
}
