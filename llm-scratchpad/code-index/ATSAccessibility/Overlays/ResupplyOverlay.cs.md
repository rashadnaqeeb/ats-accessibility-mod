# ResupplyOverlay.cs
Overlay for CycleEffectsPickPopup (Royal Resupply on World Map after winning
a settlement near negative modifiers). Player picks 1 of 3 rewards.

## class ResupplyOverlay: MenuBase (line 13)

### Fields
- private object _popup (line 15)
- private List<string> _items (line 16)
- private List<object> _slots (line 17)
- private static bool _typesCached (line 20)
- private static FieldInfo _slotsField (line 21)
- private static FieldInfo _slotModelField (line 22)
- private static PropertyInfo _modelDisplayNameProperty (line 23)
- private static PropertyInfo _modelDescriptionProperty (line 24)
- private static MethodInfo _slotOnClickMethod (line 25)

### Properties
- protected override string OverlayName { get; } (line 31)
- protected override string EmptyMessage { get; } (line 32)
- protected override int SearchItemCount { get; } (line 107)
  Always 0; search disabled for this overlay

### Methods
- protected override int GetItemCount() (line 34)
- protected override string GetLabel(int index) (line 36)
- protected override void RefreshData() (line 42)
  Reads slot list from popup via reflection; skips inactive GameObjects
- protected override EnterAction OnEnter(int index) (line 74)
- protected override void OnAction(int index) (line 76)
  Invokes the slot's OnClick method via reflection
- protected override EscapeAction OnEscape() (line 110)
- protected override void StorePopup(object popup) (line 112)
- protected override void OnClosed() (line 117)
- public static bool IsCycleEffectsPickPopup(object popup) (line 127)
  Type-name check: "CycleEffectsPickPopup"
- private static void EnsureTypes() (line 136)
  Caches reflection for CycleEffectsPickPopup, CycleEffectsPickSlot, and CycleEffectModel
