using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating game stats.
    /// Two-panel system: left panel has categories, right panel has details.
    /// </summary>
    public class StatsPanel
    {
        /// <summary>
        /// Represents a category in the left panel.
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Race { get; set; }  // For resolve categories, null otherwise
            public List<string> Details { get; set; } = new List<string>();
        }

        private bool _isOpen = false;
        private List<Category> _categories = new List<Category>();
        private int _currentCategoryIndex = 0;
        private int _currentDetailIndex = 0;
        private bool _focusOnDetails = false;

        /// <summary>
        /// Whether the stats panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the stats panel and announce the first category.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            // Build category list
            RefreshCategories();

            if (_categories.Count == 0)
            {
                Speech.Say("No stats available");
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentDetailIndex = 0;
            _focusOnDetails = false;

            AnnounceCurrentCategory();
            Debug.Log("[ATSAccessibility] Stats panel opened");
        }

        /// <summary>
        /// Close the stats panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _categories.Clear();
            Speech.Say("Stats panel closed");
            Debug.Log("[ATSAccessibility] Stats panel closed");
        }

        /// <summary>
        /// Navigate categories (left panel) with Up/Down.
        /// </summary>
        public void NavigateCategory(int direction)
        {
            if (!_isOpen || _categories.Count == 0) return;

            // If on details, go back to categories first
            if (_focusOnDetails)
            {
                _focusOnDetails = false;
                AnnounceCurrentCategory();
                return;
            }

            _currentCategoryIndex += direction;

            // Wrap around
            if (_currentCategoryIndex < 0)
                _currentCategoryIndex = _categories.Count - 1;
            else if (_currentCategoryIndex >= _categories.Count)
                _currentCategoryIndex = 0;

            _currentDetailIndex = 0;  // Reset detail index when changing category
            AnnounceCurrentCategory();
        }

        /// <summary>
        /// Navigate details (right panel) with Up/Down when in details mode.
        /// </summary>
        public void NavigateDetail(int direction)
        {
            if (!_isOpen || !_focusOnDetails) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0) return;

            _currentDetailIndex += direction;

            // Wrap around
            if (_currentDetailIndex < 0)
                _currentDetailIndex = category.Details.Count - 1;
            else if (_currentDetailIndex >= category.Details.Count)
                _currentDetailIndex = 0;

            AnnounceCurrentDetail();
        }

        /// <summary>
        /// Enter details mode (Enter key).
        /// </summary>
        public void EnterDetails()
        {
            if (!_isOpen) return;

            var category = _categories[_currentCategoryIndex];

            if (category.Details.Count == 0)
            {
                Speech.Say("No additional details");
                return;
            }

            _focusOnDetails = true;
            _currentDetailIndex = 0;
            AnnounceCurrentDetail();
        }

        /// <summary>
        /// Return to categories (Left arrow).
        /// </summary>
        public void ReturnToCategories()
        {
            if (!_isOpen) return;

            if (_focusOnDetails)
            {
                _focusOnDetails = false;
                AnnounceCurrentCategory();
            }
        }

        /// <summary>
        /// Process a key event for the stats panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_focusOnDetails)
                        NavigateDetail(-1);
                    else
                        NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_focusOnDetails)
                        NavigateDetail(1);
                    else
                        NavigateCategory(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    EnterDetails();
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToCategories();
                    return true;

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return true;  // Consume all other keys while panel is open
            }
        }

        /// <summary>
        /// Refresh the category list with current game data.
        /// </summary>
        private void RefreshCategories()
        {
            _categories.Clear();

            // Reputation
            var rep = StatsReader.GetReputationSummary();
            var repBreakdown = StatsReader.GetReputationBreakdown();
            _categories.Add(new Category
            {
                Name = "Reputation",
                Value = $"{Mathf.FloorToInt(rep.current)} of {rep.target}",
                Details = repBreakdown
            });

            // Queen's Impatience
            var imp = StatsReader.GetImpatienceSummary();
            // No detailed breakdown available for impatience
            _categories.Add(new Category
            {
                Name = "Queen's Impatience",
                Value = $"{Mathf.FloorToInt(imp.current)} of {imp.max}",
                Details = new List<string>()
            });

            // Hostility
            var host = StatsReader.GetHostilitySummary();
            var hostBreakdown = StatsReader.GetHostilityBreakdown();
            _categories.Add(new Category
            {
                Name = "Hostility",
                Value = $"{host.points} points, level {host.level}",
                Details = hostBreakdown
            });

            // Species Resolve (one per present species)
            var races = StatsReader.GetPresentRaces();
            foreach (var race in races)
            {
                var (resolve, threshold) = StatsReader.GetResolveSummary(race);
                var resolveBreakdown = StatsReader.GetResolveBreakdown(race);

                _categories.Add(new Category
                {
                    Name = $"{race} Resolve",
                    Value = $"{Mathf.FloorToInt(resolve)} of {threshold}",
                    Race = race,
                    Details = resolveBreakdown
                });
            }

            Debug.Log($"[ATSAccessibility] Stats panel refreshed: {_categories.Count} categories");
        }

        /// <summary>
        /// Announce the current category (left panel).
        /// </summary>
        private void AnnounceCurrentCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            string message = $"{category.Name}, {category.Value}";

            // Add indicator if details are available
            if (category.Details.Count > 0)
            {
                message += $". {category.Details.Count} details, press Enter for breakdown";
            }

            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Category {_currentCategoryIndex + 1}/{_categories.Count}: {message}");
        }

        /// <summary>
        /// Announce the current detail (right panel).
        /// </summary>
        private void AnnounceCurrentDetail()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex < 0 || _currentDetailIndex >= category.Details.Count) return;

            string detail = category.Details[_currentDetailIndex];
            string position = $"{_currentDetailIndex + 1} of {category.Details.Count}";

            Speech.Say($"{detail}. {position}");
            Debug.Log($"[ATSAccessibility] Detail {position}: {detail}");
        }
    }
}
