using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reads and announces the MetaRewardsPopup (end-of-settlement rewards screen).
    /// Handles the animated reveal of rewards by polling until the count stabilizes.
    /// </summary>
    public static class MetaRewardsPopupReader
    {
        // Scene index for world map (matches AccessibilityCore.SCENE_WORLDMAP)
        private const int SCENE_WORLDMAP = 3;

        // State tracking
        private static bool _isPolling = false;
        private static bool _isReady = false;
        private static string _cachedAnnouncement = null;

        /// <summary>
        /// Whether the MetaRewardsPopup is currently polling for rewards.
        /// </summary>
        public static bool IsPolling => _isPolling;

        /// <summary>
        /// Whether the MetaRewardsPopup has finished loading and is ready.
        /// </summary>
        public static bool IsReady => _isReady;

        /// <summary>
        /// Check if popup name indicates MetaRewardsPopup or MetaLevelUpPopup.
        /// </summary>
        private static bool IsMetaRewardsOrLevelUpPopup(string popupName)
        {
            return popupName.Contains("MetaRewards") || popupName.Contains("MetaLevelUp");
        }

        /// <summary>
        /// Reset state when popup closes.
        /// </summary>
        public static void Reset()
        {
            _isPolling = false;
            _isReady = false;
            _cachedAnnouncement = null;
        }

        /// <summary>
        /// Handle key events for the MetaRewardsPopup.
        /// Returns true if the key was handled.
        /// </summary>
        public static bool ProcessKeyEvent(KeyCode keyCode, GameObject popup)
        {
            // Handle Enter to close popup (when ready)
            if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
            {
                if (_isPolling)
                {
                    Speech.Say("Please wait for rewards");
                    return true;
                }

                if (_isReady)
                {
                    ClosePopup(popup);
                    return true;
                }

                return false;
            }

            // Handle arrow keys to re-read announcement
            if (keyCode != KeyCode.UpArrow && keyCode != KeyCode.DownArrow &&
                keyCode != KeyCode.LeftArrow && keyCode != KeyCode.RightArrow)
            {
                return false;
            }

            if (_isPolling)
            {
                Speech.Say("Please wait for rewards");
                return true;
            }

            if (_isReady && !string.IsNullOrEmpty(_cachedAnnouncement))
            {
                Speech.Say(_cachedAnnouncement);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Close the MetaRewardsPopup by clicking the close button.
        /// Using button click instead of Hide() directly because it properly
        /// triggers the game's expected flow for tutorial progression.
        /// </summary>
        private static void ClosePopup(GameObject popup)
        {
            if (popup == null) return;

            try
            {
                // Get the popup component (MetaRewardsPopup or MetaLevelUpPopup)
                var popupType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaRewardsPopup");
                var popupComponent = popupType != null ? popup.GetComponent(popupType) : null;

                if (popupComponent == null)
                {
                    popupType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaLevelUpPopup");
                    popupComponent = popupType != null ? popup.GetComponent(popupType) : null;
                }

                if (popupComponent == null) return;

                // Click the closeButton (required for proper tutorial flow)
                var closeButtonField = popupType.GetField("closeButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (closeButtonField != null)
                {
                    var closeButton = closeButtonField.GetValue(popupComponent) as UnityEngine.UI.Button;
                    if (closeButton != null && closeButton.gameObject.activeInHierarchy)
                    {
                        closeButton.onClick.Invoke();
                        return;
                    }
                }

                // Fallback: call Hide() directly
                var hideMethod = popupType.GetMethod("Hide", BindingFlags.Public | BindingFlags.Instance);
                hideMethod?.Invoke(popupComponent, null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ClosePopup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces the MetaRewardsPopup content including level info, experience, and rewards.
        /// Polls for rewards until the animation completes and count stabilizes.
        /// All information is combined into a single announcement to avoid interruption.
        /// </summary>
        /// <param name="popup">The MetaRewardsPopup GameObject</param>
        /// <param name="runner">MonoBehaviour for coroutine execution</param>
        public static IEnumerator AnnounceMetaRewardsPopup(GameObject popup, MonoBehaviour runner)
        {
            if (popup == null) yield break;

            // Set polling state
            _isPolling = true;
            _isReady = false;
            _cachedAnnouncement = null;

            // Get all TMP_Text components for level/exp info (available immediately)
            var allText = popup.GetComponentsInChildren<TMP_Text>(true);

            string levelText = null;
            string gainedExp = null;
            string expProgress = null;

            foreach (var text in allText)
            {
                string name = text.gameObject.name;
                string value = text.text;

                if (string.IsNullOrEmpty(value)) continue;

                if (name == "LevelHeader")
                    levelText = value;
                else if (name == "GainedExp")
                    gainedExp = value;
                else if (name == "Exp" && text.gameObject.activeInHierarchy)
                    expProgress = $"Experience {value}";
            }

            // Collect level/exp info but don't announce yet - wait for rewards
            var levelExpParts = new List<string>();
            if (!string.IsNullOrEmpty(levelText))
                levelExpParts.Add(levelText);
            if (!string.IsNullOrEmpty(gainedExp))
                levelExpParts.Add(gainedExp);
            if (!string.IsNullOrEmpty(expProgress))
                levelExpParts.Add(expProgress);

            // Poll until reward count stabilizes (no new rewards for 0.5s)
            List<string> rewardNames = new List<string>();
            int lastCount = 0;
            float elapsed = 0f;
            float maxWait = 6f;
            float pollInterval = 0.5f;

            while (elapsed < maxWait)
            {
                // Safety check: ensure popup is still visible and is MetaRewardsPopup or MetaLevelUpPopup
                if (popup == null || !popup.activeInHierarchy ||
                    !IsMetaRewardsOrLevelUpPopup(popup.name))
                {
                    _isPolling = false;
                    yield break;
                }

                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                rewardNames = GetRewardsFromMetaRewardSlots(popup);
                int currentCount = rewardNames.Count;

                // If count hasn't changed and we have rewards, animation is done
                if (currentCount > 0 && currentCount == lastCount)
                    break;


                lastCount = currentCount;
            }

            // Build combined announcement: level/exp, then rewards
            var fullAnnouncement = new List<string>();

            // Add level/exp info
            if (levelExpParts.Count > 0)
            {
                fullAnnouncement.Add(string.Join(". ", levelExpParts));
            }

            // Add rewards
            if (rewardNames.Count > 0)
            {
                fullAnnouncement.Add($"Rewards: {string.Join(", ", rewardNames)}");
            }

            // Check if on world map - auto-close to preserve tutorial tooltip accessibility
            bool isWorldMap = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == SCENE_WORLDMAP;

            if (isWorldMap)
            {
                // Auto-close on world map - don't wait for user input
                // This preserves access to the tutorial tooltip before animation finishes
                _cachedAnnouncement = string.Join(". ", fullAnnouncement);
                _isPolling = false;
                _isReady = true;

                Speech.Say(_cachedAnnouncement);

                // Auto-close the popup
                yield return new WaitForSeconds(0.1f); // Brief delay to let speech start
                ClosePopup(popup);
            }
            else
            {
                // Normal behavior - wait for user to close
                fullAnnouncement.Add("Press enter or escape to close");

                _cachedAnnouncement = string.Join(". ", fullAnnouncement);
                _isPolling = false;
                _isReady = true;

                Speech.Say(_cachedAnnouncement);
            }
        }

        /// <summary>
        /// Get reward names from MetaRewardSlot components in the popup.
        /// Accesses the rewardsSlots field directly from MetaRewardsPopup to get all slots,
        /// including those that may be inactive during the animation sequence.
        /// </summary>
        private static List<string> GetRewardsFromMetaRewardSlots(GameObject popup)
        {
            var rewardNames = new List<string>();
            if (popup == null) return rewardNames;

            try
            {
                // Try to get MetaRewardsPopup or MetaLevelUpPopup component
                var popupType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaRewardsPopup");
                var popupComponent = popupType != null ? popup.GetComponent(popupType) : null;

                // If not MetaRewardsPopup, try MetaLevelUpPopup
                if (popupComponent == null)
                {
                    popupType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaLevelUpPopup");
                    popupComponent = popupType != null ? popup.GetComponent(popupType) : null;
                }

                if (popupType == null || popupComponent == null)
                    return rewardNames;

                // Get the rewardsSlots field (List<MetaRewardSlot>)
                var slotsField = popupType.GetField("rewardsSlots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (slotsField == null)
                    return rewardNames;

                var slotsList = slotsField.GetValue(popupComponent) as System.Collections.IList;
                if (slotsList == null || slotsList.Count == 0)
                    return rewardNames;

                // Get the model field from MetaRewardSlot type
                var slotType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaRewardSlot");
                var modelField = slotType?.GetField("model",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                foreach (var slot in slotsList)
                {
                    if (slot == null) continue;

                    // Check if slot is active
                    var slotComponent = slot as Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeInHierarchy)
                        continue;

                    // Get the model
                    var model = modelField?.GetValue(slot);
                    if (model == null) continue;

                    // Get DisplayName
                    var displayNameProp = model.GetType().GetProperty("DisplayName");
                    string displayName = displayNameProp?.GetValue(model) as string;

                    if (string.IsNullOrEmpty(displayName)) continue;

                    // Get amount text
                    var getAmountMethod = model.GetType().GetMethod("GetAmountText");
                    string amountText = getAmountMethod?.Invoke(model, null) as string;

                    string rewardText = displayName;
                    if (!string.IsNullOrEmpty(amountText) && amountText != "0" && amountText != "")
                    {
                        rewardText = $"{amountText} {displayName}";
                    }

                    rewardNames.Add(rewardText);
                }

                // Also check upgradesSlot (ProgressionSlot) for level-up unlocks
                var upgradesSlotField = popupType.GetField("upgradesSlot",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (upgradesSlotField != null)
                {
                    var upgradesSlot = upgradesSlotField.GetValue(popupComponent) as Component;
                    if (upgradesSlot != null && upgradesSlot.gameObject.activeInHierarchy)
                    {
                        // ProgressionSlot has amountText field
                        var progressionSlotType = GameReflection.GetTypeByName("Eremite.View.HUD.Result.ProgressionSlot");
                        var amountTextField = progressionSlotType?.GetField("amountText",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        if (amountTextField != null)
                        {
                            var amountText = amountTextField.GetValue(upgradesSlot) as TMP_Text;
                            if (amountText != null && !string.IsNullOrEmpty(amountText.text))
                            {
                                // This is typically something like "+1 Upgrade Points"
                                rewardNames.Add(amountText.text);
                            }
                            else
                            {
                                // If no text, just indicate there's an upgrade unlock
                                rewardNames.Add("Upgrade Unlocked");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRewardsFromMetaRewardSlots failed: {ex.Message}");
            }

            return rewardNames;
        }
    }
}
