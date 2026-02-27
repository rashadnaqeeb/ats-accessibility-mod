# HearthReflection.cs

Reflection-based access to Hearth building internals.
Extracted from BuildingReflection.cs. Follows same caching patterns.
`BuildingReflection.IsHearth()` remains in BuildingReflection for routing use.

## class HearthReflection (line 12)

### Structs

#### DecorationRequirementInfo (line 535)
- public string tierName
- public int required
- public int current

#### HearthUpgradeInfo (line 544)
- public int index  — tier index (0, 1, 2)
- public string displayName
- public string effectDescription
- public int minPopulation
- public int currentPopulation
- public int minInstitutions
- public int currentInstitutions
- public List<DecorationRequirementInfo> decorationRequirements
- public bool isUnlockedInMeta
- public bool isAchieved

#### SacrificeRecipeInfo (line 791)
- public string recipeName
- public string goodName
- public float consumptionPerMin
- public string effectName
- public string effectDescription
- public int level  — current level (0 = off)
- public int maxLevel
- public bool active
- public bool canAfford

#### FuelInfo (line 957)
- public string name  — internal name (GoodModel.Name)
- public string displayName
- public bool isEnabled
- public int priority  — burn priority (0-3, higher = preferred)

#### HearthServiceInfo (line 1093)
- public string NeedName
- public string GoodName  — internal name
- public string GoodDisplayName
- public int GoodAmount
- public int Grade
- public string GradeDescription
- public bool IsGoodConsumed

### Fields (private static cached)

#### Hearth base
- private static FieldInfo _hearthStateField (line 18)  — Hearth.state
- private static FieldInfo _hearthModelField (line 19)  — Hearth.model
- private static FieldInfo _hearthStateBurningTimeLeftField (line 20)
- private static FieldInfo _hearthStateCorruptionField (line 21)
- private static FieldInfo _hearthStateHubIndexField (line 22)
- private static FieldInfo _hearthStateWorkersField (line 23)
- private static FieldInfo _hearthModelMaxBurningTimeField (line 24)
- private static FieldInfo _hearthModelMinTimeToShowNoFuelField (line 25)
- private static MethodInfo _hearthIsMainHearthMethod (line 26)
- private static MethodInfo _hearthGetRangeMethod (line 27)
- private static MethodInfo _hearthGetCorruptionRateMethod (line 28)
- private static bool _hearthBaseFieldsCached (line 29)

#### Hearth sacrifice
- private static FieldInfo _hearthStateSacrificeRecipesField (line 32)  — List<HearthSacrificeState>
- private static Type _hearthSacrificeStateType (line 33)
- private static FieldInfo _hssModelField (line 34)  — HearthSacrificeState.model
- private static FieldInfo _hssActiveField (line 35)
- private static FieldInfo _hssLevelField (line 36)
- private static Type _hearthSacrificeRecipeModelType (line 37)
- private static FieldInfo _hsrmDisplayNameField (line 38)
- private static FieldInfo _hsrmMaxLevelField (line 39)
- private static FieldInfo _hsrmGoodPerMinField (line 40)  — GoodRef
- private static FieldInfo _hsrmEffectField (line 41)  — EffectModel
- private static MethodInfo _hearthGetEffectLevelMethod (line 42)
- private static MethodInfo _hearthGetMaxLevelForMethod (line 43)
- private static MethodInfo _hearthHaveGoodsForMethod (line 44)
- private static MethodInfo _hearthSetSacrificeEffectLevelMethod (line 45)
- private static MethodInfo _settingsGetHearthSacrificeRecipeMethod (line 46)
- private static PropertyInfo _effectModelDescProp (line 47)
- private static MethodInfo _effectsServiceGetHearthSacrificeRateMethod (line 48)
- private static bool _hearthSacrificeTypesCached (line 49)

#### Hearth fuel
- private static PropertyInfo _goodsServiceFuelsProperty (line 52)  — IGoodsService.Fuels
- private static MethodInfo _hearthServiceCanBeBurnedMethod (line 53)
- private static MethodInfo _hearthServiceSetCanBeBurnedMethod (line 54)
- private static MethodInfo _hearthServiceGetPriorityMethod (line 55)
- private static MethodInfo _hearthServiceSetPriorityMethod (line 56)
- private static PropertyInfo _gsHearthServiceProperty (line 57)
- private static PropertyInfo _gsGoodsServiceProperty (line 58)
- private static FieldInfo _goodModelDisplayNameField (line 59)
- private static PropertyInfo _goodModelNameProperty (line 60)
- private static bool _hearthFuelTypesCached (line 61)

#### Hearth services (The Commons)
- private static MethodInfo _metaPerksAreHearthServicesUnlockedMethod (line 64)
- private static MethodInfo _hearthAreHearthServicesEnabledMethod (line 65)
- private static MethodInfo _hearthUnlockExtraRecipesMethod (line 66)
- private static MethodInfo _hearthOnExtraRecipesUnlockedMethod (line 67)
- private static PropertyInfo _buildingsServiceHearthsProperty (line 68)
- private static FieldInfo _hearthModelExtraRecipesField (line 69)  — HearthNeedRecipeModel[]
- private static FieldInfo _hearthModelExtraRecipesUnlockPriceField (line 70)  — GoodRef
- private static Type _hearthNeedRecipeModelType (line 71)
- private static FieldInfo _hnrmServedNeedField (line 72)
- private static FieldInfo _hnrmRequiredGoodField (line 73)  — GoodRef
- private static FieldInfo _hnrmGradeField (line 74)  — RecipeGradeModel
- private static FieldInfo _hnrmIsGoodConsumedField (line 75)
- private static Type _recipeGradeModelType (line 76)
- private static FieldInfo _rgmLevelField (line 77)
- private static FieldInfo _rgmDescriptionField (line 78)  — LocaText
- private static PropertyInfo _needModelDisplayNameProperty (line 79)
- private static bool _hearthServicesTypesCached (line 80)

#### Hearth hub/upgrade
- private static Type _hubTierType (line 83)
- private static FieldInfo _hubTierIndexField (line 84)
- private static FieldInfo _hubTierEffectField (line 85)
- private static FieldInfo _hubTierDisplayNameField (line 86)
- private static FieldInfo _hubTierMinPopulationField (line 87)
- private static FieldInfo _hubTierMinInstitutionsField (line 88)
- private static FieldInfo _hubTierDecorationsField (line 89)  — DecorationRequirement[]
- private static Type _decorationRequirementType (line 90)
- private static FieldInfo _decorReqTierField (line 91)
- private static FieldInfo _decorReqAmountField (line 92)
- private static Type _decorationTierType (line 93)
- private static FieldInfo _settingsHubsTiersField (line 94)
- private static MethodInfo _metaPerksServiceGetUnlockedHubsMethod (line 95)
- private static PropertyInfo _mbMetaPerksServiceProperty (line 96)
- private static MethodInfo _hearthIsInRangeMethod (line 97)
- private static PropertyInfo _buildingsServiceHousesProperty (line 98)
- private static PropertyInfo _buildingsServiceInstitutionsProperty (line 99)
- private static PropertyInfo _buildingsServiceDecorationsProperty (line 100)
- private static FieldInfo _decorModelHasDecorationTierField (line 101)
- private static FieldInfo _decorModelTierField (line 102)
- private static FieldInfo _decorModelDecorationScoreField (line 103)
- private static bool _hubTierTypesCached (line 104)

### Methods (private — initialization)

- private static void EnsureHearthBaseFields()
- private static void EnsureHearthSacrificeTypes()
- private static void EnsureHearthFuelTypes()
- private static void EnsureHearthServicesTypes()
- private static void EnsureHubTierTypes()

### Methods (public)

#### Core status
- public static float GetHearthFireLevel(object building) (line 380)  — burning time left as fraction of max
- public static float GetHearthFuelTimeRemaining(object building) (line 402)  — seconds
- public static bool IsHearthFireLow(object building) (line 420)
- public static bool IsHearthFireOut(object building) (line 442)
- public static int GetHearthHubIndex(object building) (line 449)
- public static float GetHearthCorruptionRate(object building) (line 467)
- public static float GetHearthRange(object building) (line 482)
- public static bool IsMainHearth(object building) (line 497)
- public static int[] GetHearthWorkerIds(object building) (line 513)

#### Hub tier / upgrades
- public static int GetUnlockedHubTierCount() (line 560)
- public static List<HearthUpgradeInfo> GetHearthUpgradeInfo(object building) (line 578)

#### Sacrifice
- public static List<object> GetHearthSacrificeRecipes(object building) (line 806)  — returns List of HearthSacrificeState objects
- public static bool SetHearthSacrificeLevel(object hearth, object recipeState, int level) (line 912)
- public static float GetHearthSacrificeRate() (line 932)  — global rate from IEffectsService

#### Fuel
- public static List<FuelInfo> GetAllFuelTypes() (line 967)
- public static bool SetFuelEnabled(string fuelName, bool enabled) (line 1023)
- public static int GetFuelPriority(string fuelName) (line 1046)
- public static bool SetFuelPriority(string fuelName, int priority) (line 1066)

#### Services (The Commons)
- public static bool AreHearthServicesMetaUnlocked() (line 1106)
- public static bool AreHearthServicesEnabled(object building) (line 1125)
- public static (string goodName, string displayName, int amount)? GetHearthServicesUnlockPrice(object building) (line 1143)
- public static bool CanAffordHearthServicesUnlock(object building) (line 1174)
- public static bool UnlockHearthServices(object building) (line 1185)
- public static List<HearthServiceInfo> GetHearthServiceRecipes(object building) (line 1242)
