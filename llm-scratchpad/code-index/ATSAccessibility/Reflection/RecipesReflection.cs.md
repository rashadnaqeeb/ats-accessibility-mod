# RecipesReflection.cs
Reflection helpers for the recipe popup. Provides methods to query recipes organized by produced good, manage global production limits, query building/recipe state, and toggle recipe active state.

## class RecipesReflection (line 7)

### Nested Classes
- public class `RecipeInfo` (line 335): `BuildingName`, `BuildingDisplayName`, `RecipeModel`, `IsActive`, `IsBuilt`, `ShowIndex`, `IsUnlocked`
- public class `GoodInfo` (line 349): `GoodName`, `GoodModel`, `DisplayName`, `Recipes` (List<RecipeInfo>)

### Fields
**Service property cache**
- private static PropertyInfo `_gsProductionServiceProperty` (line ~30)
- private static PropertyInfo `_gsStorageServiceProperty` (line ~31)
- private static PropertyInfo `_gsEffectsServiceProperty` (line ~32)
- private static PropertyInfo `_gsMetaServicesProperty` (line ~33)

**Recipe service methods**
- private static MethodInfo `_getRecipesForBuildingMethod` (line ~39)
- private static MethodInfo `_isBuildingUnlockedMethod` (line ~40)
- private static MethodInfo `_getShowIndexMethod` (line ~41)
- private static MethodInfo `_toggleRecipeMethod` (line ~42)
- private static MethodInfo `_getGlobalLimitMethod` (line ~43)
- private static MethodInfo `_setGlobalLimitMethod` (line ~44)
- private static MethodInfo `_getStorageAmountMethod` (line ~45)

**Recipe model fields**
- private static FieldInfo `_recipeNameField` (line ~51)
- private static FieldInfo `_recipeIsActiveField` (line ~52)
- private static FieldInfo `_recipeProducedGoodField` (line ~53)
- private static FieldInfo `_recipeProductionTimeField` (line ~54)
- private static FieldInfo `_recipeRequiredGoodsField` (line ~55)
- private static FieldInfo `_recipeGradeField` (line ~56)

**Good / building model fields**
- private static FieldInfo `_goodDisplayNameField` (line ~62)
- private static MethodInfo `_getGoodMethod` (line ~63)
- private static FieldInfo `_buildingDisplayNameField` (line ~64)
- private static MethodInfo `_getBuildingMethod` (line ~65)
- private static FieldInfo `_goodRefGoodField` (line ~66)
- private static FieldInfo `_goodRefAmountField` (line ~67)
- private static FieldInfo `_goodsSetGoodsField` (line ~68)

**Building state cache**
- private static PropertyInfo `_buildingStateProperty` (line ~74)
- private static PropertyInfo `_buildingModelProperty` (line ~75)
- private static PropertyInfo `_buildingsListProperty` (line ~76)
- private static PropertyInfo `_buildingStateModelProperty` (line ~77)
- private static MethodInfo `_getBuiltBuildingsOfTypeMethod` (line ~78)
- private static bool `_typesCached` (line ~81)

### Methods
- private static void `EnsureTypesCached()` (line 91)
- private static void `CacheServiceProperties(Assembly)` (line ~100)
- private static void `CacheRecipeTypes(Assembly)` (line ~110)
- private static void `CacheGoodTypes(Assembly)` (line ~120)
- private static void `CacheBuildingTypes(Assembly)` (line ~130)
- private static object `GetProductionService()` (line 275)
- private static object `GetStorageService()` (line 279)
- private static object `GetEffectsService()` (line 283)
- private static object `GetMetaServices()` (line 287)
- public static int `GetGlobalLimit(string goodName)` (line 302)
- public static bool `SetGlobalLimit(string goodName, int limit)` (line 311)
- public static int `GetStorageAmount(string goodName)` (line 323)
- public static List<GoodInfo> `GetAllGoods(bool includeUnbuilt)` (line 366)
  Returns all goods that have at least one recipe, optionally including unbuilt buildings.
- private static void `AddBuiltWorkshops(List<GoodInfo>, object productionService, object settings)` (line 385)
- private static void `AddWorkshopsFromDict(List<GoodInfo>, object buildingsDict, object settings, object productionService, bool isBuilt)` (line 398)
- private static void `AddUnbuiltWorkshops(List<GoodInfo>, object settings)` (line 471)
- private static void `AddUnbuiltWorkshop(List<GoodInfo>, string buildingName, object settings)` (line 496)
- public static List<RecipeInfo> `GetRecipesForBuilding(string buildingName, object productionService)` (line 556)
- public static bool `IsBuildingUnlocked(object productionService, object buildingModel)` (line 560)
- public static int `GetShowIndex(object productionService)` (line 581)
  Returns the "show index" used to determine if recipes should be shown vs collapsed.
- public static bool `ToggleRecipe(RecipeInfo recipe)` (line 594)
- public static string `GetGoodDisplayName(object goodModel)` (line 615)
- public static string `GetBuildingDisplayName(object buildingModel)` (line 628)
- public static object `GetWorkshopRecipeModel(string recipeName)` (line 638)
- public static int `GetRecipeOutputAmount(object recipeModel)` (line 650)
- public static string `GetRecipeOutputName(object recipeModel)` (line 662)
- public static float `GetRecipeProductionTime(object recipeModel)` (line 675)
- public static int `GetRecipeGradeLevel(object recipeModel)` (line 682)
- public static Array `GetRecipeRequiredGoods(object recipeModel)` (line 693)
- public static Array `GetGoodsSetGoods(object goodsSet)` (line 700)
- public static string `GetGoodRefDisplayName(object goodRef)` (line 707)
- public static int `GetGoodRefAmount(object goodRef)` (line 717)
- public static bool `IsRecipesPopup(object popup)` (line 728)
- public static int `LogCacheStatus()` (line 733)
