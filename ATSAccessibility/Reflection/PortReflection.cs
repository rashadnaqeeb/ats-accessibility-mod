using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to Port building internals (expeditions).
	/// Extracted from BuildingReflection.cs. Follows same caching patterns.
	/// BuildingReflection.IsPort() remains in BuildingReflection for routing use.
	/// </summary>
	public static class PortReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// Port base fields
		private static Type _portType = null;
		private static FieldInfo _portStateField = null;  // Port.state
		private static FieldInfo _portModelField = null;  // Port.model
		private static MethodInfo _portWasExpeditionStartedMethod = null;  // Port.WasExpeditionStarted()
		private static MethodInfo _portAreRewardsWaitingMethod = null;  // Port.AreRewardsWaiting()
		private static MethodInfo _portCalculateProgressMethod = null;  // Port.CalculateProgress()
		private static MethodInfo _portCalculateTimeLeftMethod = null;  // Port.CalculateTimeLeft()
		private static MethodInfo _portGetCurrentExpeditionMethod = null;  // Port.GetCurrentExpedition()
		private static MethodInfo _portGetPickedStriderGoodMethod = null;  // Port.GetPickedStriderGood(int)
		private static MethodInfo _portGetPickedCrewGoodMethod = null;  // Port.GetPickedCrewGood(int)
																		// Port action methods
		private static MethodInfo _portWasDecisionMadeMethod = null;  // Port.WasDecisionMade()
		private static MethodInfo _portLockDecisionMethod = null;  // Port.LockDecision()
		private static MethodInfo _portCancelDecisionMethod = null;  // Port.CancelDecision()
		private static MethodInfo _portAcceptRewardsMethod = null;  // Port.AcceptRewards()
		private static MethodInfo _portChangeLevelMethod = null;  // Port.ChangeLevel(int)
		private static MethodInfo _portAllGoodsDeliveredMethod = null;  // Port.AllExpeditionGoodsDelivered()
		private static MethodInfo _portIsBlockedByUnpickedCategoryMethod = null;  // Port.IsBlockedByUnpickedCategory()
		private static MethodInfo _portGetCurrentExpeditionModelMethod = null;  // Port.GetCurrentExpeditionModel()
		private static MethodInfo _portCalculateDurationMethod = null;  // Port.CalculateDuration()
																		// PortState fields
		private static FieldInfo _portStateExpeditionLevelField = null;  // PortState.expeditionLevel
		private static FieldInfo _portStateAreRewardsWaitingField = null;  // PortState.areRewardsWaiting
		private static FieldInfo _portStateBlueprintRewardField = null;  // PortState.blueprintReward
		private static FieldInfo _portStatePerkRewardField = null;  // PortState.perkReward
		private static FieldInfo _portStateExpeditionGoodsField = null;  // PortState.expeditionGoods
		private static FieldInfo _portStateWorkersField = null;  // PortState.workers
		private static FieldInfo _portStateWasDecisionMadeField = null;  // PortState.wasDecisionMade
		private static FieldInfo _portStatePickedCategoryField = null;  // PortState.pickedCategory
		private static FieldInfo _portStateStriderPickedGoodsField = null;  // PortState.striderPickedGoods (List<int>)
		private static FieldInfo _portStateCrewPickedGoodsField = null;  // PortState.crewPickedGoods (List<int>)
																		 // PortExpeditionModel fields
		private static FieldInfo _portExpedModelMaxLevelField = null;  // PortExpeditionModel.maxLevel
		private static FieldInfo _portExpedModelBlueprintsField = null;  // PortExpeditionModel.blueprints
		private static FieldInfo _portExpedModelChancesField = null;  // PortExpeditionModel.chances (PortRewardChance[])
																	  // PortExpedition fields
		private static FieldInfo _portExpedStriderGoodsField = null;  // PortExpedition.striderGoods (GoodsSet[])
		private static FieldInfo _portExpedCrewGoodsField = null;  // PortExpedition.crewGoods (GoodsSet[])
		private static FieldInfo _portExpedChancesField = null;  // PortExpedition.chances (List<PortRewardChance>)
																 // PortRewardChance fields
		private static FieldInfo _portRewardChanceRarityField = null;  // PortRewardChance.rarity
		private static FieldInfo _portRewardChanceChanceField = null;  // PortRewardChance.chance
																	   // BuildingsDropTable / category
		private static FieldInfo _buildingsDropTableBuildingsField = null;  // BuildingsDropTable.buildings
		private static FieldInfo _buildingTableEntityBuildingField = null;  // BuildingTableEntity.building
		private static FieldInfo _buildingModelCategoryField = null;  // BuildingModel.category
																	  // LimitedGoodsCollection
		private static MethodInfo _limitedGoodsGetFullAmountMethod = null;  // LimitedGoodsCollection.GetFullAmount(string)

		private static bool _portTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsurePortTypes() {
			if (_portTypesCached) return;
			_portTypesCached = true;

			ReflectionHelper.InitCache("PortReflection.Port", assembly => {
				_portType = assembly.GetType("Eremite.Buildings.Port");
				if (_portType != null) {
					_portStateField = _portType.GetField("state", GameReflection.PublicInstance);
					_portModelField = _portType.GetField("model", GameReflection.PublicInstance);
					_portWasExpeditionStartedMethod = _portType.GetMethod("WasExpeditionStarted", GameReflection.PublicInstance);
					_portAreRewardsWaitingMethod = _portType.GetMethod("AreRewardsWaiting", GameReflection.PublicInstance);
					_portCalculateProgressMethod = _portType.GetMethod("CalculateProgress", GameReflection.PublicInstance);
					_portCalculateTimeLeftMethod = _portType.GetMethod("CalculateTimeLeft", GameReflection.PublicInstance);
					_portGetCurrentExpeditionMethod = _portType.GetMethod("GetCurrentExpedition", GameReflection.PublicInstance);
					_portGetPickedStriderGoodMethod = _portType.GetMethod("GetPickedStriderGood", GameReflection.PublicInstance);
					_portGetPickedCrewGoodMethod = _portType.GetMethod("GetPickedCrewGood", GameReflection.PublicInstance);
					_portWasDecisionMadeMethod = _portType.GetMethod("WasDecisionMade", GameReflection.PublicInstance);
					_portLockDecisionMethod = _portType.GetMethod("LockDecision", GameReflection.PublicInstance);
					_portCancelDecisionMethod = _portType.GetMethod("CancelDecision", GameReflection.PublicInstance);
					_portAcceptRewardsMethod = _portType.GetMethod("AcceptRewards", GameReflection.PublicInstance);
					_portChangeLevelMethod = _portType.GetMethod("ChangeLevel", GameReflection.PublicInstance);
					_portAllGoodsDeliveredMethod = _portType.GetMethod("AllExpeditionGoodsDelivered", GameReflection.PublicInstance);
					_portIsBlockedByUnpickedCategoryMethod = _portType.GetMethod("IsBlockedByUnpickedCategory", GameReflection.PublicInstance);
					_portGetCurrentExpeditionModelMethod = _portType.GetMethod("GetCurrentExpeditionModel", GameReflection.PublicInstance);
					_portCalculateDurationMethod = _portType.GetMethod("CalculateDuration", GameReflection.PublicInstance);
				}

				var portStateType = assembly.GetType("Eremite.Buildings.PortState");
				if (portStateType != null) {
					_portStateExpeditionLevelField = portStateType.GetField("expeditionLevel", GameReflection.PublicInstance);
					_portStateAreRewardsWaitingField = portStateType.GetField("areRewardsWaiting", GameReflection.PublicInstance);
					_portStateBlueprintRewardField = portStateType.GetField("blueprintReward", GameReflection.PublicInstance);
					_portStatePerkRewardField = portStateType.GetField("perkReward", GameReflection.PublicInstance);
					_portStateExpeditionGoodsField = portStateType.GetField("expeditionGoods", GameReflection.PublicInstance);
					_portStateWorkersField = portStateType.GetField("workers", GameReflection.PublicInstance);
					_portStateWasDecisionMadeField = portStateType.GetField("wasDecisionMade", GameReflection.PublicInstance);
					_portStatePickedCategoryField = portStateType.GetField("pickedCategory", GameReflection.PublicInstance);
					_portStateStriderPickedGoodsField = portStateType.GetField("striderPickedGoods", GameReflection.PublicInstance);
					_portStateCrewPickedGoodsField = portStateType.GetField("crewPickedGoods", GameReflection.PublicInstance);
				}

				var portExpedModelType = assembly.GetType("Eremite.Buildings.PortExpeditionModel");
				if (portExpedModelType != null) {
					_portExpedModelMaxLevelField = portExpedModelType.GetField("maxLevel", GameReflection.PublicInstance);
					_portExpedModelBlueprintsField = portExpedModelType.GetField("blueprints", GameReflection.PublicInstance);
					_portExpedModelChancesField = portExpedModelType.GetField("chances", GameReflection.PublicInstance);
				}

				var portExpedType = assembly.GetType("Eremite.Buildings.PortExpedition");
				if (portExpedType != null) {
					_portExpedStriderGoodsField = portExpedType.GetField("striderGoods", GameReflection.PublicInstance);
					_portExpedCrewGoodsField = portExpedType.GetField("crewGoods", GameReflection.PublicInstance);
					_portExpedChancesField = portExpedType.GetField("chances", GameReflection.PublicInstance);
				}

				var portRewardChanceType = assembly.GetType("Eremite.Buildings.PortRewardChance");
				if (portRewardChanceType != null) {
					_portRewardChanceRarityField = portRewardChanceType.GetField("rarity", GameReflection.PublicInstance);
					_portRewardChanceChanceField = portRewardChanceType.GetField("chance", GameReflection.PublicInstance);
				}

				var buildingsDropTableType = assembly.GetType("Eremite.Model.BuildingsDropTable");
				if (buildingsDropTableType != null) {
					_buildingsDropTableBuildingsField = buildingsDropTableType.GetField("buildings", GameReflection.PublicInstance);
				}

				var buildingTableEntityType = assembly.GetType("Eremite.Model.BuildingTableEntity");
				if (buildingTableEntityType != null) {
					_buildingTableEntityBuildingField = buildingTableEntityType.GetField("building", GameReflection.PublicInstance);
				}

				var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
				if (buildingModelType != null) {
					_buildingModelCategoryField = buildingModelType.GetField("category", GameReflection.PublicInstance);
				}

				var limitedGoodsType = assembly.GetType("Eremite.LimitedGoodsCollection");
				if (limitedGoodsType != null) {
					_limitedGoodsGetFullAmountMethod = limitedGoodsType.GetMethod("GetFullAmount", new[] { typeof(string) });
				}
			});
		}

		// ========================================
		// PUBLIC API - PORT
		// ========================================

		/// <summary>
		/// Get Port expedition level.
		/// </summary>
		public static int GetPortExpeditionLevel(object building) {
			if (!BuildingReflection.IsPort(building)) return 0;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return 0;

				return (int?)_portStateExpeditionLevelField?.GetValue(state) ?? 1;
			} catch {
				return 1;
			}
		}

		/// <summary>
		/// Check if Port expedition has started.
		/// </summary>
		public static bool IsPortExpeditionStarted(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var result = ReflectionHelper.Invoke(_portWasExpeditionStartedMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if Port has rewards waiting to be collected.
		/// </summary>
		public static bool ArePortRewardsWaiting(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var result = ReflectionHelper.Invoke(_portAreRewardsWaitingMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get Port expedition progress (0-1).
		/// </summary>
		public static float GetPortProgress(object building) {
			if (!BuildingReflection.IsPort(building)) return 0f;

			EnsurePortTypes();
			try {
				return ReflectionHelper.InvokeFloat(_portCalculateProgressMethod, building);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get Port expedition time remaining in seconds.
		/// </summary>
		public static float GetPortTimeLeft(object building) {
			if (!BuildingReflection.IsPort(building)) return 0f;

			EnsurePortTypes();
			try {
				return ReflectionHelper.InvokeFloat(_portCalculateTimeLeftMethod, building);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get Port blueprint reward name (if any).
		/// </summary>
		public static string GetPortBlueprintReward(object building) {
			if (!BuildingReflection.IsPort(building)) return null;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return null;

				return ReflectionHelper.GetString(_portStateBlueprintRewardField, state);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get Port perk reward name (if any).
		/// </summary>
		public static string GetPortPerkReward(object building) {
			if (!BuildingReflection.IsPort(building)) return null;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return null;

				return ReflectionHelper.GetString(_portStatePerkRewardField, state);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if port decision was made (goods locked in).
		/// </summary>
		public static bool WasPortDecisionMade(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var result = ReflectionHelper.Invoke(_portWasDecisionMadeMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if all expedition goods have been delivered.
		/// </summary>
		public static bool AllPortGoodsDelivered(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var result = ReflectionHelper.Invoke(_portAllGoodsDeliveredMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if port is blocked by an unpicked category.
		/// </summary>
		public static bool IsPortBlockedByUnpickedCategory(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var result = ReflectionHelper.Invoke(_portIsBlockedByUnpickedCategoryMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Lock the port decision (confirm expedition).
		/// </summary>
		public static bool PortLockDecision(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			if (_portLockDecisionMethod == null) return false;

			try {
				_portLockDecisionMethod.Invoke(building, null);
				return true;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] PortLockDecision failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Cancel the port decision.
		/// </summary>
		public static bool PortCancelDecision(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			if (_portCancelDecisionMethod == null) return false;

			try {
				_portCancelDecisionMethod.Invoke(building, null);
				return true;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] PortCancelDecision failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Accept port expedition rewards.
		/// </summary>
		public static bool PortAcceptRewards(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			if (_portAcceptRewardsMethod == null) return false;

			try {
				_portAcceptRewardsMethod.Invoke(building, null);
				return true;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] PortAcceptRewards failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Change the port expedition level.
		/// </summary>
		public static bool PortChangeLevel(object building, int level) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			if (_portChangeLevelMethod == null) return false;

			try {
				_portChangeLevelMethod.Invoke(building, new object[] { level });
				return true;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] PortChangeLevel failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the max expedition level for the port.
		/// </summary>
		public static int GetPortMaxLevel(object building) {
			if (!BuildingReflection.IsPort(building)) return 1;

			EnsurePortTypes();
			try {
				var expedModel = ReflectionHelper.Invoke(_portGetCurrentExpeditionModelMethod, building);
				if (expedModel == null) return 1;

				return (int?)_portExpedModelMaxLevelField?.GetValue(expedModel) ?? 1;
			} catch {
				return 1;
			}
		}

		/// <summary>
		/// Get the duration of the current expedition at the current level.
		/// </summary>
		public static float GetPortDuration(object building) {
			if (!BuildingReflection.IsPort(building)) return 0f;

			EnsurePortTypes();
			try {
				return ReflectionHelper.InvokeFloat(_portCalculateDurationMethod, building);
			} catch {
				return 0f;
			}
		}

		// ========================================
		// STRIDER / CREW GOODS (unified API)
		// ========================================

		/// <summary>
		/// Get the number of goods sets for strider or crew in the current expedition.
		/// </summary>
		public static int GetPortGoodSetCount(object building, bool isStrider) {
			if (!BuildingReflection.IsPort(building)) return 0;

			EnsurePortTypes();
			try {
				var expedition = ReflectionHelper.Invoke(_portGetCurrentExpeditionMethod, building);
				if (expedition == null) return 0;

				var goodsField = isStrider ? _portExpedStriderGoodsField : _portExpedCrewGoodsField;
				var goodsSets = goodsField?.GetValue(expedition) as Array;
				return goodsSets?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the number of alternatives in a goods set.
		/// </summary>
		public static int GetPortAlternativeCount(object building, int setIndex, bool isStrider) {
			var goodsSet = GetGoodsSetObject(building, setIndex, isStrider);
			if (goodsSet == null) return 0;

			try {
				var goods = BuildingReflection.GoodsSetGoodsField?.GetValue(goodsSet) as Array;
				return goods?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the display name of a good alternative.
		/// </summary>
		public static string GetPortGoodDisplayName(object building, int setIndex, int altIndex, bool isStrider) {
			var goodRef = GetGoodRefObject(building, setIndex, altIndex, isStrider);
			if (goodRef == null) return null;

			try {
				return ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, goodRef);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the internal name of a good alternative.
		/// </summary>
		public static string GetPortGoodName(object building, int setIndex, int altIndex, bool isStrider) {
			var goodRef = GetGoodRefObject(building, setIndex, altIndex, isStrider);
			if (goodRef == null) return null;

			try {
				return ReflectionHelper.GetPropString(BuildingReflection.GoodRefNameProperty, goodRef);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the amount of a good alternative.
		/// </summary>
		public static int GetPortGoodAmount(object building, int setIndex, int altIndex, bool isStrider) {
			var goodRef = GetGoodRefObject(building, setIndex, altIndex, isStrider);
			if (goodRef == null) return 0;

			try {
				return ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, goodRef);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the picked index for a goods set.
		/// </summary>
		public static int GetPortPickedIndex(object building, int setIndex, bool isStrider) {
			if (!BuildingReflection.IsPort(building)) return 0;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return 0;

				var pickedField = isStrider ? _portStateStriderPickedGoodsField : _portStateCrewPickedGoodsField;
				var pickedGoods = ReflectionHelper.GetField(pickedField, state);
				if (pickedGoods == null) return 0;

				var list = pickedGoods as System.Collections.IList;
				if (list == null || setIndex < 0 || setIndex >= list.Count) return 0;

				return (int)list[setIndex];
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Set the picked index for a goods set.
		/// </summary>
		public static bool SetPortPickedIndex(object building, int setIndex, int altIndex, bool isStrider) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return false;

				var pickedField = isStrider ? _portStateStriderPickedGoodsField : _portStateCrewPickedGoodsField;
				var pickedGoods = ReflectionHelper.GetField(pickedField, state);
				if (pickedGoods == null) return false;

				var list = pickedGoods as System.Collections.IList;
				if (list == null || setIndex < 0 || setIndex >= list.Count) return false;

				list[setIndex] = altIndex;
				return true;
			} catch {
				return false;
			}
		}

		// ========================================
		// EXPEDITION GOODS & CATEGORIES
		// ========================================

		/// <summary>
		/// Get the delivered amount of a good in the port's expedition goods collection.
		/// </summary>
		public static int GetPortGoodDeliveredAmount(object building, string goodName) {
			if (!BuildingReflection.IsPort(building) || string.IsNullOrEmpty(goodName)) return 0;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return 0;

				var expedGoods = ReflectionHelper.GetField(_portStateExpeditionGoodsField, state);
				if (expedGoods == null) return 0;

				var result = ReflectionHelper.Invoke(_limitedGoodsGetFullAmountMethod, expedGoods, goodName);
				return (int?)result ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get available building categories from the expedition blueprints drop table.
		/// Returns display names.
		/// </summary>
		public static List<string> GetPortAvailableCategories(object building) {
			return GetPortCategoryNames(building, category => {
				var displayNameField = category.GetType().GetField("displayName", GameReflection.PublicInstance);
				var displayNameObj = displayNameField?.GetValue(category);
				return GameReflection.GetLocaText(displayNameObj);
			});
		}

		/// <summary>
		/// Get available building category internal names from the expedition blueprints drop table.
		/// </summary>
		public static List<string> GetPortCategoryInternalNames(object building) {
			return GetPortCategoryNames(building, category => {
				var nameProp = category.GetType().GetProperty("Name", GameReflection.PublicInstance);
				return nameProp?.GetValue(category) as string;
			});
		}

		/// <summary>
		/// Get the currently picked category name.
		/// </summary>
		public static string GetPortPickedCategory(object building) {
			if (!BuildingReflection.IsPort(building)) return null;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return null;

				return ReflectionHelper.GetString(_portStatePickedCategoryField, state);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Set the picked category (internal name).
		/// </summary>
		public static bool SetPortPickedCategory(object building, string categoryName) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var state = ReflectionHelper.GetField(_portStateField, building);
				if (state == null) return false;

				_portStatePickedCategoryField?.SetValue(state, categoryName);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if the expedition model has a blueprint reward (needs category selection).
		/// </summary>
		public static bool PortHasBlueprintReward(object building) {
			if (!BuildingReflection.IsPort(building)) return false;

			EnsurePortTypes();
			try {
				var expedModel = ReflectionHelper.Invoke(_portGetCurrentExpeditionModelMethod, building);
				if (expedModel == null) return false;

				var blueprints = ReflectionHelper.GetField(_portExpedModelBlueprintsField, expedModel);
				return blueprints != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the reward chances for the current expedition.
		/// Returns list of (rarity name, chance percentage).
		/// </summary>
		public static List<(string rarity, int chance)> GetPortRewardChances(object building) {
			if (!BuildingReflection.IsPort(building)) return new List<(string, int)>();

			EnsurePortTypes();
			try {
				var expedition = ReflectionHelper.Invoke(_portGetCurrentExpeditionMethod, building);
				if (expedition == null) return new List<(string, int)>();

				var chances = ReflectionHelper.GetField(_portExpedChancesField, expedition);
				if (chances == null) return new List<(string, int)>();

				var result = new List<(string, int)>();
				var list = chances as System.Collections.IList;
				if (list == null) return result;

				for (int i = 0; i < list.Count; i++) {
					var chance = list[i];
					if (chance == null) continue;

					var rarityObj = ReflectionHelper.GetField(_portRewardChanceRarityField, chance);
					int chanceValue = ReflectionHelper.GetInt(_portRewardChanceChanceField, chance);

					string rarityName = rarityObj?.ToString() ?? "Unknown";
					if (chanceValue > 0) {
						result.Add((rarityName, chanceValue));
					}
				}

				return result;
			} catch {
				return new List<(string, int)>();
			}
		}

		// ========================================
		// PRIVATE HELPERS
		// ========================================

		private static object GetGoodsSetObject(object building, int setIndex, bool isStrider) {
			if (!BuildingReflection.IsPort(building)) return null;

			EnsurePortTypes();
			try {
				var expedition = ReflectionHelper.Invoke(_portGetCurrentExpeditionMethod, building);
				if (expedition == null) return null;

				var goodsField = isStrider ? _portExpedStriderGoodsField : _portExpedCrewGoodsField;
				var goodsSets = goodsField?.GetValue(expedition) as Array;
				if (goodsSets == null || setIndex < 0 || setIndex >= goodsSets.Length) return null;

				return goodsSets.GetValue(setIndex);
			} catch {
				return null;
			}
		}

		private static object GetGoodRefObject(object building, int setIndex, int altIndex, bool isStrider) {
			var goodsSet = GetGoodsSetObject(building, setIndex, isStrider);
			if (goodsSet == null) return null;

			try {
				var goods = BuildingReflection.GoodsSetGoodsField?.GetValue(goodsSet) as Array;
				if (goods == null || altIndex < 0 || altIndex >= goods.Length) return null;

				return goods.GetValue(altIndex);
			} catch {
				return null;
			}
		}

		private static List<string> GetPortCategoryNames(object building, Func<object, string> extractName) {
			if (!BuildingReflection.IsPort(building)) return new List<string>();

			EnsurePortTypes();
			try {
				var expedModel = ReflectionHelper.Invoke(_portGetCurrentExpeditionModelMethod, building);
				if (expedModel == null) return new List<string>();

				var blueprints = ReflectionHelper.GetField(_portExpedModelBlueprintsField, expedModel);
				if (blueprints == null) return new List<string>();

				var buildingsArray = _buildingsDropTableBuildingsField?.GetValue(blueprints) as Array;
				if (buildingsArray == null) return new List<string>();

				var categories = new HashSet<string>();
				var result = new List<string>();

				for (int i = 0; i < buildingsArray.Length; i++) {
					var entity = buildingsArray.GetValue(i);
					if (entity == null) continue;

					var buildingModel = ReflectionHelper.GetField(_buildingTableEntityBuildingField, entity);
					if (buildingModel == null) continue;

					var category = ReflectionHelper.GetField(_buildingModelCategoryField, buildingModel);
					if (category == null) continue;

					string name = extractName(category);
					if (!string.IsNullOrEmpty(name) && categories.Add(name)) {
						result.Add(name);
					}
				}

				return result;
			} catch {
				return new List<string>();
			}
		}
	}
}
