# ReputationRewardOverlay.cs
Accessible overlay for the ReputationRewardsPopup (mid-game blueprint reward selection).
Provides flat list navigation through building choices plus extend and reroll options.

## class ReputationRewardOverlay: MenuBase (line 12)

### Fields
- private enum ItemType { Building, Extend, Reroll } (line 14)
  Navigation item types
- private class NavItem (line 16)
  - public ItemType Type (line 17)
  - public object Model (line 18)
    BuildingModel (for Building type only)
  - public string Label (line 19)
    Announcement text
  - public string SearchName (line 20)
    Name for type-ahead (buildings only)
- private object _popup (line 24)
- private List<NavItem> _items (line 25)
- public static bool SuppressBlueprintAnnouncement { get; private set; } (line 28)
  Flag to suppress EventAnnouncer's "New blueprint available" when we announce description

### Properties
- protected override string OverlayName { get; } (line 39)
- protected override string EmptyMessage { get; } (line 40)

### Methods
- public static void ResetSuppression() (line 31)
  Defensive reset for scene transitions; clears SuppressBlueprintAnnouncement
- protected override int GetItemCount() (line 42)
- protected override string GetLabel(int index) (line 44)
- protected override string GetSearchName(int index) (line 50)
  Returns SearchName only for Building items; Extend/Reroll are not searchable
- protected override void RefreshData() (line 56)
  Populates _items with buildings, then Extend (if available), then Reroll (if meta-unlocked)
- protected override EnterAction OnEnter(int index) (line 105)
- protected override void OnAction(int index) (line 107)
  Dispatches to ActivateBuilding, ActivateExtend, or ActivateReroll based on item type
- protected override EscapeAction OnEscape() (line 125)
- protected override void StorePopup(object popup) (line 127)
- protected override string GetOpenAnnouncement() (line 131)
  Sets SuppressBlueprintAnnouncement=true if popup has description text
- protected override void OnClosed() (line 147)
- private void ActivateBuilding(NavItem item) (line 157)
  Picks the building; if more options remain, refreshes and re-announces; otherwise waits for popup hide
- private void ActivateExtend() (line 178)
  Checks affordability, calls Extend, then moves focus to the newly added building
- private void ActivateReroll() (line 206)
  Checks affordability, calls Reroll, resets to index 0
- private int CountBuildings() (line 230)
  Returns count of items with Type == Building
- private int GetLastBuildingIndex() (line 239)
  Returns the index of the last Building item, or 0 if none
