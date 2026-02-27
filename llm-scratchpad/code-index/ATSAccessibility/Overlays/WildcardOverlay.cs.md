# WildcardOverlay.cs
Accessible overlay for the WildcardPopup (mid-game blueprint selection).
Provides two-level category/building navigation with type-ahead search,
Space to toggle selection, and Enter to confirm picks.

Level 0 = categories, Level 1 = buildings within the selected category.

## class WildcardOverlay: MenuBase (line 16)

### Fields
- private class Category (line 20)
  Represents a building category with its buildings.
  - public string Name { get; set; } (line 21)
  - public int Order { get; set; } (line 22)
  - public List<BuildingItem> Buildings { get; set; } (line 23)
- private class BuildingItem (line 29)
  Represents an individual building within a category.
  - public string Name { get; set; } (line 30)
  - public int Order { get; set; } (line 31)
  - public object Model { get; set; } (line 32)
- private object _popup (line 36)
- private List<Category> _categories (line 37)
- private List<(int catIdx, int bldIdx, string name)> _allBuildings (line 38)
  Flat list of all buildings for cross-category type-ahead search
- private int _picksRequired (line 43)
- private HashSet<object> _selectedModels (line 44)

### Properties
- protected override string OverlayName { get; } (line 49)
- protected override string EmptyMessage { get; } (line 50)
- protected override int SearchItemCount { get; } (line 190)
  Searches _allBuildings (flat cross-category list)
- protected override int SearchCurrentIndex { get; } (line 192)
  Computes flat index from _indices[0] (category) + CurrentIndex (building within category)

### Methods
- protected override int GetItemCount() (line 52)
- protected override string GetLabel(int index) (line 63)
  Level 1: appends ", selected" if model is in _selectedModels
- protected override void RefreshData() (line 82)
  Groups buildings by category, sorts by order then name, builds _allBuildings flat list
- protected override EnterAction OnEnter(int index) (line 132)
  Level 0: DrillDown if category has buildings; Level 1: Action (confirm picks)
- protected override void OnAction(int index) (line 150)
  Level 1 only: calls ConfirmPicks
- protected override void OnSpace(int index) (line 155)
  Level 1 only: calls ToggleCurrentBuilding
- protected override void StorePopup(object popup) (line 164)
  Also reads _picksRequired and pre-populates _selectedModels from existing picks
- protected override string GetOpenAnnouncement() (line 173)
- protected override void OnClosed() (line 179)
- protected override string GetSearchName(int index) (line 205)
  Returns building name from flat _allBuildings list
- protected override void SearchMoveTo(int index) (line 211)
  Jumps to the category and building for the flat index; sets Level 1
- private void ToggleCurrentBuilding() (line 224)
  Calls WildcardReflection.ToggleSlot; updates _selectedModels; announces selected/deselected + count
- private void ConfirmPicks() (line 259)
  Validates count == _picksRequired; calls WildcardReflection.Confirm; popup hides itself on success
