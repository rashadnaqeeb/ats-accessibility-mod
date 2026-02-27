# EventAnnouncer.cs
Subscribes to game events and announces them via speech.
All subscriptions are managed to be disposed on scene changes.

## class EventAnnouncer (line 16)

### Fields
- private List<IDisposable> _subscriptions (line 17)
- private bool _subscribed (line 18)
- private float _gracePeriodEndTime (line 21)
- private const float INITIALIZATION_GRACE_PERIOD = 2f (line 22)
- private int _lastAnnouncedHostilityLevel (line 25)
- private HashSet<string> _announcedAlerts (line 26)
- private Queue<string> _announcedAlertsOrder (line 27)
- private HashSet<string> _announcedNews (line 28)
- private Queue<string> _announcedNewsOrder (line 29)
- private static readonly Regex RichTextTagsRegex (line 32)
- private static readonly Regex AlertPrefixRegex (line 36)
- private List<(string message, float time, Vector2Int? location)> _pendingMessages (line 40)
- private float _batchStartTime (line 41)
- private const float BATCH_WINDOW = 0.15f (line 43)
- private IDisposable _grassLocationSub (line 1030)
- private IDisposable _springsLocationSub (line 1031)
- private IDisposable _relicLocationSub (line 1032)
- private IDisposable _relicHighlightSub (line 1033)

### Properties
- public Action OnNewOrderAvailable { get; set; } (line 45)
  Callback invoked when a new order is available; used by OrdersOverlay to refresh.

### Methods
- public void TrySubscribe() (line 51)
  Called periodically until successful. Subscribes to all game event observables when game is active.
- public void Dispose() (line 91)
  Disposes all subscriptions, clears deduplication sets, resets reflection cache, clears static instance reference.
- private bool IsInGracePeriod() (line 131)
- private static Vector2Int? GetBuildingLocation(object building) (line 136)
- private static Vector2Int? GetGladeLocation(object gladeState) (line 142)
- private static Vector2Int? GetLastRevealedLocation(List<Vector2Int> locations) (line 163)
- private Vector2Int? GetVillagerLocation(object villager) (line 166)
  Gets location via villager's lastWorkId -> building -> Field position.
- private static Vector2Int? TryGetAlertBuildingLocation(object alert) (line 179)
  Two-stage: (1) dictionary reverse-lookup for multi-building monitors, (2) Focus method param type lookup for single-building monitors.
- private static Vector2Int? TryGetFirstBuildingFromFocusMethod(object monitor) (line 252)
  Inspects the monitor's private Focus(BuildingType) method to identify which BuildingsService collection to look in.
- private void Announce(string message, Vector2Int? location = null) (line 310)
  Queues message for batched output (does not speak immediately).
- public void ProcessMessageQueue() (line 325)
  Called from Update loop. Waits for BATCH_WINDOW to expire, deduplicates, adds to history panel, then speaks all pending messages as one utterance.
- private void SubscribeToCalendar(object gameServices) (line 384)
- private void OnSeasonChanged(object season) (line 403)
- private void AnnouncePlagueEvent(string seasonName) (line 422)
  Announces plague activation/end for Sealed Forest biome (Storm = plague activates, Drizzle = plague ends).
- private void OnYearChanged(object year) (line 455)
- private void SubscribeToNewcomers(object gameServices) (line 466)
- private void OnNewcomersArrival(object _) (line 478)
- private void SubscribeToVillagers(object gameServices) (line 489)
- private void OnVillagerRemoved(object villager) (line 501)
- private void SubscribeToHostility(object gameServices) (line 549)
- private void OnHostilityLevelUp(object level) (line 568)
- private void OnHostilityLevelDown(object level) (line 578)
- private void SubscribeToTrade(object gameServices) (line 593)
- private void OnTraderDeparted(object traderVisit) (line 605)
- private void SubscribeToOrders(object gameServices) (line 615)
- private void OnOrderStarted(object orderState) (line 641)
- private void OnOrderCompleted(object orderState) (line 650)
- private void OnOrderFailed(object orderState) (line 656)
- private void SubscribeToGlades(object gameServices) (line 666)
- private void OnGladeRevealed(object gladeState) (line 678)
- private void SubscribeToReputation(object gameServices) (line 707)
- private void OnReputationChanged(object reputationChange) (line 726)
- private void OnGameResult(object won) (line 749)
- private void SubscribeToNews(object gameServices) (line 763)
- private void OnNewsPublished(object newsList) (line 775)
  Deduplicates with FIFO eviction (max 100 items). Strips rich text and "Alert:" prefixes.
- private void SubscribeToGameBlackboard() (line 817)
- private void OnBuildingFinished(object building) (line 903)
- private string GetBuildingName(object building) (line 915)
- private void OnHearthIgnited(object hearth) (line 946)
- private void OnHearthLeveledUp(object hearth) (line 954)
- private void OnHearthLeveledDown(object hearth) (line 960)
- private void OnHearthCorrupted(object hearth) (line 966)
- private void OnGoodDiscovered(object goodName) (line 972)
- private void OnRelicResolved(object relic) (line 997)
- private void OnRewardChaseStarted(object gladeState) (line 1008)
- private void OnRewardChaseEnded(object gladeState) (line 1014)
- private void OnPortExpeditionStarted(object port) (line 1020)
- private void SubscribeToLocateEvents() (line 1035)
- private void OnGrassLocationRevealed() (line 1050)
- private void OnSpringsLocationRevealed() (line 1056)
- private void OnRelicLocationRevealed() (line 1062)
- private void OnRelicHighlighted(string relicName, Vector2Int position) (line 1068)
- private void SubscribeToReputationRewards(object gameServices) (line 1081)
- private void OnBlueprintPickRequested(object _) (line 1093)
- private void SubscribeToCornerstones(object gameServices) (line 1103)
- private void OnCornerstonePicksChanged(object _) (line 1115)
- private void SubscribeToMonitors(object gameServices) (line 1125)
- private void OnAlertsChanged(object alertsList) (line 1137)
  Processes IMonitorsService alerts. Deduplicates with FIFO eviction (max 100 items). Strips rich text and "Alert:" prefixes.
- public static void RegisterSacrificeStoppedPatch(Harmony harmony) (line 1209)
  Registers a Harmony postfix patch for sacrifice stop events.
- public void SetInstance() (line 1280)
  Sets static _instance = this for Harmony patch callbacks.
- public static void ClearSacrificeState() (line 1287)
