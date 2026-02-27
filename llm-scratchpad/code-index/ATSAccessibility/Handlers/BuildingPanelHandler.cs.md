# BuildingPanelHandler.cs
Main handler for building panel accessibility.
Subscribes to building panel open/close events and routes keyboard input
to the appropriate building-specific navigator.

## class BuildingPanelHandler: IKeyHandler, IHelpProvider (line 15)

### Fields
- private KeyboardManager _keyboardManager (line 20)
- private IDisposable _shownSubscription (line 26)
- private IDisposable _closedSubscription (line 27)
- private bool _subscribed (line 28)
- private IBuildingNavigator _currentNavigator (line 34)
- private object _currentBuilding (line 35)
- private bool _isCleaningUp (line 36)
- private ProductionNavigator _productionNavigator (line 42)
- private SimpleNavigator _simpleNavigator (line 43)
- private HearthNavigator _hearthNavigator (line 44)
- private HouseNavigator _houseNavigator (line 45)
- private RelicNavigator _relicNavigator (line 46)
- private PortNavigator _portNavigator (line 47)
- private FishingHutNavigator _fishingHutNavigator (line 48)
- private StorageNavigator _storageNavigator (line 49)
- private InstitutionNavigator _institutionNavigator (line 50)
- private ShrineNavigator _shrineNavigator (line 51)
- private PoroNavigator _poroNavigator (line 52)
- private WaterNavigator _waterNavigator (line 53)
- private HydrantNavigator _hydrantNavigator (line 54)
- private FarmfieldNavigator _farmfieldNavigator (line 55)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 61)
- public string HelpContextName { get; } (line 62)
- public bool IsActive { get; } (line 82)
  True when a navigator and building are set, the game's panel is still open, and no popup context is active. Side-effect free; cleanup is done in ProcessKey.

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 64)
  Delegates to the current navigator's IHelpProvider if it implements that interface.
- public IReadOnlyList<string> GetPassthroughKeys() (line 70)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 89)
  Checks IsActive (which re-checks panel open state); if stale state is detected, calls CleanupNavigator. Otherwise delegates to _currentNavigator.ProcessKey.
- private void CleanupNavigator() (line 105)
  Closes the current navigator and clears state. Uses _isCleaningUp guard to prevent re-entrant calls.
- public BuildingPanelHandler(KeyboardManager keyboardManager) (line 123)
  Instantiates all 14 navigator types.
- public void TrySubscribe() (line 146)
  Subscribes to building panel shown/closed events via BuildingReflection. Called periodically from AccessibilityCore.Update() until both subscriptions succeed.
- public void Dispose() (line 165)
  Disposes event subscriptions and calls CleanupAllNavigators to release building references.
- private void CleanupAllNavigators() (line 181)
  Calls Close() on all 14 navigators to release stale building references.
- private void OnBuildingPanelShown(object building) (line 202)
  Event handler: selects the appropriate navigator via SelectNavigator, then opens it. Falls back to announcing building name if no navigator matches.
- private void OnBuildingPanelClosed(object building) (line 219)
  Event handler: plays panel-hide sound, closes current navigator, announces "Panel closed". Uses _isCleaningUp guard.
- private IBuildingNavigator SelectNavigator(object building) (line 241)
  Dispatches to the correct navigator based on building type checks in priority order (most specific first, ProductionBuilding last as many special types are subclasses). Falls back to SimpleNavigator.
