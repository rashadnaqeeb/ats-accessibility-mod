using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Subscribes to game events and announces them via speech.
    /// All subscriptions are managed to be disposed on scene changes.
    /// </summary>
    public class EventAnnouncer
    {
        private List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _subscribed = false;

        // Track subscription time to ignore events during initialization
        private float _subscriptionTime = 0f;
        private const float INITIALIZATION_GRACE_PERIOD = 2f; // Ignore events for 2 seconds after subscribing

        // Track last announced values to avoid duplicate announcements
        private int _lastAnnouncedHostilityLevel = -1;
        private HashSet<string> _announcedAlerts = new HashSet<string>();
        private Queue<string> _announcedAlertsOrder = new Queue<string>();
        private HashSet<string> _announcedNews = new HashSet<string>();

        // Static compiled regex for stripping rich text tags
        private static readonly System.Text.RegularExpressions.Regex RichTextTagsRegex =
            new System.Text.RegularExpressions.Regex("<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

        // Cached reflection for villager removal
        private static MethodInfo _villagerGetDisplayNameMethod;
        private static FieldInfo _villagerStateField;
        private static FieldInfo _villagerStateLossTypeField;
        private static FieldInfo _villagerStateLossReasonField;
        private static bool _villagerReflectionCached = false;

        // Cached reflection for glade danger level
        private static MethodInfo _gladesGetDangerLevelMethod;

        // Cached reflection metadata
        private static bool _reflectionCached = false;
        private static PropertyInfo _calendarServiceProperty;
        private static PropertyInfo _hostilityServiceProperty;
        private static PropertyInfo _tradeServiceProperty;
        private static PropertyInfo _ordersServiceProperty;
        private static PropertyInfo _gladesServiceProperty;
        private static PropertyInfo _reputationServiceProperty;
        private static PropertyInfo _newsServiceProperty;
        private static PropertyInfo _newcomersServiceProperty;
        private static PropertyInfo _reputationRewardsServiceProperty;
        private static PropertyInfo _cornerstonesServiceProperty;
        private static PropertyInfo _monitorsServiceProperty;
        private static PropertyInfo _villagersServiceProperty;

        /// <summary>
        /// Try to subscribe to game events.
        /// Called periodically until successful.
        /// </summary>
        public void TrySubscribe()
        {
            if (_subscribed) return;
            if (!GameReflection.GetIsGameActive()) return;

            try
            {
                EnsureReflectionCached();

                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return;

                // Subscribe to all event sources
                // Note: Some events removed in favor of game's built-in alerts (IMonitorsService)
                SubscribeToCalendar(gameServices);
                SubscribeToHostility(gameServices);
                SubscribeToTrade(gameServices);
                SubscribeToOrders(gameServices);
                SubscribeToGlades(gameServices);
                SubscribeToReputation(gameServices);
                SubscribeToNews(gameServices);
                SubscribeToNewcomers(gameServices);
                SubscribeToGameBlackboard();
                SubscribeToReputationRewards(gameServices);
                SubscribeToCornerstones(gameServices);
                SubscribeToMonitors(gameServices);
                SubscribeToVillagers(gameServices);

                _subscribed = true;
                _subscriptionTime = Time.realtimeSinceStartup;
                Debug.Log("[ATSAccessibility] EventAnnouncer: Subscribed to game events");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] EventAnnouncer subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose all subscriptions.
        /// Called when leaving game scene.
        /// </summary>
        public void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub?.Dispose();
            }
            _subscriptions.Clear();
            _subscribed = false;
            _subscriptionTime = 0f;
            _lastAnnouncedHostilityLevel = -1;
            _announcedAlerts.Clear();
            _announcedAlertsOrder.Clear();
            _announcedNews.Clear();
            Debug.Log("[ATSAccessibility] EventAnnouncer: Disposed all subscriptions");
        }

        /// <summary>
        /// Check if we're still in the initialization grace period.
        /// Events during this period are ignored to avoid announcing pre-existing state.
        /// </summary>
        private bool IsInGracePeriod()
        {
            return Time.realtimeSinceStartup - _subscriptionTime < INITIALIZATION_GRACE_PERIOD;
        }

        /// <summary>
        /// Announce a message via speech and add it to the history.
        /// </summary>
        private void Announce(string message)
        {
            Speech.Say(message);
            AnnouncementHistoryPanel.AddMessage(message);
        }

        private void EnsureReflectionCached()
        {
            if (_reflectionCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null) return;

            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _calendarServiceProperty = gameServicesType.GetProperty("CalendarService");
                _hostilityServiceProperty = gameServicesType.GetProperty("HostilityService");
                _tradeServiceProperty = gameServicesType.GetProperty("TradeService");
                _ordersServiceProperty = gameServicesType.GetProperty("OrdersService");
                _gladesServiceProperty = gameServicesType.GetProperty("GladesService");
                _reputationServiceProperty = gameServicesType.GetProperty("ReputationService");
                _newsServiceProperty = gameServicesType.GetProperty("NewsService");
                _newcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");
                _reputationRewardsServiceProperty = gameServicesType.GetProperty("ReputationRewardsService");
                _cornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService");
                _monitorsServiceProperty = gameServicesType.GetProperty("MonitorsService");
                _villagersServiceProperty = gameServicesType.GetProperty("VillagersService");
            }

            _reflectionCached = true;
        }

        // ========================================
        // CALENDAR SERVICE (Season, Year)
        // ========================================

        private void SubscribeToCalendar(object gameServices)
        {
            var service = _calendarServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnSeasonChanged
            var onSeasonChanged = service.GetType().GetProperty("OnSeasonChanged")?.GetValue(service);
            if (onSeasonChanged != null)
            {
                var sub = GameReflection.SubscribeToObservable(onSeasonChanged, OnSeasonChanged);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnYearChanged
            var onYearChanged = service.GetType().GetProperty("OnYearChanged")?.GetValue(service);
            if (onYearChanged != null)
            {
                var sub = GameReflection.SubscribeToObservable(onYearChanged, OnYearChanged);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnSeasonChanged(object season)
        {
            if (!Plugin.AnnounceSeasonChanged.Value) return;
            if (IsInGracePeriod()) return;
            string seasonName = season?.ToString() ?? "Unknown";
            Announce($"Season changed to {seasonName}");
        }

        private void OnYearChanged(object year)
        {
            if (!Plugin.AnnounceYearChanged.Value) return;
            if (IsInGracePeriod()) return;
            Announce($"Year {year}");
        }

        // ========================================
        // NEWCOMERS SERVICE
        // ========================================
        // OnNewcomersArrival removed - covered by game's AlertsNewcomers

        private void SubscribeToNewcomers(object gameServices)
        {
            var service = _newcomersServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnNewcomersPicked - announces when player picks newcomers (not covered by game alerts)
            var onNewcomersPicked = service.GetType().GetProperty("OnNewcomersPicked")?.GetValue(service);
            if (onNewcomersPicked != null)
            {
                var sub = GameReflection.SubscribeToObservable(onNewcomersPicked, OnNewcomersPicked);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnNewcomersPicked(object _)
        {
            if (!Plugin.AnnounceVillagersArrived.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Newcomers joined the settlement");
        }

        // ========================================
        // VILLAGERS SERVICE (Villager Loss)
        // ========================================
        // Re-added because game's NewsService alerts depend on user's in-game alert settings

        private void SubscribeToVillagers(object gameServices)
        {
            var service = _villagersServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnVillagerRemoved - fires when a villager dies or leaves
            var onVillagerRemoved = service.GetType().GetProperty("OnVillagerRemoved")?.GetValue(service);
            if (onVillagerRemoved != null)
            {
                var sub = GameReflection.SubscribeToObservable(onVillagerRemoved, OnVillagerRemoved);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void EnsureVillagerReflectionCached(object villager)
        {
            if (_villagerReflectionCached || villager == null) return;

            var villagerType = villager.GetType();
            _villagerGetDisplayNameMethod = villagerType.GetMethod("GetDisplayName");
            _villagerStateField = villagerType.GetField("state");

            var stateObj = _villagerStateField?.GetValue(villager);
            if (stateObj != null)
            {
                var stateType = stateObj.GetType();
                _villagerStateLossTypeField = stateType.GetField("lossType");
                _villagerStateLossReasonField = stateType.GetField("lossReasonKey");
            }

            _villagerReflectionCached = true;
        }

        private void OnVillagerRemoved(object villager)
        {
            if (!Plugin.AnnounceVillagerLost.Value) return;
            if (IsInGracePeriod()) return;

            try
            {
                EnsureVillagerReflectionCached(villager);

                // Get villager name using cached method
                string villagerName = _villagerGetDisplayNameMethod?.Invoke(villager, null) as string ?? "Villager";

                // Get loss type from villager.state.lossType using cached fields
                var state = _villagerStateField?.GetValue(villager);
                var lossType = _villagerStateLossTypeField?.GetValue(state);
                string lossTypeStr = lossType?.ToString() ?? "Unknown";

                // Get reason from villager.state.lossReasonKey using cached field
                string reasonKey = _villagerStateLossReasonField?.GetValue(state) as string;

                string reason = "";
                if (!string.IsNullOrEmpty(reasonKey))
                {
                    // Extract readable text from key (e.g., "Villagers_LeaveReason_LowResolve" -> "Low Resolve")
                    reason = reasonKey.Replace("Villagers_LeaveReason_", "")
                                      .Replace("Villagers_DeathReason_", "")
                                      .Replace("_", " ");
                }

                string message;
                if (lossTypeStr == "Leave")
                    message = $"{villagerName} left";
                else if (lossTypeStr == "Exile")
                    message = $"{villagerName} exiled";
                else
                    message = $"{villagerName} died";

                if (!string.IsNullOrEmpty(reason))
                    message += $": {reason}";

                Announce(message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OnVillagerRemoved failed: {ex.Message}");
                Announce("Villager lost");
            }
        }

        // ========================================
        // HOSTILITY SERVICE
        // ========================================

        private void SubscribeToHostility(object gameServices)
        {
            var service = _hostilityServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnLevelUp
            var onLevelUp = service.GetType().GetProperty("OnLevelUp")?.GetValue(service);
            if (onLevelUp != null)
            {
                var sub = GameReflection.SubscribeToObservable(onLevelUp, OnHostilityLevelUp);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnLevelDown
            var onLevelDown = service.GetType().GetProperty("OnLevelDown")?.GetValue(service);
            if (onLevelDown != null)
            {
                var sub = GameReflection.SubscribeToObservable(onLevelDown, OnHostilityLevelDown);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnHostilityLevelUp(object level)
        {
            if (!Plugin.AnnounceHostilityLevelChange.Value) return;
            if (IsInGracePeriod()) return;
            int lvl = level is int i ? i : -1;
            if (lvl != _lastAnnouncedHostilityLevel)
            {
                _lastAnnouncedHostilityLevel = lvl;
                Announce($"Hostility increased to level {lvl}");
            }
        }

        private void OnHostilityLevelDown(object level)
        {
            if (!Plugin.AnnounceHostilityLevelChange.Value) return;
            if (IsInGracePeriod()) return;
            int lvl = level is int i ? i : -1;
            if (lvl != _lastAnnouncedHostilityLevel)
            {
                _lastAnnouncedHostilityLevel = lvl;
                Announce($"Hostility decreased to level {lvl}");
            }
        }

        // ========================================
        // TRADE SERVICE
        // ========================================
        // OnTraderArrived removed - covered by game's AlertsTraderArrived

        private void SubscribeToTrade(object gameServices)
        {
            var service = _tradeServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnTraderDepartured (note: game uses "Departured" spelling) - not covered by game alerts
            var onTraderDeparted = service.GetType().GetProperty("OnTraderDepartured")?.GetValue(service);
            if (onTraderDeparted != null)
            {
                var sub = GameReflection.SubscribeToObservable(onTraderDeparted, OnTraderDeparted);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnTraderDeparted(object traderVisit)
        {
            if (!Plugin.AnnounceTraderDeparted.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Trader departed");
        }

        // ========================================
        // ORDERS SERVICE
        // ========================================
        // OnOrderCompleted removed - covered by game's AlertsCompletedOrders

        private void SubscribeToOrders(object gameServices)
        {
            var service = _ordersServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnOrderStarted (new order available) - not covered by game alerts
            var onOrderStarted = service.GetType().GetProperty("OnOrderStarted")?.GetValue(service);
            if (onOrderStarted != null)
            {
                var sub = GameReflection.SubscribeToObservable(onOrderStarted, OnOrderStarted);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnOrderFailed - not covered by game alerts
            var onOrderFailed = service.GetType().GetProperty("OnOrderFailed")?.GetValue(service);
            if (onOrderFailed != null)
            {
                var sub = GameReflection.SubscribeToObservable(onOrderFailed, OnOrderFailed);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnOrderStarted(object orderState)
        {
            if (!Plugin.AnnounceOrderAvailable.Value) return;
            if (IsInGracePeriod()) return;
            Announce("New order available");
        }

        private void OnOrderFailed(object orderState)
        {
            if (!Plugin.AnnounceOrderFailed.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Order failed");
        }

        // ========================================
        // GLADES SERVICE
        // ========================================

        private void SubscribeToGlades(object gameServices)
        {
            var service = _gladesServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnGladeRevealed
            var onGladeRevealed = service.GetType().GetProperty("OnGladeRevealed")?.GetValue(service);
            if (onGladeRevealed != null)
            {
                var sub = GameReflection.SubscribeToObservable(onGladeRevealed, OnGladeRevealed);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnGladeRevealed(object gladeState)
        {
            if (!Plugin.AnnounceGladeRevealed.Value) return;
            if (IsInGracePeriod()) return;

            string dangerInfo = "";
            try
            {
                // Get danger level from GladesService using cached method
                var gameServices = GameReflection.GetGameServices();
                var gladesService = _gladesServiceProperty?.GetValue(gameServices);
                if (gladesService != null)
                {
                    // Cache the method on first use
                    if (_gladesGetDangerLevelMethod == null)
                    {
                        _gladesGetDangerLevelMethod = gladesService.GetType().GetMethod("GetDangerLevel");
                    }

                    var dangerLevel = _gladesGetDangerLevelMethod?.Invoke(gladesService, new[] { gladeState });
                    if (dangerLevel != null)
                    {
                        string level = dangerLevel.ToString();
                        if (level != "None" && level != "Safe")
                        {
                            dangerInfo = $", {level} danger";
                        }
                    }
                }
            }
            catch { }

            Announce($"Glade revealed{dangerInfo}");
        }

        // ========================================
        // REPUTATION SERVICE
        // ========================================

        private void SubscribeToReputation(object gameServices)
        {
            var service = _reputationServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnReputationChanged
            var onReputationChanged = service.GetType().GetProperty("OnReputationChanged")?.GetValue(service);
            if (onReputationChanged != null)
            {
                var sub = GameReflection.SubscribeToObservable(onReputationChanged, OnReputationChanged);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnGameResult
            var onGameResult = service.GetType().GetProperty("OnGameResult")?.GetValue(service);
            if (onGameResult != null)
            {
                var sub = GameReflection.SubscribeToObservable(onGameResult, OnGameResult);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnReputationChanged(object reputationChange)
        {
            if (!Plugin.AnnounceReputationChanged.Value) return;
            if (IsInGracePeriod()) return;

            try
            {
                // Get the amount from the change
                var amountField = reputationChange?.GetType().GetField("amount");
                float amount = amountField != null ? (float)amountField.GetValue(reputationChange) : 0f;

                // Only announce if it's a significant change (positive or negative)
                if (Math.Abs(amount) >= 0.1f)
                {
                    // Format to 1 decimal place
                    string amountStr = Math.Abs(amount).ToString("F1");
                    if (amount > 0)
                        Announce($"Reputation gained: {amountStr}");
                    else
                        Announce($"Reputation lost: {amountStr}");
                }
            }
            catch
            {
                // Fallback
            }
        }

        private void OnGameResult(object won)
        {
            if (!Plugin.AnnounceGameResult.Value) return;

            bool isWon = won is bool b && b;
            if (isWon)
                Announce("Victory! Game won");
            else
                Announce("Defeat! Game lost");
        }

        // ========================================
        // NEWS SERVICE
        // ========================================

        private void SubscribeToNews(object gameServices)
        {
            var service = _newsServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // News
            var newsObservable = service.GetType().GetProperty("News")?.GetValue(service);
            if (newsObservable != null)
            {
                var sub = GameReflection.SubscribeToObservable(newsObservable, OnNewsPublished);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnNewsPublished(object newsList)
        {
            if (!Plugin.AnnounceGameWarnings.Value) return;
            if (IsInGracePeriod()) return;

            try
            {
                // newsList is List<News>
                var list = newsList as System.Collections.IList;
                if (list == null || list.Count == 0) return;

                // Check each news item and announce only new ones
                foreach (var news in list)
                {
                    if (news == null) continue;

                    var contentProperty = news.GetType().GetProperty("content");
                    var content = contentProperty?.GetValue(news)?.ToString();

                    if (string.IsNullOrEmpty(content)) continue;

                    // Skip if already announced
                    if (_announcedNews.Contains(content)) continue;
                    _announcedNews.Add(content);

                    // Clean up if too large
                    if (_announcedNews.Count > 50)
                    {
                        _announcedNews.Clear();
                        _announcedNews.Add(content);
                    }

                    // Strip any rich text tags like <color>, <b>, etc.
                    string cleanContent = RichTextTagsRegex.Replace(content, "");
                    Announce($"Alert: {cleanContent}");
                }
            }
            catch { }
        }

        // BLIGHT SERVICE - Removed, covered by game's AlertsBlight

        // ========================================
        // GAME BLACKBOARD SERVICE
        // ========================================

        private void SubscribeToGameBlackboard()
        {
            var blackboard = GameReflection.GetGameBlackboardService();
            if (blackboard == null) return;

            var blackboardType = blackboard.GetType();

            // BuildingFinished
            var buildingFinishedProp = blackboardType.GetProperty("BuildingFinished");
            if (buildingFinishedProp != null)
            {
                var buildingFinished = buildingFinishedProp.GetValue(blackboard);
                if (buildingFinished != null)
                {
                    var sub = GameReflection.SubscribeToObservable(buildingFinished, OnBuildingFinished);
                    if (sub != null) _subscriptions.Add(sub);
                }
            }

            // FinishedBuildingRemoved removed - covered by game's AlertsBuildingLoss

            // OnHearthIgnited
            var hearthIgnited = blackboardType.GetProperty("OnHearthIgnited")?.GetValue(blackboard);
            if (hearthIgnited != null)
            {
                var sub = GameReflection.SubscribeToObservable(hearthIgnited, OnHearthIgnited);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnHearthDiedDown removed - covered by game's AlertsFireDown

            // OnHubLeveledUp
            var hubLeveledUp = blackboardType.GetProperty("OnHubLeveledUp")?.GetValue(blackboard);
            if (hubLeveledUp != null)
            {
                var sub = GameReflection.SubscribeToObservable(hubLeveledUp, OnHearthLeveledUp);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnHubLeveledDown
            var hubLeveledDown = blackboardType.GetProperty("OnHubLeveledDown")?.GetValue(blackboard);
            if (hubLeveledDown != null)
            {
                var sub = GameReflection.SubscribeToObservable(hubLeveledDown, OnHearthLeveledDown);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnHearthCorrupted
            var hearthCorrupted = blackboardType.GetProperty("OnHearthCorrupted")?.GetValue(blackboard);
            if (hearthCorrupted != null)
            {
                var sub = GameReflection.SubscribeToObservable(hearthCorrupted, OnHearthCorrupted);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnGoodDiscovered
            var goodDiscovered = blackboardType.GetProperty("OnGoodDiscovered")?.GetValue(blackboard);
            if (goodDiscovered != null)
            {
                var sub = GameReflection.SubscribeToObservable(goodDiscovered, OnGoodDiscovered);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnBlightCystSpawned removed - covered by game's AlertsBlight

            // OnRelicResolved
            var relicResolved = blackboardType.GetProperty("OnRelicResolved")?.GetValue(blackboard);
            if (relicResolved != null)
            {
                var sub = GameReflection.SubscribeToObservable(relicResolved, OnRelicResolved);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnRewardChaseStarted
            var chaseStarted = blackboardType.GetProperty("OnRewardChaseStarted")?.GetValue(blackboard);
            if (chaseStarted != null)
            {
                var sub = GameReflection.SubscribeToObservable(chaseStarted, OnRewardChaseStarted);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnRewardChaseEnded
            var chaseEnded = blackboardType.GetProperty("OnRewardChaseEnded")?.GetValue(blackboard);
            if (chaseEnded != null)
            {
                var sub = GameReflection.SubscribeToObservable(chaseEnded, OnRewardChaseEnded);
                if (sub != null) _subscriptions.Add(sub);
            }

            // OnPortExpeditionFinished
            var portExpeditionFinished = blackboardType.GetProperty("OnPortExpeditionFinished")?.GetValue(blackboard);
            if (portExpeditionFinished != null)
            {
                var sub = GameReflection.SubscribeToObservable(portExpeditionFinished, OnPortExpeditionFinished);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnBuildingFinished(object building)
        {
            if (!Plugin.AnnounceConstructionComplete.Value) return;
            if (IsInGracePeriod()) return; // Ignore events during initialization

            string buildingName = GetBuildingName(building);
            Announce($"{buildingName} construction complete");
        }

        /// <summary>
        /// Extract the display name from a Building object.
        /// Building has a DisplayName property that returns BuildingModel.displayName.Text
        /// </summary>
        private string GetBuildingName(object building)
        {
            if (building == null) return "Building";

            try
            {
                // Try DisplayName property first (direct on Building)
                var displayNameProp = building.GetType().GetProperty("DisplayName");
                if (displayNameProp != null)
                {
                    var name = displayNameProp.GetValue(building) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }

                // Fallback: try BuildingModel.displayName (displayName is a field, not property)
                var modelProperty = building.GetType().GetProperty("BuildingModel");
                if (modelProperty != null)
                {
                    var model = modelProperty.GetValue(building);
                    if (model != null)
                    {
                        var displayNameField = model.GetType().GetField("displayName");
                        var displayName = displayNameField?.GetValue(model);
                        var name = GameReflection.GetLocaText(displayName);
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            }
            catch
            {
                // Failed to get building name, return fallback
            }

            return "Building";
        }

        // OnBuildingDestroyed removed - covered by game's AlertsBuildingLoss

        private void OnHearthIgnited(object hearth)
        {
            if (!Plugin.AnnounceHearthIgnited.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Hearth ignited");
        }

        // OnHearthDied removed - covered by game's AlertsFireDown

        private void OnHearthLeveledUp(object hearth)
        {
            if (!Plugin.AnnounceHearthLevelChange.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Hearth leveled up");
        }

        private void OnHearthLeveledDown(object hearth)
        {
            if (!Plugin.AnnounceHearthLevelChange.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Hearth leveled down");
        }

        private void OnHearthCorrupted(object hearth)
        {
            if (!Plugin.AnnounceHearthCorrupted.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Hearth corrupted by blight");
        }

        private void OnGoodDiscovered(object goodName)
        {
            if (!Plugin.AnnounceGoodDiscovered.Value) return;
            if (IsInGracePeriod()) return;

            string name = goodName?.ToString() ?? "Unknown";
            // Try to get the display name from settings
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings != null)
                {
                    var getGoodMethod = settings.GetType().GetMethod("GetGood");
                    var good = getGoodMethod?.Invoke(settings, new[] { goodName });
                    if (good != null)
                    {
                        // GoodModel.displayName is a field, not a property
                        var displayNameField = good.GetType().GetField("displayName");
                        var displayName = displayNameField?.GetValue(good);
                        name = GameReflection.GetLocaText(displayName) ?? name;
                    }
                }
            }
            catch { }

            Announce($"New good discovered: {name}");
        }

        // OnBlightCystSpawned removed - covered by game's AlertsBlight

        private void OnRelicResolved(object relic)
        {
            if (!Plugin.AnnounceRelicResolved.Value) return;
            if (IsInGracePeriod()) return;

            // Relic extends Building, so we can use GetBuildingName
            string relicName = GetBuildingName(relic);
            if (relicName == "Building") relicName = "Relic"; // Fallback

            Announce($"{relicName} resolved");
        }

        private void OnRewardChaseStarted(object gladeState)
        {
            if (!Plugin.AnnounceRewardChase.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Reward chase started");
        }

        private void OnRewardChaseEnded(object gladeState)
        {
            if (!Plugin.AnnounceRewardChase.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Reward chase ended");
        }

        private void OnPortExpeditionFinished(object port)
        {
            if (!Plugin.AnnouncePortExpeditionFinished.Value) return;
            if (IsInGracePeriod()) return;
            Announce("Port expedition finished");
        }

        // ========================================
        // REPUTATION REWARDS SERVICE (Blueprints)
        // ========================================

        private void SubscribeToReputationRewards(object gameServices)
        {
            var service = _reputationRewardsServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // PickPopupRequested - fires when the blueprint pick popup is requested
            var pickPopupRequested = service.GetType().GetProperty("PickPopupRequested")?.GetValue(service);
            if (pickPopupRequested != null)
            {
                var sub = GameReflection.SubscribeToObservable(pickPopupRequested, OnBlueprintPickRequested);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnBlueprintPickRequested(object _)
        {
            if (!Plugin.AnnounceBlueprintAvailable.Value) return;
            if (IsInGracePeriod()) return;
            Announce("New blueprint available to pick");
        }

        // ========================================
        // CORNERSTONES SERVICE
        // ========================================

        private void SubscribeToCornerstones(object gameServices)
        {
            var service = _cornerstonesServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // OnPicksChanged - fires when cornerstone picks become available
            var onPicksChanged = service.GetType().GetProperty("OnPicksChanged")?.GetValue(service);
            if (onPicksChanged != null)
            {
                var sub = GameReflection.SubscribeToObservable(onPicksChanged, OnCornerstonePicksChanged);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnCornerstonePicksChanged(object _)
        {
            if (!Plugin.AnnounceCornerstoneAvailable.Value) return;
            if (IsInGracePeriod()) return;
            Announce("New cornerstone available to pick");
        }

        // ========================================
        // MONITORS SERVICE (Game's Built-in Alerts)
        // ========================================

        private void SubscribeToMonitors(object gameServices)
        {
            var service = _monitorsServiceProperty?.GetValue(gameServices);
            if (service == null) return;

            // Subscribe to Alerts observable
            var alertsObservable = service.GetType().GetProperty("Alerts")?.GetValue(service);
            if (alertsObservable != null)
            {
                var sub = GameReflection.SubscribeToObservable(alertsObservable, OnAlertsChanged);
                if (sub != null) _subscriptions.Add(sub);
            }
        }

        private void OnAlertsChanged(object alertsList)
        {
            if (!Plugin.AnnounceGameAlerts.Value) return;
            if (IsInGracePeriod()) return;

            try
            {
                var list = alertsList as System.Collections.IList;
                if (list == null || list.Count == 0) return;

                // Check each alert and announce new ones
                foreach (var alert in list)
                {
                    if (alert == null) continue;

                    // Get alert properties
                    var textField = alert.GetType().GetField("text");
                    var dismissedField = alert.GetType().GetField("dismissed");
                    var showTimeField = alert.GetType().GetField("showTime");

                    string text = textField?.GetValue(alert) as string;
                    bool dismissed = dismissedField != null && (bool)dismissedField.GetValue(alert);
                    float showTime = showTimeField != null ? (float)showTimeField.GetValue(alert) : 0f;

                    if (string.IsNullOrEmpty(text) || dismissed) continue;

                    // Create a unique key for this alert (text + showTime to handle same text at different times)
                    string alertKey = $"{text}_{showTime:F2}";

                    // Only announce if we haven't already announced this specific alert
                    if (!_announcedAlerts.Contains(alertKey))
                    {
                        _announcedAlerts.Add(alertKey);
                        _announcedAlertsOrder.Enqueue(alertKey);

                        // Evict oldest alerts to prevent memory growth
                        while (_announcedAlerts.Count > 100)
                        {
                            var oldest = _announcedAlertsOrder.Dequeue();
                            _announcedAlerts.Remove(oldest);
                        }

                        // Strip any rich text tags
                        text = RichTextTagsRegex.Replace(text, "");
                        Announce($"Alert: {text}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] OnAlertsChanged error: {ex.Message}");
            }
        }
    }
}
