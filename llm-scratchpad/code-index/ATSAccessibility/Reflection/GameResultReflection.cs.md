# GameResultReflection.cs

Reflection helper for GameResultPopup data extraction.
Provides access to win/loss state, progression data, score breakdown, and world event info.

## class GameResultReflection (line 13)

### Nested Types
- public struct ScoreEntry (line 465): fields `Label` (string), `Points` (int), `Amount` (int)
- public struct WorldEventInfo (line 523): fields `Name` (string), `Description` (string), `Completed` (bool), `Objectives` (List<string>)

### Fields (private static cached)
- private static bool _typesCached (line 15)

#### GameResultPopup
- private static Type _gameResultPopupType (line 18)
- private static FieldInfo _headerTextField (line 21)
- private static FieldInfo _descTextField (line 22)
- private static FieldInfo _menuButtonField (line 23)
- private static FieldInfo _continueButtonField (line 24)

#### GameMB.StateService
- private static PropertyInfo _gameMBStateServiceProperty (line 27)
- private static PropertyInfo _stateServiceGameObjectivesProperty (line 28)
- private static FieldInfo _gameObjectivesHasWonField (line 29)
- private static FieldInfo _gameObjectivesHasLostField (line 30)

#### GameMB.GameSealService
- private static PropertyInfo _gameMBGameSealServiceProperty (line 33)
- private static MethodInfo _isSealedBiomeMethod (line 34)

#### TutorialService
- private static PropertyInfo _mbTutorialServiceProperty (line 37)
- private static MethodInfo _isAnyTutorialMethod (line 38)

#### GameMB.Biome
- private static PropertyInfo _gameMBBiomeProperty (line 41)

#### GameGoalsService
- private static PropertyInfo _gameServicesGameGoalsServiceProperty (line 44)
- private static MethodInfo _getUnshownCompletedGoalsMethod (line 45)

#### MetaStateService / progression
- private static PropertyInfo _metaStateServiceProperty (line 48)
- private static PropertyInfo _mssEconomyProperty (line 49)
- private static PropertyInfo _mssLevelProperty (line 50)
- private static FieldInfo _economyCurrentCycleExpField (line 51)
- private static FieldInfo _levelLevelField (line 52)
- private static FieldInfo _levelExpField (line 53)
- private static FieldInfo _levelTargetExpField (line 54)

#### Settings / Goals
- private static MethodInfo _settingsGetGoalMethod (line 57)
- private static FieldInfo _goalDisplayNameField (line 58)

#### ScoreCalculator
- private static Type _scoreCalculatorType (line 61)
- private static MethodInfo _getScoreMethod (line 62)
- private static FieldInfo _scoreDataLabelField (line 63)
- private static FieldInfo _scoreDataPointsField (line 64)
- private static FieldInfo _scoreDataAmountField (line 65)

#### WorldStateService / world event
- private static PropertyInfo _mbWorldStateServiceProperty (line 68)
- private static PropertyInfo _wsssCycleProperty (line 69)
- private static FieldInfo _cycleActiveCycleGoalsField (line 70)
- private static FieldInfo _goalStateModelField (line 71)
- private static FieldInfo _goalStateCompletedField (line 72)
- private static FieldInfo _goalDescriptionField (line 73)
- private static MethodInfo _goalGetObjectivesBreakdownMethod (line 74)

#### TMP_Text
- private static PropertyInfo _tmpTextProperty (line 77)

#### MetaCurrency
- private static FieldInfo _metaCurrencyNameField (line 80)
- private static FieldInfo _metaCurrencyAmountField (line 81)
- private static PropertyInfo _stateConditionsProperty (line 82)
- private static FieldInfo _conditionsRewardsField (line 83)

#### BiomeService / seal fragments
- private static PropertyInfo _gameMBBiomeServiceProperty (line 86)
- private static PropertyInfo _biomeServiceDifficultyProperty (line 87)
- private static FieldInfo _difficultySealFragmentsField (line 88)

#### ConditionsService / game mode
- private static PropertyInfo _gameMBConditionsServiceProperty (line 91)
- private static MethodInfo _isCustomGameMethod (line 92)
- private static MethodInfo _isChallangeMethod (line 93)

### Methods
- private static void EnsureTypes() (line 801)
- private static object GetStateService() (line 695)
- private static object GetGameSealService() (line ~)
- private static object GetTutorialService() (line ~)
- private static object GetBiome() (line ~)
- private static object GetGameGoalsService() (line ~)
- private static object GetMetaStateService() (line ~)
- private static object GetWorldStateService() (line ~)
- private static object GetBiomeService() (line ~)
- private static object GetConditionsService() (line ~)

#### Detection
- public static bool IsGameResultPopup(object popup) (line 102)
  Uses `GetType().Name == "GameResultPopup"` comparison.

#### Win/Loss state
- public static bool HasWon() (line 114)
- public static bool HasLost() (line 133)
- public static bool IsSealedBiome() (line 152)
- public static bool IsTutorial() (line 167)

#### UI text
- public static string GetHeaderText(object popup) (line 187)
  Reads from `headerText` TMP field.
- public static string GetDescriptionText(object popup) (line 204)
  Reads from `descText` TMP field.

#### Progression
- public static int GetGainedExp() (line 236)
- public static (int level, int exp, int targetExp) GetLevelInfo() (line 253)
- public static List<string> GetCompletedGoals() (line 275)
  Returns display names of completed goals.
- public static List<(string name, int amount)> GetMetaCurrencies() (line 312)
- public static List<(string name, int amount)> GetStoredMetaCurrencies() (line 355)
  Checks main storage for goods matching meta currency models.
- public static int GetSealFragments() (line 417)
  Returns 0 for custom games and challenge runs.

#### Score
- public static List<ScoreEntry> GetScoreBreakdown() (line 474)
  Returns empty list for tutorials.
- public static int GetTotalScore() (line 510)
  Sums `GetScoreBreakdown().Points`.

#### World event
- public static bool HasActiveWorldEvent() (line 532)
- public static WorldEventInfo? GetWorldEventInfo() (line 540)

#### Buttons
- public static bool IsContinueButtonAvailable(object popup) (line 615)
  Checks button's GameObject.activeSelf.
- public static void ClickMenuButton(object popup) (line 640)
  Invokes onClick.
- public static void ClickContinueButton(object popup) (line 667)
  Invokes onClick.

#### Private helpers
- private static string GetTMPText(object component) (line ~)
- private static string GetMetaCurrencyDisplayName(string currencyName) (line ~)
- private static bool IsCustomGame() (line ~)
- private static bool IsChallenge() (line ~)
- private static IEnumerable GetActiveCycleGoals() (line ~)

#### Cache
- public static int LogCacheStatus() (line 949)
