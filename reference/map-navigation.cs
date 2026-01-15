// =============================================================================
// MAP NAVIGATION REFERENCE - Grid-based map exploration patterns
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;

namespace Reference
{
    /// <summary>
    /// Patterns for navigating the game map via grid coordinates
    /// </summary>
    public class MapNavigationReference : MonoBehaviour
    {
        // =====================================================================
        // MAP GRID SYSTEM
        // =====================================================================

        // Map is 70x70 grid (coordinates 0-69)
        public const int MAP_SIZE = 70;
        public const int MIN_COORD = 0;
        public const int MAX_COORD = 69;

        // Current cursor position
        private int _cursorX = 35; // Start in center
        private int _cursorY = 35;

        // Cached service references (must refresh on scene change!)
        private object _mapService;
        private object _gladesService;
        private MethodInfo _getFieldMethod;
        private MethodInfo _getObjectOnMethod;
        private MethodInfo _getGladeMethod;

        // =====================================================================
        // SERVICE CACHING - Refresh on each Game scene load
        // =====================================================================

        public void CacheMapServices(object gameServices)
        {
            if (gameServices == null) return;

            var gsType = gameServices.GetType();

            // Get MapService
            var mapServiceProp = gsType.GetProperty("MapService");
            _mapService = mapServiceProp?.GetValue(gameServices);

            if (_mapService != null)
            {
                var mapType = _mapService.GetType();
                _getFieldMethod = mapType.GetMethod("GetField", new[] { typeof(int), typeof(int) });
                _getObjectOnMethod = mapType.GetMethod("GetObjectOn", new[] { typeof(int), typeof(int) });
            }

            // Get GladesService
            var gladesServiceProp = gsType.GetProperty("GladesService");
            _gladesService = gladesServiceProp?.GetValue(gameServices);

            if (_gladesService != null)
            {
                var gladesType = _gladesService.GetType();
                _getGladeMethod = gladesType.GetMethod("GetGlade", new[] { typeof(Vector2Int) });
            }
        }

        public void ClearServiceCaches()
        {
            _mapService = null;
            _gladesService = null;
            _getFieldMethod = null;
            _getObjectOnMethod = null;
            _getGladeMethod = null;
        }

        // =====================================================================
        // CURSOR MOVEMENT
        // =====================================================================

        public void MoveCursor(int deltaX, int deltaY)
        {
            _cursorX = Mathf.Clamp(_cursorX + deltaX, MIN_COORD, MAX_COORD);
            _cursorY = Mathf.Clamp(_cursorY + deltaY, MIN_COORD, MAX_COORD);

            AnnounceTile(_cursorX, _cursorY);
        }

        public void ResetCursor()
        {
            _cursorX = MAP_SIZE / 2;
            _cursorY = MAP_SIZE / 2;
        }

        public (int x, int y) GetCursorPosition() => (_cursorX, _cursorY);

        // =====================================================================
        // TILE INFORMATION - Live lookups (never cache!)
        // =====================================================================

        /// <summary>
        /// Get the field (tile) at a coordinate
        /// </summary>
        public object GetField(int x, int y)
        {
            if (_mapService == null || _getFieldMethod == null) return null;

            try
            {
                return _getFieldMethod.Invoke(_mapService, new object[] { x, y });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get terrain type at coordinate
        /// </summary>
        public string GetTerrainType(int x, int y)
        {
            var field = GetField(x, y);
            if (field == null) return "Unknown";

            // Field.Type is an enum
            var typeProp = field.GetType().GetProperty("Type");
            if (typeProp == null) return "Unknown";

            var typeValue = typeProp.GetValue(field);
            return typeValue?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Check if tile is traversable
        /// </summary>
        public bool IsTraversable(int x, int y)
        {
            var field = GetField(x, y);
            if (field == null) return false;

            var traversableProp = field.GetType().GetProperty("IsTraversable");
            if (traversableProp == null) return false;

            return (bool)traversableProp.GetValue(field);
        }

        /// <summary>
        /// Get the object on a tile (building, resource, etc.)
        /// </summary>
        public object GetObjectOnTile(int x, int y)
        {
            if (_mapService == null || _getObjectOnMethod == null) return null;

            try
            {
                return _getObjectOnMethod.Invoke(_mapService, new object[] { x, y });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a readable name for the object on a tile
        /// </summary>
        public string GetObjectName(int x, int y)
        {
            var obj = GetObjectOnTile(x, y);
            if (obj == null) return null;

            // Try to get a display name
            var objType = obj.GetType();

            // Try Model.Name or similar patterns
            var modelProp = objType.GetProperty("Model");
            if (modelProp != null)
            {
                var model = modelProp.GetValue(obj);
                if (model != null)
                {
                    var nameProp = model.GetType().GetProperty("Name");
                    if (nameProp != null)
                    {
                        return nameProp.GetValue(model)?.ToString();
                    }
                }
            }

            // Fallback to type name
            return objType.Name;
        }

        // =====================================================================
        // GLADE (FOG OF WAR) DETECTION
        // =====================================================================

        /// <summary>
        /// Get the glade at a coordinate, if any
        /// </summary>
        public object GetGlade(int x, int y)
        {
            if (_gladesService == null || _getGladeMethod == null) return null;

            try
            {
                return _getGladeMethod.Invoke(_gladesService, new object[] { new Vector2Int(x, y) });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if coordinate is in unexplored glade
        /// </summary>
        public bool IsInGlade(int x, int y)
        {
            return GetGlade(x, y) != null;
        }

        /// <summary>
        /// Get glade danger level (0 = safe, higher = more dangerous)
        /// </summary>
        public uint GetGladeDanger(int x, int y)
        {
            var glade = GetGlade(x, y);
            if (glade == null) return 0;

            var dangerField = glade.GetType().GetField("Danger",
                BindingFlags.Public | BindingFlags.Instance);

            if (dangerField != null)
            {
                return (uint)dangerField.GetValue(glade);
            }

            return 0;
        }

        // =====================================================================
        // ANNOUNCEMENTS - Build readable descriptions
        // =====================================================================

        public void AnnounceTile(int x, int y)
        {
            string announcement = GetTileDescription(x, y);
            // Speech.Say(announcement);
            Debug.Log($"Tile ({x}, {y}): {announcement}");
        }

        public string GetTileDescription(int x, int y)
        {
            // Check if in glade first
            if (IsInGlade(x, y))
            {
                uint danger = GetGladeDanger(x, y);
                string dangerLevel = danger switch
                {
                    0 => "safe",
                    1 or 2 => "low danger",
                    3 or 4 => "moderate danger",
                    5 or 6 => "high danger",
                    _ => "extreme danger"
                };
                return $"Unexplored glade, {dangerLevel}";
            }

            // Check for objects on tile
            string objectName = GetObjectName(x, y);
            if (!string.IsNullOrEmpty(objectName))
            {
                return objectName;
            }

            // Fall back to terrain type
            string terrain = GetTerrainType(x, y);
            return terrain;
        }

        /// <summary>
        /// Get detailed tile info (for K key)
        /// </summary>
        public string GetDetailedTileInfo(int x, int y)
        {
            var parts = new System.Collections.Generic.List<string>();

            // Position
            parts.Add($"Position {x}, {y}");

            // Terrain
            string terrain = GetTerrainType(x, y);
            parts.Add(terrain);

            // Passability
            bool passable = IsTraversable(x, y);
            parts.Add(passable ? "Passable" : "Blocked");

            // Contents
            string objectName = GetObjectName(x, y);
            if (!string.IsNullOrEmpty(objectName))
            {
                parts.Add($"Contains: {objectName}");
            }

            // Glade
            if (IsInGlade(x, y))
            {
                uint danger = GetGladeDanger(x, y);
                parts.Add($"In glade, danger level {danger}");
            }

            return string.Join(". ", parts);
        }

        // =====================================================================
        // INPUT HANDLING EXAMPLE
        // =====================================================================

        private void Update()
        {
            // Example: Arrow keys to move cursor
            if (Input.GetKeyDown(KeyCode.UpArrow))
                MoveCursor(0, 1);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                MoveCursor(0, -1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                MoveCursor(-1, 0);
            else if (Input.GetKeyDown(KeyCode.RightArrow))
                MoveCursor(1, 0);

            // K for detailed info
            if (Input.GetKeyDown(KeyCode.K))
            {
                string info = GetDetailedTileInfo(_cursorX, _cursorY);
                Debug.Log(info);
                // Speech.Say(info);
            }
        }
    }
}
