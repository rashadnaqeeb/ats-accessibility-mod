using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Helper class for getting building range/reach information.
    /// Used by 'd' key to announce what resources are in range or what buildings are connected.
    /// </summary>
    public static class RangeInfoHelper
    {
        /// <summary>
        /// Check if a building is finished (not under construction).
        /// </summary>
        private static bool IsBuildingFinished(object building)
        {
            if (building == null) return false;
            var method = building.GetType().GetMethod("IsFinished",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            return method != null && (bool)method.Invoke(building, null);
        }

        /// <summary>
        /// Find the nearest finished storage building to a position.
        /// </summary>
        private static (string name, float distance)? FindNearestStorage(Vector2 position)
        {
            var storages = GameReflection.GetAllStorageBuildings();
            if (storages == null) return null;

            float nearestDist = float.MaxValue;
            string nearestName = null;

            foreach (var storage in storages)
            {
                if (storage == null) continue;
                if (!IsBuildingFinished(storage)) continue;

                var entrance = GameReflection.GetBuildingEntranceCenter(storage);
                if (!entrance.HasValue) continue;

                float dist = Vector2.Distance(position, entrance.Value);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    var model = GameReflection.GetBuildingModel(storage);
                    nearestName = model != null ? GameReflection.GetDisplayName(model) : "Storage";
                }
            }

            return nearestName != null ? (nearestName, nearestDist) : null;
        }

        /// <summary>
        /// Get range info for a placed building.
        /// </summary>
        public static string GetBuildingRangeInfo(object building)
        {
            if (building == null) return "No building";

            var model = GameReflection.GetBuildingModel(building);
            if (model == null) return "Unknown building";

            var center = GameReflection.GetBuildingCenter(building);
            if (!center.HasValue) return "Cannot determine building center";

            Vector2 center2D = new Vector2(center.Value.x, center.Value.z);

            // Check building type and get appropriate info
            if (GameReflection.IsCampModel(model))
            {
                return GetCampRangeInfo(model, center2D);
            }
            else if (GameReflection.IsGathererHutModel(model))
            {
                return GetGathererHutRangeInfo(model, center2D);
            }
            else if (GameReflection.IsFishingHutModel(model))
            {
                return GetFishingHutRangeInfo(model, center2D);
            }
            else if (GameReflection.IsHearthModel(model))
            {
                return GetHearthRangeInfo(building);
            }
            else if (GameReflection.IsProductionBuilding(building))
            {
                // For production buildings (Workshop, Mine, Farm), show supply chain info
                return GetProductionBuildingSupplyInfo(building);
            }
            else
            {
                // For other buildings (Houses, Institutions, Decorations), show hearth connection
                return GetBuildingHearthConnection(building);
            }
        }

        /// <summary>
        /// Get range preview for a building about to be placed.
        /// </summary>
        public static string GetBuildingRangePreview(object buildingModel, int cursorX, int cursorY, int rotation, bool canPlace)
        {
            if (!canPlace) return "Invalid";
            if (buildingModel == null) return "No building selected";

            // Calculate center based on cursor and size
            Vector2Int baseSize = GameReflection.GetBuildingSize(buildingModel);
            bool isRotated = (rotation % 2) == 1;
            Vector2Int effectiveSize = isRotated
                ? new Vector2Int(baseSize.y, baseSize.x)
                : baseSize;

            Vector2 center2D = GameReflection.CalculateBuildingCenter(cursorX, cursorY, effectiveSize);

            // Check building type and get appropriate info
            if (GameReflection.IsCampModel(buildingModel))
            {
                return GetCampRangeInfo(buildingModel, center2D);
            }
            else if (GameReflection.IsGathererHutModel(buildingModel))
            {
                return GetGathererHutRangeInfo(buildingModel, center2D);
            }
            else if (GameReflection.IsFishingHutModel(buildingModel))
            {
                return GetFishingHutRangeInfo(buildingModel, center2D);
            }
            else if (GameReflection.IsHearthModel(buildingModel))
            {
                // For hearth preview, we can't use IsInRange yet - show base range
                float range = GameReflection.GetEffectiveHearthRange(buildingModel);
                return $"Hearth range: {range:F1} tiles";
            }
            else if (GameReflection.IsWorkshopModel(buildingModel))
            {
                // For workshop preview, show nearby suppliers and storage distance
                return GetProductionBuildingPreview(buildingModel, cursorX, cursorY);
            }
            else if (GameReflection.IsHouseModel(buildingModel) ||
                     GameReflection.IsInstitutionModel(buildingModel) ||
                     GameReflection.IsDecorationModel(buildingModel))
            {
                // Houses, Institutions, and Decorations are affected by hearth range
                return GetPositionHearthConnection(cursorX, cursorY);
            }
            else
            {
                // Other production buildings - show nearby suppliers and storage distance
                return GetProductionBuildingPreview(buildingModel, cursorX, cursorY);
            }
        }

        /// <summary>
        /// Get resource info for gathering buildings (Camp, GathererHut, FishingHut).
        /// Groups by resource node display name.
        /// </summary>
        private static string GetGatheringBuildingRangeInfo(
            object model, Vector2 center2D, object resourceDict,
            bool isDeposit, string resourceTypeName)
        {
            float maxDistance = GameReflection.GetGatheringBuildingMaxDistance(model);
            var goodNames = GameReflection.GetGatheringBuildingGoodNames(model);

            if (resourceDict == null || goodNames.Count == 0)
                return $"No {resourceTypeName} available";

            var nodeInfo = CountResourcesByNodeName(resourceDict, goodNames, center2D, maxDistance, isDeposit);

            if (nodeInfo.Count == 0)
                return $"No {resourceTypeName} in range";

            var results = new List<string>();
            foreach (var kvp in nodeInfo)
            {
                results.Add($"{kvp.Key}: {kvp.Value.count}, closest {kvp.Value.closestDistance:F0} tiles");
            }

            return string.Join(". ", results);
        }

        private static string GetCampRangeInfo(object campModel, Vector2 center2D)
        {
            return GetGatheringBuildingRangeInfo(
                campModel, center2D, GameReflection.GetAvailableResources(),
                isDeposit: false, "resources");
        }

        private static string GetGathererHutRangeInfo(object hutModel, Vector2 center2D)
        {
            return GetGatheringBuildingRangeInfo(
                hutModel, center2D, GameReflection.GetAvailableDeposits(),
                isDeposit: true, "deposits");
        }

        private static string GetFishingHutRangeInfo(object hutModel, Vector2 center2D)
        {
            return GetGatheringBuildingRangeInfo(
                hutModel, center2D, GameReflection.GetAvailableLakes(),
                isDeposit: true, "lakes");
        }

        /// <summary>
        /// Get info about buildings in hearth range.
        /// Only counts Houses, Institutions, and Decorations (what matters for hub level).
        /// </summary>
        private static string GetHearthRangeInfo(object hearth)
        {
            int housesCount = 0;
            int institutionsCount = 0;
            int decorationsCount = 0;

            try
            {
                // Count finished houses in range
                var houses = GameReflection.GetAllHouses();
                if (houses != null)
                {
                    foreach (var house in houses)
                    {
                        if (house == null) continue;

                        if (IsBuildingFinished(house) && GameReflection.IsInHearthRange(hearth, house))
                        {
                            housesCount++;
                        }
                    }
                }

                // Count finished institutions in range
                var institutions = GameReflection.GetAllInstitutions();
                if (institutions != null)
                {
                    foreach (var institution in institutions)
                    {
                        if (institution == null) continue;

                        if (IsBuildingFinished(institution) && GameReflection.IsInHearthRange(hearth, institution))
                        {
                            institutionsCount++;
                        }
                    }
                }

                // Count finished decorations in range
                var decorations = GameReflection.GetAllDecorations();
                if (decorations != null)
                {
                    foreach (var decoration in decorations)
                    {
                        if (decoration == null) continue;

                        if (IsBuildingFinished(decoration) && GameReflection.IsInHearthRange(hearth, decoration))
                        {
                            decorationsCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetHearthRangeInfo failed: {ex.Message}");
            }

            if (housesCount == 0 && institutionsCount == 0 && decorationsCount == 0)
                return "No buildings in range";

            var parts = new List<string>();
            if (housesCount > 0)
                parts.Add($"{housesCount} {(housesCount == 1 ? "House" : "Houses")}");
            if (institutionsCount > 0)
                parts.Add($"{institutionsCount} {(institutionsCount == 1 ? "Institution" : "Institutions")}");
            if (decorationsCount > 0)
                parts.Add($"{decorationsCount} {(decorationsCount == 1 ? "Decoration" : "Decorations")}");

            return string.Join(", ", parts);
        }

        /// <summary>
        /// Get supply chain info for a production building.
        /// Shows nearby buildings that can supply required inputs and distance to storage.
        /// </summary>
        private static string GetProductionBuildingSupplyInfo(object building)
        {
            var results = new List<string>();

            try
            {
                // Get building's entrance center for distance calculations
                var entranceCenter = GameReflection.GetBuildingEntranceCenter(building);
                if (!entranceCenter.HasValue)
                {
                    return "Cannot determine building position";
                }

                Vector2 buildingPos = entranceCenter.Value;

                // For placed buildings, get only the allowed inputs from active recipes
                var allowedInputs = GameReflection.GetBuildingRequiredInputs(building);
                if (allowedInputs.Count > 0)
                {
                    // Use actual outputs check (what suppliers can really produce)
                    results.Add(FindNearbySuppliers(buildingPos, allowedInputs, building, useActualOutputs: true));
                }

                // 2. Find nearest storage (warehouse) and its distance
                var nearestStorage = FindNearestStorage(buildingPos);
                if (nearestStorage.HasValue)
                {
                    results.Add($"{nearestStorage.Value.name}: {nearestStorage.Value.distance:F0} tiles");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetProductionBuildingSupplyInfo failed: {ex.Message}");
                return "Cannot determine supply chain";
            }

            return results.Count > 0 ? string.Join(". ", results) : "No supply chain info";
        }

        /// <summary>
        /// Find nearby suppliers for given inputs at a position.
        /// Returns formatted string like "Nearby: Camp (Wood, Reeds): 4 tiles" or "No nearby suppliers".
        /// </summary>
        /// <param name="buildingPos">Position to check from</param>
        /// <param name="requiredInputs">List of good names needed as inputs</param>
        /// <param name="excludeBuilding">Building to exclude (self)</param>
        /// <param name="useActualOutputs">If true, check what supplier can actually output (for placed buildings).
        /// If false, check all possible outputs (for build mode preview).</param>
        private static string FindNearbySuppliers(Vector2 buildingPos, List<string> requiredInputs, object excludeBuilding = null, bool useActualOutputs = false)
        {
            if (requiredInputs.Count == 0)
            {
                return "No inputs required";
            }

            float localStorageRange = GameReflection.GetLocalStorageDistance();

            // Cache per-producer data to avoid redundant calculations when a building produces multiple goods
            // Key: producer object, Value: (name, distance, actual outputs if needed)
            var producerCache = new Dictionary<object, (string name, float distance, List<string> actualOutputs)>();

            // Track goods per producer name (for display grouping)
            var nearbySuppliers = new Dictionary<string, (List<string> goods, float minDistance)>();

            foreach (var inputGood in requiredInputs)
            {
                var producers = GameReflection.GetBuildingsThatProduce(inputGood);
                foreach (var producer in producers)
                {
                    if (excludeBuilding != null && producer == excludeBuilding) continue;

                    // Check cache first to avoid redundant distance/name calculations
                    if (!producerCache.TryGetValue(producer, out var cached))
                    {
                        var producerEntrance = GameReflection.GetBuildingEntranceCenter(producer);
                        if (!producerEntrance.HasValue) continue;

                        float dist = Vector2.Distance(buildingPos, producerEntrance.Value);

                        // Skip if outside local storage range
                        if (dist > localStorageRange) continue;

                        var producerModel = GameReflection.GetBuildingModel(producer);
                        string producerName = producerModel != null
                            ? GameReflection.GetDisplayName(producerModel) ?? "Building"
                            : "Building";

                        // Get actual outputs once if needed
                        List<string> actualOutputs = useActualOutputs
                            ? GameReflection.GetBuildingActualOutputs(producer)
                            : null;

                        cached = (producerName, dist, actualOutputs);
                        producerCache[producer] = cached;
                    }
                    else if (cached.distance > localStorageRange)
                    {
                        // Previously cached but out of range
                        continue;
                    }

                    // Check if this producer can actually output this good
                    if (useActualOutputs && (cached.actualOutputs == null || !cached.actualOutputs.Contains(inputGood)))
                    {
                        continue;
                    }

                    // Get good display name
                    string goodDisplayName = GetGoodDisplayName(inputGood) ?? inputGood;

                    // Track by producer name - merge goods and keep minimum distance
                    if (!nearbySuppliers.ContainsKey(cached.name))
                    {
                        nearbySuppliers[cached.name] = (new List<string>(), cached.distance);
                    }

                    var entry = nearbySuppliers[cached.name];
                    if (!entry.goods.Contains(goodDisplayName))
                    {
                        entry.goods.Add(goodDisplayName);
                    }
                    // Keep the minimum distance for this producer type
                    if (cached.distance < entry.minDistance)
                    {
                        nearbySuppliers[cached.name] = (entry.goods, cached.distance);
                    }
                }
            }

            if (nearbySuppliers.Count > 0)
            {
                var supplierParts = new List<string>();
                foreach (var kvp in nearbySuppliers)
                {
                    string goodsList = string.Join(", ", kvp.Value.goods);
                    supplierParts.Add($"{kvp.Key} ({goodsList}): {kvp.Value.minDistance:F0} tiles");
                }
                return "Nearby: " + string.Join(", ", supplierParts);
            }
            else
            {
                return "No nearby suppliers";
            }
        }

        /// <summary>
        /// Get hearth connection info for a non-hearth building.
        /// Only Houses, Institutions, and Decorations are affected by hearth range.
        /// </summary>
        private static string GetBuildingHearthConnection(object building)
        {
            // Cache type to avoid repeated GetType() calls
            var buildingType = building.GetType();

            // Check if this building type is affected by hearth range
            string typeName = buildingType.Name;
            bool isHearthRelevant = typeName == "House" || typeName == "Institution" || typeName == "Decoration";

            if (!isHearthRelevant)
            {
                return "No range info";
            }

            // Get building's field position
            var fieldProp = buildingType.GetProperty("Field",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fieldProp == null) return "Cannot determine position";

            var fieldValue = fieldProp.GetValue(building);
            if (fieldValue == null) return "Cannot determine position";
            var field = (Vector2Int)fieldValue;
            return GetPositionHearthConnection(field.x, field.y);
        }

        /// <summary>
        /// Get hearth connection info for a position (used for preview).
        /// </summary>
        private static string GetPositionHearthConnection(int x, int y)
        {
            var position = new Vector2Int(x, y);
            var hearths = GameReflection.GetAllHearths();
            if (hearths == null) return "Cannot access hearths";

            var connectedHearths = new List<string>();

            foreach (var hearth in hearths)
            {
                if (hearth == null) continue;

                if (GameReflection.IsInHearthRange(hearth, position))
                {
                    // Get hearth's center and calculate distance
                    var hearthCenter = GameReflection.GetBuildingCenter(hearth);
                    if (hearthCenter.HasValue)
                    {
                        Vector2 hearthCenter2D = new Vector2(hearthCenter.Value.x, hearthCenter.Value.z);
                        float distance = Vector2.Distance(hearthCenter2D, new Vector2(x, y));

                        // Get hearth name
                        var model = GameReflection.GetBuildingModel(hearth);
                        string hearthName = model != null
                            ? GameReflection.GetDisplayName(model) ?? "Hearth"
                            : "Hearth";

                        connectedHearths.Add($"{hearthName}, {distance:F0} tiles");
                    }
                    else
                    {
                        connectedHearths.Add("Hearth");
                    }
                }
            }

            return connectedHearths.Count > 0
                ? "Connected to " + string.Join("; ", connectedHearths)
                : "Not in hearth range";
        }

        /// <summary>
        /// Count resources in range grouped by their node display name.
        /// Returns a dictionary of node name -> (count, closest distance).
        /// </summary>
        private static Dictionary<string, (int count, float closestDistance)> CountResourcesByNodeName(
            object resourceDict, List<string> goodNames, Vector2 center2D, float maxDistance, bool isDeposit)
        {
            var result = new Dictionary<string, (int count, float closestDistance)>();

            if (resourceDict == null) return result;

            try
            {
                var dict = resourceDict as IDictionary;
                if (dict == null) return result;

                // Iterate through all good names the building can harvest
                foreach (var goodName in goodNames)
                {
                    if (!dict.Contains(goodName)) continue;

                    var resourceList = dict[goodName] as IEnumerable;
                    if (resourceList == null) continue;

                    foreach (var resource in resourceList)
                    {
                        var field = GameReflection.GetResourceField(resource);
                        if (!field.HasValue) continue;

                        float distance;
                        if (isDeposit)
                        {
                            // Deposits/lakes can be multi-tile, check closest tile
                            var size = GameReflection.GetResourceSize(resource) ?? Vector2Int.one;
                            distance = GameReflection.CalculateDepositDistance(center2D, field.Value, size);
                        }
                        else
                        {
                            // Natural resources are single-tile
                            distance = GameReflection.CalculateResourceDistance(center2D, field.Value);
                        }

                        if (distance < maxDistance)
                        {
                            // Get the node's display name (e.g., "Lush Tree", "Mushrooms")
                            string nodeName = GameReflection.GetResourceNodeDisplayName(resource);
                            if (string.IsNullOrEmpty(nodeName))
                            {
                                // Fallback to good name if display name not available
                                nodeName = GetGoodDisplayName(goodName) ?? goodName;
                            }

                            if (!result.ContainsKey(nodeName))
                            {
                                result[nodeName] = (0, float.MaxValue);
                            }

                            var current = result[nodeName];
                            result[nodeName] = (
                                current.count + 1,
                                Math.Min(current.closestDistance, distance)
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] CountResourcesByNodeName failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get production building preview for a position (used in build mode).
        /// Shows nearby suppliers for all possible inputs and distance to nearest storage.
        /// </summary>
        private static string GetProductionBuildingPreview(object buildingModel, int cursorX, int cursorY)
        {
            var results = new List<string>();

            try
            {
                Vector2 buildingPos = new Vector2(cursorX, cursorY);

                // 1. Find nearby suppliers using model's possible inputs
                // Use all possible inputs (building not configured yet), but check actual outputs of suppliers
                var possibleInputs = GameReflection.GetModelPossibleInputs(buildingModel);
                if (possibleInputs.Count > 0)
                {
                    results.Add(FindNearbySuppliers(buildingPos, possibleInputs, excludeBuilding: null, useActualOutputs: true));
                }

                // 2. Find nearest storage (warehouse) and its distance
                var nearestStorage = FindNearestStorage(buildingPos);
                if (nearestStorage.HasValue)
                {
                    results.Add($"{nearestStorage.Value.name}: {nearestStorage.Value.distance:F0} tiles");
                }
                else
                {
                    results.Add("No storage found");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetProductionBuildingPreview failed: {ex.Message}");
                return "Cannot determine supply chain";
            }

            return results.Count > 0 ? string.Join(". ", results) : "No supply chain info";
        }

        /// <summary>
        /// Get display name for a good by its internal name.
        /// </summary>
        private static string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return null;

            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return null;

                // Settings.GetGood(string name) returns GoodModel
                var getGoodMethod = settings.GetType().GetMethod("GetGood",
                    new Type[] { typeof(string) });
                if (getGoodMethod == null) return null;

                var goodModel = getGoodMethod.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return null;

                return GameReflection.GetDisplayName(goodModel);
            }
            catch
            {
                return null;
            }
        }
    }
}
