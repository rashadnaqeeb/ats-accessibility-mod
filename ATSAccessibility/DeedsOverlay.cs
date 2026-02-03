using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the GoalsPopup (Deeds menu).
    /// Two-level navigation: categories -> goals.
    /// Level 0 = categories, Level 1 = goals within current category.
    /// </summary>
    public class DeedsOverlay : MenuBase, IKeyHandler
    {
        // Goal entry within a category
        private class GoalEntry
        {
            public string Name;
            public object State;
            public object Model;
            public bool Completed;
            public bool Rewarded;
        }

        // Category containing goals
        private class CategoryEntry
        {
            public string Name;
            public List<GoalEntry> Goals;
        }

        // State
        private bool _captureNextPopup;
        private object _childPopup;

        // Data
        private List<CategoryEntry> _categories = new List<CategoryEntry>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen && !IsSuspended;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        /// <summary>
        /// Whether the next popup should be captured as a child (reward display after claim).
        /// </summary>
        public bool ShouldCaptureNextPopup
        {
            get
            {
                if (!_captureNextPopup) return false;
                _captureNextPopup = false;
                return true;
            }
        }

        /// <summary>
        /// Whether a child popup is currently being tracked.
        /// </summary>
        public bool HasChildPopup => _childPopup != null;

        /// <summary>
        /// Store a child popup reference (reward display opened after claim).
        /// The overlay stays active and Escape will close this popup first.
        /// </summary>
        public void SetChildPopup(object popup)
        {
            _childPopup = popup;
            Debug.Log("[ATSAccessibility] DeedsOverlay: child popup captured");
        }

        /// <summary>
        /// Clear the child popup reference (called when it's closed externally).
        /// </summary>
        public void ClearChildPopup()
        {
            _childPopup = null;
        }

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Deeds";
        protected override string EmptyMessage => "No categories available";

        protected override int GetItemCount()
        {
            if (Level == 0)
                return _categories.Count;
            else
                return GetCurrentGoals()?.Count ?? 0;
        }

        protected override string GetLabel(int index)
        {
            if (Level == 0)
            {
                if (index >= 0 && index < _categories.Count)
                    return _categories[index].Name;
                return null;
            }
            else
            {
                var goals = GetCurrentGoals();
                if (goals == null || index < 0 || index >= goals.Count) return null;

                var goal = goals[index];
                var description = DeedsReflection.GetGoalDescription(goal.Model);
                string status;

                if (goal.Completed && !goal.Rewarded)
                {
                    status = "ready to collect";
                }
                else if (goal.Completed && goal.Rewarded)
                {
                    status = "completed";
                }
                else
                {
                    status = DeedsReflection.GetGoalProgressText(goal.Model, goal.State);
                }

                // Description already ends with a period from localization
                if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(status))
                    return $"{goal.Name}. {description} {status}";
                else if (!string.IsNullOrEmpty(description))
                    return $"{goal.Name}. {description}";
                else if (!string.IsNullOrEmpty(status))
                    return $"{goal.Name}, {status}";
                else
                    return goal.Name;
            }
        }

        protected override void RefreshData()
        {
            _categories.Clear();

            var allGoals = DeedsReflection.GetAllGoalStates();
            if (allGoals.Count == 0) return;

            // Group goals by category
            var categoryMap = new Dictionary<object, List<GoalEntry>>();
            var readyToCollect = new List<GoalEntry>();

            foreach (var (state, model) in allGoals)
            {
                var category = DeedsReflection.GetGoalCategory(model);
                if (category == null) continue;

                bool completed = DeedsReflection.IsGoalCompleted(state);
                bool rewarded = DeedsReflection.IsGoalRewarded(state);

                var entry = new GoalEntry
                {
                    Name = DeedsReflection.GetGoalName(model),
                    State = state,
                    Model = model,
                    Completed = completed,
                    Rewarded = rewarded
                };

                // Collect claimable goals for synthetic category
                if (completed && !rewarded)
                {
                    readyToCollect.Add(entry);
                }

                if (!categoryMap.ContainsKey(category))
                {
                    categoryMap[category] = new List<GoalEntry>();
                }
                categoryMap[category].Add(entry);
            }

            // Sort categories by order
            var sortedCategories = categoryMap.Keys
                .OrderBy(c => DeedsReflection.GetCategoryOrder(c))
                .ToList();

            // Prepend "Ready to Collect" if there are claimable goals
            if (readyToCollect.Count > 0)
            {
                _categories.Add(new CategoryEntry
                {
                    Name = "Ready to Collect",
                    Goals = readyToCollect
                });
            }

            // Add regular categories
            foreach (var cat in sortedCategories)
            {
                _categories.Add(new CategoryEntry
                {
                    Name = DeedsReflection.GetCategoryName(cat),
                    Goals = categoryMap[cat]
                });
            }
        }

        protected override EnterAction OnEnter(int index)
        {
            if (Level == 0)
            {
                if (index < 0 || index >= _categories.Count) return EnterAction.None;
                var category = _categories[index];
                if (category.Goals.Count == 0)
                {
                    Speech.Say("No goals");
                    return EnterAction.None;
                }
                return EnterAction.DrillDown;
            }
            else
            {
                return EnterAction.Action;
            }
        }

        protected override void OnAction(int index)
        {
            if (Level == 1)
                ActivateCurrentItem(index);
        }

        protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            // If a child popup (reward display) is open, pass Escape to game to close it.
            // Don't clear _childPopup here -- OnPopupHidden will handle cleanup.
            if (_childPopup != null && keyCode == KeyCode.Escape)
            {
                Speech.Say("Rewards popup closed");
                // Pass to game to close the reward popup
                return false;
            }
            return null;
        }

        // Escape passes to game to close popup at both levels
        protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

        protected override string GetOpenAnnouncement()
        {
            if (_categories.Count > 0)
                return $"Deeds. {_categories[0].Name}";
            return $"Deeds. {EmptyMessage}";
        }

        protected override void OnClosed()
        {
            _childPopup = null;
            _captureNextPopup = false;
            _categories.Clear();
        }

        // ========================================
        // CROSS-CATEGORY SEARCH
        // ========================================

        protected override int SearchItemCount
        {
            get
            {
                if (Level == 1)
                    return GetCurrentGoals()?.Count ?? 0;
                // Category level: flat cross-category search across all goals
                return GetFlatGoalCount();
            }
        }

        protected override int SearchCurrentIndex
        {
            get
            {
                if (Level == 1)
                    return CurrentIndex;
                return _indices[0];
            }
        }

        protected override string GetSearchName(int index)
        {
            if (Level == 1)
            {
                var goals = GetCurrentGoals();
                return (goals != null && index >= 0 && index < goals.Count) ? goals[index].Name : null;
            }
            // Category level: flat cross-category search
            return GetFlatGoalName(index);
        }

        protected override void SearchMoveTo(int index)
        {
            if (Level == 1)
            {
                NavigateTo(index);
                return;
            }

            // Category level: resolve flat index to (category, item) and enter items
            ResolveFlatGoalIndex(index, out int catIdx, out int itemIdx);
            _indices[0] = catIdx;
            SetLevel(1);
            _indices[1] = itemIdx;
            AnnounceCurrentItem();
        }

        // ========================================
        // ACTIVATION
        // ========================================

        private void ActivateCurrentItem(int index)
        {
            var goals = GetCurrentGoals();
            if (goals == null || index < 0 || index >= goals.Count) return;

            var goal = goals[index];

            if (goal.Completed && !goal.Rewarded)
            {
                // Claimable goal
                if (DeedsReflection.IsInGame())
                {
                    Speech.Say("Can only collect from the Citadel");
                    return;
                }

                if (DeedsReflection.ClaimGoal(goal.State, goal.Model))
                {
                    goal.Rewarded = true;
                    _captureNextPopup = true;
                    var rewardNames = DeedsReflection.GetRewardNames(goal.Model);
                    if (rewardNames.Length > 0)
                    {
                        Speech.Say($"Collected. Rewards: {string.Join(", ", rewardNames)}");
                    }
                    else
                    {
                        Speech.Say("Collected");
                    }
                }
                else
                {
                    Speech.Say("Cannot collect");
                    SoundManager.PlayFailed();
                }
            }
        }

        // ========================================
        // HELPERS
        // ========================================

        private List<GoalEntry> GetCurrentGoals()
        {
            if (_categories.Count == 0) return null;
            int catIdx = _indices[0];
            if (catIdx < 0 || catIdx >= _categories.Count) return null;
            return _categories[catIdx].Goals;
        }

        /// <summary>
        /// Get the total number of goals across all categories (for flat search indexing).
        /// </summary>
        private int GetFlatGoalCount()
        {
            int count = 0;
            for (int c = 0; c < _categories.Count; c++)
                count += _categories[c].Goals.Count;
            return count;
        }

        /// <summary>
        /// Get a goal name by flat index across all categories.
        /// </summary>
        private string GetFlatGoalName(int flatIndex)
        {
            int remaining = flatIndex;
            for (int c = 0; c < _categories.Count; c++)
            {
                var goals = _categories[c].Goals;
                if (remaining < goals.Count)
                    return goals[remaining].Name;
                remaining -= goals.Count;
            }
            return null;
        }

        /// <summary>
        /// Resolve a flat goal index to (categoryIndex, itemIndex).
        /// </summary>
        private void ResolveFlatGoalIndex(int flatIndex, out int categoryIndex, out int itemIndex)
        {
            int remaining = flatIndex;
            for (int c = 0; c < _categories.Count; c++)
            {
                var goals = _categories[c].Goals;
                if (remaining < goals.Count)
                {
                    categoryIndex = c;
                    itemIndex = remaining;
                    return;
                }
                remaining -= goals.Count;
            }
            categoryIndex = 0;
            itemIndex = 0;
        }
    }
}
