using ATSAccessibility.Core;
using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to game internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (GameController, services, etc.) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class GameReflection {
		// ========================================
		// BINDINGFLAGS CONSTANTS (reduces typo risk)
		// ========================================
		public const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
		public const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
		public const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;

		// ========================================
		// CACHED REFLECTION METADATA (safe to cache)
		// ========================================
		private static Assembly _gameAssembly = null;
		private static bool _assemblyCached = false;

		// GameController type info
		private static Type _gameControllerType = null;
		private static PropertyInfo _gcIsGameActiveProperty = null;  // static IsGameActive

		// MainController type info
		private static Type _mainControllerType = null;
		private static PropertyInfo _mcInstanceProperty = null;      // static Instance
		private static PropertyInfo _mcAppServicesProperty = null;   // instance AppServices

		// PopupsService access (via AppServices)
		private static PropertyInfo _popupsServiceProperty = null;   // AppServices.PopupsService

		private static bool _typesInitialized = false;

		// ========================================
		// TAB SYSTEM REFLECTION (TabsPanel/TabsButton)
		// ========================================
		private static Type _tabsPanelType = null;
		private static Type _tabsButtonType = null;
		private static FieldInfo _tabsPanelButtonsField = null;      // TabsPanel.buttons (TabsButton[])
		private static FieldInfo _tabsPanelCurrentField = null;      // TabsPanel.current (TabsButton)
		private static FieldInfo _tabsButtonButtonField = null;      // TabsButton.button (Button)
		private static FieldInfo _tabsButtonContentField = null;     // TabsButton.content (GameObject)
		private static bool _tabTypesCached = false;

		/// <summary>
		/// Get a service from GameServices by its cached PropertyInfo.
		/// Replaces the duplicated 6-line pattern across reflection files.
		/// </summary>
		public static object GetService(PropertyInfo serviceProperty) {
			var gameServices = GetGameServices();
			if (gameServices == null || serviceProperty == null) return null;
			try { return serviceProperty.GetValue(gameServices); } catch { return null; }
		}

		/// <summary>
		/// Get a service from MetaServices by its cached PropertyInfo.
		/// Replaces the duplicated meta-service pattern across reflection files.
		/// </summary>
		public static object GetMetaService(PropertyInfo serviceProperty) {
			var metaServices = GetMetaServices();
			if (metaServices == null || serviceProperty == null) return null;
			try { return serviceProperty.GetValue(metaServices); } catch { return null; }
		}

		// ========================================
		// LOCATEXT HELPER
		// ========================================

		// Cache for LocaText.Text property
		private static PropertyInfo _locaTextTextProperty;

		/// <summary>
		/// Extract the Text string from a LocaText object.
		/// Handles null checks and caches the property info.
		/// </summary>
		public static string GetLocaText(object locaText) {
			if (locaText == null) return null;

			// Cache the Text property on first use
			if (_locaTextTextProperty == null) {
				_locaTextTextProperty = locaText.GetType().GetProperty("Text", PublicInstance);
			}

			try {
				return _locaTextTextProperty?.GetValue(locaText) as string;
			} catch {
				return null;
			}
		}

		// ========================================
		// INTERNAL ACCESSORS (for WorldMapReflection)
		// ========================================

		internal static Assembly GameAssembly {
			get {
				EnsureAssembly();
				return _gameAssembly;
			}
		}

		internal static bool TryInvokeBoolInternal(MethodInfo method, object instance, object[] args = null) {
			if (args == null) return ReflectionHelper.InvokeBool(method, instance);
			if (args.Length == 1) return ReflectionHelper.InvokeBool(method, instance, args[0]);
			if (args.Length == 2) return ReflectionHelper.InvokeBool(method, instance, args[0], args[1]);
			return false;
		}

		internal static void EnsureMetaControllerTypesInternal() {
			EnsureMetaControllerTypes();
		}

		internal static PropertyInfo MetaControllerInstanceProperty {
			get {
				EnsureMetaControllerTypes();
				return _metaControllerInstanceProperty;
			}
		}

		internal static PropertyInfo McMetaServicesProperty {
			get {
				EnsureMetaControllerTypes();
				return _mcMetaServicesProperty;
			}
		}

		/// <summary>
		/// Get the MetaServices instance (fresh each time).
		/// Path: MetaController.Instance.MetaServices
		/// </summary>
		public static object GetMetaServices() {
			EnsureMetaControllerTypes();

			try {
				var metaController = _metaControllerInstanceProperty?.GetValue(null);
				if (metaController == null) return null;

				return _mcMetaServicesProperty?.GetValue(metaController);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMetaServices failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureAssembly() {
			if (_assemblyCached) return;

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
				if (assembly.GetName().Name == "Assembly-CSharp") {
					_gameAssembly = assembly;
					Debug.Log("[ATSAccessibility] Found Assembly-CSharp");
					break;
				}
			}

			if (_gameAssembly == null) {
				Debug.LogWarning("[ATSAccessibility] Assembly-CSharp not found");
			}

			_assemblyCached = true;
		}

		private static void EnsureTypes() {
			if (_typesInitialized) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_typesInitialized = true;
				return;
			}

			try {
				// Cache GameController type info
				_gameControllerType = _gameAssembly.GetType("Eremite.Controller.GameController");
				if (_gameControllerType != null) {
					_gcIsGameActiveProperty = _gameControllerType.GetProperty("IsGameActive",
						BindingFlags.Public | BindingFlags.Static);

					Debug.Log("[ATSAccessibility] Cached GameController type info");
				} else {
					Debug.LogWarning("[ATSAccessibility] GameController type not found");
				}

				// Cache MainController type info
				_mainControllerType = _gameAssembly.GetType("Eremite.Controller.MainController");
				if (_mainControllerType != null) {
					_mcInstanceProperty = _mainControllerType.GetProperty("Instance",
						BindingFlags.Public | BindingFlags.Static);
					_mcAppServicesProperty = _mainControllerType.GetProperty("AppServices",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached MainController type info");
				} else {
					Debug.LogWarning("[ATSAccessibility] MainController type not found");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Type caching failed: {ex.Message}");
			}

			_typesInitialized = true;
		}

		// ========================================
		// PUBLIC API - Always returns fresh data
		// ========================================

		/// <summary>
		/// Check if game is active (in settlement with GameController initialized).
		/// This reads a static property on GameController, safe to call anytime.
		/// </summary>
		public static bool GetIsGameActive() {
			EnsureTypes();

			if (_gcIsGameActiveProperty == null) return false;

			try {
				return (bool)_gcIsGameActiveProperty.GetValue(null);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get MainController instance. This persists across scenes via DontDestroyOnLoad.
		/// Still, do not cache long-term as it could be recreated.
		/// </summary>
		public static object GetMainControllerInstance() {
			EnsureTypes();

			if (_mcInstanceProperty == null) return null;

			try {
				return _mcInstanceProperty.GetValue(null);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get AppServices from MainController.
		/// </summary>
		public static object GetAppServices() {
			var mc = GetMainControllerInstance();
			if (mc == null || _mcAppServicesProperty == null) return null;

			try {
				return _mcAppServicesProperty.GetValue(mc);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get PopupsService from AppServices.
		/// DO NOT cache - get fresh reference each time.
		/// </summary>
		public static object GetPopupsService() {
			var appServices = GetAppServices();
			if (appServices == null) return null;

			try {
				// Cache the property info (safe), but always get fresh value
				if (_popupsServiceProperty == null) {
					_popupsServiceProperty = appServices.GetType().GetProperty("PopupsService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				return _popupsServiceProperty?.GetValue(appServices);
			} catch {
				return null;
			}
		}

		// Cached field for activePopups list
		private static FieldInfo _activePopupsField = null;

		/// <summary>
		/// Get the top active popup from PopupsService (index 0 of activePopups list).
		/// Returns null if no popups are active.
		/// </summary>
		public static object GetTopActivePopup() {
			var popupsService = GetPopupsService();
			if (popupsService == null) return null;

			try {
				if (_activePopupsField == null) {
					_activePopupsField = popupsService.GetType().GetField("activePopups",
						BindingFlags.NonPublic | BindingFlags.Instance);
				}

				var activePopups = _activePopupsField?.GetValue(popupsService) as System.Collections.IList;
				if (activePopups == null || activePopups.Count == 0) return null;

				return activePopups[0];
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetTopActivePopup failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Find a type by name in the game assembly.
		/// Used for detecting game-specific components like TabsButton.
		/// </summary>
		public static Type FindTypeByName(string typeName) {
			EnsureAssembly();
			if (_gameAssembly == null) return null;

			try {
				return _gameAssembly.GetTypes()
					.FirstOrDefault(t => t.Name == typeName);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get a type by its full name (e.g., "Eremite.View.HUD.GoodSlot").
		/// More efficient than FindTypeByName when full name is known.
		/// </summary>
		public static Type GetTypeByName(string fullTypeName) {
			EnsureAssembly();
			return _gameAssembly?.GetType(fullTypeName);
		}

		/// <summary>
		/// Get the game Settings via MB.Settings static property.
		/// Contains all game model data including goods, buildings, etc.
		/// </summary>
		public static object GetSettings() {
			EnsureAssembly();
			if (_gameAssembly == null) {
				Debug.Log("[ATSAccessibility] GetSettings: _gameAssembly is null");
				return null;
			}

			try {
				var mbType = _gameAssembly.GetType("Eremite.MB");
				if (mbType == null) {
					Debug.Log("[ATSAccessibility] GetSettings: Eremite.MB type not found");
					return null;
				}

				// Settings is protected static, so we need NonPublic flag
				var settingsProperty = mbType.GetProperty("Settings",
					BindingFlags.NonPublic | BindingFlags.Static);
				if (settingsProperty == null) {
					Debug.Log("[ATSAccessibility] GetSettings: Settings property not found");
					return null;
				}

				var result = settingsProperty.GetValue(null);
				if (result == null) {
					Debug.Log("[ATSAccessibility] GetSettings: Settings value is null");
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetSettings failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// TAB SYSTEM API
		// ========================================

		/// <summary>
		/// Ensure TabsPanel and TabsButton types are cached.
		/// Call this before accessing tab-related reflection data.
		/// </summary>
		public static void EnsureTabTypes() {
			if (_tabTypesCached) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_tabTypesCached = true;
				return;
			}

			try {
				// Cache TabsPanel type and fields
				_tabsPanelType = FindTypeByName("TabsPanel");
				if (_tabsPanelType != null) {
					_tabsPanelButtonsField = _tabsPanelType.GetField("buttons",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_tabsPanelCurrentField = _tabsPanelType.GetField("current",
						BindingFlags.NonPublic | BindingFlags.Instance);

					Debug.Log($"[ATSAccessibility] Cached TabsPanel type: {_tabsPanelType.FullName}");
				}

				// Cache TabsButton type and fields
				_tabsButtonType = FindTypeByName("TabsButton");
				if (_tabsButtonType != null) {
					_tabsButtonButtonField = _tabsButtonType.GetField("button",
						BindingFlags.NonPublic | BindingFlags.Instance);
					_tabsButtonContentField = _tabsButtonType.GetField("content",
						BindingFlags.NonPublic | BindingFlags.Instance);

					Debug.Log($"[ATSAccessibility] Cached TabsButton type: {_tabsButtonType.FullName}");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Tab type caching failed: {ex.Message}");
			}

			_tabTypesCached = true;
		}

		// Public accessors for tab types
		public static Type TabsPanelType { get { EnsureTabTypes(); return _tabsPanelType; } }
		public static FieldInfo TabsPanelButtonsField { get { EnsureTabTypes(); return _tabsPanelButtonsField; } }
		public static FieldInfo TabsPanelCurrentField { get { EnsureTabTypes(); return _tabsPanelCurrentField; } }
		public static FieldInfo TabsButtonButtonField { get { EnsureTabTypes(); return _tabsButtonButtonField; } }
		public static FieldInfo TabsButtonContentField { get { EnsureTabTypes(); return _tabsButtonContentField; } }

		// ========================================
		// TOGGLEBUTTON REFLECTION (game's custom toggle wrapping a Button)
		// ========================================
		private static Type _toggleButtonType = null;
		private static MethodInfo _toggleIsOnMethod = null;
		private static bool _toggleButtonTypeCached = false;

		/// <summary>
		/// Ensure ToggleButton type and IsOn method are cached.
		/// ToggleButton is the game's custom toggle that wraps a Unity Button.
		/// </summary>
		public static void EnsureToggleButtonType() {
			if (_toggleButtonTypeCached) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_toggleButtonTypeCached = true;
				return;
			}

			try {
				_toggleButtonType = _gameAssembly.GetType("Eremite.View.ToggleButton");
				if (_toggleButtonType != null) {
					_toggleIsOnMethod = _toggleButtonType.GetMethod("IsOn",
						BindingFlags.Public | BindingFlags.Instance);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ToggleButton type caching failed: {ex.Message}");
			}

			_toggleButtonTypeCached = true;
		}

		public static Type ToggleButtonType { get { EnsureToggleButtonType(); return _toggleButtonType; } }
		public static MethodInfo ToggleIsOnMethod { get { EnsureToggleButtonType(); return _toggleIsOnMethod; } }

		// ========================================
		// META CONTROLLER REFLECTION
		// ========================================
		// Path: MetaController.Instance.MetaServices

		private static Type _metaControllerType = null;
		private static PropertyInfo _metaControllerInstanceProperty = null;  // static Instance
		private static PropertyInfo _mcMetaServicesProperty = null;          // MetaServices
		private static bool _metaControllerTypesCached = false;

		private static void EnsureMetaControllerTypes() {
			if (_metaControllerTypesCached) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_metaControllerTypesCached = true;
				return;
			}

			try {
				// Cache MetaController type
				_metaControllerType = _gameAssembly.GetType("Eremite.Controller.MetaController");
				if (_metaControllerType != null) {
					_metaControllerInstanceProperty = _metaControllerType.GetProperty("Instance",
						BindingFlags.Public | BindingFlags.Static);
					_mcMetaServicesProperty = _metaControllerType.GetProperty("MetaServices",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached MetaController type info");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] MetaController type caching failed: {ex.Message}");
			}

			_metaControllerTypesCached = true;
		}

		// ========================================
		// GAME SERVICES REFLECTION (for in-game services)
		// ========================================
		// Path: GameController.Instance.GameServices.XxxService

		private static PropertyInfo _gcInstanceProperty = null;       // static Instance
		private static PropertyInfo _gcGameServicesProperty = null;   // GameServices
		private static PropertyInfo _gsReputationRewardsProperty = null;  // ReputationRewardsService
		private static bool _gameServicesTypesCached = false;

		// Camera controller access (for GetCameraController utility)
		private static PropertyInfo _gcCameraControllerProperty = null;  // GameController.CameraController
		private static bool _cameraTypesCached = false;

		private static void EnsureGameServicesTypes() {
			if (_gameServicesTypesCached) return;
			EnsureTypes();

			if (_gameControllerType == null) {
				_gameServicesTypesCached = true;
				return;
			}

			try {
				// Cache GameController.Instance property
				_gcInstanceProperty = _gameControllerType.GetProperty("Instance",
					BindingFlags.Public | BindingFlags.Static);

				// Cache GameServices property
				_gcGameServicesProperty = _gameControllerType.GetProperty("GameServices",
					BindingFlags.Public | BindingFlags.Instance);

				// Cache ReputationRewardsService property from IGameServices interface
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsReputationRewardsProperty = gameServicesType.GetProperty("ReputationRewardsService",
						BindingFlags.Public | BindingFlags.Instance);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameServices type caching failed: {ex.Message}");
			}

			_gameServicesTypesCached = true;
		}

		/// <summary>
		/// Get the ReputationRewardsService from GameController.
		/// Only available when in a game (IsGameActive == true).
		/// </summary>
		public static object GetReputationRewardsService() {
			EnsureGameServicesTypes();

			if (!GetIsGameActive()) return null;

			try {
				// Get GameController.Instance
				var gameController = _gcInstanceProperty?.GetValue(null);
				if (gameController == null) return null;

				// Get GameServices
				var gameServices = _gcGameServicesProperty?.GetValue(gameController);
				if (gameServices == null) return null;

				// Get ReputationRewardsService
				return _gsReputationRewardsProperty?.GetValue(gameServices);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetReputationRewardsService failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// MAP NAVIGATION REFLECTION
		// ========================================
		// Path: GameController.Instance.GameServices.MapService / GladesService / VillagersService

		private static PropertyInfo _gsMapServiceProperty = null;
		private static PropertyInfo _gsGladesServiceProperty = null;
		private static PropertyInfo _gsVillagersServiceProperty = null;
		private static MethodInfo _mapGetFieldMethod = null;
		private static MethodInfo _mapGetObjectOnMethod = null;
		private static MethodInfo _gladesGetGladeMethod = null;
		private static PropertyInfo _villagersVillagersProperty = null;  // Dictionary<int, Villager>
		private static PropertyInfo _gsResourcesServiceProperty = null;
		private static PropertyInfo _gsDepositsServiceProperty = null;
		private static PropertyInfo _gsOreServiceProperty = null;
		private static PropertyInfo _gsSpringsServiceProperty = null;
		private static MethodInfo _springsRemoveFromGridMethod = null;
		private static MethodInfo _springsReturnOnGridMethod = null;
		private static PropertyInfo _gsLakesServiceProperty = null;
		private static PropertyInfo _gsBuildingsServiceProperty = null;
		private static PropertyInfo _gsConditionsServiceProperty = null;
		private static MethodInfo _conditionsIsBlightActiveMethod = null;
		private static PropertyInfo _gsBlightServiceProperty = null;  // BlightService
		private static MethodInfo _blightGetGlobalActiveCystsMethod = null;  // BlightService.GetGlobalActiveCysts()
		private static MethodInfo _blightGetPredictedPercentageCorruptionMethod = null;  // BlightService.GetPredictedPercentageCorruption()
		private static PropertyInfo _buildingsBlightsProperty = null;  // BuildingsService.BuildingsBlights
		private static MethodInfo _buildingsGetMainHearthMethod = null;  // BuildingsService.GetMainHearth()
		private static MethodInfo _buildingBlightGetActiveCystsMethod = null;  // BuildingBlight.GetActiveCysts()
		private static PropertyInfo _buildingBlightOwnerProperty = null;  // BuildingBlight.Owner
		private static MethodInfo _hearthGetCorruptionRateMethod = null;  // Hearth.GetCorruptionRate()
		private static PropertyInfo _gsGladesProperty = null;  // GladesService.Glades list
		private static PropertyInfo _mapFieldsProperty = null;  // MapService.Fields (Map<Field>)
		private static FieldInfo _mapWidthField = null;         // Fields.width
		private static FieldInfo _mapHeightField = null;        // Fields.height
		private static MethodInfo _mapInBoundsMethod = null;    // MapService.InBounds(int, int)
		private static PropertyInfo _gsBiomeServiceProperty = null;  // GameServices.BiomeService
		private static PropertyInfo _biomeCurrentBiomeProperty = null;  // BiomeService.CurrentBiome
		private static bool _mapTypesCached = false;

		private static void EnsureMapTypes() {
			if (_mapTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_mapTypesCached = true;
				return;
			}

			try {
				// Get IGameServices interface for service properties
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsMapServiceProperty = gameServicesType.GetProperty("MapService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsGladesServiceProperty = gameServicesType.GetProperty("GladesService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsVillagersServiceProperty = gameServicesType.GetProperty("VillagersService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsResourcesServiceProperty = gameServicesType.GetProperty("ResourcesService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsDepositsServiceProperty = gameServicesType.GetProperty("DepositsService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsOreServiceProperty = gameServicesType.GetProperty("OreService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsSpringsServiceProperty = gameServicesType.GetProperty("SpringsService",
						BindingFlags.Public | BindingFlags.Instance);

					// Cache SpringsService grid methods for Extractor placement
					var springsServiceType = _gameAssembly.GetType("Eremite.Services.SpringsService");
					if (springsServiceType != null) {
						_springsRemoveFromGridMethod = springsServiceType.GetMethod("RemoveSpringsFromGrid",
							BindingFlags.Public | BindingFlags.Instance);
						_springsReturnOnGridMethod = springsServiceType.GetMethod("ReturnSpringsOnGrid",
							BindingFlags.Public | BindingFlags.Instance);
					}

					_gsLakesServiceProperty = gameServicesType.GetProperty("LakesService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsBuildingsServiceProperty = gameServicesType.GetProperty("BuildingsService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsConditionsServiceProperty = gameServicesType.GetProperty("ConditionsService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get IsBlightActive method from IConditionsService
				var conditionsServiceType = _gameAssembly.GetType("Eremite.Services.IConditionsService");
				if (conditionsServiceType != null) {
					_conditionsIsBlightActiveMethod = conditionsServiceType.GetMethod("IsBlightActive",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get BlightService from IGameServices
				if (gameServicesType != null) {
					_gsBlightServiceProperty = gameServicesType.GetProperty("BlightService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get BlightService methods from IBlightService
				var blightServiceType = _gameAssembly.GetType("Eremite.Services.IBlightService");
				if (blightServiceType != null) {
					_blightGetGlobalActiveCystsMethod = blightServiceType.GetMethod("GetGlobalActiveCysts",
						BindingFlags.Public | BindingFlags.Instance);
					_blightGetPredictedPercentageCorruptionMethod = blightServiceType.GetMethod("GetPredictedPercentageCorruption",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get BuildingsBlights and GetMainHearth from IBuildingsService
				var buildingsServiceType = _gameAssembly.GetType("Eremite.Services.IBuildingsService");
				if (buildingsServiceType != null) {
					_buildingsBlightsProperty = buildingsServiceType.GetProperty("BuildingsBlights",
						BindingFlags.Public | BindingFlags.Instance);
					_buildingsGetMainHearthMethod = buildingsServiceType.GetMethod("GetMainHearth",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get BuildingBlight methods
				var buildingBlightType = _gameAssembly.GetType("Eremite.Buildings.BuildingBlight");
				if (buildingBlightType != null) {
					_buildingBlightGetActiveCystsMethod = buildingBlightType.GetMethod("GetActiveCysts",
						BindingFlags.Public | BindingFlags.Instance);
					_buildingBlightOwnerProperty = buildingBlightType.GetProperty("Owner",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get Hearth.GetCorruptionRate method
				var hearthType = _gameAssembly.GetType("Eremite.Buildings.Hearth");
				if (hearthType != null) {
					_hearthGetCorruptionRateMethod = hearthType.GetMethod("GetCorruptionRate",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get Glades property and method from IGladesService
				var gladesServiceType = _gameAssembly.GetType("Eremite.Services.IGladesService");
				if (gladesServiceType != null) {
					_gsGladesProperty = gladesServiceType.GetProperty("Glades",
						BindingFlags.Public | BindingFlags.Instance);
					_gladesGetGladeMethod = gladesServiceType.GetMethod("GetGlade",
						new Type[] { typeof(Vector2Int) });
				}

				// Get MapService methods
				var mapServiceType = _gameAssembly.GetType("Eremite.Services.IMapService");
				if (mapServiceType != null) {
					_mapGetFieldMethod = mapServiceType.GetMethod("GetField",
						new Type[] { typeof(int), typeof(int) });
					_mapGetObjectOnMethod = mapServiceType.GetMethod("GetObjectOn",
						new Type[] { typeof(int), typeof(int) });
					_mapFieldsProperty = mapServiceType.GetProperty("Fields",
						BindingFlags.Public | BindingFlags.Instance);
					_mapInBoundsMethod = mapServiceType.GetMethod("InBounds",
						new Type[] { typeof(int), typeof(int) });
				}

				// Get VillagersService.Villagers property
				var villagersServiceType = _gameAssembly.GetType("Eremite.Services.IVillagersService");
				if (villagersServiceType != null) {
					_villagersVillagersProperty = villagersServiceType.GetProperty("Villagers",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get BiomeService from IGameServices
				if (gameServicesType != null) {
					_gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get CurrentBiome from IBiomeService
				var biomeServiceType = _gameAssembly.GetType("Eremite.Services.IBiomeService");
				if (biomeServiceType != null) {
					_biomeCurrentBiomeProperty = biomeServiceType.GetProperty("CurrentBiome",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached map service types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Map type caching failed: {ex.Message}");
			}

			_mapTypesCached = true;
		}

		/// <summary>
		/// Get GameServices from GameController.Instance.
		/// Only available when in a game (IsGameActive == true).
		/// </summary>
		public static object GetGameServices() {
			EnsureGameServicesTypes();

			if (!GetIsGameActive()) return null;

			try {
				var gameController = _gcInstanceProperty?.GetValue(null);
				if (gameController == null) return null;

				return _gcGameServicesProperty?.GetValue(gameController);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get MapService from GameServices.
		/// </summary>
		public static object GetMapService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsMapServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get GladesService from GameServices.
		/// </summary>
		public static object GetGladesService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsGladesServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get VillagersService from GameServices.
		/// </summary>
		public static object GetVillagersService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsVillagersServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get Field at map coordinates.
		/// Returns null if out of bounds or not in game.
		/// </summary>
		public static object GetField(int x, int y) {
			EnsureMapTypes();
			return ReflectionHelper.Invoke(_mapGetFieldMethod, GetMapService(), x, y);
		}

		/// <summary>
		/// Get object (building/resource) on a map tile.
		/// Returns null if nothing there or not in game.
		/// </summary>
		public static object GetObjectOn(int x, int y) {
			EnsureMapTypes();
			return ReflectionHelper.Invoke(_mapGetObjectOnMethod, GetMapService(), x, y);
		}

		/// <summary>
		/// Get Glade at map coordinates.
		/// Returns null if no glade at position or not in game.
		/// </summary>
		public static object GetGlade(int x, int y) {
			EnsureMapTypes();
			return ReflectionHelper.Invoke(_gladesGetGladeMethod, GetGladesService(), new Vector2Int(x, y));
		}

		/// <summary>
		/// Get all villagers as a dictionary.
		/// Returns null if not in game.
		/// </summary>
		public static object GetAllVillagers() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_villagersVillagersProperty, GetVillagersService());
		}

		/// <summary>
		/// Get ResourcesService from GameServices.
		/// Contains NaturalResources dictionary.
		/// </summary>
		public static object GetResourcesService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsResourcesServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get DepositsService from GameServices.
		/// Contains Deposits dictionary.
		/// </summary>
		public static object GetDepositsService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsDepositsServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get OreService from GameServices.
		/// Contains Ores dictionary (copper veins, etc.).
		/// </summary>
		public static object GetOreService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsOreServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get SpringsService from GameServices.
		/// Contains Springs dictionary (water sources).
		/// </summary>
		public static object GetSpringsService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsSpringsServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Remove all free springs from the map grid.
		/// Must be called before Extractor placement checks so IsFieldEmpty passes.
		/// Always pair with ReturnSpringsOnGrid after the check.
		/// </summary>
		public static bool RemoveSpringsFromGrid() {
			EnsureMapTypes();
			var springsService = GetSpringsService();
			return ReflectionHelper.InvokeVoid(_springsRemoveFromGridMethod, springsService);
		}

		/// <summary>
		/// Return all free springs to the map grid.
		/// Must be called after Extractor placement checks to restore grid state.
		/// </summary>
		public static bool ReturnSpringsOnGrid() {
			EnsureMapTypes();
			var springsService = GetSpringsService();
			return ReflectionHelper.InvokeVoid(_springsReturnOnGridMethod, springsService);
		}

		/// <summary>
		/// Get LakesService from GameServices.
		/// Contains Lakes dictionary (fishing spots).
		/// </summary>
		public static object GetLakesService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsLakesServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get BuildingsService from GameServices.
		/// Contains Buildings dictionary.
		/// </summary>
		public static object GetBuildingsService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsBuildingsServiceProperty, GetGameServices());
		}

		private static MethodInfo _getBuildingByIdMethod;
		private static MethodInfo _hasBuildingByIdMethod;
		private static bool _getBuildingByIdCached = false;

		/// <summary>
		/// Get a building by its numeric ID from BuildingsService.
		/// Returns null if building not found or not in game.
		/// </summary>
		public static object GetBuildingById(int id) {
			if (!GetIsGameActive()) return null;

			var buildingsService = GetBuildingsService();
			if (buildingsService == null) return null;

			if (!_getBuildingByIdCached) {
				var bsType = buildingsService.GetType();
				_hasBuildingByIdMethod = bsType.GetMethod("HasBuilding", new[] { typeof(int) });
				_getBuildingByIdMethod = bsType.GetMethod("GetBuilding", new[] { typeof(int) });
				_getBuildingByIdCached = true;
			}

			if (_hasBuildingByIdMethod == null || _getBuildingByIdMethod == null) return null;

			try {
				bool has = (bool)_hasBuildingByIdMethod.Invoke(buildingsService, new object[] { id });
				if (!has) return null;
				return _getBuildingByIdMethod.Invoke(buildingsService, new object[] { id });
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get ConditionsService from GameServices.
		/// </summary>
		public static object GetConditionsService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsConditionsServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get BiomeService from GameServices.
		/// </summary>
		public static object GetBiomeService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsBiomeServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get the current biome model.
		/// Returns null if not in game.
		/// </summary>
		public static object GetCurrentBiome() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_biomeCurrentBiomeProperty, GetBiomeService());
		}

		/// <summary>
		/// Check if blight is currently active in the game.
		/// </summary>
		public static bool IsBlightActive() {
			EnsureMapTypes();

			try {
				var conditionsService = GetConditionsService();
				if (conditionsService == null || _conditionsIsBlightActiveMethod == null) return false;

				return (bool)_conditionsIsBlightActiveMethod.Invoke(conditionsService, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBlightActive failed: {ex.Message}"); }
			return false;
		}

		/// <summary>
		/// Get BlightService from GameServices.
		/// </summary>
		public static object GetBlightService() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsBlightServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Get total active cysts in the settlement.
		/// Returns 0 if not in game or blight is not active.
		/// </summary>
		public static int GetGlobalActiveCysts() {
			EnsureMapTypes();

			try {
				var blightService = GetBlightService();
				if (blightService == null || _blightGetGlobalActiveCystsMethod == null) return 0;

				return (int)_blightGetGlobalActiveCystsMethod.Invoke(blightService, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGlobalActiveCysts failed: {ex.Message}"); }
			return 0;
		}

		/// <summary>
		/// Get predicted corruption percentage (0-1).
		/// Returns 0 if not in game or blight is not active.
		/// </summary>
		public static float GetPredictedCorruptionPercentage() {
			EnsureMapTypes();

			try {
				var blightService = GetBlightService();
				if (blightService == null || _blightGetPredictedPercentageCorruptionMethod == null) return 0f;

				return (float)_blightGetPredictedPercentageCorruptionMethod.Invoke(blightService, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetPredictedCorruptionPercentage failed: {ex.Message}"); }
			return 0f;
		}

		/// <summary>
		/// Get all BuildingBlight components from BuildingsService.
		/// Returns null if not in game.
		/// </summary>
		public static object GetBuildingsBlights() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_buildingsBlightsProperty, GetBuildingsService());
		}

		/// <summary>
		/// Get the main hearth building.
		/// Returns null if not in game.
		/// </summary>
		public static object GetMainHearth() {
			EnsureMapTypes();

			try {
				var buildingsService = GetBuildingsService();
				if (buildingsService == null || _buildingsGetMainHearthMethod == null) return null;

				return _buildingsGetMainHearthMethod.Invoke(buildingsService, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMainHearth failed: {ex.Message}"); }
			return null;
		}

		/// <summary>
		/// Get the active cyst count for a BuildingBlight component.
		/// </summary>
		public static int GetBlightActiveCysts(object buildingBlight) {
			EnsureMapTypes();

			if (buildingBlight == null || _buildingBlightGetActiveCystsMethod == null) return 0;

			try {
				return (int)_buildingBlightGetActiveCystsMethod.Invoke(buildingBlight, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBlightActiveCysts failed: {ex.Message}"); }
			return 0;
		}

		/// <summary>
		/// Get the owner Building from a BuildingBlight component.
		/// </summary>
		public static object GetBlightOwner(object buildingBlight) {
			EnsureMapTypes();

			if (buildingBlight == null || _buildingBlightOwnerProperty == null) return null;

			try {
				return _buildingBlightOwnerProperty.GetValue(buildingBlight);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBlightOwner failed: {ex.Message}"); }
			return null;
		}

		/// <summary>
		/// Get the corruption rate (0-1) from a Hearth building.
		/// </summary>
		public static float GetHearthCorruptionRate(object hearth) {
			EnsureMapTypes();

			if (hearth == null || _hearthGetCorruptionRateMethod == null) return 0f;

			try {
				return (float)_hearthGetCorruptionRateMethod.Invoke(hearth, null);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetHearthCorruptionRate failed: {ex.Message}"); }
			return 0f;
		}

		/// <summary>
		/// Get all glades from GladesService.
		/// Returns null if not in game.
		/// </summary>
		public static object GetAllGlades() {
			EnsureMapTypes();
			return ReflectionHelper.GetProp(_gsGladesProperty, GetGladesService());
		}

		/// <summary>
		/// Get the map width from MapService.Fields.
		/// Returns 70 as fallback if not available.
		/// </summary>
		public static int GetMapWidth() {
			EnsureMapTypes();
			var mapService = GetMapService();
			if (mapService == null) return 70; // Fallback

			try {
				if (_mapFieldsProperty != null) {
					var fields = _mapFieldsProperty.GetValue(mapService);
					if (fields != null) {
						if (_mapWidthField == null)
							_mapWidthField = fields.GetType().GetField("width",
								BindingFlags.Public | BindingFlags.Instance);
						if (_mapWidthField != null)
							return (int)_mapWidthField.GetValue(fields);
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMapWidth failed: {ex.Message}"); }
			return 70;
		}

		/// <summary>
		/// Get the map height from MapService.Fields.
		/// Returns 70 as fallback if not available.
		/// </summary>
		public static int GetMapHeight() {
			EnsureMapTypes();
			var mapService = GetMapService();
			if (mapService == null) return 70; // Fallback

			try {
				if (_mapFieldsProperty != null) {
					var fields = _mapFieldsProperty.GetValue(mapService);
					if (fields != null) {
						if (_mapHeightField == null)
							_mapHeightField = fields.GetType().GetField("height",
								BindingFlags.Public | BindingFlags.Instance);
						if (_mapHeightField != null)
							return (int)_mapHeightField.GetValue(fields);
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMapHeight failed: {ex.Message}"); }
			return 70;
		}

		/// <summary>
		/// Check if map coordinates are within bounds using MapService.InBounds().
		/// Returns false if not in game or coordinates are out of bounds.
		/// </summary>
		public static bool MapInBounds(int x, int y) {
			EnsureMapTypes();
			var mapService = GetMapService();
			if (mapService == null) return false;
			return ReflectionHelper.InvokeBool(_mapInBoundsMethod, mapService, x, y);
		}

		// Cached reflection for hearth position
		private static PropertyInfo _buildingFieldProperty = null;
		private static PropertyInfo _hearthsDictProperty = null;

		/// <summary>
		/// Get the main hearth's map position (Ancient Hearth).
		/// Returns null if not in game or hearth not found.
		/// </summary>
		public static Vector2Int? GetMainHearthPosition() {
			EnsureMapTypes();
			var buildingsService = GetBuildingsService();
			if (buildingsService == null) return null;

			try {
				// Get Hearths dictionary property
				if (_hearthsDictProperty == null) {
					_hearthsDictProperty = buildingsService.GetType().GetProperty("Hearths",
						BindingFlags.Public | BindingFlags.Instance);
				}

				if (_hearthsDictProperty == null) return null;

				var hearthsDict = _hearthsDictProperty.GetValue(buildingsService);
				if (hearthsDict == null) return null;

				// Get the dictionary as IDictionary to iterate
				var dict = hearthsDict as System.Collections.IDictionary;
				if (dict == null || dict.Count == 0) return null;

				// Get the first hearth (main hearth is always first)
				object firstHearth = null;
				foreach (System.Collections.DictionaryEntry entry in dict) {
					firstHearth = entry.Value;
					break;
				}

				if (firstHearth == null) return null;

				// Cache Field property (inherited from Building)
				if (_buildingFieldProperty == null) {
					_buildingFieldProperty = firstHearth.GetType().GetProperty("Field",
						BindingFlags.Public | BindingFlags.Instance);
				}

				if (_buildingFieldProperty == null) return null;

				var field = _buildingFieldProperty.GetValue(firstHearth);
				if (field is Vector2Int pos) {
					return pos;
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMainHearthPosition failed: {ex.Message}");
			}

			return null;
		}

		// ========================================
		// TIME SCALE SERVICE API (Pause/Unpause)
		// ========================================

		private static PropertyInfo _gsTimeScaleServiceProperty = null;
		private static MethodInfo _tssIsPausedMethod = null;
		private static MethodInfo _tssPauseMethod = null;
		private static MethodInfo _tssUnpauseMethod = null;
		private static bool _timeScaleTypesCached = false;

		private static void EnsureTimeScaleTypes() {
			if (_timeScaleTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_timeScaleTypesCached = true;
				return;
			}

			try {
				// Get TimeScaleService property from IGameServices
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsTimeScaleServiceProperty = gameServicesType.GetProperty("TimeScaleService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get methods from ITimeScaleService interface
				var timeScaleServiceType = _gameAssembly.GetType("Eremite.Services.ITimeScaleService");
				if (timeScaleServiceType != null) {
					_tssIsPausedMethod = timeScaleServiceType.GetMethod("IsPaused",
						BindingFlags.Public | BindingFlags.Instance);
					_tssPauseMethod = timeScaleServiceType.GetMethod("Pause",
						BindingFlags.Public | BindingFlags.Instance);
					_tssUnpauseMethod = timeScaleServiceType.GetMethod("Unpause",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached TimeScaleService type info");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] TimeScaleService type caching failed: {ex.Message}");
			}

			_timeScaleTypesCached = true;
		}

		/// <summary>
		/// Get TimeScaleService from GameServices.
		/// </summary>
		public static object GetTimeScaleService() {
			EnsureTimeScaleTypes();
			return ReflectionHelper.GetProp(_gsTimeScaleServiceProperty, GetGameServices());
		}

		/// <summary>
		/// Check if the game is currently paused.
		/// </summary>
		public static bool IsPaused() {
			EnsureTimeScaleTypes();
			return ReflectionHelper.InvokeBool(_tssIsPausedMethod, GetTimeScaleService());
		}

		// Game speed values: 0=paused, 1=1x, 2=1.5x, 3=2x, 4=3x
		private static readonly float[] Speeds = new float[] { 0f, 1f, 1.5f, 2f, 3f };
		private static MethodInfo _tssChangeMethod = null;

		/// <summary>
		/// Set game speed (1-4). 1=normal, 2=1.5x, 3=2x, 4=3x
		/// </summary>
		public static void SetSpeed(int speedIndex) {
			if (speedIndex < 1 || speedIndex > 4) return;

			EnsureTimeScaleTypes();
			var timeScaleService = GetTimeScaleService();
			if (timeScaleService == null) return;

			try {
				// Cache the Change method if needed
				if (_tssChangeMethod == null) {
					_tssChangeMethod = timeScaleService.GetType().GetMethod("Change",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Change(float scale, bool userBased, bool force = false)
				_tssChangeMethod?.Invoke(timeScaleService, new object[] { Speeds[speedIndex], true, false });
				Debug.Log($"[ATSAccessibility] Game speed set to {speedIndex} ({Speeds[speedIndex]}x)");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetSpeed failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Toggle pause state. If paused, unpause. If unpaused, pause.
		/// </summary>
		public static void TogglePause() {
			EnsureTimeScaleTypes();
			var timeScaleService = GetTimeScaleService();
			if (timeScaleService == null) return;

			try {
				if (IsPaused()) {
					// Unpause(userBased: true)
					_tssUnpauseMethod?.Invoke(timeScaleService, new object[] { true });
					Debug.Log("[ATSAccessibility] Game unpaused");
				} else {
					// Pause(userBased: true)
					_tssPauseMethod?.Invoke(timeScaleService, new object[] { true });
					Debug.Log("[ATSAccessibility] Game paused");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] TogglePause failed: {ex.Message}");
			}
		}

		// ========================================
		// CAMERA CONTROLLER API
		// ========================================

		private static void EnsureCameraTypes() {
			if (_cameraTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameControllerType == null) {
				_cameraTypesCached = true;
				return;
			}

			try {
				// Cache GameController.CameraController property
				_gcCameraControllerProperty = _gameControllerType.GetProperty("CameraController",
					BindingFlags.Public | BindingFlags.Instance);

				if (_gcCameraControllerProperty != null) {
					Debug.Log("[ATSAccessibility] Cached CameraController property");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Camera type caching failed: {ex.Message}");
			}

			_cameraTypesCached = true;
		}

		/// <summary>
		/// Get the CameraController from GameController.Instance.
		/// Only available when in a game (IsGameActive == true).
		/// </summary>
		public static object GetCameraController() {
			EnsureCameraTypes();

			if (!GetIsGameActive()) return null;
			if (_gcInstanceProperty == null || _gcCameraControllerProperty == null) return null;

			try {
				var gameController = _gcInstanceProperty.GetValue(null);
				if (gameController == null) return null;

				return _gcCameraControllerProperty.GetValue(gameController);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCameraController failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Set the camera target to make the camera smoothly pan to a transform.
		/// Uses our Harmony patch to implement smooth following that the game can't clear.
		/// </summary>
		public static void SetCameraTarget(Transform target) {
			if (target == null) return;

			// Use our Harmony patch's static target storage
			// This prevents the game from clearing the target when keyboard input is detected
			CameraControllerUpdateMovementPatch.SetTarget(target);
		}

		// ========================================
		// OBSERVABLE SUBSCRIPTION UTILITY
		// ========================================

		/// <summary>
		/// Subscribe to a UniRx IObservable using reflection.
		/// </summary>
		public static IDisposable SubscribeToObservable(object observable, Action<object> callback) {
			if (observable == null) return null;

			try {
				var observableType = observable.GetType();
				Debug.Log($"[ATSAccessibility] Observable type: {observableType.FullName}");

				// UniRx Subject<T> uses Subscribe(IObserver<T>), not Subscribe(Action<T>)
				// We need to create an IObserver wrapper
				var methods = observableType.GetMethods();

				foreach (var method in methods) {
					if (method.Name != "Subscribe") continue;
					var parameters = method.GetParameters();
					if (parameters.Length != 1) continue;

					var paramType = parameters[0].ParameterType;
					if (!paramType.IsGenericType) continue;

					// Check for IObserver<T>
					if (paramType.GetGenericTypeDefinition() == typeof(IObserver<>)) {
						var elementType = paramType.GetGenericArguments()[0];
						Debug.Log($"[ATSAccessibility] Found Subscribe(IObserver<{elementType.Name}>)");

						// Create our observer wrapper
						var observerType = typeof(ActionObserver<>).MakeGenericType(elementType);
						var observer = Activator.CreateInstance(observerType, new object[] { callback });

						// Invoke Subscribe
						var result = method.Invoke(observable, new object[] { observer });
						Debug.Log($"[ATSAccessibility] Subscribe invoked, result: {result != null}");
						return result as IDisposable;
					}
				}

				Debug.LogWarning("[ATSAccessibility] No matching Subscribe method found");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Observable subscription failed: {ex.Message}\n{ex.StackTrace}");
			}

			return null;
		}

		/// <summary>
		/// IObserver wrapper that calls an Action for each OnNext.
		/// Generic class to support different observable element types.
		/// </summary>
		public class ActionObserver<T>: IObserver<T> {
			private readonly Action<object> _callback;

			public ActionObserver(Action<object> callback) {
				_callback = callback;
			}

			public void OnNext(T value) {
				try {
					_callback?.Invoke(value);
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] Observer callback error: {ex.Message}");
				}
			}

			public void OnError(Exception error) {
				Debug.LogError($"[ATSAccessibility] Observable error: {error.Message}");
			}

			public void OnCompleted() {
				Debug.Log("[ATSAccessibility] Observable completed");
			}
		}

		// ========================================
		// WIKI/ENCYCLOPEDIA - Delegated to WikiReflection.cs
		// ========================================

		/// <summary>
		/// Check if the popup is a WikiPopup.
		/// This is a forwarding method to WikiReflection for backward compatibility.
		/// </summary>
		public static bool IsWikiPopup(object popup) => WikiReflection.IsWikiPopup(popup);

		// ========================================
		// STATS SERVICES (Reputation, Hostility, Resolve)
		// ========================================

		private static PropertyInfo _gsReputationServiceProperty = null;
		private static PropertyInfo _gsHostilityServiceProperty = null;
		private static PropertyInfo _gsResolveServiceProperty = null;
		private static PropertyInfo _gsRacesServiceProperty = null;
		private static bool _statsServiceTypesCached = false;

		private static void EnsureStatsServiceTypes() {
			if (_statsServiceTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_statsServiceTypesCached = true;
				return;
			}

			try {
				// Get IGameServices interface for service properties
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsReputationServiceProperty = gameServicesType.GetProperty("ReputationService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsHostilityServiceProperty = gameServicesType.GetProperty("HostilityService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsResolveServiceProperty = gameServicesType.GetProperty("ResolveService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsRacesServiceProperty = gameServicesType.GetProperty("RacesService",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached stats service types");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Stats service type caching failed: {ex.Message}");
			}

			_statsServiceTypesCached = true;
		}

		/// <summary>
		/// Get ReputationService from GameServices.
		/// Contains reputation values and penalty (impatience).
		/// </summary>
		public static object GetReputationService() {
			EnsureStatsServiceTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsReputationServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get HostilityService from GameServices.
		/// Contains hostility points and level.
		/// </summary>
		public static object GetHostilityService() {
			EnsureStatsServiceTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsHostilityServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get ResolveService from GameServices.
		/// Contains species resolve values and effects.
		/// </summary>
		public static object GetResolveService() {
			EnsureStatsServiceTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsResolveServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		// ========================================
		// FAVORING (RACE PREFERENCE)
		// ========================================

		private static MethodInfo _rsFavorRaceMethod = null;
		private static MethodInfo _rsStopFavoringMethod = null;
		private static MethodInfo _rsIsFavoredMethod = null;
		private static MethodInfo _rsIsFavoringOnCooldownMethod = null;
		private static MethodInfo _rsGetFavorCooldownLeftMethod = null;
		private static bool _favoringTypesCached = false;

		private static void EnsureFavoringTypes() {
			if (_favoringTypesCached) return;

			var resolveService = GetResolveService();
			if (resolveService == null) return;

			try {
				var type = resolveService.GetType();
				_rsFavorRaceMethod = type.GetMethod("FavorRace", PublicInstance);
				_rsStopFavoringMethod = type.GetMethod("StopFavoringRace", PublicInstance);
				_rsIsFavoredMethod = type.GetMethod("IsFavored", PublicInstance);
				_rsIsFavoringOnCooldownMethod = type.GetMethod("IsFavoringOnCooldown", PublicInstance);
				_rsGetFavorCooldownLeftMethod = type.GetMethod("GetFavorCooldownLeft", PublicInstance);
				_favoringTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] EnsureFavoringTypes failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a race is currently being favored.
		/// </summary>
		public static bool IsFavored(string raceName) {
			EnsureFavoringTypes();
			var resolveService = GetResolveService();
			if (resolveService == null || _rsIsFavoredMethod == null) return false;

			try {
				return (bool)_rsIsFavoredMethod.Invoke(resolveService, new object[] { raceName });
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Start favoring a race (gives them resolve bonus, penalizes others).
		/// </summary>
		public static bool FavorRace(string raceName) {
			EnsureFavoringTypes();
			var resolveService = GetResolveService();
			if (resolveService == null || _rsFavorRaceMethod == null) return false;

			try {
				_rsFavorRaceMethod.Invoke(resolveService, new object[] { raceName });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] FavorRace failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Stop favoring any race.
		/// </summary>
		public static bool StopFavoringRace() {
			EnsureFavoringTypes();
			var resolveService = GetResolveService();
			if (resolveService == null || _rsStopFavoringMethod == null) return false;

			try {
				_rsStopFavoringMethod.Invoke(resolveService, null);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] StopFavoringRace failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if favoring is on cooldown.
		/// </summary>
		public static bool IsFavoringOnCooldown() {
			EnsureFavoringTypes();
			var resolveService = GetResolveService();
			if (resolveService == null || _rsIsFavoringOnCooldownMethod == null) return false;

			try {
				return (bool)_rsIsFavoringOnCooldownMethod.Invoke(resolveService, null);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get remaining cooldown time for favoring.
		/// </summary>
		public static float GetFavorCooldownLeft() {
			EnsureFavoringTypes();
			var resolveService = GetResolveService();
			if (resolveService == null || _rsGetFavorCooldownLeftMethod == null) return 0f;

			try {
				return (float)_rsGetFavorCooldownLeftMethod.Invoke(resolveService, null);
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get RacesService from GameServices.
		/// Contains race definitions and configurations.
		/// </summary>
		public static object GetRacesService() {
			EnsureStatsServiceTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsRacesServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		// ========================================
		// CALENDAR SERVICE (Season, Year, Time)
		// ========================================

		private static PropertyInfo _gsCalendarServiceProperty = null;
		private static PropertyInfo _calYearProperty = null;
		private static PropertyInfo _calSeasonProperty = null;
		private static MethodInfo _calGetTimeTillNextSeasonMethod = null;
		private static bool _calendarTypesCached = false;

		private static void EnsureCalendarTypes() {
			if (_calendarTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_calendarTypesCached = true;
				return;
			}

			try {
				// Get IGameServices interface for CalendarService property
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get ICalendarService interface for Year, Season, and GetTimeTillNextSeasonChange
				var calendarServiceType = _gameAssembly.GetType("Eremite.Services.ICalendarService");
				if (calendarServiceType != null) {
					_calYearProperty = calendarServiceType.GetProperty("Year",
						BindingFlags.Public | BindingFlags.Instance);
					_calSeasonProperty = calendarServiceType.GetProperty("Season",
						BindingFlags.Public | BindingFlags.Instance);
					_calGetTimeTillNextSeasonMethod = calendarServiceType.GetMethod("GetTimeTillNextSeasonChange",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached CalendarService types");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CalendarService type caching failed: {ex.Message}");
			}

			_calendarTypesCached = true;
		}

		/// <summary>
		/// Get CalendarService from GameServices.
		/// Contains season, year, and time information.
		/// </summary>
		public static object GetCalendarService() {
			EnsureCalendarTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsCalendarServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the current settlement year.
		/// </summary>
		public static int GetYear() {
			EnsureCalendarTypes();
			var calService = GetCalendarService();
			if (calService == null) return 0;

			try {
				return (int)(_calYearProperty?.GetValue(calService) ?? 0);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the current season as enum int (0=Drizzle, 1=Clearance, 2=Storm).
		/// </summary>
		public static int GetSeason() {
			EnsureCalendarTypes();
			var calService = GetCalendarService();
			if (calService == null) return -1;

			try {
				var seasonEnum = _calSeasonProperty?.GetValue(calService);
				if (seasonEnum != null) {
					return (int)seasonEnum;
				}
				return -1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Get time remaining until next season change in seconds.
		/// </summary>
		public static float GetTimeTillNextSeason() {
			EnsureCalendarTypes();
			var calService = GetCalendarService();
			if (calService == null) return 0f;

			try {
				return (float)(_calGetTimeTillNextSeasonMethod?.Invoke(calService, null) ?? 0f);
			} catch {
				return 0f;
			}
		}

		// ========================================
		// GAME TIME SERVICE
		// ========================================

		private static PropertyInfo _gsGameTimeServiceProperty = null;
		private static PropertyInfo _gameTimeTimeProperty = null;
		private static bool _gameTimeTypesCached = false;

		private static void EnsureGameTimeTypes() {
			if (_gameTimeTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_gameTimeTypesCached = true;
				return;
			}

			try {
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGameTimeServiceProperty = gameServicesType.GetProperty("GameTimeService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				var gameTimeServiceType = _gameAssembly.GetType("Eremite.Services.IGameTimeService");
				if (gameTimeServiceType != null) {
					_gameTimeTimeProperty = gameTimeServiceType.GetProperty("Time",
						BindingFlags.Public | BindingFlags.Instance);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameTimeService type caching failed: {ex.Message}");
			}

			_gameTimeTypesCached = true;
		}

		/// <summary>
		/// Get the current game time (in-game seconds since settlement start).
		/// </summary>
		public static float GetGameTime() {
			EnsureGameTimeTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return 0f;

			try {
				var gameTimeService = _gsGameTimeServiceProperty?.GetValue(gameServices);
				if (gameTimeService == null) return 0f;
				return (float)(_gameTimeTimeProperty?.GetValue(gameTimeService) ?? 0f);
			} catch {
				return 0f;
			}
		}

		// ========================================
		// MYSTERIES/MODIFIERS (StateService access)
		// ========================================

		private static PropertyInfo _gsStateServiceProperty = null;
		private static PropertyInfo _ssSeasonalEffectsProperty = null;
		private static FieldInfo _seEffectsField = null;
		private static PropertyInfo _ssConditionsProperty = null;
		private static FieldInfo _condEarlyEffectsField = null;
		private static FieldInfo _condLateEffectsField = null;
		private static bool _mysteriesTypesCached = false;

		// Settings methods for model lookup
		private static MethodInfo _settingsGetSimpleSeasonalEffectMethod = null;
		private static MethodInfo _settingsGetConditionalSeasonalEffectMethod = null;
		private static MethodInfo _settingsGetEffectMethod = null;
		private static bool _settingsModelMethodsCached = false;

		private static void EnsureMysteriesTypes() {
			if (_mysteriesTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_mysteriesTypesCached = true;
				return;
			}

			try {
				// Get StateService property from IGameServices
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsStateServiceProperty = gameServicesType.GetProperty("StateService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get SeasonalEffects and Conditions from IStateService
				var stateServiceType = _gameAssembly.GetType("Eremite.Services.IStateService");
				if (stateServiceType != null) {
					_ssSeasonalEffectsProperty = stateServiceType.GetProperty("SeasonalEffects",
						BindingFlags.Public | BindingFlags.Instance);
					_ssConditionsProperty = stateServiceType.GetProperty("Conditions",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get effects field from SeasonalEffectsState
				var seasonalEffectsStateType = _gameAssembly.GetType("Eremite.Model.State.SeasonalEffectsState");
				if (seasonalEffectsStateType != null) {
					_seEffectsField = seasonalEffectsStateType.GetField("effects",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get earlyEffects and lateEffects from ConditionsState
				var conditionsStateType = _gameAssembly.GetType("Eremite.Model.State.ConditionsState");
				if (conditionsStateType != null) {
					_condEarlyEffectsField = conditionsStateType.GetField("earlyEffects",
						BindingFlags.Public | BindingFlags.Instance);
					_condLateEffectsField = conditionsStateType.GetField("lateEffects",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached mysteries/modifiers types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Mysteries type caching failed: {ex.Message}");
			}

			_mysteriesTypesCached = true;
		}

		private static void EnsureSettingsModelMethods() {
			if (_settingsModelMethodsCached) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_settingsModelMethodsCached = true;
				return;
			}

			try {
				// Get Settings type (Eremite.Model.Settings)
				var settingsType = _gameAssembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					// GetSimpleSeasonalEffect(string name)
					_settingsGetSimpleSeasonalEffectMethod = settingsType.GetMethod("GetSimpleSeasonalEffect",
						BindingFlags.Public | BindingFlags.Instance,
						null,
						new Type[] { typeof(string) },
						null);

					// GetConditionalSeasonalEffect(string name)
					_settingsGetConditionalSeasonalEffectMethod = settingsType.GetMethod("GetConditionalSeasonalEffect",
						BindingFlags.Public | BindingFlags.Instance,
						null,
						new Type[] { typeof(string) },
						null);

					// GetEffect(string name)
					_settingsGetEffectMethod = settingsType.GetMethod("GetEffect",
						BindingFlags.Public | BindingFlags.Instance,
						null,
						new Type[] { typeof(string) },
						null);

					Debug.Log("[ATSAccessibility] Cached Settings model lookup methods");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Settings model methods caching failed: {ex.Message}");
			}

			_settingsModelMethodsCached = true;
		}

		/// <summary>
		/// Get StateService from GameServices.
		/// Contains seasonal effects and conditions state.
		/// </summary>
		public static object GetStateService() {
			EnsureMysteriesTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsStateServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get SeasonalEffectsState from StateService.
		/// Contains the effects dictionary.
		/// </summary>
		public static object GetSeasonalEffectsState() {
			EnsureMysteriesTypes();
			var stateService = GetStateService();
			if (stateService == null) return null;

			try {
				return _ssSeasonalEffectsProperty?.GetValue(stateService);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the seasonal effects dictionary from SeasonalEffectsState.
		/// Returns Dictionary&lt;string, SeasonalEffectState&gt;.
		/// </summary>
		public static System.Collections.IDictionary GetSeasonalEffectsDictionary() {
			EnsureMysteriesTypes();
			var seasonalEffectsState = GetSeasonalEffectsState();
			if (seasonalEffectsState == null) return null;

			try {
				return _seEffectsField?.GetValue(seasonalEffectsState) as System.Collections.IDictionary;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get ConditionsState from StateService.
		/// Contains early and late effects lists.
		/// </summary>
		public static object GetConditionsState() {
			EnsureMysteriesTypes();
			var stateService = GetStateService();
			if (stateService == null) return null;

			try {
				return _ssConditionsProperty?.GetValue(stateService);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the early effects list from ConditionsState.
		/// These are modifiers applied at embark.
		/// </summary>
		public static List<string> GetEarlyEffects() {
			EnsureMysteriesTypes();
			var conditionsState = GetConditionsState();
			if (conditionsState == null) return null;

			try {
				return _condEarlyEffectsField?.GetValue(conditionsState) as List<string>;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the late effects list from ConditionsState.
		/// These are modifiers applied at embark.
		/// </summary>
		public static List<string> GetLateEffects() {
			EnsureMysteriesTypes();
			var conditionsState = GetConditionsState();
			if (conditionsState == null) return null;

			try {
				return _condLateEffectsField?.GetValue(conditionsState) as List<string>;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get SimpleSeasonalEffect model by name from Settings.
		/// </summary>
		public static object GetSimpleSeasonalEffectModel(string name) {
			EnsureSettingsModelMethods();
			var settings = GetSettings();
			if (settings == null || _settingsGetSimpleSeasonalEffectMethod == null) return null;

			try {
				return _settingsGetSimpleSeasonalEffectMethod.Invoke(settings, new object[] { name });
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get ConditionalSeasonalEffect model by name from Settings.
		/// </summary>
		public static object GetConditionalSeasonalEffectModel(string name) {
			EnsureSettingsModelMethods();
			var settings = GetSettings();
			if (settings == null || _settingsGetConditionalSeasonalEffectMethod == null) return null;

			try {
				return _settingsGetConditionalSeasonalEffectMethod.Invoke(settings, new object[] { name });
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get Effect model by name from Settings.
		/// Used for world modifiers.
		/// </summary>
		public static object GetEffectModel(string name) {
			EnsureSettingsModelMethods();
			var settings = GetSettings();
			if (settings == null || _settingsGetEffectMethod == null) return null;

			try {
				return _settingsGetEffectMethod.Invoke(settings, new object[] { name });
			} catch {
				return null;
			}
		}

		// ========================================
		// GOODS/STORAGE REFLECTION (for resource panel)
		// ========================================

		private static PropertyInfo _gsStorageServiceProperty = null;
		private static MethodInfo _ssGetStorageMethod = null;
		private static PropertyInfo _storageGoodsProperty = null;
		private static FieldInfo _goodsCollectionGoodsField = null;
		private static FieldInfo _settingsGoodsField = null;
		private static bool _goodsTypesCached = false;

		private static void EnsureGoodsTypes() {
			if (_goodsTypesCached) return;
			EnsureGameServicesTypes();

			if (_gameAssembly == null) {
				_goodsTypesCached = true;
				return;
			}

			try {
				// Get StorageService property from IGameServices
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsStorageServiceProperty = gameServicesType.GetProperty("StorageService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get GetStorage method from IStorageService
				var storageServiceType = _gameAssembly.GetType("Eremite.Services.IStorageService");
				if (storageServiceType != null) {
					_ssGetStorageMethod = storageServiceType.GetMethod("GetStorage",
						Type.EmptyTypes); // No parameters version
				}

				// Get Goods property from Storage class
				var storageType = _gameAssembly.GetType("Eremite.Buildings.Storage");
				if (storageType != null) {
					_storageGoodsProperty = storageType.GetProperty("Goods",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get goods field from GoodsCollection
				var goodsCollectionType = _gameAssembly.GetType("Eremite.GoodsCollection");
				if (goodsCollectionType != null) {
					_goodsCollectionGoodsField = goodsCollectionType.GetField("goods",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get Goods array from Settings
				var settingsType = _gameAssembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGoodsField = settingsType.GetField("Goods",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached goods/storage types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Goods type caching failed: {ex.Message}");
			}

			_goodsTypesCached = true;
		}

		/// <summary>
		/// Get StorageService from GameServices.
		/// </summary>
		public static object GetStorageService() {
			EnsureGoodsTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsStorageServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the main Storage building (contains goods collection).
		/// </summary>
		public static object GetMainStorage() {
			EnsureGoodsTypes();
			var storageService = GetStorageService();
			if (storageService == null || _ssGetStorageMethod == null) return null;

			try {
				return _ssGetStorageMethod.Invoke(storageService, null);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get all stored goods as a dictionary (goodName -> amount).
		/// Only includes goods with amount > 0.
		/// </summary>
		public static Dictionary<string, int> GetAllStoredGoods() {
			EnsureGoodsTypes();
			var storage = GetMainStorage();
			if (storage == null) return new Dictionary<string, int>();

			try {
				// Get Goods property (LockedGoodsCollection)
				var goodsCollection = _storageGoodsProperty?.GetValue(storage);
				if (goodsCollection == null) return new Dictionary<string, int>();

				// Get the goods dictionary
				var goodsDict = _goodsCollectionGoodsField?.GetValue(goodsCollection) as Dictionary<string, int>;
				if (goodsDict == null) return new Dictionary<string, int>();

				// Filter to only goods with amount > 0
				var result = new Dictionary<string, int>();
				foreach (var kvp in goodsDict) {
					if (kvp.Value > 0) {
						result[kvp.Key] = kvp.Value;
					}
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetAllStoredGoods failed: {ex.Message}");
				return new Dictionary<string, int>();
			}
		}

		/// <summary>
		/// Get all GoodModel definitions from Settings.
		/// </summary>
		public static Array GetAllGoodModels() {
			EnsureGoodsTypes();
			var settings = GetSettings();
			if (settings == null || _settingsGoodsField == null) return null;

			try {
				return _settingsGoodsField.GetValue(settings) as Array;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the category of a GoodModel.
		/// </summary>
		public static object GetGoodCategory(object goodModel) {
			if (goodModel == null) return null;

			try {
				var categoryField = goodModel.GetType().GetField("category",
					BindingFlags.Public | BindingFlags.Instance);
				return categoryField?.GetValue(goodModel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the display name from a GoodModel or GoodCategoryModel.
		/// Both use displayName.Text pattern.
		/// </summary>
		public static string GetDisplayName(object model) {
			if (model == null) return null;

			try {
				// Get displayName field (LocaText)
				var displayNameField = model.GetType().GetField("displayName",
					BindingFlags.Public | BindingFlags.Instance);
				if (displayNameField == null) return null;

				var locaText = displayNameField.GetValue(model);
				if (locaText == null) return null;

				// Get Text property from LocaText
				var textProperty = locaText.GetType().GetProperty("Text",
					BindingFlags.Public | BindingFlags.Instance);
				return textProperty?.GetValue(locaText) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the internal name from a model (SO.Name property).
		/// </summary>
		public static string GetModelName(object model) {
			if (model == null) return null;

			try {
				var nameProperty = model.GetType().GetProperty("Name",
					BindingFlags.Public | BindingFlags.Instance);
				return nameProperty?.GetValue(model) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the order field from a model (used for sorting).
		/// </summary>
		public static int GetModelOrder(object model) {
			if (model == null) return 0;

			try {
				var orderField = model.GetType().GetField("order",
					BindingFlags.Public | BindingFlags.Instance);
				if (orderField != null) {
					return (int)orderField.GetValue(model);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGoodOrder failed: {ex.Message}"); }
			return 0;
		}

		/// <summary>
		/// Check if a GoodModel is active.
		/// </summary>
		public static bool IsGoodActive(object goodModel) {
			if (goodModel == null) return false;

			try {
				var isActiveField = goodModel.GetType().GetField("isActive",
					BindingFlags.Public | BindingFlags.Instance);
				if (isActiveField != null) {
					return (bool)isActiveField.GetValue(goodModel);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsGoodActive failed: {ex.Message}"); }
			return true; // Default to active
		}

		// ========================================
		// SHARED GOODREF PROPERTIES - Forwarding to BuildingReflection
		// ========================================
		public static System.Type GoodRefType { get { return BuildingReflection.GoodRefType; } }
		public static System.Reflection.FieldInfo GoodRefGoodField { get { return BuildingReflection.GoodRefGoodField; } }
		public static System.Reflection.FieldInfo GoodRefAmountField { get { return BuildingReflection.GoodRefAmountField; } }
		public static System.Reflection.PropertyInfo GoodRefDisplayNameProperty { get { return BuildingReflection.GoodRefDisplayNameProperty; } }

		// ========================================
		// MENU HUB - POPUP OPENING METHODS
		// ========================================

		// Cached reflection metadata for GameBlackboardService
		private static PropertyInfo _gsGameBlackboardServiceProperty = null;
		private static bool _gameBlackboardTypesInitialized = false;

		private static void EnsureGameBlackboardTypes() {
			if (_gameBlackboardTypesInitialized) return;
			EnsureAssembly();

			if (_gameAssembly == null) {
				_gameBlackboardTypesInitialized = true;
				return;
			}

			try {
				// Get IGameServices interface for GameBlackboardService property
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGameBlackboardServiceProperty = gameServicesType.GetProperty("GameBlackboardService",
						BindingFlags.Public | BindingFlags.Instance);
					Debug.Log("[ATSAccessibility] Cached GameBlackboardService property");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameBlackboard type caching failed: {ex.Message}");
			}

			_gameBlackboardTypesInitialized = true;
		}

		/// <summary>
		/// Get GameBlackboardService from GameServices.
		/// </summary>
		public static object GetGameBlackboardService() {
			EnsureGameBlackboardTypes();

			var gameServices = GetGameServices();
			if (gameServices == null || _gsGameBlackboardServiceProperty == null) return null;

			try {
				return _gsGameBlackboardServiceProperty.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		// Cached Unit.Default value — safe to cache permanently: UniRx.Unit.Default is an immutable boxed struct, not a game service instance.
		private static object _unitDefault = null;
		private static bool _unitDefaultCached = false;

		/// <summary>
		/// Get UniRx.Unit.Default value for Subject&lt;Unit&gt; OnNext calls.
		/// </summary>
		public static object GetUnitDefault() {
			if (_unitDefaultCached) return _unitDefault;

			try {
				Type unitType = Type.GetType("UniRx.Unit, UniRx");
				if (unitType == null) {
					foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
						unitType = assembly.GetType("UniRx.Unit");
						if (unitType != null) break;
					}
				}

				if (unitType != null) {
					// Try as a field first
					var defaultField = unitType.GetField("Default", BindingFlags.Public | BindingFlags.Static);
					if (defaultField != null) {
						_unitDefault = defaultField.GetValue(null);
					} else {
						// Try as a property
						var defaultProperty = unitType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
						if (defaultProperty != null) {
							_unitDefault = defaultProperty.GetValue(null);
						} else {
							// Unit is a struct - default(Unit) works, so create an instance
							_unitDefault = Activator.CreateInstance(unitType);
						}
					}
				}

				_unitDefaultCached = true;
				if (_unitDefault == null) {
					Debug.LogWarning("[ATSAccessibility] Could not get UniRx.Unit.Default - type not found");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetUnitDefault failed: {ex.Message}");
				_unitDefaultCached = true;
			}

			return _unitDefault;
		}

		/// <summary>
		/// Helper to invoke OnNext on a UniRx Subject property.
		/// </summary>
		public static bool InvokeSubjectOnNext(object blackboardService, string subjectPropertyName, object parameter) {
			if (blackboardService == null) return false;

			try {
				var subjectProperty = blackboardService.GetType().GetProperty(subjectPropertyName,
					BindingFlags.Public | BindingFlags.Instance);
				if (subjectProperty == null) {
					Debug.LogWarning($"[ATSAccessibility] Subject property '{subjectPropertyName}' not found");
					return false;
				}

				var subject = subjectProperty.GetValue(blackboardService);
				if (subject == null) {
					Debug.LogWarning($"[ATSAccessibility] Subject '{subjectPropertyName}' is null");
					return false;
				}

				var onNextMethod = subject.GetType().GetMethod("OnNext",
					BindingFlags.Public | BindingFlags.Instance);
				if (onNextMethod == null) {
					Debug.LogWarning($"[ATSAccessibility] OnNext method not found on subject");
					return false;
				}

				onNextMethod.Invoke(subject, new object[] { parameter });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] InvokeSubjectOnNext failed for {subjectPropertyName}: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Open the Recipes popup via GameBlackboardService.RecipesPopupRequested.
		/// </summary>
		public static bool OpenRecipesPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenRecipesPopup: GameBlackboardService not available");
				return false;
			}

			try {
				// Create RecipesPopupRequest(true) for playShowAnim
				var requestType = _gameAssembly?.GetType("Eremite.View.Popups.Recipes.RecipesPopupRequest");
				if (requestType == null) {
					Debug.LogWarning("[ATSAccessibility] RecipesPopupRequest type not found");
					return false;
				}

				// Constructor: RecipesPopupRequest(bool playShowAnim)
				var constructor = requestType.GetConstructor(new[] { typeof(bool) });
				if (constructor == null) {
					Debug.LogWarning("[ATSAccessibility] RecipesPopupRequest constructor not found");
					return false;
				}

				var request = constructor.Invoke(new object[] { true });
				return InvokeSubjectOnNext(blackboardService, "RecipesPopupRequested", request);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OpenRecipesPopup failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Open the Orders popup via GameBlackboardService.OrdersPopupRequested.
		/// </summary>
		public static bool OpenOrdersPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenOrdersPopup: GameBlackboardService not available");
				return false;
			}

			return InvokeSubjectOnNext(blackboardService, "OrdersPopupRequested", true);
		}

		/// <summary>
		/// Open the Trade Routes popup via GameBlackboardService.TradeRoutesPopupRequested.
		/// </summary>
		public static bool OpenTradeRoutesPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenTradeRoutesPopup: GameBlackboardService not available");
				return false;
			}

			return InvokeSubjectOnNext(blackboardService, "TradeRoutesPopupRequested", true);
		}

		/// <summary>
		/// Open the Consumption Control popup via GameBlackboardService.ConsumptionPopupRequested.
		/// </summary>
		public static bool OpenConsumptionPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenConsumptionPopup: GameBlackboardService not available");
				return false;
			}

			return InvokeSubjectOnNext(blackboardService, "ConsumptionPopupRequested", true);
		}

		/// <summary>
		/// Open the Payments popup via GameBlackboardService.PaymentsPopupRequested.
		/// </summary>
		public static bool OpenPaymentsPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenPaymentsPopup: GameBlackboardService not available");
				return false;
			}

			return InvokeSubjectOnNext(blackboardService, "PaymentsPopupRequested", true);
		}

		/// <summary>
		/// Open the Trends popup via GameBlackboardService.TrendsPopupRequested.
		/// </summary>
		public static bool OpenTrendsPopup() {
			var blackboardService = GetGameBlackboardService();
			if (blackboardService == null) {
				Debug.LogWarning("[ATSAccessibility] OpenTrendsPopup: GameBlackboardService not available");
				return false;
			}

			return InvokeSubjectOnNext(blackboardService, "TrendsPopupRequested", true);
		}



		/// <summary>
		/// Open the Trader panel via TraderPanel.Instance.Show().
		/// </summary>
		public static bool OpenTraderPanel() {
			try {
				// Find TraderPanel type
				var traderPanelType = _gameAssembly?.GetType("Eremite.Buildings.UI.Trade.TraderPanel");
				if (traderPanelType == null) {
					Debug.LogWarning("[ATSAccessibility] TraderPanel type not found");
					return false;
				}

				// Get Instance static property
				var instanceProperty = traderPanelType.GetProperty("Instance",
					BindingFlags.Public | BindingFlags.Static);
				if (instanceProperty == null) {
					Debug.LogWarning("[ATSAccessibility] TraderPanel.Instance property not found");
					return false;
				}

				var traderPanel = instanceProperty.GetValue(null);
				if (traderPanel == null) {
					// TraderPanel.Instance is null - likely no trading post built yet
					Debug.LogWarning("[ATSAccessibility] TraderPanel.Instance is null (no trading post built?)");
					return false;
				}

				// Get current trader visit from TradeService
				var gameServices = GetGameServices();
				if (gameServices == null) {
					Debug.LogWarning("[ATSAccessibility] GameServices not available");
					return false;
				}

				var gameServicesType = _gameAssembly?.GetType("Eremite.Services.IGameServices");
				var tradeServiceProperty = gameServicesType?.GetProperty("TradeService",
					BindingFlags.Public | BindingFlags.Instance);
				if (tradeServiceProperty == null) {
					Debug.LogWarning("[ATSAccessibility] TradeService property not found");
					return false;
				}

				var tradeService = tradeServiceProperty.GetValue(gameServices);
				if (tradeService == null) {
					Debug.LogWarning("[ATSAccessibility] TradeService is null");
					return false;
				}

				// Get current visit using GetCurrentMainVisit() method (may return null if no trader)
				var getCurrentVisitMethod = tradeService.GetType().GetMethod("GetCurrentMainVisit",
					BindingFlags.Public | BindingFlags.Instance);
				object currentVisit = null;
				if (getCurrentVisitMethod != null) {
					currentVisit = getCurrentVisitMethod.Invoke(tradeService, null);
				}

				// Call Show(visit, playShowAnim) - visit can be null, panel handles it
				var showMethod = traderPanelType.GetMethod("Show",
					BindingFlags.Public | BindingFlags.Instance,
					null,
					new Type[] { _gameAssembly.GetType("Eremite.Model.State.TraderVisitState"), typeof(bool) },
					null);

				if (showMethod == null) {
					Debug.LogWarning("[ATSAccessibility] TraderPanel.Show method not found");
					return false;
				}

				showMethod.Invoke(traderPanel, new object[] { currentVisit, true });
				Debug.Log("[ATSAccessibility] TraderPanel opened successfully");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OpenTraderPanel failed: {ex.Message}\n{ex.StackTrace}");
				return false;
			}
		}

		// ========================================
		// GOODS HELPERS
		// ========================================

		// Cache for Settings.GetGood method
		private static MethodInfo _settingsGetGoodMethodCached = null;
		private static bool _settingsGetGoodCached = false;

		private static void EnsureSettingsGetGood() {
			if (_settingsGetGoodCached) return;

			try {
				var assembly = GameAssembly;
				if (assembly == null) {
					_settingsGetGoodCached = true;
					return;
				}

				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetGoodMethodCached = settingsType.GetMethod("GetGood",
						new[] { typeof(string) });
				}
			} catch {
				// Ignore
			}

			_settingsGetGoodCached = true;
		}

		/// <summary>
		/// Get the display name for a good by its internal name.
		/// </summary>
		public static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return "Unknown";

			EnsureSettingsGetGood();

			try {
				var settings = GetSettings();
				if (settings == null || _settingsGetGoodMethodCached == null) return goodName;

				var goodModel = _settingsGetGoodMethodCached.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return goodName;

				var displayNameProp = goodModel.GetType().GetProperty("displayName", PublicInstance);
				var locaText = displayNameProp?.GetValue(goodModel);
				return GetLocaText(locaText) ?? goodName;
			} catch {
				return goodName;
			}
		}

		/// <summary>
		/// Get the description of a good by its internal name.
		/// Returns the full description with sources, sinks, and races (rich text stripped).
		/// </summary>
		public static string GetGoodDescription(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return null;

			EnsureSettingsGetGood();

			try {
				var settings = GetSettings();
				if (settings == null || _settingsGetGoodMethodCached == null) return null;

				var goodModel = _settingsGetGoodMethodCached.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return null;

				var descProp = goodModel.GetType().GetProperty("Description", PublicInstance);
				var description = descProp?.GetValue(goodModel) as string;
				if (string.IsNullOrEmpty(description)) return null;

				return OrdersReflection.StripRichText(description).Trim();
			} catch (System.Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGoodDescription failed for {goodName}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Alias for GetAllStoredGoods() for consistency.
		/// </summary>
		public static Dictionary<string, int> GetStorageGoods() {
			return GetAllStoredGoods();
		}

		// Cache for Settings.GetRelic method
		private static MethodInfo _settingsGetRelicMethodCached = null;
		private static bool _settingsGetRelicCached = false;

		private static void EnsureSettingsGetRelic() {
			if (_settingsGetRelicCached) return;

			try {
				var assembly = GameAssembly;
				if (assembly == null) {
					_settingsGetRelicCached = true;
					return;
				}

				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetRelicMethodCached = settingsType.GetMethod("GetRelic",
						new[] { typeof(string) });
				}
			} catch {
				// Ignore
			}

			_settingsGetRelicCached = true;
		}

		/// <summary>
		/// Get the display name for a relic by its internal model name.
		/// </summary>
		public static string GetRelicDisplayName(string relicModelName) {
			if (string.IsNullOrEmpty(relicModelName)) return "Unknown";

			EnsureSettingsGetRelic();

			try {
				var settings = GetSettings();
				if (settings == null || _settingsGetRelicMethodCached == null) return relicModelName;

				var relicModel = _settingsGetRelicMethodCached.Invoke(settings, new object[] { relicModelName });
				if (relicModel == null) return relicModelName;

				var displayNameField = relicModel.GetType().GetField("displayName", PublicInstance);
				var locaText = displayNameField?.GetValue(relicModel);
				return GetLocaText(locaText) ?? relicModelName;
			} catch {
				return relicModelName;
			}
		}

		// Cache for Settings.GetMetaCurrency method
		private static MethodInfo _settingsGetMetaCurrencyMethodCached = null;
		private static PropertyInfo _metaCurrencyModelDisplayNameProperty = null;
		private static bool _settingsGetMetaCurrencyCached = false;

		private static void EnsureSettingsGetMetaCurrency() {
			if (_settingsGetMetaCurrencyCached) return;

			try {
				var assembly = GameAssembly;
				if (assembly == null) {
					_settingsGetMetaCurrencyCached = true;
					return;
				}

				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetMetaCurrencyMethodCached = settingsType.GetMethod("GetMetaCurrency",
						new[] { typeof(string) });
				}

				var metaCurrencyModelType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
				if (metaCurrencyModelType != null) {
					_metaCurrencyModelDisplayNameProperty = metaCurrencyModelType.GetProperty("DisplayName",
						PublicInstance);
				}
			} catch {
				// Ignore
			}

			_settingsGetMetaCurrencyCached = true;
		}

		/// <summary>
		/// Get the display name for a meta currency by its internal name.
		/// Meta currencies include Food Stockpiles, Machinery Parts, Artifacts, etc.
		/// </summary>
		public static string GetMetaCurrencyDisplayName(string currencyName) {
			if (string.IsNullOrEmpty(currencyName)) return "Unknown";

			EnsureSettingsGetMetaCurrency();

			try {
				var settings = GetSettings();
				if (settings == null || _settingsGetMetaCurrencyMethodCached == null) return currencyName;

				var currencyModel = _settingsGetMetaCurrencyMethodCached.Invoke(settings, new object[] { currencyName });
				if (currencyModel == null) return currencyName;

				// MetaCurrencyModel.DisplayName returns the localized string directly (not LocaText)
				if (_metaCurrencyModelDisplayNameProperty != null) {
					var displayName = _metaCurrencyModelDisplayNameProperty.GetValue(currencyModel)?.ToString();
					return !string.IsNullOrEmpty(displayName) ? displayName : currencyName;
				}

				return currencyName;
			} catch {
				return currencyName;
			}
		}

		// ========================================
		// MODIFIERS PANEL (Effects, Cornerstones, Perks)
		// ========================================

		private static PropertyInfo _gsEffectsServiceProperty = null;
		private static PropertyInfo _gsPerksServiceProperty = null;
		private static MethodInfo _esGetAllConditionsMethod = null;
		private static PropertyInfo _psSortedPerksProperty = null;
		private static PropertyInfo _ssCornerstonesProperty = null;
		private static FieldInfo _csActiveCornerstonesField = null;
		private static bool _modifiersPanelTypesCached = false;

		private static void EnsureModifiersPanelTypes() {
			if (_modifiersPanelTypesCached) return;
			EnsureGameServicesTypes();
			EnsureMysteriesTypes();

			if (_gameAssembly == null) {
				_modifiersPanelTypesCached = true;
				return;
			}

			try {
				// Get EffectsService and PerksService from IGameServices
				var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService",
						BindingFlags.Public | BindingFlags.Instance);
					_gsPerksServiceProperty = gameServicesType.GetProperty("PerksService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get GetAllConditions method from IEffectsService
				var effectsServiceType = _gameAssembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_esGetAllConditionsMethod = effectsServiceType.GetMethod("GetAllConditions",
						Type.EmptyTypes);
				}

				// Get SortedPerks property from IPerksService
				var perksServiceType = _gameAssembly.GetType("Eremite.Services.IPerksService");
				if (perksServiceType != null) {
					_psSortedPerksProperty = perksServiceType.GetProperty("SortedPerks",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get Cornerstones property from IStateService
				var stateServiceType = _gameAssembly.GetType("Eremite.Services.IStateService");
				if (stateServiceType != null) {
					_ssCornerstonesProperty = stateServiceType.GetProperty("Cornerstones",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get activeCornerstones field from CornerstonesState
				var cornerstonesStateType = _gameAssembly.GetType("Eremite.Model.State.CornerstonesState");
				if (cornerstonesStateType != null) {
					_csActiveCornerstonesField = cornerstonesStateType.GetField("activeCornerstones",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached modifiers panel types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Modifiers panel type caching failed: {ex.Message}");
			}

			_modifiersPanelTypesCached = true;
		}

		/// <summary>
		/// Get EffectsService from GameServices.
		/// </summary>
		public static object GetEffectsService() {
			EnsureModifiersPanelTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsEffectsServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get PerksService from GameServices.
		/// </summary>
		public static object GetPerksService() {
			EnsureModifiersPanelTypes();
			var gameServices = GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsPerksServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get CornerstonesState from StateService.
		/// </summary>
		public static object GetCornerstonesState() {
			EnsureModifiersPanelTypes();
			var stateService = GetStateService();
			if (stateService == null) return null;

			try {
				return _ssCornerstonesProperty?.GetValue(stateService);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get all active conditions/effects via EffectsService.GetAllConditions().
		/// Returns IEnumerable of EffectModel objects.
		/// Includes: biome effects, difficulty modifiers, embark effects, event effects.
		/// </summary>
		public static System.Collections.IEnumerable GetAllConditions() {
			EnsureModifiersPanelTypes();
			var effectsService = GetEffectsService();
			if (effectsService == null || _esGetAllConditionsMethod == null) return null;

			try {
				return _esGetAllConditionsMethod.Invoke(effectsService, null) as System.Collections.IEnumerable;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetAllConditions failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the list of active cornerstone effect names.
		/// Returns List of effect name strings.
		/// </summary>
		public static List<string> GetActiveCornerstones() {
			EnsureModifiersPanelTypes();
			var cornerstonesState = GetCornerstonesState();
			if (cornerstonesState == null || _csActiveCornerstonesField == null) return null;

			try {
				return _csActiveCornerstonesField.GetValue(cornerstonesState) as List<string>;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetActiveCornerstones failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the sorted perks list from PerksService.
		/// Returns List of PerkState objects with name, stacks, hidden fields.
		/// </summary>
		public static System.Collections.IList GetSortedPerks() {
			EnsureModifiersPanelTypes();
			var perksService = GetPerksService();
			if (perksService == null || _psSortedPerksProperty == null) return null;

			try {
				return _psSortedPerksProperty.GetValue(perksService) as System.Collections.IList;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetSortedPerks failed: {ex.Message}");
				return null;
			}
		}

		// Cached PerkState field info
		private static FieldInfo _perkStateNameField = null;
		private static FieldInfo _perkStateStacksField = null;
		private static FieldInfo _perkStateHiddenField = null;
		private static bool _perkStateFieldsCached = false;

		private static void EnsurePerkStateFields(object firstPerk) {
			if (_perkStateFieldsCached || firstPerk == null) return;

			try {
				var perkType = firstPerk.GetType();
				_perkStateNameField = perkType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
				_perkStateStacksField = perkType.GetField("stacks", BindingFlags.Public | BindingFlags.Instance);
				_perkStateHiddenField = perkType.GetField("hidden", BindingFlags.Public | BindingFlags.Instance);
				Debug.Log("[ATSAccessibility] Cached PerkState fields");
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsurePerkStateFields failed: {ex.Message}");
			}

			_perkStateFieldsCached = true;
		}

		/// <summary>
		/// Extract perk info from a PerkState object.
		/// Returns tuple of (name, stacks, hidden).
		/// </summary>
		public static (string name, int stacks, bool hidden) GetPerkInfo(object perkState) {
			if (perkState == null) return (null, 0, true);

			EnsurePerkStateFields(perkState);

			try {
				string name = _perkStateNameField?.GetValue(perkState) as string ?? "";
				int stacks = (int?)_perkStateStacksField?.GetValue(perkState) ?? 1;
				bool hidden = (bool?)_perkStateHiddenField?.GetValue(perkState) ?? false;
				return (name, stacks, hidden);
			} catch {
				return (null, 0, true);
			}
		}

		// Cached EffectModel property for IsPerk check
		private static PropertyInfo _effectModelIsPerkProperty = null;
		private static PropertyInfo _effectModelNameProperty = null;
		private static bool _effectModelPropsCached = false;

		private static void EnsureEffectModelProps(object effectModel) {
			if (_effectModelPropsCached || effectModel == null) return;

			try {
				var effectType = effectModel.GetType();
				_effectModelIsPerkProperty = effectType.GetProperty("IsPerk", BindingFlags.Public | BindingFlags.Instance);
				_effectModelNameProperty = effectType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
				Debug.Log("[ATSAccessibility] Cached EffectModel IsPerk/Name properties");
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureEffectModelProps failed: {ex.Message}");
			}

			_effectModelPropsCached = true;
		}

		/// <summary>
		/// Check if an EffectModel is a perk (IsPerk property).
		/// Effects with IsPerk=true get added to perks list when applied.
		/// </summary>
		public static bool GetEffectIsPerk(object effectModel) {
			if (effectModel == null) return false;

			EnsureEffectModelProps(effectModel);

			try {
				return (bool?)_effectModelIsPerkProperty?.GetValue(effectModel) ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the internal Name property from an EffectModel.
		/// </summary>
		public static string GetEffectName(object effectModel) {
			if (effectModel == null) return null;

			EnsureEffectModelProps(effectModel);

			try {
				return _effectModelNameProperty?.GetValue(effectModel) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the wrapped EffectModel from a SimpleSeasonalEffectModel.
		/// Only SimpleSeasonalEffectModel has an "effect" field - ConditionalSeasonalEffectModel does not.
		/// </summary>
		public static object GetSeasonalEffectWrappedEffect(object seasonalEffectModel) {
			if (seasonalEffectModel == null) return null;

			try {
				// Get the effect field directly from this model instance
				// SimpleSeasonalEffectModel has "effect" field, ConditionalSeasonalEffectModel does not
				var modelType = seasonalEffectModel.GetType();
				var effectField = modelType.GetField("effect", BindingFlags.Public | BindingFlags.Instance);
				return effectField?.GetValue(seasonalEffectModel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the internal name of the wrapped effect inside a seasonal effect model.
		/// This is the name that appears in PerksService when the mystery is active.
		/// Only works for SimpleSeasonalEffectModel which has an "effect" field.
		/// </summary>
		public static string GetSeasonalEffectWrappedEffectName(object seasonalEffectModel) {
			var wrappedEffect = GetSeasonalEffectWrappedEffect(seasonalEffectModel);
			return GetEffectName(wrappedEffect);
		}

		/// <summary>
		/// Get the hostility level required for a seasonal effect model.
		/// Both SimpleSeasonalEffectModel and ConditionalSeasonalEffectModel have this field.
		/// Returns 0 if no hostility level requirement.
		/// </summary>
		public static int GetSeasonalEffectHostilityLevel(object seasonalEffectModel) {
			if (seasonalEffectModel == null) return 0;

			try {
				var modelType = seasonalEffectModel.GetType();
				var hostilityField = modelType.GetField("hostilityLevel", BindingFlags.Public | BindingFlags.Instance);
				return (int?)hostilityField?.GetValue(seasonalEffectModel) ?? 0;
			} catch {
				return 0;
			}
		}



		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(GameReflection), "GameReflection");
		}
	}
}
