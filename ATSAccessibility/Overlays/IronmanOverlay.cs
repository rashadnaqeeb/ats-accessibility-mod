using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the IronmanUpgradePopup (Queen's Hand Trial upgrades).
	/// Three-level navigation: sections -> items -> rewards.
	/// Sections: Pick Options (3 choices), Core Upgrades, and Unlocked.
	/// Pattern B at Level 1: Enter/Space=buy (Action), Right=view rewards (CanDrillDown).
	/// </summary>
	public class IronmanOverlay: MenuBase {
		private enum SectionType { PickOptions, CoreUpgrades, Unlocked }

		// Section data
		private SectionType[] _sections;
		private string[] _sectionNames;

		// Item data
		private List<IronmanReflection.UpgradeInfo> _currentItems = new List<IronmanReflection.UpgradeInfo>();
		private List<IronmanReflection.RewardInfo> _rewards = new List<IronmanReflection.RewardInfo>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.ironman.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			switch (Level) {
				case 0: return _sections?.Length ?? 0;
				case 1: return _currentItems.Count;
				case 2: return _rewards.Count;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0:
					return (_sectionNames != null && index >= 0 && index < _sectionNames.Length)
						? _sectionNames[index] : null;
				case 1:
					return (index >= 0 && index < _currentItems.Count) ? _currentItems[index].Name : null;
				case 2:
					return (index >= 0 && index < _rewards.Count) ? _rewards[index].Name : null;
				default:
					return null;
			}
		}

		protected override void AnnounceCurrentItem() {
			switch (Level) {
				case 0:
					AnnounceSection();
					break;
				case 1:
					AnnounceItem();
					break;
				case 2:
					AnnounceReward();
					break;
			}
		}

		protected override void RefreshData() {
			BuildSections();
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					if (_sections == null || index < 0 || index >= _sections.Length) return EnterAction.None;
					var items = LoadItemsForSection(_sections[index]);
					if (items.Count == 0) {
						Speech.Say(Strings.Get("common.no_upgrades"));
						return EnterAction.None;
					}
					_currentItems = items;
					return EnterAction.DrillDown;

				case 1:
					return EnterAction.Action;

				default:
					return EnterAction.None;
			}
		}

		protected override void OnAction(int index) {
			if (Level == 1)
				ActivateItem();
		}

		protected override void OnSpace(int index) {
			if (Level == 1)
				ActivateItem();
		}

		protected override bool CanDrillDown(int index) {
			switch (Level) {
				case 0:
					// Right arrow: load items, check empty
					if (_sections == null || index < 0 || index >= _sections.Length) return false;
					var items = LoadItemsForSection(_sections[index]);
					if (items.Count == 0) {
						Speech.Say(Strings.Get("common.no_upgrades"));
						return false;
					}
					_currentItems = items;
					return true;

				case 1:
					// Right arrow: load rewards, check empty
					if (index < 0 || index >= _currentItems.Count) return false;
					var rewards = IronmanReflection.GetRewards(_currentItems[index].UpgradeObj);
					if (rewards.Count == 0) {
						Speech.Say(Strings.Get("common.no_rewards"));
						return false;
					}
					_rewards = rewards;
					return true;

				default:
					return false;
			}
		}

		protected override void OnDrillDown(int index) {
			// Data already loaded by OnEnter or CanDrillDown
		}

		protected override void OnGoBack() {
			if (Level == 2)
				_rewards.Clear();
			else if (Level == 1)
				_currentItems.Clear();
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// At section level, Escape and Left both close and pass to game
			if (Level == 0 && (keyCode == KeyCode.Escape || keyCode == KeyCode.LeftArrow)) {
				Close();
				// Pass to game to close popup
				return false;
			}
			return null;
		}

		// Default OnEscape: GoBack if level > 0, PassThrough if level 0
		// Level 0 Escape is handled in HandleSpecialKey before reaching OnEscape

		protected override string GetOpenAnnouncement() {
			// Custom announcement handled in OnOpened
			return null;
		}

		protected override void OnOpened() {
			AnnounceOpen();
		}

		protected override void OnClosed() {
			_sections = null;
			_sectionNames = null;
			_currentItems.Clear();
			_rewards.Clear();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override string GetSearchName(int index) {
			switch (Level) {
				case 0:
					return (_sectionNames != null && index >= 0 && index < _sectionNames.Length)
						? _sectionNames[index] : null;
				case 1:
					return (index >= 0 && index < _currentItems.Count) ? _currentItems[index].Name : null;
				case 2:
					return (index >= 0 && index < _rewards.Count) ? _rewards[index].Name : null;
				default:
					return null;
			}
		}

		// ========================================
		// SECTION BUILDING
		// ========================================

		private void BuildSections() {
			var sectionList = new List<SectionType>();
			var nameList = new List<string>();

			// Add Random Upgrades section only if not at max picks
			if (!IronmanReflection.HasReachedMaxPicks()) {
				sectionList.Add(SectionType.PickOptions);
				nameList.Add(Strings.Get("overlay.ironman.section.random"));
			}

			// Always add Core Upgrades
			sectionList.Add(SectionType.CoreUpgrades);
			nameList.Add(Strings.Get("overlay.ironman.section.core"));

			// Add Unlocked section if there are unlocked upgrades
			var unlocked = IronmanReflection.GetUnlockedUpgrades();
			if (unlocked.Count > 0) {
				sectionList.Add(SectionType.Unlocked);
				nameList.Add(Strings.Get("common.unlocked"));
			}

			_sections = sectionList.ToArray();
			_sectionNames = nameList.ToArray();
		}

		/// <summary>
		/// Load items for a section type. Returns the item list (caller stores it).
		/// </summary>
		private List<IronmanReflection.UpgradeInfo> LoadItemsForSection(SectionType sectionType) {
			switch (sectionType) {
				case SectionType.PickOptions:
					return IronmanReflection.GetCurrentPickOptions();
				case SectionType.CoreUpgrades:
					return IronmanReflection.GetCoreUpgrades();
				case SectionType.Unlocked:
					return IronmanReflection.GetUnlockedUpgrades();
				default:
					return new List<IronmanReflection.UpgradeInfo>();
			}
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		private void AnnounceOpen() {
			int completed = IronmanReflection.GetCompletedPicks();
			int max = IronmanReflection.GetMaxPicks();

			if (IronmanReflection.HasReachedMaxPicks()) {
				Speech.Say(Strings.Get("overlay.ironman.open.all_done", max));
			} else {
				Speech.Say(Strings.Get("overlay.ironman.open.pick", completed + 1, max));
			}

			AnnounceSection();
		}

		private void AnnounceSection() {
			if (_sectionNames == null || _indices[0] < 0 || _indices[0] >= _sectionNames.Length) return;

			string name = _sectionNames[_indices[0]];
			var sectionType = _sections[_indices[0]];

			int count = 0;
			switch (sectionType) {
				case SectionType.PickOptions:
					count = IronmanReflection.GetCurrentPickOptions().Count;
					break;
				case SectionType.CoreUpgrades:
					count = IronmanReflection.GetCoreUpgrades().Count;
					break;
				case SectionType.Unlocked:
					count = IronmanReflection.GetUnlockedUpgrades().Count;
					break;
			}

			Speech.Say(Strings.Get("overlay.ironman.section_row", name, count));
		}

		private void AnnounceItem() {
			if (_indices[1] < 0 || _indices[1] >= _currentItems.Count) return;

			var upgrade = _currentItems[_indices[1]];

			if (upgrade.IsUnlocked) {
				Speech.Say(Strings.Get("overlay.ironman.item.unlocked", upgrade.Name));
			} else if (upgrade.CanAfford) {
				Speech.Say(Strings.Get("overlay.ironman.item.buyable", upgrade.Name, upgrade.PriceText));
			} else {
				Speech.Say(Strings.Get("overlay.ironman.item.cannot_afford", upgrade.Name, upgrade.PriceText));
			}
		}

		private void AnnounceReward() {
			if (_indices[2] < 0 || _indices[2] >= _rewards.Count) return;

			var reward = _rewards[_indices[2]];

			if (!string.IsNullOrEmpty(reward.Description))
				Speech.Say(Strings.Get("overlay.ironman.reward.with_desc", reward.Name, reward.Description));
			else
				Speech.Say(reward.Name);
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void ActivateItem() {
			if (_indices[1] < 0 || _indices[1] >= _currentItems.Count) return;

			var upgrade = _currentItems[_indices[1]];

			if (upgrade.IsUnlocked) {
				Speech.Say(Strings.Get("overlay.ironman.already_unlocked"));
				return;
			}

			if (!upgrade.CanAfford) {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("common.cant_afford"));
				return;
			}

			if (IronmanReflection.Pick(upgrade.UpgradeObj)) {
				SoundManager.PlayCapitalUpgradeBought();
				Speech.Say(Strings.Get("overlay.ironman.purchased", upgrade.Name));

				// Refresh data after purchase
				RefreshAfterPurchase();
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("common.purchase_failed"));
			}
		}

		private void RefreshAfterPurchase() {
			// Remember if we were in Random Upgrades section
			bool wasInRandomUpgrades = Level == 1 &&
				_sections != null &&
				_indices[0] < _sections.Length &&
				_sections[_indices[0]] == SectionType.PickOptions;

			// Rebuild sections (pick options might be gone if max reached)
			BuildSections();

			// If we're still in items level, refresh the current section
			if (Level == 1 && _sections != null && _indices[0] < _sections.Length) {
				var sectionType = _sections[_indices[0]];

				switch (sectionType) {
					case SectionType.PickOptions:
						_currentItems = IronmanReflection.GetCurrentPickOptions();
						// Reset to first item since these are completely new options
						_indices[1] = 0;
						break;
					case SectionType.CoreUpgrades:
						_currentItems = IronmanReflection.GetCoreUpgrades();
						// Clamp index if needed
						if (_indices[1] >= _currentItems.Count)
							_indices[1] = Mathf.Max(0, _currentItems.Count - 1);
						break;
					case SectionType.Unlocked:
						_currentItems = IronmanReflection.GetUnlockedUpgrades();
						// Clamp index if needed
						if (_indices[1] >= _currentItems.Count)
							_indices[1] = Mathf.Max(0, _currentItems.Count - 1);
						break;
				}

				// If no items left in section, go back to sections
				if (_currentItems.Count == 0) {
					SetLevel(0);
					if (_indices[0] >= _sections.Length)
						_indices[0] = Mathf.Max(0, _sections.Length - 1);
					AnnounceSection();
				} else if (wasInRandomUpgrades && sectionType == SectionType.PickOptions) {
					// Announce new pick status and first option
					int completed = IronmanReflection.GetCompletedPicks();
					int max = IronmanReflection.GetMaxPicks();
					Speech.Say(Strings.Get("overlay.ironman.new_pick", completed + 1, max));
					AnnounceItem();
				}
			} else if (_sections == null || _indices[0] >= _sections.Length) {
				// Section was removed (pick options gone), adjust index
				_indices[0] = Mathf.Max(0, (_sections?.Length ?? 1) - 1);
				SetLevel(0);
				// Announce that all picks are complete
				int max = IronmanReflection.GetMaxPicks();
				Speech.Say(Strings.Get("overlay.ironman.all_picks_done", max));
				AnnounceSection();
			}
		}
	}
}
