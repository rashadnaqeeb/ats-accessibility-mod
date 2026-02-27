# MysteriesPanel.cs
Virtual speech panel for settlement modifiers.
Five categories: Positive Mysteries, Negative Mysteries, Effects, Cornerstones, Perks.
Level 0 = categories, Level 1 = items within a category.
Cross-category item navigation flows between categories on Up/Down at boundaries.

## class MysteriesPanel: MenuBase (line 17)

### Fields
- private List<Category> _categories (line 52)
- private static FieldInfo _sesModelField (line 55)
- private static FieldInfo _sesSeasonField (line 56)
- private static FieldInfo _sesIsActiveField (line 57)
- private static FieldInfo _sesIsPositiveField (line 58)
- private static bool _sesFieldsCached (line 59)
- private static PropertyInfo _effectDisplayNameProperty (line 62)
- private static PropertyInfo _effectDescriptionProperty (line 63)
- private static PropertyInfo _effectIsPositiveProperty (line 64)
- private static bool _modelFieldsCached (line 65)
- private static FieldInfo _conditionCategoryField (line 68)
- private static FieldInfo _conditionAmountField (line 69)
- private static FieldInfo _categoryDisplayNameField (line 70)
- private static bool _conditionFieldsCached (line 71)
- private int _currentCategoryIndex (line 74)
  Compatibility alias mapping to _indices[0].
- private int _currentItemIndex (line 75)
  Compatibility alias mapping to _indices[1].

### Properties
- protected override string OverlayName { get; } (line 81)
- protected override string EmptyMessage { get; } (line 82)

### Methods
- protected override int GetItemCount() (line 84)
  Level 0: category count. Level 1: item count for current category.
- protected override string GetLabel(int index) (line 94)
  Level 0: "{Name}, {count}". Level 1: delegates to BuildItemAnnouncement(index).
- protected override string GetSearchName(int index) (line 106)
- protected override void RefreshData() (line 121)
  Builds all five categories by calling GetMysteriesByType, GetActiveEffects, GetCornerstoneItems, GetActivePerks. Passes exclusion sets between them to avoid duplicates.
- protected override EnterAction OnEnter(int index) (line 180)
  Level 0: DrillDown if category has items. Level 1: None.
- protected override EscapeAction OnEscape() (line 192)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 194)
  Level 0 LeftArrow passes to InfoPanelMenu. Level 1 Up/Down (when search not active) calls NavigateItemAcrossCategories.
- protected override string GetOpenAnnouncement() (line 207)
- protected override void OnClosed() (line 222)
- public bool ProcessKeyEvent(KeyCode keyCode) (line 231)
  Bridge method for InfoPanelMenu; wraps ProcessKey with default modifiers.
- private void NavigateItemAcrossCategories(int direction) (line 242)
  Moves to next/previous item, skipping empty categories and wrapping. Announces category name when crossing a boundary.
- private void AnnounceCategoryAndItem() (line 277)
  Announces "{CategoryName}. {itemText}" when crossing category boundaries.
- private string BuildItemAnnouncement(int itemIndex) (line 297)
  Formats item text based on ItemType: Mysteries get "Active/Inactive, Name, Season. Description Condition"; Effects/Cornerstones get "Name. Description"; Perks get "Name [xN]. Description".
- private (List<MysteryItem> positive, List<MysteryItem> negative) GetMysteriesByType(HashSet<string> outMysteryNames) (line 351)
  Iterates SeasonalEffects dictionary, collects internal model names (and wrapped effect names) into outMysteryNames for exclusion, splits items into positive/negative lists.
- private List<MysteryItem> GetActiveEffects(HashSet<string> outEffectNames) (line 403)
  Gets effects from EffectsService.GetAllConditions(), skipping IsPerk=true entries. Collects internal names into outEffectNames and deduplicates by display name.
- private List<MysteryItem> GetCornerstoneItems(List<string> cornerstones) (line 439)
- private List<MysteryItem> GetActivePerks(HashSet<string> mysteryNames, HashSet<string> cornerstoneNames, HashSet<string> effectNames) (line 461)
  Gets perks from PerksService, excluding hidden perks and any names already in the three exclusion sets.
- private void EnsureSeasonalEffectStateFields() (line 499)
  Lazy-caches FieldInfo for SeasonalEffectState.model/season/isActive/isPositive (public fields, not properties).
- private void EnsureModelFields() (line 534)
  Lazy-caches PropertyInfo for EffectModel.DisplayName/Description/isPositive.
- private bool GetIsActive(object state) (line 566)
- private MysteryItem CreateMysteryItem(string key, object state) (line 579)
  Builds a MysteryItem from a SeasonalEffectState, resolving display name and description via runtime type reflection on the model.
- private void EnsureConditionFields(object firstCondition) (line 649)
  Lazy-caches FieldInfo for NeedCategoryCondition.category/amount and the category's displayName field. Called on first conditional mystery encountered.
- private string GetConditionText(object seasonalEffectModel) (line 683)
  Extracts hostility level requirement and need-category conditions from the model, returning a "requires ..." string.
- private MysteryItem CreateEffectItem(object effectModel) (line 735)
  Creates a MysteryItem from an EffectModel. Falls back to internal name if display name is missing or has broken localization.
- private MysteryItem CreateCornerstoneItem(string effectName) (line 773)
- private MysteryItem CreatePerkItem(string effectName, int stacks) (line 809)
