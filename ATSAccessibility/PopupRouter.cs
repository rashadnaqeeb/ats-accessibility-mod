using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Delegate-based popup routing. Replaces the if/else chains in AccessibilityCore
	/// for OnPopupShown/OnPopupHidden with a registration list.
	/// </summary>
	public class PopupRouter {
		private struct Registration {
			public Func<object, bool> CanHandle;
			public Action<object> OnShow;
			public Action<object> OnHide;
			public Action ForceClose;
			public KeyboardManager.NavigationContext Context;
		}

		private readonly List<Registration> _registrations = new List<Registration>();
		private readonly DeedsOverlay _deedsOverlay;
		private readonly UINavigator _uiNavigator;
		private readonly KeyboardManager _keyboardManager;

		public PopupRouter(DeedsOverlay deedsOverlay, UINavigator uiNavigator, KeyboardManager keyboardManager) {
			_deedsOverlay = deedsOverlay;
			_uiNavigator = uiNavigator;
			_keyboardManager = keyboardManager;
		}

		/// <summary>
		/// Full registration with separate show/hide/forceClose callbacks and optional context.
		/// </summary>
		public void Register(Func<object, bool> canHandle, Action<object> onShow, Action<object> onHide, Action forceClose, KeyboardManager.NavigationContext context = KeyboardManager.NavigationContext.Popup) {
			_registrations.Add(new Registration {
				CanHandle = canHandle,
				OnShow = onShow,
				OnHide = onHide,
				ForceClose = forceClose,
				Context = context
			});
		}

		/// <summary>
		/// Convenience overload: onHide calls overlay.Close(), forceClose calls overlay.Close().
		/// </summary>
		public void Register(Func<object, bool> canHandle, Action<object> onShow, MenuBase overlay) {
			Register(canHandle, onShow, _ => overlay.Close(), () => overlay.Close());
		}

		/// <summary>
		/// Route a popup-shown event to the matching registration.
		/// Falls back to deeds capture/suspend, then UINavigator.
		/// </summary>
		public void HandlePopupShown(object popup) {
			foreach (var reg in _registrations) {
				if (reg.CanHandle(popup)) {
					reg.OnShow(popup);
					_keyboardManager?.SetContext(reg.Context);
					return;
				}
			}

			// Deeds child-capture fallback
			if (_deedsOverlay != null && _deedsOverlay.ShouldCaptureNextPopup) {
				Debug.Log("[ATSAccessibility] Capturing reward popup as deeds child");
				_deedsOverlay.SetChildPopup(popup);
				return;
			}

			// Deeds suspend fallback
			if (_deedsOverlay != null && _deedsOverlay.IsActive) {
				_deedsOverlay.Suspend();
			}

			// Generic UINavigator fallback
			_uiNavigator?.OnPopupShown(popup);
			_keyboardManager?.SetContext(KeyboardManager.NavigationContext.Popup);
		}

		/// <summary>
		/// Route a popup-hidden event to the matching registration.
		/// Returns true if the caller should proceed with context restoration,
		/// false if deeds handled it internally (no context change needed).
		/// </summary>
		public bool HandlePopupHidden(object popup) {
			foreach (var reg in _registrations) {
				if (reg.CanHandle(popup)) {
					reg.OnHide(popup);
					return true;
				}
			}

			// Deeds child-popup fallback
			if (_deedsOverlay != null && _deedsOverlay.IsActive && _deedsOverlay.HasChildPopup) {
				_deedsOverlay.ClearChildPopup();
				return false;
			}

			// Deeds resume fallback
			if (_deedsOverlay != null && _deedsOverlay.IsSuspended) {
				Debug.Log("[ATSAccessibility] Resuming Deeds overlay after child popup closed");
				_deedsOverlay.Resume();
				return false;
			}

			// Generic UINavigator fallback
			_uiNavigator?.OnPopupHidden(popup);
			return true;
		}

		/// <summary>
		/// Force-close every registered overlay.
		/// </summary>
		public void CloseAll() {
			foreach (var reg in _registrations) {
				reg.ForceClose();
			}
		}
	}
}
