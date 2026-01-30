using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the IronmanUpgradePopup (Queen's Hand Trial upgrades).
    /// Three-level navigation: sections -> items -> rewards.
    /// Sections: Pick Options (3 choices), Core Upgrades, and Unlocked.
    /// </summary>
    public class IronmanOverlay : IKeyHandler
    {
        private enum Level { Sections, Items, Rewards }
        private enum SectionType { PickOptions, CoreUpgrades, Unlocked }

        // State
        private bool _isOpen;
        private bool _suspended;
        private Level _level;
        private int _currentSectionIndex;
        private int _currentItemIndex;
        private int _currentRewardIndex;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Section data
        private SectionType[] _sections;
        private string[] _sectionNames;

        // Item data
        private List<IronmanReflection.UpgradeInfo> _currentItems = new List<IronmanReflection.UpgradeInfo>();
        private List<IronmanReflection.RewardInfo> _rewards = new List<IronmanReflection.RewardInfo>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen && !_suspended;

        public bool IsSuspended => _isOpen && _suspended;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (_level)
            {
                case Level.Sections:
                    return ProcessSectionKey(keyCode);
                case Level.Items:
                    return ProcessItemKey(keyCode);
                case Level.Rewards:
                    return ProcessRewardKey(keyCode);
                default:
                    return true;
            }
        }

        // ========================================
        // Public Methods
        // ========================================

        public void Open()
        {
            BuildSections();

            if (_sections.Length == 0)
            {
                Debug.LogWarning("[ATSAccessibility] Ironman overlay: no sections available");
                return;
            }

            _isOpen = true;
            _suspended = false;
            _level = Level.Sections;
            _currentSectionIndex = 0;
            _currentItemIndex = 0;
            _currentRewardIndex = 0;
            _currentItems.Clear();
            _rewards.Clear();
            _search.Clear();

            AnnounceOpen();
        }

        public void Close()
        {
            _isOpen = false;
            _suspended = false;
            _sections = null;
            _sectionNames = null;
            _currentItems.Clear();
            _rewards.Clear();
            _currentSectionIndex = 0;
            _currentItemIndex = 0;
            _currentRewardIndex = 0;
            _search.Clear();
        }

        public void Resume()
        {
            if (!_isOpen) return;

            _suspended = false;
            AnnounceCurrentLevel();
        }

        // ========================================
        // Section Building
        // ========================================

        private void BuildSections()
        {
            var sectionList = new List<SectionType>();
            var nameList = new List<string>();

            // Add Random Upgrades section only if not at max picks
            if (!IronmanReflection.HasReachedMaxPicks())
            {
                sectionList.Add(SectionType.PickOptions);
                nameList.Add("Random Upgrades");
            }

            // Always add Core Upgrades
            sectionList.Add(SectionType.CoreUpgrades);
            nameList.Add("Core Upgrades");

            // Add Unlocked section if there are unlocked upgrades
            var unlocked = IronmanReflection.GetUnlockedUpgrades();
            if (unlocked.Count > 0)
            {
                sectionList.Add(SectionType.Unlocked);
                nameList.Add("Unlocked");
            }

            _sections = sectionList.ToArray();
            _sectionNames = nameList.ToArray();
        }

        // ========================================
        // Section Level Keys
        // ========================================

        private bool ProcessSectionKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateSections(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateSections(-1);
                    return true;

                case KeyCode.Home:
                    if (_sections != null && _sections.Length > 0)
                    {
                        _currentSectionIndex = 0;
                        AnnounceSection();
                    }
                    return true;

                case KeyCode.End:
                    if (_sections != null && _sections.Length > 0)
                    {
                        _currentSectionIndex = _sections.Length - 1;
                        AnnounceSection();
                    }
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    EnterItems();
                    return true;

                case KeyCode.LeftArrow:
                    Close();
                    // Pass to game to close popup
                    return false;

                case KeyCode.Escape:
                    Close();
                    // Pass to game to close popup
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        HandleSearchKey(keyCode);
                        return true;
                    }
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // Item Level Keys
        // ========================================

        private bool ProcessItemKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateItems(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateItems(-1);
                    return true;

                case KeyCode.Home:
                    if (_currentItems.Count > 0)
                    {
                        _currentItemIndex = 0;
                        AnnounceItem();
                    }
                    return true;

                case KeyCode.End:
                    if (_currentItems.Count > 0)
                    {
                        _currentItemIndex = _currentItems.Count - 1;
                        AnnounceItem();
                    }
                    return true;

                case KeyCode.RightArrow:
                    EnterRewards();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ActivateItem();
                    return true;

                case KeyCode.LeftArrow:
                    _level = Level.Sections;
                    _currentItems.Clear();
                    _search.Clear();
                    AnnounceSection();
                    return true;

                case KeyCode.Escape:
                    // Go back to sections, don't close popup
                    _level = Level.Sections;
                    _currentItems.Clear();
                    _search.Clear();
                    AnnounceSection();
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        HandleSearchKey(keyCode);
                        return true;
                    }
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // Reward Level Keys
        // ========================================

        private bool ProcessRewardKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateRewards(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateRewards(-1);
                    return true;

                case KeyCode.Home:
                    if (_rewards.Count > 0)
                    {
                        _currentRewardIndex = 0;
                        AnnounceReward();
                    }
                    return true;

                case KeyCode.End:
                    if (_rewards.Count > 0)
                    {
                        _currentRewardIndex = _rewards.Count - 1;
                        AnnounceReward();
                    }
                    return true;

                case KeyCode.LeftArrow:
                case KeyCode.Escape:
                    _level = Level.Items;
                    _rewards.Clear();
                    _search.Clear();
                    AnnounceItem();
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        HandleSearchKey(keyCode);
                        return true;
                    }
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // Navigation
        // ========================================

        private void NavigateSections(int direction)
        {
            if (_sections == null || _sections.Length == 0) return;

            _currentSectionIndex = NavigationUtils.WrapIndex(_currentSectionIndex, direction, _sections.Length);
            AnnounceSection();
        }

        private void NavigateItems(int direction)
        {
            if (_currentItems.Count == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, _currentItems.Count);
            AnnounceItem();
        }

        private void NavigateRewards(int direction)
        {
            if (_rewards.Count == 0) return;

            _currentRewardIndex = NavigationUtils.WrapIndex(_currentRewardIndex, direction, _rewards.Count);
            AnnounceReward();
        }

        private void EnterItems()
        {
            if (_currentSectionIndex < 0 || _currentSectionIndex >= _sections.Length) return;

            var sectionType = _sections[_currentSectionIndex];

            switch (sectionType)
            {
                case SectionType.PickOptions:
                    _currentItems = IronmanReflection.GetCurrentPickOptions();
                    break;
                case SectionType.CoreUpgrades:
                    _currentItems = IronmanReflection.GetCoreUpgrades();
                    break;
                case SectionType.Unlocked:
                    _currentItems = IronmanReflection.GetUnlockedUpgrades();
                    break;
            }

            if (_currentItems.Count == 0)
            {
                Speech.Say("No upgrades");
                return;
            }

            _level = Level.Items;
            _currentItemIndex = 0;
            _search.Clear();
            AnnounceItem();
        }

        private void EnterRewards()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _currentItems.Count) return;

            var upgrade = _currentItems[_currentItemIndex];
            _rewards = IronmanReflection.GetRewards(upgrade.UpgradeObj);

            if (_rewards.Count == 0)
            {
                Speech.Say("No rewards");
                return;
            }

            _level = Level.Rewards;
            _currentRewardIndex = 0;
            _search.Clear();
            AnnounceReward();
        }

        private void ActivateItem()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _currentItems.Count) return;

            var upgrade = _currentItems[_currentItemIndex];

            if (upgrade.IsUnlocked)
            {
                Speech.Say("Already unlocked");
                return;
            }

            if (!upgrade.CanAfford)
            {
                SoundManager.PlayFailed();
                Speech.Say("Can't afford");
                return;
            }

            if (IronmanReflection.Pick(upgrade.UpgradeObj))
            {
                SoundManager.PlayCapitalUpgradeBought();
                Speech.Say($"Purchased {upgrade.Name}");

                // Refresh data after purchase
                RefreshAfterPurchase();
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Purchase failed");
            }
        }

        private void RefreshAfterPurchase()
        {
            // Remember if we were in Random Upgrades section
            bool wasInRandomUpgrades = _level == Level.Items &&
                _currentSectionIndex < _sections.Length &&
                _sections[_currentSectionIndex] == SectionType.PickOptions;

            // Rebuild sections (pick options might be gone if max reached)
            BuildSections();

            // If we're still in items level, refresh the current section
            if (_level == Level.Items && _currentSectionIndex < _sections.Length)
            {
                var sectionType = _sections[_currentSectionIndex];

                switch (sectionType)
                {
                    case SectionType.PickOptions:
                        _currentItems = IronmanReflection.GetCurrentPickOptions();
                        // Reset to first item since these are completely new options
                        _currentItemIndex = 0;
                        break;
                    case SectionType.CoreUpgrades:
                        _currentItems = IronmanReflection.GetCoreUpgrades();
                        // Clamp index if needed
                        if (_currentItemIndex >= _currentItems.Count)
                            _currentItemIndex = Mathf.Max(0, _currentItems.Count - 1);
                        break;
                    case SectionType.Unlocked:
                        _currentItems = IronmanReflection.GetUnlockedUpgrades();
                        // Clamp index if needed
                        if (_currentItemIndex >= _currentItems.Count)
                            _currentItemIndex = Mathf.Max(0, _currentItems.Count - 1);
                        break;
                }

                // If no items left in section, go back to sections
                if (_currentItems.Count == 0)
                {
                    _level = Level.Sections;
                    if (_currentSectionIndex >= _sections.Length)
                        _currentSectionIndex = Mathf.Max(0, _sections.Length - 1);
                    AnnounceSection();
                }
                else if (wasInRandomUpgrades && sectionType == SectionType.PickOptions)
                {
                    // Announce new pick status and first option
                    int completed = IronmanReflection.GetCompletedPicks();
                    int max = IronmanReflection.GetMaxPicks();
                    Speech.Say($"Pick {completed + 1} of {max}");
                    AnnounceItem();
                }
            }
            else if (_currentSectionIndex >= _sections.Length)
            {
                // Section was removed (pick options gone), adjust index
                _currentSectionIndex = Mathf.Max(0, _sections.Length - 1);
                _level = Level.Sections;
                // Announce that all picks are complete
                int max = IronmanReflection.GetMaxPicks();
                Speech.Say($"All {max} picks complete");
                AnnounceSection();
            }
        }

        // ========================================
        // Announcements
        // ========================================

        private void AnnounceOpen()
        {
            int completed = IronmanReflection.GetCompletedPicks();
            int max = IronmanReflection.GetMaxPicks();

            if (IronmanReflection.HasReachedMaxPicks())
            {
                Speech.Say($"Ironman Upgrades. All {max} picks complete");
            }
            else
            {
                Speech.Say($"Ironman Upgrades. Pick {completed + 1} of {max}");
            }

            AnnounceSection();
        }

        private void AnnounceCurrentLevel()
        {
            switch (_level)
            {
                case Level.Sections:
                    AnnounceSection();
                    break;
                case Level.Items:
                    AnnounceItem();
                    break;
                case Level.Rewards:
                    AnnounceReward();
                    break;
            }
        }

        private void AnnounceSection()
        {
            if (_currentSectionIndex < 0 || _currentSectionIndex >= _sectionNames.Length) return;

            string name = _sectionNames[_currentSectionIndex];
            var sectionType = _sections[_currentSectionIndex];

            int count = 0;
            switch (sectionType)
            {
                case SectionType.PickOptions:
                    count = IronmanReflection.GetCurrentPickOptions().Count;
                    break;
                case SectionType.CoreUpgrades:
                    count = IronmanReflection.GetCoreUpgrades().Count;
                    break;
                case SectionType.Unlocked:
                    count = IronmanReflection.GetUnlockedUpgrades().Count;
                    break;
            }

            Speech.Say($"{name}, {count} upgrades");
        }

        private void AnnounceItem()
        {
            if (_currentItemIndex < 0 || _currentItemIndex >= _currentItems.Count) return;

            var upgrade = _currentItems[_currentItemIndex];

            if (upgrade.IsUnlocked)
            {
                Speech.Say($"{upgrade.Name}, unlocked");
            }
            else if (upgrade.CanAfford)
            {
                Speech.Say($"{upgrade.Name}, {upgrade.PriceText}");
            }
            else
            {
                Speech.Say($"{upgrade.Name}, {upgrade.PriceText}, can't afford");
            }
        }

        private void AnnounceReward()
        {
            if (_currentRewardIndex < 0 || _currentRewardIndex >= _rewards.Count) return;

            var reward = _rewards[_currentRewardIndex];

            if (!string.IsNullOrEmpty(reward.Description))
                Speech.Say($"{reward.Name}, {reward.Description}");
            else
                Speech.Say(reward.Name);
        }

        // ========================================
        // Type-Ahead Search
        // ========================================

        private void HandleSearchKey(KeyCode keyCode)
        {
            char c = (char)('a' + (keyCode - KeyCode.A));
            _search.AddChar(c);

            int match = FindMatch();
            if (match >= 0)
            {
                SetCurrentIndex(match);
                AnnounceCurrentLevel();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindMatch();
            if (match >= 0)
            {
                SetCurrentIndex(match);
                AnnounceCurrentLevel();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatch()
        {
            if (!_search.HasBuffer) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            switch (_level)
            {
                case Level.Sections:
                    for (int i = 0; i < _sectionNames.Length; i++)
                    {
                        if (_sectionNames[i].ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;

                case Level.Items:
                    for (int i = 0; i < _currentItems.Count; i++)
                    {
                        if (_currentItems[i].Name.ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;

                case Level.Rewards:
                    for (int i = 0; i < _rewards.Count; i++)
                    {
                        if (_rewards[i].Name.ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;
            }

            return -1;
        }

        private void SetCurrentIndex(int index)
        {
            switch (_level)
            {
                case Level.Sections:
                    _currentSectionIndex = index;
                    break;
                case Level.Items:
                    _currentItemIndex = index;
                    break;
                case Level.Rewards:
                    _currentRewardIndex = index;
                    break;
            }
        }
    }
}
