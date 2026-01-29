using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to wiki/encyclopedia game internals.
    /// Extracted from GameReflection to keep encyclopedia-specific code separate.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references - they are destroyed on scene change
    /// </summary>
    public static class WikiReflection
    {
        // ========================================
        // ASSEMBLY ACCESS (delegates to GameReflection)
        // ========================================
        private static Assembly _gameAssembly = null;
        private static bool _assemblyCached = false;

        private static void EnsureAssembly()
        {
            if (_assemblyCached) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Assembly-CSharp")
                {
                    _gameAssembly = assembly;
                    break;
                }
            }

            _assemblyCached = true;
        }

        // ========================================
        // WIKI/ENCYCLOPEDIA REFLECTION
        // ========================================

        private static Type _wikiPopupType;
        private static Type _wikiCategoryButtonType;
        private static Type _wikiSlotType;
        private static bool _wikiTypesLookedUp;

        // WikiPopup fields
        private static FieldInfo _wikiPopupCategoryButtonsField;  // List<WikiCategoryButton> categoryButtons
        private static FieldInfo _wikiPopupCurrentField;          // WikiCategoryPanel current
        private static FieldInfo _wikiPopupPanelsField;           // WikiCategoryPanel[] panels

        // WikiCategoryButton fields
        private static FieldInfo _wcbButtonField;                 // Button button
        private static PropertyInfo _wcbPanelProp;                // WikiCategoryPanel Panel

        // WikiSlot fields
        private static FieldInfo _wsButtonField;                  // Button button
        private static MethodInfo _wsIsUnlockedMethod;            // bool IsUnlocked()

        private static void EnsureWikiTypes()
        {
            if (_wikiTypesLookedUp) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _wikiTypesLookedUp = true;
                return;
            }

            try
            {
                // Cache WikiPopup type
                _wikiPopupType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiPopup");
                if (_wikiPopupType != null)
                {
                    _wikiPopupCategoryButtonsField = _wikiPopupType.GetField("categoryButtons",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wikiPopupCurrentField = _wikiPopupType.GetField("current",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wikiPopupPanelsField = _wikiPopupType.GetField("panels",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiPopup type info");
                }

                // Cache WikiCategoryButton type
                _wikiCategoryButtonType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiCategoryButton");
                if (_wikiCategoryButtonType != null)
                {
                    _wcbButtonField = _wikiCategoryButtonType.GetField("button",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wcbPanelProp = _wikiCategoryButtonType.GetProperty("Panel",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiCategoryButton type info");
                }

                // Cache WikiSlot base type
                _wikiSlotType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiSlot");
                if (_wikiSlotType != null)
                {
                    _wsButtonField = _wikiSlotType.GetField("button",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _wsIsUnlockedMethod = _wikiSlotType.GetMethod("IsUnlocked",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiSlot type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: Wiki type caching failed: {ex.Message}");
            }

            _wikiTypesLookedUp = true;
        }

        // Public accessors for wiki types
        public static Type WikiPopupType { get { EnsureWikiTypes(); return _wikiPopupType; } }
        public static Type WikiCategoryButtonType { get { EnsureWikiTypes(); return _wikiCategoryButtonType; } }
        public static Type WikiSlotType { get { EnsureWikiTypes(); return _wikiSlotType; } }

        /// <summary>
        /// Check if the popup is a WikiPopup.
        /// </summary>
        public static bool IsWikiPopup(object popup)
        {
            if (popup == null) return false;
            EnsureWikiTypes();
            if (_wikiPopupType == null) return false;

            return _wikiPopupType.IsAssignableFrom(popup.GetType());
        }

        /// <summary>
        /// Get the category buttons list from a WikiPopup.
        /// </summary>
        public static System.Collections.IList GetWikiCategoryButtons(object wikiPopup)
        {
            EnsureWikiTypes();
            if (wikiPopup == null || _wikiPopupCategoryButtonsField == null) return null;

            try
            {
                return _wikiPopupCategoryButtonsField.GetValue(wikiPopup) as System.Collections.IList;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current (active) WikiCategoryPanel from a WikiPopup.
        /// </summary>
        public static object GetCurrentWikiPanel(object wikiPopup)
        {
            EnsureWikiTypes();
            if (wikiPopup == null || _wikiPopupCurrentField == null) return null;

            try
            {
                return _wikiPopupCurrentField.GetValue(wikiPopup);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the WikiCategoryPanel associated with a WikiCategoryButton.
        /// </summary>
        public static object GetCategoryButtonPanel(object categoryButton)
        {
            EnsureWikiTypes();
            if (categoryButton == null || _wcbPanelProp == null) return null;

            try
            {
                return _wcbPanelProp.GetValue(categoryButton);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a WikiSlot is unlocked.
        /// </summary>
        public static bool IsWikiSlotUnlocked(object slot)
        {
            EnsureWikiTypes();
            if (slot == null || _wsIsUnlockedMethod == null) return false;

            try
            {
                return (bool)_wsIsUnlockedMethod.Invoke(slot, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Click the button on a WikiCategoryButton or WikiSlot.
        /// </summary>
        public static void ClickWikiButton(object buttonHolder)
        {
            if (buttonHolder == null) return;
            EnsureWikiTypes();

            try
            {
                FieldInfo buttonField = null;
                var holderType = buttonHolder.GetType();

                // Check if it's a WikiCategoryButton
                if (_wikiCategoryButtonType != null && _wikiCategoryButtonType.IsAssignableFrom(holderType))
                {
                    buttonField = _wcbButtonField;
                }
                // Check if it's a WikiSlot (or derived)
                else if (_wikiSlotType != null && _wikiSlotType.IsAssignableFrom(holderType))
                {
                    buttonField = _wsButtonField;
                }

                if (buttonField != null)
                {
                    var button = buttonField.GetValue(buttonHolder) as UnityEngine.UI.Button;
                    if (button != null && button.interactable)
                    {
                        button.onClick.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: ClickWikiButton failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Find slots in a WikiCategoryPanel via reflection.
        /// All panel types have a "slots" field containing List of WikiSlot-derived types.
        /// </summary>
        public static System.Collections.IList GetPanelSlots(object panel)
        {
            if (panel == null) return null;

            try
            {
                var panelType = panel.GetType();
                var slotsField = panelType.GetField("slots",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotsField != null)
                {
                    return slotsField.GetValue(panel) as System.Collections.IList;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetPanelSlots failed: {ex.Message}");
            }

            return null;
        }

        // ========================================
        // RACE/SPECIES DATA REFLECTION
        // ========================================

        private static Type _wikiRaceSlotType;
        private static PropertyInfo _wrsRaceProperty;  // WikiRaceSlot.Race property
        private static MethodInfo _raceGetCharacteristicsListTextMethod;
        private static bool _raceTypesLookedUp;

        // RaceModel fields for content extraction
        private static FieldInfo _raceDisplayNameField;     // LocaText displayName
        private static FieldInfo _raceDescriptionField;     // LocaText description
        private static FieldInfo _raceNeedsField;           // NeedModel[] needs
        private static FieldInfo _raceRacialBuildingsField; // BuildingModel[] racialBuildings

        // Additional RaceModel fields for stats
        private static FieldInfo _raceInitialResolveField;        // float initialResolve
        private static FieldInfo _raceNeedsIntervalField;         // float needsInterval
        private static FieldInfo _raceResilienceLabelField;       // LocaText resilienceLabel
        private static FieldInfo _raceResolveThresholdField;      // Vector2 resolveForReputationTreshold
        private static FieldInfo _raceRepThresholdIncreaseField;  // float reputationTresholdIncreasePerReputation
        private static FieldInfo _raceHungerToleranceField;       // int hungerTolerance
        private static FieldInfo _raceRevealEffectDescField;      // LocaText revealEffectLongDesc
        private static FieldInfo _racePassiveEffectDescField;     // LocaText passiveEffectLongDesc

        // NeedModel property
        private static PropertyInfo _needDisplayNameProperty;  // string DisplayName { get; }

        // BuildingModel field
        private static FieldInfo _buildingDisplayNameField;    // LocaText displayName

        private static void EnsureRaceTypes()
        {
            if (_raceTypesLookedUp) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _raceTypesLookedUp = true;
                return;
            }

            try
            {
                // Cache WikiRaceSlot type
                _wikiRaceSlotType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiRaceSlot");
                if (_wikiRaceSlotType != null)
                {
                    _wrsRaceProperty = _wikiRaceSlotType.GetProperty("Race",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiRaceSlot type info");
                }

                // Cache RaceModel type and fields
                var raceModelType = _gameAssembly.GetType("Eremite.Model.RaceModel");
                if (raceModelType != null)
                {
                    _raceGetCharacteristicsListTextMethod = raceModelType.GetMethod("GetCharacteristicsListText",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceDisplayNameField = raceModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceDescriptionField = raceModelType.GetField("description",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceNeedsField = raceModelType.GetField("needs",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceRacialBuildingsField = raceModelType.GetField("racialBuildings",
                        BindingFlags.Public | BindingFlags.Instance);

                    // Additional stat fields
                    _raceInitialResolveField = raceModelType.GetField("initialResolve",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceNeedsIntervalField = raceModelType.GetField("needsInterval",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceResilienceLabelField = raceModelType.GetField("resilienceLabel",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceResolveThresholdField = raceModelType.GetField("resolveForReputationTreshold",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceRepThresholdIncreaseField = raceModelType.GetField("reputationTresholdIncreasePerReputation",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceHungerToleranceField = raceModelType.GetField("hungerTolerance",
                        BindingFlags.Public | BindingFlags.Instance);
                    _raceRevealEffectDescField = raceModelType.GetField("revealEffectLongDesc",
                        BindingFlags.Public | BindingFlags.Instance);
                    _racePassiveEffectDescField = raceModelType.GetField("passiveEffectLongDesc",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached RaceModel type info");
                }

                // Cache NeedModel.DisplayName property
                var needModelType = _gameAssembly.GetType("Eremite.Model.NeedModel");
                if (needModelType != null)
                {
                    _needDisplayNameProperty = needModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached NeedModel type info");
                }

                // Cache BuildingModel.displayName field
                var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                if (buildingModelType != null)
                {
                    _buildingDisplayNameField = buildingModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached BuildingModel type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: Race type caching failed: {ex.Message}");
            }

            _raceTypesLookedUp = true;
        }

        /// <summary>
        /// Check if a slot is a WikiRaceSlot.
        /// </summary>
        public static bool IsWikiRaceSlot(object slot)
        {
            if (slot == null) return false;
            EnsureRaceTypes();
            if (_wikiRaceSlotType == null) return false;

            return _wikiRaceSlotType.IsAssignableFrom(slot.GetType());
        }

        /// <summary>
        /// Get the RaceModel from a WikiRaceSlot.
        /// </summary>
        public static object GetRaceModelFromSlot(object slot)
        {
            if (slot == null) return null;
            EnsureRaceTypes();

            if (_wikiRaceSlotType == null || _wrsRaceProperty == null) return null;
            if (!_wikiRaceSlotType.IsAssignableFrom(slot.GetType())) return null;

            try
            {
                return _wrsRaceProperty.GetValue(slot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceModelFromSlot failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get characteristics/specializations text from a RaceModel.
        /// </summary>
        public static string GetRaceCharacteristicsText(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceGetCharacteristicsListTextMethod == null) return null;

            try
            {
                return _raceGetCharacteristicsListTextMethod.Invoke(raceModel, null) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceCharacteristicsText failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a RaceModel.
        /// </summary>
        public static string GetRaceDisplayName(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceDisplayNameField == null) return null;

            try
            {
                var locaText = _raceDisplayNameField.GetValue(raceModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the description from a RaceModel.
        /// </summary>
        public static string GetRaceDescription(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceDescriptionField == null) return null;

            try
            {
                var locaText = _raceDescriptionField.GetValue(raceModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceDescription failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the needs array from a RaceModel.
        /// </summary>
        public static Array GetRaceNeeds(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceNeedsField == null) return null;

            try
            {
                return _raceNeedsField.GetValue(raceModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceNeeds failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the DisplayName property from a NeedModel.
        /// </summary>
        public static string GetNeedDisplayName(object needModel)
        {
            if (needModel == null) return null;
            EnsureRaceTypes();

            if (_needDisplayNameProperty == null) return null;

            try
            {
                return _needDisplayNameProperty.GetValue(needModel) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetNeedDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the racial buildings array from a RaceModel.
        /// </summary>
        public static Array GetRaceBuildings(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceRacialBuildingsField == null) return null;

            try
            {
                return _raceRacialBuildingsField.GetValue(raceModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRaceBuildings failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a BuildingModel.
        /// </summary>
        public static string GetBuildingDisplayName(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureRaceTypes();

            if (_buildingDisplayNameField == null) return null;

            try
            {
                var locaText = _buildingDisplayNameField.GetValue(buildingModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the initial resolve value from a RaceModel.
        /// </summary>
        public static float GetRaceInitialResolve(object raceModel)
        {
            if (raceModel == null) return 0f;
            EnsureRaceTypes();

            if (_raceInitialResolveField == null) return 0f;

            try
            {
                return (float)_raceInitialResolveField.GetValue(raceModel);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the needs interval (break interval) from a RaceModel in seconds.
        /// </summary>
        public static float GetRaceNeedsInterval(object raceModel)
        {
            if (raceModel == null) return 0f;
            EnsureRaceTypes();

            if (_raceNeedsIntervalField == null) return 0f;

            try
            {
                return (float)_raceNeedsIntervalField.GetValue(raceModel);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the resilience label text from a RaceModel.
        /// </summary>
        public static string GetRaceResilienceLabel(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceResilienceLabelField == null) return null;

            try
            {
                var locaText = _raceResilienceLabelField.GetValue(raceModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the demanding value (resolveForReputationTreshold.x) from a RaceModel.
        /// </summary>
        public static float GetRaceDemanding(object raceModel)
        {
            if (raceModel == null) return 0f;
            EnsureRaceTypes();

            if (_raceResolveThresholdField == null) return 0f;

            try
            {
                var threshold = (Vector2)_raceResolveThresholdField.GetValue(raceModel);
                return threshold.x;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the decadent value (reputationTresholdIncreasePerReputation) from a RaceModel.
        /// </summary>
        public static float GetRaceDecadent(object raceModel)
        {
            if (raceModel == null) return 0f;
            EnsureRaceTypes();

            if (_raceRepThresholdIncreaseField == null) return 0f;

            try
            {
                return (float)_raceRepThresholdIncreaseField.GetValue(raceModel);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the hunger tolerance value from a RaceModel.
        /// </summary>
        public static int GetRaceHungerTolerance(object raceModel)
        {
            if (raceModel == null) return 0;
            EnsureRaceTypes();

            if (_raceHungerToleranceField == null) return 0;

            try
            {
                return (int)_raceHungerToleranceField.GetValue(raceModel);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the reveal effect description from a RaceModel.
        /// </summary>
        public static string GetRaceRevealEffect(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_raceRevealEffectDescField == null) return null;

            try
            {
                var locaText = _raceRevealEffectDescField.GetValue(raceModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the passive effect description from a RaceModel.
        /// </summary>
        public static string GetRacePassiveEffect(object raceModel)
        {
            if (raceModel == null) return null;
            EnsureRaceTypes();

            if (_racePassiveEffectDescField == null) return null;

            try
            {
                var locaText = _racePassiveEffectDescField.GetValue(raceModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // BUILDING DATA REFLECTION
        // ========================================

        // WikiBuildingSlot
        private static Type _wikiBuildingSlotType;
        private static PropertyInfo _wbsBuildingProperty;  // BuildingModel Building

        // BuildingModel fields (some already cached)
        private static FieldInfo _buildingDescriptionField;    // LocaText description
        private static FieldInfo _buildingCategoryField;       // BuildingCategoryModel category
        private static FieldInfo _buildingSizeField;           // Vector2Int size
        private static FieldInfo _buildingMovableField;        // bool movable
        private static FieldInfo _buildingRequiredGoodsField;  // GoodRef[] requiredGoods
        private static FieldInfo _buildingTagsField;           // BuildingTagModel[] tags
        private static PropertyInfo _buildingWorkplacesCountProperty; // int WorkplacesCount
        private static PropertyInfo _buildingDescriptionProperty;     // string Description (virtual)

        // WorkshopModel fields
        private static Type _workshopModelType;
        private static FieldInfo _workshopRecipesField;        // WorkshopRecipeModel[] recipes
        private static FieldInfo _workshopWorkplacesField;     // WorkplaceModel[] workplaces

        // WorkshopRecipeModel fields
        private static Type _workshopRecipeModelType;
        private static FieldInfo _recipeProducedGoodField;     // GoodRef producedGood
        private static FieldInfo _recipeRequiredGoodsField;    // GoodsSet[] requiredGoods
        private static FieldInfo _recipeProductionTimeField;   // float productionTime
        private static FieldInfo _recipeGradeField;            // RecipeGradeModel grade

        // GoodRef fields
        private static Type _goodRefType;
        private static FieldInfo _goodRefGoodField;            // GoodModel good
        private static FieldInfo _goodRefAmountField;          // int amount

        // GoodsSet fields
        private static Type _goodsSetType;
        private static FieldInfo _goodsSetGoodsField;          // GoodRef[] goods

        // GoodModel fields
        private static FieldInfo _goodModelDisplayNameField;   // LocaText displayName

        // BuildingTagModel fields
        private static FieldInfo _tagDisplayNameField;         // LocaText displayName
        private static FieldInfo _tagVisibleField;             // bool visible

        // BuildingCategoryModel / LabelModel fields
        private static PropertyInfo _categoryDisplayNameProperty;  // string DisplayName (from LabelModel)

        // RecipeGradeModel fields
        private static FieldInfo _gradeDescriptionField;       // LocaText description
        private static FieldInfo _gradeLevelField;             // int level

        private static bool _buildingTypesLookedUp;

        private static void EnsureBuildingTypes()
        {
            if (_buildingTypesLookedUp) return;
            EnsureAssembly();

            if (_gameAssembly == null)
            {
                _buildingTypesLookedUp = true;
                return;
            }

            try
            {
                // Cache WikiBuildingSlot type
                _wikiBuildingSlotType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiBuildingSlot");
                if (_wikiBuildingSlotType != null)
                {
                    _wbsBuildingProperty = _wikiBuildingSlotType.GetProperty("Building",
                        BindingFlags.Public | BindingFlags.Instance);
                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiBuildingSlot type info");
                }

                // Cache BuildingModel type and fields
                var buildingModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingModel");
                if (buildingModelType != null)
                {
                    _buildingDescriptionField = buildingModelType.GetField("description",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _buildingCategoryField = buildingModelType.GetField("category",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingSizeField = buildingModelType.GetField("size",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingMovableField = buildingModelType.GetField("movable",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingRequiredGoodsField = buildingModelType.GetField("requiredGoods",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingTagsField = buildingModelType.GetField("tags",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingWorkplacesCountProperty = buildingModelType.GetProperty("WorkplacesCount",
                        BindingFlags.Public | BindingFlags.Instance);
                    _buildingDescriptionProperty = buildingModelType.GetProperty("Description",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached BuildingModel type info");
                }

                // Cache WorkshopModel type
                _workshopModelType = _gameAssembly.GetType("Eremite.Buildings.WorkshopModel");
                if (_workshopModelType != null)
                {
                    _workshopRecipesField = _workshopModelType.GetField("recipes",
                        BindingFlags.Public | BindingFlags.Instance);
                    _workshopWorkplacesField = _workshopModelType.GetField("workplaces",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WorkshopModel type info");
                }

                // Cache WorkshopRecipeModel type
                _workshopRecipeModelType = _gameAssembly.GetType("Eremite.Buildings.WorkshopRecipeModel");
                if (_workshopRecipeModelType != null)
                {
                    _recipeProducedGoodField = _workshopRecipeModelType.GetField("producedGood",
                        BindingFlags.Public | BindingFlags.Instance);
                    _recipeRequiredGoodsField = _workshopRecipeModelType.GetField("requiredGoods",
                        BindingFlags.Public | BindingFlags.Instance);
                    _recipeProductionTimeField = _workshopRecipeModelType.GetField("productionTime",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WorkshopRecipeModel type info");
                }

                // Cache RecipeModel base type for grade field
                var recipeModelType = _gameAssembly.GetType("Eremite.Buildings.RecipeModel");
                if (recipeModelType != null)
                {
                    _recipeGradeField = recipeModelType.GetField("grade",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache GoodRef type
                _goodRefType = _gameAssembly.GetType("Eremite.Model.GoodRef");
                if (_goodRefType != null)
                {
                    _goodRefGoodField = _goodRefType.GetField("good",
                        BindingFlags.Public | BindingFlags.Instance);
                    _goodRefAmountField = _goodRefType.GetField("amount",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached GoodRef type info");
                }

                // Cache GoodsSet type
                _goodsSetType = _gameAssembly.GetType("Eremite.Model.GoodsSet");
                if (_goodsSetType != null)
                {
                    _goodsSetGoodsField = _goodsSetType.GetField("goods",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached GoodsSet type info");
                }

                // Cache GoodModel type
                var goodModelType = _gameAssembly.GetType("Eremite.Model.GoodModel");
                if (goodModelType != null)
                {
                    _goodModelDisplayNameField = goodModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache BuildingTagModel type
                var buildingTagModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingTagModel");
                if (buildingTagModelType != null)
                {
                    _tagDisplayNameField = buildingTagModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _tagVisibleField = buildingTagModelType.GetField("visible",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached BuildingTagModel type info");
                }

                // Cache LabelModel type for category display name
                var labelModelType = _gameAssembly.GetType("Eremite.Model.LabelModel");
                if (labelModelType != null)
                {
                    _categoryDisplayNameProperty = labelModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache RecipeGradeModel type
                var recipeGradeModelType = _gameAssembly.GetType("Eremite.Buildings.RecipeGradeModel");
                if (recipeGradeModelType != null)
                {
                    _gradeDescriptionField = recipeGradeModelType.GetField("description",
                        BindingFlags.Public | BindingFlags.Instance);
                    _gradeLevelField = recipeGradeModelType.GetField("level",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached RecipeGradeModel type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: Building type caching failed: {ex.Message}");
            }

            _buildingTypesLookedUp = true;
        }

        // ========================================
        // BUILDING PUBLIC API
        // ========================================

        /// <summary>
        /// Check if a slot is a WikiBuildingSlot.
        /// </summary>
        public static bool IsWikiBuildingSlot(object slot)
        {
            if (slot == null) return false;
            EnsureBuildingTypes();
            if (_wikiBuildingSlotType == null) return false;

            return _wikiBuildingSlotType.IsAssignableFrom(slot.GetType());
        }

        /// <summary>
        /// Get the BuildingModel from a WikiBuildingSlot.
        /// </summary>
        public static object GetBuildingModelFromSlot(object slot)
        {
            if (slot == null) return null;
            EnsureBuildingTypes();

            if (_wikiBuildingSlotType == null || _wbsBuildingProperty == null) return null;
            if (!_wikiBuildingSlotType.IsAssignableFrom(slot.GetType())) return null;

            try
            {
                return _wbsBuildingProperty.GetValue(slot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingModelFromSlot failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the description from a BuildingModel (virtual property).
        /// </summary>
        public static string GetBuildingDescription(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingTypes();

            if (_buildingDescriptionProperty == null) return null;

            try
            {
                return _buildingDescriptionProperty.GetValue(buildingModel) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingDescription failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the category display name from a BuildingModel.
        /// </summary>
        public static string GetBuildingCategory(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingTypes();

            if (_buildingCategoryField == null) return null;

            try
            {
                var category = _buildingCategoryField.GetValue(buildingModel);
                if (category == null) return null;

                // Try to get DisplayName from LabelModel
                if (_categoryDisplayNameProperty != null)
                {
                    return _categoryDisplayNameProperty.GetValue(category) as string;
                }

                // Fallback to ToString
                return category.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingCategory failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the size from a BuildingModel.
        /// </summary>
        public static Vector2Int GetBuildingSize(object buildingModel)
        {
            if (buildingModel == null) return Vector2Int.zero;
            EnsureBuildingTypes();

            if (_buildingSizeField == null) return Vector2Int.zero;

            try
            {
                return (Vector2Int)_buildingSizeField.GetValue(buildingModel);
            }
            catch
            {
                return Vector2Int.zero;
            }
        }

        /// <summary>
        /// Get whether a BuildingModel is movable.
        /// </summary>
        public static bool GetBuildingMovable(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureBuildingTypes();

            if (_buildingMovableField == null) return false;

            try
            {
                return (bool)_buildingMovableField.GetValue(buildingModel);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the workplaces count from a BuildingModel.
        /// </summary>
        public static int GetBuildingWorkplacesCount(object buildingModel)
        {
            if (buildingModel == null) return 0;
            EnsureBuildingTypes();

            if (_buildingWorkplacesCountProperty == null) return 0;

            try
            {
                return (int)_buildingWorkplacesCountProperty.GetValue(buildingModel);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the required goods (construction cost) from a BuildingModel.
        /// </summary>
        public static Array GetBuildingRequiredGoods(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingTypes();

            if (_buildingRequiredGoodsField == null) return null;

            try
            {
                return _buildingRequiredGoodsField.GetValue(buildingModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingRequiredGoods failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a GoodRef.
        /// </summary>
        public static string GetGoodRefDisplayName(object goodRef)
        {
            if (goodRef == null) return null;
            EnsureBuildingTypes();

            if (_goodRefGoodField == null || _goodModelDisplayNameField == null) return null;

            try
            {
                var good = _goodRefGoodField.GetValue(goodRef);
                if (good == null) return null;

                var locaText = _goodModelDisplayNameField.GetValue(good);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetGoodRefDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the amount from a GoodRef.
        /// </summary>
        public static int GetGoodRefAmount(object goodRef)
        {
            if (goodRef == null) return 0;
            EnsureBuildingTypes();

            if (_goodRefAmountField == null) return 0;

            try
            {
                return (int)_goodRefAmountField.GetValue(goodRef);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the tags array from a BuildingModel.
        /// </summary>
        public static Array GetBuildingTags(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureBuildingTypes();

            if (_buildingTagsField == null) return null;

            try
            {
                return _buildingTagsField.GetValue(buildingModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingTags failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a BuildingTagModel.
        /// </summary>
        public static string GetTagDisplayName(object tagModel)
        {
            if (tagModel == null) return null;
            EnsureBuildingTypes();

            if (_tagDisplayNameField == null) return null;

            try
            {
                var locaText = _tagDisplayNameField.GetValue(tagModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetTagDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get whether a BuildingTagModel is visible.
        /// </summary>
        public static bool GetTagVisible(object tagModel)
        {
            if (tagModel == null) return false;
            EnsureBuildingTypes();

            if (_tagVisibleField == null) return false;

            try
            {
                return (bool)_tagVisibleField.GetValue(tagModel);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a BuildingModel is a WorkshopModel.
        /// </summary>
        public static bool IsWorkshopModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureBuildingTypes();
            if (_workshopModelType == null) return false;

            return _workshopModelType.IsAssignableFrom(buildingModel.GetType());
        }

        /// <summary>
        /// Get the recipes array from a WorkshopModel.
        /// </summary>
        public static Array GetWorkshopRecipes(object workshopModel)
        {
            if (workshopModel == null) return null;
            EnsureBuildingTypes();

            if (_workshopModelType == null || _workshopRecipesField == null) return null;
            if (!_workshopModelType.IsAssignableFrom(workshopModel.GetType())) return null;

            try
            {
                return _workshopRecipesField.GetValue(workshopModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetWorkshopRecipes failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the output good name from a WorkshopRecipeModel.
        /// </summary>
        public static string GetRecipeOutputName(object recipeModel)
        {
            if (recipeModel == null) return null;
            EnsureBuildingTypes();

            if (_recipeProducedGoodField == null) return null;

            try
            {
                var producedGood = _recipeProducedGoodField.GetValue(recipeModel);
                return GetGoodRefDisplayName(producedGood);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRecipeOutputName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the output amount from a WorkshopRecipeModel.
        /// </summary>
        public static int GetRecipeOutputAmount(object recipeModel)
        {
            if (recipeModel == null) return 0;
            EnsureBuildingTypes();

            if (_recipeProducedGoodField == null) return 0;

            try
            {
                var producedGood = _recipeProducedGoodField.GetValue(recipeModel);
                return GetGoodRefAmount(producedGood);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the production time from a WorkshopRecipeModel.
        /// </summary>
        public static float GetRecipeProductionTime(object recipeModel)
        {
            if (recipeModel == null) return 0f;
            EnsureBuildingTypes();

            if (_recipeProductionTimeField == null) return 0f;

            try
            {
                return (float)_recipeProductionTimeField.GetValue(recipeModel);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the grade level (0, 1, 2 stars) from a recipe.
        /// </summary>
        public static int GetRecipeGradeLevel(object recipeModel)
        {
            if (recipeModel == null) return 0;
            EnsureBuildingTypes();

            if (_recipeGradeField == null || _gradeLevelField == null) return 0;

            try
            {
                var grade = _recipeGradeField.GetValue(recipeModel);
                if (grade == null) return 0;

                return (int)_gradeLevelField.GetValue(grade);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the required goods sets (inputs) from a WorkshopRecipeModel.
        /// Returns array of GoodsSet objects.
        /// </summary>
        public static Array GetRecipeRequiredGoods(object recipeModel)
        {
            if (recipeModel == null) return null;
            EnsureBuildingTypes();

            if (_recipeRequiredGoodsField == null) return null;

            try
            {
                return _recipeRequiredGoodsField.GetValue(recipeModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRecipeRequiredGoods failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the goods array from a GoodsSet.
        /// Returns array of GoodRef objects (alternatives for this input slot).
        /// </summary>
        public static Array GetGoodsSetGoods(object goodsSet)
        {
            if (goodsSet == null) return null;
            EnsureBuildingTypes();

            if (_goodsSetGoodsField == null) return null;

            try
            {
                return _goodsSetGoodsField.GetValue(goodsSet) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetGoodsSetGoods failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // UPGRADABLE BUILDING REFLECTION
        // ========================================

        // UpgradableBuildingModel (extends BuildingModel)
        private static Type _upgradableBuildingModelType;
        private static FieldInfo _ubmLevelsField;              // BuildingLevelModel[] levels
        private static FieldInfo _ubmHideUpgradesField;        // bool hideUpgradesInWiki

        // BuildingLevelModel
        private static Type _buildingLevelModelType;
        private static FieldInfo _blmOptionsField;             // BuildingPerkModel[] options
        private static FieldInfo _blmRequiredGoodsField;       // GoodsSet[] requiredGoods

        // BuildingPerkModel
        private static Type _buildingPerkModelType;
        private static PropertyInfo _bpmDisplayNameProperty;   // string DisplayName
        private static MethodInfo _bpmGetDescriptionMethod;    // string GetDescription(Building)
        private static MethodInfo _bpmGetAmountTextMethod;     // string GetAmountText()

        private static bool _upgradeTypesInitialized = false;

        /// <summary>
        /// Ensure upgrade-related types are cached.
        /// </summary>
        private static void EnsureUpgradeTypes()
        {
            if (_upgradeTypesInitialized) return;
            _upgradeTypesInitialized = true;

            EnsureAssembly();
            if (_gameAssembly == null) return;

            try
            {
                // Cache UpgradableBuildingModel type
                _upgradableBuildingModelType = _gameAssembly.GetType("Eremite.Buildings.UpgradableBuildingModel");
                if (_upgradableBuildingModelType != null)
                {
                    _ubmLevelsField = _upgradableBuildingModelType.GetField("levels",
                        BindingFlags.Public | BindingFlags.Instance);
                    _ubmHideUpgradesField = _upgradableBuildingModelType.GetField("hideUpgradesInWiki",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached UpgradableBuildingModel type info");
                }

                // Cache BuildingLevelModel type
                _buildingLevelModelType = _gameAssembly.GetType("Eremite.Buildings.BuildingLevelModel");
                if (_buildingLevelModelType != null)
                {
                    _blmOptionsField = _buildingLevelModelType.GetField("options",
                        BindingFlags.Public | BindingFlags.Instance);
                    _blmRequiredGoodsField = _buildingLevelModelType.GetField("requiredGoods",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached BuildingLevelModel type info");
                }

                // Cache BuildingPerkModel type
                _buildingPerkModelType = _gameAssembly.GetType("Eremite.Model.BuildingPerkModel");
                if (_buildingPerkModelType != null)
                {
                    _bpmDisplayNameProperty = _buildingPerkModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bpmGetDescriptionMethod = _buildingPerkModelType.GetMethod("GetDescription",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new Type[] { _gameAssembly.GetType("Eremite.Buildings.Building") ?? typeof(object) }, null);
                    _bpmGetAmountTextMethod = _buildingPerkModelType.GetMethod("GetAmountText",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached BuildingPerkModel type info");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: EnsureUpgradeTypes failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a BuildingModel is an UpgradableBuildingModel.
        /// </summary>
        public static bool IsUpgradableBuildingModel(object buildingModel)
        {
            if (buildingModel == null) return false;
            EnsureUpgradeTypes();
            if (_upgradableBuildingModelType == null) return false;

            return _upgradableBuildingModelType.IsAssignableFrom(buildingModel.GetType());
        }

        /// <summary>
        /// Get whether upgrades should be hidden in wiki for this building.
        /// </summary>
        public static bool GetHideUpgradesInWiki(object buildingModel)
        {
            if (buildingModel == null) return true;
            EnsureUpgradeTypes();

            if (_upgradableBuildingModelType == null || _ubmHideUpgradesField == null) return true;
            if (!_upgradableBuildingModelType.IsAssignableFrom(buildingModel.GetType())) return true;

            try
            {
                return (bool)_ubmHideUpgradesField.GetValue(buildingModel);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get the building levels array from an UpgradableBuildingModel.
        /// </summary>
        public static Array GetBuildingLevels(object buildingModel)
        {
            if (buildingModel == null) return null;
            EnsureUpgradeTypes();

            if (_upgradableBuildingModelType == null || _ubmLevelsField == null) return null;
            if (!_upgradableBuildingModelType.IsAssignableFrom(buildingModel.GetType())) return null;

            try
            {
                return _ubmLevelsField.GetValue(buildingModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetBuildingLevels failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the perk options array from a BuildingLevelModel.
        /// </summary>
        public static Array GetLevelOptions(object levelModel)
        {
            if (levelModel == null) return null;
            EnsureUpgradeTypes();

            if (_blmOptionsField == null) return null;

            try
            {
                return _blmOptionsField.GetValue(levelModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetLevelOptions failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the required goods sets (upgrade cost) from a BuildingLevelModel.
        /// </summary>
        public static Array GetLevelRequiredGoods(object levelModel)
        {
            if (levelModel == null) return null;
            EnsureUpgradeTypes();

            if (_blmRequiredGoodsField == null) return null;

            try
            {
                return _blmRequiredGoodsField.GetValue(levelModel) as Array;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetLevelRequiredGoods failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a BuildingPerkModel.
        /// </summary>
        public static string GetPerkDisplayName(object perkModel)
        {
            if (perkModel == null) return null;
            EnsureUpgradeTypes();

            if (_bpmDisplayNameProperty == null) return null;

            try
            {
                return _bpmDisplayNameProperty.GetValue(perkModel) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetPerkDisplayName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the description from a BuildingPerkModel.
        /// </summary>
        public static string GetPerkDescription(object perkModel)
        {
            if (perkModel == null) return null;
            EnsureUpgradeTypes();

            if (_bpmGetDescriptionMethod == null) return null;

            try
            {
                // Call GetDescription(null) - building context is optional
                return _bpmGetDescriptionMethod.Invoke(perkModel, new object[] { null }) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetPerkDescription failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the amount text from a BuildingPerkModel (e.g., "+10%", "+2").
        /// </summary>
        public static string GetPerkAmountText(object perkModel)
        {
            if (perkModel == null) return null;
            EnsureUpgradeTypes();

            if (_bpmGetAmountTextMethod == null) return null;

            try
            {
                return _bpmGetAmountTextMethod.Invoke(perkModel, null) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetPerkAmountText failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // RELIC/GLADE EVENT REFLECTION
        // ========================================

        // WikiRelicSlot
        private static Type _wikiRelicSlotType;
        private static PropertyInfo _wrsRelicProperty;  // RelicModel Relic

        // RelicModel fields
        private static Type _relicModelType;
        private static FieldInfo _relicDisplayNameField;        // LocaText displayName
        private static FieldInfo _relicDangerLevelField;        // DangerLevel dangerLevel
        private static FieldInfo _relicWorkplacesField;         // WorkplaceModel[] workplaces
        private static FieldInfo _relicHasDynamicEffectsField;  // bool hasDynamicEffects
        private static FieldInfo _relicEffectsTiersField;       // EffectStep[] effectsTiers
        private static FieldInfo _relicActiveEffectsField;      // EffectModel[] activeEffects
        private static FieldInfo _relicDifficultiesField;       // RelicDifficulty[] difficulties
        private static FieldInfo _relicHasDynamicRewardsField;  // bool hasDynamicRewards
        private static FieldInfo _relicRewardsTiersField;       // RewardStep[] rewardsTiers
        private static FieldInfo _relicDecisionsRewardsField;   // EffectsTable[] decisionsRewards
        private static PropertyInfo _relicHasDecisionProperty;  // bool HasDecision

        // EffectStep fields
        private static Type _effectStepType;
        private static FieldInfo _effectStepTimeToStartField;   // float timeToStart
        private static FieldInfo _effectStepEffectField;        // EffectModel[] effect

        // RewardStep fields
        private static Type _rewardStepType;
        private static FieldInfo _rewardStepTimeToStartField;   // float timeToStart
        private static FieldInfo _rewardStepRewardsField;       // EffectModel[] rewards
        private static FieldInfo _rewardStepRewardsTableField;  // EffectsTable rewardsTable

        // RelicDifficulty fields
        private static Type _relicDifficultyType;
        private static FieldInfo _relicDifficultyDifficultyField;           // int difficulty
        private static FieldInfo _relicDifficultyEffectTimeRatioField;      // float effectTimeToStartRatio
        private static FieldInfo _relicDifficultyDecisionsField;            // RelicDecision[] decisions

        // RelicDecision fields
        private static Type _relicDecisionType;
        private static FieldInfo _relicDecisionWorkingTimeField;    // float workingTime
        private static FieldInfo _relicDecisionLabelField;          // LabelModel label
        private static FieldInfo _relicDecisionRequiredGoodsField;  // GoodsSetTable requriedGoods
        private static FieldInfo _relicDecisionWorkingEffectsField; // EffectModel[] workingEffects

        // GoodsSetTable fields
        private static Type _goodsSetTableType;
        private static FieldInfo _goodsSetTableSetsField;           // GoodsSet[] sets

        // EffectModel fields
        private static Type _effectModelType;
        private static PropertyInfo _effectModelDisplayNameProperty;    // string DisplayName
        private static PropertyInfo _effectModelDescriptionProperty;    // string Description

        // EffectsTable fields
        private static Type _effectsTableType;
        private static MethodInfo _effectsTableGetAllEffectsMethod;     // IEnumerable<EffectModel> GetAllEffects()

        // LabelModel fields (for decision labels)
        private static FieldInfo _labelModelDisplayNameField;       // LocaText displayName

        private static bool _relicTypesInitialized = false;

        private static void EnsureRelicTypes()
        {
            if (_relicTypesInitialized) return;
            _relicTypesInitialized = true;

            EnsureAssembly();
            if (_gameAssembly == null) return;

            try
            {
                // Cache WikiRelicSlot type
                _wikiRelicSlotType = _gameAssembly.GetType("Eremite.View.UI.Wiki.WikiRelicSlot");
                if (_wikiRelicSlotType != null)
                {
                    _wrsRelicProperty = _wikiRelicSlotType.GetProperty("Relic",
                        BindingFlags.Public | BindingFlags.Instance);
                    Debug.Log("[ATSAccessibility] WikiReflection: Cached WikiRelicSlot type info");
                }

                // Cache RelicModel type
                _relicModelType = _gameAssembly.GetType("Eremite.Buildings.RelicModel");
                if (_relicModelType != null)
                {
                    _relicDisplayNameField = _relicModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDangerLevelField = _relicModelType.GetField("dangerLevel",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicWorkplacesField = _relicModelType.GetField("workplaces",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicHasDynamicEffectsField = _relicModelType.GetField("hasDynamicEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicEffectsTiersField = _relicModelType.GetField("effectsTiers",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicActiveEffectsField = _relicModelType.GetField("activeEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDifficultiesField = _relicModelType.GetField("difficulties",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicHasDynamicRewardsField = _relicModelType.GetField("hasDynamicRewards",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicRewardsTiersField = _relicModelType.GetField("rewardsTiers",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDecisionsRewardsField = _relicModelType.GetField("decisionsRewards",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicHasDecisionProperty = _relicModelType.GetProperty("HasDecision",
                        BindingFlags.Public | BindingFlags.Instance);

                    Debug.Log("[ATSAccessibility] WikiReflection: Cached RelicModel type info");
                }

                // Cache EffectStep type
                _effectStepType = _gameAssembly.GetType("Eremite.Buildings.EffectStep");
                if (_effectStepType != null)
                {
                    _effectStepTimeToStartField = _effectStepType.GetField("timeToStart",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectStepEffectField = _effectStepType.GetField("effect",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache RewardStep type
                _rewardStepType = _gameAssembly.GetType("Eremite.Buildings.RewardStep");
                if (_rewardStepType != null)
                {
                    _rewardStepTimeToStartField = _rewardStepType.GetField("timeToStart",
                        BindingFlags.Public | BindingFlags.Instance);
                    _rewardStepRewardsField = _rewardStepType.GetField("rewards",
                        BindingFlags.Public | BindingFlags.Instance);
                    _rewardStepRewardsTableField = _rewardStepType.GetField("rewardsTable",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache RelicDifficulty type
                _relicDifficultyType = _gameAssembly.GetType("Eremite.Buildings.RelicDifficulty");
                if (_relicDifficultyType != null)
                {
                    _relicDifficultyDifficultyField = _relicDifficultyType.GetField("difficulty",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDifficultyEffectTimeRatioField = _relicDifficultyType.GetField("effectTimeToStartRatio",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDifficultyDecisionsField = _relicDifficultyType.GetField("decisions",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache RelicDecision type
                _relicDecisionType = _gameAssembly.GetType("Eremite.Buildings.RelicDecision");
                if (_relicDecisionType != null)
                {
                    _relicDecisionWorkingTimeField = _relicDecisionType.GetField("workingTime",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDecisionLabelField = _relicDecisionType.GetField("label",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDecisionRequiredGoodsField = _relicDecisionType.GetField("requriedGoods",
                        BindingFlags.Public | BindingFlags.Instance);
                    _relicDecisionWorkingEffectsField = _relicDecisionType.GetField("workingEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache GoodsSetTable type
                _goodsSetTableType = _gameAssembly.GetType("Eremite.Model.GoodsSetTable");
                if (_goodsSetTableType != null)
                {
                    _goodsSetTableSetsField = _goodsSetTableType.GetField("sets",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache EffectModel type
                _effectModelType = _gameAssembly.GetType("Eremite.Model.EffectModel");
                if (_effectModelType != null)
                {
                    _effectModelDisplayNameProperty = _effectModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectModelDescriptionProperty = _effectModelType.GetProperty("Description",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache EffectsTable type
                _effectsTableType = _gameAssembly.GetType("Eremite.Model.EffectsTable");
                if (_effectsTableType != null)
                {
                    _effectsTableGetAllEffectsMethod = _effectsTableType.GetMethod("GetAllEffects",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache LabelModel displayName field
                var labelModelType = _gameAssembly.GetType("Eremite.Model.LabelModel");
                if (labelModelType != null)
                {
                    _labelModelDisplayNameField = labelModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] WikiReflection: Cached all relic types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: EnsureRelicTypes failed: {ex.Message}");
            }
        }

        // ========================================
        // RELIC PUBLIC API
        // ========================================

        /// <summary>
        /// Check if a slot is a WikiRelicSlot.
        /// </summary>
        public static bool IsWikiRelicSlot(object slot)
        {
            if (slot == null) return false;
            EnsureRelicTypes();
            if (_wikiRelicSlotType == null) return false;

            return _wikiRelicSlotType.IsAssignableFrom(slot.GetType());
        }

        /// <summary>
        /// Get the RelicModel from a WikiRelicSlot.
        /// </summary>
        public static object GetRelicModelFromSlot(object slot)
        {
            if (slot == null) return null;
            EnsureRelicTypes();

            if (_wikiRelicSlotType == null || _wrsRelicProperty == null) return null;
            if (!_wikiRelicSlotType.IsAssignableFrom(slot.GetType())) return null;

            try
            {
                return _wrsRelicProperty.GetValue(slot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRelicModelFromSlot failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the display name from a RelicModel.
        /// </summary>
        public static string GetRelicDisplayName(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicDisplayNameField == null) return null;

            try
            {
                var locaText = _relicDisplayNameField.GetValue(relicModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the danger level from a RelicModel as a string.
        /// </summary>
        public static string GetRelicDangerLevel(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicDangerLevelField == null) return null;

            try
            {
                var dangerLevel = _relicDangerLevelField.GetValue(relicModel);
                return dangerLevel?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the workplaces count from a RelicModel.
        /// </summary>
        public static int GetRelicWorkplacesCount(object relicModel)
        {
            if (relicModel == null) return 0;
            EnsureRelicTypes();

            if (_relicWorkplacesField == null) return 0;

            try
            {
                var workplaces = _relicWorkplacesField.GetValue(relicModel) as Array;
                return workplaces?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get whether the relic has dynamic (escalating) effects.
        /// </summary>
        public static bool GetRelicHasDynamicEffects(object relicModel)
        {
            if (relicModel == null) return false;
            EnsureRelicTypes();

            if (_relicHasDynamicEffectsField == null) return false;

            try
            {
                return (bool)_relicHasDynamicEffectsField.GetValue(relicModel);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the effect tiers (for dynamic effects) from a RelicModel.
        /// </summary>
        public static Array GetRelicEffectsTiers(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicEffectsTiersField == null) return null;

            try
            {
                return _relicEffectsTiersField.GetValue(relicModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the active effects (for static effects) from a RelicModel.
        /// </summary>
        public static Array GetRelicActiveEffects(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicActiveEffectsField == null) return null;

            try
            {
                return _relicActiveEffectsField.GetValue(relicModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get whether the relic has multiple decision paths.
        /// </summary>
        public static bool GetRelicHasDecision(object relicModel)
        {
            if (relicModel == null) return false;
            EnsureRelicTypes();

            if (_relicHasDecisionProperty == null) return false;

            try
            {
                return (bool)_relicHasDecisionProperty.GetValue(relicModel);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the difficulties array from a RelicModel.
        /// </summary>
        public static Array GetRelicDifficulties(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicDifficultiesField == null) return null;

            try
            {
                return _relicDifficultiesField.GetValue(relicModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the decisions rewards array from a RelicModel.
        /// </summary>
        public static Array GetRelicDecisionsRewards(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicDecisionsRewardsField == null) return null;

            try
            {
                return _relicDecisionsRewardsField.GetValue(relicModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the rewards tiers from a RelicModel.
        /// </summary>
        public static Array GetRelicRewardsTiers(object relicModel)
        {
            if (relicModel == null) return null;
            EnsureRelicTypes();

            if (_relicRewardsTiersField == null) return null;

            try
            {
                return _relicRewardsTiersField.GetValue(relicModel) as Array;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // EFFECT STEP ACCESSORS
        // ========================================

        /// <summary>
        /// Get the time to start from an EffectStep.
        /// </summary>
        public static float GetEffectStepTimeToStart(object effectStep)
        {
            if (effectStep == null) return 0f;
            EnsureRelicTypes();

            if (_effectStepTimeToStartField == null) return 0f;

            try
            {
                return (float)_effectStepTimeToStartField.GetValue(effectStep);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the effects array from an EffectStep.
        /// </summary>
        public static Array GetEffectStepEffects(object effectStep)
        {
            if (effectStep == null) return null;
            EnsureRelicTypes();

            if (_effectStepEffectField == null) return null;

            try
            {
                return _effectStepEffectField.GetValue(effectStep) as Array;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // REWARD STEP ACCESSORS
        // ========================================

        /// <summary>
        /// Get the rewards array from a RewardStep.
        /// </summary>
        public static Array GetRewardStepRewards(object rewardStep)
        {
            if (rewardStep == null) return null;
            EnsureRelicTypes();

            if (_rewardStepRewardsField == null) return null;

            try
            {
                return _rewardStepRewardsField.GetValue(rewardStep) as Array;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all effects from a RewardStep (including from rewardsTable).
        /// </summary>
        public static System.Collections.Generic.List<object> GetRewardStepAllEffects(object rewardStep)
        {
            if (rewardStep == null) return null;
            EnsureRelicTypes();

            var result = new System.Collections.Generic.List<object>();

            try
            {
                // Add direct rewards
                var rewards = _rewardStepRewardsField?.GetValue(rewardStep) as Array;
                if (rewards != null)
                {
                    foreach (var reward in rewards)
                    {
                        if (reward != null)
                            result.Add(reward);
                    }
                }

                // Add rewards from table
                var rewardsTable = _rewardStepRewardsTableField?.GetValue(rewardStep);
                if (rewardsTable != null && _effectsTableGetAllEffectsMethod != null)
                {
                    var tableEffects = _effectsTableGetAllEffectsMethod.Invoke(rewardsTable, null) as System.Collections.IEnumerable;
                    if (tableEffects != null)
                    {
                        foreach (var effect in tableEffects)
                        {
                            if (effect != null)
                                result.Add(effect);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetRewardStepAllEffects failed: {ex.Message}");
            }

            return result;
        }

        // ========================================
        // RELIC DIFFICULTY ACCESSORS
        // ========================================

        /// <summary>
        /// Get the difficulty level from a RelicDifficulty.
        /// </summary>
        public static int GetRelicDifficultyLevel(object relicDifficulty)
        {
            if (relicDifficulty == null) return 0;
            EnsureRelicTypes();

            if (_relicDifficultyDifficultyField == null) return 0;

            try
            {
                return (int)_relicDifficultyDifficultyField.GetValue(relicDifficulty);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the effect time ratio from a RelicDifficulty.
        /// </summary>
        public static float GetRelicDifficultyEffectTimeRatio(object relicDifficulty)
        {
            if (relicDifficulty == null) return 1f;
            EnsureRelicTypes();

            if (_relicDifficultyEffectTimeRatioField == null) return 1f;

            try
            {
                return (float)_relicDifficultyEffectTimeRatioField.GetValue(relicDifficulty);
            }
            catch
            {
                return 1f;
            }
        }

        /// <summary>
        /// Get the decisions array from a RelicDifficulty.
        /// </summary>
        public static Array GetRelicDifficultyDecisions(object relicDifficulty)
        {
            if (relicDifficulty == null) return null;
            EnsureRelicTypes();

            if (_relicDifficultyDecisionsField == null) return null;

            try
            {
                return _relicDifficultyDecisionsField.GetValue(relicDifficulty) as Array;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // RELIC DECISION ACCESSORS
        // ========================================

        /// <summary>
        /// Get the working time from a RelicDecision.
        /// </summary>
        public static float GetRelicDecisionWorkingTime(object relicDecision)
        {
            if (relicDecision == null) return 0f;
            EnsureRelicTypes();

            if (_relicDecisionWorkingTimeField == null) return 0f;

            try
            {
                return (float)_relicDecisionWorkingTimeField.GetValue(relicDecision);
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the label from a RelicDecision.
        /// </summary>
        public static string GetRelicDecisionLabel(object relicDecision)
        {
            if (relicDecision == null) return null;
            EnsureRelicTypes();

            if (_relicDecisionLabelField == null) return null;

            try
            {
                var label = _relicDecisionLabelField.GetValue(relicDecision);
                if (label == null) return null;

                // Get displayName from LabelModel
                if (_labelModelDisplayNameField != null)
                {
                    var locaText = _labelModelDisplayNameField.GetValue(label);
                    return GameReflection.GetLocaText(locaText);
                }

                return label.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the required goods from a RelicDecision.
        /// </summary>
        public static Array GetRelicDecisionRequiredGoods(object relicDecision)
        {
            if (relicDecision == null) return null;
            EnsureRelicTypes();

            if (_relicDecisionRequiredGoodsField == null) return null;

            try
            {
                var goodsSetTable = _relicDecisionRequiredGoodsField.GetValue(relicDecision);
                if (goodsSetTable == null) return null;

                // Get sets from GoodsSetTable
                if (_goodsSetTableSetsField != null)
                {
                    return _goodsSetTableSetsField.GetValue(goodsSetTable) as Array;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the working effects from a RelicDecision.
        /// </summary>
        public static Array GetRelicDecisionWorkingEffects(object relicDecision)
        {
            if (relicDecision == null) return null;
            EnsureRelicTypes();

            if (_relicDecisionWorkingEffectsField == null) return null;

            try
            {
                return _relicDecisionWorkingEffectsField.GetValue(relicDecision) as Array;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // EFFECT MODEL ACCESSORS
        // ========================================

        /// <summary>
        /// Get the display name from an EffectModel.
        /// </summary>
        public static string GetEffectDisplayName(object effectModel)
        {
            if (effectModel == null) return null;
            EnsureRelicTypes();

            if (_effectModelDisplayNameProperty == null) return null;

            try
            {
                return _effectModelDisplayNameProperty.GetValue(effectModel) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the description from an EffectModel.
        /// </summary>
        public static string GetEffectDescription(object effectModel)
        {
            if (effectModel == null) return null;
            EnsureRelicTypes();

            if (_effectModelDescriptionProperty == null) return null;

            try
            {
                return _effectModelDescriptionProperty.GetValue(effectModel) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get all effects from an EffectsTable.
        /// </summary>
        public static System.Collections.Generic.List<object> GetEffectsTableAllEffects(object effectsTable)
        {
            if (effectsTable == null) return null;
            EnsureRelicTypes();

            if (_effectsTableGetAllEffectsMethod == null) return null;

            var result = new System.Collections.Generic.List<object>();

            try
            {
                var effects = _effectsTableGetAllEffectsMethod.Invoke(effectsTable, null) as System.Collections.IEnumerable;
                if (effects != null)
                {
                    foreach (var effect in effects)
                    {
                        if (effect != null)
                            result.Add(effect);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] WikiReflection: GetEffectsTableAllEffects failed: {ex.Message}");
            }

            return result;
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(WikiReflection), "WikiReflection");
        }
    }
}
