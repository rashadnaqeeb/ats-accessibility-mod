# CustomGamesOverlay.cs
Accessible overlay for the Custom Games (Training Expeditions) popup.
Two-level navigation: Level 0 = top menu (13 sections), Level 1 = section items.

## class CustomGamesOverlay: MenuBase (line 13)

### Fields
- private enum SectionType (line 14)
  Values: Difficulty, Seed, Biome, Races, Reputation, Seasons, SeasonalEffects, Blight, Modifiers, TradeTowns, EmbarkGoods, EmbarkEffects, Embark
- private object _popup (line 31)
- private readonly List<SectionType> _sections (line 33)
  Initialized inline with all 13 section types in display order.
- private List<object> _difficulties (line 51)
- private List<(object biome, string name)> _biomes (line 52)
- private List<(object race, string name, bool selected)> _races (line 53)
- private List<(string name, int index, int max, float value)> _sliders (line 54)
- private List<CustomGamesReflection.ModifierInfo> _modifiers (line 55)
- private List<CustomGamesReflection.ModifierInfo> _filteredModifiers (line 56)
- private List<CustomGamesReflection.SeasonalEffectInfo> _seasonalEffects (line 57)
- private List<(string name, bool selected)> _tradeTowns (line 58)
- private List<(string name, int amount)> _embarkGoods (line 59)
- private List<(object effect, string name, bool selected)> _embarkEffects (line 60)
- private int _modifierCategoryIndex (line 63)
  0=WorldMap, 1=Daily, 2=Difficulty, 3=All
- private readonly string[] _categoryNames (line 64)
- private bool _isEditingSeed (line 67)
- private TMPro.TMP_InputField _seedInputField (line 68)
- private static readonly List<HelpEntry> _customGamesHelpEntries (line 154)

### Properties
- protected override string OverlayName { get; } (line 74)
- protected override string EmptyMessage { get; } (line 75)
- protected override int SearchItemCount { get; } (line 213)
  Only returns non-zero for Modifiers section at Level 1.
- protected override int SearchCurrentIndex { get; } (line 221)
- private SectionType CurrentSection { get; } (line 233)

### Methods
- protected override void StorePopup(object popup) (line 77)
- protected override int GetItemCount() (line 81)
- protected override string GetLabel(int index) (line 87)
- protected override void RefreshData() (line 93)
  No-op; data is loaded lazily when entering sections.
- protected override EnterAction OnEnter(int index) (line 96)
- protected override void OnAction(int index) (line 100)
  Level 0: Embark triggers embark, Seed starts editing, others enter section. Level 1: activates section item.
- protected override void OnSpace(int index) (line 120)
  Level 0: Seed randomizes, Blight toggles, SeasonalEffects cycles mode. Level 1: toggles section item.
- protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) (line 141)
  Applies shift-key multiplier (5x for most, 10x for EmbarkGoods).
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 157)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 159)
  When editing seed, intercepts Enter/Escape to exit edit mode and passes other keys through. Level 0 Right arrow enters section. Tab cycles modifier category.
- protected override EscapeAction OnEscape() (line 187)
  GoBack if Level > 0; PassThrough at Level 0 to let game close popup.
- protected override void OnGoBack() (line 194)
- protected override string GetOpenAnnouncement() (line 198)
- protected override void OnClosed() (line 204)
- protected override string GetSearchName(int index) (line 223)
- private string GetTopMenuItemAnnouncement(SectionType section) (line 235)
  Reads live data for each section to build a summary label (e.g. "Races: 3 selected").
- private string GetSectionItemLabel(int index) (line 301)
- private void EnterSection(SectionType section) (line 403)
  Guards Blight section entry when blight is disabled; loads data then advances to Level 1.
- private void StartSeedEdit() (line 426)
  Disables InputBlocker, focuses TMP input field, and sets _isEditingSeed flag.
- private void ExitSeedEdit() (line 446)
  Re-enables InputBlocker and deactivates the input field.
- private void RandomizeSeed() (line 462)
- private void LoadSectionData(SectionType section) (line 476)
  Clears all cached data then loads only the relevant collection for the given section.
- private void ClearCachedData() (line 529)
- private int GetSectionItemCount(SectionType section) (line 542)
- private void ActivateSectionItem() (line 579)
  Dispatches to select (Difficulty/Biome) or toggle based on current section type.
- private void ToggleSectionItem() (line 610)
- private void SelectDifficulty() (line 644)
  Calls GoBackToTopMenu on success.
- private void SelectBiome() (line 660)
  Calls GoBackToTopMenu on success.
- private void ToggleRace() (line 675)
- private void ToggleTradeTown() (line 694)
- private void ToggleSeasonalEffect() (line 713)
  Enforces per-type maximum before toggling.
- private void ToggleModifier() (line 738)
- private void ToggleEmbarkEffect() (line 752)
- private void ToggleBlight() (line 768)
  Reloads blight sliders after enabling.
- private void ToggleBlightFromMenu() (line 784)
  Variant called from Level 0 Space; re-announces top menu item on success.
- private void ToggleSeasonalEffectsModeFromMenu() (line 795)
  Variant called from Level 0 Space; re-announces top menu item on success.
- private void AdjustSlider(int delta) (line 810)
  Handles SeasonalEffects positive count, EmbarkGoods amount, and Reputation/Seasons/Blight sliders.
- private void CycleModifierCategory(int direction) (line 873)
  Wraps _modifierCategoryIndex and calls FilterModifiers; announces category name and count.
- private void FilterModifiers() (line 887)
  Populates _filteredModifiers from _modifiers based on _modifierCategoryIndex (3=All).
- private void GoBackToTopMenu() (line 903)
- private void TriggerEmbark() (line 914)
