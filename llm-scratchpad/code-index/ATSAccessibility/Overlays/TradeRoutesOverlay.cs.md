# TradeRoutesOverlay.cs
Accessible overlay for the TradeRoutesPopup.
Level 0: Main menu (active routes summary, towns, toggles).
Level 1: Active Routes or Town Offers (determined by _branch).

## class TradeRoutesOverlay: MenuBase (line 13)

### Fields
- private enum Branch { MainMenu, ActiveRoutes, TownOffers } (line 14)
- private enum MainMenuItemType { ActiveRoutes, Town, AutoCollect, OnlyAvailable } (line 15)
- private class MainMenuItem (line 17)
  - public MainMenuItemType Type (line 18)
  - public string Label (line 19)
  - public string SearchName (line 20)
  - public int TownIndex (line 21)
- private Branch _branch (line 24)
- private int _currentTownIndex (line 25)
- private List<MainMenuItem> _mainMenuItems (line 27)
- private List<TradeRoutesReflection.RouteInfo> _routes (line 28)
- private List<TradeRoutesReflection.TownInfo> _towns (line 29)
- private List<TradeRoutesReflection.OfferInfo> _offers (line 30)

### Properties
- protected override string OverlayName { get; } (line 36)
- protected override string EmptyMessage { get; } (line 37)
- protected override int SearchItemCount { get; } (line 160)
  MainMenu searches _mainMenuItems; TownOffers searches offers+1; ActiveRoutes returns 0

### Methods
- protected override int GetItemCount() (line 39)
- protected override string GetLabel(int index) (line 52)
  TownOffers: index 0 is the Extend Offers item, index 1+ are offers
- protected override void RefreshData() (line 76)
- protected override EnterAction OnEnter(int index) (line 81)
- protected override void OnAction(int index) (line 85)
  Dispatches to ActivateMainMenuItem, CollectCurrentRoute, or ActivateTownOffersItem
- protected override bool CanDrillDown(int index) (line 99)
  Only ActiveRoutes and Town items in the main menu can drill down
- protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) (line 106)
  In TownOffers branch: adjusts trade amount via AdjustAmount
- protected override void OnGoBack() (line 111)
  Resets _branch to MainMenu and refreshes
- protected override EscapeAction OnEscape() (line 117)
  Level > 0: GoBack; Level 0: PassThrough to game
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 123)
  RightArrow in MainMenu: enters ActiveRoutes or TownOffers for current item
- protected override string GetOpenAnnouncement() (line 141)
- protected override void AnnounceCurrentItem() (line 146)
- protected override void OnClosed() (line 152)
- protected override string GetSearchName(int index) (line 170)
- private void RefreshAllData() (line 190)
- private void RefreshMainMenu() (line 195)
  Builds _mainMenuItems: ActiveRoutes entry, one entry per town, AutoCollect, OnlyAvailable
- private void RefreshActiveRoutes() (line 239)
- private void RefreshTownOffers(int townIndex) (line 243)
- private void ClearData() (line 253)
- private void ActivateMainMenuItem(int index) (line 264)
- private void ToggleAutoCollect() (line 284)
  Toggles auto-collect; if newly enabled, immediately collects all ready routes
- private void ToggleOnlyAvailable() (line 306)
- private void EnterActiveRoutes() (line 320)
  Validates routes exist, sets branch to ActiveRoutes, drills to Level 1
- private string BuildRouteLabel(TradeRoutesReflection.RouteInfo route) (line 337)
- private void CollectCurrentRoute() (line 347)
  Collects ready route; on success, returns to main menu if no routes remain
- private void EnterTownOffers(MainMenuItem item) (line 380)
  Sets branch to TownOffers, refreshes offers, drills to Level 1
- private void AnnounceTownHeader(TradeRoutesReflection.TownInfo town) (line 399)
- private string BuildTownLabel(TradeRoutesReflection.TownInfo town) (line 407)
- private string BuildExtendOffersLabel(TradeRoutesReflection.TownInfo town) (line 426)
- private string BuildOfferLabel(TradeRoutesReflection.OfferInfo offer) (line 435)
- private void ActivateTownOffersItem() (line 459)
  Index 0 calls ExtendOffers; index 1+ calls AcceptCurrentOffer
- private void AdjustAmount(int delta) (line 466)
  Clamps offer multiplier between 1 and offer.MaxMultiplier
- private void AcceptCurrentOffer() (line 497)
  Accepts the trade offer; returns to main menu if no offers remain
- private void ExtendOffers() (line 534)
  Pays to extend town offers; focuses the newly added offer
- private void ReturnToMainMenu(bool dataChanged) (line 566)
  Resets to Level 0, branch MainMenu; refreshes data if dataChanged
