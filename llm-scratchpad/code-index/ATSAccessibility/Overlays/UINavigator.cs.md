# UINavigator.cs
Handles UI navigation within popups/menus.
Uses panel-based hierarchy: Left/Right switches panels, Up/Down cycles elements.

## class UINavigator: IKeyHandler, ISearchable, IHelpProvider (line 16)

### Fields
- private MonoBehaviour _coroutineRunner (line 18)
- private GameObject _currentPopup (line 21)
- private GameObject _currentMenu (line 24)
- private List<GameObject> _panels (line 27)
- private int _currentPanelIndex (line 28)
- private List<Selectable> _elements (line 31)
- private int _currentElementIndex (line 32)
- private bool _isTabbedPopup (line 35)
- private List<Selectable> _tabButtons (line 36)
- private object _tabsPanelRef (line 37)
- private TMP_Dropdown _activeDropdown (line 40)
- private List<Toggle> _dropdownToggles (line 41)
- private int _dropdownIndex (line 42)
- private bool _isEditingTextField (line 45)
- private TMP_InputField _editingInputField (line 46)
- private TypeAheadSearch _search (line 49)
- private string _lastAnnouncedSection (line 52)
- private static readonly List<HelpEntry> _helpEntries (line 83)

### Properties
- public bool HasActivePopup { get; } (line 57)
- public bool HasActiveMenu { get; } (line 62)
- public bool IsNavigationActive { get; } (line 67)
- public bool IsDropdownOpen { get; } (line 72)
- public bool IsEditingTextField { get; } (line 77)
- public HelpBehavior HelpBehavior { get; } (line 88)
  Returns HelpBehavior.Terminator
- public string HelpContextName { get; } (line 89)
- public bool IsActive { get; } (line 96)
  IKeyHandler implementation; true when HasActivePopup or HasActiveMenu

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 90)
- public IReadOnlyList<string> GetPassthroughKeys() (line 91)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 102)
  IKeyHandler implementation; routes to dropdown, text field, search, or standard navigation
- private bool ProcessDropdownKey(KeyCode keyCode) (line 177)
- public UINavigator(MonoBehaviour coroutineRunner) (line 208)
- public void OnPopupShown(object popup) (line 219)
  Builds navigation panels/elements when a new popup is detected
- public void OnPopupHidden(object popup) (line 239)
  Resets state; if another popup is still active underneath, re-attaches to it
- public void SetupMenuNavigation(GameObject menuRoot, string menuName = null) (line 259)
  For Canvas-based menus (e.g. main menu); treats root as single panel; skips if popup active
- public void ClearMenuNavigation() (line 289)
- public void Reset() (line 306)
- private void ResetPopup() (line 311)
- private void ClearNavigationState() (line 318)
- public void NavigatePanel(int direction) (line 338)
  Wraps panel index; rebuilds elements and announces panel name
- public void NavigateElementTo(int index) (line 352)
  Jump to absolute element index (Home/End)
- public void NavigateElement(int direction) (line 361)
  Wraps element index circularly
- public bool ActivateCurrentElement() (line 372)
  Handles Button (with ToggleButton detection), Toggle, TMP_Dropdown, TMP_InputField, Slider
  When activating a tab button in a tabbed popup, auto-switches to the content panel
- public void OpenDropdown(TMP_Dropdown dropdown) (line 430)
  Shows dropdown, harvests toggles (skipping template "Option A"), focuses current selection
- public bool NavigateDropdownOption(int direction) (line 480)
  Returns false if dropdown list has been closed externally
- public void SelectCurrentDropdownOption() (line 506)
  Sets toggle.isOn = true to trigger game's selection mechanism
- public void CloseActiveDropdown() (line 525)
  Hides dropdown and sets InputBlocker.BlockCancelOnce to prevent Escape from closing parent popup
- private void ClearDropdownState() (line 537)
- private void AnnounceDropdownOption() (line 543)
- public void StartTextFieldEdit(TMP_InputField inputField) (line 558)
  Disables InputBlocker, focuses field, announces current text
- public void EndTextFieldEdit(bool submit) (line 576)
  Re-enables InputBlocker; submit deactivates field and announces final text; cancel just deactivates
- public void AdjustCurrentSlider(int direction, int stepPercent = 1) (line 606)
  Steps slider by stepPercent% of its total range; announces resulting percent
- int ISearchable.SearchItemCount { get; } (line 624)
- int ISearchable.SearchCurrentIndex { get; } (line 626)
- string ISearchable.GetSearchLabel(int index) (line 628)
- void ISearchable.SearchMoveTo(int index) (line 633)
- private GameObject GetGameObjectFromPopup(object popup) (line 642)
- private void RebuildNavigation() (line 647)
- private void RebuildPanels() (line 652)
- private void RebuildElementsForCurrentPanel() (line 665)
  Focuses the active tab if on a tabbed popup's tab panel
- private int FindActiveTabIndex() (line 694)
  Reads TabsPanel.current to find the active tab's Button in the elements list
- private void AnnouncePopup() (line 722)
  Starts AnnouncePopupDelayed coroutine
- private IEnumerator AnnouncePopupDelayed() (line 727)
  Waits one frame then scans TMP_Text elements for title/description; has hardcoded names for Options/Pause menus
- private void AnnouncePanelName() (line 801)
  Announces "Tabs" or "Content" for tabbed popups; cleaned object name otherwise
- private void AnnounceCurrentElement() (line 820)
  Announces section heading on section change, then element text, type, and state
