using ATSAccessibility.Panels;
using ATSAccessibility.Handlers;
using ATSAccessibility.Overlays;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ATSAccessibility.Core {
	public class AccessibilityCore: MonoBehaviour {

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

		// Popup routing for all overlay popups
		private PopupRouter _popupRouter;

		// Tutorial tooltip handler for tutorial text navigation
		private TutorialTooltipHandler _tutorialTooltipHandler;

		// Help overlay for F12 context-sensitive help
		private HelpOverlay _helpOverlay;

		// Capital screen overlay for Smoldering City (referenced by capital event callbacks)
		private CapitalOverlay _capitalOverlay;
		private IDisposable _capitalEnabledSubscription;
		private IDisposable _capitalClosedSubscription;
		private bool _subscribedToCapital = false;

		// World tutorials overlay for tutorial selection on world map (not popup-routed)
		private WorldTutorialsOverlay _worldTutorialsOverlay;

		// Update checker state
		private bool _updateCheckHandled = false;
		private float _updateCheckDelay = 0f;
		private const float UPDATE_CHECK_ANNOUNCE_DELAY = 3f;

		// Deferred menu rebuild (wait for user input after popup closes)
		private bool _menuPendingSetup = false;

		// Cached main menu canvas (cleared on scene unload)
		private GameObject _cachedMainMenuCanvas = null;

		private void Start() {
			Debug.Log("[ATSAccessibility] AccessibilityCore.Start()");

			// Subscribe to scene events
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;

			// Initialize speech (Tolk)
			_speechInitialized = Speech.Initialize();

			// Load localized strings (fallback/English). The game's language isn't known
			// this early; Strings.ApplyGameLanguage() switches to the user's language
			// later once TextsService is available (see Update()).
			Strings.Initialize();

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
			_announcementHistoryPanel = new AnnouncementHistoryPanel(_mapNavigator);

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

			// Initialize overlays as local variables (popup-routed via PopupRouter)
			var recipesOverlay = new RecipesOverlay();
			var wildcardOverlay = new WildcardOverlay();
			wildcardOverlay.SetEncyclopediaNavigator(_encyclopediaNavigator);
			var reputationRewardOverlay = new ReputationRewardOverlay();
			reputationRewardOverlay.SetEncyclopediaNavigator(_encyclopediaNavigator);
			var cornerstoneOverlay = new CornerstoneOverlay();
			var cornerstoneLimitOverlay = new CornerstoneLimitOverlay();
			var newcomersOverlay = new NewcomersOverlay();
			var ordersOverlay = new OrdersOverlay();
			var orderPickOverlay = new OrderPickOverlay();
			var consumptionOverlay = new ConsumptionOverlay();
			var deedsOverlay = new DeedsOverlay();
			var rewardsPackOverlay = new RewardsPackOverlay();
			var resupplyOverlay = new ResupplyOverlay();
			var traderOverlay = new TraderOverlay();
			var assaultResultOverlay = new AssaultResultOverlay();
			var dialogueOverlay = new DialogueOverlay();
			var sealOverlay = new SealOverlay();
			var worldEventOverlay = new WorldEventOverlay();
			var trendsOverlay = new TrendsOverlay();
			var tradeRoutesOverlay = new TradeRoutesOverlay();
			var cycleEndOverlay = new CycleEndOverlay();
			var paymentsOverlay = new PaymentsOverlay();
			var metaRewardsOverlay = new MetaRewardsOverlay(this);
			var gameResultOverlay = new GameResultOverlay();
			var blackMarketOverlay = new BlackMarketOverlay();
			var altarOverlay = new AltarOverlay();
			var perkCrafterOverlay = new PerkCrafterOverlay();
			var gamesHistoryOverlay = new GamesHistoryOverlay();
			var profilesOverlay = new ProfilesOverlay();
			var dailyExpeditionOverlay = new DailyExpeditionOverlay();
			var customGamesOverlay = new CustomGamesOverlay();
			_capitalOverlay = new CapitalOverlay();
			var capitalUpgradeOverlay = new CapitalUpgradeOverlay();
			var ironmanOverlay = new IronmanOverlay();

			// Wire up event announcer to refresh orders overlay when new orders arrive
			_eventAnnouncer.OnNewOrderAvailable = () => ordersOverlay.RefreshOnNewOrder();

			// Initialize tutorial tooltip handler (needs UINavigator to check for blocking popups)
			_tutorialTooltipHandler = new TutorialTooltipHandler(_uiNavigator);

			// Initialize world tutorials overlay for world map tutorial selection
			_worldTutorialsOverlay = new WorldTutorialsOverlay();

			// Initialize help overlay for F12 context-sensitive help
			_helpOverlay = new HelpOverlay();
			_keyboardManager.SetHelpOverlay(_helpOverlay);

			// Initialize popup router (deeds overlay needed for fallback logic)
			_popupRouter = new PopupRouter(deedsOverlay, _uiNavigator, _keyboardManager);

			// Register popup routing (order preserved from original if/else chain)
			_popupRouter.Register(GameReflection.IsWikiPopup,
				p => _encyclopediaNavigator.OnWikiPopupShown(p),
				_ => _encyclopediaNavigator.OnWikiPopupHidden(),
				() => _encyclopediaNavigator.OnWikiPopupHidden(),
				KeyboardManager.NavigationContext.Encyclopedia);
			_popupRouter.Register(RecipesReflection.IsRecipesPopup, _ => recipesOverlay.Open(), recipesOverlay);
			_popupRouter.Register(WildcardReflection.IsWildcardPopup, p => wildcardOverlay.Open(p), wildcardOverlay);
			_popupRouter.Register(ReputationRewardReflection.IsReputationRewardsPopup, p => reputationRewardOverlay.Open(p), reputationRewardOverlay);
			_popupRouter.Register(CornerstoneReflection.IsRewardPickPopup, p => cornerstoneOverlay.Open(p), cornerstoneOverlay);
			_popupRouter.Register(CornerstoneReflection.IsCornerstonesLimitPickPopup,
				p => cornerstoneLimitOverlay.Open(p),
				_ => { cornerstoneLimitOverlay.Close(); cornerstoneOverlay.RefreshAfterLimit(); },
				() => cornerstoneLimitOverlay.Close());
			_popupRouter.Register(NewcomersReflection.IsNewcomersPopup, p => newcomersOverlay.Open(p), newcomersOverlay);
			_popupRouter.Register(OrdersReflection.IsOrdersPopup, p => ordersOverlay.Open(p), ordersOverlay);
			_popupRouter.Register(OrdersReflection.IsOrderPickPopup,
				p => orderPickOverlay.Open(p),
				_ => { orderPickOverlay.Close(); ordersOverlay.RefreshAfterPick(); },
				() => orderPickOverlay.Close());
			_popupRouter.Register(IronmanReflection.IsIronmanUpgradePopup, _ => ironmanOverlay.Open(), ironmanOverlay);
			_popupRouter.Register(CapitalUpgradeReflection.IsCapitalUpgradePopup, _ => capitalUpgradeOverlay.Open(), capitalUpgradeOverlay);
			_popupRouter.Register(ConsumptionReflection.IsConsumptionPopup, _ => consumptionOverlay.Open(), consumptionOverlay);
			_popupRouter.Register(DeedsReflection.IsGoalsPopup, _ => deedsOverlay.Open(), deedsOverlay);
			_popupRouter.Register(RewardsPackOverlay.IsRewardsPackPopup, p => rewardsPackOverlay.Open(p), rewardsPackOverlay);
			_popupRouter.Register(ResupplyOverlay.IsCycleEffectsPickPopup, p => resupplyOverlay.Open(p), resupplyOverlay);
			_popupRouter.Register(AssaultResultOverlay.IsAssaultResultPopup, p => assaultResultOverlay.Open(p), assaultResultOverlay);
			_popupRouter.Register(TradeReflection.IsTraderPanel,
				p => { TradeReflection.SetCurrentPanel(p); traderOverlay.Open(); },
				_ => { TradeReflection.ClearCurrentPanel(); traderOverlay.Close(); },
				() => traderOverlay.Close());
			_popupRouter.Register(NarrationReflection.IsHomePopup, p => dialogueOverlay.Open(p), dialogueOverlay);
			_popupRouter.Register(TradeRoutesReflection.IsTradeRoutesPopup, p => tradeRoutesOverlay.Open(p), tradeRoutesOverlay);
			_popupRouter.Register(SealReflection.IsSealPanel, _ => sealOverlay.Open(), sealOverlay);
			_popupRouter.Register(WorldEventReflection.IsWorldEventPopup, p => worldEventOverlay.Open(p), worldEventOverlay);
			_popupRouter.Register(TrendsReflection.IsTrendsPopup, p => trendsOverlay.Open(p), trendsOverlay);
			_popupRouter.Register(CycleEndOverlay.IsWorldCycleEndPopup, p => cycleEndOverlay.Open(p), cycleEndOverlay);
			_popupRouter.Register(PaymentsReflection.IsPaymentsPopup, p => paymentsOverlay.Open(p), paymentsOverlay);
			_popupRouter.Register(IsMetaRewardsOrLevelUpPopup,
				p => {
					if (SceneManager.GetActiveScene().buildIndex == SceneConstants.SCENE_WORLDMAP) {
						_tutorialWasActiveBeforePopup = TutorialReflection.IsTooltipVisible();
						if (_tutorialWasActiveBeforePopup)
							TutorialReflection.GetTutorialTooltip();
					}
					metaRewardsOverlay.OnPopupShown(p);
				},
				p => {
					metaRewardsOverlay.OnPopupHidden(p);
					if (SceneManager.GetActiveScene().buildIndex == SceneConstants.SCENE_WORLDMAP && _tutorialWasActiveBeforePopup) {
						_tutorialWasActiveBeforePopup = false;
						_waitingForTutorialTooltip = true;
					}
				},
				() => metaRewardsOverlay.Reset());
			_popupRouter.Register(GameResultReflection.IsGameResultPopup,
				p => { ordersOverlay.Close(); gameResultOverlay.Open(p); },
				gameResultOverlay);
			_popupRouter.Register(BlackMarketReflection.IsBlackMarketPopup, p => blackMarketOverlay.Open(p), blackMarketOverlay);
			_popupRouter.Register(AltarReflection.IsAltarPanel, _ => altarOverlay.Open(), altarOverlay);
			_popupRouter.Register(PerkCrafterReflection.IsPerkCrafterPopup, _ => perkCrafterOverlay.Open(), perkCrafterOverlay);
			_popupRouter.Register(GamesHistoryReflection.IsGamesHistoryPopup, _ => gamesHistoryOverlay.Open(), gamesHistoryOverlay);
			_popupRouter.Register(DailyExpeditionReflection.IsDailyChallengePopup, p => dailyExpeditionOverlay.Open(p), dailyExpeditionOverlay);
			_popupRouter.Register(ProfilesReflection.IsProfilesPopup, _ => profilesOverlay.Open(), profilesOverlay);
			_popupRouter.Register(CustomGamesReflection.IsCustomGamePopup, p => customGamesOverlay.Open(p), customGamesOverlay);

			// Create context handlers for settlement and world map
			var settlementHandler = new SettlementKeyHandler(
				_mapNavigator, _mapScanner, _infoPanelMenu, _menuHub, _rewardsPanel, _buildingMenuPanel, _moveModeController, _announcementHistoryPanel, _confirmationDialog, harvestMarkHandler);
			var worldMapHandler = new WorldMapKeyHandler(_worldMapNavigator, _worldMapScanner);
			worldMapHandler.SetTutorialsOverlay(_worldTutorialsOverlay);

			// Register key handlers in priority order (highest priority first)
			_keyboardManager.RegisterHandler(_helpOverlay);  // Help overlay (F12, when open captures all keys)
			_keyboardManager.RegisterHandler(_tutorialTooltipHandler);  // Tutorial tooltip (blocks input during tutorial)
			_keyboardManager.RegisterHandler(_confirmationDialog);  // Confirmation dialog (blocks all input when active)
			_keyboardManager.RegisterHandler(metaRewardsOverlay);  // Meta rewards/level-up popup (above game result so player can close it first)
			_keyboardManager.RegisterHandler(gameResultOverlay);  // Game result (victory/defeat) - high priority terminal state
			_keyboardManager.RegisterHandler(new SettlementInfoHandler()); // Alt+S/V/O settlement info (above all menus/overlays)
			_keyboardManager.RegisterHandler(new WorldMapInfoHandler()); // Alt+L/R/S/T world map info (above all menus/overlays)
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
			_keyboardManager.RegisterHandler(recipesOverlay);      // Recipes popup overlay
			_keyboardManager.RegisterHandler(wildcardOverlay);    // Wildcard popup overlay
			_keyboardManager.RegisterHandler(cornerstoneLimitOverlay);   // Cornerstone limit popup overlay
			_keyboardManager.RegisterHandler(cornerstoneOverlay);       // Cornerstone pick popup overlay
			_keyboardManager.RegisterHandler(newcomersOverlay);         // Newcomers group selection overlay
			_keyboardManager.RegisterHandler(orderPickOverlay);         // Order pick popup overlay (higher priority - child popup)
			_keyboardManager.RegisterHandler(ordersOverlay);            // Orders popup overlay
			_keyboardManager.RegisterHandler(consumptionOverlay);       // Consumption control popup overlay
			_keyboardManager.RegisterHandler(deedsOverlay);             // Deeds (goals) popup overlay
			_keyboardManager.RegisterHandler(reputationRewardOverlay);  // Reputation reward popup overlay
			_keyboardManager.RegisterHandler(rewardsPackOverlay);  // Rewards pack popup overlay (port rewards)
			_keyboardManager.RegisterHandler(resupplyOverlay);    // Royal Resupply popup overlay (world map)
			_keyboardManager.RegisterHandler(assaultResultOverlay); // Assault result popup overlay (before trader so it gets priority)
			_keyboardManager.RegisterHandler(traderOverlay);        // Trader panel overlay
			_keyboardManager.RegisterHandler(dialogueOverlay);      // NPC dialogue overlay
			_keyboardManager.RegisterHandler(sealOverlay);         // Seal building overlay (Sealed Forest)
			_keyboardManager.RegisterHandler(worldEventOverlay);  // World event popup overlay (world map)
			_keyboardManager.RegisterHandler(trendsOverlay);     // Trends popup overlay (storage operations)
			_keyboardManager.RegisterHandler(tradeRoutesOverlay); // Trade routes popup overlay
			_keyboardManager.RegisterHandler(cycleEndOverlay);   // Cycle end popup overlay (world map)
			_keyboardManager.RegisterHandler(paymentsOverlay);   // Payments popup overlay
			_keyboardManager.RegisterHandler(blackMarketOverlay); // Black Market popup overlay
			_keyboardManager.RegisterHandler(altarOverlay);        // Altar (Forsaken Altar) popup overlay
			_keyboardManager.RegisterHandler(perkCrafterOverlay);  // Perk Crafter (Cornerstone Forge) popup overlay
			_keyboardManager.RegisterHandler(gamesHistoryOverlay); // Games History popup overlay
			_keyboardManager.RegisterHandler(dailyExpeditionOverlay); // Daily Expedition popup overlay
			_keyboardManager.RegisterHandler(customGamesOverlay);   // Custom Games (Training Expeditions) popup overlay
			_keyboardManager.RegisterHandler(profilesOverlay);     // Profiles (save selection) popup overlay
			_keyboardManager.RegisterHandler(_uiNavigator);         // Generic popup/menu navigation
			_keyboardManager.RegisterHandler(_embarkPanel);         // Pre-expedition setup
			_keyboardManager.RegisterHandler(ironmanOverlay);       // Ironman upgrade popup overlay
			_keyboardManager.RegisterHandler(capitalUpgradeOverlay); // Capital upgrade popup overlay
			_keyboardManager.RegisterHandler(_capitalOverlay);     // Capital screen overlay
			_keyboardManager.RegisterHandler(settlementHandler);    // Settlement map navigation (fallback)
			_keyboardManager.RegisterHandler(_worldTutorialsOverlay); // World tutorials HUD (world map)
			_keyboardManager.RegisterHandler(worldMapHandler);      // World map navigation (fallback)

			// Check if we're already on a scene (mod loaded mid-game)
			CheckCurrentScene();

			// Validate all reflection caches and log results
			ValidateReflectionCaches();
		}

		private void ValidateReflectionCaches() {
			try {
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
				totalMissing += AutomatonReflection.LogCacheStatus();

				if (totalMissing == 0)
					Debug.Log("[ATSAccessibility] Reflection validation: All fields cached successfully");
				else
					Debug.Log($"[ATSAccessibility] Reflection validation: {totalMissing} total fields MISSING across all classes");
			} catch (Exception ex) {
				Debug.Log($"[ATSAccessibility] Reflection validation failed: {ex.Message}");
			}
		}

		private void OnDestroy() {
			Debug.Log("[ATSAccessibility] AccessibilityCore.OnDestroy()");

			SceneManager.sceneLoaded -= OnSceneLoaded;
			SceneManager.sceneUnloaded -= OnSceneUnloaded;

			// Dispose popup subscriptions
			DisposePopupSubscriptions();

			// Dispose building panel handler
			_buildingPanelHandler?.Dispose();

			Speech.Shutdown();

			MenuBase.ClearStaticState();
		}

		private void Update() {
			// Process pending event announcements (batches messages to prevent interruption)
			_eventAnnouncer?.ProcessMessageQueue();

			// Check for tutorial tooltip text changes (auto-announce)
			_tutorialTooltipHandler?.CheckForTextChanges();

			// Poll for update check result (delayed so menu setup speech finishes first)
			if (!_updateCheckHandled) {
				_updateCheckDelay += Time.unscaledDeltaTime;
				if (_updateCheckDelay >= UPDATE_CHECK_ANNOUNCE_DELAY && UpdateChecker.TryAnnounceResult())
					_updateCheckHandled = true;
			}

			// Polling for game state changes (settlement entry)
			// Use unscaledDeltaTime so it works even when game is paused
			_pollTimer += Time.unscaledDeltaTime;
			if (_pollTimer >= POLL_INTERVAL) {
				_pollTimer = 0f;
				PollGameState();

				// Try to subscribe to popups if not already subscribed
				if (!_subscribedToPopups) {
					TrySubscribeToPopups();
				}

				// Try to subscribe to embark events if not already subscribed
				if (!_subscribedToEmbark) {
					TrySubscribeToEmbark();
				}

				// Try to subscribe to capital screen events if not already subscribed
				if (!_subscribedToCapital) {
					TrySubscribeToCapital();
				}

				// Try to subscribe to building panel events
				_buildingPanelHandler?.TrySubscribe();

				// Try to subscribe to game events for announcements
				_eventAnnouncer?.TrySubscribe();
			}
		}

		private void OnGUI() {
			// Process input in OnGUI - this captures input even when UI has focus
			Event e = Event.current;
			if (e == null || !e.isKey || e.type != EventType.KeyDown) return;

			// Accept events that carry either a KeyCode or a typed character. IME and
			// non-Latin input (Cyrillic, CJK) can arrive as character-only events with
			// KeyCode.None; those are needed for cross-language type-ahead search.
			bool hasKey = e.keyCode != KeyCode.None;
			bool hasChar = e.character != '\0';
			if (!hasKey && !hasChar) return;

			// Deferred menu setup - rebuild on first key press after popup closes
			if (_menuPendingSetup) {
				Debug.Log("[ATSAccessibility] Rebuilding menu navigation on user input");
				SetupMainMenuNavigation();
				_menuPendingSetup = false;
			}

			var modifiers = new KeyboardManager.KeyModifiers(e.control, e.alt, e.shift, e.character);
			_keyboardManager?.ProcessKeyEvent(e.keyCode, modifiers);
		}

		private void CheckCurrentScene() {
			Scene activeScene = SceneManager.GetActiveScene();
			Debug.Log($"[ATSAccessibility] Current scene: {activeScene.name} (index: {activeScene.buildIndex})");

			Strings.ApplyGameLanguage();
			ProcessSceneLoad(activeScene);
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
			Debug.Log($"[ATSAccessibility] Scene loaded: {scene.name} (index: {scene.buildIndex})");
			// Re-check game language — it may have been set by the player between scenes.
			Strings.ApplyGameLanguage();
			ProcessSceneLoad(scene);
		}

		private void OnSceneUnloaded(Scene scene) {
			Debug.Log($"[ATSAccessibility] Scene unloaded: {scene.name} (index: {scene.buildIndex})");

			// Cancel any pending Invoke calls to prevent callbacks on destroyed objects
			CancelInvoke(nameof(AnnounceMainMenu));
			CancelInvoke(nameof(SetupWorldMapNavigation));
			CancelInvoke(nameof(SetupMainMenuNavigation));

			// Clear state when leaving scenes
			if (scene.buildIndex == SceneConstants.SCENE_GAME) {
				_announcedGameStart = false;
				_wasGameActive = false;
				_buildModeController?.Reset();
				_moveModeController?.Reset();
				_tutorialTooltipHandler?.Reset();
				_mapNavigator?.ClearCursor();  // Clear so it reinitializes on next game
				WorkerInfoHelper.Reset();
				StatsReader.ResetSpeciesCycling();
				AnnouncementHistoryPanel.ClearHistory();
			} else if (scene.buildIndex == SceneConstants.SCENE_MENU) {
				_announcedMainMenu = false;
				_cachedMainMenuCanvas = null;
			} else if (scene.buildIndex == SceneConstants.SCENE_WORLDMAP) {
				_announcedWorldMap = false;
				_worldMapNavigator?.Reset();
			}

			// Close all overlays to prevent stale state after scene teardown
			CloseAllOverlays();

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

			// Clear static state that could become stale across scenes
			TradeReflection.ClearCurrentPanel();
			ConstructionReflection.ClearBuildingCreatorInstance();
			CameraControllerUpdateMovementPatch.ClearTarget();
			ReputationRewardOverlay.ResetSuppression();
			TutorialReflection.ClearCachedTooltip();

			// Reset UI navigator state
			_uiNavigator?.Reset();
			_keyboardManager?.SetContext(KeyboardManager.NavigationContext.None);
		}

		private void ProcessSceneLoad(Scene scene) {
			if (scene.buildIndex == SceneConstants.SCENE_MENU && !_announcedMainMenu) {
				// Delay announcement to ensure scene is fully loaded
				Invoke(nameof(AnnounceMainMenu), ANNOUNCEMENT_DELAY);
			} else if (scene.buildIndex == SceneConstants.SCENE_GAME) {
				// For game scene, we wait for GameController.IsGameActive
				// This is handled by polling since the controller initializes async
				_announcedGameStart = false;
			} else if (scene.buildIndex == SceneConstants.SCENE_WORLDMAP) {
				// Delay to allow WorldController to initialize
				_announcedWorldMap = false;
				Invoke(nameof(SetupWorldMapNavigation), ANNOUNCEMENT_DELAY);
			}
		}

		private void AnnounceMainMenu() {
			if (_announcedMainMenu) return;

			if (_speechInitialized && Speech.IsAvailable) {
				// Services are reliably up by now; retry language selection in case
				// CheckCurrentScene ran too early to read TextsService.CurrentLocaCode.
				Strings.ApplyGameLanguage();
				Speech.Say(Strings.Get("core.main_menu"));
				_announcedMainMenu = true;
				Debug.Log("[ATSAccessibility] Announced: Main menu");

				// Dev: dump the game's own loca tables if the config flag is set.
				// One-shot per launch; translator is expected to flip the flag back off.
				if (Plugin.DumpGameLocalization.Value) {
					LocaDumper.DumpAll();
				}

				// Check for mod updates
				if (Plugin.CheckForUpdates.Value) {
					UpdateChecker.Check(Plugin.ModVersion);
					_updateCheckHandled = false;
					_updateCheckDelay = 0f;
				}

				// Set up main menu navigation after a short delay (let UI initialize)
				Invoke(nameof(SetupMainMenuNavigation), ANNOUNCEMENT_DELAY);
			} else {
				Debug.LogWarning("[ATSAccessibility] Speech not available for main menu announcement");
			}
		}

		private void SetupMainMenuNavigation() {
			// Use cached canvas if available and still valid
			if (_cachedMainMenuCanvas != null && _cachedMainMenuCanvas.activeInHierarchy) {
				_uiNavigator?.SetupMenuNavigation(_cachedMainMenuCanvas, "Main Menu");
				_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
				return;
			}

			// Find the main menu Canvas (expensive FindObjectsOfType call - cache the result)
			var canvases = FindObjectsOfType<Canvas>();

			GameObject mainMenuRoot = null;

			foreach (var canvas in canvases) {
				// Skip inactive canvases
				if (!canvas.gameObject.activeInHierarchy) continue;

				string name = canvas.gameObject.name.ToLower();

				// Look for main menu indicators
				if (name.Contains("mainmenu") || name.Contains("main menu") || name.Contains("menu")) {
					// Check if it has buttons
					var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
					if (buttons.Length > 0) {
						mainMenuRoot = canvas.gameObject;
						break;
					}
				}
			}

			// Fallback: if no "menu" canvas found, look for any canvas with multiple buttons
			if (mainMenuRoot == null) {
				foreach (var canvas in canvases) {
					if (!canvas.gameObject.activeInHierarchy) continue;

					var buttons = canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
					if (buttons.Length >= 3) // Main menu typically has several buttons
					{
						mainMenuRoot = canvas.gameObject;
						break;
					}
				}
			}

			if (mainMenuRoot != null) {
				_cachedMainMenuCanvas = mainMenuRoot;
				_uiNavigator?.SetupMenuNavigation(mainMenuRoot, "Main Menu");
				_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup); // Reuse Popup context for menu
			} else {
				Debug.LogWarning("[ATSAccessibility] Could not find main menu canvas");
			}
		}

		private void SetupWorldMapNavigation() {
			if (_announcedWorldMap) return;

			if (WorldMapReflection.IsWorldMapActive()) {
				_worldMapNavigator?.Reset();
				_keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);

				if (_speechInitialized && Speech.IsAvailable) {
					Speech.Say(Strings.Get("core.world_map"), interrupt: false);  // Queue to avoid interrupting other speech
					_announcedWorldMap = true;
					Debug.Log("[ATSAccessibility] Announced: World map");
				}
			} else {
				// WorldController not ready yet, retry
				Debug.Log("[ATSAccessibility] WorldController not ready, retrying...");
				Invoke(nameof(SetupWorldMapNavigation), ANNOUNCEMENT_DELAY);
			}
		}

		private void PollGameState() {
			// Check if game is active (we're in settlement with GameController initialized)
			bool isGameActive = GameReflection.GetIsGameActive();

			if (isGameActive && !_wasGameActive) {
				// Just entered game
				_wasGameActive = true;

				if (!_announcedGameStart && _speechInitialized && Speech.IsAvailable) {
					Speech.Say(Strings.Get("core.game_started"));
					_announcedGameStart = true;
					Debug.Log("[ATSAccessibility] Announced: Game started");
				}

				// Set map navigation context (if no popup is open)
				// Note: Don't call ResetCursor here - game services aren't fully loaded yet.
				// Let lazy initialization in MoveCursor() handle it on first arrow key press.
				if (_uiNavigator == null || !_uiNavigator.HasActivePopup) {
					_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Map);
					Debug.Log("[ATSAccessibility] Set context to Map navigation");
				}
			} else if (!isGameActive && _wasGameActive) {
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
		private void TrySubscribeToPopups() {
			if (_subscribedToPopups) return;

			var popupsService = GameReflection.GetPopupsService();
			if (popupsService == null) return;

			try {
				var popupsServiceType = popupsService.GetType();

				// Get AnyPopupShown observable
				var shownProperty = popupsServiceType.GetProperty("AnyPopupShown");
				var shownObservable = shownProperty?.GetValue(popupsService);

				// Get AnyPopupHidden observable
				var hiddenProperty = popupsServiceType.GetProperty("AnyPopupHidden");
				var hiddenObservable = hiddenProperty?.GetValue(popupsService);

				if (shownObservable != null && hiddenObservable != null) {
					// Subscribe to observables using shared utility
					_popupShownSubscription = GameReflection.SubscribeToObservable(shownObservable, OnPopupShown);
					_popupHiddenSubscription = GameReflection.SubscribeToObservable(hiddenObservable, OnPopupHidden);

					_subscribedToPopups = true;
					Debug.Log("[ATSAccessibility] Subscribed to popup events");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to subscribe to popups: {ex.Message}\n{ex.StackTrace}");
			}
		}

		/// <summary>
		/// Dispose popup subscriptions.
		/// </summary>
		private void DisposePopupSubscriptions() {
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
		private void TrySubscribeToEmbark() {
			if (_subscribedToEmbark) return;

			// Only subscribe when on world map scene
			if (SceneManager.GetActiveScene().buildIndex != SceneConstants.SCENE_WORLDMAP) return;

			try {
				// Subscribe to OnFieldPreviewShown (embark screen opened)
				_embarkShownSubscription = EmbarkReflection.SubscribeToFieldPreviewShown(OnEmbarkScreenShown);

				// Subscribe to OnFieldPreviewClosed (embark screen closed)
				_embarkClosedSubscription = EmbarkReflection.SubscribeToFieldPreviewClosed(OnEmbarkScreenClosed);

				if (_embarkShownSubscription != null && _embarkClosedSubscription != null) {
					_subscribedToEmbark = true;
					Debug.Log("[ATSAccessibility] Subscribed to embark screen events");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to subscribe to embark events: {ex.Message}");
			}
		}

		/// <summary>
		/// Dispose embark subscriptions.
		/// </summary>
		private void DisposeEmbarkSubscriptions() {
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
		private void OnEmbarkScreenShown(object worldField) {
			Debug.Log("[ATSAccessibility] Embark screen shown");
			_embarkPanel?.Open(worldField);
			_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Embark);
		}

		/// <summary>
		/// Called when the embark screen is closed (OnFieldPreviewClosed event).
		/// </summary>
		private void OnEmbarkScreenClosed(object worldField) {
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
		private void TrySubscribeToCapital() {
			if (_subscribedToCapital) return;

			// Only subscribe when on world map scene
			if (SceneManager.GetActiveScene().buildIndex != SceneConstants.SCENE_WORLDMAP) return;

			try {
				_capitalEnabledSubscription = CapitalReflection.SubscribeToCapitalEnabled(OnCapitalScreenShown);
				_capitalClosedSubscription = CapitalReflection.SubscribeToCapitalClosed(OnCapitalScreenClosed);

				if (_capitalEnabledSubscription != null && _capitalClosedSubscription != null) {
					_subscribedToCapital = true;
					Debug.Log("[ATSAccessibility] Subscribed to capital screen events");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to subscribe to capital events: {ex.Message}");
			}
		}

		/// <summary>
		/// Dispose capital screen subscriptions.
		/// </summary>
		private void DisposeCapitalSubscriptions() {
			_capitalEnabledSubscription?.Dispose();
			_capitalClosedSubscription?.Dispose();
			_capitalEnabledSubscription = null;
			_capitalClosedSubscription = null;
			_subscribedToCapital = false;

			// Close capital overlay if open
			_capitalOverlay?.Close();
		}

		/// <summary>
		/// Close all overlays to prevent stale state after scene teardown.
		/// Each overlay's Close() guards with if (!_isOpen) return, so this is safe to call at any time.
		/// </summary>
		private void CloseAllOverlays() {
			_popupRouter?.CloseAll();
			_capitalOverlay?.Close();
			_worldTutorialsOverlay?.Close();
		}

		/// <summary>
		/// Called when the capital screen is shown (OnCapitalEnabled event).
		/// </summary>
		private void OnCapitalScreenShown(object _) {
			Debug.Log("[ATSAccessibility] Capital screen shown");
			_capitalOverlay?.Open();
		}

		/// <summary>
		/// Called when the capital screen is closed (OnCapitalClosed event).
		/// </summary>
		private void OnCapitalScreenClosed(object _) {
			Debug.Log("[ATSAccessibility] Capital screen closed");
			_capitalOverlay?.Close();

			// Return to world map context
			_keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
		}

		/// <summary>
		/// Called when a popup is shown.
		/// </summary>
		private void OnPopupShown(object popup) {
			string popupTypeName = popup?.GetType()?.Name ?? "null";
			Debug.Log($"[ATSAccessibility] Popup shown event received: {popupTypeName}");
			_popupRouter.HandlePopupShown(popup);
		}

		/// <summary>
		/// Called when a popup is hidden.
		/// </summary>
		private void OnPopupHidden(object popup) {
			bool shouldRestoreContext = _popupRouter.HandlePopupHidden(popup);
			if (!shouldRestoreContext) return;

			// Only handle context change if no more popups active
			if (_uiNavigator != null && !_uiNavigator.HasActivePopup) {
				// If on menu scene, defer menu setup until user presses a key
				// This ensures popup close animation is complete and elements are inactive
				if (SceneManager.GetActiveScene().buildIndex == SceneConstants.SCENE_MENU) {
					Debug.Log("[ATSAccessibility] Popup closed on menu scene, deferring menu setup to next input");
					_menuPendingSetup = true;
					// Keep context as Popup so navigation keys work
				} else if (GameReflection.GetIsGameActive()) {
					// In settlement - return to map navigation
					_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Map);
					Debug.Log("[ATSAccessibility] Popup closed in settlement, returning to Map context");
				} else if (SceneManager.GetActiveScene().buildIndex == SceneConstants.SCENE_WORLDMAP) {
					// If capital overlay is suspended (sub-panel was open), resume it
					if (_capitalOverlay != null && _capitalOverlay.IsSuspended) {
						_capitalOverlay.Resume();
						Debug.Log("[ATSAccessibility] Popup closed on world map, resuming capital overlay");
					} else {
						_keyboardManager?.SetContext(KeyboardManager.NavigationContext.WorldMap);
						Debug.Log("[ATSAccessibility] Popup closed on world map, returning to WorldMap context");

						// If MetaRewardsPopup closed during tutorial, poll for next tooltip
						if (_waitingForTutorialTooltip) {
							_waitingForTutorialTooltip = false;
							StartCoroutine(PollForWorldTutorialTooltip());
						}
					}
				} else {
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
		private static bool IsMetaRewardsOrLevelUpPopup(object popup) {
			if (popup == null) return false;
			var go = popup as UnityEngine.GameObject;
			if (go == null) {
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
		private IEnumerator PollForWorldTutorialTooltip() {
			float elapsed = 0f;
			float maxWait = 10f;
			float pollInterval = 0.25f;

			while (elapsed < maxWait) {
				yield return new WaitForSeconds(pollInterval);
				elapsed += pollInterval;

				// Check if tooltip became visible
				if (TutorialReflection.IsTooltipVisible()) {
					string text = TutorialReflection.GetCurrentText();
					if (!string.IsNullOrEmpty(text)) {
						// Queue the announcement (don't interrupt rewards)
						Speech.Say(text, interrupt: false);
						_tutorialTooltipHandler?.ForceEngage();
					}
					yield break;
				}

				// Abort if we left the world map
				if (SceneManager.GetActiveScene().buildIndex != SceneConstants.SCENE_WORLDMAP)
					yield break;
			}
		}
	}
}
