using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for the main Storage building (warehouse).
	/// Provides navigation through Goods, Workers, Abilities, and Upgrades sections.
	/// </summary>
	public class StorageNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Goods,
			Workers,
			Abilities,
			Upgrades,
			Haulers
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;

		// Goods data (from global storage)
		private class GoodData {
			public string GoodName;
			public string DisplayName;
			public int Amount;
			public int Reserve;
		}
		private List<GoodData> _goods = new List<GoodData>();

		// Abilities data
		private int _abilityCount;

		// Haulers data - a flat list of the panel's rows: each group's toggle followed
		// by that group's building-type priority rows.
		private class HaulerRow {
			public bool IsToggle;
			public bool IsProduct;
			public object TypeKey;      // null for toggle rows
			public string DisplayName;
			public int Priority;
		}
		private List<HaulerRow> _haulerRows = new List<HaulerRow>();

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		protected override string NavigatorName => "StorageNavigator";

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Goods:
					return _goods.Count > 0 ? _goods.Count : 1;  // At least 1 for "Empty" message
				case SectionType.Workers:
					return _workersSection.GetItemCount();
				case SectionType.Abilities:
					return _abilityCount > 0 ? _abilityCount : 1;  // At least 1 for "No abilities" message
				case SectionType.Upgrades:
					return _upgradesSection.GetItemCount();
				case SectionType.Haulers:
					return _haulerRows.Count;
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			// Workers have sub-items (races to assign, plus unassign if occupied)
			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return _workersSection.GetSubItemCount(itemIndex);
			}

			// Upgrades have sub-items (perks)
			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.GetSubItemCount(itemIndex);
			}

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
				case SectionType.Goods:
					AnnounceGoodItem(itemIndex);
					break;
				case SectionType.Workers:
					_workersSection.AnnounceItem(itemIndex);
					break;
				case SectionType.Abilities:
					AnnounceAbilityItem(itemIndex);
					break;
				case SectionType.Upgrades:
					_upgradesSection.AnnounceItem(itemIndex);
					break;
				case SectionType.Haulers:
					AnnounceHaulerItem(itemIndex);
					break;
			}
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				_workersSection.AnnounceSubItem(itemIndex, subItemIndex);
			} else if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				_upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
			}
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return false;

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return PerformWorkerSubItemAction(itemIndex, subItemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
			}

			return false;
		}

		protected override void RefreshData() {
			RefreshGoodsData();
			RefreshAbilityData();
			RefreshHaulerData();
			BuildSections();

			Debug.Log($"[ATSAccessibility] StorageNavigator: Refreshed data - {_goods.Count} goods, {_workersSection.MaxWorkers} worker slots, {_abilityCount} abilities, {_haulerRows.Count} hauler rows");
		}

		protected override void ClearData() {
			_goods.Clear();
			_haulerRows.Clear();
			_sectionNames = null;
			_sectionTypes = null;
			ClearWorkersSection();
			ClearUpgradesSection();
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshGoodsData() {
			_goods.Clear();

			// Get goods from the global storage
			var storageGoods = GameReflection.GetStorageGoods();
			foreach (var kvp in storageGoods) {
				string displayName = GameReflection.GetGoodDisplayName(kvp.Key) ?? kvp.Key;
				_goods.Add(new GoodData {
					GoodName = kvp.Key,
					DisplayName = displayName,
					Amount = kvp.Value,
					Reserve = RecipesReflection.GetStorageReserve(kvp.Key)
				});
			}

			// Sort by display name for easier navigation
			_goods.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName));
		}

		private void RefreshAbilityData() {
			_abilityCount = BuildingReflection.GetCycleAbilityCount();
		}

		/// <summary>
		/// Rebuild the flat hauler row list. Empty unless this is the main storage and
		/// a storage hauler meta perk is unlocked, matching the game's own tab condition.
		/// </summary>
		private void RefreshHaulerData() {
			_haulerRows.Clear();

			if (!HaulersReflection.IsMainStorage(_building)) return;
			if (!HaulersReflection.AreAnyStorageHaulersUnlocked()) return;

			AddHaulerGroup(isProduct: true);
			AddHaulerGroup(isProduct: false);
		}

		private void AddHaulerGroup(bool isProduct) {
			_haulerRows.Add(new HaulerRow { IsToggle = true, IsProduct = isProduct });

			foreach (var prio in HaulersReflection.GetPriorities(isProduct)) {
				_haulerRows.Add(new HaulerRow {
					IsProduct = isProduct,
					TypeKey = prio.TypeKey,
					DisplayName = prio.DisplayName,
					Priority = prio.Priority
				});
			}
		}

		private void BuildSections() {
			var sections = new List<string>();
			var types = new List<SectionType>();

			// Goods section
			sections.Add(Strings.Get("common.goods"));
			types.Add(SectionType.Goods);

			// Abilities section (only if abilities exist)
			if (_abilityCount > 0) {
				sections.Add(Strings.Get("common.abilities"));
				types.Add(SectionType.Abilities);
			}

			// Haulers section (main storage only, once a hauler perk is unlocked)
			if (_haulerRows.Count > 0) {
				sections.Add(Strings.Get("nav.storage.section.haulers"));
				types.Add(SectionType.Haulers);
			}

			// Workers section (only if building currently accepts worker assignment)
			if (TryInitializeWorkersSection()) {
				sections.Add(Strings.Get("common.workers"));
				types.Add(SectionType.Workers);
			}

			// Upgrades section if available
			if (TryInitializeUpgradesSection()) {
				sections.Add(Strings.Get("common.upgrades"));
				types.Add(SectionType.Upgrades);
			}

			_sectionNames = sections.ToArray();
			_sectionTypes = types.ToArray();
		}

		// ========================================
		// GOODS SECTION
		// ========================================

		private void AnnounceGoodItem(int itemIndex) {
			// Refresh goods data to get current amounts
			RefreshGoodsData();

			if (_goods.Count == 0) {
				Speech.Say(Strings.Get("nav.common.storage_empty"));
				return;
			}

			// The refresh can shrink the list under a stale index — clamp and
			// announce the last item rather than going silent on a key press.
			itemIndex = ClampItemIndex(itemIndex, _goods.Count);

			var good = _goods[itemIndex];
			string text = Strings.Get("nav.common.storage_item", good.DisplayName, good.Amount);
			if (good.Reserve > 0)
				text += Strings.Get("nav.storage.reserve_suffix", good.Reserve);
			Speech.Say(text);
		}

		// ========================================
		// ABILITIES SECTION
		// ========================================

		private void AnnounceAbilityItem(int itemIndex) {
			if (_abilityCount == 0) {
				Speech.Say(Strings.Get("nav.storage.no_abilities"));
				return;
			}

			if (itemIndex >= _abilityCount) return;

			string abilityName = BuildingReflection.GetCycleAbilityName(itemIndex) ?? Strings.Get("nav.storage.unknown_ability");
			int charges = BuildingReflection.GetCycleAbilityCharges(itemIndex);
			string description = BuildingReflection.GetCycleAbilityDescription(itemIndex);

			string chargeText = charges > 0 ? Strings.Get("nav.storage.charges_count", charges) : Strings.Get("common.no_charges_remaining");

			if (!string.IsNullOrEmpty(description)) {
				Speech.Say(Strings.Get("nav.storage.ability_with_desc", abilityName, description, chargeText));
			} else {
				Speech.Say(Strings.Get("nav.storage.ability_plain", abilityName, chargeText));
			}
		}

		// ========================================
		// HAULERS SECTION
		// ========================================

		private void AnnounceHaulerItem(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _haulerRows.Count) return;
			Speech.Say(DescribeHaulerRow(_haulerRows[itemIndex]));
		}

		private string DescribeHaulerRow(HaulerRow row) {
			if (row.IsToggle) {
				bool on = IsHaulGroupEnabled(row.IsProduct);
				string state = Strings.Get(on ? "common.enabled_lower" : "common.disabled_lower");
				return Strings.Get(row.IsProduct ? "nav.storage.haul_products" : "nav.storage.haul_ingredients", state);
			}

			string text = Strings.Get("nav.storage.hauler_priority", row.DisplayName, row.Priority);

			// The game greys out priority rows whose group toggle is off.
			if (!IsHaulGroupEnabled(row.IsProduct))
				text = Strings.Get("nav.storage.hauler_priority_inactive", text);

			return text;
		}

		private static bool IsHaulGroupEnabled(bool isProduct) {
			return isProduct ? HaulersReflection.GetHaulProducts() : HaulersReflection.GetHaulIngredients();
		}

		/// <summary>
		/// Enter/Space on a hauler row. Toggle rows flip their group; priority rows have
		/// no action (they are adjusted with plus/minus instead).
		/// </summary>
		private bool ToggleHaulerRow(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _haulerRows.Count) return false;

			var row = _haulerRows[itemIndex];
			if (!row.IsToggle) return false;

			bool newValue = !IsHaulGroupEnabled(row.IsProduct);
			bool applied = row.IsProduct
				? HaulersReflection.SetHaulProducts(newValue)
				: HaulersReflection.SetHaulIngredients(newValue);

			if (!applied) return false;

			SoundManager.PlayButtonClick();
			Speech.Say(DescribeHaulerRow(row));
			return true;
		}

		private void AdjustHaulerPriority(int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers) {
			if (itemIndex < 0 || itemIndex >= _haulerRows.Count) return;

			var row = _haulerRows[itemIndex];
			if (row.IsToggle) return;

			int adjustedDelta = delta * (modifiers.Shift ? 10 : 1);
			int newPriority = Mathf.Clamp(row.Priority + adjustedDelta, HaulersReflection.MinPriority, HaulersReflection.MaxPriority);

			if (newPriority == row.Priority) {
				SoundManager.PlayFailed();
				return;
			}

			if (!HaulersReflection.SetPriority(row.IsProduct, row.TypeKey, newPriority))
				return;

			row.Priority = newPriority;

			SoundManager.PlayButtonClick();
			Speech.Say(DescribeHaulerRow(row));
		}

		protected override bool PerformItemAction(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return false;

			if (_sectionTypes[sectionIndex] == SectionType.Abilities) {
				return UseAbility(itemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Haulers) {
				return ToggleHaulerRow(itemIndex);
			}

			return false;
		}

		protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Workers)
				return Strings.Get("common.no_free_workers");
			return null;
		}

		private bool UseAbility(int abilityIndex) {
			if (abilityIndex >= _abilityCount) return false;

			int charges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
			if (charges <= 0) {
				Speech.Say(Strings.Get("common.no_charges_remaining"));
				return true;  // Still handled the action
			}

			string abilityName = BuildingReflection.GetCycleAbilityName(abilityIndex) ?? Strings.Get("nav.storage.ability_default");

			if (BuildingReflection.UseCycleAbility(abilityIndex)) {
				int newCharges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
				Speech.Say(Strings.Get("nav.storage.used_ability", abilityName, newCharges));

				// Refresh ability data in case charges changed
				RefreshAbilityData();
				return true;
			} else {
				Speech.Say(Strings.Get("nav.storage.cannot_use_ability", abilityName));
				return true;
			}
		}

		// ========================================
		// GOODS ADJUSTMENT (+/- RESERVE)
		// ========================================

		protected override void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			if (_sectionTypes[sectionIndex] == SectionType.Haulers) {
				AdjustHaulerPriority(itemIndex, delta, modifiers);
				return;
			}

			if (_sectionTypes[sectionIndex] != SectionType.Goods)
				return;

			if (itemIndex >= _goods.Count)
				return;

			var good = _goods[itemIndex];
			int adjustedDelta = delta * (modifiers.Shift ? 10 : 1);
			int newReserve = Math.Max(0, good.Reserve + adjustedDelta);

			RecipesReflection.SetStorageReserve(good.GoodName, newReserve);
			good.Reserve = newReserve;

			SoundManager.PlayButtonClick();
			Speech.Say(newReserve > 0 ? Strings.Get("nav.storage.reserve_line", newReserve) : Strings.Get("nav.storage.no_reserve"));
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
				case SectionType.Goods:
					return itemIndex < _goods.Count ? _goods[itemIndex].DisplayName : null;
				case SectionType.Workers:
					return _workersSection.GetItemName(itemIndex);
				case SectionType.Abilities:
					return itemIndex < _abilityCount ? BuildingReflection.GetCycleAbilityName(itemIndex) : null;
				case SectionType.Upgrades:
					return _upgradesSection.GetItemName(itemIndex);
				case SectionType.Haulers:
					return itemIndex < _haulerRows.Count ? GetHaulerRowName(_haulerRows[itemIndex]) : null;
				default:
					return null;
			}
		}

		private string GetHaulerRowName(HaulerRow row) {
			return row.IsToggle ? DescribeHaulerRow(row) : row.DisplayName;
		}

		protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return null;

			// Upgrades have sub-items
			if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
				return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);

			if (_sectionTypes[sectionIndex] == SectionType.Workers)
				return _workersSection.GetSubItemName(itemIndex, subItemIndex);

			return null;
		}
	}
}
