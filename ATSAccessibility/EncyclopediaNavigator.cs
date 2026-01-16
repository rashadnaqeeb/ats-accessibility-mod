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
