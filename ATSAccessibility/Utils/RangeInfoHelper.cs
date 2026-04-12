using ATSAccessibility.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Helper class for getting building range/reach information.
	/// Used by 'd' key to announce what resources are in range or what buildings are connected.
	/// </summary>
	public static class RangeInfoHelper {
		/// <summary>
		/// Check if a building is finished (not under construction).
		/// </summary>
		private static bool IsBuildingFinished(object building) {
			if (building == null) return false;
			var method = building.GetType().GetMethod("IsFinished",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			return method != null && (bool)method.Invoke(building, null);
		}

		/// <summary>
		/// Find the nearest finished storage building to a position.
		/// </summary>
		private static (string name, float distance)? FindNearestStorage(Vector2 position) {
			var storages = ConstructionReflection.GetAllStorageBuildings();
			if (storages == null) return null;

			float nearestDist = float.MaxValue;
			string nearestName = null;

			foreach (var storage in storages) {
				if (storage == null) continue;
				if (!IsBuildingFinished(storage)) continue;

				var entrance = ConstructionReflection.GetBuildingEntranceCenter(storage);
				if (!entrance.HasValue) continue;

				float dist = Vector2.Distance(position, entrance.Value);
				if (dist < nearestDist) {
					nearestDist = dist;
					var model = ConstructionReflection.GetBuildingModel(storage);
					nearestName = model != null ? GameReflection.GetDisplayName(model) : Strings.Get("common.storage");
				}
			}

			return nearestName != null ? (nearestName, nearestDist) : null;
		}

		/// <summary>
		/// Get range info for a placed building.
		/// </summary>
		public static string GetBuildingRangeInfo(object building) {
			if (building == null) return Strings.Get("common.no_building");

			var model = ConstructionReflection.GetBuildingModel(building);
			if (model == null) return Strings.Get("common.unknown_building");

			var center = ConstructionReflection.GetBuildingCenter(building);
			if (!center.HasValue) return Strings.Get("util.range.cannot_center");

			Vector2 center2D = new Vector2(center.Value.x, center.Value.z);

			// Check building type and get appropriate info
			if (ConstructionReflection.IsCampModel(model)) {
				return GetCampRangeInfo(model, center2D);
			} else if (ConstructionReflection.IsGathererHutModel(model)) {
				return GetGathererHutRangeInfo(model, center2D);
			} else if (ConstructionReflection.IsFishingHutModel(model)) {
				return GetFishingHutRangeInfo(model, center2D);
			} else if (ConstructionReflection.IsHearthModel(model)) {
				return GetHearthRangeInfo(building);
			} else if (BuildingReflection.IsFarm(building)) {
				return GetFarmRangeInfo(building);
			} else if (BuildingReflection.IsFarmfield(building)) {
				var fieldPos = ConstructionReflection.GetBuildingGridPosition(building);
				if (fieldPos != Vector2Int.zero) {
					return GetFarmRangeInfoForTile(fieldPos.x, fieldPos.y);
				}
				return Strings.Get("util.range.no_farms_in_range");
			} else if (BuildingReflection.IsProductionBuilding(building)) {
				// For production buildings (Workshop, Mine), show supply chain info
				return GetProductionBuildingSupplyInfo(building);
			} else {
				// For other buildings (Houses, Institutions, Decorations), show hearth connection
				return GetBuildingHearthConnection(building);
			}
		}

		/// <summary>
		/// Get range preview for a building about to be placed.
		/// </summary>
		public static string GetBuildingRangePreview(object buildingModel, int cursorX, int cursorY, int rotation, bool canPlace) {
			if (!canPlace) return Strings.Get("util.range.invalid");
			if (buildingModel == null) return Strings.Get("util.range.no_building_selected");

			// Calculate center based on cursor and size
			Vector2Int baseSize = ConstructionReflection.GetBuildingSize(buildingModel);
			bool isRotated = (rotation % 2) == 1;
			Vector2Int effectiveSize = isRotated
				? new Vector2Int(baseSize.y, baseSize.x)
				: baseSize;

			Vector2 center2D = ConstructionReflection.CalculateBuildingCenter(cursorX, cursorY, effectiveSize);

			// Check building type and get appropriate info
			if (ConstructionReflection.IsCampModel(buildingModel)) {
				return GetCampRangeInfo(buildingModel, center2D);
			} else if (ConstructionReflection.IsGathererHutModel(buildingModel)) {
				return GetGathererHutRangeInfo(buildingModel, center2D);
			} else if (ConstructionReflection.IsFishingHutModel(buildingModel)) {
				return GetFishingHutRangeInfo(buildingModel, center2D);
			} else if (ConstructionReflection.IsHearthModel(buildingModel)) {
				// For hearth preview, we can't use IsInRange yet - show base range
				float range = ConstructionReflection.GetEffectiveHearthRange(buildingModel);
				return Strings.Get("util.range.hearth_preview", range.ToString("F1"));
			} else if (ConstructionReflection.IsFarmModel(buildingModel)) {
				return GetFarmRangePreview(buildingModel, cursorX, cursorY, effectiveSize);
			} else if (ConstructionReflection.IsWorkshopModel(buildingModel)) {
				// For workshop preview, show nearby suppliers and storage distance
				return GetProductionBuildingPreview(buildingModel, cursorX, cursorY);
			} else if (ConstructionReflection.IsHouseModel(buildingModel) ||
					   ConstructionReflection.IsInstitutionModel(buildingModel) ||
					   ConstructionReflection.IsDecorationModel(buildingModel)) {
				// Houses, Institutions, and Decorations are affected by hearth range
				return GetPositionHearthConnection(cursorX, cursorY);
			} else {
				// Other production buildings - show nearby suppliers and storage distance
				return GetProductionBuildingPreview(buildingModel, cursorX, cursorY);
			}
		}

		/// <summary>
		/// Get range info for a resource at a position (inverse of building range).
		/// Finds placed buildings that can exploit this resource and their distances.
		/// </summary>
		public static string GetResourceRangeInfo(int cursorX, int cursorY) {
			try {
				var objectOn = GameReflection.GetObjectOn(cursorX, cursorY);
				if (objectOn == null) return Strings.Get("util.range.no_building_or_resource");

				string typeName = objectOn.GetType().Name;

				if (typeName == "NaturalResource") {
					return GetNaturalResourceRangeInfo(objectOn);
				} else if (typeName == "ResourceDeposit") {
					return GetDepositRangeInfo(objectOn);
				} else if (typeName == "Lake") {
					return GetLakeRangeInfo(objectOn);
				} else if (typeName == "Field") {
					// GetObjectOn returns the Field object itself for empty tiles
					if (MapReflection.IsFieldGrass(objectOn)) {
						return GetFarmRangeInfoForTile(cursorX, cursorY);
					}
					return Strings.Get("util.range.no_building_or_resource");
				} else {
					return Strings.Get("util.range.no_building_or_resource");
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetResourceRangeInfo failed: {ex.Message}");
				return Strings.Get("util.range.no_building_or_resource");
			}
		}

		/// <summary>
		/// Find placed camps in range that can harvest a natural resource.
		/// </summary>
		private static string GetNaturalResourceRangeInfo(object resource) {
			var field = ConstructionReflection.GetResourceField(resource);
			if (!field.HasValue) return Strings.Get("util.range.no_buildings_in_range");

			// Get the resource's good name for matching against camp recipes
			var modelProp = resource.GetType().GetProperty("Model");
			if (modelProp == null) return Strings.Get("util.range.no_buildings_in_range");
			var model = modelProp.GetValue(resource);
			if (model == null) return Strings.Get("util.range.no_buildings_in_range");

			var refGoodNameProp = model.GetType().GetProperty("RefGoodName");
			if (refGoodNameProp == null) return Strings.Get("util.range.no_buildings_in_range");
			string refGoodName = refGoodNameProp.GetValue(model) as string;
			if (string.IsNullOrEmpty(refGoodName)) return Strings.Get("util.range.no_buildings_in_range");

			// Find all placed camps and check which can harvest this good and are in range
			var camps = ConstructionReflection.GetAllCamps();
			if (camps == null) return Strings.Get("util.range.no_buildings_in_range");

			var matches = new List<(string name, float distance)>();

			foreach (var camp in camps) {
				if (camp == null) continue;
				if (!IsBuildingFinished(camp)) continue;

				var campModel = ConstructionReflection.GetBuildingModel(camp);
				if (campModel == null) continue;

				// Check if this camp type can harvest this good
				var goodNames = ConstructionReflection.GetGatheringBuildingGoodNames(campModel);
				if (!goodNames.Contains(refGoodName)) continue;

				var center = ConstructionReflection.GetBuildingCenter(camp);
				if (!center.HasValue) continue;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float distance = ConstructionReflection.CalculateResourceDistance(center2D, field.Value);
				float maxDistance = ConstructionReflection.GetGatheringBuildingMaxDistance(campModel);

				if (distance < maxDistance) {
					string name = GameReflection.GetDisplayName(campModel) ?? Strings.Get("util.range.camp");
					matches.Add((name, distance));
				}
			}

			return FormatBuildingRangeMatches(matches);
		}

		/// <summary>
		/// Find placed gatherer huts in range that can work a resource deposit.
		/// </summary>
		private static string GetDepositRangeInfo(object deposit) {
			var field = ConstructionReflection.GetResourceField(deposit);
			if (!field.HasValue) return Strings.Get("util.range.no_buildings_in_range");

			var size = ConstructionReflection.GetResourceSize(deposit) ?? Vector2Int.one;

			// Get the deposit model for matching against hut recipes
			var modelProp = deposit.GetType().GetProperty("Model");
			if (modelProp == null) return Strings.Get("util.range.no_buildings_in_range");
			var depositModel = modelProp.GetValue(deposit);
			if (depositModel == null) return Strings.Get("util.range.no_buildings_in_range");

			// Get the deposit's good name (ResourceDepositModel inherits GoodName from ResourceModel)
			var goodNameProp = depositModel.GetType().GetProperty("GoodName");
			if (goodNameProp == null) return Strings.Get("util.range.no_buildings_in_range");
			string goodName = goodNameProp.GetValue(depositModel) as string;
			if (string.IsNullOrEmpty(goodName)) return Strings.Get("util.range.no_buildings_in_range");

			// Get the deposit's minimum grade requirement
			int depositMinGrade = ConstructionReflection.GetResourceMinGradeLevel(deposit);

			var huts = ConstructionReflection.GetAllGathererHuts();
			if (huts == null) return Strings.Get("util.range.no_buildings_in_range");

			var matches = new List<(string name, float distance)>();

			foreach (var hut in huts) {
				if (hut == null) continue;
				if (!IsBuildingFinished(hut)) continue;

				var hutModel = ConstructionReflection.GetBuildingModel(hut);
				if (hutModel == null) continue;

				var goodNames = ConstructionReflection.GetGatheringBuildingGoodNames(hutModel);
				if (!goodNames.Contains(goodName)) continue;

				// Check if the hut's recipe grade is high enough for this deposit
				if (depositMinGrade >= 0) {
					var gradeLevels = ConstructionReflection.GetGatheringBuildingGradeLevels(hutModel);
					int hutGrade;
					if (gradeLevels.TryGetValue(goodName, out hutGrade) && hutGrade < depositMinGrade) continue;
				}

				var center = ConstructionReflection.GetBuildingCenter(hut);
				if (!center.HasValue) continue;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float distance = ConstructionReflection.CalculateDepositDistance(center2D, field.Value, size);
				float maxDistance = ConstructionReflection.GetGatheringBuildingMaxDistance(hutModel);

				if (distance < maxDistance) {
					string name = GameReflection.GetDisplayName(hutModel) ?? Strings.Get("util.range.gatherer_hut");
					matches.Add((name, distance));
				}
			}

			return FormatBuildingRangeMatches(matches);
		}

		/// <summary>
		/// Find placed fishing huts in range that can work a lake.
		/// </summary>
		private static string GetLakeRangeInfo(object lake) {
			var field = ConstructionReflection.GetResourceField(lake);
			if (!field.HasValue) return Strings.Get("util.range.no_buildings_in_range");

			var size = ConstructionReflection.GetResourceSize(lake) ?? Vector2Int.one;

			// LakeModel inherits GoodName from ResourceModel
			var modelProp = lake.GetType().GetProperty("Model");
			if (modelProp == null) return Strings.Get("util.range.no_buildings_in_range");
			var lakeModel = modelProp.GetValue(lake);
			if (lakeModel == null) return Strings.Get("util.range.no_buildings_in_range");

			var goodNameProp = lakeModel.GetType().GetProperty("GoodName");
			string goodName = goodNameProp?.GetValue(lakeModel) as string;

			// Get the lake's minimum grade requirement
			int lakeMinGrade = ConstructionReflection.GetResourceMinGradeLevel(lake);

			var huts = ConstructionReflection.GetAllFishingHuts();
			if (huts == null) return Strings.Get("util.range.no_buildings_in_range");

			var matches = new List<(string name, float distance)>();

			foreach (var hut in huts) {
				if (hut == null) continue;
				if (!IsBuildingFinished(hut)) continue;

				var hutModel = ConstructionReflection.GetBuildingModel(hut);
				if (hutModel == null) continue;

				// If we have a good name, check recipe match; otherwise accept all fishing huts
				if (!string.IsNullOrEmpty(goodName)) {
					var goodNames = ConstructionReflection.GetGatheringBuildingGoodNames(hutModel);
					if (!goodNames.Contains(goodName)) continue;

					// Check if the hut's recipe grade is high enough for this lake
					if (lakeMinGrade >= 0) {
						var gradeLevels = ConstructionReflection.GetGatheringBuildingGradeLevels(hutModel);
						int hutGrade;
						if (gradeLevels.TryGetValue(goodName, out hutGrade) && hutGrade < lakeMinGrade) continue;
					}
				}

				var center = ConstructionReflection.GetBuildingCenter(hut);
				if (!center.HasValue) continue;

				Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
				float distance = ConstructionReflection.CalculateDepositDistance(center2D, field.Value, size);
				float maxDistance = ConstructionReflection.GetGatheringBuildingMaxDistance(hutModel);

				if (distance < maxDistance) {
					string name = GameReflection.GetDisplayName(hutModel) ?? Strings.Get("util.range.fishing_hut");
					matches.Add((name, distance));
				}
			}

			return FormatBuildingRangeMatches(matches);
		}

		/// <summary>
		/// Format matched buildings into announcement string, sorted by distance.
		/// </summary>
		private static string FormatBuildingRangeMatches(List<(string name, float distance)> matches) {
			if (matches.Count == 0)
				return Strings.Get("util.range.no_buildings_in_range");

			matches.Sort((a, b) => a.distance.CompareTo(b.distance));

			var parts = new List<string>();
			foreach (var match in matches) {
				parts.Add(Strings.Get("util.range.match_entry", match.name, match.distance.ToString("F0")));
			}

			return string.Join(". ", parts);
		}

		/// <summary>
		/// Find placed farms whose work area covers the given tile position.
		/// </summary>
		private static string GetFarmRangeInfoForTile(int tileX, int tileY) {
			try {
				var farms = ConstructionReflection.GetAllFarms();
				if (farms == null) return Strings.Get("util.range.no_farms_in_range");

				var matches = new List<(string name, float distance)>();
				int bonus = ConstructionReflection.GetBonusFarmArea();

				foreach (var farm in farms) {
					if (farm == null) continue;
					if (!IsBuildingFinished(farm)) continue;

					var model = ConstructionReflection.GetBuildingModel(farm);
					if (model == null) continue;

					var farmPos = ConstructionReflection.GetBuildingGridPosition(farm);
					if (farmPos == Vector2Int.zero) continue;

					var buildingSize = ConstructionReflection.GetBuildingSize(model);
					Vector2Int baseWorkArea = MapReflection.GetFarmModelWorkArea(model);
					Vector2Int workArea = new Vector2Int(baseWorkArea.x + bonus, baseWorkArea.y + bonus);

					// Calculate work area bounds
					int minX = farmPos.x - workArea.x;
					int maxX = farmPos.x + buildingSize.x + workArea.x - 1;
					int minY = farmPos.y - workArea.y;
					int maxY = farmPos.y + buildingSize.y + workArea.y - 1;

					// Check if tile is within work area (and not under the farm building itself)
					if (tileX >= minX && tileX <= maxX && tileY >= minY && tileY <= maxY) {
						bool underBuilding = tileX >= farmPos.x && tileX < farmPos.x + buildingSize.x &&
											 tileY >= farmPos.y && tileY < farmPos.y + buildingSize.y;
						if (!underBuilding) {
							// Calculate distance from farm center to tile
							var center = ConstructionReflection.GetBuildingCenter(farm);
							float distance = 0f;
							if (center.HasValue) {
								Vector2 center2D = new Vector2(center.Value.x, center.Value.z);
								distance = Vector2.Distance(center2D, new Vector2(tileX + 0.5f, tileY + 0.5f));
							}

							string name = GameReflection.GetDisplayName(model) ?? Strings.Get("util.range.farm");
							matches.Add((name, distance));
						}
					}
				}

				if (matches.Count == 0)
					return Strings.Get("util.range.no_farms_in_range");

				return FormatBuildingRangeMatches(matches);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetFarmRangeInfoForTile failed: {ex.Message}");
				return Strings.Get("util.range.no_farms_in_range");
			}
		}

		/// <summary>
		/// Get resource info for gathering buildings (Camp, GathererHut, FishingHut).
		/// Groups by resource node display name.
		/// </summary>
		private static string GetGatheringBuildingRangeInfo(
			object model, Vector2 center2D, object resourceDict,
			bool isDeposit, string resourceTypeName) {
			float maxDistance = ConstructionReflection.GetGatheringBuildingMaxDistance(model);
			var goodNames = ConstructionReflection.GetGatheringBuildingGoodNames(model);

			if (resourceDict == null || goodNames.Count == 0)
				return Strings.Get("util.range.no_type_available", resourceTypeName);

			// For deposits/lakes, get grade levels to filter out resources the building can't harvest
			Dictionary<string, int> gradeLevels = isDeposit
				? ConstructionReflection.GetGatheringBuildingGradeLevels(model)
				: null;

			var nodeInfo = CountResourcesByNodeName(resourceDict, goodNames, center2D, maxDistance, isDeposit, gradeLevels);

			if (nodeInfo.Count == 0)
				return Strings.Get("util.range.no_type_in_range", resourceTypeName);

			var results = new List<string>();
			foreach (var kvp in nodeInfo) {
				results.Add(Strings.Get("util.range.resource_entry", kvp.Key, kvp.Value.count, kvp.Value.closestDistance.ToString("F0")));
			}

			return string.Join(". ", results);
		}

		private static string GetCampRangeInfo(object campModel, Vector2 center2D) {
			return GetGatheringBuildingRangeInfo(
				campModel, center2D, ConstructionReflection.GetAvailableResources(),
				isDeposit: false, Strings.Get("common.resources_lower"));
		}

		private static string GetGathererHutRangeInfo(object hutModel, Vector2 center2D) {
			return GetGatheringBuildingRangeInfo(
				hutModel, center2D, ConstructionReflection.GetAvailableDeposits(),
				isDeposit: true, Strings.Get("util.range.type_deposits"));
		}

		private static string GetFishingHutRangeInfo(object hutModel, Vector2 center2D) {
			return GetGatheringBuildingRangeInfo(
				hutModel, center2D, ConstructionReflection.GetAvailableLakes(),
				isDeposit: true, Strings.Get("util.range.type_lakes"));
		}

		/// <summary>
		/// Get info about buildings in hearth range.
		/// Only counts Houses, Institutions, and Decorations (what matters for hub level).
		/// </summary>
		private static string GetHearthRangeInfo(object hearth) {
			int housesCount = 0;
			int institutionsCount = 0;
			int decorationsCount = 0;

			try {
				// Count finished houses in range
				var houses = ConstructionReflection.GetAllHouses();
				if (houses != null) {
					foreach (var house in houses) {
						if (house == null) continue;

						if (IsBuildingFinished(house) && ConstructionReflection.IsInHearthRange(hearth, house)) {
							housesCount++;
						}
					}
				}

				// Count finished institutions in range
				var institutions = ConstructionReflection.GetAllInstitutions();
				if (institutions != null) {
					foreach (var institution in institutions) {
						if (institution == null) continue;

						if (IsBuildingFinished(institution) && ConstructionReflection.IsInHearthRange(hearth, institution)) {
							institutionsCount++;
						}
					}
				}

				// Count finished decorations in range
				var decorations = ConstructionReflection.GetAllDecorations();
				if (decorations != null) {
					foreach (var decoration in decorations) {
						if (decoration == null) continue;

						if (IsBuildingFinished(decoration) && ConstructionReflection.IsInHearthRange(hearth, decoration)) {
							decorationsCount++;
						}
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetHearthRangeInfo failed: {ex.Message}");
			}

			if (housesCount == 0 && institutionsCount == 0 && decorationsCount == 0)
				return Strings.Get("util.range.no_buildings_in_range");

			var parts = new List<string>();
			if (housesCount > 0)
				parts.Add(Strings.Get("util.range.count_entry", housesCount, housesCount == 1 ? Strings.Get("util.range.house") : Strings.Get("util.range.houses")));
			if (institutionsCount > 0)
				parts.Add(Strings.Get("util.range.count_entry", institutionsCount, institutionsCount == 1 ? Strings.Get("util.range.institution") : Strings.Get("util.range.institutions")));
			if (decorationsCount > 0)
				parts.Add(Strings.Get("util.range.count_entry", decorationsCount, decorationsCount == 1 ? Strings.Get("util.range.decoration") : Strings.Get("common.decorations")));

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Get range info for a Farm building.
		/// Shows farm fields and available fertile soil separately.
		/// </summary>
		private static string GetFarmRangeInfo(object farm) {
			try {
				var model = ConstructionReflection.GetBuildingModel(farm);
				if (model == null) return Strings.Get("util.range.no_fertile_soil");

				var fieldPos = ConstructionReflection.GetBuildingGridPosition(farm);
				if (fieldPos == Vector2Int.zero) return Strings.Get("util.range.no_fertile_soil");

				var buildingSize = ConstructionReflection.GetBuildingSize(model);
				Vector2Int baseWorkArea = MapReflection.GetFarmModelWorkArea(model);
				int bonus = ConstructionReflection.GetBonusFarmArea();
				Vector2Int workArea = new Vector2Int(baseWorkArea.x + bonus, baseWorkArea.y + bonus);

				int minX = fieldPos.x - workArea.x;
				int maxX = fieldPos.x + buildingSize.x + workArea.x - 1;
				int minY = fieldPos.y - workArea.y;
				int maxY = fieldPos.y + buildingSize.y + workArea.y - 1;

				int mapWidth = GameReflection.GetMapWidth();
				int mapHeight = GameReflection.GetMapHeight();
				int grassCount = 0;
				int farmfieldCount = 0;

				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) continue;

						// Skip building footprint
						if (x >= fieldPos.x && x < fieldPos.x + buildingSize.x &&
							y >= fieldPos.y && y < fieldPos.y + buildingSize.y) continue;

						// Skip unrevealed glades
						if (MapReflection.IsInUnrevealedGlade(x, y)) continue;

						var field = GameReflection.GetField(x, y);
						if (field == null) continue;

						if (MapReflection.IsFieldGrass(field)) {
							if (ConstructionReflection.HasFarmfieldAt(x, y))
								farmfieldCount++;
							else
								grassCount++;
						}
					}
				}

				if (grassCount == 0 && farmfieldCount == 0)
					return Strings.Get("util.range.no_fertile_soil");

				var parts = new List<string>();
				if (farmfieldCount > 0)
					parts.Add(Strings.Get("util.range.farm_fields", farmfieldCount));
				if (grassCount > 0)
					parts.Add(Strings.Get("util.range.fertile_soil", grassCount));

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetFarmRangeInfo failed: {ex.Message}");
				return Strings.Get("util.range.cannot_determine_range");
			}
		}

		/// <summary>
		/// Get range preview for a Farm being placed.
		/// Counts fertile soil (grass) tiles and existing farm fields in the work area.
		/// </summary>
		private static string GetFarmRangePreview(object farmModel, int cursorX, int cursorY, Vector2Int buildingSize) {
			try {
				// Get work area from model + meta bonus
				Vector2Int baseWorkArea = MapReflection.GetFarmModelWorkArea(farmModel);
				if (baseWorkArea == Vector2Int.zero) {
					return Strings.Get("util.range.cannot_determine_work_area");
				}

				int bonus = ConstructionReflection.GetBonusFarmArea();
				Vector2Int workArea = new Vector2Int(baseWorkArea.x + bonus, baseWorkArea.y + bonus);

				// Calculate the area bounds (work area extends around the building)
				int minX = cursorX - workArea.x;
				int maxX = cursorX + buildingSize.x + workArea.x - 1;
				int minY = cursorY - workArea.y;
				int maxY = cursorY + buildingSize.y + workArea.y - 1;

				// Count grass tiles and farm fields in the area
				int grassCount = 0;
				int farmfieldCount = 0;
				int mapWidth = GameReflection.GetMapWidth();
				int mapHeight = GameReflection.GetMapHeight();

				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						// Skip out of bounds
						if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) continue;

						// Skip tiles covered by the building itself
						if (x >= cursorX && x < cursorX + buildingSize.x &&
							y >= cursorY && y < cursorY + buildingSize.y) continue;

						// Skip unrevealed glades
						if (MapReflection.IsInUnrevealedGlade(x, y)) continue;

						var field = GameReflection.GetField(x, y);
						if (field == null) continue;

						// Check if grass tile using FieldType
						if (MapReflection.IsFieldGrass(field)) {
							// Check if there's a finished farmfield at this position
							if (ConstructionReflection.HasFarmfieldAt(x, y)) {
								farmfieldCount++;
							} else {
								grassCount++;
							}
						}
					}
				}

				if (grassCount == 0 && farmfieldCount == 0)
					return Strings.Get("util.range.no_fertile_soil");

				var parts = new List<string>();
				if (farmfieldCount > 0)
					parts.Add(Strings.Get("util.range.farm_fields", farmfieldCount));
				if (grassCount > 0)
					parts.Add(Strings.Get("util.range.fertile_soil", grassCount));

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetFarmRangePreview failed: {ex.Message}");
				return Strings.Get("util.range.cannot_determine_range");
			}
		}

		/// <summary>
		/// Get supply chain info for a production building.
		/// Shows nearby buildings that can supply required inputs and distance to storage.
		/// </summary>
		private static string GetProductionBuildingSupplyInfo(object building) {
			var results = new List<string>();

			try {
				// Get building's entrance center for distance calculations
				var entranceCenter = ConstructionReflection.GetBuildingEntranceCenter(building);
				if (!entranceCenter.HasValue) {
					return Strings.Get("util.range.cannot_determine_position");
				}

				Vector2 buildingPos = entranceCenter.Value;

				// For placed buildings, get only the allowed inputs from active recipes
				var allowedInputs = ConstructionReflection.GetBuildingRequiredInputs(building);
				if (allowedInputs.Count > 0) {
					// Use actual outputs check (what suppliers can really produce)
					results.Add(FindNearbySuppliers(buildingPos, allowedInputs, building, useActualOutputs: true));
				}

				// 2. Find nearest storage (warehouse) and its distance
				var nearestStorage = FindNearestStorage(buildingPos);
				if (nearestStorage.HasValue) {
					results.Add(Strings.Get("util.range.storage_entry", nearestStorage.Value.name, nearestStorage.Value.distance.ToString("F0")));
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetProductionBuildingSupplyInfo failed: {ex.Message}");
				return Strings.Get("util.range.cannot_determine_supply");
			}

			return results.Count > 0 ? string.Join(". ", results) : Strings.Get("util.range.no_supply_chain_info");
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
		private static string FindNearbySuppliers(Vector2 buildingPos, List<string> requiredInputs, object excludeBuilding = null, bool useActualOutputs = false) {
			if (requiredInputs.Count == 0) {
				return Strings.Get("util.range.no_inputs_required");
			}

			float localStorageRange = ConstructionReflection.GetLocalStorageDistance();

			// Cache per-producer data to avoid redundant calculations when a building produces multiple goods
			// Key: producer object, Value: (name, distance, actual outputs if needed)
			var producerCache = new Dictionary<object, (string name, float distance, List<string> actualOutputs)>();

			// Track goods per producer name (for display grouping)
			var nearbySuppliers = new Dictionary<string, (List<string> goods, float minDistance)>();

			foreach (var inputGood in requiredInputs) {
				var producers = ConstructionReflection.GetBuildingsThatProduce(inputGood);
				foreach (var producer in producers) {
					if (excludeBuilding != null && producer == excludeBuilding) continue;

					// Check cache first to avoid redundant distance/name calculations
					if (!producerCache.TryGetValue(producer, out var cached)) {
						var producerEntrance = ConstructionReflection.GetBuildingEntranceCenter(producer);
						if (!producerEntrance.HasValue) continue;

						float dist = Vector2.Distance(buildingPos, producerEntrance.Value);

						// Skip if outside local storage range
						if (dist > localStorageRange) continue;

						var producerModel = ConstructionReflection.GetBuildingModel(producer);
						string producerName = producerModel != null
							? GameReflection.GetDisplayName(producerModel) ?? Strings.Get("common.building")
							: Strings.Get("common.building");

						// Get actual outputs once if needed
						List<string> actualOutputs = useActualOutputs
							? ConstructionReflection.GetBuildingActualOutputs(producer)
							: null;

						cached = (producerName, dist, actualOutputs);
						producerCache[producer] = cached;
					} else if (cached.distance > localStorageRange) {
						// Previously cached but out of range
						continue;
					}

					// Check if this producer can actually output this good
					if (useActualOutputs && (cached.actualOutputs == null || !cached.actualOutputs.Contains(inputGood))) {
						continue;
					}

					// Get good display name
					string goodDisplayName = GetGoodDisplayName(inputGood) ?? inputGood;

					// Track by producer name - merge goods and keep minimum distance
					if (!nearbySuppliers.ContainsKey(cached.name)) {
						nearbySuppliers[cached.name] = (new List<string>(), cached.distance);
					}

					var entry = nearbySuppliers[cached.name];
					if (!entry.goods.Contains(goodDisplayName)) {
						entry.goods.Add(goodDisplayName);
					}
					// Keep the minimum distance for this producer type
					if (cached.distance < entry.minDistance) {
						nearbySuppliers[cached.name] = (entry.goods, cached.distance);
					}
				}
			}

			if (nearbySuppliers.Count > 0) {
				var supplierParts = new List<string>();
				foreach (var kvp in nearbySuppliers) {
					string goodsList = string.Join(", ", kvp.Value.goods);
					supplierParts.Add(Strings.Get("util.range.supplier_entry", kvp.Key, goodsList, kvp.Value.minDistance.ToString("F0")));
				}
				return Strings.Get("util.range.nearby", string.Join(", ", supplierParts));
			} else {
				return Strings.Get("util.range.no_nearby_suppliers");
			}
		}

		/// <summary>
		/// Get hearth connection info for a non-hearth building.
		/// Only Houses, Institutions, and Decorations are affected by hearth range.
		/// </summary>
		private static string GetBuildingHearthConnection(object building) {
			// Cache type to avoid repeated GetType() calls
			var buildingType = building.GetType();

			// Check if this building type is affected by hearth range
			string typeName = buildingType.Name;
			bool isHearthRelevant = typeName == "House" || typeName == "Institution" || typeName == "Decoration";

			if (!isHearthRelevant) {
				return Strings.Get("util.range.no_range_info");
			}

			// Get building's field position
			var fieldProp = buildingType.GetProperty("Field",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
			if (fieldProp == null) return Strings.Get("util.range.cannot_determine_pos");

			var fieldValue = fieldProp.GetValue(building);
			if (fieldValue == null) return Strings.Get("util.range.cannot_determine_pos");
			var field = (Vector2Int)fieldValue;
			return GetPositionHearthConnection(field.x, field.y);
		}

		/// <summary>
		/// Get hearth connection info for a position (used for preview).
		/// </summary>
		private static string GetPositionHearthConnection(int x, int y) {
			var position = new Vector2Int(x, y);
			var hearths = ConstructionReflection.GetAllHearths();
			if (hearths == null) return Strings.Get("util.range.cannot_access_hearths");

			var connectedHearths = new List<string>();

			foreach (var hearth in hearths) {
				if (hearth == null) continue;

				if (ConstructionReflection.IsInHearthRange(hearth, position)) {
					// Get hearth's center and calculate distance
					var hearthCenter = ConstructionReflection.GetBuildingCenter(hearth);
					if (hearthCenter.HasValue) {
						Vector2 hearthCenter2D = new Vector2(hearthCenter.Value.x, hearthCenter.Value.z);
						float distance = Vector2.Distance(hearthCenter2D, new Vector2(x, y));

						// Get hearth name
						var model = ConstructionReflection.GetBuildingModel(hearth);
						string hearthName = model != null
							? GameReflection.GetDisplayName(model) ?? Strings.Get("util.range.hearth")
							: Strings.Get("util.range.hearth");

						connectedHearths.Add(Strings.Get("util.range.connected_hearth", hearthName, distance.ToString("F0")));
					} else {
						connectedHearths.Add(Strings.Get("util.range.hearth"));
					}
				}
			}

			return connectedHearths.Count > 0
				? Strings.Get("util.range.connected_to", string.Join("; ", connectedHearths))
				: Strings.Get("util.range.not_in_hearth_range");
		}

		/// <summary>
		/// Count resources in range grouped by their node display name.
		/// Returns a dictionary of node name -> (count, closest distance).
		/// When gradeLevels is provided, filters out deposits/lakes whose minGradeToCollect
		/// exceeds the building's recipe grade for that good.
		/// </summary>
		private static Dictionary<string, (int count, float closestDistance)> CountResourcesByNodeName(
			object resourceDict, List<string> goodNames, Vector2 center2D, float maxDistance, bool isDeposit,
			Dictionary<string, int> gradeLevels = null) {
			var result = new Dictionary<string, (int count, float closestDistance)>();

			if (resourceDict == null) return result;

			try {
				var dict = resourceDict as IDictionary;
				if (dict == null) return result;

				// Iterate through all good names the building can harvest
				foreach (var goodName in goodNames) {
					if (!dict.Contains(goodName)) continue;

					// Get the building's recipe grade level for this good (if grade filtering is active)
					int recipeGradeLevel = -1;
					if (gradeLevels != null) {
						gradeLevels.TryGetValue(goodName, out recipeGradeLevel);
					}

					var resourceList = dict[goodName] as IEnumerable;
					if (resourceList == null) continue;

					foreach (var resource in resourceList) {
						// Filter by grade: skip deposits/lakes the building can't harvest
						if (gradeLevels != null) {
							int minGrade = ConstructionReflection.GetResourceMinGradeLevel(resource);
							if (minGrade >= 0 && recipeGradeLevel < minGrade) continue;
						}

						var field = ConstructionReflection.GetResourceField(resource);
						if (!field.HasValue) continue;

						float distance;
						if (isDeposit) {
							// Deposits/lakes can be multi-tile, check closest tile
							var size = ConstructionReflection.GetResourceSize(resource) ?? Vector2Int.one;
							distance = ConstructionReflection.CalculateDepositDistance(center2D, field.Value, size);
						} else {
							// Natural resources are single-tile
							distance = ConstructionReflection.CalculateResourceDistance(center2D, field.Value);
						}

						if (distance < maxDistance) {
							// Get the node's display name (e.g., "Lush Tree", "Mushrooms")
							string nodeName = ConstructionReflection.GetResourceNodeDisplayName(resource);
							if (string.IsNullOrEmpty(nodeName)) {
								// Fallback to good name if display name not available
								nodeName = GetGoodDisplayName(goodName) ?? goodName;
							}

							if (!result.ContainsKey(nodeName)) {
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
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] CountResourcesByNodeName failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get production building preview for a position (used in build mode).
		/// Shows nearby suppliers for all possible inputs and distance to nearest storage.
		/// </summary>
		private static string GetProductionBuildingPreview(object buildingModel, int cursorX, int cursorY) {
			var results = new List<string>();

			try {
				Vector2 buildingPos = new Vector2(cursorX, cursorY);

				// 1. Find nearby suppliers using model's possible inputs
				// Use all possible inputs (building not configured yet), but check actual outputs of suppliers
				var possibleInputs = ConstructionReflection.GetModelPossibleInputs(buildingModel);
				if (possibleInputs.Count > 0) {
					results.Add(FindNearbySuppliers(buildingPos, possibleInputs, excludeBuilding: null, useActualOutputs: true));
				}

				// 2. Find nearest storage (warehouse) and its distance
				var nearestStorage = FindNearestStorage(buildingPos);
				if (nearestStorage.HasValue) {
					results.Add(Strings.Get("util.range.storage_entry", nearestStorage.Value.name, nearestStorage.Value.distance.ToString("F0")));
				} else {
					results.Add(Strings.Get("util.range.no_storage_found"));
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetProductionBuildingPreview failed: {ex.Message}");
				return Strings.Get("util.range.cannot_determine_supply");
			}

			return results.Count > 0 ? string.Join(". ", results) : Strings.Get("util.range.no_supply_chain_info");
		}

		/// <summary>
		/// Get display name for a good by its internal name.
		/// </summary>
		private static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return null;

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				// Settings.GetGood(string name) returns GoodModel
				var getGoodMethod = settings.GetType().GetMethod("GetGood",
					new Type[] { typeof(string) });
				if (getGoodMethod == null) return null;

				var goodModel = getGoodMethod.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return null;

				return GameReflection.GetDisplayName(goodModel);
			} catch {
				return null;
			}
		}
	}
}
