# TileInfoReflection.cs
Reflection helpers for tile info objects: natural resources, deposits, buildings, and their models/states. Uses per-type dictionaries for polymorphic game types (each concrete type may have different PropertyInfo/FieldInfo).

## class TileInfoReflection (line 7)

### Fields
**Per-type dictionaries (cache by runtime Type)**
- private static Dictionary<Type, PropertyInfo> `_naturalResourceModelProps` (line ~12)
- private static Dictionary<Type, PropertyInfo> `_naturalResourceStateProps` (line ~13)
- private static Dictionary<Type, FieldInfo> `_resourceStateChargesLeftFields` (line ~14)
- private static Dictionary<Type, FieldInfo> `_resourceModelChargesFields` (line ~15)
- private static Dictionary<Type, PropertyInfo> `_resourceModelRefGoodNameProps` (line ~16)
- private static Dictionary<Type, PropertyInfo> `_depositModelProps` (line ~20)
- private static Dictionary<Type, PropertyInfo> `_depositStateProps` (line ~21)
- private static Dictionary<Type, PropertyInfo> `_depositModelDescProps` (line ~22)
- private static Dictionary<Type, FieldInfo> `_depositStateChargesLeftFields` (line ~23)
- private static Dictionary<Type, FieldInfo> `_depositStateMaxChargesFields` (line ~24)
- private static Dictionary<Type, PropertyInfo> `_buildingModelProps` (line ~28)
- private static Dictionary<Type, PropertyInfo> `_buildingModelDescProps` (line ~29)

**Shared model fields (lazily cached once)**
- private static FieldInfo `_displayNameField` (line ~35)
- private static bool `_sharedCached` (line ~36)

**Service cache**
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~42)
- private static MethodInfo `_getSeasonMethod` (line ~43)
- private static bool `_serviceCached` (line ~44)

### Methods
**Natural resource accessors**
- public static PropertyInfo `GetNaturalResourceModelProp(Type type)` (line 27)
- public static PropertyInfo `GetNaturalResourceStateProp(Type type)` (line 34)
- public static FieldInfo `GetResourceStateChargesLeftField(Type type)` (line 43)
- public static FieldInfo `GetResourceModelChargesField(Type type)` (line 51)
- public static PropertyInfo `GetResourceModelRefGoodNameProp(Type type)` (line 59)

**Deposit accessors**
- public static PropertyInfo `GetDepositModelProp(Type type)` (line 77)
- public static PropertyInfo `GetDepositStateProp(Type type)` (line 84)
- public static PropertyInfo `GetDepositModelDescProp(Type type)` (line 92)
- public static FieldInfo `GetDepositStateChargesLeftField(Type type)` (line 101)
- public static FieldInfo `GetDepositStateMaxChargesField(Type type)` (line 109)

**Building accessors**
- public static PropertyInfo `GetBuildingModelProp(Type type)` (line 124)
- public static PropertyInfo `GetBuildingModelDescProp(Type type)` (line 132)

**Shared/utility methods**
- private static void `EnsureSharedCache(object instance)` (line 157)
  Lazily caches the `displayName` FieldInfo from the first encountered game object.
- private static void `EnsureServiceCache(object instance, object gameServices)` (line 202)
  Lazily caches the calendar service property and season method.
- public static bool `GetGladeWasDiscovered(object glade)` (line 220)
- public static string `GetLocalizedText(object instance, string fieldName)` (line 235)
  Reads any LocaText field by name from an arbitrary object.
- public static PropertyInfo `GetDescriptionProperty(object instance)` (line 252)
  Lazily resolves a `Description` property from an arbitrary instance's type.
