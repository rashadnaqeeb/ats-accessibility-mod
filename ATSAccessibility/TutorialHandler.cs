using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles tutorial tooltip detection and announcement.
    /// Subscribes to TutorialService.Phase changes to announce tutorial text.
    /// Separate module that can be expanded for generic tooltip support.
    /// </summary>
    /// <remarks>
    /// TODO: Known issues to revisit:
    /// - TimeControl and Wood phases don't announce (their TutorialTooltip hides too quickly)
    /// - Attempted timestamp-based fix caused announcements to fire out of order
    /// - Need different approach: perhaps polling for tooltip visibility, or capturing text immediately on phase change
    /// - Working phases: CameraControl, Impatience, Reputation (TutorialTexts), ReputationPick (TutorialTooltip stays active)
    /// </remarks>
    public class TutorialHandler
    {
        private IDisposable _phaseSubscription;
        private bool _subscribed = false;
        private Action _onPhaseChangedCallback;

        // Cached type for finding TutorialTooltip
        private Type _tutorialTooltipType = null;

        // Cached tooltip reference for visibility checks
        private Component _cachedTooltip = null;

        // Cached TutorialTexts references for positioned text tutorials
        private GameObject _cachedTutorialTexts = null;
        private TMP_Text _cachedTextElement = null;

        // Last announced phase and text for re-reading
        private string _lastAnnouncedPhase = null;
        private string _lastAnnouncedText = null;

        /// <summary>
        /// Whether a tutorial is currently visible and blocking interaction.
        /// </summary>
        public bool IsTutorialActive => CheckTutorialVisible();

        /// <summary>
        /// Create a TutorialHandler with a callback for when phase changes.
        /// The callback should schedule AnnounceTooltip() with a delay.
        /// </summary>
        public TutorialHandler(Action onPhaseChanged)
        {
            _onPhaseChangedCallback = onPhaseChanged;
        }

        /// <summary>
        /// Try to subscribe to TutorialService.Phase changes.
        /// Call this periodically until subscribed.
        /// </summary>
        public void TrySubscribe()
        {
            if (_subscribed) return;

            var phaseObservable = GameReflection.GetTutorialPhaseObservable();
            if (phaseObservable == null)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: Phase observable not available yet");
                return;
            }

            Debug.Log($"[ATSAccessibility] TutorialHandler: Got Phase observable: {phaseObservable.GetType().FullName}");

            _phaseSubscription = GameReflection.SubscribeToObservable(phaseObservable, OnPhaseChanged);
            _subscribed = _phaseSubscription != null;

            if (_subscribed)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: Subscribed to tutorial phase changes");
            }
        }

        /// <summary>
        /// Dispose the subscription and reset state.
        /// </summary>
        public void Dispose()
        {
            _phaseSubscription?.Dispose();
            _phaseSubscription = null;
            _subscribed = false;
            _lastAnnouncedPhase = null;
            _lastAnnouncedText = null;
            _cachedTooltip = null;
            _cachedTutorialTexts = null;
            _cachedTextElement = null;
            Debug.Log("[ATSAccessibility] TutorialHandler: Disposed");
        }

        /// <summary>
        /// Re-announce the current tutorial text.
        /// Call this when user presses a key to re-read.
        /// </summary>
        public void ReannounceCurrentTutorial()
        {
            if (!string.IsNullOrEmpty(_lastAnnouncedText) && IsTutorialActive)
            {
                Debug.Log($"[ATSAccessibility] Tutorial (re-read): {_lastAnnouncedText}");
                Speech.Say(_lastAnnouncedText);
            }
        }

        /// <summary>
        /// Check if any tutorial UI is currently visible.
        /// Uses cached references - no expensive scene searches.
        /// </summary>
        private bool CheckTutorialVisible()
        {
            // Skip if no phase set or nothing was announced
            if (string.IsNullOrEmpty(_lastAnnouncedPhase) || _lastAnnouncedPhase == "None")
            {
                return false;
            }

            // Check cached TutorialTooltip
            if (_cachedTooltip != null && _cachedTooltip.gameObject.activeInHierarchy)
            {
                return true;
            }

            // Check cached TutorialTexts element
            if (_cachedTextElement != null && _cachedTextElement.gameObject.activeInHierarchy)
            {
                return true;
            }

            // Nothing cached or cached objects are inactive
            return false;
        }

        /// <summary>
        /// Called when tutorial phase changes.
        /// </summary>
        private void OnPhaseChanged(object phase)
        {
            string phaseName = phase?.ToString() ?? "None";
            Debug.Log($"[ATSAccessibility] Tutorial phase changed: {phaseName}");

            // Store phase name for announcement lookup
            _lastAnnouncedPhase = phaseName;

            // Notify the host to schedule announcement with delay
            _onPhaseChangedCallback?.Invoke();
        }

        /// <summary>
        /// Check if a phase is an end marker (no tooltip shown).
        /// </summary>
        private bool IsEndMarkerPhase(string phase)
        {
            return phase == "Tut1End" || phase == "Tut2End" ||
                   phase == "Tut3End" || phase == "Tut4End" ||
                   phase == "TutorialEnd";
        }

        /// <summary>
        /// Find the TutorialTooltip and announce its text.
        /// Call this after a delay when phase changes.
        /// </summary>
        public void AnnounceTooltip()
        {
            Debug.Log($"[ATSAccessibility] TutorialHandler.AnnounceTooltip() entered, phase={_lastAnnouncedPhase}");

            // Skip "None" phase
            if (_lastAnnouncedPhase == "None")
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: Skipping None phase");
                return;
            }

            // Skip end marker phases (they don't have tooltips)
            if (IsEndMarkerPhase(_lastAnnouncedPhase))
            {
                Debug.Log($"[ATSAccessibility] TutorialHandler: Skipping end marker phase {_lastAnnouncedPhase}");
                return;
            }

            // Try TutorialTooltip first (popup style)
            if (TryAnnounceTutorialTooltip())
            {
                return;
            }

            // Fall back to TutorialTexts (positioned text)
            if (TryAnnounceTutorialTexts())
            {
                return;
            }

            Debug.Log("[ATSAccessibility] TutorialHandler: No tutorial content found");
        }

        /// <summary>
        /// Try to announce from TutorialTooltip (popup style).
        /// Finds tooltip even if inactive (it may have been hidden quickly).
        /// </summary>
        private bool TryAnnounceTutorialTooltip()
        {
            // Cache the type
            if (_tutorialTooltipType == null)
            {
                _tutorialTooltipType = GameReflection.FindTypeByName("TutorialTooltip");
            }

            if (_tutorialTooltipType == null)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: TutorialTooltip type not found");
                return false;
            }

            // Find tooltip even if inactive (it may have been hidden before we got here)
            // Use FindObjectsOfType with includeInactive: true
            var tooltips = UnityEngine.Object.FindObjectsOfType(_tutorialTooltipType, true) as Component[];
            var tooltip = tooltips?.FirstOrDefault();

            if (tooltip == null)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: No TutorialTooltip instance found");
                _cachedTooltip = null;
                return false;
            }

            Debug.Log($"[ATSAccessibility] TutorialHandler: Found TutorialTooltip, active={tooltip.gameObject.activeInHierarchy}");

            // Only read text if tooltip is actually active - otherwise we get stale text from previous use
            if (!tooltip.gameObject.activeInHierarchy)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: Tooltip is inactive, skipping to avoid stale text");
                _cachedTooltip = null;
                return false;
            }

            // Cache for visibility checks
            _cachedTooltip = tooltip;

            // Try to find text at Content/Text path first (the TextTyper component)
            Transform contentText = tooltip.transform.Find("Content/Text");
            TMP_Text textComponent = contentText?.GetComponent<TMP_Text>();

            // Also look for speaker name at Content/Name
            Transform contentName = tooltip.transform.Find("Content/Name");
            TMP_Text nameComponent = contentName?.GetComponent<TMP_Text>();

            string contentTextStr = textComponent?.text;
            string speakerText = nameComponent?.text;

            Debug.Log($"[ATSAccessibility] TutorialHandler: Content/Text={contentTextStr?.Length ?? 0} chars, Content/Name={speakerText ?? "null"}");

            // If Content/Text path didn't work, fall back to scanning all text components
            if (string.IsNullOrEmpty(contentTextStr))
            {
                var textComponents = tooltip.GetComponentsInChildren<TMP_Text>(true);
                Debug.Log($"[ATSAccessibility] TutorialHandler: Scanning {textComponents.Length} TMP_Text components");

                foreach (var tc in textComponents)
                {
                    if (tc == null || string.IsNullOrEmpty(tc.text)) continue;

                    string objName = tc.gameObject.name.ToLower();
                    string text = tc.text;

                    Debug.Log($"[ATSAccessibility] TutorialHandler: TMP_Text '{tc.gameObject.name}' = '{text.Substring(0, Math.Min(50, text.Length))}'");

                    // "Label" or "Text" contains the content, "Name" contains the speaker
                    if (objName == "label" || objName == "text" || objName.Contains("content") || objName.Contains("desc") || objName.Contains("message") || objName.Contains("body"))
                    {
                        contentTextStr = text;
                    }
                    else if (objName == "name" || objName.Contains("speaker") || objName.Contains("title") || objName.Contains("header"))
                    {
                        speakerText = text;
                    }
                    else if (contentTextStr == null && text.Length > 20)
                    {
                        contentTextStr = text;
                    }
                    else if (speakerText == null)
                    {
                        speakerText = text;
                    }
                }
            }

            // If still no content, we can't announce
            if (string.IsNullOrEmpty(contentTextStr))
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: No text content found in tooltip");
                return false;
            }

            // Build announcement with speaker if available
            string announcement;
            if (!string.IsNullOrEmpty(speakerText))
            {
                announcement = $"{speakerText}. {contentTextStr}";
            }
            else
            {
                announcement = contentTextStr;
            }

            _lastAnnouncedText = announcement;
            Debug.Log($"[ATSAccessibility] Tutorial: {announcement}");
            Speech.Say(announcement);
            return true;
        }

        /// <summary>
        /// Try to announce from TutorialTexts (positioned text style).
        /// Maps phase names to specific text elements.
        /// </summary>
        private bool TryAnnounceTutorialTexts()
        {
            // Find TutorialTexts GameObject
            if (_cachedTutorialTexts == null || !_cachedTutorialTexts.activeInHierarchy)
            {
                _cachedTutorialTexts = null;
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name == "TutorialTexts" && obj.activeInHierarchy)
                    {
                        _cachedTutorialTexts = obj;
                        Debug.Log("[ATSAccessibility] TutorialHandler: Found TutorialTexts GameObject");
                        break;
                    }
                }
            }

            if (_cachedTutorialTexts == null)
            {
                Debug.Log("[ATSAccessibility] TutorialHandler: TutorialTexts not found");
                return false;
            }

            // Map phase to text element name
            string targetTextName = GetTextNameForPhase(_lastAnnouncedPhase);
            if (targetTextName == null)
            {
                Debug.Log($"[ATSAccessibility] TutorialHandler: No text mapping for phase {_lastAnnouncedPhase}");
                return false;
            }

            // Find the specific text element - must be active
            var textComponents = _cachedTutorialTexts.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tc in textComponents)
            {
                if (tc.gameObject.name == targetTextName &&
                    tc.gameObject.activeInHierarchy &&
                    !string.IsNullOrEmpty(tc.text))
                {
                    _cachedTextElement = tc;
                    _cachedTooltip = null; // Clear other cache

                    string announcement = $"The Queen's Envoy. {tc.text}";
                    _lastAnnouncedText = announcement;
                    Debug.Log($"[ATSAccessibility] Tutorial (Texts/{targetTextName}): {announcement}");
                    Speech.Say(announcement);
                    return true;
                }
            }

            Debug.Log($"[ATSAccessibility] TutorialHandler: Text element '{targetTextName}' not found or inactive");
            _cachedTextElement = null;
            return false;
        }

        /// <summary>
        /// Map tutorial phase to the corresponding text element name in TutorialTexts.
        /// </summary>
        private string GetTextNameForPhase(string phase)
        {
            switch (phase)
            {
                case "CameraControl":
                    return "Mid";
                case "Impatience":
                    return "LowerRight";
                case "Reputation":
                    return "LowerLeft";
                case "RacesHUD":
                case "RacesHUDFirst":
                    return "RacesHUDFirstRight";
                case "RacesHUDSecond":
                    return "RacesHUDSecondRight";
                case "RacesHUDBelow":
                    return "RacesHUDBelowPanel";
                case "Hostility":
                case "HostilityHUD":
                    return "HostilityHUD";
                case "SeasonalEffects":
                    return "SeasonalEffects";
                default:
                    return null;
            }
        }

    }
}
