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
                return GetHearthRangeInfo(building, center2D);
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
        /// Get resource info for a Camp (harvests NaturalResources).
        /// Groups by resource node display name (e.g., "Lush Tree") instead of good type.
        /// </summary>
        private static string GetCampRangeInfo(object campModel, Vector2 center2D)
        {
            float maxDistance = GameReflection.GetGatheringBuildingMaxDistance(campModel);
            var goodNames = GameReflection.GetGatheringBuildingGoodNames(campModel);
            var availableResources = GameReflection.GetAvailableResources();

            if (availableResources == null || goodNames.Count == 0)
                return "No resources available";

            // Count resources by node display name (e.g., "Lush Tree", "Mushrooms")
            var nodeInfo = CountResourcesByNodeName(availableResources, goodNames, center2D, maxDistance, isDeposit: false);

            if (nodeInfo.Count == 0)
                return "No resources in range";

            var results = new List<string>();
            foreach (var kvp in nodeInfo)
            {
                results.Add($"{kvp.Key}: {kvp.Value.count}, closest {kvp.Value.closestDistance:F0} tiles");
            }

            return string.Join(". ", results);
        }

        /// <summary>
        /// Get resource info for a GathererHut (harvests ResourceDeposits).
        /// Groups by deposit display name.
        /// </summary>
        private static string GetGathererHutRangeInfo(object hutModel, Vector2 center2D)
        {
            float maxDistance = GameReflection.GetGatheringBuildingMaxDistance(hutModel);
            var goodNames = GameReflection.GetGatheringBuildingGoodNames(hutModel);
            var availableDeposits = GameReflection.GetAvailableDeposits();

            if (availableDeposits == null || goodNames.Count == 0)
                return "No deposits available";

            var nodeInfo = CountResourcesByNodeName(availableDeposits, goodNames, center2D, maxDistance, isDeposit: true);

            if (nodeInfo.Count == 0)
                return "No deposits in range";

            var results = new List<string>();
            foreach (var kvp in nodeInfo)
            {
                results.Add($"{kvp.Key}: {kvp.Value.count}, closest {kvp.Value.closestDistance:F0} tiles");
            }

            return string.Join(". ", results);
        }

        /// <summary>
        /// Get resource info for a FishingHut (harvests Lakes).
        /// Groups by lake display name.
        /// </summary>
        private static string GetFishingHutRangeInfo(object hutModel, Vector2 center2D)
        {
            float maxDistance = GameReflection.GetGatheringBuildingMaxDistance(hutModel);
            var goodNames = GameReflection.GetGatheringBuildingGoodNames(hutModel);
            var availableLakes = GameReflection.GetAvailableLakes();

            if (availableLakes == null || goodNames.Count == 0)
                return "No lakes available";

            var nodeInfo = CountResourcesByNodeName(availableLakes, goodNames, center2D, maxDistance, isDeposit: true);

            if (nodeInfo.Count == 0)
                return "No lakes in range";

            var results = new List<string>();
            foreach (var kvp in nodeInfo)
            {
                results.Add($"{kvp.Key}: {kvp.Value.count}, closest {kvp.Value.closestDistance:F0} tiles");
            }

            return string.Join(". ", results);
        }

        /// <summary>
        /// Get info about buildings in hearth range.
        /// Only counts Houses, Institutions, and Decorations (what matters for hub level).
        /// </summary>
        private static string GetHearthRangeInfo(object hearth, Vector2 center2D)
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

                        // Check if finished
                        var isFinishedMethod = house.GetType().GetMethod("IsFinished",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(house, null);

                        if (isFinished && GameReflection.IsInHearthRange(hearth, house))
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

                        var isFinishedMethod = institution.GetType().GetMethod("IsFinished",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(institution, null);

                        if (isFinished && GameReflection.IsInHearthRange(hearth, institution))
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

                        var isFinishedMethod = decoration.GetType().GetMethod("IsFinished",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(decoration, null);

                        if (isFinished && GameReflection.IsInHearthRange(hearth, decoration))
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
                var storages = GameReflection.GetAllStorageBuildings();
                float nearestStorageDist = float.MaxValue;
                string nearestStorageName = null;

                if (storages != null)
                {
                    foreach (var storage in storages)
                    {
                        if (storage == null) continue;

                        var isFinishedMethod = storage.GetType().GetMethod("IsFinished",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(storage, null);

                        if (!isFinished) continue;

                        var storageEntrance = GameReflection.GetBuildingEntranceCenter(storage);
                        if (storageEntrance.HasValue)
                        {
                            float dist = Vector2.Distance(buildingPos, storageEntrance.Value);
                            if (dist < nearestStorageDist)
                            {
                                nearestStorageDist = dist;
                                var storageModel = GameReflection.GetBuildingModel(storage);
                                nearestStorageName = storageModel != null
                                    ? GameReflection.GetDisplayName(storageModel)
                                    : "Storage";
                            }
                        }
                    }
                }

                if (nearestStorageName != null)
                {
                    results.Add($"{nearestStorageName}: {nearestStorageDist:F0} tiles");
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
            // Track multiple goods per supplier: producerKey -> (list of goods, distance)
            var nearbySuppliers = new Dictionary<string, (List<string> goods, float distance)>();

            foreach (var inputGood in requiredInputs)
            {
                var producers = GameReflection.GetBuildingsThatProduce(inputGood);
                foreach (var producer in producers)
                {
                    if (excludeBuilding != null && producer == excludeBuilding) continue;

                    var producerEntrance = GameReflection.GetBuildingEntranceCenter(producer);
                    if (!producerEntrance.HasValue) continue;

                    float dist = Vector2.Distance(buildingPos, producerEntrance.Value);

                    // Only include if within local storage range
                    if (dist <= localStorageRange)
                    {
                        // If useActualOutputs, check what the producer can actually output
                        if (useActualOutputs)
                        {
                            var actualOutputs = GameReflection.GetBuildingActualOutputs(producer);
                            if (!actualOutputs.Contains(inputGood))
                            {
                                continue; // This producer can't actually output this good
                            }
                        }

                        var producerModel = GameReflection.GetBuildingModel(producer);
                        string producerName = producerModel != null
                            ? GameReflection.GetDisplayName(producerModel) ?? "Building"
                            : "Building";

                        // Get good display name
                        string goodDisplayName = GetGoodDisplayName(inputGood) ?? inputGood;

                        // Use producer name + distance as key to group same buildings
                        string key = $"{producerName}_{dist:F1}";

                        if (!nearbySuppliers.ContainsKey(key))
                        {
                            nearbySuppliers[key] = (new List<string>(), dist);
                        }

                        var entry = nearbySuppliers[key];
                        if (!entry.goods.Contains(goodDisplayName))
                        {
                            entry.goods.Add(goodDisplayName);
                        }
                        nearbySuppliers[key] = entry;
                    }
                }
            }

            if (nearbySuppliers.Count > 0)
            {
                var supplierParts = new List<string>();
                // Group by producer name (remove the distance suffix for display)
                var groupedByName = new Dictionary<string, (List<string> goods, float distance)>();
                foreach (var kvp in nearbySuppliers)
                {
                    string producerName = kvp.Key.Substring(0, kvp.Key.LastIndexOf('_'));
                    if (!groupedByName.ContainsKey(producerName) || groupedByName[producerName].distance > kvp.Value.distance)
                    {
                        // Merge goods lists if same producer at different distances
                        if (groupedByName.ContainsKey(producerName))
                        {
                            var existing = groupedByName[producerName];
                            foreach (var g in kvp.Value.goods)
                            {
                                if (!existing.goods.Contains(g))
                                    existing.goods.Add(g);
                            }
                            groupedByName[producerName] = (existing.goods, Math.Min(existing.distance, kvp.Value.distance));
                        }
                        else
                        {
                            groupedByName[producerName] = kvp.Value;
                        }
                    }
                    else
                    {
                        // Merge goods from farther instance
                        var existing = groupedByName[producerName];
                        foreach (var g in kvp.Value.goods)
                        {
                            if (!existing.goods.Contains(g))
                                existing.goods.Add(g);
                        }
                        groupedByName[producerName] = existing;
                    }
                }

                foreach (var kvp in groupedByName)
                {
                    string goodsList = string.Join(", ", kvp.Value.goods);
                    supplierParts.Add($"{kvp.Key} ({goodsList}): {kvp.Value.distance:F0} tiles");
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
            // Check if this building type is affected by hearth range
            string typeName = building.GetType().Name;
            bool isHearthRelevant = typeName == "House" || typeName == "Institution" || typeName == "Decoration";

            if (!isHearthRelevant)
            {
                return "No range info";
            }

            // Get building's field position
            var fieldProp = building.GetType().GetProperty("Field",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fieldProp == null) return "Cannot determine position";

            var field = (Vector2Int)fieldProp.GetValue(building);
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
                var storages = GameReflection.GetAllStorageBuildings();
                float nearestStorageDist = float.MaxValue;
                string nearestStorageName = null;

                if (storages != null)
                {
                    foreach (var storage in storages)
                    {
                        if (storage == null) continue;

                        var isFinishedMethod = storage.GetType().GetMethod("IsFinished",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        bool isFinished = isFinishedMethod != null && (bool)isFinishedMethod.Invoke(storage, null);

                        if (!isFinished) continue;

                        var storageEntrance = GameReflection.GetBuildingEntranceCenter(storage);
                        if (storageEntrance.HasValue)
                        {
                            float dist = Vector2.Distance(buildingPos, storageEntrance.Value);
                            if (dist < nearestStorageDist)
                            {
                                nearestStorageDist = dist;
                                var storageModel = GameReflection.GetBuildingModel(storage);
                                nearestStorageName = storageModel != null
                                    ? GameReflection.GetDisplayName(storageModel)
                                    : "Storage";
                            }
                        }
                    }
                }

                if (nearestStorageName != null)
                {
                    results.Add($"{nearestStorageName}: {nearestStorageDist:F0} tiles");
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
