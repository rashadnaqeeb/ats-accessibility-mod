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

        // Workers panel for profession counts by race
        private WorkersPanel _workersPanel;

        // Info panel menu for unified panel access
        private InfoPanelMenu _infoPanelMenu;

        // Menu hub for quick popup access
        private MenuHub _menuHub;

        // Rewards panel for quick reward selection
        private RewardsPanel _rewardsPanel;

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

        // Recipes popup overlay for recipe management
        private RecipesOverlay _recipesOverlay;

        // Wildcard popup overlay for blueprint selection
        private WildcardOverlay _wildcardOverlay;

        // Reputation reward popup overlay for blueprint rewards
        private ReputationRewardOverlay _reputationRewardOverlay;

        // Cornerstone popup overlays for perk selection
        private CornerstoneOverlay _cornerstoneOverlay;
        private CornerstoneLimitOverlay _cornerstoneLimitOverlay;

        // Newcomers popup overlay for group selection
        private NewcomersOverlay _newcomersOverlay;

        // Orders popup overlays for order navigation and pick selection
        private OrdersOverlay _ordersOverlay;
        private OrderPickOverlay _orderPickOverlay;

        // Consumption control popup overlay
        private ConsumptionOverlay _consumptionOverlay;

        // Deeds popup overlay for goals navigation
        private DeedsOverlay _deedsOverlay;

        // Rewards pack popup overlay (port expedition rewards)
        private RewardsPackOverlay _rewardsPackOverlay;

        // Trader panel overlay for trading with merchants
        private TraderOverlay _traderOverlay;

        // Assault result popup overlay (after assaulting a trader)
        private AssaultResultOverlay _assaultResultOverlay;

        // Dialogue overlay for NPC dialogue navigation
        private DialogueOverlay _dialogueOverlay;

        // Trade routes popup overlay for trade town navigation
        private TradeRoutesOverlay _tradeRoutesOverlay;

        // Cycle end popup overlay for Blightstorm cycle completion
        private CycleEndOverlay _cycleEndOverlay;

        // Payments popup overlay for pending payments/obligations
        private PaymentsOverlay _paymentsOverlay;

        // Meta rewards popup overlay for end-of-game rewards and level-up
        private MetaRewardsOverlay _metaRewardsOverlay;

        // Game result popup overlay for victory/defeat screen
        private GameResultOverlay _gameResultOverlay;

        // Black Market popup overlay for trading offers
        private BlackMarketOverlay _blackMarketOverlay;

        // Altar (Forsaken Altar) popup overlay for cornerstone upgrades
        private AltarOverlay _altarOverlay;

        // Perk Crafter (Cornerstone Forge) popup overlay for cornerstone crafting
        private PerkCrafterOverlay _perkCrafterOverlay;

        // Games History popup overlay for past settlements
        private GamesHistoryOverlay _gamesHistoryOverlay;

        // Profiles popup overlay for save selection
        private ProfilesOverlay _profilesOverlay;

        // Daily Expedition popup overlay for daily challenge
        private DailyExpeditionOverlay _dailyExpeditionOverlay;

        // Custom Games popup overlay for Training Expeditions
        private CustomGamesOverlay _customGamesOverlay;

        // Tutorial tooltip handler for tutorial text navigation
        private TutorialTooltipHandler _tutorialTooltipHandler;

        // Capital screen overlay for Smoldering City
        private CapitalOverlay _capitalOverlay;
        private CapitalUpgradeOverlay _capitalUpgradeOverlay;
        private IronmanOverlay _ironmanOverlay;
        private IDisposable _capitalEnabledSubscription;
        private IDisposable _capitalClosedSubscription;
        private bool _subscribedToCapital = false;

        // World tutorials overlay for tutorial selection on world map
        private WorldTutorialsOverlay _worldTutorialsOverlay;

        // Seal overlay for Sealed Forest biome seal building
        private SealOverlay _sealOverlay;

        // World event overlay for world map event decisions
        private WorldEventOverlay _worldEventOverlay;

        // Trends overlay for storage operations
        private TrendsOverlay _trendsOverlay;

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

            // Initialize workers panel
            _workersPanel = new WorkersPanel();

            // Initialize announcements settings panel
            _announcementsPanel = new AnnouncementsSettingsPanel();

            // Initialize event announcer
            _eventAnnouncer = new EventAnnouncer();

            // Initialize announcement history panel
            _announcementHistoryPanel = new AnnouncementHistoryPanel();

            // Initialize info panel menu (unified access to stats, resources, mysteries, villagers, workers, announcements)
            _infoPanelMenu = new InfoPanelMenu(_statsPanel, _settlementResourcePanel, _mysteriesPanel, _villagersPanel, _workersPanel, _announcementsPanel);

            // Initialize menu hub for quick popup access
            _menuHub = new MenuHub();

            // Initialize rewards panel for quick reward selection
            _rewardsPanel = new RewardsPanel();

            // Initialize building menu panel and build mode controller
            _buildingMenuPanel = new BuildingMenuPanel();
            _buildModeController = new BuildModeController(_mapNavigator, _buildingMenuPanel);
            _buildingMenuPanel.SetBuildModeController(_buildModeController);

            // Initialize move mode controller
            _moveModeController = new MoveModeController(_mapNavigator);

            // Initialize harvest mark handler for tree marking/unmarking
            var harvestMarkHandler = new HarvestMarkHandler(_mapNavigator);

            // Initialize world map navigator and scanner
            _worldMapNavigator = new WorldMapNavigator();
            _worldMapScanner = new WorldMapScanner(_worldMapNavigator);

            // Initialize embark panel
            _embarkPanel = new EmbarkPanel();

            // Initialize building panel handler
            _buildingPanelHandler = new BuildingPanelHandler(_keyboardManager);

            // Initialize confirmation dialog for destructive actions
            _confirmationDialog = new ConfirmationDialog();

            // Initialize recipes overlay for Recipes popup
            _recipesOverlay = new RecipesOverlay();

            // Initialize wildcard overlay for blueprint selection popup
            _wildcardOverlay = new WildcardOverlay();

            // Initialize reputation reward overlay for blueprint rewards popup
            _reputationRewardOverlay = new ReputationRewardOverlay();

            // Initialize cornerstone overlays for perk selection popups
            _cornerstoneOverlay = new CornerstoneOverlay();
            _cornerstoneLimitOverlay = new CornerstoneLimitOverlay();

            // Initialize newcomers overlay for group selection popup
            _newcomersOverlay = new NewcomersOverlay();

            // Initialize orders overlays for order navigation and pick selection
            _ordersOverlay = new OrdersOverlay();
            _orderPickOverlay = new OrderPickOverlay();

            // Wire up event announcer to refresh orders overlay when new orders arrive
            _eventAnnouncer.OnNewOrderAvailable = () => _ordersOverlay?.RefreshOnNewOrder();

            // Initialize consumption control overlay
            _consumptionOverlay = new ConsumptionOverlay();

            // Initialize deeds overlay for goals popup
            _deedsOverlay = new DeedsOverlay();

            // Initialize rewards pack overlay (port expedition rewards)
            _rewardsPackOverlay = new RewardsPackOverlay();

            // Initialize trader overlay
            _traderOverlay = new TraderOverlay();

            // Initialize assault result overlay
            _assaultResultOverlay = new AssaultResultOverlay();

            // Initialize dialogue overlay for NPC dialogue navigation
            _dialogueOverlay = new DialogueOverlay();

            // Initialize seal overlay for Sealed Forest biome
            _sealOverlay = new SealOverlay();

            // Initialize world event overlay for world map events
            _worldEventOverlay = new WorldEventOverlay();

            // Initialize trends overlay for storage operations
            _trendsOverlay = new TrendsOverlay();

            // Initialize trade routes overlay for trade town navigation
            _tradeRoutesOverlay = new TradeRoutesOverlay();

            // Initialize cycle end overlay for Blightstorm cycle completion
            _cycleEndOverlay = new CycleEndOverlay();

            // Initialize payments overlay for pending payments/obligations
            _paymentsOverlay = new PaymentsOverlay();

            // Initialize meta rewards overlay for end-of-game rewards and level-up
            _metaRewardsOverlay = new MetaRewardsOverlay(this);

            // Initialize game result overlay for victory/defeat screen
            _gameResultOverlay = new GameResultOverlay();

            // Initialize black market overlay for trading offers
            _blackMarketOverlay = new BlackMarketOverlay();

            // Initialize altar overlay for Forsaken Altar
            _altarOverlay = new AltarOverlay();

            // Initialize perk crafter overlay for Cornerstone Forge
            _perkCrafterOverlay = new PerkCrafterOverlay();

            // Initialize games history overlay
            _gamesHistoryOverlay = new GamesHistoryOverlay();

            // Initialize profiles overlay
            _profilesOverlay = new ProfilesOverlay();

            // Initialize daily expedition overlay
            _dailyExpeditionOverlay = new DailyExpeditionOverlay();

            // Initialize custom games overlay (Training Expeditions)
            _customGamesOverlay = new CustomGamesOverlay();

            // Initialize capital screen overlay
            _capitalOverlay = new CapitalOverlay();
            _capitalUpgradeOverlay = new CapitalUpgradeOverlay();
            _ironmanOverlay = new IronmanOverlay();

            // Initialize tutorial tooltip handler (needs UINavigator to check for blocking popups)
            _tutorialTooltipHandler = new TutorialTooltipHandler(_uiNavigator);

            // Initialize world tutorials overlay for world map tutorial selection
            _worldTutorialsOverlay = new WorldTutorialsOverlay();

            // Create context handlers for settlement and world map
            var settlementHandler = new SettlementKeyHandler(
                _mapNavigator, _mapScanner, _infoPanelMenu, _menuHub, _rewardsPanel, _buildingMenuPanel, _moveModeController, _announcementHistoryPanel, _confirmationDialog, harvestMarkHandler);
            var worldMapHandler = new WorldMapKeyHandler(_worldMapNavigator, _worldMapScanner);
            worldMapHandler.SetTutorialsOverlay(_worldTutorialsOverlay);

            // Register key handlers in priority order (highest priority first)
            _keyboardManager.RegisterHandler(_tutorialTooltipHandler);  // Tutorial tooltip (blocks input during tutorial)
            _keyboardManager.RegisterHandler(_confirmationDialog);  // Confirmation dialog (blocks all input when active)
            _keyboardManager.RegisterHandler(_metaRewardsOverlay);  // Meta rewards/level-up popup (above game result so player can close it first)
            _keyboardManager.RegisterHandler(_gameResultOverlay);  // Game result (victory/defeat) - high priority terminal state
            _keyboardManager.RegisterHandler(new SettlementInfoHandler()); // Alt+S/V/O settlement info (above all menus/overlays)
            _keyboardManager.RegisterHandler(_infoPanelMenu);       // F1 menu and child panels
            _keyboardManager.RegisterHandler(_menuHub);             // F2 quick access menu
            _keyboardManager.RegisterHandler(_rewardsPanel);        // F3 rewards panel
            _keyboardManager.RegisterHandler(_announcementHistoryPanel); // Alt+H announcement history
            _keyboardManager.RegisterHandler(_buildingPanelHandler); // Building panel accessibility
            _keyboardManager.RegisterHandler(_buildingMenuPanel);   // Tab building menu
            _keyboardManager.RegisterHandler(_buildModeController); // Building placement (selective passthrough)
            _keyboardManager.RegisterHandler(_moveModeController);  // Building relocation (selective passthrough)
            _keyboardManager.RegisterHandler(harvestMarkHandler);    // Tree mark/unmark selection
            _keyboardManager.RegisterHandler(_encyclopediaNavigator); // Wiki popup
            _keyboardManager.RegisterHandler(_recipesOverlay);      // Recipes popup overlay
            _keyboardManager.RegisterHandler(_wildcardOverlay);    // Wildcard popup overlay
            _keyboardManager.RegisterHandler(_cornerstoneLimitOverlay);   // Cornerstone limit popup overlay
            _keyboardManager.RegisterHandler(_cornerstoneOverlay);       // Cornerstone pick popup overlay
            _keyboardManager.RegisterHandler(_newcomersOverlay);         // Newcomers group selection overlay
            _keyboardManager.RegisterHandler(_orderPickOverlay);         // Order pick popup overlay (higher priority - child popup)
            _keyboardManager.RegisterHandler(_ordersOverlay);            // Orders popup overlay
            _keyboardManager.RegisterHandler(_consumptionOverlay);       // Consumption control popup overlay
            _keyboardManager.RegisterHandler(_deedsOverlay);             // Deeds (goals) popup overlay
            _keyboardManager.RegisterHandler(_reputationRewardOverlay);  // Reputation reward popup overlay
            _keyboardManager.RegisterHandler(_rewardsPackOverlay);  // Rewards pack popup overlay (port rewards)
            _keyboardManager.RegisterHandler(_assaultResultOverlay); // Assault result popup overlay (before trader so it gets priority)
            _keyboardManager.RegisterHandler(_traderOverlay);        // Trader panel overlay
            _keyboardManager.RegisterHandler(_dialogueOverlay);      // NPC dialogue overlay
            _keyboardManager.RegisterHandler(_sealOverlay);         // Seal building overlay (Sealed Forest)
            _keyboardManager.RegisterHandler(_worldEventOverlay);  // World event popup overlay (world map)
            _keyboardManager.RegisterHandler(_trendsOverlay);     // Trends popup overlay (storage operations)
            _keyboardManager.RegisterHandler(_tradeRoutesOverlay); // Trade routes popup overlay
            _keyboardManager.RegisterHandler(_cycleEndOverlay);   // Cycle end popup overlay (world map)
            _keyboardManager.RegisterHandler(_paymentsOverlay);   // Payments popup overlay
            _keyboardManager.RegisterHandler(_blackMarketOverlay); // Black Market popup overlay
            _keyboardManager.RegisterHandler(_altarOverlay);        // Altar (Forsaken Altar) popup overlay
            _keyboardManager.RegisterHandler(_perkCrafterOverlay);  // Perk Crafter (Cornerstone Forge) popup overlay
            _keyboardManager.RegisterHandler(_gamesHistoryOverlay); // Games History popup overlay
            _keyboardManager.RegisterHandler(_dailyExpeditionOverlay); // Daily Expedition popup overlay
            _keyboardManager.RegisterHandler(_customGamesOverlay);   // Custom Games (Training Expeditions) popup overlay
            _keyboardManager.RegisterHandler(_profilesOverlay);     // Profiles (save selection) popup overlay
            _keyboardManager.RegisterHandler(_uiNavigator);         // Generic popup/menu navigation
            _keyboardManager.RegisterHandler(_embarkPanel);         // Pre-expedition setup
            _keyboardManager.RegisterHandler(_ironmanOverlay);       // Ironman upgrade popup overlay
            _keyboardManager.RegisterHandler(_capitalUpgradeOverlay); // Capital upgrade popup overlay
            _keyboardManager.RegisterHandler(_capitalOverlay);     // Capital screen overlay
            _keyboardManager.RegisterHandler(settlementHandler);    // Settlement map navigation (fallback)
            _keyboardManager.RegisterHandler(_worldTutorialsOverlay); // World tutorials HUD (world map)
            _keyboardManager.RegisterHandler(worldMapHandler);      // World map navigation (fallback)

            // Check if we're already on a scene (mod loaded mid-game)
            CheckCurrentScene();

            // Validate all reflection caches and log results
            ValidateReflectionCaches();
        }

        private void ValidateReflectionCaches()
        {
            try
            {
                int totalMissing = 0;
                totalMissing += GameReflection.LogCacheStatus();
                totalMissing += BuildingReflection.LogCacheStatus();
                totalMissing += WorldMapReflection.LogCacheStatus();
                totalMissing += EmbarkReflection.LogCacheStatus();
                totalMissing += OrdersReflection.LogCacheStatus();
                totalMissing += RecipesReflection.LogCacheStatus();
                totalMissing += RewardsReflection.LogCacheStatus();
                totalMissing += ReputationRewardReflection.LogCacheStatus();
                totalMissing += CornerstoneReflection.LogCacheStatus();
                totalMissing += NewcomersReflection.LogCacheStatus();
                totalMissing += WildcardReflection.LogCacheStatus();
                totalMissing += WikiReflection.LogCacheStatus();
                totalMissing += TradeReflection.LogCacheStatus();
                totalMissing += TradeRoutesReflection.LogCacheStatus();
                totalMissing += BlackMarketReflection.LogCacheStatus();
                totalMissing += AltarReflection.LogCacheStatus();
                totalMissing += PerkCrafterReflection.LogCacheStatus();
                totalMissing += CapitalReflection.LogCacheStatus();
                totalMissing += CapitalUpgradeReflection.LogCacheStatus();
                totalMissing += GameResultReflection.LogCacheStatus();
                totalMissing += DeedsReflection.LogCacheStatus();
                totalMissing += ConsumptionReflection.LogCacheStatus();
                totalMissing += PaymentsReflection.LogCacheStatus();
                totalMissing += NarrationReflection.LogCacheStatus();
                totalMissing += ProfilesReflection.LogCacheStatus();
                totalMissing += CustomGamesReflection.LogCacheStatus();
                totalMissing += SealReflection.LogCacheStatus();
                totalMissing += IronmanReflection.LogCacheStatus();
                totalMissing += WorldEventReflection.LogCacheStatus();
                totalMissing += TrendsReflection.LogCacheStatus();
                totalMissing += TutorialReflection.LogCacheStatus();
                totalMissing += GamesHistoryReflection.LogCacheStatus();
                totalMissing += DailyExpeditionReflection.LogCacheStatus();

                if (totalMissing == 0)
                    Debug.Log("[ATSAccessibility] Reflection validation: All fields cached successfully");
                else
                    Debug.Log($"[ATSAccessibility] Reflection validation: {totalMissing} total fields MISSING across all classes");
            }
            catch (Exception ex)
            {
                Debug.Log($"[ATSAccessibility] Reflection validation failed: {ex.Message}");
            }
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

            // Check for tutorial tooltip text changes (auto-announce)
            _tutorialTooltipHandler?.CheckForTextChanges();

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

                // Try to subscribe to capital screen events if not already subscribed
                if (!_subscribedToCapital)
                {
                    TrySubscribeToCapital();
                }

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
            if (e != null && e.isKey && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
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
                _profilesOverlay?.Close();  // Close if scene unloads during profile switch
            }
            else if (scene.buildIndex == SCENE_WORLDMAP)
            {
                _announcedWorldMap = false;
                _worldMapNavigator?.Reset();
                _worldTutorialsOverlay?.Close();
            }

            // Dispose popup subscriptions (PopupsService is destroyed on scene change)
            DisposePopupSubscriptions();

            // Dispose embark subscriptions (WorldBlackboardService is destroyed on scene change)
            DisposeEmbarkSubscriptions();

            // Dispose capital screen subscriptions (WorldBlackboardService is destroyed on scene change)
            DisposeCapitalSubscriptions();

            // Dispose building panel handler subscriptions
            _buildingPanelHandler?.Dispose();

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
                    Speech.Say("World map", interrupt: false);  // Queue to avoid interrupting other speech
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

        // ========================================
        // CAPITAL SCREEN EVENT SUBSCRIPTION
        // ========================================

        /// <summary>
        /// Try to subscribe to capital screen events from WorldBlackboardService.
        /// Called periodically until successful.
        /// </summary>
        private void TrySubscribeToCapital()
        {
            if (_subscribedToCapital) return;

            // Only subscribe when on world map scene
            if (SceneManager.GetActiveScene().buildIndex != SCENE_WORLDMAP) return;

            try
            {
                _capitalEnabledSubscription = CapitalReflection.SubscribeToCapitalEnabled(OnCapitalScreenShown);
                _capitalClosedSubscription = CapitalReflection.SubscribeToCapitalClosed(OnCapitalScreenClosed);

                if (_capitalEnabledSubscription != null && _capitalClosedSubscription != null)
                {
                    _subscribedToCapital = true;
                    Debug.Log("[ATSAccessibility] Subscribed to capital screen events");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to subscribe to capital events: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose capital screen subscriptions.
        /// </summary>
        private void DisposeCapitalSubscriptions()
        {
            _capitalEnabledSubscription?.Dispose();
            _capitalClosedSubscription?.Dispose();
            _capitalEnabledSubscription = null;
            _capitalClosedSubscription = null;
            _subscribedToCapital = false;

            // Close capital overlay if open
            _capitalOverlay?.Close();
        }

        /// <summary>
        /// Called when the capital screen is shown (OnCapitalEnabled event).
        /// </summary>
        private void OnCapitalScreenShown(object _)
        {
            Debug.Log("[ATSAccessibility] Capital screen shown");
            _capitalOverlay?.Open();
        }

        /// <summary>
        /// Called when the capital screen is closed (OnCapitalClosed event).
        /// </summary>
        private void OnCapitalScreenClosed(object _)
        {
            Debug.Log("[ATSAccessibility] Capital screen closed");
            _capitalOverlay?.Close();

            // Return to world map context
            _keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
        }

        /// <summary>
        /// Called when a popup is shown.
        /// </summary>
        private void OnPopupShown(object popup)
        {
            string popupTypeName = popup?.GetType()?.Name ?? "null";
            Debug.Log($"[ATSAccessibility] Popup shown event received: {popupTypeName}");

            // Check wiki popup FIRST - it has its own navigator
            if (GameReflection.IsWikiPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wiki popup detected, using Encyclopedia navigator");
                _encyclopediaNavigator?.OnWikiPopupShown(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Encyclopedia);
                return;
            }

            // Check recipes popup - it has its own overlay
            if (RecipesReflection.IsRecipesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Recipes popup detected, using Recipes overlay");
                _recipesOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check wildcard popup - it has its own overlay
            if (WildcardReflection.IsWildcardPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wildcard popup detected, using Wildcard overlay");
                _wildcardOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check reputation reward popup - it has its own overlay
            if (ReputationRewardReflection.IsReputationRewardsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Reputation rewards popup detected, using ReputationReward overlay");
                _reputationRewardOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            if (CornerstoneReflection.IsRewardPickPopup(popup))
            {
                _cornerstoneOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }
            if (CornerstoneReflection.IsCornerstonesLimitPickPopup(popup))
            {
                _cornerstoneLimitOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check newcomers popup - it has its own overlay
            if (NewcomersReflection.IsNewcomersPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Newcomers popup detected, using Newcomers overlay");
                _newcomersOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check orders popup - it has its own overlay
            if (OrdersReflection.IsOrdersPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Orders popup detected, using Orders overlay");
                _ordersOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check order pick popup - it has its own overlay
            if (OrdersReflection.IsOrderPickPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Order pick popup detected, using OrderPick overlay");
                _orderPickOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check Ironman upgrade popup - it has its own overlay (must check before regular capital)
            if (IronmanReflection.IsIronmanUpgradePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Ironman upgrade popup detected, using Ironman overlay");
                _ironmanOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check capital upgrade popup - it has its own overlay
            if (CapitalUpgradeReflection.IsCapitalUpgradePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Capital upgrade popup detected, using CapitalUpgrade overlay");
                _capitalUpgradeOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check consumption popup - it has its own overlay
            if (ConsumptionReflection.IsConsumptionPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Consumption popup detected, using Consumption overlay");
                _consumptionOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check goals popup - it has its own overlay
            if (DeedsReflection.IsGoalsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Goals popup detected, using Deeds overlay");
                _deedsOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check rewards pack popup (port expedition rewards)
            if (RewardsPackOverlay.IsRewardsPackPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Rewards pack popup detected, using RewardsPack overlay");
                _rewardsPackOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check assault result popup (after assaulting a trader)
            if (AssaultResultOverlay.IsAssaultResultPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Assault result popup detected, using AssaultResult overlay");
                _assaultResultOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check trader panel - it has its own overlay
            if (TradeReflection.IsTraderPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Trader panel detected, using Trader overlay");
                TradeReflection.SetCurrentPanel(popup);
                _traderOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check home popup (NPC dialogue) - it has its own overlay
            if (NarrationReflection.IsHomePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Home popup detected, using Dialogue overlay");
                _dialogueOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check trade routes popup - it has its own overlay
            if (TradeRoutesReflection.IsTradeRoutesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Trade routes popup detected, using TradeRoutes overlay");
                _tradeRoutesOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check seal panel (Sealed Forest biome) - it has its own overlay
            if (SealReflection.IsSealPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Seal panel detected, using Seal overlay");
                _sealOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check world event popup (world map) - it has its own overlay
            if (WorldEventReflection.IsWorldEventPopup(popup))
            {
                Debug.Log("[ATSAccessibility] World event popup detected, using WorldEvent overlay");
                _worldEventOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check trends popup - it has its own overlay
            if (TrendsReflection.IsTrendsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Trends popup detected, using Trends overlay");
                _trendsOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check cycle end popup (world map) - it has its own overlay
            if (CycleEndOverlay.IsWorldCycleEndPopup(popup))
            {
                Debug.Log("[ATSAccessibility] World cycle end popup detected, using CycleEnd overlay");
                _cycleEndOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check payments popup - it has its own overlay
            if (PaymentsReflection.IsPaymentsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Payments popup detected, using Payments overlay");
                _paymentsOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check meta rewards/level-up popup - it has its own overlay
            // Must be checked BEFORE game result popup since both may be open simultaneously
            if (IsMetaRewardsOrLevelUpPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Meta rewards/level-up popup detected, using MetaRewards overlay");

                // If on world map, cache the tutorial tooltip NOW (before animation finishes)
                // Only track for polling if tutorial tooltip was actually visible
                if (SceneManager.GetActiveScene().buildIndex == SCENE_WORLDMAP)
                {
                    _tutorialWasActiveBeforePopup = TutorialReflection.IsTooltipVisible();
                    if (_tutorialWasActiveBeforePopup)
                    {
                        // Force cache the tooltip while it's still accessible
                        TutorialReflection.GetTutorialTooltip();
                    }
                }

                _metaRewardsOverlay?.OnPopupShown(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check game result popup (victory/defeat) - it has its own overlay
            if (GameResultReflection.IsGameResultPopup(popup))
            {
                // Close any active overlays that might block the game result screen
                _ordersOverlay?.Close();

                _gameResultOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check black market popup - it has its own overlay
            if (BlackMarketReflection.IsBlackMarketPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Black Market popup detected, using BlackMarket overlay");
                _blackMarketOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check altar panel - it has its own overlay
            if (AltarReflection.IsAltarPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Altar panel detected, using Altar overlay");
                _altarOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check perk crafter popup (Cornerstone Forge) - it has its own overlay
            if (PerkCrafterReflection.IsPerkCrafterPopup(popup))
            {
                Debug.Log("[ATSAccessibility] PerkCrafter popup detected, using PerkCrafter overlay");
                _perkCrafterOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check games history popup - it has its own overlay
            if (GamesHistoryReflection.IsGamesHistoryPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Games History popup detected, using GamesHistory overlay");
                _gamesHistoryOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check daily challenge popup - it has its own overlay
            if (DailyExpeditionReflection.IsDailyChallengePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Daily Challenge popup detected, using DailyExpedition overlay");
                _dailyExpeditionOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check profiles popup (save selection) - it has its own overlay
            if (ProfilesReflection.IsProfilesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Profiles popup detected, using Profiles overlay");
                _profilesOverlay?.Open();
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // Check custom games popup (Training Expeditions) - it has its own overlay
            if (CustomGamesReflection.IsCustomGamePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Custom Games popup detected, using CustomGames overlay");
                _customGamesOverlay?.Open(popup);
                _keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
                return;
            }

            // If deeds overlay just claimed a reward, capture the popup as a child
            if (_deedsOverlay != null && _deedsOverlay.ShouldCaptureNextPopup)
            {
                Debug.Log("[ATSAccessibility] Capturing reward popup as deeds child");
                _deedsOverlay.SetChildPopup(popup);
                return;
            }

            // If deeds overlay is active and a different popup opens on top, suspend it
            if (_deedsOverlay != null && _deedsOverlay.IsActive)
            {
                _deedsOverlay.Suspend();
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

            // Check wiki popup first
            if (GameReflection.IsWikiPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wiki popup closed");
                _encyclopediaNavigator?.OnWikiPopupHidden();
                // Fall through to handle context change
            }
            // Check recipes popup
            else if (RecipesReflection.IsRecipesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Recipes popup closed");
                _recipesOverlay?.Close();
                // Fall through to handle context change
            }
            // Check wildcard popup
            else if (WildcardReflection.IsWildcardPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Wildcard popup closed");
                _wildcardOverlay?.Close();
                // Fall through to handle context change
            }
            // Check reputation reward popup
            else if (ReputationRewardReflection.IsReputationRewardsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Reputation rewards popup closed");
                _reputationRewardOverlay?.Close();
                // Fall through to handle context change
            }
            // Check cornerstone pick popup
            else if (CornerstoneReflection.IsRewardPickPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Cornerstone pick popup closed");
                _cornerstoneOverlay?.Close();
                // Fall through to handle context change
            }
            // Check cornerstone limit popup
            else if (CornerstoneReflection.IsCornerstonesLimitPickPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Cornerstone limit popup closed");
                _cornerstoneLimitOverlay?.Close();
                // Refresh main overlay in case a new pick loaded after limit removal
                _cornerstoneOverlay?.RefreshAfterLimit();
                // Fall through to handle context change
            }
            // Check newcomers popup
            else if (NewcomersReflection.IsNewcomersPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Newcomers popup closed");
                _newcomersOverlay?.Close();
                // Fall through to handle context change
            }
            // Check orders popup
            else if (OrdersReflection.IsOrdersPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Orders popup closed");
                _ordersOverlay?.Close();
                // Fall through to handle context change
            }
            // Check order pick popup
            else if (OrdersReflection.IsOrderPickPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Order pick popup closed");
                _orderPickOverlay?.Close();
                // Refresh orders overlay since picked order is now active
                _ordersOverlay?.RefreshAfterPick();
                // Fall through to handle context change
            }
            // Check Ironman upgrade popup
            else if (IronmanReflection.IsIronmanUpgradePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Ironman upgrade popup closed");
                _ironmanOverlay?.Close();
                // Fall through to handle context change
            }
            // Check capital upgrade popup
            else if (CapitalUpgradeReflection.IsCapitalUpgradePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Capital upgrade popup closed");
                _capitalUpgradeOverlay?.Close();
                // Fall through to handle context change
            }
            // Check consumption popup
            else if (ConsumptionReflection.IsConsumptionPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Consumption popup closed");
                _consumptionOverlay?.Close();
                // Fall through to handle context change
            }
            // Check goals popup
            else if (DeedsReflection.IsGoalsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Goals popup closed");
                _deedsOverlay?.Close();
                // Fall through to handle context change
            }
            // Check rewards pack popup
            else if (RewardsPackOverlay.IsRewardsPackPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Rewards pack popup closed");
                _rewardsPackOverlay?.Close();
                // Fall through to handle context change
            }
            // Check assault result popup
            else if (AssaultResultOverlay.IsAssaultResultPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Assault result popup closed");
                _assaultResultOverlay?.Close();
                // Fall through to handle context change
            }
            // Check trader panel
            else if (TradeReflection.IsTraderPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Trader panel closed");
                TradeReflection.ClearCurrentPanel();
                _traderOverlay?.Close();
                // Fall through to handle context change
            }
            // Check home popup (NPC dialogue)
            else if (NarrationReflection.IsHomePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Home popup closed");
                _dialogueOverlay?.Close();
                // Fall through to handle context change
            }
            // Check trade routes popup
            else if (TradeRoutesReflection.IsTradeRoutesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Trade routes popup closed");
                _tradeRoutesOverlay?.Close();
                // Fall through to handle context change
            }
            // Check seal panel (Sealed Forest biome)
            else if (SealReflection.IsSealPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Seal panel closed");
                _sealOverlay?.Close();
                // Fall through to handle context change
            }
            // Check world event popup (world map)
            else if (WorldEventReflection.IsWorldEventPopup(popup))
            {
                Debug.Log("[ATSAccessibility] World event popup closed");
                _worldEventOverlay?.Close();
                // Fall through to handle context change
            }
            // Check trends popup
            else if (TrendsReflection.IsTrendsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Trends popup closed");
                _trendsOverlay?.Close();
                // Fall through to handle context change
            }
            // Check cycle end popup (world map)
            else if (CycleEndOverlay.IsWorldCycleEndPopup(popup))
            {
                Debug.Log("[ATSAccessibility] World cycle end popup closed");
                _cycleEndOverlay?.Close();
                // Fall through to handle context change
            }
            // Check payments popup
            else if (PaymentsReflection.IsPaymentsPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Payments popup closed");
                _paymentsOverlay?.Close();
                // Fall through to handle context change
            }
            // Check game result popup (victory/defeat)
            else if (GameResultReflection.IsGameResultPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Game result popup closed");
                _gameResultOverlay?.Close();
                // Fall through to handle context change
            }
            // Check black market popup
            else if (BlackMarketReflection.IsBlackMarketPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Black Market popup closed");
                _blackMarketOverlay?.Close();
                // Fall through to handle context change
            }
            // Check altar panel
            else if (AltarReflection.IsAltarPanel(popup))
            {
                Debug.Log("[ATSAccessibility] Altar panel closed");
                _altarOverlay?.Close();
                // Fall through to handle context change
            }
            // Check perk crafter popup
            else if (PerkCrafterReflection.IsPerkCrafterPopup(popup))
            {
                Debug.Log("[ATSAccessibility] PerkCrafter popup closed");
                _perkCrafterOverlay?.Close();
                // Fall through to handle context change
            }
            // Check games history popup
            else if (GamesHistoryReflection.IsGamesHistoryPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Games History popup closed");
                _gamesHistoryOverlay?.Close();
                // Fall through to handle context change
            }
            // Check daily challenge popup
            else if (DailyExpeditionReflection.IsDailyChallengePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Daily Challenge popup closed");
                _dailyExpeditionOverlay?.Close();
                // Fall through to handle context change
            }
            // Check profiles popup (save selection)
            else if (ProfilesReflection.IsProfilesPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Profiles popup closed");
                _profilesOverlay?.Close();
                // Fall through to handle context change
            }
            // Check custom games popup (Training Expeditions)
            else if (CustomGamesReflection.IsCustomGamePopup(popup))
            {
                Debug.Log("[ATSAccessibility] Custom Games popup closed");
                _customGamesOverlay?.Close();
                // Fall through to handle context change
            }
            // Check MetaRewardsPopup/MetaLevelUpPopup - close overlay and handle tutorial polling
            else if (IsMetaRewardsOrLevelUpPopup(popup))
            {
                Debug.Log("[ATSAccessibility] Meta rewards/level-up popup closed");
                _metaRewardsOverlay?.OnPopupHidden(popup);

                // If on world map and tutorial was active before popup, set flag to poll for tutorial tooltip
                if (SceneManager.GetActiveScene().buildIndex == SCENE_WORLDMAP && _tutorialWasActiveBeforePopup)
                {
                    _tutorialWasActiveBeforePopup = false;
                    _waitingForTutorialTooltip = true;
                }
                // Fall through to handle context change
            }
            else
            {
                // If deeds overlay has a child popup that was closed, clear it
                if (_deedsOverlay != null && _deedsOverlay.IsActive && _deedsOverlay.HasChildPopup)
                {
                    _deedsOverlay.ClearChildPopup();
                    return;
                }

                // If deeds overlay is suspended (non-claim popup closed), resume it
                if (_deedsOverlay != null && _deedsOverlay.IsSuspended)
                {
                    Debug.Log("[ATSAccessibility] Resuming Deeds overlay after child popup closed");
                    _deedsOverlay.Resume();
                    return;
                }

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
                    // If capital overlay is suspended (sub-panel was open), resume it
                    if (_capitalOverlay != null && _capitalOverlay.IsSuspended)
                    {
                        _capitalOverlay.Resume();
                        Debug.Log("[ATSAccessibility] Popup closed on world map, resuming capital overlay");
                    }
                    else
                    {
                        _keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
                        Debug.Log("[ATSAccessibility] Popup closed on world map, returning to WorldMap context");

                        // If MetaRewardsPopup closed during tutorial, poll for next tooltip
                        if (_waitingForTutorialTooltip)
                        {
                            _waitingForTutorialTooltip = false;
                            StartCoroutine(PollForWorldTutorialTooltip());
                        }
                    }
                }
                else
                {
                    _keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);
                }
            }
        }

        // Track if we should poll for tutorial tooltip after MetaRewardsPopup
        private bool _waitingForTutorialTooltip = false;
        // Track if tutorial was active when MetaRewardsPopup opened (to avoid unnecessary polling)
        private bool _tutorialWasActiveBeforePopup = false;

        /// <summary>
        /// Check if a popup is MetaRewardsPopup or MetaLevelUpPopup.
        /// </summary>
        private static bool IsMetaRewardsOrLevelUpPopup(object popup)
        {
            if (popup == null) return false;
            var go = popup as UnityEngine.GameObject;
            if (go == null)
            {
                var component = popup as UnityEngine.Component;
                go = component?.gameObject;
            }
            if (go == null) return false;
            return go.name.Contains("MetaRewards") || go.name.Contains("MetaLevelUp");
        }

        /// <summary>
        /// Polls for the tutorial tooltip to become visible on the world map.
        /// Called after MetaRewardsPopup closes during a tutorial.
        /// </summary>
        private IEnumerator PollForWorldTutorialTooltip()
        {
            float elapsed = 0f;
            float maxWait = 10f;
            float pollInterval = 0.25f;

            while (elapsed < maxWait)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                // Check if tooltip became visible
                if (TutorialReflection.IsTooltipVisible())
                {
                    string text = TutorialReflection.GetCurrentText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Queue the announcement (don't interrupt rewards)
                        Speech.Say(text, interrupt: false);
                        _tutorialTooltipHandler?.ForceEngage();
                    }
                    yield break;
                }

                // Abort if we left the world map
                if (SceneManager.GetActiveScene().buildIndex != SCENE_WORLDMAP)
                    yield break;
            }
        }
    }
}
