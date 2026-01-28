using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Relic buildings (glade events).
    /// Provides phase-based navigation with name/description header at top:
    /// - Phase A (not started): [Name/Desc], Decisions, Choose Requirements, Effects, Preview Rewards, Start Investigation
    /// - Phase B (in progress): [Name/Desc], Status (info only), Workers, Requirements, Effects, Rewards, Cancel Investigation
    /// - Phase C (finished): [Name/Desc], Status (info only), Workers, Storage
    /// </summary>
    public class RelicNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Decisions,
            Requirements,
            Effects,
            Rewards,
            Status,
            Workers,
            Storage,
            Upgrades,
            Cancel
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private string _buildingDescription;
        private string _threatLevel;

        // Phase flags
        private bool _investigationStarted;
        private bool _investigationFinished;
        private float _progress;  // 0-1
        private float _timeLeft;

        // Decision data
        private int _decisionCount;
        private bool _hasMultipleDecisions;
        private int _selectedDecisionIndex;

        // Requirements for current decision
        private struct GoodsSetData
        {
            public int alternativeCount;
            public string[] goodNames;
            public string[] goodDisplayNames;
            public int[] goodAmounts;
            public int pickedIndex;
        }
        private GoodsSetData[] _goodsSets;
        private int _goodsSetCount;

        // Effects for current decision
        private BuildingReflection.RelicEffectInfo[] _workingEffects;
        private BuildingReflection.RelicEffectInfo[] _activeEffects;
        private BuildingReflection.RelicEffectInfo[] _dynamicEffects;  // Current tier effects
        private BuildingReflection.RelicEffectInfo[] _nextTierEffects;  // Next tier effects (preview)
        private bool _areEffectsPermanent;

        // Dynamic effects (escalating over time)
        private bool _hasDynamicEffects;
        private int _currentEffectTier;
        private int _totalEffectTiers;
        private float _timeToNextTier;
        private bool _isLastTierReached;

        // Rewards for current decision
        private BuildingReflection.RelicRewardInfo[] _rewards;
        private bool _hasRewards;

        // Status/action data
        private bool _canStart;
        private string _startBlockingReason;
        private bool _canCancel;
        private bool _hasAnyWorkplace;

        // Worker data
        private int[] _workerIds;
        private int _maxWorkers;
        private List<(string raceName, int freeCount)> _availableRaces = new List<(string, int)>();
        private bool _racesRefreshedForWorkerSection = false;

        // Storage data (Phase C rewards)
        private List<(string goodName, string displayName, int amount)> _storageItems = new List<(string, string, int)>();
        private int _storageTotalSum;

        // ========================================
        // BASE CLASS IMPLEMENTATION
        // ========================================

        protected override string NavigatorName => "RelicNavigator";

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
                case SectionType.Decisions:
                    return _decisionCount;
                case SectionType.Requirements:
                    return _goodsSetCount;
                case SectionType.Effects:
                    return GetEffectsItemCount();
                case SectionType.Rewards:
                    return _rewards?.Length ?? 0;
                case SectionType.Status:
                    return GetStatusItemCount();
                case SectionType.Workers:
                    return _maxWorkers;
                case SectionType.Storage:
                    return _storageItems.Count;
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

            if (_sectionTypes[sectionIndex] == SectionType.Requirements)
            {
                // Sub-items only if goods set has alternatives (more than 1 good)
                if (itemIndex >= 0 && itemIndex < _goodsSetCount && _goodsSets[itemIndex].alternativeCount > 1)
                    return _goodsSets[itemIndex].alternativeCount;
                return 0;
            }

            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return GetWorkerSubItemCount(itemIndex);
            }

            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.GetSubItemCount(itemIndex);
            }

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (_sectionTypes == null || sectionIndex < 0 || sectionIndex >= _sectionTypes.Length) return;

            var sectionType = _sectionTypes[sectionIndex];

            if (sectionType == SectionType.Info)
            {
                // Info section: announce building name, threat level, and description
                string header = _buildingName;
                header += $". Threat level: {_threatLevel}";
                if (!string.IsNullOrEmpty(_buildingDescription))
                    header += ". " + _buildingDescription;
                Speech.Say(header);
                return;
            }

            if (sectionType == SectionType.Status)
            {
                if (_investigationFinished)
                {
                    // Phase C: Resolved status
                    if (_storageTotalSum > 0)
                        Speech.Say($"Status: Resolved, {_storageTotalSum} goods awaiting pickup");
                    else
                        Speech.Say("Status: Resolved");
                }
                else if (_investigationStarted)
                {
                    // Phase B: In progress status with full details
                    int percentage = Mathf.RoundToInt(_progress * 100f);
                    string timeStr = FormatTimeLeft();
                    Speech.Say($"Status: In progress, {percentage}%, {timeStr}");
                }
                else
                {
                    // Phase A: Start investigation
                    if (_canStart)
                        Speech.Say("Start Investigation");
                    else
                        Speech.Say(_startBlockingReason ?? "Cannot start");
                }
                return;
            }

            if (sectionType == SectionType.Effects)
            {
                string effectsAnnouncement = _sectionNames[sectionIndex];
                if (_areEffectsPermanent)
                    effectsAnnouncement += ", permanent";
                Speech.Say(effectsAnnouncement);
                return;
            }

            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        private string FormatDynamicEffectTime(float seconds)
        {
            int secs = Mathf.RoundToInt(seconds);
            if (secs <= 0)
                return "moments";
            if (secs < 60)
                return $"{secs} seconds";
            int minutes = secs / 60;
            int remainingSecs = secs % 60;
            if (remainingSecs > 0)
                return $"{minutes} minutes {remainingSecs} seconds";
            return $"{minutes} minutes";
        }

        private string FormatTimeLeft()
        {
            int seconds = Mathf.RoundToInt(_timeLeft);
            if (seconds <= 0)
                return "almost done";
            if (seconds < 60)
                return $"{seconds} seconds remaining";
            int minutes = seconds / 60;
            int remainingSecs = seconds % 60;
            if (remainingSecs > 0)
                return $"{minutes} minutes {remainingSecs} seconds remaining";
            return $"{minutes} minutes remaining";
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Decisions:
                    AnnounceDecisionItem(itemIndex);
                    break;
                case SectionType.Requirements:
                    AnnounceRequirementItem(itemIndex);
                    break;
                case SectionType.Effects:
                    AnnounceEffectItem(itemIndex);
                    break;
                case SectionType.Rewards:
                    AnnounceRewardItem(itemIndex);
                    break;
                case SectionType.Workers:
                    AnnounceWorkerItem(itemIndex);
                    break;
                case SectionType.Storage:
                    AnnounceStorageItem(itemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Requirements)
            {
                AnnounceRequirementSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                AnnounceWorkerSubItem(itemIndex, subItemIndex);
            }
            else if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Decisions:
                    return PerformDecisionAction(itemIndex);
                case SectionType.Effects:
                    return PerformEffectAction(itemIndex);
                case SectionType.Rewards:
                    return PerformRewardAction(itemIndex);
                default:
                    return false;
            }
        }

        protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Workers)
                return "No free workers";
            return null;
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Requirements)
            {
                return PerformRequirementSubItemAction(itemIndex, subItemIndex);
            }
            if (_sectionTypes[sectionIndex] == SectionType.Workers && itemIndex < _maxWorkers)
            {
                return PerformWorkerSubItemAction(itemIndex, subItemIndex);
            }
            if (_sectionTypes[sectionIndex] == SectionType.Upgrades)
            {
                return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
            }
            return false;
        }

        protected override string GetItemName(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return null;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Decisions:
                    return BuildingReflection.GetRelicDecisionLabel(_building, itemIndex);
                case SectionType.Requirements:
                    if (itemIndex >= 0 && itemIndex < _goodsSetCount)
                    {
                        int picked = _goodsSets[itemIndex].pickedIndex;
                        if (picked >= 0 && picked < _goodsSets[itemIndex].goodDisplayNames.Length)
                            return _goodsSets[itemIndex].goodDisplayNames[picked];
                    }
                    return null;
                case SectionType.Storage:
                    if (itemIndex >= 0 && itemIndex < _storageItems.Count)
                        return _storageItems[itemIndex].displayName;
                    return null;
                default:
                    return null;
            }
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Relic";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _threatLevel = BuildingReflection.GetRelicDangerLevel(_building) ?? "None";

            // Phase flags
            _investigationStarted = BuildingReflection.IsRelicInvestigationStarted(_building);
            _investigationFinished = BuildingReflection.IsRelicInvestigationFinished(_building);
            _progress = BuildingReflection.GetRelicProgress(_building);
            _timeLeft = BuildingReflection.GetRelicTimeLeft(_building);

            // Decision metadata
            _hasMultipleDecisions = BuildingReflection.RelicHasMultipleDecisions(_building);
            _decisionCount = BuildingReflection.GetRelicDecisionCount(_building);
            _selectedDecisionIndex = BuildingReflection.GetRelicDecisionIndex(_building);

            // Model properties
            _hasAnyWorkplace = BuildingReflection.RelicHasAnyWorkplace(_building);
            _areEffectsPermanent = BuildingReflection.RelicAreEffectsPermanent(_building);

            // Dynamic effects (escalating over time)
            _hasDynamicEffects = BuildingReflection.RelicHasDynamicEffects(_building);
            _currentEffectTier = BuildingReflection.GetRelicCurrentEffectTier(_building);
            _totalEffectTiers = BuildingReflection.GetRelicEffectTierCount(_building);
            _timeToNextTier = BuildingReflection.GetRelicTimeToNextEffectTier(_building);
            _isLastTierReached = BuildingReflection.RelicIsLastEffectTierReached(_building);

            // Worker data
            _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
            _maxWorkers = _workerIds?.Length ?? 0;
            RefreshAvailableRaces();

            // Decision-dependent data
            RefreshDecisionDetails();

            // Storage data (Phase C)
            RefreshStorageData();

            // Status/actions
            RefreshStatusData();

            // Build sections
            BuildSections();
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _buildingName = null;
            _buildingDescription = null;
            _threatLevel = null;
            _workerIds = null;
            _availableRaces.Clear();
            _racesRefreshedForWorkerSection = false;
            _goodsSets = null;
            _goodsSetCount = 0;
            _workingEffects = null;
            _activeEffects = null;
            _dynamicEffects = null;
            _nextTierEffects = null;
            _hasDynamicEffects = false;
            _currentEffectTier = 0;
            _totalEffectTiers = 0;
            _timeToNextTier = 0f;
            _isLastTierReached = false;
            _rewards = null;
            _hasRewards = false;
            _storageItems.Clear();
            _storageTotalSum = 0;
            ClearUpgradesSection();
        }

        // ========================================
        // SECTION BUILDING
        // ========================================

        private void BuildSections()
        {
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            // Info section at top of all phases (name and description, no items)
            sectionNames.Add("Info");
            sectionTypes.Add(SectionType.Info);

            if (_investigationFinished)
            {
                // Phase C: Info, Status, Storage
                sectionNames.Add("Status");
                sectionTypes.Add(SectionType.Status);

                if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
                {
                    sectionNames.Add("Workers");
                    sectionTypes.Add(SectionType.Workers);
                }

                if (_storageItems.Count > 0)
                {
                    sectionNames.Add("Storage");
                    sectionTypes.Add(SectionType.Storage);
                }
            }
            else if (_investigationStarted)
            {
                // Phase B: Info, Status, Workers, Requirements, Effects, Rewards, Cancel
                sectionNames.Add("Status");
                sectionTypes.Add(SectionType.Status);

                if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
                {
                    sectionNames.Add("Workers");
                    sectionTypes.Add(SectionType.Workers);
                }

                if (_goodsSetCount > 0)
                {
                    sectionNames.Add("Requirements");
                    sectionTypes.Add(SectionType.Requirements);
                }

                if (GetEffectsItemCount() > 0)
                {
                    sectionNames.Add("Effects");
                    sectionTypes.Add(SectionType.Effects);
                }

                if (_hasRewards)
                {
                    sectionNames.Add("Rewards");
                    sectionTypes.Add(SectionType.Rewards);
                }

                if (_canCancel)
                {
                    sectionNames.Add("Cancel Investigation");
                    sectionTypes.Add(SectionType.Cancel);
                }
            }
            else
            {
                // Phase A: Info, Decisions, Choose Requirements, Effects, Workers, Rewards, Start Investigation
                if (_hasMultipleDecisions)
                {
                    sectionNames.Add("Decisions");
                    sectionTypes.Add(SectionType.Decisions);
                }

                if (_goodsSetCount > 0)
                {
                    sectionNames.Add("Choose Requirements");
                    sectionTypes.Add(SectionType.Requirements);
                }

                if (GetEffectsItemCount() > 0)
                {
                    sectionNames.Add("Effects");
                    sectionTypes.Add(SectionType.Effects);
                }

                // Workers section - required for relics with workplaces
                if (_maxWorkers > 0 && BuildingReflection.ShouldAllowWorkerManagement(_building))
                {
                    sectionNames.Add("Workers");
                    sectionTypes.Add(SectionType.Workers);
                }

                if (_hasRewards)
                {
                    sectionNames.Add("Preview Rewards");
                    sectionTypes.Add(SectionType.Rewards);
                }

                sectionNames.Add("Start Investigation");
                sectionTypes.Add(SectionType.Status);
            }

            // Add Upgrades section if available (all phases)
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();
        }

        // ========================================
        // DECISION-DEPENDENT DATA REFRESH
        // ========================================

        private void RefreshDecisionDetails()
        {
            int safeDecision = BuildingReflection.GetRelicSafeDecisionIndex(_building);

            // Requirements
            RefreshGoodsSets(safeDecision);

            // Effects
            // Working effects shown in Phase A (preview) and Phase B (active), not in Phase C
            _workingEffects = !_investigationFinished
                ? BuildingReflection.GetRelicWorkingEffects(_building)
                : null;

            // Active effects only used when NOT using dynamic effects (mutually exclusive)
            _activeEffects = !_hasDynamicEffects
                ? BuildingReflection.GetRelicActiveEffects(_building)
                : null;

            _dynamicEffects = BuildingReflection.GetRelicCurrentDynamicEffects(_building);
            _nextTierEffects = BuildingReflection.GetRelicNextDynamicEffects(_building);

            // Rewards
            RefreshRewards(safeDecision);
        }

        private void RefreshGoodsSets(int decisionIndex)
        {
            _goodsSetCount = BuildingReflection.GetRelicGoodsSetCount(_building, decisionIndex);
            if (_goodsSetCount == 0)
            {
                _goodsSets = null;
                return;
            }

            _goodsSets = new GoodsSetData[_goodsSetCount];
            for (int i = 0; i < _goodsSetCount; i++)
            {
                int altCount = BuildingReflection.GetRelicGoodsAlternativeCount(_building, decisionIndex, i);
                int pickedIndex = BuildingReflection.GetRelicPickedGoodIndex(_building, decisionIndex, i);

                var names = new string[altCount];
                var displayNames = new string[altCount];
                var amounts = new int[altCount];

                for (int j = 0; j < altCount; j++)
                {
                    names[j] = BuildingReflection.GetRelicGoodName(_building, decisionIndex, i, j);
                    displayNames[j] = BuildingReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, j) ?? "Unknown";
                    amounts[j] = BuildingReflection.GetRelicGoodAmount(_building, decisionIndex, i, j);
                }

                _goodsSets[i] = new GoodsSetData
                {
                    alternativeCount = altCount,
                    goodNames = names,
                    goodDisplayNames = displayNames,
                    goodAmounts = amounts,
                    pickedIndex = pickedIndex
                };
            }
        }

        private void RefreshRewards(int decisionIndex)
        {
            bool hasDynamic = BuildingReflection.RelicHasDynamicRewards(_building);
            bool hasDecisionRewards = BuildingReflection.RelicHasDecisionRewards(_building);

            if (hasDynamic || hasDecisionRewards)
            {
                _rewards = BuildingReflection.GetRelicDecisionRewards(_building, decisionIndex);
                _hasRewards = _rewards != null && _rewards.Length > 0;
            }
            else
            {
                _rewards = null;
                _hasRewards = false;
            }
        }

        private void RefreshStatusData()
        {
            if (!_investigationStarted && !_investigationFinished)
            {
                _canStart = BuildingReflection.RelicCanStart(_building, out _startBlockingReason);
            }
            _canCancel = BuildingReflection.RelicCanCancel(_building);
        }

        // ========================================
        // DECISIONS SECTION
        // ========================================

        private void AnnounceDecisionItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _decisionCount) return;

            string label = BuildingReflection.GetRelicDecisionLabel(_building, itemIndex) ?? $"Decision {itemIndex + 1}";
            float workTime = BuildingReflection.GetRelicDecisionWorkingTime(_building, itemIndex);

            string announcement = label;
            if (workTime > 0f)
                announcement += $", {Mathf.RoundToInt(workTime)} seconds";

            if (itemIndex == _selectedDecisionIndex)
                announcement += ", selected";

            // Append requirements summary for this decision
            string reqSummary = GetDecisionRequirementsSummary(itemIndex);
            if (!string.IsNullOrEmpty(reqSummary))
                announcement += $", requires: {reqSummary}";

            // Append effects (cached, only accurate for selected decision)
            if (itemIndex == _selectedDecisionIndex)
            {
                string effectsSummary = GetEffectsSummary();
                if (!string.IsNullOrEmpty(effectsSummary))
                    announcement += $", effects: {effectsSummary}";
            }

            // Append rewards for this decision
            string rewardsSummary = GetDecisionRewardsSummary(itemIndex);
            if (!string.IsNullOrEmpty(rewardsSummary))
                announcement += $", rewards: {rewardsSummary}";

            Speech.Say(announcement);
        }

        private string GetDecisionRequirementsSummary(int decisionIndex)
        {
            int setCount = BuildingReflection.GetRelicGoodsSetCount(_building, decisionIndex);
            if (setCount == 0) return null;

            var parts = new List<string>();
            for (int i = 0; i < setCount; i++)
            {
                int altCount = BuildingReflection.GetRelicGoodsAlternativeCount(_building, decisionIndex, i);
                if (altCount == 0) continue;

                if (altCount == 1)
                {
                    string name = BuildingReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, 0) ?? "Unknown";
                    int amount = BuildingReflection.GetRelicGoodAmount(_building, decisionIndex, i, 0);
                    parts.Add($"{name} {amount}");
                }
                else
                {
                    var alts = new List<string>();
                    for (int j = 0; j < altCount; j++)
                    {
                        string name = BuildingReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, j) ?? "Unknown";
                        int amount = BuildingReflection.GetRelicGoodAmount(_building, decisionIndex, i, j);
                        alts.Add($"{name} {amount}");
                    }
                    parts.Add(string.Join(" or ", alts));
                }
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private string GetEffectsSummary()
        {
            var parts = new List<string>();
            if (_workingEffects != null)
            {
                foreach (var e in _workingEffects)
                    parts.Add((e.IsPositive ? "+" : "-") + e.Name);
            }
            if (_activeEffects != null)
            {
                foreach (var e in _activeEffects)
                    parts.Add((e.IsPositive ? "+" : "-") + e.Name);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private string GetDecisionRewardsSummary(int decisionIndex)
        {
            var rewards = BuildingReflection.GetRelicDecisionRewards(_building, decisionIndex);
            if (rewards == null || rewards.Length == 0) return null;

            var parts = new List<string>();
            foreach (var r in rewards)
                parts.Add(r.Name);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private bool PerformDecisionAction(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _decisionCount) return false;

            if (BuildingReflection.SetRelicDecisionIndex(_building, itemIndex))
            {
                _selectedDecisionIndex = itemIndex;
                string label = BuildingReflection.GetRelicDecisionLabel(_building, itemIndex) ?? $"Decision {itemIndex + 1}";
                Speech.Say($"Selected: {label}");
                SoundManager.PlayButtonClick();

                // Refresh decision-dependent data and rebuild sections
                RefreshDecisionDetails();
                RefreshStatusData();
                BuildSections();
                return true;
            }

            Speech.Say("Cannot select decision");
            return false;
        }

        // ========================================
        // REQUIREMENTS SECTION
        // ========================================

        private void AnnounceRequirementItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return;

            var set = _goodsSets[itemIndex];
            if (set.alternativeCount == 0) return;
            int pickedIndex = set.pickedIndex;
            if (pickedIndex < 0 || pickedIndex >= set.alternativeCount) pickedIndex = 0;

            string displayName = set.goodDisplayNames[pickedIndex];
            int amount = set.goodAmounts[pickedIndex];

            if (_investigationStarted)
            {
                // Phase B: show delivery progress
                string goodName = set.goodNames[pickedIndex];
                int delivered = BuildingReflection.GetRelicDeliveredAmount(_building, goodName);
                string announcement = $"{displayName}: {delivered} of {amount} delivered";
                Speech.Say(announcement);
            }
            else
            {
                // Phase A: show requirement with stored amount
                string goodName = set.goodNames[pickedIndex];
                int inStorage = BuildingReflection.GetStoredGoodAmount(goodName);
                string announcement = $"{displayName}: {amount} ({inStorage} in storage)";
                if (set.alternativeCount > 1)
                    announcement += $", {set.alternativeCount - 1} other options";
                Speech.Say(announcement);
            }
        }

        private void AnnounceRequirementSubItem(int itemIndex, int subItemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return;

            var set = _goodsSets[itemIndex];
            if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return;

            string displayName = set.goodDisplayNames[subItemIndex];
            int amount = set.goodAmounts[subItemIndex];
            int inStorage = BuildingReflection.GetStoredGoodAmount(set.goodNames[subItemIndex]);
            string announcement = $"{displayName}: {amount} ({inStorage} in storage)";

            if (subItemIndex == set.pickedIndex)
                announcement += ", selected";

            Speech.Say(announcement);
        }

        private bool PerformRequirementSubItemAction(int itemIndex, int subItemIndex)
        {
            if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return false;
            if (_investigationStarted) return false;  // Can't change during investigation

            var set = _goodsSets[itemIndex];
            if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return false;

            int safeDecision = BuildingReflection.GetRelicSafeDecisionIndex(_building);
            if (BuildingReflection.SetRelicPickedGoodIndex(_building, safeDecision, itemIndex, subItemIndex))
            {
                _goodsSets[itemIndex].pickedIndex = subItemIndex;
                string displayName = set.goodDisplayNames[subItemIndex];
                Speech.Say($"Picked: {displayName}");
                SoundManager.PlayButtonClick();

                // Refresh status (availability may have changed)
                RefreshStatusData();
                _navigationLevel = 1;
                return true;
            }

            Speech.Say("Cannot pick good");
            return false;
        }

        // ========================================
        // EFFECTS SECTION
        // ========================================

        private int GetEffectsItemCount()
        {
            int count = 0;
            if (_workingEffects != null) count += _workingEffects.Length;
            if (_activeEffects != null) count += _activeEffects.Length;
            if (_dynamicEffects != null) count += _dynamicEffects.Length;
            if (_nextTierEffects != null) count += _nextTierEffects.Length;
            return count;
        }

        private void AnnounceEffectItem(int itemIndex)
        {
            var effect = GetEffectAtIndex(itemIndex);
            if (effect == null) return;

            string announcement = effect.Value.Name;
            announcement += effect.Value.IsPositive ? ", positive" : ", negative";

            // Indicate effect type
            if (IsWorkingEffect(itemIndex))
            {
                announcement += ", during investigation";
            }
            else if (IsNextTierEffect(itemIndex))
            {
                if (_timeToNextTier > 0f)
                    announcement += $", in {FormatDynamicEffectTime(_timeToNextTier)}";
                else
                    announcement += ", pending";
            }

            // Include description
            if (!string.IsNullOrEmpty(effect.Value.Description))
                announcement += ". " + effect.Value.Description;

            Speech.Say(announcement);
        }

        private bool PerformEffectAction(int itemIndex)
        {
            var effect = GetEffectAtIndex(itemIndex);
            if (effect == null) return false;

            if (!string.IsNullOrEmpty(effect.Value.Description))
            {
                Speech.Say(effect.Value.Description);
                return true;
            }
            return false;
        }

        private BuildingReflection.RelicEffectInfo? GetEffectAtIndex(int itemIndex)
        {
            int workingCount = _workingEffects?.Length ?? 0;
            int activeCount = _activeEffects?.Length ?? 0;
            int dynamicCount = _dynamicEffects?.Length ?? 0;
            int nextTierCount = _nextTierEffects?.Length ?? 0;

            if (itemIndex < 0) return null;

            if (itemIndex < workingCount)
                return _workingEffects[itemIndex];

            int activeIndex = itemIndex - workingCount;
            if (activeIndex < activeCount)
                return _activeEffects[activeIndex];

            int dynamicIndex = itemIndex - workingCount - activeCount;
            if (dynamicIndex < dynamicCount)
                return _dynamicEffects[dynamicIndex];

            int nextTierIndex = itemIndex - workingCount - activeCount - dynamicCount;
            if (nextTierIndex < nextTierCount)
                return _nextTierEffects[nextTierIndex];

            return null;
        }

        private bool IsWorkingEffect(int itemIndex)
        {
            int workingCount = _workingEffects?.Length ?? 0;
            return itemIndex >= 0 && itemIndex < workingCount;
        }

        private bool IsNextTierEffect(int itemIndex)
        {
            int workingCount = _workingEffects?.Length ?? 0;
            int activeCount = _activeEffects?.Length ?? 0;
            int dynamicCount = _dynamicEffects?.Length ?? 0;
            return itemIndex >= workingCount + activeCount + dynamicCount;
        }

        // ========================================
        // REWARDS SECTION
        // ========================================

        private void AnnounceRewardItem(int itemIndex)
        {
            if (_rewards == null || itemIndex < 0 || itemIndex >= _rewards.Length) return;

            string announcement = _rewards[itemIndex].Name;
            if (!string.IsNullOrEmpty(_rewards[itemIndex].Description))
                announcement += $", {_rewards[itemIndex].Description}";
            Speech.Say(announcement);
        }

        private bool PerformRewardAction(int itemIndex)
        {
            return false;  // Description shown inline in announcement
        }

        // ========================================
        // STATUS SECTION
        // ========================================

        protected override bool PerformSectionAction(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length) return false;

            var sectionType = _sectionTypes[sectionIndex];

            // Handle Cancel section (Phase B)
            if (sectionType == SectionType.Cancel && _investigationStarted && _canCancel)
            {
                return PerformCancelAction();
            }

            // Handle Start Investigation (Phase A Status section)
            if (sectionType != SectionType.Status) return false;
            if (_investigationStarted || _investigationFinished) return false;

            // Phase A: Start investigation from section level
            if (!_canStart)
            {
                Speech.Say(_startBlockingReason ?? "Cannot start");
                SoundManager.PlayFailed();
                return true;
            }

            if (BuildingReflection.RelicStartInvestigation(_building))
            {
                Speech.Say("Investigation started");
                SoundManager.PlayButtonClick();
                var startSound = BuildingReflection.GetRelicInvestigationStartSoundModel(_building);
                SoundManager.PlaySoundEffect(startSound);
                if (BuildingReflection.RelicHasWorkingEffects(_building))
                    SoundManager.PlayRelicStartWithWorkingEffects();

                _investigationStarted = true;
                _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
                _maxWorkers = _workerIds?.Length ?? 0;
                RefreshDecisionDetails();
                RefreshStatusData();
                BuildSections();
                _currentSectionIndex = 0;
                _navigationLevel = 0;
                return true;
            }
            else
            {
                Speech.Say("Failed to start investigation");
                SoundManager.PlayFailed();
                return true;
            }
        }

        private bool PerformCancelAction()
        {
            if (BuildingReflection.RelicCancelInvestigation(_building))
            {
                Speech.Say("Investigation cancelled");
                SoundManager.PlayButtonClick();
                if (BuildingReflection.RelicHasWorkingEffects(_building))
                    SoundManager.PlayRelicStopWithWorkingEffects();

                // Refresh to transition back to Phase A
                _investigationStarted = false;
                _progress = 0f;
                _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
                _maxWorkers = _workerIds?.Length ?? 0;
                RefreshDecisionDetails();
                RefreshStatusData();
                BuildSections();
                _currentSectionIndex = 0;
                _navigationLevel = 0;
                return true;
            }
            else
            {
                Speech.Say("Failed to cancel");
                SoundManager.PlayFailed();
                return false;
            }
        }

        private int GetStatusItemCount()
        {
            // Status is now section-level only (no items to drill into)
            return 0;
        }

        // ========================================
        // STORAGE SECTION (Phase C)
        // ========================================

        private void RefreshStorageData()
        {
            _storageItems.Clear();
            _storageTotalSum = 0;

            if (!_investigationFinished) return;

            _storageItems = BuildingReflection.GetRelicRewardStorageItems(_building);
            _storageTotalSum = BuildingReflection.GetRelicRewardStorageFullSum(_building);
        }

        private void AnnounceStorageItem(int itemIndex)
        {
            // Refresh storage data to get current amounts
            RefreshStorageData();

            if (itemIndex < 0 || itemIndex >= _storageItems.Count) return;

            var (_, displayName, amount) = _storageItems[itemIndex];
            Speech.Say($"{displayName}: {amount}");
        }

        // ========================================
        // WORKERS SECTION
        // ========================================

        private void RefreshAvailableRaces(bool force = false)
        {
            if (!force && _racesRefreshedForWorkerSection) return;

            _availableRaces = BuildingReflection.GetRacesWithFreeWorkers(includeZeroFree: true);
            _racesRefreshedForWorkerSection = true;
        }

        private bool IsValidWorkerIndex(int workerIndex)
        {
            return _workerIds != null && workerIndex >= 0 && workerIndex < _workerIds.Length;
        }

        private int GetWorkerSubItemCount(int workerIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return 0;

            RefreshAvailableRaces();

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int count = _availableRaces.Count;
            if (slotOccupied) count++;  // Add "Unassign" option

            return count;
        }

        private void AnnounceWorkerItem(int itemIndex)
        {
            // Refresh races on each worker announcement to catch changes during panel session
            RefreshAvailableRaces(force: true);

            if (!IsValidWorkerIndex(itemIndex))
            {
                Speech.Say("Invalid worker slot");
                return;
            }

            int workerId = _workerIds[itemIndex];
            int slotNum = itemIndex + 1;

            if (workerId <= 0)
            {
                Speech.Say($"Worker slot {slotNum}: Empty");
                return;
            }

            string workerDesc = BuildingReflection.GetWorkerDescription(workerId);
            if (string.IsNullOrEmpty(workerDesc))
            {
                Speech.Say($"Worker slot {slotNum}: Assigned");
                return;
            }

            Speech.Say($"Worker slot {slotNum}: {workerDesc}");
        }

        private void AnnounceWorkerSubItem(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex))
            {
                Speech.Say("Invalid worker slot");
                return;
            }

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                Speech.Say("Unassign worker");
                return;
            }

            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                var (raceName, freeCount) = _availableRaces[raceIndex];
                Speech.Say($"{raceName}: {freeCount} available");
            }
            else
            {
                Speech.Say("Invalid option");
            }
        }

        private bool PerformWorkerSubItemAction(int workerIndex, int subItemIndex)
        {
            if (!IsValidWorkerIndex(workerIndex)) return false;

            bool slotOccupied = !BuildingReflection.IsWorkerSlotEmpty(_building, workerIndex);
            int raceOffset = slotOccupied ? 1 : 0;

            if (slotOccupied && subItemIndex == 0)
            {
                // Unassign
                if (BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex))
                {
                    _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
                    RefreshAvailableRaces(force: true);
                    Speech.Say("Worker unassigned");
                    _navigationLevel = 1;
                    return true;
                }
                else
                {
                    Speech.Say("Cannot unassign worker");
                    return false;
                }
            }

            // Assign race
            int raceIndex = subItemIndex - raceOffset;
            if (raceIndex >= 0 && raceIndex < _availableRaces.Count)
            {
                var (raceName, freeCount) = _availableRaces[raceIndex];

                // Check if race has free workers
                if (freeCount == 0)
                {
                    Speech.Say($"No free {raceName} workers");
                    SoundManager.PlayFailed();
                    return false;
                }

                if (slotOccupied)
                {
                    BuildingReflection.UnassignWorkerFromSlot(_building, workerIndex);
                    _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
                }

                if (BuildingReflection.AssignWorkerToSlot(_building, workerIndex, raceName))
                {
                    _workerIds = BuildingReflection.GetRelicWorkerIds(_building);
                    RefreshAvailableRaces(force: true);

                    if (IsValidWorkerIndex(workerIndex))
                    {
                        string workerDesc = BuildingReflection.GetWorkerDescription(_workerIds[workerIndex]);
                        Speech.Say($"Assigned: {workerDesc ?? raceName}");
                    }
                    else
                    {
                        Speech.Say($"Assigned: {raceName}");
                    }
                    _navigationLevel = 1;
                    return true;
                }
                else
                {
                    Speech.Say($"Cannot assign {raceName}");
                    return false;
                }
            }

            return false;
        }
    }
}
