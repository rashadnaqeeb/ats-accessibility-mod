# StatsReflection.cs
Cached reflection metadata for game statistics services: ReputationService, HostilityService, ResolveService, and VillagersService. Unlike other reflection files, exposes cached PropertyInfo/MethodInfo as public properties for direct use by consumers (e.g., EventAnnouncer).

## class StatsReflection (line 7)

### Fields
**Reputation service cache**
- private static PropertyInfo `_gsRepServiceProperty` (line ~15)
- private static PropertyInfo `_repReputationProperty` (line ~16)
- private static PropertyInfo `_repMaxReputationProperty` (line ~17)

**Hostility service cache**
- private static PropertyInfo `_gsHostilityServiceProperty` (line ~23)
- private static PropertyInfo `_hostilityValueProperty` (line ~24)

**Resolve service cache**
- private static PropertyInfo `_gsResolveServiceProperty` (line ~30)
- private static PropertyInfo `_resolveValueProperty` (line ~31)

**Villagers service cache**
- private static PropertyInfo `_gsVillagersServiceProperty` (line ~37)
- private static PropertyInfo `_villagersCountProperty` (line ~38)
- private static PropertyInfo `_villagersMaxCountProperty` (line ~39)
- private static PropertyInfo `_villagersHungryCountProperty` (line ~40)

**Enum types**
- private static Type `_seasonType` (line ~46)
- private static bool `_cached` (line ~49)

### Properties
- public static PropertyInfo `GsRepServiceProperty` (line ~55)
- public static PropertyInfo `RepReputationProperty` (line ~58)
- public static PropertyInfo `RepMaxReputationProperty` (line ~61)
- public static PropertyInfo `GsHostilityServiceProperty` (line ~64)
- public static PropertyInfo `HostilityValueProperty` (line ~67)
- public static PropertyInfo `GsResolveServiceProperty` (line ~70)
- public static PropertyInfo `ResolveValueProperty` (line ~73)
- public static PropertyInfo `GsVillagersServiceProperty` (line ~76)
- public static PropertyInfo `VillagersCountProperty` (line ~79)
- public static PropertyInfo `VillagersMaxCountProperty` (line ~82)
- public static PropertyInfo `VillagersHungryCountProperty` (line ~85)
- public static Type `SeasonType` (line ~88)

### Methods
- public static void `EnsureCached()` (line 88)
  Note: public unlike other reflection classes, so consumers can guarantee initialization.
- private static void `CacheReputationTypes(Assembly)` (line 101)
- private static void `CacheHostilityTypes(Assembly)` (line 120)
- private static void `CacheResolveTypes(Assembly)` (line 131)
- private static void `CacheVillagersTypes(Assembly)` (line 143)
- private static void `CacheEnumTypes(Assembly)` (line 150)
