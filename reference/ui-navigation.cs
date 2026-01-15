// =============================================================================
// UI NAVIGATION REFERENCE - Patterns for navigating Unity UI elements
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Reference
{
    /// <summary>
    /// Patterns for discovering and navigating UI elements
    /// </summary>
    public static class UINavigationReference
    {
        // =====================================================================
        // ELEMENT DISCOVERY - Find all interactive elements
        // =====================================================================

        /// <summary>
        /// Find all interactive UI elements, optionally filtered to a container
        /// </summary>
        public static List<Selectable> FindInteractiveElements(GameObject container = null)
        {
            Selectable[] allSelectables;

            if (container != null)
            {
                allSelectables = container.GetComponentsInChildren<Selectable>(false);
            }
            else
            {
                allSelectables = Object.FindObjectsOfType<Selectable>();
            }

            var result = new List<Selectable>();

            foreach (var selectable in allSelectables)
            {
                // Skip inactive or non-interactable
                if (!selectable.gameObject.activeInHierarchy) continue;
                if (!selectable.interactable) continue;

                // Skip elements that are just decoration
                if (ShouldIgnoreElement(selectable)) continue;

                // Skip elements with no meaningful text
                string text = GetElementText(selectable);
                if (string.IsNullOrEmpty(text)) continue;

                result.Add(selectable);
            }

            return result;
        }

        /// <summary>
        /// Element names to ignore (decorative, not useful for navigation)
        /// </summary>
        private static readonly string[] IgnoredPatterns = new[]
        {
            "scrollbar", "scroll", "hide", "blend", "resize", "grip", "handle",
            "background", "bg", "border", "shadow", "glow", "highlight", "outline",
            "blocker", "template", "viewport", "mask", "spacer", "separator", "divider",
            "arrow", "caret"
        };

        private static bool ShouldIgnoreElement(Selectable selectable)
        {
            string name = selectable.gameObject.name.ToLower();

            foreach (var pattern in IgnoredPatterns)
            {
                if (name.Contains(pattern)) return true;
            }

            return false;
        }

        // =====================================================================
        // TEXT EXTRACTION - Get readable text from UI elements
        // =====================================================================

        /// <summary>
        /// Get the primary text content of a UI element
        /// </summary>
        public static string GetElementText(Selectable selectable)
        {
            if (selectable == null) return null;

            // Try TextMeshPro first (more common in modern Unity)
            var tmpText = selectable.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
            {
                return CleanText(tmpText.text);
            }

            // Try legacy UI Text
            var uiText = selectable.GetComponentInChildren<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
            {
                return CleanText(uiText.text);
            }

            // For dropdowns, get current selection
            var dropdown = selectable as TMPro.TMP_Dropdown;
            if (dropdown != null && dropdown.options.Count > 0)
            {
                return dropdown.options[dropdown.value].text;
            }

            // For input fields, get placeholder or current text
            var inputField = selectable as TMPro.TMP_InputField;
            if (inputField != null)
            {
                if (!string.IsNullOrEmpty(inputField.text))
                    return inputField.text;
                if (inputField.placeholder != null)
                {
                    var placeholder = inputField.placeholder.GetComponent<TMPro.TMP_Text>();
                    if (placeholder != null)
                        return placeholder.text;
                }
            }

            // Fall back to cleaned object name
            return CleanObjectName(selectable.gameObject.name);
        }

        /// <summary>
        /// Clean text by removing rich text tags and extra whitespace
        /// </summary>
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove rich text tags like <color>, <b>, <size>, etc.
            text = Regex.Replace(text, "<[^>]+>", "");

            // Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Convert object name to readable text
        /// </summary>
        private static string CleanObjectName(string name)
        {
            // Remove common prefixes/suffixes
            name = Regex.Replace(name, @"^(btn_|button_|txt_|text_|lbl_|label_)", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"(_btn|_button|_txt|_text|_lbl|_label)$", "", RegexOptions.IgnoreCase);

            // Convert PascalCase/camelCase to spaces
            name = Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2");

            // Convert underscores to spaces
            name = name.Replace("_", " ");

            return name.Trim();
        }

        // =====================================================================
        // ELEMENT TYPE & STATE - Semantic information for screen readers
        // =====================================================================

        /// <summary>
        /// Get a screen-reader-friendly description of an element
        /// </summary>
        public static string GetAccessibleDescription(Selectable selectable)
        {
            string text = GetElementText(selectable);
            string type = GetElementType(selectable);
            string state = GetElementState(selectable);

            var parts = new List<string>();

            if (!string.IsNullOrEmpty(text))
                parts.Add(text);

            if (!string.IsNullOrEmpty(type))
                parts.Add(type);

            if (!string.IsNullOrEmpty(state))
                parts.Add(state);

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Get the semantic type of a UI element
        /// </summary>
        public static string GetElementType(Selectable selectable)
        {
            if (selectable is Button)
                return "button";

            if (selectable is Toggle toggle)
            {
                // Check if it's part of a toggle group (radio button)
                if (toggle.group != null)
                    return "radio button";
                return "checkbox";
            }

            if (selectable is Slider)
                return "slider";

            if (selectable is TMPro.TMP_Dropdown || selectable is Dropdown)
                return "dropdown";

            if (selectable is TMPro.TMP_InputField || selectable is InputField)
                return "text field";

            if (selectable is Scrollbar)
                return "scrollbar";

            return "control";
        }

        /// <summary>
        /// Get the current state of a UI element
        /// </summary>
        public static string GetElementState(Selectable selectable)
        {
            // Toggle state
            if (selectable is Toggle toggle)
            {
                return toggle.isOn ? "checked" : "unchecked";
            }

            // Slider value
            if (selectable is Slider slider)
            {
                int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                return $"{percent} percent";
            }

            // Dropdown selection
            if (selectable is TMPro.TMP_Dropdown dropdown)
            {
                if (dropdown.options.Count > 0 && dropdown.value < dropdown.options.Count)
                {
                    return dropdown.options[dropdown.value].text;
                }
            }

            // Input field content
            if (selectable is TMPro.TMP_InputField inputField)
            {
                if (string.IsNullOrEmpty(inputField.text))
                    return "empty";
                return $"contains: {inputField.text}";
            }

            return null;
        }

        // =====================================================================
        // PANEL/TAB DETECTION - Group elements by container
        // =====================================================================

        /// <summary>
        /// Find the containing panel for an element
        /// </summary>
        public static GameObject FindContainingPanel(GameObject element)
        {
            var current = element.transform;

            while (current != null)
            {
                string name = current.name.ToLower();

                // Look for panel-like containers
                if (name.Contains("panel") ||
                    name.Contains("content") ||
                    name.Contains("section") ||
                    name.Contains("container") ||
                    name.Contains("window") ||
                    name.Contains("dialog") ||
                    name.Contains("popup"))
                {
                    return current.gameObject;
                }

                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Group elements by their containing panel
        /// </summary>
        public static Dictionary<GameObject, List<Selectable>> GroupByPanel(List<Selectable> elements)
        {
            var groups = new Dictionary<GameObject, List<Selectable>>();

            foreach (var element in elements)
            {
                var panel = FindContainingPanel(element.gameObject);
                if (panel == null)
                {
                    // Create a "root" group for elements without panels
                    panel = element.transform.root.gameObject;
                }

                if (!groups.ContainsKey(panel))
                {
                    groups[panel] = new List<Selectable>();
                }

                groups[panel].Add(element);
            }

            return groups;
        }

        // =====================================================================
        // TAB CONTENT FILTERING - Only show elements in active tab
        // =====================================================================

        /// <summary>
        /// Filter elements to only those in the active tab content
        /// </summary>
        public static List<Selectable> FilterToActiveTab(List<Selectable> elements, GameObject activeTabContent)
        {
            if (activeTabContent == null) return elements;

            return elements.Where(e => e.transform.IsChildOf(activeTabContent.transform)).ToList();
        }

        /// <summary>
        /// Check if element is in a "shared" area (always visible regardless of tab)
        /// </summary>
        public static bool IsInSharedArea(Selectable element)
        {
            var current = element.transform;

            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("shared") || name.Contains("tabs") || name.Contains("header"))
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        // =====================================================================
        // ELEMENT ACTIVATION - Programmatically interact with elements
        // =====================================================================

        /// <summary>
        /// Activate/click a UI element
        /// </summary>
        public static void ActivateElement(Selectable selectable)
        {
            if (selectable is Button button)
            {
                button.onClick.Invoke();
            }
            else if (selectable is Toggle toggle)
            {
                toggle.isOn = !toggle.isOn;
            }
            else if (selectable is TMPro.TMP_Dropdown dropdown)
            {
                dropdown.Show();
            }
            else if (selectable is Slider slider)
            {
                // For sliders, Select() focuses for keyboard control
                slider.Select();
            }
        }

        /// <summary>
        /// Adjust a slider value
        /// </summary>
        public static void AdjustSlider(Slider slider, float delta)
        {
            float range = slider.maxValue - slider.minValue;
            float step = range * delta; // delta is typically 0.1 for 10% adjustment
            slider.value = Mathf.Clamp(slider.value + step, slider.minValue, slider.maxValue);
        }
    }
}
