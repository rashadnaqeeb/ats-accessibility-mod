using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the GameResultPopup (victory/defeat screen).
	/// Level 0 = top-level items, Level 1 = sub-items within Section items.
	/// </summary>
	public class GameResultOverlay: MenuBase {
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

		public override bool IsActive => IsOpen && IsPopupVisible();

		private bool IsPopupVisible() {
			if (_popup == null) return false;
			var mb = _popup as MonoBehaviour;
			if (mb == null) return false;
			return mb.gameObject != null && mb.gameObject.activeSelf;
		}

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.game_result.title");
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
					Speech.Say(Strings.Get("common.empty"));
					break;

				case ItemType.Button:
					if (item.OnActivate != null) {
						item.OnActivate();
						SoundManager.PlayButtonClick();
						AnnounceDecisionIfShown();
					}
					break;
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// Continue playing opens a child DecisionPopup ("Are you sure?") inside the
			// GameResultPopup. It never fires its own popup-shown event, so we intercept
			// input here while it is visible and route Enter/Escape to its buttons.
			if (!GameResultReflection.IsDecisionPopupVisible(_popup))
				return null;

			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					GameResultReflection.ClickDecisionConfirm(_popup);
					return true;
				case KeyCode.Escape:
					GameResultReflection.ClickDecisionCancel(_popup);
					Speech.Say(Strings.Get("common.cancelled"));
					return true;
				default:
					return true;
			}
		}

		private void AnnounceDecisionIfShown() {
			if (!GameResultReflection.IsDecisionPopupVisible(_popup)) return;

			var (title, desc) = GameResultReflection.GetDecisionPopupTexts(_popup);
			if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(desc)) return;

			string instructions = Strings.Get("dialog.confirm.instructions");
			Speech.Say($"{title}. {desc}. {instructions}");
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
				header = Strings.Get(GameResultReflection.HasWon() ? "overlay.game_result.victory" : "overlay.game_result.defeat");
			}

			string desc = GameResultReflection.GetDescriptionText(_popup);

			// Flavor text comes pre-localized with period, so just use colon separator
			string label = string.IsNullOrEmpty(desc) ? header : Strings.Get("overlay.game_result.header_with_desc", header, desc);

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
				expSummary = Strings.Get("overlay.game_result.xp_max", gainedExp, levelInfo.level);
			} else {
				expSummary = Strings.Get("overlay.game_result.xp_progress", gainedExp, levelInfo.level, levelInfo.exp, levelInfo.targetExp);
			}
			subItems.Add(expSummary);

			// Completed goals
			var completedGoals = GameResultReflection.GetCompletedGoals();
			foreach (var goal in completedGoals) {
				subItems.Add(Strings.Get("overlay.game_result.completed", goal));
			}

			// Meta currencies from field rewards
			var currencies = GameResultReflection.GetMetaCurrencies();
			foreach (var (name, amount) in currencies) {
				subItems.Add(Strings.Get("overlay.game_result.currency", name, amount));
			}

			// Stored meta currencies (goods collected during the game)
			var storedCurrencies = GameResultReflection.GetStoredMetaCurrencies();
			foreach (var (name, amount) in storedCurrencies) {
				subItems.Add(Strings.Get("overlay.game_result.currency", name, amount));
			}

			// Seal fragments
			int sealFragments = GameResultReflection.GetSealFragments();
			if (sealFragments > 0) {
				subItems.Add(Strings.Get("overlay.game_result.seal_fragments", sealFragments));
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = Strings.Get("overlay.game_result.section.progression"),
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
			subItems.Add(Strings.Get("overlay.game_result.total_score", totalScore));

			// Individual score entries
			foreach (var entry in scoreBreakdown) {
				subItems.Add(Strings.Get("overlay.game_result.score_entry", entry.Label, entry.Points));
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = Strings.Get("overlay.game_result.section.score"),
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
				subItems.Add(Strings.Get("overlay.game_result.unlocked", reward));
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = Strings.Get("overlay.game_result.section.tutorial"),
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
			string resultText = Strings.Get(info.Completed ? "overlay.game_result.result_completed" : "overlay.game_result.result_failed");
			subItems.Add(resultText);

			// Objectives
			if (info.Objectives != null) {
				foreach (var (key, value) in info.Objectives) {
					subItems.Add(Strings.Get("overlay.game_result.objective", key, value));
				}
			}

			_items.Add(new TopLevelItem {
				Type = ItemType.Section,
				Label = Strings.Get("common.world_event"),
				SubItems = subItems
			});
		}

		private void AddActionButtons() {
			// Return to world map (always available)
			_items.Add(new TopLevelItem {
				Type = ItemType.Button,
				Label = Strings.Get("overlay.game_result.return"),
				OnActivate = () => GameResultReflection.ClickMenuButton(_popup)
			});

			// Continue playing (only if available)
			if (GameResultReflection.IsContinueButtonAvailable(_popup)) {
				_items.Add(new TopLevelItem {
					Type = ItemType.Button,
					Label = Strings.Get("overlay.game_result.continue"),
					OnActivate = () => GameResultReflection.ClickContinueButton(_popup)
				});
			}
		}
	}
}
