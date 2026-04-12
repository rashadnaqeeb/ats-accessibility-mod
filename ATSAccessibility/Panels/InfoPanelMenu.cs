using ATSAccessibility.Utils;
using ATSAccessibility.Core;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Unified menu for accessing information panels (Stats, Resources, Mysteries, Villagers, Announcements).
	/// Opened with F1 from the settlement map.
	/// </summary>
	public class InfoPanelMenu: MenuBase {
		private enum MenuPanel {
			Resources,
			Villagers,
			Workers,
			Stats,
			Modifiers,
			Announcements
		}

		private static readonly string[] _menuLabelKeys = {
			"common.resources",
			"common.villagers",
			"common.workers",
			"panel.info.menu.stats",
			"common.modifiers",
			"panel.info.menu.announcements"
		};

		private readonly StatsPanel _statsPanel;
		private readonly SettlementResourcePanel _resourcePanel;
		private readonly MysteriesPanel _mysteriesPanel;
		private readonly VillagersPanel _villagersPanel;
		private readonly WorkersPanel _workersPanel;
		private readonly AnnouncementsSettingsPanel _announcementsPanel;

		private MenuPanel? _activeChildPanel;

		// Flag to suppress announcement when opening directly to a child panel
		private bool _directOpen;

		/// <summary>
		/// Whether a child panel (Stats, Resources, or Mysteries) is currently open.
		/// </summary>
		public bool IsInChildPanel => _activeChildPanel.HasValue;

		public InfoPanelMenu(StatsPanel statsPanel, SettlementResourcePanel resourcePanel, MysteriesPanel mysteriesPanel, VillagersPanel villagersPanel, WorkersPanel workersPanel, AnnouncementsSettingsPanel announcementsPanel) {
			_statsPanel = statsPanel;
			_resourcePanel = resourcePanel;
			_mysteriesPanel = mysteriesPanel;
			_villagersPanel = villagersPanel;
			_workersPanel = workersPanel;
			_announcementsPanel = announcementsPanel;
		}

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("panel.info.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _menuLabelKeys.Length;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _menuLabelKeys.Length) return null;
			return Strings.Get(_menuLabelKeys[index]);
		}

		protected override void RefreshData() { } // Static list

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			OpenSelectedPanel();
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// F-key panel switching - close self and pass through to open target panel
			if (keyCode == KeyCode.F2 || keyCode == KeyCode.F3) {
				Close();
				return false; // Let SettlementKeyHandler open the target panel
			}
			// F1 closes own panel (works from child panels too)
			if (keyCode == KeyCode.F1) {
				SoundManager.PlayButtonClick();
				Close();
				return true;
			}

			// If a child panel is open, delegate all keys
			if (_activeChildPanel.HasValue) {
				switch (keyCode) {
					case KeyCode.LeftArrow:
						// Try to let child handle Left first (for internal navigation)
						if (ProcessChildPanelKey(keyCode)) {
							return true; // Child handled it (was in nested view)
						}
						// Child returned false (at root level), return to menu
						CloseActiveChildPanel();
						AnnounceCurrentItem();
						return true;

					case KeyCode.Escape:
						// Let child handle Escape first (e.g., to clear search)
						if (ProcessChildPanelKey(keyCode)) {
							return true; // Child handled it
						}
						// Close entire overlay
						SoundManager.PlayButtonClick();
						Close();
						return true;

					default:
						// Alt+I: announce resource description when in resource panel
						if (modifiers.Alt && keyCode == KeyCode.I
							&& _activeChildPanel == MenuPanel.Resources) {
							_resourcePanel?.AnnounceCurrentItemDescription();
							return true;
						}
						// Delegate to child panel
						return ProcessChildPanelKey(keyCode);
				}
			}

			// Right arrow at root: open panel (same as Enter)
			if (keyCode == KeyCode.RightArrow) {
				if (GetItemCount() > 0)
					OpenSelectedPanel();
				return true;
			}

			return null; // Proceed with normal MenuBase processing
		}

		protected override EscapeAction OnEscape() {
			SoundManager.PlayButtonClick();
			return EscapeAction.Close;
		}

		protected override void OnOpened() {
			SoundManager.PlayPopupShow();
		}

		protected override string GetOpenAnnouncement() {
			if (_directOpen) {
				_directOpen = false;
				return null; // Suppress - the child panel will announce
			}

			if (_menuLabelKeys.Length > 0)
				return Strings.Get("panel.info.open", OverlayName, Strings.Get(_menuLabelKeys[0]));
			return OverlayName;
		}

		protected override void OnClosed() {
			CloseActiveChildPanel();
			InputBlocker.BlockCancelOnce = true;
			Speech.Say(Strings.Get("common.closed"));
		}

		// ========================================
		// PUBLIC METHODS
		// ========================================

		/// <summary>
		/// Toggle the info panel menu. If already open, closes it.
		/// Callers should use this instead of Open() for toggle behavior.
		/// </summary>
		public void Toggle() {
			if (IsOpen) {
				SoundManager.PlayButtonClick();
				Close();
				return;
			}

			Open();
		}

		/// <summary>
		/// Open a specific panel directly, bypassing the menu.
		/// If already open with the same panel, closes everything (toggle behavior).
		/// </summary>
		public void OpenStatsPanel() => OpenPanelDirect(MenuPanel.Stats);
		public void OpenModifiersPanel() => OpenPanelDirect(MenuPanel.Modifiers);
		public void OpenVillagersPanel() => OpenPanelDirect(MenuPanel.Villagers);
		public void OpenWorkersPanel() => OpenPanelDirect(MenuPanel.Workers);

		// ========================================
		// CHILD PANEL MANAGEMENT
		// ========================================

		private void OpenPanelDirect(MenuPanel panel) {
			int panelIndex = (int)panel;

			// If already open with this same panel, close everything
			if (IsOpen && _activeChildPanel == panel) {
				SoundManager.PlayButtonClick();
				Close();
				return;
			}

			// If open with different panel or menu, close current child
			if (IsOpen) {
				CloseActiveChildPanel();
			} else {
				_directOpen = true;
				Open();
			}

			CurrentIndex = panelIndex;
			OpenSelectedPanel();
		}

		private void OpenSelectedPanel() {
			var panel = (MenuPanel)CurrentIndex;

			switch (panel) {
				case MenuPanel.Stats:
					_statsPanel?.Open();
					break;
				case MenuPanel.Resources:
					_resourcePanel?.Open();
					break;
				case MenuPanel.Modifiers:
					_mysteriesPanel?.Open();
					break;
				case MenuPanel.Villagers:
					_villagersPanel?.Open();
					break;
				case MenuPanel.Workers:
					_workersPanel?.Open();
					break;
				case MenuPanel.Announcements:
					_announcementsPanel?.Open();
					break;
			}

			_activeChildPanel = panel;
			Debug.Log($"[ATSAccessibility] Opened {panel} panel from info menu");
		}

		private void CloseActiveChildPanel() {
			if (!_activeChildPanel.HasValue) return;

			switch (_activeChildPanel.Value) {
				case MenuPanel.Stats:
					if (_statsPanel?.IsOpen == true)
						_statsPanel.Close();
					break;
				case MenuPanel.Resources:
					if (_resourcePanel?.IsOpen == true)
						_resourcePanel.Close();
					break;
				case MenuPanel.Modifiers:
					if (_mysteriesPanel?.IsOpen == true)
						_mysteriesPanel.Close();
					break;
				case MenuPanel.Villagers:
					if (_villagersPanel?.IsOpen == true)
						_villagersPanel.Close();
					break;
				case MenuPanel.Workers:
					if (_workersPanel?.IsOpen == true)
						_workersPanel.Close();
					break;
				case MenuPanel.Announcements:
					if (_announcementsPanel?.IsOpen == true)
						_announcementsPanel.Close();
					break;
			}

			_activeChildPanel = null;
		}

		private bool ProcessChildPanelKey(KeyCode keyCode) {
			if (!_activeChildPanel.HasValue) return false;

			switch (_activeChildPanel.Value) {
				case MenuPanel.Stats:
					return _statsPanel?.ProcessKeyEvent(keyCode) ?? false;
				case MenuPanel.Resources:
					return _resourcePanel?.ProcessKeyEvent(keyCode) ?? false;
				case MenuPanel.Modifiers:
					return _mysteriesPanel?.ProcessKeyEvent(keyCode) ?? false;
				case MenuPanel.Villagers:
					return _villagersPanel?.ProcessKeyEvent(keyCode) ?? false;
				case MenuPanel.Workers:
					return _workersPanel?.ProcessKeyEvent(keyCode) ?? false;
				case MenuPanel.Announcements:
					return _announcementsPanel?.ProcessKeyEvent(keyCode) ?? false;
			}

			return false;
		}
	}
}
