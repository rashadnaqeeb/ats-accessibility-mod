# MapReflection.cs
Reflection-based access to map objects: fields (tiles), glades, relics, villagers, and service dictionary properties. Used by MapNavigator and MapScanner. Unlike other reflection files, caches are lazily populated from the first encountered runtime object rather than from a known type name.

## class MapReflection (line 6)

### Fields
**Field (tile) cache**
- private static PropertyInfo `_fieldTypeProperty` (line ~20)
- private static PropertyInfo `_fieldIsTraversableProperty` (line ~21)

**Glade cache**
- private static FieldInfo `_gladeWasDiscoveredField` (line ~30)
- private static FieldInfo `_gladeDangerLevelField` (line ~31)
- private static PropertyInfo `_gladeFieldsProperty` (line ~32)
- private static FieldInfo `_gladeHasRewardChaseField` (line ~33)
- private static FieldInfo `_gladeRewardChaseEndField` (line ~34)
- private static FieldInfo `_gladeRelicsField` (line ~35)

**Relic cache**
- private static FieldInfo `_relicNameField` (line ~45)
- private static PropertyInfo `_relicPositionProperty` (line ~46)
- private static Type `_rewardChaseRelicType` (line ~47)

**Villager cache**
- private static PropertyInfo `_villagerPositionProperty` (line ~56)
- private static FieldInfo `_villagerRaceField` (line ~57)

**Service cache (per-type dictionaries)**
- private static PropertyInfo `_naturalResourcesProperty` (line ~65)
- private static PropertyInfo `_depositsProperty` (line ~66)
- private static PropertyInfo `_oresProperty` (line ~67)
- private static PropertyInfo `_springsProperty` (line ~68)
- private static PropertyInfo `_lakesProperty` (line ~69)
- private static PropertyInfo `_buildingsProperty` (line ~70)
- private static PropertyInfo `_objectDisplayNameProperty` (line ~75)
- private static PropertyInfo `_buildingDisplayNameProperty` (line ~76)
- private static PropertyInfo `_buildingPositionProperty` (line ~77)

### Methods
- private static void `EnsureFieldCached(object)` (line 26)
  Lazy-caches PropertyInfo from the first encountered field object's runtime type.
- public static string `GetFieldTypeName(object)` (line 43)
- public static bool `GetFieldIsTraversable(object)` (line 78)
- private static void `EnsureGladeCached(object)` (line 102)
  Lazy-caches from the first encountered glade object.
- private static void `EnsureGladeCachedFromList(object)` (line 123)
  Variant that resolves from a list's element type.
- public static bool `GetGladeWasDiscovered(object)` (line 136)
- public static int `GetGladeDangerLevelRaw(object)` (line 145)
- public static object `GetGladeFields(object)` (line 151)
- public static bool `GetGladeHasRewardChase(object)` (line 156)
- public static object `GetGladeRewardChaseEnd(object)` (line 161)
- public static object `GetGladeRelics(object)` (line 166)
- public static object `GetGladeFirstField(object)` (line 174)
  Returns the first field in a glade's field list, used to determine glade position.
- private static void `EnsureRelicCached(object)` (line 194)
- public static bool `IsRewardChaseRelic(object)` (line 208)
- public static string `GetRelicName(object)` (line 213)
- public static object `GetRelicPosition(object)` (line 218)
- private static void `EnsureVillagerCached(object)` (line 234)
- public static object `GetVillagerPosition(object)` (line 254)
- public static string `GetVillagerRace(object)` (line 268)
- public static object `GetNaturalResources(object)` (line 285)
- public static object `GetDeposits(object)` (line 292)
- public static object `GetOres(object)` (line 299)
- public static object `GetSprings(object)` (line 306)
- public static object `GetLakes(object)` (line 313)
- public static object `GetBuildings(object)` (line 320)
- public static string `GetObjectDisplayName(object)` (line 335)
  Lazy-caches `DisplayName` property from any game object type.
- public static string `GetBuildingDisplayName(object)` (line 374)
- public static object `GetBuildingPosition(object)` (line 406)
- public static string `GetResourceSizeType(object)` (line 425)
  Returns resource size type name (e.g., "Small", "Large") for tile info display.
