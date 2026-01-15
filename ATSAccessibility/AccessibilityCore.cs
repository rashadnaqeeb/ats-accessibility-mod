using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ATSAccessibility
{
    public class AccessibilityCore : MonoBehaviour
    {
        // Scene indices from MainController.cs:
        // 0 = Menu, 1 = Game (settlement), 2 = Transition, 3 = WorldMap, 4 = Intro, 5 = IronmanCutscene, 6 = Credits
        private const int SCENE_MENU = 0;
        private const int SCENE_GAME = 1;

        // State tracking
        private bool _speechInitialized = false;
        private bool _announcedMainMenu = false;
        private bool _announcedGameStart = false;
        private bool _wasGameActive = false;

        // Polling for game state (fallback since GameController initializes async)
        private float _pollTimer = 0f;
        private const float POLL_INTERVAL = 0.5f;

        // UI Navigation
        private UINavigator _uiNavigator;
        private KeyboardManager _keyboardManager;

        // Popup event subscriptions (IDisposable from UniRx)
        private IDisposable _popupShownSubscription;
        private IDisposable _popupHiddenSubscription;
        private bool _subscribedToPopups = false;

        // Tutorial handling
        private TutorialHandler _tutorialHandler;

        // Deferred menu rebuild (wait for user input after popup closes)
        private bool _menuPendingSetup = false;

        private void Start()
        {
            Debug.Log("[ATSAccessibility] AccessibilityCore.Start()");

            // Subscribe to scene events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Initialize speech (Tolk)
            _speechInitialized = Speech.Initialize();

            // Initialize UI navigation
            _uiNavigator = new UINavigator();
            _keyboardManager = new KeyboardManager(_uiNavigator);

            // Initialize tutorial handler
            _tutorialHandler = new TutorialHandler(OnTutorialPhaseChanged);

            // Wire up tutorial handler to keyboard manager
            _keyboardManager.SetTutorialHandler(_tutorialHandler);

            // Check if we're already on a scene (mod loaded mid-game)
            CheckCurrentScene();
        }

        private void OnDestroy()
        {
            Debug.Log("[ATSAccessibility] AccessibilityCore.OnDestroy()");

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            // Dispose popup subscriptions
            DisposePopupSubscriptions();

            Speech.Shutdown();
        }

        private void Update()
        {
            // Polling for game state changes (settlement entry)
            // Use unscaledDeltaTime so it works even when game is paused
            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer >= POLL_INTERVAL)
            {
                _pollTimer = 0f;
                PollGameState();

                // Try to subscribe to popups if not already subscribed
                if (!_subscribedToPopups)
                {
                    TrySubscribeToPopups();
                }

                // Try to subscribe to tutorial phase changes
                _tutorialHandler?.TrySubscribe();
            }
        }

        private void OnGUI()
        {
            // Process input in OnGUI - this captures input even when UI has focus
            Event e = Event.current;
            if (e != null && e.isKey && e.type == EventType.KeyDown)
            {
                Debug.Log($"[ATSAccessibility] DEBUG: OnGUI detected key: {e.keyCode}");

                // Deferred menu setup - rebuild on first key press after popup closes
                if (_menuPendingSetup)
                {
                    Debug.Log("[ATSAccessibility] Rebuilding menu navigation on user input");
                    SetupMainMenuNavigation();
                    _menuPendingSetup = false;
                }

                _keyboardManager?.ProcessKeyEvent(e.keyCode);
            }
        }

        private void CheckCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Debug.Log($"[ATSAccessibility] Current scene: {activeScene.name} (index: {activeScene.buildIndex})");

            ProcessSceneLoad(activeScene);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[ATSAccessibility] Scene loaded: {scene.name} (index: {scene.buildIndex})");
            ProcessSceneLoad(scene);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Debug.Log($"[ATSAccessibility] Scene unloaded: {scene.name} (index: {scene.buildIndex})");

            // Clear state when leaving scenes
            if (scene.buildIndex == SCENE_GAME)
            {
                _announcedGameStart = false;
                _wasGameActive = false;
            }
            else if (scene.buildIndex == SCENE_MENU)
            {
                _announcedMainMenu = false;
            }

            // Dispose popup subscriptions (PopupsService is destroyed on scene change)
            DisposePopupSubscriptions();

            // Dispose tutorial handler subscription
            _tutorialHandler?.Dispose();

            // Reset UI navigator state
            _uiNavigator?.Reset();
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);

            // Clear any cached references
            GameReflection.ClearCachedInstances();
        }

        private void ProcessSceneLoad(Scene scene)
        {
            if (scene.buildIndex == SCENE_MENU && !_announcedMainMenu)
            {
                // Delay announcement to ensure scene is fully loaded
                Invoke(nameof(AnnounceMainMenu), 0.5f);
            }
            else if (scene.buildIndex == SCENE_GAME)
            {
                // For game scene, we wait for GameController.IsGameActive
                // This is handled by polling since the controller initializes async
                _announcedGameStart = false;
            }
        }

        private void AnnounceMainMenu()
        {
            if (_announcedMainMenu) return;

            if (_speechInitialized && Speech.IsAvailable)
            {
                Speech.Say("Mod loaded");
                _announcedMainMenu = true;
                Debug.Log("[ATSAccessibility] Announced: Mod loaded");

                // Set up main menu navigation after a short delay (let UI initialize)
                Invoke(nameof(SetupMainMenuNavigation), 0.5f);
            }
            else
            {
                Debug.LogWarning("[ATSAccessibility] Speech not available for main menu announcement");
            }
        }

        private void SetupMainMenuNavigation()
        {
            // Find the main menu Canvas
            // Look for Canvas objects with buttons - the main menu typically has "MainMenu" or similar in name
            var canvases = FindObjectsOfType<Canvas>();
            Debug.Log($"[ATSAccessibility] DEBUG: Found {canvases.Length} canvases in scene");

            GameObject mainMenuRoot = null;

            foreach (var canvas in canvases)
            {
                // Skip inactive canvases
                if (!canvas.gameObject.activeInHierarchy) continue;

                string name = canvas.gameObject.name.ToLower();
                Debug.Log($"[ATSAccessibility] DEBUG: Canvas '{canvas.gameObject.name}' active={canvas.gameObject.activeInHierarchy}");

                // Look for main menu indicators
                if (name.Contains("mainmenu") || name.Contains("main menu") || name.Contains("menu"))
                {
                    // Check if it has buttons
                    var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    if (buttons.Length > 0)
                    {
                        mainMenuRoot = canvas.gameObject;
                        Debug.Log($"[ATSAccessibility] DEBUG: Found main menu candidate: {canvas.gameObject.name} with {buttons.Length} buttons");
                        break;
                    }
                }
            }

            // Fallback: if no "menu" canvas found, look for any canvas with multiple buttons
            if (mainMenuRoot == null)
            {
                foreach (var canvas in canvases)
                {
                    if (!canvas.gameObject.activeInHierarchy) continue;

                    var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    if (buttons.Length >= 3) // Main menu typically has several buttons
                    {
                        mainMenuRoot = canvas.gameObject;
                        Debug.Log($"[ATSAccessibility] DEBUG: Using fallback canvas: {canvas.gameObject.name} with {buttons.Length} buttons");
                        break;
                    }
                }
            }

            if (mainMenuRoot != null)
            {
                _uiNavigator?.SetupMenuNavigation(mainMenuRoot, "Main Menu");
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup); // Reuse Popup context for menu
            }
            else
            {
                Debug.LogWarning("[ATSAccessibility] Could not find main menu canvas");
            }
        }

        private void PollGameState()
        {
            // Check if game is active (we're in settlement with GameController initialized)
            bool isGameActive = GameReflection.GetIsGameActive();

            if (isGameActive && !_wasGameActive)
            {
                // Just entered game
                _wasGameActive = true;

                if (!_announcedGameStart && _speechInitialized && Speech.IsAvailable)
                {
                    Speech.Say("Game started");
                    _announcedGameStart = true;
                    Debug.Log("[ATSAccessibility] Announced: Game started");
                }
            }
            else if (!isGameActive && _wasGameActive)
            {
                // Just left game
                _wasGameActive = false;
                // State will be reset in OnSceneUnloaded
            }
        }

        // ========================================
        // POPUP EVENT SUBSCRIPTION
        // ========================================

        /// <summary>
        /// Try to subscribe to PopupsService events.
        /// Called periodically until successful.
        /// </summary>
        private void TrySubscribeToPopups()
        {
            if (_subscribedToPopups) return;

            var popupsService = GameReflection.GetPopupsService();
            if (popupsService == null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: PopupsService is null, waiting...");
                return;
            }

            Debug.Log($"[ATSAccessibility] DEBUG: Got PopupsService: {popupsService.GetType().FullName}");

            try
            {
                var popupsServiceType = popupsService.GetType();

                // Get AnyPopupShown observable
                var shownProperty = popupsServiceType.GetProperty("AnyPopupShown");
                var shownObservable = shownProperty?.GetValue(popupsService);
                Debug.Log($"[ATSAccessibility] DEBUG: AnyPopupShown property: {shownProperty != null}, observable: {shownObservable != null}");

                // Get AnyPopupHidden observable
                var hiddenProperty = popupsServiceType.GetProperty("AnyPopupHidden");
                var hiddenObservable = hiddenProperty?.GetValue(popupsService);
                Debug.Log($"[ATSAccessibility] DEBUG: AnyPopupHidden property: {hiddenProperty != null}, observable: {hiddenObservable != null}");

                if (shownObservable != null && hiddenObservable != null)
                {
                    // Subscribe to observables using reflection
                    _popupShownSubscription = SubscribeToObservable(shownObservable, OnPopupShown);
                    _popupHiddenSubscription = SubscribeToObservable(hiddenObservable, OnPopupHidden);

                    Debug.Log($"[ATSAccessibility] DEBUG: Subscriptions created - shown: {_popupShownSubscription != null}, hidden: {_popupHiddenSubscription != null}");

                    _subscribedToPopups = true;
                    Debug.Log("[ATSAccessibility] Subscribed to popup events");
                }
                else
                {
                    Debug.LogWarning("[ATSAccessibility] DEBUG: Observables are null, cannot subscribe");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to subscribe to popups: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Subscribe to a UniRx IObservable using reflection.
        /// </summary>
        private IDisposable SubscribeToObservable(object observable, Action<object> callback)
        {
            if (observable == null) return null;

            try
            {
                var observableType = observable.GetType();
                Debug.Log($"[ATSAccessibility] DEBUG: Observable type: {observableType.FullName}");

                // UniRx Subject<T> uses Subscribe(IObserver<T>), not Subscribe(Action<T>)
                // We need to create an IObserver wrapper
                var methods = observableType.GetMethods();

                foreach (var method in methods)
                {
                    if (method.Name != "Subscribe") continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length != 1) continue;

                    var paramType = parameters[0].ParameterType;
                    if (!paramType.IsGenericType) continue;

                    // Check for IObserver<T>
                    if (paramType.GetGenericTypeDefinition() == typeof(IObserver<>))
                    {
                        var elementType = paramType.GetGenericArguments()[0];
                        Debug.Log($"[ATSAccessibility] DEBUG: Found Subscribe(IObserver<{elementType.Name}>)");

                        // Create our observer wrapper
                        var observerType = typeof(ActionObserver<>).MakeGenericType(elementType);
                        var observer = Activator.CreateInstance(observerType, new object[] { callback });

                        // Invoke Subscribe
                        var result = method.Invoke(observable, new object[] { observer });
                        Debug.Log($"[ATSAccessibility] DEBUG: Subscribe invoked, result: {result != null}");
                        return result as IDisposable;
                    }
                }

                Debug.LogWarning("[ATSAccessibility] DEBUG: No matching Subscribe method found");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Observable subscription failed: {ex.Message}\n{ex.StackTrace}");
            }

            return null;
        }

        /// <summary>
        /// IObserver wrapper that calls an Action for each OnNext.
        /// </summary>
        private class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<object> _callback;

            public ActionObserver(Action<object> callback)
            {
                _callback = callback;
            }

            public void OnNext(T value)
            {
                try
                {
                    _callback?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] Observer callback error: {ex.Message}");
                }
            }

            public void OnError(Exception error)
            {
                Debug.LogError($"[ATSAccessibility] Observable error: {error.Message}");
            }

            public void OnCompleted()
            {
                Debug.Log("[ATSAccessibility] Observable completed");
            }
        }

        /// <summary>
        /// Dispose popup subscriptions.
        /// </summary>
        private void DisposePopupSubscriptions()
        {
            _popupShownSubscription?.Dispose();
            _popupHiddenSubscription?.Dispose();
            _popupShownSubscription = null;
            _popupHiddenSubscription = null;
            _subscribedToPopups = false;
        }

        /// <summary>
        /// Called when a popup is shown.
        /// </summary>
        private void OnPopupShown(object popup)
        {
            Debug.Log($"[ATSAccessibility] Popup shown event received");
            _uiNavigator?.OnPopupShown(popup);
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
        }

        /// <summary>
        /// Called when a popup is hidden.
        /// </summary>
        private void OnPopupHidden(object popup)
        {
            Debug.Log($"[ATSAccessibility] Popup hidden event received");
            _uiNavigator?.OnPopupHidden(popup);

            // Only handle context change if no more popups active
            if (_uiNavigator != null && !_uiNavigator.HasActivePopup)
            {
                // If on menu scene, defer menu setup until user presses a key
                // This ensures popup close animation is complete and elements are inactive
                if (SceneManager.GetActiveScene().buildIndex == SCENE_MENU)
                {
                    Debug.Log("[ATSAccessibility] Popup closed on menu scene, deferring menu setup to next input");
                    _menuPendingSetup = true;
                    // Keep context as Popup so navigation keys work
                }
                else
                {
                    _keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);
                }
            }
        }

        // ========================================
        // TUTORIAL EVENT HANDLING
        // ========================================

        /// <summary>
        /// Called when tutorial phase changes.
        /// Schedules announcement with delay to let tooltip text update.
        /// Uses real-time delay since game may have timeScale=0 during loading.
        /// </summary>
        private void OnTutorialPhaseChanged()
        {
            Debug.Log("[ATSAccessibility] Tutorial phase changed, scheduling announcement (real-time)");
            StartCoroutine(AnnounceTutorialDelayed());
        }

        /// <summary>
        /// Coroutine to announce tutorial after real-time delay.
        /// </summary>
        private IEnumerator AnnounceTutorialDelayed()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log("[ATSAccessibility] AnnounceTutorial() called after real-time delay");
            _tutorialHandler?.AnnounceTooltip();
        }
    }
}
