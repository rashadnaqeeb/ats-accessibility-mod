# PopupReflection.cs
Reflection helpers for shared popup slot types (GoodSlot, EffectSlot, Good, EffectModel) and Popup.Hide. Used by AssaultResultOverlay, RewardsPackOverlay, and CycleEndOverlay.

## class PopupReflection (line 7)

### Fields
- private static Type `_goodSlotType` (line ~12)
- private static FieldInfo `_goodSlotGoodField` (line ~13)
- private static Type `_effectSlotType` (line ~16)
- private static FieldInfo `_effectSlotModelField` (line ~17)
- private static FieldInfo `_goodNameField` (line ~20)
- private static FieldInfo `_goodAmountField` (line ~21)
- private static PropertyInfo `_effectDisplayNameProperty` (line ~24)
- private static PropertyInfo `_effectDescriptionProperty` (line ~25)
- private static MethodInfo `_popupHideMethod` (line ~28)
- private static MethodInfo `_getGoodMethod` (line ~29)
- private static FieldInfo `_goodModelDisplayNameField` (line ~30)
- private static bool `_cached` (line ~33)

### Methods
- private static void `EnsureTypes()` (line 33)
- public static object `GetGoodFromSlot(object goodSlot)` (line 74)
- public static string `GetGoodName(object good)` (line 82)
- public static int `GetGoodAmount(object good)` (line 90)
- public static object `GetEffectModel(object effectSlot)` (line 98)
- public static string `GetEffectDisplayName(object effectModel)` (line 106)
- public static string `GetEffectDescription(object effectModel)` (line 114)
- public static bool `HidePopup(object popup)` (line 122)
  Calls the game's Popup.Hide() via reflection.
- public static string `GetTmpText(object textMeshPro)` (line 131)
  Reads the `text` property from any TMP_Text object.
- public static string `GetGoodDisplayName(string goodName)` (line 145)
  Resolves a good's localized display name from Settings.
- public static int `LogCacheStatus()` (line 164)
