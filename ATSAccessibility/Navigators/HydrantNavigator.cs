using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Hydrant buildings.
    /// Hydrants extend Building (not ProductionBuilding) so they have no workers.
    /// Provides Info and Fuel sections.
    /// </summary>
    public class HydrantNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Fuel
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;

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
                case SectionType.Info:
                    return GetInfoItemCount();
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
                case SectionType.Info:
                    AnnounceInfoItem(itemIndex);
                    break;
                case SectionType.Fuel:
                    AnnounceFuelItem(itemIndex);
                    break;
            }
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Hydrant";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            RefreshFuelData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] HydrantNavigator: Refreshed data for {_buildingName}, cysts: {_freeCysts}, fuel: {_fuelAmount}");
        }

        protected override void ClearData()
        {
            _buildingName = null;
            _buildingDescription = null;
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
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

            // Always have Fuel section for Hydrant
            sections.Add("Fuel");
            types.Add(SectionType.Fuel);

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Always have name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;
            count++;  // Status
            return count;
        }

        private void AnnounceInfoItem(int itemIndex)
        {
            string item = GetInfoItem(itemIndex);
            Speech.Say(item);
        }

        // ========================================
        // INFO SECTION ITEMS
        // ========================================

        private string GetInfoItem(int itemIndex)
        {
            int index = 0;

            // Name
            if (itemIndex == index) return $"Name: {_buildingName}";
            index++;

            // Description (if present)
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index) return $"Description: {_buildingDescription}";
                index++;
            }

            // Status
            if (itemIndex == index)
            {
                if (!_isFinished)
                {
                    return "Status: Under construction";
                }
                else if (_isSleeping)
                {
                    return "Status: Paused";
                }
                else
                {
                    return "Status: Active";
                }
            }

            return "Unknown item";
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
