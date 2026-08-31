using ATSAccessibility.Panels;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Core {
	/// <summary>
	/// Centralized keyboard input handling with handler chain pattern.
	/// Handlers are processed in priority order; the first active handler
	/// that returns true from ProcessKey() consumes the key event.
	/// </summary>
	public class KeyboardManager {
		/// <summary>
		/// Key modifiers state (Ctrl, Alt, Shift) plus the typed character from the
		/// current OnGUI event. TypedChar is 0 for non-character key events (arrows,
		/// function keys, etc.) and carries Unicode letters for type-ahead search
		/// across locales (Latin, Cyrillic, CJK, etc.).
		/// </summary>
		public struct KeyModifiers {
			public bool Control { get; }
			public bool Alt { get; }
			public bool Shift { get; }
			public char TypedChar { get; }

			public KeyModifiers(bool control, bool alt, bool shift, char typedChar) {
				Control = control;
				Alt = alt;
				Shift = shift;
				TypedChar = typedChar;
			}
		}

		/// <summary>
		/// Navigation context for debugging and logging purposes.
		/// With the full handler chain, this is purely informational.
		/// </summary>
		public enum NavigationContext {
			None,
			Popup,
			Map,
			WorldMap,
			Dialogue,
			Encyclopedia,
			Embark
		}

		// Current navigation context (informational only)
		public NavigationContext CurrentContext { get; private set; } = NavigationContext.None;

		// Handler chain in priority order
		private readonly List<IKeyHandler> _handlers = new List<IKeyHandler>();

		/// <summary>Ordered handler list for help collection.</summary>
		public IReadOnlyList<IKeyHandler> Handlers => _handlers;

		// Help overlay for F12
		private HelpOverlay _helpOverlay;

		/// <summary>
		/// Register a key handler. Handlers are processed in registration order.
		/// </summary>
		public void RegisterHandler(IKeyHandler handler) {
			if (handler != null && !_handlers.Contains(handler)) {
				_handlers.Add(handler);
				Debug.Log($"[ATSAccessibility] Registered key handler: {handler.GetType().Name}");
			}
		}

		/// <summary>
		/// Set the help overlay reference for F12 interception.
		/// </summary>
		public void SetHelpOverlay(HelpOverlay overlay) {
			_helpOverlay = overlay;
		}

		/// <summary>
		/// Set the current navigation context (informational only).
		/// </summary>
		public void SetContext(NavigationContext context) {
			if (CurrentContext != context) {
				Debug.Log($"[ATSAccessibility] Navigation context changed: {CurrentContext} -> {context}");
				CurrentContext = context;
			}
		}

		/// <summary>
		/// Process a key event from OnGUI.
		/// Iterates through handlers in priority order until one handles the key.
		/// </summary>
		public void ProcessKeyEvent(KeyCode keyCode, KeyModifiers modifiers = default) {
			// Ignore modifier-only key presses — they carry no action on their own
			// and would cause handlers to react to bare Alt/Ctrl/Shift key-down events
			if (IsModifierKey(keyCode))
				return;

			// F12: toggle context-sensitive help overlay
			if (keyCode == KeyCode.F12 && _helpOverlay != null) {
				if (_helpOverlay.IsOpen) {
					_helpOverlay.Close();
				} else {
					var (_, entries) = HelpCollector.Collect(_handlers);
					_helpOverlay.ShowHelp(entries);
				}
				return;
			}

			foreach (var handler in _handlers) {
				// Isolate each handler: an exception in one handler must not abort
				// key dispatch for the handlers below it, or the whole keyboard
				// pipeline dies on every keypress.
				bool active;
				try {
					active = handler.IsActive;
					_lastKnownActive[handler] = active;
				} catch (System.Exception ex) {
					LogHandlerError(handler, keyCode, ex);
					// Can't tell if this handler wants the key — fall back to its
					// last successful answer (inactive when unknown). A handler
					// that was active is usually a modal overlay; skipping it
					// would leak every keypress to the map handlers stacked
					// beneath the open popup, matching the ProcessKey policy
					// below of never letting a broken active handler's key fall
					// through.
					_lastKnownActive.TryGetValue(handler, out active);
				}
				if (!active) continue;

				try {
					if (handler.ProcessKey(keyCode, modifiers)) {
						return; // Key was handled
					}
				} catch (System.Exception ex) {
					LogHandlerError(handler, keyCode, ex);
					// The key was aimed at this ACTIVE handler (often a modal
					// overlay). Treat it as consumed — letting it fall through
					// would hand the same keypress to the map handlers behind
					// the open popup, possibly after a partial side effect.
					return;
				}
			}

			// Key was not handled by any handler - let it pass through to the game
		}

		// Last successful IsActive answer per handler, used when the getter itself
		// throws (game-API drift). Bounded by the registered handler count.
		private readonly Dictionary<IKeyHandler, bool> _lastKnownActive = new Dictionary<IKeyHandler, bool>();

		// Handlers whose failure was already reported — logged once per handler per
		// session (matching ReflectionHelper.LogAccessError): a persistently broken
		// handler under held arrow keys must not write stack traces to the log on
		// every keypress.
		private readonly HashSet<string> _loggedHandlerErrors = new HashSet<string>();

		private void LogHandlerError(IKeyHandler handler, KeyCode keyCode, System.Exception ex) {
			string name = handler.GetType().Name;
			if (_loggedHandlerErrors.Add(name))
				Debug.LogError($"[ATSAccessibility] Handler {name} threw on {keyCode} (logged once): {ex}");
		}

		private static bool IsModifierKey(KeyCode keyCode) {
			switch (keyCode) {
				case KeyCode.LeftAlt:
				case KeyCode.RightAlt:
				case KeyCode.LeftControl:
				case KeyCode.RightControl:
				case KeyCode.LeftShift:
				case KeyCode.RightShift:
				case KeyCode.LeftCommand:
				case KeyCode.RightCommand:
					return true;
				default:
					return false;
			}
		}
	}
}
