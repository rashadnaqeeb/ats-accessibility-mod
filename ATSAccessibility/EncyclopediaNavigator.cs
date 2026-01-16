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
    public class EncyclopediaNavigator
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

        public bool IsActive => _wikiPopup != null;

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
                _currentPanel = (WikiPanel)newPanel;

                // When entering Articles panel, rebuild from current category
                if (_currentPanel == WikiPanel.Articles)
                {
                    RebuildArticles();
                }
                // When entering Content panel, rebuild from current article
                else if (_currentPanel == WikiPanel.Content)
                {
                    RebuildContent();
                }

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

            var buttons = GameReflection.GetWikiCategoryButtons(_wikiPopup);
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
            var currentPanel = GameReflection.GetCurrentWikiPanel(_wikiPopup);
            if (currentPanel != null)
            {
                _currentCategoryPanel = currentPanel;

                // Find the button for this panel
                for (int i = 0; i < _categoryButtons.Count; i++)
                {
                    var buttonPanel = GameReflection.GetCategoryButtonPanel(_categoryButtons[i]);
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
            GameReflection.ClickWikiButton(button);

            // Update the current panel reference
            _currentCategoryPanel = GameReflection.GetCategoryButtonPanel(button);

            Debug.Log($"[ATSAccessibility] Activated category {_categoryIndex}");

            // Give the game time to switch panels, then rebuild articles
            // (caller should wait a frame before switching panels)
            return true;
        }

        // ========================================
        // ARTICLE NAVIGATION
        // ========================================

        private void RebuildArticles()
        {
            _articleSlots.Clear();
            _articleIndex = 0;

            // Get the current panel from the wiki popup
            var currentPanel = GameReflection.GetCurrentWikiPanel(_wikiPopup);
            if (currentPanel == null)
            {
                Debug.LogWarning("[ATSAccessibility] No current wiki panel");
                return;
            }

            _currentCategoryPanel = currentPanel;

            // Get slots from the panel
            var slots = GameReflection.GetPanelSlots(currentPanel);
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
            if (!GameReflection.IsWikiSlotUnlocked(slot))
            {
                Speech.Say("Locked");
                return false;
            }

            GameReflection.ClickWikiButton(slot);

            Debug.Log($"[ATSAccessibility] Activated article {_articleIndex}");
            return true;
        }

        // ========================================
        // CONTENT NAVIGATION
        // ========================================

        private void RebuildContent()
        {
            _contentLines.Clear();
            _contentLineIndex = 0;

            // Check for structured article types
            if (_articleIndex >= 0 && _articleIndex < _articleSlots.Count)
            {
                var slot = _articleSlots[_articleIndex];

                // Check for race article
                if (GameReflection.IsWikiRaceSlot(slot))
                {
                    BuildRaceContent(slot);
                    Debug.Log($"[ATSAccessibility] Built structured race content: {_contentLines.Count} lines");
                    return;
                }

                // Check for building article
                if (GameReflection.IsWikiBuildingSlot(slot))
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
            var raceModel = GameReflection.GetRaceModelFromSlot(raceSlot);
            if (raceModel == null) return;

            // 1. Name
            AddIfNotEmpty(GameReflection.GetRaceDisplayName(raceModel));

            // 2. Description
            AddIfNotEmpty(StripRichTextTags(GameReflection.GetRaceDescription(raceModel)));

            // 3. Stats (one per line, "Label: value" format)
            var resolve = GameReflection.GetRaceInitialResolve(raceModel);
            _contentLines.Add($"Resolve: {resolve}");

            var interval = GameReflection.GetRaceNeedsInterval(raceModel);
            _contentLines.Add($"Break Interval: {FormatMinSec(interval)}");

            var resilience = GameReflection.GetRaceResilienceLabel(raceModel);
            AddIfNotEmpty("Resilience: " + resilience);

            var demanding = GameReflection.GetRaceDemanding(raceModel);
            _contentLines.Add($"Demanding: {demanding}");

            var decadent = GameReflection.GetRaceDecadent(raceModel);
            _contentLines.Add($"Decadent: {Mathf.RoundToInt(decadent)}");

            var hunger = GameReflection.GetRaceHungerTolerance(raceModel);
            _contentLines.Add($"Hunger Tolerance: {hunger}");

            // 4. Effects
            var revealEffect = GameReflection.GetRaceRevealEffect(raceModel);
            if (!string.IsNullOrEmpty(revealEffect))
                _contentLines.Add("Reveal Effect: " + StripRichTextTags(revealEffect));

            var passiveEffect = GameReflection.GetRacePassiveEffect(raceModel);
            if (!string.IsNullOrEmpty(passiveEffect))
                _contentLines.Add("Passive Effect: " + StripRichTextTags(passiveEffect));

            // 5. Needs (comma-separated on one line)
            var needs = GameReflection.GetRaceNeeds(raceModel);
            if (needs != null && needs.Length > 0)
            {
                var needNames = new List<string>();
                foreach (var need in needs)
                {
                    var needName = GameReflection.GetNeedDisplayName(need);
                    if (!string.IsNullOrEmpty(needName))
                        needNames.Add(needName);
                }
                if (needNames.Count > 0)
                    _contentLines.Add("Needs: " + string.Join(", ", needNames));
            }

            // 6. Species Buildings (comma-separated on one line)
            var buildings = GameReflection.GetRaceBuildings(raceModel);
            if (buildings != null && buildings.Length > 0)
            {
                var buildingNames = new List<string>();
                foreach (var building in buildings)
                {
                    var buildingName = GameReflection.GetBuildingDisplayName(building);
                    if (!string.IsNullOrEmpty(buildingName))
                        buildingNames.Add(buildingName);
                }
                if (buildingNames.Count > 0)
                    _contentLines.Add("Species Buildings: " + string.Join(", ", buildingNames));
            }

            // 7. Specializations (multi-line with header)
            var characteristics = GameReflection.GetRaceCharacteristicsText(raceModel);
            if (!string.IsNullOrEmpty(characteristics))
            {
                _contentLines.Add("Specializations:");
                foreach (var line in characteristics.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var cleaned = StripRichTextTags(line.Trim());
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
            var building = GameReflection.GetBuildingModelFromSlot(buildingSlot);
            if (building == null) return;

            // 1. Name
            AddIfNotEmpty(GameReflection.GetBuildingDisplayName(building));

            // 2. Description (includes production list for workshops)
            AddIfNotEmpty(StripRichTextTags(GameReflection.GetBuildingDescription(building)));

            // 3. Category
            var category = GameReflection.GetBuildingCategory(building);
            if (!string.IsNullOrEmpty(category))
                _contentLines.Add($"Category: {category}");

            // 4. Size
            var size = GameReflection.GetBuildingSize(building);
            if (size.x > 0 && size.y > 0)
                _contentLines.Add($"Size: {size.x}x{size.y}");

            // 5. Workplaces
            var workplaces = GameReflection.GetBuildingWorkplacesCount(building);
            if (workplaces > 0)
                _contentLines.Add($"Workplaces: {workplaces}");

            // 6. Movability
            var movable = GameReflection.GetBuildingMovable(building);
            _contentLines.Add(movable ? "Can be moved" : "Cannot be moved");

            // 7. Construction cost
            var requiredGoods = GameReflection.GetBuildingRequiredGoods(building);
            if (requiredGoods != null && requiredGoods.Length > 0)
            {
                var costs = new List<string>();
                foreach (var goodRef in requiredGoods)
                {
                    var name = GameReflection.GetGoodRefDisplayName(goodRef);
                    var amount = GameReflection.GetGoodRefAmount(goodRef);
                    if (!string.IsNullOrEmpty(name) && amount > 0)
                        costs.Add($"{amount} {name}");
                }
                if (costs.Count > 0)
                    _contentLines.Add("Construction: " + string.Join(", ", costs));
            }

            // 8. Tags
            var tags = GameReflection.GetBuildingTags(building);
            if (tags != null && tags.Length > 0)
            {
                var tagNames = new List<string>();
                foreach (var tag in tags)
                {
                    if (GameReflection.GetTagVisible(tag))
                    {
                        var name = GameReflection.GetTagDisplayName(tag);
                        if (!string.IsNullOrEmpty(name))
                            tagNames.Add(name);
                    }
                }
                if (tagNames.Count > 0)
                    _contentLines.Add("Tags: " + string.Join(", ", tagNames));
            }

            // 9. Recipes (for workshops)
            if (GameReflection.IsWorkshopModel(building))
            {
                BuildWorkshopRecipes(building);
            }

            // 10. Upgrades (for upgradable buildings)
            if (GameReflection.IsUpgradableBuildingModel(building) &&
                !GameReflection.GetHideUpgradesInWiki(building))
            {
                BuildUpgradeInfo(building);
            }
        }

        /// <summary>
        /// Build recipe list for a workshop building.
        /// </summary>
        private void BuildWorkshopRecipes(object workshop)
        {
            var recipes = GameReflection.GetWorkshopRecipes(workshop);
            if (recipes == null || recipes.Length == 0) return;

            _contentLines.Add("Recipes:");
            foreach (var recipe in recipes)
            {
                var outputName = GameReflection.GetRecipeOutputName(recipe);
                var outputAmount = GameReflection.GetRecipeOutputAmount(recipe);
                var productionTime = GameReflection.GetRecipeProductionTime(recipe);
                var gradeLevel = GameReflection.GetRecipeGradeLevel(recipe);

                // Build grade stars string
                var gradeStr = gradeLevel > 0 ? $" ({gradeLevel} star{(gradeLevel > 1 ? "s" : "")})" : "";

                // Build inputs string
                var inputParts = new List<string>();
                var requiredGoods = GameReflection.GetRecipeRequiredGoods(recipe);
                if (requiredGoods != null)
                {
                    foreach (var goodsSet in requiredGoods)
                    {
                        var goods = GameReflection.GetGoodsSetGoods(goodsSet);
                        if (goods != null && goods.Length > 0)
                        {
                            // Multiple goods in a set = alternatives (OR)
                            var alternatives = new List<string>();
                            foreach (var goodRef in goods)
                            {
                                var name = GameReflection.GetGoodRefDisplayName(goodRef);
                                var amount = GameReflection.GetGoodRefAmount(goodRef);
                                if (!string.IsNullOrEmpty(name))
                                    alternatives.Add($"{amount} {name}");
                            }
                            if (alternatives.Count > 0)
                                inputParts.Add(string.Join(" OR ", alternatives));
                        }
                    }
                }

                var inputs = inputParts.Count > 0 ? string.Join(" + ", inputParts) : "nothing";
                var time = FormatMinSec(productionTime);

                _contentLines.Add($"  {outputAmount} {outputName}{gradeStr}: {inputs} ({time})");
            }
        }

        /// <summary>
        /// Build upgrade information for an upgradable building.
        /// </summary>
        private void BuildUpgradeInfo(object building)
        {
            var levels = GameReflection.GetBuildingLevels(building);
            if (levels == null || levels.Length <= 1) return;  // Skip if only base level

            _contentLines.Add("Upgrades:");

            // Start from index 1 (level I), skip index 0 (base)
            for (int i = 1; i < levels.Length; i++)
            {
                var level = levels.GetValue(i);
                var levelNum = IntToRoman(i);  // I, II, III, etc.

                // Get upgrade cost
                var requiredGoods = GameReflection.GetLevelRequiredGoods(level);
                var costStr = FormatUpgradeCost(requiredGoods);

                _contentLines.Add($"  Level {levelNum}: {costStr}");

                // Get perk options
                var options = GameReflection.GetLevelOptions(level);
                if (options != null)
                {
                    foreach (var perk in options)
                    {
                        var name = GameReflection.GetPerkDisplayName(perk);
                        var amount = GameReflection.GetPerkAmountText(perk);
                        var desc = StripRichTextTags(GameReflection.GetPerkDescription(perk));

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
        /// Format the upgrade cost from an array of GoodsSet objects.
        /// </summary>
        private string FormatUpgradeCost(Array requiredGoods)
        {
            if (requiredGoods == null || requiredGoods.Length == 0) return "Free";

            var parts = new List<string>();
            foreach (var goodsSet in requiredGoods)
            {
                var goods = GameReflection.GetGoodsSetGoods(goodsSet);
                if (goods != null && goods.Length > 0)
                {
                    var alternatives = new List<string>();
                    foreach (var goodRef in goods)
                    {
                        var name = GameReflection.GetGoodRefDisplayName(goodRef);
                        var amount = GameReflection.GetGoodRefAmount(goodRef);
                        if (!string.IsNullOrEmpty(name))
                            alternatives.Add($"{amount} {name}");
                    }
                    if (alternatives.Count > 0)
                        parts.Add(string.Join(" OR ", alternatives));
                }
            }
            return parts.Count > 0 ? string.Join(" + ", parts) : "Free";
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
            return $"{mins:D2}:{secs:D2}";
        }

        /// <summary>
        /// Strip Unity rich text tags like &lt;sprite name=xxx&gt;, &lt;color&gt;, etc.
        /// </summary>
        private string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove all tags matching <xxx> or <xxx=yyy> or </xxx>
            return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "").Trim();
        }

        private string ExtractPreviewContent()
        {
            // Get the current panel
            var currentPanel = GameReflection.GetCurrentWikiPanel(_wikiPopup);
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

            if (_categoryIndex < 0 || _categoryIndex >= _categoryButtons.Count)
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

            if (_articleIndex < 0 || _articleIndex >= _articleSlots.Count)
                return;

            var slot = _articleSlots[_articleIndex];
            var comp = slot as Component;
            string name = comp != null ? UIElementFinder.GetTextFromTransform(comp.transform) : "Unknown";

            bool unlocked = GameReflection.IsWikiSlotUnlocked(slot);
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

            if (_contentLineIndex < 0 || _contentLineIndex >= _contentLines.Count)
                return;

            var line = _contentLines[_contentLineIndex];
            Speech.Say(line);
        }
    }
}
