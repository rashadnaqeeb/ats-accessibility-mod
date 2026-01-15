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
        private static PropertyInfo _gcInstanceProperty = null;      // static Instance
        private static PropertyInfo _gcIsGameActiveProperty = null;  // static IsGameActive
        private static PropertyInfo _gcGameServicesProperty = null;  // instance GameServices

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
                    _gcInstanceProperty = _gameControllerType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _gcIsGameActiveProperty = _gameControllerType.GetProperty("IsGameActive",
                        BindingFlags.Public | BindingFlags.Static);
                    _gcGameServicesProperty = _gameControllerType.GetProperty("GameServices",
                        BindingFlags.Public | BindingFlags.Instance);

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
        /// Get GameController instance. Returns null if not in game.
        /// DO NOT cache this value - it's destroyed on scene change.
        /// </summary>
        public static object GetGameControllerInstance()
        {
            EnsureTypes();

            if (_gcInstanceProperty == null) return null;

            try
            {
                return _gcInstanceProperty.GetValue(null);
            }
            catch
            {
                return null;
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
        /// Get GameServices from current GameController.
        /// DO NOT cache - services are destroyed on scene change.
        /// </summary>
        public static object GetGameServices()
        {
            var gc = GetGameControllerInstance();
            if (gc == null || _gcGameServicesProperty == null) return null;

            try
            {
                return _gcGameServicesProperty.GetValue(gc);
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
        public static Type TabsButtonType { get { EnsureTabTypes(); return _tabsButtonType; } }
        public static FieldInfo TabsPanelButtonsField { get { EnsureTabTypes(); return _tabsPanelButtonsField; } }
        public static FieldInfo TabsPanelCurrentField { get { EnsureTabTypes(); return _tabsPanelCurrentField; } }
        public static FieldInfo TabsButtonButtonField { get { EnsureTabTypes(); return _tabsButtonButtonField; } }
        public static FieldInfo TabsButtonContentField { get { EnsureTabTypes(); return _tabsButtonContentField; } }
    }
}
