# ConsumptionReflection.cs

## class ConsumptionReflection (line 14)

### Fields (private static cached)
- private static Type _consumptionPanelType (line 19)
- private static PropertyInfo _consumptionServiceProp (line 20)
- private static PropertyInfo _needCategoriesProp (line 21)
- private static PropertyInfo _categoryIsHouseBasedProp (line 22)
- private static PropertyInfo _categoryDisplayNameProp (line 23)
- private static PropertyInfo _categoryNeedsProp (line 24)
- private static PropertyInfo _needIsProhibitableProp (line 25)
- private static PropertyInfo _needDisplayNameProp (line 26)
- private static FieldInfo _consumptionPermitsField (line 27)
- private static PropertyInfo _rawFoodPermitsProp (line 28)
- private static MethodInfo _isRawFoodPermittedMethod (line 29)
- private static MethodInfo _setRawFoodPermissionMethod (line 30)
- private static MethodInfo _setAllRawFoodPermissionsMethod (line 31)
- private static MethodInfo _isNeedPermittedForRaceMethod (line 32)
- private static MethodInfo _setNeedPermissionForRaceMethod (line 33)
- private static PropertyInfo _racesServiceRevealedRacesProp (line 34)
- private static PropertyInfo _revealedRaceModelProp (line 35)
- private static PropertyInfo _raceModelDisplayNameProp (line 36)
- private static PropertyInfo _racesServiceProp (line 37)
- private static MethodInfo _getResolveImpactMethod (line 38)
- private static FieldInfo _blockingEffectsField (line 39)
- private static PropertyInfo _goodModelDisplayNameProp (line 40)
- private static bool _typesCached (line 42)

### Methods
- private static void EnsureTypes() (line 44)
- private static object GetConsumptionService() (line 227)
- public static bool IsConsumptionPopup(object popup) (line 242)
- public static bool IsBlocked() (line 252)
  Returns true if any blocking effects are active that prevent consumption changes.
- public static List<object> GetCategories() (line 263)
  Returns NeedCategoryModel objects, excluding house-based categories.
- public static string GetCategoryName(object category) (line 289)
- public static List<string> GetRawFoods() (line 299)
  Returns raw food good IDs from rawFoodConsumptionPermits.
- public static string GetRawFoodName(string id) (line 330)
- public static bool IsRawFoodPermitted(string id) (line 356)
- public static void SetRawFoodPermission(string id, bool isOn) (line 367)
- public static void SetAllRawFoodPermission(bool isOn) (line 377)
- public static bool IsAllRawFoodPermitted() (line 387)
- public static bool IsAllRawFoodProhibited() (line 398)
- public static List<object> GetNeedsForCategory(object category) (line 409)
  Returns prohibitable NeedModel objects for the given category.
- public static string GetNeedName(object need) (line 433)
- public static bool IsNeedBlanketPermitted(object need) (line 443)
  Returns true if ALL revealed races permit this need.
- public static bool IsNeedBlanketProhibited(object need) (line 458)
  Returns true if ALL revealed races prohibit this need.
- public static void SetNeedBlanketPermission(object need, bool isOn) (line 474)
  Sets permission for all revealed races at once.
- public static List<object> GetAllRevealedRaces() (line 485)
- public static List<object> GetRacesForNeed(object need) (line 516)
  Returns revealed races that have this need.
- public static string GetRaceName(object race) (line 553)
- public static bool IsNeedPermittedForRace(object race, object need) (line 562)
- public static void SetNeedPermissionForRace(object race, object need, bool isOn) (line 573)
- public static (int current, int max) GetResolveImpact(object race, object need) (line 584)
- public static void SetAllNeedsPermissionForCategory(object category, bool isOn) (line 608)
- public static string GetCategoryStatus(object category, bool isRawFood) (line 619)
  Returns `"all permitted"`, `"all prohibited"`, or `"mixed"`.
- public static string GetNeedStatus(object need) (line 656)
  Returns `"all permitted"`, `"all prohibited"`, or `"mixed"`.
- public static string GetBlockingEffectsList() (line 666)
  Returns comma-separated list of blocking effect names, or null if none.
- public static string GetRaceNeedsStatus(object race) (line 693)
  Returns `"all permitted"`, `"all prohibited"`, or `"mixed"` for all needs of a race.
- public static void SetAllNeedsPermissionForRace(object race, bool isOn) (line 724)
- private static List<object> GetAllNeeds() (line 746)
- public static int LogCacheStatus() (line 752)
