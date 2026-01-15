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
    /// Handles UI navigation within popups/menus.
    /// Uses panel-based hierarchy: Left/Right switches panels, Up/Down cycles elements.
    /// </summary>
    public class UINavigator
    {
        // Element name patterns to ignore (decorative, not useful for navigation)
        private static readonly string[] IgnoredElementNames = {
            "scrollbar", "background", "resize", "handle", "hide", "blend", "item", "template"
        };

        // Current popup being navigated (compare references for changes)
        private GameObject _currentPopup = null;

        // Current menu being navigated (for Canvas-based menus like main menu)
        private GameObject _currentMenu = null;

        // Panel navigation
        private List<GameObject> _panels = new List<GameObject>();
        private int _currentPanelIndex = 0;

        // Element navigation within current panel
        private List<Selectable> _elements = new List<Selectable>();
        private int _currentElementIndex = 0;

        // Tabbed popup support
        private bool _isTabbedPopup = false;
        private List<Selectable> _tabButtons = new List<Selectable>();

        // Tab detection state
        private object _tabsPanelRef = null;                    // Reference to TabsPanel component

        /// <summary>
        /// Whether there's an active popup being navigated.
        /// </summary>
        public bool HasActivePopup => _currentPopup != null;

        /// <summary>
        /// Whether there's an active menu being navigated.
        /// </summary>
        public bool HasActiveMenu => _currentMenu != null;

        /// <summary>
        /// Whether navigation is active (popup or menu).
        /// </summary>
        public bool IsNavigationActive => _currentPopup != null || _currentMenu != null;

        /// <summary>
        /// Called when a popup is shown. Compares references to detect actual changes.
        /// </summary>
        public void OnPopupShown(object popup)
        {
            Debug.Log($"[ATSAccessibility] DEBUG: OnPopupShown called with: {popup?.GetType().FullName ?? "null"}");

            var popupGO = GetGameObjectFromPopup(popup);
            if (popupGO == null)
            {
                Debug.LogWarning("[ATSAccessibility] DEBUG: Could not get GameObject from popup");
                return;
            }

            Debug.Log($"[ATSAccessibility] DEBUG: Popup GameObject: {popupGO.name}");

            // Compare references - not just event firing
            if (popupGO != _currentPopup)
            {
                Debug.Log($"[ATSAccessibility] New popup opened: {popupGO.name}");

                // Different popup - full reset
                _currentPopup = popupGO;
                _currentPanelIndex = 0;
                _currentElementIndex = 0;

                RebuildNavigation();
                AnnouncePopup();
            }
            else
            {
                Debug.Log("[ATSAccessibility] DEBUG: Same popup as before, ignoring");
            }
        }

        /// <summary>
        /// Called when a popup is hidden.
        /// </summary>
        public void OnPopupHidden(object popup)
        {
            var popupGO = GetGameObjectFromPopup(popup);
            if (popupGO == null) return;

            // Only clear if THIS popup closed (not a stacked one)
            if (popupGO == _currentPopup)
            {
                Debug.Log($"[ATSAccessibility] Popup closed: {popupGO.name}");
                ResetPopup();
            }
        }

        /// <summary>
        /// Set up navigation for a Canvas-based menu (like main menu).
        /// </summary>
        public void SetupMenuNavigation(GameObject menuRoot, string menuName = null)
        {
            if (menuRoot == null) return;

            // Don't override popup navigation
            if (_currentPopup != null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: Popup active, not setting up menu navigation");
                return;
            }

            // Compare references to detect actual changes
            // But always rebuild if elements were cleared (e.g., by a popup)
            if (menuRoot == _currentMenu && _elements.Count > 0)
            {
                return;
            }

            Debug.Log($"[ATSAccessibility] Setting up menu navigation: {menuRoot.name}");

            _currentMenu = menuRoot;
            _currentPanelIndex = 0;
            _currentElementIndex = 0;

            // For menus, skip panel detection - use root as single panel
            // This finds ALL active selectables instead of missing buttons in oddly-named containers
            _panels.Clear();
            _panels.Add(menuRoot);
            RebuildElementsForCurrentPanel();

            // Announce menu
            string name = menuName ?? CleanObjectName(menuRoot.name);
            if (_elements.Count > 0)
            {
                Debug.Log($"[ATSAccessibility] Menu '{name}' has {_elements.Count} elements");
                AnnounceCurrentElement();
            }
            else
            {
                Debug.Log($"[ATSAccessibility] Menu '{name}' has no navigable elements");
            }
        }

        /// <summary>
        /// Clear menu navigation (when leaving menu scene or popup opens).
        /// </summary>
        public void ClearMenuNavigation()
        {
            if (_currentMenu != null)
            {
                Debug.Log($"[ATSAccessibility] Clearing menu navigation: {_currentMenu.name}");
                _currentMenu = null;

                // Only clear elements if no popup is active
                if (_currentPopup == null)
                {
                    _panels.Clear();
                    _elements.Clear();
                    _currentPanelIndex = 0;
                    _currentElementIndex = 0;
                }
            }
        }

        /// <summary>
        /// Reset all navigation state.
        /// </summary>
        public void Reset()
        {
            _currentPopup = null;
            _currentMenu = null;
            _panels.Clear();
            _elements.Clear();
            _tabButtons.Clear();
            _isTabbedPopup = false;
            _tabsPanelRef = null;
            _currentPanelIndex = 0;
            _currentElementIndex = 0;
        }

        /// <summary>
        /// Reset popup navigation only (preserves menu if active).
        /// </summary>
        private void ResetPopup()
        {
            _currentPopup = null;
            _panels.Clear();
            _elements.Clear();
            _tabButtons.Clear();
            _isTabbedPopup = false;
            _tabsPanelRef = null;
            _currentPanelIndex = 0;
            _currentElementIndex = 0;
        }

        /// <summary>
        /// Navigate between panels (Left/Right arrows).
        /// </summary>
        public void NavigatePanel(int direction)
        {
            if (_panels.Count <= 1) return;

            _currentPanelIndex = (_currentPanelIndex + direction + _panels.Count) % _panels.Count;
            _currentElementIndex = 0;

            RebuildElementsForCurrentPanel();
            AnnouncePanelName();
        }

        /// <summary>
        /// Navigate between elements within current panel (Up/Down arrows).
        /// </summary>
        public void NavigateElement(int direction)
        {
            Debug.Log($"[ATSAccessibility] DEBUG: NavigateElement called, direction={direction}, elementCount={_elements.Count}");

            if (_elements.Count == 0)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No elements to navigate");
                return;
            }

            _currentElementIndex = (_currentElementIndex + direction + _elements.Count) % _elements.Count;
            Debug.Log($"[ATSAccessibility] DEBUG: New element index: {_currentElementIndex}");
            AnnounceCurrentElement();
        }

        /// <summary>
        /// Activate the current element (Enter/Space).
        /// </summary>
        public void ActivateCurrentElement()
        {
            if (_elements.Count == 0 || _currentElementIndex >= _elements.Count) return;

            var element = _elements[_currentElementIndex];
            if (element == null) return;

            try
            {
                if (element is Button button)
                {
                    button.onClick.Invoke();
                    Debug.Log($"[ATSAccessibility] Activated button: {GetElementText(element)}");
                }
                else if (element is Toggle toggle)
                {
                    toggle.isOn = !toggle.isOn;
                    string state = toggle.isOn ? "checked" : "unchecked";
                    Speech.Say(state);
                    Debug.Log($"[ATSAccessibility] Toggled: {GetElementText(element)} -> {state}");
                }
                else if (element is TMP_Dropdown dropdown)
                {
                    dropdown.Show();
                    Debug.Log($"[ATSAccessibility] Opened dropdown: {GetElementText(element)}");
                }
                else if (element is Slider slider)
                {
                    // For sliders, just focus them - user can then use arrow keys
                    slider.Select();
                    Debug.Log($"[ATSAccessibility] Selected slider: {GetElementText(element)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to activate element: {ex.Message}");
            }
        }

        /// <summary>
        /// Dismiss the current popup (Escape key).
        /// Looks for close/hide/back buttons or BackableButton components.
        /// </summary>
        public void DismissPopup()
        {
            if (_currentPopup == null) return;

            try
            {
                // Strategy 1: Look for BackableButton component (game's close button pattern)
                var backableButtons = _currentPopup.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && c.GetType().Name == "BackableButton")
                    .ToList();

                if (backableButtons.Count > 0)
                {
                    // BackableButton is on a Button - get the Button and click it
                    var button = backableButtons[0].GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.Invoke();
                        Debug.Log($"[ATSAccessibility] Dismissed popup via BackableButton");
                        return;
                    }
                }

                // Strategy 2: Look for buttons with close/hide/back/cancel in name
                var closeButtons = _currentPopup.GetComponentsInChildren<Button>(true)
                    .Where(b => {
                        string name = b.gameObject.name.ToLower();
                        return name.Contains("close") || name.Contains("hide") ||
                               name.Contains("back") || name.Contains("cancel") ||
                               name.Contains("exit") || name.Contains("dismiss");
                    })
                    .ToList();

                if (closeButtons.Count > 0)
                {
                    closeButtons[0].onClick.Invoke();
                    Debug.Log($"[ATSAccessibility] Dismissed popup via close button: {closeButtons[0].name}");
                    return;
                }

                // Strategy 3: Look for "Blend" button (full-screen dismiss area)
                var blendButton = _currentPopup.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(b => b.gameObject.name.ToLower() == "blend");

                if (blendButton != null)
                {
                    blendButton.onClick.Invoke();
                    Debug.Log($"[ATSAccessibility] Dismissed popup via Blend button");
                    return;
                }

                Debug.Log("[ATSAccessibility] No dismiss button found for popup");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to dismiss popup: {ex.Message}");
            }
        }

        // ========================================
        // PRIVATE HELPERS
        // ========================================

        /// <summary>
        /// Convert popup object to GameObject.
        /// Popup inherits from MonoBehaviour via MB.
        /// </summary>
        private GameObject GetGameObjectFromPopup(object popup)
        {
            var component = popup as Component;
            return component?.gameObject;
        }

        /// <summary>
        /// Rebuild all navigation data for the current popup.
        /// </summary>
        private void RebuildNavigation()
        {
            RebuildPanels();
            RebuildElementsForCurrentPanel();
        }

        /// <summary>
        /// Find all panels within the current popup or menu.
        /// For tabbed popups, creates Panel 0 (tabs) and Panel 1 (content).
        /// </summary>
        private void RebuildPanels()
        {
            Debug.Log("[ATSAccessibility] DEBUG: RebuildPanels starting");
            _panels.Clear();
            _tabButtons.Clear();
            _isTabbedPopup = false;

            // Use popup if active, otherwise use menu
            var root = _currentPopup ?? _currentMenu;
            if (root == null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: RebuildPanels - no active popup or menu");
                return;
            }

            // Try to detect tabbed popup structure
            if (_currentPopup != null && TrySetupTabbedPopup(root))
            {
                Debug.Log($"[ATSAccessibility] Detected tabbed popup with {_tabButtons.Count} tabs");
                return;
            }

            // Standard panel detection (original behavior)
            // Find transforms that look like panels (include inactive - popup may be animating in)
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            Debug.Log($"[ATSAccessibility] DEBUG: Found {allTransforms.Length} transforms in {(_currentPopup != null ? "popup" : "menu")}");

            var potentialPanels = new List<GameObject>();

            foreach (var t in allTransforms)
            {
                string name = t.name.ToLower();
                if (name.Contains("panel") || name.Contains("content") || name.Contains("section"))
                {
                    // For menus, only count active elements; for popups, include inactive (animating)
                    var selectables = t.GetComponentsInChildren<Selectable>(true);
                    var validSelectables = selectables.Where(s =>
                        (_currentPopup != null || s.gameObject.activeInHierarchy) &&
                        s.interactable &&
                        !ShouldIgnoreElement(s)).ToList();
                    Debug.Log($"[ATSAccessibility] DEBUG: Panel candidate '{t.name}' has {validSelectables.Count} valid selectables");
                    if (validSelectables.Count > 0)
                    {
                        potentialPanels.Add(t.gameObject);
                    }
                }
            }

            Debug.Log($"[ATSAccessibility] DEBUG: Found {potentialPanels.Count} potential panels before nesting filter");

            // Filter out nested panels (only keep top-level ones)
            foreach (var panel in potentialPanels)
            {
                bool isNested = potentialPanels.Any(other =>
                    other != panel && panel.transform.IsChildOf(other.transform));

                if (!isNested)
                {
                    _panels.Add(panel);
                    Debug.Log($"[ATSAccessibility] DEBUG: Added panel: {panel.name}");
                }
            }

            // If no panels found, use the root itself as the single "panel"
            if (_panels.Count == 0)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No panels found, using root as panel");
                _panels.Add(root);
            }

            Debug.Log($"[ATSAccessibility] Found {_panels.Count} panels");
        }

        /// <summary>
        /// Try to set up a tabbed popup structure using TabsPanel/TabsButton detection.
        /// </summary>
        private bool TrySetupTabbedPopup(GameObject root)
        {
            // Reset tab state
            _tabsPanelRef = null;

            // Try TabsPanel detection (game's built-in tab system)
            if (TrySetupTabsPanelPopup(root))
            {
                Debug.Log("[ATSAccessibility] Detected TabsPanel-based tabs");
                return true;
            }

            Debug.Log("[ATSAccessibility] DEBUG: No tabbed structure detected");
            return false;
        }

        /// <summary>
        /// Detect tabs using game's TabsPanel/TabsButton system.
        /// </summary>
        private bool TrySetupTabsPanelPopup(GameObject root)
        {
            var tabsPanelType = GameReflection.TabsPanelType;
            if (tabsPanelType == null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: TabsPanel type not found");
                return false;
            }

            // Find TabsPanel component in popup
            var components = root.GetComponentsInChildren<Component>(true);
            object tabsPanel = null;

            foreach (var comp in components)
            {
                if (comp != null && comp.GetType() == tabsPanelType)
                {
                    tabsPanel = comp;
                    break;
                }
            }

            if (tabsPanel == null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No TabsPanel component found");
                return false;
            }

            Debug.Log($"[ATSAccessibility] DEBUG: Found TabsPanel: {((Component)tabsPanel).gameObject.name}");

            // Get buttons array from TabsPanel
            var buttonsField = GameReflection.TabsPanelButtonsField;
            var tabsButtonArray = buttonsField?.GetValue(tabsPanel) as Array;

            if (tabsButtonArray == null || tabsButtonArray.Length < 2)
            {
                Debug.Log($"[ATSAccessibility] DEBUG: TabsPanel has {tabsButtonArray?.Length ?? 0} buttons, need at least 2");
                return false;
            }

            // Get the Unity Button from each TabsButton
            var buttonField = GameReflection.TabsButtonButtonField;
            _tabButtons.Clear();

            foreach (var tabsButton in tabsButtonArray)
            {
                if (tabsButton == null) continue;

                var unityButton = buttonField?.GetValue(tabsButton) as Button;
                if (unityButton != null)
                {
                    _tabButtons.Add(unityButton);
                    var text = unityButton.GetComponentInChildren<TMP_Text>(true)?.text ?? "?";
                    Debug.Log($"[ATSAccessibility] DEBUG: Found tab button via TabsPanel: '{text}'");
                }
            }

            if (_tabButtons.Count < 2)
            {
                Debug.Log($"[ATSAccessibility] DEBUG: Only {_tabButtons.Count} valid tab buttons found");
                return false;
            }

            // Store TabsPanel reference for active content lookup
            _tabsPanelRef = tabsPanel;
            _isTabbedPopup = true;

            _panels.Add(root); // Panel 0 = Tabs
            _panels.Add(root); // Panel 1 = Content

            Debug.Log($"[ATSAccessibility] DEBUG: Set up TabsPanel popup with {_tabButtons.Count} tabs");
            return true;
        }

        /// <summary>
        /// Find the active content panel for the current tab using TabsPanel.current.content.
        /// </summary>
        private Transform FindActiveContentPanel(GameObject root)
        {
            if (_tabsPanelRef == null) return null;

            try
            {
                var currentField = GameReflection.TabsPanelCurrentField;
                var currentTabsButton = currentField?.GetValue(_tabsPanelRef);

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

            Debug.Log("[ATSAccessibility] DEBUG: No active content panel found");
            return null;
        }

        /// <summary>
        /// Find all interactive elements within the current panel.
        /// For tabbed popups: Panel 0 = tabs only, Panel 1 = content only.
        /// </summary>
        private void RebuildElementsForCurrentPanel()
        {
            Debug.Log("[ATSAccessibility] DEBUG: RebuildElementsForCurrentPanel starting");
            _elements.Clear();
            _currentElementIndex = 0;

            if (_panels.Count == 0)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No panels, cannot rebuild elements");
                return;
            }

            var panel = _panels[_currentPanelIndex];
            string panelLabel = _isTabbedPopup ? (_currentPanelIndex == 0 ? "Tabs" : "Content") : panel.name;
            Debug.Log($"[ATSAccessibility] DEBUG: Rebuilding elements for panel '{panelLabel}' (index {_currentPanelIndex}, tabbed={_isTabbedPopup})");

            // Handle tabbed popup specially
            if (_isTabbedPopup)
            {
                if (_currentPanelIndex == 0)
                {
                    // Panel 0: Tabs only
                    // Include tabs even if not fully active (popup animation)
                    foreach (var tab in _tabButtons)
                    {
                        if (tab != null)
                        {
                            string text = GetElementText(tab);
                            Debug.Log($"[ATSAccessibility] DEBUG: Adding tab '{tab.gameObject.name}' with text '{text}'");
                            _elements.Add(tab);
                        }
                    }
                    Debug.Log($"[ATSAccessibility] Found {_elements.Count} tab buttons");
                }
                else
                {
                    // Panel 1: Content (only from active content panel, excluding tabs)
                    var activeContentPanel = FindActiveContentPanel(panel);
                    Transform searchRoot = activeContentPanel ?? panel.transform;

                    var selectables = searchRoot.GetComponentsInChildren<Selectable>(true);
                    Debug.Log($"[ATSAccessibility] DEBUG: Searching for content in '{searchRoot.name}' ({selectables.Length} selectables)");

                    foreach (var sel in selectables)
                    {
                        // Skip inactive elements
                        if (!sel.gameObject.activeInHierarchy) continue;
                        if (!sel.interactable) continue;
                        if (ShouldIgnoreElement(sel)) continue;

                        // Skip tab buttons
                        if (_tabButtons.Contains(sel))
                        {
                            Debug.Log($"[ATSAccessibility] DEBUG: Skipping tab button in content: {sel.gameObject.name}");
                            continue;
                        }

                        string text = GetElementText(sel);
                        if (string.IsNullOrEmpty(text)) continue;

                        Debug.Log($"[ATSAccessibility] DEBUG: Adding content element '{sel.gameObject.name}' with text '{text}'");
                        _elements.Add(sel);
                    }
                    Debug.Log($"[ATSAccessibility] Found {_elements.Count} content elements in active panel");
                }
                return;
            }

            // Standard (non-tabbed) panel handling
            // Include inactive selectables - popup may be animating in
            var allSelectables = panel.GetComponentsInChildren<Selectable>(true);
            Debug.Log($"[ATSAccessibility] DEBUG: Found {allSelectables.Length} total selectables in panel");

            // If no selectables, log what children exist to understand the structure
            if (allSelectables.Length == 0)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No selectables found. Logging popup structure:");
                LogChildStructure(panel.transform, 0, 3); // Log 3 levels deep
            }

            foreach (var sel in allSelectables)
            {
                // For menus, skip inactive elements (hidden panels/tabs)
                // For popups, include inactive elements (they animate in)
                if (_currentPopup == null && !sel.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!sel.interactable)
                {
                    Debug.Log($"[ATSAccessibility] DEBUG: Skipping '{sel.gameObject.name}' - not interactable");
                    continue;
                }
                if (ShouldIgnoreElement(sel))
                {
                    Debug.Log($"[ATSAccessibility] DEBUG: Skipping '{sel.gameObject.name}' - in ignore list");
                    continue;
                }

                // Skip elements with no meaningful text
                string text = GetElementText(sel);
                if (string.IsNullOrEmpty(text))
                {
                    Debug.Log($"[ATSAccessibility] DEBUG: Skipping '{sel.gameObject.name}' - no text");
                    continue;
                }

                Debug.Log($"[ATSAccessibility] DEBUG: Adding element '{sel.gameObject.name}' with text '{text}'");
                _elements.Add(sel);
            }

            Debug.Log($"[ATSAccessibility] Found {_elements.Count} elements in panel {panel.name}");
        }

        /// <summary>
        /// Check if an element should be ignored (decorative).
        /// </summary>
        private bool ShouldIgnoreElement(Selectable element)
        {
            string name = element.gameObject.name.ToLower();
            foreach (var ignored in IgnoredElementNames)
            {
                if (name.Contains(ignored)) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the primary text content of a UI element.
        /// Looks inside the element first, then checks parent/sibling for labels.
        /// </summary>
        private string GetElementText(Selectable element)
        {
            if (element == null) return null;

            // Handle dropdowns FIRST - their inner text is the value, not a label
            if (element is TMP_Dropdown dropdown && dropdown.options.Count > 0)
            {
                string value = dropdown.options[dropdown.value].text;
                // Try to find label from parent
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
                // Check for inner text that's not just a number
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
                // Check if this is a meaningful label (not generic placeholder text)
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

            // Try to find label from parent/sibling (for toggles, sliders, etc.)
            string parentLabel = FindLabelFromParent(element.transform);
            if (!string.IsNullOrEmpty(parentLabel))
            {
                return parentLabel;
            }

            // Fallback to cleaned object name
            return CleanObjectName(element.gameObject.name);
        }

        /// <summary>
        /// Check if text is just a numeric value (for sliders showing their value).
        /// </summary>
        private bool IsNumericText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return float.TryParse(text, out _);
        }

        /// <summary>
        /// Check if text is generic placeholder text that should be replaced with a label.
        /// </summary>
        private bool IsGenericText(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            var genericTexts = new[] { "Toggle", "Slider", "Button", "Dropdown", "Option A", "Item" };
            foreach (var generic in genericTexts)
            {
                if (text.Equals(generic, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Find a label by looking at parent container's children (siblings of the element).
        /// Typical structure: Parent -> [Label TMP_Text, Toggle/Slider Selectable]
        /// </summary>
        private string FindLabelFromParent(Transform elementTransform)
        {
            var parent = elementTransform.parent;
            if (parent == null) return null;

            // Look for sibling TMP_Text elements that could be labels
            foreach (Transform sibling in parent)
            {
                if (sibling == elementTransform) continue;

                // Skip if sibling has a Selectable (it's another control, not a label)
                if (sibling.GetComponent<Selectable>() != null) continue;

                // Check for direct TMP_Text component
                var labelText = sibling.GetComponent<TMP_Text>();
                if (labelText != null && !string.IsNullOrEmpty(labelText.text) && !IsGenericText(labelText.text))
                {
                    return labelText.text;
                }

                // Check for TMP_Text in children (Label might be a container)
                var childText = sibling.GetComponentInChildren<TMP_Text>();
                if (childText != null && !string.IsNullOrEmpty(childText.text) && !IsGenericText(childText.text))
                {
                    return childText.text;
                }

                // Check for legacy UI Text
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
                // Check if grandparent has a direct label child
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

        /// <summary>
        /// Convert object name to readable text.
        /// </summary>
        private string CleanObjectName(string name)
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

        /// <summary>
        /// Get the semantic type of a UI element.
        /// </summary>
        private string GetElementType(Selectable element)
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
        /// Get the current state of a UI element (for checkboxes, sliders, etc.).
        /// </summary>
        private string GetElementState(Selectable element)
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

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        /// <summary>
        /// Announce the popup when it opens - reads title, description, then first element.
        /// </summary>
        private void AnnouncePopup()
        {
            if (_currentPopup == null) return;

            var announcements = new List<string>();

            // Try to find Title and Desc text elements (common popup pattern)
            var textElements = _currentPopup.GetComponentsInChildren<TMP_Text>(true);

            foreach (var text in textElements)
            {
                string objName = text.gameObject.name.ToLower();
                if (objName == "title" || objName.EndsWith("title"))
                {
                    if (!string.IsNullOrEmpty(text.text))
                        announcements.Add(text.text);
                }
                else if (objName == "desc" || objName == "description" || objName.EndsWith("desc"))
                {
                    if (!string.IsNullOrEmpty(text.text))
                        announcements.Add(text.text);
                }
            }

            // Fallback to popup name if no title found
            if (announcements.Count == 0)
            {
                announcements.Add(CleanObjectName(_currentPopup.name));
            }

            string announcement = string.Join(". ", announcements);
            Debug.Log($"[ATSAccessibility] Announcing popup: {announcement}");
            Speech.Say(announcement);

            // Also announce the first element after a brief pause
            if (_elements.Count > 0)
            {
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Announce the current panel name.
        /// </summary>
        private void AnnouncePanelName()
        {
            if (_panels.Count == 0 || _currentPanelIndex >= _panels.Count) return;

            string name;
            if (_isTabbedPopup)
            {
                // For tabbed popups: Panel 0 = "Tabs", Panel 1 = "Content"
                name = _currentPanelIndex == 0 ? "Tabs" : "Content";
            }
            else
            {
                var panel = _panels[_currentPanelIndex];
                name = CleanObjectName(panel.name);
            }

            Speech.Say(name);

            // Also announce the first element in the panel
            if (_elements.Count > 0)
            {
                _currentElementIndex = 0;
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Announce the current element (text + type + state).
        /// </summary>
        private void AnnounceCurrentElement()
        {
            Debug.Log($"[ATSAccessibility] DEBUG: AnnounceCurrentElement - elementCount={_elements.Count}, index={_currentElementIndex}");

            if (_elements.Count == 0 || _currentElementIndex >= _elements.Count)
            {
                Debug.Log("[ATSAccessibility] DEBUG: No element to announce");
                return;
            }

            var element = _elements[_currentElementIndex];
            if (element == null)
            {
                Debug.Log("[ATSAccessibility] DEBUG: Element at index is null");
                return;
            }

            string text = GetElementText(element);
            string type = GetElementType(element);
            string state = GetElementState(element);

            Debug.Log($"[ATSAccessibility] DEBUG: Element - text='{text}', type='{type}', state='{state}'");

            // Build announcement: "Text, type, state"
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(text))
                parts.Add(text);
            if (!string.IsNullOrEmpty(type))
                parts.Add(type);
            if (!string.IsNullOrEmpty(state))
                parts.Add(state);

            string announcement = string.Join(", ", parts);
            Debug.Log($"[ATSAccessibility] DEBUG: Announcing: '{announcement}'");
            Speech.Say(announcement);
        }

        /// <summary>
        /// Log the child structure of a transform for debugging.
        /// </summary>
        private void LogChildStructure(Transform parent, int depth, int maxDepth)
        {
            if (depth >= maxDepth) return;

            string indent = new string(' ', depth * 2);

            foreach (Transform child in parent)
            {
                // Get component types on this object
                var components = child.GetComponents<Component>();
                var componentNames = new List<string>();
                foreach (var comp in components)
                {
                    if (comp != null)
                        componentNames.Add(comp.GetType().Name);
                }

                string componentStr = string.Join(", ", componentNames);
                bool isActive = child.gameObject.activeInHierarchy;

                Debug.Log($"[ATSAccessibility] DEBUG: {indent}{child.name} [{componentStr}] active={isActive}");

                // Recurse
                LogChildStructure(child, depth + 1, maxDepth);
            }
        }
    }
}
