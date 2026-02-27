# GameReflection.cs

Provides reflection-based access to game internals. Central reflection file used by all other reflection classes.

CRITICAL RULES:
- Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
- NEVER cache instance references (GameController, services, etc.) - they are destroyed on scene change
- All public methods return fresh values by querying through cached PropertyInfo

## class GameReflection (line 19)

### Constants
- public const BindingFlags PublicInstance (line 23)
  `BindingFlags.Public | BindingFlags.Instance`
- public const BindingFlags NonPublicInstance (line 24)
  `BindingFlags.NonPublic | BindingFlags.Instance`
- public const BindingFlags PublicStatic (line 25)
  `BindingFlags.Public | BindingFlags.Static`

### Nested Types
- public class ActionObserver<T>: IObserver<T> (line 1445)
  - public void OnNext(T value)
  - public void OnError(Exception error)
  - public void OnCompleted()

### Fields (private static cached — organized by subsystem)

#### Assembly / Core
- private static Assembly _gameAssembly (line 30)
- private static bool _assemblyCached (line 31)
- private static Type _gameControllerType (line 34)
- private static PropertyInfo _gcIsGameActiveProperty (line 35)
- private static Type _mainControllerType (line 38)
- private static PropertyInfo _mcInstanceProperty (line 39)
- private static PropertyInfo _mcAppServicesProperty (line 40)
- private static PropertyInfo _popupsServiceProperty (line 43)
- private static bool _typesInitialized (line 45)

#### LocaText
- private static PropertyInfo _locaTextTextProperty (line 83)

#### Tab system
- private static Type _tabsPanelType (line 50)
- private static Type _tabsButtonType (line 51)
- private static FieldInfo _tabsPanelButtonsField (line 52)
- private static FieldInfo _tabsPanelCurrentField (line 53)
- private static FieldInfo _tabsButtonButtonField (line 54)
- private static FieldInfo _tabsButtonContentField (line 55)
- private static bool _tabTypesCached (line 56)

#### ToggleButton
- private static Type _toggleButtonType (line 436)
- private static MethodInfo _toggleIsOnMethod (line 437)
- private static bool _toggleButtonTypeCached (line 438)

#### MetaController
- private static Type _metaControllerType (line 474)
- private static PropertyInfo _metaControllerInstanceProperty (line 475)
- private static PropertyInfo _mcMetaServicesProperty (line 476)
- private static bool _metaControllerTypesCached (line 477)

#### GameServices
- private static PropertyInfo _gcInstanceProperty (line 511)
- private static PropertyInfo _gcGameServicesProperty (line 512)
- private static PropertyInfo _gsReputationRewardsProperty (line 513)
- private static bool _gameServicesTypesCached (line 514)
- private static PropertyInfo _gcCameraControllerProperty (line 517)
- private static bool _cameraTypesCached (line 518)

#### GoodRef (shared by multiple reflection files)
- private static Type _goodRefType (line 2370)
- private static FieldInfo _goodRefGoodField (line 2371)
- private static FieldInfo _goodRefAmountField (line 2372)
- private static PropertyInfo _goodRefDisplayNameProperty (line 2373)
- private static bool _bmFieldsCached (line 2374)

### Properties (public static, lazy-initialized)

#### Tab system
- public static Type TabsPanelType { get; } (line 427)
- public static FieldInfo TabsPanelButtonsField { get; } (line 428)
- public static FieldInfo TabsPanelCurrentField { get; } (line 429)
- public static FieldInfo TabsButtonButtonField { get; } (line 430)
- public static FieldInfo TabsButtonContentField { get; } (line 431)

#### ToggleButton
- public static Type ToggleButtonType { get; } (line 466)
- public static MethodInfo ToggleIsOnMethod { get; } (line 467)

#### GoodRef (shared cached fields for GoodRef struct)
- public static Type GoodRefType { get; } (line 2380)
- public static FieldInfo GoodRefGoodField { get; } (line 2381)
- public static FieldInfo GoodRefAmountField { get; } (line 2382)
- public static PropertyInfo GoodRefDisplayNameProperty { get; } (line 2383)

### Internal Members
- internal static Assembly GameAssembly { get; } (line 108)
  Lazy-initialized access to Assembly-CSharp.
- internal static bool TryInvokeBoolInternal(MethodInfo method, object instance, object[] args = null) (line 115)
- internal static void EnsureMetaControllerTypesInternal() (line 122)
- internal static PropertyInfo MetaControllerInstanceProperty { get; } (line 126)
- internal static PropertyInfo McMetaServicesProperty { get; } (line 133)

### Methods

#### Core service access
- public static object GetService(PropertyInfo serviceProperty) (line 62)
  Get a service from GameServices by its cached PropertyInfo.
- public static object GetMetaService(PropertyInfo serviceProperty) (line 72)
  Get a service from MetaServices by its cached PropertyInfo.
- public static string GetLocaText(object locaText) (line 89)
  Extract the Text string from a LocaText object; caches the Text property info.
- public static object GetMetaServices() (line 144)
  Path: MetaController.Instance.MetaServices
- public static bool GetIsGameActive() (line 228)
  Reads static property GameController.IsGameActive.
- public static object GetMainControllerInstance() (line 244)
- public static object GetAppServices() (line 259)
- public static object GetPopupsService() (line 274)
- public static object GetTopActivePopup() (line 298)
  Returns index 0 of PopupsService.activePopups list.
- public static Type FindTypeByName(string typeName) (line 322)
  Scans assembly for type by short name (not full name).
- public static Type GetTypeByName(string fullTypeName) (line 338)
  Gets type by full name — more efficient than FindTypeByName.
- public static object GetSettings() (line 347)
  Via MB.Settings (protected static).
- public static void EnsureTabTypes() (line 388)
- public static void EnsureToggleButtonType() (line 444)

#### Game services
- public static object GetReputationRewardsService() (line 555)
- public static object GetGameServices() (line 762)
  Path: GameController.Instance.GameServices
- public static object GetMapService() (line 780)
- public static object GetGladesService() (line 788)
- public static object GetVillagersService() (line 796)
- public static object GetField(int x, int y) (line 805)
- public static object GetObjectOn(int x, int y) (line 814)
- public static object GetGlade(int x, int y) (line 823)
- public static object GetAllVillagers() (line 832)
- public static object GetResourcesService() (line 841)
- public static object GetDepositsService() (line 850)
- public static object GetOreService() (line 859)
- public static object GetSpringsService() (line 868)
- public static void RemoveSpringsFromGrid() (line 878)
- public static void ReturnSpringsOnGrid() (line 888)
- public static object GetLakesService() (line 898)
- public static object GetBuildingsService() (line 907)
- public static object GetBuildingById(int id) (line 920)
- public static object GetConditionsService() (line 947)
- public static object GetBiomeService() (line 955)
- public static object GetCurrentBiome() (line 964)
- public static bool IsBlightActive() (line 972)
- public static object GetBlightService() (line 987)
- public static int GetGlobalActiveCysts() (line 996)
- public static float GetPredictedCorruptionPercentage() (line 1012)
- public static IEnumerable GetBuildingsBlights() (line 1028)
- public static object GetMainHearth() (line 1037)
- public static int GetBlightActiveCysts(object buildingBlight) (line 1052)
- public static object GetBlightOwner(object buildingBlight) (line 1066)
- public static float GetHearthCorruptionRate(object hearth) (line 1080)
- public static object GetAllGlades() (line 1095)
- public static int GetMapWidth() (line 1104)
- public static int GetMapHeight() (line 1128)
- public static bool MapInBounds(int x, int y) (line 1152)
- public static Vector2Int? GetMainHearthPosition() (line 1167)

#### Time / Speed
- public static object GetTimeScaleService() (line 1265)
- public static bool IsPaused() (line 1273)
- public static void SetSpeed(int speedIndex) (line 1285)
- public static void TogglePause() (line 1310)

#### Camera
- public static object GetCameraController() (line 1362)
- public static void SetCameraTarget(Transform target) (line 1383)

#### Observable subscriptions
- public static IDisposable SubscribeToObservable(object observable, Action<object> callback) (line 1398)
  General helper for subscribing to IObservable<T> via ActionObserver<T>.

#### Popup detection
- public static bool IsWikiPopup(object popup) (line 1477)
  Forwards to WikiReflection.IsWikiPopup.

#### Reputation / Races
- public static object GetReputationService() (line 1524)
- public static object GetHostilityService() (line 1540)
- public static object GetResolveService() (line 1556)
- public static bool IsFavored(string raceName) (line 1601)
- public static void FavorRace(string raceName) (line 1616)
- public static void StopFavoringRace() (line 1633)
- public static bool IsFavoringOnCooldown() (line 1650)
- public static float GetFavorCooldownLeft() (line 1665)
- public static object GetRacesService() (line 1681)

#### Calendar / Seasons
- public static object GetCalendarService() (line 1743)
- public static int GetYear() (line 1758)
- public static int GetSeason() (line 1773)
- public static float GetTimeTillNextSeason() (line 1792)
- public static float GetGameTime() (line 1843)

#### State / Seasonal effects
- public static object GetStateService() (line 1972)
- public static object GetSeasonalEffectsState() (line 1988)
- public static IDictionary GetSeasonalEffectsDictionary() (line 2004)
- public static object GetConditionsState() (line 2020)
- public static List<string> GetEarlyEffects() (line 2036)
- public static List<string> GetLateEffects() (line 2052)
- public static object GetSimpleSeasonalEffectModel(string name) (line 2067)
- public static object GetConditionalSeasonalEffectModel(string name) (line 2082)
- public static object GetEffectModel(string name) (line 2098)

#### Storage / Goods
- public static object GetStorageService() (line 2177)
- public static object GetMainStorage() (line 2192)
- public static Dictionary<string, int> GetAllStoredGoods() (line 2208)
- public static Array GetAllGoodModels() (line 2239)
- public static object GetGoodCategory(object goodModel) (line 2254)
- public static string GetDisplayName(object model) (line 2270)
- public static string GetModelName(object model) (line 2294)
- public static int GetModelOrder(object model) (line 2309)
- public static bool IsGoodActive(object goodModel) (line 2325)

#### Building construction system
- public static void ClearBuildingCreatorInstance() (line 2357)
- public static Array GetAllBuildingModels() (line 2509)
- public static Array GetBuildingCategories() (line 2524)
- public static object GetBuildingCategory(object buildingModel) (line 2539)
- public static bool IsBuildingInShop(object buildingModel) (line 2553)
- public static Vector2Int GetBuildingSize(object buildingModel) (line 2568)
- public static string GetBuildingDescription(object buildingModel) (line 2583)
- public static List<(string name, int required, int available)> GetBuildingCosts(object buildingModel) (line 2608)
- public static bool IsBuildingActive(object buildingModel) (line 2661)
- public static bool IsCategoryOnHUD(object categoryModel) (line 2676)
- public static object GetGameContentService() (line 2691)
- public static object GetConstructionService() (line 2706)
- public static bool IsBuildingUnlocked(object buildingModel) (line 2721)
- public static bool CanConstructBuilding(object buildingModel) (line 2737)
- public static object CreateBuilding(object buildingModel, int rotation = 0) (line 2754)
- public static bool CanPlaceBuilding(object building) (line 2832)
- public static void SetBuildingPosition(object building, Vector2Int gridPos) (line 2848)
- public static void RotateBuilding(object building, int rotation) (line 2864)
- public static int GetBuildingRotation(object building) (line 2878)
- public static void FinalizeBuildingPlacement(object building) (line 2893)
- public static void RemoveBuilding(object building, bool refund = true) (line 2907)
- public static object GetBuildingAtPosition(int x, int y) (line 2922)
- public static bool IsBuildingUnfinished(object building) (line 2938)
- public static float GetBuildingProgress(object building) (line 3021)
- public static List<(string name, int delivered, int required)> GetConstructionMaterials(object building) (line 3043)
- public static bool IsBuilding(object obj) (line 3100)
- public static bool IsRemovableResource(object obj) (line 3112)
- public static void RemoveResourceNode(object resource) (line 3122)
- public static bool IsRelic(object building) (line 3155)
- public static void PickBuilding(object building) (line 3187)
- public static Vector2Int? GetBuildingEntranceTile(object building) (line 3296)
- public static bool GetBuildingShouldShowEntrance(object building) (line 3320)
- public static bool CanRotateBuilding(object building) (line 3338)
- public static bool CanRotateBuildingModel(object buildingModel) (line 3357)
- public static bool CanMovePlacedBuilding(object building) (line 3375)
- public static bool HasMovingCost(object building) (line 3412)
- public static bool CanAffordMove(object building) (line 3432)
- public static (string displayName, int amount)? GetMovingCostInfo(object building) (line 3453)
- public static void PayForMoving(object building) (line 3485)
- public static void RefundMoving(object building) (line 3530)
- public static bool CanRotatePlacedBuilding(object building) (line 3568)
- public static int RotatePlacedBuilding(object building) (line 3596)
  Returns new rotation int.
- public static int RotatePlacedBuildingDirection(object building, int direction) (line 3654)
  Returns new rotation int.
- public static Vector2Int GetBuildingGridPosition(object building) (line 3718)
- public static object GetBuildingModel(object building) (line 3745)
- public static void LiftBuilding(object building) (line 3768)
- public static void PlaceBuildingOnGrid(object building) (line 3797)

#### Blackboard / HUD
- public static object GetGameBlackboardService() (line 3857)
- public static object GetUnitDefault() (line 3877)
- public static void InvokeSubjectOnNext(object blackboardService, string subjectPropertyName, object parameter) (line 3921)
- public static void OpenRecipesPopup() (line 3956)
- public static void OpenOrdersPopup() (line 3989)
- public static void OpenTradeRoutesPopup() (line 4002)
- public static void OpenConsumptionPopup() (line 4015)
- public static void OpenPaymentsPopup() (line 4028)
- public static void OpenTrendsPopup() (line 4041)
- public static bool AreTradeRoutesUnlocked() (line 4094)
- public static bool IsConsumptionControlUnlocked() (line 4114)
- public static bool IsBlueprintRerollUnlocked() (line 4166)
- public static int GetBonusFarmArea() (line 4189)
- public static void OpenTraderPanel() (line 4212)
- public static string GetGoodDisplayName(string goodName) (line 4319)
- public static string GetGoodDescription(string goodName) (line 4343)
- public static Dictionary<string, int> GetStorageGoods() (line 4369)
- public static string GetRelicDisplayName(string relicModelName) (line 4402)
- public static string GetMetaCurrencyDisplayName(string currencyName) (line 4459)

#### Effects / Cornerstones / Perks
- public static object GetEffectsService() (line 4554)
- public static object GetPerksService() (line 4569)
- public static object GetCornerstonesState() (line 4584)
- public static IEnumerable GetAllConditions() (line 4601)
- public static List<string> GetActiveCornerstones() (line 4618)
- public static IList GetSortedPerks() (line 4635)
- public static (string name, int stacks, bool hidden) GetPerkInfo(object perkState) (line 4674)
- public static bool GetEffectIsPerk(object effectModel) (line 4713)
- public static string GetEffectName(object effectModel) (line 4728)
- public static object GetSeasonalEffectWrappedEffect(object seasonalEffectModel) (line 4744)
- public static string GetSeasonalEffectWrappedEffectName(object seasonalEffectModel) (line 4763)
- public static int GetSeasonalEffectHostilityLevel(object seasonalEffectModel) (line 4773)

#### Building model type checks
- public static bool IsCampModel(object buildingModel) (line 4924)
- public static bool IsGathererHutModel(object buildingModel) (line 4933)
- public static bool IsFishingHutModel(object buildingModel) (line 4942)
- public static bool IsHearthModel(object buildingModel) (line 4951)
- public static bool IsWorkshopModel(object buildingModel) (line 4960)
- public static bool IsFarmModel(object buildingModel) (line 4969)
- public static bool IsFarmfield(object obj) (line 4978)
- public static bool HasFarmfieldAt(int x, int y) (line 4988)
- public static bool IsHouseModel(object buildingModel) (line 5049)
- public static bool IsInstitutionModel(object buildingModel) (line 5058)
- public static bool IsDecorationModel(object buildingModel) (line 5066)

#### Building model stats
- public static float GetGatheringBuildingMaxDistance(object buildingModel) (line 5075)
- public static float GetHearthBaseRange(object buildingModel) (line 5099)
- public static float GetEffectiveHearthRange(object buildingModel) (line 5117)
- public static List<string> GetGatheringBuildingGoodNames(object buildingModel) (line 5140)

#### Resources / Deposits / Lakes
- public static IEnumerable GetAvailableResources() (line 5184)
- public static IEnumerable GetAvailableDeposits() (line 5201)
- public static IEnumerable GetAvailableLakes() (line 5218)
- public static string GetResourceNodeDisplayName(object resource) (line 5235)
- public static int GetLakeChargesLeft(object lake) (line 5270)
- public static List<(string name, int amount)> GetLakeStoredGoods(object lake) (line 5289)
- public static void ForceDepliteLake(object lake) (line 5324)
- public static int GetResourceNodePriority(object node) (line 5342)
- public static void SetResourceNodePriority(object node, int priority) (line 5365)
- public static void SetGlobalResourceNodePriority(object node, int priority) (line 5391)

#### Construction priorities
- public static int GetBuildingConstructionPriority(object building) (line 5421)
- public static void SetBuildingConstructionPriority(object building, int priority) (line 5441)
- public static void SetGlobalBuildingConstructionPriority(object building, int priority) (line 5462)

#### Geometry helpers
- public static Vector3? GetBuildingCenter(object building) (line 5483)
- public static Vector2Int? GetResourceField(object resource) (line 5501)
- public static Vector2Int? GetResourceSize(object resource) (line 5519)
- public static IEnumerable GetAllHearths() (line 5537)
- public static IEnumerable GetAllHouses() (line 5559)
- public static IEnumerable GetAllInstitutions() (line 5576)
- public static IEnumerable GetAllDecorations() (line 5593)
- public static bool IsHouseBuilding(object building) (line 5610)
- public static bool IsInHearthRange(object hearth, Vector2Int position) (line 5618)
- public static bool IsInHearthRange(object hearth, object building) (line 5639)
- public static float CalculateResourceDistance(Vector2 buildingCenter2D, Vector2Int resourceField) (line 5689)
- public static float CalculateDepositDistance(Vector2 buildingCenter2D, Vector2Int depositField, Vector2Int depositSize) (line 5699)
- public static Vector2 CalculateBuildingCenter(int cursorX, int cursorY, Vector2Int size) (line 5719)
- public static Vector2? GetBuildingEntranceCenter(object building) (line 5734)
- public static IEnumerable GetAllStorageBuildings() (line 5752)
- public static IEnumerable GetAllFarms() (line 5769)
- public static IEnumerable GetAllCamps() (line 5786)
- public static IEnumerable GetAllGathererHuts() (line 5803)
- public static IEnumerable GetAllFishingHuts() (line 5820)
- public static float GetLocalStorageDistance() (line 5838)

#### Building supply chain analysis
- public static bool IsBuildingSourceOf(object building, string goodName) (line 5865)
- public static List<string> GetBuildingRequiredInputs(object building) (line 5906)
- public static List<string> GetModelPossibleInputs(object buildingModel) (line 5992)
- public static List<object> GetBuildingsThatProduce(string goodName) (line 6056)
- public static bool IsProductionBuilding(object building) (line 6095)
- public static List<string> GetBuildingActualOutputs(object building) (line 6109)

#### Effects state
- public static object GetEffectsState() (line 6461)

#### Glade info
- public static bool HasGladeInfo() (line 6477)
- public static bool HasDangerousGladeInfo() (line 6495)
- public static List<Vector2Int> GetRevealedGrassLocations() (line 6512)
- public static List<Vector2Int> GetRevealedSpringsLocations() (line 6527)
- public static List<Vector2Int> GetRevealedRelicLocations() (line 6542)
- public static int GetLocationMarkerType(int x, int y) (line 6558)
- public static string GetGladeContentsSummary(object glade) (line 6607)

#### Location marker subscriptions
- public static IDisposable SubscribeToGrassLocationRequested(Action callback) (line 6693)
- public static IDisposable SubscribeToSpringsLocationRequested(Action callback) (line 6713)
- public static IDisposable SubscribeToRelicLocationRequested(Action callback) (line 6733)

#### Relics
- public static object GetRelicsService() (line 6800)
- public static IDisposable SubscribeToRelicsHighlightRequested(Action<string, Vector2Int> callback) (line 6816)
- public static Dictionary<Vector2Int, string> GetHighlightedRelics() (line 6863)
- public static string GetHighlightedRelicAt(int x, int y) (line 6871)
- public static void ClearHighlightedRelics() (line 6881)

#### Natural resource markers
- public static bool IsNaturalResourceMarked(object resource) (line 6911)
- public static object GetNaturalResourceAt(Vector2Int pos) (line 6969)
- public static bool HasNaturalResourceAt(Vector2Int pos) (line 6990)
- public static void MarkNaturalResourceAt(Vector2Int pos) (line 6998)
- public static void UnmarkNaturalResourceAt(Vector2Int pos) (line 7017)
- public static bool IsNaturalResourceGladeEdge(Vector2Int pos) (line 7035)
- public static List<Vector2Int> GetAllNaturalResourcePositions() (line 7056)

#### Farm / Field helpers
- public static Vector2Int GetFarmModelWorkArea(object farmModel) (line 7109)
- public static bool IsFieldGrass(object field) (line 7128)
- public static bool IsInUnrevealedGlade(int x, int y) (line 7148)

#### Seal
- public static object GetGameSealService() (line 7241)
- public static bool IsSealedBiome() (line 7249)
- public static Vector2Int GetSealField() (line 7260)
- public static IDictionary GetSeals() (line 7278)
- public static Vector2Int GetSealSize() (line 7295)
- public static Vector2Int GetGuidepostTargetField() (line 7320)

#### All buildings list
- public static List<object> GetAllBuildingObjects() (line 7353)
- public static Vector2Int GetBuildingPosition(object building) (line 7385)
- public static string GetBuildingDisplayName(object building) (line 7405)

#### Cache
- public static int LogCacheStatus() (line 7409)
