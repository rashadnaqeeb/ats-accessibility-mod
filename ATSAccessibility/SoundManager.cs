using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Centralized sound playback for accessibility mod.
    /// Triggers game sounds when mod actions bypass normal game flow.
    /// </summary>
    public static class SoundManager
    {
        // Cached reflection metadata
        private static PropertyInfo _soundsManagerProperty = null;  // MainController.SoundsManager
        private static PropertyInfo _soundsProperty = null;  // MainController.Sounds (SoundReferences)
        private static MethodInfo _playSoundEffectMethod = null;  // ISoundsManager.PlaySoundEffect
        private static MethodInfo _playButtonSoundMethod = null;  // ISoundsManager.PlayButtonSound
        private static MethodInfo _playFailedSoundMethod = null;  // ISoundsManager.PlayFailedSound
        private static bool _cached = false;

        // Sound properties from SoundReferences
        private static PropertyInfo _buildingDestroyedProperty = null;
        private static PropertyInfo _buildingPlacedProperty = null;
        private static PropertyInfo _buildingRotatedProperty = null;
        private static PropertyInfo _buildingMoveStartedProperty = null;
        private static PropertyInfo _buildingMoveFinishedProperty = null;
        private static PropertyInfo _buildingSleepProperty = null;
        private static PropertyInfo _buildingWakeUpProperty = null;
        private static PropertyInfo _buildingRecipeOnProperty = null;
        private static PropertyInfo _buildingRecipeOffProperty = null;
        private static PropertyInfo _buildingPanelShowProperty = null;
        private static PropertyInfo _buildingPanelHideProperty = null;
        private static PropertyInfo _rainpunkUnlockProperty = null;
        private static PropertyInfo _rainpunkStopButtonProperty = null;
        private static PropertyInfo _buildingFireButtonStartProperty = null;

        // Popup sounds
        private static PropertyInfo _popupShowProperty = null;
        private static PropertyInfo _consumptionPopupShowProperty = null;
        private static PropertyInfo _traderPanelOpenedProperty = null;

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;  // Set early to prevent repeated attempts on failure

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // MainController properties
                var mainControllerType = assembly.GetType("Eremite.Controller.MainController");
                if (mainControllerType != null)
                {
                    _soundsManagerProperty = mainControllerType.GetProperty("SoundsManager", GameReflection.PublicInstance);
                    _soundsProperty = mainControllerType.GetProperty("Sounds", GameReflection.PublicInstance);
                }

                // ISoundsManager methods
                var soundsManagerType = assembly.GetType("Eremite.Sound.ISoundsManager");
                if (soundsManagerType != null)
                {
                    _playSoundEffectMethod = soundsManagerType.GetMethod("PlaySoundEffect", GameReflection.PublicInstance);
                    _playButtonSoundMethod = soundsManagerType.GetMethod("PlayButtonSound", GameReflection.PublicInstance);
                    _playFailedSoundMethod = soundsManagerType.GetMethod("PlayFailedSound", GameReflection.PublicInstance);
                }

                // SoundReferences properties
                var soundReferencesType = assembly.GetType("Eremite.Model.Sound.SoundReferences");
                if (soundReferencesType != null)
                {
                    _buildingDestroyedProperty = soundReferencesType.GetProperty("BuildingDestroyed", GameReflection.PublicInstance);
                    _buildingPlacedProperty = soundReferencesType.GetProperty("BuildingPlaced", GameReflection.PublicInstance);
                    _buildingRotatedProperty = soundReferencesType.GetProperty("BuildingRotated", GameReflection.PublicInstance);
                    _buildingMoveStartedProperty = soundReferencesType.GetProperty("BuildingMoveStarted", GameReflection.PublicInstance);
                    _buildingMoveFinishedProperty = soundReferencesType.GetProperty("BuildingMoveFinished", GameReflection.PublicInstance);
                    _buildingSleepProperty = soundReferencesType.GetProperty("BuildingSleep", GameReflection.PublicInstance);
                    _buildingWakeUpProperty = soundReferencesType.GetProperty("BuildingWakeUp", GameReflection.PublicInstance);
                    _buildingRecipeOnProperty = soundReferencesType.GetProperty("BuildingRecipeOn", GameReflection.PublicInstance);
                    _buildingRecipeOffProperty = soundReferencesType.GetProperty("BuildingRecipeOff", GameReflection.PublicInstance);
                    _buildingPanelShowProperty = soundReferencesType.GetProperty("BuildingPanelShow", GameReflection.PublicInstance);
                    _buildingPanelHideProperty = soundReferencesType.GetProperty("BuildingPanelHide", GameReflection.PublicInstance);
                    _rainpunkUnlockProperty = soundReferencesType.GetProperty("RainpunkUnlock", GameReflection.PublicInstance);
                    _rainpunkStopButtonProperty = soundReferencesType.GetProperty("RainpunkStopButton", GameReflection.PublicInstance);
                    _buildingFireButtonStartProperty = soundReferencesType.GetProperty("BuildingFireButtonStart", GameReflection.PublicInstance);

                    // Popup sounds
                    _popupShowProperty = soundReferencesType.GetProperty("PopupShow", GameReflection.PublicInstance);
                    _consumptionPopupShowProperty = soundReferencesType.GetProperty("ConsumptionPopupShow", GameReflection.PublicInstance);
                    _traderPanelOpenedProperty = soundReferencesType.GetProperty("TraderPanelOpened", GameReflection.PublicInstance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SoundManager cache failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the sounds manager instance. Returns null if not available.
        /// </summary>
        private static object GetSoundsManager()
        {
            EnsureCached();
            var mainController = GameReflection.GetMainControllerInstance();
            if (mainController == null) return null;
            return _soundsManagerProperty?.GetValue(mainController);
        }

        /// <summary>
        /// Play a sound effect from SoundReferences.
        /// </summary>
        private static void PlaySound(PropertyInfo soundProperty)
        {
            if (soundProperty == null) return;

            try
            {
                var mainController = GameReflection.GetMainControllerInstance();
                if (mainController == null) return;

                var soundsManager = _soundsManagerProperty?.GetValue(mainController);
                if (soundsManager == null) return;

                var sounds = _soundsProperty?.GetValue(mainController);
                if (sounds == null) return;

                var sound = soundProperty.GetValue(sounds);
                if (sound == null) return;

                _playSoundEffectMethod?.Invoke(soundsManager, new object[] { sound });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PlaySound failed: {ex.Message}");
            }
        }

        // ========================================
        // UI SOUNDS
        // ========================================

        /// <summary>
        /// Play the standard button click sound.
        /// </summary>
        public static void PlayButtonClick()
        {
            try
            {
                var soundsManager = GetSoundsManager();
                _playButtonSoundMethod?.Invoke(soundsManager, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PlayButtonClick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Play the failed/error sound.
        /// </summary>
        public static void PlayFailed()
        {
            try
            {
                var soundsManager = GetSoundsManager();
                _playFailedSoundMethod?.Invoke(soundsManager, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PlayFailed failed: {ex.Message}");
            }
        }

        // ========================================
        // BUILDING SOUNDS
        // ========================================

        /// <summary>
        /// Play the building destroyed sound.
        /// </summary>
        public static void PlayBuildingDestroyed()
        {
            EnsureCached();
            PlaySound(_buildingDestroyedProperty);
        }

        /// <summary>
        /// Play the building placed sound.
        /// </summary>
        public static void PlayBuildingPlaced()
        {
            EnsureCached();
            PlaySound(_buildingPlacedProperty);
        }

        /// <summary>
        /// Play the building rotated sound.
        /// </summary>
        public static void PlayBuildingRotated()
        {
            EnsureCached();
            PlaySound(_buildingRotatedProperty);
        }

        /// <summary>
        /// Play the building move started sound.
        /// </summary>
        public static void PlayBuildingMoveStarted()
        {
            EnsureCached();
            PlaySound(_buildingMoveStartedProperty);
        }

        /// <summary>
        /// Play the building move finished sound.
        /// </summary>
        public static void PlayBuildingMoveFinished()
        {
            EnsureCached();
            PlaySound(_buildingMoveFinishedProperty);
        }

        /// <summary>
        /// Play the building sleep sound.
        /// </summary>
        public static void PlayBuildingSleep()
        {
            EnsureCached();
            PlaySound(_buildingSleepProperty);
        }

        /// <summary>
        /// Play the building wake up sound.
        /// </summary>
        public static void PlayBuildingWakeUp()
        {
            EnsureCached();
            PlaySound(_buildingWakeUpProperty);
        }

        /// <summary>
        /// Play the recipe enabled sound.
        /// </summary>
        public static void PlayRecipeOn()
        {
            EnsureCached();
            PlaySound(_buildingRecipeOnProperty);
        }

        /// <summary>
        /// Play the recipe disabled sound.
        /// </summary>
        public static void PlayRecipeOff()
        {
            EnsureCached();
            PlaySound(_buildingRecipeOffProperty);
        }

        /// <summary>
        /// Play the fire button start sound (used for sacrifice enable).
        /// </summary>
        public static void PlayBuildingFireButtonStart()
        {
            EnsureCached();
            PlaySound(_buildingFireButtonStartProperty);
        }

        /// <summary>
        /// Play the building panel opened sound.
        /// </summary>
        public static void PlayBuildingPanelShow()
        {
            EnsureCached();
            PlaySound(_buildingPanelShowProperty);
        }

        /// <summary>
        /// Play the building panel closed sound.
        /// </summary>
        public static void PlayBuildingPanelHide()
        {
            EnsureCached();
            PlaySound(_buildingPanelHideProperty);
        }

        // ========================================
        // RAINPUNK SOUNDS
        // ========================================

        /// <summary>
        /// Play the rainpunk unlock/install sound.
        /// </summary>
        public static void PlayRainpunkUnlock()
        {
            EnsureCached();
            PlaySound(_rainpunkUnlockProperty);
        }

        /// <summary>
        /// Play the rainpunk stop button sound.
        /// </summary>
        public static void PlayRainpunkStop()
        {
            EnsureCached();
            PlaySound(_rainpunkStopButtonProperty);
        }

        // ========================================
        // POPUP SOUNDS
        // ========================================

        /// <summary>
        /// Play the generic popup show sound.
        /// </summary>
        public static void PlayPopupShow()
        {
            EnsureCached();
            PlaySound(_popupShowProperty);
        }

        /// <summary>
        /// Play the consumption popup show sound.
        /// </summary>
        public static void PlayConsumptionPopupShow()
        {
            EnsureCached();
            PlaySound(_consumptionPopupShowProperty);
        }

        /// <summary>
        /// Play the trader panel opened sound.
        /// </summary>
        public static void PlayTraderPanelOpened()
        {
            EnsureCached();
            PlaySound(_traderPanelOpenedProperty);
        }

        // ========================================
        // MENU BUTTON SOUNDS (by clip name)
        // ========================================

        /// <summary>
        /// Play the recipes menu button sound.
        /// </summary>
        public static void PlayMenuRecipes()
        {
            PlaySoundByClipName("menu_recipes");
        }

        /// <summary>
        /// Play the orders menu button sound.
        /// </summary>
        public static void PlayMenuOrders()
        {
            PlaySoundByClipName("menu_orders");
        }

        /// <summary>
        /// Play the trends menu button sound.
        /// </summary>
        public static void PlayMenuTrends()
        {
            PlaySoundByClipName("trends_window");
        }

        /// <summary>
        /// Play the trade routes menu button sound.
        /// </summary>
        public static void PlayMenuTradeRoutes()
        {
            PlaySoundByClipName("menu_trade_routes");
        }

        // Cached AudioClip type
        private static Type _audioClipType = null;

        /// <summary>
        /// Play a sound by finding its AudioClip by name.
        /// Used for sounds that aren't exposed through SoundReferences properties.
        /// </summary>
        private static void PlaySoundByClipName(string clipName)
        {
            try
            {
                EnsureCached();

                // Get AudioClip type via reflection
                if (_audioClipType == null)
                {
                    _audioClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
                    if (_audioClipType == null)
                    {
                        // Fallback: search loaded assemblies
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            _audioClipType = asm.GetType("UnityEngine.AudioClip");
                            if (_audioClipType != null) break;
                        }
                    }
                }

                if (_audioClipType == null)
                {
                    Debug.LogWarning("[ATSAccessibility] AudioClip type not found");
                    return;
                }

                // Find all AudioClips and search by name
                var clips = Resources.FindObjectsOfTypeAll(_audioClipType);
                object targetClip = null;

                foreach (var clip in clips)
                {
                    if (clip != null && clip.name == clipName)
                    {
                        targetClip = clip;
                        break;
                    }
                }

                if (targetClip == null)
                {
                    Debug.LogWarning($"[ATSAccessibility] Sound clip not found: {clipName}");
                    return;
                }

                // Get the SoundsManager and play via AudioSource
                var mainController = GameReflection.GetMainControllerInstance();
                if (mainController == null) return;

                var soundsManager = _soundsManagerProperty?.GetValue(mainController);
                if (soundsManager == null) return;

                // The SoundsManager has a buttonAudioSource we can use
                var smType = soundsManager.GetType();
                var buttonSourceField = smType.GetField("buttonAudioSource", BindingFlags.NonPublic | BindingFlags.Instance);
                var audioSource = buttonSourceField?.GetValue(soundsManager);

                if (audioSource != null)
                {
                    // Set clip and play via reflection
                    var asType = audioSource.GetType();
                    var clipProp = asType.GetProperty("clip");
                    var volumeProp = asType.GetProperty("volume");
                    var playMethod = asType.GetMethod("Play", Type.EmptyTypes);

                    clipProp?.SetValue(audioSource, targetClip);
                    volumeProp?.SetValue(audioSource, 1.0f);
                    playMethod?.Invoke(audioSource, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] PlaySoundByClipName({clipName}) failed: {ex.Message}");
            }
        }
    }
}
