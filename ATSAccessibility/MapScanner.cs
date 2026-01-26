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
            public string BuildingTypeName;   // Runtime type name for subcategory lookup (e.g., "Hearth", "Workshop")
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

        // Building subcategory state
        private int _currentSubcategoryIndex = 0;
        private Dictionary<int, List<ItemGroup>> _cachedBuildingsBySubcategory = null;
        private Dictionary<int, List<ItemGroup>> _cachedResourcesBySubcategory = null;

        private readonly MapNavigator _mapNavigator;

        // ========================================
        // BUILDING SUBCATEGORY DEFINITIONS
        // ========================================

        private static readonly string[] SubcategoryNames = new string[]
        {
            "Essential", "Gathering", "Production", "Trade",
            "Housing and Services", "Special Buildings",
            "Blight Fighting", "Decorations", "Ruins", "Roads"
        };

        private static readonly Dictionary<string, int> BuildingTypeToSubcategory = new Dictionary<string, int>
        {
            // Essential (0)
            { "Hearth", 0 }, { "Storage", 0 },
            // Gathering (1)
            { "Camp", 1 }, { "GathererHut", 1 }, { "Farm", 1 }, { "Farmfield", 1 },
            { "FishingHut", 1 }, { "Mine", 1 }, { "Extractor", 1 }, { "RainCatcher", 1 }, { "Collector", 1 },
            // Production (2)
            { "Workshop", 2 },
            // Trade (3)
            { "TradingPost", 3 }, { "PerkCrafter", 3 }, { "BlackMarket", 3 },
            // Housing and Services (4)
            { "House", 4 }, { "Institution", 4 },
            // Special Buildings (5)
            { "Port", 5 }, { "Altar", 5 }, { "Shrine", 5 }, { "Seal", 5 }, { "Poro", 5 }, { "Spawner", 5 },
            // Blight Fighting (6)
            { "BlightPost", 6 }, { "Hydrant", 6 },
            // Decorations (7)
            { "Decoration", 7 },
            // Ruins (8)
            { "Relic", 8 },
            // Roads (9)
            { "Road", 9 }
        };

        private static readonly string[] ResourceSubcategoryNames = new string[]
        {
            "Natural Resources",
            "Extracted Resources",
            "Collected Resources"
        };

        // Reflection cache for scanning
        private FieldInfo _gladeFieldsField = null;
        private FieldInfo _gladeDangerLevelField = null;
        private FieldInfo _gladeWasDiscoveredField = null;
        private PropertyInfo _naturalResourcesProperty = null;
        private PropertyInfo _depositsProperty = null;
        private PropertyInfo _oresProperty = null;
        private PropertyInfo _springsProperty = null;
        private PropertyInfo _lakesProperty = null;
        private PropertyInfo _buildingsProperty = null;
        private PropertyInfo _fieldTypeProperty = null;
        private bool _fieldTypeCached = false;
        private bool _reflectionCached = false;

        // Unrevealed glade tiles cache (rebuilt each scan)
        private HashSet<Vector2Int> _unrevealedGladeTiles = null;

        // ========================================
        // CONSTRUCTOR
        // ========================================

        public MapScanner(MapNavigator mapNavigator)
        {
            _mapNavigator = mapNavigator;
        }

        // ========================================
        // STATIC COMPARERS (avoid closure allocations)
        // ========================================

        private static int CompareGroupsByDistance(ItemGroup a, ItemGroup b)
        {
            int distA = a.Items.Count > 0 ? a.Items[0].Distance : int.MaxValue;
            int distB = b.Items.Count > 0 ? b.Items[0].Distance : int.MaxValue;
            return distA.CompareTo(distB);
        }

        private static int CompareItemsByDistance(ScannedItem a, ScannedItem b)
        {
            return a.Distance.CompareTo(b.Distance);
        }

        /// <summary>
        /// Calculate Chebyshev distance (max of dx, dy) from cursor to position.
        /// </summary>
        private static int CalculateDistance(Vector2Int pos, int cursorX, int cursorY)
        {
            return Math.Max(Math.Abs(pos.x - cursorX), Math.Abs(pos.y - cursorY));
        }

        /// <summary>
        /// Finalize group dictionary into sorted list (sort items within each group by distance).
        /// </summary>
        private static List<ItemGroup> FinalizeGroups(Dictionary<string, ItemGroup> groups)
        {
            var result = new List<ItemGroup>(groups.Values);
            foreach (var group in result)
            {
                group.Items.Sort(CompareItemsByDistance);
            }
            return result;
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Change category (Ctrl+PageUp/Down). Full rescan.
        /// </summary>
        public void ChangeCategory(int direction)
        {
            const int categoryCount = 3; // Glades, Resources, Buildings
            _currentCategory = (ScanCategory)NavigationUtils.WrapIndex((int)_currentCategory, direction, categoryCount);

            // Reset subcategory state
            _currentSubcategoryIndex = 0;
            _cachedBuildingsBySubcategory = null;
            _cachedResourcesBySubcategory = null;
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

            // For Buildings, use subcategory system
            if (_currentCategory == ScanCategory.Buildings)
            {
                // Build unrevealed glade tiles map first
                EnsureReflectionCache();
                BuildUnrevealedGladeTilesMap();

                ScanBuildingsWithSubcategories();
                _unrevealedGladeTiles = null;

                // Find first non-empty subcategory
                bool foundSubcategory = false;
                for (int i = 0; i < SubcategoryNames.Length; i++)
                {
                    if (_cachedBuildingsBySubcategory.TryGetValue(i, out var groups) && groups.Count > 0)
                    {
                        _currentSubcategoryIndex = i;
                        _cachedGroups = groups;
                        foundSubcategory = true;
                        break;
                    }
                }

                if (!foundSubcategory || _cachedGroups == null || _cachedGroups.Count == 0)
                {
                    Speech.Say($"{categoryName}, none");
                }
                else
                {
                    var currentGroup = _cachedGroups[_currentGroupIndex];
                    int itemNum = _currentItemIndex + 1;
                    int itemTotal = currentGroup.Items.Count;
                    // Intentional: "X of Y" position context is useful for scanner navigation
                    Speech.Say($"{categoryName}, {SubcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
                }
            }
            else if (_currentCategory == ScanCategory.Resources)
            {
                // Resources use subcategory system
                EnsureReflectionCache();
                BuildUnrevealedGladeTilesMap();
                ScanResourcesWithSubcategories();
                _unrevealedGladeTiles = null;

                // Find first non-empty subcategory
                bool foundSubcategory = false;
                for (int i = 0; i < ResourceSubcategoryNames.Length; i++)
                {
                    if (_cachedResourcesBySubcategory.TryGetValue(i, out var groups) && groups.Count > 0)
                    {
                        _currentSubcategoryIndex = i;
                        _cachedGroups = groups;
                        foundSubcategory = true;
                        break;
                    }
                }

                if (!foundSubcategory || _cachedGroups == null || _cachedGroups.Count == 0)
                {
                    Speech.Say($"{categoryName}, none");
                }
                else
                {
                    var currentGroup = _cachedGroups[_currentGroupIndex];
                    int itemNum = _currentItemIndex + 1;
                    int itemTotal = currentGroup.Items.Count;
                    // Intentional: "X of Y" position context is useful for scanner navigation
                    Speech.Say($"{categoryName}, {ResourceSubcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
                }
            }
            else
            {
                // For Glades, use standard scanning
                ScanCurrentCategory();

                if (_cachedGroups == null || _cachedGroups.Count == 0 || _cachedGroups[0].Items.Count == 0)
                {
                    Speech.Say($"{categoryName}, none");
                }
                else
                {
                    var currentGroup = _cachedGroups[_currentGroupIndex];
                    int itemNum = _currentItemIndex + 1;
                    int itemTotal = currentGroup.Items.Count;
                    // Intentional: "X of Y" position context is useful for scanner navigation
                    Speech.Say($"{categoryName}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
                }
            }
        }

        /// <summary>
        /// Change group within category (PageUp/Down). Category rescan.
        /// For Buildings, navigates within current subcategory only.
        /// </summary>
        public void ChangeGroup(int direction)
        {
            _currentItemIndex = 0;

            // For Buildings, use subcategory groups
            if (_currentCategory == ScanCategory.Buildings)
            {
                // Rescan if needed
                if (_cachedBuildingsBySubcategory == null)
                {
                    EnsureReflectionCache();
                    BuildUnrevealedGladeTilesMap();
                    ScanBuildingsWithSubcategories();
                    _unrevealedGladeTiles = null;
                }

                // Get groups from current subcategory
                if (!_cachedBuildingsBySubcategory.TryGetValue(_currentSubcategoryIndex, out var subcategoryGroups) || subcategoryGroups.Count == 0)
                {
                    AnnounceEmpty();
                    return;
                }

                _cachedGroups = subcategoryGroups;
                _currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
                AnnounceCurrentItem();
            }
            else if (_currentCategory == ScanCategory.Resources)
            {
                // Rescan if needed
                if (_cachedResourcesBySubcategory == null)
                {
                    EnsureReflectionCache();
                    BuildUnrevealedGladeTilesMap();
                    ScanResourcesWithSubcategories();
                    _unrevealedGladeTiles = null;
                }

                // Get groups from current subcategory
                if (!_cachedResourcesBySubcategory.TryGetValue(_currentSubcategoryIndex, out var subcategoryGroups) || subcategoryGroups.Count == 0)
                {
                    AnnounceEmpty();
                    return;
                }

                _cachedGroups = subcategoryGroups;
                _currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
                AnnounceCurrentItem();
            }
            else
            {
                // For Glades, use standard scanning
                ScanCurrentCategory();

                if (_cachedGroups == null || _cachedGroups.Count == 0)
                {
                    AnnounceEmpty();
                    return;
                }

                _currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
                AnnounceCurrentItem();
            }
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

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, currentGroup.Items.Count);
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
            int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

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

            // Build unrevealed glade tiles map for Resources/Buildings scans (O(1) lookup)
            if (_currentCategory == ScanCategory.Resources || _currentCategory == ScanCategory.Buildings)
            {
                BuildUnrevealedGladeTilesMap();
            }

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

            // Clear glade tiles cache after scan
            _unrevealedGladeTiles = null;

            // Sort groups by nearest item distance
            if (_cachedGroups != null && _cachedGroups.Count > 0)
            {
                _cachedGroups.Sort(CompareGroupsByDistance);
            }
        }

        private void BuildUnrevealedGladeTilesMap()
        {
            _unrevealedGladeTiles = new HashSet<Vector2Int>();

            try
            {
                var allGlades = GameReflection.GetAllGlades();
                if (allGlades == null) return;

                var gladesList = allGlades as IEnumerable;
                if (gladesList == null) return;

                foreach (var glade in gladesList)
                {
                    if (glade == null) continue;

                    // Only include unrevealed glades
                    if (GetGladeWasDiscovered(glade)) continue;

                    // Get all fields in this glade
                    if (_gladeFieldsField != null)
                    {
                        var fields = _gladeFieldsField.GetValue(glade) as IList;
                        if (fields != null)
                        {
                            foreach (var field in fields)
                            {
                                _unrevealedGladeTiles.Add((Vector2Int)field);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] BuildUnrevealedGladeTilesMap failed: {ex.Message}");
            }
        }

        private List<ItemGroup> ScanGlades()
        {
            var groups = new Dictionary<string, ItemGroup>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;
            bool hasGladeInfo = GameReflection.HasGladeInfo();
            bool hasDangerousGladeInfo = GameReflection.HasDangerousGladeInfo();

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

                    // Build group name based on what info is available
                    string groupName;
                    if (!hasDangerousGladeInfo)
                    {
                        // Cursed Royal Woodlands: ALL glade markers are hidden
                        groupName = "Unknown glade";
                    }
                    else if (hasGladeInfo)
                    {
                        // Has glade info perk - show type and contents
                        string contents = GameReflection.GetGladeContentsSummary(glade);
                        groupName = $"{dangerLevel} glade: {contents}";
                    }
                    else
                    {
                        // Normal biome without glade info perk - show type only
                        groupName = $"{dangerLevel} glade";
                    }

                    // Get position (first field in glade)
                    Vector2Int position = GetGladePosition(glade);
                    if (position.x < 0 || position.y < 0) continue;

                    int distance = CalculateDistance(position, cursorX, cursorY);

                    if (!groups.TryGetValue(groupName, out var group))
                    {
                        group = new ItemGroup(groupName);
                        groups[groupName] = group;
                    }

                    group.Items.Add(new ScannedItem(position, distance));
                }

                // Add location marker groups (grass/spring markers)
                ScanLocationMarkers(groups, cursorX, cursorY);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanGlades failed: {ex.Message}");
            }

            return FinalizeGroups(groups);
        }

        /// <summary>
        /// Helper to scan a single type of location marker and add to groups.
        /// </summary>
        private void ScanLocationMarkerType(
            List<Vector2Int> locations,
            string groupName,
            Dictionary<string, ItemGroup> groups,
            int cursorX, int cursorY)
        {
            if (locations == null || locations.Count == 0) return;

            var group = new ItemGroup(groupName);
            foreach (var pos in locations)
            {
                // Only include if in unrevealed glade
                if (IsInsideUnrevealedGlade(pos))
                {
                    group.Items.Add(new ScannedItem(pos, CalculateDistance(pos, cursorX, cursorY)));
                }
            }
            if (group.Items.Count > 0)
            {
                group.Items.Sort(CompareItemsByDistance);
                groups[groupName] = group;
            }
        }

        /// <summary>
        /// Scan for location markers (grass/spring/relic) and add them as groups.
        /// Only includes markers in unrevealed glades.
        /// </summary>
        private void ScanLocationMarkers(Dictionary<string, ItemGroup> groups, int cursorX, int cursorY)
        {
            // Scan location marker types (grass/spring/relic)
            ScanLocationMarkerType(GameReflection.GetRevealedGrassLocations(), "Grass marker", groups, cursorX, cursorY);
            ScanLocationMarkerType(GameReflection.GetRevealedSpringsLocations(), "Spring marker", groups, cursorX, cursorY);
            ScanLocationMarkerType(GameReflection.GetRevealedRelicLocations(), "Relic marker", groups, cursorX, cursorY);

            // Highlighted relics (from Short Range Scanner, etc)
            var highlightedRelics = GameReflection.GetHighlightedRelics();
            if (highlightedRelics != null && highlightedRelics.Count > 0)
            {
                foreach (var kvp in highlightedRelics)
                {
                    var pos = kvp.Key;
                    var relicName = kvp.Value;

                    // Only include if in unrevealed glade
                    if (IsInsideUnrevealedGlade(pos))
                    {
                        // Get display name for the relic
                        string displayName = GameReflection.GetRelicDisplayName(relicName);
                        string highlightedGroupName = $"Highlighted: {displayName}";
                        if (!groups.ContainsKey(highlightedGroupName))
                        {
                            var group = new ItemGroup(highlightedGroupName);
                            group.Items.Add(new ScannedItem(pos, CalculateDistance(pos, cursorX, cursorY)));
                            groups[highlightedGroupName] = group;
                        }
                    }
                }
            }
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
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(resource);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                // Separate marked (for woodcutting) from unmarked
                                bool isMarked = GameReflection.IsNaturalResourceMarked(resource);
                                string groupName = isMarked ? $"Marked {displayName}" : displayName;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(groupName, out var group))
                                {
                                    group = new ItemGroup(groupName);
                                    groups[groupName] = group;
                                }

                                group.Items.Add(new ScannedItem(pos, distance));
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
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(deposit);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(displayName, out var depositGroup))
                                {
                                    depositGroup = new ItemGroup(displayName);
                                    groups[displayName] = depositGroup;
                                }

                                depositGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Scan Ores (copper veins, etc.)
                var oreService = GameReflection.GetOreService();
                if (oreService != null)
                {
                    EnsureOresProperty(oreService);
                    if (_oresProperty != null)
                    {
                        var ores = _oresProperty.GetValue(oreService) as IDictionary;
                        if (ores != null)
                        {
                            foreach (DictionaryEntry entry in ores)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var ore = entry.Value;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(ore);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(displayName, out var oreGroup))
                                {
                                    oreGroup = new ItemGroup(displayName);
                                    groups[displayName] = oreGroup;
                                }

                                oreGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Scan Springs (water geysers)
                var springsService = GameReflection.GetSpringsService();
                if (springsService != null)
                {
                    EnsureSpringsProperty(springsService);
                    if (_springsProperty != null)
                    {
                        var springs = _springsProperty.GetValue(springsService) as IDictionary;
                        if (springs != null)
                        {
                            foreach (DictionaryEntry entry in springs)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var spring = entry.Value;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(spring);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(displayName, out var springGroup))
                                {
                                    springGroup = new ItemGroup(displayName);
                                    groups[displayName] = springGroup;
                                }

                                springGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Scan Lakes (fishing spots)
                var lakesService = GameReflection.GetLakesService();
                if (lakesService != null)
                {
                    EnsureLakesProperty(lakesService);
                    if (_lakesProperty != null)
                    {
                        var lakes = _lakesProperty.GetValue(lakesService) as IDictionary;
                        if (lakes != null)
                        {
                            foreach (DictionaryEntry entry in lakes)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var lake = entry.Value;

                                // Skip if inside unrevealed glade
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(lake);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(displayName, out var lakeGroup))
                                {
                                    lakeGroup = new ItemGroup(displayName);
                                    groups[displayName] = lakeGroup;
                                }

                                lakeGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Scan Fertile Soil (fields with type "Grass")
                int mapWidth = GameReflection.GetMapWidth();
                int mapHeight = GameReflection.GetMapHeight();
                var fertileSoilGroup = new ItemGroup("Fertile Soil");

                for (int x = 0; x < mapWidth; x++)
                {
                    for (int y = 0; y < mapHeight; y++)
                    {
                        var pos = new Vector2Int(x, y);
                        if (IsInsideUnrevealedGlade(pos)) continue;

                        var field = GameReflection.GetField(x, y);
                        if (field == null) continue;

                        string typeName = GetFieldTypeName(field);
                        if (typeName == "Grass")
                        {
                            int distance = CalculateDistance(pos, cursorX, cursorY);
                            fertileSoilGroup.Items.Add(new ScannedItem(pos, distance));
                        }
                    }
                }

                if (fertileSoilGroup.Items.Count > 0)
                {
                    fertileSoilGroup.Items.Sort(CompareItemsByDistance);
                    groups["Fertile Soil"] = fertileSoilGroup;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanResources failed: {ex.Message}");
            }

            return FinalizeGroups(groups);
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
                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetBuildingDisplayName(building);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!groups.TryGetValue(displayName, out var group))
                                {
                                    group = new ItemGroup(displayName);
                                    group.BuildingTypeName = GetBuildingTypeName(building);
                                    groups[displayName] = group;
                                }

                                group.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanBuildings failed: {ex.Message}");
            }

            return FinalizeGroups(groups);
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
            // Intentional: "X of Y" position context is useful for scanner navigation
            Speech.Say($"{currentGroup.TypeName}, {itemNum} of {itemTotal}");
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

            int absDx = Math.Abs(dx);
            int absDy = Math.Abs(dy);

            // Only use diagonal if both axes are significant (within 2:1 ratio)
            bool useNS = absDy > 0 && absDy * 2 >= absDx;
            bool useEW = absDx > 0 && absDx * 2 >= absDy;

            string ns = useNS ? (dy > 0 ? "north" : "south") : "";
            string ew = useEW ? (dx > 0 ? "east" : "west") : "";

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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureResourcesProperty failed: {ex.Message}"); }
        }

        private void EnsureDepositsProperty(object depositsService)
        {
            if (_depositsProperty != null) return;

            try
            {
                _depositsProperty = depositsService.GetType().GetProperty("Deposits",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureDepositsProperty failed: {ex.Message}"); }
        }

        private void EnsureOresProperty(object oreService)
        {
            if (_oresProperty != null) return;

            try
            {
                _oresProperty = oreService.GetType().GetProperty("Ore",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureOresProperty failed: {ex.Message}"); }
        }

        private void EnsureSpringsProperty(object springsService)
        {
            if (_springsProperty != null) return;

            try
            {
                _springsProperty = springsService.GetType().GetProperty("Springs",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureSpringsProperty failed: {ex.Message}"); }
        }

        private void EnsureLakesProperty(object lakesService)
        {
            if (_lakesProperty != null) return;

            try
            {
                _lakesProperty = lakesService.GetType().GetProperty("Lakes",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureLakesProperty failed: {ex.Message}"); }
        }

        private void EnsureBuildingsProperty(object buildingsService)
        {
            if (_buildingsProperty != null) return;

            try
            {
                _buildingsProperty = buildingsService.GetType().GetProperty("Buildings",
                    BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] EnsureBuildingsProperty failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladeWasDiscovered failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladeDangerLevel failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladePosition failed: {ex.Message}"); }
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetBuildingPosition failed: {ex.Message}"); }
            return new Vector2Int(-1, -1);
        }

        private string GetFieldTypeName(object field)
        {
            if (field == null) return null;

            if (!_fieldTypeCached)
            {
                _fieldTypeProperty = field.GetType().GetProperty("Type");
                _fieldTypeCached = true;
            }

            if (_fieldTypeProperty == null) return null;

            try
            {
                var typeValue = _fieldTypeProperty.GetValue(field);
                if (typeValue == null) return null;

                var typeType = typeValue.GetType();

                // Try displayName first (matches navigator's approach)
                var displayNameProp = typeType.GetProperty("displayName");
                if (displayNameProp != null)
                {
                    var displayName = displayNameProp.GetValue(typeValue);
                    if (displayName != null)
                    {
                        string text = displayName.ToString();
                        if (!string.IsNullOrEmpty(text)) return text;
                    }
                }

                // Fallback to name
                var nameProp = typeType.GetProperty("name");
                if (nameProp != null)
                {
                    var name = nameProp.GetValue(typeValue);
                    if (name != null) return name.ToString();
                }

                // Final fallback to ToString()
                return typeValue.ToString();
            }
            catch { }

            return null;
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetObjectDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if position is inside an unrevealed glade (Vector2Int overload, avoids allocation in hot path).
        /// </summary>
        private bool IsInsideUnrevealedGlade(Vector2Int pos)
        {
            // Use cached HashSet for O(1) lookup
            if (_unrevealedGladeTiles != null)
            {
                return _unrevealedGladeTiles.Contains(pos);
            }

            // Fallback if cache not built (shouldn't happen in normal flow)
            var glade = GameReflection.GetGlade(pos.x, pos.y);
            if (glade == null) return false;
            return !GetGladeWasDiscovered(glade);
        }

        private bool IsInsideUnrevealedGlade(int x, int y)
        {
            return IsInsideUnrevealedGlade(new Vector2Int(x, y));
        }

        // ========================================
        // BUILDING SUBCATEGORY HELPERS
        // ========================================

        /// <summary>
        /// Get the runtime type name of a building (e.g., "Hearth", "Workshop").
        /// </summary>
        private string GetBuildingTypeName(object building)
        {
            if (building == null) return null;

            try
            {
                // Get the base type name without "State" suffix
                var typeName = building.GetType().Name;

                // Remove "State" suffix if present (e.g., "HearthState" -> "Hearth")
                if (typeName.EndsWith("State"))
                {
                    typeName = typeName.Substring(0, typeName.Length - 5);
                }

                return typeName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the subcategory index for a building based on its type.
        /// </summary>
        private int GetBuildingSubcategoryIndex(object building)
        {
            var typeName = GetBuildingTypeName(building);
            if (typeName == null) return SubcategoryNames.Length - 1; // Default to last (Roads)

            if (BuildingTypeToSubcategory.TryGetValue(typeName, out int index))
            {
                return index;
            }

            // Default to last (Roads) for unknown types
            return SubcategoryNames.Length - 1;
        }

        /// <summary>
        /// Get the display name of a building from BuildingsService.
        /// Uses BuildingModel.displayName (same pattern as TileInfoReader).
        /// </summary>
        private string GetBuildingDisplayName(object building)
        {
            if (building == null) return null;

            try
            {
                var buildingType = building.GetType();

                // Try BuildingModel property first (like TileInfoReader uses)
                var buildingModelProp = buildingType.GetProperty("BuildingModel",
                    BindingFlags.Public | BindingFlags.Instance);
                if (buildingModelProp != null)
                {
                    var buildingModel = buildingModelProp.GetValue(building);
                    if (buildingModel != null)
                    {
                        var modelType = buildingModel.GetType();

                        // Try displayName field
                        var displayNameField = modelType.GetField("displayName",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (displayNameField != null)
                        {
                            var displayName = displayNameField.GetValue(buildingModel);
                            if (displayName != null)
                            {
                                string text = displayName.ToString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    return text;
                                }
                            }
                        }
                    }
                }

                // Fallback to Model property
                var modelProperty = buildingType.GetProperty("Model",
                    BindingFlags.Public | BindingFlags.Instance);
                if (modelProperty != null)
                {
                    var model = modelProperty.GetValue(building);
                    if (model != null)
                    {
                        var modelType = model.GetType();

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
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Scan all buildings and organize them by subcategory.
        /// </summary>
        private void ScanBuildingsWithSubcategories()
        {
            _cachedBuildingsBySubcategory = new Dictionary<int, List<ItemGroup>>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            // Initialize all subcategories
            for (int i = 0; i < SubcategoryNames.Length; i++)
            {
                _cachedBuildingsBySubcategory[i] = new List<ItemGroup>();
            }

            try
            {
                var buildingsService = GameReflection.GetBuildingsService();
                if (buildingsService == null) return;

                EnsureBuildingsProperty(buildingsService);
                if (_buildingsProperty == null) return;

                var buildings = _buildingsProperty.GetValue(buildingsService) as IDictionary;
                if (buildings == null) return;

                // Group buildings by (subcategory, displayName)
                var groupsByKey = new Dictionary<(int subcategory, string displayName), ItemGroup>();

                foreach (DictionaryEntry entry in buildings)
                {
                    var building = entry.Value;
                    if (building == null) continue;

                    // Get building position
                    Vector2Int pos = GetBuildingPosition(building);
                    if (pos.x < 0 || pos.y < 0) continue;

                    // Skip if inside unrevealed glade
                    if (IsInsideUnrevealedGlade(pos)) continue;

                    // Get building info
                    string displayName = GetBuildingDisplayName(building);
                    if (string.IsNullOrEmpty(displayName)) continue;

                    string buildingTypeName = GetBuildingTypeName(building);
                    int subcategoryIndex = GetBuildingSubcategoryIndex(building);

                    int distance = CalculateDistance(pos, cursorX, cursorY);

                    var key = (subcategoryIndex, displayName);
                    if (!groupsByKey.TryGetValue(key, out var group))
                    {
                        group = new ItemGroup(displayName);
                        group.BuildingTypeName = buildingTypeName;
                        groupsByKey[key] = group;
                    }

                    group.Items.Add(new ScannedItem(pos, distance));
                }

                // Distribute groups to subcategories
                foreach (var kvp in groupsByKey)
                {
                    int subcategory = kvp.Key.subcategory;
                    var group = kvp.Value;

                    // Sort items by distance
                    group.Items.Sort(CompareItemsByDistance);

                    _cachedBuildingsBySubcategory[subcategory].Add(group);
                }

                // Sort groups within each subcategory by distance
                foreach (var subcategory in _cachedBuildingsBySubcategory.Values)
                {
                    subcategory.Sort(CompareGroupsByDistance);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanBuildingsWithSubcategories failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Scan all resources and organize them by subcategory.
        /// Subcategory 0 (Natural Resources): NaturalResources + Fertile Soil
        /// Subcategory 1 (Extracted Resources): Ores + Springs
        /// Subcategory 2 (Collected Resources): Deposits + Lakes
        /// </summary>
        private void ScanResourcesWithSubcategories()
        {
            _cachedResourcesBySubcategory = new Dictionary<int, List<ItemGroup>>();
            int cursorX = _mapNavigator.CursorX;
            int cursorY = _mapNavigator.CursorY;

            for (int i = 0; i < ResourceSubcategoryNames.Length; i++)
            {
                _cachedResourcesBySubcategory[i] = new List<ItemGroup>();
            }

            // One group dictionary per subcategory
            var naturalGroups = new Dictionary<string, ItemGroup>();
            var extractedGroups = new Dictionary<string, ItemGroup>();
            var collectedGroups = new Dictionary<string, ItemGroup>();

            try
            {
                // === Subcategory 0: Natural Resources ===

                // NaturalResources service
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

                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(resource);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                bool isMarked = GameReflection.IsNaturalResourceMarked(resource);
                                string groupName = isMarked ? $"Marked {displayName}" : displayName;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!naturalGroups.TryGetValue(groupName, out var group))
                                {
                                    group = new ItemGroup(groupName);
                                    naturalGroups[groupName] = group;
                                }

                                group.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Fertile Soil
                int mapWidth = GameReflection.GetMapWidth();
                int mapHeight = GameReflection.GetMapHeight();
                var fertileSoilGroup = new ItemGroup("Fertile Soil");

                for (int x = 0; x < mapWidth; x++)
                {
                    for (int y = 0; y < mapHeight; y++)
                    {
                        var pos = new Vector2Int(x, y);
                        if (IsInsideUnrevealedGlade(pos)) continue;

                        var field = GameReflection.GetField(x, y);
                        if (field == null) continue;

                        string typeName = GetFieldTypeName(field);
                        if (typeName == "Grass")
                        {
                            int distance = CalculateDistance(pos, cursorX, cursorY);
                            fertileSoilGroup.Items.Add(new ScannedItem(pos, distance));
                        }
                    }
                }

                if (fertileSoilGroup.Items.Count > 0)
                {
                    fertileSoilGroup.Items.Sort(CompareItemsByDistance);
                    naturalGroups["Fertile Soil"] = fertileSoilGroup;
                }

                // === Subcategory 1: Extracted Resources ===

                // Ores service
                var oreService = GameReflection.GetOreService();
                if (oreService != null)
                {
                    EnsureOresProperty(oreService);
                    if (_oresProperty != null)
                    {
                        var ores = _oresProperty.GetValue(oreService) as IDictionary;
                        if (ores != null)
                        {
                            foreach (DictionaryEntry entry in ores)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var ore = entry.Value;

                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(ore);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!extractedGroups.TryGetValue(displayName, out var oreGroup))
                                {
                                    oreGroup = new ItemGroup(displayName);
                                    extractedGroups[displayName] = oreGroup;
                                }

                                oreGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Springs service
                var springsService = GameReflection.GetSpringsService();
                if (springsService != null)
                {
                    EnsureSpringsProperty(springsService);
                    if (_springsProperty != null)
                    {
                        var springs = _springsProperty.GetValue(springsService) as IDictionary;
                        if (springs != null)
                        {
                            foreach (DictionaryEntry entry in springs)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var spring = entry.Value;

                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(spring);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!extractedGroups.TryGetValue(displayName, out var springGroup))
                                {
                                    springGroup = new ItemGroup(displayName);
                                    extractedGroups[displayName] = springGroup;
                                }

                                springGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // === Subcategory 2: Collected Resources ===

                // Deposits service
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

                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(deposit);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!collectedGroups.TryGetValue(displayName, out var depositGroup))
                                {
                                    depositGroup = new ItemGroup(displayName);
                                    collectedGroups[displayName] = depositGroup;
                                }

                                depositGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Lakes service
                var lakesService = GameReflection.GetLakesService();
                if (lakesService != null)
                {
                    EnsureLakesProperty(lakesService);
                    if (_lakesProperty != null)
                    {
                        var lakes = _lakesProperty.GetValue(lakesService) as IDictionary;
                        if (lakes != null)
                        {
                            foreach (DictionaryEntry entry in lakes)
                            {
                                var pos = (Vector2Int)entry.Key;
                                var lake = entry.Value;

                                if (IsInsideUnrevealedGlade(pos)) continue;

                                string displayName = GetObjectDisplayName(lake);
                                if (string.IsNullOrEmpty(displayName)) continue;

                                int distance = CalculateDistance(pos, cursorX, cursorY);

                                if (!collectedGroups.TryGetValue(displayName, out var lakeGroup))
                                {
                                    lakeGroup = new ItemGroup(displayName);
                                    collectedGroups[displayName] = lakeGroup;
                                }

                                lakeGroup.Items.Add(new ScannedItem(pos, distance));
                            }
                        }
                    }
                }

                // Finalize and sort each subcategory
                var naturalList = FinalizeGroups(naturalGroups);
                naturalList.Sort(CompareGroupsByDistance);
                _cachedResourcesBySubcategory[0] = naturalList;

                var extractedList = FinalizeGroups(extractedGroups);
                extractedList.Sort(CompareGroupsByDistance);
                _cachedResourcesBySubcategory[1] = extractedList;

                var collectedList = FinalizeGroups(collectedGroups);
                collectedList.Sort(CompareGroupsByDistance);
                _cachedResourcesBySubcategory[2] = collectedList;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] ScanResourcesWithSubcategories failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Navigate to next/previous subcategory (Shift+PageUp/Down).
        /// Applies to Buildings and Resources categories. Skips empty subcategories.
        /// </summary>
        public void ChangeSubcategory(int direction)
        {
            if (_currentCategory == ScanCategory.Buildings)
            {
                // Rescan if needed
                if (_cachedBuildingsBySubcategory == null)
                {
                    ScanBuildingsWithSubcategories();
                }

                ChangeSubcategoryInternal(direction, SubcategoryNames, _cachedBuildingsBySubcategory, "No buildings in any subcategory");
            }
            else if (_currentCategory == ScanCategory.Resources)
            {
                // Rescan if needed
                if (_cachedResourcesBySubcategory == null)
                {
                    EnsureReflectionCache();
                    BuildUnrevealedGladeTilesMap();
                    ScanResourcesWithSubcategories();
                    _unrevealedGladeTiles = null;
                }

                ChangeSubcategoryInternal(direction, ResourceSubcategoryNames, _cachedResourcesBySubcategory, "No resources in any subcategory");
            }
            else
            {
                Speech.Say("No subcategories");
            }
        }

        private void ChangeSubcategoryInternal(int direction, string[] subcategoryNames, Dictionary<int, List<ItemGroup>> cache, string emptyMessage)
        {
            int startIndex = _currentSubcategoryIndex;
            int attempts = 0;
            int maxAttempts = subcategoryNames.Length;

            do
            {
                _currentSubcategoryIndex = NavigationUtils.WrapIndex(_currentSubcategoryIndex, direction, subcategoryNames.Length);
                attempts++;

                if (cache.TryGetValue(_currentSubcategoryIndex, out var groups) && groups.Count > 0)
                {
                    _cachedGroups = groups;
                    _currentGroupIndex = 0;
                    _currentItemIndex = 0;

                    var currentGroup = _cachedGroups[_currentGroupIndex];
                    int itemNum = _currentItemIndex + 1;
                    int itemTotal = currentGroup.Items.Count;
                    // Intentional: "X of Y" position context is useful for scanner navigation
                    Speech.Say($"{subcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
                    return;
                }
            }
            while (attempts < maxAttempts && _currentSubcategoryIndex != startIndex);

            _currentSubcategoryIndex = startIndex;
            Speech.Say(emptyMessage);
        }
    }
}
