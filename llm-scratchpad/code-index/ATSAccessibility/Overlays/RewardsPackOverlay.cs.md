# RewardsPackOverlay.cs
Overlay for RewardsPackPopup (shown after port expedition rewards are granted).
Provides flat list navigation through goods and effects received.

## class RewardsPackOverlay: MenuBase (line 13)

### Fields
- private object _popup (line 15)
- private List<string> _items (line 16)
- private static bool _typesCached (line 19)
- private static FieldInfo _goodsSlotsField (line 20)
- private static FieldInfo _effectsSlotsField (line 21)
- private static FieldInfo _headerField (line 22)
- private static FieldInfo _descField (line 23)

### Properties
- protected override string OverlayName { get; } (line 29)
- protected override string EmptyMessage { get; } (line 30)
- protected override int SearchItemCount { get; } (line 102)
  Always 0; no search for rewards overlay

### Methods
- protected override int GetItemCount() (line 32)
- protected override string GetLabel(int index) (line 34)
- protected override void RefreshData() (line 40)
  Reads goods slots then effects slots; skips inactive GameObjects; goods show amount if > 1
- protected override EnterAction OnEnter(int index) (line 96)
- protected override void OnAction(int index) (line 98)
  Calls Dismiss() regardless of which item is focused
- protected override EscapeAction OnEscape() (line 104)
  Dismisses, speaks "Closed", blocks cancel-once, returns Close
- protected override void StorePopup(object popup) (line 111)
- protected override string GetOpenAnnouncement() (line 116)
  Uses flavor text from _descField if available; falls back to OverlayName
- protected override void OnClosed() (line 125)
- public static bool IsRewardsPackPopup(object popup) (line 134)
  Type-name check: "RewardsPackPopup"
- private void Dismiss() (line 143)
  Hides the popup via PopupReflection.HidePopup
- private string GetPopupText(FieldInfo textField) (line 155)
  Reads TMP text from a field on the popup object via PopupReflection.GetTmpText
- private static void EnsureTypes() (line 164)
  Caches reflection for RewardsPackPopup fields (goodsSlots, effectsSlots, header, desc)
