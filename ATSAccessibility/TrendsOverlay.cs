using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the TrendsPopup.
    /// Provides navigation through goods and their storage operations.
    /// Number keys toggle time frame for aggregating operations.
    /// </summary>
    public class TrendsOverlay : IKeyHandler
    {
        // Time frame options (in ticks)
        private const int TICKS_10_SECONDS = 1;
        private const int TICKS_1_MINUTE = 6;
        private const int TICKS_5_MINUTES = 30;

        // State
        private bool _isOpen;
        private object _popup;
        private int _timeFrameTicks = TICKS_1_MINUTE;  // Default: 1 minute

        // Goods list
        private List<string> _goods = new List<string>();
        private int _goodIndex;

        // Operations for current good
        private List<TrendsReflection.AggregatedOperation> _operations = new List<TrendsReflection.AggregatedOperation>();
        private int _operationIndex;

        // Type-ahead search for goods
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Clear search on navigation keys
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                // Time frame toggles
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    SetTimeFrame(TICKS_10_SECONDS, "Last 10 seconds");
                    return true;

                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    SetTimeFrame(TICKS_1_MINUTE, "Last minute");
                    return true;

                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    SetTimeFrame(TICKS_5_MINUTES, "Last 5 minutes");
                    return true;

                // Goods navigation
                case KeyCode.LeftArrow:
                    NavigateGoods(-1);
                    return true;

                case KeyCode.RightArrow:
                    NavigateGoods(1);
                    return true;

                // Operations navigation
                case KeyCode.UpArrow:
                    NavigateOperations(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateOperations(1);
                    return true;

                case KeyCode.Backspace:
                    if (_search.RemoveChar())
                    {
                        if (_search.HasBuffer)
                            Speech.Say($"Search: {_search.Buffer}");
                        else
                            Speech.Say("Search cleared");
                    }
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                default:
                    // Type-ahead search for goods (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        _search.AddChar(c);

                        int match = FindMatchingGood();
                        if (match >= 0)
                        {
                            _goodIndex = match;
                            RefreshOperations();
                            AnnounceCurrentGood();
                        }
                        else
                        {
                            Speech.Say($"No match for {_search.Buffer}");
                        }
                        return true;
                    }

                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when TrendsPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _goodIndex = 0;
            _operationIndex = 0;
            _search.Clear();

            // Get all goods with trend data
            _goods = TrendsReflection.GetAllGoods();

            if (_goods.Count == 0)
            {
                Speech.Say("No goods with trend data");
                return;
            }

            // Try to start with the good selected in the popup
            string currentGood = TrendsReflection.GetCurrentGood(popup);
            if (!string.IsNullOrEmpty(currentGood))
            {
                int idx = _goods.IndexOf(currentGood);
                if (idx >= 0)
                {
                    _goodIndex = idx;
                }
            }

            RefreshOperations();
            AnnounceCurrentGood();
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _goods.Clear();
            _operations.Clear();
            _search.Clear();
        }

        // ========================================
        // TIME FRAME
        // ========================================

        private void SetTimeFrame(int ticks, string label)
        {
            if (_timeFrameTicks == ticks)
            {
                // Already on this time frame, just announce
                AnnounceTimeFrameAndGood(label);
                return;
            }

            _timeFrameTicks = ticks;
            RefreshOperations();
            AnnounceTimeFrameAndGood(label);
        }

        private void AnnounceTimeFrameAndGood(string timeFrameLabel)
        {
            if (_goods.Count == 0)
            {
                Speech.Say($"{timeFrameLabel}. No goods");
                return;
            }

            string goodName = GetCurrentGoodDisplayName();
            string changeText = FormatNetChange(GetNetChangeFromOperations());
            Speech.Say($"{timeFrameLabel}. {goodName}, {changeText}");
        }

        // ========================================
        // GOODS NAVIGATION
        // ========================================

        private void NavigateGoods(int direction)
        {
            if (_goods.Count == 0) return;

            _goodIndex = NavigationUtils.WrapIndex(_goodIndex, direction, _goods.Count);
            RefreshOperations();
            AnnounceCurrentGood();
        }

        private void AnnounceCurrentGood()
        {
            if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count)
            {
                Speech.Say("No goods");
                return;
            }

            string goodName = GetCurrentGoodDisplayName();
            string changeText = FormatNetChange(GetNetChangeFromOperations());
            Speech.Say($"{goodName}, {changeText}");
        }

        private string GetCurrentGoodDisplayName()
        {
            if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count)
                return "Unknown";

            return TrendsReflection.GetGoodDisplayName(_goods[_goodIndex]);
        }

        private int FindMatchingGood()
        {
            if (!_search.HasBuffer || _goods.Count == 0) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _goods.Count; i++)
            {
                string displayName = TrendsReflection.GetGoodDisplayName(_goods[i]);
                if (!string.IsNullOrEmpty(displayName) &&
                    displayName.ToLowerInvariant().StartsWith(lowerBuffer))
                {
                    return i;
                }
            }

            return -1;
        }

        // ========================================
        // OPERATIONS NAVIGATION
        // ========================================

        private void RefreshOperations()
        {
            _operationIndex = 0;

            if (_goods.Count == 0 || _goodIndex < 0 || _goodIndex >= _goods.Count)
            {
                _operations = new List<TrendsReflection.AggregatedOperation>();
                return;
            }

            _operations = TrendsReflection.GetAggregatedOperations(_goods[_goodIndex], _timeFrameTicks);
        }

        private void NavigateOperations(int direction)
        {
            if (_operations.Count == 0)
            {
                Speech.Say("No changes");
                return;
            }

            _operationIndex = NavigationUtils.WrapIndex(_operationIndex, direction, _operations.Count);
            AnnounceCurrentOperation();
        }

        private void AnnounceCurrentOperation()
        {
            if (_operations.Count == 0 || _operationIndex < 0 || _operationIndex >= _operations.Count)
            {
                Speech.Say("No changes");
                return;
            }

            var op = _operations[_operationIndex];
            string amountText = FormatAmount(op.TotalAmount);
            Speech.Say($"{op.DisplayName}: {amountText}");
        }

        private int GetNetChangeFromOperations()
        {
            int net = 0;
            for (int i = 0; i < _operations.Count; i++)
                net += _operations[i].TotalAmount;
            return net;
        }

        // ========================================
        // FORMATTING
        // ========================================

        private string FormatNetChange(int amount)
        {
            if (amount == 0)
                return "no changes";
            else if (amount > 0)
                return $"net +{amount}";
            else
                return $"net {amount}";
        }

        private string FormatAmount(int amount)
        {
            if (amount > 0)
                return $"+{amount}";
            else
                return amount.ToString();
        }
    }
}
