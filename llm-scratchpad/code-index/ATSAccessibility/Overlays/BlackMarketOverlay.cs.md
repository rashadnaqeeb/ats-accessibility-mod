# BlackMarketOverlay.cs
Accessible overlay for the BlackMarketPopup.
Level 0 = main list (header + reroll + offers).
Level 1 = sub-menu (buy now / buy on credit).

## class BlackMarketOverlay: MenuBase (line 13)

### Fields
- private enum ItemType (line 14)
  Values: Header, Reroll, Offer
- private class NavItem (line 16)
  Fields: ItemType Type, BlackMarketReflection.OfferInfo? Offer, string Label, string SearchName
- private object _blackMarket (line 24)
- private List<NavItem> _items (line 27)

### Properties
- protected override string OverlayName { get; } (line 33)
- protected override string EmptyMessage { get; } (line 34)
- protected override int SearchItemCount { get; } (line 165)

### Methods
- protected override int GetItemCount() (line 36)
  Returns _items.Count at level 0; always 2 (Buy now / Buy on credit) at level 1.
- protected override string GetLabel(int index) (line 43)
  At level 1, reads parent offer from _indices[0] to format buy/credit labels.
- protected override void RefreshData() (line 72)
  Populates _items with header, reroll button, and all un-bought offers.
- protected override EnterAction OnEnter(int index) (line 103)
  Header and Reroll return Action; Offer returns DrillDown to open the buy sub-menu.
- protected override void OnAction(int index) (line 123)
- protected override void StorePopup(object popup) (line 143)
- protected override string GetOpenAnnouncement() (line 150)
- protected override void OnClosed() (line 156)
- protected override string GetSearchName(int index) (line 167)
- private string BuildRerollLabel() (line 177)
  Shows cooldown time remaining or price; includes "cannot afford" suffix when applicable.
- private string BuildOfferLabel(BlackMarketReflection.OfferInfo offer) (line 193)
- private void ExecuteReroll() (line 216)
  Validates cooldown and affordability before calling Reroll; navigates to first offer on success.
- private void ExitSubMenuAfterPurchase() (line 253)
  Returns to level 0 after a successful purchase; clamps index if the bought offer is removed.
- private void ExecuteSubMenuAction(int index) (line 262)
  index 0 = buy now, index 1 = buy on credit; reads parent offer from _indices[0].
