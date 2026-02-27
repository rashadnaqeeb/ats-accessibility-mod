# DialogueOverlay.cs
Accessible overlay for the HomePopup (NPC dialogue navigation).
Provides flat list navigation through header, dialogue text, and choices/continue.
Queues rapid events to prevent missing dialogue.

## class DialogueOverlay: MenuBase (line 14)

### Fields
- private enum ItemType (line 15)
  Values: Header, Dialogue, Continue, Choice
- private class ListItem (line 17)
  Fields: ItemType Type, string Text, NarrationReflection.ChoiceInfo Choice
- private enum EventType (line 23)
  Values: Dialogue, Branch
- private class QueuedEvent (line 24)
  Fields: EventType Type, object Data
- private List<ListItem> _items (line 30)
- private object _currentPopup (line 31)
- private object _currentDialogue (line 32)
- private object _currentBranch (line 33)
- private Queue<QueuedEvent> _eventQueue (line 36)
- private bool _processingEvent (line 37)
- private IDisposable _dialogueSub (line 40)
- private IDisposable _branchSub (line 41)

### Properties
- protected override string OverlayName { get; } (line 47)
- protected override string EmptyMessage { get; } (line 48)

### Methods
- protected override int GetItemCount() (line 50)
- protected override string GetLabel(int index) (line 52)
- protected override void RefreshData() (line 57)
  No-op; list is rebuilt by event handlers, not by the standard open/refresh flow.
- protected override EnterAction OnEnter(int index) (line 59)
- protected override void OnAction(int index) (line 61)
  Continue: clears queue and calls ExecuteTransition. Choice: clears queue and calls SelectChoice. Header/Dialogue: processes next queued event if any, otherwise re-announces.
- protected override void StorePopup(object popup) (line 99)
- protected override string GetOpenAnnouncement() (line 103)
- protected override void OnOpened() (line 110)
  Subscribes to NarrationReflection dialogue and branch events.
- protected override void OnClosed() (line 122)
  Disposes subscriptions and clears all state including event queue.
- protected override EscapeAction OnEscape() (line 136)
- protected override void AnnounceCurrentItem() (line 138)
  Overrides default to use Speech.Say directly rather than going through GetLabel.
- protected override string GetSearchName(int index) (line 147)
  Returns text only for Choice items; Header and Dialogue items are not searchable.
- private void OnDialogueRequested(object dialogue) (line 156)
  Event handler: enqueues dialogue event and starts processing if not already in progress.
- private void OnBranchRequested(object branch) (line 165)
  Event handler: enqueues branch event and starts processing if not already in progress.
- private void ProcessNextEvent() (line 174)
  Dequeues next event, builds appropriate list (BuildDialogueList or BuildBranchList), resets to index 0, and announces.
- private void BuildDialogueList(object dialogue) (line 208)
  Builds: NPC header, dialogue text, and optional "Continue" item if transition exists.
- private void BuildBranchList(object branch) (line 234)
  Builds: NPC header, currently displayed text (from popup), and all available choices.
