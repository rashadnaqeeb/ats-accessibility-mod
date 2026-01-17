using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reads detailed tile information (like tooltips) for the I key feature.
    /// Provides building, natural resource, and deposit info via reflection.
    /// Uses cached reflection metadata for performance while fetching fresh values each call.
    /// </summary>
    public static class TileInfoReader
    {
        // ========================================
        // REFLECTION CACHE (per-type dictionaries)
        // ========================================

        // NaturalResource reflection cache (per-type for different resource/model types)
        private static Dictionary<Type, PropertyInfo> _naturalResourceModelProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, PropertyInfo> _naturalResourceStateProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, FieldInfo> _resourceStateChargesLeftFields = new Dictionary<Type, FieldInfo>();
        private static Dictionary<Type, FieldInfo> _resourceModelChargesFields = new Dictionary<Type, FieldInfo>();
        private static Dictionary<Type, PropertyInfo> _resourceModelRefGoodNameProps = new Dictionary<Type, PropertyInfo>();

        // ResourceDeposit reflection cache (per-type)
        private static Dictionary<Type, PropertyInfo> _depositModelProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, PropertyInfo> _depositStateProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, PropertyInfo> _depositModelDescProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, FieldInfo> _depositStateChargesLeftFields = new Dictionary<Type, FieldInfo>();
        private static Dictionary<Type, FieldInfo> _depositStateMaxChargesFields = new Dictionary<Type, FieldInfo>();

        // Building reflection cache (per-type)
        private static Dictionary<Type, PropertyInfo> _buildingModelProps = new Dictionary<Type, PropertyInfo>();
        private static Dictionary<Type, PropertyInfo> _buildingModelDescProps = new Dictionary<Type, PropertyInfo>();

        // Shared model fields (production, extraProduction)
        private static FieldInfo _productionField;
        private static FieldInfo _extraProductionField;
        private static FieldInfo _goodRefGoodField;
        private static FieldInfo _goodRefAmountField;
        private static PropertyInfo _goodRefChanceDisplayNameProp;
        private static FieldInfo _goodRefChanceField;
        private static FieldInfo _goodDisplayNameField;
        private static PropertyInfo _locaTextTextProp;
        private static bool _sharedCached;

        // Service reflection cache
        private static PropertyInfo _campsMatrixProp;
        private static PropertyInfo _hutsMatrixProp;
        private static bool _serviceCached;

        // ========================================
        // CACHE INITIALIZATION METHODS
        // ========================================

        private static void EnsureSharedCache(object model)
        {
            if (_sharedCached || model == null) return;

            var modelType = model.GetType();
            _productionField = modelType.GetField("production", BindingFlags.Public | BindingFlags.Instance);
            _extraProductionField = modelType.GetField("extraProduction", BindingFlags.Public | BindingFlags.Instance);

            // Cache GoodRef fields if we have a production object
            if (_productionField != null)
            {
                var production = _productionField.GetValue(model);
                if (production != null)
                {
                    var prodType = production.GetType();
                    _goodRefGoodField = prodType.GetField("good", BindingFlags.Public | BindingFlags.Instance);
                    _goodRefAmountField = prodType.GetField("amount", BindingFlags.Public | BindingFlags.Instance);

                    // Cache Good fields
                    if (_goodRefGoodField != null)
                    {
                        var good = _goodRefGoodField.GetValue(production);
                        if (good != null)
                        {
                            _goodDisplayNameField = good.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance);

                            // Cache LocaText.Text property
                            if (_goodDisplayNameField != null)
                            {
                                var displayName = _goodDisplayNameField.GetValue(good);
                                if (displayName != null)
                                {
                                    _locaTextTextProp = displayName.GetType().GetProperty("Text");
                                }
                            }
                        }
                    }
                }
            }

            // Cache GoodRefChance fields if we have extraProduction
            if (_extraProductionField != null)
            {
                var extraProduction = _extraProductionField.GetValue(model) as Array;
                if (extraProduction != null && extraProduction.Length > 0)
                {
                    var firstItem = extraProduction.GetValue(0);
                    if (firstItem != null)
                    {
                        var itemType = firstItem.GetType();
                        _goodRefChanceDisplayNameProp = itemType.GetProperty("DisplayName");
                        _goodRefChanceField = itemType.GetField("chance", BindingFlags.Public | BindingFlags.Instance);
                    }
                }
            }

            _sharedCached = true;
        }

        private static void EnsureServiceCache(object resourcesService, object depositsService)
        {
            if (_serviceCached) return;

            if (resourcesService != null && _campsMatrixProp == null)
            {
                _campsMatrixProp = resourcesService.GetType().GetProperty("CampsMatrix", BindingFlags.Public | BindingFlags.Instance);
            }

            if (depositsService != null && _hutsMatrixProp == null)
            {
                _hutsMatrixProp = depositsService.GetType().GetProperty("HutsMatrix", BindingFlags.Public | BindingFlags.Instance);
            }

            _serviceCached = true;
        }

        // ========================================
        // SAFE ACCESS HELPERS
        // ========================================

        private static int GetIntField(object obj, FieldInfo field)
        {
            if (obj == null || field == null) return 0;
            try { return (int)field.GetValue(obj); }
            catch { return 0; }
        }

        private static string GetStringProperty(object obj, PropertyInfo prop)
        {
            if (obj == null || prop == null) return null;
            try { return prop.GetValue(obj) as string; }
            catch { return null; }
        }

        private static float GetFloatField(object obj, FieldInfo field)
        {
            if (obj == null || field == null) return 0f;
            try { return (float)field.GetValue(obj); }
            catch { return 0f; }
        }

        // ========================================
        // CONSOLIDATED HELPERS
        // ========================================

        /// <summary>
        /// Get charges info: "X of Y charges"
        /// For NaturalResource: chargesLeft from state, max from model
        /// For Deposit: both from state
        /// </summary>
        private static string GetChargesInfo(object state, FieldInfo chargesLeftField, object maxSource, FieldInfo maxChargesField)
        {
            int chargesLeft = GetIntField(state, chargesLeftField);
            int maxCharges = GetIntField(maxSource, maxChargesField);

            return maxCharges > 0 ? $"{chargesLeft} of {maxCharges} charges" : null;
        }

        /// <summary>
        /// Get localized text from a LocaText field (fieldName.Text).
        /// Uses cached _locaTextTextProp when available.
        /// </summary>
        private static string GetLocalizedText(object obj, string fieldName)
        {
            try
            {
                var field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) return null;

                var locaText = field.GetValue(obj);
                if (locaText == null) return null;

                // Use cached property if available, otherwise get it
                var textProp = _locaTextTextProp ?? locaText.GetType().GetProperty("Text");
                if (textProp == null) return null;

                return textProp.GetValue(locaText) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get names from a dictionary of building models.
        /// Shared logic for CampsMatrix and HutsMatrix lookups.
        /// </summary>
        private static string GetSourceBuildingNames(object dictionary, object key, bool useContainsKey)
        {
            if (dictionary == null || key == null) return null;

            try
            {
                object buildingList;

                if (useContainsKey)
                {
                    // For object keys (like depositModel), check ContainsKey first
                    var containsKeyMethod = dictionary.GetType().GetMethod("ContainsKey");
                    if (containsKeyMethod == null) return null;

                    bool containsKey = (bool)containsKeyMethod.Invoke(dictionary, new object[] { key });
                    if (!containsKey) return null;
                }

                // Get the list using indexer
                var indexerProp = dictionary.GetType().GetProperty("Item");
                if (indexerProp == null) return null;

                try
                {
                    buildingList = indexerProp.GetValue(dictionary, new object[] { key });
                }
                catch
                {
                    return null;
                }

                if (buildingList == null) return null;

                var listEnumerable = buildingList as IEnumerable;
                if (listEnumerable == null) return null;

                var names = new List<string>();
                foreach (var building in listEnumerable)
                {
                    if (building == null) continue;

                    string name = GetLocalizedText(building, "displayName");
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }

                return names.Count > 0 ? string.Join(", ", names) : null;
            }
            catch
            {
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
        public static void ReadCurrentTile(int cursorX, int cursorY)
        {
            var objectOn = GameReflection.GetObjectOn(cursorX, cursorY);

            if (objectOn == null)
            {
                Speech.Say("No object");
                return;
            }

            string typeName = objectOn.GetType().Name;

            // GetObjectOn returns Field when there's no actual object
            if (typeName == "Field")
            {
                Speech.Say("No object");
                return;
            }

            string info = null;

            // Determine object type and get appropriate info
            // Check inheritance chain for Building (Storage -> ProductionBuilding -> Building)
            if (InheritsFrom(objectOn.GetType(), "Building"))
            {
                info = GetBuildingInfo(objectOn);
            }
            else if (typeName == "NaturalResource")
            {
                info = GetNaturalResourceInfo(objectOn);
            }
            else if (typeName == "ResourceDeposit")
            {
                info = GetResourceDepositInfo(objectOn);
            }
            else
            {
                // Unknown type - try generic name extraction
                info = GetGenericObjectInfo(objectOn);
            }

            if (!string.IsNullOrEmpty(info))
            {
                Speech.Say(info);
            }
            else
            {
                Speech.Say("No information available");
            }
        }

        // ========================================
        // OBJECT TYPE HANDLERS
        // ========================================

        /// <summary>
        /// Get info for a building (description only - name already announced).
        /// </summary>
        private static string GetBuildingInfo(object building)
        {
            try
            {
                var buildingType = building.GetType();

                // Get or cache BuildingModel property for this type
                if (!_buildingModelProps.TryGetValue(buildingType, out var buildingModelProp))
                {
                    buildingModelProp = buildingType.GetProperty("BuildingModel");
                    _buildingModelProps[buildingType] = buildingModelProp;
                }
                if (buildingModelProp == null) return null;

                var buildingModel = buildingModelProp.GetValue(building);
                if (buildingModel == null) return null;

                var modelType = buildingModel.GetType();

                // Get or cache Description property for this model type
                if (!_buildingModelDescProps.TryGetValue(modelType, out var descProp))
                {
                    descProp = modelType.GetProperty("Description");
                    _buildingModelDescProps[modelType] = descProp;
                }

                var parts = new List<string>();

                // Get Description
                string desc = GetStringProperty(buildingModel, descProp);
                if (!string.IsNullOrEmpty(desc))
                {
                    parts.Add(desc);
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetBuildingInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get info for a natural resource (tree, mushroom, etc).
        /// Includes: description, charges, products, sources (name excluded - already announced).
        /// </summary>
        private static string GetNaturalResourceInfo(object resource)
        {
            try
            {
                var resourceType = resource.GetType();

                // Get or cache Model property
                if (!_naturalResourceModelProps.TryGetValue(resourceType, out var modelProp))
                {
                    modelProp = resourceType.GetProperty("Model");
                    _naturalResourceModelProps[resourceType] = modelProp;
                }
                if (modelProp == null) return null;

                var model = modelProp.GetValue(resource);
                if (model == null) return null;

                var modelType = model.GetType();

                // Get or cache State property
                if (!_naturalResourceStateProps.TryGetValue(resourceType, out var stateProp))
                {
                    stateProp = resourceType.GetProperty("State");
                    _naturalResourceStateProps[resourceType] = stateProp;
                }
                var state = stateProp?.GetValue(resource);
                var stateType = state?.GetType();

                // Get or cache charges fields
                if (!_resourceModelChargesFields.TryGetValue(modelType, out var modelChargesField))
                {
                    modelChargesField = modelType.GetField("charges", BindingFlags.Public | BindingFlags.Instance);
                    _resourceModelChargesFields[modelType] = modelChargesField;
                }

                FieldInfo stateChargesLeftField = null;
                if (stateType != null && !_resourceStateChargesLeftFields.TryGetValue(stateType, out stateChargesLeftField))
                {
                    stateChargesLeftField = stateType.GetField("chargesLeft", BindingFlags.Public | BindingFlags.Instance);
                    _resourceStateChargesLeftFields[stateType] = stateChargesLeftField;
                }

                // Get or cache RefGoodName property
                if (!_resourceModelRefGoodNameProps.TryGetValue(modelType, out var refGoodNameProp))
                {
                    refGoodNameProp = modelType.GetProperty("RefGoodName");
                    _resourceModelRefGoodNameProps[modelType] = refGoodNameProp;
                }

                var parts = new List<string>();

                // Description - NaturalResourceModel uses a 'description' field (LocaText), not a property
                string desc = GetLocalizedText(model, "description");
                if (!string.IsNullOrEmpty(desc))
                {
                    parts.Add(desc);
                }

                // Charges: chargesLeft from state, max from model
                string chargesInfo = GetChargesInfo(state, stateChargesLeftField, model, modelChargesField);
                if (!string.IsNullOrEmpty(chargesInfo))
                {
                    parts.Add(chargesInfo);
                }

                // Main product
                string productInfo = GetMainProductInfo(model);
                if (!string.IsNullOrEmpty(productInfo))
                {
                    parts.Add($"Produces {productInfo}");
                }

                // Extra products
                string extraInfo = GetExtraProductsInfo(model);
                if (!string.IsNullOrEmpty(extraInfo))
                {
                    parts.Add($"Extra: {extraInfo}");
                }

                // Source buildings (camps that can harvest)
                string sourcesInfo = GetCampsForResource(model, refGoodNameProp);
                if (!string.IsNullOrEmpty(sourcesInfo))
                {
                    parts.Add($"Harvested by: {sourcesInfo}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetNaturalResourceInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get info for a resource deposit (clay, copper, etc).
        /// Includes: description, charges, products, sources (name excluded - already announced).
        /// </summary>
        private static string GetResourceDepositInfo(object deposit)
        {
            try
            {
                var depositType = deposit.GetType();

                // Get or cache Model property
                if (!_depositModelProps.TryGetValue(depositType, out var modelProp))
                {
                    modelProp = depositType.GetProperty("Model");
                    _depositModelProps[depositType] = modelProp;
                }
                if (modelProp == null) return null;

                var model = modelProp.GetValue(deposit);
                if (model == null) return null;

                var modelType = model.GetType();

                // Get or cache State property
                if (!_depositStateProps.TryGetValue(depositType, out var stateProp))
                {
                    stateProp = depositType.GetProperty("State");
                    _depositStateProps[depositType] = stateProp;
                }
                var state = stateProp?.GetValue(deposit);
                var stateType = state?.GetType();

                // Get or cache Description property
                if (!_depositModelDescProps.TryGetValue(modelType, out var descProp))
                {
                    descProp = modelType.GetProperty("Description");
                    _depositModelDescProps[modelType] = descProp;
                }

                // Get or cache charges fields (both from state for deposits)
                FieldInfo stateChargesLeftField = null;
                FieldInfo stateMaxChargesField = null;
                if (stateType != null)
                {
                    if (!_depositStateChargesLeftFields.TryGetValue(stateType, out stateChargesLeftField))
                    {
                        stateChargesLeftField = stateType.GetField("chargesLeft", BindingFlags.Public | BindingFlags.Instance);
                        _depositStateChargesLeftFields[stateType] = stateChargesLeftField;
                    }
                    if (!_depositStateMaxChargesFields.TryGetValue(stateType, out stateMaxChargesField))
                    {
                        stateMaxChargesField = stateType.GetField("maxCharges", BindingFlags.Public | BindingFlags.Instance);
                        _depositStateMaxChargesFields[stateType] = stateMaxChargesField;
                    }
                }

                var parts = new List<string>();

                // Description
                string desc = GetStringProperty(model, descProp);
                if (!string.IsNullOrEmpty(desc))
                {
                    parts.Add(desc);
                }

                // Charges: both values from state for deposits
                string chargesInfo = GetChargesInfo(state, stateChargesLeftField, state, stateMaxChargesField);
                if (!string.IsNullOrEmpty(chargesInfo))
                {
                    parts.Add(chargesInfo);
                }

                // Main product
                string productInfo = GetMainProductInfo(model);
                if (!string.IsNullOrEmpty(productInfo))
                {
                    parts.Add($"Produces {productInfo}");
                }

                // Extra products
                string extraInfo = GetExtraProductsInfo(model);
                if (!string.IsNullOrEmpty(extraInfo))
                {
                    parts.Add($"Extra: {extraInfo}");
                }

                // Source buildings (gatherer huts that can work this deposit)
                string sourcesInfo = GetHutsForDeposit(model);
                if (!string.IsNullOrEmpty(sourcesInfo))
                {
                    parts.Add($"Gathered by: {sourcesInfo}");
                }

                return string.Join(". ", parts);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetResourceDepositInfo failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generic fallback for unknown object types.
        /// </summary>
        private static string GetGenericObjectInfo(object obj)
        {
            try
            {
                // Try Model.displayName first
                var modelProp = obj.GetType().GetProperty("Model");
                if (modelProp != null)
                {
                    var model = modelProp.GetValue(obj);
                    if (model != null)
                    {
                        string name = GetLocalizedText(model, "displayName");
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }

                // Fallback to type name
                return obj.GetType().Name;
            }
            catch
            {
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
        private static string GetMainProductInfo(object model)
        {
            try
            {
                var productionField = _productionField ?? model.GetType().GetField("production", BindingFlags.Public | BindingFlags.Instance);
                if (productionField == null) return null;

                var production = productionField.GetValue(model);
                if (production == null) return null;

                // Get good
                var goodField = _goodRefGoodField ?? production.GetType().GetField("good", BindingFlags.Public | BindingFlags.Instance);
                if (goodField == null) return null;

                var good = goodField.GetValue(production);
                if (good == null) return null;

                // Get displayName
                var displayNameField = _goodDisplayNameField ?? good.GetType().GetField("displayName", BindingFlags.Public | BindingFlags.Instance);
                if (displayNameField == null) return null;

                var displayName = displayNameField.GetValue(good);
                if (displayName == null) return null;

                // Get Text
                var textProp = _locaTextTextProp ?? displayName.GetType().GetProperty("Text");
                if (textProp == null) return null;

                string productName = textProp.GetValue(displayName) as string;

                // Get amount
                var amountField = _goodRefAmountField ?? production.GetType().GetField("amount", BindingFlags.Public | BindingFlags.Instance);
                int amount = GetIntField(production, amountField);

                if (!string.IsNullOrEmpty(productName))
                {
                    return amount > 1 ? $"{amount} {productName}" : productName;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Get extra products info: "Product1 X%, Product2 Y%"
        /// Path: Model.extraProduction[] array of GoodRefChance
        /// </summary>
        private static string GetExtraProductsInfo(object model)
        {
            try
            {
                var extraProductionField = _extraProductionField ?? model.GetType().GetField("extraProduction", BindingFlags.Public | BindingFlags.Instance);
                if (extraProductionField == null) return null;

                var extraProduction = extraProductionField.GetValue(model) as Array;
                if (extraProduction == null || extraProduction.Length == 0) return null;

                var parts = new List<string>();

                foreach (var item in extraProduction)
                {
                    if (item == null) continue;

                    // Get DisplayName (using cached property if available)
                    var displayNameProp = _goodRefChanceDisplayNameProp ?? item.GetType().GetProperty("DisplayName");
                    string productName = displayNameProp != null ? displayNameProp.GetValue(item) as string : null;

                    // Get chance (using cached field if available)
                    var chanceField = _goodRefChanceField ?? item.GetType().GetField("chance", BindingFlags.Public | BindingFlags.Instance);
                    float chance = GetFloatField(item, chanceField);

                    if (!string.IsNullOrEmpty(productName) && chance > 0)
                    {
                        int percent = Mathf.RoundToInt(chance * 100f);
                        parts.Add($"{productName} {percent}%");
                    }
                }

                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch { }

            return null;
        }

        // ========================================
        // SOURCE BUILDING HELPERS
        // ========================================

        /// <summary>
        /// Get camps that can harvest a natural resource.
        /// Path: ResourcesService.CampsMatrix[Model.RefGoodName]
        /// </summary>
        private static string GetCampsForResource(object resourceModel, PropertyInfo refGoodNameProp)
        {
            try
            {
                // Get RefGoodName from model (use passed-in cached property)
                string refGoodName = GetStringProperty(resourceModel, refGoodNameProp);
                if (string.IsNullOrEmpty(refGoodName)) return null;

                // Get ResourcesService
                var resourcesService = GameReflection.GetResourcesService();
                if (resourcesService == null) return null;

                // Initialize service cache
                EnsureServiceCache(resourcesService, null);

                // Get CampsMatrix dictionary
                var campsMatrixProp = _campsMatrixProp ?? resourcesService.GetType().GetProperty("CampsMatrix", BindingFlags.Public | BindingFlags.Instance);
                if (campsMatrixProp == null) return null;

                var campsMatrix = campsMatrixProp.GetValue(resourcesService);

                // Use consolidated helper (string key doesn't need ContainsKey check)
                return GetSourceBuildingNames(campsMatrix, refGoodName, false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCampsForResource failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get gatherer huts that can work a resource deposit.
        /// Path: DepositsService.HutsMatrix[Model]
        /// </summary>
        private static string GetHutsForDeposit(object depositModel)
        {
            try
            {
                // Get DepositsService
                var depositsService = GameReflection.GetDepositsService();
                if (depositsService == null) return null;

                // Initialize service cache
                EnsureServiceCache(null, depositsService);

                // Get HutsMatrix dictionary
                var hutsMatrixProp = _hutsMatrixProp ?? depositsService.GetType().GetProperty("HutsMatrix", BindingFlags.Public | BindingFlags.Instance);
                if (hutsMatrixProp == null) return null;

                var hutsMatrix = hutsMatrixProp.GetValue(depositsService);

                // Use consolidated helper (object key needs ContainsKey check)
                return GetSourceBuildingNames(hutsMatrix, depositModel, true);
            }
            catch (Exception ex)
            {
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
        private static bool InheritsFrom(Type type, string ancestorName)
        {
            Type current = type;
            while (current != null)
            {
                if (current.Name == ancestorName)
                    return true;
                current = current.BaseType;
            }
            return false;
        }
    }
}
