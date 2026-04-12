using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the Daily Expedition (Daily Challenge) popup.
	/// Provides flat list navigation with informational items, submenus for
	/// difficulty selection and modifiers, and embark button.
	///
	/// Uses MenuBase Level 0 for the main list only. Submenus (difficulty,
	/// modifiers) are handled entirely via _submenuMode and HandleSpecialKey,
	/// which intercepts ALL keys when a submenu is active.
	/// </summary>
	public class DailyExpeditionOverlay: MenuBase {
		private enum ItemType {
			Biome,
			TimeLeft,
			Races,
			EmbarkGoods,
			EmbarkEffects,
			Modifiers,       // Interactive - Right arrow opens modifiers submenu
			SeasonalEffects,
			Rewards,
			Completed,
			Difficulty,      // Interactive - Enter opens difficulty submenu
			Embark           // Interactive - Enter triggers embark
		}

		private enum SubmenuMode {
			None,
			Difficulty,
			Modifiers
		}

		// Data
		private object _popup;
		private List<(ItemType type, string text)> _items = new List<(ItemType, string)>();

		// Submenu state
		private SubmenuMode _submenuMode = SubmenuMode.None;
		private int _submenuIndex;
		private readonly TypeAheadSearch _submenuSearch = new TypeAheadSearch();
		private readonly SubmenuSearchable _submenuSearchable;

		// Difficulty data
		private List<object> _difficulties = new List<object>();

		// Modifiers data (name, description)
		private List<(string name, string description)> _modifiers = new List<(string, string)>();

		public DailyExpeditionOverlay() {
			_submenuSearchable = new SubmenuSearchable(this);
		}

		// ========================================
		// MenuBase Overrides
		// ========================================

		protected override string OverlayName => Strings.Get("common.daily_expedition");

		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			return index >= 0 && index < _items.Count ? _items[index].text : null;
		}

		protected override void RefreshData() {
			_items.Clear();

			var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);

			// Build static items
			BuildStaticItems();

			// Build difficulty-dependent items
			BuildDifficultyDependentItems(currentDifficulty);

			// Add interactive items at the end
			string diffName = currentDifficulty != null
				? DailyExpeditionReflection.GetDifficultyDisplayName(currentDifficulty)
				: Strings.Get("common.unknown");
			_items.Add((ItemType.Difficulty, Strings.Get("overlay.daily.difficulty", diffName)));
			_items.Add((ItemType.Embark, Strings.Get("common.embark")));

			Debug.Log($"[ATSAccessibility] DailyExpeditionOverlay: Built {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];

			switch (item.type) {
				case ItemType.Difficulty:
					OpenDifficultySubmenu();
					break;

				case ItemType.Modifiers:
					OpenModifiersSubmenu();
					break;

				case ItemType.Embark:
					TriggerEmbark();
					break;

				default:
					// Non-interactive item - just re-announce
					AnnounceCurrentItem();
					break;
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// If submenu is active, intercept ALL keys
			if (_submenuMode != SubmenuMode.None)
				return ProcessSubmenuKey(keyCode, modifiers);

			// Main list special keys
			if (keyCode == KeyCode.RightArrow) {
				if (CurrentIndex >= 0 && CurrentIndex < _items.Count &&
					_items[CurrentIndex].type == ItemType.Modifiers) {
					OpenModifiersSubmenu();
					return true;
				}
				return true; // Consume (no other Right action)
			}

			if (keyCode == KeyCode.Escape) {
				// Pass to game to close popup
				return false;
			}

			return null; // Standard nav for Up/Down/Home/End/Enter
		}

		protected override bool CanDrillDown(int index) => false;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count > 0)
				return Strings.Get("overlay.daily.open", _items[0].text);
			return Strings.Get("common.daily_expedition");
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
			_difficulties.Clear();
			_modifiers.Clear();
			_submenuMode = SubmenuMode.None;
			_submenuIndex = 0;
			_submenuSearch.Clear();
		}

		protected override int SearchItemCount =>
			_submenuMode == SubmenuMode.None ? _items.Count : 0;

		protected override string GetSearchName(int index) {
			return index >= 0 && index < _items.Count ? _items[index].text : null;
		}

		// ========================================
		// Submenu Key Processing
		// ========================================

		private bool ProcessSubmenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_submenuSearch.HandleKey(keyCode, modifiers, _submenuSearchable))
				return true;

			switch (keyCode) {
				case KeyCode.DownArrow:
					NavigateSubmenu(1);
					return true;

				case KeyCode.UpArrow:
					NavigateSubmenu(-1);
					return true;

				case KeyCode.Home: {
						int count = GetSubmenuCount();
						if (count > 0) {
							_submenuIndex = 0;
							AnnounceSubmenuItem();
						}
					}
					return true;

				case KeyCode.End: {
						int count = GetSubmenuCount();
						if (count > 0) {
							_submenuIndex = count - 1;
							AnnounceSubmenuItem();
						}
					}
					return true;

				case KeyCode.LeftArrow:
					// Exit submenu (for modifiers)
					if (_submenuMode == SubmenuMode.Modifiers) {
						CloseSubmenu(announce: true);
						return true;
					}
					return true;

				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (_submenuMode == SubmenuMode.Difficulty) {
						SelectDifficulty();
					} else if (_submenuMode == SubmenuMode.Modifiers) {
						// Re-announce current modifier
						AnnounceSubmenuItem();
					}
					return true;

				case KeyCode.Escape:
					CloseSubmenu(announce: true);
					return true;

				default:
					// Consume all other keys while submenu is open
					return true;
			}
		}

		// ========================================
		// Submenu Navigation (shared)
		// ========================================

		private void NavigateSubmenu(int direction) {
			int count = GetSubmenuCount();
			if (count == 0) return;

			_submenuIndex = NavigationUtils.WrapIndex(_submenuIndex, direction, count);
			AnnounceSubmenuItem();
		}

		private int GetSubmenuCount() {
			switch (_submenuMode) {
				case SubmenuMode.Difficulty:
					return _difficulties.Count;
				case SubmenuMode.Modifiers:
					return _modifiers.Count;
				default:
					return 0;
			}
		}

		private void AnnounceSubmenuItem() {
			switch (_submenuMode) {
				case SubmenuMode.Difficulty:
					AnnounceDifficultyItem();
					break;
				case SubmenuMode.Modifiers:
					AnnounceModifierItem();
					break;
			}
		}

		private void CloseSubmenu(bool announce) {
			_submenuMode = SubmenuMode.None;
			_submenuSearch.Clear();

			if (announce) {
				// Re-announce current main list item
				AnnounceCurrentItem();
			}

			Debug.Log("[ATSAccessibility] Submenu closed");
		}

		// ========================================
		// Submenu ISearchable Adapter
		// ========================================

		private class SubmenuSearchable: ISearchable {
			private readonly DailyExpeditionOverlay _owner;

			public SubmenuSearchable(DailyExpeditionOverlay owner) { _owner = owner; }

			public int SearchItemCount => _owner.GetSubmenuCount();

			public int SearchCurrentIndex => _owner._submenuIndex;

			public string GetSearchLabel(int index) {
				switch (_owner._submenuMode) {
					case SubmenuMode.Difficulty:
						return index < _owner._difficulties.Count
							? DailyExpeditionReflection.GetDifficultyDisplayName(_owner._difficulties[index])
							: null;
					case SubmenuMode.Modifiers:
						return index < _owner._modifiers.Count
							? _owner._modifiers[index].name
							: null;
					default:
						return null;
				}
			}

			public void SearchMoveTo(int index) {
				_owner._submenuIndex = index;
				_owner.AnnounceSubmenuItem();
			}
		}

		// ========================================
		// Difficulty Submenu
		// ========================================

		private void OpenDifficultySubmenu() {
			_difficulties = DailyExpeditionReflection.GetAvailableDifficulties(_popup);
			if (_difficulties.Count == 0) {
				Speech.Say(Strings.Get("common.no_difficulties_available"));
				return;
			}

			_submenuMode = SubmenuMode.Difficulty;
			_submenuSearch.Clear();

			// Find current difficulty index
			var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);
			int currentIdx = DailyExpeditionReflection.GetDifficultyIndex(currentDifficulty);
			_submenuIndex = 0;

			for (int i = 0; i < _difficulties.Count; i++) {
				if (DailyExpeditionReflection.GetDifficultyIndex(_difficulties[i]) == currentIdx) {
					_submenuIndex = i;
					break;
				}
			}

			SoundManager.PlayButtonClick();
			AnnounceDifficultyItem();
			Debug.Log($"[ATSAccessibility] Difficulty submenu opened, {_difficulties.Count} options");
		}

		private void AnnounceDifficultyItem() {
			if (_submenuIndex < 0 || _submenuIndex >= _difficulties.Count) return;

			var difficulty = _difficulties[_submenuIndex];
			string name = DailyExpeditionReflection.GetDifficultyDisplayName(difficulty);

			// Check if this is the current difficulty
			var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);
			int currentIdx = DailyExpeditionReflection.GetDifficultyIndex(currentDifficulty);
			int thisIdx = DailyExpeditionReflection.GetDifficultyIndex(difficulty);

			if (thisIdx == currentIdx) {
				Speech.Say(Strings.Get("overlay.daily.diff.current", name));
			} else {
				Speech.Say(name);
			}
		}

		private void SelectDifficulty() {
			if (_submenuIndex < 0 || _submenuIndex >= _difficulties.Count) return;

			var selectedDifficulty = _difficulties[_submenuIndex];

			if (DailyExpeditionReflection.SetDifficulty(_popup, selectedDifficulty)) {
				SoundManager.PlayButtonClick();
				_submenuMode = SubmenuMode.None;
				_submenuSearch.Clear();

				// Rebuild affected items
				RefreshDifficultyDependentItems();

				// Announce selected difficulty
				string diffName = DailyExpeditionReflection.GetDifficultyDisplayName(selectedDifficulty);
				Speech.Say(Strings.Get("overlay.daily.diff.selected", diffName));

				Debug.Log($"[ATSAccessibility] Difficulty changed to {diffName}");
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("overlay.daily.diff.could_not"));
			}
		}

		// ========================================
		// Modifiers Submenu
		// ========================================

		private void OpenModifiersSubmenu() {
			_modifiers = DailyExpeditionReflection.GetModifiersDetailed(_popup);
			if (_modifiers.Count == 0) {
				Speech.Say(Strings.Get("overlay.daily.no_modifiers"));
				return;
			}

			_submenuMode = SubmenuMode.Modifiers;
			_submenuSearch.Clear();
			_submenuIndex = 0;

			AnnounceModifierItem();
			Debug.Log($"[ATSAccessibility] Modifiers submenu opened, {_modifiers.Count} modifiers");
		}

		private void AnnounceModifierItem() {
			if (_submenuIndex < 0 || _submenuIndex >= _modifiers.Count) return;

			var (name, description) = _modifiers[_submenuIndex];

			if (!string.IsNullOrEmpty(description)) {
				Speech.Say(Strings.Get("overlay.daily.modifier_with_desc", name, description));
			} else {
				Speech.Say(name);
			}
		}

		// ========================================
		// Embark
		// ========================================

		private void TriggerEmbark() {
			if (DailyExpeditionReflection.TriggerEmbark(_popup)) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("common.embarking"));
				Debug.Log("[ATSAccessibility] Embark triggered");
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("common.could_not_embark"));
			}
		}

		// ========================================
		// Data Building
		// ========================================

		private void BuildStaticItems() {
			// Biome
			string biome = DailyExpeditionReflection.GetBiomeName(_popup);
			_items.Add((ItemType.Biome, Strings.Get("overlay.daily.biome", biome)));

			// Time left
			string timeLeft = DailyExpeditionReflection.GetTimeLeft(_popup);
			_items.Add((ItemType.TimeLeft, Strings.Get("overlay.daily.time_left", timeLeft)));

			// Races
			var races = DailyExpeditionReflection.GetRaces(_popup);
			if (races.Count > 0) {
				_items.Add((ItemType.Races, Strings.Get("overlay.daily.races", string.Join(", ", races))));
			}

			// Embark goods
			var goods = DailyExpeditionReflection.GetEmbarkGoods(_popup);
			if (goods.Count > 0) {
				_items.Add((ItemType.EmbarkGoods, Strings.Get("overlay.daily.embark_goods", string.Join(", ", goods))));
			}

			// Embark effects
			var effects = DailyExpeditionReflection.GetEmbarkEffects(_popup);
			if (effects.Count > 0) {
				_items.Add((ItemType.EmbarkEffects, Strings.Get("overlay.daily.embark_effects", string.Join(", ", effects))));
			}

			// Modifiers (with count, interactive submenu)
			var modifiers = DailyExpeditionReflection.GetModifiers(_popup);
			if (modifiers.Count > 0) {
				_items.Add((ItemType.Modifiers, Strings.Get("overlay.daily.modifiers", modifiers.Count)));
			}
		}

		private void BuildDifficultyDependentItems(object difficulty) {
			// Cache completed status (used in multiple places)
			bool completed = DailyExpeditionReflection.IsCompleted(_popup);

			// Seasonal effects counts and magnitude
			var (positive, negative) = DailyExpeditionReflection.GetSeasonalEffectsCounts(difficulty);
			string magnitude = DailyExpeditionReflection.GetEffectsMagnitude(difficulty);
			if (positive > 0 || negative > 0) {
				string effectsText = Strings.Get("overlay.daily.seasonal", positive, negative);
				if (!string.IsNullOrEmpty(magnitude)) {
					effectsText += Strings.Get("overlay.daily.seasonal.magnitude", magnitude);
				}
				_items.Add((ItemType.SeasonalEffects, effectsText));
			}

			// Rewards (affected by difficulty multiplier)
			var rewards = DailyExpeditionReflection.GetRewards(_popup);
			if (rewards.Count > 0) {
				_items.Add((ItemType.Rewards, Strings.Get("overlay.daily.rewards", string.Join(", ", rewards))));
			} else {
				// No rewards if already done today at this difficulty
				if (completed) {
					_items.Add((ItemType.Rewards, Strings.Get("overlay.daily.rewards.none")));
				}
			}

			// Completed status
			_items.Add((ItemType.Completed, Strings.Get("overlay.daily.completed", Strings.Get(completed ? "overlay.daily.yes" : "overlay.daily.no"))));
		}

		private void RefreshDifficultyDependentItems() {
			// Find and update the difficulty-dependent items
			var difficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);

			// Remove old difficulty-dependent items
			_items.RemoveAll(item =>
				item.type == ItemType.SeasonalEffects ||
				item.type == ItemType.Rewards ||
				item.type == ItemType.Completed ||
				item.type == ItemType.Difficulty);

			// Also remove Embark (we'll re-add it)
			_items.RemoveAll(item => item.type == ItemType.Embark);

			// Re-add difficulty-dependent items
			BuildDifficultyDependentItems(difficulty);

			// Re-add interactive items
			string diffName = difficulty != null
				? DailyExpeditionReflection.GetDifficultyDisplayName(difficulty)
				: Strings.Get("common.unknown");
			_items.Add((ItemType.Difficulty, Strings.Get("overlay.daily.difficulty", diffName)));
			_items.Add((ItemType.Embark, Strings.Get("common.embark")));

			// Update current index to point to difficulty item
			for (int i = 0; i < _items.Count; i++) {
				if (_items[i].type == ItemType.Difficulty) {
					CurrentIndex = i;
					break;
				}
			}
		}
	}
}
