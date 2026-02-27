# RecipesOverlay.cs
Accessible overlay for the Recipes popup (F2 Menu Hub -> Recipes).
Provides keyboard navigation of recipes organized by produced good,
with controls for global production limits and recipe toggling.

Level 0 = goods, Level 1 = recipes for the selected good.

## class RecipesOverlay: MenuBase (line 16)

### Fields
- private List<RecipesReflection.GoodInfo> _goods (line 18)
- private bool _showAllGoods (line 19)
  false = unlocked buildings only, true = include locked buildings
- private static readonly List<HelpEntry> _recipesHelpEntries (line 122)

### Properties
- protected override string OverlayName { get; } (line 25)
- protected override string EmptyMessage { get; } (line 26)
- protected override int SearchItemCount { get; } (line 131)
  Only searches at Level 0 (goods list)

### Methods
- protected override int GetItemCount() (line 28)
- protected override string GetLabel(int index) (line 36)
- protected override void RefreshData() (line 66)
- protected override EnterAction OnEnter(int index) (line 70)
- protected override void OnAction(int index) (line 88)
  At Level 1: announces full recipe detail
- protected override void OnSpace(int index) (line 93)
  At Level 1: toggles current recipe active/inactive
- protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) (line 98)
  At Level 0: adjusts global production limit; Shift multiplies delta by 10
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 107)
  Handles Ctrl+T (toggle show all) and Plus key for limit adjustment
- public override IReadOnlyList<HelpEntry> GetHelpEntries() (line 125)
- protected override string GetSearchName(int index) (line 134)
- protected override string GetOpenAnnouncement() (line 144)
- protected override void OnClosed() (line 155)
- private void AdjustLimit(int delta) (line 164)
  Updates global limit via reflection and pushes to all built workshops following the global limit
- private void ToggleCurrentRecipe() (line 193)
- private void ToggleShowAll() (line 218)
  Resets navigation to Level 0, index 0 after toggle
- private RecipesReflection.GoodInfo GetCurrentGood() (line 242)
- private RecipesReflection.RecipeInfo GetCurrentRecipe() (line 248)
- private void AnnounceRecipeFull() (line 263)
  Announces current recipe in encyclopedia format: "{Output} x {Amount}: {Inputs} {Time}{Stars}"
