using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Institution buildings (Tavern, Temple, etc.).
    /// Institutions extend ProductionBuilding and have workers.
    /// Provides Status (toggle), Services (recipes), Storage, and Workers sections.
    /// </summary>
    public class InstitutionNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Status,
            Effects,
            Services,
            Storage,
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

        // Service/Recipe data
        private List<ServiceInfo> _services = new List<ServiceInfo>();

        // Storage data
        private List<(string goodName, string displayName, int amount)> _storageGoods = new List<(string, string, int)>();

        // Effects data
        private int _effectCount;

        // ========================================
        // SERVICE INFO STRUCT
        // ========================================

        private struct ServiceInfo
        {
            public int RecipeIndex;
            public string ServedNeedName;
            public bool ConsumesGood;
            public string CurrentGoodName;
            public int AvailableGoodsCount;
        }

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "InstitutionNavigator";

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
                case SectionType.Effects:
                    return _effectCount;
                case SectionType.Services:
                    return _services.Count > 0 ? _services.Count : 1;
                case SectionType.Storage:
                    return _storageGoods.Count > 0 ? _storageGoods.Count : 1;
                case SectionType.Workers:
                    return _workersSection.GetItemCount();
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

            // Services have sub-items (available ingredients to choose from)
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                // Only if it consumes goods and has multiple options
                if (service.ConsumesGood && service.AvailableGoodsCount > 1)
                {
                    return service.AvailableGoodsCount;
                }
            }

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
                case SectionType.Effects:
                    AnnounceEffectItem(itemIndex);
                    break;
                case SectionType.Services:
                    AnnounceServiceItem(itemIndex);
                    break;
                case SectionType.Storage:
                    AnnounceStorageItem(itemIndex);
                    break;
                case SectionType.Workers:
                    _workersSection.AnnounceItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                string goodName = BuildingReflection.GetInstitutionAvailableGoodName(_building, service.RecipeIndex, subItemIndex);
                Speech.Say($"Option {subItemIndex + 1}: {goodName ?? "Unknown"}");
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Workers)
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
            if (_sectionTypes[sectionIndex] == SectionType.Services && itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                if (BuildingReflection.ChangeInstitutionIngredient(_building, service.RecipeIndex, subItemIndex))
                {
                    string goodName = BuildingReflection.GetInstitutionAvailableGoodName(_building, service.RecipeIndex, subItemIndex);
                    Speech.Say($"Changed to {goodName ?? "Unknown"}");
                    RefreshServiceData();  // Refresh to update current good
                    return true;
                }
            }

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
            _effectCount = BuildingReflection.GetInstitutionEffectCount(_building);

            RefreshServiceData();
            RefreshStorageData();
            BuildSections();

            Debug.Log($"[ATSAccessibility] InstitutionNavigator: Refreshed data - {_effectCount} effects, {_services.Count} services, {_storageGoods.Count} storage goods");
        }

        protected override void ClearData()
        {
            _services.Clear();
            _storageGoods.Clear();
            ClearWorkersSection();
            _sectionNames = null;
            _sectionTypes = null;
            ClearUpgradesSection();
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshServiceData()
        {
            _services.Clear();

            int recipeCount = BuildingReflection.GetInstitutionRecipeCount(_building);
            for (int i = 0; i < recipeCount; i++)
            {
                var service = new ServiceInfo
                {
                    RecipeIndex = i,
                    ServedNeedName = BuildingReflection.GetInstitutionServedNeedName(_building, i),
                    ConsumesGood = BuildingReflection.IsInstitutionRecipeGoodConsumed(_building, i),
                    CurrentGoodName = BuildingReflection.GetInstitutionCurrentGoodName(_building, i),
                    AvailableGoodsCount = BuildingReflection.GetInstitutionAvailableGoodsCount(_building, i)
                };
                _services.Add(service);
            }
        }

        private void RefreshStorageData()
        {
            _storageGoods.Clear();

            var storageGoods = BuildingReflection.GetInstitutionStorageGoods(_building);
            foreach (var kvp in storageGoods)
            {
                string displayName = GameReflection.GetGoodDisplayName(kvp.Key) ?? kvp.Key;
                _storageGoods.Add((kvp.Key, displayName, kvp.Value));
            }

            // Sort by display name
            _storageGoods.Sort((a, b) => string.Compare(a.displayName, b.displayName));
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Status
            sections.Add("Status");
            types.Add(SectionType.Status);

            // Effects section (only if institution has effects)
            if (_effectCount > 0)
            {
                sections.Add("Effects");
                types.Add(SectionType.Effects);
            }

            // Services section
            sections.Add("Services");
            types.Add(SectionType.Services);

            // Storage section
            sections.Add("Storage");
            types.Add(SectionType.Storage);

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
        // EFFECTS SECTION
        // ========================================

        private void AnnounceEffectItem(int itemIndex)
        {
            if (itemIndex >= _effectCount) return;

            string name = BuildingReflection.GetInstitutionEffectName(_building, itemIndex);
            int minWorkers = BuildingReflection.GetInstitutionEffectMinWorkers(_building, itemIndex);
            bool isActive = BuildingReflection.IsInstitutionEffectActive(_building, itemIndex);
            string description = BuildingReflection.GetInstitutionEffectDescription(_building, itemIndex);

            string activation = isActive ? "active" : $"requires {minWorkers} workers";

            if (!string.IsNullOrEmpty(description))
                Speech.Say($"{name}, {activation}, {description}");
            else
                Speech.Say($"{name}, {activation}");
        }

        // ========================================
        // SERVICES SECTION
        // ========================================

        private void AnnounceServiceItem(int itemIndex)
        {
            if (_services.Count == 0)
            {
                Speech.Say("No services available");
                return;
            }

            if (itemIndex < _services.Count)
            {
                var service = _services[itemIndex];
                string needName = service.ServedNeedName ?? "Unknown need";

                if (service.ConsumesGood)
                {
                    string goodName = service.CurrentGoodName ?? "Unknown";
                    Speech.Say($"{needName}: using {goodName}");
                }
                else
                {
                    Speech.Say($"{needName}: Free service (no consumption)");
                }
            }
        }

        // ========================================
        // STORAGE SECTION
        // ========================================

        private void AnnounceStorageItem(int itemIndex)
        {
            // Refresh storage data to get current amounts
            RefreshStorageData();

            if (_storageGoods.Count == 0)
            {
                Speech.Say("Storage is empty");
                return;
            }

            if (itemIndex < _storageGoods.Count)
            {
                var good = _storageGoods[itemIndex];
                Speech.Say($"{good.displayName}: {good.amount}");
            }
        }

    }
}
