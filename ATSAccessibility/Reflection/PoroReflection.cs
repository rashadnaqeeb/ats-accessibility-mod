using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Poro-specific reflection (companion creature buildings).
	/// Extracted from BuildingReflection.cs — routing type-check (IsPoro) stays there.
	/// </summary>
	public static class PoroReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		internal static Type _poroType = null;
		private static FieldInfo _poroStateField = null;
		private static FieldInfo _poroModelField = null;
		private static FieldInfo _poroStateNeedsField = null;
		private static FieldInfo _poroModelNeedsField = null;
		private static FieldInfo _poroStateHappinessField = null;
		private static FieldInfo _poroStateProductionProgressField = null;
		private static FieldInfo _poroStateProductField = null;
		private static FieldInfo _poroModelProductField = null;
		private static FieldInfo _poroModelMaxProductsField = null;
		private static FieldInfo _poroNeedStateLevelField = null;
		private static FieldInfo _poroNeedStatePickedGoodField = null;
		private static FieldInfo _poroNeedModelDisplayNameField = null;
		private static FieldInfo _poroNeedModelGoodsField = null;
		private static MethodInfo _poroCanFulfillMethod = null;
		private static MethodInfo _poroFulfillMethod = null;
		private static MethodInfo _poroCanGatherProductsMethod = null;
		private static MethodInfo _poroGatherProductsMethod = null;
		private static MethodInfo _poroGoodChangedMethod = null;
		private static MethodInfo _poroGetCurrentGoodForMethod = null;
		private static bool _poroTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		internal static void EnsureTypes() {
			if (_poroTypesCached) return;
			_poroTypesCached = true;

			ReflectionHelper.InitCache("PoroReflection", assembly => {
				_poroType = assembly.GetType("Eremite.Buildings.Poro");
				if (_poroType != null) {
					_poroStateField = _poroType.GetField("state", GameReflection.PublicInstance);
					_poroModelField = _poroType.GetField("model", GameReflection.PublicInstance);
					_poroCanFulfillMethod = _poroType.GetMethod("CanFulfill", GameReflection.PublicInstance);
					_poroFulfillMethod = _poroType.GetMethod("Fulfill", GameReflection.PublicInstance);
					_poroCanGatherProductsMethod = _poroType.GetMethod("CanGatherProducts", GameReflection.PublicInstance);
					_poroGatherProductsMethod = _poroType.GetMethod("GatherProducts", GameReflection.PublicInstance);
					_poroGoodChangedMethod = _poroType.GetMethod("GoodChanged", GameReflection.PublicInstance);
					_poroGetCurrentGoodForMethod = _poroType.GetMethod("GetCurrentGoodFor", GameReflection.PublicInstance);
				}

				var poroStateType = assembly.GetType("Eremite.Buildings.PoroState");
				if (poroStateType != null) {
					_poroStateNeedsField = poroStateType.GetField("needs", GameReflection.PublicInstance);
					_poroStateHappinessField = poroStateType.GetField("happiness", GameReflection.PublicInstance);
					_poroStateProductionProgressField = poroStateType.GetField("productionProgress", GameReflection.PublicInstance);
					_poroStateProductField = poroStateType.GetField("product", GameReflection.PublicInstance);
				}

				var poroModelType = assembly.GetType("Eremite.Buildings.PoroModel");
				if (poroModelType != null) {
					_poroModelNeedsField = poroModelType.GetField("needs", GameReflection.PublicInstance);
					_poroModelProductField = poroModelType.GetField("product", GameReflection.PublicInstance);
					_poroModelMaxProductsField = poroModelType.GetField("maxProducts", GameReflection.PublicInstance);
				}

				var poroNeedStateType = assembly.GetType("Eremite.Buildings.PoroNeedState");
				if (poroNeedStateType != null) {
					_poroNeedStateLevelField = poroNeedStateType.GetField("level", GameReflection.PublicInstance);
					_poroNeedStatePickedGoodField = poroNeedStateType.GetField("pickedGood", GameReflection.PublicInstance);
				}

				var poroNeedModelType = assembly.GetType("Eremite.Buildings.PoroNeedModel");
				if (poroNeedModelType != null) {
					_poroNeedModelDisplayNameField = poroNeedModelType.GetField("displayName", GameReflection.PublicInstance);
					_poroNeedModelGoodsField = poroNeedModelType.GetField("goods", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// PUBLIC API
		// ========================================

		public static bool IsPoro(object building) {
			if (building == null) return false;
			EnsureTypes();
			if (_poroType == null) return false;
			return _poroType.IsInstanceOfType(building);
		}

		public static float GetHappiness(object building) {
			if (!IsPoro(building)) return 0f;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				if (state == null) return 0f;

				return (float?)_poroStateHappinessField?.GetValue(state) ?? 0f;
			} catch {
				return 0f;
			}
		}

		public static float GetProductionProgress(object building) {
			if (!IsPoro(building)) return 0f;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				if (state == null) return 0f;

				return (float?)_poroStateProductionProgressField?.GetValue(state) ?? 0f;
			} catch {
				return 0f;
			}
		}

		public static int GetNeedCount(object building) {
			if (!IsPoro(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return 0;

				var needs = _poroModelNeedsField?.GetValue(model) as Array;
				return needs?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetNeedName(object building, int needIndex) {
			if (!IsPoro(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return null;

				var needs = _poroModelNeedsField?.GetValue(model) as Array;
				if (needs == null || needIndex >= needs.Length) return null;

				var needModel = needs.GetValue(needIndex);
				var displayName = ReflectionHelper.GetField(_poroNeedModelDisplayNameField, needModel);
				return GameReflection.GetLocaText(displayName);
			} catch {
				return null;
			}
		}

		public static float GetNeedLevel(object building, int needIndex) {
			if (!IsPoro(building)) return 0f;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				if (state == null) return 0f;

				var needs = _poroStateNeedsField?.GetValue(state) as Array;
				if (needs == null || needIndex >= needs.Length) return 0f;

				var needState = needs.GetValue(needIndex);
				return (float?)_poroNeedStateLevelField?.GetValue(needState) ?? 0f;
			} catch {
				return 0f;
			}
		}

		public static string GetNeedCurrentGoodName(object building, int needIndex) {
			if (!IsPoro(building)) return null;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (state == null || model == null) return null;

				var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
				var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
				if (stateNeeds == null || modelNeeds == null) return null;
				if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return null;

				var needState = stateNeeds.GetValue(needIndex);
				var needModel = modelNeeds.GetValue(needIndex);

				var good = ReflectionHelper.Invoke(_poroGetCurrentGoodForMethod, building, needState, needModel);
				if (good == null) return null;

				var goodName = good.GetType().GetField("name", GameReflection.PublicInstance)?.GetValue(good) as string;
				if (string.IsNullOrEmpty(goodName)) return null;

				return BuildingReflection.GetGoodDisplayName(goodName);
			} catch {
				return null;
			}
		}

		public static int GetNeedAvailableGoodsCount(object building, int needIndex) {
			if (!IsPoro(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return 0;

				var needs = _poroModelNeedsField?.GetValue(model) as Array;
				if (needs == null || needIndex >= needs.Length) return 0;

				var needModel = needs.GetValue(needIndex);
				var goodsSet = ReflectionHelper.GetField(_poroNeedModelGoodsField, needModel);
				if (goodsSet == null) return 0;

				var goodsArray = goodsSet.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(goodsSet) as Array;
				return goodsArray?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetNeedAvailableGoodName(object building, int needIndex, int goodIndex) {
			if (!IsPoro(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return null;

				var needs = _poroModelNeedsField?.GetValue(model) as Array;
				if (needs == null || needIndex >= needs.Length) return null;

				var needModel = needs.GetValue(needIndex);
				var goodsSet = ReflectionHelper.GetField(_poroNeedModelGoodsField, needModel);
				if (goodsSet == null) return null;

				var goodsArray = goodsSet.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(goodsSet) as Array;
				if (goodsArray == null || goodIndex >= goodsArray.Length) return null;

				var goodRef = goodsArray.GetValue(goodIndex);
				return BuildingReflection.GetGoodRefDisplayName(goodRef);
			} catch {
				return null;
			}
		}

		public static bool CanFulfillNeed(object building, int needIndex) {
			if (!IsPoro(building)) return false;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (state == null || model == null) return false;

				var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
				var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
				if (stateNeeds == null || modelNeeds == null) return false;
				if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return false;

				var needState = stateNeeds.GetValue(needIndex);
				var needModel = modelNeeds.GetValue(needIndex);

				var result = ReflectionHelper.Invoke(_poroCanFulfillMethod, building, needState, needModel);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		public static bool FulfillNeed(object building, int needIndex) {
			if (!IsPoro(building)) return false;
			EnsureTypes();

			try {
				if (!CanFulfillNeed(building, needIndex))
					return false;

				var state = ReflectionHelper.GetField(_poroStateField, building);
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (state == null || model == null) return false;

				var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
				var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
				if (stateNeeds == null || modelNeeds == null) return false;
				if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return false;

				var needState = stateNeeds.GetValue(needIndex);
				var needModel = modelNeeds.GetValue(needIndex);

				ReflectionHelper.InvokeVoid(_poroFulfillMethod, building, needState, needModel);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PoroReflection.FulfillNeed failed: {ex.Message}");
				return false;
			}
		}

		public static bool ChangeNeedGood(object building, int needIndex, int goodIndex) {
			if (!IsPoro(building)) return false;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				if (state == null) return false;

				var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
				if (stateNeeds == null || needIndex >= stateNeeds.Length) return false;

				var needState = stateNeeds.GetValue(needIndex);
				ReflectionHelper.InvokeVoid(_poroGoodChangedMethod, building, needState, goodIndex);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PoroReflection.ChangeNeedGood failed: {ex.Message}");
				return false;
			}
		}

		public static string GetProductName(object building) {
			if (!IsPoro(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return null;

				var productRef = ReflectionHelper.GetField(_poroModelProductField, model);
				return BuildingReflection.GetGoodRefDisplayName(productRef);
			} catch {
				return null;
			}
		}

		public static int GetProductAmount(object building) {
			if (!IsPoro(building)) return 0;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_poroStateField, building);
				if (state == null) return 0;

				var product = ReflectionHelper.GetField(_poroStateProductField, state);
				if (product == null) return 0;

				return (int?)product.GetType().GetField("amount", GameReflection.PublicInstance)?.GetValue(product) ?? 0;
			} catch {
				return 0;
			}
		}

		public static int GetMaxProducts(object building) {
			if (!IsPoro(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_poroModelField, building);
				if (model == null) return 0;

				return ReflectionHelper.GetInt(_poroModelMaxProductsField, model);
			} catch {
				return 0;
			}
		}

		public static bool CanGatherProducts(object building) {
			if (!IsPoro(building)) return false;
			EnsureTypes();

			try {
				var result = ReflectionHelper.Invoke(_poroCanGatherProductsMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		public static bool GatherProducts(object building) {
			if (!IsPoro(building)) return false;
			EnsureTypes();

			try {
				if (!CanGatherProducts(building))
					return false;

				ReflectionHelper.InvokeVoid(_poroGatherProductsMethod, building);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PoroReflection.GatherProducts failed: {ex.Message}");
				return false;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(PoroReflection), "PoroReflection");
		}
	}
}
