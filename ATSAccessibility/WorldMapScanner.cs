using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Scanner for quick navigation to different types of world map features.
    /// Cycles through types with PageUp/Down, items within type with Alt+PageUp/Down.
    /// </summary>
    public class WorldMapScanner
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public enum ScanType
        {
            Settlement = 0,  // Includes capital and player cities
            Seal = 1,
            RevealedModifier = 2,
            UnknownModifier = 3,
            RevealedEvent = 4,
            UnknownEvent = 5
        }

        public class ScannedItem
        {
            public Vector3Int Position;
            public int Distance;
            public string Name;

            public ScannedItem(Vector3Int position, int distance, string name)
            {
                Position = position;
                Distance = distance;
                Name = name;
            }
        }

        // ========================================
        // STATE
        // ========================================

        private ScanType _currentType = ScanType.Settlement;
        private int _currentItemIndex = 0;
        private List<ScannedItem> _cachedItems = new List<ScannedItem>();
        private readonly WorldMapNavigator _navigator;

        // Hex directions for direction calculation (same as WorldMapNavigator)
        private static readonly Vector3Int[] HexDirections = new Vector3Int[]
        {
            new Vector3Int(-1, 0, 1),   // 0: NW
            new Vector3Int(0, -1, 1),   // 1: NE
            new Vector3Int(1, -1, 0),   // 2: E
            new Vector3Int(1, 0, -1),   // 3: SE
            new Vector3Int(0, 1, -1),   // 4: SW
            new Vector3Int(-1, 1, 0)    // 5: W
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

        private static int CompareByDistance(ScannedItem a, ScannedItem b)
        {
            return a.Distance.CompareTo(b.Distance);
        }

        private const int TYPE_COUNT = 6;

        // ========================================
        // CONSTRUCTOR
        // ========================================

        public WorldMapScanner(WorldMapNavigator navigator)
        {
            _navigator = navigator;
        }

        // ========================================
        // PUBLIC API
        // ========================================

        /// <summary>
        /// Cycle between scan types (PageUp/Down). Rescans and resets item index.
        /// </summary>
        public void ChangeType(int direction)
        {
            int newType = ((int)_currentType + direction + TYPE_COUNT) % TYPE_COUNT;
            _currentType = (ScanType)newType;

            // Rescan and reset index
            ScanCurrentType();
            _currentItemIndex = 0;

            AnnounceTypeChange();
        }

        /// <summary>
        /// Cycle items within current type (Alt+PageUp/Down). No rescan.
        /// </summary>
        public void ChangeItem(int direction)
        {
            if (_cachedItems.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            _currentItemIndex = (_currentItemIndex + direction + _cachedItems.Count) % _cachedItems.Count;
            AnnounceItem();
        }

        /// <summary>
        /// Announce direction and distance to current item (Home key).
        /// </summary>
        public void AnnounceDirection()
        {
            if (_cachedItems.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var item = _cachedItems[_currentItemIndex];
            var cursorPos = _navigator.CursorPosition;

            int distance = GetHexDistance(cursorPos, item.Position);

            if (distance == 0)
            {
                Speech.Say("here");
            }
            else
            {
                string direction = GetDirectionTo(cursorPos, item.Position);
                Speech.Say($"{distance} tiles {direction}");
            }
        }

        /// <summary>
        /// Jump cursor to current item (End key).
        /// </summary>
        public void JumpToItem()
        {
            if (_cachedItems.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var item = _cachedItems[_currentItemIndex];
            _navigator.SetCursorPosition(item.Position);

            Speech.Say($"Moved to {item.Name}");
        }

        // ========================================
        // SCANNING LOGIC
        // ========================================

        private void ScanCurrentType()
        {
            _cachedItems.Clear();
            var cursorPos = _navigator.CursorPosition;

            // Iterate over actual world map positions
            foreach (var pos in WorldMapReflection.GetWorldMapPositions())
            {
                ScannedItem item = CheckPosition(pos, cursorPos);
                if (item != null)
                {
                    _cachedItems.Add(item);
                }
            }

            // Sort by distance
            _cachedItems.Sort(CompareByDistance);
        }

        private ScannedItem CheckPosition(Vector3Int pos, Vector3Int cursorPos)
        {
            bool isRevealed = WorldMapReflection.WorldMapIsRevealed(pos);

            switch (_currentType)
            {
                case ScanType.RevealedModifier:
                    if (isRevealed && WorldMapReflection.WorldMapHasModifier(pos))
                    {
                        string name = WorldMapReflection.WorldMapGetModifierName(pos) ?? "Modifier";
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, name);
                    }
                    break;

                case ScanType.UnknownModifier:
                    if (!isRevealed && WorldMapReflection.WorldMapHasModifier(pos))
                    {
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, "Unknown modifier");
                    }
                    break;

                case ScanType.RevealedEvent:
                    if (isRevealed && WorldMapReflection.WorldMapHasEvent(pos))
                    {
                        string name = WorldMapReflection.WorldMapGetEventName(pos) ?? "Event";
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, name);
                    }
                    break;

                case ScanType.UnknownEvent:
                    if (!isRevealed && WorldMapReflection.WorldMapHasEvent(pos))
                    {
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, "Unknown event");
                    }
                    break;

                case ScanType.Seal:
                    if (WorldMapReflection.WorldMapHasSeal(pos))
                    {
                        string name = WorldMapReflection.WorldMapGetSealName(pos) ?? "Seal";
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, name);
                    }
                    break;

                case ScanType.Settlement:
                    // Player cities (not capital)
                    if (WorldMapReflection.WorldMapIsCity(pos) && !WorldMapReflection.WorldMapIsCapital(pos))
                    {
                        string name = WorldMapReflection.WorldMapGetCityName(pos) ?? "Settlement";
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, name);
                    }
                    // Capital - only include once at origin (0,0,0) to avoid duplicates
                    if (pos == Vector3Int.zero && WorldMapReflection.WorldMapIsCapital(pos))
                    {
                        int dist = GetHexDistance(cursorPos, pos);
                        return new ScannedItem(pos, dist, "Capital");
                    }
                    break;
            }

            return null;
        }

        // ========================================
        // ANNOUNCEMENT HELPERS
        // ========================================

        private void AnnounceTypeChange()
        {
            string typeName = GetTypeName(_currentType);

            if (_cachedItems.Count == 0)
            {
                Speech.Say($"No {typeName}s");
            }
            else
            {
                var item = _cachedItems[_currentItemIndex];
                int itemNum = _currentItemIndex + 1;
                int total = _cachedItems.Count;
                Speech.Say($"{typeName}, {item.Name}, {itemNum} of {total}");
            }
        }

        private void AnnounceItem()
        {
            if (_cachedItems.Count == 0)
            {
                AnnounceEmpty();
                return;
            }

            var item = _cachedItems[_currentItemIndex];
            int itemNum = _currentItemIndex + 1;
            int total = _cachedItems.Count;
            Speech.Say($"{item.Name}, {itemNum} of {total}");
        }

        private void AnnounceEmpty()
        {
            string typeName = GetTypeName(_currentType);
            Speech.Say($"No {typeName}s");
        }

        private string GetTypeName(ScanType type)
        {
            return type switch
            {
                ScanType.RevealedModifier => "revealed modifier",
                ScanType.UnknownModifier => "unknown modifier",
                ScanType.RevealedEvent => "revealed event",
                ScanType.UnknownEvent => "unknown event",
                ScanType.Seal => "seal",
                ScanType.Settlement => "settlement",
                _ => "item"
            };
        }

        // ========================================
        // HEX MATH HELPERS
        // ========================================

        private int GetHexDistance(Vector3Int from, Vector3Int to)
        {
            var diff = from - to;
            return Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));
        }

        private string GetDirectionTo(Vector3Int from, Vector3Int to)
        {
            var toTarget = to - from;

            int x = toTarget.x;
            int y = toTarget.y;
            int z = toTarget.z;

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
                int dot = toTarget.x * dir.x + toTarget.y * dir.y + toTarget.z * dir.z;
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
