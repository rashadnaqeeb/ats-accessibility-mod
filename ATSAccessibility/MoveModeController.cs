using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Controls building move mode: relocating existing buildings.
    /// Works with MapNavigator for cursor position.
    /// </summary>
    public class MoveModeController
    {
        private bool _isActive = false;
        private object _movingBuilding = null;
        private string _buildingName = null;
        private Vector2Int _originalPosition;
        private int _originalRotation;
        private int _currentRotation;

        // Reference to map navigator for cursor position
        private readonly MapNavigator _mapNavigator;

        /// <summary>
        /// Whether move mode is currently active.
        /// </summary>
        public bool IsActive => _isActive;

        public MoveModeController(MapNavigator mapNavigator)
        {
            _mapNavigator = mapNavigator;
        }

        /// <summary>
        /// Enter move mode for a building at the specified position.
        /// </summary>
        public void EnterMoveMode(object building)
        {
            if (building == null)
            {
                Speech.Say("No building here");
                return;
            }

            // Check if building can be moved
            if (!GameReflection.CanMovePlacedBuilding(building))
            {
                Speech.Say("Unmovable");
                Debug.Log($"[ATSAccessibility] Building cannot be moved");
                return;
            }

            // Store original state for potential cancellation
            _originalPosition = GameReflection.GetBuildingGridPosition(building);
            _originalRotation = GameReflection.GetBuildingRotation(building);
            _currentRotation = _originalRotation;

            _movingBuilding = building;
            _buildingName = GameReflection.GetDisplayName(building) ?? "Building";

            // Lift building from grid (removes from grid but keeps the object)
            GameReflection.LiftBuilding(building);

            _isActive = true;

            // Get building size for extension announcement
            var buildingModel = GameReflection.GetBuildingModel(building);
            Vector2Int size = buildingModel != null
                ? GameReflection.GetBuildingSize(buildingModel)
                : new Vector2Int(1, 1);

            bool isRotated = (_currentRotation % 2) == 1;
            int extendEast = (isRotated ? size.y : size.x) - 1;
            int extendNorth = (isRotated ? size.x : size.y) - 1;
            string extension = GetExtensionAnnouncement(extendEast, extendNorth);

            Speech.Say($"Move mode: {_buildingName}, {extension}");
            Debug.Log($"[ATSAccessibility] Entered move mode for: {_buildingName} at ({_originalPosition.x}, {_originalPosition.y})");
        }

        /// <summary>
        /// Exit move mode, either placing at new position or cancelling.
        /// </summary>
        /// <param name="cancel">True to restore original position, false to place at cursor.</param>
        public void ExitMoveMode(bool cancel)
        {
            if (!_isActive || _movingBuilding == null) return;

            if (cancel)
            {
                // Restore original position and rotation
                GameReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
                if (_currentRotation != _originalRotation)
                {
                    GameReflection.RotateBuilding(_movingBuilding, _originalRotation);
                }
                GameReflection.PlaceBuildingOnGrid(_movingBuilding);

                InputBlocker.BlockCancelOnce = true;
                Speech.Say("Move cancelled");
                Debug.Log($"[ATSAccessibility] Move cancelled, restored to ({_originalPosition.x}, {_originalPosition.y})");
            }
            else
            {
                // Try to place at current cursor position
                int x = _mapNavigator.CursorX;
                int y = _mapNavigator.CursorY;
                Vector2Int newPos = new Vector2Int(x, y);

                // Set position to cursor
                GameReflection.SetBuildingPosition(_movingBuilding, newPos);

                // Check if placement is valid
                if (!GameReflection.CanPlaceBuilding(_movingBuilding))
                {
                    // Restore position (building stays lifted for another attempt)
                    GameReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
                    Speech.Say("Cannot place here");
                    Debug.Log($"[ATSAccessibility] Cannot place {_buildingName} at ({x}, {y})");
                    return; // Don't exit move mode, let user try another position
                }

                // Place building on grid at new position
                GameReflection.PlaceBuildingOnGrid(_movingBuilding);

                Speech.Say($"{_buildingName} moved");
                Debug.Log($"[ATSAccessibility] Moved {_buildingName} from ({_originalPosition.x}, {_originalPosition.y}) to ({x}, {y})");
            }

            // Clean up state
            _isActive = false;
            _movingBuilding = null;
            _buildingName = null;
        }

        /// <summary>
        /// Process a key event for move mode.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isActive) return false;

            switch (keyCode)
            {
                case KeyCode.R:
                    RotateBuilding();
                    return true;

                case KeyCode.Space:
                    ExitMoveMode(false); // Try to place
                    return true;

                case KeyCode.Escape:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ExitMoveMode(true); // Cancel
                    return true;

                // Let arrow keys pass through to MapNavigator
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                    return false;

                // Let other map keys pass through
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
            if (_movingBuilding == null) return;

            // Check if building can be rotated
            var buildingModel = GameReflection.GetBuildingModel(_movingBuilding);
            if (buildingModel != null && !GameReflection.CanRotateBuildingModel(buildingModel))
            {
                Speech.Say("Cannot rotate");
                return;
            }

            // Increment rotation
            _currentRotation = (_currentRotation + 1) % 4;
            GameReflection.RotateBuilding(_movingBuilding, _currentRotation);

            string direction = GetCardinalDirection(_currentRotation);

            // Get building size and adjust for rotation
            Vector2Int baseSize = buildingModel != null
                ? GameReflection.GetBuildingSize(buildingModel)
                : new Vector2Int(1, 1);
            bool isRotated = (_currentRotation % 2) == 1;
            int extendEast = (isRotated ? baseSize.y : baseSize.x) - 1;
            int extendNorth = (isRotated ? baseSize.x : baseSize.y) - 1;

            string extension = GetExtensionAnnouncement(extendEast, extendNorth);

            Speech.Say($"{direction}, {extension}");
            Debug.Log($"[ATSAccessibility] Building rotated to {_currentRotation} ({direction})");
        }

        /// <summary>
        /// Get a readable announcement of how far the building extends from cursor.
        /// </summary>
        private string GetExtensionAnnouncement(int east, int north)
        {
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
