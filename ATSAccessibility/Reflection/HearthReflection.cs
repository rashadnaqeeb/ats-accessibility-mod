using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to Hearth building internals.
	/// Extracted from BuildingReflection.cs. Follows same caching patterns.
	/// BuildingReflection.IsHearth() remains in BuildingReflection for routing use.
	/// </summary>
	public static class HearthReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// Hearth base fields
		private static FieldInfo _hearthStateField = null;  // Hearth.state
		private static FieldInfo _hearthModelField = null;  // Hearth.model
		private static FieldInfo _hearthStateBurningTimeLeftField = null;  // HearthState.burningTimeLeft
		private static FieldInfo _hearthStateCorruptionField = null;  // HearthState.corruption
		private static FieldInfo _hearthStateHubIndexField = null;  // HearthState.hubIndex
		private static FieldInfo _hearthStateWorkersField = null;  // HearthState.workers
		private static FieldInfo _hearthModelMaxBurningTimeField = null;  // HearthModel.maxBurningTime
		private static FieldInfo _hearthModelMinTimeToShowNoFuelField = null;  // HearthModel.minTimeToShowNoFuel
		private static MethodInfo _hearthIsMainHearthMethod = null;  // Hearth.IsMainHearth()
		private static MethodInfo _hearthGetRangeMethod = null;  // Hearth.GetRange()
		private static MethodInfo _hearthGetCorruptionRateMethod = null;  // Hearth.GetCorruptionRate()
		private static bool _hearthBaseFieldsCached = false;

		// Hearth sacrifice fields
		private static FieldInfo _hearthStateSacrificeRecipesField = null;  // HearthState.sacrificeRecipes (List<HearthSacrificeState>)
		private static Type _hearthSacrificeStateType = null;
		private static FieldInfo _hssModelField = null;  // HearthSacrificeState.model (string)
		private static FieldInfo _hssActiveField = null;  // HearthSacrificeState.active (bool)
		private static FieldInfo _hssLevelField = null;  // HearthSacrificeState.level (int)
		private static Type _hearthSacrificeRecipeModelType = null;
		private static FieldInfo _hsrmDisplayNameField = null;  // HearthSacrificeRecipeModel.displayName (LocaText)
		private static FieldInfo _hsrmMaxLevelField = null;  // HearthSacrificeRecipeModel.maxLevel (int)
		private static FieldInfo _hsrmGoodPerMinField = null;  // HearthSacrificeRecipeModel.goodPerMin (GoodRef)
		private static FieldInfo _hsrmEffectField = null;  // HearthSacrificeRecipeModel.effect (EffectModel)
		private static MethodInfo _hearthGetEffectLevelMethod = null;  // Hearth.GetEffectLevel(HearthSacrificeState)
		private static MethodInfo _hearthGetMaxLevelForMethod = null;  // Hearth.GetMaxLevelFor(HearthSacrificeState)
		private static MethodInfo _hearthHaveGoodsForMethod = null;  // Hearth.HaveGoodsFor(HearthSacrificeState)
		private static MethodInfo _hearthSetSacrificeEffectLevelMethod = null;  // Hearth.SetSacrificeEffectLevel(HearthSacrificeState, int)
		private static MethodInfo _settingsGetHearthSacrificeRecipeMethod = null;  // Settings.GetHearthSacrificeRecipe(string)
		private static PropertyInfo _effectModelDescProp = null;  // EffectModel.Description (string)
		private static MethodInfo _effectsServiceGetHearthSacrificeRateMethod = null;  // IEffectsService.GetHearthSacraficeRate()
		private static bool _hearthSacrificeTypesCached = false;

		// Hearth fuel fields
		private static PropertyInfo _goodsServiceFuelsProperty = null;  // IGoodsService.Fuels (GoodModel[])
		private static MethodInfo _hearthServiceCanBeBurnedMethod = null;  // IHearthService.CanBeBurned(string)
		private static MethodInfo _hearthServiceSetCanBeBurnedMethod = null;  // IHearthService.SetCanBeBurned(string, bool)
		private static MethodInfo _hearthServiceGetPriorityMethod = null;  // IHearthService.GetPriority(string)
		private static MethodInfo _hearthServiceSetPriorityMethod = null;  // IHearthService.SetPriority(string, int)
		private static PropertyInfo _gsHearthServiceProperty = null;  // IGameServices.HearthService
		private static PropertyInfo _gsGoodsServiceProperty = null;  // IGameServices.GoodsService
		private static FieldInfo _goodModelDisplayNameField = null;  // GoodModel.displayName
		private static PropertyInfo _goodModelNameProperty = null;  // GoodModel.Name
		private static bool _hearthFuelTypesCached = false;

		// Hearth services fields (The Commons)
		private static MethodInfo _metaPerksAreHearthServicesUnlockedMethod = null;  // IMetaPerksService.AreHearthServicesUnlocked()
		private static MethodInfo _hearthAreHearthServicesEnabledMethod = null;  // Hearth.AreHearthServicesEnabled()
		private static MethodInfo _hearthUnlockExtraRecipesMethod = null;  // Hearth.UnlockExtraRecipes()
		private static MethodInfo _hearthOnExtraRecipesUnlockedMethod = null;  // Hearth.OnExtraRecipesUnlocked()
		private static PropertyInfo _buildingsServiceHearthsProperty = null;  // IBuildingsService.Hearths
		private static FieldInfo _hearthModelExtraRecipesField = null;  // HearthModel.extraRecipes (HearthNeedRecipeModel[])
		private static FieldInfo _hearthModelExtraRecipesUnlockPriceField = null;  // HearthModel.extraRecipesUnlockPrice (GoodRef)
		private static Type _hearthNeedRecipeModelType = null;
		private static FieldInfo _hnrmServedNeedField = null;  // HearthNeedRecipeModel.servedNeed (NeedModel)
		private static FieldInfo _hnrmRequiredGoodField = null;  // HearthNeedRecipeModel.requiredGood (GoodRef)
		private static FieldInfo _hnrmGradeField = null;  // HearthNeedRecipeModel.grade (RecipeGradeModel)
		private static FieldInfo _hnrmIsGoodConsumedField = null;  // HearthNeedRecipeModel.isGoodConsumed (bool)
		private static Type _recipeGradeModelType = null;
		private static FieldInfo _rgmLevelField = null;  // RecipeGradeModel.level (int)
		private static FieldInfo _rgmDescriptionField = null;  // RecipeGradeModel.description (LocaText)
		private static PropertyInfo _needModelDisplayNameProperty = null;  // NeedModel.DisplayName (string)
		private static bool _hearthServicesTypesCached = false;

		// Hearth hub/upgrade fields
		private static Type _hubTierType = null;
		private static FieldInfo _hubTierIndexField = null;  // HubTier.index
		private static FieldInfo _hubTierEffectField = null;  // HubTier.effect
		private static FieldInfo _hubTierDisplayNameField = null;  // HubTier.displayName
		private static FieldInfo _hubTierMinPopulationField = null;  // HubTier.minPopulation
		private static FieldInfo _hubTierMinInstitutionsField = null;  // HubTier.minInstitutions
		private static FieldInfo _hubTierDecorationsField = null;  // HubTier.decorations (DecorationRequirement[])
		private static Type _decorationRequirementType = null;
		private static FieldInfo _decorReqTierField = null;  // DecorationRequirement.tier
		private static FieldInfo _decorReqAmountField = null;  // DecorationRequirement.amount
		private static Type _decorationTierType = null;
		private static FieldInfo _settingsHubsTiersField = null;  // Settings.hubsTiers
		private static MethodInfo _metaPerksServiceGetUnlockedHubsMethod = null;  // MetaPerksService.GetUnlockedHubs()
		private static PropertyInfo _mbMetaPerksServiceProperty = null;  // MB.MetaPerksService
		private static MethodInfo _hearthIsInRangeMethod = null;  // Hearth.IsInRange(Building)
		private static PropertyInfo _buildingsServiceHousesProperty = null;  // IBuildingsService.Houses
		private static PropertyInfo _buildingsServiceInstitutionsProperty = null;  // IBuildingsService.Institutions
		private static PropertyInfo _buildingsServiceDecorationsProperty = null;  // IBuildingsService.Decorations
		private static FieldInfo _decorModelHasDecorationTierField = null;  // DecorationModel.hasDecorationTier
		private static FieldInfo _decorModelTierField = null;  // DecorationModel.tier
		private static FieldInfo _decorModelDecorationScoreField = null;  // DecorationModel.decorationScore
		private static bool _hubTierTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureHearthBaseFields() {
			if (_hearthBaseFieldsCached) return;
			_hearthBaseFieldsCached = true;

			// Ensure the Hearth type is cached in BuildingReflection first
			BuildingReflection.EnsureHearthBaseType();

			ReflectionHelper.InitCache("HearthReflection.Hearth", assembly => {
				var hearthType = BuildingReflection.HearthType;
				if (hearthType != null) {
					_hearthStateField = hearthType.GetField("state", GameReflection.PublicInstance);
					_hearthModelField = hearthType.GetField("model", GameReflection.PublicInstance);
					_hearthIsMainHearthMethod = hearthType.GetMethod("IsMainHearth", GameReflection.PublicInstance);
					_hearthGetRangeMethod = hearthType.GetMethod("GetRange", GameReflection.PublicInstance);
					_hearthGetCorruptionRateMethod = hearthType.GetMethod("GetCorruptionRate", GameReflection.PublicInstance);
				}

				var hearthStateType = assembly.GetType("Eremite.Buildings.HearthState");
				if (hearthStateType != null) {
					_hearthStateBurningTimeLeftField = hearthStateType.GetField("burningTimeLeft", GameReflection.PublicInstance);
					_hearthStateCorruptionField = hearthStateType.GetField("corruption", GameReflection.PublicInstance);
					_hearthStateHubIndexField = hearthStateType.GetField("hubIndex", GameReflection.PublicInstance);
					_hearthStateWorkersField = hearthStateType.GetField("workers", GameReflection.PublicInstance);
				}

				var hearthModelType = assembly.GetType("Eremite.Buildings.HearthModel");
				if (hearthModelType != null) {
					_hearthModelMaxBurningTimeField = hearthModelType.GetField("maxBurningTime", GameReflection.PublicInstance);
					_hearthModelMinTimeToShowNoFuelField = hearthModelType.GetField("minTimeToShowNoFuel", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureHearthSacrificeTypes() {
			if (_hearthSacrificeTypesCached) return;
			_hearthSacrificeTypesCached = true;

			// Ensure base hearth fields are cached first
			EnsureHearthBaseFields();

			ReflectionHelper.InitCache("HearthReflection.HearthSacrifice", assembly => {
				// HearthState.sacrificeRecipes field
				var hearthStateType = assembly.GetType("Eremite.Buildings.HearthState");
				if (hearthStateType != null) {
					_hearthStateSacrificeRecipesField = hearthStateType.GetField("sacrificeRecipes", GameReflection.PublicInstance);
				}

				// HearthSacrificeState type and fields
				_hearthSacrificeStateType = assembly.GetType("Eremite.Buildings.HearthSacrificeState");
				if (_hearthSacrificeStateType != null) {
					_hssModelField = _hearthSacrificeStateType.GetField("model", GameReflection.PublicInstance);
					_hssActiveField = _hearthSacrificeStateType.GetField("active", GameReflection.PublicInstance);
					_hssLevelField = _hearthSacrificeStateType.GetField("level", GameReflection.PublicInstance);
				}

				// HearthSacrificeRecipeModel type and fields
				_hearthSacrificeRecipeModelType = assembly.GetType("Eremite.Buildings.HearthSacrificeRecipeModel");
				if (_hearthSacrificeRecipeModelType != null) {
					_hsrmDisplayNameField = _hearthSacrificeRecipeModelType.GetField("displayName", GameReflection.PublicInstance);
					_hsrmMaxLevelField = _hearthSacrificeRecipeModelType.GetField("maxLevel", GameReflection.PublicInstance);
					_hsrmGoodPerMinField = _hearthSacrificeRecipeModelType.GetField("goodPerMin", GameReflection.PublicInstance);
					_hsrmEffectField = _hearthSacrificeRecipeModelType.GetField("effect", GameReflection.PublicInstance);
				}

				// Hearth methods for sacrifice
				var hearthType = BuildingReflection.HearthType;
				if (hearthType != null) {
					_hearthGetEffectLevelMethod = hearthType.GetMethod("GetEffectLevel", GameReflection.PublicInstance);
					_hearthGetMaxLevelForMethod = hearthType.GetMethod("GetMaxLevelFor", GameReflection.PublicInstance);
					_hearthHaveGoodsForMethod = hearthType.GetMethod("HaveGoodsFor", GameReflection.PublicInstance);
					_hearthSetSacrificeEffectLevelMethod = hearthType.GetMethod("SetSacrificeEffectLevel", GameReflection.PublicInstance);
				}

				// Settings.GetHearthSacrificeRecipe method
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetHearthSacrificeRecipeMethod = settingsType.GetMethod("GetHearthSacrificeRecipe", GameReflection.PublicInstance);
				}

				// EffectModel.Description property
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectModelDescProp = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
				}

				// IEffectsService.GetHearthSacraficeRate method
				var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_effectsServiceGetHearthSacrificeRateMethod = effectsServiceType.GetMethod("GetHearthSacraficeRate", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureHearthFuelTypes() {
			if (_hearthFuelTypesCached) return;
			_hearthFuelTypesCached = true;

			ReflectionHelper.InitCache("HearthReflection.HearthFuel", assembly => {
				// Get GoodsService and HearthService properties from IGameServices
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGoodsServiceProperty = gameServicesType.GetProperty("GoodsService", GameReflection.PublicInstance);
					_gsHearthServiceProperty = gameServicesType.GetProperty("HearthService", GameReflection.PublicInstance);
				}

				// IGoodsService.Fuels property
				var goodsServiceType = assembly.GetType("Eremite.Services.IGoodsService");
				if (goodsServiceType != null) {
					_goodsServiceFuelsProperty = goodsServiceType.GetProperty("Fuels", GameReflection.PublicInstance);
				}

				// IHearthService methods
				var hearthServiceType = assembly.GetType("Eremite.Services.IHearthService");
				if (hearthServiceType != null) {
					_hearthServiceCanBeBurnedMethod = hearthServiceType.GetMethod("CanBeBurned", GameReflection.PublicInstance);
					_hearthServiceSetCanBeBurnedMethod = hearthServiceType.GetMethod("SetCanBeBurned", GameReflection.PublicInstance);
					_hearthServiceGetPriorityMethod = hearthServiceType.GetMethod("GetPriority", GameReflection.PublicInstance);
					_hearthServiceSetPriorityMethod = hearthServiceType.GetMethod("SetPriority", GameReflection.PublicInstance);
				}

				// GoodModel fields
				var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
				if (goodModelType != null) {
					_goodModelDisplayNameField = goodModelType.GetField("displayName", GameReflection.PublicInstance);
					_goodModelNameProperty = goodModelType.GetProperty("Name", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureHearthServicesTypes() {
			if (_hearthServicesTypesCached) return;
			_hearthServicesTypesCached = true;

			// Ensure base hearth fields are cached first
			EnsureHearthBaseFields();

			ReflectionHelper.InitCache("HearthReflection.HearthServices", assembly => {
				// IMetaPerksService.AreHearthServicesUnlocked()
				var metaPerksServiceType = assembly.GetType("Eremite.Services.IMetaPerksService");
				if (metaPerksServiceType != null) {
					_metaPerksAreHearthServicesUnlockedMethod = metaPerksServiceType.GetMethod("AreHearthServicesUnlocked", GameReflection.PublicInstance);
				}

				var hearthType = BuildingReflection.HearthType;
				if (hearthType != null) {
					_hearthAreHearthServicesEnabledMethod = hearthType.GetMethod("AreHearthServicesEnabled", GameReflection.PublicInstance);
					_hearthUnlockExtraRecipesMethod = hearthType.GetMethod("UnlockExtraRecipes", GameReflection.PublicInstance);
					_hearthOnExtraRecipesUnlockedMethod = hearthType.GetMethod("OnExtraRecipesUnlocked", GameReflection.PublicInstance);
				}

				// IBuildingsService.Hearths property
				var buildingsServiceType = assembly.GetType("Eremite.Services.IBuildingsService");
				if (buildingsServiceType != null) {
					_buildingsServiceHearthsProperty = buildingsServiceType.GetProperty("Hearths", GameReflection.PublicInstance);
				}

				// HearthModel fields
				var hearthModelType = assembly.GetType("Eremite.Buildings.HearthModel");
				if (hearthModelType != null) {
					_hearthModelExtraRecipesField = hearthModelType.GetField("extraRecipes", GameReflection.PublicInstance);
					_hearthModelExtraRecipesUnlockPriceField = hearthModelType.GetField("extraRecipesUnlockPrice", GameReflection.PublicInstance);
				}

				// HearthNeedRecipeModel type and fields
				_hearthNeedRecipeModelType = assembly.GetType("Eremite.Buildings.HearthNeedRecipeModel");
				if (_hearthNeedRecipeModelType != null) {
					_hnrmServedNeedField = _hearthNeedRecipeModelType.GetField("servedNeed", GameReflection.PublicInstance);
					_hnrmRequiredGoodField = _hearthNeedRecipeModelType.GetField("requiredGood", GameReflection.PublicInstance);
					_hnrmGradeField = _hearthNeedRecipeModelType.GetField("grade", GameReflection.PublicInstance);
					_hnrmIsGoodConsumedField = _hearthNeedRecipeModelType.GetField("isGoodConsumed", GameReflection.PublicInstance);
				}

				// RecipeGradeModel type and fields
				_recipeGradeModelType = assembly.GetType("Eremite.Buildings.RecipeGradeModel");
				if (_recipeGradeModelType != null) {
					_rgmLevelField = _recipeGradeModelType.GetField("level", GameReflection.PublicInstance);
					_rgmDescriptionField = _recipeGradeModelType.GetField("description", GameReflection.PublicInstance);
				}

				// NeedModel.DisplayName property
				var needModelType = assembly.GetType("Eremite.Model.NeedModel");
				if (needModelType != null) {
					_needModelDisplayNameProperty = needModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureHubTierTypes() {
			if (_hubTierTypesCached) return;
			_hubTierTypesCached = true;

			EnsureHearthBaseFields();
			BuildingReflection.EnsureHouseTypes();
			InstitutionReflection.EnsureTypes();
			BuildingReflection.EnsureDecorationType();

			ReflectionHelper.InitCache("HearthReflection.HubTier", assembly => {
				// HubTier type and fields
				_hubTierType = assembly.GetType("Eremite.Buildings.HubTier");
				if (_hubTierType != null) {
					_hubTierIndexField = _hubTierType.GetField("index", GameReflection.PublicInstance);
					_hubTierEffectField = _hubTierType.GetField("effect", GameReflection.PublicInstance);
					_hubTierDisplayNameField = _hubTierType.GetField("displayName", GameReflection.PublicInstance);
					_hubTierMinPopulationField = _hubTierType.GetField("minPopulation", GameReflection.PublicInstance);
					_hubTierMinInstitutionsField = _hubTierType.GetField("minInstitutions", GameReflection.PublicInstance);
					_hubTierDecorationsField = _hubTierType.GetField("decorations", GameReflection.PublicInstance);
				}

				// DecorationRequirement type and fields
				_decorationRequirementType = assembly.GetType("Eremite.Buildings.DecorationRequirement");
				if (_decorationRequirementType != null) {
					_decorReqTierField = _decorationRequirementType.GetField("tier", GameReflection.PublicInstance);
					_decorReqAmountField = _decorationRequirementType.GetField("amount", GameReflection.PublicInstance);
				}

				// DecorationTier type (for comparing tiers)
				_decorationTierType = assembly.GetType("Eremite.Buildings.DecorationTier");

				// Settings.hubsTiers field
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsHubsTiersField = settingsType.GetField("hubsTiers", GameReflection.PublicInstance);
				}

				// MB.MetaPerksService property
				var mbType = assembly.GetType("Eremite.MB");
				if (mbType != null) {
					_mbMetaPerksServiceProperty = mbType.GetProperty("MetaPerksService", BindingFlags.NonPublic | BindingFlags.Static);
				}

				// MetaPerksService.GetUnlockedHubs method
				var metaPerksServiceType = assembly.GetType("Eremite.Services.IMetaPerksService");
				if (metaPerksServiceType != null) {
					_metaPerksServiceGetUnlockedHubsMethod = metaPerksServiceType.GetMethod("GetUnlockedHubs", GameReflection.PublicInstance);
				}

				// Hearth.IsInRange(IMapObject) method - specify parameter type to avoid ambiguous match
				var hearthType = BuildingReflection.HearthType;
				if (hearthType != null) {
					var mapObjectType = assembly.GetType("Eremite.IMapObject");
					if (mapObjectType != null) {
						_hearthIsInRangeMethod = hearthType.GetMethod("IsInRange", new[] { mapObjectType });
					}
				}

				// BuildingsService properties
				var buildingsServiceType = assembly.GetType("Eremite.Services.IBuildingsService");
				if (buildingsServiceType != null) {
					_buildingsServiceHousesProperty = buildingsServiceType.GetProperty("Houses", GameReflection.PublicInstance);
					_buildingsServiceInstitutionsProperty = buildingsServiceType.GetProperty("Institutions", GameReflection.PublicInstance);
					_buildingsServiceDecorationsProperty = buildingsServiceType.GetProperty("Decorations", GameReflection.PublicInstance);
				}

				// DecorationModel fields
				var decorModelType = assembly.GetType("Eremite.Buildings.DecorationModel");
				if (decorModelType != null) {
					_decorModelHasDecorationTierField = decorModelType.GetField("hasDecorationTier", GameReflection.PublicInstance);
					_decorModelTierField = decorModelType.GetField("tier", GameReflection.PublicInstance);
					_decorModelDecorationScoreField = decorModelType.GetField("decorationScore", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// PUBLIC API - HEARTH
		// ========================================

		/// <summary>
		/// Get Hearth fire level (0-1 percentage of max burn time remaining).
		/// </summary>
		public static float GetHearthFireLevel(object building) {
			if (!BuildingReflection.IsHearth(building)) return 0f;

			EnsureHearthBaseFields();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				var model = ReflectionHelper.GetField(_hearthModelField, building);
				if (state == null || model == null) return 0f;

				float burningTimeLeft = (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
				float maxBurningTime = (float?)_hearthModelMaxBurningTimeField?.GetValue(model) ?? 1f;

				return burningTimeLeft / maxBurningTime;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get Hearth remaining fuel time in seconds.
		/// </summary>
		public static float GetHearthFuelTimeRemaining(object building) {
			if (!BuildingReflection.IsHearth(building)) return 0f;

			EnsureHearthBaseFields();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				if (state == null) return 0f;

				return (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Check if Hearth fire is low (warning state).
		/// </summary>
		public static bool IsHearthFireLow(object building) {
			if (!BuildingReflection.IsHearth(building)) return false;

			EnsureHearthBaseFields();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				var model = ReflectionHelper.GetField(_hearthModelField, building);
				if (state == null || model == null) return false;

				float burningTimeLeft = (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
				float minTimeToShowNoFuel = (float?)_hearthModelMinTimeToShowNoFuelField?.GetValue(model) ?? 5f;

				return burningTimeLeft < minTimeToShowNoFuel;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if Hearth fire is out (no fuel).
		/// </summary>
		public static bool IsHearthFireOut(object building) {
			return GetHearthFuelTimeRemaining(building) <= 0f;
		}

		/// <summary>
		/// Get Hearth hub index (-1 = no hub active).
		/// </summary>
		public static int GetHearthHubIndex(object building) {
			if (!BuildingReflection.IsHearth(building)) return -1;

			EnsureHearthBaseFields();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				if (state == null) return -1;

				return (int?)_hearthStateHubIndexField?.GetValue(state) ?? -1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Get Hearth corruption/blight level (0-1).
		/// </summary>
		public static float GetHearthCorruptionRate(object building) {
			if (!BuildingReflection.IsHearth(building)) return 0f;

			EnsureHearthBaseFields();

			try {
				return ReflectionHelper.InvokeFloat(_hearthGetCorruptionRateMethod, building);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get Hearth range (hub radius).
		/// </summary>
		public static float GetHearthRange(object building) {
			if (!BuildingReflection.IsHearth(building)) return 0f;

			EnsureHearthBaseFields();

			try {
				return ReflectionHelper.InvokeFloat(_hearthGetRangeMethod, building);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Check if Hearth is the main (Ancient) Hearth.
		/// </summary>
		public static bool IsMainHearth(object building) {
			if (!BuildingReflection.IsHearth(building)) return false;

			EnsureHearthBaseFields();

			try {
				var result = ReflectionHelper.Invoke(_hearthIsMainHearthMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get worker IDs for a Hearth.
		/// </summary>
		public static int[] GetHearthWorkerIds(object building) {
			if (!BuildingReflection.IsHearth(building)) return new int[0];

			EnsureHearthBaseFields();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				if (state == null) return new int[0];

				return _hearthStateWorkersField?.GetValue(state) as int[] ?? new int[0];
			} catch {
				return new int[0];
			}
		}

		// ========================================
		// PUBLIC API - HEARTH UPGRADES (Hub Tiers)
		// ========================================

		/// <summary>
		/// Data structure for decoration requirement info.
		/// </summary>
		public struct DecorationRequirementInfo {
			public string tierName;
			public int required;
			public int current;
		}

		/// <summary>
		/// Data structure for hearth upgrade tier information.
		/// </summary>
		public struct HearthUpgradeInfo {
			public int index;               // Tier index (0, 1, 2)
			public string displayName;      // "Service 1", "Service 2", "Service 3"
			public string effectDescription;// Effect granted by this tier
			public int minPopulation;       // Required population
			public int currentPopulation;   // Current population in range
			public int minInstitutions;     // Required institutions
			public int currentInstitutions; // Current institutions in range
			public List<DecorationRequirementInfo> decorationRequirements;
			public bool isUnlockedInMeta;   // Unlocked via metaprogression
			public bool isAchieved;         // Currently active (requirements met)
		}

		/// <summary>
		/// Get the number of hub tiers unlocked via metaprogression.
		/// </summary>
		public static int GetUnlockedHubTierCount() {
			EnsureHubTierTypes();

			try {
				var metaPerksService = _mbMetaPerksServiceProperty?.GetValue(null);
				if (metaPerksService == null) return 1;

				var result = ReflectionHelper.Invoke(_metaPerksServiceGetUnlockedHubsMethod, metaPerksService);
				return (int?)result ?? 1;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetUnlockedHubTierCount failed: {ex.Message}");
				return 1;
			}
		}

		/// <summary>
		/// Get upgrade info for all hub tiers for a specific hearth.
		/// </summary>
		public static List<HearthUpgradeInfo> GetHearthUpgradeInfo(object building) {
			var result = new List<HearthUpgradeInfo>();

			if (!BuildingReflection.IsHearth(building)) return result;

			EnsureHubTierTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return result;

				var tiers = _settingsHubsTiersField?.GetValue(settings) as Array;
				if (tiers == null) return result;

				int unlockedCount = GetUnlockedHubTierCount();
				int currentHubIndex = GetHearthHubIndex(building);

				// Get current counts for this hearth
				int currentPop = CountPopulationForHearth(building);
				int currentInst = CountInstitutionsForHearth(building);

				foreach (var tier in tiers) {
					int tierIndex = ReflectionHelper.GetInt(_hubTierIndexField, tier);

					var info = new HearthUpgradeInfo();
					info.decorationRequirements = new List<DecorationRequirementInfo>();

					info.index = tierIndex;
					info.isUnlockedInMeta = tierIndex < unlockedCount;
					info.isAchieved = info.isUnlockedInMeta && currentHubIndex >= info.index;

					// Display name
					var displayNameLoca = ReflectionHelper.GetField(_hubTierDisplayNameField, tier);
					info.displayName = GameReflection.GetLocaText(displayNameLoca) ?? $"Upgrade {info.index + 1}";

					// Only gather detailed info for meta-unlocked tiers
					if (info.isUnlockedInMeta) {
						// Effect description
						var effect = ReflectionHelper.GetField(_hubTierEffectField, tier);
						if (effect != null) {
							EnsureHearthSacrificeTypes(); // For _effectModelDescProp
							info.effectDescription = _effectModelDescProp?.GetValue(effect) as string ?? "";
						}

						// Requirements
						info.minPopulation = ReflectionHelper.GetInt(_hubTierMinPopulationField, tier);
						info.currentPopulation = currentPop;
						info.minInstitutions = ReflectionHelper.GetInt(_hubTierMinInstitutionsField, tier);
						info.currentInstitutions = currentInst;

						// Decoration requirements
						var decorReqs = _hubTierDecorationsField?.GetValue(tier) as Array;
						if (decorReqs != null) {
							foreach (var decorReq in decorReqs) {
								var reqInfo = new DecorationRequirementInfo();

								var decorTier = ReflectionHelper.GetField(_decorReqTierField, decorReq);
								// Get tier name and append "decorations" for clarity
								string tierName = GetDecorationTierName(decorTier);
								reqInfo.tierName = tierName + " decorations";
								reqInfo.required = ReflectionHelper.GetInt(_decorReqAmountField, decorReq);
								reqInfo.current = CountDecorationsForHearth(building, decorTier);

								info.decorationRequirements.Add(reqInfo);
							}
						}
					}

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHearthUpgradeInfo failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get the display name of a decoration tier.
		/// </summary>
		private static string GetDecorationTierName(object decorTier) {
			if (decorTier == null) return "Unknown";

			try {
				// DecorationTier extends LabelModel which has displayName
				var displayNameField = decorTier.GetType().GetField("displayName", GameReflection.PublicInstance);
				var displayNameLoca = displayNameField?.GetValue(decorTier);
				return GameReflection.GetLocaText(displayNameLoca) ?? "Decorations";
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetDecorationTierName failed: {ex.Message}");
				return "Decorations";
			}
		}

		/// <summary>
		/// Count population (residents in houses) within a hearth's range.
		/// </summary>
		private static int CountPopulationForHearth(object hearth) {
			EnsureHubTierTypes();
			BuildingReflection.EnsureHouseTypes();
			BuildingReflection.EnsureHearthBaseType();

			try {
				var houses = ConstructionReflection.GetAllHouses();
				if (houses == null || _hearthIsInRangeMethod == null) return 0;

				int count = 0;

				foreach (var house in houses) {
					// Check if finished using cached method
					bool isFinished = ReflectionHelper.InvokeBool(BuildingReflection.BuildingIsFinishedMethod, house);
					if (!isFinished) continue;

					// Check if in range
					bool inRange = ReflectionHelper.InvokeBool(_hearthIsInRangeMethod, hearth, house);
					if (!inRange) continue;

					// Count residents
					var state = ReflectionHelper.GetField(BuildingReflection.HouseStateField, house);
					if (state != null) {
						var residents = ReflectionHelper.GetList(BuildingReflection.HouseStateResidentsField, state);
						count += residents?.Count ?? 0;
					}
				}

				return count;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CountPopulationForHearth failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Count institutions within a hearth's range.
		/// </summary>
		private static int CountInstitutionsForHearth(object hearth) {
			EnsureHubTierTypes();
			BuildingReflection.EnsureHearthBaseType();

			try {
				var institutions = ConstructionReflection.GetAllInstitutions();
				if (institutions == null) return 0;

				int count = 0;

				foreach (var inst in institutions) {
					// Check if finished using cached method
					bool isFinished = ReflectionHelper.InvokeBool(BuildingReflection.BuildingIsFinishedMethod, inst);
					if (!isFinished) continue;

					bool inRange = ReflectionHelper.InvokeBool(_hearthIsInRangeMethod, hearth, inst);
					if (inRange) count++;
				}

				return count;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CountInstitutionsForHearth failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Count decoration score for a specific tier within a hearth's range.
		/// </summary>
		private static int CountDecorationsForHearth(object hearth, object decorTier) {
			EnsureHubTierTypes();
			BuildingReflection.EnsureHearthBaseType();

			try {
				var decorations = ConstructionReflection.GetAllDecorations();
				if (decorations == null || _hearthIsInRangeMethod == null) return 0;

				int score = 0;

				foreach (var decor in decorations) {
					// Check if finished using cached method
					bool isFinished = ReflectionHelper.InvokeBool(BuildingReflection.BuildingIsFinishedMethod, decor);
					if (!isFinished) continue;

					// Check if decoration has a tier
					var model = decor.GetType().GetField("model", GameReflection.PublicInstance)?.GetValue(decor);
					if (model == null) continue;

					bool hasTier = ReflectionHelper.GetBool(_decorModelHasDecorationTierField, model);
					if (!hasTier) continue;

					// Check if same tier
					var tier = ReflectionHelper.GetField(_decorModelTierField, model);
					if (tier != decorTier) continue;

					// Check if in range
					bool inRange = ReflectionHelper.InvokeBool(_hearthIsInRangeMethod, hearth, decor);
					if (!inRange) continue;

					// Add score
					int decorScore = ReflectionHelper.GetInt(_decorModelDecorationScoreField, model);
					score += decorScore;
				}

				return score;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CountDecorationsForHearth failed: {ex.Message}");
				return 0;
			}
		}

		// ========================================
		// PUBLIC API - HEARTH SACRIFICE
		// ========================================

		/// <summary>
		/// Data structure for hearth sacrifice recipe information.
		/// </summary>
		public struct SacrificeRecipeInfo {
			public string recipeName;       // HearthSacrificeRecipeModel.displayName.Text
			public string goodName;         // HearthSacrificeRecipeModel.goodPerMin.good.displayName.Text
			public float consumptionPerMin; // Actual consumption per minute per level (affected by perks)
			public string effectName;       // HearthSacrificeRecipeModel.effect.DisplayName
			public string effectDescription;// HearthSacrificeRecipeModel.effect.Description
			public int level;               // Current level (0 = off)
			public int maxLevel;            // Max level allowed
			public bool active;             // Is currently active
			public bool canAfford;          // Have goods to enable/continue
		}

		/// <summary>
		/// Get the list of sacrifice recipe states from a Hearth building.
		/// </summary>
		public static List<object> GetHearthSacrificeRecipes(object building) {
			var result = new List<object>();

			if (!BuildingReflection.IsHearth(building)) return result;

			EnsureHearthSacrificeTypes();

			try {
				var state = ReflectionHelper.GetField(_hearthStateField, building);
				if (state == null) return result;

				var recipes = ReflectionHelper.GetList(_hearthStateSacrificeRecipesField, state);
				if (recipes == null) return result;

				foreach (var recipe in recipes) {
					result.Add(recipe);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHearthSacrificeRecipes failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get detailed info about a sacrifice recipe.
		/// </summary>
		public static SacrificeRecipeInfo GetSacrificeRecipeInfo(object hearth, object recipeState) {
			var info = new SacrificeRecipeInfo();

			if (hearth == null || recipeState == null) return info;

			EnsureHearthSacrificeTypes();
			BuildingReflection.EnsureRaceBonusTypes();  // For LocaText.Text
			BuildingReflection.EnsureBlightConfigTypes();  // For GoodRef.DisplayName
			BuildingReflection.EnsureRecipeModelTypes();  // For GoodRef.amount

			try {
				// Get model name from state
				string modelName = ReflectionHelper.GetString(_hssModelField, recipeState);
				if (string.IsNullOrEmpty(modelName)) return info;

				// Get the recipe model from Settings
				var settings = GameReflection.GetSettings();
				if (settings == null) return info;

				var recipeModel = ReflectionHelper.Invoke(_settingsGetHearthSacrificeRecipeMethod, settings, modelName);
				if (recipeModel == null) return info;

				// Get display name
				var displayNameLoca = ReflectionHelper.GetField(_hsrmDisplayNameField, recipeModel);
				if (displayNameLoca != null) {
					info.recipeName = GameReflection.GetLocaText(displayNameLoca) ?? modelName;
				} else {
					info.recipeName = modelName;
				}

				// Get good info
				var goodPerMin = ReflectionHelper.GetField(_hsrmGoodPerMinField, recipeModel);
				if (goodPerMin != null) {
					int baseAmount = ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, goodPerMin);
					info.goodName = ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, goodPerMin) ?? "Unknown";

					// Calculate actual consumption rate (affected by perks)
					// Formula: baseAmount / sacrificeRate
					float sacrificeRate = GetHearthSacrificeRate();
					if (sacrificeRate > 0) {
						info.consumptionPerMin = (float)baseAmount / sacrificeRate;
					} else {
						info.consumptionPerMin = baseAmount;
					}
				}

				// Get max level from model
				info.maxLevel = (int?)_hsrmMaxLevelField?.GetValue(recipeModel) ?? 4;

				// Get effect info
				var effect = ReflectionHelper.GetField(_hsrmEffectField, recipeModel);
				if (effect != null) {
					info.effectName = ReflectionHelper.GetPropString(BuildingReflection.EffectModelDisplayNameProperty, effect) ?? "";
					info.effectDescription = _effectModelDescProp?.GetValue(effect) as string ?? "";
				}

				// Get current state from hearth methods
				info.active = ReflectionHelper.GetBool(_hssActiveField, recipeState);
				info.level = ReflectionHelper.InvokeInt(_hearthGetEffectLevelMethod, hearth, recipeState);

				// Get max level from hearth (may differ due to effects)
				var maxLevelResult = ReflectionHelper.Invoke(_hearthGetMaxLevelForMethod, hearth, recipeState);
				if (maxLevelResult is int maxLevel) {
					info.maxLevel = maxLevel;
				}

				// Check if can afford
				var canAffordResult = ReflectionHelper.Invoke(_hearthHaveGoodsForMethod, hearth, recipeState);
				info.canAfford = (bool?)canAffordResult ?? false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetSacrificeRecipeInfo failed: {ex.Message}");
			}

			return info;
		}

		/// <summary>
		/// Set the sacrifice effect level for a recipe.
		/// </summary>
		public static bool SetHearthSacrificeLevel(object hearth, object recipeState, int level) {
			if (hearth == null || recipeState == null) return false;

			EnsureHearthSacrificeTypes();

			if (_hearthSetSacrificeEffectLevelMethod == null) return false;

			try {
				_hearthSetSacrificeEffectLevelMethod.Invoke(hearth, new object[] { recipeState, level });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetHearthSacrificeLevel failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the current hearth sacrifice rate modifier from EffectsService.
		/// This affects how much goods are consumed per minute for sacrifices.
		/// </summary>
		public static float GetHearthSacrificeRate() {
			EnsureHearthSacrificeTypes();

			try {
				var effectsService = GameReflection.GetEffectsService();
				if (effectsService == null) return 1f;

				var result = ReflectionHelper.Invoke(_effectsServiceGetHearthSacrificeRateMethod, effectsService);
				if (result is float rate) {
					return rate;
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHearthSacrificeRate failed: {ex.Message}");
			}

			return 1f;  // Default rate
		}

		// ========================================
		// PUBLIC API - HEARTH FUEL
		// ========================================

		/// <summary>
		/// Data structure for fuel type information.
		/// </summary>
		public struct FuelInfo {
			public string name;         // Internal name (GoodModel.Name)
			public string displayName;  // Display name (GoodModel.displayName.Text)
			public bool isEnabled;      // Whether this fuel can be burned
			public int priority;        // Burn priority (0-3, higher = preferred)
		}

		/// <summary>
		/// Get list of all fuel types with their enabled status.
		/// </summary>
		public static List<FuelInfo> GetAllFuelTypes() {
			var result = new List<FuelInfo>();

			EnsureHearthFuelTypes();
			BuildingReflection.EnsureRaceBonusTypes();  // For LocaText.Text

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return result;

				// Get GoodsService
				var goodsService = ReflectionHelper.GetProp(_gsGoodsServiceProperty, gameServices);
				if (goodsService == null) return result;

				// Get HearthService
				var hearthService = ReflectionHelper.GetProp(_gsHearthServiceProperty, gameServices);
				if (hearthService == null) return result;

				// Get Fuels array
				var fuels = _goodsServiceFuelsProperty?.GetValue(goodsService) as Array;
				if (fuels == null) return result;

				foreach (var fuel in fuels) {
					if (fuel == null) continue;

					var info = new FuelInfo();

					// Get name
					info.name = ReflectionHelper.GetPropString(_goodModelNameProperty, fuel) ?? "";

					// Get display name
					var displayNameLoca = ReflectionHelper.GetField(_goodModelDisplayNameField, fuel);
					if (displayNameLoca != null) {
						info.displayName = GameReflection.GetLocaText(displayNameLoca) ?? info.name;
					} else {
						info.displayName = info.name;
					}

					// Check if enabled
					info.isEnabled = ReflectionHelper.InvokeBool(_hearthServiceCanBeBurnedMethod, hearthService, info.name);

					// Get priority
					info.priority = ReflectionHelper.InvokeInt(_hearthServiceGetPriorityMethod, hearthService, info.name);

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetAllFuelTypes failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Set whether a fuel type can be burned.
		/// </summary>
		public static bool SetFuelEnabled(string fuelName, bool enabled) {
			EnsureHearthFuelTypes();

			if (_hearthServiceSetCanBeBurnedMethod == null) return false;

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var hearthService = ReflectionHelper.GetProp(_gsHearthServiceProperty, gameServices);
				if (hearthService == null) return false;

				_hearthServiceSetCanBeBurnedMethod.Invoke(hearthService, new object[] { fuelName, enabled });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetFuelEnabled failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the burn priority for a fuel type (0-3).
		/// </summary>
		public static int GetFuelPriority(string fuelName) {
			EnsureHearthFuelTypes();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return 0;

				var hearthService = ReflectionHelper.GetProp(_gsHearthServiceProperty, gameServices);
				if (hearthService == null) return 0;

				return ReflectionHelper.InvokeInt(_hearthServiceGetPriorityMethod, hearthService, fuelName);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetFuelPriority failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Set the burn priority for a fuel type (0-3).
		/// </summary>
		public static bool SetFuelPriority(string fuelName, int priority) {
			EnsureHearthFuelTypes();

			if (_hearthServiceSetPriorityMethod == null) return false;

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var hearthService = ReflectionHelper.GetProp(_gsHearthServiceProperty, gameServices);
				if (hearthService == null) return false;

				_hearthServiceSetPriorityMethod.Invoke(hearthService, new object[] { fuelName, priority });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetFuelPriority failed: {ex.Message}");
				return false;
			}
		}

		// ========================================
		// PUBLIC API - HEARTH SERVICES (THE COMMONS)
		// ========================================

		/// <summary>
		/// Data structure for hearth service recipe information.
		/// </summary>
		public struct HearthServiceInfo {
			public string NeedName;        // servedNeed.DisplayName
			public string GoodName;        // requiredGood internal name
			public string GoodDisplayName; // requiredGood display name
			public int GoodAmount;         // requiredGood amount
			public int Grade;              // grade level (stars)
			public string GradeDescription;// grade.description.Text
			public bool IsGoodConsumed;    // whether goods are consumed
		}

		/// <summary>
		/// Check if Hearth Services are unlocked via meta progression.
		/// </summary>
		public static bool AreHearthServicesMetaUnlocked() {
			EnsureHearthServicesTypes();
			EnsureHubTierTypes();  // For _mbMetaPerksServiceProperty

			try {
				var metaPerksService = _mbMetaPerksServiceProperty?.GetValue(null);
				if (metaPerksService == null) return false;

				var result = ReflectionHelper.Invoke(_metaPerksAreHearthServicesUnlockedMethod, metaPerksService);
				return (bool?)result ?? false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] AreHearthServicesMetaUnlocked failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if The Commons is activated in current settlement.
		/// </summary>
		public static bool AreHearthServicesEnabled(object building) {
			if (!BuildingReflection.IsHearth(building)) return false;

			EnsureHearthServicesTypes();

			try {
				var result = ReflectionHelper.Invoke(_hearthAreHearthServicesEnabledMethod, building);
				return (bool?)result ?? false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] AreHearthServicesEnabled failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the unlock price for The Commons.
		/// Returns (goodName, displayName, amount) or null if not applicable.
		/// </summary>
		public static (string goodName, string displayName, int amount)? GetHearthServicesUnlockPrice(object building) {
			if (!BuildingReflection.IsHearth(building)) return null;

			EnsureHearthServicesTypes();
			BuildingReflection.EnsureBlightConfigTypes();  // For GoodRef fields

			try {
				var model = ReflectionHelper.GetField(_hearthModelField, building);
				if (model == null) return null;

				var unlockPrice = ReflectionHelper.GetField(_hearthModelExtraRecipesUnlockPriceField, model);
				if (unlockPrice == null) return null;

				// Get good name and amount from GoodRef
				string goodName = ReflectionHelper.GetPropString(BuildingReflection.GoodRefNameProperty, unlockPrice);
				string displayName = ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, unlockPrice);
				int amount = ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, unlockPrice);

				if (string.IsNullOrEmpty(goodName)) return null;
				if (string.IsNullOrEmpty(displayName)) displayName = goodName;

				return (goodName, displayName, amount);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHearthServicesUnlockPrice failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Check if the player can afford to unlock The Commons.
		/// </summary>
		public static bool CanAffordHearthServicesUnlock(object building) {
			var price = GetHearthServicesUnlockPrice(building);
			if (price == null) return false;

			int stored = BuildingReflection.GetMainStorageAmountInternal(price.Value.goodName);
			return stored >= price.Value.amount;
		}

		/// <summary>
		/// Unlock The Commons (pay cost and activate hearth services).
		/// </summary>
		public static bool UnlockHearthServices(object building) {
			if (!BuildingReflection.IsHearth(building)) return false;
			if (AreHearthServicesEnabled(building)) return false;
			if (!CanAffordHearthServicesUnlock(building)) return false;

			EnsureHearthServicesTypes();

			if (_hearthUnlockExtraRecipesMethod == null) return false;

			try {
				_hearthUnlockExtraRecipesMethod.Invoke(building, null);
				NotifyAllHearthsServicesUnlocked();
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UnlockHearthServices failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Notify all hearths that services have been unlocked (refreshes their needs cache).
		/// </summary>
		private static void NotifyAllHearthsServicesUnlocked() {
			EnsureHearthServicesTypes();

			if (_hearthOnExtraRecipesUnlockedMethod == null || _buildingsServiceHearthsProperty == null) {
				Debug.LogWarning("[ATSAccessibility] NotifyAllHearthsServicesUnlocked: Missing reflection data");
				return;
			}

			try {
				var buildingsService = GameReflection.GetBuildingsService();
				if (buildingsService == null) return;

				var hearthsDict = _buildingsServiceHearthsProperty.GetValue(buildingsService);
				if (hearthsDict == null) return;

				// Get Values property from Dictionary<int, Hearth>
				var valuesProperty = hearthsDict.GetType().GetProperty("Values", GameReflection.PublicInstance);
				var values = valuesProperty?.GetValue(hearthsDict) as System.Collections.IEnumerable;
				if (values == null) return;

				foreach (var hearth in values) {
					if (hearth != null) {
						_hearthOnExtraRecipesUnlockedMethod.Invoke(hearth, null);
					}
				}

				Debug.Log("[ATSAccessibility] NotifyAllHearthsServicesUnlocked: Notified all hearths");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] NotifyAllHearthsServicesUnlocked failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get service recipes from The Commons.
		/// </summary>
		public static List<HearthServiceInfo> GetHearthServiceRecipes(object building) {
			var result = new List<HearthServiceInfo>();

			if (!BuildingReflection.IsHearth(building)) return result;

			EnsureHearthServicesTypes();
			BuildingReflection.EnsureBlightConfigTypes();  // For GoodRef fields
			BuildingReflection.EnsureRaceBonusTypes();  // For LocaText.Text

			try {
				var model = ReflectionHelper.GetField(_hearthModelField, building);
				if (model == null) return result;

				var extraRecipes = _hearthModelExtraRecipesField?.GetValue(model) as Array;
				if (extraRecipes == null) return result;

				foreach (var recipe in extraRecipes) {
					if (recipe == null) continue;

					var info = new HearthServiceInfo();

					// Get served need name
					var servedNeed = ReflectionHelper.GetField(_hnrmServedNeedField, recipe);
					if (servedNeed != null) {
						info.NeedName = ReflectionHelper.GetPropString(_needModelDisplayNameProperty, servedNeed) ?? "Unknown";
					} else {
						info.NeedName = "Unknown";
					}

					// Get required good info
					info.IsGoodConsumed = ReflectionHelper.GetBool(_hnrmIsGoodConsumedField, recipe);
					if (info.IsGoodConsumed) {
						var requiredGood = ReflectionHelper.GetField(_hnrmRequiredGoodField, recipe);
						if (requiredGood != null) {
							info.GoodName = ReflectionHelper.GetPropString(BuildingReflection.GoodRefNameProperty, requiredGood) ?? "";
							info.GoodDisplayName = ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, requiredGood) ?? info.GoodName;
							info.GoodAmount = ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, requiredGood);
						}
					}

					// Get grade info
					var grade = ReflectionHelper.GetField(_hnrmGradeField, recipe);
					if (grade != null) {
						info.Grade = ReflectionHelper.GetInt(_rgmLevelField, grade);
						var descLoca = ReflectionHelper.GetField(_rgmDescriptionField, grade);
						if (descLoca != null) {
							info.GradeDescription = GameReflection.GetLocaText(descLoca) ?? "";
						}
					}

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHearthServiceRecipes failed: {ex.Message}");
			}

			return result;
		}
	}
}
