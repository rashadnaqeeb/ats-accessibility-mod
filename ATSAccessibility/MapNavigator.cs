using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard-based map navigation in settlement view.
    /// Arrow keys move a virtual cursor on the map grid, announcing tile contents.
    /// Map size is dynamically determined from the game's MapService.
    /// </summary>
    public class MapNavigator
    {
        // Virtual cursor position (initialized to center on first use)
        private int _cursorX = -1;
        private int _cursorY = -1;

        // Cached reflection info for Field properties
        private PropertyInfo _fieldTypeProperty = null;
        private PropertyInfo _fieldIsTraversableProperty = null;
        private bool _fieldPropertiesCached = false;

        // Cached reflection info for Glade fields
        private FieldInfo _gladeWasDiscoveredField = null;
        private FieldInfo _gladeDangerLevelField = null;
        private bool _gladePropertiesCached = false;

        // Cached reflection info for Villager properties
        private PropertyInfo _villagerActorStateProperty = null;
        private FieldInfo _actorStatePositionField = null;
        private PropertyInfo _villagerRaceProperty = null;
        private bool _villagerPropertiesCached = false;

        /// <summary>
        /// Current cursor X position.
        /// </summary>
        public int CursorX => _cursorX;

        /// <summary>
        /// Current cursor Y position.
        /// </summary>
        public int CursorY => _cursorY;

        /// <summary>
        /// Move the cursor by delta and announce the new tile.
        /// </summary>
        public void MoveCursor(int dx, int dy)
        {
            // Initialize cursor to center if not yet set
            if (_cursorX < 0 || _cursorY < 0)
            {
                ResetCursor();
            }

            int newX = _cursorX + dx;
            int newY = _cursorY + dy;

            // Bounds check using game's MapService
            if (!GameReflection.MapInBounds(newX, newY))
            {
                Speech.Say("edge of map");
                return;
            }

            _cursorX = newX;
            _cursorY = newY;

            // Fetch field once, reuse for announcement and camera
            var field = GameReflection.GetField(_cursorX, _cursorY);

            AnnounceTile(field);
            SyncCameraToTile(field);
        }

        /// <summary>
        /// Set cursor to specific position (for scanner End key).
        /// Does not announce - caller handles announcement.
        /// </summary>
        public void SetCursorPosition(int x, int y)
        {
            // Bounds check using game's MapService
            if (!GameReflection.MapInBounds(x, y))
                return;

            _cursorX = x;
            _cursorY = y;

            var field = GameReflection.GetField(_cursorX, _cursorY);
            SyncCameraToTile(field);
        }

        /// <summary>
        /// Skip tiles in direction until finding a tile with different announcement.
        /// If edge reached without finding different tile, stay put and announce edge.
        /// </summary>
        public void SkipToNextChange(int dx, int dy)
        {
            // Initialize cursor to center if not yet set
            if (_cursorX < 0 || _cursorY < 0)
            {
                ResetCursor();
            }

            // Get current tile's announcement as baseline (exclude villagers for comparison)
            var currentField = GameReflection.GetField(_cursorX, _cursorY);
            string currentAnnouncement = GetTileAnnouncement(_cursorX, _cursorY, currentField, includeVillagers: false);

            int newX = _cursorX;
            int newY = _cursorY;

            // Step in direction until we find different tile or hit edge
            while (true)
            {
                int nextX = newX + dx;
                int nextY = newY + dy;

                // Check bounds BEFORE moving using game's MapService
                if (!GameReflection.MapInBounds(nextX, nextY))
                {
                    // Hit edge without finding different tile - stay at current position
                    Speech.Say("no change till edge");
                    return;
                }

                newX = nextX;
                newY = nextY;

                // Get this tile's announcement (need fresh field for correct terrain/passability)
                var nextField = GameReflection.GetField(newX, newY);
                string nextAnnouncement = GetTileAnnouncement(newX, newY, nextField, includeVillagers: false);

                // Exact string comparison
                if (nextAnnouncement != currentAnnouncement)
                {
                    // Found different tile - move there
                    _cursorX = newX;
                    _cursorY = newY;
                    AnnounceTile(nextField);
                    SyncCameraToTile(nextField);
                    return;
                }
            }
        }

        /// <summary>
        /// Announce current coordinates (K key).
        /// </summary>
        public void AnnounceCurrentPosition()
        {
            Speech.Say($"{_cursorX}, {_cursorY}");
        }

        /// <summary>
        /// Clear cursor position so it will be reinitialized on next use.
        /// Call this when leaving a game session.
        /// </summary>
        public void ClearCursor()
        {
            _cursorX = -1;
            _cursorY = -1;
        }

        /// <summary>
        /// Reset cursor to the Ancient Hearth position, or map center as fallback.
        /// </summary>
        public void ResetCursor()
        {
            var hearthPos = GameReflection.GetMainHearthPosition();
            if (hearthPos.HasValue)
            {
                _cursorX = hearthPos.Value.x;
                _cursorY = hearthPos.Value.y;
            }
            else
            {
                // Fallback to center if hearth not found
                _cursorX = GameReflection.GetMapWidth() / 2;
                _cursorY = GameReflection.GetMapHeight() / 2;
            }
        }

        /// <summary>
        /// Announce the current tile contents.
        /// </summary>
        private void AnnounceTile(object field)
        {
            string announcement = GetTileAnnouncement(_cursorX, _cursorY, field);
            if (!string.IsNullOrEmpty(announcement))
            {
                Speech.Say(announcement);
            }
        }

        /// <summary>
        /// Build announcement string for a tile.
        /// </summary>
        /// <param name="includeVillagers">If false, skip villager check (for skip comparison performance)</param>
        private string GetTileAnnouncement(int x, int y, object field, bool includeVillagers = true)
        {
            // Check for unrevealed glade first
            var glade = GameReflection.GetGlade(x, y);
            if (glade != null)
            {
                bool wasDiscovered = GetGladeWasDiscovered(glade);
                if (!wasDiscovered)
                {
                    // Unrevealed glade - only announce type
                    string dangerLevel = GetGladeDangerLevel(glade);
                    return $"glade-{dangerLevel.ToLower()}";
                }
            }

            // Revealed tile - check contents
            var parts = new List<string>();

            // Check for building/resource
            var objectOn = GameReflection.GetObjectOn(x, y);
            bool hasRealObject = false;

            if (objectOn != null)
            {
                // GetObjectOn returns Field when there's no actual object - skip those
                string typeName = objectOn.GetType().Name;
                if (typeName != "Field")
                {
                    string objectName = GetObjectName(objectOn);
                    if (!string.IsNullOrEmpty(objectName))
                    {
                        // Check building state if it's a building
                        if (GameReflection.IsBuilding(objectOn))
                        {
                            if (GameReflection.IsBuildingUnfinished(objectOn))
                            {
                                objectName += ", under construction";
                            }
                            else if (GameReflection.IsRelic(objectOn))
                            {
                                objectName += ", ruin";
                            }
                        }
                        parts.Add(objectName);
                        hasRealObject = true;
                    }
                }
            }

            if (!hasRealObject)
            {
                // No object - announce terrain using passed-in field
                if (field != null)
                {
                    string terrain = GetFieldType(field);
                    if (!string.IsNullOrEmpty(terrain))
                    {
                        parts.Add(terrain);
                    }
                }
            }

            // Check passability using same field (no second GetField call)
            if (field != null)
            {
                bool isTraversable = GetFieldIsTraversable(field);
                if (!isTraversable)
                {
                    parts.Add("impassable");
                }
            }

            // Check for villagers (optional - excluded during skip comparison for performance)
            if (includeVillagers)
            {
                string villagerInfo = GetVillagersOnTile(x, y);
                if (!string.IsNullOrEmpty(villagerInfo))
                {
                    parts.Add(villagerInfo);
                }
            }

            return string.Join(", ", parts);
        }

        // ========================================
        // FIELD PROPERTY ACCESS
        // ========================================

        private void EnsureFieldProperties(object field)
        {
            if (_fieldPropertiesCached || field == null) return;

            try
            {
                var fieldType = field.GetType();
                _fieldTypeProperty = fieldType.GetProperty("Type");
                _fieldIsTraversableProperty = fieldType.GetProperty("IsTraversable");
            }
            catch { }

            _fieldPropertiesCached = true;
        }

        private string GetFieldType(object field)
        {
            EnsureFieldProperties(field);
            if (_fieldTypeProperty == null) return "unknown";

            try
            {
                var typeValue = _fieldTypeProperty.GetValue(field);
                if (typeValue == null) return "unknown";

                var typeType = typeValue.GetType();
                string result = null;

                // Try to get displayName or name from the type object
                var displayNameProp = typeType.GetProperty("displayName");
                if (displayNameProp != null)
                {
                    var displayName = displayNameProp.GetValue(typeValue);
                    if (displayName != null) result = displayName.ToString();
                }

                if (result == null)
                {
                    var nameProp = typeType.GetProperty("name");
                    if (nameProp != null)
                    {
                        var name = nameProp.GetValue(typeValue);
                        if (name != null) result = name.ToString();
                    }
                }

                if (result == null) result = typeValue.ToString();

                // Map game names to more descriptive names
                if (result == "Grass") return "Fertile Soil";
                if (result == "Sand") return "Soil";

                return result;
            }
            catch
            {
                return "unknown";
            }
        }

        private bool GetFieldIsTraversable(object field)
        {
            EnsureFieldProperties(field);
            if (_fieldIsTraversableProperty == null) return true;

            try
            {
                return (bool)_fieldIsTraversableProperty.GetValue(field);
            }
            catch
            {
                return true;
            }
        }

        // ========================================
        // GLADE PROPERTY ACCESS
        // ========================================

        private void EnsureGladeProperties(object glade)
        {
            if (_gladePropertiesCached || glade == null) return;

            try
            {
                var gladeType = glade.GetType();
                // GladeState uses fields, not properties
                _gladeWasDiscoveredField = gladeType.GetField("wasDiscovered",
                    BindingFlags.Public | BindingFlags.Instance);
                _gladeDangerLevelField = gladeType.GetField("dangerLevel",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch { }

            _gladePropertiesCached = true;
        }

        private bool GetGladeWasDiscovered(object glade)
        {
            EnsureGladeProperties(glade);

            try
            {
                if (_gladeWasDiscoveredField != null)
                {
                    return (bool)_gladeWasDiscoveredField.GetValue(glade);
                }
            }
            catch { }

            // Default to discovered (don't hide content if we can't determine state)
            return true;
        }

        private string GetGladeDangerLevel(object glade)
        {
            EnsureGladeProperties(glade);

            try
            {
                if (_gladeDangerLevelField != null)
                {
                    var dangerValue = _gladeDangerLevelField.GetValue(glade);
                    string dangerStr = dangerValue?.ToString() ?? "unknown";

                    // Map enum values to user-friendly names
                    switch (dangerStr)
                    {
                        case "None":
                            return "small";
                        case "Dangerous":
                            return "dangerous";
                        case "Forbidden":
                            return "forbidden";
                        default:
                            return dangerStr.ToLower();
                    }
                }
            }
            catch { }

            return "unknown";
        }

        // ========================================
        // OBJECT NAME ACCESS (building/resource)
        // ========================================

        private string GetObjectName(object obj)
        {
            if (obj == null) return null;

            try
            {
                var objType = obj.GetType();

                // Try Model.displayName first (specific type like "Lush Tree")
                // Then fall back to Model.label.displayName (generic category like "Woodlands Trees")
                var modelProperty = objType.GetProperty("Model");
                if (modelProperty != null)
                {
                    var model = modelProperty.GetValue(obj);
                    if (model != null)
                    {
                        var modelType = model.GetType();

                        // Try Model.displayName first
                        var displayNameField = modelType.GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                        if (displayNameField != null)
                        {
                            var displayName = displayNameField.GetValue(model);
                            if (displayName != null)
                            {
                                string displayText = displayName.ToString();
                                if (!string.IsNullOrEmpty(displayText))
                                {
                                    return displayText;
                                }
                            }
                        }

                        // Fall back to Model.label.displayName
                        var labelField = modelType.GetField("label", BindingFlags.Public | BindingFlags.Instance);
                        if (labelField != null)
                        {
                            var label = labelField.GetValue(model);
                            if (label != null)
                            {
                                var labelDisplayNameField = label.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                                if (labelDisplayNameField != null)
                                {
                                    var labelDisplayName = labelDisplayNameField.GetValue(label);
                                    if (labelDisplayName != null)
                                    {
                                        string labelText = labelDisplayName.ToString();
                                        if (!string.IsNullOrEmpty(labelText))
                                        {
                                            return labelText;
                                        }
                                    }
                                }
                            }
                        }

                        var nameProp = modelType.GetProperty("name");
                        if (nameProp != null)
                        {
                            var name = nameProp.GetValue(model);
                            if (name != null)
                            {
                                return Speech.CleanResourceName(name.ToString());
                            }
                        }
                    }
                }

                // Try Name property
                var nameProperty = objType.GetProperty("Name");
                if (nameProperty != null)
                {
                    var nameValue = nameProperty.GetValue(obj);
                    if (nameValue != null)
                    {
                        return nameValue.ToString();
                    }
                }

                // Try DisplayName property
                var displayNameProperty = objType.GetProperty("DisplayName");
                if (displayNameProperty != null)
                {
                    var nameValue = displayNameProperty.GetValue(obj);
                    if (nameValue != null)
                    {
                        return nameValue.ToString();
                    }
                }

                // Fallback to type name
                return objType.Name;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // VILLAGER ACCESS
        // ========================================

        private void EnsureVillagerProperties(object villager)
        {
            if (_villagerPropertiesCached || villager == null) return;

            try
            {
                var villagerType = villager.GetType();
                _villagerActorStateProperty = villagerType.GetProperty("ActorState");
                _villagerRaceProperty = villagerType.GetProperty("Race");

                // Get ActorState type for position field
                if (_villagerActorStateProperty != null)
                {
                    var actorState = _villagerActorStateProperty.GetValue(villager);
                    if (actorState != null)
                    {
                        var actorStateType = actorState.GetType();
                        _actorStatePositionField = actorStateType.GetField("position",
                            BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }
            catch { }

            _villagerPropertiesCached = true;
        }

        private string GetVillagersOnTile(int x, int y)
        {
            var allVillagers = GameReflection.GetAllVillagers();
            if (allVillagers == null) return null;

            try
            {
                // allVillagers is Dictionary<int, Villager>
                // We need to iterate and check positions
                var villagersDict = allVillagers as IDictionary;
                if (villagersDict == null) return null;

                var raceCounts = new Dictionary<string, int>();

                foreach (DictionaryEntry entry in villagersDict)
                {
                    var villager = entry.Value;
                    if (villager == null) continue;

                    EnsureVillagerProperties(villager);

                    // Get position
                    Vector3 position = GetVillagerPosition(villager);
                    int villagerX = Mathf.FloorToInt(position.x);
                    int villagerZ = Mathf.FloorToInt(position.z);

                    if (villagerX == x && villagerZ == y)
                    {
                        // Villager is on this tile
                        string race = GetVillagerRace(villager);
                        if (string.IsNullOrEmpty(race)) race = "villager";

                        if (raceCounts.ContainsKey(race))
                        {
                            raceCounts[race]++;
                        }
                        else
                        {
                            raceCounts[race] = 1;
                        }
                    }
                }

                if (raceCounts.Count == 0) return null;

                // Build announcement like "2 beavers, 1 human"
                var parts = new List<string>();
                foreach (var kvp in raceCounts)
                {
                    string race = kvp.Key.ToLower();
                    int count = kvp.Value;

                    // Simple pluralization
                    if (count > 1 && !race.EndsWith("s"))
                    {
                        race += "s";
                    }

                    parts.Add($"{count} {race}");
                }

                return string.Join(", ", parts);
            }
            catch
            {
                return null;
            }
        }

        private Vector3 GetVillagerPosition(object villager)
        {
            try
            {
                if (_villagerActorStateProperty == null) return Vector3.zero;

                var actorState = _villagerActorStateProperty.GetValue(villager);
                if (actorState == null || _actorStatePositionField == null) return Vector3.zero;

                return (Vector3)_actorStatePositionField.GetValue(actorState);
            }
            catch
            {
                return Vector3.zero;
            }
        }

        private string GetVillagerRace(object villager)
        {
            try
            {
                if (_villagerRaceProperty == null) return null;

                var raceValue = _villagerRaceProperty.GetValue(villager);
                return raceValue?.ToString();
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // CAMERA SYNC
        // ========================================

        /// <summary>
        /// Sync the camera to follow the current cursor position.
        /// Uses the game's built-in smooth camera movement.
        /// </summary>
        private void SyncCameraToTile(object field)
        {
            if (field == null) return;

            try
            {
                // Get the Field's transform property
                var transformProperty = field.GetType().GetProperty("transform");
                if (transformProperty == null) return;

                var fieldTransform = transformProperty.GetValue(field) as Transform;
                if (fieldTransform != null)
                {
                    GameReflection.SetCameraTarget(fieldTransform);
                }
            }
            catch { }
        }

        // ========================================
        // BUILDING ACTIVATION (Enter key)
        // ========================================

        /// <summary>
        /// Activate/open the building panel for the building at current cursor position.
        /// Returns true if a building was activated, false otherwise.
        /// </summary>
        public bool ActivateBuilding()
        {
            // Initialize cursor if needed
            if (_cursorX < 0 || _cursorY < 0)
            {
                ResetCursor();
            }

            // Get object at cursor position
            var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
            if (objectOn == null || objectOn.GetType().Name == "Field")
            {
                Speech.Say("No building here");
                return false;
            }

            // Check if it's a building
            if (!GameReflection.IsBuilding(objectOn))
            {
                Speech.Say("Not a building");
                return false;
            }

            // Try to pick the building (opens its panel)
            if (GameReflection.PickBuilding(objectOn))
            {
                return true;
            }
            else
            {
                Speech.Say("Cannot open building");
                return false;
            }
        }

        // ========================================
        // ENTRANCE ANNOUNCEMENT (E key)
        // ========================================

        /// <summary>
        /// Announce entrance location for building at current cursor position.
        /// Only works for buildings that show entrances (warehouses, workshops, etc.)
        /// </summary>
        public void AnnounceEntrance()
        {
            // Initialize cursor if needed
            if (_cursorX < 0 || _cursorY < 0)
            {
                ResetCursor();
            }

            // Get object at cursor position
            var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
            if (objectOn == null || objectOn.GetType().Name == "Field")
            {
                Speech.Say("No building here");
                return;
            }

            // Check if it's a building
            if (!GameReflection.IsBuilding(objectOn))
            {
                Speech.Say("Not a building");
                return;
            }

            // Check if this building type shows entrances
            if (!GameReflection.GetBuildingShouldShowEntrance(objectOn))
            {
                Speech.Say("No entrance");
                return;
            }

            // Get entrance tile
            var entranceTile = GameReflection.GetBuildingEntranceTile(objectOn);
            if (!entranceTile.HasValue)
            {
                Speech.Say("Entrance not found");
                return;
            }

            int entranceX = entranceTile.Value.x;
            int entranceY = entranceTile.Value.y;

            // Calculate distance and direction from cursor to entrance
            int dx = entranceX - _cursorX;
            int dy = entranceY - _cursorY;

            // Check if we're already at the entrance
            if (dx == 0 && dy == 0)
            {
                Speech.Say("At entrance");
                return;
            }

            // Calculate Manhattan distance
            int distance = Mathf.Abs(dx) + Mathf.Abs(dy);

            // Determine direction
            string direction = GetDirection(dx, dy);

            // Announce
            string tileWord = distance == 1 ? "tile" : "tiles";
            Speech.Say($"Entrance {distance} {tileWord} {direction}");
        }

        /// <summary>
        /// Get cardinal/intercardinal direction from delta.
        /// </summary>
        private string GetDirection(int dx, int dy)
        {
            // Determine primary direction based on deltas
            // In this game: +X is East, +Y is North
            if (dx == 0 && dy > 0) return "north";
            if (dx == 0 && dy < 0) return "south";
            if (dx > 0 && dy == 0) return "east";
            if (dx < 0 && dy == 0) return "west";
            if (dx > 0 && dy > 0) return "northeast";
            if (dx > 0 && dy < 0) return "southeast";
            if (dx < 0 && dy > 0) return "northwest";
            if (dx < 0 && dy < 0) return "southwest";
            return "unknown";
        }

        // ========================================
        // BUILDING ROTATION (R key)
        // ========================================

        // Rotation directions: 0=North, 1=East, 2=South, 3=West
        private static readonly string[] RotationDirections = { "North", "East", "South", "West" };

        /// <summary>
        /// Rotate the building at current cursor position and announce the new direction.
        /// </summary>
        public void RotateBuilding()
        {
            // Initialize cursor if needed
            if (_cursorX < 0 || _cursorY < 0)
            {
                ResetCursor();
            }

            // Get object at cursor position
            var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
            if (objectOn == null || objectOn.GetType().Name == "Field")
            {
                Speech.Say("No building here");
                return;
            }

            // Check if it's a building
            if (!GameReflection.IsBuilding(objectOn))
            {
                Speech.Say("Not a building");
                return;
            }

            // Check if building type supports rotation
            if (!GameReflection.CanRotateBuilding(objectOn))
            {
                Speech.Say("Cannot rotate");
                return;
            }

            // Check if building is movable (required for rotation)
            if (!GameReflection.CanMovePlacedBuilding(objectOn))
            {
                Speech.Say("Unmovable");
                return;
            }

            // Check if rotation would be blocked by obstacles
            if (!GameReflection.CanRotatePlacedBuilding(objectOn))
            {
                Speech.Say("Rotation blocked");
                return;
            }

            // Rotate the building
            int newRotation = GameReflection.RotatePlacedBuilding(objectOn);
            if (newRotation >= 0 && newRotation < RotationDirections.Length)
            {
                Speech.Say(RotationDirections[newRotation]);
            }
            else
            {
                Speech.Say("Rotation failed");
            }
        }
    }
}
