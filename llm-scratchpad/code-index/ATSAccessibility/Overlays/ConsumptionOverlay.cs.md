# ConsumptionOverlay.cs
Accessible overlay for the ConsumptionPopup (consumption control).
Three-level navigation: categories -> items -> races.
Pattern B: Right drills down, Enter does nothing at any level.
Space toggles at all levels.

## class ConsumptionOverlay: MenuBase (line 13)

### Fields
- private class CategoryData (line 15)
  Fields: object Category, string Name, bool IsRawFood, bool IsRace, object Race
- private bool _isBlocked (line 24)
- private List<CategoryData> _categories (line 27)
- private List<object> _items (line 30)
  Level 1: raw food IDs (string) or need objects.
- private List<string> _itemNames (line 31)
- private List<object> _races (line 34)
- private List<string> _raceNames (line 35)
- private bool _currentCategoryIsRawFood (line 38)

### Properties
- protected override string OverlayName { get; } (line 44)
- protected override string EmptyMessage { get; } (line 45)
- protected override int SearchItemCount { get; } (line 155)

### Methods
- protected override int GetItemCount() (line 47)
- protected override string GetLabel(int index) (line 56)
  Delegates to level-specific announcement helpers.
- protected override void RefreshData() (line 65)
- protected override EnterAction OnEnter(int index) (line 70)
  Always returns None (Pattern B: Enter does nothing).
- protected override void OnSpace(int index) (line 72)
  Dispatches to ToggleCategory, ToggleItem, or ToggleRace based on level.
- protected override bool CanDrillDown(int index) (line 84)
  Pattern B: loads next-level data and validates it is non-empty before allowing drill-down.
- protected override void OnDrillDown(int index) (line 115)
  No-op: data already loaded by CanDrillDown before this is called.
- protected override void OnGoBack() (line 119)
- protected override string GetOpenAnnouncement() (line 129)
  Reports blocking effects by name if consumption is blocked.
- protected override void OnClosed() (line 143)
- protected override string GetSearchName(int index) (line 166)
- private void ToggleCategory() (line 183)
  Toggle logic differs by category type: raw food uses all-or-nothing, race uses race needs, otherwise uses category needs.
- private void ToggleItem() (line 215)
  For raw food items toggles individual permission; for need items uses blanket toggle based on current mixed status.
- private void ToggleRace() (line 245)
  Toggles a single need/race pair permission.
- private string GetCategoryAnnouncement(int index) (line 267)
- private string GetItemAnnouncement(int index) (line 280)
- private string GetRaceAnnouncement(int index) (line 296)
  Includes resolve impact (bonus or rationing penalty magnitude).
- private void RefreshCategories() (line 314)
  Builds: "Raw Food" first, then dynamic need categories, then per-race master toggles.
- private void RefreshItems(CategoryData category) (line 345)
- private void RefreshRaces(object need) (line 364)
