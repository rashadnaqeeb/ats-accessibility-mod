using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the ConsumptionPopup (consumption control).
    /// Three-level navigation: categories → items → races.
    /// </summary>
    public class ConsumptionOverlay : IKeyHandler
    {
        // Navigation levels
        private const int LEVEL_CATEGORIES = 0;
        private const int LEVEL_ITEMS = 1;
        private const int LEVEL_RACES = 2;

        // Category data
        private class CategoryData
        {
            public object Category;     // NeedCategoryModel object (null for raw food/race)
            public string Name;
            public bool IsRawFood;
            public bool IsRace;
            public object Race;         // RaceModel object (only when IsRace)
        }

        // State
        private bool _isOpen;
        private bool _isBlocked;
        private int _level;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Level 0: Categories
        private List<CategoryData> _categories = new List<CategoryData>();
        private int _categoryIndex;

        // Level 1: Items (raw food IDs or need objects)
        private List<object> _items = new List<object>();
        private List<string> _itemNames = new List<string>();
        private int _itemIndex;

        // Level 2: Races (for selected need)
        private List<object> _races = new List<object>();
        private List<string> _raceNames = new List<string>();
        private int _raceIndex;

        // Track whether current category is raw food (for level 1 behavior)
        private bool _currentCategoryIsRawFood;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (_level)
            {
                case LEVEL_CATEGORIES:
                    return ProcessCategoryKey(keyCode);
                case LEVEL_ITEMS:
                    return ProcessItemKey(keyCode);
                case LEVEL_RACES:
                    return ProcessRaceKey(keyCode);
                default:
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _level = LEVEL_CATEGORIES;
            _categoryIndex = 0;
            _isBlocked = ConsumptionReflection.IsBlocked();
            _search.Clear();

            RefreshCategories();

            if (_isBlocked)
            {
                string effects = ConsumptionReflection.GetBlockingEffectsList();
                if (!string.IsNullOrEmpty(effects))
                    Speech.Say($"Consumption control blocked by {effects}");
                else
                    Speech.Say("Consumption control blocked");
            }
            else if (_categories.Count > 0)
            {
                Speech.Say($"Consumption. {GetCategoryAnnouncement(0)}");
            }
            else
            {
                Speech.Say("Consumption. No categories available");
            }
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _level = LEVEL_CATEGORIES;
            _categories.Clear();
            _items.Clear();
            _itemNames.Clear();
            _races.Clear();
            _raceNames.Clear();
            _search.Clear();
        }

        // ========================================
        // LEVEL 0: CATEGORIES
        // ========================================

        private bool ProcessCategoryKey(KeyCode keyCode)
        {
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateCategory(1);
                    return true;

                case KeyCode.Home:
                    if (_categories.Count > 0) { _categoryIndex = 0; Speech.Say(GetCategoryAnnouncement(0)); }
                    return true;

                case KeyCode.End:
                    if (_categories.Count > 0) { _categoryIndex = _categories.Count - 1; Speech.Say(GetCategoryAnnouncement(_categoryIndex)); }
                    return true;

                case KeyCode.RightArrow:
                    ExpandCategory();
                    return true;

                case KeyCode.Space:
                    ToggleCategory();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleCategoryBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleCategorySearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private void NavigateCategory(int direction)
        {
            if (_categories.Count == 0) return;
            _categoryIndex = NavigationUtils.WrapIndex(_categoryIndex, direction, _categories.Count);
            Speech.Say(GetCategoryAnnouncement(_categoryIndex));
        }

        private void ExpandCategory()
        {
            if (_categories.Count == 0) return;

            var cat = _categories[_categoryIndex];
            if (cat.IsRace) return;

            _currentCategoryIsRawFood = cat.IsRawFood;
            RefreshItems(cat);

            if (_items.Count == 0)
            {
                Speech.Say("No items");
                return;
            }

            _search.Clear();
            _level = LEVEL_ITEMS;
            _itemIndex = 0;
            Speech.Say(GetItemAnnouncement(0));
        }

        private void ToggleCategory()
        {
            if (_categories.Count == 0) return;

            if (_isBlocked)
            {
                Speech.Say("Blocked");
                SoundManager.PlayFailed();
                return;
            }

            var cat = _categories[_categoryIndex];
            bool setTo;

            if (cat.IsRawFood)
            {
                // Toggle all raw foods: only permit if all prohibited; mixed → prohibit
                setTo = ConsumptionReflection.IsAllRawFoodProhibited();
                ConsumptionReflection.SetAllRawFoodPermission(setTo);
            }
            else if (cat.IsRace)
            {
                // Toggle all needs for this race: if not all prohibited, prohibit; otherwise permit
                string status = ConsumptionReflection.GetRaceNeedsStatus(cat.Race);
                setTo = (status == "all prohibited");
                ConsumptionReflection.SetAllNeedsPermissionForRace(cat.Race, setTo);
            }
            else
            {
                // Toggle all needs in category: if not all prohibited, prohibit; otherwise permit
                string status = ConsumptionReflection.GetCategoryStatus(cat.Category, false);
                setTo = (status == "all prohibited");
                ConsumptionReflection.SetAllNeedsPermissionForCategory(cat.Category, setTo);
            }

            SoundManager.PlayButtonClick();
            Speech.Say(setTo ? "Permitted" : "Prohibited");
        }

        private string GetCategoryAnnouncement(int index)
        {
            if (index < 0 || index >= _categories.Count) return "";
            var cat = _categories[index];

            if (cat.IsRace)
            {
                string status = ConsumptionReflection.GetRaceNeedsStatus(cat.Race);
                return $"{cat.Name}, {status}";
            }

            string catStatus = ConsumptionReflection.GetCategoryStatus(cat.Category, cat.IsRawFood);
            return $"{cat.Name}, {catStatus}";
        }

        // ========================================
        // LEVEL 1: ITEMS
        // ========================================

        private bool ProcessItemKey(KeyCode keyCode)
        {
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateItem(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateItem(1);
                    return true;

                case KeyCode.Home:
                    if (_items.Count > 0) { _itemIndex = 0; Speech.Say(GetItemAnnouncement(0)); }
                    return true;

                case KeyCode.End:
                    if (_items.Count > 0) { _itemIndex = _items.Count - 1; Speech.Say(GetItemAnnouncement(_itemIndex)); }
                    return true;

                case KeyCode.RightArrow:
                    if (!_currentCategoryIsRawFood)
                        ExpandNeedToRaces();
                    return true;

                case KeyCode.LeftArrow:
                    _search.Clear();
                    CollapseToCategories();
                    return true;

                case KeyCode.Space:
                    ToggleItem();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    _search.Clear();
                    CollapseToCategories();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleItemBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleItemSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private void NavigateItem(int direction)
        {
            if (_items.Count == 0) return;
            _itemIndex = NavigationUtils.WrapIndex(_itemIndex, direction, _items.Count);
            Speech.Say(GetItemAnnouncement(_itemIndex));
        }

        private void ExpandNeedToRaces()
        {
            if (_items.Count == 0 || _currentCategoryIsRawFood) return;

            var need = _items[_itemIndex];
            RefreshRaces(need);

            if (_races.Count == 0)
            {
                Speech.Say("No races");
                return;
            }

            _search.Clear();
            _level = LEVEL_RACES;
            _raceIndex = 0;
            Speech.Say(GetRaceAnnouncement(0));
        }

        private void CollapseToCategories()
        {
            _level = LEVEL_CATEGORIES;
            _items.Clear();
            _itemNames.Clear();
            Speech.Say(GetCategoryAnnouncement(_categoryIndex));
        }

        private void ToggleItem()
        {
            if (_items.Count == 0) return;

            if (_isBlocked)
            {
                Speech.Say("Blocked");
                SoundManager.PlayFailed();
                return;
            }

            bool setTo;

            if (_currentCategoryIsRawFood)
            {
                // Toggle individual raw food
                string id = _items[_itemIndex] as string;
                if (id == null) return;

                setTo = !ConsumptionReflection.IsRawFoodPermitted(id);
                ConsumptionReflection.SetRawFoodPermission(id, setTo);
            }
            else
            {
                // Blanket toggle for a need (all races)
                var need = _items[_itemIndex];
                string status = ConsumptionReflection.GetNeedStatus(need);
                setTo = (status == "all prohibited");
                ConsumptionReflection.SetNeedBlanketPermission(need, setTo);
            }

            SoundManager.PlayButtonClick();
            Speech.Say(setTo ? "Permitted" : "Prohibited");
        }

        private string GetItemAnnouncement(int index)
        {
            if (index < 0 || index >= _items.Count) return "";

            string name = (index < _itemNames.Count) ? _itemNames[index] : "Unknown";

            if (_currentCategoryIsRawFood)
            {
                string id = _items[index] as string;
                bool permitted = (id != null) && ConsumptionReflection.IsRawFoodPermitted(id);
                return $"{name}, {(permitted ? "permitted" : "prohibited")}";
            }
            else
            {
                var need = _items[index];
                string status = ConsumptionReflection.GetNeedStatus(need);
                return $"{name}, {status}";
            }
        }

        // ========================================
        // LEVEL 2: RACES
        // ========================================

        private bool ProcessRaceKey(KeyCode keyCode)
        {
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateRace(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateRace(1);
                    return true;

                case KeyCode.Home:
                    if (_races.Count > 0) { _raceIndex = 0; Speech.Say(GetRaceAnnouncement(0)); }
                    return true;

                case KeyCode.End:
                    if (_races.Count > 0) { _raceIndex = _races.Count - 1; Speech.Say(GetRaceAnnouncement(_raceIndex)); }
                    return true;

                case KeyCode.LeftArrow:
                    _search.Clear();
                    CollapseToItems();
                    return true;

                case KeyCode.Space:
                    ToggleRace();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    _search.Clear();
                    CollapseToItems();
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleRaceBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleRaceSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        private void NavigateRace(int direction)
        {
            if (_races.Count == 0) return;
            _raceIndex = NavigationUtils.WrapIndex(_raceIndex, direction, _races.Count);
            Speech.Say(GetRaceAnnouncement(_raceIndex));
        }

        private void CollapseToItems()
        {
            _level = LEVEL_ITEMS;
            _races.Clear();
            _raceNames.Clear();
            Speech.Say(GetItemAnnouncement(_itemIndex));
        }

        private void ToggleRace()
        {
            if (_races.Count == 0 || _items.Count == 0) return;

            if (_isBlocked)
            {
                Speech.Say("Blocked");
                SoundManager.PlayFailed();
                return;
            }

            var race = _races[_raceIndex];
            var need = _items[_itemIndex];
            bool setTo = !ConsumptionReflection.IsNeedPermittedForRace(race, need);
            ConsumptionReflection.SetNeedPermissionForRace(race, need, setTo);

            SoundManager.PlayButtonClick();
            Speech.Say(setTo ? "Permitted" : "Prohibited");
        }

        private string GetRaceAnnouncement(int index)
        {
            if (index < 0 || index >= _races.Count || _items.Count == 0) return "";

            string name = (index < _raceNames.Count) ? _raceNames[index] : "Unknown";
            var race = _races[index];
            var need = _items[_itemIndex];

            bool permitted = ConsumptionReflection.IsNeedPermittedForRace(race, need);
            var (_, max) = ConsumptionReflection.GetResolveImpact(race, need);

            string impact = permitted ? $"+{max} resolve bonus" : $"{max} rationing penalty";
            return $"{name}, {(permitted ? "permitted" : "prohibited")}, {impact}";
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshCategories()
        {
            _categories.Clear();

            // First category is always "Raw Food"
            _categories.Add(new CategoryData
            {
                Category = null,
                Name = "Raw Food",
                IsRawFood = true
            });

            // Add dynamic need categories
            var categories = ConsumptionReflection.GetCategories();
            foreach (var cat in categories)
            {
                _categories.Add(new CategoryData
                {
                    Category = cat,
                    Name = ConsumptionReflection.GetCategoryName(cat),
                    IsRawFood = false
                });
            }

            // Add per-race master toggles at the bottom
            var races = ConsumptionReflection.GetAllRevealedRaces();
            foreach (var race in races)
            {
                _categories.Add(new CategoryData
                {
                    Name = ConsumptionReflection.GetRaceName(race),
                    IsRace = true,
                    Race = race
                });
            }
        }

        private void RefreshItems(CategoryData category)
        {
            _items.Clear();
            _itemNames.Clear();

            if (category.IsRawFood)
            {
                var foods = ConsumptionReflection.GetRawFoods();
                foreach (var id in foods)
                {
                    _items.Add(id);
                    _itemNames.Add(ConsumptionReflection.GetRawFoodName(id));
                }
            }
            else
            {
                var needs = ConsumptionReflection.GetNeedsForCategory(category.Category);
                foreach (var need in needs)
                {
                    _items.Add(need);
                    _itemNames.Add(ConsumptionReflection.GetNeedName(need));
                }
            }
        }

        private void RefreshRaces(object need)
        {
            _races.Clear();
            _raceNames.Clear();

            var races = ConsumptionReflection.GetRacesForNeed(need);
            foreach (var race in races)
            {
                _races.Add(race);
                _raceNames.Add(ConsumptionReflection.GetRaceName(race));
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH - CATEGORIES
        // ========================================

        private void HandleCategorySearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindCategoryMatch();
            if (matchIndex >= 0)
            {
                _categoryIndex = matchIndex;
                Speech.Say(GetCategoryAnnouncement(_categoryIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleCategoryBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindCategoryMatch();
            if (matchIndex >= 0)
            {
                _categoryIndex = matchIndex;
                Speech.Say(GetCategoryAnnouncement(_categoryIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindCategoryMatch()
        {
            if (!_search.HasBuffer || _categories.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _categories.Count; i++)
            {
                if (string.IsNullOrEmpty(_categories[i].Name)) continue;

                if (_categories[i].Name.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }

        // ========================================
        // TYPE-AHEAD SEARCH - ITEMS
        // ========================================

        private void HandleItemSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindItemMatch();
            if (matchIndex >= 0)
            {
                _itemIndex = matchIndex;
                Speech.Say(GetItemAnnouncement(_itemIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleItemBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindItemMatch();
            if (matchIndex >= 0)
            {
                _itemIndex = matchIndex;
                Speech.Say(GetItemAnnouncement(_itemIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindItemMatch()
        {
            if (!_search.HasBuffer || _itemNames.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _itemNames.Count; i++)
            {
                if (string.IsNullOrEmpty(_itemNames[i])) continue;

                if (_itemNames[i].ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }

        // ========================================
        // TYPE-AHEAD SEARCH - RACES
        // ========================================

        private void HandleRaceSearchKey(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindRaceMatch();
            if (matchIndex >= 0)
            {
                _raceIndex = matchIndex;
                Speech.Say(GetRaceAnnouncement(_raceIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleRaceBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindRaceMatch();
            if (matchIndex >= 0)
            {
                _raceIndex = matchIndex;
                Speech.Say(GetRaceAnnouncement(_raceIndex));
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindRaceMatch()
        {
            if (!_search.HasBuffer || _raceNames.Count == 0) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _raceNames.Count; i++)
            {
                if (string.IsNullOrEmpty(_raceNames[i])) continue;

                if (_raceNames[i].ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
