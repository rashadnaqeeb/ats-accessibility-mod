using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard-based map navigation in settlement view.
    /// Arrow keys move a virtual cursor on the 70x70 grid, announcing tile contents.
    /// </summary>
    public class MapNavigator
    {
        // Map bounds (70x70 grid)
        private const int MAP_MIN = 0;
        private const int MAP_MAX = 69;

        // Virtual cursor position (start at center)
        private int _cursorX = 35;
        private int _cursorY = 35;

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
            int newX = _cursorX + dx;
            int newY = _cursorY + dy;

            // Bounds check
            if (newX < MAP_MIN || newX > MAP_MAX || newY < MAP_MIN || newY > MAP_MAX)
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
        /// Announce current coordinates (K key).
        /// </summary>
        public void AnnounceCurrentPosition()
        {
            Speech.Say($"{_cursorX}, {_cursorY}");
        }

        /// <summary>
        /// Reset cursor to center of map.
        /// </summary>
        public void ResetCursor()
        {
            _cursorX = 35;
            _cursorY = 35;
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
        private string GetTileAnnouncement(int x, int y, object field)
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

            // Check for villagers
            string villagerInfo = GetVillagersOnTile(x, y);
            if (!string.IsNullOrEmpty(villagerInfo))
            {
                parts.Add(villagerInfo);
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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Field property caching failed: {ex.Message}");
            }

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

                // Debug: log the type and its properties
                var typeType = typeValue.GetType();
                Debug.Log($"[ATSAccessibility] Field.Type is {typeType.Name}, value={typeValue}");

                // Try to get displayName or name from the type object
                var displayNameProp = typeType.GetProperty("displayName");
                if (displayNameProp != null)
                {
                    var displayName = displayNameProp.GetValue(typeValue);
                    Debug.Log($"[ATSAccessibility] Field.Type.displayName = {displayName}");
                    if (displayName != null) return displayName.ToString();
                }

                var nameProp = typeType.GetProperty("name");
                if (nameProp != null)
                {
                    var name = nameProp.GetValue(typeValue);
                    Debug.Log($"[ATSAccessibility] Field.Type.name = {name}");
                    if (name != null) return name.ToString();
                }

                // Log all properties for debugging
                foreach (var prop in typeType.GetProperties())
                {
                    try
                    {
                        var val = prop.GetValue(typeValue);
                        Debug.Log($"[ATSAccessibility] Field.Type.{prop.Name} = {val}");
                    }
                    catch { }
                }

                return typeValue.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetFieldType failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Glade field caching failed: {ex.Message}");
            }

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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetGladeWasDiscovered failed: {ex.Message}");
            }

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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetGladeDangerLevel failed: {ex.Message}");
            }

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
                Debug.Log($"[ATSAccessibility] GetObjectName: object type is {objType.FullName}");

                // Log all properties for debugging
                Debug.Log($"[ATSAccessibility] Properties on {objType.Name}:");
                foreach (var prop in objType.GetProperties())
                {
                    try
                    {
                        var val = prop.GetValue(obj);
                        string valStr = val?.ToString() ?? "null";
                        if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                        Debug.Log($"[ATSAccessibility]   .{prop.Name} = {valStr}");
                    }
                    catch { }
                }

                // Try Model.label.displayName first (specific type like "Lush Tree")
                // Then fall back to Model.displayName (generic category like "Woodlands Trees")
                var modelProperty = objType.GetProperty("Model");
                if (modelProperty != null)
                {
                    var model = modelProperty.GetValue(obj);
                    if (model != null)
                    {
                        Debug.Log($"[ATSAccessibility] Found Model property, type={model.GetType().Name}");
                        var modelType = model.GetType();

                        // Log all fields for debugging
                        Debug.Log($"[ATSAccessibility] Fields on {modelType.Name}:");
                        foreach (var field in modelType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            try
                            {
                                var val = field.GetValue(model);
                                string valStr = val?.ToString() ?? "null";
                                if (valStr.Length > 100) valStr = valStr.Substring(0, 100) + "...";
                                Debug.Log($"[ATSAccessibility]   .{field.Name} = {valStr}");
                            }
                            catch { }
                        }

                        // Try Model.displayName first (specific type like "Lush Tree")
                        // Note: displayName is already user-friendly, don't run through CleanResourceName
                        var displayNameField = modelType.GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                        if (displayNameField != null)
                        {
                            var displayName = displayNameField.GetValue(model);
                            if (displayName != null)
                            {
                                string displayText = displayName.ToString();
                                Debug.Log($"[ATSAccessibility] Model.displayName = {displayText}");
                                if (!string.IsNullOrEmpty(displayText))
                                {
                                    return displayText;
                                }
                            }
                        }

                        // Fall back to Model.label.displayName (generic category like "Resource")
                        var labelField = modelType.GetField("label", BindingFlags.Public | BindingFlags.Instance);
                        if (labelField != null)
                        {
                            var label = labelField.GetValue(model);
                            if (label != null)
                            {
                                Debug.Log($"[ATSAccessibility] Found label field, type={label.GetType().Name}");
                                var labelDisplayNameField = label.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                                if (labelDisplayNameField != null)
                                {
                                    var labelDisplayName = labelDisplayNameField.GetValue(label);
                                    if (labelDisplayName != null)
                                    {
                                        string labelText = labelDisplayName.ToString();
                                        Debug.Log($"[ATSAccessibility] Model.label.displayName = {labelText}");
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
                                Debug.Log($"[ATSAccessibility] Model.name = {name}");
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
                Debug.Log($"[ATSAccessibility] No name found, using type name: {objType.Name}");
                return objType.Name;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetObjectName failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Villager property caching failed: {ex.Message}");
            }

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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetVillagersOnTile failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SyncCameraToTile failed: {ex.Message}");
            }
        }
    }
}
