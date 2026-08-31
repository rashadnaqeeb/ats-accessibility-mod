using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Institution-specific reflection (Tavern, Temple, etc.).
	/// Extracted from BuildingReflection.cs — routing type-check (IsInstitution) stays there.
	/// </summary>
	public static class InstitutionReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		internal static Type _institutionType = null;
		private static FieldInfo _institutionStateField = null;
		private static FieldInfo _institutionModelField = null;
		private static FieldInfo _institutionStorageField = null;
		private static FieldInfo _institutionStateRecipesField = null;
		private static FieldInfo _institutionModelRecipesField = null;
		private static FieldInfo _institutionRecipeStatePickedGoodField = null;
		private static FieldInfo _institutionRecipeModelServedNeedField = null;
		private static FieldInfo _institutionRecipeModelRequiredGoodsField = null;
		private static FieldInfo _institutionRecipeModelIsGoodConsumedField = null;
		private static MethodInfo _institutionChangeIngredientMethod = null;
		private static FieldInfo _institutionModelActiveEffectsField = null;
		private static FieldInfo _institutionEffectModelMinWorkersField = null;
		private static FieldInfo _institutionEffectModelEffectField = null;
		private static bool _institutionTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		internal static void EnsureTypes() {
			if (_institutionTypesCached) return;
			_institutionTypesCached = true;

			ReflectionHelper.InitCache("InstitutionReflection", assembly => {
				_institutionType = assembly.GetType("Eremite.Buildings.Institution");
				if (_institutionType != null) {
					_institutionStateField = _institutionType.GetField("state", GameReflection.PublicInstance);
					_institutionModelField = _institutionType.GetField("model", GameReflection.PublicInstance);
					_institutionStorageField = _institutionType.GetField("storage", GameReflection.PublicInstance);
					_institutionChangeIngredientMethod = _institutionType.GetMethod("ChangeIngredientFor", GameReflection.PublicInstance);
				}

				var institutionStateType = assembly.GetType("Eremite.Buildings.InstitutionState");
				if (institutionStateType != null) {
					_institutionStateRecipesField = institutionStateType.GetField("recipes", GameReflection.PublicInstance);
				}

				var institutionModelType = assembly.GetType("Eremite.Buildings.InstitutionModel");
				if (institutionModelType != null) {
					_institutionModelRecipesField = institutionModelType.GetField("recipes", GameReflection.PublicInstance);
					_institutionModelActiveEffectsField = institutionModelType.GetField("activeEffects", GameReflection.PublicInstance);
				}

				var institutionEffectModelType = assembly.GetType("Eremite.Buildings.InstitutionEffectModel");
				if (institutionEffectModelType != null) {
					_institutionEffectModelMinWorkersField = institutionEffectModelType.GetField("minWorkers", GameReflection.PublicInstance);
					_institutionEffectModelEffectField = institutionEffectModelType.GetField("effect", GameReflection.PublicInstance);
				}

				var institutionRecipeStateType = assembly.GetType("Eremite.Buildings.InstitutionRecipeState");
				if (institutionRecipeStateType != null) {
					_institutionRecipeStatePickedGoodField = institutionRecipeStateType.GetField("pickedGood", GameReflection.PublicInstance);
				}

				var institutionRecipeModelType = assembly.GetType("Eremite.Buildings.InstitutionRecipeModel");
				if (institutionRecipeModelType != null) {
					_institutionRecipeModelServedNeedField = institutionRecipeModelType.GetField("servedNeed", GameReflection.PublicInstance);
					_institutionRecipeModelRequiredGoodsField = institutionRecipeModelType.GetField("requiredGoods", GameReflection.PublicInstance);
					_institutionRecipeModelIsGoodConsumedField = institutionRecipeModelType.GetField("isGoodConsumed", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// PUBLIC API
		// ========================================

		public static bool IsInstitution(object building) {
			if (building == null) return false;
			EnsureTypes();
			if (_institutionType == null) return false;
			return _institutionType.IsInstanceOfType(building);
		}

		public static int GetRecipeCount(object building) {
			if (!IsInstitution(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return 0;

				var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
				return recipes?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetServedNeedName(object building, int recipeIndex) {
			if (!IsInstitution(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return null;

				var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
				if (recipes == null || recipeIndex >= recipes.Length) return null;

				var recipeModel = recipes.GetValue(recipeIndex);
				var servedNeed = ReflectionHelper.GetField(_institutionRecipeModelServedNeedField, recipeModel);
				if (servedNeed == null) return null;

				return servedNeed.GetType().GetProperty("DisplayName", GameReflection.PublicInstance)?.GetValue(servedNeed) as string;
			} catch {
				return null;
			}
		}

		public static bool IsRecipeGoodConsumed(object building, int recipeIndex) {
			if (!IsInstitution(building)) return false;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return false;

				var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
				if (recipes == null || recipeIndex >= recipes.Length) return false;

				var recipeModel = recipes.GetValue(recipeIndex);
				return ReflectionHelper.GetBool(_institutionRecipeModelIsGoodConsumedField, recipeModel);
			} catch {
				return false;
			}
		}

		public static string GetCurrentGoodName(object building, int recipeIndex) {
			if (!IsInstitution(building)) return null;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_institutionStateField, building);
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (state == null || model == null) return null;

				var stateRecipes = ReflectionHelper.GetList(_institutionStateRecipesField, state);
				var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
				if (stateRecipes == null || modelRecipes == null) return null;
				if (recipeIndex >= stateRecipes.Count || recipeIndex >= modelRecipes.Length) return null;

				var recipeState = stateRecipes[recipeIndex];
				var recipeModel = modelRecipes.GetValue(recipeIndex);

				int pickedGood = ReflectionHelper.GetInt(_institutionRecipeStatePickedGoodField, recipeState);
				var requiredGoods = ReflectionHelper.GetField(_institutionRecipeModelRequiredGoodsField, recipeModel);
				if (requiredGoods == null) return null;

				var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
				if (goodsArray == null || pickedGood >= goodsArray.Length) return null;

				var goodRef = goodsArray.GetValue(pickedGood);
				return BuildingReflection.GetGoodRefDisplayName(goodRef);
			} catch {
				return null;
			}
		}

		public static int GetAvailableGoodsCount(object building, int recipeIndex) {
			if (!IsInstitution(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return 0;

				var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
				if (modelRecipes == null || recipeIndex >= modelRecipes.Length) return 0;

				var recipeModel = modelRecipes.GetValue(recipeIndex);
				var requiredGoods = ReflectionHelper.GetField(_institutionRecipeModelRequiredGoodsField, recipeModel);
				if (requiredGoods == null) return 0;

				var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
				return goodsArray?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetAvailableGoodName(object building, int recipeIndex, int goodIndex) {
			if (!IsInstitution(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return null;

				var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
				if (modelRecipes == null || recipeIndex >= modelRecipes.Length) return null;

				var recipeModel = modelRecipes.GetValue(recipeIndex);
				var requiredGoods = ReflectionHelper.GetField(_institutionRecipeModelRequiredGoodsField, recipeModel);
				if (requiredGoods == null) return null;

				var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
				if (goodsArray == null || goodIndex >= goodsArray.Length) return null;

				var goodRef = goodsArray.GetValue(goodIndex);
				return BuildingReflection.GetGoodRefDisplayName(goodRef);
			} catch {
				return null;
			}
		}

		public static bool ChangeIngredient(object building, int recipeIndex, int goodIndex) {
			if (!IsInstitution(building)) return false;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_institutionStateField, building);
				if (state == null) return false;

				var stateRecipes = ReflectionHelper.GetList(_institutionStateRecipesField, state);
				if (stateRecipes == null || recipeIndex >= stateRecipes.Count) return false;

				var recipeState = stateRecipes[recipeIndex];
				ReflectionHelper.InvokeVoid(_institutionChangeIngredientMethod, building, recipeState, goodIndex);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] InstitutionReflection.ChangeIngredient failed: {ex.Message}");
				return false;
			}
		}

		public static Dictionary<string, int> GetStorageGoods(object building) {
			if (!IsInstitution(building)) return new Dictionary<string, int>();
			EnsureTypes();

			try {
				var storage = ReflectionHelper.GetField(_institutionStorageField, building);
				if (storage == null) return new Dictionary<string, int>();

				return BuildingReflection.GetBuildingStorageGoodsInternal(storage);
			} catch {
				return new Dictionary<string, int>();
			}
		}

		public static int GetEffectCount(object building) {
			if (!IsInstitution(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return 0;

				var effects = _institutionModelActiveEffectsField?.GetValue(model) as Array;
				return effects?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetEffectName(object building, int effectIndex) {
			if (!IsInstitution(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return null;

				var effects = _institutionModelActiveEffectsField?.GetValue(model) as Array;
				if (effects == null || effectIndex >= effects.Length) return null;

				var effectModel = effects.GetValue(effectIndex);
				var effect = ReflectionHelper.GetField(_institutionEffectModelEffectField, effectModel);
				if (effect == null) return null;

				var displayNameProp = effect.GetType().GetProperty("DisplayName", GameReflection.PublicInstance);
				return displayNameProp?.GetValue(effect) as string;
			} catch {
				return null;
			}
		}

		public static int GetEffectMinWorkers(object building, int effectIndex) {
			if (!IsInstitution(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return 0;

				var effects = _institutionModelActiveEffectsField?.GetValue(model) as Array;
				if (effects == null || effectIndex >= effects.Length) return 0;

				var effectModel = effects.GetValue(effectIndex);
				return ReflectionHelper.GetInt(_institutionEffectModelMinWorkersField, effectModel);
			} catch {
				return 0;
			}
		}

		public static string GetEffectDescription(object building, int effectIndex) {
			if (!IsInstitution(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_institutionModelField, building);
				if (model == null) return null;

				var effects = _institutionModelActiveEffectsField?.GetValue(model) as Array;
				if (effects == null || effectIndex >= effects.Length) return null;

				var effectModel = effects.GetValue(effectIndex);
				var effect = ReflectionHelper.GetField(_institutionEffectModelEffectField, effectModel);
				if (effect == null) return null;

				var descProp = effect.GetType().GetProperty("Description", GameReflection.PublicInstance);
				return descProp?.GetValue(effect) as string;
			} catch {
				return null;
			}
		}

		public static bool IsEffectActive(object building, int effectIndex) {
			if (!IsInstitution(building)) return false;
			EnsureTypes();

			try {
				int currentWorkers = BuildingReflection.GetWorkerCount(building);
				int minWorkers = GetEffectMinWorkers(building, effectIndex);
				return currentWorkers >= minWorkers;
			} catch {
				return false;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(InstitutionReflection), "InstitutionReflection");
		}
	}
}
