using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the WildcardPopup (mid-game blueprint selection).
    /// Provides two-level category/building navigation with type-ahead search,
    /// Space to toggle selection, and Enter to confirm picks.
    /// </summary>
    public class WildcardOverlay : IKeyHandler
    {
        // Navigation levels
        private const int LEVEL_CATEGORIES = 0;
        private const int LEVEL_BUILDINGS = 1;

        /// <summary>
        /// Represents a building category with its buildings.
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public int Order { get; set; }
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

        // State
        private bool _isOpen;
        private object _popup;

        // Navigation
        private int _navigationLevel;
        private int _categoryIndex;
        private int _buildingIndex;

        // Data
        private List<Category> _categories = new List<Category>();
        private List<(int catIdx, int bldIdx, string name)> _allBuildings =
            new List<(int, int, string)>();
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Picks
        private int _picksRequired;
        private HashSet<object> _selectedModels = new HashSet<object>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.RightArrow:
                    if (_navigationLevel == LEVEL_CATEGORIES)
                        EnterBuildings();
                    return true;

                case KeyCode.LeftArrow:
                    if (_navigationLevel == LEVEL_BUILDINGS)
                        ReturnToCategories();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_navigationLevel == LEVEL_CATEGORIES)
                        EnterBuildings();
                    else
                        ConfirmPicks();
                    return true;

                case KeyCode.Space:
                    if (_navigationLevel == LEVEL_BUILDINGS)
                        ToggleCurrentBuilding();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        ClearSearch();
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    if (_navigationLevel == LEVEL_BUILDINGS)
                    {
                        ReturnToCategories();
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup (OnPopupHidden will close our overlay)
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when a WildcardPopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _navigationLevel = LEVEL_CATEGORIES;
            _categoryIndex = 0;
            _buildingIndex = 0;
            _search.Clear();
            _selectedModels.Clear();

            // Read picks required
            _picksRequired = WildcardReflection.GetPicksRequired();

            // Read any existing picks from popup
            var existingPicks = WildcardReflection.GetCurrentPicks(popup);
            foreach (var pick in existingPicks)
                _selectedModels.Add(pick);

            RefreshData();

            if (_categories.Count == 0)
            {
                Speech.Say($"Wildcard pick, select {_picksRequired}. No buildings available");
            }
            else
            {
                var category = _categories[_categoryIndex];
                Speech.Say($"Wildcard pick, select {_picksRequired}. {category.Name}");
            }

            Debug.Log($"[ATSAccessibility] WildcardOverlay opened, {_picksRequired} picks required, {_categories.Count} categories");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _search.Clear();
            _selectedModels.Clear();
            ClearData();

            Debug.Log("[ATSAccessibility] WildcardOverlay closed");
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _categories.Clear();
            _allBuildings.Clear();

            var buildings = WildcardReflection.GetAvailableBuildings();
            if (buildings == null || buildings.Count == 0) return;

            // Group by category, filtering to HUD categories
            var categoryDict = new Dictionary<string, Category>();
            foreach (var building in buildings)
            {
                if (!categoryDict.TryGetValue(building.CategoryName, out var category))
                {
                    category = new Category
                    {
                        Name = building.CategoryName,
                        Order = building.CategoryOrder
                    };
                    categoryDict[building.CategoryName] = category;
                }

                category.Buildings.Add(new BuildingItem
                {
                    Name = building.DisplayName,
                    Order = building.BuildingOrder,
                    Model = building.Model
                });
            }

            // Sort categories by order then name
            _categories = categoryDict.Values
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

            // Build flat list for type-ahead
            for (int ci = 0; ci < _categories.Count; ci++)
            {
                var cat = _categories[ci];
                for (int bi = 0; bi < cat.Buildings.Count; bi++)
                {
                    _allBuildings.Add((ci, bi, cat.Buildings[bi].Name));
                }
            }

            Debug.Log($"[ATSAccessibility] WildcardOverlay data refreshed: {_categories.Count} categories, {_allBuildings.Count} buildings");
        }

        private void ClearData()
        {
            _categories.Clear();
            _allBuildings.Clear();
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_navigationLevel == LEVEL_CATEGORIES)
                NavigateCategories(direction);
            else
                NavigateBuildings(direction);
        }

        private void NavigateCategories(int direction)
        {
            if (_categories.Count == 0) return;

            _categoryIndex = NavigationUtils.WrapIndex(_categoryIndex, direction, _categories.Count);
            AnnounceCategory();
        }

        private void NavigateBuildings(int direction)
        {
            if (_categories.Count == 0) return;
            var category = _categories[_categoryIndex];
            if (category.Buildings.Count == 0) return;

            _buildingIndex = NavigationUtils.WrapIndex(_buildingIndex, direction, category.Buildings.Count);
            AnnounceBuilding();
        }

        private void EnterBuildings()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_categoryIndex];
            if (category.Buildings.Count == 0)
            {
                Speech.Say("No buildings in this category");
                return;
            }

            _navigationLevel = LEVEL_BUILDINGS;
            _buildingIndex = 0;
            AnnounceBuilding();
        }

        private void ReturnToCategories()
        {
            _navigationLevel = LEVEL_CATEGORIES;
            AnnounceCategory();
        }

        // ========================================
        // SELECTION
        // ========================================

        private void ToggleCurrentBuilding()
        {
            if (_categories.Count == 0) return;
            var category = _categories[_categoryIndex];
            if (_buildingIndex < 0 || _buildingIndex >= category.Buildings.Count) return;

            var building = category.Buildings[_buildingIndex];

            bool toggled = WildcardReflection.ToggleSlot(_popup, building.Model);
            if (!toggled)
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            // Update local selection tracking
            if (_selectedModels.Contains(building.Model))
                _selectedModels.Remove(building.Model);
            else
                _selectedModels.Add(building.Model);

            // Read authoritative count from popup
            int currentCount = WildcardReflection.GetCurrentPickCount(_popup);

            if (_selectedModels.Contains(building.Model))
                Speech.Say($"Selected. {currentCount} of {_picksRequired}");
            else
                Speech.Say($"Deselected. {currentCount} of {_picksRequired}");
        }

        // ========================================
        // CONFIRM
        // ========================================

        private void ConfirmPicks()
        {
            int currentCount = WildcardReflection.GetCurrentPickCount(_popup);

            if (currentCount != _picksRequired)
            {
                Speech.Say($"Select {_picksRequired} blueprints, {currentCount} selected");
                SoundManager.PlayFailed();
                return;
            }

            bool confirmed = WildcardReflection.Confirm(_popup);
            if (confirmed)
            {
                Speech.Say("Blueprints unlocked");
                // Popup hides itself → OnPopupHidden → Close()
            }
            else
            {
                Speech.Say("Could not confirm");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(char c)
        {
            if (_allBuildings.Count == 0) return;

            _search.AddChar(c);

            int matchIndex = _search.FindMatch(_allBuildings, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allBuildings[matchIndex];
                _categoryIndex = match.catIdx;
                _buildingIndex = match.bldIdx;
                _navigationLevel = LEVEL_BUILDINGS;  // Auto-enter buildings
                AnnounceBuilding();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = _search.FindMatch(_allBuildings, entry => entry.name);
            if (matchIndex >= 0)
            {
                var match = _allBuildings[matchIndex];
                _categoryIndex = match.catIdx;
                _buildingIndex = match.bldIdx;
                _navigationLevel = LEVEL_BUILDINGS;
                AnnounceBuilding();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void ClearSearch()
        {
            _search.Clear();
            Speech.Say("Search cleared");
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceCategory()
        {
            if (_categoryIndex < 0 || _categoryIndex >= _categories.Count) return;
            var category = _categories[_categoryIndex];
            Speech.Say(category.Name);
        }

        private void AnnounceBuilding()
        {
            if (_categories.Count == 0) return;
            var category = _categories[_categoryIndex];
            if (_buildingIndex < 0 || _buildingIndex >= category.Buildings.Count) return;

            var building = category.Buildings[_buildingIndex];
            bool isSelected = _selectedModels.Contains(building.Model);

            if (isSelected)
                Speech.Say($"{building.Name}, selected");
            else
                Speech.Say(building.Name);
        }
    }
}
