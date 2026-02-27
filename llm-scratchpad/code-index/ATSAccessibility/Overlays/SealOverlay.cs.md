# SealOverlay.cs
Accessible overlay for the Seal building panel in Sealed Forest biome.
Two-level navigation: sections -> offerings detail.
Level 0 = 5 sections (Effects, Progress, Dialogue, Offerings, Reward).
Level 1 = offerings list (Enter/Space delivers).

## class SealOverlay: MenuBase (line 15)

### Fields
- private enum Section { Effects, Progress, Dialogue, Offerings, Reward } (line 20)
- private static readonly Section[] _allSections (line 21)
- private object _seal (line 27)
- private bool _sealUnavailable (line 28)
- private object _currentStage (line 31)
  SealKitState
- private object _currentStageModel (line 32)
  SealKitModel
- private Array _offerings (line 33)
  SealPartModel[]
- private Array _offeringOrders (line 34)
  OrderState[]
- private static PropertyInfo _effectDisplayNameProperty (line 37)
- private static PropertyInfo _effectDescriptionProperty (line 38)
- private static bool _effectPropsCached (line 39)

### Properties
- protected override string OverlayName { get; } (line 45)
- protected override string EmptyMessage { get; } (line 46)
- protected override int SearchItemCount { get; } (line 150)
  Only searches at Level 1 (offerings); 0 at Level 0

### Methods
- protected override int GetItemCount() (line 48)
- protected override string GetLabel(int index) (line 57)
- protected override void RefreshData() (line 67)
- protected override EnterAction OnEnter(int index) (line 81)
  Level 0: DrillDown only for Offerings section; all others are Action. Level 1: Action (deliver)
- protected override void OnAction(int index) (line 91)
  Level 0: re-announces section; Level 1: calls TryDeliver
- protected override void OnSpace(int index) (line 100)
  Level 1 only: calls TryDeliver
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 105)
  Always returns null (no special keys)
- protected override void OnDrillDown(int index) (line 109)
  No-op; offerings already loaded in RefreshData
- protected override void AnnounceCurrentItem() (line 116)
  Custom: routes to AnnounceSection at Level 0, AnnounceOffering at Level 1
- protected override string GetOpenAnnouncement() (line 123)
  Returns null when seal is available (OnOpened handles announcement)
- protected override void OnOpened() (line 133)
  Calls AnnounceSection if seal is available
- protected override void OnClosed() (line 138)
- protected override string GetSearchName(int index) (line 152)
- private void AnnounceSection() (line 164)
  Dispatches to the appropriate Announce* method based on current section index
- private void AnnounceEffects() (line 187)
  Reads plague state; announces current plague or next plague with time remaining
- private void AnnounceProgress() (line 220)
  Announces stage N of M and which stages are completed
- private void AnnounceDialogue() (line 236)
  Reads and announces the current stage's dialogue text
- private void AnnounceOffering(int index) (line 244)
  Announces name, objectives, description, and deliverable status of an offering
- private void AnnounceReward() (line 270)
  Reads and announces the current stage's completion reward
- private void TryDeliver() (line 292)
  Checks deliverability, calls SealReflection.CompleteOffering, refreshes stage data, closes if seal complete
- private bool CanDeliverOffering(object orderState, object offering) (line 343)
- private string GetObjectivesText(object offering, object orderState) (line 353)
- private static void EnsureEffectPropertyCached() (line 365)
  Caches DisplayName and Description properties on Eremite.Model.EffectModel
- private static string GetEffectDisplayName(object effectModel) (line 380)
- private static string GetEffectDescription(object effectModel) (line 388)
  Also strips rich text tags via OrdersReflection.StripRichText
