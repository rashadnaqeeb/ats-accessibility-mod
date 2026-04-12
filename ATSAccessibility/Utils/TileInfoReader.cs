using ATSAccessibility.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Reads detailed tile information (like tooltips) for the I key feature.
	/// Provides building, natural resource, and deposit info via reflection.
	/// Reflection caching is handled by TileInfoReflection.
	/// </summary>
	public static class TileInfoReader {
		// ========================================
		// CONSOLIDATED HELPERS
		// ========================================

		/// <summary>
		/// Get charges info: "X of Y charges"
		/// For NaturalResource: chargesLeft from state, max from model
		/// For Deposit: both from state
		/// </summary>
		private static string GetChargesInfo(object state, FieldInfo chargesLeftField, object maxSource, FieldInfo maxChargesField) {
			int chargesLeft = ReflectionHelper.GetInt(chargesLeftField, state);
			int maxCharges = ReflectionHelper.GetInt(maxChargesField, maxSource);

			return maxCharges > 0 ? Strings.Get("util.tile_info.charges", chargesLeft, maxCharges) : null;
		}

		/// <summary>
		/// Get names from a dictionary of building models.
		/// Shared logic for CampsMatrix and HutsMatrix lookups.
		/// </summary>
		private static string GetSourceBuildingNames(object dictionary, object key, bool useContainsKey) {
			if (dictionary == null || key == null) return null;

			try {
				object buildingList;

				if (useContainsKey) {
					// For object keys (like depositModel), check ContainsKey first
					var containsKeyMethod = dictionary.GetType().GetMethod("ContainsKey");
					if (containsKeyMethod == null) return null;

					bool containsKey = (bool)containsKeyMethod.Invoke(dictionary, new object[] { key });
					if (!containsKey) return null;
				}

				// Get the list using indexer
				var indexerProp = dictionary.GetType().GetProperty("Item");
				if (indexerProp == null) return null;

				try {
					buildingList = indexerProp.GetValue(dictionary, new object[] { key });
				} catch (Exception ex) {
					Debug.LogWarning($"[ATSAccessibility] GetSourceBuildingNames indexer failed: {ex.Message}");
					return null;
				}

				if (buildingList == null) return null;

				var listEnumerable = buildingList as IEnumerable;
				if (listEnumerable == null) return null;

				var names = new List<string>();
				foreach (var building in listEnumerable) {
					if (building == null) continue;

					string name = TileInfoReflection.GetLocalizedText(building, "displayName");
					if (!string.IsNullOrEmpty(name)) {
						names.Add(name);
					}
				}

				return names.Count > 0 ? string.Join(", ", names) : null;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetSourceBuildingNames failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// PUBLIC API
		// ========================================

		/// <summary>
		/// Read and announce detailed info about the object at the current cursor position.
		/// Called when I key is pressed during map navigation.
		/// </summary>
		public static void ReadCurrentTile(int cursorX, int cursorY) {
			var glade = GameReflection.GetGlade(cursorX, cursorY);
			if (glade != null && !TileInfoReflection.GetGladeWasDiscovered(glade)) {
				Speech.Say(Strings.Get("util.tile_info.unrevealed_glade"));
				return;
			}

			var objectOn = GameReflection.GetObjectOn(cursorX, cursorY);

			if (objectOn == null) {
				Speech.Say(Strings.Get("util.tile_info.no_object"));
				return;
			}

			string typeName = objectOn.GetType().Name;

			// GetObjectOn returns Field when there's no actual object
			if (typeName == "Field") {
				Speech.Say(Strings.Get("util.tile_info.no_object"));
				return;
			}

			string info = null;

			// Determine object type and get appropriate info
			// Check inheritance chain for Building (Storage -> ProductionBuilding -> Building)
			if (InheritsFrom(objectOn.GetType(), "Building")) {
				info = GetBuildingInfo(objectOn);
			} else if (typeName == "NaturalResource") {
				info = GetNaturalResourceInfo(objectOn);
			} else if (typeName == "ResourceDeposit") {
				info = GetResourceDepositInfo(objectOn);
			} else if (typeName == "Ore") {
				info = GetOreInfo(objectOn);
			} else if (typeName == "Spring") {
				info = GetSpringInfo(objectOn);
			} else if (typeName == "Lake") {
				info = GetLakeInfo(objectOn);
			} else {
				// Unknown type - try generic name extraction
				info = GetGenericObjectInfo(objectOn);
			}

			if (!string.IsNullOrEmpty(info)) {
				Speech.Say(info);
			} else {
				Speech.Say(Strings.Get("util.tile_info.no_information"));
			}
		}

		// ========================================
		// OBJECT TYPE HANDLERS
		// ========================================

		// ========================================
		// GUIDEPOST DIRECTION HELPERS
		// ========================================

		/// <summary>
		/// Convert an angle (in degrees) to a compass direction string.
		/// Standard compass bearing: 0° = North, 90° = East, 180° = South, 270° = West.
		/// </summary>
		private static string AngleToCompassDirection(float angle) {
			// Normalize to 0-360
			angle = ((angle % 360f) + 360f) % 360f;

			// 8-point compass with 45° segments centered on each direction
			if (angle >= 337.5f || angle < 22.5f) return "North";
			if (angle >= 22.5f && angle < 67.5f) return "Northeast";
			if (angle >= 67.5f && angle < 112.5f) return "East";
			if (angle >= 112.5f && angle < 157.5f) return "Southeast";
			if (angle >= 157.5f && angle < 202.5f) return "South";
			if (angle >= 202.5f && angle < 247.5f) return "Southwest";
			if (angle >= 247.5f && angle < 292.5f) return "West";
			return "Northwest";
		}

		/// <summary>
		/// Get guidepost direction info if the building is a SealGuidepostView.
		/// Returns null if not a guidepost.
		/// </summary>
		private static string GetGuidepostDirection(object building) {
			try {
				// First check if we're in a sealed biome
				if (!MapReflection.IsSealedBiome()) return null;

				// Get the building's view field (Decoration has public "view" field)
				var viewField = building.GetType().GetField("view", GameReflection.PublicInstance);
				if (viewField == null) return null;

				var view = viewField.GetValue(building);
				if (view == null) return null;

				// Check if it's a SealGuidepostView
				if (view.GetType().Name != "SealGuidepostView") return null;

				// Get building's world position from view.Position (inherited from BaseMB)
				var positionProp = view.GetType().GetProperty("Position", GameReflection.PublicInstance);
				if (positionProp == null) return null;

				var positionObj = positionProp.GetValue(view);
				if (!(positionObj is Vector3 position)) return null;

				// Get seal target field
				Vector2Int sealField = MapReflection.GetGuidepostTargetField();
				if (sealField == default) return null;

				// Get seal size for center calculation
				Vector2Int sealSize = MapReflection.GetSealSize();
				if (sealSize == default) return null;

				// Calculate seal center (same as game's GetCenter method)
				Vector3 targetCenter = new Vector3(
					sealField.x + sealSize.x / 2f,
					0f,
					sealField.y + sealSize.y / 2f);

				// Calculate compass bearing from guidepost to seal
				// dir.x = east/west on grid, dir.z = north/south on grid
				Vector3 dir = targetCenter - position;
				float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
				// Normalize to 0-360 and round to whole degree
				int degrees = Mathf.RoundToInt(((angle % 360f) + 360f) % 360f);
				if (degrees == 360) degrees = 0;

				return Strings.Get("util.tile_info.pointing", degrees);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGuidepostDirection failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for a building (description only - name already announced).
		/// </summary>
		private static string GetBuildingInfo(object building) {
			try {
				var parts = new List<string>();

				var buildingType = building.GetType();

				// Mine-specific: prepend underlying ore info
				if (buildingType.Name == "Mine") {
					string oreInfo = GetMineOreInfo(building);
					if (!string.IsNullOrEmpty(oreInfo)) {
						parts.Add(oreInfo);
					}
				}

				// Check for guidepost direction info
				string guidepostInfo = GetGuidepostDirection(building);
				if (!string.IsNullOrEmpty(guidepostInfo))
					parts.Add(guidepostInfo);

				// Construction priority for buildings under construction
				if (ConstructionReflection.IsBuildingUnfinished(building)) {
					int constructionPrio = ConstructionReflection.GetBuildingConstructionPriority(building);
					if (constructionPrio != 0) {
						parts.Add(Strings.Get("util.tile_info.priority", FormatNodePriority(constructionPrio)));
					}
				}

				// Get BuildingModel property for this type
				var buildingModelProp = TileInfoReflection.GetBuildingModelProp(buildingType);
				if (buildingModelProp == null) return parts.Count > 0 ? string.Join(", ", parts) : null;

				var buildingModel = buildingModelProp.GetValue(building);
				if (buildingModel == null) return parts.Count > 0 ? string.Join(", ", parts) : null;

				var modelType = buildingModel.GetType();

				// Get Description property for this model type
				var descProp = TileInfoReflection.GetBuildingModelDescProp(modelType);

				// Get Description
				string desc = ReflectionHelper.GetPropString(descProp, buildingModel);
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetBuildingInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get underlying ore info for a Mine: "Copper Ore, 12 of 20 charges. Coal, 6 of 14 charges"
		/// Groups ores in the mine's footprint by display name and sums currently-accessible
		/// charges at the mine's upgrade level. Matches what the sighted mine panel shows
		/// (Mine.GetChargesLeft/GetMaxCharges).
		/// </summary>
		public static string GetMineOreInfo(object mine) {
			try {
				TileInfoReflection.EnsureMineOreCache();

				var getOreMethod = TileInfoReflection.MineGetOreUnderMineMethod;
				if (getOreMethod == null) return null;

				var oresArray = getOreMethod.Invoke(mine, null) as Array;
				if (oresArray == null || oresArray.Length == 0) return null;

				var modelProp = TileInfoReflection.OreModelProp;
				var availMethod = TileInfoReflection.OreGetAvailableChargesForMethod;
				var maxMethod = TileInfoReflection.OreGetMaxChargesForMethod;
				var displayNameField = TileInfoReflection.OreModelDisplayNameField;
				if (modelProp == null || availMethod == null || maxMethod == null || displayNameField == null) return null;

				var groups = new Dictionary<string, (int avail, int max)>();
				var order = new List<string>();

				foreach (var ore in oresArray) {
					if (ore == null) continue;

					var model = modelProp.GetValue(ore);
					if (model == null) continue;

					var locaText = displayNameField.GetValue(model);
					string name = GameReflection.GetLocaText(locaText);
					if (string.IsNullOrEmpty(name)) continue;

					int avail = ReflectionHelper.InvokeInt(availMethod, ore, mine);
					int max = ReflectionHelper.InvokeInt(maxMethod, ore, mine);

					if (groups.TryGetValue(name, out var totals)) {
						groups[name] = (totals.avail + avail, totals.max + max);
					} else {
						groups[name] = (avail, max);
						order.Add(name);
					}
				}

				if (order.Count == 0) return null;

				var sb = new StringBuilder();
				for (int i = 0; i < order.Count; i++) {
					if (i > 0) sb.Append(". ");
					var name = order[i];
					var totals = groups[name];
					sb.Append(Strings.Get("util.tile_info.mine_ore", name, totals.avail, totals.max));
				}

				return sb.ToString();
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetMineOreInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for a natural resource (tree, mushroom, etc).
		/// Includes: description, charges, products, sources (name excluded - already announced).
		/// </summary>
		private static string GetNaturalResourceInfo(object resource) {
			try {
				var resourceType = resource.GetType();

				// Get Model property
				var modelProp = TileInfoReflection.GetNaturalResourceModelProp(resourceType);
				if (modelProp == null) return null;

				var model = modelProp.GetValue(resource);
				if (model == null) return null;

				var modelType = model.GetType();

				// Get State property
				var stateProp = TileInfoReflection.GetNaturalResourceStateProp(resourceType);
				var state = stateProp?.GetValue(resource);
				var stateType = state?.GetType();

				// Get charges fields
				var modelChargesField = TileInfoReflection.GetResourceModelChargesField(modelType);

				FieldInfo stateChargesLeftField = null;
				if (stateType != null) {
					stateChargesLeftField = TileInfoReflection.GetResourceStateChargesLeftField(stateType);
				}

				// Get RefGoodName property
				var refGoodNameProp = TileInfoReflection.GetResourceModelRefGoodNameProp(modelType);

				var parts = new List<string>();

				// Charges first: chargesLeft from state, max from model
				string chargesInfo = GetChargesInfo(state, stateChargesLeftField, model, modelChargesField);
				if (!string.IsNullOrEmpty(chargesInfo)) {
					parts.Add(chargesInfo);
				}

				// Description from model.description field (LocaText)
				string desc = TileInfoReflection.GetLocalizedText(model, "description");
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				// Main product
				string productInfo = GetMainProductInfo(model);
				if (!string.IsNullOrEmpty(productInfo)) {
					parts.Add(Strings.Get("util.tile_info.produces", productInfo));
				}

				// Extra products
				string extraInfo = GetExtraProductsInfo(model);
				if (!string.IsNullOrEmpty(extraInfo)) {
					parts.Add(Strings.Get("util.tile_info.extra", extraInfo));
				}

				// Source buildings (camps that can harvest)
				string sourcesInfo = GetCampsForResource(model, refGoodNameProp);
				if (!string.IsNullOrEmpty(sourcesInfo)) {
					parts.Add(Strings.Get("util.tile_info.harvested_by", sourcesInfo));
				}

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetNaturalResourceInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for a resource deposit (clay, copper, etc).
		/// Includes: description, charges, products, sources (name excluded - already announced).
		/// </summary>
		private static string GetResourceDepositInfo(object deposit) {
			try {
				var depositType = deposit.GetType();

				// Get Model property
				var modelProp = TileInfoReflection.GetDepositModelProp(depositType);
				if (modelProp == null) return null;

				var model = modelProp.GetValue(deposit);
				if (model == null) return null;

				var modelType = model.GetType();

				// Get State property
				var stateProp = TileInfoReflection.GetDepositStateProp(depositType);
				var state = stateProp?.GetValue(deposit);
				var stateType = state?.GetType();

				// Get Description property
				var descProp = TileInfoReflection.GetDepositModelDescProp(modelType);

				// Get charges fields (both from state for deposits)
				FieldInfo stateChargesLeftField = null;
				FieldInfo stateMaxChargesField = null;
				if (stateType != null) {
					stateChargesLeftField = TileInfoReflection.GetDepositStateChargesLeftField(stateType);
					stateMaxChargesField = TileInfoReflection.GetDepositStateMaxChargesField(stateType);
				}

				var parts = new List<string>();

				// Charges first: both values from state for deposits
				string chargesInfo = GetChargesInfo(state, stateChargesLeftField, state, stateMaxChargesField);
				if (!string.IsNullOrEmpty(chargesInfo)) {
					parts.Add(chargesInfo);
				}

				// Priority (only if non-default)
				int depositPrio = ConstructionReflection.GetResourceNodePriority(deposit);
				if (depositPrio != 0) {
					parts.Add(Strings.Get("util.tile_info.priority", FormatNodePriority(depositPrio)));
				}

				// Description
				string desc = ReflectionHelper.GetPropString(descProp, model);
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				// Main product
				string productInfo = GetMainProductInfo(model);
				if (!string.IsNullOrEmpty(productInfo)) {
					parts.Add(Strings.Get("util.tile_info.produces", productInfo));
				}

				// Extra products
				string extraInfo = GetExtraProductsInfo(model);
				if (!string.IsNullOrEmpty(extraInfo)) {
					parts.Add(Strings.Get("util.tile_info.extra", extraInfo));
				}

				// Source buildings (gatherer huts that can work this deposit)
				string sourcesInfo = GetHutsForDeposit(model);
				if (!string.IsNullOrEmpty(sourcesInfo)) {
					parts.Add(Strings.Get("util.tile_info.gathered_by", sourcesInfo));
				}

				return string.Join(", ", parts);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetResourceDepositInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for an ore deposit (copper vein, etc).
		/// Includes: total charges, description, products.
		/// </summary>
		private static string GetOreInfo(object ore) {
			try {
				var oreType = ore.GetType();

				// Get Model property
				var modelProp = oreType.GetProperty("Model");
				if (modelProp == null) return null;

				var model = modelProp.GetValue(ore);
				if (model == null) return null;

				// Get State property
				var stateProp = oreType.GetProperty("State");
				var state = stateProp?.GetValue(ore);

				var parts = new List<string>();

				// Get total charges from state (mainCharges + extraCharges arrays)
				if (state != null) {
					var stateType = state.GetType();
					var mainChargesField = stateType.GetField("mainCharges", GameReflection.PublicInstance);
					var extraChargesField = stateType.GetField("extraCharges", GameReflection.PublicInstance);

					int totalCharges = 0;
					if (mainChargesField != null) {
						var mainCharges = mainChargesField.GetValue(state) as int[];
						if (mainCharges != null) {
							foreach (int c in mainCharges) totalCharges += c;
						}
					}
					if (extraChargesField != null) {
						var extraCharges = extraChargesField.GetValue(state) as int[];
						if (extraCharges != null) {
							foreach (int c in extraCharges) totalCharges += c;
						}
					}

					if (totalCharges > 0) {
						parts.Add(Strings.Get("util.tile_info.charges_remaining", totalCharges));
					}
				}

				// Description
				string desc = TileInfoReflection.GetLocalizedText(model, "description");
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				// Product info from displayProduct
				var modelType = model.GetType();
				var displayProductField = modelType.GetField("displayProduct", GameReflection.PublicInstance);
				if (displayProductField != null) {
					var displayProduct = displayProductField.GetValue(model);
					if (displayProduct != null) {
						var goodField = displayProduct.GetType().GetField("good", GameReflection.PublicInstance);
						if (goodField != null) {
							var good = goodField.GetValue(displayProduct);
							if (good != null) {
								string productName = TileInfoReflection.GetLocalizedText(good, "displayName");
								if (!string.IsNullOrEmpty(productName)) {
									parts.Add(Strings.Get("util.tile_info.produces", productName));
								}
							}
						}
					}
				}

				return parts.Count > 0 ? string.Join(", ", parts) : null;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetOreInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for a spring (water source).
		/// Includes: charges, description.
		/// </summary>
		private static string GetSpringInfo(object spring) {
			try {
				var springType = spring.GetType();

				// Get Model property
				var modelProp = springType.GetProperty("Model");
				if (modelProp == null) return null;

				var model = modelProp.GetValue(spring);
				if (model == null) return null;

				// Get State property
				var stateProp = springType.GetProperty("State");
				var state = stateProp?.GetValue(spring);

				var parts = new List<string>();

				// Get charges from state
				if (state != null) {
					var stateType = state.GetType();
					var chargesLeftField = stateType.GetField("chargesLeft", GameReflection.PublicInstance);
					var maxChargesField = stateType.GetField("maxCharges", GameReflection.PublicInstance);

					int chargesLeft = ReflectionHelper.GetInt(chargesLeftField, state);
					int maxCharges = ReflectionHelper.GetInt(maxChargesField, state);

					if (maxCharges > 0) {
						parts.Add(Strings.Get("util.tile_info.charges", chargesLeft, maxCharges));
					}
				}

				// Description
				string desc = TileInfoReflection.GetLocalizedText(model, "description");
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				return parts.Count > 0 ? string.Join(", ", parts) : null;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetSpringInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get info for a lake (fishing spot).
		/// Includes: charges, description, products.
		/// </summary>
		private static string GetLakeInfo(object lake) {
			try {
				var lakeType = lake.GetType();

				// Get Model property
				var modelProp = lakeType.GetProperty("Model");
				if (modelProp == null) return null;

				var model = modelProp.GetValue(lake);
				if (model == null) return null;

				// Get State property
				var stateProp = lakeType.GetProperty("State");
				var state = stateProp?.GetValue(lake);

				var parts = new List<string>();

				// Get charges from state
				if (state != null) {
					var stateType = state.GetType();
					var chargesLeftField = stateType.GetField("chargesLeft", GameReflection.PublicInstance);
					var maxChargesField = stateType.GetField("maxCharges", GameReflection.PublicInstance);

					int chargesLeft = ReflectionHelper.GetInt(chargesLeftField, state);
					int maxCharges = ReflectionHelper.GetInt(maxChargesField, state);

					if (maxCharges > 0) {
						parts.Add(Strings.Get("util.tile_info.charges", chargesLeft, maxCharges));
					}

					// Get stored fish waiting for pickup
					var goodsField = stateType.GetField("goods", GameReflection.PublicInstance);
					if (goodsField != null) {
						var goods = goodsField.GetValue(state);
						if (goods != null) {
							// GoodsContainer has a Sum() method
							var sumMethod = goods.GetType().GetMethod("Sum", GameReflection.PublicInstance, null, Type.EmptyTypes, null);
							if (sumMethod != null) {
								int storedFish = (int)sumMethod.Invoke(goods, null);
								if (storedFish > 0) {
									parts.Add(Strings.Get("util.tile_info.stored_fish", storedFish));
								}
							}
						}
					}
				}

				// Priority (only if non-default)
				int lakePrio = ConstructionReflection.GetResourceNodePriority(lake);
				if (lakePrio != 0) {
					parts.Add(Strings.Get("util.tile_info.priority", FormatNodePriority(lakePrio)));
				}

				// Description - LakeModel has a Description property that includes grade requirement
				string desc = TileInfoReflection.GetDescriptionProperty(model);
				if (!string.IsNullOrEmpty(desc)) {
					parts.Add(desc);
				}

				// Product info from production field
				string productInfo = GetMainProductInfo(model);
				if (!string.IsNullOrEmpty(productInfo)) {
					parts.Add(Strings.Get("util.tile_info.produces", productInfo));
				}

				return parts.Count > 0 ? string.Join(", ", parts) : null;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetLakeInfo failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Generic fallback for unknown object types.
		/// </summary>
		private static string GetGenericObjectInfo(object obj) {
			try {
				// Try Model.displayName first
				var modelProp = obj.GetType().GetProperty("Model");
				if (modelProp != null) {
					var model = modelProp.GetValue(obj);
					if (model != null) {
						string name = TileInfoReflection.GetLocalizedText(model, "displayName");
						if (!string.IsNullOrEmpty(name)) {
							return name;
						}
					}
				}

				// Fallback to type name
				return obj.GetType().Name;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGenericObjectInfo failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// PRODUCT INFO HELPERS
		// ========================================

		/// <summary>
		/// Get main product info: "ProductName (amount)"
		/// Path: Model.production.good.displayName.Text
		/// </summary>
		private static string GetMainProductInfo(object model) {
			try {
				var productionField = TileInfoReflection.ProductionField ?? model.GetType().GetField("production", GameReflection.PublicInstance);
				if (productionField == null) return null;

				var production = productionField.GetValue(model);
				if (production == null) return null;

				// Get good
				var goodField = GameReflection.GoodRefGoodField ?? production.GetType().GetField("good", GameReflection.PublicInstance);
				if (goodField == null) return null;

				var good = goodField.GetValue(production);
				if (good == null) return null;

				// Get displayName
				var displayNameField = TileInfoReflection.GoodDisplayNameField ?? good.GetType().GetField("displayName", GameReflection.PublicInstance);
				if (displayNameField == null) return null;

				var displayName = displayNameField.GetValue(good);
				string productName = GameReflection.GetLocaText(displayName);

				// Get amount
				var amountField = GameReflection.GoodRefAmountField ?? production.GetType().GetField("amount", GameReflection.PublicInstance);
				int amount = ReflectionHelper.GetInt(amountField, production);

				if (!string.IsNullOrEmpty(productName)) {
					return amount > 1 ? Strings.Get("util.tile_info.product_with_amount", amount, productName) : Strings.Get("util.tile_info.product_no_amount", productName);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetMainProductInfo failed: {ex.Message}"); }

			return null;
		}

		/// <summary>
		/// Get extra products info: "Product1 X%, Product2 Y%"
		/// Path: Model.extraProduction[] array of GoodRefChance
		/// </summary>
		private static string GetExtraProductsInfo(object model) {
			try {
				var extraProductionField = TileInfoReflection.ExtraProductionField ?? model.GetType().GetField("extraProduction", GameReflection.PublicInstance);
				if (extraProductionField == null) return null;

				var extraProduction = extraProductionField.GetValue(model) as Array;
				if (extraProduction == null || extraProduction.Length == 0) return null;

				var parts = new List<string>();

				foreach (var item in extraProduction) {
					if (item == null) continue;

					// Get DisplayName (using cached property if available)
					var displayNameProp = TileInfoReflection.GoodRefChanceDisplayNameProp ?? item.GetType().GetProperty("DisplayName");
					string productName = displayNameProp != null ? displayNameProp.GetValue(item) as string : null;

					// Get chance (using cached field if available)
					var chanceField = TileInfoReflection.GoodRefChanceField ?? item.GetType().GetField("chance", GameReflection.PublicInstance);
					float chance = ReflectionHelper.GetFloat(chanceField, item);

					if (!string.IsNullOrEmpty(productName) && chance > 0) {
						int percent = Mathf.RoundToInt(chance * 100f);
						parts.Add(Strings.Get("util.tile_info.extra_product", productName, percent));
					}
				}

				return parts.Count > 0 ? string.Join(", ", parts) : null;
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetExtraProductsInfo failed: {ex.Message}"); }

			return null;
		}

		// ========================================
		// SOURCE BUILDING HELPERS
		// ========================================

		/// <summary>
		/// Get camps that can harvest a natural resource.
		/// Path: ResourcesService.CampsMatrix[Model.RefGoodName]
		/// </summary>
		private static string GetCampsForResource(object resourceModel, PropertyInfo refGoodNameProp) {
			try {
				// Get RefGoodName from model (use passed-in cached property)
				string refGoodName = ReflectionHelper.GetPropString(refGoodNameProp, resourceModel);
				if (string.IsNullOrEmpty(refGoodName)) return null;

				// Get ResourcesService
				var resourcesService = GameReflection.GetResourcesService();
				if (resourcesService == null) return null;

				// Initialize service cache
				TileInfoReflection.EnsureServiceCache(resourcesService, null);

				// Get CampsMatrix dictionary
				var campsMatrixProp = TileInfoReflection.CampsMatrixProp ?? resourcesService.GetType().GetProperty("CampsMatrix", GameReflection.PublicInstance);
				if (campsMatrixProp == null) return null;

				var campsMatrix = campsMatrixProp.GetValue(resourcesService);

				// Use consolidated helper (string key doesn't need ContainsKey check)
				return GetSourceBuildingNames(campsMatrix, refGoodName, false);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCampsForResource failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get gatherer huts that can work a resource deposit.
		/// Path: DepositsService.HutsMatrix[Model]
		/// </summary>
		private static string GetHutsForDeposit(object depositModel) {
			try {
				// Get DepositsService
				var depositsService = GameReflection.GetDepositsService();
				if (depositsService == null) return null;

				// Initialize service cache
				TileInfoReflection.EnsureServiceCache(null, depositsService);

				// Get HutsMatrix dictionary
				var hutsMatrixProp = TileInfoReflection.HutsMatrixProp ?? depositsService.GetType().GetProperty("HutsMatrix", GameReflection.PublicInstance);
				if (hutsMatrixProp == null) return null;

				var hutsMatrix = hutsMatrixProp.GetValue(depositsService);

				// Use consolidated helper (object key needs ContainsKey check)
				return GetSourceBuildingNames(hutsMatrix, depositModel, true);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHutsForDeposit failed: {ex.Message}");
				return null;
			}
		}

		// ========================================
		// UTILITY METHODS
		// ========================================

		/// <summary>
		/// Check if a type inherits from a class with the given name anywhere in its hierarchy.
		/// </summary>
		private static bool InheritsFrom(Type type, string ancestorName) {
			Type current = type;
			while (current != null) {
				if (current.Name == ancestorName)
					return true;
				current = current.BaseType;
			}
			return false;
		}
		private static string FormatNodePriority(int priority) {
			if (priority == -5) return Strings.Get("common.priority_lowest");
			if (priority == 5) return Strings.Get("common.priority_highest");
			if (priority == 0) return Strings.Get("common.priority_default");
			return priority.ToString();
		}
	}
}
