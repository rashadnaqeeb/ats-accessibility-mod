# AltarOverlay.cs
Accessible overlay for the Forsaken Altar panel.
Provides multi-level navigation: Main Menu -> Resources/Cornerstones -> Currencies/Races.

## class AltarOverlay: MenuBase (line 12)

### Fields
- private enum MenuLevel (line 17)
  Values: Main, Resources, Currencies, Races, Cornerstones
- private enum MainItem (line 25)
  Values: Resources, Cornerstones, Skip
- private enum ResourceItem (line 27)
  Values: Currencies, Villagers
- private bool _isActive (line 33)
- private MenuLevel _menuLevel (line 34)
- private List<AltarReflection.CurrencyInfo> _currencies (line 36)
- private List<AltarReflection.RaceInfo> _races (line 37)
- private List<AltarReflection.EffectInfo> _cornerstones (line 38)

### Properties
- protected override string OverlayName { get; } (line 44)
- protected override string EmptyMessage { get; } (line 45)
- protected override int SearchItemCount { get; } (line 245)

### Methods
- protected override int GetItemCount() (line 47)
- protected override string GetLabel(int index) (line 58)
- protected override void RefreshData() (line 112)
- protected override EnterAction OnEnter(int index) (line 121)
- protected override void OnDrillDown(int index) (line 141)
- protected override void OnGoBack() (line 162)
- protected override void OnAction(int index) (line 175)
- protected override void OnSpace(int index) (line 193)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 208)
  Returns false (pass-through) on Escape when altar is inactive; returns true (consume) otherwise.
- protected override EscapeAction OnEscape() (line 217)
- protected override string GetOpenAnnouncement() (line 221)
  Returns inactive-state message with next charge threshold when altar is not active.
- protected override void OnClosed() (line 235)
- protected override string GetSearchName(int index) (line 256)
- private void ToggleCurrency() (line 278)
- private void ToggleRace() (line 291)
- private void ToggleVillagersMaster() (line 304)
- private void PurchaseCornerstone() (line 318)
  After purchase, if another pick is available, stays open and refreshes to next cornerstone list.
- private void ExecuteSkip() (line 349)
  After skip, if another pick is available, stays open and refreshes to main menu.
