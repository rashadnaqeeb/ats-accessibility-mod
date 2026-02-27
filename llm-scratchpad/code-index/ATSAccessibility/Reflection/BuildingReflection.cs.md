# BuildingReflection.cs

Provides reflection-based access to building panel and building internals.
Follows same patterns as GameReflection.cs — cache reflection metadata, never cache instances.
Hearth, Relic, and Port detail access split into HearthReflection.cs, RelicReflection.cs, and PortReflection.cs respectively;
this file retains type-check methods (`IsHearth`, `IsRelic`, `IsPort`) and internal accessors used by those files.
Construction/placement/pick-move/meta-perks/range/lake/supply-chain/enumeration helpers split into ConstructionReflection.cs;
GoodRef forwarding properties delegate there.

## class BuildingReflection (line 12)

### Structs

#### GoodsCost (line 6370)
- public string goodName  — internal name for storage lookup
- public string displayName
- public int required
- public int available

#### UpgradePerkInfo (line 6380)
- public int perkIndex
- public string displayName
- public string description
- public bool isChosen

#### UpgradeLevelInfo (line 6390)
- public int levelIndex
- public string levelName  — "Level I", "Level II", etc.
- public bool isAchieved
- public bool canAfford
- public List<GoodsCost> requiredGoods
- public List<UpgradePerkInfo> perks

### Fields (private static cached — organized by subsystem)

#### Panel
- private static Type _buildingPanelType (line 18)
- private static FieldInfo _currentBuildingField (line 19)
- private static bool _panelTypesCached (line 20)

#### Building base
- private static PropertyInfo _buildingModelProperty (line 23)
- private static PropertyInfo _buildingStateProperty (line 24)
- private static PropertyInfo _buildingIdProperty (line 25)
- private static PropertyInfo _buildingDisplayNameProperty (line 26)
- private static MethodInfo _buildingIsFinishedMethod (line 27)
- private static bool _buildingTypesCached (line 28)

#### BuildingModel
- private static PropertyInfo _modelDescriptionProperty (line 31)
- private static bool _modelTypesCached (line 32)

#### BuildingState
- private static FieldInfo _stateFinishedField (line 35)
- private static FieldInfo _stateIsSleepingField (line 36)
- private static bool _stateTypesCached (line 37)

#### ProductionBuilding
- private static Type _productionBuildingType (line 40)
- private static PropertyInfo _workersProperty (line 41)
- private static PropertyInfo _productionStorageProperty (line 42)
- private static PropertyInfo _productionBuildingStateProperty (line 43)
- private static bool _productionTypesCached (line 44)

#### IWorkshop
- private static Type _workshopInterfaceType (line 47)
- private static PropertyInfo _workshopRecipesProperty (line 48)
- private static PropertyInfo _workshopIngredientsStorageProperty (line 49)
- private static MethodInfo _switchProductionOfMethod (line 50)
- private static bool _workshopTypesCached (line 51)

#### BuildingIngredientsStorage
- private static FieldInfo _ingredientsStorageGoodsField (line 54)
- private static FieldInfo _goodsCollectionGoodsField (line 55)  — GoodsCollection.goods (Dictionary<string,int>); shared with IngredientsStorage
- private static bool _ingredientsStorageTypesCached (line 56)

#### Camp
- private static Type _campType (line 59)
- private static FieldInfo _campStateField (line 60)
- private static FieldInfo _campStateRecipesField (line 61)
- private static MethodInfo _campSwitchProductionOfMethod (line 62)
- private static FieldInfo _campStateModeField (line 63)  — CampMode enum
- private static MethodInfo _campSetModeMethod (line 64)
- private static bool _campTypesCached (line 65)

#### Farm
- private static Type _farmType (line 68)
- private static FieldInfo _farmStateField (line 69)
- private static MethodInfo _farmCountSownFieldsMethod (line 70)
- private static MethodInfo _farmCountPlowedFieldsMethod (line 71)  — typo in game: CountPlownFieldsInRange
- private static MethodInfo _farmCountAllFieldsMethod (line 72)  — typo in game: CountAllReaveleadFieldsInRange
- private static MethodInfo _farmSwitchProductionOfMethod (line 73)
- private static bool _farmTypesCached (line 74)

#### Farmfield
- private static Type _farmfieldType (line 77)
- private static FieldInfo _farmfieldStateField (line 78)
- private static Type _farmfieldStateType (line 79)
- private static FieldInfo _farmfieldStatePlowedField (line 80)
- private static FieldInfo _farmfieldStatePlantField (line 81)  — FarmfieldPlantState
- private static Type _farmfieldPlantStateType (line 82)
- private static FieldInfo _farmfieldPlantRecipeField (line 83)  — string
- private static FieldInfo _farmfieldPlantGoodField (line 84)  — Good
- private static FieldInfo _farmfieldPlantMultiplierField (line 85)  — int
- private static bool _farmfieldTypesCached (line 86)

#### FishingHut
- private static Type _fishingHutType (line 89)
- private static FieldInfo _fishingHutStateField (line 90)
- private static FieldInfo _fishingHutModelField (line 91)
- private static FieldInfo _fishingHutStateBaitModeField (line 92)
- private static FieldInfo _fishingHutStateBaitChargesField (line 93)
- private static FieldInfo _fishingHutStateRecipesField (line 94)
- private static MethodInfo _fishingHutChangeModeMethod (line 95)
- private static MethodInfo _fishingHutSwitchProductionOfMethod (line 96)
- private static FieldInfo _fishingHutModelBaitIngredientField (line 97)
- private static bool _fishingHutTypesCached (line 98)

#### RecipeState
- private static FieldInfo _recipeActiveField (line 101)
- private static FieldInfo _recipeModelField (line 102)
- private static FieldInfo _recipePrioField (line 103)

#### WorkshopRecipeState
- private static FieldInfo _recipeLimitField (line 105)
- private static FieldInfo _isLimitLocalField (line 106)
- private static FieldInfo _recipeProductNameField (line 107)
- private static FieldInfo _recipeIngredientsField (line 108)  — IngredientState[][]
- private static bool _recipeTypesCached (line 109)

#### Recipe model info
- private static MethodInfo _settingsGetRecipeMethod (line 112)
- private static FieldInfo _recipeModelProductionTimeField (line 113)
- private static FieldInfo _recipeModelGradeField (line 114)
- private static FieldInfo _gradeModelLevelField (line 115)
- private static FieldInfo _recipeModelProducedGoodField (line 117)  — GoodRef
- private static FieldInfo _recipeGoodModelDisplayNameField (line 118)  — GoodModel.displayName (LocaText)
- private static bool _recipeModelTypesCached (line 119)

#### IngredientState
- private static FieldInfo _ingredientGoodField (line 122)
- private static FieldInfo _ingredientAllowedField (line 123)
- private static FieldInfo _ingredientPriorityField (line 124)
- private static FieldInfo _goodAmountField (line 126)  — Good.amount
- private static bool _ingredientTypesCached (line 127)

#### Building panel events
- private static PropertyInfo _onBuildingPanelShownProperty (line 130)
- private static PropertyInfo _onBuildingPanelClosedProperty (line 131)
- private static bool _eventTypesCached (line 132)

#### ActorsService
- private static PropertyInfo _actorsServiceProperty (line 135)
- private static MethodInfo _getActorMethod (line 136)
- private static bool _actorTypesCached (line 137)

#### Actor properties
- private static PropertyInfo _actorStateProperty (line 140)
- private static FieldInfo _villagerStateNameField (line 141)
- private static FieldInfo _villagerStateRaceField (line 142)
- private static MethodInfo _getTaskDescriptionMethod (line 143)
- private static bool _actorPropertiesCached (line 144)

#### VillagersService
- private static PropertyInfo _villagersServiceProperty (line 147)
- private static MethodInfo _getDefaultProfessionAmountMethod (line 148)
- private static MethodInfo _getDefaultProfessionVillagerMethod (line 149)
- private static MethodInfo _setProfessionMethod (line 150)
- private static MethodInfo _releaseFromProfessionMethod (line 151)
- private static MethodInfo _getVillagerMethod (line 152)
- private static PropertyInfo _villagersServiceRacesProperty (line 153)
- private static bool _villagersServiceTypesCached (line 154)

#### RacesService / race bonuses
- private static PropertyInfo _racesServiceRacesProperty (line 157)
- private static FieldInfo _raceModelCharacteristicsField (line 158)
- private static FieldInfo _raceModelPassiveEffectDescField (line 159)  — firekeeper effect
- private static FieldInfo _raceCharacteristicTagField (line 160)
- private static FieldInfo _raceCharacteristicEffectField (line 161)
- private static FieldInfo _raceCharacteristicGlobalEffectField (line 162)
- private static FieldInfo _raceCharacteristicBuildingPerkField (line 163)
- private static FieldInfo _buildingModelTagsField (line 164)
- private static FieldInfo _buildingTagDisplayNameField (line 165)
- private static FieldInfo _villagerPerkDisplayNameField (line 166)
- private static PropertyInfo _effectModelDisplayNameProperty (line 167)
- private static PropertyInfo _buildingPerkDisplayNameProperty (line 168)
- private static bool _raceBonusTypesCached (line 169)

#### ProductionBuilding (profession / workplaces)
- private static PropertyInfo _professionProperty (line 172)
- private static PropertyInfo _workplacesProperty (line 173)
- private static bool _professionTypesCached (line 174)

#### BuildingStorage (output goods)
- private static PropertyInfo _storageGoodsProperty (line 177)
- private static MethodInfo _storageGetDeliveryStateMethod (line 179)
- private static MethodInfo _storageSwitchForceDeliveryMethod (line 180)
- private static MethodInfo _storageSwitchConstantForceDeliveryMethod (line 181)
- private static FieldInfo _deliveryStateForcedField (line 182)
- private static FieldInfo _deliveryStateConstantForcedField (line 183)
- private static bool _storageTypesCached (line 184)

#### Hearth base type (for IsHearth routing only)
- private static Type _hearthType (line 187)
- private static bool _hearthTypesCached (line 188)

#### House
- private static Type _houseType (line 191)
- private static FieldInfo _houseStateField (line 192)
- private static FieldInfo _houseModelField (line 193)
- private static FieldInfo _houseStateResidentsField (line 194)
- private static MethodInfo _houseGetHousingPlacesMethod (line 195)
- private static MethodInfo _houseGetMaxHousingPlacesMethod (line 196)
- private static MethodInfo _houseIsFullMethod (line 197)
- private static bool _houseTypesCached (line 198)

#### Relic base type (for IsRelic routing only)
- private static Type _relicType (line 201)
- private static bool _relicTypesCached (line 202)

#### EffectModel description (shared with RelicReflection)
- private static PropertyInfo _effectModelDescriptionProperty (line 205)
- private static PropertyInfo _effectModelIsPositiveProperty (line 206)
- private static bool _effectDescriptionTypesCached (line 207)
- private static MethodInfo _goodsCollectionGetAmountMethod (line 210)  — GoodsCollection.GetAmount(string)

#### Port base type (for IsPort routing only)
- private static Type _portType (line 213)
- private static bool _portTypesCached (line 214)

#### Decoration
- private static Type _decorationType (line 217)
- private static bool _decorationTypesCached (line 218)

#### Storage building
- private static Type _storageType (line 221)
- private static bool _storageTypesCached2 (line 222)

#### Institution (Tavern, Temple, etc.)
- private static Type _institutionType (line 225)
- private static FieldInfo _institutionStateField (line 226)
- private static FieldInfo _institutionModelField (line 227)
- private static FieldInfo _institutionStorageField (line 228)
- private static FieldInfo _institutionStateRecipesField (line 229)
- private static FieldInfo _institutionModelRecipesField (line 230)
- private static FieldInfo _institutionRecipeStatePickedGoodField (line 231)
- private static FieldInfo _institutionRecipeModelServedNeedField (line 232)
- private static FieldInfo _institutionRecipeModelRequiredGoodsField (line 233)  — GoodsSet
- private static FieldInfo _institutionRecipeModelIsGoodConsumedField (line 234)
- private static MethodInfo _institutionChangeIngredientMethod (line 235)
- private static FieldInfo _institutionModelActiveEffectsField (line 236)  — InstitutionEffectModel[]
- private static FieldInfo _institutionEffectModelMinWorkersField (line 237)
- private static FieldInfo _institutionEffectModelEffectField (line 238)  — EffectModel
- private static bool _institutionTypesCached (line 239)

#### Shrine
- private static Type _shrineType (line 242)
- private static FieldInfo _shrineStateField (line 243)
- private static FieldInfo _shrineModelField (line 244)
- private static FieldInfo _shrineStateEffectsField (line 245)  — ShrineEffectsState[]
- private static FieldInfo _shrineModelEffectsField (line 246)  — ShrineEffectsModel[]
- private static FieldInfo _shrineEffectsStateChargesLeftField (line 247)
- private static FieldInfo _shrineEffectsModelLabelField (line 248)  — LocaText
- private static FieldInfo _shrineEffectsModelChargesField (line 249)
- private static FieldInfo _shrineEffectsModelEffectsField (line 250)  — EffectModel[]
- private static MethodInfo _shrineUseEffectMethod (line 251)
- private static FieldInfo _shrineModelChargingLoopField (line 252)  — SoundRef
- private static FieldInfo _shrineModelFinalSoundField (line 253)  — SoundRef
- private static bool _shrineTypesCached (line 254)

#### Poro
- private static Type _poroType (line 257)
- private static FieldInfo _poroStateField (line 258)
- private static FieldInfo _poroModelField (line 259)
- private static FieldInfo _poroStateNeedsField (line 260)  — PoroNeedState[]
- private static FieldInfo _poroModelNeedsField (line 261)  — PoroNeedModel[]
- private static FieldInfo _poroStateHappinessField (line 262)
- private static FieldInfo _poroStateProductionProgressField (line 263)
- private static FieldInfo _poroStateProductField (line 264)  — Good
- private static FieldInfo _poroModelProductField (line 265)  — GoodRef
- private static FieldInfo _poroModelMaxProductsField (line 266)
- private static FieldInfo _poroNeedStateLevelField (line 267)
- private static FieldInfo _poroNeedStatePickedGoodField (line 268)
- private static FieldInfo _poroNeedModelDisplayNameField (line 269)  — LocaText
- private static FieldInfo _poroNeedModelGoodsField (line 270)  — GoodsSet
- private static MethodInfo _poroCanFulfillMethod (line 271)
- private static MethodInfo _poroFulfillMethod (line 272)
- private static MethodInfo _poroCanGatherProductsMethod (line 273)
- private static MethodInfo _poroGatherProductsMethod (line 274)
- private static MethodInfo _poroGoodChangedMethod (line 275)
- private static MethodInfo _poroGetCurrentGoodForMethod (line 276)
- private static bool _poroTypesCached (line 277)

#### RainCatcher
- private static Type _rainCatcherType (line 280)
- private static FieldInfo _rainCatcherStateField (line 281)
- private static FieldInfo _rainCatcherModelField (line 282)
- private static MethodInfo _rainCatcherGetCurrentWaterTypeMethod (line 283)
- private static bool _rainCatcherTypesCached (line 284)

#### Extractor
- private static Type _extractorType (line 287)
- private static Type _extractorModelType (line 288)
- private static FieldInfo _extractorStateField (line 289)
- private static FieldInfo _extractorModelField (line 290)
- private static MethodInfo _extractorGetWaterTypeMethod (line 291)
- private static FieldInfo _extractorModelProductionTimeField (line 292)
- private static FieldInfo _extractorModelProducedAmountField (line 293)
- private static bool _extractorTypesCached (line 294)

#### Hydrant
- private static Type _hydrantType (line 297)
- private static FieldInfo _hydrantStateField (line 298)
- private static FieldInfo _hydrantModelField (line 299)
- private static bool _hydrantTypesCached (line 300)

#### WaterModel (RainCatcher / Extractor)
- private static FieldInfo _waterModelDisplayNameField (line 303)
- private static FieldInfo _waterModelGoodField (line 304)
- private static bool _waterModelTypesCached (line 305)

#### Cycle Abilities
- private static FieldInfo _condCycleAbilitiesField (line 308)  — ConditionsState.cycleAbilities
- private static FieldInfo _cycleAbilityModelField (line 309)  — string
- private static FieldInfo _cycleAbilityGameEffectField (line 310)  — string
- private static FieldInfo _cycleAbilityChargesField (line 311)  — int
- private static bool _cycleAbilityTypesCached (line 312)

#### GameModelService
- private static PropertyInfo _gsGameModelServiceProperty (line 315)
- private static MethodInfo _gmsGetEffectMethod (line 316)
- private static FieldInfo _effectModelDisplayNameField (line 317)  — LocaText (NonPublicInstance)
- private static MethodInfo _effectModelApplyMethod (line 318)
- private static MethodInfo _effectModelCanBeDrawnMethod (line 319)
- private static bool _gameModelServiceTypesCached (line 320)

#### BlightService
- private static PropertyInfo _gsBlightServiceProperty (line 323)
- private static MethodInfo _blightCountFreeCystsMethod (line 324)
- private static bool _blightServiceTypesCached (line 325)

#### Blight fuel config
- private static FieldInfo _settingsBlightConfigField (line 328)
- private static FieldInfo _blightConfigBlightPostFuelField (line 329)  — GoodRef
- private static PropertyInfo _goodRefNameProperty (line 330)
- private static bool _blightConfigTypesCached (line 331)

#### StorageService (for fuel amount)
- private static PropertyInfo _gsStorageService2Property (line 334)
- private static PropertyInfo _storageServiceMainProperty (line 335)
- private static MethodInfo _mainStorageGetAmountMethod (line 336)
- private static bool _storageService2TypesCached (line 337)

#### RainpunkService
- private static PropertyInfo _gsRainpunkServiceProperty (line 340)
- private static MethodInfo _rainpunkCountWaterLeftMethod (line 341)
- private static MethodInfo _rainpunkCountTanksCapacityMethod (line 342)
- private static MethodInfo _rainpunkGetWaterPerCystsMethod (line 343)
- private static MethodInfo _rainpunkIsWaterSpawningBlightMethod (line 344)
- private static FieldInfo _wsWaterUsedField (line 345)
- private static FieldInfo _engineModelWaterPerSecField (line 346)
- private static bool _rainpunkServiceTypesCached (line 347)

#### Rainpunk engine types
- private static Type _workshopType (line 350)
- private static Type _workshopStateType (line 351)
- private static Type _rainpunkEngineStateType (line 352)
- private static Type _rainpunkEngineModelType (line 353)
- private static Type _buildingRainpunkModelType (line 354)
- private static FieldInfo _workshopStateField (line 355)
- private static FieldInfo _wsRainpunkUnlockedField (line 356)
- private static FieldInfo _wsEnginesField (line 357)
- private static FieldInfo _workshopModelField (line 358)
- private static FieldInfo _wmRainpunkField (line 359)
- private static FieldInfo _brpEnginesField (line 360)
- private static FieldInfo _engineStateIndexField (line 361)
- private static FieldInfo _engineStateLevelField (line 362)
- private static FieldInfo _engineStateRequestedLevelField (line 363)
- private static FieldInfo _engineModelMaxLevelField (line 364)
- private static FieldInfo _engineModelLevelsField (line 365)  — RainpunkEngineLevel[]
- private static FieldInfo _engineLevelPerkField (line 366)
- private static PropertyInfo _buildingPerkDisplayNameProp (line 367)
- private static FieldInfo _engineModelUpSoundField (line 368)  — SoundRef
- private static FieldInfo _engineModelDownSoundField (line 369)  — SoundRef
- private static Type _soundRefType (line 370)
- private static MethodInfo _soundRefGetNextMethod (line 371)
- private static bool _rainpunkEngineTypesCached (line 372)

#### Building Upgrades (UpgradableBuilding)
- private static Type _upgradableBuildingType (line 375)
- private static Type _upgradableBuildingModelType (line 376)
- private static Type _upgradableBuildingStateType (line 377)
- private static Type _buildingLevelModelType (line 378)
- private static Type _goodsSetType (line 379)
- private static PropertyInfo _upgradableModelProperty (line 380)
- private static PropertyInfo _upgradableStateProperty (line 381)
- private static PropertyInfo _hasUpgradesProperty (line 382)
- private static FieldInfo _upgradableModelLevelsField (line 383)  — BuildingLevelModel[]
- private static FieldInfo _upgradableStateLevelField (line 384)
- private static FieldInfo _upgradableStateUpgradesField (line 385)  — bool[][]
- private static FieldInfo _levelModelRequiredGoodsField (line 386)  — GoodsSet[]
- private static FieldInfo _levelModelOptionsField (line 387)  — BuildingPerkModel[]
- private static FieldInfo _goodsSetGoodsField (line 388)  — GoodRef[]
- private static FieldInfo _buildingPerkDescField (line 389)
- private static MethodInfo _buildingPerkGetDescMethod (line 390)
- private static bool _upgradeTypesCached (line 391)

#### Building destruction (late-declared, line 6218)
- private static MethodInfo _canBeDestroyedMethod (line 6219)
- private static MethodInfo _removeMethod (line 6220)
- private static FieldInfo _deliveredGoodsField (line 6221)
- private static FieldInfo _deliveredGoodsGoodsField (line 6222)
- private static FieldInfo _baseRefundRateField (line 6223)
- private static MethodInfo _getBuildingRefundRateMethod (line 6224)
- private static bool _destructionTypesCached (line 6225)

### Methods (private — initialization)

- private static void EnsurePanelTypes() (line 397)
- internal static void EnsureBuildingTypes() (line 410)
- private static void EnsureModelTypes() (line 426)
- private static void EnsureStateTypes() (line 438)
- private static void EnsureProductionTypes() (line 451)
- private static void EnsureWorkshopTypes() (line 465)
- private static void EnsureCampTypes() (line 479)
- private static void EnsureFarmTypes() (line 499)
- private static void EnsureFarmfieldTypes() (line 515)
- private static void EnsureFishingHutTypes() (line 540)
- private static void EnsureRecipeTypes() (line 567)
- internal static void EnsureRecipeModelTypes() (line 591)
- private static void EnsureIngredientTypes() (line 629)
- private static void EnsureEventTypes() (line 649)
- private static void EnsureActorTypes() (line 662)
- private static void EnsureActorProperties() (line 679)
- private static void EnsureVillagersServiceTypes() (line 701)
- internal static void EnsureRaceBonusTypes() (line 731)
- private static void EnsureProfessionTypes() (line 791)
- private static void EnsureStorageTypes() (line 804)
- private static void EnsureIngredientsStorageTypes() (line 835)
- internal static void EnsureHearthBaseType() (line 854)  — caches only Hearth type; full hearth reflection in HearthReflection.cs
- internal static void EnsureHouseTypes() (line 868)
- internal static void EnsureRelicBaseType() (line 894)  — caches only Relic type; full relic reflection in RelicReflection.cs
- internal static void EnsureEffectDescriptionTypes() (line 907)
- internal static void EnsurePortBaseType() (line 952)  — caches only Port type; full port reflection in PortReflection.cs
- internal static void EnsureDecorationType() (line 965)
- private static void EnsureStorageType2() (line 974)
- internal static void EnsureInstitutionTypes() (line 983)
- private static void EnsureShrineTypes() (line 1028)
- private static void EnsurePoroTypes() (line 1074)
- private static void EnsureRainCatcherTypes() (line 1121)
- private static void EnsureExtractorTypes() (line 1136)
- private static void EnsureHydrantTypes() (line 1157)
- private static void EnsureWaterModelTypes() (line 1171)
- private static void EnsureCycleAbilityTypes() (line 1185)
- internal static void EnsureGameModelServiceTypes() (line 1207)
- private static void EnsureBlightServiceTypes() (line 1240)
- internal static void EnsureBlightConfigTypes() (line 1260)
- internal static void EnsureStorageService2Types() (line 1286)
- private static void EnsureRainpunkServiceTypes() (line 1324)
- private static void EnsureRainpunkEngineTypes() (line 1353)
- private static void EnsureUpgradeTypes() (line 1424)
- private static void EnsureDestructionTypes() (line 6227)

### Properties (internal — accessors for split reflection files)

- internal static Type HearthType (line 864)
- internal static FieldInfo HouseStateField (line 891)
- internal static FieldInfo HouseStateResidentsField (line 892)
- internal static Type RelicType (line 903)
- internal static PropertyInfo EffectModelDescriptionProperty (line 928)
- internal static PropertyInfo EffectModelIsPositiveProperty (line 932)
- internal static MethodInfo GoodsCollectionGetAmountMethod (line 936)
- internal static FieldInfo GoodsCollectionGoodsField (line 940)
- internal static MethodInfo SoundRefGetNextMethod (line 944)
- internal static FieldInfo GoodsSetGoodsField (line 948)
- internal static Type PortType (line 961)
- internal static PropertyInfo GoodRefNameProperty (line 1313)
- internal static PropertyInfo EffectModelDisplayNameProperty (line 1314)
- internal static MethodInfo BuildingIsFinishedMethod (line 1317)

### Properties (public — forwarding to ConstructionReflection, line 6828)

- public static Type GoodRefType (line 6833)  — forwards to ConstructionReflection.GoodRefType
- public static FieldInfo GoodRefGoodField (line 6835)  — forwards to ConstructionReflection.GoodRefGoodField
- public static FieldInfo GoodRefAmountField (line 6837)  — forwards to ConstructionReflection.GoodRefAmountField
- public static PropertyInfo GoodRefDisplayNameProperty (line 6839)  — forwards to ConstructionReflection.GoodRefDisplayNameProperty

### Methods (internal — for HearthReflection)

- internal static int GetMainStorageAmountInternal(string goodName) (line 1320)  — delegates to GetMainStorageAmount

### Methods (public — Panel state)

- public static object GetCurrentBuilding() (line 1481)
- public static bool IsBuildingPanelOpen() (line 1496)

### Methods (public — Building info)

- public static string GetBuildingName(object building) (line 1508)
- public static string GetBuildingDescription(object building) (line 1524)
- public static int GetBuildingId(object building) (line 1543)
- public static string GetBuildingTypeName(object building) (line 1558)
- public static bool IsBuildingFinished(object building) (line 1571)
- public static bool IsBuildingSleeping(object building) (line 1590)
- public static bool CanBuildingSleep(object building) (line 1611)
- public static bool SleepBuilding(object building) (line 1625)
- public static bool WakeUpBuilding(object building) (line 1642)
- public static bool ToggleBuildingSleep(object building) (line 1658)

### Methods (public — Building type checks)

- public static bool IsProductionBuilding(object building) (line 1671)
- public static bool IsWorkshop(object building) (line 1686)
- public static bool IsCamp(object building) (line 1699)
- public static int GetCampMode(object building) (line 1712)
- public static bool SetCampMode(object building, int mode) (line 1730)
- public static string[] GetCampModeNames() (line 1755)  — static strings for CampMode enum
- public static bool IsFarm(object building) (line 1770)
- public static int GetFarmSownFields(object building) (line 1783)
- public static int GetFarmPlowedFields(object building) (line 1798)
- public static int GetFarmTotalFields(object building) (line 1813)
- public static int GetFarmPlacedFieldsCount(object building) (line 1829)  — counts actual placed farmfield buildings in range; uses ConstructionReflection helpers
- public static bool IsFarmfield(object building) (line 1883)
- public static bool IsFarmfieldPlowed(object building) (line 1896)
- public static bool IsFarmfieldSeeded(object building) (line 1914)
- public static string GetFarmfieldCropName(object building) (line 1934)
- public static (string goodName, int amount)? GetFarmfieldExpectedYield(object building) (line 1994)
- public static bool IsFishingHut(object building) (line 2039)
- public static int GetFishingBaitMode(object building) (line 2053)  — 0=None, 1=Optional, 2=OnlyWithBait
- public static bool SetFishingBaitMode(object building, int mode) (line 2071)
- public static string[] GetFishingBaitModeNames() (line 2096)  — static strings for FishmanBaitMode enum
- public static int GetFishingBaitCharges(object building) (line 2109)
- public static string GetFishingBaitIngredient(object building) (line 2128)
- public static List<object> GetFishingHutRecipes(object building) (line 2151)
- public static bool ToggleFishingHutRecipe(object building, object recipeState) (line 2178)

### Methods (public — Workers)

- public static int[] GetWorkerIds(object building) (line 2198)
- public static int GetWorkerCount(object building) (line 2213)
- public static int GetMaxWorkers(object building) (line 2225)
- public static object GetActor(int actorId) (line 2232)
- public static string GetActorName(object actor) (line 2254)
- public static string GetActorRace(object actor) (line 2276)
- public static string GetActorTaskDescription(object actor) (line 2296)
- public static string GetWorkerDescription(int workerId) (line 2312)  — "Name, Race, Task"

### Methods (public — Worker assignment)

- public static List<(string raceName, int freeCount)> GetRacesWithFreeWorkers(bool includeZeroFree = false) (line 2349)
- public static int GetFreeWorkerCount(string raceName) (line 2393)
- public static (string bonus, string bonusType) GetRaceBonusWithType(object building, string raceName) (line 2496)
- public static string GetRaceBonusForBuilding(object building, string raceName) (line 2593)  — delegates to GetRaceBonusWithType
- public static string GetRaceBonusTypeForBuilding(object building, string raceName) (line 2602)  — delegates to GetRaceBonusWithType
- public static string GetRaceFirekeeperEffect(string raceName) (line 2611)
- public static bool AssignWorkerToSlot(object building, int slotIndex, string raceName) (line 2630)
- public static bool UnassignWorkerFromSlot(object building, int slotIndex) (line 2668)
- public static bool IsWorkerSlotEmpty(object building, int slotIndex) (line 2707)

### Methods (private — Worker assignment helpers)

- private static object GetVillagersService() (line 2338)
- private static object FindRaceModel(string raceName) (line 2413)
- private static (object characteristic, object tag) FindMatchingCharacteristic(object building, object raceModel) (line 2438)
- private static string GetBonusTypeFromCharacteristic(object characteristic) (line 2469)
- private static string GetBonusNameFromCharacteristic(object characteristic, object buildingTag) (line 2525)

### Methods (public — Recipes)

- public static List<object> GetRecipes(object building) (line 2721)  — handles IWorkshop, Camp, and Farm
- public static bool ToggleRecipe(object building, object recipeState) (line 2811)  — handles IWorkshop, Camp, and Farm
- public static bool IsRecipeActive(object recipeState) (line 2860)
- public static int GetRecipeLimit(object recipeState) (line 2875)  — -1 = unlimited
- public static bool IsRecipeLimitLocal(object recipeState) (line 2890)
- public static string GetRecipeModelName(object recipeState) (line 2905)
- public static string GetRecipeProductName(object recipeState) (line 2920)
- public static object GetRecipeModel(object recipeState) (line 2935)
- public static int GetRecipeGrade(object recipeState) (line 2957)
- public static float GetRecipeProductionTime(object recipeState) (line 2976)
- public static int GetRecipeProducedAmount(object recipeState) (line 2992)
- public static string GetRecipeProducedGoodDisplayName(object recipeState) (line 3011)

### Methods (public — Farm recipes)

- public static object GetFarmRecipeModel(object recipeState) (line 3039)
- public static string GetFarmRecipeProductDisplayName(object recipeState) (line 3067)
- public static int GetFarmRecipeProductAmount(object recipeState) (line 3094)
- public static int GetFarmRecipeGradeLevel(object recipeState) (line 3121)
- public static float GetFarmRecipePlantingTime(object recipeState) (line 3148)
- public static float GetFarmRecipeHarvestTime(object recipeState) (line 3165)
- public static float GetFarmPlantingRate(object farm) (line 3182)
- public static float GetFarmHarvestingRate(object farm) (line 3198)

### Methods (public — Ingredients)

- public static object GetRecipeIngredients(object recipeState) (line 3215)  — returns IngredientState[][] raw object
- public static int GetRecipeIngredientSlotCount(object recipeState) (line 3230)
- public static object[] GetIngredientSlotOptions(object recipeState, int slotIndex) (line 3239)  — options for one ingredient slot
- public static string GetIngredientGoodName(object ingredientState) (line 3261)
- public static int GetIngredientAmount(object ingredientState) (line 3281)
- public static bool IsIngredientAllowed(object ingredientState) (line 3299)
- public static void ToggleIngredientAllowed(object ingredientState) (line 3314)
- public static int GetIngredientPriority(object ingredientState) (line 3328)
- public static void SetIngredientPriority(object ingredientState, int priority) (line 3338)
- public static void SetRecipeLimit(object recipeState, int limit) (line 3354)
- public static void SetRecipeLimitFromGlobal(object recipeState, int limit) (line 3369)
- public static int GetRecipePriority(object recipeState) (line 3383)
- public static void SetRecipePriority(object recipeState, int priority) (line 3398)

### Methods (public — Storage / delivery)

- public static bool HasProductionStorage(object building) (line 3416)
- public static List<(string goodName, int amount)> GetProductionStorageGoods(object building) (line 3433)
- public static string GetGoodDisplayName(string goodName) (line 3475)
- public static bool HasIngredientsStorage(object building) (line 3499)
- public static List<(string goodName, int amount)> GetIngredientsStorageGoods(object building) (line 3522)
- public static (bool isForced, bool isConstantForced) GetOutputDeliveryState(object building, string goodName) (line 3568)
- public static bool ToggleForceDelivery(object building, string goodName) (line 3599)
- public static bool ToggleConstantDelivery(object building, string goodName) (line 3630)
- public static bool ReturnIngredientToWarehouse(object building, string goodName, int amount) (line 3660)

### Methods (public — Building type routing)

- public static bool IsHearth(object building) (line 3727)
- public static bool IsHouse(object building) (line 3743)
- public static List<int> GetHouseResidents(object building) (line 3756)
- public static int GetHouseCapacity(object building) (line 3783)
- public static int GetHouseMaxCapacity(object building) (line 3798)
- public static bool IsHouseFull(object building) (line 3813)
- public static bool IsRelic(object building) (line 3834)
- public static bool IsPort(object building) (line 3852)

### Methods (public — Event subscription)

- public static IDisposable SubscribeToBuildingPanelShown(Action<object> callback) (line 3870)
- public static IDisposable SubscribeToBuildingPanelClosed(Action<object> callback) (line 3891)

### Methods (public — Decoration / Storage building)

- public static bool IsDecoration(object building) (line 3915)
- public static bool IsStorage(object building) (line 3932)
- public static bool AreWorkplacesActive(object building) (line 3947)
- public static bool ShouldAllowWorkerManagement(object building) (line 3972)  — delegates to ConstructionReflection.IsBuildingUnfinished, PortReflection, RelicReflection

### Methods (public — Institution)

- public static bool IsInstitution(object building) (line 4005)
- public static int GetInstitutionRecipeCount(object building) (line 4018)
- public static string GetInstitutionServedNeedName(object building, int recipeIndex) (line 4037)
- public static bool IsInstitutionRecipeGoodConsumed(object building, int recipeIndex) (line 4063)
- public static string GetInstitutionCurrentGoodName(object building, int recipeIndex) (line 4085)
- public static int GetInstitutionAvailableGoodsCount(object building, int recipeIndex) (line 4122)
- public static string GetInstitutionAvailableGoodName(object building, int recipeIndex, int goodIndex) (line 4148)
- public static bool ChangeInstitutionIngredient(object building, int recipeIndex, int goodIndex) (line 4177)
- public static Dictionary<string, int> GetInstitutionStorageGoods(object building) (line 4201)
- public static int GetInstitutionEffectCount(object building) (line 4219)
- public static string GetInstitutionEffectName(object building, int effectIndex) (line 4238)
- public static int GetInstitutionEffectMinWorkers(object building, int effectIndex) (line 4264)
- public static string GetInstitutionEffectDescription(object building, int effectIndex) (line 4286)
- public static bool IsInstitutionEffectActive(object building, int effectIndex) (line 4312)

### Methods (public — Shrine)

- public static bool IsShrine(object building) (line 4333)
- public static int GetShrineEffectTierCount(object building) (line 4346)
- public static string GetShrineTierLabel(object building, int tierIndex) (line 4365)
- public static int GetShrineTierChargesLeft(object building, int tierIndex) (line 4388)
- public static int GetShrineTierMaxCharges(object building, int tierIndex) (line 4410)
- public static int GetShrineTierEffectCount(object building, int tierIndex) (line 4432)
- public static bool CanShrineTierEffectBeDrawn(object building, int tierIndex, int effectIndex) (line 4456)
- public static string GetShrineTierEffectName(object building, int tierIndex, int effectIndex) (line 4485)
- public static string GetShrineTierEffectDescription(object building, int tierIndex, int effectIndex) (line 4525)
- public static bool UseShrineEffect(object building, int tierIndex, int effectIndex) (line 4591)
- public static object GetShrineChargingLoopSound(object building) (line 4650)
- public static object GetShrineFinalSound(object building) (line 4660)

### Methods (private — Shrine helpers)

- private static object GetShrineSoundModel(object building, FieldInfo soundField) (line 4666)
- private static string ExtractSpeciesFromEffect(object effect, Type effectType, string description) (line 4552)

### Methods (public — Poro)

- public static bool IsPoro(object building) (line 4689)
- public static float GetPoroHappiness(object building) (line 4702)
- public static float GetPoroProductionProgress(object building) (line 4720)
- public static int GetPoroNeedCount(object building) (line 4738)
- public static string GetPoroNeedName(object building, int needIndex) (line 4757)
- public static float GetPoroNeedLevel(object building, int needIndex) (line 4780)
- public static string GetPoroNeedCurrentGoodName(object building, int needIndex) (line 4802)
- public static int GetPoroNeedAvailableGoodsCount(object building, int needIndex) (line 4837)
- public static string GetPoroNeedAvailableGoodName(object building, int needIndex, int goodIndex) (line 4863)
- public static bool CanFulfillPoroNeed(object building, int needIndex) (line 4892)
- public static bool FulfillPoroNeed(object building, int needIndex) (line 4920)
- public static bool ChangePoroNeedGood(object building, int needIndex, int goodIndex) (line 4952)
- public static string GetPoroProductName(object building) (line 4976)
- public static int GetPoroProductAmount(object building) (line 4995)
- public static int GetPoroMaxProducts(object building) (line 5016)
- public static bool CanGatherPoroProducts(object building) (line 5034)
- public static bool GatherPoroProducts(object building) (line 5050)

### Methods (public — Water buildings)

- public static bool IsRainCatcher(object building) (line 5074)
- public static string GetRainCatcherWaterTypeName(object building) (line 5087)
- public static bool IsExtractor(object building) (line 5110)
- public static bool IsExtractorModel(object buildingModel) (line 5123)
- public static string GetExtractorWaterTypeName(object building) (line 5136)
- public static float GetExtractorProductionTime(object building) (line 5155)
- public static int GetExtractorProducedAmount(object building) (line 5173)
- public static bool IsHydrant(object building) (line 5195)

### Methods (public — Cycle abilities)

- public static int GetCycleAbilityCount() (line 5227)
- public static string GetCycleAbilityName(int index) (line 5235)
- public static int GetCycleAbilityCharges(int index) (line 5265)
- public static string GetCycleAbilityDescription(int index) (line 5284)
- public static bool UseCycleAbility(int index) (line 5312)

### Methods (internal — Effect model lookup)

- internal static object GetEffectModel(string effectName) (line 5376)

### Methods (public — Blight / Hydrant)

- public static int GetBlightFreeCysts() (line 5399)
- public static int GetBlightFuelAmount() (line 5418)
- public static string GetBlightFuelName() (line 5441)

### Methods (private — Blight / Storage helpers)

- private static string GetBlightFuelNameInternal() (line 5463)
- private static object GetStorageServiceInternal() (line 5485)

### Methods (public — Water tank)

- public static int GetWaterTankCurrent(object building) (line 5505)
- public static int GetWaterTankCapacity(object building) (line 5527)

### Methods (private — Water helpers)

- private static object GetWaterModelFromBuilding(object building) (line 5549)

### Methods (public — Rainpunk water / blight progress)

- public static float GetTotalWaterUsePerSecond(object building) (line 5570)
- public static int GetBlightProgress(object building) (line 5601)

### Methods (public — Rainpunk engine)

- public static bool IsRainpunkEnabledGlobally() (line 5654)  — traverses MetaController.Instance.MetaServices.MetaPerksService
- public static bool HasRainpunkCapability(object building) (line 5721)
- public static bool IsRainpunkUnlocked(object building) (line 5742)
- public static int GetEngineCount(object building) (line 5759)
- public static int GetEngineCurrentLevel(object building, int engineIndex) (line 5777)
- public static int GetEngineRequestedLevel(object building, int engineIndex) (line 5791)
- public static int GetEngineMaxLevel(object building, int engineIndex) (line 5805)
- public static string GetEngineLevelEffect(object building, int engineIndex, int level) (line 5820)
- public static bool IncreaseEngineLevel(object building, int engineIndex) (line 5854)
- public static bool DecreaseEngineLevel(object building, int engineIndex) (line 5874)
- public static bool HasRunningEngines(object building) (line 5893)
- public static bool StopAllEngines(object building) (line 5921)
- public static void PlayEngineUpSound(object building, int engineIndex) (line 5991)
- public static void PlayEngineDownSound(object building, int engineIndex) (line 5998)
- public static (string goodName, string displayName, int amount)? GetRainpunkUnlockPrice(object building) (line 6042)
- public static bool CanAffordRainpunkUnlock(object building) (line 6077)
- public static bool UnlockRainpunk(object building) (line 6088)

### Methods (private — Rainpunk engine helpers)

- private static bool IsWorkshopClass(object building) (line 5643)
- private static object GetEngineState(object building, int engineIndex) (line 5948)
- private static object GetEngineModel(object building, int engineIndex) (line 5968)
- private static void PlayEngineSound(object building, int engineIndex, FieldInfo soundField) (line 6005)

### Methods (private/public — Stored goods)

- private static int GetMainStorageAmount(string goodName) (line 6109)
- public static int GetStoredGoodAmount(string goodName) (line 6130)  — main storage

### Methods (private — Helpers)

- private static string GetGoodRefDisplayName(object goodRef) (line 6154)
- private static Dictionary<string, int> GetBuildingStorageGoodsInternal(object storage) (line 6180)
- private static System.Collections.IList GetCycleAbilitiesList() (line 5212)

### Methods (public — Building destruction)

- public static bool CanBeDestroyed(object building) (line 6267)
- public static bool DestroyBuilding(object building) (line 6283)
- public static List<(string name, int amount)> GetDestructionRefund(object building) (line 6303)

### Methods (public — Upgrades)

- public static bool HasUpgradesAvailable(object building) (line 6402)
- public static int GetCurrentUpgradeLevel(object building) (line 6423)
- public static int GetUpgradeLevelCount(object building) (line 6445)
- public static bool IsPerkChosen(object building, int levelIndex, int perkIndex) (line 6468)
- public static List<UpgradeLevelInfo> GetUpgradeLevelsInfo(object building) (line 6503)
- public static bool PurchaseUpgrade(object building, int levelIndex, int perkIndex) (line 6680)  — creates Func<int,Good> delegate via Expression.Lambda

### Methods (private — Upgrade helpers)

- private static GoodsCost? ParseGoodRef(object goodRef) (line 6595)
- private static string GetPerkDisplayName(object perk) (line 6634)
- private static string GetPerkDescription(object perk, object building) (line 6650)
- private static object CreateGoodPickerDelegate(List<GoodsCost> costs, Type goodType, Type funcType) (line 6738)
- private static List<GoodsCost> GetRequiredGoodsForLevel(object building, int levelIndex) (line 6781)
- private static string GetRomanNumeral(int number) (line 6816)

### Methods (public — Diagnostics)

- public static int LogCacheStatus() (line 6841)
