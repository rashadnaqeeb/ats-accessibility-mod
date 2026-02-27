# MetaRewardsOverlay.cs
Key handler for MetaRewardsPopup and MetaLevelUpPopup.
Registered above GameResultOverlay so players can close the level-up
popup before interacting with the game result screen.
Does not extend MenuBase because it operates on raw GameObjects (not
service-layer popups) and delegates reading to a coroutine-based reader.

## class MetaRewardsOverlay: IKeyHandler (line 13)

### Fields
- private GameObject _currentPopup (line 14)
- private MonoBehaviour _coroutineRunner (line 15)

### Properties
- public bool IsActive { get; } (line 25)

### Methods
- public MetaRewardsOverlay(MonoBehaviour coroutineRunner) (line 17)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 27)
  Delegates all key handling to MetaRewardsPopupReader; passes Escape through; consumes everything else.
- public static bool IsMetaRewardsOrLevelUpPopup(string popupName) (line 51)
  Predicate for PopupRouter: checks if popup name contains "MetaRewards" or "MetaLevelUp".
- public void OnPopupShown(object popup) (line 59)
  Casts popup to Component, checks name via IsMetaRewardsOrLevelUpPopup, then starts the announcement coroutine.
- public void OnPopupHidden(object popup) (line 82)
  Clears state only if the hidden popup matches the currently tracked one.
- public void Reset() (line 99)
