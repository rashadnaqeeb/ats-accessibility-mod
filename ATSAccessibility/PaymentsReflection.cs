using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to payments/obligations internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class PaymentsReflection
    {
        // ========================================
        // PAYMENT INFO STRUCT
        // ========================================

        public struct PaymentInfo
        {
            public object State;           // PaymentState
            public string TypeLabel;       // "Tax", "Tithe", etc.
            public string SourceLabel;     // Source description
            public string GoodName;        // Display name
            public int GoodAmount;
            public int DueYear;
            public string DueSeason;       // "Drizzle", "Clearance", "Storm"
            public float TimeRemaining;    // Seconds
            public int AutoPaymentType;    // 0=None, 1=Instant, 2=End
            public bool CanPay;
            public string PenaltyDesc;     // Penalty effect description
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // Popup type detection
        private static Type _paymentsPopupType = null;

        // IGameServices properties
        private static PropertyInfo _gsPaymentsServiceProperty = null;
        private static PropertyInfo _gsCalendarServiceProperty = null;
        private static PropertyInfo _gsStateServiceProperty = null;
        private static PropertyInfo _gsGameModelServiceProperty = null;

        // IPaymentsService methods
        private static MethodInfo _payMethod = null;
        private static MethodInfo _canPayMethod = null;
        private static MethodInfo _getModelMethod = null;

        // PaymentState fields
        private static FieldInfo _psPaymentField = null;        // Good payment
        private static FieldInfo _psDueDateField = null;        // GameDate dueDate
        private static FieldInfo _psAutoPaymentTypeField = null; // AutoPaymentType
        private static FieldInfo _psModelField = null;          // string model
        private static FieldInfo _psPenaltyModelField = null;   // string penaltyModel

        // Good struct fields
        private static FieldInfo _goodNameField = null;
        private static FieldInfo _goodAmountField = null;

        // GameDate fields
        private static FieldInfo _gdYearField = null;
        private static FieldInfo _gdSeasonField = null;

        // PaymentEffectModel fields
        private static FieldInfo _pemTypeLabelField = null;     // LabelModel typeLabel
        private static FieldInfo _pemSourceLabelField = null;   // LabelModel sourceLabel

        // LabelModel fields
        private static FieldInfo _lmDisplayNameField = null;    // LocaText displayName

        // CalendarService method
        private static MethodInfo _calGetSecondsLeftToMethod = null;

        // StateService access
        private static PropertyInfo _ssEffectsProperty = null;
        private static FieldInfo _esPaymentsField = null;

        // EffectModel access for penalty description
        private static MethodInfo _gmsGetEffectMethod = null;
        private static PropertyInfo _emDescriptionProperty = null;

        // Cached types
        private static Type _paymentStateType = null;
        private static Type _gameDateType = null;

        // Pre-allocated args
        private static readonly object[] _args1 = new object[1];

        // Season names
        private static readonly string[] _seasonNames = { "Drizzle", "Clearance", "Storm" };

        // Auto-payment labels
        private static readonly string[] _autoPaymentLabels = { "none", "instant", "last minute" };

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
                    Debug.LogWarning("[ATSAccessibility] PaymentsReflection: Game assembly not available");
                    return;
                }

                CachePopupTypes(assembly);
                CacheServiceTypes(assembly);
                CachePaymentStateTypes(assembly);
                CacheGoodTypes(assembly);
                CacheGameDateTypes(assembly);
                CachePaymentModelTypes(assembly);
                CacheStateTypes(assembly);
                CacheEffectTypes(assembly);

                Debug.Log("[ATSAccessibility] PaymentsReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PaymentsReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupTypes(Assembly assembly)
        {
            _paymentsPopupType = assembly.GetType("Eremite.View.Popups.Recipes.PaymentsPopup");
            if (_paymentsPopupType == null)
                Debug.LogWarning("[ATSAccessibility] PaymentsReflection: PaymentsPopup type not found");
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsPaymentsServiceProperty = gameServicesType.GetProperty("PaymentsService", GameReflection.PublicInstance);
                _gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService", GameReflection.PublicInstance);
                _gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
                _gsGameModelServiceProperty = gameServicesType.GetProperty("GameModelService", GameReflection.PublicInstance);
            }

            // IPaymentsService methods
            var paymentsServiceType = assembly.GetType("Eremite.Services.IPaymentsService");
            _paymentStateType = assembly.GetType("Eremite.Model.State.PaymentState");

            if (paymentsServiceType != null && _paymentStateType != null)
            {
                _payMethod = paymentsServiceType.GetMethod("Pay", new Type[] { _paymentStateType });
                _canPayMethod = paymentsServiceType.GetMethod("CanPay", new Type[] { _paymentStateType });
                _getModelMethod = paymentsServiceType.GetMethod("GetModel", new Type[] { _paymentStateType });
            }

            // ICalendarService.GetSecondsLeftTo
            var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
            _gameDateType = assembly.GetType("Eremite.Model.State.GameDate");
            if (calendarServiceType != null && _gameDateType != null)
            {
                _calGetSecondsLeftToMethod = calendarServiceType.GetMethod("GetSecondsLeftTo",
                    new Type[] { _gameDateType });
            }
        }

        private static void CachePaymentStateTypes(Assembly assembly)
        {
            if (_paymentStateType == null)
                _paymentStateType = assembly.GetType("Eremite.Model.State.PaymentState");

            if (_paymentStateType != null)
            {
                _psPaymentField = _paymentStateType.GetField("payment", GameReflection.PublicInstance);
                _psDueDateField = _paymentStateType.GetField("dueDate", GameReflection.PublicInstance);
                _psAutoPaymentTypeField = _paymentStateType.GetField("autoPaymentType", GameReflection.PublicInstance);
                _psModelField = _paymentStateType.GetField("model", GameReflection.PublicInstance);
                _psPenaltyModelField = _paymentStateType.GetField("penaltyModel", GameReflection.PublicInstance);
            }
        }

        private static void CacheGoodTypes(Assembly assembly)
        {
            var goodType = assembly.GetType("Eremite.Model.Good");
            if (goodType != null)
            {
                _goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
                _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
            }
        }

        private static void CacheGameDateTypes(Assembly assembly)
        {
            if (_gameDateType == null)
                _gameDateType = assembly.GetType("Eremite.Model.State.GameDate");

            if (_gameDateType != null)
            {
                _gdYearField = _gameDateType.GetField("year", GameReflection.PublicInstance);
                _gdSeasonField = _gameDateType.GetField("season", GameReflection.PublicInstance);
            }
        }

        private static void CachePaymentModelTypes(Assembly assembly)
        {
            var paymentEffectModelType = assembly.GetType("Eremite.Model.Effects.Payment.PaymentEffectModel");
            if (paymentEffectModelType != null)
            {
                _pemTypeLabelField = paymentEffectModelType.GetField("typeLabel", GameReflection.PublicInstance);
                _pemSourceLabelField = paymentEffectModelType.GetField("sourceLabel", GameReflection.PublicInstance);
            }

            var labelModelType = assembly.GetType("Eremite.Model.LabelModel");
            if (labelModelType != null)
            {
                _lmDisplayNameField = labelModelType.GetField("displayName", GameReflection.PublicInstance);
            }
        }

        private static void CacheStateTypes(Assembly assembly)
        {
            var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
            if (stateServiceType != null)
            {
                _ssEffectsProperty = stateServiceType.GetProperty("Effects", GameReflection.PublicInstance);
            }

            var effectsStateType = assembly.GetType("Eremite.Model.State.EffectsState");
            if (effectsStateType != null)
            {
                _esPaymentsField = effectsStateType.GetField("payments", GameReflection.PublicInstance);
            }
        }

        private static void CacheEffectTypes(Assembly assembly)
        {
            var gameModelServiceType = assembly.GetType("Eremite.Services.IGameModelService");
            if (gameModelServiceType != null)
            {
                _gmsGetEffectMethod = gameModelServiceType.GetMethod("GetEffect", new Type[] { typeof(string) });
            }

            var effectModelType = assembly.GetType("Eremite.Model.Effects.EffectModel");
            if (effectModelType != null)
            {
                _emDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
            }
        }

        // ========================================
        // SERVICE ACCESSORS (fresh each call)
        // ========================================

        private static object GetPaymentsService() => GameReflection.GetService(_gsPaymentsServiceProperty);

        private static object GetCalendarService() => GameReflection.GetService(_gsCalendarServiceProperty);

        private static object GetStateService() => GameReflection.GetService(_gsStateServiceProperty);

        private static object GetGameModelService() => GameReflection.GetService(_gsGameModelServiceProperty);

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Check if the given popup is a PaymentsPopup.
        /// </summary>
        public static bool IsPaymentsPopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_paymentsPopupType == null) return false;
            return _paymentsPopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Get all pending payments with full info.
        /// </summary>
        public static List<PaymentInfo> GetPayments()
        {
            EnsureCached();
            var result = new List<PaymentInfo>();

            var stateService = GetStateService();
            if (stateService == null) return result;

            try
            {
                // Get StateService.Effects
                var effects = _ssEffectsProperty?.GetValue(stateService);
                if (effects == null) return result;

                // Get effects.payments (List<PaymentState>)
                var payments = _esPaymentsField?.GetValue(effects) as IList;
                if (payments == null) return result;

                var paymentsService = GetPaymentsService();
                var calendarService = GetCalendarService();

                foreach (var state in payments)
                {
                    if (state == null) continue;

                    var info = new PaymentInfo { State = state };

                    // Get payment Good (name, amount)
                    var payment = _psPaymentField?.GetValue(state);
                    if (payment != null)
                    {
                        var goodNameRaw = _goodNameField?.GetValue(payment) as string;
                        info.GoodName = GameReflection.GetGoodDisplayName(goodNameRaw);
                        var amountObj = _goodAmountField?.GetValue(payment);
                        info.GoodAmount = amountObj is int amt ? amt : 0;
                    }

                    // Get due date
                    var dueDate = _psDueDateField?.GetValue(state);
                    if (dueDate != null)
                    {
                        var yearObj = _gdYearField?.GetValue(dueDate);
                        info.DueYear = yearObj is int y ? y : 0;

                        var seasonObj = _gdSeasonField?.GetValue(dueDate);
                        int seasonInt = seasonObj != null ? (int)seasonObj : 0;
                        info.DueSeason = seasonInt >= 0 && seasonInt < _seasonNames.Length
                            ? _seasonNames[seasonInt] : "Unknown";

                        // Get time remaining
                        if (calendarService != null && _calGetSecondsLeftToMethod != null)
                        {
                            try
                            {
                                _args1[0] = dueDate;
                                var secondsObj = _calGetSecondsLeftToMethod.Invoke(calendarService, _args1);
                                info.TimeRemaining = secondsObj is float s ? s : 0f;
                            }
                            catch { info.TimeRemaining = 0f; }
                        }
                    }

                    // Get auto-payment type
                    var autoTypeObj = _psAutoPaymentTypeField?.GetValue(state);
                    info.AutoPaymentType = autoTypeObj != null ? (int)autoTypeObj : 0;

                    // Get CanPay
                    if (paymentsService != null && _canPayMethod != null)
                    {
                        try
                        {
                            _args1[0] = state;
                            var canPayObj = _canPayMethod.Invoke(paymentsService, _args1);
                            info.CanPay = canPayObj is bool cp && cp;
                        }
                        catch { info.CanPay = false; }
                    }

                    // Get model for type/source labels
                    if (paymentsService != null && _getModelMethod != null)
                    {
                        try
                        {
                            _args1[0] = state;
                            var model = _getModelMethod.Invoke(paymentsService, _args1);
                            if (model != null)
                            {
                                // Type label
                                var typeLabel = _pemTypeLabelField?.GetValue(model);
                                if (typeLabel != null)
                                {
                                    var displayName = _lmDisplayNameField?.GetValue(typeLabel);
                                    info.TypeLabel = GameReflection.GetLocaText(displayName) ?? "";
                                }

                                // Source label
                                var sourceLabel = _pemSourceLabelField?.GetValue(model);
                                if (sourceLabel != null)
                                {
                                    var displayName = _lmDisplayNameField?.GetValue(sourceLabel);
                                    info.SourceLabel = GameReflection.GetLocaText(displayName) ?? "";
                                }
                            }
                        }
                        catch { }
                    }

                    // Get penalty description
                    var penaltyModelName = _psPenaltyModelField?.GetValue(state) as string;
                    if (!string.IsNullOrEmpty(penaltyModelName))
                    {
                        info.PenaltyDesc = GetEffectDescription(penaltyModelName);
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] PaymentsReflection.GetPayments failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Pay a payment.
        /// </summary>
        public static bool Pay(object paymentState)
        {
            EnsureCached();
            if (paymentState == null || _payMethod == null) return false;

            var paymentsService = GetPaymentsService();
            if (paymentsService == null) return false;

            try
            {
                _args1[0] = paymentState;
                _payMethod.Invoke(paymentsService, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] PaymentsReflection.Pay failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a payment can be paid (affordable).
        /// </summary>
        public static bool CanPay(object paymentState)
        {
            EnsureCached();
            if (paymentState == null || _canPayMethod == null) return false;

            var paymentsService = GetPaymentsService();
            if (paymentsService == null) return false;

            try
            {
                _args1[0] = paymentState;
                var result = _canPayMethod.Invoke(paymentsService, _args1);
                return result is bool b && b;
            }
            catch { return false; }
        }

        /// <summary>
        /// Set auto-payment type for a payment.
        /// </summary>
        public static bool SetAutoPaymentType(object paymentState, int type)
        {
            EnsureCached();
            if (paymentState == null || _psAutoPaymentTypeField == null) return false;

            try
            {
                // Convert int to AutoPaymentType enum
                var autoPaymentType = GameReflection.GameAssembly?.GetType("Eremite.Model.State.AutoPaymentType");
                if (autoPaymentType == null) return false;

                var enumValue = Enum.ToObject(autoPaymentType, type);
                _psAutoPaymentTypeField.SetValue(paymentState, enumValue);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] PaymentsReflection.SetAutoPaymentType failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the display label for an auto-payment type.
        /// </summary>
        public static string GetAutoPaymentLabel(int type)
        {
            if (type >= 0 && type < _autoPaymentLabels.Length)
                return _autoPaymentLabels[type];
            return "unknown";
        }

        /// <summary>
        /// Format time in seconds to mm:ss or hh:mm:ss string.
        /// </summary>
        public static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0:00";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return string.Format("{0}:{1:D2}:{2:D2}", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
            return string.Format("{0}:{1:D2}", (int)ts.TotalMinutes, ts.Seconds);
        }

        /// <summary>
        /// Convert year number to Roman numeral string.
        /// </summary>
        public static string YearToRoman(int year)
        {
            if (year <= 0) return year.ToString();

            var result = new System.Text.StringBuilder();
            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] numerals = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

            for (int i = 0; i < values.Length; i++)
            {
                while (year >= values[i])
                {
                    result.Append(numerals[i]);
                    year -= values[i];
                }
            }
            return result.ToString();
        }

        // ========================================
        // INTERNAL HELPERS
        // ========================================

        /// <summary>
        /// Get effect description by model name.
        /// </summary>
        private static string GetEffectDescription(string effectModelName)
        {
            if (string.IsNullOrEmpty(effectModelName)) return null;

            var gameModelService = GetGameModelService();
            if (gameModelService == null || _gmsGetEffectMethod == null) return null;

            try
            {
                _args1[0] = effectModelName;
                var effectModel = _gmsGetEffectMethod.Invoke(gameModelService, _args1);
                if (effectModel == null) return null;

                var desc = _emDescriptionProperty?.GetValue(effectModel) as string;
                return desc;
            }
            catch { return null; }
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(PaymentsReflection), "PaymentsReflection");
        }
    }
}
