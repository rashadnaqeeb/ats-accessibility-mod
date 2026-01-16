using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles discovery of UI panels and elements, plus text/type/state extraction.
    /// Static utility class used by UINavigator.
    /// </summary>
    public static class UIElementFinder
    {
        // Element name patterns to ignore (decorative, not useful for navigation)
        private static readonly string[] IgnoredElementNames = {
            "scrollbar", "background", "resize", "handle", "hide", "blend", "item", "template"
        };

        // Generic text patterns to ignore (not useful labels)
        private static readonly string[] GenericTexts = {
            "Toggle", "Slider", "Button", "Dropdown", "Option A", "Item"
        };

        // Cached reflection for DemoElement.inFullGame field
        private static System.Reflection.FieldInfo _demoElementInFullGameField;
        private static bool _demoElementFieldLookedUp;

        // ========================================
        // PANEL DISCOVERY
        // ========================================

        /// <summary>
        /// Find all panels within a popup or menu root.
        /// Returns panels list, tab buttons list, tabs panel reference, and whether it's tabbed.
        /// </summary>
        public static PanelDiscoveryResult DiscoverPanels(GameObject root, bool isPopup)
        {
            var result = new PanelDiscoveryResult();

            if (root == null) return result;

            // Try to detect tabbed popup structure
            if (isPopup && TrySetupTabsPanel(root, result))
            {
                Debug.Log($"[ATSAccessibility] Detected tabbed popup with {result.TabButtons.Count} tabs");
                return result;
            }

            // Standard panel detection
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var potentialPanels = new List<GameObject>();

            foreach (var t in allTransforms)
            {
                string name = t.name;
                if (name.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("section", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Skip panels that are inside an inactive nested popup
                    var owningPopup = FindOwningPopup(t, root.transform);
                    if (owningPopup != null && !owningPopup.activeInHierarchy)
                    {
                        continue;
                    }

                    var selectables = t.GetComponentsInChildren<Selectable>(true);
                    var validSelectables = selectables.Where(s =>
                        (isPopup || s.gameObject.activeInHierarchy) &&
                        s.interactable &&
                        !ShouldIgnoreElement(s)).ToList();

                    if (validSelectables.Count > 0)
                    {
                        potentialPanels.Add(t.gameObject);
                    }
                }
            }

            // Filter out nested panels (only keep top-level ones)
            foreach (var panel in potentialPanels)
            {
                bool isNested = potentialPanels.Any(other =>
                    other != panel && panel.transform.IsChildOf(other.transform));

                if (!isNested)
                {
                    result.Panels.Add(panel);
                }
            }

            // If no panels found, use the root itself as the single "panel"
            if (result.Panels.Count == 0)
            {
                result.Panels.Add(root);
            }

            Debug.Log($"[ATSAccessibility] Found {result.Panels.Count} panels");
            return result;
        }

        /// <summary>
        /// Find the owning popup of a component by walking up the hierarchy.
        /// Returns null if no intermediate popup found between component and root.
        /// </summary>
        private static GameObject FindOwningPopup(Transform t, Transform root)
        {
            var current = t.parent;
            while (current != null && current != root)
            {
                if (current.name.Contains("Popup"))
                    return current.gameObject;
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Try to detect TabsPanel/TabsButton structure.
        /// </summary>
        private static bool TrySetupTabsPanel(GameObject root, PanelDiscoveryResult result)
        {
            var tabsPanelType = GameReflection.TabsPanelType;
            if (tabsPanelType == null) return false;

            // Find TabsPanel component in popup
            var components = root.GetComponentsInChildren<Component>(true);
            object tabsPanel = null;

            foreach (var comp in components)
            {
                if (comp != null && comp.GetType() == tabsPanelType)
                {
                    // Check if this TabsPanel is inside a nested child popup that's inactive
                    var owningPopup = FindOwningPopup(comp.transform, root.transform);
                    if (owningPopup != null && !owningPopup.activeInHierarchy)
                    {
                        Debug.Log($"[ATSAccessibility] Skipping TabsPanel in inactive nested popup: {owningPopup.name}");
                        continue;
                    }

                    tabsPanel = comp;
                    break;
                }
            }

            if (tabsPanel == null) return false;

            Debug.Log($"[ATSAccessibility] DEBUG: Found TabsPanel: {((Component)tabsPanel).gameObject.name}");

            // Get buttons array from TabsPanel
            var buttonsField = GameReflection.TabsPanelButtonsField;
            var tabsButtonArray = buttonsField?.GetValue(tabsPanel) as Array;

            if (tabsButtonArray == null || tabsButtonArray.Length < 2) return false;

            // Get the Unity Button from each TabsButton
            var buttonField = GameReflection.TabsButtonButtonField;

            foreach (var tabsButton in tabsButtonArray)
            {
                if (tabsButton == null) continue;

                var unityButton = buttonField?.GetValue(tabsButton) as Button;
                if (unityButton != null)
                {
                    result.TabButtons.Add(unityButton);
                    var text = unityButton.GetComponentInChildren<TMP_Text>(true)?.text ?? "?";
                    Debug.Log($"[ATSAccessibility] DEBUG: Found tab button: '{text}'");
                }
            }

            if (result.TabButtons.Count < 2) return false;

            // Store TabsPanel reference for active content lookup
            result.TabsPanelRef = tabsPanel;
            result.IsTabbedPopup = true;
            result.Panels.Add(root); // Panel 0 = Tabs
            result.Panels.Add(root); // Panel 1 = Content

            Debug.Log("[ATSAccessibility] Detected TabsPanel-based tabs");
            return true;
        }

        /// <summary>
        /// Find the active content panel for the current tab.
        /// </summary>
        public static Transform FindActiveContentPanel(object tabsPanelRef)
        {
            if (tabsPanelRef == null) return null;

            try
            {
                var currentField = GameReflection.TabsPanelCurrentField;
                var currentTabsButton = currentField?.GetValue(tabsPanelRef);

                if (currentTabsButton != null)
                {
                    var contentField = GameReflection.TabsButtonContentField;
                    var content = contentField?.GetValue(currentTabsButton) as GameObject;

                    if (content != null)
                    {
                        Debug.Log($"[ATSAccessibility] DEBUG: Active tab content: {content.name}");
                        return content.transform;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] DEBUG: Error getting TabsPanel content: {ex.Message}");
            }

            return null;
        }

        // ========================================
        // ELEMENT DISCOVERY
        // ========================================

        /// <summary>
        /// Find all interactive elements within a panel.
        /// </summary>
        public static List<Selectable> FindElementsInPanel(
            GameObject panel,
            bool isPopup,
            bool isTabbedPopup,
            int panelIndex,
            List<Selectable> tabButtons,
            object tabsPanelRef)
        {
            var elements = new List<Selectable>();

            if (panel == null) return elements;

            // Handle tabbed popup specially
            if (isTabbedPopup)
            {
                if (panelIndex == 0)
                {
                    // Panel 0: Tabs only
                    foreach (var tab in tabButtons)
                    {
                        if (tab != null)
                        {
                            elements.Add(tab);
                        }
                    }
                    Debug.Log($"[ATSAccessibility] Found {elements.Count} tab buttons");
                }
                else
                {
                    // Panel 1: Content (only from active content panel, excluding tabs)
                    var activeContentPanel = FindActiveContentPanel(tabsPanelRef);
                    Transform searchRoot = activeContentPanel ?? panel.transform;

                    var selectables = searchRoot.GetComponentsInChildren<Selectable>(true);
                    Debug.Log($"[ATSAccessibility] DEBUG: Searching for content in '{searchRoot.name}' ({selectables.Length} selectables)");

                    foreach (var sel in selectables)
                    {
                        if (!sel.gameObject.activeInHierarchy) continue;
                        if (!sel.interactable) continue;
                        if (ShouldIgnoreElement(sel)) continue;
                        if (tabButtons.Contains(sel)) continue;

                        string text = GetElementText(sel);
                        if (string.IsNullOrEmpty(text)) continue;

                        elements.Add(sel);
                    }
                    Debug.Log($"[ATSAccessibility] Found {elements.Count} content elements in active panel");
                }
                return elements;
            }

            // Standard (non-tabbed) panel handling
            var allSelectables = panel.GetComponentsInChildren<Selectable>(true);

            foreach (var sel in allSelectables)
            {
                if (ShouldSkipElement(sel, panel.transform, isPopup)) continue;

                string text = GetElementText(sel);
                if (string.IsNullOrEmpty(text)) continue;

                elements.Add(sel);
            }

            Debug.Log($"[ATSAccessibility] Found {elements.Count} elements in panel {panel.name}");
            return elements;
        }

        /// <summary>
        /// Unified check for whether to skip an element during navigation.
        /// Consolidates visibility, interactability, and filter checks.
        /// </summary>
        private static bool ShouldSkipElement(Selectable sel, Transform boundary, bool isPopup)
        {
            // For menus, skip inactive elements (check full hierarchy)
            // For popups, allow animated elements (parent inactive) but skip directly deactivated
            if (!isPopup && !sel.gameObject.activeInHierarchy) return true;
            if (isPopup && !sel.gameObject.activeSelf) return true;

            // For popups, check hidden/demo-only elements (single hierarchy walk)
            if (isPopup)
            {
                var (filtered, reason) = IsElementFiltered(sel.transform, boundary);
                if (filtered)
                {
                    Debug.Log($"[ATSAccessibility] DEBUG: Skipping '{sel.name}' - {reason}");
                    return true;
                }
            }

            if (!sel.interactable) return true;
            if (ShouldIgnoreElement(sel)) return true;

            return false;
        }

        /// <summary>
        /// Check if element should be filtered out due to being hidden or demo-only.
        /// Combines hierarchy walk for: deactivated, CanvasGroup alpha=0, and DemoElement.
        /// Returns (isFiltered, reason) where reason is for debug logging.
        /// </summary>
        private static (bool filtered, string reason) IsElementFiltered(Transform t, Transform boundary)
        {
            var current = t;
            while (current != null && current != boundary)
            {
                // Check if GameObject is deactivated
                if (!current.gameObject.activeSelf)
                    return (true, "hidden (inactive)");

                // Check if CanvasGroup is hiding this element (alpha = 0)
                var canvasGroup = current.GetComponent<CanvasGroup>();
                if (canvasGroup != null && canvasGroup.alpha < 0.01f)
                    return (true, "hidden (alpha=0)");

                // Check for DemoElement component (demo-only UI)
                var components = current.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "DemoElement")
                    {
                        // Cache the FieldInfo on first lookup
                        if (!_demoElementFieldLookedUp)
                        {
                            _demoElementInFullGameField = comp.GetType().GetField("inFullGame",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            _demoElementFieldLookedUp = true;
                        }

                        if (_demoElementInFullGameField != null)
                        {
                            bool inFullGame = (bool)_demoElementInFullGameField.GetValue(comp);
                            if (!inFullGame)
                                return (true, "demo-only");
                        }
                    }
                }

                current = current.parent;
            }
            return (false, null);
        }

        /// <summary>
        /// Check if an element should be ignored (decorative).
        /// </summary>
        public static bool ShouldIgnoreElement(Selectable element)
        {
            string name = element.gameObject.name;
            foreach (var ignored in IgnoredElementNames)
            {
                if (name.IndexOf(ignored, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ========================================
        // TEXT EXTRACTION
        // ========================================

        /// <summary>
        /// Get the primary text content of a UI element.
        /// </summary>
        public static string GetElementText(Selectable element)
        {
            if (element == null) return null;

            // Handle dropdowns FIRST - their inner text is the value, not a label
            if (element is TMP_Dropdown dropdown && dropdown.options.Count > 0)
            {
                string value = dropdown.options[dropdown.value].text;
                string label = FindLabelFromParent(element.transform);
                if (!string.IsNullOrEmpty(label))
                {
                    return $"{label}: {value}";
                }
                return value;
            }

            // Handle sliders - their inner text might just be the value
            if (element is Slider slider)
            {
                string label = FindLabelFromParent(element.transform);
                if (!string.IsNullOrEmpty(label))
                {
                    return label;
                }
                var tmpText = element.GetComponentInChildren<TMP_Text>();
                if (tmpText != null && !string.IsNullOrEmpty(tmpText.text) && !IsNumericText(tmpText.text))
                {
                    return tmpText.text;
                }
                return CleanObjectName(element.gameObject.name);
            }

            // Try TextMeshPro first (most common)
            var innerTmpText = element.GetComponentInChildren<TMP_Text>();
            if (innerTmpText != null && !string.IsNullOrEmpty(innerTmpText.text))
            {
                string innerText = innerTmpText.text;
                if (!IsGenericText(innerText))
                {
                    return innerText;
                }
            }

            // Try legacy UI Text
            var uiText = element.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                string innerText = uiText.text;
                if (!IsGenericText(innerText))
                {
                    return innerText;
                }
            }

            // For input fields, get text or placeholder
            if (element is TMP_InputField inputField)
            {
                if (!string.IsNullOrEmpty(inputField.text))
                    return inputField.text;
                if (inputField.placeholder != null)
                {
                    var placeholder = inputField.placeholder.GetComponent<TMP_Text>();
                    if (placeholder != null)
                        return placeholder.text;
                }
            }

            // Try to find label from parent/sibling
            string parentLabel = FindLabelFromParent(element.transform);
            if (!string.IsNullOrEmpty(parentLabel))
            {
                return parentLabel;
            }

            // Fallback to cleaned object name
            return CleanObjectName(element.gameObject.name);
        }

        /// <summary>
        /// Find a label by looking at parent container's children (siblings of the element).
        /// </summary>
        private static string FindLabelFromParent(Transform elementTransform)
        {
            var parent = elementTransform.parent;
            if (parent == null) return null;

            // Look for sibling TMP_Text elements that could be labels
            foreach (Transform sibling in parent)
            {
                if (sibling == elementTransform) continue;
                if (sibling.GetComponent<Selectable>() != null) continue;

                var labelText = sibling.GetComponent<TMP_Text>();
                if (labelText != null && !string.IsNullOrEmpty(labelText.text) && !IsGenericText(labelText.text))
                {
                    return labelText.text;
                }

                var childText = sibling.GetComponentInChildren<TMP_Text>();
                if (childText != null && !string.IsNullOrEmpty(childText.text) && !IsGenericText(childText.text))
                {
                    return childText.text;
                }

                var legacyText = sibling.GetComponent<Text>();
                if (legacyText != null && !string.IsNullOrEmpty(legacyText.text) && !IsGenericText(legacyText.text))
                {
                    return legacyText.text;
                }
            }

            // Try grandparent if parent didn't have labels
            var grandparent = parent.parent;
            if (grandparent != null)
            {
                foreach (Transform uncle in grandparent)
                {
                    if (uncle == parent) continue;
                    if (uncle.GetComponent<Selectable>() != null) continue;

                    var labelText = uncle.GetComponent<TMP_Text>();
                    if (labelText != null && !string.IsNullOrEmpty(labelText.text) && !IsGenericText(labelText.text))
                    {
                        return labelText.text;
                    }
                }
            }

            return null;
        }

        private static bool IsNumericText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return float.TryParse(text, out _);
        }

        private static bool IsGenericText(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            foreach (var generic in GenericTexts)
            {
                if (text.Equals(generic, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Convert object name to readable text.
        /// </summary>
        public static string CleanObjectName(string name)
        {
            name = Regex.Replace(name, @"^(btn_|button_|txt_|text_|lbl_|label_)", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"(_btn|_button|_txt|_text|_lbl|_label)$", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2");
            name = name.Replace("_", " ");
            return name.Trim();
        }

        // ========================================
        // ELEMENT SEMANTICS
        // ========================================

        /// <summary>
        /// Get the semantic type of a UI element.
        /// </summary>
        public static string GetElementType(Selectable element)
        {
            if (element is Button)
                return "button";

            if (element is Toggle toggle)
            {
                if (toggle.group != null)
                    return "radio button";
                return "checkbox";
            }

            if (element is Slider)
                return "slider";

            if (element is TMP_Dropdown || element is Dropdown)
                return "dropdown";

            if (element is TMP_InputField || element is InputField)
                return "text field";

            return "control";
        }

        /// <summary>
        /// Get the current state of a UI element.
        /// </summary>
        public static string GetElementState(Selectable element)
        {
            if (element is Toggle toggle)
            {
                return toggle.isOn ? "checked" : "unchecked";
            }

            if (element is Slider slider)
            {
                int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                return $"{percent} percent";
            }

            return null;
        }
    }

    /// <summary>
    /// Result of panel discovery operation.
    /// </summary>
    public class PanelDiscoveryResult
    {
        public List<GameObject> Panels { get; } = new List<GameObject>();
        public List<Selectable> TabButtons { get; } = new List<Selectable>();
        public object TabsPanelRef { get; set; }
        public bool IsTabbedPopup { get; set; }
    }
}
