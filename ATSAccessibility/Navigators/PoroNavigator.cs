using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Poro buildings (creature care).
    /// Poros extend Building (not ProductionBuilding) and have no workers.
    /// Provides Info, Happiness, Needs, and Product sections.
    /// </summary>
    public class PoroNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Happiness,
            Needs,
            Product
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

        // Happiness data
        private float _happiness;
        private float _productionProgress;

        // Needs data
        private List<NeedInfo> _needs = new List<NeedInfo>();

        // Product data
        private string _productName;
        private int _productAmount;
        private int _maxProducts;
        private bool _canGather;

        // ========================================
        // NEED INFO STRUCT
        // ========================================

        private struct NeedInfo
        {
            public int NeedIndex;
            public string NeedName;
            public float Level;
            public string CurrentGoodName;
            public int AvailableGoodsCount;
            public bool CanFulfill;
        }

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "PoroNavigator";

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
                case SectionType.Happiness:
                    return 2;  // Happiness level, Production progress
                case SectionType.Needs:
                    return _needs.Count > 0 ? _needs.Count : 1;
                case SectionType.Product:
                    return 1;  // Product info (amount ready)
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Needs have sub-items (Feed, Change good options)
            if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count)
            {
                var need = _needs[itemIndex];
                int count = 0;
                if (need.CanFulfill) count++;  // Feed action
                if (need.AvailableGoodsCount > 1) count += need.AvailableGoodsCount;  // Good options
                return count;
            }

            // Product has sub-item (Collect action if products ready)
            if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather)
            {
                return 1;  // Collect action
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
                case SectionType.Info:
                    AnnounceInfoItem(itemIndex);
                    break;
                case SectionType.Happiness:
                    AnnounceHappinessItem(itemIndex);
                    break;
                case SectionType.Needs:
                    AnnounceNeedItem(itemIndex);
                    break;
                case SectionType.Product:
                    AnnounceProductItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count)
            {
                var need = _needs[itemIndex];
                int subIndex = subItemIndex;

                // First sub-item is Feed action (if available)
                if (need.CanFulfill)
                {
                    if (subIndex == 0)
                    {
                        Speech.Say("Feed, Enter to feed");
                        return;
                    }
                    subIndex--;
                }

                // Remaining sub-items are good options
                if (need.AvailableGoodsCount > 1 && subIndex < need.AvailableGoodsCount)
                {
                    string goodName = BuildingReflection.GetPoroNeedAvailableGoodName(_building, need.NeedIndex, subIndex);
                    Speech.Say($"Change to {goodName ?? "Unknown"}, Enter to select");
                }
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather)
            {
                Speech.Say($"Collect {_productAmount} {_productName ?? "products"}, Enter to collect");
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count)
            {
                var need = _needs[itemIndex];
                int subIndex = subItemIndex;

                // First sub-item is Feed action (if available)
                if (need.CanFulfill)
                {
                    if (subIndex == 0)
                    {
                        if (BuildingReflection.FulfillPoroNeed(_building, need.NeedIndex))
                        {
                            Speech.Say("Fed successfully");
                            RefreshNeedData();
                            return true;
                        }
                        else
                        {
                            Speech.Say("Cannot feed");
                            return false;
                        }
                    }
                    subIndex--;
                }

                // Remaining sub-items are good options
                if (need.AvailableGoodsCount > 1 && subIndex < need.AvailableGoodsCount)
                {
                    if (BuildingReflection.ChangePoroNeedGood(_building, need.NeedIndex, subIndex))
                    {
                        string goodName = BuildingReflection.GetPoroNeedAvailableGoodName(_building, need.NeedIndex, subIndex);
                        Speech.Say($"Changed to {goodName ?? "Unknown"}");
                        RefreshNeedData();
                        return true;
                    }
                }
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather)
            {
                if (BuildingReflection.GatherPoroProducts(_building))
                {
                    Speech.Say($"Collected {_productAmount} {_productName ?? "products"}");
                    RefreshProductData();
                    return true;
                }
                else
                {
                    Speech.Say("Cannot collect");
                    return false;
                }
            }
            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Poro";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            RefreshHappinessData();
            RefreshNeedData();
            RefreshProductData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] PoroNavigator: Refreshed data - happiness {_happiness:P0}, {_needs.Count} needs");
        }

        protected override void ClearData()
        {
            _needs.Clear();
            _sectionNames = null;
            _sectionTypes = null;
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshHappinessData()
        {
            _happiness = BuildingReflection.GetPoroHappiness(_building);
            _productionProgress = BuildingReflection.GetPoroProductionProgress(_building);
        }

        private void RefreshNeedData()
        {
            _needs.Clear();

            int needCount = BuildingReflection.GetPoroNeedCount(_building);
            for (int i = 0; i < needCount; i++)
            {
                var need = new NeedInfo
                {
                    NeedIndex = i,
                    NeedName = BuildingReflection.GetPoroNeedName(_building, i),
                    Level = BuildingReflection.GetPoroNeedLevel(_building, i),
                    CurrentGoodName = BuildingReflection.GetPoroNeedCurrentGoodName(_building, i),
                    AvailableGoodsCount = BuildingReflection.GetPoroNeedAvailableGoodsCount(_building, i),
                    CanFulfill = BuildingReflection.CanFulfillPoroNeed(_building, i)
                };
                _needs.Add(need);
            }
        }

        private void RefreshProductData()
        {
            _productName = BuildingReflection.GetPoroProductName(_building);
            _productAmount = BuildingReflection.GetPoroProductAmount(_building);
            _maxProducts = BuildingReflection.GetPoroMaxProducts(_building);
            _canGather = BuildingReflection.CanGatherPoroProducts(_building);
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

            // Happiness section
            sections.Add("Happiness");
            types.Add(SectionType.Happiness);

            // Needs section
            sections.Add("Needs");
            types.Add(SectionType.Needs);

            // Product section
            sections.Add("Product");
            types.Add(SectionType.Product);

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
                if (!_isFinished)
                {
                    Speech.Say("Status: Under construction");
                }
                else if (_isSleeping)
                {
                    Speech.Say("Status: Paused");
                }
                else
                {
                    Speech.Say("Status: Active");
                }
            }
        }

        // ========================================
        // HAPPINESS SECTION
        // ========================================

        private void AnnounceHappinessItem(int itemIndex)
        {
            if (itemIndex == 0)
            {
                Speech.Say($"Happiness: {_happiness:P0}");
            }
            else if (itemIndex == 1)
            {
                Speech.Say($"Production progress: {_productionProgress:P0}");
            }
        }

        // ========================================
        // NEEDS SECTION
        // ========================================

        private void AnnounceNeedItem(int itemIndex)
        {
            if (_needs.Count == 0)
            {
                Speech.Say("No needs");
                return;
            }

            if (itemIndex < _needs.Count)
            {
                var need = _needs[itemIndex];
                string needName = need.NeedName ?? $"Need {need.NeedIndex + 1}";
                string levelPercent = $"{need.Level:P0}";
                string currentGood = need.CurrentGoodName ?? "Unknown";

                string announcement = $"{needName}: {levelPercent}, using {currentGood}";

                // Add hints for available actions
                var hints = new List<string>();
                if (need.CanFulfill) hints.Add("can feed");
                if (need.AvailableGoodsCount > 1) hints.Add("can change good");

                if (hints.Count > 0)
                {
                    announcement += ", Enter to " + string.Join(" or ", hints);
                }

                Speech.Say(announcement);
            }
        }

        // ========================================
        // PRODUCT SECTION
        // ========================================

        private void AnnounceProductItem(int itemIndex)
        {
            if (itemIndex == 0)
            {
                string productName = _productName ?? "Product";
                string announcement = $"{productName}: {_productAmount} of {_maxProducts} ready";

                if (_canGather)
                {
                    announcement += ", Enter to collect";
                }
                else if (_productAmount == 0)
                {
                    announcement += " (none ready)";
                }

                Speech.Say(announcement);
            }
        }
    }
}
