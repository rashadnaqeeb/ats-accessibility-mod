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
        /// <summary>
        /// Announces the MetaRewardsPopup content including level info, experience, and rewards.
        /// Polls for rewards until the animation completes and count stabilizes.
        /// </summary>
        /// <param name="popup">The MetaRewardsPopup GameObject</param>
        /// <param name="runner">MonoBehaviour for coroutine execution</param>
        public static IEnumerator AnnounceMetaRewardsPopup(GameObject popup, MonoBehaviour runner)
        {
            if (popup == null) yield break;

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

            // Announce level/exp info immediately
            var initialAnnouncements = new List<string>();
            if (!string.IsNullOrEmpty(levelText))
                initialAnnouncements.Add(levelText);
            if (!string.IsNullOrEmpty(gainedExp))
                initialAnnouncements.Add(gainedExp);
            if (!string.IsNullOrEmpty(expProgress))
                initialAnnouncements.Add(expProgress);

            if (initialAnnouncements.Count > 0)
            {
                string initial = string.Join(". ", initialAnnouncements);
                Debug.Log($"[ATSAccessibility] MetaRewards initial: {initial}");
                Speech.Say(initial);
            }

            // Poll until reward count stabilizes (no new rewards for 0.5s)
            List<string> rewardNames = new List<string>();
            int lastCount = 0;
            float elapsed = 0f;
            float maxWait = 6f;
            float pollInterval = 0.5f;

            while (elapsed < maxWait)
            {
                // Safety check: ensure popup is still visible and is MetaRewardsPopup
                if (popup == null || !popup.activeInHierarchy ||
                    !popup.name.Contains("MetaRewards"))
                {
                    Debug.Log("[ATSAccessibility] MetaRewardsPopup closed during polling");
                    yield break;
                }

                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                rewardNames = GetRewardsFromMetaRewardSlots(popup);
                int currentCount = rewardNames.Count;

                Debug.Log($"[ATSAccessibility] Polling: {currentCount} rewards at {elapsed}s");

                // If count hasn't changed and we have rewards, animation is done
                if (currentCount > 0 && currentCount == lastCount)
                {
                    Debug.Log($"[ATSAccessibility] Reward count stabilized at {currentCount}");
                    break;
                }

                lastCount = currentCount;
            }

            // Announce rewards if found
            if (rewardNames.Count > 0)
            {
                string rewards = $"Rewards: {string.Join(", ", rewardNames)}";
                Debug.Log($"[ATSAccessibility] MetaRewards: {rewards}");
                Speech.Say(rewards);
            }
            else
            {
                Debug.Log("[ATSAccessibility] MetaRewardsPopup: No rewards found after waiting");
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
                // Get the MetaRewardsPopup component
                var popupType = GameReflection.GetTypeByName("Eremite.View.HUD.MetaRewardsPopup");
                if (popupType == null)
                {
                    Debug.Log("[ATSAccessibility] MetaRewardsPopup type not found");
                    return rewardNames;
                }

                var popupComponent = popup.GetComponent(popupType);
                if (popupComponent == null)
                {
                    Debug.Log("[ATSAccessibility] MetaRewardsPopup component not found");
                    return rewardNames;
                }

                // Get the rewardsSlots field (List<MetaRewardSlot>)
                var slotsField = popupType.GetField("rewardsSlots",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (slotsField == null)
                {
                    Debug.Log("[ATSAccessibility] rewardsSlots field not found");
                    return rewardNames;
                }

                var slotsList = slotsField.GetValue(popupComponent) as System.Collections.IList;
                if (slotsList == null || slotsList.Count == 0)
                {
                    Debug.Log("[ATSAccessibility] rewardsSlots list is empty");
                    return rewardNames;
                }

                Debug.Log($"[ATSAccessibility] Found {slotsList.Count} reward slots in list");

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
                    Debug.Log($"[ATSAccessibility] MetaReward: {rewardText}");
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
                                Debug.Log($"[ATSAccessibility] MetaReward (upgrade): {amountText.text}");
                            }
                            else
                            {
                                // If no text, just indicate there's an upgrade unlock
                                rewardNames.Add("Upgrade Unlocked");
                                Debug.Log("[ATSAccessibility] MetaReward (upgrade): Upgrade Unlocked");
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
