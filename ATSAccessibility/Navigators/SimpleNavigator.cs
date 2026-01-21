using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Simple navigator for buildings without specialized navigation needs.
    /// Provides basic Info section with building name, description, and status.
    /// Used for: Storage, Decoration, and other non-production buildings.
    /// </summary>
    public class SimpleNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTIONS
        // ========================================

        private static readonly string[] SECTIONS = new[] { "Info" };

        // ========================================
        // CACHED DATA
        // ========================================

        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "SimpleNavigator";

        protected override string[] GetSections()
        {
            return SECTIONS;
        }

        protected override int GetItemCount(int sectionIndex)
        {
            // Info section has items: Name, Description, Status
            if (sectionIndex == 0)
            {
                int count = 1;  // Always have name
                if (!string.IsNullOrEmpty(_buildingDescription)) count++;
                count++;  // Status
                return count;
            }
            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (sectionIndex == 0)
            {
                Speech.Say("Info");
            }
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex == 0)
            {
                string item = GetInfoItem(itemIndex);
                Speech.Say(item);
            }
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Unknown building";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            Debug.Log($"[ATSAccessibility] SimpleNavigator: Refreshed data for {_buildingName}");
        }

        protected override void ClearData()
        {
            _buildingName = null;
            _buildingDescription = null;
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
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
            return sectionIndex == 0 ? "Info" : null;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex != 0) return null;

            int index = 0;
            if (itemIndex == index) return "Name";
            index++;
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index) return "Description";
                index++;
            }
            if (itemIndex == index) return "Status";
            return null;
        }
    }
}
