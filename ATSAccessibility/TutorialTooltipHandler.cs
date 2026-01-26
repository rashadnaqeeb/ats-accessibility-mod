using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Key handler for TutorialTooltip navigation.
    /// Provides keyboard support for advancing through tutorial text.
    /// Auto-announces text when tooltip becomes visible or text changes.
    ///
    /// State machine:
    /// - Engaged when tooltip appears or text changes (captures keys)
    /// - Disengaged when Enter pressed with no continue button (lets player act)
    /// - Re-engages when text changes
    /// </summary>
    public class TutorialTooltipHandler : IKeyHandler
    {
        // Reference to UINavigator to check if a popup is blocking
        private readonly UINavigator _uiNavigator;

        // State tracking (updated by CheckForTextChanges in Update loop)
        private bool _isVisible = false;   // Cached visibility to avoid reflection in IsActive
        private bool _wasVisible = false;
        private string _lastText = null;
        private string _lastAnnouncedText = null;  // Track what we've actually announced to prevent duplicates
        private bool _isEngaged = false;   // Whether we're capturing keys
        private int _lastPhase = -1;       // Track phase changes
        private bool _forceEngaged = false; // When true, stay engaged regardless of tooltip visibility

        public TutorialTooltipHandler(UINavigator uiNavigator)
        {
            _uiNavigator = uiNavigator;
        }

        /// <summary>
        /// Force the handler to engage and capture keys.
        /// Called externally when we detect the tooltip via polling.
        /// Sets _forceEngaged flag to prevent CheckForTextChanges from disengaging.
        /// </summary>
        public void ForceEngage()
        {
            _isVisible = true;
            _isEngaged = true;
            _forceEngaged = true;
        }

        // ========================================
        // HARDCODED ACCESSIBILITY MESSAGES
        // ========================================
        // Maps TutorialPhase ID to custom accessibility text.
        // If a phase has a custom message, it replaces the game's text.
        // If not, the game's text is used as fallback.

        private static readonly Dictionary<int, string> _accessibilityMessages = new Dictionary<int, string>
        {
            // Tutorial 1: Basics
            { 10, "Welcome to against the storm! You are now in a tutorial mode. You can press enter to dismiss these tutorial boxes, though you will have to wait for the animations to finish. Now, explore your new settlement with the arrow keys." }, // CameraControl
            { 20, "Neglecting your village will increase the Queen's Impatience and bring her wrath upon you. You will lose the game if the impatience bar fills." }, // Impatience
            { 30, "Fulfilling your duties will increase the town's Reputation, unlock new buildings and eventually bring you to victory. You can check both of these stats on the fly by pressing s at any time outside these popups. For more details, you can open the dedicated stats screen, found in the f1 information menu." }, // Reputation
            { 35, "As a reward for gaining reputation points, you unlock blueprints- new buildings for you to use to win the game. One is ready to collect now. Dismiss this popup and press f3, the rewards menu. Pick blueprints." }, // ReputationPick
            { 40, "You can now press space bar to resume the game. If you press the numbers 1 through 4, you control the speed at which time flows." }, // TimeControl
            { 50, "Now, you will have to build a woodcutters camp. Check the read me for building instructions. The rest of this tutorial involves completing orders, which you can find in the f2 menu. This is the last tooltip." }, // Wood

            // Tutorial 4: The Cycle
            { 340, "It is almost upon us, so no caravans are allowed to embark. Press E to finish the cycle." }, // CyclePreFinish
        };

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        // Active when tooltip visible AND we're engaged AND no popup is blocking input
        // OR when force-engaged (for world map tutorial after MetaRewardsPopup)
        // This prevents consuming keys when MetaRewardsPopup or other popups are open
        public bool IsActive
        {
            get
            {
                bool hasPopup = _uiNavigator?.HasActivePopup ?? false;
                // Force engaged bypasses visibility check (for world map tutorial)
                bool result = (_forceEngaged || (_isVisible && _isEngaged)) && !hasPopup;
                return result;
            }
        }

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (TutorialReflection.IsButtonActive() && TutorialReflection.TriggerContinue())
                    {
                        // Success - clear force engaged state
                        _forceEngaged = false;
                    }
                    else if (TutorialReflection.IsButtonExpected())
                    {
                        // Button will appear after animation - wait
                        Speech.Say("Wait for animation to finish");
                    }
                    else
                    {
                        // No button expected - disengage so player can perform the required action
                        _isEngaged = false;
                        _forceEngaged = false;
                        Speech.Say("Perform the action to continue");
                    }
                    return true;

                case KeyCode.Escape:
                    // Disengage so player can interact with game, but don't close tooltip
                    _isEngaged = false;
                    _forceEngaged = false;
                    Speech.Say("Tutorial paused");
                    return true;

                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    // Re-read current text on arrow keys
                    AnnounceCurrentText();
                    return true;

                default:
                    // Consume all other keys while engaged
                    return true;
            }
        }

        // ========================================
        // AUTO-ANNOUNCE POLLING
        // ========================================

        /// <summary>
        /// Called from AccessibilityCore's Update loop to detect visibility
        /// and text changes for auto-announcement.
        /// </summary>
        public void CheckForTextChanges()
        {
            // Update cached visibility (used by IsActive to avoid reflection)
            _isVisible = TutorialReflection.IsTooltipVisible();

            if (_isVisible)
            {
                int currentPhase = TutorialReflection.GetCurrentPhase();
                string gameText = TutorialReflection.GetCurrentText();

                // Use custom message if available, otherwise use game text
                string textToAnnounce = GetTextForPhase(currentPhase, gameText);

                // Check for phase/text changes
                bool phaseChanged = currentPhase != _lastPhase && currentPhase != -1;
                bool textChanged = gameText != _lastText && !string.IsNullOrEmpty(gameText);

                // Announce when tooltip first becomes visible OR when text actually changes
                if (!_wasVisible || phaseChanged || textChanged)
                {
                    if (!string.IsNullOrEmpty(textToAnnounce))
                    {
                        _isEngaged = true;  // Re-engage on new text
                        // Don't announce if force engaged (already announced by polling)
                        // Also don't re-announce if we already announced this exact text
                        if (!_forceEngaged && textToAnnounce != _lastAnnouncedText)
                        {
                            Speech.Say(textToAnnounce);
                            _lastAnnouncedText = textToAnnounce;
                        }
                        _lastText = gameText;
                        _lastPhase = currentPhase;
                    }
                }
            }
            else if (!_forceEngaged)
            {
                // Reset when tooltip is hidden (unless force engaged)
                // Keep _lastText and _lastPhase to avoid re-announcing same content
                _isEngaged = false;
                _lastAnnouncedText = null;  // Clear so same text can announce if tutorial shows again
            }

            _wasVisible = _isVisible;
        }

        /// <summary>
        /// Get the text to announce for a given phase.
        /// Returns custom accessibility message if available, otherwise the game's text.
        /// </summary>
        private string GetTextForPhase(int phase, string gameText)
        {
            if (_accessibilityMessages.TryGetValue(phase, out string customMessage))
            {
                return customMessage;
            }
            return gameText;
        }

        // ========================================
        // HELPERS
        // ========================================

        private void AnnounceCurrentText()
        {
            int currentPhase = TutorialReflection.GetCurrentPhase();
            string gameText = TutorialReflection.GetCurrentText();
            string text = GetTextForPhase(currentPhase, gameText);

            if (!string.IsNullOrEmpty(text))
            {
                Speech.Say(text);
            }
            else
            {
                Speech.Say("No text");
            }
        }
    }
}
