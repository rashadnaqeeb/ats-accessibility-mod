using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to world map game internals.
	/// Extracted from GameReflection for maintainability.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (WorldController, services, etc.) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class WorldMapReflection {
		// ========================================
		// WORLD MAP REFLECTION
		// ========================================
		// Path: WorldController.Instance.WorldServices.WorldMapService
		// Path: MetaController.Instance.MetaServices.WorldStateService

		private static Type _worldControllerType = null;
		private static PropertyInfo _wcInstanceProperty = null;       // static Instance
		private static PropertyInfo _wcWorldServicesProperty = null;  // WorldServices
		private static PropertyInfo _wcCameraControllerProperty = null;  // CameraController
		private static FieldInfo _wccTargetField = null;  // WorldCameraController.target
		private static PropertyInfo _wsWorldMapServiceProperty = null;   // WorldMapService
		private static PropertyInfo _wsWorldBlackboardServiceProperty = null;  // WorldBlackboardService
		private static PropertyInfo _msWorldStateServiceProperty = null;  // WorldStateService (from IMetaServices)
		private static bool _worldMapTypesCached = false;

		// WorldMapService methods
		private static MethodInfo _wmsGetFieldMethod = null;
		private static MethodInfo _wmsIsRevealedMethod = null;
		private static MethodInfo _wmsCanBePickedMethod = null;
		private static MethodInfo _wmsInBoundsMethod = null;
		private static MethodInfo _wmsIsCapitalMethod = null;
		private static MethodInfo _wmsIsCityMethod = null;
		private static MethodInfo _wmsGetDistanceToStartTownMethod = null;
		private static MethodInfo _wmsFindLastTownMethod = null;
		private static PropertyInfo _wmsFieldsMapProperty = null;

		// WorldStateService methods
		private static MethodInfo _wssHasModifierMethod = null;
		private static MethodInfo _wssHasEventMethod = null;
		private static MethodInfo _wssHasSealMethod = null;
		private static MethodInfo _wssGetModifierModelMethod = null;
		private static bool _wssGetModifierModelMethodLookedUp = false;  // Tracks if concrete lookup was attempted
		private static MethodInfo _wssGetEventModelMethod = null;
		private static MethodInfo _wssGetSealModelMethod = null;
		private static MethodInfo _wssGetDisplayNameForMethod = null;
		private static PropertyInfo _wssFieldsProperty = null;

		// WorldCalendarService
		private static PropertyInfo _wsWorldCalendarServiceProperty = null;
		private static MethodInfo _wcsIsStormAboutToComeMethod = null;
		private static MethodInfo _wcsHasPlayedFinalGameMethod = null;

		// WorldSealsService (from WorldServices)
		private static PropertyInfo _wsWorldSealsServiceProperty = null;
		private static MethodInfo _wsealsCanAffordSealMethod = null;

		// WorldStateService additional
		private static MethodInfo _wssGetNearbySealMethod = null;
		private static PropertyInfo _wssCycleProperty = null;

		// WorldBlackboardService
		private static PropertyInfo _wbbOnFieldClickedProperty = null;

		// ========================================
		// META STATE SERVICE
		// ========================================
		// Path: MetaController.Instance.MetaServices.MetaStateService

		private static bool _metaStateCached = false;

		// MetaStateService access
		private static PropertyInfo _msMetaStateServiceProperty = null;

		// MetaStateService properties
		private static PropertyInfo _mssEconomyProperty = null;      // MetaStateService.Economy
		private static PropertyInfo _mssLevelProperty = null;         // MetaStateService.Level
		private static PropertyInfo _mssCapitalProperty = null;       // MetaStateService.Capital
		private static PropertyInfo _mssStateProperty = null;         // MetaStateService.State

		// MetaEconomyState fields
		private static FieldInfo _economyCurrentCycleExpField = null;
		private static FieldInfo _economyMetaCurrenciesField = null;

		// LevelState fields
		private static FieldInfo _levelLevelField = null;
		private static FieldInfo _levelExpField = null;
		private static FieldInfo _levelTargetExpField = null;

		// CapitalState
		private static FieldInfo _capitalCurrentCycleUpgradesField = null;

		// MetaState.isIronman
		private static FieldInfo _stateIsIronmanField = null;

		// Settings.GetCapitalUpgrade / Settings.GetMetaCurrency
		private static MethodInfo _settingsGetCapitalUpgradeMethod = null;
		private static MethodInfo _settingsGetMetaCurrencyMethod = null;

		// CapitalUpgradeModel
		private static FieldInfo _upgradeDisplayNameField = null;
		private static FieldInfo _upgradeIronmanDisplayNameField = null;

		// WorldBlackboardService.OnCycleEndPhase
		private static PropertyInfo _wbbOnCycleEndPhaseProperty = null;

		// CycleEndPhase.AnimationRequested enum value
		private static object _animationRequestedValue = null;

		// ========================================
		// CYCLE STATE
		// ========================================

		private static PropertyInfo _wssSealsProperty = null;

		// CycleState fields
		private static FieldInfo _cycleYearField = null;
		private static FieldInfo _cycleYearsInCycleField = null;
		private static FieldInfo _cycleGamesPlayedField = null;
		private static FieldInfo _cycleGamesWonField = null;
		private static FieldInfo _cycleSealFragmentsField = null;

		// ========================================
		// SEALS (uses _wsWorldSealsServiceProperty from WORLD MAP REFLECTION section)
		// ========================================

		private static MethodInfo _sealsWasAnyCompletedMethod = null;
		private static MethodInfo _sealsGetHighestWonMethod = null;

		// WorldField properties (accessed frequently)
		private static PropertyInfo _worldFieldBiomeProperty = null;
		private static PropertyInfo _worldFieldTransformProperty = null;

		// BiomeModel fields (accessed frequently)
		private static FieldInfo _biomeDisplayNameField = null;
		private static FieldInfo _biomeDescriptionField = null;
		private static FieldInfo _biomeSoilGradeField = null;
		private static FieldInfo _biomeEffectsField = null;
		private static FieldInfo _biomeWantedGoodsField = null;
		private static MethodInfo _biomeGetDepositsGoodsMethod = null;
		private static MethodInfo _biomeGetTreesGoodsMethod = null;

		// EffectModel properties (accessed frequently)
		private static PropertyInfo _effectDisplayNameProperty = null;
		private static PropertyInfo _effectDescriptionProperty = null;
		private static PropertyInfo _effectIsPositiveProperty = null;

		// GoodModel fields
		private static FieldInfo _goodDisplayNameField = null;

		private static void EnsureWorldMapTypes() {
			if (_worldMapTypesCached) return;
			GameReflection.EnsureMetaControllerTypesInternal(); // Ensures MetaController types are cached

			var gameAssembly = GameReflection.GameAssembly;
			if (gameAssembly == null) {
				_worldMapTypesCached = true;
				return;
			}

			try {
				// Cache WorldController type
				_worldControllerType = gameAssembly.GetType("Eremite.Controller.WorldController");
				if (_worldControllerType != null) {
					_wcInstanceProperty = _worldControllerType.GetProperty("Instance",
						BindingFlags.Public | BindingFlags.Static);
					_wcWorldServicesProperty = _worldControllerType.GetProperty("WorldServices",
						BindingFlags.Public | BindingFlags.Instance);
					_wcCameraControllerProperty = _worldControllerType.GetProperty("CameraController",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached WorldController type info");
				}

				// Cache IWorldServices interface properties
				var worldServicesType = gameAssembly.GetType("Eremite.Services.World.IWorldServices");
				if (worldServicesType != null) {
					_wsWorldMapServiceProperty = worldServicesType.GetProperty("WorldMapService",
						BindingFlags.Public | BindingFlags.Instance);
					_wsWorldBlackboardServiceProperty = worldServicesType.GetProperty("WorldBlackboardService",
						BindingFlags.Public | BindingFlags.Instance);
					_wsWorldCalendarServiceProperty = worldServicesType.GetProperty("WorldCalendarService",
						BindingFlags.Public | BindingFlags.Instance);
					_wsWorldSealsServiceProperty = worldServicesType.GetProperty("WorldSealsService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache IWorldCalendarService methods
				var worldCalendarServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldCalendarService");
				if (worldCalendarServiceType != null) {
					_wcsIsStormAboutToComeMethod = worldCalendarServiceType.GetMethod("IsStormAboutToCome");
					_wcsHasPlayedFinalGameMethod = worldCalendarServiceType.GetMethod("HasPlayedFinalGame");
					Debug.Log("[ATSAccessibility] Cached IWorldCalendarService type info");
				}

				// Cache IWorldSealsService methods
				var worldSealsServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldSealsService");
				if (worldSealsServiceType != null) {
					_wsealsCanAffordSealMethod = worldSealsServiceType.GetMethod("CanAffordSeal");
					Debug.Log("[ATSAccessibility] Cached IWorldSealsService type info");
				}

				// Cache IWorldMapService methods
				var worldMapServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldMapService");
				if (worldMapServiceType != null) {
					_wmsGetFieldMethod = worldMapServiceType.GetMethod("GetField",
						new Type[] { typeof(Vector3Int) });
					_wmsIsRevealedMethod = worldMapServiceType.GetMethod("IsRevealed",
						new Type[] { typeof(Vector3Int), typeof(int) });
					_wmsCanBePickedMethod = worldMapServiceType.GetMethod("CanBePicked",
						new Type[] { typeof(Vector3Int) });
					_wmsInBoundsMethod = worldMapServiceType.GetMethod("InBounds",
						new Type[] { typeof(Vector3Int) });
					_wmsIsCapitalMethod = worldMapServiceType.GetMethod("IsCapital",
						new Type[] { typeof(Vector3Int) });
					_wmsIsCityMethod = worldMapServiceType.GetMethod("IsCity",
						new Type[] { typeof(Vector3Int) });
					_wmsGetDistanceToStartTownMethod = worldMapServiceType.GetMethod("GetDistanceToStartTown",
						new Type[] { typeof(Vector3Int) });
					_wmsFindLastTownMethod = worldMapServiceType.GetMethod("FindLastTown");
					_wmsFieldsMapProperty = worldMapServiceType.GetProperty("FieldsMap",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached IWorldMapService type info");
				}

				// Cache IWorldStateService methods (from IMetaServices)
				var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
				if (metaServicesType != null) {
					_msWorldStateServiceProperty = metaServicesType.GetProperty("WorldStateService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				var worldStateServiceType = gameAssembly.GetType("Eremite.Services.IWorldStateService");
				if (worldStateServiceType != null) {
					_wssHasModifierMethod = worldStateServiceType.GetMethod("HasModifier",
						new Type[] { typeof(Vector3Int) });
					_wssHasEventMethod = worldStateServiceType.GetMethod("HasEvent",
						new Type[] { typeof(Vector3Int) });
					_wssHasSealMethod = worldStateServiceType.GetMethod("HasSeal",
						new Type[] { typeof(Vector3Int) });
					_wssGetModifierModelMethod = worldStateServiceType.GetMethod("GetModifierModel",
						new Type[] { typeof(Vector3Int) });
					_wssGetEventModelMethod = worldStateServiceType.GetMethod("GetEventModel",
						new Type[] { typeof(Vector3Int) });
					_wssGetSealModelMethod = worldStateServiceType.GetMethod("GetSealModel",
						new Type[] { typeof(Vector3Int) });
					_wssGetDisplayNameForMethod = worldStateServiceType.GetMethod("GetDisplayNameFor",
						new Type[] { typeof(Vector3Int) });
					_wssFieldsProperty = worldStateServiceType.GetProperty("Fields",
						BindingFlags.Public | BindingFlags.Instance);
					_wssGetNearbySealMethod = worldStateServiceType.GetMethod("GetNearbySeal",
						new Type[] { typeof(Vector3Int) });
					_wssCycleProperty = worldStateServiceType.GetProperty("Cycle",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached IWorldStateService type info");
				}

				// Cache WorldBlackboardService OnFieldClicked property
				var worldBlackboardServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldBlackboardService");
				if (worldBlackboardServiceType != null) {
					_wbbOnFieldClickedProperty = worldBlackboardServiceType.GetProperty("OnFieldClicked",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache WorldField properties
				var worldFieldType = gameAssembly.GetType("Eremite.WorldMap.WorldField");
				if (worldFieldType != null) {
					_worldFieldBiomeProperty = worldFieldType.GetProperty("Biome",
						BindingFlags.Public | BindingFlags.Instance);
					_worldFieldTransformProperty = worldFieldType.GetProperty("transform",
						BindingFlags.Public | BindingFlags.Instance);
					Debug.Log("[ATSAccessibility] Cached WorldField properties");
				}

				// Cache BiomeModel fields/methods
				var biomeModelType = gameAssembly.GetType("Eremite.WorldMap.BiomeModel");
				if (biomeModelType != null) {
					_biomeDisplayNameField = biomeModelType.GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeDescriptionField = biomeModelType.GetField("description",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeSoilGradeField = biomeModelType.GetField("soilGrade",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeEffectsField = biomeModelType.GetField("effects",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeWantedGoodsField = biomeModelType.GetField("wantedGoods",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeGetDepositsGoodsMethod = biomeModelType.GetMethod("GetDepositsGoods",
						BindingFlags.Public | BindingFlags.Instance);
					_biomeGetTreesGoodsMethod = biomeModelType.GetMethod("GetTreesGoods",
						BindingFlags.Public | BindingFlags.Instance);
					Debug.Log("[ATSAccessibility] Cached BiomeModel fields/methods");
				}

				// Cache EffectModel properties
				var effectModelType = gameAssembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
					_effectIsPositiveProperty = effectModelType.GetProperty("IsPositive",
						BindingFlags.Public | BindingFlags.Instance);
					Debug.Log("[ATSAccessibility] Cached EffectModel properties");
				}

				// Cache GoodModel fields
				var goodModelType = gameAssembly.GetType("Eremite.Model.GoodModel");
				if (goodModelType != null) {
					_goodDisplayNameField = goodModelType.GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					Debug.Log("[ATSAccessibility] Cached GoodModel fields");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] World map type caching failed: {ex.Message}");
			}

			_worldMapTypesCached = true;
		}

		private static void EnsureMetaStateTypes() {
			if (_metaStateCached) return;
			GameReflection.EnsureMetaControllerTypesInternal();

			var gameAssembly = GameReflection.GameAssembly;
			if (gameAssembly == null) {
				_metaStateCached = true;
				return;
			}

			try {
				// MetaStateService access from IMetaServices
				var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
				if (metaServicesType != null) {
					_msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
						GameReflection.PublicInstance);
				}

				// MetaStateService properties from IMetaStateService
				var metaStateServiceType = gameAssembly.GetType("Eremite.Services.IMetaStateService");
				if (metaStateServiceType != null) {
					_mssEconomyProperty = metaStateServiceType.GetProperty("Economy",
						GameReflection.PublicInstance);
					_mssLevelProperty = metaStateServiceType.GetProperty("Level",
						GameReflection.PublicInstance);
					_mssCapitalProperty = metaStateServiceType.GetProperty("Capital",
						GameReflection.PublicInstance);
					_mssStateProperty = metaStateServiceType.GetProperty("State",
						GameReflection.PublicInstance);
				}

				// MetaEconomyState fields
				var economyStateType = gameAssembly.GetType("Eremite.Model.State.MetaEconomyState");
				if (economyStateType != null) {
					_economyCurrentCycleExpField = economyStateType.GetField("currentCycleExp",
						GameReflection.PublicInstance);
					_economyMetaCurrenciesField = economyStateType.GetField("metaCurrencies",
						GameReflection.PublicInstance);
				}

				// LevelState fields
				var levelStateType = gameAssembly.GetType("Eremite.Model.State.LevelState");
				if (levelStateType != null) {
					_levelLevelField = levelStateType.GetField("level",
						GameReflection.PublicInstance);
					_levelExpField = levelStateType.GetField("exp",
						GameReflection.PublicInstance);
					_levelTargetExpField = levelStateType.GetField("targetExp",
						GameReflection.PublicInstance);
				}

				// CapitalState
				var capitalStateType = gameAssembly.GetType("Eremite.WorldMap.CapitalState");
				if (capitalStateType != null) {
					_capitalCurrentCycleUpgradesField = capitalStateType.GetField("currentCycleUnlockedUpgrades",
						GameReflection.PublicInstance);
				}

				// MetaState.isIronman
				var metaStateType = gameAssembly.GetType("Eremite.Model.State.MetaState");
				if (metaStateType != null) {
					_stateIsIronmanField = metaStateType.GetField("isIronman",
						GameReflection.PublicInstance);
				}

				// Settings.GetCapitalUpgrade / Settings.GetMetaCurrency
				var settingsType = gameAssembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetCapitalUpgradeMethod = settingsType.GetMethod("GetCapitalUpgrade",
						GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
					_settingsGetMetaCurrencyMethod = settingsType.GetMethod("GetMetaCurrency",
						GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
				}

				// CapitalUpgradeModel
				var upgradeModelType = gameAssembly.GetType("Eremite.WorldMap.CapitalUpgradeModel");
				if (upgradeModelType != null) {
					_upgradeDisplayNameField = upgradeModelType.GetField("displayName",
						GameReflection.PublicInstance);
					_upgradeIronmanDisplayNameField = upgradeModelType.GetField("ironmanDisplayName",
						GameReflection.PublicInstance);
				}

				// WorldBlackboardService.OnCycleEndPhase
				var wbbType = gameAssembly.GetType("Eremite.Services.World.IWorldBlackboardService");
				if (wbbType != null) {
					_wbbOnCycleEndPhaseProperty = wbbType.GetProperty("OnCycleEndPhase",
						GameReflection.PublicInstance);
				}

				// CycleEndPhase.AnimationRequested enum value
				var cycleEndPhaseType = gameAssembly.GetType("Eremite.CycleEndPhase");
				if (cycleEndPhaseType != null) {
					try {
						_animationRequestedValue = Enum.Parse(cycleEndPhaseType, "AnimationRequested");
					} catch (ArgumentException) {
						Debug.LogWarning("[ATSAccessibility] WorldMapReflection: AnimationRequested enum value not found");
						_animationRequestedValue = null;
					}
				}

				// CycleState fields
				var cycleStateType = gameAssembly.GetType("Eremite.WorldMap.CycleState");
				if (cycleStateType != null) {
					_cycleYearField = cycleStateType.GetField("year",
						GameReflection.PublicInstance);
					_cycleYearsInCycleField = cycleStateType.GetField("yearsInCycle",
						GameReflection.PublicInstance);
					_cycleGamesPlayedField = cycleStateType.GetField("gamesPlayedInCycle",
						GameReflection.PublicInstance);
					_cycleGamesWonField = cycleStateType.GetField("gamesWonInCycle",
						GameReflection.PublicInstance);
					_cycleSealFragmentsField = cycleStateType.GetField("sealFragments",
						GameReflection.PublicInstance);
				}

				// WorldStateService.Seals
				var worldStateServiceType = gameAssembly.GetType("Eremite.Services.IWorldStateService");
				if (worldStateServiceType != null) {
					_wssSealsProperty = worldStateServiceType.GetProperty("Seals",
						GameReflection.PublicInstance);
				}

				// Seal service methods (WorldSealsService property cached in EnsureWorldMapTypes)
				var worldSealsServiceType = gameAssembly.GetType("Eremite.Services.World.IWorldSealsService");
				if (worldSealsServiceType != null) {
					_sealsWasAnyCompletedMethod = worldSealsServiceType.GetMethod("WasAnySealEverCompleted",
						GameReflection.PublicInstance);
					_sealsGetHighestWonMethod = worldSealsServiceType.GetMethod("GetHighestWonSeal",
						GameReflection.PublicInstance);
				}

				Debug.Log("[ATSAccessibility] Cached MetaState/CycleState/Seals types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] MetaState type caching failed: {ex.Message}");
			}

			_metaStateCached = true;
		}

		/// <summary>
		/// Check if we are on the world map (WorldController.Instance != null).
		/// </summary>
		public static bool IsWorldMapActive() {
			EnsureWorldMapTypes();

			if (_wcInstanceProperty == null) return false;

			try {
				var instance = _wcInstanceProperty.GetValue(null);
				return instance != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get WorldController.Instance.
		/// </summary>
		public static object GetWorldController() {
			EnsureWorldMapTypes();

			if (_wcInstanceProperty == null) return null;

			try {
				return _wcInstanceProperty.GetValue(null);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get WorldServices from WorldController.
		/// </summary>
		public static object GetWorldServices() {
			var wc = GetWorldController();
			if (wc == null || _wcWorldServicesProperty == null) return null;

			try {
				return _wcWorldServicesProperty.GetValue(wc);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get WorldMapService from WorldServices.
		/// </summary>
		public static object GetWorldMapService() {
			EnsureWorldMapTypes();
			var ws = GetWorldServices();
			if (ws == null || _wsWorldMapServiceProperty == null) return null;

			try {
				return _wsWorldMapServiceProperty.GetValue(ws);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get WorldStateService from MetaController.Instance.MetaServices.
		/// </summary>
		public static object GetWorldStateService() {
			EnsureWorldMapTypes();

			try {
				var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
				if (metaController == null) return null;

				var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
				if (metaServices == null) return null;

				return _msWorldStateServiceProperty?.GetValue(metaServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get all world map field positions from WorldStateService.Fields.
		/// </summary>
		public static IEnumerable<Vector3Int> GetWorldMapPositions() {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null || _wssFieldsProperty == null) return Enumerable.Empty<Vector3Int>();

			try {
				var fields = _wssFieldsProperty.GetValue(wss);
				if (fields == null) return Enumerable.Empty<Vector3Int>();

				// Fields is Dictionary<Vector3Int, WorldFieldState>
				var keys = ReflectionHelper.IterateKeys(fields) as IEnumerable<Vector3Int>;
				return keys ?? Enumerable.Empty<Vector3Int>();
			} catch {
				return Enumerable.Empty<Vector3Int>();
			}
		}

		/// <summary>
		/// Get WorldBlackboardService from WorldServices.
		/// </summary>
		public static object GetWorldBlackboardService() {
			EnsureWorldMapTypes();
			var ws = GetWorldServices();
			if (ws == null || _wsWorldBlackboardServiceProperty == null) return null;

			try {
				return _wsWorldBlackboardServiceProperty.GetValue(ws);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get WorldCalendarService from WorldServices.
		/// </summary>
		public static object GetWorldCalendarService() {
			EnsureWorldMapTypes();
			var ws = GetWorldServices();
			if (ws == null || _wsWorldCalendarServiceProperty == null) return null;

			try {
				return _wsWorldCalendarServiceProperty.GetValue(ws);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get WorldSealsService from WorldServices.
		/// </summary>
		public static object GetWorldSealsService() {
			EnsureWorldMapTypes();
			var ws = GetWorldServices();
			if (ws == null || _wsWorldSealsServiceProperty == null) return null;

			try {
				return _wsWorldSealsServiceProperty.GetValue(ws);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if the blightstorm is about to come (last year of cycle).
		/// </summary>
		public static bool IsStormAboutToCome() {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wcsIsStormAboutToComeMethod, GetWorldCalendarService());
		}

		/// <summary>
		/// Check if the final (seal) game has already been played this cycle.
		/// </summary>
		public static bool HasPlayedFinalGame() {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wcsHasPlayedFinalGameMethod, GetWorldCalendarService());
		}

		/// <summary>
		/// Get seal fragment status for a tile's nearby seal.
		/// Returns (currentFragments, requiredFragments), or (-1, -1) if no nearby seal.
		/// </summary>
		public static (int current, int required) GetSealFragmentStatus(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null) return (-1, -1);

			try {
				// Get nearby seal model
				if (_wssGetNearbySealMethod == null) return (-1, -1);
				var seal = _wssGetNearbySealMethod.Invoke(wss, new object[] { cubicPos });
				if (seal == null) return (-1, -1);

				// Get minFragmentsToStart
				var minFragField = seal.GetType().GetField("minFragmentsToStart",
					BindingFlags.Public | BindingFlags.Instance);
				var required = (int)(minFragField?.GetValue(seal) ?? -1);
				if (required < 0) return (-1, -1);

				// Get current fragments from CycleState
				if (_wssCycleProperty == null) return (-1, required);
				var cycle = _wssCycleProperty.GetValue(wss);
				if (cycle == null) return (-1, required);

				var sealFragmentsField = cycle.GetType().GetField("sealFragments",
					BindingFlags.Public | BindingFlags.Instance);
				var current = (int)(sealFragmentsField?.GetValue(cycle) ?? -1);

				return (current, required);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetSealFragmentStatus failed: {ex.Message}");
				return (-1, -1);
			}
		}

		/// <summary>
		/// Get the position and name of the last town (embark starting point).
		/// Returns the most recently finished non-capital city, or the capital if none.
		/// </summary>
		public static (Vector3Int position, string name) GetLastTownInfo() {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsFindLastTownMethod == null)
				return (Vector3Int.zero, null);

			try {
				var cityState = _wmsFindLastTownMethod.Invoke(wms, null);
				if (cityState == null)
					return (Vector3Int.zero, null);

				var fieldInfo = cityState.GetType().GetField("field",
					BindingFlags.Public | BindingFlags.Instance);
				var position = (Vector3Int)(fieldInfo?.GetValue(cityState) ?? Vector3Int.zero);

				// Use GetDisplayNameFor for the localized town name
				var name = WorldMapGetCityName(position);

				return (position, name);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetLastTownInfo failed: {ex.Message}");
				return (Vector3Int.zero, null);
			}
		}

		/// <summary>
		/// Get the embark range from a given starting position.
		/// This is the max hex distance you can embark from that position.
		/// The game's InRange check uses strict less-than (path.Count - 1 < range),
		/// and the path includes the start tile, so effective max distance = raw range - 1.
		/// </summary>
		public static int GetEmbarkRange(Vector3Int from) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetDistanceToStartTownMethod == null) return -1;

			try {
				var raw = (int)_wmsGetDistanceToStartTownMethod.Invoke(wms, new object[] { from });
				return raw - 1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Check if a world map position is within bounds.
		/// </summary>
		public static bool WorldMapInBounds(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wmsInBoundsMethod, GetWorldMapService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position is revealed (not in fog).
		/// </summary>
		public static bool WorldMapIsRevealed(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wmsIsRevealedMethod, GetWorldMapService(), new object[] { cubicPos, 0 });
		}

		/// <summary>
		/// Check if a world map position is the capital (0,0,0).
		/// </summary>
		public static bool WorldMapIsCapital(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wmsIsCapitalMethod, GetWorldMapService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position has a city.
		/// </summary>
		public static bool WorldMapIsCity(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wmsIsCityMethod, GetWorldMapService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position can be picked for embark.
		/// </summary>
		public static bool WorldMapCanBePicked(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wmsCanBePickedMethod, GetWorldMapService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position has a modifier.
		/// </summary>
		public static bool WorldMapHasModifier(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wssHasModifierMethod, GetWorldStateService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position has an event.
		/// </summary>
		public static bool WorldMapHasEvent(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wssHasEventMethod, GetWorldStateService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Check if a world map position has a seal.
		/// </summary>
		public static bool WorldMapHasSeal(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			return GameReflection.TryInvokeBoolInternal(_wssHasSealMethod, GetWorldStateService(), new object[] { cubicPos });
		}

		/// <summary>
		/// Get the distance from a position to the capital.
		/// </summary>
		public static int WorldMapGetDistanceToCapital(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetDistanceToStartTownMethod == null) return -1;

			try {
				return (int)_wmsGetDistanceToStartTownMethod.Invoke(wms, new object[] { cubicPos });
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Get the biome display name for a world map position.
		/// </summary>
		public static string WorldMapGetBiomeName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetFieldMethod == null) return null;

			try {
				// Get WorldField at position
				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return null;

				// Get Biome using cached property
				var biome = _worldFieldBiomeProperty?.GetValue(field);
				if (biome == null) return null;

				// Get displayName using cached field
				var displayName = _biomeDisplayNameField?.GetValue(biome);
				return GameReflection.GetLocaText(displayName);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the city name for a world map position.
		/// </summary>
		public static string WorldMapGetCityName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null || _wssGetDisplayNameForMethod == null) return null;

			try {
				return _wssGetDisplayNameForMethod.Invoke(wss, new object[] { cubicPos }) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the modifier name for a world map position.
		/// </summary>
		public static string WorldMapGetModifierName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null) return null;

			// Try interface method first, fallback to concrete class (cached lookup)
			if (_wssGetModifierModelMethod == null && !_wssGetModifierModelMethodLookedUp) {
				_wssGetModifierModelMethodLookedUp = true;
				var concreteMethod = wss.GetType().GetMethod("GetModifierModel",
					new Type[] { typeof(Vector3Int) });
				if (concreteMethod != null) {
					_wssGetModifierModelMethod = concreteMethod;
					Debug.Log("[ATSAccessibility] Cached GetModifierModel from concrete type");
				}
			}

			if (_wssGetModifierModelMethod == null)
				return null;

			try {
				var model = _wssGetModifierModelMethod.Invoke(wss, new object[] { cubicPos });
				if (model == null) return null;

				var displayNameProp = model.GetType().GetProperty("DisplayName",
					BindingFlags.Public | BindingFlags.Instance);
				return displayNameProp?.GetValue(model) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the event name for a world map position.
		/// </summary>
		public static string WorldMapGetEventName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null || _wssGetEventModelMethod == null) return null;

			try {
				var model = _wssGetEventModelMethod.Invoke(wss, new object[] { cubicPos });
				if (model == null) return null;

				// Get displayName from model
				var displayNameField = model.GetType().GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				var displayName = displayNameField?.GetValue(model);
				return GameReflection.GetLocaText(displayName);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the seal name for a world map position.
		/// </summary>
		public static string WorldMapGetSealName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null || _wssGetSealModelMethod == null) return null;

			try {
				var model = _wssGetSealModelMethod.Invoke(wss, new object[] { cubicPos });
				if (model == null) return null;

				// Get displayName from model
				var displayNameField = model.GetType().GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				var displayName = displayNameField?.GetValue(model);
				return GameReflection.GetLocaText(displayName);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Trigger a field click on the world map.
		/// This opens the embark screen for the selected tile.
		/// </summary>
		public static void WorldMapTriggerFieldClick(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wbb = GetWorldBlackboardService();
			if (wbb == null || _wbbOnFieldClickedProperty == null) return;

			try {
				var subject = _wbbOnFieldClickedProperty.GetValue(wbb);
				if (subject == null) return;

				// Call OnNext on the Subject<Vector3Int>
				var onNextMethod = subject.GetType().GetMethod("OnNext",
					new Type[] { typeof(Vector3Int) });
				onNextMethod?.Invoke(subject, new object[] { cubicPos });

				Debug.Log($"[ATSAccessibility] Triggered field click at {cubicPos}");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapTriggerFieldClick failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Move the world map camera to a position.
		/// </summary>
		public static void SetWorldCameraPosition(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wc = GetWorldController();
			if (wc == null || _wcCameraControllerProperty == null) return;

			try {
				var cameraController = _wcCameraControllerProperty.GetValue(wc);
				if (cameraController == null) return;

				// Get the transform
				var transformProperty = cameraController.GetType().GetProperty("transform",
					BindingFlags.Public | BindingFlags.Instance);
				var transform = transformProperty?.GetValue(cameraController) as Transform;
				if (transform == null) return;

				// Convert cubic to world coordinates
				var worldPos = CubicToWorld(cubicPos);

				// Smoothly move to position
				transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetWorldCameraPosition failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Set the camera target to a world map field for smooth following.
		/// The game's WorldCameraController has a target field but doesn't use it.
		/// Our Harmony patch adds target-following behavior to UpdateMovement().
		/// </summary>
		public static void SetWorldCameraTarget(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wc = GetWorldController();
			if (wc == null || _wcCameraControllerProperty == null) return;

			try {
				var cameraController = _wcCameraControllerProperty.GetValue(wc);
				if (cameraController == null) return;

				// Cache target field
				if (_wccTargetField == null) {
					_wccTargetField = cameraController.GetType().GetField("target",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get WorldField at position to get its Transform
				var wms = GetWorldMapService();
				if (wms == null || _wmsGetFieldMethod == null) return;

				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return;

				var transformProp = field.GetType().GetProperty("transform",
					BindingFlags.Public | BindingFlags.Instance);
				var fieldTransform = transformProp?.GetValue(field) as Transform;

				_wccTargetField?.SetValue(cameraController, fieldTransform);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetWorldCameraTarget failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Convert cubic hex coordinates to world position.
		/// Matches the game's WorldMapUtils.CubicToWorld implementation.
		/// </summary>
		public static Vector3 CubicToWorld(Vector3Int cubic) {
			// HexSize = 0.62f
			const float HexSize = 0.62f;

			// CubicToAxial: q = cubic.x, r = cubic.z
			int q = cubic.x;
			int r = cubic.z;

			// AxialToWorld
			float x = HexSize * (1.5f * q);
			float y = HexSize * (Mathf.Sqrt(3f) / 2f * q + Mathf.Sqrt(3f) * r);

			return new Vector3(x, y, 0f);
		}

		// ========================================
		// WORLD MAP TOOLTIP DATA METHODS
		// ========================================

		/// <summary>
		/// Get the minimum difficulty display name for a world map position.
		/// </summary>
		public static string WorldMapGetMinDifficultyName(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null) return null;

			try {
				// Get GetMinDifficultyFor method
				var method = wms.GetType().GetMethod("GetMinDifficultyFor",
					new Type[] { typeof(Vector3Int) });
				if (method == null) return null;

				var difficulty = method.Invoke(wms, new object[] { cubicPos });
				if (difficulty == null) return null;

				// Get GetDisplayName method
				var getDisplayName = difficulty.GetType().GetMethod("GetDisplayName",
					BindingFlags.Public | BindingFlags.Instance);
				return getDisplayName?.Invoke(difficulty, null) as string;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetMinDifficultyName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the preparation points penalty from the min difficulty for a position.
		/// Returns a negative number (the penalty to subtract from base points).
		/// </summary>
		public static int WorldMapGetDifficultyPreparationPenalty(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null) return 0;

			try {
				var method = wms.GetType().GetMethod("GetMinDifficultyFor",
					new Type[] { typeof(Vector3Int) });
				if (method == null) return 0;

				var difficulty = method.Invoke(wms, new object[] { cubicPos });
				if (difficulty == null) return 0;

				var penaltyField = difficulty.GetType().GetField("preparationPointsPenalty",
					BindingFlags.Public | BindingFlags.Instance);
				if (penaltyField == null) return 0;

				var penalty = penaltyField.GetValue(difficulty);
				return penalty != null ? (int)penalty : 0;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetDifficultyPreparationPenalty failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Get the seal fragments required to win for the last picked difficulty at a position.
		/// </summary>
		public static int WorldMapGetSealFragmentsForWin(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null) return -1;

			try {
				// Get the min difficulty for this field
				var getMinDiff = wms.GetType().GetMethod("GetMinDifficultyFor",
					new Type[] { typeof(Vector3Int) });
				if (getMinDiff == null) return -1;

				var difficulty = getMinDiff.Invoke(wms, new object[] { cubicPos });
				if (difficulty == null) return -1;

				// Get sealFramentsForWin field
				var field = difficulty.GetType().GetField("sealFramentsForWin",
					BindingFlags.Public | BindingFlags.Instance);
				if (field == null) return -1;

				return (int)field.GetValue(difficulty);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetSealFragmentsForWin failed: {ex.Message}");
				return -1;
			}
		}

		/// <summary>
		/// Internal helper to get field effects with all data.
		/// Returns list of (name, description, isPositive) tuples, sorted with negative effects first.
		/// </summary>
		private static List<(string name, string description, bool isPositive)> GetFieldEffectsInternal(Vector3Int cubicPos) {
			var wms = GetWorldMapService();
			var wss = GetWorldStateService();
			if (wms == null) return null;

			// Get WorldField
			var field = _wmsGetFieldMethod?.Invoke(wms, new object[] { cubicPos });
			if (field == null) return null;

			// Get Biome.effects
			var biomeProperty = field.GetType().GetProperty("Biome",
				BindingFlags.Public | BindingFlags.Instance);
			var biome = biomeProperty?.GetValue(field);
			if (biome == null) return null;

			var effectsField = biome.GetType().GetField("effects",
				BindingFlags.Public | BindingFlags.Instance);
			var biomeEffects = effectsField?.GetValue(biome) as System.Collections.IEnumerable;

			var effectsList = new List<(string name, string description, bool isPositive)>();

			// Add biome effects
			if (biomeEffects != null) {
				foreach (var effect in biomeEffects) {
					if (effect == null) continue;

					var displayNameProp = effect.GetType().GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					var descriptionProp = effect.GetType().GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
					var isPositiveProp = effect.GetType().GetProperty("IsPositive",
						BindingFlags.Public | BindingFlags.Instance);

					var name = displayNameProp?.GetValue(effect) as string;
					var description = descriptionProp?.GetValue(effect) as string ?? "";
					var isPositive = (bool)(isPositiveProp?.GetValue(effect) ?? true);

					if (!string.IsNullOrEmpty(name))
						effectsList.Add((name, description, isPositive));
				}
			}

			// Get modifier effects from GetModifiersInfluencing
			if (wss != null) {
				var getModifiers = wss.GetType().GetMethod("GetModifiersInfluencing",
					new Type[] { typeof(Vector3Int) });
				if (getModifiers != null) {
					var modifierNames = getModifiers.Invoke(wss, new object[] { cubicPos }) as List<string>;
					if (modifierNames != null) {
						var settings = GameReflection.GetSettings();
						if (settings != null) {
							var getModifier = settings.GetType().GetMethod("GetModifier",
								new Type[] { typeof(string) });
							if (getModifier != null) {
								foreach (var modName in modifierNames) {
									var modifier = getModifier.Invoke(settings, new object[] { modName });
									if (modifier != null) {
										var effectField = modifier.GetType().GetField("effect",
											BindingFlags.Public | BindingFlags.Instance);
										var effect = effectField?.GetValue(modifier);
										if (effect != null) {
											var displayNameProp = effect.GetType().GetProperty("DisplayName",
												BindingFlags.Public | BindingFlags.Instance);
											var descriptionProp = effect.GetType().GetProperty("Description",
												BindingFlags.Public | BindingFlags.Instance);
											var isPositiveProp = effect.GetType().GetProperty("IsPositive",
												BindingFlags.Public | BindingFlags.Instance);

											var name = displayNameProp?.GetValue(effect) as string;
											var description = descriptionProp?.GetValue(effect) as string ?? "";
											var isPositive = (bool)(isPositiveProp?.GetValue(effect) ?? true);

											if (!string.IsNullOrEmpty(name))
												effectsList.Add((name, description, isPositive));
										}
									}
								}
							}
						}
					}
				}
			}

			// Sort: negative effects first, then positive
			effectsList.Sort((a, b) => a.isPositive.CompareTo(b.isPositive));

			return effectsList;
		}

		/// <summary>
		/// Get the field effects (biome effects + modifier effects) for a world map position.
		/// Returns effect names, sorted with negative effects first.
		/// </summary>
		public static string[] WorldMapGetFieldEffects(Vector3Int cubicPos) {
			EnsureWorldMapTypes();

			try {
				var effects = GetFieldEffectsInternal(cubicPos);
				if (effects == null) return Array.Empty<string>();

				return effects.ConvertAll(e => e.name).ToArray();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetFieldEffects failed: {ex.Message}");
				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Get meta currency rewards for a world map position.
		/// Returns array of "amount currencyName" strings.
		/// </summary>
		public static string[] WorldMapGetMetaCurrencies(Vector3Int cubicPos) {
			EnsureWorldMapTypes();

			try {
				// Get MetaController.Instance.MetaServices.MetaEconomyService
				var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
				if (metaController == null) return Array.Empty<string>();

				var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
				if (metaServices == null) return Array.Empty<string>();

				// Get MetaEconomyService
				var mesProp = metaServices.GetType().GetProperty("MetaEconomyService",
					BindingFlags.Public | BindingFlags.Instance);
				var metaEconomyService = mesProp?.GetValue(metaServices);
				if (metaEconomyService == null) return Array.Empty<string>();

				// Get min difficulty for this field
				var wms = GetWorldMapService();
				if (wms == null) return Array.Empty<string>();

				var getMinDiff = wms.GetType().GetMethod("GetMinDifficultyFor",
					new Type[] { typeof(Vector3Int) });
				var difficulty = getMinDiff?.Invoke(wms, new object[] { cubicPos });
				if (difficulty == null) return Array.Empty<string>();

				// Get GetCurrenciesFor(Vector3Int cubicPos, DifficultyModel difficulty)
				var getCurrencies = metaEconomyService.GetType().GetMethod("GetCurrenciesFor",
					new Type[] { typeof(Vector3Int), difficulty.GetType() });
				if (getCurrencies == null) return Array.Empty<string>();

				var currencies = getCurrencies.Invoke(metaEconomyService, new object[] { cubicPos, difficulty }) as System.Collections.IList;
				if (currencies == null || currencies.Count == 0) return Array.Empty<string>();

				var settings = GameReflection.GetSettings();
				if (settings == null) return Array.Empty<string>();

				var getMetaCurrency = settings.GetType().GetMethod("GetMetaCurrency",
					new Type[] { typeof(string) });
				if (getMetaCurrency == null) return Array.Empty<string>();

				var result = new List<string>();
				foreach (var currency in currencies) {
					if (currency == null) continue;

					// MetaCurrency has name (string) and amount (int) fields
					var nameField = currency.GetType().GetField("name",
						BindingFlags.Public | BindingFlags.Instance);
					var amountField = currency.GetType().GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);

					var name = nameField?.GetValue(currency) as string;
					var amount = (int)(amountField?.GetValue(currency) ?? 0);

					if (!string.IsNullOrEmpty(name) && amount > 0) {
						// Get display name from MetaCurrencyModel
						var model = getMetaCurrency.Invoke(settings, new object[] { name });
						if (model != null) {
							var displayNameProp = model.GetType().GetProperty("DisplayName",
								BindingFlags.Public | BindingFlags.Instance);
							var displayName = displayNameProp?.GetValue(model) as string ?? name;
							result.Add($"{amount} {displayName}");
						}
					}
				}

				return result.ToArray();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetMetaCurrencies failed: {ex.Message}");
				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Get seal information for a world map position.
		/// Returns (sealName, difficultyName, minFragments, rewardsPercent, bonusYears, isCompleted).
		/// </summary>
		public static (string sealName, string difficultyName, int minFragments, int rewardsPercent, int bonusYears, bool isCompleted) WorldMapGetSealInfo(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null) return (null, null, 0, 0, 0, false);

			try {
				// Get seal model via GetNearbySeal
				var getNearbySeal = wss.GetType().GetMethod("GetNearbySeal",
					new Type[] { typeof(Vector3Int) });
				var seal = getNearbySeal?.Invoke(wss, new object[] { cubicPos });
				if (seal == null) return (null, null, 0, 0, 0, false);

				// Get displayName
				var displayNameField = seal.GetType().GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				var displayName = displayNameField?.GetValue(seal);
				var sealName = GameReflection.GetLocaText(displayName) ?? "";

				// Get difficulty
				var diffField = seal.GetType().GetField("difficulty",
					BindingFlags.Public | BindingFlags.Instance);
				var difficulty = diffField?.GetValue(seal);
				var difficultyName = "";
				if (difficulty != null) {
					var getDisplayName = difficulty.GetType().GetMethod("GetDisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					difficultyName = getDisplayName?.Invoke(difficulty, null) as string ?? "";
				}

				// Get minFragmentsToStart
				var minFragField = seal.GetType().GetField("minFragmentsToStart",
					BindingFlags.Public | BindingFlags.Instance);
				var minFragments = (int)(minFragField?.GetValue(seal) ?? 0);

				// Get rewardsMultiplier
				var rewardsMulField = seal.GetType().GetField("rewardsMultiplier",
					BindingFlags.Public | BindingFlags.Instance);
				var rewardsMultiplier = (float)(rewardsMulField?.GetValue(seal) ?? 0f);
				var rewardsPercent = (int)(rewardsMultiplier * 100);

				// Get bonusYearsPerCycle
				var bonusYearsField = seal.GetType().GetField("bonusYearsPerCycle",
					BindingFlags.Public | BindingFlags.Instance);
				var bonusYears = (int)(bonusYearsField?.GetValue(seal) ?? 0);

				// Check if completed via WorldSealsService
				bool isCompleted = false;
				var worldServices = GetWorldServices();
				if (worldServices != null) {
					var sealsServiceProp = worldServices.GetType().GetProperty("WorldSealsService",
						BindingFlags.Public | BindingFlags.Instance);
					var sealsService = sealsServiceProp?.GetValue(worldServices);
					if (sealsService != null) {
						var wasAnyCompleted = sealsService.GetType().GetMethod("WasAnySealEverCompleted",
							BindingFlags.Public | BindingFlags.Instance);
						if (wasAnyCompleted != null && (bool)wasAnyCompleted.Invoke(sealsService, null)) {
							var getHighestWon = sealsService.GetType().GetMethod("GetHighestWonSeal",
								BindingFlags.Public | BindingFlags.Instance);
							var highestWon = getHighestWon?.Invoke(sealsService, null);
							if (highestWon != null && difficulty != null) {
								var sealDiffField = highestWon.GetType().GetField("difficulty",
									BindingFlags.Public | BindingFlags.Instance);
								var highestDiff = sealDiffField?.GetValue(highestWon);
								if (highestDiff != null) {
									var sealIndexField = difficulty.GetType().GetField("index",
										BindingFlags.Public | BindingFlags.Instance);
									var highestIndexField = highestDiff.GetType().GetField("index",
										BindingFlags.Public | BindingFlags.Instance);
									var sealIndex = (int)(sealIndexField?.GetValue(difficulty) ?? 0);
									var highestIndex = (int)(highestIndexField?.GetValue(highestDiff) ?? -1);
									isCompleted = sealIndex <= highestIndex;
								}
							}
						}
					}
				}

				return (sealName, difficultyName, minFragments, rewardsPercent, bonusYears, isCompleted);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetSealInfo failed: {ex.Message}");
				return (null, null, 0, 0, 0, false);
			}
		}

		/// <summary>
		/// Get modifier effect information for a world map position.
		/// Returns (effectName, labelName, description, isPositive).
		/// </summary>
		public static (string effectName, string labelName, string description, bool isPositive) WorldMapGetModifierInfo(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wss = GetWorldStateService();
			if (wss == null)
				return (null, null, null, false);

			try {
				// Get modifier state
				var getModifier = wss.GetType().GetMethod("GetModifier",
					new Type[] { typeof(Vector3Int) });
				if (getModifier == null)
					return (null, null, null, false);

				var modifierState = getModifier.Invoke(wss, new object[] { cubicPos });
				if (modifierState == null)
					return (null, null, null, false);

				// Get model name
				var modelField = modifierState.GetType().GetField("model",
					BindingFlags.Public | BindingFlags.Instance);
				var modelName = modelField?.GetValue(modifierState) as string;
				if (string.IsNullOrEmpty(modelName))
					return (null, null, null, false);

				// Get ModifierModel from settings
				var settings = GameReflection.GetSettings();
				if (settings == null)
					return (null, null, null, false);

				var getModifierModel = settings.GetType().GetMethod("GetModifier",
					new Type[] { typeof(string) });
				if (getModifierModel == null)
					return (null, null, null, false);

				var modifier = getModifierModel.Invoke(settings, new object[] { modelName });
				if (modifier == null)
					return (null, null, null, false);

				// Get effect
				var effectField = modifier.GetType().GetField("effect",
					BindingFlags.Public | BindingFlags.Instance);
				var effect = effectField?.GetValue(modifier);
				if (effect == null)
					return (null, null, null, false);

				// Get effect DisplayName
				var displayNameProp = effect.GetType().GetProperty("DisplayName",
					BindingFlags.Public | BindingFlags.Instance);
				var effectName = displayNameProp?.GetValue(effect) as string ?? "";

				// Get label.displayName
				var labelField = effect.GetType().GetField("label",
					BindingFlags.Public | BindingFlags.Instance);
				var label = labelField?.GetValue(effect);
				var labelName = "";
				if (label != null) {
					var labelDisplayNameField = label.GetType().GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					var labelLocaText = labelDisplayNameField?.GetValue(label);
					labelName = GameReflection.GetLocaText(labelLocaText) ?? "";
				}

				// Get Description
				var descProp = effect.GetType().GetProperty("Description",
					BindingFlags.Public | BindingFlags.Instance);
				var description = descProp?.GetValue(effect) as string ?? "";

				// Get IsPositive
				var isPositiveProp = effect.GetType().GetProperty("IsPositive",
					BindingFlags.Public | BindingFlags.Instance);
				var isPositive = (bool)(isPositiveProp?.GetValue(effect) ?? false);

				return (effectName, labelName, description, isPositive);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetModifierInfo failed: {ex.Message}\n{ex.StackTrace}");
				return (null, null, null, false);
			}
		}

		/// <summary>
		/// Check if an event at a world map position can be reached.
		/// </summary>
		public static bool WorldMapCanReachEvent(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null) return false;

			try {
				var canReachEvent = wms.GetType().GetMethod("CanReachEvent",
					new Type[] { typeof(Vector3Int) });
				if (canReachEvent == null) return false;

				return (bool)canReachEvent.Invoke(wms, new object[] { cubicPos });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapCanReachEvent failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if the world map position has any path to it from explored territory.
		/// </summary>
		public static bool WorldMapHasAnyPathTo(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null) return false;

			try {
				// Get WorldField first
				var field = _wmsGetFieldMethod?.Invoke(wms, new object[] { cubicPos });
				if (field == null) return false;

				var hasAnyPathTo = wms.GetType().GetMethod("HasAnyPathTo",
					new Type[] { field.GetType() });
				if (hasAnyPathTo == null) return false;

				return (bool)hasAnyPathTo.Invoke(wms, new object[] { field });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapHasAnyPathTo failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get wanted goods for a city at a world map position.
		/// Only available if trade routes are enabled.
		/// </summary>
		public static string[] WorldMapGetWantedGoods(Vector3Int cubicPos) {
			EnsureWorldMapTypes();

			try {
				// Check if trade routes are enabled
				var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
				if (metaController == null) return Array.Empty<string>();

				var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
				if (metaServices == null) return Array.Empty<string>();

				var mpsProp = metaServices.GetType().GetProperty("MetaPerksService",
					BindingFlags.Public | BindingFlags.Instance);
				var metaPerksService = mpsProp?.GetValue(metaServices);
				if (metaPerksService == null) return Array.Empty<string>();

				var areTradeRoutes = metaPerksService.GetType().GetMethod("AreTradeRoutesEnabled",
					BindingFlags.Public | BindingFlags.Instance);
				if (areTradeRoutes == null || !(bool)areTradeRoutes.Invoke(metaPerksService, null))
					return Array.Empty<string>();

				// Get biome's wanted goods
				var wms = GetWorldMapService();
				if (wms == null) return Array.Empty<string>();

				var field = _wmsGetFieldMethod?.Invoke(wms, new object[] { cubicPos });
				if (field == null) return Array.Empty<string>();

				var biomeProperty = field.GetType().GetProperty("Biome",
					BindingFlags.Public | BindingFlags.Instance);
				var biome = biomeProperty?.GetValue(field);
				if (biome == null) return Array.Empty<string>();

				var wantedGoodsField = biome.GetType().GetField("wantedGoods",
					BindingFlags.Public | BindingFlags.Instance);
				var wantedGoods = wantedGoodsField?.GetValue(biome) as System.Array;
				if (wantedGoods == null || wantedGoods.Length == 0) return Array.Empty<string>();

				var result = new List<string>();
				foreach (var good in wantedGoods) {
					if (good == null) continue;

					var displayNameField = good.GetType().GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					var displayName = displayNameField?.GetValue(good);
					var name = GameReflection.GetLocaText(displayName);
					if (!string.IsNullOrEmpty(name))
						result.Add(name);
				}

				return result.ToArray();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetWantedGoods failed: {ex.Message}");
				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Get the biome description for a world map position.
		/// </summary>
		public static string WorldMapGetBiomeDescription(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetFieldMethod == null) return null;

			try {
				// Get WorldField at position
				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return null;

				// Get Biome using cached property
				var biome = _worldFieldBiomeProperty?.GetValue(field);
				if (biome == null) return null;

				// Get description using cached field
				var description = _biomeDescriptionField?.GetValue(biome);
				return GameReflection.GetLocaText(description);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeDescription failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the biome soil grade (fertility) for a world map position.
		/// </summary>
		public static string WorldMapGetBiomeSoilGrade(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetFieldMethod == null) return null;

			try {
				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return null;

				var biome = _worldFieldBiomeProperty?.GetValue(field);
				if (biome == null) return null;

				var soilGrade = _biomeSoilGradeField?.GetValue(biome);
				return GameReflection.GetLocaText(soilGrade);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeSoilGrade failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the resource deposit goods available in this biome.
		/// </summary>
		public static List<string> WorldMapGetBiomeDepositsGoods(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetFieldMethod == null) return null;

			try {
				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return null;

				// Use cached property
				var biome = _worldFieldBiomeProperty?.GetValue(field);
				if (biome == null) return null;

				// Use cached method
				if (_biomeGetDepositsGoodsMethod == null) return null;

				var goods = _biomeGetDepositsGoodsMethod.Invoke(biome, null) as System.Collections.IEnumerable;
				if (goods == null) return null;

				var result = new List<string>();
				foreach (var good in goods) {
					if (good == null) continue;

					// Use cached field
					var displayName = _goodDisplayNameField?.GetValue(good);
					var text = GameReflection.GetLocaText(displayName);
					if (!string.IsNullOrEmpty(text))
						result.Add(text);
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeDepositsGoods failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the tree/natural resource goods available in this biome.
		/// </summary>
		public static List<string> WorldMapGetBiomeTreesGoods(Vector3Int cubicPos) {
			EnsureWorldMapTypes();
			var wms = GetWorldMapService();
			if (wms == null || _wmsGetFieldMethod == null) return null;

			try {
				var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
				if (field == null) return null;

				// Use cached property
				var biome = _worldFieldBiomeProperty?.GetValue(field);
				if (biome == null) return null;

				// Use cached method
				if (_biomeGetTreesGoodsMethod == null) return null;

				var goods = _biomeGetTreesGoodsMethod.Invoke(biome, null) as System.Collections.IEnumerable;
				if (goods == null) return null;

				var result = new List<string>();
				foreach (var good in goods) {
					if (good == null) continue;

					// Use cached field
					var displayName = _goodDisplayNameField?.GetValue(good);
					var text = GameReflection.GetLocaText(displayName);
					if (!string.IsNullOrEmpty(text))
						result.Add(text);
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeTreesGoods failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get field effects with their descriptions for a world map position.
		/// Returns list of (name, description) tuples, sorted with negative effects first.
		/// </summary>
		public static List<(string name, string description)> WorldMapGetFieldEffectsWithDescriptions(Vector3Int cubicPos) {
			EnsureWorldMapTypes();

			try {
				var effects = GetFieldEffectsInternal(cubicPos);
				if (effects == null) return new List<(string, string)>();

				return effects.ConvertAll(e => (e.name, e.description));
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] WorldMapGetFieldEffectsWithDescriptions failed: {ex.Message}");
				return new List<(string, string)>();
			}
		}

		// ========================================
		// META STATE SERVICE API
		// ========================================

		/// <summary>
		/// Get MetaStateService from MetaController.Instance.MetaServices.
		/// </summary>
		public static object GetMetaStateService() {
			EnsureMetaStateTypes();

			try {
				var metaServices = GameReflection.GetMetaServices();
				if (metaServices == null || _msMetaStateServiceProperty == null) return null;

				return _msMetaStateServiceProperty.GetValue(metaServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get player level info.
		/// Returns (level, exp, targetExp).
		/// </summary>
		public static (int level, int exp, int targetExp) GetLevelInfo() {
			EnsureMetaStateTypes();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return (0, 0, 0);

				var levelState = _mssLevelProperty?.GetValue(metaStateService);
				if (levelState == null) return (0, 0, 0);

				var level = (int)(_levelLevelField?.GetValue(levelState) ?? 0);
				var exp = (int)(_levelExpField?.GetValue(levelState) ?? 0);
				var targetExp = (int)(_levelTargetExpField?.GetValue(levelState) ?? 0);

				return (level, exp, targetExp);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetLevelInfo failed: {ex.Message}");
				return (0, 0, 0);
			}
		}

		/// <summary>
		/// Get the experience gained in the current cycle.
		/// </summary>
		public static int GetCurrentCycleExp() {
			EnsureMetaStateTypes();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return 0;

				var economy = _mssEconomyProperty?.GetValue(metaStateService);
				if (economy == null || _economyCurrentCycleExpField == null) return 0;

				var expObj = _economyCurrentCycleExpField.GetValue(economy);
				return expObj is int exp ? exp : 0;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCurrentCycleExp failed: {ex.Message}");
				return 0;
			}
		}

		/// <summary>
		/// Get the capital upgrades unlocked during the current cycle.
		/// Returns an IEnumerable of upgrade ID strings, or null.
		/// </summary>
		public static IEnumerable GetCurrentCycleUnlockedUpgrades() {
			EnsureMetaStateTypes();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return null;

				var capital = _mssCapitalProperty?.GetValue(metaStateService);
				if (capital == null || _capitalCurrentCycleUpgradesField == null) return null;

				return _capitalCurrentCycleUpgradesField.GetValue(capital) as IEnumerable;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the display name for a capital upgrade.
		/// Uses ironman display name if isIronman is true and one exists.
		/// </summary>
		public static string GetCapitalUpgradeDisplayName(object settings, string upgradeId, bool isIronman) {
			EnsureMetaStateTypes();

			try {
				if (_settingsGetCapitalUpgradeMethod == null) return upgradeId;

				var upgrade = _settingsGetCapitalUpgradeMethod.Invoke(settings, new object[] { upgradeId });
				if (upgrade == null) return upgradeId;

				string name = null;

				// Try ironman display name first if in ironman mode
				if (isIronman && _upgradeIronmanDisplayNameField != null) {
					var ironmanLocaText = _upgradeIronmanDisplayNameField.GetValue(upgrade);
					name = GameReflection.GetLocaText(ironmanLocaText);
				}

				// Fall back to regular display name
				if (string.IsNullOrEmpty(name) && _upgradeDisplayNameField != null) {
					var locaText = _upgradeDisplayNameField.GetValue(upgrade);
					name = GameReflection.GetLocaText(locaText);
				}

				return !string.IsNullOrEmpty(name) ? name : upgradeId;
			} catch {
				return upgradeId;
			}
		}

		/// <summary>
		/// Check if the current save is in ironman mode.
		/// </summary>
		public static bool IsIronmanMode() {
			EnsureMetaStateTypes();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null || _mssStateProperty == null || _stateIsIronmanField == null)
					return false;

				var state = _mssStateProperty.GetValue(metaStateService);
				if (state == null) return false;

				var isIronmanObj = _stateIsIronmanField.GetValue(state);
				return isIronmanObj is bool b && b;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get meta currencies dictionary from economy state.
		/// Returns IDictionary (string -> int), or null.
		/// </summary>
		public static System.Collections.IDictionary GetMetaCurrencies() {
			EnsureMetaStateTypes();

			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return null;

				var economyState = _mssEconomyProperty?.GetValue(metaStateService);
				if (economyState == null || _economyMetaCurrenciesField == null) return null;

				return _economyMetaCurrenciesField.GetValue(economyState) as System.Collections.IDictionary;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the display name for a meta currency by its internal name.
		/// </summary>
		public static string GetMetaCurrencyDisplayName(string currencyName) {
			EnsureMetaStateTypes();

			try {
				if (_settingsGetMetaCurrencyMethod == null || string.IsNullOrEmpty(currencyName))
					return currencyName;

				var settings = GameReflection.GetSettings();
				if (settings == null) return currencyName;

				var model = _settingsGetMetaCurrencyMethod.Invoke(settings, new object[] { currencyName });
				if (model == null) return currencyName;

				var displayNameProp = model.GetType().GetProperty("DisplayName",
					GameReflection.PublicInstance);
				return displayNameProp?.GetValue(model) as string ?? currencyName;
			} catch {
				return currencyName;
			}
		}

		// ========================================
		// CYCLE STATE API
		// ========================================

		/// <summary>
		/// Cycle property on WorldStateService, for direct access by consumers.
		/// </summary>
		public static PropertyInfo CycleProperty {
			get {
				EnsureWorldMapTypes();
				return _wssCycleProperty;
			}
		}

		/// <summary>
		/// Get cycle/storm information.
		/// Returns (year, yearsInCycle, gamesWon, gamesPlayed, sealFragments).
		/// </summary>
		public static (int year, int yearsInCycle, int gamesWon, int gamesPlayed, int sealFragments) GetCycleInfo() {
			EnsureMetaStateTypes();

			try {
				var worldStateService = GetWorldStateService();
				if (worldStateService == null || _wssCycleProperty == null) return (0, 0, 0, 0, 0);

				var cycleState = _wssCycleProperty.GetValue(worldStateService);
				if (cycleState == null) return (0, 0, 0, 0, 0);

				var year = (int)(_cycleYearField?.GetValue(cycleState) ?? 0);
				var yearsInCycle = (int)(_cycleYearsInCycleField?.GetValue(cycleState) ?? 0);
				var gamesPlayed = (int)(_cycleGamesPlayedField?.GetValue(cycleState) ?? 0);
				var gamesWon = (int)(_cycleGamesWonField?.GetValue(cycleState) ?? 0);
				var sealFragments = (int)(_cycleSealFragmentsField?.GetValue(cycleState) ?? 0);

				return (year, yearsInCycle, gamesWon, gamesPlayed, sealFragments);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCycleInfo failed: {ex.Message}");
				return (0, 0, 0, 0, 0);
			}
		}

		// ========================================
		// SEALS API
		// ========================================

		/// <summary>
		/// WorldSealsService property on IWorldServices, for direct access by consumers.
		/// </summary>
		public static PropertyInfo WorldSealsServiceProperty {
			get {
				EnsureWorldMapTypes();
				return _wsWorldSealsServiceProperty;
			}
		}

		/// <summary>
		/// WasAnySealEverCompleted method on WorldSealsService.
		/// </summary>
		public static MethodInfo SealsWasAnyCompleted {
			get {
				EnsureMetaStateTypes();
				return _sealsWasAnyCompletedMethod;
			}
		}

		/// <summary>
		/// GetHighestWonSeal method on WorldSealsService.
		/// </summary>
		public static MethodInfo SealsGetHighestWon {
			get {
				EnsureMetaStateTypes();
				return _sealsGetHighestWonMethod;
			}
		}

		// ========================================
		// CYCLE END API
		// ========================================

		/// <summary>
		/// Trigger the cycle end animation by invoking
		/// WorldBlackboardService.OnCycleEndPhase.OnNext(CycleEndPhase.AnimationRequested).
		/// Returns true if successful.
		/// </summary>
		public static bool TriggerCycleEndAnimation() {
			EnsureMetaStateTypes();

			var wbb = GetWorldBlackboardService();
			if (wbb == null || _wbbOnCycleEndPhaseProperty == null || _animationRequestedValue == null)
				return false;

			try {
				var subject = _wbbOnCycleEndPhaseProperty.GetValue(wbb);
				if (subject == null) return false;

				var onNextMethod = subject.GetType().GetMethod("OnNext",
					GameReflection.PublicInstance);
				if (onNextMethod == null) return false;

				onNextMethod.Invoke(subject, new[] { _animationRequestedValue });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] TriggerCycleEndAnimation failed: {ex.Message}");
				return false;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(WorldMapReflection), "WorldMapReflection");
		}
	}
}
