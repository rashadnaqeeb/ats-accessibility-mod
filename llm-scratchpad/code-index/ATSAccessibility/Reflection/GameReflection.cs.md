# GameReflection.cs

Provides reflection-based access to game internals. Central reflection file used by all other reflection classes. After extractions to MapReflection.cs and BuildingReflection.cs, this file covers: core service access, tab/toggle systems, time/speed, camera, observable subscriptions, stats services, calendar, game time, mysteries/modifiers, goods/storage, blackboard/popup opening, goods helpers, and effects/cornerstones/perks.

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
  IObserver wrapper that calls an Action for each OnNext. Used by SubscribeToObservable.
  - public void OnNext(T value) (line 1452)
  - public void OnError(Exception error) (line 1460)
  - public void OnCompleted() (line 1464)

### Fields (private static cached -- organized by subsystem)

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

#### Map navigation (many service PropertyInfos and MethodInfos)
- Lines 582-614: _gsMapServiceProperty, _gsGladesServiceProperty, _gsVillagersServiceProperty, _mapGetFieldMethod, _mapGetObjectOnMethod, _gladesGetGladeMethod, _villagersVillagersProperty, _gsResourcesServiceProperty, _gsDepositsServiceProperty, _gsOreServiceProperty, _gsSpringsServiceProperty, _springsRemoveFromGridMethod, _springsReturnOnGridMethod, _gsLakesServiceProperty, _gsBuildingsServiceProperty, _gsConditionsServiceProperty, _conditionsIsBlightActiveMethod, _gsBlightServiceProperty, _blightGetGlobalActiveCystsMethod, _blightGetPredictedPercentageCorruptionMethod, _buildingsBlightsProperty, _buildingsGetMainHearthMethod, _buildingBlightGetActiveCystsMethod, _buildingBlightOwnerProperty, _hearthGetCorruptionRateMethod, _gsGladesProperty, _mapFieldsProperty, _mapWidthField, _mapHeightField, _mapInBoundsMethod, _gsBiomeServiceProperty, _biomeCurrentBiomeProperty, _mapTypesCached

#### Time scale
- Lines 1220-1224: _gsTimeScaleServiceProperty, _tssIsPausedMethod, _tssPauseMethod, _tssUnpauseMethod, _timeScaleTypesCached
- private static MethodInfo _tssChangeMethod (line 1280)

#### Calendar
- Lines 1697-1701: _gsCalendarServiceProperty, _calYearProperty, _calSeasonProperty, _calGetTimeTillNextSeasonMethod, _calendarTypesCached

#### Game time
- Lines 1808-1810: _gsGameTimeServiceProperty, _gameTimeTimeProperty, _gameTimeTypesCached

#### Mysteries/Modifiers (StateService)
- Lines 1861-1873: _gsStateServiceProperty, _ssSeasonalEffectsProperty, _seEffectsField, _ssConditionsProperty, _condEarlyEffectsField, _condLateEffectsField, _mysteriesTypesCached, _settingsGetSimpleSeasonalEffectMethod, _settingsGetConditionalSeasonalEffectMethod, _settingsGetEffectMethod, _settingsModelMethodsCached

#### Stats services
- Lines 1483-1487: _gsReputationServiceProperty, _gsHostilityServiceProperty, _gsResolveServiceProperty, _gsRacesServiceProperty, _statsServiceTypesCached

#### Favoring (race preference)
- Lines 1572-1577: _rsFavorRaceMethod, _rsStopFavoringMethod, _rsIsFavoredMethod, _rsIsFavoringOnCooldownMethod, _rsGetFavorCooldownLeftMethod, _favoringTypesCached

#### Goods/Storage
- Lines 2114-2119: _gsStorageServiceProperty, _ssGetStorageMethod, _storageGoodsProperty, _goodsCollectionGoodsField, _settingsGoodsField, _goodsTypesCached

#### GoodRef (forwarded to BuildingReflection)
- public static Type GoodRefType (line 2341) -- forwards to BuildingReflection.GoodRefType
- public static FieldInfo GoodRefGoodField (line 2342)
- public static FieldInfo GoodRefAmountField (line 2343)
- public static PropertyInfo GoodRefDisplayNameProperty (line 2344)

#### GameBlackboardService
- Lines 2351-2352: _gsGameBlackboardServiceProperty, _gameBlackboardTypesInitialized
- private static object _unitDefault (line 2395)
- private static bool _unitDefaultCached (line 2396)

#### Goods helpers
- Lines 2659-2660: _settingsGetGoodMethodCached, _settingsGetGoodCached
- Lines 2742-2743: _settingsGetRelicMethodCached, _settingsGetRelicCached
- Lines 2791-2793: _settingsGetMetaCurrencyMethodCached, _metaCurrencyModelDisplayNameProperty, _settingsGetMetaCurrencyCached

#### Modifiers panel (Effects, Cornerstones, Perks)
- Lines 2855-2861: _gsEffectsServiceProperty, _gsPerksServiceProperty, _esGetAllConditionsMethod, _psSortedPerksProperty, _ssCornerstonesProperty, _csActiveCornerstonesField, _modifiersPanelTypesCached
- Lines 3017-3020: _perkStateNameField, _perkStateStacksField, _perkStateHiddenField, _perkStateFieldsCached
- Lines 3057-3060: _effectModelIsPerkProperty, _effectModelNameProperty, _effectModelPropsCached

#### BuildingById
- Lines 912-914: _getBuildingByIdMethod, _hasBuildingByIdMethod, _getBuildingByIdCached

#### Hearth position
- Lines 1160-1161: _buildingFieldProperty, _hearthsDictProperty

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

### Internal Members
- internal static Assembly GameAssembly { get; } (line 108)
  Lazy-initialized access to Assembly-CSharp.
- internal static bool TryInvokeBoolInternal(MethodInfo, object, object[]) (line 115)
  Multi-arity InvokeBool dispatcher for WorldMapReflection.
- internal static void EnsureMetaControllerTypesInternal() (line 122)
- internal static PropertyInfo MetaControllerInstanceProperty { get; } (line 126)
- internal static PropertyInfo McMetaServicesProperty { get; } (line 133)

### Methods

#### Initialization (private)
- private static void EnsureAssembly() (line 162)
- private static void EnsureTypes() (line 180)
  Caches GameController and MainController types.
- private static void EnsureMetaControllerTypes() (line 479)
- private static void EnsureGameServicesTypes() (line 520)
  Caches GameController.Instance, GameServices, ReputationRewardsService.
- private static void EnsureMapTypes() (line 616)
  Caches ~30 service properties/methods for map, glades, villagers, blight, biome.
- private static void EnsureTimeScaleTypes() (line 1226)
- private static void EnsureCameraTypes() (line 1334)
- private static void EnsureStatsServiceTypes() (line 1489)
- private static void EnsureFavoringTypes() (line 1579)
- private static void EnsureCalendarTypes() (line 1703)
- private static void EnsureGameTimeTypes() (line 1812)
- private static void EnsureMysteriesTypes() (line 1875)
- private static void EnsureSettingsModelMethods() (line 1925)
- private static void EnsureGoodsTypes() (line 2121)
- private static void EnsureGameBlackboardTypes() (line 2354)
- private static void EnsureSettingsGetGood() (line 2662)
- private static void EnsureSettingsGetRelic() (line 2745)
- private static void EnsureSettingsGetMetaCurrency() (line 2795)
- private static void EnsureModifiersPanelTypes() (line 2863)
- private static void EnsurePerkStateFields(object firstPerk) (line 3022)
- private static void EnsureEffectModelProps(object effectModel) (line 3062)

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
  Scans assembly for type by short name (uses FirstOrDefault).
- public static Type GetTypeByName(string fullTypeName) (line 338)
  Gets type by full name -- more efficient than FindTypeByName.
- public static object GetSettings() (line 347)
  Via MB.Settings (protected static).

#### Tab system
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
- public static bool RemoveSpringsFromGrid() (line 878)
  Removes free springs from grid for Extractor placement checks.
- public static bool ReturnSpringsOnGrid() (line 888)
  Restores springs after placement checks.
- public static object GetLakesService() (line 898)
- public static object GetBuildingsService() (line 907)
- public static object GetBuildingById(int id) (line 920)
  Lazy-caches HasBuilding/GetBuilding methods from BuildingsService.
- public static object GetConditionsService() (line 947)
- public static object GetBiomeService() (line 955)
- public static object GetCurrentBiome() (line 964)

#### Blight
- public static bool IsBlightActive() (line 972)
- public static object GetBlightService() (line 987)
- public static int GetGlobalActiveCysts() (line 996)
- public static float GetPredictedCorruptionPercentage() (line 1012)
  Returns 0-1 float.
- public static object GetBuildingsBlights() (line 1028)
- public static object GetMainHearth() (line 1037)
- public static int GetBlightActiveCysts(object buildingBlight) (line 1052)
- public static object GetBlightOwner(object buildingBlight) (line 1066)
- public static float GetHearthCorruptionRate(object hearth) (line 1080)
  Returns 0-1 float.

#### Map geometry
- public static object GetAllGlades() (line 1095)
- public static int GetMapWidth() (line 1104)
  Fallback: 70.
- public static int GetMapHeight() (line 1128)
  Fallback: 70.
- public static bool MapInBounds(int x, int y) (line 1152)
- public static Vector2Int? GetMainHearthPosition() (line 1167)
  Gets first hearth from BuildingsService.Hearths dictionary.

#### Time / Speed
- public static object GetTimeScaleService() (line 1265)
- public static bool IsPaused() (line 1273)
- public static void SetSpeed(int speedIndex) (line 1285)
  1=normal, 2=1.5x, 3=2x, 4=3x. Uses Change(float, bool, bool).
- public static void TogglePause() (line 1310)

#### Camera
- public static object GetCameraController() (line 1362)
- public static void SetCameraTarget(Transform target) (line 1383)
  Uses CameraControllerUpdateMovementPatch.SetTarget for smooth panning.

#### Observable subscriptions
- public static IDisposable SubscribeToObservable(object observable, Action<object> callback) (line 1398)
  General helper for subscribing to IObservable<T> via ActionObserver<T>. Searches for Subscribe(IObserver<T>) method.

#### Popup detection
- public static bool IsWikiPopup(object popup) (line 1477)
  Forwards to WikiReflection.IsWikiPopup.

#### Stats services (Reputation, Hostility, Resolve, Races)
- public static object GetReputationService() (line 1524)
- public static object GetHostilityService() (line 1540)
- public static object GetResolveService() (line 1556)

#### Favoring (race preference)
- public static bool IsFavored(string raceName) (line 1601)
- public static bool FavorRace(string raceName) (line 1616)
- public static bool StopFavoringRace() (line 1633)
- public static bool IsFavoringOnCooldown() (line 1650)
- public static float GetFavorCooldownLeft() (line 1665)
- public static object GetRacesService() (line 1681)

#### Calendar / Seasons
- public static object GetCalendarService() (line 1743)
- public static int GetYear() (line 1758)
- public static int GetSeason() (line 1773)
  0=Drizzle, 1=Clearance, 2=Storm.
- public static float GetTimeTillNextSeason() (line 1792)

#### Game time
- public static float GetGameTime() (line 1843)
  In-game seconds since settlement start.

#### State / Seasonal effects (Mysteries)
- public static object GetStateService() (line 1972)
- public static object GetSeasonalEffectsState() (line 1988)
- public static IDictionary GetSeasonalEffectsDictionary() (line 2004)
  Returns Dictionary<string, SeasonalEffectState>.
- public static object GetConditionsState() (line 2020)
- public static List<string> GetEarlyEffects() (line 2036)
  Modifiers applied at embark.
- public static List<string> GetLateEffects() (line 2052)
  Modifiers applied at embark.
- public static object GetSimpleSeasonalEffectModel(string name) (line 2067)
- public static object GetConditionalSeasonalEffectModel(string name) (line 2082)
- public static object GetEffectModel(string name) (line 2098)
  Used for world modifiers.

#### Storage / Goods
- public static object GetStorageService() (line 2177)
- public static object GetMainStorage() (line 2192)
- public static Dictionary<string, int> GetAllStoredGoods() (line 2208)
  Only includes goods with amount > 0.
- public static Array GetAllGoodModels() (line 2239)
  From Settings.Goods.
- public static object GetGoodCategory(object goodModel) (line 2254)
- public static string GetDisplayName(object model) (line 2270)
  Works for GoodModel or GoodCategoryModel via displayName.Text.
- public static string GetModelName(object model) (line 2294)
  SO.Name property.
- public static int GetModelOrder(object model) (line 2309)
  The `order` field, used for sorting.
- public static bool IsGoodActive(object goodModel) (line 2325)

#### GoodRef (forwarding to BuildingReflection)
- public static Type GoodRefType { get; } (line 2341)
- public static FieldInfo GoodRefGoodField { get; } (line 2342)
- public static FieldInfo GoodRefAmountField { get; } (line 2343)
- public static PropertyInfo GoodRefDisplayNameProperty { get; } (line 2344)

#### Blackboard / Popup opening
- public static object GetGameBlackboardService() (line 2381)
- public static object GetUnitDefault() (line 2401)
  Gets UniRx.Unit.Default; safe to cache permanently.
- public static bool InvokeSubjectOnNext(object blackboardService, string subjectPropertyName, object parameter) (line 2445)
  Helper to invoke OnNext on a UniRx Subject property.
- public static bool OpenRecipesPopup() (line 2480)
  Creates RecipesPopupRequest(true) and fires RecipesPopupRequested.
- public static bool OpenOrdersPopup() (line 2513)
- public static bool OpenTradeRoutesPopup() (line 2526)
- public static bool OpenConsumptionPopup() (line 2539)
- public static bool OpenPaymentsPopup() (line 2552)
- public static bool OpenTrendsPopup() (line 2565)
- public static bool OpenTraderPanel() (line 2580)
  Opens TraderPanel.Instance.Show(currentVisit, true). Complex: finds TraderPanel type, gets current visit from TradeService, calls Show.

#### Goods helpers
- public static string GetGoodDisplayName(string goodName) (line 2687)
  Looks up GoodModel from Settings.GetGood, returns displayName.
- public static string GetGoodDescription(string goodName) (line 2711)
  Returns full description with rich text stripped.
- public static Dictionary<string, int> GetStorageGoods() (line 2737)
  Alias for GetAllStoredGoods().
- public static string GetRelicDisplayName(string relicModelName) (line 2770)
  Looks up relic model from Settings.GetRelic.
- public static string GetMetaCurrencyDisplayName(string currencyName) (line 2827)
  Looks up MetaCurrencyModel from Settings.GetMetaCurrency.

#### Effects / Cornerstones / Perks
- public static object GetEffectsService() (line 2922)
- public static object GetPerksService() (line 2937)
- public static object GetCornerstonesState() (line 2952)
- public static IEnumerable GetAllConditions() (line 2969)
  Via EffectsService.GetAllConditions(). Includes biome effects, difficulty modifiers, embark effects, event effects.
- public static List<string> GetActiveCornerstones() (line 2986)
  List of active cornerstone effect names.
- public static IList GetSortedPerks() (line 3003)
  List of PerkState objects with name, stacks, hidden fields.
- public static (string name, int stacks, bool hidden) GetPerkInfo(object perkState) (line 3042)
- public static bool GetEffectIsPerk(object effectModel) (line 3081)
- public static string GetEffectName(object effectModel) (line 3096)
- public static object GetSeasonalEffectWrappedEffect(object seasonalEffectModel) (line 3112)
  Only SimpleSeasonalEffectModel has an "effect" field.
- public static string GetSeasonalEffectWrappedEffectName(object seasonalEffectModel) (line 3131)
- public static int GetSeasonalEffectHostilityLevel(object seasonalEffectModel) (line 3141)

#### Cache diagnostics
- public static int LogCacheStatus() (line 3155)
  Delegates to ReflectionValidator.TriggerAndValidate.
