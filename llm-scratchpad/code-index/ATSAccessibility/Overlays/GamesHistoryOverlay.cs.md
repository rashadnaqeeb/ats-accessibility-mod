# GamesHistoryOverlay.cs
Accessible overlay for the Games History popup.
Three-level navigation: Main Menu -> Submenu -> Settlement Details (flat list).

## class GamesHistoryOverlay: MenuBase (line 10)

### Fields
- private enum MainMenuItem { CycleStats, Upgrades, History } (line 11)
- private static readonly string[] MainMenuItems (line 14)
- private List<(string label, string value)> _cycleStats (line 17)
- private List<(string label, string value)> _upgrades (line 18)
- private List<object> _settlements (line 19)
- private List<string> _settlementDetailItems (line 22)

### Properties
- protected override string OverlayName { get; } (line 28)
- protected override string EmptyMessage { get; } (line 30)

### Methods
- protected override int GetItemCount() (line 32)
- protected override string GetLabel(int index) (line 41)
- protected override void RefreshData() (line 58)
- protected override EnterAction OnEnter(int index) (line 64)
  At Level 1: DrillDown only for History items (settlement records); Cycle Stats and Upgrades are read-only.
- protected override void OnDrillDown(int index) (line 82)
  At Level 1 -> Level 2: builds the flat settlement detail list.
- protected override void OnGoBack() (line 90)
  Clears detail items when going back from Level 2.
- protected override string GetOpenAnnouncement() (line 96)
- protected override void OnClosed() (line 100)
- protected override int SearchItemCount { get; } (line 111)
- protected override string GetSearchName(int index) (line 122)
- private int GetCurrentSubmenuCount() (line 143)
- private string GetSubmenuLabel(int index) (line 156)
  Formats label+value for stats/upgrades; formats name+won/lost for settlements.
- private string GetSubmenuItemName(int index) (line 185)
  Returns just the name portion (without value) for type-ahead search.
- private void BuildSettlementDetailItems(object settlement) (line 198)
  Builds a flat list of strings covering summary, races, cornerstones, modifiers, buildings, and seasonal effects for one settlement record.
