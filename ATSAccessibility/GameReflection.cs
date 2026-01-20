using System;
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
            catch { return null; }
        }

        private static object TryInvokeMethod(MethodInfo method, object instance, object[] args = null)
        {
            if (method == null || instance == null) return null;
            try { return method.Invoke(instance, args); }
            catch { return null; }
        }

        private static bool TryInvokeBool(MethodInfo method, object instance, object[] args = null)
        {
            if (method == null || instance == null) return false;
            try { return (bool?)method.Invoke(instance, args) ?? false; }
            catch { return false; }
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

        internal static void EnsureTutorialTypesInternal()
        {
            EnsureTutorialTypes();
        }

        internal static PropertyInfo MetaControllerInstanceProperty
        {
            get
            {
                EnsureTutorialTypes();
                return _metaControllerInstanceProperty;
            }
        }

        internal static PropertyInfo McMetaServicesProperty
        {
            get
            {
                EnsureTutorialTypes();
                return _mcMetaServicesProperty;
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
        private static PropertyInfo _gsOreServiceProperty = null;
        private static PropertyInfo _gsSpringsServiceProperty = null;
        private static PropertyInfo _gsLakesServiceProperty = null;
        private static PropertyInfo _gsBuildingsServiceProperty = null;
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }
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
            catch { }

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
            catch { }

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
            catch { }

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
                    return true; // Fall back to allowing
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
                    return true; // Fall back to allowing rotation
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

        /// <summary>
        /// Helper to invoke OnNext on a UniRx Subject property.
        /// </summary>
        private static bool InvokeSubjectOnNext(object blackboardService, string subjectPropertyName, object parameter)
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
        /// Open the Villagers/Resolve popup via GameBlackboardService.ResolvePopupRequested.
        /// Uses Unit.Default since this Subject takes no parameter.
        /// </summary>
        public static bool OpenVillagersPopup()
        {
            var blackboardService = GetGameBlackboardService();
            if (blackboardService == null)
            {
                Debug.LogWarning("[ATSAccessibility] OpenVillagersPopup: GameBlackboardService not available");
                return false;
            }

            try
            {
                // UniRx.Unit.Default is the singleton value for Unit type
                var unitType = Type.GetType("UniRx.Unit, UniRx");
                if (unitType == null)
                {
                    // Try to find it in loaded assemblies
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        unitType = assembly.GetType("UniRx.Unit");
                        if (unitType != null) break;
                    }
                }

                if (unitType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] UniRx.Unit type not found");
                    return false;
                }

                var defaultField = unitType.GetField("Default", BindingFlags.Public | BindingFlags.Static);
                if (defaultField == null)
                {
                    Debug.LogWarning("[ATSAccessibility] Unit.Default field not found");
                    return false;
                }

                var unitDefault = defaultField.GetValue(null);
                return InvokeSubjectOnNext(blackboardService, "ResolvePopupRequested", unitDefault);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenVillagersPopup failed: {ex.Message}");
                return false;
            }
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
                    Debug.LogWarning("[ATSAccessibility] TraderPanel.Instance is null (no trading post?)");
                    return false;
                }

                // Get current trader visit from TradeService
                var gameServices = GetGameServices();
                if (gameServices == null) return false;

                var tradeServiceType = _gameAssembly?.GetType("Eremite.Services.ITradeService");
                var tradeServiceProperty = gameServices.GetType().GetProperty("TradeService",
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

                // Get current visit (may be null if no trader)
                var currentVisitProperty = tradeService.GetType().GetProperty("CurrentVisit",
                    BindingFlags.Public | BindingFlags.Instance);
                object currentVisit = null;
                if (currentVisitProperty != null)
                {
                    currentVisit = currentVisitProperty.GetValue(tradeService);
                }

                // Call Show(visit, playShowAnim)
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
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenTraderPanel failed: {ex.Message}");
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
    }
}
