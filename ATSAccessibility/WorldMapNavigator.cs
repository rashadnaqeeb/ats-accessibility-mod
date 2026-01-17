using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard navigation on the world map hex grid.
    /// Uses Q/A/Z/E/D/C keys for 6-direction movement.
    /// </summary>
    public class WorldMapNavigator
    {
        // Hex directions in cubic coordinates
        // Order: NW (Q), NE (E), E (D), SE (C), SW (Z), W (A)
        private static readonly Vector3Int[] HexDirections = new Vector3Int[]
        {
            new Vector3Int(-1, 0, 1),   // 0: NW (Q)
            new Vector3Int(0, -1, 1),   // 1: NE (E)
            new Vector3Int(1, -1, 0),   // 2: E  (D)
            new Vector3Int(1, 0, -1),   // 3: SE (C)
            new Vector3Int(0, 1, -1),   // 4: SW (Z)
            new Vector3Int(-1, 1, 0)    // 5: W  (A)
        };

        private static readonly string[] DirectionNames = new string[]
        {
            "northwest",
            "northeast",
            "east",
            "southeast",
            "southwest",
            "west"
        };

        // Current cursor position in cubic coordinates
        // (0, 0, 0) is the Smoldering City / capital
        private Vector3Int _cursorPos = Vector3Int.zero;

        /// <summary>
        /// Current cursor position in cubic coordinates.
        /// </summary>
        public Vector3Int CursorPosition => _cursorPos;

        /// <summary>
        /// Move cursor in the specified direction and announce the new tile.
        /// </summary>
        /// <param name="directionIndex">Direction index 0-5 (NW, NE, E, SE, SW, W)</param>
        public void MoveCursor(int directionIndex)
        {
            if (directionIndex < 0 || directionIndex >= 6) return;

            var newPos = _cursorPos + HexDirections[directionIndex];

            // Check if in bounds
            if (!GameReflection.WorldMapInBounds(newPos))
            {
                Speech.Say("Edge of map");
                return;
            }

            _cursorPos = newPos;
            SyncCameraToTile();
            AnnounceTile();
        }

        /// <summary>
        /// Jump cursor to the capital (Smoldering City).
        /// </summary>
        public void JumpToCapital()
        {
            _cursorPos = Vector3Int.zero;
            SyncCameraToTile();
            Speech.Say("Smoldering City");
        }

        /// <summary>
        /// Select the current tile (trigger embark/event).
        /// </summary>
        public void Interact()
        {
            GameReflection.WorldMapTriggerFieldClick(_cursorPos);
        }

        /// <summary>
        /// Read detailed information about the current tile.
        /// </summary>
        public void ReadDetailedInfo()
        {
            var info = GetDetailedTileInfo();
            Speech.Say(info);
        }

        /// <summary>
        /// Reset cursor to capital.
        /// </summary>
        public void Reset()
        {
            _cursorPos = Vector3Int.zero;
            Debug.Log("[ATSAccessibility] WorldMapNavigator reset to capital");
        }

        /// <summary>
        /// Announce the current tile briefly.
        /// </summary>
        private void AnnounceTile()
        {
            var info = GetBriefTileInfo();
            Speech.Say(info);
        }

        /// <summary>
        /// Get brief tile info for movement announcements.
        /// Format: "{biome}, {type}" or "unexplored"
        /// </summary>
        private string GetBriefTileInfo()
        {
            // Check if revealed
            if (!GameReflection.WorldMapIsRevealed(_cursorPos))
            {
                return "Unexplored";
            }

            var biome = GameReflection.WorldMapGetBiomeName(_cursorPos) ?? "Unknown biome";
            var tileType = GetTileType();

            if (string.IsNullOrEmpty(tileType))
            {
                return biome;
            }

            return $"{biome}, {tileType}";
        }

        /// <summary>
        /// Get detailed tile info for I key.
        /// </summary>
        private string GetDetailedTileInfo()
        {
            // Check if revealed
            if (!GameReflection.WorldMapIsRevealed(_cursorPos))
            {
                var distance = GetHexDistance(_cursorPos, Vector3Int.zero);
                var direction = GetDirectionToCapital(_cursorPos);
                return $"Unexplored, Capital: {distance} {direction}";
            }

            var parts = new System.Collections.Generic.List<string>();

            // Biome
            var biome = GameReflection.WorldMapGetBiomeName(_cursorPos) ?? "Unknown biome";
            parts.Add(biome);

            // Type and details
            if (GameReflection.WorldMapIsCapital(_cursorPos))
            {
                parts.Add("Smoldering City (Capital)");
            }
            else if (GameReflection.WorldMapIsCity(_cursorPos))
            {
                var cityName = GameReflection.WorldMapGetCityName(_cursorPos);
                if (!string.IsNullOrEmpty(cityName))
                {
                    parts.Add($"City: {cityName}");
                }
                else
                {
                    parts.Add("City");
                }
            }
            else if (GameReflection.WorldMapHasEvent(_cursorPos))
            {
                var eventName = GameReflection.WorldMapGetEventName(_cursorPos);
                if (!string.IsNullOrEmpty(eventName))
                {
                    parts.Add($"Event: {eventName}");
                }
                else
                {
                    parts.Add("Event");
                }
            }
            else if (GameReflection.WorldMapHasSeal(_cursorPos))
            {
                var sealName = GameReflection.WorldMapGetSealName(_cursorPos);
                if (!string.IsNullOrEmpty(sealName))
                {
                    parts.Add($"Seal: {sealName}");
                }
                else
                {
                    parts.Add("Seal");
                }
            }
            else if (GameReflection.WorldMapHasModifier(_cursorPos))
            {
                var modifierName = GameReflection.WorldMapGetModifierName(_cursorPos);
                if (!string.IsNullOrEmpty(modifierName))
                {
                    parts.Add($"Modifier: {modifierName}");
                }
                else
                {
                    parts.Add("Modifier");
                }
            }

            // Distance from capital (only show if not at capital)
            if (!GameReflection.WorldMapIsCapital(_cursorPos))
            {
                var distance = GetHexDistance(_cursorPos, Vector3Int.zero);
                var direction = GetDirectionToCapital(_cursorPos);
                parts.Add($"Capital: {distance} {direction}");
            }

            // Reachability
            if (GameReflection.WorldMapCanBePicked(_cursorPos))
            {
                parts.Add("Can embark here");
            }
            else if (!GameReflection.WorldMapIsCapital(_cursorPos) && !GameReflection.WorldMapIsCity(_cursorPos))
            {
                parts.Add("Cannot embark");
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Get the tile type string for brief announcements.
        /// </summary>
        private string GetTileType()
        {
            if (GameReflection.WorldMapIsCapital(_cursorPos))
            {
                return "Capital";
            }
            if (GameReflection.WorldMapIsCity(_cursorPos))
            {
                return "City";
            }
            if (GameReflection.WorldMapHasEvent(_cursorPos))
            {
                return "Event";
            }
            if (GameReflection.WorldMapHasSeal(_cursorPos))
            {
                return "Seal";
            }
            if (GameReflection.WorldMapHasModifier(_cursorPos))
            {
                return "Modifier";
            }
            return null;
        }

        /// <summary>
        /// Move the camera to follow the cursor.
        /// </summary>
        private void SyncCameraToTile()
        {
            GameReflection.SetWorldCameraPosition(_cursorPos);
        }

        /// <summary>
        /// Calculate hex distance between two cubic coordinate positions.
        /// For hex grids, distance = max(|dx|, |dy|, |dz|)
        /// </summary>
        private int GetHexDistance(Vector3Int from, Vector3Int to)
        {
            var diff = from - to;
            return Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
        }

        /// <summary>
        /// Get the direction name from current position toward the capital.
        /// Returns the closest cardinal/ordinal direction (e.g., "southwest").
        /// </summary>
        private string GetDirectionToCapital(Vector3Int from)
        {
            // Direction vector pointing toward capital (0,0,0)
            var toCapital = Vector3Int.zero - from;

            // Find which hex direction best matches by dot product
            int bestIndex = 0;
            int bestDot = int.MinValue;

            for (int i = 0; i < HexDirections.Length; i++)
            {
                var dir = HexDirections[i];
                int dot = toCapital.x * dir.x + toCapital.y * dir.y + toCapital.z * dir.z;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }

            return DirectionNames[bestIndex];
        }
    }
}
