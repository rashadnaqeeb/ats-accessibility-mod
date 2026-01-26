using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to capital screen internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// - All public methods return fresh values by querying through cached PropertyInfo
    /// </summary>
    public static class CapitalReflection
    {
        // WorldBlackboardService observables for capital screen
        private static PropertyInfo _wbbOnCapitalEnabledProperty = null;
        private static PropertyInfo _wbbOnCapitalClosedProperty = null;

        // WorldBlackboardService subjects for opening panels
        private static PropertyInfo _wbbCapitalUpgradePanelRequestedProperty = null;
        private static PropertyInfo _wbbHomePopupRequestedProperty = null;
        private static PropertyInfo _wbbGenderPickPopupRequestedProperty = null;

        // MetaStateService for narration state (gender check)
        private static PropertyInfo _msMetaStateServiceProperty = null;
        private static PropertyInfo _mssNarrationProperty = null;
        private static FieldInfo _narrationHandTypeField = null;

        // BlackboardService (from AppServices) for deeds/history
        private static PropertyInfo _appBlackboardServiceProperty = null;

        // MetaPerksService for unlock checks
        private static PropertyInfo _msMetaPerksServiceProperty = null;
        private static MethodInfo _mpsIsHomeEnabledMethod = null;
        private static MethodInfo _mpsIsDailyChallengeEnabledMethod = null;
        private static MethodInfo _mpsIsCustomGameEnabledMethod = null;
        private static MethodInfo _mpsAreGoalsEnabledMethod = null;

        private static bool _typesCached = false;

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var gameAssembly = GameReflection.GameAssembly;
            if (gameAssembly == null)
            {
                _typesCached = true;
                return;
            }

            try
            {
                // Cache WorldBlackboardService observable/subject properties
                var wbbType = gameAssembly.GetType("Eremite.Services.World.IWorldBlackboardService");
                if (wbbType != null)
                {
                    _wbbOnCapitalEnabledProperty = wbbType.GetProperty("OnCapitalEnabled",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wbbOnCapitalClosedProperty = wbbType.GetProperty("OnCapitalClosed",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wbbCapitalUpgradePanelRequestedProperty = wbbType.GetProperty("CapitalUpgradePanelRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wbbHomePopupRequestedProperty = wbbType.GetProperty("HomePopupRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                    _wbbGenderPickPopupRequestedProperty = wbbType.GetProperty("GenderPickPopupRequested",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache BlackboardService property from IServices (AppServices type)
                var servicesType = gameAssembly.GetType("Eremite.Services.IServices");
                if (servicesType != null)
                {
                    _appBlackboardServiceProperty = servicesType.GetProperty("BlackboardService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaPerksService and MetaStateService properties
                var metaServicesType = gameAssembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msMetaPerksServiceProperty = metaServicesType.GetProperty("MetaPerksService",
                        BindingFlags.Public | BindingFlags.Instance);
                    _msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache IMetaStateService.Narration property
                var metaStateServiceType = gameAssembly.GetType("Eremite.Services.IMetaStateService");
                if (metaStateServiceType != null)
                {
                    _mssNarrationProperty = metaStateServiceType.GetProperty("Narration",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache NarrationState.handType field
                var narrationStateType = gameAssembly.GetType("Eremite.Model.State.NarrationState");
                if (narrationStateType != null)
                {
                    _narrationHandTypeField = narrationStateType.GetField("handType",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                var metaPerksServiceType = gameAssembly.GetType("Eremite.Services.IMetaPerksService");
                if (metaPerksServiceType != null)
                {
                    // Game has typo: "IsHomeEnbabled" (not "IsHomeEnabled")
                    _mpsIsHomeEnabledMethod = metaPerksServiceType.GetMethod("IsHomeEnbabled",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mpsIsDailyChallengeEnabledMethod = metaPerksServiceType.GetMethod("IsDailyChallengeEnabled",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mpsIsCustomGameEnabledMethod = metaPerksServiceType.GetMethod("IsCustomGameEnabled",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mpsAreGoalsEnabledMethod = metaPerksServiceType.GetMethod("AreGoalsEnabled",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached CapitalReflection types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CapitalReflection type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }

        /// <summary>
        /// Subscribe to OnCapitalEnabled event (capital screen opened).
        /// </summary>
        public static IDisposable SubscribeToCapitalEnabled(Action<object> callback)
        {
            EnsureTypes();

            try
            {
                var wbb = WorldMapReflection.GetWorldBlackboardService();
                if (wbb == null || _wbbOnCapitalEnabledProperty == null) return null;

                var observable = _wbbOnCapitalEnabledProperty.GetValue(wbb);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToCapitalEnabled failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Subscribe to OnCapitalClosed event (capital screen closed).
        /// </summary>
        public static IDisposable SubscribeToCapitalClosed(Action<object> callback)
        {
            EnsureTypes();

            try
            {
                var wbb = WorldMapReflection.GetWorldBlackboardService();
                if (wbb == null || _wbbOnCapitalClosedProperty == null) return null;

                var observable = _wbbOnCapitalClosedProperty.GetValue(wbb);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToCapitalClosed failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Open the Buy Upgrades panel via WorldBlackboardService.CapitalUpgradePanelRequested.
        /// </summary>
        public static bool OpenUpgrades()
        {
            EnsureTypes();

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null || _wbbCapitalUpgradePanelRequestedProperty == null) return false;

            return GameReflection.InvokeSubjectOnNext(wbb, "CapitalUpgradePanelRequested", true);
        }

        /// <summary>
        /// Open the Deeds popup via BlackboardService.GoalsPopupRequested.
        /// </summary>
        public static bool OpenDeeds()
        {
            EnsureTypes();

            var blackboardService = GetBlackboardService();
            if (blackboardService == null) return false;

            return GameReflection.InvokeSubjectOnNext(blackboardService, "GoalsPopupRequested", true);
        }

        /// <summary>
        /// Open the Game History popup via BlackboardService.GamesHistoryPopupRequested.
        /// </summary>
        public static bool OpenHistory()
        {
            EnsureTypes();

            var blackboardService = GetBlackboardService();
            if (blackboardService == null) return false;

            return GameReflection.InvokeSubjectOnNext(blackboardService, "GamesHistoryPopupRequested", true);
        }

        /// <summary>
        /// Check if gender/hand type has been picked (required before first Home dialog).
        /// </summary>
        public static bool IsGenderPicked()
        {
            EnsureTypes();

            try
            {
                var metaServices = GameReflection.GetMetaServices();
                if (metaServices == null || _msMetaStateServiceProperty == null) return true; // Assume picked if can't check

                var metaStateService = _msMetaStateServiceProperty.GetValue(metaServices);
                if (metaStateService == null || _mssNarrationProperty == null) return true;

                var narrationState = _mssNarrationProperty.GetValue(metaStateService);
                if (narrationState == null || _narrationHandTypeField == null) return true;

                var handType = _narrationHandTypeField.GetValue(narrationState);
                if (handType is int ht)
                {
                    return ht >= 0;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] IsGenderPicked failed: {ex.Message}");
                return true; // Assume picked on error
            }
        }

        /// <summary>
        /// Open the Gender Pick popup via WorldBlackboardService.GenderPickPopupRequested.
        /// </summary>
        public static bool OpenGenderPickPopup()
        {
            EnsureTypes();

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null || _wbbGenderPickPopupRequestedProperty == null) return false;

            try
            {
                // Resolve UniRx.Unit
                Type unitType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    unitType = assembly.GetType("UniRx.Unit");
                    if (unitType != null) break;
                }

                if (unitType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] UniRx.Unit type not found");
                    return false;
                }

                var unitDefault = Activator.CreateInstance(unitType);
                return GameReflection.InvokeSubjectOnNext(wbb, "GenderPickPopupRequested", unitDefault);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenGenderPickPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the Home popup via WorldBlackboardService.HomePopupRequested.
        /// Checks if gender is picked first, opens gender pick popup if not.
        /// </summary>
        public static bool OpenHome()
        {
            EnsureTypes();

            // Check if gender needs to be picked first (matches game behavior)
            if (!IsGenderPicked())
            {
                Debug.Log("[ATSAccessibility] Gender not picked, opening GenderPickPopup first");
                return OpenGenderPickPopup();
            }

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null || _wbbHomePopupRequestedProperty == null) return false;

            try
            {
                // Resolve UniRx.Unit — a zero-field value type, so any instance equals Default
                Type unitType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    unitType = assembly.GetType("UniRx.Unit");
                    if (unitType != null) break;
                }

                if (unitType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] UniRx.Unit type not found");
                    return false;
                }

                var unitDefault = Activator.CreateInstance(unitType);
                return GameReflection.InvokeSubjectOnNext(wbb, "HomePopupRequested", unitDefault);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenHome failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the Daily Expedition popup via WorldBlackboardService.DailyChallengePopupRequested.
        /// </summary>
        public static bool OpenDailyExpedition()
        {
            EnsureTypes();

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null) return false;

            return GameReflection.InvokeSubjectOnNext(wbb, "DailyChallengePopupRequested", true);
        }

        /// <summary>
        /// Open the Training Expedition popup via WorldBlackboardService.CustomGamePopupRequested.
        /// </summary>
        public static bool OpenTrainingExpedition()
        {
            EnsureTypes();

            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb == null) return false;

            return GameReflection.InvokeSubjectOnNext(wbb, "CustomGamePopupRequested", true);
        }

        /// <summary>
        /// Check if the Daily Expedition feature is unlocked.
        /// </summary>
        public static bool IsDailyExpeditionUnlocked()
        {
            EnsureTypes();

            if (_mpsIsDailyChallengeEnabledMethod == null) return false;

            try
            {
                var metaPerksService = GetMetaPerksService();
                if (metaPerksService == null) return false;

                var result = _mpsIsDailyChallengeEnabledMethod.Invoke(metaPerksService, null);
                return result != null && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the Training Expedition feature is unlocked.
        /// </summary>
        public static bool IsTrainingExpeditionUnlocked()
        {
            EnsureTypes();

            if (_mpsIsCustomGameEnabledMethod == null) return false;

            try
            {
                var metaPerksService = GetMetaPerksService();
                if (metaPerksService == null) return false;

                var result = _mpsIsCustomGameEnabledMethod.Invoke(metaPerksService, null);
                return result != null && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the Home feature is unlocked.
        /// </summary>
        public static bool IsHomeUnlocked()
        {
            EnsureTypes();

            if (_mpsIsHomeEnabledMethod == null) return false;

            try
            {
                var metaPerksService = GetMetaPerksService();
                if (metaPerksService == null) return false;

                var result = _mpsIsHomeEnabledMethod.Invoke(metaPerksService, null);
                return result != null && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the Deeds (Goals) feature is unlocked.
        /// </summary>
        public static bool IsDeedsUnlocked()
        {
            EnsureTypes();

            if (_mpsAreGoalsEnabledMethod == null) return false;

            try
            {
                var metaPerksService = GetMetaPerksService();
                if (metaPerksService == null) return false;

                var result = _mpsAreGoalsEnabledMethod.Invoke(metaPerksService, null);
                return result != null && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get BlackboardService from AppServices.
        /// </summary>
        private static object GetBlackboardService()
        {
            var appServices = GameReflection.GetAppServices();
            if (appServices == null || _appBlackboardServiceProperty == null) return null;

            try
            {
                return _appBlackboardServiceProperty.GetValue(appServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get MetaPerksService from MetaController.Instance.MetaServices.
        /// </summary>
        private static object GetMetaPerksService()
        {
            try
            {
                var metaController = GameReflection.MetaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                var metaServices = GameReflection.McMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null) return null;

                return _msMetaPerksServiceProperty?.GetValue(metaServices);
            }
            catch
            {
                return null;
            }
        }
    }
}
