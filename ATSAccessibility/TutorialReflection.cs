using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to TutorialTooltip and WorldTutorialsHUD internals.
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

        // Cached tooltip reference for world map (service returns null after animation)
        private static object _cachedTooltip = null;


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
            if (appServices == null || _tooltipsServiceProperty == null)
                return null;

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
        /// Falls back to cached reference if service returns null (world map after animation).
        /// </summary>
        public static object GetTutorialTooltip()
        {
            EnsureCached();

            var tooltipsService = GetTooltipsService();
            if (tooltipsService != null && _getTutorialTooltipMethod != null)
            {
                try
                {
                    var result = _getTutorialTooltipMethod.Invoke(tooltipsService, null);
                    if (result != null)
                    {
                        // Cache for later use (service may return null after animation)
                        _cachedTooltip = result;
                        return result;
                    }
                }
                catch
                {
                    // Fall through to cached
                }
            }

            // Return cached tooltip if still valid (gameObject active)
            if (_cachedTooltip != null)
            {
                var component = _cachedTooltip as Component;
                if (component != null && component.gameObject != null && component.gameObject.activeInHierarchy)
                {
                    return _cachedTooltip;
                }
                // Cached tooltip no longer valid
                _cachedTooltip = null;
            }

            return null;
        }

        /// <summary>
        /// Clear the cached tooltip reference (call on scene change).
        /// </summary>
        public static void ClearCachedTooltip()
        {
            _cachedTooltip = null;
        }

        /// <summary>
        /// Check if the TutorialTooltip is currently visible.
        /// Uses gameObject.activeInHierarchy instead of isShown field because
        /// the game sets isShown=false during AnimateHide() but the tooltip
        /// is still visible and interactive until the animation completes.
        /// </summary>
        public static bool IsTooltipVisible()
        {
            EnsureCached();

            var tooltip = GetTutorialTooltip();
            if (tooltip == null)
            {
                // Only log occasionally to avoid spam
                return false;
            }

            try
            {
                // Check if the GameObject is actually active (more reliable than isShown)
                var tooltipComponent = tooltip as Component;
                bool active = tooltipComponent?.gameObject?.activeInHierarchy ?? false;
                return active;
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

        // ========================================
        // WORLD TUTORIALS HUD REFLECTION
        // ========================================

        private static bool _worldTutorialsCached = false;

        // WorldTutorialsHUD type and fields
        private static Type _worldTutorialsHUDType = null;
        private static PropertyInfo _wthIsShownProperty = null;  // bool IsShown

        // TutorialGameConfig fields
        private static FieldInfo _tgcDisplayNameField = null;
        private static FieldInfo _tgcLockedTooltipField = null;
        private static FieldInfo _tgcRequiredMetaRewardField = null;
        private static FieldInfo _tgcRewardsField = null;
        private static FieldInfo _tgcBiomeField = null;

        // MetaRewardModel fields
        private static FieldInfo _mrShowUnlockingField = null;
        private static PropertyInfo _mrDisplayNameProperty = null;

        // TutorialsConfig.GetConfigFor method
        private static MethodInfo _getConfigForBiomeMethod = null;

        // TutorialService methods
        private static MethodInfo _wasEverFinishedMethod = null;

        // TutorialService.Phase property for getting current phase
        private static PropertyInfo _tutorialPhaseProperty = null;

        // MetaConditionsService methods
        private static MethodInfo _isUnlockedMethod = null;

        // WorldTutorialService methods
        private static MethodInfo _startTutorialMethod = null;

        // TutorialsConfig fields (for getting all tutorial configs)
        private static FieldInfo _tut1ConfigField = null;
        private static FieldInfo _tut2ConfigField = null;
        private static FieldInfo _tut3ConfigField = null;
        private static FieldInfo _tut4ConfigField = null;

        private static void EnsureWorldTutorialsCached()
        {
            if (_worldTutorialsCached) return;
            _worldTutorialsCached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // WorldTutorialsHUD type
                _worldTutorialsHUDType = assembly.GetType("Eremite.WorldMap.UI.WorldTutorialsHUD");
                if (_worldTutorialsHUDType != null)
                {
                    _wthIsShownProperty = _worldTutorialsHUDType.GetProperty("IsShown", GameReflection.PublicInstance);
                }

                // TutorialGameConfig type
                var tutorialGameConfigType = assembly.GetType("Eremite.Model.Configs.TutorialGameConfig");
                if (tutorialGameConfigType != null)
                {
                    _tgcDisplayNameField = tutorialGameConfigType.GetField("displayName", GameReflection.PublicInstance);
                    _tgcLockedTooltipField = tutorialGameConfigType.GetField("lockedTooltip", GameReflection.PublicInstance);
                    _tgcRequiredMetaRewardField = tutorialGameConfigType.GetField("requiredMetaReward", GameReflection.PublicInstance);
                    _tgcRewardsField = tutorialGameConfigType.GetField("rewards", GameReflection.PublicInstance);
                    _tgcBiomeField = tutorialGameConfigType.GetField("biome", GameReflection.PublicInstance);
                }

                // MetaRewardModel type
                var metaRewardModelType = assembly.GetType("Eremite.Model.Meta.MetaRewardModel");
                if (metaRewardModelType != null)
                {
                    _mrShowUnlockingField = metaRewardModelType.GetField("showUnlocking", GameReflection.PublicInstance);
                    _mrDisplayNameProperty = metaRewardModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                }

                // TutorialsConfig type (for getting all configs)
                var tutorialsConfigType = assembly.GetType("Eremite.Model.Configs.TutorialsConfig");
                if (tutorialsConfigType != null)
                {
                    _tut1ConfigField = tutorialsConfigType.GetField("tut1Config", GameReflection.PublicInstance);
                    _tut2ConfigField = tutorialsConfigType.GetField("tut2Config", GameReflection.PublicInstance);
                    _tut3ConfigField = tutorialsConfigType.GetField("tut3Config", GameReflection.PublicInstance);
                    _tut4ConfigField = tutorialsConfigType.GetField("tut4Config", GameReflection.PublicInstance);

                    // GetConfigFor(BiomeModel biome) method
                    var biomeModelType = assembly.GetType("Eremite.Model.BiomeModel");
                    if (biomeModelType != null)
                    {
                        _getConfigForBiomeMethod = tutorialsConfigType.GetMethod("GetConfigFor",
                            new Type[] { biomeModelType });
                    }
                }

                // ITutorialService methods
                var tutorialServiceType = assembly.GetType("Eremite.Services.ITutorialService");
                if (tutorialServiceType != null)
                {
                    _wasEverFinishedMethod = tutorialServiceType.GetMethod("WasEverFinished",
                        new Type[] { tutorialGameConfigType ?? typeof(object) });
                    _tutorialPhaseProperty = tutorialServiceType.GetProperty("Phase", GameReflection.PublicInstance);
                }

                // IMetaConditionsService methods
                var metaConditionsServiceType = assembly.GetType("Eremite.Services.IMetaConditionsService");
                if (metaConditionsServiceType != null && metaRewardModelType != null)
                {
                    _isUnlockedMethod = metaConditionsServiceType.GetMethod("IsUnlocked",
                        new Type[] { metaRewardModelType });
                }

                // IWorldTutorialService methods
                var worldTutorialServiceType = assembly.GetType("Eremite.Services.World.IWorldTutorialService");
                if (worldTutorialServiceType != null)
                {
                    _startTutorialMethod = worldTutorialServiceType.GetMethod("StartTutorial",
                        new Type[] { tutorialGameConfigType ?? typeof(object) });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialReflection: WorldTutorials caching failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the WorldTutorialsHUD instance in the scene.
        /// </summary>
        private static object GetWorldTutorialsHUD()
        {
            EnsureWorldTutorialsCached();
            if (_worldTutorialsHUDType == null) return null;

            try
            {
                return UnityEngine.Object.FindObjectOfType(_worldTutorialsHUDType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if the WorldTutorialsHUD panel is currently visible.
        /// </summary>
        public static bool IsWorldTutorialsHUDVisible()
        {
            EnsureWorldTutorialsCached();

            var hud = GetWorldTutorialsHUD();
            if (hud == null || _wthIsShownProperty == null) return false;

            try
            {
                var isShown = _wthIsShownProperty.GetValue(hud);
                return isShown is bool shown && shown;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle the WorldTutorialsHUD visibility.
        /// </summary>
        public static void ToggleWorldTutorialsHUD()
        {
            EnsureWorldTutorialsCached();

            var hud = GetWorldTutorialsHUD();
            if (hud == null || _wthIsShownProperty == null) return;

            try
            {
                bool currentState = (bool)_wthIsShownProperty.GetValue(hud);
                _wthIsShownProperty.SetValue(hud, !currentState);

                // Find and invoke the animation method
                var animateMethod = currentState
                    ? _worldTutorialsHUDType.GetMethod("AnimateHide", GameReflection.NonPublicInstance)
                    : _worldTutorialsHUDType.GetMethod("AnimateShow", GameReflection.NonPublicInstance);

                animateMethod?.Invoke(hud, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleWorldTutorialsHUD failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Data class for tutorial information.
        /// </summary>
        public class TutorialInfo
        {
            public object Config { get; set; }
            public string DisplayName { get; set; }
            public bool IsCompleted { get; set; }
            public bool IsUnlocked { get; set; }
            public string LockedReason { get; set; }
        }

        /// <summary>
        /// Get all tutorials with their status.
        /// </summary>
        public static List<TutorialInfo> GetAllTutorials()
        {
            EnsureWorldTutorialsCached();
            var result = new List<TutorialInfo>();

            try
            {
                // Get Settings.tutorialsConfig (it's a field, not a property)
                var settings = GameReflection.GetSettings();
                if (settings == null)
                    return result;

                var tutorialsConfigField = settings.GetType().GetField("tutorialsConfig", GameReflection.PublicInstance);
                if (tutorialsConfigField == null)
                    return result;

                var tutorialsConfig = tutorialsConfigField.GetValue(settings);
                if (tutorialsConfig == null)
                    return result;

                // Get all 4 tutorial configs
                var configs = new List<object>();
                if (_tut1ConfigField != null)
                {
                    var cfg = _tut1ConfigField.GetValue(tutorialsConfig);
                    if (cfg != null) configs.Add(cfg);
                }

                if (_tut2ConfigField != null)
                {
                    var cfg = _tut2ConfigField.GetValue(tutorialsConfig);
                    if (cfg != null) configs.Add(cfg);
                }
                if (_tut3ConfigField != null)
                {
                    var cfg = _tut3ConfigField.GetValue(tutorialsConfig);
                    if (cfg != null) configs.Add(cfg);
                }
                if (_tut4ConfigField != null)
                {
                    var cfg = _tut4ConfigField.GetValue(tutorialsConfig);
                    if (cfg != null) configs.Add(cfg);
                }

                foreach (var config in configs)
                {
                    if (config == null) continue;

                    var info = new TutorialInfo
                    {
                        Config = config,
                        DisplayName = GetTutorialDisplayName(config),
                        IsCompleted = IsTutorialCompleted(config),
                        IsUnlocked = IsTutorialUnlocked(config),
                        LockedReason = GetTutorialLockedReason(config)
                    };

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetAllTutorials failed: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// Get the display name for a tutorial config.
        /// </summary>
        public static string GetTutorialDisplayName(object config)
        {
            EnsureWorldTutorialsCached();
            if (config == null || _tgcDisplayNameField == null) return null;

            try
            {
                var displayName = _tgcDisplayNameField.GetValue(config);
                return GameReflection.GetLocaText(displayName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a tutorial has been completed (ever finished).
        /// </summary>
        public static bool IsTutorialCompleted(object config)
        {
            EnsureWorldTutorialsCached();
            if (config == null) return false;

            try
            {
                // Get TutorialService from MB (via AppServices)
                var appServices = GameReflection.GetAppServices();
                if (appServices == null) return false;

                var tutorialServiceProp = appServices.GetType().GetProperty("TutorialService", GameReflection.PublicInstance);
                var tutorialService = tutorialServiceProp?.GetValue(appServices);
                if (tutorialService == null || _wasEverFinishedMethod == null) return false;

                var result = _wasEverFinishedMethod.Invoke(tutorialService, new[] { config });
                return result is bool finished && finished;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] IsTutorialCompleted failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a tutorial is unlocked (requiredMetaReward is null or unlocked).
        /// </summary>
        public static bool IsTutorialUnlocked(object config)
        {
            EnsureWorldTutorialsCached();
            if (config == null || _tgcRequiredMetaRewardField == null) return true;

            try
            {
                var requiredReward = _tgcRequiredMetaRewardField.GetValue(config);
                if (requiredReward == null) return true;  // No requirement = always unlocked

                // Get MetaConditionsService from MetaServices (not AppServices)
                var metaServices = GameReflection.GetMetaServices();
                if (metaServices == null) return false;

                var metaConditionsServiceProp = metaServices.GetType().GetProperty("MetaConditionsService", GameReflection.PublicInstance);
                var metaConditionsService = metaConditionsServiceProp?.GetValue(metaServices);
                if (metaConditionsService == null || _isUnlockedMethod == null) return false;

                var result = _isUnlockedMethod.Invoke(metaConditionsService, new[] { requiredReward });
                return result is bool unlocked && unlocked;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] IsTutorialUnlocked failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Get the locked reason tooltip for a tutorial config.
        /// </summary>
        public static string GetTutorialLockedReason(object config)
        {
            EnsureWorldTutorialsCached();
            if (config == null || _tgcLockedTooltipField == null) return null;

            try
            {
                var lockedTooltip = _tgcLockedTooltipField.GetValue(config);
                return GameReflection.GetLocaText(lockedTooltip);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Start a tutorial by its config.
        /// </summary>
        public static bool StartTutorial(object config)
        {
            EnsureWorldTutorialsCached();
            if (config == null || _startTutorialMethod == null) return false;

            try
            {
                // Get WorldTutorialService from WorldServices
                var worldServices = WorldMapReflection.GetWorldServices();
                if (worldServices == null) return false;

                var worldTutorialServiceProp = worldServices.GetType().GetProperty("WorldTutorialService", GameReflection.PublicInstance);
                var worldTutorialService = worldTutorialServiceProp?.GetValue(worldServices);
                if (worldTutorialService == null) return false;

                _startTutorialMethod.Invoke(worldTutorialService, new[] { config });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] StartTutorial failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current tutorial phase as an integer.
        /// Returns -1 if not in a tutorial or on error.
        /// </summary>
        public static int GetCurrentPhase()
        {
            EnsureWorldTutorialsCached();

            try
            {
                // Get TutorialService from MetaServices (it's a meta service, not app service)
                var metaServices = GameReflection.GetMetaServices();
                if (metaServices == null || _tutorialPhaseProperty == null) return -1;

                var tutorialServiceProp = metaServices.GetType().GetProperty("TutorialService", GameReflection.PublicInstance);
                var tutorialService = tutorialServiceProp?.GetValue(metaServices);
                if (tutorialService == null) return -1;

                // Phase is a ReactiveProperty<TutorialPhase>, need to get .Value
                var phaseReactive = _tutorialPhaseProperty.GetValue(tutorialService);
                if (phaseReactive == null) return -1;

                var valueProp = phaseReactive.GetType().GetProperty("Value", GameReflection.PublicInstance);
                var phaseValue = valueProp?.GetValue(phaseReactive);
                if (phaseValue == null) return -1;

                // TutorialPhase is an enum, convert to int
                return (int)phaseValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCurrentPhase failed: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Get the list of reward display names for the current tutorial biome.
        /// Only returns rewards with showUnlocking = true.
        /// Returns empty list if not a tutorial or on error.
        /// </summary>
        public static List<string> GetTutorialRewardsForCurrentBiome()
        {
            EnsureWorldTutorialsCached();
            var result = new List<string>();

            try
            {
                // Get current biome from GameMB.Biome
                var gameMBType = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
                if (gameMBType == null) return result;

                var biomeProperty = gameMBType.GetProperty("Biome", GameReflection.PublicStatic);
                var currentBiome = biomeProperty?.GetValue(null);
                if (currentBiome == null) return result;

                // Get Settings.tutorialsConfig
                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                var tutorialsConfigField = settings.GetType().GetField("tutorialsConfig", GameReflection.PublicInstance);
                var tutorialsConfig = tutorialsConfigField?.GetValue(settings);
                if (tutorialsConfig == null || _getConfigForBiomeMethod == null) return result;

                // Call GetConfigFor(biome) - may throw if not a tutorial biome
                object tutorialConfig;
                try
                {
                    tutorialConfig = _getConfigForBiomeMethod.Invoke(tutorialsConfig, new[] { currentBiome });
                }
                catch
                {
                    // Not a tutorial biome
                    return result;
                }

                if (tutorialConfig == null || _tgcRewardsField == null) return result;

                // Get rewards array
                var rewards = _tgcRewardsField.GetValue(tutorialConfig) as Array;
                if (rewards == null) return result;

                // Filter and get display names
                foreach (var reward in rewards)
                {
                    if (reward == null) continue;

                    // Check showUnlocking
                    var showUnlockingObj = _mrShowUnlockingField?.GetValue(reward);
                    bool showUnlocking = showUnlockingObj is bool b && b;
                    if (!showUnlocking) continue;

                    // Get DisplayName
                    var displayName = _mrDisplayNameProperty?.GetValue(reward) as string;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        result.Add(displayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetTutorialRewardsForCurrentBiome failed: {ex.Message}");
            }

            return result;
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(TutorialReflection), "TutorialReflection");
        }
    }
}
