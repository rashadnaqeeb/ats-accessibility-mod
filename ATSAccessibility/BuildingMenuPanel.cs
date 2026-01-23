using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for selecting buildings to place.
    /// Two-panel system: left panel has categories, right panel has buildings in category.
    /// </summary>
    public class BuildingMenuPanel : IKeyHandler
    {
        /// <summary>
        /// Represents a building category (e.g., Housing, Food Production).
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public int Order { get; set; }
            public object Model { get; set; }
            public List<BuildingItem> Buildings { get; set; } = new List<BuildingItem>();
        }

        /// <summary>
        /// Represents an individual building within a category.
        /// </summary>
        private class BuildingItem
        {
            public string Name { get; set; }
            public int Order { get; set; }
            public object Model { get; set; }
        }

        private bool _isOpen = false;
        private List<Category> _categories = new List<Category>();
        private int _currentCategoryIndex = 0;
        private int _currentBuildingIndex = 0;
        private bool _focusOnBuildings = false;  // Left panel (categories) vs right panel (buildings)

        // Type-ahead search for cross-category building search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Flat list of all buildings for cross-category search
        private List<(int categoryIndex, int buildingIndex, string name)> _allBuildings =
            new List<(int, int, string)>();

        // Reference to build mode controller for entering build mode
        private BuildModeController _buildModeController;

        /// <summary>
        /// Whether the building menu is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _isOpen;

        /// <summary>
        /// Set the build mode controller reference.
        /// </summary>
        public void SetBuildModeController(BuildModeController controller)
        {
            _buildModeController = controller;
        }

        /// <summary>
        /// Open the building menu and announce the first category.
        /// Toggles closed if already open.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                SoundManager.PlayButtonClick();
                Close();
                return;
            }

            // Build category list from game data
            RefreshCategories();

            if (_categories.Count == 0)
            {
                Speech.Say("No buildings available");
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentBuildingIndex = 0;
            _focusOnBuildings = false;
            _search.Clear();

            SoundManager.PlayPopupShow();
            // Announce menu opening with first category in one speech to avoid cutoff
            var category = _categories[_currentCategoryIndex];
            int buildingCount = category.Buildings.Count;
            Speech.Say($"Building Menu. {category.Name}: {buildingCount}");
            Debug.Log("[ATSAccessibility] Building menu opened");
        }

        /// <summary>
        /// Close the building menu.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;  // Block the Cancel action that will fire this frame
            _categories.Clear();
            _search.Clear();
            Speech.Say("Building menu closed");
            Debug.Log("[ATSAccessibility] Building menu closed");
        }

        /// <summary>
        /// Process a key event for the building menu (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_focusOnBuildings)
                        NavigateBuilding(-1);
                    else
                        NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_focusOnBuildings)
                        NavigateBuilding(1);
                    else
                        NavigateCategory(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_focusOnBuildings)
                        SelectBuilding();
                    else
                        EnterBuildings();
                    return true;

                case KeyCode.RightArrow:
                    EnterBuildings();
                    return true;

                case KeyCode.LeftArrow:
                    ReturnToCategories();
                    return true;

                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        ClearSearchBuffer();
                        InputBlocker.BlockCancelOnce = true;  // Block the Cancel action
                        return true;
                    }
                    SoundManager.PlayButtonClick();
                    Close();
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    return true;  // Consume all other keys while panel is open
            }
        }

        /// <summary>
        /// Navigate categories (left panel) with Up/Down.
        /// </summary>
        private void NavigateCategory(int direction)
        {
            if (!_isOpen || _categories.Count == 0) return;

            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, _categories.Count);
            _currentBuildingIndex = 0;  // Reset building index when changing category
            AnnounceCurrentCategory();
        }

        /// <summary>
        /// Navigate buildings (right panel) with Up/Down when in buildings mode.
        /// </summary>
        private void NavigateBuilding(int direction)
        {
            if (!_isOpen || !_focusOnBuildings) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Buildings.Count == 0) return;

            _currentBuildingIndex = NavigationUtils.WrapIndex(_currentBuildingIndex, direction, category.Buildings.Count);
            AnnounceCurrentBuilding();
        }

        /// <summary>
        /// Enter buildings mode (Enter or Right arrow).
        /// </summary>
        private void EnterBuildings()
        {
            if (!_isOpen) return;

            var category = _categories[_currentCategoryIndex];

            if (category.Buildings.Count == 0)
            {
                Speech.Say("No buildings in this category");
                return;
            }

            _focusOnBuildings = true;
            _currentBuildingIndex = 0;
            AnnounceCurrentBuilding();
        }

        /// <summary>
        /// Return to categories (Left arrow).
        /// </summary>
        private void ReturnToCategories()
        {
            if (!_isOpen) return;

            if (_focusOnBuildings)
            {
                _focusOnBuildings = false;
                AnnounceCurrentCategory();
            }
        }

        /// <summary>
        /// Select the current building and enter build mode.
        /// </summary>
        private void SelectBuilding()
        {
            if (!_isOpen || !_focusOnBuildings) return;

            var category = _categories[_currentCategoryIndex];
            if (_currentBuildingIndex < 0 || _currentBuildingIndex >= category.Buildings.Count) return;

            var building = category.Buildings[_currentBuildingIndex];

            // Check if building can still be constructed
            if (!GameReflection.CanConstructBuilding(building.Model))
            {
                Speech.Say($"{building.Name} cannot be built, at maximum");
                return;
            }

            // Close menu and enter build mode
            SoundManager.PlayButtonClick();
            _isOpen = false;
            _categories.Clear();

            if (_buildModeController != null)
            {
                _buildModeController.EnterBuildMode(building.Model, building.Name);
            }
            else
            {
                Debug.LogError("[ATSAccessibility] BuildModeController not set");
                Speech.Say("Build mode unavailable");
            }
        }

        /// <summary>
        /// Refresh the category list with current game data.
        /// Groups unlocked buildings by their category, sorted by category order.
        /// </summary>
        private void RefreshCategories()
        {
            _categories.Clear();

            // Get all building categories
            var allCategories = GameReflection.GetBuildingCategories();
            if (allCategories == null)
            {
                Debug.LogWarning("[ATSAccessibility] Could not get BuildingCategories from Settings");
                return;
            }

            // Build category lookup
            var categoryDict = new Dictionary<object, Category>();
            foreach (var catModel in allCategories)
            {
                if (!GameReflection.IsCategoryOnHUD(catModel)) continue;

                var name = GameReflection.GetDisplayName(catModel) ?? "Unknown";
                var order = GameReflection.GetModelOrder(catModel);

                categoryDict[catModel] = new Category
                {
                    Name = name,
                    Order = order,
                    Model = catModel,
                    Buildings = new List<BuildingItem>()
                };
            }

            // Get all building models
            var allBuildings = GameReflection.GetAllBuildingModels();
            if (allBuildings == null)
            {
                Debug.LogWarning("[ATSAccessibility] Could not get BuildingModels from Settings");
                return;
            }

            // Filter and group buildings
            int unlockedCount = 0;
            foreach (var buildingModel in allBuildings)
            {
                // Skip inactive, not in shop, or locked buildings
                if (!GameReflection.IsBuildingActive(buildingModel)) continue;
                if (!GameReflection.IsBuildingInShop(buildingModel)) continue;
                if (!GameReflection.IsBuildingUnlocked(buildingModel)) continue;

                var category = GameReflection.GetBuildingCategory(buildingModel);
                if (category == null || !categoryDict.ContainsKey(category)) continue;

                var name = GameReflection.GetDisplayName(buildingModel) ?? GameReflection.GetModelName(buildingModel) ?? "Unknown";
                var order = GameReflection.GetModelOrder(buildingModel);

                categoryDict[category].Buildings.Add(new BuildingItem
                {
                    Name = name,
                    Order = order,
                    Model = buildingModel
                });
                unlockedCount++;
            }

            // Filter to categories with buildings and sort
            _categories = categoryDict.Values
                .Where(c => c.Buildings.Count > 0)
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Name)
                .ToList();

            // Sort buildings within each category
            foreach (var category in _categories)
            {
                category.Buildings = category.Buildings
                    .OrderBy(b => b.Order)
                    .ThenBy(b => b.Name)
                    .ToList();
            }

            // Build flat list of all buildings for cross-category search
            _allBuildings.Clear();
            for (int ci = 0; ci < _categories.Count; ci++)
            {
                var cat = _categories[ci];
                for (int bi = 0; bi < cat.Buildings.Count; bi++)
                {
                    _allBuildings.Add((ci, bi, cat.Buildings[bi].Name));
                }
            }

            Debug.Log($"[ATSAccessibility] Building menu refreshed: {_categories.Count} categories, {unlockedCount} buildings");
        }

        /// <summary>
        /// Announce the current category (left panel).
        /// </summary>
        private void AnnounceCurrentCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            int buildingCount = category.Buildings.Count;

            Speech.Say($"{category.Name}: {buildingCount}");
            Debug.Log($"[ATSAccessibility] Category {_currentCategoryIndex + 1}/{_categories.Count}: {category.Name}, {buildingCount} buildings");
        }

        /// <summary>
        /// Announce the current building (right panel).
        /// </summary>
        private void AnnounceCurrentBuilding()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentBuildingIndex < 0 || _currentBuildingIndex >= category.Buildings.Count) return;

            var building = category.Buildings[_currentBuildingIndex];

            // Get building size
            var size = GameReflection.GetBuildingSize(building.Model);
            string sizeText = $"{size.x}x{size.y}";

            // Get building costs (includes "not enough" annotations for insufficient goods)
            string costs = GameReflection.GetBuildingCosts(building.Model);
            string costsText = !string.IsNullOrEmpty(costs) ? $" {costs}." : "";

            // Get building description
            string description = GameReflection.GetBuildingDescription(building.Model) ?? "";

            // Check if can be constructed
            bool canConstruct = GameReflection.CanConstructBuilding(building.Model);
            string status = canConstruct ? "" : ", at maximum";

            // Format: "Name, size. 5 Planks, not enough, 3 Bricks. Description"
            string announcement = $"{building.Name}{status}, {sizeText}.{costsText} {description}";
            Speech.Say(announcement);
            Debug.Log($"[ATSAccessibility] Building: {building.Name}{status}, {sizeText}");
        }

        /// <summary>
        /// Handle a search key (A-Z) for type-ahead navigation.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            if (_allBuildings.Count == 0) return;

            _search.AddChar(c);

            // Find first building starting with buffer (case-insensitive)
            int matchIndex = _search.FindMatch(_allBuildings, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allBuildings[matchIndex];
                _currentCategoryIndex = match.categoryIndex;
                _currentBuildingIndex = match.buildingIndex;
                _focusOnBuildings = true;  // Auto-enter buildings panel
                AnnounceCurrentBuilding();
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' matched building at category {_currentCategoryIndex}, building {_currentBuildingIndex}");
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' found no match");
            }
        }

        /// <summary>
        /// Handle backspace key to remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (!_search.RemoveChar())
                return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                Debug.Log("[ATSAccessibility] Search buffer cleared via backspace");
                return;
            }

            // Re-search with shortened buffer
            int matchIndex = _search.FindMatch(_allBuildings, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allBuildings[matchIndex];
                _currentCategoryIndex = match.categoryIndex;
                _currentBuildingIndex = match.buildingIndex;
                _focusOnBuildings = true;
                AnnounceCurrentBuilding();
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' matched building at category {_currentCategoryIndex}, building {_currentBuildingIndex}");
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' found no match");
            }
        }

        /// <summary>
        /// Clear the search buffer and announce.
        /// </summary>
        private void ClearSearchBuffer()
        {
            _search.Clear();
            Speech.Say("Search cleared");
            Debug.Log("[ATSAccessibility] Search buffer cleared");
        }
    }
}
