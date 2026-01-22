using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Base class for building navigators with section-based navigation.
    /// Provides consistent keyboard handling: Up/Down navigate, Enter/Right enter sections/items,
    /// Left returns to previous level, Escape closes panel.
    ///
    /// Navigation levels:
    /// - Level 0: Sections (Info, Workers, Recipes, Storage, etc.)
    /// - Level 1: Items within section (individual recipes, workers, goods)
    /// - Level 2: Sub-items (recipe settings, worker details) - optional
    /// - Level 3: Sub-sub-items (ingredient options) - optional
    /// </summary>
    public abstract class BuildingSectionNavigator : IBuildingNavigator
    {
        // ========================================
        // NAVIGATION STATE
        // ========================================

        protected object _building;
        protected int _currentSectionIndex;
        protected int _currentItemIndex;
        protected int _currentSubItemIndex;
        protected int _currentSubSubItemIndex;
        protected int _navigationLevel;  // 0 = sections, 1 = items, 2 = sub-items, 3 = sub-sub-items

        // Type-ahead search
        protected readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Shared upgrades section handler
        protected readonly BuildingUpgradesSection _upgradesSection = new BuildingUpgradesSection();

        // ========================================
        // IBUILDINGNAVIGATOR IMPLEMENTATION
        // ========================================

        /// <summary>
        /// Open the navigator for a building.
        /// </summary>
        public virtual void Open(object building)
        {
            _building = building;
            _currentSectionIndex = 0;
            _currentItemIndex = 0;
            _currentSubItemIndex = 0;
            _currentSubSubItemIndex = 0;
            _navigationLevel = 0;
            _search.Clear();

            RefreshData();
            AnnounceBuildingOpened();
        }

        /// <summary>
        /// Close the navigator.
        /// </summary>
        public virtual void Close()
        {
            _building = null;
            _search.Clear();
            ClearData();
        }

        /// <summary>
        /// Process a key event.
        /// </summary>
        public virtual bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (_building == null) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateUp();
                    return true;

                case KeyCode.DownArrow:
                    NavigateDown();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    EnterLevel();
                    return true;

                case KeyCode.LeftArrow:
                    if (_navigationLevel > 0)
                    {
                        ExitLevel();
                    }
                    // At root level, do nothing but consume the key
                    return true;

                case KeyCode.Space:
                    PerformAction();
                    return true;

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
                    if (_navigationLevel > 0)
                    {
                        // At item/sub-item level: go back one level, block game from closing panel
                        InputBlocker.BlockCancelOnce = true;
                        ExitLevel();
                        return true;
                    }
                    else
                    {
                        // Pass to game to close building panel
                        Debug.Log($"[ATSAccessibility] {NavigatorName}: Letting game close panel via Escape");
                        return false;
                    }

                case KeyCode.KeypadPlus:
                case KeyCode.Equals:  // + key (unshifted)
                    AdjustValue(1, modifiers);
                    return true;

                case KeyCode.KeypadMinus:
                case KeyCode.Minus:
                    AdjustValue(-1, modifiers);
                    return true;

                default:
                    // Handle A-Z keys for type-ahead search (at all navigation levels)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    return true;  // Consume other keys while panel is open
            }
        }

        // ========================================
        // ABSTRACT MEMBERS - DERIVED CLASSES IMPLEMENT
        // ========================================

        /// <summary>
        /// Name of the navigator for debugging.
        /// </summary>
        protected abstract string NavigatorName { get; }

        /// <summary>
        /// Get available section names for this building.
        /// </summary>
        protected abstract string[] GetSections();

        /// <summary>
        /// Get number of items in the specified section.
        /// </summary>
        protected abstract int GetItemCount(int sectionIndex);

        /// <summary>
        /// Get number of sub-items for the specified item (0 if no sub-items).
        /// </summary>
        protected virtual int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            return 0;  // Default: no sub-items
        }

        /// <summary>
        /// Announce the current section.
        /// </summary>
        protected abstract void AnnounceSection(int sectionIndex);

        /// <summary>
        /// Announce the current item within the section.
        /// </summary>
        protected abstract void AnnounceItem(int sectionIndex, int itemIndex);

        /// <summary>
        /// Announce the current sub-item (if applicable).
        /// </summary>
        protected virtual void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            // Default: do nothing
        }

        /// <summary>
        /// Perform action on current item (Enter/Space at item level).
        /// Returns true if action was performed.
        /// </summary>
        protected virtual bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            return false;  // Default: no action
        }

        /// <summary>
        /// Perform action on current sub-item (Enter/Space at sub-item level).
        /// Returns true if action was performed.
        /// </summary>
        protected virtual bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            return false;  // Default: no action
        }

        /// <summary>
        /// Get number of sub-sub-items for the specified sub-item (0 if no sub-sub-items).
        /// Used for Level 3 navigation (e.g., ingredient options).
        /// </summary>
        protected virtual int GetSubSubItemCount(int sectionIndex, int itemIndex, int subItemIndex)
        {
            return 0;  // Default: no sub-sub-items
        }

        /// <summary>
        /// Announce the current sub-sub-item (Level 3).
        /// </summary>
        protected virtual void AnnounceSubSubItem(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            // Default: do nothing
        }

        /// <summary>
        /// Perform action on current sub-sub-item (Enter/Space at Level 3).
        /// Returns true if action was performed.
        /// </summary>
        protected virtual bool PerformSubSubItemAction(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            return false;  // Default: no action
        }

        /// <summary>
        /// Adjust a value for current item (+/- keys).
        /// Shift modifier typically means larger increments (e.g., 10 instead of 1).
        /// </summary>
        protected virtual void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)
        {
            // Default: do nothing
        }

        /// <summary>
        /// Get the searchable name for a section at the given index.
        /// Subclasses can override this to enable type-ahead search at Level 0.
        /// </summary>
        /// <param name="sectionIndex">The section index.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetSectionName(int sectionIndex)
        {
            // Default: use section names from GetSections()
            var sections = GetSections();
            if (sections != null && sectionIndex >= 0 && sectionIndex < sections.Length)
                return sections[sectionIndex];
            return null;
        }

        /// <summary>
        /// Get the searchable name for an item at the given index within a section.
        /// Subclasses can override this to enable type-ahead search at Level 1.
        /// </summary>
        /// <param name="sectionIndex">The section index.</param>
        /// <param name="itemIndex">The item index within the section.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetItemName(int sectionIndex, int itemIndex)
        {
            return null;  // Default: search not supported
        }

        /// <summary>
        /// Get the searchable name for a sub-item at the given indices.
        /// Subclasses can override this to enable type-ahead search at Level 2.
        /// </summary>
        /// <param name="sectionIndex">The section index.</param>
        /// <param name="itemIndex">The item index within the section.</param>
        /// <param name="subItemIndex">The sub-item index within the item.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            return null;  // Default: search not supported
        }

        /// <summary>
        /// Get the searchable name for a sub-sub-item at the given indices.
        /// Subclasses can override this to enable type-ahead search at Level 3.
        /// </summary>
        /// <param name="sectionIndex">The section index.</param>
        /// <param name="itemIndex">The item index within the section.</param>
        /// <param name="subItemIndex">The sub-item index within the item.</param>
        /// <param name="subSubItemIndex">The sub-sub-item index within the sub-item.</param>
        /// <returns>The searchable name, or null if search is not supported.</returns>
        protected virtual string GetSubSubItemName(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)
        {
            return null;  // Default: search not supported
        }

        /// <summary>
        /// Refresh data from building state.
        /// </summary>
        protected abstract void RefreshData();

        /// <summary>
        /// Clear cached data.
        /// </summary>
        protected abstract void ClearData();

        // ========================================
        // NAVIGATION LOGIC
        // ========================================

        private void NavigateUp()
        {
            switch (_navigationLevel)
            {
                case 0:
                    NavigateSections(-1);
                    break;
                case 1:
                    NavigateItems(-1);
                    break;
                case 2:
                    NavigateSubItems(-1);
                    break;
                case 3:
                    NavigateSubSubItems(-1);
                    break;
            }
        }

        private void NavigateDown()
        {
            switch (_navigationLevel)
            {
                case 0:
                    NavigateSections(1);
                    break;
                case 1:
                    NavigateItems(1);
                    break;
                case 2:
                    NavigateSubItems(1);
                    break;
                case 3:
                    NavigateSubSubItems(1);
                    break;
            }
        }

        private void NavigateSections(int direction)
        {
            var sections = GetSections();
            if (sections == null || sections.Length == 0) return;

            _currentSectionIndex = NavigationUtils.WrapIndex(_currentSectionIndex, direction, sections.Length);
            _currentItemIndex = 0;
            _currentSubItemIndex = 0;
            _currentSubSubItemIndex = 0;
            _search.Clear();  // Clear search when changing sections
            AnnounceSection(_currentSectionIndex);
        }

        private void NavigateItems(int direction)
        {
            int itemCount = GetItemCount(_currentSectionIndex);
            if (itemCount == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, itemCount);
            _currentSubItemIndex = 0;
            _currentSubSubItemIndex = 0;
            AnnounceItem(_currentSectionIndex, _currentItemIndex);
        }

        private void NavigateSubItems(int direction)
        {
            int subItemCount = GetSubItemCount(_currentSectionIndex, _currentItemIndex);
            if (subItemCount == 0) return;

            _currentSubItemIndex = NavigationUtils.WrapIndex(_currentSubItemIndex, direction, subItemCount);
            _currentSubSubItemIndex = 0;
            AnnounceSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
        }

        private void NavigateSubSubItems(int direction)
        {
            int subSubItemCount = GetSubSubItemCount(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
            if (subSubItemCount == 0) return;

            _currentSubSubItemIndex = NavigationUtils.WrapIndex(_currentSubSubItemIndex, direction, subSubItemCount);
            AnnounceSubSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, _currentSubSubItemIndex);
        }

        private void EnterLevel()
        {
            switch (_navigationLevel)
            {
                case 0:
                    // Enter items within section
                    int itemCount = GetItemCount(_currentSectionIndex);
                    if (itemCount == 0)
                    {
                        Speech.Say("No items in this section");
                        return;
                    }
                    _navigationLevel = 1;
                    _currentItemIndex = 0;
                    AnnounceItem(_currentSectionIndex, _currentItemIndex);
                    break;

                case 1:
                    // Try to enter sub-items, or perform action
                    int subItemCount = GetSubItemCount(_currentSectionIndex, _currentItemIndex);
                    if (subItemCount > 0)
                    {
                        _navigationLevel = 2;
                        _currentSubItemIndex = 0;
                        AnnounceSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
                    }
                    else
                    {
                        PerformItemAction(_currentSectionIndex, _currentItemIndex);
                    }
                    break;

                case 2:
                    // Try to enter sub-sub-items (Level 3), or perform action
                    int subSubItemCount = GetSubSubItemCount(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
                    if (subSubItemCount > 0)
                    {
                        _navigationLevel = 3;
                        _currentSubSubItemIndex = 0;
                        AnnounceSubSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, _currentSubSubItemIndex);
                    }
                    else
                    {
                        PerformSubItemAction(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
                    }
                    break;

                case 3:
                    // At sub-sub-item level, perform action
                    PerformSubSubItemAction(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, _currentSubSubItemIndex);
                    break;
            }
        }

        private void ExitLevel()
        {
            if (_navigationLevel > 0)
            {
                _navigationLevel--;
                _search.Clear();  // Clear search when changing levels
                switch (_navigationLevel)
                {
                    case 0:
                        AnnounceSection(_currentSectionIndex);
                        break;
                    case 1:
                        AnnounceItem(_currentSectionIndex, _currentItemIndex);
                        break;
                    case 2:
                        AnnounceSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
                        break;
                }
            }
        }

        private void PerformAction()
        {
            if (_navigationLevel == 3)
            {
                PerformSubSubItemAction(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, _currentSubSubItemIndex);
            }
            else if (_navigationLevel == 2)
            {
                PerformSubItemAction(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
            }
            else if (_navigationLevel == 1)
            {
                PerformItemAction(_currentSectionIndex, _currentItemIndex);
            }
        }

        private void AdjustValue(int delta, KeyboardManager.KeyModifiers modifiers)
        {
            if (_navigationLevel >= 1)
            {
                AdjustItemValue(_currentSectionIndex, _currentItemIndex, delta, modifiers);
            }
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceBuildingOpened()
        {
            string buildingName = BuildingReflection.GetBuildingName(_building) ?? "Building";
            string status = GetBuildingStatus();

            string announcement = buildingName;
            if (!string.IsNullOrEmpty(status))
            {
                announcement += ", " + status;
            }

            Speech.Say(announcement);
            Debug.Log($"[ATSAccessibility] {NavigatorName}: Opened panel for {buildingName}");

            // Announce first section
            var sections = GetSections();
            if (sections != null && sections.Length > 0)
            {
                AnnounceSection(0);
            }
        }

        private string GetBuildingStatus()
        {
            if (!BuildingReflection.IsBuildingFinished(_building))
            {
                return "under construction";
            }
            if (BuildingReflection.IsBuildingSleeping(_building))
            {
                return "paused";
            }
            return null;
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle a search key (A-Z) for type-ahead navigation.
        /// Searches within the current navigation level.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);
            string prefix = _search.Buffer.ToLowerInvariant();

            switch (_navigationLevel)
            {
                case 0:
                    SearchSections(prefix);
                    break;
                case 1:
                    SearchItems(prefix);
                    break;
                case 2:
                    SearchSubItems(prefix);
                    break;
                case 3:
                    SearchSubSubItems(prefix);
                    break;
            }
        }

        private void SearchSections(string prefix)
        {
            var sections = GetSections();
            if (sections == null || sections.Length == 0) return;

            for (int i = 0; i < sections.Length; i++)
            {
                string name = GetSectionName(i);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentSectionIndex = i;
                    _currentItemIndex = 0;
                    _currentSubItemIndex = 0;
                    _currentSubSubItemIndex = 0;
                    AnnounceSection(_currentSectionIndex);
                    Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' matched section at index {i}");
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
            Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' found no match at section level");
        }

        private void SearchItems(string prefix)
        {
            int itemCount = GetItemCount(_currentSectionIndex);
            if (itemCount == 0) return;

            for (int i = 0; i < itemCount; i++)
            {
                string name = GetItemName(_currentSectionIndex, i);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentItemIndex = i;
                    _currentSubItemIndex = 0;
                    _currentSubSubItemIndex = 0;
                    AnnounceItem(_currentSectionIndex, _currentItemIndex);
                    Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' matched item at index {i}");
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
            Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' found no match at item level");
        }

        private void SearchSubItems(string prefix)
        {
            int subItemCount = GetSubItemCount(_currentSectionIndex, _currentItemIndex);
            if (subItemCount == 0) return;

            for (int i = 0; i < subItemCount; i++)
            {
                string name = GetSubItemName(_currentSectionIndex, _currentItemIndex, i);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentSubItemIndex = i;
                    _currentSubSubItemIndex = 0;
                    AnnounceSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
                    Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' matched sub-item at index {i}");
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
            Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' found no match at sub-item level");
        }

        private void SearchSubSubItems(string prefix)
        {
            int subSubItemCount = GetSubSubItemCount(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex);
            if (subSubItemCount == 0) return;

            for (int i = 0; i < subSubItemCount; i++)
            {
                string name = GetSubSubItemName(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, i);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(prefix))
                {
                    _currentSubSubItemIndex = i;
                    AnnounceSubSubItem(_currentSectionIndex, _currentItemIndex, _currentSubItemIndex, _currentSubSubItemIndex);
                    Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' matched sub-sub-item at index {i}");
                    return;
                }
            }

            Speech.Say($"No match for {_search.Buffer}");
            Debug.Log($"[ATSAccessibility] {NavigatorName} search '{_search.Buffer}' found no match at sub-sub-item level");
        }

        /// <summary>
        /// Handle backspace key to remove last character from search buffer.
        /// Works at all navigation levels.
        /// </summary>
        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                Debug.Log($"[ATSAccessibility] {NavigatorName} search buffer cleared via backspace");
                return;
            }

            // Re-search with shortened buffer at current level
            string prefix = _search.Buffer.ToLowerInvariant();
            switch (_navigationLevel)
            {
                case 0:
                    SearchSections(prefix);
                    break;
                case 1:
                    SearchItems(prefix);
                    break;
                case 2:
                    SearchSubItems(prefix);
                    break;
                case 3:
                    SearchSubSubItems(prefix);
                    break;
            }
        }

        // ========================================
        // UPGRADES SECTION HELPERS
        // ========================================

        /// <summary>
        /// Try to add an Upgrades section if the building supports upgrades.
        /// Call this in RefreshData() when building the section list.
        /// Returns true if upgrades were added (caller should add to section types).
        /// </summary>
        protected bool TryInitializeUpgradesSection()
        {
            if (_building == null) return false;
            if (!BuildingReflection.HasUpgradesAvailable(_building)) return false;

            _upgradesSection.Initialize(_building);
            return _upgradesSection.HasUpgrades();
        }

        /// <summary>
        /// Clear the upgrades section data.
        /// Call this in ClearData().
        /// </summary>
        protected void ClearUpgradesSection()
        {
            _upgradesSection.Clear();
        }
    }
}
