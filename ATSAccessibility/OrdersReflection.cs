using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to orders system internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class OrdersReflection
    {
        // ========================================
        // RICH TEXT STRIPPING
        // ========================================

        private static readonly Regex RichTextRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex ProductionBonusRegex = new Regex(
            @"(\+\d+) to (.+?) production \(from gathering, farming, fishing, or production\)\.?",
            RegexOptions.Compiled);

        public static string StripRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return RichTextRegex.Replace(text, "");
        }

        /// <summary>
        /// Strip trailing period from localized text (punctuation handled by overlays).
        /// </summary>
        private static string TrimObjectiveText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.TrimEnd('.');
        }

        /// <summary>
        /// Basic English pluralization of the last word in a name.
        /// </summary>
        private static string Pluralize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            int lastSpace = name.LastIndexOf(' ');
            string prefix = lastSpace >= 0 ? name.Substring(0, lastSpace + 1) : "";
            string word = lastSpace >= 0 ? name.Substring(lastSpace + 1) : name;

            if (word.Length == 0) return name;

            char last = word[word.Length - 1];
            if (last == 's' || last == 'x' || last == 'z')
                word += "es";
            else if (last == 'h' && word.Length >= 2 && (word[word.Length - 2] == 's' || word[word.Length - 2] == 'c'))
                word += "es";
            else if (last == 'y' && word.Length >= 2 && "aeiou".IndexOf(word[word.Length - 2]) < 0)
                word = word.Substring(0, word.Length - 1) + "ies";
            else
                word += "s";

            return prefix + word;
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // Popup type detection
        private static Type _ordersPopupType = null;
        private static Type _orderPickPopupType = null;

        // IGameServices properties
        private static PropertyInfo _gsOrdersServiceProperty = null;
        private static PropertyInfo _gsGameTimeServiceProperty = null;

        // IOrdersService methods/properties
        private static PropertyInfo _osOrdersProperty = null;
        private static MethodInfo _osCanCompleteMethod = null;
        private static MethodInfo _osCompleteOrderMethod = null;
        private static MethodInfo _osOrderPickedMethod = null;
        private static MethodInfo _osGetPicksForMethod = null;
        private static MethodInfo _osSwitchOrderTrackingMethod = null;
        private static MethodInfo _osGetCurrentlyPickedOrderMethod = null;

        // IGameTimeService.Time property
        private static PropertyInfo _gtsTimeProperty = null;

        // OrderState fields
        private static FieldInfo _osModelField = null;
        private static FieldInfo _osStartedField = null;
        private static FieldInfo _osPickedField = null;
        private static FieldInfo _osCompletedField = null;
        private static FieldInfo _osIsFailedField = null;
        private static FieldInfo _osTimeLeftField = null;
        private static FieldInfo _osTrackedField = null;
        private static FieldInfo _osObjectivesField = null;
        private static FieldInfo _osPicksField = null;
        private static FieldInfo _osStartTimeField = null;
        private static FieldInfo _osRewardsField = null;
        private static FieldInfo _osShouldBeFailableField = null;

        // OrderPickState fields
        private static FieldInfo _opsModelField = null;
        private static FieldInfo _opsSetIndexField = null;
        private static FieldInfo _opsFailedField = null;
        private static FieldInfo _opsRewardsField = null;

        // OrderModel fields/methods
        private static FieldInfo _omDisplayNameField = null;
        private static FieldInfo _omCanBeFailedField = null;
        private static FieldInfo _omTimeToFailField = null;
        private static FieldInfo _omReputationRewardField = null;
        private static FieldInfo _omUnlockAfterField = null;
        private static FieldInfo _omLogicsSetsField = null;
        private static MethodInfo _omGetLogicsMethod = null;       // GetLogics(OrderState)
        private static MethodInfo _omGetLogicsIntMethod = null;    // GetLogics(int)

        // OrderLogicsSet fields
        private static FieldInfo _olsLogicsField = null;

        // OrderLogic methods/properties
        private static MethodInfo _olGetObjectiveTextMethod = null;
        private static PropertyInfo _olDisplayNameProperty = null;
        private static PropertyInfo _olDescriptionProperty = null;
        private static MethodInfo _olGetAmountTextMethod = null;  // GetAmountText() no args
        private static MethodInfo _olIsCompletedMethod = null;
        private static PropertyInfo _olHasStoredAmountProperty = null;
        private static PropertyInfo _olGetStoredAmountProperty = null;
        private static MethodInfo _olGetWarningTextMethod = null;

        // EffectModel properties
        private static PropertyInfo _emDisplayNameProperty = null;
        private static PropertyInfo _emDescriptionProperty = null;
        private static MethodInfo _emGetAmountTextMethod = null;

        // Settings.GetOrder method
        private static MethodInfo _settingsGetOrderMethod = null;

        // Settings.GetEffect method
        private static MethodInfo _settingsGetEffectMethod = null;

        // GameBlackboardService.OrderPickPopupRequested property
        private static PropertyInfo _gbbOrderPickPopupRequestedProperty = null;

        // OrderPickPopup.order field (the order being shown in the pick popup)
        private static FieldInfo _oppOrderField = null;

        // Popup.Hide method
        private static MethodInfo _popupHideMethod = null;

        // Subject<T>.OnNext method (for firing events)
        private static MethodInfo _subjectOnNextMethod = null;

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
                    Debug.LogWarning("[ATSAccessibility] OrdersReflection: Game assembly not available");
                    return;
                }

                CachePopupTypes(assembly);
                CacheServiceTypes(assembly);
                CacheOrderStateTypes(assembly);
                CacheOrderPickStateTypes(assembly);
                CacheOrderModelTypes(assembly);
                CacheOrderLogicTypes(assembly);
                CacheEffectModelTypes(assembly);
                CacheSettingsTypes(assembly);
                CacheBlackboardTypes(assembly);

                Debug.Log("[ATSAccessibility] OrdersReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OrdersReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            _ordersPopupType = assembly.GetType("Eremite.View.HUD.Orders.OrdersPopup");
            _orderPickPopupType = assembly.GetType("Eremite.View.HUD.Orders.OrderPickPopup");

            if (_orderPickPopupType != null)
            {
                _oppOrderField = _orderPickPopupType.GetField("order", GameReflection.NonPublicInstance);
            }

            var popupType = assembly.GetType("Eremite.View.Popups.Popup");
            if (popupType != null)
            {
                _popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
            }
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsOrdersServiceProperty = gameServicesType.GetProperty("OrdersService", GameReflection.PublicInstance);
                _gsGameTimeServiceProperty = gameServicesType.GetProperty("GameTimeService", GameReflection.PublicInstance);
            }

            var osType = assembly.GetType("Eremite.Services.IOrdersService");
            if (osType != null)
            {
                _osOrdersProperty = osType.GetProperty("Orders", GameReflection.PublicInstance);
                _osCanCompleteMethod = osType.GetMethod("CanComplete", GameReflection.PublicInstance);
                _osGetPicksForMethod = osType.GetMethod("GetPicksFor", GameReflection.PublicInstance);
                _osSwitchOrderTrackingMethod = osType.GetMethod("SwitchOrderTracking", GameReflection.PublicInstance);
                _osGetCurrentlyPickedOrderMethod = osType.GetMethod("GetCurrentlyPickedOrder", GameReflection.PublicInstance);
                _osOrderPickedMethod = osType.GetMethod("OrderPicked", GameReflection.PublicInstance);

                // CompleteOrder(OrderState, OrderModel, bool force) - we'll pass force=false
                var orderStateType = assembly.GetType("Eremite.Model.Orders.OrderState");
                var orderModelType = assembly.GetType("Eremite.Model.Orders.OrderModel");
                if (orderStateType != null && orderModelType != null)
                {
                    _osCompleteOrderMethod = osType.GetMethod("CompleteOrder",
                        new[] { orderStateType, orderModelType, typeof(bool) });
                }
            }

            var gtsType = assembly.GetType("Eremite.Services.IGameTimeService");
            if (gtsType != null)
            {
                _gtsTimeProperty = gtsType.GetProperty("Time", GameReflection.PublicInstance);
            }
        }

        private static void CacheOrderStateTypes(Assembly assembly)
        {
            var type = assembly.GetType("Eremite.Model.Orders.OrderState");
            if (type != null)
            {
                _osModelField = type.GetField("model", GameReflection.PublicInstance);
                _osPickedField = type.GetField("picked", GameReflection.PublicInstance);
                _osCompletedField = type.GetField("completed", GameReflection.PublicInstance);
                _osIsFailedField = type.GetField("isFailed", GameReflection.PublicInstance);
                _osTimeLeftField = type.GetField("timeLeft", GameReflection.PublicInstance);
                _osTrackedField = type.GetField("tracked", GameReflection.PublicInstance);
                _osPicksField = type.GetField("picks", GameReflection.PublicInstance);
                _osRewardsField = type.GetField("rewards", GameReflection.PublicInstance);
                _osShouldBeFailableField = type.GetField("shouldBeFailable", GameReflection.PublicInstance);
            }

            // Inherited from BaseOrderState
            var baseType = assembly.GetType("Eremite.Model.Orders.BaseOrderState");
            if (baseType != null)
            {
                _osStartedField = baseType.GetField("started", GameReflection.PublicInstance);
                _osObjectivesField = baseType.GetField("objectives", GameReflection.PublicInstance);
                _osStartTimeField = baseType.GetField("startTime", GameReflection.PublicInstance);
            }
        }

        private static void CacheOrderPickStateTypes(Assembly assembly)
        {
            var type = assembly.GetType("Eremite.Model.Orders.OrderPickState");
            if (type != null)
            {
                _opsModelField = type.GetField("model", GameReflection.PublicInstance);
                _opsSetIndexField = type.GetField("setIndex", GameReflection.PublicInstance);
                _opsFailedField = type.GetField("failed", GameReflection.PublicInstance);
                _opsRewardsField = type.GetField("rewards", GameReflection.PublicInstance);
            }
        }

        private static void CacheOrderModelTypes(Assembly assembly)
        {
            var type = assembly.GetType("Eremite.Model.Orders.OrderModel");
            if (type != null)
            {
                _omDisplayNameField = type.GetField("displayName", GameReflection.PublicInstance);
                _omCanBeFailedField = type.GetField("canBeFailed", GameReflection.PublicInstance);
                _omTimeToFailField = type.GetField("timeToFail", GameReflection.PublicInstance);
                _omReputationRewardField = type.GetField("reputationReward", GameReflection.PublicInstance);
                _omUnlockAfterField = type.GetField("unlockAfter", GameReflection.PublicInstance);
                _omLogicsSetsField = type.GetField("logicsSets", GameReflection.PublicInstance);

                // GetLogics(OrderState)
                var orderStateType = assembly.GetType("Eremite.Model.Orders.OrderState");
                if (orderStateType != null)
                {
                    _omGetLogicsMethod = type.GetMethod("GetLogics", new[] { orderStateType });
                }

                // GetLogics(int)
                _omGetLogicsIntMethod = type.GetMethod("GetLogics", new[] { typeof(int) });
            }

            // OrderLogicsSet.logics
            var olsType = assembly.GetType("Eremite.Model.Orders.OrderLogicsSet");
            if (olsType != null)
            {
                _olsLogicsField = olsType.GetField("logics", GameReflection.PublicInstance);
            }
        }

        private static void CacheOrderLogicTypes(Assembly assembly)
        {
            var type = assembly.GetType("Eremite.Model.Orders.OrderLogic");
            if (type != null)
            {
                // GetObjectiveText(ObjectiveState)
                var objStateType = assembly.GetType("Eremite.Model.Orders.ObjectiveState");
                if (objStateType != null)
                {
                    _olGetObjectiveTextMethod = type.GetMethod("GetObjectiveText", new[] { objStateType });
                    _olIsCompletedMethod = type.GetMethod("IsCompleted", new[] { objStateType });
                }

                _olDisplayNameProperty = type.GetProperty("DisplayName", GameReflection.PublicInstance);
                _olDescriptionProperty = type.GetProperty("Description", GameReflection.PublicInstance);
                _olHasStoredAmountProperty = type.GetProperty("HasStoredAmount", GameReflection.PublicInstance);
                _olGetStoredAmountProperty = type.GetProperty("GetStoredAmount", GameReflection.PublicInstance);
                _olGetWarningTextMethod = type.GetMethod("GetWarningText", Type.EmptyTypes);

                // GetAmountText() - no parameters
                _olGetAmountTextMethod = type.GetMethod("GetAmountText", Type.EmptyTypes);
            }
        }

        private static void CacheEffectModelTypes(Assembly assembly)
        {
            var type = assembly.GetType("Eremite.Model.EffectModel");
            if (type != null)
            {
                _emDisplayNameProperty = type.GetProperty("DisplayName", GameReflection.PublicInstance);
                _emDescriptionProperty = type.GetProperty("Description", GameReflection.PublicInstance);
                _emGetAmountTextMethod = type.GetMethod("GetAmountText", Type.EmptyTypes);
            }
        }

        private static void CacheSettingsTypes(Assembly assembly)
        {
            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsGetOrderMethod = settingsType.GetMethod("GetOrder", new[] { typeof(string) });
                _settingsGetEffectMethod = settingsType.GetMethod("GetEffect", new[] { typeof(string) });
            }
        }

        private static void CacheBlackboardTypes(Assembly assembly)
        {
            var gbbType = assembly.GetType("Eremite.Services.IGameBlackboardService");
            if (gbbType != null)
            {
                _gbbOrderPickPopupRequestedProperty = gbbType.GetProperty("OrderPickPopupRequested", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // TYPE DETECTION
        // ========================================

        public static bool IsOrdersPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            return _ordersPopupType != null && _ordersPopupType.IsInstanceOfType(popup);
        }

        public static bool IsOrderPickPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            return _orderPickPopupType != null && _orderPickPopupType.IsInstanceOfType(popup);
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetOrdersService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsOrdersServiceProperty == null) return null;
            try { return _gsOrdersServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetGameTimeService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsGameTimeServiceProperty == null) return null;
            try { return _gsGameTimeServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        // ========================================
        // DATA ACCESS
        // ========================================

        /// <summary>
        /// Get the list of current orders (OrderState objects).
        /// </summary>
        public static IList GetOrders()
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osOrdersProperty == null) return null;
            try { return _osOrdersProperty.GetValue(service) as IList; }
            catch { return null; }
        }

        /// <summary>
        /// Get the OrderModel from Settings for a given OrderState.
        /// </summary>
        public static object GetOrderModel(object orderState)
        {
            EnsureCached();
            if (orderState == null || _osModelField == null) return null;

            try
            {
                var modelName = _osModelField.GetValue(orderState) as string;
                if (string.IsNullOrEmpty(modelName)) return null;

                var settings = GameReflection.GetSettings();
                if (settings == null || _settingsGetOrderMethod == null) return null;

                _args1[0] = modelName;
                return _settingsGetOrderMethod.Invoke(settings, _args1);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the display name of an OrderModel.
        /// </summary>
        public static string GetOrderDisplayName(object orderModel)
        {
            if (orderModel == null || _omDisplayNameField == null) return null;
            try
            {
                var locaText = _omDisplayNameField.GetValue(orderModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch { return null; }
        }

        // ========================================
        // ORDER STATE QUERIES
        // ========================================

        public static bool IsStarted(object orderState)
        {
            if (orderState == null || _osStartedField == null) return false;
            try { return (bool)_osStartedField.GetValue(orderState); }
            catch { return false; }
        }

        public static bool IsPicked(object orderState)
        {
            if (orderState == null || _osPickedField == null) return false;
            try { return (bool)_osPickedField.GetValue(orderState); }
            catch { return false; }
        }

        public static bool IsCompleted(object orderState)
        {
            if (orderState == null || _osCompletedField == null) return false;
            try { return (bool)_osCompletedField.GetValue(orderState); }
            catch { return false; }
        }

        public static bool IsFailed(object orderState)
        {
            if (orderState == null || _osIsFailedField == null) return false;
            try { return (bool)_osIsFailedField.GetValue(orderState); }
            catch { return false; }
        }

        public static bool IsTracked(object orderState)
        {
            if (orderState == null || _osTrackedField == null) return false;
            try { return (bool)_osTrackedField.GetValue(orderState); }
            catch { return false; }
        }

        public static float GetTimeLeft(object orderState)
        {
            if (orderState == null || _osTimeLeftField == null) return 0f;
            try { return (float)_osTimeLeftField.GetValue(orderState); }
            catch { return 0f; }
        }

        public static float GetStartTime(object orderState)
        {
            if (orderState == null || _osStartTimeField == null) return 0f;
            try { return (float)_osStartTimeField.GetValue(orderState); }
            catch { return 0f; }
        }

        public static bool CanBeFailed(object orderModel)
        {
            if (orderModel == null || _omCanBeFailedField == null) return false;
            try { return (bool)_omCanBeFailedField.GetValue(orderModel); }
            catch { return false; }
        }

        public static bool IsShouldBeFailable(object orderState)
        {
            if (orderState == null || _osShouldBeFailableField == null) return false;
            try { return (bool)_osShouldBeFailableField.GetValue(orderState); }
            catch { return false; }
        }

        public static float GetTimeToFail(object orderModel)
        {
            if (orderModel == null || _omTimeToFailField == null) return 0f;
            try { return (float)_omTimeToFailField.GetValue(orderModel); }
            catch { return 0f; }
        }

        public static bool HasUnlockAfter(object orderModel)
        {
            if (orderModel == null || _omUnlockAfterField == null) return false;
            try { return _omUnlockAfterField.GetValue(orderModel) != null; }
            catch { return false; }
        }

        public static string GetUnlockAfterName(object orderModel)
        {
            if (orderModel == null || _omUnlockAfterField == null) return null;
            try
            {
                var unlockAfter = _omUnlockAfterField.GetValue(orderModel);
                if (unlockAfter == null) return null;
                return GetOrderDisplayName(unlockAfter);
            }
            catch { return null; }
        }

        // ========================================
        // GAME TIME
        // ========================================

        public static float GetGameTime()
        {
            EnsureCached();
            var service = GetGameTimeService();
            if (service == null || _gtsTimeProperty == null) return 0f;
            try { return (float)_gtsTimeProperty.GetValue(service); }
            catch { return 0f; }
        }

        // ========================================
        // OBJECTIVES
        // ========================================

        /// <summary>
        /// Get objective texts for an active order (has ObjectiveState).
        /// Returns stripped rich text like "3/10 Planks".
        /// </summary>
        public static List<string> GetObjectiveTexts(object orderModel, object orderState)
        {
            EnsureCached();
            var result = new List<string>();
            if (orderModel == null || orderState == null) return result;

            try
            {
                // Get logics via GetLogics(OrderState)
                if (_omGetLogicsMethod == null) return result;
                _args1[0] = orderState;
                var logics = _omGetLogicsMethod.Invoke(orderModel, _args1) as Array;
                if (logics == null) return result;

                // Get objectives array from state
                var objectives = _osObjectivesField?.GetValue(orderState) as Array;

                for (int i = 0; i < logics.Length; i++)
                {
                    var logic = logics.GetValue(i);
                    if (logic == null) continue;

                    string text = null;
                    if (_olGetObjectiveTextMethod != null && objectives != null && i < objectives.Length)
                    {
                        var objState = objectives.GetValue(i);
                        _args1[0] = objState;
                        text = _olGetObjectiveTextMethod.Invoke(logic, _args1) as string;
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        result.Add(TrimObjectiveText(StripRichText(text)));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetObjectiveTexts failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get objective texts for a pick option (no ObjectiveState yet).
        /// Uses DisplayName + GetAmountText() from each logic.
        /// </summary>
        public static List<string> GetPickObjectiveTexts(object orderModel, int setIndex)
        {
            EnsureCached();
            var result = new List<string>();
            if (orderModel == null) return result;

            try
            {
                Array logics = null;

                // Try GetLogics(int setIndex)
                if (_omGetLogicsIntMethod != null)
                {
                    _args1[0] = setIndex;
                    logics = _omGetLogicsIntMethod.Invoke(orderModel, _args1) as Array;
                }

                if (logics == null) return result;

                foreach (var logic in logics)
                {
                    if (logic == null) continue;

                    string displayName = _olDisplayNameProperty?.GetValue(logic) as string;
                    if (string.IsNullOrEmpty(displayName)) continue;

                    // Some logics embed the amount in DisplayName (e.g. "Deliver 10 Amber"),
                    // others keep it separate (e.g. "Produce Pipes" + amount "6").
                    // For the latter, use the Description property which formats correctly
                    // (e.g. "Produce 6 Pipes").
                    string amountText = null;
                    if (_olGetAmountTextMethod != null)
                        amountText = _olGetAmountTextMethod.Invoke(logic, null) as string;

                    if (!string.IsNullOrEmpty(amountText))
                    {
                        string stripped = StripRichText(amountText);
                        if (!string.IsNullOrEmpty(stripped) && !displayName.Contains(stripped))
                        {
                            // Amount not in DisplayName - use type-specific formatting
                            string typeName = logic.GetType().Name;
                            bool skipDescription = typeName.Contains("Building") || typeName == "GoodLogic";

                            if (!skipDescription)
                            {
                                // Try Description which may have proper localized placement
                                // (e.g. "Complete 2 events")
                                string desc = _olDescriptionProperty?.GetValue(logic) as string;
                                string strippedDesc = !string.IsNullOrEmpty(desc) ? StripRichText(desc).Trim() : null;

                                if (!string.IsNullOrEmpty(strippedDesc) && strippedDesc.Contains(stripped))
                                {
                                    // Description has the amount placed by localization
                                    // Truncate multi-sentence descriptions (upgrade tutorial text)
                                    int sentenceEnd = strippedDesc.IndexOf(". ", StringComparison.Ordinal);
                                    if (sentenceEnd >= 0)
                                        strippedDesc = strippedDesc.Substring(0, sentenceEnd);
                                    result.Add(TrimObjectiveText(strippedDesc));
                                    continue;
                                }
                            }

                            // Fallback: type-specific formatting
                            // For building types, prefix with "Build" (e.g. "Build 3 Shelter")
                            // For verb+noun patterns, insert after first word (e.g. "Produce 6 Pipes")
                            int spaceIdx = displayName.IndexOf(' ');
                            if (typeName.Contains("Building"))
                            {
                                int amount = 0;
                                int.TryParse(stripped, out amount);
                                string name = amount > 1 ? Pluralize(displayName) : displayName;
                                result.Add(TrimObjectiveText($"Build {stripped} {name}"));
                            }
                            else if (spaceIdx > 0)
                                result.Add(TrimObjectiveText($"{displayName.Substring(0, spaceIdx)} {stripped} {displayName.Substring(spaceIdx + 1)}"));
                            else
                                result.Add(TrimObjectiveText($"{stripped} {displayName}"));
                        }
                        else
                        {
                            result.Add(TrimObjectiveText(displayName));
                        }
                    }
                    else
                    {
                        result.Add(TrimObjectiveText(displayName));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetPickObjectiveTexts failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get stored amounts for pick objectives (e.g. "Planks: 5 in storage").
        /// Only returns entries for objectives that have stored amounts.
        /// </summary>
        public static List<string> GetPickStoredAmounts(object orderModel, int setIndex)
        {
            EnsureCached();
            var result = new List<string>();
            if (orderModel == null || _olHasStoredAmountProperty == null || _olGetStoredAmountProperty == null)
                return result;

            try
            {
                Array logics = null;
                if (_omGetLogicsIntMethod != null)
                {
                    _args1[0] = setIndex;
                    logics = _omGetLogicsIntMethod.Invoke(orderModel, _args1) as Array;
                }
                if (logics == null) return result;

                foreach (var logic in logics)
                {
                    if (logic == null) continue;

                    bool hasStored = (bool)_olHasStoredAmountProperty.GetValue(logic);
                    if (!hasStored) continue;

                    int stored = (int)_olGetStoredAmountProperty.GetValue(logic);
                    string displayName = _olDisplayNameProperty?.GetValue(logic) as string;
                    if (string.IsNullOrEmpty(displayName)) continue;

                    result.Add($"{displayName}: {stored}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetPickStoredAmounts failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get warning texts for pick objectives (e.g. missing building warnings).
        /// Only returns non-null warnings.
        /// </summary>
        public static List<string> GetPickWarningTexts(object orderModel, int setIndex)
        {
            EnsureCached();
            var result = new List<string>();
            if (orderModel == null || _olGetWarningTextMethod == null) return result;

            try
            {
                Array logics = null;
                if (_omGetLogicsIntMethod != null)
                {
                    _args1[0] = setIndex;
                    logics = _omGetLogicsIntMethod.Invoke(orderModel, _args1) as Array;
                }
                if (logics == null) return result;

                foreach (var logic in logics)
                {
                    if (logic == null) continue;

                    string warning = _olGetWarningTextMethod.Invoke(logic, null) as string;
                    if (string.IsNullOrEmpty(warning)) continue;

                    string stripped = StripRichText(warning).Trim();
                    if (!string.IsNullOrEmpty(stripped))
                        result.Add(stripped);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetPickWarningTexts failed: {ex.Message}");
            }

            return result;
        }

        // ========================================
        // REWARDS
        // ========================================

        /// <summary>
        /// Get reward display texts for an active order's rewards list.
        /// </summary>
        public static List<string> GetRewardTexts(object orderState)
        {
            EnsureCached();
            return ResolveEffectNames(GetRewardsList(orderState));
        }

        /// <summary>
        /// Get reward display texts for a pick option's rewards list.
        /// </summary>
        public static List<string> GetPickRewardTexts(object pickState)
        {
            EnsureCached();
            if (pickState == null || _opsRewardsField == null) return new List<string>();
            var rewards = _opsRewardsField.GetValue(pickState) as IList;
            return ResolveEffectNames(rewards);
        }

        /// <summary>
        /// Get the reputation reward text for an order model.
        /// </summary>
        public static string GetReputationRewardText(object orderModel)
        {
            EnsureCached();
            if (orderModel == null || _omReputationRewardField == null) return null;

            try
            {
                var effectModel = _omReputationRewardField.GetValue(orderModel);
                if (effectModel == null) return null;

                string amountText = null;
                if (_emGetAmountTextMethod != null)
                    amountText = _emGetAmountTextMethod.Invoke(effectModel, null) as string;

                if (!string.IsNullOrEmpty(amountText))
                    return $"{StripRichText(amountText)} Reputation";
                return "1 Reputation";
            }
            catch { return null; }
        }

        private static IList GetRewardsList(object orderState)
        {
            if (orderState == null || _osRewardsField == null) return null;
            try { return _osRewardsField.GetValue(orderState) as IList; }
            catch { return null; }
        }

        private static List<string> ResolveEffectNames(IList effectNames)
        {
            var result = new List<string>();
            if (effectNames == null) return result;

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsGetEffectMethod == null) return result;

            foreach (var nameObj in effectNames)
            {
                var name = nameObj as string;
                if (string.IsNullOrEmpty(name)) continue;

                try
                {
                    _args1[0] = name;
                    var effectModel = _settingsGetEffectMethod.Invoke(settings, _args1);
                    if (effectModel == null) continue;

                    // Building blueprints have verbose descriptions listing productions;
                    // use DisplayName (just the building name) for those.
                    bool isBlueprint = name.Contains("Blueprint");
                    string text = GetEffectDisplayText(effectModel, isBlueprint);
                    if (!string.IsNullOrEmpty(text))
                        result.Add(isBlueprint ? $"Blueprint: {text}" : text);
                }
                catch { }
            }

            return result;
        }

        private static string GetEffectDisplayText(object effectModel, bool useDisplayName = false)
        {
            if (effectModel == null) return null;

            try
            {
                string displayName = _emDisplayNameProperty?.GetValue(effectModel) as string;

                if (useDisplayName)
                    return displayName;

                // Prefer Description (tooltip text) - gives actual mechanical meaning
                string description = _emDescriptionProperty?.GetValue(effectModel) as string;
                if (!string.IsNullOrEmpty(description))
                {
                    string stripped = StripRichText(description).Trim();
                    if (!string.IsNullOrEmpty(stripped))
                    {
                        // Simplify verbose production bonus descriptions
                        stripped = ProductionBonusRegex.Replace(stripped, "$1 bonus to $2 production");
                        return TrimObjectiveText(stripped);
                    }
                }

                // Fallback to DisplayName + amount
                string amountText = null;
                if (_emGetAmountTextMethod != null)
                {
                    amountText = _emGetAmountTextMethod.Invoke(effectModel, null) as string;
                }

                if (!string.IsNullOrEmpty(amountText))
                    return StripRichText($"{amountText} {displayName}");
                return displayName;
            }
            catch { return null; }
        }

        // ========================================
        // ACTIONS
        // ========================================

        /// <summary>
        /// Check if an order can be completed (delivered).
        /// </summary>
        public static bool CanComplete(object orderState, object orderModel)
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osCanCompleteMethod == null) return false;

            try
            {
                _args2[0] = orderState;
                _args2[1] = orderModel;
                return (bool)_osCanCompleteMethod.Invoke(service, _args2);
            }
            catch { return false; }
        }

        /// <summary>
        /// Complete (deliver) an order. Returns false if method not found.
        /// </summary>
        public static bool CompleteOrder(object orderState, object orderModel)
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osCompleteOrderMethod == null) return false;

            try
            {
                _args3[0] = orderState;
                _args3[1] = orderModel;
                _args3[2] = false;  // force = false
                _osCompleteOrderMethod.Invoke(service, _args3);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] CompleteOrder failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pick an order option. Returns false if method not found.
        /// </summary>
        public static bool PickOrder(object orderState, object pickState)
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osOrderPickedMethod == null) return false;

            try
            {
                _args2[0] = orderState;
                _args2[1] = pickState;
                _osOrderPickedMethod.Invoke(service, _args2);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] PickOrder failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggle order tracking. Returns false if method not found.
        /// </summary>
        public static bool ToggleTracking(object orderState)
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osSwitchOrderTrackingMethod == null) return false;

            try
            {
                bool currentlyTracked = IsTracked(orderState);
                _args2[0] = orderState;
                _args2[1] = !currentlyTracked;
                _osSwitchOrderTrackingMethod.Invoke(service, _args2);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ToggleTracking failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Fire the OrderPickPopupRequested event to open the pick popup.
        /// </summary>
        public static bool FireOrderPickPopupRequested(object orderState)
        {
            EnsureCached();
            if (orderState == null || _gbbOrderPickPopupRequestedProperty == null) return false;

            try
            {
                var blackboard = GameReflection.GetGameBlackboardService();
                if (blackboard == null) return false;

                var subject = _gbbOrderPickPopupRequestedProperty.GetValue(blackboard);
                if (subject == null) return false;

                // Get OnNext method from the subject
                if (_subjectOnNextMethod == null)
                {
                    _subjectOnNextMethod = subject.GetType().GetMethod("OnNext", GameReflection.PublicInstance);
                }
                if (_subjectOnNextMethod == null) return false;

                _args1[0] = orderState;
                _subjectOnNextMethod.Invoke(subject, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] FireOrderPickPopupRequested failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the OrderState from the OrderPickPopup's private 'order' field.
        /// </summary>
        public static object GetPopupOrder(object popup)
        {
            EnsureCached();
            if (popup == null || _oppOrderField == null) return null;
            try { return _oppOrderField.GetValue(popup); }
            catch { return null; }
        }

        /// <summary>
        /// Hide a popup by calling Popup.Hide().
        /// </summary>
        public static bool HidePopup(object popup)
        {
            EnsureCached();
            if (popup == null || _popupHideMethod == null) return false;

            try
            {
                _popupHideMethod.Invoke(popup, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] HidePopup failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PICK ACCESS
        // ========================================

        /// <summary>
        /// Get available picks for an order.
        /// </summary>
        public static IList GetPicksFor(object orderState)
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osGetPicksForMethod == null) return null;

            try
            {
                _args1[0] = orderState;
                return _osGetPicksForMethod.Invoke(service, _args1) as IList;
            }
            catch { return null; }
        }

        /// <summary>
        /// Get the order state that is currently awaiting a pick.
        /// </summary>
        public static object GetCurrentlyPickedOrder()
        {
            EnsureCached();
            var service = GetOrdersService();
            if (service == null || _osGetCurrentlyPickedOrderMethod == null) return null;

            try { return _osGetCurrentlyPickedOrderMethod.Invoke(service, null); }
            catch { return null; }
        }

        // ========================================
        // PICK STATE ACCESS
        // ========================================

        public static string GetPickModel(object pickState)
        {
            if (pickState == null || _opsModelField == null) return null;
            try { return _opsModelField.GetValue(pickState) as string; }
            catch { return null; }
        }

        public static int GetPickSetIndex(object pickState)
        {
            if (pickState == null || _opsSetIndexField == null) return 0;
            try { return (int)_opsSetIndexField.GetValue(pickState); }
            catch { return 0; }
        }

        public static bool IsPickFailed(object pickState)
        {
            if (pickState == null || _opsFailedField == null) return false;
            try { return (bool)_opsFailedField.GetValue(pickState); }
            catch { return false; }
        }

        /// <summary>
        /// Get the OrderModel for a pick state (resolves pick.model via Settings).
        /// </summary>
        public static object GetPickOrderModel(object pickState)
        {
            EnsureCached();
            var modelName = GetPickModel(pickState);
            if (string.IsNullOrEmpty(modelName)) return null;

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsGetOrderMethod == null) return null;

            try
            {
                _args1[0] = modelName;
                return _settingsGetOrderMethod.Invoke(settings, _args1);
            }
            catch { return null; }
        }

        // ========================================
        // FORMATTING
        // ========================================

        /// <summary>
        /// Format seconds into mm:ss or hh:mm:ss string.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0:00";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return ts.ToString(@"h\:mm\:ss");
            return ts.ToString(@"m\:ss");
        }
    }
}
