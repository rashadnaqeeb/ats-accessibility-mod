using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection-based access to Relic building internals (glade events).
	/// Follows the same caching pattern as BuildingReflection.cs.
	/// BuildingReflection.IsRelic() is kept there for routing use by BuildingPanelHandler.
	/// </summary>
	public static class RelicReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// Relic base fields (state, model, methods)
		private static FieldInfo _relicStateField = null;  // Relic.state
		private static FieldInfo _relicModelField = null;  // Relic.model
		private static FieldInfo _relicStateInvestigationStartedField = null;  // RelicState.investigationStarted
		private static FieldInfo _relicStateInvestigationFinishedField = null;  // RelicState.investigationFinished
		private static FieldInfo _relicStateWorkProgressField = null;  // RelicState.workProgress
		private static FieldInfo _relicStateRelicGoodsField = null;  // RelicState.relicGoods
		private static FieldInfo _relicStateRewardsField = null;  // RelicState.rewards
		private static FieldInfo _relicStateWorkersField = null;  // RelicState.workers
		private static MethodInfo _relicGetExpectedWorkingTimeLeftMethod = null;  // Relic.GetExpectedWorkingTimeLeft()
		private static PropertyInfo _relicDifficultyProperty = null;  // Relic.Difficulty

		// Relic action methods
		private static MethodInfo _relicStartInvestigationMethod = null;  // Relic.StartInvestigation()
		private static MethodInfo _relicCancelMethod = null;  // Relic.Cancel()
		private static MethodInfo _relicCanCancelMethod = null;  // Relic.CanCancel()
		private static MethodInfo _relicHasAnyWorkplaceMethod = null;  // Relic.HasAnyWorkplace()
		private static MethodInfo _relicHasOrderMethod = null;  // Relic.HasOrder()
		private static MethodInfo _relicIsOrderCompletedMethod = null;  // Relic.IsOrderCompleted()
		private static MethodInfo _relicGetWorkingEffectsMethod = null;  // Relic.GetWorkingEffects()
		private static MethodInfo _relicGetSafeDecisionIndexMethod = null;  // Relic.GetSafeDecisionIndex()
		private static MethodInfo _relicIsLastDynamicEffectReachedMethod = null;  // Relic.IsLastDynamicEffectReached()
		private static MethodInfo _relicGetCurrentDynamicEffectsMethod = null;  // Relic.GetCurrentDynamicEffects()

		// RelicState decision/goods fields
		private static FieldInfo _relicStateDecisionIndexField = null;  // RelicState.decisionIndex (int)
		private static FieldInfo _relicStatePickedGoodsField = null;  // RelicState.pickedGoods (int[][])
		private static FieldInfo _relicStateRewardsSetsField = null;  // RelicState.rewardsSets (List<string>[])
		private static FieldInfo _relicStateRewardsTiersField = null;  // RelicState.rewardsTiers (List<string>[])
		private static FieldInfo _relicStateCurrentDynamicRewardField = null;  // RelicState.currentDynamicReward (int)

		// RelicState dynamic effects
		private static FieldInfo _relicStateCurrentDynamicEffectField = null;  // RelicState.currentDynamicEffect (int)
		private static FieldInfo _relicStateNextDynamicEffectChangeField = null;  // RelicState.nextDynamicEffectChange (float)

		// RelicModel fields
		private static FieldInfo _relicModelDifficultiesField = null;  // RelicModel.difficulties (RelicDifficulty[])
		private static FieldInfo _relicModelDecisionsRewardsField = null;  // RelicModel.decisionsRewards (EffectsTable[])
		private static FieldInfo _relicModelHasDynamicRewardsField = null;  // RelicModel.hasDynamicRewards (bool)
		private static FieldInfo _relicModelActiveEffectsField = null;  // RelicModel.activeEffects (EffectModel[])
		private static FieldInfo _relicModelAreEffectsPermanentField = null;  // RelicModel.areEffectsPermanent (bool)
		private static FieldInfo _relicModelForceRequirementsField = null;  // RelicModel.forceRequirements (bool)
		private static FieldInfo _relicModelWorkplacesField = null;  // RelicModel.workplaces (WorkplaceModel[])
		private static PropertyInfo _relicModelHasDecisionProperty = null;  // RelicModel.HasDecision (bool)
		private static FieldInfo _relicModelDangerLevelField = null;  // RelicModel.dangerLevel (DangerLevel enum)
		private static FieldInfo _relicModelHasDynamicEffectsField = null;  // RelicModel.hasDynamicEffects (bool)
		private static FieldInfo _relicModelEffectsTiersField = null;  // RelicModel.effectsTiers (EffectStep[])

		// RelicDifficulty
		private static FieldInfo _relicDifficultyDecisionsField = null;  // RelicDifficulty.decisions (RelicDecision[])

		// RelicDecision fields
		private static FieldInfo _relicDecisionLabelField = null;  // RelicDecision.label (LabelModel)
		private static FieldInfo _relicDecisionWorkingTimeField = null;  // RelicDecision.workingTime (float)
		private static FieldInfo _relicDecisionWorkingEffectsField = null;  // RelicDecision.workingEffects (EffectModel[])
		private static FieldInfo _relicDecisionReqGoodsField = null;  // RelicDecision.requriedGoods (GoodsSetTable)
		private static FieldInfo _relicDecisionDecisionTagField = null;  // RelicDecision.decisionTag (DecisionTag)

		// GoodsSetTable
		private static FieldInfo _goodsSetTableSetsField = null;  // GoodsSetTable.sets (GoodsSet[])

		// LabelModel / DecisionTag
		private static FieldInfo _labelModelDisplayNameField = null;  // LabelModel.displayName (LocaText)
		private static FieldInfo _decisionTagDisplayNameField = null;  // DecisionTag.displayName (LocaText)

		// Relic reward storage
		private static MethodInfo _lockedGoodsGetFullAmountMethod = null;  // LockedGoodsCollection.GetFullAmount(string)
		private static MethodInfo _lockedGoodsFullSumMethod = null;  // LockedGoodsCollection.FullSum()

		// Relic sound fields
		private static FieldInfo _investigationStartSoundField = null;  // RelicModel.investigationStartSound (SoundRef)

		private static bool _relicBaseFieldsCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureRelicBaseFields() {
			if (_relicBaseFieldsCached) return;
			_relicBaseFieldsCached = true;

			// Ensure the Relic type is cached in BuildingReflection (also used by IsRelic)
			BuildingReflection.EnsureRelicBaseType();

			ReflectionHelper.InitCache("RelicReflection.Relic", assembly => {
				var relicType = BuildingReflection.RelicType;
				if (relicType != null) {
					_relicStateField = relicType.GetField("state", GameReflection.PublicInstance);
					_relicModelField = relicType.GetField("model", GameReflection.PublicInstance);
					_relicGetExpectedWorkingTimeLeftMethod = relicType.GetMethod("GetExpectedWorkingTimeLeft", GameReflection.PublicInstance);
					_relicDifficultyProperty = relicType.GetProperty("Difficulty", GameReflection.PublicInstance);

					// Action methods
					_relicStartInvestigationMethod = relicType.GetMethod("StartInvestigation", GameReflection.PublicInstance);
					_relicCancelMethod = relicType.GetMethod("Cancel", GameReflection.PublicInstance);
					_relicCanCancelMethod = relicType.GetMethod("CanCancel", GameReflection.PublicInstance);
					_relicHasAnyWorkplaceMethod = relicType.GetMethod("HasAnyWorkplace", GameReflection.PublicInstance);
					_relicHasOrderMethod = relicType.GetMethod("HasOrder", GameReflection.PublicInstance);
					_relicIsOrderCompletedMethod = relicType.GetMethod("IsOrderCompleted", GameReflection.PublicInstance);
					_relicGetWorkingEffectsMethod = relicType.GetMethod("GetWorkingEffects", GameReflection.PublicInstance);
					_relicGetSafeDecisionIndexMethod = relicType.GetMethod("GetSafeDecisionIndex", GameReflection.PublicInstance);
					_relicIsLastDynamicEffectReachedMethod = relicType.GetMethod("IsLastDynamicEffectReached", GameReflection.PublicInstance);
					_relicGetCurrentDynamicEffectsMethod = relicType.GetMethod("GetCurrentDynamicEffects", GameReflection.PublicInstance);
				}

				var relicStateType = assembly.GetType("Eremite.Buildings.RelicState");
				if (relicStateType != null) {
					_relicStateInvestigationStartedField = relicStateType.GetField("investigationStarted", GameReflection.PublicInstance);
					_relicStateInvestigationFinishedField = relicStateType.GetField("investigationFinished", GameReflection.PublicInstance);
					_relicStateWorkProgressField = relicStateType.GetField("workProgress", GameReflection.PublicInstance);
					_relicStateRelicGoodsField = relicStateType.GetField("relicGoods", GameReflection.PublicInstance);
					_relicStateRewardsField = relicStateType.GetField("rewards", GameReflection.PublicInstance);
					_relicStateWorkersField = relicStateType.GetField("workers", GameReflection.PublicInstance);
					_relicStateDecisionIndexField = relicStateType.GetField("decisionIndex", GameReflection.PublicInstance);
					_relicStatePickedGoodsField = relicStateType.GetField("pickedGoods", GameReflection.PublicInstance);
					_relicStateRewardsSetsField = relicStateType.GetField("rewardsSets", GameReflection.PublicInstance);
					_relicStateRewardsTiersField = relicStateType.GetField("rewardsTiers", GameReflection.PublicInstance);
					_relicStateCurrentDynamicRewardField = relicStateType.GetField("currentDynamicReward", GameReflection.PublicInstance);
					_relicStateCurrentDynamicEffectField = relicStateType.GetField("currentDynamicEffect", GameReflection.PublicInstance);
					_relicStateNextDynamicEffectChangeField = relicStateType.GetField("nextDynamicEffectChange", GameReflection.PublicInstance);
				}

				var relicModelType = assembly.GetType("Eremite.Buildings.RelicModel");
				if (relicModelType != null) {
					_relicModelDifficultiesField = relicModelType.GetField("difficulties", GameReflection.PublicInstance);
					_relicModelDecisionsRewardsField = relicModelType.GetField("decisionsRewards", GameReflection.PublicInstance);
					_relicModelHasDynamicRewardsField = relicModelType.GetField("hasDynamicRewards", GameReflection.PublicInstance);
					_relicModelActiveEffectsField = relicModelType.GetField("activeEffects", GameReflection.PublicInstance);
					_relicModelAreEffectsPermanentField = relicModelType.GetField("areEffectsPermanent", GameReflection.PublicInstance);
					_relicModelForceRequirementsField = relicModelType.GetField("forceRequirements", GameReflection.PublicInstance);
					_relicModelWorkplacesField = relicModelType.GetField("workplaces", GameReflection.PublicInstance);
					_relicModelHasDecisionProperty = relicModelType.GetProperty("HasDecision", GameReflection.PublicInstance);
					_investigationStartSoundField = relicModelType.GetField("investigationStartSound", GameReflection.PublicInstance);
					_relicModelDangerLevelField = relicModelType.GetField("dangerLevel", GameReflection.PublicInstance);
					_relicModelHasDynamicEffectsField = relicModelType.GetField("hasDynamicEffects", GameReflection.PublicInstance);
					_relicModelEffectsTiersField = relicModelType.GetField("effectsTiers", GameReflection.PublicInstance);
				}

				// RelicDifficulty fields
				var relicDifficultyType = assembly.GetType("Eremite.Buildings.RelicDifficulty");
				if (relicDifficultyType != null) {
					_relicDifficultyDecisionsField = relicDifficultyType.GetField("decisions", GameReflection.PublicInstance);
				}

				// RelicDecision fields
				var relicDecisionType = assembly.GetType("Eremite.Buildings.RelicDecision");
				if (relicDecisionType != null) {
					_relicDecisionLabelField = relicDecisionType.GetField("label", GameReflection.PublicInstance);
					_relicDecisionWorkingTimeField = relicDecisionType.GetField("workingTime", GameReflection.PublicInstance);
					_relicDecisionWorkingEffectsField = relicDecisionType.GetField("workingEffects", GameReflection.PublicInstance);
					_relicDecisionReqGoodsField = relicDecisionType.GetField("requriedGoods", GameReflection.PublicInstance);
					_relicDecisionDecisionTagField = relicDecisionType.GetField("decisionTag", GameReflection.PublicInstance);
				}

				// GoodsSetTable.sets
				var goodsSetTableType = assembly.GetType("Eremite.Model.GoodsSetTable");
				if (goodsSetTableType != null) {
					_goodsSetTableSetsField = goodsSetTableType.GetField("sets", GameReflection.PublicInstance);
				}

				// LabelModel.displayName
				var labelModelType = assembly.GetType("Eremite.Model.LabelModel");
				if (labelModelType != null) {
					_labelModelDisplayNameField = labelModelType.GetField("displayName", GameReflection.PublicInstance);
				}

				// DecisionTag.displayName
				var decisionTagType = assembly.GetType("Eremite.Model.DecisionTag");
				if (decisionTagType != null) {
					_decisionTagDisplayNameField = decisionTagType.GetField("displayName", GameReflection.PublicInstance);
				}

				// LockedGoodsCollection methods for reward storage
				var lockedGoodsType = assembly.GetType("Eremite.LockedGoodsCollection");
				if (lockedGoodsType != null) {
					_lockedGoodsGetFullAmountMethod = lockedGoodsType.GetMethod("GetFullAmount", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
					_lockedGoodsFullSumMethod = lockedGoodsType.GetMethod("FullSum", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// STRUCTS
		// ========================================

		public struct RelicEffectInfo {
			public string Name;
			public string Description;
			public bool IsPositive;
		}

		public struct RelicRewardInfo {
			public string Name;
			public string Description;
		}

		// ========================================
		// PUBLIC API - RELIC
		// ========================================

		/// <summary>
		/// Get the danger level of a relic as a display string.
		/// Returns "None", "Negative", "Dangerous", or "Forbidden".
		/// </summary>
		public static string GetRelicDangerLevel(object building) {
			if (!BuildingReflection.IsRelic(building)) return null;

			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return null;

				var dangerLevel = ReflectionHelper.GetField(_relicModelDangerLevelField, model);
				if (dangerLevel == null) return null;

				// DangerLevel is an enum: None=0, Negative=1, Dangerous=2, Forbidden=3
				return dangerLevel.ToString();
			} catch {
				return null;
			}
		}

		public static bool IsRelicInvestigationStarted(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;

			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_relicStateInvestigationStartedField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if Relic investigation is finished.
		/// </summary>
		public static bool IsRelicInvestigationFinished(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;

			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_relicStateInvestigationFinishedField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get Relic work progress (0-1).
		/// </summary>
		public static float GetRelicProgress(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0f;

			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0f;

				return (float?)_relicStateWorkProgressField?.GetValue(state) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get expected time remaining for Relic work in seconds.
		/// </summary>
		public static float GetRelicTimeLeft(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0f;

			EnsureRelicBaseFields();

			try {
				return ReflectionHelper.InvokeFloat(_relicGetExpectedWorkingTimeLeftMethod, building);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get worker IDs for a Relic.
		/// </summary>
		public static int[] GetRelicWorkerIds(object building) {
			if (!BuildingReflection.IsRelic(building)) return new int[0];

			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return new int[0];

				return _relicStateWorkersField?.GetValue(state) as int[] ?? new int[0];
			} catch {
				return new int[0];
			}
		}

		// ========================================
		// PUBLIC API - RELIC DECISIONS & EVENTS
		// ========================================

		/// <summary>
		/// Check if relic model has multiple decisions (HasDecision property).
		/// </summary>
		public static bool RelicHasMultipleDecisions(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return false;
				return ReflectionHelper.GetPropBool(_relicModelHasDecisionProperty, model);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get number of decisions for the current difficulty.
		/// </summary>
		public static int GetRelicDecisionCount(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var difficulty = ReflectionHelper.GetProp(_relicDifficultyProperty, building);
				if (difficulty == null) return 0;

				var decisions = _relicDifficultyDecisionsField?.GetValue(difficulty) as Array;
				return decisions?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the current decision index from state (-1 means none selected).
		/// </summary>
		public static int GetRelicDecisionIndex(object building) {
			if (!BuildingReflection.IsRelic(building)) return -1;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return -1;
				return (int?)_relicStateDecisionIndexField?.GetValue(state) ?? -1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Set the decision index on the relic state.
		/// </summary>
		public static bool SetRelicDecisionIndex(object building, int index) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null || _relicStateDecisionIndexField == null) return false;
				_relicStateDecisionIndexField.SetValue(state, index);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the label text for a decision at the given index.
		/// </summary>
		public static string GetRelicDecisionLabel(object building, int decisionIndex) {
			if (!BuildingReflection.IsRelic(building)) return null;
			EnsureRelicBaseFields();

			try {
				var difficulty = ReflectionHelper.GetProp(_relicDifficultyProperty, building);
				if (difficulty == null) return null;

				var decisions = _relicDifficultyDecisionsField?.GetValue(difficulty) as Array;
				if (decisions == null || decisionIndex < 0 || decisionIndex >= decisions.Length) return null;

				var decision = decisions.GetValue(decisionIndex);
				if (decision == null) return null;

				// Get label text
				var label = ReflectionHelper.GetField(_relicDecisionLabelField, decision);
				string labelText = null;
				if (label != null) {
					var displayNameLoca = ReflectionHelper.GetField(_labelModelDisplayNameField, label);
					if (displayNameLoca != null)
						labelText = GameReflection.GetLocaText(displayNameLoca);
				}

				// Get decision tag text
				var decisionTag = ReflectionHelper.GetField(_relicDecisionDecisionTagField, decision);
				string tagText = null;
				if (decisionTag != null) {
					var tagDisplayNameLoca = ReflectionHelper.GetField(_decisionTagDisplayNameField, decisionTag);
					if (tagDisplayNameLoca != null)
						tagText = GameReflection.GetLocaText(tagDisplayNameLoca);
				}

				if (!string.IsNullOrEmpty(tagText) && !string.IsNullOrEmpty(labelText))
					return $"{labelText} ({tagText})";
				return labelText ?? tagText ?? $"Decision {decisionIndex + 1}";
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the working time for a decision at the given index.
		/// </summary>
		public static float GetRelicDecisionWorkingTime(object building, int decisionIndex) {
			if (!BuildingReflection.IsRelic(building)) return 0f;
			EnsureRelicBaseFields();

			try {
				var difficulty = ReflectionHelper.GetProp(_relicDifficultyProperty, building);
				if (difficulty == null) return 0f;

				var decisions = _relicDifficultyDecisionsField?.GetValue(difficulty) as Array;
				if (decisions == null || decisionIndex < 0 || decisionIndex >= decisions.Length) return 0f;

				var decision = decisions.GetValue(decisionIndex);
				if (decision == null) return 0f;

				return (float?)_relicDecisionWorkingTimeField?.GetValue(decision) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get the number of goods sets (requirement groups) for a given decision.
		/// </summary>
		public static int GetRelicGoodsSetCount(object building, int decisionIndex) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var decision = GetRelicDecisionObject(building, decisionIndex);
				if (decision == null) return 0;

				var reqGoods = ReflectionHelper.GetField(_relicDecisionReqGoodsField, decision);
				if (reqGoods == null) return 0;

				var sets = _goodsSetTableSetsField?.GetValue(reqGoods) as Array;
				return sets?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the number of alternative goods in a goods set.
		/// </summary>
		public static int GetRelicGoodsAlternativeCount(object building, int decisionIndex, int setIndex) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var goodsSet = GetRelicGoodsSetObject(building, decisionIndex, setIndex);
				if (goodsSet == null) return 0;

				var goods = BuildingReflection.GoodsSetGoodsField?.GetValue(goodsSet) as Array;
				return goods?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get display name of a good at [decisionIndex][setIndex][goodIndex].
		/// </summary>
		public static string GetRelicGoodDisplayName(object building, int decisionIndex, int setIndex, int goodIndex) {
			if (!BuildingReflection.IsRelic(building)) return null;
			EnsureRelicBaseFields();

			try {
				var goodRef = GetRelicGoodRefObject(building, decisionIndex, setIndex, goodIndex);
				if (goodRef == null) return null;

				return ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, goodRef);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the name (internal ID) of a good at [decisionIndex][setIndex][goodIndex].
		/// </summary>
		public static string GetRelicGoodName(object building, int decisionIndex, int setIndex, int goodIndex) {
			if (!BuildingReflection.IsRelic(building)) return null;
			EnsureRelicBaseFields();

			try {
				var goodRef = GetRelicGoodRefObject(building, decisionIndex, setIndex, goodIndex);
				if (goodRef == null) return null;

				return ReflectionHelper.GetPropString(BuildingReflection.GoodRefNameProperty, goodRef);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get amount of a good at [decisionIndex][setIndex][goodIndex].
		/// </summary>
		public static int GetRelicGoodAmount(object building, int decisionIndex, int setIndex, int goodIndex) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var goodRef = GetRelicGoodRefObject(building, decisionIndex, setIndex, goodIndex);
				if (goodRef == null) return 0;

				return ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, goodRef);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the picked good index for a goods set in the current decision.
		/// </summary>
		public static int GetRelicPickedGoodIndex(object building, int decisionIndex, int setIndex) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0;

				var pickedGoods = ReflectionHelper.GetField(_relicStatePickedGoodsField, state);
				if (pickedGoods == null) return 0;

				// pickedGoods is int[][]
				var outerArray = pickedGoods as int[][];
				if (outerArray == null || decisionIndex < 0 || decisionIndex >= outerArray.Length) return 0;

				var innerArray = outerArray[decisionIndex];
				if (innerArray == null || setIndex < 0 || setIndex >= innerArray.Length) return 0;

				return innerArray[setIndex];
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Set the picked good index for a goods set.
		/// </summary>
		public static bool SetRelicPickedGoodIndex(object building, int decisionIndex, int setIndex, int goodIndex) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return false;

				var pickedGoods = _relicStatePickedGoodsField?.GetValue(state) as int[][];
				if (pickedGoods == null || decisionIndex < 0 || decisionIndex >= pickedGoods.Length) return false;

				var innerArray = pickedGoods[decisionIndex];
				if (innerArray == null || setIndex < 0 || setIndex >= innerArray.Length) return false;

				innerArray[setIndex] = goodIndex;
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get working effects for the selected decision.
		/// </summary>
		public static RelicEffectInfo[] GetRelicWorkingEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return new RelicEffectInfo[0];
			EnsureRelicBaseFields();

			try {
				// Use Relic.GetWorkingEffects() which already handles difficulty/decision
				var effects = ReflectionHelper.Invoke(_relicGetWorkingEffectsMethod, building) as Array;
				if (effects == null || effects.Length == 0) return new RelicEffectInfo[0];

				return ExtractEffectInfos(effects);
			} catch {
				return new RelicEffectInfo[0];
			}
		}

		/// <summary>
		/// Get active effects from the relic model (static active effects).
		/// </summary>
		public static RelicEffectInfo[] GetRelicActiveEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return new RelicEffectInfo[0];
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return new RelicEffectInfo[0];

				var effects = _relicModelActiveEffectsField?.GetValue(model) as Array;
				if (effects == null || effects.Length == 0) return new RelicEffectInfo[0];

				return ExtractEffectInfos(effects);
			} catch {
				return new RelicEffectInfo[0];
			}
		}

		/// <summary>
		/// Check if relic effects are permanent.
		/// </summary>
		public static bool RelicAreEffectsPermanent(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return false;
				return ReflectionHelper.GetBool(_relicModelAreEffectsPermanentField, model);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if relic has dynamic rewards.
		/// </summary>
		public static bool RelicHasDynamicRewards(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return false;
				return ReflectionHelper.GetBool(_relicModelHasDynamicRewardsField, model);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get rewards for a given decision (from state.rewardsSets).
		/// Resolves effect names to display names via GameModelService.
		/// </summary>
		public static RelicRewardInfo[] GetRelicDecisionRewards(object building, int decisionIndex) {
			if (!BuildingReflection.IsRelic(building)) return new RelicRewardInfo[0];
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return new RelicRewardInfo[0];

				// Check if using dynamic rewards
				var model = ReflectionHelper.GetField(_relicModelField, building);
				bool hasDynamic = ReflectionHelper.GetBool(_relicModelHasDynamicRewardsField, model);

				if (hasDynamic) {
					// Dynamic rewards: read from state.rewardsTiers[currentDynamicReward]
					int currentTier = (int?)_relicStateCurrentDynamicRewardField?.GetValue(state) ?? -1;
					if (currentTier < 0) currentTier = 0;  // Default to first tier

					var rewardsTiers = _relicStateRewardsTiersField?.GetValue(state) as Array;
					if (rewardsTiers == null || currentTier >= rewardsTiers.Length) return new RelicRewardInfo[0];

					var tierList = rewardsTiers.GetValue(currentTier) as List<string>;
					if (tierList == null) return new RelicRewardInfo[0];

					return ResolveEffectNames(tierList);
				} else {
					// Decision-based rewards: read from state.rewardsSets[decisionIndex]
					var rewardsSets = _relicStateRewardsSetsField?.GetValue(state) as Array;
					if (rewardsSets == null || decisionIndex < 0 || decisionIndex >= rewardsSets.Length) return new RelicRewardInfo[0];

					var setList = rewardsSets.GetValue(decisionIndex) as List<string>;
					if (setList == null) return new RelicRewardInfo[0];

					return ResolveEffectNames(setList);
				}
			} catch {
				return new RelicRewardInfo[0];
			}
		}

		/// <summary>
		/// Check if the relic has any decision-based rewards (decisionsRewards array not empty).
		/// </summary>
		public static bool RelicHasDecisionRewards(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return false;

				var decisionsRewards = _relicModelDecisionsRewardsField?.GetValue(model) as Array;
				return decisionsRewards != null && decisionsRewards.Length > 0;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get delivered amount of a good for a relic (from state.relicGoods).
		/// </summary>
		public static int GetRelicDeliveredAmount(object building, string goodName) {
			if (!BuildingReflection.IsRelic(building) || string.IsNullOrEmpty(goodName)) return 0;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0;

				var relicGoods = ReflectionHelper.GetField(_relicStateRelicGoodsField, state);
				if (relicGoods == null) return 0;

				var getAmountMethod = BuildingReflection.GoodsCollectionGetAmountMethod;
				if (getAmountMethod == null) return 0;
				var result = getAmountMethod.Invoke(relicGoods, new object[] { goodName });
				return (int?)result ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Check if the relic has any workplace slots.
		/// </summary>
		public static bool RelicHasAnyWorkplace(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				if (_relicHasAnyWorkplaceMethod == null) return false;
				return (bool?)_relicHasAnyWorkplaceMethod.Invoke(building, null) ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if the relic can start investigation, returning a blocking reason if not.
		/// </summary>
		public static bool RelicCanStart(object building, out string blockingReason) {
			blockingReason = null;
			if (!BuildingReflection.IsRelic(building)) { blockingReason = "Not a relic"; return false; }
			EnsureRelicBaseFields();

			try {
				// Already started?
				bool started = ReflectionHelper.GetBool(_relicStateInvestigationStartedField, ReflectionHelper.GetField(_relicStateField, building));
				if (started) { blockingReason = "Already started"; return false; }

				// Decision required?
				bool hasMultipleDecisions = RelicHasMultipleDecisions(building);
				if (hasMultipleDecisions) {
					int decisionIndex = GetRelicDecisionIndex(building);
					if (decisionIndex < 0) {
						blockingReason = "Select a decision first";
						return false;
					}
				}

				// Force requirements check (for instant-goods relics without workplaces)
				var model = ReflectionHelper.GetField(_relicModelField, building);
				bool forceReqs = ReflectionHelper.GetBool(_relicModelForceRequirementsField, model);
				bool hasWorkplace = RelicHasAnyWorkplace(building);

				if (forceReqs && !hasWorkplace) {
					// Check if goods are available in storage
					int safeDecision = GetRelicSafeDecisionIndex(building);
					int setCount = GetRelicGoodsSetCount(building, safeDecision);
					for (int i = 0; i < setCount; i++) {
						int pickedIndex = GetRelicPickedGoodIndex(building, safeDecision, i);
						string goodName = GetRelicGoodName(building, safeDecision, i, pickedIndex);
						int amount = GetRelicGoodAmount(building, safeDecision, i, pickedIndex);
						if (!string.IsNullOrEmpty(goodName) && amount > 0) {
							int stored = BuildingReflection.GetStoredGoodAmount(goodName);
							if (stored < amount) {
								blockingReason = "Required goods not available";
								return false;
							}
						}
					}
				}

				// Order check
				bool hasOrder = ReflectionHelper.InvokeBool(_relicHasOrderMethod, building);
				if (hasOrder) {
					bool orderCompleted = ReflectionHelper.InvokeBool(_relicIsOrderCompletedMethod, building);
					if (!orderCompleted) {
						blockingReason = "Complete the order first";
						return false;
					}
				}

				return true;
			} catch (Exception ex) {
				blockingReason = $"Error: {ex.Message}";
				return false;
			}
		}

		/// <summary>
		/// Start the relic investigation.
		/// </summary>
		public static bool RelicStartInvestigation(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				if (_relicStartInvestigationMethod == null) return false;
				_relicStartInvestigationMethod.Invoke(building, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if the relic investigation can be cancelled.
		/// </summary>
		public static bool RelicCanCancel(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				if (_relicCanCancelMethod == null) return false;
				return (bool?)_relicCanCancelMethod.Invoke(building, null) ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Cancel the relic investigation.
		/// </summary>
		public static bool RelicCancelInvestigation(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				if (_relicCancelMethod == null) return false;
				_relicCancelMethod.Invoke(building, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the investigation start SoundModel for this relic (from model.investigationStartSound.GetNext()).
		/// Returns null if not available.
		/// </summary>
		public static object GetRelicInvestigationStartSoundModel(object building) {
			if (!BuildingReflection.IsRelic(building)) return null;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return null;

				var soundRef = ReflectionHelper.GetField(_investigationStartSoundField, model);
				if (soundRef == null) return null;

				var soundRefGetNext = BuildingReflection.SoundRefGetNextMethod;
				if (soundRefGetNext == null) return null;
				return soundRefGetNext.Invoke(soundRef, null);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if the relic currently has working effects (based on difficulty and decision).
		/// </summary>
		public static bool RelicHasWorkingEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var effects = ReflectionHelper.Invoke(_relicGetWorkingEffectsMethod, building) as System.Array;
				return effects != null && effects.Length > 0;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if the relic has dynamic effects that escalate over time.
		/// </summary>
		public static bool RelicHasDynamicEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return false;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return false;
				return ReflectionHelper.GetBool(_relicModelHasDynamicEffectsField, model);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the current dynamic effect tier index (0-based).
		/// </summary>
		public static int GetRelicCurrentEffectTier(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0;
				return ReflectionHelper.GetInt(_relicStateCurrentDynamicEffectField, state);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the total number of dynamic effect tiers.
		/// </summary>
		public static int GetRelicEffectTierCount(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return 0;
				var tiers = _relicModelEffectsTiersField?.GetValue(model) as System.Array;
				return tiers?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Check if the relic has reached its last (worst) dynamic effect tier.
		/// </summary>
		public static bool RelicIsLastEffectTierReached(object building) {
			if (!BuildingReflection.IsRelic(building)) return true;
			EnsureRelicBaseFields();

			try {
				if (_relicIsLastDynamicEffectReachedMethod != null)
					return (bool?)_relicIsLastDynamicEffectReachedMethod.Invoke(building, null) ?? true;

				// Fallback: manual check
				int current = GetRelicCurrentEffectTier(building);
				int total = GetRelicEffectTierCount(building);
				return current >= total - 1;
			} catch {
				return true;
			}
		}

		/// <summary>
		/// Get the time remaining until the next dynamic effect tier activates.
		/// Returns 0 if at last tier or no dynamic effects.
		/// </summary>
		public static float GetRelicTimeToNextEffectTier(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0f;
			if (!RelicHasDynamicEffects(building)) return 0f;
			if (RelicIsLastEffectTierReached(building)) return 0f;

			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0f;

				float nextChange = (float?)_relicStateNextDynamicEffectChangeField?.GetValue(state) ?? 0f;
				float currentTime = GameReflection.GetGameTime();
				float remaining = nextChange - currentTime;
				return remaining > 0f ? remaining : 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get the next tier's dynamic effects (what will activate when countdown ends).
		/// </summary>
		public static RelicEffectInfo[] GetRelicNextDynamicEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return new RelicEffectInfo[0];
			if (!RelicHasDynamicEffects(building)) return new RelicEffectInfo[0];
			if (RelicIsLastEffectTierReached(building)) return new RelicEffectInfo[0];

			EnsureRelicBaseFields();

			try {
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return new RelicEffectInfo[0];

				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return new RelicEffectInfo[0];

				var effectsTiers = _relicModelEffectsTiersField?.GetValue(model) as System.Array;
				if (effectsTiers == null || effectsTiers.Length == 0) return new RelicEffectInfo[0];

				int currentTier = ReflectionHelper.GetInt(_relicStateCurrentDynamicEffectField, state);
				int nextTier = currentTier + 1;
				if (nextTier >= effectsTiers.Length) return new RelicEffectInfo[0];

				var tierStep = effectsTiers.GetValue(nextTier);
				if (tierStep == null) return new RelicEffectInfo[0];

				var effectField = tierStep.GetType().GetField("effect", GameReflection.PublicInstance);
				var tierEffects = effectField?.GetValue(tierStep) as System.Array;
				if (tierEffects == null || tierEffects.Length == 0) return new RelicEffectInfo[0];

				return ExtractEffectInfos(tierEffects);
			} catch {
				return new RelicEffectInfo[0];
			}
		}

		/// <summary>
		/// Get the current dynamic effects (from the current tier).
		/// These are the escalating effects that get worse over time.
		/// </summary>
		public static RelicEffectInfo[] GetRelicCurrentDynamicEffects(object building) {
			if (!BuildingReflection.IsRelic(building)) return new RelicEffectInfo[0];
			if (!RelicHasDynamicEffects(building)) return new RelicEffectInfo[0];

			EnsureRelicBaseFields();

			try {
				// Try method first
				if (_relicGetCurrentDynamicEffectsMethod != null) {
					var effects = _relicGetCurrentDynamicEffectsMethod.Invoke(building, null) as System.Array;
					if (effects != null && effects.Length > 0)
						return ExtractEffectInfos(effects);
				}

				// Fallback: access effectsTiers[currentDynamicEffect].effect directly
				var model = ReflectionHelper.GetField(_relicModelField, building);
				if (model == null) return new RelicEffectInfo[0];

				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return new RelicEffectInfo[0];

				var effectsTiers = _relicModelEffectsTiersField?.GetValue(model) as System.Array;
				if (effectsTiers == null || effectsTiers.Length == 0) return new RelicEffectInfo[0];

				int currentTier = ReflectionHelper.GetInt(_relicStateCurrentDynamicEffectField, state);
				if (currentTier < 0 || currentTier >= effectsTiers.Length) return new RelicEffectInfo[0];

				var tierStep = effectsTiers.GetValue(currentTier);
				if (tierStep == null) return new RelicEffectInfo[0];

				// EffectStep.effect field
				var effectField = tierStep.GetType().GetField("effect", GameReflection.PublicInstance);
				var tierEffects = effectField?.GetValue(tierStep) as System.Array;
				if (tierEffects == null || tierEffects.Length == 0) return new RelicEffectInfo[0];

				return ExtractEffectInfos(tierEffects);
			} catch {
				return new RelicEffectInfo[0];
			}
		}

		/// <summary>
		/// Get the safe decision index (max of 0 and state.decisionIndex).
		/// </summary>
		public static int GetRelicSafeDecisionIndex(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				if (_relicGetSafeDecisionIndexMethod != null)
					return (int?)_relicGetSafeDecisionIndexMethod.Invoke(building, null) ?? 0;

				// Fallback: manual implementation
				int idx = GetRelicDecisionIndex(building);
				return idx < 0 ? 0 : idx;
			} catch {
				return 0;
			}
		}

		// ========================================
		// RELIC REWARD STORAGE (Phase C)
		// ========================================

		/// <summary>
		/// Gets the reward storage items remaining in a resolved relic.
		/// Returns list of (goodName, displayName, amount) for goods with amount > 0.
		/// </summary>
		public static List<(string goodName, string displayName, int amount)> GetRelicRewardStorageItems(object building) {
			var result = new List<(string, string, int)>();
			if (!BuildingReflection.IsRelic(building)) return result;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return result;

				var rewards = ReflectionHelper.GetField(_relicStateRewardsField, state);
				if (rewards == null) return result;

				// Get the goods dictionary keys
				var goodsDict = ReflectionHelper.GetField(BuildingReflection.GoodsCollectionGoodsField, rewards);
				if (goodsDict == null) return result;

				// Iterate via reflection (Dictionary<string, int>)
				var keys = ReflectionHelper.IterateKeys(goodsDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					string goodName = key as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					// Use GetFullAmount to include locked goods (being carried by haulers)
					int amount = 0;
					if (_lockedGoodsGetFullAmountMethod != null)
						amount = (int?)_lockedGoodsGetFullAmountMethod.Invoke(rewards, new object[] { goodName }) ?? 0;

					if (amount > 0) {
						string displayName = BuildingReflection.GetGoodDisplayName(goodName) ?? goodName;
						result.Add((goodName, displayName, amount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRelicRewardStorageItems failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Gets the total count of goods remaining in a resolved relic's reward storage
		/// (including goods currently being carried by haulers).
		/// </summary>
		public static int GetRelicRewardStorageFullSum(object building) {
			if (!BuildingReflection.IsRelic(building)) return 0;
			EnsureRelicBaseFields();

			try {
				var state = ReflectionHelper.GetField(_relicStateField, building);
				if (state == null) return 0;

				var rewards = ReflectionHelper.GetField(_relicStateRewardsField, state);
				if (rewards == null) return 0;

				if (_lockedGoodsFullSumMethod != null)
					return (int?)_lockedGoodsFullSumMethod.Invoke(rewards, null) ?? 0;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRelicRewardStorageFullSum failed: {ex.Message}");
			}

			return 0;
		}

		// ========================================
		// RELIC PRIVATE HELPERS
		// ========================================

		private static object GetRelicDecisionObject(object building, int decisionIndex) {
			var difficulty = ReflectionHelper.GetProp(_relicDifficultyProperty, building);
			if (difficulty == null) return null;

			var decisions = _relicDifficultyDecisionsField?.GetValue(difficulty) as Array;
			if (decisions == null || decisionIndex < 0 || decisionIndex >= decisions.Length) return null;

			return decisions.GetValue(decisionIndex);
		}

		private static object GetRelicGoodsSetObject(object building, int decisionIndex, int setIndex) {
			var decision = GetRelicDecisionObject(building, decisionIndex);
			if (decision == null) return null;

			var reqGoods = ReflectionHelper.GetField(_relicDecisionReqGoodsField, decision);
			if (reqGoods == null) return null;

			var sets = _goodsSetTableSetsField?.GetValue(reqGoods) as Array;
			if (sets == null || setIndex < 0 || setIndex >= sets.Length) return null;

			return sets.GetValue(setIndex);
		}

		private static object GetRelicGoodRefObject(object building, int decisionIndex, int setIndex, int goodIndex) {
			var goodsSet = GetRelicGoodsSetObject(building, decisionIndex, setIndex);
			if (goodsSet == null) return null;

			var goods = BuildingReflection.GoodsSetGoodsField?.GetValue(goodsSet) as Array;
			if (goods == null || goodIndex < 0 || goodIndex >= goods.Length) return null;

			return goods.GetValue(goodIndex);
		}

		private static RelicEffectInfo[] ExtractEffectInfos(Array effects) {
			var result = new RelicEffectInfo[effects.Length];
			for (int i = 0; i < effects.Length; i++) {
				var effect = effects.GetValue(i);
				if (effect == null) continue;

				result[i] = new RelicEffectInfo {
					Name = ReflectionHelper.GetPropString(BuildingReflection.EffectModelDisplayNameProperty, effect) ?? "Unknown",
					Description = ReflectionHelper.GetPropString(BuildingReflection.EffectModelDescriptionProperty, effect) ?? "",
					IsPositive = (bool?)BuildingReflection.EffectModelIsPositiveProperty?.GetValue(effect) ?? true
				};
			}
			return result;
		}

		private static RelicRewardInfo[] ResolveEffectNames(List<string> effectNames) {
			BuildingReflection.EnsureGameModelServiceTypes();

			var result = new RelicRewardInfo[effectNames.Count];
			for (int i = 0; i < effectNames.Count; i++) {
				var effectModel = BuildingReflection.GetEffectModel(effectNames[i]);
				if (effectModel != null) {
					result[i] = new RelicRewardInfo {
						Name = ReflectionHelper.GetPropString(BuildingReflection.EffectModelDisplayNameProperty, effectModel) ?? effectNames[i],
						Description = ReflectionHelper.GetPropString(BuildingReflection.EffectModelDescriptionProperty, effectModel) ?? ""
					};
				} else {
					result[i] = new RelicRewardInfo { Name = effectNames[i], Description = "" };
				}
			}
			return result;
		}
	}
}
