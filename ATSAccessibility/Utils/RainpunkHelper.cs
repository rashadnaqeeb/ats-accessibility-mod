using ATSAccessibility.Reflection;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Helper class for getting rainpunk engine information.
	/// Used by 'p' key to announce engine status and control.
	/// </summary>
	public static class RainpunkHelper {
		// Cached reflection - safe to cache PropertyInfo (survives scene changes)
		private static PropertyInfo _buildingsProperty;
		private static bool _reflectionCached;

		private static void EnsureReflectionCached(object buildingsService) {
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
		public static string GetRainpunkInfo(int cursorX, int cursorY) {
			// Check if rainpunk meta is unlocked
			if (!BuildingReflection.IsRainpunkEnabledGlobally()) {
				return Strings.Get("util.rainpunk.not_unlocked");
			}

			// Check if cursor is on a building with engines
			var buildingAtCursor = ConstructionReflection.GetBuildingAtPosition(cursorX, cursorY);
			if (buildingAtCursor != null) {
				int engineCount = BuildingReflection.GetEngineCount(buildingAtCursor);
				if (engineCount > 0 && BuildingReflection.IsRainpunkUnlocked(buildingAtCursor)) {
					return GetEngineStatusSummary(buildingAtCursor);
				}
			}

			// Find nearest running engine
			return FindNearestRunningEngine(cursorX, cursorY);
		}

		/// <summary>
		/// Stop all engines at the building under cursor.
		/// </summary>
		public static string StopAllEnginesAtBuilding(int cursorX, int cursorY) {
			// Check if rainpunk meta is unlocked
			if (!BuildingReflection.IsRainpunkEnabledGlobally()) {
				return Strings.Get("util.rainpunk.not_unlocked");
			}

			var building = ConstructionReflection.GetBuildingAtPosition(cursorX, cursorY);
			if (building == null) {
				return Strings.Get("common.no_building");
			}

			int engineCount = BuildingReflection.GetEngineCount(building);
			if (engineCount == 0) {
				return Strings.Get("util.rainpunk.no_engines");
			}

			if (!BuildingReflection.IsRainpunkUnlocked(building)) {
				return Strings.Get("util.rainpunk.not_installed");
			}

			// Check if any engines are running
			if (!BuildingReflection.HasRunningEngines(building)) {
				return Strings.Get("util.rainpunk.already_stopped");
			}

			// Stop all engines
			if (BuildingReflection.StopAllEngines(building)) {
				SoundManager.PlayRainpunkStop();
				return Strings.Get("util.rainpunk.all_stopped");
			}

			return Strings.Get("util.rainpunk.stop_failed");
		}

		/// <summary>
		/// Get engine status summary for a building with rainpunk installed.
		/// </summary>
		private static string GetEngineStatusSummary(object building) {
			int engineCount = BuildingReflection.GetEngineCount(building);
			if (engineCount == 0) return Strings.Get("util.rainpunk.no_engines");

			int runningCount = 0;
			int maxLevel = 0;
			int currentMaxRequestedLevel = 0;

			for (int i = 0; i < engineCount; i++) {
				int requestedLevel = BuildingReflection.GetEngineRequestedLevel(building, i);
				int engineMaxLevel = BuildingReflection.GetEngineMaxLevel(building, i);

				if (requestedLevel > 0) {
					runningCount++;
					if (requestedLevel > currentMaxRequestedLevel)
						currentMaxRequestedLevel = requestedLevel;
				}

				if (engineMaxLevel > maxLevel)
					maxLevel = engineMaxLevel;
			}

			string engineWord = engineCount == 1 ? Strings.Get("util.rainpunk.engine_singular") : Strings.Get("util.rainpunk.engine_plural");

			if (runningCount == 0) {
				return Strings.Get("util.rainpunk.engines_all_stopped", engineCount, engineWord);
			}

			return Strings.Get("util.rainpunk.running_summary", runningCount, engineCount, engineWord, currentMaxRequestedLevel, maxLevel);
		}

		/// <summary>
		/// Find the nearest building with running engines.
		/// Returns "No running engines" if none found.
		/// </summary>
		private static string FindNearestRunningEngine(int cursorX, int cursorY) {
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null) {
				return Strings.Get("util.rainpunk.no_running_engines");
			}

			try {
				EnsureReflectionCached(buildingsService);
				var buildingsDict = _buildingsProperty?.GetValue(buildingsService) as IDictionary;

				if (buildingsDict == null) {
					return Strings.Get("util.rainpunk.no_running_engines");
				}

				string nearestName = null;
				int nearestDistance = int.MaxValue;
				int nearestDx = 0;
				int nearestDy = 0;

				foreach (DictionaryEntry entry in buildingsDict) {
					var building = entry.Value;
					if (building == null) continue;

					// Check if this is a workshop with running engines
					if (!BuildingReflection.HasRunningEngines(building)) continue;

					// Get position
					var field = ConstructionReflection.GetBuildingGridPosition(building);

					int dx = field.x - cursorX;
					int dy = field.y - cursorY;
					int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

					if (distance < nearestDistance) {
						nearestDistance = distance;
						nearestDx = dx;
						nearestDy = dy;
						nearestName = GetBuildingDisplayName(building);
					}
				}

				if (nearestName != null) {
					string direction = NavigationUtils.GetDirection(nearestDx, nearestDy);
					if (string.IsNullOrEmpty(direction)) direction = Strings.Get("common.here_lower");
					string tileWord = nearestDistance == 1 ? Strings.Get("common.tile") : Strings.Get("common.tiles");
					return Strings.Get("util.rainpunk.nearest", nearestName, nearestDistance, tileWord, direction);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] FindNearestRunningEngine failed: {ex.Message}");
			}

			return Strings.Get("util.rainpunk.no_running_engines");
		}

		/// <summary>
		/// Get display name for a building.
		/// </summary>
		private static string GetBuildingDisplayName(object building) {
			if (building == null) return Strings.Get("common.building");

			try {
				var model = ConstructionReflection.GetBuildingModel(building);
				if (model != null) {
					string name = GameReflection.GetDisplayName(model);
					if (!string.IsNullOrEmpty(name))
						return name;
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingDisplayName failed: {ex.Message}");
			}

			return Strings.Get("common.building");
		}
	}
}
