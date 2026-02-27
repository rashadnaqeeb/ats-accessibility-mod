# RainpunkHelper.cs
Helper class for getting rainpunk engine information.
Used by 'p' key to announce engine status and control.

## class RainpunkHelper (line 12)

### Fields
- private static PropertyInfo _buildingsProperty (line 14)
- private static bool _reflectionCached (line 15)

### Methods
- private static void EnsureReflectionCached(object buildingsService) (line 17)
- public static string GetRainpunkInfo(int cursorX, int cursorY) (line 30)
  Returns "Rainpunk not unlocked", engine status for building under cursor if it has engines, or nearest running engine summary.
- public static string StopAllEnginesAtBuilding(int cursorX, int cursorY) (line 52)
  Stops all running engines at the building under cursor. Returns status message.
- private static string GetEngineStatusSummary(object building) (line 89)
  Returns "N of M engines running, level L of Max" or "N engines, all stopped".
- private static string FindNearestRunningEngine(int cursorX, int cursorY) (line 122)
  Iterates all buildings via BuildingsService.Buildings dictionary. Returns "Nearest: BuildingName, D tiles direction" or "No running engines".
- private static string GetDirection(int dx, int dy) (line 177)
  Returns "here" for (0,0). Uses 2:1 ratio for diagonal vs cardinal.
- private static string GetBuildingDisplayName(object building) (line 198)
