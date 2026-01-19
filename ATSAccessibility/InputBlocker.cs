using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace ATSAccessibility
{
    /// <summary>
    /// Manages input blocking state and action whitelisting.
    /// When blocking is enabled, all game input is blocked except whitelisted actions.
    /// </summary>
    public static class InputBlocker
    {
        /// <summary>
        /// Whether input blocking is currently active.
        /// Can be temporarily disabled when editing text fields.
        /// </summary>
        public static bool IsBlocking { get; set; } = true;

        /// <summary>
        /// When true, blocks the Cancel action once then resets itself.
        /// Used when StatsPanel is closing to prevent game menu from opening.
        /// </summary>
        public static bool BlockCancelOnce { get; set; } = false;

        // Action names to allow through InputService even when blocking
        private static readonly HashSet<string> WhitelistedActions = new HashSet<string>
        {
            "Confirm",           // Enter key for button confirmation
            "Cancel",            // Escape key for menu/popup dismissal
            "ContinueTutorial"   // Enter key for tutorial advancement
        };

        /// <summary>
        /// Check if an InputAction should be allowed through when blocking.
        /// </summary>
        public static bool IsActionWhitelisted(InputAction action)
        {
            if (action == null) return false;
            return WhitelistedActions.Contains(action.name);
        }
    }
}
