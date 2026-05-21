using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing recipe popup data.
	/// Provides methods to query recipes organized by produced good,
	/// manage global production limits, and toggle recipe states.
	/// </summary>
	public static class RecipesReflection {
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

		// StoragePrefsService
		private static PropertyInfo _gsStoragePrefsServiceProperty = null;
		private static MethodInfo _getLimitMethod = null;
		private static MethodInfo _setLimitMethod = null;

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

		// BiomeModel (for producibility check)
		private static MethodInfo _biomeGetDepositsGoodsMethod = null;
		private static MethodInfo _biomeGetTreesGoodsMethod = null;

		// Popup type detection
		private static Type _recipesPopupType;

		private static bool _typesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypesCached() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("RecipesReflection", assembly => {
				CacheServiceProperties(assembly);
				CacheRecipeTypes(assembly);
				CacheGoodTypes(assembly);
				CacheBuildingTypes(assembly);
				CacheBiomeTypes(assembly);
				_recipesPopupType = assembly.GetType("Eremite.View.Popups.Recipes.RecipesPopup");
			});
		}

		private static void CacheServiceProperties(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
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
			if (workshopsServiceType != null) {
				_getGlobalLimitForMethod = workshopsServiceType.GetMethod("GetGlobalLimitFor",
					new[] { typeof(string) });
				_setGlobalLimitForMethod = workshopsServiceType.GetMethod("SetGlobalLimitFor",
					new[] { typeof(string), typeof(int) });
			}

			// StorageService - GetAmount is on IStorageService directly
			var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
			if (storageServiceType != null) {
				_getAmountMethod = storageServiceType.GetMethod("GetAmount",
					new[] { typeof(string) });
			}

			// StoragePrefsService
			_gsStoragePrefsServiceProperty = gameServicesType?.GetProperty("StoragePrefsService",
				BindingFlags.Public | BindingFlags.Instance);
			var storagePrefsServiceType = assembly.GetType("Eremite.Services.IStoragePrefsService");
			if (storagePrefsServiceType != null) {
				_getLimitMethod = storagePrefsServiceType.GetMethod("GetLimit",
					new[] { typeof(string) });
				_setLimitMethod = storagePrefsServiceType.GetMethod("SetLimit",
					new[] { typeof(string), typeof(int) });
			}

			// BuildingsService dictionaries
			var buildingsServiceType = assembly.GetType("Eremite.Services.IBuildingsService");
			if (buildingsServiceType != null) {
				_workshopsDictProperty = buildingsServiceType.GetProperty("Workshops",
					BindingFlags.Public | BindingFlags.Instance);
				_blightPostsDictProperty = buildingsServiceType.GetProperty("BlightPosts",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// RecipesService
			var recipesServiceType = assembly.GetType("Eremite.Services.IRecipesService");
			if (recipesServiceType != null) {
				_getRecipesForMethod = recipesServiceType.GetMethod("GetRecipesFor",
					new[] { typeof(string) });
			}

			// GameContentService
			var gameContentServiceType = assembly.GetType("Eremite.Services.IGameContentService");
			if (gameContentServiceType != null) {
				_isUnlockedMethod = gameContentServiceType.GetMethod("IsUnlocked",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// ConstructionService
			var constructionServiceType = assembly.GetType("Eremite.Services.IConstructionService");
			if (constructionServiceType != null) {
				_getShowIndexMethod = constructionServiceType.GetMethod("GetShowIndex",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// Settings access via MB static class (Settings is protected static)
			var mbType = assembly.GetType("Eremite.MB");
			if (mbType != null) {
				_mbSettingsProperty = mbType.GetProperty("Settings",
					BindingFlags.NonPublic | BindingFlags.Static);
			}

			// Settings fields
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
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

		private static void CacheRecipeTypes(Assembly assembly) {
			// IWorkshop interface
			var workshopInterfaceType = assembly.GetType("Eremite.Buildings.IWorkshop");
			if (workshopInterfaceType != null) {
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
			if (recipeStateType != null) {
				_recipeStateModelField = recipeStateType.GetField("model",
					BindingFlags.Public | BindingFlags.Instance);
				_recipeStateActiveField = recipeStateType.GetField("active",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// WorkshopRecipeModel
			var recipeModelType = assembly.GetType("Eremite.Buildings.WorkshopRecipeModel");
			if (recipeModelType != null) {
				_recipeProducedGoodField = recipeModelType.GetField("producedGood",
					BindingFlags.Public | BindingFlags.Instance);
				_recipeRequiredGoodsField = recipeModelType.GetField("requiredGoods",
					BindingFlags.Public | BindingFlags.Instance);
				_recipeProductionTimeField = recipeModelType.GetField("productionTime",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// RecipeModel (base class of WorkshopRecipeModel) - grade field is inherited
			var baseRecipeModelType = assembly.GetType("Eremite.Buildings.RecipeModel");
			if (baseRecipeModelType != null) {
				_recipeGradeField = baseRecipeModelType.GetField("grade",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// RecipeGradeModel
			var gradeModelType = assembly.GetType("Eremite.Buildings.RecipeGradeModel");
			if (gradeModelType != null) {
				_gradeModelLevelField = gradeModelType.GetField("level",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheGoodTypes(Assembly assembly) {
			// GoodModel
			var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
			if (goodModelType != null) {
				_goodDisplayNameField = goodModelType.GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				_goodNameProperty = goodModelType.GetProperty("Name",
					BindingFlags.Public | BindingFlags.Instance);
				_goodCategoryField = goodModelType.GetField("category",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// GoodsSet
			var goodsSetType = assembly.GetType("Eremite.Model.GoodsSet");
			if (goodsSetType != null) {
				_goodsSetGoodsField = goodsSetType.GetField("goods",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheBuildingTypes(Assembly assembly) {
			// BuildingModel
			var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
			if (buildingModelType != null) {
				_buildingDisplayNameField = buildingModelType.GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				_buildingNameProperty = buildingModelType.GetProperty("Name",
					BindingFlags.Public | BindingFlags.Instance);
				_hasAccessToMethod = buildingModelType.GetMethod("HasAccessTo",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheBiomeTypes(Assembly assembly) {
			var biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
			if (biomeModelType != null) {
				_biomeGetDepositsGoodsMethod = biomeModelType.GetMethod("GetDepositsGoods",
					BindingFlags.Public | BindingFlags.Instance);
				_biomeGetTreesGoodsMethod = biomeModelType.GetMethod("GetTreesGoods",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		private static object GetGameService(PropertyInfo serviceProperty) {
			EnsureTypesCached();
			return GameReflection.GetService(serviceProperty);
		}

		private static object GetWorkshopsService() => GetGameService(_gsWorkshopsServiceProperty);
		private static object GetStorageService() => GetGameService(_gsStorageServiceProperty);
		private static object GetStoragePrefsService() => GetGameService(_gsStoragePrefsServiceProperty);
		private static object GetBuildingsService() => GetGameService(_gsBuildingsServiceProperty);
		private static object GetGameContentService() => GetGameService(_gsGameContentServiceProperty);
		private static object GetRecipesService() => GetGameService(_gsRecipesServiceProperty);
		private static object GetConstructionService() => GetGameService(_gsConstructionServiceProperty);

		private static object GetSettings() {
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
		public static int GetGlobalLimit(string goodName) {
			var workshopsService = GetWorkshopsService();
			return ReflectionHelper.InvokeInt(_getGlobalLimitForMethod, workshopsService, goodName);
		}

		/// <summary>
		/// Set the global production limit for a good.
		/// Set to 0 to remove the limit.
		/// </summary>
		public static bool SetGlobalLimit(string goodName, int limit) {
			var workshopsService = GetWorkshopsService();
			return ReflectionHelper.InvokeVoid(_setGlobalLimitForMethod, workshopsService, goodName, limit);
		}

		// ========================================
		// STORAGE ACCESS
		// ========================================

		/// <summary>
		/// Get the amount of a good in the main storage.
		/// </summary>
		public static int GetStorageAmount(string goodName) {
			var storageService = GetStorageService();
			return ReflectionHelper.InvokeInt(_getAmountMethod, storageService, goodName);
		}

		// ========================================
		// STORAGE RESERVE (MINIMUM) ACCESS
		// ========================================

		/// <summary>
		/// Get the storage reserve (minimum) for a good.
		/// Returns 0 if no reserve is set.
		/// </summary>
		public static int GetStorageReserve(string goodName) {
			var storagePrefsService = GetStoragePrefsService();
			return ReflectionHelper.InvokeInt(_getLimitMethod, storagePrefsService, goodName);
		}

		/// <summary>
		/// Set the storage reserve (minimum) for a good.
		/// Setting to 0 removes the reserve.
		/// </summary>
		public static bool SetStorageReserve(string goodName, int amount) {
			var storagePrefsService = GetStoragePrefsService();
			return ReflectionHelper.InvokeVoid(_setLimitMethod, storagePrefsService, goodName, amount);
		}

		// ========================================
		// RECIPE DATA ACCESS
		// ========================================

		/// <summary>
		/// Data structure for a recipe in the overlay.
		/// </summary>
		public class RecipeInfo {
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
		public class GoodInfo {
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
		public static List<GoodInfo> GetAllGoods(bool showAll) {
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

		private static void AddBuiltWorkshops(Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings) {
			var buildingsService = GetBuildingsService();
			if (buildingsService == null) return;

			var workshopsDict = ReflectionHelper.GetProp(_workshopsDictProperty, buildingsService);
			if (workshopsDict != null)
				AddWorkshopsFromDict(workshopsDict, result, constructedBuildings);

			var blightPostsDict = ReflectionHelper.GetProp(_blightPostsDictProperty, buildingsService);
			if (blightPostsDict != null)
				AddWorkshopsFromDict(blightPostsDict, result, constructedBuildings);
		}

		private static void AddWorkshopsFromDict(object dict, Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings) {
			// Use reflection to iterate the dictionary values
			var valuesProperty = dict.GetType().GetProperty("Values");
			if (valuesProperty == null) return;

			var values = valuesProperty.GetValue(dict) as IEnumerable;
			if (values == null) return;

			foreach (var workshop in values) {
				if (workshop == null) continue;

				// Mark this building model as constructed
				var buildingModel = ReflectionHelper.GetProp(_workshopBaseModelProperty, workshop);
				if (buildingModel != null) {
					var modelName = ReflectionHelper.GetPropString(_buildingNameProperty, buildingModel);
					if (!string.IsNullOrEmpty(modelName)) {
						constructedBuildings.Add(modelName);
					}
				}

				// Get recipes from this workshop
				var recipes = ReflectionHelper.GetProp(_workshopRecipesProperty, workshop) as IEnumerable;
				if (recipes == null) continue;

				// Get workshop display name and index
				var workshopDisplayName = GetBuildingDisplayName(buildingModel);
				var workshopBase = ReflectionHelper.GetProp(_workshopBaseProperty, workshop);
				var showIndex = GetShowIndex(workshopBase);

				foreach (var recipeState in recipes) {
					if (recipeState == null) continue;

					var modelName = ReflectionHelper.GetString(_recipeStateModelField, recipeState);
					if (string.IsNullOrEmpty(modelName)) continue;

					var recipeModel = GetWorkshopRecipeModel(modelName);
					if (recipeModel == null) continue;

					var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
					if (producedGoodRef == null) continue;

					var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
					if (goodModel == null) continue;

					var goodName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
					if (string.IsNullOrEmpty(goodName)) continue;

					// Get or create GoodInfo
					if (!result.TryGetValue(goodName, out var goodInfo)) {
						goodInfo = new GoodInfo {
							Name = goodName,
							DisplayName = GetGoodDisplayName(goodModel),
							StorageAmount = GetStorageAmount(goodName),
							Limit = GetGlobalLimit(goodName)
						};
						result[goodName] = goodInfo;
					}

					// Add recipe info
					goodInfo.Recipes.Add(new RecipeInfo {
						Workshop = workshop,
						WorkshopModel = buildingModel,
						RecipeState = recipeState,
						RecipeModel = recipeModel,
						WorkshopName = workshopDisplayName,
						WorkshopIndex = showIndex,
						IsActive = ReflectionHelper.GetBool(_recipeStateActiveField, recipeState),
						IsBuilt = true
					});
				}
			}
		}

		private static void AddUnbuiltWorkshops(Dictionary<string, GoodInfo> result, HashSet<string> constructedBuildings, bool skipUnlockCheck) {
			var settings = GetSettings();
			if (settings == null) return;

			var recipesService = GetRecipesService();
			var gameContentService = GetGameContentService();

			// Get workshops array
			var workshops = ReflectionHelper.GetField(_settingsWorkshopsField, settings) as Array;
			if (workshops != null) {
				foreach (var workshopModel in workshops) {
					AddUnbuiltWorkshop(workshopModel, result, constructedBuildings, recipesService, gameContentService, skipUnlockCheck);
				}
			}

			// Get blight posts array
			var blightPosts = ReflectionHelper.GetField(_settingsBlightPostsField, settings) as Array;
			if (blightPosts != null) {
				foreach (var workshopModel in blightPosts) {
					AddUnbuiltWorkshop(workshopModel, result, constructedBuildings, recipesService, gameContentService, skipUnlockCheck);
				}
			}
		}


		private static void AddUnbuiltWorkshop(object workshopModel, Dictionary<string, GoodInfo> result,
			HashSet<string> constructedBuildings, object recipesService, object gameContentService, bool skipUnlockCheck) {
			if (workshopModel == null) return;

			var modelName = ReflectionHelper.GetPropString(_buildingNameProperty, workshopModel);
			if (string.IsNullOrEmpty(modelName)) return;

			// Skip if already constructed
			if (constructedBuildings.Contains(modelName)) return;

			// Check if unlocked (skip check in "show all" mode)
			if (!skipUnlockCheck && !IsBuildingUnlocked(workshopModel, gameContentService)) return;

			// Check if has access to
			if (!ReflectionHelper.InvokeBool(_hasAccessToMethod, workshopModel)) return;

			// Get recipes for this building
			var recipeNames = GetRecipesForBuilding(modelName, recipesService);
			if (recipeNames == null || recipeNames.Count == 0) return;

			var workshopDisplayName = GetBuildingDisplayName(workshopModel);

			foreach (var recipeName in recipeNames) {
				var recipeModel = GetWorkshopRecipeModel(recipeName);
				if (recipeModel == null) continue;

				var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
				if (producedGoodRef == null) continue;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
				if (goodModel == null) continue;

				var goodName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
				if (string.IsNullOrEmpty(goodName)) continue;

				// Get or create GoodInfo
				if (!result.TryGetValue(goodName, out var goodInfo)) {
					goodInfo = new GoodInfo {
						Name = goodName,
						DisplayName = GetGoodDisplayName(goodModel),
						StorageAmount = GetStorageAmount(goodName),
						Limit = GetGlobalLimit(goodName)
					};
					result[goodName] = goodInfo;
				}

				// Add recipe info (unbuilt)
				goodInfo.Recipes.Add(new RecipeInfo {
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

		private static List<string> GetRecipesForBuilding(string buildingName, object recipesService) {
			return ReflectionHelper.Invoke(_getRecipesForMethod, recipesService, buildingName) as List<string>;
		}

		private static bool IsBuildingUnlocked(object buildingModel, object gameContentService) {
			if (gameContentService == null || _isUnlockedMethod == null) return true;

			try {
				// Try to find the overload that takes BuildingModel
				var methods = gameContentService.GetType().GetMethods()
					.Where(m => m.Name == "IsUnlocked");

				foreach (var method in methods) {
					var parameters = method.GetParameters();
					if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(buildingModel.GetType())) {
						return ReflectionHelper.InvokeBool(method, gameContentService, buildingModel);
					}
				}

				return true;
			} catch {
				return true;
			}
		}

		private static int GetShowIndex(object building) {
			var constructionService = GetConstructionService();
			return ReflectionHelper.InvokeInt(_getShowIndexMethod, constructionService, building);
		}

		// ========================================
		// RECIPE TOGGLING
		// ========================================

		/// <summary>
		/// Toggle a recipe's active state.
		/// Returns the new active state.
		/// </summary>
		public static bool ToggleRecipe(RecipeInfo recipe) {
			if (recipe == null || recipe.Workshop == null || recipe.RecipeState == null) {
				Debug.LogWarning("[ATSAccessibility] RecipesReflection: Cannot toggle unbuilt recipe");
				return false;
			}

			// Call workshop.SwitchProductionOf(recipeState)
			ReflectionHelper.InvokeVoid(_switchProductionOfMethod, recipe.Workshop, recipe.RecipeState);

			// Get the new active state
			recipe.IsActive = ReflectionHelper.GetBool(_recipeStateActiveField, recipe.RecipeState);
			return recipe.IsActive;
		}

		// ========================================
		// NAME HELPERS
		// ========================================

		/// <summary>
		/// Get the display name of a good model.
		/// </summary>
		public static string GetGoodDisplayName(object goodModel) {
			if (goodModel == null) return Strings.Get("common.unknown");

			var text = ReflectionHelper.GetLocaString(_goodDisplayNameField, goodModel);
			if (!string.IsNullOrEmpty(text)) return text;

			// Fallback to Name property
			return ReflectionHelper.GetPropString(_goodNameProperty, goodModel) ?? Strings.Get("common.unknown");
		}

		/// <summary>
		/// Get the display name of a building model.
		/// </summary>
		public static string GetBuildingDisplayName(object buildingModel) {
			if (buildingModel == null) return Strings.Get("common.unknown");

			var text = ReflectionHelper.GetLocaString(_buildingDisplayNameField, buildingModel);
			if (!string.IsNullOrEmpty(text)) return text;

			// Fallback to Name property
			return ReflectionHelper.GetPropString(_buildingNameProperty, buildingModel) ?? Strings.Get("common.unknown");
		}

		private static object GetWorkshopRecipeModel(string recipeName) {
			var settings = GetSettings();
			return ReflectionHelper.Invoke(_getWorkshopRecipeMethod, settings, recipeName);
		}

		// ========================================
		// RECIPE INFO HELPERS
		// ========================================

		/// <summary>
		/// Get the output amount from a recipe model.
		/// </summary>
		public static int GetRecipeOutputAmount(object recipeModel) {
			if (recipeModel == null) return 0;

			var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
			if (producedGoodRef == null) return 0;

			return ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, producedGoodRef);
		}

		/// <summary>
		/// Get the output good name from a recipe model.
		/// </summary>
		public static string GetRecipeOutputName(object recipeModel) {
			if (recipeModel == null) return Strings.Get("common.unknown");

			var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
			if (producedGoodRef == null) return Strings.Get("common.unknown");

			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
			return GetGoodDisplayName(goodModel);
		}

		/// <summary>
		/// Get the production time from a recipe model.
		/// </summary>
		public static float GetRecipeProductionTime(object recipeModel) {
			return ReflectionHelper.GetFloat(_recipeProductionTimeField, recipeModel);
		}

		/// <summary>
		/// Get the grade level (stars) from a recipe model.
		/// </summary>
		public static int GetRecipeGradeLevel(object recipeModel) {
			var grade = ReflectionHelper.GetField(_recipeGradeField, recipeModel);
			if (grade == null) return 0;

			return ReflectionHelper.GetInt(_gradeModelLevelField, grade);
		}

		/// <summary>
		/// Get the required goods (ingredients) from a recipe model.
		/// Returns array of GoodsSet objects.
		/// </summary>
		public static Array GetRecipeRequiredGoods(object recipeModel) {
			return ReflectionHelper.GetField(_recipeRequiredGoodsField, recipeModel) as Array;
		}

		/// <summary>
		/// Get the goods array from a GoodsSet.
		/// </summary>
		public static Array GetGoodsSetGoods(object goodsSet) {
			return ReflectionHelper.GetField(_goodsSetGoodsField, goodsSet) as Array;
		}

		/// <summary>
		/// Get the display name from a GoodRef.
		/// </summary>
		public static string GetGoodRefDisplayName(object goodRef) {
			if (goodRef == null) return Strings.Get("common.unknown");

			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, goodRef);
			return GetGoodDisplayName(goodModel);
		}

		/// <summary>
		/// Get the amount from a GoodRef.
		/// </summary>
		public static int GetGoodRefAmount(object goodRef) {
			return ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, goodRef);
		}

		// ========================================
		// RECIPE COMPARISON
		// ========================================

		public enum RecipeCompareStatus { New, Better, Same, Worse }

		public class RecipeComparison {
			public string GoodDisplayName;
			public int GradeLevel; // star level of this recipe
			public RecipeCompareStatus Status;
			public int LevelDifference; // targetLevel - ownedLevel (only meaningful for Better)
			public bool CanProduce; // whether the recipe's ingredients are obtainable on this map
		}

		/// <summary>
		/// Compare each recipe of a candidate building against the player's owned workshops.
		/// Returns per-recipe comparison status (new, better, already unlocked)
		/// and whether each recipe can be produced on the current map.
		/// Returns empty list if the building has no recipes.
		/// </summary>
		public static List<RecipeComparison> GetRecipeComparisons(string buildingName) {
			EnsureTypesCached();
			var result = new List<RecipeComparison>();

			if (string.IsNullOrEmpty(buildingName)) return result;

			var recipesService = GetRecipesService();
			if (recipesService == null) return result;

			// Get recipes for the candidate building
			var candidateRecipeNames = GetRecipesForBuilding(buildingName, recipesService);
			if (candidateRecipeNames == null || candidateRecipeNames.Count == 0) return result;

			// Collect owned workshop models (same logic as game's CacheOwnedWorkshops)
			var ownedWorkshopNames = CollectOwnedWorkshopNames();

			// Build set of all obtainable goods on this map (for producibility check)
			// Include the candidate building itself since the player is about to pick it
			var workshopNamesWithCandidate = new List<string>(ownedWorkshopNames);
			if (!workshopNamesWithCandidate.Contains(buildingName))
				workshopNamesWithCandidate.Add(buildingName);
			var obtainableGoods = BuildObtainableGoods(workshopNamesWithCandidate, recipesService);

			foreach (var recipeName in candidateRecipeNames) {
				var recipeModel = GetWorkshopRecipeModel(recipeName);
				if (recipeModel == null) continue;

				// Get produced good internal name
				var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
				if (producedGoodRef == null) continue;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
				if (goodModel == null) continue;

				var goodInternalName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
				if (string.IsNullOrEmpty(goodInternalName)) continue;

				// Get target grade level
				int targetLevel = GetRecipeGradeLevel(recipeModel);

				// Find best owned grade for this good
				int? bestOwnedLevel = GetBestOwnedGradeLevel(goodInternalName, ownedWorkshopNames, recipesService);

				// Check if recipe can be produced on this map
				bool canProduce = CanProduceRecipe(recipeModel, obtainableGoods);

				var comparison = new RecipeComparison {
					GoodDisplayName = GetGoodDisplayName(goodModel),
					GradeLevel = targetLevel,
					CanProduce = canProduce
				};

				if (bestOwnedLevel == null) {
					comparison.Status = RecipeCompareStatus.New;
				} else if (bestOwnedLevel.Value < targetLevel) {
					comparison.Status = RecipeCompareStatus.Better;
					comparison.LevelDifference = targetLevel - bestOwnedLevel.Value;
				} else if (bestOwnedLevel.Value > targetLevel) {
					comparison.Status = RecipeCompareStatus.Worse;
					comparison.LevelDifference = targetLevel - bestOwnedLevel.Value;
				} else {
					comparison.Status = RecipeCompareStatus.Same;
				}

				result.Add(comparison);
			}

			return result;
		}

		/// <summary>
		/// Check if a specific recipe can be produced given a set of obtainable goods.
		/// Returns true if every ingredient slot has at least one obtainable good.
		/// </summary>
		private static bool CanProduceRecipe(object recipeModel, HashSet<string> obtainableGoods) {
			var requiredGoods = ReflectionHelper.GetField(_recipeRequiredGoodsField, recipeModel) as Array;
			if (requiredGoods == null || requiredGoods.Length == 0) return true; // No ingredients needed

			foreach (var goodsSet in requiredGoods) {
				if (goodsSet == null) continue;

				var goods = ReflectionHelper.GetField(_goodsSetGoodsField, goodsSet) as Array;
				if (goods == null || goods.Length == 0) continue; // Empty slot, skip

				// Check if at least one good in this slot is obtainable
				bool slotSatisfied = false;
				foreach (var goodRef in goods) {
					if (goodRef == null) continue;
					var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, goodRef);
					if (goodModel == null) continue;
					var goodName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
					if (!string.IsNullOrEmpty(goodName) && obtainableGoods.Contains(goodName)) {
						slotSatisfied = true;
						break;
					}
				}

				if (!slotSatisfied) return false;
			}

			return true;
		}

		/// <summary>
		/// Build the set of all goods obtainable on the current map, considering biome resources
		/// and all recipes from the given workshops. Uses a fixed-point algorithm:
		/// start with biome resources, then iteratively add outputs of recipes whose
		/// ingredients are all obtainable, until no new goods are added.
		/// </summary>
		private static HashSet<string> BuildObtainableGoods(List<string> workshopNames, object recipesService) {
			var obtainable = new HashSet<string>();

			// Step 1: Seed with biome resources (raw materials from deposits + trees)
			var biome = GameReflection.GetCurrentBiome();
			if (biome != null) {
				CollectBiomeResourceNames(biome, obtainable);
			}

			// Step 2: Collect all recipes from the given workshops
			// Each entry: (output good name, list of ingredient slots where each slot is a list of good names)
			var allRecipes = new List<KeyValuePair<string, List<List<string>>>>();
			foreach (var workshopName in workshopNames) {
				var recipeNames = GetRecipesForBuilding(workshopName, recipesService);
				if (recipeNames == null) continue;

				foreach (var recipeName in recipeNames) {
					var recipeModel = GetWorkshopRecipeModel(recipeName);
					if (recipeModel == null) continue;

					var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
					if (producedGoodRef == null) continue;
					var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
					if (goodModel == null) continue;
					var outputName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
					if (string.IsNullOrEmpty(outputName)) continue;

					var slots = GetIngredientSlotGoodNames(recipeModel);
					allRecipes.Add(new KeyValuePair<string, List<List<string>>>(outputName, slots));
				}
			}

			// Step 3: Fixed-point expansion — keep adding producible outputs until stable
			bool changed = true;
			while (changed) {
				changed = false;
				foreach (var recipe in allRecipes) {
					if (obtainable.Contains(recipe.Key)) continue;

					bool canProduce = true;
					foreach (var slot in recipe.Value) {
						bool slotSatisfied = false;
						foreach (var goodName in slot) {
							if (obtainable.Contains(goodName)) {
								slotSatisfied = true;
								break;
							}
						}
						if (!slotSatisfied) {
							canProduce = false;
							break;
						}
					}

					if (canProduce) {
						obtainable.Add(recipe.Key);
						changed = true;
					}
				}
			}

			return obtainable;
		}

		/// <summary>
		/// Get the internal good names for each ingredient slot in a recipe.
		/// Returns a list of slots, where each slot is a list of alternative good names.
		/// </summary>
		private static List<List<string>> GetIngredientSlotGoodNames(object recipeModel) {
			var result = new List<List<string>>();
			var requiredGoods = ReflectionHelper.GetField(_recipeRequiredGoodsField, recipeModel) as Array;
			if (requiredGoods == null) return result;

			foreach (var goodsSet in requiredGoods) {
				if (goodsSet == null) continue;
				var goods = ReflectionHelper.GetField(_goodsSetGoodsField, goodsSet) as Array;
				if (goods == null || goods.Length == 0) continue;

				var slotNames = new List<string>();
				foreach (var goodRef in goods) {
					if (goodRef == null) continue;
					var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, goodRef);
					if (goodModel == null) continue;
					var goodName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
					if (!string.IsNullOrEmpty(goodName))
						slotNames.Add(goodName);
				}

				if (slotNames.Count > 0)
					result.Add(slotNames);
			}

			return result;
		}

		/// <summary>
		/// Collect internal names of all biome resources (deposits + trees) into the given set.
		/// </summary>
		private static void CollectBiomeResourceNames(object biome, HashSet<string> names) {
			CollectGoodNamesFromEnumerable(
				_biomeGetDepositsGoodsMethod != null ? _biomeGetDepositsGoodsMethod.Invoke(biome, null) as IEnumerable : null,
				names);
			CollectGoodNamesFromEnumerable(
				_biomeGetTreesGoodsMethod != null ? _biomeGetTreesGoodsMethod.Invoke(biome, null) as IEnumerable : null,
				names);
		}

		/// <summary>
		/// Extract internal good names from an IEnumerable of GoodModel objects.
		/// </summary>
		private static void CollectGoodNamesFromEnumerable(IEnumerable goods, HashSet<string> names) {
			if (goods == null) return;
			foreach (var goodModel in goods) {
				if (goodModel == null) continue;
				var name = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
				if (!string.IsNullOrEmpty(name))
					names.Add(name);
			}
		}

		/// <summary>
		/// Collect internal names of all owned workshop models
		/// (unlocked from settings + built but not unlocked from BuildingsService).
		/// </summary>
		private static List<string> CollectOwnedWorkshopNames() {
			var names = new List<string>();
			var settings = GetSettings();
			var gameContentService = GetGameContentService();

			// Settings.workshops where IsUnlocked
			if (settings != null) {
				var workshops = ReflectionHelper.GetField(_settingsWorkshopsField, settings) as Array;
				if (workshops != null) {
					foreach (var workshopModel in workshops) {
						if (workshopModel == null) continue;
						if (IsBuildingUnlocked(workshopModel, gameContentService)) {
							var name = ReflectionHelper.GetPropString(_buildingNameProperty, workshopModel);
							if (!string.IsNullOrEmpty(name))
								names.Add(name);
						}
					}
				}
			}

			// BuildingsService.Workshops values where NOT unlocked (built but not in settings unlock list)
			var buildingsService = GetBuildingsService();
			if (buildingsService != null) {
				var workshopsDict = ReflectionHelper.GetProp(_workshopsDictProperty, buildingsService);
				if (workshopsDict != null) {
					var valuesProperty = workshopsDict.GetType().GetProperty("Values");
					var values = valuesProperty?.GetValue(workshopsDict) as IEnumerable;
					if (values != null) {
						foreach (var workshop in values) {
							if (workshop == null) continue;
							var model = ReflectionHelper.GetProp(_workshopBaseModelProperty, workshop);
							if (model == null) continue;
							if (!IsBuildingUnlocked(model, gameContentService)) {
								var name = ReflectionHelper.GetPropString(_buildingNameProperty, model);
								if (!string.IsNullOrEmpty(name) && !names.Contains(name))
									names.Add(name);
							}
						}
					}
				}
			}

			return names;
		}

		/// <summary>
		/// Find the best owned grade level for a produced good across all owned workshops.
		/// Returns null if no owned workshop produces this good.
		/// </summary>
		private static int? GetBestOwnedGradeLevel(string goodName, List<string> ownedWorkshopNames, object recipesService) {
			int? bestLevel = null;

			foreach (var workshopName in ownedWorkshopNames) {
				var recipeNames = GetRecipesForBuilding(workshopName, recipesService);
				if (recipeNames == null) continue;

				foreach (var recipeName in recipeNames) {
					var recipeModel = GetWorkshopRecipeModel(recipeName);
					if (recipeModel == null) continue;

					var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
					if (producedGoodRef == null) continue;

					var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGoodRef);
					if (goodModel == null) continue;

					var recipeGoodName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
					if (recipeGoodName != goodName) continue;

					int level = GetRecipeGradeLevel(recipeModel);
					if (bestLevel == null || level > bestLevel.Value)
						bestLevel = level;
				}
			}

			return bestLevel;
		}

		// ========================================
		// INGREDIENT MODE
		// ========================================

		/// <summary>
		/// Get all goods that are consumed as ingredients, organized by ingredient good.
		/// Each GoodInfo represents an ingredient, with Recipes listing every recipe that uses it.
		/// </summary>
		public static List<GoodInfo> GetAllGoodsAsIngredients(bool showAll) {
			EnsureTypesCached();

			// Get all goods in producer mode to harvest all recipes
			var producerGoods = GetAllGoods(showAll);
			if (producerGoods == null) return new List<GoodInfo>();

			var result = new Dictionary<string, GoodInfo>();
			var seenRecipes = new Dictionary<string, HashSet<string>>(); // ingredientName -> set of recipe workshop+model keys

			foreach (var producerGood in producerGoods) {
				foreach (var recipe in producerGood.Recipes) {
					var requiredGoods = GetRecipeRequiredGoods(recipe.RecipeModel);
					if (requiredGoods == null) continue;

					// Build a unique key for this recipe instance
					var workshopName = ReflectionHelper.GetPropString(_buildingNameProperty, recipe.WorkshopModel) ?? "";
					var recipeKey = workshopName + ":" + (recipe.RecipeState?.GetHashCode().ToString() ?? recipe.RecipeModel.GetHashCode().ToString());

					foreach (var goodsSet in requiredGoods) {
						if (goodsSet == null) continue;
						var goods = GetGoodsSetGoods(goodsSet);
						if (goods == null) continue;

						foreach (var goodRef in goods) {
							if (goodRef == null) continue;
							var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, goodRef);
							if (goodModel == null) continue;

							var ingredientName = ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
							if (string.IsNullOrEmpty(ingredientName)) continue;

							// Deduplicate: same recipe shouldn't appear twice for the same ingredient
							if (!seenRecipes.TryGetValue(ingredientName, out var seen)) {
								seen = new HashSet<string>();
								seenRecipes[ingredientName] = seen;
							}
							if (!seen.Add(recipeKey)) continue;

							if (!result.TryGetValue(ingredientName, out var ingredientInfo)) {
								ingredientInfo = new GoodInfo {
									Name = ingredientName,
									DisplayName = GetGoodDisplayName(goodModel),
									StorageAmount = GetStorageAmount(ingredientName),
									Limit = GetGlobalLimit(ingredientName)
								};
								result[ingredientName] = ingredientInfo;
							}

							ingredientInfo.Recipes.Add(recipe);
						}
					}
				}
			}

			var list = new List<GoodInfo>(result.Values);
			list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
			return list;
		}

		/// <summary>
		/// Get the internal name of the produced good from a recipe model.
		/// </summary>
		public static string GetRecipeOutputInternalName(object recipeModel) {
			if (recipeModel == null) return null;
			var producedGoodRef = ReflectionHelper.GetField(_recipeProducedGoodField, recipeModel);
			if (producedGoodRef == null) return null;
			return GetGoodRefInternalName(producedGoodRef);
		}

		/// <summary>
		/// Get the internal name from a GoodRef.
		/// </summary>
		public static string GetGoodRefInternalName(object goodRef) {
			if (goodRef == null) return null;
			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, goodRef);
			if (goodModel == null) return null;
			return ReflectionHelper.GetPropString(_goodNameProperty, goodModel);
		}

		// ========================================
		// POPUP DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a RecipesPopup.
		/// </summary>
		public static bool IsRecipesPopup(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return _recipesPopupType != null && _recipesPopupType.IsInstanceOfType(popup);
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(RecipesReflection), "RecipesReflection");
		}
	}
}
