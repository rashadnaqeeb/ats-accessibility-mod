using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing GoalsPopup (Deeds menu) data and interaction.
	/// </summary>
	public static class DeedsReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// MetaStateService access: MetaController.Instance.MetaServices.MetaStateService
		private static PropertyInfo _msMetaStateServiceProperty;

		// MetaStateService.Goals → MetaGoalsState
		private static PropertyInfo _mssGoalsProperty;

		// MetaGoalsState.goals → List<GoalState>
		private static FieldInfo _mgsGoalsField;

		// Settings.GetGoal(string) → GoalModel
		private static MethodInfo _settingsGetGoalMethod;

		// GoalModel fields/properties
		private static FieldInfo _goalModelLabelField;
		private static FieldInfo _goalModelDisplayNameField;
		private static PropertyInfo _goalModelDescriptionProperty;
		private static MethodInfo _goalModelGetMetaProgressTextMethod;
		private static FieldInfo _goalModelIsActiveField;
		private static FieldInfo _goalModelIsCycleGoalField;
		private static MethodInfo _goalModelHasAccessToMethod;
		private static FieldInfo _goalModelRewardsField;

		// MetaRewardModel.DisplayName
		private static PropertyInfo _rewardDisplayNameProperty;

		// GoalState fields
		private static FieldInfo _goalStateModelField;
		private static FieldInfo _goalStateCompletedField;
		private static FieldInfo _goalStateRewardedField;

		// GoalCategoryModel (LabelModel subclass) fields
		private static FieldInfo _categoryDisplayNameField;
		private static FieldInfo _categoryOrderField;
		private static FieldInfo _categoryIsHiddenField;

		// GoalsService access and RewardGoal method
		private static PropertyInfo _msGoalsServiceProperty;
		private static MethodInfo _gsRewardGoalMethod;

		// GoalsPopup type for detection
		private static Type _goalsPopupType;

		private static bool _typesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypesCached() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("DeedsReflection", assembly => {
				CacheMetaStateTypes(assembly);
				CacheGoalModelTypes(assembly);
				CacheGoalStateTypes(assembly);
				CacheCategoryTypes(assembly);
				CacheGoalsServiceTypes(assembly);
				CachePopupType(assembly);
			});
		}

		private static void CacheMetaStateTypes(Assembly assembly) {
			var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
			if (metaServicesType != null) {
				_msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
					GameReflection.PublicInstance);
				_msGoalsServiceProperty = metaServicesType.GetProperty("GoalsService",
					GameReflection.PublicInstance);
			}

			var metaStateServiceType = assembly.GetType("Eremite.Services.IMetaStateService");
			if (metaStateServiceType != null) {
				_mssGoalsProperty = metaStateServiceType.GetProperty("Goals",
					GameReflection.PublicInstance);
			}

			var metaGoalsStateType = assembly.GetType("Eremite.Model.State.MetaGoalsState")
				?? assembly.GetType("Eremite.Model.Goals.MetaGoalsState");
			if (metaGoalsStateType != null) {
				_mgsGoalsField = metaGoalsStateType.GetField("goals", GameReflection.PublicInstance);
			}
		}

		private static void CacheGoalModelTypes(Assembly assembly) {
			// Settings.GetGoal(string)
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetGoalMethod = settingsType.GetMethod("GetGoal",
					new[] { typeof(string) });
			}

			var goalModelType = assembly.GetType("Eremite.Model.Goals.GoalModel");
			if (goalModelType != null) {
				_goalModelLabelField = goalModelType.GetField("label", GameReflection.PublicInstance);
				_goalModelDisplayNameField = goalModelType.GetField("displayName", GameReflection.PublicInstance);
				_goalModelDescriptionProperty = goalModelType.GetProperty("Description", GameReflection.PublicInstance);
				_goalModelIsActiveField = goalModelType.GetField("isActive", GameReflection.PublicInstance);
				_goalModelIsCycleGoalField = goalModelType.GetField("isCycleGoal", GameReflection.PublicInstance);
				_goalModelHasAccessToMethod = goalModelType.GetMethod("HasAccessTo", GameReflection.PublicInstance);
				_goalModelRewardsField = goalModelType.GetField("rewards", GameReflection.PublicInstance);

				// GetMetaProgressText takes a GoalState parameter
				var goalStateType = assembly.GetType("Eremite.Model.Goals.GoalState");
				if (goalStateType != null) {
					_goalModelGetMetaProgressTextMethod = goalModelType.GetMethod("GetMetaProgressText",
						new[] { goalStateType });
				}
			}

			var rewardModelType = assembly.GetType("Eremite.Model.Meta.MetaRewardModel");
			if (rewardModelType != null) {
				_rewardDisplayNameProperty = rewardModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
			}
		}

		private static void CacheGoalStateTypes(Assembly assembly) {
			var goalStateType = assembly.GetType("Eremite.Model.Goals.GoalState");
			if (goalStateType != null) {
				_goalStateModelField = goalStateType.GetField("model", GameReflection.PublicInstance);
				_goalStateCompletedField = goalStateType.GetField("completed", GameReflection.PublicInstance);
				_goalStateRewardedField = goalStateType.GetField("rewarded", GameReflection.PublicInstance);
			}
		}

		private static void CacheCategoryTypes(Assembly assembly) {
			// GoalCategoryModel extends LabelModel
			var categoryType = assembly.GetType("Eremite.Model.Goals.GoalCategoryModel");
			if (categoryType != null) {
				_categoryIsHiddenField = categoryType.GetField("isHiddenCategory", GameReflection.PublicInstance);
				_categoryOrderField = categoryType.GetField("order", GameReflection.PublicInstance);
			}

			// LabelModel has displayName (base class of GoalCategoryModel)
			var labelModelType = assembly.GetType("Eremite.Model.LabelModel");
			if (labelModelType != null) {
				_categoryDisplayNameField = labelModelType.GetField("displayName", GameReflection.PublicInstance);
			}
		}

		private static void CacheGoalsServiceTypes(Assembly assembly) {
			var goalsServiceType = assembly.GetType("Eremite.Services.IGoalsService");
			if (goalsServiceType != null) {
				var goalStateType = assembly.GetType("Eremite.Model.Goals.GoalState");
				var goalModelType = assembly.GetType("Eremite.Model.Goals.GoalModel");
				if (goalStateType != null && goalModelType != null) {
					_gsRewardGoalMethod = goalsServiceType.GetMethod("RewardGoal",
						new[] { goalStateType, goalModelType });
				}
			}
		}

		private static void CachePopupType(Assembly assembly) {
			_goalsPopupType = assembly.GetType("Eremite.WorldMap.UI.Goals.GoalsPopup");
		}

		// ========================================
		// META SERVICE ACCESS
		// ========================================

		private static object GetMetaStateService() => GameReflection.GetMetaService(_msMetaStateServiceProperty);

		private static object GetGoalsService() => GameReflection.GetMetaService(_msGoalsServiceProperty);

		// ========================================
		// POPUP DETECTION
		// ========================================

		public static bool IsGoalsPopup(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			if (_goalsPopupType == null) return false;
			return _goalsPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// GOAL DATA ACCESS
		// ========================================

		/// <summary>
		/// Get all goal states from MetaStateService, paired with their resolved models.
		/// Filters out inaccessible, inactive, and cycle goals.
		/// </summary>
		public static List<(object state, object model)> GetAllGoalStates() {
			EnsureTypesCached();
			var result = new List<(object, object)>();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null || _mssGoalsProperty == null) return result;

				var goalsState = _mssGoalsProperty.GetValue(metaStateService);
				if (goalsState == null || _mgsGoalsField == null) return result;

				var goalsList = _mgsGoalsField.GetValue(goalsState) as IList;
				if (goalsList == null) return result;

				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsGetGoalMethod == null) return result;

				foreach (var state in goalsList) {
					if (state == null) continue;

					var modelName = _goalStateModelField?.GetValue(state) as string;
					if (string.IsNullOrEmpty(modelName)) continue;

					var model = _settingsGetGoalMethod.Invoke(settings, new object[] { modelName });
					if (model == null) continue;

					// Filter: must be active
					if (_goalModelIsActiveField != null) {
						var isActive = (bool)_goalModelIsActiveField.GetValue(model);
						if (!isActive) continue;
					}

					// Filter: must not be a cycle goal
					if (_goalModelIsCycleGoalField != null) {
						var isCycle = (bool)_goalModelIsCycleGoalField.GetValue(model);
						if (isCycle) continue;
					}

					// Filter: must have access (DLC/demo check)
					if (_goalModelHasAccessToMethod != null) {
						var hasAccess = (bool)_goalModelHasAccessToMethod.Invoke(model, null);
						if (!hasAccess) continue;
					}

					// Filter: hidden categories only show completed goals
					var category = GetGoalCategory(model);
					if (category != null && IsCategoryHidden(category)) {
						if (!IsGoalCompleted(state)) continue;
					}

					result.Add((state, model));
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] DeedsReflection: GetAllGoalStates failed: {ex.Message}");
			}

			return result;
		}

		public static string GetGoalName(object model) {
			if (model == null) return Strings.Get("common.unknown");
			return ReflectionHelper.GetLocaString(_goalModelDisplayNameField, model) ?? Strings.Get("common.unknown");
		}

		public static string GetGoalDescription(object model) {
			if (model == null) return "";
			return ReflectionHelper.GetPropString(_goalModelDescriptionProperty, model) ?? "";
		}

		public static string GetGoalProgressText(object model, object state) {
			if (model == null || state == null) return "";
			return ReflectionHelper.InvokeString(_goalModelGetMetaProgressTextMethod, model, state) ?? "";
		}

		public static bool IsGoalCompleted(object state) {
			return ReflectionHelper.GetBool(_goalStateCompletedField, state);
		}

		public static bool IsGoalRewarded(object state) {
			return ReflectionHelper.GetBool(_goalStateRewardedField, state);
		}

		public static object GetGoalCategory(object model) {
			return ReflectionHelper.GetField(_goalModelLabelField, model);
		}

		public static string GetCategoryName(object category) {
			if (category == null) return Strings.Get("common.unknown");
			return ReflectionHelper.GetLocaString(_categoryDisplayNameField, category) ?? Strings.Get("common.unknown");
		}

		public static int GetCategoryOrder(object category) {
			return ReflectionHelper.GetInt(_categoryOrderField, category);
		}

		public static bool IsCategoryHidden(object category) {
			return ReflectionHelper.GetBool(_categoryIsHiddenField, category);
		}

		/// <summary>
		/// Get the display names of all rewards for a goal model.
		/// </summary>
		public static string[] GetRewardNames(object model) {
			EnsureTypesCached();
			if (model == null || _goalModelRewardsField == null) return new string[0];

			try {
				var rewards = _goalModelRewardsField.GetValue(model) as Array;
				if (rewards == null || rewards.Length == 0) return new string[0];

				var names = new List<string>();
				foreach (var reward in rewards) {
					if (reward == null) continue;

					string name = null;
					if (_rewardDisplayNameProperty != null) {
						name = _rewardDisplayNameProperty.GetValue(reward) as string;
					}

					if (!string.IsNullOrEmpty(name))
						names.Add(name);
				}

				return names.ToArray();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] DeedsReflection: GetRewardNames failed: {ex.Message}");
				return new string[0];
			}
		}

		/// <summary>
		/// Claim a completed goal's reward.
		/// Returns false if GoalsService or method not found.
		/// </summary>
		public static bool ClaimGoal(object state, object model) {
			EnsureTypesCached();
			if (state == null || model == null) return false;
			if (_gsRewardGoalMethod == null) return false;

			var goalsService = GetGoalsService();
			if (goalsService == null) return false;

			return ReflectionHelper.InvokeVoid(_gsRewardGoalMethod, goalsService, state, model);
		}

		/// <summary>
		/// Check if the player is currently in a game (settlement).
		/// </summary>
		public static bool IsInGame() {
			return GameReflection.GetIsGameActive();
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(DeedsReflection), "DeedsReflection");
		}
	}
}
