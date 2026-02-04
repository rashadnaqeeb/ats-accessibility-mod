using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Provides reflection-based access to embark screen game internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class EmbarkReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// MetaStateService access: MetaController.Instance.MetaServices.MetaStateService
		private static PropertyInfo _msMetaStateServiceProperty = null;

		// EmbarkBonusesState access: MetaStateService.EmbarkBonuses
		private static PropertyInfo _mssEmbarkBonusesProperty = null;

		// EmbarkBonusesState fields
		private static FieldInfo _ebsCaravansField = null;
		private static FieldInfo _ebsEffectsOptionsField = null;
		private static FieldInfo _ebsRewardsPickedField = null;
		private static FieldInfo _ebsGoodsOptionsField = null;
		private static FieldInfo _ebsGoodsPickedField = null;

		// EmbarkCaravanState fields
		private static FieldInfo _ecsRevealedRacesField = null;
		private static FieldInfo _ecsRacesField = null;
		private static FieldInfo _ecsVillagersField = null;
		private static FieldInfo _ecsEmbarkGoodsField = null;
		private static FieldInfo _ecsBonusEmbarkGoodsField = null;
		private static FieldInfo _ecsEmbarkEffectsField = null;
		private static FieldInfo _ecsBonusEmbarkEffectsField = null;

		// ConditionPickState fields (for bonuses)
		private static FieldInfo _cpsNameField = null;
		private static FieldInfo _cpsCostField = null;

		// GoodPickState fields (for goods bonuses)
		private static FieldInfo _gpsNameField = null;
		private static FieldInfo _gpsAmountField = null;
		private static FieldInfo _gpsCostField = null;

		// Good struct fields
		private static FieldInfo _goodNameField = null;
		private static FieldInfo _goodAmountField = null;

		// WorldBlackboardService observables
		private static PropertyInfo _wbbOnFieldPreviewShownProperty = null;
		private static PropertyInfo _wbbOnFieldPreviewClosedProperty = null;
		private static PropertyInfo _wbbPickedCaravanProperty = null;

		// CaravanPickPanel for proper slot selection
		private static Type _caravanPickPanelType = null;
		private static FieldInfo _cppSlotsField = null;
		private static FieldInfo _cppCurrentField = null;
		private static MethodInfo _cppPickMethod = null;

		// DifficultyModel access
		private static Type _difficultyModelType = null;
		private static PropertyInfo _dmIndexProperty = null;
		private static MethodInfo _dmGetDisplayNameMethod = null;
		private static FieldInfo _dmPositiveEffectsField = null;
		private static FieldInfo _dmNegativeEffectsField = null;
		private static FieldInfo _dmRewardsMultiplierField = null;
		private static FieldInfo _dmPreparationPointsPenaltyField = null;
		private static FieldInfo _dmMinEffectCostField = null;
		private static FieldInfo _dmMaxEffectCostField = null;

		// MetaPerksService for base preparation points
		private static PropertyInfo _msMetaPerksServiceProperty = null;
		private static MethodInfo _mpsGetBasePreparationPointsMethod = null;

		// MetaConditionsService for max unlocked difficulty
		private static PropertyInfo _msMetaConditionsServiceProperty = null;
		private static MethodInfo _mcsGetMaxUnlockedDifficultyMethod = null;

		// WorldMapService for min difficulty
		private static MethodInfo _wmsGetMinDifficultyForMethod = null;

		// Settings.difficulties array
		private static FieldInfo _settingsDifficultiesField = null;

		// EmbarkDifficultyPicker for setting difficulty
		private static Type _embarkDifficultyPickerType = null;
		private static MethodInfo _edpSetDifficultyMethod = null;
		private static MethodInfo _edpGetPickedDifficultyMethod = null;
		private static FieldInfo _dpDifficultyField = null;  // From base class DifficultyPicker

		// AscensionModifierModel fields
		private static FieldInfo _ammShortDescField = null;
		private static FieldInfo _ammEffectField = null;
		private static FieldInfo _ammIsShownField = null;

		// WorldEmbarkService for bonus points
		private static PropertyInfo _wsWorldEmbarkServiceProperty = null;
		private static MethodInfo _wesGetBonusPreparationPointsMethod = null;

		// Settings for model lookups
		private static MethodInfo _settingsGetEffectMethod = null;
		private static MethodInfo _settingsGetGoodMethod = null;
		private static MethodInfo _settingsGetRaceMethod = null;

		private static bool _typesCached = false;

		// ========================================
		// CACHED INSTANCE REFERENCES (cleared on panel close)
		// ========================================

		// Cached EmbarkDifficultyPicker - expensive FindObjectOfType call
		private static object _cachedDifficultyPicker = null;

		// Cached min difficulty penalty for the field (game uses min difficulty, not selected)
		private static int _cachedMinDifficultyPenalty = 0;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;
			GameReflection.EnsureMetaControllerTypesInternal(); // Ensures MetaController types are cached

			ReflectionHelper.InitCache("EmbarkReflection", assembly => {
				CacheMetaStateServiceTypes(assembly);
				CacheEmbarkBonusesTypes(assembly);
				CacheCaravanTypes(assembly);
				CacheConditionPickTypes(assembly);
				CacheWorldBlackboardTypes(assembly);
				CacheDifficultyTypes(assembly);
				CacheSettingsTypes(assembly);
			});

			_typesCached = true;
		}

		private static void CacheMetaStateServiceTypes(Assembly gameAssembly) {
			// IMetaServices.MetaStateService
			var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
			if (metaServicesType != null) {
				_msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
					BindingFlags.Public | BindingFlags.Instance);
				_msMetaPerksServiceProperty = metaServicesType.GetProperty("MetaPerksService",
					BindingFlags.Public | BindingFlags.Instance);
				_msMetaConditionsServiceProperty = metaServicesType.GetProperty("MetaConditionsService",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// IMetaStateService.EmbarkBonuses
			var metaStateServiceType = gameAssembly.GetType("Eremite.Services.IMetaStateService");
			if (metaStateServiceType != null) {
				_mssEmbarkBonusesProperty = metaStateServiceType.GetProperty("EmbarkBonuses",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// IMetaPerksService.GetBasePreparationPoints
			var metaPerksServiceType = gameAssembly.GetType("Eremite.Services.IMetaPerksService");
			if (metaPerksServiceType != null) {
				_mpsGetBasePreparationPointsMethod = metaPerksServiceType.GetMethod("GetBasePreparationPoints",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// IMetaConditionsService.GetMaxUnlockedDifficulty
			var metaConditionsServiceType = gameAssembly.GetType("Eremite.Services.IMetaConditionsService");
			if (metaConditionsServiceType != null) {
				_mcsGetMaxUnlockedDifficultyMethod = metaConditionsServiceType.GetMethod("GetMaxUnlockedDifficulty",
					BindingFlags.Public | BindingFlags.Instance);
			}

			Debug.Log("[ATSAccessibility] Cached MetaStateService types");
		}

		private static void CacheEmbarkBonusesTypes(Assembly gameAssembly) {
			var embarkBonusesStateType = gameAssembly.GetType("Eremite.Model.State.EmbarkBonusesState");
			if (embarkBonusesStateType != null) {
				_ebsCaravansField = embarkBonusesStateType.GetField("caravans",
					BindingFlags.Public | BindingFlags.Instance);
				_ebsEffectsOptionsField = embarkBonusesStateType.GetField("effectsOptions",
					BindingFlags.Public | BindingFlags.Instance);
				_ebsRewardsPickedField = embarkBonusesStateType.GetField("rewardsPicked",
					BindingFlags.Public | BindingFlags.Instance);
				_ebsGoodsOptionsField = embarkBonusesStateType.GetField("goodsOptions",
					BindingFlags.Public | BindingFlags.Instance);
				_ebsGoodsPickedField = embarkBonusesStateType.GetField("goodsPicked",
					BindingFlags.Public | BindingFlags.Instance);

				Debug.Log("[ATSAccessibility] Cached EmbarkBonusesState fields");
			}
		}

		private static void CacheCaravanTypes(Assembly gameAssembly) {
			var caravanStateType = gameAssembly.GetType("Eremite.Model.State.EmbarkCaravanState");
			if (caravanStateType != null) {
				_ecsRevealedRacesField = caravanStateType.GetField("revealedRaces",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsRacesField = caravanStateType.GetField("races",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsVillagersField = caravanStateType.GetField("villagers",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsEmbarkGoodsField = caravanStateType.GetField("embarkGoods",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsBonusEmbarkGoodsField = caravanStateType.GetField("bonusEmbarkGoods",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsEmbarkEffectsField = caravanStateType.GetField("embarkEffects",
					BindingFlags.Public | BindingFlags.Instance);
				_ecsBonusEmbarkEffectsField = caravanStateType.GetField("bonusEmbarkEffects",
					BindingFlags.Public | BindingFlags.Instance);

				Debug.Log("[ATSAccessibility] Cached EmbarkCaravanState fields");
			}

			// Good struct
			var goodType = gameAssembly.GetType("Eremite.Model.Good");
			if (goodType != null) {
				_goodNameField = goodType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
				_goodAmountField = goodType.GetField("amount", BindingFlags.Public | BindingFlags.Instance);
				Debug.Log("[ATSAccessibility] Cached Good struct fields");
			}
		}

		private static void CacheConditionPickTypes(Assembly gameAssembly) {
			// ConditionPickState (for effects/buildings)
			var conditionPickStateType = gameAssembly.GetType("Eremite.Model.State.ConditionPickState");
			if (conditionPickStateType != null) {
				_cpsNameField = conditionPickStateType.GetField("name",
					BindingFlags.Public | BindingFlags.Instance);
				_cpsCostField = conditionPickStateType.GetField("cost",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// GoodPickState (for goods)
			var goodPickStateType = gameAssembly.GetType("Eremite.Model.State.GoodPickState");
			if (goodPickStateType != null) {
				_gpsNameField = goodPickStateType.GetField("name",
					BindingFlags.Public | BindingFlags.Instance);
				_gpsAmountField = goodPickStateType.GetField("amount",
					BindingFlags.Public | BindingFlags.Instance);
				_gpsCostField = goodPickStateType.GetField("cost",
					BindingFlags.Public | BindingFlags.Instance);
			}

			Debug.Log("[ATSAccessibility] Cached condition pick types");
		}

		private static void CacheWorldBlackboardTypes(Assembly gameAssembly) {
			var worldBlackboardServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldBlackboardService");
			if (worldBlackboardServiceType != null) {
				_wbbOnFieldPreviewShownProperty = worldBlackboardServiceType.GetProperty("OnFieldPreviewShown",
					BindingFlags.Public | BindingFlags.Instance);
				_wbbOnFieldPreviewClosedProperty = worldBlackboardServiceType.GetProperty("OnFieldPreviewClosed",
					BindingFlags.Public | BindingFlags.Instance);
				_wbbPickedCaravanProperty = worldBlackboardServiceType.GetProperty("PickedCaravan",
					BindingFlags.Public | BindingFlags.Instance);

				Debug.Log("[ATSAccessibility] Cached WorldBlackboardService observable properties");
			}

			// IWorldServices.WorldEmbarkService
			var worldServicesType = gameAssembly.GetType("Eremite.Services.World.IWorldServices");
			if (worldServicesType != null) {
				_wsWorldEmbarkServiceProperty = worldServicesType.GetProperty("WorldEmbarkService",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// IWorldEmbarkService.GetBonusPreparationPoints
			var worldEmbarkServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldEmbarkService");
			if (worldEmbarkServiceType != null) {
				_wesGetBonusPreparationPointsMethod = worldEmbarkServiceType.GetMethod("GetBonusPreparationPoints",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// CaravanPickPanel for triggering UI slot selection
			_caravanPickPanelType = gameAssembly.GetType("Eremite.View.Menu.Pick.CaravanPickPanel");
			if (_caravanPickPanelType != null) {
				_cppSlotsField = _caravanPickPanelType.GetField("slots",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_cppCurrentField = _caravanPickPanelType.GetField("current",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_cppPickMethod = _caravanPickPanelType.GetMethod("Pick",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Debug.Log("[ATSAccessibility] Cached CaravanPickPanel types");
			}
		}

		private static void CacheDifficultyTypes(Assembly gameAssembly) {
			_difficultyModelType = gameAssembly.GetType("Eremite.Model.DifficultyModel");
			if (_difficultyModelType != null) {
				_dmIndexProperty = _difficultyModelType.GetProperty("index",
					BindingFlags.Public | BindingFlags.Instance);
				_dmGetDisplayNameMethod = _difficultyModelType.GetMethod("GetDisplayName",
					BindingFlags.Public | BindingFlags.Instance);
				_dmPositiveEffectsField = _difficultyModelType.GetField("positiveEffects",
					BindingFlags.Public | BindingFlags.Instance);
				_dmNegativeEffectsField = _difficultyModelType.GetField("negativeEffects",
					BindingFlags.Public | BindingFlags.Instance);
				_dmRewardsMultiplierField = _difficultyModelType.GetField("rewardsMultiplier",
					BindingFlags.Public | BindingFlags.Instance);
				_dmPreparationPointsPenaltyField = _difficultyModelType.GetField("preparationPointsPenalty",
					BindingFlags.Public | BindingFlags.Instance);
				_dmMinEffectCostField = _difficultyModelType.GetField("minEffectCost",
					BindingFlags.Public | BindingFlags.Instance);
				_dmMaxEffectCostField = _difficultyModelType.GetField("maxEffectCost",
					BindingFlags.Public | BindingFlags.Instance);

				Debug.Log("[ATSAccessibility] Cached DifficultyModel types");
			}

			// AscensionModifierModel for modifier descriptions
			var ascensionModifierType = gameAssembly.GetType("Eremite.Model.AscensionModifierModel");
			if (ascensionModifierType != null) {
				_ammShortDescField = ascensionModifierType.GetField("shortDesc",
					BindingFlags.Public | BindingFlags.Instance);
				_ammEffectField = ascensionModifierType.GetField("effect",
					BindingFlags.Public | BindingFlags.Instance);
				_ammIsShownField = ascensionModifierType.GetField("isShown",
					BindingFlags.Public | BindingFlags.Instance);
			}

			// EmbarkDifficultyPicker (extends DifficultyPicker)
			_embarkDifficultyPickerType = gameAssembly.GetType("Eremite.WorldMap.UI.EmbarkDifficultyPicker");

			// DifficultyPicker base class methods
			var difficultyPickerType = gameAssembly.GetType("Eremite.WorldMap.UI.DifficultyPicker");
			if (difficultyPickerType != null) {
				_edpSetDifficultyMethod = difficultyPickerType.GetMethod("SetDifficulty",
					BindingFlags.NonPublic | BindingFlags.Instance);
				_edpGetPickedDifficultyMethod = difficultyPickerType.GetMethod("GetPickedDifficulty",
					BindingFlags.Public | BindingFlags.Instance);
				_dpDifficultyField = difficultyPickerType.GetField("difficulty",
					BindingFlags.NonPublic | BindingFlags.Instance);

				Debug.Log("[ATSAccessibility] Cached DifficultyPicker types");
			}

			// IWorldMapService.GetMinDifficultyFor
			var worldMapServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldMapService");
			if (worldMapServiceType != null) {
				_wmsGetMinDifficultyForMethod = worldMapServiceType.GetMethod("GetMinDifficultyFor",
					new Type[] { typeof(Vector3Int) });
			}
		}

		private static void CacheSettingsTypes(Assembly gameAssembly) {
			var settingsType = gameAssembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetEffectMethod = settingsType.GetMethod("GetEffect",
					new Type[] { typeof(string) });
				_settingsGetGoodMethod = settingsType.GetMethod("GetGood",
					new Type[] { typeof(string) });
				_settingsGetRaceMethod = settingsType.GetMethod("GetRace",
					new Type[] { typeof(string) });
				_settingsDifficultiesField = settingsType.GetField("difficulties",
					BindingFlags.Public | BindingFlags.Instance);
			}
		}

		// ========================================
		// INSTANCE CACHE MANAGEMENT
		// ========================================

		/// <summary>
		/// Cache expensive instance references when embark panel opens.
		/// Call this when entering the embark screen.
		/// </summary>
		/// <param name="fieldPos">Field position to cache min difficulty penalty for</param>
		public static void CacheInstancesOnOpen(Vector3Int fieldPos) {
			_cachedDifficultyPicker = FindEmbarkDifficultyPickerInternal();

			// Cache the min difficulty penalty - game uses min difficulty for points calculation,
			// not the currently selected difficulty
			var minDifficulty = GetMinDifficultyFor(fieldPos);
			_cachedMinDifficultyPenalty = minDifficulty != null
				? GetDifficultyPreparationPenalty(minDifficulty)
				: 0;

			Debug.Log($"[ATSAccessibility] EmbarkReflection: Cached instances, min difficulty penalty: {_cachedMinDifficultyPenalty}");
		}

		/// <summary>
		/// Clear cached instance references when embark panel closes.
		/// Instance references become stale on scene changes.
		/// </summary>
		public static void ClearInstanceCaches() {
			_cachedDifficultyPicker = null;
			_cachedMinDifficultyPenalty = 0;
			Debug.Log("[ATSAccessibility] EmbarkReflection: Cleared instance caches");
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		/// <summary>
		/// Get MetaStateService from MetaController.Instance.MetaServices.
		/// DO NOT cache - get fresh reference each time.
		/// </summary>
		public static object GetMetaStateService() {
			EnsureTypes();

			var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
			if (metaController == null) return null;

			var metaServices = ReflectionHelper.GetProp(GameReflection.McMetaServicesProperty, metaController);
			if (metaServices == null) return null;

			return ReflectionHelper.GetProp(_msMetaStateServiceProperty, metaServices);
		}

		/// <summary>
		/// Get MetaPerksService from MetaController.Instance.MetaServices.
		/// </summary>
		private static object GetMetaPerksService() {
			EnsureTypes();
			return GameReflection.GetMetaService(_msMetaPerksServiceProperty);
		}

		/// <summary>
		/// Get WorldEmbarkService from WorldController.Instance.WorldServices.
		/// </summary>
		private static object GetWorldEmbarkService() {
			EnsureTypes();

			var wc = WorldMapReflection.GetWorldController();
			if (wc == null) return null;

			var worldServices = wc.GetType().GetProperty("WorldServices",
				BindingFlags.Public | BindingFlags.Instance)?.GetValue(wc);
			if (worldServices == null) return null;

			return ReflectionHelper.GetProp(_wsWorldEmbarkServiceProperty, worldServices);
		}

		// ========================================
		// EMBARK BONUSES STATE ACCESS
		// ========================================

		/// <summary>
		/// Get the current EmbarkBonusesState from MetaStateService.
		/// </summary>
		public static object GetEmbarkBonuses() {
			var metaStateService = GetMetaStateService();
			return ReflectionHelper.GetProp(_mssEmbarkBonusesProperty, metaStateService);
		}

		/// <summary>
		/// Get the list of caravans from EmbarkBonusesState.
		/// Returns a list of EmbarkCaravanState objects.
		/// </summary>
		public static List<object> GetCaravans() {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			var list = ReflectionHelper.GetList(_ebsCaravansField, embarkBonuses);
			return list?.Cast<object>().ToList() ?? new List<object>();
		}

		/// <summary>
		/// Get available embark effects (not yet picked).
		/// </summary>
		public static List<object> GetEffectsAvailable() {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			var list = ReflectionHelper.GetList(_ebsEffectsOptionsField, embarkBonuses);
			return list?.Cast<object>().ToList() ?? new List<object>();
		}

		/// <summary>
		/// Get picked embark effects.
		/// </summary>
		public static List<object> GetEffectsPicked() {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			var list = ReflectionHelper.GetList(_ebsRewardsPickedField, embarkBonuses);
			return list?.Cast<object>().ToList() ?? new List<object>();
		}

		/// <summary>
		/// Get available embark goods (not yet picked).
		/// </summary>
		public static List<object> GetGoodsAvailable() {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			var list = ReflectionHelper.GetList(_ebsGoodsOptionsField, embarkBonuses);
			return list?.Cast<object>().ToList() ?? new List<object>();
		}

		/// <summary>
		/// Get picked embark goods.
		/// </summary>
		public static List<object> GetGoodsPicked() {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			var list = ReflectionHelper.GetList(_ebsGoodsPickedField, embarkBonuses);
			return list?.Cast<object>().ToList() ?? new List<object>();
		}

		// ========================================
		// CARAVAN DATA ACCESS
		// ========================================

		/// <summary>
		/// Get the number of revealed races in a caravan.
		/// </summary>
		public static int GetCaravanRevealedCount(object caravan) {
			return ReflectionHelper.GetInt(_ecsRevealedRacesField, caravan);
		}

		/// <summary>
		/// Get the race names in a caravan (unique species).
		/// </summary>
		public static List<string> GetCaravanRaces(object caravan) {
			var list = ReflectionHelper.GetList(_ecsRacesField, caravan);
			return list?.Cast<object>().Select(r => r?.ToString() ?? "Unknown").ToList() ?? new List<string>();
		}

		/// <summary>
		/// Get the villager list (one entry per villager, race name).
		/// </summary>
		public static List<string> GetCaravanVillagers(object caravan) {
			var list = ReflectionHelper.GetList(_ecsVillagersField, caravan);
			return list?.Cast<object>().Select(v => v?.ToString() ?? "Unknown").ToList() ?? new List<string>();
		}

		/// <summary>
		/// Get embark goods (base goods) from a caravan.
		/// Returns list of (name, amount) tuples.
		/// </summary>
		public static List<(string name, int amount)> GetCaravanGoods(object caravan) {
			var list = ReflectionHelper.GetList(_ecsEmbarkGoodsField, caravan);
			if (list == null) return new List<(string, int)>();

			var result = new List<(string, int)>();
			foreach (var good in list) {
				var name = ReflectionHelper.GetString(_goodNameField, good) ?? "Unknown";
				var amount = ReflectionHelper.GetInt(_goodAmountField, good);
				result.Add((name, amount));
			}
			return result;
		}

		/// <summary>
		/// Get bonus embark goods from a caravan.
		/// </summary>
		public static List<(string name, int amount)> GetCaravanBonusGoods(object caravan) {
			var list = ReflectionHelper.GetList(_ecsBonusEmbarkGoodsField, caravan);
			if (list == null) return new List<(string, int)>();

			var result = new List<(string, int)>();
			foreach (var good in list) {
				var name = ReflectionHelper.GetString(_goodNameField, good) ?? "Unknown";
				var amount = ReflectionHelper.GetInt(_goodAmountField, good);
				result.Add((name, amount));
			}
			return result;
		}

		/// <summary>
		/// Get embark effects from a caravan.
		/// </summary>
		public static List<string> GetCaravanEffects(object caravan) {
			var list = ReflectionHelper.GetList(_ecsEmbarkEffectsField, caravan);
			return list?.Cast<object>().Select(e => e?.ToString() ?? "Unknown").ToList() ?? new List<string>();
		}

		/// <summary>
		/// Get caravan race counts (villagers per race and unknown race count).
		/// Uses villagers.Distinct() like the game UI does - all villagers have known races.
		/// </summary>
		/// <param name="caravan">The caravan state object</param>
		/// <returns>Tuple of (raceCounts dictionary, unknownRaceCount) where unknownRaceCount is
		/// the number of hidden race slots (gameplayRaces - revealed races)</returns>
		public static (Dictionary<string, int> raceCounts, int unknownRaceCount) GetCaravanRaceCounts(object caravan) {
			var raceCounts = new Dictionary<string, int>();
			int unknownRaceCount = 0;

			if (caravan == null) return (raceCounts, unknownRaceCount);

			var villagers = GetCaravanVillagers(caravan);

			// Count villagers per race (all villagers have known races)
			foreach (var villagerRace in villagers) {
				if (!raceCounts.ContainsKey(villagerRace))
					raceCounts[villagerRace] = 0;
				raceCounts[villagerRace]++;
			}

			// Calculate unknown race slots (gameplayRaces - distinct revealed races)
			int gameplayRaces = GetGameplayRaces();
			int revealedRaceCount = raceCounts.Count;
			unknownRaceCount = Math.Max(0, gameplayRaces - revealedRaceCount);

			return (raceCounts, unknownRaceCount);
		}

		/// <summary>
		/// Get the gameplayRaces setting (max species per settlement, typically 3).
		/// </summary>
		private static int GetGameplayRaces() {
			var settings = GameReflection.GetSettings();
			if (settings == null) return 3; // Default fallback

			var field = settings.GetType().GetField("gameplayRaces",
				BindingFlags.Public | BindingFlags.Instance);
			int value = ReflectionHelper.GetInt(field, settings);
			return value > 0 ? value : 3; // Default fallback
		}

		/// <summary>
		/// Build a display string for a caravan option.
		/// Format: "Human: 3, Beaver: 2, 1 unknown race"
		/// </summary>
		public static string GetCaravanDisplayString(object caravan, int index) {
			var villagers = GetCaravanVillagers(caravan);
			if (villagers.Count == 0) return $"Caravan {index + 1}";

			// Use shared helper for race counting
			var (counts, unknownRaceCount) = GetCaravanRaceCounts(caravan);

			var parts = new List<string>();
			foreach (var kvp in counts) {
				var displayName = GetRaceDisplayName(kvp.Key);
				parts.Add($"{displayName}: {kvp.Value}");
			}
			if (unknownRaceCount > 0) {
				string raceWord = unknownRaceCount == 1 ? "race" : "races";
				parts.Add($"{unknownRaceCount} unknown {raceWord}");
			}

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Get display name for a race from Settings.
		/// </summary>
		public static string GetRaceDisplayName(string raceName) {
			if (string.IsNullOrEmpty(raceName)) return "Unknown";

			var settings = GameReflection.GetSettings();
			if (settings == null) return raceName;

			var raceModel = ReflectionHelper.Invoke(_settingsGetRaceMethod, settings, raceName);
			if (raceModel == null) return raceName;

			// Get displayName from RaceModel
			var displayNameProp = raceModel.GetType().GetProperty("displayName",
				BindingFlags.Public | BindingFlags.Instance);
			var locaText = displayNameProp?.GetValue(raceModel);
			return GameReflection.GetLocaText(locaText) ?? raceName;
		}

		// ========================================
		// CONDITION/GOOD PICK STATE ACCESS
		// ========================================

		/// <summary>
		/// Get the name from a ConditionPickState.
		/// </summary>
		public static string GetConditionPickName(object conditionPick) {
			return ReflectionHelper.GetString(_cpsNameField, conditionPick) ?? "Unknown";
		}

		/// <summary>
		/// Get the cost from a ConditionPickState.
		/// </summary>
		public static int GetConditionPickCost(object conditionPick) {
			return ReflectionHelper.GetInt(_cpsCostField, conditionPick);
		}

		/// <summary>
		/// Get the name from a GoodPickState.
		/// </summary>
		public static string GetGoodPickName(object goodPick) {
			return ReflectionHelper.GetString(_gpsNameField, goodPick) ?? "Unknown";
		}

		/// <summary>
		/// Get the amount from a GoodPickState.
		/// </summary>
		public static int GetGoodPickAmount(object goodPick) {
			return ReflectionHelper.GetInt(_gpsAmountField, goodPick);
		}

		/// <summary>
		/// Get the cost from a GoodPickState.
		/// </summary>
		public static int GetGoodPickCost(object goodPick) {
			return ReflectionHelper.GetInt(_gpsCostField, goodPick);
		}

		/// <summary>
		/// Get display name for an effect from Settings.
		/// </summary>
		public static string GetEffectDisplayName(string effectName) {
			if (string.IsNullOrEmpty(effectName)) return "Unknown";

			var settings = GameReflection.GetSettings();
			if (settings == null) return effectName;

			var effectModel = ReflectionHelper.Invoke(_settingsGetEffectMethod, settings, effectName);
			if (effectModel == null) return effectName;

			// Use DisplayName property (capital D) which is the public accessor
			var displayNameProp = effectModel.GetType().GetProperty("DisplayName",
				BindingFlags.Public | BindingFlags.Instance);
			return displayNameProp?.GetValue(effectModel)?.ToString() ?? effectName;
		}

		/// <summary>
		/// Get display name for a good from Settings.
		/// </summary>
		public static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return "Unknown";

			var settings = GameReflection.GetSettings();
			if (settings == null) return goodName;

			var goodModel = ReflectionHelper.Invoke(_settingsGetGoodMethod, settings, goodName);
			if (goodModel == null) return goodName;

			var displayNameProp = goodModel.GetType().GetProperty("displayName",
				BindingFlags.Public | BindingFlags.Instance);
			var locaText = displayNameProp?.GetValue(goodModel);
			return GameReflection.GetLocaText(locaText) ?? goodName;
		}

		// ========================================
		// CARAVAN SELECTION
		// ========================================

		/// <summary>
		/// Get the currently picked caravan from WorldBlackboardService.
		/// </summary>
		public static object GetPickedCaravan() {
			EnsureTypes();

			var wbb = WorldMapReflection.GetWorldBlackboardService();
			if (wbb == null) return null;

			var reactiveProp = ReflectionHelper.GetProp(_wbbPickedCaravanProperty, wbb);
			if (reactiveProp == null) return null;

			// Get Value from ReactiveProperty<EmbarkCaravanState>
			var valueProp = reactiveProp.GetType().GetProperty("Value",
				BindingFlags.Public | BindingFlags.Instance);
			var caravan = valueProp?.GetValue(reactiveProp);

			// Log for debugging
			if (caravan != null) {
				var villagers = GetCaravanVillagers(caravan);
				Debug.Log($"[ATSAccessibility] GetPickedCaravan: Retrieved caravan with {villagers.Count} villagers: {string.Join(", ", villagers)}");
			}

			return caravan;
		}

		/// <summary>
		/// Set the picked caravan by triggering the game's UI selection.
		/// This properly updates both the reactive property AND the UI state.
		/// </summary>
		public static void SetPickedCaravan(object caravanState) {
			EnsureTypes();

			try {
				if (caravanState == null) {
					Debug.LogWarning("[ATSAccessibility] SetPickedCaravan: caravanState is null!");
					return;
				}

				var villagers = GetCaravanVillagers(caravanState);
				Debug.Log($"[ATSAccessibility] SetPickedCaravan: Setting caravan with {villagers.Count} villagers: {string.Join(", ", villagers)}");

				// Find the caravan index
				var caravans = GetCaravans();
				int targetIndex = -1;
				for (int i = 0; i < caravans.Count; i++) {
					if (ReferenceEquals(caravans[i], caravanState)) {
						targetIndex = i;
						break;
					}
				}

				if (targetIndex < 0) {
					Debug.LogError("[ATSAccessibility] SetPickedCaravan: Caravan not found in list!");
					return;
				}

				// Try to use CaravanPickPanel.Pick() for proper UI state update
				if (SetPickedCaravanViaUI(targetIndex)) {
					Debug.Log($"[ATSAccessibility] SetPickedCaravan: Successfully set via UI (index {targetIndex})");
					return;
				}

				// Fallback: set directly on reactive property (may not persist)
				Debug.LogWarning("[ATSAccessibility] SetPickedCaravan: UI method failed, falling back to direct set");
				SetPickedCaravanDirect(caravanState);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetPickedCaravan failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Set caravan via the game's CaravanPickPanel UI.
		/// </summary>
		private static bool SetPickedCaravanViaUI(int slotIndex) {
			if (_caravanPickPanelType == null || _cppSlotsField == null || _cppPickMethod == null) {
				Debug.Log("[ATSAccessibility] SetPickedCaravanViaUI: CaravanPickPanel types not cached");
				return false;
			}

			try {
				// Find CaravanPickPanel in scene
				var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new Type[] { typeof(Type) },
					null);
				var panel = findMethod?.Invoke(null, new object[] { _caravanPickPanelType });

				if (panel == null) {
					Debug.Log("[ATSAccessibility] SetPickedCaravanViaUI: CaravanPickPanel not found in scene");
					return false;
				}

				// Get slots list
				var slots = ReflectionHelper.GetList(_cppSlotsField, panel);
				if (slots == null || slotIndex >= slots.Count) {
					Debug.Log($"[ATSAccessibility] SetPickedCaravanViaUI: Invalid slot index {slotIndex}, slots count: {slots?.Count}");
					return false;
				}

				var targetSlot = slots[slotIndex];
				if (targetSlot == null) {
					Debug.Log("[ATSAccessibility] SetPickedCaravanViaUI: Target slot is null");
					return false;
				}

				// Call Pick(CaravanPickSlot slot) on the panel
				ReflectionHelper.InvokeVoid(_cppPickMethod, panel, targetSlot);
				Debug.Log($"[ATSAccessibility] SetPickedCaravanViaUI: Called Pick on slot {slotIndex}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetPickedCaravanViaUI failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Direct fallback: set caravan on reactive property (may not persist if UI resets).
		/// </summary>
		private static void SetPickedCaravanDirect(object caravanState) {
			var wbb = WorldMapReflection.GetWorldBlackboardService();
			if (wbb == null) return;

			var reactiveProp = ReflectionHelper.GetProp(_wbbPickedCaravanProperty, wbb);
			if (reactiveProp == null) return;

			var valueProp = reactiveProp.GetType().GetProperty("Value",
				BindingFlags.Public | BindingFlags.Instance);
			valueProp?.SetValue(reactiveProp, caravanState);
		}

		/// <summary>
		/// Get the index of the currently picked caravan (0-2), or -1 if none.
		/// </summary>
		public static int GetPickedCaravanIndex() {
			var picked = GetPickedCaravan();
			if (picked == null) {
				Debug.Log("[ATSAccessibility] GetPickedCaravanIndex: No caravan picked (null)");
				return -1;
			}

			var caravans = GetCaravans();
			Debug.Log($"[ATSAccessibility] GetPickedCaravanIndex: Checking {caravans.Count} caravans");

			for (int i = 0; i < caravans.Count; i++) {
				bool isMatch = ReferenceEquals(caravans[i], picked);
				Debug.Log($"[ATSAccessibility] GetPickedCaravanIndex: Caravan[{i}] ReferenceEquals={isMatch}, same hash={caravans[i]?.GetHashCode() == picked?.GetHashCode()}");
				if (isMatch)
					return i;
			}

			Debug.LogWarning("[ATSAccessibility] GetPickedCaravanIndex: Picked caravan not found in caravans list!");
			return -1;
		}

		// ========================================
		// EMBARK POINTS CALCULATION
		// ========================================

		/// <summary>
		/// Get base preparation points available.
		/// </summary>
		public static int GetBasePreparationPoints() {
			EnsureTypes();
			var metaPerksService = GetMetaPerksService();
			return ReflectionHelper.InvokeInt(_mpsGetBasePreparationPointsMethod, metaPerksService);
		}

		/// <summary>
		/// Get bonus preparation points available (from cycle effects, etc.).
		/// </summary>
		public static int GetBonusPreparationPoints() {
			EnsureTypes();
			var worldEmbarkService = GetWorldEmbarkService();
			return ReflectionHelper.InvokeInt(_wesGetBonusPreparationPointsMethod, worldEmbarkService);
		}

		/// <summary>
		/// Get total preparation points available (base + bonus).
		/// Uses the min difficulty penalty (cached on panel open), matching the game's behavior
		/// where base points are calculated from the minimum required difficulty, not selected difficulty.
		/// </summary>
		public static int GetTotalPreparationPoints() {
			int rawBase = GetBasePreparationPoints();
			// Use cached min difficulty penalty - game calculates base points using
			// GetMinDifficultyFor(field).preparationPointsPenalty, not current selection
			return Math.Max(0, rawBase + _cachedMinDifficultyPenalty) + GetBonusPreparationPoints();
		}

		/// <summary>
		/// Calculate total points used from picked bonuses.
		/// </summary>
		public static int CalculatePointsUsed() {
			int total = 0;

			// Sum effect costs
			foreach (var effect in GetEffectsPicked()) {
				total += GetConditionPickCost(effect);
			}

			// Sum good costs
			foreach (var good in GetGoodsPicked()) {
				total += GetGoodPickCost(good);
			}

			return total;
		}

		/// <summary>
		/// Calculate remaining points (accounts for current difficulty penalty).
		/// </summary>
		public static int CalculatePointsRemaining() {
			return GetTotalPreparationPoints() - CalculatePointsUsed();
		}

		// ========================================
		// BONUS TOGGLING
		// ========================================

		/// <summary>
		/// Toggle an effect bonus (add if available, remove if picked).
		/// Returns true if toggled successfully, false if cannot afford.
		/// </summary>
		public static (bool success, bool added) ToggleEffectBonus(object effectPick) {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			if (embarkBonuses == null) return (false, false);

			try {
				var availableList = ReflectionHelper.GetList(_ebsEffectsOptionsField, embarkBonuses);
				var pickedList = ReflectionHelper.GetList(_ebsRewardsPickedField, embarkBonuses);
				if (availableList == null || pickedList == null) return (false, false);

				// Check if it's in available list (add)
				if (availableList.Contains(effectPick)) {
					// Check if we can afford
					int cost = GetConditionPickCost(effectPick);
					if (cost > CalculatePointsRemaining())
						return (false, false); // Can't afford

					availableList.Remove(effectPick);
					pickedList.Add(effectPick);
					return (true, true); // Added
				}
				// Check if it's in picked list (remove)
				else if (pickedList.Contains(effectPick)) {
					pickedList.Remove(effectPick);
					availableList.Add(effectPick);
					return (true, false); // Removed
				}

				return (false, false);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ToggleEffectBonus failed: {ex.Message}");
				return (false, false);
			}
		}

		/// <summary>
		/// Toggle a good bonus (add if available, remove if picked).
		/// Returns true if toggled successfully, false if cannot afford.
		/// </summary>
		public static (bool success, bool added) ToggleGoodBonus(object goodPick) {
			EnsureTypes();
			var embarkBonuses = GetEmbarkBonuses();
			if (embarkBonuses == null) return (false, false);

			try {
				var availableList = ReflectionHelper.GetList(_ebsGoodsOptionsField, embarkBonuses);
				var pickedList = ReflectionHelper.GetList(_ebsGoodsPickedField, embarkBonuses);
				if (availableList == null || pickedList == null) return (false, false);

				// Check if it's in available list (add)
				if (availableList.Contains(goodPick)) {
					// Check if we can afford
					int cost = GetGoodPickCost(goodPick);
					if (cost > CalculatePointsRemaining())
						return (false, false); // Can't afford

					availableList.Remove(goodPick);
					pickedList.Add(goodPick);
					return (true, true); // Added
				}
				// Check if it's in picked list (remove)
				else if (pickedList.Contains(goodPick)) {
					pickedList.Remove(goodPick);
					availableList.Add(goodPick);
					return (true, false); // Removed
				}

				return (false, false);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ToggleGoodBonus failed: {ex.Message}");
				return (false, false);
			}
		}

		// ========================================
		// DIFFICULTY MANAGEMENT
		// ========================================

		/// <summary>
		/// Get MetaConditionsService from MetaController.Instance.MetaServices.
		/// </summary>
		private static object GetMetaConditionsService() {
			EnsureTypes();
			return GameReflection.GetMetaService(_msMetaConditionsServiceProperty);
		}

		/// <summary>
		/// Get the WorldMapService from WorldController.
		/// </summary>
		private static object GetWorldMapService() {
			EnsureTypes();

			try {
				var wc = WorldMapReflection.GetWorldController();
				if (wc == null) return null;

				var worldServices = wc.GetType().GetProperty("WorldServices",
					BindingFlags.Public | BindingFlags.Instance)?.GetValue(wc);
				if (worldServices == null) return null;

				var wmsProperty = worldServices.GetType().GetProperty("WorldMapService",
					BindingFlags.Public | BindingFlags.Instance);
				return wmsProperty?.GetValue(worldServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Find the active EmbarkDifficultyPicker in the scene.
		/// Uses cached reference if available and still valid.
		/// </summary>
		private static object FindEmbarkDifficultyPicker() {
			// Return cached picker if available and still valid (Unity null check)
			// Unity objects become null when destroyed, but == null check handles this
			if (_cachedDifficultyPicker != null) {
				// Verify the cached object is still valid (not destroyed)
				var unityObj = _cachedDifficultyPicker as UnityEngine.Object;
				if (unityObj != null && unityObj != null) // Double check: C# null and Unity null
					return _cachedDifficultyPicker;
				else
					_cachedDifficultyPicker = null; // Clear stale reference
			}

			// Fall back to finding it (expensive)
			return FindEmbarkDifficultyPickerInternal();
		}

		/// <summary>
		/// Internal method that actually performs FindObjectOfType.
		/// </summary>
		private static object FindEmbarkDifficultyPickerInternal() {
			EnsureTypes();

			if (_embarkDifficultyPickerType == null) return null;

			try {
				// Use Unity's FindObjectOfType to find the active picker
				var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new Type[] { typeof(Type) },
					null);
				return findMethod?.Invoke(null, new object[] { _embarkDifficultyPickerType });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] FindEmbarkDifficultyPicker failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get all difficulties from Settings.
		/// </summary>
		public static List<object> GetAllDifficulties() {
			EnsureTypes();
			var settings = GameReflection.GetSettings();
			var array = ReflectionHelper.GetField(_settingsDifficultiesField, settings) as Array;
			if (array == null) return new List<object>();
			return array.Cast<object>().ToList();
		}

		/// <summary>
		/// Get the max unlocked difficulty.
		/// </summary>
		public static object GetMaxUnlockedDifficulty() {
			EnsureTypes();
			var metaConditionsService = GetMetaConditionsService();
			return ReflectionHelper.Invoke(_mcsGetMaxUnlockedDifficultyMethod, metaConditionsService);
		}

		/// <summary>
		/// Get the minimum difficulty for a field position.
		/// </summary>
		public static object GetMinDifficultyFor(Vector3Int fieldPos) {
			EnsureTypes();
			var worldMapService = GetWorldMapService();
			return ReflectionHelper.Invoke(_wmsGetMinDifficultyForMethod, worldMapService, fieldPos);
		}

		/// <summary>
		/// Get available difficulties for the current field (between min and max unlocked).
		/// </summary>
		public static List<object> GetAvailableDifficulties(Vector3Int fieldPos) {
			EnsureTypes();

			var allDifficulties = GetAllDifficulties();
			var minDifficulty = GetMinDifficultyFor(fieldPos);
			var maxUnlocked = GetMaxUnlockedDifficulty();

			if (allDifficulties.Count == 0) return new List<object>();

			int minIndex = GetDifficultyIndex(minDifficulty);
			int maxIndex = GetDifficultyIndex(maxUnlocked);

			// Filter to pickable difficulties within range
			var available = new List<object>();
			foreach (var diff in allDifficulties) {
				int index = GetDifficultyIndex(diff);

				// Check if canBePicked
				var canBePickedField = diff.GetType().GetField("canBePicked",
					BindingFlags.Public | BindingFlags.Instance);
				bool canBePicked = ReflectionHelper.GetBool(canBePickedField, diff);

				if (!canBePicked) continue;

				// Must be at least min difficulty and at most max unlocked (or one above)
				if (index >= minIndex && index <= maxIndex + 1) {
					available.Add(diff);
				}
			}

			// Sort by index
			available.Sort((a, b) => GetDifficultyIndex(a).CompareTo(GetDifficultyIndex(b)));

			return available;
		}

		/// <summary>
		/// Get the currently selected difficulty from the EmbarkDifficultyPicker.
		/// </summary>
		public static object GetCurrentDifficulty() {
			EnsureTypes();
			var picker = FindEmbarkDifficultyPicker();
			return ReflectionHelper.Invoke(_edpGetPickedDifficultyMethod, picker);
		}

		/// <summary>
		/// Set the difficulty via EmbarkDifficultyPicker (triggers game callbacks).
		/// </summary>
		public static bool SetDifficulty(object difficultyModel) {
			EnsureTypes();
			var picker = FindEmbarkDifficultyPicker();
			if (picker == null || _edpSetDifficultyMethod == null) {
				Debug.LogWarning("[ATSAccessibility] SetDifficulty: Picker not found");
				return false;
			}

			bool success = ReflectionHelper.InvokeVoid(_edpSetDifficultyMethod, picker, difficultyModel);
			if (success) Debug.Log($"[ATSAccessibility] Difficulty set successfully");
			return success;
		}

		/// <summary>
		/// Get the display name from a DifficultyModel.
		/// </summary>
		public static string GetDifficultyDisplayName(object difficulty) {
			return ReflectionHelper.InvokeString(_dmGetDisplayNameMethod, difficulty) ?? "Unknown";
		}

		/// <summary>
		/// Get the index from a DifficultyModel.
		/// </summary>
		public static int GetDifficultyIndex(object difficulty) {
			if (difficulty == null) return -1;
			var indexField = difficulty.GetType().GetField("index",
				BindingFlags.Public | BindingFlags.Instance);
			if (indexField == null) return -1;
			return ReflectionHelper.GetInt(indexField, difficulty);
		}

		/// <summary>
		/// Get difficulty modifiers as list of descriptions.
		/// Includes seasonal effects counts, effect severity range, and full ascension modifier details.
		/// </summary>
		/// <param name="difficulty">The difficulty model</param>
		/// <param name="fieldPos">Optional field position for looking up effect severity labels</param>
		public static List<string> GetDifficultyModifiers(object difficulty, Vector3Int? fieldPos = null) {
			var result = new List<string>();
			if (difficulty == null) return result;

			try {
				// 1. Add seasonal mystery counts
				var (positive, negative) = GetDifficultySeasonalEffects(difficulty);
				if (negative > 0)
					result.Add($"{negative} negative seasonal myster{(negative > 1 ? "ies" : "y")}");
				if (positive > 0)
					result.Add($"{positive} positive seasonal myster{(positive > 1 ? "ies" : "y")}");

				// 2. Add effect severity range with labels (if available)
				if (fieldPos.HasValue) {
					string severityLabel = GetDifficultyEffectCostRangeLabel(difficulty, fieldPos.Value);
					if (!string.IsNullOrEmpty(severityLabel)) {
						result.Add(severityLabel);
					}
				}

				// 3. Add ascension modifiers with full descriptions
				var modifiersField = difficulty.GetType().GetField("modifiers",
					BindingFlags.Public | BindingFlags.Instance);
				if (modifiersField != null) {
					var modifiers = modifiersField.GetValue(difficulty) as Array;
					if (modifiers != null) {
						foreach (var modifier in modifiers) {
							if (modifier == null) continue;

							// Check if isShown
							bool isShown = _ammIsShownField == null || ReflectionHelper.GetBool(_ammIsShownField, modifier);
							if (!isShown) continue;

							// Try to get effect's DisplayName and Description for full details
							string modifierText = null;

							var effect = ReflectionHelper.GetField(_ammEffectField, modifier);
							if (effect != null) {
								// Get DisplayName property
								var displayNameProp = effect.GetType().GetProperty("DisplayName",
									BindingFlags.Public | BindingFlags.Instance);
								string displayName = displayNameProp?.GetValue(effect)?.ToString();

								// Get Description property
								var descProp = effect.GetType().GetProperty("Description",
									BindingFlags.Public | BindingFlags.Instance);
								string description = descProp?.GetValue(effect)?.ToString();

								if (!string.IsNullOrEmpty(displayName)) {
									if (!string.IsNullOrEmpty(description)) {
										modifierText = $"{displayName}: {description}";
									} else {
										modifierText = displayName;
									}
								}
							}

							// Fall back to shortDesc if effect info not available
							if (string.IsNullOrEmpty(modifierText)) {
								modifierText = ReflectionHelper.GetLocaString(_ammShortDescField, modifier);
							}

							if (!string.IsNullOrEmpty(modifierText)) {
								result.Add(modifierText);
							}
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetDifficultyModifiers failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get the preparation points penalty for a difficulty.
		/// </summary>
		public static int GetDifficultyPreparationPenalty(object difficulty) {
			return ReflectionHelper.GetInt(_dmPreparationPointsPenaltyField, difficulty);
		}

		/// <summary>
		/// Get the rewards multiplier for a difficulty.
		/// </summary>
		public static float GetDifficultyRewardsMultiplier(object difficulty) {
			return ReflectionHelper.GetFloat(_dmRewardsMultiplierField, difficulty);
		}

		/// <summary>
		/// Check if a difficulty is unlocked.
		/// </summary>
		public static bool IsDifficultyUnlocked(object difficulty) {
			if (difficulty == null) return false;

			var maxUnlocked = GetMaxUnlockedDifficulty();
			if (maxUnlocked == null) return false;

			int diffIndex = GetDifficultyIndex(difficulty);
			int maxIndex = GetDifficultyIndex(maxUnlocked);

			return diffIndex <= maxIndex;
		}

		/// <summary>
		/// Get the seal fragments required to win for a specific difficulty.
		/// </summary>
		public static int GetDifficultySealFragments(object difficulty) {
			if (difficulty == null) return 0;
			var field = difficulty.GetType().GetField("sealFramentsForWin",
				BindingFlags.Public | BindingFlags.Instance);
			return ReflectionHelper.GetInt(field, difficulty);
		}

		/// <summary>
		/// Get seasonal effects counts (positive and negative) for a difficulty.
		/// </summary>
		public static (int positive, int negative) GetDifficultySeasonalEffects(object difficulty) {
			if (difficulty == null) return (0, 0);
			int positive = ReflectionHelper.GetInt(_dmPositiveEffectsField, difficulty);
			int negative = ReflectionHelper.GetInt(_dmNegativeEffectsField, difficulty);
			return (positive, negative);
		}

		/// <summary>
		/// Get effect cost range (min and max) for a difficulty.
		/// </summary>
		public static (int min, int max) GetDifficultyEffectCostRange(object difficulty) {
			if (difficulty == null) return (0, 0);
			int min = ReflectionHelper.GetInt(_dmMinEffectCostField, difficulty);
			int max = ReflectionHelper.GetInt(_dmMaxEffectCostField, difficulty);
			return (min, max);
		}

		/// <summary>
		/// Build a mapping of effect difficulty cost to label text from a biome's seasons config.
		/// </summary>
		private static Dictionary<int, string> BuildEffectCostLabelMap(object biome) {
			var map = new Dictionary<int, string>();
			if (biome == null) return map;

			try {
				// Get biome.seasons (SeasonsConfig)
				var seasonsField = biome.GetType().GetField("seasons",
					BindingFlags.Public | BindingFlags.Instance);
				var seasons = seasonsField?.GetValue(biome);
				if (seasons == null) return map;

				// Get simpleEffects array
				var simpleEffectsField = seasons.GetType().GetField("simpleEffects",
					BindingFlags.Public | BindingFlags.Instance);
				var simpleEffects = simpleEffectsField?.GetValue(seasons) as Array;

				// Get conditionalEffects array
				var conditionalEffectsField = seasons.GetType().GetField("conditionalEffects",
					BindingFlags.Public | BindingFlags.Instance);
				var conditionalEffects = conditionalEffectsField?.GetValue(seasons) as Array;

				// Process simple effects
				if (simpleEffects != null) {
					foreach (var effect in simpleEffects) {
						if (effect == null) continue;
						AddCostLabelToMap(effect, map);
					}
				}

				// Process conditional effects
				if (conditionalEffects != null) {
					foreach (var effect in conditionalEffects) {
						if (effect == null) continue;
						AddCostLabelToMap(effect, map);
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] BuildEffectCostLabelMap failed: {ex.Message}");
			}

			return map;
		}

		/// <summary>
		/// Helper to extract difficultyCost and costLabel from a seasonal effect and add to map.
		/// </summary>
		private static void AddCostLabelToMap(object effect, Dictionary<int, string> map) {
			try {
				var costField = effect.GetType().GetField("difficultyCost",
					BindingFlags.Public | BindingFlags.Instance);
				var labelField = effect.GetType().GetField("costLabel",
					BindingFlags.Public | BindingFlags.Instance);

				if (costField == null || labelField == null) return;

				int cost = ReflectionHelper.GetInt(costField, effect);
				var label = ReflectionHelper.GetField(labelField, effect);

				if (label != null && !map.ContainsKey(cost)) {
					// Get displayName.Text from LabelModel
					var displayNameField = label.GetType().GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					var text = ReflectionHelper.GetLocaString(displayNameField, label);

					if (!string.IsNullOrEmpty(text)) {
						map[cost] = text;
					}
				}
			} catch {
				// Ignore individual effect errors
			}
		}

		/// <summary>
		/// Get the effect cost range as a label string (e.g., "Mild to Severe") for a difficulty and field.
		/// Returns null if labels cannot be determined.
		/// </summary>
		public static string GetDifficultyEffectCostRangeLabel(object difficulty, Vector3Int fieldPos) {
			if (difficulty == null) return null;

			try {
				var (minCost, maxCost) = GetDifficultyEffectCostRange(difficulty);

				// Get the field via WorldMapService
				var wms = WorldMapReflection.GetWorldMapService();
				if (wms == null) return null;

				var getFieldMethod = wms.GetType().GetMethod("GetField",
					new Type[] { typeof(Vector3Int) });

				var field = ReflectionHelper.Invoke(getFieldMethod, wms, fieldPos);
				if (field == null) return null;

				var biomeProperty = field.GetType().GetProperty("Biome",
					BindingFlags.Public | BindingFlags.Instance);
				var biome = biomeProperty?.GetValue(field);
				if (biome == null) return null;

				// Build the cost-to-label mapping
				var labelMap = BuildEffectCostLabelMap(biome);
				if (labelMap.Count == 0) return null;

				// Find the label for min cost (find closest if exact not found)
				string minLabel = FindLabelForCost(labelMap, minCost);
				string maxLabel = FindLabelForCost(labelMap, maxCost);

				if (string.IsNullOrEmpty(minLabel) || string.IsNullOrEmpty(maxLabel))
					return null;

				if (minLabel == maxLabel)
					return $"Mystery severity: {minLabel}";

				return $"Mystery severity: {minLabel} to {maxLabel}";
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetDifficultyEffectCostRangeLabel failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Find the label for a given cost, using closest match if exact not found.
		/// </summary>
		private static string FindLabelForCost(Dictionary<int, string> labelMap, int cost) {
			if (labelMap.TryGetValue(cost, out string label))
				return label;

			// Find closest cost
			int closestCost = labelMap.Keys.OrderBy(k => Math.Abs(k - cost)).FirstOrDefault();
			return labelMap.TryGetValue(closestCost, out label) ? label : null;
		}

		/// <summary>
		/// Get meta currency rewards for a field position with a specific difficulty.
		/// Returns list of "amount currencyName" strings.
		/// </summary>
		public static List<string> GetMetaCurrenciesForDifficulty(Vector3Int fieldPos, object difficulty) {
			EnsureTypes();

			try {
				// Get MetaController.Instance.MetaServices.MetaEconomyService
				var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
				if (metaController == null) return new List<string>();

				var metaServices = ReflectionHelper.GetProp(GameReflection.McMetaServicesProperty, metaController);
				if (metaServices == null) return new List<string>();

				// Get MetaEconomyService
				var mesProp = metaServices.GetType().GetProperty("MetaEconomyService",
					BindingFlags.Public | BindingFlags.Instance);
				var metaEconomyService = mesProp?.GetValue(metaServices);
				if (metaEconomyService == null) return new List<string>();

				// Get GetCurrenciesFor(Vector3Int cubicPos, DifficultyModel difficulty)
				var getCurrencies = metaEconomyService.GetType().GetMethod("GetCurrenciesFor",
					new Type[] { typeof(Vector3Int), difficulty.GetType() });
				if (getCurrencies == null) return new List<string>();

				var currencies = ReflectionHelper.Invoke(getCurrencies, metaEconomyService, fieldPos, difficulty) as IList;
				if (currencies == null || currencies.Count == 0) return new List<string>();

				var settings = GameReflection.GetSettings();
				if (settings == null) return new List<string>();

				var getMetaCurrency = settings.GetType().GetMethod("GetMetaCurrency",
					new Type[] { typeof(string) });
				if (getMetaCurrency == null) return new List<string>();

				var result = new List<string>();
				foreach (var currency in currencies) {
					// MetaCurrency has name (string) and amount (int) fields
					var nameField = currency.GetType().GetField("name",
						BindingFlags.Public | BindingFlags.Instance);
					var amountField = currency.GetType().GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);

					var name = ReflectionHelper.GetString(nameField, currency);
					var amount = ReflectionHelper.GetInt(amountField, currency);

					if (!string.IsNullOrEmpty(name) && amount > 0) {
						// Get display name from MetaCurrencyModel
						var model = ReflectionHelper.Invoke(getMetaCurrency, settings, name);
						if (model != null) {
							var displayNameProp = model.GetType().GetProperty("DisplayName",
								BindingFlags.Public | BindingFlags.Instance);
							var displayName = displayNameProp?.GetValue(model) as string ?? name;
							result.Add($"{amount} {displayName}");
						}
					}
				}

				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMetaCurrenciesForDifficulty failed: {ex.Message}");
				return new List<string>();
			}
		}

		// ========================================
		// EVENT SUBSCRIPTIONS
		// ========================================

		/// <summary>
		/// Subscribe to OnFieldPreviewShown event.
		/// Returns IDisposable subscription.
		/// </summary>
		public static IDisposable SubscribeToFieldPreviewShown(Action<object> callback) {
			EnsureTypes();

			var wbb = WorldMapReflection.GetWorldBlackboardService();
			var observable = ReflectionHelper.GetProp(_wbbOnFieldPreviewShownProperty, wbb);
			if (observable == null) return null;

			return GameReflection.SubscribeToObservable(observable, callback);
		}

		/// <summary>
		/// Subscribe to OnFieldPreviewClosed event.
		/// Returns IDisposable subscription.
		/// </summary>
		public static IDisposable SubscribeToFieldPreviewClosed(Action<object> callback) {
			EnsureTypes();

			var wbb = WorldMapReflection.GetWorldBlackboardService();
			var observable = ReflectionHelper.GetProp(_wbbOnFieldPreviewClosedProperty, wbb);
			if (observable == null) return null;

			return GameReflection.SubscribeToObservable(observable, callback);
		}

		// ========================================
		// EMBARK TRIGGER
		// ========================================

		/// <summary>
		/// Trigger the embark action by invoking the BuildingsPickScreen's confirm flow.
		/// Returns true if successfully triggered.
		/// </summary>
		public static bool TriggerEmbark() {
			EnsureTypes();

			try {
				var gameAssembly = GameReflection.GameAssembly;
				if (gameAssembly == null) {
					Debug.LogError("[ATSAccessibility] TriggerEmbark: Game assembly not found");
					return false;
				}

				// Find BuildingsPickScreen
				var pickScreenType = gameAssembly.GetType("Eremite.View.Menu.Pick.BuildingsPickScreen");
				if (pickScreenType == null) {
					Debug.LogError("[ATSAccessibility] TriggerEmbark: BuildingsPickScreen type not found");
					return false;
				}

				var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
					BindingFlags.Public | BindingFlags.Static,
					null,
					new Type[] { typeof(Type) },
					null);
				var pickScreen = findMethod?.Invoke(null, new object[] { pickScreenType });

				if (pickScreen == null) {
					Debug.LogError("[ATSAccessibility] TriggerEmbark: BuildingsPickScreen not found in scene");
					return false;
				}

				// Call TryToConfirm method
				var tryToConfirmMethod = pickScreenType.GetMethod("TryToConfirm",
					BindingFlags.NonPublic | BindingFlags.Instance);

				if (tryToConfirmMethod != null) {
					ReflectionHelper.InvokeVoid(tryToConfirmMethod, pickScreen);
					Debug.Log("[ATSAccessibility] TriggerEmbark: TryToConfirm invoked successfully");
					return true;
				}

				// Fallback: try to find and click the confirm button directly
				var confirmButtonField = pickScreenType.GetField("confirmButton",
					BindingFlags.NonPublic | BindingFlags.Instance);
				var confirmButton = ReflectionHelper.GetField(confirmButtonField, pickScreen);

				if (confirmButton != null) {
					// ButtonAdv has OnClick observable, but we can also try onClick.Invoke()
					var onClickMethod = confirmButton.GetType().GetMethod("OnPointerClick",
						BindingFlags.Public | BindingFlags.Instance);

					if (ReflectionHelper.InvokeVoid(onClickMethod, confirmButton, (object)null)) {
						Debug.Log("[ATSAccessibility] TriggerEmbark: Button click invoked");
						return true;
					}
				}

				Debug.LogError("[ATSAccessibility] TriggerEmbark: Could not find method to trigger embark");
				return false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] TriggerEmbark failed: {ex.Message}");
				return false;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(EmbarkReflection), "EmbarkReflection");
		}
	}
}
