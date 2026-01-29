using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Hearth buildings (Ancient Hearth, Small Hearth).
    /// Provides navigation through Fire, Sacrifice, Upgrades, Blight, and Workers sections.
    /// </summary>
    public class HearthNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Fire,
            Sacrifice,
            Services,   // The Commons (hearth services)
            Upgrades,
            Blight,
            Workers
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private bool _isMainHearth;

        // Fire data
        private float _fuelLevel;  // 0-1
        private float _fuelTimeRemaining;
        private bool _isFireLow;
        private bool _isFireOut;

        // Upgrades data
        private List<BuildingReflection.HearthUpgradeInfo> _upgradeInfo = new List<BuildingReflection.HearthUpgradeInfo>();

        // Blight data
        private float _corruptionRate;

        // Sacrifice data
        private List<object> _sacrificeRecipes = new List<object>();
        private List<BuildingReflection.SacrificeRecipeInfo> _sacrificeInfo = new List<BuildingReflection.SacrificeRecipeInfo>();

        // Fuel data
        private List<BuildingReflection.FuelInfo> _fuelTypes = new List<BuildingReflection.FuelInfo>();

        // Services data (The Commons)
        private bool _servicesMetaUnlocked = false;
        private bool _servicesSettlementUnlocked = false;
        private List<BuildingReflection.HearthServiceInfo> _serviceRecipes = new List<BuildingReflection.HearthServiceInfo>();

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        public HearthNavigator()
        {
            _workersSection.GetWorkerIdsFunc = BuildingReflection.GetHearthWorkerIds;
        }

        protected override string NavigatorName => "HearthNavigator";

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
                case SectionType.Fire:
                    return GetFireItemCount();
                case SectionType.Sacrifice:
                    return _sacrificeRecipes.Count;
                case SectionType.Services:
                    if (!_servicesSettlementUnlocked)
                        return 1;  // Just the unlock option
                    return _serviceRecipes.Count;
                case SectionType.Upgrades:
                    return _upgradeInfo.Count;
                case SectionType.Blight:
                    return 1;  // Just corruption level
                case SectionType.Workers:
                    return _workersSection.GetItemCount();
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Fire section: Fuel types item (index 2) has sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return _fuelTypes.Count;
            }

            // Workers have sub-items (races to assign, plus unassign if occupied)
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                return _workersSection.GetSubItemCount(itemIndex);
            }

            // Sacrifice uses +/- keys, no sub-items
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
                case SectionType.Fire:
                    AnnounceFireItem(itemIndex);
                    break;
                case SectionType.Sacrifice:
                    AnnounceSacrificeItem(itemIndex);
                    break;
                case SectionType.Services:
                    AnnounceServiceItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    AnnounceUpgradeItem(itemIndex);
                    break;
                case SectionType.Blight:
                    AnnounceBlightItem(itemIndex);
                    break;
                case SectionType.Workers:
                    _workersSection.AnnounceItem(itemIndex);
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
            // Fire section: Fuel types sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                AnnounceFuelSubItem(subItemIndex);
                return;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                _workersSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            // Services unlock action
            if (_sectionTypes[sectionIndex] == SectionType.Services && !_servicesSettlementUnlocked && itemIndex == 0)
            {
                if (!BuildingReflection.CanAffordHearthServicesUnlock(_building))
                {
                    Speech.Say("Not enough resources");
                    SoundManager.PlayFailed();
                    return false;
                }

                if (BuildingReflection.UnlockHearthServices(_building))
                {
                    _servicesSettlementUnlocked = true;
                    _serviceRecipes = BuildingReflection.GetHearthServiceRecipes(_building);
                    SoundManager.PlayButtonClick();
                    Speech.Say("The Commons unlocked");
                    return true;
                }
                else
                {
                    Speech.Say("Cannot unlock");
                    SoundManager.PlayFailed();
                    return false;
                }
            }

            return false;
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            // Fire section: Toggle fuel type
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return ToggleFuel(subItemIndex);
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
            return false;
        }

        protected override void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            // Sacrifice section uses +/- to adjust level
            if (_sectionTypes[sectionIndex] == SectionType.Sacrifice)
            {
                AdjustSacrificeLevel(itemIndex, delta);
            }
        }

        protected override void RefreshData()
        {
            _isMainHearth = BuildingReflection.IsMainHearth(_building);

            // Fire data
            _fuelLevel = BuildingReflection.GetHearthFireLevel(_building);
            _fuelTimeRemaining = BuildingReflection.GetHearthFuelTimeRemaining(_building);
            _isFireLow = BuildingReflection.IsHearthFireLow(_building);
            _isFireOut = BuildingReflection.IsHearthFireOut(_building);

            // Upgrades data
            _upgradeInfo = BuildingReflection.GetHearthUpgradeInfo(_building);

            // Blight data
            _corruptionRate = BuildingReflection.GetHearthCorruptionRate(_building);

            // Sacrifice data
            _sacrificeRecipes = BuildingReflection.GetHearthSacrificeRecipes(_building);
            RefreshSacrificeInfo();

            // Fuel data
            _fuelTypes = BuildingReflection.GetAllFuelTypes();

            // Services data (The Commons)
            _servicesMetaUnlocked = BuildingReflection.AreHearthServicesMetaUnlocked();
            if (_servicesMetaUnlocked)
            {
                _servicesSettlementUnlocked = BuildingReflection.AreHearthServicesEnabled(_building);
                if (_servicesSettlementUnlocked)
                {
                    _serviceRecipes = BuildingReflection.GetHearthServiceRecipes(_building);
                }
            }

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Fire");
            sectionTypes.Add(SectionType.Fire);

            // Sacrifice section only shown if there are sacrifice recipes
            if (_sacrificeRecipes.Count > 0)
            {
                sectionNames.Add("Sacrifice");
                sectionTypes.Add(SectionType.Sacrifice);
            }

            // Services section only shown on main hearth if meta progression unlocked
            if (_servicesMetaUnlocked && _isMainHearth)
            {
                sectionNames.Add("Services");
                sectionTypes.Add(SectionType.Services);
            }

            // Upgrades section only shown if there are upgrade tiers
            if (_upgradeInfo.Count > 0)
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            // Blight section only shown for main hearth when blight is active
            if (_isMainHearth && GameReflection.IsBlightActive())
            {
                sectionNames.Add("Blight");
                sectionTypes.Add(SectionType.Blight);
            }

            if (TryInitializeWorkersSection())
            {
                sectionNames.Add("Workers");
                sectionTypes.Add(SectionType.Workers);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] HearthNavigator: Refreshed data, {_sectionNames.Length} sections");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            ClearWorkersSection();
            _sacrificeRecipes.Clear();
            _sacrificeInfo.Clear();
            _fuelTypes.Clear();
            _upgradeInfo.Clear();
            _servicesMetaUnlocked = false;
            _servicesSettlementUnlocked = false;
            _serviceRecipes.Clear();
        }

        // ========================================
        // FIRE SECTION
        // ========================================

        private int GetFireItemCount()
        {
            return 3;  // Fuel level, Time remaining, Fuel types
        }

        private void AnnounceFireItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    int percentage = Mathf.RoundToInt(_fuelLevel * 100f);
                    Speech.Say($"Fuel level: {percentage} percent");
                    break;
                case 1:
                    int seconds = Mathf.RoundToInt(_fuelTimeRemaining);
                    if (seconds <= 0)
                    {
                        Speech.Say("Time remaining: Fire is out");
                    }
                    else if (seconds < 60)
                    {
                        Speech.Say($"Time remaining: {seconds} seconds");
                    }
                    else
                    {
                        int minutes = seconds / 60;
                        int remainingSecs = seconds % 60;
                        if (remainingSecs > 0)
                            Speech.Say($"Time remaining: {minutes} minutes {remainingSecs} seconds");
                        else
                            Speech.Say($"Time remaining: {minutes} minutes");
                    }
                    break;
                case 2:
                    // Fuel types submenu
                    int enabledCount = 0;
                    foreach (var fuel in _fuelTypes)
                    {
                        if (fuel.isEnabled) enabledCount++;
                    }
                    Speech.Say($"Fuel types: {enabledCount} of {_fuelTypes.Count} enabled");
                    break;
            }
        }

        // ========================================
        // UPGRADES SECTION
        // ========================================

        private void AnnounceUpgradeItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count)
            {
                Speech.Say("Invalid upgrade");
                return;
            }

            // Refresh upgrade info to get current state
            _upgradeInfo = BuildingReflection.GetHearthUpgradeInfo(_building);
            if (itemIndex >= _upgradeInfo.Count)
            {
                Speech.Say("Invalid upgrade");
                return;
            }

            var info = _upgradeInfo[itemIndex];

            // Build status string
            string status = info.isAchieved ? "Achieved" : "Available";

            // Build requirements string
            var reqParts = new List<string>();

            // Housed population
            if (info.minPopulation > 0)
            {
                reqParts.Add($"Housed population {info.currentPopulation} of {info.minPopulation}");
            }

            // Institutions
            if (info.minInstitutions > 0)
            {
                reqParts.Add($"Institutions {info.currentInstitutions} of {info.minInstitutions}");
            }

            // Decorations (tier name already includes "decorations" suffix)
            foreach (var decorReq in info.decorationRequirements)
            {
                reqParts.Add($"{decorReq.tierName} {decorReq.current} of {decorReq.required}");
            }

            string requirements = reqParts.Count > 0 ? string.Join(", ", reqParts) : "None";

            // Build announcement
            string announcement = $"{info.displayName}: {status}. Requirements: {requirements}";

            // Add effect
            if (!string.IsNullOrEmpty(info.effectDescription))
            {
                announcement += $". Effect: {info.effectDescription}";
            }

            Speech.Say(announcement);
        }

        private string GetUpgradeItemName(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _upgradeInfo.Count)
                return null;

            return _upgradeInfo[itemIndex].displayName;
        }

        // ========================================
        // BLIGHT SECTION
        // ========================================

        private void AnnounceBlightItem(int itemIndex)
        {
            int percentage = Mathf.RoundToInt(_corruptionRate * 100f);
            if (percentage <= 0)
                Speech.Say("Corruption: None");
            else
                Speech.Say($"Corruption: {percentage} percent");
        }

        // ========================================
        // SACRIFICE SECTION
        // ========================================

        private void RefreshSacrificeInfo()
        {
            _sacrificeInfo.Clear();
            foreach (var recipe in _sacrificeRecipes)
            {
                var info = BuildingReflection.GetSacrificeRecipeInfo(_building, recipe);
                _sacrificeInfo.Add(info);
            }
        }

        private void AnnounceSacrificeItem(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count)
            {
                Speech.Say("Invalid sacrifice recipe");
                return;
            }

            // Refresh the info for this recipe to get current state
            if (recipeIndex < _sacrificeRecipes.Count)
            {
                _sacrificeInfo[recipeIndex] = BuildingReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
            }

            var info = _sacrificeInfo[recipeIndex];

            // Use good name as primary identifier
            string name = info.goodName;
            if (string.IsNullOrEmpty(name) || name == "Unknown")
            {
                name = info.recipeName;
            }

            // Get effect description
            string effect = info.effectDescription;
            if (string.IsNullOrEmpty(effect))
            {
                effect = info.effectName;
            }
            if (!string.IsNullOrEmpty(effect))
            {
                effect = effect + " per level";
            }

            if (info.level > 0)
            {
                // Active: "{Good}: Level X, {total consumption} per minute, {effect} per level"
                float totalConsumption = info.consumptionPerMin * info.level;
                int consumptionRounded = Mathf.RoundToInt(totalConsumption);
                Speech.Say($"{name}: Level {info.level}, {consumptionRounded} per minute, {effect}");
            }
            else
            {
                // Off: "{Good}: Off, {effect} per level"
                Speech.Say($"{name}: Off, {effect}");
            }
        }

        private void AdjustSacrificeLevel(int recipeIndex, int delta)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeRecipes.Count)
                return;

            // Refresh info to get current state
            _sacrificeInfo[recipeIndex] = BuildingReflection.GetSacrificeRecipeInfo(_building, _sacrificeRecipes[recipeIndex]);
            var info = _sacrificeInfo[recipeIndex];
            var recipeState = _sacrificeRecipes[recipeIndex];

            int currentLevel = info.level;
            int newLevel = currentLevel + delta;

            // Clamp to valid range (0 to maxLevel)
            if (newLevel < 0) newLevel = 0;
            if (newLevel > info.maxLevel) newLevel = info.maxLevel;

            // No change needed
            if (newLevel == currentLevel)
            {
                if (delta > 0 && currentLevel == info.maxLevel)
                {
                    Speech.Say("Maximum level");
                }
                else if (delta < 0 && currentLevel == 0)
                {
                    Speech.Say("Already off");
                }
                return;
            }

            // Check if can afford when increasing from 0
            if (currentLevel == 0 && newLevel > 0 && !info.canAfford)
            {
                SoundManager.PlayFailed();
                Speech.Say($"Not enough {info.goodName}");
                return;
            }

            // Apply the change
            if (BuildingReflection.SetHearthSacrificeLevel(_building, recipeState, newLevel))
            {
                if (newLevel == 0)
                {
                    SoundManager.PlayButtonClick();
                    Speech.Say("Off");
                }
                else if (currentLevel == 0)
                {
                    // Enabling from off
                    SoundManager.PlayBuildingFireButtonStart();
                    Speech.Say($"Level {newLevel}");
                }
                else
                {
                    SoundManager.PlayButtonClick();
                    Speech.Say($"Level {newLevel}");
                }
                RefreshSacrificeInfo();
            }
        }

        private string GetSacrificeItemName(int recipeIndex)
        {
            if (recipeIndex < 0 || recipeIndex >= _sacrificeInfo.Count)
                return null;

            var info = _sacrificeInfo[recipeIndex];
            // Use good name for search
            if (!string.IsNullOrEmpty(info.goodName) && info.goodName != "Unknown")
            {
                return info.goodName;
            }
            return info.recipeName;
        }

        // ========================================
        // SERVICES SECTION (The Commons)
        // ========================================

        private void AnnounceServiceItem(int itemIndex)
        {
            if (!_servicesSettlementUnlocked)
            {
                // Unlock option
                var price = BuildingReflection.GetHearthServicesUnlockPrice(_building);
                if (price != null)
                {
                    bool canAfford = BuildingReflection.CanAffordHearthServicesUnlock(_building);
                    string affordText = canAfford ? "" : ", not enough resources";
                    Speech.Say($"Locked, costs {price.Value.amount} {price.Value.displayName}{affordText}");
                }
                else
                {
                    Speech.Say("Locked");
                }
                return;
            }

            // Service recipe
            if (itemIndex < 0 || itemIndex >= _serviceRecipes.Count)
            {
                Speech.Say("Invalid service");
                return;
            }

            var service = _serviceRecipes[itemIndex];
            // Format: "Need name: requires X Good, Y stars" or "Need name: free, Y stars"
            if (service.IsGoodConsumed && service.GoodAmount > 0)
            {
                Speech.Say($"{service.NeedName}: requires {service.GoodAmount} {service.GoodDisplayName}, {service.Grade} stars");
            }
            else
            {
                Speech.Say($"{service.NeedName}: free, {service.Grade} stars");
            }
        }

        private string GetServiceItemName(int itemIndex)
        {
            if (!_servicesSettlementUnlocked)
                return itemIndex == 0 ? "Unlock" : null;

            if (itemIndex >= 0 && itemIndex < _serviceRecipes.Count)
                return _serviceRecipes[itemIndex].NeedName;

            return null;
        }

        // ========================================
        // FUEL SUB-ITEMS (inside Fire section)
        // ========================================

        private void AnnounceFuelSubItem(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
            {
                Speech.Say("Invalid fuel type");
                return;
            }

            // Refresh the fuel state
            _fuelTypes = BuildingReflection.GetAllFuelTypes();

            var fuel = _fuelTypes[subItemIndex];
            string status = fuel.isEnabled ? "Enabled" : "Disabled";
            Speech.Say($"{fuel.displayName}: {status}");
        }

        private bool ToggleFuel(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
                return false;

            var fuel = _fuelTypes[subItemIndex];
            bool newState = !fuel.isEnabled;

            if (BuildingReflection.SetFuelEnabled(fuel.name, newState))
            {
                SoundManager.PlayButtonClick();
                Speech.Say(newState ? "Enabled" : "Disabled");
                _fuelTypes = BuildingReflection.GetAllFuelTypes();
                return true;
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Cannot change fuel setting");
                return false;
            }
        }

        private string GetFuelSubItemName(int subItemIndex)
        {
            if (subItemIndex < 0 || subItemIndex >= _fuelTypes.Count)
                return null;

            return _fuelTypes[subItemIndex].displayName;
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
                case SectionType.Fire:
                    switch (itemIndex)
                    {
                        case 0: return "Fuel";
                        case 1: return "Time";
                        case 2: return "Fuel types";
                        default: return null;
                    }
                case SectionType.Sacrifice:
                    return GetSacrificeItemName(itemIndex);
                case SectionType.Services:
                    return GetServiceItemName(itemIndex);
                case SectionType.Upgrades:
                    return GetUpgradeItemName(itemIndex);
                case SectionType.Blight:
                    return "Corruption";
                case SectionType.Workers:
                    return _workersSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            // Fire section: Fuel types sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Fire && itemIndex == 2)
            {
                return GetFuelSubItemName(subItemIndex);
            }

            // Workers have sub-items
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
            {
                return _workersSection.GetSubItemName(itemIndex, subItemIndex);
            }

            return null;
        }
    }
}
