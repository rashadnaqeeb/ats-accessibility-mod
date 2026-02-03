using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the CapitalUpgradePopup (Buy Upgrades screen).
    /// Three-level navigation: structures -> upgrades -> rewards.
    /// Pattern B at Level 1: Enter=buy (Action), Right=view rewards (CanDrillDown).
    /// </summary>
    public class CapitalUpgradeOverlay : MenuBase, IKeyHandler
    {
        private static readonly Regex NumberPattern = new Regex(@"([+-]\d+)(%?)", RegexOptions.Compiled);

        // Data
        private List<CapitalUpgradeReflection.StructureInfo> _structures = new List<CapitalUpgradeReflection.StructureInfo>();
        private List<CapitalUpgradeReflection.UpgradeInfo> _upgrades = new List<CapitalUpgradeReflection.UpgradeInfo>();
        private List<CapitalUpgradeReflection.RewardInfo> _rewards = new List<CapitalUpgradeReflection.RewardInfo>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen && !IsSuspended;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Capital Upgrades";
        protected override string EmptyMessage => "No structures available";

        protected override int GetItemCount()
        {
            switch (Level)
            {
                case 0: return _structures.Count;
                case 1: return _upgrades.Count;
                case 2: return _rewards.Count;
                default: return 0;
            }
        }

        protected override string GetLabel(int index)
        {
            switch (Level)
            {
                case 0:
                    return (index >= 0 && index < _structures.Count) ? _structures[index].Name : null;
                case 1:
                    return (index >= 0 && index < _upgrades.Count) ? _upgrades[index].Name : null;
                case 2:
                    return (index >= 0 && index < _rewards.Count) ? _rewards[index].Name : null;
                default:
                    return null;
            }
        }

        protected override void AnnounceCurrentItem()
        {
            switch (Level)
            {
                case 0:
                    AnnounceStructure();
                    break;
                case 1:
                    AnnounceUpgrade();
                    break;
                case 2:
                    AnnounceReward();
                    break;
            }
        }

        protected override void RefreshData()
        {
            _structures = CapitalUpgradeReflection.GetStructures();
        }

        protected override EnterAction OnEnter(int index)
        {
            switch (Level)
            {
                case 0:
                    if (index < 0 || index >= _structures.Count) return EnterAction.None;
                    var upgrades = CapitalUpgradeReflection.GetUpgrades(_structures[index].StructureObj);
                    if (upgrades.Count == 0)
                    {
                        Speech.Say("No upgrades");
                        return EnterAction.None;
                    }
                    _upgrades = upgrades;
                    return EnterAction.DrillDown;

                case 1:
                    return EnterAction.Action;

                default:
                    return EnterAction.None;
            }
        }

        protected override void OnAction(int index)
        {
            if (Level == 1)
                ActivateUpgrade();
        }

        protected override bool CanDrillDown(int index)
        {
            switch (Level)
            {
                case 0:
                    // Right arrow: load upgrades, check empty
                    if (index < 0 || index >= _structures.Count) return false;
                    var upgrades = CapitalUpgradeReflection.GetUpgrades(_structures[index].StructureObj);
                    if (upgrades.Count == 0)
                    {
                        Speech.Say("No upgrades");
                        return false;
                    }
                    _upgrades = upgrades;
                    return true;

                case 1:
                    // Right arrow: load rewards, check empty
                    if (index < 0 || index >= _upgrades.Count) return false;
                    var rewards = CapitalUpgradeReflection.GetRewards(_upgrades[index].UpgradeObj);
                    if (rewards.Count == 0)
                    {
                        Speech.Say("No rewards");
                        return false;
                    }
                    _rewards = rewards;
                    return true;

                default:
                    return false;
            }
        }

        protected override void OnDrillDown(int index)
        {
            // Data already loaded by OnEnter or CanDrillDown
        }

        protected override void OnGoBack()
        {
            if (Level == 2)
                _rewards.Clear();
            else if (Level == 1)
                _upgrades.Clear();
        }

        protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (keyCode == KeyCode.Escape)
            {
                Close();
                // Pass to game to close popup
                return false;
            }
            return null;
        }

        protected override string GetOpenAnnouncement()
        {
            // Custom announcement handled in OnOpened
            return null;
        }

        protected override void OnOpened()
        {
            AnnounceStructure();
        }

        protected override void OnClosed()
        {
            _structures.Clear();
            _upgrades.Clear();
            _rewards.Clear();
        }

        // ========================================
        // SEARCH
        // ========================================

        protected override string GetSearchName(int index)
        {
            switch (Level)
            {
                case 0:
                    return (index >= 0 && index < _structures.Count) ? _structures[index].Name : null;
                case 1:
                    return (index >= 0 && index < _upgrades.Count) ? _upgrades[index].Name : null;
                case 2:
                    return (index >= 0 && index < _rewards.Count) ? _rewards[index].Name : null;
                default:
                    return null;
            }
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceStructure()
        {
            if (_indices[0] < 0 || _indices[0] >= _structures.Count) return;

            var structure = _structures[_indices[0]];
            Speech.Say($"{structure.Name}, {structure.UnlockedCount} of {structure.TotalUpgrades}");
        }

        private void AnnounceUpgrade()
        {
            if (_indices[1] < 0 || _indices[1] >= _upgrades.Count) return;

            var upgrade = _upgrades[_indices[1]];
            int level = _indices[1] + 1;

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
            if (_indices[2] < 0 || _indices[2] >= _rewards.Count) return;

            var reward = _rewards[_indices[2]];
            int level = _indices[1] + 1;
            string totalSuffix = _indices[2] == 0 ? GetRewardTotalSuffix(reward.Description, level) : "";

            if (!string.IsNullOrEmpty(reward.Description))
                Speech.Say($"{reward.Name}, {reward.Description}{totalSuffix}");
            else
                Speech.Say($"{reward.Name}{totalSuffix}");
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void ActivateUpgrade()
        {
            if (_indices[1] < 0 || _indices[1] >= _upgrades.Count) return;

            var upgrade = _upgrades[_indices[1]];

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
            if (_indices[0] < 0 || _indices[0] >= _structures.Count) return;

            // Refresh structures to update unlocked counts
            _structures = CapitalUpgradeReflection.GetStructures();

            if (_indices[0] >= _structures.Count) return;

            var structure = _structures[_indices[0]];
            _upgrades = CapitalUpgradeReflection.GetUpgrades(structure.StructureObj);

            // Clamp index if needed
            if (_indices[1] >= _upgrades.Count)
                _indices[1] = _upgrades.Count - 1;
        }

        // ========================================
        // HELPERS
        // ========================================

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
    }
}
