using System;
using System.Collections.Generic;
using System.Reflection;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to automaton (automated worker) data.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// </summary>
	public static class AutomatonReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// Automaton type for identity check
		private static Type _automatonType = null;

		// Actor.IsBoundToWorkplace — fallback discriminator
		private static PropertyInfo _isBoundToWorkplaceProperty = null;

		// IGameServices.AutomatonsService
		private static PropertyInfo _automatonsServiceProperty = null;

		// IAutomatonsService methods
		private static MethodInfo _getAutomatonMethod = null;  // GetAutomaton(int)
		private static MethodInfo _isAliveMethod = null;  // IsAlive(int)

		// Automaton.model field (AutomatonModel)
		private static FieldInfo _automatonModelField = null;

		// AutomatonModel.displayName (LocaText)
		private static FieldInfo _modelDisplayNameField = null;

		// ProductionBuilding.ProductionBuildingState property
		private static PropertyInfo _productionBuildingStateProperty = null;

		// ProductionBuildingState.looseAutomatons (List<int>)
		private static FieldInfo _looseAutomatonsField = null;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("AutomatonReflection", assembly => {
				// Actor.IsBoundToWorkplace
				var actorType = assembly.GetType("Eremite.Characters.Actor");
				if (actorType != null) {
					_isBoundToWorkplaceProperty = actorType.GetProperty("IsBoundToWorkplace", GameReflection.PublicInstance);
				}

				// IGameServices.AutomatonsService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_automatonsServiceProperty = gameServicesType.GetProperty("AutomatonsService");
				}

				// IAutomatonsService methods
				var automatonsServiceType = assembly.GetType("Eremite.Services.IAutomatonsService");
				if (automatonsServiceType != null) {
					_getAutomatonMethod = automatonsServiceType.GetMethod("GetAutomaton", new[] { typeof(int) });
					_isAliveMethod = automatonsServiceType.GetMethod("IsAlive", new[] { typeof(int) });
				}

				// Automaton type and model field
				_automatonType = assembly.GetType("Eremite.Characters.Automatons.Automaton");
				if (_automatonType != null) {
					_automatonModelField = _automatonType.GetField("model", GameReflection.PublicInstance);
				}

				// AutomatonModel.displayName
				var automatonModelType = assembly.GetType("Eremite.Model.AutomatonModel");
				if (automatonModelType != null) {
					_modelDisplayNameField = automatonModelType.GetField("displayName", GameReflection.PublicInstance);
				}

				// ProductionBuilding.ProductionBuildingState
				var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
				if (productionBuildingType != null) {
					_productionBuildingStateProperty = productionBuildingType.GetProperty("ProductionBuildingState", GameReflection.PublicInstance);
				}

				// ProductionBuildingState.looseAutomatons
				var pbStateType = assembly.GetType("Eremite.Buildings.ProductionBuildingState");
				if (pbStateType != null) {
					_looseAutomatonsField = pbStateType.GetField("looseAutomatons", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// TYPE DETECTION
		// ========================================

		/// <summary>
		/// Check if an actor is an automaton (IsBoundToWorkplace == true).
		/// Returns false for villagers and null actors.
		/// </summary>
		public static bool IsAutomaton(object actor) {
			if (actor == null) return false;
			EnsureCached();
			if (_automatonType != null)
				return _automatonType.IsInstanceOfType(actor);
			// Fallback if type lookup failed
			return ReflectionHelper.GetPropBool(_isBoundToWorkplaceProperty, actor);
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		/// <summary>
		/// Get an automaton instance by actor ID from AutomatonsService.
		/// Returns null if not found or service unavailable.
		/// </summary>
		public static object GetAutomaton(int id) {
			if (id <= 0) return null;
			EnsureCached();

			var service = GameReflection.GetService(_automatonsServiceProperty);
			if (service == null) return null;

			return ReflectionHelper.Invoke(_getAutomatonMethod, service, id);
		}

		/// <summary>
		/// Check if an automaton with the given ID is alive.
		/// </summary>
		public static bool IsAlive(int id) {
			if (id <= 0) return false;
			EnsureCached();

			var service = GameReflection.GetService(_automatonsServiceProperty);
			if (service == null) return false;

			return ReflectionHelper.InvokeBool(_isAliveMethod, service, id);
		}

		// ========================================
		// MODEL INFO
		// ========================================

		/// <summary>
		/// Get the localized display name of an automaton's model (e.g. "Hauler").
		/// The actor must be an Automaton instance.
		/// </summary>
		public static string GetAutomatonDisplayName(object actor) {
			if (actor == null) return null;
			EnsureCached();

			var model = ReflectionHelper.GetField(_automatonModelField, actor);
			if (model == null) return null;

			return ReflectionHelper.GetLocaString(_modelDisplayNameField, model);
		}

		// ========================================
		// BUILDING ACCESS
		// ========================================

		/// <summary>
		/// Get list of loose automaton IDs attached to a production building.
		/// Loose automatons don't occupy worker slots.
		/// Returns empty list on failure or if building has no loose automatons.
		/// </summary>
		public static List<int> GetLooseAutomatonIds(object building) {
			var result = new List<int>();
			if (building == null) return result;
			EnsureCached();

			var pbState = ReflectionHelper.GetProp(_productionBuildingStateProperty, building);
			if (pbState == null) return result;

			var rawList = ReflectionHelper.GetList(_looseAutomatonsField, pbState);
			if (rawList == null) return result;

			foreach (var item in rawList) {
				if (item is int id) {
					result.Add(id);
				}
			}

			return result;
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(AutomatonReflection), "AutomatonReflection");
		}
	}
}
