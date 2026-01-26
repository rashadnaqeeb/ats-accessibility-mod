using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to TutorialTooltip internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class TutorialReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // TutorialTooltip type and fields
        private static Type _tutorialTooltipType = null;
        private static FieldInfo _isShownField = null;
        private static FieldInfo _textTyperField = null;
        private static FieldInfo _buttonField = null;
        private static FieldInfo _hasMoreTextField = null;
        private static FieldInfo _triggerField = null;

        // TutorialTextTrigger fields
        private static FieldInfo _triggerButtonEnabledField = null;

        // TextTyper type and fields
        private static FieldInfo _textTyperTextMeshField = null;
        private static PropertyInfo _tmpTextProperty = null;

        // IServices / AppServices access
        private static PropertyInfo _tooltipsServiceProperty = null;

        // TooltipsService.Get<T> method (generic, cached after MakeGenericMethod)
        private static MethodInfo _tooltipsServiceGetMethod = null;
        private static MethodInfo _getTutorialTooltipMethod = null;  // Cached generic version

        // Button click invocation
        private static PropertyInfo _buttonOnClickProperty = null;
        private static MethodInfo _onClickInvokeMethod = null;


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
                if (assembly == null) return;

                // TutorialTooltip type
                _tutorialTooltipType = assembly.GetType("Eremite.Tutorial.Views.TutorialTooltip");
                if (_tutorialTooltipType != null)
                {
                    _isShownField = _tutorialTooltipType.GetField("isShown", GameReflection.NonPublicInstance);
                    _textTyperField = _tutorialTooltipType.GetField("textTyper", GameReflection.NonPublicInstance);
                    _buttonField = _tutorialTooltipType.GetField("button", GameReflection.NonPublicInstance);
                    _hasMoreTextField = _tutorialTooltipType.GetField("hasMoreText", GameReflection.NonPublicInstance);
                    _triggerField = _tutorialTooltipType.GetField("trigger", GameReflection.NonPublicInstance);
                }

                // TutorialTextTrigger type
                var triggerType = assembly.GetType("Eremite.Tutorial.Views.TutorialTextTrigger");
                if (triggerType != null)
                {
                    _triggerButtonEnabledField = triggerType.GetField("buttonEnabled", GameReflection.PublicInstance);
                }

                // TextTyper type
                var textTyperType = assembly.GetType("Eremite.View.TextTyper");
                if (textTyperType != null)
                {
                    _textTyperTextMeshField = textTyperType.GetField("textMesh", GameReflection.NonPublicInstance);
                }

                // IServices interface (for TooltipsService property)
                var servicesType = assembly.GetType("Eremite.Services.IServices");
                if (servicesType != null)
                {
                    _tooltipsServiceProperty = servicesType.GetProperty("TooltipsService");
                }

                // ITooltipsService interface
                var tooltipsServiceType = assembly.GetType("Eremite.Services.ITooltipsService");
                if (tooltipsServiceType != null)
                {
                    // Get<T>() method - cache the generic version for TutorialTooltip
                    _tooltipsServiceGetMethod = tooltipsServiceType.GetMethod("Get");
                    if (_tooltipsServiceGetMethod != null && _tutorialTooltipType != null)
                    {
                        _getTutorialTooltipMethod = _tooltipsServiceGetMethod.MakeGenericMethod(_tutorialTooltipType);
                    }
                }

                Debug.Log("[ATSAccessibility] TutorialReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // TOOLTIP ACCESS
        // ========================================

        /// <summary>
        /// Get the TooltipsService instance (fresh each time).
        /// </summary>
        private static object GetTooltipsService()
        {
            EnsureCached();

            var appServices = GameReflection.GetAppServices();
            if (appServices == null || _tooltipsServiceProperty == null) return null;

            try
            {
                return _tooltipsServiceProperty.GetValue(appServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the TutorialTooltip instance from TooltipsService (fresh each time).
        /// </summary>
        public static object GetTutorialTooltip()
        {
            EnsureCached();

            var tooltipsService = GetTooltipsService();
            if (tooltipsService == null || _getTutorialTooltipMethod == null) return null;

            try
            {
                return _getTutorialTooltipMethod.Invoke(tooltipsService, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection: GetTutorialTooltip failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the TutorialTooltip is currently visible.
        /// </summary>
        public static bool IsTooltipVisible()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _isShownField == null) return false;

            try
            {
                var isShown = _isShownField.GetValue(tooltip);
                return isShown is bool shown && shown;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the tooltip has more text to show (continue button state).
        /// </summary>
        public static bool HasMoreText()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _hasMoreTextField == null) return false;

            try
            {
                var hasMore = _hasMoreTextField.GetValue(tooltip);
                return hasMore is bool more && more;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // TEXT ACCESS
        // ========================================

        /// <summary>
        /// Get the current text displayed in the TutorialTooltip.
        /// </summary>
        public static string GetCurrentText()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _textTyperField == null) return null;

            try
            {
                var textTyper = _textTyperField.GetValue(tooltip);
                if (textTyper == null || _textTyperTextMeshField == null) return null;

                var textMesh = _textTyperTextMeshField.GetValue(textTyper);
                if (textMesh == null) return null;

                // Cache TMP_Text property on first use
                if (_tmpTextProperty == null)
                {
                    _tmpTextProperty = textMesh.GetType().GetProperty("text", GameReflection.PublicInstance);
                }

                return _tmpTextProperty?.GetValue(textMesh) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection: GetCurrentText failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // ACTIONS
        // ========================================

        /// <summary>
        /// Check if the button is expected to appear after animation completes.
        /// Returns true if trigger.buttonEnabled is true.
        /// </summary>
        public static bool IsButtonExpected()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _triggerField == null) return false;

            try
            {
                var trigger = _triggerField.GetValue(tooltip);
                if (trigger == null || _triggerButtonEnabledField == null) return false;

                var buttonEnabled = _triggerButtonEnabledField.GetValue(trigger);
                return buttonEnabled is bool enabled && enabled;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the continue button is currently active/visible.
        /// </summary>
        public static bool IsButtonActive()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _buttonField == null) return false;

            try
            {
                var button = _buttonField.GetValue(tooltip);
                if (button == null) return false;

                // Cast to Component to access gameObject directly (avoids reflection issues)
                var buttonComponent = button as Component;
                if (buttonComponent == null) return false;

                return buttonComponent.gameObject.activeInHierarchy;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection: IsButtonActive failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Trigger the continue button click to advance the tutorial.
        /// </summary>
        /// <returns>True if the button was clicked, false if not available.</returns>
        public static bool TriggerContinue()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null || _buttonField == null) return false;

            try
            {
                var button = _buttonField.GetValue(tooltip);
                if (button == null) return false;

                // Check if button is active first
                if (!IsButtonActive()) return false;

                // Cache onClick property on first use
                if (_buttonOnClickProperty == null)
                {
                    _buttonOnClickProperty = button.GetType().GetProperty("onClick", GameReflection.PublicInstance);
                }

                if (_buttonOnClickProperty == null) return false;

                var onClick = _buttonOnClickProperty.GetValue(button);
                if (onClick == null) return false;

                // Cache Invoke method on first use
                if (_onClickInvokeMethod == null)
                {
                    _onClickInvokeMethod = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
                }

                if (_onClickInvokeMethod == null) return false;

                _onClickInvokeMethod.Invoke(onClick, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection: TriggerContinue failed: {ex.Message}");
                return false;
            }
        }

    }
}
