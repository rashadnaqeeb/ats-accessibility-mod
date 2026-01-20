using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to building panel and building internals.
    /// Follows same patterns as GameReflection.cs - cache reflection metadata, never cache instances.
    /// </summary>
    public static class BuildingReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // BuildingPanel static field
        private static Type _buildingPanelType = null;
        private static FieldInfo _currentBuildingField = null;
        private static bool _panelTypesCached = false;

        // Building base properties
        private static PropertyInfo _buildingModelProperty = null;
        private static PropertyInfo _buildingStateProperty = null;
        private static PropertyInfo _buildingIdProperty = null;
        private static PropertyInfo _buildingDisplayNameProperty = null;  // Building.DisplayName (string)
        private static bool _buildingTypesCached = false;

        // BuildingModel properties
        private static PropertyInfo _modelDescriptionProperty = null;
        private static bool _modelTypesCached = false;

        // BuildingState fields (they are fields, not properties)
        private static FieldInfo _stateFinishedField = null;
        private static FieldInfo _stateIsSleepingField = null;
        private static bool _stateTypesCached = false;

        // ProductionBuilding properties
        private static Type _productionBuildingType = null;
        private static PropertyInfo _workersProperty = null;
        private static PropertyInfo _productionStorageProperty = null;
        private static PropertyInfo _productionBuildingStateProperty = null;
        private static bool _productionTypesCached = false;

        // IWorkshop interface (for buildings with recipes)
        private static Type _workshopInterfaceType = null;
        private static PropertyInfo _workshopRecipesProperty = null;
        private static PropertyInfo _workshopIngredientsStorageProperty = null;  // IWorkshop.IngredientsStorage
        private static MethodInfo _switchProductionOfMethod = null;
        private static bool _workshopTypesCached = false;

        // BuildingIngredientsStorage (input goods storage)
        private static FieldInfo _ingredientsStorageGoodsField = null;  // BuildingIngredientsStorage.goods (GoodsCollection)
        private static FieldInfo _goodsCollectionGoodsField = null;  // GoodsCollection.goods (Dictionary<string, int>)
        private static bool _ingredientsStorageTypesCached = false;

        // Camp type (has recipes but doesn't implement IWorkshop)
        private static Type _campType = null;
        private static FieldInfo _campStateField = null;
        private static FieldInfo _campStateRecipesField = null;
        private static MethodInfo _campSwitchProductionOfMethod = null;
        private static FieldInfo _campStateModeField = null;  // CampState.mode (CampMode enum)
        private static MethodInfo _campSetModeMethod = null;  // Camp.SetMode(CampMode)
        private static bool _campTypesCached = false;

        // Farm type
        private static Type _farmType = null;
        private static FieldInfo _farmStateField = null;  // Farm.state
        private static MethodInfo _farmCountSownFieldsMethod = null;  // Farm.CountSownFieldsInRange()
        private static MethodInfo _farmCountPlowedFieldsMethod = null;  // Farm.CountPlownFieldsInRange() - note typo in game
        private static MethodInfo _farmCountAllFieldsMethod = null;  // Farm.CountAllReaveleadFieldsInRange()
        private static bool _farmTypesCached = false;

        // FishingHut type
        private static Type _fishingHutType = null;
        private static FieldInfo _fishingHutStateField = null;  // FishingHut.state
        private static FieldInfo _fishingHutModelField = null;  // FishingHut.model
        private static FieldInfo _fishingHutStateBaitModeField = null;  // FishingHutState.baitMode
        private static FieldInfo _fishingHutStateBaitChargesField = null;  // FishingHutState.baitChargesLeft
        private static FieldInfo _fishingHutStateRecipesField = null;  // FishingHutState.recipes
        private static MethodInfo _fishingHutChangeModeMethod = null;  // FishingHut.ChangeMode(FishmanBaitMode)
        private static MethodInfo _fishingHutSwitchProductionOfMethod = null;  // FishingHut.SwitchProductionOf(RecipeState)
        private static FieldInfo _fishingHutModelBaitIngredientField = null;  // FishingHutModel.baitIngredient
        private static bool _fishingHutTypesCached = false;

        // RecipeState fields (they are fields, not properties)
        private static FieldInfo _recipeActiveField = null;
        private static FieldInfo _recipeModelField = null;
        private static FieldInfo _recipePrioField = null;
        // WorkshopRecipeState fields
        private static FieldInfo _recipeLimitField = null;
        private static FieldInfo _recipeProductNameField = null;
        private static FieldInfo _recipeIngredientsField = null;  // IngredientState[][]
        private static bool _recipeTypesCached = false;

        // Recipe model info (for production time, grade)
        private static MethodInfo _settingsGetRecipeMethod = null;  // MB.Settings.GetRecipe(name)
        private static FieldInfo _recipeModelProductionTimeField = null;
        private static FieldInfo _recipeModelGradeField = null;
        private static FieldInfo _gradeModelLevelField = null;
        // Produced good info (from WorkshopRecipeModel.producedGood)
        private static FieldInfo _recipeModelProducedGoodField = null;  // GoodRef
        private static FieldInfo _goodRefAmountField = null;  // GoodRef.amount
        private static FieldInfo _goodRefGoodField = null;  // GoodRef.good (GoodModel)
        private static PropertyInfo _goodModelDisplayNameProperty = null;  // GoodModel.displayName
        private static bool _recipeModelTypesCached = false;

        // IngredientState fields
        private static FieldInfo _ingredientGoodField = null;
        private static FieldInfo _ingredientAllowedField = null;
        // Good struct fields (for ingredient amounts)
        private static FieldInfo _goodAmountField = null;  // Good.amount
        private static bool _ingredientTypesCached = false;

        // Building panel events
        private static PropertyInfo _onBuildingPanelShownProperty = null;
        private static PropertyInfo _onBuildingPanelClosedProperty = null;
        private static bool _eventTypesCached = false;

        // ActorsService for worker info
        private static PropertyInfo _actorsServiceProperty = null;
        private static MethodInfo _getActorMethod = null;
        private static bool _actorTypesCached = false;

        // Actor properties
        private static PropertyInfo _actorStateProperty = null;  // Actor.ActorState
        private static FieldInfo _villagerStateNameField = null;  // VillagerState.name
        private static FieldInfo _villagerStateRaceField = null;  // VillagerState.race
        private static MethodInfo _getTaskDescriptionMethod = null;  // Actor.GetTaskDescription()
        private static bool _actorPropertiesCached = false;

        // VillagersService for worker assignment
        private static PropertyInfo _villagersServiceProperty = null;
        private static MethodInfo _getDefaultProfessionAmountMethod = null;  // GetDefaultProfessionAmount(race)
        private static MethodInfo _getDefaultProfessionVillagerMethod = null;  // GetDefaultProfessionVillager(race, building)
        private static MethodInfo _setProfessionMethod = null;  // SetProfession(villager, profession, building, workplace)
        private static MethodInfo _releaseFromProfessionMethod = null;  // ReleaseFromProfession(villager)
        private static MethodInfo _getVillagerMethod = null;  // GetVillager(id)
        private static PropertyInfo _villagersServiceRacesProperty = null;  // Races dictionary
        private static bool _villagersServiceTypesCached = false;

        // ProductionBuilding for profession and workplaces
        private static PropertyInfo _professionProperty = null;  // ProductionBuilding.Profession
        private static PropertyInfo _workplacesProperty = null;  // ProductionBuilding.Workplaces
        private static bool _professionTypesCached = false;

        // BuildingStorage (ProductionStorage) for output goods
        private static PropertyInfo _storageGoodsProperty = null;  // BuildingStorage.Goods
        private static PropertyInfo _goodsCollectionGoodsProperty = null;  // BuildingGoodsCollection.goods (Dictionary<string, int>)
        private static MethodInfo _storageGetDeliveryStateMethod = null;  // BuildingGoodsCollection.GetDeliveryState(string)
        private static MethodInfo _storageSwitchForceDeliveryMethod = null;  // BuildingStorage.SwitchForceDelivery(string, GoodDeliveryState)
        private static MethodInfo _storageSwitchConstantForceDeliveryMethod = null;  // BuildingStorage.SwitchConstantForceDelivery(string, GoodDeliveryState)
        private static FieldInfo _deliveryStateForcedField = null;  // GoodDeliveryState.deliveryForced
        private static FieldInfo _deliveryStateConstantForcedField = null;  // GoodDeliveryState.constantDeliveryForced
        private static bool _storageTypesCached = false;

        // Hearth-specific
        private static Type _hearthType = null;
        private static FieldInfo _hearthStateField = null;  // Hearth.state
        private static FieldInfo _hearthModelField = null;  // Hearth.model
        private static FieldInfo _hearthStateBurningTimeLeftField = null;  // HearthState.burningTimeLeft
        private static FieldInfo _hearthStateCorruptionField = null;  // HearthState.corruption
        private static FieldInfo _hearthStateHubIndexField = null;  // HearthState.hubIndex
        private static FieldInfo _hearthStateWorkersField = null;  // HearthState.workers
        private static FieldInfo _hearthModelMaxBurningTimeField = null;  // HearthModel.maxBurningTime
        private static FieldInfo _hearthModelMinTimeToShowNoFuelField = null;  // HearthModel.minTimeToShowNoFuel
        private static MethodInfo _hearthIsMainHearthMethod = null;  // Hearth.IsMainHearth()
        private static MethodInfo _hearthGetRangeMethod = null;  // Hearth.GetRange()
        private static MethodInfo _hearthGetCorruptionRateMethod = null;  // Hearth.GetCorruptionRate()
        private static bool _hearthTypesCached = false;

        // House-specific
        private static Type _houseType = null;
        private static FieldInfo _houseStateField = null;  // House.state
        private static FieldInfo _houseModelField = null;  // House.model
        private static FieldInfo _houseStateResidentsField = null;  // HouseState.residents (List<int>)
        private static MethodInfo _houseGetHousingPlacesMethod = null;  // House.GetHousingPlaces()
        private static MethodInfo _houseGetMaxHousingPlacesMethod = null;  // House.GetMaxHousingPlaces()
        private static MethodInfo _houseIsFullMethod = null;  // House.IsFull()
        private static bool _houseTypesCached = false;

        // Relic-specific
        private static Type _relicType = null;
        private static FieldInfo _relicStateField = null;  // Relic.state
        private static FieldInfo _relicModelField = null;  // Relic.model
        private static FieldInfo _relicStateInvestigationStartedField = null;  // RelicState.investigationStarted
        private static FieldInfo _relicStateInvestigationFinishedField = null;  // RelicState.investigationFinished
        private static FieldInfo _relicStateWorkProgressField = null;  // RelicState.workProgress
        private static FieldInfo _relicStateRelicGoodsField = null;  // RelicState.relicGoods
        private static FieldInfo _relicStateRewardsField = null;  // RelicState.rewards
        private static FieldInfo _relicStateWorkersField = null;  // RelicState.workers
        private static MethodInfo _relicGetExpectedWorkingTimeLeftMethod = null;  // Relic.GetExpectedWorkingTimeLeft()
        private static MethodInfo _relicGetRequriedGoodsMethod = null;  // Relic.GetRequriedGoods()
        private static MethodInfo _relicGetCurrentDecisionPickedGoodForMethod = null;  // Relic.GetCurrentDecisionPickedGoodFor()
        private static PropertyInfo _relicDifficultyProperty = null;  // Relic.Difficulty
        private static bool _relicTypesCached = false;

        // Port-specific
        private static Type _portType = null;
        private static FieldInfo _portStateField = null;  // Port.state
        private static FieldInfo _portModelField = null;  // Port.model
        private static FieldInfo _portStateExpeditionLevelField = null;  // PortState.expeditionLevel
        private static FieldInfo _portStateAreRewardsWaitingField = null;  // PortState.areRewardsWaiting
        private static FieldInfo _portStateBlueprintRewardField = null;  // PortState.blueprintReward
        private static FieldInfo _portStatePerkRewardField = null;  // PortState.perkReward
        private static FieldInfo _portStateExpeditionGoodsField = null;  // PortState.expeditionGoods
        private static FieldInfo _portStateWorkersField = null;  // PortState.workers
        private static MethodInfo _portWasExpeditionStartedMethod = null;  // Port.WasExpeditionStarted()
        private static MethodInfo _portAreRewardsWaitingMethod = null;  // Port.AreRewardsWaiting()
        private static MethodInfo _portCalculateProgressMethod = null;  // Port.CalculateProgress()
        private static MethodInfo _portCalculateTimeLeftMethod = null;  // Port.CalculateTimeLeft()
        private static MethodInfo _portGetCurrentExpeditionMethod = null;  // Port.GetCurrentExpedition()
        private static MethodInfo _portGetPickedStriderGoodMethod = null;  // Port.GetPickedStriderGood(int)
        private static MethodInfo _portGetPickedCrewGoodMethod = null;  // Port.GetPickedCrewGood(int)
        private static bool _portTypesCached = false;

        // Decoration-specific
        private static Type _decorationType = null;
        private static bool _decorationTypesCached = false;

        // Storage-specific (main storage building)
        private static Type _storageType = null;
        private static bool _storageTypesCached2 = false;  // _storageTypesCached already used for BuildingStorage

        // Institution-specific (Tavern, Temple, etc.)
        private static Type _institutionType = null;
        private static FieldInfo _institutionStateField = null;  // Institution.state
        private static FieldInfo _institutionModelField = null;  // Institution.model
        private static FieldInfo _institutionStorageField = null;  // Institution.storage (BuildingStorage)
        private static FieldInfo _institutionStateRecipesField = null;  // InstitutionState.recipes
        private static FieldInfo _institutionModelRecipesField = null;  // InstitutionModel.recipes
        private static FieldInfo _institutionRecipeStatePickedGoodField = null;  // InstitutionRecipeState.pickedGood
        private static FieldInfo _institutionRecipeModelServedNeedField = null;  // InstitutionRecipeModel.servedNeed
        private static FieldInfo _institutionRecipeModelRequiredGoodsField = null;  // InstitutionRecipeModel.requiredGoods (GoodsSet)
        private static FieldInfo _institutionRecipeModelIsGoodConsumedField = null;  // InstitutionRecipeModel.isGoodConsumed
        private static MethodInfo _institutionChangeIngredientMethod = null;  // Institution.ChangeIngredientFor(recipeState, pickedGood)
        private static bool _institutionTypesCached = false;

        // Shrine-specific
        private static Type _shrineType = null;
        private static FieldInfo _shrineStateField = null;  // Shrine.state
        private static FieldInfo _shrineModelField = null;  // Shrine.model
        private static FieldInfo _shrineStateEffectsField = null;  // ShrineState.effects (ShrineEffectsState[])
        private static FieldInfo _shrineModelEffectsField = null;  // ShrineModel.effects (ShrineEffectsModel[])
        private static FieldInfo _shrineEffectsStateChargesLeftField = null;  // ShrineEffectsState.chargesLeft
        private static FieldInfo _shrineEffectsModelLabelField = null;  // ShrineEffectsModel.label (LocaText)
        private static FieldInfo _shrineEffectsModelChargesField = null;  // ShrineEffectsModel.charges
        private static FieldInfo _shrineEffectsModelEffectsField = null;  // ShrineEffectsModel.effects (EffectModel[])
        private static MethodInfo _shrineUseEffectMethod = null;  // Shrine.UseEffect(state, model, index)
        private static bool _shrineTypesCached = false;

        // Poro-specific
        private static Type _poroType = null;
        private static FieldInfo _poroStateField = null;  // Poro.state
        private static FieldInfo _poroModelField = null;  // Poro.model
        private static FieldInfo _poroStateNeedsField = null;  // PoroState.needs (PoroNeedState[])
        private static FieldInfo _poroModelNeedsField = null;  // PoroModel.needs (PoroNeedModel[])
        private static FieldInfo _poroStateHappinessField = null;  // PoroState.happiness
        private static FieldInfo _poroStateProductionProgressField = null;  // PoroState.productionProgress
        private static FieldInfo _poroStateProductField = null;  // PoroState.product (Good)
        private static FieldInfo _poroModelProductField = null;  // PoroModel.product (GoodRef)
        private static FieldInfo _poroModelMaxProductsField = null;  // PoroModel.maxProducts
        private static FieldInfo _poroNeedStateLevelField = null;  // PoroNeedState.level
        private static FieldInfo _poroNeedStatePickedGoodField = null;  // PoroNeedState.pickedGood
        private static FieldInfo _poroNeedModelDisplayNameField = null;  // PoroNeedModel.displayName (LocaText)
        private static FieldInfo _poroNeedModelGoodsField = null;  // PoroNeedModel.goods (GoodsSet)
        private static MethodInfo _poroCanFulfillMethod = null;  // Poro.CanFulfill(state, model)
        private static MethodInfo _poroFulfillMethod = null;  // Poro.Fulfill(state, model)
        private static MethodInfo _poroCanGatherProductsMethod = null;  // Poro.CanGatherProducts()
        private static MethodInfo _poroGatherProductsMethod = null;  // Poro.GatherProducts()
        private static MethodInfo _poroGoodChangedMethod = null;  // Poro.GoodChanged(state, goodIndex)
        private static MethodInfo _poroGetCurrentGoodForMethod = null;  // Poro.GetCurrentGoodFor(state, model)
        private static bool _poroTypesCached = false;

        // RainCatcher-specific
        private static Type _rainCatcherType = null;
        private static FieldInfo _rainCatcherStateField = null;  // RainCatcher.state
        private static FieldInfo _rainCatcherModelField = null;  // RainCatcher.model
        private static MethodInfo _rainCatcherGetCurrentWaterTypeMethod = null;  // RainCatcher.GetCurrentWaterType()
        private static bool _rainCatcherTypesCached = false;

        // Extractor-specific
        private static Type _extractorType = null;
        private static FieldInfo _extractorStateField = null;  // Extractor.state
        private static FieldInfo _extractorModelField = null;  // Extractor.model
        private static MethodInfo _extractorGetWaterTypeMethod = null;  // Extractor.GetWaterType()
        private static FieldInfo _extractorModelProductionTimeField = null;  // ExtractorModel.productionTime
        private static FieldInfo _extractorModelProducedAmountField = null;  // ExtractorModel.producedAmount
        private static bool _extractorTypesCached = false;

        // Hydrant-specific
        private static Type _hydrantType = null;
        private static FieldInfo _hydrantStateField = null;  // Hydrant.state
        private static FieldInfo _hydrantModelField = null;  // Hydrant.model
        private static bool _hydrantTypesCached = false;

        // WaterModel (for RainCatcher/Extractor)
        private static PropertyInfo _waterModelDisplayNameProperty = null;  // WaterModel.displayName
        private static FieldInfo _waterModelGoodField = null;  // WaterModel.good
        private static bool _waterModelTypesCached = false;

        // Cycle Abilities (from ConditionsState.cycleAbilities)
        private static FieldInfo _condCycleAbilitiesField = null;  // ConditionsState.cycleAbilities (List<CycleAbilityState>)
        private static FieldInfo _cycleAbilityModelField = null;  // CycleAbilityState.model (string)
        private static FieldInfo _cycleAbilityGameEffectField = null;  // CycleAbilityState.gameEffect (string)
        private static FieldInfo _cycleAbilityChargesField = null;  // CycleAbilityState.charges (int)
        private static bool _cycleAbilityTypesCached = false;

        // GameModelService (for effect models)
        private static PropertyInfo _gsGameModelServiceProperty = null;  // IGameServices.GameModelService
        private static MethodInfo _gmsGetEffectMethod = null;  // IGameModelService.GetEffect(string)
        private static FieldInfo _effectModelDisplayNameField = null;  // EffectModel.displayName (LocaText)
        private static MethodInfo _effectModelApplyMethod = null;  // EffectModel.Apply(context, source, sourceId)
        private static MethodInfo _effectModelCanBeDrawnMethod = null;  // EffectModel.CanBeDrawn()
        private static bool _gameModelServiceTypesCached = false;

        // BlightService (for hydrant fuel info)
        private static PropertyInfo _gsBlightServiceProperty = null;  // IGameServices.BlightService
        private static MethodInfo _blightCountFreeCystsMethod = null;  // IBlightService.CountGlobalFreeCysts()
        private static bool _blightServiceTypesCached = false;

        // Blight fuel config (from Settings.blightConfig)
        private static FieldInfo _settingsBlightConfigField = null;  // Settings.blightConfig
        private static FieldInfo _blightConfigBlightPostFuelField = null;  // BlightConfig.blightPostFuel (GoodRef)
        private static PropertyInfo _goodRefNameProperty = null;  // GoodRef.Name
        private static PropertyInfo _goodRefDisplayNameProperty = null;  // GoodRef.DisplayName
        private static bool _blightConfigTypesCached = false;

        // StorageService Main (for getting fuel amount)
        private static PropertyInfo _gsStorageService2Property = null;  // IGameServices.StorageService (duplicate to avoid collision)
        private static PropertyInfo _storageServiceMainProperty = null;  // IStorageService.Main
        private static MethodInfo _mainStorageGetAmountMethod = null;  // MainStorage.GetAmount(string)
        private static bool _storageService2TypesCached = false;

        // RainpunkService (for water tank levels)
        private static PropertyInfo _gsRainpunkServiceProperty = null;  // IGameServices.RainpunkService
        private static MethodInfo _rainpunkCountWaterLeftMethod = null;  // IRainpunkService.CountWaterLeft(WaterModel)
        private static MethodInfo _rainpunkCountTanksCapacityMethod = null;  // IRainpunkService.CountTanksCapacity(WaterModel)
        private static bool _rainpunkServiceTypesCached = false;

        // Rainpunk engine types (for workshop engine control)
        private static Type _workshopType = null;
        private static Type _workshopStateType = null;
        private static Type _rainpunkEngineStateType = null;
        private static Type _rainpunkEngineModelType = null;
        private static Type _buildingRainpunkModelType = null;
        private static FieldInfo _workshopStateField = null;  // Workshop.state
        private static FieldInfo _wsRainpunkUnlockedField = null;  // WorkshopState.rainpunkUnlocked
        private static FieldInfo _wsEnginesField = null;  // WorkshopState.engines
        private static FieldInfo _workshopModelField = null;  // Workshop.model
        private static FieldInfo _wmRainpunkField = null;  // WorkshopModel.rainpunk
        private static FieldInfo _brpEnginesField = null;  // BuildingRainpunkModel.engines
        private static FieldInfo _engineStateIndexField = null;  // RainpunkEngineState.index
        private static FieldInfo _engineStateLevelField = null;  // RainpunkEngineState.level
        private static FieldInfo _engineStateRequestedLevelField = null;  // RainpunkEngineState.requestedLevel
        private static FieldInfo _engineModelMaxLevelField = null;  // RainpunkEngineModel.maxLevel
        private static bool _rainpunkEngineTypesCached = false;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsurePanelTypes()
        {
            if (_panelTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _panelTypesCached = true;
                return;
            }

            try
            {
                _buildingPanelType = assembly.GetType("Eremite.Buildings.UI.BuildingPanel");
                if (_buildingPanelType != null)
                {
                    _currentBuildingField = _buildingPanelType.GetField("currentBuilding",
                        BindingFlags.Public | BindingFlags.Static);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached BuildingPanel.currentBuilding");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection panel types failed: {ex.Message}");
            }

            _panelTypesCached = true;
        }

        private static void EnsureBuildingTypes()
        {
            if (_buildingTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _buildingTypesCached = true;
                return;
            }

            try
            {
                var buildingType = assembly.GetType("Eremite.Buildings.Building");
                if (buildingType != null)
                {
                    _buildingModelProperty = buildingType.GetProperty("BuildingModel", GameReflection.PublicInstance);
                    _buildingStateProperty = buildingType.GetProperty("BuildingState", GameReflection.PublicInstance);
                    _buildingIdProperty = buildingType.GetProperty("Id", GameReflection.PublicInstance);
                    _buildingDisplayNameProperty = buildingType.GetProperty("DisplayName", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached Building properties");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection building types failed: {ex.Message}");
            }

            _buildingTypesCached = true;
        }

        private static void EnsureModelTypes()
        {
            if (_modelTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _modelTypesCached = true;
                return;
            }

            try
            {
                var modelType = assembly.GetType("Eremite.Buildings.BuildingModel");
                if (modelType != null)
                {
                    _modelDescriptionProperty = modelType.GetProperty("Description", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached BuildingModel properties");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection model types failed: {ex.Message}");
            }

            _modelTypesCached = true;
        }

        private static void EnsureStateTypes()
        {
            if (_stateTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _stateTypesCached = true;
                return;
            }

            try
            {
                var stateType = assembly.GetType("Eremite.Buildings.BuildingState");
                if (stateType != null)
                {
                    _stateFinishedField = stateType.GetField("finished", GameReflection.PublicInstance);
                    _stateIsSleepingField = stateType.GetField("isSleeping", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached BuildingState fields");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection state types failed: {ex.Message}");
            }

            _stateTypesCached = true;
        }

        private static void EnsureProductionTypes()
        {
            if (_productionTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _productionTypesCached = true;
                return;
            }

            try
            {
                _productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
                if (_productionBuildingType != null)
                {
                    _workersProperty = _productionBuildingType.GetProperty("Workers", GameReflection.PublicInstance);
                    _productionStorageProperty = _productionBuildingType.GetProperty("ProductionStorage", GameReflection.PublicInstance);
                    _productionBuildingStateProperty = _productionBuildingType.GetProperty("ProductionBuildingState", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached ProductionBuilding properties");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection production types failed: {ex.Message}");
            }

            _productionTypesCached = true;
        }

        private static void EnsureWorkshopTypes()
        {
            if (_workshopTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _workshopTypesCached = true;
                return;
            }

            try
            {
                _workshopInterfaceType = assembly.GetType("Eremite.Buildings.IWorkshop");
                if (_workshopInterfaceType != null)
                {
                    _workshopRecipesProperty = _workshopInterfaceType.GetProperty("Recipes", GameReflection.PublicInstance);
                    _workshopIngredientsStorageProperty = _workshopInterfaceType.GetProperty("IngredientsStorage", GameReflection.PublicInstance);
                    _switchProductionOfMethod = _workshopInterfaceType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached IWorkshop interface");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection workshop types failed: {ex.Message}");
            }

            _workshopTypesCached = true;
        }

        private static void EnsureCampTypes()
        {
            if (_campTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _campTypesCached = true;
                return;
            }

            try
            {
                _campType = assembly.GetType("Eremite.Buildings.Camp");
                if (_campType != null)
                {
                    _campStateField = _campType.GetField("state", GameReflection.PublicInstance);
                    _campSwitchProductionOfMethod = _campType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
                    _campSetModeMethod = _campType.GetMethod("SetMode", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached Camp type");
                }

                var campStateType = assembly.GetType("Eremite.Buildings.CampState");
                if (campStateType != null)
                {
                    _campStateRecipesField = campStateType.GetField("recipes", GameReflection.PublicInstance);
                    _campStateModeField = campStateType.GetField("mode", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached CampState fields");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection camp types failed: {ex.Message}");
            }

            _campTypesCached = true;
        }

        private static void EnsureFarmTypes()
        {
            if (_farmTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _farmTypesCached = true;
                return;
            }

            try
            {
                _farmType = assembly.GetType("Eremite.Buildings.Farm");
                if (_farmType != null)
                {
                    _farmStateField = _farmType.GetField("state", GameReflection.PublicInstance);
                    _farmCountSownFieldsMethod = _farmType.GetMethod("CountSownFieldsInRange", GameReflection.PublicInstance);
                    _farmCountPlowedFieldsMethod = _farmType.GetMethod("CountPlownFieldsInRange", GameReflection.PublicInstance);  // Note: typo in game code
                    _farmCountAllFieldsMethod = _farmType.GetMethod("CountAllReaveleadFieldsInRange", GameReflection.PublicInstance);  // Note: typo in game code
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached Farm type");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection farm types failed: {ex.Message}");
            }

            _farmTypesCached = true;
        }

        private static void EnsureFishingHutTypes()
        {
            if (_fishingHutTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _fishingHutTypesCached = true;
                return;
            }

            try
            {
                _fishingHutType = assembly.GetType("Eremite.Buildings.FishingHut");
                if (_fishingHutType != null)
                {
                    _fishingHutStateField = _fishingHutType.GetField("state", GameReflection.PublicInstance);
                    _fishingHutModelField = _fishingHutType.GetField("model", GameReflection.PublicInstance);
                    _fishingHutChangeModeMethod = _fishingHutType.GetMethod("ChangeMode", GameReflection.PublicInstance);
                    _fishingHutSwitchProductionOfMethod = _fishingHutType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached FishingHut type");
                }

                var fishingHutStateType = assembly.GetType("Eremite.Buildings.FishingHutState");
                if (fishingHutStateType != null)
                {
                    _fishingHutStateBaitModeField = fishingHutStateType.GetField("baitMode", GameReflection.PublicInstance);
                    _fishingHutStateBaitChargesField = fishingHutStateType.GetField("baitChargesLeft", GameReflection.PublicInstance);
                    _fishingHutStateRecipesField = fishingHutStateType.GetField("recipes", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached FishingHutState fields");
                }

                var fishingHutModelType = assembly.GetType("Eremite.Buildings.FishingHutModel");
                if (fishingHutModelType != null)
                {
                    _fishingHutModelBaitIngredientField = fishingHutModelType.GetField("baitIngredient", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached FishingHutModel fields");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection fishing hut types failed: {ex.Message}");
            }

            _fishingHutTypesCached = true;
        }

        private static void EnsureRecipeTypes()
        {
            if (_recipeTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _recipeTypesCached = true;
                return;
            }

            try
            {
                // RecipeState fields
                var recipeStateType = assembly.GetType("Eremite.Buildings.RecipeState");
                if (recipeStateType != null)
                {
                    _recipeActiveField = recipeStateType.GetField("active", GameReflection.PublicInstance);
                    _recipeModelField = recipeStateType.GetField("model", GameReflection.PublicInstance);
                    _recipePrioField = recipeStateType.GetField("prio", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached RecipeState fields");
                }

                // WorkshopRecipeState fields
                var workshopRecipeStateType = assembly.GetType("Eremite.Buildings.WorkshopRecipeState");
                if (workshopRecipeStateType != null)
                {
                    _recipeLimitField = workshopRecipeStateType.GetField("limit", GameReflection.PublicInstance);
                    _recipeProductNameField = workshopRecipeStateType.GetField("productName", GameReflection.PublicInstance);
                    _recipeIngredientsField = workshopRecipeStateType.GetField("ingredients", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached WorkshopRecipeState fields");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection recipe types failed: {ex.Message}");
            }

            _recipeTypesCached = true;
        }

        private static void EnsureRecipeModelTypes()
        {
            if (_recipeModelTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _recipeModelTypesCached = true;
                return;
            }

            try
            {
                // Settings.GetRecipe method
                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetRecipeMethod = settingsType.GetMethod("GetRecipe", GameReflection.PublicInstance);
                }

                // WorkshopRecipeModel fields
                var recipeModelType = assembly.GetType("Eremite.Buildings.WorkshopRecipeModel");
                if (recipeModelType != null)
                {
                    _recipeModelProductionTimeField = recipeModelType.GetField("productionTime", GameReflection.PublicInstance);
                    _recipeModelProducedGoodField = recipeModelType.GetField("producedGood", GameReflection.PublicInstance);
                }

                // RecipeModel.grade field (in base class)
                var baseRecipeModelType = assembly.GetType("Eremite.Buildings.RecipeModel");
                if (baseRecipeModelType != null)
                {
                    _recipeModelGradeField = baseRecipeModelType.GetField("grade", GameReflection.PublicInstance);
                }

                // RecipeGradeModel.level field
                var gradeModelType = assembly.GetType("Eremite.Buildings.RecipeGradeModel");
                if (gradeModelType != null)
                {
                    _gradeModelLevelField = gradeModelType.GetField("level", GameReflection.PublicInstance);
                }

                // GoodRef fields (for produced good info)
                var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
                if (goodRefType != null)
                {
                    _goodRefAmountField = goodRefType.GetField("amount", GameReflection.PublicInstance);
                    _goodRefGoodField = goodRefType.GetField("good", GameReflection.PublicInstance);
                }

                // GoodModel displayName property
                var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
                if (goodModelType != null)
                {
                    _goodModelDisplayNameProperty = goodModelType.GetProperty("displayName", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached RecipeModel types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection recipe model types failed: {ex.Message}");
            }

            _recipeModelTypesCached = true;
        }

        private static void EnsureIngredientTypes()
        {
            if (_ingredientTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _ingredientTypesCached = true;
                return;
            }

            try
            {
                var ingredientStateType = assembly.GetType("Eremite.Buildings.IngredientState");
                if (ingredientStateType != null)
                {
                    _ingredientGoodField = ingredientStateType.GetField("good", GameReflection.PublicInstance);
                    _ingredientAllowedField = ingredientStateType.GetField("allowed", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached IngredientState fields");
                }

                // Good struct has amount field
                var goodType = assembly.GetType("Eremite.Model.Good");
                if (goodType != null)
                {
                    _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached Good.amount field");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection ingredient types failed: {ex.Message}");
            }

            _ingredientTypesCached = true;
        }

        private static void EnsureEventTypes()
        {
            if (_eventTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _eventTypesCached = true;
                return;
            }

            try
            {
                var blackboardType = assembly.GetType("Eremite.Services.IGameBlackboardService");
                if (blackboardType != null)
                {
                    _onBuildingPanelShownProperty = blackboardType.GetProperty("OnBuildingPanelShown", GameReflection.PublicInstance);
                    _onBuildingPanelClosedProperty = blackboardType.GetProperty("OnBuildingPanelClosed", GameReflection.PublicInstance);
                    Debug.Log("[ATSAccessibility] BuildingReflection: Cached building panel event properties");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection event types failed: {ex.Message}");
            }

            _eventTypesCached = true;
        }

        private static void EnsureActorTypes()
        {
            if (_actorTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _actorTypesCached = true;
                return;
            }

            try
            {
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _actorsServiceProperty = gameServicesType.GetProperty("ActorsService", GameReflection.PublicInstance);
                }

                var actorsServiceType = assembly.GetType("Eremite.Services.IActorsService");
                if (actorsServiceType != null)
                {
                    _getActorMethod = actorsServiceType.GetMethod("GetActor", new[] { typeof(int) });
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached ActorsService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection actor types failed: {ex.Message}");
            }

            _actorTypesCached = true;
        }

        private static void EnsureActorProperties()
        {
            if (_actorPropertiesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _actorPropertiesCached = true;
                return;
            }

            try
            {
                var actorType = assembly.GetType("Eremite.Characters.Actor");
                if (actorType != null)
                {
                    // Actor.ActorState property (returns VillagerState for villagers)
                    _actorStateProperty = actorType.GetProperty("ActorState", GameReflection.PublicInstance);
                    // Actor.GetTaskDescription() method
                    _getTaskDescriptionMethod = actorType.GetMethod("GetTaskDescription", GameReflection.PublicInstance);
                }

                // VillagerState fields (stores the villager's name and race)
                var villagerStateType = assembly.GetType("Eremite.Characters.Villagers.VillagerState");
                if (villagerStateType != null)
                {
                    _villagerStateNameField = villagerStateType.GetField("name", GameReflection.PublicInstance);
                    _villagerStateRaceField = villagerStateType.GetField("race", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Actor properties");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection actor properties failed: {ex.Message}");
            }

            _actorPropertiesCached = true;
        }

        private static void EnsureVillagersServiceTypes()
        {
            if (_villagersServiceTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _villagersServiceTypesCached = true;
                return;
            }

            try
            {
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _villagersServiceProperty = gameServicesType.GetProperty("VillagersService", GameReflection.PublicInstance);
                }

                var villagersServiceType = assembly.GetType("Eremite.Services.IVillagersService");
                if (villagersServiceType != null)
                {
                    _getDefaultProfessionAmountMethod = villagersServiceType.GetMethod("GetDefaultProfessionAmount", new[] { typeof(string) });
                    _releaseFromProfessionMethod = villagersServiceType.GetMethod("ReleaseFromProfession", GameReflection.PublicInstance);
                    _getVillagerMethod = villagersServiceType.GetMethod("GetVillager", new[] { typeof(int) });
                    _villagersServiceRacesProperty = villagersServiceType.GetProperty("Races", GameReflection.PublicInstance);

                    // These methods have specific parameter types
                    var villagerType = assembly.GetType("Eremite.Characters.Villagers.Villager");
                    var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
                    if (villagerType != null && productionBuildingType != null)
                    {
                        _getDefaultProfessionVillagerMethod = villagersServiceType.GetMethod("GetDefaultProfessionVillager",
                            new[] { typeof(string), productionBuildingType });
                        _setProfessionMethod = villagersServiceType.GetMethod("SetProfession",
                            new[] { villagerType, typeof(string), productionBuildingType, typeof(int), typeof(bool) });
                    }
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached VillagersService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection villagers service types failed: {ex.Message}");
            }

            _villagersServiceTypesCached = true;
        }

        private static void EnsureProfessionTypes()
        {
            if (_professionTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _professionTypesCached = true;
                return;
            }

            try
            {
                var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
                if (productionBuildingType != null)
                {
                    _professionProperty = productionBuildingType.GetProperty("Profession", GameReflection.PublicInstance);
                    _workplacesProperty = productionBuildingType.GetProperty("Workplaces", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Profession types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection profession types failed: {ex.Message}");
            }

            _professionTypesCached = true;
        }

        private static void EnsureStorageTypes()
        {
            if (_storageTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _storageTypesCached = true;
                return;
            }

            try
            {
                var buildingStorageType = assembly.GetType("Eremite.Buildings.BuildingStorage");
                if (buildingStorageType != null)
                {
                    _storageGoodsProperty = buildingStorageType.GetProperty("Goods", GameReflection.PublicInstance);
                    _storageSwitchForceDeliveryMethod = buildingStorageType.GetMethod("SwitchForceDelivery", GameReflection.PublicInstance);
                    _storageSwitchConstantForceDeliveryMethod = buildingStorageType.GetMethod("SwitchConstantForceDelivery", GameReflection.PublicInstance);
                }

                var goodsCollectionType = assembly.GetType("Eremite.Buildings.BuildingGoodsCollection");
                if (goodsCollectionType != null)
                {
                    _goodsCollectionGoodsProperty = goodsCollectionType.GetProperty("goods", GameReflection.PublicInstance);
                    _storageGetDeliveryStateMethod = goodsCollectionType.GetMethod("GetDeliveryState", GameReflection.PublicInstance);
                }

                var deliveryStateType = assembly.GetType("Eremite.Buildings.GoodDeliveryState");
                if (deliveryStateType != null)
                {
                    _deliveryStateForcedField = deliveryStateType.GetField("deliveryForced", GameReflection.PublicInstance);
                    _deliveryStateConstantForcedField = deliveryStateType.GetField("constantDeliveryForced", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Storage types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection storage types failed: {ex.Message}");
            }

            _storageTypesCached = true;
        }

        private static void EnsureIngredientsStorageTypes()
        {
            if (_ingredientsStorageTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _ingredientsStorageTypesCached = true;
                return;
            }

            try
            {
                var ingredientsStorageType = assembly.GetType("Eremite.Buildings.BuildingIngredientsStorage");
                if (ingredientsStorageType != null)
                {
                    _ingredientsStorageGoodsField = ingredientsStorageType.GetField("goods", GameReflection.PublicInstance);
                }

                var goodsCollectionType = assembly.GetType("Eremite.GoodsCollection");
                if (goodsCollectionType != null)
                {
                    _goodsCollectionGoodsField = goodsCollectionType.GetField("goods", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached IngredientsStorage types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection ingredients storage types failed: {ex.Message}");
            }

            _ingredientsStorageTypesCached = true;
        }

        private static void EnsureHearthTypes()
        {
            if (_hearthTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _hearthTypesCached = true;
                return;
            }

            try
            {
                _hearthType = assembly.GetType("Eremite.Buildings.Hearth");
                if (_hearthType != null)
                {
                    _hearthStateField = _hearthType.GetField("state", GameReflection.PublicInstance);
                    _hearthModelField = _hearthType.GetField("model", GameReflection.PublicInstance);
                    _hearthIsMainHearthMethod = _hearthType.GetMethod("IsMainHearth", GameReflection.PublicInstance);
                    _hearthGetRangeMethod = _hearthType.GetMethod("GetRange", GameReflection.PublicInstance);
                    _hearthGetCorruptionRateMethod = _hearthType.GetMethod("GetCorruptionRate", GameReflection.PublicInstance);
                }

                var hearthStateType = assembly.GetType("Eremite.Buildings.HearthState");
                if (hearthStateType != null)
                {
                    _hearthStateBurningTimeLeftField = hearthStateType.GetField("burningTimeLeft", GameReflection.PublicInstance);
                    _hearthStateCorruptionField = hearthStateType.GetField("corruption", GameReflection.PublicInstance);
                    _hearthStateHubIndexField = hearthStateType.GetField("hubIndex", GameReflection.PublicInstance);
                    _hearthStateWorkersField = hearthStateType.GetField("workers", GameReflection.PublicInstance);
                }

                var hearthModelType = assembly.GetType("Eremite.Buildings.HearthModel");
                if (hearthModelType != null)
                {
                    _hearthModelMaxBurningTimeField = hearthModelType.GetField("maxBurningTime", GameReflection.PublicInstance);
                    _hearthModelMinTimeToShowNoFuelField = hearthModelType.GetField("minTimeToShowNoFuel", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Hearth types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection hearth types failed: {ex.Message}");
            }

            _hearthTypesCached = true;
        }

        private static void EnsureHouseTypes()
        {
            if (_houseTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _houseTypesCached = true;
                return;
            }

            try
            {
                _houseType = assembly.GetType("Eremite.Buildings.House");
                if (_houseType != null)
                {
                    _houseStateField = _houseType.GetField("state", GameReflection.PublicInstance);
                    _houseModelField = _houseType.GetField("model", GameReflection.PublicInstance);
                    _houseGetHousingPlacesMethod = _houseType.GetMethod("GetHousingPlaces", GameReflection.PublicInstance);
                    _houseGetMaxHousingPlacesMethod = _houseType.GetMethod("GetMaxHousingPlaces", GameReflection.PublicInstance);
                    _houseIsFullMethod = _houseType.GetMethod("IsFull", GameReflection.PublicInstance);
                }

                var houseStateType = assembly.GetType("Eremite.Buildings.HouseState");
                if (houseStateType != null)
                {
                    _houseStateResidentsField = houseStateType.GetField("residents", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached House types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection house types failed: {ex.Message}");
            }

            _houseTypesCached = true;
        }

        private static void EnsureRelicTypes()
        {
            if (_relicTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _relicTypesCached = true;
                return;
            }

            try
            {
                _relicType = assembly.GetType("Eremite.Buildings.Relic");
                if (_relicType != null)
                {
                    _relicStateField = _relicType.GetField("state", GameReflection.PublicInstance);
                    _relicModelField = _relicType.GetField("model", GameReflection.PublicInstance);
                    _relicGetExpectedWorkingTimeLeftMethod = _relicType.GetMethod("GetExpectedWorkingTimeLeft", GameReflection.PublicInstance);
                    _relicGetRequriedGoodsMethod = _relicType.GetMethod("GetRequriedGoods", GameReflection.PublicInstance);
                    _relicGetCurrentDecisionPickedGoodForMethod = _relicType.GetMethod("GetCurrentDecisionPickedGoodFor", GameReflection.PublicInstance);
                    _relicDifficultyProperty = _relicType.GetProperty("Difficulty", GameReflection.PublicInstance);
                }

                var relicStateType = assembly.GetType("Eremite.Buildings.RelicState");
                if (relicStateType != null)
                {
                    _relicStateInvestigationStartedField = relicStateType.GetField("investigationStarted", GameReflection.PublicInstance);
                    _relicStateInvestigationFinishedField = relicStateType.GetField("investigationFinished", GameReflection.PublicInstance);
                    _relicStateWorkProgressField = relicStateType.GetField("workProgress", GameReflection.PublicInstance);
                    _relicStateRelicGoodsField = relicStateType.GetField("relicGoods", GameReflection.PublicInstance);
                    _relicStateRewardsField = relicStateType.GetField("rewards", GameReflection.PublicInstance);
                    _relicStateWorkersField = relicStateType.GetField("workers", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Relic types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection relic types failed: {ex.Message}");
            }

            _relicTypesCached = true;
        }

        private static void EnsurePortTypes()
        {
            if (_portTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _portTypesCached = true;
                return;
            }

            try
            {
                _portType = assembly.GetType("Eremite.Buildings.Port");
                if (_portType != null)
                {
                    _portStateField = _portType.GetField("state", GameReflection.PublicInstance);
                    _portModelField = _portType.GetField("model", GameReflection.PublicInstance);
                    _portWasExpeditionStartedMethod = _portType.GetMethod("WasExpeditionStarted", GameReflection.PublicInstance);
                    _portAreRewardsWaitingMethod = _portType.GetMethod("AreRewardsWaiting", GameReflection.PublicInstance);
                    _portCalculateProgressMethod = _portType.GetMethod("CalculateProgress", GameReflection.PublicInstance);
                    _portCalculateTimeLeftMethod = _portType.GetMethod("CalculateTimeLeft", GameReflection.PublicInstance);
                    _portGetCurrentExpeditionMethod = _portType.GetMethod("GetCurrentExpedition", GameReflection.PublicInstance);
                    _portGetPickedStriderGoodMethod = _portType.GetMethod("GetPickedStriderGood", GameReflection.PublicInstance);
                    _portGetPickedCrewGoodMethod = _portType.GetMethod("GetPickedCrewGood", GameReflection.PublicInstance);
                }

                var portStateType = assembly.GetType("Eremite.Buildings.PortState");
                if (portStateType != null)
                {
                    _portStateExpeditionLevelField = portStateType.GetField("expeditionLevel", GameReflection.PublicInstance);
                    _portStateAreRewardsWaitingField = portStateType.GetField("areRewardsWaiting", GameReflection.PublicInstance);
                    _portStateBlueprintRewardField = portStateType.GetField("blueprintReward", GameReflection.PublicInstance);
                    _portStatePerkRewardField = portStateType.GetField("perkReward", GameReflection.PublicInstance);
                    _portStateExpeditionGoodsField = portStateType.GetField("expeditionGoods", GameReflection.PublicInstance);
                    _portStateWorkersField = portStateType.GetField("workers", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Port types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection port types failed: {ex.Message}");
            }

            _portTypesCached = true;
        }

        private static void EnsureDecorationType()
        {
            if (_decorationTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _decorationTypesCached = true;
                return;
            }

            try
            {
                _decorationType = assembly.GetType("Eremite.Buildings.Decoration");
                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Decoration type");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection decoration type failed: {ex.Message}");
            }

            _decorationTypesCached = true;
        }

        private static void EnsureStorageType2()
        {
            if (_storageTypesCached2) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _storageTypesCached2 = true;
                return;
            }

            try
            {
                _storageType = assembly.GetType("Eremite.Buildings.Storage");
                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Storage building type");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection storage type failed: {ex.Message}");
            }

            _storageTypesCached2 = true;
        }

        private static void EnsureInstitutionTypes()
        {
            if (_institutionTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _institutionTypesCached = true;
                return;
            }

            try
            {
                _institutionType = assembly.GetType("Eremite.Buildings.Institution");
                if (_institutionType != null)
                {
                    _institutionStateField = _institutionType.GetField("state", GameReflection.PublicInstance);
                    _institutionModelField = _institutionType.GetField("model", GameReflection.PublicInstance);
                    _institutionStorageField = _institutionType.GetField("storage", GameReflection.PublicInstance);
                    _institutionChangeIngredientMethod = _institutionType.GetMethod("ChangeIngredientFor", GameReflection.PublicInstance);
                }

                var institutionStateType = assembly.GetType("Eremite.Buildings.InstitutionState");
                if (institutionStateType != null)
                {
                    _institutionStateRecipesField = institutionStateType.GetField("recipes", GameReflection.PublicInstance);
                }

                var institutionModelType = assembly.GetType("Eremite.Buildings.InstitutionModel");
                if (institutionModelType != null)
                {
                    _institutionModelRecipesField = institutionModelType.GetField("recipes", GameReflection.PublicInstance);
                }

                var institutionRecipeStateType = assembly.GetType("Eremite.Buildings.InstitutionRecipeState");
                if (institutionRecipeStateType != null)
                {
                    _institutionRecipeStatePickedGoodField = institutionRecipeStateType.GetField("pickedGood", GameReflection.PublicInstance);
                }

                var institutionRecipeModelType = assembly.GetType("Eremite.Buildings.InstitutionRecipeModel");
                if (institutionRecipeModelType != null)
                {
                    _institutionRecipeModelServedNeedField = institutionRecipeModelType.GetField("servedNeed", GameReflection.PublicInstance);
                    _institutionRecipeModelRequiredGoodsField = institutionRecipeModelType.GetField("requiredGoods", GameReflection.PublicInstance);
                    _institutionRecipeModelIsGoodConsumedField = institutionRecipeModelType.GetField("isGoodConsumed", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Institution types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection institution types failed: {ex.Message}");
            }

            _institutionTypesCached = true;
        }

        private static void EnsureShrineTypes()
        {
            if (_shrineTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _shrineTypesCached = true;
                return;
            }

            try
            {
                _shrineType = assembly.GetType("Eremite.Buildings.Shrine");
                if (_shrineType != null)
                {
                    _shrineStateField = _shrineType.GetField("state", GameReflection.PublicInstance);
                    _shrineModelField = _shrineType.GetField("model", GameReflection.PublicInstance);
                    _shrineUseEffectMethod = _shrineType.GetMethod("UseEffect", GameReflection.PublicInstance);
                }

                var shrineStateType = assembly.GetType("Eremite.Buildings.ShrineState");
                if (shrineStateType != null)
                {
                    _shrineStateEffectsField = shrineStateType.GetField("effects", GameReflection.PublicInstance);
                }

                var shrineModelType = assembly.GetType("Eremite.Buildings.ShrineModel");
                if (shrineModelType != null)
                {
                    _shrineModelEffectsField = shrineModelType.GetField("effects", GameReflection.PublicInstance);
                }

                var shrineEffectsStateType = assembly.GetType("Eremite.Buildings.ShrineEffectsState");
                if (shrineEffectsStateType != null)
                {
                    _shrineEffectsStateChargesLeftField = shrineEffectsStateType.GetField("chargesLeft", GameReflection.PublicInstance);
                }

                var shrineEffectsModelType = assembly.GetType("Eremite.Buildings.ShrineEffectsModel");
                if (shrineEffectsModelType != null)
                {
                    _shrineEffectsModelLabelField = shrineEffectsModelType.GetField("label", GameReflection.PublicInstance);
                    _shrineEffectsModelChargesField = shrineEffectsModelType.GetField("charges", GameReflection.PublicInstance);
                    _shrineEffectsModelEffectsField = shrineEffectsModelType.GetField("effects", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Shrine types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection shrine types failed: {ex.Message}");
            }

            _shrineTypesCached = true;
        }

        private static void EnsurePoroTypes()
        {
            if (_poroTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _poroTypesCached = true;
                return;
            }

            try
            {
                _poroType = assembly.GetType("Eremite.Buildings.Poro");
                if (_poroType != null)
                {
                    _poroStateField = _poroType.GetField("state", GameReflection.PublicInstance);
                    _poroModelField = _poroType.GetField("model", GameReflection.PublicInstance);
                    _poroCanFulfillMethod = _poroType.GetMethod("CanFulfill", GameReflection.PublicInstance);
                    _poroFulfillMethod = _poroType.GetMethod("Fulfill", GameReflection.PublicInstance);
                    _poroCanGatherProductsMethod = _poroType.GetMethod("CanGatherProducts", GameReflection.PublicInstance);
                    _poroGatherProductsMethod = _poroType.GetMethod("GatherProducts", GameReflection.PublicInstance);
                    _poroGoodChangedMethod = _poroType.GetMethod("GoodChanged", GameReflection.PublicInstance);
                    _poroGetCurrentGoodForMethod = _poroType.GetMethod("GetCurrentGoodFor", GameReflection.PublicInstance);
                }

                var poroStateType = assembly.GetType("Eremite.Buildings.PoroState");
                if (poroStateType != null)
                {
                    _poroStateNeedsField = poroStateType.GetField("needs", GameReflection.PublicInstance);
                    _poroStateHappinessField = poroStateType.GetField("happiness", GameReflection.PublicInstance);
                    _poroStateProductionProgressField = poroStateType.GetField("productionProgress", GameReflection.PublicInstance);
                    _poroStateProductField = poroStateType.GetField("product", GameReflection.PublicInstance);
                }

                var poroModelType = assembly.GetType("Eremite.Buildings.PoroModel");
                if (poroModelType != null)
                {
                    _poroModelNeedsField = poroModelType.GetField("needs", GameReflection.PublicInstance);
                    _poroModelProductField = poroModelType.GetField("product", GameReflection.PublicInstance);
                    _poroModelMaxProductsField = poroModelType.GetField("maxProducts", GameReflection.PublicInstance);
                }

                var poroNeedStateType = assembly.GetType("Eremite.Buildings.PoroNeedState");
                if (poroNeedStateType != null)
                {
                    _poroNeedStateLevelField = poroNeedStateType.GetField("level", GameReflection.PublicInstance);
                    _poroNeedStatePickedGoodField = poroNeedStateType.GetField("pickedGood", GameReflection.PublicInstance);
                }

                var poroNeedModelType = assembly.GetType("Eremite.Buildings.PoroNeedModel");
                if (poroNeedModelType != null)
                {
                    _poroNeedModelDisplayNameField = poroNeedModelType.GetField("displayName", GameReflection.PublicInstance);
                    _poroNeedModelGoodsField = poroNeedModelType.GetField("goods", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Poro types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection poro types failed: {ex.Message}");
            }

            _poroTypesCached = true;
        }

        private static void EnsureRainCatcherTypes()
        {
            if (_rainCatcherTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _rainCatcherTypesCached = true;
                return;
            }

            try
            {
                _rainCatcherType = assembly.GetType("Eremite.Buildings.RainCatcher");
                if (_rainCatcherType != null)
                {
                    _rainCatcherStateField = _rainCatcherType.GetField("state", GameReflection.PublicInstance);
                    _rainCatcherModelField = _rainCatcherType.GetField("model", GameReflection.PublicInstance);
                    _rainCatcherGetCurrentWaterTypeMethod = _rainCatcherType.GetMethod("GetCurrentWaterType", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached RainCatcher types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection raincatcher types failed: {ex.Message}");
            }

            _rainCatcherTypesCached = true;
        }

        private static void EnsureExtractorTypes()
        {
            if (_extractorTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _extractorTypesCached = true;
                return;
            }

            try
            {
                _extractorType = assembly.GetType("Eremite.Buildings.Extractor");
                if (_extractorType != null)
                {
                    _extractorStateField = _extractorType.GetField("state", GameReflection.PublicInstance);
                    _extractorModelField = _extractorType.GetField("model", GameReflection.PublicInstance);
                    _extractorGetWaterTypeMethod = _extractorType.GetMethod("GetWaterType", GameReflection.PublicInstance);
                }

                var extractorModelType = assembly.GetType("Eremite.Buildings.ExtractorModel");
                if (extractorModelType != null)
                {
                    _extractorModelProductionTimeField = extractorModelType.GetField("productionTime", GameReflection.PublicInstance);
                    _extractorModelProducedAmountField = extractorModelType.GetField("producedAmount", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Extractor types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection extractor types failed: {ex.Message}");
            }

            _extractorTypesCached = true;
        }

        private static void EnsureHydrantTypes()
        {
            if (_hydrantTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _hydrantTypesCached = true;
                return;
            }

            try
            {
                _hydrantType = assembly.GetType("Eremite.Buildings.Hydrant");
                if (_hydrantType != null)
                {
                    _hydrantStateField = _hydrantType.GetField("state", GameReflection.PublicInstance);
                    _hydrantModelField = _hydrantType.GetField("model", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached Hydrant types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection hydrant types failed: {ex.Message}");
            }

            _hydrantTypesCached = true;
        }

        private static void EnsureWaterModelTypes()
        {
            if (_waterModelTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _waterModelTypesCached = true;
                return;
            }

            try
            {
                var waterModelType = assembly.GetType("Eremite.Model.WaterModel");
                if (waterModelType != null)
                {
                    _waterModelDisplayNameProperty = waterModelType.GetProperty("displayName", GameReflection.PublicInstance);
                    _waterModelGoodField = waterModelType.GetField("good", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached WaterModel types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection watermodel types failed: {ex.Message}");
            }

            _waterModelTypesCached = true;
        }

        private static void EnsureCycleAbilityTypes()
        {
            if (_cycleAbilityTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _cycleAbilityTypesCached = true;
                return;
            }

            try
            {
                // ConditionsState.cycleAbilities field
                var conditionsStateType = assembly.GetType("Eremite.Model.State.ConditionsState");
                if (conditionsStateType != null)
                {
                    _condCycleAbilitiesField = conditionsStateType.GetField("cycleAbilities", GameReflection.PublicInstance);
                }

                // CycleAbilityState fields
                var cycleAbilityStateType = assembly.GetType("Eremite.WorldMap.CycleAbilityState");
                if (cycleAbilityStateType != null)
                {
                    _cycleAbilityModelField = cycleAbilityStateType.GetField("model", GameReflection.PublicInstance);
                    _cycleAbilityGameEffectField = cycleAbilityStateType.GetField("gameEffect", GameReflection.PublicInstance);
                    _cycleAbilityChargesField = cycleAbilityStateType.GetField("charges", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached CycleAbility types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection cycle ability types failed: {ex.Message}");
            }

            _cycleAbilityTypesCached = true;
        }

        private static void EnsureGameModelServiceTypes()
        {
            if (_gameModelServiceTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _gameModelServiceTypesCached = true;
                return;
            }

            try
            {
                // IGameServices.GameModelService
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsGameModelServiceProperty = gameServicesType.GetProperty("GameModelService", GameReflection.PublicInstance);
                }

                // IGameModelService.GetEffect
                var gameModelServiceType = assembly.GetType("Eremite.Services.IGameModelService");
                if (gameModelServiceType != null)
                {
                    _gmsGetEffectMethod = gameModelServiceType.GetMethod("GetEffect", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
                }

                // EffectModel.displayName and Apply
                var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    _effectModelDisplayNameField = effectModelType.GetField("displayName", GameReflection.NonPublicInstance);
                    _effectModelCanBeDrawnMethod = effectModelType.GetMethod("CanBeDrawn", GameReflection.PublicInstance);
                    // Apply method has signature: Apply(EffectContextType, string, int)
                    var effectContextType = assembly.GetType("Eremite.Model.Effects.EffectContextType");
                    if (effectContextType != null)
                    {
                        _effectModelApplyMethod = effectModelType.GetMethod("Apply", GameReflection.PublicInstance, null,
                            new[] { effectContextType, typeof(string), typeof(int) }, null);
                    }
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached GameModelService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection game model service types failed: {ex.Message}");
            }

            _gameModelServiceTypesCached = true;
        }

        private static void EnsureBlightServiceTypes()
        {
            if (_blightServiceTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _blightServiceTypesCached = true;
                return;
            }

            try
            {
                // IGameServices.BlightService
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsBlightServiceProperty = gameServicesType.GetProperty("BlightService", GameReflection.PublicInstance);
                }

                // IBlightService.CountGlobalFreeCysts
                var blightServiceType = assembly.GetType("Eremite.Services.IBlightService");
                if (blightServiceType != null)
                {
                    _blightCountFreeCystsMethod = blightServiceType.GetMethod("CountGlobalFreeCysts", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached BlightService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection blight service types failed: {ex.Message}");
            }

            _blightServiceTypesCached = true;
        }

        private static void EnsureBlightConfigTypes()
        {
            if (_blightConfigTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _blightConfigTypesCached = true;
                return;
            }

            try
            {
                // Settings.blightConfig
                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsBlightConfigField = settingsType.GetField("blightConfig", GameReflection.PublicInstance);
                }

                // BlightConfig.blightPostFuel
                var blightConfigType = assembly.GetType("Eremite.Model.Configs.BlightConfig");
                if (blightConfigType != null)
                {
                    _blightConfigBlightPostFuelField = blightConfigType.GetField("blightPostFuel", GameReflection.PublicInstance);
                }

                // GoodRef.Name and DisplayName
                var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
                if (goodRefType != null)
                {
                    _goodRefNameProperty = goodRefType.GetProperty("Name", GameReflection.PublicInstance);
                    _goodRefDisplayNameProperty = goodRefType.GetProperty("DisplayName", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached BlightConfig types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection blight config types failed: {ex.Message}");
            }

            _blightConfigTypesCached = true;
        }

        private static void EnsureStorageService2Types()
        {
            if (_storageService2TypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _storageService2TypesCached = true;
                return;
            }

            try
            {
                // IGameServices.StorageService
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsStorageService2Property = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
                }

                // IStorageService.Main
                var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
                if (storageServiceType != null)
                {
                    _storageServiceMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
                }

                // MainStorage.GetAmount(string)
                var mainStorageType = assembly.GetType("Eremite.Services.MainStorage");
                if (mainStorageType != null)
                {
                    _mainStorageGetAmountMethod = mainStorageType.GetMethod("GetAmount", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached StorageService2 types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection storage service2 types failed: {ex.Message}");
            }

            _storageService2TypesCached = true;
        }

        private static void EnsureRainpunkServiceTypes()
        {
            if (_rainpunkServiceTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _rainpunkServiceTypesCached = true;
                return;
            }

            try
            {
                // IGameServices.RainpunkService
                var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
                if (gameServicesType != null)
                {
                    _gsRainpunkServiceProperty = gameServicesType.GetProperty("RainpunkService", GameReflection.PublicInstance);
                }

                // IRainpunkService.CountWaterLeft and CountTanksCapacity
                var rainpunkServiceType = assembly.GetType("Eremite.Services.IRainpunkService");
                var waterModelType = assembly.GetType("Eremite.Model.WaterModel");
                if (rainpunkServiceType != null && waterModelType != null)
                {
                    _rainpunkCountWaterLeftMethod = rainpunkServiceType.GetMethod("CountWaterLeft", GameReflection.PublicInstance, null, new[] { waterModelType }, null);
                    _rainpunkCountTanksCapacityMethod = rainpunkServiceType.GetMethod("CountTanksCapacity", GameReflection.PublicInstance, null, new[] { waterModelType }, null);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached RainpunkService types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection rainpunk service types failed: {ex.Message}");
            }

            _rainpunkServiceTypesCached = true;
        }

        private static void EnsureRainpunkEngineTypes()
        {
            if (_rainpunkEngineTypesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _rainpunkEngineTypesCached = true;
                return;
            }

            try
            {
                // Workshop and WorkshopState types
                _workshopType = assembly.GetType("Eremite.Buildings.Workshop");
                _workshopStateType = assembly.GetType("Eremite.Buildings.WorkshopState");
                _rainpunkEngineStateType = assembly.GetType("Eremite.Buildings.RainpunkEngineState");
                _rainpunkEngineModelType = assembly.GetType("Eremite.Buildings.RainpunkEngineModel");
                _buildingRainpunkModelType = assembly.GetType("Eremite.Buildings.BuildingRainpunkModel");

                if (_workshopType != null)
                {
                    _workshopStateField = _workshopType.GetField("state", GameReflection.PublicInstance);
                    _workshopModelField = _workshopType.GetField("model", GameReflection.PublicInstance);
                }

                if (_workshopStateType != null)
                {
                    _wsRainpunkUnlockedField = _workshopStateType.GetField("rainpunkUnlocked", GameReflection.PublicInstance);
                    _wsEnginesField = _workshopStateType.GetField("engines", GameReflection.PublicInstance);
                }

                // WorkshopModel.rainpunk field
                var workshopModelType = assembly.GetType("Eremite.Buildings.WorkshopModel");
                if (workshopModelType != null)
                {
                    _wmRainpunkField = workshopModelType.GetField("rainpunk", GameReflection.PublicInstance);
                }

                // BuildingRainpunkModel.engines field
                if (_buildingRainpunkModelType != null)
                {
                    _brpEnginesField = _buildingRainpunkModelType.GetField("engines", GameReflection.PublicInstance);
                }

                // RainpunkEngineState fields
                if (_rainpunkEngineStateType != null)
                {
                    _engineStateIndexField = _rainpunkEngineStateType.GetField("index", GameReflection.PublicInstance);
                    _engineStateLevelField = _rainpunkEngineStateType.GetField("level", GameReflection.PublicInstance);
                    _engineStateRequestedLevelField = _rainpunkEngineStateType.GetField("requestedLevel", GameReflection.PublicInstance);
                }

                // RainpunkEngineModel fields
                if (_rainpunkEngineModelType != null)
                {
                    _engineModelMaxLevelField = _rainpunkEngineModelType.GetField("maxLevel", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] BuildingReflection: Cached RainpunkEngine types");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingReflection rainpunk engine types failed: {ex.Message}");
            }

            _rainpunkEngineTypesCached = true;
        }

        // ========================================
        // PUBLIC API - PANEL STATE
        // ========================================

        /// <summary>
        /// Get the currently displayed building from BuildingPanel.currentBuilding.
        /// </summary>
        public static object GetCurrentBuilding()
        {
            EnsurePanelTypes();

            if (_currentBuildingField == null) return null;

            try
            {
                return _currentBuildingField.GetValue(null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a building panel is currently open.
        /// </summary>
        public static bool IsBuildingPanelOpen()
        {
            return GetCurrentBuilding() != null;
        }

        // ========================================
        // PUBLIC API - BUILDING INFO
        // ========================================

        /// <summary>
        /// Get the display name of a building.
        /// Uses Building.DisplayName property which returns the localized name directly.
        /// </summary>
        public static string GetBuildingName(object building)
        {
            if (building == null) return null;

            EnsureBuildingTypes();

            try
            {
                return _buildingDisplayNameProperty?.GetValue(building) as string;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetBuildingName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the description of a building.
        /// </summary>
        public static string GetBuildingDescription(object building)
        {
            if (building == null) return null;

            EnsureBuildingTypes();
            EnsureModelTypes();

            try
            {
                var model = _buildingModelProperty?.GetValue(building);
                if (model == null) return null;

                return _modelDescriptionProperty?.GetValue(model) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the building's ID.
        /// </summary>
        public static int GetBuildingId(object building)
        {
            if (building == null) return -1;

            EnsureBuildingTypes();

            try
            {
                return (int?)_buildingIdProperty?.GetValue(building) ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get the building's type name for routing to appropriate navigator.
        /// </summary>
        public static string GetBuildingTypeName(object building)
        {
            if (building == null) return null;

            try
            {
                return building.GetType().Name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if building construction is finished.
        /// </summary>
        public static bool IsBuildingFinished(object building)
        {
            if (building == null) return false;

            EnsureBuildingTypes();
            EnsureStateTypes();

            try
            {
                var state = _buildingStateProperty?.GetValue(building);
                if (state == null) return false;

                return (bool?)_stateFinishedField?.GetValue(state) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if building is sleeping/paused.
        /// </summary>
        public static bool IsBuildingSleeping(object building)
        {
            if (building == null) return false;

            EnsureBuildingTypes();
            EnsureStateTypes();

            try
            {
                var state = _buildingStateProperty?.GetValue(building);
                if (state == null) return false;

                return (bool?)_stateIsSleepingField?.GetValue(state) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if building supports being paused (sleep).
        /// Most finished production buildings with workers can sleep.
        /// Hearth, Storage, Port, Relic, Road cannot sleep when finished.
        /// </summary>
        public static bool CanBuildingSleep(object building)
        {
            if (building == null) return false;

            try
            {
                var canSleepMethod = building.GetType().GetMethod("CanSleep", GameReflection.PublicInstance);
                return (bool?)canSleepMethod?.Invoke(building, null) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pause (sleep) a building. Workers will be unassigned.
        /// </summary>
        public static bool SleepBuilding(object building)
        {
            if (building == null) return false;
            if (!CanBuildingSleep(building)) return false;
            if (IsBuildingSleeping(building)) return false;

            try
            {
                var sleepMethod = building.GetType().GetMethod("Sleep", GameReflection.PublicInstance);
                sleepMethod?.Invoke(building, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resume (wake up) a paused building.
        /// </summary>
        public static bool WakeUpBuilding(object building)
        {
            if (building == null) return false;
            if (!IsBuildingSleeping(building)) return false;

            try
            {
                var wakeUpMethod = building.GetType().GetMethod("WakeUp", GameReflection.PublicInstance);
                wakeUpMethod?.Invoke(building, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle building sleep state. Returns true if state changed.
        /// </summary>
        public static bool ToggleBuildingSleep(object building)
        {
            if (building == null) return false;

            if (IsBuildingSleeping(building))
            {
                return WakeUpBuilding(building);
            }
            else
            {
                return SleepBuilding(building);
            }
        }

        /// <summary>
        /// Check if building is a production building (has workers/recipes).
        /// </summary>
        public static bool IsProductionBuilding(object building)
        {
            if (building == null) return false;

            EnsureProductionTypes();

            if (_productionBuildingType == null) return false;

            return _productionBuildingType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Check if building implements IWorkshop (has recipe management).
        /// Workshop, Farm, Mine, BlightPost, etc. implement IWorkshop.
        /// Note: Camp does NOT implement IWorkshop but has recipes via state.recipes.
        /// </summary>
        public static bool IsWorkshop(object building)
        {
            if (building == null) return false;

            EnsureWorkshopTypes();

            if (_workshopInterfaceType == null) return false;

            return _workshopInterfaceType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Check if building is a Camp (has recipes but not via IWorkshop).
        /// </summary>
        public static bool IsCamp(object building)
        {
            if (building == null) return false;

            EnsureCampTypes();

            if (_campType == null) return false;

            return _campType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the current Camp mode (0-4 corresponding to CampMode enum).
        /// </summary>
        public static int GetCampMode(object building)
        {
            if (!IsCamp(building)) return 0;

            EnsureCampTypes();

            try
            {
                var state = _campStateField?.GetValue(building);
                if (state == null) return 0;

                var mode = _campStateModeField?.GetValue(state);
                return mode != null ? (int)mode : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Set the Camp mode.
        /// </summary>
        public static bool SetCampMode(object building, int mode)
        {
            if (!IsCamp(building)) return false;

            EnsureCampTypes();

            try
            {
                if (_campSetModeMethod == null) return false;

                // Convert int to CampMode enum
                var assembly = GameReflection.GameAssembly;
                var campModeType = assembly?.GetType("Eremite.Buildings.CampMode");
                if (campModeType == null) return false;

                var enumValue = Enum.ToObject(campModeType, mode);
                _campSetModeMethod.Invoke(building, new object[] { enumValue });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetCampMode failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get localized names for all Camp modes.
        /// </summary>
        public static string[] GetCampModeNames()
        {
            // These correspond to CampMode enum: None, OnlyMarked, NoGlades, OnlyMarkedGlades, NoGladesAndOnlyMarked
            return new string[]
            {
                "No restrictions",
                "Only marked trees",
                "Avoid glades",
                "Only marked in glades",
                "Avoid glades and only marked"
            };
        }

        /// <summary>
        /// Check if building is a Farm.
        /// </summary>
        public static bool IsFarm(object building)
        {
            if (building == null) return false;

            EnsureFarmTypes();

            if (_farmType == null) return false;

            return _farmType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get count of sown fields in farm's range.
        /// </summary>
        public static int GetFarmSownFields(object building)
        {
            if (!IsFarm(building)) return 0;

            EnsureFarmTypes();

            try
            {
                var result = _farmCountSownFieldsMethod?.Invoke(building, null);
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get count of plowed fields in farm's range.
        /// </summary>
        public static int GetFarmPlowedFields(object building)
        {
            if (!IsFarm(building)) return 0;

            EnsureFarmTypes();

            try
            {
                var result = _farmCountPlowedFieldsMethod?.Invoke(building, null);
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get total count of available fields in farm's range.
        /// </summary>
        public static int GetFarmTotalFields(object building)
        {
            if (!IsFarm(building)) return 0;

            EnsureFarmTypes();

            try
            {
                var result = _farmCountAllFieldsMethod?.Invoke(building, null);
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if building is a FishingHut.
        /// </summary>
        public static bool IsFishingHut(object building)
        {
            if (building == null) return false;

            EnsureFishingHutTypes();

            if (_fishingHutType == null) return false;

            return _fishingHutType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the current FishingHut bait mode (0-2 corresponding to FishmanBaitMode enum).
        /// 0 = None, 1 = Optional, 2 = OnlyWithBait
        /// </summary>
        public static int GetFishingBaitMode(object building)
        {
            if (!IsFishingHut(building)) return 0;

            EnsureFishingHutTypes();

            try
            {
                var state = _fishingHutStateField?.GetValue(building);
                if (state == null) return 0;

                var mode = _fishingHutStateBaitModeField?.GetValue(state);
                return mode != null ? (int)mode : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Set the FishingHut bait mode.
        /// </summary>
        public static bool SetFishingBaitMode(object building, int mode)
        {
            if (!IsFishingHut(building)) return false;

            EnsureFishingHutTypes();

            try
            {
                if (_fishingHutChangeModeMethod == null) return false;

                // Convert int to FishmanBaitMode enum
                var assembly = GameReflection.GameAssembly;
                var baitModeType = assembly?.GetType("Eremite.Buildings.FishmanBaitMode");
                if (baitModeType == null) return false;

                var enumValue = Enum.ToObject(baitModeType, mode);
                _fishingHutChangeModeMethod.Invoke(building, new object[] { enumValue });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetFishingBaitMode failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get localized names for all FishingHut bait modes.
        /// </summary>
        public static string[] GetFishingBaitModeNames()
        {
            // These correspond to FishmanBaitMode enum: None, Optional, OnlyWithBait
            return new string[]
            {
                "No bait",
                "Optional bait",
                "Only with bait"
            };
        }

        /// <summary>
        /// Get remaining bait charges for a FishingHut.
        /// </summary>
        public static int GetFishingBaitCharges(object building)
        {
            if (!IsFishingHut(building)) return 0;

            EnsureFishingHutTypes();

            try
            {
                var state = _fishingHutStateField?.GetValue(building);
                if (state == null) return 0;

                var charges = _fishingHutStateBaitChargesField?.GetValue(state);
                return (int?)charges ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the bait ingredient name for a FishingHut.
        /// </summary>
        public static string GetFishingBaitIngredient(object building)
        {
            if (!IsFishingHut(building)) return null;

            EnsureFishingHutTypes();

            try
            {
                var model = _fishingHutModelField?.GetValue(building);
                if (model == null) return null;

                var baitIngredient = _fishingHutModelBaitIngredientField?.GetValue(model);
                if (baitIngredient == null) return null;

                // baitIngredient is a GoodModel, get its Name property
                var nameProperty = baitIngredient.GetType().GetProperty("Name", GameReflection.PublicInstance);
                return nameProperty?.GetValue(baitIngredient) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get recipes for a FishingHut (returns List of RecipeState objects).
        /// </summary>
        public static List<object> GetFishingHutRecipes(object building)
        {
            var result = new List<object>();
            if (!IsFishingHut(building)) return result;

            EnsureFishingHutTypes();

            try
            {
                var state = _fishingHutStateField?.GetValue(building);
                if (state == null) return result;

                var recipes = _fishingHutStateRecipesField?.GetValue(state) as System.Collections.IList;
                if (recipes == null) return result;

                foreach (var recipe in recipes)
                {
                    result.Add(recipe);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return result;
        }

        /// <summary>
        /// Toggle a recipe for a FishingHut.
        /// </summary>
        public static bool ToggleFishingHutRecipe(object building, object recipeState)
        {
            if (!IsFishingHut(building) || recipeState == null) return false;

            EnsureFishingHutTypes();

            try
            {
                _fishingHutSwitchProductionOfMethod?.Invoke(building, new object[] { recipeState });
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // PUBLIC API - WORKERS
        // ========================================

        /// <summary>
        /// Get worker IDs for a production building.
        /// </summary>
        public static int[] GetWorkerIds(object building)
        {
            if (building == null || !IsProductionBuilding(building)) return new int[0];

            EnsureProductionTypes();

            try
            {
                return _workersProperty?.GetValue(building) as int[] ?? new int[0];
            }
            catch
            {
                return new int[0];
            }
        }

        /// <summary>
        /// Get worker count for a production building.
        /// </summary>
        public static int GetWorkerCount(object building)
        {
            var workerIds = GetWorkerIds(building);
            int count = 0;
            foreach (var id in workerIds)
            {
                if (id > 0) count++;
            }
            return count;
        }

        /// <summary>
        /// Get maximum worker slots for a production building.
        /// </summary>
        public static int GetMaxWorkers(object building)
        {
            return GetWorkerIds(building).Length;
        }

        /// <summary>
        /// Get an actor (villager) by ID.
        /// </summary>
        public static object GetActor(int actorId)
        {
            if (actorId <= 0) return null;

            EnsureActorTypes();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return null;

                var actorsService = _actorsServiceProperty?.GetValue(gameServices);
                if (actorsService == null) return null;

                return _getActorMethod?.Invoke(actorsService, new object[] { actorId });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get actor's display name.
        /// The name is stored in VillagerState.name, accessed via Actor.ActorState.
        /// </summary>
        public static string GetActorName(object actor)
        {
            if (actor == null) return null;

            EnsureActorProperties();

            try
            {
                // Get the ActorState (which is actually VillagerState for villagers)
                var actorState = _actorStateProperty?.GetValue(actor);
                if (actorState == null) return null;

                // Get the name field from the state
                return _villagerStateNameField?.GetValue(actorState) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get actor's race name.
        /// The race is stored in VillagerState.race, accessed via Actor.ActorState.
        /// Returns strings like "Human", "Beaver", "Lizard", "Harpy", "Fox".
        /// </summary>
        public static string GetActorRace(object actor)
        {
            if (actor == null) return null;

            EnsureActorProperties();

            try
            {
                // Get the ActorState (which is actually VillagerState for villagers)
                var actorState = _actorStateProperty?.GetValue(actor);
                if (actorState == null) return null;

                // Get the race field from the state
                return _villagerStateRaceField?.GetValue(actorState) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get actor's current task description (e.g., "Traveling", "On break", "Working").
        /// </summary>
        public static string GetActorTaskDescription(object actor)
        {
            if (actor == null) return null;

            EnsureActorProperties();

            try
            {
                return _getTaskDescriptionMethod?.Invoke(actor, null) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get a formatted description of a worker for announcement.
        /// Returns "Name, Race, Task" with available parts.
        /// </summary>
        public static string GetWorkerDescription(int workerId)
        {
            if (workerId <= 0) return null;

            var actor = GetActor(workerId);
            if (actor == null) return null;

            string name = GetActorName(actor) ?? "Unknown";
            string race = GetActorRace(actor);
            string task = GetActorTaskDescription(actor);

            var parts = new List<string> { name };
            if (!string.IsNullOrEmpty(race))
                parts.Add(race);
            if (!string.IsNullOrEmpty(task))
                parts.Add(task);

            return string.Join(", ", parts);
        }

        // ========================================
        // PUBLIC API - WORKER ASSIGNMENT
        // ========================================

        /// <summary>
        /// Get the VillagersService instance.
        /// </summary>
        private static object GetVillagersService()
        {
            EnsureVillagersServiceTypes();

            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return null;

                return _villagersServiceProperty?.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get list of race names that have free workers available.
        /// Returns tuples of (raceName, freeCount).
        /// </summary>
        public static List<(string raceName, int freeCount)> GetRacesWithFreeWorkers()
        {
            var result = new List<(string, int)>();

            EnsureVillagersServiceTypes();

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null) return result;

                // Get the Races dictionary
                var racesDict = _villagersServiceRacesProperty?.GetValue(villagersService);
                if (racesDict == null) return result;

                // Iterate through races
                var keys = racesDict.GetType().GetProperty("Keys")?.GetValue(racesDict) as System.Collections.IEnumerable;
                if (keys == null) return result;

                foreach (var raceKey in keys)
                {
                    string raceName = raceKey as string;
                    if (string.IsNullOrEmpty(raceName)) continue;

                    int freeCount = GetFreeWorkerCount(raceName);
                    if (freeCount > 0)
                    {
                        result.Add((raceName, freeCount));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRacesWithFreeWorkers failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get count of free workers for a specific race.
        /// </summary>
        public static int GetFreeWorkerCount(string raceName)
        {
            if (string.IsNullOrEmpty(raceName)) return 0;

            EnsureVillagersServiceTypes();

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null) return 0;

                var result = _getDefaultProfessionAmountMethod?.Invoke(villagersService, new object[] { raceName });
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Assign a free worker of the specified race to a building slot.
        /// </summary>
        public static bool AssignWorkerToSlot(object building, int slotIndex, string raceName)
        {
            if (building == null || string.IsNullOrEmpty(raceName)) return false;
            if (!IsProductionBuilding(building)) return false;

            EnsureVillagersServiceTypes();
            EnsureProfessionTypes();

            try
            {
                var villagersService = GetVillagersService();
                if (villagersService == null) return false;

                // Get a free villager of this race
                var villager = _getDefaultProfessionVillagerMethod?.Invoke(villagersService, new object[] { raceName, building });
                if (villager == null)
                {
                    Debug.Log($"[ATSAccessibility] AssignWorkerToSlot: No free villager of race {raceName}");
                    return false;
                }

                // Get the building's profession
                string profession = _professionProperty?.GetValue(building) as string;
                if (string.IsNullOrEmpty(profession))
                {
                    Debug.LogError("[ATSAccessibility] AssignWorkerToSlot: Could not get building profession");
                    return false;
                }

                // Assign the villager
                _setProfessionMethod?.Invoke(villagersService, new object[] { villager, profession, building, slotIndex, true });
                Debug.Log($"[ATSAccessibility] AssignWorkerToSlot: Assigned {raceName} to slot {slotIndex}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AssignWorkerToSlot failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Unassign a worker from a building slot.
        /// </summary>
        public static bool UnassignWorkerFromSlot(object building, int slotIndex)
        {
            if (building == null) return false;
            if (!IsProductionBuilding(building)) return false;

            EnsureVillagersServiceTypes();

            try
            {
                var workerIds = GetWorkerIds(building);
                if (slotIndex < 0 || slotIndex >= workerIds.Length) return false;

                int workerId = workerIds[slotIndex];
                if (workerId <= 0)
                {
                    Debug.Log("[ATSAccessibility] UnassignWorkerFromSlot: Slot is already empty");
                    return false;
                }

                var villagersService = GetVillagersService();
                if (villagersService == null) return false;

                // Get the villager
                var villager = _getVillagerMethod?.Invoke(villagersService, new object[] { workerId });
                if (villager == null)
                {
                    Debug.LogError("[ATSAccessibility] UnassignWorkerFromSlot: Could not get villager");
                    return false;
                }

                // Release from profession
                _releaseFromProfessionMethod?.Invoke(villagersService, new object[] { villager, true });
                Debug.Log($"[ATSAccessibility] UnassignWorkerFromSlot: Unassigned worker from slot {slotIndex}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] UnassignWorkerFromSlot failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a worker slot is empty.
        /// </summary>
        public static bool IsWorkerSlotEmpty(object building, int slotIndex)
        {
            var workerIds = GetWorkerIds(building);
            if (slotIndex < 0 || slotIndex >= workerIds.Length) return true;
            return workerIds[slotIndex] <= 0;
        }

        // ========================================
        // PUBLIC API - RECIPES
        // ========================================

        /// <summary>
        /// Get recipe states for a building with recipes.
        /// Handles both IWorkshop buildings and Camp.
        /// </summary>
        public static List<object> GetRecipes(object building)
        {
            var result = new List<object>();

            if (building == null)
                return result;

            // Try IWorkshop first
            if (IsWorkshop(building))
            {
                EnsureWorkshopTypes();

                try
                {
                    var recipes = _workshopRecipesProperty?.GetValue(building);
                    if (recipes != null)
                    {
                        var enumerable = recipes as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (var recipe in enumerable)
                            {
                                result.Add(recipe);
                            }
                        }
                    }
                    Debug.Log($"[ATSAccessibility] GetRecipes (IWorkshop): Found {result.Count} recipes");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] GetRecipes (IWorkshop) failed: {ex.Message}");
                }

                return result;
            }

            // Try Camp
            if (IsCamp(building))
            {
                EnsureCampTypes();

                try
                {
                    var campState = _campStateField?.GetValue(building);
                    if (campState != null)
                    {
                        var recipes = _campStateRecipesField?.GetValue(campState);
                        if (recipes != null)
                        {
                            var enumerable = recipes as System.Collections.IEnumerable;
                            if (enumerable != null)
                            {
                                foreach (var recipe in enumerable)
                                {
                                    result.Add(recipe);
                                }
                            }
                        }
                    }
                    Debug.Log($"[ATSAccessibility] GetRecipes (Camp): Found {result.Count} recipes");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] GetRecipes (Camp) failed: {ex.Message}");
                }

                return result;
            }

            return result;
        }

        /// <summary>
        /// Toggle a recipe's active state.
        /// Handles both IWorkshop and Camp buildings.
        /// </summary>
        public static bool ToggleRecipe(object building, object recipeState)
        {
            if (building == null || recipeState == null)
                return false;

            // Try IWorkshop first
            if (IsWorkshop(building))
            {
                EnsureWorkshopTypes();

                try
                {
                    _switchProductionOfMethod?.Invoke(building, new object[] { recipeState });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] ToggleRecipe (IWorkshop) failed: {ex.Message}");
                    return false;
                }
            }

            // Try Camp
            if (IsCamp(building))
            {
                EnsureCampTypes();

                try
                {
                    _campSwitchProductionOfMethod?.Invoke(building, new object[] { recipeState });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] ToggleRecipe (Camp) failed: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a recipe is active (enabled).
        /// </summary>
        public static bool IsRecipeActive(object recipeState)
        {
            if (recipeState == null) return false;

            EnsureRecipeTypes();

            try
            {
                return (bool?)_recipeActiveField?.GetValue(recipeState) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get recipe production limit (-1 = unlimited).
        /// </summary>
        public static int GetRecipeLimit(object recipeState)
        {
            if (recipeState == null) return -1;

            EnsureRecipeTypes();

            try
            {
                return (int?)_recipeLimitField?.GetValue(recipeState) ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get recipe model name (used to look up display name).
        /// </summary>
        public static string GetRecipeModelName(object recipeState)
        {
            if (recipeState == null) return null;

            EnsureRecipeTypes();

            try
            {
                return _recipeModelField?.GetValue(recipeState) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get recipe product name (the good being produced).
        /// </summary>
        public static string GetRecipeProductName(object recipeState)
        {
            if (recipeState == null) return null;

            EnsureRecipeTypes();

            try
            {
                return _recipeProductNameField?.GetValue(recipeState) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the recipe model object for a recipe state.
        /// </summary>
        public static object GetRecipeModel(object recipeState)
        {
            if (recipeState == null) return null;

            EnsureRecipeTypes();
            EnsureRecipeModelTypes();

            try
            {
                string modelName = _recipeModelField?.GetValue(recipeState) as string;
                if (string.IsNullOrEmpty(modelName)) return null;

                var settings = GameReflection.GetSettings();
                if (settings == null) return null;

                return _settingsGetRecipeMethod?.Invoke(settings, new object[] { modelName });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get recipe grade/star level (0-3 typically).
        /// </summary>
        public static int GetRecipeGrade(object recipeState)
        {
            var model = GetRecipeModel(recipeState);
            if (model == null) return 0;

            EnsureRecipeModelTypes();

            try
            {
                var grade = _recipeModelGradeField?.GetValue(model);
                if (grade == null) return 0;

                return (int?)_gradeModelLevelField?.GetValue(grade) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get recipe production time in seconds.
        /// </summary>
        public static float GetRecipeProductionTime(object recipeState)
        {
            var model = GetRecipeModel(recipeState);
            if (model == null) return 0f;

            EnsureRecipeModelTypes();

            try
            {
                return (float?)_recipeModelProductionTimeField?.GetValue(model) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get recipe produced amount (how many items produced per cycle).
        /// </summary>
        public static int GetRecipeProducedAmount(object recipeState)
        {
            var model = GetRecipeModel(recipeState);
            if (model == null) return 1;

            EnsureRecipeModelTypes();

            try
            {
                var producedGood = _recipeModelProducedGoodField?.GetValue(model);
                if (producedGood == null) return 1;

                return (int?)_goodRefAmountField?.GetValue(producedGood) ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Get recipe produced good display name.
        /// </summary>
        public static string GetRecipeProducedGoodDisplayName(object recipeState)
        {
            var model = GetRecipeModel(recipeState);
            if (model == null) return null;

            EnsureRecipeModelTypes();

            try
            {
                var producedGood = _recipeModelProducedGoodField?.GetValue(model);
                if (producedGood == null) return null;

                var goodModel = _goodRefGoodField?.GetValue(producedGood);
                if (goodModel == null) return null;

                var displayNameObj = _goodModelDisplayNameProperty?.GetValue(goodModel);
                return GameReflection.GetLocaText(displayNameObj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the ingredients array for a WorkshopRecipeState.
        /// Returns a 2D array: [slot][options], each option is an IngredientState.
        /// </summary>
        public static object GetRecipeIngredients(object recipeState)
        {
            if (recipeState == null) return null;

            EnsureRecipeTypes();

            try
            {
                return _recipeIngredientsField?.GetValue(recipeState);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the number of ingredient slots for a recipe.
        /// </summary>
        public static int GetRecipeIngredientSlotCount(object recipeState)
        {
            var ingredients = GetRecipeIngredients(recipeState) as System.Array;
            return ingredients?.Length ?? 0;
        }

        /// <summary>
        /// Get ingredient options for a specific slot.
        /// Returns array of IngredientState objects.
        /// </summary>
        public static object[] GetIngredientSlotOptions(object recipeState, int slotIndex)
        {
            var ingredients = GetRecipeIngredients(recipeState) as System.Array;
            if (ingredients == null || slotIndex < 0 || slotIndex >= ingredients.Length)
                return new object[0];

            try
            {
                var slot = ingredients.GetValue(slotIndex) as System.Array;
                if (slot == null) return new object[0];

                var result = new object[slot.Length];
                for (int i = 0; i < slot.Length; i++)
                {
                    result[i] = slot.GetValue(i);
                }
                return result;
            }
            catch
            {
                return new object[0];
            }
        }

        /// <summary>
        /// Get the good name from an IngredientState.
        /// </summary>
        public static string GetIngredientGoodName(object ingredientState)
        {
            if (ingredientState == null) return null;

            EnsureIngredientTypes();

            try
            {
                var good = _ingredientGoodField?.GetValue(ingredientState);
                if (good == null) return null;

                // Good is a struct with a 'name' field
                var nameField = good.GetType().GetField("name", GameReflection.PublicInstance);
                return nameField?.GetValue(good) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the amount from an IngredientState (how many used per production cycle).
        /// </summary>
        public static int GetIngredientAmount(object ingredientState)
        {
            if (ingredientState == null) return 1;

            EnsureIngredientTypes();

            try
            {
                var good = _ingredientGoodField?.GetValue(ingredientState);
                if (good == null) return 1;

                return (int?)_goodAmountField?.GetValue(good) ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Check if an ingredient option is allowed/enabled.
        /// </summary>
        public static bool IsIngredientAllowed(object ingredientState)
        {
            if (ingredientState == null) return false;

            EnsureIngredientTypes();

            try
            {
                return (bool?)_ingredientAllowedField?.GetValue(ingredientState) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle an ingredient's allowed state.
        /// </summary>
        public static void ToggleIngredientAllowed(object ingredientState)
        {
            if (ingredientState == null) return;

            EnsureIngredientTypes();

            try
            {
                bool current = (bool?)_ingredientAllowedField?.GetValue(ingredientState) ?? false;
                _ingredientAllowedField?.SetValue(ingredientState, !current);
            }
            catch { }
        }

        /// <summary>
        /// Set recipe production limit.
        /// </summary>
        public static void SetRecipeLimit(object recipeState, int limit)
        {
            if (recipeState == null) return;

            EnsureRecipeTypes();

            try
            {
                _recipeLimitField?.SetValue(recipeState, limit);
            }
            catch { }
        }

        // ========================================
        // PUBLIC API - STORAGE
        // ========================================

        /// <summary>
        /// Check if a building has ProductionStorage.
        /// </summary>
        public static bool HasProductionStorage(object building)
        {
            if (building == null || !IsProductionBuilding(building)) return false;

            EnsureProductionTypes();

            try
            {
                var storage = _productionStorageProperty?.GetValue(building);
                return storage != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get storage goods for a production building.
        /// Returns list of (goodName, amount) pairs for goods with amount > 0.
        /// </summary>
        public static List<(string goodName, int amount)> GetProductionStorageGoods(object building)
        {
            var result = new List<(string, int)>();

            if (building == null || !IsProductionBuilding(building)) return result;

            EnsureProductionTypes();
            EnsureStorageTypes();

            try
            {
                var storage = _productionStorageProperty?.GetValue(building);
                if (storage == null) return result;

                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return result;

                // Get the goods dictionary (Dictionary<string, int>)
                var goodsDict = _goodsCollectionGoodsProperty?.GetValue(goodsCollection);
                if (goodsDict == null) return result;

                // Iterate through the dictionary
                var keysProperty = goodsDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(goodsDict) as System.Collections.IEnumerable;
                if (keys == null) return result;

                var indexer = goodsDict.GetType().GetProperty("Item");

                foreach (var key in keys)
                {
                    string goodName = key as string;
                    if (string.IsNullOrEmpty(goodName)) continue;

                    int amount = (int?)indexer?.GetValue(goodsDict, new[] { key }) ?? 0;
                    if (amount > 0)
                    {
                        result.Add((goodName, amount));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetProductionStorageGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get display name for a good.
        /// </summary>
        public static string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return goodName;

            EnsureRecipeModelTypes();

            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return goodName;

                // settings.GetGood(name)
                var getGoodMethod = settings.GetType().GetMethod("GetGood", new[] { typeof(string) });
                var goodModel = getGoodMethod?.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return goodName;

                var displayNameObj = _goodModelDisplayNameProperty?.GetValue(goodModel);
                return GameReflection.GetLocaText(displayNameObj) ?? goodName;
            }
            catch
            {
                return goodName;
            }
        }

        /// <summary>
        /// Check if a building has IngredientsStorage (implements IWorkshop).
        /// </summary>
        public static bool HasIngredientsStorage(object building)
        {
            if (building == null) return false;

            EnsureWorkshopTypes();

            if (_workshopInterfaceType == null) return false;

            // Check if building implements IWorkshop
            if (!_workshopInterfaceType.IsInstanceOfType(building))
                return false;

            try
            {
                var storage = _workshopIngredientsStorageProperty?.GetValue(building);
                return storage != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get ingredients storage goods for a building (input goods).
        /// Returns list of (goodName, amount) pairs for goods with amount > 0.
        /// </summary>
        public static List<(string goodName, int amount)> GetIngredientsStorageGoods(object building)
        {
            var result = new List<(string, int)>();

            if (building == null) return result;

            EnsureWorkshopTypes();
            EnsureIngredientsStorageTypes();

            if (_workshopInterfaceType == null || !_workshopInterfaceType.IsInstanceOfType(building))
                return result;

            try
            {
                var storage = _workshopIngredientsStorageProperty?.GetValue(building);
                if (storage == null) return result;

                var goodsCollection = _ingredientsStorageGoodsField?.GetValue(storage);
                if (goodsCollection == null) return result;

                // Get the goods dictionary (Dictionary<string, int>)
                var goodsDict = _goodsCollectionGoodsField?.GetValue(goodsCollection);
                if (goodsDict == null) return result;

                // Iterate through the dictionary
                var keysProperty = goodsDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(goodsDict) as System.Collections.IEnumerable;
                if (keys == null) return result;

                var indexer = goodsDict.GetType().GetProperty("Item");

                foreach (var key in keys)
                {
                    string goodName = key as string;
                    if (string.IsNullOrEmpty(goodName)) continue;

                    int amount = (int?)indexer?.GetValue(goodsDict, new[] { key }) ?? 0;
                    if (amount > 0)
                    {
                        result.Add((goodName, amount));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetIngredientsStorageGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get delivery state for a good in production storage.
        /// Returns (isForced, isConstantForced) tuple.
        /// </summary>
        public static (bool isForced, bool isConstantForced) GetOutputDeliveryState(object building, string goodName)
        {
            if (building == null || string.IsNullOrEmpty(goodName))
                return (false, false);

            EnsureProductionTypes();
            EnsureStorageTypes();

            try
            {
                var storage = _productionStorageProperty?.GetValue(building);
                if (storage == null) return (false, false);

                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return (false, false);

                var deliveryState = _storageGetDeliveryStateMethod?.Invoke(goodsCollection, new object[] { goodName });
                if (deliveryState == null) return (false, false);

                bool isForced = (bool?)_deliveryStateForcedField?.GetValue(deliveryState) ?? false;
                bool isConstantForced = (bool?)_deliveryStateConstantForcedField?.GetValue(deliveryState) ?? false;

                return (isForced, isConstantForced);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetOutputDeliveryState failed: {ex.Message}");
                return (false, false);
            }
        }

        /// <summary>
        /// Toggle force delivery for a good in production storage.
        /// Forces next available worker to transport the product to warehouse.
        /// </summary>
        public static bool ToggleForceDelivery(object building, string goodName)
        {
            if (building == null || string.IsNullOrEmpty(goodName))
                return false;

            EnsureProductionTypes();
            EnsureStorageTypes();

            try
            {
                var storage = _productionStorageProperty?.GetValue(building);
                if (storage == null) return false;

                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return false;

                var deliveryState = _storageGetDeliveryStateMethod?.Invoke(goodsCollection, new object[] { goodName });
                if (deliveryState == null) return false;

                _storageSwitchForceDeliveryMethod?.Invoke(storage, new object[] { goodName, deliveryState });

                Debug.Log($"[ATSAccessibility] Toggled force delivery for {goodName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleForceDelivery failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggle constant (auto) delivery for a good in production storage.
        /// When enabled, product is delivered each time it's produced instead of waiting for storage full.
        /// </summary>
        public static bool ToggleConstantDelivery(object building, string goodName)
        {
            if (building == null || string.IsNullOrEmpty(goodName))
                return false;

            EnsureProductionTypes();
            EnsureStorageTypes();

            try
            {
                var storage = _productionStorageProperty?.GetValue(building);
                if (storage == null) return false;

                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return false;

                var deliveryState = _storageGetDeliveryStateMethod?.Invoke(goodsCollection, new object[] { goodName });
                if (deliveryState == null) return false;

                _storageSwitchConstantForceDeliveryMethod?.Invoke(storage, new object[] { goodName, deliveryState });

                Debug.Log($"[ATSAccessibility] Toggled constant delivery for {goodName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleConstantDelivery failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Return an ingredient from ingredients storage back to the main warehouse.
        /// </summary>
        public static bool ReturnIngredientToWarehouse(object building, string goodName, int amount)
        {
            if (building == null || string.IsNullOrEmpty(goodName) || amount <= 0)
                return false;

            EnsureWorkshopTypes();
            EnsureIngredientsStorageTypes();

            if (_workshopInterfaceType == null || !_workshopInterfaceType.IsInstanceOfType(building))
                return false;

            try
            {
                // Get ingredients storage
                var ingredientsStorage = _workshopIngredientsStorageProperty?.GetValue(building);
                if (ingredientsStorage == null) return false;

                var goodsCollection = _ingredientsStorageGoodsField?.GetValue(ingredientsStorage);
                if (goodsCollection == null) return false;

                // Create Good struct
                var goodType = GameReflection.GameAssembly?.GetType("Eremite.Model.Good");
                if (goodType == null) return false;

                var good = Activator.CreateInstance(goodType, new object[] { goodName, amount });

                // Remove from ingredients storage
                var removeMethod = goodsCollection.GetType().GetMethod("Remove", new[] { goodType });
                removeMethod?.Invoke(goodsCollection, new object[] { good });

                // Get building model name and ID for store call
                var buildingModelProp = building.GetType().GetProperty("BuildingModel", GameReflection.PublicInstance);
                var buildingModel = buildingModelProp?.GetValue(building);
                var modelNameProp = buildingModel?.GetType().GetProperty("Name", GameReflection.PublicInstance);
                var modelName = modelNameProp?.GetValue(buildingModel) as string ?? "";

                var buildingIdProp = building.GetType().GetProperty("Id", GameReflection.PublicInstance);
                int buildingId = (int?)buildingIdProp?.GetValue(building) ?? 0;

                // Store in main warehouse
                var storageService = GameReflection.GetStorageService();
                if (storageService != null)
                {
                    // Find the IngredientsReturn operation type
                    var operationType = GameReflection.GameAssembly?.GetType("Eremite.Model.StorageOperationType");
                    object ingredientsReturnValue = null;
                    if (operationType != null)
                    {
                        ingredientsReturnValue = Enum.Parse(operationType, "IngredientsReturn");
                    }

                    var storeMethod = storageService.GetType().GetMethod("Store", new[] { goodType, typeof(string), typeof(int), operationType });
                    storeMethod?.Invoke(storageService, new object[] { good, modelName, buildingId, ingredientsReturnValue });
                }

                Debug.Log($"[ATSAccessibility] Returned {amount} {goodName} to warehouse");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ReturnIngredientToWarehouse failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PUBLIC API - HEARTH
        // ========================================

        /// <summary>
        /// Check if building is a Hearth.
        /// </summary>
        public static bool IsHearth(object building)
        {
            if (building == null) return false;

            EnsureHearthTypes();

            if (_hearthType == null) return false;

            return _hearthType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get Hearth fire level (0-1 percentage of max burn time remaining).
        /// </summary>
        public static float GetHearthFireLevel(object building)
        {
            if (!IsHearth(building)) return 0f;

            EnsureHearthTypes();

            try
            {
                var state = _hearthStateField?.GetValue(building);
                var model = _hearthModelField?.GetValue(building);
                if (state == null || model == null) return 0f;

                float burningTimeLeft = (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
                float maxBurningTime = (float?)_hearthModelMaxBurningTimeField?.GetValue(model) ?? 1f;

                return burningTimeLeft / maxBurningTime;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get Hearth remaining fuel time in seconds.
        /// </summary>
        public static float GetHearthFuelTimeRemaining(object building)
        {
            if (!IsHearth(building)) return 0f;

            EnsureHearthTypes();

            try
            {
                var state = _hearthStateField?.GetValue(building);
                if (state == null) return 0f;

                return (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Check if Hearth fire is low (warning state).
        /// </summary>
        public static bool IsHearthFireLow(object building)
        {
            if (!IsHearth(building)) return false;

            EnsureHearthTypes();

            try
            {
                var state = _hearthStateField?.GetValue(building);
                var model = _hearthModelField?.GetValue(building);
                if (state == null || model == null) return false;

                float burningTimeLeft = (float?)_hearthStateBurningTimeLeftField?.GetValue(state) ?? 0f;
                float minTimeToShowNoFuel = (float?)_hearthModelMinTimeToShowNoFuelField?.GetValue(model) ?? 5f;

                return burningTimeLeft < minTimeToShowNoFuel;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if Hearth fire is out (no fuel).
        /// </summary>
        public static bool IsHearthFireOut(object building)
        {
            return GetHearthFuelTimeRemaining(building) <= 0f;
        }

        /// <summary>
        /// Get Hearth hub index (-1 = no hub active).
        /// </summary>
        public static int GetHearthHubIndex(object building)
        {
            if (!IsHearth(building)) return -1;

            EnsureHearthTypes();

            try
            {
                var state = _hearthStateField?.GetValue(building);
                if (state == null) return -1;

                return (int?)_hearthStateHubIndexField?.GetValue(state) ?? -1;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get Hearth corruption/blight level (0-1).
        /// </summary>
        public static float GetHearthCorruptionRate(object building)
        {
            if (!IsHearth(building)) return 0f;

            EnsureHearthTypes();

            try
            {
                var result = _hearthGetCorruptionRateMethod?.Invoke(building, null);
                return (float?)result ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get Hearth range (hub radius).
        /// </summary>
        public static float GetHearthRange(object building)
        {
            if (!IsHearth(building)) return 0f;

            EnsureHearthTypes();

            try
            {
                var result = _hearthGetRangeMethod?.Invoke(building, null);
                return (float?)result ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Check if Hearth is the main (Ancient) Hearth.
        /// </summary>
        public static bool IsMainHearth(object building)
        {
            if (!IsHearth(building)) return false;

            EnsureHearthTypes();

            try
            {
                var result = _hearthIsMainHearthMethod?.Invoke(building, null);
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get worker IDs for a Hearth.
        /// </summary>
        public static int[] GetHearthWorkerIds(object building)
        {
            if (!IsHearth(building)) return new int[0];

            EnsureHearthTypes();

            try
            {
                var state = _hearthStateField?.GetValue(building);
                if (state == null) return new int[0];

                return _hearthStateWorkersField?.GetValue(state) as int[] ?? new int[0];
            }
            catch
            {
                return new int[0];
            }
        }

        // ========================================
        // PUBLIC API - HOUSE
        // ========================================

        /// <summary>
        /// Check if building is a House.
        /// </summary>
        public static bool IsHouse(object building)
        {
            if (building == null) return false;

            EnsureHouseTypes();

            if (_houseType == null) return false;

            return _houseType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get House resident villager IDs.
        /// </summary>
        public static List<int> GetHouseResidents(object building)
        {
            var result = new List<int>();

            if (!IsHouse(building)) return result;

            EnsureHouseTypes();

            try
            {
                var state = _houseStateField?.GetValue(building);
                if (state == null) return result;

                var residents = _houseStateResidentsField?.GetValue(state) as System.Collections.IList;
                if (residents == null) return result;

                foreach (var id in residents)
                {
                    if (id is int intId)
                    {
                        result.Add(intId);
                    }
                }
            }
            catch { }

            return result;
        }

        /// <summary>
        /// Get current House capacity (may be reduced by effects).
        /// </summary>
        public static int GetHouseCapacity(object building)
        {
            if (!IsHouse(building)) return 0;

            EnsureHouseTypes();

            try
            {
                var result = _houseGetHousingPlacesMethod?.Invoke(building, null);
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get maximum House capacity.
        /// </summary>
        public static int GetHouseMaxCapacity(object building)
        {
            if (!IsHouse(building)) return 0;

            EnsureHouseTypes();

            try
            {
                var result = _houseGetMaxHousingPlacesMethod?.Invoke(building, null);
                return (int?)result ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if House is full.
        /// </summary>
        public static bool IsHouseFull(object building)
        {
            if (!IsHouse(building)) return false;

            EnsureHouseTypes();

            try
            {
                var result = _houseIsFullMethod?.Invoke(building, null);
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // PUBLIC API - RELIC
        // ========================================

        /// <summary>
        /// Check if building is a Relic.
        /// </summary>
        public static bool IsRelic(object building)
        {
            if (building == null) return false;

            EnsureRelicTypes();

            if (_relicType == null) return false;

            return _relicType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Check if Relic investigation has been started.
        /// </summary>
        public static bool IsRelicInvestigationStarted(object building)
        {
            if (!IsRelic(building)) return false;

            EnsureRelicTypes();

            try
            {
                var state = _relicStateField?.GetValue(building);
                if (state == null) return false;

                return (bool?)_relicStateInvestigationStartedField?.GetValue(state) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if Relic investigation is finished.
        /// </summary>
        public static bool IsRelicInvestigationFinished(object building)
        {
            if (!IsRelic(building)) return false;

            EnsureRelicTypes();

            try
            {
                var state = _relicStateField?.GetValue(building);
                if (state == null) return false;

                return (bool?)_relicStateInvestigationFinishedField?.GetValue(state) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get Relic work progress (0-1).
        /// </summary>
        public static float GetRelicProgress(object building)
        {
            if (!IsRelic(building)) return 0f;

            EnsureRelicTypes();

            try
            {
                var state = _relicStateField?.GetValue(building);
                if (state == null) return 0f;

                return (float?)_relicStateWorkProgressField?.GetValue(state) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get expected time remaining for Relic work in seconds.
        /// </summary>
        public static float GetRelicTimeLeft(object building)
        {
            if (!IsRelic(building)) return 0f;

            EnsureRelicTypes();

            try
            {
                var result = _relicGetExpectedWorkingTimeLeftMethod?.Invoke(building, null);
                return (float?)result ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get worker IDs for a Relic.
        /// </summary>
        public static int[] GetRelicWorkerIds(object building)
        {
            if (!IsRelic(building)) return new int[0];

            EnsureRelicTypes();

            try
            {
                var state = _relicStateField?.GetValue(building);
                if (state == null) return new int[0];

                return _relicStateWorkersField?.GetValue(state) as int[] ?? new int[0];
            }
            catch
            {
                return new int[0];
            }
        }

        // ========================================
        // PUBLIC API - PORT
        // ========================================

        /// <summary>
        /// Check if building is a Port.
        /// </summary>
        public static bool IsPort(object building)
        {
            if (building == null) return false;

            EnsurePortTypes();

            if (_portType == null) return false;

            return _portType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get Port expedition level.
        /// </summary>
        public static int GetPortExpeditionLevel(object building)
        {
            if (!IsPort(building)) return 0;

            EnsurePortTypes();

            try
            {
                var state = _portStateField?.GetValue(building);
                if (state == null) return 0;

                return (int?)_portStateExpeditionLevelField?.GetValue(state) ?? 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Check if Port expedition has started.
        /// </summary>
        public static bool IsPortExpeditionStarted(object building)
        {
            if (!IsPort(building)) return false;

            EnsurePortTypes();

            try
            {
                var result = _portWasExpeditionStartedMethod?.Invoke(building, null);
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if Port has rewards waiting to be collected.
        /// </summary>
        public static bool ArePortRewardsWaiting(object building)
        {
            if (!IsPort(building)) return false;

            EnsurePortTypes();

            try
            {
                var result = _portAreRewardsWaitingMethod?.Invoke(building, null);
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get Port expedition progress (0-1).
        /// </summary>
        public static float GetPortProgress(object building)
        {
            if (!IsPort(building)) return 0f;

            EnsurePortTypes();

            try
            {
                var result = _portCalculateProgressMethod?.Invoke(building, null);
                return (float?)result ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get Port expedition time remaining in seconds.
        /// </summary>
        public static float GetPortTimeLeft(object building)
        {
            if (!IsPort(building)) return 0f;

            EnsurePortTypes();

            try
            {
                var result = _portCalculateTimeLeftMethod?.Invoke(building, null);
                return (float?)result ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get Port blueprint reward name (if any).
        /// </summary>
        public static string GetPortBlueprintReward(object building)
        {
            if (!IsPort(building)) return null;

            EnsurePortTypes();

            try
            {
                var state = _portStateField?.GetValue(building);
                if (state == null) return null;

                return _portStateBlueprintRewardField?.GetValue(state) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get Port perk reward name (if any).
        /// </summary>
        public static string GetPortPerkReward(object building)
        {
            if (!IsPort(building)) return null;

            EnsurePortTypes();

            try
            {
                var state = _portStateField?.GetValue(building);
                if (state == null) return null;

                return _portStatePerkRewardField?.GetValue(state) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get worker IDs for a Port.
        /// </summary>
        public static int[] GetPortWorkerIds(object building)
        {
            if (!IsPort(building)) return new int[0];

            EnsurePortTypes();

            try
            {
                var state = _portStateField?.GetValue(building);
                if (state == null) return new int[0];

                return _portStateWorkersField?.GetValue(state) as int[] ?? new int[0];
            }
            catch
            {
                return new int[0];
            }
        }

        // ========================================
        // PUBLIC API - EVENT SUBSCRIPTION
        // ========================================

        /// <summary>
        /// Subscribe to OnBuildingPanelShown event.
        /// Callback receives the Building object.
        /// </summary>
        public static IDisposable SubscribeToBuildingPanelShown(Action<object> callback)
        {
            EnsureEventTypes();

            try
            {
                var blackboard = GameReflection.GetGameBlackboardService();
                if (blackboard == null || _onBuildingPanelShownProperty == null) return null;

                var observable = _onBuildingPanelShownProperty.GetValue(blackboard);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToBuildingPanelShown failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Subscribe to OnBuildingPanelClosed event.
        /// Callback receives the Building object.
        /// </summary>
        public static IDisposable SubscribeToBuildingPanelClosed(Action<object> callback)
        {
            EnsureEventTypes();

            try
            {
                var blackboard = GameReflection.GetGameBlackboardService();
                if (blackboard == null || _onBuildingPanelClosedProperty == null) return null;

                var observable = _onBuildingPanelClosedProperty.GetValue(blackboard);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, callback);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SubscribeToBuildingPanelClosed failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // PUBLIC API - DECORATION
        // ========================================

        /// <summary>
        /// Check if building is a Decoration.
        /// </summary>
        public static bool IsDecoration(object building)
        {
            if (building == null) return false;

            EnsureDecorationType();

            if (_decorationType == null) return false;

            return _decorationType.IsInstanceOfType(building);
        }

        // ========================================
        // PUBLIC API - STORAGE BUILDING
        // ========================================

        /// <summary>
        /// Check if building is a Storage building (main warehouse).
        /// </summary>
        public static bool IsStorage(object building)
        {
            if (building == null) return false;

            EnsureStorageType2();

            if (_storageType == null) return false;

            return _storageType.IsInstanceOfType(building);
        }

        // ========================================
        // PUBLIC API - INSTITUTION
        // ========================================

        /// <summary>
        /// Check if building is an Institution (Tavern, Temple, etc.).
        /// </summary>
        public static bool IsInstitution(object building)
        {
            if (building == null) return false;

            EnsureInstitutionTypes();

            if (_institutionType == null) return false;

            return _institutionType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the number of service recipes in an Institution.
        /// </summary>
        public static int GetInstitutionRecipeCount(object building)
        {
            if (!IsInstitution(building)) return 0;

            EnsureInstitutionTypes();

            try
            {
                var model = _institutionModelField?.GetValue(building);
                if (model == null) return 0;

                var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
                return recipes?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the served need name for an institution recipe.
        /// </summary>
        public static string GetInstitutionServedNeedName(object building, int recipeIndex)
        {
            if (!IsInstitution(building)) return null;

            EnsureInstitutionTypes();

            try
            {
                var model = _institutionModelField?.GetValue(building);
                if (model == null) return null;

                var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
                if (recipes == null || recipeIndex >= recipes.Length) return null;

                var recipeModel = recipes.GetValue(recipeIndex);
                var servedNeed = _institutionRecipeModelServedNeedField?.GetValue(recipeModel);
                if (servedNeed == null) return null;

                return GameReflection.GetLocaText(servedNeed.GetType().GetProperty("displayName", GameReflection.PublicInstance)?.GetValue(servedNeed));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if institution recipe consumes goods.
        /// </summary>
        public static bool IsInstitutionRecipeGoodConsumed(object building, int recipeIndex)
        {
            if (!IsInstitution(building)) return false;

            EnsureInstitutionTypes();

            try
            {
                var model = _institutionModelField?.GetValue(building);
                if (model == null) return false;

                var recipes = _institutionModelRecipesField?.GetValue(model) as Array;
                if (recipes == null || recipeIndex >= recipes.Length) return false;

                var recipeModel = recipes.GetValue(recipeIndex);
                return (bool?)_institutionRecipeModelIsGoodConsumedField?.GetValue(recipeModel) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the currently picked good name for an institution recipe.
        /// </summary>
        public static string GetInstitutionCurrentGoodName(object building, int recipeIndex)
        {
            if (!IsInstitution(building)) return null;

            EnsureInstitutionTypes();

            try
            {
                var state = _institutionStateField?.GetValue(building);
                var model = _institutionModelField?.GetValue(building);
                if (state == null || model == null) return null;

                var stateRecipes = _institutionStateRecipesField?.GetValue(state) as Array;
                var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
                if (stateRecipes == null || modelRecipes == null) return null;
                if (recipeIndex >= stateRecipes.Length || recipeIndex >= modelRecipes.Length) return null;

                var recipeState = stateRecipes.GetValue(recipeIndex);
                var recipeModel = modelRecipes.GetValue(recipeIndex);

                int pickedGood = (int?)_institutionRecipeStatePickedGoodField?.GetValue(recipeState) ?? 0;
                var requiredGoods = _institutionRecipeModelRequiredGoodsField?.GetValue(recipeModel);
                if (requiredGoods == null) return null;

                // GoodsSet has a 'goods' field that is GoodRef[]
                var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
                if (goodsArray == null || pickedGood >= goodsArray.Length) return null;

                var goodRef = goodsArray.GetValue(pickedGood);
                return GetGoodRefDisplayName(goodRef);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the available goods count for an institution recipe.
        /// </summary>
        public static int GetInstitutionAvailableGoodsCount(object building, int recipeIndex)
        {
            if (!IsInstitution(building)) return 0;

            EnsureInstitutionTypes();

            try
            {
                var model = _institutionModelField?.GetValue(building);
                if (model == null) return 0;

                var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
                if (modelRecipes == null || recipeIndex >= modelRecipes.Length) return 0;

                var recipeModel = modelRecipes.GetValue(recipeIndex);
                var requiredGoods = _institutionRecipeModelRequiredGoodsField?.GetValue(recipeModel);
                if (requiredGoods == null) return 0;

                var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
                return goodsArray?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get a specific available good name for an institution recipe.
        /// </summary>
        public static string GetInstitutionAvailableGoodName(object building, int recipeIndex, int goodIndex)
        {
            if (!IsInstitution(building)) return null;

            EnsureInstitutionTypes();

            try
            {
                var model = _institutionModelField?.GetValue(building);
                if (model == null) return null;

                var modelRecipes = _institutionModelRecipesField?.GetValue(model) as Array;
                if (modelRecipes == null || recipeIndex >= modelRecipes.Length) return null;

                var recipeModel = modelRecipes.GetValue(recipeIndex);
                var requiredGoods = _institutionRecipeModelRequiredGoodsField?.GetValue(recipeModel);
                if (requiredGoods == null) return null;

                var goodsArray = requiredGoods.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(requiredGoods) as Array;
                if (goodsArray == null || goodIndex >= goodsArray.Length) return null;

                var goodRef = goodsArray.GetValue(goodIndex);
                return GetGoodRefDisplayName(goodRef);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Change the ingredient for an institution recipe.
        /// </summary>
        public static bool ChangeInstitutionIngredient(object building, int recipeIndex, int goodIndex)
        {
            if (!IsInstitution(building)) return false;

            EnsureInstitutionTypes();

            try
            {
                var state = _institutionStateField?.GetValue(building);
                if (state == null) return false;

                var stateRecipes = _institutionStateRecipesField?.GetValue(state) as Array;
                if (stateRecipes == null || recipeIndex >= stateRecipes.Length) return false;

                var recipeState = stateRecipes.GetValue(recipeIndex);
                _institutionChangeIngredientMethod?.Invoke(building, new object[] { recipeState, goodIndex });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ChangeInstitutionIngredient failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the storage goods for an institution.
        /// </summary>
        public static Dictionary<string, int> GetInstitutionStorageGoods(object building)
        {
            if (!IsInstitution(building)) return new Dictionary<string, int>();

            EnsureInstitutionTypes();

            try
            {
                var storage = _institutionStorageField?.GetValue(building);
                if (storage == null) return new Dictionary<string, int>();

                return GetBuildingStorageGoodsInternal(storage);
            }
            catch
            {
                return new Dictionary<string, int>();
            }
        }

        // ========================================
        // PUBLIC API - SHRINE
        // ========================================

        /// <summary>
        /// Check if building is a Shrine.
        /// </summary>
        public static bool IsShrine(object building)
        {
            if (building == null) return false;

            EnsureShrineTypes();

            if (_shrineType == null) return false;

            return _shrineType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the number of effect tiers in a shrine.
        /// </summary>
        public static int GetShrineEffectTierCount(object building)
        {
            if (!IsShrine(building)) return 0;

            EnsureShrineTypes();

            try
            {
                var model = _shrineModelField?.GetValue(building);
                if (model == null) return 0;

                var effects = _shrineModelEffectsField?.GetValue(model) as Array;
                return effects?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the label for a shrine effect tier.
        /// </summary>
        public static string GetShrineTierLabel(object building, int tierIndex)
        {
            if (!IsShrine(building)) return null;

            EnsureShrineTypes();

            try
            {
                var model = _shrineModelField?.GetValue(building);
                if (model == null) return null;

                var effects = _shrineModelEffectsField?.GetValue(model) as Array;
                if (effects == null || tierIndex >= effects.Length) return null;

                var effectModel = effects.GetValue(tierIndex);
                var label = _shrineEffectsModelLabelField?.GetValue(effectModel);
                return GameReflection.GetLocaText(label);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the charges left for a shrine effect tier.
        /// </summary>
        public static int GetShrineTierChargesLeft(object building, int tierIndex)
        {
            if (!IsShrine(building)) return 0;

            EnsureShrineTypes();

            try
            {
                var state = _shrineStateField?.GetValue(building);
                if (state == null) return 0;

                var effects = _shrineStateEffectsField?.GetValue(state) as Array;
                if (effects == null || tierIndex >= effects.Length) return 0;

                var effectState = effects.GetValue(tierIndex);
                return (int?)_shrineEffectsStateChargesLeftField?.GetValue(effectState) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the max charges for a shrine effect tier.
        /// </summary>
        public static int GetShrineTierMaxCharges(object building, int tierIndex)
        {
            if (!IsShrine(building)) return 0;

            EnsureShrineTypes();

            try
            {
                var model = _shrineModelField?.GetValue(building);
                if (model == null) return 0;

                var effects = _shrineModelEffectsField?.GetValue(model) as Array;
                if (effects == null || tierIndex >= effects.Length) return 0;

                var effectModel = effects.GetValue(tierIndex);
                return (int?)_shrineEffectsModelChargesField?.GetValue(effectModel) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the number of effects in a shrine tier.
        /// </summary>
        public static int GetShrineTierEffectCount(object building, int tierIndex)
        {
            if (!IsShrine(building)) return 0;

            EnsureShrineTypes();

            try
            {
                var model = _shrineModelField?.GetValue(building);
                if (model == null) return 0;

                var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
                if (effectTiers == null || tierIndex >= effectTiers.Length) return 0;

                var effectModel = effectTiers.GetValue(tierIndex);
                var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
                return effects?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get an effect name from a shrine tier.
        /// </summary>
        public static string GetShrineTierEffectName(object building, int tierIndex, int effectIndex)
        {
            if (!IsShrine(building)) return null;

            EnsureShrineTypes();

            try
            {
                var model = _shrineModelField?.GetValue(building);
                if (model == null) return null;

                var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
                if (effectTiers == null || tierIndex >= effectTiers.Length) return null;

                var effectModel = effectTiers.GetValue(tierIndex);
                var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
                if (effects == null || effectIndex >= effects.Length) return null;

                var effect = effects.GetValue(effectIndex);
                return GameReflection.GetLocaText(effect.GetType().GetProperty("displayName", GameReflection.PublicInstance)?.GetValue(effect));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Use an effect from a shrine tier.
        /// </summary>
        public static bool UseShrineEffect(object building, int tierIndex, int effectIndex)
        {
            if (!IsShrine(building)) return false;

            EnsureShrineTypes();

            try
            {
                // Check if charges are available
                if (GetShrineTierChargesLeft(building, tierIndex) <= 0)
                    return false;

                var state = _shrineStateField?.GetValue(building);
                var model = _shrineModelField?.GetValue(building);
                if (state == null || model == null) return false;

                var stateEffects = _shrineStateEffectsField?.GetValue(state) as Array;
                var modelEffects = _shrineModelEffectsField?.GetValue(model) as Array;
                if (stateEffects == null || modelEffects == null) return false;
                if (tierIndex >= stateEffects.Length || tierIndex >= modelEffects.Length) return false;

                var effectState = stateEffects.GetValue(tierIndex);
                var effectModel = modelEffects.GetValue(tierIndex);

                _shrineUseEffectMethod?.Invoke(building, new object[] { effectState, effectModel, effectIndex });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] UseShrineEffect failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PUBLIC API - PORO
        // ========================================

        /// <summary>
        /// Check if building is a Poro.
        /// </summary>
        public static bool IsPoro(object building)
        {
            if (building == null) return false;

            EnsurePoroTypes();

            if (_poroType == null) return false;

            return _poroType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the happiness level of a Poro (0-1).
        /// </summary>
        public static float GetPoroHappiness(object building)
        {
            if (!IsPoro(building)) return 0f;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                if (state == null) return 0f;

                return (float?)_poroStateHappinessField?.GetValue(state) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the production progress of a Poro (0-1).
        /// </summary>
        public static float GetPoroProductionProgress(object building)
        {
            if (!IsPoro(building)) return 0f;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                if (state == null) return 0f;

                return (float?)_poroStateProductionProgressField?.GetValue(state) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the number of needs for a Poro.
        /// </summary>
        public static int GetPoroNeedCount(object building)
        {
            if (!IsPoro(building)) return 0;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return 0;

                var needs = _poroModelNeedsField?.GetValue(model) as Array;
                return needs?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the display name of a Poro need.
        /// </summary>
        public static string GetPoroNeedName(object building, int needIndex)
        {
            if (!IsPoro(building)) return null;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return null;

                var needs = _poroModelNeedsField?.GetValue(model) as Array;
                if (needs == null || needIndex >= needs.Length) return null;

                var needModel = needs.GetValue(needIndex);
                var displayName = _poroNeedModelDisplayNameField?.GetValue(needModel);
                return GameReflection.GetLocaText(displayName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the level of a Poro need (0-1).
        /// </summary>
        public static float GetPoroNeedLevel(object building, int needIndex)
        {
            if (!IsPoro(building)) return 0f;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                if (state == null) return 0f;

                var needs = _poroStateNeedsField?.GetValue(state) as Array;
                if (needs == null || needIndex >= needs.Length) return 0f;

                var needState = needs.GetValue(needIndex);
                return (float?)_poroNeedStateLevelField?.GetValue(needState) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the current good name for a Poro need.
        /// </summary>
        public static string GetPoroNeedCurrentGoodName(object building, int needIndex)
        {
            if (!IsPoro(building)) return null;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                var model = _poroModelField?.GetValue(building);
                if (state == null || model == null) return null;

                var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
                var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
                if (stateNeeds == null || modelNeeds == null) return null;
                if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return null;

                var needState = stateNeeds.GetValue(needIndex);
                var needModel = modelNeeds.GetValue(needIndex);

                // Call Poro.GetCurrentGoodFor(state, model) to get the Good
                var good = _poroGetCurrentGoodForMethod?.Invoke(building, new object[] { needState, needModel });
                if (good == null) return null;

                // Good has a 'name' field that is the good ID
                var goodName = good.GetType().GetField("name", GameReflection.PublicInstance)?.GetValue(good) as string;
                if (string.IsNullOrEmpty(goodName)) return null;

                return GetGoodDisplayName(goodName);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the number of available goods for a Poro need.
        /// </summary>
        public static int GetPoroNeedAvailableGoodsCount(object building, int needIndex)
        {
            if (!IsPoro(building)) return 0;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return 0;

                var needs = _poroModelNeedsField?.GetValue(model) as Array;
                if (needs == null || needIndex >= needs.Length) return 0;

                var needModel = needs.GetValue(needIndex);
                var goodsSet = _poroNeedModelGoodsField?.GetValue(needModel);
                if (goodsSet == null) return 0;

                var goodsArray = goodsSet.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(goodsSet) as Array;
                return goodsArray?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get an available good name for a Poro need.
        /// </summary>
        public static string GetPoroNeedAvailableGoodName(object building, int needIndex, int goodIndex)
        {
            if (!IsPoro(building)) return null;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return null;

                var needs = _poroModelNeedsField?.GetValue(model) as Array;
                if (needs == null || needIndex >= needs.Length) return null;

                var needModel = needs.GetValue(needIndex);
                var goodsSet = _poroNeedModelGoodsField?.GetValue(needModel);
                if (goodsSet == null) return null;

                var goodsArray = goodsSet.GetType().GetField("goods", GameReflection.PublicInstance)?.GetValue(goodsSet) as Array;
                if (goodsArray == null || goodIndex >= goodsArray.Length) return null;

                var goodRef = goodsArray.GetValue(goodIndex);
                return GetGoodRefDisplayName(goodRef);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a Poro need can be fulfilled.
        /// </summary>
        public static bool CanFulfillPoroNeed(object building, int needIndex)
        {
            if (!IsPoro(building)) return false;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                var model = _poroModelField?.GetValue(building);
                if (state == null || model == null) return false;

                var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
                var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
                if (stateNeeds == null || modelNeeds == null) return false;
                if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return false;

                var needState = stateNeeds.GetValue(needIndex);
                var needModel = modelNeeds.GetValue(needIndex);

                var result = _poroCanFulfillMethod?.Invoke(building, new object[] { needState, needModel });
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Fulfill a Poro need (feed the poro).
        /// </summary>
        public static bool FulfillPoroNeed(object building, int needIndex)
        {
            if (!IsPoro(building)) return false;

            EnsurePoroTypes();

            try
            {
                if (!CanFulfillPoroNeed(building, needIndex))
                    return false;

                var state = _poroStateField?.GetValue(building);
                var model = _poroModelField?.GetValue(building);
                if (state == null || model == null) return false;

                var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
                var modelNeeds = _poroModelNeedsField?.GetValue(model) as Array;
                if (stateNeeds == null || modelNeeds == null) return false;
                if (needIndex >= stateNeeds.Length || needIndex >= modelNeeds.Length) return false;

                var needState = stateNeeds.GetValue(needIndex);
                var needModel = modelNeeds.GetValue(needIndex);

                _poroFulfillMethod?.Invoke(building, new object[] { needState, needModel });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] FulfillPoroNeed failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Change the good for a Poro need.
        /// </summary>
        public static bool ChangePoroNeedGood(object building, int needIndex, int goodIndex)
        {
            if (!IsPoro(building)) return false;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                if (state == null) return false;

                var stateNeeds = _poroStateNeedsField?.GetValue(state) as Array;
                if (stateNeeds == null || needIndex >= stateNeeds.Length) return false;

                var needState = stateNeeds.GetValue(needIndex);
                _poroGoodChangedMethod?.Invoke(building, new object[] { needState, goodIndex });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ChangePoroNeedGood failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the product name for a Poro.
        /// </summary>
        public static string GetPoroProductName(object building)
        {
            if (!IsPoro(building)) return null;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return null;

                var productRef = _poroModelProductField?.GetValue(model);
                return GetGoodRefDisplayName(productRef);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current product amount ready to collect.
        /// </summary>
        public static int GetPoroProductAmount(object building)
        {
            if (!IsPoro(building)) return 0;

            EnsurePoroTypes();

            try
            {
                var state = _poroStateField?.GetValue(building);
                if (state == null) return 0;

                var product = _poroStateProductField?.GetValue(state);
                if (product == null) return 0;

                return (int?)product.GetType().GetField("amount", GameReflection.PublicInstance)?.GetValue(product) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the max products a Poro can hold.
        /// </summary>
        public static int GetPoroMaxProducts(object building)
        {
            if (!IsPoro(building)) return 0;

            EnsurePoroTypes();

            try
            {
                var model = _poroModelField?.GetValue(building);
                if (model == null) return 0;

                return (int?)_poroModelMaxProductsField?.GetValue(model) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Check if Poro products can be gathered.
        /// </summary>
        public static bool CanGatherPoroProducts(object building)
        {
            if (!IsPoro(building)) return false;

            EnsurePoroTypes();

            try
            {
                var result = _poroCanGatherProductsMethod?.Invoke(building, null);
                return (bool?)result ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gather products from a Poro.
        /// </summary>
        public static bool GatherPoroProducts(object building)
        {
            if (!IsPoro(building)) return false;

            EnsurePoroTypes();

            try
            {
                if (!CanGatherPoroProducts(building))
                    return false;

                _poroGatherProductsMethod?.Invoke(building, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GatherPoroProducts failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PUBLIC API - RAINCATCHER
        // ========================================

        /// <summary>
        /// Check if building is a RainCatcher.
        /// </summary>
        public static bool IsRainCatcher(object building)
        {
            if (building == null) return false;

            EnsureRainCatcherTypes();

            if (_rainCatcherType == null) return false;

            return _rainCatcherType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the current water type name for a RainCatcher.
        /// </summary>
        public static string GetRainCatcherWaterTypeName(object building)
        {
            if (!IsRainCatcher(building)) return null;

            EnsureRainCatcherTypes();
            EnsureWaterModelTypes();

            try
            {
                var waterModel = _rainCatcherGetCurrentWaterTypeMethod?.Invoke(building, null);
                if (waterModel == null) return null;

                return GameReflection.GetLocaText(_waterModelDisplayNameProperty?.GetValue(waterModel));
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // PUBLIC API - EXTRACTOR
        // ========================================

        /// <summary>
        /// Check if building is an Extractor.
        /// </summary>
        public static bool IsExtractor(object building)
        {
            if (building == null) return false;

            EnsureExtractorTypes();

            if (_extractorType == null) return false;

            return _extractorType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Get the water type name for an Extractor.
        /// </summary>
        public static string GetExtractorWaterTypeName(object building)
        {
            if (!IsExtractor(building)) return null;

            EnsureExtractorTypes();
            EnsureWaterModelTypes();

            try
            {
                var waterModel = _extractorGetWaterTypeMethod?.Invoke(building, null);
                if (waterModel == null) return null;

                return GameReflection.GetLocaText(_waterModelDisplayNameProperty?.GetValue(waterModel));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the production time for an Extractor.
        /// </summary>
        public static float GetExtractorProductionTime(object building)
        {
            if (!IsExtractor(building)) return 0f;

            EnsureExtractorTypes();

            try
            {
                var model = _extractorModelField?.GetValue(building);
                if (model == null) return 0f;

                return (float?)_extractorModelProductionTimeField?.GetValue(model) ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// Get the produced amount for an Extractor.
        /// </summary>
        public static int GetExtractorProducedAmount(object building)
        {
            if (!IsExtractor(building)) return 0;

            EnsureExtractorTypes();

            try
            {
                var model = _extractorModelField?.GetValue(building);
                if (model == null) return 0;

                return (int?)_extractorModelProducedAmountField?.GetValue(model) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // PUBLIC API - HYDRANT
        // ========================================

        /// <summary>
        /// Check if building is a Hydrant.
        /// </summary>
        public static bool IsHydrant(object building)
        {
            if (building == null) return false;

            EnsureHydrantTypes();

            if (_hydrantType == null) return false;

            return _hydrantType.IsInstanceOfType(building);
        }

        // ========================================
        // PUBLIC API - CYCLE ABILITIES
        // ========================================

        /// <summary>
        /// Get the list of cycle abilities from ConditionsState.
        /// </summary>
        private static System.Collections.IList GetCycleAbilitiesList()
        {
            EnsureCycleAbilityTypes();
            var conditionsState = GameReflection.GetConditionsState();
            if (conditionsState == null) return null;

            try
            {
                return _condCycleAbilitiesField?.GetValue(conditionsState) as System.Collections.IList;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the number of cycle abilities available.
        /// </summary>
        public static int GetCycleAbilityCount()
        {
            var abilities = GetCycleAbilitiesList();
            return abilities?.Count ?? 0;
        }

        /// <summary>
        /// Get the display name of a cycle ability at the given index.
        /// </summary>
        public static string GetCycleAbilityName(int index)
        {
            EnsureCycleAbilityTypes();
            EnsureGameModelServiceTypes();

            var abilities = GetCycleAbilitiesList();
            if (abilities == null || index < 0 || index >= abilities.Count) return null;

            try
            {
                var ability = abilities[index];
                if (ability == null) return null;

                // Get the gameEffect string
                string gameEffect = _cycleAbilityGameEffectField?.GetValue(ability) as string;
                if (string.IsNullOrEmpty(gameEffect)) return null;

                // Get the effect model
                var effectModel = GetEffectModel(gameEffect);
                if (effectModel == null) return gameEffect;  // Fallback to ID

                // Get display name from effect model
                var displayName = _effectModelDisplayNameField?.GetValue(effectModel);
                return GameReflection.GetLocaText(displayName) ?? gameEffect;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the remaining charges of a cycle ability at the given index.
        /// </summary>
        public static int GetCycleAbilityCharges(int index)
        {
            EnsureCycleAbilityTypes();

            var abilities = GetCycleAbilitiesList();
            if (abilities == null || index < 0 || index >= abilities.Count) return 0;

            try
            {
                var ability = abilities[index];
                if (ability == null) return 0;

                return (int?)_cycleAbilityChargesField?.GetValue(ability) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Use a cycle ability at the given index, decrementing charges and applying the effect.
        /// Returns true if successful.
        /// </summary>
        public static bool UseCycleAbility(int index)
        {
            EnsureCycleAbilityTypes();
            EnsureGameModelServiceTypes();
            EnsureStorageService2Types();

            var abilities = GetCycleAbilitiesList();
            if (abilities == null || index < 0 || index >= abilities.Count) return false;

            try
            {
                var ability = abilities[index];
                if (ability == null) return false;

                // Check charges
                int charges = (int?)_cycleAbilityChargesField?.GetValue(ability) ?? 0;
                if (charges <= 0) return false;

                // Get the effect model
                string gameEffect = _cycleAbilityGameEffectField?.GetValue(ability) as string;
                if (string.IsNullOrEmpty(gameEffect)) return false;

                var effectModel = GetEffectModel(gameEffect);
                if (effectModel == null) return false;

                // Check if effect can be drawn
                bool canBeDrawn = (bool?)_effectModelCanBeDrawnMethod?.Invoke(effectModel, null) ?? false;
                if (!canBeDrawn) return false;

                // Decrement charges
                _cycleAbilityChargesField?.SetValue(ability, charges - 1);

                // Get main storage info for the effect context
                var storageService = GetStorageServiceInternal();
                string sourceName = "Main Storage";
                int sourceId = 0;

                if (storageService != null && _storageServiceMainProperty != null)
                {
                    var mainStorage = _storageServiceMainProperty.GetValue(storageService);
                    if (mainStorage != null)
                    {
                        var modelNameProp = mainStorage.GetType().GetProperty("ModelName", GameReflection.PublicInstance);
                        var idProp = mainStorage.GetType().GetProperty("Id", GameReflection.PublicInstance);
                        sourceName = modelNameProp?.GetValue(mainStorage) as string ?? sourceName;
                        sourceId = (int?)idProp?.GetValue(mainStorage) ?? 0;
                    }
                }

                // Apply the effect with EffectContextType.Building (enum value 0)
                var assembly = GameReflection.GameAssembly;
                var effectContextType = assembly?.GetType("Eremite.Model.Effects.EffectContextType");
                if (effectContextType != null && _effectModelApplyMethod != null)
                {
                    var buildingContext = Enum.ToObject(effectContextType, 0);  // Building = 0
                    _effectModelApplyMethod.Invoke(effectModel, new object[] { buildingContext, sourceName, sourceId });
                }

                Debug.Log($"[ATSAccessibility] Used cycle ability: {gameEffect}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] UseCycleAbility failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get an EffectModel by name from GameModelService.
        /// </summary>
        private static object GetEffectModel(string effectName)
        {
            EnsureGameModelServiceTypes();

            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsGameModelServiceProperty == null) return null;

            try
            {
                var gameModelService = _gsGameModelServiceProperty.GetValue(gameServices);
                if (gameModelService == null) return null;

                return _gmsGetEffectMethod?.Invoke(gameModelService, new object[] { effectName });
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // PUBLIC API - BLIGHT FUEL (for Hydrant)
        // ========================================

        /// <summary>
        /// Get the number of free (unfought) cysts globally.
        /// </summary>
        public static int GetBlightFreeCysts()
        {
            EnsureBlightServiceTypes();

            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsBlightServiceProperty == null) return 0;

            try
            {
                var blightService = _gsBlightServiceProperty.GetValue(gameServices);
                if (blightService == null) return 0;

                return (int?)_blightCountFreeCystsMethod?.Invoke(blightService, null) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the current amount of blight post fuel in storage.
        /// </summary>
        public static int GetBlightFuelAmount()
        {
            EnsureBlightConfigTypes();
            EnsureStorageService2Types();

            string fuelName = GetBlightFuelNameInternal();
            if (string.IsNullOrEmpty(fuelName)) return 0;

            var storageService = GetStorageServiceInternal();
            if (storageService == null) return 0;

            try
            {
                var mainStorage = _storageServiceMainProperty?.GetValue(storageService);
                if (mainStorage == null) return 0;

                return (int?)_mainStorageGetAmountMethod?.Invoke(mainStorage, new object[] { fuelName }) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the display name of the blight post fuel.
        /// </summary>
        public static string GetBlightFuelName()
        {
            EnsureBlightConfigTypes();

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsBlightConfigField == null) return null;

            try
            {
                var blightConfig = _settingsBlightConfigField.GetValue(settings);
                if (blightConfig == null) return null;

                var blightPostFuel = _blightConfigBlightPostFuelField?.GetValue(blightConfig);
                if (blightPostFuel == null) return null;

                return _goodRefDisplayNameProperty?.GetValue(blightPostFuel) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the internal name of the blight post fuel (for storage lookups).
        /// </summary>
        private static string GetBlightFuelNameInternal()
        {
            EnsureBlightConfigTypes();

            var settings = GameReflection.GetSettings();
            if (settings == null || _settingsBlightConfigField == null) return null;

            try
            {
                var blightConfig = _settingsBlightConfigField.GetValue(settings);
                if (blightConfig == null) return null;

                var blightPostFuel = _blightConfigBlightPostFuelField?.GetValue(blightConfig);
                if (blightPostFuel == null) return null;

                return _goodRefNameProperty?.GetValue(blightPostFuel) as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get StorageService from GameServices (internal helper).
        /// </summary>
        private static object GetStorageServiceInternal()
        {
            EnsureStorageService2Types();

            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsStorageService2Property == null) return null;

            try
            {
                return _gsStorageService2Property.GetValue(gameServices);
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // PUBLIC API - WATER TANK (for RainCatcher/Extractor)
        // ========================================

        /// <summary>
        /// Get the current water level in the tank for the water type produced by a building.
        /// </summary>
        public static int GetWaterTankCurrent(object building)
        {
            EnsureRainpunkServiceTypes();

            var waterModel = GetWaterModelFromBuilding(building);
            if (waterModel == null) return 0;

            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsRainpunkServiceProperty == null) return 0;

            try
            {
                var rainpunkService = _gsRainpunkServiceProperty.GetValue(gameServices);
                if (rainpunkService == null) return 0;

                return (int?)_rainpunkCountWaterLeftMethod?.Invoke(rainpunkService, new object[] { waterModel }) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the maximum water tank capacity for the water type produced by a building.
        /// </summary>
        public static int GetWaterTankCapacity(object building)
        {
            EnsureRainpunkServiceTypes();

            var waterModel = GetWaterModelFromBuilding(building);
            if (waterModel == null) return 0;

            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsRainpunkServiceProperty == null) return 0;

            try
            {
                var rainpunkService = _gsRainpunkServiceProperty.GetValue(gameServices);
                if (rainpunkService == null) return 0;

                return (int?)_rainpunkCountTanksCapacityMethod?.Invoke(rainpunkService, new object[] { waterModel }) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the WaterModel from a RainCatcher or Extractor building.
        /// </summary>
        private static object GetWaterModelFromBuilding(object building)
        {
            if (building == null) return null;

            try
            {
                if (IsRainCatcher(building))
                {
                    EnsureRainCatcherTypes();
                    return _rainCatcherGetCurrentWaterTypeMethod?.Invoke(building, null);
                }
                else if (IsExtractor(building))
                {
                    EnsureExtractorTypes();
                    return _extractorGetWaterTypeMethod?.Invoke(building, null);
                }
            }
            catch
            {
                // Fall through
            }

            return null;
        }

        // ========================================
        // PUBLIC API - RAINPUNK ENGINES (for Workshops)
        // ========================================

        /// <summary>
        /// Check if a building is specifically the Workshop class (not just IWorkshop).
        /// Only Workshop class has rainpunk engines.
        /// </summary>
        private static bool IsWorkshopClass(object building)
        {
            if (building == null) return false;
            EnsureRainpunkEngineTypes();
            return _workshopType != null && _workshopType.IsInstanceOfType(building);
        }

        /// <summary>
        /// Check if rainpunk is enabled at the meta/account level.
        /// This is a progression unlock that must be earned.
        /// Path: MetaController.Instance.MetaServices.MetaPerksService.IsRainpunkEnabled()
        /// </summary>
        public static bool IsRainpunkEnabledGlobally()
        {
            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return false;

                // Get MetaController.Instance
                var metaControllerType = assembly.GetType("Eremite.Controller.MetaController");
                if (metaControllerType == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaController type not found");
                    return false;
                }

                var instanceProp = metaControllerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: Instance property not found");
                    return false;
                }

                var metaController = instanceProp.GetValue(null);
                if (metaController == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaController instance is null");
                    return false;
                }

                // Get MetaServices
                var metaServicesProp = metaController.GetType().GetProperty("MetaServices", GameReflection.PublicInstance);
                if (metaServicesProp == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaServices property not found");
                    return false;
                }

                var metaServices = metaServicesProp.GetValue(metaController);
                if (metaServices == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaServices is null");
                    return false;
                }

                // Get MetaPerksService
                var metaPerksServiceProp = metaServices.GetType().GetProperty("MetaPerksService", GameReflection.PublicInstance);
                if (metaPerksServiceProp == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaPerksService property not found");
                    return false;
                }

                var metaPerksService = metaPerksServiceProp.GetValue(metaServices);
                if (metaPerksService == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaPerksService is null");
                    return false;
                }

                // Call IsRainpunkEnabled()
                var isRainpunkEnabledMethod = metaPerksService.GetType().GetMethod("IsRainpunkEnabled", GameReflection.PublicInstance);
                if (isRainpunkEnabledMethod == null)
                {
                    Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: IsRainpunkEnabled method not found");
                    return false;
                }

                return (bool?)isRainpunkEnabledMethod.Invoke(metaPerksService, null) ?? false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] IsRainpunkEnabledGlobally exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a workshop has rainpunk capability (model has rainpunk defined AND meta unlock obtained).
        /// </summary>
        public static bool HasRainpunkCapability(object building)
        {
            // First check if rainpunk is enabled at the meta level
            if (!IsRainpunkEnabledGlobally()) return false;
            if (!IsWorkshopClass(building)) return false;

            EnsureRainpunkEngineTypes();

            try
            {
                var model = _workshopModelField?.GetValue(building);
                if (model == null) return false;

                var rainpunkModel = _wmRainpunkField?.GetValue(model);
                return rainpunkModel != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if rainpunk is unlocked for a workshop.
        /// </summary>
        public static bool IsRainpunkUnlocked(object building)
        {
            if (!IsWorkshopClass(building)) return false;
            EnsureRainpunkEngineTypes();

            try
            {
                var state = _workshopStateField?.GetValue(building);
                if (state == null) return false;

                return (bool?)_wsRainpunkUnlockedField?.GetValue(state) ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the number of engines in a workshop.
        /// </summary>
        public static int GetEngineCount(object building)
        {
            if (!IsWorkshopClass(building)) return 0;
            EnsureRainpunkEngineTypes();

            try
            {
                var state = _workshopStateField?.GetValue(building);
                if (state == null) return 0;

                var engines = _wsEnginesField?.GetValue(state) as Array;
                return engines?.Length ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the current level of an engine (actual level based on water availability).
        /// </summary>
        public static int GetEngineCurrentLevel(object building, int engineIndex)
        {
            var engineState = GetEngineState(building, engineIndex);
            if (engineState == null) return 0;

            try
            {
                return (int?)_engineStateLevelField?.GetValue(engineState) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the requested level of an engine (player-set level).
        /// </summary>
        public static int GetEngineRequestedLevel(object building, int engineIndex)
        {
            var engineState = GetEngineState(building, engineIndex);
            if (engineState == null) return 0;

            try
            {
                return (int?)_engineStateRequestedLevelField?.GetValue(engineState) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Get the maximum level of an engine.
        /// </summary>
        public static int GetEngineMaxLevel(object building, int engineIndex)
        {
            var engineModel = GetEngineModel(building, engineIndex);
            if (engineModel == null) return 0;

            try
            {
                return (int?)_engineModelMaxLevelField?.GetValue(engineModel) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Increase the requested level of an engine by 1.
        /// </summary>
        public static bool IncreaseEngineLevel(object building, int engineIndex)
        {
            var engineState = GetEngineState(building, engineIndex);
            if (engineState == null) return false;

            int maxLevel = GetEngineMaxLevel(building, engineIndex);
            int currentRequested = GetEngineRequestedLevel(building, engineIndex);

            if (currentRequested >= maxLevel) return false;

            try
            {
                _engineStateRequestedLevelField?.SetValue(engineState, currentRequested + 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Decrease the requested level of an engine by 1.
        /// </summary>
        public static bool DecreaseEngineLevel(object building, int engineIndex)
        {
            var engineState = GetEngineState(building, engineIndex);
            if (engineState == null) return false;

            int currentRequested = GetEngineRequestedLevel(building, engineIndex);

            if (currentRequested <= 0) return false;

            try
            {
                _engineStateRequestedLevelField?.SetValue(engineState, currentRequested - 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Set the requested level of an engine to a specific value.
        /// </summary>
        public static bool SetEngineLevel(object building, int engineIndex, int level)
        {
            var engineState = GetEngineState(building, engineIndex);
            if (engineState == null) return false;

            int maxLevel = GetEngineMaxLevel(building, engineIndex);
            level = Math.Max(0, Math.Min(level, maxLevel));

            try
            {
                _engineStateRequestedLevelField?.SetValue(engineState, level);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the engine state object for a specific engine index.
        /// </summary>
        private static object GetEngineState(object building, int engineIndex)
        {
            if (!IsWorkshopClass(building)) return null;
            EnsureRainpunkEngineTypes();

            try
            {
                var state = _workshopStateField?.GetValue(building);
                if (state == null) return null;

                var engines = _wsEnginesField?.GetValue(state) as Array;
                if (engines == null || engineIndex < 0 || engineIndex >= engines.Length) return null;

                return engines.GetValue(engineIndex);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the engine model object for a specific engine index.
        /// </summary>
        private static object GetEngineModel(object building, int engineIndex)
        {
            if (!IsWorkshopClass(building)) return null;
            EnsureRainpunkEngineTypes();

            try
            {
                var model = _workshopModelField?.GetValue(building);
                if (model == null) return null;

                var rainpunkModel = _wmRainpunkField?.GetValue(model);
                if (rainpunkModel == null) return null;

                var engineModels = _brpEnginesField?.GetValue(rainpunkModel) as Array;
                if (engineModels == null || engineIndex < 0 || engineIndex >= engineModels.Length) return null;

                return engineModels.GetValue(engineIndex);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the rainpunk unlock price for a workshop.
        /// Returns (goodName, displayName, amount) or null if not applicable.
        /// </summary>
        public static (string goodName, string displayName, int amount)? GetRainpunkUnlockPrice(object building)
        {
            if (!IsWorkshopClass(building)) return null;
            if (!HasRainpunkCapability(building)) return null;
            if (IsRainpunkUnlocked(building)) return null;

            EnsureRainpunkEngineTypes();

            try
            {
                // Get the unlock price via Workshop.GetRainpunkUnlockPrice()
                var getRainpunkUnlockPriceMethod = building.GetType().GetMethod("GetRainpunkUnlockPrice", GameReflection.PublicInstance);
                if (getRainpunkUnlockPriceMethod == null) return null;

                var goodObj = getRainpunkUnlockPriceMethod.Invoke(building, null);
                if (goodObj == null) return null;

                // Good struct has 'name' (string) and 'amount' (int) fields
                var nameField = goodObj.GetType().GetField("name", GameReflection.PublicInstance);
                var amountField = goodObj.GetType().GetField("amount", GameReflection.PublicInstance);

                string goodName = nameField?.GetValue(goodObj) as string;
                int amount = (int?)amountField?.GetValue(goodObj) ?? 0;

                if (string.IsNullOrEmpty(goodName)) return null;

                string displayName = GetGoodDisplayName(goodName) ?? goodName;
                return (goodName, displayName, amount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetRainpunkUnlockPrice failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if we have enough resources to unlock rainpunk.
        /// </summary>
        public static bool CanAffordRainpunkUnlock(object building)
        {
            var price = GetRainpunkUnlockPrice(building);
            if (price == null) return false;

            int stored = GetMainStorageAmount(price.Value.goodName);
            return stored >= price.Value.amount;
        }

        /// <summary>
        /// Unlock rainpunk for a workshop (pays the cost).
        /// </summary>
        public static bool UnlockRainpunk(object building)
        {
            if (!IsWorkshopClass(building)) return false;
            if (!HasRainpunkCapability(building)) return false;
            if (IsRainpunkUnlocked(building)) return false;
            if (!CanAffordRainpunkUnlock(building)) return false;

            try
            {
                var unlockMethod = building.GetType().GetMethod("UnlockRainpunk", GameReflection.PublicInstance);
                if (unlockMethod == null) return false;

                unlockMethod.Invoke(building, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] UnlockRainpunk failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get amount of a good from the main storage.
        /// </summary>
        private static int GetMainStorageAmount(string goodName)
        {
            try
            {
                var gameServices = GameReflection.GetGameServices();
                if (gameServices == null) return 0;

                EnsureStorageService2Types();
                var storageService = _gsStorageService2Property?.GetValue(gameServices);
                if (storageService == null) return 0;

                var mainStorage = _storageServiceMainProperty?.GetValue(storageService);
                if (mainStorage == null) return 0;

                return (int?)_mainStorageGetAmountMethod?.Invoke(mainStorage, new object[] { goodName }) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // ========================================
        // HELPER METHODS
        // ========================================

        /// <summary>
        /// Get display name from a GoodRef object.
        /// </summary>
        private static string GetGoodRefDisplayName(object goodRef)
        {
            if (goodRef == null) return null;

            try
            {
                // First get the GoodModel from the 'good' field
                var goodModel = goodRef.GetType().GetField("good", GameReflection.PublicInstance)?.GetValue(goodRef);
                if (goodModel == null) return null;

                // Try to get the Name property from GoodModel (inherited from SO)
                var goodName = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance)?.GetValue(goodModel) as string;
                if (!string.IsNullOrEmpty(goodName))
                {
                    // Look up the display name using the good ID
                    return GetGoodDisplayName(goodName);
                }

                // Fallback: try displayName field directly (it's a LocaText)
                var displayNameField = goodModel.GetType().GetField("displayName", GameReflection.PublicInstance)?.GetValue(goodModel);
                if (displayNameField != null)
                {
                    return GameReflection.GetLocaText(displayNameField);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get goods from a BuildingStorage component.
        /// </summary>
        private static Dictionary<string, int> GetBuildingStorageGoodsInternal(object storage)
        {
            var result = new Dictionary<string, int>();

            try
            {
                // BuildingStorage.Goods property
                EnsureStorageTypes();
                var goodsCollection = _storageGoodsProperty?.GetValue(storage);
                if (goodsCollection == null) return result;

                // BuildingGoodsCollection.goods property - use reflection to iterate
                // (direct cast to Dictionary<string, int> fails at runtime)
                var goodsDict = _goodsCollectionGoodsProperty?.GetValue(goodsCollection);
                if (goodsDict == null) return result;

                // Iterate through the dictionary using reflection
                var keysProperty = goodsDict.GetType().GetProperty("Keys");
                var keys = keysProperty?.GetValue(goodsDict) as System.Collections.IEnumerable;
                if (keys == null) return result;

                var indexer = goodsDict.GetType().GetProperty("Item");

                foreach (var key in keys)
                {
                    string goodName = key as string;
                    if (string.IsNullOrEmpty(goodName)) continue;

                    int amount = (int?)indexer?.GetValue(goodsDict, new[] { key }) ?? 0;
                    if (amount > 0)
                    {
                        result[goodName] = amount;
                    }
                }
            }
            catch
            {
                // Return empty dictionary on error
            }

            return result;
        }
    }
}
