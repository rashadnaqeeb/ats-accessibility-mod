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
        private const int SCENE_WORLDMAP = 3;

        // Delay for announcements after scene load (ensures UI is fully initialized)
        private const float ANNOUNCEMENT_DELAY = 0.5f;

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

        // Map Navigation
        private MapNavigator _mapNavigator;

        // Popup event subscriptions (IDisposable from UniRx)
        private IDisposable _popupShownSubscription;
        private IDisposable _popupHiddenSubscription;
        private bool _subscribedToPopups = false;

        // Tutorial handling
        private TutorialHandler _tutorialHandler;

        // Encyclopedia/wiki navigation
        private EncyclopediaNavigator _encyclopediaNavigator;

        // Map scanner for quick object finding
        private MapScanner _mapScanner;

        // Stats panel for game statistics
        private StatsPanel _statsPanel;

        // Mysteries panel for forest mysteries and modifiers
        private MysteriesPanel _mysteriesPanel;

        // Settlement resource panel for inventory browsing
        private SettlementResourcePanel _settlementResourcePanel;

        // Villagers panel for villager information
        private VillagersPanel _villagersPanel;

        // Info panel menu for unified panel access
        private InfoPanelMenu _infoPanelMenu;

        // Menu hub for quick popup access
        private MenuHub _menuHub;

        // Building menu panel for construction
        private BuildingMenuPanel _buildingMenuPanel;

        // Build mode controller for placing buildings
        private BuildModeController _buildModeController;

        // Move mode controller for relocating buildings
        private MoveModeController _moveModeController;

        // World map navigator
        private WorldMapNavigator _worldMapNavigator;
        private WorldMapScanner _worldMapScanner;
        private bool _announcedWorldMap = false;

        // Embark panel for pre-expedition setup
        private EmbarkPanel _embarkPanel;
        private IDisposable _embarkShownSubscription;
        private IDisposable _embarkClosedSubscription;
        private bool _subscribedToEmbark = false;

        // Building panel handler for building accessibility
        private BuildingPanelHandler _buildingPanelHandler;

        // Announcements settings panel
        private AnnouncementsSettingsPanel _announcementsPanel;

        // Event announcer for game events
        private EventAnnouncer _eventAnnouncer;

        // Announcement history panel for reviewing recent events
        private AnnouncementHistoryPanel _announcementHistoryPanel;

        // Confirmation dialog for destructive actions
        private ConfirmationDialog _confirmationDialog;

        // Deferred menu rebuild (wait for user input after popup closes)
        private bool _menuPendingSetup = false;

        // Cached main menu canvas (cleared on scene unload)
        private GameObject _cachedMainMenuCanvas = null;

        private void Start()
        {
            Debug.Log("[ATSAccessibility] AccessibilityCore.Start()");

            // Subscribe to scene events
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // Initialize speech (Tolk)
            _speechInitialized = Speech.Initialize();

            // Initialize UI navigation
            _uiNavigator = new UINavigator(this);
            _keyboardManager = new KeyboardManager();

            // Initialize map navigation
            _mapNavigator = new MapNavigator();

            // Initialize tutorial handler
            _tutorialHandler = new TutorialHandler(OnTutorialPhaseChanged);

            // Initialize encyclopedia navigator
            _encyclopediaNavigator = new EncyclopediaNavigator();

            // Initialize map scanner
            _mapScanner = new MapScanner(_mapNavigator);

            // Initialize stats panel
            _statsPanel = new StatsPanel();

            // Initialize mysteries panel
            _mysteriesPanel = new MysteriesPanel();

            // Initialize settlement resource panel
            _settlementResourcePanel = new SettlementResourcePanel();

            // Initialize villagers panel
            _villagersPanel = new VillagersPanel();

            // Initialize announcements settings panel
            _announcementsPanel = new AnnouncementsSettingsPanel();

            // Initialize event announcer
            _eventAnnouncer = new EventAnnouncer();

            // Initialize announcement history panel
            _announcementHistoryPanel = new AnnouncementHistoryPanel();

            // Initialize info panel menu (unified access to stats, resources, mysteries, villagers, announcements)
            _infoPanelMenu = new InfoPanelMenu(_statsPanel, _settlementResourcePanel, _mysteriesPanel, _villagersPanel, _announcementsPanel);

            // Initialize menu hub for quick popup access
            _menuHub = new MenuHub();

            // Initialize building menu panel and build mode controller
            _buildingMenuPanel = new BuildingMenuPanel();
            _buildModeController = new BuildModeController(_mapNavigator, _buildingMenuPanel);
            _buildingMenuPanel.SetBuildModeController(_buildModeController);

            // Initialize move mode controller
            _moveModeController = new MoveModeController(_mapNavigator);

            // Initialize world map navigator and scanner
            _worldMapNavigator = new WorldMapNavigator();
            _worldMapScanner = new WorldMapScanner(_worldMapNavigator);

            // Initialize embark panel
            _embarkPanel = new EmbarkPanel();

            // Initialize building panel handler
            _buildingPanelHandler = new BuildingPanelHandler();

            // Initialize confirmation dialog for destructive actions
            _confirmationDialog = new ConfirmationDialog();

            // Create context handlers for settlement and world map
            var settlementHandler = new SettlementKeyHandler(
                _mapNavigator, _mapScanner, _infoPanelMenu, _menuHub, _buildingMenuPanel, _moveModeController, _announcementHistoryPanel, _confirmationDialog);
            var worldMapHandler = new WorldMapKeyHandler(_worldMapNavigator, _worldMapScanner);

            // Register key handlers in priority order (highest priority first)
            _keyboardManager.RegisterHandler(_confirmationDialog);  // Confirmation dialog (blocks all input when active)
            _keyboardManager.RegisterHandler(_infoPanelMenu);       // F1 menu and child panels
            _keyboardManager.RegisterHandler(_menuHub);             // F2 quick access menu
            _keyboardManager.RegisterHandler(_announcementHistoryPanel); // Alt+H announcement history
            _keyboardManager.RegisterHandler(_buildingPanelHandler); // Building panel accessibility
            _keyboardManager.RegisterHandler(_buildingMenuPanel);   // Tab building menu
            _keyboardManager.RegisterHandler(_buildModeController); // Building placement (selective passthrough)
            _keyboardManager.RegisterHandler(_moveModeController);  // Building relocation (selective passthrough)
            _keyboardManager.RegisterHandler(_encyclopediaNavigator); // Wiki popup
            _keyboardManager.RegisterHandler(_uiNavigator);         // Generic popup/menu navigation
            _keyboardManager.RegisterHandler(_embarkPanel);         // Pre-expedition setup
            _keyboardManager.RegisterHandler(_tutorialHandler);     // Tutorial tooltips (passthrough)
            _keyboardManager.RegisterHandler(settlementHandler);    // Settlement map navigation (fallback)
            _keyboardManager.RegisterHandler(worldMapHandler);      // World map navigation (fallback)

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

            // Dispose building panel handler
            _buildingPanelHandler?.Dispose();

            Speech.Shutdown();
        }

        private void Update()
        {
            // Process pending event announcements (batches messages to prevent interruption)
            _eventAnnouncer?.ProcessMessageQueue();

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

                // Try to subscribe to embark events if not already subscribed
                if (!_subscribedToEmbark)
                {
                    TrySubscribeToEmbark();
                }

                // Try to subscribe to tutorial phase changes
                _tutorialHandler?.TrySubscribe();

                // Try to subscribe to building panel events
                _buildingPanelHandler?.TrySubscribe();

                // Try to subscribe to game events for announcements
                _eventAnnouncer?.TrySubscribe();
            }
        }

        private void OnGUI()
        {
            // Process input in OnGUI - this captures input even when UI has focus
            Event e = Event.current;
            if (e != null && e.isKey && e.type == EventType.KeyDown)
            {
                // Deferred menu setup - rebuild on first key press after popup closes
                if (_menuPendingSetup)
                {
                    Debug.Log("[ATSAccessibility] Rebuilding menu navigation on user input");
                    SetupMainMenuNavigation();
                    _menuPendingSetup = false;
                }

                var modifiers = new KeyboardManager.KeyModifiers(e.control, e.alt, e.shift);
                _keyboardManager?.ProcessKeyEvent(e.keyCode, modifiers);
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

            // Cancel any pending Invoke calls to prevent callbacks on destroyed objects
            CancelInvoke(nameof(AnnounceMainMenu));
            CancelInvoke(nameof(SetupWorldMapNavigation));
            CancelInvoke(nameof(SetupMainMenuNavigation));

            // Clear state when leaving scenes
            if (scene.buildIndex == SCENE_GAME)
            {
                _announcedGameStart = false;
                _wasGameActive = false;
                _mapNavigator?.ClearCursor();  // Clear so it reinitializes on next game
            }
            else if (scene.buildIndex == SCENE_MENU)
            {
                _announcedMainMenu = false;
                _cachedMainMenuCanvas = null;
            }
            else if (scene.buildIndex == SCENE_WORLDMAP)
            {
                _announcedWorldMap = false;
                _worldMapNavigator?.Reset();
            }

            // Dispose popup subscriptions (PopupsService is destroyed on scene change)
            DisposePopupSubscriptions();

            // Dispose embark subscriptions (WorldBlackboardService is destroyed on scene change)
            DisposeEmbarkSubscriptions();

            // Dispose building panel handler subscriptions
            _buildingPanelHandler?.Dispose();

            // Dispose tutorial handler subscription
            _tutorialHandler?.Dispose();

            // Dispose event announcer subscriptions
            _eventAnnouncer?.Dispose();

            // Reset UI navigator state
            _uiNavigator?.Reset();
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);
        }

        private void ProcessSceneLoad(Scene scene)
        {
            if (scene.buildIndex == SCENE_MENU && !_announcedMainMenu)
            {
                // Delay announcement to ensure scene is fully loaded
                Invoke(nameof(AnnounceMainMenu), ANNOUNCEMENT_DELAY);
            }
            else if (scene.buildIndex == SCENE_GAME)
            {
                // For game scene, we wait for GameController.IsGameActive
                // This is handled by polling since the controller initializes async
                _announcedGameStart = false;
            }
            else if (scene.buildIndex == SCENE_WORLDMAP)
            {
                // Delay to allow WorldController to initialize
                _announcedWorldMap = false;
                Invoke(nameof(SetupWorldMapNavigation), ANNOUNCEMENT_DELAY);
            }
        }

        private void AnnounceMainMenu()
        {
            if (_announcedMainMenu) return;

            if (_speechInitialized && Speech.IsAvailable)
            {
                Speech.Say("Main menu");
                _announcedMainMenu = true;
                Debug.Log("[ATSAccessibility] Announced: Main menu");

                // Set up main menu navigation after a short delay (let UI initialize)
                Invoke(nameof(SetupMainMenuNavigation), ANNOUNCEMENT_DELAY);
            }
            else
            {
                Debug.LogWarning("[ATSAccessibility] Speech not available for main menu announcement");
            }
        }

        private void SetupMainMenuNavigation()
        {
            // Use cached canvas if available and still valid
            if (_cachedMainMenuCanvas != null && _cachedMainMenuCanvas.activeInHierarchy)
            {
                _uiNavigator?.SetupMenuNavigation(_cachedMainMenuCanvas, "Main Menu");
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Find the main menu Canvas (expensive FindObjectsOfType call - cache the result)
            var canvases = FindObjectsOfType<Canvas>();

            GameObject mainMenuRoot = null;

            foreach (var canvas in canvases)
            {
                // Skip inactive canvases
                if (!canvas.gameObject.activeInHierarchy) continue;

                string name = canvas.gameObject.name.ToLower();

                // Look for main menu indicators
                if (name.Contains("mainmenu") || name.Contains("main menu") || name.Contains("menu"))
                {
                    // Check if it has buttons
                    var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    if (buttons.Length > 0)
                    {
                        mainMenuRoot = canvas.gameObject;
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
                        break;
                    }
                }
            }

            if (mainMenuRoot != null)
            {
                _cachedMainMenuCanvas = mainMenuRoot;
                _uiNavigator?.SetupMenuNavigation(mainMenuRoot, "Main Menu");
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup); // Reuse Popup context for menu
            }
            else
            {
                Debug.LogWarning("[ATSAccessibility] Could not find main menu canvas");
            }
        }

        private void SetupWorldMapNavigation()
        {
            if (_announcedWorldMap) return;

            if (WorldMapReflection.IsWorldMapActive())
            {
                _worldMapNavigator?.Reset();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);

                if (_speechInitialized && Speech.IsAvailable)
                {
                    Speech.Say("World map");
                    _announcedWorldMap = true;
                    Debug.Log("[ATSAccessibility] Announced: World map");
                }
            }
            else
            {
                // WorldController not ready yet, retry
                Debug.Log("[ATSAccessibility] WorldController not ready, retrying...");
                Invoke(nameof(SetupWorldMapNavigation), ANNOUNCEMENT_DELAY);
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

                // Set map navigation context (if no popup is open)
                // Note: Don't call ResetCursor here - game services aren't fully loaded yet.
                // Let lazy initialization in MoveCursor() handle it on first arrow key press.
                if (_uiNavigator == null || !_uiNavigator.HasActivePopup)
                {
                    _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Map);
                    Debug.Log("[ATSAccessibility] Set context to Map navigation");
                }
            }
            else if (!isGameActive && _wasGameActive)
            {
                // Just left game
                _wasGameActive = false;
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);
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
            if (popupsService == null) return;

            try
            {
                var popupsServiceType = popupsService.GetType();

                // Get AnyPopupShown observable
                var shownProperty = popupsServiceType.GetProperty("AnyPopupShown");
                var shownObservable = shownProperty?.GetValue(popupsService);

                // Get AnyPopupHidden observable
                var hiddenProperty = popupsServiceType.GetProperty("AnyPopupHidden");
                var hiddenObservable = hiddenProperty?.GetValue(popupsService);

                if (shownObservable != null && hiddenObservable != null)
                {
                    // Subscribe to observables using shared utility
                    _popupShownSubscription = GameReflection.SubscribeToObservable(shownObservable, OnPopupShown);
                    _popupHiddenSubscription = GameReflection.SubscribeToObservable(hiddenObservable, OnPopupHidden);

                    _subscribedToPopups = true;
                    Debug.Log("[ATSAccessibility] Subscribed to popup events");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to subscribe to popups: {ex.Message}\n{ex.StackTrace}");
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

        // ========================================
        // EMBARK EVENT SUBSCRIPTION
        // ========================================

        /// <summary>
        /// Try to subscribe to embark screen events from WorldBlackboardService.
        /// Called periodically until successful.
        /// </summary>
        private void TrySubscribeToEmbark()
        {
            if (_subscribedToEmbark) return;

            // Only subscribe when on world map scene
            if (SceneManager.GetActiveScene().buildIndex != SCENE_WORLDMAP) return;

            try
            {
                // Subscribe to OnFieldPreviewShown (embark screen opened)
                _embarkShownSubscription = EmbarkReflection.SubscribeToFieldPreviewShown(OnEmbarkScreenShown);

                // Subscribe to OnFieldPreviewClosed (embark screen closed)
                _embarkClosedSubscription = EmbarkReflection.SubscribeToFieldPreviewClosed(OnEmbarkScreenClosed);

                if (_embarkShownSubscription != null && _embarkClosedSubscription != null)
                {
                    _subscribedToEmbark = true;
                    Debug.Log("[ATSAccessibility] Subscribed to embark screen events");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to subscribe to embark events: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose embark subscriptions.
        /// </summary>
        private void DisposeEmbarkSubscriptions()
        {
            _embarkShownSubscription?.Dispose();
            _embarkClosedSubscription?.Dispose();
            _embarkShownSubscription = null;
            _embarkClosedSubscription = null;
            _subscribedToEmbark = false;

            // Close embark panel if open
            _embarkPanel?.Close();
        }

        /// <summary>
        /// Called when the embark screen is shown (OnFieldPreviewShown event).
        /// </summary>
        private void OnEmbarkScreenShown(object worldField)
        {
            Debug.Log("[ATSAccessibility] Embark screen shown");
            _embarkPanel?.Open(worldField);
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Embark);
        }

        /// <summary>
        /// Called when the embark screen is closed (OnFieldPreviewClosed event).
        /// </summary>
        private void OnEmbarkScreenClosed(object worldField)
        {
            Debug.Log("[ATSAccessibility] Embark screen closed");
            _embarkPanel?.Close();

            // Return to world map context
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
        }

        /// <summary>
        /// Called when a popup is shown.
        /// </summary>
        private void OnPopupShown(object popup)
        {
            Debug.Log($"[ATSAccessibility] Popup shown event received");

            // Check wiki popup FIRST - it has its own navigator
            if (GameReflection.IsWikiPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wiki popup detected, using Encyclopedia navigator");
                _encyclopediaNavigator?.OnWikiPopupShown(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Encyclopedia);
                return;
            }

            // Standard popup handling
            _uiNavigator?.OnPopupShown(popup);
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
        }

        /// <summary>
        /// Called when a popup is hidden.
        /// </summary>
        private void OnPopupHidden(object popup)
        {
            Debug.Log($"[ATSAccessibility] Popup hidden event received");

            // Check wiki popup first
            if (GameReflection.IsWikiPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wiki popup closed");
                _encyclopediaNavigator?.OnWikiPopupHidden();
                // Fall through to handle context change
            }
            else
            {
                _uiNavigator?.OnPopupHidden(popup);
            }

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
                else if (GameReflection.GetIsGameActive())
                {
                    // In settlement - return to map navigation
                    _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Map);
                    Debug.Log("[ATSAccessibility] Popup closed in settlement, returning to Map context");
                }
                else if (SceneManager.GetActiveScene().buildIndex == SCENE_WORLDMAP)
                {
                    _keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
                    Debug.Log("[ATSAccessibility] Popup closed on world map, returning to WorldMap context");
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
        /// Needs enough delay for tooltip to update its text content.
        /// </summary>
        private IEnumerator AnnounceTutorialDelayed()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[ATSAccessibility] AnnounceTutorial() called after real-time delay");
            _tutorialHandler?.AnnounceTooltip();
        }
    }
}
