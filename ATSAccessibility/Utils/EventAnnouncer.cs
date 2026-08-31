using ATSAccessibility.Panels;
using ATSAccessibility.Overlays;
using ATSAccessibility.Core;
using ATSAccessibility.Reflection;
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Subscribes to game events and announces them via speech.
	/// All subscriptions are managed to be disposed on scene changes.
	/// </summary>
	public class EventAnnouncer {
		private List<IDisposable> _subscriptions = new List<IDisposable>();
		private bool _subscribed = false;

		// Track grace period end time to ignore events during initialization
		private float _gracePeriodEndTime = 0f;
		private const float INITIALIZATION_GRACE_PERIOD = 2f; // Ignore events for 2 seconds after subscribing

		// Track last announced values to avoid duplicate announcements
		private int _lastAnnouncedHostilityLevel = -1;
		private HashSet<string> _announcedAlerts = new HashSet<string>();
		private Queue<string> _announcedAlertsOrder = new Queue<string>();
		private HashSet<string> _announcedNews = new HashSet<string>();
		private Queue<string> _announcedNewsOrder = new Queue<string>();

		// Static compiled regex for stripping rich text tags
		private static readonly System.Text.RegularExpressions.Regex RichTextTagsRegex =
			new System.Text.RegularExpressions.Regex("<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

		// Static compiled regex for stripping "Alert:" or "Alert" prefix from game alerts
		private static readonly System.Text.RegularExpressions.Regex AlertPrefixRegex =
			new System.Text.RegularExpressions.Regex(@"^\s*alert:?\s*", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		// Message batching to prevent interruption when multiple events fire at once
		private List<(string message, float time, Vector2Int? location)> _pendingMessages = new List<(string, float, Vector2Int?)>();
		private float _batchStartTime = 0f;
		private const float BATCH_WINDOW = 0.15f; // 150ms batching window

		// Callback for when a new order becomes available (used to refresh OrdersOverlay)
		public Action OnNewOrderAvailable { get; set; }

		/// <summary>
		/// Try to subscribe to game events.
		/// Called periodically until successful.
		/// </summary>
		// Bounded per-source retry: a source that isn't ready yet is retried on
		// the next poll while the sources that succeeded stay live, and after MAX
		// attempts the partial set is accepted with a loud log — a source lost to
		// game-API drift must be visible in Player.log, not silently absent for
		// the whole settlement, and one dead source must not black out the rest.
		private int _subscribeAttempts = 0;
		private const int MAX_SUBSCRIBE_ATTEMPTS = 10;
		private readonly HashSet<string> _subscribedSources = new HashSet<string>();

		public void TrySubscribe() {
			if (_subscribed) return;
			if (!GameReflection.GetIsGameActive()) return;

			object gameServices;
			try {
				EventReflection.EnsureReflectionCached();
				gameServices = GameReflection.GetGameServices();
			} catch (Exception ex) {
				// Bounded like source failures: setup that throws on every poll
				// (game-API drift) must not error-log forever. Sources already
				// live from earlier attempts keep announcing.
				Debug.LogError($"[ATSAccessibility] EventAnnouncer subscription setup failed: {ex.Message}");
				if (++_subscribeAttempts >= MAX_SUBSCRIBE_ATTEMPTS)
					AcceptSubscriptions(new List<string> { "setup (all unsubscribed sources)" });
				return;
			}
			if (gameServices == null) return;

			// Keep the grace window open across retries: sources subscribed on an
			// earlier attempt stay live, and their initialization noise must not
			// be announced while the remaining sources are still being retried
			// (polls are 0.5s apart, so the 2s window always covers the gap).
			_gracePeriodEndTime = Time.realtimeSinceStartup + INITIALIZATION_GRACE_PERIOD;

			// Subscribe to all event sources
			// Note: Some events removed in favor of game's built-in alerts (IMonitorsService)
			var failed = new List<string>();
			if (!TrySubscribeSource("Calendar", () => SubscribeToCalendar(gameServices))) failed.Add("Calendar");
			if (!TrySubscribeSource("Hostility", () => SubscribeToHostility(gameServices))) failed.Add("Hostility");
			if (!TrySubscribeSource("Trade", () => SubscribeToTrade(gameServices))) failed.Add("Trade");
			if (!TrySubscribeSource("Orders", () => SubscribeToOrders(gameServices))) failed.Add("Orders");
			if (!TrySubscribeSource("Glades", () => SubscribeToGlades(gameServices))) failed.Add("Glades");
			if (!TrySubscribeSource("Reputation", () => SubscribeToReputation(gameServices))) failed.Add("Reputation");
			if (!TrySubscribeSource("News", () => SubscribeToNews(gameServices))) failed.Add("News");
			if (!TrySubscribeSource("Newcomers", () => SubscribeToNewcomers(gameServices))) failed.Add("Newcomers");
			if (!TrySubscribeSource("GameBlackboard", SubscribeToGameBlackboard)) failed.Add("GameBlackboard");
			if (!TrySubscribeSource("ReputationRewards", () => SubscribeToReputationRewards(gameServices))) failed.Add("ReputationRewards");
			if (!TrySubscribeSource("Cornerstones", () => SubscribeToCornerstones(gameServices))) failed.Add("Cornerstones");
			if (!TrySubscribeSource("Monitors", () => SubscribeToMonitors(gameServices))) failed.Add("Monitors");
			if (!TrySubscribeSource("Villagers", () => SubscribeToVillagers(gameServices))) failed.Add("Villagers");
			if (!TrySubscribeSource("LocateEvents", SubscribeToLocateEvents)) failed.Add("LocateEvents");

			if (failed.Count > 0 && ++_subscribeAttempts < MAX_SUBSCRIBE_ATTEMPTS)
				return;  // Failed sources retry next poll; successful ones stay live.

			AcceptSubscriptions(failed);
		}

		/// <summary>
		/// Run one source's subscribe function, at most once per settlement.
		/// On failure (returned false or threw), dispose only the subscriptions
		/// that source added, so the next poll can retry it without stacking
		/// duplicates and without disturbing the other sources.
		/// </summary>
		private bool TrySubscribeSource(string name, Func<bool> subscribe) {
			if (_subscribedSources.Contains(name)) return true;

			int countBefore = _subscriptions.Count;
			bool ok;
			try {
				ok = subscribe();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] EventAnnouncer: subscribing {name} threw: {ex.Message}");
				ok = false;
			}

			if (!ok) {
				// Roll back this source's partial subs only (group helpers can
				// fail after some of their observables already subscribed).
				for (int i = _subscriptions.Count - 1; i >= countBefore; i--) {
					_subscriptions[i]?.Dispose();
					_subscriptions.RemoveAt(i);
				}
				return false;
			}

			_subscribedSources.Add(name);
			return true;
		}

		private void AcceptSubscriptions(List<string> failed) {
			if (failed.Count > 0)
				Debug.LogError($"[ATSAccessibility] EventAnnouncer: continuing without {string.Join(", ", failed)} after {MAX_SUBSCRIBE_ATTEMPTS} attempts — those events will not be announced this settlement");

			_subscribed = true;
			_gracePeriodEndTime = Time.realtimeSinceStartup + INITIALIZATION_GRACE_PERIOD;
			SetInstance();  // Set instance for static patch callbacks
			Debug.Log("[ATSAccessibility] EventAnnouncer: Subscribed to game events");
		}

		/// <summary>
		/// Subscribe to one observable and track the subscription.
		/// Returns false when the observable is missing or the subscribe failed.
		/// </summary>
		private bool AddSubscription(object observable, Action<object> callback) {
			if (observable == null) return false;
			var sub = GameReflection.SubscribeToObservable(observable, callback);
			if (sub == null) return false;
			_subscriptions.Add(sub);
			return true;
		}

		/// <summary>
		/// Dispose all subscriptions.
		/// Called when leaving game scene.
		/// </summary>
		public void Dispose() {
			try {
				foreach (var sub in _subscriptions) {
					sub?.Dispose();
				}
				_subscriptions.Clear();
				_subscribed = false;
				_subscribeAttempts = 0;
				_subscribedSources.Clear();
				_gracePeriodEndTime = 0f;
				_lastAnnouncedHostilityLevel = -1;
				_announcedAlerts.Clear();
				_announcedAlertsOrder.Clear();
				_announcedNews.Clear();
				_announcedNewsOrder.Clear();
				_pendingMessages.Clear();

				// Reset reflection cached flags so they get re-cached on next game
				// (services may have different types/methods in different game versions)
				EventReflection.ResetCache();

				// Clear sacrifice tracking state
				ClearSacrificeState();

				// Clear building idle tracking state
				ClearBuildingIdleState();

				// Clear highlighted relics tracking
				MapReflection.ClearHighlightedRelics();
			} finally {
				// Clear static instance to prevent stale reference on scene change
				// Must be in finally to avoid stale reference if cleanup throws
				if (_instance == this) {
					_instance = null;
				}

				Debug.Log("[ATSAccessibility] EventAnnouncer: Disposed all subscriptions");
			}
		}

		/// <summary>
		/// Check if we're still in the initialization grace period.
		/// Events during this period are ignored to avoid announcing pre-existing state.
		/// Uses pre-calculated end time for consistent checks across concurrent events.
		/// </summary>
		private bool IsInGracePeriod() {
			return Time.realtimeSinceStartup < _gracePeriodEndTime;
		}

		// ========================================
		// LOCATION HELPERS
		// ========================================

		private static Vector2Int? GetBuildingLocation(object building) {
			if (building == null) return null;
			var pos = ConstructionReflection.GetBuildingGridPosition(building);
			if (pos == Vector2Int.zero) return null;
			return pos;
		}

		private static Vector2Int? GetGladeLocation(object gladeState) {
			if (gladeState == null) return null;

			EventReflection.EnsureGladeFieldsCached(gladeState);
			if (EventReflection.GladeFieldsField == null) return null;

			try {
				var fields = EventReflection.GladeFieldsField.GetValue(gladeState) as List<Vector2Int>;
				if (fields != null && fields.Count > 0)
					return fields[0];
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGladeLocation failed: {ex.Message}");
			}

			return null;
		}

		private static Vector2Int? GetLastRevealedLocation(List<Vector2Int> locations) {
			if (locations == null || locations.Count == 0) return null;
			return locations[locations.Count - 1];
		}

		private Vector2Int? GetVillagerLocation(object villager) {
			if (villager == null) return null;

			try {
				if (EventReflection.VillagerLastWorkIdField == null || EventReflection.VillagerStateField == null) return null;

				var stateObj = EventReflection.VillagerStateField.GetValue(villager);
				if (stateObj == null) return null;

				int lastWorkId = (int)EventReflection.VillagerLastWorkIdField.GetValue(stateObj);
				if (lastWorkId <= 0) return null;

				var building = GameReflection.GetBuildingById(lastWorkId);
				return GetBuildingLocation(building);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetVillagerLocation failed: {ex.Message}");
				return null;
			}
		}

		private static Vector2Int? TryGetAlertBuildingLocation(object alert) {
			if (alert == null) return null;

			try {
				// Get clickCallback delegate from alert
				var callbackField = alert.GetType().GetField("clickCallback");
				var callback = callbackField?.GetValue(alert) as System.Delegate;
				if (callback == null) return null;

				// Get the monitor instance (callback target)
				var monitor = callback.Target;
				if (monitor == null) return null;

				// Stage 1: Dictionary reverse-lookup for multi-building monitors
				// (HearthsMonitor, CampsMonitor, FarmsMonitor, MinesMonitor, etc.)
				var type = monitor.GetType();
				while (type != null) {
					foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
						if (!typeof(System.Collections.IDictionary).IsAssignableFrom(field.FieldType)) continue;
						var dict = field.GetValue(monitor) as System.Collections.IDictionary;
						if (dict == null) continue;

						// Reverse-lookup: find which key (building) maps to this alert
						foreach (System.Collections.DictionaryEntry entry in dict) {
							if (entry.Value != alert) continue;
							// Try to read Field property (Vector2Int) from the building
							var fieldProp = entry.Key.GetType().GetProperty("Field", BindingFlags.Public | BindingFlags.Instance);
							if (fieldProp != null) {
								var pos = fieldProp.GetValue(entry.Key);
								if (pos is Vector2Int v) return v;
							}
							break;
						}
					}
					type = type.BaseType;
				}

				// Stage 2: Single-alert monitor fallback
				// (NoFirekeeperMonitor, BlightMonitor, PortMonitor, etc.)
				// These store a single MonitorAlert field, not a dictionary.
				// Detect by finding a MonitorAlert field matching our alert, then use
				// the monitor's Focus(BuildingType) method signature to find the right
				// building collection on BuildingsService.
				type = monitor.GetType();
				while (type != null) {
					foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
						if (typeof(System.Collections.IDictionary).IsAssignableFrom(field.FieldType)) continue;
						var val = field.GetValue(monitor);
						if (val != alert) continue;

						// Confirmed: this monitor owns the alert via a single field
						return TryGetFirstBuildingFromFocusMethod(monitor);
					}
					type = type.BaseType;
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] TryGetAlertBuildingLocation failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// For single-alert monitors, find the target building by examining the monitor's
		/// private Focus(BuildingType) method. The parameter type identifies which
		/// BuildingsService collection holds the target building.
		/// </summary>
		private static Vector2Int? TryGetFirstBuildingFromFocusMethod(object monitor) {
			// Find a private Focus method that takes a single building parameter
			Type buildingParamType = null;
			var scanType = monitor.GetType();
			while (scanType != null && buildingParamType == null) {
				foreach (var method in scanType.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
					if (method.Name != "Focus") continue;
					var parameters = method.GetParameters();
					if (parameters.Length != 1) continue;
					// Verify parameter type has a Field property (is a Building)
					if (parameters[0].ParameterType.GetProperty("Field", BindingFlags.Public | BindingFlags.Instance) == null) continue;
					buildingParamType = parameters[0].ParameterType;
					break;
				}
				scanType = scanType.BaseType;
			}

			if (buildingParamType == null) return null;

			// Find matching collection on BuildingsService
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) return null;

			foreach (var prop in buildingsService.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
				if (!prop.PropertyType.IsGenericType) continue;
				if (!prop.CanRead) continue;

				var genArgs = prop.PropertyType.GetGenericArguments();
				if (genArgs.Length != 2) continue;
				if (!buildingParamType.IsAssignableFrom(genArgs[1])) continue;

				try {
					var dict = prop.GetValue(buildingsService) as System.Collections.IDictionary;
					if (dict == null || dict.Count == 0) continue;

					// Get first entry's Field position
					foreach (System.Collections.DictionaryEntry entry in dict) {
						var building = entry.Value;
						if (building == null) continue;
						var fieldProp = building.GetType().GetProperty("Field", BindingFlags.Public | BindingFlags.Instance);
						if (fieldProp == null) continue;
						var pos = fieldProp.GetValue(building);
						if (pos is Vector2Int v) return v;
						break;
					}
				} catch { continue; }
			}

			return null;
		}

		/// <summary>
		/// Queue a message for announcement. Messages are batched to prevent
		/// interruption when multiple events fire simultaneously.
		/// </summary>
		private void Announce(string message, Vector2Int? location = null) {
			float currentTime = Time.realtimeSinceStartup;

			// Start a new batch if this is the first message or batch window has expired
			if (_pendingMessages.Count == 0) {
				_batchStartTime = currentTime;
			}

			_pendingMessages.Add((message, currentTime, location));
		}

		/// <summary>
		/// Process the message queue. Call this from Update loop.
		/// Groups duplicate messages and combines multiple messages into single announcement.
		/// </summary>
		public void ProcessMessageQueue() {
			if (_pendingMessages.Count == 0) return;

			float currentTime = Time.realtimeSinceStartup;

			// Wait for batch window to complete
			if (currentTime - _batchStartTime < BATCH_WINDOW) return;

			// Group messages by content and count duplicates
			var messageCounts = new Dictionary<string, int>();
			var messageLocations = new Dictionary<string, Vector2Int?>();
			var messageOrder = new List<string>(); // Preserve order of first occurrence

			foreach (var (message, time, location) in _pendingMessages) {
				// Skip blueprint announcement if overlay is showing description
				if (ReputationRewardOverlay.SuppressBlueprintAnnouncement &&
					message == Strings.Get("util.event.new_blueprint")) {
					continue;
				}

				if (messageCounts.ContainsKey(message)) {
					messageCounts[message]++;
					// Keep first non-null location
					if (location.HasValue && !messageLocations[message].HasValue)
						messageLocations[message] = location;
				} else {
					messageCounts[message] = 1;
					messageLocations[message] = location;
					messageOrder.Add(message);
				}
			}

			// Build list of formatted messages (with count suffix if duplicated)
			var formattedMessages = new List<string>();
			foreach (var message in messageOrder) {
				int count = messageCounts[message];
				string formatted = count > 1 ? Strings.Get("util.event.duplicate", message, count) : message;
				formattedMessages.Add(formatted);

				// Add each message to history individually for review
				AnnouncementHistoryPanel.AddMessage(formatted, messageLocations[message]);
			}

			// Combine all messages into single speech output to prevent interruption
			if (formattedMessages.Count == 1) {
				Speech.Say(formattedMessages[0]);
			} else {
				// Join with period+space for natural pause between messages
				string combined = string.Join(". ", formattedMessages);
				Speech.Say(combined);
			}

			_pendingMessages.Clear();
		}

		// ========================================
		// CALENDAR SERVICE (Season, Year)
		// ========================================

		private bool SubscribeToCalendar(object gameServices) {
			var service = EventReflection.CalendarServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			bool ok = AddSubscription(service.GetType().GetProperty("OnSeasonChanged")?.GetValue(service), OnSeasonChanged);
			ok &= AddSubscription(service.GetType().GetProperty("OnYearChanged")?.GetValue(service), OnYearChanged);
			return ok;
		}

		// Season names (lookup keys for Strings.Get — resolved at call time)
		private static readonly string[] _seasonNameKeys = {
			"common.season_drizzle",
			"common.season_clearance",
			"common.season_storm",
		};

		private void OnSeasonChanged(object season) {
			if (IsInGracePeriod()) return;

			int seasonInt = season != null ? Convert.ToInt32(season) : -1;
			string seasonName = (seasonInt >= 0 && seasonInt < _seasonNameKeys.Length)
				? Strings.Get(_seasonNameKeys[seasonInt])
				: Strings.Get("common.unknown");

			// Announce season change if enabled
			if (Plugin.AnnounceSeasonChanged.Value) {
				Announce(Strings.Get("util.event.season_changed", seasonName));
			}

			// Check for Sealed Forest plague events
			if (Plugin.AnnouncePlagueEvents.Value) {
				AnnouncePlagueEvent(seasonInt);
			}
		}

		/// <summary>
		/// Announce plague activation/end for Sealed Forest biome.
		/// Season int follows the game enum: Drizzle=0, Clearance=1, Storm=2.
		/// </summary>
		private void AnnouncePlagueEvent(int seasonInt) {
			// Check if we're in Sealed Forest (seals exist)
			var seal = SealReflection.GetFirstSeal();
			if (seal == null) return;

			// Don't announce if seal is already completed
			if (SealReflection.IsSealCompleted(seal)) return;

			if (seasonInt == 2) {
				// Plague activates when Storm starts
				var sealGameState = SealReflection.GetSealGameState();
				if (sealGameState == null) return;

				string effectName = SealReflection.GetCurrentEffect(sealGameState);
				if (string.IsNullOrEmpty(effectName)) return;

				var effectModel = GameReflection.GetEffectModel(effectName);
				string displayName = EventReflection.GetEffectDisplayName(effectModel) ?? effectName;
				string description = EventReflection.GetEffectDescription(effectModel);
				// Strip rich text tags from description
				if (!string.IsNullOrEmpty(description))
					description = RichTextTagsRegex.Replace(description, "").Trim();

				if (!string.IsNullOrEmpty(description))
					Announce(Strings.Get("util.event.plague_activated_with_description", displayName, description));
				else
					Announce(Strings.Get("util.event.plague_activated", displayName));
			} else if (seasonInt == 0) {
				// Plague ends when Drizzle starts
				Announce(Strings.Get("util.event.plague_ended"));
			}
		}

		private void OnYearChanged(object year) {
			if (!Plugin.AnnounceYearChanged.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.year", year));
		}

		// ========================================
		// NEWCOMERS SERVICE
		// ========================================
		// OnNewcomersArrival removed - covered by game's AlertsNewcomers

		private bool SubscribeToNewcomers(object gameServices) {
			var service = EventReflection.NewcomersServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// OnNewcomersArrival - announces when newcomers arrive and are ready to be picked
			return AddSubscription(service.GetType().GetProperty("OnNewcomersArrival")?.GetValue(service), OnNewcomersArrival);
		}

		private void OnNewcomersArrival(object _) {
			if (!Plugin.AnnounceNewcomersWaiting.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.newcomers_waiting"));
		}

		// ========================================
		// VILLAGERS SERVICE (Villager Loss)
		// ========================================
		// Re-added because game's NewsService alerts depend on user's in-game alert settings

		private bool SubscribeToVillagers(object gameServices) {
			var service = EventReflection.VillagersServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// OnVillagerRemoved - fires when a villager dies or leaves
			return AddSubscription(service.GetType().GetProperty("OnVillagerRemoved")?.GetValue(service), OnVillagerRemoved);
		}

		private void OnVillagerRemoved(object villager) {
			if (!Plugin.AnnounceVillagerLost.Value) return;
			if (IsInGracePeriod()) return;

			try {
				EventReflection.EnsureVillagerReflectionCached(villager);

				// Get villager name using cached method
				string villagerName = EventReflection.VillagerGetDisplayNameMethod?.Invoke(villager, null) as string ?? Strings.Get("util.event.villager");

				// Get loss type from villager.state.lossType using cached fields
				var state = EventReflection.VillagerStateField?.GetValue(villager);
				var lossType = EventReflection.VillagerStateLossTypeField?.GetValue(state);
				string lossTypeStr = lossType?.ToString() ?? "Unknown";

				// Get reason from villager.state.lossReasonKey using cached field
				string reasonKey = EventReflection.VillagerStateLossReasonField?.GetValue(state) as string;

				string reason = "";
				if (!string.IsNullOrEmpty(reasonKey)) {
					reason = GameReflection.ResolveLocaKey(reasonKey);
				}

				string message;
				if (lossTypeStr == "Leave")
					message = Strings.Get("util.event.villager_left", villagerName);
				else if (lossTypeStr == "Exile")
					message = Strings.Get("util.event.villager_exiled", villagerName);
				else
					message = Strings.Get("util.event.villager_died", villagerName);

				if (!string.IsNullOrEmpty(reason))
					message = Strings.Get("util.event.with_reason", message, reason);

				Announce(message, GetVillagerLocation(villager));
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OnVillagerRemoved failed: {ex.Message}");
				Announce(Strings.Get("common.villager_lost"));
			}
		}

		// ========================================
		// HOSTILITY SERVICE
		// ========================================

		private bool SubscribeToHostility(object gameServices) {
			var service = EventReflection.HostilityServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			bool ok = AddSubscription(service.GetType().GetProperty("OnLevelUp")?.GetValue(service), OnHostilityLevelUp);
			ok &= AddSubscription(service.GetType().GetProperty("OnLevelDown")?.GetValue(service), OnHostilityLevelDown);
			return ok;
		}

		private void OnHostilityLevelUp(object level) {
			if (!Plugin.AnnounceHostilityLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			int lvl = level is int i ? i : -1;
			if (lvl != _lastAnnouncedHostilityLevel) {
				_lastAnnouncedHostilityLevel = lvl;
				Announce(Strings.Get("util.event.hostility_increased", lvl));
			}
		}

		private void OnHostilityLevelDown(object level) {
			if (!Plugin.AnnounceHostilityLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			int lvl = level is int i ? i : -1;
			if (lvl != _lastAnnouncedHostilityLevel) {
				_lastAnnouncedHostilityLevel = lvl;
				Announce(Strings.Get("util.event.hostility_decreased", lvl));
			}
		}

		// ========================================
		// TRADE SERVICE
		// ========================================
		// OnTraderArrived removed - covered by game's AlertsTraderArrived

		private bool SubscribeToTrade(object gameServices) {
			var service = EventReflection.TradeServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// OnTraderDepartured (note: game uses "Departured" spelling) - not covered by game alerts
			return AddSubscription(service.GetType().GetProperty("OnTraderDepartured")?.GetValue(service), OnTraderDeparted);
		}

		private void OnTraderDeparted(object traderVisit) {
			if (!Plugin.AnnounceTraderDeparted.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("common.trader_departed"));
		}

		// ========================================
		// ORDERS SERVICE
		// ========================================

		private bool SubscribeToOrders(object gameServices) {
			var service = EventReflection.OrdersServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// OnOrderStarted (new order available) and OnOrderFailed are not covered
			// by game alerts; OnOrderCompleted is immediate where the game's alert
			// has a delay.
			bool ok = AddSubscription(service.GetType().GetProperty("OnOrderStarted")?.GetValue(service), OnOrderStarted);
			ok &= AddSubscription(service.GetType().GetProperty("OnOrderCompleted")?.GetValue(service), OnOrderCompleted);
			ok &= AddSubscription(service.GetType().GetProperty("OnOrderFailed")?.GetValue(service), OnOrderFailed);
			return ok;
		}

		private void OnOrderStarted(object orderState) {
			// Notify overlay regardless of announcement settings
			OnNewOrderAvailable?.Invoke();

			if (!Plugin.AnnounceOrderAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.new_order"));
		}

		private void OnOrderCompleted(object orderState) {
			if (!Plugin.AnnounceOrderCompleted.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("common.order_completed"));
		}

		private void OnOrderFailed(object orderState) {
			if (!Plugin.AnnounceOrderFailed.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("common.order_failed"));
		}

		// ========================================
		// GLADES SERVICE
		// ========================================

		private bool SubscribeToGlades(object gameServices) {
			var service = EventReflection.GladesServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			return AddSubscription(service.GetType().GetProperty("OnGladeRevealed")?.GetValue(service), OnGladeRevealed);
		}

		private void OnGladeRevealed(object gladeState) {
			if (!Plugin.AnnounceGladeRevealed.Value) return;
			if (IsInGracePeriod()) return;

			string dangerInfo = "";
			try {
				// Get danger level from GladesService using cached method
				var gameServices = GameReflection.GetGameServices();
				var gladesService = EventReflection.GladesServiceProperty?.GetValue(gameServices);
				if (gladesService != null) {
					EventReflection.EnsureGladesGetDangerLevelCached(gladesService);

					var dangerLevel = EventReflection.GladesGetDangerLevelMethod?.Invoke(gladesService, new[] { gladeState });
					if (dangerLevel != null) {
						string level = dangerLevel.ToString();
						string localized = level switch {
							"Dangerous" => Strings.Get("handler.mapnav.glade_danger_dangerous"),
							"Forbidden" => Strings.Get("handler.mapnav.glade_danger_forbidden"),
							_ => null
						};
						if (localized != null) {
							dangerInfo = Strings.Get("util.event.glade_danger_suffix", localized);
						}
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] OnGladeRevealed danger lookup failed: {ex.Message}"); }

			Announce(Strings.Get("util.event.glade_revealed", dangerInfo), GetGladeLocation(gladeState));
		}

		// ========================================
		// REPUTATION SERVICE
		// ========================================

		private bool SubscribeToReputation(object gameServices) {
			var service = EventReflection.ReputationServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			bool ok = AddSubscription(service.GetType().GetProperty("OnReputationChanged")?.GetValue(service), OnReputationChanged);
			ok &= AddSubscription(service.GetType().GetProperty("OnGameResult")?.GetValue(service), OnGameResult);
			return ok;
		}

		private void OnReputationChanged(object reputationChange) {
			if (!Plugin.AnnounceReputationChanged.Value) return;
			if (IsInGracePeriod()) return;

			try {
				// Get the amount from the change
				var amountField = reputationChange?.GetType().GetField("amount");
				float amount = amountField != null ? (float)amountField.GetValue(reputationChange) : 0f;

				// Only announce if it's a significant change (positive or negative)
				if (Math.Abs(amount) >= 0.1f) {
					// Format to 1 decimal place
					string amountStr = Math.Abs(amount).ToString("F1");
					if (amount > 0)
						Announce(Strings.Get("util.event.reputation_gained", amountStr));
					else
						Announce(Strings.Get("util.event.reputation_lost", amountStr));
				}
			} catch {
				// Fallback
			}
		}

		private void OnGameResult(object won) {
			if (!Plugin.AnnounceGameResult.Value) return;

			bool isWon = won is bool b && b;
			if (isWon)
				Announce(Strings.Get("util.event.victory"));
			else
				Announce(Strings.Get("util.event.defeat"));
		}

		// ========================================
		// NEWS SERVICE
		// ========================================

		private bool SubscribeToNews(object gameServices) {
			var service = EventReflection.NewsServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			return AddSubscription(service.GetType().GetProperty("News")?.GetValue(service), OnNewsPublished);
		}

		private void OnNewsPublished(object newsList) {
			if (!Plugin.AnnounceGameWarnings.Value) return;
			if (IsInGracePeriod()) return;

			try {
				// newsList is List<News>
				var list = newsList as System.Collections.IList;
				if (list == null || list.Count == 0) return;

				// Check each news item and announce only new ones
				foreach (var news in list) {
					if (news == null) continue;

					var contentProperty = news.GetType().GetProperty("content");
					var content = contentProperty?.GetValue(news)?.ToString();

					if (string.IsNullOrEmpty(content)) continue;

					// Skip if already announced
					if (_announcedNews.Contains(content)) continue;
					_announcedNews.Add(content);
					_announcedNewsOrder.Enqueue(content);

					// FIFO eviction to prevent memory growth
					while (_announcedNews.Count > 50 && _announcedNewsOrder.Count > 0) {
						var oldest = _announcedNewsOrder.Dequeue();
						_announcedNews.Remove(oldest);
					}

					// Strip any rich text tags like <color>, <b>, etc.
					string cleanContent = RichTextTagsRegex.Replace(content, "");
					Announce(Strings.Get("util.event.alert", cleanContent));
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] OnNewsPublished failed: {ex.Message}"); }
		}

		// BLIGHT SERVICE - Removed, covered by game's AlertsBlight

		// ========================================
		// GAME BLACKBOARD SERVICE
		// ========================================

		private bool SubscribeToGameBlackboard() {
			var blackboard = GameReflection.GetGameBlackboardService();
			if (blackboard == null) return false;

			var blackboardType = blackboard.GetType();

			// FinishedBuildingRemoved, OnHearthDiedDown, and OnBlightCystSpawned are
			// intentionally absent - covered by the game's own alerts.
			bool ok = AddSubscription(blackboardType.GetProperty("BuildingFinished")?.GetValue(blackboard), OnBuildingFinished);
			ok &= AddSubscription(blackboardType.GetProperty("OnHearthIgnited")?.GetValue(blackboard), OnHearthIgnited);
			ok &= AddSubscription(blackboardType.GetProperty("OnHubLeveledUp")?.GetValue(blackboard), OnHearthLeveledUp);
			ok &= AddSubscription(blackboardType.GetProperty("OnHubLeveledDown")?.GetValue(blackboard), OnHearthLeveledDown);
			ok &= AddSubscription(blackboardType.GetProperty("OnHearthCorrupted")?.GetValue(blackboard), OnHearthCorrupted);
			ok &= AddSubscription(blackboardType.GetProperty("OnGoodDiscovered")?.GetValue(blackboard), OnGoodDiscovered);
			ok &= AddSubscription(blackboardType.GetProperty("OnRelicResolved")?.GetValue(blackboard), OnRelicResolved);
			ok &= AddSubscription(blackboardType.GetProperty("OnRewardChaseStarted")?.GetValue(blackboard), OnRewardChaseStarted);
			ok &= AddSubscription(blackboardType.GetProperty("OnRewardChaseEnded")?.GetValue(blackboard), OnRewardChaseEnded);
			ok &= AddSubscription(blackboardType.GetProperty("OnPortExpeditionStarted")?.GetValue(blackboard), OnPortExpeditionStarted);
			return ok;
		}

		private void OnBuildingFinished(object building) {
			if (!Plugin.AnnounceConstructionComplete.Value) return;
			if (IsInGracePeriod()) return; // Ignore events during initialization

			string buildingName = GetBuildingName(building);
			Announce(Strings.Get("util.event.construction_complete", buildingName), GetBuildingLocation(building));
		}

		/// <summary>
		/// Extract the display name from a Building object.
		/// Building has a DisplayName property that returns BuildingModel.displayName.Text
		/// </summary>
		private string GetBuildingName(object building) {
			if (building == null) return Strings.Get("common.building");

			try {
				// Try DisplayName property first (direct on Building)
				var displayNameProp = building.GetType().GetProperty("DisplayName");
				if (displayNameProp != null) {
					var name = displayNameProp.GetValue(building) as string;
					if (!string.IsNullOrEmpty(name)) return name;
				}

				// Fallback: try BuildingModel.displayName (displayName is a field, not property)
				var modelProperty = building.GetType().GetProperty("BuildingModel");
				if (modelProperty != null) {
					var model = modelProperty.GetValue(building);
					if (model != null) {
						var displayNameField = model.GetType().GetField("displayName");
						var displayName = displayNameField?.GetValue(model);
						var name = GameReflection.GetLocaText(displayName);
						if (!string.IsNullOrEmpty(name)) return name;
					}
				}
			} catch {
				// Failed to get building name, return fallback
			}

			return Strings.Get("common.building");
		}

		// OnBuildingDestroyed removed - covered by game's AlertsBuildingLoss

		private void OnHearthIgnited(object hearth) {
			if (!Plugin.AnnounceHearthIgnited.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("common.hearth_ignited"), GetBuildingLocation(hearth));
		}

		// OnHearthDied removed - covered by game's AlertsFireDown

		private void OnHearthLeveledUp(object hearth) {
			if (!Plugin.AnnounceHearthLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.hearth_leveled_up"), GetBuildingLocation(hearth));
		}

		private void OnHearthLeveledDown(object hearth) {
			if (!Plugin.AnnounceHearthLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.hearth_leveled_down"), GetBuildingLocation(hearth));
		}

		private void OnHearthCorrupted(object hearth) {
			if (!Plugin.AnnounceHearthCorrupted.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.hearth_corrupted"), GetBuildingLocation(hearth));
		}

		private void OnGoodDiscovered(object goodName) {
			if (!Plugin.AnnounceGoodDiscovered.Value) return;
			if (IsInGracePeriod()) return;

			string rawName = goodName?.ToString();
			string name = string.IsNullOrEmpty(rawName)
				? Strings.Get("common.unknown")
				: GameReflection.GetGoodDisplayName(rawName);

			Announce(Strings.Get("util.event.good_discovered", name));
		}

		// OnBlightCystSpawned removed - covered by game's AlertsBlight

		private void OnRelicResolved(object relic) {
			if (!Plugin.AnnounceRelicResolved.Value) return;
			if (IsInGracePeriod()) return;

			// Relic extends Building, so we can use GetBuildingName
			string relicName = GetBuildingName(relic);
			if (relicName == Strings.Get("common.building")) relicName = Strings.Get("common.relic"); // Fallback

			Announce(Strings.Get("util.event.relic_resolved", relicName), GetBuildingLocation(relic));
		}

		private void OnRewardChaseStarted(object gladeState) {
			if (!Plugin.AnnounceRewardChase.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.reward_chase_started"), GetGladeLocation(gladeState));
		}

		private void OnRewardChaseEnded(object gladeState) {
			if (!Plugin.AnnounceRewardChase.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.reward_chase_ended"), GetGladeLocation(gladeState));
		}

		private void OnPortExpeditionStarted(object port) {
			if (!Plugin.AnnouncePortExpeditionStarted.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("common.expedition_departed"), GetBuildingLocation(port));
		}

		// ========================================
		// LOCATE EVENTS (Grass/Springs/Relic Location Markers)
		// ========================================

		private IDisposable _grassLocationSub;
		private IDisposable _springsLocationSub;
		private IDisposable _relicLocationSub;
		private IDisposable _relicHighlightSub;

		private bool SubscribeToLocateEvents() {
			_grassLocationSub = MapReflection.SubscribeToGrassLocationRequested(OnGrassLocationRevealed);
			if (_grassLocationSub != null) _subscriptions.Add(_grassLocationSub);

			_springsLocationSub = MapReflection.SubscribeToSpringsLocationRequested(OnSpringsLocationRevealed);
			if (_springsLocationSub != null) _subscriptions.Add(_springsLocationSub);

			_relicLocationSub = MapReflection.SubscribeToRelicLocationRequested(OnRelicLocationRevealed);
			if (_relicLocationSub != null) _subscriptions.Add(_relicLocationSub);

			// Subscribe to relic highlight events (Short Range Scanner, etc)
			_relicHighlightSub = MapReflection.SubscribeToRelicsHighlightRequested(OnRelicHighlighted);
			if (_relicHighlightSub != null) _subscriptions.Add(_relicHighlightSub);

			return _grassLocationSub != null && _springsLocationSub != null
				&& _relicLocationSub != null && _relicHighlightSub != null;
		}

		private void OnGrassLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.fertile_soil_revealed"), GetLastRevealedLocation(MapReflection.GetRevealedGrassLocations()));
		}

		private void OnSpringsLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.spring_revealed"), GetLastRevealedLocation(MapReflection.GetRevealedSpringsLocations()));
		}

		private void OnRelicLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.relic_location_revealed"), GetLastRevealedLocation(MapReflection.GetRevealedRelicLocations()));
		}

		private void OnRelicHighlighted(string relicName, UnityEngine.Vector2Int position) {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;

			// Get a friendly name for the relic
			string friendlyName = GameReflection.GetRelicDisplayName(relicName);
			Announce(Strings.Get("util.event.relic_highlighted", friendlyName), (Vector2Int?)position);
		}

		// ========================================
		// REPUTATION REWARDS SERVICE (Blueprints)
		// ========================================

		private bool SubscribeToReputationRewards(object gameServices) {
			var service = EventReflection.ReputationRewardsServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// PickPopupRequested - fires when the blueprint pick popup is requested
			return AddSubscription(service.GetType().GetProperty("PickPopupRequested")?.GetValue(service), OnBlueprintPickRequested);
		}

		private void OnBlueprintPickRequested(object _) {
			if (!Plugin.AnnounceBlueprintAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.new_blueprint"));
		}

		// ========================================
		// CORNERSTONES SERVICE
		// ========================================

		private bool SubscribeToCornerstones(object gameServices) {
			var service = EventReflection.CornerstonesServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			// OnPicksChanged - fires when cornerstone picks become available
			return AddSubscription(service.GetType().GetProperty("OnPicksChanged")?.GetValue(service), OnCornerstonePicksChanged);
		}

		private void OnCornerstonePicksChanged(object _) {
			if (!Plugin.AnnounceCornerstoneAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce(Strings.Get("util.event.new_cornerstone"));
		}

		// ========================================
		// MONITORS SERVICE (Game's Built-in Alerts)
		// ========================================

		private bool SubscribeToMonitors(object gameServices) {
			var service = EventReflection.MonitorsServiceProperty?.GetValue(gameServices);
			if (service == null) return false;

			return AddSubscription(service.GetType().GetProperty("Alerts")?.GetValue(service), OnAlertsChanged);
		}

		private void OnAlertsChanged(object alertsList) {
			if (!Plugin.AnnounceGameAlerts.Value) return;
			if (IsInGracePeriod()) return;

			try {
				var list = alertsList as System.Collections.IList;
				if (list == null || list.Count == 0) return;

				// Check each alert and announce new ones
				foreach (var alert in list) {
					if (alert == null) continue;

					// Cache alert field metadata on first use
					EventReflection.EnsureAlertFieldsCached(alert);

					string text = EventReflection.AlertTextField?.GetValue(alert) as string;
					bool dismissed = EventReflection.AlertDismissedField != null && (bool)EventReflection.AlertDismissedField.GetValue(alert);
					float showTime = EventReflection.AlertShowTimeField != null ? (float)EventReflection.AlertShowTimeField.GetValue(alert) : 0f;

					if (string.IsNullOrEmpty(text) || dismissed) continue;

					// Create a unique key for this alert (text + showTime to handle same text at different times)
					string alertKey = $"{text}_{showTime:F2}";

					// Only announce if we haven't already announced this specific alert
					if (!_announcedAlerts.Contains(alertKey)) {
						_announcedAlerts.Add(alertKey);
						_announcedAlertsOrder.Enqueue(alertKey);

						// Evict oldest alerts to prevent memory growth
						// Keep HashSet and Queue in sync by checking both
						while (_announcedAlerts.Count > 100 && _announcedAlertsOrder.Count > 0) {
							var oldest = _announcedAlertsOrder.Dequeue();
							_announcedAlerts.Remove(oldest);
						}

						// Strip any rich text tags
						text = RichTextTagsRegex.Replace(text, "");

						// Strip "Alert:" or "Alert" prefix if game already includes it
						text = AlertPrefixRegex.Replace(text, "");

						Announce(Strings.Get("util.event.alert", text), TryGetAlertBuildingLocation(alert));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OnAlertsChanged error: {ex.Message}");
			}
		}

		// ========================================
		// SACRIFICE STOPPED (Harmony Patch)
		// ========================================
		//
		// Note: Sacrifice detection uses asymmetric approach:
		// - Sacrifice STARTED: Detected via HearthNavigator UI action (immediate feedback)
		// - Sacrifice STOPPED: Detected via Harmony patch on HearthView.UpdateSacrificeStatus
		//   (because sacrifice can stop from goods depletion, not just UI action)
		//
		// This asymmetry is intentional - UI actions have immediate feedback, while
		// automatic stops need to be detected via the game's internal state changes.

		// Track sacrifice state per HearthView instance to detect when it stops.
		// Note: Uses GetHashCode as key which can theoretically collide, but this is
		// acceptable for UI tracking where collisions are rare and consequences minor.
		private static Dictionary<int, bool> _hearthSacrificeStates = new Dictionary<int, bool>();
		private static EventAnnouncer _instance;

		/// <summary>
		/// Register the Harmony patch for HearthView.UpdateSacrificeStatus.
		/// Called from Plugin after Harmony.PatchAll().
		/// </summary>
		public static void RegisterSacrificeStoppedPatch(Harmony harmony) {
			try {
				var assembly = GameReflection.GameAssembly;
				if (assembly == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register sacrifice patch - game assembly not found");
					return;
				}

				var hearthViewType = assembly.GetType("Eremite.Buildings.HearthView");
				if (hearthViewType == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register sacrifice patch - HearthView type not found");
					return;
				}

				var targetMethod = hearthViewType.GetMethod("UpdateSacrificeStatus", BindingFlags.Public | BindingFlags.Instance);
				if (targetMethod == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register sacrifice patch - UpdateSacrificeStatus method not found");
					return;
				}

				var postfixMethod = typeof(EventAnnouncer).GetMethod(nameof(UpdateSacrificeStatusPostfix), BindingFlags.Static | BindingFlags.NonPublic);
				if (postfixMethod == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register sacrifice patch - postfix method not found");
					return;
				}

				harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
				Debug.Log("[ATSAccessibility] Registered HearthView.UpdateSacrificeStatus patch for sacrifice stopped announcements");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to register sacrifice patch: {ex.Message}");
			}
		}

		/// <summary>
		/// Postfix for HearthView.UpdateSacrificeStatus(bool isOn).
		/// Announces when sacrifice stops (transitions from on to off).
		/// </summary>
		private static void UpdateSacrificeStatusPostfix(object __instance, bool isOn) {
			try {
				if (!Plugin.AnnounceSacrificeStopped.Value) return;

				// Use instance hash code as key
				int key = __instance.GetHashCode();

				// Check if we have a previous state
				if (_hearthSacrificeStates.TryGetValue(key, out bool wasOn)) {
					// Detect transition from on to off
					if (wasOn && !isOn) {
						// Sacrifice stopped - announce it
						if (_instance != null && !_instance.IsInGracePeriod()) {
							_instance.Announce(Strings.Get("common.sacrifice_stopped"));
						}
					}
				}

				// Update state
				_hearthSacrificeStates[key] = isOn;

				// Leak backstop only. Entries are bounded by hearth count within a
				// settlement and cleared on scene unload; the threshold is set far
				// above any real settlement so it never thrashes tracked state
				// (clearing wholesale re-baselines every building and can eat a
				// transition that lands right after the eviction).
				if (_hearthSacrificeStates.Count > 1000) {
					_hearthSacrificeStates.Clear();
					_hearthSacrificeStates[key] = isOn;
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UpdateSacrificeStatusPostfix error: {ex.Message}");
			}
		}

		/// <summary>
		/// Set the singleton instance for announcements from static patch methods.
		/// </summary>
		public void SetInstance() {
			_instance = this;
		}

		/// <summary>
		/// Clear sacrifice tracking state. Called on dispose.
		/// </summary>
		public static void ClearSacrificeState() {
			_hearthSacrificeStates.Clear();
		}

		// ========================================
		// BUILDING IDLE (Harmony Patch)
		// ========================================
		//
		// Detects when production buildings become idle (all workers idle).
		// Excludes building types that have dedicated game monitors (Camp, Mine,
		// FishingHut, GathererHut, Farm) which already generate their own alerts.

		private static Dictionary<int, bool> _buildingIdleStates = new Dictionary<int, bool>();
		private static HashSet<Type> _monitoredBuildingTypes;
		private static PropertyInfo _isIdleProp;
		private static PropertyInfo _displayNameProp;

		/// <summary>
		/// Register the Harmony patch for ProductionBuilding.UpdateIdleStatus.
		/// Called from Plugin after Harmony.PatchAll().
		/// </summary>
		public static void RegisterBuildingIdlePatch(Harmony harmony) {
			try {
				var assembly = GameReflection.GameAssembly;
				if (assembly == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register building idle patch - game assembly not found");
					return;
				}

				var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
				if (productionBuildingType == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register building idle patch - ProductionBuilding type not found");
					return;
				}

				var targetMethod = productionBuildingType.GetMethod("UpdateIdleStatus", BindingFlags.NonPublic | BindingFlags.Instance);
				if (targetMethod == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register building idle patch - UpdateIdleStatus method not found");
					return;
				}

				// Cache IsIdle and DisplayName properties
				_isIdleProp = productionBuildingType.GetProperty("IsIdle", BindingFlags.Public | BindingFlags.Instance);
				_displayNameProp = productionBuildingType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);

				// Cache monitored building types (these have game monitors that already alert on idle)
				_monitoredBuildingTypes = new HashSet<Type>();
				string[] monitoredTypeNames = {
					"Eremite.Buildings.Camp",
					"Eremite.Buildings.Mine",
					"Eremite.Buildings.FishingHut",
					"Eremite.Buildings.GathererHut",
					"Eremite.Buildings.Farm"
				};
				foreach (var typeName in monitoredTypeNames) {
					var type = assembly.GetType(typeName);
					if (type != null) _monitoredBuildingTypes.Add(type);
				}

				var postfixMethod = typeof(EventAnnouncer).GetMethod(nameof(UpdateIdleStatusPostfix), BindingFlags.Static | BindingFlags.NonPublic);
				if (postfixMethod == null) {
					Debug.LogWarning("[ATSAccessibility] Cannot register building idle patch - postfix method not found");
					return;
				}

				harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
				Debug.Log("[ATSAccessibility] Registered ProductionBuilding.UpdateIdleStatus patch for building idle announcements");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Failed to register building idle patch: {ex.Message}");
			}
		}

		/// <summary>
		/// Postfix for ProductionBuilding.UpdateIdleStatus().
		/// Announces when a building becomes idle (transitions from not-idle to idle).
		/// Excludes building types with dedicated game monitors.
		/// </summary>
		private static void UpdateIdleStatusPostfix(object __instance) {
			try {
				if (!Plugin.AnnounceBuildingIdle.Value) return;
				if (_isIdleProp == null || _monitoredBuildingTypes == null) return;

				// Skip building types that have dedicated game monitors
				if (_monitoredBuildingTypes.Contains(__instance.GetType())) return;

				int key = __instance.GetHashCode();
				bool isIdle = (bool)_isIdleProp.GetValue(__instance);

				// Check for false → true transition
				if (_buildingIdleStates.TryGetValue(key, out bool wasIdle)) {
					if (!wasIdle && isIdle) {
						if (_instance != null && !_instance.IsInGracePeriod()) {
							string name = _displayNameProp?.GetValue(__instance) as string ?? Strings.Get("common.building");
							_instance.Announce(Strings.Get("util.event.building_idle", name), GetBuildingLocation(__instance));
						}
					}
				}

				_buildingIdleStates[key] = isIdle;

				// Leak backstop only. Entries are bounded by production-building
				// count within a settlement and cleared on scene unload; a low
				// threshold thrashed every frame in 50+ building settlements,
				// re-baselining all state and eating idle transitions.
				if (_buildingIdleStates.Count > 1000) {
					_buildingIdleStates.Clear();
					_buildingIdleStates[key] = isIdle;
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UpdateIdleStatusPostfix error: {ex.Message}");
			}
		}

		/// <summary>
		/// Clear building idle tracking state. Called on dispose.
		/// </summary>
		public static void ClearBuildingIdleState() {
			_buildingIdleStates.Clear();
		}
	}
}
