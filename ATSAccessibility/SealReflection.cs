using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to seal building data.
    /// Uses game-facing terminology:
    /// - Stage (internally "kit") - The seal has 4 stages to complete
    /// - Offering (internally "part") - Each stage has multiple offerings to choose from
    /// - Plague - Negative effect that activates during Storm season
    /// </summary>
    public static class SealReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // Type detection
        private static Type _sealPanelType = null;

        // Seal instance methods/fields
        private static MethodInfo _sealIsSealCompletedMethod = null;
        private static MethodInfo _sealGetFirstUncompletedKitMethod = null;
        private static MethodInfo _sealGetModelForMethod = null;
        private static MethodInfo _sealIsKitCompletedMethod = null;
        private static MethodInfo _sealGetCompletedPartForMethod = null;
        private static FieldInfo _sealStateField = null;

        // SealState.kits field
        private static FieldInfo _sealStateKitsField = null;

        // SealKitState fields
        private static FieldInfo _kitStateCompletedIndexField = null;
        private static FieldInfo _kitStateOrdersField = null;

        // SealKitModel fields
        private static FieldInfo _kitModelDialogueField = null;
        private static FieldInfo _kitModelPartsField = null;
        private static FieldInfo _kitModelRewardField = null;

        // SealPartModel fields
        private static FieldInfo _partModelDisplayNameField = null;
        private static FieldInfo _partModelDescriptionField = null;
        private static FieldInfo _partModelOrderField = null;

        // SealGameState fields (via StateService.SealGame)
        private static PropertyInfo _stateServiceSealGameProperty = null;
        private static FieldInfo _sealGameStateCurrentEffectField = null;
        private static FieldInfo _sealGameStateNextEffectField = null;

        // CalendarService methods
        private static MethodInfo _calendarGetSecondsLeftToMethod = null;
        private static PropertyInfo _calendarYearProperty = null;

        // GameDate struct
        private static Type _gameDateType = null;
        private static ConstructorInfo _gameDateCtor = null;

        // Season enum
        private static Type _seasonType = null;
        private static object _seasonStorm = null;

        // GameSealService.CompletePart method
        private static MethodInfo _gameSealServiceCompletePartMethod = null;

        // GameBlackboardService.OnExternalOrderTrackingChanged
        private static PropertyInfo _gbbOnExternalOrderTrackingChangedProperty = null;

        // OrderState.tracked field
        private static FieldInfo _orderStateTrackedField = null;

        // Pre-allocated args arrays
        private static readonly object[] _args1 = new object[1];
        private static readonly object[] _args2 = new object[2];
        private static readonly object[] _args3 = new object[3];

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] SealReflection: Game assembly not available");
                    return;
                }

                CacheSealPanelType(assembly);
                CacheSealTypes(assembly);
                CacheSealKitTypes(assembly);
                CacheSealPartTypes(assembly);
                CacheSealGameStateTypes(assembly);
                CacheCalendarTypes(assembly);
                CacheGameSealServiceTypes(assembly);
                CacheBlackboardTypes(assembly);

                Debug.Log("[ATSAccessibility] SealReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SealReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheSealPanelType(Assembly assembly)
        {
            _sealPanelType = assembly.GetType("Eremite.Buildings.UI.Seals.SealPanel");
        }

        private static void CacheSealTypes(Assembly assembly)
        {
            var sealType = assembly.GetType("Eremite.Buildings.Seal");
            if (sealType != null)
            {
                _sealIsSealCompletedMethod = sealType.GetMethod("IsSealCompleted", GameReflection.PublicInstance);
                _sealGetFirstUncompletedKitMethod = sealType.GetMethod("GetFirstUncompletedKit", GameReflection.PublicInstance);
                _sealIsKitCompletedMethod = sealType.GetMethod("IsKitCompleted", GameReflection.PublicInstance);
                _sealGetCompletedPartForMethod = sealType.GetMethod("GetCompletedPartFor", GameReflection.PublicInstance);
                _sealStateField = sealType.GetField("state", GameReflection.PublicInstance);

                // GetModelFor takes a SealKitState parameter
                var kitStateType = assembly.GetType("Eremite.Buildings.SealKitState");
                if (kitStateType != null)
                {
                    _sealGetModelForMethod = sealType.GetMethod("GetModelFor", new[] { kitStateType });
                }
            }

            // SealState.kits
            var sealStateType = assembly.GetType("Eremite.Buildings.SealState");
            if (sealStateType != null)
            {
                _sealStateKitsField = sealStateType.GetField("kits", GameReflection.PublicInstance);
            }
        }

        private static void CacheSealKitTypes(Assembly assembly)
        {
            var kitStateType = assembly.GetType("Eremite.Buildings.SealKitState");
            if (kitStateType != null)
            {
                _kitStateCompletedIndexField = kitStateType.GetField("completedIndex", GameReflection.PublicInstance);
                _kitStateOrdersField = kitStateType.GetField("orders", GameReflection.PublicInstance);
            }

            var kitModelType = assembly.GetType("Eremite.Buildings.SealKitModel");
            if (kitModelType != null)
            {
                _kitModelDialogueField = kitModelType.GetField("dialogue", GameReflection.PublicInstance);
                _kitModelPartsField = kitModelType.GetField("parts", GameReflection.PublicInstance);
                _kitModelRewardField = kitModelType.GetField("reward", GameReflection.PublicInstance);
            }
        }

        private static void CacheSealPartTypes(Assembly assembly)
        {
            var partModelType = assembly.GetType("Eremite.Buildings.SealPartModel");
            if (partModelType != null)
            {
                _partModelDisplayNameField = partModelType.GetField("displayName", GameReflection.PublicInstance);
                _partModelDescriptionField = partModelType.GetField("description", GameReflection.PublicInstance);
                _partModelOrderField = partModelType.GetField("order", GameReflection.PublicInstance);
            }

            // OrderState.tracked
            var orderStateType = assembly.GetType("Eremite.Model.Orders.OrderState");
            if (orderStateType != null)
            {
                _orderStateTrackedField = orderStateType.GetField("tracked", GameReflection.PublicInstance);
            }
        }

        private static void CacheSealGameStateTypes(Assembly assembly)
        {
            // StateService.SealGame property
            var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
            if (stateServiceType != null)
            {
                _stateServiceSealGameProperty = stateServiceType.GetProperty("SealGame", GameReflection.PublicInstance);
            }

            var sealGameStateType = assembly.GetType("Eremite.Model.State.SealGameState");
            if (sealGameStateType != null)
            {
                _sealGameStateCurrentEffectField = sealGameStateType.GetField("currentEffect", GameReflection.PublicInstance);
                _sealGameStateNextEffectField = sealGameStateType.GetField("nextEffect", GameReflection.PublicInstance);
            }
        }

        private static void CacheCalendarTypes(Assembly assembly)
        {
            var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
            if (calendarServiceType != null)
            {
                _calendarYearProperty = calendarServiceType.GetProperty("Year", GameReflection.PublicInstance);

                // GetSecondsLeftTo takes a GameDate
                _gameDateType = assembly.GetType("Eremite.Model.State.GameDate");
                if (_gameDateType != null)
                {
                    _calendarGetSecondsLeftToMethod = calendarServiceType.GetMethod("GetSecondsLeftTo", new[] { _gameDateType });

                    // Get the constructor: GameDate(int year, Season season)
                    _seasonType = assembly.GetType("Eremite.Model.Season");
                    if (_seasonType != null)
                    {
                        _gameDateCtor = _gameDateType.GetConstructor(new[] { typeof(int), _seasonType });

                        // Cache Storm enum value
                        _seasonStorm = Enum.Parse(_seasonType, "Storm");
                    }
                }
            }
        }

        private static void CacheGameSealServiceTypes(Assembly assembly)
        {
            var gameSealServiceType = assembly.GetType("Eremite.Services.IGameSealService");
            if (gameSealServiceType != null)
            {
                // CompletePart(SealKitState state, SealKitModel model, int index)
                var kitStateType = assembly.GetType("Eremite.Buildings.SealKitState");
                var kitModelType = assembly.GetType("Eremite.Buildings.SealKitModel");
                if (kitStateType != null && kitModelType != null)
                {
                    _gameSealServiceCompletePartMethod = gameSealServiceType.GetMethod("CompletePart",
                        new[] { kitStateType, kitModelType, typeof(int) });
                }
            }
        }

        private static void CacheBlackboardTypes(Assembly assembly)
        {
            var gbbType = assembly.GetType("Eremite.Services.IGameBlackboardService");
            if (gbbType != null)
            {
                _gbbOnExternalOrderTrackingChangedProperty = gbbType.GetProperty("OnExternalOrderTrackingChanged", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // TYPE DETECTION
        // ========================================

        /// <summary>
        /// Check if a popup is the SealPanel.
        /// </summary>
        public static bool IsSealPanel(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            return _sealPanelType != null && _sealPanelType.IsInstanceOfType(popup);
        }

        // ========================================
        // SEAL INSTANCE ACCESS
        // ========================================

        /// <summary>
        /// Get the first (and only) seal building from the map.
        /// </summary>
        public static object GetFirstSeal()
        {
            EnsureCached();
            try
            {
                var seals = GameReflection.GetSeals();
                if (seals == null) return null;

                // Get first value from dictionary
                var valuesProperty = seals.GetType().GetProperty("Values");
                var values = valuesProperty?.GetValue(seals) as IEnumerable;
                if (values == null) return null;

                foreach (var seal in values)
                {
                    return seal;  // Return first one
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetFirstSeal failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the seal is fully completed (all 4 stages done).
        /// </summary>
        public static bool IsSealCompleted(object seal)
        {
            if (seal == null || _sealIsSealCompletedMethod == null) return true;
            try { return (bool)_sealIsSealCompletedMethod.Invoke(seal, null); }
            catch { return true; }
        }

        // ========================================
        // STAGE STATE ACCESS (internally "kit")
        // ========================================

        /// <summary>
        /// Get the first uncompleted stage state (SealKitState).
        /// </summary>
        public static object GetFirstUncompletedStage(object seal)
        {
            if (seal == null || _sealGetFirstUncompletedKitMethod == null) return null;
            try { return _sealGetFirstUncompletedKitMethod.Invoke(seal, null); }
            catch { return null; }
        }

        /// <summary>
        /// Get the stage model (SealKitModel) for a stage state.
        /// </summary>
        public static object GetStageModel(object seal, object stageState)
        {
            if (seal == null || stageState == null || _sealGetModelForMethod == null) return null;
            try
            {
                _args1[0] = stageState;
                return _sealGetModelForMethod.Invoke(seal, _args1);
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if a stage is completed.
        /// </summary>
        public static bool IsStageCompleted(object seal, object stageState)
        {
            if (seal == null || stageState == null || _sealIsKitCompletedMethod == null) return false;
            try
            {
                _args1[0] = stageState;
                return (bool)_sealIsKitCompletedMethod.Invoke(seal, _args1);
            }
            catch { return false; }
        }

        /// <summary>
        /// Get the completed offering model (SealPartModel) for a completed stage.
        /// </summary>
        public static object GetCompletedOfferingFor(object seal, object stageState)
        {
            if (seal == null || stageState == null || _sealGetCompletedPartForMethod == null) return null;
            try
            {
                _args1[0] = stageState;
                return _sealGetCompletedPartForMethod.Invoke(seal, _args1);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the completed index from a stage state (-1 if not completed).
        /// </summary>
        public static int GetStageCompletedIndex(object stageState)
        {
            if (stageState == null || _kitStateCompletedIndexField == null) return -1;
            try { return (int)_kitStateCompletedIndexField.GetValue(stageState); }
            catch { return -1; }
        }

        /// <summary>
        /// Get the orders array (OrderState[]) from a stage state.
        /// </summary>
        public static Array GetStageOrders(object stageState)
        {
            if (stageState == null || _kitStateOrdersField == null) return null;
            try { return _kitStateOrdersField.GetValue(stageState) as Array; }
            catch { return null; }
        }

        /// <summary>
        /// Get all stage states from the seal.
        /// </summary>
        public static Array GetAllStages(object seal)
        {
            if (seal == null || _sealStateField == null || _sealStateKitsField == null) return null;
            try
            {
                var state = _sealStateField.GetValue(seal);
                if (state == null) return null;
                return _sealStateKitsField.GetValue(state) as Array;
            }
            catch { return null; }
        }

        // ========================================
        // STAGE MODEL ACCESS
        // ========================================

        /// <summary>
        /// Get the dialogue text from a stage model.
        /// </summary>
        public static string GetStageDialogue(object stageModel)
        {
            if (stageModel == null || _kitModelDialogueField == null) return null;
            try
            {
                var locaText = _kitModelDialogueField.GetValue(stageModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the offerings array (SealPartModel[]) from a stage model.
        /// </summary>
        public static Array GetStageOfferings(object stageModel)
        {
            if (stageModel == null || _kitModelPartsField == null) return null;
            try { return _kitModelPartsField.GetValue(stageModel) as Array; }
            catch { return null; }
        }

        /// <summary>
        /// Get the reward (EffectModel) from a stage model.
        /// </summary>
        public static object GetStageReward(object stageModel)
        {
            if (stageModel == null || _kitModelRewardField == null) return null;
            try { return _kitModelRewardField.GetValue(stageModel); }
            catch { return null; }
        }

        // ========================================
        // OFFERING MODEL ACCESS (internally "part")
        // ========================================

        /// <summary>
        /// Get the display name from an offering model.
        /// </summary>
        public static string GetOfferingDisplayName(object offeringModel)
        {
            if (offeringModel == null || _partModelDisplayNameField == null) return null;
            try
            {
                var locaText = _partModelDisplayNameField.GetValue(offeringModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the description from an offering model.
        /// </summary>
        public static string GetOfferingDescription(object offeringModel)
        {
            if (offeringModel == null || _partModelDescriptionField == null) return null;
            try
            {
                var locaText = _partModelDescriptionField.GetValue(offeringModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the order model (OrderModel) from an offering model.
        /// </summary>
        public static object GetOfferingOrder(object offeringModel)
        {
            if (offeringModel == null || _partModelOrderField == null) return null;
            try { return _partModelOrderField.GetValue(offeringModel); }
            catch { return null; }
        }

        // ========================================
        // PLAGUE STATE ACCESS
        // ========================================

        /// <summary>
        /// Get the SealGameState from StateService.
        /// </summary>
        public static object GetSealGameState()
        {
            EnsureCached();
            try
            {
                var stateService = GameReflection.GetStateService();
                if (stateService == null || _stateServiceSealGameProperty == null) return null;
                return _stateServiceSealGameProperty.GetValue(stateService);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the current active plague effect name (empty if not in storm).
        /// </summary>
        public static string GetCurrentEffect(object sealGameState)
        {
            if (sealGameState == null || _sealGameStateCurrentEffectField == null) return null;
            try { return _sealGameStateCurrentEffectField.GetValue(sealGameState) as string; }
            catch { return null; }
        }

        /// <summary>
        /// Get the next plague effect name (what will activate during storm).
        /// </summary>
        public static string GetNextEffect(object sealGameState)
        {
            if (sealGameState == null || _sealGameStateNextEffectField == null) return null;
            try { return _sealGameStateNextEffectField.GetValue(sealGameState) as string; }
            catch { return null; }
        }

        /// <summary>
        /// Check if a plague effect is currently active.
        /// </summary>
        public static bool IsEffectActive(object sealGameState)
        {
            var current = GetCurrentEffect(sealGameState);
            return !string.IsNullOrEmpty(current);
        }

        /// <summary>
        /// Get seconds until the next storm season starts.
        /// </summary>
        public static float GetSecondsUntilStorm()
        {
            EnsureCached();
            try
            {
                var calendarService = GameReflection.GetCalendarService();
                if (calendarService == null || _calendarGetSecondsLeftToMethod == null) return 0f;
                if (_gameDateCtor == null || _calendarYearProperty == null || _seasonStorm == null) return 0f;

                // Get current year
                int year = (int)_calendarYearProperty.GetValue(calendarService);

                // Create GameDate(year, Season.Storm)
                var gameDate = _gameDateCtor.Invoke(new object[] { year, _seasonStorm });

                // Call GetSecondsLeftTo
                _args1[0] = gameDate;
                return (float)_calendarGetSecondsLeftToMethod.Invoke(calendarService, _args1);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetSecondsUntilStorm failed: {ex.Message}");
                return 0f;
            }
        }

        // ========================================
        // ACTIONS
        // ========================================

        /// <summary>
        /// Complete (deliver) an offering for the current stage.
        /// </summary>
        public static bool CompleteOffering(object stageState, object stageModel, int offeringIndex)
        {
            EnsureCached();
            if (_gameSealServiceCompletePartMethod == null) return false;

            try
            {
                var gameSealService = GameReflection.GetGameSealService();
                if (gameSealService == null) return false;

                _args3[0] = stageState;
                _args3[1] = stageModel;
                _args3[2] = offeringIndex;
                _gameSealServiceCompletePartMethod.Invoke(gameSealService, _args3);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] CompleteOffering failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggle tracking for an order state.
        /// </summary>
        public static bool ToggleOfferingTracking(object orderState)
        {
            EnsureCached();
            if (orderState == null || _orderStateTrackedField == null || _gbbOnExternalOrderTrackingChangedProperty == null)
                return false;

            try
            {
                // Toggle the tracked field
                bool currentlyTracked = (bool)_orderStateTrackedField.GetValue(orderState);
                _orderStateTrackedField.SetValue(orderState, !currentlyTracked);

                // Fire the OnExternalOrderTrackingChanged event
                var blackboard = GameReflection.GetGameBlackboardService();
                if (blackboard == null) return false;

                var subject = _gbbOnExternalOrderTrackingChangedProperty.GetValue(blackboard);
                if (subject == null) return false;

                var onNextMethod = subject.GetType().GetMethod("OnNext", GameReflection.PublicInstance);
                if (onNextMethod == null) return false;

                _args1[0] = orderState;
                onNextMethod.Invoke(subject, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ToggleOfferingTracking failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if an order is currently tracked.
        /// </summary>
        public static bool IsOfferingTracked(object orderState)
        {
            if (orderState == null || _orderStateTrackedField == null) return false;
            try { return (bool)_orderStateTrackedField.GetValue(orderState); }
            catch { return false; }
        }

        // ========================================
        // PROGRESS SUMMARY
        // ========================================

        /// <summary>
        /// Get progress summary: current stage number, total stages, list of completed offering names.
        /// </summary>
        public static (int current, int total, List<string> completedNames) GetProgress(object seal)
        {
            var result = (current: 1, total: 4, completedNames: new List<string>());
            if (seal == null) return result;

            try
            {
                var stages = GetAllStages(seal);
                if (stages == null) return result;

                result.total = stages.Length;
                int firstUncompleted = -1;

                for (int i = 0; i < stages.Length; i++)
                {
                    var stageState = stages.GetValue(i);
                    if (stageState == null) continue;

                    if (IsStageCompleted(seal, stageState))
                    {
                        var completedOffering = GetCompletedOfferingFor(seal, stageState);
                        if (completedOffering != null)
                        {
                            string name = GetOfferingDisplayName(completedOffering);
                            if (!string.IsNullOrEmpty(name))
                                result.completedNames.Add(name);
                        }
                    }
                    else if (firstUncompleted < 0)
                    {
                        firstUncompleted = i;
                    }
                }

                // Current stage is 1-based; return total+1 when all stages complete
                result.current = (firstUncompleted >= 0) ? (firstUncompleted + 1) : result.total + 1;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetProgress failed: {ex.Message}");
            }

            return result;
        }
    }
}
