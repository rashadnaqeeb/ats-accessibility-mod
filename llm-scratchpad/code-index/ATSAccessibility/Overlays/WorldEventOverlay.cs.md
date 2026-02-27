# WorldEventOverlay.cs
Accessible overlay for WorldEventPopup (decision screen for world events on the world map).
Provides flat list navigation: header (event name + description), then decision options.

## class WorldEventOverlay: MenuBase (line 12)

### Fields
- private enum ItemType { Header, Option } (line 14)
- private class ListItem (line 16)
  - public ItemType Type (line 17)
  - public string Text (line 18)
  - public int OptionIndex (line 19)
    Only for Option type; -1 for Header
- private List<ListItem> _items (line 23)
- private object _model (line 27)
- private object _state (line 28)

### Properties
- protected override string OverlayName { get; } (line 33)
- protected override string EmptyMessage { get; } (line 34)

### Methods
- protected override int GetItemCount() (line 36)
- protected override string GetLabel(int index) (line 38)
- protected override string GetSearchName(int index) (line 44)
  Returns text only for Option items; Header is excluded from search
- protected override void RefreshData() (line 50)
  Builds _items: one Header item (name + description), then one item per decision option
- protected override EnterAction OnEnter(int index) (line 82)
- protected override void OnAction(int index) (line 84)
  Header: re-announces item; Option: calls ExecuteOption
- protected override EscapeAction OnEscape() (line 99)
- protected override string GetOpenAnnouncement() (line 101)
  Returns the header text (index 0)
- protected override void StorePopup(object popup) (line 108)
  Reads world event, model, and state from popup via WorldEventReflection
- protected override void OnClosed() (line 114)
- private void ExecuteOption(int index) (line 124)
  Checks CanExecuteOption; on success calls ExecuteDecision (game closes popup itself)
- private string BuildOptionText(int index) (line 149)
  Appends ", disabled, {reason}" or ", disabled" for non-executable options
