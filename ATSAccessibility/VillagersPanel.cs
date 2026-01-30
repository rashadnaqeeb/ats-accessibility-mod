using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for navigating villager information.
    ///
    /// Navigation model:
    /// - Level 1 (Categories): Shared Needs (if any), then each race - Up/Down to navigate, Enter/Right to enter details
    /// - Level 2 (Details): Resolve, Needs, Favoring - Up/Down to navigate, Left to return
    /// - Level 3 (Sub-details): Resolve breakdown - Right to expand
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
            Favoring
        }

        private class DetailItem
        {
            public DetailType Type { get; set; }
            public string Label { get; set; }
            public List<string> SubDetails { get; set; } = new List<string>();
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

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Cached reflection metadata
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
            _search.Clear();

            AnnounceCategory();
            Debug.Log("[ATSAccessibility] Villagers panel opened");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _categories.Clear();
            _search.Clear();
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

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateUp();
                    return true;

                case KeyCode.DownArrow:
                    NavigateDown();
                    return true;

                case KeyCode.Home:
                    NavigateToFirst();
                    return true;

                case KeyCode.End:
                    NavigateToLast();
                    return true;

                case KeyCode.RightArrow:
                    DrillIn();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    NavigateRight();
                    return true;

                case KeyCode.LeftArrow:
                    return NavigateLeft();

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to parent to handle panel closing
                    return false;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                    }
                    return true;  // Consume all keys while panel is open
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

        private void DrillIn()
        {
            if (_focusOnSubDetails)
            {
                return;
            }

            if (_focusOnDetails)
            {
                var category = _categories[_currentCategoryIndex];
                if (_currentDetailIndex < category.Details.Count)
                {
                    var detail = category.Details[_currentDetailIndex];

                    // Only drill into sub-details, never trigger actions
                    if (detail.SubDetails.Count > 0)
                    {
                        _focusOnSubDetails = true;
                        _currentSubDetailIndex = 0;
                        AnnounceSubDetail();
                    }
                }
                return;
            }

            // Enter details from category level
            var cat = _categories[_currentCategoryIndex];
            if (cat.Details.Count > 0)
            {
                _focusOnDetails = true;
                _currentDetailIndex = 0;
                AnnounceDetail();
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
                _search.Clear();
                AnnounceDetail();
                return true;
            }

            if (_focusOnDetails)
            {
                _focusOnDetails = false;
                _search.Clear();
                AnnounceCategory();
                return true;
            }

            // Pass to parent (InfoPanelMenu) to close this panel
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

        private void NavigateToFirst()
        {
            if (_focusOnSubDetails)
            {
                var category = _categories[_currentCategoryIndex];
                if (_currentDetailIndex < category.Details.Count && category.Details[_currentDetailIndex].SubDetails.Count > 0)
                {
                    _currentSubDetailIndex = 0;
                    AnnounceSubDetail();
                }
            }
            else if (_focusOnDetails)
            {
                _currentDetailIndex = 0;
                _currentSubDetailIndex = 0;
                AnnounceDetail();
            }
            else
            {
                if (_categories.Count == 0) return;
                _currentCategoryIndex = 0;
                _currentDetailIndex = 0;
                _currentSubDetailIndex = 0;
                AnnounceCategory();
            }
        }

        private void NavigateToLast()
        {
            if (_focusOnSubDetails)
            {
                var category = _categories[_currentCategoryIndex];
                if (_currentDetailIndex < category.Details.Count)
                {
                    var detail = category.Details[_currentDetailIndex];
                    if (detail.SubDetails.Count > 0)
                    {
                        _currentSubDetailIndex = detail.SubDetails.Count - 1;
                        AnnounceSubDetail();
                    }
                }
            }
            else if (_focusOnDetails)
            {
                var category = _categories[_currentCategoryIndex];
                if (category.Details.Count > 0)
                {
                    _currentDetailIndex = category.Details.Count - 1;
                    _currentSubDetailIndex = 0;
                    AnnounceDetail();
                }
            }
            else
            {
                if (_categories.Count == 0) return;
                _currentCategoryIndex = _categories.Count - 1;
                _currentDetailIndex = 0;
                _currentSubDetailIndex = 0;
                AnnounceCategory();
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle a search key (A-Z) for type-ahead navigation within current level.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            if (_focusOnSubDetails)
            {
                SearchSubDetails();
            }
            else if (_focusOnDetails)
            {
                SearchDetails();
            }
            else
            {
                SearchCategories();
            }
        }

        /// <summary>
        /// Handle backspace key to remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            // Re-search with shortened buffer
            if (_focusOnSubDetails)
            {
                SearchSubDetails();
            }
            else if (_focusOnDetails)
            {
                SearchDetails();
            }
            else
            {
                SearchCategories();
            }
        }

        private void SearchCategories()
        {
            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < _categories.Count; i++)
            {
                if (_categories[i].DisplayName.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentCategoryIndex = i;
                    _currentDetailIndex = 0;
                    _currentSubDetailIndex = 0;
                    AnnounceCategory();
                    return;
                }
            }
            Speech.Say($"No match for {_search.Buffer}");
        }

        private void SearchDetails()
        {
            if (_currentCategoryIndex >= _categories.Count) return;
            var category = _categories[_currentCategoryIndex];
            string prefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < category.Details.Count; i++)
            {
                if (category.Details[i].Label.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentDetailIndex = i;
                    _currentSubDetailIndex = 0;
                    AnnounceDetail();
                    return;
                }
            }
            Speech.Say($"No match for {_search.Buffer}");
        }

        private void SearchSubDetails()
        {
            if (_currentCategoryIndex >= _categories.Count) return;
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex >= category.Details.Count) return;
            var detail = category.Details[_currentDetailIndex];

            string prefix = _search.Buffer.ToLowerInvariant();
            for (int i = 0; i < detail.SubDetails.Count; i++)
            {
                if (detail.SubDetails[i].ToLowerInvariant().StartsWith(prefix))
                {
                    _currentSubDetailIndex = i;
                    AnnounceSubDetail();
                    return;
                }
            }
            Speech.Say($"No match for {_search.Buffer}");
        }

        // ========================================
        // FAVORING
        // ========================================

        private void PerformFavoringAction()
        {
            if (_currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            string raceName = category.RaceName;
            if (raceName == null) return;  // Shared needs category has no favoring

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
                PlayFavoringSound(raceName);
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

        /// <summary>
        /// Play the race-specific favoring sound (matches game's FavoringButton behavior).
        /// </summary>
        private void PlayFavoringSound(string raceName)
        {
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return;

                var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
                var raceModel = getRaceMethod?.Invoke(settings, new object[] { raceName });
                if (raceModel == null) return;

                var soundField = raceModel.GetType().GetField("favoringStartSound", GameReflection.PublicInstance);
                var soundRef = soundField?.GetValue(raceModel);
                if (soundRef == null) return;

                var getNextMethod = soundRef.GetType().GetMethod("GetNext", GameReflection.PublicInstance);
                var soundModel = getNextMethod?.Invoke(soundRef, null);
                SoundManager.PlaySoundEffect(soundModel);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] PlayFavoringSound failed: {ex.Message}");
            }
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceCategory()
        {
            if (_currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];

            if (category.RaceName == null)
            {
                // Shared needs category - just the name
                Speech.Say(category.DisplayName);
            }
            else
            {
                string favoredStatus = GameReflection.IsFavored(category.RaceName) ? ", favored" : "";
                string message = $"{category.DisplayName}{favoredStatus}. {category.Population} villagers, {category.FreeWorkers} free, {category.Homeless} homeless";
                Speech.Say(message);
            }

            Debug.Log($"[ATSAccessibility] Villagers category: {category.DisplayName}");
        }

        private void AnnounceDetail()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex >= category.Details.Count) return;

            var detail = category.Details[_currentDetailIndex];
            string message = detail.Label;

            // Add type-specific suffix if expandable
            if (detail.Type == DetailType.Resolve && detail.SubDetails.Count > 0)
            {
                message += ". Press right for breakdown";
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

            // Track needs per race for shared needs detection
            var needRaces = new Dictionary<string, List<SharedNeedRaceInfo>>();
            var needOrder = new List<string>();

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

                var needs = GetRaceNeeds(raceName);
                BuildRaceDetails(category, raceName, needs);
                _categories.Add(category);

                // Collect needs for shared detection
                foreach (var need in needs)
                {
                    if (!needRaces.ContainsKey(need.name))
                    {
                        needRaces[need.name] = new List<SharedNeedRaceInfo>();
                        needOrder.Add(need.name);
                    }
                    needRaces[need.name].Add(new SharedNeedRaceInfo
                    {
                        RaceName = raceName,
                        DisplayName = category.DisplayName,
                        NeedModel = need.model,
                        Population = category.Population
                    });
                }
            }

            // Build shared needs category (needs appearing in 2+ races)
            BuildSharedNeedsCategory(needRaces, needOrder);

            Debug.Log($"[ATSAccessibility] Villagers panel refreshed: {_categories.Count} categories");
        }

        private void BuildSharedNeedsCategory(Dictionary<string, List<SharedNeedRaceInfo>> needRaces, List<string> needOrder)
        {
            var sharedCategory = new RaceCategory
            {
                RaceName = null,
                DisplayName = "Shared Needs",
                Population = 0,
                FreeWorkers = 0,
                Homeless = 0
            };

            bool firstNeed = true;
            foreach (var needName in needOrder)
            {
                var races = needRaces[needName];
                if (races.Count < 2) continue;

                int totalSatisfied = 0;
                int totalPopulation = 0;
                var raceNames = new List<string>();

                foreach (var info in races)
                {
                    totalSatisfied += GetNeedSatisfiedCount(info.RaceName, info.NeedModel);
                    totalPopulation += info.Population;
                    raceNames.Add(info.DisplayName.ToLowerInvariant());
                }

                string prefix = firstNeed ? "Needs: " : "";
                firstNeed = false;
                string racesStr = string.Join(",", raceNames);

                sharedCategory.Details.Add(new DetailItem
                {
                    Type = DetailType.Need,
                    Label = $"{prefix}{needName}, {racesStr}, {totalSatisfied} of {totalPopulation} satisfied"
                });
            }

            if (sharedCategory.Details.Count > 0)
            {
                _categories.Insert(0, sharedCategory);
            }
        }

        private void BuildRaceDetails(RaceCategory category, string raceName, List<NeedInfo> needs)
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

            // 3. Favoring option
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

        private class SharedNeedRaceInfo
        {
            public string RaceName;
            public string DisplayName;
            public object NeedModel;
            public int Population;
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

    }
}
