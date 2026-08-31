using ATSAccessibility.Utils;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Virtual speech-only panel for navigating game stats.
	/// Two-level system: Level 0 = categories, Level 1 = details.
	/// </summary>
	public class StatsPanel: MenuBase {
		/// <summary>
		/// Represents a category in the left panel.
		/// </summary>
		private class Category {
			public string Name { get; set; }
			public string Value { get; set; }
			public List<string> Details { get; set; } = new List<string>();
		}

		private List<Category> _categories = new List<Category>();

		// Compatibility aliases for readability
		private int _currentCategoryIndex { get => _indices[0]; set => _indices[0] = value; }
		private int _currentItemIndex { get => _indices[1]; set => _indices[1] = value; }

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("panel.stats.title");
		protected override string EmptyMessage => Strings.Get("panel.stats.empty");

		protected override int GetItemCount() {
			if (Level == 0)
				return _categories.Count;

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
				return _categories[_currentCategoryIndex].Details.Count;

			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count) {
					var cat = _categories[index];
					return Strings.Get("panel.stats.category", cat.Name, cat.Value);
				}
				return null;
			}

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count) {
				var details = _categories[_currentCategoryIndex].Details;
				if (index >= 0 && index < details.Count)
					return details[index];
			}
			return null;
		}

		protected override string GetSearchName(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count)
					return _categories[index].Name;
				return null;
			}
			return GetLabel(index);
		}

		protected override void RefreshData() {
			_categories.Clear();

			// Reputation - format to 2 decimals, strip trailing zeros
			var rep = StatsReader.GetReputationSummary();
			var repBreakdown = StatsReader.GetReputationBreakdown();
			_categories.Add(new Category {
				Name = Strings.Get("panel.stats.reputation"),
				Value = Strings.Get("panel.stats.reputation_value", rep.current, rep.target),
				Details = repBreakdown
			});

			// Queen's Impatience - format to 2 decimals, strip trailing zeros
			var imp = StatsReader.GetImpatienceSummary();
			var impBreakdown = StatsReader.GetImpatienceBreakdown();
			_categories.Add(new Category {
				Name = Strings.Get("panel.stats.impatience"),
				Value = Strings.Get("panel.stats.impatience_value", imp.current, imp.max),
				Details = impBreakdown
			});

			// Hostility
			var host = StatsReader.GetHostilitySummary();
			var hostBreakdown = StatsReader.GetHostilityBreakdown();
			_categories.Add(new Category {
				Name = Strings.Get("panel.stats.hostility"),
				Value = Strings.Get("panel.stats.hostility_value", host.points, host.level),
				Details = hostBreakdown
			});

			// Per-species Resolve
			var races = StatsReader.GetPresentRaces();
			foreach (var raceName in races) {
				var (resolve, threshold, settling) = StatsReader.GetResolveSummary(raceName);
				var resolveBreakdown = StatsReader.GetResolveBreakdown(raceName);
				int population = StatsReader.GetRaceCount(raceName);

				_categories.Add(new Category {
					Name = Strings.Get("panel.stats.resolve_name", raceName),
					Value = Strings.Get("panel.stats.resolve_value", Mathf.FloorToInt(resolve), threshold, settling, population),
					Details = resolveBreakdown
				});
			}

			Debug.Log($"[ATSAccessibility] Stats panel refreshed: {_categories.Count} categories");
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count
					&& _categories[_currentCategoryIndex].Details.Count > 0)
					return EnterAction.DrillDown;

				Speech.Say(Strings.Get("panel.stats.no_details"));
				return EnterAction.None;
			}
			return EnterAction.None;
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.LeftArrow && Level == 0)
				return false; // Pass to InfoPanelMenu to close child panel
			return null;
		}

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0) return EmptyMessage;
			return GetLabel(0);
		}

		protected override void OnClosed() {
			_categories.Clear();
			if (!IsClosingSilently) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say(Strings.Get("panel.stats.closed", OverlayName));
			}
		}
	}
}
