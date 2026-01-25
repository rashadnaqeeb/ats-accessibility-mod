using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Hydrant buildings.
    /// Hydrants extend Building (not ProductionBuilding) so they have no workers.
    /// Provides Fuel section only.
    /// </summary>
    public class HydrantNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Fuel
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;

        // Fuel data
        private int _freeCysts;
        private int _fuelAmount;
        private string _fuelDisplayName;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "HydrantNavigator";

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
                case SectionType.Fuel:
                    return 2;  // Cysts, Fuel amount
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
                case SectionType.Fuel:
                    AnnounceFuelItem(itemIndex);
                    break;
            }
        }

        protected override void RefreshData()
        {
            RefreshFuelData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] HydrantNavigator: Refreshed data, cysts: {_freeCysts}, fuel: {_fuelAmount}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshFuelData()
        {
            _freeCysts = BuildingReflection.GetBlightFreeCysts();
            _fuelAmount = BuildingReflection.GetBlightFuelAmount();
            _fuelDisplayName = BuildingReflection.GetBlightFuelName() ?? "Fuel";
        }

        private void BuildSections()
        {
            // Fuel is the only section
            _sectionNames = new[] { "Fuel" };
            _sectionTypes = new[] { SectionType.Fuel };
        }

        // ========================================
        // FUEL SECTION
        // ========================================

        private void AnnounceFuelItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    // Free cysts count
                    Speech.Say($"Free cysts: {_freeCysts}");
                    break;
                case 1:
                    // Fuel amount with status
                    string status = GetFuelStatus();
                    Speech.Say($"{_fuelDisplayName}: {_fuelAmount} ({status})");
                    break;
            }
        }

        private string GetFuelStatus()
        {
            if (_freeCysts == 0)
            {
                return "sufficient";
            }

            float ratio = (float)_fuelAmount / _freeCysts;
            if (ratio < 0.5f)
            {
                return "low";
            }
            else if (ratio < 1.0f)
            {
                return "medium";
            }
            else
            {
                return "high";
            }
        }
    }
}
