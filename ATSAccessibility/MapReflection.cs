using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
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
	}
}
