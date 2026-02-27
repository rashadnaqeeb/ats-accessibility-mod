# EventReflection.cs

Provides cached reflection metadata for EventAnnouncer's game service access.
Caches PropertyInfo/MethodInfo/FieldInfo (safe to cache across scenes), but never caches service instances (destroyed on scene change).

Unlike other reflection files, this class has multiple independent cache groups with separate `bool _*Cached` guards, each initialized lazily from a live instance or from assembly scanning.

## class EventReflection (line 11)

### Fields and Properties — Group 1: Service Properties (line 13)

Private fields:
- private static bool _reflectionCached (line 16)
- private static PropertyInfo _calendarServiceProperty (line 17)
- private static PropertyInfo _hostilityServiceProperty (line 18)
- private static PropertyInfo _tradeServiceProperty (line 19)
- private static PropertyInfo _ordersServiceProperty (line 20)
- private static PropertyInfo _gladesServiceProperty (line 21)
- private static PropertyInfo _reputationServiceProperty (line 22)
- private static PropertyInfo _newsServiceProperty (line 23)
- private static PropertyInfo _newcomersServiceProperty (line 24)
- private static PropertyInfo _reputationRewardsServiceProperty (line 25)
- private static PropertyInfo _cornerstonesServiceProperty (line 26)
- private static PropertyInfo _monitorsServiceProperty (line 27)
- private static PropertyInfo _villagersServiceProperty (line 28)

Public read-only property accessors (IGameServices PropertyInfos):
- public static PropertyInfo CalendarServiceProperty (line 30)
- public static PropertyInfo HostilityServiceProperty (line 31)
- public static PropertyInfo TradeServiceProperty (line 32)
- public static PropertyInfo OrdersServiceProperty (line 33)
- public static PropertyInfo GladesServiceProperty (line 34)
- public static PropertyInfo ReputationServiceProperty (line 35)
- public static PropertyInfo NewsServiceProperty (line 36)
- public static PropertyInfo NewcomersServiceProperty (line 37)
- public static PropertyInfo ReputationRewardsServiceProperty (line 38)
- public static PropertyInfo CornerstonesServiceProperty (line 39)
- public static PropertyInfo MonitorsServiceProperty (line 40)
- public static PropertyInfo VillagersServiceProperty (line 41)

### Fields and Properties — Group 2: Villager Reflection (line 67)

Private fields:
- private static bool _villagerReflectionCached (line 71)
- private static MethodInfo _villagerGetDisplayNameMethod (line 72)
- private static FieldInfo _villagerStateField (line 73)
- private static FieldInfo _villagerStateLossTypeField (line 74)
- private static FieldInfo _villagerStateLossReasonField (line 75)
- private static FieldInfo _villagerLastWorkIdField (line 76)

Public read-only property accessors:
- public static MethodInfo VillagerGetDisplayNameMethod (line 78)
- public static FieldInfo VillagerStateField (line 79)
- public static FieldInfo VillagerStateLossTypeField (line 80)
- public static FieldInfo VillagerStateLossReasonField (line 81)
- public static FieldInfo VillagerLastWorkIdField (line 82)

### Fields and Properties — Group 3: Alert Fields (line 102)

Private fields:
- private static bool _alertFieldsCached (line 106)
- private static FieldInfo _alertTextField (line 107)
- private static FieldInfo _alertDismissedField (line 108)
- private static FieldInfo _alertShowTimeField (line 109)

Public read-only property accessors:
- public static FieldInfo AlertTextField (line 111)
- public static FieldInfo AlertDismissedField (line 112)
- public static FieldInfo AlertShowTimeField (line 113)

### Fields and Properties — Group 4: Glade Fields (line 125)

Private fields:
- private static bool _gladeFieldsCached (line 129)
- private static FieldInfo _gladeFieldsField (line 130)

Public read-only property accessor:
- public static FieldInfo GladeFieldsField (line 132)

### Fields and Properties — Group 5: Glade Danger Level (line 141)

Private fields:
- private static bool _gladesGetDangerLevelCached (line 145)
- private static MethodInfo _gladesGetDangerLevelMethod (line 146)

Public read-only property accessor:
- public static MethodInfo GladesGetDangerLevelMethod (line 148)

### Fields — Group 6: Effect Properties (line 157)

Private fields:
- private static bool _effectPropsCached (line 161)
- private static PropertyInfo _effectDisplayNameProperty (line 162)
- private static PropertyInfo _effectDescriptionProperty (line 163)

### Methods

#### Group 1
- public static void EnsureReflectionCached() (line 43)
  Initializes all IGameServices property infos via ReflectionHelper.InitCache.

#### Group 2
- public static void EnsureVillagerReflectionCached(object villager) (line 84)
  Caches villager reflection from a live villager instance (not from assembly scan).

#### Group 3
- public static void EnsureAlertFieldsCached(object alert) (line 115)
  Caches alert fields from a live alert instance.

#### Group 4
- public static void EnsureGladeFieldsCached(object gladeState) (line 134)
  Caches glade fields from a live glade state instance.

#### Group 5
- public static void EnsureGladesGetDangerLevelCached(object gladesService) (line 150)
  Caches GetDangerLevel method from a live glades service instance.

#### Group 6
- public static void EnsureEffectPropertyCached() (line 165)
- public static string GetEffectDisplayName(object effectModel) (line 180)
- public static string GetEffectDescription(object effectModel) (line 188)

#### Cache Reset
- public static void ResetCache() (line 206)
  Resets all cached flags so reflection gets re-cached on next game. Called from EventAnnouncer.Dispose().
