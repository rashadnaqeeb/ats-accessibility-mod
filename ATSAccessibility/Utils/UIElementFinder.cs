using ATSAccessibility.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Handles discovery of UI panels and elements, plus text/type/state extraction.
	/// Static utility class used by UINavigator.
	/// </summary>
	public static class UIElementFinder {
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
		public static PanelDiscoveryResult DiscoverPanels(GameObject root, bool isPopup) {
			var result = new PanelDiscoveryResult();

			if (root == null) return result;

			// Try to detect tabbed popup structure
			if (isPopup && TrySetupTabsPanel(root, result)) {
				return result;
			}

			// Standard panel detection
			var allTransforms = root.GetComponentsInChildren<Transform>(true);
			var potentialPanels = new List<GameObject>();

			foreach (var t in allTransforms) {
				string name = t.name;
				if (name.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
					name.IndexOf("content", StringComparison.OrdinalIgnoreCase) >= 0 ||
					name.IndexOf("section", StringComparison.OrdinalIgnoreCase) >= 0) {
					// Skip panels that are inside an inactive nested popup
					var owningPopup = FindOwningPopup(t, root.transform);
					if (owningPopup != null && !owningPopup.activeInHierarchy) {
						continue;
					}

					var selectables = t.GetComponentsInChildren<Selectable>(true);
					var validSelectables = selectables.Where(s =>
						(isPopup || s.gameObject.activeInHierarchy) &&
						s.interactable &&
						!ShouldIgnoreElement(s)).ToList();

					if (validSelectables.Count > 0) {
						potentialPanels.Add(t.gameObject);
					}
				}
			}

			// Filter out nested panels (only keep top-level ones)
			foreach (var panel in potentialPanels) {
				bool isNested = potentialPanels.Any(other =>
					other != panel && panel.transform.IsChildOf(other.transform));

				if (!isNested) {
					result.Panels.Add(panel);
				}
			}

			// If no panels found, use the root itself as the single "panel"
			if (result.Panels.Count == 0) {
				result.Panels.Add(root);
			}

			return result;
		}

		/// <summary>
		/// Find the owning popup of a component by walking up the hierarchy.
		/// Returns null if no intermediate popup found between component and root.
		/// </summary>
		private static GameObject FindOwningPopup(Transform t, Transform root) {
			var current = t.parent;
			while (current != null && current != root) {
				if (current.name.Contains("Popup"))
					return current.gameObject;
				current = current.parent;
			}
			return null;
		}

		/// <summary>
		/// Try to detect TabsPanel/TabsButton structure.
		/// </summary>
		private static bool TrySetupTabsPanel(GameObject root, PanelDiscoveryResult result) {
			var tabsPanelType = GameReflection.TabsPanelType;
			if (tabsPanelType == null) return false;

			// Find TabsPanel component in popup
			var components = root.GetComponentsInChildren<Component>(true);
			object tabsPanel = null;

			foreach (var comp in components) {
				if (comp != null && comp.GetType() == tabsPanelType) {
					// Check if this TabsPanel is inside a nested child popup that's inactive
					var owningPopup = FindOwningPopup(comp.transform, root.transform);
					if (owningPopup != null && !owningPopup.activeInHierarchy) {
						continue;
					}

					tabsPanel = comp;
					break;
				}
			}

			if (tabsPanel == null) return false;

			// Get buttons array from TabsPanel
			var buttonsField = GameReflection.TabsPanelButtonsField;
			var tabsButtonArray = buttonsField?.GetValue(tabsPanel) as Array;

			if (tabsButtonArray == null || tabsButtonArray.Length < 2) return false;

			// Get the Unity Button from each TabsButton
			var buttonField = GameReflection.TabsButtonButtonField;

			foreach (var tabsButton in tabsButtonArray) {
				if (tabsButton == null) continue;

				var unityButton = buttonField?.GetValue(tabsButton) as Button;
				if (unityButton != null) {
					result.TabButtons.Add(unityButton);
				}
			}

			if (result.TabButtons.Count < 2) return false;

			// Store TabsPanel reference for active content lookup
			result.TabsPanelRef = tabsPanel;
			result.IsTabbedPopup = true;
			result.Panels.Add(root); // Panel 0 = Tabs
			result.Panels.Add(root); // Panel 1 = Content

			return true;
		}

		/// <summary>
		/// Find the active content panel for the current tab.
		/// </summary>
		public static Transform FindActiveContentPanel(object tabsPanelRef) {
			if (tabsPanelRef == null) return null;

			try {
				var currentField = GameReflection.TabsPanelCurrentField;
				var currentTabsButton = currentField?.GetValue(tabsPanelRef);

				if (currentTabsButton != null) {
					var contentField = GameReflection.TabsButtonContentField;
					var content = contentField?.GetValue(currentTabsButton) as GameObject;

					if (content != null) {
						return content.transform;
					}
				}
			} catch {
				// Silently handle errors getting tab content
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
			object tabsPanelRef) {
			var elements = new List<Selectable>();

			if (panel == null) return elements;

			// Handle tabbed popup specially
			if (isTabbedPopup) {
				if (panelIndex == 0) {
					// Panel 0: Tabs only
					foreach (var tab in tabButtons) {
						if (tab != null) {
							elements.Add(tab);
						}
					}
				} else {
					// Panel 1: Content (only from the active tab's content)
					var activeContentPanel = FindActiveContentPanel(tabsPanelRef);

					// No current tab means the game has not initialized its tabs yet - our
					// popup-shown handler can run first. Every tab's content is still active
					// at that point, so searching the popup root would snapshot the union of
					// all tabs. Those controls go inactive a moment later without ever having
					// run Start(), leaving them readable but inert. Report none instead; the
					// caller falls back to the tabs so the player can pick one.
					if (activeContentPanel == null) return elements;

					var selectables = activeContentPanel.GetComponentsInChildren<Selectable>(true);

					foreach (var sel in selectables) {
						if (!sel.gameObject.activeInHierarchy) continue;
						if (!sel.interactable) continue;
						if (ShouldIgnoreElement(sel)) continue;
						if (tabButtons.Contains(sel)) continue;
						if (ShouldSkipHaulerSlotElement(sel)) continue;

						string text = GetElementText(sel);
						if (string.IsNullOrEmpty(text)) continue;

						elements.Add(sel);
					}
				}
				return elements;
			}

			// Standard (non-tabbed) panel handling
			var allSelectables = panel.GetComponentsInChildren<Selectable>(true);

			foreach (var sel in allSelectables) {
				if (ShouldSkipElement(sel, panel.transform, isPopup)) continue;

				string text = GetElementText(sel);
				if (string.IsNullOrEmpty(text)) continue;

				elements.Add(sel);
			}

			return elements;
		}

		/// <summary>
		/// A hauler priority row exposes three Selectables (minus, input, plus). Collapse
		/// it to just the input field, which +/- then adjusts, so skip the two buttons.
		/// Also skip every element of a hidden pooled row: the panel deactivates the slot
		/// root but not its children, so they would otherwise show up as bare "0" fields.
		/// </summary>
		public static bool ShouldSkipHaulerSlotElement(Selectable sel) {
			var slot = HaulersReflection.GetSlotFor(sel);
			if (slot == null) return false;

			if (!HaulersReflection.IsSlotVisible(slot)) return true;

			return !HaulersReflection.IsSlotRepresentative(slot, sel);
		}

		/// <summary>
		/// Build the "Building, priority N" label for a hauler priority row's input field,
		/// or null if the element is not one. Rows greyed out by their group's toggle are
		/// marked inactive.
		/// </summary>
		private static string TryGetHaulerSlotLabel(Selectable element) {
			var slot = HaulersReflection.GetSlotFor(element);
			if (slot == null) return null;
			if (!HaulersReflection.IsSlotRepresentative(slot, element)) return null;

			if (!HaulersReflection.TryGetSlotValues(slot, out string label, out int priority))
				return null;

			string text = Strings.Get("nav.storage.hauler_priority", label, priority);

			if (HaulersReflection.IsSlotInactive(slot))
				text = Strings.Get("nav.storage.hauler_priority_inactive", text);

			return text;
		}

		/// <summary>
		/// Unified check for whether to skip an element during navigation.
		/// Consolidates visibility, interactability, and filter checks.
		/// </summary>
		private static bool ShouldSkipElement(Selectable sel, Transform boundary, bool isPopup) {
			if (ShouldSkipHaulerSlotElement(sel)) return true;

			// For menus, skip inactive elements (check full hierarchy)
			// For popups, allow animated elements (parent inactive) but skip directly deactivated
			if (!isPopup && !sel.gameObject.activeInHierarchy) return true;
			if (isPopup && !sel.gameObject.activeSelf) return true;

			// For popups, check hidden/demo-only elements (single hierarchy walk)
			if (isPopup) {
				var (filtered, _) = IsElementFiltered(sel.transform, boundary);
				if (filtered) return true;
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
		private static (bool filtered, string reason) IsElementFiltered(Transform t, Transform boundary) {
			var current = t;
			while (current != null && current != boundary) {
				// Check if GameObject is deactivated
				if (!current.gameObject.activeSelf)
					return (true, "hidden (inactive)");

				// Check if CanvasGroup is hiding this element (alpha = 0)
				var canvasGroup = current.GetComponent<CanvasGroup>();
				if (canvasGroup != null && canvasGroup.alpha < 0.01f)
					return (true, "hidden (alpha=0)");

				// Check for DemoElement component (demo-only UI)
				var components = current.GetComponents<Component>();
				foreach (var comp in components) {
					if (comp != null && comp.GetType().Name == "DemoElement") {
						// Cache the FieldInfo on first lookup
						if (!_demoElementFieldLookedUp) {
							_demoElementInFullGameField = comp.GetType().GetField("inFullGame",
								System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
							_demoElementFieldLookedUp = true;
						}

						if (_demoElementInFullGameField != null) {
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
		public static bool ShouldIgnoreElement(Selectable element) {
			string name = element.gameObject.name;
			foreach (var ignored in IgnoredElementNames) {
				if (name.IndexOf(ignored, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			return false;
		}

		// ========================================
		// TEXT EXTRACTION
		// ========================================

		/// <summary>
		/// Get text from any UI component's TMP_Text children.
		/// Useful for non-Selectable elements like WikiSlot, WikiCategoryButton.
		/// </summary>
		public static string GetTextFromTransform(Transform t) {
			if (t == null) return null;

			var tmpText = t.GetComponentInChildren<TMP_Text>();
			if (tmpText != null && !string.IsNullOrEmpty(tmpText.text)) {
				return tmpText.text;
			}

			return CleanObjectName(t.gameObject.name);
		}

		/// <summary>
		/// Get the primary text content of a UI element.
		/// </summary>
		public static string GetElementText(Selectable element) {
			if (element == null) return null;

			// Hauler priority rows read as "Building, priority N" rather than as a bare
			// number in a text field.
			string haulerLabel = TryGetHaulerSlotLabel(element);
			if (!string.IsNullOrEmpty(haulerLabel)) return haulerLabel;

			// Icon-only DLC shop buttons (Frogs/Bats in main menu) have no inner text,
			// so pull the localized name + description from their DLCConfig.
			string dlcLabel = DlcShopButtonReflection.TryGetLabel(element);
			if (!string.IsNullOrEmpty(dlcLabel)) return dlcLabel;

			// Handle dropdowns FIRST - their inner text is the value, not a label
			if (element is TMP_Dropdown dropdown && dropdown.options.Count > 0) {
				string value = dropdown.options[dropdown.value].text;
				string label = FindLabelFromParent(element.transform);
				if (!string.IsNullOrEmpty(label)) {
					return Strings.Get("util.ui_element.dropdown_with_label", label, value);
				}
				return value;
			}

			// Handle sliders - their inner text might just be the value
			if (element is Slider slider) {
				string label = FindLabelFromParent(element.transform);
				if (!string.IsNullOrEmpty(label)) {
					return label;
				}
				var tmpText = element.GetComponentInChildren<TMP_Text>();
				if (tmpText != null && !string.IsNullOrEmpty(tmpText.text) && !IsNumericText(tmpText.text)) {
					return tmpText.text;
				}
				return CleanObjectName(element.gameObject.name);
			}

			// Try TextMeshPro first (most common)
			var innerTmpText = element.GetComponentInChildren<TMP_Text>();
			if (innerTmpText != null && !string.IsNullOrEmpty(innerTmpText.text)) {
				string innerText = innerTmpText.text;

				// Special case: "Pick" buttons need context from parent slot
				if (innerText.Equals("Pick", StringComparison.OrdinalIgnoreCase)) {
					string context = TryGetPickButtonContext(element.gameObject);
					if (!string.IsNullOrEmpty(context)) {
						return Strings.Get("util.ui_element.pick_context", context);
					}
				}

				if (!IsGenericText(innerText)) {
					return innerText;
				}
			}

			// Try legacy UI Text
			var uiText = element.GetComponentInChildren<Text>();
			if (uiText != null && !string.IsNullOrEmpty(uiText.text)) {
				string innerText = uiText.text;
				if (!IsGenericText(innerText)) {
					return innerText;
				}
			}

			// For input fields, get text or placeholder
			if (element is TMP_InputField inputField) {
				if (!string.IsNullOrEmpty(inputField.text))
					return inputField.text;
				if (inputField.placeholder != null) {
					var placeholder = inputField.placeholder.GetComponent<TMP_Text>();
					if (placeholder != null)
						return placeholder.text;
				}
			}

			// Try to find label from parent/sibling
			string parentLabel = FindLabelFromParent(element.transform);
			if (!string.IsNullOrEmpty(parentLabel)) {
				return parentLabel;
			}

			// Fallback to cleaned object name
			return CleanObjectName(element.gameObject.name);
		}

		/// <summary>
		/// Find a label by looking at parent container's children (siblings of the element).
		/// </summary>
		private static string FindLabelFromParent(Transform elementTransform) {
			var parent = elementTransform.parent;
			if (parent == null) return null;

			// Look for sibling TMP_Text elements that could be labels
			foreach (Transform sibling in parent) {
				if (sibling == elementTransform) continue;
				if (sibling.GetComponent<Selectable>() != null) continue;

				var labelText = sibling.GetComponent<TMP_Text>();
				if (labelText != null && !string.IsNullOrEmpty(labelText.text) && !IsGenericText(labelText.text)) {
					return labelText.text;
				}

				var childText = sibling.GetComponentInChildren<TMP_Text>();
				if (childText != null && !string.IsNullOrEmpty(childText.text) && !IsGenericText(childText.text)) {
					return childText.text;
				}

				var legacyText = sibling.GetComponent<Text>();
				if (legacyText != null && !string.IsNullOrEmpty(legacyText.text) && !IsGenericText(legacyText.text)) {
					return legacyText.text;
				}
			}

			// Try grandparent if parent didn't have labels
			var grandparent = parent.parent;
			if (grandparent != null) {
				foreach (Transform uncle in grandparent) {
					if (uncle == parent) continue;
					if (uncle.GetComponent<Selectable>() != null) continue;

					var labelText = uncle.GetComponent<TMP_Text>();
					if (labelText != null && !string.IsNullOrEmpty(labelText.text) && !IsGenericText(labelText.text)) {
						return labelText.text;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Try to get context for a "Pick" button by finding the item name
		/// in sibling TMP_Text components within the parent slot.
		/// </summary>
		private static string TryGetPickButtonContext(GameObject buttonObject) {
			var parent = buttonObject.transform.parent;
			if (parent == null) return null;

			// Common paths for item names in slot containers
			string[] namePaths = { "Name", "Name/Text", "BG2/Header", "Header" };

			foreach (var path in namePaths) {
				var nameTransform = parent.Find(path);
				if (nameTransform != null) {
					var tmpText = nameTransform.GetComponent<TMP_Text>();
					if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text)) {
						return tmpText.text;
					}
				}
			}

			// Fallback: Search all sibling TMP_Text for non-trivial content
			foreach (Transform sibling in parent) {
				if (sibling.gameObject == buttonObject) continue;

				var tmpText = sibling.GetComponentInChildren<TMP_Text>();
				if (tmpText != null && !string.IsNullOrWhiteSpace(tmpText.text)) {
					string text = tmpText.text.Trim();
					// Skip if it's just another generic label
					if (!text.Equals("Pick", StringComparison.OrdinalIgnoreCase) && text.Length > 2)
						return text;
				}
			}

			return null;
		}

		private static bool IsNumericText(string text) {
			if (string.IsNullOrEmpty(text)) return false;
			return float.TryParse(text, out _);
		}

		private static bool IsGenericText(string text) {
			if (string.IsNullOrEmpty(text)) return true;

			foreach (var generic in GenericTexts) {
				if (text.Equals(generic, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Convert object name to readable text.
		/// </summary>
		public static string CleanObjectName(string name) {
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
		public static string GetElementType(Selectable element) {
			// Hauler priority rows already announce as "Building, priority N" - calling
			// them a text field adds nothing.
			if (HaulersReflection.GetSlotFor(element) != null)
				return null;

			if (element is Button) {
				// Check if this Button is wrapped by a ToggleButton (game's custom toggle)
				if (FindToggleButton(element) != null)
					return Strings.Get("util.ui_element.checkbox");
				return Strings.Get("util.ui_element.button");
			}

			if (element is Toggle toggle) {
				if (toggle.group != null)
					return Strings.Get("util.ui_element.radio_button");
				return Strings.Get("util.ui_element.checkbox");
			}

			if (element is Slider)
				return Strings.Get("util.ui_element.slider");

			if (element is TMP_Dropdown || element is Dropdown)
				return Strings.Get("util.ui_element.dropdown");

			if (element is TMP_InputField || element is InputField)
				return Strings.Get("util.ui_element.text_field");

			return Strings.Get("util.ui_element.control");
		}

		/// <summary>
		/// Get the current state of a UI element.
		/// </summary>
		public static string GetElementState(Selectable element) {
			if (element is Toggle toggle) {
				return toggle.isOn ? Strings.Get("common.checked") : Strings.Get("common.unchecked");
			}

			if (element is Button) {
				var toggleButton = FindToggleButton(element);
				if (toggleButton != null) {
					bool? state = GetToggleButtonState(toggleButton);
					if (state.HasValue) return state.Value ? Strings.Get("common.checked") : Strings.Get("common.unchecked");
				}
			}

			if (element is Slider slider) {
				int percent = Mathf.RoundToInt(slider.normalizedValue * 100);
				return Strings.Get("util.ui_element.percent", percent);
			}

			return null;
		}

		// ========================================
		// TOGGLEBUTTON DETECTION
		// ========================================

		/// <summary>
		/// Check if a Button element is wrapped by the game's ToggleButton component.
		/// Returns the ToggleButton component if found, null otherwise.
		/// </summary>
		public static Component FindToggleButton(Selectable element) {
			if (!(element is Button)) return null;
			var toggleButtonType = GameReflection.ToggleButtonType;
			if (toggleButtonType == null) return null;

			// Check same GameObject (Button and ToggleButton on same object)
			var toggleButton = element.GetComponent(toggleButtonType);
			if (toggleButton != null) return toggleButton;

			// Check parent (ToggleButton might be on parent of the Button)
			var parent = element.transform.parent;
			if (parent != null) {
				toggleButton = parent.GetComponent(toggleButtonType);
				if (toggleButton != null) return toggleButton;
			}

			return null;
		}

		/// <summary>
		/// Get the isOn state of a ToggleButton component via reflection.
		/// </summary>
		public static bool? GetToggleButtonState(Component toggleButton) {
			var method = GameReflection.ToggleIsOnMethod;
			if (method == null || toggleButton == null) return null;
			try {
				return (bool)method.Invoke(toggleButton, null);
			} catch {
				return null;
			}
		}

		// ========================================
		// SECTION DETECTION
		// ========================================

		/// <summary>
		/// Find the section name for a UI element by walking up the hierarchy.
		/// Looks for ancestors whose name contains "Section" and returns a readable name.
		/// </summary>
		public static string FindSectionName(Transform element) {
			var current = element.parent;
			while (current != null) {
				string name = current.name;
				if (name.IndexOf("Section", StringComparison.OrdinalIgnoreCase) >= 0) {
					// Try to find a header text label in this section
					string headerText = FindSectionHeaderText(current);
					if (!string.IsNullOrEmpty(headerText)) return headerText;

					// Fall back to cleaned name without "Section" suffix
					int idx = name.IndexOf("Section", StringComparison.OrdinalIgnoreCase);
					if (idx > 0) name = name.Substring(0, idx);
					return CleanObjectName(name);
				}
				current = current.parent;
			}
			return null;
		}

		/// <summary>
		/// Look for a header TMP_Text in a section that's a direct child and not part of a Selectable.
		/// </summary>
		private static string FindSectionHeaderText(Transform section) {
			foreach (Transform child in section) {
				// Skip if this child contains interactive elements
				if (child.GetComponent<Selectable>() != null) continue;

				var text = child.GetComponent<TMP_Text>();
				if (text != null && !string.IsNullOrEmpty(text.text) && !IsGenericText(text.text)) {
					return text.text;
				}
			}
			return null;
		}
	}

	/// <summary>
	/// Result of panel discovery operation.
	/// </summary>
	public class PanelDiscoveryResult {
		public List<GameObject> Panels { get; } = new List<GameObject>();
		public List<Selectable> TabButtons { get; } = new List<Selectable>();
		public object TabsPanelRef { get; set; }
		public bool IsTabbedPopup { get; set; }
	}
}
