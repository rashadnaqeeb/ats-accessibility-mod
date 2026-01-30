using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the WorldTutorialsHUD.
    /// Provides keyboard navigation for the 4 tutorial missions on the world map.
    /// Opened via F1 key from WorldMapKeyHandler.
    /// </summary>
    public class WorldTutorialsOverlay : IKeyHandler
    {
        // State
        private bool _isOpen = false;
        private int _currentIndex = 0;
        private List<TutorialReflection.TutorialInfo> _tutorials = new List<TutorialReflection.TutorialInfo>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(_tutorials.Count - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SelectCurrentTutorial();
                    return true;

                case KeyCode.Escape:
                case KeyCode.F1:
                    // Close the HUD and overlay (F1 toggles, Escape just closes)
                    TutorialReflection.ToggleWorldTutorialsHUD();
                    Close();
                    return true;

                default:
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when WorldTutorialsHUD becomes visible.
        /// Called from AccessibilityCore when polling detects visibility change.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _currentIndex = 0;

            RefreshData();

            if (_tutorials.Count > 0)
            {
                Speech.Say($"Tutorials. {GetTutorialAnnouncement(0)}");
            }
            else
            {
                Speech.Say("Tutorials. No tutorials available");
            }

            Debug.Log($"[ATSAccessibility] WorldTutorialsOverlay opened, {_tutorials.Count} tutorials");
        }

        /// <summary>
        /// Close the overlay when WorldTutorialsHUD is hidden.
        /// Called from AccessibilityCore when polling detects visibility change.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _tutorials.Clear();

            Debug.Log("[ATSAccessibility] WorldTutorialsOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _tutorials = TutorialReflection.GetAllTutorials();
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_tutorials.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _tutorials.Count);
            AnnounceCurrentTutorial();
        }

        private void NavigateTo(int index)
        {
            if (_tutorials.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _tutorials.Count - 1);
            AnnounceCurrentTutorial();
        }

        private void AnnounceCurrentTutorial()
        {
            if (_currentIndex < 0 || _currentIndex >= _tutorials.Count) return;
            Speech.Say(GetTutorialAnnouncement(_currentIndex));
        }

        private string GetTutorialAnnouncement(int index)
        {
            if (index < 0 || index >= _tutorials.Count) return "";

            var tutorial = _tutorials[index];
            var status = "";

            if (!tutorial.IsUnlocked)
            {
                status = ", locked";
            }
            else if (tutorial.IsCompleted)
            {
                status = ", completed";
            }

            return $"{tutorial.DisplayName}{status}";
        }

        // ========================================
        // SELECTION
        // ========================================

        private void SelectCurrentTutorial()
        {
            if (_tutorials.Count == 0 || _currentIndex < 0 || _currentIndex >= _tutorials.Count) return;

            var tutorial = _tutorials[_currentIndex];

            if (!tutorial.IsUnlocked)
            {
                // Announce locked reason
                var reason = tutorial.LockedReason;
                if (!string.IsNullOrEmpty(reason))
                {
                    Speech.Say($"Locked. {reason}");
                }
                else
                {
                    Speech.Say("Locked");
                }
                SoundManager.PlayFailed();
                return;
            }

            // Start the tutorial - game will show its own confirmation popup
            if (TutorialReflection.StartTutorial(tutorial.Config))
            {
                SoundManager.PlayButtonClick();
                // Game will show confirmation popup, then start tutorial if confirmed
            }
            else
            {
                Speech.Say("Could not start tutorial");
                SoundManager.PlayFailed();
            }
        }
    }
}
