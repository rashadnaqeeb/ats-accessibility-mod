# ProductionNavigator.cs
Navigator for production buildings (Workshop, Farm, Mine, Camp, etc.).
Top section shows Status (Active/Paused) with Enter/Space to toggle.
Followed by Workers, Recipes, and other sections based on building type.

## class ProductionNavigator: BuildingSectionNavigator (line 13)

### Fields (private enum SectionType, line 18)
- `Status` - Active/Paused toggle at top
- `Workers`
- `Recipes`
- `Rainpunk` - Rainpunk engine control (workshops only)
- `Inputs` - Ingredients storage (input goods)
- `Outputs` - Production storage (output goods)
- `Settings` - Camp mode settings
- `Fields` - Farm field capacity
- `Upgrades` - Building upgrades

### Fields
- `private` `string[]` `_sectionNames` (line 34)
- `private` `SectionType[]` `_sectionTypes` (line 35) - Maps section index to section type
- `private` `string` `_buildingName` (line 36)
- `private` `bool` `_isSleeping` (line 37)
- `private` `bool` `_canSleep` (line 38) - Whether building supports pausing
- `private` `bool` `_isCamp` (line 39) - Camp/gathering buildings have simple recipes (no submenu)
- `private` `List<RecipeInfo>` `_recipes` (line 42)
- `private` `List<(string goodName, string displayName, int amount)>` `_inputGoods` (line 45) - Ingredients storage
- `private` `List<(string goodName, string displayName, int amount)>` `_outputGoods` (line 46) - Production storage
- `private` `bool` `_hasInputStorage` (line 47)
- `private` `bool` `_hasOutputStorage` (line 48)
- `private` `bool` `_isFarm` (line 51)
- `private` `int` `_farmSownFields` (line 52)
- `private` `int` `_farmPlowedFields` (line 53)
- `private` `int` `_farmTotalFields` (line 54) - Total = placed farmfields + empty grass
- `private` `int` `_farmPlacedFields` (line 55) - Actual placed farmfield buildings
- `private` `int` `_campMode` (line 58)
- `private` `string[]` `_campModeNames` (line 59)
- `private` `bool` `_hasRainpunk` (line 62)
- `private` `bool` `_rainpunkUnlocked` (line 63)
- `private` `int` `_engineCount` (line 64)
- `private const int` `RAINPUNK_ITEM_WATER_STORED = 0` (line 894)
- `private const int` `RAINPUNK_ITEM_WATER_USE = 1` (line 895)
- `private const int` `RAINPUNK_ITEM_BLIGHT = 2` (line 896)
- `private const int` `RECIPE_SUBITEM_STATUS = 0` (line 1050)
- `private const int` `RECIPE_SUBITEM_PRIORITY = 1` (line 1051)
- `private const int` `RECIPE_SUBITEM_PRODUCTION = 2` (line 1052)
- `private const int` `RECIPE_SUBITEM_LIMIT = 3` (line 1053)
- `private const int` `RECIPE_SUBITEM_INGREDIENTS_START = 4` (line 1054)

### Fields (private struct RecipeInfo, line 70)
- `object` `RecipeState`
- `string` `ModelName`
- `string` `ProductName` - The good being produced
- `bool` `IsActive`
- `int` `Limit`
- `bool` `IsLimitLocal`
- `int` `Priority`

### Properties
- `protected override` `string` `NavigatorName` (line 84) - Returns `"ProductionNavigator"`

### Methods

#### BuildingSectionNavigator abstract implementations
- `protected override` `string[]` `GetSections()` (line 86)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 90)
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 118) - Outputs: 2 sub-items (force transport, auto-deliver); Inputs: 1 (return to warehouse); Workers: races; Recipes: 4 + ingredient slots (0 for Camp)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 158) - Status announced dynamically as "Active" or "Paused"
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 169)
- `protected override` `bool` `PerformItemAction(int sectionIndex, int itemIndex)` (line 201) - Recipes: toggle; Rainpunk: unlock action
- `protected override` `bool` `ToggleBuildingSleep()` (line 231)
- `protected override` `bool` `PerformSectionAction(int sectionIndex)` (line 251) - Status section: `ToggleBuildingSleep`
- `protected override` `string` `GetNoSubItemsMessage(int sectionIndex, int itemIndex)` (line 261)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 267)
- `protected override` `void` `AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers)` (line 289) - Recipes: adjusts priority (Level 3), priority sub-item, or limit; Rainpunk: engine level
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 308)
- `protected override` `void` `RefreshData()` (line 337) - Caches all building data and dynamically builds sections list
- `protected override` `void` `ClearData()` (line 430)
- `protected override` `int` `GetSubSubItemCount(int sectionIndex, int itemIndex, int subItemIndex)` (line 1234) - Only recipe ingredient slots have sub-sub-items; requires multiple options
- `protected override` `void` `AnnounceSubSubItem(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 1257) - Announces ingredient option: amount, name, priority, storage count, enabled/disabled
- `protected override` `bool` `PerformSubSubItemAction(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 1287) - Toggles ingredient allowed state
- `protected override` `string` `GetSectionName(int sectionIndex)` (line 1323)
- `protected override` `string` `GetItemName(int sectionIndex, int itemIndex)` (line 1330)
- `protected override` `string` `GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex)` (line 1364)
- `protected override` `string` `GetSubSubItemName(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex)` (line 1413)

#### Recipe section
- `private` `void` `RefreshRecipes()` (line 457)
- `private` `string` `FormatPriority(int priority)` (line 476) - Returns "0 (lowest)" / "3 (highest)" / number
- `private` `void` `AdjustRecipePriority(int recipeIndex, int delta)` (line 484)
- `private` `void` `AdjustIngredientPriority(int recipeIndex, int delta)` (line 506) - Adjusts priority of a specific ingredient option at Level 3
- `private` `void` `AnnounceInputItem(int itemIndex)` (line 566) - Re-fetches storage before announcing
- `private` `void` `AnnounceInputSubItem(int itemIndex, int subItemIndex)` (line 584)
- `private` `bool` `PerformInputSubItemAction(int itemIndex, int subItemIndex)` (line 599) - Returns goods to warehouse; goes back to Level 1
- `private` `void` `AnnounceOutputItem(int itemIndex)` (line 627) - Re-fetches storage; shows delivery state
- `private` `void` `AnnounceOutputSubItem(int itemIndex, int subItemIndex)` (line 654)
- `private` `bool` `PerformOutputSubItemAction(int itemIndex, int subItemIndex)` (line 682) - subItemIndex 0: toggle force delivery; 1: toggle constant delivery
- `private` `void` `AnnounceRecipeItem(int itemIndex)` (line 720)
- `private` `string` `GetRecipeDisplayName(RecipeInfo recipe)` (line 741) - Prefers product name; falls back to model name; applies `CleanupName`
- `private` `string` `CleanupName(string name)` (line 757) - Strips recipe prefixes and replaces underscores
- `private` `void` `ToggleRecipe(int itemIndex)` (line 771)

#### Settings section (Camp modes)
- `private` `void` `AnnounceSettingsItem(int itemIndex)` (line 803) - Announces current cutting mode
- `private` `void` `AnnounceSettingsSubItem(int subItemIndex)` (line 813)
- `private` `bool` `PerformSettingsSubItemAction(int subItemIndex)` (line 828) - Sets camp mode and returns to Level 1

#### Fields section (Farm)
- `private` `void` `AnnounceFieldsItem(int itemIndex)` (line 856) - Index 0: summary of placed farmfields + available fertile soil; 1: sown; 2: plowed

#### Rainpunk section
- `private` `int` `GetRainpunkItemCount()` (line 898) - If not unlocked: 1 (unlock item); else 2 + optional blight + engine count
- `private` `int` `GetRainpunkEngineStartIndex()` (line 912) - Returns 3 if blight item present, else 2
- `private` `void` `AnnounceRainpunkItem(int itemIndex)` (line 920)
- `private` `string` `GetRainpunkItemName(int itemIndex)` (line 959)
- `private` `void` `AnnounceEngine(int engineIndex)` (line 978)
- `private` `void` `AdjustEngineLevel(int itemIndex, int delta)` (line 999)

#### Recipe sub-items
- `private` `void` `RefreshStorage()` (line 534)
- `private` `int` `GetRecipeSubItemCount(int recipeIndex)` (line 1056) - Returns 4 + ingredient slot count
- `private` `void` `AnnounceRecipeSubItem(int recipeIndex, int subItemIndex)` (line 1066)
- `private` `void` `AnnounceProductionInfo(object recipeState)` (line 1105) - Dispatches to `AnnounceFarmProductionInfo` for farms
- `private` `void` `AnnounceFarmProductionInfo(object recipeState)` (line 1130) - Announces crop, amount, plant/harvest times (adjusted by farm rates), stars
- `private` `void` `AnnounceIngredientSlot(object recipeState, int slotIndex)` (line 1164) - Lists enabled and disabled ingredient options for a slot
- `private` `void` `AdjustRecipeLimit(int recipeIndex, int delta)` (line 1203) - 0 = unlimited; +1 from unlimited sets to 1
- `private` `string` `GetRecipeItemName(int itemIndex)` (line 1356)
- `private` `string` `GetRecipeSubItemName(int recipeIndex, int subItemIndex)` (line 1386)
- `private` `string` `GetSettingsSubItemName(int subItemIndex)` (line 1406)
