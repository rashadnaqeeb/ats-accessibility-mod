using System;
using System.Reflection;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Cached reflection metadata for game statistics services
	/// (ReputationService, HostilityService, ResolveService, VillagersService).
	/// </summary>
	public static class StatsReflection {
		// ========================================
		// REPUTATION SERVICE
		// ========================================

		private static PropertyInfo _repReputationProperty = null;       // ReactiveProperty<float>
		private static PropertyInfo _repPenaltyProperty = null;          // ReactiveProperty<float> (impatience)
		private static MethodInfo _repGetToWinMethod = null;             // GetReputationToWin()
		private static MethodInfo _repGetPenaltyToLooseMethod = null;    // GetReputationPenaltyToLoose()
		private static MethodInfo _repGetGainedFromMethod = null;        // GetReputationGainedFrom(source)
		private static MethodInfo _repGetPenaltyPerSecMethod = null;     // GetReputationPenaltyPerSec()
		private static MethodInfo _repGetBasePenaltyPerSecMethod = null; // GetBaseReputationPenaltyPerSec()
		private static PropertyInfo _repStateProperty = null;            // State (GameObjectivesState)
		private static FieldInfo _gracePeriodLeftField = null;           // gracePeriodLeft field

		public static PropertyInfo RepReputationProperty => _repReputationProperty;
		public static PropertyInfo RepPenaltyProperty => _repPenaltyProperty;
		public static MethodInfo RepGetToWinMethod => _repGetToWinMethod;
		public static MethodInfo RepGetPenaltyToLooseMethod => _repGetPenaltyToLooseMethod;
		public static MethodInfo RepGetGainedFromMethod => _repGetGainedFromMethod;
		public static MethodInfo RepGetPenaltyPerSecMethod => _repGetPenaltyPerSecMethod;
		public static MethodInfo RepGetBasePenaltyPerSecMethod => _repGetBasePenaltyPerSecMethod;
		public static PropertyInfo RepStateProperty => _repStateProperty;
		public static FieldInfo GracePeriodLeftField => _gracePeriodLeftField;

		// ========================================
		// HOSTILITY SERVICE
		// ========================================

		private static PropertyInfo _hostPointsProperty = null;          // ReactiveProperty<int>
		private static PropertyInfo _hostLevelProperty = null;           // ReactiveProperty<int>
		private static MethodInfo _hostGetSourceAmountMethod = null;     // GetSourceAmount(source)
		private static MethodInfo _hostGetPointsForMethod = null;        // GetPointsFor(source)
		private static MethodInfo _hostGetPointsLeftToNextLevelMethod = null; // GetPointsLeftToNextLevel()

		public static PropertyInfo HostPointsProperty => _hostPointsProperty;
		public static PropertyInfo HostLevelProperty => _hostLevelProperty;
		public static MethodInfo HostGetSourceAmountMethod => _hostGetSourceAmountMethod;
		public static MethodInfo HostGetPointsForMethod => _hostGetPointsForMethod;
		public static MethodInfo HostGetPointsLeftToNextLevelMethod => _hostGetPointsLeftToNextLevelMethod;

		// ========================================
		// RESOLVE SERVICE
		// ========================================

		private static MethodInfo _resGetResolveForMethod = null;        // GetResolveFor(race)
		private static MethodInfo _resGetMinResolveForRepMethod = null;  // GetMinResolveForReputation(race)
		private static MethodInfo _resGetTargetResolveForMethod = null;  // GetTargetResolveFor(race) - settling point
		private static PropertyInfo _resEffectsProperty = null;          // Effects dictionary

		public static MethodInfo ResGetResolveForMethod => _resGetResolveForMethod;
		public static MethodInfo ResGetMinResolveForRepMethod => _resGetMinResolveForRepMethod;
		public static MethodInfo ResGetTargetResolveForMethod => _resGetTargetResolveForMethod;
		public static PropertyInfo ResEffectsProperty => _resEffectsProperty;

		// ========================================
		// VILLAGERS SERVICE
		// ========================================

		private static PropertyInfo _villRacesProperty = null;           // Races dictionary

		public static PropertyInfo VillRacesProperty => _villRacesProperty;

		// ========================================
		// ENUM TYPES
		// ========================================

		private static Type _reputationChangeSourceType = null;
		private static Type _hostilitySourceType = null;

		public static Type ReputationChangeSourceType => _reputationChangeSourceType;
		public static Type HostilitySourceType => _hostilitySourceType;

		// ========================================
		// CACHING
		// ========================================

		private static bool _cached = false;

		// Internal (not public) so ReflectionValidator's non-public method scan
		// finds and triggers it before validating the cached fields.
		internal static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("StatsReflection", assembly => {
				CacheReputationTypes(assembly);
				CacheHostilityTypes(assembly);
				CacheResolveTypes(assembly);
				CacheVillagersTypes(assembly);
				CacheEnumTypes(assembly);
			});
		}

		private static void CacheReputationTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Services.ReputationService");
			if (type != null) {
				_repReputationProperty = type.GetProperty("Reputation", GameReflection.PublicInstance);
				_repPenaltyProperty = type.GetProperty("ReputationPenalty", GameReflection.PublicInstance);
				_repGetToWinMethod = type.GetMethod("GetReputationToWin", GameReflection.PublicInstance);
				_repGetPenaltyToLooseMethod = type.GetMethod("GetReputationPenaltyToLoose", GameReflection.PublicInstance);
				_repGetGainedFromMethod = type.GetMethod("GetReputationGainedFrom", GameReflection.PublicInstance);
				_repGetPenaltyPerSecMethod = type.GetMethod("GetReputationPenaltyPerSec", GameReflection.PublicInstance);
				_repGetBasePenaltyPerSecMethod = type.GetMethod("GetBaseReputationPenaltyPerSec", GameReflection.PublicInstance);
				_repStateProperty = type.GetProperty("State", GameReflection.PublicInstance);
			}

			var stateType = assembly.GetType("Eremite.Model.State.GameObjectivesState");
			if (stateType != null) {
				_gracePeriodLeftField = stateType.GetField("gracePeriodLeft", GameReflection.PublicInstance);
			}
		}

		private static void CacheHostilityTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Services.HostilityService");
			if (type != null) {
				_hostPointsProperty = type.GetProperty("Points", GameReflection.PublicInstance);
				_hostLevelProperty = type.GetProperty("Level", GameReflection.PublicInstance);
				_hostGetSourceAmountMethod = type.GetMethod("GetSourceAmount", GameReflection.PublicInstance);
				_hostGetPointsForMethod = type.GetMethod("GetPointsFor", GameReflection.PublicInstance);
				_hostGetPointsLeftToNextLevelMethod = type.GetMethod("GetPointsLeftToNextLevel", GameReflection.PublicInstance);
			}
		}

		private static void CacheResolveTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Services.ResolveService");
			if (type != null) {
				_resGetResolveForMethod = type.GetMethod("GetResolveFor", GameReflection.PublicInstance);
				_resGetMinResolveForRepMethod = type.GetMethod("GetMinResolveForReputation",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
				_resGetTargetResolveForMethod = type.GetMethod("GetTargetResolveFor",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
				_resEffectsProperty = type.GetProperty("Effects", GameReflection.PublicInstance);
			}
		}

		private static void CacheVillagersTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Services.VillagersService");
			if (type != null) {
				_villRacesProperty = type.GetProperty("Races", GameReflection.PublicInstance);
			}
		}

		private static void CacheEnumTypes(Assembly assembly) {
			_reputationChangeSourceType = assembly.GetType("Eremite.Services.ReputationChangeSource");
			_hostilitySourceType = assembly.GetType("Eremite.Model.State.HostilitySource");
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(StatsReflection), "StatsReflection");
		}
	}
}
