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
		/// Key modifiers state (Ctrl, Alt, Shift).
		/// </summary>
		public struct KeyModifiers {
			public bool Control { get; }
			public bool Alt { get; }
			public bool Shift { get; }

			public KeyModifiers(bool control, bool alt, bool shift) {
				Control = control;
				Alt = alt;
				Shift = shift;
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
					var (name, entries) = HelpCollector.Collect(_handlers);
					_helpOverlay.ShowHelp(name, entries);
				}
				return;
			}

			foreach (var handler in _handlers) {
				if (handler.IsActive && handler.ProcessKey(keyCode, modifiers)) {
					return; // Key was handled
				}
			}

			// Key was not handled by any handler - let it pass through to the game
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
