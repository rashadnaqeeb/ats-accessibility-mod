# AnnouncementsSettingsPanel.cs
Settings panel for toggling event announcements.
Accessed from the F1 Information Panels menu.
Not an IKeyHandler - called by InfoPanelMenu via ProcessKeyEvent(KeyCode).

## class AnnouncementsSettingsPanel: MenuBase (line 12)

### Fields
- private List<SettingItem> _items (line 18)

### Properties
- protected override string OverlayName { get; } (line 34)
- protected override string EmptyMessage { get; } (line 35)

### Methods
- public bool ProcessKeyEvent(KeyCode keyCode) (line 27)
  Bridge method for InfoPanelMenu; wraps ProcessKey with default modifiers.
- protected override int GetItemCount() (line 37)
- protected override string GetLabel(int index) (line 39)
  Returns "{Label}, On" or "{Label}, Off" based on config value.
- protected override void RefreshData() (line 45)
  Delegates to BuildItemList().
- protected override EnterAction OnEnter(int index) (line 47)
- protected override void OnAction(int index) (line 53)
  Delegates to ToggleCurrentSetting().
- protected override void OnSpace(int index) (line 55)
  Delegates to ToggleCurrentSetting().
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 57)
  LeftArrow returns false to signal parent (InfoPanelMenu) to close this child panel.
- protected override EscapeAction OnEscape() (line 63)
- protected override void OnClosed() (line 65)
- protected override string GetSearchName(int index) (line 69)
- private void ToggleCurrentSetting() (line 77)
  Flips the ConfigEntry<bool> value for the current item and re-announces it.
- private void BuildItemList() (line 85)
  Populates _items with all announcement config entries from Plugin, grouped by category.
