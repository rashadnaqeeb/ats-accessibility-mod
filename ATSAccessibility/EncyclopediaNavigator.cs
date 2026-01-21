using System;
using System.Collections.Generic;
using System.Linq;
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

            Debug.Log("[ATSAccessibility] EncyclopediaNavigator activated");

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

            Debug.Log("[ATSAccessibility] EncyclopediaNavigator deactivated");
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

            Debug.Log($"[ATSAccessibility] Found {_categoryButtons.Count} wiki category buttons");

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

            Debug.Log($"[ATSAccessibility] Activated category {_categoryIndex}");

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

            Debug.Log($"[ATSAccessibility] Found {_articleSlots.Count} wiki article slots");
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

            Debug.Log($"[ATSAccessibility] Activated article {_articleIndex}");

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
                    Debug.Log($"[ATSAccessibility] Built structured race content: {_contentLines.Count} lines");
                    return;
                }

                // Check for building article
                if (WikiReflection.IsWikiBuildingSlot(slot))
                {
                    BuildBuildingContent(slot);
                    Debug.Log($"[ATSAccessibility] Built structured building content: {_contentLines.Count} lines");
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

            Debug.Log($"[ATSAccessibility] Found {_contentLines.Count} content lines");
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

                // Build inputs string using new recipe format
                var requiredGoods = WikiReflection.GetRecipeRequiredGoods(recipe);
                var inputs = FormatRecipeIngredients(requiredGoods);

                // Format time
                var time = FormatRecipeTime(productionTime);

                // Format stars
                var stars = gradeLevel > 0 ? $" {gradeLevel} star{(gradeLevel > 1 ? "s" : "")}." : "";

                _contentLines.Add($"  {outputName} x {outputAmount}: {inputs} {time}{stars}");
            }
        }

        /// <summary>
        /// Format recipe ingredients in the new readable format.
        /// Same amounts: "3 x one of Herbs, Insects, Resin."
        /// Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
        /// </summary>
        private string FormatRecipeIngredients(Array goodsSets)
        {
            if (goodsSets == null || goodsSets.Length == 0) return "nothing.";

            var parts = new List<string>();
            foreach (var goodsSet in goodsSets)
            {
                var goods = WikiReflection.GetGoodsSetGoods(goodsSet);
                if (goods == null || goods.Length == 0) continue;

                // Collect names and amounts
                var items = new List<(string name, int amount)>();
                foreach (var goodRef in goods)
                {
                    var name = WikiReflection.GetGoodRefDisplayName(goodRef);
                    var amount = WikiReflection.GetGoodRefAmount(goodRef);
                    if (!string.IsNullOrEmpty(name))
                        items.Add((name, amount));
                }

                if (items.Count == 0) continue;

                if (items.Count == 1)
                {
                    // Single item, no alternatives
                    parts.Add($"{items[0].name} x {items[0].amount}.");
                }
                else
                {
                    // Multiple alternatives - check if all amounts are the same
                    bool sameAmounts = items.All(i => i.amount == items[0].amount);

                    if (sameAmounts)
                    {
                        // Same amounts: "3 x one of Herbs, Insects, Resin."
                        var names = string.Join(", ", items.Select(i => i.name));
                        parts.Add($"{items[0].amount} x one of {names}.");
                    }
                    else
                    {
                        // Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
                        var itemStrs = items.Select(i => $"{i.name} x {i.amount}");
                        parts.Add($"One of {string.Join(", ", itemStrs)}.");
                    }
                }
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "nothing.";
        }

        /// <summary>
        /// Format production time for recipes.
        /// </summary>
        private string FormatRecipeTime(float totalSeconds)
        {
            int secs = (int)totalSeconds;
            return $"Takes {secs} sec.";
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
        /// Only active in the Articles panel.
        /// </summary>
        private void HandleSearchKey(char c)
        {
            // Type-ahead search only works in Articles panel
            if (_currentPanel != WikiPanel.Articles)
                return;

            if (_articleSlots.Count == 0)
                return;

            _search.AddChar(c);

            // Find first article starting with buffer
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

        /// <summary>
        /// Handle backspace key - remove last character from search buffer.
        /// </summary>
        private void HandleBackspace()
        {
            if (_currentPanel != WikiPanel.Articles)
                return;

            if (!_search.RemoveChar())
                return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
            }
            else
            {
                // Re-search with shortened buffer
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
