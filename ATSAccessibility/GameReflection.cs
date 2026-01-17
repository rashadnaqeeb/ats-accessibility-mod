using System;
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
            if (_gameAssembly == null) return null;

            try
            {
                var mbType = _gameAssembly.GetType("Eremite.MB");
                if (mbType == null) return null;

                var settingsProperty = mbType.GetProperty("Settings",
                    BindingFlags.Public | BindingFlags.Static);
                return settingsProperty?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clear any cached instance references.
        /// Call this on scene unload to be safe.
        /// Note: We deliberately don't cache instances, so this is a no-op,
        /// but it's here for the pattern and future expansion.
        /// </summary>
        public static void ClearCachedInstances()
        {
            // Nothing to clear - we deliberately don't cache instances
            // This method exists for the pattern and documentation
            Debug.Log("[ATSAccessibility] ClearCachedInstances called (no-op by design)");
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
        // TUTORIAL SYSTEM REFLECTION
        // ========================================
        // Path: MetaController.Instance.MetaServices.TutorialService.Phase

        private static Type _metaControllerType = null;
        private static PropertyInfo _metaControllerInstanceProperty = null;  // static Instance
        private static PropertyInfo _mcMetaServicesProperty = null;          // MetaServices
        private static PropertyInfo _msTutorialServiceProperty = null;       // TutorialService
        private static PropertyInfo _tsPhaseProperty = null;                 // Phase (ReactiveProperty)
        private static bool _tutorialTypesCached = false;

        private static void EnsureTutorialTypes()
        {
            if (_tutorialTypesCached) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _tutorialTypesCached = true;
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

                // Cache TutorialService property (from MetaServices interface)
                var metaServicesType = _gameAssembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msTutorialServiceProperty = metaServicesType.GetProperty("TutorialService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache Phase property (from TutorialService)
                var tutorialServiceType = _gameAssembly.GetType("Eremite.Services.ITutorialService");
                if (tutorialServiceType != null)
                {
                    _tsPhaseProperty = tutorialServiceType.GetProperty("Phase",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached TutorialService type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Tutorial type caching failed: {ex.Message}");
            }

            _tutorialTypesCached = true;
        }

        // ========================================
        // GAME SERVICES REFLECTION (for in-game services)
        // ========================================
        // Path: GameController.Instance.GameServices.XxxService

        private static PropertyInfo _gcInstanceProperty = null;       // static Instance
        private static PropertyInfo _gcGameServicesProperty = null;   // GameServices
        private static PropertyInfo _gsReputationRewardsProperty = null;  // ReputationRewardsService
        private static bool _gameServicesTypesCached = false;

        // Camera controller access
        private static PropertyInfo _gcCameraControllerProperty = null;  // GameController.CameraController
        private static FieldInfo _ccTargetField = null;                   // CameraController.target
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

        /// <summary>
        /// Get the TutorialService.Phase observable (ReactiveProperty).
        /// Subscribe to this to get notified of tutorial phase changes.
        /// </summary>
        public static object GetTutorialPhaseObservable()
        {
            EnsureTutorialTypes();

            try
            {
                // Get MetaController.Instance
                var metaController = _metaControllerInstanceProperty?.GetValue(null);
                if (metaController == null)
                {
                    Debug.Log("[ATSAccessibility] DEBUG: MetaController.Instance is null");
                    return null;
                }

                // Get MetaServices
                var metaServices = _mcMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null)
                {
                    Debug.Log("[ATSAccessibility] DEBUG: MetaServices is null");
                    return null;
                }

                // Get TutorialService
                var tutorialService = _msTutorialServiceProperty?.GetValue(metaServices);
                if (tutorialService == null)
                {
                    Debug.Log("[ATSAccessibility] DEBUG: TutorialService is null");
                    return null;
                }

                // Get Phase (ReactiveProperty<TutorialPhase>)
                var phase = _tsPhaseProperty?.GetValue(tutorialService);
                Debug.Log($"[ATSAccessibility] DEBUG: Got TutorialService.Phase: {phase?.GetType().FullName}");
                return phase;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetTutorialPhaseObservable failed: {ex.Message}");
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
        private static PropertyInfo _gsBuildingsServiceProperty = null;
        private static PropertyInfo _gsGladesProperty = null;  // GladesService.Glades list
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
                    _gsBuildingsServiceProperty = gameServicesType.GetProperty("BuildingsService",
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
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsMapServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get GladesService from GameServices.
        /// </summary>
        public static object GetGladesService()
        {
            EnsureMapTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsGladesServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get VillagersService from GameServices.
        /// </summary>
        public static object GetVillagersService()
        {
            EnsureMapTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsVillagersServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get Field at map coordinates.
        /// Returns null if out of bounds or not in game.
        /// </summary>
        public static object GetField(int x, int y)
        {
            EnsureMapTypes();
            var mapService = GetMapService();
            if (mapService == null || _mapGetFieldMethod == null) return null;

            try
            {
                return _mapGetFieldMethod.Invoke(mapService, new object[] { x, y });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get object (building/resource) on a map tile.
        /// Returns null if nothing there or not in game.
        /// </summary>
        public static object GetObjectOn(int x, int y)
        {
            EnsureMapTypes();
            var mapService = GetMapService();
            if (mapService == null || _mapGetObjectOnMethod == null) return null;

            try
            {
                return _mapGetObjectOnMethod.Invoke(mapService, new object[] { x, y });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get Glade at map coordinates.
        /// Returns null if no glade at position or not in game.
        /// </summary>
        public static object GetGlade(int x, int y)
        {
            EnsureMapTypes();
            var gladesService = GetGladesService();
            if (gladesService == null || _gladesGetGladeMethod == null) return null;

            try
            {
                return _gladesGetGladeMethod.Invoke(gladesService, new object[] { new Vector2Int(x, y) });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all villagers as a dictionary.
        /// Returns null if not in game.
        /// </summary>
        public static object GetAllVillagers()
        {
            EnsureMapTypes();
            var villagersService = GetVillagersService();
            if (villagersService == null || _villagersVillagersProperty == null) return null;

            try
            {
                return _villagersVillagersProperty.GetValue(villagersService);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ResourcesService from GameServices.
        /// Contains NaturalResources dictionary.
        /// </summary>
        public static object GetResourcesService()
        {
            EnsureMapTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsResourcesServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get DepositsService from GameServices.
        /// Contains Deposits dictionary.
        /// </summary>
        public static object GetDepositsService()
        {
            EnsureMapTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsDepositsServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get BuildingsService from GameServices.
        /// Contains Buildings dictionary.
        /// </summary>
        public static object GetBuildingsService()
        {
            EnsureMapTypes();
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsBuildingsServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all glades from GladesService.
        /// Returns null if not in game.
        /// </summary>
        public static object GetAllGlades()
        {
            EnsureMapTypes();
            var gladesService = GetGladesService();
            if (gladesService == null || _gsGladesProperty == null) return null;

            try
            {
                return _gsGladesProperty.GetValue(gladesService);
            }
            catch
            {
                return null;
            }
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
            var gameServices = GetGameServices();
            if (gameServices == null) return null;

            try
            {
                return _gsTimeScaleServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if the game is currently paused.
        /// </summary>
        public static bool IsPaused()
        {
            EnsureTimeScaleTypes();
            var timeScaleService = GetTimeScaleService();
            if (timeScaleService == null || _tssIsPausedMethod == null) return false;

            try
            {
                return (bool)_tssIsPausedMethod.Invoke(timeScaleService, null);
            }
            catch
            {
                return false;
            }
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
        /// Uses the game's built-in smooth camera movement system.
        /// </summary>
        public static void SetCameraTarget(Transform target)
        {
            if (target == null) return;

            var cameraController = GetCameraController();
            if (cameraController == null) return;

            try
            {
                // Cache the target field if not already cached
                if (_ccTargetField == null)
                {
                    _ccTargetField = cameraController.GetType().GetField("target",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                if (_ccTargetField != null)
                {
                    _ccTargetField.SetValue(cameraController, target);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetCameraTarget failed: {ex.Message}");
            }
        }

        // ========================================
        // OBSERVABLE SUBSCRIPTION UTILITY
        // ========================================

        /// <summary>
        /// Subscribe to a UniRx IObservable using reflection.
        /// This utility is shared between AccessibilityCore and TutorialHandler.
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
        // WORLD MAP REFLECTION
        // ========================================
        // Path: WorldController.Instance.WorldServices.WorldMapService
        // Path: MetaController.Instance.MetaServices.WorldStateService

        private static Type _worldControllerType = null;
        private static PropertyInfo _wcInstanceProperty = null;       // static Instance
        private static PropertyInfo _wcWorldServicesProperty = null;  // WorldServices
        private static PropertyInfo _wcCameraControllerProperty = null;  // CameraController
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
        private static PropertyInfo _wmsFieldsMapProperty = null;

        // WorldStateService methods
        private static MethodInfo _wssHasModifierMethod = null;
        private static MethodInfo _wssHasEventMethod = null;
        private static MethodInfo _wssHasSealMethod = null;
        private static MethodInfo _wssGetModifierModelMethod = null;
        private static MethodInfo _wssGetEventModelMethod = null;
        private static MethodInfo _wssGetSealModelMethod = null;
        private static MethodInfo _wssGetDisplayNameForMethod = null;

        // WorldBlackboardService
        private static PropertyInfo _wbbOnFieldClickedProperty = null;

        private static void EnsureWorldMapTypes()
        {
            if (_worldMapTypesCached) return;
            EnsureTutorialTypes(); // Ensures MetaController types are cached

            if (_gameAssembly == null)
            {
                _worldMapTypesCached = true;
                return;
            }

            try
            {
                // Cache WorldController type
                _worldControllerType = _gameAssembly.GetType("Eremite.Controller.WorldController");
                if (_worldControllerType != null)
                {
                    _wcInstanceProperty = _worldControllerType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _wcWorldServicesProperty = _worldControllerType.GetProperty("WorldServices",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wcCameraControllerProperty = _worldControllerType.GetProperty("CameraController",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached WorldController type info");
                }

                // Cache IWorldServices interface properties
                var worldServicesType = _gameAssembly.GetType("Eremite.Services.World.IWorldServices");
                if (worldServicesType != null)
                {
                    _wsWorldMapServiceProperty = worldServicesType.GetProperty("WorldMapService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wsWorldBlackboardServiceProperty = worldServicesType.GetProperty("WorldBlackboardService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache IWorldMapService methods
                var worldMapServiceType = _gameAssembly.GetType("Eremite.Services.World.IWorldMapService");
                if (worldMapServiceType != null)
                {
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
                    _wmsFieldsMapProperty = worldMapServiceType.GetProperty("FieldsMap",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached IWorldMapService type info");
                }

                // Cache IWorldStateService methods (from IMetaServices)
                var metaServicesType = _gameAssembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msWorldStateServiceProperty = metaServicesType.GetProperty("WorldStateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                var worldStateServiceType = _gameAssembly.GetType("Eremite.Services.IWorldStateService");
                if (worldStateServiceType != null)
                {
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

                    Debug.Log("[ATSAccessibility] Cached IWorldStateService type info");
                }

                // Cache WorldBlackboardService OnFieldClicked property
                var worldBlackboardServiceType = _gameAssembly.GetType("Eremite.Services.World.IWorldBlackboardService");
                if (worldBlackboardServiceType != null)
                {
                    _wbbOnFieldClickedProperty = worldBlackboardServiceType.GetProperty("OnFieldClicked",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] World map type caching failed: {ex.Message}");
            }

            _worldMapTypesCached = true;
        }

        /// <summary>
        /// Check if we are on the world map (WorldController.Instance != null).
        /// </summary>
        public static bool IsWorldMapActive()
        {
            EnsureWorldMapTypes();

            if (_wcInstanceProperty == null) return false;

            try
            {
                var instance = _wcInstanceProperty.GetValue(null);
                return instance != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get WorldController.Instance.
        /// </summary>
        public static object GetWorldController()
        {
            EnsureWorldMapTypes();

            if (_wcInstanceProperty == null) return null;

            try
            {
                return _wcInstanceProperty.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get WorldServices from WorldController.
        /// </summary>
        public static object GetWorldServices()
        {
            var wc = GetWorldController();
            if (wc == null || _wcWorldServicesProperty == null) return null;

            try
            {
                return _wcWorldServicesProperty.GetValue(wc);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get WorldMapService from WorldServices.
        /// </summary>
        public static object GetWorldMapService()
        {
            EnsureWorldMapTypes();
            var ws = GetWorldServices();
            if (ws == null || _wsWorldMapServiceProperty == null) return null;

            try
            {
                return _wsWorldMapServiceProperty.GetValue(ws);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get WorldStateService from MetaController.Instance.MetaServices.
        /// </summary>
        public static object GetWorldStateService()
        {
            EnsureWorldMapTypes();

            try
            {
                var metaController = _metaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                var metaServices = _mcMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null) return null;

                return _msWorldStateServiceProperty?.GetValue(metaServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get WorldBlackboardService from WorldServices.
        /// </summary>
        public static object GetWorldBlackboardService()
        {
            EnsureWorldMapTypes();
            var ws = GetWorldServices();
            if (ws == null || _wsWorldBlackboardServiceProperty == null) return null;

            try
            {
                return _wsWorldBlackboardServiceProperty.GetValue(ws);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a world map position is within bounds.
        /// </summary>
        public static bool WorldMapInBounds(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsInBoundsMethod == null) return false;

            try
            {
                return (bool)_wmsInBoundsMethod.Invoke(wms, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position is revealed (not in fog).
        /// </summary>
        public static bool WorldMapIsRevealed(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsIsRevealedMethod == null) return false;

            try
            {
                return (bool)_wmsIsRevealedMethod.Invoke(wms, new object[] { cubicPos, 0 });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position is the capital (0,0,0).
        /// </summary>
        public static bool WorldMapIsCapital(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsIsCapitalMethod == null) return false;

            try
            {
                return (bool)_wmsIsCapitalMethod.Invoke(wms, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position has a city.
        /// </summary>
        public static bool WorldMapIsCity(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsIsCityMethod == null) return false;

            try
            {
                return (bool)_wmsIsCityMethod.Invoke(wms, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position can be picked for embark.
        /// </summary>
        public static bool WorldMapCanBePicked(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsCanBePickedMethod == null) return false;

            try
            {
                return (bool)_wmsCanBePickedMethod.Invoke(wms, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position has a modifier.
        /// </summary>
        public static bool WorldMapHasModifier(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssHasModifierMethod == null) return false;

            try
            {
                return (bool)_wssHasModifierMethod.Invoke(wss, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position has an event.
        /// </summary>
        public static bool WorldMapHasEvent(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssHasEventMethod == null) return false;

            try
            {
                return (bool)_wssHasEventMethod.Invoke(wss, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a world map position has a seal.
        /// </summary>
        public static bool WorldMapHasSeal(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssHasSealMethod == null) return false;

            try
            {
                return (bool)_wssHasSealMethod.Invoke(wss, new object[] { cubicPos });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the distance from a position to the capital.
        /// </summary>
        public static int WorldMapGetDistanceToCapital(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsGetDistanceToStartTownMethod == null) return -1;

            try
            {
                return (int)_wmsGetDistanceToStartTownMethod.Invoke(wms, new object[] { cubicPos });
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get the biome display name for a world map position.
        /// </summary>
        public static string WorldMapGetBiomeName(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wms = GetWorldMapService();
            if (wms == null || _wmsGetFieldMethod == null) return null;

            try
            {
                // Get WorldField at position
                var field = _wmsGetFieldMethod.Invoke(wms, new object[] { cubicPos });
                if (field == null) return null;

                // Get Biome property
                var biomeProperty = field.GetType().GetProperty("Biome",
                    BindingFlags.Public | BindingFlags.Instance);
                var biome = biomeProperty?.GetValue(field);
                if (biome == null) return null;

                // Get displayName field
                var displayNameField = biome.GetType().GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                var displayName = displayNameField?.GetValue(biome);
                if (displayName == null) return null;

                // Get Text property from LocaText
                var textProperty = displayName.GetType().GetProperty("Text",
                    BindingFlags.Public | BindingFlags.Instance);
                return textProperty?.GetValue(displayName) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WorldMapGetBiomeName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the city name for a world map position.
        /// </summary>
        public static string WorldMapGetCityName(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssGetDisplayNameForMethod == null) return null;

            try
            {
                return _wssGetDisplayNameForMethod.Invoke(wss, new object[] { cubicPos }) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the modifier name for a world map position.
        /// </summary>
        public static string WorldMapGetModifierName(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssGetModifierModelMethod == null) return null;

            try
            {
                var model = _wssGetModifierModelMethod.Invoke(wss, new object[] { cubicPos });
                if (model == null) return null;

                // Get displayName from model
                var displayNameField = model.GetType().GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                var displayName = displayNameField?.GetValue(model);
                if (displayName == null) return null;

                var textProperty = displayName.GetType().GetProperty("Text",
                    BindingFlags.Public | BindingFlags.Instance);
                return textProperty?.GetValue(displayName) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the event name for a world map position.
        /// </summary>
        public static string WorldMapGetEventName(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssGetEventModelMethod == null) return null;

            try
            {
                var model = _wssGetEventModelMethod.Invoke(wss, new object[] { cubicPos });
                if (model == null) return null;

                // Get displayName from model
                var displayNameField = model.GetType().GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                var displayName = displayNameField?.GetValue(model);
                if (displayName == null) return null;

                var textProperty = displayName.GetType().GetProperty("Text",
                    BindingFlags.Public | BindingFlags.Instance);
                return textProperty?.GetValue(displayName) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the seal name for a world map position.
        /// </summary>
        public static string WorldMapGetSealName(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wss = GetWorldStateService();
            if (wss == null || _wssGetSealModelMethod == null) return null;

            try
            {
                var model = _wssGetSealModelMethod.Invoke(wss, new object[] { cubicPos });
                if (model == null) return null;

                // Get displayName from model
                var displayNameField = model.GetType().GetField("displayName",
                    BindingFlags.Public | BindingFlags.Instance);
                var displayName = displayNameField?.GetValue(model);
                if (displayName == null) return null;

                var textProperty = displayName.GetType().GetProperty("Text",
                    BindingFlags.Public | BindingFlags.Instance);
                return textProperty?.GetValue(displayName) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Trigger a field click on the world map.
        /// This opens the embark screen for the selected tile.
        /// </summary>
        public static void WorldMapTriggerFieldClick(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wbb = GetWorldBlackboardService();
            if (wbb == null || _wbbOnFieldClickedProperty == null) return;

            try
            {
                var subject = _wbbOnFieldClickedProperty.GetValue(wbb);
                if (subject == null) return;

                // Call OnNext on the Subject<Vector3Int>
                var onNextMethod = subject.GetType().GetMethod("OnNext",
                    new Type[] { typeof(Vector3Int) });
                onNextMethod?.Invoke(subject, new object[] { cubicPos });

                Debug.Log($"[ATSAccessibility] Triggered field click at {cubicPos}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WorldMapTriggerFieldClick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Move the world map camera to a position.
        /// </summary>
        public static void SetWorldCameraPosition(Vector3Int cubicPos)
        {
            EnsureWorldMapTypes();
            var wc = GetWorldController();
            if (wc == null || _wcCameraControllerProperty == null) return;

            try
            {
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetWorldCameraPosition failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert cubic hex coordinates to world position.
        /// Matches the game's WorldMapUtils.CubicToWorld implementation.
        /// </summary>
        public static Vector3 CubicToWorld(Vector3Int cubic)
        {
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
    }
}
