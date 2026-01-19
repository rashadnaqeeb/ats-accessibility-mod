using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating game stats.
    /// Two-panel system: left panel has categories, right panel has details.
    /// </summary>
    public class StatsPanel : TwoLevelPanel
    {
        /// <summary>
        /// Represents a category in the left panel.
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public List<string> Details { get; set; } = new List<string>();
        }

        private List<Category> _categories = new List<Category>();

        // ========================================
        // ABSTRACT MEMBER IMPLEMENTATIONS
        // ========================================

        protected override string PanelName => "Stats panel";
        protected override string EmptyMessage => "No stats available";
        protected override string NoItemsMessage => "No additional details";
        protected override int CategoryCount => _categories.Count;
        protected override int CurrentItemCount =>
            _currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count
                ? _categories[_currentCategoryIndex].Details.Count
                : 0;

        protected override void RefreshData()
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
                var (resolve, threshold, settling) = StatsReader.GetResolveSummary(race);
                var resolveBreakdown = StatsReader.GetResolveBreakdown(race);

                _categories.Add(new Category
                {
                    Name = $"{race} Resolve",
                    Value = $"{Mathf.FloorToInt(resolve)} of {threshold}, drifting towards {settling}",
                    Details = resolveBreakdown
                });
            }

            Debug.Log($"[ATSAccessibility] Stats panel refreshed: {_categories.Count} categories");
        }

        protected override void ClearData()
        {
            _categories.Clear();
        }

        protected override void AnnounceCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            string message = $"{category.Name}, {category.Value}";

            // Add indicator if details are available
            int detailCount = category.Details.Count;
            if (detailCount > 0)
            {
                string detailWord = detailCount == 1 ? "detail" : "details";
                message += $". {detailCount} {detailWord}";
            }

            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Category {_currentCategoryIndex + 1}/{_categories.Count}: {message}");
        }

        protected override void AnnounceItem()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentItemIndex < 0 || _currentItemIndex >= category.Details.Count) return;

            string detail = category.Details[_currentItemIndex];
            Speech.Say(detail);
            Debug.Log($"[ATSAccessibility] Detail: {detail}");
        }
    }
}
