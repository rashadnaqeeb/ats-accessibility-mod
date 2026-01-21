using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Controls building placement mode: rotation, placement, and removal.
    /// Works with MapNavigator for cursor position.
    /// </summary>
    public class BuildModeController : IKeyHandler
    {
        private bool _isActive = false;
        private object _selectedBuildingModel = null;
        private string _selectedBuildingName = null;
        private int _rotation = 0;  // 0-3, maps to cardinal directions

        // Reference to map navigator for cursor position
        private readonly MapNavigator _mapNavigator;

        // Reference to building menu for returning
        private readonly BuildingMenuPanel _buildingMenuPanel;

        /// <summary>
        /// Whether build mode is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        public BuildModeController(MapNavigator mapNavigator, BuildingMenuPanel buildingMenuPanel)
        {
            _mapNavigator = mapNavigator;
            _buildingMenuPanel = buildingMenuPanel;
        }

        /// <summary>
        /// Enter build mode with a selected building.
        /// </summary>
        public void EnterBuildMode(object buildingModel, string buildingName)
        {
            if (buildingModel == null)
            {
                Debug.LogError("[ATSAccessibility] EnterBuildMode called with null model");
                return;
            }

            _selectedBuildingModel = buildingModel;
            _selectedBuildingName = buildingName;
            _rotation = 0;
            _isActive = true;

            // Get building size for initial announcement
            Vector2Int size = GameReflection.GetBuildingSize(buildingModel);
            int extendEast = size.x - 1;
            int extendNorth = size.y - 1;
            string extension = GetExtensionAnnouncement(extendEast, extendNorth);

            Speech.Say($"Build mode: {buildingName}, {extension}");
            Debug.Log($"[ATSAccessibility] Entered build mode for: {buildingName} (size {size.x}x{size.y})");
        }

        /// <summary>
        /// Exit build mode and clean up.
        /// </summary>
        public void ExitBuildMode()
        {
            if (!_isActive) return;

            _isActive = false;
            _selectedBuildingModel = null;
            _selectedBuildingName = null;
            _rotation = 0;

            InputBlocker.BlockCancelOnce = true;
            Speech.Say("Exited build mode");
            Debug.Log("[ATSAccessibility] Exited build mode");
        }

        /// <summary>
        /// Process a key event for build mode (IKeyHandler).
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isActive) return false;

            switch (keyCode)
            {
                case KeyCode.R:
                    RotateBuilding();
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

                case KeyCode.Escape:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExitBuildMode();
                    return true;

                // Pass to MapNavigator for cursor movement
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    return false;

                // Pass to MapScanner for building/resource scanning
                case KeyCode.PageUp:
                case KeyCode.PageDown:
                    return false;

                // Pass to MapNavigator for position/tile info
                case KeyCode.K:
                case KeyCode.I:
                    return false;

                default:
                    // Consume other keys to prevent interference
                    return true;
            }
        }

        /// <summary>
        /// Rotate the building and announce the new direction.
        /// </summary>
        private void RotateBuilding()
        {
            // Check if the building model allows rotation
            if (!GameReflection.CanRotateBuildingModel(_selectedBuildingModel))
            {
                Speech.Say("Cannot rotate");
                return;
            }

            _rotation = (_rotation + 1) % 4;
            string direction = GetCardinalDirection(_rotation);

            // Get building size and adjust for rotation
            Vector2Int baseSize = GameReflection.GetBuildingSize(_selectedBuildingModel);
            bool isRotated = (_rotation % 2) == 1;
            int extendEast = (isRotated ? baseSize.y : baseSize.x) - 1;
            int extendNorth = (isRotated ? baseSize.x : baseSize.y) - 1;

            // Build extension announcement
            string extension = GetExtensionAnnouncement(extendEast, extendNorth);

            Speech.Say($"{direction}, {extension}");
            Debug.Log($"[ATSAccessibility] Building rotated to {_rotation} ({direction}), extends {extendEast}E {extendNorth}N");
        }

        /// <summary>
        /// Get a readable announcement of how far the building extends from cursor.
        /// </summary>
        private string GetExtensionAnnouncement(int east, int north)
        {
            // Handle 1x1 buildings
            if (east == 0 && north == 0)
            {
                return "1 tile";
            }

            var parts = new System.Collections.Generic.List<string>();

            if (east > 0)
            {
                parts.Add($"{east} east");
            }
            if (north > 0)
            {
                parts.Add($"{north} north");
            }

            return "extends " + string.Join(", ", parts);
        }

        /// <summary>
        /// Attempt to place the building at the current cursor position.
        /// </summary>
        private void PlaceBuilding()
        {
            if (_selectedBuildingModel == null || _mapNavigator == null)
            {
                Speech.Say("Cannot place");
                return;
            }

            // Check if we can still construct this building type
            if (!GameReflection.CanConstructBuilding(_selectedBuildingModel))
            {
                Speech.Say($"{_selectedBuildingName} at maximum, cannot place more");
                return;
            }

            // Get cursor position
            int x = _mapNavigator.CursorX;
            int y = _mapNavigator.CursorY;

            // Create the building at the cursor position
            var building = GameReflection.CreateBuilding(_selectedBuildingModel, _rotation);
            if (building == null)
            {
                Speech.Say("Failed to create building");
                Debug.LogError("[ATSAccessibility] Failed to create building instance");
                return;
            }

            // Set position
            GameReflection.SetBuildingPosition(building, new Vector2Int(x, y));

            // Check if placement is valid
            if (!GameReflection.CanPlaceBuilding(building))
            {
                // Remove the building since we can't place it
                GameReflection.RemoveBuilding(building, false);
                Speech.Say("Cannot place here");
                Debug.Log($"[ATSAccessibility] Cannot place {_selectedBuildingName} at ({x}, {y})");
                return;
            }

            // Finalize placement
            GameReflection.FinalizeBuildingPlacement(building);

            Speech.Say($"{_selectedBuildingName} placed");
            Debug.Log($"[ATSAccessibility] Placed {_selectedBuildingName} at ({x}, {y}) rotation {_rotation}");

            // Check if we can build more
            if (!GameReflection.CanConstructBuilding(_selectedBuildingModel))
            {
                Speech.Say($"Maximum {_selectedBuildingName} reached");
                ExitBuildMode();
            }
        }

        /// <summary>
        /// Remove an unfinished building at the current cursor position.
        /// </summary>
        private void RemoveBuildingAtCursor()
        {
            if (_mapNavigator == null)
            {
                Speech.Say("Cannot remove");
                return;
            }

            int x = _mapNavigator.CursorX;
            int y = _mapNavigator.CursorY;

            // Check if there's a building at cursor
            var building = GameReflection.GetBuildingAtPosition(x, y);
            if (building == null)
            {
                Speech.Say("No building here");
                return;
            }

            // Check if it's unfinished (under construction)
            if (!GameReflection.IsBuildingUnfinished(building))
            {
                Speech.Say("Building already complete, use game controls to remove");
                return;
            }

            // Get the building name for announcement
            string buildingName = GameReflection.GetDisplayName(building) ?? "Building";

            // Remove with refund
            GameReflection.RemoveBuilding(building, true);

            Speech.Say($"{buildingName} removed");
            Debug.Log($"[ATSAccessibility] Removed building at ({x}, {y})");
        }

        /// <summary>
        /// Return to the building menu without exiting build mode entirely.
        /// </summary>
        private void ReturnToMenu()
        {
            _isActive = false;
            _selectedBuildingModel = null;
            _selectedBuildingName = null;

            // Open the menu
            _buildingMenuPanel?.Open();
        }

        /// <summary>
        /// Get the cardinal direction name for a rotation value.
        /// </summary>
        private string GetCardinalDirection(int rotation)
        {
            return rotation switch
            {
                0 => "North",
                1 => "East",
                2 => "South",
                3 => "West",
                _ => "Unknown"
            };
        }
    }
}
