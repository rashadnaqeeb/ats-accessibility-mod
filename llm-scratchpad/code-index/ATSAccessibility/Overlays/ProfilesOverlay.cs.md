# ProfilesOverlay.cs
Accessible overlay for the ProfilesPopup (save selection screen).
Two-level navigation: Level 0 = save slots, Level 1 = submenu (actions per slot).
Supports text input for rename and confirmation for destructive actions.

## class ProfilesOverlay: MenuBase (line 14)

### Fields
- private enum ItemType { SaveSlot, CreateNew, SwitchMode } (line 19)
- private enum SubMenuItem { Name, Switch, Reset, Delete } (line 25)
- private enum ConfirmAction { None, Reset, Delete } (line 31)
- private bool _viewingQueensHand (line 42)
- private ConfirmAction _awaitingConfirm (line 44)
- private bool _editingName (line 46)
- private StringBuilder _editBuffer (line 47)
- private List<ProfileItem> _items (line 49)
- private List<SubMenuItem> _submenuItems (line 50)
- private object _currentSlotProfile (line 51)
- private class ProfileItem (line 57)
  - public ItemType Type (line 58)
  - public object Profile (line 59)
  - public bool IsCurrent (line 60)
  - public bool IsDefault (line 61)
  - public bool IsIronman (line 62)
  - public bool IsPickable (line 63)
  - public string DisplayName (line 64)
  - public string IronmanStatus (line 65)
  - public int SlotNumber (line 66)

### Properties
- protected override string OverlayName { get; } (line 73)
- protected override string EmptyMessage { get; } (line 74)
- protected override int SearchItemCount { get; } (line 175)
  Non-zero only at Level 0.
- protected override int SearchCurrentIndex { get; } (line 178)

### Methods
- protected override int GetItemCount() (line 76)
- protected override string GetLabel(int index) (line 82)
- protected override void RefreshData() (line 88)
- protected override EnterAction OnEnter(int index) (line 92)
  SaveSlot items DrillDown; CreateNew and SwitchMode items use Action.
- protected override bool CanDrillDown(int index) (line 101)
  True only for SaveSlot items at Level 0.
- protected override void OnDrillDown(int index) (line 107)
  Stores the selected profile reference and builds the submenu for it.
- protected override void OnAction(int index) (line 114)
  At Level 0: CreateNew or ToggleMode. At Level 1: ActivateSubmenuItem.
- protected override void OnGoBack() (line 133)
  Clears submenu and current slot reference.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 138)
  When editing name: routes to ProcessEditingKey. When awaiting confirmation: routes to ProcessConfirmKey. Otherwise null (no intercept).
- protected override EscapeAction OnEscape() (line 150)
- protected override string GetOpenAnnouncement() (line 157)
- protected override void OnClosed() (line 162)
- protected override string GetSearchName(int index) (line 181)
  Returns DisplayName (name only, without slot number prefix).
- private void RefreshItems() (line 190)
  Populates save slots, then fills remaining slots with CreateNew items up to maxSlots, then adds SwitchMode item if applicable.
- private void RefreshSubmenuItems() (line 238)
  Builds submenu: always Name; Switch if not current and pickable; Reset always; Delete only if not default and not ironman.
- private string GetItemAnnouncement(int index) (line 268)
- private string GetSubmenuItemLabel(int index) (line 294)
  Reset label varies for ironman slots (seed reset options).
- private void CreateNewProfile() (line 326)
- private void ToggleMode() (line 342)
  Flips _viewingQueensHand, resets index and search, refreshes items.
- private void ActivateSubmenuItem() (line 363)
  Dispatches to StartNameEditing, SwitchToProfile, or RequestConfirmation.
- private void SwitchToProfile() (line 387)
- private void RequestConfirmation(ConfirmAction action) (line 402)
  Sets _awaitingConfirm and announces the action with "Press Enter to confirm".
- private bool ProcessConfirmKey(KeyCode keyCode) (line 408)
  Enter executes; any other key cancels. Blocks Escape from closing popup via InputBlocker.
- private void ExecuteConfirmedAction() (line 421)
  Performs the confirmed Reset or Delete, then calls ExitSubmenu and refreshes.
- private void ExitSubmenu() (line 459)
  Clears submenu state and returns to Level 0.
- private void StartNameEditing() (line 470)
  Reads current name from reflection, announces it with editing instructions.
- private bool ProcessEditingKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 478)
  Full text input: letters (with shift), digits (alpha row and keypad), space, minus/underscore, period, backspace. Blocks Escape from closing popup.
- private void SaveName() (line 545)
  Trims buffer; rejects empty names; calls ProfilesReflection.RenameProfile; re-syncs _indices[0] to the renamed slot.
- private void CancelNameEditing() (line 576)
