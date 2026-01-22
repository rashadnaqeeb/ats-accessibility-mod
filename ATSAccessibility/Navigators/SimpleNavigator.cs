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
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;
        private bool _hasUpgrades;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "SimpleNavigator";

        protected override string[] GetSections()
        {
            return _sectionNames;
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

            // Upgrades section
            if (_hasUpgrades && sectionIndex == 1)
            {
                return _upgradesSection.GetItemCount();
            }

            return 0;
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            // Upgrades section has sub-items (perks)
            if (_hasUpgrades && sectionIndex == 1)
            {
                return _upgradesSection.GetSubItemCount(itemIndex);
            }
            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
            {
                Speech.Say(_sectionNames[sectionIndex]);
            }
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex == 0)
            {
                string item = GetInfoItem(itemIndex);
                Speech.Say(item);
            }
            else if (_hasUpgrades && sectionIndex == 1)
            {
                _upgradesSection.AnnounceItem(itemIndex);
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_hasUpgrades && sectionIndex == 1)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_hasUpgrades && sectionIndex == 1)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Unknown building";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            // Build sections list
            var sections = new System.Collections.Generic.List<string>();
            sections.Add("Info");

            // Add Upgrades section if available
            _hasUpgrades = TryInitializeUpgradesSection();
            if (_hasUpgrades)
            {
                sections.Add("Upgrades");
            }

            _sectionNames = sections.ToArray();

            Debug.Log($"[ATSAccessibility] SimpleNavigator: Refreshed data for {_buildingName}");
        }

        protected override void ClearData()
        {
            _buildingName = null;
            _buildingDescription = null;
            _sectionNames = null;
            _hasUpgrades = false;
            ClearUpgradesSection();
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
            if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
                return _sectionNames[sectionIndex];
            return null;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex == 0)
            {
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

            if (_hasUpgrades && sectionIndex == 1)
            {
                return _upgradesSection.GetItemName(itemIndex);
            }

            return null;
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_hasUpgrades && sectionIndex == 1)
            {
                return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);
            }
            return null;
        }
    }
}
