# CornerstoneReflection.cs

## class CornerstoneReflection (line 13)

### Nested Types
- public class CornerstoneOption (line 16): fields `Model` (object), `DisplayName` (string), `Description` (string), `Rarity` (string), `IsEthereal` (bool)

### Fields (private static cached)
- private static Type _rewardPickPopupType (line 29)
- private static Type _cornerstonesLimitPickPopupType (line 30)
- private static PropertyInfo _cornerstonesServiceProp (line 31)
- private static PropertyInfo _cornerstonesStateServiceProp (line 32)
- private static PropertyInfo _pickOptionsProp (line 33)
- private static PropertyInfo _activeEffectsProp (line 34)
- private static PropertyInfo _rerollsLeftProp (line 35)
- private static MethodInfo _rerollMethod (line 36)
- private static MethodInfo _extendMethod (line 37)
- private static MethodInfo _canExtendMethod (line 38)
- private static MethodInfo _canAffordExtendMethod (line 39)
- private static MethodInfo _getExtendCostMethod (line 40)
- private static MethodInfo _onRewardPickedMethod (line 41)
- private static MethodInfo _skipMethod (line 42)
- private static MethodInfo _removeAndConfirmMethod (line 43)
- private static MethodInfo _cancelLimitPopupMethod (line 44)
- private static FieldInfo _npcNameField (line 45)
- private static FieldInfo _npcDialogueField (line 46)
- private static FieldInfo _declinePayoffField (line 47)
- private static FieldInfo _defaultConfigField (line 48)
- private static FieldInfo _configNpcNameField (line 49)
- private static FieldInfo _configDialogueField (line 50)
- private static FieldInfo _effectModelDisplayNameField (line 51)
- private static FieldInfo _effectModelDescriptionField (line 52)
- private static FieldInfo _effectModelRarityField (line 53)
- private static FieldInfo _effectModelIsEtherealField (line 54)
- private static PropertyInfo _rarityDisplayNameProp (line 55)
- private static PropertyInfo _goodRefDisplayNameProp (line 56)
- private static FieldInfo _goodRefAmountField (line 57)
- private static PropertyInfo _goodRefGoodProp (line 58)
- private static bool _typesCached (line 74)

### Methods
- private static void EnsureTypesCached() (line 80)
- private static object GetCornerstonesService() (line 212)
- private static object GetCornerstonesStateService() (line 220)
- public static bool IsRewardPickPopup(object popup) (line 231)
- public static bool IsCornerstonesLimitPickPopup(object popup) (line 236)
- public static List<CornerstoneOption> GetCurrentOptions() (line 281)
  Returns the current set of cornerstone pick options from the active popup.
- public static (string npcName, string dialogue) GetNpcDialogue(object popup) (line 317)
  Falls back to popup's defaultConfiguration if direct fields are null.
- public static void PickCornerstone(object popup, object effectModel) (line 358)
  Invokes popup's OnRewardPicked — triggers async Pick flow including limit check.
- public static void Skip(object popup) (line 371)
- public static (int amount, string goodDisplayName) GetDeclinePayoff() (line 380)
- public static int GetRerollsLeft() (line 400)
- public static void Reroll(object popup) (line 410)
  Calls via popup's Reroll method to keep UI in sync.
- public static bool CanExtend() (line 423)
- public static bool CanAffordExtend() (line 432)
- public static void Extend() (line 441)
- public static (int amount, string goodDisplayName) GetExtendCost() (line 451)
- public static List<CornerstoneOption> GetActiveCornerstones() (line 476)
  Returns currently active cornerstones from the cornerstones state service.
- public static void RemoveAndConfirm(object limitPopup, object effectModel) (line 500)
  Removes cornerstone, finishes the limit task as true, and hides the popup.
- public static void CancelLimitPopup(object limitPopup) (line 534)
  Finishes the limit task as false and hides the popup.
- private static string GetEffectDisplayName(object effectModel) (line ~)
- private static string GetEffectDescription(object effectModel) (line ~)
- private static string GetEffectRarity(object effectModel) (line ~)
- private static bool GetEffectIsEthereal(object effectModel) (line ~)
- private static CornerstoneOption BuildOption(object effectModel) (line ~)
- public static int LogCacheStatus() (line 547)
