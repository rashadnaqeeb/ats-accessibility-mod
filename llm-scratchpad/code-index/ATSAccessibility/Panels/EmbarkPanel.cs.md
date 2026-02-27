# EmbarkPanel.cs
Virtual speech-only panel for accessible embark screen navigation.
Top-level menu with sections: Mission Info, Caravans, Spend Embark Points, Difficulty, Embark.
Each section uses two-panel navigation (categories/details) like StatsPanel.

Level 0: top menu (MenuBase standard nav).
Level 1: section content (all keys handled via HandleSpecialKey).

## class EmbarkPanel: MenuBase (line 18)

### Fields
- private object _currentField (line 42)
  The WorldField object passed to Open(worldField).
- private Vector3Int _cachedFieldPos (line 43)
  Cached field position, extracted once on open to avoid repeated reflection calls.
- private readonly string[] _topMenuItems (line 46)
  { "Mission Info", "Caravans", "Spend Embark Points", "Difficulty", "Embark" }
- private EmbarkSection _currentSection (line 55)
- private List<Category> _categories (line 58)
  Section-level categories (not the same as MenuBase's level-indexed items).
- private int _sectionCategoryIndex (line 59)
- private int _sectionDetailIndex (line 60)
- private bool _sectionFocusOnDetails (line 61)
  Whether focus is on the right (details) panel within a section.
- private static readonly List<HelpEntry> _embarkHelpEntries (line 163)

### Properties
- protected override string OverlayName { get; } (line 67)
- protected override string EmptyMessage { get; } (line 68)
- protected override int SearchItemCount { get; } (line 172)
  Returns 0 at top menu; otherwise returns detail count (SpendPoints, details-focused) or category count.
- protected override int SearchCurrentIndex { get; } (line 191)
  Returns _sectionDetailIndex when in SpendPoints or details-focused; otherwise _sectionCategoryIndex.

### Methods
- protected override int GetItemCount() (line 70)
  Level 0: top menu item count. Level 1+: always 0 (section nav is handled manually).
- protected override string GetLabel(int index) (line 75)
- protected override void RefreshData() (line 81)
  No-op; data is populated on-demand when entering sections.
- protected override EnterAction OnEnter(int index) (line 85)
  Level 0: Action for Embark (index 4), DrillDown for all others.
- protected override bool CanDrillDown(int index) (line 97)
  Level 0: true for all except index 4 (Embark). Level 1: false.
- protected override void OnAction(int index) (line 102)
  Level 0 index 4 only: calls TriggerEmbark().
- protected override void OnDrillDown(int index) (line 107)
  Resets section navigation state and builds categories for the selected section (BuildMissionInfoCategories, BuildCaravanCategories, BuildSpendPointsCategories, or BuildDifficultyCategories).
- protected override void OnGoBack() (line 134)
  Returns to TopMenu state and clears categories.
- protected override EscapeAction OnEscape() (line 139)
  Level 0: PassThrough (game shows confirm dialog). Level 1: GoBack (not normally reached; Level 1 Escape is handled in HandleSpecialKey).
- protected override string GetOpenAnnouncement() (line 145)
- protected override void OnOpened() (line 151)
- protected override void OnClosed() (line 155)
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 166)
- protected override string GetSearchName(int index) (line 199)
  SpendPoints or details-focused: detail string. Otherwise: category name.
- protected override void SearchMoveTo(int index) (line 215)
  Moves to the found index and announces appropriately for the current section mode.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 232)
  Level 1+: delegates to HandleSectionKey. Level 0: returns null for MenuBase handling.
- private bool HandleSectionKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 242)
  Lets search handle keys first, then dispatches to HandleSpendPointsKey or HandleStandardSectionKey.
- private bool HandleStandardSectionKey(KeyCode keyCode) (line 258)
  Handles Up/Down/Home/End/Right/Left/Enter/Escape for MissionInfo, Caravans, Difficulty sections. Right drills into details; Left returns to categories or top menu; Escape blocks cancel and navigates back.
- private bool HandleSpendPointsKey(KeyCode keyCode) (line 358)
  Handles SpendPoints section: Up/Down = item nav, Left/Right = panel nav (Left at first panel returns to top menu), Enter = toggle bonus, Escape = back to top menu.
- private void ReturnToTopMenu() (line 416)
  Clears categories, resets to Level 0, announces current top menu item.
- public new void Open(object worldField) (line 430)
  Hides MenuBase.Open(); stores worldField, caches field position, caches expensive instance references via EmbarkReflection.CacheInstancesOnOpen, then calls base Open().
- private void NavigateCategory(int direction) (line 449)
- private void AnnounceCurrentCategory() (line 457)
  Announces "{Name}[, {Value}][. Press right for details]".
- private void ActivateCategory() (line 476)
  Section-specific activation: Caravans calls SelectCaravan; Difficulty calls SelectDifficulty; others enter details.
- private void NavigateDetail(int direction) (line 522)
- private void AnnounceCurrentDetail() (line 532)
- private void ActivateDetail() (line 543)
  SpendPoints: toggles bonus. Others: re-reads detail.
- private void NavigateSpendPointsPanel(int direction) (line 568)
  Wraps _sectionCategoryIndex and announces the panel with points summary.
- private void NavigateSpendPointsItem(int direction) (line 576)
- private void AnnounceSpendPointsPanel() (line 589)
  Announces "{CategoryName}. {used} of {total} points spent".
- private void AnnounceSpendPointsItem() (line 599)
- private void ActivateSpendPointsItem() (line 609)
  Calls ToggleBonus on the selected item.
- private void BuildMissionInfoCategories() (line 631)
  Builds categories for biome, difficulty, modifiers, seal fragments, rewards, and embark points. Reads from WorldMapReflection and EmbarkReflection.
- private void BuildCaravanCategories() (line 736)
  Builds one category per caravan slot (always 3), marking selected and locked slots.
- private List<string> BuildCaravanDetails(object caravan) (line 775)
  Builds detail strings for species, base goods, and bonus goods.
- private void SelectCaravan(object caravan) (line 808)
  Calls EmbarkReflection.SetPickedCaravan, rebuilds categories, announces "Caravan selected".
- private void BuildSpendPointsCategories(bool announce = true) (line 823)
  Builds three panels: Available Effects, Available Goods, and Spent. Each includes DataList for activation.
- private void ToggleBonus(string categoryName, object item) (line 907)
  Determines item type from type name or category name, calls ToggleEffectBonus or ToggleGoodBonus, announces result and remaining points, rebuilds section preserving position.
- private void BuildDifficultyCategories() (line 954)
  Builds one category per available difficulty, marking selected and locked. Starts at current difficulty.
- private bool IsSameDifficulty(object diff1, object diff2) (line 1010)
  Compares difficulty by index.
- private void SelectDifficulty(object difficulty) (line 1015)
  Checks if locked, calls EmbarkReflection.SetDifficulty, rebuilds, announces selected name.
- private void TriggerEmbark() (line 1041)
  Validates caravan selected and points not overspent before calling EmbarkReflection.TriggerEmbark(). Panel stays open (closes via game callback on success).
- private Vector3Int GetFieldPosition() (line 1073)
  Returns cached field position.
- private Vector3Int GetFieldPositionInternal() (line 1081)
  Extracts CubicPos from the WorldField object via reflection. Called once on open.
