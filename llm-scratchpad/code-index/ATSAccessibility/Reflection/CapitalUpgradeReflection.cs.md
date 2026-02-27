# CapitalUpgradeReflection.cs

## class CapitalUpgradeReflection (line 13)

### Nested Types
- public enum UpgradeStatus (line 16): `Unlocked`, `Buyable`, `TooExpensive`, `LevelRequired`, `Locked`
- public struct StructureInfo (line 18): fields `model`, `displayName`, `upgrades`
- public struct UpgradeInfo (line 25): fields `model`, `displayName`, `status`, `priceText`, `rewards`
- public struct RewardInfo (line 34): fields `displayName`, `description`

### Fields (private static cached)
- private static PropertyInfo _metaControllerProp (line 40)
- private static PropertyInfo _mcMetaServicesProp (line 41)
- private static PropertyInfo _capitalServiceProp (line 42)
- private static FieldInfo _capitalUpgradeRequestedField (line 43)
- private static PropertyInfo _capitalServiceStructuresProp (line 44)
- private static PropertyInfo _structureUpgradesProp (line 45)
- private static PropertyInfo _structureDisplayNameProp (line 46)
- private static PropertyInfo _upgradeDisplayNameProp (line 47)
- private static PropertyInfo _upgradeRewardsProp (line 48)
- private static PropertyInfo _upgradeStatusProp (line 49)
- private static PropertyInfo _upgradePriceProp (line 50)
- private static FieldInfo _upgradeIsCompletedField (line 51)
- private static FieldInfo _upgradeMinLevelField (line 52)
- private static FieldInfo _rewardDisplayNameField (line 53)
- private static FieldInfo _rewardDescriptionField (line 54)
- private static PropertyInfo _metaStateServiceProp (line 55)
- private static PropertyInfo _metaStateCurrentLevelProp (line 56)
- private static MethodInfo _canBuyUpgradeMethod (line 57)
- private static MethodInfo _buyUpgradeMethod (line 58)
- private static PropertyInfo _metaCurrencyPriceProp (line 59)
- private static PropertyInfo _metaCurrencyAmountProp (line 60)
- private static PropertyInfo _metaCurrencyNameProp (line 61)
- private static PropertyInfo _metaCurrencyDisplayNameProp (line 62)
- private static PropertyInfo _metaStateServiceCurrencyProp (line 63)
- private static MethodInfo _metaStateGetAmountMethod (line 64)
- private static bool _typesCached (line 78)

### Methods
- private static void EnsureTypes() (line 80)
- private static object GetCapitalService() (line 184)
- public static bool IsCapitalUpgradePopup(object popup) (line 193)
- public static List<StructureInfo> GetStructures() (line 193)
  Returns all capital structures with their upgrades.
- public static List<UpgradeInfo> GetUpgrades(object structure) (line 240)
  Returns upgrades for a given structure model.
- public static List<RewardInfo> GetRewards(object upgrade) (line 281)
  Returns reward info for a given upgrade model.
- public static void BuyUpgrade(object upgrade) (line 313)
  Fires CapitalUpgradeRequested.OnNext with the upgrade model.
- private static UpgradeStatus DetermineStatus(object upgrade, object metaStateService, int currentLevel) (line 339)
- private static bool IsLevelCompleted(object upgrade, object metaStateService) (line 378)
- private static string GetPriceText(object upgrade, object metaStateService) (line 385)
- public static int LogCacheStatus() (line 415)
