// =============================================================================
// EVENT HOOKS REFERENCE - Subscribing to game events via reflection
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Reference
{
    /// <summary>
    /// Patterns for subscribing to game events without direct references
    /// </summary>
    public class EventHooksReference : MonoBehaviour
    {
        // Store subscriptions for cleanup
        private List<IDisposable> _subscriptions = new List<IDisposable>();

        // Cached reflection data
        private Type _gameControllerType;
        private PropertyInfo _gcInstanceProperty;
        private PropertyInfo _gcIsGameActiveProperty;
        private PropertyInfo _gameServicesProperty;

        private object _cachedModeService;
        private PropertyInfo _idleValueProperty;

        // =====================================================================
        // UNITY SCENE EVENTS - Always available, no reflection needed
        // =====================================================================

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            // Dispose all subscriptions
            foreach (var sub in _subscriptions)
                sub?.Dispose();
            _subscriptions.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene loaded: {scene.name}");

            if (scene.name == "Game")
            {
                // Game scene - try to subscribe to game events
                TrySubscribeToGameEvents();
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Debug.Log($"Scene unloaded: {scene.name}");

            if (scene.name == "Game")
            {
                // Clear cached services - they're now invalid
                _cachedModeService = null;

                // Dispose game-specific subscriptions
                foreach (var sub in _subscriptions)
                    sub?.Dispose();
                _subscriptions.Clear();
            }
        }

        // =====================================================================
        // GAME CONTROLLER ACCESS - Via reflection
        // =====================================================================

        private void CacheGameControllerType()
        {
            if (_gameControllerType != null) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Assembly-CSharp") continue;

                _gameControllerType = assembly.GetType("Eremite.Controller.GameController");
                if (_gameControllerType != null)
                {
                    _gcInstanceProperty = _gameControllerType.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    _gcIsGameActiveProperty = _gameControllerType.GetProperty("IsGameActive",
                        BindingFlags.Public | BindingFlags.Static);
                    _gameServicesProperty = _gameControllerType.GetProperty("GameServices",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                break;
            }
        }

        private object GetGameControllerInstance()
        {
            CacheGameControllerType();
            return _gcInstanceProperty?.GetValue(null);
        }

        private bool GetIsGameActive()
        {
            CacheGameControllerType();
            if (_gcIsGameActiveProperty == null) return false;
            return (bool)_gcIsGameActiveProperty.GetValue(null);
        }

        private object GetGameServices()
        {
            var gc = GetGameControllerInstance();
            if (gc == null) return null;
            return _gameServicesProperty?.GetValue(gc);
        }

        // =====================================================================
        // UNIRX OBSERVABLE SUBSCRIPTION - For game's reactive properties
        // =====================================================================

        /// <summary>
        /// Subscribe to a UniRx IReadOnlyReactiveProperty or Observable
        /// </summary>
        private IDisposable TrySubscribeToObservable<T>(object observable, Action<T> onNext)
        {
            if (observable == null) return null;

            try
            {
                var observableType = observable.GetType();

                // Try to find Subscribe method
                // UniRx uses extension methods, so we look for instance method first
                var subscribeMethod = observableType.GetMethod("Subscribe",
                    new Type[] { typeof(Action<T>) });

                if (subscribeMethod == null)
                {
                    // Try finding it as a generic method
                    foreach (var method in observableType.GetMethods())
                    {
                        if (method.Name == "Subscribe" && method.GetParameters().Length == 1)
                        {
                            var param = method.GetParameters()[0];
                            if (param.ParameterType.IsGenericType)
                            {
                                subscribeMethod = method;
                                break;
                            }
                        }
                    }
                }

                if (subscribeMethod != null)
                {
                    var result = subscribeMethod.Invoke(observable, new object[] { onNext });
                    return result as IDisposable;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to subscribe: {ex.Message}");
            }

            return null;
        }

        // =====================================================================
        // GAME EVENT SUBSCRIPTIONS
        // =====================================================================

        private void TrySubscribeToGameEvents()
        {
            var gameServices = GetGameServices();
            if (gameServices == null) return;

            // Get ModeService
            var modeServiceProp = gameServices.GetType().GetProperty("ModeService");
            if (modeServiceProp != null)
            {
                _cachedModeService = modeServiceProp.GetValue(gameServices);

                // Subscribe to Idle changes
                var idleProp = _cachedModeService?.GetType().GetProperty("Idle");
                if (idleProp != null)
                {
                    var idleObservable = idleProp.GetValue(_cachedModeService);

                    // Cache the Value property for polling
                    _idleValueProperty = idleObservable?.GetType().GetProperty("Value");

                    // Subscribe to changes
                    var sub = TrySubscribeToObservable<bool>(idleObservable, OnIdleModeChanged);
                    if (sub != null) _subscriptions.Add(sub);
                }
            }

            Debug.Log("Subscribed to game events");
        }

        private void OnIdleModeChanged(bool isIdle)
        {
            Debug.Log($"Idle mode changed: {isIdle}");
            // isIdle = true means normal gameplay
            // isIdle = false means in a special mode (building placement, etc.)
        }

        // =====================================================================
        // POLLING FALLBACK - For when events aren't reliable
        // =====================================================================

        private float _lastPollTime = 0f;
        private const float POLL_INTERVAL = 0.25f;
        private bool _lastKnownGameActive = false;

        private void Update()
        {
            // Poll for game state changes as fallback
            if (Time.time - _lastPollTime > POLL_INTERVAL)
            {
                _lastPollTime = Time.time;
                PollGameState();
            }
        }

        private void PollGameState()
        {
            bool isGameActive = GetIsGameActive();

            if (isGameActive != _lastKnownGameActive)
            {
                _lastKnownGameActive = isGameActive;

                if (isGameActive)
                {
                    Debug.Log("Game became active (entered settlement)");
                    TrySubscribeToGameEvents();
                }
                else
                {
                    Debug.Log("Game became inactive (left settlement)");
                }
            }

            // Also poll ModeService.Idle.Value if subscriptions failed
            if (_cachedModeService != null && _idleValueProperty != null)
            {
                // This reads the current value without subscription
                var idleProp = _cachedModeService.GetType().GetProperty("Idle");
                var idleObservable = idleProp?.GetValue(_cachedModeService);
                if (idleObservable != null)
                {
                    bool currentIdle = (bool)_idleValueProperty.GetValue(idleObservable);
                    // Use currentIdle...
                }
            }
        }

        // =====================================================================
        // MAIN CONTROLLER ACCESS - For AppServices (popups, etc.)
        // =====================================================================

        private Type _mainControllerType;
        private PropertyInfo _mcInstanceProperty;
        private PropertyInfo _appServicesProperty;

        private object GetMainControllerInstance()
        {
            if (_mainControllerType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "Assembly-CSharp") continue;

                    _mainControllerType = assembly.GetType("Eremite.Controller.MainController");
                    if (_mainControllerType != null)
                    {
                        _mcInstanceProperty = _mainControllerType.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static);
                        _appServicesProperty = _mainControllerType.GetProperty("AppServices",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                    break;
                }
            }

            return _mcInstanceProperty?.GetValue(null);
        }

        private object GetAppServices()
        {
            var mc = GetMainControllerInstance();
            if (mc == null) return null;
            return _appServicesProperty?.GetValue(mc);
        }

        private object GetPopupsService()
        {
            var appServices = GetAppServices();
            if (appServices == null) return null;

            var popupsServiceProp = appServices.GetType().GetProperty("PopupsService");
            return popupsServiceProp?.GetValue(appServices);
        }
    }
}
