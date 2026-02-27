# TrendsOverlay.cs
Accessible overlay for the TrendsPopup.
Provides navigation through goods and their storage operations.
Number keys toggle time frame for aggregating operations.

This overlay has parallel navigation axes: Left/Right navigates goods,
Up/Down navigates operations for the current good. MenuBase Level 0
tracks the operation index; _goodIndex is a separate axis.

## class TrendsOverlay: MenuBase (line 17)

### Fields
- private const int TICKS_10_SECONDS = 1 (line 19)
- private const int TICKS_1_MINUTE = 6 (line 20)
- private const int TICKS_5_MINUTES = 30 (line 21)
- private object _popup (line 24)
- private int _timeFrameTicks = TICKS_1_MINUTE (line 25)
- private List<string> _goods (line 28)
  Separate navigation axis; not managed by MenuBase indices
- private int _goodIndex (line 29)
- private List<TrendsReflection.AggregatedOperation> _operations (line 32)
  Navigated via MenuBase Level 0 (Up/Down)
- private static readonly List<HelpEntry> _trendsHelpEntries (line 86)

### Properties
- protected override string OverlayName { get; } (line 38)
- protected override string EmptyMessage { get; } (line 40)
- protected override int SearchItemCount { get; } (line 149)
  Searches the goods list (not operations)
- protected override int SearchCurrentIndex { get; } (line 151)
  Returns _goodIndex (not MenuBase CurrentIndex)

### Methods
- protected override int GetItemCount() (line 42)
- protected override string GetLabel(int index) (line 44)
- protected override void RefreshData() (line 50)
  Loads goods list; tries to sync _goodIndex with the popup's current selection
- protected override EnterAction OnEnter(int index) (line 66)
  Always returns None (no drill-down or action on operations)
- protected override void StorePopup(object popup) (line 68)
- protected override string GetOpenAnnouncement() (line 72)
- protected override void OnClosed() (line 80)
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 92)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 98)
  1/2/3: set time frame; Left/Right: navigate goods; Escape: PassThrough; returns null for Up/Down
- protected override EscapeAction OnEscape() (line 143)
- protected override string GetSearchName(int index) (line 153)
  Returns display name for the good at the given index in the goods list
- protected override void SearchMoveTo(int index) (line 158)
  Moves _goodIndex, refreshes operations, and announces the new good
- private void RefreshOperations() (line 168)
  Resets CurrentIndex to 0; reloads operations for current good and time frame
- private void SetTimeFrame(int ticks, string label) (line 183)
  Updates _timeFrameTicks, refreshes operations, and announces time frame + current good
- private void AnnounceTimeFrameAndGood(string timeFrameLabel) (line 195)
- private void AnnounceCurrentGood() (line 210)
  Announces current good display name and net change summary
- private string GetCurrentGoodDisplayName() (line 221)
- private int GetNetChangeFromOperations() (line 228)
  Sums TotalAmount across all operations for a single net value
- private string FormatNetChange(int amount) (line 239)
  Returns "no changes", "net +N", or "net -N"
- private string FormatAmount(int amount) (line 248)
  Returns "+N" for positive, "N" for zero/negative (used for individual operation labels)
