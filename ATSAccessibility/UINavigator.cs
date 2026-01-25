using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles UI navigation within popups/menus.
    /// Uses panel-based hierarchy: Left/Right switches panels, Up/Down cycles elements.
    /// </summary>
    public class UINavigator : IKeyHandler
    {
        // MonoBehaviour reference for starting coroutines
        private MonoBehaviour _coroutineRunner;

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

        // Text field editing state
        private bool _isEditingTextField = false;
        private TMP_InputField _editingInputField = null;

        // Type-ahead search
        private TypeAheadSearch _search = new TypeAheadSearch();

        /// <summary>
        /// Whether there's an active popup being navigated.
        /// </summary>
        public bool HasActivePopup => _currentPopup != null;

        /// <summary>
        /// Whether the current popup is the MetaRewardsPopup.
        /// </summary>
        public bool IsMetaRewardsPopup => _currentPopup != null && _currentPopup.name.Contains("MetaRewards");

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

        /// <summary>
        /// Whether currently editing a text field.
        /// </summary>
        public bool IsEditingTextField => _isEditingTextField;

        /// <summary>
        /// Whether this handler is currently active (IKeyHandler).
        /// </summary>
        public bool IsActive => HasActivePopup || HasActiveMenu;

        /// <summary>
        /// Process a key event for popup/menu navigation (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!HasActivePopup && !HasActiveMenu) return false;

            // If editing a text field, only handle Enter (submit) and Escape (cancel)
            if (_isEditingTextField)
            {
                switch (keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        EndTextFieldEdit(submit: true);
                        return true;
                    case KeyCode.Escape:
                        EndTextFieldEdit(submit: false);
                        return true;
                    default:
                        // All other keys pass through to the text field
                        return true;
                }
            }

            // If a dropdown is open, handle it first
            if (IsDropdownOpen)
            {
                return ProcessDropdownKey(keyCode);
            }

            // Special handling for MetaRewardsPopup (polling/repeat behavior)
            if (IsMetaRewardsPopup)
            {
                if (MetaRewardsPopupReader.ProcessKeyEvent(keyCode))
                {
                    return true;
                }
            }

            // Clear search buffer on navigation keys
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateElement(-1);
                    return true;
                case KeyCode.DownArrow:
                    NavigateElement(1);
                    return true;
                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals:
                    AdjustCurrentSlider(1, modifiers.Shift ? 10 : 1);
                    return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    AdjustCurrentSlider(-1, modifiers.Shift ? 10 : 1);
                    return true;
                case KeyCode.LeftArrow:
                    NavigatePanel(-1);
                    return true;
                case KeyCode.RightArrow:
                    NavigatePanel(1);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrentElement();
                    return true;
                case KeyCode.Space:
                    ActivateCurrentElement();
                    return true;
                case KeyCode.Backspace:
                    return HandleBackspace();
                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        InputBlocker.BlockCancelOnce = true;
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close popup
                    return false;
                default:
                    // Handle A-Z keys for type-ahead search
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        return HandleSearchKey(c);
                    }
                    // Consume all other keys while popup/menu is active
                    return true;
            }
        }

        /// <summary>
        /// Process dropdown key events.
        /// Returns true if key was handled, false if dropdown was closed externally.
        /// </summary>
        private bool ProcessDropdownKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    return NavigateDropdownOption(-1);

                case KeyCode.DownArrow:
                    return NavigateDropdownOption(1);

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    SelectCurrentDropdownOption();
                    return true;

                case KeyCode.Escape:
                    CloseActiveDropdown();
                    return true;

                default:
                    // Other keys - let dropdown stay open but don't handle
                    return true;
            }
        }

        // ========================================
        // CONSTRUCTOR
        // ========================================

        /// <summary>
        /// Creates a new UINavigator with a MonoBehaviour reference for coroutine execution.
        /// </summary>
        public UINavigator(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

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

                // Check if there's still an active popup underneath that we should attach to
                var remainingPopup = GameReflection.GetTopActivePopup();
                if (remainingPopup != null)
                {
                    Debug.Log("[ATSAccessibility] Found remaining popup, re-attaching");
                    OnPopupShown(remainingPopup);
                }
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
            // Reset MetaRewardsPopupReader state if it was a MetaRewards popup
            if (_currentPopup != null && _currentPopup.name.Contains("MetaRewards"))
            {
                MetaRewardsPopupReader.Reset();
            }

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
            _search.Clear();
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
                else if (element is TMP_InputField inputField)
                {
                    StartTextFieldEdit(inputField);
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

            // Guard against empty dropdown (prevents division by zero)
            if (_dropdownToggles.Count == 0)
            {
                Debug.LogWarning("[ATSAccessibility] Dropdown has no toggles");
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

            // Prevent Escape from also closing the parent popup
            InputBlocker.BlockCancelOnce = true;

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
        // TEXT FIELD EDITING
        // ========================================

        /// <summary>
        /// Start editing a text field.
        /// </summary>
        public void StartTextFieldEdit(TMP_InputField inputField)
        {
            _isEditingTextField = true;
            _editingInputField = inputField;

            // Disable input blocking so keys reach the text field
            InputBlocker.IsBlocking = false;

            // Focus the field
            inputField.Select();
            inputField.ActivateInputField();

            string currentText = string.IsNullOrEmpty(inputField.text) ? "empty" : inputField.text;
            Speech.Say($"Editing, current text: {currentText}");
            Debug.Log($"[ATSAccessibility] Started editing text field: {inputField.name}");
        }

        /// <summary>
        /// End text field editing (submit or cancel).
        /// </summary>
        public void EndTextFieldEdit(bool submit)
        {
            if (!_isEditingTextField || _editingInputField == null) return;

            if (submit)
            {
                // Deselect triggers OnEndEdit which submits
                _editingInputField.DeactivateInputField();
                string finalText = string.IsNullOrEmpty(_editingInputField.text) ? "empty" : _editingInputField.text;
                Speech.Say($"Submitted: {finalText}");
                Debug.Log($"[ATSAccessibility] Submitted text field: {finalText}");
            }
            else
            {
                // Cancel - just deactivate without submitting
                _editingInputField.DeactivateInputField();
                Speech.Say("Cancelled");
                Debug.Log("[ATSAccessibility] Cancelled text field editing");
            }

            _isEditingTextField = false;
            _editingInputField = null;

            // Re-enable input blocking
            InputBlocker.IsBlocking = true;
        }

        // ========================================
        // SLIDER ADJUSTMENT
        // ========================================

        /// <summary>
        /// Adjust the current slider value by a step.
        /// </summary>
        /// <param name="direction">1 for increase, -1 for decrease</param>
        /// <param name="stepPercent">Step size as percentage (1 = 1%, 10 = 10%)</param>
        public void AdjustCurrentSlider(int direction, int stepPercent = 1)
        {
            if (_elements.Count == 0 || _currentElementIndex >= _elements.Count) return;

            var element = _elements[_currentElementIndex];
            if (element is Slider slider)
            {
                float step = (slider.maxValue - slider.minValue) * (stepPercent / 100f);
                float newValue = Mathf.Clamp(slider.value + (step * direction), slider.minValue, slider.maxValue);
                slider.value = newValue;

                int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
                Speech.Say($"{percent} percent");
                Debug.Log($"[ATSAccessibility] Adjusted slider to {percent}%");
            }
        }

        // ========================================
        // TYPE-AHEAD SEARCH
        // ========================================

        /// <summary>
        /// Handle a character key for type-ahead search.
        /// </summary>
        private bool HandleSearchKey(char c)
        {
            if (_elements.Count == 0) return false;

            _search.AddChar(c);
            int matchIndex = _search.FindMatch(_elements, UIElementFinder.GetElementText);

            if (matchIndex >= 0)
            {
                _currentElementIndex = matchIndex;
                AnnounceCurrentElement();
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' matched element at index {matchIndex}");
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
                Debug.Log($"[ATSAccessibility] Search '{_search.Buffer}' - no match");
            }

            return true;
        }

        /// <summary>
        /// Handle backspace to remove last character from search buffer.
        /// </summary>
        private bool HandleBackspace()
        {
            if (!_search.HasBuffer) return true;  // Consume even with empty buffer

            _search.RemoveChar();

            if (_search.HasBuffer)
            {
                int matchIndex = _search.FindMatch(_elements, UIElementFinder.GetElementText);
                if (matchIndex >= 0)
                {
                    _currentElementIndex = matchIndex;
                    AnnounceCurrentElement();
                }
                else
                {
                    Speech.Say($"No match for {_search.Buffer}");
                }
            }
            else
            {
                Speech.Say("Search cleared");
            }

            return true;
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
            _coroutineRunner?.StartCoroutine(AnnouncePopupDelayed());
        }

        private IEnumerator AnnouncePopupDelayed()
        {
            // Wait one frame for text elements to be populated
            yield return null;

            if (_currentPopup == null) yield break;

            // Special handling for MetaRewardsPopup
            if (_currentPopup.name.Contains("MetaRewards"))
            {
                yield return MetaRewardsPopupReader.AnnounceMetaRewardsPopup(_currentPopup, _coroutineRunner);
                yield break;
            }

            // Hardcoded names for menus that pick up extraneous text
            string popupName = _currentPopup.name;
            if (popupName.Contains("Options") || popupName.Contains("Settings"))
            {
                Speech.Say("Options");
                yield break;
            }
            if (popupName.Contains("Pause") || popupName.Contains("GameMenu"))
            {
                Speech.Say("Pause Menu");
                yield break;
            }

            // Only get active elements - inactive tabs (like Key Bindings) have placeholder text
            var textElements = _currentPopup.GetComponentsInChildren<TMP_Text>(false);

            // OptionsPopup has tons of settings labels - use strict filtering
            bool isOptionsPopup = _currentPopup.name.Contains("Options");

            // Collect title and description separately
            string titleText = null;
            string descText = null;

            foreach (var text in textElements)
            {
                if (string.IsNullOrEmpty(text.text)) continue;

                string objName = text.gameObject.name;

                // Match title patterns: "title", "*title", "name", "*name", "header", "*header"
                if (titleText == null &&
                    (objName.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                     objName.EndsWith("title", StringComparison.OrdinalIgnoreCase) ||
                     objName.Equals("name", StringComparison.OrdinalIgnoreCase) ||
                     objName.EndsWith("name", StringComparison.OrdinalIgnoreCase) ||
                     objName.Equals("header", StringComparison.OrdinalIgnoreCase) ||
                     objName.EndsWith("header", StringComparison.OrdinalIgnoreCase)))
                {
                    titleText = text.text;
                }
                // Match description patterns: "desc", "*desc", "description", "text", "message", "label"
                else if (descText == null &&
                         (objName.Equals("desc", StringComparison.OrdinalIgnoreCase) ||
                          objName.Equals("description", StringComparison.OrdinalIgnoreCase) ||
                          objName.EndsWith("desc", StringComparison.OrdinalIgnoreCase) ||
                          // For non-Options popups, also match common dialogue patterns
                          (!isOptionsPopup &&
                           (objName.Equals("text", StringComparison.OrdinalIgnoreCase) ||
                            objName.Equals("message", StringComparison.OrdinalIgnoreCase) ||
                            objName.Equals("label", StringComparison.OrdinalIgnoreCase)))))
                {
                    descText = text.text;
                }
            }

            // Build announcement: prefer title + desc, fall back to popup name
            string announcement;
            if (!string.IsNullOrEmpty(titleText) && !string.IsNullOrEmpty(descText))
            {
                announcement = $"{titleText}. {descText}";
            }
            else if (!string.IsNullOrEmpty(titleText))
            {
                announcement = titleText;
            }
            else if (!string.IsNullOrEmpty(descText))
            {
                announcement = descText;
            }
            else
            {
                announcement = UIElementFinder.CleanObjectName(_currentPopup.name);
            }
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
            Speech.Say(announcement);
        }
    }
}
