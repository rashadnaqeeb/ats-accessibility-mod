using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech-only panel for accessible embark screen navigation.
    /// Top-level menu with sections: Mission Info, Caravans, Spend Embark Points, Difficulty, Embark.
    /// Each section uses two-panel navigation (categories/details) like StatsPanel.
    /// </summary>
    public class EmbarkPanel
    {
        /// <summary>
        /// Top-level menu sections.
        /// </summary>
        public enum EmbarkSection
        {
            TopMenu = 0,
            MissionInfo = 1,
            Caravans = 2,
            SpendPoints = 3,
            Difficulty = 4
        }

        /// <summary>
        /// Category in a section's left panel.
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public List<string> Details { get; set; } = new List<string>();
            public object Data { get; set; }  // Associated game object (caravan, bonus, etc.)
            public List<object> DataList { get; set; }  // For lists like caravans, bonuses
        }

        // Panel state
        private bool _isOpen = false;
        private object _currentField;  // WorldField object

        // Top menu
        private readonly string[] _topMenuItems = {
            "Mission Info",
            "Caravans",
            "Spend Embark Points",
            "Difficulty",
            "Embark"
        };
        private int _topMenuIndex = 0;

        // Current section
        private EmbarkSection _currentSection = EmbarkSection.TopMenu;

        // Section navigation (category/detail like StatsPanel)
        private List<Category> _categories = new List<Category>();
        private int _currentCategoryIndex = 0;
        private int _currentDetailIndex = 0;
        private bool _focusOnDetails = false;

        /// <summary>
        /// Whether the embark panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the embark panel when field preview is shown.
        /// </summary>
        public void Open(object worldField)
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            _currentField = worldField;
            _isOpen = true;
            _currentSection = EmbarkSection.TopMenu;
            _topMenuIndex = 0;
            _categories.Clear();
            _currentCategoryIndex = 0;
            _currentDetailIndex = 0;
            _focusOnDetails = false;

            Speech.Say("Embark screen");
            AnnounceTopMenu();

            Debug.Log("[ATSAccessibility] EmbarkPanel opened");
        }

        /// <summary>
        /// Close the embark panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _currentField = null;
            _categories.Clear();

            Speech.Say("Embark panel closed");
            Debug.Log("[ATSAccessibility] EmbarkPanel closed");
        }

        // ========================================
        // INPUT HANDLING
        // ========================================

        /// <summary>
        /// Process a key event for the embark panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateUp();
                    return true;

                case KeyCode.DownArrow:
                    NavigateDown();
                    return true;

                case KeyCode.LeftArrow:
                    NavigateLeft();
                    return true;

                case KeyCode.RightArrow:
                    NavigateRight();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Activate();
                    return true;

                case KeyCode.Escape:
                    HandleEscape();
                    return true;

                default:
                    // Consume all other keys while panel is open
                    return true;
            }
        }

        private void NavigateUp()
        {
            if (_currentSection == EmbarkSection.TopMenu)
            {
                _topMenuIndex--;
                if (_topMenuIndex < 0) _topMenuIndex = _topMenuItems.Length - 1;
                AnnounceTopMenu();
            }
            else if (_currentSection == EmbarkSection.SpendPoints)
            {
                // Up/Down navigates items within current panel
                NavigateSpendPointsItem(-1);
            }
            else if (_focusOnDetails)
            {
                NavigateDetail(-1);
            }
            else
            {
                NavigateCategory(-1);
            }
        }

        private void NavigateDown()
        {
            if (_currentSection == EmbarkSection.TopMenu)
            {
                _topMenuIndex++;
                if (_topMenuIndex >= _topMenuItems.Length) _topMenuIndex = 0;
                AnnounceTopMenu();
            }
            else if (_currentSection == EmbarkSection.SpendPoints)
            {
                // Up/Down navigates items within current panel
                NavigateSpendPointsItem(1);
            }
            else if (_focusOnDetails)
            {
                NavigateDetail(1);
            }
            else
            {
                NavigateCategory(1);
            }
        }

        private void NavigateLeft()
        {
            if (_currentSection == EmbarkSection.TopMenu) return;

            if (_currentSection == EmbarkSection.SpendPoints)
            {
                // Left/Right navigates between panels
                NavigateSpendPointsPanel(-1);
            }
            else if (_focusOnDetails)
            {
                // Return to categories
                _focusOnDetails = false;
                AnnounceCurrentCategory();
            }
        }

        private void NavigateRight()
        {
            if (_currentSection == EmbarkSection.TopMenu) return;

            if (_currentSection == EmbarkSection.SpendPoints)
            {
                // Left/Right navigates between panels
                NavigateSpendPointsPanel(1);
            }
            else if (!_focusOnDetails && _categories.Count > 0)
            {
                var category = _categories[_currentCategoryIndex];
                if (category.Details.Count > 0)
                {
                    _focusOnDetails = true;
                    _currentDetailIndex = 0;
                    AnnounceCurrentDetail();
                }
                else
                {
                    Speech.Say("No details");
                }
            }
        }

        private void NavigateSpendPointsPanel(int direction)
        {
            if (_categories.Count == 0) return;

            _currentCategoryIndex += direction;
            if (_currentCategoryIndex < 0) _currentCategoryIndex = _categories.Count - 1;
            if (_currentCategoryIndex >= _categories.Count) _currentCategoryIndex = 0;

            _currentDetailIndex = 0;
            AnnounceSpendPointsPanel();
        }

        private void NavigateSpendPointsItem(int direction)
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0)
            {
                Speech.Say("No items in this panel");
                return;
            }

            _currentDetailIndex += direction;
            if (_currentDetailIndex < 0) _currentDetailIndex = category.Details.Count - 1;
            if (_currentDetailIndex >= category.Details.Count) _currentDetailIndex = 0;

            AnnounceSpendPointsItem();
        }

        private void AnnounceSpendPointsPanel()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            string panelPosition = $"{_currentCategoryIndex + 1} of {_categories.Count}";
            string itemCount = category.Details.Count > 0
                ? $"{category.Details.Count} items"
                : "empty";

            Speech.Say($"{category.Name}, {category.Value}, {itemCount}, panel {panelPosition}");
        }

        private void AnnounceSpendPointsItem()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0) return;

            string item = category.Details[_currentDetailIndex];
            string position = $"{_currentDetailIndex + 1} of {category.Details.Count}";

            Speech.Say($"{item}, {position}");
        }

        private void Activate()
        {
            if (_currentSection == EmbarkSection.TopMenu)
            {
                ActivateTopMenuItem();
            }
            else if (_currentSection == EmbarkSection.SpendPoints)
            {
                ActivateSpendPointsItem();
            }
            else if (_focusOnDetails)
            {
                ActivateDetail();
            }
            else
            {
                ActivateCategory();
            }
        }

        private void ActivateSpendPointsItem()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0 || category.DataList == null || category.DataList.Count == 0)
            {
                Speech.Say("No item selected");
                return;
            }

            if (_currentDetailIndex >= category.DataList.Count)
            {
                Speech.Say("Invalid selection");
                return;
            }

            var item = category.DataList[_currentDetailIndex];
            ToggleBonus(category.Name, item);
        }

        private void HandleEscape()
        {
            if (_currentSection == EmbarkSection.TopMenu)
            {
                // Close the embark panel entirely
                Close();
            }
            else if (_focusOnDetails)
            {
                // Back to categories - block game from receiving Escape
                InputBlocker.BlockCancelOnce = true;
                _focusOnDetails = false;
                AnnounceCurrentCategory();
            }
            else
            {
                // Back to top menu - block game from receiving Escape
                InputBlocker.BlockCancelOnce = true;
                _currentSection = EmbarkSection.TopMenu;
                AnnounceTopMenu();
            }
        }

        // ========================================
        // TOP MENU
        // ========================================

        private void AnnounceTopMenu()
        {
            string item = _topMenuItems[_topMenuIndex];
            string position = $"{_topMenuIndex + 1} of {_topMenuItems.Length}";
            Speech.Say($"{item}. {position}");
        }

        private void ActivateTopMenuItem()
        {
            switch (_topMenuIndex)
            {
                case 0: // Mission Info
                    EnterSection(EmbarkSection.MissionInfo);
                    BuildMissionInfoCategories();
                    break;

                case 1: // Caravans
                    EnterSection(EmbarkSection.Caravans);
                    BuildCaravanCategories();
                    break;

                case 2: // Spend Embark Points
                    EnterSection(EmbarkSection.SpendPoints);
                    BuildSpendPointsCategories();
                    break;

                case 3: // Difficulty
                    EnterSection(EmbarkSection.Difficulty);
                    BuildDifficultyCategories();
                    break;

                case 4: // Embark
                    TriggerEmbark();
                    break;
            }
        }

        private void EnterSection(EmbarkSection section)
        {
            _currentSection = section;
            _categories.Clear();
            _currentCategoryIndex = 0;
            _currentDetailIndex = 0;
            _focusOnDetails = false;
        }

        // ========================================
        // CATEGORY NAVIGATION
        // ========================================

        private void NavigateCategory(int direction)
        {
            if (_categories.Count == 0) return;

            _currentCategoryIndex += direction;

            // Wrap around
            if (_currentCategoryIndex < 0)
                _currentCategoryIndex = _categories.Count - 1;
            else if (_currentCategoryIndex >= _categories.Count)
                _currentCategoryIndex = 0;

            _currentDetailIndex = 0;
            AnnounceCurrentCategory();
        }

        private void AnnounceCurrentCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count)
            {
                Speech.Say("No items");
                return;
            }

            var category = _categories[_currentCategoryIndex];
            string message = category.Name;

            if (!string.IsNullOrEmpty(category.Value))
                message += $", {category.Value}";

            if (category.Details.Count > 0)
            {
                message += ". Press right for details";
            }

            string position = $"{_currentCategoryIndex + 1} of {_categories.Count}";
            message += $". {position}";

            Speech.Say(message);
        }

        private void ActivateCategory()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];

            // Section-specific activation
            switch (_currentSection)
            {
                case EmbarkSection.Caravans:
                    // Select the caravan
                    if (category.Data != null)
                    {
                        SelectCaravan(category.Data);
                    }
                    else
                    {
                        Speech.Say("This caravan slot is locked");
                    }
                    break;

                case EmbarkSection.SpendPoints:
                    // Enter details to see/toggle bonuses
                    if (category.Details.Count > 0)
                    {
                        _focusOnDetails = true;
                        _currentDetailIndex = 0;
                        AnnounceCurrentDetail();
                    }
                    break;

                case EmbarkSection.Difficulty:
                    if (category.Data != null)
                    {
                        SelectDifficulty(category.Data);
                    }
                    break;

                default:
                    // Just enter details if available
                    if (category.Details.Count > 0)
                    {
                        _focusOnDetails = true;
                        _currentDetailIndex = 0;
                        AnnounceCurrentDetail();
                    }
                    break;
            }
        }

        // ========================================
        // DETAIL NAVIGATION
        // ========================================

        private void NavigateDetail(int direction)
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            if (category.Details.Count == 0) return;

            _currentDetailIndex += direction;

            // Wrap around
            if (_currentDetailIndex < 0)
                _currentDetailIndex = category.Details.Count - 1;
            else if (_currentDetailIndex >= category.Details.Count)
                _currentDetailIndex = 0;

            AnnounceCurrentDetail();
        }

        private void AnnounceCurrentDetail()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentDetailIndex < 0 || _currentDetailIndex >= category.Details.Count)
            {
                Speech.Say("No details");
                return;
            }

            string detail = category.Details[_currentDetailIndex];
            string position = $"{_currentDetailIndex + 1} of {category.Details.Count}";

            Speech.Say($"{detail}, {position}");
        }

        private void ActivateDetail()
        {
            if (_categories.Count == 0) return;

            var category = _categories[_currentCategoryIndex];
            if (category.DataList == null || _currentDetailIndex >= category.DataList.Count) return;

            var item = category.DataList[_currentDetailIndex];

            // Section-specific detail activation
            switch (_currentSection)
            {
                case EmbarkSection.SpendPoints:
                    ToggleBonus(category.Name, item);
                    break;

                default:
                    // Re-read the detail
                    AnnounceCurrentDetail();
                    break;
            }
        }

        // ========================================
        // MISSION INFO SECTION
        // ========================================

        private void BuildMissionInfoCategories()
        {
            _categories.Clear();

            // Get field info from WorldMapReflection
            var fieldPos = GetFieldPosition();

            // Get currently selected difficulty (use min difficulty as fallback)
            var currentDifficulty = EmbarkReflection.GetCurrentDifficulty();

            // Biome - enhanced with description and resource nodes
            var biomeName = WorldMapReflection.WorldMapGetBiomeName(fieldPos);
            var biomeDescription = WorldMapReflection.WorldMapGetBiomeDescription(fieldPos);
            var biomeDeposits = WorldMapReflection.WorldMapGetBiomeDepositsGoods(fieldPos) ?? new List<string>();
            var biomeTrees = WorldMapReflection.WorldMapGetBiomeTreesGoods(fieldPos) ?? new List<string>();

            var biomeDetails = new List<string>();

            // Add description first (strip trailing period since AnnounceCurrentDetail adds one)
            if (!string.IsNullOrEmpty(biomeDescription))
                biomeDetails.Add(biomeDescription.TrimEnd('.'));

            // Add resource nodes
            if (biomeDeposits.Count > 0)
                biomeDetails.Add($"Deposits: {string.Join(", ", biomeDeposits)}");

            if (biomeTrees.Count > 0)
                biomeDetails.Add($"Resources from trees: {string.Join(", ", biomeTrees)}");

            _categories.Add(new Category
            {
                Name = "Biome",
                Value = biomeName ?? "Unknown",
                Details = biomeDetails
            });

            // Difficulty - show selected difficulty with minimum as detail
            var minDifficulty = WorldMapReflection.WorldMapGetMinDifficultyName(fieldPos);
            var selectedDiffName = currentDifficulty != null
                ? EmbarkReflection.GetDifficultyDisplayName(currentDifficulty)
                : minDifficulty;

            var difficultyDetails = new List<string>();
            if (!string.IsNullOrEmpty(minDifficulty) && minDifficulty != selectedDiffName)
            {
                difficultyDetails.Add($"Minimum for this field: {minDifficulty}");
            }

            _categories.Add(new Category
            {
                Name = "Selected Difficulty",
                Value = selectedDiffName ?? "Unknown",
                Details = difficultyDetails
            });

            // Modifiers
            var effects = WorldMapReflection.WorldMapGetFieldEffectsWithDescriptions(fieldPos);
            var modifierDetails = effects?.Select(e =>
                string.IsNullOrEmpty(e.description) ? e.name : $"{e.name}: {e.description}"
            ).ToList() ?? new List<string>();
            _categories.Add(new Category
            {
                Name = "Modifiers",
                Value = $"{modifierDetails.Count} modifiers",
                Details = modifierDetails
            });

            // Seal fragments - use selected difficulty
            int fragments = currentDifficulty != null
                ? EmbarkReflection.GetDifficultySealFragments(currentDifficulty)
                : WorldMapReflection.WorldMapGetSealFragmentsForWin(fieldPos);
            if (fragments > 0)
            {
                _categories.Add(new Category
                {
                    Name = "Seal Fragments Awarded",
                    Value = fragments.ToString()
                });
            }

            // Rewards - use selected difficulty
            var rewardDetails = currentDifficulty != null
                ? EmbarkReflection.GetMetaCurrenciesForDifficulty(fieldPos, currentDifficulty)
                : WorldMapReflection.WorldMapGetMetaCurrencies(fieldPos)?.ToList() ?? new List<string>();
            if (rewardDetails.Count > 0)
            {
                _categories.Add(new Category
                {
                    Name = "Rewards",
                    Value = $"{rewardDetails.Count} rewards",
                    Details = rewardDetails
                });
            }

            // Embark points - use selected difficulty's penalty
            int rawBasePoints = EmbarkReflection.GetBasePreparationPoints();
            int difficultyPenalty = currentDifficulty != null
                ? EmbarkReflection.GetDifficultyPreparationPenalty(currentDifficulty)
                : WorldMapReflection.WorldMapGetDifficultyPreparationPenalty(fieldPos);
            int basePoints = Math.Max(0, rawBasePoints + difficultyPenalty);
            int bonusPoints = EmbarkReflection.GetBonusPreparationPoints();
            _categories.Add(new Category
            {
                Name = "Embark Points",
                Value = $"{basePoints} base, {bonusPoints} bonus"
            });

            if (_categories.Count > 0)
            {
                AnnounceCurrentCategory();
            }
            else
            {
                Speech.Say("No mission info available");
            }
        }

        // ========================================
        // CARAVANS SECTION
        // ========================================

        private void BuildCaravanCategories()
        {
            _categories.Clear();

            var caravans = EmbarkReflection.GetCaravans();
            int pickedIndex = EmbarkReflection.GetPickedCaravanIndex();
            int totalSlots = 3; // Game always has 3 caravan slots

            for (int i = 0; i < totalSlots; i++)
            {
                if (i < caravans.Count)
                {
                    // Unlocked caravan
                    var caravan = caravans[i];
                    bool isSelected = (i == pickedIndex);

                    var details = BuildCaravanDetails(caravan);
                    string displayStr = EmbarkReflection.GetCaravanDisplayString(caravan, i);
                    string selectedMarker = isSelected ? "Selected, " : "";

                    _categories.Add(new Category
                    {
                        Name = $"{selectedMarker}Caravan {i + 1}",
                        Value = displayStr,
                        Details = details,
                        Data = caravan
                    });
                }
                else
                {
                    // Locked slot
                    _categories.Add(new Category
                    {
                        Name = $"Caravan {i + 1}",
                        Value = "Slot locked",
                        Details = new List<string>(),
                        Data = null
                    });
                }
            }

            // Start at the selected caravan
            _currentCategoryIndex = Math.Max(0, pickedIndex);
            AnnounceCurrentCategory();
        }

        private List<string> BuildCaravanDetails(object caravan)
        {
            var details = new List<string>();

            // Species breakdown
            var villagers = EmbarkReflection.GetCaravanVillagers(caravan);
            var races = EmbarkReflection.GetCaravanRaces(caravan);
            int revealedCount = EmbarkReflection.GetCaravanRevealedCount(caravan);

            // Determine which races are revealed (first 'revealedCount' races in the list)
            var revealedRaces = new HashSet<string>();
            for (int i = 0; i < revealedCount && i < races.Count; i++)
            {
                revealedRaces.Add(races[i]);
            }

            var raceCounts = new Dictionary<string, int>();
            int unknownCount = 0;

            foreach (var villagerRace in villagers)
            {
                // Check if this villager's race is in the revealed set
                if (revealedRaces.Contains(villagerRace))
                {
                    if (!raceCounts.ContainsKey(villagerRace))
                        raceCounts[villagerRace] = 0;
                    raceCounts[villagerRace]++;
                }
                else
                {
                    unknownCount++;
                }
            }

            // Add species to details
            foreach (var kvp in raceCounts)
            {
                var displayName = EmbarkReflection.GetRaceDisplayName(kvp.Key);
                details.Add($"{kvp.Value} {displayName}");
            }
            if (unknownCount > 0)
            {
                details.Add($"{unknownCount} villagers, species hidden");
            }

            // Base goods
            var goods = EmbarkReflection.GetCaravanGoods(caravan);
            foreach (var (name, amount) in goods)
            {
                var displayName = EmbarkReflection.GetGoodDisplayName(name);
                details.Add($"{amount} {displayName}");
            }

            // Bonus goods
            var bonusGoods = EmbarkReflection.GetCaravanBonusGoods(caravan);
            foreach (var (name, amount) in bonusGoods)
            {
                var displayName = EmbarkReflection.GetGoodDisplayName(name);
                details.Add($"{amount} {displayName} (bonus)");
            }

            return details;
        }

        private void SelectCaravan(object caravan)
        {
            EmbarkReflection.SetPickedCaravan(caravan);

            // Rebuild to update selected state
            int prevIndex = _currentCategoryIndex;
            BuildCaravanCategories();
            _currentCategoryIndex = prevIndex;

            Speech.Say("Caravan selected");
        }

        // ========================================
        // SPEND POINTS SECTION
        // ========================================

        private void BuildSpendPointsCategories(bool announce = true)
        {
            _categories.Clear();

            // Panel 1: Available Effects
            var effectsAvailable = EmbarkReflection.GetEffectsAvailable();
            var effectDetails = new List<string>();
            var effectDataList = new List<object>();

            foreach (var effect in effectsAvailable)
            {
                string name = EmbarkReflection.GetConditionPickName(effect);
                string displayName = EmbarkReflection.GetEffectDisplayName(name);
                int cost = EmbarkReflection.GetConditionPickCost(effect);
                effectDetails.Add($"{displayName}, {cost} points");
                effectDataList.Add(effect);
            }

            _categories.Add(new Category
            {
                Name = "Available Effects",
                Value = effectDetails.Count > 0 ? $"{effectDetails.Count} available" : "None",
                Details = effectDetails,
                DataList = effectDataList
            });

            // Panel 2: Available Goods
            var goodsAvailable = EmbarkReflection.GetGoodsAvailable();
            var goodDetails = new List<string>();
            var goodDataList = new List<object>();

            foreach (var good in goodsAvailable)
            {
                string name = EmbarkReflection.GetGoodPickName(good);
                string displayName = EmbarkReflection.GetGoodDisplayName(name);
                int amount = EmbarkReflection.GetGoodPickAmount(good);
                int cost = EmbarkReflection.GetGoodPickCost(good);
                goodDetails.Add($"{amount} {displayName}, {cost} points");
                goodDataList.Add(good);
            }

            _categories.Add(new Category
            {
                Name = "Available Goods",
                Value = goodDetails.Count > 0 ? $"{goodDetails.Count} available" : "None",
                Details = goodDetails,
                DataList = goodDataList
            });

            // Panel 3: Spent - points summary + picked items
            // Calculate total with difficulty penalty applied
            int rawBase = EmbarkReflection.GetBasePreparationPoints();
            var currentDifficulty = EmbarkReflection.GetCurrentDifficulty();
            int difficultyPenalty = currentDifficulty != null
                ? EmbarkReflection.GetDifficultyPreparationPenalty(currentDifficulty)
                : WorldMapReflection.WorldMapGetDifficultyPreparationPenalty(GetFieldPosition());
            int total = Math.Max(0, rawBase + difficultyPenalty) + EmbarkReflection.GetBonusPreparationPoints();
            int used = EmbarkReflection.CalculatePointsUsed();

            var spentDetails = new List<string>();
            var spentDataList = new List<object>();

            // Add picked effects
            var effectsPicked = EmbarkReflection.GetEffectsPicked();
            foreach (var effect in effectsPicked)
            {
                string name = EmbarkReflection.GetConditionPickName(effect);
                string displayName = EmbarkReflection.GetEffectDisplayName(name);
                int cost = EmbarkReflection.GetConditionPickCost(effect);
                spentDetails.Add($"{displayName}, {cost} points");
                spentDataList.Add(effect);
            }

            // Add picked goods
            var goodsPicked = EmbarkReflection.GetGoodsPicked();
            foreach (var good in goodsPicked)
            {
                string name = EmbarkReflection.GetGoodPickName(good);
                string displayName = EmbarkReflection.GetGoodDisplayName(name);
                int amount = EmbarkReflection.GetGoodPickAmount(good);
                int cost = EmbarkReflection.GetGoodPickCost(good);
                spentDetails.Add($"{amount} {displayName}, {cost} points");
                spentDataList.Add(good);
            }

            _categories.Add(new Category
            {
                Name = "Spent",
                Value = $"{used} of {total} points",
                Details = spentDetails,
                DataList = spentDataList
            });

            if (announce)
            {
                AnnounceSpendPointsPanel();
            }
        }

        private void ToggleBonus(string categoryName, object item)
        {
            bool success;
            bool added;

            // Determine type from the item itself (ConditionPickState vs GoodPickState)
            string typeName = item?.GetType().Name ?? "";
            if (typeName.Contains("Condition") || categoryName.Contains("Effect"))
            {
                (success, added) = EmbarkReflection.ToggleEffectBonus(item);
            }
            else if (typeName.Contains("Good") || categoryName.Contains("Good"))
            {
                (success, added) = EmbarkReflection.ToggleGoodBonus(item);
            }
            else
            {
                Speech.Say("Cannot toggle this item");
                return;
            }

            if (success)
            {
                string action = added ? "Added" : "Removed";
                int remaining = EmbarkReflection.CalculatePointsRemaining();
                Speech.Say($"{action}. {remaining} points remaining");

                // Rebuild the section, preserving position (don't announce since we already gave feedback)
                int prevCategoryIndex = _currentCategoryIndex;
                int prevDetailIndex = _currentDetailIndex;
                BuildSpendPointsCategories(announce: false);

                // Restore position
                if (_categories.Count > 0)
                {
                    _currentCategoryIndex = Math.Min(prevCategoryIndex, _categories.Count - 1);
                    var category = _categories[_currentCategoryIndex];
                    _currentDetailIndex = Math.Min(prevDetailIndex, Math.Max(0, category.Details.Count - 1));
                }
            }
            else
            {
                int cost = 0;
                if (categoryName.Contains("Effect"))
                    cost = EmbarkReflection.GetConditionPickCost(item);
                else if (categoryName.Contains("Good"))
                    cost = EmbarkReflection.GetGoodPickCost(item);

                int remaining = EmbarkReflection.CalculatePointsRemaining();
                Speech.Say($"Cannot afford. Need {cost} points, only {remaining} remaining");
            }
        }

        // ========================================
        // DIFFICULTY SECTION
        // ========================================

        private void BuildDifficultyCategories()
        {
            _categories.Clear();

            var fieldPos = GetFieldPosition();
            var difficulties = EmbarkReflection.GetAvailableDifficulties(fieldPos);
            var currentDifficulty = EmbarkReflection.GetCurrentDifficulty();
            int currentIndex = -1;

            for (int i = 0; i < difficulties.Count; i++)
            {
                var diff = difficulties[i];
                var name = EmbarkReflection.GetDifficultyDisplayName(diff);
                bool isSelected = IsSameDifficulty(diff, currentDifficulty);
                if (isSelected) currentIndex = i;

                // Check if unlocked
                bool isUnlocked = EmbarkReflection.IsDifficultyUnlocked(diff);

                // Build details: modifiers, penalty, rewards
                var details = new List<string>();

                var modifiers = EmbarkReflection.GetDifficultyModifiers(diff, fieldPos);
                foreach (var modDesc in modifiers)
                {
                    if (!string.IsNullOrEmpty(modDesc))
                    {
                        details.Add(modDesc);
                    }
                }

                int penalty = EmbarkReflection.GetDifficultyPreparationPenalty(diff);
                if (penalty != 0)
                    details.Add($"Preparation points: {penalty}");

                float rewardsMult = EmbarkReflection.GetDifficultyRewardsMultiplier(diff);
                if (rewardsMult > 0)
                    details.Add($"Rewards multiplier: {rewardsMult:P0}");

                string selectedMarker = isSelected ? "Selected, " : "";
                string lockedMarker = !isUnlocked ? " (Locked)" : "";

                _categories.Add(new Category
                {
                    Name = $"{selectedMarker}{name}{lockedMarker}",
                    Value = "",  // Details will speak for themselves
                    Details = details,
                    Data = diff
                });
            }

            // Start at current difficulty
            _currentCategoryIndex = Math.Max(0, currentIndex);

            if (_categories.Count > 0)
            {
                AnnounceCurrentCategory();
            }
            else
            {
                Speech.Say("No difficulties available");
            }
        }

        private bool IsSameDifficulty(object diff1, object diff2)
        {
            if (diff1 == null || diff2 == null) return false;
            return EmbarkReflection.GetDifficultyIndex(diff1) == EmbarkReflection.GetDifficultyIndex(diff2);
        }

        private void SelectDifficulty(object difficulty)
        {
            // Check if locked
            if (!EmbarkReflection.IsDifficultyUnlocked(difficulty))
            {
                Speech.Say("This difficulty is locked");
                return;
            }

            bool success = EmbarkReflection.SetDifficulty(difficulty);

            if (success)
            {
                // Rebuild to update selected state
                int prevIndex = _currentCategoryIndex;
                BuildDifficultyCategories();
                _currentCategoryIndex = prevIndex;

                var name = EmbarkReflection.GetDifficultyDisplayName(difficulty);
                Speech.Say($"{name} selected");
            }
            else
            {
                Speech.Say("Cannot select this difficulty");
            }
        }

        // ========================================
        // EMBARK ACTION
        // ========================================

        private void TriggerEmbark()
        {
            // Check if caravan is selected
            var picked = EmbarkReflection.GetPickedCaravan();
            if (picked == null)
            {
                Speech.Say("Please select a caravan first");
                return;
            }

            // Check if points are overspent
            int remaining = EmbarkReflection.CalculatePointsRemaining();
            if (remaining < 0)
            {
                Speech.Say($"Cannot embark. Over budget by {-remaining} points");
                return;
            }

            // Close our panel first
            Close();

            // Trigger the game's embark flow
            Speech.Say("Embarking");
            bool success = EmbarkReflection.TriggerEmbark();

            if (!success)
            {
                Speech.Say("Embark failed. Please use the game's embark button.");
            }
        }

        // ========================================
        // HELPERS
        // ========================================

        private Vector3Int GetFieldPosition()
        {
            if (_currentField == null) return Vector3Int.zero;

            try
            {
                // Get CubicPos from WorldField
                var cubicPosProp = _currentField.GetType().GetProperty("CubicPos",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (cubicPosProp != null)
                {
                    return (Vector3Int)cubicPosProp.GetValue(_currentField);
                }
            }
            catch
            {
                // Fallback
            }

            return Vector3Int.zero;
        }
    }
}
