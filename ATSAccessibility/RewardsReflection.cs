using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to reward services for the F3 Rewards panel.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// - All public methods return fresh values by querying through cached PropertyInfo
    /// </summary>
    public static class RewardsReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // IGameServices service properties
        private static PropertyInfo _gsReputationRewardsServiceProperty = null;
        private static PropertyInfo _gsCornerstonesServiceProperty = null;
        private static PropertyInfo _gsNewcomersServiceProperty = null;

        // IReputationRewardsService properties/methods
        private static PropertyInfo _rrsRewardsToCollectProperty = null;
        private static MethodInfo _rrsRequestPopupMethod = null;

        // ICornerstonesService methods
        private static MethodInfo _csGetCurrentPickMethod = null;

        // INewcomersService methods
        private static MethodInfo _nsAreNewcomersWaitningMethod = null;  // Note: typo in game
        private static MethodInfo _nsGetCurrentNewcomersMethod = null;

        // ReactiveProperty<int>.Value property
        private static PropertyInfo _reactivePropertyValueProperty = null;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType == null) return;

                // Cache service property accessors
                _gsReputationRewardsServiceProperty = gameServicesType.GetProperty("ReputationRewardsService");
                _gsCornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService");
                _gsNewcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");

                // Cache ReputationRewardsService members
                var rrsType = assembly.GetType("Eremite.Services.IReputationRewardsService");
                if (rrsType != null)
                {
                    _rrsRewardsToCollectProperty = rrsType.GetProperty("RewardsToCollect");
                    _rrsRequestPopupMethod = rrsType.GetMethod("RequestPopup");
                }

                // Cache CornerstonesService members
                var csType = assembly.GetType("Eremite.Services.ICornerstonesService");
                if (csType != null)
                {
                    _csGetCurrentPickMethod = csType.GetMethod("GetCurrentPick");
                }

                // Cache NewcomersService members
                var nsType = assembly.GetType("Eremite.Services.INewcomersService");
                if (nsType != null)
                {
                    _nsAreNewcomersWaitningMethod = nsType.GetMethod("AreNewcomersWaitning");  // Note: typo in game
                    _nsGetCurrentNewcomersMethod = nsType.GetMethod("GetCurrentNewcomers");
                }

                _cached = true;
                Debug.Log("[ATSAccessibility] RewardsReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RewardsReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // REWARD DETECTION
        // ========================================

        /// <summary>
        /// Check if there are pending blueprints to pick.
        /// Uses ReputationRewardsService.RewardsToCollect.Value > 0.
        /// </summary>
        public static bool HasPendingBlueprints()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var rewardsService = _gsReputationRewardsServiceProperty?.GetValue(gameServices);
                if (rewardsService == null) return false;

                var rewardsToCollect = _rrsRewardsToCollectProperty?.GetValue(rewardsService);
                if (rewardsToCollect == null) return false;

                // Get the Value property from ReactiveProperty<int>
                if (_reactivePropertyValueProperty == null)
                {
                    _reactivePropertyValueProperty = rewardsToCollect.GetType().GetProperty("Value");
                }

                var value = _reactivePropertyValueProperty?.GetValue(rewardsToCollect);
                if (value is int count)
                {
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingBlueprints failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if there are pending cornerstones to pick.
        /// Uses CornerstonesService.GetCurrentPick() != null.
        /// </summary>
        public static bool HasPendingCornerstones()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var cornerstonesService = _gsCornerstonesServiceProperty?.GetValue(gameServices);
                if (cornerstonesService == null) return false;

                var currentPick = _csGetCurrentPickMethod?.Invoke(cornerstonesService, null);
                return currentPick != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingCornerstones failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Check if there are newcomers waiting.
        /// Uses NewcomersService.AreNewcomersWaitning() (note: typo in game).
        /// </summary>
        public static bool HasPendingNewcomers()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return false;

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null) return false;

                var result = _nsAreNewcomersWaitningMethod?.Invoke(newcomersService, null);
                if (result is bool waiting)
                {
                    return waiting;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HasPendingNewcomers failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // POPUP TRIGGERS
        // ========================================

        /// <summary>
        /// Open the blueprints popup.
        /// Uses ReputationRewardsService.RequestPopup().
        /// </summary>
        public static bool OpenBlueprintsPopup()
        {
            EnsureCached();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: GameServices not available");
                    return false;
                }

                var rewardsService = _gsReputationRewardsServiceProperty?.GetValue(gameServices);
                if (rewardsService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: ReputationRewardsService not available");
                    return false;
                }

                if (_rrsRequestPopupMethod == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: RequestPopup method not found");
                    return false;
                }

                _rrsRequestPopupMethod.Invoke(rewardsService, null);
                Debug.Log("[ATSAccessibility] Opened blueprints popup");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenBlueprintsPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the cornerstones popup.
        /// Fires GameBlackboardService.OnRewardsPopupRequested.
        /// </summary>
        public static bool OpenCornerstonesPopup()
        {
            try
            {
                var blackboardService = GameReflection.GetGameBlackboardService();
                if (blackboardService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: GameBlackboardService not available");
                    return false;
                }

                // Get Unit.Default for Subject<Unit>
                var unitDefault = GameReflection.GetUnitDefault();
                if (unitDefault == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: Could not get Unit.Default");
                    return false;
                }

                // Use the shared helper to fire OnRewardsPopupRequested.OnNext(Unit.Default)
                bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnRewardsPopupRequested", unitDefault);
                if (result)
                {
                    Debug.Log("[ATSAccessibility] Opened cornerstones popup");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenCornerstonesPopup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Open the newcomers popup.
        /// Fires GameBlackboardService.OnNewcomersPopupRequested with current newcomers.
        /// </summary>
        public static bool OpenNewcomersPopup()
        {
            EnsureCached();

            try
            {
                // First get the current newcomers
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameServices not available");
                    return false;
                }

                var newcomersService = _gsNewcomersServiceProperty?.GetValue(gameServices);
                if (newcomersService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: NewcomersService not available");
                    return false;
                }

                var currentNewcomers = _nsGetCurrentNewcomersMethod?.Invoke(newcomersService, null);
                if (currentNewcomers == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: No current newcomers");
                    return false;
                }

                // Now fire the event using the shared helper
                var blackboardService = GameReflection.GetGameBlackboardService();
                if (blackboardService == null)
                {
                    Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameBlackboardService not available");
                    return false;
                }

                bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnNewcomersPopupRequested", currentNewcomers);
                if (result)
                {
                    Debug.Log("[ATSAccessibility] Opened newcomers popup");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OpenNewcomersPopup failed: {ex.Message}");
                return false;
            }
        }
    }
}
