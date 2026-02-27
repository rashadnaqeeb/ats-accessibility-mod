# BlightInfoHelper.cs
Helper class for getting blight information.
Used by 'b' key to announce blight status, cysts, and corruption.

## class BlightInfoHelper (line 12)

### Fields
- private static PropertyInfo _buildingIdProperty (line 14)
- private static PropertyInfo _buildingFieldProperty (line 15)
- private static bool _reflectionCached (line 16)

### Methods
- private static void EnsureReflectionCached(object building) (line 18)
- public static string GetBlightInfo(int cursorX, int cursorY) (line 33)
  Returns contextual blight info: "No blight", "No cysts", "N cysts" if on a blighted building, or "BuildingName, N cysts, D tiles direction. T total, P% corruption" for nearest blighted building.
- private static int GetCystsOnBuilding(object building, IList blightsList) (line 79)
- private static (string buildingName, int cysts, int distance, string direction)? FindNearestBlightedBuilding(int cursorX, int cursorY, IList blightsList) (line 110)
- private static string GetDirection(int dx, int dy) (line 165)
  Uses 2:1 ratio to decide cardinal vs diagonal. Returns "here" when dx==dy==0.
- private static string FormatGlobalStats(int globalCysts) (line 186)
  Returns "N total, P% corruption".
- private static string GetBuildingDisplayName(object building) (line 196)
