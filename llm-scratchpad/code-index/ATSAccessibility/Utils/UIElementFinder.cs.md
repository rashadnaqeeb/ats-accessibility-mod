# UIElementFinder.cs
Handles discovery of UI panels and elements, plus text/type/state extraction.
Static utility class used by UINavigator.

## class UIElementFinder (line 15)

### Fields
- private static readonly string[] IgnoredElementNames (line 17)
  { "scrollbar", "background", "resize", "handle", "hide", "blend", "item", "template" }
- private static readonly string[] GenericTexts (line 22)
  { "Toggle", "Slider", "Button", "Dropdown", "Option A", "Item" }
- private static FieldInfo _demoElementInFullGameField (line 27)
- private static bool _demoElementFieldLookedUp (line 28)

### Methods
- public static PanelDiscoveryResult DiscoverPanels(GameObject root, bool isPopup) (line 38)
  Finds all panels within a popup or menu root. Tries tabbed structure first, then standard panel detection. Filters to top-level panels only. Falls back to root if none found.
- private static GameObject FindOwningPopup(Transform t, Transform root) (line 97)
  Walks up hierarchy looking for a "Popup" name between component and root. Returns null if no intermediate popup found.
- private static bool TrySetupTabsPanel(GameObject root, PanelDiscoveryResult result) (line 110)
  Detects TabsPanel/TabsButton structure. Requires at least 2 tab buttons. Panel 0 = tabs, Panel 1 = content.
- public static Transform FindActiveContentPanel(object tabsPanelRef) (line 165)
  Gets the content GameObject for the currently active tab via TabsPanel.current.content.
- public static List<Selectable> FindElementsInPanel(GameObject panel, bool isPopup, bool isTabbedPopup, int panelIndex, List<Selectable> tabButtons, object tabsPanelRef) (line 194)
  For tabbed popups: panel 0 returns tab buttons, panel 1 returns content elements from active tab. Standard: returns all non-ignored, non-empty-text selectables.
- private static bool ShouldSkipElement(Selectable sel, Transform boundary, bool isPopup) (line 255)
  Consolidated visibility+interactability+filter check. Menus require activeInHierarchy; popups allow parent-inactive elements but check CanvasGroup/DemoElement.
- private static (bool filtered, string reason) IsElementFiltered(Transform t, Transform boundary) (line 278)
  Single hierarchy walk combining: deactivated check, CanvasGroup alpha=0 check, DemoElement.inFullGame=false check.
- public static bool ShouldIgnoreElement(Selectable element) (line 317)
  Checks element name against IgnoredElementNames list.
- public static string GetTextFromTransform(Transform t) (line 333)
  Gets TMP_Text from children, or falls back to CleanObjectName. For non-Selectable elements.
- public static string GetElementText(Selectable element) (line 347)
  Priority: dropdown value+label, slider label, TMP_Text (with Pick context lookup), legacy Text, input field text/placeholder, sibling label, cleaned object name. Skips generic text strings.
- private static string FindLabelFromParent(Transform elementTransform) (line 424)
  Searches siblings (and grandparent-level aunts/uncles) for non-interactive TMP_Text labels.
- private static string TryGetPickButtonContext(GameObject buttonObject) (line 470)
  Checks common path names (Name, Name/Text, BG2/Header, Header) then all sibling TMP_Text for item context.
- private static bool IsNumericText(string text) (line 503)
- private static bool IsGenericText(string text) (line 508)
- public static string CleanObjectName(string name) (line 521)
  Strips btn_/txt_/lbl_ prefixes and _btn/_txt/_lbl suffixes, splits camelCase, replaces underscores with spaces.
- public static string GetElementType(Selectable element) (line 536)
  Returns "button", "checkbox", "radio button", "slider", "dropdown", or "text field". Buttons wrapped by ToggleButton component become "checkbox".
- public static string GetElementState(Selectable element) (line 565)
  Returns "checked"/"unchecked" for Toggle and ToggleButton, "N percent" for Slider, null otherwise.
- public static Component FindToggleButton(Selectable element) (line 594)
  Checks the Button's own GameObject, then parent, for ToggleButton component.
- public static bool? GetToggleButtonState(Component toggleButton) (line 616)
  Calls ToggleButton's isOn method via reflection. Returns null on failure.
- public static string FindSectionName(Transform element) (line 634)
  Walks up hierarchy looking for ancestor with "Section" in name. Prefers header text label, falls back to cleaned name minus "Section" suffix.
- private static string FindSectionHeaderText(Transform section) (line 656)
  Looks for direct-child TMP_Text that is not part of a Selectable.

---

## class PanelDiscoveryResult (line 673)
Result of panel discovery operation.

### Properties
- public List<GameObject> Panels { get; } (line 674)
- public List<Selectable> TabButtons { get; } (line 675)
- public object TabsPanelRef { get; set; } (line 676)
- public bool IsTabbedPopup { get; set; } (line 677)
