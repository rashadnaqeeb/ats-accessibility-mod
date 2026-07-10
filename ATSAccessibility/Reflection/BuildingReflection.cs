using ATSAccessibility.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to building panel and building internals.
	/// Follows same patterns as GameReflection.cs - cache reflection metadata, never cache instances.
	/// </summary>
	public static class BuildingReflection {
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
		private static MethodInfo _buildingIsFinishedMethod = null;  // Building.IsFinished()
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
		private static MethodInfo _farmSwitchProductionOfMethod = null;  // Farm.SwitchProductionOf(RecipeState)
		private static bool _farmTypesCached = false;

		// Farmfield type (individual farm field tiles)
		private static Type _farmfieldType = null;
		private static FieldInfo _farmfieldStateField = null;  // Farmfield.state (FarmfieldState)
		private static Type _farmfieldStateType = null;
		private static FieldInfo _farmfieldStatePlowedField = null;  // FarmfieldState.isPlowed (bool)
		private static FieldInfo _farmfieldStatePlantField = null;  // FarmfieldState.plant (FarmfieldPlantState)
		private static Type _farmfieldPlantStateType = null;
		private static FieldInfo _farmfieldPlantRecipeField = null;  // FarmfieldPlantState.recipe (string)
		private static FieldInfo _farmfieldPlantGoodField = null;  // FarmfieldPlantState.good (Good)
		private static FieldInfo _farmfieldPlantMultiplierField = null;  // FarmfieldPlantState.multiplier (int)
		private static bool _farmfieldTypesCached = false;

		// GathererHut type (Harvesters', Trappers', Foragers' Camps — uses GathererHutRecipeModel.refGood)
		private static Type _gathererHutType = null;
		private static bool _gathererHutTypesCached = false;

		// Collector type (uses CollectorRecipeModel.producedGood)
		private static Type _collectorType = null;
		private static bool _collectorTypesCached = false;

		// Mine type (uses MineRecipeModel.producedGood)
		private static Type _mineType = null;
		private static bool _mineTypesCached = false;

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

		// Nearby lakes (the UI calls them ponds) — mirrors NearbyLakesPanel
		private static PropertyInfo _lakesServiceLakesProperty = null;  // ILakesService.Lakes
		private static MethodInfo _lakesServiceInDistanceMethod = null;  // ILakesService.InDistance(FishingHut, Lake)
		private static PropertyInfo _lakeStateProperty = null;  // Lake.State
		private static MethodInfo _lakeGetProducedGoodMethod = null;  // Lake.GetProducedGood()
		private static FieldInfo _lakeStateIsAvailableField = null;  // LakeState.isAvailable
		private static FieldInfo _lakeStateChargesLeftField = null;  // LakeState.chargesLeft
		private static FieldInfo _lakeStateMaxChargesField = null;  // LakeState.maxCharges
		private static bool _nearbyLakeTypesCached = false;

		// RecipeState fields (they are fields, not properties)
		private static FieldInfo _recipeActiveField = null;
		private static FieldInfo _recipeModelField = null;
		private static FieldInfo _recipePrioField = null;
		// WorkshopRecipeState fields
		private static FieldInfo _recipeLimitField = null;
		private static FieldInfo _isLimitLocalField = null;
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
		private static FieldInfo _recipeGoodModelDisplayNameField = null;  // GoodModel.displayName (LocaText)
		private static PropertyInfo _recipeGoodModelNameProperty = null;  // GoodModel.Name
																		  // Camp recipe info (CampRecipeModel has refGood instead of producedGood)
		private static MethodInfo _settingsGetCampRecipeMethod = null;  // Settings.GetCampRecipe(name)
		private static FieldInfo _campRecipeModelRefGoodField = null;  // CampRecipeModel.refGood (GoodRef)
																	   // Fishing Hut recipe info (FishingHutRecipeModel has refGood instead of producedGood)
		private static MethodInfo _settingsGetFishingHutRecipeMethod = null;  // Settings.GetFishingHutRecipe(name)
		private static FieldInfo _fishingHutRecipeModelRefGoodField = null;  // FishingHutRecipeModel.refGood (GoodRef)
																			 // GathererHut recipe info (GathererHutRecipeModel has refGood)
		private static MethodInfo _settingsGetGathererHutRecipeMethod = null;  // Settings.GetGathererHutRecipe(name)
		private static FieldInfo _gathererHutRecipeModelRefGoodField = null;  // GathererHutRecipeModel.refGood (GoodRef)
																			  // Collector recipe info (CollectorRecipeModel has producedGood)
		private static MethodInfo _settingsGetCollectorRecipeMethod = null;  // Settings.GetCollectorRecipe(name)
		private static FieldInfo _collectorRecipeModelProducedGoodField = null;  // CollectorRecipeModel.producedGood (GoodRef)
																				 // Mine recipe info (MineRecipeModel has producedGood)
		private static MethodInfo _settingsGetMineRecipeMethod = null;  // Settings.GetMineRecipe(name)
		private static FieldInfo _mineRecipeModelProducedGoodField = null;  // MineRecipeModel.producedGood (GoodRef)
		private static bool _recipeModelTypesCached = false;

		// IngredientState fields
		private static FieldInfo _ingredientGoodField = null;
		private static FieldInfo _ingredientAllowedField = null;
		private static FieldInfo _ingredientPriorityField = null;
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

		// RacesService for race bonuses
		private static PropertyInfo _racesServiceRacesProperty = null;  // IRacesService.Races (RaceModel[])
		private static FieldInfo _raceModelCharacteristicsField = null;  // RaceModel.characteristics (RaceCharacteristicModel[])
		private static FieldInfo _raceModelPassiveEffectDescField = null;  // RaceModel.passiveEffectLongDesc (LocaText) - firekeeper effect
		private static FieldInfo _raceCharacteristicTagField = null;  // RaceCharacteristicModel.tag (BuildingTagModel)
		private static FieldInfo _raceCharacteristicEffectField = null;  // RaceCharacteristicModel.effect (VillagerPerkModel)
		private static FieldInfo _raceCharacteristicGlobalEffectField = null;  // RaceCharacteristicModel.globalEffect (EffectModel)
		private static FieldInfo _raceCharacteristicBuildingPerkField = null;  // RaceCharacteristicModel.buildingPerk (BuildingPerkModel)
		private static FieldInfo _buildingModelTagsField = null;  // BuildingModel.tags (BuildingTagModel[])
		private static FieldInfo _buildingTagDisplayNameField = null;  // BuildingTagModel.displayName (LocaText)
		private static FieldInfo _villagerPerkDisplayNameField = null;  // VillagerPerkModel.displayName (LocaText)
		private static PropertyInfo _effectModelDisplayNameProperty = null;  // EffectModel.DisplayName (string)
		private static PropertyInfo _buildingPerkDisplayNameProperty = null;  // BuildingPerkModel.DisplayName (string)
		private static bool _raceBonusTypesCached = false;

		// ProductionBuilding for profession and workplaces
		private static PropertyInfo _professionProperty = null;  // ProductionBuilding.Profession
		private static PropertyInfo _workplacesProperty = null;  // ProductionBuilding.Workplaces
		private static bool _professionTypesCached = false;

		// BuildingStorage (ProductionStorage) for output goods
		private static PropertyInfo _storageGoodsProperty = null;  // BuildingStorage.Goods
																   // Note: _goodsCollectionGoodsField is shared with IngredientsStorage (defined above)
		private static MethodInfo _storageGetDeliveryStateMethod = null;  // BuildingGoodsCollection.GetDeliveryState(string)
		private static MethodInfo _storageSwitchForceDeliveryMethod = null;  // BuildingStorage.SwitchForceDelivery(string, GoodDeliveryState)
		private static MethodInfo _storageSwitchConstantForceDeliveryMethod = null;  // BuildingStorage.SwitchConstantForceDelivery(string, GoodDeliveryState)
		private static FieldInfo _deliveryStateForcedField = null;  // GoodDeliveryState.deliveryForced
		private static FieldInfo _deliveryStateConstantForcedField = null;  // GoodDeliveryState.constantDeliveryForced
		private static bool _storageTypesCached = false;

		// Hearth base type (kept here for IsHearth type-check used by BuildingPanelHandler routing)
		private static Type _hearthType = null;
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

		// Relic base type (kept here for IsRelic type-check used by BuildingPanelHandler routing)
		private static Type _relicType = null;
		private static bool _relicTypesCached = false;

		// EffectModel Description and IsPositive (used by GetCycleAbilityDescription and RelicReflection)
		private static PropertyInfo _effectModelDescriptionProperty = null;  // EffectModel.Description (string)
		private static PropertyInfo _effectModelIsPositiveProperty = null;  // EffectModel.IsPositive (bool)
		private static bool _effectDescriptionTypesCached = false;

		// GoodsCollection.GetAmount (used by RelicReflection and other code)
		private static MethodInfo _goodsCollectionGetAmountMethod = null;  // GoodsCollection.GetAmount(string)

		// Port base type (kept here for IsPort type-check used by BuildingPanelHandler routing)
		private static Type _portType = null;
		private static bool _portTypesCached = false;

		// Decoration-specific
		private static Type _decorationType = null;
		private static bool _decorationTypesCached = false;

		// Storage-specific (main storage building)
		private static Type _storageType = null;
		private static bool _storageTypesCached2 = false;  // _storageTypesCached already used for BuildingStorage

		// Institution-specific fields in InstitutionReflection.cs
		// Shrine-specific fields in ShrineReflection.cs
		// Poro-specific fields in PoroReflection.cs

		// RainCatcher-specific
		private static Type _rainCatcherType = null;
		private static FieldInfo _rainCatcherStateField = null;  // RainCatcher.state
		private static FieldInfo _rainCatcherModelField = null;  // RainCatcher.model
		private static MethodInfo _rainCatcherGetCurrentWaterTypeMethod = null;  // RainCatcher.GetCurrentWaterType()
		private static bool _rainCatcherTypesCached = false;

		// Extractor-specific
		private static Type _extractorType = null;
		private static Type _extractorModelType = null;
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
		private static FieldInfo _waterModelDisplayNameField = null;  // WaterModel.displayName
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
		private static MethodInfo _rainpunkGetWaterPerCystsMethod = null;  // IRainpunkService.GetWaterPerCysts(Workshop)
		private static MethodInfo _rainpunkIsWaterSpawningBlightMethod = null;  // IRainpunkService.IsWaterSpawningBlight(Workshop)
		private static FieldInfo _wsWaterUsedField = null;  // WorkshopState.waterUsed
		private static FieldInfo _engineModelWaterPerSecField = null;  // RainpunkEngineModel.waterPerSec
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
		private static FieldInfo _brpWaterField = null;  // BuildingRainpunkModel.water
		private static FieldInfo _engineStateIndexField = null;  // RainpunkEngineState.index
		private static FieldInfo _engineStateLevelField = null;  // RainpunkEngineState.level
		private static FieldInfo _engineStateRequestedLevelField = null;  // RainpunkEngineState.requestedLevel
		private static FieldInfo _engineModelMaxLevelField = null;  // RainpunkEngineModel.maxLevel
		private static FieldInfo _engineModelLevelsField = null;  // RainpunkEngineModel.levels (RainpunkEngineLevel[])
		private static FieldInfo _engineLevelPerkField = null;  // RainpunkEngineLevel.perk (BuildingPerkModel)
		private static PropertyInfo _buildingPerkDisplayNameProp = null;  // BuildingPerkModel.DisplayName
		private static FieldInfo _engineModelUpSoundField = null;  // RainpunkEngineModel.upSound (SoundRef)
		private static FieldInfo _engineModelDownSoundField = null;  // RainpunkEngineModel.downSound (SoundRef)
		private static Type _soundRefType = null;
		private static MethodInfo _soundRefGetNextMethod = null;  // SoundRef.GetNext()
		private static bool _rainpunkEngineTypesCached = false;

		// Building Upgrades (UpgradableBuilding system)
		private static Type _upgradableBuildingType = null;
		private static Type _upgradableBuildingModelType = null;
		private static Type _upgradableBuildingStateType = null;
		private static Type _buildingLevelModelType = null;
		private static Type _goodsSetType = null;
		private static PropertyInfo _upgradableModelProperty = null;  // UpgradableBuilding.UpgradableModel
		private static PropertyInfo _upgradableStateProperty = null;  // UpgradableBuilding.UpgradableState
		private static PropertyInfo _hasUpgradesProperty = null;  // UpgradableBuilding.HasUpgrades
		private static FieldInfo _upgradableModelLevelsField = null;  // UpgradableBuildingModel.levels (BuildingLevelModel[])
		private static FieldInfo _upgradableStateLevelField = null;  // UpgradableBuildingState.level
		private static FieldInfo _upgradableStateUpgradesField = null;  // UpgradableBuildingState.upgrades (bool[][])
		private static FieldInfo _levelModelRequiredGoodsField = null;  // BuildingLevelModel.requiredGoods (GoodsSet[])
		private static FieldInfo _levelModelOptionsField = null;  // BuildingLevelModel.options (BuildingPerkModel[])
		private static FieldInfo _goodsSetGoodsField = null;  // GoodsSet.goods (GoodRef[])
		private static FieldInfo _buildingPerkDescField = null;  // BuildingPerkModel.description (LocaText)
		private static MethodInfo _buildingPerkGetDescMethod = null;  // BuildingPerkModel.GetDescription(building)
		private static bool _upgradeTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsurePanelTypes() {
			if (_panelTypesCached) return;
			_panelTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Panel", assembly => {
				_buildingPanelType = assembly.GetType("Eremite.Buildings.UI.BuildingPanel");
				if (_buildingPanelType != null) {
					_currentBuildingField = _buildingPanelType.GetField("currentBuilding",
						BindingFlags.Public | BindingFlags.Static);
				}
			});
		}

		internal static void EnsureBuildingTypes() {
			if (_buildingTypesCached) return;
			_buildingTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Building", assembly => {
				var buildingType = assembly.GetType("Eremite.Buildings.Building");
				if (buildingType != null) {
					_buildingModelProperty = buildingType.GetProperty("BuildingModel", GameReflection.PublicInstance);
					_buildingStateProperty = buildingType.GetProperty("BuildingState", GameReflection.PublicInstance);
					_buildingIdProperty = buildingType.GetProperty("Id", GameReflection.PublicInstance);
					_buildingDisplayNameProperty = buildingType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_buildingIsFinishedMethod = buildingType.GetMethod("IsFinished", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureModelTypes() {
			if (_modelTypesCached) return;
			_modelTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Model", assembly => {
				var modelType = assembly.GetType("Eremite.Buildings.BuildingModel");
				if (modelType != null) {
					_modelDescriptionProperty = modelType.GetProperty("Description", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureStateTypes() {
			if (_stateTypesCached) return;
			_stateTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.State", assembly => {
				var stateType = assembly.GetType("Eremite.Buildings.BuildingState");
				if (stateType != null) {
					_stateFinishedField = stateType.GetField("finished", GameReflection.PublicInstance);
					_stateIsSleepingField = stateType.GetField("isSleeping", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureProductionTypes() {
			if (_productionTypesCached) return;
			_productionTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Production", assembly => {
				_productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
				if (_productionBuildingType != null) {
					_workersProperty = _productionBuildingType.GetProperty("Workers", GameReflection.PublicInstance);
					_productionStorageProperty = _productionBuildingType.GetProperty("ProductionStorage", GameReflection.PublicInstance);
					_productionBuildingStateProperty = _productionBuildingType.GetProperty("ProductionBuildingState", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureWorkshopTypes() {
			if (_workshopTypesCached) return;
			_workshopTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Workshop", assembly => {
				_workshopInterfaceType = assembly.GetType("Eremite.Buildings.IWorkshop");
				if (_workshopInterfaceType != null) {
					_workshopRecipesProperty = _workshopInterfaceType.GetProperty("Recipes", GameReflection.PublicInstance);
					_workshopIngredientsStorageProperty = _workshopInterfaceType.GetProperty("IngredientsStorage", GameReflection.PublicInstance);
					_switchProductionOfMethod = _workshopInterfaceType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureCampTypes() {
			if (_campTypesCached) return;
			_campTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Camp", assembly => {
				_campType = assembly.GetType("Eremite.Buildings.Camp");
				if (_campType != null) {
					_campStateField = _campType.GetField("state", GameReflection.PublicInstance);
					_campSwitchProductionOfMethod = _campType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
					_campSetModeMethod = _campType.GetMethod("SetMode", GameReflection.PublicInstance);
				}

				var campStateType = assembly.GetType("Eremite.Buildings.CampState");
				if (campStateType != null) {
					_campStateRecipesField = campStateType.GetField("recipes", GameReflection.PublicInstance);
					_campStateModeField = campStateType.GetField("mode", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureGathererHutTypes() {
			if (_gathererHutTypesCached) return;
			_gathererHutTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.GathererHut", assembly => {
				_gathererHutType = assembly.GetType("Eremite.Buildings.GathererHut");
			});
		}

		private static void EnsureCollectorTypes() {
			if (_collectorTypesCached) return;
			_collectorTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Collector", assembly => {
				_collectorType = assembly.GetType("Eremite.Buildings.Collector");
			});
		}

		private static void EnsureMineTypes() {
			if (_mineTypesCached) return;
			_mineTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Mine", assembly => {
				_mineType = assembly.GetType("Eremite.Buildings.Mine");
			});
		}

		private static void EnsureFarmTypes() {
			if (_farmTypesCached) return;
			_farmTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Farm", assembly => {
				_farmType = assembly.GetType("Eremite.Buildings.Farm");
				if (_farmType != null) {
					_farmStateField = _farmType.GetField("state", GameReflection.PublicInstance);
					_farmCountSownFieldsMethod = _farmType.GetMethod("CountSownFieldsInRange", GameReflection.PublicInstance);
					_farmCountPlowedFieldsMethod = _farmType.GetMethod("CountPlownFieldsInRange", GameReflection.PublicInstance);  // Note: typo in game code
					_farmCountAllFieldsMethod = _farmType.GetMethod("CountAllReaveleadFieldsInRange", GameReflection.PublicInstance);  // Note: typo in game code
					_farmSwitchProductionOfMethod = _farmType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureFarmfieldTypes() {
			if (_farmfieldTypesCached) return;
			_farmfieldTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Farmfield", assembly => {
				_farmfieldType = assembly.GetType("Eremite.Buildings.Farmfield");
				if (_farmfieldType != null) {
					_farmfieldStateField = _farmfieldType.GetField("state", GameReflection.PublicInstance);
				}

				_farmfieldStateType = assembly.GetType("Eremite.Buildings.FarmfieldState");
				if (_farmfieldStateType != null) {
					_farmfieldStatePlowedField = _farmfieldStateType.GetField("isPlowed", GameReflection.PublicInstance);
					_farmfieldStatePlantField = _farmfieldStateType.GetField("plant", GameReflection.PublicInstance);
				}

				_farmfieldPlantStateType = assembly.GetType("Eremite.Buildings.FarmfieldPlantState");
				if (_farmfieldPlantStateType != null) {
					_farmfieldPlantRecipeField = _farmfieldPlantStateType.GetField("recipe", GameReflection.PublicInstance);
					_farmfieldPlantGoodField = _farmfieldPlantStateType.GetField("good", GameReflection.PublicInstance);
					_farmfieldPlantMultiplierField = _farmfieldPlantStateType.GetField("multiplier", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureFishingHutTypes() {
			if (_fishingHutTypesCached) return;
			_fishingHutTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.FishingHut", assembly => {
				_fishingHutType = assembly.GetType("Eremite.Buildings.FishingHut");
				if (_fishingHutType != null) {
					_fishingHutStateField = _fishingHutType.GetField("state", GameReflection.PublicInstance);
					_fishingHutModelField = _fishingHutType.GetField("model", GameReflection.PublicInstance);
					_fishingHutChangeModeMethod = _fishingHutType.GetMethod("ChangeMode", GameReflection.PublicInstance);
					_fishingHutSwitchProductionOfMethod = _fishingHutType.GetMethod("SwitchProductionOf", GameReflection.PublicInstance);
				}

				var fishingHutStateType = assembly.GetType("Eremite.Buildings.FishingHutState");
				if (fishingHutStateType != null) {
					_fishingHutStateBaitModeField = fishingHutStateType.GetField("baitMode", GameReflection.PublicInstance);
					_fishingHutStateBaitChargesField = fishingHutStateType.GetField("baitChargesLeft", GameReflection.PublicInstance);
					_fishingHutStateRecipesField = fishingHutStateType.GetField("recipes", GameReflection.PublicInstance);
				}

				var fishingHutModelType = assembly.GetType("Eremite.Buildings.FishingHutModel");
				if (fishingHutModelType != null) {
					_fishingHutModelBaitIngredientField = fishingHutModelType.GetField("baitIngredient", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureNearbyLakeTypes() {
			if (_nearbyLakeTypesCached) return;
			_nearbyLakeTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.NearbyLakes", assembly => {
				var lakesServiceType = assembly.GetType("Eremite.Services.ILakesService");
				var lakeType = assembly.GetType("Eremite.MapObjects.Lake");

				if (lakesServiceType != null) {
					_lakesServiceLakesProperty = lakesServiceType.GetProperty("Lakes", GameReflection.PublicInstance);
					_lakesServiceInDistanceMethod = lakesServiceType.GetMethod("InDistance", GameReflection.PublicInstance);
				}

				if (lakeType != null) {
					_lakeStateProperty = lakeType.GetProperty("State", GameReflection.PublicInstance);
					_lakeGetProducedGoodMethod = lakeType.GetMethod("GetProducedGood", GameReflection.PublicInstance);
				}

				var lakeStateType = assembly.GetType("Eremite.MapObjects.LakeState");
				if (lakeStateType != null) {
					_lakeStateIsAvailableField = lakeStateType.GetField("isAvailable", GameReflection.PublicInstance);
					_lakeStateChargesLeftField = lakeStateType.GetField("chargesLeft", GameReflection.PublicInstance);
					_lakeStateMaxChargesField = lakeStateType.GetField("maxCharges", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureRecipeTypes() {
			if (_recipeTypesCached) return;
			_recipeTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Recipe", assembly => {
				// RecipeState fields
				var recipeStateType = assembly.GetType("Eremite.Buildings.RecipeState");
				if (recipeStateType != null) {
					_recipeActiveField = recipeStateType.GetField("active", GameReflection.PublicInstance);
					_recipeModelField = recipeStateType.GetField("model", GameReflection.PublicInstance);
					_recipePrioField = recipeStateType.GetField("prio", GameReflection.PublicInstance);
				}

				// WorkshopRecipeState fields
				var workshopRecipeStateType = assembly.GetType("Eremite.Buildings.WorkshopRecipeState");
				if (workshopRecipeStateType != null) {
					_recipeLimitField = workshopRecipeStateType.GetField("limit", GameReflection.PublicInstance);
					_isLimitLocalField = workshopRecipeStateType.GetField("isLimitLocal", GameReflection.PublicInstance);
					_recipeProductNameField = workshopRecipeStateType.GetField("productName", GameReflection.PublicInstance);
					_recipeIngredientsField = workshopRecipeStateType.GetField("ingredients", GameReflection.PublicInstance);
				}
			});
		}

		internal static void EnsureRecipeModelTypes() {
			if (_recipeModelTypesCached) return;
			_recipeModelTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RecipeModel", assembly => {
				// Settings.GetRecipe method
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetRecipeMethod = settingsType.GetMethod("GetRecipe", GameReflection.PublicInstance);
				}

				// WorkshopRecipeModel fields
				var recipeModelType = assembly.GetType("Eremite.Buildings.WorkshopRecipeModel");
				if (recipeModelType != null) {
					_recipeModelProductionTimeField = recipeModelType.GetField("productionTime", GameReflection.PublicInstance);
					_recipeModelProducedGoodField = recipeModelType.GetField("producedGood", GameReflection.PublicInstance);
				}

				// RecipeModel.grade field (in base class)
				var baseRecipeModelType = assembly.GetType("Eremite.Buildings.RecipeModel");
				if (baseRecipeModelType != null) {
					_recipeModelGradeField = baseRecipeModelType.GetField("grade", GameReflection.PublicInstance);
				}

				// RecipeGradeModel.level field
				var gradeModelType = assembly.GetType("Eremite.Buildings.RecipeGradeModel");
				if (gradeModelType != null) {
					_gradeModelLevelField = gradeModelType.GetField("level", GameReflection.PublicInstance);
				}

				// GoodModel displayName field (LocaText)
				var goodModelType = assembly.GetType("Eremite.Model.GoodModel");
				if (goodModelType != null) {
					_recipeGoodModelDisplayNameField = goodModelType.GetField("displayName", GameReflection.PublicInstance);
					_recipeGoodModelNameProperty = goodModelType.GetProperty("Name", GameReflection.PublicInstance);
				}

				// Camp recipe support: Settings.GetCampRecipe + CampRecipeModel.refGood
				if (settingsType != null) {
					_settingsGetCampRecipeMethod = settingsType.GetMethod("GetCampRecipe", GameReflection.PublicInstance);
				}

				var campRecipeModelType = assembly.GetType("Eremite.Buildings.CampRecipeModel");
				if (campRecipeModelType != null) {
					_campRecipeModelRefGoodField = campRecipeModelType.GetField("refGood", GameReflection.PublicInstance);
				}

				// Fishing Hut recipe support: Settings.GetFishingHutRecipe + FishingHutRecipeModel.refGood
				if (settingsType != null) {
					_settingsGetFishingHutRecipeMethod = settingsType.GetMethod("GetFishingHutRecipe", GameReflection.PublicInstance);
				}

				var fishingHutRecipeModelType = assembly.GetType("Eremite.Buildings.FishingHutRecipeModel");
				if (fishingHutRecipeModelType != null) {
					_fishingHutRecipeModelRefGoodField = fishingHutRecipeModelType.GetField("refGood", GameReflection.PublicInstance);
				}

				// GathererHut recipe support: Settings.GetGathererHutRecipe + GathererHutRecipeModel.refGood
				if (settingsType != null) {
					_settingsGetGathererHutRecipeMethod = settingsType.GetMethod("GetGathererHutRecipe", GameReflection.PublicInstance);
				}

				var gathererHutRecipeModelType = assembly.GetType("Eremite.Buildings.GathererHutRecipeModel");
				if (gathererHutRecipeModelType != null) {
					_gathererHutRecipeModelRefGoodField = gathererHutRecipeModelType.GetField("refGood", GameReflection.PublicInstance);
				}

				// Collector recipe support: Settings.GetCollectorRecipe + CollectorRecipeModel.producedGood
				if (settingsType != null) {
					_settingsGetCollectorRecipeMethod = settingsType.GetMethod("GetCollectorRecipe", GameReflection.PublicInstance);
				}

				var collectorRecipeModelType = assembly.GetType("Eremite.Buildings.CollectorRecipeModel");
				if (collectorRecipeModelType != null) {
					_collectorRecipeModelProducedGoodField = collectorRecipeModelType.GetField("producedGood", GameReflection.PublicInstance);
				}

				// Mine recipe support: Settings.GetMineRecipe + MineRecipeModel.producedGood
				if (settingsType != null) {
					_settingsGetMineRecipeMethod = settingsType.GetMethod("GetMineRecipe", GameReflection.PublicInstance);
				}

				var mineRecipeModelType = assembly.GetType("Eremite.Buildings.MineRecipeModel");
				if (mineRecipeModelType != null) {
					_mineRecipeModelProducedGoodField = mineRecipeModelType.GetField("producedGood", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureIngredientTypes() {
			if (_ingredientTypesCached) return;
			_ingredientTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Ingredient", assembly => {
				var ingredientStateType = assembly.GetType("Eremite.Buildings.IngredientState");
				if (ingredientStateType != null) {
					_ingredientGoodField = ingredientStateType.GetField("good", GameReflection.PublicInstance);
					_ingredientAllowedField = ingredientStateType.GetField("allowed", GameReflection.PublicInstance);
					_ingredientPriorityField = ingredientStateType.GetField("priority", GameReflection.PublicInstance);
				}

				// Good struct has amount field
				var goodType = assembly.GetType("Eremite.Model.Good");
				if (goodType != null) {
					_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureEventTypes() {
			if (_eventTypesCached) return;
			_eventTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Event", assembly => {
				var blackboardType = assembly.GetType("Eremite.Services.IGameBlackboardService");
				if (blackboardType != null) {
					_onBuildingPanelShownProperty = blackboardType.GetProperty("OnBuildingPanelShown", GameReflection.PublicInstance);
					_onBuildingPanelClosedProperty = blackboardType.GetProperty("OnBuildingPanelClosed", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureActorTypes() {
			if (_actorTypesCached) return;
			_actorTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Actor", assembly => {
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_actorsServiceProperty = gameServicesType.GetProperty("ActorsService", GameReflection.PublicInstance);
				}

				var actorsServiceType = assembly.GetType("Eremite.Services.IActorsService");
				if (actorsServiceType != null) {
					_getActorMethod = actorsServiceType.GetMethod("GetActor", new[] { typeof(int) });
				}
			});
		}

		private static void EnsureActorProperties() {
			if (_actorPropertiesCached) return;
			_actorPropertiesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.ActorProps", assembly => {
				var actorType = assembly.GetType("Eremite.Characters.Actor");
				if (actorType != null) {
					// Actor.ActorState property (returns VillagerState for villagers)
					_actorStateProperty = actorType.GetProperty("ActorState", GameReflection.PublicInstance);
					// Actor.GetTaskDescription() method
					_getTaskDescriptionMethod = actorType.GetMethod("GetTaskDescription", GameReflection.PublicInstance);
				}

				// VillagerState fields (stores the villager's name and race)
				var villagerStateType = assembly.GetType("Eremite.Characters.Villagers.VillagerState");
				if (villagerStateType != null) {
					_villagerStateNameField = villagerStateType.GetField("name", GameReflection.PublicInstance);
					_villagerStateRaceField = villagerStateType.GetField("race", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureVillagersServiceTypes() {
			if (_villagersServiceTypesCached) return;
			_villagersServiceTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.VillagersService", assembly => {
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_villagersServiceProperty = gameServicesType.GetProperty("VillagersService", GameReflection.PublicInstance);
				}

				var villagersServiceType = assembly.GetType("Eremite.Services.IVillagersService");
				if (villagersServiceType != null) {
					_getDefaultProfessionAmountMethod = villagersServiceType.GetMethod("GetDefaultProfessionAmount", new[] { typeof(string) });
					_releaseFromProfessionMethod = villagersServiceType.GetMethod("ReleaseFromProfession", GameReflection.PublicInstance);
					_getVillagerMethod = villagersServiceType.GetMethod("GetVillager", new[] { typeof(int) });
					_villagersServiceRacesProperty = villagersServiceType.GetProperty("Races", GameReflection.PublicInstance);

					// These methods have specific parameter types
					var villagerType = assembly.GetType("Eremite.Characters.Villagers.Villager");
					var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
					if (villagerType != null && productionBuildingType != null) {
						_getDefaultProfessionVillagerMethod = villagersServiceType.GetMethod("GetDefaultProfessionVillager",
							new[] { typeof(string), productionBuildingType });
						_setProfessionMethod = villagersServiceType.GetMethod("SetProfession",
							new[] { villagerType, typeof(string), productionBuildingType, typeof(int), typeof(bool) });
					}
				}
			});
		}

		internal static void EnsureRaceBonusTypes() {
			if (_raceBonusTypesCached) return;
			_raceBonusTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RaceBonus", assembly => {
				// IRacesService.Races property
				var racesServiceType = assembly.GetType("Eremite.Services.IRacesService");
				if (racesServiceType != null) {
					_racesServiceRacesProperty = racesServiceType.GetProperty("Races", GameReflection.PublicInstance);
				}

				// RaceModel fields
				var raceModelType = assembly.GetType("Eremite.Model.RaceModel");
				if (raceModelType != null) {
					_raceModelCharacteristicsField = raceModelType.GetField("characteristics", GameReflection.PublicInstance);
					_raceModelPassiveEffectDescField = raceModelType.GetField("passiveEffectLongDesc", GameReflection.PublicInstance);
				}

				// RaceCharacteristicModel fields
				var raceCharacteristicType = assembly.GetType("Eremite.Model.RaceCharacteristicModel");
				if (raceCharacteristicType != null) {
					_raceCharacteristicTagField = raceCharacteristicType.GetField("tag", GameReflection.PublicInstance);
					_raceCharacteristicEffectField = raceCharacteristicType.GetField("effect", GameReflection.PublicInstance);
					_raceCharacteristicGlobalEffectField = raceCharacteristicType.GetField("globalEffect", GameReflection.PublicInstance);
					_raceCharacteristicBuildingPerkField = raceCharacteristicType.GetField("buildingPerk", GameReflection.PublicInstance);
				}

				// VillagerPerkModel.displayName field
				var villagerPerkType = assembly.GetType("Eremite.Characters.Villagers.VillagerPerkModel");
				if (villagerPerkType != null) {
					_villagerPerkDisplayNameField = villagerPerkType.GetField("displayName", GameReflection.PublicInstance);
				}

				// EffectModel.DisplayName property
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectModelDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
				}

				// BuildingPerkModel.DisplayName property
				var buildingPerkModelType = assembly.GetType("Eremite.Model.BuildingPerkModel");
				if (buildingPerkModelType != null) {
					_buildingPerkDisplayNameProperty = buildingPerkModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
				}

				// BuildingModel.tags field
				var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
				if (buildingModelType != null) {
					_buildingModelTagsField = buildingModelType.GetField("tags", GameReflection.PublicInstance);
				}

				// BuildingTagModel.displayName field
				var buildingTagModelType = assembly.GetType("Eremite.Buildings.BuildingTagModel");
				if (buildingTagModelType != null) {
					_buildingTagDisplayNameField = buildingTagModelType.GetField("displayName", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureProfessionTypes() {
			if (_professionTypesCached) return;
			_professionTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Profession", assembly => {
				var productionBuildingType = assembly.GetType("Eremite.Buildings.ProductionBuilding");
				if (productionBuildingType != null) {
					_professionProperty = productionBuildingType.GetProperty("Profession", GameReflection.PublicInstance);
					_workplacesProperty = productionBuildingType.GetProperty("Workplaces", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureStorageTypes() {
			if (_storageTypesCached) return;
			_storageTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Storage", assembly => {
				var buildingStorageType = assembly.GetType("Eremite.Buildings.BuildingStorage");
				if (buildingStorageType != null) {
					_storageGoodsProperty = buildingStorageType.GetProperty("Goods", GameReflection.PublicInstance);
					_storageSwitchForceDeliveryMethod = buildingStorageType.GetMethod("SwitchForceDelivery", GameReflection.PublicInstance);
					_storageSwitchConstantForceDeliveryMethod = buildingStorageType.GetMethod("SwitchConstantForceDelivery", GameReflection.PublicInstance);
				}

				var goodsCollectionType = assembly.GetType("Eremite.Buildings.BuildingGoodsCollection");
				if (goodsCollectionType != null) {
					_storageGetDeliveryStateMethod = goodsCollectionType.GetMethod("GetDeliveryState", GameReflection.PublicInstance);
				}

				// goods field is on GoodsCollection base class, not BuildingGoodsCollection
				var baseGoodsCollectionType = assembly.GetType("Eremite.GoodsCollection");
				if (baseGoodsCollectionType != null) {
					_goodsCollectionGoodsField = baseGoodsCollectionType.GetField("goods", GameReflection.PublicInstance);
				}

				var deliveryStateType = assembly.GetType("Eremite.Buildings.GoodDeliveryState");
				if (deliveryStateType != null) {
					_deliveryStateForcedField = deliveryStateType.GetField("deliveryForced", GameReflection.PublicInstance);
					_deliveryStateConstantForcedField = deliveryStateType.GetField("constantDeliveryForced", GameReflection.PublicInstance);
				}
			});
		}

		private static void EnsureIngredientsStorageTypes() {
			if (_ingredientsStorageTypesCached) return;
			_ingredientsStorageTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.IngredientsStorage", assembly => {
				var ingredientsStorageType = assembly.GetType("Eremite.Buildings.BuildingIngredientsStorage");
				if (ingredientsStorageType != null) {
					_ingredientsStorageGoodsField = ingredientsStorageType.GetField("goods", GameReflection.PublicInstance);
				}

				var goodsCollectionType = assembly.GetType("Eremite.GoodsCollection");
				if (goodsCollectionType != null) {
					_goodsCollectionGoodsField = goodsCollectionType.GetField("goods", GameReflection.PublicInstance);
				}
			});
		}

		// EnsureHearthTypes: Only caches the Hearth type needed for IsHearth() type-check.
		// All other hearth reflection is in HearthReflection.cs.
		internal static void EnsureHearthBaseType() {
			if (_hearthTypesCached) return;
			_hearthTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.HearthBase", assembly => {
				_hearthType = assembly.GetType("Eremite.Buildings.Hearth");
			});
		}

		// Internal accessor for HearthReflection to get the cached Hearth type
		internal static Type HearthType {
			get { EnsureHearthBaseType(); return _hearthType; }
		}

		internal static void EnsureHouseTypes() {
			if (_houseTypesCached) return;
			_houseTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.House", assembly => {
				_houseType = assembly.GetType("Eremite.Buildings.House");
				if (_houseType != null) {
					_houseStateField = _houseType.GetField("state", GameReflection.PublicInstance);
					_houseModelField = _houseType.GetField("model", GameReflection.PublicInstance);
					_houseGetHousingPlacesMethod = _houseType.GetMethod("GetHousingPlaces", GameReflection.PublicInstance);
					_houseGetMaxHousingPlacesMethod = _houseType.GetMethod("GetMaxHousingPlaces", GameReflection.PublicInstance);
					_houseIsFullMethod = _houseType.GetMethod("IsFull", GameReflection.PublicInstance);
				}

				var houseStateType = assembly.GetType("Eremite.Buildings.HouseState");
				if (houseStateType != null) {
					_houseStateResidentsField = houseStateType.GetField("residents", GameReflection.PublicInstance);
				}

			});
		}

		// Internal accessors for HearthReflection (hub tier population counting)
		internal static FieldInfo HouseStateField { get { EnsureHouseTypes(); return _houseStateField; } }
		internal static FieldInfo HouseStateResidentsField { get { EnsureHouseTypes(); return _houseStateResidentsField; } }

		internal static void EnsureRelicBaseType() {
			if (_relicTypesCached) return;
			_relicTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RelicBase", assembly => {
				_relicType = assembly.GetType("Eremite.Buildings.Relic");
			});
		}

		internal static Type RelicType {
			get { EnsureRelicBaseType(); return _relicType; }
		}

		internal static void EnsureEffectDescriptionTypes() {
			if (_effectDescriptionTypesCached) return;
			_effectDescriptionTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.EffectDescription", assembly => {
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectModelDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
					_effectModelIsPositiveProperty = effectModelType.GetProperty("IsPositive", GameReflection.PublicInstance);
				}

				// GoodsCollection.GetAmount (also needed by RelicReflection)
				if (_goodsCollectionGetAmountMethod == null) {
					var goodsCollectionType = assembly.GetType("Eremite.GoodsCollection");
					if (goodsCollectionType != null) {
						_goodsCollectionGetAmountMethod = goodsCollectionType.GetMethod("GetAmount", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
					}
				}
			});
		}

		internal static PropertyInfo EffectModelDescriptionProperty {
			get { EnsureEffectDescriptionTypes(); return _effectModelDescriptionProperty; }
		}

		internal static PropertyInfo EffectModelIsPositiveProperty {
			get { EnsureEffectDescriptionTypes(); return _effectModelIsPositiveProperty; }
		}

		internal static MethodInfo GoodsCollectionGetAmountMethod {
			get { EnsureEffectDescriptionTypes(); return _goodsCollectionGetAmountMethod; }
		}

		internal static FieldInfo GoodsCollectionGoodsField {
			get { EnsureIngredientsStorageTypes(); return _goodsCollectionGoodsField; }
		}

		internal static MethodInfo SoundRefGetNextMethod {
			get { EnsureRainpunkEngineTypes(); return _soundRefGetNextMethod; }
		}

		internal static FieldInfo GoodsSetGoodsField {
			get { EnsureUpgradeTypes(); return _goodsSetGoodsField; }
		}

		internal static void EnsurePortBaseType() {
			if (_portTypesCached) return;
			_portTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.PortBase", assembly => {
				_portType = assembly.GetType("Eremite.Buildings.Port");
			});
		}

		internal static Type PortType {
			get { EnsurePortBaseType(); return _portType; }
		}

		internal static void EnsureDecorationType() {
			if (_decorationTypesCached) return;
			_decorationTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Decoration", assembly => {
				_decorationType = assembly.GetType("Eremite.Buildings.Decoration");
			});
		}

		private static void EnsureStorageType2() {
			if (_storageTypesCached2) return;
			_storageTypesCached2 = true;

			ReflectionHelper.InitCache("BuildingReflection.Storage2", assembly => {
				_storageType = assembly.GetType("Eremite.Buildings.Storage");
			});
		}

		private static void EnsureRainCatcherTypes() {
			if (_rainCatcherTypesCached) return;
			_rainCatcherTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RainCatcher", assembly => {
				_rainCatcherType = assembly.GetType("Eremite.Buildings.RainCatcher");
				if (_rainCatcherType != null) {
					_rainCatcherStateField = _rainCatcherType.GetField("state", GameReflection.PublicInstance);
					_rainCatcherModelField = _rainCatcherType.GetField("model", GameReflection.PublicInstance);
					_rainCatcherGetCurrentWaterTypeMethod = _rainCatcherType.GetMethod("GetCurrentWaterType", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureExtractorTypes() {
			if (_extractorTypesCached) return;
			_extractorTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Extractor", assembly => {
				_extractorType = assembly.GetType("Eremite.Buildings.Extractor");
				if (_extractorType != null) {
					_extractorStateField = _extractorType.GetField("state", GameReflection.PublicInstance);
					_extractorModelField = _extractorType.GetField("model", GameReflection.PublicInstance);
					_extractorGetWaterTypeMethod = _extractorType.GetMethod("GetWaterType", GameReflection.PublicInstance);
				}

				_extractorModelType = assembly.GetType("Eremite.Buildings.ExtractorModel");
				if (_extractorModelType != null) {
					_extractorModelProductionTimeField = _extractorModelType.GetField("productionTime", GameReflection.PublicInstance);
					_extractorModelProducedAmountField = _extractorModelType.GetField("producedAmount", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureHydrantTypes() {
			if (_hydrantTypesCached) return;
			_hydrantTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Hydrant", assembly => {
				_hydrantType = assembly.GetType("Eremite.Buildings.Hydrant");
				if (_hydrantType != null) {
					_hydrantStateField = _hydrantType.GetField("state", GameReflection.PublicInstance);
					_hydrantModelField = _hydrantType.GetField("model", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureWaterModelTypes() {
			if (_waterModelTypesCached) return;
			_waterModelTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.WaterModel", assembly => {
				var waterModelType = assembly.GetType("Eremite.Model.WaterModel");
				if (waterModelType != null) {
					_waterModelDisplayNameField = waterModelType.GetField("displayName", GameReflection.PublicInstance);
					_waterModelGoodField = waterModelType.GetField("good", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureCycleAbilityTypes() {
			if (_cycleAbilityTypesCached) return;
			_cycleAbilityTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.CycleAbility", assembly => {
				// ConditionsState.cycleAbilities field
				var conditionsStateType = assembly.GetType("Eremite.Model.State.ConditionsState");
				if (conditionsStateType != null) {
					_condCycleAbilitiesField = conditionsStateType.GetField("cycleAbilities", GameReflection.PublicInstance);
				}

				// CycleAbilityState fields
				var cycleAbilityStateType = assembly.GetType("Eremite.WorldMap.CycleAbilityState");
				if (cycleAbilityStateType != null) {
					_cycleAbilityModelField = cycleAbilityStateType.GetField("model", GameReflection.PublicInstance);
					_cycleAbilityGameEffectField = cycleAbilityStateType.GetField("gameEffect", GameReflection.PublicInstance);
					_cycleAbilityChargesField = cycleAbilityStateType.GetField("charges", GameReflection.PublicInstance);
				}

			});
		}

		internal static void EnsureGameModelServiceTypes() {
			if (_gameModelServiceTypesCached) return;
			_gameModelServiceTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.GameModelService", assembly => {
				// IGameServices.GameModelService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsGameModelServiceProperty = gameServicesType.GetProperty("GameModelService", GameReflection.PublicInstance);
				}

				// IGameModelService.GetEffect
				var gameModelServiceType = assembly.GetType("Eremite.Services.IGameModelService");
				if (gameModelServiceType != null) {
					_gmsGetEffectMethod = gameModelServiceType.GetMethod("GetEffect", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
				}

				// EffectModel.displayName and Apply
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectModelDisplayNameField = effectModelType.GetField("displayName", GameReflection.NonPublicInstance);
					_effectModelCanBeDrawnMethod = effectModelType.GetMethod("CanBeDrawn", GameReflection.PublicInstance);
					// Apply method has signature: Apply(EffectContextType, string, int)
					var effectContextType = assembly.GetType("Eremite.Model.Effects.EffectContextType");
					if (effectContextType != null) {
						_effectModelApplyMethod = effectModelType.GetMethod("Apply", GameReflection.PublicInstance, null,
							new[] { effectContextType, typeof(string), typeof(int) }, null);
					}
				}

			});
		}

		private static void EnsureBlightServiceTypes() {
			if (_blightServiceTypesCached) return;
			_blightServiceTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.BlightService", assembly => {
				// IGameServices.BlightService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsBlightServiceProperty = gameServicesType.GetProperty("BlightService", GameReflection.PublicInstance);
				}

				// IBlightService.CountGlobalFreeCysts
				var blightServiceType = assembly.GetType("Eremite.Services.IBlightService");
				if (blightServiceType != null) {
					_blightCountFreeCystsMethod = blightServiceType.GetMethod("CountGlobalFreeCysts", GameReflection.PublicInstance);
				}

			});
		}

		internal static void EnsureBlightConfigTypes() {
			if (_blightConfigTypesCached) return;
			_blightConfigTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.BlightConfig", assembly => {
				// Settings.blightConfig
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsBlightConfigField = settingsType.GetField("blightConfig", GameReflection.PublicInstance);
				}

				// BlightConfig.blightPostFuel
				var blightConfigType = assembly.GetType("Eremite.Model.Configs.BlightConfig");
				if (blightConfigType != null) {
					_blightConfigBlightPostFuelField = blightConfigType.GetField("blightPostFuel", GameReflection.PublicInstance);
				}

				// GoodRef.Name
				var goodRefType = assembly.GetType("Eremite.Model.GoodRef");
				if (goodRefType != null) {
					_goodRefNameProperty = goodRefType.GetProperty("Name", GameReflection.PublicInstance);
				}

			});
		}

		internal static void EnsureStorageService2Types() {
			if (_storageService2TypesCached) return;
			_storageService2TypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.StorageService2", assembly => {
				// IGameServices.StorageService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsStorageService2Property = gameServicesType.GetProperty("StorageService", GameReflection.PublicInstance);
				}

				// IStorageService.Main
				var storageServiceType = assembly.GetType("Eremite.Services.IStorageService");
				if (storageServiceType != null) {
					_storageServiceMainProperty = storageServiceType.GetProperty("Main", GameReflection.PublicInstance);
				}

				// Storage.GetAmount(string) - Main storage is of type Eremite.Buildings.Storage
				var storageType = assembly.GetType("Eremite.Buildings.Storage");
				if (storageType != null) {
					_mainStorageGetAmountMethod = storageType.GetMethod("GetAmount", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
				}

			});
		}

		// Internal accessors for HearthReflection (GoodRef.Name, EffectModel.DisplayName)
		internal static PropertyInfo GoodRefNameProperty { get { EnsureBlightConfigTypes(); return _goodRefNameProperty; } }
		internal static PropertyInfo EffectModelDisplayNameProperty { get { EnsureRaceBonusTypes(); return _effectModelDisplayNameProperty; } }

		// Internal accessor for HearthReflection (building finished check in hub-tier counting)
		internal static MethodInfo BuildingIsFinishedMethod { get { EnsureBuildingTypes(); return _buildingIsFinishedMethod; } }

		// Internal accessor for HearthReflection (main storage amount lookup)
		internal static int GetMainStorageAmountInternal(string goodName) {
			return GetMainStorageAmount(goodName);
		}

		private static void EnsureRainpunkServiceTypes() {
			if (_rainpunkServiceTypesCached) return;
			_rainpunkServiceTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RainpunkService", assembly => {
				// IGameServices.RainpunkService
				var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
				if (gameServicesType != null) {
					_gsRainpunkServiceProperty = gameServicesType.GetProperty("RainpunkService", GameReflection.PublicInstance);
				}

				// IRainpunkService.CountWaterLeft and CountTanksCapacity
				var rainpunkServiceType = assembly.GetType("Eremite.Services.IRainpunkService");
				var waterModelType = assembly.GetType("Eremite.Model.WaterModel");
				if (rainpunkServiceType != null && waterModelType != null) {
					_rainpunkCountWaterLeftMethod = rainpunkServiceType.GetMethod("CountWaterLeft", GameReflection.PublicInstance, null, new[] { waterModelType }, null);
					_rainpunkCountTanksCapacityMethod = rainpunkServiceType.GetMethod("CountTanksCapacity", GameReflection.PublicInstance, null, new[] { waterModelType }, null);
				}

				// IRainpunkService.GetWaterPerCysts and IsWaterSpawningBlight (takes Workshop)
				var workshopType = assembly.GetType("Eremite.Buildings.Workshop");
				if (rainpunkServiceType != null && workshopType != null) {
					_rainpunkGetWaterPerCystsMethod = rainpunkServiceType.GetMethod("GetWaterPerCysts", GameReflection.PublicInstance, null, new[] { workshopType }, null);
					_rainpunkIsWaterSpawningBlightMethod = rainpunkServiceType.GetMethod("IsWaterSpawningBlight", GameReflection.PublicInstance, null, new[] { workshopType }, null);
				}

			});
		}

		private static void EnsureRainpunkEngineTypes() {
			if (_rainpunkEngineTypesCached) return;
			_rainpunkEngineTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.RainpunkEngine", assembly => {
				// Workshop and WorkshopState types
				_workshopType = assembly.GetType("Eremite.Buildings.Workshop");
				_workshopStateType = assembly.GetType("Eremite.Buildings.WorkshopState");
				_rainpunkEngineStateType = assembly.GetType("Eremite.Buildings.RainpunkEngineState");
				_rainpunkEngineModelType = assembly.GetType("Eremite.Buildings.RainpunkEngineModel");
				_buildingRainpunkModelType = assembly.GetType("Eremite.Buildings.BuildingRainpunkModel");

				if (_workshopType != null) {
					_workshopStateField = _workshopType.GetField("state", GameReflection.PublicInstance);
					_workshopModelField = _workshopType.GetField("model", GameReflection.PublicInstance);
				}

				if (_workshopStateType != null) {
					_wsRainpunkUnlockedField = _workshopStateType.GetField("rainpunkUnlocked", GameReflection.PublicInstance);
					_wsEnginesField = _workshopStateType.GetField("engines", GameReflection.PublicInstance);
					_wsWaterUsedField = _workshopStateType.GetField("waterUsed", GameReflection.PublicInstance);
				}

				// WorkshopModel.rainpunk field
				var workshopModelType = assembly.GetType("Eremite.Buildings.WorkshopModel");
				if (workshopModelType != null) {
					_wmRainpunkField = workshopModelType.GetField("rainpunk", GameReflection.PublicInstance);
				}

				// BuildingRainpunkModel fields
				if (_buildingRainpunkModelType != null) {
					_brpEnginesField = _buildingRainpunkModelType.GetField("engines", GameReflection.PublicInstance);
					_brpWaterField = _buildingRainpunkModelType.GetField("water", GameReflection.PublicInstance);
				}

				// RainpunkEngineState fields
				if (_rainpunkEngineStateType != null) {
					_engineStateIndexField = _rainpunkEngineStateType.GetField("index", GameReflection.PublicInstance);
					_engineStateLevelField = _rainpunkEngineStateType.GetField("level", GameReflection.PublicInstance);
					_engineStateRequestedLevelField = _rainpunkEngineStateType.GetField("requestedLevel", GameReflection.PublicInstance);
				}

				// RainpunkEngineModel fields
				if (_rainpunkEngineModelType != null) {
					_engineModelMaxLevelField = _rainpunkEngineModelType.GetField("maxLevel", GameReflection.PublicInstance);
					_engineModelLevelsField = _rainpunkEngineModelType.GetField("levels", GameReflection.PublicInstance);
					_engineModelUpSoundField = _rainpunkEngineModelType.GetField("upSound", GameReflection.PublicInstance);
					_engineModelDownSoundField = _rainpunkEngineModelType.GetField("downSound", GameReflection.PublicInstance);
					_engineModelWaterPerSecField = _rainpunkEngineModelType.GetField("waterPerSec", GameReflection.PublicInstance);
				}

				// SoundRef type and GetNext method for playing engine sounds
				_soundRefType = assembly.GetType("Eremite.Model.Sound.SoundRef");
				if (_soundRefType != null) {
					_soundRefGetNextMethod = _soundRefType.GetMethod("GetNext", GameReflection.PublicInstance);
				}

				// RainpunkEngineLevel fields
				var engineLevelType = assembly.GetType("Eremite.Buildings.RainpunkEngineLevel");
				if (engineLevelType != null) {
					_engineLevelPerkField = engineLevelType.GetField("perk", GameReflection.PublicInstance);
				}

				// BuildingPerkModel.DisplayName property
				var buildingPerkModelType = assembly.GetType("Eremite.Model.BuildingPerkModel");
				if (buildingPerkModelType != null) {
					_buildingPerkDisplayNameProp = buildingPerkModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
				}

			});
		}

		private static void EnsureUpgradeTypes() {
			if (_upgradeTypesCached) return;
			_upgradeTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Upgrade", assembly => {
				// UpgradableBuilding type and properties
				_upgradableBuildingType = assembly.GetType("Eremite.Buildings.UpgradableBuilding");
				if (_upgradableBuildingType != null) {
					_upgradableModelProperty = _upgradableBuildingType.GetProperty("UpgradableModel", GameReflection.PublicInstance);
					_upgradableStateProperty = _upgradableBuildingType.GetProperty("UpgradableState", GameReflection.PublicInstance);
					_hasUpgradesProperty = _upgradableBuildingType.GetProperty("HasUpgrades", GameReflection.PublicInstance);
				}

				// UpgradableBuildingModel type
				_upgradableBuildingModelType = assembly.GetType("Eremite.Buildings.UpgradableBuildingModel");
				if (_upgradableBuildingModelType != null) {
					_upgradableModelLevelsField = _upgradableBuildingModelType.GetField("levels", GameReflection.PublicInstance);
				}

				// UpgradableBuildingState type
				_upgradableBuildingStateType = assembly.GetType("Eremite.Buildings.UpgradableBuildingState");
				if (_upgradableBuildingStateType != null) {
					_upgradableStateLevelField = _upgradableBuildingStateType.GetField("level", GameReflection.PublicInstance);
					_upgradableStateUpgradesField = _upgradableBuildingStateType.GetField("upgrades", GameReflection.PublicInstance);
				}

				// BuildingLevelModel type
				_buildingLevelModelType = assembly.GetType("Eremite.Buildings.BuildingLevelModel");
				if (_buildingLevelModelType != null) {
					_levelModelRequiredGoodsField = _buildingLevelModelType.GetField("requiredGoods", GameReflection.PublicInstance);
					_levelModelOptionsField = _buildingLevelModelType.GetField("options", GameReflection.PublicInstance);
				}

				// GoodsSet type
				_goodsSetType = assembly.GetType("Eremite.Model.GoodsSet");
				if (_goodsSetType != null) {
					_goodsSetGoodsField = _goodsSetType.GetField("goods", GameReflection.PublicInstance);
				}

				// BuildingPerkModel - DisplayName property, description field, and GetDescription method
				var buildingPerkModelType = assembly.GetType("Eremite.Model.BuildingPerkModel");
				if (buildingPerkModelType != null) {
					_buildingPerkDisplayNameProp = buildingPerkModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_buildingPerkDescField = buildingPerkModelType.GetField("description", BindingFlags.NonPublic | BindingFlags.Instance);
					_buildingPerkGetDescMethod = buildingPerkModelType.GetMethod("GetDescription", GameReflection.PublicInstance);
				}

			});
		}

		// ========================================
		// PUBLIC API - PANEL STATE
		// ========================================

		/// <summary>
		/// Get the currently displayed building from BuildingPanel.currentBuilding.
		/// </summary>
		public static object GetCurrentBuilding() {
			EnsurePanelTypes();

			if (_currentBuildingField == null) return null;

			try {
				return _currentBuildingField.GetValue(null);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if a building panel is currently open.
		/// </summary>
		public static bool IsBuildingPanelOpen() {
			return GetCurrentBuilding() != null;
		}

		// ========================================
		// PUBLIC API - BUILDING INFO
		// ========================================

		/// <summary>
		/// Get the internal model name (asset name) of a building.
		/// This is the non-localized identifier from BuildingModel.Name (e.g., "Archeology office").
		/// </summary>
		public static string GetBuildingModelName(object building) {
			if (building == null) return null;

			EnsureBuildingTypes();

			var model = ReflectionHelper.GetProp(_buildingModelProperty, building);
			if (model == null) return null;

			var nameProp = model.GetType().GetProperty("Name", GameReflection.PublicInstance);
			return nameProp?.GetValue(model) as string;
		}

		/// <summary>
		/// Get the display name of a building.
		/// Uses Building.DisplayName property which returns the localized name directly.
		/// </summary>
		public static string GetBuildingName(object building) {
			if (building == null) return null;

			EnsureBuildingTypes();

			try {
				return ReflectionHelper.GetPropString(_buildingDisplayNameProperty, building);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetBuildingName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the description of a building.
		/// </summary>
		public static string GetBuildingDescription(object building) {
			if (building == null) return null;

			EnsureBuildingTypes();
			EnsureModelTypes();

			try {
				var model = ReflectionHelper.GetProp(_buildingModelProperty, building);
				if (model == null) return null;

				return ReflectionHelper.GetPropString(_modelDescriptionProperty, model);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the building's ID.
		/// </summary>
		public static int GetBuildingId(object building) {
			if (building == null) return -1;

			EnsureBuildingTypes();

			try {
				return (int?)_buildingIdProperty?.GetValue(building) ?? -1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Get the building's type name for routing to appropriate navigator.
		/// </summary>
		public static string GetBuildingTypeName(object building) {
			if (building == null) return null;

			try {
				return building.GetType().Name;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Check if building construction is finished.
		/// </summary>
		public static bool IsBuildingFinished(object building) {
			if (building == null) return false;

			EnsureBuildingTypes();
			EnsureStateTypes();

			try {
				var state = ReflectionHelper.GetProp(_buildingStateProperty, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_stateFinishedField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if building is sleeping/paused.
		/// </summary>
		public static bool IsBuildingSleeping(object building) {
			if (building == null) return false;

			EnsureBuildingTypes();
			EnsureStateTypes();

			try {
				var state = ReflectionHelper.GetProp(_buildingStateProperty, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_stateIsSleepingField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if building supports being paused (sleep).
		/// Most finished production buildings with workers can sleep.
		/// Hearth, Storage, Port, Relic, Road cannot sleep when finished.
		/// </summary>
		public static bool CanBuildingSleep(object building) {
			if (building == null) return false;

			try {
				var canSleepMethod = building.GetType().GetMethod("CanSleep", GameReflection.PublicInstance);
				return (bool?)canSleepMethod?.Invoke(building, null) ?? false;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Pause (sleep) a building. Workers will be unassigned.
		/// </summary>
		public static bool SleepBuilding(object building) {
			if (building == null) return false;
			if (!CanBuildingSleep(building)) return false;
			if (IsBuildingSleeping(building)) return false;

			try {
				var sleepMethod = building.GetType().GetMethod("Sleep", GameReflection.PublicInstance);
				sleepMethod?.Invoke(building, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Resume (wake up) a paused building.
		/// </summary>
		public static bool WakeUpBuilding(object building) {
			if (building == null) return false;
			if (!IsBuildingSleeping(building)) return false;

			try {
				var wakeUpMethod = building.GetType().GetMethod("WakeUp", GameReflection.PublicInstance);
				wakeUpMethod?.Invoke(building, null);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Toggle building sleep state. Returns true if state changed.
		/// </summary>
		public static bool ToggleBuildingSleep(object building) {
			if (building == null) return false;

			if (IsBuildingSleeping(building)) {
				return WakeUpBuilding(building);
			} else {
				return SleepBuilding(building);
			}
		}

		/// <summary>
		/// Check if building is a production building (has workers/recipes).
		/// </summary>
		public static bool IsProductionBuilding(object building) {
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
		public static bool IsWorkshop(object building) {
			if (building == null) return false;

			EnsureWorkshopTypes();

			if (_workshopInterfaceType == null) return false;

			return _workshopInterfaceType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if building is a Camp (has recipes but not via IWorkshop).
		/// </summary>
		public static bool IsCamp(object building) {
			if (building == null) return false;

			EnsureCampTypes();

			if (_campType == null) return false;

			return _campType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if building is a GathererHut (Harvesters'/Trappers'/Foragers' Camp).
		/// </summary>
		public static bool IsGathererHut(object building) {
			if (building == null) return false;

			EnsureGathererHutTypes();

			if (_gathererHutType == null) return false;

			return _gathererHutType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if building is a Collector.
		/// </summary>
		public static bool IsCollector(object building) {
			if (building == null) return false;

			EnsureCollectorTypes();

			if (_collectorType == null) return false;

			return _collectorType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if building is a Mine.
		/// </summary>
		public static bool IsMine(object building) {
			if (building == null) return false;

			EnsureMineTypes();

			if (_mineType == null) return false;

			return _mineType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Get the current Camp mode (0-4 corresponding to CampMode enum).
		/// </summary>
		public static int GetCampMode(object building) {
			if (!IsCamp(building)) return 0;

			EnsureCampTypes();

			try {
				var state = ReflectionHelper.GetField(_campStateField, building);
				if (state == null) return 0;

				return ReflectionHelper.GetEnum(_campStateModeField, state);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Set the Camp mode.
		/// </summary>
		public static bool SetCampMode(object building, int mode) {
			if (!IsCamp(building)) return false;

			EnsureCampTypes();

			try {
				if (_campSetModeMethod == null) return false;

				// Convert int to CampMode enum
				var assembly = GameReflection.GameAssembly;
				var campModeType = assembly?.GetType("Eremite.Buildings.CampMode");
				if (campModeType == null) return false;

				var enumValue = Enum.ToObject(campModeType, mode);
				_campSetModeMethod.Invoke(building, new object[] { enumValue });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetCampMode failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get localized names for all Camp modes, resolved through the game's own loca table.
		/// </summary>
		public static string[] GetCampModeNames() {
			// CampMode enum order: None, OnlyMarked, NoGlades, OnlyMarkedGlades, NoGladesAndOnlyMarked
			var names = new string[5];
			for (int i = 0; i < names.Length; i++)
				names[i] = GameReflection.ResolveLocaKey($"MenuUI_Options_CampMode_{i}");
			return names;
		}

		/// <summary>
		/// Check if building is a Farm.
		/// </summary>
		public static bool IsFarm(object building) {
			if (building == null) return false;

			EnsureFarmTypes();

			if (_farmType == null) return false;

			return _farmType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Get count of sown fields in farm's range.
		/// </summary>
		public static int GetFarmSownFields(object building) {
			if (!IsFarm(building)) return 0;

			EnsureFarmTypes();

			try {
				return ReflectionHelper.InvokeInt(_farmCountSownFieldsMethod, building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get count of plowed fields in farm's range.
		/// </summary>
		public static int GetFarmPlowedFields(object building) {
			if (!IsFarm(building)) return 0;

			EnsureFarmTypes();

			try {
				return ReflectionHelper.InvokeInt(_farmCountPlowedFieldsMethod, building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get total count of available fields in farm's range (includes farmfields + empty grass).
		/// </summary>
		public static int GetFarmTotalFields(object building) {
			if (!IsFarm(building)) return 0;

			EnsureFarmTypes();

			try {
				return ReflectionHelper.InvokeInt(_farmCountAllFieldsMethod, building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get count of placed farmfields in a farm's range.
		/// Uses BuildingsService.Farmfields to count actual placed farmfield buildings.
		/// </summary>
		public static int GetFarmPlacedFieldsCount(object building) {
			if (!IsFarm(building)) return 0;

			try {
				var model = ConstructionReflection.GetBuildingModel(building);
				if (model == null) return 0;

				// Get farm's field position and size
				var fieldPos = ConstructionReflection.GetBuildingGridPosition(building);
				if (fieldPos == Vector2Int.zero) return 0;

				var buildingSize = ConstructionReflection.GetBuildingSize(model);

				// Get work area from model + meta bonus
				Vector2Int baseWorkArea = MapReflection.GetFarmModelWorkArea(model);
				int bonus = ConstructionReflection.GetBonusFarmArea();
				Vector2Int workArea = new Vector2Int(baseWorkArea.x + bonus, baseWorkArea.y + bonus);

				// Calculate bounds
				int minX = fieldPos.x - workArea.x;
				int maxX = fieldPos.x + buildingSize.x + workArea.x - 1;
				int minY = fieldPos.y - workArea.y;
				int maxY = fieldPos.y + buildingSize.y + workArea.y - 1;

				int mapWidth = GameReflection.GetMapWidth();
				int mapHeight = GameReflection.GetMapHeight();
				int count = 0;

				for (int x = minX; x <= maxX; x++) {
					for (int y = minY; y <= maxY; y++) {
						if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) continue;

						// Skip building footprint
						if (x >= fieldPos.x && x < fieldPos.x + buildingSize.x &&
							y >= fieldPos.y && y < fieldPos.y + buildingSize.y) continue;

						if (ConstructionReflection.HasFarmfieldAt(x, y))
							count++;
					}
				}

				return count;
			} catch {
				return 0;
			}
		}

		// ========================================
		// FARMFIELD METHODS
		// ========================================

		/// <summary>
		/// Check if building is a Farmfield (individual farm field tile).
		/// </summary>
		public static bool IsFarmfield(object building) {
			if (building == null) return false;

			EnsureFarmfieldTypes();

			if (_farmfieldType == null) return false;

			return _farmfieldType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if a Farmfield is plowed.
		/// </summary>
		public static bool IsFarmfieldPlowed(object building) {
			if (!IsFarmfield(building)) return false;

			EnsureFarmfieldTypes();

			try {
				var state = ReflectionHelper.GetField(_farmfieldStateField, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_farmfieldStatePlowedField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if a Farmfield is seeded (has a plant).
		/// </summary>
		public static bool IsFarmfieldSeeded(object building) {
			if (!IsFarmfield(building)) return false;

			EnsureFarmfieldTypes();

			try {
				var state = ReflectionHelper.GetField(_farmfieldStateField, building);
				if (state == null) return false;

				var plant = ReflectionHelper.GetField(_farmfieldStatePlantField, state);
				return plant != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the crop name for a seeded Farmfield.
		/// Uses the FarmRecipeModel's producedGood.DisplayName.
		/// </summary>
		public static string GetFarmfieldCropName(object building) {
			if (!IsFarmfield(building)) return null;

			EnsureFarmfieldTypes();

			try {
				var state = ReflectionHelper.GetField(_farmfieldStateField, building);
				if (state == null) return null;

				var plant = ReflectionHelper.GetField(_farmfieldStatePlantField, state);
				if (plant == null) return null;

				// Get the recipe name from plant state
				var recipeName = ReflectionHelper.GetString(_farmfieldPlantRecipeField, plant);
				if (string.IsNullOrEmpty(recipeName)) return null;

				// Look up the FarmRecipeModel via Settings.GetFarmRecipe(recipeName)
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var getFarmRecipeMethod = settings.GetType().GetMethod("GetFarmRecipe",
					new Type[] { typeof(string) });
				if (getFarmRecipeMethod == null) return null;

				var farmRecipeModel = getFarmRecipeMethod.Invoke(settings, new object[] { recipeName });
				if (farmRecipeModel == null) return null;

				// Get producedGood.good.displayName.Text
				var producedGoodField = farmRecipeModel.GetType().GetField("producedGood", GameReflection.PublicInstance);
				if (producedGoodField == null) return null;

				var producedGood = producedGoodField.GetValue(farmRecipeModel);
				if (producedGood == null) return null;

				var goodField = producedGood.GetType().GetField("good", GameReflection.PublicInstance);
				if (goodField == null) return null;

				var goodModel = goodField.GetValue(producedGood);
				if (goodModel == null) return null;

				// Try displayName field (LocaText)
				var displayNameField = goodModel.GetType().GetField("displayName", GameReflection.PublicInstance);
				if (displayNameField != null) {
					var displayName = displayNameField.GetValue(goodModel);
					if (displayName != null) {
						return GameReflection.GetLocaText(displayName);
					}
				}

				return null;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetFarmfieldCropName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the expected yield for a seeded Farmfield.
		/// Returns (goodDisplayName, amount) from plant.Result (plant.good * plant.multiplier).
		/// </summary>
		public static (string goodName, int amount)? GetFarmfieldExpectedYield(object building) {
			if (!IsFarmfield(building)) return null;

			EnsureFarmfieldTypes();

			try {
				var state = ReflectionHelper.GetField(_farmfieldStateField, building);
				if (state == null) return null;

				var plant = ReflectionHelper.GetField(_farmfieldStatePlantField, state);
				if (plant == null) return null;

				// Get the good struct and multiplier
				var goodObj = ReflectionHelper.GetField(_farmfieldPlantGoodField, plant);
				if (goodObj == null) return null;

				int multiplier = (int?)_farmfieldPlantMultiplierField?.GetValue(plant) ?? 1;

				// Good struct has 'name' (string) and 'amount' (int) fields
				var nameField = goodObj.GetType().GetField("name", GameReflection.PublicInstance);
				var amountField = goodObj.GetType().GetField("amount", GameReflection.PublicInstance);

				string goodName = nameField?.GetValue(goodObj) as string;
				int baseAmount = (int?)amountField?.GetValue(goodObj) ?? 0;

				if (string.IsNullOrEmpty(goodName)) return null;

				// Calculate final amount (Result = good * multiplier)
				int finalAmount = baseAmount * multiplier;

				// Get display name for the good
				string displayName = GetGoodDisplayName(goodName);
				if (string.IsNullOrEmpty(displayName))
					displayName = goodName;

				return (displayName, finalAmount);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetFarmfieldExpectedYield failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Check if building is a FishingHut.
		/// </summary>
		public static bool IsFishingHut(object building) {
			if (building == null) return false;

			EnsureFishingHutTypes();

			if (_fishingHutType == null) return false;

			return _fishingHutType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Get the current FishingHut bait mode (0-2 corresponding to FishmanBaitMode enum).
		/// 0 = None, 1 = Optional, 2 = OnlyWithBait
		/// </summary>
		public static int GetFishingBaitMode(object building) {
			if (!IsFishingHut(building)) return 0;

			EnsureFishingHutTypes();

			try {
				var state = ReflectionHelper.GetField(_fishingHutStateField, building);
				if (state == null) return 0;

				return ReflectionHelper.GetEnum(_fishingHutStateBaitModeField, state);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Set the FishingHut bait mode.
		/// </summary>
		public static bool SetFishingBaitMode(object building, int mode) {
			if (!IsFishingHut(building)) return false;

			EnsureFishingHutTypes();

			try {
				if (_fishingHutChangeModeMethod == null) return false;

				// Convert int to FishmanBaitMode enum
				var assembly = GameReflection.GameAssembly;
				var baitModeType = assembly?.GetType("Eremite.Buildings.FishmanBaitMode");
				if (baitModeType == null) return false;

				var enumValue = Enum.ToObject(baitModeType, mode);
				_fishingHutChangeModeMethod.Invoke(building, new object[] { enumValue });
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SetFishingBaitMode failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get localized names for all FishingHut bait modes, resolved through the game's own loca table.
		/// </summary>
		public static string[] GetFishingBaitModeNames() {
			// FishmanBaitMode enum order: None, Optional, OnlyWithBait
			return new string[]
			{
				GameReflection.ResolveLocaKey("GameUI_FishingHutPanel_BaitMode_None"),
				GameReflection.ResolveLocaKey("GameUI_FishingHutPanel_BaitMode_Optional"),
				GameReflection.ResolveLocaKey("GameUI_FishingHutPanel_BaitMode_OnlyWithBait")
			};
		}

		/// <summary>
		/// Get remaining bait charges for a FishingHut.
		/// </summary>
		public static int GetFishingBaitCharges(object building) {
			if (!IsFishingHut(building)) return 0;

			EnsureFishingHutTypes();

			try {
				var state = ReflectionHelper.GetField(_fishingHutStateField, building);
				if (state == null) return 0;

				var charges = ReflectionHelper.GetField(_fishingHutStateBaitChargesField, state);
				return (int?)charges ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the bait ingredient name for a FishingHut.
		/// </summary>
		public static string GetFishingBaitIngredient(object building) {
			if (!IsFishingHut(building)) return null;

			EnsureFishingHutTypes();

			try {
				var model = ReflectionHelper.GetField(_fishingHutModelField, building);
				if (model == null) return null;

				var baitIngredient = ReflectionHelper.GetField(_fishingHutModelBaitIngredientField, model);
				if (baitIngredient == null) return null;

				// baitIngredient is a GoodModel, get its Name property
				var nameProperty = baitIngredient.GetType().GetProperty("Name", GameReflection.PublicInstance);
				return nameProperty?.GetValue(baitIngredient) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get recipes for a FishingHut (returns List of RecipeState objects).
		/// </summary>
		public static List<object> GetFishingHutRecipes(object building) {
			var result = new List<object>();
			if (!IsFishingHut(building)) return result;

			EnsureFishingHutTypes();

			try {
				var state = ReflectionHelper.GetField(_fishingHutStateField, building);
				if (state == null) return result;

				var recipes = ReflectionHelper.GetList(_fishingHutStateRecipesField, state);
				if (recipes == null) return result;

				foreach (var recipe in recipes) {
					if (recipe != null)
						result.Add(recipe);
				}
			} catch {
				// Return empty list on error
			}

			return result;
		}

		/// <summary>
		/// Get the ponds (internally Lakes) in harvesting range of a FishingHut.
		/// Mirrors NearbyLakesPanel: every available lake the hut is in distance of,
		/// in the same order the panel lays its slots out.
		/// </summary>
		public static List<(string goodName, int chargesLeft, int maxCharges)> GetFishingHutNearbyLakes(object building) {
			var result = new List<(string, int, int)>();
			if (!IsFishingHut(building)) return result;

			EnsureNearbyLakeTypes();

			try {
				var lakesService = GameReflection.GetLakesService();
				if (lakesService == null || _lakesServiceInDistanceMethod == null) return result;

				var lakes = ReflectionHelper.GetProp(_lakesServiceLakesProperty, lakesService);
				var keys = ReflectionHelper.IterateKeys(lakes);
				if (keys == null) return result;

				foreach (var key in keys) {
					var lake = ReflectionHelper.DictGet(lakes, key);
					if (lake == null) continue;

					var state = ReflectionHelper.GetProp(_lakeStateProperty, lake);
					if (state == null) continue;

					if (!ReflectionHelper.GetBool(_lakeStateIsAvailableField, state)) continue;
					if (!ReflectionHelper.InvokeBool(_lakesServiceInDistanceMethod, lakesService, building, lake)) continue;

					result.Add((
						ReflectionHelper.InvokeString(_lakeGetProducedGoodMethod, lake),
						ReflectionHelper.GetInt(_lakeStateChargesLeftField, state),
						ReflectionHelper.GetInt(_lakeStateMaxChargesField, state)
					));
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetFishingHutNearbyLakes failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Toggle a recipe for a FishingHut.
		/// </summary>
		public static bool ToggleFishingHutRecipe(object building, object recipeState) {
			if (!IsFishingHut(building) || recipeState == null) return false;

			EnsureFishingHutTypes();

			try {
				ReflectionHelper.InvokeVoid(_fishingHutSwitchProductionOfMethod, building, recipeState);
				return true;
			} catch {
				return false;
			}
		}

		// ========================================
		// PUBLIC API - WORKERS
		// ========================================

		/// <summary>
		/// Get worker IDs for a production building.
		/// </summary>
		public static int[] GetWorkerIds(object building) {
			if (building == null || !IsProductionBuilding(building)) return new int[0];

			EnsureProductionTypes();

			try {
				return _workersProperty?.GetValue(building) as int[] ?? new int[0];
			} catch {
				return new int[0];
			}
		}

		/// <summary>
		/// Get worker count for a production building.
		/// </summary>
		public static int GetWorkerCount(object building) {
			var workerIds = GetWorkerIds(building);
			int count = 0;
			foreach (var id in workerIds) {
				if (id > 0) count++;
			}
			return count;
		}

		/// <summary>
		/// Get maximum worker slots for a production building.
		/// </summary>
		public static int GetMaxWorkers(object building) {
			return GetWorkerIds(building).Length;
		}

		/// <summary>
		/// Get an actor (villager) by ID.
		/// </summary>
		public static object GetActor(int actorId) {
			if (actorId <= 0) return null;

			EnsureActorTypes();

			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return null;

				var actorsService = ReflectionHelper.GetProp(_actorsServiceProperty, gameServices);
				if (actorsService == null) return null;

				return ReflectionHelper.Invoke(_getActorMethod, actorsService, actorId);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get actor's display name.
		/// The name is stored in VillagerState.name, accessed via Actor.ActorState.
		/// </summary>
		public static string GetActorName(object actor) {
			if (actor == null) return null;

			EnsureActorProperties();

			try {
				// Get the ActorState (which is actually VillagerState for villagers)
				var actorState = ReflectionHelper.GetProp(_actorStateProperty, actor);
				if (actorState == null) return null;

				// Get the name field from the state
				return ReflectionHelper.GetString(_villagerStateNameField, actorState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get actor's race name.
		/// The race is stored in VillagerState.race, accessed via Actor.ActorState.
		/// Returns strings like "Human", "Beaver", "Lizard", "Harpy", "Fox".
		/// </summary>
		public static string GetActorRace(object actor) {
			if (actor == null) return null;

			EnsureActorProperties();

			try {
				// Get the ActorState (which is actually VillagerState for villagers)
				var actorState = ReflectionHelper.GetProp(_actorStateProperty, actor);
				if (actorState == null) return null;

				// Get the race field from the state
				return ReflectionHelper.GetString(_villagerStateRaceField, actorState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get actor's current task description (e.g., "Traveling", "On break", "Working").
		/// </summary>
		public static string GetActorTaskDescription(object actor) {
			if (actor == null) return null;

			EnsureActorProperties();

			try {
				return ReflectionHelper.InvokeString(_getTaskDescriptionMethod, actor);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get a formatted description of a worker for announcement.
		/// For villagers: "Name, Race, Task". For automatons: "DisplayName automaton, Task".
		/// </summary>
		public static string GetWorkerDescription(int workerId) {
			if (workerId <= 0) return null;

			var actor = GetActor(workerId);
			if (actor == null) return null;

			// Automaton branch: display name + "automaton" suffix + task
			if (AutomatonReflection.IsAutomaton(actor)) {
				string displayName = AutomatonReflection.GetAutomatonDisplayName(actor);
				string label = displayName != null
					? Strings.Get("reflection.building.automaton_named", displayName)
					: Strings.Get("reflection.building.automaton");
				string automatonTask = GetActorTaskDescription(actor);
				return !string.IsNullOrEmpty(automatonTask) ? $"{label}, {automatonTask}" : label;
			}

			// Villager path: name + race + task
			string name = GetActorName(actor) ?? Strings.Get("common.unknown");
			string race = GetActorRace(actor);
			string task = GetActorTaskDescription(actor);

			var parts = new List<string> { name };
			if (!string.IsNullOrEmpty(race))
				parts.Add(EmbarkReflection.GetRaceDisplayName(race));
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
		private static object GetVillagersService() {
			EnsureVillagersServiceTypes();
			return GameReflection.GetService(_villagersServiceProperty);
		}

		/// <summary>
		/// Get list of race names that have free workers available.
		/// Returns tuples of (raceName, freeCount).
		/// Only includes races that are actually present in the settlement (population > 0).
		/// </summary>
		/// <param name="includeZeroFree">If true, include races with 0 free workers.</param>
		public static List<(string raceName, int freeCount)> GetRacesWithFreeWorkers(bool includeZeroFree = false) {
			var result = new List<(string, int)>();

			EnsureVillagersServiceTypes();

			try {
				var villagersService = GetVillagersService();
				if (villagersService == null) return result;

				// Get the Races dictionary: Dictionary<string, List<Villager>>
				var racesDict = ReflectionHelper.GetProp(_villagersServiceRacesProperty, villagersService);
				if (racesDict == null) return result;

				// Iterate through races
				var keys = ReflectionHelper.IterateKeys(racesDict);
				if (keys == null) return result;

				foreach (var raceKey in keys) {
					string raceName = raceKey as string;
					if (string.IsNullOrEmpty(raceName)) continue;

					// Check if race has any villagers (is actually present in settlement)
					var villagerList = ReflectionHelper.DictGet(racesDict, raceKey);
					if (villagerList != null) {
						var countProp = villagerList.GetType().GetProperty("Count");
						int population = (int)(countProp?.GetValue(villagerList) ?? 0);
						if (population == 0) continue;  // Skip races with no villagers
					}

					int freeCount = GetFreeWorkerCount(raceName);
					if (includeZeroFree || freeCount > 0) {
						result.Add((raceName, freeCount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRacesWithFreeWorkers failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get count of free workers for a specific race.
		/// </summary>
		public static int GetFreeWorkerCount(string raceName) {
			if (string.IsNullOrEmpty(raceName)) return 0;

			EnsureVillagersServiceTypes();

			try {
				var villagersService = GetVillagersService();
				if (villagersService == null) return 0;

				var result = ReflectionHelper.Invoke(_getDefaultProfessionAmountMethod, villagersService, raceName);
				return (int?)result ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Find a RaceModel by name from RacesService.
		/// Returns null if not found.
		/// </summary>
		private static object FindRaceModel(string raceName) {
			if (string.IsNullOrEmpty(raceName)) return null;

			var racesService = GameReflection.GetRacesService();
			if (racesService == null) return null;

			var races = _racesServiceRacesProperty?.GetValue(racesService) as System.Array;
			if (races == null) return null;

			foreach (var race in races) {
				if (race == null) continue;
				var nameProperty = race.GetType().GetProperty("Name", GameReflection.PublicInstance);
				string name = nameProperty?.GetValue(race) as string;
				if (name == raceName) {
					return race;
				}
			}

			return null;
		}

		/// <summary>
		/// Find a matching characteristic between a race and building.
		/// Returns (characteristic, matchingTag) or (null, null) if no match.
		/// </summary>
		private static (object characteristic, object tag) FindMatchingCharacteristic(object building, object raceModel) {
			if (building == null || raceModel == null) return (null, null);

			var buildingModel = ReflectionHelper.GetProp(_buildingModelProperty, building);
			if (buildingModel == null) return (null, null);

			var tags = _buildingModelTagsField?.GetValue(buildingModel) as System.Array;
			if (tags == null || tags.Length == 0) return (null, null);

			var characteristics = _raceModelCharacteristicsField?.GetValue(raceModel) as System.Array;
			if (characteristics == null || characteristics.Length == 0) return (null, null);

			foreach (var buildingTag in tags) {
				if (buildingTag == null) continue;

				foreach (var characteristic in characteristics) {
					if (characteristic == null) continue;

					var characteristicTag = ReflectionHelper.GetField(_raceCharacteristicTagField, characteristic);
					if (characteristicTag != null && characteristicTag == buildingTag) {
						return (characteristic, buildingTag);
					}
				}
			}

			return (null, null);
		}

		/// <summary>
		/// Determine the bonus type (Efficiency or Comfort) from a characteristic's effect.
		/// </summary>
		private static string GetBonusTypeFromCharacteristic(object characteristic) {
			var effect = ReflectionHelper.GetField(_raceCharacteristicEffectField, characteristic);
			if (effect != null) {
				string typeName = effect.GetType().Name;
				return Strings.Get(typeName.Contains("Resolve") ? "reflection.building.bonus_comfort" : "reflection.building.bonus_efficiency");
			}

			var buildingPerk = ReflectionHelper.GetField(_raceCharacteristicBuildingPerkField, characteristic);
			if (buildingPerk != null) {
				string typeName = buildingPerk.GetType().Name;
				return Strings.Get(typeName.Contains("Resolve") ? "reflection.building.bonus_comfort" : "reflection.building.bonus_efficiency");
			}

			var globalEffect = ReflectionHelper.GetField(_raceCharacteristicGlobalEffectField, characteristic);
			if (globalEffect != null) {
				string typeName = globalEffect.GetType().Name;
				return Strings.Get(typeName.Contains("Resolve") ? "reflection.building.bonus_comfort" : "reflection.building.bonus_efficiency");
			}

			return null;
		}

		/// <summary>
		/// Get both the racial bonus name and type for a race at a building in one call.
		/// More efficient than calling GetRaceBonusForBuilding and GetRaceBonusTypeForBuilding separately.
		/// Returns (bonus name, bonus type) or (null, null) if no bonus applies.
		/// </summary>
		public static (string bonus, string bonusType) GetRaceBonusWithType(object building, string raceName) {
			if (building == null || string.IsNullOrEmpty(raceName)) return (null, null);

			EnsureRaceBonusTypes();
			EnsureBuildingTypes();

			try {
				var raceModel = FindRaceModel(raceName);
				if (raceModel == null) return (null, null);

				var (characteristic, buildingTag) = FindMatchingCharacteristic(building, raceModel);
				if (characteristic == null) return (null, null);

				// Get the bonus name
				string bonus = GetBonusNameFromCharacteristic(characteristic, buildingTag);

				// Get the bonus type
				string bonusType = GetBonusTypeFromCharacteristic(characteristic);

				return (bonus, bonusType);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRaceBonusWithType failed: {ex.Message}");
				return (null, null);
			}
		}

		/// <summary>
		/// Extract the bonus display name from a characteristic and its tag.
		/// </summary>
		private static string GetBonusNameFromCharacteristic(object characteristic, object buildingTag) {
			// Try tag's display name first
			var displayNameLoca = ReflectionHelper.GetField(_buildingTagDisplayNameField, buildingTag);
			if (displayNameLoca != null) {
				string displayName = GameReflection.GetLocaText(displayNameLoca);
				if (!string.IsNullOrEmpty(displayName) && !displayName.Contains("Missing key")) {
					return displayName;
				}
			}

			// Try effect's displayName (VillagerPerkModel)
			var effect = ReflectionHelper.GetField(_raceCharacteristicEffectField, characteristic);
			if (effect != null) {
				var effectDisplayNameLoca = ReflectionHelper.GetField(_villagerPerkDisplayNameField, effect);
				if (effectDisplayNameLoca != null) {
					string effectDisplayName = GameReflection.GetLocaText(effectDisplayNameLoca);
					if (!string.IsNullOrEmpty(effectDisplayName) && !effectDisplayName.Contains("Missing key")) {
						var descProp = effect.GetType().GetProperty("Description", GameReflection.PublicInstance);
						string desc = descProp?.GetValue(effect) as string;
						if (!string.IsNullOrEmpty(desc) && !desc.Contains("Missing key")) {
							return $"{effectDisplayName}, {desc}";
						}
						return effectDisplayName;
					}
				}
			}

			// Try buildingPerk's DisplayName (BuildingPerkModel)
			var buildingPerk = ReflectionHelper.GetField(_raceCharacteristicBuildingPerkField, characteristic);
			if (buildingPerk != null) {
				string perkDisplayName = ReflectionHelper.GetPropString(_buildingPerkDisplayNameProperty, buildingPerk);
				if (!string.IsNullOrEmpty(perkDisplayName) && !perkDisplayName.Contains("Missing key")) {
					var getDescMethod = buildingPerk.GetType().GetMethod("GetDescription", new[] { typeof(object).Assembly.GetType("Eremite.Buildings.Building") ?? typeof(object) });
					string desc = null;
					try {
						desc = getDescMethod?.Invoke(buildingPerk, new object[] { null }) as string;
					} catch {
						// Ignore description fetch errors
					}
					if (!string.IsNullOrEmpty(desc) && !desc.Contains("Missing key")) {
						return $"{perkDisplayName}, {desc}";
					}
					return perkDisplayName;
				}
			}

			// Try globalEffect's DisplayName (EffectModel)
			var globalEffect = ReflectionHelper.GetField(_raceCharacteristicGlobalEffectField, characteristic);
			if (globalEffect != null) {
				string globalDisplayName = ReflectionHelper.GetPropString(_effectModelDisplayNameProperty, globalEffect);
				if (!string.IsNullOrEmpty(globalDisplayName) && !globalDisplayName.Contains("Missing key")) {
					var descProp = globalEffect.GetType().GetProperty("Description", GameReflection.PublicInstance);
					string desc = descProp?.GetValue(globalEffect) as string;
					if (!string.IsNullOrEmpty(desc) && !desc.Contains("Missing key")) {
						return $"{globalDisplayName}, {desc}";
					}
					return globalDisplayName;
				}
			}

			return null;
		}

		/// <summary>
		/// Get the racial bonus tag name for a race at a specific building, if any.
		/// Returns the tag's display name (e.g., "Woodcutters", "Farmers") if the race has a matching bonus,
		/// or null if no bonus applies to this building.
		/// </summary>
		public static string GetRaceBonusForBuilding(object building, string raceName) {
			var (bonus, _) = GetRaceBonusWithType(building, raceName);
			return bonus;
		}

		/// <summary>
		/// Get the bonus type (Efficiency or Comfort) for a race working at a building.
		/// Prefer GetRaceBonusWithType if you need both bonus name and type.
		/// </summary>
		public static string GetRaceBonusTypeForBuilding(object building, string raceName) {
			var (_, bonusType) = GetRaceBonusWithType(building, raceName);
			return bonusType;
		}

		/// <summary>
		/// Get the firekeeper (passive) effect description for a race.
		/// This is the bonus that applies when the race is assigned to a Hearth.
		/// </summary>
		public static string GetRaceFirekeeperEffect(string raceName) {
			if (string.IsNullOrEmpty(raceName)) return null;

			EnsureRaceBonusTypes();

			try {
				var raceModel = FindRaceModel(raceName);
				if (raceModel == null) return null;

				return ReflectionHelper.GetLocaString(_raceModelPassiveEffectDescField, raceModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRaceFirekeeperEffect failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Assign a free worker of the specified race to a building slot.
		/// </summary>
		public static bool AssignWorkerToSlot(object building, int slotIndex, string raceName) {
			if (building == null || string.IsNullOrEmpty(raceName)) return false;
			if (!IsProductionBuilding(building)) return false;

			EnsureVillagersServiceTypes();
			EnsureProfessionTypes();

			try {
				var villagersService = GetVillagersService();
				if (villagersService == null) return false;

				// Get a free villager of this race
				var villager = ReflectionHelper.Invoke(_getDefaultProfessionVillagerMethod, villagersService, raceName, building);
				if (villager == null) {
					Debug.Log($"[ATSAccessibility] AssignWorkerToSlot: No free villager of race {raceName}");
					return false;
				}

				// Get the building's profession
				string profession = ReflectionHelper.GetPropString(_professionProperty, building);
				if (string.IsNullOrEmpty(profession)) {
					Debug.LogError("[ATSAccessibility] AssignWorkerToSlot: Could not get building profession");
					return false;
				}

				// Assign the villager
				_setProfessionMethod?.Invoke(villagersService, new object[] { villager, profession, building, slotIndex, true });
				Debug.Log($"[ATSAccessibility] AssignWorkerToSlot: Assigned {raceName} to slot {slotIndex}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] AssignWorkerToSlot failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Unassign a worker from a building slot.
		/// </summary>
		public static bool UnassignWorkerFromSlot(object building, int slotIndex) {
			if (building == null) return false;
			if (!IsProductionBuilding(building)) return false;

			EnsureVillagersServiceTypes();

			try {
				var workerIds = GetWorkerIds(building);
				if (slotIndex < 0 || slotIndex >= workerIds.Length) return false;

				int workerId = workerIds[slotIndex];
				if (workerId <= 0) {
					Debug.Log("[ATSAccessibility] UnassignWorkerFromSlot: Slot is already empty");
					return false;
				}

				// Automatons cannot be unassigned via VillagersService
				var slotActor = GetActor(workerId);
				if (slotActor != null && AutomatonReflection.IsAutomaton(slotActor)) {
					Debug.Log("[ATSAccessibility] UnassignWorkerFromSlot: Skipping automaton-occupied slot");
					return false;
				}

				var villagersService = GetVillagersService();
				if (villagersService == null) return false;

				// Get the villager
				var villager = ReflectionHelper.Invoke(_getVillagerMethod, villagersService, workerId);
				if (villager == null) {
					Debug.LogError("[ATSAccessibility] UnassignWorkerFromSlot: Could not get villager");
					return false;
				}

				// Release from profession
				ReflectionHelper.InvokeVoid(_releaseFromProfessionMethod, villagersService, villager, true);
				Debug.Log($"[ATSAccessibility] UnassignWorkerFromSlot: Unassigned worker from slot {slotIndex}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UnassignWorkerFromSlot failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if a worker slot is empty.
		/// </summary>
		public static bool IsWorkerSlotEmpty(object building, int slotIndex) {
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
		public static List<object> GetRecipes(object building) {
			var result = new List<object>();

			if (building == null)
				return result;

			// Try IWorkshop first
			if (IsWorkshop(building)) {
				EnsureWorkshopTypes();

				try {
					var recipes = ReflectionHelper.GetProp(_workshopRecipesProperty, building);
					if (recipes != null) {
						var enumerable = recipes as System.Collections.IEnumerable;
						if (enumerable != null) {
							foreach (var recipe in enumerable) {
								if (recipe != null)
									result.Add(recipe);
							}
						}
					}
					Debug.Log($"[ATSAccessibility] GetRecipes (IWorkshop): Found {result.Count} recipes");
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] GetRecipes (IWorkshop) failed: {ex.Message}");
				}

				return result;
			}

			// Try Camp
			if (IsCamp(building)) {
				EnsureCampTypes();

				try {
					var campState = ReflectionHelper.GetField(_campStateField, building);
					if (campState != null) {
						var recipes = ReflectionHelper.GetField(_campStateRecipesField, campState);
						if (recipes != null) {
							var enumerable = recipes as System.Collections.IEnumerable;
							if (enumerable != null) {
								foreach (var recipe in enumerable) {
									if (recipe != null)
										result.Add(recipe);
								}
							}
						}
					}
					Debug.Log($"[ATSAccessibility] GetRecipes (Camp): Found {result.Count} recipes");
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] GetRecipes (Camp) failed: {ex.Message}");
				}

				return result;
			}

			// Try Farm (also stores recipes in state.recipes like Camp)
			if (IsFarm(building)) {
				EnsureFarmTypes();

				try {
					var farmState = ReflectionHelper.GetField(_farmStateField, building);
					if (farmState != null) {
						// Farm uses state.recipes (List<RecipeState>)
						var recipesField = farmState.GetType().GetField("recipes", GameReflection.PublicInstance);
						var recipes = recipesField?.GetValue(farmState);
						if (recipes != null) {
							var enumerable = recipes as System.Collections.IEnumerable;
							if (enumerable != null) {
								foreach (var recipe in enumerable) {
									if (recipe != null)
										result.Add(recipe);
								}
							}
						}
					}
					Debug.Log($"[ATSAccessibility] GetRecipes (Farm): Found {result.Count} recipes");
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] GetRecipes (Farm) failed: {ex.Message}");
				}

				return result;
			}

			return result;
		}

		/// <summary>
		/// Toggle a recipe's active state.
		/// Handles both IWorkshop and Camp buildings.
		/// </summary>
		public static bool ToggleRecipe(object building, object recipeState) {
			if (building == null || recipeState == null)
				return false;

			// Try IWorkshop first
			if (IsWorkshop(building)) {
				EnsureWorkshopTypes();

				try {
					ReflectionHelper.InvokeVoid(_switchProductionOfMethod, building, recipeState);
					return true;
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] ToggleRecipe (IWorkshop) failed: {ex.Message}");
					return false;
				}
			}

			// Try Camp
			if (IsCamp(building)) {
				EnsureCampTypes();

				try {
					ReflectionHelper.InvokeVoid(_campSwitchProductionOfMethod, building, recipeState);
					return true;
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] ToggleRecipe (Camp) failed: {ex.Message}");
					return false;
				}
			}

			// Try Farm
			if (IsFarm(building)) {
				EnsureFarmTypes();

				try {
					ReflectionHelper.InvokeVoid(_farmSwitchProductionOfMethod, building, recipeState);
					return true;
				} catch (Exception ex) {
					Debug.LogError($"[ATSAccessibility] ToggleRecipe (Farm) failed: {ex.Message}");
					return false;
				}
			}

			return false;
		}

		/// <summary>
		/// Check if a recipe is active (enabled).
		/// </summary>
		public static bool IsRecipeActive(object recipeState) {
			if (recipeState == null) return false;

			EnsureRecipeTypes();

			try {
				return ReflectionHelper.GetBool(_recipeActiveField, recipeState);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get recipe production limit (-1 = unlimited).
		/// </summary>
		public static int GetRecipeLimit(object recipeState) {
			if (recipeState == null) return -1;

			EnsureRecipeTypes();

			try {
				return (int?)_recipeLimitField?.GetValue(recipeState) ?? -1;
			} catch {
				return -1;
			}
		}

		/// <summary>
		/// Check if a recipe's limit is local (true) or follows the global limit (false).
		/// </summary>
		public static bool IsRecipeLimitLocal(object recipeState) {
			if (recipeState == null) return true;

			EnsureRecipeTypes();

			try {
				return (bool?)_isLimitLocalField?.GetValue(recipeState) ?? true;
			} catch {
				return true;
			}
		}

		/// <summary>
		/// Get recipe model name (used to look up display name).
		/// </summary>
		public static string GetRecipeModelName(object recipeState) {
			if (recipeState == null) return null;

			EnsureRecipeTypes();

			try {
				return ReflectionHelper.GetString(_recipeModelField, recipeState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get recipe product name (the good being produced).
		/// </summary>
		public static string GetRecipeProductName(object recipeState) {
			if (recipeState == null) return null;

			EnsureRecipeTypes();

			try {
				return ReflectionHelper.GetString(_recipeProductNameField, recipeState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the recipe model object for a recipe state.
		/// </summary>
		public static object GetRecipeModel(object recipeState) {
			if (recipeState == null) return null;

			EnsureRecipeTypes();
			EnsureRecipeModelTypes();

			try {
				string modelName = ReflectionHelper.GetString(_recipeModelField, recipeState);
				if (string.IsNullOrEmpty(modelName)) return null;

				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				return ReflectionHelper.Invoke(_settingsGetRecipeMethod, settings, modelName);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get recipe grade/star level (0-3 typically).
		/// </summary>
		public static int GetRecipeGrade(object recipeState) {
			var model = GetRecipeModel(recipeState);
			if (model == null) return 0;

			EnsureRecipeModelTypes();

			try {
				var grade = ReflectionHelper.GetField(_recipeModelGradeField, model);
				if (grade == null) return 0;

				return ReflectionHelper.GetInt(_gradeModelLevelField, grade);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get recipe production time in seconds.
		/// </summary>
		public static float GetRecipeProductionTime(object recipeState) {
			var model = GetRecipeModel(recipeState);
			if (model == null) return 0f;

			EnsureRecipeModelTypes();

			try {
				return (float?)_recipeModelProductionTimeField?.GetValue(model) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get recipe produced amount (how many items produced per cycle).
		/// </summary>
		public static int GetRecipeProducedAmount(object recipeState) {
			var model = GetRecipeModel(recipeState);
			if (model == null) return 1;

			EnsureRecipeModelTypes();

			try {
				var producedGood = ReflectionHelper.GetField(_recipeModelProducedGoodField, model);
				if (producedGood == null) return 1;

				return (int?)GameReflection.GoodRefAmountField?.GetValue(producedGood) ?? 1;
			} catch {
				return 1;
			}
		}

		/// <summary>
		/// Get recipe produced good display name.
		/// </summary>
		public static string GetRecipeProducedGoodDisplayName(object recipeState) {
			var model = GetRecipeModel(recipeState);
			if (model == null) return null;

			EnsureRecipeModelTypes();

			try {
				var producedGood = ReflectionHelper.GetField(_recipeModelProducedGoodField, model);
				if (producedGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGood);
				if (goodModel == null) return null;

				var displayNameLoca = ReflectionHelper.GetField(_recipeGoodModelDisplayNameField, goodModel);
				return GameReflection.GetLocaText(displayNameLoca);
			} catch {
				return null;
			}
		}

		// ========================================
		// FARM RECIPE METHODS
		// ========================================

		/// <summary>
		/// Get the FarmRecipeModel for a farm RecipeState.
		/// Uses Settings.GetFarmRecipe(recipeState.model).
		/// </summary>
		public static object GetFarmRecipeModel(object recipeState) {
			if (recipeState == null) return null;

			try {
				// Get recipeState.model (string name)
				var modelField = recipeState.GetType().GetField("model", GameReflection.PublicInstance);
				if (modelField == null) return null;

				var modelName = modelField.GetValue(recipeState) as string;
				if (string.IsNullOrEmpty(modelName)) return null;

				// Get Settings.GetFarmRecipe(name)
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var getFarmRecipeMethod = settings.GetType().GetMethod("GetFarmRecipe",
					new System.Type[] { typeof(string) });
				if (getFarmRecipeMethod == null) return null;

				return getFarmRecipeMethod.Invoke(settings, new object[] { modelName });
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the display name for a farm recipe's produced good.
		/// </summary>
		public static string GetFarmRecipeProductDisplayName(object recipeState) {
			var farmRecipeModel = GetFarmRecipeModel(recipeState);
			if (farmRecipeModel == null) return null;

			try {
				// FarmRecipeModel.producedGood is a GoodRef
				var producedGoodField = farmRecipeModel.GetType().GetField("producedGood", GameReflection.PublicInstance);
				if (producedGoodField == null) return null;

				var producedGood = producedGoodField.GetValue(farmRecipeModel);
				if (producedGood == null) return null;

				// GoodRef.DisplayName property
				var displayNameProp = producedGood.GetType().GetProperty("DisplayName", GameReflection.PublicInstance);
				if (displayNameProp != null) {
					return displayNameProp.GetValue(producedGood) as string;
				}

				return null;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the amount produced by a farm recipe.
		/// </summary>
		public static int GetFarmRecipeProductAmount(object recipeState) {
			var farmRecipeModel = GetFarmRecipeModel(recipeState);
			if (farmRecipeModel == null) return 0;

			try {
				// FarmRecipeModel.producedGood is a GoodRef
				var producedGoodField = farmRecipeModel.GetType().GetField("producedGood", GameReflection.PublicInstance);
				if (producedGoodField == null) return 0;

				var producedGood = producedGoodField.GetValue(farmRecipeModel);
				if (producedGood == null) return 0;

				// GoodRef.amount field
				var amountField = producedGood.GetType().GetField("amount", GameReflection.PublicInstance);
				if (amountField != null) {
					return (int?)amountField.GetValue(producedGood) ?? 0;
				}

				return 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the star/grade level for a farm recipe.
		/// </summary>
		public static int GetFarmRecipeGradeLevel(object recipeState) {
			var farmRecipeModel = GetFarmRecipeModel(recipeState);
			if (farmRecipeModel == null) return 0;

			try {
				// FarmRecipeModel inherits from RecipeModel which has grade field
				var gradeField = farmRecipeModel.GetType().GetField("grade", GameReflection.PublicInstance);
				if (gradeField == null) return 0;

				var grade = gradeField.GetValue(farmRecipeModel);
				if (grade == null) return 0;

				// RecipeGradeModel.level field
				var levelField = grade.GetType().GetField("level", GameReflection.PublicInstance);
				if (levelField != null) {
					return (int?)levelField.GetValue(grade) ?? 0;
				}

				return 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get base planting time from a FarmRecipeModel (in seconds).
		/// </summary>
		public static float GetFarmRecipePlantingTime(object recipeState) {
			var farmRecipeModel = GetFarmRecipeModel(recipeState);
			if (farmRecipeModel == null) return 0f;

			try {
				var plantingTimeField = farmRecipeModel.GetType().GetField("plantingTime", GameReflection.PublicInstance);
				if (plantingTimeField == null) return 0f;

				return (float?)plantingTimeField.GetValue(farmRecipeModel) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get base harvest time from a FarmRecipeModel (in seconds).
		/// </summary>
		public static float GetFarmRecipeHarvestTime(object recipeState) {
			var farmRecipeModel = GetFarmRecipeModel(recipeState);
			if (farmRecipeModel == null) return 0f;

			try {
				var harvestTimeField = farmRecipeModel.GetType().GetField("harvestTime", GameReflection.PublicInstance);
				if (harvestTimeField == null) return 0f;

				return (float?)harvestTimeField.GetValue(farmRecipeModel) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get the farm's current planting rate multiplier.
		/// </summary>
		public static float GetFarmPlantingRate(object farm) {
			if (!IsFarm(farm)) return 1f;

			try {
				var method = farm.GetType().GetMethod("GetPlantingRate", GameReflection.PublicInstance);
				if (method == null) return 1f;

				return (float?)method.Invoke(farm, null) ?? 1f;
			} catch {
				return 1f;
			}
		}

		/// <summary>
		/// Get the farm's current harvesting rate multiplier.
		/// </summary>
		public static float GetFarmHarvestingRate(object farm) {
			if (!IsFarm(farm)) return 1f;

			try {
				var method = farm.GetType().GetMethod("GetHarvestingRate", GameReflection.PublicInstance);
				if (method == null) return 1f;

				return (float?)method.Invoke(farm, null) ?? 1f;
			} catch {
				return 1f;
			}
		}

		/// <summary>
		/// Get the ingredients array for a WorkshopRecipeState.
		/// Returns a 2D array: [slot][options], each option is an IngredientState.
		/// </summary>
		public static object GetRecipeIngredients(object recipeState) {
			if (recipeState == null) return null;

			EnsureRecipeTypes();

			try {
				return _recipeIngredientsField?.GetValue(recipeState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the number of ingredient slots for a recipe.
		/// </summary>
		public static int GetRecipeIngredientSlotCount(object recipeState) {
			var ingredients = GetRecipeIngredients(recipeState) as System.Array;
			return ingredients?.Length ?? 0;
		}

		/// <summary>
		/// Get ingredient options for a specific slot.
		/// Returns array of IngredientState objects.
		/// </summary>
		public static object[] GetIngredientSlotOptions(object recipeState, int slotIndex) {
			var ingredients = GetRecipeIngredients(recipeState) as System.Array;
			if (ingredients == null || slotIndex < 0 || slotIndex >= ingredients.Length)
				return new object[0];

			try {
				var slot = ingredients.GetValue(slotIndex) as System.Array;
				if (slot == null) return new object[0];

				var result = new object[slot.Length];
				for (int i = 0; i < slot.Length; i++) {
					result[i] = slot.GetValue(i);
				}
				return result;
			} catch {
				return new object[0];
			}
		}

		/// <summary>
		/// Get the good name from an IngredientState.
		/// </summary>
		public static string GetIngredientGoodName(object ingredientState) {
			if (ingredientState == null) return null;

			EnsureIngredientTypes();

			try {
				var good = ReflectionHelper.GetField(_ingredientGoodField, ingredientState);
				if (good == null) return null;

				// Good is a struct with a 'name' field
				var nameField = good.GetType().GetField("name", GameReflection.PublicInstance);
				return nameField?.GetValue(good) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the amount from an IngredientState (how many used per production cycle).
		/// </summary>
		public static int GetIngredientAmount(object ingredientState) {
			if (ingredientState == null) return 1;

			EnsureIngredientTypes();

			try {
				var good = ReflectionHelper.GetField(_ingredientGoodField, ingredientState);
				if (good == null) return 1;

				return (int?)_goodAmountField?.GetValue(good) ?? 1;
			} catch {
				return 1;
			}
		}

		/// <summary>
		/// Check if an ingredient option is allowed/enabled.
		/// </summary>
		public static bool IsIngredientAllowed(object ingredientState) {
			if (ingredientState == null) return false;

			EnsureIngredientTypes();

			try {
				return ReflectionHelper.GetBool(_ingredientAllowedField, ingredientState);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Toggle an ingredient's allowed state.
		/// </summary>
		public static void ToggleIngredientAllowed(object ingredientState) {
			if (ingredientState == null) return;

			EnsureIngredientTypes();

			try {
				bool current = ReflectionHelper.GetBool(_ingredientAllowedField, ingredientState);
				_ingredientAllowedField?.SetValue(ingredientState, !current);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] ToggleIngredientAllowed failed: {ex.Message}"); }
		}

		/// <summary>
		/// Get an ingredient's priority (0-3). Returns 0 if not available.
		/// </summary>
		public static int GetIngredientPriority(object ingredientState) {
			if (ingredientState == null) return 0;

			EnsureIngredientTypes();
			return ReflectionHelper.GetInt(_ingredientPriorityField, ingredientState);
		}

		/// <summary>
		/// Set an ingredient's priority (clamped to 0-3). Also force-enables the ingredient.
		/// </summary>
		public static void SetIngredientPriority(object ingredientState, int priority) {
			if (ingredientState == null) return;

			EnsureIngredientTypes();

			try {
				int clamped = System.Math.Max(0, System.Math.Min(3, priority));
				_ingredientPriorityField?.SetValue(ingredientState, clamped);
				// Setting priority also enables the ingredient (matches game behavior)
				_ingredientAllowedField?.SetValue(ingredientState, true);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetIngredientPriority failed: {ex.Message}"); }
		}

		/// <summary>
		/// Set recipe production limit.
		/// </summary>
		public static void SetRecipeLimit(object recipeState, int limit) {
			if (recipeState == null) return;

			EnsureRecipeTypes();

			try {
				_recipeLimitField?.SetValue(recipeState, limit);
				_isLimitLocalField?.SetValue(recipeState, true);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetRecipeLimit failed: {ex.Message}"); }
		}

		/// <summary>
		/// Set recipe limit as a global limit (isLimitLocal = false).
		/// Used when pushing a global limit change to individual recipe states.
		/// </summary>
		public static void SetRecipeLimitFromGlobal(object recipeState, int limit) {
			if (recipeState == null) return;

			EnsureRecipeTypes();

			try {
				_recipeLimitField?.SetValue(recipeState, limit);
				_isLimitLocalField?.SetValue(recipeState, false);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetRecipeLimitFromGlobal failed: {ex.Message}"); }
		}

		/// <summary>
		/// Get recipe priority (0-3, higher = worked on first).
		/// </summary>
		public static int GetRecipePriority(object recipeState) {
			if (recipeState == null) return 0;

			EnsureRecipeTypes();

			try {
				return (int?)_recipePrioField?.GetValue(recipeState) ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Set recipe priority (clamped to 0-3).
		/// </summary>
		public static void SetRecipePriority(object recipeState, int priority) {
			if (recipeState == null) return;

			EnsureRecipeTypes();

			try {
				int clamped = Math.Max(0, Math.Min(3, priority));
				_recipePrioField?.SetValue(recipeState, clamped);
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] SetRecipePriority failed: {ex.Message}"); }
		}

		// ========================================
		// PUBLIC API - STORAGE
		// ========================================

		/// <summary>
		/// Check if a building has ProductionStorage.
		/// </summary>
		public static bool HasProductionStorage(object building) {
			if (building == null || !IsProductionBuilding(building)) return false;

			EnsureProductionTypes();

			try {
				var storage = ReflectionHelper.GetProp(_productionStorageProperty, building);
				return storage != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get storage goods for a production building.
		/// Returns list of (goodName, amount) pairs for goods with amount > 0.
		/// </summary>
		public static List<(string goodName, int amount)> GetProductionStorageGoods(object building) {
			var result = new List<(string, int)>();

			if (building == null || !IsProductionBuilding(building)) return result;

			EnsureProductionTypes();
			EnsureStorageTypes();

			try {
				var storage = ReflectionHelper.GetProp(_productionStorageProperty, building);
				if (storage == null) return result;

				var goodsCollection = ReflectionHelper.GetProp(_storageGoodsProperty, storage);
				if (goodsCollection == null) return result;

				// Get the goods dictionary (Dictionary<string, int>)
				var goodsDict = ReflectionHelper.GetField(_goodsCollectionGoodsField, goodsCollection);
				if (goodsDict == null) return result;

				// Iterate through the dictionary
				var keys = ReflectionHelper.IterateKeys(goodsDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					string goodName = key as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					int amount = ReflectionHelper.DictGetInt(goodsDict, key);
					if (amount > 0) {
						result.Add((goodName, amount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetProductionStorageGoods failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Resolve a camp recipe's produced good internal name (e.g. "Wood") from the recipe's model name.
		/// Camp recipes use plain RecipeState (no productName field); the produced good lives on CampRecipeModel.refGood.
		/// </summary>
		public static string GetCampRecipeGoodName(string recipeModelName) {
			if (string.IsNullOrEmpty(recipeModelName)) return null;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var model = ReflectionHelper.Invoke(_settingsGetCampRecipeMethod, settings, recipeModelName);
				if (model == null) return null;

				var refGood = ReflectionHelper.GetField(_campRecipeModelRefGoodField, model);
				if (refGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, refGood);
				if (goodModel == null) return null;

				return ReflectionHelper.GetPropString(_recipeGoodModelNameProperty, goodModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCampRecipeGoodName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Resolve a fishing hut recipe's produced good internal name (e.g. "Fish") from the recipe's model name.
		/// Fishing Hut recipes use plain RecipeState (no productName field); the produced good lives on FishingHutRecipeModel.refGood.
		/// </summary>
		public static string GetFishingHutRecipeGoodName(string recipeModelName) {
			if (string.IsNullOrEmpty(recipeModelName)) return null;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var model = ReflectionHelper.Invoke(_settingsGetFishingHutRecipeMethod, settings, recipeModelName);
				if (model == null) return null;

				var refGood = ReflectionHelper.GetField(_fishingHutRecipeModelRefGoodField, model);
				if (refGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, refGood);
				if (goodModel == null) return null;

				return ReflectionHelper.GetPropString(_recipeGoodModelNameProperty, goodModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetFishingHutRecipeGoodName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Resolve a gatherer hut recipe's produced good internal name from the recipe's model name.
		/// GathererHut recipes use plain RecipeState (no productName field); the produced good lives on GathererHutRecipeModel.refGood.
		/// </summary>
		public static string GetGathererHutRecipeGoodName(string recipeModelName) {
			if (string.IsNullOrEmpty(recipeModelName)) return null;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var model = ReflectionHelper.Invoke(_settingsGetGathererHutRecipeMethod, settings, recipeModelName);
				if (model == null) return null;

				var refGood = ReflectionHelper.GetField(_gathererHutRecipeModelRefGoodField, model);
				if (refGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, refGood);
				if (goodModel == null) return null;

				return ReflectionHelper.GetPropString(_recipeGoodModelNameProperty, goodModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetGathererHutRecipeGoodName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Resolve a collector recipe's produced good internal name from the recipe's model name.
		/// Collector recipes use plain RecipeState (no productName field); the produced good lives on CollectorRecipeModel.producedGood.
		/// </summary>
		public static string GetCollectorRecipeGoodName(string recipeModelName) {
			if (string.IsNullOrEmpty(recipeModelName)) return null;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var model = ReflectionHelper.Invoke(_settingsGetCollectorRecipeMethod, settings, recipeModelName);
				if (model == null) return null;

				var producedGood = ReflectionHelper.GetField(_collectorRecipeModelProducedGoodField, model);
				if (producedGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGood);
				if (goodModel == null) return null;

				return ReflectionHelper.GetPropString(_recipeGoodModelNameProperty, goodModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetCollectorRecipeGoodName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Resolve a mine recipe's produced good internal name from the recipe's model name.
		/// Mine recipes use plain RecipeState (no productName field); the produced good lives on MineRecipeModel.producedGood.
		/// </summary>
		public static string GetMineRecipeGoodName(string recipeModelName) {
			if (string.IsNullOrEmpty(recipeModelName)) return null;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return null;

				var model = ReflectionHelper.Invoke(_settingsGetMineRecipeMethod, settings, recipeModelName);
				if (model == null) return null;

				var producedGood = ReflectionHelper.GetField(_mineRecipeModelProducedGoodField, model);
				if (producedGood == null) return null;

				var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, producedGood);
				if (goodModel == null) return null;

				return ReflectionHelper.GetPropString(_recipeGoodModelNameProperty, goodModel);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMineRecipeGoodName failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get display name for a good.
		/// </summary>
		public static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return goodName;

			EnsureRecipeModelTypes();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return goodName;

				// settings.GetGood(name)
				var getGoodMethod = settings.GetType().GetMethod("GetGood", new[] { typeof(string) });
				var goodModel = getGoodMethod?.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return goodName;

				var displayNameLoca = ReflectionHelper.GetField(_recipeGoodModelDisplayNameField, goodModel);
				return GameReflection.GetLocaText(displayNameLoca) ?? goodName;
			} catch {
				return goodName;
			}
		}

		/// <summary>
		/// Check if a building has IngredientsStorage (implements IWorkshop).
		/// </summary>
		public static bool HasIngredientsStorage(object building) {
			if (building == null) return false;

			EnsureWorkshopTypes();

			if (_workshopInterfaceType == null) return false;

			// Check if building implements IWorkshop
			if (!_workshopInterfaceType.IsInstanceOfType(building))
				return false;

			try {
				var storage = ReflectionHelper.GetProp(_workshopIngredientsStorageProperty, building);
				return storage != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get ingredients storage goods for a building (input goods).
		/// Returns list of (goodName, amount) pairs for goods with amount > 0.
		/// </summary>
		public static List<(string goodName, int amount)> GetIngredientsStorageGoods(object building) {
			var result = new List<(string, int)>();

			if (building == null) return result;

			EnsureWorkshopTypes();
			EnsureIngredientsStorageTypes();

			if (_workshopInterfaceType == null || !_workshopInterfaceType.IsInstanceOfType(building))
				return result;

			try {
				var storage = ReflectionHelper.GetProp(_workshopIngredientsStorageProperty, building);
				if (storage == null) return result;

				var goodsCollection = ReflectionHelper.GetField(_ingredientsStorageGoodsField, storage);
				if (goodsCollection == null) return result;

				// Get the goods dictionary (Dictionary<string, int>)
				var goodsDict = ReflectionHelper.GetField(_goodsCollectionGoodsField, goodsCollection);
				if (goodsDict == null) return result;

				// Iterate through the dictionary
				var keys = ReflectionHelper.IterateKeys(goodsDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					string goodName = key as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					int amount = ReflectionHelper.DictGetInt(goodsDict, key);
					if (amount > 0) {
						result.Add((goodName, amount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetIngredientsStorageGoods failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get delivery state for a good in production storage.
		/// Returns (isForced, isConstantForced) tuple.
		/// </summary>
		public static (bool isForced, bool isConstantForced) GetOutputDeliveryState(object building, string goodName) {
			if (building == null || string.IsNullOrEmpty(goodName))
				return (false, false);

			EnsureProductionTypes();
			EnsureStorageTypes();

			try {
				var storage = ReflectionHelper.GetProp(_productionStorageProperty, building);
				if (storage == null) return (false, false);

				var goodsCollection = ReflectionHelper.GetProp(_storageGoodsProperty, storage);
				if (goodsCollection == null) return (false, false);

				var deliveryState = ReflectionHelper.Invoke(_storageGetDeliveryStateMethod, goodsCollection, goodName);
				if (deliveryState == null) return (false, false);

				bool isForced = ReflectionHelper.GetBool(_deliveryStateForcedField, deliveryState);
				bool isConstantForced = ReflectionHelper.GetBool(_deliveryStateConstantForcedField, deliveryState);

				return (isForced, isConstantForced);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetOutputDeliveryState failed: {ex.Message}");
				return (false, false);
			}
		}

		/// <summary>
		/// Toggle force delivery for a good in production storage.
		/// Forces next available worker to transport the product to warehouse.
		/// </summary>
		public static bool ToggleForceDelivery(object building, string goodName) {
			if (building == null || string.IsNullOrEmpty(goodName))
				return false;

			EnsureProductionTypes();
			EnsureStorageTypes();

			try {
				var storage = ReflectionHelper.GetProp(_productionStorageProperty, building);
				if (storage == null) return false;

				var goodsCollection = ReflectionHelper.GetProp(_storageGoodsProperty, storage);
				if (goodsCollection == null) return false;

				var deliveryState = ReflectionHelper.Invoke(_storageGetDeliveryStateMethod, goodsCollection, goodName);
				if (deliveryState == null) return false;

				ReflectionHelper.InvokeVoid(_storageSwitchForceDeliveryMethod, storage, goodName, deliveryState);

				Debug.Log($"[ATSAccessibility] Toggled force delivery for {goodName}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ToggleForceDelivery failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Toggle constant (auto) delivery for a good in production storage.
		/// When enabled, product is delivered each time it's produced instead of waiting for storage full.
		/// </summary>
		public static bool ToggleConstantDelivery(object building, string goodName) {
			if (building == null || string.IsNullOrEmpty(goodName))
				return false;

			EnsureProductionTypes();
			EnsureStorageTypes();

			try {
				var storage = ReflectionHelper.GetProp(_productionStorageProperty, building);
				if (storage == null) return false;

				var goodsCollection = ReflectionHelper.GetProp(_storageGoodsProperty, storage);
				if (goodsCollection == null) return false;

				var deliveryState = ReflectionHelper.Invoke(_storageGetDeliveryStateMethod, goodsCollection, goodName);
				if (deliveryState == null) return false;

				ReflectionHelper.InvokeVoid(_storageSwitchConstantForceDeliveryMethod, storage, goodName, deliveryState);

				Debug.Log($"[ATSAccessibility] Toggled constant delivery for {goodName}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ToggleConstantDelivery failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Return an ingredient from ingredients storage back to the main warehouse.
		/// </summary>
		public static bool ReturnIngredientToWarehouse(object building, string goodName, int amount) {
			if (building == null || string.IsNullOrEmpty(goodName) || amount <= 0)
				return false;

			EnsureWorkshopTypes();
			EnsureIngredientsStorageTypes();

			if (_workshopInterfaceType == null || !_workshopInterfaceType.IsInstanceOfType(building))
				return false;

			try {
				// Get ingredients storage
				var ingredientsStorage = ReflectionHelper.GetProp(_workshopIngredientsStorageProperty, building);
				if (ingredientsStorage == null) return false;

				var goodsCollection = ReflectionHelper.GetField(_ingredientsStorageGoodsField, ingredientsStorage);
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
				if (storageService != null) {
					// Find the IngredientsReturn operation type
					var operationType = GameReflection.GameAssembly?.GetType("Eremite.Model.StorageOperationType");
					object ingredientsReturnValue = null;
					if (operationType != null) {
						ingredientsReturnValue = Enum.Parse(operationType, "IngredientsReturn");
					}

					var storeMethod = storageService.GetType().GetMethod("Store", new[] { goodType, typeof(string), typeof(int), operationType });
					storeMethod?.Invoke(storageService, new object[] { good, modelName, buildingId, ingredientsReturnValue });
				}

				Debug.Log($"[ATSAccessibility] Returned {amount} {goodName} to warehouse");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ReturnIngredientToWarehouse failed: {ex.Message}");
				return false;
			}
		}

		// ========================================
		// PUBLIC API - HEARTH (type-check only; rest is in HearthReflection.cs)
		// ========================================

		/// <summary>
		/// Check if building is a Hearth. Used by BuildingPanelHandler for navigator routing.
		/// All other hearth API is in HearthReflection.
		/// </summary>
		public static bool IsHearth(object building) {
			if (building == null) return false;

			EnsureHearthBaseType();

			if (_hearthType == null) return false;

			return _hearthType.IsInstanceOfType(building);
		}
		// ========================================
		// PUBLIC API - HOUSE
		// ========================================

		/// <summary>
		/// Check if building is a House.
		/// </summary>
		public static bool IsHouse(object building) {
			if (building == null) return false;

			EnsureHouseTypes();

			if (_houseType == null) return false;

			return _houseType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Get House resident villager IDs.
		/// </summary>
		public static List<int> GetHouseResidents(object building) {
			var result = new List<int>();

			if (!IsHouse(building)) return result;

			EnsureHouseTypes();

			try {
				var state = ReflectionHelper.GetField(_houseStateField, building);
				if (state == null) return result;

				var residents = ReflectionHelper.GetList(_houseStateResidentsField, state);
				if (residents == null) return result;

				foreach (var id in residents) {
					if (id is int intId) {
						result.Add(intId);
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetHouseResidents failed: {ex.Message}"); }

			return result;
		}

		/// <summary>
		/// Get current House capacity (may be reduced by effects).
		/// </summary>
		public static int GetHouseCapacity(object building) {
			if (!IsHouse(building)) return 0;

			EnsureHouseTypes();

			try {
				return ReflectionHelper.InvokeInt(_houseGetHousingPlacesMethod, building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get maximum House capacity.
		/// </summary>
		public static int GetHouseMaxCapacity(object building) {
			if (!IsHouse(building)) return 0;

			EnsureHouseTypes();

			try {
				return ReflectionHelper.InvokeInt(_houseGetMaxHousingPlacesMethod, building);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Check if House is full.
		/// </summary>
		public static bool IsHouseFull(object building) {
			if (!IsHouse(building)) return false;

			EnsureHouseTypes();

			try {
				var result = ReflectionHelper.Invoke(_houseIsFullMethod, building);
				return (bool?)result ?? false;
			} catch {
				return false;
			}
		}

		// ========================================
		// PUBLIC API - RELIC (type-check only; rest is in RelicReflection.cs)
		// ========================================

		/// <summary>
		/// Check if building is a Relic.
		/// Used for building panel routing (BuildingPanelHandler). All other relic API is in RelicReflection.cs.
		/// </summary>
		public static bool IsRelic(object building) {
			if (building == null) return false;

			EnsureRelicBaseType();

			if (_relicType == null) return false;

			return _relicType.IsInstanceOfType(building);
		}

		// ========================================
		// PUBLIC API - PORT (type-check only; rest is in PortReflection.cs)
		// ========================================

		/// <summary>
		/// Check if building is a Port.
		/// Used for building panel routing (BuildingPanelHandler). All other port API is in PortReflection.cs.
		/// </summary>
		public static bool IsPort(object building) {
			if (building == null) return false;

			EnsurePortBaseType();

			if (_portType == null) return false;

			return _portType.IsInstanceOfType(building);
		}

		// ========================================
		// PUBLIC API - EVENT SUBSCRIPTION
		// ========================================

		/// <summary>
		/// Subscribe to OnBuildingPanelShown event.
		/// Callback receives the Building object.
		/// </summary>
		public static IDisposable SubscribeToBuildingPanelShown(Action<object> callback) {
			EnsureEventTypes();

			try {
				var blackboard = GameReflection.GetGameBlackboardService();
				if (blackboard == null || _onBuildingPanelShownProperty == null) return null;

				var observable = _onBuildingPanelShownProperty.GetValue(blackboard);
				if (observable == null) return null;

				return GameReflection.SubscribeToObservable(observable, callback);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SubscribeToBuildingPanelShown failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Subscribe to OnBuildingPanelClosed event.
		/// Callback receives the Building object.
		/// </summary>
		public static IDisposable SubscribeToBuildingPanelClosed(Action<object> callback) {
			EnsureEventTypes();

			try {
				var blackboard = GameReflection.GetGameBlackboardService();
				if (blackboard == null || _onBuildingPanelClosedProperty == null) return null;

				var observable = _onBuildingPanelClosedProperty.GetValue(blackboard);
				if (observable == null) return null;

				return GameReflection.SubscribeToObservable(observable, callback);
			} catch (Exception ex) {
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
		public static bool IsDecoration(object building) {
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
		public static bool IsStorage(object building) {
			if (building == null) return false;

			EnsureStorageType2();

			if (_storageType == null) return false;

			return _storageType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if workplaces are active for a building.
		/// For Storage buildings, this checks if haulers are unlocked via meta progression.
		/// For other ProductionBuildings, this is typically always true.
		/// </summary>
		public static bool AreWorkplacesActive(object building) {
			if (building == null) return false;
			if (!IsProductionBuilding(building)) return false;

			try {
				var areWorkplacesActiveProp = building.GetType().GetProperty("AreWorkplacesActive",
					BindingFlags.Public | BindingFlags.Instance);
				if (areWorkplacesActiveProp != null) {
					return (bool)areWorkplacesActiveProp.GetValue(building);
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] AreWorkplacesActive failed: {ex.Message}"); }

			// Default to true for non-Storage production buildings
			return true;
		}

		/// <summary>
		/// Check if a building currently needs/accepts worker assignment.
		/// This is a higher-level check than AreWorkplacesActive:
		/// - Buildings under construction: never (must be completed first)
		/// - Port: only during Phase 2 (decision made, expedition not started)
		/// - Relic: during Phase 2 (working) or Phase 3 (collecting rewards)
		/// - Storage: only when haulers are unlocked (via AreWorkplacesActive)
		/// - Other buildings: whenever workplaces are active
		/// </summary>
		public static bool ShouldAllowWorkerManagement(object building) {
			if (building == null) return false;
			if (ConstructionReflection.IsBuildingUnfinished(building)) return false;
			if (!IsProductionBuilding(building)) return false;
			if (!AreWorkplacesActive(building)) return false;

			if (IsPort(building)) {
				return PortReflection.WasPortDecisionMade(building) && !PortReflection.IsPortExpeditionStarted(building);
			}

			if (IsRelic(building)) {
				// Phase A: Before investigation - workers must be assigned before starting
				if (!RelicReflection.IsRelicInvestigationStarted(building) && RelicReflection.RelicHasAnyWorkplace(building))
					return true;
				// Phase B: Working (investigation started but not finished)
				if (RelicReflection.IsRelicInvestigationStarted(building) && !RelicReflection.IsRelicInvestigationFinished(building))
					return true;
				// Phase C: Collecting rewards (investigation finished but rewards still need to be unloaded)
				if (RelicReflection.IsRelicInvestigationFinished(building) && RelicReflection.GetRelicRewardStorageFullSum(building) > 0)
					return true;
				return false;
			}

			return true;
		}

		// Institution API: see InstitutionReflection.cs
		public static bool IsInstitution(object building) => InstitutionReflection.IsInstitution(building);

		// Shrine API: see ShrineReflection.cs
		public static bool IsShrine(object building) => ShrineReflection.IsShrine(building);

		// Poro API: see PoroReflection.cs
		public static bool IsPoro(object building) => PoroReflection.IsPoro(building);

		// ========================================
		// PUBLIC API - RAINCATCHER
		// ========================================

		/// <summary>
		/// Check if building is a RainCatcher.
		/// </summary>
		public static bool IsRainCatcher(object building) {
			if (building == null) return false;

			EnsureRainCatcherTypes();

			if (_rainCatcherType == null) return false;

			return _rainCatcherType.IsInstanceOfType(building);
		}

		// ========================================
		// PUBLIC API - EXTRACTOR
		// ========================================

		/// <summary>
		/// Check if building is an Extractor.
		/// </summary>
		public static bool IsExtractor(object building) {
			if (building == null) return false;

			EnsureExtractorTypes();

			if (_extractorType == null) return false;

			return _extractorType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if a building model is an ExtractorModel.
		/// </summary>
		public static bool IsExtractorModel(object buildingModel) {
			if (buildingModel == null) return false;

			EnsureExtractorTypes();

			if (_extractorModelType == null) return false;

			return _extractorModelType.IsInstanceOfType(buildingModel);
		}

		/// <summary>
		/// Get the production time for an Extractor.
		/// </summary>
		public static float GetExtractorProductionTime(object building) {
			if (!IsExtractor(building)) return 0f;

			EnsureExtractorTypes();

			try {
				var model = ReflectionHelper.GetField(_extractorModelField, building);
				if (model == null) return 0f;

				return (float?)_extractorModelProductionTimeField?.GetValue(model) ?? 0f;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get the produced amount for an Extractor.
		/// </summary>
		public static int GetExtractorProducedAmount(object building) {
			if (!IsExtractor(building)) return 0;

			EnsureExtractorTypes();

			try {
				var model = ReflectionHelper.GetField(_extractorModelField, building);
				if (model == null) return 0;

				return ReflectionHelper.GetInt(_extractorModelProducedAmountField, model);
			} catch {
				return 0;
			}
		}

		// ========================================
		// PUBLIC API - HYDRANT
		// ========================================

		/// <summary>
		/// Check if building is a Hydrant.
		/// </summary>
		public static bool IsHydrant(object building) {
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
		private static System.Collections.IList GetCycleAbilitiesList() {
			EnsureCycleAbilityTypes();
			var conditionsState = GameReflection.GetConditionsState();
			if (conditionsState == null) return null;

			try {
				return ReflectionHelper.GetList(_condCycleAbilitiesField, conditionsState);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the number of cycle abilities available.
		/// </summary>
		public static int GetCycleAbilityCount() {
			var abilities = GetCycleAbilitiesList();
			return abilities?.Count ?? 0;
		}

		/// <summary>
		/// Get the display name of a cycle ability at the given index.
		/// </summary>
		public static string GetCycleAbilityName(int index) {
			EnsureCycleAbilityTypes();
			EnsureGameModelServiceTypes();

			var abilities = GetCycleAbilitiesList();
			if (abilities == null || index < 0 || index >= abilities.Count) return null;

			try {
				var ability = abilities[index];
				if (ability == null) return null;

				// Get the gameEffect string
				string gameEffect = ReflectionHelper.GetString(_cycleAbilityGameEffectField, ability);
				if (string.IsNullOrEmpty(gameEffect)) return null;

				// Get the effect model
				var effectModel = GetEffectModel(gameEffect);
				if (effectModel == null) return gameEffect;  // Fallback to ID

				// Get display name from effect model
				var displayName = ReflectionHelper.GetField(_effectModelDisplayNameField, effectModel);
				return GameReflection.GetLocaText(displayName) ?? gameEffect;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the remaining charges of a cycle ability at the given index.
		/// </summary>
		public static int GetCycleAbilityCharges(int index) {
			EnsureCycleAbilityTypes();

			var abilities = GetCycleAbilitiesList();
			if (abilities == null || index < 0 || index >= abilities.Count) return 0;

			try {
				var ability = abilities[index];
				if (ability == null) return 0;

				return ReflectionHelper.GetInt(_cycleAbilityChargesField, ability);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the description of a cycle ability at the given index.
		/// </summary>
		public static string GetCycleAbilityDescription(int index) {
			EnsureCycleAbilityTypes();
			EnsureGameModelServiceTypes();
			EnsureEffectDescriptionTypes();  // For _effectModelDescriptionProperty

			var abilities = GetCycleAbilitiesList();
			if (abilities == null || index < 0 || index >= abilities.Count) return null;

			try {
				var ability = abilities[index];
				if (ability == null) return null;

				string gameEffect = ReflectionHelper.GetString(_cycleAbilityGameEffectField, ability);
				if (string.IsNullOrEmpty(gameEffect)) return null;

				var effectModel = GetEffectModel(gameEffect);
				if (effectModel == null) return null;

				return ReflectionHelper.GetPropString(_effectModelDescriptionProperty, effectModel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Use a cycle ability at the given index, decrementing charges and applying the effect.
		/// Returns true if successful.
		/// </summary>
		public static bool UseCycleAbility(int index) {
			EnsureCycleAbilityTypes();
			EnsureGameModelServiceTypes();
			EnsureStorageService2Types();

			var abilities = GetCycleAbilitiesList();
			if (abilities == null || index < 0 || index >= abilities.Count) return false;

			try {
				var ability = abilities[index];
				if (ability == null) return false;

				// Check charges
				int charges = ReflectionHelper.GetInt(_cycleAbilityChargesField, ability);
				if (charges <= 0) return false;

				// Get the effect model
				string gameEffect = ReflectionHelper.GetString(_cycleAbilityGameEffectField, ability);
				if (string.IsNullOrEmpty(gameEffect)) return false;

				var effectModel = GetEffectModel(gameEffect);
				if (effectModel == null) return false;

				// Check if effect can be drawn
				bool canBeDrawn = ReflectionHelper.InvokeBool(_effectModelCanBeDrawnMethod, effectModel);
				if (!canBeDrawn) return false;

				// Decrement charges
				_cycleAbilityChargesField?.SetValue(ability, charges - 1);

				// Get main storage info for the effect context
				var storageService = GetStorageServiceInternal();
				string sourceName = "Main Storage";
				int sourceId = 0;

				if (storageService != null && _storageServiceMainProperty != null) {
					var mainStorage = _storageServiceMainProperty.GetValue(storageService);
					if (mainStorage != null) {
						var modelNameProp = mainStorage.GetType().GetProperty("ModelName", GameReflection.PublicInstance);
						var idProp = mainStorage.GetType().GetProperty("Id", GameReflection.PublicInstance);
						sourceName = modelNameProp?.GetValue(mainStorage) as string ?? sourceName;
						sourceId = (int?)idProp?.GetValue(mainStorage) ?? 0;
					}
				}

				// Apply the effect with EffectContextType.Building (enum value 0)
				var assembly = GameReflection.GameAssembly;
				var effectContextType = assembly?.GetType("Eremite.Model.Effects.EffectContextType");
				if (effectContextType != null && _effectModelApplyMethod != null) {
					var buildingContext = Enum.ToObject(effectContextType, 0);  // Building = 0
					_effectModelApplyMethod.Invoke(effectModel, new object[] { buildingContext, sourceName, sourceId });
				}

				Debug.Log($"[ATSAccessibility] Used cycle ability: {gameEffect}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UseCycleAbility failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get an EffectModel by name from GameModelService.
		/// </summary>
		internal static object GetEffectModel(string effectName) {
			EnsureGameModelServiceTypes();

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsGameModelServiceProperty == null) return null;

			try {
				var gameModelService = _gsGameModelServiceProperty.GetValue(gameServices);
				if (gameModelService == null) return null;

				return ReflectionHelper.Invoke(_gmsGetEffectMethod, gameModelService, effectName);
			} catch {
				return null;
			}
		}

		// ========================================
		// PUBLIC API - BLIGHT FUEL (for Hydrant)
		// ========================================

		/// <summary>
		/// Get the number of free (unfought) cysts globally.
		/// </summary>
		public static int GetBlightFreeCysts() {
			EnsureBlightServiceTypes();

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsBlightServiceProperty == null) return 0;

			try {
				var blightService = _gsBlightServiceProperty.GetValue(gameServices);
				if (blightService == null) return 0;

				return ReflectionHelper.InvokeInt(_blightCountFreeCystsMethod, blightService);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the current amount of blight post fuel in storage.
		/// </summary>
		public static int GetBlightFuelAmount() {
			EnsureBlightConfigTypes();
			EnsureStorageService2Types();

			string fuelName = GetBlightFuelNameInternal();
			if (string.IsNullOrEmpty(fuelName)) return 0;

			var storageService = GetStorageServiceInternal();
			if (storageService == null) return 0;

			try {
				var mainStorage = ReflectionHelper.GetProp(_storageServiceMainProperty, storageService);
				if (mainStorage == null) return 0;

				return ReflectionHelper.InvokeInt(_mainStorageGetAmountMethod, mainStorage, fuelName);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the display name of the blight post fuel.
		/// </summary>
		public static string GetBlightFuelName() {
			EnsureBlightConfigTypes();

			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsBlightConfigField == null) return null;

			try {
				var blightConfig = _settingsBlightConfigField.GetValue(settings);
				if (blightConfig == null) return null;

				var blightPostFuel = ReflectionHelper.GetField(_blightConfigBlightPostFuelField, blightConfig);
				if (blightPostFuel == null) return null;

				return ReflectionHelper.GetPropString(GameReflection.GoodRefDisplayNameProperty, blightPostFuel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the internal name of the blight post fuel (for storage lookups).
		/// </summary>
		private static string GetBlightFuelNameInternal() {
			EnsureBlightConfigTypes();

			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsBlightConfigField == null) return null;

			try {
				var blightConfig = _settingsBlightConfigField.GetValue(settings);
				if (blightConfig == null) return null;

				var blightPostFuel = ReflectionHelper.GetField(_blightConfigBlightPostFuelField, blightConfig);
				if (blightPostFuel == null) return null;

				return ReflectionHelper.GetPropString(_goodRefNameProperty, blightPostFuel);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get StorageService from GameServices (internal helper).
		/// </summary>
		private static object GetStorageServiceInternal() {
			EnsureStorageService2Types();

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsStorageService2Property == null) return null;

			try {
				return _gsStorageService2Property.GetValue(gameServices);
			} catch {
				return null;
			}
		}

		// ========================================
		// PUBLIC API - WATER TANK (for RainCatcher/Extractor)
		// ========================================

		/// <summary>
		/// Get the current water level in the tank for the water type produced by a building.
		/// </summary>
		public static int GetWaterTankCurrent(object building) {
			EnsureRainpunkServiceTypes();

			var waterModel = GetWaterModelFromBuilding(building);
			if (waterModel == null) return 0;

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsRainpunkServiceProperty == null) return 0;

			try {
				var rainpunkService = _gsRainpunkServiceProperty.GetValue(gameServices);
				if (rainpunkService == null) return 0;

				return ReflectionHelper.InvokeInt(_rainpunkCountWaterLeftMethod, rainpunkService, waterModel);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the maximum water tank capacity for the water type produced by a building.
		/// </summary>
		public static int GetWaterTankCapacity(object building) {
			EnsureRainpunkServiceTypes();

			var waterModel = GetWaterModelFromBuilding(building);
			if (waterModel == null) return 0;

			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null || _gsRainpunkServiceProperty == null) return 0;

			try {
				var rainpunkService = _gsRainpunkServiceProperty.GetValue(gameServices);
				if (rainpunkService == null) return 0;

				return ReflectionHelper.InvokeInt(_rainpunkCountTanksCapacityMethod, rainpunkService, waterModel);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the WaterModel from a RainCatcher, Extractor, or Workshop building.
		/// </summary>
		private static object GetWaterModelFromBuilding(object building) {
			if (building == null) return null;

			try {
				if (IsRainCatcher(building)) {
					EnsureRainCatcherTypes();
					return ReflectionHelper.Invoke(_rainCatcherGetCurrentWaterTypeMethod, building);
				} else if (IsExtractor(building)) {
					EnsureExtractorTypes();
					return ReflectionHelper.Invoke(_extractorGetWaterTypeMethod, building);
				} else if (IsWorkshopClass(building)) {
					EnsureRainpunkEngineTypes();
					var model = ReflectionHelper.GetField(_workshopModelField, building);
					if (model == null) return null;
					var rainpunkModel = ReflectionHelper.GetField(_wmRainpunkField, model);
					if (rainpunkModel == null) return null;
					return ReflectionHelper.GetField(_brpWaterField, rainpunkModel);
				}
			} catch {
				// Fall through
			}

			return null;
		}

		/// <summary>
		/// Get the water type name for any water-using building (workshop, RainCatcher, Extractor).
		/// </summary>
		public static string GetWaterTypeName(object building) {
			EnsureWaterModelTypes();

			var waterModel = GetWaterModelFromBuilding(building);
			if (waterModel == null) return null;

			return ReflectionHelper.GetLocaString(_waterModelDisplayNameField, waterModel);
		}

		/// <summary>
		/// Get total water consumption per second based on requested engine levels.
		/// </summary>
		public static float GetTotalWaterUsePerSecond(object building) {
			if (!IsWorkshopClass(building)) return 0f;
			if (!IsRainpunkUnlocked(building)) return 0f;

			EnsureRainpunkEngineTypes();

			try {
				int engineCount = GetEngineCount(building);
				float totalUse = 0f;

				for (int i = 0; i < engineCount; i++) {
					int currentLevel = GetEngineRequestedLevel(building, i);
					if (currentLevel <= 0) continue;

					var engineModel = GetEngineModel(building, i);
					if (engineModel == null) continue;

					float waterPerSec = (float?)_engineModelWaterPerSecField?.GetValue(engineModel) ?? 0f;
					totalUse += waterPerSec * currentLevel;
				}

				return totalUse;
			} catch {
				return 0f;
			}
		}

		/// <summary>
		/// Get blightrot progress as a percentage (0-100).
		/// Returns -1 if blight is not active or not spawning from this building.
		/// </summary>
		public static int GetBlightProgress(object building) {
			if (!IsWorkshopClass(building)) return -1;
			if (!IsRainpunkUnlocked(building)) return -1;

			EnsureRainpunkEngineTypes();
			EnsureRainpunkServiceTypes();

			try {
				// Get waterUsed from workshop state
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return -1;

				int waterUsed = ReflectionHelper.GetInt(_wsWaterUsedField, state);

				// Get waterPerCyst from RainpunkService
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return -1;

				var rainpunkService = ReflectionHelper.GetProp(_gsRainpunkServiceProperty, gameServices);
				if (rainpunkService == null) return -1;

				// Check if blight is spawning from this building
				bool isSpawning = ReflectionHelper.InvokeBool(_rainpunkIsWaterSpawningBlightMethod, rainpunkService, building);
				if (!isSpawning) return -1;

				int waterPerCyst = ReflectionHelper.InvokeInt(_rainpunkGetWaterPerCystsMethod, rainpunkService, building);
				if (waterPerCyst <= 0) return 0;

				return (int)((float)waterUsed / waterPerCyst * 100);
			} catch {
				return -1;
			}
		}

		// ========================================
		// PUBLIC API - RAINPUNK ENGINES (for Workshops)
		// ========================================

		/// <summary>
		/// Check if a building is specifically the Workshop class (not just IWorkshop).
		/// Only Workshop class has rainpunk engines.
		/// </summary>
		private static bool IsWorkshopClass(object building) {
			if (building == null) return false;
			EnsureRainpunkEngineTypes();
			return _workshopType != null && _workshopType.IsInstanceOfType(building);
		}

		/// <summary>
		/// Check if rainpunk is enabled at the meta/account level.
		/// This is a progression unlock that must be earned.
		/// Path: MetaController.Instance.MetaServices.MetaPerksService.IsRainpunkEnabled()
		/// </summary>
		public static bool IsRainpunkEnabledGlobally() {
			try {
				var assembly = GameReflection.GameAssembly;
				if (assembly == null) return false;

				// Get MetaController.Instance
				var metaControllerType = assembly.GetType("Eremite.Controller.MetaController");
				if (metaControllerType == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaController type not found");
					return false;
				}

				var instanceProp = metaControllerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
				if (instanceProp == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: Instance property not found");
					return false;
				}

				var metaController = instanceProp.GetValue(null);
				if (metaController == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaController instance is null");
					return false;
				}

				// Get MetaServices
				var metaServicesProp = metaController.GetType().GetProperty("MetaServices", GameReflection.PublicInstance);
				if (metaServicesProp == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaServices property not found");
					return false;
				}

				var metaServices = metaServicesProp.GetValue(metaController);
				if (metaServices == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaServices is null");
					return false;
				}

				// Get MetaPerksService
				var metaPerksServiceProp = metaServices.GetType().GetProperty("MetaPerksService", GameReflection.PublicInstance);
				if (metaPerksServiceProp == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaPerksService property not found");
					return false;
				}

				var metaPerksService = metaPerksServiceProp.GetValue(metaServices);
				if (metaPerksService == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: MetaPerksService is null");
					return false;
				}

				// Call IsRainpunkEnabled()
				var isRainpunkEnabledMethod = metaPerksService.GetType().GetMethod("IsRainpunkEnabled", GameReflection.PublicInstance);
				if (isRainpunkEnabledMethod == null) {
					Debug.LogError("[ATSAccessibility] IsRainpunkEnabledGlobally: IsRainpunkEnabled method not found");
					return false;
				}

				return (bool?)isRainpunkEnabledMethod.Invoke(metaPerksService, null) ?? false;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] IsRainpunkEnabledGlobally exception: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if a workshop has rainpunk capability (model has rainpunk defined AND meta unlock obtained).
		/// </summary>
		public static bool HasRainpunkCapability(object building) {
			// First check if rainpunk is enabled at the meta level
			if (!IsRainpunkEnabledGlobally()) return false;
			if (!IsWorkshopClass(building)) return false;

			EnsureRainpunkEngineTypes();

			try {
				var model = ReflectionHelper.GetField(_workshopModelField, building);
				if (model == null) return false;

				var rainpunkModel = ReflectionHelper.GetField(_wmRainpunkField, model);
				return rainpunkModel != null;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if rainpunk is unlocked for a workshop.
		/// </summary>
		public static bool IsRainpunkUnlocked(object building) {
			if (!IsWorkshopClass(building)) return false;
			EnsureRainpunkEngineTypes();

			try {
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return false;

				return ReflectionHelper.GetBool(_wsRainpunkUnlockedField, state);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the number of engines in a workshop.
		/// </summary>
		public static int GetEngineCount(object building) {
			if (!IsWorkshopClass(building)) return 0;
			EnsureRainpunkEngineTypes();

			try {
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return 0;

				var engines = _wsEnginesField?.GetValue(state) as Array;
				return engines?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the current level of an engine (actual level based on water availability).
		/// </summary>
		public static int GetEngineCurrentLevel(object building, int engineIndex) {
			var engineState = GetEngineState(building, engineIndex);
			if (engineState == null) return 0;

			try {
				return ReflectionHelper.GetInt(_engineStateLevelField, engineState);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the requested level of an engine (player-set level).
		/// </summary>
		public static int GetEngineRequestedLevel(object building, int engineIndex) {
			var engineState = GetEngineState(building, engineIndex);
			if (engineState == null) return 0;

			try {
				return ReflectionHelper.GetInt(_engineStateRequestedLevelField, engineState);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the maximum level of an engine.
		/// </summary>
		public static int GetEngineMaxLevel(object building, int engineIndex) {
			var engineModel = GetEngineModel(building, engineIndex);
			if (engineModel == null) return 0;

			try {
				return ReflectionHelper.GetInt(_engineModelMaxLevelField, engineModel);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the effect description for a specific engine level.
		/// Returns the perk's display name (e.g., "+25% production speed").
		/// </summary>
		public static string GetEngineLevelEffect(object building, int engineIndex, int level) {
			if (level <= 0) return null;

			var engineModel = GetEngineModel(building, engineIndex);
			if (engineModel == null) return null;

			EnsureRainpunkEngineTypes();

			try {
				// Get the levels array
				var levels = _engineModelLevelsField?.GetValue(engineModel) as Array;
				if (levels == null) return null;

				// Find the level entry (levels array is 0-indexed, level 1 is at index 0)
				int levelIndex = level - 1;
				if (levelIndex < 0 || levelIndex >= levels.Length) return null;

				var levelEntry = levels.GetValue(levelIndex);
				if (levelEntry == null) return null;

				// Get the perk from the level
				var perk = ReflectionHelper.GetField(_engineLevelPerkField, levelEntry);
				if (perk == null) return null;

				// Get the perk's display name
				return _buildingPerkDisplayNameProp?.GetValue(perk) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Increase the requested level of an engine by 1.
		/// </summary>
		public static bool IncreaseEngineLevel(object building, int engineIndex) {
			var engineState = GetEngineState(building, engineIndex);
			if (engineState == null) return false;

			int maxLevel = GetEngineMaxLevel(building, engineIndex);
			int currentRequested = GetEngineRequestedLevel(building, engineIndex);

			if (currentRequested >= maxLevel) return false;

			try {
				_engineStateRequestedLevelField?.SetValue(engineState, currentRequested + 1);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Decrease the requested level of an engine by 1.
		/// </summary>
		public static bool DecreaseEngineLevel(object building, int engineIndex) {
			var engineState = GetEngineState(building, engineIndex);
			if (engineState == null) return false;

			int currentRequested = GetEngineRequestedLevel(building, engineIndex);

			if (currentRequested <= 0) return false;

			try {
				_engineStateRequestedLevelField?.SetValue(engineState, currentRequested - 1);
				return true;
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if any engine in a workshop has requestedLevel > 0 (is running).
		/// </summary>
		public static bool HasRunningEngines(object building) {
			if (!IsRainpunkUnlocked(building)) return false;
			EnsureRainpunkEngineTypes();

			try {
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return false;

				var engines = _wsEnginesField?.GetValue(state) as Array;
				if (engines == null || engines.Length == 0) return false;

				for (int i = 0; i < engines.Length; i++) {
					var engineState = engines.GetValue(i);
					if (engineState != null) {
						int requestedLevel = ReflectionHelper.GetInt(_engineStateRequestedLevelField, engineState);
						if (requestedLevel > 0)
							return true;
					}
				}
			} catch {
				return false;
			}
			return false;
		}

		/// <summary>
		/// Stop all engines in a workshop by setting requestedLevel = 0 for each.
		/// </summary>
		public static bool StopAllEngines(object building) {
			if (!IsRainpunkUnlocked(building)) return false;
			EnsureRainpunkEngineTypes();

			try {
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return false;

				var engines = _wsEnginesField?.GetValue(state) as Array;
				if (engines == null || engines.Length == 0) return false;

				for (int i = 0; i < engines.Length; i++) {
					var engineState = engines.GetValue(i);
					if (engineState != null) {
						_engineStateRequestedLevelField?.SetValue(engineState, 0);
					}
				}
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] StopAllEngines failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the engine state object for a specific engine index.
		/// </summary>
		private static object GetEngineState(object building, int engineIndex) {
			if (!IsWorkshopClass(building)) return null;
			EnsureRainpunkEngineTypes();

			try {
				var state = ReflectionHelper.GetField(_workshopStateField, building);
				if (state == null) return null;

				var engines = _wsEnginesField?.GetValue(state) as Array;
				if (engines == null || engineIndex < 0 || engineIndex >= engines.Length) return null;

				return engines.GetValue(engineIndex);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the engine model object for a specific engine index.
		/// </summary>
		private static object GetEngineModel(object building, int engineIndex) {
			if (!IsWorkshopClass(building)) return null;
			EnsureRainpunkEngineTypes();

			try {
				var model = ReflectionHelper.GetField(_workshopModelField, building);
				if (model == null) return null;

				var rainpunkModel = ReflectionHelper.GetField(_wmRainpunkField, model);
				if (rainpunkModel == null) return null;

				var engineModels = _brpEnginesField?.GetValue(rainpunkModel) as Array;
				if (engineModels == null || engineIndex < 0 || engineIndex >= engineModels.Length) return null;

				return engineModels.GetValue(engineIndex);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Play the engine level increase sound for a specific engine.
		/// </summary>
		public static void PlayEngineUpSound(object building, int engineIndex) {
			PlayEngineSound(building, engineIndex, _engineModelUpSoundField);
		}

		/// <summary>
		/// Play the engine level decrease sound for a specific engine.
		/// </summary>
		public static void PlayEngineDownSound(object building, int engineIndex) {
			PlayEngineSound(building, engineIndex, _engineModelDownSoundField);
		}

		/// <summary>
		/// Play an engine sound from the engine model.
		/// </summary>
		private static void PlayEngineSound(object building, int engineIndex, FieldInfo soundField) {
			if (soundField == null) return;

			EnsureRainpunkEngineTypes();

			try {
				var engineModel = GetEngineModel(building, engineIndex);
				if (engineModel == null) return;

				// Get the SoundRef from the engine model
				var soundRef = soundField.GetValue(engineModel);
				if (soundRef == null) return;

				// Call GetNext() on the SoundRef to get the SoundModel
				var soundModel = ReflectionHelper.Invoke(_soundRefGetNextMethod, soundRef);
				if (soundModel == null) return;

				// Get MainController and play the sound
				var mainController = GameReflection.GetMainControllerInstance();
				if (mainController == null) return;

				var mainControllerType = mainController.GetType();
				var soundsManagerProp = mainControllerType.GetProperty("SoundsManager", GameReflection.PublicInstance);
				var soundsManager = soundsManagerProp?.GetValue(mainController);
				if (soundsManager == null) return;

				var playSoundMethod = soundsManager.GetType().GetMethod("PlaySoundEffect", GameReflection.PublicInstance);
				playSoundMethod?.Invoke(soundsManager, new object[] { soundModel });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PlayEngineSound failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Get the rainpunk unlock price for a workshop.
		/// Returns (goodName, displayName, amount) or null if not applicable.
		/// </summary>
		public static (string goodName, string displayName, int amount)? GetRainpunkUnlockPrice(object building) {
			if (!IsWorkshopClass(building)) return null;
			if (!HasRainpunkCapability(building)) return null;
			if (IsRainpunkUnlocked(building)) return null;

			EnsureRainpunkEngineTypes();

			try {
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
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRainpunkUnlockPrice failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Check if we have enough resources to unlock rainpunk.
		/// </summary>
		public static bool CanAffordRainpunkUnlock(object building) {
			var price = GetRainpunkUnlockPrice(building);
			if (price == null) return false;

			int stored = GetMainStorageAmount(price.Value.goodName);
			return stored >= price.Value.amount;
		}

		/// <summary>
		/// Unlock rainpunk for a workshop (pays the cost).
		/// </summary>
		public static bool UnlockRainpunk(object building) {
			if (!IsWorkshopClass(building)) return false;
			if (!HasRainpunkCapability(building)) return false;
			if (IsRainpunkUnlocked(building)) return false;
			if (!CanAffordRainpunkUnlock(building)) return false;

			try {
				var unlockMethod = building.GetType().GetMethod("UnlockRainpunk", GameReflection.PublicInstance);
				if (unlockMethod == null) return false;

				unlockMethod.Invoke(building, null);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] UnlockRainpunk failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get amount of a good from the main storage.
		/// </summary>
		private static int GetMainStorageAmount(string goodName) {
			try {
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return 0;

				EnsureStorageService2Types();
				var storageService = ReflectionHelper.GetProp(_gsStorageService2Property, gameServices);
				if (storageService == null) return 0;

				var mainStorage = ReflectionHelper.GetProp(_storageServiceMainProperty, storageService);
				if (mainStorage == null) return 0;

				return ReflectionHelper.InvokeInt(_mainStorageGetAmountMethod, mainStorage, goodName);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the amount of a named good stored in the settlement's main storage.
		/// </summary>
		public static int GetStoredGoodAmount(string goodName) {
			EnsureStorageService2Types();

			try {
				var storageService = GetStorageServiceInternal();
				if (storageService == null) return 0;

				var mainStorage = ReflectionHelper.GetProp(_storageServiceMainProperty, storageService);
				if (mainStorage == null) return 0;

				var result = ReflectionHelper.Invoke(_mainStorageGetAmountMethod, mainStorage, goodName);
				return (int?)result ?? 0;
			} catch {
				return 0;
			}
		}

		// ========================================
		// HELPER METHODS
		// ========================================

		/// <summary>
		/// Get display name from a GoodRef object.
		/// </summary>
		internal static string GetGoodRefDisplayName(object goodRef) {
			if (goodRef == null) return null;

			try {
				// GoodRef has a DisplayName property: good.displayName.Text
				var displayName = goodRef.GetType().GetProperty("DisplayName", GameReflection.PublicInstance)?.GetValue(goodRef) as string;
				if (!string.IsNullOrEmpty(displayName))
					return displayName;

				// Fallback: get the GoodModel and look up via Settings
				var goodModel = goodRef.GetType().GetField("good", GameReflection.PublicInstance)?.GetValue(goodRef);
				if (goodModel == null) return null;

				var goodName = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance)?.GetValue(goodModel) as string;
				if (!string.IsNullOrEmpty(goodName))
					return GetGoodDisplayName(goodName);

				return null;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get goods from a BuildingStorage component.
		/// </summary>
		internal static Dictionary<string, int> GetBuildingStorageGoodsInternal(object storage) {
			var result = new Dictionary<string, int>();

			try {
				// BuildingStorage.Goods property
				EnsureStorageTypes();
				var goodsCollection = ReflectionHelper.GetProp(_storageGoodsProperty, storage);
				if (goodsCollection == null) return result;

				// BuildingGoodsCollection.goods property - use reflection to iterate
				// (direct cast to Dictionary<string, int> fails at runtime)
				var goodsDict = ReflectionHelper.GetField(_goodsCollectionGoodsField, goodsCollection);
				if (goodsDict == null) return result;

				// Iterate through the dictionary using reflection
				var keys = ReflectionHelper.IterateKeys(goodsDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					string goodName = key as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					int amount = ReflectionHelper.DictGetInt(goodsDict, key);
					if (amount > 0) {
						result[goodName] = amount;
					}
				}
			} catch {
				// Return empty dictionary on error
			}

			return result;
		}

		// ========================================
		// BUILDING DESTRUCTION
		// ========================================

		// Building destruction methods (cached)
		private static MethodInfo _canBeDestroyedMethod = null;
		private static MethodInfo _removeMethod = null;
		private static FieldInfo _deliveredGoodsField = null;  // BuildingState.deliveredGoods
		private static FieldInfo _deliveredGoodsGoodsField = null;  // LimitedGoodsCollection.goods (Dictionary<string, int>)
		private static FieldInfo _baseRefundRateField = null;  // BuildingModel.baseRefundRate
		private static MethodInfo _getBuildingRefundRateMethod = null;  // IEffectsService.GetBuildingRefundRate
		private static bool _destructionTypesCached = false;

		private static void EnsureDestructionTypes() {
			if (_destructionTypesCached) return;
			_destructionTypesCached = true;

			ReflectionHelper.InitCache("BuildingReflection.Destruction", assembly => {
				var buildingType = assembly.GetType("Eremite.Buildings.Building");
				if (buildingType != null) {
					_canBeDestroyedMethod = buildingType.GetMethod("CanBeDestroyed", GameReflection.PublicInstance);
					_removeMethod = buildingType.GetMethod("Remove", GameReflection.PublicInstance, null, new[] { typeof(bool) }, null);
				}

				// BuildingState.deliveredGoods field
				var buildingStateType = assembly.GetType("Eremite.Buildings.BuildingState");
				if (buildingStateType != null) {
					_deliveredGoodsField = buildingStateType.GetField("deliveredGoods", GameReflection.PublicInstance);
				}

				// LimitedGoodsCollection.goods field (Dictionary<string, int>)
				var limitedGoodsCollectionType = assembly.GetType("Eremite.LimitedGoodsCollection");
				if (limitedGoodsCollectionType != null) {
					_deliveredGoodsGoodsField = limitedGoodsCollectionType.GetField("goods", GameReflection.PublicInstance);
				}

				// BuildingModel.baseRefundRate field
				var buildingModelType = assembly.GetType("Eremite.Buildings.BuildingModel");
				if (buildingModelType != null) {
					_baseRefundRateField = buildingModelType.GetField("baseRefundRate", GameReflection.PublicInstance);
				}

				// IEffectsService.GetBuildingRefundRate method
				var effectsServiceType = assembly.GetType("Eremite.Services.IEffectsService");
				if (effectsServiceType != null) {
					_getBuildingRefundRateMethod = effectsServiceType.GetMethod("GetBuildingRefundRate", GameReflection.PublicInstance);
				}
			});
		}

		/// <summary>
		/// Check if a building can be destroyed.
		/// </summary>
		public static bool CanBeDestroyed(object building) {
			if (building == null) return false;

			EnsureDestructionTypes();

			try {
				return ReflectionHelper.InvokeBool(_canBeDestroyedMethod, building);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CanBeDestroyed failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Destroy a building with material refund.
		/// </summary>
		public static bool DestroyBuilding(object building) {
			if (building == null) return false;
			if (!CanBeDestroyed(building)) return false;

			EnsureDestructionTypes();

			try {
				// Remove(true) = refund materials
				ReflectionHelper.InvokeVoid(_removeMethod, building, true);
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] DestroyBuilding failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Get the materials that will be refunded when destroying a building.
		/// Returns a list of (displayName, amount) tuples.
		/// </summary>
		public static List<(string name, int amount)> GetDestructionRefund(object building) {
			var result = new List<(string name, int amount)>();
			if (building == null) return result;

			EnsureDestructionTypes();
			EnsureBuildingTypes();

			try {
				// Get BuildingState
				var state = ReflectionHelper.GetProp(_buildingStateProperty, building);
				if (state == null) return result;

				// Get BuildingModel for baseRefundRate
				var model = ReflectionHelper.GetProp(_buildingModelProperty, building);
				if (model == null) return result;

				// Get deliveredGoods from state
				var deliveredGoods = ReflectionHelper.GetField(_deliveredGoodsField, state);
				if (deliveredGoods == null) return result;

				// Get the goods dictionary from deliveredGoods
				var goodsDict = ReflectionHelper.GetField(_deliveredGoodsGoodsField, deliveredGoods);
				if (goodsDict == null) return result;

				// Get baseRefundRate from model
				float baseRefundRate = (float?)_baseRefundRateField?.GetValue(model) ?? 1f;

				// Get the actual refund rate from EffectsService
				float refundRate = baseRefundRate;
				var effectsService = GameReflection.GetEffectsService();
				if (effectsService != null && _getBuildingRefundRateMethod != null) {
					refundRate = (float?)_getBuildingRefundRateMethod.Invoke(effectsService, new object[] { baseRefundRate }) ?? baseRefundRate;
				}

				// Iterate through the goods dictionary using reflection
				var keys = ReflectionHelper.IterateKeys(goodsDict);
				if (keys == null) return result;

				foreach (var key in keys) {
					string goodName = key as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					int baseAmount = ReflectionHelper.DictGetInt(goodsDict, key);
					if (baseAmount <= 0) continue;

					// Calculate refunded amount (floor of baseAmount * refundRate)
					int refundAmount = (int)(baseAmount * refundRate);
					if (refundAmount <= 0) continue;

					// Get display name
					string displayName = GetGoodDisplayName(goodName) ?? goodName;
					result.Add((displayName, refundAmount));
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetDestructionRefund failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// PUBLIC API - BUILDING UPGRADES
		// ========================================

		/// <summary>
		/// Data structure for goods cost information.
		/// </summary>
		public struct GoodsCost {
			public string goodName;    // Internal name for storage lookup
			public string displayName; // Localized display name
			public int required;       // Amount needed
			public int available;      // Amount in warehouse
		}

		/// <summary>
		/// Data structure for upgrade perk information.
		/// </summary>
		public struct UpgradePerkInfo {
			public int perkIndex;
			public string displayName;
			public string description;
			public bool isChosen;      // This perk was selected for this level
		}

		/// <summary>
		/// Data structure for upgrade level information.
		/// </summary>
		public struct UpgradeLevelInfo {
			public int levelIndex;              // 0-based index
			public string levelName;            // "Level I", "Level II", etc.
			public bool isAchieved;             // Level already purchased
			public bool canAfford;              // Player has required goods
			public List<GoodsCost> requiredGoods;  // Cost items (first option from each GoodsSet)
			public List<UpgradePerkInfo> perks; // Available perk choices
		}

		/// <summary>
		/// Check if a building is an upgradable building and has upgrades available.
		/// </summary>
		public static bool HasUpgradesAvailable(object building) {
			if (building == null) return false;

			EnsureUpgradeTypes();

			try {
				// Check if it's an UpgradableBuilding
				if (_upgradableBuildingType == null ||
					!_upgradableBuildingType.IsAssignableFrom(building.GetType()))
					return false;

				// Check HasUpgrades property (includes AreUpgradesUnlockd check)
				return ReflectionHelper.GetPropBool(_hasUpgradesProperty, building);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get the current upgrade level of a building (0 = base, 1 = Level I purchased, etc.).
		/// </summary>
		public static int GetCurrentUpgradeLevel(object building) {
			if (building == null) return 0;

			EnsureUpgradeTypes();

			try {
				if (_upgradableBuildingType == null ||
					!_upgradableBuildingType.IsAssignableFrom(building.GetType()))
					return 0;

				var state = ReflectionHelper.GetProp(_upgradableStateProperty, building);
				if (state == null) return 0;

				return ReflectionHelper.GetInt(_upgradableStateLevelField, state);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get the total number of upgrade levels available for a building.
		/// </summary>
		public static int GetUpgradeLevelCount(object building) {
			if (building == null) return 0;

			EnsureUpgradeTypes();

			try {
				if (_upgradableBuildingType == null ||
					!_upgradableBuildingType.IsAssignableFrom(building.GetType()))
					return 0;

				var model = ReflectionHelper.GetProp(_upgradableModelProperty, building);
				if (model == null) return 0;

				var levels = _upgradableModelLevelsField?.GetValue(model) as Array;
				return levels?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Check if a specific perk was chosen for a level.
		/// </summary>
		public static bool IsPerkChosen(object building, int levelIndex, int perkIndex) {
			if (building == null) return false;

			EnsureUpgradeTypes();

			try {
				if (_upgradableBuildingType == null ||
					!_upgradableBuildingType.IsAssignableFrom(building.GetType()))
					return false;

				var state = ReflectionHelper.GetProp(_upgradableStateProperty, building);
				if (state == null) return false;

				// upgrades is bool[][] - jagged array
				var upgrades = ReflectionHelper.GetField(_upgradableStateUpgradesField, state);
				if (upgrades == null) return false;

				// Access as jagged array using reflection
				var outerArray = upgrades as Array;
				if (outerArray == null || levelIndex < 0 || levelIndex >= outerArray.Length)
					return false;

				var innerArray = outerArray.GetValue(levelIndex) as bool[];
				if (innerArray == null || perkIndex < 0 || perkIndex >= innerArray.Length)
					return false;

				return innerArray[perkIndex];
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Get detailed upgrade information for all levels of a building.
		/// </summary>
		public static List<UpgradeLevelInfo> GetUpgradeLevelsInfo(object building) {
			var result = new List<UpgradeLevelInfo>();

			if (building == null) return result;
			if (!HasUpgradesAvailable(building)) return result;

			EnsureUpgradeTypes();

			try {
				var model = ReflectionHelper.GetProp(_upgradableModelProperty, building);
				if (model == null) return result;

				var levels = _upgradableModelLevelsField?.GetValue(model) as Array;
				if (levels == null) return result;

				int currentLevel = GetCurrentUpgradeLevel(building);

				for (int i = 0; i < levels.Length; i++) {
					var levelModel = levels.GetValue(i);
					if (levelModel == null) continue;

					// Check perks first - skip levels with no perks (base level placeholders)
					var perksArray = _levelModelOptionsField?.GetValue(levelModel) as Array;
					int perkCount = perksArray?.Length ?? 0;
					if (perkCount == 0) {
						// This is a base level with no choices - skip it
						// But count it as achieved for subsequent level calculations
						continue;
					}

					var info = new UpgradeLevelInfo {
						levelIndex = i,
						levelName = GetRomanNumeral(i + 1),  // Level I, II, III, etc.
						isAchieved = currentLevel > i,
						requiredGoods = new List<GoodsCost>(),
						perks = new List<UpgradePerkInfo>()
					};

					// Get required goods (GoodsSet[] - each GoodsSet is an OR group)
					var requiredGoodsSets = _levelModelRequiredGoodsField?.GetValue(levelModel) as Array;
					if (requiredGoodsSets != null) {
						bool canAffordAll = true;
						foreach (var goodsSet in requiredGoodsSets) {
							if (goodsSet == null) continue;

							// Get goods from GoodsSet (GoodRef[])
							var goods = _goodsSetGoodsField?.GetValue(goodsSet) as Array;
							if (goods == null || goods.Length == 0) continue;

							// Take the first GoodRef as the primary option
							var firstGood = goods.GetValue(0);
							if (firstGood == null) continue;

							var cost = ParseGoodRef(firstGood);
							if (cost.HasValue) {
								info.requiredGoods.Add(cost.Value);
								if (cost.Value.available < cost.Value.required)
									canAffordAll = false;
							}
						}
						info.canAfford = canAffordAll;
					}

					// Get perk options (BuildingPerkModel[])
					var perks = _levelModelOptionsField?.GetValue(levelModel) as Array;
					if (perks != null) {
						for (int j = 0; j < perks.Length; j++) {
							var perk = perks.GetValue(j);
							if (perk == null) continue;

							var perkInfo = new UpgradePerkInfo {
								perkIndex = j,
								displayName = GetPerkDisplayName(perk),
								description = GetPerkDescription(perk, building),
								isChosen = IsPerkChosen(building, i, j)
							};
							info.perks.Add(perkInfo);
						}
					}

					result.Add(info);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetUpgradeLevelsInfo failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Parse a GoodRef into a GoodsCost structure.
		/// </summary>
		private static GoodsCost? ParseGoodRef(object goodRef) {
			if (goodRef == null) return null;

			try {
				// Get good field (GoodModel)
				var goodModel = goodRef.GetType().GetField("good", GameReflection.PublicInstance)?.GetValue(goodRef);
				if (goodModel == null) return null;

				// Get amount
				int amount = (int?)goodRef.GetType().GetField("amount", GameReflection.PublicInstance)?.GetValue(goodRef) ?? 0;

				// Get good name (internal ID)
				string goodName = goodModel.GetType().GetProperty("Name", GameReflection.PublicInstance)?.GetValue(goodModel) as string;
				if (string.IsNullOrEmpty(goodName)) return null;

				// Get display name
				string displayName = GetGoodDisplayName(goodName);
				if (string.IsNullOrEmpty(displayName)) {
					var displayNameField = goodModel.GetType().GetField("displayName", GameReflection.PublicInstance)?.GetValue(goodModel);
					displayName = GameReflection.GetLocaText(displayNameField) ?? goodName;
				}

				// Get available amount from warehouse
				int available = GetMainStorageAmount(goodName);

				return new GoodsCost {
					goodName = goodName,
					displayName = displayName,
					required = amount,
					available = available
				};
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get display name from a BuildingPerkModel.
		/// </summary>
		private static string GetPerkDisplayName(object perk) {
			if (perk == null) return Strings.Get("common.unknown");

			EnsureUpgradeTypes();

			try {
				// Use DisplayName property
				return _buildingPerkDisplayNameProp?.GetValue(perk) as string ?? Strings.Get("common.unknown");
			} catch {
				return Strings.Get("common.unknown");
			}
		}

		/// <summary>
		/// Get description from a BuildingPerkModel.
		/// </summary>
		private static string GetPerkDescription(object perk, object building) {
			if (perk == null) return "";

			try {
				// Try GetDescription method first (takes building for context)
				if (_buildingPerkGetDescMethod != null) {
					var desc = _buildingPerkGetDescMethod.Invoke(perk, new[] { building }) as string;
					if (!string.IsNullOrEmpty(desc)) return desc;
				}

				// Fall back to description field
				if (_buildingPerkDescField != null) {
					var descLoca = _buildingPerkDescField.GetValue(perk);
					return GameReflection.GetLocaText(descLoca) ?? "";
				}
			} catch {
				// Fall through
			}

			return "";
		}

		/// <summary>
		/// Purchase an upgrade for a building using the game's Upgrade method.
		/// Creates a Func<int, Good> delegate at runtime to pass to the game.
		/// </summary>
		/// <param name="building">The upgradable building.</param>
		/// <param name="levelIndex">The upgrade level index (0-based).</param>
		/// <param name="perkIndex">The perk index to choose for this level.</param>
		/// <returns>True if upgrade was purchased successfully.</returns>
		public static bool PurchaseUpgrade(object building, int levelIndex, int perkIndex) {
			if (building == null) return false;

			EnsureUpgradeTypes();

			try {
				if (_upgradableBuildingType == null ||
					!_upgradableBuildingType.IsAssignableFrom(building.GetType()))
					return false;

				// Get the required goods for this level to create the delegate
				var costs = GetRequiredGoodsForLevel(building, levelIndex);

				// Get the Good type from game assembly
				var goodType = GameReflection.GameAssembly?.GetType("Eremite.Model.Good");
				if (goodType == null) {
					Debug.LogError("[ATSAccessibility] PurchaseUpgrade: Could not find Good type");
					return false;
				}

				// Create the Func<int, Good> delegate type
				var funcType = typeof(Func<,>).MakeGenericType(typeof(int), goodType);

				// Create the goodPicker delegate
				object goodPicker = CreateGoodPickerDelegate(costs, goodType, funcType);
				if (goodPicker == null) {
					Debug.LogError("[ATSAccessibility] PurchaseUpgrade: Failed to create goodPicker delegate");
					return false;
				}

				// Find the Upgrade method on UpgradableBuilding
				// Signature: void Upgrade(int level, int upgradeIndex, Func<int, Good> goodPicker)
				var upgradeMethod = _upgradableBuildingType.GetMethod("Upgrade",
					new[] { typeof(int), typeof(int), funcType });

				if (upgradeMethod == null) {
					Debug.LogError("[ATSAccessibility] PurchaseUpgrade: Could not find Upgrade method");
					return false;
				}

				// Call Upgrade(levelIndex, perkIndex, goodPicker)
				upgradeMethod.Invoke(building, new object[] { levelIndex, perkIndex, goodPicker });

				Debug.Log($"[ATSAccessibility] PurchaseUpgrade: Successfully purchased upgrade level {levelIndex} perk {perkIndex}");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PurchaseUpgrade failed: {ex.Message}");
				if (ex.InnerException != null) {
					Debug.LogError($"[ATSAccessibility] Inner exception: {ex.InnerException.Message}");
				}
				return false;
			}
		}

		/// <summary>
		/// Create a Func<int, Good> delegate that returns the appropriate Good for each cost index.
		/// Uses Expression.Lambda to create the delegate at runtime with the correct game type.
		/// </summary>
		private static object CreateGoodPickerDelegate(List<GoodsCost> costs, Type goodType, Type funcType) {
			try {
				// Find the Good constructor: Good(string name, int amount)
				var goodConstructor = goodType.GetConstructor(new[] { typeof(string), typeof(int) });
				if (goodConstructor == null) {
					Debug.LogError("[ATSAccessibility] CreateGoodPickerDelegate: Could not find Good constructor");
					return null;
				}

				// Prepare the goods data arrays
				var goodNames = costs.Select(c => c.goodName).ToArray();
				var amounts = costs.Select(c => c.required).ToArray();

				// Build expression: (int index) => new Good(goodNames[index], amounts[index])
				var indexParam = System.Linq.Expressions.Expression.Parameter(typeof(int), "index");

				// Create constants for the arrays
				var goodNamesConst = System.Linq.Expressions.Expression.Constant(goodNames);
				var amountsConst = System.Linq.Expressions.Expression.Constant(amounts);

				// Array access expressions
				var nameAccess = System.Linq.Expressions.Expression.ArrayIndex(goodNamesConst, indexParam);
				var amountAccess = System.Linq.Expressions.Expression.ArrayIndex(amountsConst, indexParam);

				// New Good(name, amount) expression
				var newGood = System.Linq.Expressions.Expression.New(goodConstructor, nameAccess, amountAccess);

				// Create and compile the lambda
				var lambda = System.Linq.Expressions.Expression.Lambda(funcType, newGood, indexParam);
				var compiledDelegate = lambda.Compile();

				Debug.Log($"[ATSAccessibility] CreateGoodPickerDelegate: Created delegate for {costs.Count} goods");
				return compiledDelegate;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreateGoodPickerDelegate failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Get the required goods for a specific upgrade level.
		/// Returns the first option from each GoodsSet (default behavior matching game UI).
		/// </summary>
		private static List<GoodsCost> GetRequiredGoodsForLevel(object building, int levelIndex) {
			var result = new List<GoodsCost>();

			try {
				var model = ReflectionHelper.GetProp(_upgradableModelProperty, building);
				if (model == null) return result;

				var levels = _upgradableModelLevelsField?.GetValue(model) as Array;
				if (levels == null || levelIndex < 0 || levelIndex >= levels.Length) return result;

				var levelModel = levels.GetValue(levelIndex);
				var requiredGoodsSets = _levelModelRequiredGoodsField?.GetValue(levelModel) as Array;
				if (requiredGoodsSets == null) return result;

				foreach (var goodsSet in requiredGoodsSets) {
					if (goodsSet == null) continue;
					var goods = _goodsSetGoodsField?.GetValue(goodsSet) as Array;
					if (goods == null || goods.Length == 0) continue;

					// Take first option from each GoodsSet (default behavior)
					var firstGood = goods.GetValue(0);
					var cost = ParseGoodRef(firstGood);
					if (cost.HasValue)
						result.Add(cost.Value);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRequiredGoodsForLevel failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Convert number to Roman numeral for level names.
		/// </summary>
		private static string GetRomanNumeral(int number) {
			switch (number) {
				case 1: return Strings.Get("reflection.building.level_1");
				case 2: return Strings.Get("reflection.building.level_2");
				case 3: return Strings.Get("reflection.building.level_3");
				case 4: return Strings.Get("reflection.building.level_4");
				case 5: return Strings.Get("reflection.building.level_5");
				default: return Strings.Get("reflection.building.level_n", number);
			}
		}


		// ========================================
		// FORWARDING PROPERTIES (delegated to ConstructionReflection)
		// ========================================

		/// <summary>Shared GoodRef type — forwarded to ConstructionReflection where EnsureBuildingModelFields lives.</summary>
		public static System.Type GoodRefType => ConstructionReflection.GoodRefType;
		/// <summary>Shared GoodRef.good field — forwarded to ConstructionReflection.</summary>
		public static System.Reflection.FieldInfo GoodRefGoodField => ConstructionReflection.GoodRefGoodField;
		/// <summary>Shared GoodRef.amount field — forwarded to ConstructionReflection.</summary>
		public static System.Reflection.FieldInfo GoodRefAmountField => ConstructionReflection.GoodRefAmountField;
		/// <summary>Shared GoodRef.DisplayName property — forwarded to ConstructionReflection.</summary>
		public static System.Reflection.PropertyInfo GoodRefDisplayNameProperty => ConstructionReflection.GoodRefDisplayNameProperty;

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(BuildingReflection), "BuildingReflection");
		}
	}
}
