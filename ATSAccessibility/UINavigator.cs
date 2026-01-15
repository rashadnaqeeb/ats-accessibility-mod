using System;
using System.Collections.Generic;
using System.Linq;
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
        // Current popup being navigated
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
        private object _tabsPanelRef = null;

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

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Called when a popup is shown.
        /// </summary>
        public void OnPopupShown(object popup)
        {
            var popupGO = GetGameObjectFromPopup(popup);
            if (popupGO == null) return;

            // Compare references - not just event firing
            if (popupGO != _currentPopup)
            {
                Debug.Log($"[ATSAccessibility] New popup opened: {popupGO.name}");

                _currentPopup = popupGO;
                _currentPanelIndex = 0;
                _currentElementIndex = 0;

                RebuildNavigation();
                AnnouncePopup();
            }
        }

        /// <summary>
        /// Called when a popup is hidden.
        /// </summary>
        public void OnPopupHidden(object popup)
        {
            var popupGO = GetGameObjectFromPopup(popup);
            if (popupGO == null) return;

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
            if (_currentPopup != null) return;

            // Compare references - but always rebuild if elements were cleared
            if (menuRoot == _currentMenu && _elements.Count > 0) return;

            Debug.Log($"[ATSAccessibility] Setting up menu navigation: {menuRoot.name}");

            _currentMenu = menuRoot;
            _currentPanelIndex = 0;
            _currentElementIndex = 0;

            // For menus, use root as single panel
            _panels.Clear();
            _panels.Add(menuRoot);
            RebuildElementsForCurrentPanel();

            string name = menuName ?? UIElementFinder.CleanObjectName(menuRoot.name);
            if (_elements.Count > 0)
            {
                Debug.Log($"[ATSAccessibility] Menu '{name}' has {_elements.Count} elements");
                AnnounceCurrentElement();
            }
        }

        /// <summary>
        /// Clear menu navigation.
        /// </summary>
        public void ClearMenuNavigation()
        {
            if (_currentMenu != null)
            {
                Debug.Log($"[ATSAccessibility] Clearing menu navigation: {_currentMenu.name}");
                _currentMenu = null;

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

        // ========================================
        // NAVIGATION ACTIONS
        // ========================================

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
            if (_elements.Count == 0) return;

            _currentElementIndex = (_currentElementIndex + direction + _elements.Count) % _elements.Count;
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
                    Debug.Log($"[ATSAccessibility] Activated button: {UIElementFinder.GetElementText(element)}");
                }
                else if (element is Toggle toggle)
                {
                    toggle.isOn = !toggle.isOn;
                    string state = toggle.isOn ? "checked" : "unchecked";
                    Speech.Say(state);
                    Debug.Log($"[ATSAccessibility] Toggled: {UIElementFinder.GetElementText(element)} -> {state}");
                }
                else if (element is TMP_Dropdown dropdown)
                {
                    dropdown.Show();
                    Debug.Log($"[ATSAccessibility] Opened dropdown: {UIElementFinder.GetElementText(element)}");
                }
                else if (element is Slider slider)
                {
                    slider.Select();
                    Debug.Log($"[ATSAccessibility] Selected slider: {UIElementFinder.GetElementText(element)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to activate element: {ex.Message}");
            }
        }

        /// <summary>
        /// Dismiss the current popup (Escape key).
        /// </summary>
        public void DismissPopup()
        {
            if (_currentPopup == null) return;

            try
            {
                // Strategy 1: Look for BackableButton component
                var backableButtons = _currentPopup.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && c.GetType().Name == "BackableButton")
                    .ToList();

                if (backableButtons.Count > 0)
                {
                    var button = backableButtons[0].GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.Invoke();
                        Debug.Log("[ATSAccessibility] Dismissed popup via BackableButton");
                        return;
                    }
                }

                // Strategy 2: Look for close/back/cancel buttons
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

                // Strategy 3: Look for "Blend" button
                var blendButton = _currentPopup.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(b => b.gameObject.name.ToLower() == "blend");

                if (blendButton != null)
                {
                    blendButton.onClick.Invoke();
                    Debug.Log("[ATSAccessibility] Dismissed popup via Blend button");
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

        private GameObject GetGameObjectFromPopup(object popup)
        {
            var component = popup as Component;
            return component?.gameObject;
        }

        private void RebuildNavigation()
        {
            RebuildPanels();
            RebuildElementsForCurrentPanel();
        }

        private void RebuildPanels()
        {
            var root = _currentPopup ?? _currentMenu;
            if (root == null) return;

            bool isPopup = _currentPopup != null;
            var result = UIElementFinder.DiscoverPanels(root, isPopup);

            _panels = result.Panels;
            _tabButtons = result.TabButtons;
            _tabsPanelRef = result.TabsPanelRef;
            _isTabbedPopup = result.IsTabbedPopup;
        }

        private void RebuildElementsForCurrentPanel()
        {
            _elements.Clear();
            _currentElementIndex = 0;

            if (_panels.Count == 0) return;

            var panel = _panels[_currentPanelIndex];
            bool isPopup = _currentPopup != null;

            _elements = UIElementFinder.FindElementsInPanel(
                panel,
                isPopup,
                _isTabbedPopup,
                _currentPanelIndex,
                _tabButtons,
                _tabsPanelRef);
        }

        // ========================================
        // ANNOUNCEMENTS
        // ========================================

        private void AnnouncePopup()
        {
            if (_currentPopup == null) return;

            var announcements = new List<string>();
            // Only scan active text elements - inactive tabs may have placeholder text
            var textElements = _currentPopup.GetComponentsInChildren<TMP_Text>(false);

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

            if (announcements.Count == 0)
            {
                announcements.Add(UIElementFinder.CleanObjectName(_currentPopup.name));
            }

            string announcement = string.Join(". ", announcements);
            Debug.Log($"[ATSAccessibility] Announcing popup: {announcement}");
            Speech.Say(announcement);

            // Don't automatically announce the first element - let the popup info be read
            // User must press arrow key to start element navigation
        }

        private void AnnouncePanelName()
        {
            if (_panels.Count == 0 || _currentPanelIndex >= _panels.Count) return;

            string name;
            if (_isTabbedPopup)
            {
                name = _currentPanelIndex == 0 ? "Tabs" : "Content";
            }
            else
            {
                var panel = _panels[_currentPanelIndex];
                name = UIElementFinder.CleanObjectName(panel.name);
            }

            Speech.Say(name);

            if (_elements.Count > 0)
            {
                _currentElementIndex = 0;
                AnnounceCurrentElement();
            }
        }

        private void AnnounceCurrentElement()
        {
            if (_elements.Count == 0 || _currentElementIndex >= _elements.Count) return;

            var element = _elements[_currentElementIndex];
            if (element == null) return;

            string text = UIElementFinder.GetElementText(element);
            string type = UIElementFinder.GetElementType(element);
            string state = UIElementFinder.GetElementState(element);

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
    }
}
