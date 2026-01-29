using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for water-producing buildings (RainCatcher, Extractor).
    /// Both extend ProductionBuilding and have workers.
    /// Provides Status (toggle), Water, and Workers sections.
    /// </summary>
    public class WaterNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Status,
            Water,
            Workers,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private bool _isSleeping;
        private bool _canSleep;

        // Water data
        private string _waterTypeName;
        private bool _isRainCatcher;
        private bool _isExtractor;
        private float _productionTime;
        private int _producedAmount;
        private int _tankCurrent;
        private int _tankCapacity;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "WaterNavigator";

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
                case SectionType.Status:
                    return 0;
                case SectionType.Water:
                    return GetWaterItemCount();
                case SectionType.Workers:
                    return _workersSection.GetItemCount();
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemCount();
                default:
                    return 0;
            }
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Status)
            {
                string status = _isSleeping ? "Paused" : "Active";
                Speech.Say($"Status: {status}");
                return;
            }

            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Water:
                    AnnounceWaterItem(itemIndex);
                    break;
                case SectionType.Workers:
                    _workersSection.AnnounceItem(itemIndex);
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

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                return _workersSection.GetSubItemCount(itemIndex);
            }

            // Upgrades section has sub-items (perks)
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.GetSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
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

        protected override bool PerformSectionAction(int sectionIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Status)
            {
                if (!_canSleep)
                {
                    Speech.Say("Cannot pause this building");
                    return false;
                }

                bool wasSleeping = _isSleeping;
                if (BuildingReflection.ToggleBuildingSleep(_building))
                {
                    _isSleeping = !wasSleeping;
                    if (!wasSleeping)
                    {
                        // Workers were unassigned when pausing
                        _workersSection.RefreshWorkerIds();
                    }

                    if (_isSleeping)
                    {
                        SoundManager.PlayBuildingSleep();
                        Speech.Say("Paused");
                    }
                    else
                    {
                        SoundManager.PlayBuildingWakeUp();
                        Speech.Say("Active");
                    }
                    return true;
                }
                else
                {
                    SoundManager.PlayFailed();
                    Speech.Say("Cannot change building state");
                    return false;
                }
            }

            return false;
        }

        protected override void RefreshData()
        {
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _canSleep = BuildingReflection.CanBuildingSleep(_building);

            RefreshWaterData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] WaterNavigator: Refreshed data, water type: {_waterTypeName}");
        }

        protected override void ClearData()
        {
            _waterTypeName = null;
            _sectionNames = null;
            _sectionTypes = null;
            ClearWorkersSection();
            ClearUpgradesSection();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshWaterData()
        {
            _isRainCatcher = BuildingReflection.IsRainCatcher(_building);
            _isExtractor = BuildingReflection.IsExtractor(_building);

            if (_isRainCatcher)
            {
                _waterTypeName = BuildingReflection.GetRainCatcherWaterTypeName(_building);
            }
            else if (_isExtractor)
            {
                _waterTypeName = BuildingReflection.GetExtractorWaterTypeName(_building);
                _productionTime = BuildingReflection.GetExtractorProductionTime(_building);
                _producedAmount = BuildingReflection.GetExtractorProducedAmount(_building);
            }

            // Get tank levels
            _tankCurrent = BuildingReflection.GetWaterTankCurrent(_building);
            _tankCapacity = BuildingReflection.GetWaterTankCapacity(_building);
        }

        private void BuildSections()
        {
            var sections = new System.Collections.Generic.List<string>();
            var types = new System.Collections.Generic.List<SectionType>();

            // Always have Status
            sections.Add("Status");
            types.Add(SectionType.Status);

            // Water section
            sections.Add("Water");
            types.Add(SectionType.Water);

            // Workers section (only if building currently accepts worker assignment)
            if (TryInitializeWorkersSection())
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sections.Add("Upgrades");
                types.Add(SectionType.Upgrades);
            }

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // WATER SECTION
        // ========================================

        private int GetWaterItemCount()
        {
            int count = 1;  // Water type
            count++;  // Tank level (always present for water buildings)
            if (_isExtractor)
            {
                count += 2;  // Production time, Amount per cycle
            }
            return count;
        }

        private void AnnounceWaterItem(int itemIndex)
        {
            int index = 0;

            // Water type
            if (itemIndex == index)
            {
                string typeName = _waterTypeName ?? "Unknown";
                Speech.Say($"Water type: {typeName}");
                return;
            }
            index++;

            // Storage level (settlement-wide for this water type)
            if (itemIndex == index)
            {
                if (_tankCapacity > 0)
                {
                    int percent = (int)(((float)_tankCurrent / _tankCapacity) * 100);
                    Speech.Say($"Storage: {_tankCurrent} of {_tankCapacity} ({percent}%)");
                }
                else
                {
                    Speech.Say($"Storage: {_tankCurrent}");
                }
                return;
            }
            index++;

            if (_isExtractor)
            {
                // Production time
                if (itemIndex == index)
                {
                    string timeStr = FormatTime(_productionTime);
                    Speech.Say($"Production time: {timeStr}");
                    return;
                }
                index++;

                // Amount per cycle
                if (itemIndex == index)
                {
                    Speech.Say($"Amount per cycle: {_producedAmount}");
                    return;
                }
            }
        }

        private string FormatTime(float seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds:F0} seconds";
            }
            else
            {
                int minutes = (int)(seconds / 60);
                int remainingSeconds = (int)(seconds % 60);
                if (remainingSeconds == 0)
                {
                    return $"{minutes} minute{(minutes > 1 ? "s" : "")}";
                }
                return $"{minutes} minute{(minutes > 1 ? "s" : "")} {remainingSeconds} second{(remainingSeconds > 1 ? "s" : "")}";
            }
        }

    }
}
