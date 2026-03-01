using System;
using System.Reflection;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to WorldEventPopup and WorldEventModel.
	/// Used for navigating world event decision screens on the world map.
	///
	/// Note: Instance data (popup, model, state) is NOT cached here - callers
	/// must pass instances as parameters. Only reflection metadata is cached.
	/// </summary>
	public static class WorldEventReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================
		private static bool _cached;

		// WorldEventPopup type detection
		private static Type _worldEventPopupType;

		// WorldEventPopup.worldEvent field (WorldEvent instance)
		private static FieldInfo _popupWorldEventField;

		// WorldEvent.model field (WorldEventModel)
		private static FieldInfo _worldEventModelField;

		// WorldEvent.state field (WorldEventState)
		private static FieldInfo _worldEventStateField;

		// WorldEventModel properties
		private static FieldInfo _modelDisplayNameField;       // LocaText displayName
		private static FieldInfo _modelDescriptionField;       // LocaText description
		private static FieldInfo _modelOptionsField;           // WorldEventLogic[] options

		// WorldEventModel methods
		private static MethodInfo _modelGetDescriptionForOptionMethod;   // GetDescriptionForOption(int)
		private static MethodInfo _modelCanExecuteMethod;                 // CanExecute(int)
		private static MethodInfo _modelGetExecutionBlockReasonMethod;    // GetExecutionBlockReason(int)
		private static MethodInfo _modelExecuteDecisionMethod;            // ExecuteDecision(WorldEventState, int)
		private static MethodInfo _modelGetResultDescriptionMethod;      // GetResultDescriptionForOption(int)


		// ========================================
		// TYPE DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a WorldEventPopup.
		/// </summary>
		public static bool IsWorldEventPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			if (_worldEventPopupType == null) return false;
			return _worldEventPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// INSTANCE EXTRACTION
		// ========================================

		/// <summary>
		/// Extract the WorldEvent instance from a WorldEventPopup.
		/// </summary>
		public static object GetWorldEvent(object popup) {
			if (popup == null) return null;
			EnsureCached();
			return ReflectionHelper.GetField(_popupWorldEventField, popup);
		}

		/// <summary>
		/// Extract the WorldEventModel from a WorldEvent instance.
		/// </summary>
		public static object GetModel(object worldEvent) {
			if (worldEvent == null) return null;
			EnsureCached();
			return ReflectionHelper.GetField(_worldEventModelField, worldEvent);
		}

		/// <summary>
		/// Extract the WorldEventState from a WorldEvent instance.
		/// </summary>
		public static object GetState(object worldEvent) {
			if (worldEvent == null) return null;
			EnsureCached();
			return ReflectionHelper.GetField(_worldEventStateField, worldEvent);
		}

		// ========================================
		// DATA ACCESS
		// ========================================

		/// <summary>
		/// Get the event's display name.
		/// </summary>
		public static string GetEventName(object model) {
			if (model == null) return null;
			EnsureCached();
			return ReflectionHelper.GetLocaString(_modelDisplayNameField, model);
		}

		/// <summary>
		/// Get the event's description.
		/// </summary>
		public static string GetEventDescription(object model) {
			if (model == null) return null;
			EnsureCached();
			return ReflectionHelper.GetLocaString(_modelDescriptionField, model);
		}

		/// <summary>
		/// Get the number of decision options.
		/// </summary>
		public static int GetOptionCount(object model) {
			if (model == null) return 0;
			EnsureCached();
			var options = ReflectionHelper.GetField(_modelOptionsField, model) as Array;
			return options?.Length ?? 0;
		}

		/// <summary>
		/// Get the description for a specific option.
		/// </summary>
		public static string GetOptionDescription(object model, int index) {
			if (model == null) return null;
			EnsureCached();
			if (index < 0 || index >= GetOptionCount(model)) return null;
			return ReflectionHelper.InvokeString(_modelGetDescriptionForOptionMethod, model, index);
		}

		/// <summary>
		/// Check if an option can be executed.
		/// </summary>
		public static bool CanExecuteOption(object model, int index) {
			if (model == null) return false;
			EnsureCached();
			if (index < 0 || index >= GetOptionCount(model)) return false;
			return ReflectionHelper.InvokeBool(_modelCanExecuteMethod, model, index);
		}

		/// <summary>
		/// Get the reason why an option cannot be executed.
		/// </summary>
		public static string GetExecutionBlockReason(object model, int index) {
			if (model == null) return null;
			EnsureCached();
			if (index < 0 || index >= GetOptionCount(model)) return null;
			return ReflectionHelper.InvokeString(_modelGetExecutionBlockReasonMethod, model, index);
		}

		/// <summary>
		/// Execute the selected decision.
		/// Returns true if the action was triggered (async execution).
		/// </summary>
		public static bool ExecuteDecision(object model, object state, int index) {
			if (model == null || state == null) return false;
			EnsureCached();
			if (index < 0 || index >= GetOptionCount(model)) return false;
			// ExecuteDecision returns UniTask<bool>, just invoke it (fire and forget)
			// The game handles the async flow and will close the popup on success
			return ReflectionHelper.InvokeVoid(_modelExecuteDecisionMethod, model, state, index);
		}

		/// <summary>
		/// Get the result description for a specific option (reward text).
		/// </summary>
		public static string GetResultDescription(object model, int index) {
			if (model == null) return null;
			EnsureCached();
			if (index < 0 || index >= GetOptionCount(model)) return null;
			return ReflectionHelper.InvokeString(_modelGetResultDescriptionMethod, model, index);
		}

		// ========================================
		// REFLECTION CACHING
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("WorldEventReflection", assembly => {
				// Cache WorldEventPopup type
				_worldEventPopupType = assembly.GetType("Eremite.WorldMap.UI.WorldEvents.WorldEventPopup");
				if (_worldEventPopupType != null) {
					_popupWorldEventField = _worldEventPopupType.GetField("worldEvent",
						GameReflection.NonPublicInstance);
				}

				// Cache WorldEvent type
				var worldEventType = assembly.GetType("Eremite.WorldMap.Controllers.WorldEvent");
				if (worldEventType != null) {
					_worldEventModelField = worldEventType.GetField("model",
						GameReflection.PublicInstance | GameReflection.NonPublicInstance);
					_worldEventStateField = worldEventType.GetField("state",
						GameReflection.PublicInstance | GameReflection.NonPublicInstance);
				}

				// Cache WorldEventModel type
				var modelType = assembly.GetType("Eremite.Model.WorldEventModel");
				if (modelType != null) {
					_modelDisplayNameField = modelType.GetField("displayName",
						GameReflection.PublicInstance);
					_modelDescriptionField = modelType.GetField("description",
						GameReflection.PublicInstance);
					_modelOptionsField = modelType.GetField("options",
						GameReflection.PublicInstance);

					_modelGetDescriptionForOptionMethod = modelType.GetMethod("GetDescriptionForOption",
						GameReflection.PublicInstance,
						null, new[] { typeof(int) }, null);
					_modelCanExecuteMethod = modelType.GetMethod("CanExecute",
						GameReflection.PublicInstance,
						null, new[] { typeof(int) }, null);
					_modelGetExecutionBlockReasonMethod = modelType.GetMethod("GetExecutionBlockReason",
						GameReflection.PublicInstance,
						null, new[] { typeof(int) }, null);
					_modelGetResultDescriptionMethod = modelType.GetMethod("GetResultDescriptionForOption",
						GameReflection.PublicInstance,
						null, new[] { typeof(int) }, null);

					// ExecuteDecision takes WorldEventState and int
					var stateType = assembly.GetType("Eremite.WorldMap.WorldEventState");
					if (stateType != null) {
						_modelExecuteDecisionMethod = modelType.GetMethod("ExecuteDecision",
							GameReflection.PublicInstance,
							null, new[] { stateType, typeof(int) }, null);
					}
				}
			});
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(WorldEventReflection), "WorldEventReflection");
		}
	}
}
