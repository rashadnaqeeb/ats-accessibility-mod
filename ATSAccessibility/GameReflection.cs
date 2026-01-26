using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to game internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (GameController, services, etc.) - they are destroyed on scene change
    /// - All public methods return fresh values by querying through cached PropertyInfo
    /// </summary>
    public static class GameReflection
    {
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

        // ========================================
        // HELPER METHODS (reduce try-catch boilerplate)
        // ========================================

        private static T TryGetPropertyValue<T>(PropertyInfo prop, object instance) where T : class
        {
            if (prop == null || instance == null) return null;
            try { return prop.GetValue(instance) as T; }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] TryGetPropertyValue failed: {ex.Message}"); return null; }
        }

        private static object TryInvokeMethod(MethodInfo method, object instance, object[] args = null)
        {
            if (method == null || instance == null) return null;
            try { return method.Invoke(instance, args); }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] TryInvokeMethod failed: {ex.Message}"); return null; }
        }

        private static bool TryInvokeBool(MethodInfo method, object instance, object[] args = null)
        {
            if (method == null || instance == null) return false;
            try { return (bool?)method.Invoke(instance, args) ?? false; }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] TryInvokeBool failed: {ex.Message}"); return false; }
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
        public static string GetLocaText(object locaText)
        {
            if (locaText == null) return null;

            // Cache the Text property on first use
            if (_locaTextTextProperty == null)
            {
                _locaTextTextProperty = locaText.GetType().GetProperty("Text", PublicInstance);
            }

            try
            {
                return _locaTextTextProperty?.GetValue(locaText) as string;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // INTERNAL ACCESSORS (for WorldMapReflection)
        // ========================================

        internal static Assembly GameAssembly
        {
            get
            {
                EnsureAssembly();
                return _gameAssembly;
            }
        }

        internal static bool TryInvokeBoolInternal(MethodInfo method, object instance, object[] args = null)
        {
            return TryInvokeBool(method, instance, args);
        }

        internal static void EnsureMetaControllerTypesInternal()
        {
            EnsureMetaControllerTypes();
        }

        internal static PropertyInfo MetaControllerInstanceProperty
        {
            get
            {
                EnsureMetaControllerTypes();
                return _metaControllerInstanceProperty;
            }
        }

        internal static PropertyInfo McMetaServicesProperty
        {
            get
            {
                EnsureMetaControllerTypes();
                return _mcMetaServicesProperty;
            }
        }

        /// <summary>
        /// Get the MetaServices instance (fresh each time).
        /// Path: MetaController.Instance.MetaServices
        /// </summary>
        public static object GetMetaServices()
        {
            EnsureMetaControllerTypes();

            try
            {
                var metaController = _metaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                return _mcMetaServicesProperty?.GetValue(metaController);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetMetaServices failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureAssembly()
        {
            if (_assemblyCached) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Assembly-CSharp")
                {
                    _gameAssembly = assembly;
                    Debug.Log("[ATSAccessibility] Found Assembly-CSharp");
                    break;
                }
            }

            if (_gameAssembly == null)
            {
                Debug.LogWarning("[ATSAccessibility] Assembly-CSharp not found");
            }

            _assemblyCached = true;
        }

        private static void EnsureTypes()
        {
            if (_typesInitialized) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _typesInitialized = true;
                return;
            }

            try
            {
                // Cache GameController type info
                _gameControllerType = _gameAssembly.GetType("Eremite.Controller.GameController");
                if (_gameControllerType != null)
                {
                    _gcIsGameActiveProperty = _gameControllerType.GetProperty("IsGameActive",
                        BindingFlags.Public | BindingFlags.Static);

                    Debug.Log("[ATSAccessibility] Cached GameController type info");
                }
                else
                {
                    Debug.LogWarning("[ATSAccessibility] GameController type not found");
                }

                // Cache MainController type info
                _mainControllerType = _gameAssembly.GetType("Eremite.Controller.MainController");
                if (_mainControllerType != null)
                {
                    _mcInstanceProperty = _mainControllerType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _mcAppServicesProperty = _mainControllerType.GetProperty("AppServices",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached MainController type info");
                }
                else
                {
                    Debug.LogWarning("[ATSAccessibility] MainController type not found");
                }
            }
            catch (Exception ex)
            {
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
        public static bool GetIsGameActive()
        {
            EnsureTypes();

            if (_gcIsGameActiveProperty == null) return false;

            try
            {
                return (bool)_gcIsGameActiveProperty.GetValue(null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get MainController instance. This persists across scenes via DontDestroyOnLoad.
        /// Still, do not cache long-term as it could be recreated.
        /// </summary>
        public static object GetMainControllerInstance()
        {
            EnsureTypes();

            if (_mcInstanceProperty == null) return null;

            try
            {
                return _mcInstanceProperty.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get AppServices from MainController.
        /// </summary>
        public static object GetAppServices()
        {
            var mc = GetMainControllerInstance();
            if (mc == null || _mcAppServicesProperty == null) return null;

            try
            {
                return _mcAppServicesProperty.GetValue(mc);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get PopupsService from AppServices.
        /// DO NOT cache - get fresh reference each time.
        /// </summary>
        public static object GetPopupsService()
        {
            var appServices = GetAppServices();
            if (appServices == null) return null;

            try
            {
                // Cache the property info (safe), but always get fresh value
                if (_popupsServiceProperty == null)
                {
                    _popupsServiceProperty = appServices.GetType().GetProperty("PopupsService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                return _popupsServiceProperty?.GetValue(appServices);
            }
            catch
            {
                return null;
            }
        }

        // Cached field for activePopups list
        private static FieldInfo _activePopupsField = null;

        /// <summary>
        /// Get the top active popup from PopupsService (index 0 of activePopups list).
        /// Returns null if no popups are active.
        /// </summary>
        public static object GetTopActivePopup()
        {
            var popupsService = GetPopupsService();
            if (popupsService == null) return null;

            try
            {
                if (_activePopupsField == null)
                {
                    _activePopupsField = popupsService.GetType().GetField("activePopups",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                var activePopups = _activePopupsField?.GetValue(popupsService) as System.Collections.IList;
                if (activePopups == null || activePopups.Count == 0) return null;

                return activePopups[0];
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTopActivePopup failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Find a type by name in the game assembly.
        /// Used for detecting game-specific components like TabsButton.
        /// </summary>
        public static Type FindTypeByName(string typeName)
        {
            EnsureAssembly();
            if (_gameAssembly == null) return null;

            try
            {
                return _gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == typeName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a type by its full name (e.g., "Eremite.View.HUD.GoodSlot").
        /// More efficient than FindTypeByName when full name is known.
        /// </summary>
        public static Type GetTypeByName(string fullTypeName)
        {
            EnsureAssembly();
            return _gameAssembly?.GetType(fullTypeName);
        }

        /// <summary>
        /// Get the game Settings via MB.Settings static property.
        /// Contains all game model data including goods, buildings, etc.
        /// </summary>
        public static object GetSettings()
        {
            EnsureAssembly();
            if (_gameAssembly == null)
            {
                Debug.Log("[ATSAccessibility] GetSettings: _gameAssembly is null");
                return null;
            }

            try
            {
                var mbType = _gameAssembly.GetType("Eremite.MB");
                if (mbType == null)
                {
                    Debug.Log("[ATSAccessibility] GetSettings: Eremite.MB type not found");
                    return null;
                }

                // Settings is protected static, so we need NonPublic flag
                var settingsProperty = mbType.GetProperty("Settings",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (settingsProperty == null)
                {
                    Debug.Log("[ATSAccessibility] GetSettings: Settings property not found");
                    return null;
                }

                var result = settingsProperty.GetValue(null);
                if (result == null)
                {
                    Debug.Log("[ATSAccessibility] GetSettings: Settings value is null");
                }
                return result;
            }
            catch (Exception ex)
            {
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
        public static void EnsureTabTypes()
        {
            if (_tabTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _tabTypesCached = true;
                return;
            }

            try
            {
                // Cache TabsPanel type and fields
                _tabsPanelType = FindTypeByName("TabsPanel");
                if (_tabsPanelType != null)
                {
                    _tabsPanelButtonsField = _tabsPanelType.GetField("buttons",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _tabsPanelCurrentField = _tabsPanelType.GetField("current",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    Debug.Log($"[ATSAccessibility] Cached TabsPanel type: {_tabsPanelType.FullName}");
                }

                // Cache TabsButton type and fields
                _tabsButtonType = FindTypeByName("TabsButton");
                if (_tabsButtonType != null)
                {
                    _tabsButtonButtonField = _tabsButtonType.GetField("button",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _tabsButtonContentField = _tabsButtonType.GetField("content",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    Debug.Log($"[ATSAccessibility] Cached TabsButton type: {_tabsButtonType.FullName}");
                }
            }
            catch (Exception ex)
            {
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
        // META CONTROLLER REFLECTION
        // ========================================
        // Path: MetaController.Instance.MetaServices

        private static Type _metaControllerType = null;
        private static PropertyInfo _metaControllerInstanceProperty = null;  // static Instance
        private static PropertyInfo _mcMetaServicesProperty = null;          // MetaServices
        private static bool _metaControllerTypesCached = false;

        private static void EnsureMetaControllerTypes()
        {
            if (_metaControllerTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _metaControllerTypesCached = true;
                return;
            }

            try
            {
                // Cache MetaController type
                _metaControllerType = _gameAssembly.GetType("Eremite.Controller.MetaController");
                if (_metaControllerType != null)
                {
                    _metaControllerInstanceProperty = _metaControllerType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _mcMetaServicesProperty = _metaControllerType.GetProperty("MetaServices",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached MetaController type info");
                }
            }
            catch (Exception ex)
            {
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

        private static void EnsureGameServicesTypes()
        {
            if (_gameServicesTypesCached) return;
            EnsureTypes();

            if (_gameControllerType == null)
            {
                _gameServicesTypesCached = true;
                return;
            }

            try
            {
                // Cache GameController.Instance property
                _gcInstanceProperty = _gameControllerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);

                // Cache GameServices property
                _gcGameServicesProperty = _gameControllerType.GetProperty("GameServices",
                    BindingFlags.Public | BindingFlags.Instance);

                // Cache ReputationRewardsService property from IGameServices interface
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsReputationRewardsProperty = gameServicesType.GetProperty("ReputationRewardsService",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GameServices type caching failed: {ex.Message}");
            }

            _gameServicesTypesCached = true;
        }

        /// <summary>
        /// Get the ReputationRewardsService from GameController.
        /// Only available when in a game (IsGameActive == true).
        /// </summary>
        public static object GetReputationRewardsService()
        {
            EnsureGameServicesTypes();

            if (!GetIsGameActive()) return null;

            try
            {
                // Get GameController.Instance
                var gameController = _gcInstanceProperty?.GetValue(null);
                if (gameController == null) return null;

                // Get GameServices
                var gameServices = _gcGameServicesProperty?.GetValue(gameController);
                if (gameServices == null) return null;

                // Get ReputationRewardsService
                return _gsReputationRewardsProperty?.GetValue(gameServices);
            }
            catch (Exception ex)
            {
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
        private static bool _mapTypesCached = false;

        private static void EnsureMapTypes()
        {
            if (_mapTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _mapTypesCached = true;
                return;
            }

            try
            {
                // Get IGameServices interface for service properties
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
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
                    _gsLakesServiceProperty = gameServicesType.GetProperty("LakesService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gsBuildingsServiceProperty = gameServicesType.GetProperty("BuildingsService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gsConditionsServiceProperty = gameServicesType.GetProperty("ConditionsService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get IsBlightActive method from IConditionsService
                var conditionsServiceType = _gameAssembly.GetType("Eremite.Services.IConditionsService");
                if (conditionsServiceType != null)
                {
                    _conditionsIsBlightActiveMethod = conditionsServiceType.GetMethod("IsBlightActive",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get BlightService from IGameServices
                if (gameServicesType != null)
                {
                    _gsBlightServiceProperty = gameServicesType.GetProperty("BlightService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get BlightService methods from IBlightService
                var blightServiceType = _gameAssembly.GetType("Eremite.Services.IBlightService");
                if (blightServiceType != null)
                {
                    _blightGetGlobalActiveCystsMethod = blightServiceType.GetMethod("GetGlobalActiveCysts",
                        BindingFlags.Public | BindingFlags.Instance);
                    _blightGetPredictedPercentageCorruptionMethod = blightServiceType.GetMethod("GetPredictedPercentageCorruption",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get BuildingsBlights and GetMainHearth from IBuildingsService
                var buildingsServiceType = _gameAssembly.GetType("Eremite.Services.IBuildingsService");
                if (buildingsServiceType != null)
                {
                    _buildingsBlightsProperty = buildingsServiceType.GetProperty("BuildingsBlights",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingsGetMainHearthMethod = buildingsServiceType.GetMethod("GetMainHearth",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get BuildingBlight methods
                var buildingBlightType = _gameAssembly.GetType("Eremite.Buildings.BuildingBlight");
                if (buildingBlightType != null)
                {
                    _buildingBlightGetActiveCystsMethod = buildingBlightType.GetMethod("GetActiveCysts",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingBlightOwnerProperty = buildingBlightType.GetProperty("Owner",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Hearth.GetCorruptionRate method
                var hearthType = _gameAssembly.GetType("Eremite.Buildings.Hearth");
                if (hearthType != null)
                {
                    _hearthGetCorruptionRateMethod = hearthType.GetMethod("GetCorruptionRate",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Glades property and method from IGladesService
                var gladesServiceType = _gameAssembly.GetType("Eremite.Services.IGladesService");
                if (gladesServiceType != null)
                {
                    _gsGladesProperty = gladesServiceType.GetProperty("Glades",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gladesGetGladeMethod = gladesServiceType.GetMethod("GetGlade",
                        new Type[] { typeof(Vector2Int) });
                }

                // Get MapService methods
                var mapServiceType = _gameAssembly.GetType("Eremite.Services.IMapService");
                if (mapServiceType != null)
                {
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
                if (villagersServiceType != null)
                {
                    _villagersVillagersProperty = villagersServiceType.GetProperty("Villagers",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached map service types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Map type caching failed: {ex.Message}");
            }

            _mapTypesCached = true;
        }

        /// <summary>
        /// Get GameServices from GameController.Instance.
        /// Only available when in a game (IsGameActive == true).
        /// </summary>
        public static object GetGameServices()
        {
            EnsureGameServicesTypes();

            if (!GetIsGameActive()) return null;

            try
            {
                var gameController = _gcInstanceProperty?.GetValue(null);
                if (gameController == null) return null;

                return _gcGameServicesProperty?.GetValue(gameController);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get MapService from GameServices.
        /// </summary>
        public static object GetMapService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsMapServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get GladesService from GameServices.
        /// </summary>
        public static object GetGladesService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsGladesServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get VillagersService from GameServices.
        /// </summary>
        public static object GetVillagersService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsVillagersServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get Field at map coordinates.
        /// Returns null if out of bounds or not in game.
        /// </summary>
        public static object GetField(int x, int y)
        {
            EnsureMapTypes();
            return TryInvokeMethod(_mapGetFieldMethod, GetMapService(), new object[] { x, y });
        }

        /// <summary>
        /// Get object (building/resource) on a map tile.
        /// Returns null if nothing there or not in game.
        /// </summary>
        public static object GetObjectOn(int x, int y)
        {
            EnsureMapTypes();
            return TryInvokeMethod(_mapGetObjectOnMethod, GetMapService(), new object[] { x, y });
        }

        /// <summary>
        /// Get Glade at map coordinates.
        /// Returns null if no glade at position or not in game.
        /// </summary>
        public static object GetGlade(int x, int y)
        {
            EnsureMapTypes();
            return TryInvokeMethod(_gladesGetGladeMethod, GetGladesService(), new object[] { new Vector2Int(x, y) });
        }

        /// <summary>
        /// Get all villagers as a dictionary.
        /// Returns null if not in game.
        /// </summary>
        public static object GetAllVillagers()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_villagersVillagersProperty, GetVillagersService());
        }

        /// <summary>
        /// Get ResourcesService from GameServices.
        /// Contains NaturalResources dictionary.
        /// </summary>
        public static object GetResourcesService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsResourcesServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get DepositsService from GameServices.
        /// Contains Deposits dictionary.
        /// </summary>
        public static object GetDepositsService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsDepositsServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get OreService from GameServices.
        /// Contains Ores dictionary (copper veins, etc.).
        /// </summary>
        public static object GetOreService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsOreServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get SpringsService from GameServices.
        /// Contains Springs dictionary (water sources).
        /// </summary>
        public static object GetSpringsService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsSpringsServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get LakesService from GameServices.
        /// Contains Lakes dictionary (fishing spots).
        /// </summary>
        public static object GetLakesService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsLakesServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get BuildingsService from GameServices.
        /// Contains Buildings dictionary.
        /// </summary>
        public static object GetBuildingsService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsBuildingsServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get ConditionsService from GameServices.
        /// </summary>
        public static object GetConditionsService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsConditionsServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Check if blight is currently active in the game.
        /// </summary>
        public static bool IsBlightActive()
        {
            EnsureMapTypes();

            try
            {
                var conditionsService = GetConditionsService();
                if (conditionsService == null || _conditionsIsBlightActiveMethod == null) return false;

                return (bool)_conditionsIsBlightActiveMethod.Invoke(conditionsService, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBlightActive failed: {ex.Message}"); }
            return false;
        }

        /// <summary>
        /// Get BlightService from GameServices.
        /// </summary>
        public static object GetBlightService()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsBlightServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Get total active cysts in the settlement.
        /// Returns 0 if not in game or blight is not active.
        /// </summary>
        public static int GetGlobalActiveCysts()
        {
            EnsureMapTypes();

            try
            {
                var blightService = GetBlightService();
                if (blightService == null || _blightGetGlobalActiveCystsMethod == null) return 0;

                return (int)_blightGetGlobalActiveCystsMethod.Invoke(blightService, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGlobalActiveCysts failed: {ex.Message}"); }
            return 0;
        }

        /// <summary>
        /// Get predicted corruption percentage (0-1).
        /// Returns 0 if not in game or blight is not active.
        /// </summary>
        public static float GetPredictedCorruptionPercentage()
        {
            EnsureMapTypes();

            try
            {
                var blightService = GetBlightService();
                if (blightService == null || _blightGetPredictedPercentageCorruptionMethod == null) return 0f;

                return (float)_blightGetPredictedPercentageCorruptionMethod.Invoke(blightService, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetPredictedCorruptionPercentage failed: {ex.Message}"); }
            return 0f;
        }

        /// <summary>
        /// Get all BuildingBlight components from BuildingsService.
        /// Returns null if not in game.
        /// </summary>
        public static object GetBuildingsBlights()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_buildingsBlightsProperty, GetBuildingsService());
        }

        /// <summary>
        /// Get the main hearth building.
        /// Returns null if not in game.
        /// </summary>
        public static object GetMainHearth()
        {
            EnsureMapTypes();

            try
            {
                var buildingsService = GetBuildingsService();
                if (buildingsService == null || _buildingsGetMainHearthMethod == null) return null;

                return _buildingsGetMainHearthMethod.Invoke(buildingsService, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMainHearth failed: {ex.Message}"); }
            return null;
        }

        /// <summary>
        /// Get the active cyst count for a BuildingBlight component.
        /// </summary>
        public static int GetBlightActiveCysts(object buildingBlight)
        {
            EnsureMapTypes();

            if (buildingBlight == null || _buildingBlightGetActiveCystsMethod == null) return 0;

            try
            {
                return (int)_buildingBlightGetActiveCystsMethod.Invoke(buildingBlight, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBlightActiveCysts failed: {ex.Message}"); }
            return 0;
        }

        /// <summary>
        /// Get the owner Building from a BuildingBlight component.
        /// </summary>
        public static object GetBlightOwner(object buildingBlight)
        {
            EnsureMapTypes();

            if (buildingBlight == null || _buildingBlightOwnerProperty == null) return null;

            try
            {
                return _buildingBlightOwnerProperty.GetValue(buildingBlight);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBlightOwner failed: {ex.Message}"); }
            return null;
        }

        /// <summary>
        /// Get the corruption rate (0-1) from a Hearth building.
        /// </summary>
        public static float GetHearthCorruptionRate(object hearth)
        {
            EnsureMapTypes();

            if (hearth == null || _hearthGetCorruptionRateMethod == null) return 0f;

            try
            {
                return (float)_hearthGetCorruptionRateMethod.Invoke(hearth, null);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetHearthCorruptionRate failed: {ex.Message}"); }
            return 0f;
        }

        /// <summary>
        /// Get all glades from GladesService.
        /// Returns null if not in game.
        /// </summary>
        public static object GetAllGlades()
        {
            EnsureMapTypes();
            return TryGetPropertyValue<object>(_gsGladesProperty, GetGladesService());
        }

        /// <summary>
        /// Get the map width from MapService.Fields.
        /// Returns 70 as fallback if not available.
        /// </summary>
        public static int GetMapWidth()
        {
            EnsureMapTypes();
            var mapService = GetMapService();
            if (mapService == null) return 70; // Fallback

            try
            {
                if (_mapFieldsProperty != null)
                {
                    var fields = _mapFieldsProperty.GetValue(mapService);
                    if (fields != null)
                    {
                        if (_mapWidthField == null)
                            _mapWidthField = fields.GetType().GetField("width",
                                BindingFlags.Public | BindingFlags.Instance);
                        if (_mapWidthField != null)
                            return (int)_mapWidthField.GetValue(fields);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMapWidth failed: {ex.Message}"); }
            return 70;
        }

        /// <summary>
        /// Get the map height from MapService.Fields.
        /// Returns 70 as fallback if not available.
        /// </summary>
        public static int GetMapHeight()
        {
            EnsureMapTypes();
            var mapService = GetMapService();
            if (mapService == null) return 70; // Fallback

            try
            {
                if (_mapFieldsProperty != null)
                {
                    var fields = _mapFieldsProperty.GetValue(mapService);
                    if (fields != null)
                    {
                        if (_mapHeightField == null)
                            _mapHeightField = fields.GetType().GetField("height",
                                BindingFlags.Public | BindingFlags.Instance);
                        if (_mapHeightField != null)
                            return (int)_mapHeightField.GetValue(fields);
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMapHeight failed: {ex.Message}"); }
            return 70;
        }

        /// <summary>
        /// Check if map coordinates are within bounds using MapService.InBounds().
        /// Returns false if not in game or coordinates are out of bounds.
        /// </summary>
        public static bool MapInBounds(int x, int y)
        {
            EnsureMapTypes();
            var mapService = GetMapService();
            if (mapService == null) return false;
            return TryInvokeBool(_mapInBoundsMethod, mapService, new object[] { x, y });
        }

        // Cached reflection for hearth position
        private static PropertyInfo _buildingFieldProperty = null;
        private static PropertyInfo _hearthsDictProperty = null;

        /// <summary>
        /// Get the main hearth's map position (Ancient Hearth).
        /// Returns null if not in game or hearth not found.
        /// </summary>
        public static Vector2Int? GetMainHearthPosition()
        {
            EnsureMapTypes();
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                // Get Hearths dictionary property
                if (_hearthsDictProperty == null)
                {
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
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    firstHearth = entry.Value;
                    break;
                }

                if (firstHearth == null) return null;

                // Cache Field property (inherited from Building)
                if (_buildingFieldProperty == null)
                {
                    _buildingFieldProperty = firstHearth.GetType().GetProperty("Field",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_buildingFieldProperty == null) return null;

                var field = _buildingFieldProperty.GetValue(firstHearth);
                if (field is Vector2Int pos)
                {
                    return pos;
                }
            }
            catch (Exception ex)
            {
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

        private static void EnsureTimeScaleTypes()
        {
            if (_timeScaleTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _timeScaleTypesCached = true;
                return;
            }

            try
            {
                // Get TimeScaleService property from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsTimeScaleServiceProperty = gameServicesType.GetProperty("TimeScaleService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get methods from ITimeScaleService interface
                var timeScaleServiceType = _gameAssembly.GetType("Eremite.Services.ITimeScaleService");
                if (timeScaleServiceType != null)
                {
                    _tssIsPausedMethod = timeScaleServiceType.GetMethod("IsPaused",
                        BindingFlags.Public | BindingFlags.Instance);
                    _tssPauseMethod = timeScaleServiceType.GetMethod("Pause",
                        BindingFlags.Public | BindingFlags.Instance);
                    _tssUnpauseMethod = timeScaleServiceType.GetMethod("Unpause",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached TimeScaleService type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TimeScaleService type caching failed: {ex.Message}");
            }

            _timeScaleTypesCached = true;
        }

        /// <summary>
        /// Get TimeScaleService from GameServices.
        /// </summary>
        public static object GetTimeScaleService()
        {
            EnsureTimeScaleTypes();
            return TryGetPropertyValue<object>(_gsTimeScaleServiceProperty, GetGameServices());
        }

        /// <summary>
        /// Check if the game is currently paused.
        /// </summary>
        public static bool IsPaused()
        {
            EnsureTimeScaleTypes();
            return TryInvokeBool(_tssIsPausedMethod, GetTimeScaleService());
        }

        // Game speed values: 0=paused, 1=1x, 2=1.5x, 3=2x, 4=3x
        private static readonly float[] Speeds = new float[] { 0f, 1f, 1.5f, 2f, 3f };
        private static MethodInfo _tssChangeMethod = null;

        /// <summary>
        /// Set game speed (1-4). 1=normal, 2=1.5x, 3=2x, 4=3x
        /// </summary>
        public static void SetSpeed(int speedIndex)
        {
            if (speedIndex < 1 || speedIndex > 4) return;

            EnsureTimeScaleTypes();
            var timeScaleService = GetTimeScaleService();
            if (timeScaleService == null) return;

            try
            {
                // Cache the Change method if needed
                if (_tssChangeMethod == null)
                {
                    _tssChangeMethod = timeScaleService.GetType().GetMethod("Change",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Change(float scale, bool userBased, bool force = false)
                _tssChangeMethod?.Invoke(timeScaleService, new object[] { Speeds[speedIndex], true, false });
                Debug.Log($"[ATSAccessibility] Game speed set to {speedIndex} ({Speeds[speedIndex]}x)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetSpeed failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Toggle pause state. If paused, unpause. If unpaused, pause.
        /// </summary>
        public static void TogglePause()
        {
            EnsureTimeScaleTypes();
            var timeScaleService = GetTimeScaleService();
            if (timeScaleService == null) return;

            try
            {
                if (IsPaused())
                {
                    // Unpause(userBased: true)
                    _tssUnpauseMethod?.Invoke(timeScaleService, new object[] { true });
                    Debug.Log("[ATSAccessibility] Game unpaused");
                }
                else
                {
                    // Pause(userBased: true)
                    _tssPauseMethod?.Invoke(timeScaleService, new object[] { true });
                    Debug.Log("[ATSAccessibility] Game paused");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TogglePause failed: {ex.Message}");
            }
        }

        // ========================================
        // CAMERA CONTROLLER API
        // ========================================

        private static void EnsureCameraTypes()
        {
            if (_cameraTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameControllerType == null)
            {
                _cameraTypesCached = true;
                return;
            }

            try
            {
                // Cache GameController.CameraController property
                _gcCameraControllerProperty = _gameControllerType.GetProperty("CameraController",
                    BindingFlags.Public | BindingFlags.Instance);

                if (_gcCameraControllerProperty != null)
                {
                    Debug.Log("[ATSAccessibility] Cached CameraController property");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Camera type caching failed: {ex.Message}");
            }

            _cameraTypesCached = true;
        }

        /// <summary>
        /// Get the CameraController from GameController.Instance.
        /// Only available when in a game (IsGameActive == true).
        /// </summary>
        public static object GetCameraController()
        {
            EnsureCameraTypes();

            if (!GetIsGameActive()) return null;
            if (_gcInstanceProperty == null || _gcCameraControllerProperty == null) return null;

            try
            {
                var gameController = _gcInstanceProperty.GetValue(null);
                if (gameController == null) return null;

                return _gcCameraControllerProperty.GetValue(gameController);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCameraController failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Set the camera target to make the camera smoothly pan to a transform.
        /// Uses our Harmony patch to implement smooth following that the game can't clear.
        /// </summary>
        public static void SetCameraTarget(Transform target)
        {
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
        public static IDisposable SubscribeToObservable(object observable, Action<object> callback)
        {
            if (observable == null) return null;

            try
            {
                var observableType = observable.GetType();
                Debug.Log($"[ATSAccessibility] Observable type: {observableType.FullName}");

                // UniRx Subject<T> uses Subscribe(IObserver<T>), not Subscribe(Action<T>)
                // We need to create an IObserver wrapper
                var methods = observableType.GetMethods();

                foreach (var method in methods)
                {
                    if (method.Name != "Subscribe") continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length != 1) continue;

                    var paramType = parameters[0].ParameterType;
                    if (!paramType.IsGenericType) continue;

                    // Check for IObserver<T>
                    if (paramType.GetGenericTypeDefinition() == typeof(IObserver<>))
                    {
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Observable subscription failed: {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }

        /// <summary>
        /// IObserver wrapper that calls an Action for each OnNext.
        /// Generic class to support different observable element types.
        /// </summary>
        public class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<object> _callback;

            public ActionObserver(Action<object> callback)
            {
                _callback = callback;
            }

            public void OnNext(T value)
            {
                try
                {
                    _callback?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] Observer callback error: {ex.Message}");
                }
            }

            public void OnError(Exception error)
            {
                Debug.LogError($"[ATSAccessibility] Observable error: {error.Message}");
            }

            public void OnCompleted()
            {
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

        private static void EnsureStatsServiceTypes()
        {
            if (_statsServiceTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _statsServiceTypesCached = true;
                return;
            }

            try
            {
                // Get IGameServices interface for service properties
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Stats service type caching failed: {ex.Message}");
            }

            _statsServiceTypesCached = true;
        }

        /// <summary>
        /// Get ReputationService from GameServices.
        /// Contains reputation values and penalty (impatience).
        /// </summary>
        public static object GetReputationService()
        {
            EnsureStatsServiceTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsReputationServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get HostilityService from GameServices.
        /// Contains hostility points and level.
        /// </summary>
        public static object GetHostilityService()
        {
            EnsureStatsServiceTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsHostilityServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ResolveService from GameServices.
        /// Contains species resolve values and effects.
        /// </summary>
        public static object GetResolveService()
        {
            EnsureStatsServiceTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsResolveServiceProperty?.GetValue(gameServices);
            }
            catch
            {
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

        private static void EnsureFavoringTypes()
        {
            if (_favoringTypesCached) return;

            var resolveService = GetResolveService();
            if (resolveService == null) return;

            try
            {
                var type = resolveService.GetType();
                _rsFavorRaceMethod = type.GetMethod("FavorRace", PublicInstance);
                _rsStopFavoringMethod = type.GetMethod("StopFavoringRace", PublicInstance);
                _rsIsFavoredMethod = type.GetMethod("IsFavored", PublicInstance);
                _rsIsFavoringOnCooldownMethod = type.GetMethod("IsFavoringOnCooldown", PublicInstance);
                _rsGetFavorCooldownLeftMethod = type.GetMethod("GetFavorCooldownLeft", PublicInstance);
                _favoringTypesCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] EnsureFavoringTypes failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a race is currently being favored.
        /// </summary>
        public static bool IsFavored(string raceName)
        {
            EnsureFavoringTypes();
            var resolveService = GetResolveService();
            if (resolveService == null || _rsIsFavoredMethod == null) return false;

            try
            {
                return (bool)_rsIsFavoredMethod.Invoke(resolveService, new object[] { raceName });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Start favoring a race (gives them resolve bonus, penalizes others).
        /// </summary>
        public static bool FavorRace(string raceName)
        {
            EnsureFavoringTypes();
            var resolveService = GetResolveService();
            if (resolveService == null || _rsFavorRaceMethod == null) return false;

            try
            {
                _rsFavorRaceMethod.Invoke(resolveService, new object[] { raceName });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] FavorRace failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop favoring any race.
        /// </summary>
        public static bool StopFavoringRace()
        {
            EnsureFavoringTypes();
            var resolveService = GetResolveService();
            if (resolveService == null || _rsStopFavoringMethod == null) return false;

            try
            {
                _rsStopFavoringMethod.Invoke(resolveService, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] StopFavoringRace failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if favoring is on cooldown.
        /// </summary>
        public static bool IsFavoringOnCooldown()
        {
            EnsureFavoringTypes();
            var resolveService = GetResolveService();
            if (resolveService == null || _rsIsFavoringOnCooldownMethod == null) return false;

            try
            {
                return (bool)_rsIsFavoringOnCooldownMethod.Invoke(resolveService, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get remaining cooldown time for favoring.
        /// </summary>
        public static float GetFavorCooldownLeft()
        {
            EnsureFavoringTypes();
            var resolveService = GetResolveService();
            if (resolveService == null || _rsGetFavorCooldownLeftMethod == null) return 0f;

            try
            {
                return (float)_rsGetFavorCooldownLeftMethod.Invoke(resolveService, null);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get RacesService from GameServices.
        /// Contains race definitions and configurations.
        /// </summary>
        public static object GetRacesService()
        {
            EnsureStatsServiceTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsRacesServiceProperty?.GetValue(gameServices);
            }
            catch
            {
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

        private static void EnsureCalendarTypes()
        {
            if (_calendarTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _calendarTypesCached = true;
                return;
            }

            try
            {
                // Get IGameServices interface for CalendarService property
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get ICalendarService interface for Year, Season, and GetTimeTillNextSeasonChange
                var calendarServiceType = _gameAssembly.GetType("Eremite.Services.ICalendarService");
                if (calendarServiceType != null)
                {
                    _calYearProperty = calendarServiceType.GetProperty("Year",
                        BindingFlags.Public | BindingFlags.Instance);
                    _calSeasonProperty = calendarServiceType.GetProperty("Season",
                        BindingFlags.Public | BindingFlags.Instance);
                    _calGetTimeTillNextSeasonMethod = calendarServiceType.GetMethod("GetTimeTillNextSeasonChange",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached CalendarService types");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CalendarService type caching failed: {ex.Message}");
            }

            _calendarTypesCached = true;
        }

        /// <summary>
        /// Get CalendarService from GameServices.
        /// Contains season, year, and time information.
        /// </summary>
        public static object GetCalendarService()
        {
            EnsureCalendarTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsCalendarServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current settlement year.
        /// </summary>
        public static int GetYear()
        {
            EnsureCalendarTypes();
            var calService = GetCalendarService();
            if (calService == null) return 0;

            try
            {
                return (int)(_calYearProperty?.GetValue(calService) ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the current season as enum int (0=Drizzle, 1=Clearance, 2=Storm).
        /// </summary>
        public static int GetSeason()
        {
            EnsureCalendarTypes();
            var calService = GetCalendarService();
            if (calService == null) return -1;

            try
            {
                var seasonEnum = _calSeasonProperty?.GetValue(calService);
                if (seasonEnum != null)
                {
                    return (int)seasonEnum;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get time remaining until next season change in seconds.
        /// </summary>
        public static float GetTimeTillNextSeason()
        {
            EnsureCalendarTypes();
            var calService = GetCalendarService();
            if (calService == null) return 0f;

            try
            {
                return (float)(_calGetTimeTillNextSeasonMethod?.Invoke(calService, null) ?? 0f);
            }
            catch
            {
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

        private static void EnsureMysteriesTypes()
        {
            if (_mysteriesTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _mysteriesTypesCached = true;
                return;
            }

            try
            {
                // Get StateService property from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsStateServiceProperty = gameServicesType.GetProperty("StateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get SeasonalEffects and Conditions from IStateService
                var stateServiceType = _gameAssembly.GetType("Eremite.Services.IStateService");
                if (stateServiceType != null)
                {
                    _ssSeasonalEffectsProperty = stateServiceType.GetProperty("SeasonalEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                    _ssConditionsProperty = stateServiceType.GetProperty("Conditions",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get effects field from SeasonalEffectsState
                var seasonalEffectsStateType = _gameAssembly.GetType("Eremite.Model.State.SeasonalEffectsState");
                if (seasonalEffectsStateType != null)
                {
                    _seEffectsField = seasonalEffectsStateType.GetField("effects",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get earlyEffects and lateEffects from ConditionsState
                var conditionsStateType = _gameAssembly.GetType("Eremite.Model.State.ConditionsState");
                if (conditionsStateType != null)
                {
                    _condEarlyEffectsField = conditionsStateType.GetField("earlyEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                    _condLateEffectsField = conditionsStateType.GetField("lateEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached mysteries/modifiers types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Mysteries type caching failed: {ex.Message}");
            }

            _mysteriesTypesCached = true;
        }

        private static void EnsureSettingsModelMethods()
        {
            if (_settingsModelMethodsCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _settingsModelMethodsCached = true;
                return;
            }

            try
            {
                // Get Settings type (Eremite.Model.Settings)
                var settingsType = _gameAssembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Settings model methods caching failed: {ex.Message}");
            }

            _settingsModelMethodsCached = true;
        }

        /// <summary>
        /// Get StateService from GameServices.
        /// Contains seasonal effects and conditions state.
        /// </summary>
        public static object GetStateService()
        {
            EnsureMysteriesTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsStateServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get SeasonalEffectsState from StateService.
        /// Contains the effects dictionary.
        /// </summary>
        public static object GetSeasonalEffectsState()
        {
            EnsureMysteriesTypes();
            var stateService = GetStateService();
            if (stateService == null) return null;

            try
            {
                return _ssSeasonalEffectsProperty?.GetValue(stateService);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the seasonal effects dictionary from SeasonalEffectsState.
        /// Returns Dictionary&lt;string, SeasonalEffectState&gt;.
        /// </summary>
        public static System.Collections.IDictionary GetSeasonalEffectsDictionary()
        {
            EnsureMysteriesTypes();
            var seasonalEffectsState = GetSeasonalEffectsState();
            if (seasonalEffectsState == null) return null;

            try
            {
                return _seEffectsField?.GetValue(seasonalEffectsState) as System.Collections.IDictionary;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ConditionsState from StateService.
        /// Contains early and late effects lists.
        /// </summary>
        public static object GetConditionsState()
        {
            EnsureMysteriesTypes();
            var stateService = GetStateService();
            if (stateService == null) return null;

            try
            {
                return _ssConditionsProperty?.GetValue(stateService);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the early effects list from ConditionsState.
        /// These are modifiers applied at embark.
        /// </summary>
        public static List<string> GetEarlyEffects()
        {
            EnsureMysteriesTypes();
            var conditionsState = GetConditionsState();
            if (conditionsState == null) return null;

            try
            {
                return _condEarlyEffectsField?.GetValue(conditionsState) as List<string>;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the late effects list from ConditionsState.
        /// These are modifiers applied at embark.
        /// </summary>
        public static List<string> GetLateEffects()
        {
            EnsureMysteriesTypes();
            var conditionsState = GetConditionsState();
            if (conditionsState == null) return null;

            try
            {
                return _condLateEffectsField?.GetValue(conditionsState) as List<string>;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get SimpleSeasonalEffect model by name from Settings.
        /// </summary>
        public static object GetSimpleSeasonalEffectModel(string name)
        {
            EnsureSettingsModelMethods();
            var settings = GetSettings();
            if (settings == null || _settingsGetSimpleSeasonalEffectMethod == null) return null;

            try
            {
                return _settingsGetSimpleSeasonalEffectMethod.Invoke(settings, new object[] { name });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ConditionalSeasonalEffect model by name from Settings.
        /// </summary>
        public static object GetConditionalSeasonalEffectModel(string name)
        {
            EnsureSettingsModelMethods();
            var settings = GetSettings();
            if (settings == null || _settingsGetConditionalSeasonalEffectMethod == null) return null;

            try
            {
                return _settingsGetConditionalSeasonalEffectMethod.Invoke(settings, new object[] { name });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get Effect model by name from Settings.
        /// Used for world modifiers.
        /// </summary>
        public static object GetEffectModel(string name)
        {
            EnsureSettingsModelMethods();
            var settings = GetSettings();
            if (settings == null || _settingsGetEffectMethod == null) return null;

            try
            {
                return _settingsGetEffectMethod.Invoke(settings, new object[] { name });
            }
            catch
            {
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

        private static void EnsureGoodsTypes()
        {
            if (_goodsTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _goodsTypesCached = true;
                return;
            }

            try
            {
                // Get StorageService property from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsStorageServiceProperty = gameServicesType.GetProperty("StorageService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get GetStorage method from IStorageService
                var storageServiceType = _gameAssembly.GetType("Eremite.Services.IStorageService");
                if (storageServiceType != null)
                {
                    _ssGetStorageMethod = storageServiceType.GetMethod("GetStorage",
                        Type.EmptyTypes); // No parameters version
                }

                // Get Goods property from Storage class
                var storageType = _gameAssembly.GetType("Eremite.Buildings.Storage");
                if (storageType != null)
                {
                    _storageGoodsProperty = storageType.GetProperty("Goods",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get goods field from GoodsCollection
                var goodsCollectionType = _gameAssembly.GetType("Eremite.GoodsCollection");
                if (goodsCollectionType != null)
                {
                    _goodsCollectionGoodsField = goodsCollectionType.GetField("goods",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Goods array from Settings
                var settingsType = _gameAssembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGoodsField = settingsType.GetField("Goods",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached goods/storage types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Goods type caching failed: {ex.Message}");
            }

            _goodsTypesCached = true;
        }

        /// <summary>
        /// Get StorageService from GameServices.
        /// </summary>
        public static object GetStorageService()
        {
            EnsureGoodsTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsStorageServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the main Storage building (contains goods collection).
        /// </summary>
        public static object GetMainStorage()
        {
            EnsureGoodsTypes();
            var storageService = GetStorageService();
            if (storageService == null || _ssGetStorageMethod == null) return null;

            try
            {
                return _ssGetStorageMethod.Invoke(storageService, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all stored goods as a dictionary (goodName -> amount).
        /// Only includes goods with amount > 0.
        /// </summary>
        public static Dictionary<string, int> GetAllStoredGoods()
        {
            EnsureGoodsTypes();
            var storage = GetMainStorage();
            if (storage == null) return new Dictionary<string, int>();

            try
            {
                // Get Goods property (LockedGoodsCollection)
                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return new Dictionary<string, int>();

                // Get the goods dictionary
                var goodsDict = _goodsCollectionGoodsField?.GetValue(goodsCollection) as Dictionary<string, int>;
                if (goodsDict == null) return new Dictionary<string, int>();

                // Filter to only goods with amount > 0
                var result = new Dictionary<string, int>();
                foreach (var kvp in goodsDict)
                {
                    if (kvp.Value > 0)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetAllStoredGoods failed: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Get all GoodModel definitions from Settings.
        /// </summary>
        public static Array GetAllGoodModels()
        {
            EnsureGoodsTypes();
            var settings = GetSettings();
            if (settings == null || _settingsGoodsField == null) return null;

            try
            {
                return _settingsGoodsField.GetValue(settings) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the category of a GoodModel.
        /// </summary>
        public static object GetGoodCategory(object goodModel)
        {
            if (goodModel == null) return null;

            try
            {
                var categoryField = goodModel.GetType().GetField("category",
                    BindingFlags.Public | BindingFlags.Instance);
                return categoryField?.GetValue(goodModel);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a GoodModel or GoodCategoryModel.
        /// Both use displayName.Text pattern.
        /// </summary>
        public static string GetDisplayName(object model)
        {
            if (model == null) return null;

            try
            {
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
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the internal name from a model (SO.Name property).
        /// </summary>
        public static string GetModelName(object model)
        {
            if (model == null) return null;

            try
            {
                var nameProperty = model.GetType().GetProperty("Name",
                    BindingFlags.Public | BindingFlags.Instance);
                return nameProperty?.GetValue(model) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the order field from a model (used for sorting).
        /// </summary>
        public static int GetModelOrder(object model)
        {
            if (model == null) return 0;

            try
            {
                var orderField = model.GetType().GetField("order",
                    BindingFlags.Public | BindingFlags.Instance);
                if (orderField != null)
                {
                    return (int)orderField.GetValue(model);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGoodOrder failed: {ex.Message}"); }
            return 0;
        }

        /// <summary>
        /// Check if a GoodModel is active.
        /// </summary>
        public static bool IsGoodActive(object goodModel)
        {
            if (goodModel == null) return false;

            try
            {
                var isActiveField = goodModel.GetType().GetField("isActive",
                    BindingFlags.Public | BindingFlags.Instance);
                if (isActiveField != null)
                {
                    return (bool)isActiveField.GetValue(goodModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsGoodActive failed: {ex.Message}"); }
            return true; // Default to active
        }

        // ========================================
        // BUILDING SYSTEM REFLECTION
        // ========================================

        private static FieldInfo _settingsBuildingsField = null;
        private static FieldInfo _settingsBuildingCategoriesField = null;
        private static PropertyInfo _gsGameContentServiceProperty = null;
        private static PropertyInfo _gsConstructionServiceProperty = null;
        private static MethodInfo _gcsIsUnlockedMethod = null;
        private static MethodInfo _csCanConstructMethod = null;
        private static Type _buildingCreatorType = null;
        private static MethodInfo _bcCreateBuildingMethod = null;
        private static object _buildingCreatorInstance = null;
        private static bool _buildingTypesCached = false;

        // BuildingModel field caching (used by multiple methods called per-building)
        private static FieldInfo _bmCategoryField = null;
        private static FieldInfo _bmIsInShopField = null;
        private static FieldInfo _bmSizeField = null;
        private static FieldInfo _bmIsActiveField = null;
        private static PropertyInfo _bmDescriptionProperty = null;
        private static FieldInfo _bmDescriptionField = null;
        private static PropertyInfo _locaTextProperty = null;
        private static FieldInfo _bcmIsOnHUDField = null;
        private static FieldInfo _bmRequiredGoodsField = null;
        private static FieldInfo _goodRefAmountField = null;
        private static PropertyInfo _goodRefDisplayNameProperty = null;
        private static bool _bmFieldsCached = false;

        private static void EnsureBuildingTypes()
        {
            if (_buildingTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _buildingTypesCached = true;
                return;
            }

            try
            {
                // Get Buildings and BuildingCategories from Settings
                var settingsType = _gameAssembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsBuildingsField = settingsType.GetField("Buildings",
                        BindingFlags.Public | BindingFlags.Instance);
                    _settingsBuildingCategoriesField = settingsType.GetField("BuildingCategories",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get GameContentService from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsGameContentServiceProperty = gameServicesType.GetProperty("GameContentService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gsConstructionServiceProperty = gameServicesType.GetProperty("ConstructionService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get IsUnlocked method from IGameContentService
                var gameContentServiceType = _gameAssembly.GetType("Eremite.Services.IGameContentService");
                if (gameContentServiceType != null)
                {
                    var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                    if (buildingModelType != null)
                    {
                        _gcsIsUnlockedMethod = gameContentServiceType.GetMethod("IsUnlocked",
                            new Type[] { buildingModelType });
                    }
                }

                // Get CanConstruct method from IConstructionService
                var constructionServiceType = _gameAssembly.GetType("Eremite.Services.IConstructionService");
                if (constructionServiceType != null)
                {
                    var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                    if (buildingModelType != null)
                    {
                        _csCanConstructMethod = constructionServiceType.GetMethod("CanConstruct",
                            new Type[] { buildingModelType });
                    }
                }

                // Get BuildingCreator class
                _buildingCreatorType = _gameAssembly.GetType("Eremite.Buildings.BuildingCreator");
                if (_buildingCreatorType != null)
                {
                    _bcCreateBuildingMethod = _buildingCreatorType.GetMethod("CreateBuilding",
                        new Type[] { _gameAssembly.GetType("Eremite.Buildings.BuildingModel"), typeof(int) });
                }

                Debug.Log("[ATSAccessibility] Cached building system types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Building type caching failed: {ex.Message}");
            }

            _buildingTypesCached = true;
        }

        /// <summary>
        /// Cache BuildingModel and BuildingCategoryModel field info for efficient per-building lookups.
        /// </summary>
        private static void EnsureBuildingModelFields()
        {
            if (_bmFieldsCached) return;
            EnsureBuildingTypes();

            if (_gameAssembly == null)
            {
                _bmFieldsCached = true;
                return;
            }

            try
            {
                // Cache BuildingModel fields
                var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                if (buildingModelType != null)
                {
                    _bmCategoryField = buildingModelType.GetField("category",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmIsInShopField = buildingModelType.GetField("isInShop",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmSizeField = buildingModelType.GetField("size",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmIsActiveField = buildingModelType.GetField("isActive",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmDescriptionProperty = buildingModelType.GetProperty("Description",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmDescriptionField = buildingModelType.GetField("description",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _bmRequiredGoodsField = buildingModelType.GetField("requiredGoods",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache GoodRef fields for building costs
                var goodRefType = _gameAssembly.GetType("Eremite.Model.GoodRef");
                if (goodRefType != null)
                {
                    _goodRefAmountField = goodRefType.GetField("amount",
                        BindingFlags.Public | BindingFlags.Instance);
                    _goodRefDisplayNameProperty = goodRefType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache BuildingCategoryModel fields
                var buildingCategoryModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingCategoryModel");
                if (buildingCategoryModelType != null)
                {
                    _bcmIsOnHUDField = buildingCategoryModelType.GetField("isOnHUD",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache LocaText.Text property (used by description)
                var locaTextType = _gameAssembly.GetType("Eremite.Model.LocaText");
                if (locaTextType != null)
                {
                    _locaTextProperty = locaTextType.GetProperty("Text",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached BuildingModel field info");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingModel field caching failed: {ex.Message}");
            }

            _bmFieldsCached = true;
        }

        /// <summary>
        /// Get all BuildingModel definitions from Settings.
        /// </summary>
        public static Array GetAllBuildingModels()
        {
            EnsureBuildingTypes();
            var settings = GetSettings();
            if (settings == null || _settingsBuildingsField == null) return null;

            try
            {
                return _settingsBuildingsField.GetValue(settings) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all BuildingCategoryModel definitions from Settings.
        /// </summary>
        public static Array GetBuildingCategories()
        {
            EnsureBuildingTypes();
            var settings = GetSettings();
            if (settings == null || _settingsBuildingCategoriesField == null) return null;

            try
            {
                return _settingsBuildingCategoriesField.GetValue(settings) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the category of a BuildingModel.
        /// </summary>
        public static object GetBuildingCategory(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingModelFields();

            try
            {
                return _bmCategoryField?.GetValue(buildingModel);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a building model is in the shop (should show in build menu).
        /// </summary>
        public static bool IsBuildingInShop(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureBuildingModelFields();

            try
            {
                if (_bmIsInShopField != null)
                {
                    return (bool)_bmIsInShopField.GetValue(buildingModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBuildingInShop failed: {ex.Message}"); }
            return true; // Default to true
        }

        /// <summary>
        /// Get the size of a building model.
        /// </summary>
        public static Vector2Int GetBuildingSize(object buildingModel)
        {
            if (buildingModel == null) return Vector2Int.one;
            EnsureBuildingModelFields();

            try
            {
                if (_bmSizeField != null)
                {
                    return (Vector2Int)_bmSizeField.GetValue(buildingModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingSize failed: {ex.Message}"); }
            return Vector2Int.one;
        }

        /// <summary>
        /// Get the description of a building model.
        /// </summary>
        public static string GetBuildingDescription(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingModelFields();

            try
            {
                // Try the Description property first (virtual property in BuildingModel)
                if (_bmDescriptionProperty != null)
                {
                    return _bmDescriptionProperty.GetValue(buildingModel) as string;
                }

                // Fall back to description field (LocaText)
                if (_bmDescriptionField != null)
                {
                    var locaText = _bmDescriptionField.GetValue(buildingModel);
                    if (locaText != null && _locaTextProperty != null)
                    {
                        return _locaTextProperty.GetValue(locaText) as string;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingDescription failed: {ex.Message}"); }
            return null;
        }

        /// <summary>
        /// Get the construction costs of a building model as a formatted string.
        /// Returns format like "2 Wood, 4 Planks" or null if no costs.
        /// </summary>
        public static string GetBuildingCosts(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureConstructionTypes();

            try
            {
                // Use ConstructionService.GetConstructionCostFor to get rate-adjusted costs
                var constructionService = GetConstructionService();
                if (constructionService != null && _csGetConstructionCostForMethod != null &&
                    _goodStructNameField != null && _goodStructAmountField != null)
                {
                    var requiredGoods = _csGetConstructionCostForMethod.Invoke(
                        constructionService, new[] { buildingModel }) as Array;
                    if (requiredGoods != null && requiredGoods.Length > 0)
                    {
                        var storedGoods = GetAllStoredGoods();
                        var costs = new List<string>();
                        foreach (var good in requiredGoods)
                        {
                            if (good == null) continue;
                            string goodName = _goodStructNameField.GetValue(good) as string;
                            int amount = (int)_goodStructAmountField.GetValue(good);
                            if (amount > 0 && !string.IsNullOrEmpty(goodName))
                            {
                                string displayName = GetGoodDisplayName(goodName);
                                int stored = 0;
                                storedGoods.TryGetValue(goodName, out stored);
                                if (stored < amount)
                                    costs.Add($"{amount} {displayName}, not enough");
                                else
                                    costs.Add($"{amount} {displayName}");
                            }
                        }
                        if (costs.Count > 0) return string.Join(", ", costs);
                    }
                }

                // Fallback: read base costs from model if service unavailable
                EnsureBuildingModelFields();
                var rawGoods = _bmRequiredGoodsField?.GetValue(buildingModel) as Array;
                if (rawGoods == null || rawGoods.Length == 0) return null;

                var fallbackCosts = new List<string>();
                foreach (var goodRef in rawGoods)
                {
                    if (goodRef == null) continue;
                    int amount = (int?)_goodRefAmountField?.GetValue(goodRef) ?? 0;
                    string displayName = _goodRefDisplayNameProperty?.GetValue(goodRef) as string;
                    if (amount > 0 && !string.IsNullOrEmpty(displayName))
                        fallbackCosts.Add($"{amount} {displayName}");
                }
                return fallbackCosts.Count > 0 ? string.Join(", ", fallbackCosts) : null;
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingCosts failed: {ex.Message}"); }
            return null;
        }

        /// <summary>
        /// Check if building model is active.
        /// </summary>
        public static bool IsBuildingActive(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureBuildingModelFields();

            try
            {
                if (_bmIsActiveField != null)
                {
                    return (bool)_bmIsActiveField.GetValue(buildingModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsBuildingActive failed: {ex.Message}"); }
            return true;
        }

        /// <summary>
        /// Check if building category is on HUD (should show in categories).
        /// </summary>
        public static bool IsCategoryOnHUD(object categoryModel)
        {
            if (categoryModel == null) return false;
            EnsureBuildingModelFields();

            try
            {
                if (_bcmIsOnHUDField != null)
                {
                    return (bool)_bcmIsOnHUDField.GetValue(categoryModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] IsCategoryOnHUD failed: {ex.Message}"); }
            return true;
        }

        /// <summary>
        /// Get GameContentService from GameServices.
        /// </summary>
        public static object GetGameContentService()
        {
            EnsureBuildingTypes();
            var gameServices = GetGameServices();
            if (gameServices == null || _gsGameContentServiceProperty == null) return null;

            try
            {
                return _gsGameContentServiceProperty.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ConstructionService from GameServices.
        /// </summary>
        public static object GetConstructionService()
        {
            EnsureBuildingTypes();
            var gameServices = GetGameServices();
            if (gameServices == null || _gsConstructionServiceProperty == null) return null;

            try
            {
                return _gsConstructionServiceProperty.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a building is unlocked in the current game.
        /// </summary>
        public static bool IsBuildingUnlocked(object buildingModel)
        {
            EnsureBuildingTypes();
            var gameContentService = GetGameContentService();
            if (gameContentService == null || _gcsIsUnlockedMethod == null || buildingModel == null)
                return false;

            try
            {
                return (bool)_gcsIsUnlockedMethod.Invoke(gameContentService, new object[] { buildingModel });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a building can be constructed (not at max amount).
        /// </summary>
        public static bool CanConstructBuilding(object buildingModel)
        {
            EnsureBuildingTypes();
            var constructionService = GetConstructionService();
            if (constructionService == null || _csCanConstructMethod == null || buildingModel == null)
                return false;

            try
            {
                return (bool)_csCanConstructMethod.Invoke(constructionService, new object[] { buildingModel });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a building instance using BuildingCreator.
        /// The building is not yet placed on the grid.
        /// </summary>
        public static object CreateBuilding(object buildingModel, int rotation = 0)
        {
            EnsureBuildingTypes();
            if (_buildingCreatorType == null || _bcCreateBuildingMethod == null || buildingModel == null)
                return null;

            try
            {
                // Reuse cached BuildingCreator instance (stateless)
                if (_buildingCreatorInstance == null)
                    _buildingCreatorInstance = Activator.CreateInstance(_buildingCreatorType);

                return _bcCreateBuildingMethod.Invoke(_buildingCreatorInstance, new object[] { buildingModel, rotation });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CreateBuilding failed: {ex.Message}");
                return null;
            }
        }

        // Building placement reflection
        private static MethodInfo _csCanPlaceOnGridMethod = null;
        private static MethodInfo _csPlaceOnGridMethod = null;
        private static MethodInfo _csRemoveFromGridMethod = null;
        private static MethodInfo _buildingManualPlacingFinishedMethod = null;
        private static MethodInfo _buildingRemoveMethod = null;
        // Note: _buildingFieldProperty is already defined above for Ancient Hearth
        private static PropertyInfo _buildingRotationProperty = null;
        private static MethodInfo _buildingSetPositionMethod = null;
        private static MethodInfo _buildingRotateMethod = null;
        private static bool _buildingPlacementTypesCached = false;

        private static void EnsureBuildingPlacementTypes()
        {
            if (_buildingPlacementTypesCached) return;
            EnsureBuildingTypes();

            if (_gameAssembly == null)
            {
                _buildingPlacementTypesCached = true;
                return;
            }

            try
            {
                // Get ConstructionService methods
                var constructionServiceType = _gameAssembly.GetType("Eremite.Services.IConstructionService");
                var buildingType = _gameAssembly.GetType("Eremite.Buildings.Building");

                if (constructionServiceType != null && buildingType != null)
                {
                    _csCanPlaceOnGridMethod = constructionServiceType.GetMethod("CanPlaceOnGrid",
                        new Type[] { buildingType });
                    _csPlaceOnGridMethod = constructionServiceType.GetMethod("PlaceOnGrid",
                        new Type[] { buildingType });
                    _csRemoveFromGridMethod = constructionServiceType.GetMethod("RemoveFromGrid",
                        new Type[] { buildingType });
                }

                if (buildingType != null)
                {
                    // Get Building methods and properties
                    _buildingManualPlacingFinishedMethod = buildingType.GetMethod("ManualPlacingFinished",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingRemoveMethod = buildingType.GetMethod("Remove",
                        new Type[] { typeof(bool) });
                    // _buildingFieldProperty is cached elsewhere (Ancient Hearth section)
                    _buildingRotationProperty = buildingType.GetProperty("Rotation",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingSetPositionMethod = buildingType.GetMethod("SetPosition",
                        new Type[] { typeof(Vector3) });
                    _buildingRotateMethod = buildingType.GetMethod("Rotate",
                        new Type[] { typeof(int) });
                }

                Debug.Log("[ATSAccessibility] Cached building placement types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Building placement type caching failed: {ex.Message}");
            }

            _buildingPlacementTypesCached = true;
        }

        /// <summary>
        /// Check if a building can be placed at its current position.
        /// </summary>
        public static bool CanPlaceBuilding(object building)
        {
            EnsureBuildingPlacementTypes();
            var constructionService = GetConstructionService();
            if (constructionService == null || _csCanPlaceOnGridMethod == null || building == null)
                return false;

            try
            {
                return (bool)_csCanPlaceOnGridMethod.Invoke(constructionService, new object[] { building });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Set a building's position.
        /// </summary>
        public static void SetBuildingPosition(object building, Vector2Int gridPos)
        {
            EnsureBuildingPlacementTypes();
            if (building == null || _buildingSetPositionMethod == null) return;

            try
            {
                // Convert grid position to world position
                Vector3 worldPos = new Vector3(gridPos.x, 0, gridPos.y);
                _buildingSetPositionMethod.Invoke(building, new object[] { worldPos });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetBuildingPosition failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Rotate a building to a specific rotation value (0-3).
        /// </summary>
        public static void RotateBuilding(object building, int rotation)
        {
            EnsureBuildingPlacementTypes();
            if (building == null || _buildingRotateMethod == null) return;

            try
            {
                _buildingRotateMethod.Invoke(building, new object[] { rotation });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RotateBuilding failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current rotation of a building (0-3).
        /// </summary>
        public static int GetBuildingRotation(object building)
        {
            EnsureBuildingPlacementTypes();
            if (building == null || _buildingRotationProperty == null) return 0;

            try
            {
                return (int)_buildingRotationProperty.GetValue(building);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Finalize building placement after setting position.
        /// This registers the building, plays sounds, and starts construction.
        /// </summary>
        public static void FinalizeBuildingPlacement(object building)
        {
            EnsureBuildingPlacementTypes();
            if (building == null || _buildingManualPlacingFinishedMethod == null) return;

            try
            {
                _buildingManualPlacingFinishedMethod.Invoke(building, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] FinalizeBuildingPlacement failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Remove a building from the game.
        /// </summary>
        public static void RemoveBuilding(object building, bool refund = true)
        {
            EnsureBuildingPlacementTypes();
            if (building == null || _buildingRemoveMethod == null) return;

            try
            {
                _buildingRemoveMethod.Invoke(building, new object[] { refund });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RemoveBuilding failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get building at a specific map position.
        /// Returns null if no building at that position.
        /// </summary>
        public static object GetBuildingAtPosition(int x, int y)
        {
            var obj = GetObjectOn(x, y);
            if (obj == null) return null;

            // Check if it's a Building type
            var buildingType = _gameAssembly?.GetType("Eremite.Buildings.Building");
            if (buildingType != null && buildingType.IsInstanceOfType(obj))
            {
                return obj;
            }

            return null;
        }

        /// <summary>
        /// Check if a building is unfinished (still under construction).
        /// </summary>
        public static bool IsBuildingUnfinished(object building)
        {
            if (building == null) return false;

            try
            {
                // Get BuildingState property
                var stateProperty = building.GetType().GetProperty("BuildingState",
                    BindingFlags.Public | BindingFlags.Instance);
                if (stateProperty == null) return false;

                var state = stateProperty.GetValue(building);
                if (state == null) return false;

                // Get finished field from state
                var finishedField = state.GetType().GetField("finished",
                    BindingFlags.Public | BindingFlags.Instance);
                if (finishedField == null) return false;

                return !(bool)finishedField.GetValue(state);
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // CONSTRUCTION PROGRESS REFLECTION
        // ========================================

        private static FieldInfo _buildingProgressField = null;
        private static FieldInfo _deliveredGoodsField = null;
        private static FieldInfo _constructionGoodsField = null;  // goods dict on GoodsCollection base
        private static MethodInfo _csGetConstructionCostForMethod = null;
        private static FieldInfo _goodStructNameField = null;
        private static FieldInfo _goodStructAmountField = null;
        private static bool _constructionTypesCached = false;

        private static void EnsureConstructionTypes()
        {
            if (_constructionTypesCached) return;

            if (_gameAssembly == null)
            {
                _constructionTypesCached = true;
                return;
            }

            try
            {
                // BuildingState fields
                var buildingStateType = _gameAssembly.GetType("Eremite.Buildings.BuildingState");
                if (buildingStateType != null)
                {
                    _buildingProgressField = buildingStateType.GetField("buildingProgress", PublicInstance);
                    _deliveredGoodsField = buildingStateType.GetField("deliveredGoods", PublicInstance);
                }

                // GoodsCollection.goods (public, base class) for delivered amounts
                var goodsCollectionType = _gameAssembly.GetType("Eremite.GoodsCollection");
                if (goodsCollectionType != null)
                {
                    _constructionGoodsField = goodsCollectionType.GetField("goods", PublicInstance);
                }

                // ConstructionService.GetConstructionCostFor(BuildingModel) for required amounts
                var constructionServiceType = _gameAssembly.GetType("Eremite.Services.IConstructionService");
                var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                if (constructionServiceType != null && buildingModelType != null)
                {
                    _csGetConstructionCostForMethod = constructionServiceType.GetMethod("GetConstructionCostFor",
                        new Type[] { buildingModelType });
                }

                // Good struct fields (name, amount)
                var goodType = _gameAssembly.GetType("Eremite.Model.Good");
                if (goodType != null)
                {
                    _goodStructNameField = goodType.GetField("name", PublicInstance);
                    _goodStructAmountField = goodType.GetField("amount", PublicInstance);
                }

                Debug.Log("[ATSAccessibility] Cached construction types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Construction type caching failed: {ex.Message}");
            }

            _constructionTypesCached = true;
        }

        /// <summary>
        /// Get building construction progress (0-1 float).
        /// </summary>
        public static float GetBuildingProgress(object building)
        {
            if (building == null) return 0f;
            EnsureConstructionTypes();

            try
            {
                var stateProperty = building.GetType().GetProperty("BuildingState", PublicInstance);
                if (stateProperty == null) return 0f;

                var state = stateProperty.GetValue(building);
                if (state == null || _buildingProgressField == null) return 0f;

                return (float)_buildingProgressField.GetValue(state);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get construction materials with delivered and required amounts.
        /// Uses ConstructionService.GetConstructionCostFor (same as game UI) for required amounts.
        /// Returns list of (displayName, delivered, required).
        /// </summary>
        public static List<(string name, int delivered, int required)> GetConstructionMaterials(object building)
        {
            if (building == null) return null;
            EnsureConstructionTypes();

            try
            {
                // Get required amounts from ConstructionService (matches game UI)
                var buildingModel = GetBuildingModel(building);
                var constructionService = GetConstructionService();
                if (buildingModel == null || constructionService == null ||
                    _csGetConstructionCostForMethod == null ||
                    _goodStructNameField == null || _goodStructAmountField == null)
                    return null;

                var requiredGoods = _csGetConstructionCostForMethod.Invoke(
                    constructionService, new[] { buildingModel }) as Array;
                if (requiredGoods == null || requiredGoods.Length == 0) return null;

                // Get delivered amounts from BuildingState.deliveredGoods.goods dict
                Dictionary<string, int> deliveredDict = null;
                var stateProperty = building.GetType().GetProperty("BuildingState", PublicInstance);
                if (stateProperty != null)
                {
                    var state = stateProperty.GetValue(building);
                    if (state != null && _deliveredGoodsField != null)
                    {
                        var deliveredGoods = _deliveredGoodsField.GetValue(state);
                        if (deliveredGoods != null && _constructionGoodsField != null)
                        {
                            deliveredDict = _constructionGoodsField.GetValue(deliveredGoods)
                                as Dictionary<string, int>;
                        }
                    }
                }

                var result = new List<(string name, int delivered, int required)>();
                foreach (var good in requiredGoods)
                {
                    if (good == null) continue;

                    string goodName = _goodStructNameField.GetValue(good) as string;
                    int required = (int)_goodStructAmountField.GetValue(good);
                    if (string.IsNullOrEmpty(goodName) || required <= 0) continue;

                    int delivered = 0;
                    if (deliveredDict != null && deliveredDict.ContainsKey(goodName))
                        delivered = deliveredDict[goodName];

                    string displayName = GetGoodDisplayName(goodName);
                    result.Add((displayName, delivered, required));
                }

                return result.Count > 0 ? result : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetConstructionMaterials failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if an object from GetObjectOn is a Building (not a resource or field).
        /// </summary>
        public static bool IsBuilding(object obj)
        {
            if (obj == null) return false;

            var buildingType = _gameAssembly?.GetType("Eremite.Buildings.Building");
            return buildingType != null && buildingType.IsInstanceOfType(obj);
        }

        /// <summary>
        /// Check if a building is a relic/ruin (its model is a RelicModel).
        /// Relics are special buildings created when regular buildings are destroyed/ruined,
        /// or glade events that need to be investigated.
        /// </summary>
        public static bool IsRelic(object building)
        {
            if (building == null) return false;

            try
            {
                // Get BuildingModel property from the building
                var buildingModelProp = building.GetType().GetProperty("BuildingModel",
                    BindingFlags.Public | BindingFlags.Instance);
                if (buildingModelProp == null) return false;

                var model = buildingModelProp.GetValue(building);
                if (model == null) return false;

                // Check if the model is a RelicModel
                var relicModelType = _gameAssembly?.GetType("Eremite.Buildings.RelicModel");
                return relicModelType != null && relicModelType.IsInstanceOfType(model);
            }
            catch
            {
                return false;
            }
        }

        // Cached reflection for PickBuilding
        private static PropertyInfo _modeServiceProperty = null;
        private static PropertyInfo _destructionModeProperty = null;
        private static PropertyInfo _harvestModeProperty = null;
        private static MethodInfo _buildingPickMethod = null;
        private static bool _pickBuildingCached = false;

        /// <summary>
        /// Pick/select a building to open its panel.
        /// Returns true if successful, false if in a mode that prevents picking
        /// or if the building cannot be picked.
        /// </summary>
        public static bool PickBuilding(object building)
        {
            if (building == null) return false;
            if (!IsBuilding(building)) return false;

            try
            {
                // Cache reflection info
                if (!_pickBuildingCached)
                {
                    CachePickBuildingReflection();
                }

                // Check if in destruction mode or harvest mode (don't pick in these modes)
                if (IsInDestructionMode() || IsInHarvestMode())
                {
                    Debug.Log("[ATSAccessibility] Cannot pick building: in destruction or harvest mode");
                    return false;
                }

                // Get or cache the Pick method
                if (_buildingPickMethod == null)
                {
                    var buildingType = _gameAssembly?.GetType("Eremite.Buildings.Building");
                    if (buildingType != null)
                    {
                        _buildingPickMethod = buildingType.GetMethod("Pick",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }

                if (_buildingPickMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] Could not find Building.Pick method");
                    return false;
                }

                // Call Pick() on the building
                _buildingPickMethod.Invoke(building, null);
                Debug.Log("[ATSAccessibility] Picked building successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PickBuilding failed: {ex.Message}");
                return false;
            }
        }

        private static void CachePickBuildingReflection()
        {
            try
            {
                // Get ModeService from GameServices
                var gameServicesType = _gameAssembly?.GetType("Eremite.Services.GameServices");
                if (gameServicesType != null)
                {
                    _modeServiceProperty = gameServicesType.GetProperty("ModeService",
                        BindingFlags.Public | BindingFlags.Static);
                }

                // Get mode properties from ModeService type
                var modeServiceType = _gameAssembly?.GetType("Eremite.Services.ModeService");
                if (modeServiceType != null)
                {
                    _destructionModeProperty = modeServiceType.GetProperty("BuildingDestructionMode",
                        BindingFlags.Public | BindingFlags.Instance);
                    _harvestModeProperty = modeServiceType.GetProperty("HarvestMode",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                _pickBuildingCached = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CachePickBuildingReflection failed: {ex.Message}");
                _pickBuildingCached = true; // Don't retry
            }
        }

        private static bool IsInDestructionMode()
        {
            try
            {
                if (_modeServiceProperty == null) return false;

                var modeService = _modeServiceProperty.GetValue(null);
                if (modeService == null || _destructionModeProperty == null) return false;

                var destructionMode = _destructionModeProperty.GetValue(modeService);
                if (destructionMode == null) return false;

                // It's a ReactiveProperty<bool>, get the Value
                var valueProperty = destructionMode.GetType().GetProperty("Value");
                if (valueProperty == null) return false;

                return (bool)valueProperty.GetValue(destructionMode);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsInHarvestMode()
        {
            try
            {
                if (_modeServiceProperty == null) return false;

                var modeService = _modeServiceProperty.GetValue(null);
                if (modeService == null || _harvestModeProperty == null) return false;

                var harvestMode = _harvestModeProperty.GetValue(modeService);
                if (harvestMode == null) return false;

                // It's a ReactiveProperty<bool>, get the Value
                var valueProperty = harvestMode.GetType().GetProperty("Value");
                if (valueProperty == null) return false;

                return (bool)valueProperty.GetValue(harvestMode);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the entrance tile coordinates for a building.
        /// Returns null if the building has no entrance or if it can't be determined.
        /// </summary>
        public static Vector2Int? GetBuildingEntranceTile(object building)
        {
            if (building == null) return null;

            try
            {
                // Get Entrance property (Vector3 world position)
                var entranceProperty = building.GetType().GetProperty("Entrance",
                    BindingFlags.Public | BindingFlags.Instance);
                if (entranceProperty == null) return null;

                var entrancePos = (Vector3)entranceProperty.GetValue(building);

                // Convert world position to tile coordinates
                return new Vector2Int(
                    Mathf.FloorToInt(entrancePos.x),
                    Mathf.FloorToInt(entrancePos.z)
                );
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a building should show its entrance (has meaningful entrance for gameplay).
        /// </summary>
        public static bool GetBuildingShouldShowEntrance(object building)
        {
            if (building == null) return false;

            try
            {
                // ShouldShowEntrance is a protected virtual property
                var shouldShowProp = building.GetType().GetProperty("ShouldShowEntrance",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (shouldShowProp != null)
                {
                    return (bool)shouldShowProp.GetValue(building);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingShouldShowEntrance failed: {ex.Message}"); }

            return false;
        }

        /// <summary>
        /// Check if a building instance can be rotated.
        /// </summary>
        public static bool CanRotateBuilding(object building)
        {
            if (building == null) return false;

            try
            {
                // Get BuildingModel property
                var modelProp = building.GetType().GetProperty("BuildingModel",
                    BindingFlags.Public | BindingFlags.Instance);
                if (modelProp == null) return false;

                var model = modelProp.GetValue(building);
                return CanRotateBuildingModel(model);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] CanRotateBuilding failed: {ex.Message}"); }

            return false;
        }

        /// <summary>
        /// Check if a building model allows rotation.
        /// </summary>
        public static bool CanRotateBuildingModel(object buildingModel)
        {
            if (buildingModel == null) return false;

            try
            {
                // Get canRotate field from model
                var canRotateField = buildingModel.GetType().GetField("canRotate",
                    BindingFlags.Public | BindingFlags.Instance);
                if (canRotateField != null)
                {
                    return (bool)canRotateField.GetValue(buildingModel);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] CanRotateBuildingModel failed: {ex.Message}"); }

            return false;
        }

        /// <summary>
        /// Check if a building can be moved (required for rotation).
        /// </summary>
        public static bool CanMovePlacedBuilding(object building)
        {
            if (building == null) return false;

            try
            {
                var constructionService = GetConstructionService();
                if (constructionService == null) return false;

                // Get CanBeMoved method (takes Building parameter)
                var canMoveMethod = constructionService.GetType().GetMethod("CanBeMoved",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { building.GetType() }, null);

                // Try with base Building type if exact type doesn't match
                if (canMoveMethod == null)
                {
                    var buildingType = _gameAssembly?.GetType("Eremite.Buildings.Building");
                    if (buildingType != null)
                    {
                        canMoveMethod = constructionService.GetType().GetMethod("CanBeMoved",
                            BindingFlags.Public | BindingFlags.Instance,
                            null, new Type[] { buildingType }, null);
                    }
                }

                if (canMoveMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] CanBeMoved method not found");
                    return false; // Don't allow if method not found
                }

                return (bool)canMoveMethod.Invoke(constructionService, new object[] { building });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CanMovePlacedBuilding failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if moving this building has a resource cost.
        /// </summary>
        public static bool HasMovingCost(object building)
        {
            if (building == null) return false;
            try
            {
                var constructionService = GetConstructionService();
                if (constructionService == null) return false;

                var method = constructionService.GetType().GetMethod("HasMovingCost",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null) return false;

                return (bool)method.Invoke(constructionService, new object[] { building });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] HasMovingCost failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the player can afford to move this building.
        /// </summary>
        public static bool CanAffordMove(object building)
        {
            if (building == null) return false;
            try
            {
                var constructionService = GetConstructionService();
                if (constructionService == null) return true;

                var method = constructionService.GetType().GetMethod("CanAffordMove",
                    BindingFlags.Public | BindingFlags.Instance);
                if (method == null) return true;

                return (bool)method.Invoke(constructionService, new object[] { building });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CanAffordMove failed: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Get the moving cost display name and amount for a building.
        /// Returns null if no cost.
        /// </summary>
        public static (string displayName, int amount)? GetMovingCostInfo(object building)
        {
            if (building == null) return null;
            try
            {
                var model = GetBuildingModel(building);
                if (model == null) return null;

                var movingCostField = model.GetType().GetField("movingCost",
                    BindingFlags.Public | BindingFlags.Instance);
                if (movingCostField == null) return null;

                var goodRef = movingCostField.GetValue(model);
                if (goodRef == null) return null;

                var amountField = goodRef.GetType().GetField("amount", BindingFlags.Public | BindingFlags.Instance);
                int amount = (int)(amountField?.GetValue(goodRef) ?? 0);
                if (amount <= 0) return null;

                var displayNameProp = goodRef.GetType().GetProperty("DisplayName",
                    BindingFlags.Public | BindingFlags.Instance);
                string displayName = displayNameProp?.GetValue(goodRef) as string ?? "Unknown";

                return (displayName, amount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetMovingCostInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pay the moving cost for a building. Call before moving.
        /// Returns true if cost was paid (or no cost needed).
        /// </summary>
        public static bool PayForMoving(object building)
        {
            if (building == null) return false;
            if (!HasMovingCost(building)) return true;  // No cost, success

            try
            {
                var model = GetBuildingModel(building);
                if (model == null) return false;

                var movingCostField = model.GetType().GetField("movingCost",
                    BindingFlags.Public | BindingFlags.Instance);
                var goodRef = movingCostField?.GetValue(model);
                if (goodRef == null) return false;

                // Get Good via ToGood()
                var toGoodMethod = goodRef.GetType().GetMethod("ToGood", BindingFlags.Public | BindingFlags.Instance);
                if (toGoodMethod == null) return false;
                object good = toGoodMethod.Invoke(goodRef, null);

                // Get StorageService
                var storageService = GetStorageService();
                if (storageService == null) return false;

                // Get StorageOperationType.BuildingMove enum value
                var opType = _gameAssembly.GetType("Eremite.Model.StorageOperationType");
                if (opType == null) return false;
                object buildingMoveValue = Enum.Parse(opType, "BuildingMove");

                // Call Remove(Good, StorageOperationType)
                var goodType = good.GetType();
                var removeMethod = storageService.GetType().GetMethod("Remove",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { goodType, opType }, null);
                if (removeMethod == null) return false;

                removeMethod.Invoke(storageService, new object[] { good, buildingMoveValue });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PayForMoving failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refund the moving cost for a building. Call on cancel.
        /// </summary>
        public static void RefundMoving(object building)
        {
            if (building == null) return;

            try
            {
                var model = GetBuildingModel(building);
                if (model == null) return;

                var movingCostField = model.GetType().GetField("movingCost",
                    BindingFlags.Public | BindingFlags.Instance);
                var goodRef = movingCostField?.GetValue(model);
                if (goodRef == null) return;

                var toGoodMethod = goodRef.GetType().GetMethod("ToGood", BindingFlags.Public | BindingFlags.Instance);
                if (toGoodMethod == null) return;
                object good = toGoodMethod.Invoke(goodRef, null);

                var storageService = GetStorageService();
                if (storageService == null) return;

                var opType = _gameAssembly.GetType("Eremite.Model.StorageOperationType");
                if (opType == null) return;
                object buildingRefundValue = Enum.Parse(opType, "BuildingRefund");

                var goodType = good.GetType();
                var storeMethod = storageService.GetType().GetMethod("Store",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new Type[] { goodType, opType }, null);

                storeMethod?.Invoke(storageService, new object[] { good, buildingRefundValue });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RefundMoving failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a placed building can be rotated in place.
        /// Uses the game's ConstructionService.CanBeRotatedInPlace check.
        /// </summary>
        public static bool CanRotatePlacedBuilding(object building)
        {
            if (building == null) return false;

            try
            {
                var constructionService = GetConstructionService();
                if (constructionService == null) return false;

                // Get CanBeRotatedInPlace method
                var canRotateMethod = constructionService.GetType().GetMethod("CanBeRotatedInPlace",
                    BindingFlags.Public | BindingFlags.Instance);
                if (canRotateMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] CanBeRotatedInPlace method not found");
                    return false; // Don't allow if method not found
                }

                return (bool)canRotateMethod.Invoke(constructionService, new object[] { building });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CanRotatePlacedBuilding failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rotate a placed building and return the new rotation (0-3).
        /// Properly updates the map grid by removing and re-placing the building.
        /// Call CanMovePlacedBuilding and CanRotatePlacedBuilding first to check validity.
        /// Returns -1 if rotation failed.
        /// </summary>
        public static int RotatePlacedBuilding(object building)
        {
            if (building == null) return -1;

            try
            {
                // Get MapService for grid operations
                var mapService = GetMapService();
                if (mapService == null)
                {
                    Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: MapService not found");
                    return -1;
                }

                // Get RemoveFromGrid and PlaceOnGrid methods
                var removeMethod = mapService.GetType().GetMethod("RemoveFromGrid",
                    BindingFlags.Public | BindingFlags.Instance);
                var placeMethod = mapService.GetType().GetMethod("PlaceOnGrid",
                    BindingFlags.Public | BindingFlags.Instance);

                if (removeMethod == null || placeMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: Grid methods not found");
                    return -1;
                }

                // Get the Rotate method
                var rotateMethod = building.GetType().GetMethod("Rotate",
                    BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (rotateMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] RotatePlacedBuilding: Rotate method not found");
                    return -1;
                }

                // 1. Remove from grid (clears old footprint)
                removeMethod.Invoke(mapService, new object[] { building });

                // 2. Rotate the building
                rotateMethod.Invoke(building, null);

                // 3. Re-place on grid (sets new footprint)
                placeMethod.Invoke(mapService, new object[] { building });

                // Get the new rotation value
                var rotationProp = building.GetType().GetProperty("Rotation",
                    BindingFlags.Public | BindingFlags.Instance);
                if (rotationProp != null)
                {
                    return (int)rotationProp.GetValue(building);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RotatePlacedBuilding failed: {ex.Message}");
            }

            return -1;
        }

        /// <summary>
        /// Get a building's grid position.
        /// Returns the building's Field property as Vector2Int.
        /// </summary>
        public static Vector2Int GetBuildingGridPosition(object building)
        {
            if (building == null) return Vector2Int.zero;

            try
            {
                // _buildingFieldProperty may already be cached from Ancient Hearth code
                if (_buildingFieldProperty == null)
                {
                    _buildingFieldProperty = building.GetType().GetProperty("Field",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_buildingFieldProperty != null)
                {
                    var field = _buildingFieldProperty.GetValue(building);
                    if (field is Vector2Int pos)
                    {
                        return pos;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetBuildingGridPosition failed: {ex.Message}");
            }

            return Vector2Int.zero;
        }

        /// <summary>
        /// Get the building model (template) from a placed building instance.
        /// Returns the BuildingModel that was used to create this building.
        /// </summary>
        public static object GetBuildingModel(object building)
        {
            if (building == null) return null;

            try
            {
                // Building.BuildingModel property returns the BuildingModel
                var modelProperty = building.GetType().GetProperty("BuildingModel",
                    BindingFlags.Public | BindingFlags.Instance);

                if (modelProperty != null)
                {
                    return modelProperty.GetValue(building);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetBuildingModel failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Lift a building from the map grid without destroying it.
        /// This removes the building's footprint from the grid but keeps the object.
        /// Call PlaceBuildingOnGrid to put it back.
        /// </summary>
        public static void LiftBuilding(object building)
        {
            if (building == null) return;

            try
            {
                var mapService = GetMapService();
                if (mapService == null)
                {
                    Debug.LogError("[ATSAccessibility] LiftBuilding: MapService not found");
                    return;
                }

                var removeMethod = mapService.GetType().GetMethod("RemoveFromGrid",
                    BindingFlags.Public | BindingFlags.Instance);

                if (removeMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] LiftBuilding: RemoveFromGrid method not found");
                    return;
                }

                removeMethod.Invoke(mapService, new object[] { building });
                Debug.Log("[ATSAccessibility] Building lifted from grid");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] LiftBuilding failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Place a building on the map grid at its current position.
        /// Use after LiftBuilding and SetBuildingPosition to move a building.
        /// </summary>
        public static void PlaceBuildingOnGrid(object building)
        {
            if (building == null) return;

            try
            {
                var mapService = GetMapService();
                if (mapService == null)
                {
                    Debug.LogError("[ATSAccessibility] PlaceBuildingOnGrid: MapService not found");
                    return;
                }

                var placeMethod = mapService.GetType().GetMethod("PlaceOnGrid",
                    BindingFlags.Public | BindingFlags.Instance);

                if (placeMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] PlaceBuildingOnGrid: PlaceOnGrid method not found");
                    return;
                }

                placeMethod.Invoke(mapService, new object[] { building });
                Debug.Log("[ATSAccessibility] Building placed on grid");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PlaceBuildingOnGrid failed: {ex.Message}");
            }
        }

        // ========================================
        // MENU HUB - POPUP OPENING METHODS
        // ========================================

        // Cached reflection metadata for GameBlackboardService
        private static PropertyInfo _gsGameBlackboardServiceProperty = null;
        private static bool _gameBlackboardTypesInitialized = false;

        private static void EnsureGameBlackboardTypes()
        {
            if (_gameBlackboardTypesInitialized) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _gameBlackboardTypesInitialized = true;
                return;
            }

            try
            {
                // Get IGameServices interface for GameBlackboardService property
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsGameBlackboardServiceProperty = gameServicesType.GetProperty("GameBlackboardService",
                        BindingFlags.Public | BindingFlags.Instance);
                    Debug.Log("[ATSAccessibility] Cached GameBlackboardService property");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GameBlackboard type caching failed: {ex.Message}");
            }

            _gameBlackboardTypesInitialized = true;
        }

        /// <summary>
        /// Get GameBlackboardService from GameServices.
        /// </summary>
        public static object GetGameBlackboardService()
        {
            EnsureGameBlackboardTypes();

            var gameServices = GetGameServices();
            if (gameServices == null || _gsGameBlackboardServiceProperty == null) return null;

            try
            {
                return _gsGameBlackboardServiceProperty.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        // Cached Unit.Default value
        private static object _unitDefault = null;
        private static bool _unitDefaultCached = false;

        /// <summary>
        /// Get UniRx.Unit.Default value for Subject&lt;Unit&gt; OnNext calls.
        /// </summary>
        public static object GetUnitDefault()
        {
            if (_unitDefaultCached) return _unitDefault;

            try
            {
                Type unitType = Type.GetType("UniRx.Unit, UniRx");
                if (unitType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        unitType = assembly.GetType("UniRx.Unit");
                        if (unitType != null) break;
                    }
                }

                if (unitType != null)
                {
                    // Try as a field first
                    var defaultField = unitType.GetField("Default", BindingFlags.Public | BindingFlags.Static);
                    if (defaultField != null)
                    {
                        _unitDefault = defaultField.GetValue(null);
                    }
                    else
                    {
                        // Try as a property
                        var defaultProperty = unitType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                        if (defaultProperty != null)
                        {
                            _unitDefault = defaultProperty.GetValue(null);
                        }
                        else
                        {
                            // Unit is a struct - default(Unit) works, so create an instance
                            _unitDefault = Activator.CreateInstance(unitType);
                        }
                    }
                }

                _unitDefaultCached = true;
                if (_unitDefault == null)
                {
                    Debug.LogWarning("[ATSAccessibility] Could not get UniRx.Unit.Default - type not found");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetUnitDefault failed: {ex.Message}");
                _unitDefaultCached = true;
            }

            return _unitDefault;
        }

        /// <summary>
        /// Helper to invoke OnNext on a UniRx Subject property.
        /// </summary>
        public static bool InvokeSubjectOnNext(object blackboardService, string subjectPropertyName, object parameter)
        {
            if (blackboardService == null) return false;

            try
            {
                var subjectProperty = blackboardService.GetType().GetProperty(subjectPropertyName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (subjectProperty == null)
                {
                    Debug.LogWarning($"[ATSAccessibility] Subject property '{subjectPropertyName}' not found");
                    return false;
                }

                var subject = subjectProperty.GetValue(blackboardService);
                if (subject == null)
                {
                    Debug.LogWarning($"[ATSAccessibility] Subject '{subjectPropertyName}' is null");
                    return false;
                }

                var onNextMethod = subject.GetType().GetMethod("OnNext",
                    BindingFlags.Public | BindingFlags.Instance);
                if (onNextMethod == null)
                {
                    Debug.LogWarning($"[ATSAccessibility] OnNext method not found on subject");
                    return false;
                }

                onNextMethod.Invoke(subject, new object[] { parameter });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] InvokeSubjectOnNext failed for {subjectPropertyName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the Recipes popup via GameBlackboardService.RecipesPopupRequested.
        /// </summary>
        public static bool OpenRecipesPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenRecipesPopup: GameBlackboardService not available");
                return false;
            }

            try
            {
                // Create RecipesPopupRequest(true) for playShowAnim
                var requestType = _gameAssembly?.GetType("Eremite.View.Popups.Recipes.RecipesPopupRequest");
                if (requestType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] RecipesPopupRequest type not found");
                    return false;
                }

                // Constructor: RecipesPopupRequest(bool playShowAnim)
                var constructor = requestType.GetConstructor(new[] { typeof(bool) });
                if (constructor == null)
                {
                    Debug.LogWarning("[ATSAccessibility] RecipesPopupRequest constructor not found");
                    return false;
                }

                var request = constructor.Invoke(new object[] { true });
                return InvokeSubjectOnNext(blackboardService, "RecipesPopupRequested", request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenRecipesPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the Orders popup via GameBlackboardService.OrdersPopupRequested.
        /// </summary>
        public static bool OpenOrdersPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenOrdersPopup: GameBlackboardService not available");
                return false;
            }

            return InvokeSubjectOnNext(blackboardService, "OrdersPopupRequested", true);
        }

        /// <summary>
        /// Open the Trade Routes popup via GameBlackboardService.TradeRoutesPopupRequested.
        /// </summary>
        public static bool OpenTradeRoutesPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenTradeRoutesPopup: GameBlackboardService not available");
                return false;
            }

            return InvokeSubjectOnNext(blackboardService, "TradeRoutesPopupRequested", true);
        }

        /// <summary>
        /// Open the Consumption Control popup via GameBlackboardService.ConsumptionPopupRequested.
        /// </summary>
        public static bool OpenConsumptionPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenConsumptionPopup: GameBlackboardService not available");
                return false;
            }

            return InvokeSubjectOnNext(blackboardService, "ConsumptionPopupRequested", true);
        }

        /// <summary>
        /// Open the Payments popup via GameBlackboardService.PaymentsPopupRequested.
        /// </summary>
        public static bool OpenPaymentsPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenPaymentsPopup: GameBlackboardService not available");
                return false;
            }

            return InvokeSubjectOnNext(blackboardService, "PaymentsPopupRequested", true);
        }

        /// <summary>
        /// Open the Trends popup via GameBlackboardService.TrendsPopupRequested.
        /// </summary>
        public static bool OpenTrendsPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenTrendsPopup: GameBlackboardService not available");
                return false;
            }

            return InvokeSubjectOnNext(blackboardService, "TrendsPopupRequested", true);
        }


        /// <summary>
        /// Open the Trader panel via TraderPanel.Instance.Show().
        /// </summary>
        public static bool OpenTraderPanel()
        {
            try
            {
                // Find TraderPanel type
                var traderPanelType = _gameAssembly?.GetType("Eremite.Buildings.UI.Trade.TraderPanel");
                if (traderPanelType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TraderPanel type not found");
                    return false;
                }

                // Get Instance static property
                var instanceProperty = traderPanelType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TraderPanel.Instance property not found");
                    return false;
                }

                var traderPanel = instanceProperty.GetValue(null);
                if (traderPanel == null)
                {
                    // TraderPanel.Instance is null - likely no trading post built yet
                    Debug.LogWarning("[ATSAccessibility] TraderPanel.Instance is null (no trading post built?)");
                    return false;
                }

                // Get current trader visit from TradeService
                var gameServices = GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] GameServices not available");
                    return false;
                }

                var gameServicesType = _gameAssembly?.GetType("Eremite.Services.IGameServices");
                var tradeServiceProperty = gameServicesType?.GetProperty("TradeService",
                    BindingFlags.Public | BindingFlags.Instance);
                if (tradeServiceProperty == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TradeService property not found");
                    return false;
                }

                var tradeService = tradeServiceProperty.GetValue(gameServices);
                if (tradeService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TradeService is null");
                    return false;
                }

                // Get current visit using GetCurrentMainVisit() method (may return null if no trader)
                var getCurrentVisitMethod = tradeService.GetType().GetMethod("GetCurrentMainVisit",
                    BindingFlags.Public | BindingFlags.Instance);
                object currentVisit = null;
                if (getCurrentVisitMethod != null)
                {
                    currentVisit = getCurrentVisitMethod.Invoke(tradeService, null);
                }

                // Call Show(visit, playShowAnim) - visit can be null, panel handles it
                var showMethod = traderPanelType.GetMethod("Show",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { _gameAssembly.GetType("Eremite.Model.State.TraderVisitState"), typeof(bool) },
                    null);

                if (showMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] TraderPanel.Show method not found");
                    return false;
                }

                showMethod.Invoke(traderPanel, new object[] { currentVisit, true });
                Debug.Log("[ATSAccessibility] TraderPanel opened successfully");
                return true;
            }
            catch (Exception ex)
            {
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

        private static void EnsureSettingsGetGood()
        {
            if (_settingsGetGoodCached) return;

            try
            {
                var assembly = GameAssembly;
                if (assembly == null)
                {
                    _settingsGetGoodCached = true;
                    return;
                }

                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetGoodMethodCached = settingsType.GetMethod("GetGood",
                        new[] { typeof(string) });
                }
            }
            catch
            {
                // Ignore
            }

            _settingsGetGoodCached = true;
        }

        /// <summary>
        /// Get the display name for a good by its internal name.
        /// </summary>
        public static string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return "Unknown";

            EnsureSettingsGetGood();

            try
            {
                var settings = GetSettings();
                if (settings == null || _settingsGetGoodMethodCached == null) return goodName;

                var goodModel = _settingsGetGoodMethodCached.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return goodName;

                var displayNameProp = goodModel.GetType().GetProperty("displayName", PublicInstance);
                var locaText = displayNameProp?.GetValue(goodModel);
                return GetLocaText(locaText) ?? goodName;
            }
            catch
            {
                return goodName;
            }
        }

        /// <summary>
        /// Alias for GetAllStoredGoods() for consistency.
        /// </summary>
        public static Dictionary<string, int> GetStorageGoods()
        {
            return GetAllStoredGoods();
        }

        // Cache for Settings.GetRelic method
        private static MethodInfo _settingsGetRelicMethodCached = null;
        private static bool _settingsGetRelicCached = false;

        private static void EnsureSettingsGetRelic()
        {
            if (_settingsGetRelicCached) return;

            try
            {
                var assembly = GameAssembly;
                if (assembly == null)
                {
                    _settingsGetRelicCached = true;
                    return;
                }

                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetRelicMethodCached = settingsType.GetMethod("GetRelic",
                        new[] { typeof(string) });
                }
            }
            catch
            {
                // Ignore
            }

            _settingsGetRelicCached = true;
        }

        /// <summary>
        /// Get the display name for a relic by its internal model name.
        /// </summary>
        public static string GetRelicDisplayName(string relicModelName)
        {
            if (string.IsNullOrEmpty(relicModelName)) return "Unknown";

            EnsureSettingsGetRelic();

            try
            {
                var settings = GetSettings();
                if (settings == null || _settingsGetRelicMethodCached == null) return relicModelName;

                var relicModel = _settingsGetRelicMethodCached.Invoke(settings, new object[] { relicModelName });
                if (relicModel == null) return relicModelName;

                var displayNameField = relicModel.GetType().GetField("displayName", PublicInstance);
                var locaText = displayNameField?.GetValue(relicModel);
                return GetLocaText(locaText) ?? relicModelName;
            }
            catch
            {
                return relicModelName;
            }
        }

        // Cache for Settings.GetMetaCurrency method
        private static MethodInfo _settingsGetMetaCurrencyMethodCached = null;
        private static PropertyInfo _metaCurrencyModelDisplayNameProperty = null;
        private static bool _settingsGetMetaCurrencyCached = false;

        private static void EnsureSettingsGetMetaCurrency()
        {
            if (_settingsGetMetaCurrencyCached) return;

            try
            {
                var assembly = GameAssembly;
                if (assembly == null)
                {
                    _settingsGetMetaCurrencyCached = true;
                    return;
                }

                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetMetaCurrencyMethodCached = settingsType.GetMethod("GetMetaCurrency",
                        new[] { typeof(string) });
                }

                var metaCurrencyModelType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
                if (metaCurrencyModelType != null)
                {
                    _metaCurrencyModelDisplayNameProperty = metaCurrencyModelType.GetProperty("DisplayName",
                        PublicInstance);
                }
            }
            catch
            {
                // Ignore
            }

            _settingsGetMetaCurrencyCached = true;
        }

        /// <summary>
        /// Get the display name for a meta currency by its internal name.
        /// Meta currencies include Food Stockpiles, Machinery Parts, Artifacts, etc.
        /// </summary>
        public static string GetMetaCurrencyDisplayName(string currencyName)
        {
            if (string.IsNullOrEmpty(currencyName)) return "Unknown";

            EnsureSettingsGetMetaCurrency();

            try
            {
                var settings = GetSettings();
                if (settings == null || _settingsGetMetaCurrencyMethodCached == null) return currencyName;

                var currencyModel = _settingsGetMetaCurrencyMethodCached.Invoke(settings, new object[] { currencyName });
                if (currencyModel == null) return currencyName;

                // MetaCurrencyModel.DisplayName returns the localized string directly (not LocaText)
                if (_metaCurrencyModelDisplayNameProperty != null)
                {
                    var displayName = _metaCurrencyModelDisplayNameProperty.GetValue(currencyModel)?.ToString();
                    return !string.IsNullOrEmpty(displayName) ? displayName : currencyName;
                }

                return currencyName;
            }
            catch
            {
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

        private static void EnsureModifiersPanelTypes()
        {
            if (_modifiersPanelTypesCached) return;
            EnsureGameServicesTypes();
            EnsureMysteriesTypes();

            if (_gameAssembly == null)
            {
                _modifiersPanelTypesCached = true;
                return;
            }

            try
            {
                // Get EffectsService and PerksService from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsEffectsServiceProperty = gameServicesType.GetProperty("EffectsService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gsPerksServiceProperty = gameServicesType.GetProperty("PerksService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get GetAllConditions method from IEffectsService
                var effectsServiceType = _gameAssembly.GetType("Eremite.Services.IEffectsService");
                if (effectsServiceType != null)
                {
                    _esGetAllConditionsMethod = effectsServiceType.GetMethod("GetAllConditions",
                        Type.EmptyTypes);
                }

                // Get SortedPerks property from IPerksService
                var perksServiceType = _gameAssembly.GetType("Eremite.Services.IPerksService");
                if (perksServiceType != null)
                {
                    _psSortedPerksProperty = perksServiceType.GetProperty("SortedPerks",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get Cornerstones property from IStateService
                var stateServiceType = _gameAssembly.GetType("Eremite.Services.IStateService");
                if (stateServiceType != null)
                {
                    _ssCornerstonesProperty = stateServiceType.GetProperty("Cornerstones",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get activeCornerstones field from CornerstonesState
                var cornerstonesStateType = _gameAssembly.GetType("Eremite.Model.State.CornerstonesState");
                if (cornerstonesStateType != null)
                {
                    _csActiveCornerstonesField = cornerstonesStateType.GetField("activeCornerstones",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached modifiers panel types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Modifiers panel type caching failed: {ex.Message}");
            }

            _modifiersPanelTypesCached = true;
        }

        /// <summary>
        /// Get EffectsService from GameServices.
        /// </summary>
        public static object GetEffectsService()
        {
            EnsureModifiersPanelTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsEffectsServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get PerksService from GameServices.
        /// </summary>
        public static object GetPerksService()
        {
            EnsureModifiersPanelTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsPerksServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get CornerstonesState from StateService.
        /// </summary>
        public static object GetCornerstonesState()
        {
            EnsureModifiersPanelTypes();
            var stateService = GetStateService();
            if (stateService == null) return null;

            try
            {
                return _ssCornerstonesProperty?.GetValue(stateService);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all active conditions/effects via EffectsService.GetAllConditions().
        /// Returns IEnumerable of EffectModel objects.
        /// Includes: biome effects, difficulty modifiers, embark effects, event effects.
        /// </summary>
        public static System.Collections.IEnumerable GetAllConditions()
        {
            EnsureModifiersPanelTypes();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esGetAllConditionsMethod == null) return null;

            try
            {
                return _esGetAllConditionsMethod.Invoke(effectsService, null) as System.Collections.IEnumerable;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetAllConditions failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the list of active cornerstone effect names.
        /// Returns List of effect name strings.
        /// </summary>
        public static List<string> GetActiveCornerstones()
        {
            EnsureModifiersPanelTypes();
            var cornerstonesState = GetCornerstonesState();
            if (cornerstonesState == null || _csActiveCornerstonesField == null) return null;

            try
            {
                return _csActiveCornerstonesField.GetValue(cornerstonesState) as List<string>;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetActiveCornerstones failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the sorted perks list from PerksService.
        /// Returns List of PerkState objects with name, stacks, hidden fields.
        /// </summary>
        public static System.Collections.IList GetSortedPerks()
        {
            EnsureModifiersPanelTypes();
            var perksService = GetPerksService();
            if (perksService == null || _psSortedPerksProperty == null) return null;

            try
            {
                return _psSortedPerksProperty.GetValue(perksService) as System.Collections.IList;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetSortedPerks failed: {ex.Message}");
                return null;
            }
        }

        // Cached PerkState field info
        private static FieldInfo _perkStateNameField = null;
        private static FieldInfo _perkStateStacksField = null;
        private static FieldInfo _perkStateHiddenField = null;
        private static bool _perkStateFieldsCached = false;

        private static void EnsurePerkStateFields(object firstPerk)
        {
            if (_perkStateFieldsCached || firstPerk == null) return;

            try
            {
                var perkType = firstPerk.GetType();
                _perkStateNameField = perkType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
                _perkStateStacksField = perkType.GetField("stacks", BindingFlags.Public | BindingFlags.Instance);
                _perkStateHiddenField = perkType.GetField("hidden", BindingFlags.Public | BindingFlags.Instance);
                Debug.Log("[ATSAccessibility] Cached PerkState fields");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsurePerkStateFields failed: {ex.Message}");
            }

            _perkStateFieldsCached = true;
        }

        /// <summary>
        /// Extract perk info from a PerkState object.
        /// Returns tuple of (name, stacks, hidden).
        /// </summary>
        public static (string name, int stacks, bool hidden) GetPerkInfo(object perkState)
        {
            if (perkState == null) return (null, 0, true);

            EnsurePerkStateFields(perkState);

            try
            {
                string name = _perkStateNameField?.GetValue(perkState) as string ?? "";
                int stacks = (int?)_perkStateStacksField?.GetValue(perkState) ?? 1;
                bool hidden = (bool?)_perkStateHiddenField?.GetValue(perkState) ?? false;
                return (name, stacks, hidden);
            }
            catch
            {
                return (null, 0, true);
            }
        }

        // Cached EffectModel property for IsPerk check
        private static PropertyInfo _effectModelIsPerkProperty = null;
        private static PropertyInfo _effectModelNameProperty = null;
        private static bool _effectModelPropsCached = false;

        private static void EnsureEffectModelProps(object effectModel)
        {
            if (_effectModelPropsCached || effectModel == null) return;

            try
            {
                var effectType = effectModel.GetType();
                _effectModelIsPerkProperty = effectType.GetProperty("IsPerk", BindingFlags.Public | BindingFlags.Instance);
                _effectModelNameProperty = effectType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                Debug.Log("[ATSAccessibility] Cached EffectModel IsPerk/Name properties");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureEffectModelProps failed: {ex.Message}");
            }

            _effectModelPropsCached = true;
        }

        /// <summary>
        /// Check if an EffectModel is a perk (IsPerk property).
        /// Effects with IsPerk=true get added to perks list when applied.
        /// </summary>
        public static bool GetEffectIsPerk(object effectModel)
        {
            if (effectModel == null) return false;

            EnsureEffectModelProps(effectModel);

            try
            {
                return (bool?)_effectModelIsPerkProperty?.GetValue(effectModel) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the internal Name property from an EffectModel.
        /// </summary>
        public static string GetEffectName(object effectModel)
        {
            if (effectModel == null) return null;

            EnsureEffectModelProps(effectModel);

            try
            {
                return _effectModelNameProperty?.GetValue(effectModel) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the wrapped EffectModel from a SimpleSeasonalEffectModel.
        /// Only SimpleSeasonalEffectModel has an "effect" field - ConditionalSeasonalEffectModel does not.
        /// </summary>
        public static object GetSeasonalEffectWrappedEffect(object seasonalEffectModel)
        {
            if (seasonalEffectModel == null) return null;

            try
            {
                // Get the effect field directly from this model instance
                // SimpleSeasonalEffectModel has "effect" field, ConditionalSeasonalEffectModel does not
                var modelType = seasonalEffectModel.GetType();
                var effectField = modelType.GetField("effect", BindingFlags.Public | BindingFlags.Instance);
                return effectField?.GetValue(seasonalEffectModel);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the internal name of the wrapped effect inside a seasonal effect model.
        /// This is the name that appears in PerksService when the mystery is active.
        /// Only works for SimpleSeasonalEffectModel which has an "effect" field.
        /// </summary>
        public static string GetSeasonalEffectWrappedEffectName(object seasonalEffectModel)
        {
            var wrappedEffect = GetSeasonalEffectWrappedEffect(seasonalEffectModel);
            return GetEffectName(wrappedEffect);
        }

        /// <summary>
        /// Get the hostility level required for a seasonal effect model.
        /// Both SimpleSeasonalEffectModel and ConditionalSeasonalEffectModel have this field.
        /// Returns 0 if no hostility level requirement.
        /// </summary>
        public static int GetSeasonalEffectHostilityLevel(object seasonalEffectModel)
        {
            if (seasonalEffectModel == null) return 0;

            try
            {
                var modelType = seasonalEffectModel.GetType();
                var hostilityField = modelType.GetField("hostilityLevel", BindingFlags.Public | BindingFlags.Instance);
                return (int?)hostilityField?.GetValue(seasonalEffectModel) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // BUILDING RANGE INFO (for 'd' key)
        // ========================================

        // Cached types for building type checks
        private static Type _campModelType = null;
        private static Type _gathererHutModelType = null;
        private static Type _fishingHutModelType = null;
        private static Type _hearthModelType = null;
        private static Type _workshopModelType = null;
        private static bool _rangeInfoTypesCached = false;

        // Cached fields for getting building data
        private static FieldInfo _campRecipesField = null;
        private static FieldInfo _campMaxDistanceField = null;
        private static FieldInfo _gathererHutRecipesField = null;
        private static FieldInfo _gathererHutMaxDistanceField = null;
        private static FieldInfo _fishingHutRecipesField = null;
        private static FieldInfo _fishingHutMaxDistanceField = null;
        private static FieldInfo _hearthHubRangeField = null;

        // Cached fields for recipe goods
        private static FieldInfo _campRecipeRefGoodField = null;
        private static FieldInfo _gathererHutRecipeRefGoodField = null;
        private static FieldInfo _fishingHutRecipeRefGoodField = null;
        private static FieldInfo _goodRefNameField = null;

        // Cached properties for services
        private static PropertyInfo _resourcesAvailableProperty = null;
        private static PropertyInfo _depositsAvailableProperty = null;
        private static PropertyInfo _lakesAvailableProperty = null;
        private static PropertyInfo _effectsServiceProperty = null;
        private static MethodInfo _effectsGetHearthRangeMethod = null;

        private static void EnsureRangeInfoTypes()
        {
            if (_rangeInfoTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _rangeInfoTypesCached = true;
                return;
            }

            try
            {
                // Cache building model types
                _campModelType = _gameAssembly.GetType("Eremite.Buildings.CampModel");
                _gathererHutModelType = _gameAssembly.GetType("Eremite.Buildings.GathererHutModel");
                _fishingHutModelType = _gameAssembly.GetType("Eremite.Buildings.FishingHutModel");
                _hearthModelType = _gameAssembly.GetType("Eremite.Buildings.HearthModel");
                _workshopModelType = _gameAssembly.GetType("Eremite.Buildings.WorkshopModel");

                // Cache CampModel fields
                if (_campModelType != null)
                {
                    _campRecipesField = _campModelType.GetField("recipes", PublicInstance);
                    _campMaxDistanceField = _campModelType.GetField("maxDistance", PublicInstance);
                }

                // Cache GathererHutModel fields
                if (_gathererHutModelType != null)
                {
                    _gathererHutRecipesField = _gathererHutModelType.GetField("recipes", PublicInstance);
                    _gathererHutMaxDistanceField = _gathererHutModelType.GetField("maxDistance", PublicInstance);
                }

                // Cache FishingHutModel fields
                if (_fishingHutModelType != null)
                {
                    _fishingHutRecipesField = _fishingHutModelType.GetField("recipes", PublicInstance);
                    _fishingHutMaxDistanceField = _fishingHutModelType.GetField("maxDistance", PublicInstance);
                }

                // Cache HearthModel fields
                if (_hearthModelType != null)
                {
                    _hearthHubRangeField = _hearthModelType.GetField("hubRange", PublicInstance);
                }

                // Cache recipe refGood fields
                var campRecipeType = _gameAssembly.GetType("Eremite.Buildings.CampRecipeModel");
                if (campRecipeType != null)
                {
                    _campRecipeRefGoodField = campRecipeType.GetField("refGood", PublicInstance);
                }

                var gathererHutRecipeType = _gameAssembly.GetType("Eremite.Buildings.GathererHutRecipeModel");
                if (gathererHutRecipeType != null)
                {
                    _gathererHutRecipeRefGoodField = gathererHutRecipeType.GetField("refGood", PublicInstance);
                }

                var fishingHutRecipeType = _gameAssembly.GetType("Eremite.Buildings.FishingHutRecipeModel");
                if (fishingHutRecipeType != null)
                {
                    _fishingHutRecipeRefGoodField = fishingHutRecipeType.GetField("refGood", PublicInstance);
                }

                // Cache GoodRef Name field (note: we use property getter in GetGatheringBuildingGoodNames, not field)
                var goodRefType = _gameAssembly.GetType("Eremite.Model.GoodRef");
                if (goodRefType != null)
                {
                    // GoodRef has a Name property, not field - we access it dynamically
                    _goodRefNameField = goodRefType.GetField("name", NonPublicInstance);
                }

                // Cache service properties for available resources
                var resourcesServiceType = _gameAssembly.GetType("Eremite.Services.IResourcesService");
                if (resourcesServiceType != null)
                {
                    _resourcesAvailableProperty = resourcesServiceType.GetProperty("AvailableResources", PublicInstance);
                }

                var depositsServiceType = _gameAssembly.GetType("Eremite.Services.IDepositsService");
                if (depositsServiceType != null)
                {
                    _depositsAvailableProperty = depositsServiceType.GetProperty("AvailableDeposits", PublicInstance);
                }

                var lakesServiceType = _gameAssembly.GetType("Eremite.Services.ILakesService");
                if (lakesServiceType != null)
                {
                    _lakesAvailableProperty = lakesServiceType.GetProperty("AvailableLakes", PublicInstance);
                }

                // Cache EffectsService for hearth range
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _effectsServiceProperty = gameServicesType.GetProperty("EffectsService", PublicInstance);
                }

                var effectsServiceType = _gameAssembly.GetType("Eremite.Services.IEffectsService");
                if (effectsServiceType != null)
                {
                    _effectsGetHearthRangeMethod = effectsServiceType.GetMethod("GetHearthRange", PublicInstance);
                }

                Debug.Log("[ATSAccessibility] Cached range info types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Range info type caching failed: {ex.Message}");
            }

            _rangeInfoTypesCached = true;
        }

        /// <summary>
        /// Check if a building model is a Camp (harvests from NaturalResources).
        /// </summary>
        public static bool IsCampModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureRangeInfoTypes();
            return _campModelType != null && _campModelType.IsInstanceOfType(buildingModel);
        }

        /// <summary>
        /// Check if a building model is a GathererHut (harvests from ResourceDeposits).
        /// </summary>
        public static bool IsGathererHutModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureRangeInfoTypes();
            return _gathererHutModelType != null && _gathererHutModelType.IsInstanceOfType(buildingModel);
        }

        /// <summary>
        /// Check if a building model is a FishingHut (harvests from Lakes).
        /// </summary>
        public static bool IsFishingHutModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureRangeInfoTypes();
            return _fishingHutModelType != null && _fishingHutModelType.IsInstanceOfType(buildingModel);
        }

        /// <summary>
        /// Check if a building model is a Hearth.
        /// </summary>
        public static bool IsHearthModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureRangeInfoTypes();
            return _hearthModelType != null && _hearthModelType.IsInstanceOfType(buildingModel);
        }

        /// <summary>
        /// Check if a building model is a Workshop (production building).
        /// </summary>
        public static bool IsWorkshopModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureRangeInfoTypes();
            return _workshopModelType != null && _workshopModelType.IsInstanceOfType(buildingModel);
        }

        /// <summary>
        /// Check if a building model is a House model (housing building).
        /// </summary>
        public static bool IsHouseModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            // HouseModel is the model type for houses
            return buildingModel.GetType().Name == "HouseModel";
        }

        /// <summary>
        /// Check if a building model is an Institution model (service building).
        /// </summary>
        public static bool IsInstitutionModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            return buildingModel.GetType().Name == "InstitutionModel";
        }

        /// <summary>
        /// Check if a building model is a Decoration model.
        /// </summary>
        public static bool IsDecorationModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            return buildingModel.GetType().Name == "DecorationModel";
        }

        /// <summary>
        /// Get the maxDistance field from a Camp/GathererHut/FishingHut model.
        /// Returns 0 if not a gathering building.
        /// </summary>
        public static float GetGatheringBuildingMaxDistance(object buildingModel)
        {
            if (buildingModel == null) return 0f;
            EnsureRangeInfoTypes();

            try
            {
                if (IsCampModel(buildingModel) && _campMaxDistanceField != null)
                {
                    return (float)_campMaxDistanceField.GetValue(buildingModel);
                }
                if (IsGathererHutModel(buildingModel) && _gathererHutMaxDistanceField != null)
                {
                    return (float)_gathererHutMaxDistanceField.GetValue(buildingModel);
                }
                if (IsFishingHutModel(buildingModel) && _fishingHutMaxDistanceField != null)
                {
                    return (float)_fishingHutMaxDistanceField.GetValue(buildingModel);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetGatheringBuildingMaxDistance failed: {ex.Message}");
            }

            return 0f;
        }

        /// <summary>
        /// Get the base hubRange from a Hearth model (before effects).
        /// </summary>
        public static float GetHearthBaseRange(object buildingModel)
        {
            if (buildingModel == null) return 0f;
            EnsureRangeInfoTypes();

            try
            {
                if (IsHearthModel(buildingModel) && _hearthHubRangeField != null)
                {
                    return (float)_hearthHubRangeField.GetValue(buildingModel);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetHearthBaseRange failed: {ex.Message}");
            }

            return 10.5f; // Default hearth range
        }

        /// <summary>
        /// Get the effective hearth range (with effects applied).
        /// </summary>
        public static float GetEffectiveHearthRange(object buildingModel)
        {
            EnsureRangeInfoTypes();
            float baseRange = GetHearthBaseRange(buildingModel);

            try
            {
                var gameServices = GetGameServices();
                if (gameServices == null) return baseRange;

                var effectsService = _effectsServiceProperty?.GetValue(gameServices);
                if (effectsService == null || _effectsGetHearthRangeMethod == null) return baseRange;

                return (float)_effectsGetHearthRangeMethod.Invoke(effectsService, new object[] { baseRange });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetEffectiveHearthRange failed: {ex.Message}");
            }

            return baseRange;
        }

        /// <summary>
        /// Get recipe good names for a gathering building.
        /// Returns list of good names this building can harvest.
        /// </summary>
        public static List<string> GetGatheringBuildingGoodNames(object buildingModel)
        {
            var goodNames = new List<string>();
            if (buildingModel == null) return goodNames;
            EnsureRangeInfoTypes();

            try
            {
                Array recipes = null;
                FieldInfo refGoodField = null;

                if (IsCampModel(buildingModel))
                {
                    recipes = _campRecipesField?.GetValue(buildingModel) as Array;
                    refGoodField = _campRecipeRefGoodField;
                }
                else if (IsGathererHutModel(buildingModel))
                {
                    recipes = _gathererHutRecipesField?.GetValue(buildingModel) as Array;
                    refGoodField = _gathererHutRecipeRefGoodField;
                }
                else if (IsFishingHutModel(buildingModel))
                {
                    recipes = _fishingHutRecipesField?.GetValue(buildingModel) as Array;
                    refGoodField = _fishingHutRecipeRefGoodField;
                }

                if (recipes == null || refGoodField == null) return goodNames;

                foreach (var recipe in recipes)
                {
                    var refGood = refGoodField.GetValue(recipe);
                    if (refGood != null)
                    {
                        // GoodRef has a Name property that returns the good's name
                        var nameProp = refGood.GetType().GetProperty("Name", PublicInstance);
                        var name = nameProp?.GetValue(refGood) as string;
                        if (!string.IsNullOrEmpty(name) && !goodNames.Contains(name))
                        {
                            goodNames.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetGatheringBuildingGoodNames failed: {ex.Message}");
            }

            return goodNames;
        }

        /// <summary>
        /// Get AvailableResources dictionary from ResourcesService.
        /// Dictionary<string, List<NaturalResource>> where key is good name.
        /// </summary>
        public static object GetAvailableResources()
        {
            EnsureRangeInfoTypes();
            var resourcesService = GetResourcesService();
            if (resourcesService == null || _resourcesAvailableProperty == null) return null;

            try
            {
                return _resourcesAvailableProperty.GetValue(resourcesService);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAvailableResources failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get AvailableDeposits dictionary from DepositsService.
        /// Dictionary<string, List<ResourceDeposit>> where key is good name.
        /// </summary>
        public static object GetAvailableDeposits()
        {
            EnsureRangeInfoTypes();
            var depositsService = GetDepositsService();
            if (depositsService == null || _depositsAvailableProperty == null) return null;

            try
            {
                return _depositsAvailableProperty.GetValue(depositsService);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAvailableDeposits failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get AvailableLakes dictionary from LakesService.
        /// Dictionary<string, List<Lake>> where key is good name.
        /// </summary>
        public static object GetAvailableLakes()
        {
            EnsureRangeInfoTypes();
            var lakesService = GetLakesService();
            if (lakesService == null || _lakesAvailableProperty == null) return null;

            try
            {
                return _lakesAvailableProperty.GetValue(lakesService);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAvailableLakes failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name of a resource node (NaturalResource, ResourceDeposit, or Lake).
        /// Returns the model's displayName which is the actual node name (e.g., "Lush Tree", "Clay Pit").
        /// </summary>
        public static string GetResourceNodeDisplayName(object resource)
        {
            if (resource == null) return null;

            try
            {
                // Get the Model property (all resource types have this)
                var modelProp = resource.GetType().GetProperty("Model", PublicInstance);
                if (modelProp == null) return null;

                var model = modelProp.GetValue(resource);
                if (model == null) return null;

                // Get displayName field from the model (NaturalResourceModel, ResourceDepositModel, LakeModel all have this)
                var displayNameField = model.GetType().GetField("displayName", PublicInstance);
                if (displayNameField != null)
                {
                    var locaText = displayNameField.GetValue(model);
                    if (locaText != null)
                    {
                        // LocaText has a Text property that returns the localized string
                        var textProp = locaText.GetType().GetProperty("Text", PublicInstance);
                        return textProp?.GetValue(locaText) as string;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetResourceNodeDisplayName failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the center position of a building.
        /// Returns null if building is null or center cannot be determined.
        /// </summary>
        public static Vector3? GetBuildingCenter(object building)
        {
            if (building == null) return null;

            try
            {
                var centerProperty = building.GetType().GetProperty("Center", PublicInstance);
                if (centerProperty != null)
                {
                    return (Vector3)centerProperty.GetValue(building);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingCenter failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get Field (position) of a resource/deposit/lake object.
        /// </summary>
        public static Vector2Int? GetResourceField(object resource)
        {
            if (resource == null) return null;

            try
            {
                var fieldProperty = resource.GetType().GetProperty("Field", PublicInstance);
                if (fieldProperty != null)
                {
                    return (Vector2Int)fieldProperty.GetValue(resource);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetResourceField failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get Size of a resource/deposit/lake object.
        /// </summary>
        public static Vector2Int? GetResourceSize(object resource)
        {
            if (resource == null) return null;

            try
            {
                var sizeProperty = resource.GetType().GetProperty("Size", PublicInstance);
                if (sizeProperty != null)
                {
                    return (Vector2Int)sizeProperty.GetValue(resource);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetResourceSize failed: {ex.Message}");
            }

            return Vector2Int.one;
        }

        /// <summary>
        /// Get all hearths from BuildingsService.
        /// </summary>
        public static System.Collections.IEnumerable GetAllHearths()
        {
            EnsureMapTypes();
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                // BuildingsService has Hearths property (Dictionary<int, Hearth>)
                if (_hearthsDictProperty == null)
                {
                    _hearthsDictProperty = buildingsService.GetType().GetProperty("Hearths", PublicInstance);
                }

                var hearthsDict = _hearthsDictProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
                return hearthsDict?.Values;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllHearths failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all houses from BuildingsService.
        /// </summary>
        public static System.Collections.IEnumerable GetAllHouses()
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                var housesProperty = buildingsService.GetType().GetProperty("Houses", PublicInstance);
                var housesDict = housesProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
                return housesDict?.Values;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllHouses failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all institutions from BuildingsService.
        /// </summary>
        public static System.Collections.IEnumerable GetAllInstitutions()
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                var institutionsProperty = buildingsService.GetType().GetProperty("Institutions", PublicInstance);
                var institutionsDict = institutionsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
                return institutionsDict?.Values;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllInstitutions failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all decorations from BuildingsService.
        /// </summary>
        public static System.Collections.IEnumerable GetAllDecorations()
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                var decorationsProperty = buildingsService.GetType().GetProperty("Decorations", PublicInstance);
                var decorationsDict = decorationsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
                return decorationsDict?.Values;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllDecorations failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a building is a House.
        /// </summary>
        public static bool IsHouseBuilding(object building)
        {
            if (building == null) return false;
            return building.GetType().Name == "House";
        }

        /// <summary>
        /// Check if a given position is within a hearth's range.
        /// </summary>
        public static bool IsInHearthRange(object hearth, Vector2Int position)
        {
            if (hearth == null) return false;

            try
            {
                // Hearth has IsInRange(Vector2Int field) method
                var isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
                    new Type[] { typeof(Vector2Int) });
                if (isInRangeMethod != null)
                {
                    return (bool)isInRangeMethod.Invoke(hearth, new object[] { position });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] IsInHearthRange failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if a building is in hearth range using the game's IsInRange method.
        /// Works for House, Institution, Decoration, or any building with a Field property.
        /// </summary>
        public static bool IsInHearthRange(object hearth, object building)
        {
            if (hearth == null || building == null) return false;

            try
            {
                // Hearth.IsInRange(Building building) - uses building's Field property
                var isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                    null,
                    new Type[] { building.GetType() },
                    null);

                if (isInRangeMethod != null)
                {
                    return (bool)isInRangeMethod.Invoke(hearth, new object[] { building });
                }

                // Fallback: try with base Building type
                var buildingType = building.GetType().BaseType;
                while (buildingType != null && buildingType.Name != "Building")
                {
                    buildingType = buildingType.BaseType;
                }

                if (buildingType != null)
                {
                    isInRangeMethod = hearth.GetType().GetMethod("IsInRange",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new Type[] { buildingType },
                        null);

                    if (isInRangeMethod != null)
                    {
                        return (bool)isInRangeMethod.Invoke(hearth, new object[] { building });
                    }
                }

                // Last fallback: use Field position
                var fieldProp = building.GetType().GetProperty("Field", PublicInstance);
                if (fieldProp != null)
                {
                    var field = (Vector2Int)fieldProp.GetValue(building);
                    return IsInHearthRange(hearth, field);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] IsInHearthRange(building) failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Calculate distance between a building center (Vector2) and a resource field (Vector2Int).
        /// Uses the game's distance formula: distance from (center.x, center.z) - FieldCenter to field.
        /// </summary>
        public static float CalculateResourceDistance(Vector2 buildingCenter2D, Vector2Int resourceField)
        {
            // Game uses: Vector2.Distance(new Vector2(building.Center.x, building.Center.z) - Constants.FieldCenter, res.Field)
            // Constants.FieldCenter is (0.5, 0.5)
            Vector2 adjustedCenter = buildingCenter2D - new Vector2(0.5f, 0.5f);
            return Vector2.Distance(adjustedCenter, (Vector2)resourceField);
        }

        /// <summary>
        /// Calculate distance from building center to the closest tile of a multi-tile deposit/lake.
        /// </summary>
        public static float CalculateDepositDistance(Vector2 buildingCenter2D, Vector2Int depositField, Vector2Int depositSize)
        {
            // For deposits/lakes, check distance to each tile and return minimum
            float minDistance = float.MaxValue;
            Vector2 adjustedCenter = buildingCenter2D - new Vector2(0.5f, 0.5f);

            for (int x = depositField.x; x < depositField.x + depositSize.x; x++)
            {
                for (int y = depositField.y; y < depositField.y + depositSize.y; y++)
                {
                    float dist = Vector2.Distance(adjustedCenter, new Vector2(x, y));
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                    }
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Calculate building center from cursor position and building size.
        /// </summary>
        public static Vector2 CalculateBuildingCenter(int cursorX, int cursorY, Vector2Int size)
        {
            // Building center is offset from cursor by half the size
            return new Vector2(
                cursorX + (size.x - 1) / 2f,
                cursorY + (size.y - 1) / 2f
            );
        }

        // ========================================
        // SUPPLY CHAIN INFO (for production buildings)
        // ========================================

        /// <summary>
        /// Get a building's entrance center position (used for distance calculations).
        /// </summary>
        public static Vector2? GetBuildingEntranceCenter(object building)
        {
            if (building == null) return null;

            try
            {
                var entranceCenterProp = building.GetType().GetProperty("EntranceCenter", PublicInstance);
                if (entranceCenterProp != null)
                {
                    return (Vector2)entranceCenterProp.GetValue(building);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingEntranceCenter failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all main storage buildings (warehouses).
        /// </summary>
        public static System.Collections.IEnumerable GetAllStorageBuildings()
        {
            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return null;

            try
            {
                var storagesProperty = buildingsService.GetType().GetProperty("Storages", PublicInstance);
                var storagesDict = storagesProperty?.GetValue(buildingsService) as System.Collections.IDictionary;
                return storagesDict?.Values;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllStorageBuildings failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get local storage distance from game config (default 6 tiles).
        /// This is the range within which production buildings can pull from each other.
        /// </summary>
        public static float GetLocalStorageDistance()
        {
            try
            {
                var settings = GetSettings();
                if (settings == null) return 6f;

                var logisticConfigField = settings.GetType().GetField("logisticConfig", PublicInstance);
                if (logisticConfigField == null) return 6f;

                var logisticConfig = logisticConfigField.GetValue(settings);
                if (logisticConfig == null) return 6f;

                var maxDistField = logisticConfig.GetType().GetField("maxLocalStorageDistance", PublicInstance);
                if (maxDistField != null)
                {
                    return (float)maxDistField.GetValue(logisticConfig);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetLocalStorageDistance failed: {ex.Message}");
            }

            return 6f; // Default
        }

        /// <summary>
        /// Check if a building is a source of a specific good (can output it).
        /// Works for production buildings (Workshop, Camp, GathererHut, etc.)
        /// Checks possible outputs based on recipes, not current inventory.
        /// </summary>
        public static bool IsBuildingSourceOf(object building, string goodName)
        {
            if (building == null || string.IsNullOrEmpty(goodName)) return false;

            try
            {
                // Get the GoodModel from settings
                var settings = GetSettings();
                if (settings == null) return false;

                var getGoodMethod = settings.GetType().GetMethod("GetGood", new Type[] { typeof(string) });
                if (getGoodMethod == null) return false;

                var goodModel = getGoodMethod.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return false;

                // Get the GoodModel type from the assembly for proper method lookup
                var goodModelType = GameAssembly.GetType("Eremite.Model.GoodModel");
                if (goodModelType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] Could not find GoodModel type");
                    return false;
                }

                // Check if building.IsSourceOf(goodModel) returns true
                var isSourceOfMethod = building.GetType().GetMethod("IsSourceOf",
                    PublicInstance, null, new Type[] { goodModelType }, null);

                if (isSourceOfMethod != null)
                {
                    return (bool)isSourceOfMethod.Invoke(building, new object[] { goodModel });
                }
                else
                {
                    Debug.Log($"[ATSAccessibility] IsSourceOf method not found on {building.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] IsBuildingSourceOf failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get all input goods required by a production building (from its recipes).
        /// Returns list of good names that are needed as inputs.
        /// </summary>
        public static List<string> GetBuildingRequiredInputs(object building)
        {
            var inputs = new List<string>();
            if (building == null) return inputs;

            try
            {
                // Try to get the state which has recipes
                var stateProperty = building.GetType().GetProperty("state", PublicInstance);
                var stateField = building.GetType().GetField("state", PublicInstance);

                object state = null;
                if (stateProperty != null)
                    state = stateProperty.GetValue(building);
                else if (stateField != null)
                    state = stateField.GetValue(building);

                if (state == null) return inputs;

                // Get recipes array from state
                var recipesField = state.GetType().GetField("recipes", PublicInstance);
                if (recipesField == null) return inputs;

                var recipesObj = recipesField.GetValue(state);
                if (recipesObj == null) return inputs;

                var recipes = recipesObj as System.Collections.IEnumerable;
                if (recipes == null) return inputs;

                foreach (var recipeState in recipes)
                {
                    if (recipeState == null) continue;

                    // Check if recipe is active
                    var activeField = recipeState.GetType().GetField("active", PublicInstance);
                    bool isActive = activeField == null || (bool)activeField.GetValue(recipeState);

                    if (!isActive) continue;

                    // Get ingredients from recipe state
                    var ingredientsField = recipeState.GetType().GetField("ingredients", PublicInstance);
                    if (ingredientsField == null) continue;

                    var ingredients = ingredientsField.GetValue(recipeState) as Array;
                    if (ingredients == null) continue;

                    // Ingredients is a 2D array: IngredientState[][]
                    foreach (var ingredientSet in ingredients)
                    {
                        var ingredientArray = ingredientSet as Array;
                        if (ingredientArray == null) continue;

                        foreach (var ingredientState in ingredientArray)
                        {
                            if (ingredientState == null) continue;

                            // Check if allowed
                            var allowedField = ingredientState.GetType().GetField("allowed", PublicInstance);
                            bool isAllowed = allowedField == null || (bool)allowedField.GetValue(ingredientState);

                            if (!isAllowed) continue;

                            // Get good name - good is a Good struct with a name field
                            var goodField = ingredientState.GetType().GetField("good", PublicInstance);
                            if (goodField != null)
                            {
                                var goodStruct = goodField.GetValue(ingredientState);
                                if (goodStruct != null)
                                {
                                    // Get the name field from the Good struct
                                    var nameField = goodStruct.GetType().GetField("name", PublicInstance);
                                    if (nameField != null)
                                    {
                                        var goodName = nameField.GetValue(goodStruct) as string;
                                        if (!string.IsNullOrEmpty(goodName) && !inputs.Contains(goodName))
                                        {
                                            inputs.Add(goodName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingRequiredInputs failed: {ex.Message}");
            }

            return inputs;
        }

        /// <summary>
        /// Get all possible input goods for a building model (all recipes, all ingredients).
        /// Works on the model itself, so it can be used for both placed buildings and build mode preview.
        /// </summary>
        public static List<string> GetModelPossibleInputs(object buildingModel)
        {
            var inputs = new List<string>();
            if (buildingModel == null) return inputs;

            try
            {
                // Get recipes array from model (WorkshopModel.recipes, etc.)
                var recipesField = buildingModel.GetType().GetField("recipes", PublicInstance);
                if (recipesField == null) return inputs;

                var recipes = recipesField.GetValue(buildingModel) as Array;
                if (recipes == null) return inputs;

                foreach (var recipe in recipes)
                {
                    if (recipe == null) continue;

                    // Get requiredGoods from recipe (GoodsSet[])
                    var requiredGoodsField = recipe.GetType().GetField("requiredGoods", PublicInstance);
                    if (requiredGoodsField == null) continue;

                    var requiredGoods = requiredGoodsField.GetValue(recipe) as Array;
                    if (requiredGoods == null) continue;

                    // Each GoodsSet has a goods array (GoodRef[])
                    foreach (var goodsSet in requiredGoods)
                    {
                        if (goodsSet == null) continue;

                        var goodsField = goodsSet.GetType().GetField("goods", PublicInstance);
                        if (goodsField == null) continue;

                        var goods = goodsField.GetValue(goodsSet) as Array;
                        if (goods == null) continue;

                        // Each GoodRef has a good field (GoodModel)
                        foreach (var goodRef in goods)
                        {
                            if (goodRef == null) continue;

                            var goodField = goodRef.GetType().GetField("good", PublicInstance);
                            if (goodField == null) continue;

                            var goodModel = goodField.GetValue(goodRef);
                            if (goodModel == null) continue;

                            // Get the Name property from GoodModel
                            var nameProperty = goodModel.GetType().GetProperty("Name", PublicInstance);
                            if (nameProperty != null)
                            {
                                var goodName = nameProperty.GetValue(goodModel) as string;
                                if (!string.IsNullOrEmpty(goodName) && !inputs.Contains(goodName))
                                {
                                    inputs.Add(goodName);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetModelPossibleInputs failed: {ex.Message}");
            }

            return inputs;
        }

        /// <summary>
        /// Get all production buildings that could supply a specific good.
        /// Includes Workshops, Camps, GathererHuts, Mines, Farms, etc.
        /// </summary>
        public static List<object> GetBuildingsThatProduce(string goodName)
        {
            var producers = new List<object>();
            if (string.IsNullOrEmpty(goodName)) return producers;

            var buildingsService = GetBuildingsService();
            if (buildingsService == null) return producers;

            try
            {
                // Get the Buildings dictionary (all buildings)
                var buildingsProperty = buildingsService.GetType().GetProperty("Buildings", PublicInstance);
                var buildingsDict = buildingsProperty?.GetValue(buildingsService) as System.Collections.IDictionary;

                if (buildingsDict != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in buildingsDict)
                    {
                        var building = entry.Value;
                        if (building == null) continue;

                        // Check if building is finished
                        var isFinishedMethod = building.GetType().GetMethod("IsFinished", PublicInstance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(building, null);

                        if (!isFinished) continue;

                        // Check if this building produces the good
                        if (IsBuildingSourceOf(building, goodName))
                        {
                            producers.Add(building);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingsThatProduce failed: {ex.Message}");
            }

            return producers;
        }

        /// <summary>
        /// Check if a building is a production building (Workshop, Camp, Mine, Farm, etc.)
        /// </summary>
        public static bool IsProductionBuilding(object building)
        {
            if (building == null) return false;

            string typeName = building.GetType().Name;
            return typeName == "Workshop" || typeName == "Camp" || typeName == "GathererHut" ||
                   typeName == "FishingHut" || typeName == "Mine" || typeName == "Farm" ||
                   typeName == "Collector" || typeName == "RainCatcher";
        }

        /// <summary>
        /// Get the goods a building can actually output.
        /// For gathering buildings (Camp, GathererHut, FishingHut), checks what resources are in range.
        /// For production buildings (Workshop), checks active recipes.
        /// </summary>
        public static List<string> GetBuildingActualOutputs(object building)
        {
            var outputs = new List<string>();
            if (building == null) return outputs;

            try
            {
                string typeName = building.GetType().Name;

                if (typeName == "Camp")
                {
                    outputs = GetCampActualOutputs(building);
                }
                else if (typeName == "GathererHut")
                {
                    outputs = GetGathererHutActualOutputs(building);
                }
                else if (typeName == "FishingHut")
                {
                    outputs = GetFishingHutActualOutputs(building);
                }
                else if (typeName == "Workshop")
                {
                    outputs = GetWorkshopActiveOutputs(building);
                }
                else
                {
                    // For other buildings, fall back to model-based possible outputs
                    var model = GetBuildingModel(building);
                    if (model != null)
                    {
                        outputs = GetModelPossibleOutputs(model);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingActualOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        /// <summary>
        /// Get goods a Camp can actually harvest based on resources in range.
        /// </summary>
        private static List<string> GetCampActualOutputs(object camp)
        {
            var outputs = new List<string>();

            try
            {
                var model = GetBuildingModel(camp);
                if (model == null) return outputs;

                // Get building center for distance check
                var center = GetBuildingCenter(camp);
                if (!center.HasValue) return outputs;

                Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
                float maxDistance = GetGatheringBuildingMaxDistance(model);

                // Get recipes to know what goods this camp can harvest
                var goodNames = GetGatheringBuildingGoodNames(model);
                var availableResources = GetAvailableResources();

                if (availableResources == null) return outputs;

                var dict = availableResources as System.Collections.IDictionary;
                if (dict == null) return outputs;

                foreach (var goodName in goodNames)
                {
                    if (!dict.Contains(goodName)) continue;

                    var resourceList = dict[goodName] as System.Collections.IEnumerable;
                    if (resourceList == null) continue;

                    // Check if any resource of this type is in range
                    foreach (var resource in resourceList)
                    {
                        var field = GetResourceField(resource);
                        if (!field.HasValue) continue;

                        float distance = CalculateResourceDistance(center2D, field.Value);
                        if (distance < maxDistance)
                        {
                            if (!outputs.Contains(goodName))
                            {
                                outputs.Add(goodName);
                            }
                            break; // Found at least one in range, move to next good type
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetCampActualOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        /// <summary>
        /// Get goods a GathererHut can actually harvest based on deposits in range.
        /// </summary>
        private static List<string> GetGathererHutActualOutputs(object hut)
        {
            var outputs = new List<string>();

            try
            {
                var model = GetBuildingModel(hut);
                if (model == null) return outputs;

                var center = GetBuildingCenter(hut);
                if (!center.HasValue) return outputs;

                Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
                float maxDistance = GetGatheringBuildingMaxDistance(model);

                var goodNames = GetGatheringBuildingGoodNames(model);
                var availableDeposits = GetAvailableDeposits();

                if (availableDeposits == null) return outputs;

                var dict = availableDeposits as System.Collections.IDictionary;
                if (dict == null) return outputs;

                foreach (var goodName in goodNames)
                {
                    if (!dict.Contains(goodName)) continue;

                    var depositList = dict[goodName] as System.Collections.IEnumerable;
                    if (depositList == null) continue;

                    foreach (var deposit in depositList)
                    {
                        var field = GetResourceField(deposit);
                        if (!field.HasValue) continue;

                        var size = GetResourceSize(deposit) ?? Vector2Int.one;
                        float distance = CalculateDepositDistance(center2D, field.Value, size);
                        if (distance < maxDistance)
                        {
                            if (!outputs.Contains(goodName))
                            {
                                outputs.Add(goodName);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetGathererHutActualOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        /// <summary>
        /// Get goods a FishingHut can actually harvest based on lakes in range.
        /// </summary>
        private static List<string> GetFishingHutActualOutputs(object hut)
        {
            var outputs = new List<string>();

            try
            {
                var model = GetBuildingModel(hut);
                if (model == null) return outputs;

                var center = GetBuildingCenter(hut);
                if (!center.HasValue) return outputs;

                Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
                float maxDistance = GetGatheringBuildingMaxDistance(model);

                var goodNames = GetGatheringBuildingGoodNames(model);
                var availableLakes = GetAvailableLakes();

                if (availableLakes == null) return outputs;

                var dict = availableLakes as System.Collections.IDictionary;
                if (dict == null) return outputs;

                foreach (var goodName in goodNames)
                {
                    if (!dict.Contains(goodName)) continue;

                    var lakeList = dict[goodName] as System.Collections.IEnumerable;
                    if (lakeList == null) continue;

                    foreach (var lake in lakeList)
                    {
                        var field = GetResourceField(lake);
                        if (!field.HasValue) continue;

                        var size = GetResourceSize(lake) ?? Vector2Int.one;
                        float distance = CalculateDepositDistance(center2D, field.Value, size);
                        if (distance < maxDistance)
                        {
                            if (!outputs.Contains(goodName))
                            {
                                outputs.Add(goodName);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetFishingHutActualOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        /// <summary>
        /// Get goods a Workshop produces based on active recipes.
        /// </summary>
        private static List<string> GetWorkshopActiveOutputs(object workshop)
        {
            var outputs = new List<string>();

            try
            {
                // Get state.recipes
                var stateField = workshop.GetType().GetField("state", PublicInstance);
                if (stateField == null) return outputs;

                var state = stateField.GetValue(workshop);
                if (state == null) return outputs;

                var recipesField = state.GetType().GetField("recipes", PublicInstance);
                if (recipesField == null) return outputs;

                var recipes = recipesField.GetValue(state) as System.Collections.IEnumerable;
                if (recipes == null) return outputs;

                foreach (var recipeState in recipes)
                {
                    if (recipeState == null) continue;

                    // Check if active
                    var activeField = recipeState.GetType().GetField("active", PublicInstance);
                    bool isActive = activeField == null || (bool)activeField.GetValue(recipeState);
                    if (!isActive) continue;

                    // Get productName
                    var productNameField = recipeState.GetType().GetField("productName", PublicInstance);
                    if (productNameField != null)
                    {
                        var productName = productNameField.GetValue(recipeState) as string;
                        if (!string.IsNullOrEmpty(productName) && !outputs.Contains(productName))
                        {
                            outputs.Add(productName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetWorkshopActiveOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        /// <summary>
        /// Get all possible outputs from a building model (all recipes).
        /// </summary>
        private static List<string> GetModelPossibleOutputs(object buildingModel)
        {
            var outputs = new List<string>();
            if (buildingModel == null) return outputs;

            try
            {
                // Get recipes array
                var recipesField = buildingModel.GetType().GetField("recipes", PublicInstance);
                if (recipesField == null) return outputs;

                var recipes = recipesField.GetValue(buildingModel) as Array;
                if (recipes == null) return outputs;

                foreach (var recipe in recipes)
                {
                    if (recipe == null) continue;

                    // Try producedGood (for WorkshopRecipeModel)
                    var producedGoodField = recipe.GetType().GetField("producedGood", PublicInstance);
                    if (producedGoodField != null)
                    {
                        var producedGood = producedGoodField.GetValue(recipe);
                        if (producedGood != null)
                        {
                            var goodField = producedGood.GetType().GetField("good", PublicInstance);
                            if (goodField != null)
                            {
                                var goodModel = goodField.GetValue(producedGood);
                                if (goodModel != null)
                                {
                                    var nameProp = goodModel.GetType().GetProperty("Name", PublicInstance);
                                    if (nameProp != null)
                                    {
                                        var name = nameProp.GetValue(goodModel) as string;
                                        if (!string.IsNullOrEmpty(name) && !outputs.Contains(name))
                                        {
                                            outputs.Add(name);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Try refGood (for CampRecipeModel, GathererHutRecipeModel, etc.)
                    var refGoodField = recipe.GetType().GetField("refGood", PublicInstance);
                    if (refGoodField != null)
                    {
                        var refGood = refGoodField.GetValue(recipe);
                        if (refGood != null)
                        {
                            var goodField = refGood.GetType().GetField("good", PublicInstance);
                            if (goodField != null)
                            {
                                var goodModel = goodField.GetValue(refGood);
                                if (goodModel != null)
                                {
                                    var nameProp = goodModel.GetType().GetProperty("Name", PublicInstance);
                                    if (nameProp != null)
                                    {
                                        var name = nameProp.GetValue(goodModel) as string;
                                        if (!string.IsNullOrEmpty(name) && !outputs.Contains(name))
                                        {
                                            outputs.Add(name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetModelPossibleOutputs failed: {ex.Message}");
            }

            return outputs;
        }

        // ========================================
        // GLADE INFO STATE
        // ========================================

        // Cached reflection for glade info
        private static PropertyInfo _ssEffectsProperty = null;
        private static FieldInfo _effectsGladeInfoOwnersField = null;
        private static FieldInfo _effectsRevealedGrassLocationsField = null;
        private static FieldInfo _effectsRevealedSpringsLocationsField = null;
        private static FieldInfo _effectsRevealedRelicsLocationsField = null;
        private static FieldInfo _effectsDangerousGladeInfoBlocksField = null;
        private static bool _gladeInfoTypesCached = false;

        private static void EnsureGladeInfoTypes()
        {
            if (_gladeInfoTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _gladeInfoTypesCached = true;
                return;
            }

            try
            {
                // Get Effects property from IStateService
                var stateServiceType = _gameAssembly.GetType("Eremite.Services.IStateService");
                if (stateServiceType != null)
                {
                    _ssEffectsProperty = stateServiceType.GetProperty("Effects",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get fields from EffectsState
                var effectsStateType = _gameAssembly.GetType("Eremite.Model.State.EffectsState");
                if (effectsStateType != null)
                {
                    _effectsGladeInfoOwnersField = effectsStateType.GetField("gladeInfoOwners",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectsRevealedGrassLocationsField = effectsStateType.GetField("revealedGrassLocations",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectsRevealedSpringsLocationsField = effectsStateType.GetField("revealedSpringsLocations",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectsRevealedRelicsLocationsField = effectsStateType.GetField("revealedRelicsByTagLocations",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectsDangerousGladeInfoBlocksField = effectsStateType.GetField("dangerousGladeInfoBlocksOwners",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached glade info types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Glade info type caching failed: {ex.Message}");
            }

            _gladeInfoTypesCached = true;
        }

        /// <summary>
        /// Get EffectsState from StateService.
        /// Contains glade info owners and revealed locations.
        /// </summary>
        public static object GetEffectsState()
        {
            EnsureGladeInfoTypes();
            var stateService = GetStateService();
            if (stateService == null) return null;

            try
            {
                return _ssEffectsProperty?.GetValue(stateService);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if full glade info is active (gladeInfoOwners list is non-empty).
        /// When active, shows glade contents in scanner and map navigator.
        /// </summary>
        public static bool HasGladeInfo()
        {
            EnsureGladeInfoTypes();
            var effectsState = GetEffectsState();
            if (effectsState == null || _effectsGladeInfoOwnersField == null) return false;

            try
            {
                var gladeInfoOwners = _effectsGladeInfoOwnersField.GetValue(effectsState) as System.Collections.IList;
                return gladeInfoOwners != null && gladeInfoOwners.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if dangerous glade info is active (NOT blocked).
        /// In Cursed Royal Woodlands, this returns false and ALL glade markers are hidden.
        /// When DangerousGladeInfo is false, players cannot distinguish between Small, Dangerous, or Forbidden glades.
        /// </summary>
        public static bool HasDangerousGladeInfo()
        {
            EnsureGladeInfoTypes();
            var effectsState = GetEffectsState();
            if (effectsState == null || _effectsDangerousGladeInfoBlocksField == null) return true;

            try
            {
                var blockOwners = _effectsDangerousGladeInfoBlocksField.GetValue(effectsState) as System.Collections.IList;
                // DangerousGladeInfo is true when NO blocks exist (count == 0)
                return blockOwners == null || blockOwners.Count == 0;
            }
            catch
            {
                return true;  // Default to showing info
            }
        }

        /// <summary>
        /// Get revealed grass locations (from Human's locate fertile soil ability).
        /// </summary>
        public static List<Vector2Int> GetRevealedGrassLocations()
        {
            EnsureGladeInfoTypes();
            var effectsState = GetEffectsState();
            if (effectsState == null || _effectsRevealedGrassLocationsField == null) return null;

            try
            {
                return _effectsRevealedGrassLocationsField.GetValue(effectsState) as List<Vector2Int>;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get revealed springs locations (from locate spring ability).
        /// </summary>
        public static List<Vector2Int> GetRevealedSpringsLocations()
        {
            EnsureGladeInfoTypes();
            var effectsState = GetEffectsState();
            if (effectsState == null || _effectsRevealedSpringsLocationsField == null) return null;

            try
            {
                return _effectsRevealedSpringsLocationsField.GetValue(effectsState) as List<Vector2Int>;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get revealed relic locations (from dig site/archaeology abilities).
        /// </summary>
        public static List<Vector2Int> GetRevealedRelicLocations()
        {
            EnsureGladeInfoTypes();
            var effectsState = GetEffectsState();
            if (effectsState == null || _effectsRevealedRelicsLocationsField == null) return null;

            try
            {
                return _effectsRevealedRelicsLocationsField.GetValue(effectsState) as List<Vector2Int>;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a position has a location marker.
        /// Returns "grass marker", "spring marker", or "relic marker" if found, null otherwise.
        /// </summary>
        public static string GetLocationMarkerType(int x, int y)
        {
            var pos = new Vector2Int(x, y);

            var grassLocations = GetRevealedGrassLocations();
            if (grassLocations != null && grassLocations.Contains(pos))
                return "grass marker";

            var springsLocations = GetRevealedSpringsLocations();
            if (springsLocations != null && springsLocations.Contains(pos))
                return "spring marker";

            var relicLocations = GetRevealedRelicLocations();
            if (relicLocations != null && relicLocations.Contains(pos))
                return "relic marker";

            return null;
        }

        // Cached reflection for glade contents
        private static FieldInfo _gladeDepositsField = null;
        private static FieldInfo _gladeRelicsField = null;
        private static FieldInfo _gladeBuildingsField = null;
        private static FieldInfo _gladeSpringsField = null;
        private static FieldInfo _gladeLakesField = null;
        private static FieldInfo _gladeOreField = null;
        private static bool _gladeContentsFieldsCached = false;

        private static void EnsureGladeContentsFields(object glade)
        {
            if (_gladeContentsFieldsCached || glade == null) return;

            try
            {
                var gladeType = glade.GetType();
                _gladeDepositsField = gladeType.GetField("deposits", BindingFlags.Public | BindingFlags.Instance);
                _gladeRelicsField = gladeType.GetField("relics", BindingFlags.Public | BindingFlags.Instance);
                _gladeBuildingsField = gladeType.GetField("buildings", BindingFlags.Public | BindingFlags.Instance);
                _gladeSpringsField = gladeType.GetField("springs", BindingFlags.Public | BindingFlags.Instance);
                _gladeLakesField = gladeType.GetField("lakes", BindingFlags.Public | BindingFlags.Instance);
                _gladeOreField = gladeType.GetField("ore", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureGladeContentsFields failed: {ex.Message}");
            }

            _gladeContentsFieldsCached = true;
        }

        /// <summary>
        /// Get a formatted summary of glade contents (e.g., "2 deposits, 1 relic").
        /// Shared by MapScanner and MapNavigator for consistent formatting.
        /// </summary>
        public static string GetGladeContentsSummary(object glade)
        {
            if (glade == null) return null;

            EnsureGladeContentsFields(glade);

            var parts = new List<string>();

            try
            {
                // Deposits
                var deposits = _gladeDepositsField?.GetValue(glade) as System.Collections.IList;
                if (deposits != null && deposits.Count > 0)
                    parts.Add($"{deposits.Count} deposit{(deposits.Count > 1 ? "s" : "")}");

                // Relics
                var relics = _gladeRelicsField?.GetValue(glade) as System.Collections.IList;
                if (relics != null && relics.Count > 0)
                    parts.Add($"{relics.Count} relic{(relics.Count > 1 ? "s" : "")}");

                // Buildings (abandoned structures)
                var buildings = _gladeBuildingsField?.GetValue(glade) as System.Collections.IList;
                if (buildings != null && buildings.Count > 0)
                    parts.Add($"{buildings.Count} building{(buildings.Count > 1 ? "s" : "")}");

                // Springs
                var springs = _gladeSpringsField?.GetValue(glade) as System.Collections.IList;
                if (springs != null && springs.Count > 0)
                    parts.Add($"{springs.Count} spring{(springs.Count > 1 ? "s" : "")}");

                // Lakes
                var lakes = _gladeLakesField?.GetValue(glade) as System.Collections.IList;
                if (lakes != null && lakes.Count > 0)
                    parts.Add($"{lakes.Count} lake{(lakes.Count > 1 ? "s" : "")}");

                // Ore
                var ore = _gladeOreField?.GetValue(glade) as System.Collections.IList;
                if (ore != null && ore.Count > 0)
                    parts.Add($"{ore.Count} ore");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetGladeContentsSummary failed: {ex.Message}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "empty";
        }

        // ========================================
        // LOCATION MARKER EVENT SUBSCRIPTION
        // ========================================

        // Cached reflection for location events
        private static PropertyInfo _esOnGrassLocationRequestedProperty = null;
        private static PropertyInfo _esOnSpringsLocationRequestedProperty = null;
        private static PropertyInfo _esOnRelicLocationRequestedProperty = null;
        private static bool _locationEventTypesCached = false;

        private static void EnsureLocationEventTypes()
        {
            if (_locationEventTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _locationEventTypesCached = true;
                return;
            }

            try
            {
                // Get event properties from IEffectsService
                var effectsServiceType = _gameAssembly.GetType("Eremite.Services.IEffectsService");
                if (effectsServiceType != null)
                {
                    _esOnGrassLocationRequestedProperty = effectsServiceType.GetProperty("OnGrassLocationRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                    _esOnSpringsLocationRequestedProperty = effectsServiceType.GetProperty("OnSpringsLocationRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                    _esOnRelicLocationRequestedProperty = effectsServiceType.GetProperty("OnRelicLocationRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached location event types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Location event type caching failed: {ex.Message}");
            }

            _locationEventTypesCached = true;
        }

        /// <summary>
        /// Subscribe to grass location revealed event (from Human's locate fertile soil ability).
        /// </summary>
        public static IDisposable SubscribeToGrassLocationRequested(Action callback)
        {
            EnsureLocationEventTypes();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esOnGrassLocationRequestedProperty == null) return null;

            try
            {
                var observable = _esOnGrassLocationRequestedProperty.GetValue(effectsService);
                if (observable != null)
                {
                    return SubscribeToObservable(observable, _ => callback());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToGrassLocationRequested failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Subscribe to springs location revealed event.
        /// </summary>
        public static IDisposable SubscribeToSpringsLocationRequested(Action callback)
        {
            EnsureLocationEventTypes();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esOnSpringsLocationRequestedProperty == null) return null;

            try
            {
                var observable = _esOnSpringsLocationRequestedProperty.GetValue(effectsService);
                if (observable != null)
                {
                    return SubscribeToObservable(observable, _ => callback());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToSpringsLocationRequested failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Subscribe to relic location revealed event (dig site/archaeology abilities).
        /// </summary>
        public static IDisposable SubscribeToRelicLocationRequested(Action callback)
        {
            EnsureLocationEventTypes();
            var effectsService = GetEffectsService();
            if (effectsService == null || _esOnRelicLocationRequestedProperty == null) return null;

            try
            {
                var observable = _esOnRelicLocationRequestedProperty.GetValue(effectsService);
                if (observable != null)
                {
                    return SubscribeToObservable(observable, _ => callback());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToRelicLocationRequested failed: {ex.Message}");
            }

            return null;
        }

        // ========================================
        // RELICS HIGHLIGHT SYSTEM (Short Range Scanner)
        // ========================================

        // Cached reflection for RelicsService
        private static PropertyInfo _gsRelicsServiceProperty = null;
        private static PropertyInfo _rsOnRelicsHighlightRequestedProperty = null;
        private static MethodInfo _rsFindRelicForMethod = null;
        private static bool _relicsHighlightTypesCached = false;

        // Track highlighted relics (position -> relic name)
        private static Dictionary<Vector2Int, string> _highlightedRelics = new Dictionary<Vector2Int, string>();

        private static void EnsureRelicsHighlightTypes()
        {
            if (_relicsHighlightTypesCached) return;
            EnsureGameServicesTypes();

            if (_gameAssembly == null)
            {
                _relicsHighlightTypesCached = true;
                return;
            }

            try
            {
                // Get RelicsService from IGameServices
                var gameServicesType = _gameAssembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsRelicsServiceProperty = gameServicesType.GetProperty("RelicsService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Get OnRelicsHighlightRequested and FindRelicFor from IRelicsService
                var relicsServiceType = _gameAssembly.GetType("Eremite.Services.IRelicsService");
                if (relicsServiceType != null)
                {
                    _rsOnRelicsHighlightRequestedProperty = relicsServiceType.GetProperty("OnRelicsHighlightRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                    _rsFindRelicForMethod = relicsServiceType.GetMethod("FindRelicFor",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached RelicsService highlight types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RelicsService type caching failed: {ex.Message}");
            }

            _relicsHighlightTypesCached = true;
        }

        /// <summary>
        /// Get RelicsService from GameServices.
        /// </summary>
        public static object GetRelicsService()
        {
            EnsureRelicsHighlightTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsRelicsServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Subscribe to relic highlight events (from Short Range Scanner, etc).
        /// Callback receives the relic name and position when a relic is highlighted.
        /// </summary>
        public static IDisposable SubscribeToRelicsHighlightRequested(Action<string, Vector2Int> callback)
        {
            EnsureRelicsHighlightTypes();
            var relicsService = GetRelicsService();
            if (relicsService == null || _rsOnRelicsHighlightRequestedProperty == null) return null;

            try
            {
                var observable = _rsOnRelicsHighlightRequestedProperty.GetValue(relicsService);
                if (observable != null)
                {
                    // Subscribe with a handler that extracts the relic info
                    return SubscribeToObservable(observable, request =>
                    {
                        try
                        {
                            // Call FindRelicFor to get the actual relic state
                            if (_rsFindRelicForMethod != null)
                            {
                                var relicState = _rsFindRelicForMethod.Invoke(relicsService, new[] { request });
                                if (relicState != null)
                                {
                                    // Get the relic's name and position
                                    var nameField = relicState.GetType().GetField("name",
                                        BindingFlags.Public | BindingFlags.Instance);
                                    var fieldField = relicState.GetType().GetField("field",
                                        BindingFlags.Public | BindingFlags.Instance);

                                    string name = nameField?.GetValue(relicState) as string ?? "Unknown";
                                    var field = fieldField?.GetValue(relicState);

                                    if (field != null)
                                    {
                                        // field is a Vector2Int
                                        var pos = (Vector2Int)field;
                                        _highlightedRelics[pos] = name;
                                        callback(name, pos);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[ATSAccessibility] RelicsHighlightRequested handler failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToRelicsHighlightRequested failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all currently highlighted relic positions.
        /// </summary>
        public static Dictionary<Vector2Int, string> GetHighlightedRelics()
        {
            return _highlightedRelics;
        }

        /// <summary>
        /// Check if a position has a highlighted relic.
        /// Returns the relic name if found, null otherwise.
        /// </summary>
        public static string GetHighlightedRelicAt(int x, int y)
        {
            var pos = new Vector2Int(x, y);
            if (_highlightedRelics.TryGetValue(pos, out string name))
                return name;
            return null;
        }

        /// <summary>
        /// Clear highlighted relics (call when game ends or new game starts).
        /// </summary>
        public static void ClearHighlightedRelics()
        {
            _highlightedRelics.Clear();
        }

        // Cached reflection for NaturalResource marked state
        private static PropertyInfo _naturalResourceStateProperty = null;
        private static FieldInfo _nrsIsMarkedField = null;
        private static bool _naturalResourceMarkedCached = false;

        private static void EnsureNaturalResourceMarkedCache(object resource)
        {
            if (_naturalResourceMarkedCached) return;
            _naturalResourceMarkedCached = true;

            try
            {
                var resourceType = resource.GetType();
                _naturalResourceStateProperty = resourceType.GetProperty("State",
                    BindingFlags.Public | BindingFlags.Instance);
                if (_naturalResourceStateProperty != null)
                {
                    var stateType = _naturalResourceStateProperty.PropertyType;
                    _nrsIsMarkedField = stateType.GetField("isMarked",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureNaturalResourceMarkedCache failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a NaturalResource is marked for woodcutting/harvesting.
        /// </summary>
        public static bool IsNaturalResourceMarked(object resource)
        {
            if (resource == null) return false;
            EnsureNaturalResourceMarkedCache(resource);
            if (_naturalResourceStateProperty == null || _nrsIsMarkedField == null) return false;
            try
            {
                var state = _naturalResourceStateProperty.GetValue(resource);
                if (state == null) return false;
                return (bool)_nrsIsMarkedField.GetValue(state);
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // HARVEST MARK/UNMARK REFLECTION
        // ========================================

        private static MethodInfo _naturalResourceMarkMethod = null;
        private static MethodInfo _naturalResourceUnmarkMethod = null;
        private static FieldInfo _nrsIsGladeEdgeField = null;
        private static PropertyInfo _resourcesNaturalResourcesProperty = null;
        private static bool _harvestReflectionCached = false;

        private static void EnsureHarvestReflectionCache(object resource)
        {
            if (_harvestReflectionCached) return;
            _harvestReflectionCached = true;

            try
            {
                var resourceType = resource.GetType();
                _naturalResourceMarkMethod = resourceType.GetMethod("Mark", PublicInstance);
                _naturalResourceUnmarkMethod = resourceType.GetMethod("Unmark", PublicInstance);

                // Get isGladeEdge from State type (already cached _naturalResourceStateProperty)
                EnsureNaturalResourceMarkedCache(resource);
                if (_naturalResourceStateProperty != null)
                {
                    var stateType = _naturalResourceStateProperty.PropertyType;
                    _nrsIsGladeEdgeField = stateType.GetField("isGladeEdge", PublicInstance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureHarvestReflectionCache failed: {ex.Message}");
            }
        }

        private static void EnsureResourcesNaturalResourcesProperty(object resourcesService)
        {
            if (_resourcesNaturalResourcesProperty != null) return;

            try
            {
                _resourcesNaturalResourcesProperty = resourcesService.GetType().GetProperty("NaturalResources",
                    PublicInstance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureResourcesNaturalResourcesProperty failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the NaturalResource object at a map position.
        /// Returns null if no resource at that position.
        /// </summary>
        public static object GetNaturalResourceAt(Vector2Int pos)
        {
            var resourcesService = GetResourcesService();
            if (resourcesService == null) return null;

            EnsureResourcesNaturalResourcesProperty(resourcesService);
            if (_resourcesNaturalResourcesProperty == null) return null;

            try
            {
                var dict = _resourcesNaturalResourcesProperty.GetValue(resourcesService) as IDictionary;
                if (dict == null) return null;
                if (dict.Contains(pos))
                    return dict[pos];
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if there is a NaturalResource at a map position.
        /// </summary>
        public static bool HasNaturalResourceAt(Vector2Int pos)
        {
            return GetNaturalResourceAt(pos) != null;
        }

        /// <summary>
        /// Mark a NaturalResource at the given position.
        /// Returns true if successfully marked.
        /// </summary>
        public static bool MarkNaturalResourceAt(Vector2Int pos)
        {
            var resource = GetNaturalResourceAt(pos);
            if (resource == null) return false;

            EnsureHarvestReflectionCache(resource);
            if (_naturalResourceMarkMethod == null) return false;

            try
            {
                _naturalResourceMarkMethod.Invoke(resource, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Unmark a NaturalResource at the given position.
        /// Returns true if successfully unmarked.
        /// </summary>
        public static bool UnmarkNaturalResourceAt(Vector2Int pos)
        {
            var resource = GetNaturalResourceAt(pos);
            if (resource == null) return false;

            EnsureHarvestReflectionCache(resource);
            if (_naturalResourceUnmarkMethod == null) return false;

            try
            {
                _naturalResourceUnmarkMethod.Invoke(resource, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a NaturalResource at the given position is on a glade edge.
        /// </summary>
        public static bool IsNaturalResourceGladeEdge(Vector2Int pos)
        {
            var resource = GetNaturalResourceAt(pos);
            if (resource == null) return false;

            EnsureNaturalResourceMarkedCache(resource);
            EnsureHarvestReflectionCache(resource);
            if (_naturalResourceStateProperty == null || _nrsIsGladeEdgeField == null) return false;

            try
            {
                var state = _naturalResourceStateProperty.GetValue(resource);
                if (state == null) return false;
                return (bool)_nrsIsGladeEdgeField.GetValue(state);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get all NaturalResource positions on the map.
        /// Returns empty list if not in game.
        /// </summary>
        public static List<Vector2Int> GetAllNaturalResourcePositions()
        {
            var result = new List<Vector2Int>();
            var resourcesService = GetResourcesService();
            if (resourcesService == null) return result;

            EnsureResourcesNaturalResourcesProperty(resourcesService);
            if (_resourcesNaturalResourcesProperty == null) return result;

            try
            {
                var dict = _resourcesNaturalResourcesProperty.GetValue(resourcesService) as IDictionary;
                if (dict == null) return result;

                foreach (DictionaryEntry entry in dict)
                {
                    result.Add((Vector2Int)entry.Key);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllNaturalResourcePositions failed: {ex.Message}");
            }

            return result;
        }
    }
}
