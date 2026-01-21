using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Helper class for getting blight information.
    /// Used by 'b' key to announce blight status, cysts, and corruption.
    /// </summary>
    public static class BlightInfoHelper
    {
        // Cached reflection - safe to cache PropertyInfo (survives scene changes)
        private static PropertyInfo _buildingIdProperty;
        private static PropertyInfo _buildingFieldProperty;
        private static bool _reflectionCached;

        private static void EnsureReflectionCached(object building)
        {
            if (_reflectionCached || building == null) return;

            var buildingType = building.GetType();
            _buildingIdProperty = buildingType.GetProperty("Id", GameReflection.PublicInstance);
            _buildingFieldProperty = buildingType.GetProperty("Field", GameReflection.PublicInstance);
            _reflectionCached = true;
        }

        /// <summary>
        /// Get blight info for current cursor position.
        /// - If no blight in settlement: "No blight"
        /// - If on building with cysts: "3 cysts"
        /// - Otherwise: "Lumber Mill, 3 cysts, 5 tiles northeast. 8 total, 15% corruption"
        /// </summary>
        public static string GetBlightInfo(int cursorX, int cursorY)
        {
            // Check if blight is active in this settlement
            if (!GameReflection.IsBlightActive())
            {
                return "No blight";
            }

            int globalCysts = GameReflection.GetGlobalActiveCysts();
            if (globalCysts == 0)
            {
                return "No cysts";
            }

            // Get blights list once for both checks
            var buildingsBlights = GameReflection.GetBuildingsBlights();
            if (buildingsBlights == null)
            {
                return FormatGlobalStats(globalCysts);
            }

            var blightsList = buildingsBlights as IList;
            if (blightsList == null || blightsList.Count == 0)
            {
                return FormatGlobalStats(globalCysts);
            }

            // Check if cursor is on a building with cysts
            var buildingAtCursor = GameReflection.GetBuildingAtPosition(cursorX, cursorY);
            if (buildingAtCursor != null)
            {
                int cystsOnBuilding = GetCystsOnBuilding(buildingAtCursor, blightsList);
                if (cystsOnBuilding > 0)
                {
                    return $"{cystsOnBuilding} {(cystsOnBuilding == 1 ? "cyst" : "cysts")}";
                }
            }

            // Find nearest building with cysts
            var nearest = FindNearestBlightedBuilding(cursorX, cursorY, blightsList);
            if (nearest.HasValue)
            {
                string globalStats = FormatGlobalStats(globalCysts);
                return $"{nearest.Value.buildingName}, {nearest.Value.cysts} {(nearest.Value.cysts == 1 ? "cyst" : "cysts")}, {nearest.Value.distance} {(nearest.Value.distance == 1 ? "tile" : "tiles")} {nearest.Value.direction}. {globalStats}";
            }

            // Fallback: just show global stats
            return FormatGlobalStats(globalCysts);
        }

        /// <summary>
        /// Get the number of active cysts on a specific building.
        /// Returns 0 if building has no blight.
        /// </summary>
        private static int GetCystsOnBuilding(object building, IList blightsList)
        {
            if (building == null || blightsList == null) return 0;

            try
            {
                EnsureReflectionCached(building);
                if (_buildingIdProperty == null) return 0;

                int buildingId = (int)_buildingIdProperty.GetValue(building);

                foreach (var blight in blightsList)
                {
                    if (blight == null) continue;

                    var owner = GameReflection.GetBlightOwner(blight);
                    if (owner == null) continue;

                    int ownerId = (int)_buildingIdProperty.GetValue(owner);
                    if (ownerId == buildingId)
                    {
                        return GameReflection.GetBlightActiveCysts(blight);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetCystsOnBuilding failed: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Find the nearest building with active cysts.
        /// Returns null if no blighted buildings found.
        /// </summary>
        private static (string buildingName, int cysts, int distance, string direction)? FindNearestBlightedBuilding(
            int cursorX, int cursorY, IList blightsList)
        {
            if (blightsList == null) return null;

            try
            {
                string nearestName = null;
                int nearestCysts = 0;
                int nearestDistance = int.MaxValue;
                int nearestDx = 0;
                int nearestDy = 0;

                foreach (var blight in blightsList)
                {
                    if (blight == null) continue;

                    int cysts = GameReflection.GetBlightActiveCysts(blight);
                    if (cysts == 0) continue;

                    var owner = GameReflection.GetBlightOwner(blight);
                    if (owner == null) continue;

                    // Ensure reflection is cached
                    EnsureReflectionCached(owner);
                    if (_buildingFieldProperty == null) continue;

                    var field = (Vector2Int)_buildingFieldProperty.GetValue(owner);

                    int dx = field.x - cursorX;
                    int dy = field.y - cursorY;
                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestCysts = cysts;
                        nearestDx = dx;
                        nearestDy = dy;

                        // Get building name
                        nearestName = GetBuildingDisplayName(owner);
                    }
                }

                if (nearestName != null)
                {
                    string direction = GetDirection(nearestDx, nearestDy);
                    return (nearestName, nearestCysts, nearestDistance, direction);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] FindNearestBlightedBuilding failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get compass direction from delta coordinates.
        /// </summary>
        private static string GetDirection(int dx, int dy)
        {
            if (dx == 0 && dy == 0) return "here";

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

        /// <summary>
        /// Format global blight stats (total cysts and corruption percentage).
        /// </summary>
        private static string FormatGlobalStats(int globalCysts)
        {
            float corruptionRate = GameReflection.GetPredictedCorruptionPercentage();
            int corruptionPercent = Mathf.RoundToInt(corruptionRate * 100f);

            return $"{globalCysts} total, {corruptionPercent}% corruption";
        }

        /// <summary>
        /// Get display name for a building.
        /// </summary>
        private static string GetBuildingDisplayName(object building)
        {
            if (building == null) return "Building";

            try
            {
                var model = GameReflection.GetBuildingModel(building);
                if (model != null)
                {
                    string name = GameReflection.GetDisplayName(model);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBuildingDisplayName failed: {ex.Message}");
            }

            return "Building";
        }
    }
}
