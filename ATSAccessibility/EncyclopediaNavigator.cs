using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides keyboard navigation for the game's WikiPopup (encyclopedia).
    /// Supports 3-panel navigation: Categories, Articles, and Content.
    /// </summary>
    public class EncyclopediaNavigator : IKeyHandler
    {
        public enum WikiPanel { Categories = 0, Articles = 1, Content = 2 }

        private object _wikiPopup;
        private WikiPanel _currentPanel = WikiPanel.Categories;

        // Categories (WikiCategoryButton list)
        private List<object> _categoryButtons = new List<object>();
        private int _categoryIndex;

        // Articles (WikiSlot list from current panel)
        private List<object> _articleSlots = new List<object>();
        private int _articleIndex;

        // Content (text lines from preview)
        private List<string> _contentLines = new List<string>();
        private int _contentLineIndex;

        // Track which category panel is currently active
        private object _currentCategoryPanel;

        // Type-ahead search for article navigation
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => _wikiPopup != null;

        /// <summary>
        /// Process a key event for the encyclopedia (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!IsActive) return false;

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateElement(-1);
                    return true;
                case KeyCode.DownArrow:
                    NavigateElement(1);
                    return true;
                case KeyCode.Home:
                    NavigateElementToFirst();
                    return true;
                case KeyCode.End:
                    NavigateElementToLast();
                    return true;
                case KeyCode.LeftArrow:
                    NavigatePanel(-1);
                    return true;
                case KeyCode.RightArrow:
                    NavigatePanel(1);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ActivateCurrentElement();
                    return true;
                case KeyCode.Backspace:
                    HandleBackspace();
                    return true;
                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        ClearSearchBuffer();
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close encyclopedia
                    return false;
                default:
                    // Check for alphabetic keys (A-Z) for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while encyclopedia is open
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Called when a WikiPopup is shown.
        /// </summary>
        public void OnWikiPopupShown(object popup)
        {
            _wikiPopup = popup;
            _currentPanel = WikiPanel.Categories;
            _categoryIndex = 0;
            _articleIndex = 0;
            _contentLineIndex = 0;

            RebuildCategories();
            AnnounceCurrentPanel();
            AnnounceCurrentElement();
        }

        /// <summary>
        /// Called when the WikiPopup is hidden.
        /// </summary>
        public void OnWikiPopupHidden()
        {
            _wikiPopup = null;
            _categoryButtons.Clear();
            _articleSlots.Clear();
            _contentLines.Clear();
            _currentCategoryPanel = null;
            _search.Clear();
        }

        // ========================================
        // NAVIGATION
        // ========================================

        /// <summary>
        /// Navigate between panels (Left/Right).
        /// </summary>
        public void NavigatePanel(int direction)
        {
            if (!IsActive) return;

            int newPanel = (int)_currentPanel + direction;

            // Clamp to valid range
            if (newPanel < 0) newPanel = 0;
            if (newPanel > 2) newPanel = 2;

            if (newPanel != (int)_currentPanel)
            {
                // Clear search buffer when leaving Articles panel
                _search.Clear();

                _currentPanel = (WikiPanel)newPanel;

                // When entering Articles panel, rebuild from current category
                if (_currentPanel == WikiPanel.Articles)
                {
                    // Reset index only when going forward from Categories
                    if (direction > 0) _articleIndex = 0;
                    RebuildArticles();
                    // Clamp index if list size changed
                    if (_articleIndex >= _articleSlots.Count)
                        _articleIndex = Math.Max(0, _articleSlots.Count - 1);
                }
                // When entering Content panel, rebuild from current article
                else if (_currentPanel == WikiPanel.Content)
                {
                    // Reset index only when going forward from Articles
                    if (direction > 0) _contentLineIndex = 0;
                    RebuildContent();
                    // Clamp index if list size changed
                    if (_contentLineIndex >= _contentLines.Count)
                        _contentLineIndex = Math.Max(0, _contentLines.Count - 1);
                }
                // When going back to Categories, _categoryIndex is already correct

                AnnounceCurrentPanel();
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Jump to first element in current panel (Home).
        /// </summary>
        public void NavigateElementToFirst()
        {
            switch (_currentPanel)
            {
                case WikiPanel.Categories:
                    if (_categoryButtons.Count > 0) { _categoryIndex = 0; AnnounceCurrentElement(); }
                    break;
                case WikiPanel.Articles:
                    if (_articleSlots.Count > 0) { _articleIndex = 0; AnnounceCurrentElement(); }
                    break;
                case WikiPanel.Content:
                    if (_contentLines.Count > 0) { _contentLineIndex = 0; AnnounceCurrentElement(); }
                    break;
            }
        }

        /// <summary>
        /// Jump to last element in current panel (End).
        /// </summary>
        public void NavigateElementToLast()
        {
            switch (_currentPanel)
            {
                case WikiPanel.Categories:
                    if (_categoryButtons.Count > 0) { _categoryIndex = _categoryButtons.Count - 1; AnnounceCurrentElement(); }
                    break;
                case WikiPanel.Articles:
                    if (_articleSlots.Count > 0) { _articleIndex = _articleSlots.Count - 1; AnnounceCurrentElement(); }
                    break;
                case WikiPanel.Content:
                    if (_contentLines.Count > 0) { _contentLineIndex = _contentLines.Count - 1; AnnounceCurrentElement(); }
                    break;
            }
        }

        /// <summary>
        /// Navigate within current panel (Up/Down).
        /// </summary>
        public void NavigateElement(int direction)
        {
            if (!IsActive) return;

            switch (_currentPanel)
            {
                case WikiPanel.Categories:
                    NavigateCategories(direction);
                    break;
                case WikiPanel.Articles:
                    NavigateArticles(direction);
                    break;
                case WikiPanel.Content:
                    NavigateContentLines(direction);
                    break;
            }
        }

        /// <summary>
        /// Activate current element (Enter/Space).
        /// </summary>
        public bool ActivateCurrentElement()
        {
            if (!IsActive) return false;

            switch (_currentPanel)
            {
                case WikiPanel.Categories:
                    return ActivateCategory();
                case WikiPanel.Articles:
                    return ActivateArticle();
                case WikiPanel.Content:
                    // Re-read current line
                    AnnounceCurrentElement();
                    return true;
            }

            return false;
        }

        // ========================================
        // CATEGORY NAVIGATION
        // ========================================

        private void RebuildCategories()
        {
            _categoryButtons.Clear();

            var buttons = WikiReflection.GetWikiCategoryButtons(_wikiPopup);
            if (buttons == null)
            {
                Debug.LogWarning("[ATSAccessibility] Could not get wiki category buttons");
                return;
            }

            foreach (var button in buttons)
            {
                if (button == null) continue;

                // Category buttons are always present (6 fixed buttons)
                // Don't filter by activeInHierarchy - they may not be fully initialized yet
                var comp = button as Component;
                if (comp != null)
                {
                    _categoryButtons.Add(button);
                }
            }

            // Find which category is currently active
            var currentPanel = WikiReflection.GetCurrentWikiPanel(_wikiPopup);
            if (currentPanel != null)
            {
                _currentCategoryPanel = currentPanel;

                // Find the button for this panel
                for (int i = 0; i < _categoryButtons.Count; i++)
                {
                    var buttonPanel = WikiReflection.GetCategoryButtonPanel(_categoryButtons[i]);
                    if (buttonPanel == currentPanel)
                    {
                        _categoryIndex = i;
                        break;
                    }
                }
            }
        }

        private void NavigateCategories(int direction)
        {
            if (_categoryButtons.Count == 0) return;

            _categoryIndex += direction;

            // Wrap around
            if (_categoryIndex < 0) _categoryIndex = _categoryButtons.Count - 1;
            if (_categoryIndex >= _categoryButtons.Count) _categoryIndex = 0;

            AnnounceCurrentElement();
        }

        private bool ActivateCategory()
        {
            if (_categoryButtons.Count == 0 || _categoryIndex < 0 || _categoryIndex >= _categoryButtons.Count)
                return false;

            var button = _categoryButtons[_categoryIndex];
            WikiReflection.ClickWikiButton(button);

            // Update the current panel reference
            _currentCategoryPanel = WikiReflection.GetCategoryButtonPanel(button);

            // Auto-advance to Articles panel
            _currentPanel = WikiPanel.Articles;
            _articleIndex = 0;  // Reset for new category
            RebuildArticles();
            AnnounceCurrentPanel();
            AnnounceCurrentElement();

            return true;
        }

        // ========================================
        // ARTICLE NAVIGATION
        // ========================================

        private void RebuildArticles()
        {
            _articleSlots.Clear();
            // Note: Don't reset _articleIndex here - caller controls it

            // Get the current panel from the wiki popup
            var currentPanel = WikiReflection.GetCurrentWikiPanel(_wikiPopup);
            if (currentPanel == null)
            {
                Debug.LogWarning("[ATSAccessibility] No current wiki panel");
                return;
            }

            _currentCategoryPanel = currentPanel;

            // Get slots from the panel
            var slots = WikiReflection.GetPanelSlots(currentPanel);
            if (slots == null)
            {
                Debug.LogWarning("[ATSAccessibility] Could not get panel slots");
                return;
            }

            foreach (var slot in slots)
            {
                if (slot == null) continue;

                // Only include active slots
                var comp = slot as Component;
                if (comp != null && comp.gameObject.activeInHierarchy)
                {
                    _articleSlots.Add(slot);
                }
            }
        }

        private void NavigateArticles(int direction)
        {
            if (_articleSlots.Count == 0) return;

            _articleIndex += direction;

            // Wrap around
            if (_articleIndex < 0) _articleIndex = _articleSlots.Count - 1;
            if (_articleIndex >= _articleSlots.Count) _articleIndex = 0;

            AnnounceCurrentElement();
        }

        private bool ActivateArticle()
        {
            if (_articleSlots.Count == 0 || _articleIndex < 0 || _articleIndex >= _articleSlots.Count)
                return false;

            var slot = _articleSlots[_articleIndex];

            // Check if unlocked
            if (!WikiReflection.IsWikiSlotUnlocked(slot))
            {
                Speech.Say("Locked");
                return false;
            }

            WikiReflection.ClickWikiButton(slot);

            // Auto-advance to Content panel
            _currentPanel = WikiPanel.Content;
            _contentLineIndex = 0;  // Start at top
            RebuildContent();
            AnnounceCurrentPanel();
            AnnounceCurrentElement();

            return true;
        }

        // ========================================
        // CONTENT NAVIGATION
        // ========================================

        private void RebuildContent()
        {
            _contentLines.Clear();
            // Note: Don't reset _contentLineIndex here - caller controls it

            // Check for structured article types
            if (_articleIndex >= 0 && _articleIndex < _articleSlots.Count)
            {
                var slot = _articleSlots[_articleIndex];

                // Check for race article
                if (WikiReflection.IsWikiRaceSlot(slot))
                {
                    BuildRaceContent(slot);
                    return;
                }

                // Check for building article
                if (WikiReflection.IsWikiBuildingSlot(slot))
                {
                    BuildBuildingContent(slot);
                    return;
                }

                // Check for glade event (relic) article
                if (WikiReflection.IsWikiRelicSlot(slot))
                {
                    BuildRelicContent(slot);
                    return;
                }
            }

            // Fall back to generic UI text extraction for other article types
            var contentText = ExtractPreviewContent();
            if (string.IsNullOrEmpty(contentText))
            {
                Debug.LogWarning("[ATSAccessibility] No preview content found");
                return;
            }

            // Split by newlines for line-by-line navigation
            var lines = contentText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    _contentLines.Add(trimmed);
                }
            }
        }

        /// <summary>
        /// Build structured content for a race/species article.
        /// Extracts data directly from RaceModel via reflection for proper ordering.
        /// </summary>
        private void BuildRaceContent(object raceSlot)
        {
            var raceModel = WikiReflection.GetRaceModelFromSlot(raceSlot);
            if (raceModel == null) return;

            // 1. Name
            AddIfNotEmpty(WikiReflection.GetRaceDisplayName(raceModel));

            // 2. Description
            AddIfNotEmpty(WikiReflection.GetRaceDescription(raceModel));

            // 3. Stats (one per line, "Label: value" format)
            var resolve = WikiReflection.GetRaceInitialResolve(raceModel);
            _contentLines.Add($"Resolve: {resolve}");

            var interval = WikiReflection.GetRaceNeedsInterval(raceModel);
            _contentLines.Add($"Break Interval: {FormatMinSec(interval)}");

            var resilience = WikiReflection.GetRaceResilienceLabel(raceModel);
            AddIfNotEmpty("Resilience: " + resilience);

            var demanding = WikiReflection.GetRaceDemanding(raceModel);
            _contentLines.Add($"Demanding: {demanding}");

            var decadent = WikiReflection.GetRaceDecadent(raceModel);
            _contentLines.Add($"Decadent: {Mathf.RoundToInt(decadent)}");

            var hunger = WikiReflection.GetRaceHungerTolerance(raceModel);
            _contentLines.Add($"Hunger Tolerance: {hunger}");

            // 4. Effects
            var revealEffect = WikiReflection.GetRaceRevealEffect(raceModel);
            if (!string.IsNullOrEmpty(revealEffect))
                _contentLines.Add("Reveal Effect: " + revealEffect);

            var passiveEffect = WikiReflection.GetRacePassiveEffect(raceModel);
            if (!string.IsNullOrEmpty(passiveEffect))
                _contentLines.Add("Passive Effect: " + passiveEffect);

            // 5. Needs (comma-separated on one line)
            var needs = WikiReflection.GetRaceNeeds(raceModel);
            if (needs != null && needs.Length > 0)
            {
                var needNames = new List<string>();
                foreach (var need in needs)
                {
                    var needName = WikiReflection.GetNeedDisplayName(need);
                    if (!string.IsNullOrEmpty(needName))
                        needNames.Add(needName);
                }
                if (needNames.Count > 0)
                    _contentLines.Add("Needs: " + string.Join(", ", needNames));
            }

            // 6. Species Buildings (comma-separated on one line)
            var buildings = WikiReflection.GetRaceBuildings(raceModel);
            if (buildings != null && buildings.Length > 0)
            {
                var buildingNames = new List<string>();
                foreach (var building in buildings)
                {
                    var buildingName = WikiReflection.GetBuildingDisplayName(building);
                    if (!string.IsNullOrEmpty(buildingName))
                        buildingNames.Add(buildingName);
                }
                if (buildingNames.Count > 0)
                    _contentLines.Add("Species Buildings: " + string.Join(", ", buildingNames));
            }

            // 7. Specializations (multi-line with header)
            var characteristics = WikiReflection.GetRaceCharacteristicsText(raceModel);
            if (!string.IsNullOrEmpty(characteristics))
            {
                _contentLines.Add("Specializations:");
                foreach (var line in characteristics.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = line.Trim();
                    if (!string.IsNullOrEmpty(cleaned))
                        _contentLines.Add("  " + cleaned);
                }
            }
        }

        /// <summary>
        /// Build structured content for a building article.
        /// Extracts data directly from BuildingModel via reflection.
        /// </summary>
        private void BuildBuildingContent(object buildingSlot)
        {
            var building = WikiReflection.GetBuildingModelFromSlot(buildingSlot);
            if (building == null) return;

            // 1. Name
            AddIfNotEmpty(WikiReflection.GetBuildingDisplayName(building));

            // 2. Description (includes production list for workshops)
            AddIfNotEmpty(WikiReflection.GetBuildingDescription(building));

            // 3. Category
            var category = WikiReflection.GetBuildingCategory(building);
            if (!string.IsNullOrEmpty(category))
                _contentLines.Add($"Category: {category}");

            // 4. Size
            var size = WikiReflection.GetBuildingSize(building);
            if (size.x > 0 && size.y > 0)
                _contentLines.Add($"Size: {size.x}x{size.y}");

            // 5. Workplaces
            var workplaces = WikiReflection.GetBuildingWorkplacesCount(building);
            if (workplaces > 0)
                _contentLines.Add($"Workplaces: {workplaces}");

            // 6. Movability
            var movable = WikiReflection.GetBuildingMovable(building);
            _contentLines.Add(movable ? "Can be moved" : "Cannot be moved");

            // 7. Construction cost
            var requiredGoods = WikiReflection.GetBuildingRequiredGoods(building);
            if (requiredGoods != null && requiredGoods.Length > 0)
            {
                var costs = new List<string>();
                foreach (var goodRef in requiredGoods)
                {
                    var name = WikiReflection.GetGoodRefDisplayName(goodRef);
                    var amount = WikiReflection.GetGoodRefAmount(goodRef);
                    if (!string.IsNullOrEmpty(name) && amount > 0)
                        costs.Add($"{amount} {name}");
                }
                if (costs.Count > 0)
                    _contentLines.Add("Construction: " + string.Join(", ", costs));
            }

            // 8. Tags
            var tags = WikiReflection.GetBuildingTags(building);
            if (tags != null && tags.Length > 0)
            {
                var tagNames = new List<string>();
                foreach (var tag in tags)
                {
                    if (WikiReflection.GetTagVisible(tag))
                    {
                        var name = WikiReflection.GetTagDisplayName(tag);
                        if (!string.IsNullOrEmpty(name))
                            tagNames.Add(name);
                    }
                }
                if (tagNames.Count > 0)
                    _contentLines.Add("Tags: " + string.Join(", ", tagNames));
            }

            // 9. Recipes (for workshops)
            if (WikiReflection.IsWorkshopModel(building))
            {
                BuildWorkshopRecipes(building);
            }

            // 10. Upgrades (for upgradable buildings)
            if (WikiReflection.IsUpgradableBuildingModel(building) &&
                !WikiReflection.GetHideUpgradesInWiki(building))
            {
                BuildUpgradeInfo(building);
            }
        }

        /// <summary>
        /// Build recipe list for a workshop building.
        /// </summary>
        private void BuildWorkshopRecipes(object workshop)
        {
            var recipes = WikiReflection.GetWorkshopRecipes(workshop);
            if (recipes == null || recipes.Length == 0) return;

            _contentLines.Add("Recipes:");
            foreach (var recipe in recipes)
            {
                var outputName = WikiReflection.GetRecipeOutputName(recipe);
                var outputAmount = WikiReflection.GetRecipeOutputAmount(recipe);
                var productionTime = WikiReflection.GetRecipeProductionTime(recipe);
                var gradeLevel = WikiReflection.GetRecipeGradeLevel(recipe);

                // Build inputs string
                var requiredGoods = WikiReflection.GetRecipeRequiredGoods(recipe);
                var inputs = RecipeFormatter.FormatIngredients(requiredGoods,
                    WikiReflection.GetGoodsSetGoods, WikiReflection.GetGoodRefDisplayName, WikiReflection.GetGoodRefAmount);

                // Format time
                var time = RecipeFormatter.FormatTime(productionTime);

                // Format stars
                var stars = gradeLevel > 0 ? $" {gradeLevel} star{(gradeLevel > 1 ? "s" : "")}." : "";

                _contentLines.Add($"  {outputName} x {outputAmount}: {inputs} {time}{stars}");
            }
        }


        /// <summary>
        /// Build upgrade information for an upgradable building.
        /// </summary>
        private void BuildUpgradeInfo(object building)
        {
            var levels = WikiReflection.GetBuildingLevels(building);
            if (levels == null || levels.Length <= 1) return;  // Skip if only base level

            _contentLines.Add("Upgrades:");

            // Start from index 1 (level I), skip index 0 (base)
            for (int i = 1; i < levels.Length; i++)
            {
                var level = levels.GetValue(i);
                var levelNum = IntToRoman(i);  // I, II, III, etc.

                // Get upgrade cost
                var requiredGoods = WikiReflection.GetLevelRequiredGoods(level);
                var costStr = FormatUpgradeCost(requiredGoods);

                _contentLines.Add($"  Level {levelNum}: {costStr}");

                // Get perk options
                var options = WikiReflection.GetLevelOptions(level);
                if (options != null)
                {
                    foreach (var perk in options)
                    {
                        var name = WikiReflection.GetPerkDisplayName(perk);
                        var amount = WikiReflection.GetPerkAmountText(perk);
                        var desc = WikiReflection.GetPerkDescription(perk);

                        // Format: "Perk Name (+10%): Description"
                        var amountPart = !string.IsNullOrEmpty(amount) ? $" ({amount})" : "";
                        _contentLines.Add($"    {name}{amountPart}: {desc}");
                    }
                }
            }
        }

        /// <summary>
        /// Build structured content for a glade event (relic) article.
        /// </summary>
        private void BuildRelicContent(object relicSlot)
        {
            var relic = WikiReflection.GetRelicModelFromSlot(relicSlot);
            if (relic == null) return;

            // 1. Name
            AddIfNotEmpty(WikiReflection.GetRelicDisplayName(relic));

            // 2. Danger level and workers
            var dangerLevel = WikiReflection.GetRelicDangerLevel(relic);
            var workplaces = WikiReflection.GetRelicWorkplacesCount(relic);
            if (!string.IsNullOrEmpty(dangerLevel))
            {
                string workerText = workplaces > 0 ? $", {workplaces} worker{(workplaces > 1 ? "s" : "")}" : "";
                _contentLines.Add($"Danger: {dangerLevel}{workerText}");
            }

            // 3. Effects section
            bool hasDynamicEffects = WikiReflection.GetRelicHasDynamicEffects(relic);
            if (hasDynamicEffects)
            {
                BuildRelicDynamicEffects(relic);
            }
            else
            {
                BuildRelicStaticEffects(relic);
            }

            // 4. Requirements/Decisions section
            bool hasDecision = WikiReflection.GetRelicHasDecision(relic);
            if (hasDecision)
            {
                BuildRelicDecisions(relic);
            }
            else
            {
                BuildRelicSinglePath(relic);
            }
        }

        /// <summary>
        /// Build dynamic (escalating) effects section for a relic.
        /// </summary>
        private void BuildRelicDynamicEffects(object relic)
        {
            var effectsTiers = WikiReflection.GetRelicEffectsTiers(relic);
            if (effectsTiers == null || effectsTiers.Length == 0) return;

            _contentLines.Add("Effects (escalating):");

            int tierNum = 1;
            foreach (var tier in effectsTiers)
            {
                var effects = WikiReflection.GetEffectStepEffects(tier);
                if (effects == null || effects.Length == 0) continue;

                float timeToStart = WikiReflection.GetEffectStepTimeToStart(tier);
                string timeStr = FormatMinSec(timeToStart);

                var effectNames = new List<string>();
                foreach (var effect in effects)
                {
                    var name = WikiReflection.GetEffectDisplayName(effect);
                    if (!string.IsNullOrEmpty(name))
                        effectNames.Add(name);
                }

                if (effectNames.Count > 0)
                {
                    _contentLines.Add($"  After {timeStr}: {string.Join(", ", effectNames)}");
                }
                tierNum++;
            }
        }

        /// <summary>
        /// Build static effects section for a relic.
        /// </summary>
        private void BuildRelicStaticEffects(object relic)
        {
            var activeEffects = WikiReflection.GetRelicActiveEffects(relic);
            if (activeEffects == null || activeEffects.Length == 0)
            {
                _contentLines.Add("Effects: None");
                return;
            }

            _contentLines.Add("Effects:");
            foreach (var effect in activeEffects)
            {
                var name = WikiReflection.GetEffectDisplayName(effect);
                if (!string.IsNullOrEmpty(name))
                    _contentLines.Add($"  {name}");
            }
        }

        /// <summary>
        /// Build decision paths for a multi-decision relic.
        /// </summary>
        private void BuildRelicDecisions(object relic)
        {
            var difficulties = WikiReflection.GetRelicDifficulties(relic);
            if (difficulties == null || difficulties.Length == 0) return;

            // Use the first difficulty level for base info
            var baseDifficulty = difficulties.GetValue(0);
            var decisions = WikiReflection.GetRelicDifficultyDecisions(baseDifficulty);
            if (decisions == null || decisions.Length == 0) return;

            var decisionsRewards = WikiReflection.GetRelicDecisionsRewards(relic);

            int decisionNum = 1;
            foreach (var decision in decisions)
            {
                var label = WikiReflection.GetRelicDecisionLabel(decision);
                if (string.IsNullOrEmpty(label))
                    label = $"Option {decisionNum}";

                _contentLines.Add($"Decision: {label}");

                // Working time
                float workingTime = WikiReflection.GetRelicDecisionWorkingTime(decision);
                if (workingTime > 0)
                    _contentLines.Add($"  Time: {FormatMinSec(workingTime)}");

                // Required goods
                var requiredGoods = WikiReflection.GetRelicDecisionRequiredGoods(decision);
                if (requiredGoods != null && requiredGoods.Length > 0)
                {
                    string costStr = FormatGoodsSets(requiredGoods, " + ", " OR ");
                    if (!string.IsNullOrEmpty(costStr))
                        _contentLines.Add($"  Cost: {costStr}");
                }

                // Working effects (during investigation)
                var workingEffects = WikiReflection.GetRelicDecisionWorkingEffects(decision);
                if (workingEffects != null && workingEffects.Length > 0)
                {
                    var effectNames = new List<string>();
                    foreach (var effect in workingEffects)
                    {
                        var name = WikiReflection.GetEffectDisplayName(effect);
                        if (!string.IsNullOrEmpty(name))
                            effectNames.Add(name);
                    }
                    if (effectNames.Count > 0)
                        _contentLines.Add($"  During: {string.Join(", ", effectNames)}");
                }

                // Rewards for this decision
                if (decisionsRewards != null && decisionNum - 1 < decisionsRewards.Length)
                {
                    var rewardTable = decisionsRewards.GetValue(decisionNum - 1);
                    var rewards = WikiReflection.GetEffectsTableAllEffects(rewardTable);
                    if (rewards != null && rewards.Count > 0)
                    {
                        var rewardNames = new List<string>();
                        foreach (var reward in rewards)
                        {
                            var name = WikiReflection.GetEffectDisplayName(reward);
                            if (!string.IsNullOrEmpty(name))
                                rewardNames.Add(name);
                        }
                        if (rewardNames.Count > 0)
                            _contentLines.Add($"  Rewards: {string.Join(", ", rewardNames)}");
                    }
                }

                decisionNum++;
            }
        }

        /// <summary>
        /// Build single-path requirements for a relic without decisions.
        /// </summary>
        private void BuildRelicSinglePath(object relic)
        {
            var difficulties = WikiReflection.GetRelicDifficulties(relic);
            if (difficulties == null || difficulties.Length == 0) return;

            // Use the first difficulty level
            var baseDifficulty = difficulties.GetValue(0);
            var decisions = WikiReflection.GetRelicDifficultyDecisions(baseDifficulty);
            if (decisions == null || decisions.Length == 0) return;

            var decision = decisions.GetValue(0);

            _contentLines.Add("Requirements:");

            // Working time
            float workingTime = WikiReflection.GetRelicDecisionWorkingTime(decision);
            if (workingTime > 0)
                _contentLines.Add($"  Time: {FormatMinSec(workingTime)}");

            // Required goods
            var requiredGoods = WikiReflection.GetRelicDecisionRequiredGoods(decision);
            if (requiredGoods != null && requiredGoods.Length > 0)
            {
                string costStr = FormatGoodsSets(requiredGoods, " + ", " OR ");
                if (!string.IsNullOrEmpty(costStr))
                    _contentLines.Add($"  Cost: {costStr}");
            }

            // Working effects
            var workingEffects = WikiReflection.GetRelicDecisionWorkingEffects(decision);
            if (workingEffects != null && workingEffects.Length > 0)
            {
                var effectNames = new List<string>();
                foreach (var effect in workingEffects)
                {
                    var name = WikiReflection.GetEffectDisplayName(effect);
                    if (!string.IsNullOrEmpty(name))
                        effectNames.Add(name);
                }
                if (effectNames.Count > 0)
                    _contentLines.Add($"  During: {string.Join(", ", effectNames)}");
            }

            // Rewards
            var rewardsTiers = WikiReflection.GetRelicRewardsTiers(relic);
            if (rewardsTiers != null && rewardsTiers.Length > 0)
            {
                var firstTier = rewardsTiers.GetValue(0);
                var rewards = WikiReflection.GetRewardStepAllEffects(firstTier);
                if (rewards != null && rewards.Count > 0)
                {
                    var rewardNames = new List<string>();
                    foreach (var reward in rewards)
                    {
                        var name = WikiReflection.GetEffectDisplayName(reward);
                        if (!string.IsNullOrEmpty(name))
                            rewardNames.Add(name);
                    }
                    if (rewardNames.Count > 0)
                        _contentLines.Add($"  Rewards: {string.Join(", ", rewardNames)}");
                }
            }
        }

        /// <summary>
        /// Convert an integer to Roman numerals (for upgrade levels).
        /// </summary>
        private string IntToRoman(int num)
        {
            return num switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                _ => num.ToString()
            };
        }

        /// <summary>
        /// Format an array of GoodsSet objects as a readable string.
        /// Each GoodsSet represents one required input slot (joined by separator).
        /// Multiple goods within a GoodsSet are alternatives (joined by altSeparator).
        /// </summary>
        private string FormatGoodsSets(Array goodsSets, string separator = ", + ", string altSeparator = " OR ")
        {
            if (goodsSets == null || goodsSets.Length == 0) return null;

            var parts = new List<string>();
            foreach (var goodsSet in goodsSets)
            {
                var goods = WikiReflection.GetGoodsSetGoods(goodsSet);
                if (goods != null && goods.Length > 0)
                {
                    var alternatives = new List<string>();
                    foreach (var goodRef in goods)
                    {
                        var name = WikiReflection.GetGoodRefDisplayName(goodRef);
                        var amount = WikiReflection.GetGoodRefAmount(goodRef);
                        if (!string.IsNullOrEmpty(name))
                            alternatives.Add($"{amount} {name}");
                    }
                    if (alternatives.Count > 0)
                        parts.Add(string.Join(altSeparator, alternatives));
                }
            }
            return parts.Count > 0 ? string.Join(separator, parts) : null;
        }

        /// <summary>
        /// Format the upgrade cost from an array of GoodsSet objects.
        /// </summary>
        private string FormatUpgradeCost(Array requiredGoods)
        {
            return FormatGoodsSets(requiredGoods) ?? "Free";
        }

        /// <summary>
        /// Helper to add a line if it's not null or empty.
        /// </summary>
        private void AddIfNotEmpty(string text)
        {
            if (!string.IsNullOrEmpty(text))
                _contentLines.Add(text);
        }

        /// <summary>
        /// Format seconds as MM:SS.
        /// </summary>
        private string FormatMinSec(float totalSeconds)
        {
            int mins = (int)(totalSeconds / 60);
            int secs = (int)(totalSeconds % 60);
            if (mins > 0 && secs > 0)
                return $"{mins}m {secs}s";
            else if (mins > 0)
                return $"{mins}m";
            else
                return $"{secs}s";
        }

        private string ExtractPreviewContent()
        {
            // Get the current panel
            var currentPanel = WikiReflection.GetCurrentWikiPanel(_wikiPopup);
            if (currentPanel == null) return null;

            var panelComp = currentPanel as Component;
            if (panelComp == null) return null;

            // Find the preview in the panel's hierarchy
            // Preview components have TMP_Text children for header and text
            var previewTransform = panelComp.transform.Find("Content/Preview");
            if (previewTransform == null)
            {
                // Try finding any child named "Preview"
                previewTransform = FindChildRecursive(panelComp.transform, "Preview");
            }

            if (previewTransform == null)
            {
                Debug.Log("[ATSAccessibility] Could not find Preview transform");
                return null;
            }

            // Collect all text from TMP_Text components in the preview
            var textComponents = previewTransform.GetComponentsInChildren<TMP_Text>(true);
            var allText = new System.Text.StringBuilder();

            foreach (var textComp in textComponents)
            {
                if (textComp == null || !textComp.gameObject.activeInHierarchy)
                    continue;

                var text = textComp.text;
                if (!string.IsNullOrEmpty(text))
                {
                    allText.AppendLine(text);
                }
            }

            return allText.ToString();
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void NavigateContentLines(int direction)
        {
            if (_contentLines.Count == 0) return;

            _contentLineIndex += direction;

            // Clamp (don't wrap for content)
            if (_contentLineIndex < 0) _contentLineIndex = 0;
            if (_contentLineIndex >= _contentLines.Count) _contentLineIndex = _contentLines.Count - 1;

            AnnounceCurrentElement();
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle an alphabetic key press for type-ahead search.
        /// Active in Categories and Articles panels.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            if (_currentPanel == WikiPanel.Categories)
            {
                if (_categoryButtons.Count == 0) return;

                _search.AddChar(c);
                int matchIndex = _search.FindMatch(_categoryButtons, button =>
                {
                    var comp = button as Component;
                    return comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : null;
                });

                if (matchIndex >= 0)
                {
                    _categoryIndex = matchIndex;
                    AnnounceCategoryElement();
                }
                else
                {
                    Speech.Say($"No match for {_search.Buffer}");
                }
                return;
            }

            if (_currentPanel != WikiPanel.Articles)
                return;

            if (_articleSlots.Count == 0)
                return;

            _search.AddChar(c);

            // Find first article starting with buffer
            int matchIndex2 = _search.FindMatch(_articleSlots, slot =>
            {
                var comp = slot as Component;
                return comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : null;
            });

            if (matchIndex2 >= 0)
            {
                _articleIndex = matchIndex2;
                AnnounceArticleElement();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        /// <summary>
        /// Handle backspace key - remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (_currentPanel != WikiPanel.Categories && _currentPanel != WikiPanel.Articles)
                return;

            if (!_search.RemoveChar())
                return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            if (_currentPanel == WikiPanel.Categories)
            {
                int matchIndex = _search.FindMatch(_categoryButtons, button =>
                {
                    var comp = button as Component;
                    return comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : null;
                });

                if (matchIndex >= 0)
                {
                    _categoryIndex = matchIndex;
                    AnnounceCategoryElement();
                }
                else
                {
                    Speech.Say($"No match for {_search.Buffer}");
                }
            }
            else
            {
                int matchIndex = _search.FindMatch(_articleSlots, slot =>
                {
                    var comp = slot as Component;
                    return comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : null;
                });

                if (matchIndex >= 0)
                {
                    _articleIndex = matchIndex;
                    AnnounceArticleElement();
                }
                else
                {
                    Speech.Say($"No match for {_search.Buffer}");
                }
            }
        }

        /// <summary>
        /// Clear the search buffer.
        /// </summary>
        private void ClearSearchBuffer()
        {
            if (_search.HasBuffer)
            {
                _search.Clear();
                Speech.Say("Search cleared");
            }
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnounceCurrentPanel()
        {
            string panelName = _currentPanel switch
            {
                WikiPanel.Categories => "Categories",
                WikiPanel.Articles => "Articles",
                WikiPanel.Content => "Content",
                _ => "Unknown"
            };

            Speech.Say(panelName);
        }

        private void AnnounceCurrentElement()
        {
            switch (_currentPanel)
            {
                case WikiPanel.Categories:
                    AnnounceCategoryElement();
                    break;
                case WikiPanel.Articles:
                    AnnounceArticleElement();
                    break;
                case WikiPanel.Content:
                    AnnounceContentElement();
                    break;
            }
        }

        private void AnnounceCategoryElement()
        {
            if (_categoryButtons.Count == 0)
            {
                Speech.Say("No categories");
                return;
            }

            if (_categoryIndex >= _categoryButtons.Count)
                return;

            var button = _categoryButtons[_categoryIndex];
            var comp = button as Component;
            string name = comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : "Unknown";

            Speech.Say(name);
        }

        private void AnnounceArticleElement()
        {
            if (_articleSlots.Count == 0)
            {
                Speech.Say("No articles");
                return;
            }

            if (_articleIndex >= _articleSlots.Count)
                return;

            var slot = _articleSlots[_articleIndex];
            var comp = slot as Component;
            string name = comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : "Unknown";

            bool unlocked = WikiReflection.IsWikiSlotUnlocked(slot);
            string lockStatus = unlocked ? "" : ", locked";

            Speech.Say($"{name}, button{lockStatus}");
        }

        private void AnnounceContentElement()
        {
            if (_contentLines.Count == 0)
            {
                Speech.Say("No content");
                return;
            }

            if (_contentLineIndex >= _contentLines.Count)
                return;

            var line = _contentLines[_contentLineIndex];
            Speech.Say(line);
        }
    }
}
