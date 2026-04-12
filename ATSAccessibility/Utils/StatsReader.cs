using ATSAccessibility.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Provides access to game statistics (Reputation, Impatience, Hostility, Resolve)
	/// via reflection and announces them via speech.
	/// </summary>
	public static class StatsReader {
		// Reusable object array for single-argument method invocations (avoid allocations in loops)
		private static readonly object[] _singleArgArray = new object[1];

		// Species cycling state for V key
		private static int _currentSpeciesIndex = 0;
		private static List<string> _cachedPresentRaces = null;

		/// <summary>Reset species cycling state between games.</summary>
		public static void ResetSpeciesCycling() {
			_currentSpeciesIndex = 0;
			_cachedPresentRaces = null;
		}

		// ========================================
		// DATA ACCESS METHODS
		// ========================================

		/// <summary>
		/// Get reputation summary as (current, target).
		/// </summary>
		public static (float current, int target) GetReputationSummary() {
			StatsReflection.EnsureCached();

			var repService = GameReflection.GetReputationService();
			if (repService == null) return (0, 0);

			try {
				// Get Reputation.Value from ReactiveProperty
				var repProp = StatsReflection.RepReputationProperty?.GetValue(repService);
				float current = 0;
				if (repProp != null) {
					var valueProp = repProp.GetType().GetProperty("Value");
					current = (float)(valueProp?.GetValue(repProp) ?? 0f);
				}

				// Get target
				int target = (int)(StatsReflection.RepGetToWinMethod?.Invoke(repService, null) ?? 0);

				return (current, target);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetReputationSummary failed: {ex.Message}");
				return (0, 0);
			}
		}

		/// <summary>
		/// Get impatience (reputation penalty) summary as (current, max).
		/// </summary>
		public static (float current, int max) GetImpatienceSummary() {
			StatsReflection.EnsureCached();

			var repService = GameReflection.GetReputationService();
			if (repService == null) return (0, 0);

			try {
				// Get ReputationPenalty.Value from ReactiveProperty
				var penaltyProp = StatsReflection.RepPenaltyProperty?.GetValue(repService);
				float current = 0;
				if (penaltyProp != null) {
					var valueProp = penaltyProp.GetType().GetProperty("Value");
					current = (float)(valueProp?.GetValue(penaltyProp) ?? 0f);
				}

				// Get max
				int max = (int)(StatsReflection.RepGetPenaltyToLooseMethod?.Invoke(repService, null) ?? 0);

				return (current, max);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetImpatienceSummary failed: {ex.Message}");
				return (0, 0);
			}
		}

		/// <summary>
		/// Format a float to 2 decimal places, stripping trailing zeros.
		/// E.g., 7.50 -> "7.5", 7.25 -> "7.25", 7.00 -> "7"
		/// </summary>
		private static string FormatDecimal(float value) {
			return value.ToString("0.##");
		}

		/// <summary>
		/// Get hostility points, level, and points to next level.
		/// </summary>
		public static (int points, int level, int pointsToNext) GetHostilitySummary() {
			StatsReflection.EnsureCached();

			var hostService = GameReflection.GetHostilityService();
			if (hostService == null) return (0, 0, 0);

			try {
				// Get Points.Value from ReactiveProperty
				var pointsProp = StatsReflection.HostPointsProperty?.GetValue(hostService);
				int points = 0;
				if (pointsProp != null) {
					var valueProp = pointsProp.GetType().GetProperty("Value");
					points = (int)(valueProp?.GetValue(pointsProp) ?? 0);
				}

				// Get Level.Value from ReactiveProperty
				var levelProp = StatsReflection.HostLevelProperty?.GetValue(hostService);
				int level = 0;
				if (levelProp != null) {
					var valueProp = levelProp.GetType().GetProperty("Value");
					level = (int)(valueProp?.GetValue(levelProp) ?? 0);
				}

				// Get points left to next level
				int pointsToNext = 0;
				if (StatsReflection.HostGetPointsLeftToNextLevelMethod != null) {
					var result = StatsReflection.HostGetPointsLeftToNextLevelMethod.Invoke(hostService, null);
					pointsToNext = result is int p ? p : 0;
				}

				return (points, level, pointsToNext);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHostilitySummary failed: {ex.Message}");
				return (0, 0, 0);
			}
		}

		/// <summary>
		/// Get resolve for a specific race as (currentResolve, thresholdForReputation, settlingPoint).
		/// </summary>
		public static (float resolve, int threshold, int settling) GetResolveSummary(string race) {
			StatsReflection.EnsureCached();

			var resService = GameReflection.GetResolveService();
			if (resService == null) return (0, 0, 0);

			try {
				float resolve = (float)(StatsReflection.ResGetResolveForMethod?.Invoke(resService, new object[] { race }) ?? 0f);
				int threshold = (int)(StatsReflection.ResGetMinResolveForRepMethod?.Invoke(resService, new object[] { race }) ?? 0);
				int settling = (int)(StatsReflection.ResGetTargetResolveForMethod?.Invoke(resService, new object[] { race }) ?? 0);

				return (resolve, threshold, settling);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetResolveSummary failed: {ex.Message}");
				return (0, 0, 0);
			}
		}

		/// <summary>
		/// Get list of present races (those with at least one villager).
		/// </summary>
		public static List<string> GetPresentRaces() {
			StatsReflection.EnsureCached();

			var result = new List<string>();
			var villService = GameReflection.GetVillagersService();
			if (villService == null) return result;

			try {
				// Get Races dictionary: Dictionary<string, List<Villager>>
				var racesDict = StatsReflection.VillRacesProperty?.GetValue(villService);
				if (racesDict == null) return result;

				// Iterate via reflection
				var keys = ReflectionHelper.IterateKeys(racesDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					var race = key as string;
					if (string.IsNullOrEmpty(race)) continue;

					var villagerList = ReflectionHelper.DictGet(racesDict, key);
					if (villagerList != null) {
						var countProp = villagerList.GetType().GetProperty("Count");
						int count = (int)(countProp?.GetValue(villagerList) ?? 0);
						if (count > 0) {
							result.Add(race);
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetPresentRaces failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get villager count for a specific race.
		/// </summary>
		public static int GetRaceCount(string race) {
			StatsReflection.EnsureCached();

			var villService = GameReflection.GetVillagersService();
			if (villService == null) return 0;

			try {
				var racesDict = StatsReflection.VillRacesProperty?.GetValue(villService);
				if (racesDict == null) return 0;

				var indexer = racesDict.GetType().GetProperty("Item");
				var villagerList = indexer?.GetValue(racesDict, new object[] { race });
				if (villagerList != null) {
					var countProp = villagerList.GetType().GetProperty("Count");
					return (int)(countProp?.GetValue(villagerList) ?? 0);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRaceCount failed: {ex.Message}");
			}

			return 0;
		}

		/// <summary>
		/// Get base resolve and resilience label for a race.
		/// </summary>
		public static (float baseResolve, string resilience) GetRaceBaseInfo(string raceName) {
			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return (0, null);

				var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
				if (getRaceMethod == null) return (0, null);

				var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
				if (raceModel == null) return (0, null);

				// Get initialResolve
				var initialResolveField = raceModel.GetType().GetField("initialResolve", GameReflection.PublicInstance);
				float baseResolve = 0;
				if (initialResolveField != null) {
					var val = initialResolveField.GetValue(raceModel);
					baseResolve = val is float f ? f : 0;
				}

				// Get resilienceLabel
				var resilienceLabelField = raceModel.GetType().GetField("resilienceLabel", GameReflection.PublicInstance);
				string resilience = null;
				if (resilienceLabelField != null) {
					var locaText = resilienceLabelField.GetValue(raceModel);
					resilience = GameReflection.GetLocaText(locaText);
				}

				return (baseResolve, resilience);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRaceBaseInfo failed: {ex.Message}");
				return (0, null);
			}
		}

		/// <summary>
		/// Get reputation breakdown by source.
		/// Returns list of strings like "+2 from Orders".
		/// </summary>
		public static List<string> GetReputationBreakdown() {
			StatsReflection.EnsureCached();

			var result = new List<string>();
			var repService = GameReflection.GetReputationService();
			if (repService == null || StatsReflection.ReputationChangeSourceType == null) return result;

			try {
				// ReputationChangeSource enum: Other=0, Order=1, Resolve=2, Relics=3
				string[] sourceNames = {
					Strings.Get("common.other"),
					Strings.Get("common.orders"),
					Strings.Get("common.resolve"),
					Strings.Get("common.relics")
				};

				for (int i = 0; i < 4; i++) {
					var enumValue = Enum.ToObject(StatsReflection.ReputationChangeSourceType, i);
					_singleArgArray[0] = enumValue;
					float amount = (float)(StatsReflection.RepGetGainedFromMethod?.Invoke(repService, _singleArgArray) ?? 0f);

					if (amount > 0.01f) {
						result.Add(Strings.Get("util.stats.rep_breakdown_entry", amount.ToString("F1"), sourceNames[i]));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetReputationBreakdown failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get impatience breakdown showing rate and grace period.
		/// </summary>
		public static List<string> GetImpatienceBreakdown() {
			StatsReflection.EnsureCached();

			var result = new List<string>();
			var repService = GameReflection.GetReputationService();
			if (repService == null) return result;

			try {
				// Get current rate
				float ratePerSec = 0f;
				if (StatsReflection.RepGetPenaltyPerSecMethod != null) {
					var val = StatsReflection.RepGetPenaltyPerSecMethod.Invoke(repService, null);
					ratePerSec = val is float f ? f : 0f;
				}

				// Get base rate
				float baseRatePerSec = 0f;
				if (StatsReflection.RepGetBasePenaltyPerSecMethod != null) {
					var val = StatsReflection.RepGetBasePenaltyPerSecMethod.Invoke(repService, null);
					baseRatePerSec = val is float f ? f : 0f;
				}

				// Format rate per minute for readability
				float ratePerMin = ratePerSec * 60f;
				result.Add(Strings.Get("util.stats.impatience_rate", ratePerMin.ToString("0.##")));

				// Show if rate is modified from base
				if (Mathf.Abs(ratePerSec - baseRatePerSec) > 0.001f) {
					float basePerMin = baseRatePerSec * 60f;
					float diff = ratePerMin - basePerMin;
					string prefix = diff > 0 ? "+" : "";
					result.Add(Strings.Get("util.stats.impatience_rate_diff", prefix, diff.ToString("0.##")));
				}

				// Get grace period
				if (StatsReflection.RepStateProperty != null && StatsReflection.GracePeriodLeftField != null) {
					var state = StatsReflection.RepStateProperty.GetValue(repService);
					if (state != null) {
						var graceVal = StatsReflection.GracePeriodLeftField.GetValue(state);
						float grace = graceVal is float g ? g : 0f;
						if (grace > 0) {
							int graceSec = Mathf.FloorToInt(grace);
							result.Add(Strings.Get("util.stats.impatience_grace", graceSec));
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetImpatienceBreakdown failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get hostility breakdown by source.
		/// Returns list of strings describing hostility sources.
		/// </summary>
		public static List<string> GetHostilityBreakdown() {
			StatsReflection.EnsureCached();

			var result = new List<string>();
			var hostService = GameReflection.GetHostilityService();
			if (hostService == null) return result;

			try {
				if (StatsReflection.HostilitySourceType == null) return result;

				// HostilitySource enum values and their meanings
				var sources = new (int value, string name)[]
				{
					(10, Strings.Get("util.stats.host_source_years")),
					(20, Strings.Get("common.glades")),
					(30, Strings.Get("util.stats.host_source_dangerous_glades")),
					(40, Strings.Get("util.stats.host_source_forbidden_glades")),
					(50, Strings.Get("common.villagers")),
					(70, Strings.Get("util.stats.host_source_woodcutters")),
					(80, Strings.Get("util.stats.host_source_burning_hearths")),
					(90, Strings.Get("util.stats.host_source_reputation_penalty")),
					(100, Strings.Get("util.stats.host_source_resources_removed")),
					(1000, Strings.Get("util.stats.host_source_effects_negative")),
					(1001, Strings.Get("util.stats.host_source_effects_positive"))
				};

				foreach (var (value, name) in sources) {
					try {
						var enumValue = Enum.ToObject(StatsReflection.HostilitySourceType, value);
						_singleArgArray[0] = enumValue;
						int points = (int)(StatsReflection.HostGetPointsForMethod?.Invoke(hostService, _singleArgArray) ?? 0);

						if (points != 0) {
							string prefix = points > 0 ? "+" : "";
							result.Add(Strings.Get("util.stats.host_breakdown_entry", prefix, points, name));
						}
					} catch {
						// Source not configured for this biome, skip it
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHostilityBreakdown failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get resolve breakdown for a race (all effects affecting resolve).
		/// </summary>
		public static List<string> GetResolveBreakdown(string race) {
			StatsReflection.EnsureCached();

			var result = new List<string>();
			var resService = GameReflection.GetResolveService();
			if (resService == null) return result;

			try {
				// Add base resolve and resilience at the top
				var raceInfo = GetRaceBaseInfo(race);
				if (raceInfo.baseResolve > 0) {
					result.Add(Strings.Get("util.stats.base_resolve", raceInfo.baseResolve));
				}
				if (!string.IsNullOrEmpty(raceInfo.resilience)) {
					result.Add(Strings.Get("util.stats.resilience", raceInfo.resilience));
				}

				// Effects is Dictionary<string, Dictionary<ResolveEffectModel, int>>
				var effectsDict = StatsReflection.ResEffectsProperty?.GetValue(resService);
				if (effectsDict == null) return result;

				// Get the race's effects dictionary
				var indexer = effectsDict.GetType().GetProperty("Item");
				var raceEffects = indexer?.GetValue(effectsDict, new object[] { race });
				if (raceEffects == null) return result;

				// Get total population for this race
				int totalPopulation = GetRaceCount(race);

				// Iterate through the effects
				var enumerator = raceEffects.GetType().GetMethod("GetEnumerator")?.Invoke(raceEffects, null);
				if (enumerator == null) return result;

				var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
				var currentProp = enumerator.GetType().GetProperty("Current");

				while ((bool)moveNextMethod.Invoke(enumerator, null)) {
					var kvp = currentProp.GetValue(enumerator);
					var keyProp = kvp.GetType().GetProperty("Key");
					var valueProp = kvp.GetType().GetProperty("Value");

					var effectModel = keyProp?.GetValue(kvp);
					int count = (int)(valueProp?.GetValue(kvp) ?? 0);

					if (effectModel != null && count > 0) {
						// Get effect name
						var displayNameField = effectModel.GetType().GetField("displayName", GameReflection.PublicInstance);
						var nameProp = effectModel.GetType().GetProperty("Name");

						var locaText = displayNameField?.GetValue(effectModel);
						string name = GameReflection.GetLocaText(locaText)
							?? nameProp?.GetValue(effectModel)?.ToString()
							?? Strings.Get("common.unknown_effect");

						// Get per-villager resolve value
						int perVillager = 0;
						var resProp = effectModel.GetType().GetProperty("resolve");
						var resField = effectModel.GetType().GetField("resolve", GameReflection.PublicInstance);
						if (resProp != null)
							perVillager = (int)(resProp.GetValue(effectModel) ?? 0);
						else if (resField != null)
							perVillager = (int)(resField.GetValue(effectModel) ?? 0);

						// Get actual average impact from ResolveService
						int actualImpact = 0;
						try {
							// Must specify parameter types due to method overloads
							var getRoundedAvgMethod = resService.GetType().GetMethod("GetRoundedAverageResolveImpact",
								GameReflection.PublicInstance,
								null,
								new Type[] { typeof(string), effectModel.GetType() },
								null);
							if (getRoundedAvgMethod != null) {
								actualImpact = (int)getRoundedAvgMethod.Invoke(resService, new object[] { race, effectModel });
							} else {
								Debug.LogWarning($"[ATSAccessibility] GetRoundedAverageResolveImpact method not found for {name}");
								actualImpact = perVillager;  // Fallback
							}
						} catch (Exception avgEx) {
							Debug.LogWarning($"[ATSAccessibility] GetRoundedAverageResolveImpact failed for {name}: {avgEx.Message}");
							actualImpact = perVillager;  // Fallback
						}

						// Format: "Biscuits: +3 (+5 for 5/9 villagers)"
						string avgPrefix = actualImpact >= 0 ? "+" : "";
						string perPrefix = perVillager >= 0 ? "+" : "";
						result.Add(Strings.Get("util.stats.resolve_effect", name, avgPrefix, actualImpact, perPrefix, perVillager, count, totalPopulation));
					}
				}

				// Dispose if IDisposable
				if (enumerator is IDisposable disposable)
					disposable.Dispose();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetResolveBreakdown failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// QUICK HOTKEY HANDLERS
		// ========================================

		/// <summary>
		/// Announce quick summary: Reputation, Impatience, Hostility (S key).
		/// </summary>
		public static void AnnounceQuickSummary() {
			var rep = GetReputationSummary();
			var imp = GetImpatienceSummary();
			var host = GetHostilitySummary();

			int hostilityThreshold = host.points + host.pointsToNext;
			string message = Strings.Get("util.stats.quick_summary",
				FormatDecimal(rep.current), rep.target,
				FormatDecimal(imp.current), imp.max,
				host.level, host.points, hostilityThreshold);

			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Stats: {message}");
		}

		/// <summary>
		/// Announce resolve summary for all present species (R key).
		/// </summary>
		public static void AnnounceResolveSummary() {
			var races = GetPresentRaces();

			if (races.Count == 0) {
				Speech.Say(Strings.Get("util.stats.no_species_present"));
				return;
			}

			var parts = new List<string>();
			foreach (var race in races) {
				var (resolve, threshold, _) = GetResolveSummary(race);

				// Format: "Humans 24/30" (current resolve / threshold)
				parts.Add(Strings.Get("util.stats.resolve_entry", race, Mathf.FloorToInt(resolve), threshold));
			}

			string message = string.Join(", ", parts);
			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Resolve: {message}");
		}

		/// <summary>
		/// Announce next species in rotation with population and resolve (V key).
		/// Cycles through present species one at a time.
		/// </summary>
		public static void AnnounceNextSpeciesResolve() {
			// Refresh the list of present races
			_cachedPresentRaces = GetPresentRaces();

			if (_cachedPresentRaces.Count == 0) {
				Speech.Say(Strings.Get("util.stats.no_species_present"));
				return;
			}

			// Wrap index if needed
			if (_currentSpeciesIndex >= _cachedPresentRaces.Count) {
				_currentSpeciesIndex = 0;
			}

			string race = _cachedPresentRaces[_currentSpeciesIndex];
			int population = GetRaceCount(race);
			var (resolve, threshold, _) = GetResolveSummary(race);

			// Pluralize species name if more than 1
			string raceName = population == 1 ? race : race + "s";

			// Format: "7 Humans, resolve 8 of 15"
			string message = Strings.Get("util.stats.species_cycle", population, raceName, Mathf.FloorToInt(resolve), threshold);
			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Species resolve: {message}");

			// Advance to next species for next press
			_currentSpeciesIndex++;
			if (_currentSpeciesIndex >= _cachedPresentRaces.Count) {
				_currentSpeciesIndex = 0;
			}
		}

		// ========================================
		// TIME/SEASON (T key)
		// ========================================

		// Season name keys for announcement
		private static readonly string[] SeasonNameKeys = { "common.season_drizzle", "common.season_clearance", "common.season_storm" };

		/// <summary>
		/// Get time summary as (year, seasonName, secondsToNextSeason).
		/// </summary>
		public static (int year, string season, float secondsRemaining) GetTimeSummary() {
			int year = GameReflection.GetYear();
			int seasonIndex = GameReflection.GetSeason();
			string season = seasonIndex >= 0 && seasonIndex < SeasonNameKeys.Length
				? Strings.Get(SeasonNameKeys[seasonIndex]) : Strings.Get("common.unknown");
			float seconds = GameReflection.GetTimeTillNextSeason();
			return (year, season, seconds);
		}

		/// <summary>
		/// Announce current season, time remaining, and year (T key).
		/// </summary>
		public static void AnnounceTimeSummary() {
			var (year, season, seconds) = GetTimeSummary();

			// Format time remaining as "X minutes Y seconds" or just "X seconds"
			string timeRemaining;
			if (seconds >= 60) {
				int minutes = Mathf.FloorToInt(seconds / 60);
				int secs = Mathf.FloorToInt(seconds % 60);
				timeRemaining = secs > 0 ? Strings.Get("util.stats.time_minutes_seconds", minutes, secs) : Strings.Get("util.stats.time_minutes", minutes);
			} else {
				timeRemaining = Strings.Get("util.stats.time_seconds", Mathf.FloorToInt(seconds));
			}

			string message = Strings.Get("util.stats.time_summary", season, timeRemaining, year);
			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Time: {message}");
		}
	}
}
