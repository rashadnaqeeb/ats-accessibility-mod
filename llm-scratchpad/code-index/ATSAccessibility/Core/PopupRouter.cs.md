# PopupRouter.cs

Delegate-based popup routing. Replaces if/else chains in AccessibilityCore for
OnPopupShown/OnPopupHidden with a registration list.

## class PopupRouter (line 11)

### struct Registration (nested, private) (line 12)
Internal record for a single popup route.

#### Fields
- public Func<object, bool> CanHandle (line 13)
- public Action<object> OnShow (line 14)
- public Action<object> OnHide (line 15)
- public Action ForceClose (line 16)
- public KeyboardManager.NavigationContext Context (line 17)

---

### Fields
- private readonly List<Registration> _registrations (line 20)
- private readonly DeedsOverlay _deedsOverlay (line 21)
- private readonly UINavigator _uiNavigator (line 22)
- private readonly KeyboardManager _keyboardManager (line 23)

### Methods
- public PopupRouter(DeedsOverlay deedsOverlay, UINavigator uiNavigator, KeyboardManager keyboardManager) (line 25)

- public void Register(Func<object, bool> canHandle, Action<object> onShow, Action<object> onHide, Action forceClose, KeyboardManager.NavigationContext context = KeyboardManager.NavigationContext.Popup) (line 34)
  - Full registration with separate show/hide/forceClose callbacks and optional context.

- public void Register(Func<object, bool> canHandle, Action<object> onShow, MenuBase overlay) (line 47)
  - Convenience overload: onHide calls overlay.Close(), forceClose calls overlay.Close().

- public void HandlePopupShown(object popup) (line 55)
  - Routes popup-shown event to the first matching registration. Falls back to: deeds child-capture (ShouldCaptureNextPopup), deeds suspend (IsActive), then UINavigator generic fallback.

- public bool HandlePopupHidden(object popup) (line 86)
  - Routes popup-hidden event. Returns true if the caller should proceed with context restoration, false if deeds handled it internally. Falls back to: deeds child-popup clear (HasChildPopup), deeds resume (IsSuspended), then UINavigator fallback.

- public void CloseAll() (line 115)
  - Force-close every registered overlay by calling each registration's ForceClose action.
