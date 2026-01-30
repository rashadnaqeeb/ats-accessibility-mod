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

        // Cached reflection info for Field properties (static per CLAUDE.md Pattern #6)
        private static PropertyInfo _fieldTypeProperty = null;
        private static PropertyInfo _fieldIsTraversableProperty = null;
        private static bool _fieldPropertiesCached = false;

        // Cached reflection info for Glade fields (static per CLAUDE.md Pattern #6)
        private static FieldInfo _gladeWasDiscoveredField = null;
        private static FieldInfo _gladeDangerLevelField = null;
        private static bool _gladePropertiesCached = false;

        // Cached reflection info for Villager properties (static per CLAUDE.md Pattern #6)
        private static PropertyInfo _villagerActorStateProperty = null;
        private static FieldInfo _actorStatePositionField = null;
        private static PropertyInfo _villagerRaceProperty = null;
        private static bool _villagerPropertiesCached = false;

        /// <summary>
        /// Current cursor X position.
        /// </summary>
        public int CursorX => _cursorX;

        /// <summary>
        /// Current cursor Y position.
        /// </summary>
        public int CursorY => _cursorY;

        /// <summary>
        /// Optional prefix callback for tile announcements.
        /// Returns a prefix string (e.g. "selected") or null for no prefix.
        /// </summary>
        public Func<int, int, string> AnnouncementPrefix { get; set; }

        /// <summary>
        /// Ensure cursor is initialized (to hearth or map center).
        /// </summary>
        private void EnsureCursorInitialized()
        {
            if (_cursorX < 0 || _cursorY < 0)
                ResetCursor();
        }

        /// <summary>
        /// Move the cursor by delta and announce the new tile.
        /// </summary>
        public void MoveCursor(int dx, int dy)
        {
            EnsureCursorInitialized();

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
            EnsureCursorInitialized();

            // Get current tile's announcement as baseline (exclude villagers for comparison)
            var currentField = GameReflection.GetField(_cursorX, _cursorY);
            string currentAnnouncement = GetTileAnnouncement(_cursorX, _cursorY, currentField, includeVillagers: false);

            int newX = _cursorX;
            int newY = _cursorY;
            int tilesSkipped = 0;

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
                tilesSkipped++;

                // Get this tile's announcement (need fresh field for correct terrain/passability)
                var nextField = GameReflection.GetField(newX, newY);
                string nextAnnouncement = GetTileAnnouncement(newX, newY, nextField, includeVillagers: false);

                // Exact string comparison
                if (nextAnnouncement != currentAnnouncement)
                {
                    // Found different tile - move there
                    _cursorX = newX;
                    _cursorY = newY;

                    string tileWord = tilesSkipped == 1 ? "tile" : "tiles";
                    string announcement = GetTileAnnouncement(_cursorX, _cursorY, nextField);
                    string prefix = AnnouncementPrefix?.Invoke(_cursorX, _cursorY);
                    if (!string.IsNullOrEmpty(prefix))
                        announcement = $"{prefix}, {announcement}";
                    Speech.Say($"{tilesSkipped} {tileWord}, {announcement}");

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
        /// Announce distance from hearth (Alt+K).
        /// </summary>
        public void AnnounceHearthDistance()
        {
            EnsureCursorInitialized();

            var hearthPos = GameReflection.GetMainHearthPosition();
            if (!hearthPos.HasValue)
            {
                Speech.Say("Hearth not found");
                return;
            }

            int dx = _cursorX - hearthPos.Value.x;
            int dy = _cursorY - hearthPos.Value.y;

            if (dx == 0 && dy == 0)
            {
                Speech.Say("At hearth");
                return;
            }

            var parts = new List<string>();
            if (dy != 0)
                parts.Add($"{Mathf.Abs(dy)} {(dy > 0 ? "south" : "north")}");
            if (dx != 0)
                parts.Add($"{Mathf.Abs(dx)} {(dx > 0 ? "west" : "east")}");

            Speech.Say($"Hearth: {string.Join(", ", parts)}");
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
                string prefix = AnnouncementPrefix?.Invoke(_cursorX, _cursorY);
                if (!string.IsNullOrEmpty(prefix))
                    announcement = $"{prefix}, {announcement}";
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
                    // Unrevealed glade - announce based on what info is available
                    string dangerLevel = GetGladeDangerLevel(glade);
                    bool hasDangerousGladeInfo = GameReflection.HasDangerousGladeInfo();
                    bool hasGladeInfo = GameReflection.HasGladeInfo();

                    string baseName;
                    if (!hasDangerousGladeInfo)
                    {
                        // Cursed Royal Woodlands: ALL glade markers are hidden
                        baseName = "glade-unknown";
                    }
                    else if (hasGladeInfo)
                    {
                        // Has glade info perk - show type and contents
                        baseName = $"glade-{dangerLevel.ToLower()}";
                        string contents = GameReflection.GetGladeContentsSummary(glade);
                        if (!string.IsNullOrEmpty(contents))
                            baseName += $": {contents}";
                    }
                    else
                    {
                        // Normal biome without glade info perk - show type only
                        baseName = $"glade-{dangerLevel.ToLower()}";
                    }

                    // Add location marker if present
                    string markerType = GameReflection.GetLocationMarkerType(x, y);
                    if (!string.IsNullOrEmpty(markerType))
                        baseName = $"{baseName}, {markerType}";

                    // Add highlighted relic info if present (from Short Range Scanner, etc)
                    string highlightedRelic = GameReflection.GetHighlightedRelicAt(x, y);
                    if (!string.IsNullOrEmpty(highlightedRelic))
                    {
                        string relicDisplayName = GameReflection.GetRelicDisplayName(highlightedRelic);
                        baseName = $"{baseName}, highlighted: {relicDisplayName}";
                    }

                    return baseName;
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
                        else if (typeName == "NaturalResource" && GameReflection.IsNaturalResourceMarked(objectOn))
                        {
                            objectName = "Marked " + objectName;
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureFieldProperties failed: {ex.Message}"); }

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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetFieldType failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetFieldIsTraversable failed: {ex.Message}");
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureGladeProperties failed: {ex.Message}"); }

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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladeWasDiscovered failed: {ex.Message}"); }

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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladeDangerLevel failed: {ex.Message}"); }

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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetObjectName failed: {ex.Message}");
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureVillagerProperties failed: {ex.Message}"); }

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

                    // Pluralize if count > 1 and doesn't already end in 's'
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
                Debug.LogWarning($"[ATSAccessibility] GetVillagersOnTile failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetVillagerPosition failed: {ex.Message}");
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetVillagerRace failed: {ex.Message}");
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SyncCameraToTile failed: {ex.Message}"); }
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
            EnsureCursorInitialized();

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

            // Announce construction progress instead of opening panel for unfinished buildings
            if (GameReflection.IsBuildingUnfinished(objectOn))
            {
                AnnounceConstruction(objectOn);
                return true;
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

        private void AnnounceConstruction(object building)
        {
            float progress = GameReflection.GetBuildingProgress(building);
            int percent = (int)(progress * 100);

            if (percent > 0)
            {
                Speech.Say($"{percent}%");
                return;
            }

            // 0% progress - announce remaining materials if any
            var materials = GameReflection.GetConstructionMaterials(building);
            if (materials != null && materials.Count > 0)
            {
                var parts = new List<string>();
                foreach (var (name, delivered, required) in materials)
                {
                    parts.Add($"{name} {delivered} of {required}");
                }
                Speech.Say(string.Join(", ", parts));
            }
            else
            {
                Speech.Say("0%");
            }
        }

        // ========================================
        // ENTRANCE ANNOUNCEMENT (E key)
        // ========================================

        // Approach tile directions indexed by rotation (0-3)
        // Rotation 0: local (0,0,-1) → grid (0,-1) = south
        // Rotation 1: local (0,0,-1) → grid (1, 0) = east
        // Rotation 2: local (0,0,-1) → grid (0, 1) = north
        // Rotation 3: local (0,0,-1) → grid (-1,0) = west
        private static readonly Vector2Int[] ApproachOffsets =
        {
            new Vector2Int(0, -1),  // rotation 0 → south
            new Vector2Int(1, 0),   // rotation 1 → east
            new Vector2Int(0, 1),   // rotation 2 → north
            new Vector2Int(-1, 0),  // rotation 3 → west
        };
        private static readonly string[] ApproachDirections = { "south", "east", "north", "west" };

        /// <summary>
        /// Announce entrance location for building at current cursor position.
        /// Reports the approach tile (1 tile outward from entrance based on rotation).
        /// </summary>
        public void AnnounceEntrance()
        {
            EnsureCursorInitialized();

            // Get object at cursor position
            var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
            bool onBuilding = objectOn != null && objectOn.GetType().Name != "Field" && GameReflection.IsBuilding(objectOn);

            if (onBuilding && TryGetApproachTile(objectOn, out int approachX, out int approachY, out int rotation))
            {
                if (_cursorX == approachX && _cursorY == approachY)
                {
                    Speech.Say("At entrance");
                    return;
                }

                int dx = approachX - _cursorX;
                int dy = approachY - _cursorY;
                int distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                string direction = GetDirection(dx, dy);
                string tileWord = distance == 1 ? "tile" : "tiles";
                string facing = ApproachDirections[rotation];
                Speech.Say($"Entrance {distance} {tileWord} {direction}, facing {facing}");
                return;
            }

            // Not on a building with an entrance — check if standing on a
            // neighbor building's approach tile. The approach tile is always
            // 1 cardinal step from the entrance tile, so check 4 neighbors.
            if (IsAtAnyApproachTile())
            {
                Speech.Say("At entrance");
                return;
            }

            Speech.Say(onBuilding ? "No entrance" : "No building here");
        }

        private bool IsAtAnyApproachTile()
        {
            for (int i = 0; i < ApproachOffsets.Length; i++)
            {
                int nx = _cursorX + ApproachOffsets[i].x;
                int ny = _cursorY + ApproachOffsets[i].y;

                if (!GameReflection.MapInBounds(nx, ny)) continue;

                var neighbor = GameReflection.GetObjectOn(nx, ny);
                if (neighbor == null || neighbor.GetType().Name == "Field" || !GameReflection.IsBuilding(neighbor))
                    continue;

                if (TryGetApproachTile(neighbor, out int ax, out int ay, out int _))
                {
                    if (ax == _cursorX && ay == _cursorY)
                        return true;
                }
            }
            return false;
        }

        private bool TryGetApproachTile(object building, out int approachX, out int approachY, out int rotation)
        {
            approachX = 0;
            approachY = 0;
            rotation = -1;

            if (!GameReflection.GetBuildingShouldShowEntrance(building))
                return false;

            var entranceTile = GameReflection.GetBuildingEntranceTile(building);
            if (!entranceTile.HasValue)
                return false;

            rotation = GameReflection.GetBuildingRotation(building);
            if (rotation < 0 || rotation > 3)
                return false;

            int ex = entranceTile.Value.x;
            int ey = entranceTile.Value.y;

            // Check if the entrance tile is inside the building.
            // For rotated buildings, the ToRotate pivot can push the entrance
            // Transform outside the footprint — in that case the entrance tile
            // itself is already the approach tile.
            var objectAtEntrance = GameReflection.GetObjectOn(ex, ey);
            bool entranceInsideBuilding = objectAtEntrance != null && ReferenceEquals(objectAtEntrance, building);

            if (entranceInsideBuilding)
            {
                Vector2Int offset = ApproachOffsets[rotation];
                approachX = ex + offset.x;
                approachY = ey + offset.y;
            }
            else
            {
                approachX = ex;
                approachY = ey;
            }
            return true;
        }

        private string GetDirection(int dx, int dy)
        {
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

        // Rotation directions: 0=North, 1=West, 2=South, 3=East
        private static readonly string[] RotationDirections = { "North", "West", "South", "East" };

        /// <summary>
        /// Rotate the building at current cursor position and announce the new direction.
        /// </summary>
        public void RotateBuilding(bool clockwise = true)
        {
            EnsureCursorInitialized();

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

            // Rotate the building in the specified direction
            // Rotation values: 0=N, 1=W, 2=S, 3=E — incrementing is counterclockwise
            int direction = clockwise ? -1 : 1;
            int newRotation = GameReflection.RotatePlacedBuildingDirection(objectOn, direction);
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
