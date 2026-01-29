using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for the main Storage building (warehouse).
    /// Provides navigation through Goods, Workers, Abilities, and Upgrades sections.
    /// </summary>
    public class StorageNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Goods,
            Workers,
            Abilities,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;

        // Goods data (from global storage)
        private List<(string goodName, string displayName, int amount)> _goods = new List<(string, string, int)>();

        // Abilities data
        private int _abilityCount;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "StorageNavigator";

        protected override string[] GetSections()
        {
            return _sectionNames;
        }

        protected override int GetItemCount(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Goods:
                    return _goods.Count > 0 ? _goods.Count : 1;  // At least 1 for "Empty" message
                case SectionType.Workers:
                    return _workersSection.GetItemCount();
                case SectionType.Abilities:
                    return _abilityCount > 0 ? _abilityCount : 1;  // At least 1 for "No abilities" message
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemCount();
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                return _workersSection.GetSubItemCount(itemIndex);
            }

            // Upgrades have sub-items (perks)
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.GetSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
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
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                _workersSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                if (_workersSection.PerformSubItemAction(itemIndex, subItemIndex))
                {
                    _navigationLevel = 1;
                    return true;
                }
                return false;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
            RefreshGoodsData();
            RefreshAbilityData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] StorageNavigator: Refreshed data - {_goods.Count} goods, {_workersSection.MaxWorkers} worker slots, {_abilityCount} abilities");
        }

        protected override void ClearData()
        {
            _goods.Clear();
            _sectionNames = null;
            _sectionTypes = null;
            ClearWorkersSection();
            ClearUpgradesSection();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshGoodsData()
        {
            _goods.Clear();

            // Get goods from the global storage
            var storageGoods = GameReflection.GetStorageGoods();
            foreach (var kvp in storageGoods)
            {
                string displayName = GameReflection.GetGoodDisplayName(kvp.Key) ?? kvp.Key;
                _goods.Add((kvp.Key, displayName, kvp.Value));
            }

            // Sort by display name for easier navigation
            _goods.Sort((a, b) => string.Compare(a.displayName, b.displayName));
        }

        private void RefreshAbilityData()
        {
            _abilityCount = BuildingReflection.GetCycleAbilityCount();
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Goods section
            sections.Add("Goods");
            types.Add(SectionType.Goods);

            // Abilities section (only if abilities exist)
            if (_abilityCount > 0)
            {
                sections.Add("Abilities");
                types.Add(SectionType.Abilities);
            }

            // Workers section (only if building currently accepts worker assignment)
            if (TryInitializeWorkersSection())
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
            }

            // Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sections.Add("Upgrades");
                types.Add(SectionType.Upgrades);
            }

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // GOODS SECTION
        // ========================================

        private void AnnounceGoodItem(int itemIndex)
        {
            // Refresh goods data to get current amounts
            RefreshGoodsData();

            if (_goods.Count == 0)
            {
                Speech.Say("Storage is empty");
                return;
            }

            if (itemIndex < _goods.Count)
            {
                var good = _goods[itemIndex];
                Speech.Say($"{good.displayName}: {good.amount}");
            }
        }

        // ========================================
        // ABILITIES SECTION
        // ========================================

        private void AnnounceAbilityItem(int itemIndex)
        {
            if (_abilityCount == 0)
            {
                Speech.Say("No abilities available");
                return;
            }

            if (itemIndex >= _abilityCount) return;

            string abilityName = BuildingReflection.GetCycleAbilityName(itemIndex) ?? "Unknown ability";
            int charges = BuildingReflection.GetCycleAbilityCharges(itemIndex);

            if (charges > 0)
            {
                Speech.Say($"{abilityName}: {charges} charges");
            }
            else
            {
                Speech.Say($"{abilityName}: No charges remaining");
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            if (_sectionTypes[sectionIndex] == SectionType.Abilities)
            {
                return UseAbility(itemIndex);
            }

            return false;
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        private bool UseAbility(int abilityIndex)
        {
            if (abilityIndex >= _abilityCount) return false;

            int charges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
            if (charges <= 0)
            {
                Speech.Say("No charges remaining");
                return true;  // Still handled the action
            }

            string abilityName = BuildingReflection.GetCycleAbilityName(abilityIndex) ?? "ability";

            if (BuildingReflection.UseCycleAbility(abilityIndex))
            {
                int newCharges = BuildingReflection.GetCycleAbilityCharges(abilityIndex);
                Speech.Say($"Used {abilityName}. {newCharges} charges remaining");

                // Refresh ability data in case charges changed
                RefreshAbilityData();
                return true;
            }
            else
            {
                Speech.Say($"Cannot use {abilityName}");
                return true;
            }
        }

        // ========================================
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
            if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
                return _sectionNames[sectionIndex];
            return null;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Goods:
                    return itemIndex < _goods.Count ? _goods[itemIndex].displayName : null;
                case SectionType.Workers:
                    return _workersSection.GetItemName(itemIndex);
                case SectionType.Abilities:
                    return itemIndex < _abilityCount ? BuildingReflection.GetCycleAbilityName(itemIndex) : null;
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
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
