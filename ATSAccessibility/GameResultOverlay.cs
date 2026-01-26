using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the GameResultPopup (victory/defeat screen).
    /// Flat top-level list with expandable sections for details.
    /// </summary>
    public class GameResultOverlay : IKeyHandler
    {
        private enum ItemType { ReadOnly, Section, Button }

        private class TopLevelItem
        {
            public ItemType Type;
            public string Label;
            public Action OnActivate;           // For Button type
            public List<string> SubItems;       // For Section type
        }

        // State
        private bool _isOpen;
        private object _popup;
        private bool _inSection;
        private int _topIndex;
        private int _subIndex;

        // Navigation data
        private List<TopLevelItem> _items = new List<TopLevelItem>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen && IsPopupVisible();

        private bool IsPopupVisible()
        {
            if (_popup == null) return false;
            var mb = _popup as MonoBehaviour;
            if (mb == null) return false;
            return mb.gameObject != null && mb.gameObject.activeSelf;
        }

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_inSection)
                        NavigateSubItem(-1);
                    else
                        NavigateTopLevel(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_inSection)
                        NavigateSubItem(1);
                    else
                        NavigateTopLevel(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    if (_inSection)
                        return true;  // No action in sub-items, just consume
                    else
                        ActivateOrEnter();
                    return true;

                case KeyCode.LeftArrow:
                    if (_inSection)
                    {
                        ReturnToTopLevel();
                        return true;
                    }
                    // Consume at top level
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _inSection = false;
            _topIndex = 0;
            _subIndex = 0;

            RefreshData();

            // Announce first item (the summary)
            if (_items.Count > 0)
            {
                Speech.Say(_items[0].Label);
            }

            Debug.Log($"[ATSAccessibility] GameResultOverlay opened, {_items.Count} top-level items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] GameResultOverlay closed");
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void NavigateTopLevel(int direction)
        {
            if (_items.Count == 0) return;

            _topIndex = NavigationUtils.WrapIndex(_topIndex, direction, _items.Count);
            Speech.Say(_items[_topIndex].Label);
        }

        private void ActivateOrEnter()
        {
            if (_items.Count == 0 || _topIndex < 0 || _topIndex >= _items.Count) return;

            var item = _items[_topIndex];

            switch (item.Type)
            {
                case ItemType.ReadOnly:
                    // Just re-announce
                    Speech.Say(item.Label);
                    break;

                case ItemType.Section:
                    if (item.SubItems != null && item.SubItems.Count > 0)
                    {
                        _inSection = true;
                        _subIndex = 0;
                        Speech.Say(item.SubItems[0]);
                    }
                    else
                    {
                        Speech.Say("Empty");
                    }
                    break;

                case ItemType.Button:
                    if (item.OnActivate != null)
                    {
                        item.OnActivate();
                        SoundManager.PlayButtonClick();
                    }
                    break;
            }
        }

        private void NavigateSubItem(int direction)
        {
            var item = _items[_topIndex];
            if (item.SubItems == null || item.SubItems.Count == 0) return;

            _subIndex = NavigationUtils.WrapIndex(_subIndex, direction, item.SubItems.Count);
            Speech.Say(item.SubItems[_subIndex]);
        }

        private void ReturnToTopLevel()
        {
            _inSection = false;
            _subIndex = 0;
            Speech.Say(_items[_topIndex].Label);
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // 1. Summary (header: flavor text) - read only
            AddSummaryItem();

            // 2. Progression section
            AddProgressionSection();

            // 3. Score section (if not tutorial)
            AddScoreSection();

            // 4. Tutorial rewards section (if tutorial)
            AddTutorialRewardsSection();

            // 5. World Event section (if active)
            AddWorldEventSection();

            // 6. Action buttons at the end
            AddActionButtons();
        }

        private void AddSummaryItem()
        {
            string header = GameResultReflection.GetHeaderText(_popup);
            if (string.IsNullOrEmpty(header))
            {
                header = GameResultReflection.HasWon() ? "Victory" : "Defeat";
            }

            string desc = GameResultReflection.GetDescriptionText(_popup);

            // Flavor text comes pre-localized with period, so just use colon separator
            string label = string.IsNullOrEmpty(desc) ? header : $"{header}: {desc}";

            _items.Add(new TopLevelItem
            {
                Type = ItemType.ReadOnly,
                Label = label
            });
        }

        private void AddProgressionSection()
        {
            var subItems = new List<string>();

            // XP summary
            int gainedExp = GameResultReflection.GetGainedExp();
            var levelInfo = GameResultReflection.GetLevelInfo();

            string expSummary;
            if (levelInfo.targetExp <= 0 || levelInfo.exp >= levelInfo.targetExp)
            {
                expSummary = $"Gained {gainedExp} experience, Level {levelInfo.level}, max level";
            }
            else
            {
                expSummary = $"Gained {gainedExp} experience, Level {levelInfo.level}, {levelInfo.exp} of {levelInfo.targetExp} to next level";
            }
            subItems.Add(expSummary);

            // Completed goals
            var completedGoals = GameResultReflection.GetCompletedGoals();
            foreach (var goal in completedGoals)
            {
                subItems.Add($"Completed: {goal}");
            }

            // Meta currencies from field rewards
            var currencies = GameResultReflection.GetMetaCurrencies();
            foreach (var (name, amount) in currencies)
            {
                subItems.Add($"{name}, {amount}");
            }

            // Stored meta currencies (goods collected during the game)
            var storedCurrencies = GameResultReflection.GetStoredMetaCurrencies();
            foreach (var (name, amount) in storedCurrencies)
            {
                subItems.Add($"{name}, {amount}");
            }

            // Seal fragments
            int sealFragments = GameResultReflection.GetSealFragments();
            if (sealFragments > 0)
            {
                subItems.Add($"Seal fragments, {sealFragments}");
            }

            _items.Add(new TopLevelItem
            {
                Type = ItemType.Section,
                Label = "Progression",
                SubItems = subItems
            });
        }

        private void AddScoreSection()
        {
            // Score section only appears if not tutorial
            if (GameResultReflection.IsTutorial()) return;

            var scoreBreakdown = GameResultReflection.GetScoreBreakdown();
            if (scoreBreakdown.Count == 0) return;

            var subItems = new List<string>();

            // Total score first (calculated from already-fetched breakdown to avoid redundant reflection)
            int totalScore = scoreBreakdown.Sum(s => s.Points);
            subItems.Add($"Total score, {totalScore} points");

            // Individual score entries
            foreach (var entry in scoreBreakdown)
            {
                subItems.Add($"{entry.Label}, {entry.Points} points");
            }

            _items.Add(new TopLevelItem
            {
                Type = ItemType.Section,
                Label = "Score",
                SubItems = subItems
            });
        }

        private void AddTutorialRewardsSection()
        {
            // Tutorial rewards section only appears for tutorials
            if (!GameResultReflection.IsTutorial()) return;

            var rewards = TutorialReflection.GetTutorialRewardsForCurrentBiome();
            if (rewards.Count == 0) return;

            var subItems = new List<string>();
            foreach (var reward in rewards)
            {
                subItems.Add($"Unlocked: {reward}");
            }

            _items.Add(new TopLevelItem
            {
                Type = ItemType.Section,
                Label = "Tutorial Unlocks",
                SubItems = subItems
            });
        }

        private void AddWorldEventSection()
        {
            // World event section only if there's an active event
            if (!GameResultReflection.HasActiveWorldEvent()) return;

            var eventInfo = GameResultReflection.GetWorldEventInfo();
            if (!eventInfo.HasValue) return;

            var info = eventInfo.Value;
            var subItems = new List<string>();

            // Event name
            subItems.Add(info.Name);

            // Result
            string resultText = info.Completed ? "Result: Completed" : "Result: Failed";
            subItems.Add(resultText);

            // Objectives
            if (info.Objectives != null)
            {
                foreach (var (key, value) in info.Objectives)
                {
                    subItems.Add($"{key}, {value}");
                }
            }

            _items.Add(new TopLevelItem
            {
                Type = ItemType.Section,
                Label = "World Event",
                SubItems = subItems
            });
        }

        private void AddActionButtons()
        {
            // Return to world map (always available)
            _items.Add(new TopLevelItem
            {
                Type = ItemType.Button,
                Label = "Return to world map",
                OnActivate = () => GameResultReflection.ClickMenuButton(_popup)
            });

            // Continue playing (only if available)
            if (GameResultReflection.IsContinueButtonAvailable(_popup))
            {
                _items.Add(new TopLevelItem
                {
                    Type = ItemType.Button,
                    Label = "Continue playing",
                    OnActivate = () => GameResultReflection.ClickContinueButton(_popup)
                });
            }
        }
    }
}
