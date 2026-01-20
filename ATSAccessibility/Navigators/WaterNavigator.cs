using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for water-producing buildings (RainCatcher, Extractor).
    /// Both extend ProductionBuilding and have workers.
    /// Provides Info, Water, and Workers sections.
    /// </summary>
    public class WaterNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Water,
            Workers
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
        private bool _canSleep;

        // Water data
        private string _waterTypeName;
        private bool _isRainCatcher;
        private bool _isExtractor;
        private float _productionTime;
        private int _producedAmount;
        private int _tankCurrent;
        private int _tankCapacity;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;

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
                case SectionType.Info:
                    return GetInfoItemCount();
                case SectionType.Water:
                    return GetWaterItemCount();
                case SectionType.Workers:
                    return _maxWorkers;
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
                case SectionType.Water:
                    AnnounceWaterItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Info section has sub-items for Status (pause/resume)
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return GetInfoSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                AnnounceInfoSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Info)
            {
                return PerformInfoSubItemAction(itemIndex, subItemIndex);
            }

            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Water Building";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);
            _canSleep = BuildingReflection.CanBuildingSleep(_building);

            RefreshWaterData();
            RefreshWorkerData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] WaterNavigator: Refreshed data for {_buildingName}, water type: {_waterTypeName}");
        }

        protected override void ClearData()
        {
            _waterTypeName = null;
            _workerIds = null;
            _sectionNames = null;
            _sectionTypes = null;
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

        private void RefreshWorkerData()
        {
            _workerIds = BuildingReflection.GetWorkerIds(_building);
            _maxWorkers = BuildingReflection.GetMaxWorkers(_building);
        }

        private void BuildSections()
        {
            var sections = new System.Collections.Generic.List<string>();
            var types = new System.Collections.Generic.List<SectionType>();

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

            // Water section
            sections.Add("Water");
            types.Add(SectionType.Water);

            // Workers section (only if building has workplaces)
            if (_maxWorkers > 0)
            {
                sections.Add("Workers");
                types.Add(SectionType.Workers);
            }

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
            count++;  // Status
            return count;
        }

        private int GetStatusItemIndex()
        {
            return string.IsNullOrEmpty(_buildingDescription) ? 1 : 2;
        }

        private int GetInfoSubItemCount(int itemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep)
            {
                return 1;  // Pause/Resume toggle
            }
            return 0;
        }

        private void AnnounceInfoItem(int itemIndex)
        {
            int index = 0;

            // Name
            if (itemIndex == index)
            {
                Speech.Say($"Name: {_buildingName}");
                return;
            }
            index++;

            // Description (if present)
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index)
                {
                    Speech.Say($"Description: {_buildingDescription}");
                    return;
                }
                index++;
            }

            // Status
            if (itemIndex == index)
            {
                string status;
                if (!_isFinished)
                {
                    status = "Under construction";
                }
                else if (_isSleeping)
                {
                    status = "Paused";
                }
                else
                {
                    status = "Active";
                }

                string announcement = $"Status: {status}";
                if (_canSleep)
                {
                    announcement += _isSleeping ? ", Enter to resume" : ", Enter to pause";
                }
                Speech.Say(announcement);
            }
        }

        private void AnnounceInfoSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep && subItemIndex == 0)
            {
                if (_isSleeping)
                {
                    Speech.Say("Resume building, Enter to confirm");
                }
                else
                {
                    Speech.Say("Pause building, workers will be unassigned, Enter to confirm");
                }
            }
        }

        private bool PerformInfoSubItemAction(int itemIndex, int subItemIndex)
        {
            if (itemIndex == GetStatusItemIndex() && _canSleep && subItemIndex == 0)
            {
                bool wasSleeping = _isSleeping;
                if (BuildingReflection.ToggleBuildingSleep(_building))
                {
                    _isSleeping = !wasSleeping;
                    if (!wasSleeping)
                    {
                        _workerIds = BuildingReflection.GetWorkerIds(_building);
                    }
                    Speech.Say(_isSleeping ? "Building paused" : "Building resumed");
                    return true;
                }
                else
                {
                    Speech.Say("Cannot change building state");
                    return false;
                }
            }
            return false;
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

            // Tank level
            if (itemIndex == index)
            {
                if (_tankCapacity > 0)
                {
                    int percent = (int)(((float)_tankCurrent / _tankCapacity) * 100);
                    Speech.Say($"Tank: {_tankCurrent} of {_tankCapacity} ({percent}%)");
                }
                else
                {
                    Speech.Say($"Tank: {_tankCurrent}");
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

        // ========================================
        // WORKERS SECTION
        // ========================================

        private void AnnounceWorkerItem(int itemIndex)
        {
            if (itemIndex >= _maxWorkers) return;

            bool isEmpty = BuildingReflection.IsWorkerSlotEmpty(_building, itemIndex);

            if (isEmpty)
            {
                Speech.Say($"Slot {itemIndex + 1} of {_maxWorkers}: Empty");
            }
            else
            {
                int workerId = _workerIds[itemIndex];
                string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
                Speech.Say($"Slot {itemIndex + 1} of {_maxWorkers}: {workerDesc ?? "Assigned"}");
            }
        }
    }
}
