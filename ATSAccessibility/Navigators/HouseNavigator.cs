using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for House buildings (Shelters).
    /// Provides navigation through Residents and Upgrades sections.
    /// </summary>
    public class HouseNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Residents,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;

        // Residents data
        private List<int> _residentIds = new List<int>();
        private int _currentCapacity;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "HouseNavigator";

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
                case SectionType.Residents:
                    // Item 0: Capacity, Items 1+: residents (or "None" if empty)
                    return 1 + (_residentIds.Count > 0 ? _residentIds.Count : 1);
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemCount();
                default:
                    return 0;
            }
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
                case SectionType.Residents:
                    AnnounceResidentItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemCount(itemIndex);

            return 0;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void RefreshData()
        {
            // Resident data
            _residentIds = BuildingReflection.GetHouseResidents(_building);
            _currentCapacity = BuildingReflection.GetHouseCapacity(_building);

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Residents");
            sectionTypes.Add(SectionType.Residents);

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] HouseNavigator: Refreshed data, {_residentIds.Count} residents");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _residentIds.Clear();
            ClearUpgradesSection();
        }

        // ========================================
        // RESIDENTS SECTION
        // ========================================

        private void AnnounceResidentItem(int itemIndex)
        {
            // Item 0: Capacity
            if (itemIndex == 0)
            {
                Speech.Say($"Capacity: {_residentIds.Count} of {_currentCapacity}");
                return;
            }

            // Items 1+: Residents
            int residentIndex = itemIndex - 1;

            if (_residentIds.Count == 0)
            {
                Speech.Say("None");
                return;
            }

            if (residentIndex >= _residentIds.Count)
            {
                Speech.Say("Invalid resident");
                return;
            }

            int residentId = _residentIds[residentIndex];
            var actor = BuildingReflection.GetActor(residentId);

            if (actor != null)
            {
                string name = BuildingReflection.GetActorName(actor) ?? "Unknown";
                string race = BuildingReflection.GetActorRace(actor);

                if (!string.IsNullOrEmpty(race))
                    Speech.Say($"{name}, {race}");
                else
                    Speech.Say(name);
            }
            else
            {
                Speech.Say("Unknown villager");
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
                case SectionType.Residents:
                    return GetResidentItemName(itemIndex);
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

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);

            return null;
        }

        private string GetResidentItemName(int itemIndex)
        {
            // Item 0 is Capacity
            if (itemIndex == 0)
                return "Capacity";

            int residentIndex = itemIndex - 1;

            if (_residentIds.Count == 0)
                return "None";

            if (residentIndex >= _residentIds.Count)
                return null;

            int residentId = _residentIds[residentIndex];
            var actor = BuildingReflection.GetActor(residentId);

            if (actor != null)
            {
                return BuildingReflection.GetActorName(actor) ?? "Unknown";
            }
            return null;
        }
    }
}
