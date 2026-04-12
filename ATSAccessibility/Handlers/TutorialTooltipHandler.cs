using ATSAccessibility.Overlays;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Key handler for TutorialTooltip navigation.
	/// Provides keyboard support for advancing through tutorial text.
	/// Auto-announces text when tooltip becomes visible or text changes.
	///
	/// State machine:
	/// - Engaged when tooltip appears or text changes (captures keys)
	/// - Disengaged when Enter pressed with no continue button (lets player act)
	/// - Re-engages when text changes
	/// </summary>
	public class TutorialTooltipHandler: IKeyHandler, IHelpProvider {
		// Reference to UINavigator to check if a popup is blocking
		private readonly UINavigator _uiNavigator;

		// State tracking (updated by CheckForTextChanges in Update loop)
		private bool _isVisible = false;   // Cached visibility to avoid reflection in IsActive
		private bool _wasVisible = false;
		private string _lastText = null;
		private string _lastAnnouncedText = null;  // Track what we've actually announced to prevent duplicates
		private bool _isEngaged = false;   // Whether we're capturing keys
		private int _lastPhase = -1;       // Track phase changes
		private bool _forceEngaged = false; // When true, stay engaged regardless of tooltip visibility

		public TutorialTooltipHandler(UINavigator uiNavigator) {
			_uiNavigator = uiNavigator;
		}

		/// <summary>
		/// Clear all state. Called on scene unload to prevent stale flags
		/// from blocking input in the next session.
		/// </summary>
		public void Reset() {
			_isVisible = false;
			_wasVisible = false;
			_lastText = null;
			_lastAnnouncedText = null;
			_isEngaged = false;
			_lastPhase = -1;
			_forceEngaged = false;
		}

		/// <summary>
		/// Force the handler to engage and capture keys.
		/// Called externally when we detect the tooltip via polling.
		/// Sets _forceEngaged flag to prevent CheckForTextChanges from disengaging.
		/// </summary>
		public void ForceEngage() {
			_isVisible = true;
			_isEngaged = true;
			_forceEngaged = true;
		}

		// ========================================
		// HARDCODED ACCESSIBILITY MESSAGES
		// ========================================
		// Maps TutorialPhase ID to custom accessibility text.
		// If a phase has a custom message, it replaces the game's text.
		// If not, the game's text is used as fallback.

		private static Dictionary<int, string> _accessibilityMessages => new Dictionary<int, string>
		{
            // Tutorial 1: Basics
            { 10, Strings.Get("handler.tutorial.phase_10") }, // CameraControl
            { 20, Strings.Get("handler.tutorial.phase_20") }, // Impatience
            { 30, Strings.Get("handler.tutorial.phase_30") }, // Reputation
            { 35, Strings.Get("handler.tutorial.phase_35") }, // ReputationPick
            { 40, Strings.Get("handler.tutorial.phase_40") }, // TimeControl
            { 50, Strings.Get("handler.tutorial.phase_50") }, // Wood

            // Tutorial 4: The Cycle
            { 340, Strings.Get("handler.tutorial.phase_340") }, // CyclePreFinish
        };

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("Arrows", Strings.Get("handler.tutorial_tooltip.help.reread_text")),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Terminator;
		public string HelpContextName => "Tutorial";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		// Active when tooltip visible AND we're engaged AND no popup is blocking input
		// OR when force-engaged (for world map tutorial after MetaRewardsPopup)
		// This prevents consuming keys when MetaRewardsPopup or other popups are open
		public bool IsActive {
			get {
				bool hasPopup = _uiNavigator?.HasActivePopup ?? false;
				// Force engaged bypasses visibility check (for world map tutorial)
				bool result = (_forceEngaged || (_isVisible && _isEngaged)) && !hasPopup;
				return result;
			}
		}

		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					if (TutorialReflection.IsButtonActive() && TutorialReflection.TriggerContinue()) {
						// Success - clear force engaged state
						_forceEngaged = false;
					} else if (TutorialReflection.IsButtonExpected()) {
						// Button will appear after animation - wait
						Speech.Say(Strings.Get("handler.tutorial.wait_animation"));
					} else {
						// No button expected - disengage so player can perform the required action
						_isEngaged = false;
						_forceEngaged = false;
						Speech.Say(Strings.Get("handler.tutorial.perform_action"));
					}
					return true;

				case KeyCode.Escape:
					// Disengage so player can interact with game, but don't close tooltip
					_isEngaged = false;
					_forceEngaged = false;
					Speech.Say(Strings.Get("handler.tutorial.paused"));
					return true;

				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
				case KeyCode.LeftArrow:
				case KeyCode.RightArrow:
					// Re-read current text on arrow keys
					AnnounceCurrentText();
					return true;

				default:
					// Consume all other keys while engaged
					return true;
			}
		}

		// ========================================
		// AUTO-ANNOUNCE POLLING
		// ========================================

		/// <summary>
		/// Called from AccessibilityCore's Update loop to detect visibility
		/// and text changes for auto-announcement.
		/// </summary>
		public void CheckForTextChanges() {
			// Update cached visibility (used by IsActive to avoid reflection)
			_isVisible = TutorialReflection.IsTooltipVisible();

			if (_isVisible) {
				int currentPhase = TutorialReflection.GetCurrentPhase();
				string gameText = TutorialReflection.GetCurrentText();

				// Use custom message if available, otherwise use game text.
				// Use _lastPhase as fallback when currentPhase is -1 to avoid
				// losing the custom message lookup during TextTyper transitions.
				int effectivePhase = currentPhase != -1 ? currentPhase : _lastPhase;
				string textToAnnounce = GetTextForPhase(effectivePhase, gameText);

				// Check for phase/text changes
				bool phaseChanged = currentPhase != _lastPhase && currentPhase != -1;
				bool textChanged = gameText != _lastText && !string.IsNullOrEmpty(gameText);

				// Announce when tooltip first becomes visible OR when text actually changes
				if (!_wasVisible || phaseChanged || textChanged) {
					if (!string.IsNullOrEmpty(textToAnnounce)) {
						_isEngaged = true;  // Re-engage on new text
											// Don't announce if force engaged (already announced by polling)
											// Also don't re-announce if we already announced this exact text
						if (!_forceEngaged && textToAnnounce != _lastAnnouncedText) {
							Speech.Say(textToAnnounce);
							_lastAnnouncedText = textToAnnounce;
						}
						_lastText = gameText;
						_lastPhase = currentPhase;
					}
				}
			} else if (!_forceEngaged) {
				// Reset when tooltip is hidden (unless force engaged).
				// Keep _lastAnnouncedText so brief visibility flickers between phases
				// don't cause the same text to be announced twice.
				_isEngaged = false;
			}

			_wasVisible = _isVisible;
		}

		/// <summary>
		/// Get the text to announce for a given phase.
		/// Returns custom accessibility message if available, otherwise the game's text.
		/// </summary>
		private string GetTextForPhase(int phase, string gameText) {
			if (_accessibilityMessages.TryGetValue(phase, out string customMessage)) {
				return customMessage;
			}
			return gameText;
		}

		// ========================================
		// HELPERS
		// ========================================

		private void AnnounceCurrentText() {
			int currentPhase = TutorialReflection.GetCurrentPhase();
			string gameText = TutorialReflection.GetCurrentText();
			string text = GetTextForPhase(currentPhase, gameText);

			if (!string.IsNullOrEmpty(text)) {
				Speech.Say(text);
			} else {
				Speech.Say(Strings.Get("handler.tutorial.no_text"));
			}
		}
	}
}
