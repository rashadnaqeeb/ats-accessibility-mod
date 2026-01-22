using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard navigation on the world map hex grid.
    /// Uses arrow keys for navigation with zigzag pattern for up/down.
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

        // Cached tile info (updated on cursor move)
        private string _cachedBriefInfo;

        // Effects panel for M key
        private WorldMapEffectsPanel _effectsPanel = new WorldMapEffectsPanel();

        // Tile type for tooltip selection
        private enum TileType
        {
            Unexplored,
            Capital,
            City,
            Seal,
            Modifier,
            Event,
            PlayableField,
            OutOfReach
        }

        private TileType _cachedTileType;

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
            if (!WorldMapReflection.WorldMapInBounds(newPos))
            {
                Debug.Log($"[ATSAccessibility] WorldMapNavigator: edge of map at {newPos}");
                Speech.Say("Edge of map");
                return;
            }

            _cursorPos = newPos;
            SyncCameraToTile();
            CacheTileInfo();
            AnnounceTile();
        }

        /// <summary>
        /// Move cursor using arrow key directions (fallback navigation).
        /// Up/Down zigzag based on z coordinate parity for predictable navigation.
        /// </summary>
        public void MoveArrow(int dx, int dy)
        {
            int directionIndex;

            if (dx > 0)  // Right → East
            {
                directionIndex = 2;
            }
            else if (dx < 0)  // Left → West
            {
                directionIndex = 5;
            }
            else
            {
                // Use z coordinate for parity since NE/NW both change z but not necessarily x
                // This ensures zigzag alternates with each up/down press
                // Bitwise AND handles negative numbers correctly
                bool evenZ = (_cursorPos.z & 1) == 0;

                if (dy > 0)  // Up
                {
                    directionIndex = evenZ ? 1 : 0;  // Even z→NE, Odd z→NW
                }
                else  // Down
                {
                    // Match the opposite of what Up does from the tile we came from
                    // Even z: came here via NW from odd z, so go SE to return
                    // Odd z: came here via NE from even z, so go SW to return
                    directionIndex = evenZ ? 3 : 4;  // Even z→SE, Odd z→SW
                }
            }

            MoveCursor(directionIndex);
        }

        /// <summary>
        /// Set cursor to a specific position (used by scanner).
        /// </summary>
        public void SetCursorPosition(Vector3Int pos)
        {
            if (!WorldMapReflection.WorldMapInBounds(pos))
            {
                Debug.Log($"[ATSAccessibility] WorldMapNavigator: SetCursorPosition out of bounds at {pos}");
                return;
            }
            _cursorPos = pos;
            SyncCameraToTile();
            CacheTileInfo();
            AnnounceTile();
        }

        /// <summary>
        /// Select the current tile (trigger embark/event).
        /// </summary>
        public void Interact()
        {
            WorldMapReflection.WorldMapTriggerFieldClick(_cursorPos);
        }

        /// <summary>
        /// Read detailed tooltip information about the current tile (I key).
        /// Content varies based on tile type.
        /// </summary>
        public void ReadTooltip()
        {
            string tooltip = BuildTooltip();
            Speech.Say(tooltip);
        }

        /// <summary>
        /// Read embark status and distance/direction to capital (D key).
        /// </summary>
        public void ReadEmbarkAndDistance()
        {
            var parts = new List<string>();

            // Embark status
            if (WorldMapReflection.WorldMapCanBePicked(_cursorPos))
                parts.Add("Can embark here");
            else
                parts.Add("Cannot embark");

            // Distance to capital (unless at capital)
            if (!WorldMapReflection.WorldMapIsCapital(_cursorPos))
            {
                var distance = GetHexDistance(_cursorPos, Vector3Int.zero);
                var direction = GetDirectionToCapital(_cursorPos);
                parts.Add($"Capital: {distance} {direction}");
            }

            Speech.Say(string.Join(", ", parts));
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
        /// Open the effects panel for the current tile.
        /// Does not work on capital/city tiles.
        /// </summary>
        public void OpenEffectsPanel()
        {
            if (_cachedTileType == TileType.Capital || _cachedTileType == TileType.City)
            {
                Speech.Say("No effects panel for this tile");
                return;
            }
            _effectsPanel.Open(_cursorPos);
        }

        /// <summary>
        /// Process key events for the effects panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessPanelKeyEvent(KeyCode keyCode)
        {
            return _effectsPanel.ProcessKeyEvent(keyCode);
        }

        /// <summary>
        /// Build and cache tile info for current position.
        /// Called once per cursor move to avoid repeated reflection.
        /// </summary>
        private void CacheTileInfo()
        {
            bool isRevealed = WorldMapReflection.WorldMapIsRevealed(_cursorPos);

            // Check for special features visible in fog of war
            bool hasSeal = WorldMapReflection.WorldMapHasSeal(_cursorPos);
            bool hasModifier = !hasSeal && WorldMapReflection.WorldMapHasModifier(_cursorPos);
            bool hasEvent = !hasSeal && !hasModifier && WorldMapReflection.WorldMapHasEvent(_cursorPos);

            // Handle unexplored tiles with special visibility rules
            if (!isRevealed)
            {
                if (hasSeal)
                {
                    // Seals visible through fog - show seal type only
                    _cachedTileType = TileType.Seal;
                    var sealName = WorldMapReflection.WorldMapGetSealName(_cursorPos);
                    _cachedBriefInfo = !string.IsNullOrEmpty(sealName)
                        ? $"Unexplored, Seal: {sealName}"
                        : "Unexplored, Seal";
                }
                else if (hasModifier)
                {
                    // Modifier visible as "?" - don't identify it
                    _cachedTileType = TileType.Unexplored;
                    _cachedBriefInfo = "Unexplored, unknown modifier";
                }
                else if (hasEvent)
                {
                    // Event visible as "?" - don't identify it
                    _cachedTileType = TileType.Unexplored;
                    _cachedBriefInfo = "Unexplored, unknown event";
                }
                else
                {
                    // Plain unexplored
                    _cachedTileType = TileType.Unexplored;
                    _cachedBriefInfo = "Unexplored";
                }
                return;
            }

            // Get biome for revealed tiles
            var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos) ?? "Unknown biome";

            // Check tile type once with short-circuit evaluation
            bool isCapital = WorldMapReflection.WorldMapIsCapital(_cursorPos);
            bool isCity = !isCapital && WorldMapReflection.WorldMapIsCity(_cursorPos);

            // Determine tile type and brief info
            string tileType = null;

            if (isCapital)
            {
                _cachedTileType = TileType.Capital;
                tileType = "Capital";
            }
            else if (isCity)
            {
                _cachedTileType = TileType.City;
                tileType = "City";
            }
            else if (hasSeal)
            {
                _cachedTileType = TileType.Seal;
                var sealName = WorldMapReflection.WorldMapGetSealName(_cursorPos);
                tileType = !string.IsNullOrEmpty(sealName) ? $"Seal: {sealName}" : "Seal";
            }
            else if (hasModifier)
            {
                _cachedTileType = TileType.Modifier;
                var modifierName = WorldMapReflection.WorldMapGetModifierName(_cursorPos);
                tileType = !string.IsNullOrEmpty(modifierName) ? modifierName : "Modifier";
            }
            else if (hasEvent)
            {
                _cachedTileType = TileType.Event;
                var eventName = WorldMapReflection.WorldMapGetEventName(_cursorPos);
                tileType = !string.IsNullOrEmpty(eventName) ? $"Event: {eventName}" : "Event";
            }
            else if (!WorldMapReflection.WorldMapHasAnyPathTo(_cursorPos))
            {
                _cachedTileType = TileType.OutOfReach;
                tileType = "Out of reach";
            }
            else if (WorldMapReflection.WorldMapCanBePicked(_cursorPos))
            {
                _cachedTileType = TileType.PlayableField;
            }
            else
            {
                _cachedTileType = TileType.OutOfReach;
                tileType = "Out of reach";
            }

            // Brief info
            _cachedBriefInfo = string.IsNullOrEmpty(tileType) ? biome : $"{biome}, {tileType}";
        }

        /// <summary>
        /// Build tooltip content based on cached tile type.
        /// Seals show full info even if unexplored (visible through fog).
        /// Unexplored modifiers/events just return "Unexplored".
        /// </summary>
        private string BuildTooltip()
        {
            // Unexplored tiles with no special features (or unexplored modifiers/events)
            if (_cachedTileType == TileType.Unexplored)
                return "Unexplored";

            // Capital/City tiles - show city tooltip
            if (_cachedTileType == TileType.Capital || _cachedTileType == TileType.City)
                return BuildCityTooltip();

            // Seal tiles - show full seal info even if unexplored (seals visible through fog)
            if (_cachedTileType == TileType.Seal)
                return BuildSealTooltip();

            // Modifier tiles - show modifier effect info
            if (_cachedTileType == TileType.Modifier)
                return BuildModifierTooltip();

            // Event tiles - show event info
            if (_cachedTileType == TileType.Event)
                return BuildEventTooltip();

            // Out of reach tiles - show limited info
            if (_cachedTileType == TileType.OutOfReach)
                return BuildOutOfReachTooltip();

            // Playable field tiles - show full field info
            return BuildPlayableFieldTooltip();
        }

        /// <summary>
        /// Build tooltip for capital/city tiles.
        /// </summary>
        private string BuildCityTooltip()
        {
            var parts = new List<string>();

            // City name
            var cityName = WorldMapReflection.WorldMapGetCityName(_cursorPos);
            if (!string.IsNullOrEmpty(cityName))
                parts.Add(cityName);
            else if (WorldMapReflection.WorldMapIsCapital(_cursorPos))
                parts.Add("Smoldering City");
            else
                parts.Add("City");

            // Biome
            var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos);
            if (!string.IsNullOrEmpty(biome))
                parts.Add(biome);

            // Wanted goods (if trade routes enabled)
            var wantedGoods = WorldMapReflection.WorldMapGetWantedGoods(_cursorPos);
            if (wantedGoods != null && wantedGoods.Length > 0)
                parts.Add($"Wants: {string.Join(", ", wantedGoods)}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Build tooltip for seal tiles.
        /// </summary>
        private string BuildSealTooltip()
        {
            var (sealName, difficultyName, minFragments, rewardsPercent, bonusYears, isCompleted) = WorldMapReflection.WorldMapGetSealInfo(_cursorPos);

            var parts = new List<string>();

            // Seal name
            if (!string.IsNullOrEmpty(sealName))
                parts.Add(sealName);
            else
                parts.Add("Seal");

            // Difficulty and requirements
            if (!string.IsNullOrEmpty(difficultyName))
                parts.Add($"{difficultyName} difficulty");

            if (minFragments > 0)
                parts.Add($"Requires {minFragments} seal fragments");

            // Rewards
            if (rewardsPercent > 0)
                parts.Add($"Bonus: {rewardsPercent}% of cycle rewards");

            if (bonusYears > 0)
                parts.Add($"{bonusYears} bonus years per cycle");

            // Completion status
            if (isCompleted)
                parts.Add("Completed");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Build tooltip for modifier tiles.
        /// </summary>
        private string BuildModifierTooltip()
        {
            var (effectName, labelName, description, isPositive) = WorldMapReflection.WorldMapGetModifierInfo(_cursorPos);

            var parts = new List<string>();

            // Effect name
            if (!string.IsNullOrEmpty(effectName))
                parts.Add(effectName);

            // Label (effect type)
            if (!string.IsNullOrEmpty(labelName))
                parts.Add($"({labelName})");

            // Description
            if (!string.IsNullOrEmpty(description))
                parts.Add(description);

            return string.Join(" ", parts);
        }

        /// <summary>
        /// Build tooltip for event tiles.
        /// </summary>
        private string BuildEventTooltip()
        {
            // Check if event is reachable
            if (!WorldMapReflection.WorldMapCanReachEvent(_cursorPos))
            {
                return "Event unreachable";
            }

            var eventName = WorldMapReflection.WorldMapGetEventName(_cursorPos);
            return !string.IsNullOrEmpty(eventName) ? eventName : "Event";
        }

        /// <summary>
        /// Build tooltip for playable field tiles.
        /// Biome is already announced in brief info, so not repeated here.
        /// </summary>
        private string BuildPlayableFieldTooltip()
        {
            var parts = new List<string>();

            // Min difficulty
            var difficulty = WorldMapReflection.WorldMapGetMinDifficultyName(_cursorPos);
            if (!string.IsNullOrEmpty(difficulty))
                parts.Add($"{difficulty} difficulty");

            // Field effects (biome + modifiers)
            var effects = WorldMapReflection.WorldMapGetFieldEffects(_cursorPos);
            if (effects != null && effects.Length > 0)
                parts.Add($"Effects: {string.Join(", ", effects)}");

            // Seal fragments to win
            var fragments = WorldMapReflection.WorldMapGetSealFragmentsForWin(_cursorPos);
            if (fragments > 0)
                parts.Add($"{fragments} seal fragments to win");

            // Meta currencies (rewards)
            var currencies = WorldMapReflection.WorldMapGetMetaCurrencies(_cursorPos);
            if (currencies != null && currencies.Length > 0)
                parts.Add($"Rewards: {string.Join(", ", currencies)}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Build tooltip for out of reach tiles.
        /// </summary>
        private string BuildOutOfReachTooltip()
        {
            var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos);
            if (!string.IsNullOrEmpty(biome))
                return $"{biome}, cannot reach";
            return "Cannot reach";
        }

        /// <summary>
        /// Announce the current tile briefly.
        /// </summary>
        private void AnnounceTile()
        {
            Speech.Say(_cachedBriefInfo);
        }

        /// <summary>
        /// Move the camera to smoothly follow the cursor.
        /// Uses target-following (patched in WorldCameraController) for smooth movement.
        /// </summary>
        private void SyncCameraToTile()
        {
            WorldMapReflection.SetWorldCameraTarget(_cursorPos);
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
        /// Returns the closest direction (north, south, or one of 6 hex directions).
        /// </summary>
        private string GetDirectionToCapital(Vector3Int from)
        {
            // Direction vector pointing toward capital (0,0,0)
            var toCapital = Vector3Int.zero - from;

            int x = toCapital.x;
            int y = toCapital.y;
            int z = toCapital.z;

            int absX = Mathf.Abs(x);
            int absY = Mathf.Abs(y);

            // Check if direction is close to pure north or south (within 2:1 ratio)
            // In hex cubic coords: north = x and y both negative, z positive
            //                      south = x and y both positive, z negative
            if (absX * 2 >= absY && absY * 2 >= absX)
            {
                if (z > 0 && x < 0 && y < 0)
                    return "north";
                if (z < 0 && x > 0 && y > 0)
                    return "south";
            }

            // Fall back to hex direction matching
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
