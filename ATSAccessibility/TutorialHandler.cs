using System;
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
    public class TutorialHandler
    {
        private IDisposable _phaseSubscription;
        private bool _subscribed = false;
        private Action _onPhaseChangedCallback;

        // Cached type for finding TutorialTooltip
        private Type _tutorialTooltipType = null;

        // Cached references to avoid expensive FindObjectOfType calls
        private Component _cachedTooltip = null;
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

            _phaseSubscription = SubscribeToObservable(phaseObservable, OnPhaseChanged);
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

            // Check cached text element from TutorialTexts
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

            // Try TutorialTooltip first (popup-style tutorials)
            if (TryAnnounceTutorialTooltip())
            {
                return;
            }

            // Fall back to TutorialTexts (positioned text tutorials)
            if (TryAnnounceTutorialTexts())
            {
                return;
            }

            Debug.Log("[ATSAccessibility] TutorialHandler: No tutorial content found");
        }

        /// <summary>
        /// Try to announce from TutorialTooltip (popup style).
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
                return false;
            }

            // Find the active TutorialTooltip in the scene
            var tooltip = UnityEngine.Object.FindObjectOfType(_tutorialTooltipType) as Component;
            if (tooltip == null || !tooltip.gameObject.activeInHierarchy)
            {
                _cachedTooltip = null; // Clear cache if not found
                return false;
            }

            // Cache for visibility checks
            _cachedTooltip = tooltip;
            _cachedTextElement = null; // Clear other cache

            // Get ALL text components from the tooltip and find the content
            var textComponents = tooltip.GetComponentsInChildren<TMP_Text>(true);
            Debug.Log($"[ATSAccessibility] TutorialHandler: Found {textComponents.Length} TMP_Text in TutorialTooltip");

            string contentText = null;
            string speakerText = null;

            foreach (var tc in textComponents)
            {
                if (tc == null || string.IsNullOrEmpty(tc.text)) continue;

                string objName = tc.gameObject.name.ToLower();
                string text = tc.text;

                Debug.Log($"[ATSAccessibility] TutorialHandler: TMP_Text '{tc.gameObject.name}' = '{text.Substring(0, Math.Min(50, text.Length))}'");

                // "Label" contains the content, "Name" contains the speaker
                if (objName == "label" || objName.Contains("content") || objName.Contains("desc") || objName.Contains("message") || objName.Contains("body"))
                {
                    contentText = text;
                }
                else if (objName == "name" || objName.Contains("speaker") || objName.Contains("title") || objName.Contains("header"))
                {
                    speakerText = text;
                }
                else if (contentText == null && text.Length > 20)
                {
                    contentText = text;
                }
                else if (speakerText == null)
                {
                    speakerText = text;
                }
            }

            // Build announcement
            var announcement = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(speakerText))
                announcement.Add(speakerText);
            if (!string.IsNullOrEmpty(contentText))
                announcement.Add(contentText);

            if (announcement.Count == 0)
            {
                return false;
            }

            string fullAnnouncement = string.Join(". ", announcement);
            _lastAnnouncedText = fullAnnouncement;
            Debug.Log($"[ATSAccessibility] Tutorial (Tooltip): {fullAnnouncement}");
            Speech.Say(fullAnnouncement);
            return true;
        }

        /// <summary>
        /// Try to announce from TutorialTexts (positioned text style).
        /// Maps phase names to specific text elements.
        /// </summary>
        private bool TryAnnounceTutorialTexts()
        {
            // Use cached TutorialTexts or find it
            if (_cachedTutorialTexts == null || !_cachedTutorialTexts.activeInHierarchy)
            {
                _cachedTutorialTexts = null;
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name == "TutorialTexts" && obj.activeInHierarchy)
                    {
                        _cachedTutorialTexts = obj;
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

            // Find the specific text element
            var textComponents = _cachedTutorialTexts.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text foundElement = null;
            string contentText = null;
            foreach (var tc in textComponents)
            {
                if (tc.gameObject.name == targetTextName && !string.IsNullOrEmpty(tc.text))
                {
                    foundElement = tc;
                    contentText = tc.text;
                    break;
                }
            }

            if (contentText == null)
            {
                Debug.Log($"[ATSAccessibility] TutorialHandler: Text element '{targetTextName}' not found or empty");
                _cachedTextElement = null;
                return false;
            }

            // Cache for visibility checks
            _cachedTextElement = foundElement;
            _cachedTooltip = null; // Clear other cache

            // For TutorialTexts phases, the speaker is always "The Queen's Envoy"
            // The game doesn't expose this in an accessible way for these phases
            string speakerName = "The Queen's Envoy";

            string announcement = $"{speakerName}. {contentText}";
            _lastAnnouncedText = announcement;
            Debug.Log($"[ATSAccessibility] Tutorial (Texts/{targetTextName}): {announcement}");
            Speech.Say(announcement);
            return true;
        }

        /// <summary>
        /// Map tutorial phase to the corresponding text element name in TutorialTexts.
        /// </summary>
        private string GetTextNameForPhase(string phase)
        {
            switch (phase)
            {
                case "CameraControl":
                    return "Mid";  // "Use [W, S, A, D] or mouse to move camera"
                case "Impatience":
                    return "LowerRight";  // "Neglecting your village will increase the Queen's impatience..."
                case "Reputation":
                    return "LowerLeft";  // "Fulfilling your duties will increase the town's reputation..."
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


        // ========================================
        // OBSERVABLE SUBSCRIPTION (via reflection)
        // ========================================

        /// <summary>
        /// Subscribe to a UniRx IObservable using reflection.
        /// </summary>
        private IDisposable SubscribeToObservable(object observable, Action<object> callback)
        {
            if (observable == null) return null;

            try
            {
                var observableType = observable.GetType();

                // Find Subscribe method that takes IObserver<T>
                var methods = observableType.GetMethods();

                foreach (var method in methods)
                {
                    if (method.Name != "Subscribe") continue;
                    var parameters = method.GetParameters();
                    if (parameters.Length != 1) continue;

                    var paramType = parameters[0].ParameterType;
                    if (!paramType.IsGenericType) continue;

                    // Check for IObserver<T>
                    if (paramType.GetGenericTypeDefinition() == typeof(IObserver<>))
                    {
                        var elementType = paramType.GetGenericArguments()[0];
                        Debug.Log($"[ATSAccessibility] TutorialHandler: Found Subscribe(IObserver<{elementType.Name}>)");

                        // Create our observer wrapper
                        var observerType = typeof(ActionObserver<>).MakeGenericType(elementType);
                        var observer = Activator.CreateInstance(observerType, new object[] { callback });

                        // Invoke Subscribe
                        var result = method.Invoke(observable, new object[] { observer });
                        return result as IDisposable;
                    }
                }

                Debug.LogWarning("[ATSAccessibility] TutorialHandler: No matching Subscribe method found");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TutorialHandler subscription failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// IObserver wrapper that calls an Action for each OnNext.
        /// </summary>
        private class ActionObserver<T> : IObserver<T>
        {
            private readonly Action<object> _callback;

            public ActionObserver(Action<object> callback)
            {
                _callback = callback;
            }

            public void OnNext(T value)
            {
                try
                {
                    _callback?.Invoke(value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] TutorialHandler observer error: {ex.Message}");
                }
            }

            public void OnError(Exception error)
            {
                Debug.LogError($"[ATSAccessibility] TutorialHandler observable error: {error.Message}");
            }

            public void OnCompleted()
            {
                Debug.Log("[ATSAccessibility] TutorialHandler observable completed");
            }
        }
    }
}
