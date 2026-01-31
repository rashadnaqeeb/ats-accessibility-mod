using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// F3 Rewards panel for quick access to pending rewards.
    /// Always shows all three reward categories (Blueprints, Cornerstones, Newcomers).
    /// Available rewards open the game's popup when selected.
    /// Unavailable rewards show when they will next be available.
    /// </summary>
    public class RewardsPanel : IKeyHandler
    {
        private enum RewardType
        {
            Blueprints,
            Cornerstones,
            Newcomers
        }

        private struct RewardItem
        {
            public RewardType Type;
            public bool Available;
            public string Label;
        }

        private bool _isOpen;
        private List<RewardItem> _items = new List<RewardItem>();
        private int _currentIndex;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether the rewards panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Open the rewards panel. Always opens with all 3 reward types.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                SoundManager.PlayButtonClick();
                Close();
                return;
            }

            RefreshItems();

            _isOpen = true;
            _currentIndex = 0;
            _search.Clear();

            SoundManager.PlayPopupShow();
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
            _items.Clear();
            _search.Clear();
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

            // F-key panel switching - close self and pass through to open target panel
            if (keyCode == KeyCode.F1 || keyCode == KeyCode.F2)
            {
                Close();
                return false;  // Let SettlementKeyHandler open the target panel
            }

            _search.ClearOnNavigationKey(keyCode);

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
                    NavigateTo(_items.Count - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateSelected();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                case KeyCode.LeftArrow:
                    // Consume but do nothing - flat list, no drill-out
                    return true;

                case KeyCode.F3:
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while panel is open
                    return true;
            }
        }

        private void RefreshItems()
        {
            _items.Clear();

            // Blueprints
            if (RewardsReflection.HasPendingBlueprints())
            {
                _items.Add(new RewardItem { Type = RewardType.Blueprints, Available = true, Label = "Blueprints" });
            }
            else
            {
                var threshold = RewardsReflection.GetNextBlueprintThreshold();
                string label = threshold.HasValue
                    ? $"Blueprints, next at {threshold.Value.nextThreshold} reputation"
                    : "Blueprints, unavailable";
                _items.Add(new RewardItem { Type = RewardType.Blueprints, Available = false, Label = label });
            }

            // Cornerstones
            if (RewardsReflection.HasPendingCornerstones())
            {
                _items.Add(new RewardItem { Type = RewardType.Cornerstones, Available = true, Label = "Cornerstones" });
            }
            else
            {
                var nextDate = RewardsReflection.GetNextCornerstoneDate();
                string label = nextDate.HasValue
                    ? $"Cornerstones, next at {nextDate.Value.season}, Year {nextDate.Value.year}"
                    : "Cornerstones, unavailable";
                _items.Add(new RewardItem { Type = RewardType.Cornerstones, Available = false, Label = label });
            }

            // Newcomers
            if (RewardsReflection.HasPendingNewcomers())
            {
                _items.Add(new RewardItem { Type = RewardType.Newcomers, Available = true, Label = "Newcomers" });
            }
            else
            {
                float time = RewardsReflection.GetTimeToNextNewcomers();
                string label = time > 0
                    ? $"Newcomers, arriving in {RewardsReflection.FormatGameTime(time)}"
                    : "Newcomers, unavailable";
                _items.Add(new RewardItem { Type = RewardType.Newcomers, Available = false, Label = label });
            }

            Debug.Log($"[ATSAccessibility] Rewards panel refreshed: {_items.Count} items");
        }

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentReward();
        }

        private void NavigateTo(int index)
        {
            if (_items.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _items.Count - 1);
            AnnounceCurrentReward();
        }

        private void ActivateSelected()
        {
            if (_items.Count == 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            if (!item.Available)
            {
                // Re-announce the label for read-only items
                Speech.Say(item.Label);
                return;
            }

            Debug.Log($"[ATSAccessibility] Opening {item.Label} from Rewards panel");

            bool success = false;

            switch (item.Type)
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
                _isOpen = false;
                _items.Clear();
                Debug.Log($"[ATSAccessibility] Successfully opened {item.Label}");
            }
            else
            {
                Speech.Say($"{item.Label} unavailable");
                Debug.Log($"[ATSAccessibility] Failed to open {item.Label}");
            }
        }

        private void AnnounceCurrentReward()
        {
            if (_items.Count == 0 || _currentIndex >= _items.Count) return;

            Speech.Say(_items[_currentIndex].Label);
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private static readonly string[] _searchNames = { "Blueprints", "Cornerstones", "Newcomers" };

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentReward();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindMatch();
            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
                AnnounceCurrentReward();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatch()
        {
            if (!_search.HasBuffer || _items.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count && i < _searchNames.Length; i++)
            {
                if (_searchNames[i].ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
