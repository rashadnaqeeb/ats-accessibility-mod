using ATSAccessibility.Core;
using ATSAccessibility.Handlers;
using ATSAccessibility.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// 3-level hierarchical scanner for quick map object location.
	/// Categories: Glades / Resources / Buildings
	/// Groups: Types within category (e.g., "Clay Deposit", "Small Warehouse")
	/// Items: Individual instances within a group
	/// </summary>
	public class MapScanner {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		/// <summary>
		/// A group of items of the same type (e.g., all "Clay Deposits").
		/// </summary>
		public class ItemGroup {
			public string TypeName;           // "Clay Deposit", "Small Warehouse", "Dangerous Glade"
			public string BuildingTypeName;   // Runtime type name for subcategory lookup (e.g., "Hearth", "Workshop")
			public List<ScannedItem> Items;   // Sorted by distance at scan time

			public ItemGroup(string typeName) {
				TypeName = typeName;
				Items = new List<ScannedItem>();
			}
		}

		/// <summary>
		/// A single scanned item with position and distance.
		/// </summary>
		public class ScannedItem {
			public Vector2Int Position;
			public int Distance;  // Manhattan distance from cursor at scan time

			public ScannedItem(Vector2Int position, int distance) {
				Position = position;
				Distance = distance;
			}
		}

		// ========================================
		// STATE
		// ========================================

		private enum ScanCategory {
			Glades = 0,
			Resources = 1,
			Buildings = 2,
			SearchResults = 3
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

		// Search results state
		private List<ItemGroup> _searchResultGroups = null;
		private ScanCategory _categoryBeforeSearch = ScanCategory.Glades;

		/// <summary>
		/// Whether the scanner is currently showing search results.
		/// </summary>
		public bool IsInSearchResults => _currentCategory == ScanCategory.SearchResults;

		// Scan origin for stable distance calculations when auto-move is on
		private int _scanOriginX;
		private int _scanOriginY;
		private bool _hasScanOrigin;
		private int _lastAutoMoveX = int.MinValue;
		private int _lastAutoMoveY = int.MinValue;

		// ========================================
		// BUILDING SUBCATEGORY DEFINITIONS
		// ========================================

		private static readonly string[] SubcategoryNames = new string[]
		{
			"All",  // 0 — excludes Decorations and Roads
            "Essential", "Gathering", "Production", "Trade",
			"Housing and Services", "Special Buildings",
			"Blight Fighting", "Decorations", "Ruins", "Roads"
		};

		private static readonly Dictionary<string, int> BuildingTypeToSubcategory = new Dictionary<string, int>
		{
            // Essential (1)
            { "Hearth", 1 }, { "Storage", 1 },
            // Gathering (2)
            { "Camp", 2 }, { "GathererHut", 2 }, { "Farm", 2 }, { "Farmfield", 2 },
			{ "FishingHut", 2 }, { "Mine", 2 }, { "Extractor", 2 }, { "RainCatcher", 2 }, { "Collector", 2 },
            // Production (3)
            { "Workshop", 3 },
            // Trade (4)
            { "TradingPost", 4 }, { "PerkCrafter", 4 }, { "BlackMarket", 4 },
            // Housing and Services (5)
            { "House", 5 }, { "Institution", 5 },
            // Special Buildings (6)
            { "Port", 6 }, { "Altar", 6 }, { "Shrine", 6 }, { "Seal", 6 }, { "Poro", 6 }, { "Spawner", 6 },
            // Blight Fighting (7)
            { "BlightPost", 7 }, { "Hydrant", 7 },
            // Decorations (8)
            { "Decoration", 8 },
            // Ruins (9)
            { "Relic", 9 },
            // Roads (10)
            { "Road", 10 }
		};

		private static readonly string[] ResourceSubcategoryNames = new string[]
		{
			"All",
			"Natural Resources",
			"Extracted Resources",
			"Nodes Small",
			"Nodes Large"
		};

		// (Reflection caching moved to MapReflection)
		private bool _reflectionCached = false;

		// Unrevealed glade tiles cache (rebuilt each scan)
		private HashSet<Vector2Int> _unrevealedGladeTiles = null;

		// ========================================
		// CONSTRUCTOR
		// ========================================

		public MapScanner(MapNavigator mapNavigator) {
			_mapNavigator = mapNavigator;
		}

		// ========================================
		// STATIC COMPARERS (avoid closure allocations)
		// ========================================

		private static int CompareGroupsByDistance(ItemGroup a, ItemGroup b) {
			int distA = a.Items.Count > 0 ? a.Items[0].Distance : int.MaxValue;
			int distB = b.Items.Count > 0 ? b.Items[0].Distance : int.MaxValue;
			return distA.CompareTo(distB);
		}

		private static int CompareItemsByDistance(ScannedItem a, ScannedItem b) {
			return a.Distance.CompareTo(b.Distance);
		}

		/// <summary>
		/// Calculate Chebyshev distance (max of dx, dy) from cursor to position.
		/// </summary>
		private static int CalculateDistance(Vector2Int pos, int cursorX, int cursorY) {
			return Math.Max(Math.Abs(pos.x - cursorX), Math.Abs(pos.y - cursorY));
		}

		/// <summary>
		/// Finalize group dictionary into sorted list (sort items within each group by distance).
		/// </summary>
		private static List<ItemGroup> FinalizeGroups(Dictionary<string, ItemGroup> groups) {
			var result = new List<ItemGroup>(groups.Values);
			foreach (var group in result) {
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
		public void ChangeCategory(int direction) {
			UpdateScanOrigin();
			const int categoryCount = 3; // Glades, Resources, Buildings
			_currentCategory = (ScanCategory)NavigationUtils.WrapIndex((int)_currentCategory, direction, categoryCount);

			// Reset subcategory state
			_currentSubcategoryIndex = 0;
			_cachedBuildingsBySubcategory = null;
			_cachedResourcesBySubcategory = null;
			_currentGroupIndex = 0;
			_currentItemIndex = 0;

			// Build combined announcement: "Category: item info" or "Category: no items"
			string categoryName = _currentCategory switch {
				ScanCategory.Glades => "Glades",
				ScanCategory.Resources => "Resources",
				ScanCategory.Buildings => "Buildings",
				_ => "Unknown"
			};

			// For Buildings, use subcategory system
			if (_currentCategory == ScanCategory.Buildings) {
				// Build unrevealed glade tiles map first
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();

				ScanBuildingsWithSubcategories();
				_unrevealedGladeTiles = null;

				// Find first non-empty subcategory
				bool foundSubcategory = false;
				for (int i = 0; i < SubcategoryNames.Length; i++) {
					if (_cachedBuildingsBySubcategory.TryGetValue(i, out var groups) && groups.Count > 0) {
						_currentSubcategoryIndex = i;
						_cachedGroups = groups;
						foundSubcategory = true;
						break;
					}
				}

				if (!foundSubcategory || _cachedGroups == null || _cachedGroups.Count == 0) {
					Speech.Say($"{categoryName}, none");
				} else {
					var currentGroup = _cachedGroups[_currentGroupIndex];
					int itemNum = _currentItemIndex + 1;
					int itemTotal = currentGroup.Items.Count;
					// Intentional: "X of Y" position context is useful for scanner navigation
					Speech.Say($"{categoryName}, {SubcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
				}
			} else if (_currentCategory == ScanCategory.Resources) {
				// Resources use subcategory system
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();
				ScanResourcesWithSubcategories();
				_unrevealedGladeTiles = null;

				// Find first non-empty subcategory
				bool foundSubcategory = false;
				for (int i = 0; i < ResourceSubcategoryNames.Length; i++) {
					if (_cachedResourcesBySubcategory.TryGetValue(i, out var groups) && groups.Count > 0) {
						_currentSubcategoryIndex = i;
						_cachedGroups = groups;
						foundSubcategory = true;
						break;
					}
				}

				if (!foundSubcategory || _cachedGroups == null || _cachedGroups.Count == 0) {
					Speech.Say($"{categoryName}, none");
				} else {
					var currentGroup = _cachedGroups[_currentGroupIndex];
					int itemNum = _currentItemIndex + 1;
					int itemTotal = currentGroup.Items.Count;
					// Intentional: "X of Y" position context is useful for scanner navigation
					Speech.Say($"{categoryName}, {ResourceSubcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
				}
			} else {
				// For Glades, use standard scanning
				ScanCurrentCategory();

				if (_cachedGroups == null || _cachedGroups.Count == 0 || _cachedGroups[0].Items.Count == 0) {
					Speech.Say($"{categoryName}, none");
				} else {
					var currentGroup = _cachedGroups[_currentGroupIndex];
					int itemNum = _currentItemIndex + 1;
					int itemTotal = currentGroup.Items.Count;
					// Intentional: "X of Y" position context is useful for scanner navigation
					Speech.Say($"{categoryName}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
				}
			}

			AutoMoveCursorSilent();
		}

		/// <summary>
		/// Change group within category (PageUp/Down). Always rescans for fresh data.
		/// For Buildings/Resources, navigates within current subcategory only.
		/// </summary>
		public void ChangeGroup(int direction) {
			UpdateScanOrigin();
			_currentItemIndex = 0;

			// Search results: no rescan, navigate within cached results
			if (_currentCategory == ScanCategory.SearchResults) {
				if (_searchResultGroups == null || _searchResultGroups.Count == 0) {
					AnnounceEmpty();
					return;
				}
				_cachedGroups = _searchResultGroups;
				_currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
				AnnounceCurrentItem();
				AutoMoveCursorSilent();
				return;
			}

			// For Buildings, use subcategory groups
			if (_currentCategory == ScanCategory.Buildings) {
				// Always rescan for fresh data
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();
				ScanBuildingsWithSubcategories();
				_unrevealedGladeTiles = null;

				// Get groups from current subcategory
				if (!_cachedBuildingsBySubcategory.TryGetValue(_currentSubcategoryIndex, out var subcategoryGroups) || subcategoryGroups.Count == 0) {
					AnnounceEmpty();
					return;
				}

				_cachedGroups = subcategoryGroups;
				_currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
				AnnounceCurrentItem();
			} else if (_currentCategory == ScanCategory.Resources) {
				// Always rescan for fresh data
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();
				ScanResourcesWithSubcategories();
				_unrevealedGladeTiles = null;

				// Get groups from current subcategory
				if (!_cachedResourcesBySubcategory.TryGetValue(_currentSubcategoryIndex, out var subcategoryGroups) || subcategoryGroups.Count == 0) {
					AnnounceEmpty();
					return;
				}

				_cachedGroups = subcategoryGroups;
				_currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
				AnnounceCurrentItem();
			} else {
				// For Glades, use standard scanning
				ScanCurrentCategory();

				if (_cachedGroups == null || _cachedGroups.Count == 0) {
					AnnounceEmpty();
					return;
				}

				_currentGroupIndex = NavigationUtils.WrapIndex(_currentGroupIndex, direction, _cachedGroups.Count);
				AnnounceCurrentItem();
			}

			AutoMoveCursorSilent();
		}

		/// <summary>
		/// Change item within group (Alt+PageUp/Down). No rescan.
		/// </summary>
		public void ChangeItem(int direction) {
			if (_cachedGroups == null || _cachedGroups.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) {
				AnnounceEmpty();
				return;
			}

			_currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, currentGroup.Items.Count);
			AnnounceCurrentItem();
			AutoMoveCursorSilent();
		}

		/// <summary>
		/// Announce distance/direction from cursor to current item (End key).
		/// Read-only - no state changes.
		/// </summary>
		public void AnnounceDistance() {
			AnnounceDistanceFrom(_mapNavigator.CursorX, _mapNavigator.CursorY, null);
		}

		/// <summary>
		/// Announce distance/direction from a specific position to current item.
		/// If suffix is provided (e.g. "of bookmark"), appends it to the announcement.
		/// </summary>
		public void AnnounceDistanceFrom(int fromX, int fromY, string suffix) {
			if (_cachedGroups == null || _cachedGroups.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var item = currentGroup.Items[_currentItemIndex];

			int dx = item.Position.x - fromX;
			int dy = item.Position.y - fromY;
			int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

			string coords = _mapNavigator.GetRelativeCoordinates(item.Position.x, item.Position.y);
			string coordsSuffix = coords != null ? $", {coords}" : "";

			if (distance == 0) {
				Speech.Say(suffix != null ? $"at {suffix}{coordsSuffix}" : $"here{coordsSuffix}");
			} else {
				string direction = GetDirection(dx, dy);
				Speech.Say(suffix != null ? $"{distance} {direction} {suffix}{coordsSuffix}" : $"{distance} tiles {direction}{coordsSuffix}");
			}
		}

		/// <summary>
		/// Read detailed tile info for the current scanner item.
		/// No rescan, no index changes.
		/// </summary>
		public void ReadCurrentItemInfo() {
			if (_cachedGroups == null || _cachedGroups.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var item = currentGroup.Items[_currentItemIndex];
			TileInfoReader.ReadCurrentTile(item.Position.x, item.Position.y);
		}

		/// <summary>
		/// Move cursor to current item (End key).
		/// No rescan, no index changes.
		/// </summary>
		public void MoveCursorToItem() {
			if (_cachedGroups == null || _cachedGroups.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var item = currentGroup.Items[_currentItemIndex];
			_mapNavigator.SetCursorPosition(item.Position.x, item.Position.y);

			// Announce where we moved
			string announcement = $"moved to {currentGroup.TypeName}";
			string coords = _mapNavigator.GetCoordinateSuffix();
			if (coords != null)
				announcement = $"{announcement}, {coords}";
			Speech.Say(announcement);
		}

		// ========================================
		// SCANNING LOGIC
		// ========================================

		private void ScanCurrentCategory() {
			EnsureReflectionCache();

			// Build unrevealed glade tiles map for Resources/Buildings scans (O(1) lookup)
			if (_currentCategory == ScanCategory.Resources || _currentCategory == ScanCategory.Buildings) {
				BuildUnrevealedGladeTilesMap();
			}

			switch (_currentCategory) {
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
			if (_cachedGroups != null && _cachedGroups.Count > 0) {
				_cachedGroups.Sort(CompareGroupsByDistance);
			}
		}

		private void BuildUnrevealedGladeTilesMap() {
			_unrevealedGladeTiles = new HashSet<Vector2Int>();

			try {
				var allGlades = GameReflection.GetAllGlades();
				if (allGlades == null) return;

				var gladesList = allGlades as IEnumerable;
				if (gladesList == null) return;

				foreach (var glade in gladesList) {
					if (glade == null) continue;

					if (MapReflection.GetGladeWasDiscovered(glade)) continue;

					var fields = MapReflection.GetGladeFields(glade);
					if (fields != null) {
						foreach (var field in fields) {
							_unrevealedGladeTiles.Add((Vector2Int)field);
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] BuildUnrevealedGladeTilesMap failed: {ex.Message}");
			}
		}

		private List<ItemGroup> ScanGlades() {
			var groups = new Dictionary<string, ItemGroup>();
			GetScanOrigin(out int cursorX, out int cursorY);
			bool hasGladeInfo = GameReflection.HasGladeInfo();
			bool hasDangerousGladeInfo = GameReflection.HasDangerousGladeInfo();

			try {
				var allGlades = GameReflection.GetAllGlades();
				if (allGlades == null) return new List<ItemGroup>();

				// allGlades is IEnumerable<GladeState>
				var gladesList = allGlades as IEnumerable;
				if (gladesList == null) return new List<ItemGroup>();

				// Collect unrevealed glades for seal candidate check
				var unrevealedGlades = new List<(object glade, Vector2Int firstField)>();

				foreach (var glade in gladesList) {
					if (glade == null) continue;

					// Check if glade is unrevealed (only show unrevealed glades)
					if (MapReflection.GetGladeWasDiscovered(glade)) continue;

					// Get danger level for grouping
					string dangerLevel = GetGladeDangerLevel(glade);

					// Build group name based on what info is available
					string groupName;
					if (!hasDangerousGladeInfo) {
						// Cursed Royal Woodlands: ALL glade markers are hidden
						groupName = "Unknown glade";
					} else if (hasGladeInfo) {
						// Has glade info perk - show type and contents
						string contents = GameReflection.GetGladeContentsSummary(glade);
						groupName = $"{dangerLevel} glade: {contents}";
					} else {
						// Normal biome without glade info perk - show type only
						groupName = $"{dangerLevel} glade";
					}

					// Get position (first field in glade)
					Vector2Int position = MapReflection.GetGladeFirstField(glade);
					if (position.x < 0 || position.y < 0) continue;

					unrevealedGlades.Add((glade, position));

					int distance = CalculateDistance(position, cursorX, cursorY);

					if (!groups.TryGetValue(groupName, out var group)) {
						group = new ItemGroup(groupName);
						groups[groupName] = group;
					}

					group.Items.Add(new ScannedItem(position, distance));
				}

				// Add location marker groups (grass/spring markers)
				ScanLocationMarkers(groups, cursorX, cursorY);

				// Add seal candidate glades (triangulated from discovered guiding stones)
				ScanSealCandidateGlades(unrevealedGlades, groups, cursorX, cursorY);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanGlades failed: {ex.Message}");
			}

			return FinalizeGroups(groups);
		}

		/// <summary>
		/// Find unrevealed glades that are seal candidates based on discovered guiding stone bearings.
		/// Each stone gives a ray toward the seal. A glade is a candidate if every ray passes
		/// within (gladeRadius + tolerance) of the glade center. With more stones discovered,
		/// fewer candidates survive, mirroring the sighted triangulation experience.
		/// </summary>
		private void ScanSealCandidateGlades(
			List<(object glade, Vector2Int firstField)> unrevealedGlades,
			Dictionary<string, ItemGroup> groups,
			int cursorX, int cursorY) {

			if (!GameReflection.IsSealedBiome()) return;

			// Get seal target for bearing calculation
			Vector2Int sealField = GameReflection.GetGuidepostTargetField();
			if (sealField == default) return;

			Vector2Int sealSize = GameReflection.GetSealSize();
			if (sealSize == default) return;

			float sealCenterX = sealField.x + sealSize.x / 2f;
			float sealCenterY = sealField.y + sealSize.y / 2f;

			// Find all discovered guiding stones and compute their bearing rays
			var rays = new List<(float ox, float oy, float dx, float dy)>();

			foreach (var building in GameReflection.GetAllBuildingObjects()) {
				var viewField = building.GetType().GetField("view",
					GameReflection.PublicInstance);
				if (viewField == null) continue;

				var view = viewField.GetValue(building);
				if (view == null || view.GetType().Name != "SealGuidepostView") continue;

				Vector2Int stonePos = MapReflection.GetBuildingPosition(building);
				if (stonePos.x < 0 || stonePos.y < 0) continue;

				// Ray direction from stone toward seal center
				float dx = sealCenterX - stonePos.x;
				float dy = sealCenterY - stonePos.y;
				float len = Mathf.Sqrt(dx * dx + dy * dy);
				if (len < 0.1f) continue;

				rays.Add((stonePos.x, stonePos.y, dx / len, dy / len));
			}

			if (rays.Count == 0) return;

			// Test each unrevealed glade against all bearing rays
			var candidateGroup = new ItemGroup("Seal candidate");

			foreach (var (glade, firstField) in unrevealedGlades) {
				// Compute glade center from all field tiles
				var fields = MapReflection.GetGladeFields(glade);
				if (fields == null || fields.Count == 0) continue;

				float sumX = 0, sumY = 0;
				foreach (var f in fields) {
					var tile = (Vector2Int)f;
					sumX += tile.x;
					sumY += tile.y;
				}
				float gladeCenterX = sumX / fields.Count;
				float gladeCenterY = sumY / fields.Count;

				// Approximate glade radius from tile count (assume roughly circular)
				float gladeRadius = Mathf.Sqrt(fields.Count / Mathf.PI);
				float threshold = gladeRadius + 5f;

				// Candidate only if ALL rays pass within threshold of glade center
				bool isCandidate = true;
				foreach (var ray in rays) {
					float pgX = gladeCenterX - ray.ox;
					float pgY = gladeCenterY - ray.oy;

					// Must be in forward direction (glade ahead of stone, not behind)
					float forward = pgX * ray.dx + pgY * ray.dy;
					if (forward <= 0) { isCandidate = false; break; }

					// Perpendicular distance from glade center to ray
					float perp = Mathf.Abs(pgX * ray.dy - pgY * ray.dx);
					if (perp > threshold) { isCandidate = false; break; }
				}

				if (isCandidate) {
					int distance = CalculateDistance(firstField, cursorX, cursorY);
					candidateGroup.Items.Add(new ScannedItem(firstField, distance));
				}
			}

			if (candidateGroup.Items.Count > 0) {
				candidateGroup.Items.Sort(CompareItemsByDistance);
				groups["Seal candidate"] = candidateGroup;
			}
		}

		/// <summary>
		/// Helper to scan a single type of location marker and add to groups.
		/// </summary>
		private void ScanLocationMarkerType(
			List<Vector2Int> locations,
			string groupName,
			Dictionary<string, ItemGroup> groups,
			int cursorX, int cursorY) {
			if (locations == null || locations.Count == 0) return;

			var group = new ItemGroup(groupName);
			foreach (var pos in locations) {
				// Only include if in unrevealed glade
				if (IsInsideUnrevealedGlade(pos)) {
					group.Items.Add(new ScannedItem(pos, CalculateDistance(pos, cursorX, cursorY)));
				}
			}
			if (group.Items.Count > 0) {
				group.Items.Sort(CompareItemsByDistance);
				groups[groupName] = group;
			}
		}

		/// <summary>
		/// Scan for location markers (grass/spring/relic) and add them as groups.
		/// Only includes markers in unrevealed glades.
		/// </summary>
		private void ScanLocationMarkers(Dictionary<string, ItemGroup> groups, int cursorX, int cursorY) {
			// Scan location marker types (grass/spring/relic)
			ScanLocationMarkerType(GameReflection.GetRevealedGrassLocations(), "Grass marker", groups, cursorX, cursorY);
			ScanLocationMarkerType(GameReflection.GetRevealedSpringsLocations(), "Spring marker", groups, cursorX, cursorY);
			ScanLocationMarkerType(GameReflection.GetRevealedRelicLocations(), "Relic marker", groups, cursorX, cursorY);

			// Highlighted relics (from Short Range Scanner, etc)
			var highlightedRelics = GameReflection.GetHighlightedRelics();
			if (highlightedRelics != null && highlightedRelics.Count > 0) {
				foreach (var kvp in highlightedRelics) {
					var pos = kvp.Key;
					var relicName = kvp.Value;

					// Only include if in unrevealed glade
					if (IsInsideUnrevealedGlade(pos)) {
						// Get display name for the relic
						string displayName = GameReflection.GetRelicDisplayName(relicName);
						string highlightedGroupName = $"Highlighted: {displayName}";
						if (!groups.ContainsKey(highlightedGroupName)) {
							var group = new ItemGroup(highlightedGroupName);
							group.Items.Add(new ScannedItem(pos, CalculateDistance(pos, cursorX, cursorY)));
							groups[highlightedGroupName] = group;
						}
					}
				}
			}

			// Reward chase relics (treasure stag, etc.)
			ScanRewardChaseRelics(groups, cursorX, cursorY);
		}

		/// <summary>
		/// Scan all glades for active reward chase relics and add them as scanner groups.
		/// Each chase gets a group named with the relic display name and remaining time.
		/// </summary>
		private void ScanRewardChaseRelics(Dictionary<string, ItemGroup> groups, int cursorX, int cursorY) {
			try {
				var allGlades = GameReflection.GetAllGlades();
				if (allGlades == null) return;

				var gladesList = allGlades as IEnumerable;
				if (gladesList == null) return;

				float gameTime = GameReflection.GetGameTime();

				foreach (var glade in gladesList) {
					if (glade == null) continue;

					if (!MapReflection.GetGladeHasRewardChase(glade)) continue;

					float chaseEnd = MapReflection.GetGladeRewardChaseEnd(glade);
					float remaining = chaseEnd - gameTime;
					if (remaining <= 0f) continue;

					var relics = MapReflection.GetGladeRelics(glade);
					if (relics == null || relics.Count == 0) continue;

					foreach (var relic in relics) {
						if (relic == null) continue;

						if (!MapReflection.IsRewardChaseRelic(relic)) continue;

						Vector2Int pos = MapReflection.GetRelicPosition(relic);
						if (pos.x < 0 || pos.y < 0) continue;

						string modelName = MapReflection.GetRelicName(relic);

						string displayName = !string.IsNullOrEmpty(modelName)
							? GameReflection.GetRelicDisplayName(modelName)
							: "Chase relic";

						// Format remaining time as m:ss
						int totalSeconds = (int)remaining;
						int minutes = totalSeconds / 60;
						int seconds = totalSeconds % 60;
						string timeStr = $"{minutes}:{seconds:D2}";

						string groupName = $"{displayName}, {timeStr}";

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.ContainsKey(groupName)) {
							var group = new ItemGroup(groupName);
							group.Items.Add(new ScannedItem(pos, distance));
							groups[groupName] = group;
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanRewardChaseRelics failed: {ex.Message}");
			}
		}

		private List<ItemGroup> ScanResources() {
			var groups = new Dictionary<string, ItemGroup>();
			GetScanOrigin(out int cursorX, out int cursorY);

			try {
				// Scan NaturalResources
				var resources = MapReflection.GetNaturalResources(GameReflection.GetResourcesService());
				if (resources != null) {
					foreach (DictionaryEntry entry in resources) {
						var pos = (Vector2Int)entry.Key;
						var resource = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(resource);
						if (string.IsNullOrEmpty(displayName)) continue;

						bool isMarked = GameReflection.IsNaturalResourceMarked(resource);
						string groupName = isMarked ? $"Marked {displayName}" : displayName;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(groupName, out var group)) {
							group = new ItemGroup(groupName);
							groups[groupName] = group;
						}

						group.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Scan Deposits
				var deposits = MapReflection.GetDeposits(GameReflection.GetDepositsService());
				if (deposits != null) {
					foreach (DictionaryEntry entry in deposits) {
						var pos = (Vector2Int)entry.Key;
						var deposit = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(deposit);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(displayName, out var depositGroup)) {
							depositGroup = new ItemGroup(displayName);
							groups[displayName] = depositGroup;
						}

						depositGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Scan Ores (copper veins, etc.)
				var ores = MapReflection.GetOres(GameReflection.GetOreService());
				if (ores != null) {
					foreach (DictionaryEntry entry in ores) {
						var pos = (Vector2Int)entry.Key;
						var ore = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(ore);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(displayName, out var oreGroup)) {
							oreGroup = new ItemGroup(displayName);
							groups[displayName] = oreGroup;
						}

						oreGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Scan Springs (water geysers)
				var springs = MapReflection.GetSprings(GameReflection.GetSpringsService());
				if (springs != null) {
					foreach (DictionaryEntry entry in springs) {
						var pos = (Vector2Int)entry.Key;
						var spring = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(spring);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(displayName, out var springGroup)) {
							springGroup = new ItemGroup(displayName);
							groups[displayName] = springGroup;
						}

						springGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Scan Lakes (fishing spots)
				var lakes = MapReflection.GetLakes(GameReflection.GetLakesService());
				if (lakes != null) {
					foreach (DictionaryEntry entry in lakes) {
						var pos = (Vector2Int)entry.Key;
						var lake = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(lake);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(displayName, out var lakeGroup)) {
							lakeGroup = new ItemGroup(displayName);
							groups[displayName] = lakeGroup;
						}

						lakeGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Scan Fertile Soil (fields with type "Grass")
				int mapWidth = GameReflection.GetMapWidth();
				int mapHeight = GameReflection.GetMapHeight();
				var fertileSoilGroup = new ItemGroup("Fertile Soil");

				for (int x = 0; x < mapWidth; x++) {
					for (int y = 0; y < mapHeight; y++) {
						var pos = new Vector2Int(x, y);
						if (IsInsideUnrevealedGlade(pos)) continue;

						var field = GameReflection.GetField(x, y);
						if (field == null) continue;

						string typeName = MapReflection.GetFieldTypeName(field);
						if (typeName == "Grass") {
							if (GameReflection.GetBuildingAtPosition(x, y) != null) continue;

							int distance = CalculateDistance(pos, cursorX, cursorY);
							fertileSoilGroup.Items.Add(new ScannedItem(pos, distance));
						}
					}
				}

				if (fertileSoilGroup.Items.Count > 0) {
					fertileSoilGroup.Items.Sort(CompareItemsByDistance);
					groups["Fertile Soil"] = fertileSoilGroup;
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanResources failed: {ex.Message}");
			}

			return FinalizeGroups(groups);
		}

		private List<ItemGroup> ScanBuildings() {
			var groups = new Dictionary<string, ItemGroup>();
			GetScanOrigin(out int cursorX, out int cursorY);

			try {
				var buildings = MapReflection.GetBuildings(GameReflection.GetBuildingsService());
				if (buildings != null) {
					foreach (DictionaryEntry entry in buildings) {
						var building = entry.Value;
						if (building == null) continue;

						Vector2Int pos = MapReflection.GetBuildingPosition(building);
						if (pos.x < 0 || pos.y < 0) continue;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetBuildingDisplayName(building);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!groups.TryGetValue(displayName, out var group)) {
							group = new ItemGroup(displayName);
							group.BuildingTypeName = GetBuildingTypeName(building);
							groups[displayName] = group;
						}

						group.Items.Add(new ScannedItem(pos, distance));
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanBuildings failed: {ex.Message}");
			}

			return FinalizeGroups(groups);
		}

		// ========================================
		// ANNOUNCEMENT HELPERS
		// ========================================

		private void AnnounceCurrentItem() {
			if (_cachedGroups == null || _cachedGroups.Count == 0) {
				AnnounceEmpty();
				return;
			}

			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) {
				AnnounceEmpty();
				return;
			}

			int itemNum = _currentItemIndex + 1;
			int itemTotal = currentGroup.Items.Count;
			// Intentional: "X of Y" position context is useful for scanner navigation
			Speech.Say($"{currentGroup.TypeName}, {itemNum} of {itemTotal}");
		}

		private void AutoMoveCursorSilent() {
			if (!Plugin.ScannerAutoMove.Value) return;
			if (_cachedGroups == null || _cachedGroups.Count == 0) return;
			var currentGroup = _cachedGroups[_currentGroupIndex];
			if (currentGroup.Items.Count == 0) return;
			var item = currentGroup.Items[_currentItemIndex];
			_mapNavigator.SetCursorPosition(item.Position.x, item.Position.y);
			_lastAutoMoveX = item.Position.x;
			_lastAutoMoveY = item.Position.y;
		}

		/// <summary>
		/// Update scan origin. When auto-move is on, origin stays fixed unless
		/// the user manually moved the cursor (detected by comparing with last auto-move position).
		/// </summary>
		private void UpdateScanOrigin() {
			if (Plugin.ScannerAutoMove.Value) {
				int cx = _mapNavigator.CursorX;
				int cy = _mapNavigator.CursorY;
				if (!_hasScanOrigin || cx != _lastAutoMoveX || cy != _lastAutoMoveY) {
					_scanOriginX = cx;
					_scanOriginY = cy;
					_hasScanOrigin = true;
				}
			} else {
				_hasScanOrigin = false;
			}
		}

		private void GetScanOrigin(out int x, out int y) {
			if (_hasScanOrigin) {
				x = _scanOriginX;
				y = _scanOriginY;
			} else {
				x = _mapNavigator.CursorX;
				y = _mapNavigator.CursorY;
			}
		}

		private void AnnounceEmpty() {
			string categoryName = _currentCategory switch {
				ScanCategory.Glades => "glades",
				ScanCategory.Resources => "resources",
				ScanCategory.Buildings => "buildings",
				ScanCategory.SearchResults => "search results",
				_ => "items"
			};
			Speech.Say($"No {categoryName}");
		}

		private string GetDirection(int dx, int dy) {
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
		// REFLECTION HELPERS (delegating to MapReflection)
		// ========================================

		private void EnsureReflectionCache() {
			if (_reflectionCached) return;
			_reflectionCached = true;

			// Trigger MapReflection glade caching from first available glade
			MapReflection.EnsureGladeCachedFromList(GameReflection.GetAllGlades());
		}

		private string GetGladeDangerLevel(object glade) {
			string raw = MapReflection.GetGladeDangerLevelRaw(glade);
			if (raw == null) return "Unknown";

			return raw switch {
				"None" => "Small",
				"Dangerous" => "Dangerous",
				"Forbidden" => "Forbidden",
				_ => raw
			};
		}

		private bool IsInsideUnrevealedGlade(Vector2Int pos) {
			if (_unrevealedGladeTiles != null) {
				return _unrevealedGladeTiles.Contains(pos);
			}

			var glade = GameReflection.GetGlade(pos.x, pos.y);
			if (glade == null) return false;
			return !MapReflection.GetGladeWasDiscovered(glade);
		}

		private bool IsInsideUnrevealedGlade(int x, int y) {
			return IsInsideUnrevealedGlade(new Vector2Int(x, y));
		}

		// ========================================
		// BUILDING SUBCATEGORY HELPERS
		// ========================================

		/// <summary>
		/// Get the runtime type name of a building (e.g., "Hearth", "Workshop").
		/// </summary>
		private string GetBuildingTypeName(object building) {
			if (building == null) return null;

			try {
				// Get the base type name without "State" suffix
				var typeName = building.GetType().Name;

				// Remove "State" suffix if present (e.g., "HearthState" -> "Hearth")
				if (typeName.EndsWith("State")) {
					typeName = typeName.Substring(0, typeName.Length - 5);
				}

				return typeName;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the subcategory index for a building based on its type.
		/// </summary>
		private int GetBuildingSubcategoryIndex(object building) {
			var typeName = GetBuildingTypeName(building);
			if (typeName == null) return SubcategoryNames.Length - 1; // Default to last (Roads)

			if (BuildingTypeToSubcategory.TryGetValue(typeName, out int index)) {
				return index;
			}

			// Default to last (Roads) for unknown types
			return SubcategoryNames.Length - 1;
		}

		/// <summary>
		/// Scan all buildings and organize them by subcategory.
		/// </summary>
		private void ScanBuildingsWithSubcategories() {
			_cachedBuildingsBySubcategory = new Dictionary<int, List<ItemGroup>>();
			GetScanOrigin(out int cursorX, out int cursorY);

			// Initialize all subcategories
			for (int i = 0; i < SubcategoryNames.Length; i++) {
				_cachedBuildingsBySubcategory[i] = new List<ItemGroup>();
			}

			try {
				var buildings = MapReflection.GetBuildings(GameReflection.GetBuildingsService());
				if (buildings == null) return;

				// Group buildings by (subcategory, displayName)
				var groupsByKey = new Dictionary<(int subcategory, string displayName), ItemGroup>();

				foreach (DictionaryEntry entry in buildings) {
					var building = entry.Value;
					if (building == null) continue;

					Vector2Int pos = MapReflection.GetBuildingPosition(building);
					if (pos.x < 0 || pos.y < 0) continue;

					if (IsInsideUnrevealedGlade(pos)) continue;

					string displayName = MapReflection.GetBuildingDisplayName(building);
					if (string.IsNullOrEmpty(displayName)) continue;

					string buildingTypeName = GetBuildingTypeName(building);
					int subcategoryIndex = GetBuildingSubcategoryIndex(building);

					int distance = CalculateDistance(pos, cursorX, cursorY);

					var key = (subcategoryIndex, displayName);
					if (!groupsByKey.TryGetValue(key, out var group)) {
						group = new ItemGroup(displayName);
						group.BuildingTypeName = buildingTypeName;
						groupsByKey[key] = group;
					}

					group.Items.Add(new ScannedItem(pos, distance));
				}

				// Distribute groups to subcategories
				foreach (var kvp in groupsByKey) {
					int subcategory = kvp.Key.subcategory;
					var group = kvp.Value;

					// Sort items by distance
					group.Items.Sort(CompareItemsByDistance);

					_cachedBuildingsBySubcategory[subcategory].Add(group);
				}

				// Sort groups within each subcategory by distance
				foreach (var subcategory in _cachedBuildingsBySubcategory.Values) {
					subcategory.Sort(CompareGroupsByDistance);
				}

				// Build "All" subcategory (excludes Decorations and Roads)
				var allGroups = new List<ItemGroup>();
				for (int i = 1; i < SubcategoryNames.Length; i++) {
					if (i == 8 || i == 10) continue;  // Decorations, Roads
					if (_cachedBuildingsBySubcategory.TryGetValue(i, out var groups))
						allGroups.AddRange(groups);
				}
				allGroups.Sort(CompareGroupsByDistance);
				_cachedBuildingsBySubcategory[0] = allGroups;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanBuildingsWithSubcategories failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Scan all resources and organize them by subcategory.
		/// Subcategory 0 (All): All resource types combined
		/// Subcategory 1 (Natural Resources): NaturalResources + Fertile Soil
		/// Subcategory 2 (Extracted Resources): Ores + Springs
		/// Subcategory 3 (Nodes Small): Small deposits + Small lakes
		/// Subcategory 4 (Nodes Large): Large/Gigantic deposits + Large/Gigantic lakes
		/// </summary>
		private void ScanResourcesWithSubcategories() {
			_cachedResourcesBySubcategory = new Dictionary<int, List<ItemGroup>>();
			GetScanOrigin(out int cursorX, out int cursorY);

			for (int i = 0; i < ResourceSubcategoryNames.Length; i++) {
				_cachedResourcesBySubcategory[i] = new List<ItemGroup>();
			}

			// One group dictionary per subcategory
			var naturalGroups = new Dictionary<string, ItemGroup>();
			var extractedGroups = new Dictionary<string, ItemGroup>();
			var nodesSmallGroups = new Dictionary<string, ItemGroup>();
			var nodesLargeGroups = new Dictionary<string, ItemGroup>();

			try {
				// === Subcategory 1: Natural Resources ===

				// NaturalResources service
				var resources = MapReflection.GetNaturalResources(GameReflection.GetResourcesService());
				if (resources != null) {
					foreach (DictionaryEntry entry in resources) {
						var pos = (Vector2Int)entry.Key;
						var resource = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(resource);
						if (string.IsNullOrEmpty(displayName)) continue;

						bool isMarked = GameReflection.IsNaturalResourceMarked(resource);
						string groupName = isMarked ? $"Marked {displayName}" : displayName;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!naturalGroups.TryGetValue(groupName, out var group)) {
							group = new ItemGroup(groupName);
							naturalGroups[groupName] = group;
						}

						group.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Fertile Soil
				int mapWidth = GameReflection.GetMapWidth();
				int mapHeight = GameReflection.GetMapHeight();
				var fertileSoilGroup = new ItemGroup("Fertile Soil");

				for (int x = 0; x < mapWidth; x++) {
					for (int y = 0; y < mapHeight; y++) {
						var pos = new Vector2Int(x, y);
						if (IsInsideUnrevealedGlade(pos)) continue;

						var field = GameReflection.GetField(x, y);
						if (field == null) continue;

						string typeName = MapReflection.GetFieldTypeName(field);
						if (typeName == "Grass") {
							// Skip if there's already a building (e.g., farm field) on this tile
							if (GameReflection.GetBuildingAtPosition(x, y) != null) continue;

							int distance = CalculateDistance(pos, cursorX, cursorY);
							fertileSoilGroup.Items.Add(new ScannedItem(pos, distance));
						}
					}
				}

				if (fertileSoilGroup.Items.Count > 0) {
					fertileSoilGroup.Items.Sort(CompareItemsByDistance);
					naturalGroups["Fertile Soil"] = fertileSoilGroup;
				}

				// === Subcategory 2: Extracted Resources ===

				// Ores service
				var ores = MapReflection.GetOres(GameReflection.GetOreService());
				if (ores != null) {
					foreach (DictionaryEntry entry in ores) {
						var pos = (Vector2Int)entry.Key;
						var ore = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(ore);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!extractedGroups.TryGetValue(displayName, out var oreGroup)) {
							oreGroup = new ItemGroup(displayName);
							extractedGroups[displayName] = oreGroup;
						}

						oreGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Springs service
				var springs = MapReflection.GetSprings(GameReflection.GetSpringsService());
				if (springs != null) {
					foreach (DictionaryEntry entry in springs) {
						var pos = (Vector2Int)entry.Key;
						var spring = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(spring);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						if (!extractedGroups.TryGetValue(displayName, out var springGroup)) {
							springGroup = new ItemGroup(displayName);
							extractedGroups[displayName] = springGroup;
						}

						springGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// === Subcategories 3 & 4: Nodes Small / Nodes Large ===

				// Deposits service
				var deposits = MapReflection.GetDeposits(GameReflection.GetDepositsService());
				if (deposits != null) {
					foreach (DictionaryEntry entry in deposits) {
						var pos = (Vector2Int)entry.Key;
						var deposit = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(deposit);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						// Route to Small or Large based on ResourceSize
						string sizeType = MapReflection.GetResourceSizeType(deposit);
						var targetGroups = sizeType == "Small" ? nodesSmallGroups : nodesLargeGroups;

						if (!targetGroups.TryGetValue(displayName, out var depositGroup)) {
							depositGroup = new ItemGroup(displayName);
							targetGroups[displayName] = depositGroup;
						}

						depositGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Lakes service
				var lakes = MapReflection.GetLakes(GameReflection.GetLakesService());
				if (lakes != null) {
					foreach (DictionaryEntry entry in lakes) {
						var pos = (Vector2Int)entry.Key;
						var lake = entry.Value;

						if (IsInsideUnrevealedGlade(pos)) continue;

						string displayName = MapReflection.GetObjectDisplayName(lake);
						if (string.IsNullOrEmpty(displayName)) continue;

						int distance = CalculateDistance(pos, cursorX, cursorY);

						// Route to Small or Large based on ResourceSize
						string sizeType = MapReflection.GetResourceSizeType(lake);
						var targetGroups = sizeType == "Small" ? nodesSmallGroups : nodesLargeGroups;

						if (!targetGroups.TryGetValue(displayName, out var lakeGroup)) {
							lakeGroup = new ItemGroup(displayName);
							targetGroups[displayName] = lakeGroup;
						}

						lakeGroup.Items.Add(new ScannedItem(pos, distance));
					}
				}

				// Finalize and sort each subcategory
				var naturalList = FinalizeGroups(naturalGroups);
				naturalList.Sort(CompareGroupsByDistance);
				_cachedResourcesBySubcategory[1] = naturalList;

				var extractedList = FinalizeGroups(extractedGroups);
				extractedList.Sort(CompareGroupsByDistance);
				_cachedResourcesBySubcategory[2] = extractedList;

				var nodesSmallList = FinalizeGroups(nodesSmallGroups);
				nodesSmallList.Sort(CompareGroupsByDistance);
				_cachedResourcesBySubcategory[3] = nodesSmallList;

				var nodesLargeList = FinalizeGroups(nodesLargeGroups);
				nodesLargeList.Sort(CompareGroupsByDistance);
				_cachedResourcesBySubcategory[4] = nodesLargeList;

				// Build "All" subcategory
				var allGroups = new List<ItemGroup>();
				for (int i = 1; i < ResourceSubcategoryNames.Length; i++) {
					if (_cachedResourcesBySubcategory.TryGetValue(i, out var groups))
						allGroups.AddRange(groups);
				}
				allGroups.Sort(CompareGroupsByDistance);
				_cachedResourcesBySubcategory[0] = allGroups;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] ScanResourcesWithSubcategories failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Navigate to next/previous subcategory (Shift+PageUp/Down).
		/// Applies to Buildings and Resources categories. Skips empty subcategories.
		/// </summary>
		public void ChangeSubcategory(int direction) {
			UpdateScanOrigin();
			if (_currentCategory == ScanCategory.SearchResults) {
				Speech.Say("No subcategories");
				return;
			}
			if (_currentCategory == ScanCategory.Buildings) {
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();
				ScanBuildingsWithSubcategories();
				_unrevealedGladeTiles = null;

				ChangeSubcategoryInternal(direction, SubcategoryNames, _cachedBuildingsBySubcategory, "No buildings in any subcategory");
			} else if (_currentCategory == ScanCategory.Resources) {
				EnsureReflectionCache();
				BuildUnrevealedGladeTilesMap();
				ScanResourcesWithSubcategories();
				_unrevealedGladeTiles = null;

				ChangeSubcategoryInternal(direction, ResourceSubcategoryNames, _cachedResourcesBySubcategory, "No resources in any subcategory");
			} else {
				Speech.Say("No subcategories");
			}
		}

		private void ChangeSubcategoryInternal(int direction, string[] subcategoryNames, Dictionary<int, List<ItemGroup>> cache, string emptyMessage) {
			int startIndex = _currentSubcategoryIndex;
			int attempts = 0;
			int maxAttempts = subcategoryNames.Length;

			do {
				_currentSubcategoryIndex = NavigationUtils.WrapIndex(_currentSubcategoryIndex, direction, subcategoryNames.Length);
				attempts++;

				if (cache.TryGetValue(_currentSubcategoryIndex, out var groups) && groups.Count > 0) {
					_cachedGroups = groups;
					_currentGroupIndex = 0;
					_currentItemIndex = 0;

					var currentGroup = _cachedGroups[_currentGroupIndex];
					int itemNum = _currentItemIndex + 1;
					int itemTotal = currentGroup.Items.Count;
					// Intentional: "X of Y" position context is useful for scanner navigation
					Speech.Say($"{subcategoryNames[_currentSubcategoryIndex]}, {currentGroup.TypeName}, {itemNum} of {itemTotal}");
					AutoMoveCursorSilent();
					return;
				}
			}
			while (attempts < maxAttempts && _currentSubcategoryIndex != startIndex);

			_currentSubcategoryIndex = startIndex;
			Speech.Say(emptyMessage);
		}

		// ========================================
		// SEARCH
		// ========================================

		/// <summary>
		/// Execute a search across all categories and switch to search results.
		/// </summary>
		public void CommitSearch(string query) {
			if (string.IsNullOrWhiteSpace(query)) {
				Speech.Say("Search cancelled");
				return;
			}

			UpdateScanOrigin();
			EnsureReflectionCache();
			BuildUnrevealedGladeTilesMap();

			// Scan all three categories
			var allGroups = new List<ItemGroup>();
			allGroups.AddRange(ScanGlades());

			ScanResourcesWithSubcategories();
			if (_cachedResourcesBySubcategory != null &&
				_cachedResourcesBySubcategory.TryGetValue(0, out var resGroups))
				allGroups.AddRange(resGroups);

			ScanBuildingsWithSubcategories();
			// Include all subcategories (1..N) to capture Decorations and Roads
			// which are excluded from the "All" bucket (index 0)
			if (_cachedBuildingsBySubcategory != null) {
				for (int i = 1; i < SubcategoryNames.Length; i++) {
					if (_cachedBuildingsBySubcategory.TryGetValue(i, out var subGroups))
						allGroups.AddRange(subGroups);
				}
			}

			_unrevealedGladeTiles = null;

			// Score and filter
			string lowerQuery = query.ToLowerInvariant();
			var scored = new List<(int score, int distance, ItemGroup group)>();
			foreach (var group in allGroups) {
				int score = ScoreMatch(group.TypeName, lowerQuery);
				if (score == 0) continue;
				int dist = group.Items.Count > 0 ? group.Items[0].Distance : int.MaxValue;
				scored.Add((score, dist, group));
			}

			// Sort: highest score first, then nearest distance
			scored.Sort((a, b) => {
				int cmp = b.score.CompareTo(a.score);
				if (cmp != 0) return cmp;
				return a.distance.CompareTo(b.distance);
			});

			_searchResultGroups = new List<ItemGroup>();
			foreach (var (_, _, group) in scored)
				_searchResultGroups.Add(group);

			_categoryBeforeSearch = _currentCategory;
			_currentCategory = ScanCategory.SearchResults;
			_currentGroupIndex = 0;
			_currentItemIndex = 0;
			_cachedGroups = _searchResultGroups;

			if (_searchResultGroups.Count == 0) {
				Speech.Say($"No results for {query}");
			} else {
				var first = _searchResultGroups[0];
				// Intentional: "X of Y" position context is useful for scanner navigation
				Speech.Say($"Search results, {first.TypeName}, 1 of {first.Items.Count}");
				AutoMoveCursorSilent();
			}
		}

		/// <summary>
		/// Clear search results and return to Glades category.
		/// </summary>
		public void ClearSearchResults() {
			_searchResultGroups = null;
			_currentCategory = _categoryBeforeSearch;
			_currentGroupIndex = 0;
			_currentItemIndex = 0;
			_cachedGroups = null;
		}

		/// <summary>
		/// Score how well a name matches a query.
		/// Returns 3 for starts-with, 2 for whole-word match, 1 for substring, 0 for no match.
		/// </summary>
		private static int ScoreMatch(string name, string lowerQuery) {
			if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(lowerQuery)) return 0;

			string lowerName = name.ToLowerInvariant();

			// Tier 3: starts with query
			if (lowerName.Length >= lowerQuery.Length &&
				string.Compare(lowerName, 0, lowerQuery, 0, lowerQuery.Length, StringComparison.Ordinal) == 0)
				return 3;

			// Tier 2: whole-word match (preceded by space)
			for (int i = 1; i < lowerName.Length; i++) {
				if (lowerName[i - 1] == ' ' && lowerName.Length - i >= lowerQuery.Length &&
					string.Compare(lowerName, i, lowerQuery, 0, lowerQuery.Length, StringComparison.Ordinal) == 0)
					return 2;
			}

			// Tier 1: substring match anywhere
			if (lowerName.Contains(lowerQuery))
				return 1;

			return 0;
		}
	}
}
