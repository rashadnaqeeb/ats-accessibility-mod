# PerkCrafterReflection.cs
Reflection helpers for the PerkCrafterPopup (Cornerstone Forge). Provides access to hooks, positive/negative effect options, crafted perk state, pricing, and all interaction methods.

## class PerkCrafterReflection (line 7)

### Nested Classes
- public class `HookOption` (line 15): `Index`, `InternalName`, `DisplayName`, `Description`
- public class `EffectOption` (line 23): `Index`, `DisplayName`, `Description`, `EffectModel`
- public class `CraftedPerkInfo` (line 30): `DisplayName`, `Description`, `HookName`, `PositiveName`, `NegativeName`

### Fields
**Popup type cache**
- private static Type `_perkCrafterPopupType` (line ~50)
- private static PropertyInfo `_pcInstanceProperty` (line ~51)
- private static MethodInfo `_pcIsShownMethod` (line ~52)

**PerkCrafter service cache**
- private static PropertyInfo `_gsPerkCrafterServiceProperty` (line ~58)
- private static MethodInfo `_pcGetPerkCrafterMethod` (line ~59)

**State cache**
- private static MethodInfo `_getStateMethod` (line ~65)
- private static FieldInfo `_stateCraftingStateField` (line ~66)
- private static FieldInfo `_stateUsesLeftField` (line ~67)
- private static FieldInfo `_stateTotalChargesField` (line ~68)
- private static FieldInfo `_stateCraftedPerksField` (line ~69)
- private static FieldInfo `_statePickedHookIndexField` (line ~70)
- private static FieldInfo `_statePickedPositiveIndexField` (line ~71)
- private static FieldInfo `_statePickedNegativeIndexField` (line ~72)
- private static FieldInfo `_stateIsNegativePickedField` (line ~73)

**Model cache**
- private static MethodInfo `_getModelMethod` (line ~79)
- private static FieldInfo `_modelHooksField` (line ~80)
- private static FieldInfo `_modelElementsContainerField` (line ~81)
- private static FieldInfo `_modelNpcDialogueField` (line ~82)

**Elements cache**
- private static FieldInfo `_containerPositiveEffectsField` (line ~88)
- private static FieldInfo `_containerNegativeEffectsField` (line ~89)

**Effect model cache**
- private static PropertyInfo `_effectDisplayNameProperty` (line ~95)
- private static PropertyInfo `_effectDescriptionProperty` (line ~96)

**Storage cache**
- private static PropertyInfo `_gsStorageServiceProperty` (line ~102)
- private static MethodInfo `_getAmountMethod` (line ~103)
- private static MethodInfo `_craftMethod` (line ~104)
- private static MethodInfo `_getPriceMethod` (line ~105)
- private static MethodInfo `_selectHookMethod` (line ~106)
- private static MethodInfo `_selectPositiveMethod` (line ~107)
- private static MethodInfo `_selectNegativeMethod` (line ~108)
- private static MethodInfo `_getCraftedPerksMethod` (line ~109)
- private static MethodInfo `_setResultNameMethod` (line ~110)
- private static MethodInfo `_randomizeNameMethod` (line ~111)
- private static FieldInfo `_resultNameField` (line ~112)
- private static bool `_typesCached` (line ~115)

### Methods
- private static void `EnsureTypesCached()` (line 123)
- private static void `CachePopupTypes(Assembly)` (line ~133)
- private static void `CachePerkCrafterTypes(Assembly)` (line ~143)
- private static void `CacheStateTypes(Assembly)` (line ~153)
- private static void `CacheModelTypes(Assembly)` (line ~163)
- private static void `CacheElementsTypes(Assembly)` (line ~173)
- private static void `CacheEffectTypes(Assembly)` (line ~183)
- private static void `CacheStorageTypes(Assembly)` (line ~193)
- public static object `GetPopupInstance()` (line 267)
- public static bool `IsPopupShown()` (line 280)
- public static bool `IsPerkCrafterPopup(object popup)` (line 288)
- private static object `GetPerkCrafter()` (line 296)
- private static object `GetState()` (line 304)
- public static int `GetCraftingState()` (line 312)
  Returns enum int value: 0=Idle, 1=HookPicked, 2=AllPicked.
- private static object `GetModel()` (line 320)
- private static object `GetElementsContainer()` (line 328)
- public static string `GetNpcDialogue()` (line 340)
- public static bool `HasUsedAllCharges()` (line 357)
- public static int `GetUsesLeft()` (line 365)
- public static int `GetTotalCharges()` (line 373)
- public static int `GetCraftedPerksCount()` (line 383)
- public static bool `IsNegativePicked()` (line 391)
- public static int `GetPickedHookIndex()` (line 403)
- public static int `GetPickedPositiveIndex()` (line 411)
- public static int `GetPickedNegativeIndex()` (line 419)
- public static List<HookOption> `GetHookOptions()` (line 433)
- private static string `GetHookDisplayName(object hook, int index)` (line 475)
- private static string `CleanInternalName(string name)` (line 488)
  Removes internal suffixes/prefixes from hook/effect names for display.
- public static List<EffectOption> `GetPositiveOptions()` (line 507)
- public static List<EffectOption> `GetNegativeOptions()` (line 513)
- private static List<EffectOption> `GetEffectOptions(FieldInfo field, bool isNegative)` (line 518)
- private static HookOption `GetCurrentHook()` (line 564)
- private static EffectOption `GetCurrentPositive()` (line 577)
- private static EffectOption `GetCurrentNegative()` (line 590)
- public static string `GetResultName()` (line 608)
  Returns the player-editable name for the perk being crafted.
- public static bool `SetResultName(string name)` (line 629)
- public static bool `RandomizeName()` (line 639)
- public static (int amount, string goodDisplayName) `GetPrice()` (line 672)
- public static int `GetStorageAmount(string goodName)` (line 690)
- public static bool `CanAffordCraft()` (line 721)
- public static bool `PerformCraft()` (line 729)
- public static bool `SelectHook(HookOption option)` (line 741)
- public static bool `SelectPositive(EffectOption option)` (line 750)
- public static bool `SelectNegative(EffectOption option)` (line 759)
- public static List<CraftedPerkInfo> `GetCraftedPerks()` (line 781)
- public static int `LogCacheStatus()` (line 813)
