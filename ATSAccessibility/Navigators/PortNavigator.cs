using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Port buildings (expeditions).
    /// Provides phase-based navigation:
    /// - Phase 1 (Planning): Pick goods, adjust level, pick category, confirm
    /// - Phase 2 (Collecting): View delivery progress, cancel
    /// - Phase 3 (In Progress): View progress/time (read-only)
    /// - Phase 4 (Rewards): View rewards, accept
    /// </summary>
    public class PortNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Level,
            Workers,
            Goods,
            Category,
            RewardsPreview,
            Confirm,
            GoodsProgress,
            Cancel,
            Status,
            Rewards,
            AcceptRewards,
            Upgrades
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;

        // Phase state
        private bool _wasDecisionMade;
        private bool _expeditionStarted;
        private bool _rewardsWaiting;

        // Info
        private int _expeditionLevel;
        private int _maxLevel;
        private float _duration;

        // Goods
        private struct GoodsSetData
        {
            public int alternativeCount;
            public string[] goodDisplayNames;
            public string[] goodNames;
            public int[] goodAmounts;
            public int pickedIndex;
        }

        private GoodsSetData[] _striderSets;
        private int _striderSetCount;
        private GoodsSetData[] _crewSets;
        private int _crewSetCount;

        // Delivery tracking (Phase 2)
        private struct DeliveryItem
        {
            public string displayName;
            public string name;
            public int delivered;
            public int needed;
        }
        private DeliveryItem[] _deliveryItems;
        private int _deliveryItemCount;

        // Categories
        private List<string> _categoryDisplayNames;
        private List<string> _categoryInternalNames;
        private string _pickedCategory;
        private bool _hasBlueprintReward;

        // Rewards preview
        private List<(string rarity, int chance)> _rewardChances;

        // Rewards (Phase 4)
        private string _blueprintReward;
        private string _perkReward;

        // Status (Phase 3)
        private float _progress;
        private float _timeLeft;

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
                case SectionType.Level:
                    return 0;
                case SectionType.Workers:
                    return _workersSection.GetItemCount();
                case SectionType.Goods:
                    return _striderSetCount + _crewSetCount;
                case SectionType.Category:
                    return _categoryDisplayNames?.Count ?? 0;
                case SectionType.RewardsPreview:
                    return _rewardChances?.Count ?? 0;
                case SectionType.Confirm:
                    return 0;  // Section-level action
                case SectionType.GoodsProgress:
                    return _deliveryItemCount;
                case SectionType.Cancel:
                    return 0;  // Section-level action
                case SectionType.Status:
                    return 0;  // Front-loaded info at section level
                case SectionType.Rewards:
                    return GetRewardsItemCount();
                case SectionType.AcceptRewards:
                    return 0;  // Section-level action
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

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    return _workersSection.GetSubItemCount(itemIndex);
                case SectionType.Goods:
                    return GetGoodsSubItemCount(itemIndex);
                case SectionType.Upgrades:
                    return _upgradesSection.GetSubItemCount(itemIndex);
                default:
                    return 0;
            }
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Level:
                    if (_maxLevel > 1)
                        Speech.Say($"Level {_expeditionLevel} of {_maxLevel}, duration {FormatDuration(_duration)}");
                    else
                        Speech.Say($"Level {_expeditionLevel}, duration {FormatDuration(_duration)}");
                    break;
                case SectionType.Status:
                    Speech.Say($"Status: In progress, {FormatProgress()}, {FormatTimeLeft()}");
                    break;
                case SectionType.Confirm:
                    if (BuildingReflection.IsPortBlockedByUnpickedCategory(_building))
                        Speech.Say("Confirm, pick a category first");
                    else
                        Speech.Say("Confirm Expedition");
                    break;
                case SectionType.Cancel:
                    Speech.Say("Cancel Expedition");
                    break;
                case SectionType.AcceptRewards:
                    Speech.Say("Accept Rewards");
                    break;
                default:
                    Speech.Say(_sectionNames[sectionIndex]);
                    break;
            }
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    _workersSection.AnnounceItem(itemIndex);
                    break;
                case SectionType.Goods:
                    AnnounceGoodsItemCombined(itemIndex);
                    break;
                case SectionType.Category:
                    AnnounceCategoryItem(itemIndex);
                    break;
                case SectionType.RewardsPreview:
                    AnnounceRewardsPreviewItem(itemIndex);
                    break;
                case SectionType.GoodsProgress:
                    AnnounceGoodsProgressItem(itemIndex);
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
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    _workersSection.AnnounceSubItem(itemIndex, subItemIndex);
                    break;
                case SectionType.Goods:
                    AnnounceGoodsSubItemCombined(itemIndex, subItemIndex);
                    break;
                case SectionType.Upgrades:
                    _upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
                    break;
            }
        }

        protected override bool PerformSectionAction(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Confirm:
                    return PerformConfirmAction();
                case SectionType.Cancel:
                    return PerformCancelAction();
                case SectionType.AcceptRewards:
                    return PerformAcceptRewardsAction();
                default:
                    return false;
            }
        }

        protected override bool PerformItemAction(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Category:
                    return PerformCategoryAction(itemIndex);
                default:
                    return false;
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return false;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    if (_workersSection.PerformSubItemAction(itemIndex, subItemIndex))
                    {
                        _navigationLevel = 1;
                        return true;
                    }
                    return false;
                case SectionType.Goods:
                    return PerformGoodsSubItemAction(itemIndex, subItemIndex);
                case SectionType.Upgrades:
                    return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
                default:
                    return false;
            }
        }

        protected override void AdjustSectionValue(int sectionIndex, int delta, KeyboardManager.KeyModifiers modifiers)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            if (_sectionTypes[sectionIndex] == SectionType.Level)
            {
                int newLevel = Mathf.Clamp(_expeditionLevel + delta, 1, _maxLevel);
                if (newLevel == _expeditionLevel)
                {
                    SoundManager.PlayFailed();
                    return;
                }

                if (BuildingReflection.PortChangeLevel(_building, newLevel))
                {
                    SoundManager.PlayButtonClick();
                    RefreshData();
                    Speech.Say($"Level {_expeditionLevel} of {_maxLevel}, duration {FormatDuration(_duration)}");
                }
            }
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
                case SectionType.Workers:
                    return _workersSection.GetItemName(itemIndex);
                case SectionType.Goods:
                    return GetGoodsItemName(itemIndex);
                case SectionType.Category:
                    if (_categoryDisplayNames != null && itemIndex >= 0 && itemIndex < _categoryDisplayNames.Count)
                        return _categoryDisplayNames[itemIndex];
                    return null;
                case SectionType.GoodsProgress:
                    if (_deliveryItems != null && itemIndex >= 0 && itemIndex < _deliveryItemCount)
                        return _deliveryItems[itemIndex].displayName;
                    return null;
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

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Workers:
                    return _workersSection.GetSubItemName(itemIndex, subItemIndex);
                case SectionType.Goods:
                    return GetGoodsSubItemName(itemIndex, subItemIndex);
                case SectionType.Upgrades:
                    return _upgradesSection.GetSubItemName(itemIndex, subItemIndex);
                default:
                    return null;
            }
        }

        // ========================================
        // DATA MANAGEMENT
        // ========================================

        protected override void RefreshData()
        {
            // Phase detection
            _wasDecisionMade = BuildingReflection.WasPortDecisionMade(_building);
            _expeditionStarted = BuildingReflection.IsPortExpeditionStarted(_building);
            _rewardsWaiting = BuildingReflection.ArePortRewardsWaiting(_building);

            // Info
            _expeditionLevel = BuildingReflection.GetPortExpeditionLevel(_building);
            _maxLevel = BuildingReflection.GetPortMaxLevel(_building);
            _duration = BuildingReflection.GetPortDuration(_building);

            // Status (Phase 3)
            _progress = BuildingReflection.GetPortProgress(_building);
            _timeLeft = BuildingReflection.GetPortTimeLeft(_building);

            // Rewards (Phase 4)
            _blueprintReward = BuildingReflection.GetPortBlueprintReward(_building);
            _perkReward = BuildingReflection.GetPortPerkReward(_building);

            // Goods and categories (Phase 1)
            if (!_wasDecisionMade)
            {
                RefreshGoodsSets();
                RefreshCategories();
                RefreshRewardChances();
            }
            else if (!_expeditionStarted)
            {
                // Phase 2: Delivery tracking
                RefreshDeliveryItems();
            }

            BuildSections();

            Debug.Log($"[ATSAccessibility] PortNavigator: Refreshed data, phase={GetPhaseString()}");
        }

        protected override void ClearData()
        {
            _sectionNames = null;
            _sectionTypes = null;
            _striderSets = null;
            _striderSetCount = 0;
            _crewSets = null;
            _crewSetCount = 0;
            ClearWorkersSection();
            _deliveryItems = null;
            _deliveryItemCount = 0;
            _categoryDisplayNames = null;
            _categoryInternalNames = null;
            _rewardChances = null;
            _blueprintReward = null;
            _perkReward = null;
            ClearUpgradesSection();
        }

        private string GetPhaseString()
        {
            if (_rewardsWaiting) return "4-Rewards";
            if (_expeditionStarted) return "3-InProgress";
            if (_wasDecisionMade) return "2-Collecting";
            return "1-Planning";
        }

        // ========================================
        // SECTION BUILDING
        // ========================================

        private void BuildSections()
        {
            var sectionNames = new List<string>();
            var sectionTypes = new List<SectionType>();

            if (_rewardsWaiting)
            {
                // Phase 4: Rewards
                if (!string.IsNullOrEmpty(_blueprintReward) || !string.IsNullOrEmpty(_perkReward))
                {
                    sectionNames.Add("Rewards");
                    sectionTypes.Add(SectionType.Rewards);
                }
                sectionNames.Add("Accept Rewards");
                sectionTypes.Add(SectionType.AcceptRewards);
            }
            else if (_expeditionStarted)
            {
                // Phase 3: In Progress - Status front-loads progress and time
                sectionNames.Add("Status");
                sectionTypes.Add(SectionType.Status);
            }
            else if (_wasDecisionMade)
            {
                // Phase 2: Collecting
                if (TryInitializeWorkersSection())
                {
                    sectionNames.Add("Workers");
                    sectionTypes.Add(SectionType.Workers);
                }
                if (_deliveryItemCount > 0)
                {
                    sectionNames.Add("Goods Progress");
                    sectionTypes.Add(SectionType.GoodsProgress);
                }
                sectionNames.Add("Cancel");
                sectionTypes.Add(SectionType.Cancel);
            }
            else
            {
                // Phase 1: Planning - no Info, start with Level
                sectionNames.Add("Level");
                sectionTypes.Add(SectionType.Level);
                if (_striderSetCount + _crewSetCount > 0)
                {
                    sectionNames.Add("Food Choices");
                    sectionTypes.Add(SectionType.Goods);
                }
                if (_hasBlueprintReward && _categoryDisplayNames != null && _categoryDisplayNames.Count > 0)
                {
                    sectionNames.Add("Choose Blueprint Reward Category");
                    sectionTypes.Add(SectionType.Category);
                }
                else
                {
                    sectionNames.Add("No blueprint reward this expedition");
                    sectionTypes.Add(SectionType.Category);
                }
                if (_rewardChances != null && _rewardChances.Count > 0)
                {
                    sectionNames.Add("Rewards Preview");
                    sectionTypes.Add(SectionType.RewardsPreview);
                }
                sectionNames.Add("Confirm");
                sectionTypes.Add(SectionType.Confirm);
            }

            // Upgrades in all phases
            if (TryInitializeUpgradesSection())
            {
                sectionNames.Add("Upgrades");
                sectionTypes.Add(SectionType.Upgrades);
            }

            _sectionNames = sectionNames.ToArray();
            _sectionTypes = sectionTypes.ToArray();
        }

        // ========================================
        // GOODS DATA REFRESH
        // ========================================

        private void RefreshGoodsSets()
        {
            // Strider goods
            _striderSetCount = BuildingReflection.GetPortStriderGoodSetCount(_building);
            if (_striderSetCount > 0)
            {
                _striderSets = new GoodsSetData[_striderSetCount];
                for (int i = 0; i < _striderSetCount; i++)
                {
                    _striderSets[i] = FetchGoodsSetData(true, i);
                }
            }
            else
            {
                _striderSets = null;
            }

            // Crew goods
            _crewSetCount = BuildingReflection.GetPortCrewGoodSetCount(_building);
            if (_crewSetCount > 0)
            {
                _crewSets = new GoodsSetData[_crewSetCount];
                for (int i = 0; i < _crewSetCount; i++)
                {
                    _crewSets[i] = FetchGoodsSetData(false, i);
                }
            }
            else
            {
                _crewSets = null;
            }
        }

        private GoodsSetData FetchGoodsSetData(bool isStrider, int setIndex)
        {
            int altCount = isStrider
                ? BuildingReflection.GetPortStriderAlternativeCount(_building, setIndex)
                : BuildingReflection.GetPortCrewAlternativeCount(_building, setIndex);
            int pickedIndex = isStrider
                ? BuildingReflection.GetPortStriderPickedIndex(_building, setIndex)
                : BuildingReflection.GetPortCrewPickedIndex(_building, setIndex);

            var displayNames = new string[altCount];
            var names = new string[altCount];
            var amounts = new int[altCount];

            for (int j = 0; j < altCount; j++)
            {
                if (isStrider)
                {
                    displayNames[j] = BuildingReflection.GetPortStriderGoodDisplayName(_building, setIndex, j) ?? "Unknown";
                    names[j] = BuildingReflection.GetPortStriderGoodName(_building, setIndex, j);
                    amounts[j] = BuildingReflection.GetPortStriderGoodAmount(_building, setIndex, j);
                }
                else
                {
                    displayNames[j] = BuildingReflection.GetPortCrewGoodDisplayName(_building, setIndex, j) ?? "Unknown";
                    names[j] = BuildingReflection.GetPortCrewGoodName(_building, setIndex, j);
                    amounts[j] = BuildingReflection.GetPortCrewGoodAmount(_building, setIndex, j);
                }
            }

            return new GoodsSetData
            {
                alternativeCount = altCount,
                goodDisplayNames = displayNames,
                goodNames = names,
                goodAmounts = amounts,
                pickedIndex = pickedIndex
            };
        }

        // ========================================
        // DELIVERY TRACKING (Phase 2)
        // ========================================

        private void RefreshDeliveryItems()
        {
            var items = new List<DeliveryItem>();

            // Gather picked strider goods
            int striderCount = BuildingReflection.GetPortStriderGoodSetCount(_building);
            for (int i = 0; i < striderCount; i++)
            {
                int pickedIndex = BuildingReflection.GetPortStriderPickedIndex(_building, i);
                string name = BuildingReflection.GetPortStriderGoodName(_building, i, pickedIndex);
                string displayName = BuildingReflection.GetPortStriderGoodDisplayName(_building, i, pickedIndex) ?? "Unknown";
                int amount = BuildingReflection.GetPortStriderGoodAmount(_building, i, pickedIndex);
                int delivered = !string.IsNullOrEmpty(name)
                    ? BuildingReflection.GetPortGoodDeliveredAmount(_building, name)
                    : 0;

                items.Add(new DeliveryItem
                {
                    displayName = displayName,
                    name = name,
                    delivered = delivered,
                    needed = amount
                });
            }

            // Gather picked crew goods
            int crewCount = BuildingReflection.GetPortCrewGoodSetCount(_building);
            for (int i = 0; i < crewCount; i++)
            {
                int pickedIndex = BuildingReflection.GetPortCrewPickedIndex(_building, i);
                string name = BuildingReflection.GetPortCrewGoodName(_building, i, pickedIndex);
                string displayName = BuildingReflection.GetPortCrewGoodDisplayName(_building, i, pickedIndex) ?? "Unknown";
                int amount = BuildingReflection.GetPortCrewGoodAmount(_building, i, pickedIndex);
                int delivered = !string.IsNullOrEmpty(name)
                    ? BuildingReflection.GetPortGoodDeliveredAmount(_building, name)
                    : 0;

                items.Add(new DeliveryItem
                {
                    displayName = displayName,
                    name = name,
                    delivered = delivered,
                    needed = amount
                });
            }

            _deliveryItems = items.ToArray();
            _deliveryItemCount = items.Count;
        }

        // ========================================
        // CATEGORIES
        // ========================================

        private void RefreshCategories()
        {
            _hasBlueprintReward = BuildingReflection.PortHasBlueprintReward(_building);
            if (_hasBlueprintReward)
            {
                _categoryDisplayNames = BuildingReflection.GetPortAvailableCategories(_building);
                _categoryInternalNames = BuildingReflection.GetPortCategoryInternalNames(_building);
                _pickedCategory = BuildingReflection.GetPortPickedCategory(_building);
            }
            else
            {
                _categoryDisplayNames = null;
                _categoryInternalNames = null;
                _pickedCategory = null;
            }
        }

        // ========================================
        // REWARD CHANCES
        // ========================================

        private void RefreshRewardChances()
        {
            _rewardChances = BuildingReflection.GetPortRewardChances(_building);
        }

        // ========================================
        // GOODS SECTION (Phase 1)
        // ========================================

        /// <summary>
        /// Resolve a combined goods item index to the appropriate set and local index.
        /// Items 0..striderSetCount-1 are strider, then crewSetCount crew items follow.
        /// </summary>
        private bool ResolveGoodsIndex(int itemIndex, out GoodsSetData[] sets, out int localIndex, out bool isStrider)
        {
            if (itemIndex >= 0 && itemIndex < _striderSetCount && _striderSets != null)
            {
                sets = _striderSets;
                localIndex = itemIndex;
                isStrider = true;
                return true;
            }

            int crewIndex = itemIndex - _striderSetCount;
            if (crewIndex >= 0 && crewIndex < _crewSetCount && _crewSets != null)
            {
                sets = _crewSets;
                localIndex = crewIndex;
                isStrider = false;
                return true;
            }

            sets = null;
            localIndex = -1;
            isStrider = false;
            return false;
        }

        private int GetGoodsSubItemCount(int itemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out _))
                return 0;
            if (sets[localIndex].alternativeCount > 1)
                return sets[localIndex].alternativeCount;
            return 0;
        }

        private void AnnounceGoodsItemCombined(int itemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out bool isStrider))
                return;

            var set = sets[localIndex];
            if (set.alternativeCount == 0) return;

            int pickedIndex = set.pickedIndex;
            if (pickedIndex < 0 || pickedIndex >= set.alternativeCount) pickedIndex = 0;

            string prefix = isStrider ? "Strider food" : "Crew food";
            string displayName = set.goodDisplayNames[pickedIndex];
            int amount = set.goodAmounts[pickedIndex];
            int inStorage = BuildingReflection.GetStoredGoodAmount(set.goodNames[pickedIndex]);

            string announcement = $"{prefix}: {displayName}, {amount} ({inStorage} in storage)";
            if (set.alternativeCount > 1)
                announcement += $", {set.alternativeCount - 1} other options";

            Speech.Say(announcement);
        }

        private void AnnounceGoodsSubItemCombined(int itemIndex, int subItemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out _))
                return;

            var set = sets[localIndex];
            if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return;

            string displayName = set.goodDisplayNames[subItemIndex];
            int amount = set.goodAmounts[subItemIndex];
            int inStorage = BuildingReflection.GetStoredGoodAmount(set.goodNames[subItemIndex]);
            string announcement = $"{displayName}: {amount} ({inStorage} in storage)";

            if (subItemIndex == set.pickedIndex)
                announcement += ", selected";

            Speech.Say(announcement);
        }

        private bool PerformGoodsSubItemAction(int itemIndex, int subItemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out bool isStrider))
                return false;

            var set = sets[localIndex];
            if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return false;

            bool success = isStrider
                ? BuildingReflection.SetPortStriderPickedIndex(_building, localIndex, subItemIndex)
                : BuildingReflection.SetPortCrewPickedIndex(_building, localIndex, subItemIndex);

            if (success)
            {
                sets[localIndex].pickedIndex = subItemIndex;
                Speech.Say($"Picked: {set.goodDisplayNames[subItemIndex]}");
                SoundManager.PlayButtonClick();
                _navigationLevel = 1;
                return true;
            }

            Speech.Say("Cannot pick good");
            return false;
        }

        private string GetGoodsItemName(int itemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out _))
                return null;
            var set = sets[localIndex];
            int picked = set.pickedIndex;
            if (picked >= 0 && picked < set.goodDisplayNames.Length)
                return set.goodDisplayNames[picked];
            return null;
        }

        private string GetGoodsSubItemName(int itemIndex, int subItemIndex)
        {
            if (!ResolveGoodsIndex(itemIndex, out var sets, out int localIndex, out _))
                return null;
            var set = sets[localIndex];
            if (subItemIndex >= 0 && subItemIndex < set.alternativeCount)
                return set.goodDisplayNames[subItemIndex];
            return null;
        }

        // ========================================
        // CATEGORY SECTION (Phase 1)
        // ========================================

        private void AnnounceCategoryItem(int itemIndex)
        {
            if (_categoryDisplayNames == null || itemIndex < 0 || itemIndex >= _categoryDisplayNames.Count)
                return;

            string displayName = _categoryDisplayNames[itemIndex];
            string announcement = displayName;

            // Check if this category is picked
            if (_categoryInternalNames != null && itemIndex < _categoryInternalNames.Count
                && !string.IsNullOrEmpty(_pickedCategory)
                && _categoryInternalNames[itemIndex] == _pickedCategory)
            {
                announcement += ", selected";
            }

            Speech.Say(announcement);
        }

        private bool PerformCategoryAction(int itemIndex)
        {
            if (_categoryInternalNames == null || itemIndex < 0 || itemIndex >= _categoryInternalNames.Count)
                return false;

            string internalName = _categoryInternalNames[itemIndex];
            if (BuildingReflection.SetPortPickedCategory(_building, internalName))
            {
                _pickedCategory = internalName;
                string displayName = _categoryDisplayNames[itemIndex];
                Speech.Say($"Picked: {displayName}");
                SoundManager.PlayButtonClick();
                return true;
            }

            Speech.Say("Cannot pick category");
            return false;
        }

        // ========================================
        // REWARDS PREVIEW SECTION (Phase 1)
        // ========================================

        private void AnnounceRewardsPreviewItem(int itemIndex)
        {
            if (_rewardChances == null || itemIndex < 0 || itemIndex >= _rewardChances.Count)
                return;

            var (rarity, chance) = _rewardChances[itemIndex];
            Speech.Say($"{rarity}: {chance} percent");
        }

        // ========================================
        // GOODS PROGRESS SECTION (Phase 2)
        // ========================================

        private void AnnounceGoodsProgressItem(int itemIndex)
        {
            if (_deliveryItems == null || itemIndex < 0 || itemIndex >= _deliveryItemCount)
                return;

            var item = _deliveryItems[itemIndex];
            Speech.Say($"{item.displayName}, {item.delivered} of {item.needed}");
        }

        // ========================================
        // STATUS SECTION (Phase 3) - Front-loaded at section level
        // ========================================

        private string FormatProgress()
        {
            int percentage = Mathf.RoundToInt(_progress * 100f);
            return $"{percentage}%";
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

        // ========================================
        // REWARDS SECTION (Phase 4)
        // ========================================

        private int GetRewardsItemCount()
        {
            int count = 0;
            if (!string.IsNullOrEmpty(_blueprintReward)) count++;
            if (!string.IsNullOrEmpty(_perkReward)) count++;
            return count > 0 ? count : 1;
        }

        private void AnnounceRewardsItem(int itemIndex)
        {
            var rewards = new List<string>();

            if (!string.IsNullOrEmpty(_blueprintReward))
            {
                string displayName = GetBuildingDisplayName(_blueprintReward);
                rewards.Add($"Blueprint: {displayName}");
            }

            if (!string.IsNullOrEmpty(_perkReward))
            {
                string displayName = GetEffectDisplayName(_perkReward);
                rewards.Add($"Perk: {displayName}");
            }

            if (rewards.Count == 0)
            {
                Speech.Say("No rewards");
                return;
            }

            if (itemIndex < rewards.Count)
                Speech.Say(rewards[itemIndex]);
        }

        // ========================================
        // ACTIONS
        // ========================================

        private bool PerformConfirmAction()
        {
            if (BuildingReflection.IsPortBlockedByUnpickedCategory(_building))
            {
                Speech.Say("Pick a category first");
                SoundManager.PlayFailed();
                return true;
            }

            if (BuildingReflection.PortLockDecision(_building))
            {
                Speech.Say("Expedition confirmed");
                SoundManager.PlayPortStartClick();
                RefreshData();
                _currentSectionIndex = 0;
                _navigationLevel = 0;
                AnnounceSection(0);
                return true;
            }

            Speech.Say("Cannot confirm");
            SoundManager.PlayFailed();
            return true;
        }

        private bool PerformCancelAction()
        {
            if (BuildingReflection.PortCancelDecision(_building))
            {
                Speech.Say("Expedition cancelled");
                SoundManager.PlayPortCancelClick();
                RefreshData();
                _currentSectionIndex = 0;
                _navigationLevel = 0;
                AnnounceSection(0);
                return true;
            }

            Speech.Say("Cannot cancel");
            SoundManager.PlayFailed();
            return true;
        }

        private bool PerformAcceptRewardsAction()
        {
            if (BuildingReflection.PortAcceptRewards(_building))
            {
                Speech.Say("Rewards accepted");
                SoundManager.PlayPortRewardsClick();
                RefreshData();
                _currentSectionIndex = 0;
                _navigationLevel = 0;
                AnnounceSection(0);
                return true;
            }

            Speech.Say("Cannot accept rewards");
            SoundManager.PlayFailed();
            return true;
        }

        // ========================================
        // HELPERS
        // ========================================

        private string FormatDuration(float seconds)
        {
            int totalSeconds = Mathf.RoundToInt(seconds);
            if (totalSeconds < 60)
                return $"{totalSeconds} seconds";

            int minutes = totalSeconds / 60;
            int remainingSecs = totalSeconds % 60;
            if (remainingSecs > 0)
                return $"{minutes} minutes {remainingSecs} seconds";
            return $"{minutes} minutes";
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
            return CleanupName(effectName);
        }

        private string CleanupName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            name = name.Replace("[", "").Replace("]", "");

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
