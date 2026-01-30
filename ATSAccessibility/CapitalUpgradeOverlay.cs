using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the CapitalUpgradePopup (Buy Upgrades screen).
    /// Three-level navigation: structures -> upgrades -> rewards.
    /// </summary>
    public class CapitalUpgradeOverlay : IKeyHandler
    {
        private static readonly Regex NumberPattern = new Regex(@"([+-]\d+)(%?)", RegexOptions.Compiled);

        private enum Level { Structures, Upgrades, Rewards }

        // State
        private bool _isOpen;
        private bool _suspended;
        private Level _level;
        private int _currentStructureIndex;
        private int _currentUpgradeIndex;
        private int _currentRewardIndex;
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Data
        private List<CapitalUpgradeReflection.StructureInfo> _structures = new List<CapitalUpgradeReflection.StructureInfo>();
        private List<CapitalUpgradeReflection.UpgradeInfo> _upgrades = new List<CapitalUpgradeReflection.UpgradeInfo>();
        private List<CapitalUpgradeReflection.RewardInfo> _rewards = new List<CapitalUpgradeReflection.RewardInfo>();

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
                case Level.Structures:
                    return ProcessStructureKey(keyCode);
                case Level.Upgrades:
                    return ProcessUpgradeKey(keyCode);
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
            _structures = CapitalUpgradeReflection.GetStructures();

            if (_structures.Count == 0)
            {
                Debug.LogWarning("[ATSAccessibility] Capital upgrade overlay: no structures available");
                return;
            }

            _isOpen = true;
            _suspended = false;
            _level = Level.Structures;
            _currentStructureIndex = 0;
            _currentUpgradeIndex = 0;
            _currentRewardIndex = 0;
            _upgrades.Clear();
            _rewards.Clear();
            _search.Clear();
            AnnounceStructure();
            Debug.Log("[ATSAccessibility] Capital upgrade overlay opened");
        }

        public void Close()
        {
            _isOpen = false;
            _suspended = false;
            _structures.Clear();
            _upgrades.Clear();
            _rewards.Clear();
            _currentStructureIndex = 0;
            _currentUpgradeIndex = 0;
            _currentRewardIndex = 0;
            _search.Clear();
            Debug.Log("[ATSAccessibility] Capital upgrade overlay closed");
        }

        public void Resume()
        {
            if (!_isOpen) return;

            _suspended = false;
            AnnounceCurrentLevel();
            Debug.Log("[ATSAccessibility] Capital upgrade overlay resumed");
        }

        // ========================================
        // Structure Level Keys
        // ========================================

        private bool ProcessStructureKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateStructures(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateStructures(-1);
                    return true;

                case KeyCode.Home:
                    if (_structures.Count > 0) { _currentStructureIndex = 0; AnnounceStructure(); }
                    return true;

                case KeyCode.End:
                    if (_structures.Count > 0) { _currentStructureIndex = _structures.Count - 1; AnnounceStructure(); }
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    EnterUpgrades();
                    return true;

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
        // Upgrade Level Keys
        // ========================================

        private bool ProcessUpgradeKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateUpgrades(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateUpgrades(-1);
                    return true;

                case KeyCode.Home:
                    if (_upgrades.Count > 0) { _currentUpgradeIndex = 0; AnnounceUpgrade(); }
                    return true;

                case KeyCode.End:
                    if (_upgrades.Count > 0) { _currentUpgradeIndex = _upgrades.Count - 1; AnnounceUpgrade(); }
                    return true;

                case KeyCode.RightArrow:
                    EnterRewards();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateUpgrade();
                    return true;

                case KeyCode.LeftArrow:
                    _level = Level.Structures;
                    _upgrades.Clear();
                    _search.Clear();
                    AnnounceStructure();
                    return true;

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
                    if (_rewards.Count > 0) { _currentRewardIndex = 0; AnnounceReward(); }
                    return true;

                case KeyCode.End:
                    if (_rewards.Count > 0) { _currentRewardIndex = _rewards.Count - 1; AnnounceReward(); }
                    return true;

                case KeyCode.LeftArrow:
                    _level = Level.Upgrades;
                    _rewards.Clear();
                    _search.Clear();
                    AnnounceUpgrade();
                    return true;

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
        // Navigation
        // ========================================

        private void NavigateStructures(int direction)
        {
            if (_structures.Count == 0) return;

            _currentStructureIndex = NavigationUtils.WrapIndex(_currentStructureIndex, direction, _structures.Count);
            AnnounceStructure();
        }

        private void NavigateUpgrades(int direction)
        {
            if (_upgrades.Count == 0) return;

            _currentUpgradeIndex = NavigationUtils.WrapIndex(_currentUpgradeIndex, direction, _upgrades.Count);
            AnnounceUpgrade();
        }

        private void NavigateRewards(int direction)
        {
            if (_rewards.Count == 0) return;

            _currentRewardIndex = NavigationUtils.WrapIndex(_currentRewardIndex, direction, _rewards.Count);
            AnnounceReward();
        }

        private void EnterUpgrades()
        {
            if (_currentStructureIndex < 0 || _currentStructureIndex >= _structures.Count) return;

            var structure = _structures[_currentStructureIndex];
            _upgrades = CapitalUpgradeReflection.GetUpgrades(structure.StructureObj);

            if (_upgrades.Count == 0)
            {
                Speech.Say("No upgrades");
                return;
            }

            _level = Level.Upgrades;
            _currentUpgradeIndex = 0;
            _search.Clear();
            AnnounceUpgrade();
        }

        private void EnterRewards()
        {
            if (_currentUpgradeIndex < 0 || _currentUpgradeIndex >= _upgrades.Count) return;

            var upgrade = _upgrades[_currentUpgradeIndex];
            _rewards = CapitalUpgradeReflection.GetRewards(upgrade.UpgradeObj);

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

        private void ActivateUpgrade()
        {
            if (_currentUpgradeIndex < 0 || _currentUpgradeIndex >= _upgrades.Count) return;

            var upgrade = _upgrades[_currentUpgradeIndex];

            switch (upgrade.Status)
            {
                case CapitalUpgradeReflection.UpgradeStatus.Buyable:
                    if (CapitalUpgradeReflection.BuyUpgrade(upgrade.UpgradeObj))
                    {
                        SoundManager.PlayCapitalUpgradeBought();
                        Speech.Say($"Purchased {upgrade.Name}");
                        // Refresh upgrades list to reflect new state
                        RefreshCurrentUpgrades();
                    }
                    else
                    {
                        SoundManager.PlayFailed();
                        Speech.Say("Purchase failed");
                    }
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.TooExpensive:
                    SoundManager.PlayFailed();
                    Speech.Say("Can't afford");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.LevelRequired:
                    SoundManager.PlayFailed();
                    Speech.Say($"Requires player level {upgrade.RequiredLevel}");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.Locked:
                    SoundManager.PlayFailed();
                    Speech.Say("Previous upgrades required");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.Unlocked:
                    // Already unlocked, no action
                    break;
            }
        }

        private void RefreshCurrentUpgrades()
        {
            if (_currentStructureIndex < 0 || _currentStructureIndex >= _structures.Count) return;

            // Refresh structures to update unlocked counts
            _structures = CapitalUpgradeReflection.GetStructures();

            if (_currentStructureIndex >= _structures.Count) return;

            var structure = _structures[_currentStructureIndex];
            _upgrades = CapitalUpgradeReflection.GetUpgrades(structure.StructureObj);

            // Clamp index if needed
            if (_currentUpgradeIndex >= _upgrades.Count)
                _currentUpgradeIndex = _upgrades.Count - 1;
        }

        // ========================================
        // Announcements
        // ========================================

        private void AnnounceCurrentLevel()
        {
            switch (_level)
            {
                case Level.Structures:
                    AnnounceStructure();
                    break;
                case Level.Upgrades:
                    AnnounceUpgrade();
                    break;
                case Level.Rewards:
                    AnnounceReward();
                    break;
            }
        }

        private void AnnounceStructure()
        {
            if (_currentStructureIndex < 0 || _currentStructureIndex >= _structures.Count) return;

            var structure = _structures[_currentStructureIndex];
            Speech.Say($"{structure.Name}, {structure.UnlockedCount} of {structure.TotalUpgrades}");
        }

        private void AnnounceUpgrade()
        {
            if (_currentUpgradeIndex < 0 || _currentUpgradeIndex >= _upgrades.Count) return;

            var upgrade = _upgrades[_currentUpgradeIndex];
            int level = _currentUpgradeIndex + 1;

            switch (upgrade.Status)
            {
                case CapitalUpgradeReflection.UpgradeStatus.Unlocked:
                    Speech.Say($"{upgrade.Name}, level {level}, unlocked");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.Buyable:
                    Speech.Say($"{upgrade.Name}, level {level}, {upgrade.PriceText}");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.TooExpensive:
                    Speech.Say($"{upgrade.Name}, level {level}, {upgrade.PriceText}, can't afford");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.LevelRequired:
                    Speech.Say($"{upgrade.Name}, level {level}, requires player level {upgrade.RequiredLevel}");
                    break;

                case CapitalUpgradeReflection.UpgradeStatus.Locked:
                    Speech.Say($"{upgrade.Name}, level {level}, locked");
                    break;
            }
        }

        private void AnnounceReward()
        {
            if (_currentRewardIndex < 0 || _currentRewardIndex >= _rewards.Count) return;

            var reward = _rewards[_currentRewardIndex];
            int level = _currentUpgradeIndex + 1;
            string totalSuffix = _currentRewardIndex == 0 ? GetRewardTotalSuffix(reward.Description, level) : "";

            if (!string.IsNullOrEmpty(reward.Description))
                Speech.Say($"{reward.Name}, {reward.Description}{totalSuffix}");
            else
                Speech.Say($"{reward.Name}{totalSuffix}");
        }

        /// <summary>
        /// Compute a cumulative total suffix for a stacking reward.
        /// Extracts the per-level value (e.g., +3%) from the description and multiplies by level.
        /// </summary>
        private string GetRewardTotalSuffix(string description, int level)
        {
            if (level <= 1 || string.IsNullOrEmpty(description)) return "";

            var match = NumberPattern.Match(description);
            if (match.Success)
            {
                int value;
                if (int.TryParse(match.Groups[1].Value, out value))
                {
                    string unit = match.Groups[2].Value;
                    int total = value * level;
                    string sign = total >= 0 ? "+" : "";
                    return $" (total {sign}{total}{unit})";
                }
            }

            return $" ({level} stacks)";
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
                case Level.Structures:
                    for (int i = 0; i < _structures.Count; i++)
                    {
                        if (_structures[i].Name.ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;

                case Level.Upgrades:
                    for (int i = 0; i < _upgrades.Count; i++)
                    {
                        if (_upgrades[i].Name.ToLowerInvariant().StartsWith(lowerPrefix))
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
                case Level.Structures:
                    _currentStructureIndex = index;
                    break;
                case Level.Upgrades:
                    _currentUpgradeIndex = index;
                    break;
                case Level.Rewards:
                    _currentRewardIndex = index;
                    break;
            }
        }
    }
}
