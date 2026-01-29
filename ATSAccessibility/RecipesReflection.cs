using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing recipe popup data.
    /// Provides methods to query recipes organized by produced good,
    /// manage global production limits, and toggle recipe states.
    /// </summary>
    public static class RecipesReflection
    {
        // ========================================
        // CACHED TYPE INFO
        // ========================================

        // WorkshopsService
        private static PropertyInfo _gsWorkshopsServiceProperty = null;
        private static MethodInfo _getGlobalLimitForMethod = null;
        private static MethodInfo _setGlobalLimitForMethod = null;

        // StorageService
        private static PropertyInfo _gsStorageServiceProperty = null;
        private static MethodInfo _getAmountMethod = null;

        // BuildingsService
        private static PropertyInfo _gsBuildingsServiceProperty = null;
        private static PropertyInfo _workshopsDictProperty = null;
        private static PropertyInfo _blightPostsDictProperty = null;

        // Settings access
        private static PropertyInfo _mbSettingsProperty = null;
        private static FieldInfo _settingsWorkshopsField = null;
        private static FieldInfo _settingsBlightPostsField = null;
        private static FieldInfo _settingsGoodsField = null;
        private static MethodInfo _getGoodMethod = null;
        private static MethodInfo _getWorkshopRecipeMethod = null;

        // GameContentService (for unlock checking)
        private static PropertyInfo _gsGameContentServiceProperty = null;
        private static MethodInfo _isUnlockedMethod = null;

        // RecipesService
        private static PropertyInfo _gsRecipesServiceProperty = null;
        private static MethodInfo _getRecipesForMethod = null;

        // IWorkshop interface
        private static PropertyInfo _workshopRecipesProperty = null;
        private static PropertyInfo _workshopBaseModelProperty = null;
        private static PropertyInfo _workshopBaseProperty = null;
        private static MethodInfo _switchProductionOfMethod = null;

        // WorkshopRecipeState
        private static FieldInfo _recipeStateModelField = null;
        private static FieldInfo _recipeStateActiveField = null;

        // WorkshopRecipeModel
        private static FieldInfo _recipeProducedGoodField = null;
        private static FieldInfo _recipeRequiredGoodsField = null;
        private static FieldInfo _recipeProductionTimeField = null;
        private static FieldInfo _recipeGradeField = null;

        // GoodRef
        private static FieldInfo _goodRefGoodField = null;
        private static FieldInfo _goodRefAmountField = null;

        // GoodModel
        private static FieldInfo _goodDisplayNameField = null;
        private static PropertyInfo _goodNameProperty = null;
        private static FieldInfo _goodCategoryField = null;

        // GoodsSet
        private static FieldInfo _goodsSetGoodsField = null;

        // BuildingModel
        private static FieldInfo _buildingDisplayNameField = null;
        private static PropertyInfo _buildingNameProperty = null;
        private static MethodInfo _hasAccessToMethod = null;

        // RecipeGradeModel
        private static FieldInfo _gradeModelLevelField = null;

        // ConstructionService (for show index)
        private static PropertyInfo _gsConstructionServiceProperty = null;
        private static MethodInfo _getShowIndexMethod = null;

        private static bool _typesCached = false;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureTypesCached()
        {
            if (_typesCached) return;
            _typesCached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] RecipesReflection: Game assembly not available");
                    return;
                }

                CacheServiceProperties(assembly);
                CacheRecipeTypes(assembly);
                CacheGoodTypes(assembly);
                CacheBuildingTypes(assembly);

                Debug.Log("[ATSAccessibility] RecipesReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RecipesReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheServiceProperties(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsWorkshopsServiceProperty = gameServicesType.GetProperty("WorkshopsService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsStorageServiceProperty = gameServicesType.GetProperty("StorageService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsBuildingsServiceProperty = gameServicesType.GetProperty("BuildingsService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsGameContentServiceProperty = gameServicesType.GetProperty("GameContentService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsRecipesServiceProperty = gameServicesType.GetProperty("RecipesService",
                    BindingFlags.Public | BindingFlags.Instance);
                _gsConstructionServiceProperty = gameServicesType.GetProperty("ConstructionService",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // WorkshopsService methods
            var workshopsServiceType = assembly.GetType("Eremite.Services.IWorkshopsService");
            if (workshopsServiceType != null)
            {
                _getGlobalLimitForMethod = workshopsServiceType.GetMethod("GetGlobalLimitFor",
                    new[] { typeof(string) });
                _setGlobalLimitForMethod = workshopsServiceType.GetMethod("SetGlobalLimitFor",
                    new[] { typeof(string), typeof(int) });
            }

            // StorageService - GetAmount is on IStorageService directly
            var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
            if (storageServiceType != null)
            {
                _getAmountMethod = storageServiceType.GetMethod("GetAmount",
                    new[] { typeof(string) });
            }

            // BuildingsService dictionaries
            var buildingsServiceType = assembly.GetType("Eremite.Services.IBuildingsService");
            if (buildingsServiceType != null)
            {
                _workshopsDictProperty = buildingsServiceType.GetProperty("Workshops",
                    BindingFlags.Public | BindingFlags.Instance);
                _blightPostsDictProperty = buildingsServiceType.GetProperty("BlightPosts",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // RecipesService
            var recipesServiceType = assembly.GetType("Eremite.Services.IRecipesService");
            if (recipesServiceType != null)
            {
                _getRecipesForMethod = recipesServiceType.GetMethod("GetRecipesFor",
                    new[] { typeof(string) });
            }

            // GameContentService
            var gameContentServiceType = assembly.GetType("Eremite.Services.IGameContentService");
            if (gameContentServiceType != null)
            {
                _isUnlockedMethod = gameContentServiceType.GetMethod("IsUnlocked",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // ConstructionService
            var constructionServiceType = assembly.GetType("Eremite.Services.IConstructionService");
            if (constructionServiceType != null)
            {
                _getShowIndexMethod = constructionServiceType.GetMethod("GetShowIndex",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // Settings access via MB static class (Settings is protected static)
            var mbType = assembly.GetType("Eremite.MB");
            if (mbType != null)
            {
                _mbSettingsProperty = mbType.GetProperty("Settings",
                    BindingFlags.NonPublic | BindingFlags.Static);
            }

            // Settings fields
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsWorkshopsField = settingsType.GetField("workshops",
                    BindingFlags.Public | BindingFlags.Instance);
                _settingsBlightPostsField = settingsType.GetField("blightPosts",
                    BindingFlags.Public | BindingFlags.Instance);
                _settingsGoodsField = settingsType.GetField("goods",
                    BindingFlags.Public | BindingFlags.Instance);
                _getGoodMethod = settingsType.GetMethod("GetGood",
                    new[] { typeof(string) });
                _getWorkshopRecipeMethod = settingsType.GetMethod("GetWorkshopRecipe",
                    new[] { typeof(string) });
            }
        }

        private static void CacheRecipeTypes(Assembly assembly)
        {
            // IWorkshop interface
            var workshopInterfaceType = assembly.GetType("Eremite.Buildings.IWorkshop");
            if (workshopInterfaceType != null)
            {
                _workshopRecipesProperty = workshopInterfaceType.GetProperty("Recipes",
                    BindingFlags.Public | BindingFlags.Instance);
                _workshopBaseModelProperty = workshopInterfaceType.GetProperty("BaseModel",
                    BindingFlags.Public | BindingFlags.Instance);
                _workshopBaseProperty = workshopInterfaceType.GetProperty("Base",
                    BindingFlags.Public | BindingFlags.Instance);
                _switchProductionOfMethod = workshopInterfaceType.GetMethod("SwitchProductionOf",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // WorkshopRecipeState
            var recipeStateType = assembly.GetType("Eremite.Buildings.WorkshopRecipeState");
            if (recipeStateType != null)
            {
                _recipeStateModelField = recipeStateType.GetField("model",
                    BindingFlags.Public | BindingFlags.Instance);
                _recipeStateActiveField = recipeStateType.GetField("active",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // WorkshopRecipeModel
            var recipeModelType = assembly.GetType("Eremite.Buildings.WorkshopRecipeModel");
            if (recipeModelType != null)
            {
                _recipeProducedGoodField = recipeModelType.GetField("producedGood",
                    BindingFlags.Public | BindingFlags.Instance);
                _recipeRequiredGoodsField = recipeModelType.GetField("requiredGoods",
                    BindingFlags.Public | BindingFlags.Instance);
                _recipeProductionTimeField = recipeModelType.GetField("productionTime",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // RecipeModel (base class of WorkshopRecipeModel) - grade field is inherited
            var baseRecipeModelType = assembly.GetType("Eremite.Buildings.RecipeModel");
            if (baseRecipeModelType != null)
            {
                _recipeGradeField = baseRecipeModelType.GetField("grade",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // RecipeGradeModel
            var gradeModelType = assembly.GetType("Eremite.Buildings.RecipeGradeModel");
            if (gradeModelType != null)
            {
                _gradeModelLevelField = gradeModelType.GetField("level",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheGoodTypes(Assembly assembly)
        {
            // GoodRef
            var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
            if (goodRefType != null)
            {
                _goodRefGoodField = goodRefType.GetField("good",
                    BindingFlags.Public | BindingFlags.Instance);
                _goodRefAmountField = goodRefType.GetField("amount",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // GoodModel
            var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
            if (goodModelType != null)
            {
                _goodDisplayNameField = goodModelType.GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                _goodNameProperty = goodModelType.GetProperty("Name",
                    BindingFlags.Public | BindingFlags.Instance);
                _goodCategoryField = goodModelType.GetField("category",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            // GoodsSet
            var goodsSetType = assembly.GetType("Eremite.Model.GoodsSet");
            if (goodsSetType != null)
            {
                _goodsSetGoodsField = goodsSetType.GetField("goods",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        private static void CacheBuildingTypes(Assembly assembly)
        {
            // BuildingModel
            var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
            if (buildingModelType != null)
            {
                _buildingDisplayNameField = buildingModelType.GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                _buildingNameProperty = buildingModelType.GetProperty("Name",
                    BindingFlags.Public | BindingFlags.Instance);
                _hasAccessToMethod = buildingModelType.GetMethod("HasAccessTo",
                    BindingFlags.Public | BindingFlags.Instance);
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetGameService(PropertyInfo serviceProperty)
        {
            EnsureTypesCached();
            return GameReflection.GetService(serviceProperty);
        }

        private static object GetWorkshopsService() => GetGameService(_gsWorkshopsServiceProperty);
        private static object GetStorageService() => GetGameService(_gsStorageServiceProperty);
        private static object GetBuildingsService() => GetGameService(_gsBuildingsServiceProperty);
        private static object GetGameContentService() => GetGameService(_gsGameContentServiceProperty);
        private static object GetRecipesService() => GetGameService(_gsRecipesServiceProperty);
        private static object GetConstructionService() => GetGameService(_gsConstructionServiceProperty);

        private static object GetSettings()
        {
            EnsureTypesCached();
            if (_mbSettingsProperty == null) return null;
            return _mbSettingsProperty.GetValue(null);
        }


        // ========================================
        // GLOBAL LIMIT ACCESS
        // ========================================

        /// <summary>
        /// Get the global production limit for a good (by internal name).
        /// Returns 0 if no limit is set.
        /// </summary>
        public static int GetGlobalLimit(string goodName)
        {
            var workshopsService = GetWorkshopsService();
            if (workshopsService == null || _getGlobalLimitForMethod == null) return 0;

            try
            {
                var result = _getGlobalLimitForMethod.Invoke(workshopsService, new object[] { goodName });
                return result is int limit ? limit : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RecipesReflection: GetGlobalLimit failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Set the global production limit for a good.
        /// Set to 0 to remove the limit.
        /// </summary>
        public static bool SetGlobalLimit(string goodName, int limit)
        {
            var workshopsService = GetWorkshopsService();
            if (workshopsService == null || _setGlobalLimitForMethod == null) return false;

            try
            {
                _setGlobalLimitForMethod.Invoke(workshopsService, new object[] { goodName, limit });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RecipesReflection: SetGlobalLimit failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // STORAGE ACCESS
        // ========================================

        /// <summary>
        /// Get the amount of a good in the main storage.
        /// </summary>
        public static int GetStorageAmount(string goodName)
        {
            var storageService = GetStorageService();
            if (storageService == null || _getAmountMethod == null) return 0;

            try
            {
                var result = _getAmountMethod.Invoke(storageService, new object[] { goodName });
                return result is int amount ? amount : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RecipesReflection: GetStorageAmount failed: {ex.Message}");
                return 0;
            }
        }

        // ========================================
        // RECIPE DATA ACCESS
        // ========================================

        /// <summary>
        /// Data structure for a recipe in the overlay.
        /// </summary>
        public class RecipeInfo
        {
            public object Workshop;        // IWorkshop instance (null if not built)
            public object WorkshopModel;   // BuildingModel
            public object RecipeState;     // WorkshopRecipeState (null if not built)
            public object RecipeModel;     // WorkshopRecipeModel
            public string WorkshopName;    // Display name of workshop
            public int WorkshopIndex;      // Show index (e.g., #1, #2)
            public bool IsActive;          // Recipe enabled/disabled
            public bool IsBuilt;           // Workshop is built vs just unlocked
        }

        /// <summary>
        /// Data structure for a good with its recipes.
        /// </summary>
        public class GoodInfo
        {
            public string Name;            // Internal name
            public string DisplayName;     // Localized display name
            public int StorageAmount;      // Amount in storage
            public int Limit;              // Global production limit (0 = no limit)
            public List<RecipeInfo> Recipes = new List<RecipeInfo>();
        }

        /// <summary>
        /// Get all goods that can be produced, organized by good name.
        /// </summary>
        /// <param name="showAll">If true, include all unlocked buildings; if false, only built buildings</param>
        /// <summary>
        /// Get all goods that can be produced, organized by good name.
        /// Default: shows built workshops + unlocked-but-unbuilt workshops.
        /// showAll=true: also includes locked (not yet unlocked) buildings.
        /// </summary>
        public static List<GoodInfo> GetAllGoods(bool showAll)
        {
            EnsureTypesCached();

            var result = new Dictionary<string, GoodInfo>();
            var constructedBuildings = new HashSet<string>();

            // First, gather recipes from built workshops
            AddBuiltWorkshops(result, constructedBuildings);

            // Then, add unbuilt workshops (always - default shows unlocked, showAll skips unlock check)
            AddUnbuiltWorkshops(result, constructedBuildings, skipUnlockCheck: showAll);

            // Convert to list and sort by display name
            var goodsList = new List<GoodInfo>(result.Values);
            goodsList.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            return goodsList;
        }

        private static void AddBuiltWorkshops(Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings)
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return;

            var workshopsDict = _workshopsDictProperty?.GetValue(buildingsService);
            if (workshopsDict != null)
                AddWorkshopsFromDict(workshopsDict, result, constructedBuildings);

            var blightPostsDict = _blightPostsDictProperty?.GetValue(buildingsService);
            if (blightPostsDict != null)
                AddWorkshopsFromDict(blightPostsDict, result, constructedBuildings);
        }

        private static void AddWorkshopsFromDict(object dict, Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings)
        {
            // Use reflection to iterate the dictionary
            var valuesProperty = dict.GetType().GetProperty("Values");
            if (valuesProperty == null) return;

            var values = valuesProperty.GetValue(dict) as IEnumerable;
            if (values == null) return;

            foreach (var workshop in values)
            {
                if (workshop == null) continue;

                // Mark this building model as constructed
                var buildingModel = _workshopBaseModelProperty?.GetValue(workshop);
                if (buildingModel != null)
                {
                    var modelName = _buildingNameProperty?.GetValue(buildingModel) as string;
                    if (!string.IsNullOrEmpty(modelName))
                    {
                        constructedBuildings.Add(modelName);
                    }
                }

                // Get recipes from this workshop
                var recipes = _workshopRecipesProperty?.GetValue(workshop) as IEnumerable;
                if (recipes == null) continue;

                // Get workshop display name and index
                var workshopDisplayName = GetBuildingDisplayName(buildingModel);
                var workshopBase = _workshopBaseProperty?.GetValue(workshop);
                var showIndex = GetShowIndex(workshopBase);

                foreach (var recipeState in recipes)
                {
                    if (recipeState == null) continue;

                    var modelName = _recipeStateModelField?.GetValue(recipeState) as string;
                    if (string.IsNullOrEmpty(modelName)) continue;

                    var recipeModel = GetWorkshopRecipeModel(modelName);
                    if (recipeModel == null) continue;

                    var producedGoodRef = _recipeProducedGoodField?.GetValue(recipeModel);
                    if (producedGoodRef == null) continue;

                    var goodModel = _goodRefGoodField?.GetValue(producedGoodRef);
                    if (goodModel == null) continue;

                    var goodName = _goodNameProperty?.GetValue(goodModel) as string;
                    if (string.IsNullOrEmpty(goodName)) continue;

                    // Get or create GoodInfo
                    if (!result.TryGetValue(goodName, out var goodInfo))
                    {
                        goodInfo = new GoodInfo
                        {
                            Name = goodName,
                            DisplayName = GetGoodDisplayName(goodModel),
                            StorageAmount = GetStorageAmount(goodName),
                            Limit = GetGlobalLimit(goodName)
                        };
                        result[goodName] = goodInfo;
                    }

                    // Add recipe info
                    var isActive = _recipeStateActiveField?.GetValue(recipeState);
                    goodInfo.Recipes.Add(new RecipeInfo
                    {
                        Workshop = workshop,
                        WorkshopModel = buildingModel,
                        RecipeState = recipeState,
                        RecipeModel = recipeModel,
                        WorkshopName = workshopDisplayName,
                        WorkshopIndex = showIndex,
                        IsActive = isActive is bool active && active,
                        IsBuilt = true
                    });
                }
            }
        }

        private static void AddUnbuiltWorkshops(Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings, bool skipUnlockCheck)
        {
            var settings = GetSettings();
            if (settings == null) return;

            var recipesService = GetRecipesService();
            var gameContentService = GetGameContentService();

            // Get workshops array
            if (_settingsWorkshopsField != null)
            {
                var workshops = _settingsWorkshopsField.GetValue(settings) as Array;
                if (workshops != null)
                {
                    foreach (var workshopModel in workshops)
                    {
                        AddUnbuiltWorkshop(workshopModel, result, constructedBuildings, recipesService, gameContentService, skipUnlockCheck);
                    }
                }
            }

            // Get blight posts array
            if (_settingsBlightPostsField != null)
            {
                var blightPosts = _settingsBlightPostsField.GetValue(settings) as Array;
                if (blightPosts != null)
                {
                    foreach (var workshopModel in blightPosts)
                    {
                        AddUnbuiltWorkshop(workshopModel, result, constructedBuildings, recipesService, gameContentService, skipUnlockCheck);
                    }
                }
            }
        }


        private static void AddUnbuiltWorkshop(object workshopModel, Dictionary<string, GoodInfo> result,
            HashSet<string> constructedBuildings, object recipesService, object gameContentService, bool skipUnlockCheck)
        {
            if (workshopModel == null) return;

            var modelName = _buildingNameProperty?.GetValue(workshopModel) as string;
            if (string.IsNullOrEmpty(modelName)) return;

            // Skip if already constructed
            if (constructedBuildings.Contains(modelName)) return;

            // Check if unlocked (skip check in "show all" mode)
            if (!skipUnlockCheck && !IsBuildingUnlocked(workshopModel, gameContentService)) return;

            // Check if has access to
            if (_hasAccessToMethod != null)
            {
                var hasAccess = _hasAccessToMethod.Invoke(workshopModel, null);
                if (hasAccess is bool access && !access) return;
            }

            // Get recipes for this building
            var recipeNames = GetRecipesForBuilding(modelName, recipesService);
            if (recipeNames == null || recipeNames.Count == 0) return;

            var workshopDisplayName = GetBuildingDisplayName(workshopModel);

            foreach (var recipeName in recipeNames)
            {
                var recipeModel = GetWorkshopRecipeModel(recipeName);
                if (recipeModel == null) continue;

                var producedGoodRef = _recipeProducedGoodField?.GetValue(recipeModel);
                if (producedGoodRef == null) continue;

                var goodModel = _goodRefGoodField?.GetValue(producedGoodRef);
                if (goodModel == null) continue;

                var goodName = _goodNameProperty?.GetValue(goodModel) as string;
                if (string.IsNullOrEmpty(goodName)) continue;

                // Get or create GoodInfo
                if (!result.TryGetValue(goodName, out var goodInfo))
                {
                    goodInfo = new GoodInfo
                    {
                        Name = goodName,
                        DisplayName = GetGoodDisplayName(goodModel),
                        StorageAmount = GetStorageAmount(goodName),
                        Limit = GetGlobalLimit(goodName)
                    };
                    result[goodName] = goodInfo;
                }

                // Add recipe info (unbuilt)
                goodInfo.Recipes.Add(new RecipeInfo
                {
                    Workshop = null,
                    WorkshopModel = workshopModel,
                    RecipeState = null,
                    RecipeModel = recipeModel,
                    WorkshopName = workshopDisplayName,
                    WorkshopIndex = 0,
                    IsActive = false,
                    IsBuilt = false
                });
            }
        }

        private static List<string> GetRecipesForBuilding(string buildingName, object recipesService)
        {
            if (recipesService == null || _getRecipesForMethod == null) return null;

            try
            {
                var result = _getRecipesForMethod.Invoke(recipesService, new object[] { buildingName });
                return result as List<string>;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsBuildingUnlocked(object buildingModel, object gameContentService)
        {
            if (gameContentService == null || _isUnlockedMethod == null) return true;

            try
            {
                // Try to find the overload that takes BuildingModel
                var methods = gameContentService.GetType().GetMethods()
                    .Where(m => m.Name == "IsUnlocked");

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(buildingModel.GetType()))
                    {
                        var result = method.Invoke(gameContentService, new object[] { buildingModel });
                        return result is bool unlocked && unlocked;
                    }
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        private static int GetShowIndex(object building)
        {
            var constructionService = GetConstructionService();
            if (constructionService == null || building == null || _getShowIndexMethod == null) return 0;

            try
            {
                var result = _getShowIndexMethod.Invoke(constructionService, new object[] { building });
                return result is int index ? index : 0;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // RECIPE TOGGLING
        // ========================================

        /// <summary>
        /// Toggle a recipe's active state.
        /// Returns the new active state.
        /// </summary>
        public static bool ToggleRecipe(RecipeInfo recipe)
        {
            if (recipe == null || recipe.Workshop == null || recipe.RecipeState == null)
            {
                Debug.LogWarning("[ATSAccessibility] RecipesReflection: Cannot toggle unbuilt recipe");
                return false;
            }

            try
            {
                // Call workshop.SwitchProductionOf(recipeState)
                if (_switchProductionOfMethod != null)
                {
                    _switchProductionOfMethod.Invoke(recipe.Workshop, new object[] { recipe.RecipeState });
                }

                // Get the new active state
                var isActive = _recipeStateActiveField?.GetValue(recipe.RecipeState);
                recipe.IsActive = isActive is bool active && active;
                return recipe.IsActive;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RecipesReflection: ToggleRecipe failed: {ex.Message}");
                return recipe.IsActive;
            }
        }

        // ========================================
        // NAME HELPERS
        // ========================================

        /// <summary>
        /// Get the display name of a good model.
        /// </summary>
        public static string GetGoodDisplayName(object goodModel)
        {
            if (goodModel == null) return "Unknown";

            try
            {
                var locaText = _goodDisplayNameField?.GetValue(goodModel);
                if (locaText != null)
                {
                    var text = GameReflection.GetLocaText(locaText);
                    if (!string.IsNullOrEmpty(text)) return text;
                }

                // Fallback to Name property
                return _goodNameProperty?.GetValue(goodModel) as string ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the display name of a building model.
        /// </summary>
        public static string GetBuildingDisplayName(object buildingModel)
        {
            if (buildingModel == null) return "Unknown";

            try
            {
                var locaText = _buildingDisplayNameField?.GetValue(buildingModel);
                if (locaText != null)
                {
                    var text = GameReflection.GetLocaText(locaText);
                    if (!string.IsNullOrEmpty(text)) return text;
                }

                // Fallback to Name property
                return _buildingNameProperty?.GetValue(buildingModel) as string ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private static object GetWorkshopRecipeModel(string recipeName)
        {
            var settings = GetSettings();
            if (settings == null || _getWorkshopRecipeMethod == null) return null;

            try
            {
                return _getWorkshopRecipeMethod.Invoke(settings, new object[] { recipeName });
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // RECIPE INFO HELPERS
        // ========================================

        /// <summary>
        /// Get the output amount from a recipe model.
        /// </summary>
        public static int GetRecipeOutputAmount(object recipeModel)
        {
            if (recipeModel == null) return 0;

            try
            {
                var producedGoodRef = _recipeProducedGoodField?.GetValue(recipeModel);
                if (producedGoodRef == null) return 0;

                var amount = _goodRefAmountField?.GetValue(producedGoodRef);
                return amount is int a ? a : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the output good name from a recipe model.
        /// </summary>
        public static string GetRecipeOutputName(object recipeModel)
        {
            if (recipeModel == null) return "Unknown";

            try
            {
                var producedGoodRef = _recipeProducedGoodField?.GetValue(recipeModel);
                if (producedGoodRef == null) return "Unknown";

                var goodModel = _goodRefGoodField?.GetValue(producedGoodRef);
                return GetGoodDisplayName(goodModel);
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the production time from a recipe model.
        /// </summary>
        public static float GetRecipeProductionTime(object recipeModel)
        {
            if (recipeModel == null || _recipeProductionTimeField == null) return 0f;

            try
            {
                var time = _recipeProductionTimeField.GetValue(recipeModel);
                return time is float f ? f : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the grade level (stars) from a recipe model.
        /// </summary>
        public static int GetRecipeGradeLevel(object recipeModel)
        {
            if (recipeModel == null || _recipeGradeField == null) return 0;

            try
            {
                var grade = _recipeGradeField.GetValue(recipeModel);
                if (grade == null) return 0;

                var level = _gradeModelLevelField?.GetValue(grade);
                return level is int l ? l : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the required goods (ingredients) from a recipe model.
        /// Returns array of GoodsSet objects.
        /// </summary>
        public static Array GetRecipeRequiredGoods(object recipeModel)
        {
            if (recipeModel == null || _recipeRequiredGoodsField == null) return null;

            try
            {
                return _recipeRequiredGoodsField.GetValue(recipeModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the goods array from a GoodsSet.
        /// </summary>
        public static Array GetGoodsSetGoods(object goodsSet)
        {
            if (goodsSet == null || _goodsSetGoodsField == null) return null;

            try
            {
                return _goodsSetGoodsField.GetValue(goodsSet) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a GoodRef.
        /// </summary>
        public static string GetGoodRefDisplayName(object goodRef)
        {
            if (goodRef == null) return "Unknown";

            try
            {
                var goodModel = _goodRefGoodField?.GetValue(goodRef);
                return GetGoodDisplayName(goodModel);
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the amount from a GoodRef.
        /// </summary>
        public static int GetGoodRefAmount(object goodRef)
        {
            if (goodRef == null || _goodRefAmountField == null) return 0;

            try
            {
                var amount = _goodRefAmountField.GetValue(goodRef);
                return amount is int a ? a : 0;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a RecipesPopup.
        /// </summary>
        public static bool IsRecipesPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "RecipesPopup";
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(RecipesReflection), "RecipesReflection");
        }
    }
}
