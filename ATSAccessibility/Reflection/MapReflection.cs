using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to map objects: fields, glades, relics, villagers,
	/// and service dictionary properties. Used by MapNavigator and MapScanner.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - Lazy-caches from first encountered runtime object where assembly type name is unknown
	/// </summary>
	public static class MapReflection {
		// ========================================
		// FIELD (tile) PROPERTIES
		// ========================================

		private static PropertyInfo _fieldTypeProperty;
		private static PropertyInfo _fieldIsTraversableProperty;
		private static bool _fieldCached;

		private static void EnsureFieldCached(object field) {
			if (_fieldCached || field == null) return;
			_fieldCached = true;

			try {
				var fieldType = field.GetType();
				_fieldTypeProperty = fieldType.GetProperty("Type");
				_fieldIsTraversableProperty = fieldType.GetProperty("IsTraversable");
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] MapReflection: EnsureFieldCached failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get the raw type name of a field (e.g., "Grass", "Sand").
		/// Returns displayName if available, falls back to name, then ToString.
		/// </summary>
		public static string GetFieldTypeName(object field) {
			if (field == null) return null;
			EnsureFieldCached(field);
			if (_fieldTypeProperty == null) return null;

			try {
				var typeValue = _fieldTypeProperty.GetValue(field);
				if (typeValue == null) return null;

				var typeType = typeValue.GetType();

				var displayNameProp = typeType.GetProperty("displayName");
				if (displayNameProp != null) {
					var displayName = displayNameProp.GetValue(typeValue);
					if (displayName != null) {
						string text = displayName.ToString();
						if (!string.IsNullOrEmpty(text)) return text;
					}
				}

				var nameProp = typeType.GetProperty("name");
				if (nameProp != null) {
					var name = nameProp.GetValue(typeValue);
					if (name != null) return name.ToString();
				}

				return typeValue.ToString();
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get whether a field tile is traversable.
		/// </summary>
		public static bool GetFieldIsTraversable(object field) {
			if (field == null) return true;
			EnsureFieldCached(field);
			if (_fieldIsTraversableProperty == null) return true;

			try {
				return (bool)_fieldIsTraversableProperty.GetValue(field);
			} catch {
				return true;
			}
		}

		// ========================================
		// GLADE FIELDS
		// ========================================

		private static FieldInfo _gladeWasDiscoveredField;
		private static FieldInfo _gladeDangerLevelField;
		private static FieldInfo _gladeFieldsField;
		private static FieldInfo _gladeHasRewardChaseField;
		private static FieldInfo _gladeRewardChaseEndField;
		private static FieldInfo _gladeRelicsField;
		private static bool _gladeCached;

		private static void EnsureGladeCached(object glade) {
			if (_gladeCached || glade == null) return;
			_gladeCached = true;

			try {
				var gladeType = glade.GetType();
				_gladeWasDiscoveredField = gladeType.GetField("wasDiscovered", GameReflection.PublicInstance);
				_gladeDangerLevelField = gladeType.GetField("dangerLevel", GameReflection.PublicInstance);
				_gladeFieldsField = gladeType.GetField("fields", GameReflection.PublicInstance);
				_gladeHasRewardChaseField = gladeType.GetField("hasRewardChase", GameReflection.PublicInstance);
				_gladeRewardChaseEndField = gladeType.GetField("rewardChaseEnd", GameReflection.PublicInstance);
				_gladeRelicsField = gladeType.GetField("relics", GameReflection.PublicInstance);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] MapReflection: EnsureGladeCached failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Trigger glade field caching from a list of glades (finds first non-null instance).
		/// Call once before accessing glade fields in a scan loop.
		/// </summary>
		public static void EnsureGladeCachedFromList(object allGlades) {
			if (_gladeCached || allGlades == null) return;
			var gladesList = allGlades as IEnumerable;
			if (gladesList == null) return;

			foreach (var glade in gladesList) {
				if (glade != null) {
					EnsureGladeCached(glade);
					return;
				}
			}
		}

		public static bool GetGladeWasDiscovered(object glade) {
			EnsureGladeCached(glade);
			return ReflectionHelper.GetBool(_gladeWasDiscoveredField, glade);
		}

		/// <summary>
		/// Get the raw danger level enum string ("None", "Dangerous", "Forbidden").
		/// Consumers map to display names (e.g., "None" -> "Small").
		/// </summary>
		public static string GetGladeDangerLevelRaw(object glade) {
			EnsureGladeCached(glade);
			var val = ReflectionHelper.GetField(_gladeDangerLevelField, glade);
			return val?.ToString();
		}

		public static IList GetGladeFields(object glade) {
			EnsureGladeCached(glade);
			return ReflectionHelper.GetList(_gladeFieldsField, glade);
		}

		public static bool GetGladeHasRewardChase(object glade) {
			EnsureGladeCached(glade);
			return ReflectionHelper.GetBool(_gladeHasRewardChaseField, glade);
		}

		public static float GetGladeRewardChaseEnd(object glade) {
			EnsureGladeCached(glade);
			return ReflectionHelper.GetFloat(_gladeRewardChaseEndField, glade);
		}

		public static IList GetGladeRelics(object glade) {
			EnsureGladeCached(glade);
			return ReflectionHelper.GetList(_gladeRelicsField, glade);
		}

		/// <summary>
		/// Get the first field position from a glade.
		/// </summary>
		public static Vector2Int GetGladeFirstField(object glade) {
			var fields = GetGladeFields(glade);
			if (fields != null && fields.Count > 0) {
				try { return (Vector2Int)fields[0]; } catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetGladeFirstField failed: {ex.Message}"); }
			}
			return new Vector2Int(-1, -1);
		}

		// ========================================
		// GLADE RELIC FIELDS
		// ========================================

		private static FieldInfo _relicIsRewardChaseField;
		private static FieldInfo _relicNameField;
		private static FieldInfo _relicPositionField;
		private static bool _relicCached;

		/// <summary>
		/// Lazy-cache GladeRelicState fields from the first encountered relic instance.
		/// </summary>
		public static void EnsureRelicCached(object relic) {
			if (_relicCached || relic == null) return;
			_relicCached = true;

			try {
				var relicType = relic.GetType();
				_relicIsRewardChaseField = relicType.GetField("isRewardChase", GameReflection.PublicInstance);
				_relicNameField = relicType.GetField("name", GameReflection.PublicInstance);
				_relicPositionField = relicType.GetField("field", GameReflection.PublicInstance);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] MapReflection: EnsureRelicCached failed: {ex.Message}");
			}
		}

		public static bool IsRewardChaseRelic(object relic) {
			EnsureRelicCached(relic);
			return ReflectionHelper.GetBool(_relicIsRewardChaseField, relic);
		}

		public static string GetRelicName(object relic) {
			EnsureRelicCached(relic);
			return ReflectionHelper.GetString(_relicNameField, relic);
		}

		public static Vector2Int GetRelicPosition(object relic) {
			EnsureRelicCached(relic);
			var val = ReflectionHelper.GetField(_relicPositionField, relic);
			if (val is Vector2Int v) return v;
			return new Vector2Int(-1, -1);
		}

		// ========================================
		// VILLAGER PROPERTIES
		// ========================================

		private static PropertyInfo _villagerActorStateProperty;
		private static FieldInfo _actorStatePositionField;
		private static PropertyInfo _villagerRaceProperty;
		private static bool _villagerCached;

		private static void EnsureVillagerCached(object villager) {
			if (_villagerCached || villager == null) return;
			_villagerCached = true;

			try {
				var villagerType = villager.GetType();
				_villagerActorStateProperty = villagerType.GetProperty("ActorState");
				_villagerRaceProperty = villagerType.GetProperty("Race");

				if (_villagerActorStateProperty != null) {
					var actorState = _villagerActorStateProperty.GetValue(villager);
					if (actorState != null) {
						_actorStatePositionField = actorState.GetType().GetField("position", GameReflection.PublicInstance);
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] MapReflection: EnsureVillagerCached failed: {ex.Message}");
			}
		}

		public static Vector3 GetVillagerPosition(object villager) {
			EnsureVillagerCached(villager);
			if (_villagerActorStateProperty == null) return Vector3.zero;

			var actorState = ReflectionHelper.GetProp(_villagerActorStateProperty, villager);
			if (actorState == null || _actorStatePositionField == null) return Vector3.zero;

			try {
				return (Vector3)_actorStatePositionField.GetValue(actorState);
			} catch {
				return Vector3.zero;
			}
		}

		public static string GetVillagerRace(object villager) {
			EnsureVillagerCached(villager);
			var val = ReflectionHelper.GetProp(_villagerRaceProperty, villager);
			return val?.ToString();
		}

		// ========================================
		// SERVICE DICTIONARY PROPERTIES (lazy-cached from service instance type)
		// ========================================

		private static PropertyInfo _naturalResourcesProperty;
		private static PropertyInfo _depositsProperty;
		private static PropertyInfo _oresProperty;
		private static PropertyInfo _springsProperty;
		private static PropertyInfo _lakesProperty;
		private static PropertyInfo _buildingsProperty;

		public static IDictionary GetNaturalResources(object resourcesService) {
			if (resourcesService == null) return null;
			if (_naturalResourcesProperty == null)
				_naturalResourcesProperty = resourcesService.GetType().GetProperty("NaturalResources", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_naturalResourcesProperty, resourcesService) as IDictionary;
		}

		public static IDictionary GetDeposits(object depositsService) {
			if (depositsService == null) return null;
			if (_depositsProperty == null)
				_depositsProperty = depositsService.GetType().GetProperty("Deposits", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_depositsProperty, depositsService) as IDictionary;
		}

		public static IDictionary GetOres(object oreService) {
			if (oreService == null) return null;
			if (_oresProperty == null)
				_oresProperty = oreService.GetType().GetProperty("Ore", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_oresProperty, oreService) as IDictionary;
		}

		public static IDictionary GetSprings(object springsService) {
			if (springsService == null) return null;
			if (_springsProperty == null)
				_springsProperty = springsService.GetType().GetProperty("Springs", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_springsProperty, springsService) as IDictionary;
		}

		public static IDictionary GetLakes(object lakesService) {
			if (lakesService == null) return null;
			if (_lakesProperty == null)
				_lakesProperty = lakesService.GetType().GetProperty("Lakes", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_lakesProperty, lakesService) as IDictionary;
		}

		public static IDictionary GetBuildings(object buildingsService) {
			if (buildingsService == null) return null;
			if (_buildingsProperty == null)
				_buildingsProperty = buildingsService.GetType().GetProperty("Buildings", GameReflection.PublicInstance);
			return ReflectionHelper.GetProp(_buildingsProperty, buildingsService) as IDictionary;
		}

		// ========================================
		// DISPLAY NAME HELPERS
		// ========================================

		/// <summary>
		/// Get display name from an object via Model.displayName (or Model.name fallback).
		/// Works for NaturalResource, ResourceDeposit, Ore, Spring, Lake objects.
		/// </summary>
		public static string GetObjectDisplayName(object obj) {
			if (obj == null) return null;

			try {
				var objType = obj.GetType();

				var modelProperty = objType.GetProperty("Model", GameReflection.PublicInstance);
				if (modelProperty != null) {
					var model = modelProperty.GetValue(obj);
					if (model != null) {
						var modelType = model.GetType();

						var displayNameField = modelType.GetField("displayName", GameReflection.PublicInstance);
						if (displayNameField != null) {
							var displayName = displayNameField.GetValue(model);
							if (displayName != null) {
								string text = displayName.ToString();
								if (!string.IsNullOrEmpty(text)) return text;
							}
						}

						var nameProp = modelType.GetProperty("name", GameReflection.PublicInstance);
						if (nameProp != null) {
							var name = nameProp.GetValue(model);
							if (name != null) return Speech.CleanResourceName(name.ToString());
						}
					}
				}

				return objType.Name;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] MapReflection: GetObjectDisplayName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get display name from a building via BuildingModel.displayName (then Model fallback).
		/// </summary>
		public static string GetBuildingDisplayName(object building) {
			if (building == null) return null;

			try {
				var buildingType = building.GetType();

				// Try BuildingModel property first
				var buildingModelProp = buildingType.GetProperty("BuildingModel", GameReflection.PublicInstance);
				if (buildingModelProp != null) {
					var buildingModel = buildingModelProp.GetValue(building);
					if (buildingModel != null) {
						var displayNameField = buildingModel.GetType().GetField("displayName", GameReflection.PublicInstance);
						if (displayNameField != null) {
							var displayName = displayNameField.GetValue(buildingModel);
							if (displayName != null) {
								string text = displayName.ToString();
								if (!string.IsNullOrEmpty(text)) return text;
							}
						}
					}
				}

				// Fallback to Model property
				return GetObjectDisplayName(building);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get building position from its Field property.
		/// </summary>
		public static Vector2Int GetBuildingPosition(object building) {
			if (building == null) return new Vector2Int(-1, -1);

			try {
				var fieldProp = building.GetType().GetProperty("Field", GameReflection.PublicInstance);
				if (fieldProp != null) {
					return (Vector2Int)fieldProp.GetValue(building);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetBuildingPosition failed: {ex.Message}");
			}

			return new Vector2Int(-1, -1);
		}

		/// <summary>
		/// Get the ResourceSize type string from a deposit or lake state object.
		/// Returns "Small", "Large", or "Gigantic" (or null on failure).
		/// </summary>
		public static string GetResourceSizeType(object resourceState) {
			if (resourceState == null) return null;

			try {
				var modelProp = resourceState.GetType().GetProperty("Model", GameReflection.PublicInstance);
				if (modelProp == null) return null;

				var model = modelProp.GetValue(resourceState);
				if (model == null) return null;

				var typeField = model.GetType().GetField("type", GameReflection.PublicInstance);
				if (typeField == null) return null;

				return typeField.GetValue(model)?.ToString();
			} catch {
				return null;
			}
		}

		// ========================================
		// GLADE INFO STATE
		// ========================================

		// Cached reflection for glade info
		private static PropertyInfo _ssEffectsProperty = null;
		private static FieldInfo _effectsGladeInfoOwnersField = null;
		private static FieldInfo _effectsRevealedGrassLocationsField = null;
		private static FieldInfo _effectsRevealedSpringsLocationsField = null;
		private static FieldInfo _effectsRevealedRelicsLocationsField = null;
		private static FieldInfo _effectsDangerousGladeInfoBlocksField = null;
		private static bool _gladeInfoTypesCached = false;

		private static void EnsureGladeInfoTypes() {
			if (_gladeInfoTypesCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_gladeInfoTypesCached = true;
				return;
			}

			try {
				// Get Effects property from IStateService
				var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
				if (stateServiceType != null) {
					_ssEffectsProperty = stateServiceType.GetProperty("Effects",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get fields from EffectsState
				var effectsStateType = assembly.GetType("Eremite.Model.State.EffectsState");
				if (effectsStateType != null) {
					_effectsGladeInfoOwnersField = effectsStateType.GetField("gladeInfoOwners",
						BindingFlags.Public | BindingFlags.Instance);
					_effectsRevealedGrassLocationsField = effectsStateType.GetField("revealedGrassLocations",
						BindingFlags.Public | BindingFlags.Instance);
					_effectsRevealedSpringsLocationsField = effectsStateType.GetField("revealedSpringsLocations",
						BindingFlags.Public | BindingFlags.Instance);
					_effectsRevealedRelicsLocationsField = effectsStateType.GetField("revealedRelicsByTagLocations",
						BindingFlags.Public | BindingFlags.Instance);
					_effectsDangerousGladeInfoBlocksField = effectsStateType.GetField("dangerousGladeInfoBlocksOwners",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached glade info types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Glade info type caching failed: {ex.Message}");
			}

			_gladeInfoTypesCached = true;
		}

		/// <summary>
		/// Get EffectsState from StateService.
		/// Contains glade info owners and revealed locations.
		/// </summary>
		public static object GetEffectsState() {
			EnsureGladeInfoTypes();
			var stateService = GameReflection.GetStateService();
			if (stateService == null) return null;

			try {
				return _ssEffectsProperty?.GetValue(stateService);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if full glade info is active (gladeInfoOwners list is non-empty).
		/// When active, shows glade contents in scanner and map navigator.
		/// </summary>
		public static bool HasGladeInfo() {
			EnsureGladeInfoTypes();
			var effectsState = GetEffectsState();
			if (effectsState == null || _effectsGladeInfoOwnersField == null) return false;

			try {
				var gladeInfoOwners = _effectsGladeInfoOwnersField.GetValue(effectsState) as System.Collections.IList;
				return gladeInfoOwners != null && gladeInfoOwners.Count > 0;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if dangerous glade info is active (NOT blocked).
		/// In Cursed Royal Woodlands, this returns false and ALL glade markers are hidden.
		/// When DangerousGladeInfo is false, players cannot distinguish between Small, Dangerous, or Forbidden glades.
		/// </summary>
		public static bool HasDangerousGladeInfo() {
			EnsureGladeInfoTypes();
			var effectsState = GetEffectsState();
			if (effectsState == null || _effectsDangerousGladeInfoBlocksField == null) return true;

			try {
				var blockOwners = _effectsDangerousGladeInfoBlocksField.GetValue(effectsState) as System.Collections.IList;
				// DangerousGladeInfo is true when NO blocks exist (count == 0)
				return blockOwners == null || blockOwners.Count == 0;
			} catch {
				return true;  // Default to showing info
			}
		}

		/// <summary>
		/// Get revealed grass locations (from Human's locate fertile soil ability).
		/// </summary>
		public static List<Vector2Int> GetRevealedGrassLocations() {
			EnsureGladeInfoTypes();
			var effectsState = GetEffectsState();
			if (effectsState == null || _effectsRevealedGrassLocationsField == null) return null;

			try {
				return _effectsRevealedGrassLocationsField.GetValue(effectsState) as List<Vector2Int>;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get revealed springs locations (from locate spring ability).
		/// </summary>
		public static List<Vector2Int> GetRevealedSpringsLocations() {
			EnsureGladeInfoTypes();
			var effectsState = GetEffectsState();
			if (effectsState == null || _effectsRevealedSpringsLocationsField == null) return null;

			try {
				return _effectsRevealedSpringsLocationsField.GetValue(effectsState) as List<Vector2Int>;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get revealed relic locations (from dig site/archaeology abilities).
		/// </summary>
		public static List<Vector2Int> GetRevealedRelicLocations() {
			EnsureGladeInfoTypes();
			var effectsState = GetEffectsState();
			if (effectsState == null || _effectsRevealedRelicsLocationsField == null) return null;

			try {
				return _effectsRevealedRelicsLocationsField.GetValue(effectsState) as List<Vector2Int>;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if a position has a location marker.
		/// Returns "grass marker", "spring marker", or "relic marker" if found, null otherwise.
		/// </summary>
		public static string GetLocationMarkerType(int x, int y) {
			var pos = new Vector2Int(x, y);

			var grassLocations = GetRevealedGrassLocations();
			if (grassLocations != null && grassLocations.Contains(pos))
				return "grass marker";

			var springsLocations = GetRevealedSpringsLocations();
			if (springsLocations != null && springsLocations.Contains(pos))
				return "spring marker";

			var relicLocations = GetRevealedRelicLocations();
			if (relicLocations != null && relicLocations.Contains(pos))
				return "relic marker";

			return null;
		}

		// Cached reflection for glade contents
		private static FieldInfo _gladeContentsDepositsField = null;
		private static FieldInfo _gladeContentsRelicsField = null;
		private static FieldInfo _gladeContentsBuildingsField = null;
		private static FieldInfo _gladeContentsSpringsField = null;
		private static FieldInfo _gladeContentsLakesField = null;
		private static FieldInfo _gladeContentsOreField = null;
		private static bool _gladeContentsFieldsCached = false;

		private static void EnsureGladeContentsFields(object glade) {
			if (_gladeContentsFieldsCached || glade == null) return;

			try {
				var gladeType = glade.GetType();
				_gladeContentsDepositsField = gladeType.GetField("deposits", BindingFlags.Public | BindingFlags.Instance);
				_gladeContentsRelicsField = gladeType.GetField("relics", BindingFlags.Public | BindingFlags.Instance);
				_gladeContentsBuildingsField = gladeType.GetField("buildings", BindingFlags.Public | BindingFlags.Instance);
				_gladeContentsSpringsField = gladeType.GetField("springs", BindingFlags.Public | BindingFlags.Instance);
				_gladeContentsLakesField = gladeType.GetField("lakes", BindingFlags.Public | BindingFlags.Instance);
				_gladeContentsOreField = gladeType.GetField("ore", BindingFlags.Public | BindingFlags.Instance);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureGladeContentsFields failed: {ex.Message}");
			}

			_gladeContentsFieldsCached = true;
		}

		/// <summary>
		/// Get a formatted summary of glade contents (e.g., "2 deposits, 1 relic").
		/// Shared by MapScanner and MapNavigator for consistent formatting.
		/// </summary>
		public static string GetGladeContentsSummary(object glade) {
			if (glade == null) return null;

			EnsureGladeContentsFields(glade);

			var parts = new List<string>();

			try {
				// Deposits
				var deposits = _gladeContentsDepositsField?.GetValue(glade) as System.Collections.IList;
				if (deposits != null && deposits.Count > 0)
					parts.Add($"{deposits.Count} deposit{(deposits.Count > 1 ? "s" : "")}");

				// Relics
				var relics = _gladeContentsRelicsField?.GetValue(glade) as System.Collections.IList;
				if (relics != null && relics.Count > 0)
					parts.Add($"{relics.Count} relic{(relics.Count > 1 ? "s" : "")}");

				// Buildings (abandoned structures)
				var buildings = _gladeContentsBuildingsField?.GetValue(glade) as System.Collections.IList;
				if (buildings != null && buildings.Count > 0)
					parts.Add($"{buildings.Count} building{(buildings.Count > 1 ? "s" : "")}");

				// Springs
				var springs = _gladeContentsSpringsField?.GetValue(glade) as System.Collections.IList;
				if (springs != null && springs.Count > 0)
					parts.Add($"{springs.Count} spring{(springs.Count > 1 ? "s" : "")}");

				// Lakes
				var lakes = _gladeContentsLakesField?.GetValue(glade) as System.Collections.IList;
				if (lakes != null && lakes.Count > 0)
					parts.Add($"{lakes.Count} lake{(lakes.Count > 1 ? "s" : "")}");

				// Ore
				var ore = _gladeContentsOreField?.GetValue(glade) as System.Collections.IList;
				if (ore != null && ore.Count > 0)
					parts.Add($"{ore.Count} ore");
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetGladeContentsSummary failed: {ex.Message}");
			}

			return parts.Count > 0 ? string.Join(", ", parts) : "empty";
		}

		// ========================================
		// LOCATION MARKER EVENT SUBSCRIPTION
		// ========================================

		// Cached reflection for location events
		private static PropertyInfo _esOnGrassLocationRequestedProperty = null;
		private static PropertyInfo _esOnSpringsLocationRequestedProperty = null;
		private static PropertyInfo _esOnRelicLocationRequestedProperty = null;
		private static bool _locationEventTypesCached = false;

		private static void EnsureLocationEventTypes() {
			if (_locationEventTypesCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_locationEventTypesCached = true;
				return;
			}

			try {
				// Get event properties from IEffectsService
				var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_esOnGrassLocationRequestedProperty = effectsServiceType.GetProperty("OnGrassLocationRequested",
						BindingFlags.Public | BindingFlags.Instance);
					_esOnSpringsLocationRequestedProperty = effectsServiceType.GetProperty("OnSpringsLocationRequested",
						BindingFlags.Public | BindingFlags.Instance);
					_esOnRelicLocationRequestedProperty = effectsServiceType.GetProperty("OnRelicLocationRequested",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached location event types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Location event type caching failed: {ex.Message}");
			}

			_locationEventTypesCached = true;
		}

		/// <summary>
		/// Subscribe to grass location revealed event (from Human's locate fertile soil ability).
		/// </summary>
		public static IDisposable SubscribeToGrassLocationRequested(Action callback) {
			EnsureLocationEventTypes();
			var effectsService = GameReflection.GetEffectsService();
			if (effectsService == null || _esOnGrassLocationRequestedProperty == null) return null;

			try {
				var observable = _esOnGrassLocationRequestedProperty.GetValue(effectsService);
				if (observable != null) {
					return GameReflection.SubscribeToObservable(observable, _ => callback());
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SubscribeToGrassLocationRequested failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Subscribe to springs location revealed event.
		/// </summary>
		public static IDisposable SubscribeToSpringsLocationRequested(Action callback) {
			EnsureLocationEventTypes();
			var effectsService = GameReflection.GetEffectsService();
			if (effectsService == null || _esOnSpringsLocationRequestedProperty == null) return null;

			try {
				var observable = _esOnSpringsLocationRequestedProperty.GetValue(effectsService);
				if (observable != null) {
					return GameReflection.SubscribeToObservable(observable, _ => callback());
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SubscribeToSpringsLocationRequested failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Subscribe to relic location revealed event (dig site/archaeology abilities).
		/// </summary>
		public static IDisposable SubscribeToRelicLocationRequested(Action callback) {
			EnsureLocationEventTypes();
			var effectsService = GameReflection.GetEffectsService();
			if (effectsService == null || _esOnRelicLocationRequestedProperty == null) return null;

			try {
				var observable = _esOnRelicLocationRequestedProperty.GetValue(effectsService);
				if (observable != null) {
					return GameReflection.SubscribeToObservable(observable, _ => callback());
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SubscribeToRelicLocationRequested failed: {ex.Message}");
			}

			return null;
		}

		// ========================================
		// RELICS HIGHLIGHT SYSTEM (Short Range Scanner)
		// ========================================

		// Cached reflection for RelicsService
		private static PropertyInfo _gsRelicsServiceProperty = null;
		private static PropertyInfo _rsOnRelicsHighlightRequestedProperty = null;
		private static MethodInfo _rsFindRelicForMethod = null;
		private static bool _relicsHighlightTypesCached = false;

		// Track highlighted relics (position -> relic name)
		private static Dictionary<Vector2Int, string> _highlightedRelics = new Dictionary<Vector2Int, string>();

		private static void EnsureRelicsHighlightTypes() {
			if (_relicsHighlightTypesCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_relicsHighlightTypesCached = true;
				return;
			}

			try {
				// Get RelicsService from IGameServices
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsRelicsServiceProperty = gameServicesType.GetProperty("RelicsService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Get OnRelicsHighlightRequested and FindRelicFor from IRelicsService
				var relicsServiceType = assembly.GetType("Eremite.Services.IRelicsService");
				if (relicsServiceType != null) {
					_rsOnRelicsHighlightRequestedProperty = relicsServiceType.GetProperty("OnRelicsHighlightRequested",
						BindingFlags.Public | BindingFlags.Instance);
					_rsFindRelicForMethod = relicsServiceType.GetMethod("FindRelicFor",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached RelicsService highlight types");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] RelicsService type caching failed: {ex.Message}");
			}

			_relicsHighlightTypesCached = true;
		}

		/// <summary>
		/// Get RelicsService from GameServices.
		/// </summary>
		public static object GetRelicsService() {
			EnsureRelicsHighlightTypes();
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return null;

			try {
				return _gsRelicsServiceProperty?.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Subscribe to relic highlight events (from Short Range Scanner, etc).
		/// Callback receives the relic name and position when a relic is highlighted.
		/// </summary>
		public static IDisposable SubscribeToRelicsHighlightRequested(Action<string, Vector2Int> callback) {
			EnsureRelicsHighlightTypes();
			var relicsService = GetRelicsService();
			if (relicsService == null || _rsOnRelicsHighlightRequestedProperty == null) return null;

			try {
				var observable = _rsOnRelicsHighlightRequestedProperty.GetValue(relicsService);
				if (observable != null) {
					// Subscribe with a handler that extracts the relic info
					return GameReflection.SubscribeToObservable(observable, request => {
						try {
							// Call FindRelicFor to get the actual relic state
							if (_rsFindRelicForMethod != null) {
								var relicState = _rsFindRelicForMethod.Invoke(relicsService, new[] { request });
								if (relicState != null) {
									// Get the relic's name and position
									var nameField = relicState.GetType().GetField("name",
										BindingFlags.Public | BindingFlags.Instance);
									var fieldField = relicState.GetType().GetField("field",
										BindingFlags.Public | BindingFlags.Instance);

									string name = nameField?.GetValue(relicState) as string ?? "Unknown";
									var field = fieldField?.GetValue(relicState);

									if (field != null) {
										// field is a Vector2Int
										var pos = (Vector2Int)field;
										_highlightedRelics[pos] = name;
										callback(name, pos);
									}
								}
							}
						} catch (Exception ex) {
							Debug.LogError($"[ATSAccessibility] RelicsHighlightRequested handler failed: {ex.Message}");
						}
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SubscribeToRelicsHighlightRequested failed: {ex.Message}");
			}

			return null;
		}

		/// <summary>
		/// Get all currently highlighted relic positions.
		/// </summary>
		public static Dictionary<Vector2Int, string> GetHighlightedRelics() {
			return _highlightedRelics;
		}

		/// <summary>
		/// Check if a position has a highlighted relic.
		/// Returns the relic name if found, null otherwise.
		/// </summary>
		public static string GetHighlightedRelicAt(int x, int y) {
			var pos = new Vector2Int(x, y);
			if (_highlightedRelics.TryGetValue(pos, out string name))
				return name;
			return null;
		}

		/// <summary>
		/// Clear highlighted relics (call when game ends or new game starts).
		/// </summary>
		public static void ClearHighlightedRelics() {
			_highlightedRelics.Clear();
		}

		// Cached reflection for NaturalResource marked state
		private static PropertyInfo _naturalResourceStateProperty = null;
		private static FieldInfo _nrsIsMarkedField = null;
		private static bool _naturalResourceMarkedCached = false;

		private static void EnsureNaturalResourceMarkedCache(object resource) {
			if (_naturalResourceMarkedCached) return;
			_naturalResourceMarkedCached = true;

			try {
				var resourceType = resource.GetType();
				_naturalResourceStateProperty = resourceType.GetProperty("State",
					BindingFlags.Public | BindingFlags.Instance);
				if (_naturalResourceStateProperty != null) {
					var stateType = _naturalResourceStateProperty.PropertyType;
					_nrsIsMarkedField = stateType.GetField("isMarked",
						BindingFlags.Public | BindingFlags.Instance);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureNaturalResourceMarkedCache failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Check if a NaturalResource is marked for woodcutting/harvesting.
		/// </summary>
		public static bool IsNaturalResourceMarked(object resource) {
			if (resource == null) return false;
			EnsureNaturalResourceMarkedCache(resource);
			if (_naturalResourceStateProperty == null || _nrsIsMarkedField == null) return false;
			try {
				var state = _naturalResourceStateProperty.GetValue(resource);
				if (state == null) return false;
				return (bool)_nrsIsMarkedField.GetValue(state);
			} catch {
				return false;
			}
		}

		// ========================================
		// HARVEST MARK/UNMARK REFLECTION
		// ========================================

		private static MethodInfo _naturalResourceMarkMethod = null;
		private static MethodInfo _naturalResourceUnmarkMethod = null;
		private static FieldInfo _nrsIsGladeEdgeField = null;
		private static PropertyInfo _resourcesNaturalResourcesProperty = null;
		private static bool _harvestReflectionCached = false;

		private static void EnsureHarvestReflectionCache(object resource) {
			if (_harvestReflectionCached) return;
			_harvestReflectionCached = true;

			try {
				var resourceType = resource.GetType();
				_naturalResourceMarkMethod = resourceType.GetMethod("Mark", GameReflection.PublicInstance);
				_naturalResourceUnmarkMethod = resourceType.GetMethod("Unmark", GameReflection.PublicInstance);

				// Get isGladeEdge from State type (already cached _naturalResourceStateProperty)
				EnsureNaturalResourceMarkedCache(resource);
				if (_naturalResourceStateProperty != null) {
					var stateType = _naturalResourceStateProperty.PropertyType;
					_nrsIsGladeEdgeField = stateType.GetField("isGladeEdge", GameReflection.PublicInstance);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureHarvestReflectionCache failed: {ex.Message}");
			}
		}

		private static void EnsureResourcesNaturalResourcesProperty(object resourcesService) {
			if (_resourcesNaturalResourcesProperty != null) return;

			try {
				_resourcesNaturalResourcesProperty = resourcesService.GetType().GetProperty("NaturalResources",
					GameReflection.PublicInstance);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureResourcesNaturalResourcesProperty failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get the NaturalResource object at a map position.
		/// Returns null if no resource at that position.
		/// </summary>
		public static object GetNaturalResourceAt(Vector2Int pos) {
			var resourcesService = GameReflection.GetResourcesService();
			if (resourcesService == null) return null;

			EnsureResourcesNaturalResourcesProperty(resourcesService);
			if (_resourcesNaturalResourcesProperty == null) return null;

			try {
				var dict = _resourcesNaturalResourcesProperty.GetValue(resourcesService) as IDictionary;
				if (dict == null) return null;
				if (dict.Contains(pos))
					return dict[pos];
				return null;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if there is a NaturalResource at a map position.
		/// </summary>
		public static bool HasNaturalResourceAt(Vector2Int pos) {
			return GetNaturalResourceAt(pos) != null;
		}

		/// <summary>
		/// Mark a NaturalResource at the given position.
		/// Returns true if successfully marked.
		/// </summary>
		public static bool MarkNaturalResourceAt(Vector2Int pos) {
			var resource = GetNaturalResourceAt(pos);
			if (resource == null) return false;

			EnsureHarvestReflectionCache(resource);
			if (_naturalResourceMarkMethod == null) return false;

			try {
				_naturalResourceMarkMethod.Invoke(resource, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Unmark a NaturalResource at the given position.
		/// Returns true if successfully unmarked.
		/// </summary>
		public static bool UnmarkNaturalResourceAt(Vector2Int pos) {
			var resource = GetNaturalResourceAt(pos);
			if (resource == null) return false;

			EnsureHarvestReflectionCache(resource);
			if (_naturalResourceUnmarkMethod == null) return false;

			try {
				_naturalResourceUnmarkMethod.Invoke(resource, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if a NaturalResource at the given position is on a glade edge.
		/// </summary>
		public static bool IsNaturalResourceGladeEdge(Vector2Int pos) {
			var resource = GetNaturalResourceAt(pos);
			if (resource == null) return false;

			EnsureNaturalResourceMarkedCache(resource);
			EnsureHarvestReflectionCache(resource);
			if (_naturalResourceStateProperty == null || _nrsIsGladeEdgeField == null) return false;

			try {
				var state = _naturalResourceStateProperty.GetValue(resource);
				if (state == null) return false;
				return (bool)_nrsIsGladeEdgeField.GetValue(state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get all NaturalResource positions on the map.
		/// Returns empty list if not in game.
		/// </summary>
		public static List<Vector2Int> GetAllNaturalResourcePositions() {
			var result = new List<Vector2Int>();
			var resourcesService = GameReflection.GetResourcesService();
			if (resourcesService == null) return result;

			EnsureResourcesNaturalResourcesProperty(resourcesService);
			if (_resourcesNaturalResourcesProperty == null) return result;

			try {
				var dict = _resourcesNaturalResourcesProperty.GetValue(resourcesService) as IDictionary;
				if (dict == null) return result;

				foreach (DictionaryEntry entry in dict) {
					result.Add((Vector2Int)entry.Key);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetAllNaturalResourcePositions failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// FARM RANGE HELPERS
		// ========================================

		private static FieldInfo _farmModelWorkAreaField = null;
		private static bool _farmModelFieldsCached = false;

		private static void EnsureFarmModelFields() {
			if (_farmModelFieldsCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_farmModelFieldsCached = true;
				return;
			}

			try {
				var farmModelType = assembly.GetType("Eremite.Buildings.FarmModel");
				if (farmModelType != null) {
					_farmModelWorkAreaField = farmModelType.GetField("workArea", GameReflection.PublicInstance);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureFarmModelFields failed: {ex.Message}");
			}

			_farmModelFieldsCached = true;
		}

		/// <summary>
		/// Get the work area Vector2Int from a FarmModel.
		/// </summary>
		public static Vector2Int GetFarmModelWorkArea(object farmModel) {
			if (farmModel == null) return Vector2Int.zero;

			EnsureFarmModelFields();

			try {
				if (_farmModelWorkAreaField != null) {
					return (Vector2Int)_farmModelWorkAreaField.GetValue(farmModel);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetFarmModelWorkArea failed: {ex.Message}");
			}

			return Vector2Int.zero;
		}

		/// <summary>
		/// Check if a Field is of type Grass (fertile soil).
		/// </summary>
		public static bool IsFieldGrass(object field) {
			if (field == null) return false;

			try {
				var typeProp = field.GetType().GetProperty("Type", GameReflection.PublicInstance);
				if (typeProp != null) {
					var fieldType = typeProp.GetValue(field);
					// FieldType.Grass has value 1 in the enum
					return fieldType != null && fieldType.ToString() == "Grass";
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] IsFieldGrass failed: {ex.Message}");
			}

			return false;
		}

		/// <summary>
		/// Check if a position is inside an unrevealed glade.
		/// </summary>
		public static bool IsInUnrevealedGlade(int x, int y) {
			try {
				var glade = GameReflection.GetGlade(x, y);
				if (glade == null) return false;

				return !GetGladeWasDiscovered(glade);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] IsInUnrevealedGlade failed: {ex.Message}");
			}

			return false;
		}

		// ========================================
		// SEAL / GUIDEPOST SUPPORT
		// ========================================

		private static PropertyInfo _gsGameSealServiceProperty = null;
		private static MethodInfo _gameSealServiceIsSealedBiomeMethod = null;
		private static MethodInfo _gameSealServiceGetSealFieldMethod = null;
		private static PropertyInfo _bsSealsProperty = null;
		private static PropertyInfo _biomeServiceDifficultyProperty = null;
		private static FieldInfo _difficultyInGameSealField = null;
		private static FieldInfo _sealModelSizeField = null;
		private static bool _sealTypesCached = false;

		private static void EnsureSealTypes() {
			if (_sealTypesCached) return;
			var assembly = GameReflection.GameAssembly;
			if (assembly == null) return;

			try {
				// Get GameSealService property from IGameServices
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGameSealServiceProperty = gameServicesType.GetProperty("GameSealService", GameReflection.PublicInstance);
				}

				// Get methods from IGameSealService
				var gameSealServiceType = assembly.GetType("Eremite.Services.IGameSealService");
				if (gameSealServiceType != null) {
					_gameSealServiceIsSealedBiomeMethod = gameSealServiceType.GetMethod("IsSealedBiome", GameReflection.PublicInstance);
					_gameSealServiceGetSealFieldMethod = gameSealServiceType.GetMethod("GetSealField", GameReflection.PublicInstance);
				}

				// Get Seals property from IBuildingsService
				var buildingsServiceType = assembly.GetType("Eremite.Services.IBuildingsService");
				if (buildingsServiceType != null) {
					_bsSealsProperty = buildingsServiceType.GetProperty("Seals", GameReflection.PublicInstance);
				}

				// Get Difficulty property from IBiomeService
				var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
				if (biomeServiceType != null) {
					_biomeServiceDifficultyProperty = biomeServiceType.GetProperty("Difficulty", GameReflection.PublicInstance);
				}

				// Get inGameSeal field from DifficultyModel
				var difficultyModelType = assembly.GetType("Eremite.Model.DifficultyModel");
				if (difficultyModelType != null) {
					_difficultyInGameSealField = difficultyModelType.GetField("inGameSeal", GameReflection.PublicInstance);
				}

				// Get size field from BuildingModel (SealModel inherits from BuildingModel)
				var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
				if (buildingModelType != null) {
					_sealModelSizeField = buildingModelType.GetField("size", GameReflection.PublicInstance);
				}

				_sealTypesCached = true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] EnsureSealTypes failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get GameSealService from GameServices.
		/// </summary>
		public static object GetGameSealService() {
			EnsureSealTypes();
			return ReflectionHelper.GetProp(_gsGameSealServiceProperty, GameReflection.GetGameServices());
		}

		/// <summary>
		/// Check if the current biome is a sealed biome (Sealed Forest).
		/// </summary>
		public static bool IsSealedBiome() {
			EnsureSealTypes();
			var gameSealService = GetGameSealService();
			if (gameSealService == null || _gameSealServiceIsSealedBiomeMethod == null) return false;
			return ReflectionHelper.InvokeBool(_gameSealServiceIsSealedBiomeMethod, gameSealService);
		}

		/// <summary>
		/// Get the seal field location from GameSealService.
		/// Used when the seal hasn't been discovered yet.
		/// </summary>
		public static Vector2Int GetSealField() {
			EnsureSealTypes();
			var gameSealService = GetGameSealService();
			if (gameSealService == null || _gameSealServiceGetSealFieldMethod == null) return default;

			try {
				var result = _gameSealServiceGetSealFieldMethod.Invoke(gameSealService, null);
				if (result is Vector2Int field) return field;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetSealField failed: {ex.Message}");
			}
			return default;
		}

		/// <summary>
		/// Get the Seals dictionary from BuildingsService.
		/// Returns null if not available.
		/// </summary>
		public static IDictionary GetSeals() {
			EnsureSealTypes();
			var buildingsService = GameReflection.GetBuildingsService();
			if (buildingsService == null || _bsSealsProperty == null) return null;

			try {
				return _bsSealsProperty.GetValue(buildingsService) as IDictionary;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetSeals failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the seal size from BiomeService.Difficulty.inGameSeal.size.
		/// Returns Vector2Int.zero if not available.
		/// </summary>
		public static Vector2Int GetSealSize() {
			EnsureSealTypes();
			var biomeService = GameReflection.GetBiomeService();
			if (biomeService == null) return default;

			try {
				var difficulty = _biomeServiceDifficultyProperty?.GetValue(biomeService);
				if (difficulty == null) return default;

				var inGameSeal = _difficultyInGameSealField?.GetValue(difficulty);
				if (inGameSeal == null) return default;

				var size = _sealModelSizeField?.GetValue(inGameSeal);
				if (size is Vector2Int sealSize) return sealSize;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetSealSize failed: {ex.Message}");
			}
			return default;
		}

		/// <summary>
		/// Get the target field for a guidepost to point at.
		/// If seals exist (discovered), uses first seal's field.
		/// Otherwise uses GameSealService.GetSealField().
		/// </summary>
		public static Vector2Int GetGuidepostTargetField() {
			// Check if seals exist (seal has been discovered)
			var seals = GetSeals();
			if (seals != null && seals.Count > 0) {
				// Get first seal's Field property
				foreach (var seal in seals.Values) {
					if (seal == null) continue;
					var fieldProp = seal.GetType().GetProperty("Field", GameReflection.PublicInstance);
					if (fieldProp != null) {
						var field = fieldProp.GetValue(seal);
						if (field is Vector2Int sealField) return sealField;
					}
					break; // Only need first one
				}
			}

			// Otherwise get seal field from GameSealService
			return GetSealField();
		}
	}
}
