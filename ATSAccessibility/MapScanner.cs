using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// 3-level hierarchical scanner for quick map object location.
    /// Categories: Glades / Resources / Buildings
    /// Groups: Types within category (e.g., "Clay Deposit", "Small Warehouse")
    /// Items: Individual instances within a group
    /// </summary>
    public class MapScanner
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        /// <summary>
        /// A group of items of the same type (e.g., all "Clay Deposits").
        /// </summary>
        public class ItemGroup
        {
            public string TypeName;           // "Clay Deposit", "Small Warehouse", "Dangerous Glade"
            public List<ScannedItem> Items;   // Sorted by distance at scan time

            public ItemGroup(string typeName)
            {
                TypeName = typeName;
                Items = new List<ScannedItem>();
            }
        }

        /// <summary>
        /// A single scanned item with position and distance.
        /// </summary>
        public class ScannedItem
        {
            public Vector2Int Position;
            public int Distance;  // Manhattan distance from cursor at scan time

            public ScannedItem(Vector2Int position, int distance)
            {
                Position = position;
                Distance = distance;
            }
        }

        // ========================================
        // STATE
        // ========================================

        private enum ScanCategory
        {
            Glades = 0,
            Resources = 1,
            Buildings = 2
        }

        private ScanCategory _currentCategory = ScanCategory.Glades;
        private int _currentGroupIndex = 0;
        private int _currentItemIndex = 0;
        private List<ItemGroup> _cachedGroups = null;

        private readonly MapNavigator _mapNavigator;

        // Reflection cache for scanning
        private FieldInfo _gladeFieldsField = null;
        private FieldInfo _gladeDangerLevelField = null;
        private FieldInfo _gladeWasDiscoveredField = null;
        private PropertyInfo _naturalResourcesProperty = null;
        private PropertyInfo _depositsProperty = null;
        private PropertyInfo _buildingsProperty = null;
        private bool _reflectionCached = false;

        // ========================================
        // CONSTRUCTOR
        // ========================================

        public MapScanner(MapNavigator mapNavigator)
        {
            _mapNavigator = mapNavigator;
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Change category (Ctrl+PageUp/Down). Full rescan.
        /// </summary>
        public void ChangeCategory(int direction)
        {
            int categoryCount = 3; // Glades, Resources, Buildings
            int newCategory = ((int)_currentCategory + direction + categoryCount) % categoryCount;
            _currentCategory = (ScanCategory)newCategory;

            // Full rescan
            ScanCurrentCategory();
            _currentGroupIndex = 0;
            _currentItemIndex = 0;

            // Build combined announcement: "Category: item info" or "Category: no items"
            string categoryName = _currentCategory switch
            {
                ScanCategory.Glades => "Glades",
                ScanCategory.Resources => "Resources",
                ScanCategory.Buildings => "Buildings",
                _ => "Unknown"
            };

            if (_cachedGroups == null || _cachedGroups.Count == 0 || _cachedGroups[0].Items.Count == 0)
            {
                Speech.Say($"{categoryName}, none");
            }
            else
            {
                var currentGroup = _cachedGroups[_currentGroupIndex];
                int itemNum = _currentItemIndex + 1;
                int itemTotal = currentGroup.Items.Count;
                Speech.Say($"{categoryName}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
            }
        }

        /// <summary>
        /// Change group within category (PageUp/Down). Category rescan.
        /// </summary>
        public void ChangeGroup(int direction)
        {
            // Rescan category to get fresh data
            ScanCurrentCategory();
            _currentItemIndex = 0;

            if (_cachedGroups == null || _cachedGroups.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            _currentGroupIndex = (_currentGroupIndex + direction + _cachedGroups.Count) % _cachedGroups.Count;
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Change item within group (Alt+PageUp/Down). No rescan.
        /// </summary>
        public void ChangeItem(int direction)
        {
            if (_cachedGroups == null || _cachedGroups.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var currentGroup = _cachedGroups[_currentGroupIndex];
            if (currentGroup.Items.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            _currentItemIndex = (_currentItemIndex + direction + currentGroup.Items.Count) % currentGroup.Items.Count;
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Announce distance/direction from cursor to current item (Home key).
        /// Read-only - no state changes.
        /// </summary>
        public void AnnounceDistance()
        {
            if (_cachedGroups == null || _cachedGroups.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var currentGroup = _cachedGroups[_currentGroupIndex];
            if (currentGroup.Items.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var item = currentGroup.Items[_currentItemIndex];
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            int dx = item.Position.x - cursorX;
            int dy = item.Position.y - cursorY;
            int distance = Math.Abs(dx) + Math.Abs(dy);

            string direction = GetDirection(dx, dy);
            string announcement = distance == 0 ? "here" : $"{distance} tiles {direction}";
            Speech.Say(announcement);
        }

        /// <summary>
        /// Move cursor to current item (End key).
        /// No rescan, no index changes.
        /// </summary>
        public void MoveCursorToItem()
        {
            if (_cachedGroups == null || _cachedGroups.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var currentGroup = _cachedGroups[_currentGroupIndex];
            if (currentGroup.Items.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var item = currentGroup.Items[_currentItemIndex];
            _mapNavigator.SetCursorPosition(item.Position.x, item.Position.y);

            // Announce where we moved
            Speech.Say($"moved to {currentGroup.TypeName}");
        }

        // ========================================
        // SCANNING LOGIC
        // ========================================

        private void ScanCurrentCategory()
        {
            EnsureReflectionCache();

            switch (_currentCategory)
            {
                case ScanCategory.Glades:
                    _cachedGroups = ScanGlades();
                    break;
                case ScanCategory.Resources:
                    _cachedGroups = ScanResources();
                    break;
                case ScanCategory.Buildings:
                    _cachedGroups = ScanBuildings();
                    break;
            }

            // Sort groups by nearest item distance
            if (_cachedGroups != null && _cachedGroups.Count > 0)
            {
                _cachedGroups.Sort((a, b) =>
                {
                    int distA = a.Items.Count > 0 ? a.Items[0].Distance : int.MaxValue;
                    int distB = b.Items.Count > 0 ? b.Items[0].Distance : int.MaxValue;
                    return distA.CompareTo(distB);
                });
            }
        }

        private List<ItemGroup> ScanGlades()
        {
            var groups = new Dictionary<string, ItemGroup>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            try
            {
                var allGlades = GameReflection.GetAllGlades();
                if (allGlades == null) return new List<ItemGroup>();

                // allGlades is IEnumerable<GladeState>
                var gladesList = allGlades as IEnumerable;
                if (gladesList == null) return new List<ItemGroup>();

                foreach (var glade in gladesList)
                {
                    if (glade == null) continue;

                    // Check if glade is unrevealed (only show unrevealed glades)
                    bool wasDiscovered = GetGladeWasDiscovered(glade);
                    if (wasDiscovered) continue;  // Skip revealed glades

                    // Get danger level for grouping
                    string dangerLevel = GetGladeDangerLevel(glade);
                    string groupName = $"{dangerLevel} glade";

                    // Get position (first field in glade)
                    Vector2Int position = GetGladePosition(glade);
                    if (position.x < 0 || position.y < 0) continue;

                    int distance = Math.Abs(position.x - cursorX) + Math.Abs(position.y - cursorY);

                    if (!groups.ContainsKey(groupName))
                    {
                        groups[groupName] = new ItemGroup(groupName);
                    }

                    groups[groupName].Items.Add(new ScannedItem(position, distance));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanGlades failed: {ex.Message}");
            }

            // Sort items within each group by distance
            var result = new List<ItemGroup>(groups.Values);
            foreach (var group in result)
            {
                group.Items.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            return result;
        }

        private List<ItemGroup> ScanResources()
        {
            var groups = new Dictionary<string, ItemGroup>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            try
            {
                // Scan NaturalResources
                var resourcesService = GameReflection.GetResourcesService();
                if (resourcesService != null)
                {
                    EnsureResourcesProperty(resourcesService);
                    if (_naturalResourcesProperty != null)
                    {
                        var resources = _naturalResourcesProperty.GetValue(resourcesService) as IDictionary;
                        if (resources != null)
                        {
                            foreach (DictionaryEntry entry in resources)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var resource = entry.Value;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos.x, pos.y)) continue;

                                string displayName = GetObjectDisplayName(resource);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = Math.Abs(pos.x - cursorX) + Math.Abs(pos.y - cursorY);

                                if (!groups.ContainsKey(displayName))
                                {
                                    groups[displayName] = new ItemGroup(displayName);
                                }

                                groups[displayName].Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Scan Deposits
                var depositsService = GameReflection.GetDepositsService();
                if (depositsService != null)
                {
                    EnsureDepositsProperty(depositsService);
                    if (_depositsProperty != null)
                    {
                        var deposits = _depositsProperty.GetValue(depositsService) as IDictionary;
                        if (deposits != null)
                        {
                            foreach (DictionaryEntry entry in deposits)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var deposit = entry.Value;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos.x, pos.y)) continue;

                                string displayName = GetObjectDisplayName(deposit);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = Math.Abs(pos.x - cursorX) + Math.Abs(pos.y - cursorY);

                                if (!groups.ContainsKey(displayName))
                                {
                                    groups[displayName] = new ItemGroup(displayName);
                                }

                                groups[displayName].Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanResources failed: {ex.Message}");
            }

            // Sort items within each group by distance
            var result = new List<ItemGroup>(groups.Values);
            foreach (var group in result)
            {
                group.Items.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            return result;
        }

        private List<ItemGroup> ScanBuildings()
        {
            var groups = new Dictionary<string, ItemGroup>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            try
            {
                var buildingsService = GameReflection.GetBuildingsService();
                if (buildingsService != null)
                {
                    EnsureBuildingsProperty(buildingsService);
                    if (_buildingsProperty != null)
                    {
                        var buildings = _buildingsProperty.GetValue(buildingsService) as IDictionary;
                        if (buildings != null)
                        {
                            foreach (DictionaryEntry entry in buildings)
                            {
                                var building = entry.Value;
                                if (building == null) continue;

                                // Get building position from Field property
                                Vector2Int pos = GetBuildingPosition(building);
                                if (pos.x < 0 || pos.y < 0) continue;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos.x, pos.y)) continue;

                                string displayName = GetObjectDisplayName(building);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = Math.Abs(pos.x - cursorX) + Math.Abs(pos.y - cursorY);

                                if (!groups.ContainsKey(displayName))
                                {
                                    groups[displayName] = new ItemGroup(displayName);
                                }

                                groups[displayName].Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanBuildings failed: {ex.Message}");
            }

            // Sort items within each group by distance
            var result = new List<ItemGroup>(groups.Values);
            foreach (var group in result)
            {
                group.Items.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            return result;
        }

        // ========================================
        // ANNOUNCEMENT HELPERS
        // ========================================

        private void AnnounceCurrentItem()
        {
            if (_cachedGroups == null || _cachedGroups.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var currentGroup = _cachedGroups[_currentGroupIndex];
            if (currentGroup.Items.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            int itemNum = _currentItemIndex + 1;
            int itemTotal = currentGroup.Items.Count;
            string announcement = $"{currentGroup.TypeName}, {itemNum} of {itemTotal}";
            Speech.Say(announcement);
        }

        private void AnnounceEmpty()
        {
            string categoryName = _currentCategory switch
            {
                ScanCategory.Glades => "glades",
                ScanCategory.Resources => "resources",
                ScanCategory.Buildings => "buildings",
                _ => "items"
            };
            Speech.Say($"No {categoryName}");
        }

        private string GetDirection(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return "";

            // Use primary and secondary directions
            string ns = dy > 0 ? "north" : (dy < 0 ? "south" : "");
            string ew = dx > 0 ? "east" : (dx < 0 ? "west" : "");

            // Combine for 8-way direction
            if (string.IsNullOrEmpty(ns)) return ew;
            if (string.IsNullOrEmpty(ew)) return ns;
            return ns + ew;  // e.g., "northeast"
        }

        // ========================================
        // REFLECTION HELPERS
        // ========================================

        private void EnsureReflectionCache()
        {
            if (_reflectionCached) return;

            try
            {
                // GladeState fields
                var gladesService = GameReflection.GetGladesService();
                var allGlades = GameReflection.GetAllGlades();
                if (allGlades != null)
                {
                    var gladesList = allGlades as IEnumerable;
                    if (gladesList != null)
                    {
                        foreach (var glade in gladesList)
                        {
                            if (glade != null)
                            {
                                var gladeType = glade.GetType();
                                _gladeFieldsField = gladeType.GetField("fields",
                                    BindingFlags.Public | BindingFlags.Instance);
                                _gladeDangerLevelField = gladeType.GetField("dangerLevel",
                                    BindingFlags.Public | BindingFlags.Instance);
                                _gladeWasDiscoveredField = gladeType.GetField("wasDiscovered",
                                    BindingFlags.Public | BindingFlags.Instance);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureReflectionCache failed: {ex.Message}");
            }

            _reflectionCached = true;
        }

        private void EnsureResourcesProperty(object resourcesService)
        {
            if (_naturalResourcesProperty != null) return;

            try
            {
                _naturalResourcesProperty = resourcesService.GetType().GetProperty("NaturalResources",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch { }
        }

        private void EnsureDepositsProperty(object depositsService)
        {
            if (_depositsProperty != null) return;

            try
            {
                _depositsProperty = depositsService.GetType().GetProperty("Deposits",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch { }
        }

        private void EnsureBuildingsProperty(object buildingsService)
        {
            if (_buildingsProperty != null) return;

            try
            {
                _buildingsProperty = buildingsService.GetType().GetProperty("Buildings",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch { }
        }

        private bool GetGladeWasDiscovered(object glade)
        {
            try
            {
                if (_gladeWasDiscoveredField != null)
                {
                    return (bool)_gladeWasDiscoveredField.GetValue(glade);
                }
            }
            catch { }
            return true;  // Default to discovered
        }

        private string GetGladeDangerLevel(object glade)
        {
            try
            {
                if (_gladeDangerLevelField != null)
                {
                    var dangerValue = _gladeDangerLevelField.GetValue(glade);
                    string dangerStr = dangerValue?.ToString() ?? "unknown";

                    return dangerStr switch
                    {
                        "None" => "Small",
                        "Dangerous" => "Dangerous",
                        "Forbidden" => "Forbidden",
                        _ => dangerStr
                    };
                }
            }
            catch { }
            return "Unknown";
        }

        private Vector2Int GetGladePosition(object glade)
        {
            try
            {
                if (_gladeFieldsField != null)
                {
                    var fields = _gladeFieldsField.GetValue(glade) as IList;
                    if (fields != null && fields.Count > 0)
                    {
                        return (Vector2Int)fields[0];
                    }
                }
            }
            catch { }
            return new Vector2Int(-1, -1);
        }

        private Vector2Int GetBuildingPosition(object building)
        {
            try
            {
                var fieldProp = building.GetType().GetProperty("Field",
                    BindingFlags.Public | BindingFlags.Instance);
                if (fieldProp != null)
                {
                    return (Vector2Int)fieldProp.GetValue(building);
                }
            }
            catch { }
            return new Vector2Int(-1, -1);
        }

        private string GetObjectDisplayName(object obj)
        {
            if (obj == null) return null;

            try
            {
                var objType = obj.GetType();

                // Try Model.displayName
                var modelProperty = objType.GetProperty("Model",
                    BindingFlags.Public | BindingFlags.Instance);
                if (modelProperty != null)
                {
                    var model = modelProperty.GetValue(obj);
                    if (model != null)
                    {
                        var modelType = model.GetType();

                        // Try displayName field
                        var displayNameField = modelType.GetField("displayName",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (displayNameField != null)
                        {
                            var displayName = displayNameField.GetValue(model);
                            if (displayName != null)
                            {
                                string text = displayName.ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    return text;
                                }
                            }
                        }

                        // Try name property
                        var nameProp = modelType.GetProperty("name",
                            BindingFlags.Public | BindingFlags.Instance);
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

                // Fallback to type name
                return objType.Name;
            }
            catch
            {
                return null;
            }
        }

        private bool IsInsideUnrevealedGlade(int x, int y)
        {
            var glade = GameReflection.GetGlade(x, y);
            if (glade == null) return false;

            return !GetGladeWasDiscovered(glade);
        }
    }
}
