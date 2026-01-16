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

        // Dropdown navigation
        private TMP_Dropdown _activeDropdown = null;
        private List<Toggle> _dropdownToggles = new List<Toggle>();
        private int _dropdownIndex = 0;

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
        /// Whether a dropdown is currently open for navigation.
        /// </summary>
        public bool IsDropdownOpen => _activeDropdown != null;

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
            _currentMenu = null;
            ClearNavigationState();
        }

        private void ResetPopup()
        {
            ClearNavigationState();
        }

        /// <summary>
        /// Clear navigation state (shared by Reset and ResetPopup).
        /// </summary>
        private void ClearNavigationState()
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
        /// Returns true if an element was activated, false if no element to activate.
        /// </summary>
        public bool ActivateCurrentElement()
        {
            if (_elements.Count == 0 || _currentElementIndex >= _elements.Count)
            {
                Debug.Log("[ATSAccessibility] No element to activate - passing through to game");
                return false;
            }

            var element = _elements[_currentElementIndex];
            if (element == null)
            {
                Debug.Log("[ATSAccessibility] Current element is null - passing through to game");
                return false;
            }

            try
            {
                if (element is Button button)
                {
                    button.onClick.Invoke();
                    Debug.Log($"[ATSAccessibility] Activated button: {UIElementFinder.GetElementText(element)}");

                    // If we just activated a tab, auto-switch to content panel
                    if (_isTabbedPopup && _currentPanelIndex == 0)
                    {
                        _currentPanelIndex = 1;
                        RebuildElementsForCurrentPanel();
                        AnnouncePanelName();
                        return true;
                    }
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
                    OpenDropdown(dropdown);
                }
                else if (element is Slider slider)
                {
                    slider.Select();
                    Debug.Log($"[ATSAccessibility] Selected slider: {UIElementFinder.GetElementText(element)}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Failed to activate element: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // DROPDOWN NAVIGATION
        // ========================================

        /// <summary>
        /// Open a dropdown for keyboard navigation.
        /// </summary>
        public void OpenDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null) return;

            _activeDropdown = dropdown;
            dropdown.Show();

            // Find the dropdown list and its toggles
            var dropdownList = dropdown.transform.Find("Dropdown List");
            if (dropdownList == null)
            {
                Debug.LogWarning("[ATSAccessibility] Dropdown list not found after Show()");
                ClearDropdownState();
                return;
            }

            // Get all toggles, filter out the template "Option A"
            _dropdownToggles.Clear();
            var allToggles = dropdownList.GetComponentsInChildren<Toggle>(true);
            foreach (var toggle in allToggles)
            {
                var label = toggle.GetComponentInChildren<TMP_Text>()?.text;
                if (label != null && label != "Option A")
                {
                    _dropdownToggles.Add(toggle);
                }
            }

            if (_dropdownToggles.Count == 0)
            {
                Debug.LogWarning("[ATSAccessibility] No valid toggles found in dropdown");
                ClearDropdownState();
                return;
            }

            // Find currently selected option (isOn=true)
            _dropdownIndex = 0;
            for (int i = 0; i < _dropdownToggles.Count; i++)
            {
                if (_dropdownToggles[i].isOn)
                {
                    _dropdownIndex = i;
                    break;
                }
            }

            // Announce dropdown opened
            string announcement = $"dropdown opened, {_dropdownToggles.Count} options";
            Debug.Log($"[ATSAccessibility] {announcement}");
            Speech.Say(announcement);

            // Announce current option
            AnnounceDropdownOption();
        }

        /// <summary>
        /// Navigate within the open dropdown. Returns false if dropdown was closed externally.
        /// </summary>
        public bool NavigateDropdownOption(int direction)
        {
            if (_activeDropdown == null) return false;

            // Check if dropdown list still exists
            var list = _activeDropdown.transform.Find("Dropdown List");
            if (list == null || !list.gameObject.activeInHierarchy)
            {
                Debug.Log("[ATSAccessibility] Dropdown closed externally");
                ClearDropdownState();
                return false;
            }

            // Navigate with wrapping
            _dropdownIndex = (_dropdownIndex + direction + _dropdownToggles.Count) % _dropdownToggles.Count;
            AnnounceDropdownOption();
            return true;
        }

        /// <summary>
        /// Select the current dropdown option and close the dropdown.
        /// </summary>
        public void SelectCurrentDropdownOption()
        {
            if (_activeDropdown == null || _dropdownToggles.Count == 0) return;

            if (_dropdownIndex >= 0 && _dropdownIndex < _dropdownToggles.Count)
            {
                var toggle = _dropdownToggles[_dropdownIndex];
                var optionText = toggle.GetComponentInChildren<TMP_Text>()?.text ?? "option";

                // Setting isOn triggers the dropdown's selection mechanism
                toggle.isOn = true;

                Debug.Log($"[ATSAccessibility] Selected dropdown option: {optionText}");
                Speech.Say($"{optionText}, selected");
            }

            ClearDropdownState();
        }

        /// <summary>
        /// Close the dropdown without selecting.
        /// </summary>
        public void CloseActiveDropdown()
        {
            if (_activeDropdown == null) return;

            _activeDropdown.Hide();
            Debug.Log("[ATSAccessibility] Dropdown cancelled");
            Speech.Say("cancelled");

            ClearDropdownState();
        }

        private void ClearDropdownState()
        {
            _activeDropdown = null;
            _dropdownToggles.Clear();
            _dropdownIndex = 0;
        }

        private void AnnounceDropdownOption()
        {
            if (_dropdownIndex >= 0 && _dropdownIndex < _dropdownToggles.Count)
            {
                var toggle = _dropdownToggles[_dropdownIndex];
                var text = toggle.GetComponentInChildren<TMP_Text>()?.text ?? "option";
                Debug.Log($"[ATSAccessibility] Dropdown option: {text}");
                Speech.Say(text);
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

            // For tabbed popups on the tabs panel, focus on the active tab
            if (_isTabbedPopup && _currentPanelIndex == 0 && _elements.Count > 0)
            {
                int activeTabIndex = FindActiveTabIndex();
                if (activeTabIndex >= 0 && activeTabIndex < _elements.Count)
                    _currentElementIndex = activeTabIndex;
            }
        }

        /// <summary>
        /// Find the index of the currently active tab in the elements list.
        /// Returns 0 if the active tab cannot be determined.
        /// </summary>
        private int FindActiveTabIndex()
        {
            if (_tabsPanelRef == null) return 0;

            try
            {
                // Get the current TabsButton from TabsPanel.current field
                var currentTabsButton = GameReflection.TabsPanelCurrentField?.GetValue(_tabsPanelRef);
                if (currentTabsButton == null) return 0;

                // Get the Button component from the TabsButton
                var activeButton = GameReflection.TabsButtonButtonField?.GetValue(currentTabsButton);
                if (activeButton == null) return 0;

                // Find which element matches
                for (int i = 0; i < _elements.Count; i++)
                {
                    if (object.ReferenceEquals(_elements[i], activeButton))
                        return i;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] FindActiveTabIndex failed: {ex.Message}");
            }

            return 0;
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
                string objName = text.gameObject.name;
                if (objName.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                    objName.EndsWith("title", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(text.text))
                        announcements.Add(text.text);
                }
                else if (objName.Equals("desc", StringComparison.OrdinalIgnoreCase) ||
                         objName.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                         objName.EndsWith("desc", StringComparison.OrdinalIgnoreCase))
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
