# InfoPanelMenu.cs
Unified menu for accessing information panels (Stats, Resources, Mysteries, Villagers, Announcements).
Opened with F1 from the settlement map.

## class InfoPanelMenu: MenuBase (line 10)

### Fields
- private static readonly string[] _menuLabels (line 20)
  Static array: { "Resources", "Villagers", "Workers", "Stats", "Modifiers", "Announcements" }
- private readonly StatsPanel _statsPanel (line 22)
- private readonly SettlementResourcePanel _resourcePanel (line 23)
- private readonly MysteriesPanel _mysteriesPanel (line 24)
- private readonly VillagersPanel _villagersPanel (line 25)
- private readonly WorkersPanel _workersPanel (line 26)
- private readonly AnnouncementsSettingsPanel _announcementsPanel (line 27)
- private MenuPanel? _activeChildPanel (line 29)
- private bool _directOpen (line 32)
  When true, suppresses the open announcement so the child panel can announce itself.

### Properties
- public bool IsInChildPanel { get; } (line 37)
  True if any child panel is currently active (_activeChildPanel has a value).
- protected override string OverlayName { get; } (line 52)
- protected override string EmptyMessage { get; } (line 53)

### Methods
- public InfoPanelMenu(StatsPanel statsPanel, SettlementResourcePanel resourcePanel, MysteriesPanel mysteriesPanel, VillagersPanel villagersPanel, WorkersPanel workersPanel, AnnouncementsSettingsPanel announcementsPanel) (line 39)
- protected override int GetItemCount() (line 55)
- protected override string GetLabel(int index) (line 57)
- protected override void RefreshData() (line 62)
- protected override EnterAction OnEnter(int index) (line 64)
- protected override void OnAction(int index) (line 66)
  Delegates to OpenSelectedPanel().
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 70)
  Handles F1/F2/F3 switching, delegates all keys to active child panel when one is open. LeftArrow returns from child to menu; Escape closes entire overlay. Alt+I in resource panel calls AnnounceCurrentItemDescription().
- protected override EscapeAction OnEscape() (line 128)
- protected override void OnOpened() (line 133)
- protected override string GetOpenAnnouncement() (line 137)
  Returns null when _directOpen is true (suppresses announcement to let child panel announce).
- protected override void OnClosed() (line 148)
- public void Toggle() (line 162)
  Opens, or closes with click sound if already open.
- public void OpenStatsPanel() (line 176)
  Opens Stats panel directly, bypassing the menu; toggles closed if already showing Stats.
- public void OpenModifiersPanel() (line 177)
  Opens Modifiers panel directly; toggles closed if already showing Modifiers.
- public void OpenVillagersPanel() (line 178)
  Opens Villagers panel directly; toggles closed if already showing Villagers.
- public void OpenWorkersPanel() (line 179)
  Opens Workers panel directly; toggles closed if already showing Workers.
- private void OpenPanelDirect(MenuPanel panel) (line 185)
  Handles toggle logic: closes everything if already on the same panel, closes current child if switching, or opens fresh if not open.
- private void OpenSelectedPanel() (line 207)
  Switches on current index to open the appropriate child panel and sets _activeChildPanel.
- private void CloseActiveChildPanel() (line 235)
  Checks each case and closes its panel if open, then clears _activeChildPanel.
- private bool ProcessChildPanelKey(KeyCode keyCode) (line 268)
  Forwards key to the active child panel's ProcessKeyEvent; returns false if no panel active.
