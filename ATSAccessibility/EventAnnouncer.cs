using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ATSAccessibility {
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
		public void TrySubscribe() {
			if (_subscribed) return;
			if (!GameReflection.GetIsGameActive()) return;

			try {
				EventReflection.EnsureReflectionCached();

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
				SubscribeToLocateEvents();

				_subscribed = true;
				_gracePeriodEndTime = Time.realtimeSinceStartup + INITIALIZATION_GRACE_PERIOD;
				SetInstance();  // Set instance for static patch callbacks
				Debug.Log("[ATSAccessibility] EventAnnouncer: Subscribed to game events");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] EventAnnouncer subscription failed: {ex.Message}");
			}
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

				// Clear highlighted relics tracking
				GameReflection.ClearHighlightedRelics();
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
			var pos = GameReflection.GetBuildingGridPosition(building);
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
					message == "New blueprint available to pick") {
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
				string formatted = count > 1 ? $"{message} x{count}" : message;
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

		private void SubscribeToCalendar(object gameServices) {
			var service = EventReflection.CalendarServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnSeasonChanged
			var onSeasonChanged = service.GetType().GetProperty("OnSeasonChanged")?.GetValue(service);
			if (onSeasonChanged != null) {
				var sub = GameReflection.SubscribeToObservable(onSeasonChanged, OnSeasonChanged);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnYearChanged
			var onYearChanged = service.GetType().GetProperty("OnYearChanged")?.GetValue(service);
			if (onYearChanged != null) {
				var sub = GameReflection.SubscribeToObservable(onYearChanged, OnYearChanged);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnSeasonChanged(object season) {
			if (IsInGracePeriod()) return;

			string seasonName = season?.ToString() ?? "Unknown";

			// Announce season change if enabled
			if (Plugin.AnnounceSeasonChanged.Value) {
				Announce($"Season changed to {seasonName}");
			}

			// Check for Sealed Forest plague events
			if (Plugin.AnnouncePlagueEvents.Value) {
				AnnouncePlagueEvent(seasonName);
			}
		}

		/// <summary>
		/// Announce plague activation/end for Sealed Forest biome.
		/// </summary>
		private void AnnouncePlagueEvent(string seasonName) {
			// Check if we're in Sealed Forest (seals exist)
			var seal = SealReflection.GetFirstSeal();
			if (seal == null) return;

			// Don't announce if seal is already completed
			if (SealReflection.IsSealCompleted(seal)) return;

			if (seasonName == "Storm") {
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
					Announce($"Plague activated: {displayName}. {description}");
				else
					Announce($"Plague activated: {displayName}");
			} else if (seasonName == "Drizzle") {
				// Plague ends when Drizzle starts
				Announce("Plague ended");
			}
		}

		private void OnYearChanged(object year) {
			if (!Plugin.AnnounceYearChanged.Value) return;
			if (IsInGracePeriod()) return;
			Announce($"Year {year}");
		}

		// ========================================
		// NEWCOMERS SERVICE
		// ========================================
		// OnNewcomersArrival removed - covered by game's AlertsNewcomers

		private void SubscribeToNewcomers(object gameServices) {
			var service = EventReflection.NewcomersServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnNewcomersArrival - announces when newcomers arrive and are ready to be picked
			var onNewcomersArrival = service.GetType().GetProperty("OnNewcomersArrival")?.GetValue(service);
			if (onNewcomersArrival != null) {
				var sub = GameReflection.SubscribeToObservable(onNewcomersArrival, OnNewcomersArrival);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnNewcomersArrival(object _) {
			if (!Plugin.AnnounceNewcomersWaiting.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Newcomers waiting to be picked");
		}

		// ========================================
		// VILLAGERS SERVICE (Villager Loss)
		// ========================================
		// Re-added because game's NewsService alerts depend on user's in-game alert settings

		private void SubscribeToVillagers(object gameServices) {
			var service = EventReflection.VillagersServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnVillagerRemoved - fires when a villager dies or leaves
			var onVillagerRemoved = service.GetType().GetProperty("OnVillagerRemoved")?.GetValue(service);
			if (onVillagerRemoved != null) {
				var sub = GameReflection.SubscribeToObservable(onVillagerRemoved, OnVillagerRemoved);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnVillagerRemoved(object villager) {
			if (!Plugin.AnnounceVillagerLost.Value) return;
			if (IsInGracePeriod()) return;

			try {
				EventReflection.EnsureVillagerReflectionCached(villager);

				// Get villager name using cached method
				string villagerName = EventReflection.VillagerGetDisplayNameMethod?.Invoke(villager, null) as string ?? "Villager";

				// Get loss type from villager.state.lossType using cached fields
				var state = EventReflection.VillagerStateField?.GetValue(villager);
				var lossType = EventReflection.VillagerStateLossTypeField?.GetValue(state);
				string lossTypeStr = lossType?.ToString() ?? "Unknown";

				// Get reason from villager.state.lossReasonKey using cached field
				string reasonKey = EventReflection.VillagerStateLossReasonField?.GetValue(state) as string;

				string reason = "";
				if (!string.IsNullOrEmpty(reasonKey)) {
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

				Announce(message, GetVillagerLocation(villager));
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OnVillagerRemoved failed: {ex.Message}");
				Announce("Villager lost");
			}
		}

		// ========================================
		// HOSTILITY SERVICE
		// ========================================

		private void SubscribeToHostility(object gameServices) {
			var service = EventReflection.HostilityServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnLevelUp
			var onLevelUp = service.GetType().GetProperty("OnLevelUp")?.GetValue(service);
			if (onLevelUp != null) {
				var sub = GameReflection.SubscribeToObservable(onLevelUp, OnHostilityLevelUp);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnLevelDown
			var onLevelDown = service.GetType().GetProperty("OnLevelDown")?.GetValue(service);
			if (onLevelDown != null) {
				var sub = GameReflection.SubscribeToObservable(onLevelDown, OnHostilityLevelDown);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnHostilityLevelUp(object level) {
			if (!Plugin.AnnounceHostilityLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			int lvl = level is int i ? i : -1;
			if (lvl != _lastAnnouncedHostilityLevel) {
				_lastAnnouncedHostilityLevel = lvl;
				Announce($"Hostility increased to level {lvl}");
			}
		}

		private void OnHostilityLevelDown(object level) {
			if (!Plugin.AnnounceHostilityLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			int lvl = level is int i ? i : -1;
			if (lvl != _lastAnnouncedHostilityLevel) {
				_lastAnnouncedHostilityLevel = lvl;
				Announce($"Hostility decreased to level {lvl}");
			}
		}

		// ========================================
		// TRADE SERVICE
		// ========================================
		// OnTraderArrived removed - covered by game's AlertsTraderArrived

		private void SubscribeToTrade(object gameServices) {
			var service = EventReflection.TradeServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnTraderDepartured (note: game uses "Departured" spelling) - not covered by game alerts
			var onTraderDeparted = service.GetType().GetProperty("OnTraderDepartured")?.GetValue(service);
			if (onTraderDeparted != null) {
				var sub = GameReflection.SubscribeToObservable(onTraderDeparted, OnTraderDeparted);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnTraderDeparted(object traderVisit) {
			if (!Plugin.AnnounceTraderDeparted.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Trader departed");
		}

		// ========================================
		// ORDERS SERVICE
		// ========================================

		private void SubscribeToOrders(object gameServices) {
			var service = EventReflection.OrdersServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnOrderStarted (new order available) - not covered by game alerts
			var onOrderStarted = service.GetType().GetProperty("OnOrderStarted")?.GetValue(service);
			if (onOrderStarted != null) {
				var sub = GameReflection.SubscribeToObservable(onOrderStarted, OnOrderStarted);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnOrderCompleted - game's alert has a delay, this is immediate
			var onOrderCompleted = service.GetType().GetProperty("OnOrderCompleted")?.GetValue(service);
			if (onOrderCompleted != null) {
				var sub = GameReflection.SubscribeToObservable(onOrderCompleted, OnOrderCompleted);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnOrderFailed - not covered by game alerts
			var onOrderFailed = service.GetType().GetProperty("OnOrderFailed")?.GetValue(service);
			if (onOrderFailed != null) {
				var sub = GameReflection.SubscribeToObservable(onOrderFailed, OnOrderFailed);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnOrderStarted(object orderState) {
			// Notify overlay regardless of announcement settings
			OnNewOrderAvailable?.Invoke();

			if (!Plugin.AnnounceOrderAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce("New order available");
		}

		private void OnOrderCompleted(object orderState) {
			if (!Plugin.AnnounceOrderCompleted.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Order completed");
		}

		private void OnOrderFailed(object orderState) {
			if (!Plugin.AnnounceOrderFailed.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Order failed");
		}

		// ========================================
		// GLADES SERVICE
		// ========================================

		private void SubscribeToGlades(object gameServices) {
			var service = EventReflection.GladesServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnGladeRevealed
			var onGladeRevealed = service.GetType().GetProperty("OnGladeRevealed")?.GetValue(service);
			if (onGladeRevealed != null) {
				var sub = GameReflection.SubscribeToObservable(onGladeRevealed, OnGladeRevealed);
				if (sub != null) _subscriptions.Add(sub);
			}
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
						if (level != "None" && level != "Safe") {
							dangerInfo = $", {level} danger";
						}
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] OnGladeRevealed danger lookup failed: {ex.Message}"); }

			Announce($"Glade revealed{dangerInfo}", GetGladeLocation(gladeState));
		}

		// ========================================
		// REPUTATION SERVICE
		// ========================================

		private void SubscribeToReputation(object gameServices) {
			var service = EventReflection.ReputationServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnReputationChanged
			var onReputationChanged = service.GetType().GetProperty("OnReputationChanged")?.GetValue(service);
			if (onReputationChanged != null) {
				var sub = GameReflection.SubscribeToObservable(onReputationChanged, OnReputationChanged);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnGameResult
			var onGameResult = service.GetType().GetProperty("OnGameResult")?.GetValue(service);
			if (onGameResult != null) {
				var sub = GameReflection.SubscribeToObservable(onGameResult, OnGameResult);
				if (sub != null) _subscriptions.Add(sub);
			}
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
						Announce($"Reputation gained: {amountStr}");
					else
						Announce($"Reputation lost: {amountStr}");
				}
			} catch {
				// Fallback
			}
		}

		private void OnGameResult(object won) {
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

		private void SubscribeToNews(object gameServices) {
			var service = EventReflection.NewsServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// News
			var newsObservable = service.GetType().GetProperty("News")?.GetValue(service);
			if (newsObservable != null) {
				var sub = GameReflection.SubscribeToObservable(newsObservable, OnNewsPublished);
				if (sub != null) _subscriptions.Add(sub);
			}
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
					Announce($"Alert: {cleanContent}");
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] OnNewsPublished failed: {ex.Message}"); }
		}

		// BLIGHT SERVICE - Removed, covered by game's AlertsBlight

		// ========================================
		// GAME BLACKBOARD SERVICE
		// ========================================

		private void SubscribeToGameBlackboard() {
			var blackboard = GameReflection.GetGameBlackboardService();
			if (blackboard == null) return;

			var blackboardType = blackboard.GetType();

			// BuildingFinished
			var buildingFinishedProp = blackboardType.GetProperty("BuildingFinished");
			if (buildingFinishedProp != null) {
				var buildingFinished = buildingFinishedProp.GetValue(blackboard);
				if (buildingFinished != null) {
					var sub = GameReflection.SubscribeToObservable(buildingFinished, OnBuildingFinished);
					if (sub != null) _subscriptions.Add(sub);
				}
			}

			// FinishedBuildingRemoved removed - covered by game's AlertsBuildingLoss

			// OnHearthIgnited
			var hearthIgnited = blackboardType.GetProperty("OnHearthIgnited")?.GetValue(blackboard);
			if (hearthIgnited != null) {
				var sub = GameReflection.SubscribeToObservable(hearthIgnited, OnHearthIgnited);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnHearthDiedDown removed - covered by game's AlertsFireDown

			// OnHubLeveledUp
			var hubLeveledUp = blackboardType.GetProperty("OnHubLeveledUp")?.GetValue(blackboard);
			if (hubLeveledUp != null) {
				var sub = GameReflection.SubscribeToObservable(hubLeveledUp, OnHearthLeveledUp);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnHubLeveledDown
			var hubLeveledDown = blackboardType.GetProperty("OnHubLeveledDown")?.GetValue(blackboard);
			if (hubLeveledDown != null) {
				var sub = GameReflection.SubscribeToObservable(hubLeveledDown, OnHearthLeveledDown);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnHearthCorrupted
			var hearthCorrupted = blackboardType.GetProperty("OnHearthCorrupted")?.GetValue(blackboard);
			if (hearthCorrupted != null) {
				var sub = GameReflection.SubscribeToObservable(hearthCorrupted, OnHearthCorrupted);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnGoodDiscovered
			var goodDiscovered = blackboardType.GetProperty("OnGoodDiscovered")?.GetValue(blackboard);
			if (goodDiscovered != null) {
				var sub = GameReflection.SubscribeToObservable(goodDiscovered, OnGoodDiscovered);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnBlightCystSpawned removed - covered by game's AlertsBlight

			// OnRelicResolved
			var relicResolved = blackboardType.GetProperty("OnRelicResolved")?.GetValue(blackboard);
			if (relicResolved != null) {
				var sub = GameReflection.SubscribeToObservable(relicResolved, OnRelicResolved);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnRewardChaseStarted
			var chaseStarted = blackboardType.GetProperty("OnRewardChaseStarted")?.GetValue(blackboard);
			if (chaseStarted != null) {
				var sub = GameReflection.SubscribeToObservable(chaseStarted, OnRewardChaseStarted);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnRewardChaseEnded
			var chaseEnded = blackboardType.GetProperty("OnRewardChaseEnded")?.GetValue(blackboard);
			if (chaseEnded != null) {
				var sub = GameReflection.SubscribeToObservable(chaseEnded, OnRewardChaseEnded);
				if (sub != null) _subscriptions.Add(sub);
			}

			// OnPortExpeditionStarted
			var portExpeditionStarted = blackboardType.GetProperty("OnPortExpeditionStarted")?.GetValue(blackboard);
			if (portExpeditionStarted != null) {
				var sub = GameReflection.SubscribeToObservable(portExpeditionStarted, OnPortExpeditionStarted);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnBuildingFinished(object building) {
			if (!Plugin.AnnounceConstructionComplete.Value) return;
			if (IsInGracePeriod()) return; // Ignore events during initialization

			string buildingName = GetBuildingName(building);
			Announce($"{buildingName} construction complete", GetBuildingLocation(building));
		}

		/// <summary>
		/// Extract the display name from a Building object.
		/// Building has a DisplayName property that returns BuildingModel.displayName.Text
		/// </summary>
		private string GetBuildingName(object building) {
			if (building == null) return "Building";

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

			return "Building";
		}

		// OnBuildingDestroyed removed - covered by game's AlertsBuildingLoss

		private void OnHearthIgnited(object hearth) {
			if (!Plugin.AnnounceHearthIgnited.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Hearth ignited", GetBuildingLocation(hearth));
		}

		// OnHearthDied removed - covered by game's AlertsFireDown

		private void OnHearthLeveledUp(object hearth) {
			if (!Plugin.AnnounceHearthLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Hearth leveled up", GetBuildingLocation(hearth));
		}

		private void OnHearthLeveledDown(object hearth) {
			if (!Plugin.AnnounceHearthLevelChange.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Hearth leveled down", GetBuildingLocation(hearth));
		}

		private void OnHearthCorrupted(object hearth) {
			if (!Plugin.AnnounceHearthCorrupted.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Hearth corrupted by blight", GetBuildingLocation(hearth));
		}

		private void OnGoodDiscovered(object goodName) {
			if (!Plugin.AnnounceGoodDiscovered.Value) return;
			if (IsInGracePeriod()) return;

			string name = goodName?.ToString() ?? "Unknown";
			// Try to get the display name from settings
			try {
				var settings = GameReflection.GetSettings();
				if (settings != null) {
					var getGoodMethod = settings.GetType().GetMethod("GetGood");
					var good = getGoodMethod?.Invoke(settings, new[] { goodName });
					if (good != null) {
						// GoodModel.displayName is a field, not a property
						var displayNameField = good.GetType().GetField("displayName");
						var displayName = displayNameField?.GetValue(good);
						name = GameReflection.GetLocaText(displayName) ?? name;
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] OnGoodDiscovered name lookup failed: {ex.Message}"); }

			Announce($"New good discovered: {name}");
		}

		// OnBlightCystSpawned removed - covered by game's AlertsBlight

		private void OnRelicResolved(object relic) {
			if (!Plugin.AnnounceRelicResolved.Value) return;
			if (IsInGracePeriod()) return;

			// Relic extends Building, so we can use GetBuildingName
			string relicName = GetBuildingName(relic);
			if (relicName == "Building") relicName = "Relic"; // Fallback

			Announce($"{relicName} resolved", GetBuildingLocation(relic));
		}

		private void OnRewardChaseStarted(object gladeState) {
			if (!Plugin.AnnounceRewardChase.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Reward chase started", GetGladeLocation(gladeState));
		}

		private void OnRewardChaseEnded(object gladeState) {
			if (!Plugin.AnnounceRewardChase.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Reward chase ended", GetGladeLocation(gladeState));
		}

		private void OnPortExpeditionStarted(object port) {
			if (!Plugin.AnnouncePortExpeditionStarted.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Expedition departed", GetBuildingLocation(port));
		}

		// ========================================
		// LOCATE EVENTS (Grass/Springs/Relic Location Markers)
		// ========================================

		private IDisposable _grassLocationSub;
		private IDisposable _springsLocationSub;
		private IDisposable _relicLocationSub;
		private IDisposable _relicHighlightSub;

		private void SubscribeToLocateEvents() {
			_grassLocationSub = GameReflection.SubscribeToGrassLocationRequested(OnGrassLocationRevealed);
			if (_grassLocationSub != null) _subscriptions.Add(_grassLocationSub);

			_springsLocationSub = GameReflection.SubscribeToSpringsLocationRequested(OnSpringsLocationRevealed);
			if (_springsLocationSub != null) _subscriptions.Add(_springsLocationSub);

			_relicLocationSub = GameReflection.SubscribeToRelicLocationRequested(OnRelicLocationRevealed);
			if (_relicLocationSub != null) _subscriptions.Add(_relicLocationSub);

			// Subscribe to relic highlight events (Short Range Scanner, etc)
			_relicHighlightSub = GameReflection.SubscribeToRelicsHighlightRequested(OnRelicHighlighted);
			if (_relicHighlightSub != null) _subscriptions.Add(_relicHighlightSub);
		}

		private void OnGrassLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Fertile soil location revealed", GetLastRevealedLocation(GameReflection.GetRevealedGrassLocations()));
		}

		private void OnSpringsLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Spring location revealed", GetLastRevealedLocation(GameReflection.GetRevealedSpringsLocations()));
		}

		private void OnRelicLocationRevealed() {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;
			Announce("Relic location revealed", GetLastRevealedLocation(GameReflection.GetRevealedRelicLocations()));
		}

		private void OnRelicHighlighted(string relicName, UnityEngine.Vector2Int position) {
			if (!Plugin.AnnounceLocateMarkers.Value) return;
			if (IsInGracePeriod()) return;

			// Get a friendly name for the relic
			string friendlyName = GameReflection.GetRelicDisplayName(relicName);
			Announce($"Relic highlighted: {friendlyName}", (Vector2Int?)position);
		}

		// ========================================
		// REPUTATION REWARDS SERVICE (Blueprints)
		// ========================================

		private void SubscribeToReputationRewards(object gameServices) {
			var service = EventReflection.ReputationRewardsServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// PickPopupRequested - fires when the blueprint pick popup is requested
			var pickPopupRequested = service.GetType().GetProperty("PickPopupRequested")?.GetValue(service);
			if (pickPopupRequested != null) {
				var sub = GameReflection.SubscribeToObservable(pickPopupRequested, OnBlueprintPickRequested);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnBlueprintPickRequested(object _) {
			if (!Plugin.AnnounceBlueprintAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce("New blueprint available to pick");
		}

		// ========================================
		// CORNERSTONES SERVICE
		// ========================================

		private void SubscribeToCornerstones(object gameServices) {
			var service = EventReflection.CornerstonesServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// OnPicksChanged - fires when cornerstone picks become available
			var onPicksChanged = service.GetType().GetProperty("OnPicksChanged")?.GetValue(service);
			if (onPicksChanged != null) {
				var sub = GameReflection.SubscribeToObservable(onPicksChanged, OnCornerstonePicksChanged);
				if (sub != null) _subscriptions.Add(sub);
			}
		}

		private void OnCornerstonePicksChanged(object _) {
			if (!Plugin.AnnounceCornerstoneAvailable.Value) return;
			if (IsInGracePeriod()) return;
			Announce("New cornerstone available to pick");
		}

		// ========================================
		// MONITORS SERVICE (Game's Built-in Alerts)
		// ========================================

		private void SubscribeToMonitors(object gameServices) {
			var service = EventReflection.MonitorsServiceProperty?.GetValue(gameServices);
			if (service == null) return;

			// Subscribe to Alerts observable
			var alertsObservable = service.GetType().GetProperty("Alerts")?.GetValue(service);
			if (alertsObservable != null) {
				var sub = GameReflection.SubscribeToObservable(alertsObservable, OnAlertsChanged);
				if (sub != null) _subscriptions.Add(sub);
			}
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

						Announce($"Alert: {text}", TryGetAlertBuildingLocation(alert));
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
							_instance.Announce("Sacrifice stopped");
						}
					}
				}

				// Update state
				_hearthSacrificeStates[key] = isOn;

				// Cleanup old entries if too many (prevents memory leak)
				if (_hearthSacrificeStates.Count > 50) {
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
	}
}
