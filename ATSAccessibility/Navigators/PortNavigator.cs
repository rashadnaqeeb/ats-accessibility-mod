using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Port buildings (expeditions).
    /// Provides navigation through Info, Status, and Rewards sections.
    /// </summary>
    public class PortNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Status,
            Rewards,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;

        // Status data
        private int _expeditionLevel;
        private bool _expeditionStarted;
        private bool _rewardsWaiting;
        private float _progress;  // 0-1
        private float _timeLeft;

        // Reward data
        private string _blueprintReward;
        private string _perkReward;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "PortNavigator";

        protected override string[] GetSections()
        {
            return _sectionNames;
        }

        protected override int GetItemCount(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    return 2;  // Name, Expedition level
                case SectionType.Status:
                    return GetStatusItemCount();
                case SectionType.Rewards:
                    return GetRewardsItemCount();
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemCount();
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemCount(itemIndex);

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    AnnounceInfoItem(itemIndex);
                    break;
                case SectionType.Status:
                    AnnounceStatusItem(itemIndex);
                    break;
                case SectionType.Rewards:
                    AnnounceRewardsItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Port";

            // Status data
            _expeditionLevel = BuildingReflection.GetPortExpeditionLevel(_building);
            _expeditionStarted = BuildingReflection.IsPortExpeditionStarted(_building);
            _rewardsWaiting = BuildingReflection.ArePortRewardsWaiting(_building);
            _progress = BuildingReflection.GetPortProgress(_building);
            _timeLeft = BuildingReflection.GetPortTimeLeft(_building);

            // Reward data
            _blueprintReward = BuildingReflection.GetPortBlueprintReward(_building);
            _perkReward = BuildingReflection.GetPortPerkReward(_building);

            // Build sections list
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            sectionNames.Add("Status");
            sectionTypes.Add(SectionType.Status);

            // Only show Rewards if rewards are waiting
            if (_rewardsWaiting && (!string.IsNullOrEmpty(_blueprintReward) || !string.IsNullOrEmpty(_perkReward)))
            {
                sectionNames.Add("Rewards");
                sectionTypes.Add(SectionType.Rewards);
            }

            // Add Upgrades section if available
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();

            Debug.Log($"[ATSAccessibility] PortNavigator: Refreshed data for {_buildingName}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _blueprintReward = null;
            _perkReward = null;
            ClearUpgradesSection();
        }

        // ========================================
        // SEARCH NAME METHODS
        // ========================================

        protected override string GetSectionName(int sectionIndex)
        {
            if (_sectionNames != null && sectionIndex >= 0 && sectionIndex < _sectionNames.Length)
                return _sectionNames[sectionIndex];
            return null;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    return itemIndex == 0 ? "Name" : "Expedition level";
                case SectionType.Status:
                    return "Status";
                case SectionType.Rewards:
                    return "Reward";
                case SectionType.Upgrades:
                    return _upgradesSection.GetItemName(itemIndex);
                default:
                    return null;
            }
        }

        protected override string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
                return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);

            return null;
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private void AnnounceInfoItem(int itemIndex)
        {
            switch (itemIndex)
            {
                case 0:
                    Speech.Say($"Name: {_buildingName}");
                    break;
                case 1:
                    Speech.Say($"Expedition level: {_expeditionLevel}");
                    break;
            }
        }

        // ========================================
        // STATUS SECTION
        // ========================================

        private int GetStatusItemCount()
        {
            if (_rewardsWaiting)
                return 1;  // "Rewards waiting"
            if (_expeditionStarted)
                return 3;  // State, Progress, Time left
            return 1;  // "Not started"
        }

        private void AnnounceStatusItem(int itemIndex)
        {
            if (_rewardsWaiting)
            {
                Speech.Say("Expedition: Complete. Rewards waiting to be collected");
                return;
            }

            if (!_expeditionStarted)
            {
                Speech.Say("Expedition: Not started. Open port panel to begin");
                return;
            }

            // Expedition in progress
            switch (itemIndex)
            {
                case 0:
                    Speech.Say("Expedition: In progress");
                    break;
                case 1:
                    int percentage = Mathf.RoundToInt(_progress * 100f);
                    Speech.Say($"Progress: {percentage} percent");
                    break;
                case 2:
                    AnnounceTimeLeft();
                    break;
            }
        }

        private void AnnounceTimeLeft()
        {
            int seconds = Mathf.RoundToInt(_timeLeft);
            if (seconds <= 0)
            {
                Speech.Say("Time remaining: Almost done");
            }
            else if (seconds < 60)
            {
                Speech.Say($"Time remaining: {seconds} seconds");
            }
            else
            {
                int minutes = seconds / 60;
                int remainingSecs = seconds % 60;
                if (remainingSecs > 0)
                    Speech.Say($"Time remaining: {minutes} minutes {remainingSecs} seconds");
                else
                    Speech.Say($"Time remaining: {minutes} minutes");
            }
        }

        // ========================================
        // REWARDS SECTION
        // ========================================

        private int GetRewardsItemCount()
        {
            int count = 0;
            if (!string.IsNullOrEmpty(_blueprintReward)) count++;
            if (!string.IsNullOrEmpty(_perkReward)) count++;
            return count > 0 ? count : 1;  // At least "No rewards" message
        }

        private void AnnounceRewardsItem(int itemIndex)
        {
            var rewards = new List<string>();

            if (!string.IsNullOrEmpty(_blueprintReward))
            {
                // Try to get display name for blueprint
                string displayName = GetBuildingDisplayName(_blueprintReward);
                rewards.Add($"Blueprint: {displayName}");
            }

            if (!string.IsNullOrEmpty(_perkReward))
            {
                // Try to get display name for perk
                string displayName = GetEffectDisplayName(_perkReward);
                rewards.Add($"Perk: {displayName}");
            }

            if (rewards.Count == 0)
            {
                Speech.Say("No rewards");
                return;
            }

            if (itemIndex < rewards.Count)
            {
                Speech.Say(rewards[itemIndex]);
            }
            else
            {
                Speech.Say("Unknown reward");
            }
        }

        private string GetBuildingDisplayName(string buildingName)
        {
            if (string.IsNullOrEmpty(buildingName)) return buildingName;

            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return CleanupName(buildingName);

                var getBuildingMethod = settings.GetType().GetMethod("GetBuilding", new[] { typeof(string) });
                var model = getBuildingMethod?.Invoke(settings, new object[] { buildingName });
                if (model == null) return CleanupName(buildingName);

                var displayNameField = model.GetType().GetField("displayName", GameReflection.PublicInstance);
                var displayNameObj = displayNameField?.GetValue(model);
                return GameReflection.GetLocaText(displayNameObj) ?? CleanupName(buildingName);
            }
            catch
            {
                return CleanupName(buildingName);
            }
        }

        private string GetEffectDisplayName(string effectName)
        {
            if (string.IsNullOrEmpty(effectName)) return effectName;

            // Effects are complex to get display names for, just clean up the name
            return CleanupName(effectName);
        }

        private string CleanupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Remove common prefixes/suffixes
            name = name.Replace("[", "").Replace("]", "");

            // Split by uppercase letters and underscores
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_')
                {
                    result.Append(' ');
                }
                else if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                {
                    result.Append(' ');
                    result.Append(c);
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString().Trim();
        }
    }
}
