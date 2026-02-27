# FishingHutNavigator.cs
Navigator for FishingHut buildings.
Top section shows Status (Active/Paused) with Enter/Space to toggle.
Followed by Bait, Recipes, Workers, and Upgrades sections.

## class FishingHutNavigator: BuildingSectionNavigator (line 12)

### Fields (private enum SectionType, line 17)
- `Status` - Active/Paused toggle at top
- `Bait` - Bait mode settings
- `Recipes` - Fish types to catch
- `Workers`
- `Upgrades`

### Fields
- `private` `string[]` `_sectionNames` (line 29)
- `private` `SectionType[]` `_sectionTypes` (line 30)
- `private` `bool` `_isSleeping` (line 31)
- `private` `bool` `_canSleep` (line 32)
- `private` `int` `_baitMode` (line 35)
- `private` `int` `_baitCharges` (line 36)
- `private` `string` `_baitIngredient` (line 37)
- `private` `string[]` `_baitModeNames` (line 38)
- `private` `List<RecipeInfo>` `_recipes` (line 41)

### Fields (private struct RecipeInfo, line 47)
- `object` `RecipeState`
- `string` `ModelName`
- `string` `ProductName`
- `bool` `IsActive`

### Properties
- `protected override` `string` `NavigatorName` (line 58) - Returns `"FishingHutNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 60)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 64) - Status: 0; Bait: 3 (mode, charges, ingredient); Recipes: recipe count; Workers/Upgrades: delegate
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 84) - Bait item 0: mode sub-items; Workers: races; Upgrades: perks
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 106) - Status: announces "Active" or "Paused"
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 117)
- `protected override` `bool` `ToggleBuildingSleep()` (line 137) - Refreshes worker IDs after waking
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 157) - Status section: `ToggleBuildingSleep`
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 167) - Recipes: toggle directly on Enter (like Camp buildings)
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 177)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 183)
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 193) - Bait+index0: set bait mode and return to Level 1; Workers: assign/unassign; Upgrades: purchase
- `protected override` `void` `RefreshData()` (line 213)
- `protected override` `void` `ClearData()` (line 263)

#### Bait section
- `private` `void` `RefreshBaitData()` (line 279)
- `private` `void` `AnnounceBaitItem(int itemIndex)` (line 284) - Re-fetches bait data; Index 0: mode name; Index 1: charges; Index 2: ingredient name
- `private` `void` `AnnounceBaitModeSubItem(int subItemIndex)` (line 316)
- `private` `bool` `PerformBaitModeSubItemAction(int subItemIndex)` (line 331) - Sets bait mode and returns to Level 1 on success

#### Recipes section
- `private` `void` `RefreshRecipes()` (line 358)
- `private` `void` `AnnounceRecipeItem(int itemIndex)` (line 373)
- `private` `string` `GetRecipeDisplayName(RecipeInfo recipe)` (line 386) - Prefers product name, falls back to model name
- `private` `void` `ToggleRecipe(int itemIndex)` (line 398)
- `private` `string` `CleanupName(string name)` (line 425) - Strips recipe prefixes/tags and replaces underscores
