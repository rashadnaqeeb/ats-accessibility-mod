using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the GoalsPopup (Deeds menu).
    /// Two-level navigation: categories -> goals.
    /// </summary>
    public class DeedsOverlay : IKeyHandler, ISearchable
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
        private bool _isOpen;
        private bool _suspended;
        private bool _captureNextPopup;
        private object _childPopup;
        private bool _focusOnItems;
        private int _currentCategoryIndex;
        private int _currentItemIndex;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Data
        private List<CategoryEntry> _categories = new List<CategoryEntry>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen && !_suspended;

        /// <summary>
        /// Whether the overlay is open but temporarily suspended (child popup active).
        /// </summary>
        public bool IsSuspended => _isOpen && _suspended;

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

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // If a child popup (reward display) is open, pass Escape to game to close it.
            // Don't clear _childPopup here — OnPopupHidden will handle cleanup.
            if (_childPopup != null && keyCode == KeyCode.Escape)
            {
                Speech.Say("Rewards popup closed");
                // Pass to game to close the reward popup
                return false;
            }

            // Centralized search handling
            if (_search.HandleKey(keyCode, modifiers, this))
                return true;

            if (_focusOnItems)
                return ProcessItemKey(keyCode);
            else
                return ProcessCategoryKey(keyCode);
        }

        // ========================================
        // CATEGORY LEVEL KEYS
        // ========================================

        private bool ProcessCategoryKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateCategory(1);
                    return true;

                case KeyCode.Home:
                    if (_categories.Count > 0)
                    {
                        _currentCategoryIndex = 0;
                        AnnounceCategory();
                    }
                    return true;

                case KeyCode.End:
                    if (_categories.Count > 0)
                    {
                        _currentCategoryIndex = _categories.Count - 1;
                        AnnounceCategory();
                    }
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    EnterItems();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // ITEM LEVEL KEYS
        // ========================================

        private bool ProcessItemKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateItem(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateItem(1);
                    return true;

                case KeyCode.Home:
                    {
                        var goals = GetCurrentGoals();
                        if (goals != null && goals.Count > 0)
                        {
                            _currentItemIndex = 0;
                            AnnounceItem();
                        }
                    }
                    return true;

                case KeyCode.End:
                    {
                        var goals = GetCurrentGoals();
                        if (goals != null && goals.Count > 0)
                        {
                            _currentItemIndex = goals.Count - 1;
                            AnnounceItem();
                        }
                    }
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToCategories();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrentItem();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _suspended = false;
            _focusOnItems = false;
            _currentCategoryIndex = 0;
            _currentItemIndex = 0;
            _search.Clear();

            RefreshData();

            if (_categories.Count > 0)
            {
                Speech.Say($"Deeds. {_categories[0].Name}");
            }
            else
            {
                Speech.Say("Deeds. No categories available");
            }

            Debug.Log($"[ATSAccessibility] DeedsOverlay opened, {_categories.Count} categories");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _suspended = false;
            _childPopup = null;
            _captureNextPopup = false;
            _focusOnItems = false;
            _search.Clear();
            _categories.Clear();

            Debug.Log("[ATSAccessibility] DeedsOverlay closed");
        }

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
        /// Whether a child popup is currently being tracked.
        /// </summary>
        public bool HasChildPopup => _childPopup != null;

        /// <summary>
        /// Clear the child popup reference (called when it's closed externally).
        /// </summary>
        public void ClearChildPopup()
        {
            _childPopup = null;
        }

        /// <summary>
        /// Suspend the overlay when a child popup (e.g. reward display) opens on top.
        /// </summary>
        public void Suspend()
        {
            if (!_isOpen) return;
            _suspended = true;
            Debug.Log("[ATSAccessibility] DeedsOverlay suspended");
        }

        /// <summary>
        /// Resume the overlay after a child popup closes.
        /// </summary>
        public void Resume()
        {
            if (!_isOpen) return;
            _suspended = false;
            if (_focusOnItems)
                AnnounceItem();
            else
                AnnounceCategory();
            Debug.Log("[ATSAccessibility] DeedsOverlay resumed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
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

        // ========================================
        // CATEGORY NAVIGATION
        // ========================================

        private void NavigateCategory(int direction)
        {
            if (_categories.Count == 0) return;

            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, _categories.Count);
            AnnounceCategory();
        }

        private void AnnounceCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;
            Speech.Say(_categories[_currentCategoryIndex].Name);
        }

        private void EnterItems()
        {
            if (_categories.Count == 0) return;
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Goals.Count == 0)
            {
                Speech.Say("No goals");
                return;
            }

            _focusOnItems = true;
            _currentItemIndex = 0;
            _search.Clear();
            AnnounceItem();
        }

        // ========================================
        // ITEM NAVIGATION
        // ========================================

        private void NavigateItem(int direction)
        {
            var goals = GetCurrentGoals();
            if (goals == null || goals.Count == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, goals.Count);
            AnnounceItem();
        }

        private void AnnounceItem()
        {
            var goals = GetCurrentGoals();
            if (goals == null || _currentItemIndex < 0 || _currentItemIndex >= goals.Count) return;

            var goal = goals[_currentItemIndex];
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
            string announcement;
            if (!string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(status))
                announcement = $"{goal.Name}. {description} {status}";
            else if (!string.IsNullOrEmpty(description))
                announcement = $"{goal.Name}. {description}";
            else if (!string.IsNullOrEmpty(status))
                announcement = $"{goal.Name}, {status}";
            else
                announcement = goal.Name;

            Speech.Say(announcement);
        }

        private void ReturnToCategories()
        {
            _focusOnItems = false;
            _search.Clear();
            AnnounceCategory();
        }

        // ========================================
        // ACTIVATION
        // ========================================

        private void ActivateCurrentItem()
        {
            var goals = GetCurrentGoals();
            if (goals == null || _currentItemIndex < 0 || _currentItemIndex >= goals.Count) return;

            var goal = goals[_currentItemIndex];

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
        // ISearchable Implementation
        // ========================================

        public int SearchItemCount
        {
            get
            {
                if (_focusOnItems)
                {
                    var goals = GetCurrentGoals();
                    return goals?.Count ?? 0;
                }
                else
                {
                    // Category level: flat cross-category search across all goals
                    return GetFlatGoalCount();
                }
            }
        }

        public int SearchCurrentIndex
        {
            get
            {
                return _focusOnItems ? _currentItemIndex : _currentCategoryIndex;
            }
        }

        public string GetSearchLabel(int index)
        {
            if (_focusOnItems)
            {
                var goals = GetCurrentGoals();
                return (goals != null && index >= 0 && index < goals.Count) ? goals[index].Name : null;
            }
            else
            {
                // Category level: flat cross-category search
                return GetFlatGoalName(index);
            }
        }

        public void SearchMoveTo(int index)
        {
            if (_focusOnItems)
            {
                _currentItemIndex = index;
                AnnounceItem();
            }
            else
            {
                // Category level: resolve flat index to (category, item) and enter items
                ResolveFlatGoalIndex(index, out int catIdx, out int itemIdx);
                _currentCategoryIndex = catIdx;
                _currentItemIndex = itemIdx;
                _focusOnItems = true;
                AnnounceItem();
            }
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

        // ========================================
        // HELPERS
        // ========================================

        private List<GoalEntry> GetCurrentGoals()
        {
            if (_categories.Count == 0) return null;
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return null;
            return _categories[_currentCategoryIndex].Goals;
        }
    }
}
