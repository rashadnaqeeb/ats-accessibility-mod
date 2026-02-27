# StatsPanel.cs
Virtual speech-only panel for navigating game stats.
Two-level system: Level 0 = categories, Level 1 = details.

## class StatsPanel: MenuBase (line 11)

### Fields
- private List<Category> _categories (line 21)
- private int _currentCategoryIndex (line 24)
  Compatibility alias mapping to _indices[0].
- private int _currentItemIndex (line 25)
  Compatibility alias mapping to _indices[1].

### Properties
- protected override string OverlayName { get; } (line 31)
- protected override string EmptyMessage { get; } (line 32)

### Methods
- protected override int GetItemCount() (line 34)
  Level 0: category count. Level 1: detail count for current category.
- protected override string GetLabel(int index) (line 44)
  Level 0: "{Name}, {Value}". Level 1: the detail string directly.
- protected override string GetSearchName(int index) (line 61)
  Level 0: category name only (without value). Level 1: delegates to GetLabel.
- protected override void RefreshData() (line 70)
  Reads Reputation, Queen's Impatience, Hostility, and per-species Resolve via StatsReader. Each becomes a Category with a summary Value and a Details list for breakdown.
- protected override EnterAction OnEnter(int index) (line 117)
  Level 0: DrillDown if category has details, else announces "No additional details" and returns None. Level 1: None (deepest level).
- protected override EscapeAction OnEscape() (line 129)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 131)
  Level 0 LeftArrow returns false to pass to InfoPanelMenu for child panel closing.
- protected override string GetOpenAnnouncement() (line 137)
- protected override void OnClosed() (line 142)
- public bool ProcessKeyEvent(KeyCode keyCode) (line 151)
  Bridge method for InfoPanelMenu; wraps ProcessKey with default modifiers.
