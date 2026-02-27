# PerkCrafterOverlay.cs
Accessible overlay for the PerkCrafterPopup (Cornerstone Forge).
Level 0: Main menu (7 crafting items) or finished mode (dialogue + crafted perks).
Level 1: Submenu for hook/positive/negative selection.
Level 2: Name editing (handled entirely in HandleSpecialKey).

## class PerkCrafterOverlay: MenuBase (line 15)

### Fields
- private enum MenuItem { Dialogue=0, Shards=1, Hook=2, Positive=3, Negative=4, Result=5, Craft=6 } (line 16)
- private MenuItem _activeSubmenu (line 26)
- private bool _isFinishedMode (line 27)
- private List<PerkCrafterReflection.HookOption> _hookOptions (line 29)
- private List<PerkCrafterReflection.EffectOption> _positiveOptions (line 30)
- private List<PerkCrafterReflection.EffectOption> _negativeOptions (line 31)
- private List<PerkCrafterReflection.CraftedPerkInfo> _craftedPerks (line 32)
- private StringBuilder _nameBuffer (line 34)
- private bool _nameEditing (line 35)

### Properties
- protected override string OverlayName { get; } (line 41)
- protected override string EmptyMessage { get; } (line 42)
- protected override int SearchItemCount { get; } (line 186)
  Non-zero only at Level 1.
- protected override int SearchCurrentIndex { get; } (line 193)

### Methods
- protected override int GetItemCount() (line 44)
  Finished mode: 1 (dialogue) + crafted perk count. Normal: 7. Level 1: submenu count.
- protected override string GetLabel(int index) (line 57)
- protected override void RefreshData() (line 68)
  Checks finished mode; loads crafted perks in finished mode, options in normal mode.
- protected override EnterAction OnEnter(int index) (line 80)
  Hook/Positive/Negative items DrillDown; others Action. In finished mode: None.
- protected override bool CanDrillDown(int index) (line 95)
  True only for Hook/Positive/Negative at Level 0 (Right arrow to enter submenu).
- protected override void OnDrillDown(int index) (line 101)
  Records which submenu is active (_activeSubmenu = item).
- protected override void OnAction(int index) (line 106)
- protected override void OnGoBack() (line 114)
  Resets _activeSubmenu to Dialogue when leaving Level 1.
- protected override EscapeAction OnEscape() (line 118)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 124)
  At Level 2: routes all keys to ProcessNameEditKey. At Level 1: Right arrow selects item. In finished mode at Level 0: Enter/Right re-announces.
- protected override string GetOpenAnnouncement() (line 146)
- protected override void AnnounceCurrentItem() (line 163)
  Dispatches to AnnounceFinishedItem, AnnounceMainMenuItem, or AnnounceSubmenuItem.
- protected override void OnClosed() (line 177)
- protected override string GetSearchName(int index) (line 195)
  Negative submenu has an extra "None" item at index 0; real options are offset by 1.
- private string GetMainMenuLabel(int index) (line 221)
  Returns dynamic labels for each of the 7 main menu items using live reflection data.
- private void AnnounceMainMenuItem() (line 277)
- private void ActivateMainMenuItem(int index) (line 287)
  Dialogue/Shards re-announce; Result opens name edit; Craft performs craft.
- private int GetSubmenuItemCount() (line 307)
  Negative count = negativeOptions.Count + 1 (for the "None" option).
- private string GetSubmenuLabel(int index) (line 320)
- private void AnnounceSubmenuItem() (line 340)
- private void SelectSubmenuItem() (line 346)
  Calls the appropriate PerkCrafterReflection.Select* method, then returns to Level 0 and clears search.
- private void OpenNameEdit() (line 385)
  Copies current name to buffer, sets Level 2, announces editing instructions.
- private bool ProcessNameEditKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 394)
  Handles the full name-editing keyboard: letters clear-on-first-key then append, Backspace removes last, Alt+R randomizes, Enter confirms, Escape cancels.
- private void ConfirmNameEdit() (line 443)
- private void CancelNameEdit() (line 456)
- private void RandomizeName() (line 462)
- private void PerformCraft() (line 478)
- private string GetFinishedLabel(int index) (line 509)
  Index 0 is dialogue; index 1+ map to crafted perks (offset by 1).
- private void AnnounceFinishedItem() (line 525)
- private void ClearData() (line 535)
