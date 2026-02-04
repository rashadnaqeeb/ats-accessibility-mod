using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
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
		private static bool _repTypesCached = false;

		public static PropertyInfo RepReputationProperty => _repReputationProperty;
		public static PropertyInfo RepPenaltyProperty => _repPenaltyProperty;
		public static MethodInfo RepGetToWinMethod => _repGetToWinMethod;
		public static MethodInfo RepGetPenaltyToLooseMethod => _repGetPenaltyToLooseMethod;
		public static MethodInfo RepGetGainedFromMethod => _repGetGainedFromMethod;
		public static MethodInfo RepGetPenaltyPerSecMethod => _repGetPenaltyPerSecMethod;
		public static MethodInfo RepGetBasePenaltyPerSecMethod => _repGetBasePenaltyPerSecMethod;
		public static PropertyInfo RepStateProperty => _repStateProperty;
		public static FieldInfo GracePeriodLeftField => _gracePeriodLeftField;

		public static void EnsureReputationTypes() {
			if (_repTypesCached) return;

			var repService = GameReflection.GetReputationService();
			if (repService == null) {
				return;
			}

			try {
				var type = repService.GetType();
				_repReputationProperty = type.GetProperty("Reputation", GameReflection.PublicInstance);
				_repPenaltyProperty = type.GetProperty("ReputationPenalty", GameReflection.PublicInstance);
				_repGetToWinMethod = type.GetMethod("GetReputationToWin", GameReflection.PublicInstance);
				_repGetPenaltyToLooseMethod = type.GetMethod("GetReputationPenaltyToLoose", GameReflection.PublicInstance);
				_repGetGainedFromMethod = type.GetMethod("GetReputationGainedFrom", GameReflection.PublicInstance);
				_repGetPenaltyPerSecMethod = type.GetMethod("GetReputationPenaltyPerSec", GameReflection.PublicInstance);
				_repGetBasePenaltyPerSecMethod = type.GetMethod("GetBaseReputationPenaltyPerSec", GameReflection.PublicInstance);
				_repStateProperty = type.GetProperty("State", GameReflection.PublicInstance);

				// Get gracePeriodLeft field from GameObjectivesState
				var assembly = GameReflection.GameAssembly;
				var stateType = assembly?.GetType("Eremite.Model.State.GameObjectivesState");
				if (stateType != null) {
					_gracePeriodLeftField = stateType.GetField("gracePeriodLeft", GameReflection.PublicInstance);
				}

				Debug.Log("[ATSAccessibility] Cached ReputationService types");
				_repTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ReputationService type caching failed: {ex.Message}");
			}
		}

		// ========================================
		// HOSTILITY SERVICE
		// ========================================

		private static PropertyInfo _hostPointsProperty = null;          // ReactiveProperty<int>
		private static PropertyInfo _hostLevelProperty = null;           // ReactiveProperty<int>
		private static MethodInfo _hostGetSourceAmountMethod = null;     // GetSourceAmount(source)
		private static MethodInfo _hostGetPointsForMethod = null;        // GetPointsFor(source)
		private static MethodInfo _hostGetPointsLeftToNextLevelMethod = null; // GetPointsLeftToNextLevel()
		private static bool _hostTypesCached = false;

		public static PropertyInfo HostPointsProperty => _hostPointsProperty;
		public static PropertyInfo HostLevelProperty => _hostLevelProperty;
		public static MethodInfo HostGetSourceAmountMethod => _hostGetSourceAmountMethod;
		public static MethodInfo HostGetPointsForMethod => _hostGetPointsForMethod;
		public static MethodInfo HostGetPointsLeftToNextLevelMethod => _hostGetPointsLeftToNextLevelMethod;

		public static void EnsureHostilityTypes() {
			if (_hostTypesCached) return;

			var hostService = GameReflection.GetHostilityService();
			if (hostService == null) {
				return;
			}

			try {
				var type = hostService.GetType();
				_hostPointsProperty = type.GetProperty("Points", GameReflection.PublicInstance);
				_hostLevelProperty = type.GetProperty("Level", GameReflection.PublicInstance);
				_hostGetSourceAmountMethod = type.GetMethod("GetSourceAmount", GameReflection.PublicInstance);
				_hostGetPointsForMethod = type.GetMethod("GetPointsFor", GameReflection.PublicInstance);
				_hostGetPointsLeftToNextLevelMethod = type.GetMethod("GetPointsLeftToNextLevel", GameReflection.PublicInstance);

				Debug.Log("[ATSAccessibility] Cached HostilityService types");
				_hostTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] HostilityService type caching failed: {ex.Message}");
			}
		}

		// ========================================
		// RESOLVE SERVICE
		// ========================================

		private static MethodInfo _resGetResolveForMethod = null;        // GetResolveFor(race)
		private static MethodInfo _resGetMinResolveForRepMethod = null;  // GetMinResolveForReputation(race)
		private static MethodInfo _resGetTargetResolveForMethod = null;  // GetTargetResolveFor(race) - settling point
		private static PropertyInfo _resEffectsProperty = null;          // Effects dictionary
		private static bool _resTypesCached = false;

		public static MethodInfo ResGetResolveForMethod => _resGetResolveForMethod;
		public static MethodInfo ResGetMinResolveForRepMethod => _resGetMinResolveForRepMethod;
		public static MethodInfo ResGetTargetResolveForMethod => _resGetTargetResolveForMethod;
		public static PropertyInfo ResEffectsProperty => _resEffectsProperty;

		public static void EnsureResolveTypes() {
			if (_resTypesCached) return;

			var resService = GameReflection.GetResolveService();
			if (resService == null) {
				return;
			}

			try {
				var type = resService.GetType();
				_resGetResolveForMethod = type.GetMethod("GetResolveFor", GameReflection.PublicInstance);
				_resGetMinResolveForRepMethod = type.GetMethod("GetMinResolveForReputation",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
				_resGetTargetResolveForMethod = type.GetMethod("GetTargetResolveFor",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
				_resEffectsProperty = type.GetProperty("Effects", GameReflection.PublicInstance);

				Debug.Log("[ATSAccessibility] Cached ResolveService types");
				_resTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ResolveService type caching failed: {ex.Message}");
			}
		}

		// ========================================
		// VILLAGERS SERVICE
		// ========================================

		private static PropertyInfo _villRacesProperty = null;           // Races dictionary
		private static bool _villTypesCached = false;

		public static PropertyInfo VillRacesProperty => _villRacesProperty;

		public static void EnsureVillagersTypes() {
			if (_villTypesCached) return;

			var villService = GameReflection.GetVillagersService();
			if (villService == null) {
				return;
			}

			try {
				var type = villService.GetType();
				_villRacesProperty = type.GetProperty("Races", GameReflection.PublicInstance);

				Debug.Log("[ATSAccessibility] Cached VillagersService types");
				_villTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] VillagersService type caching failed: {ex.Message}");
			}
		}

		// ========================================
		// ENUM TYPES
		// ========================================

		private static Type _reputationChangeSourceType = null;
		private static Type _hostilitySourceType = null;
		private static bool _enumTypesCached = false;

		public static Type ReputationChangeSourceType => _reputationChangeSourceType;
		public static Type HostilitySourceType {
			get => _hostilitySourceType;
			internal set => _hostilitySourceType = value;
		}

		public static void EnsureEnumTypes() {
			if (_enumTypesCached) return;

			try {
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					if (assembly.GetName().Name == "Assembly-CSharp") {
						_reputationChangeSourceType = assembly.GetType("Eremite.Services.ReputationChangeSource");
						break;
					}
				}
				_enumTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Enum type caching failed: {ex.Message}");
			}
		}
	}
}
