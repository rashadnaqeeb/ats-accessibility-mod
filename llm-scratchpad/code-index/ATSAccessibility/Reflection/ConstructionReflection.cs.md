# ConstructionReflection.cs

Provides reflection-based access to building construction, placement, range info,
lake interaction, supply chain analysis, and building enumeration.
Split from BuildingReflection.cs for maintainability.

## class ConstructionReflection (static) (line 14)

### Fields (private static cached — organized by subsystem)

#### Building System (line 23)
- private static FieldInfo _settingsBuildingsField (line 23)
- private static FieldInfo _settingsBuildingCategoriesField (line 24)
- private static PropertyInfo _gsGameContentServiceProperty (line 25)
- private static PropertyInfo _gsConstructionServiceProperty (line 26)
- private static MethodInfo _gcsIsUnlockedMethod (line 27)
- private static MethodInfo _csCanConstructMethod (line 28)
- private static Type _buildingCreatorType (line 29)
- private static MethodInfo _bcCreateBuildingMethod (line 30)
- private static object _buildingCreatorInstance (line 31)
- private static bool _buildingSystemTypesCached (line 32)

#### BuildingModel fields (line 43)
- private static FieldInfo _bmCategoryField (line 43)
- private static FieldInfo _bmIsInShopField (line 44)
- private static FieldInfo _bmSizeField (line 45)
- private static FieldInfo _bmIsActiveField (line 46)
- private static PropertyInfo _bmDescriptionProperty (line 47)
- private static FieldInfo _bmDescriptionField (line 48)
- private static FieldInfo _bcmIsOnHUDField (line 49)
- private static FieldInfo _bmRequiredGoodsField (line 50)
- private static Type _goodRefType (line 51)
- private static FieldInfo _goodRefGoodField (line 52)
- private static FieldInfo _goodRefAmountField (line 53)
- private static PropertyInfo _goodRefDisplayNameProperty (line 54)
- private static bool _bmFieldsCached (line 55)

#### Building Placement (line 453)
- private static MethodInfo _csCanPlaceOnGridMethod (line 453)
- private static MethodInfo _csPlaceOnGridMethod (line 454)
- private static MethodInfo _csRemoveFromGridMethod (line 455)
- private static MethodInfo _buildingManualPlacingFinishedMethod (line 456)
- private static PropertyInfo _buildingFieldProperty (line 457)
- private static MethodInfo _buildingRemoveMethod (line 458)
- private static PropertyInfo _buildingRotationProperty (line 459)
- private static MethodInfo _buildingSetPositionMethod (line 460)
- private static MethodInfo _buildingRotateMethod (line 461)
- private static bool _buildingPlacementTypesCached (line 462)

#### Construction Progress (line 648)
- private static FieldInfo _buildingProgressField (line 648)
- private static FieldInfo _deliveredGoodsField (line 649) — BuildingState.deliveredGoods
- private static FieldInfo _constructionGoodsField (line 650) — goods dict on GoodsCollection base
- private static MethodInfo _csGetConstructionCostForMethod (line 651)
- private static FieldInfo _goodStructNameField (line 652)
- private static FieldInfo _goodStructAmountField (line 653)
- private static bool _constructionTypesCached (line 654)

#### Pick/Select Building (line 834)
- private static PropertyInfo _modeServiceProperty (line 834)
- private static PropertyInfo _destructionModeProperty (line 835)
- private static PropertyInfo _harvestModeProperty (line 836)
- private static MethodInfo _buildingPickMethod (line 837)
- private static bool _pickBuildingCached (line 838)

#### Meta Perk Unlocks (line 1486)
- private static PropertyInfo _mbMetaPerksServiceProp (line 1486)
- private static MethodInfo _areTradeRoutesEnabledMethod (line 1487)
- private static MethodInfo _isConsumptionControlEnabledMethod (line 1488)
- private static bool _metaPerksReflectionCached (line 1489)
- private static PropertyInfo _mbMetaStateServiceProp (line 1492)
- private static PropertyInfo _mssPerksProperty (line 1493)
- private static FieldInfo _perksReputationRewardsRerollEnabledField (line 1494)
- private static FieldInfo _perksBonusFarmAreaField (line 1495)
- private static bool _metaStateReflectionCached (line 1496)

#### Range Info Types (line 1644)
- private static Type _campModelType (line 1644)
- private static Type _gathererHutModelType (line 1645)
- private static Type _fishingHutModelType (line 1646)
- private static Type _hearthModelType (line 1647)
- private static Type _workshopModelType (line 1648)
- private static Type _farmModelType (line 1649)
- private static Type _farmfieldType (line 1650)
- private static bool _rangeInfoTypesCached (line 1651)
- private static FieldInfo _campRecipesField (line 1654)
- private static FieldInfo _campMaxDistanceField (line 1655)
- private static FieldInfo _gathererHutRecipesField (line 1656)
- private static FieldInfo _gathererHutMaxDistanceField (line 1657)
- private static FieldInfo _fishingHutRecipesField (line 1658)
- private static FieldInfo _fishingHutMaxDistanceField (line 1659)
- private static FieldInfo _hearthHubRangeField (line 1660)
- private static FieldInfo _campRecipeRefGoodField (line 1663)
- private static FieldInfo _gathererHutRecipeRefGoodField (line 1664)
- private static FieldInfo _fishingHutRecipeRefGoodField (line 1665)
- private static FieldInfo _goodRefNameField (line 1666)
- private static PropertyInfo _resourcesAvailableProperty (line 1669)
- private static PropertyInfo _depositsAvailableProperty (line 1670)
- private static PropertyInfo _lakesAvailableProperty (line 1671)
- private static PropertyInfo _effectsServiceProperty (line 1672)
- private static MethodInfo _effectsGetHearthRangeMethod (line 1673)

#### Building Enumeration (line 3243)
- private static PropertyInfo _allBuildingsProperty (line 3243)
- private static bool _allBuildingsPropertyCached (line 3244)
- private static PropertyInfo _brHearthsDictProperty (line 2378)

---

### Shared GoodRef Properties (public static) (line 61)
- GoodRefType { get } (line 61) — returns _goodRefType via EnsureBuildingModelFields
- GoodRefGoodField { get } (line 62)
- GoodRefAmountField { get } (line 63)
- GoodRefDisplayNameProperty { get } (line 64)

---

### Cache Initialization (private)

- `EnsureBuildingSystemTypes()` (line 66) — caches Settings.Buildings, GameContentService, ConstructionService, BuildingCreator
- `EnsureBuildingModelFields()` (line 131) — caches BuildingModel fields (category, size, isInShop, etc.), GoodRef fields, BuildingCategoryModel fields
- `EnsureBuildingPlacementTypes()` (line 464) — caches CanPlaceOnGrid, PlaceOnGrid, RemoveFromGrid, Building methods (SetPosition, Rotate, Remove, ManualPlacingFinished)
- `EnsureConstructionTypes()` (line 656) — caches BuildingState.buildingProgress, deliveredGoods, GoodsCollection.goods, ConstructionService.GetConstructionCostFor, Good struct fields
- `CachePickBuildingReflection()` (line 885) — caches ModeService, BuildingDestructionMode, HarvestMode from GameServices
- `EnsureMetaPerksReflectionCached()` (line 1498) — caches MB.MetaPerksService, AreTradeRoutesEnabled, IsConsumptionControlEnabled
- `EnsureMetaStateReflectionCached()` (line 1561) — caches MB.MetaStateService, MetaPerksState fields (rerollEnabled, bonusFarmArea)
- `EnsureRangeInfoTypes()` (line 1675) — caches all building model types (Camp, GathererHut, FishingHut, Hearth, Workshop, Farm), their recipe/distance fields, resource service properties, EffectsService

---

### Building System

- `ClearBuildingCreatorInstance()` (line 38) — clears cached BuildingCreator instance on scene change
- `GetAllBuildingModels() : Array` (line 189) — get all BuildingModel definitions from Settings
- `GetBuildingCategories() : Array` (line 204) — get all BuildingCategoryModel definitions from Settings
- `GetBuildingCategory(object buildingModel) : object` (line 219) — get category of a BuildingModel
- `IsBuildingInShop(object buildingModel) : bool` (line 233) — check if model shows in build menu; defaults true
- `GetBuildingSize(object buildingModel) : Vector2Int` (line 248) — get building size; defaults Vector2Int.one
- `GetBuildingModelDescription(object buildingModel) : string` (line 264) — tries Description property then description LocaText field
- `GetBuildingCosts(object buildingModel) : string` (line 289) — formatted construction costs ("2 Wood, 4 Planks"); uses ConstructionService.GetConstructionCostFor for rate-adjusted costs with "not enough" annotation, falls back to raw requiredGoods from model
- `IsBuildingActive(object buildingModel) : bool` (line 342) — defaults true
- `IsCategoryOnHUD(object categoryModel) : bool` (line 357) — defaults true
- `GetGameContentService() : object` (line 372)
- `GetConstructionService() : object` (line 387)
- `IsBuildingUnlocked(object buildingModel) : bool` (line 402) — via GameContentService.IsUnlocked
- `CanConstructBuilding(object buildingModel) : bool` (line 418) — via ConstructionService.CanConstruct (not at max)
- `CreateBuilding(object buildingModel, int rotation = 0) : object` (line 435) — creates building instance via BuildingCreator (not yet placed); caches and reuses BuildingCreator instance

### Placement

- `CanPlaceBuilding(object building) : bool` (line 514) — via ConstructionService.CanPlaceOnGrid
- `SetBuildingPosition(object building, Vector2Int gridPos)` (line 530) — converts grid to world position (x, 0, y) and calls SetPosition
- `RotateBuilding(object building, int rotation)` (line 546) — set rotation 0-3
- `GetBuildingRotation(object building) : int` (line 560) — returns 0 on failure
- `FinalizeBuildingPlacement(object building)` (line 575) — calls ManualPlacingFinished (registers, plays sounds, starts construction)
- `RemoveBuilding(object building, bool refund = true)` (line 589)
- `GetBuildingAtPosition(int x, int y) : object` (line 604) — uses GameReflection.GetObjectOn, checks Building type
- `IsBuildingUnfinished(object building) : bool` (line 620) — checks BuildingState.finished

### Construction Progress

- `GetBuildingProgress(object building) : float` (line 704) — 0-1 from BuildingState.buildingProgress
- `GetConstructionMaterials(object building) : List<(string name, int delivered, int required)>` (line 726) — uses ConstructionService.GetConstructionCostFor for required amounts, reads BuildingState.deliveredGoods.goods for delivered amounts

### Type Checks

- `IsBuilding(object obj) : bool` (line 783)
- `IsRemovableResource(object obj) : bool` (line 795) — true for ResourceDeposit, Lake, or Spring (not NaturalResource/Ore)
- `RemoveResourceNode(object resource) : bool` (line 805) — handles Spring.Remove(0f), ResourceDeposit.Remove(), Lake.Remove()

### Pick/Select Building

- `PickBuilding(object building) : bool` (line 845) — calls Building.Pick(); refuses if in destruction or harvest mode
- `IsInDestructionMode() : bool` (line 910) — checks ModeService.BuildingDestructionMode ReactiveProperty
- `IsInHarvestMode() : bool` (line 930) — checks ModeService.HarvestMode ReactiveProperty

### Building Properties

- `GetBuildingEntranceTile(object building) : Vector2Int?` (line 954) — Entrance property converted to tile coords
- `GetBuildingShouldShowEntrance(object building) : bool` (line 978) — protected virtual ShouldShowEntrance property
- `CanRotateBuilding(object building) : bool` (line 996) — checks instance's model canRotate field
- `CanRotateBuildingModel(object buildingModel) : bool` (line 1015) — checks canRotate field directly

### Move/Rotate Placed Buildings

- `CanMovePlacedBuilding(object building) : bool` (line 1033) — via ConstructionService.CanBeMoved; tries exact type then base Building type
- `HasMovingCost(object building) : bool` (line 1070)
- `CanAffordMove(object building) : bool` (line 1090) — defaults true on failure
- `GetMovingCostInfo(object building) : (string displayName, int amount)?` (line 1111) — reads BuildingModel.movingCost GoodRef
- `PayForMoving(object building) : bool` (line 1143) — converts GoodRef to Good, calls StorageService.Remove with StorageOperationType.BuildingMove
- `RefundMoving(object building)` (line 1188) — calls StorageService.Store with StorageOperationType.BuildingRefund
- `CanRotatePlacedBuilding(object building) : bool` (line 1226) — via ConstructionService.CanBeRotatedInPlace
- `RotatePlacedBuilding(object building) : int` (line 1254) — removes from grid, calls Rotate(), re-places; returns new rotation 0-3 or -1 on failure
- `RotatePlacedBuildingDirection(object building, int direction) : int` (line 1312) — directional rotation: removes from grid, computes (current + direction + 4) % 4, calls Rotate(int), re-places, plays sound; returns new rotation or -1
- `GetBuildingGridPosition(object building) : Vector2Int` (line 1376) — via Building.Field property
- `GetBuildingModel(object building) : object` (line 1403) — via Building.BuildingModel property
- `LiftBuilding(object building)` (line 1426) — MapService.RemoveFromGrid (clears footprint, keeps object)
- `PlaceBuildingOnGrid(object building)` (line 1455) — MapService.PlaceOnGrid (sets footprint at current position)

### Meta Perk Unlock Checks

- `AreTradeRoutesUnlocked() : bool` (line 1524) — via MetaPerksService; assumes unlocked on failure
- `IsConsumptionControlUnlocked() : bool` (line 1544) — via MetaPerksService; assumes unlocked on failure
- `IsBlueprintRerollUnlocked() : bool` (line 1596) — via MetaStateService.Perks.reputationRewardsRerollEnabled
- `GetBonusFarmArea() : int` (line 1619) — via MetaStateService.Perks.bonusFarmArea; returns 0 on failure

### Building Model Type Checks

- `IsCampModel(object buildingModel) : bool` (line 1777) — harvests from NaturalResources
- `IsGathererHutModel(object buildingModel) : bool` (line 1786) — harvests from ResourceDeposits
- `IsFishingHutModel(object buildingModel) : bool` (line 1795) — harvests from Lakes
- `IsHearthModel(object buildingModel) : bool` (line 1804)
- `IsWorkshopModel(object buildingModel) : bool` (line 1813)
- `IsFarmModel(object buildingModel) : bool` (line 1822)
- `HasFarmfieldAt(int x, int y) : bool` (line 1832) — iterates BuildingsService.Farmfields; checks IsFinished and state.field position
- `IsHouseModel(object buildingModel) : bool` (line 1893) — checks type name "HouseModel"
- `IsInstitutionModel(object buildingModel) : bool` (line 1902) — checks type name "InstitutionModel"
- `IsDecorationModel(object buildingModel) : bool` (line 1910) — checks type name "DecorationModel"

### Range Info

- `GetGatheringBuildingMaxDistance(object buildingModel) : float` (line 1919) — reads maxDistance field from Camp/GathererHut/FishingHut
- `GetHearthBaseRange(object buildingModel) : float` (line 1943) — reads hubRange; defaults 10.5f
- `GetEffectiveHearthRange(object buildingModel) : float` (line 1961) — applies EffectsService.GetHearthRange modifier to base range
- `GetGatheringBuildingGoodNames(object buildingModel) : List<string>` (line 1984) — extracts good names from Camp/GathererHut/FishingHut recipe arrays via refGood.Name property
- `GetAvailableResources() : object` (line 2028) — Dictionary<string, List<NaturalResource>> from ResourcesService
- `GetAvailableDeposits() : object` (line 2045) — Dictionary<string, List<ResourceDeposit>> from DepositsService
- `GetAvailableLakes() : object` (line 2062) — Dictionary<string, List<Lake>> from LakesService
- `GetResourceNodeDisplayName(object resource) : string` (line 2079) — gets model.displayName LocaText for NaturalResource/ResourceDeposit/Lake

### Lake Interaction

- `GetLakeChargesLeft(object lake) : int` (line 2114) — from State.chargesLeft
- `GetLakeStoredGoods(object lake) : List<(string name, int amount)>` (line 2133) — from State.goods.goods Dictionary<string,int>
- `ForceDepliteLake(object lake) : bool` (line 2168) — calls Lake.ForceDeplition(); note: typo matches game API spelling

### Priority Management

- `GetResourceNodePriority(object node) : int` (line 2186) — from State.prio on ResourceDeposit/Lake
- `SetResourceNodePriority(object node, int priority) : bool` (line 2209) — clamps -5/+5
- `SetGlobalResourceNodePriority(object node, int priority) : bool` (line 2235) — calls ChangeGlobalPriorityTo on DepositsService/LakesService
- `GetBuildingConstructionPriority(object building) : int` (line 2265) — from BuildingState.constructionPriority
- `SetBuildingConstructionPriority(object building, int priority) : bool` (line 2285) — via BuildingsService.ChangePriorityTo
- `SetGlobalBuildingConstructionPriority(object building, int priority) : bool` (line 2306) — via BuildingsService.ChangeGlobalPriorityTo

### Position/Size Helpers

- `GetBuildingCenter(object building) : Vector3?` (line 2327) — Building.Center property
- `GetResourceField(object resource) : Vector2Int?` (line 2345) — Field property of resource/deposit/lake
- `GetResourceSize(object resource) : Vector2Int?` (line 2363) — Size property; defaults Vector2Int.one

### Building Enumeration (by type from BuildingsService)

- `GetAllHearths() : IEnumerable` (line 2383) — from Hearths dictionary values
- `GetAllHouses() : IEnumerable` (line 2404) — from Houses dictionary values
- `GetAllInstitutions() : IEnumerable` (line 2421) — from Institutions dictionary values
- `GetAllDecorations() : IEnumerable` (line 2438) — from Decorations dictionary values
- `IsHouseBuilding(object building) : bool` (line 2455) — checks type name "House"
- `IsInHearthRange(object hearth, Vector2Int position) : bool` (line 2463) — via Hearth.IsInRange(Vector2Int)
- `IsInHearthRange(object hearth, object building) : bool` (line 2484) — tries IsInRange(Building), falls back to base type walk, then Field position
- `GetAllStorageBuildings() : IEnumerable` (line 2598) — from Storages dictionary values
- `GetAllFarms() : IEnumerable` (line 2615) — from Farms dictionary values
- `GetAllCamps() : IEnumerable` (line 2632) — from Camps dictionary values
- `GetAllGathererHuts() : IEnumerable` (line 2649) — from GathererHuts dictionary values
- `GetAllFishingHuts() : IEnumerable` (line 2666) — from FishingHuts dictionary values

### Distance Calculations

- `CalculateResourceDistance(Vector2 buildingCenter2D, Vector2Int resourceField) : float` (line 2534) — game formula: adjusts center by (-0.5, -0.5) then Vector2.Distance
- `CalculateDepositDistance(Vector2 buildingCenter2D, Vector2Int depositField, Vector2Int depositSize) : float` (line 2544) — finds minimum distance to any tile in multi-tile deposit/lake
- `CalculateBuildingCenter(int cursorX, int cursorY, Vector2Int size) : Vector2` (line 2564) — offset from cursor by half size

### Supply Chain Info

- `GetBuildingEntranceCenter(object building) : Vector2?` (line 2580) — Building.EntranceCenter property
- `GetLocalStorageDistance() : float` (line 2684) — from Settings.logisticConfig.maxLocalStorageDistance; defaults 6f
- `IsBuildingSourceOf(object building, string goodName) : bool` (line 2711) — calls Building.IsSourceOf(GoodModel) via Settings.GetGood lookup
- `GetBuildingRequiredInputs(object building) : List<string>` (line 2752) — traverses state.recipes[].ingredients[][] to collect allowed ingredient good names from active recipes; complex: handles 2D IngredientState array
- `GetModelPossibleInputs(object buildingModel) : List<string>` (line 2838) — traverses model.recipes[].requiredGoods[].goods[].good.Name; works on model (for build mode preview)
- `GetBuildingsThatProduce(string goodName) : List<object>` (line 2902) — iterates all finished buildings from BuildingsService, checks IsBuildingSourceOf
- `GetBuildingActualOutputs(object building) : List<string>` (line 2943) — dispatches to type-specific method based on building type name

#### Private Output Helpers

- `GetCampActualOutputs(object camp) : List<string>` (line 2975) — checks Camp recipes against AvailableResources within maxDistance
- `GetGathererHutActualOutputs(object hut) : List<string>` (line 3028) — checks recipes against AvailableDeposits within maxDistance; uses CalculateDepositDistance for multi-tile
- `GetFishingHutActualOutputs(object hut) : List<string>` (line 3079) — checks recipes against AvailableLakes within maxDistance
- `GetWorkshopActiveOutputs(object workshop) : List<string>` (line 3130) — reads state.recipes[].productName for active recipes
- `GetModelPossibleOutputs(object buildingModel) : List<string>` (line 3174) — reads producedGood.good.Name and refGood.good.Name from model recipes

### General Building Enumeration

- `GetAllBuildingObjects() : List<object>` (line 3252) — all buildings from BuildingsService.Buildings dictionary
- `GetBuildingPosition(object building) : Vector2Int` (line 3284) — via Field property; returns (-1,-1) on failure
- `GetBuildingDisplayName(object building) : string` (line 3304) — delegates to BuildingReflection.GetBuildingName

### Validation

- `LogCacheStatus() : int` (line 3309) — triggers ReflectionValidator for all cached fields
