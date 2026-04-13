using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Handles keyboard-based map navigation in settlement view.
	/// Arrow keys move a virtual cursor on the map grid, announcing tile contents.
	/// Map size is dynamically determined from the game's MapService.
	/// </summary>
	public class MapNavigator {
		// Virtual cursor position (initialized to center on first use)
		private int _cursorX = -1;
		private int _cursorY = -1;

		// Coordinate origin (Ancient Hearth position) for hearth-relative display
		private int _originX;
		private int _originY;
		private bool _originSet = false;

		// (Field, glade, and villager reflection moved to MapReflection)

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
		private void EnsureCursorInitialized() {
			if (_cursorX < 0 || _cursorY < 0)
				ResetCursor();
		}

		/// <summary>
		/// Ensure coordinate origin is set to the Ancient Hearth position.
		/// Called lazily in case the hearth hasn't spawned yet at cursor init time.
		/// </summary>
		private void EnsureOriginSet() {
			if (_originSet) return;
			var hearthPos = GameReflection.GetMainHearthPosition();
			if (hearthPos.HasValue) {
				_originX = hearthPos.Value.x;
				_originY = hearthPos.Value.y;
				_originSet = true;
				// Snap cursor to hearth on first discovery
				_cursorX = _originX;
				_cursorY = _originY;
			}
		}

		/// <summary>
		/// Move the cursor by delta and announce the new tile.
		/// </summary>
		public void MoveCursor(int dx, int dy) {
			EnsureCursorInitialized();
			EnsureOriginSet();

			int newX = _cursorX + dx;
			int newY = _cursorY + dy;

			// Bounds check using game's MapService
			if (!GameReflection.MapInBounds(newX, newY)) {
				Speech.Say(Strings.Get("handler.mapnav.edge_of_map"));
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
		public void SetCursorPosition(int x, int y) {
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
		public void SkipToNextChange(int dx, int dy) {
			EnsureCursorInitialized();
			EnsureOriginSet();

			// Get current tile's announcement as baseline (exclude villagers for comparison)
			var currentField = GameReflection.GetField(_cursorX, _cursorY);
			string currentAnnouncement = GetTileAnnouncement(_cursorX, _cursorY, currentField, includeVillagers: false);

			int newX = _cursorX;
			int newY = _cursorY;
			int tilesSkipped = 0;

			// Step in direction until we find different tile or hit edge
			while (true) {
				int nextX = newX + dx;
				int nextY = newY + dy;

				// Check bounds BEFORE moving using game's MapService
				if (!GameReflection.MapInBounds(nextX, nextY)) {
					// Hit edge without finding different tile - stay at current position
					Speech.Say(Strings.Get("handler.mapnav.no_change_till_edge"));
					return;
				}

				newX = nextX;
				newY = nextY;
				tilesSkipped++;

				// Get this tile's announcement (need fresh field for correct terrain/passability)
				var nextField = GameReflection.GetField(newX, newY);
				string nextAnnouncement = GetTileAnnouncement(newX, newY, nextField, includeVillagers: false);

				// Exact string comparison
				if (nextAnnouncement != currentAnnouncement) {
					// Found different tile - move there
					_cursorX = newX;
					_cursorY = newY;

					string tileWord = tilesSkipped == 1 ? Strings.Get("common.tile") : Strings.Get("common.tiles");
					string announcement = GetTileAnnouncement(_cursorX, _cursorY, nextField);
					string prefix = AnnouncementPrefix?.Invoke(_cursorX, _cursorY);
					if (!string.IsNullOrEmpty(prefix))
						announcement = Strings.Get("handler.mapnav.prefix_announcement", prefix, announcement);
					string coords = GetCoordinateSuffix();
					if (coords != null)
						announcement = Strings.Get("handler.mapnav.with_coords", announcement, coords);
					Speech.Say(Strings.Get("handler.mapnav.skip_announcement", tilesSkipped, tileWord, announcement));

					SyncCameraToTile(nextField);
					return;
				}
			}
		}

		/// <summary>
		/// Announce current coordinates relative to Ancient Hearth (K key).
		/// </summary>
		public void AnnounceCurrentPosition() {
			EnsureCursorInitialized();
			EnsureOriginSet();

			if (_originSet) {
				int relX = _cursorX - _originX;
				int relY = _cursorY - _originY;
				Speech.Say(Strings.Get("handler.mapnav.coords", relX, relY));
			} else {
				Speech.Say(Strings.Get("handler.mapnav.coords_unavailable"));
			}
		}

		/// <summary>
		/// Returns hearth-relative coordinate string for an arbitrary position, or null if origin unknown.
		/// </summary>
		public string GetRelativeCoordinates(int x, int y) {
			EnsureOriginSet();
			if (!_originSet) return null;
			return Strings.Get("handler.mapnav.coords", x - _originX, y - _originY);
		}

		/// <summary>
		/// Returns hearth-relative coordinate string if the toggle is on, or null.
		/// </summary>
		public string GetCoordinateSuffix() {
			if (Plugin.AnnounceCoordinates?.Value != true || !_originSet)
				return null;
			int relX = _cursorX - _originX;
			int relY = _cursorY - _originY;
			return Strings.Get("handler.mapnav.coords", relX, relY);
		}

		/// <summary>
		/// Clear cursor position so it will be reinitialized on next use.
		/// Call this when leaving a game session.
		/// </summary>
		public void ClearCursor() {
			_cursorX = -1;
			_cursorY = -1;
			_originSet = false;
		}

		/// <summary>
		/// Reset cursor to the Ancient Hearth position, or map center as fallback.
		/// </summary>
		public void ResetCursor() {
			var hearthPos = GameReflection.GetMainHearthPosition();
			if (hearthPos.HasValue) {
				_cursorX = hearthPos.Value.x;
				_cursorY = hearthPos.Value.y;
				EnsureOriginSet();
			} else {
				// Fallback to center if hearth not found
				_cursorX = GameReflection.GetMapWidth() / 2;
				_cursorY = GameReflection.GetMapHeight() / 2;
			}
		}

		/// <summary>
		/// Announce the current tile contents.
		/// </summary>
		private void AnnounceTile(object field) {
			string announcement = GetTileAnnouncement(_cursorX, _cursorY, field);
			if (!string.IsNullOrEmpty(announcement)) {
				string prefix = AnnouncementPrefix?.Invoke(_cursorX, _cursorY);
				if (!string.IsNullOrEmpty(prefix))
					announcement = Strings.Get("handler.mapnav.prefix_announcement", prefix, announcement);
				string coords = GetCoordinateSuffix();
				if (coords != null)
					announcement = Strings.Get("handler.mapnav.with_coords", announcement, coords);
				Speech.Say(announcement);
			}
		}

		/// <summary>
		/// Build announcement string for a tile.
		/// </summary>
		/// <param name="includeVillagers">If false, skip villager check (for skip comparison performance)</param>
		private string GetTileAnnouncement(int x, int y, object field, bool includeVillagers = true) {
			// Check for unrevealed glade first
			var glade = GameReflection.GetGlade(x, y);
			if (glade != null) {
				bool wasDiscovered = GetGladeWasDiscovered(glade);
				if (!wasDiscovered) {
					// Unrevealed glade - announce based on what info is available
					string dangerLevel = GetGladeDangerLevel(glade);
					bool hasDangerousGladeInfo = MapReflection.HasDangerousGladeInfo();
					bool hasGladeInfo = MapReflection.HasGladeInfo();

					string baseName;
					if (!hasDangerousGladeInfo) {
						// Cursed Royal Woodlands: ALL glade markers are hidden
						baseName = Strings.Get("handler.mapnav.glade_unknown");
					} else if (hasGladeInfo) {
						// Has glade info perk - show type and contents
						baseName = Strings.Get("handler.mapnav.glade_danger", dangerLevel.ToLower());
						string contents = MapReflection.GetGladeContentsSummary(glade);
						if (!string.IsNullOrEmpty(contents))
							baseName = Strings.Get("handler.mapnav.glade_with_contents", baseName, contents);
					} else {
						// Normal biome without glade info perk - show type only
						baseName = Strings.Get("handler.mapnav.glade_danger", dangerLevel.ToLower());
					}

					// Add location marker if present
					string markerType = MapReflection.GetLocationMarkerType(x, y);
					if (!string.IsNullOrEmpty(markerType))
						baseName = Strings.Get("handler.mapnav.glade_with_marker", baseName, markerType);

					// Add highlighted relic info if present (from Short Range Scanner, etc)
					string highlightedRelic = MapReflection.GetHighlightedRelicAt(x, y);
					if (!string.IsNullOrEmpty(highlightedRelic)) {
						string relicDisplayName = GameReflection.GetRelicDisplayName(highlightedRelic);
						baseName = Strings.Get("handler.mapnav.glade_with_relic", baseName, relicDisplayName);
					}

					return baseName;
				}
			}

			// Revealed tile - check contents
			var parts = new List<string>();

			// Check for building/resource
			var objectOn = GameReflection.GetObjectOn(x, y);
			bool hasRealObject = false;

			if (objectOn != null) {
				// GetObjectOn returns Field when there's no actual object - skip those
				string typeName = objectOn.GetType().Name;
				if (typeName != "Field") {
					string objectName = GetObjectName(objectOn);
					if (!string.IsNullOrEmpty(objectName)) {
						// Check building state if it's a building
						if (ConstructionReflection.IsBuilding(objectOn)) {
							if (ConstructionReflection.IsBuildingUnfinished(objectOn)) {
								objectName += Strings.Get("handler.mapnav.under_construction");
							} else if (BuildingReflection.IsRelic(objectOn)) {
								objectName += Strings.Get("handler.mapnav.ruin");
							}
						} else if (typeName == "NaturalResource" && MapReflection.IsNaturalResourceMarked(objectOn)) {
							objectName = Strings.Get("handler.mapnav.marked_prefix", objectName);
						}
						parts.Add(objectName);
						hasRealObject = true;
					}
				}
			}

			if (!hasRealObject) {
				// No object - announce terrain using passed-in field
				if (field != null) {
					string terrain = GetFieldType(field);
					if (!string.IsNullOrEmpty(terrain)) {
						parts.Add(terrain);
					}
				}
			}

			// Check passability using same field (no second GetField call)
			if (field != null) {
				bool isTraversable = GetFieldIsTraversable(field);
				if (!isTraversable) {
					parts.Add(Strings.Get("handler.mapnav.impassable"));
				}
			}

			// Check for villagers (optional - excluded during skip comparison for performance)
			if (includeVillagers) {
				string villagerInfo = GetVillagersOnTile(x, y);
				if (!string.IsNullOrEmpty(villagerInfo)) {
					parts.Add(villagerInfo);
				}
			}

			return string.Join(", ", parts);
		}

		// ========================================
		// FIELD PROPERTY ACCESS
		// ========================================

		private string GetFieldType(object field) {
			string result = MapReflection.GetFieldTypeName(field);
			if (result == null) return Strings.Get("common.unknown_lower");

			// Map game names to more descriptive names
			if (result == "Grass") return Strings.Get("common.fertile_soil");
			if (result == "Sand") return Strings.Get("handler.mapnav.terrain_sand");

			return result;
		}

		private bool GetFieldIsTraversable(object field) {
			return MapReflection.GetFieldIsTraversable(field);
		}

		// ========================================
		// GLADE PROPERTY ACCESS
		// ========================================

		private bool GetGladeWasDiscovered(object glade) {
			// Default to discovered if field not cached yet (don't hide content)
			if (glade == null) return true;
			return MapReflection.GetGladeWasDiscovered(glade);
		}

		private string GetGladeDangerLevel(object glade) {
			string raw = MapReflection.GetGladeDangerLevelRaw(glade);
			if (raw == null) return Strings.Get("common.unknown_lower");

			return raw switch {
				"None" => Strings.Get("handler.mapnav.glade_danger_small"),
				"Dangerous" => Strings.Get("handler.mapnav.glade_danger_dangerous"),
				"Forbidden" => Strings.Get("handler.mapnav.glade_danger_forbidden"),
				_ => raw.ToLower()
			};
		}

		// ========================================
		// OBJECT NAME ACCESS (building/resource)
		// ========================================

		private string GetObjectName(object obj) {
			if (obj == null) return null;

			try {
				var objType = obj.GetType();

				// Try Model.displayName first (specific type like "Lush Tree")
				// Then fall back to Model.label.displayName (generic category like "Woodlands Trees")
				var modelProperty = objType.GetProperty("Model");
				if (modelProperty != null) {
					var model = modelProperty.GetValue(obj);
					if (model != null) {
						var modelType = model.GetType();

						// Try Model.displayName first
						var displayNameField = modelType.GetField("displayName", GameReflection.PublicInstance);
						if (displayNameField != null) {
							var displayName = displayNameField.GetValue(model);
							if (displayName != null) {
								string displayText = displayName.ToString();
								if (!string.IsNullOrEmpty(displayText)) {
									return displayText;
								}
							}
						}

						// Fall back to Model.label.displayName
						var labelField = modelType.GetField("label", GameReflection.PublicInstance);
						if (labelField != null) {
							var label = labelField.GetValue(model);
							if (label != null) {
								var labelDisplayNameField = label.GetType().GetField("displayName", GameReflection.PublicInstance);
								if (labelDisplayNameField != null) {
									var labelDisplayName = labelDisplayNameField.GetValue(label);
									if (labelDisplayName != null) {
										string labelText = labelDisplayName.ToString();
										if (!string.IsNullOrEmpty(labelText)) {
											return labelText;
										}
									}
								}
							}
						}

						var nameProp = modelType.GetProperty("name");
						if (nameProp != null) {
							var name = nameProp.GetValue(model);
							if (name != null) {
								return Speech.CleanResourceName(name.ToString());
							}
						}
					}
				}

				// Try Name property
				var nameProperty = objType.GetProperty("Name");
				if (nameProperty != null) {
					var nameValue = nameProperty.GetValue(obj);
					if (nameValue != null) {
						return nameValue.ToString();
					}
				}

				// Try DisplayName property
				var displayNameProperty = objType.GetProperty("DisplayName");
				if (displayNameProperty != null) {
					var nameValue = displayNameProperty.GetValue(obj);
					if (nameValue != null) {
						return nameValue.ToString();
					}
				}

				// Fallback to type name
				return objType.Name;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetObjectName failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// VILLAGER ACCESS
		// ========================================

		private string GetVillagersOnTile(int x, int y) {
			var allVillagers = GameReflection.GetAllVillagers();
			if (allVillagers == null) return null;

			try {
				var villagersDict = allVillagers as IDictionary;
				if (villagersDict == null) return null;

				var raceCounts = new Dictionary<string, int>();

				foreach (DictionaryEntry entry in villagersDict) {
					var villager = entry.Value;
					if (villager == null) continue;

					Vector3 position = MapReflection.GetVillagerPosition(villager);
					int villagerX = Mathf.FloorToInt(position.x);
					int villagerZ = Mathf.FloorToInt(position.z);

					if (villagerX == x && villagerZ == y) {
						string rawRace = MapReflection.GetVillagerRace(villager);
						if (string.IsNullOrEmpty(rawRace)) rawRace = "_unknown_";
						if (raceCounts.ContainsKey(rawRace)) raceCounts[rawRace]++;
						else raceCounts[rawRace] = 1;
					}
				}

				if (raceCounts.Count == 0) return null;

				var parts = new List<string>();
				foreach (var kvp in raceCounts) {
					string raceId = kvp.Key;
					int count = kvp.Value;
					string raceName = raceId == "_unknown_"
						? Strings.Get("handler.mapnav.villager_default")
						: RaceNameHelper.GetRaceName(raceId, plural: count != 1);
					string key = count == 1
						? "handler.mapnav.villager_group_singular"
						: "handler.mapnav.villager_group_plural";
					parts.Add(Strings.Get(key, count, raceName));
				}

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetVillagersOnTile failed: {ex.Message}");
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
		private void SyncCameraToTile(object field) {
			if (field == null) return;

			try {
				// Get the Field's transform property
				var transformProperty = field.GetType().GetProperty("transform");
				if (transformProperty == null) return;

				var fieldTransform = transformProperty.GetValue(field) as Transform;
				if (fieldTransform != null) {
					GameReflection.SetCameraTarget(fieldTransform);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SyncCameraToTile failed: {ex.Message}"); }
		}

		// ========================================
		// BUILDING ACTIVATION (Enter key)
		// ========================================

		/// <summary>
		/// Activate/open the building panel for the building at current cursor position.
		/// Returns true if a building was activated, false otherwise.
		/// </summary>
		public bool ActivateBuilding() {
			EnsureCursorInitialized();

			// Get object at cursor position
			var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
			if (objectOn == null || objectOn.GetType().Name == "Field") {
				Speech.Say(Strings.Get("common.no_building_here"));
				return false;
			}

			// Check if it's a building
			if (!ConstructionReflection.IsBuilding(objectOn)) {
				Speech.Say(Strings.Get("handler.mapnav.not_a_building"));
				return false;
			}

			// Announce construction progress instead of opening panel for unfinished buildings
			if (ConstructionReflection.IsBuildingUnfinished(objectOn)) {
				AnnounceConstruction(objectOn);
				return true;
			}

			// Try to pick the building (opens its panel)
			if (ConstructionReflection.PickBuilding(objectOn)) {
				return true;
			} else {
				Speech.Say(Strings.Get("handler.mapnav.cannot_open"));
				return false;
			}
		}

		private void AnnounceConstruction(object building) {
			float progress = ConstructionReflection.GetBuildingProgress(building);
			int percent = (int)(progress * 100);

			if (percent > 0) {
				Speech.Say(Strings.Get("handler.mapnav.construction_percent", percent));
				return;
			}

			// 0% progress - announce remaining materials if any
			var materials = ConstructionReflection.GetConstructionMaterials(building);
			if (materials != null && materials.Count > 0) {
				var parts = new List<string>();
				foreach (var (name, delivered, required) in materials) {
					parts.Add(Strings.Get("handler.mapnav.construction_material", name, delivered, required));
				}
				Speech.Say(string.Join(", ", parts));
			} else {
				Speech.Say(Strings.Get("handler.mapnav.construction_zero"));
			}
		}

		// ========================================
		// ENTRANCE ANNOUNCEMENT (E key)
		// ========================================

		public void AnnounceEntrance() {
			EnsureCursorInitialized();
			Speech.Say(EntranceInfoHelper.GetEntranceInfo(_cursorX, _cursorY));
		}

		// ========================================
		// BUILDING ROTATION (R key)
		// ========================================

		// Rotation directions: 0=North, 1=West, 2=South, 3=East
		private static string[] RotationDirections => new[] {
			Strings.Get("common.north"),
			Strings.Get("common.west"),
			Strings.Get("common.south"),
			Strings.Get("common.east"),
		};

		/// <summary>
		/// Rotate the building at current cursor position and announce the new direction.
		/// </summary>
		public void RotateBuilding(bool clockwise = true) {
			EnsureCursorInitialized();

			// Get object at cursor position
			var objectOn = GameReflection.GetObjectOn(_cursorX, _cursorY);
			if (objectOn == null || objectOn.GetType().Name == "Field") {
				Speech.Say(Strings.Get("common.no_building_here"));
				return;
			}

			// Check if it's a building
			if (!ConstructionReflection.IsBuilding(objectOn)) {
				Speech.Say(Strings.Get("handler.mapnav.not_a_building"));
				return;
			}

			// Check if building type supports rotation
			if (!ConstructionReflection.CanRotateBuilding(objectOn)) {
				Speech.Say(Strings.Get("common.cannot_rotate"));
				return;
			}

			// Check if building is movable (required for rotation)
			if (!ConstructionReflection.CanMovePlacedBuilding(objectOn)) {
				Speech.Say(Strings.Get("common.unmovable"));
				return;
			}

			// Check if rotation would be blocked by obstacles
			if (!ConstructionReflection.CanRotatePlacedBuilding(objectOn)) {
				Speech.Say(Strings.Get("handler.mapnav.rotate_blocked"));
				return;
			}

			// Rotate the building in the specified direction
			// Rotation values: 0=N, 1=W, 2=S, 3=E — incrementing is counterclockwise
			int direction = clockwise ? -1 : 1;
			int newRotation = ConstructionReflection.RotatePlacedBuildingDirection(objectOn, direction);
			var directions = RotationDirections;
			if (newRotation >= 0 && newRotation < directions.Length) {
				Speech.Say(directions[newRotation]);
			} else {
				Speech.Say(Strings.Get("handler.mapnav.rotate_failed"));
			}
		}
	}
}
