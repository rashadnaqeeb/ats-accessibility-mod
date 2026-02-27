# DailyExpeditionOverlay.cs
Accessible overlay for the Daily Expedition (Daily Challenge) popup.
Provides flat list navigation with informational items, submenus for
difficulty selection and modifiers, and embark button.

Uses MenuBase Level 0 for the main list only. Submenus (difficulty,
modifiers) are handled entirely via _submenuMode and HandleSpecialKey,
which intercepts ALL keys when a submenu is active.

## class DailyExpeditionOverlay: MenuBase (line 17)

### Fields
- private enum ItemType (line 18)
  Values: Biome, TimeLeft, Races, EmbarkGoods, EmbarkEffects, Modifiers, SeasonalEffects, Rewards, Completed, Difficulty, Embark
- private enum SubmenuMode (line 31)
  Values: None, Difficulty, Modifiers
- private object _popup (line 39)
- private List<(ItemType type, string text)> _items (line 40)
- private SubmenuMode _submenuMode (line 43)
- private int _submenuIndex (line 44)
- private readonly TypeAheadSearch _submenuSearch (line 45)
- private readonly SubmenuSearchable _submenuSearchable (line 46)
- private List<object> _difficulties (line 49)
- private List<(string name, string description)> _modifiers (line 52)

### Properties
- protected override string OverlayName { get; } (line 62)
- protected override string EmptyMessage { get; } (line 64)
- protected override int SearchItemCount { get; } (line 165)
  Returns _items.Count only when no submenu is active.
- private class SubmenuSearchable: ISearchable (line 285)

### Methods
- public DailyExpeditionOverlay() (line 54)
- protected override int GetItemCount() (line 66)
- protected override string GetLabel(int index) (line 68)
- protected override void RefreshData() (line 72)
  Calls BuildStaticItems then BuildDifficultyDependentItems; always appends Difficulty and Embark last.
- protected override EnterAction OnEnter(int index) (line 93)
- protected override void OnAction(int index) (line 95)
  Dispatches Difficulty->submenu, Modifiers->submenu, Embark->embark, others->re-announce.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 120)
  When submenu active, routes ALL keys to ProcessSubmenuKey. Right arrow on Modifiers item opens submenu. Escape passes through to game at main list.
- protected override bool CanDrillDown(int index) (line 143)
  Always returns false; submenus are managed inline via _submenuMode, not MenuBase levels.
- protected override void StorePopup(object popup) (line 145)
- protected override string GetOpenAnnouncement() (line 149)
- protected override void OnClosed() (line 155)
- protected override string GetSearchName(int index) (line 168)
- private bool ProcessSubmenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 176)
  Full key handler for active submenu: Up/Down navigate, Home/End jump, Enter selects (difficulty) or re-announces (modifiers), Escape closes submenu.
- private void NavigateSubmenu(int direction) (line 239)
- private int GetSubmenuCount() (line 247)
- private void AnnounceSubmenuItem() (line 258)
- private void CloseSubmenu(bool announce) (line 269)
- private void OpenDifficultySubmenu() (line 319)
  Pre-selects the current difficulty in the submenu list.
- private void AnnounceDifficultyItem() (line 346)
  Appends ", current" to the item matching the active difficulty.
- private void SelectDifficulty() (line 364)
  On success: closes submenu, calls RefreshDifficultyDependentItems, and updates CurrentIndex to Difficulty item.
- private void OpenModifiersSubmenu() (line 392)
- private void AnnounceModifierItem() (line 407)
- private void TriggerEmbark() (line 423)
- private void BuildStaticItems() (line 438)
  Adds Biome, TimeLeft, Races, EmbarkGoods, EmbarkEffects, Modifiers (if any); all are informational except Modifiers.
- private void BuildDifficultyDependentItems(object difficulty) (line 472)
  Adds SeasonalEffects, Rewards (with "already completed" handling), and Completed status.
- private void RefreshDifficultyDependentItems() (line 502)
  Removes and rebuilds all difficulty-dependent items in-place; moves CurrentIndex to Difficulty item.
