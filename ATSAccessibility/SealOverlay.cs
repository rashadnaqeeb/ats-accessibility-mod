using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Seal building panel in Sealed Forest biome.
    /// Provides keyboard navigation through seal information, plague effects,
    /// stage progress, and delivery objectives.
    /// </summary>
    public class SealOverlay : IKeyHandler
    {
        // ========================================
        // NAVIGATION STATE
        // ========================================

        private enum Section { Effects, Progress, Dialogue, Offerings, Reward }
        private static readonly Section[] _allSections = (Section[])Enum.GetValues(typeof(Section));

        private bool _isOpen;
        private object _seal;

        private Section _currentSection;
        private bool _inOfferingsDetail;  // true when navigating within Offerings
        private int _currentOfferingIndex;

        // Cached data
        private object _currentStage;       // SealKitState
        private object _currentStageModel;  // SealKitModel
        private Array _offerings;           // SealPartModel[]
        private Array _offeringOrders;      // OrderState[]

        // Type-ahead search (for offerings)
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Cached effect property info
        private static PropertyInfo _effectDisplayNameProperty = null;
        private static PropertyInfo _effectDescriptionProperty = null;
        private static bool _effectPropsCached = false;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Home:
                    if (_inOfferingsDetail)
                    {
                        if (_offerings != null && _offerings.Length > 0)
                        {
                            _currentOfferingIndex = 0;
                            AnnounceOffering(_currentOfferingIndex);
                        }
                    }
                    else
                    {
                        _currentSection = _allSections[0];
                        AnnounceSection();
                    }
                    return true;

                case KeyCode.End:
                    if (_inOfferingsDetail)
                    {
                        if (_offerings != null && _offerings.Length > 0)
                        {
                            _currentOfferingIndex = _offerings.Length - 1;
                            AnnounceOffering(_currentOfferingIndex);
                        }
                    }
                    else
                    {
                        _currentSection = _allSections[_allSections.Length - 1];
                        AnnounceSection();
                    }
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_currentSection == Section.Offerings && !_inOfferingsDetail)
                    {
                        EnterOfferingsDetail();
                        return true;
                    }
                    if (_inOfferingsDetail)
                    {
                        TryDeliver();
                        return true;
                    }
                    // Re-announce for other sections
                    AnnounceSection();
                    return true;

                case KeyCode.LeftArrow:
                    if (_inOfferingsDetail)
                    {
                        ExitOfferingsDetail();
                        return true;
                    }
                    return true;

                case KeyCode.Space:
                    if (_inOfferingsDetail)
                    {
                        TryDeliver();
                        return true;
                    }
                    return true;

                case KeyCode.T:
                    if (_inOfferingsDetail)
                    {
                        ToggleTracking();
                        return true;
                    }
                    return true;

                case KeyCode.Backspace:
                    if (_search.RemoveChar())
                    {
                        if (_search.HasBuffer)
                            Speech.Say($"Search: {_search.Buffer}");
                        else
                            Speech.Say("Search cleared");
                    }
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    if (_inOfferingsDetail)
                    {
                        ExitOfferingsDetail();
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                default:
                    // Type-ahead search in offerings detail
                    if (_inOfferingsDetail && keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        HandleSearchKey(keyCode);
                        return true;
                    }
                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when SealPanel is shown.
        /// </summary>
        public void Open()
        {
            _seal = SealReflection.GetFirstSeal();
            if (_seal == null)
            {
                Debug.LogWarning("[ATSAccessibility] SealOverlay: No seal found");
                return;
            }

            if (SealReflection.IsSealCompleted(_seal))
            {
                Speech.Say("Seal completed");
                return;
            }

            _isOpen = true;
            _currentSection = Section.Effects;
            _inOfferingsDetail = false;
            _currentOfferingIndex = 0;
            _search.Clear();

            RefreshData();
            AnnounceSection();
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            ClearData();
        }

        private void RefreshData()
        {
            _currentStage = SealReflection.GetFirstUncompletedStage(_seal);
            _currentStageModel = SealReflection.GetStageModel(_seal, _currentStage);
            _offerings = SealReflection.GetStageOfferings(_currentStageModel);
            _offeringOrders = SealReflection.GetStageOrders(_currentStage);
        }

        private void ClearData()
        {
            _seal = null;
            _currentStage = null;
            _currentStageModel = null;
            _offerings = null;
            _offeringOrders = null;
            _search.Clear();
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_inOfferingsDetail)
            {
                NavigateOfferings(direction);
            }
            else
            {
                NavigateSections(direction);
            }
        }

        private void NavigateSections(int direction)
        {
            int currentIndex = (int)_currentSection;
            int newIndex = NavigationUtils.WrapIndex(currentIndex, direction, _allSections.Length);
            _currentSection = _allSections[newIndex];
            AnnounceSection();
        }

        private void NavigateOfferings(int direction)
        {
            if (_offerings == null || _offerings.Length == 0) return;

            _currentOfferingIndex = NavigationUtils.WrapIndex(_currentOfferingIndex, direction, _offerings.Length);
            AnnounceOffering(_currentOfferingIndex);
        }

        private void EnterOfferingsDetail()
        {
            if (_offerings == null || _offerings.Length == 0)
            {
                Speech.Say("No offerings available");
                return;
            }

            _inOfferingsDetail = true;
            _currentOfferingIndex = 0;
            AnnounceOffering(_currentOfferingIndex);
        }

        private void ExitOfferingsDetail()
        {
            _inOfferingsDetail = false;
            _search.Clear();
            AnnounceSection();
        }

        // ========================================
        // SECTION ANNOUNCEMENTS
        // ========================================

        private void AnnounceSection()
        {
            switch (_currentSection)
            {
                case Section.Effects:
                    AnnounceEffects();
                    break;
                case Section.Progress:
                    AnnounceProgress();
                    break;
                case Section.Dialogue:
                    AnnounceDialogue();
                    break;
                case Section.Offerings:
                    if (_inOfferingsDetail)
                        AnnounceOffering(_currentOfferingIndex);
                    else
                        Speech.Say($"Offerings, {_offerings?.Length ?? 0} alternatives");
                    break;
                case Section.Reward:
                    AnnounceReward();
                    break;
            }
        }

        private void AnnounceEffects()
        {
            var state = SealReflection.GetSealGameState();
            if (state == null)
            {
                Speech.Say("Unable to read plague info");
                return;
            }

            if (SealReflection.IsEffectActive(state))
            {
                string effectName = SealReflection.GetCurrentEffect(state);
                var effectModel = GameReflection.GetEffectModel(effectName);
                string displayName = GetEffectDisplayName(effectModel) ?? effectName;
                string description = GetEffectDescription(effectModel);

                if (!string.IsNullOrEmpty(description))
                    Speech.Say($"Current plague: {displayName}. {description}");
                else
                    Speech.Say($"Current plague: {displayName}");
            }
            else
            {
                string effectName = SealReflection.GetNextEffect(state);
                var effectModel = GameReflection.GetEffectModel(effectName);
                string displayName = GetEffectDisplayName(effectModel) ?? effectName;
                string description = GetEffectDescription(effectModel);

                float seconds = SealReflection.GetSecondsUntilStorm();
                string timeText = FormatTime(seconds);

                if (!string.IsNullOrEmpty(description))
                    Speech.Say($"Next plague: {displayName}. {description}. Activates in {timeText}");
                else
                    Speech.Say($"Next plague: {displayName}. Activates in {timeText}");
            }
        }

        private void AnnounceProgress()
        {
            var (current, total, completedNames) = SealReflection.GetProgress(_seal);

            // Handle completion case
            if (current > total)
            {
                string completedStr = string.Join(", ", completedNames);
                Speech.Say($"All {total} stages completed: {completedStr}");
                return;
            }

            string completedText = completedNames.Count > 0
                ? $"Completed: {string.Join(", ", completedNames)}"
                : "No stages completed";
            Speech.Say($"Stage {current} of {total}. {completedText}");
        }

        private void AnnounceDialogue()
        {
            string dialogue = SealReflection.GetStageDialogue(_currentStageModel);
            if (!string.IsNullOrEmpty(dialogue))
                Speech.Say(dialogue);
            else
                Speech.Say("No dialogue");
        }

        private void AnnounceOffering(int index)
        {
            if (_offerings == null || index < 0 || index >= _offerings.Length)
            {
                Speech.Say("Offering not available");
                return;
            }

            var offering = _offerings.GetValue(index);
            var order = (_offeringOrders != null && index < _offeringOrders.Length)
                ? _offeringOrders.GetValue(index)
                : null;

            string name = SealReflection.GetOfferingDisplayName(offering) ?? "Unknown offering";
            string objectives = GetObjectivesText(offering, order);
            bool canDeliver = CanDeliverOffering(order, offering);
            string status = canDeliver ? "Deliverable" : "In progress";

            // Check tracking
            bool tracked = SealReflection.IsOfferingTracked(order);
            string trackingStr = tracked ? ", Tracked" : "";

            if (!string.IsNullOrEmpty(objectives))
                Speech.Say($"{name}. {objectives}. {status}{trackingStr}");
            else
                Speech.Say($"{name}. {status}{trackingStr}");
        }

        private void AnnounceReward()
        {
            var reward = SealReflection.GetStageReward(_currentStageModel);
            if (reward == null)
            {
                Speech.Say("No reward");
                return;
            }

            string name = GetEffectDisplayName(reward);
            string description = GetEffectDescription(reward);

            if (!string.IsNullOrEmpty(description))
                Speech.Say($"Reward: {name}. {description}");
            else if (!string.IsNullOrEmpty(name))
                Speech.Say($"Reward: {name}");
            else
                Speech.Say("Reward available");
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void TryDeliver()
        {
            if (_offerings == null || _currentOfferingIndex < 0 || _currentOfferingIndex >= _offerings.Length)
            {
                Speech.Say("Cannot deliver");
                SoundManager.PlayFailed();
                return;
            }

            var offering = _offerings.GetValue(_currentOfferingIndex);
            var order = (_offeringOrders != null && _currentOfferingIndex < _offeringOrders.Length)
                ? _offeringOrders.GetValue(_currentOfferingIndex)
                : null;

            if (!CanDeliverOffering(order, offering))
            {
                Speech.Say("Not ready to deliver");
                SoundManager.PlayFailed();
                return;
            }

            // Complete the offering
            if (SealReflection.CompleteOffering(_currentStage, _currentStageModel, _currentOfferingIndex))
            {
                string name = SealReflection.GetOfferingDisplayName(offering) ?? "Offering";
                SoundManager.PlaySealOrderDeliver();

                // Refresh data for next stage
                RefreshData();

                // Check if seal is now complete and close overlay
                if (SealReflection.IsSealCompleted(_seal))
                {
                    Speech.Say($"{name} delivered. Seal completed");
                    Close();
                    return;
                }

                Speech.Say($"{name} delivered");

                // Reset to section view
                _inOfferingsDetail = false;
                _currentOfferingIndex = 0;
            }
            else
            {
                Speech.Say("Delivery failed");
                SoundManager.PlayFailed();
            }
        }

        private void ToggleTracking()
        {
            if (_offeringOrders == null || _currentOfferingIndex < 0 || _currentOfferingIndex >= _offeringOrders.Length)
            {
                Speech.Say("Cannot toggle tracking");
                return;
            }

            var order = _offeringOrders.GetValue(_currentOfferingIndex);
            if (order == null)
            {
                Speech.Say("Cannot toggle tracking");
                return;
            }

            bool wasTracked = SealReflection.IsOfferingTracked(order);
            if (SealReflection.ToggleOfferingTracking(order))
            {
                Speech.Say(wasTracked ? "Untracked" : "Tracked");
            }
            else
            {
                Speech.Say("Failed to toggle tracking");
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        private void HandleSearchKey(KeyCode keyCode)
        {
            if (_offerings == null || _offerings.Length == 0) return;

            char c = (char)('a' + (keyCode - KeyCode.A));
            _search.AddChar(c);

            int match = FindMatchingOffering();
            if (match >= 0)
            {
                _currentOfferingIndex = match;
                AnnounceOffering(_currentOfferingIndex);
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatchingOffering()
        {
            if (!_search.HasBuffer || _offerings == null) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _offerings.Length; i++)
            {
                var offering = _offerings.GetValue(i);
                string name = SealReflection.GetOfferingDisplayName(offering);
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerBuffer))
                    return i;
            }

            return -1;
        }

        // ========================================
        // HELPERS
        // ========================================

        private bool CanDeliverOffering(object orderState, object offering)
        {
            if (orderState == null || offering == null) return false;

            // Get the OrderModel from the offering
            var orderModel = SealReflection.GetOfferingOrder(offering);
            if (orderModel == null) return false;

            return OrdersReflection.CanComplete(orderState, orderModel);
        }

        private string GetObjectivesText(object offering, object orderState)
        {
            if (offering == null || orderState == null) return null;

            var orderModel = SealReflection.GetOfferingOrder(offering);
            if (orderModel == null) return null;

            var objectives = OrdersReflection.GetObjectiveTexts(orderModel, orderState);
            if (objectives == null || objectives.Count == 0) return null;

            return string.Join(", ", objectives);
        }

        private static void EnsureEffectPropertyCached()
        {
            if (_effectPropsCached) return;
            _effectPropsCached = true;

            try
            {
                var effectModelType = GameReflection.GameAssembly?.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    _effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                    _effectDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] SealOverlay: Failed to cache effect properties: {ex.Message}");
            }
        }

        private static string GetEffectDisplayName(object effectModel)
        {
            if (effectModel == null) return null;
            EnsureEffectPropertyCached();
            if (_effectDisplayNameProperty == null) return null;

            try { return _effectDisplayNameProperty.GetValue(effectModel)?.ToString(); }
            catch { return null; }
        }

        private static string GetEffectDescription(object effectModel)
        {
            if (effectModel == null) return null;
            EnsureEffectPropertyCached();
            if (_effectDescriptionProperty == null) return null;

            try
            {
                string desc = _effectDescriptionProperty.GetValue(effectModel)?.ToString();
                // Strip rich text tags
                if (!string.IsNullOrEmpty(desc))
                    desc = OrdersReflection.StripRichText(desc).Trim();
                return desc;
            }
            catch { return null; }
        }

        private static string FormatTime(float seconds)
        {
            if (seconds <= 0) return "0:00";

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return ts.ToString(@"h\:mm\:ss");
            return ts.ToString(@"m\:ss");
        }
    }
}
