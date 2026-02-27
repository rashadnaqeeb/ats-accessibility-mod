using ATSAccessibility.Reflection;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Helper class for getting building entrance information.
	/// Used by E key in settlement map, build mode, and move mode.
	/// </summary>
	public static class EntranceInfoHelper {
		// Approach tile directions indexed by rotation (0-3)
		// Rotation 0: local (0,0,-1) -> grid (0,-1) = south
		// Rotation 1: local (0,0,-1) -> grid (1, 0) = east
		// Rotation 2: local (0,0,-1) -> grid (0, 1) = north
		// Rotation 3: local (0,0,-1) -> grid (-1,0) = west
		private static readonly Vector2Int[] ApproachOffsets =
		{
			new Vector2Int(0, -1),  // rotation 0 -> south
            new Vector2Int(1, 0),   // rotation 1 -> east
            new Vector2Int(0, 1),   // rotation 2 -> north
            new Vector2Int(-1, 0),  // rotation 3 -> west
        };
		private static readonly string[] ApproachDirections = { "south", "east", "north", "west" };

		/// <summary>
		/// Get entrance info for a placed building at cursor position (settlement map E key).
		/// </summary>
		public static string GetEntranceInfo(int cursorX, int cursorY) {
			var objectOn = GameReflection.GetObjectOn(cursorX, cursorY);
			bool onBuilding = objectOn != null && objectOn.GetType().Name != "Field" && ConstructionReflection.IsBuilding(objectOn);

			if (onBuilding && TryGetApproachTile(objectOn, out int approachX, out int approachY, out int rotation)) {
				return FormatEntrance(cursorX, cursorY, approachX, approachY, rotation);
			}

			// Not on a building with an entrance - check if standing on a
			// neighbor building's approach tile
			if (IsAtAnyApproachTile(cursorX, cursorY)) {
				return "At entrance";
			}

			return onBuilding ? "No entrance" : "No building here";
		}

		/// <summary>
		/// Get entrance preview for a building being placed or moved.
		/// Building must already be positioned at cursor via SetBuildingPosition.
		/// Uses geometric footprint check since building isn't on the grid.
		/// </summary>
		public static string GetEntrancePreview(object building, int cursorX, int cursorY, object buildingModel, int rotation) {
			if (building == null) return "No entrance";

			if (!ConstructionReflection.GetBuildingShouldShowEntrance(building))
				return "No entrance";

			var entranceTile = ConstructionReflection.GetBuildingEntranceTile(building);
			if (!entranceTile.HasValue)
				return "No entrance";

			int buildingRotation = ConstructionReflection.GetBuildingRotation(building);
			if (buildingRotation < 0 || buildingRotation > 3)
				return "No entrance";

			int ex = entranceTile.Value.x;
			int ey = entranceTile.Value.y;

			// Determine if entrance tile is inside the building footprint geometrically.
			// We can't use GetObjectOn because the building isn't placed on the grid.
			bool entranceInsideBuilding = IsInsideFootprint(ex, ey, cursorX, cursorY, buildingModel, rotation);

			int approachX, approachY;
			if (entranceInsideBuilding) {
				Vector2Int offset = ApproachOffsets[buildingRotation];
				approachX = ex + offset.x;
				approachY = ey + offset.y;
			} else {
				approachX = ex;
				approachY = ey;
			}

			return FormatEntrance(cursorX, cursorY, approachX, approachY, buildingRotation);
		}

		/// <summary>
		/// Check if entrance tile is inside the building's footprint using geometry.
		/// </summary>
		private static bool IsInsideFootprint(int tileX, int tileY, int cursorX, int cursorY, object buildingModel, int rotation) {
			if (buildingModel == null) return false;

			Vector2Int baseSize = ConstructionReflection.GetBuildingSize(buildingModel);
			bool isRotated = (rotation % 2) == 1;
			int sizeX = isRotated ? baseSize.y : baseSize.x;
			int sizeY = isRotated ? baseSize.x : baseSize.y;

			return tileX >= cursorX && tileX < cursorX + sizeX &&
				   tileY >= cursorY && tileY < cursorY + sizeY;
		}

		/// <summary>
		/// Try to get the approach tile for a placed building using GetObjectOn.
		/// </summary>
		private static bool TryGetApproachTile(object building, out int approachX, out int approachY, out int rotation) {
			approachX = 0;
			approachY = 0;
			rotation = -1;

			if (!ConstructionReflection.GetBuildingShouldShowEntrance(building))
				return false;

			var entranceTile = ConstructionReflection.GetBuildingEntranceTile(building);
			if (!entranceTile.HasValue)
				return false;

			rotation = ConstructionReflection.GetBuildingRotation(building);
			if (rotation < 0 || rotation > 3)
				return false;

			int ex = entranceTile.Value.x;
			int ey = entranceTile.Value.y;

			// Check if the entrance tile is inside the building.
			// For rotated buildings, the ToRotate pivot can push the entrance
			// Transform outside the footprint - in that case the entrance tile
			// itself is already the approach tile.
			var objectAtEntrance = GameReflection.GetObjectOn(ex, ey);
			bool entranceInsideBuilding = objectAtEntrance != null && ReferenceEquals(objectAtEntrance, building);

			if (entranceInsideBuilding) {
				Vector2Int offset = ApproachOffsets[rotation];
				approachX = ex + offset.x;
				approachY = ey + offset.y;
			} else {
				approachX = ex;
				approachY = ey;
			}
			return true;
		}

		/// <summary>
		/// Check if standing at any neighbor building's approach tile.
		/// The approach tile is always 1 cardinal step from the entrance tile,
		/// so check 4 neighbors.
		/// </summary>
		private static bool IsAtAnyApproachTile(int cursorX, int cursorY) {
			for (int i = 0; i < ApproachOffsets.Length; i++) {
				int nx = cursorX + ApproachOffsets[i].x;
				int ny = cursorY + ApproachOffsets[i].y;

				if (!GameReflection.MapInBounds(nx, ny)) continue;

				var neighbor = GameReflection.GetObjectOn(nx, ny);
				if (neighbor == null || neighbor.GetType().Name == "Field" || !ConstructionReflection.IsBuilding(neighbor))
					continue;

				if (TryGetApproachTile(neighbor, out int ax, out int ay, out int _)) {
					if (ax == cursorX && ay == cursorY)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Format entrance announcement from cursor and approach tile positions.
		/// </summary>
		private static string FormatEntrance(int cursorX, int cursorY, int approachX, int approachY, int rotation) {
			if (cursorX == approachX && cursorY == approachY) {
				return "At entrance";
			}

			int dx = approachX - cursorX;
			int dy = approachY - cursorY;
			int distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
			string direction = GetDirection(dx, dy);
			string tileWord = distance == 1 ? "tile" : "tiles";
			string facing = ApproachDirections[rotation];
			return $"Entrance {distance} {tileWord} {direction}, facing {facing}";
		}

		/// <summary>
		/// Get cardinal/intercardinal direction from delta.
		/// </summary>
		private static string GetDirection(int dx, int dy) {
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
	}
}
