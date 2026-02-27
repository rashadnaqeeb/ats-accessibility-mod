# DeedsReflection.cs

Reflection helpers for accessing GoalsPopup (Deeds menu) data and interaction.

## class DeedsReflection (line 11)

### Fields (private static cached)
- private static PropertyInfo _msMetaStateServiceProperty (line 17)
  MetaController.Instance.MetaServices.MetaStateService access.
- private static PropertyInfo _mssGoalsProperty (line 20)
  MetaStateService.Goals → MetaGoalsState.
- private static FieldInfo _mgsGoalsField (line 23)
  MetaGoalsState.goals → List<GoalState>.
- private static MethodInfo _settingsGetGoalMethod (line 26)
  Settings.GetGoal(string) → GoalModel.
- private static FieldInfo _goalModelLabelField (line 29)
- private static FieldInfo _goalModelDisplayNameField (line 30)
- private static PropertyInfo _goalModelDescriptionProperty (line 31)
- private static MethodInfo _goalModelGetMetaProgressTextMethod (line 32)
- private static FieldInfo _goalModelIsActiveField (line 33)
- private static FieldInfo _goalModelIsCycleGoalField (line 34)
- private static MethodInfo _goalModelHasAccessToMethod (line 35)
- private static FieldInfo _goalModelRewardsField (line 36)
- private static PropertyInfo _rewardDisplayNameProperty (line 39)
  MetaRewardModel.DisplayName.
- private static FieldInfo _goalStateModelField (line 42)
- private static FieldInfo _goalStateCompletedField (line 43)
- private static FieldInfo _goalStateRewardedField (line 44)
- private static FieldInfo _categoryDisplayNameField (line 47)
  GoalCategoryModel (LabelModel subclass).
- private static FieldInfo _categoryOrderField (line 48)
- private static FieldInfo _categoryIsHiddenField (line 49)
- private static PropertyInfo _msGoalsServiceProperty (line 52)
- private static MethodInfo _gsRewardGoalMethod (line 53)
- private static Type _goalsPopupType (line 56)
- private static bool _typesCached (line 58)

### Methods
- private static void EnsureTypesCached() (line 64)
- private static object GetMetaStateService() (line 176)
- private static object GetGoalsService() (line 178)
- public static bool IsGoalsPopup(object popup) (line 184)
- public static List<(object state, object model)> GetAllGoalStates() (line 199)
  Filters out inactive, cycle goals, inaccessible, and hidden-category-incomplete goals.
- public static string GetGoalName(object model) (line 258)
- public static string GetGoalDescription(object model) (line 263)
- public static string GetGoalProgressText(object model, object state) (line 268)
- public static bool IsGoalCompleted(object state) (line 273)
- public static bool IsGoalRewarded(object state) (line 277)
- public static object GetGoalCategory(object model) (line 281)
- public static string GetCategoryName(object category) (line 285)
- public static int GetCategoryOrder(object category) (line 290)
- public static bool IsCategoryHidden(object category) (line 294)
- public static string[] GetRewardNames(object model) (line 301)
- public static void ClaimGoal(object state, object model) (line 333)
- public static bool IsInGame() (line 347)
  Delegates to `GameReflection.GetIsGameActive()`.
- public static int LogCacheStatus() (line 351)
