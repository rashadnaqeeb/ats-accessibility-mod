using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to WorldEventPopup and WorldEventModel.
    /// Used for navigating world event decision screens on the world map.
    ///
    /// Note: Instance data (popup, model, state) is NOT cached here - callers
    /// must pass instances as parameters. Only reflection metadata is cached.
    /// </summary>
    public static class WorldEventReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================
        private static bool _cached;

        // WorldEventPopup type detection
        private static Type _worldEventPopupType;

        // WorldEventPopup.worldEvent field (WorldEvent instance)
        private static FieldInfo _popupWorldEventField;

        // WorldEvent.model field (WorldEventModel)
        private static FieldInfo _worldEventModelField;

        // WorldEvent.state field (WorldEventState)
        private static FieldInfo _worldEventStateField;

        // WorldEventModel properties
        private static FieldInfo _modelDisplayNameField;       // LocaText displayName
        private static FieldInfo _modelDescriptionField;       // LocaText description
        private static FieldInfo _modelOptionsField;           // WorldEventLogic[] options

        // WorldEventModel methods
        private static MethodInfo _modelGetDescriptionForOptionMethod;   // GetDescriptionForOption(int)
        private static MethodInfo _modelCanExecuteMethod;                 // CanExecute(int)
        private static MethodInfo _modelGetExecutionBlockReasonMethod;    // GetExecutionBlockReason(int)
        private static MethodInfo _modelExecuteDecisionMethod;            // ExecuteDecision(WorldEventState, int)

        // Pre-allocated args arrays to avoid GC pressure
        private static readonly object[] _args1 = new object[1];
        private static readonly object[] _args2 = new object[2];

        // ========================================
        // TYPE DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a WorldEventPopup.
        /// </summary>
        public static bool IsWorldEventPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_worldEventPopupType == null) return false;
            return _worldEventPopupType.IsInstanceOfType(popup);
        }

        // ========================================
        // INSTANCE EXTRACTION
        // ========================================

        /// <summary>
        /// Extract the WorldEvent instance from a WorldEventPopup.
        /// </summary>
        public static object GetWorldEvent(object popup)
        {
            if (popup == null) return null;
            EnsureCached();
            if (_popupWorldEventField == null) return null;

            try
            {
                return _popupWorldEventField.GetValue(popup);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetWorldEvent failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract the WorldEventModel from a WorldEvent instance.
        /// </summary>
        public static object GetModel(object worldEvent)
        {
            if (worldEvent == null) return null;
            EnsureCached();
            if (_worldEventModelField == null) return null;

            try
            {
                return _worldEventModelField.GetValue(worldEvent);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetModel failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract the WorldEventState from a WorldEvent instance.
        /// </summary>
        public static object GetState(object worldEvent)
        {
            if (worldEvent == null) return null;
            EnsureCached();
            if (_worldEventStateField == null) return null;

            try
            {
                return _worldEventStateField.GetValue(worldEvent);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetState failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // DATA ACCESS
        // ========================================

        /// <summary>
        /// Get the event's display name.
        /// </summary>
        public static string GetEventName(object model)
        {
            if (model == null) return null;
            EnsureCached();
            if (_modelDisplayNameField == null) return null;

            try
            {
                var locaText = _modelDisplayNameField.GetValue(model);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetEventName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the event's description.
        /// </summary>
        public static string GetEventDescription(object model)
        {
            if (model == null) return null;
            EnsureCached();
            if (_modelDescriptionField == null) return null;

            try
            {
                var locaText = _modelDescriptionField.GetValue(model);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetEventDescription failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the number of decision options.
        /// </summary>
        public static int GetOptionCount(object model)
        {
            if (model == null) return 0;
            EnsureCached();
            if (_modelOptionsField == null) return 0;

            try
            {
                var options = _modelOptionsField.GetValue(model) as Array;
                return options?.Length ?? 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetOptionCount failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get the description for a specific option.
        /// </summary>
        public static string GetOptionDescription(object model, int index)
        {
            if (model == null) return null;
            EnsureCached();
            if (_modelGetDescriptionForOptionMethod == null) return null;
            if (index < 0 || index >= GetOptionCount(model)) return null;

            try
            {
                _args1[0] = index;
                return _modelGetDescriptionForOptionMethod.Invoke(model, _args1) as string;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetOptionDescription failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if an option can be executed.
        /// </summary>
        public static bool CanExecuteOption(object model, int index)
        {
            if (model == null) return false;
            EnsureCached();
            if (_modelCanExecuteMethod == null) return false;
            if (index < 0 || index >= GetOptionCount(model)) return false;

            try
            {
                _args1[0] = index;
                var result = _modelCanExecuteMethod.Invoke(model, _args1);
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: CanExecuteOption failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the reason why an option cannot be executed.
        /// </summary>
        public static string GetExecutionBlockReason(object model, int index)
        {
            if (model == null) return null;
            EnsureCached();
            if (_modelGetExecutionBlockReasonMethod == null) return null;
            if (index < 0 || index >= GetOptionCount(model)) return null;

            try
            {
                _args1[0] = index;
                return _modelGetExecutionBlockReasonMethod.Invoke(model, _args1) as string;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] WorldEventReflection: GetExecutionBlockReason failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Execute the selected decision.
        /// Returns true if the action was triggered (async execution).
        /// </summary>
        public static bool ExecuteDecision(object model, object state, int index)
        {
            if (model == null || state == null) return false;
            EnsureCached();
            if (_modelExecuteDecisionMethod == null) return false;
            if (index < 0 || index >= GetOptionCount(model)) return false;

            try
            {
                // ExecuteDecision returns UniTask<bool>, just invoke it (fire and forget)
                // The game handles the async flow and will close the popup on success
                _args2[0] = state;
                _args2[1] = index;
                _modelExecuteDecisionMethod.Invoke(model, _args2);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WorldEventReflection: ExecuteDecision failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // REFLECTION CACHING
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _cached = true;
                return;
            }

            try
            {
                // Cache WorldEventPopup type
                _worldEventPopupType = assembly.GetType("Eremite.WorldMap.UI.WorldEvents.WorldEventPopup");
                if (_worldEventPopupType != null)
                {
                    _popupWorldEventField = _worldEventPopupType.GetField("worldEvent",
                        GameReflection.NonPublicInstance);
                    Debug.Log("[ATSAccessibility] WorldEventReflection: Cached WorldEventPopup type");
                }

                // Cache WorldEvent type
                var worldEventType = assembly.GetType("Eremite.WorldMap.Controllers.WorldEvent");
                if (worldEventType != null)
                {
                    _worldEventModelField = worldEventType.GetField("model",
                        GameReflection.PublicInstance | GameReflection.NonPublicInstance);
                    _worldEventStateField = worldEventType.GetField("state",
                        GameReflection.PublicInstance | GameReflection.NonPublicInstance);
                }

                // Cache WorldEventModel type
                var modelType = assembly.GetType("Eremite.Model.WorldEventModel");
                if (modelType != null)
                {
                    _modelDisplayNameField = modelType.GetField("displayName",
                        GameReflection.PublicInstance);
                    _modelDescriptionField = modelType.GetField("description",
                        GameReflection.PublicInstance);
                    _modelOptionsField = modelType.GetField("options",
                        GameReflection.PublicInstance);

                    _modelGetDescriptionForOptionMethod = modelType.GetMethod("GetDescriptionForOption",
                        GameReflection.PublicInstance,
                        null, new[] { typeof(int) }, null);
                    _modelCanExecuteMethod = modelType.GetMethod("CanExecute",
                        GameReflection.PublicInstance,
                        null, new[] { typeof(int) }, null);
                    _modelGetExecutionBlockReasonMethod = modelType.GetMethod("GetExecutionBlockReason",
                        GameReflection.PublicInstance,
                        null, new[] { typeof(int) }, null);

                    // ExecuteDecision takes WorldEventState and int
                    var stateType = assembly.GetType("Eremite.WorldMap.WorldEventState");
                    if (stateType != null)
                    {
                        _modelExecuteDecisionMethod = modelType.GetMethod("ExecuteDecision",
                            GameReflection.PublicInstance,
                            null, new[] { stateType, typeof(int) }, null);
                    }

                    Debug.Log("[ATSAccessibility] WorldEventReflection: Cached WorldEventModel methods");
                }

                Debug.Log("[ATSAccessibility] WorldEventReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WorldEventReflection: Type caching failed: {ex.Message}");
            }

            _cached = true;
        }
    }
}
