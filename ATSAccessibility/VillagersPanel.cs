using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating villager information.
    /// Custom panel with selective 3rd level for resolve breakdown and individual villagers.
    ///
    /// Navigation model:
    /// - Level 1 (Categories): Each race - Up/Down to navigate, Enter/Right to enter details
    /// - Level 2 (Details): Resolve, Needs, Effects, Villagers - Up/Down to navigate, Left to return
    /// - Level 3 (Sub-details): Resolve breakdown or villager info - only for specific items
    /// </summary>
    public class VillagersPanel
    {
        // ========================================
        // DETAIL ITEM TYPES
        // ========================================

        private enum DetailType
        {
            Resolve,
            Need,
            Villager,
            Favoring
        }

        private class DetailItem
        {
            public DetailType Type { get; set; }
            public string Label { get; set; }
            public List<string> SubDetails { get; set; } = new List<string>();
            public object Data { get; set; }  // For villager, holds the villager object
        }

        private class RaceCategory
        {
            public string RaceName { get; set; }
            public string DisplayName { get; set; }
            public int Population { get; set; }
            public int FreeWorkers { get; set; }
            public int Homeless { get; set; }
            public List<DetailItem> Details { get; set; } = new List<DetailItem>();
        }

        // ========================================
        // STATE
        // ========================================

        private bool _isOpen;
        private List<RaceCategory> _categories = new List<RaceCategory>();
        private int _currentCategoryIndex;
        private int _currentDetailIndex;
        private int _currentSubDetailIndex;
        private bool _focusOnDetails;
        private bool _focusOnSubDetails;

        // Cached reflection metadata
        private static PropertyInfo _villRacesProperty;
        private static MethodInfo _villGetDefaultProfessionAmountMethod;
        private static MethodInfo _villGetHomelessAmountMethod;
        private static bool _typesCached;

        // ========================================
        // PUBLIC API
        // ========================================

        public bool IsOpen => _isOpen;

        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            RefreshData();

            if (_categories.Count == 0)
            {
                Speech.Say("No villagers present");
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentDetailIndex = 0;
            _currentSubDetailIndex = 0;
            _focusOnDetails = false;
            _focusOnSubDetails = false;

            AnnounceCategory();
            Debug.Log("[ATSAccessibility] Villagers panel opened");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _categories.Clear();
            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Villagers panel closed");
            Debug.Log("[ATSAccessibility] Villagers panel closed");
        }

        /// <summary>
        /// Process a key event for the panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateUp();
                    return true;

                case KeyCode.DownArrow:
                    NavigateDown();
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    NavigateRight();
                    return true;

                case KeyCode.LeftArrow:
                    return NavigateLeft();

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return true;  // Consume all other keys while panel is open
            }
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void NavigateUp()
        {
            if (_focusOnSubDetails)
            {
                NavigateSubDetail(-1);
            }
            else if (_focusOnDetails)
            {
                NavigateDetail(-1);
            }
            else
            {
                NavigateCategoryInternal(-1);
            }
        }

        private void NavigateDown()
        {
            if (_focusOnSubDetails)
            {
                NavigateSubDetail(1);
            }
            else if (_focusOnDetails)
            {
                NavigateDetail(1);
            }
            else
            {
                NavigateCategoryInternal(1);
            }
        }

        private void NavigateRight()
        {
            if (_focusOnSubDetails)
            {
                // Already at deepest level, re-announce
                AnnounceSubDetail();
                return;
            }

            if (_focusOnDetails)
            {
                var category = _categories[_currentCategoryIndex];
                if (_currentDetailIndex < category.Details.Count)
                {
                    var detail = category.Details[_currentDetailIndex];

                    // Handle Favoring action
                    if (detail.Type == DetailType.Favoring)
                    {
                        PerformFavoringAction();
                        return;
                    }

                    // Try to enter sub-details if available
                    if (detail.SubDetails.Count > 0)
                    {
                        _focusOnSubDetails = true;
                        _currentSubDetailIndex = 0;
                        AnnounceSubDetail();
                        return;
                    }
                }
                // No sub-details, re-announce
                AnnounceDetail();
                return;
            }

            // Enter details
            var cat = _categories[_currentCategoryIndex];
            if (cat.Details.Count > 0)
            {
                _focusOnDetails = true;
                _currentDetailIndex = 0;
                AnnounceDetail();
            }
            else
            {
                Speech.Say("No details for this race");
            }
        }

        private bool NavigateLeft()
        {
            if (_focusOnSubDetails)
            {
                _focusOnSubDetails = false;
                AnnounceDetail();
                return true;
            }

            if (_focusOnDetails)
            {
                _focusOnDetails = false;
                AnnounceCategory();
                return true;
            }

            // At root level, let parent handle (return to InfoPanelMenu)
            return false;
        }

        private void NavigateCategoryInternal(int direction)
        {
            if (_categories.Count == 0) return;
            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, _categories.Count);
            _currentDetailIndex = 0;
            _currentSubDetailIndex = 0;
            AnnounceCategory();
        }

        private void NavigateDetail(int direction)
        {
            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0) return;
            _currentDetailIndex = NavigationUtils.WrapIndex(_currentDetailIndex, direction, category.Details.Count);
            _currentSubDetailIndex = 0;
            AnnounceDetail();
        }

        private void NavigateSubDetail(int direction)
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex >= category.Details.Count) return;
            var detail = category.Details[_currentDetailIndex];
            if (detail.SubDetails.Count == 0) return;
            _currentSubDetailIndex = NavigationUtils.WrapIndex(_currentSubDetailIndex, direction, detail.SubDetails.Count);
            AnnounceSubDetail();
        }

        // ========================================
        // FAVORING
        // ========================================

        private void PerformFavoringAction()
        {
            if (_currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            string raceName = category.RaceName;

            // Check if already favored
            if (GameReflection.IsFavored(raceName))
            {
                // Stop favoring
                if (GameReflection.StopFavoringRace())
                {
                    Speech.Say($"{category.DisplayName} no longer favored");
                    UpdateFavoringLabel();
                }
                else
                {
                    Speech.Say("Failed to stop favoring");
                }
                return;
            }

            // Check cooldown
            if (GameReflection.IsFavoringOnCooldown())
            {
                float cooldown = GameReflection.GetFavorCooldownLeft();
                Speech.Say($"Favoring on cooldown, {Mathf.CeilToInt(cooldown)} seconds remaining");
                return;
            }

            // Check if there are other races to penalize (need at least 2 races with villagers)
            int racesWithVillagers = 0;
            foreach (var cat in _categories)
            {
                if (cat.Population > 0) racesWithVillagers++;
            }
            if (racesWithVillagers < 2)
            {
                Speech.Say("Need at least two races with villagers to use favoring");
                return;
            }

            // Check if this race has any villagers
            if (category.Population == 0)
            {
                Speech.Say("Cannot favor a race with no villagers");
                return;
            }

            // Start favoring
            if (GameReflection.FavorRace(raceName))
            {
                Speech.Say($"{category.DisplayName} now favored. Other races penalized");
                UpdateFavoringLabel();
            }
            else
            {
                Speech.Say("Failed to favor race");
            }
        }

        private void UpdateFavoringLabel()
        {
            // Update the Favoring label for all categories to reflect new state
            foreach (var category in _categories)
            {
                var favoringItem = category.Details.Find(d => d.Type == DetailType.Favoring);
                if (favoringItem != null)
                {
                    favoringItem.Label = GetFavoringLabel(category.RaceName);
                }
            }
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceCategory()
        {
            if (_currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            string favoredStatus = GameReflection.IsFavored(category.RaceName) ? ", favored" : "";
            string message = $"{category.DisplayName}{favoredStatus}. {category.Population} villagers, {category.FreeWorkers} free, {category.Homeless} homeless";

            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Villagers category: {message}");
        }

        private void AnnounceDetail()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex >= category.Details.Count) return;

            var detail = category.Details[_currentDetailIndex];
            string message = detail.Label;

            // Add type-specific suffix if expandable
            if (detail.SubDetails.Count > 0)
            {
                switch (detail.Type)
                {
                    case DetailType.Resolve:
                        message += ". Press right for breakdown";
                        break;
                    case DetailType.Villager:
                        message += ". Press right for details";
                        break;
                }
            }

            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Villagers detail: {message}");
        }

        private void AnnounceSubDetail()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex >= category.Details.Count) return;
            var detail = category.Details[_currentDetailIndex];
            if (_currentSubDetailIndex >= detail.SubDetails.Count) return;

            string message = detail.SubDetails[_currentSubDetailIndex];
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Villagers sub-detail: {message}");
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshData()
        {
            _categories.Clear();
            EnsureTypes();

            var races = StatsReader.GetPresentRaces();
            foreach (var raceName in races)
            {
                var category = new RaceCategory
                {
                    RaceName = raceName,
                    DisplayName = GetRaceDisplayName(raceName),
                    Population = StatsReader.GetRaceCount(raceName),
                    FreeWorkers = GetFreeWorkers(raceName),
                    Homeless = GetHomeless(raceName)
                };

                BuildRaceDetails(category, raceName);
                _categories.Add(category);
            }

            Debug.Log($"[ATSAccessibility] Villagers panel refreshed: {_categories.Count} races");
        }

        private void BuildRaceDetails(RaceCategory category, string raceName)
        {
            // 1. Resolve with breakdown
            var (resolve, threshold, settling) = StatsReader.GetResolveSummary(raceName);
            var resolveBreakdown = StatsReader.GetResolveBreakdown(raceName);

            category.Details.Add(new DetailItem
            {
                Type = DetailType.Resolve,
                Label = $"Resolve: {Mathf.FloorToInt(resolve)} of {threshold}, settling to {settling}",
                SubDetails = resolveBreakdown
            });

            // 2. Needs (each need as separate item, first one gets "Needs:" prefix)
            var needs = GetRaceNeeds(raceName);
            bool firstNeed = true;
            foreach (var need in needs)
            {
                string needName = need.name;
                int satisfied = GetNeedSatisfiedCount(raceName, need.model);
                int total = category.Population;

                string prefix = firstNeed ? "Needs: " : "";
                firstNeed = false;

                category.Details.Add(new DetailItem
                {
                    Type = DetailType.Need,
                    Label = $"{prefix}{needName}, {satisfied} of {total} satisfied"
                });
            }

            // Note: "Other effects" are already included in the resolve breakdown above

            // 3. Individual villagers (first one gets "Villagers:" prefix)
            var villagers = GetVillagersForRace(raceName);
            bool firstVillager = true;
            foreach (var villager in villagers)
            {
                string name = GetVillagerName(villager);
                string profession = GetVillagerProfession(villager);
                var subDetails = BuildVillagerSubDetails(villager);

                string prefix = firstVillager ? "Villagers: " : "";
                firstVillager = false;

                category.Details.Add(new DetailItem
                {
                    Type = DetailType.Villager,
                    Label = $"{prefix}{name}, {profession}",
                    SubDetails = subDetails,
                    Data = villager
                });
            }

            // 4. Favoring option
            category.Details.Add(new DetailItem
            {
                Type = DetailType.Favoring,
                Label = GetFavoringLabel(raceName)
            });
        }

        private string GetFavoringLabel(string raceName)
        {
            if (GameReflection.IsFavored(raceName))
            {
                return "Favoring: Active. Press Enter to stop";
            }
            else if (GameReflection.IsFavoringOnCooldown())
            {
                float cooldown = GameReflection.GetFavorCooldownLeft();
                return $"Favoring: On cooldown, {Mathf.CeilToInt(cooldown)} seconds";
            }
            else
            {
                return "Favoring: Press Enter to favor this race";
            }
        }

        private List<string> BuildVillagerSubDetails(object villager)
        {
            var details = new List<string>();

            try
            {
                // Get villager state
                var stateField = villager.GetType().GetField("state", GameReflection.PublicInstance);
                var state = stateField?.GetValue(villager);
                if (state == null) return details;

                // Name and gender
                var nameField = state.GetType().GetField("name", GameReflection.PublicInstance);
                var isMaleField = state.GetType().GetField("isMale", GameReflection.PublicInstance);
                string name = nameField?.GetValue(state) as string ?? "Unknown";
                bool isMale = (bool)(isMaleField?.GetValue(state) ?? true);
                details.Add($"Name: {name}, {(isMale ? "Male" : "Female")}");

                // Profession
                var professionField = state.GetType().GetField("profession", GameReflection.PublicInstance);
                string profession = professionField?.GetValue(state) as string ?? "Unknown";
                details.Add($"Profession: {profession}");

                // Housing status
                var houseField = state.GetType().GetField("house", GameReflection.PublicInstance);
                int houseId = (int)(houseField?.GetValue(state) ?? 0);
                details.Add(houseId > 0 ? "Housed" : "Homeless");

                // Perks
                var perksField = state.GetType().GetProperty("perks", GameReflection.PublicInstance)
                    ?? state.GetType().GetField("perks", GameReflection.PublicInstance) as MemberInfo;
                if (perksField != null)
                {
                    object perks = perksField is PropertyInfo pi ? pi.GetValue(state) : ((FieldInfo)perksField).GetValue(state);
                    if (perks != null)
                    {
                        var perksList = GetPerkNames(perks);
                        if (perksList.Count > 0)
                        {
                            details.Add($"Perks: {string.Join(", ", perksList)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BuildVillagerSubDetails failed: {ex.Message}");
            }

            return details;
        }

        private List<string> GetPerkNames(object perksState)
        {
            var result = new List<string>();
            try
            {
                // ActorPerksState has a 'perks' list
                var perksListField = perksState.GetType().GetField("perks", GameReflection.PublicInstance);
                var perksList = perksListField?.GetValue(perksState) as System.Collections.IEnumerable;
                if (perksList == null) return result;

                foreach (var perk in perksList)
                {
                    // Each perk has a 'name' field (string key)
                    var perkNameField = perk.GetType().GetField("name", GameReflection.PublicInstance);
                    string perkName = perkNameField?.GetValue(perk) as string;
                    if (!string.IsNullOrEmpty(perkName))
                    {
                        // Try to get display name
                        string displayName = GetPerkDisplayName(perkName) ?? perkName;
                        result.Add(displayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetPerkNames failed: {ex.Message}");
            }
            return result;
        }

        private string GetPerkDisplayName(string perkName)
        {
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return null;

                // Try GetVillagerPerk method
                var getPerkMethod = settings.GetType().GetMethod("GetVillagerPerk", GameReflection.PublicInstance);
                if (getPerkMethod != null)
                {
                    var perkModel = getPerkMethod.Invoke(settings, new object[] { perkName });
                    if (perkModel != null)
                    {
                        var displayNameField = perkModel.GetType().GetField("displayName", GameReflection.PublicInstance);
                        var locaText = displayNameField?.GetValue(perkModel);
                        return GameReflection.GetLocaText(locaText);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetPerkDisplayName failed: {ex.Message}"); }
            return null;
        }

        // ========================================
        // REFLECTION HELPERS
        // ========================================

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var villService = GameReflection.GetVillagersService();
            if (villService != null)
            {
                var type = villService.GetType();
                _villRacesProperty = type.GetProperty("Races", GameReflection.PublicInstance);
                _villGetDefaultProfessionAmountMethod = type.GetMethod("GetDefaultProfessionAmount",
                    GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
                _villGetHomelessAmountMethod = type.GetMethod("GetHomelessAmount",
                    GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
            }

            _typesCached = true;
            Debug.Log("[ATSAccessibility] VillagersPanel types cached");
        }

        private string GetRaceDisplayName(string raceName)
        {
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return raceName;

                var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
                if (getRaceMethod != null)
                {
                    var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
                    if (raceModel != null)
                    {
                        var displayNameField = raceModel.GetType().GetField("displayName", GameReflection.PublicInstance);
                        var locaText = displayNameField?.GetValue(raceModel);
                        return GameReflection.GetLocaText(locaText) ?? raceName;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetRaceDisplayName failed: {ex.Message}"); }
            return raceName;
        }

        private int GetFreeWorkers(string raceName)
        {
            try
            {
                var villService = GameReflection.GetVillagersService();
                if (villService != null && _villGetDefaultProfessionAmountMethod != null)
                {
                    return (int)_villGetDefaultProfessionAmountMethod.Invoke(villService, new object[] { raceName });
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetFreeWorkers failed: {ex.Message}"); }
            return 0;
        }

        private int GetHomeless(string raceName)
        {
            try
            {
                var villService = GameReflection.GetVillagersService();
                if (villService != null && _villGetHomelessAmountMethod != null)
                {
                    return (int)_villGetHomelessAmountMethod.Invoke(villService, new object[] { raceName });
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetHomeless failed: {ex.Message}"); }
            return 0;
        }

        private class NeedInfo
        {
            public string name;
            public object model;
        }

        private List<NeedInfo> GetRaceNeeds(string raceName)
        {
            var result = new List<NeedInfo>();
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
                if (getRaceMethod == null) return result;

                var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
                if (raceModel == null) return result;

                // Get needs array
                var needsField = raceModel.GetType().GetField("needs", GameReflection.PublicInstance);
                var needsArray = needsField?.GetValue(raceModel) as Array;
                if (needsArray == null) return result;

                foreach (var need in needsArray)
                {
                    if (need == null) continue;

                    // Check if visible
                    var isVisibleField = need.GetType().GetField("isVisible", GameReflection.PublicInstance);
                    bool isVisible = (bool)(isVisibleField?.GetValue(need) ?? true);
                    if (!isVisible) continue;

                    // Get display name via effect.displayName
                    var effectField = need.GetType().GetField("effect", GameReflection.PublicInstance);
                    var effect = effectField?.GetValue(need);
                    if (effect == null) continue;

                    var displayNameField = effect.GetType().GetField("displayName", GameReflection.PublicInstance);
                    var locaText = displayNameField?.GetValue(effect);
                    string name = GameReflection.GetLocaText(locaText) ?? "Unknown";

                    result.Add(new NeedInfo { name = name, model = need });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetRaceNeeds failed: {ex.Message}");
            }
            return result;
        }

        private int GetNeedSatisfiedCount(string raceName, object needModel)
        {
            try
            {
                // Get NeedsService via GameServices
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return 0;

                var needsServiceProp = gameServices.GetType().GetProperty("NeedsService", GameReflection.PublicInstance);
                var needsService = needsServiceProp?.GetValue(gameServices);
                if (needsService == null) return 0;

                // Get RaceModel
                var settings = GameReflection.GetSettings();
                var getRaceMethod = settings?.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
                var raceModel = getRaceMethod?.Invoke(settings, new object[] { raceName });
                if (raceModel == null) return 0;

                // Call CountVillagersWithFulfilled(NeedModel, RaceModel)
                var method = needsService.GetType().GetMethod("CountVillagersWithFulfilled",
                    new Type[] { needModel.GetType(), raceModel.GetType() });
                if (method != null)
                {
                    return (int)method.Invoke(needsService, new object[] { needModel, raceModel });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetNeedSatisfiedCount failed: {ex.Message}");
            }
            return 0;
        }

        private List<object> GetVillagersForRace(string raceName)
        {
            var result = new List<object>();
            try
            {
                var villService = GameReflection.GetVillagersService();
                if (villService == null || _villRacesProperty == null) return result;

                var racesDict = _villRacesProperty.GetValue(villService);
                if (racesDict == null) return result;

                var indexer = racesDict.GetType().GetProperty("Item");
                var villagerList = indexer?.GetValue(racesDict, new object[] { raceName }) as System.Collections.IEnumerable;
                if (villagerList == null) return result;

                foreach (var villager in villagerList)
                {
                    result.Add(villager);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetVillagersForRace failed: {ex.Message}");
            }
            return result;
        }

        private string GetVillagerName(object villager)
        {
            try
            {
                var stateField = villager.GetType().GetField("state", GameReflection.PublicInstance);
                var state = stateField?.GetValue(villager);
                if (state != null)
                {
                    var nameField = state.GetType().GetField("name", GameReflection.PublicInstance);
                    return nameField?.GetValue(state) as string ?? "Unknown";
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetVillagerName failed: {ex.Message}"); }
            return "Unknown";
        }

        private string GetVillagerProfession(object villager)
        {
            try
            {
                var professionModelField = villager.GetType().GetField("professionModel", GameReflection.PublicInstance);
                var professionModel = professionModelField?.GetValue(villager);
                if (professionModel != null)
                {
                    var displayNameField = professionModel.GetType().GetField("displayName", GameReflection.PublicInstance);
                    var locaText = displayNameField?.GetValue(professionModel);
                    return GameReflection.GetLocaText(locaText) ?? "Worker";
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetVillagerProfession failed: {ex.Message}"); }
            return "Worker";
        }
    }
}
