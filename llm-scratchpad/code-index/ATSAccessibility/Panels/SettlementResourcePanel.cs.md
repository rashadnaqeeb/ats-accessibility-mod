# SettlementResourcePanel.cs
Virtual speech-only panel for navigating settlement resources by category.
Level 0 = categories, Level 1 = items in category.
Cross-category item navigation flows between categories on Up/Down at boundaries.
Flat cross-category search across all resources.

## class SettlementResourcePanel: MenuBase (line 15)

### Fields
- private List<Category> _categories (line 36)
- private List<(int categoryIndex, int itemIndex, string name)> _allResources (line 39)
  Flat list of all resources for cross-category search; parallel to the nested _categories structure.
- private int _currentCategoryIndex (line 43)
  Compatibility alias mapping to _indices[0].
- private int _currentItemIndex (line 44)
  Compatibility alias mapping to _indices[1].
- private static readonly List<HelpEntry> _resourceHelpEntries (line 199)

### Properties
- protected override string OverlayName { get; } (line 50)
- protected override string EmptyMessage { get; } (line 51)
- protected override int SearchItemCount { get; } (line 304)
  Returns flat _allResources count for cross-category search.

### Methods
- protected override int GetItemCount() (line 53)
  Level 0: category count. Level 1: item count for current category.
- protected override string GetLabel(int index) (line 63)
  Level 0: "{Name}: {N} type[s]". Level 1: "{ResourceName}, {amount}".
- protected override void RefreshData() (line 82)
  Fetches all stored goods (amount > 0), looks up display names and categories from GoodModel definitions, groups into Category objects, sorts categories and items by order then name, builds flat _allResources list.
- protected override EnterAction OnEnter(int index) (line 172)
  Level 0: DrillDown if category has items. Level 1: None (read-only).
- protected override EscapeAction OnEscape() (line 184)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 186)
  Level 0 LeftArrow returns false to pass to InfoPanelMenu. Level 1 Up/Down (when search not active) calls NavigateItemAcrossCategories.
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 202)
- protected override string GetOpenAnnouncement() (line 204)
- protected override void OnClosed() (line 209)
- public bool ProcessKeyEvent(KeyCode keyCode) (line 219)
  Bridge method for InfoPanelMenu; wraps ProcessKey with default modifiers.
- public void AnnounceCurrentItemDescription() (line 230)
  Announces the description/tooltip of the currently focused resource (Level 1 only). Called from InfoPanelMenu on Alt+I.
- private void NavigateItemAcrossCategories(int direction) (line 255)
  Moves to next/previous item, skipping empty categories and wrapping. Announces category name when crossing a boundary.
- private void AnnounceCategoryAndItem() (line 290)
  Announces "{CategoryName}. {ResourceName}, {amount}" when crossing category boundaries.
- protected override string GetSearchName(int index) (line 306)
  Returns name from flat _allResources list (searches across all categories).
- protected override void SearchMoveTo(int index) (line 312)
  Sets category and item indices from flat list position, promotes to Level 1 if at Level 0, announces current item.
