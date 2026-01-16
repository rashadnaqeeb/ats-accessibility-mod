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

                // Get GladesService method
                var gladesServiceType = _gameAssembly.GetType("Eremite.Services.IGladesService");
                if (gladesServiceType != null)
                {
                    _gladesGetGladeMethod = gladesServiceType.GetMethod("GetGlade",
                        new Type[] { typeof(Vector2Int) });
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
        // WIKI/ENCYCLOPEDIA REFLECTION
        // ========================================

        private static Type _wikiPopupType;
        private static Type _wikiCategoryButtonType;
        private static Type _wikiSlotType;
        private static bool _wikiTypesLookedUp;

        // WikiPopup fields
        private static FieldInfo _wikiPopupCategoryButtonsField;  // List<WikiCategoryButton> categoryButtons
        private static FieldInfo _wikiPopupCurrentField;          // WikiCategoryPanel current
        private static FieldInfo _wikiPopupPanelsField;           // WikiCategoryPanel[] panels

        // WikiCategoryButton fields
        private static FieldInfo _wcbButtonField;                 // Button button
        private static PropertyInfo _wcbPanelProp;                // WikiCategoryPanel Panel

        // WikiSlot fields
        private static FieldInfo _wsButtonField;                  // Button button
        private static MethodInfo _wsIsUnlockedMethod;            // bool IsUnlocked()

        private static void EnsureWikiTypes()
        {
            if (_wikiTypesLookedUp) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _wikiTypesLookedUp = true;
                return;
            }

            try
            {
                // Cache WikiPopup type
                _wikiPopupType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiPopup");
                if (_wikiPopupType != null)
                {
                    _wikiPopupCategoryButtonsField = _wikiPopupType.GetField("categoryButtons",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wikiPopupCurrentField = _wikiPopupType.GetField("current",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wikiPopupPanelsField = _wikiPopupType.GetField("panels",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached WikiPopup type info");
                }

                // Cache WikiCategoryButton type
                _wikiCategoryButtonType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiCategoryButton");
                if (_wikiCategoryButtonType != null)
                {
                    _wcbButtonField = _wikiCategoryButtonType.GetField("button",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wcbPanelProp = _wikiCategoryButtonType.GetProperty("Panel",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached WikiCategoryButton type info");
                }

                // Cache WikiSlot base type
                _wikiSlotType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiSlot");
                if (_wikiSlotType != null)
                {
                    _wsButtonField = _wikiSlotType.GetField("button",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wsIsUnlockedMethod = _wikiSlotType.GetMethod("IsUnlocked",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] Cached WikiSlot type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Wiki type caching failed: {ex.Message}");
            }

            _wikiTypesLookedUp = true;
        }

        // Public accessors for wiki types
        public static Type WikiPopupType { get { EnsureWikiTypes(); return _wikiPopupType; } }
        public static Type WikiCategoryButtonType { get { EnsureWikiTypes(); return _wikiCategoryButtonType; } }
        public static Type WikiSlotType { get { EnsureWikiTypes(); return _wikiSlotType; } }

        /// <summary>
        /// Check if the popup is a WikiPopup.
        /// </summary>
        public static bool IsWikiPopup(object popup)
        {
            if (popup == null) return false;
            EnsureWikiTypes();
            if (_wikiPopupType == null) return false;

            return _wikiPopupType.IsAssignableFrom(popup.GetType());
        }

        /// <summary>
        /// Get the category buttons list from a WikiPopup.
        /// </summary>
        public static System.Collections.IList GetWikiCategoryButtons(object wikiPopup)
        {
            EnsureWikiTypes();
            if (wikiPopup == null || _wikiPopupCategoryButtonsField == null) return null;

            try
            {
                return _wikiPopupCategoryButtonsField.GetValue(wikiPopup) as System.Collections.IList;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current (active) WikiCategoryPanel from a WikiPopup.
        /// </summary>
        public static object GetCurrentWikiPanel(object wikiPopup)
        {
            EnsureWikiTypes();
            if (wikiPopup == null || _wikiPopupCurrentField == null) return null;

            try
            {
                return _wikiPopupCurrentField.GetValue(wikiPopup);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the panels array from a WikiPopup.
        /// </summary>
        public static Array GetWikiPanels(object wikiPopup)
        {
            EnsureWikiTypes();
            if (wikiPopup == null || _wikiPopupPanelsField == null) return null;

            try
            {
                return _wikiPopupPanelsField.GetValue(wikiPopup) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the WikiCategoryPanel associated with a WikiCategoryButton.
        /// </summary>
        public static object GetCategoryButtonPanel(object categoryButton)
        {
            EnsureWikiTypes();
            if (categoryButton == null || _wcbPanelProp == null) return null;

            try
            {
                return _wcbPanelProp.GetValue(categoryButton);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a WikiSlot is unlocked.
        /// </summary>
        public static bool IsWikiSlotUnlocked(object slot)
        {
            EnsureWikiTypes();
            if (slot == null || _wsIsUnlockedMethod == null) return false;

            try
            {
                return (bool)_wsIsUnlockedMethod.Invoke(slot, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Click the button on a WikiCategoryButton or WikiSlot.
        /// </summary>
        public static void ClickWikiButton(object buttonHolder)
        {
            if (buttonHolder == null) return;
            EnsureWikiTypes();

            try
            {
                FieldInfo buttonField = null;
                var holderType = buttonHolder.GetType();

                // Check if it's a WikiCategoryButton
                if (_wikiCategoryButtonType != null && _wikiCategoryButtonType.IsAssignableFrom(holderType))
                {
                    buttonField = _wcbButtonField;
                }
                // Check if it's a WikiSlot (or derived)
                else if (_wikiSlotType != null && _wikiSlotType.IsAssignableFrom(holderType))
                {
                    buttonField = _wsButtonField;
                }

                if (buttonField != null)
                {
                    var button = buttonField.GetValue(buttonHolder) as UnityEngine.UI.Button;
                    if (button != null && button.interactable)
                    {
                        button.onClick.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ClickWikiButton failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Find slots in a WikiCategoryPanel via reflection.
        /// All panel types have a "slots" field containing List of WikiSlot-derived types.
        /// </summary>
        public static System.Collections.IList GetPanelSlots(object panel)
        {
            if (panel == null) return null;

            try
            {
                var panelType = panel.GetType();
                var slotsField = panelType.GetField("slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotsField != null)
                {
                    return slotsField.GetValue(panel) as System.Collections.IList;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetPanelSlots failed: {ex.Message}");
            }

            return null;
        }
    }
}
