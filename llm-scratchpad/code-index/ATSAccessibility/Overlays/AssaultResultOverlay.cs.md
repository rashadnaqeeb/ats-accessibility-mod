# AssaultResultOverlay.cs
Overlay for TraderAssaultResultPopup (shown after assaulting a trader).
Provides flat list navigation through stolen goods, perks, consequences, and villagers lost.

## class AssaultResultOverlay: MenuBase (line 13)

### Fields
- private object _popup (line 15)
- private List<string> _items (line 16)
- private static bool _typesCached (line 19)
- private static FieldInfo _descField (line 20)
- private static FieldInfo _villagersKilledField (line 21)
- private static FieldInfo _gainedGoodsSlotsField (line 22)
- private static FieldInfo _gainedRewardsSlotsField (line 23)
- private static FieldInfo _effectsRewardSlotsField (line 24)

### Properties
- protected override string OverlayName { get; } (line 30)
- protected override string EmptyMessage { get; } (line 31)
- protected override int SearchItemCount { get; } (line 76)

### Methods
- protected override int GetItemCount() (line 33)
- protected override string GetLabel(int index) (line 35)
- protected override void RefreshData() (line 41)
  Builds flat string list: description, villagers lost, stolen goods, stolen perks, consequences.
- protected override EnterAction OnEnter(int index) (line 70)
- protected override void OnAction(int index) (line 72)
  Calls Dismiss() regardless of which item is activated.
- protected override EscapeAction OnEscape() (line 78)
  Dismisses and sets InputBlocker.BlockCancelOnce to prevent the game from double-processing Escape.
- protected override void StorePopup(object popup) (line 85)
- protected override void OnClosed() (line 90)
- public static bool IsAssaultResultPopup(object popup) (line 99)
- private void Dismiss() (line 108)
- private void ReadGoodsSlots(FieldInfo slotsField, string prefix) (line 120)
  Iterates a reflected list of good-slot MonoBehaviours; skips inactive GameObjects.
- private void ReadEffectsSlots(FieldInfo slotsField, string prefix) (line 147)
  Iterates a reflected list of effect-slot MonoBehaviours; skips inactive GameObjects.
- private string GetTextFieldValue(FieldInfo textField) (line 178)
  Reads a TMP text component value from a field on the popup object.
- private static void EnsureTypes() (line 187)
  Caches FieldInfo for TraderAssaultResultPopup fields via ReflectionHelper.InitCache.
