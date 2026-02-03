using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Accessible overlay for the GameResultPopup (victory/defeat screen).
	/// Level 0 = top-level items, Level 1 = sub-items within Section items.
	/// </summary>
	public class GameResultOverlay: MenuBase, IKeyHandler {
		private enum ItemType { ReadOnly, Section, Button }

		private class TopLevelItem {
			public ItemType Type;
			public string Label;
			public Action OnActivate;           // For Button type
			public List<string> SubItems;       // For Section type
		}

		// Data
		private object _popup;
		private List<TopLevelItem> _items = new List<TopLevelItem>();

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		public bool IsActive => IsOpen && IsPopupVisible();

		bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
			ProcessKey(keyCode, modifiers);

		private bool IsPopupVisible() {
			if (_popup == null) return false;
			var mb = _popup as MonoBehaviour;
			if (mb == null) return false;
			return mb.gameObject != null && mb.gameObject.activeSelf;
		}

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Game Result";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			if (Level == 0)
				return _items.Count;

			// Level 1: sub-items of the current top-level Section
			int topIdx = _indices[0];
			if (topIdx >= 0 && topIdx < _items.Count)
				return _items[topIdx].SubItems?.Count ?? 0;
			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _items.Count)
					return _items[index].Label;
				return null;
			}

			// Level 1: sub-item label
			int topIdx = _indices[0];
			if (topIdx >= 0 && topIdx < _items.Count) {
				var subItems = _items[topIdx].SubItems;
				if (subItems != null && index >= 0 && index < subItems.Count)
					return subItems[index];
			}
			return null;
		}

		protected override void RefreshData() {
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

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _items.Count) {
					var item = _items[index];
					if (item.Type == ItemType.Section && item.SubItems != null && item.SubItems.Count > 0)
						return EnterAction.DrillDown;
				}
				return EnterAction.Action;
			}

			// Level 1: no action in sub-items
			return EnterAction.None;
		}

		protected override void OnAction(int index) {
			if (Level != 0) return;
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];

			switch (item.Type) {
				case ItemType.ReadOnly:
					// Re-announce
					AnnounceCurrentItem();
					break;

				case ItemType.Section:
					// Section with no sub-items
					Speech.Say("Empty");
					break;

				case ItemType.Button:
					if (item.OnActivate != null) {
						item.OnActivate();
						SoundManager.PlayButtonClick();
					}
					break;
			}
		}

		protected override EscapeAction OnEscape() {
			if (Level > 0)
				return EscapeAction.GoBack;

			// Pass to game to close popup
			return EscapeAction.PassThrough;
		}

		protected override int SearchItemCount => 0; // No search in this overlay

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count > 0)
				return _items[0].Label;
			return OverlayName;
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void AddSummaryItem() {
			string header = GameResultReflection.GetHeaderText(_popup);
			if (string.IsNullOrEmpty(header)) {
				header = GameResultReflection.HasWon() ? "Victory" : "Defeat";
			}

			string desc = GameResultReflection.GetDescriptionText(_popup);

			// Flavor text comes pre-localized with period, so just use colon separator
			string label = string.IsNullOrEmpty(desc) ? header : $"{header}: {desc}";

			_items.Add(new TopLevelItem {
				Type = ItemType.ReadOnly,
				Label = label
			});
		}

		private void AddProgressionSection() {
			var subItems = new List<string>();

			// XP summary
			int gainedExp = GameResultReflection.GetGainedExp();
			var levelInfo = GameResultReflection.GetLevelInfo();

			string expSummary;
			if (levelInfo.targetExp <= 0 || levelInfo.exp >= levelInfo.targetExp) {
				expSummary = $"Gained {gainedExp} experience, Level {levelInfo.level}, max level";
			} else {
				expSummary = $"Gained {gainedExp} experience, Level {levelInfo.level}, {levelInfo.exp} of {levelInfo.targetExp} to next level";
			}
			subItems.Add(expSummary);

			// Completed goals
			var completedGoals = GameResultReflection.GetCompletedGoals();
			foreach (var goal in completedGoals) {
				subItems.Add($"Completed: {goal}");
			}

			// Meta currencies from field rewards
			var currencies = GameResultReflection.GetMetaCurrencies();
			foreach (var (name, amount) in currencies) {
				subItems.Add($"{name}, {amount}");
			}

			// Stored meta currencies (goods collected during the game)
			var storedCurrencies = GameResultReflection.GetStoredMetaCurrencies();
			foreach (var (name, amount) in storedCurrencies) {
				subItems.Add($"{name}, {amount}");
			}

			// Seal fragments
			int sealFragments = GameResultReflection.GetSealFragments();
			if (sealFragments > 0) {
				subItems.Add($"Seal fragments, {sealFragments}");
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = "Progression",
				SubItems = subItems
			});
		}

		private void AddScoreSection() {
			// Score section only appears if not tutorial
			if (GameResultReflection.IsTutorial()) return;

			var scoreBreakdown = GameResultReflection.GetScoreBreakdown();
			if (scoreBreakdown.Count == 0) return;

			var subItems = new List<string>();

			// Total score first (calculated from already-fetched breakdown to avoid redundant reflection)
			int totalScore = scoreBreakdown.Sum(s => s.Points);
			subItems.Add($"Total score, {totalScore} points");

			// Individual score entries
			foreach (var entry in scoreBreakdown) {
				subItems.Add($"{entry.Label}, {entry.Points} points");
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = "Score",
				SubItems = subItems
			});
		}

		private void AddTutorialRewardsSection() {
			// Tutorial rewards section only appears for tutorials
			if (!GameResultReflection.IsTutorial()) return;

			var rewards = TutorialReflection.GetTutorialRewardsForCurrentBiome();
			if (rewards.Count == 0) return;

			var subItems = new List<string>();
			foreach (var reward in rewards) {
				subItems.Add($"Unlocked: {reward}");
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = "Tutorial Unlocks",
				SubItems = subItems
			});
		}

		private void AddWorldEventSection() {
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
			if (info.Objectives != null) {
				foreach (var (key, value) in info.Objectives) {
					subItems.Add($"{key}, {value}");
				}
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = "World Event",
				SubItems = subItems
			});
		}

		private void AddActionButtons() {
			// Return to world map (always available)
			_items.Add(new TopLevelItem {
				Type = ItemType.Button,
				Label = "Return to world map",
				OnActivate = () => GameResultReflection.ClickMenuButton(_popup)
			});

			// Continue playing (only if available)
			if (GameResultReflection.IsContinueButtonAvailable(_popup)) {
				_items.Add(new TopLevelItem {
					Type = ItemType.Button,
					Label = "Continue playing",
					OnActivate = () => GameResultReflection.ClickContinueButton(_popup)
				});
			}
		}
	}
}
