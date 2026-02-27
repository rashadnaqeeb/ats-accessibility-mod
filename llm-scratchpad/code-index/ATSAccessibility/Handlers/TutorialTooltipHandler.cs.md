# TutorialTooltipHandler.cs
Key handler for TutorialTooltip navigation.
Provides keyboard support for advancing through tutorial text.
Auto-announces text when tooltip becomes visible or text changes.

State machine:
- Engaged when tooltip appears or text changes (captures keys)
- Disengaged when Enter pressed with no continue button (lets player act)
- Re-engages when text changes

## class TutorialTooltipHandler: IKeyHandler, IHelpProvider (line 19)

### Fields
- private readonly UINavigator _uiNavigator (line 21)
  Used to check if a popup is blocking (suppresses IsActive when HasActivePopup is true).
- private bool _isVisible (line 24)
  Cached tooltip visibility; updated by CheckForTextChanges to avoid reflection in IsActive hot path.
- private bool _wasVisible (line 25)
- private string _lastText (line 26)
- private string _lastAnnouncedText (line 27)
  Tracks what was last spoken to prevent re-announcing the same text during brief visibility flickers between phases.
- private bool _isEngaged (line 28)
  Whether this handler is capturing keys.
- private int _lastPhase (line 29)
  Tracks the last known phase ID; used as fallback when GetCurrentPhase returns -1 during TextTyper transitions.
- private bool _forceEngaged (line 30)
  When true, keeps handler engaged regardless of tooltip visibility (used for world map tutorial after MetaRewardsPopup).
- private static readonly Dictionary<int, string> _accessibilityMessages (line 54)
  Maps TutorialPhase IDs to custom accessibility text that replaces the game's default tooltip text for those phases.
- private static readonly List<HelpEntry> _helpEntries (line 72)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 76)
- public string HelpContextName { get; } (line 77)
- public bool IsActive { get; } (line 88)
  True when (_forceEngaged OR (_isVisible AND _isEngaged)) AND no popup is blocking. _forceEngaged bypasses visibility check for world map tutorial phase.

### Methods
- public TutorialTooltipHandler(UINavigator uiNavigator) (line 32)
- public void ForceEngage() (line 41)
  Sets _isVisible, _isEngaged, and _forceEngaged to true. Called externally when tutorial is detected via polling; _forceEngaged prevents CheckForTextChanges from disengaging.
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 78)
- public IReadOnlyList<string> GetPassthroughKeys() (line 79)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 97)
  Enter: triggers continue if button active, or says "Wait for animation" if button expected but not yet active, or disengages if no button expected. Escape: disengages without closing tooltip. Arrow keys: re-read current text. All other keys consumed.
- public void CheckForTextChanges() (line 144)
  Called each frame from AccessibilityCore.Update. Updates _isVisible, checks for phase/text changes, announces new text (skipping if _forceEngaged already announced it or if same text was already announced). Resets engagement when tooltip hidden (unless _forceEngaged).
- private string GetTextForPhase(int phase, string gameText) (line 190)
  Returns the custom accessibility message for the phase if one exists, otherwise the game's text.
- private void AnnounceCurrentText() (line 201)
  Re-reads and speaks the current phase's text (arrow key re-read behavior).
