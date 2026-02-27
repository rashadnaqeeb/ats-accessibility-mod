# WorldTutorialsOverlay.cs
Accessible overlay for the WorldTutorialsHUD.
Provides keyboard navigation for the 4 tutorial missions on the world map.
Opened via F1 key from WorldMapKeyHandler.

## class WorldTutorialsOverlay: MenuBase (line 13)

### Fields
- private List<TutorialReflection.TutorialInfo> _tutorials (line 15)

### Properties
- protected override string OverlayName { get; } (line 21)
- protected override string EmptyMessage { get; } (line 22)
- protected override int SearchItemCount { get; } (line 72)
  Always 0; search disabled

### Methods
- protected override int GetItemCount() (line 24)
- protected override string GetLabel(int index) (line 26)
  Appends ", locked" or ", completed" status suffix as appropriate
- protected override void RefreshData() (line 41)
- protected override EnterAction OnEnter(int index) (line 45)
- protected override void OnAction(int index) (line 47)
  Checks IsUnlocked; speaks locked reason if blocked; calls TutorialReflection.StartTutorial
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 74)
  F1: toggles WorldTutorialsHUD and closes overlay (F1 acts as a toggle)
- protected override EscapeAction OnEscape() (line 84)
  Toggles WorldTutorialsHUD and returns Close
- protected override void OnClosed() (line 90)
