using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to building construction, placement, range info,
	/// lake interaction, supply chain analysis, and building enumeration.
	/// Split from BuildingReflection.cs for maintainability.
	/// </summary>
	public static class ConstructionReflection {
		// ========================================
		// BUILDING SYSTEM REFLECTION (moved from GameReflection)
		// ========================================

		// ========================================
		// BUILDING SYSTEM REFLECTION
		// ========================================

		private static FieldInfo _settingsBuildingsField = null;
		private static FieldInfo _settingsBuildingCategoriesField = null;
		private static PropertyInfo _gsGameContentServiceProperty = null;
		private static PropertyInfo _gsConstructionServiceProperty = null;
		private static MethodInfo _gcsIsUnlockedMethod = null;
		private static MethodInfo _csCanConstructMethod = null;
		private static Type _buildingCreatorType = null;
		private static MethodInfo _bcCreateBuildingMethod = null;
		private static object _buildingCreatorInstance = null;
		private static bool _buildingSystemTypesCached = false;

		/// <summary>
		/// Clear cached BuildingCreator instance on scene change.
		/// The instance may hold internal references to destroyed game services.
		/// </summary>
		public static void ClearBuildingCreatorInstance() {
			_buildingCreatorInstance = null;
		}

		// BuildingModel field caching (used by multiple methods called per-building)
		private static FieldInfo _bmCategoryField = null;
		private static FieldInfo _bmIsInShopField = null;
		private static FieldInfo _bmSizeField = null;
		private static FieldInfo _bmIsActiveField = null;
		private static PropertyInfo _bmDescriptionProperty = null;
		private static FieldInfo _bmDescriptionField = null;
		private static FieldInfo _bcmIsOnHUDField = null;
		private static FieldInfo _bmRequiredGoodsField = null;
		private static Type _goodRefType = null;
		private static FieldInfo _goodRefGoodField = null;
		private static FieldInfo _goodRefAmountField = null;
		private static PropertyInfo _goodRefDisplayNameProperty = null;
		private static bool _bmFieldsCached = false;

		// ========================================
		// SHARED GOODREF PROPERTIES (used by multiple Reflection files via BuildingReflection forwarding)
		// ========================================

		public static Type GoodRefType { get { EnsureBuildingModelFields(); return _goodRefType; } }
		public static FieldInfo GoodRefGoodField { get { EnsureBuildingModelFields(); return _goodRefGoodField; } }
		public static FieldInfo GoodRefAmountField { get { EnsureBuildingModelFields(); return _goodRefAmountField; } }
		public static PropertyInfo GoodRefDisplayNameProperty { get { EnsureBuildingModelFields(); return _goodRefDisplayNameProperty; } }

		private static void EnsureBuildingSystemTypes() {
			if (_buildingSystemTypesCached) return;

			if (GameReflection.GameAssembly == null) {
				_buildingSystemTypesCached = true;
				return;
			}

			try {
				// Get Buildings and BuildingCategories from Settings
				var settingsType = GameReflection.GameAssembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsBuildingsField = settingsType.GetField("Buildings",
						BindingFlags.Public | BindingFlags.Instance);
					_settingsBuildingCategoriesField = settingsType.GetField("BuildingCategories",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get GameContentService from IGameServices
				var gameServicesType = GameReflection.GameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGameContentServiceProperty = gameServicesType.GetProperty("GameContentService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsConstructionServiceProperty = gameServicesType.GetProperty("ConstructionService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get IsUnlocked method from IGameContentService
				var gameContentServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IGameContentService");
				if (gameContentServiceType != null) {
					var buildingModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingModel");
					if (buildingModelType != null) {
						_gcsIsUnlockedMethod = gameContentServiceType.GetMethod("IsUnlocked",
							new Type[] { buildingModelType });
					}
				}

				// Get CanConstruct method from IConstructionService
				var constructionServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IConstructionService");
				if (constructionServiceType != null) {
					var buildingModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingModel");
					if (buildingModelType != null) {
						_csCanConstructMethod = constructionServiceType.GetMethod("CanConstruct",
							new Type[] { buildingModelType });
					}
				}

				// Get BuildingCreator class
				_buildingCreatorType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingCreator");
				if (_buildingCreatorType != null) {
					_bcCreateBuildingMethod = _buildingCreatorType.GetMethod("CreateBuilding",
						new Type[] { GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingModel"), typeof(int) });
				}

				Debug.Log("[ATSAccessibility] Cached building system types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Building type caching failed: {ex.Message}");
			}

			_buildingSystemTypesCached = true;
		}

		/// <summary>
		/// Cache BuildingModel and BuildingCategoryModel field info for efficient per-building lookups.
		/// </summary>
		private static void EnsureBuildingModelFields() {
			if (_bmFieldsCached) return;
			EnsureBuildingSystemTypes();

			if (GameReflection.GameAssembly == null) {
				_bmFieldsCached = true;
				return;
			}

			try {
				// Cache BuildingModel fields
				var buildingModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingModel");
				if (buildingModelType != null) {
					_bmCategoryField = buildingModelType.GetField("category",
						BindingFlags.Public | BindingFlags.Instance);
					_bmIsInShopField = buildingModelType.GetField("isInShop",
						BindingFlags.Public | BindingFlags.Instance);
					_bmSizeField = buildingModelType.GetField("size",
						BindingFlags.Public | BindingFlags.Instance);
					_bmIsActiveField = buildingModelType.GetField("isActive",
						BindingFlags.Public | BindingFlags.Instance);
					_bmDescriptionProperty = buildingModelType.GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
					_bmDescriptionField = buildingModelType.GetField("description",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_bmRequiredGoodsField = buildingModelType.GetField("requiredGoods",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache GoodRef fields (shared across multiple Reflection files)
				_goodRefType = GameReflection.GameAssembly.GetType("Eremite.Model.GoodRef");
				if (_goodRefType != null) {
					_goodRefGoodField = _goodRefType.GetField("good",
						BindingFlags.Public | BindingFlags.Instance);
					_goodRefAmountField = _goodRefType.GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);
					_goodRefDisplayNameProperty = _goodRefType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache BuildingCategoryModel fields
				var buildingCategoryModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingCategoryModel");
				if (buildingCategoryModelType != null) {
					_bcmIsOnHUDField = buildingCategoryModelType.GetField("isOnHUD",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached BuildingModel field info");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] BuildingModel field caching failed: {ex.Message}");
			}

			_bmFieldsCached = true;
		}

		/// <summary>
		/// Get all BuildingModel definitions from Settings.
		/// </summary>
		public static Array GetAllBuildingModels() {
			EnsureBuildingSystemTypes();
			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsBuildingsField == null) return null;

			try {
				return _settingsBuildingsField.GetValue(settings) as Array;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get all BuildingCategoryModel definitions from Settings.
		/// </summary>
		public static Array GetBuildingCategories() {
			EnsureBuildingSystemTypes();
			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsBuildingCategoriesField == null) return null;

			try {
				return _settingsBuildingCategoriesField.GetValue(settings) as Array;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the category of a BuildingModel.
		/// </summary>
		public static object GetBuildingCategory(object buildingModel) {
			if (buildingModel == null) return null;
			EnsureBuildingModelFields();

			try {
				return _bmCategoryField?.GetValue(buildingModel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if a building model is in the shop (should show in build menu).
		/// </summary>
		public static bool IsBuildingInShop(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureBuildingModelFields();

			try {
				if (_bmIsInShopField != null) {
					return (bool)_bmIsInShopField.GetValue(buildingModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBuildingInShop failed: {ex.Message}"); }
			return true; // Default to true
		}

		/// <summary>
		/// Get the size of a building model.
		/// </summary>
		public static Vector2Int GetBuildingSize(object buildingModel) {
			if (buildingModel == null) return Vector2Int.one;
			EnsureBuildingModelFields();

			try {
				if (_bmSizeField != null) {
					return (Vector2Int)_bmSizeField.GetValue(buildingModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingSize failed: {ex.Message}"); }
			return Vector2Int.one;
		}

		/// <summary>
		/// Get the description of a building model (from Settings/BuildingModel).
		/// For a building instance's description, use the existing GetBuildingDescription(building) above.
		/// </summary>
		public static string GetBuildingModelDescription(object buildingModel) {
			if (buildingModel == null) return null;
			EnsureBuildingModelFields();

			try {
				// Try the Description property first (virtual property in BuildingModel)
				if (_bmDescriptionProperty != null) {
					return _bmDescriptionProperty.GetValue(buildingModel) as string;
				}

				// Fall back to description field (LocaText)
				if (_bmDescriptionField != null) {
					var locaText = _bmDescriptionField.GetValue(buildingModel);
					if (locaText != null) {
						return GameReflection.GetLocaText(locaText);
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingDescription failed: {ex.Message}"); }
			return null;
		}

		/// <summary>
		/// Get the construction costs of a building model as a formatted string.
		/// Returns format like "2 Wood, 4 Planks" or null if no costs.
		/// </summary>
		public static string GetBuildingCosts(object buildingModel) {
			if (buildingModel == null) return null;
			EnsureConstructionTypes();

			try {
				// Use ConstructionService.GetConstructionCostFor to get rate-adjusted costs
				var constructionService = GetConstructionService();
				if (constructionService != null && _csGetConstructionCostForMethod != null &&
					_goodStructNameField != null && _goodStructAmountField != null) {
					var requiredGoods = _csGetConstructionCostForMethod.Invoke(
						constructionService, new[] { buildingModel }) as Array;
					if (requiredGoods != null && requiredGoods.Length > 0) {
						var storedGoods = GameReflection.GetAllStoredGoods();
						var costs = new List<string>();
						foreach (var good in requiredGoods) {
							if (good == null) continue;
							string goodName = _goodStructNameField.GetValue(good) as string;
							int amount = (int)_goodStructAmountField.GetValue(good);
							if (amount > 0 && !string.IsNullOrEmpty(goodName)) {
								string displayName = GameReflection.GetGoodDisplayName(goodName);
								int stored = 0;
								storedGoods.TryGetValue(goodName, out stored);
								if (stored < amount)
									costs.Add($"{amount} {displayName}, not enough");
								else
									costs.Add($"{amount} {displayName}");
							}
						}
						if (costs.Count > 0) return string.Join(", ", costs);
					}
				}

				// Fallback: read base costs from model if service unavailable
				EnsureBuildingModelFields();
				var rawGoods = _bmRequiredGoodsField?.GetValue(buildingModel) as Array;
				if (rawGoods == null || rawGoods.Length == 0) return null;

				var fallbackCosts = new List<string>();
				foreach (var goodRef in rawGoods) {
					if (goodRef == null) continue;
					int amount = (int?)_goodRefAmountField?.GetValue(goodRef) ?? 0;
					string displayName = _goodRefDisplayNameProperty?.GetValue(goodRef) as string;
					if (amount > 0 && !string.IsNullOrEmpty(displayName))
						fallbackCosts.Add($"{amount} {displayName}");
				}
				return fallbackCosts.Count > 0 ? string.Join(", ", fallbackCosts) : null;
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingCosts failed: {ex.Message}"); }
			return null;
		}

		/// <summary>
		/// Check if building model is active.
		/// </summary>
		public static bool IsBuildingActive(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureBuildingModelFields();

			try {
				if (_bmIsActiveField != null) {
					return (bool)_bmIsActiveField.GetValue(buildingModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBuildingActive failed: {ex.Message}"); }
			return true;
		}

		/// <summary>
		/// Check if building category is on HUD (should show in categories).
		/// </summary>
		public static bool IsCategoryOnHUD(object categoryModel) {
			if (categoryModel == null) return false;
			EnsureBuildingModelFields();

			try {
				if (_bcmIsOnHUDField != null) {
					return (bool)_bcmIsOnHUDField.GetValue(categoryModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsCategoryOnHUD failed: {ex.Message}"); }
			return true;
		}

		/// <summary>
		/// Get GameContentService from GameServices.
		/// </summary>
		public static object GetGameContentService() {
			EnsureBuildingSystemTypes();
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsGameContentServiceProperty == null) return null;

			try {
				return _gsGameContentServiceProperty.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get ConstructionService from GameServices.
		/// </summary>
		public static object GetConstructionService() {
			EnsureBuildingSystemTypes();
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsConstructionServiceProperty == null) return null;

			try {
				return _gsConstructionServiceProperty.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if a building is unlocked in the current game.
		/// </summary>
		public static bool IsBuildingUnlocked(object buildingModel) {
			EnsureBuildingSystemTypes();
			var gameContentService = GetGameContentService();
			if (gameContentService == null || _gcsIsUnlockedMethod == null || buildingModel == null)
				return false;

			try {
				return (bool)_gcsIsUnlockedMethod.Invoke(gameContentService, new object[] { buildingModel });
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if a building can be constructed (not at max amount).
		/// </summary>
		public static bool CanConstructBuilding(object buildingModel) {
			EnsureBuildingSystemTypes();
			var constructionService = GetConstructionService();
			if (constructionService == null || _csCanConstructMethod == null || buildingModel == null)
				return false;

			try {
				return (bool)_csCanConstructMethod.Invoke(constructionService, new object[] { buildingModel });
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Create a building instance using BuildingCreator.
		/// The building is not yet placed on the grid.
		/// </summary>
		public static object CreateBuilding(object buildingModel, int rotation = 0) {
			EnsureBuildingSystemTypes();
			if (_buildingCreatorType == null || _bcCreateBuildingMethod == null || buildingModel == null)
				return null;

			try {
				// Reuse cached BuildingCreator instance (stateless)
				if (_buildingCreatorInstance == null)
					_buildingCreatorInstance = Activator.CreateInstance(_buildingCreatorType);

				return _bcCreateBuildingMethod.Invoke(_buildingCreatorInstance, new object[] { buildingModel, rotation });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreateBuilding failed: {ex.Message}");
				return null;
			}
		}

		// Building placement reflection
		private static MethodInfo _csCanPlaceOnGridMethod = null;
		private static MethodInfo _csPlaceOnGridMethod = null;
		private static MethodInfo _csRemoveFromGridMethod = null;
		private static MethodInfo _buildingManualPlacingFinishedMethod = null;
		private static PropertyInfo _buildingFieldProperty = null;  // Building.Field (Vector2Int grid position)
		private static MethodInfo _buildingRemoveMethod = null;
		private static PropertyInfo _buildingRotationProperty = null;
		private static MethodInfo _buildingSetPositionMethod = null;
		private static MethodInfo _buildingRotateMethod = null;
		private static bool _buildingPlacementTypesCached = false;

		private static void EnsureBuildingPlacementTypes() {
			if (_buildingPlacementTypesCached) return;
			EnsureBuildingSystemTypes();

			if (GameReflection.GameAssembly == null) {
				_buildingPlacementTypesCached = true;
				return;
			}

			try {
				// Get ConstructionService methods
				var constructionServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IConstructionService");
				var buildingType = GameReflection.GameAssembly.GetType("Eremite.Buildings.Building");

				if (constructionServiceType != null && buildingType != null) {
					_csCanPlaceOnGridMethod = constructionServiceType.GetMethod("CanPlaceOnGrid",
						new Type[] { buildingType });
					_csPlaceOnGridMethod = constructionServiceType.GetMethod("PlaceOnGrid",
						new Type[] { buildingType });
					_csRemoveFromGridMethod = constructionServiceType.GetMethod("RemoveFromGrid",
						new Type[] { buildingType });
				}

				if (buildingType != null) {
					// Get Building methods and properties
					_buildingManualPlacingFinishedMethod = buildingType.GetMethod("ManualPlacingFinished",
						BindingFlags.Public | BindingFlags.Instance);
					_buildingRemoveMethod = buildingType.GetMethod("Remove",
						new Type[] { typeof(bool) });
					_buildingFieldProperty = buildingType.GetProperty("Field",
						BindingFlags.Public | BindingFlags.Instance);
					_buildingRotationProperty = buildingType.GetProperty("Rotation",
						BindingFlags.Public | BindingFlags.Instance);
					_buildingSetPositionMethod = buildingType.GetMethod("SetPosition",
						new Type[] { typeof(Vector3) });
					_buildingRotateMethod = buildingType.GetMethod("Rotate",
						new Type[] { typeof(int) });
				}

				Debug.Log("[ATSAccessibility] Cached building placement types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Building placement type caching failed: {ex.Message}");
			}

			_buildingPlacementTypesCached = true;
		}

		/// <summary>
		/// Check if a building can be placed at its current position.
		/// </summary>
		public static bool CanPlaceBuilding(object building) {
			EnsureBuildingPlacementTypes();
			var constructionService = GetConstructionService();
			if (constructionService == null || _csCanPlaceOnGridMethod == null || building == null)
				return false;

			try {
				return (bool)_csCanPlaceOnGridMethod.Invoke(constructionService, new object[] { building });
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Set a building's position.
		/// </summary>
		public static void SetBuildingPosition(object building, Vector2Int gridPos) {
			EnsureBuildingPlacementTypes();
			if (building == null || _buildingSetPositionMethod == null) return;

			try {
				// Convert grid position to world position
				Vector3 worldPos = new Vector3(gridPos.x, 0, gridPos.y);
				_buildingSetPositionMethod.Invoke(building, new object[] { worldPos });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetBuildingPosition failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Rotate a building to a specific rotation value (0-3).
		/// </summary>
		public static void RotateBuilding(object building, int rotation) {
			EnsureBuildingPlacementTypes();
			if (building == null || _buildingRotateMethod == null) return;

			try {
				_buildingRotateMethod.Invoke(building, new object[] { rotation });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RotateBuilding failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get the current rotation of a building (0-3).
		/// </summary>
		public static int GetBuildingRotation(object building) {
			EnsureBuildingPlacementTypes();
			if (building == null || _buildingRotationProperty == null) return 0;

			try {
				return (int)_buildingRotationProperty.GetValue(building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Finalize building placement after setting position.
		/// This registers the building, plays sounds, and starts construction.
		/// </summary>
		public static void FinalizeBuildingPlacement(object building) {
			EnsureBuildingPlacementTypes();
			if (building == null || _buildingManualPlacingFinishedMethod == null) return;

			try {
				_buildingManualPlacingFinishedMethod.Invoke(building, null);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] FinalizeBuildingPlacement failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Remove a building from the game.
		/// </summary>
		public static void RemoveBuilding(object building, bool refund = true) {
			EnsureBuildingPlacementTypes();
			if (building == null || _buildingRemoveMethod == null) return;

			try {
				_buildingRemoveMethod.Invoke(building, new object[] { refund });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RemoveBuilding failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get building at a specific map position.
		/// Returns null if no building at that position.
		/// </summary>
		public static object GetBuildingAtPosition(int x, int y) {
			var obj = GameReflection.GetObjectOn(x, y);
			if (obj == null) return null;

			// Check if it's a Building type
			var buildingType = GameReflection.GameAssembly?.GetType("Eremite.Buildings.Building");
			if (buildingType != null && buildingType.IsInstanceOfType(obj)) {
				return obj;
			}

			return null;
		}

		/// <summary>
		/// Check if a building is unfinished (still under construction).
		/// </summary>
		public static bool IsBuildingUnfinished(object building) {
			if (building == null) return false;

			try {
				// Get BuildingState property
				var stateProperty = building.GetType().GetProperty("BuildingState",
					BindingFlags.Public | BindingFlags.Instance);
				if (stateProperty == null) return false;

				var state = stateProperty.GetValue(building);
				if (state == null) return false;

				// Get finished field from state
				var finishedField = state.GetType().GetField("finished",
					BindingFlags.Public | BindingFlags.Instance);
				if (finishedField == null) return false;

				return !(bool)finishedField.GetValue(state);
			} catch {
				return false;
			}
		}


		// ========================================
		// CONSTRUCTION PROGRESS REFLECTION
		// ========================================

		private static FieldInfo _buildingProgressField = null;
		private static FieldInfo _deliveredGoodsField = null;  // BuildingState.deliveredGoods (cached locally)
		private static FieldInfo _constructionGoodsField = null;  // goods dict on GoodsCollection base
		private static MethodInfo _csGetConstructionCostForMethod = null;
		private static FieldInfo _goodStructNameField = null;
		private static FieldInfo _goodStructAmountField = null;
		private static bool _constructionTypesCached = false;

		private static void EnsureConstructionTypes() {
			if (_constructionTypesCached) return;

			if (GameReflection.GameAssembly == null) {
				_constructionTypesCached = true;
				return;
			}

			try {
				// BuildingState fields
				var buildingStateType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingState");
				if (buildingStateType != null) {
					_buildingProgressField = buildingStateType.GetField("buildingProgress", GameReflection.PublicInstance);
					_deliveredGoodsField = buildingStateType.GetField("deliveredGoods", GameReflection.PublicInstance);
				}

				// GoodsCollection.goods (public, base class) for delivered amounts
				var goodsCollectionType = GameReflection.GameAssembly.GetType("Eremite.GoodsCollection");
				if (goodsCollectionType != null) {
					_constructionGoodsField = goodsCollectionType.GetField("goods", GameReflection.PublicInstance);
				}

				// ConstructionService.GetConstructionCostFor(BuildingModel) for required amounts
				var constructionServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IConstructionService");
				var buildingModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.BuildingModel");
				if (constructionServiceType != null && buildingModelType != null) {
					_csGetConstructionCostForMethod = constructionServiceType.GetMethod("GetConstructionCostFor",
						new Type[] { buildingModelType });
				}

				// Good struct fields (name, amount)
				var goodType = GameReflection.GameAssembly.GetType("Eremite.Model.Good");
				if (goodType != null) {
					_goodStructNameField = goodType.GetField("name", GameReflection.PublicInstance);
					_goodStructAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
				}

				Debug.Log("[ATSAccessibility] Cached construction types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Construction type caching failed: {ex.Message}");
			}

			_constructionTypesCached = true;
		}

		/// <summary>
		/// Get building construction progress (0-1 float).
		/// </summary>
		public static float GetBuildingProgress(object building) {
			if (building == null) return 0f;
			EnsureConstructionTypes();

			try {
				var stateProperty = building.GetType().GetProperty("BuildingState", GameReflection.PublicInstance);
				if (stateProperty == null) return 0f;

				var state = stateProperty.GetValue(building);
				if (state == null || _buildingProgressField == null) return 0f;

				return (float)_buildingProgressField.GetValue(state);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get construction materials with delivered and required amounts.
		/// Uses ConstructionService.GetConstructionCostFor (same as game UI) for required amounts.
		/// Returns list of (displayName, delivered, required).
		/// </summary>
		public static List<(string name, int delivered, int required)> GetConstructionMaterials(object building) {
			if (building == null) return null;
			EnsureConstructionTypes();

			try {
				// Get required amounts from ConstructionService (matches game UI)
				var buildingModel = GetBuildingModel(building);
				var constructionService = GetConstructionService();
				if (buildingModel == null || constructionService == null ||
					_csGetConstructionCostForMethod == null ||
					_goodStructNameField == null || _goodStructAmountField == null)
					return null;

				var requiredGoods = _csGetConstructionCostForMethod.Invoke(
					constructionService, new[] { buildingModel }) as Array;
				if (requiredGoods == null || requiredGoods.Length == 0) return null;

				// Get delivered amounts from BuildingState.deliveredGoods.goods dict
				Dictionary<string, int> deliveredDict = null;
				var stateProperty = building.GetType().GetProperty("BuildingState", GameReflection.PublicInstance);
				if (stateProperty != null) {
					var state = stateProperty.GetValue(building);
					if (state != null && _deliveredGoodsField != null) {
						var deliveredGoods = _deliveredGoodsField.GetValue(state);
						if (deliveredGoods != null && _constructionGoodsField != null) {
							deliveredDict = _constructionGoodsField.GetValue(deliveredGoods)
								as Dictionary<string, int>;
						}
					}
				}

				var result = new List<(string name, int delivered, int required)>();
				foreach (var good in requiredGoods) {
					if (good == null) continue;

					string goodName = _goodStructNameField.GetValue(good) as string;
					int required = (int)_goodStructAmountField.GetValue(good);
					if (string.IsNullOrEmpty(goodName) || required <= 0) continue;

					int delivered = 0;
					if (deliveredDict != null && deliveredDict.ContainsKey(goodName))
						delivered = deliveredDict[goodName];

					string displayName = GameReflection.GetGoodDisplayName(goodName);
					result.Add((displayName, delivered, required));
				}

				return result.Count > 0 ? result : null;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetConstructionMaterials failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Check if an object from GetObjectOn is a Building (not a resource or field).
		/// </summary>
		public static bool IsBuilding(object obj) {
			if (obj == null) return false;

			var buildingType = GameReflection.GameAssembly?.GetType("Eremite.Buildings.Building");
			return buildingType != null && buildingType.IsInstanceOfType(obj);
		}

		/// <summary>
		/// Check if an object from GetObjectOn is a removable resource node
		/// (ResourceDeposit, Lake, or Spring — types the game's destruction mode supports).
		/// NaturalResource and Ore are NOT removable via destruction mode.
		/// </summary>
		public static bool IsRemovableResource(object obj) {
			if (obj == null) return false;
			var typeName = obj.GetType().Name;
			return typeName == "ResourceDeposit" || typeName == "Lake" || typeName == "Spring";
		}

		/// <summary>
		/// Remove a resource node (ResourceDeposit, Lake, or Spring) via reflection.
		/// Returns true if removal succeeded.
		/// </summary>
		public static bool RemoveResourceNode(object resource) {
			if (resource == null) return false;

			try {
				var typeName = resource.GetType().Name;
				MethodInfo removeMethod;

				if (typeName == "Spring") {
					// Spring.Remove(float time) — pass 0f for immediate removal
					removeMethod = resource.GetType().GetMethod("Remove", GameReflection.PublicInstance, null, new[] { typeof(float) }, null);
					if (removeMethod == null) return false;
					removeMethod.Invoke(resource, new object[] { 0f });
				} else if (typeName == "ResourceDeposit" || typeName == "Lake") {
					// ResourceDeposit.Remove() and Lake.Remove() — no params
					removeMethod = resource.GetType().GetMethod("Remove", GameReflection.PublicInstance, null, Type.EmptyTypes, null);
					if (removeMethod == null) return false;
					removeMethod.Invoke(resource, null);
				} else {
					return false;
				}

				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RemoveResourceNode failed: {ex.Message}");
				return false;
			}
		}

		// Cached reflection for PickBuilding
		private static PropertyInfo _modeServiceProperty = null;
		private static PropertyInfo _destructionModeProperty = null;
		private static PropertyInfo _harvestModeProperty = null;
		private static MethodInfo _buildingPickMethod = null;
		private static bool _pickBuildingCached = false;

		/// <summary>
		/// Pick/select a building to open its panel.
		/// Returns true if successful, false if in a mode that prevents picking
		/// or if the building cannot be picked.
		/// </summary>
		public static bool PickBuilding(object building) {
			if (building == null) return false;
			if (!IsBuilding(building)) return false;

			try {
				// Cache reflection info
				if (!_pickBuildingCached) {
					CachePickBuildingReflection();
				}

				// Check if in destruction mode or harvest mode (don't pick in these modes)
				if (IsInDestructionMode() || IsInHarvestMode()) {
					Debug.Log("[ATSAccessibility] Cannot pick building: in destruction or harvest mode");
					return false;
				}

				// Get or cache the Pick method
				if (_buildingPickMethod == null) {
					var buildingType = GameReflection.GameAssembly?.GetType("Eremite.Buildings.Building");
					if (buildingType != null) {
						_buildingPickMethod = buildingType.GetMethod("Pick",
							BindingFlags.Public | BindingFlags.Instance);
					}
				}

				if (_buildingPickMethod == null) {
					Debug.LogError("[ATSAccessibility] Could not find Building.Pick method");
					return false;
				}

				// Call Pick() on the building
				_buildingPickMethod.Invoke(building, null);
				Debug.Log("[ATSAccessibility] Picked building successfully");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PickBuilding failed: {ex.Message}");
				return false;
			}
		}

		private static void CachePickBuildingReflection() {
			try {
				// Get ModeService from GameServices
				var gameServicesType = GameReflection.GameAssembly?.GetType("Eremite.Services.GameServices");
				if (gameServicesType != null) {
					_modeServiceProperty = gameServicesType.GetProperty("ModeService",
						BindingFlags.Public | BindingFlags.Static);
				}

				// Get mode properties from ModeService type
				var modeServiceType = GameReflection.GameAssembly?.GetType("Eremite.Services.ModeService");
				if (modeServiceType != null) {
					_destructionModeProperty = modeServiceType.GetProperty("BuildingDestructionMode",
						BindingFlags.Public | BindingFlags.Instance);
					_harvestModeProperty = modeServiceType.GetProperty("HarvestMode",
						BindingFlags.Public | BindingFlags.Instance);
				}

				_pickBuildingCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CachePickBuildingReflection failed: {ex.Message}");
				_pickBuildingCached = true; // Don't retry
			}
		}

		private static bool IsInDestructionMode() {
			try {
				if (_modeServiceProperty == null) return false;

				var modeService = _modeServiceProperty.GetValue(null);
				if (modeService == null || _destructionModeProperty == null) return false;

				var destructionMode = _destructionModeProperty.GetValue(modeService);
				if (destructionMode == null) return false;

				// It's a ReactiveProperty<bool>, get the Value
				var valueProperty = destructionMode.GetType().GetProperty("Value");
				if (valueProperty == null) return false;

				return (bool)valueProperty.GetValue(destructionMode);
			} catch {
				return false;
			}
		}

		private static bool IsInHarvestMode() {
			try {
				if (_modeServiceProperty == null) return false;

				var modeService = _modeServiceProperty.GetValue(null);
				if (modeService == null || _harvestModeProperty == null) return false;

				var harvestMode = _harvestModeProperty.GetValue(modeService);
				if (harvestMode == null) return false;

				// It's a ReactiveProperty<bool>, get the Value
				var valueProperty = harvestMode.GetType().GetProperty("Value");
				if (valueProperty == null) return false;

				return (bool)valueProperty.GetValue(harvestMode);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the entrance tile coordinates for a building.
		/// Returns null if the building has no entrance or if it can't be determined.
		/// </summary>
		public static Vector2Int? GetBuildingEntranceTile(object building) {
			if (building == null) return null;

			try {
				// Get Entrance property (Vector3 world position)
				var entranceProperty = building.GetType().GetProperty("Entrance",
					BindingFlags.Public | BindingFlags.Instance);
				if (entranceProperty == null) return null;

				var entrancePos = (Vector3)entranceProperty.GetValue(building);

				// Convert world position to tile coordinates
				return new Vector2Int(
					Mathf.FloorToInt(entrancePos.x),
					Mathf.FloorToInt(entrancePos.z)
				);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if a building should show its entrance (has meaningful entrance for gameplay).
		/// </summary>
		public static bool GetBuildingShouldShowEntrance(object building) {
			if (building == null) return false;

			try {
				// ShouldShowEntrance is a protected virtual property
				var shouldShowProp = building.GetType().GetProperty("ShouldShowEntrance",
					BindingFlags.NonPublic | BindingFlags.Instance);
				if (shouldShowProp != null) {
					return (bool)shouldShowProp.GetValue(building);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingShouldShowEntrance failed: {ex.Message}"); }

			return false;
		}

		/// <summary>
		/// Check if a building instance can be rotated.
		/// </summary>
		public static bool CanRotateBuilding(object building) {
			if (building == null) return false;

			try {
				// Get BuildingModel property
				var modelProp = building.GetType().GetProperty("BuildingModel",
					BindingFlags.Public | BindingFlags.Instance);
				if (modelProp == null) return false;

				var model = modelProp.GetValue(building);
				return CanRotateBuildingModel(model);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] CanRotateBuilding failed: {ex.Message}"); }

			return false;
		}

		/// <summary>
		/// Check if a building model allows rotation.
		/// </summary>
		public static bool CanRotateBuildingModel(object buildingModel) {
			if (buildingModel == null) return false;

			try {
				// Get canRotate field from model
				var canRotateField = buildingModel.GetType().GetField("canRotate",
					BindingFlags.Public | BindingFlags.Instance);
				if (canRotateField != null) {
					return (bool)canRotateField.GetValue(buildingModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] CanRotateBuildingModel failed: {ex.Message}"); }

			return false;
		}

		/// <summary>
		/// Check if a building can be moved (required for rotation).
		/// </summary>
		public static bool CanMovePlacedBuilding(object building) {
			if (building == null) return false;

			try {
				var constructionService = GetConstructionService();
				if (constructionService == null) return false;

				// Get CanBeMoved method (takes Building parameter)
				var canMoveMethod = constructionService.GetType().GetMethod("CanBeMoved",
					BindingFlags.Public | BindingFlags.Instance,
					null, new Type[] { building.GetType() }, null);

				// Try with base Building type if exact type doesn't match
				if (canMoveMethod == null) {
					var buildingType = GameReflection.GameAssembly?.GetType("Eremite.Buildings.Building");
					if (buildingType != null) {
						canMoveMethod = constructionService.GetType().GetMethod("CanBeMoved",
							BindingFlags.Public | BindingFlags.Instance,
							null, new Type[] { buildingType }, null);
					}
				}

				if (canMoveMethod == null) {
					Debug.LogWarning("[ATSAccessibility] CanBeMoved method not found");
					return false; // Don't allow if method not found
				}

				return (bool)canMoveMethod.Invoke(constructionService, new object[] { building });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CanMovePlacedBuilding failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if moving this building has a resource cost.
		/// </summary>
		public static bool HasMovingCost(object building) {
			if (building == null) return false;
			try {
				var constructionService = GetConstructionService();
				if (constructionService == null) return false;

				var method = constructionService.GetType().GetMethod("HasMovingCost",
					BindingFlags.Public | BindingFlags.Instance);
				if (method == null) return false;

				return (bool)method.Invoke(constructionService, new object[] { building });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] HasMovingCost failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if the player can afford to move this building.
		/// </summary>
		public static bool CanAffordMove(object building) {
			if (building == null) return false;
			try {
				var constructionService = GetConstructionService();
				if (constructionService == null) return true;

				var method = constructionService.GetType().GetMethod("CanAffordMove",
					BindingFlags.Public | BindingFlags.Instance);
				if (method == null) return true;

				return (bool)method.Invoke(constructionService, new object[] { building });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CanAffordMove failed: {ex.Message}");
				return true;
			}
		}

		/// <summary>
		/// Get the moving cost display name and amount for a building.
		/// Returns null if no cost.
		/// </summary>
		public static (string displayName, int amount)? GetMovingCostInfo(object building) {
			if (building == null) return null;
			try {
				var model = GetBuildingModel(building);
				if (model == null) return null;

				var movingCostField = model.GetType().GetField("movingCost",
					BindingFlags.Public | BindingFlags.Instance);
				if (movingCostField == null) return null;

				var goodRef = movingCostField.GetValue(model);
				if (goodRef == null) return null;

				var amountField = goodRef.GetType().GetField("amount", BindingFlags.Public | BindingFlags.Instance);
				int amount = (int)(amountField?.GetValue(goodRef) ?? 0);
				if (amount <= 0) return null;

				var displayNameProp = goodRef.GetType().GetProperty("DisplayName",
					BindingFlags.Public | BindingFlags.Instance);
				string displayName = displayNameProp?.GetValue(goodRef) as string ?? "Unknown";

				return (displayName, amount);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMovingCostInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Pay the moving cost for a building. Call before moving.
		/// Returns true if cost was paid (or no cost needed).
		/// </summary>
		public static bool PayForMoving(object building) {
			if (building == null) return false;
			if (!HasMovingCost(building)) return true;  // No cost, success

			try {
				var model = GetBuildingModel(building);
				if (model == null) return false;

				var movingCostField = model.GetType().GetField("movingCost",
					BindingFlags.Public | BindingFlags.Instance);
				var goodRef = movingCostField?.GetValue(model);
				if (goodRef == null) return false;

				// Get Good via ToGood()
				var toGoodMethod = goodRef.GetType().GetMethod("ToGood", BindingFlags.Public | BindingFlags.Instance);
				if (toGoodMethod == null) return false;
				object good = toGoodMethod.Invoke(goodRef, null);

				// Get StorageService
				var storageService = GameReflection.GetStorageService();
				if (storageService == null) return false;

				// Get StorageOperationType.BuildingMove enum value
				var opType = GameReflection.GameAssembly.GetType("Eremite.Model.StorageOperationType");
				if (opType == null) return false;
				object buildingMoveValue = Enum.Parse(opType, "BuildingMove");

				// Call Remove(Good, StorageOperationType)
				var goodType = good.GetType();
				var removeMethod = storageService.GetType().GetMethod("Remove",
					BindingFlags.Public | BindingFlags.Instance,
					null, new Type[] { goodType, opType }, null);
				if (removeMethod == null) return false;

				removeMethod.Invoke(storageService, new object[] { good, buildingMoveValue });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PayForMoving failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Refund the moving cost for a building. Call on cancel.
		/// </summary>
		public static void RefundMoving(object building) {
			if (building == null) return;

			try {
				var model = GetBuildingModel(building);
				if (model == null) return;

				var movingCostField = model.GetType().GetField("movingCost",
					BindingFlags.Public | BindingFlags.Instance);
				var goodRef = movingCostField?.GetValue(model);
				if (goodRef == null) return;

				var toGoodMethod = goodRef.GetType().GetMethod("ToGood", BindingFlags.Public | BindingFlags.Instance);
				if (toGoodMethod == null) return;
				object good = toGoodMethod.Invoke(goodRef, null);

				var storageService = GameReflection.GetStorageService();
				if (storageService == null) return;

				var opType = GameReflection.GameAssembly.GetType("Eremite.Model.StorageOperationType");
				if (opType == null) return;
				object buildingRefundValue = Enum.Parse(opType, "BuildingRefund");

				var goodType = good.GetType();
				var storeMethod = storageService.GetType().GetMethod("Store",
					BindingFlags.Public | BindingFlags.Instance,
					null, new Type[] { goodType, opType }, null);

				storeMethod?.Invoke(storageService, new object[] { good, buildingRefundValue });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RefundMoving failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a placed building can be rotated in place.
		/// Uses the game's ConstructionService.CanBeRotatedInPlace check.
		/// </summary>
		public static bool CanRotatePlacedBuilding(object building) {
			if (building == null) return false;

			try {
				var constructionService = GetConstructionService();
				if (constructionService == null) return false;

				// Get CanBeRotatedInPlace method
				var canRotateMethod = constructionService.GetType().GetMethod("CanBeRotatedInPlace",
					BindingFlags.Public | BindingFlags.Instance);
				if (canRotateMethod == null) {
					Debug.LogWarning("[ATSAccessibility] CanBeRotatedInPlace method not found");
					return false; // Don't allow if method not found
				}

				return (bool)canRotateMethod.Invoke(constructionService, new object[] { building });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CanRotatePlacedBuilding failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Rotate a placed building and return the new rotation (0-3).
		/// Properly updates the map grid by removing and re-placing the building.
		/// Call CanMovePlacedBuilding and CanRotatePlacedBuilding first to check validity.
		/// Returns -1 if rotation failed.
		/// </summary>
		public static int RotatePlacedBuilding(object building) {
			if (building == null) return -1;

			try {
				// Get MapService for grid operations
				var mapService = GameReflection.GetMapService();
				if (mapService == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: MapService not found");
					return -1;
				}

				// Get RemoveFromGrid and PlaceOnGrid methods
				var removeMethod = mapService.GetType().GetMethod("RemoveFromGrid",
					BindingFlags.Public | BindingFlags.Instance);
				var placeMethod = mapService.GetType().GetMethod("PlaceOnGrid",
					BindingFlags.Public | BindingFlags.Instance);

				if (removeMethod == null || placeMethod == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: Grid methods not found");
					return -1;
				}

				// Get the Rotate method
				var rotateMethod = building.GetType().GetMethod("Rotate",
					BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				if (rotateMethod == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: Rotate method not found");
					return -1;
				}

				// 1. Remove from grid (clears old footprint)
				removeMethod.Invoke(mapService, new object[] { building });

				// 2. Rotate the building
				rotateMethod.Invoke(building, null);

				// 3. Re-place on grid (sets new footprint)
				placeMethod.Invoke(mapService, new object[] { building });

				// Get the new rotation value
				var rotationProp = building.GetType().GetProperty("Rotation",
					BindingFlags.Public | BindingFlags.Instance);
				if (rotationProp != null) {
					return (int)rotationProp.GetValue(building);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RotatePlacedBuilding failed: {ex.Message}");
			}

			return -1;
		}

		/// <summary>
		/// Rotate a placed building in a specific direction and return the new rotation (0-3).
		/// direction: -1 for clockwise, +1 for counterclockwise (rotation values 0=N,1=W,2=S,3=E).
		/// Call CanMovePlacedBuilding and CanRotatePlacedBuilding first to check validity.
		/// Returns -1 if rotation failed.
		/// </summary>
		public static int RotatePlacedBuildingDirection(object building, int direction) {
			if (building == null) return -1;

			try {
				// Get MapService for grid operations
				var mapService = GameReflection.GetMapService();
				if (mapService == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuildingDirection: MapService not found");
					return -1;
				}

				// Get RemoveFromGrid and PlaceOnGrid methods
				var removeMethod = mapService.GetType().GetMethod("RemoveFromGrid",
					BindingFlags.Public | BindingFlags.Instance);
				var placeMethod = mapService.GetType().GetMethod("PlaceOnGrid",
					BindingFlags.Public | BindingFlags.Instance);

				if (removeMethod == null || placeMethod == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuildingDirection: Grid methods not found");
					return -1;
				}

				// Read current rotation
				var rotationProp = building.GetType().GetProperty("Rotation",
					BindingFlags.Public | BindingFlags.Instance);
				if (rotationProp == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuildingDirection: Rotation property not found");
					return -1;
				}

				int current = (int)rotationProp.GetValue(building);
				int newRotation = (current + direction + 4) % 4;

				// Use the cached parameterized Rotate(int) method
				EnsureBuildingPlacementTypes();
				if (_buildingRotateMethod == null) {
					Debug.LogError("[ATSAccessibility] RotatePlacedBuildingDirection: Rotate method not found");
					return -1;
				}

				// 1. Remove from grid (clears old footprint)
				removeMethod.Invoke(mapService, new object[] { building });

				// 2. Rotate the building to the computed rotation
				_buildingRotateMethod.Invoke(building, new object[] { newRotation });

				// 3. Re-place on grid (sets new footprint)
				placeMethod.Invoke(mapService, new object[] { building });

				// Rotate(int) doesn't play a sound, so play it explicitly
				ATSAccessibility.Utils.SoundManager.PlayBuildingRotated();

				return newRotation;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RotatePlacedBuildingDirection failed: {ex.Message}");
			}

			return -1;
		}

		/// <summary>
		/// Get a building's grid position.
		/// Returns the building's Field property as Vector2Int.
		/// </summary>
		public static Vector2Int GetBuildingGridPosition(object building) {
			if (building == null) return Vector2Int.zero;

			try {
				// _buildingFieldProperty may already be cached from Ancient Hearth code
				if (_buildingFieldProperty == null) {
					_buildingFieldProperty = building.GetType().GetProperty("Field",
						BindingFlags.Public | BindingFlags.Instance);
				}

				if (_buildingFieldProperty != null) {
					var field = _buildingFieldProperty.GetValue(building);
					if (field is Vector2Int pos) {
						return pos;
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetBuildingGridPosition failed: {ex.Message}");
			}

			return Vector2Int.zero;
		}

		/// <summary>
		/// Get the building model (template) from a placed building instance.
		/// Returns the BuildingModel that was used to create this building.
		/// </summary>
		public static object GetBuildingModel(object building) {
			if (building == null) return null;

			try {
				// Building.BuildingModel property returns the BuildingModel
				var modelProperty = building.GetType().GetProperty("BuildingModel",
					BindingFlags.Public | BindingFlags.Instance);

				if (modelProperty != null) {
					return modelProperty.GetValue(building);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetBuildingModel failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Lift a building from the map grid without destroying it.
		/// This removes the building's footprint from the grid but keeps the object.
		/// Call PlaceBuildingOnGrid to put it back.
		/// </summary>
		public static void LiftBuilding(object building) {
			if (building == null) return;

			try {
				var mapService = GameReflection.GetMapService();
				if (mapService == null) {
					Debug.LogError("[ATSAccessibility] LiftBuilding: MapService not found");
					return;
				}

				var removeMethod = mapService.GetType().GetMethod("RemoveFromGrid",
					BindingFlags.Public | BindingFlags.Instance);

				if (removeMethod == null) {
					Debug.LogError("[ATSAccessibility] LiftBuilding: RemoveFromGrid method not found");
					return;
				}

				removeMethod.Invoke(mapService, new object[] { building });
				Debug.Log("[ATSAccessibility] Building lifted from grid");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] LiftBuilding failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Place a building on the map grid at its current position.
		/// Use after LiftBuilding and SetBuildingPosition to move a building.
		/// </summary>
		public static void PlaceBuildingOnGrid(object building) {
			if (building == null) return;

			try {
				var mapService = GameReflection.GetMapService();
				if (mapService == null) {
					Debug.LogError("[ATSAccessibility] PlaceBuildingOnGrid: MapService not found");
					return;
				}

				var placeMethod = mapService.GetType().GetMethod("PlaceOnGrid",
					BindingFlags.Public | BindingFlags.Instance);

				if (placeMethod == null) {
					Debug.LogError("[ATSAccessibility] PlaceBuildingOnGrid: PlaceOnGrid method not found");
					return;
				}

				placeMethod.Invoke(mapService, new object[] { building });
				Debug.Log("[ATSAccessibility] Building placed on grid");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PlaceBuildingOnGrid failed: {ex.Message}");
			}
		}


		// ========================================
		// META PERK UNLOCK CHECKS
		// ========================================

		// Cached reflection for MetaPerksService
		private static PropertyInfo _mbMetaPerksServiceProp = null;
		private static MethodInfo _areTradeRoutesEnabledMethod = null;
		private static MethodInfo _isConsumptionControlEnabledMethod = null;
		private static bool _metaPerksReflectionCached = false;

		// Cached reflection for MetaStateService.Perks (for fields not exposed via MetaPerksService)
		private static PropertyInfo _mbMetaStateServiceProp = null;
		private static PropertyInfo _mssPerksProperty = null;
		private static FieldInfo _perksReputationRewardsRerollEnabledField = null;
		private static FieldInfo _perksBonusFarmAreaField = null;
		private static bool _metaStateReflectionCached = false;

		private static void EnsureMetaPerksReflectionCached() {
			if (_metaPerksReflectionCached) return;
			_metaPerksReflectionCached = true;

			try {
				// Get MB type and MetaPerksService property (protected static)
				var mbType = GameReflection.GameAssembly?.GetType("Eremite.MB");
				if (mbType != null) {
					_mbMetaPerksServiceProp = mbType.GetProperty("MetaPerksService",
						BindingFlags.NonPublic | BindingFlags.Static);
				}

				// Get IMetaPerksService methods
				var metaPerksServiceType = GameReflection.GameAssembly?.GetType("Eremite.Services.IMetaPerksService");
				if (metaPerksServiceType != null) {
					_areTradeRoutesEnabledMethod = metaPerksServiceType.GetMethod("AreTradeRoutesEnabled");
					_isConsumptionControlEnabledMethod = metaPerksServiceType.GetMethod("IsConsumptionControlEnabled");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to cache MetaPerksService reflection: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if Trade Routes feature is unlocked via meta progression.
		/// </summary>
		public static bool AreTradeRoutesUnlocked() {
			EnsureMetaPerksReflectionCached();

			if (_mbMetaPerksServiceProp == null || _areTradeRoutesEnabledMethod == null)
				return true; // Assume unlocked if reflection fails

			try {
				var metaPerksService = _mbMetaPerksServiceProp.GetValue(null);
				if (metaPerksService == null) return true;

				var result = _areTradeRoutesEnabledMethod.Invoke(metaPerksService, null);
				return result is bool enabled && enabled;
			} catch {
				return true; // Assume unlocked on error
			}
		}

		/// <summary>
		/// Check if Consumption Control feature is unlocked via meta progression.
		/// </summary>
		public static bool IsConsumptionControlUnlocked() {
			EnsureMetaPerksReflectionCached();

			if (_mbMetaPerksServiceProp == null || _isConsumptionControlEnabledMethod == null)
				return true; // Assume unlocked if reflection fails

			try {
				var metaPerksService = _mbMetaPerksServiceProp.GetValue(null);
				if (metaPerksService == null) return true;

				var result = _isConsumptionControlEnabledMethod.Invoke(metaPerksService, null);
				return result is bool enabled && enabled;
			} catch {
				return true; // Assume unlocked on error
			}
		}

		private static void EnsureMetaStateReflectionCached() {
			if (_metaStateReflectionCached) return;
			_metaStateReflectionCached = true;

			try {
				// Get MB type and MetaStateService property (protected static)
				var mbType = GameReflection.GameAssembly?.GetType("Eremite.MB");
				if (mbType != null) {
					_mbMetaStateServiceProp = mbType.GetProperty("MetaStateService",
						BindingFlags.NonPublic | BindingFlags.Static);
				}

				// Get IMetaStateService.Perks property
				var metaStateServiceType = GameReflection.GameAssembly?.GetType("Eremite.Services.IMetaStateService");
				if (metaStateServiceType != null) {
					_mssPerksProperty = metaStateServiceType.GetProperty("Perks",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get MetaPerksState fields
				var metaPerksStateType = GameReflection.GameAssembly?.GetType("Eremite.Model.State.MetaPerksState");
				if (metaPerksStateType != null) {
					_perksReputationRewardsRerollEnabledField = metaPerksStateType.GetField("reputationRewardsRerollEnabled",
						BindingFlags.Public | BindingFlags.Instance);
					_perksBonusFarmAreaField = metaPerksStateType.GetField("bonusFarmArea",
						BindingFlags.Public | BindingFlags.Instance);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to cache MetaStateService reflection: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if Blueprint Reroll feature is unlocked via meta progression.
		/// </summary>
		public static bool IsBlueprintRerollUnlocked() {
			EnsureMetaStateReflectionCached();

			if (_mbMetaStateServiceProp == null || _mssPerksProperty == null || _perksReputationRewardsRerollEnabledField == null)
				return true; // Assume unlocked if reflection fails

			try {
				var metaStateService = _mbMetaStateServiceProp.GetValue(null);
				if (metaStateService == null) return true;

				var perks = _mssPerksProperty.GetValue(metaStateService);
				if (perks == null) return true;

				var result = _perksReputationRewardsRerollEnabledField.GetValue(perks);
				return result is bool enabled && enabled;
			} catch {
				return true; // Assume unlocked on error
			}
		}

		/// <summary>
		/// Get the bonus farm area from meta progression (extends farm work area).
		/// </summary>
		public static int GetBonusFarmArea() {
			EnsureMetaStateReflectionCached();

			if (_mbMetaStateServiceProp == null || _mssPerksProperty == null || _perksBonusFarmAreaField == null)
				return 0; // No bonus if reflection fails

			try {
				var metaStateService = _mbMetaStateServiceProp.GetValue(null);
				if (metaStateService == null) return 0;

				var perks = _mssPerksProperty.GetValue(metaStateService);
				if (perks == null) return 0;

				var result = _perksBonusFarmAreaField.GetValue(perks);
				return result is int bonus ? bonus : 0;
			} catch {
				return 0;
			}
		}

		// ========================================
		// BUILDING RANGE INFO (for 'd' key)
		// ========================================

		// Cached types for building type checks
		private static Type _campModelType = null;
		private static Type _gathererHutModelType = null;
		private static Type _fishingHutModelType = null;
		private static Type _hearthModelType = null;
		private static Type _workshopModelType = null;
		private static Type _farmModelType = null;
		private static Type _farmfieldType = null;
		private static bool _rangeInfoTypesCached = false;

		// Cached fields for getting building data
		private static FieldInfo _campRecipesField = null;
		private static FieldInfo _campMaxDistanceField = null;
		private static FieldInfo _gathererHutRecipesField = null;
		private static FieldInfo _gathererHutMaxDistanceField = null;
		private static FieldInfo _fishingHutRecipesField = null;
		private static FieldInfo _fishingHutMaxDistanceField = null;
		private static FieldInfo _hearthHubRangeField = null;

		// Cached fields for recipe goods
		private static FieldInfo _campRecipeRefGoodField = null;
		private static FieldInfo _gathererHutRecipeRefGoodField = null;
		private static FieldInfo _fishingHutRecipeRefGoodField = null;
		private static FieldInfo _goodRefNameField = null;

		// Cached properties for services
		private static PropertyInfo _resourcesAvailableProperty = null;
		private static PropertyInfo _depositsAvailableProperty = null;
		private static PropertyInfo _lakesAvailableProperty = null;
		private static PropertyInfo _effectsServiceProperty = null;
		private static MethodInfo _effectsGetHearthRangeMethod = null;

		private static void EnsureRangeInfoTypes() {
			if (_rangeInfoTypesCached) return;

			if (GameReflection.GameAssembly == null) {
				_rangeInfoTypesCached = true;
				return;
			}

			try {
				// Cache building model types
				_campModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.CampModel");
				_gathererHutModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.GathererHutModel");
				_fishingHutModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.FishingHutModel");
				_hearthModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.HearthModel");
				_workshopModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.WorkshopModel");
				_farmModelType = GameReflection.GameAssembly.GetType("Eremite.Buildings.FarmModel");
				_farmfieldType = GameReflection.GameAssembly.GetType("Eremite.Buildings.Farmfield");

				// Cache CampModel fields
				if (_campModelType != null) {
					_campRecipesField = _campModelType.GetField("recipes", GameReflection.PublicInstance);
					_campMaxDistanceField = _campModelType.GetField("maxDistance", GameReflection.PublicInstance);
				}

				// Cache GathererHutModel fields
				if (_gathererHutModelType != null) {
					_gathererHutRecipesField = _gathererHutModelType.GetField("recipes", GameReflection.PublicInstance);
					_gathererHutMaxDistanceField = _gathererHutModelType.GetField("maxDistance", GameReflection.PublicInstance);
				}

				// Cache FishingHutModel fields
				if (_fishingHutModelType != null) {
					_fishingHutRecipesField = _fishingHutModelType.GetField("recipes", GameReflection.PublicInstance);
					_fishingHutMaxDistanceField = _fishingHutModelType.GetField("maxDistance", GameReflection.PublicInstance);
				}

				// Cache HearthModel fields
				if (_hearthModelType != null) {
					_hearthHubRangeField = _hearthModelType.GetField("hubRange", GameReflection.PublicInstance);
				}

				// Cache recipe refGood fields
				var campRecipeType = GameReflection.GameAssembly.GetType("Eremite.Buildings.CampRecipeModel");
				if (campRecipeType != null) {
					_campRecipeRefGoodField = campRecipeType.GetField("refGood", GameReflection.PublicInstance);
				}

				var gathererHutRecipeType = GameReflection.GameAssembly.GetType("Eremite.Buildings.GathererHutRecipeModel");
				if (gathererHutRecipeType != null) {
					_gathererHutRecipeRefGoodField = gathererHutRecipeType.GetField("refGood", GameReflection.PublicInstance);
				}

				var fishingHutRecipeType = GameReflection.GameAssembly.GetType("Eremite.Buildings.FishingHutRecipeModel");
				if (fishingHutRecipeType != null) {
					_fishingHutRecipeRefGoodField = fishingHutRecipeType.GetField("refGood", GameReflection.PublicInstance);
				}

				// Cache GoodRef Name field (note: we use property getter in GetGatheringBuildingGoodNames, not field)
				var goodRefType = GameReflection.GameAssembly.GetType("Eremite.Model.GoodRef");
				if (goodRefType != null) {
					// GoodRef has a Name property, not field - we access it dynamically
					_goodRefNameField = goodRefType.GetField("name", GameReflection.NonPublicInstance);
				}

				// Cache service properties for available resources
				var resourcesServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IResourcesService");
				if (resourcesServiceType != null) {
					_resourcesAvailableProperty = resourcesServiceType.GetProperty("AvailableResources", GameReflection.PublicInstance);
				}

				var depositsServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IDepositsService");
				if (depositsServiceType != null) {
					_depositsAvailableProperty = depositsServiceType.GetProperty("AvailableDeposits", GameReflection.PublicInstance);
				}

				var lakesServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.ILakesService");
				if (lakesServiceType != null) {
					_lakesAvailableProperty = lakesServiceType.GetProperty("AvailableLakes", GameReflection.PublicInstance);
				}

				// Cache EffectsService for hearth range
				var gameServicesType = GameReflection.GameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_effectsServiceProperty = gameServicesType.GetProperty("EffectsService", GameReflection.PublicInstance);
				}

				var effectsServiceType = GameReflection.GameAssembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_effectsGetHearthRangeMethod = effectsServiceType.GetMethod("GetHearthRange", GameReflection.PublicInstance);
				}

				Debug.Log("[ATSAccessibility] Cached range info types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Range info type caching failed: {ex.Message}");
			}

			_rangeInfoTypesCached = true;
		}

		/// <summary>
		/// Check if a building model is a Camp (harvests from NaturalResources).
		/// </summary>
		public static bool IsCampModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _campModelType != null && _campModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if a building model is a GathererHut (harvests from ResourceDeposits).
		/// </summary>
		public static bool IsGathererHutModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _gathererHutModelType != null && _gathererHutModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if a building model is a FishingHut (harvests from Lakes).
		/// </summary>
		public static bool IsFishingHutModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _fishingHutModelType != null && _fishingHutModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if a building model is a Hearth.
		/// </summary>
		public static bool IsHearthModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _hearthModelType != null && _hearthModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if a building model is a Workshop (production building).
		/// </summary>
		public static bool IsWorkshopModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _workshopModelType != null && _workshopModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if a building model is a Farm (agricultural building).
		/// </summary>
		public static bool IsFarmModel(object buildingModel) {
			if (buildingModel == null) return false;
			EnsureRangeInfoTypes();
			return _farmModelType != null && _farmModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Check if there's a finished farmfield at the given position.
		/// Uses BuildingsService.Farmfields collection.
		/// </summary>
		public static bool HasFarmfieldAt(int x, int y) {
			try {
				var buildingsService = GameReflection.GetBuildingsService();
				if (buildingsService == null) return false;

				// Get BuildingsService.Farmfields property
				var farmfieldsProperty = buildingsService.GetType().GetProperty("Farmfields",
					BindingFlags.Public | BindingFlags.Instance);
				if (farmfieldsProperty == null) return false;

				var farmfieldsDict = farmfieldsProperty.GetValue(buildingsService);
				if (farmfieldsDict == null) return false;

				// Iterate through farmfields to find one at this position
				var valuesProperty = farmfieldsDict.GetType().GetProperty("Values");
				if (valuesProperty == null) return false;

				var values = valuesProperty.GetValue(farmfieldsDict) as System.Collections.IEnumerable;
				if (values == null) return false;

				Vector2Int targetPos = new Vector2Int(x, y);

				foreach (var farmfield in values) {
					if (farmfield == null) continue;

					// Check if farmfield is finished
					var isFinishedMethod = farmfield.GetType().GetMethod("IsFinished",
						BindingFlags.Public | BindingFlags.Instance);
					if (isFinishedMethod != null) {
						var finished = isFinishedMethod.Invoke(farmfield, null);
						if (finished is bool isFinished && !isFinished)
							continue;
					}

					// Get farmfield's state.field position
					var stateField = farmfield.GetType().GetField("state",
						BindingFlags.Public | BindingFlags.Instance);
					if (stateField == null) continue;

					var state = stateField.GetValue(farmfield);
					if (state == null) continue;

					var fieldField = state.GetType().GetField("field",
						BindingFlags.Public | BindingFlags.Instance);
					if (fieldField == null) continue;

					var fieldPos = fieldField.GetValue(state);
					if (fieldPos is Vector2Int pos && pos == targetPos) {
						return true;
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] HasFarmfieldAt failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Check if a building model is a House model (housing building).
		/// </summary>
		public static bool IsHouseModel(object buildingModel) {
			if (buildingModel == null) return false;
			// HouseModel is the model type for houses
			return buildingModel.GetType().Name == "HouseModel";
		}

		/// <summary>
		/// Check if a building model is an Institution model (service building).
		/// </summary>
		public static bool IsInstitutionModel(object buildingModel) {
			if (buildingModel == null) return false;
			return buildingModel.GetType().Name == "InstitutionModel";
		}

		/// <summary>
		/// Check if a building model is a Decoration model.
		/// </summary>
		public static bool IsDecorationModel(object buildingModel) {
			if (buildingModel == null) return false;
			return buildingModel.GetType().Name == "DecorationModel";
		}

		/// <summary>
		/// Get the maxDistance field from a Camp/GathererHut/FishingHut model.
		/// Returns 0 if not a gathering building.
		/// </summary>
		public static float GetGatheringBuildingMaxDistance(object buildingModel) {
			if (buildingModel == null) return 0f;
			EnsureRangeInfoTypes();

			try {
				if (IsCampModel(buildingModel) && _campMaxDistanceField != null) {
					return (float)_campMaxDistanceField.GetValue(buildingModel);
				}
				if (IsGathererHutModel(buildingModel) && _gathererHutMaxDistanceField != null) {
					return (float)_gathererHutMaxDistanceField.GetValue(buildingModel);
				}
				if (IsFishingHutModel(buildingModel) && _fishingHutMaxDistanceField != null) {
					return (float)_fishingHutMaxDistanceField.GetValue(buildingModel);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGatheringBuildingMaxDistance failed: {ex.Message}");
			}

			return 0f;
		}

		/// <summary>
		/// Get the base hubRange from a Hearth model (before effects).
		/// </summary>
		public static float GetHearthBaseRange(object buildingModel) {
			if (buildingModel == null) return 0f;
			EnsureRangeInfoTypes();

			try {
				if (IsHearthModel(buildingModel) && _hearthHubRangeField != null) {
					return (float)_hearthHubRangeField.GetValue(buildingModel);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetHearthBaseRange failed: {ex.Message}");
			}

			return 10.5f; // Default hearth range
		}

		/// <summary>
		/// Get the effective hearth range (with effects applied).
		/// </summary>
		public static float GetEffectiveHearthRange(object buildingModel) {
			EnsureRangeInfoTypes();
			float baseRange = GetHearthBaseRange(buildingModel);

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return baseRange;

				var effectsService = _effectsServiceProperty?.GetValue(gameServices);
				if (effectsService == null || _effectsGetHearthRangeMethod == null) return baseRange;

				return (float)_effectsGetHearthRangeMethod.Invoke(effectsService, new object[] { baseRange });
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetEffectiveHearthRange failed: {ex.Message}");
			}

			return baseRange;
		}

		/// <summary>
		/// Get recipe good names for a gathering building.
		/// Returns list of good names this building can harvest.
		/// </summary>
		public static List<string> GetGatheringBuildingGoodNames(object buildingModel) {
			var goodNames = new List<string>();
			if (buildingModel == null) return goodNames;
			EnsureRangeInfoTypes();

			try {
				Array recipes = null;
				FieldInfo refGoodField = null;

				if (IsCampModel(buildingModel)) {
					recipes = _campRecipesField?.GetValue(buildingModel) as Array;
					refGoodField = _campRecipeRefGoodField;
				} else if (IsGathererHutModel(buildingModel)) {
					recipes = _gathererHutRecipesField?.GetValue(buildingModel) as Array;
					refGoodField = _gathererHutRecipeRefGoodField;
				} else if (IsFishingHutModel(buildingModel)) {
					recipes = _fishingHutRecipesField?.GetValue(buildingModel) as Array;
					refGoodField = _fishingHutRecipeRefGoodField;
				}

				if (recipes == null || refGoodField == null) return goodNames;

				foreach (var recipe in recipes) {
					var refGood = refGoodField.GetValue(recipe);
					if (refGood != null) {
						// GoodRef has a Name property that returns the good's name
						var nameProp = refGood.GetType().GetProperty("Name", GameReflection.PublicInstance);
						var name = nameProp?.GetValue(refGood) as string;
						if (!string.IsNullOrEmpty(name) && !goodNames.Contains(name)) {
							goodNames.Add(name);
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGatheringBuildingGoodNames failed: {ex.Message}");
			}

			return goodNames;
		}

		/// <summary>
		/// Get AvailableResources dictionary from ResourcesService.
		/// Dictionary<string, List<NaturalResource>> where key is good name.
		/// </summary>
		public static object GetAvailableResources() {
			EnsureRangeInfoTypes();
			var resourcesService = GameReflection.GetResourcesService();
			if (resourcesService == null || _resourcesAvailableProperty == null) return null;

			try {
				return _resourcesAvailableProperty.GetValue(resourcesService);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAvailableResources failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get AvailableDeposits dictionary from DepositsService.
		/// Dictionary<string, List<ResourceDeposit>> where key is good name.
		/// </summary>
		public static object GetAvailableDeposits() {
			EnsureRangeInfoTypes();
			var depositsService = GameReflection.GetDepositsService();
			if (depositsService == null || _depositsAvailableProperty == null) return null;

			try {
				return _depositsAvailableProperty.GetValue(depositsService);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAvailableDeposits failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get AvailableLakes dictionary from LakesService.
		/// Dictionary<string, List<Lake>> where key is good name.
		/// </summary>
		public static object GetAvailableLakes() {
			EnsureRangeInfoTypes();
			var lakesService = GameReflection.GetLakesService();
			if (lakesService == null || _lakesAvailableProperty == null) return null;

			try {
				return _lakesAvailableProperty.GetValue(lakesService);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAvailableLakes failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the display name of a resource node (NaturalResource, ResourceDeposit, or Lake).
		/// Returns the model's displayName which is the actual node name (e.g., "Lush Tree", "Clay Pit").
		/// </summary>
		public static string GetResourceNodeDisplayName(object resource) {
			if (resource == null) return null;

			try {
				// Get the Model property (all resource types have this)
				var modelProp = resource.GetType().GetProperty("Model", GameReflection.PublicInstance);
				if (modelProp == null) return null;

				var model = modelProp.GetValue(resource);
				if (model == null) return null;

				// Get displayName field from the model (NaturalResourceModel, ResourceDepositModel, LakeModel all have this)
				var displayNameField = model.GetType().GetField("displayName", GameReflection.PublicInstance);
				if (displayNameField != null) {
					var locaText = displayNameField.GetValue(model);
					if (locaText != null) {
						// LocaText has a Text property that returns the localized string
						var textProp = locaText.GetType().GetProperty("Text", GameReflection.PublicInstance);
						return textProp?.GetValue(locaText) as string;
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetResourceNodeDisplayName failed: {ex.Message}");
			}

			return null;
		}

		// ========================================
		// LAKE INTERACTION
		// ========================================

		/// <summary>
		/// Get the charges remaining on a lake.
		/// </summary>
		public static int GetLakeChargesLeft(object lake) {
			if (lake == null || lake.GetType().Name != "Lake") return 0;

			try {
				var stateProp = lake.GetType().GetProperty("State", GameReflection.PublicInstance);
				var state = stateProp?.GetValue(lake);
				if (state == null) return 0;

				var chargesField = state.GetType().GetField("chargesLeft", GameReflection.PublicInstance);
				return chargesField != null ? (int)chargesField.GetValue(state) : 0;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetLakeChargesLeft failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Get the stored goods in a lake as a list of (displayName, amount).
		/// </summary>
		public static List<(string name, int amount)> GetLakeStoredGoods(object lake) {
			var result = new List<(string name, int amount)>();
			if (lake == null || lake.GetType().Name != "Lake") return result;

			try {
				var stateProp = lake.GetType().GetProperty("State", GameReflection.PublicInstance);
				var state = stateProp?.GetValue(lake);
				if (state == null) return result;

				var goodsField = state.GetType().GetField("goods", GameReflection.PublicInstance);
				var goodsCollection = goodsField?.GetValue(state);
				if (goodsCollection == null) return result;

				// GoodsCollection.goods is Dictionary<string, int>
				var dictField = goodsCollection.GetType().GetField("goods", GameReflection.PublicInstance);
				var dict = dictField?.GetValue(goodsCollection) as Dictionary<string, int>;
				if (dict == null) return result;

				foreach (var kvp in dict) {
					if (kvp.Value > 0) {
						string displayName = GameReflection.GetGoodDisplayName(kvp.Key);
						result.Add((displayName, kvp.Value));
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetLakeStoredGoods failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Force deplete a lake (stop fishing, stored goods will still be delivered).
		/// Returns true if succeeded.
		/// </summary>
		public static bool ForceDepliteLake(object lake) {
			if (lake == null || lake.GetType().Name != "Lake") return false;

			try {
				var method = lake.GetType().GetMethod("ForceDeplition", GameReflection.PublicInstance, null, Type.EmptyTypes, null);
				if (method == null) return false;
				method.Invoke(lake, null);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ForceDepliteLake failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the priority of a resource deposit or lake.
		/// Both ResourceDepositState and LakeState have a "prio" field.
		/// </summary>
		public static int GetResourceNodePriority(object node) {
			if (node == null) return 0;
			string typeName = node.GetType().Name;
			if (typeName != "ResourceDeposit" && typeName != "Lake") return 0;

			try {
				var stateProp = node.GetType().GetProperty("State", GameReflection.PublicInstance);
				var state = stateProp?.GetValue(node);
				if (state == null) return 0;

				var prioField = state.GetType().GetField("prio", GameReflection.PublicInstance);
				return prioField != null ? (int)prioField.GetValue(state) : 0;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetResourceNodePriority failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Set the priority of a resource deposit or lake.
		/// Both ResourceDepositState and LakeState have a "prio" field.
		/// Clamps to -5/+5 (same as game UI).
		/// </summary>
		public static bool SetResourceNodePriority(object node, int priority) {
			if (node == null) return false;
			string typeName = node.GetType().Name;
			if (typeName != "ResourceDeposit" && typeName != "Lake") return false;

			priority = Math.Max(-5, Math.Min(5, priority));

			try {
				var stateProp = node.GetType().GetProperty("State", GameReflection.PublicInstance);
				var state = stateProp?.GetValue(node);
				if (state == null) return false;

				var prioField = state.GetType().GetField("prio", GameReflection.PublicInstance);
				if (prioField == null) return false;
				prioField.SetValue(state, priority);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetResourceNodePriority failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Set priority on all deposits or lakes producing the same good as the given node.
		/// Calls the game's ChangeGlobalPriorityTo on the appropriate service.
		/// </summary>
		public static bool SetGlobalResourceNodePriority(object node, int priority) {
			if (node == null) return false;
			string typeName = node.GetType().Name;
			if (typeName != "ResourceDeposit" && typeName != "Lake") return false;

			priority = Math.Max(-5, Math.Min(5, priority));

			try {
				object service;
				if (typeName == "ResourceDeposit") {
					service = GameReflection.GetDepositsService();
				} else {
					service = GameReflection.GetLakesService();
				}
				if (service == null) return false;

				var method = service.GetType().GetMethod("ChangeGlobalPriorityTo", GameReflection.PublicInstance);
				if (method == null) return false;
				method.Invoke(service, new object[] { node, priority });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetGlobalResourceNodePriority failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the construction priority of a building under construction.
		/// Reads BuildingState.constructionPriority. Returns 0 default.
		/// </summary>
		public static int GetBuildingConstructionPriority(object building) {
			if (building == null) return 0;

			try {
				var stateProperty = building.GetType().GetProperty("BuildingState", GameReflection.PublicInstance);
				var state = stateProperty?.GetValue(building);
				if (state == null) return 0;

				var prioField = state.GetType().GetField("constructionPriority", GameReflection.PublicInstance);
				return prioField != null ? (int)prioField.GetValue(state) : 0;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingConstructionPriority failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Set the construction priority of a building under construction.
		/// Uses BuildingsService.ChangePriorityTo so ConstructionQueue re-sorts.
		/// </summary>
		public static bool SetBuildingConstructionPriority(object building, int priority) {
			if (building == null) return false;

			try {
				var service = GameReflection.GetBuildingsService();
				if (service == null) return false;

				var method = service.GetType().GetMethod("ChangePriorityTo", GameReflection.PublicInstance);
				if (method == null) return false;
				method.Invoke(service, new object[] { building, priority });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetBuildingConstructionPriority failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Set construction priority on all under-construction buildings of the same model.
		/// Uses BuildingsService.ChangeGlobalPriorityTo.
		/// </summary>
		public static bool SetGlobalBuildingConstructionPriority(object building, int priority) {
			if (building == null) return false;

			try {
				var service = GameReflection.GetBuildingsService();
				if (service == null) return false;

				var method = service.GetType().GetMethod("ChangeGlobalPriorityTo", GameReflection.PublicInstance);
				if (method == null) return false;
				method.Invoke(service, new object[] { building, priority });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetGlobalBuildingConstructionPriority failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the center position of a building.
		/// Returns null if building is null or center cannot be determined.
		/// </summary>
		public static Vector3? GetBuildingCenter(object building) {
			if (building == null) return null;

			try {
				var centerProperty = building.GetType().GetProperty("Center", GameReflection.PublicInstance);
				if (centerProperty != null) {
					return (Vector3)centerProperty.GetValue(building);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingCenter failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Get Field (position) of a resource/deposit/lake object.
		/// </summary>
		public static Vector2Int? GetResourceField(object resource) {
			if (resource == null) return null;

			try {
				var fieldProperty = resource.GetType().GetProperty("Field", GameReflection.PublicInstance);
				if (fieldProperty != null) {
					return (Vector2Int)fieldProperty.GetValue(resource);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetResourceField failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Get Size of a resource/deposit/lake object.
		/// </summary>
		public static Vector2Int? GetResourceSize(object resource) {
			if (resource == null) return null;

			try {
				var sizeProperty = resource.GetType().GetProperty("Size", GameReflection.PublicInstance);
				if (sizeProperty != null) {
					return (Vector2Int)sizeProperty.GetValue(resource);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetResourceSize failed: {ex.Message}");
			}

			return Vector2Int.one;
		}

		private static System.Reflection.PropertyInfo _brHearthsDictProperty = null;

		/// <summary>
		/// Get all hearths from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllHearths() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				// BuildingsService has Hearths property (Dictionary<int, Hearth>)
				if (_brHearthsDictProperty == null) {
					_brHearthsDictProperty = buildingsService.GetType().GetProperty("Hearths", GameReflection.PublicInstance);
				}

				var hearthsDict = _brHearthsDictProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return hearthsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllHearths failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all houses from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllHouses() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var housesProperty = buildingsService.GetType().GetProperty("Houses", GameReflection.PublicInstance);
				var housesDict = housesProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return housesDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllHouses failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all institutions from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllInstitutions() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var institutionsProperty = buildingsService.GetType().GetProperty("Institutions", GameReflection.PublicInstance);
				var institutionsDict = institutionsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return institutionsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllInstitutions failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all decorations from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllDecorations() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var decorationsProperty = buildingsService.GetType().GetProperty("Decorations", GameReflection.PublicInstance);
				var decorationsDict = decorationsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return decorationsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllDecorations failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Check if a building is a House.
		/// </summary>
		public static bool IsHouseBuilding(object building) {
			if (building == null) return false;
			return building.GetType().Name == "House";
		}

		/// <summary>
		/// Check if a given position is within a hearth's range.
		/// </summary>
		public static bool IsInHearthRange(object hearth, Vector2Int position) {
			if (hearth == null) return false;

			try {
				// Hearth has IsInRange(Vector2Int field) method
				var isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
					new Type[] { typeof(Vector2Int) });
				if (isInRangeMethod != null) {
					return (bool)isInRangeMethod.Invoke(hearth, new object[] { position });
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] IsInHearthRange failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Check if a building is in hearth range using the game's IsInRange method.
		/// Works for House, Institution, Decoration, or any building with a Field property.
		/// </summary>
		public static bool IsInHearthRange(object hearth, object building) {
			if (hearth == null || building == null) return false;

			try {
				// Hearth.IsInRange(Building building) - uses building's Field property
				var isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
					null,
					new Type[] { building.GetType() },
					null);

				if (isInRangeMethod != null) {
					return (bool)isInRangeMethod.Invoke(hearth, new object[] { building });
				}

				// Fallback: try with base Building type
				var buildingType = building.GetType().BaseType;
				while (buildingType != null && buildingType.Name != "Building") {
					buildingType = buildingType.BaseType;
				}

				if (buildingType != null) {
					isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
						null,
						new Type[] { buildingType },
						null);

					if (isInRangeMethod != null) {
						return (bool)isInRangeMethod.Invoke(hearth, new object[] { building });
					}
				}

				// Last fallback: use Field position
				var fieldProp = building.GetType().GetProperty("Field", GameReflection.PublicInstance);
				if (fieldProp != null) {
					var field = (Vector2Int)fieldProp.GetValue(building);
					return IsInHearthRange(hearth, field);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] IsInHearthRange(building) failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Calculate distance between a building center (Vector2) and a resource field (Vector2Int).
		/// Uses the game's distance formula: distance from (center.x, center.z) - FieldCenter to field.
		/// </summary>
		public static float CalculateResourceDistance(Vector2 buildingCenter2D, Vector2Int resourceField) {
			// Game uses: Vector2.Distance(new Vector2(building.Center.x, building.Center.z) - Constants.FieldCenter, res.Field)
			// Constants.FieldCenter is (0.5, 0.5)
			Vector2 adjustedCenter = buildingCenter2D - new Vector2(0.5f, 0.5f);
			return Vector2.Distance(adjustedCenter, (Vector2)resourceField);
		}

		/// <summary>
		/// Calculate distance from building center to the closest tile of a multi-tile deposit/lake.
		/// </summary>
		public static float CalculateDepositDistance(Vector2 buildingCenter2D, Vector2Int depositField, Vector2Int depositSize) {
			// For deposits/lakes, check distance to each tile and return minimum
			float minDistance = float.MaxValue;
			Vector2 adjustedCenter = buildingCenter2D - new Vector2(0.5f, 0.5f);

			for (int x = depositField.x; x < depositField.x + depositSize.x; x++) {
				for (int y = depositField.y; y < depositField.y + depositSize.y; y++) {
					float dist = Vector2.Distance(adjustedCenter, new Vector2(x, y));
					if (dist < minDistance) {
						minDistance = dist;
					}
				}
			}

			return minDistance;
		}

		/// <summary>
		/// Calculate building center from cursor position and building size.
		/// </summary>
		public static Vector2 CalculateBuildingCenter(int cursorX, int cursorY, Vector2Int size) {
			// Building center is offset from cursor by half the size
			return new Vector2(
				cursorX + (size.x - 1) / 2f,
				cursorY + (size.y - 1) / 2f
			);
		}


		// ========================================
		// SUPPLY CHAIN INFO (for production buildings)
		// ========================================

		/// <summary>
		/// Get a building's entrance center position (used for distance calculations).
		/// </summary>
		public static Vector2? GetBuildingEntranceCenter(object building) {
			if (building == null) return null;

			try {
				var entranceCenterProp = building.GetType().GetProperty("EntranceCenter", GameReflection.PublicInstance);
				if (entranceCenterProp != null) {
					return (Vector2)entranceCenterProp.GetValue(building);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingEntranceCenter failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Get all main storage buildings (warehouses).
		/// </summary>
		public static System.Collections.IEnumerable GetAllStorageBuildings() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var storagesProperty = buildingsService.GetType().GetProperty("Storages", GameReflection.PublicInstance);
				var storagesDict = storagesProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return storagesDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllStorageBuildings failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all farms from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllFarms() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var farmsProperty = buildingsService.GetType().GetProperty("Farms", GameReflection.PublicInstance);
				var farmsDict = farmsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return farmsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllFarms failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all camps from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllCamps() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var campsProperty = buildingsService.GetType().GetProperty("Camps", GameReflection.PublicInstance);
				var campsDict = campsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return campsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllCamps failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all gatherer huts from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllGathererHuts() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var hutsProperty = buildingsService.GetType().GetProperty("GathererHuts", GameReflection.PublicInstance);
				var hutsDict = hutsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return hutsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllGathererHuts failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all fishing huts from BuildingsService.
		/// </summary>
		public static System.Collections.IEnumerable GetAllFishingHuts() {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				var hutsProperty = buildingsService.GetType().GetProperty("FishingHuts", GameReflection.PublicInstance);
				var hutsDict = hutsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
				return hutsDict?.Values;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllFishingHuts failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get local storage distance from game config (default 6 tiles).
		/// This is the range within which production buildings can pull from each other.
		/// </summary>
		public static float GetLocalStorageDistance() {
			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return 6f;

				var logisticConfigField = settings.GetType().GetField("logisticConfig", GameReflection.PublicInstance);
				if (logisticConfigField == null) return 6f;

				var logisticConfig = logisticConfigField.GetValue(settings);
				if (logisticConfig == null) return 6f;

				var maxDistField = logisticConfig.GetType().GetField("maxLocalStorageDistance", GameReflection.PublicInstance);
				if (maxDistField != null) {
					return (float)maxDistField.GetValue(logisticConfig);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetLocalStorageDistance failed: {ex.Message}");
			}

			return 6f; // Default
		}

		/// <summary>
		/// Check if a building is a source of a specific good (can output it).
		/// Works for production buildings (Workshop, Camp, GathererHut, etc.)
		/// Checks possible outputs based on recipes, not current inventory.
		/// </summary>
		public static bool IsBuildingSourceOf(object building, string goodName) {
			if (building == null || string.IsNullOrEmpty(goodName)) return false;

			try {
				// Get the GoodModel from settings
				var settings = GameReflection.GetSettings();
				if (settings == null) return false;

				var getGoodMethod = settings.GetType().GetMethod("GetGood", new Type[] { typeof(string) });
				if (getGoodMethod == null) return false;

				var goodModel = getGoodMethod.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return false;

				// Get the GoodModel type from the assembly for proper method lookup
				var goodModelType = GameReflection.GameAssembly.GetType("Eremite.Model.GoodModel");
				if (goodModelType == null) {
					Debug.LogWarning("[ATSAccessibility] Could not find GoodModel type");
					return false;
				}

				// Check if building.IsSourceOf(goodModel) returns true
				var isSourceOfMethod = building.GetType().GetMethod("IsSourceOf",
					GameReflection.PublicInstance, null, new Type[] { goodModelType }, null);

				if (isSourceOfMethod != null) {
					return (bool)isSourceOfMethod.Invoke(building, new object[] { goodModel });
				} else {
					Debug.Log($"[ATSAccessibility] IsSourceOf method not found on {building.GetType().Name}");
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] IsBuildingSourceOf failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Get all input goods required by a production building (from its recipes).
		/// Returns list of good names that are needed as inputs.
		/// </summary>
		public static List<string> GetBuildingRequiredInputs(object building) {
			var inputs = new List<string>();
			if (building == null) return inputs;

			try {
				// Try to get the state which has recipes
				var stateProperty = building.GetType().GetProperty("state", GameReflection.PublicInstance);
				var stateField = building.GetType().GetField("state", GameReflection.PublicInstance);

				object state = null;
				if (stateProperty != null)
					state = stateProperty.GetValue(building);
				else if (stateField != null)
					state = stateField.GetValue(building);

				if (state == null) return inputs;

				// Get recipes array from state
				var recipesField = state.GetType().GetField("recipes", GameReflection.PublicInstance);
				if (recipesField == null) return inputs;

				var recipesObj = recipesField.GetValue(state);
				if (recipesObj == null) return inputs;

				var recipes = recipesObj as System.Collections.IEnumerable;
				if (recipes == null) return inputs;

				foreach (var recipeState in recipes) {
					if (recipeState == null) continue;

					// Check if recipe is active
					var activeField = recipeState.GetType().GetField("active", GameReflection.PublicInstance);
					bool isActive = activeField == null || (bool)activeField.GetValue(recipeState);

					if (!isActive) continue;

					// Get ingredients from recipe state
					var ingredientsField = recipeState.GetType().GetField("ingredients", GameReflection.PublicInstance);
					if (ingredientsField == null) continue;

					var ingredients = ingredientsField.GetValue(recipeState) as Array;
					if (ingredients == null) continue;

					// Ingredients is a 2D array: IngredientState[][]
					foreach (var ingredientSet in ingredients) {
						var ingredientArray = ingredientSet as Array;
						if (ingredientArray == null) continue;

						foreach (var ingredientState in ingredientArray) {
							if (ingredientState == null) continue;

							// Check if allowed
							var allowedField = ingredientState.GetType().GetField("allowed", GameReflection.PublicInstance);
							bool isAllowed = allowedField == null || (bool)allowedField.GetValue(ingredientState);

							if (!isAllowed) continue;

							// Get good name - good is a Good struct with a name field
							var goodField = ingredientState.GetType().GetField("good", GameReflection.PublicInstance);
							if (goodField != null) {
								var goodStruct = goodField.GetValue(ingredientState);
								if (goodStruct != null) {
									// Get the name field from the Good struct
									var nameField = goodStruct.GetType().GetField("name", GameReflection.PublicInstance);
									if (nameField != null) {
										var goodName = nameField.GetValue(goodStruct) as string;
										if (!string.IsNullOrEmpty(goodName) && !inputs.Contains(goodName)) {
											inputs.Add(goodName);
										}
									}
								}
							}
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingRequiredInputs failed: {ex.Message}");
			}

			return inputs;
		}

		/// <summary>
		/// Get all possible input goods for a building model (all recipes, all ingredients).
		/// Works on the model itself, so it can be used for both placed buildings and build mode preview.
		/// </summary>
		public static List<string> GetModelPossibleInputs(object buildingModel) {
			var inputs = new List<string>();
			if (buildingModel == null) return inputs;

			try {
				// Get recipes array from model (WorkshopModel.recipes, etc.)
				var recipesField = buildingModel.GetType().GetField("recipes", GameReflection.PublicInstance);
				if (recipesField == null) return inputs;

				var recipes = recipesField.GetValue(buildingModel) as Array;
				if (recipes == null) return inputs;

				foreach (var recipe in recipes) {
					if (recipe == null) continue;

					// Get requiredGoods from recipe (GoodsSet[])
					var requiredGoodsField = recipe.GetType().GetField("requiredGoods", GameReflection.PublicInstance);
					if (requiredGoodsField == null) continue;

					var requiredGoods = requiredGoodsField.GetValue(recipe) as Array;
					if (requiredGoods == null) continue;

					// Each GoodsSet has a goods array (GoodRef[])
					foreach (var goodsSet in requiredGoods) {
						if (goodsSet == null) continue;

						var goodsField = goodsSet.GetType().GetField("goods", GameReflection.PublicInstance);
						if (goodsField == null) continue;

						var goods = goodsField.GetValue(goodsSet) as Array;
						if (goods == null) continue;

						// Each GoodRef has a good field (GoodModel)
						foreach (var goodRef in goods) {
							if (goodRef == null) continue;

							var goodField = goodRef.GetType().GetField("good", GameReflection.PublicInstance);
							if (goodField == null) continue;

							var goodModel = goodField.GetValue(goodRef);
							if (goodModel == null) continue;

							// Get the Name property from GoodModel
							var nameProperty = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
							if (nameProperty != null) {
								var goodName = nameProperty.GetValue(goodModel) as string;
								if (!string.IsNullOrEmpty(goodName) && !inputs.Contains(goodName)) {
									inputs.Add(goodName);
								}
							}
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetModelPossibleInputs failed: {ex.Message}");
			}

			return inputs;
		}

		/// <summary>
		/// Get all production buildings that could supply a specific good.
		/// Includes Workshops, Camps, GathererHuts, Mines, Farms, etc.
		/// </summary>
		public static List<object> GetBuildingsThatProduce(string goodName) {
			var producers = new List<object>();
			if (string.IsNullOrEmpty(goodName)) return producers;

			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return producers;

			try {
				// Get the Buildings dictionary (all buildings)
				var buildingsProperty = buildingsService.GetType().GetProperty("Buildings", GameReflection.PublicInstance);
				var buildingsDict = buildingsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;

				if (buildingsDict != null) {
					foreach (System.Collections.DictionaryEntry entry in buildingsDict) {
						var building = entry.Value;
						if (building == null) continue;

						// Check if building is finished
						var isFinishedMethod = building.GetType().GetMethod("IsFinished", GameReflection.PublicInstance);
						bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(building, null);

						if (!isFinished) continue;

						// Check if this building produces the good
						if (IsBuildingSourceOf(building, goodName)) {
							producers.Add(building);
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingsThatProduce failed: {ex.Message}");
			}

			return producers;
		}

		/// <summary>
		/// Get the goods a building can actually output.
		/// For gathering buildings (Camp, GathererHut, FishingHut), checks what resources are in range.
		/// For production buildings (Workshop), checks active recipes.
		/// </summary>
		public static List<string> GetBuildingActualOutputs(object building) {
			var outputs = new List<string>();
			if (building == null) return outputs;

			try {
				string typeName = building.GetType().Name;

				if (typeName == "Camp") {
					outputs = GetCampActualOutputs(building);
				} else if (typeName == "GathererHut") {
					outputs = GetGathererHutActualOutputs(building);
				} else if (typeName == "FishingHut") {
					outputs = GetFishingHutActualOutputs(building);
				} else if (typeName == "Workshop") {
					outputs = GetWorkshopActiveOutputs(building);
				} else {
					// For other buildings, fall back to model-based possible outputs
					var model = GetBuildingModel(building);
					if (model != null) {
						outputs = GetModelPossibleOutputs(model);
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingActualOutputs failed: {ex.Message}");
			}

			return outputs;
		}

		/// <summary>
		/// Get goods a Camp can actually harvest based on resources in range.
		/// </summary>
		private static List<string> GetCampActualOutputs(object camp) {
			var outputs = new List<string>();

			try {
				var model = GetBuildingModel(camp);
				if (model == null) return outputs;

				// Get building center for distance check
				var center = GetBuildingCenter(camp);
				if (!center.HasValue) return outputs;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float maxDistance = GetGatheringBuildingMaxDistance(model);

				// Get recipes to know what goods this camp can harvest
				var goodNames = GetGatheringBuildingGoodNames(model);
				var availableResources = GetAvailableResources();

				if (availableResources == null) return outputs;

				var dict = availableResources as System.Collections.IDictionary;
				if (dict == null) return outputs;

				foreach (var goodName in goodNames) {
					if (!dict.Contains(goodName)) continue;

					var resourceList = dict[goodName] as System.Collections.IEnumerable;
					if (resourceList == null) continue;

					// Check if any resource of this type is in range
					foreach (var resource in resourceList) {
						var field = GetResourceField(resource);
						if (!field.HasValue) continue;

						float distance = CalculateResourceDistance(center2D, field.Value);
						if (distance < maxDistance) {
							if (!outputs.Contains(goodName)) {
								outputs.Add(goodName);
							}
							break; // Found at least one in range, move to next good type
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetCampActualOutputs failed: {ex.Message}");
			}

			return outputs;
		}

		/// <summary>
		/// Get goods a GathererHut can actually harvest based on deposits in range.
		/// </summary>
		private static List<string> GetGathererHutActualOutputs(object hut) {
			var outputs = new List<string>();

			try {
				var model = GetBuildingModel(hut);
				if (model == null) return outputs;

				var center = GetBuildingCenter(hut);
				if (!center.HasValue) return outputs;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float maxDistance = GetGatheringBuildingMaxDistance(model);

				var goodNames = GetGatheringBuildingGoodNames(model);
				var availableDeposits = GetAvailableDeposits();

				if (availableDeposits == null) return outputs;

				var dict = availableDeposits as System.Collections.IDictionary;
				if (dict == null) return outputs;

				foreach (var goodName in goodNames) {
					if (!dict.Contains(goodName)) continue;

					var depositList = dict[goodName] as System.Collections.IEnumerable;
					if (depositList == null) continue;

					foreach (var deposit in depositList) {
						var field = GetResourceField(deposit);
						if (!field.HasValue) continue;

						var size = GetResourceSize(deposit) ?? Vector2Int.one;
						float distance = CalculateDepositDistance(center2D, field.Value, size);
						if (distance < maxDistance) {
							if (!outputs.Contains(goodName)) {
								outputs.Add(goodName);
							}
							break;
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGathererHutActualOutputs failed: {ex.Message}");
			}

			return outputs;
		}

		/// <summary>
		/// Get goods a FishingHut can actually harvest based on lakes in range.
		/// </summary>
		private static List<string> GetFishingHutActualOutputs(object hut) {
			var outputs = new List<string>();

			try {
				var model = GetBuildingModel(hut);
				if (model == null) return outputs;

				var center = GetBuildingCenter(hut);
				if (!center.HasValue) return outputs;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float maxDistance = GetGatheringBuildingMaxDistance(model);

				var goodNames = GetGatheringBuildingGoodNames(model);
				var availableLakes = GetAvailableLakes();

				if (availableLakes == null) return outputs;

				var dict = availableLakes as System.Collections.IDictionary;
				if (dict == null) return outputs;

				foreach (var goodName in goodNames) {
					if (!dict.Contains(goodName)) continue;

					var lakeList = dict[goodName] as System.Collections.IEnumerable;
					if (lakeList == null) continue;

					foreach (var lake in lakeList) {
						var field = GetResourceField(lake);
						if (!field.HasValue) continue;

						var size = GetResourceSize(lake) ?? Vector2Int.one;
						float distance = CalculateDepositDistance(center2D, field.Value, size);
						if (distance < maxDistance) {
							if (!outputs.Contains(goodName)) {
								outputs.Add(goodName);
							}
							break;
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetFishingHutActualOutputs failed: {ex.Message}");
			}

			return outputs;
		}

		/// <summary>
		/// Get goods a Workshop produces based on active recipes.
		/// </summary>
		private static List<string> GetWorkshopActiveOutputs(object workshop) {
			var outputs = new List<string>();

			try {
				// Get state.recipes
				var stateField = workshop.GetType().GetField("state", GameReflection.PublicInstance);
				if (stateField == null) return outputs;

				var state = stateField.GetValue(workshop);
				if (state == null) return outputs;

				var recipesField = state.GetType().GetField("recipes", GameReflection.PublicInstance);
				if (recipesField == null) return outputs;

				var recipes = recipesField.GetValue(state) as System.Collections.IEnumerable;
				if (recipes == null) return outputs;

				foreach (var recipeState in recipes) {
					if (recipeState == null) continue;

					// Check if active
					var activeField = recipeState.GetType().GetField("active", GameReflection.PublicInstance);
					bool isActive = activeField == null || (bool)activeField.GetValue(recipeState);
					if (!isActive) continue;

					// Get productName
					var productNameField = recipeState.GetType().GetField("productName", GameReflection.PublicInstance);
					if (productNameField != null) {
						var productName = productNameField.GetValue(recipeState) as string;
						if (!string.IsNullOrEmpty(productName) && !outputs.Contains(productName)) {
							outputs.Add(productName);
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetWorkshopActiveOutputs failed: {ex.Message}");
			}

			return outputs;
		}

		/// <summary>
		/// Get all possible outputs from a building model (all recipes).
		/// </summary>
		private static List<string> GetModelPossibleOutputs(object buildingModel) {
			var outputs = new List<string>();
			if (buildingModel == null) return outputs;

			try {
				// Get recipes array
				var recipesField = buildingModel.GetType().GetField("recipes", GameReflection.PublicInstance);
				if (recipesField == null) return outputs;

				var recipes = recipesField.GetValue(buildingModel) as Array;
				if (recipes == null) return outputs;

				foreach (var recipe in recipes) {
					if (recipe == null) continue;

					// Try producedGood (for WorkshopRecipeModel)
					var producedGoodField = recipe.GetType().GetField("producedGood", GameReflection.PublicInstance);
					if (producedGoodField != null) {
						var producedGood = producedGoodField.GetValue(recipe);
						if (producedGood != null) {
							var goodField = producedGood.GetType().GetField("good", GameReflection.PublicInstance);
							if (goodField != null) {
								var goodModel = goodField.GetValue(producedGood);
								if (goodModel != null) {
									var nameProp = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
									if (nameProp != null) {
										var name = nameProp.GetValue(goodModel) as string;
										if (!string.IsNullOrEmpty(name) && !outputs.Contains(name)) {
											outputs.Add(name);
										}
									}
								}
							}
						}
					}

					// Try refGood (for CampRecipeModel, GathererHutRecipeModel, etc.)
					var refGoodField = recipe.GetType().GetField("refGood", GameReflection.PublicInstance);
					if (refGoodField != null) {
						var refGood = refGoodField.GetValue(recipe);
						if (refGood != null) {
							var goodField = refGood.GetType().GetField("good", GameReflection.PublicInstance);
							if (goodField != null) {
								var goodModel = goodField.GetValue(refGood);
								if (goodModel != null) {
									var nameProp = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance);
									if (nameProp != null) {
										var name = nameProp.GetValue(goodModel) as string;
										if (!string.IsNullOrEmpty(name) && !outputs.Contains(name)) {
											outputs.Add(name);
										}
									}
								}
							}
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetModelPossibleOutputs failed: {ex.Message}");
			}

			return outputs;
		}


		// ========================================
		// BUILDING ENUMERATION HELPERS
		// ========================================

		private static PropertyInfo _allBuildingsProperty = null;
		private static bool _allBuildingsPropertyCached = false;

		// Note: reuses existing _buildingFieldProperty for Field access

		/// <summary>
		/// Get all building objects from the BuildingsService.Buildings dictionary.
		/// Returns empty list on failure.
		/// </summary>
		public static List<object> GetAllBuildingObjects() {
			var result = new List<object>();

			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return result;

			if (!_allBuildingsPropertyCached) {
				_allBuildingsProperty = buildingsService.GetType().GetProperty("Buildings", GameReflection.PublicInstance);
				_allBuildingsPropertyCached = true;
			}

			if (_allBuildingsProperty == null) return result;

			try {
				var dict = _allBuildingsProperty.GetValue(buildingsService) as System.Collections.IDictionary;
				if (dict == null) return result;

				foreach (System.Collections.DictionaryEntry entry in dict) {
					if (entry.Value != null)
						result.Add(entry.Value);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllBuildingObjects failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get a building's grid position via its Field property.
		/// Returns (-1,-1) on failure.
		/// </summary>
		public static Vector2Int GetBuildingPosition(object building) {
			if (building == null) return new Vector2Int(-1, -1);

			try {
				if (_buildingFieldProperty == null)
					_buildingFieldProperty = building.GetType().GetProperty("Field", GameReflection.PublicInstance);

				if (_buildingFieldProperty != null)
					return (Vector2Int)_buildingFieldProperty.GetValue(building);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingPosition failed: {ex.Message}");
			}

			return new Vector2Int(-1, -1);
		}

		/// <summary>
		/// Get a building's display name.
		/// Delegates to BuildingReflection.GetBuildingName which uses Building.DisplayName.
		/// </summary>
		public static string GetBuildingDisplayName(object building) {
			return BuildingReflection.GetBuildingName(building);
		}


		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(ConstructionReflection), "ConstructionReflection");
		}
	}
}
