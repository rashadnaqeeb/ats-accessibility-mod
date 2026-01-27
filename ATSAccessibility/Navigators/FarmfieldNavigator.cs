using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for individual farm field tiles.
    /// Provides a flat list of information: Name, Status (Empty/Plowed/Seeded), Expected Yield.
    /// Uses sections as the flat list items so Escape closes directly.
    /// </summary>
    public class FarmfieldNavigator : BuildingSectionNavigator
    {
        // ========================================
        // CACHED DATA
        // ========================================

        private string _buildingName;
        private bool _isPlowed;
        private bool _isSeeded;
        private string _cropName;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "FarmfieldNavigator";

        protected override string[] GetSections() => new[] { "Name", "Status" };

        protected override int GetItemCount(int sectionIndex)
        {
            // No sub-items - this is a flat list
            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            switch (sectionIndex)
            {
                case 0:
                    // Name
                    Speech.Say(_buildingName);
                    break;

                case 1:
                    // Status (visual state of the field)
                    if (_isSeeded)
                    {
                        Speech.Say($"Seeded with {_cropName}");
                    }
                    else if (_isPlowed)
                    {
                        Speech.Say("Plowed");
                    }
                    else
                    {
                        Speech.Say("Empty");
                    }
                    break;
            }
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            // No items - flat list uses sections only
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Farm Field";
            _isPlowed = BuildingReflection.IsFarmfieldPlowed(_building);
            _isSeeded = BuildingReflection.IsFarmfieldSeeded(_building);

            if (_isSeeded)
            {
                _cropName = BuildingReflection.GetFarmfieldCropName(_building) ?? "Unknown crop";
            }
            else
            {
                _cropName = null;
            }

            Debug.Log($"[ATSAccessibility] FarmfieldNavigator: Refreshed data - plowed={_isPlowed}, seeded={_isSeeded}, crop={_cropName}");
        }

        protected override void ClearData()
        {
            _buildingName = null;
            _isPlowed = false;
            _isSeeded = false;
            _cropName = null;
        }

        // ========================================
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
            switch (sectionIndex)
            {
                case 0: return "Name";
                case 1: return "Status";
                default: return null;
            }
        }
    }
}
