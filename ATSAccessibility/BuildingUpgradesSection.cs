using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Shared handler for building upgrades section.
    /// Any building navigator can use this to handle the Upgrades section.
    /// </summary>
    public class BuildingUpgradesSection
    {
        private object _building;
        private List<BuildingReflection.UpgradeLevelInfo> _levels = new List<BuildingReflection.UpgradeLevelInfo>();
        private int _nextAvailableIndex;  // Index in _levels of the next purchasable level (-1 if all done)
        private bool _initialized = false;

        // Track purchases locally to prevent duplicates (game state may have timing delays)
        private HashSet<int> _purchasedThisSession = new HashSet<int>();

        /// <summary>
        /// Initialize the section with a building.
        /// </summary>
        public void Initialize(object building)
        {
            // Only clear purchase tracking if this is a different building
            if (_building != building)
            {
                _purchasedThisSession.Clear();
            }

            _building = building;
            _levels.Clear();
            _nextAvailableIndex = -1;
            _initialized = false;

            if (building == null) return;
            if (!BuildingReflection.HasUpgradesAvailable(building)) return;

            _levels = BuildingReflection.GetUpgradeLevelsInfo(building);

            // Find the first non-achieved level (that's the next available one)
            // Also account for levels we purchased this session (game state may be delayed)
            for (int i = 0; i < _levels.Count; i++)
            {
                if (!_levels[i].isAchieved && !_purchasedThisSession.Contains(i))
                {
                    _nextAvailableIndex = i;
                    break;
                }
            }

            _initialized = true;
        }

        /// <summary>
        /// Clear cached data.
        /// </summary>
        public void Clear()
        {
            _building = null;
            _levels.Clear();
            _nextAvailableIndex = -1;
            _initialized = false;
            _purchasedThisSession.Clear();
        }

        /// <summary>
        /// Check if there are any upgrades to navigate.
        /// </summary>
        public bool HasUpgrades()
        {
            return _initialized && _levels.Count > 0;
        }

        /// <summary>
        /// Get the number of upgrade levels (items at Level 1 navigation).
        /// </summary>
        public int GetItemCount()
        {
            return _levels.Count;
        }

        /// <summary>
        /// Get the number of perks for a given level (sub-items at Level 2 navigation).
        /// </summary>
        public int GetSubItemCount(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
                return 0;

            return _levels[levelIndex].perks.Count;
        }

        /// <summary>
        /// Announce an upgrade level (item).
        /// Levels are sequential: must complete earlier levels before later ones.
        /// </summary>
        public void AnnounceItem(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
            {
                Speech.Say("Invalid upgrade level");
                return;
            }

            var level = _levels[levelIndex];

            if (IsLevelAchieved(levelIndex))
            {
                // Level already purchased - show which perk was chosen
                string chosenPerk = GetChosenPerkName(level);
                if (!string.IsNullOrEmpty(chosenPerk))
                {
                    Speech.Say($"{level.levelName}: Achieved, {chosenPerk}");
                }
                else
                {
                    Speech.Say($"{level.levelName}: Achieved");
                }
            }
            else if (levelIndex == _nextAvailableIndex)
            {
                // This is the next available level - show cost
                string costText = GetCostText(level);
                string affordText = level.canAfford ? "" : ", cannot afford";
                Speech.Say($"{level.levelName}: {costText}{affordText}");
            }
            else
            {
                // Level is locked - need to complete previous levels first
                Speech.Say($"{level.levelName}: Locked, complete previous level first");
            }
        }

        /// <summary>
        /// Announce a perk within an upgrade level (sub-item).
        /// </summary>
        public void AnnounceSubItem(int levelIndex, int perkIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
            {
                Speech.Say("Invalid upgrade level");
                return;
            }

            var level = _levels[levelIndex];

            if (perkIndex < 0 || perkIndex >= level.perks.Count)
            {
                Speech.Say("Invalid perk");
                return;
            }

            var perk = level.perks[perkIndex];

            string statusText;
            if (IsLevelAchieved(levelIndex))
            {
                // Level achieved - show if this perk was chosen or not
                statusText = perk.isChosen ? "Chosen" : "Not chosen";
            }
            else if (levelIndex == _nextAvailableIndex)
            {
                // This is the next available level - perks are available options
                statusText = "Available";
            }
            else
            {
                // Level is locked
                statusText = "Locked";
            }

            string announcement = $"{perk.displayName}: {statusText}";

            if (!string.IsNullOrEmpty(perk.description))
            {
                announcement += $". {perk.description}";
            }

            Speech.Say(announcement);
        }

        /// <summary>
        /// Perform action on a perk (Enter at sub-item level).
        /// Purchases the upgrade if conditions are met.
        /// </summary>
        public bool PerformSubItemAction(int levelIndex, int perkIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
                return false;

            var level = _levels[levelIndex];

            if (IsLevelAchieved(levelIndex))
            {
                Speech.Say("Upgrade already purchased");
                return false;
            }

            if (levelIndex != _nextAvailableIndex)
            {
                Speech.Say("Complete previous level first");
                return false;
            }

            if (!level.canAfford)
            {
                Speech.Say("Not enough resources");
                return false;
            }

            if (perkIndex < 0 || perkIndex >= level.perks.Count)
            {
                Speech.Say("Invalid perk");
                return false;
            }

            // Call the game's Upgrade() method via reflection
            if (BuildingReflection.PurchaseUpgrade(_building, level.levelIndex, perkIndex))
            {
                var perk = level.perks[perkIndex];
                Speech.Say($"Purchased {perk.displayName}");

                // Track locally to prevent duplicates (game state may be delayed)
                _purchasedThisSession.Add(levelIndex);

                // Refresh cached data to reflect the purchase
                Initialize(_building);
                return true;
            }
            else
            {
                Speech.Say("Purchase failed");
                return false;
            }
        }

        /// <summary>
        /// Get the searchable name for an upgrade level.
        /// </summary>
        public string GetItemName(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
                return null;

            return _levels[levelIndex].levelName;
        }

        /// <summary>
        /// Get the searchable name for a perk.
        /// </summary>
        public string GetSubItemName(int levelIndex, int perkIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
                return null;

            var level = _levels[levelIndex];
            if (perkIndex < 0 || perkIndex >= level.perks.Count)
                return null;

            return level.perks[perkIndex].displayName;
        }

        /// <summary>
        /// Check if a level is achieved (either from game state or purchased this session).
        /// </summary>
        private bool IsLevelAchieved(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= _levels.Count)
                return false;
            return _levels[levelIndex].isAchieved || _purchasedThisSession.Contains(levelIndex);
        }

        /// <summary>
        /// Get the name of the chosen perk for an achieved level.
        /// </summary>
        private string GetChosenPerkName(BuildingReflection.UpgradeLevelInfo level)
        {
            foreach (var perk in level.perks)
            {
                if (perk.isChosen)
                    return perk.displayName;
            }
            return null;
        }

        /// <summary>
        /// Format the cost text for an upgrade level.
        /// </summary>
        private string GetCostText(BuildingReflection.UpgradeLevelInfo level)
        {
            if (level.requiredGoods.Count == 0)
                return "Free";

            var parts = new List<string>();
            foreach (var cost in level.requiredGoods)
            {
                parts.Add($"{cost.required} {cost.displayName}");
            }
            return string.Join(", ", parts);
        }
    }
}
