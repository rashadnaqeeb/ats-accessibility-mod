using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the PerkCrafterPopup (Cornerstone Forge).
	/// Level 0: Main menu (7 crafting items) or finished mode (dialogue + crafted perks).
	/// Level 1: Submenu for hook/positive/negative selection.
	/// Level 2: Name editing (handled entirely in HandleSpecialKey).
	/// </summary>
	public class PerkCrafterOverlay: MenuBase {
		private enum MenuItem {
			Dialogue = 0,
			Shards = 1,
			Hook = 2,
			Positive = 3,
			Negative = 4,
			Result = 5,
			Craft = 6
		}

		private MenuItem _activeSubmenu;
		private bool _isFinishedMode;

		private List<PerkCrafterReflection.HookOption> _hookOptions;
		private List<PerkCrafterReflection.EffectOption> _positiveOptions;
		private List<PerkCrafterReflection.EffectOption> _negativeOptions;
		private List<PerkCrafterReflection.CraftedPerkInfo> _craftedPerks;

		private StringBuilder _nameBuffer;
		private bool _nameEditing;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.cornerstone_forge");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			switch (Level) {
				case 0:
					if (_isFinishedMode)
						return 1 + (_craftedPerks?.Count ?? 0);
					return 7;
				case 1:
					return GetSubmenuItemCount();
				default:
					return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0:
					return _isFinishedMode ? GetFinishedLabel(index) : GetMainMenuLabel(index);
				case 1:
					return GetSubmenuLabel(index);
				default:
					return null;
			}
		}

		protected override void RefreshData() {
			_isFinishedMode = PerkCrafterReflection.HasUsedAllCharges();

			if (_isFinishedMode) {
				_craftedPerks = PerkCrafterReflection.GetCraftedPerks();
			} else {
				// Repair any corrupted tier indices before reading options
				PerkCrafterReflection.RepairTierIndices();
				_hookOptions = PerkCrafterReflection.GetHookOptions();
				_positiveOptions = PerkCrafterReflection.GetPositiveOptions();
				_negativeOptions = PerkCrafterReflection.GetNegativeOptions();
			}
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					if (_isFinishedMode) return EnterAction.None;
					var item = (MenuItem)index;
					if (item == MenuItem.Hook || item == MenuItem.Positive || item == MenuItem.Negative)
						return EnterAction.DrillDown;
					return EnterAction.Action;
				case 1:
					return EnterAction.Action;
				default:
					return EnterAction.None;
			}
		}

		protected override bool CanDrillDown(int index) {
			if (Level != 0 || _isFinishedMode) return false;
			var item = (MenuItem)index;
			return item == MenuItem.Hook || item == MenuItem.Positive || item == MenuItem.Negative;
		}

		protected override void OnDrillDown(int index) {
			var item = (MenuItem)index;
			_activeSubmenu = item;
		}

		protected override void OnAction(int index) {
			if (Level == 0) {
				ActivateMainMenuItem(index);
			} else if (Level == 1) {
				SelectSubmenuItem();
			}
		}

		protected override void OnGoBack() {
			_activeSubmenu = MenuItem.Dialogue;
		}

		protected override EscapeAction OnEscape() {
			if (Level > 0) return EscapeAction.GoBack;
			// Pass to game to close popup
			return EscapeAction.PassThrough;
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (Level == 2)
				return ProcessNameEditKey(keyCode, modifiers);

			if (Level == 0 && _isFinishedMode) {
				if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter ||
					keyCode == KeyCode.RightArrow) {
					AnnounceCurrentItem();
					return true;
				}
			}

			if (Level == 1) {
				if (keyCode == KeyCode.RightArrow) {
					SelectSubmenuItem();
					return true;
				}
			}

			return null;
		}

		protected override string GetOpenAnnouncement() {
			if (_isFinishedMode) {
				string msg = Strings.Get("overlay.perk_crafter.open.finished", _craftedPerks?.Count ?? 0);
				if (_craftedPerks != null && _craftedPerks.Count > 0) {
					string first = GetFinishedLabel(0);
					if (!string.IsNullOrEmpty(first))
						msg += Strings.Get("overlay.perk_crafter.open.finished_first", first);
				}
				return msg;
			}

			string label = GetMainMenuLabel(0);
			if (!string.IsNullOrEmpty(label))
				return Strings.Get("overlay.perk_crafter.open.prefix", label);
			return Strings.Get("common.cornerstone_forge");
		}

		protected override void AnnounceCurrentItem() {
			int count = GetItemCount();
			if (count == 0) return;

			if (Level == 0) {
				if (_isFinishedMode)
					AnnounceFinishedItem();
				else
					AnnounceMainMenuItem();
			} else if (Level == 1) {
				AnnounceSubmenuItem();
			}
		}

		protected override void OnClosed() {
			_nameEditing = false;
			ClearData();
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override int SearchItemCount {
			get {
				if (Level == 1) return GetSubmenuItemCount();
				return 0;
			}
		}

		protected override int SearchCurrentIndex => Level == 1 ? CurrentIndex : 0;

		protected override string GetSearchName(int index) {
			if (Level != 1) return null;

			switch (_activeSubmenu) {
				case MenuItem.Hook:
					if (_hookOptions != null && index >= 0 && index < _hookOptions.Count)
						return _hookOptions[index].Description;
					break;
				case MenuItem.Positive:
					if (_positiveOptions != null && index >= 0 && index < _positiveOptions.Count)
						return _positiveOptions[index].Description;
					break;
				case MenuItem.Negative:
					if (index == 0) return Strings.Get("common.none");
					int negIdx = index - 1;
					if (_negativeOptions != null && negIdx >= 0 && negIdx < _negativeOptions.Count)
						return _negativeOptions[negIdx].Description;
					break;
			}
			return null;
		}

		// ========================================
		// MAIN MENU LABELS (Level 0)
		// ========================================

		private string GetMainMenuLabel(int index) {
			var item = (MenuItem)index;
			switch (item) {
				case MenuItem.Dialogue: {
						var dialogue = PerkCrafterReflection.GetNpcDialogue();
						return !string.IsNullOrEmpty(dialogue)
							? Strings.Get("overlay.perk_crafter.dialogue_with", dialogue)
							: Strings.Get("overlay.perk_crafter.dialogue_empty");
					}
				case MenuItem.Shards: {
						int usesLeft = PerkCrafterReflection.GetUsesLeft();
						int total = PerkCrafterReflection.GetTotalCharges();
						int crafted = PerkCrafterReflection.GetCraftedPerksCount();
						return usesLeft > 0
							? Strings.Get("overlay.perk_crafter.crafting", crafted + 1, total)
							: Strings.Get("overlay.perk_crafter.all_used");
					}
				case MenuItem.Hook: {
						var currentHook = PerkCrafterReflection.GetCurrentHook();
						return currentHook != null
							? Strings.Get("overlay.perk_crafter.hook", currentHook.Description)
							: Strings.Get("overlay.perk_crafter.hook.none");
					}
				case MenuItem.Positive: {
						var currentPositive = PerkCrafterReflection.GetCurrentPositive();
						return currentPositive != null
							? Strings.Get("overlay.perk_crafter.positive", currentPositive.Description)
							: Strings.Get("overlay.perk_crafter.positive.none");
					}
				case MenuItem.Negative: {
						int negIndex = PerkCrafterReflection.GetPickedNegativeIndex();
						if (negIndex < 0)
							return Strings.Get("overlay.perk_crafter.negative.none_line");
						var currentNegative = PerkCrafterReflection.GetCurrentNegative();
						return currentNegative != null
							? Strings.Get("overlay.perk_crafter.negative", currentNegative.Description)
							: Strings.Get("overlay.perk_crafter.negative.not_selected");
					}
				case MenuItem.Result: {
						var resultName = PerkCrafterReflection.GetResultName();
						return !string.IsNullOrEmpty(resultName)
							? Strings.Get("overlay.perk_crafter.result", resultName)
							: Strings.Get("overlay.perk_crafter.result.unnamed");
					}
				case MenuItem.Craft: {
						var (amount, goodName) = PerkCrafterReflection.GetPrice();
						int have = PerkCrafterReflection.GetStorageAmount();
						return PerkCrafterReflection.CanAffordCraft()
							? Strings.Get("overlay.perk_crafter.craft", amount, goodName, have)
							: Strings.Get("overlay.perk_crafter.craft.unavailable", amount, goodName, have);
					}
				default:
					return null;
			}
		}

		private void AnnounceMainMenuItem() {
			string label = GetMainMenuLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		// ========================================
		// MAIN MENU ACTIVATION
		// ========================================

		private void ActivateMainMenuItem(int index) {
			var item = (MenuItem)index;
			switch (item) {
				case MenuItem.Dialogue:
				case MenuItem.Shards:
					AnnounceMainMenuItem();
					break;
				case MenuItem.Result:
					OpenNameEdit();
					break;
				case MenuItem.Craft:
					PerformCraft();
					break;
			}
		}

		// ========================================
		// SUBMENU (Level 1)
		// ========================================

		private int GetSubmenuItemCount() {
			switch (_activeSubmenu) {
				case MenuItem.Hook:
					return _hookOptions?.Count ?? 0;
				case MenuItem.Positive:
					return _positiveOptions?.Count ?? 0;
				case MenuItem.Negative:
					return (_negativeOptions?.Count ?? 0) + 1;
				default:
					return 0;
			}
		}

		private string GetSubmenuLabel(int index) {
			switch (_activeSubmenu) {
				case MenuItem.Hook:
					if (_hookOptions != null && index >= 0 && index < _hookOptions.Count)
						return _hookOptions[index].Description;
					break;
				case MenuItem.Positive:
					if (_positiveOptions != null && index >= 0 && index < _positiveOptions.Count)
						return _positiveOptions[index].Description;
					break;
				case MenuItem.Negative:
					if (index == 0) return Strings.Get("overlay.perk_crafter.negative.none_submenu");
					int negIdx = index - 1;
					if (_negativeOptions != null && negIdx >= 0 && negIdx < _negativeOptions.Count)
						return _negativeOptions[negIdx].Description;
					break;
			}
			return null;
		}

		private void AnnounceSubmenuItem() {
			string label = GetSubmenuLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		private void SelectSubmenuItem() {
			bool success = false;
			int index = CurrentIndex;

			switch (_activeSubmenu) {
				case MenuItem.Hook:
					if (_hookOptions != null && index < _hookOptions.Count)
						success = PerkCrafterReflection.SelectHook(_hookOptions[index]);
					break;
				case MenuItem.Positive:
					if (_positiveOptions != null && index < _positiveOptions.Count)
						success = PerkCrafterReflection.SelectPositive(_positiveOptions[index]);
					break;
				case MenuItem.Negative:
					if (index == 0)
						success = PerkCrafterReflection.SelectNegative(null);
					else if (_negativeOptions != null && index - 1 < _negativeOptions.Count)
						success = PerkCrafterReflection.SelectNegative(_negativeOptions[index - 1]);
					break;
			}

			if (success) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("common.selected"));
				RefreshData();
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("common.cannot_select"));
			}

			SetLevel(0);
			_search.Clear();
			AnnounceMainMenuItem();
		}

		// ========================================
		// NAME EDITING (Level 2)
		// ========================================

		private void OpenNameEdit() {
			var currentName = PerkCrafterReflection.GetResultName() ?? "";
			_nameBuffer = new StringBuilder(currentName);
			SetLevel(2);
			_nameEditing = true;

			Speech.Say(Strings.Get("overlay.perk_crafter.name.editing", currentName));
		}

		private bool ProcessNameEditKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					ConfirmNameEdit();
					return true;

				case KeyCode.Escape:
					CancelNameEdit();
					InputBlocker.BlockCancelOnce = true;
					return true;

				case KeyCode.Backspace:
					if (_nameBuffer.Length > 0) {
						_nameBuffer.Remove(_nameBuffer.Length - 1, 1);
						Speech.Say(_nameBuffer.Length > 0 ? _nameBuffer.ToString() : Strings.Get("common.empty"));
					}
					return true;

				case KeyCode.R:
					if (modifiers.Alt && !modifiers.Shift && !modifiers.Control) {
						RandomizeName();
						return true;
					}
					goto default;

				default:
					if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z) {
						char c = modifiers.Shift ?
							(char)('A' + (keyCode - KeyCode.A)) :
							(char)('a' + (keyCode - KeyCode.A));

						if (_nameEditing) {
							_nameBuffer.Clear();
							_nameEditing = false;
						}

						_nameBuffer.Append(c);
						Speech.Say(_nameBuffer.ToString());
						return true;
					} else if (keyCode == KeyCode.Space) {
						_nameBuffer.Append(' ');
						Speech.Say(_nameBuffer.ToString());
						return true;
					}
					return true;
			}
		}

		private void ConfirmNameEdit() {
			if (_nameBuffer.Length > 0) {
				PerkCrafterReflection.SetResultName(_nameBuffer.ToString());
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("overlay.perk_crafter.name.saved", _nameBuffer));
			} else {
				Speech.Say(Strings.Get("common.name_unchanged"));
			}

			SetLevel(0);
			_nameEditing = false;
		}

		private void CancelNameEdit() {
			Speech.Say(Strings.Get("common.cancelled"));
			SetLevel(0);
			_nameEditing = false;
		}

		private void RandomizeName() {
			if (PerkCrafterReflection.RandomizeName()) {
				SoundManager.PlayButtonClick();
				var newName = PerkCrafterReflection.GetResultName();
				_nameBuffer = new StringBuilder(newName ?? "");
				Speech.Say(Strings.Get("overlay.perk_crafter.name.randomized", newName));
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("common.cannot_randomize"));
			}
		}

		// ========================================
		// CRAFTING
		// ========================================

		private void PerformCraft() {
			if (!PerkCrafterReflection.CanAffordCraft()) {
				var (amount, goodName) = PerkCrafterReflection.GetPrice();
				int have = PerkCrafterReflection.GetStorageAmount();
				Speech.Say(Strings.Get("overlay.perk_crafter.craft.cannot_afford", amount, goodName, have));
				SoundManager.PlayFailed();
				return;
			}

			// Force the model to rebuild from current state before crafting.
			// The model may be stale from a saved session (e.g. with a negative
			// that was since deselected) if the user didn't change any selections.
			PerkCrafterReflection.ForceRebuildResult();

			if (PerkCrafterReflection.PerformCraft()) {
				SoundManager.PlayButtonClick();
				RefreshData();

				if (_isFinishedMode) {
					Speech.Say(Strings.Get("overlay.perk_crafter.craft.done_all"));
					CurrentIndex = 0;
				} else {
					int crafted = PerkCrafterReflection.GetCraftedPerksCount();
					int total = PerkCrafterReflection.GetTotalCharges();
					Speech.Say(Strings.Get("overlay.perk_crafter.craft.done_next", crafted + 1, total));
				}
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("overlay.perk_crafter.craft.failed"));
			}
		}

		// ========================================
		// FINISHED MODE
		// ========================================

		private string GetFinishedLabel(int index) {
			if (index == 0) {
				var dialogue = PerkCrafterReflection.GetNpcDialogue();
				return !string.IsNullOrEmpty(dialogue)
					? Strings.Get("overlay.perk_crafter.dialogue_with", dialogue)
					: Strings.Get("overlay.perk_crafter.dialogue_finished");
			}

			int perkIndex = index - 1;
			if (_craftedPerks != null && perkIndex < _craftedPerks.Count) {
				var perk = _craftedPerks[perkIndex];
				return Strings.Get("overlay.perk_crafter.finished.perk", perkIndex + 1, perk.Name, perk.Description);
			}
			return null;
		}

		private void AnnounceFinishedItem() {
			string label = GetFinishedLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		// ========================================
		// HELPERS
		// ========================================

		private void ClearData() {
			_hookOptions?.Clear();
			_positiveOptions?.Clear();
			_negativeOptions?.Clear();
			_craftedPerks?.Clear();
		}
	}
}
