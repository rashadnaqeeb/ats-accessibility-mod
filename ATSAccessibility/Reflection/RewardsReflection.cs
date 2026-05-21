using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to reward services for the F3 Rewards panel.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class RewardsReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// IGameServices service properties
		private static PropertyInfo _gsReputationRewardsServiceProperty = null;
		private static PropertyInfo _gsCornerstonesServiceProperty = null;
		private static PropertyInfo _gsNewcomersServiceProperty = null;

		// IReputationRewardsService properties/methods
		private static PropertyInfo _rrsRewardsToCollectProperty = null;
		private static MethodInfo _rrsRequestPopupMethod = null;

		// ICornerstonesService methods
		private static MethodInfo _csGetCurrentPickMethod = null;

		// INewcomersService methods
		private static MethodInfo _nsAreNewcomersWaitningMethod = null;  // Note: typo in game
		private static MethodInfo _nsGetCurrentNewcomersMethod = null;

		// ReactiveProperty<int>.Value property
		private static PropertyInfo _reactivePropertyValueProperty = null;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("RewardsReflection", assembly => {
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType == null) return;

				// Cache service property accessors
				_gsReputationRewardsServiceProperty = gameServicesType.GetProperty("ReputationRewardsService");
				_gsCornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService");
				_gsNewcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");

				// Cache ReputationRewardsService members
				var rrsType = assembly.GetType("Eremite.Services.IReputationRewardsService");
				if (rrsType != null) {
					_rrsRewardsToCollectProperty = rrsType.GetProperty("RewardsToCollect");
					_rrsRequestPopupMethod = rrsType.GetMethod("RequestPopup");
				}

				// Cache CornerstonesService members
				var csType = assembly.GetType("Eremite.Services.ICornerstonesService");
				if (csType != null) {
					_csGetCurrentPickMethod = csType.GetMethod("GetCurrentPick");
				}

				// Cache NewcomersService members
				var nsType = assembly.GetType("Eremite.Services.INewcomersService");
				if (nsType != null) {
					_nsAreNewcomersWaitningMethod = nsType.GetMethod("AreNewcomersWaitning");  // Note: typo in game
					_nsGetCurrentNewcomersMethod = nsType.GetMethod("GetCurrentNewcomers");
				}
			});
		}

		// ========================================
		// REWARD DETECTION
		// ========================================

		/// <summary>
		/// Check if there are pending blueprints to pick.
		/// Uses ReputationRewardsService.RewardsToCollect.Value > 0.
		/// </summary>
		public static bool HasPendingBlueprints() {
			EnsureCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var rewardsService = ReflectionHelper.GetProp(_gsReputationRewardsServiceProperty, gameServices);
				if (rewardsService == null) return false;

				var rewardsToCollect = ReflectionHelper.GetProp(_rrsRewardsToCollectProperty, rewardsService);
				if (rewardsToCollect == null) return false;

				// Get the Value property from ReactiveProperty<int>
				if (_reactivePropertyValueProperty == null) {
					_reactivePropertyValueProperty = rewardsToCollect.GetType().GetProperty("Value");
				}

				return ReflectionHelper.GetPropInt(_reactivePropertyValueProperty, rewardsToCollect) > 0;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] HasPendingBlueprints failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Get the number of pending blueprints to pick.
		/// Uses ReputationRewardsService.RewardsToCollect.Value.
		/// </summary>
		public static int GetPendingBlueprintCount() {
			EnsureCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return 0;

				var rewardsService = ReflectionHelper.GetProp(_gsReputationRewardsServiceProperty, gameServices);
				if (rewardsService == null) return 0;

				var rewardsToCollect = ReflectionHelper.GetProp(_rrsRewardsToCollectProperty, rewardsService);
				if (rewardsToCollect == null) return 0;

				// Get the Value property from ReactiveProperty<int>
				if (_reactivePropertyValueProperty == null) {
					_reactivePropertyValueProperty = rewardsToCollect.GetType().GetProperty("Value");
				}

				return ReflectionHelper.GetPropInt(_reactivePropertyValueProperty, rewardsToCollect);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetPendingBlueprintCount failed: {ex.Message}");
			}

			return 0;
		}

		/// <summary>
		/// Check if there are pending cornerstones to pick.
		/// Uses CornerstonesService.GetCurrentPick() != null.
		/// </summary>
		public static bool HasPendingCornerstones() {
			EnsureCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var cornerstonesService = ReflectionHelper.GetProp(_gsCornerstonesServiceProperty, gameServices);
				if (cornerstonesService == null) return false;

				var currentPick = ReflectionHelper.Invoke(_csGetCurrentPickMethod, cornerstonesService);
				return currentPick != null;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] HasPendingCornerstones failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Check if there are newcomers waiting.
		/// Uses NewcomersService.AreNewcomersWaitning() (note: typo in game).
		/// </summary>
		public static bool HasPendingNewcomers() {
			EnsureCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return false;

				var newcomersService = ReflectionHelper.GetProp(_gsNewcomersServiceProperty, gameServices);
				if (newcomersService == null) return false;

				return ReflectionHelper.InvokeBool(_nsAreNewcomersWaitningMethod, newcomersService);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] HasPendingNewcomers failed: {ex.Message}");
			}

			return false;
		}

		// ========================================
		// POPUP TRIGGERS
		// ========================================

		/// <summary>
		/// Open the blueprints popup.
		/// Uses ReputationRewardsService.RequestPopup().
		/// </summary>
		public static bool OpenBlueprintsPopup() {
			EnsureCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) {
					Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: GameServices not available");
					return false;
				}

				var rewardsService = ReflectionHelper.GetProp(_gsReputationRewardsServiceProperty, gameServices);
				if (rewardsService == null) {
					Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: ReputationRewardsService not available");
					return false;
				}

				if (_rrsRequestPopupMethod == null) {
					Debug.LogWarning("[ATSAccessibility] OpenBlueprintsPopup: RequestPopup method not found");
					return false;
				}

				if (!ReflectionHelper.InvokeVoid(_rrsRequestPopupMethod, rewardsService)) return false;
				Debug.Log("[ATSAccessibility] Opened blueprints popup");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OpenBlueprintsPopup failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Open the cornerstones popup.
		/// Fires GameBlackboardService.OnRewardsPopupRequested.
		/// </summary>
		public static bool OpenCornerstonesPopup() {
			try {
				var blackboardService = GameReflection.GetGameBlackboardService();
				if (blackboardService == null) {
					Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: GameBlackboardService not available");
					return false;
				}

				// Get Unit.Default for Subject<Unit>
				var unitDefault = GameReflection.GetUnitDefault();
				if (unitDefault == null) {
					Debug.LogWarning("[ATSAccessibility] OpenCornerstonesPopup: Could not get Unit.Default");
					return false;
				}

				// Use the shared helper to fire OnRewardsPopupRequested.OnNext(Unit.Default)
				bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnRewardsPopupRequested", unitDefault);
				if (result) {
					Debug.Log("[ATSAccessibility] Opened cornerstones popup");
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OpenCornerstonesPopup failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Open the newcomers popup.
		/// Fires GameBlackboardService.OnNewcomersPopupRequested with current newcomers.
		/// </summary>
		public static bool OpenNewcomersPopup() {
			EnsureCached();

			try {
				// First get the current newcomers
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) {
					Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameServices not available");
					return false;
				}

				var newcomersService = ReflectionHelper.GetProp(_gsNewcomersServiceProperty, gameServices);
				if (newcomersService == null) {
					Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: NewcomersService not available");
					return false;
				}

				var currentNewcomers = ReflectionHelper.Invoke(_nsGetCurrentNewcomersMethod, newcomersService);
				if (currentNewcomers == null) {
					Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: No current newcomers");
					return false;
				}

				// Now fire the event using the shared helper
				var blackboardService = GameReflection.GetGameBlackboardService();
				if (blackboardService == null) {
					Debug.LogWarning("[ATSAccessibility] OpenNewcomersPopup: GameBlackboardService not available");
					return false;
				}

				bool result = GameReflection.InvokeSubjectOnNext(blackboardService, "OnNewcomersPopupRequested", currentNewcomers);
				if (result) {
					Debug.Log("[ATSAccessibility] Opened newcomers popup");
				}
				return result;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] OpenNewcomersPopup failed: {ex.Message}");
				return false;
			}
		}

		// ========================================
		// UNAVAILABILITY INFO
		// ========================================

		private static bool _unavailTypesCached = false;

		// IReputationService.Reputation (ReactiveProperty<float>)
		private static PropertyInfo _repReputationProperty = null;

		// HasRewardFor(int) on concrete ReputationRewardsService
		private static MethodInfo _rrsHasRewardForMethod = null;

		// INewcomersService.GetTimeToNextVisit()
		private static MethodInfo _nsGetTimeToNextVisitMethod = null;

		// BiomeService access for cornerstone dates
		private static PropertyInfo _gsBiomeServiceProperty = null;
		private static PropertyInfo _bsCurrentBiomeProperty = null;
		private static FieldInfo _bmSeasonsField = null;
		private static FieldInfo _scSeasonRewardsField = null;

		// CalendarService Quarter property
		private static PropertyInfo _calQuarterProperty = null;

		// SeasonRewardModel fields
		private static FieldInfo _srmYearField = null;
		private static FieldInfo _srmSeasonField = null;
		private static FieldInfo _srmQuarterField = null;

		private static void EnsureUnavailTypeCached() {
			if (_unavailTypesCached) return;
			_unavailTypesCached = true;

			ReflectionHelper.InitCache("RewardsReflection unavailability", assembly => {
				// IReputationService.Reputation property
				var repServiceType = assembly.GetType("Eremite.Services.IReputationService");
				if (repServiceType != null) {
					_repReputationProperty = repServiceType.GetProperty("Reputation",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// HasRewardFor on concrete ReputationRewardsService (public method)
				var rrsConcreteType = assembly.GetType("Eremite.Services.ReputationRewardsService");
				if (rrsConcreteType != null) {
					_rrsHasRewardForMethod = rrsConcreteType.GetMethod("HasRewardFor",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// INewcomersService.GetTimeToNextVisit
				var nsType = assembly.GetType("Eremite.Services.INewcomersService");
				if (nsType != null) {
					_nsGetTimeToNextVisitMethod = nsType.GetMethod("GetTimeToNextVisit",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// BiomeService from IGameServices
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// IBiomeService.CurrentBiome
				var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
				if (biomeServiceType != null) {
					_bsCurrentBiomeProperty = biomeServiceType.GetProperty("CurrentBiome",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// BiomeModel.seasons field
				var biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
				if (biomeModelType != null) {
					_bmSeasonsField = biomeModelType.GetField("seasons",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// SeasonsConfig.SeasonRewards field
				var seasonsConfigType = assembly.GetType("Eremite.Model.Configs.SeasonsConfig");
				if (seasonsConfigType != null) {
					_scSeasonRewardsField = seasonsConfigType.GetField("SeasonRewards",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// CalendarService Quarter property
				var calServiceType = assembly.GetType("Eremite.Services.ICalendarService");
				if (calServiceType != null) {
					_calQuarterProperty = calServiceType.GetProperty("Quarter",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// SeasonRewardModel fields
				var srmType = assembly.GetType("Eremite.Model.SeasonRewardModel");
				if (srmType != null) {
					_srmYearField = srmType.GetField("year", BindingFlags.Public | BindingFlags.Instance);
					_srmSeasonField = srmType.GetField("season", BindingFlags.Public | BindingFlags.Instance);
					_srmQuarterField = srmType.GetField("quarter", BindingFlags.Public | BindingFlags.Instance);
				}
			});
		}

		/// <summary>
		/// Get the next reputation threshold that grants a blueprint reward.
		/// Returns (nextThreshold, currentRep) or null if not determinable.
		/// </summary>
		public static (int nextThreshold, int currentRep)? GetNextBlueprintThreshold() {
			EnsureCached();
			EnsureUnavailTypeCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return null;

				// Get current reputation value
				var repService = GameReflection.GetReputationService();
				if (repService == null) return null;

				var repReactive = ReflectionHelper.GetProp(_repReputationProperty, repService);
				if (repReactive == null) return null;

				// Get Value from ReactiveProperty<float>
				var valueProp = repReactive.GetType().GetProperty("Value");
				var repValue = valueProp?.GetValue(repReactive);
				if (!(repValue is float repFloat)) return null;

				int currentRep = (int)repFloat;

				// Get ReputationRewardsService instance
				var rewardsService = ReflectionHelper.GetProp(_gsReputationRewardsServiceProperty, gameServices);
				if (rewardsService == null || _rrsHasRewardForMethod == null) return null;

				// Search for next threshold (reputation rewards are typically 1-20)
				for (int i = currentRep + 1; i <= 30; i++) {
					if (ReflectionHelper.InvokeBool(_rrsHasRewardForMethod, rewardsService, i)) {
						return (i, currentRep);
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetNextBlueprintThreshold failed: {ex.Message}");
			}

			return null;
		}

		// Season names (lookup keys for Strings.Get — resolved at call time)
		private static readonly string[] SeasonNameKeys = {
			"common.season_drizzle",
			"common.season_clearance",
			"common.season_storm",
		};

		/// <summary>
		/// Get the next cornerstone reward date.
		/// Returns (season name, year) or null if not determinable.
		/// </summary>
		public static (string season, int year)? GetNextCornerstoneDate() {
			EnsureUnavailTypeCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return null;

				// Get current game date from CalendarService
				var calService = GameReflection.GetCalendarService();
				if (calService == null) return null;

				int curYear = GameReflection.GetYear();
				int curSeason = GameReflection.GetSeason();
				int curQuarter = ReflectionHelper.GetPropInt(_calQuarterProperty, calService);

				if (curYear <= 0 || curSeason < 0 || curQuarter < 0) return null;

				// Get biome's SeasonRewards list
				var biomeService = ReflectionHelper.GetProp(_gsBiomeServiceProperty, gameServices);
				if (biomeService == null) return null;

				var currentBiome = ReflectionHelper.GetProp(_bsCurrentBiomeProperty, biomeService);
				if (currentBiome == null) return null;

				var seasonsConfig = ReflectionHelper.GetField(_bmSeasonsField, currentBiome);
				if (seasonsConfig == null) return null;

				var seasonRewardsList = ReflectionHelper.GetField(_scSeasonRewardsField, seasonsConfig);
				if (seasonRewardsList == null) return null;

				// Iterate the list to find next reward date after current date
				var enumerable = seasonRewardsList as IEnumerable;
				if (enumerable == null) return null;

				int bestYear = int.MaxValue;
				int bestSeason = int.MaxValue;
				int bestQuarter = int.MaxValue;
				bool found = false;

				foreach (var srm in enumerable) {
					if (srm == null) continue;

					int y = ReflectionHelper.GetInt(_srmYearField, srm);
					int s = ReflectionHelper.GetInt(_srmSeasonField, srm);
					int q = ReflectionHelper.GetInt(_srmQuarterField, srm);

					// Check if this date is in the future
					if (!IsDateAfter(y, s, q, curYear, curSeason, curQuarter)) continue;

					// Check if this is earlier than current best
					if (!found || IsDateAfter(bestYear, bestSeason, bestQuarter, y, s, q)) {
						bestYear = y;
						bestSeason = s;
						bestQuarter = q;
						found = true;
					}
				}

				if (found && bestSeason >= 0 && bestSeason < SeasonNameKeys.Length) {
					return (Strings.Get(SeasonNameKeys[bestSeason]), bestYear);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetNextCornerstoneDate failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Returns true if date (y1,s1,q1) is strictly after (y2,s2,q2).
		/// </summary>
		private static bool IsDateAfter(int y1, int s1, int q1, int y2, int s2, int q2) {
			if (y1 != y2) return y1 > y2;
			if (s1 != s2) return s1 > s2;
			return q1 > q2;
		}

		/// <summary>
		/// Get time in seconds until next newcomers visit, or -1 if unavailable.
		/// </summary>
		public static float GetTimeToNextNewcomers() {
			EnsureCached();
			EnsureUnavailTypeCached();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return -1f;

				var newcomersService = ReflectionHelper.GetProp(_gsNewcomersServiceProperty, gameServices);
				if (newcomersService == null) return -1f;

				if (_nsGetTimeToNextVisitMethod == null) return -1f;

				return ReflectionHelper.InvokeFloat(_nsGetTimeToNextVisitMethod, newcomersService);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetTimeToNextNewcomers failed: {ex.Message}");
			}

			return -1f;
		}

		/// <summary>
		/// Format game time in seconds to a readable string.
		/// Rounds to nearest 5 seconds for cleaner output.
		/// </summary>
		public static string FormatGameTime(float seconds) {
			if (seconds <= 0) return Strings.Get("reflection.rewards.time_soon");

			// Round to nearest 5 seconds
			int totalSeconds = (int)(Mathf.Round(seconds / 5f) * 5f);
			if (totalSeconds <= 0) totalSeconds = 5;

			int minutes = totalSeconds / 60;
			int secs = totalSeconds % 60;

			if (minutes > 0 && secs > 0) {
				string minPart = Strings.Get(minutes == 1 ? "reflection.rewards.time_minutes_singular" : "reflection.rewards.time_minutes_plural", minutes);
				string secPart = Strings.Get(secs == 1 ? "reflection.rewards.time_seconds_singular" : "reflection.rewards.time_seconds_plural", secs);
				return Strings.Get("reflection.rewards.time_minutes_seconds", minPart, secPart);
			}
			if (minutes > 0)
				return Strings.Get(minutes == 1 ? "reflection.rewards.time_minutes_singular" : "reflection.rewards.time_minutes_plural", minutes);
			return Strings.Get(secs == 1 ? "reflection.rewards.time_seconds_singular" : "reflection.rewards.time_seconds_plural", secs);
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(RewardsReflection), "RewardsReflection");
		}
	}
}
