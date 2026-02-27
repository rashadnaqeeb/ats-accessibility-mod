# IronmanReflection.cs
Reflection helpers for accessing the Ironman (Queen's Hand Trial) upgrade popup, its state, and all interaction methods.

## class IronmanReflection (line 11)

### Fields
- private static Type `_ironmanUpgradePopupType` (line 35)
- private static PropertyInfo `_msIronmanServiceProperty` (line 38)
- private static MethodInfo `_getCompletedPicksMethod` (line 41)
- private static MethodInfo `_hasReachedMaxPicksMethod` (line 42)
- private static MethodInfo `_getCurrentPickMethod` (line 43)
- private static MethodInfo `_canAffordMethod` (line 44)
- private static MethodInfo `_pickMethod` (line 45)
- private static MethodInfo `_isUnlockedMethod` (line 46)
- private static MethodInfo `_isCoreMethod` (line 47)
- private static FieldInfo `_settingsIronmanConfigField` (line 50)
- private static FieldInfo `_configCoreUpgradesField` (line 51)
- private static FieldInfo `_configPicksField` (line 52)
- private static FieldInfo `_pickStateOptionsField` (line 55)
- private static FieldInfo `_optionModelField` (line 56)
- private static MethodInfo `_getCapitalUpgradeMethod` (line 59)
- private static FieldInfo `_ironmanDisplayNameField` (line 62)
- private static FieldInfo `_ironmanPriceField` (line 63)
- private static FieldInfo `_upgradeRewardsField` (line 64)
- private static FieldInfo `_currencyRefCurrencyField` (line 67)
- private static FieldInfo `_currencyRefAmountField` (line 68)
- private static PropertyInfo `_currencyModelDisplayNameProperty` (line 71)
- private static PropertyInfo `_rewardDisplayNameProperty` (line 74)
- private static PropertyInfo `_rewardDescriptionProperty` (line 75)
- private static PropertyInfo `_msMetaStateServiceProperty` (line 78)
- private static PropertyInfo `_metaStateCapitalProperty` (line 79)
- private static FieldInfo `_capitalUnlockedUpgradesField` (line 80)
- private static bool `_typesCached` (line 83)

### Nested Structs
- public struct `UpgradeInfo` (line 15): `Model`, `DisplayName`, `PriceText`, `IsCore`, `IsUnlocked`, `Rewards`
- public struct `RewardInfo` (line 24): `DisplayName`, `Description`

### Methods
- private static void `EnsureTypes()` (line 77)
  Caches all metadata. Note: `_msIronmanServiceProperty` is accessed from IMetaServices, not IGameServices.
- public static bool `IsIronmanUpgradePopup(object popup)` (line 209)
- private static object `GetIronmanService()` (line 219)
- public static int `GetCompletedPicks()` (line 227)
  Returns how many upgrades the player has already picked.
- public static int `GetMaxPicks()` (line 236)
  Returns the total number of picks available from config.
- public static bool `HasReachedMaxPicks()` (line 249)
- public static List<UpgradeInfo> `GetCurrentPickOptions()` (line 260)
  Returns the upgrades available to choose from in the current pick state.
- public static List<UpgradeInfo> `GetCoreUpgrades()` (line 294)
  Returns all core (always available) upgrades from settings config.
- public static List<UpgradeInfo> `GetUnlockedUpgrades()` (line 319)
  Returns all capital upgrades the player has previously unlocked (from CapitalState).
- private static UpgradeInfo `CreateUpgradeInfo(object upgrade, object upgradeModel, bool isCore)` (line 358)
- private static string `GetIronmanDisplayName(object upgradeModel)` (line 377)
- private static string `GetIronmanPriceText(object upgradeModel)` (line 384)
  Formats the price as "N CurrencyName" using CurrencyRef fields.
- public static bool `CanAfford(object service, object upgradeModel)` (line 410)
- public static bool `IsUnlocked(object service, object upgradeModel)` (line 419)
- public static bool `Pick(object upgradeModel)` (line 429)
  Performs the upgrade pick via the IronmanService.
- public static List<RewardInfo> `GetRewards(object upgradeModel)` (line 438)
- public static int `LogCacheStatus()` (line 459)
