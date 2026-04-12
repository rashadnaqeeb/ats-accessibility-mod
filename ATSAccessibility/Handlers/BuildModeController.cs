using ATSAccessibility.Panels;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Controls building placement mode: rotation, placement, and removal.
	/// Works with MapNavigator for cursor position.
	/// </summary>
	public class BuildModeController: IKeyHandler, IHelpProvider {
		private bool _isActive = false;
		private object _selectedBuildingModel = null;
		private string _selectedBuildingName = null;
		private int _rotation = 0;  // 0-3, maps to cardinal directions

		// Reference to map navigator for cursor position
		private readonly MapNavigator _mapNavigator;

		// Reference to building menu for returning
		private readonly BuildingMenuPanel _buildingMenuPanel;

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("R", "Rotate clockwise"),
			new HelpEntry("Shift+R", "Rotate counter-clockwise"),
			new HelpEntry("Space", "Place building"),
			new HelpEntry("Shift+Space", "Remove unfinished building"),
			new HelpEntry("Enter", "Place and exit"),
			new HelpEntry("Escape", "Exit build mode"),
			new HelpEntry("Tab", "Return to menu"),
			new HelpEntry("E", "Entrance preview"),
			new HelpEntry("D", "Range preview"),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Filter;
		public string HelpContextName => "Build Mode";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		/// <summary>
		/// Whether build mode is currently active.
		/// </summary>
		public bool IsActive => _isActive;

		public BuildModeController(MapNavigator mapNavigator, BuildingMenuPanel buildingMenuPanel) {
			_mapNavigator = mapNavigator;
			_buildingMenuPanel = buildingMenuPanel;
			MenuBase.OnAnyMenuOpened += () => { if (_isActive) ExitBuildMode(); };
		}

		/// <summary>
		/// Enter build mode with a selected building.
		/// </summary>
		public void EnterBuildMode(object buildingModel, string buildingName) {
			if (buildingModel == null) {
				Debug.LogError("[ATSAccessibility] EnterBuildMode called with null model");
				return;
			}

			_selectedBuildingModel = buildingModel;
			_selectedBuildingName = buildingName;
			_rotation = 0;
			_isActive = true;

			// Get building size for initial announcement
			Vector2Int size = ConstructionReflection.GetBuildingSize(buildingModel);
			int extendEast = size.x - 1;
			int extendNorth = size.y - 1;
			string extension = NavigationUtils.GetExtensionAnnouncement(extendEast, extendNorth);

			Speech.Say(Strings.Get("handler.buildmode.entered", buildingName, extension));
			Debug.Log($"[ATSAccessibility] Entered build mode for: {buildingName} (size {size.x}x{size.y})");
		}

		/// <summary>
		/// Silently clear all state without sounds or speech.
		/// Called on scene unload when game objects may be destroyed.
		/// </summary>
		public void Reset() {
			_isActive = false;
			_selectedBuildingModel = null;
			_selectedBuildingName = null;
			_rotation = 0;
		}

		/// <summary>
		/// Exit build mode and clean up.
		/// </summary>
		public void ExitBuildMode(bool queue = false) {
			if (!_isActive) return;

			_isActive = false;
			_selectedBuildingModel = null;
			_selectedBuildingName = null;
			_rotation = 0;

			SoundManager.PlayBuildingPanelHide();
			InputBlocker.BlockCancelOnce = true;
			Speech.Say(Strings.Get("handler.buildmode.exited"), interrupt: !queue);
			Debug.Log("[ATSAccessibility] Exited build mode");
		}

		/// <summary>
		/// Process a key event for build mode (IKeyHandler).
		/// Returns true if the key was handled.
		/// </summary>
		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!_isActive) return false;

			switch (keyCode) {
				case KeyCode.R:
					RotateBuilding(!modifiers.Shift);
					return true;

				case KeyCode.Space:
					if (modifiers.Shift)
						RemoveBuildingAtCursor();
					else
						PlaceBuilding();
					return true;

				case KeyCode.Tab:
					ReturnToMenu();
					return true;

				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					PlaceBuilding();
					if (_isActive) ExitBuildMode(queue: true);
					return true;

				case KeyCode.Escape:
					ExitBuildMode();
					return true;

				// Entrance preview for building about to be placed
				case KeyCode.E:
					if (_selectedBuildingModel != null) {
						var building = ConstructionReflection.CreateBuilding(_selectedBuildingModel, _rotation);
						if (building != null) {
							ConstructionReflection.SetBuildingPosition(building,
								new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY));
							// Apply visual rotation so entrance Transform is positioned correctly
							ConstructionReflection.RotateBuilding(building, _rotation);
							string preview = EntranceInfoHelper.GetEntrancePreview(
								building, _mapNavigator.CursorX, _mapNavigator.CursorY,
								_selectedBuildingModel, _rotation);
							ConstructionReflection.RemoveBuilding(building, false);
							Speech.Say(preview);
						}
					}
					return true;

				// Building range/orientation info for placement preview
				case KeyCode.D:
					if (_selectedBuildingModel != null) {
						bool canPlace = CanPlaceAtCursor();
						string preview = RangeInfoHelper.GetBuildingRangePreview(
							_selectedBuildingModel,
							_mapNavigator.CursorX,
							_mapNavigator.CursorY,
							_rotation,
							canPlace);
						Speech.Say(preview);
					}
					return true;

				// Consume M to prevent activating move mode while in build mode
				case KeyCode.M:
					return true;

				default:
					// Pass unhandled keys through to other handlers
					return false;
			}
		}

		/// <summary>
		/// Rotate the building and announce the new direction.
		/// </summary>
		private void RotateBuilding(bool clockwise) {
			// Check if the building model allows rotation
			if (!ConstructionReflection.CanRotateBuildingModel(_selectedBuildingModel)) {
				Speech.Say(Strings.Get("common.cannot_rotate"));
				return;
			}

			_rotation = (_rotation + (clockwise ? 3 : 1)) % 4;
			SoundManager.PlayBuildingRotated();
			string direction = NavigationUtils.GetCardinalDirection(_rotation);

			// Get building size and adjust for rotation
			Vector2Int baseSize = ConstructionReflection.GetBuildingSize(_selectedBuildingModel);
			bool isRotated = (_rotation % 2) == 1;
			int extendEast = (isRotated ? baseSize.y : baseSize.x) - 1;
			int extendNorth = (isRotated ? baseSize.x : baseSize.y) - 1;

			// Build extension announcement
			string extension = NavigationUtils.GetExtensionAnnouncement(extendEast, extendNorth);

			Speech.Say(Strings.Get("handler.buildmode.rotated", direction, extension));
			Debug.Log($"[ATSAccessibility] Building rotated to {_rotation} ({direction}), extends {extendEast}E {extendNorth}N");
		}


		/// <summary>
		/// Attempt to place the building at the current cursor position.
		/// </summary>
		private void PlaceBuilding() {
			if (_selectedBuildingModel == null || _mapNavigator == null) {
				Speech.Say(Strings.Get("handler.buildmode.cannot_place"));
				return;
			}

			// Check if we can still construct this building type
			if (!ConstructionReflection.CanConstructBuilding(_selectedBuildingModel)) {
				Speech.Say(Strings.Get("handler.buildmode.at_max", _selectedBuildingName));
				return;
			}

			// Get cursor position
			int x = _mapNavigator.CursorX;
			int y = _mapNavigator.CursorY;

			// Create the building at the cursor position
			var building = ConstructionReflection.CreateBuilding(_selectedBuildingModel, _rotation);
			if (building == null) {
				Speech.Say(Strings.Get("handler.buildmode.failed_create"));
				Debug.LogError("[ATSAccessibility] Failed to create building instance");
				return;
			}

			// Set position
			ConstructionReflection.SetBuildingPosition(building, new Vector2Int(x, y));

			// Extractors need springs removed from the grid before placement check,
			// otherwise IsFieldEmpty fails because the spring occupies the grid
			bool isExtractor = BuildingReflection.IsExtractorModel(_selectedBuildingModel);
			if (isExtractor)
				GameReflection.RemoveSpringsFromGrid();

			try {
				// Check if placement is valid
				if (!ConstructionReflection.CanPlaceBuilding(building)) {
					// Remove the building since we can't place it
					ConstructionReflection.RemoveBuilding(building, false);
					Speech.Say(Strings.Get("common.cannot_place_here"));
					Debug.Log($"[ATSAccessibility] Cannot place {_selectedBuildingName} at ({x}, {y})");
					return;
				}

				// Finalize placement
				ConstructionReflection.FinalizeBuildingPlacement(building);
			} finally {
				if (isExtractor)
					GameReflection.ReturnSpringsOnGrid();
			}

			Speech.Say(Strings.Get("handler.buildmode.placed", _selectedBuildingName));
			Debug.Log($"[ATSAccessibility] Placed {_selectedBuildingName} at ({x}, {y}) rotation {_rotation}");

			// Check if we can build more
			if (!ConstructionReflection.CanConstructBuilding(_selectedBuildingModel)) {
				Speech.Say(Strings.Get("handler.buildmode.max_reached", _selectedBuildingName));
				ExitBuildMode();
			}
		}

		/// <summary>
		/// Remove an unfinished building at the current cursor position.
		/// </summary>
		private void RemoveBuildingAtCursor() {
			if (_mapNavigator == null) {
				Speech.Say(Strings.Get("handler.buildmode.cannot_remove"));
				return;
			}

			int x = _mapNavigator.CursorX;
			int y = _mapNavigator.CursorY;

			// Check if there's a building at cursor
			var building = ConstructionReflection.GetBuildingAtPosition(x, y);
			if (building == null) {
				Speech.Say(Strings.Get("common.no_building_here"));
				return;
			}

			// Check if it's unfinished (under construction)
			if (!ConstructionReflection.IsBuildingUnfinished(building)) {
				Speech.Say(Strings.Get("handler.buildmode.already_complete"));
				return;
			}

			// Get the building name for announcement
			string buildingName = GameReflection.GetDisplayName(building) ?? Strings.Get("common.building");

			// Remove with refund
			ConstructionReflection.RemoveBuilding(building, true);

			Speech.Say(Strings.Get("handler.buildmode.removed", buildingName));
			Debug.Log($"[ATSAccessibility] Removed building at ({x}, {y})");
		}

		/// <summary>
		/// Return to the building menu without exiting build mode entirely.
		/// </summary>
		private void ReturnToMenu() {
			_isActive = false;
			_selectedBuildingModel = null;
			_selectedBuildingName = null;

			// Open the menu
			_buildingMenuPanel?.Toggle();
		}


		/// <summary>
		/// Check if the current building can be placed at the cursor position.
		/// Creates a temporary building to test placement, then removes it.
		/// </summary>
		private bool CanPlaceAtCursor() {
			if (_selectedBuildingModel == null || _mapNavigator == null)
				return false;

			// Check if we can still construct this building type
			if (!ConstructionReflection.CanConstructBuilding(_selectedBuildingModel))
				return false;

			// Create a temporary building to test placement
			var building = ConstructionReflection.CreateBuilding(_selectedBuildingModel, _rotation);
			if (building == null)
				return false;

			// Set position
			ConstructionReflection.SetBuildingPosition(building, new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY));

			// Extractors need springs removed from the grid before placement check
			bool isExtractor = BuildingReflection.IsExtractorModel(_selectedBuildingModel);
			if (isExtractor)
				GameReflection.RemoveSpringsFromGrid();

			// Check if placement is valid
			bool canPlace = ConstructionReflection.CanPlaceBuilding(building);

			if (isExtractor)
				GameReflection.ReturnSpringsOnGrid();

			// Remove the temporary building (no refund)
			ConstructionReflection.RemoveBuilding(building, false);

			return canPlace;
		}
	}
}
