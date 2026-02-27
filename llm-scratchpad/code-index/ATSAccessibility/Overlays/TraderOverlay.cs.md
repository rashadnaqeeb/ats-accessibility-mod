# TraderOverlay.cs
Accessible overlay for the TraderPanel.
Provides multi-level navigation for trader interaction:
- Mode 1 (No Trader): Flat list with next trader info and force arrival
- Mode 2 (Trader Present): Main menu with goods trading, perks, and assault

Level 0: Main menu (NoTrader items or TraderPresent items)
Level 1: Branch content — GoodsTrade goods list (with tabs) or Perks list
Confirmations are modal (not a level) — intercepted in HandleSpecialKey.

## class TraderOverlay: MenuBase (line 19)

### Fields
- private enum Mode { NoTrader, TraderPresent } (line 24)
- private enum Branch { GoodsTrade, Perks } (line 25)
- private enum Tab { Sell, Buy } (line 26)
- private enum ConfirmState { None, Trade, Assault } (line 27)
- private class NavItem (line 29)
  - public string Label (line 30)
  - public string SearchName (line 31)
  - public Action OnActivate (line 32)
- private class TradeGoodItem (line 35)
  - public string Name (line 36)
  - public string DisplayName (line 37)
  - public int MaxAmount (line 38)
  - public int OfferedAmount (line 39)
  - public float UnitValue (line 40)
  - public bool IsSell (line 41)
- private class PerkItem (line 44)
  - public string Name (line 45)
  - public string DisplayName (line 46)
  - public string Description (line 47)
  - public float Price (line 48)
  - public bool Discounted (line 49)
  - public float DiscountRatio (line 50)
  - public bool Sold (line 51)
  - public object EffectState (line 52)
- private Mode _mode (line 55)
- private Branch _branch (line 56)
- private Tab _currentTab (line 57)
- private ConfirmState _confirmState (line 58)
- private List<NavItem> _noTraderItems (line 61)
- private List<NavItem> _mainMenuItems (line 64)
- private List<TradeGoodItem> _sellGoods (line 65)
- private List<TradeGoodItem> _buyGoods (line 66)
- private List<PerkItem> _perks (line 67)
- private static readonly List<HelpEntry> _traderHelpEntries (line 152)

### Properties
- protected override string OverlayName { get; } (line 73)
- protected override string EmptyMessage { get; } (line 74)

### Methods
- protected override int GetItemCount() (line 76)
- protected override string GetLabel(int index) (line 92)
- protected override void RefreshData() (line 117)
  Detects trader presence and delegates to RefreshTraderData or RefreshNoTraderData
- protected override EnterAction OnEnter(int index) (line 127)
- protected override void OnAction(int index) (line 129)
  Level 0: invokes NavItem.OnActivate; Level 1 Perks: calls BuyCurrentPerk
- protected override EscapeAction OnEscape() (line 148)
  Level > 0: GoBack; Level 0: PassThrough to game
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 157)
- protected override void OnGoBack() (line 159)
  Refreshes main menu when returning from Perks
- protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) (line 165)
  Level 1 GoodsTrade: adjusts quantity; Shift multiplies delta by 10
- protected override string GetOpenAnnouncement() (line 171)
- protected override void OnClosed() (line 183)
- protected override string GetSearchName(int index) (line 192)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 218)
  Intercepts confirmation modal; handles Alt+B/A; handles Left/Right tab switch and Plus at Level 1 GoodsTrade
- private void RefreshNoTraderData() (line 263)
  Builds _noTraderItems with trader info, description, and force arrival option
- private void ActivateForceArrival() (line 346)
- private void RefreshTraderData() (line 370)
- private void RefreshMainMenu() (line 377)
  Builds main menu with trader info, Goods Trade, Perks, and Assault (if available)
- private void RefreshSellGoods() (line 423)
- private void RefreshBuyGoods() (line 438)
- private void RefreshPerks() (line 453)
- private void ClearData() (line 470)
- private void EnterGoodsTrade() (line 482)
- private void AnnounceSellTab() (line 493)
- private void AnnounceBuyTab() (line 501)
- private string BuildGoodLabel(TradeGoodItem good) (line 509)
  Different format when offered amount is nonzero (shows total Amber value)
- private void AdjustQuantity(int delta) (line 527)
  Clamps to [0, MaxAmount]; announces new amount and running balance
- private float CalculateBalance() (line 543)
- private void AnnounceBalance() (line 548)
  Announces sell total, buy total, balance, and whether trade is fair
- private void CalculateTradeTotals(out float sellTotal, out float buyTotal) (line 556)
- private void TryAcceptTrade() (line 570)
  Validates trade is non-empty and fair; sets ConfirmState.Trade and announces summary
- private void EnterPerks() (line 622)
- private string BuildPerkLabel(PerkItem perk) (line 637)
- private void BuyCurrentPerk() (line 654)
  Checks amber balance, calls BuyPerk, plays transaction sounds, refreshes all data
- private void EnterAssaultConfirm() (line 692)
  Sets ConfirmState.Assault and announces confirmation prompt
- private bool ProcessConfirmationKey(KeyCode keyCode) (line 702)
  Modal key handler for both Trade and Assault confirmations; Enter confirms, Escape cancels
- private void ExecuteAssault() (line 738)
- private void ExecuteTrade() (line 752)
  Executes the trade, plays sounds, resets offered amounts, re-announces current tab
