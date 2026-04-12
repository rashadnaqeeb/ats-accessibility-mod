using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for Hearth buildings (Ancient Hearth, Small Hearth).
	/// Provides navigation through Fire, Sacrifice, Upgrades, Blight, and Workers sections.
	/// </summary>
	public class HearthNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Fire,
			Sacrifice,
			Services,   // The Commons (hearth services)
			Upgrades,
			Blight,
			Workers
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;
		private bool _isMainHearth;

		// Fire data
		private float _fuelLevel;  // 0-1
		private float _fuelTimeRemaining;
		private bool _isFireLow;
		private bool _isFireOut;

		// Upgrades data
		private List<HearthReflection.HearthUpgradeInfo> _upgradeInfo = new List<HearthReflection.HearthUpgradeInfo>();

		// Blight data
		private float _corruptionRate;

		// Sacrifice data
		private List<object> _sacrificeRecipes = new List<object>();
		private List<HearthReflection.SacrificeRecipeInfo> _sacrificeInfo = new List<HearthReflection.SacrificeRecipeInfo>();

		// Fuel data
		private List<HearthReflection.FuelInfo> _fuelTypes = new List<HearthReflection.FuelInfo>();

		// Services data (The Commons)
		private bool _servicesMetaUnlocked = false;
		private bool _servicesSettlementUnlocked = false;
		private List<HearthReflection.HearthServiceInfo> _serviceRecipes = new List<HearthReflection.HearthServiceInfo>();

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		public HearthNavigator() {
			_workersSection.GetWorkerIdsFunc = HearthReflection.GetHearthWorkerIds;
		}

		protected override string NavigatorName => "HearthNavigator";

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Fire:
					return GetFireItemCount();
				case SectionType.Sacrifice:
					return _sacrificeRecipes.Count;
				case SectionType.Services:
					if (!_servicesSettlementUnlocked)
						return 1;  // Just the unlock option
					return _serviceRecipes.Count;
				case SectionType.Upgrades:
					return _upgradeInfo.Count;
				case SectionType.Blight:
					return 1;  // Just corruption level
				case SectionType.Workers:
					return _workersSection.GetItemCount();
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			// Fire section: Fuel types item (index 2) has sub-items
			if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2) {
				return _fuelTypes.Count;
			}

			// Workers have sub-items (races to assign, plus unassign if occupied)
			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return _workersSection.GetSubItemCount(itemIndex);
			}

			// Sacrifice uses +/- keys, no sub-items
			return 0;
		}

		protected override void AnnounceSection(int sectionIndex) {
			string sectionName = _sectionNames[sectionIndex];
			Speech.Say(sectionName);
		}

		protected override void AnnounceItem(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Fire:
					AnnounceFireItem(itemIndex);
					break;
				case SectionType.Sacrifice:
					AnnounceSacrificeItem(itemIndex);
					break;
				case SectionType.Services:
					AnnounceServiceItem(itemIndex);
					break;
				case SectionType.Upgrades:
					AnnounceUpgradeItem(itemIndex);
					break;
				case SectionType.Blight:
					AnnounceBlightItem(itemIndex);
					break;
				case SectionType.Workers:
					_workersSection.AnnounceItem(itemIndex);
					break;
			}
		}

		protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Workers)
				return Strings.Get("common.no_free_workers");
			return null;
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			// Fire section: Fuel types sub-items
			if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2) {
				AnnounceFuelSubItem(subItemIndex);
				return;
			}

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				_workersSection.AnnounceSubItem(itemIndex, subItemIndex);
			}
		}

		protected override bool PerformItemAction(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return false;

			// Sacrifice: hint about +/- keys
			if (_sectionTypes[sectionIndex] == SectionType.Sacrifice) {
				Speech.Say(Strings.Get("nav.hearth.sacrifice.hint"));
				return true;
			}

			// Services unlock action
			if (_sectionTypes[sectionIndex] == SectionType.Services && !_servicesSettlementUnlocked && itemIndex == 0) {
				if (!HearthReflection.CanAffordHearthServicesUnlock(_building)) {
					Speech.Say(Strings.Get("common.not_enough_resources"));
					SoundManager.PlayFailed();
					return false;
				}

				if (HearthReflection.UnlockHearthServices(_building)) {
					_servicesSettlementUnlocked = true;
					_serviceRecipes = HearthReflection.GetHearthServiceRecipes(_building);
					SoundManager.PlayButtonClick();
					Speech.Say(Strings.Get("nav.hearth.commons_unlocked"));
					return true;
				} else {
					Speech.Say(Strings.Get("nav.hearth.cannot_unlock"));
					SoundManager.PlayFailed();
					return false;
				}
			}

			return false;
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			// Fire section: Toggle fuel type
			if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2) {
				return ToggleFuel(subItemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return PerformWorkerSubItemAction(itemIndex, subItemIndex);
			}
			return false;
		}

		protected override void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			// Fire section: adjust fuel priority at sub-item level
			if (_sectionTypes[sectionIndex] == SectionType.Fire && _navigationLevel >= 2) {
				AdjustFuelPriority(_currentSubItemIndex, delta);
				return;
			}

			// Sacrifice section uses +/- to adjust level
			if (_sectionTypes[sectionIndex] == SectionType.Sacrifice) {
				AdjustSacrificeLevel(itemIndex, delta);
			}
		}

		protected override void RefreshData() {
			_isMainHearth = HearthReflection.IsMainHearth(_building);

			// Fire data
			_fuelLevel = HearthReflection.GetHearthFireLevel(_building);
			_fuelTimeRemaining = HearthReflection.GetHearthFuelTimeRemaining(_building);
			_isFireLow = HearthReflection.IsHearthFireLow(_building);
			_isFireOut = HearthReflection.IsHearthFireOut(_building);

			// Upgrades data
			_upgradeInfo = HearthReflection.GetHearthUpgradeInfo(_building);

			// Blight data
			_corruptionRate = HearthReflection.GetHearthCorruptionRate(_building);

			// Sacrifice data
			_sacrificeRecipes = HearthReflection.GetHearthSacrificeRecipes(_building);
			RefreshSacrificeInfo();

			// Fuel data
			_fuelTypes = HearthReflection.GetAllFuelTypes();

			// Services data (The Commons)
			_servicesMetaUnlocked = HearthReflection.AreHearthServicesMetaUnlocked();
			if (_servicesMetaUnlocked) {
				_servicesSettlementUnlocked = HearthReflection.AreHearthServicesEnabled(_building);
				if (_servicesSettlementUnlocked) {
					_serviceRecipes = HearthReflection.GetHearthServiceRecipes(_building);
				}
			}

			// Build sections list
			var sectionNames = new List<string>();
			var sectionTypes = new List<SectionType>();

			sectionNames.Add(Strings.Get("nav.hearth.section.fire"));
			sectionTypes.Add(SectionType.Fire);

			// Sacrifice section only shown if there are sacrifice recipes
			if (_sacrificeRecipes.Count > 0) {
				sectionNames.Add(Strings.Get("nav.hearth.section.sacrifice"));
				sectionTypes.Add(SectionType.Sacrifice);
			}

			// Services section only shown on main hearth if meta progression unlocked
			if (_servicesMetaUnlocked && _isMainHearth) {
				sectionNames.Add(Strings.Get("common.services"));
				sectionTypes.Add(SectionType.Services);
			}

			// Upgrades section only shown if there are upgrade tiers
			if (_upgradeInfo.Count > 0) {
				sectionNames.Add(Strings.Get("common.upgrades"));
				sectionTypes.Add(SectionType.Upgrades);
			}

			// Blight section only shown for main hearth when blight is active
			if (_isMainHearth && GameReflection.IsBlightActive()) {
				sectionNames.Add(Strings.Get("nav.hearth.section.blight"));
				sectionTypes.Add(SectionType.Blight);
			}

			if (TryInitializeWorkersSection()) {
				sectionNames.Add(Strings.Get("common.workers"));
				sectionTypes.Add(SectionType.Workers);
			}

			_sectionNames = sectionNames.ToArray();
			_sectionTypes = sectionTypes.ToArray();

			Debug.Log($"[ATSAccessibility] HearthNavigator: Refreshed data, {_sectionNames.Length} sections");
		}

		protected override void ClearData() {
			_sectionNames = null;
			_sectionTypes = null;
			ClearWorkersSection();
			_sacrificeRecipes.Clear();
			_sacrificeInfo.Clear();
			_fuelTypes.Clear();
			_upgradeInfo.Clear();
			_servicesMetaUnlocked = false;
			_servicesSettlementUnlocked = false;
			_serviceRecipes.Clear();
		}

		// ========================================
		// FIRE SECTION
		// ========================================

		private int GetFireItemCount() {
			return 3;  // Fuel level, Time remaining, Fuel types
		}

		private void AnnounceFireItem(int itemIndex) {
			switch (itemIndex) {
				case 0:
					int percentage = Mathf.RoundToInt(_fuelLevel * 100f);
					Speech.Say(Strings.Get("nav.hearth.fuel_level", percentage));
					break;
				case 1:
					int seconds = Mathf.RoundToInt(_fuelTimeRemaining);
					if (seconds <= 0) {
						Speech.Say(Strings.Get("nav.hearth.time_fire_out"));
					} else if (seconds < 60) {
						Speech.Say(Strings.Get("nav.hearth.time_seconds", seconds));
					} else {
						int minutes = seconds / 60;
						int remainingSecs = seconds % 60;
						if (remainingSecs > 0)
							Speech.Say(Strings.Get("nav.hearth.time_minutes_seconds", minutes, remainingSecs));
						else
							Speech.Say(Strings.Get("nav.hearth.time_minutes", minutes));
					}
					break;
				case 2:
					// Fuel types submenu
					int enabledCount = 0;
					foreach (var fuel in _fuelTypes) {
						if (fuel.isEnabled) enabledCount++;
					}
					Speech.Say(Strings.Get("nav.hearth.fuel_types_count", enabledCount, _fuelTypes.Count));
					break;
			}
		}

		// ========================================
		// UPGRADES SECTION
		// ========================================

		private void AnnounceUpgradeItem(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count) {
				Speech.Say(Strings.Get("nav.hearth.invalid_upgrade"));
				return;
			}

			// Refresh upgrade info to get current state
			_upgradeInfo = HearthReflection.GetHearthUpgradeInfo(_building);
			if (itemIndex >= _upgradeInfo.Count) {
				Speech.Say(Strings.Get("nav.hearth.invalid_upgrade"));
				return;
			}

			var info = _upgradeInfo[itemIndex];

			// Locked tiers: just announce the name and that it requires meta progression
			if (!info.isUnlockedInMeta) {
				Speech.Say(Strings.Get("nav.hearth.upgrade_meta_locked", info.displayName));
				return;
			}

			// Build status string
			string status = info.isAchieved ? Strings.Get("nav.hearth.upgrade.achieved") : Strings.Get("common.available");

			// Build requirements string
			var reqParts = new List<string>();

			// Housed population
			if (info.minPopulation > 0) {
				reqParts.Add(Strings.Get("nav.hearth.req.housed_population", info.currentPopulation, info.minPopulation));
			}

			// Institutions
			if (info.minInstitutions > 0) {
				reqParts.Add(Strings.Get("nav.hearth.req.institutions", info.currentInstitutions, info.minInstitutions));
			}

			// Decorations (tier name already includes "decorations" suffix)
			foreach (var decorReq in info.decorationRequirements) {
				reqParts.Add(Strings.Get("nav.hearth.req.decoration", decorReq.tierName, decorReq.current, decorReq.required));
			}

			string requirements = reqParts.Count > 0 ? string.Join(", ", reqParts) : Strings.Get("common.none");

			// Build announcement
			string announcement = Strings.Get("nav.hearth.upgrade_line", info.displayName, status, requirements);

			// Add effect
			if (!string.IsNullOrEmpty(info.effectDescription)) {
				announcement += Strings.Get("nav.hearth.upgrade_effect_suffix", info.effectDescription);
			}

			Speech.Say(announcement);
		}

		private string GetUpgradeItemName(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count)
				return null;

			return _upgradeInfo[itemIndex].displayName;
		}

		// ========================================
		// BLIGHT SECTION
		// ========================================

		private void AnnounceBlightItem(int itemIndex) {
			int percentage = Mathf.RoundToInt(_corruptionRate * 100f);
			if (percentage <= 0)
				Speech.Say(Strings.Get("nav.hearth.corruption_none"));
			else
				Speech.Say(Strings.Get("nav.hearth.corruption_percent", percentage));
		}

		// ========================================
		// SACRIFICE SECTION
		// ========================================

		private void RefreshSacrificeInfo() {
			_sacrificeInfo.Clear();
			foreach (var recipe in _sacrificeRecipes) {
				var info = HearthReflection.GetSacrificeRecipeInfo(_building, recipe);
				_sacrificeInfo.Add(info);
			}
		}

		private void AnnounceSacrificeItem(int recipeIndex) {
			if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count) {
				Speech.Say(Strings.Get("nav.hearth.invalid_sacrifice"));
				return;
			}

			// Refresh the info for this recipe to get current state
			if (recipeIndex < _sacrificeRecipes.Count) {
				_sacrificeInfo[recipeIndex] = HearthReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
			}

			var info = _sacrificeInfo[recipeIndex];

			// Use good name as primary identifier
			string name = info.goodName;
			if (string.IsNullOrEmpty(name)) {
				name = info.recipeName;
			}

			// Get effect description (strip trailing period from localized text)
			string effect = info.effectDescription;
			if (string.IsNullOrEmpty(effect)) {
				effect = info.effectName;
			}
			if (!string.IsNullOrEmpty(effect)) {
				effect = effect.TrimEnd('.');
				effect = effect + Strings.Get("nav.hearth.sacrifice.per_level_suffix");
			}

			if (info.level > 0) {
				// Active: "{Good}: Level X, {total consumption} per minute, {effect} per level"
				float totalConsumption = info.consumptionPerMin * info.level;
				int consumptionRounded = Mathf.RoundToInt(totalConsumption);
				Speech.Say(Strings.Get("nav.hearth.sacrifice.active", name, info.level, consumptionRounded, effect));
			} else {
				// Off: "{Good}: Off, {effect} per level"
				Speech.Say(Strings.Get("nav.hearth.sacrifice.off", name, effect));
			}
		}

		private void AdjustSacrificeLevel(int recipeIndex, int delta) {
			if (recipeIndex < 0 || recipeIndex >= _sacrificeRecipes.Count)
				return;

			// Refresh info to get current state
			_sacrificeInfo[recipeIndex] = HearthReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
			var info = _sacrificeInfo[recipeIndex];
			var recipeState = _sacrificeRecipes[recipeIndex];

			int currentLevel = info.level;
			int newLevel = currentLevel + delta;

			// Clamp to valid range (0 to maxLevel)
			if (newLevel < 0) newLevel = 0;
			if (newLevel > info.maxLevel) newLevel = info.maxLevel;

			// No change needed
			if (newLevel == currentLevel) {
				if (delta > 0 && currentLevel == info.maxLevel) {
					Speech.Say(Strings.Get("nav.hearth.maximum_level"));
				} else if (delta < 0 && currentLevel == 0) {
					Speech.Say(Strings.Get("nav.hearth.already_off"));
				}
				return;
			}

			// Check if can afford when increasing from 0
			if (currentLevel == 0 && newLevel > 0 && !info.canAfford) {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("nav.hearth.not_enough_good", info.goodName));
				return;
			}

			// Apply the change
			if (HearthReflection.SetHearthSacrificeLevel(_building, recipeState, newLevel)) {
				if (newLevel == 0) {
					SoundManager.PlayButtonClick();
					Speech.Say(Strings.Get("common.off"));
				} else if (currentLevel == 0) {
					// Enabling from off
					SoundManager.PlayBuildingFireButtonStart();
					Speech.Say(Strings.Get("nav.hearth.level_n", newLevel));
				} else {
					SoundManager.PlayButtonClick();
					Speech.Say(Strings.Get("nav.hearth.level_n", newLevel));
				}
				RefreshSacrificeInfo();
			}
		}

		private string GetSacrificeItemName(int recipeIndex) {
			if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count)
				return null;

			var info = _sacrificeInfo[recipeIndex];
			// Use good name for search
			if (!string.IsNullOrEmpty(info.goodName) && info.goodName != "Unknown") {
				return info.goodName;
			}
			return info.recipeName;
		}

		// ========================================
		// SERVICES SECTION (The Commons)
		// ========================================

		private void AnnounceServiceItem(int itemIndex) {
			if (!_servicesSettlementUnlocked) {
				// Unlock option
				var price = HearthReflection.GetHearthServicesUnlockPrice(_building);
				if (price != null) {
					bool canAfford = HearthReflection.CanAffordHearthServicesUnlock(_building);
					string affordText = canAfford ? "" : Strings.Get("nav.common.not_enough_resources_suffix");
					Speech.Say(Strings.Get("nav.hearth.services.locked_with_cost", price.Value.amount, price.Value.displayName, affordText));
				} else {
					Speech.Say(Strings.Get("common.locked"));
				}
				return;
			}

			// Service recipe
			if (itemIndex < 0 || itemIndex >= _serviceRecipes.Count) {
				Speech.Say(Strings.Get("nav.hearth.invalid_service"));
				return;
			}

			var service = _serviceRecipes[itemIndex];
			// Format: "Need name: requires X Good, Y stars" or "Need name: free, Y stars"
			if (service.IsGoodConsumed && service.GoodAmount > 0) {
				Speech.Say(Strings.Get("nav.hearth.service.paid", service.NeedName, service.GoodAmount, service.GoodDisplayName, service.Grade));
			} else {
				Speech.Say(Strings.Get("nav.hearth.service.free", service.NeedName, service.Grade));
			}
		}

		private string GetServiceItemName(int itemIndex) {
			if (!_servicesSettlementUnlocked)
				return itemIndex == 0 ? Strings.Get("common.unlock") : null;

			if (itemIndex >= 0 && itemIndex < _serviceRecipes.Count)
				return _serviceRecipes[itemIndex].NeedName;

			return null;
		}

		// ========================================
		// FUEL SUB-ITEMS (inside Fire section)
		// ========================================

		private void AnnounceFuelSubItem(int subItemIndex) {
			if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count) {
				Speech.Say(Strings.Get("nav.hearth.invalid_fuel"));
				return;
			}

			// Refresh the fuel state
			_fuelTypes = HearthReflection.GetAllFuelTypes();

			var fuel = _fuelTypes[subItemIndex];
			string status = fuel.isEnabled ? Strings.Get("common.enabled") : Strings.Get("common.disabled");
			if (fuel.priority > 0)
				Speech.Say(Strings.Get("nav.hearth.fuel.with_priority", fuel.displayName, status, fuel.priority));
			else
				Speech.Say(Strings.Get("nav.hearth.fuel.status", fuel.displayName, status));
		}

		private bool ToggleFuel(int subItemIndex) {
			if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
				return false;

			var fuel = _fuelTypes[subItemIndex];
			bool newState = !fuel.isEnabled;

			if (HearthReflection.SetFuelEnabled(fuel.name, newState)) {
				SoundManager.PlayButtonClick();
				Speech.Say(newState ? Strings.Get("common.enabled") : Strings.Get("common.disabled"));
				_fuelTypes = HearthReflection.GetAllFuelTypes();
				return true;
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("nav.hearth.cannot_change_fuel"));
				return false;
			}
		}

		private void AdjustFuelPriority(int subItemIndex, int delta) {
			if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
				return;

			var fuel = _fuelTypes[subItemIndex];
			int currentPrio = HearthReflection.GetFuelPriority(fuel.name);
			int newPrio = Mathf.Clamp(currentPrio + delta, 0, 3);

			if (newPrio == currentPrio) {
				Speech.Say(delta > 0 ? Strings.Get("common.maximum") : Strings.Get("common.minimum"));
				return;
			}

			HearthReflection.SetFuelPriority(fuel.name, newPrio);
			Speech.Say(Strings.Get("nav.common.priority_line", FormatPriority(newPrio)));
			_fuelTypes = HearthReflection.GetAllFuelTypes();
		}

		private string FormatPriority(int priority) {
			switch (priority) {
				case 0: return Strings.Get("nav.common.priority.lowest");
				case 3: return Strings.Get("nav.common.priority.highest");
				default: return priority.ToString();
			}
		}

		private string GetFuelSubItemName(int subItemIndex) {
			if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
				return null;

			return _fuelTypes[subItemIndex].displayName;
		}

		// ========================================
		// SEARCH NAME METHODS
		// ========================================

		protected override string GetSectionName(int sectionIndex) {
			if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
				return _sectionNames[sectionIndex];
			return null;
		}

		protected override string GetItemName(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return null;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Fire:
					switch (itemIndex) {
						case 0: return Strings.Get("common.fuel");
						case 1: return Strings.Get("nav.hearth.search.time");
						case 2: return Strings.Get("nav.hearth.search.fuel_types");
						default: return null;
					}
				case SectionType.Sacrifice:
					return GetSacrificeItemName(itemIndex);
				case SectionType.Services:
					return GetServiceItemName(itemIndex);
				case SectionType.Upgrades:
					return GetUpgradeItemName(itemIndex);
				case SectionType.Blight:
					return Strings.Get("nav.hearth.search.corruption");
				case SectionType.Workers:
					return _workersSection.GetItemName(itemIndex);
				default:
					return null;
			}
		}

		protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return null;

			// Fire section: Fuel types sub-items
			if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2) {
				return GetFuelSubItemName(subItemIndex);
			}

			// Workers have sub-items
			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return _workersSection.GetSubItemName(itemIndex, subItemIndex);
			}

			return null;
		}
	}
}
