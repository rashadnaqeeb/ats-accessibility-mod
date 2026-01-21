using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Helper class for getting rainpunk engine information.
    /// Used by 'p' key to announce engine status and control.
    /// </summary>
    public static class RainpunkHelper
    {
        // Cached reflection - safe to cache PropertyInfo (survives scene changes)
        private static PropertyInfo _buildingsProperty;
        private static bool _reflectionCached;

        private static void EnsureReflectionCached(object buildingsService)
        {
            if (_reflectionCached || buildingsService == null) return;

            _buildingsProperty = buildingsService.GetType().GetProperty("Buildings", GameReflection.PublicInstance);
            _reflectionCached = true;
        }

        /// <summary>
        /// Get rainpunk info for current cursor position.
        /// - Meta not unlocked: "Rainpunk not unlocked"
        /// - On building with engines: "{N} of {M} engines running, level {X} of {Y}"
        /// - Otherwise: "Nearest: {BuildingName}, {distance} {direction}" or "No running engines"
        /// </summary>
        public static string GetRainpunkInfo(int cursorX, int cursorY)
        {
            // Check if rainpunk meta is unlocked
            if (!BuildingReflection.IsRainpunkEnabledGlobally())
            {
                return "Rainpunk not unlocked";
            }

            // Check if cursor is on a building with engines
            var buildingAtCursor = GameReflection.GetBuildingAtPosition(cursorX, cursorY);
            if (buildingAtCursor != null)
            {
                int engineCount = BuildingReflection.GetEngineCount(buildingAtCursor);
                if (engineCount > 0 && BuildingReflection.IsRainpunkUnlocked(buildingAtCursor))
                {
                    return GetEngineStatusSummary(buildingAtCursor);
                }
            }

            // Find nearest running engine
            return FindNearestRunningEngine(cursorX, cursorY);
        }

        /// <summary>
        /// Stop all engines at the building under cursor.
        /// </summary>
        public static string StopAllEnginesAtBuilding(int cursorX, int cursorY)
        {
            // Check if rainpunk meta is unlocked
            if (!BuildingReflection.IsRainpunkEnabledGlobally())
            {
                return "Rainpunk not unlocked";
            }

            var building = GameReflection.GetBuildingAtPosition(cursorX, cursorY);
            if (building == null)
            {
                return "No building";
            }

            int engineCount = BuildingReflection.GetEngineCount(building);
            if (engineCount == 0)
            {
                return "No engines";
            }

            if (!BuildingReflection.IsRainpunkUnlocked(building))
            {
                return "Rainpunk not installed";
            }

            // Check if any engines are running
            if (!BuildingReflection.HasRunningEngines(building))
            {
                return "Engines already stopped";
            }

            // Stop all engines
            if (BuildingReflection.StopAllEngines(building))
            {
                SoundManager.PlayRainpunkStop();
                return "All engines stopped";
            }

            return "Failed to stop engines";
        }

        /// <summary>
        /// Get engine status summary for a building with rainpunk installed.
        /// </summary>
        private static string GetEngineStatusSummary(object building)
        {
            int engineCount = BuildingReflection.GetEngineCount(building);
            if (engineCount == 0) return "No engines";

            int runningCount = 0;
            int maxLevel = 0;
            int currentMaxRequestedLevel = 0;

            for (int i = 0; i < engineCount; i++)
            {
                int requestedLevel = BuildingReflection.GetEngineRequestedLevel(building, i);
                int engineMaxLevel = BuildingReflection.GetEngineMaxLevel(building, i);

                if (requestedLevel > 0)
                {
                    runningCount++;
                    if (requestedLevel > currentMaxRequestedLevel)
                        currentMaxRequestedLevel = requestedLevel;
                }

                if (engineMaxLevel > maxLevel)
                    maxLevel = engineMaxLevel;
            }

            if (runningCount == 0)
            {
                return $"{engineCount} {(engineCount == 1 ? "engine" : "engines")}, all stopped";
            }

            return $"{runningCount} of {engineCount} {(engineCount == 1 ? "engine" : "engines")} running, level {currentMaxRequestedLevel} of {maxLevel}";
        }

        /// <summary>
        /// Find the nearest building with running engines.
        /// Returns "No running engines" if none found.
        /// </summary>
        private static string FindNearestRunningEngine(int cursorX, int cursorY)
        {
            var buildingsService = GameReflection.GetBuildingsService();
            if (buildingsService == null)
            {
                return "No running engines";
            }

            try
            {
                EnsureReflectionCached(buildingsService);
                var buildingsDict = _buildingsProperty?.GetValue(buildingsService) as IDictionary;

                if (buildingsDict == null)
                {
                    return "No running engines";
                }

                string nearestName = null;
                int nearestDistance = int.MaxValue;
                int nearestDx = 0;
                int nearestDy = 0;

                foreach (DictionaryEntry entry in buildingsDict)
                {
                    var building = entry.Value;
                    if (building == null) continue;

                    // Check if this is a workshop with running engines
                    if (!BuildingReflection.HasRunningEngines(building)) continue;

                    // Get position
                    var field = GameReflection.GetBuildingGridPosition(building);

                    int dx = field.x - cursorX;
                    int dy = field.y - cursorY;
                    int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestDx = dx;
                        nearestDy = dy;
                        nearestName = GetBuildingDisplayName(building);
                    }
                }

                if (nearestName != null)
                {
                    string direction = GetDirection(nearestDx, nearestDy);
                    return $"Nearest: {nearestName}, {nearestDistance} {(nearestDistance == 1 ? "tile" : "tiles")} {direction}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] FindNearestRunningEngine failed: {ex.Message}");
            }

            return "No running engines";
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
