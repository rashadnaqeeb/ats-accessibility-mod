using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Reflection helpers for accessing ReputationRewardsPopup data and interaction.
	/// Provides methods to query current reward options, pick buildings, reroll, and extend.
	/// </summary>
	public static class ReputationRewardReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		public class RewardOption {
			public object Model;         // BuildingModel
			public string DisplayName;
			public string Description;   // ListDescription
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// IReputationRewardsService methods
		private static MethodInfo _rrsGetCurrentPicksMethod = null;
		private static MethodInfo _rrsCanAffordRerollMethod = null;
		private static MethodInfo _rrsRerollMethod = null;
		private static MethodInfo _rrsGetRerollPriceMethod = null;
		private static MethodInfo _rrsCanExtendMethod = null;
		private static MethodInfo _rrsCanAffordExtendMethod = null;
		private static MethodInfo _rrsExtendMethod = null;

		// ReputationReward.building (public field)
		private static FieldInfo _rrBuildingField = null;

		// Settings.GetBuilding(string)
		private static MethodInfo _settingsGetBuildingMethod = null;

		// BuildingModel.ListDescription (public virtual property)
		private static PropertyInfo _bmListDescriptionProperty = null;

		// Good struct fields (for reroll price)
		private static FieldInfo _goodNameField = null;
		private static FieldInfo _goodAmountField = null;

		// BiomeService/Blueprints for extend cost
		private static PropertyInfo _gsBiomeServiceProperty = null;
		private static PropertyInfo _bsBlueprintsProperty = null;
		private static FieldInfo _bbcExtendCostField = null;

		// GoodRef fields (for extend cost)
		private static FieldInfo _grGoodField = null;
		private static FieldInfo _grAmountField = null;

		// ReputationRewardsPopup methods (private)
		private static MethodInfo _rpOnBuildingPickedMethod = null;
		private static MethodInfo _rpRerollMethod = null;

		// ReputationRewardsPopup textTyper (for tutorial description)
		private static FieldInfo _rpTextTyperField = null;
		private static FieldInfo _ttTextMeshField = null;
		private static PropertyInfo _tmpTextProperty = null;

		private static bool _typesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypesCached() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("ReputationRewardReflection", assembly => {
				CacheServiceMethods(assembly);
				CacheRewardTypes(assembly);
				CacheSettingsMethods(assembly);
				CacheBuildingModelTypes(assembly);
				CacheGoodTypes(assembly);
				CacheBiomeTypes(assembly);
				CachePopupTypes(assembly);
			});
		}

		private static void CacheServiceMethods(Assembly assembly) {
			var serviceType = assembly.GetType("Eremite.Services.IReputationRewardsService");
			if (serviceType == null) return;

			_rrsGetCurrentPicksMethod = serviceType.GetMethod("GetCurrentPicks",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsCanAffordRerollMethod = serviceType.GetMethod("CanAffordReroll",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsRerollMethod = serviceType.GetMethod("Reroll",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsGetRerollPriceMethod = serviceType.GetMethod("GetRerollPrice",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsCanExtendMethod = serviceType.GetMethod("CanExtend",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsCanAffordExtendMethod = serviceType.GetMethod("CanAffordExtend",
				BindingFlags.Public | BindingFlags.Instance);
			_rrsExtendMethod = serviceType.GetMethod("Extend",
				BindingFlags.Public | BindingFlags.Instance);
		}

		private static void CacheRewardTypes(Assembly assembly) {
			var rewardType = assembly.GetType("Eremite.Model.State.ReputationReward");
			if (rewardType != null) {
				_rrBuildingField = rewardType.GetField("building",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheSettingsMethods(Assembly assembly) {
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetBuildingMethod = settingsType.GetMethod("GetBuilding",
					new[] { typeof(string) });
			}
		}

		private static void CacheBuildingModelTypes(Assembly assembly) {
			var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
			if (buildingModelType != null) {
				_bmListDescriptionProperty = buildingModelType.GetProperty("ListDescription",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheGoodTypes(Assembly assembly) {
			var goodType = assembly.GetType("Eremite.Model.Good");
			if (goodType != null) {
				_goodNameField = goodType.GetField("name",
					BindingFlags.Public | BindingFlags.Instance);
				_goodAmountField = goodType.GetField("amount",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CacheBiomeTypes(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
					BindingFlags.Public | BindingFlags.Instance);
			}

			var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
			if (biomeServiceType != null) {
				_bsBlueprintsProperty = biomeServiceType.GetProperty("Blueprints",
					BindingFlags.Public | BindingFlags.Instance);
			}

			var blueprintsConfigType = assembly.GetType("Eremite.Model.Configs.BiomeBlueprintsConfig");
			if (blueprintsConfigType != null) {
				_bbcExtendCostField = blueprintsConfigType.GetField("extendCost",
					BindingFlags.Public | BindingFlags.Instance);
			}

			var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
			if (goodRefType != null) {
				_grGoodField = goodRefType.GetField("good",
					BindingFlags.Public | BindingFlags.Instance);
				_grAmountField = goodRefType.GetField("amount",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		private static void CachePopupTypes(Assembly assembly) {
			var popupType = assembly.GetType("Eremite.View.HUD.ReputationRewardsPopup");
			if (popupType != null) {
				_rpOnBuildingPickedMethod = popupType.GetMethod("OnBuildingPicked",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_rpRerollMethod = popupType.GetMethod("Reroll",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_rpTextTyperField = popupType.GetField("textTyper",
					BindingFlags.NonPublic | BindingFlags.Instance);
			}

			// TextTyper.textMesh field
			var textTyperType = assembly.GetType("Eremite.View.TextTyper");
			if (textTyperType != null) {
				_ttTextMeshField = textTyperType.GetField("textMesh",
					BindingFlags.NonPublic | BindingFlags.Instance);
			}
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		private static object GetService() {
			return GameReflection.GetReputationRewardsService();
		}

		private static object GetBiomeService() {
			EnsureTypesCached();
			return GameReflection.GetService(_gsBiomeServiceProperty);
		}

		// ========================================
		// POPUP DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a ReputationRewardsPopup.
		/// </summary>
		public static bool IsReputationRewardsPopup(object popup) {
			if (popup == null) return false;
			return popup.GetType().Name == "ReputationRewardsPopup";
		}

		/// <summary>
		/// Check if we're in the first tutorial with tutorial-specific description.
		/// This matches the game's IsTutorialDesc() logic.
		/// </summary>
		public static bool IsTutorialMode() {
			// Check if TutorialService.IsFirstTutorial(Biome) && Reputation == 0
			// This is when the popup shows tutorial-specific text
			try {
				var metaServices = GameReflection.GetMetaServices();
				if (metaServices == null) return false;

				// Get TutorialService
				var tutorialServiceProp = metaServices.GetType().GetProperty("TutorialService");
				var tutorialService = tutorialServiceProp?.GetValue(metaServices);
				if (tutorialService == null) return false;

				// Get current biome from BiomeService
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var biomeServiceProp = gameServices.GetType().GetProperty("BiomeService");
				var biomeService = biomeServiceProp?.GetValue(gameServices);
				if (biomeService == null) return false;

				var biomeProp = biomeService.GetType().GetProperty("Biome");
				var biome = biomeProp?.GetValue(biomeService);
				if (biome == null) return false;

				// Check IsFirstTutorial(biome)
				var isFirstTutorialMethod = tutorialService.GetType().GetMethod("IsFirstTutorial");
				if (isFirstTutorialMethod == null) return false;

				var isFirstTutorial = (bool)isFirstTutorialMethod.Invoke(tutorialService, new[] { biome });
				if (!isFirstTutorial) return false;

				// Check Reputation == 0
				var reputationServiceProp = gameServices.GetType().GetProperty("ReputationService");
				var reputationService = reputationServiceProp?.GetValue(gameServices);
				if (reputationService == null) return false;

				var reputationProp = reputationService.GetType().GetProperty("Reputation");
				var reputationReactive = reputationProp?.GetValue(reputationService);
				if (reputationReactive == null) return false;

				var valueProp = reputationReactive.GetType().GetProperty("Value");
				var reputation = valueProp?.GetValue(reputationReactive);
				if (reputation == null) return false;

				return (float)reputation == 0f;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the popup's description text (from textTyper.textMesh.text).
		/// This includes tutorial-specific text during the first tutorial.
		/// </summary>
		public static string GetPopupDescription(object popup) {
			if (popup == null) return null;
			EnsureTypesCached();

			var textTyper = ReflectionHelper.GetField(_rpTextTyperField, popup);
			if (textTyper == null) return null;

			var textMesh = ReflectionHelper.GetField(_ttTextMeshField, textTyper);
			if (textMesh == null) return null;

			// Cache TMP_Text.text property on first use
			if (_tmpTextProperty == null) {
				_tmpTextProperty = textMesh.GetType().GetProperty("text",
					BindingFlags.Public | BindingFlags.Instance);
			}

			return ReflectionHelper.GetPropString(_tmpTextProperty, textMesh);
		}

		// ========================================
		// DATA ACCESS
		// ========================================

		/// <summary>
		/// Get the current reward options (buildings to choose from).
		/// Returns null/empty if no current pick is available.
		/// </summary>
		public static List<RewardOption> GetCurrentOptions() {
			EnsureTypesCached();

			var result = new List<RewardOption>();
			var service = GetService();
			if (service == null || _rrsGetCurrentPicksMethod == null) return result;

			try {
				var picks = ReflectionHelper.Invoke(_rrsGetCurrentPicksMethod, service) as IList;
				if (picks == null || picks.Count == 0) return result;

				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsGetBuildingMethod == null) return result;

				foreach (var pick in picks) {
					if (pick == null) continue;

					var buildingName = ReflectionHelper.GetString(_rrBuildingField, pick);
					if (string.IsNullOrEmpty(buildingName)) continue;

					var buildingModel = ReflectionHelper.Invoke(_settingsGetBuildingMethod, settings, buildingName);
					if (buildingModel == null) continue;

					var displayName = GameReflection.GetDisplayName(buildingModel) ?? buildingName;
					var description = ReflectionHelper.GetPropString(_bmListDescriptionProperty, buildingModel);

					result.Add(new RewardOption {
						Model = buildingModel,
						DisplayName = displayName,
						Description = description
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetCurrentOptions failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Pick a building from the popup. Invokes the popup's OnBuildingPicked method
		/// which calls service.RewardPicked and then either shows next reward or hides popup.
		/// </summary>
		public static bool PickBuilding(object popup, object buildingModel) {
			if (popup == null || buildingModel == null) return false;
			EnsureTypesCached();

			return ReflectionHelper.InvokeVoid(_rpOnBuildingPickedMethod, popup, buildingModel);
		}

		// ========================================
		// REROLL
		// ========================================

		/// <summary>
		/// Check if the player can afford to reroll.
		/// </summary>
		public static bool CanAffordReroll() {
			EnsureTypesCached();
			var service = GetService();
			if (service == null) return false;

			return ReflectionHelper.InvokeBool(_rrsCanAffordRerollMethod, service);
		}

		/// <summary>
		/// Reroll the current reward options via the popup's own method.
		/// This keeps the popup's UI in sync (price slot updates with increasing cost).
		/// </summary>
		public static bool Reroll(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();

			return ReflectionHelper.InvokeVoid(_rpRerollMethod, popup);
		}

		/// <summary>
		/// Get the reroll cost as (amount, good display name).
		/// </summary>
		public static (int amount, string goodDisplayName) GetRerollCost() {
			EnsureTypesCached();
			var service = GetService();
			if (service == null || _rrsGetRerollPriceMethod == null) return (0, "Unknown");

			try {
				var good = ReflectionHelper.Invoke(_rrsGetRerollPriceMethod, service);
				if (good == null) return (0, "Unknown");

				var name = ReflectionHelper.GetString(_goodNameField, good) ?? "";
				var amount = ReflectionHelper.GetInt(_goodAmountField, good);
				var displayName = GameReflection.GetGoodDisplayName(name);

				return (amount, displayName);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetRerollCost failed: {ex.Message}");
				return (0, "Unknown");
			}
		}

		// ========================================
		// EXTEND
		// ========================================

		/// <summary>
		/// Check if extending is available (effect enabled + not already extended).
		/// </summary>
		public static bool CanExtend() {
			EnsureTypesCached();
			var service = GetService();
			if (service == null) return false;

			return ReflectionHelper.InvokeBool(_rrsCanExtendMethod, service);
		}

		/// <summary>
		/// Check if the player can afford to extend.
		/// </summary>
		public static bool CanAffordExtend() {
			EnsureTypesCached();
			var service = GetService();
			if (service == null) return false;

			return ReflectionHelper.InvokeBool(_rrsCanAffordExtendMethod, service);
		}

		/// <summary>
		/// Extend the current reward options (add one more building choice).
		/// </summary>
		public static bool Extend() {
			EnsureTypesCached();
			var service = GetService();
			if (service == null) return false;

			return ReflectionHelper.InvokeVoid(_rrsExtendMethod, service);
		}

		/// <summary>
		/// Get the extend cost as (amount, good display name).
		/// Reads from BiomeService.Blueprints.extendCost (GoodRef).
		/// </summary>
		public static (int amount, string goodDisplayName) GetExtendCost() {
			EnsureTypesCached();

			try {
				var biomeService = GetBiomeService();
				if (biomeService == null) return (0, "Unknown");

				var blueprints = ReflectionHelper.GetProp(_bsBlueprintsProperty, biomeService);
				if (blueprints == null) return (0, "Unknown");

				var extendCost = ReflectionHelper.GetField(_bbcExtendCostField, blueprints);
				if (extendCost == null) return (0, "Unknown");

				var amount = ReflectionHelper.GetInt(_grAmountField, extendCost);
				var goodModel = ReflectionHelper.GetField(_grGoodField, extendCost);
				var displayName = goodModel != null ? (GameReflection.GetDisplayName(goodModel) ?? "Unknown") : "Unknown";

				return (amount, displayName);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ReputationRewardReflection: GetExtendCost failed: {ex.Message}");
				return (0, "Unknown");
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(ReputationRewardReflection), "ReputationRewardReflection");
		}
	}
}
