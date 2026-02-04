using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Provides reflection-based access to tile info objects: natural resources, deposits,
	/// buildings, and their models/states. Used by TileInfoReader for the I key feature.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, FieldInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - Per-type dictionaries handle polymorphic game types (different resource/model subtypes)
	/// </summary>
	public static class TileInfoReflection {
		// ========================================
		// NATURAL RESOURCE CACHE (per-type dictionaries)
		// ========================================

		private static Dictionary<Type, PropertyInfo> _naturalResourceModelProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, PropertyInfo> _naturalResourceStateProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, FieldInfo> _resourceStateChargesLeftFields = new Dictionary<Type, FieldInfo>();
		private static Dictionary<Type, FieldInfo> _resourceModelChargesFields = new Dictionary<Type, FieldInfo>();
		private static Dictionary<Type, PropertyInfo> _resourceModelRefGoodNameProps = new Dictionary<Type, PropertyInfo>();

		public static PropertyInfo GetNaturalResourceModelProp(Type resourceType) {
			if (!_naturalResourceModelProps.TryGetValue(resourceType, out var prop)) {
				prop = resourceType.GetProperty("Model");
				_naturalResourceModelProps[resourceType] = prop;
			}
			return prop;
		}

		public static PropertyInfo GetNaturalResourceStateProp(Type resourceType) {
			if (!_naturalResourceStateProps.TryGetValue(resourceType, out var prop)) {
				prop = resourceType.GetProperty("State");
				_naturalResourceStateProps[resourceType] = prop;
			}
			return prop;
		}

		public static FieldInfo GetResourceStateChargesLeftField(Type stateType) {
			if (!_resourceStateChargesLeftFields.TryGetValue(stateType, out var field)) {
				field = stateType.GetField("chargesLeft", GameReflection.PublicInstance);
				_resourceStateChargesLeftFields[stateType] = field;
			}
			return field;
		}

		public static FieldInfo GetResourceModelChargesField(Type modelType) {
			if (!_resourceModelChargesFields.TryGetValue(modelType, out var field)) {
				field = modelType.GetField("charges", GameReflection.PublicInstance);
				_resourceModelChargesFields[modelType] = field;
			}
			return field;
		}

		public static PropertyInfo GetResourceModelRefGoodNameProp(Type modelType) {
			if (!_resourceModelRefGoodNameProps.TryGetValue(modelType, out var prop)) {
				prop = modelType.GetProperty("RefGoodName");
				_resourceModelRefGoodNameProps[modelType] = prop;
			}
			return prop;
		}

		// ========================================
		// RESOURCE DEPOSIT CACHE (per-type dictionaries)
		// ========================================

		private static Dictionary<Type, PropertyInfo> _depositModelProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, PropertyInfo> _depositStateProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, PropertyInfo> _depositModelDescProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, FieldInfo> _depositStateChargesLeftFields = new Dictionary<Type, FieldInfo>();
		private static Dictionary<Type, FieldInfo> _depositStateMaxChargesFields = new Dictionary<Type, FieldInfo>();

		public static PropertyInfo GetDepositModelProp(Type depositType) {
			if (!_depositModelProps.TryGetValue(depositType, out var prop)) {
				prop = depositType.GetProperty("Model");
				_depositModelProps[depositType] = prop;
			}
			return prop;
		}

		public static PropertyInfo GetDepositStateProp(Type depositType) {
			if (!_depositStateProps.TryGetValue(depositType, out var prop)) {
				prop = depositType.GetProperty("State");
				_depositStateProps[depositType] = prop;
			}
			return prop;
		}

		public static PropertyInfo GetDepositModelDescProp(Type modelType) {
			if (!_depositModelDescProps.TryGetValue(modelType, out var prop)) {
				prop = modelType.GetProperty("Description");
				_depositModelDescProps[modelType] = prop;
			}
			return prop;
		}

		public static FieldInfo GetDepositStateChargesLeftField(Type stateType) {
			if (!_depositStateChargesLeftFields.TryGetValue(stateType, out var field)) {
				field = stateType.GetField("chargesLeft", GameReflection.PublicInstance);
				_depositStateChargesLeftFields[stateType] = field;
			}
			return field;
		}

		public static FieldInfo GetDepositStateMaxChargesField(Type stateType) {
			if (!_depositStateMaxChargesFields.TryGetValue(stateType, out var field)) {
				field = stateType.GetField("maxCharges", GameReflection.PublicInstance);
				_depositStateMaxChargesFields[stateType] = field;
			}
			return field;
		}

		// ========================================
		// BUILDING CACHE (per-type dictionaries)
		// ========================================

		private static Dictionary<Type, PropertyInfo> _buildingModelProps = new Dictionary<Type, PropertyInfo>();
		private static Dictionary<Type, PropertyInfo> _buildingModelDescProps = new Dictionary<Type, PropertyInfo>();

		public static PropertyInfo GetBuildingModelProp(Type buildingType) {
			if (!_buildingModelProps.TryGetValue(buildingType, out var prop)) {
				prop = buildingType.GetProperty("BuildingModel");
				_buildingModelProps[buildingType] = prop;
			}
			return prop;
		}

		public static PropertyInfo GetBuildingModelDescProp(Type modelType) {
			if (!_buildingModelDescProps.TryGetValue(modelType, out var prop)) {
				prop = modelType.GetProperty("Description");
				_buildingModelDescProps[modelType] = prop;
			}
			return prop;
		}

		// ========================================
		// SHARED MODEL FIELDS (production, extraProduction)
		// ========================================

		private static FieldInfo _productionField;
		private static FieldInfo _extraProductionField;
		private static FieldInfo _goodRefGoodField;
		private static FieldInfo _goodRefAmountField;
		private static PropertyInfo _goodRefChanceDisplayNameProp;
		private static FieldInfo _goodRefChanceField;
		private static FieldInfo _goodDisplayNameField;
		private static bool _sharedCached;

		public static FieldInfo ProductionField => _productionField;
		public static FieldInfo ExtraProductionField => _extraProductionField;
		public static FieldInfo GoodRefGoodField => _goodRefGoodField;
		public static FieldInfo GoodRefAmountField => _goodRefAmountField;
		public static PropertyInfo GoodRefChanceDisplayNameProp => _goodRefChanceDisplayNameProp;
		public static FieldInfo GoodRefChanceField => _goodRefChanceField;
		public static FieldInfo GoodDisplayNameField => _goodDisplayNameField;

		public static void EnsureSharedCache(object model) {
			if (_sharedCached || model == null) return;

			var modelType = model.GetType();
			_productionField = modelType.GetField("production", GameReflection.PublicInstance);
			_extraProductionField = modelType.GetField("extraProduction", GameReflection.PublicInstance);

			// Cache GoodRef fields if we have a production object
			if (_productionField != null) {
				var production = _productionField.GetValue(model);
				if (production != null) {
					var prodType = production.GetType();
					_goodRefGoodField = prodType.GetField("good", GameReflection.PublicInstance);
					_goodRefAmountField = prodType.GetField("amount", GameReflection.PublicInstance);

					// Cache Good fields
					if (_goodRefGoodField != null) {
						var good = _goodRefGoodField.GetValue(production);
						if (good != null) {
							_goodDisplayNameField = good.GetType().GetField("displayName", GameReflection.PublicInstance);
						}
					}
				}
			}

			// Cache GoodRefChance fields if we have extraProduction
			if (_extraProductionField != null) {
				var extraProduction = _extraProductionField.GetValue(model) as Array;
				if (extraProduction != null && extraProduction.Length > 0) {
					var firstItem = extraProduction.GetValue(0);
					if (firstItem != null) {
						var itemType = firstItem.GetType();
						_goodRefChanceDisplayNameProp = itemType.GetProperty("DisplayName");
						_goodRefChanceField = itemType.GetField("chance", GameReflection.PublicInstance);
					}
				}
			}

			_sharedCached = true;
		}

		// ========================================
		// SERVICE CACHE
		// ========================================

		private static PropertyInfo _campsMatrixProp;
		private static PropertyInfo _hutsMatrixProp;
		private static bool _serviceCached;

		public static PropertyInfo CampsMatrixProp => _campsMatrixProp;
		public static PropertyInfo HutsMatrixProp => _hutsMatrixProp;

		public static void EnsureServiceCache(object resourcesService, object depositsService) {
			if (_serviceCached) return;

			if (resourcesService != null && _campsMatrixProp == null) {
				_campsMatrixProp = resourcesService.GetType().GetProperty("CampsMatrix", GameReflection.PublicInstance);
			}

			if (depositsService != null && _hutsMatrixProp == null) {
				_hutsMatrixProp = depositsService.GetType().GetProperty("HutsMatrix", GameReflection.PublicInstance);
			}

			_serviceCached = true;
		}

		// ========================================
		// GLADE DISCOVERY (delegates to MapReflection)
		// ========================================

		public static bool GetGladeWasDiscovered(object glade) {
			return MapReflection.GetGladeWasDiscovered(glade);
		}

		// ========================================
		// SAFE ACCESS HELPERS
		// ========================================

		public static int GetIntField(object obj, FieldInfo field) {
			if (obj == null || field == null) return 0;
			try { return (int)field.GetValue(obj); } catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetIntField failed: {ex.Message}"); return 0; }
		}

		public static string GetStringProperty(object obj, PropertyInfo prop) {
			if (obj == null || prop == null) return null;
			try { return prop.GetValue(obj) as string; } catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetStringProperty failed: {ex.Message}"); return null; }
		}

		public static float GetFloatField(object obj, FieldInfo field) {
			if (obj == null || field == null) return 0f;
			try { return (float)field.GetValue(obj); } catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetFloatField failed: {ex.Message}"); return 0f; }
		}

		// ========================================
		// UTILITY METHODS
		// ========================================

		/// <summary>
		/// Get localized text from a LocaText field (fieldName.Text).
		/// </summary>
		public static string GetLocalizedText(object obj, string fieldName) {
			try {
				var field = obj.GetType().GetField(fieldName, GameReflection.PublicInstance);
				if (field == null) return null;

				var locaText = field.GetValue(obj);
				return GameReflection.GetLocaText(locaText);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetLocalizedText failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the Description property from a model object.
		/// For NaturalResourceModel/ResourceDepositModel, this includes the grade requirement text with sprite tags.
		/// </summary>
		public static string GetDescriptionProperty(object model) {
			if (model == null) return null;

			try {
				var descProp = model.GetType().GetProperty("Description", GameReflection.PublicInstance);
				return descProp?.GetValue(model) as string;
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetDescriptionProperty failed: {ex.Message}");
				return null;
			}
		}
	}
}
