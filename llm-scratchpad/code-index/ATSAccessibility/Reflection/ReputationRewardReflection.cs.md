# ReputationRewardReflection.cs
Reflection helpers for ReputationRewardsPopup data and interaction. Provides methods to query current reward options, pick buildings, reroll, and extend.

## class ReputationRewardReflection (line 12)

### Nested Classes
- public class `RewardOption` (line 17): `Model` (BuildingModel), `DisplayName`, `Description`

### Fields
**IReputationRewardsService methods**
- private static MethodInfo `_rrsGetCurrentPicksMethod` (line 28)
- private static MethodInfo `_rrsCanAffordRerollMethod` (line 29)
- private static MethodInfo `_rrsRerollMethod` (line 30)
- private static MethodInfo `_rrsGetRerollPriceMethod` (line 31)
- private static MethodInfo `_rrsCanExtendMethod` (line 32)
- private static MethodInfo `_rrsCanAffordExtendMethod` (line 33)
- private static MethodInfo `_rrsExtendMethod` (line 34)

**ReputationReward fields**
- private static FieldInfo `_rrBuildingField` (line 37)

**Settings methods**
- private static MethodInfo `_settingsGetBuildingMethod` (line 40)

**BuildingModel properties**
- private static PropertyInfo `_bmListDescriptionProperty` (line 43)

**Good struct fields**
- private static FieldInfo `_goodNameField` (line 46)
- private static FieldInfo `_goodAmountField` (line 47)

**Biome extend cost fields**
- private static PropertyInfo `_gsBiomeServiceProperty` (line 50)
- private static PropertyInfo `_bsBlueprintsProperty` (line 51)
- private static FieldInfo `_bbcExtendCostField` (line 52)

**Popup private methods**
- private static MethodInfo `_rpOnBuildingPickedMethod` (line 55)
- private static MethodInfo `_rpRerollMethod` (line 56)
- private static FieldInfo `_rpTextTyperField` (line 59)
- private static FieldInfo `_ttTextMeshField` (line 60)
- private static PropertyInfo `_tmpTextProperty` (line 61)
- private static bool `_typesCached` (line 63)

### Methods
- private static void `EnsureTypesCached()` (line 69)
- private static void `CacheServiceMethods(Assembly)` (line 84)
- private static void `CacheRewardTypes(Assembly)` (line 104)
- private static void `CacheSettingsMethods(Assembly)` (line 112)
- private static void `CacheBuildingModelTypes(Assembly)` (line 120)
- private static void `CacheGoodTypes(Assembly)` (line 128)
- private static void `CacheBiomeTypes(Assembly)` (line 138)
- private static void `CachePopupTypes(Assembly)` (line 159)
- private static object `GetService()` (line 182)
- private static object `GetBiomeService()` (line 186)
- public static bool `IsReputationRewardsPopup(object popup)` (line 198)
  Checks by type name rather than cached Type for robustness.
- public static bool `IsTutorialMode()` (line 207)
  Returns true if the first tutorial is active and reputation is 0 (matches game's IsTutorialDesc() logic).
- public static string `GetPopupDescription(object popup)` (line 261)
  Reads tutorial description text from textTyper.textMesh.text.
- public static List<RewardOption> `GetCurrentOptions()` (line 288)
- public static bool `PickBuilding(object popup, object buildingModel)` (line 331)
  Calls popup's private OnBuildingPicked method to trigger the full pick flow.
- public static bool `CanAffordReroll()` (line 345)
- public static bool `Reroll(object popup)` (line 357)
  Calls popup's private Reroll method so the UI price slot updates correctly.
- public static (int amount, string goodDisplayName) `GetRerollCost()` (line 367)
- public static bool `CanExtend()` (line 394)
- public static bool `CanAffordExtend()` (line 405)
- public static bool `Extend()` (line 416)
- public static (int amount, string goodDisplayName) `GetExtendCost()` (line 428)
  Reads from BiomeService.Blueprints.extendCost (GoodRef).
- public static int `LogCacheStatus()` (line 452)
