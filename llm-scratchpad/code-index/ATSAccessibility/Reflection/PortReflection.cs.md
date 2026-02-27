# PortReflection.cs

Reflection-based access to Port building internals (expeditions).
Extracted from BuildingReflection.cs. Follows same caching patterns.
`BuildingReflection.IsPort()` remains in BuildingReflection for routing use.

## class PortReflection (line 12)

### Fields (private static cached)

#### Port base
- private static Type _portType (line 18)
- private static FieldInfo _portStateField (line 19)  — Port.state
- private static FieldInfo _portModelField (line 20)  — Port.model
- private static MethodInfo _portWasExpeditionStartedMethod (line 21)
- private static MethodInfo _portAreRewardsWaitingMethod (line 22)
- private static MethodInfo _portCalculateProgressMethod (line 23)
- private static MethodInfo _portCalculateTimeLeftMethod (line 24)
- private static MethodInfo _portGetCurrentExpeditionMethod (line 25)
- private static MethodInfo _portGetPickedStriderGoodMethod (line 26)
- private static MethodInfo _portGetPickedCrewGoodMethod (line 27)

#### Port action methods
- private static MethodInfo _portWasDecisionMadeMethod (line 29)
- private static MethodInfo _portLockDecisionMethod (line 30)
- private static MethodInfo _portCancelDecisionMethod (line 31)
- private static MethodInfo _portAcceptRewardsMethod (line 32)
- private static MethodInfo _portChangeLevelMethod (line 33)
- private static MethodInfo _portAllGoodsDeliveredMethod (line 34)
- private static MethodInfo _portIsBlockedByUnpickedCategoryMethod (line 35)
- private static MethodInfo _portGetCurrentExpeditionModelMethod (line 36)
- private static MethodInfo _portCalculateDurationMethod (line 37)

#### PortState
- private static FieldInfo _portStateExpeditionLevelField (line 39)
- private static FieldInfo _portStateAreRewardsWaitingField (line 40)
- private static FieldInfo _portStateBlueprintRewardField (line 41)
- private static FieldInfo _portStatePerkRewardField (line 42)
- private static FieldInfo _portStateExpeditionGoodsField (line 43)
- private static FieldInfo _portStateWorkersField (line 44)
- private static FieldInfo _portStateWasDecisionMadeField (line 45)
- private static FieldInfo _portStatePickedCategoryField (line 46)
- private static FieldInfo _portStateStriderPickedGoodsField (line 47)  — List<int>
- private static FieldInfo _portStateCrewPickedGoodsField (line 48)  — List<int>

#### PortExpeditionModel
- private static FieldInfo _portExpedModelMaxLevelField (line 50)
- private static FieldInfo _portExpedModelBlueprintsField (line 51)
- private static FieldInfo _portExpedModelChancesField (line 52)  — PortRewardChance[]

#### PortExpedition
- private static FieldInfo _portExpedStriderGoodsField (line 54)  — GoodsSet[]
- private static FieldInfo _portExpedCrewGoodsField (line 55)  — GoodsSet[]
- private static FieldInfo _portExpedChancesField (line 56)  — List<PortRewardChance>

#### PortRewardChance
- private static FieldInfo _portRewardChanceRarityField (line 58)
- private static FieldInfo _portRewardChanceChanceField (line 59)

#### BuildingsDropTable / category (for blueprint category selection)
- private static FieldInfo _buildingsDropTableBuildingsField (line 61)
- private static FieldInfo _buildingTableEntityBuildingField (line 62)
- private static FieldInfo _buildingModelCategoryField (line 63)

#### LimitedGoodsCollection
- private static MethodInfo _limitedGoodsGetFullAmountMethod (line 65)

- private static bool _portTypesCached (line 67)

### Methods (private)

- private static void EnsurePortTypes() (line 73)

### Methods (public)

- public static int GetPortExpeditionLevel(object building) (line 163)
- public static bool IsPortExpeditionStarted(object building) (line 180)
- public static bool ArePortRewardsWaiting(object building) (line 195)
- public static float GetPortProgress(object building) (line 210)  — returns 0-1
- public static float GetPortTimeLeft(object building) (line 224)  — seconds remaining
- public static string GetPortBlueprintReward(object building) (line 238)
- public static string GetPortPerkReward(object building) (line 255)
- public static bool WasPortDecisionMade(object building) (line 272)
- public static bool AllPortGoodsDelivered(object building) (line 287)
- public static bool IsPortBlockedByUnpickedCategory(object building) (line 302)
- public static bool PortLockDecision(object building) (line 317)
- public static bool PortCancelDecision(object building) (line 335)
- public static bool PortAcceptRewards(object building) (line 353)
- public static bool PortChangeLevel(object building, int level) (line 371)
- public static int GetPortMaxLevel(object building) (line 389)
- public static float GetPortDuration(object building) (line 406)

#### Strider goods
- public static int GetPortStriderGoodSetCount(object building) (line 420)
- public static int GetPortStriderAlternativeCount(object building, int setIndex) (line 438)
- public static string GetPortStriderGoodDisplayName(object building, int setIndex, int altIndex) (line 453)
- public static string GetPortStriderGoodName(object building, int setIndex, int altIndex) (line 467)  — internal name
- public static int GetPortStriderGoodAmount(object building, int setIndex, int altIndex) (line 481)
- public static int GetPortStriderPickedIndex(object building, int setIndex) (line 495)
- public static bool SetPortStriderPickedIndex(object building, int setIndex, int altIndex) (line 518)

#### Crew goods
- public static int GetPortCrewGoodSetCount(object building) (line 542)
- public static int GetPortCrewAlternativeCount(object building, int setIndex) (line 560)
- public static string GetPortCrewGoodDisplayName(object building, int setIndex, int altIndex) (line 575)
- public static string GetPortCrewGoodName(object building, int setIndex, int altIndex) (line 589)  — internal name
- public static int GetPortCrewGoodAmount(object building, int setIndex, int altIndex) (line 603)
- public static int GetPortCrewPickedIndex(object building, int setIndex) (line 617)
- public static bool SetPortCrewPickedIndex(object building, int setIndex, int altIndex) (line 640)

#### Goods delivery / categories
- public static int GetPortGoodDeliveredAmount(object building, string goodName) (line 664)
- public static List<string> GetPortAvailableCategories(object building) (line 686)  — display names
- public static List<string> GetPortCategoryInternalNames(object building) (line 730)
- public static string GetPortPickedCategory(object building) (line 774)
- public static bool SetPortPickedCategory(object building, string categoryName) (line 791)
- public static bool PortHasBlueprintReward(object building) (line 809)
- public static List<(string rarity, int chance)> GetPortRewardChances(object building) (line 828)

### Methods (private helpers)

- private static object GetPortStriderGoodsSetObject(object building, int setIndex) (line 864)
- private static object GetPortStriderGoodRefObject(object building, int setIndex, int altIndex) (line 881)
- private static object GetPortCrewGoodsSetObject(object building, int setIndex) (line 895)
- private static object GetPortCrewGoodRefObject(object building, int setIndex, int altIndex) (line 912)
