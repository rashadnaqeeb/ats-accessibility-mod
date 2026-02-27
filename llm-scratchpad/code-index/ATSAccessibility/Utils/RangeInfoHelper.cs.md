# RangeInfoHelper.cs
Helper class for getting building range/reach information.
Used by 'd' key to announce what resources are in range or what buildings are connected.

## class RangeInfoHelper (line 12)

### Methods
- private static bool IsBuildingFinished(object building) (line 16)
  Checks via reflection for IsFinished() method on the building.
- private static (string name, float distance)? FindNearestStorage(Vector2 position) (line 26)
- public static string GetBuildingRangeInfo(object building) (line 54)
  Dispatches to type-specific handlers based on building model type (Camp, GathererHut, FishingHut, Hearth, Farm, Farmfield, ProductionBuilding, or generic). Returns supply chain, hearth connection, or resource count info.
- public static string GetBuildingRangePreview(object buildingModel, int cursorX, int cursorY, int rotation, bool canPlace) (line 94)
  Build mode preview version. Calculates center from cursor+size before dispatch.
- public static string GetResourceRangeInfo(int cursorX, int cursorY) (line 138)
  Inverse of building range: finds placed buildings that can exploit a resource/deposit/lake at cursor. Returns "No building or resource" for unknown types.
- private static string GetNaturalResourceRangeInfo(object resource) (line 169)
  Finds camps in range that can harvest the resource's good. Matches via RefGoodName and camp range.
- private static string GetDepositRangeInfo(object deposit) (line 220)
  Finds gatherer huts in range that can work the deposit. Matches via GoodName.
- private static string GetLakeRangeInfo(object lake) (line 272)
  Finds fishing huts in range. If no good name on lake model, accepts all fishing huts.
- private static string FormatBuildingRangeMatches(List<(string name, float distance)> matches) (line 324)
  Sorts by distance, returns "Name, D tiles. Name2, D2 tiles" format.
- private static string GetFarmRangeInfoForTile(int tileX, int tileY) (line 341)
  Finds placed farms whose work area covers a grass tile at the given position.
- private static string GetGatheringBuildingRangeInfo(object model, Vector2 center2D, object resourceDict, bool isDeposit, string resourceTypeName) (line 402)
  Shared logic for Camp/GathererHut/FishingHut. Groups resources by node display name with count and closest distance.
- private static string GetCampRangeInfo(object campModel, Vector2 center2D) (line 424)
- private static string GetGathererHutRangeInfo(object hutModel, Vector2 center2D) (line 430)
- private static string GetFishingHutRangeInfo(object hutModel, Vector2 center2D) (line 436)
- private static string GetHearthRangeInfo(object hearth) (line 446)
  Counts Houses, Institutions, and Decorations in hearth range (relevant for hub level).
- private static string GetFarmRangeInfo(object farm) (line 509)
  Uses game's totalFields count and counts farmfields to split into "N farm fields, M fertile soil".
- private static int CountFarmfieldsInFarmRange(object farm) (line 537)
  Iterates tiles in work area bounds to count placed farmfields.
- private static string GetFarmRangePreview(object farmModel, int cursorX, int cursorY, Vector2Int buildingSize) (line 586)
  Counts grass and existing farmfields in the work area for build mode.
- private static string GetProductionBuildingSupplyInfo(object building) (line 656)
  Shows nearby suppliers for active recipe inputs + nearest storage distance.
- private static string FindNearbySuppliers(Vector2 buildingPos, List<string> requiredInputs, object excludeBuilding = null, bool useActualOutputs = false) (line 697)
  Caches per-producer data to avoid redundant distance/name calculations. Groups goods by producer name. Returns "Nearby: Name (Good1, Good2): D tiles" or "No nearby suppliers".
- private static string GetBuildingHearthConnection(object building) (line 783)
  Only processes House, Institution, Decoration types; returns "No range info" for others.
- private static string GetPositionHearthConnection(int x, int y) (line 809)
  Checks all hearths for range inclusion. Returns "Connected to Name, D tiles" list or "Not in hearth range".
- private static Dictionary<string, (int count, float closestDistance)> CountResourcesByNodeName(object resourceDict, List<string> goodNames, Vector2 center2D, float maxDistance, bool isDeposit) (line 848)
  Iterates dictionary keyed by goodName. Uses node display name as group key.
- private static string GetProductionBuildingPreview(object buildingModel, int cursorX, int cursorY) (line 910)
  Build mode version: uses model's possible inputs (not active recipe), checks actual supplier outputs.
- private static string GetGoodDisplayName(string goodName) (line 941)
  Looks up display name via Settings.GetGood(string).
