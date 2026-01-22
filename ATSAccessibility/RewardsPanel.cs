using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// F3 Rewards panel for quick access to pending rewards.
    /// Lists available reward categories (Blueprints, Cornerstones, Newcomers)
    /// and opens the game's popup when selected.
    /// </summary>
    public class RewardsPanel : IKeyHandler
    {
        private enum RewardType
        {
            Blueprints,
            Cornerstones,
            Newcomers
        }

        private static readonly string[] _rewardLabels =
        {
            "Blueprints",
            "Cornerstones",
            "Newcomers"
        };

        private bool _isOpen;
        private List<RewardType> _availableRewards = new List<RewardType>();
        private int _currentIndex;

        /// <summary>
        /// Whether the rewards panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Open the rewards panel. If no rewards available, announces and returns.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            RefreshAvailableRewards();

            if (_availableRewards.Count == 0)
            {
                Speech.Say("No rewards available");
                return;
            }

            _isOpen = true;
            _currentIndex = 0;

            AnnouncePanel();
            AnnounceCurrentReward();
            Debug.Log("[ATSAccessibility] Rewards panel opened");
        }

        /// <summary>
        /// Close the rewards panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _availableRewards.Clear();
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Closed");
            Debug.Log("[ATSAccessibility] Rewards panel closed");
        }

        /// <summary>
        /// Process a key event for the rewards panel (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
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

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    OpenSelectedReward();
                    return true;

                case KeyCode.Escape:
                case KeyCode.LeftArrow:
                    Close();
                    return true;

                case KeyCode.F3:
                    Close();
                    return true;

                default:
                    // Consume all other keys while panel is open
                    return true;
            }
        }

        private void RefreshAvailableRewards()
        {
            _availableRewards.Clear();

            if (RewardsReflection.HasPendingBlueprints())
            {
                _availableRewards.Add(RewardType.Blueprints);
            }

            if (RewardsReflection.HasPendingCornerstones())
            {
                _availableRewards.Add(RewardType.Cornerstones);
            }

            if (RewardsReflection.HasPendingNewcomers())
            {
                _availableRewards.Add(RewardType.Newcomers);
            }

            Debug.Log($"[ATSAccessibility] Found {_availableRewards.Count} available rewards");
        }

        private void Navigate(int direction)
        {
            if (_availableRewards.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _availableRewards.Count);
            AnnounceCurrentReward();
        }

        private void OpenSelectedReward()
        {
            if (_availableRewards.Count == 0 || _currentIndex >= _availableRewards.Count) return;

            var rewardType = _availableRewards[_currentIndex];
            string label = _rewardLabels[(int)rewardType];
            Debug.Log($"[ATSAccessibility] Opening {label} from Rewards panel");

            bool success = false;

            switch (rewardType)
            {
                case RewardType.Blueprints:
                    success = RewardsReflection.OpenBlueprintsPopup();
                    break;

                case RewardType.Cornerstones:
                    success = RewardsReflection.OpenCornerstonesPopup();
                    break;

                case RewardType.Newcomers:
                    success = RewardsReflection.OpenNewcomersPopup();
                    break;
            }

            if (success)
            {
                // Close panel after opening popup
                _isOpen = false;
                _availableRewards.Clear();
                Debug.Log($"[ATSAccessibility] Successfully opened {label}");
            }
            else
            {
                Speech.Say($"{label} unavailable");
                Debug.Log($"[ATSAccessibility] Failed to open {label}");
            }
        }

        private void AnnouncePanel()
        {
            int count = _availableRewards.Count;
            Speech.Say($"Rewards, {count} available");
        }

        private void AnnounceCurrentReward()
        {
            if (_availableRewards.Count == 0 || _currentIndex >= _availableRewards.Count) return;

            var rewardType = _availableRewards[_currentIndex];
            string label = _rewardLabels[(int)rewardType];
            Speech.Say(label);
        }
    }
}
