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
        // State tracking (updated by CheckForTextChanges in Update loop)
        private bool _isVisible = false;   // Cached visibility to avoid reflection in IsActive
        private bool _wasVisible = false;
        private string _lastText = null;
        private bool _isEngaged = false;   // Whether we're capturing keys

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        // Active when tooltip visible AND we're engaged (uses cached state)
        public bool IsActive => _isVisible && _isEngaged;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (TutorialReflection.IsButtonActive())
                    {
                        // Button available - advance tutorial
                        if (!TutorialReflection.TriggerContinue())
                        {
                            Speech.Say("Could not advance tutorial");
                        }
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
                        Speech.Say("Perform the action to continue");
                    }
                    return true;

                case KeyCode.Escape:
                    // Disengage so player can interact with game, but don't close tooltip
                    _isEngaged = false;
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
                string currentText = TutorialReflection.GetCurrentText();

                // Engage and announce if tooltip just became visible OR text changed
                if (!_wasVisible || (currentText != _lastText && !string.IsNullOrEmpty(currentText)))
                {
                    if (!string.IsNullOrEmpty(currentText))
                    {
                        _isEngaged = true;  // Re-engage on new text
                        Speech.Say(currentText);
                        _lastText = currentText;
                    }
                }
            }
            else
            {
                // Reset when tooltip is hidden
                _lastText = null;
                _isEngaged = false;
            }

            _wasVisible = _isVisible;
        }

        // ========================================
        // HELPERS
        // ========================================

        private void AnnounceCurrentText()
        {
            string text = TutorialReflection.GetCurrentText();
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
