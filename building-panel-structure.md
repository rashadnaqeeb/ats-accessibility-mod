# Building Panel Structure

This document describes the game's building panel UI structure, based on analysis of the decompiled game source code.

## Overview

When a player clicks on a building, a panel appears showing building information and controls. The panel type depends on the building type, and panels can have multiple tabs.

## Class Hierarchy

```
PreviewPanel (base)
└── BuildingPanel (abstract)
    ├── ProductionBuildingPanel (abstract)
    │   ├── WorkshopPanel
    │   ├── FarmPanel
    │   ├── MinePanel
    │   ├── GathererHutPanel
    │   ├── CampPanel
    │   ├── CollectorPanel
    │   ├── BlightPostPanel
    │   ├── RainCatcherPanel
    │   ├── FishingHutPanel
    │   └── ExtractorPanel
    ├── HearthPanel
    ├── HousePanel
    ├── StoragePanel
    ├── InstitutionPanel
    ├── DecorationPanel
    ├── SimplePanel
    ├── HydrantPanel
    ├── RelicPanel
    ├── ShrinePanel
    ├── PortPanel
    └── PoroPanel
```

## Implementation Hooks (For Accessibility)

### Detecting Building Panel Open/Close

**Static Field:** `BuildingPanel.currentBuilding` holds the currently shown building (or null if no panel open)

**Events (via GameMB.GameBlackboardService):**
- `OnBuildingPanelShown` - Fires when building panel opens, passes the Building
- `OnBuildingPanelClosed` - Fires when building panel closes, passes the Building
- `BuildingFinished` - Fires when construction completes
- `FinishedBuildingEarlyRemoved` / `UnfinishedBuildingEarlyRemoved` - Building destroyed

**Picked Object:** `GameMB.GameInputService.PickedObject.Value` - Currently picked/selected object

### Building Selection Flow

1. Player clicks building → `Building.OnClicked()`
2. Building calls `OnPicked()` (abstract, each building type implements)
3. `OnPicked()` calls panel's `Show(this)` method (e.g., `WorkshopPanel.Instance.Show(this)`)
4. Panel sets `BuildingPanel.currentBuilding = building`
5. Panel fires `OnBuildingPanelShown` event
6. Panel calls `SetUpBuilding()` to populate UI

### Building Data Access Patterns

**Getting current building:** `BuildingPanel.currentBuilding`

**Building identity:**
- `building.BuildingModel.displayName.Text` - Localized name
- `building.BuildingModel.Description` - Localized description
- `building.BuildingModel.Name` - Internal name (for lookups)
- `building.Id` - Unique instance ID

**Building state:**
- `building.BuildingState.finished` - Is construction complete
- `building.BuildingState.isSleeping` - Is paused
- `building.HasUpgrades` - Can be upgraded

**Type detection:** Check inheritance or use `is` operator:
- `building is ProductionBuilding` - Has workers, recipes
- `building is Workshop` - Specific building type

### BuildingsService Access

**Get service:** `GameMB.BuildingsService` (or via reflection `GameReflection.GetBuildingsService()`)

**Typed dictionaries (Dictionary<int, T>):**
- `Workshops`, `Farms`, `Mines`, `GathererHuts`, `Camps`, `Collectors`
- `Houses`, `Hearths`, `Storages`, `Institutions`, `Decorations`
- `BlightPosts`, `Hydrants`, `Extractors`, `RainCatchers`
- `Relics`, `Shrines`, `Ports`, `Poros`
- `Buildings` - All buildings by ID
- `ProductionBuildings` - List of all production buildings

### Shared Sub-Panel Data Access

**BuildingWorkersPanel:**
- `building.Workplaces` (WorkplaceModel[]) - Workplace slots
- `building.Workers` (int[]) - Villager IDs per slot (0 = empty)
- `GameMB.ActorsService.GetActor(workerId)` - Get villager details

**BuildingStoragePanel:**
- `storage.Goods.goods` (Dictionary<string, int>) - Good name → amount
- `storage.Goods.GetFullAmount(goodName)` - Amount including reserved
- `storage.Goods.GetDeliveryState(goodName)` - Delivery toggle state

**BuildingEffectsPanel:**
- `GameMB.StateService.Effects.perks` - All active perks
- `GameMB.GameModelService.GetEffect(perkName).HasImpactOn(buildingModel)` - Check if affects this building

---

## Recipe and Good Data Access

### Good/Item Names

**GoodModel:**
- `good.displayName.Text` - Localized item name
- `good.Description` - Full description with sources
- `good.ShortDescription` - Brief description
- `good.GetNameWithIcon()` - Name with sprite icon markup
- `good.category` - GoodCategoryModel reference

**GoodRef (amount + good reference):**
- `goodRef.DisplayName` - Shortcut to `good.displayName.Text`
- `goodRef.amount` - Quantity
- `goodRef.ToGood()` - Convert to Good struct

### Recipe Details

**Base RecipeModel:**
- `recipe.Name` - Internal recipe name
- `recipe.GetProducedGood()` - Output good name (abstract)
- `recipe.GetAllIngredients()` - List of all input GoodModels
- `recipe.grade` - RecipeGradeModel (efficiency tier)
- `recipe.GetGradeDescription()` - Localized grade text

**WorkshopRecipeModel:**
- `recipe.producedGood` (GoodRef) - Output with amount
- `recipe.requiredGoods` (GoodsSet[]) - Ingredient slots, each with alternatives
- `recipe.productionTime` (float) - Base production time in seconds

**GoodsSet (ingredient alternatives):**
- `goodsSet.goods` (GoodRef[]) - Alternative ingredients for this slot

**Getting recipe models from Settings:**
- `MB.Settings.GetWorkshopRecipe(recipeName)`
- `MB.Settings.GetFarmRecipe(recipeName)`
- `MB.Settings.GetMineRecipe(recipeName)`
- `MB.Settings.GetGathererHutRecipe(recipeName)`
- `MB.Settings.GetFishingHutRecipe(recipeName)`
- `MB.Settings.GetCollectorRecipe(recipeName)`
- `MB.Settings.GetInstitutionRecipe(recipeName)`
- `MB.Settings.GetRecipe(recipeName)` - Generic (returns base RecipeModel)

### Production Progress

**ProductionState (base):**
- `state.progress` (float 0-1) - Completion percentage
- `state.recipe` (string) - Recipe name being produced
- `state.worker` (int) - Villager ID doing the work
- `state.index` (int) - Workplace index
- `state.WasStarted` (bool) - Is production in progress
- `state.IsFinished` (bool) - Is progress >= 1

**WorkshopProductionState (extends ProductionState):**
- `state.product` (Good) - What's being made
- `state.ingredients` (List<Good>) - What's being consumed

**Accessing production per building:**
- Workshop: `workshop.state.production[]` (WorkshopProductionState[])
- Farm: `farm.state.production[]` (FarmProductionState[])
- Mine: `mine.state.production[]` (CollectorProductionState[])
- GathererHut: `hut.state.production[]` (CampProductionState[])
- Camp: `camp.state.production[]` (CampProductionState[])
- FishingHut: `hut.state.production[]` (FishingHutProductionState[])

### Panel Navigation (Keyboard)

**Game's Built-in Keyboard Support:**
- Escape - Close panel (`MB.InputService.WasCanceled()`)
- Prev/Next Building - Navigate buildings of same type (`MB.InputConfig.MoveToPrevBuilding/MoveToNextBuilding`)

**No Built-in Navigation Within Panels:**
- Recipe slots, storage items, etc. are mouse-click only
- Unity's `Selectable` navigation is not used
- **We must build our own keyboard navigation** for:
  - Recipe list (toggle, ingredients)
  - Storage items
  - Worker slots
  - Settings/modes
  - Tab navigation

## Common Elements (BuildingPanel Base)

All building panels inherit from `BuildingPanel` which provides:

### Header Information
- **nameText** - Building name (e.g., "Crude Workstation")
- **descText** - Building description
- **amountText** - Building count if multiple exist (e.g., "(2/5)")
- **label** - Optional display label
- **specializationText** - Building tags/specializations (icons)

### Common Buttons
- **moveButton** - Relocate the building
- **sleepButton** - Pause/unpause building
- **nextButton** / **prevButton** - Navigate between buildings of same type

### Common Sub-Panels
- **effectsPanel** (BuildingEffectsPanel) - Shows active effects on building
- **constructionPanel** (BuildingConstructionPanel) - Shows construction progress/requirements
- **workersPanel** (BuildingWorkersPanel) - Shows assigned workers (for production buildings)
- **upgradesPanel** (BuildingUpgradesPanel) - Shows available upgrades

## Tab System (AnimatedTabsPanel)

Many panels use `AnimatedTabsPanel` for tabbed content. Common tabs:

| Tab Name | Purpose | Condition |
|----------|---------|-----------|
| Effects | Building effects | Has active effects |
| Upgrades | Building upgrades | Building is upgradable |
| Rainpunk | Rainpunk augmentation | Rainpunk enabled & building supports it |
| Blight | Blight-related (Hearth) | Blight is active |
| Services | Hearth services | Services unlocked via meta progression |
| Abilities | Cycle abilities (Storage) | Abilities exist |

**Note**: The number of tabs varies by building. Maximum observed is 4 tabs (Hearth: main, Effects, Blight, Services).

---

## Unified User-Facing Concepts

This section maps the game's varied UI implementations to unified concepts for consistent accessibility. The game uses different class names and structures, but from a user perspective, many features are the same thing.

### Complete Concept Matrix

| Unified Concept | User Question | Buildings That Have It |
|-----------------|---------------|------------------------|
| **Tasks/Recipes** | "What can this building do?" | Workshop, Farm, Mine, Gatherer, Camp, Collector, BlightPost, Fishing, Institution, Hearth |
| **Storage** | "What goods are here?" | Most production buildings, Relic (rewards), Port (rewards), Storage |
| **Fuel** | "What powers this?" | Hearth (fire), Workshop (rainpunk rods), BlightPost, Hydrant |
| **Capacity** | "How full is this?" | RainCatcher, Extractor, House, Farm, Rainpunk water tank |
| **Workers** | "Who's working here?" | All production buildings, some Relics |
| **Settings** | "How is this configured?" | Camp (mode), Fishing (bait), Hearth (fuel types, stop toggle), Rainpunk (engine levels) |
| **Effects** | "What bonuses are active?" | Decoration, Institution, Hearth (hub), Shrine (tiers), All (perks on building) |
| **Upgrades** | "Can I improve this?" | Mine, Camp, BlightPost, House, Shrine (tiers) |
| **Progress/Activity** | "What's happening now?" | Construction, Relic (investigation), Port (expedition) |
| **Needs** | "What does it want?" | Poro, House (residents) |
| **Choices** | "What should I pick?" | Relic (path, rewards), Port (level, category) |
| **Rainpunk** | "How are the engines?" | Workshop (when rainpunk enabled) |

---

### 1. Tasks/Recipes

**User concept:** "What can this building produce or do?"

| Game Implementation | Building Type | Key Data |
|---------------------|---------------|----------|
| `WorkshopRecipeSlot` | Workshop | Product, ingredients, time, toggle, limit, priority |
| `FarmRecipeSlot` | Farm | Crop, growth time, toggle |
| `MineRecipeSlot` | Mine | Resource, toggle |
| `GathererHutRecipeSlot` | Gatherer Hut | Resource, priority |
| `CollectorRecipeSlot` | Collector | Resource |
| `BlightPostRecipeSlot` | Blight Post | Fuel type for fighting blight |
| `FishingHutRecipeSlot` | Fishing Hut | Fish type, priority |
| `InstitutionRecipeSlot` | Institution | Service provided, ingredients |
| `HearthEffectButton` | Hearth | Sacrifice recipes (cornerstones) |

**Unified announcement:** "{Recipe name}: {enabled/disabled}, {progress if active}"
**Drill-down:** Ingredients, time, limit, priority controls

---

### 2. Storage/Goods

**User concept:** "What items are stored in this building?"

| Game Implementation | Building Type | Purpose |
|---------------------|---------------|---------|
| `ingredientsPanel` | Workshop, BlightPost | Input materials |
| `storagePanel` | Most production buildings | Output products |
| `storageSlots/gridStorageSlots` | Storage building | All settlement goods |
| `RelicStoragePanel` | Relic | Rewards after completion |
| `PortRewardsPanel` | Port | Expedition rewards |

**Unified announcement:** "{Good name}: {amount}" or "Inputs: {list}" / "Outputs: {list}"

---

### 3. Fuel/Consumables

**User concept:** "What resources power this building?"

| Game Implementation | Building Type | What It Tracks |
|---------------------|---------------|----------------|
| `HearthFuelsPanel` | Hearth | Which fuel types to burn |
| `fuelRodsPanel` | Workshop (Rainpunk) | Rainpunk fuel rod status |
| `fuelPanel` | BlightPost, Hydrant | Blight-fighting fuel |

**Unified announcement:** "Fuel: {type}, {amount remaining}"

---

### 4. Capacity/Fill Level

**User concept:** "How full or utilized is this?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `tankBar` | RainCatcher, Extractor | Water level |
| `waterBar` | Workshop (Rainpunk) | Rainpunk water consumption |
| `residentsText` + slots | House | Residents / max beds |
| `fieldsText`, `plowedFieldsText` | Farm | Sown/plowed fields |
| `PoroNeedSlot` bar | Poro | Need satisfaction level |

**Unified announcement:** "{Thing}: {current} of {max}" (e.g., "Water tank: 75%", "Residents: 3 of 4")

---

### 5. Workers/Occupants

**User concept:** "Who is assigned to this building?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `BuildingWorkersPanel` | All production buildings | Worker slots with names/status |
| `HouseResidentButton` slots | House | Resident villagers |
| Workers in Relic | Some Relics | Workers for investigation |

**Unified announcement:** "Workers: {count} of {max}" + individual names/status on drill-down

---

### 6. Settings/Configuration

**User concept:** "How is this building configured to behave?"

| Game Implementation | Building Type | Options |
|---------------------|---------------|---------|
| Mode toggles | Camp | 5 tree-cutting behavior modes |
| `baitPanel` | Fishing Hut | Which bait to use |
| `HearthFuelsPanel` | Hearth | Which fuel types to accept |
| `stopAfterStormToggle` | Hearth | Stop sacrifices after storm |
| `EnginePanel` levels | Workshop (Rainpunk) | Engine pressure levels (0-3) |

**Unified announcement:** "{Setting}: {current value}"
**Action:** Change setting value

---

### 7. Effects/Bonuses

**User concept:** "What bonuses does this building provide or receive?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `activeEffects` | Decoration | Passive effects from building |
| `activeEffectsSlots` | Institution | Service bonuses provided |
| `HearthHubPanel` | Hearth | Hub area effects |
| `BuildingEffectsPanel` | All buildings (tab) | Perks affecting this building |
| `ShrineEffectsPanel` tiers | Shrine | Tiered upgradeable effects |

**Unified announcement:** "Effects: {count}" then list each effect

---

### 8. Upgrades

**User concept:** "Can I upgrade this building?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `BuildingUpgradesPanel` | Mine, Camp, BlightPost, House | Available upgrades |
| `ShrineEffectsPanel` | Shrine | Tiered effects to unlock |

**Unified announcement:** "Upgrades available: {count}" or "Upgrade: {name}, costs {materials}"

---

### 9. Progress/Activity State

**User concept:** "What is currently happening with this building?"

| Game Implementation | Building Type | States |
|---------------------|---------------|--------|
| `BuildingConstructionPanel` | All (during construction) | Materials → Building (progress %) |
| `RelicProgressPanel` | Relic | Not started → In progress → Complete |
| `PortTimePanel` | Port | Idle → Expedition in progress → Rewards waiting |

**Unified announcement:** "{Activity}: {state}" (e.g., "Construction: 45%", "Investigation: in progress", "Expedition: rewards waiting")

---

### 10. Needs (Creature Care)

**User concept:** "What does this creature need?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `PoroNeedSlot` | Poro | Need name, satisfaction bar, good to feed |
| `PoroHappinessPanel` | Poro | Overall happiness |
| `PoroProductPanel` | Poro | What it produces when happy |

**Unified announcement:** "{Need}: {percentage}%, feed with {good}"

---

### 11. Choices/Decisions

**User concept:** "What do I need to choose?"

| Game Implementation | Building Type | Choices |
|---------------------|---------------|---------|
| `RelicDecisionPanel` | Relic | Investigation path |
| `RelicRewardsPanel` | Relic | Reward tier selection |
| `PortRequirementsPanel` | Port | Expedition level |
| `PortRewardsPickPanel` | Port | Reward category |

**Unified announcement:** "Choose: {option 1}, {option 2}..."

---

### 12. Rainpunk System

**User concept:** "How are the steam engines configured?"

| Game Implementation | Building Type | Shows |
|---------------------|---------------|-------|
| `BuildingRainpunkPanel` | Workshop | Full rainpunk system |
| `EnginePanel` slots | Workshop | Individual engine levels |
| `blightBar` | Workshop | Water usage vs blight |
| `tankBar` / `waterBar` | Workshop | Water consumption |

**Unified announcement:** "Rainpunk: {unlocked/locked}, Engine 1: level {0-3}, Engine 2: level {0-3}, Water: {%}"

---

## Unified Navigation Structure

Based on the unified concepts above, here's a single navigation structure that works for ALL buildings:

```
Building: {Name} ({Status})
│
├── Info
│   ├── Description
│   ├── "Building {X} of {Y}"
│   └── Tags (if any)
│
├── Progress (if applicable)
│   ├── Construction: {X}%, materials status
│   ├── Investigation: {state}, {progress}
│   └── Expedition: {state}, {time remaining}
│
├── Tasks (if has recipes/production)
│   ├── {Recipe 1}: {enabled}, {details}
│   ├── {Recipe 2}: ...
│   └── (drill into recipe for ingredients, toggle, limit, priority)
│
├── Workers (if applicable)
│   ├── "{count} of {max} workers"
│   └── Individual: {name}, {status}
│
├── Capacity (if applicable)
│   └── Tank/Residents/Fields: {current} of {max}
│
├── Storage (if has goods)
│   ├── Inputs: {list}
│   └── Outputs: {list}
│
├── Fuel (if applicable)
│   └── {fuel type}: {amount}
│
├── Settings (if configurable)
│   └── {setting}: {value} (actionable)
│
├── Rainpunk (if Workshop with rainpunk)
│   ├── Status: {locked/unlocked}
│   ├── Engines: {level per engine}
│   └── Water/Blight status
│
├── Needs (if Poro)
│   └── {need}: {%}, feed with {good}
│
├── Choices (if decisions pending)
│   └── {choice type}: {options}
│
├── Effects
│   ├── From building: {list}
│   └── On building: {perks list}
│
├── Upgrades (if available)
│   └── {upgrade}: {cost}
│
└── Actions
    ├── Pause/Resume
    ├── Move
    ├── Previous/Next
    └── Destroy
```

This structure shows sections conditionally based on what the building supports.

---

## Buildings Requiring Special Handling

While most buildings can use the unified concept readers, these buildings have unique features that need dedicated implementation:

### Hearth (Unique)

The Hearth is the most complex building with several unique systems:

| Unique Feature | Description | Why It's Special |
|----------------|-------------|------------------|
| **Fire Panel** | Fire status, fuel consumption | Only building with "fire" concept |
| **Blight Management** | Cyst tracking, purging | Main hearth only, conditional on blight being active |
| **Sacrifice Recipes** | Burn cornerstones for effects | Different from normal recipes - consumes perks |
| **Services Panel** | Hearth services | Unlocked via meta progression, unique UI |
| **Hub Effects** | Area-of-effect bonuses | Unique to hearths |

**Recommendation:** Dedicated `HearthReader` that extends base with these unique sections.

---

### Relic (Unique State Machine)

Relics have a multi-stage investigation system unlike any other building:

| Unique Feature | Description | Why It's Special |
|----------------|-------------|------------------|
| **Investigation States** | Not started → In progress → Complete | 3-state machine with different UI per state |
| **Decision Paths** | Choose investigation approach | Branching choices affect outcomes |
| **Effect Tiers** | Tiered effects to unlock | Different from normal effects |
| **Reward Tiers** | Pick reward category | Interactive selection |
| **Order Requirements** | Some relics require completing orders | Unique blocking condition |
| **Instant vs Worker** | Some need workers, some instant | Variable based on relic type |

**Recommendation:** Dedicated `RelicReader` with state-aware navigation.

---

### Port (Unique State Machine)

Ports have an expedition system with its own workflow:

| Unique Feature | Description | Why It's Special |
|----------------|-------------|------------------|
| **Expedition States** | Idle → In progress → Rewards waiting | 3-state machine |
| **Level Selection** | Choose expedition difficulty | Affects requirements and rewards |
| **Requirements by Level** | Different costs per level | Dynamic based on selection |
| **Time Countdown** | Shows return time | Only building with countdown timer |
| **Category Selection** | Pick reward type (blueprints) | Interactive picker |

**Recommendation:** Dedicated `PortReader` with state-aware navigation.

---

### Poro (Unique - Single Building Type)

Poros are giant creatures with a needs system found nowhere else:

| Unique Feature | Description | Why It's Special |
|----------------|-------------|------------------|
| **Needs System** | Multiple needs with satisfaction bars | Only building with "needs" |
| **Feeding Action** | Click to feed specific goods | Interactive per-need action |
| **Happiness Meter** | Overall happiness affects production | Aggregate of needs |
| **Product Output** | What it produces when happy | Tied to happiness level |

**Recommendation:** Dedicated `PoroReader` - this is the only building using the Needs concept, so it may not be worth abstracting.

---

### Shrine (Unique Upgrade System)

Shrines have tiered effects that work differently from normal upgrades:

| Unique Feature | Description | Why It's Special |
|----------------|-------------|------------------|
| **Effect Tiers** | Multiple levels of effects | Each tier has own cost and effects |
| **Progressive Unlock** | Must unlock tiers in order | Sequential progression |
| **Per-Tier Effects** | Different effects at each level | Not just "better" but different |

**Recommendation:** Could use Upgrades concept but may need `ShrineReader` for tier navigation.

---

---

## Verified Concept Mapping (Per Building Type)

Each building verified against game source code. ✓ = confirmed present, (cond) = conditional.

### Production Buildings (extend ProductionBuildingPanel)

All production buildings inherit: Workers panel, Stats panel, Effects tab (conditional)

#### 1. Workshop
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (WorkshopRecipeSlot) - toggle, ingredients, limit, priority |
| Storage Input | ✓ | `ingredientsPanel` (WorkshopIngredientsStoragePanel) |
| Storage Output | ✓ | `storagePanel` (BuildingStoragePanel) |
| Workers | ✓ | Inherited from ProductionBuildingPanel |
| Effects | ✓ (cond) | `effectsTab` |
| Rainpunk | ✓ (cond) | `rainpunkPanel`, `fuelRodsPanel`, `rainpunkTab` - engines, water, blight bar |

**Tabs:** Main, Effects (cond), Rainpunk (cond)

#### 2. Farm
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (FarmRecipeSlot) |
| Storage Output | ✓ | `storagePanel` |
| Capacity | ✓ | `fieldsText` (sown/total), `plowedFieldsText` (plowed/total) |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |
| Build Action | ✓ | `slot` - button to build farm fields |

**Tabs:** Main, Effects (cond)
**Note:** Has unique "build field" action button

#### 3. Mine
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (MineRecipeSlot) |
| Storage Output | ✓ | `storagePanel` |
| Workers | ✓ | Inherited |
| Upgrades | ✓ (cond) | `upgradesTab` - requires meta unlock |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond), Upgrades (cond)

#### 4. Gatherer Hut
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (GathererHutRecipeSlot) - with priority |
| Storage Output | ✓ | `storagePanel` |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond)

#### 5. Camp
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✗ | No recipes - just collects wood |
| Settings | ✓ | 5 mode toggles (None, OnlyMarked, NoGlades, NoGladesAndOnlyMarked, OnlyMarkedGlades) |
| Storage Output | ✓ | `storagePanel` |
| Workers | ✓ | Inherited |
| Upgrades | ✓ (cond) | `upgradesTab` |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond), Upgrades (cond)
**Note:** No recipes! Settings control tree-cutting behavior.

#### 6. Collector
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (CollectorRecipeSlot) |
| Storage Output | ✓ | `storagePanel` |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond)

#### 7. Blight Post
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (BlightPostRecipeSlot) |
| Storage Input | ✓ | `ingredientsPanel` (BlightPostIngredientsStoragePanel) |
| Storage Output | ✗ | No output - fights blight, doesn't produce goods |
| Fuel | ✓ | `fuelPanel` (BlightFuelPanel) - shows fuel vs cysts |
| Workers | ✓ | Inherited |
| Upgrades | ✓ (cond) | `upgradesTab` - requires meta unlock |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond), Upgrades (cond)

#### 8. Fishing Hut
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesButtons` (FishingHutRecipeSlot) - with priority |
| Storage | ✗ | No storage panel shown |
| Settings | ✓ | `baitPanel` - mode dropdown, product, ingredient, bait status |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond)
**Note:** Bait panel is unique - dropdown + multiple slots

#### 9. Rain Catcher
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✗ | No recipes |
| Storage | ✗ | No storage panel |
| Capacity | ✓ | `tankBar` (WaterTankBar) |
| Water Info | ✓ | `waterCounter`, `timeCounter`, `gradeIcon`, `waterIcon` |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond)
**Note:** Unique water production display (amount, time, grade, type)

#### 10. Extractor
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✗ | No recipes |
| Storage | ✗ | No storage panel |
| Capacity | ✓ | `tankBar` (WaterTankBar) |
| Water Info | ✓ | `waterCounter`, `timeCounter`, `waterIcon` |
| Workers | ✓ | Inherited |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond)
**Note:** Similar to Rain Catcher but no grade icon

---

### Non-Production Buildings (extend BuildingPanel directly)

These do NOT have inherited workers panel or stats panel.

#### 11. House
| Concept | Present | Implementation |
|---------|---------|----------------|
| Capacity | ✓ | `residentsText` ("Residents: X/Y") |
| Occupants | ✓ | `slots` (HouseResidentButton) - shows each resident, lock/unlock |
| Upgrades | ✓ (cond) | `upgradesTab` |
| Effects | ✓ (cond) | From base |
| Workers | ✗ | Not a production building |

**Tabs:** Main, Upgrades (cond)
**Note:** Resident slots can be locked/unlocked

#### 12. Storage
| Concept | Present | Implementation |
|---------|---------|----------------|
| Storage | ✓ | `storageSlots`/`gridStorageSlots` - ALL settlement goods |
| Search | ✓ | `searchBar` (GoodsSearchBar) |
| View Modes | ✓ | `listButton`/`gridButton` |
| Cycle Abilities | ✓ (cond) | `cycleAbilitiesTab` - activatable abilities |
| Effects | ✓ (cond) | From base |

**Tabs:** Main, Abilities (cond)
**Note:** Cycle abilities are clickable to activate

#### 13. Institution
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tasks | ✓ | `recipesSlots` (InstitutionRecipeSlot) - services |
| Effects Active | ✓ | `activeEffectsSlots` (InstitutionEffectSlot) - provided bonuses |
| Storage Input | ✓ | `storagePanel` (InstitutionIngredientsStoragePanel) |
| Effects | ✓ (cond) | `effectsTab` |
| Workers | ✗ | No explicit workers panel (may have implicit) |

**Tabs:** Main, Effects (cond)
**Note:** Shows both recipes (services) AND active effects (bonuses provided)

#### 14. Decoration
| Concept | Present | Implementation |
|---------|---------|----------------|
| Effects Active | ✓ | `activeEffects` (EffectSlot) - passive effects from building |
| Effects | ✓ | From base |

**No tabs** - simple panel
**Note:** Very simple, just shows passive effects

#### 15. Simple
| Concept | Present | Implementation |
|---------|---------|----------------|
| Info | ✓ | Basic building info only |

**No unique features** - used for buildings under construction
**Note:** Has `hideOnFinished` option

#### 16. Hydrant
| Concept | Present | Implementation |
|---------|---------|----------------|
| Fuel | ✓ | `fuelPanel` (BlightFuelPanel) - blight fuel status |
| Effects | ✓ | From base |

**No tabs** - simple panel

---

### Special Buildings (Require Dedicated Readers)

#### 17. Hearth - SPECIAL
| Concept | Present | Implementation |
|---------|---------|----------------|
| Fire | ✓ UNIQUE | `firePanel` (HearthFirePanel) - burn bar, last fuel |
| Fuel Selection | ✓ | `fuelsPanel` (HearthFuelsPanel) - which fuels to accept |
| Hub Effects | ✓ UNIQUE | `hubPanel` (HearthHubPanel) - area bonuses |
| Tasks/Sacrifices | ✓ | `effectsButtons` (HearthEffectButton) - burn cornerstones |
| Settings | ✓ | `stopAfterStormToggle` |
| Blight | ✓ UNIQUE (cond) | `hearthBlightPanel` - corruption bar, cysts, prediction (MAIN HEARTH ONLY) |
| Services | ✓ UNIQUE (cond) | `servicesPanel` - unlockable recipes (meta unlock required) |
| Effects | ✓ (cond) | `effectsTab` |

**Tabs:** Main, Effects (cond), Blight (cond - main hearth + blight active), Services (cond - meta unlock)
**Requires:** Dedicated `HearthReader`

#### 18. Relic - SPECIAL
| Concept | Present | Implementation |
|---------|---------|----------------|
| Progress | ✓ UNIQUE | `progress` (RelicProgressPanel) - investigation progress |
| Decisions | ✓ UNIQUE | `decision` (RelicDecisionPanel) - path picker |
| Effect Tiers | ✓ UNIQUE | `effects` (RelicEffectsPanel) - tiered effects |
| Reward Tiers | ✓ UNIQUE | `rewards` (RelicRewardsPanel) - pick rewards |
| Storage | ✓ | `storagePanel` (RelicStoragePanel) - rewards after completion |
| Workers | ✓ (some) | Some relics require workers |
| Actions | ✓ | `startButton`, `cancelButton` (ChargedButton) |

**States:** Not started → In progress → Complete
**Requires:** Dedicated `RelicReader` with state machine

#### 19. Shrine - SPECIAL
| Concept | Present | Implementation |
|---------|---------|----------------|
| Tiered Effects | ✓ UNIQUE | `slots` (ShrineEffectsPanel) - multiple tiers |
| Header | ✓ | `header` - shrine type |

**No tabs** - shows tiered effects directly
**Requires:** Dedicated `ShrineReader` for tier navigation

#### 20. Port - SPECIAL
| Concept | Present | Implementation |
|---------|---------|----------------|
| Level Selection | ✓ UNIQUE | `requirementsPanel` (PortRequirementsPanel) - expedition level |
| Time | ✓ UNIQUE | `timePanel` (PortTimePanel) - countdown |
| Rewards | ✓ UNIQUE | `rewardsPanel` (PortRewardsPanel) - current rewards |
| Category Pick | ✓ UNIQUE | `rewardsPickPanel` (PortRewardsPickPanel) - blueprint category |
| Actions | ✓ | `button` - Start/Cancel/Accept based on state |

**States:** Idle → In progress → Rewards waiting
**Requires:** Dedicated `PortReader` with state machine

#### 21. Poro - SPECIAL
| Concept | Present | Implementation |
|---------|---------|----------------|
| Needs | ✓ UNIQUE | `needsSlots` (PoroNeedSlot) - satisfaction bars, feeding |
| Happiness | ✓ UNIQUE | `happinessPanel` (PoroHappinessPanel) - overall happiness |
| Product | ✓ UNIQUE | `productPanel` (PoroProductPanel) - what it produces |

**No tabs** - shows needs directly
**Requires:** Dedicated `PoroReader` - only building with needs system

---

## Concept Matrix: What Each Building Needs

✓ = needs this concept, ✗ = does NOT need, (c) = conditional

### Standard Buildings (16) - Unified Concept Readers

| Building | Tasks | Storage In | Storage Out | Workers | Capacity | Fuel | Settings | Upgrades | Effects | Notes |
|----------|-------|------------|-------------|---------|----------|------|----------|----------|---------|-------|
| Workshop | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | (c) | +Rainpunk system |
| Farm | ✓ | ✗ | ✓ | ✓ | ✓ fields | ✗ | ✗ | ✗ | (c) | +Build field action |
| Mine | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ | (c) | (c) | |
| Gatherer Hut | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | (c) | |
| Camp | ✗ | ✗ | ✓ | ✓ | ✗ | ✗ | ✓ modes | (c) | (c) | NO recipes |
| Collector | ✓ | ✗ | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | (c) | |
| Blight Post | ✓ | ✓ | ✗ | ✓ | ✗ | ✓ | ✗ | (c) | (c) | NO output |
| Fishing Hut | ✓ | ✗ | ✗ | ✓ | ✗ | ✗ | ✓ bait | ✗ | (c) | NO storage |
| Rain Catcher | ✗ | ✗ | ✗ | ✓ | ✓ tank | ✗ | ✗ | ✗ | (c) | +Water info |
| Extractor | ✗ | ✗ | ✗ | ✓ | ✓ tank | ✗ | ✗ | ✗ | (c) | +Water info |
| House | ✗ | ✗ | ✗ | ✗ | ✓ residents | ✗ | ✗ | (c) | (c) | +Lock slots |
| Storage | ✗ | ✗ | ✓ all | ✗ | ✗ | ✗ | ✗ | ✗ | (c) | +Search +Abilities |
| Institution | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | (c) | +Active effects |
| Decoration | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ | Very simple |
| Simple | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | ✗ | Construction only |
| Hydrant | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ | (c) | Very simple |

### Special Buildings (5) - Need Dedicated Readers

| Building | Why Special | Unique Features |
|----------|-------------|-----------------|
| **Hearth** | Many unique systems | Fire panel, fuel selection, hub effects, sacrifices, blight (main only), services |
| **Relic** | State machine | Investigation states, decisions, effect tiers, reward tiers, orders |
| **Port** | State machine | Expedition states, level selection, time countdown, category picker |
| **Poro** | Unique needs system | Multiple needs with satisfaction bars, feeding, happiness, product |
| **Shrine** | Tiered effects | Progressive tier unlock, different effects per tier |

### Concept Implementation Priority

Based on the matrix, here's what covers the most buildings:

| Concept | Buildings Using | Priority |
|---------|-----------------|----------|
| Effects | 15 of 16 | HIGH - almost universal |
| Workers | 10 of 16 | HIGH - all production buildings |
| Tasks | 9 of 16 | HIGH - most production buildings |
| Storage Out | 7 of 16 | MEDIUM |
| Capacity | 4 of 16 | MEDIUM (but 3 different types) |
| Upgrades | 4 of 16 | MEDIUM |
| Storage In | 3 of 16 | LOW |
| Fuel | 2 of 16 | LOW |
| Settings | 2 of 16 | LOW (but complex - modes vs bait) |

### Buildings That Need Almost Nothing

These can work with just base Info + Effects:

| Building | Only Needs |
|----------|-----------|
| Decoration | Effects |
| Simple | Nothing (just info) |
| Hydrant | Fuel, Effects |

### Buildings That Need The Most

| Building | Concept Count | Concepts |
|----------|---------------|----------|
| Workshop | 5 + Rainpunk | Tasks, Storage In, Storage Out, Workers, Effects, (Rainpunk) |
| Blight Post | 5 | Tasks, Storage In, Fuel, Workers, Upgrades, Effects |
| Farm | 5 | Tasks, Storage Out, Workers, Capacity, Effects, (Build action) |
| Camp | 4 | Storage Out, Workers, Settings, Upgrades, Effects |

---

## Building Type Details

### 1. Workshop (WorkshopPanel)

Production building for crafting goods.

**Unique Elements:**
- **recipesButtons** - List of `WorkshopRecipeSlot` for each recipe
- **ingredientsPanel** - Shows ingredients storage
- **storagePanel** - Shows produced goods storage
- **rainpunkPanel** - Rainpunk augmentation controls
- **fuelRodsPanel** - Fuel rods for rainpunk

**Recipe Slot Information:**
- Recipe name (product name)
- Toggle on/off
- Production time
- Ingredients required (multiple ingredient slots)
- Production limit (cap)
- Priority controls (move up/down)
- Active/inactive blend overlay

**Tabs:** Main, Effects, Rainpunk (conditional)

---

### 2. Farm (FarmPanel)

Grows crops on farm fields.

**Unique Elements:**
- **fieldsText** - "Sown fields: X/Y"
- **plowedFieldsText** - "Plowed fields: X/Y"
- **recipesButtons** - List of `FarmRecipeSlot`
- **storagePanel** - Produced goods
- **slot** - Button to build farm fields

**Tabs:** Main, Effects

---

### 3. Mine (MinePanel)

Extracts resources from deposits.

**Unique Elements:**
- **recipesButtons** - List of `MineRecipeSlot`
- **storagePanel** - Extracted goods

**Tabs:** Main, Effects, Upgrades (conditional)

---

### 4. Gatherer Hut (GathererHutPanel)

Collects resources from natural deposits.

**Unique Elements:**
- **recipesButtons** - List of `GathererHutRecipeSlot`
- **storagePanel** - Collected goods
- Recipe priority controls

---

### 5. Camp (CampPanel)

Woodcutters camp with work mode selection.

**Unique Elements:**
- **Mode toggles** (5 options):
  - None - No restrictions
  - OnlyMarked - Only marked trees
  - NoGlades - Avoid glades
  - NoGladesAndOnlyMarked - Both restrictions
  - OnlyMarkedGlades - Only marked in glades
- **storagePanel** - Produced goods

**Tabs:** Main, Effects, Upgrades (conditional)

---

### 6. Collector (CollectorPanel)

Collects from deposits (like rain collectors).

**Unique Elements:**
- **recipesButtons** - List of `CollectorRecipeSlot`
- **storagePanel** - Collected goods

---

### 7. Blight Post (BlightPostPanel)

Fights blight corruption.

**Unique Elements:**
- **recipesButtons** - List of `BlightPostRecipeSlot`
- **ingredientsPanel** - Blight fighting ingredients
- **fuelPanel** - Fuel status

**Tabs:** Main, Effects, Upgrades (conditional)

---

### 8. Rain Catcher (RainCatcherPanel)

Collects water during rain.

**Unique Elements:**
- **waterCounter** - Amount produced
- **timeCounter** - Production time
- **gradeIcon** - Water quality grade
- **waterIcon** - Water type icon
- **tankBar** - Water tank fill level

---

### 9. Fishing Hut (FishingHutPanel)

Catches fish with bait system.

**Unique Elements:**
- **baitPanel** - Bait selection and management
- **recipesButtons** - List of `FishingHutRecipeSlot`
- Recipe priority controls

---

### 10. Extractor (ExtractorPanel)

Extracts water from sources.

**Unique Elements:**
- **waterCounter** - Amount produced
- **timeCounter** - Production time
- **waterIcon** - Water type icon
- **tankBar** - Water tank fill level

---

### 11. Hearth (HearthPanel)

Central fire that provides services.

**Unique Elements:**
- **firePanel** - Fire status and controls
- **fuelsPanel** - Fuel selection
- **hubPanel** - Hearth hub effects
- **hearthBlightPanel** - Blight management (main hearth only)
- **servicesPanel** - Hearth services
- **effectsButtons** - Sacrifice recipes (cornerstones)
- **stopAfterStormToggle** - Stop sacrifices after storm

**Tabs:** Main, Effects, Blight (conditional), Services (conditional)

---

### 12. House (HousePanel)

Provides housing for villagers.

**Unique Elements:**
- **residentsText** - "Residents: X/Y"
- **slots** - List of `HouseResidentButton` showing each resident/slot
  - Can lock/unlock housing slots
  - Shows villager if occupied

**Tabs:** Main, Upgrades (conditional)

---

### 13. Storage (StoragePanel)

Main settlement storage.

**Unique Elements:**
- **searchBar** - Filter goods by name
- **listButton** / **gridButton** - Switch view mode
- **storageSlots** / **gridStorageSlots** - Good slots in list/grid view
- Each slot shows: good icon, name, amount

**Tabs:** Main, Abilities (conditional - cycle abilities)

---

### 14. Institution (InstitutionPanel)

Service buildings (taverns, temples, etc.).

**Unique Elements:**
- **recipesSlots** - List of `InstitutionRecipeSlot`
- **activeEffectsSlots** - List of `InstitutionEffectSlot`
- **storagePanel** - Ingredients storage

**Tabs:** Main, Effects (conditional)

---

### 15. Decoration (DecorationPanel)

Decorative buildings with passive effects.

**Unique Elements:**
- **activeEffects** - List of `EffectSlot` showing passive effects

No tabs (simple panel).

---

### 16. Simple Panel (SimplePanel)

Used for buildings under construction or without special UI.

No unique elements - just basic building info.

---

### 17. Hydrant (HydrantPanel)

Blight-fighting hydrant.

**Unique Elements:**
- **fuelPanel** - Fuel status

---

### 18. Relic (RelicPanel)

Ancient relics with investigation system.

**Unique Elements:**
- **progress** - Investigation progress
- **decision** - Decision picker (different paths)
- **effects** - Effect tiers available
- **rewards** - Reward tiers available
- **startButton** - Start investigation
- **cancelButton** - Cancel investigation (charged button)
- **storagePanel** - Rewards after completion
- Various blink animators for highlighting requirements

**States:**
- Not started - Shows effects, rewards, start button
- In progress - Shows progress, cancel button
- Completed - Shows storage with rewards

---

### 19. Shrine (ShrinePanel)

Upgradable shrines with tiered effects.

**Unique Elements:**
- **header** - Shrine type header
- **slots** - List of `ShrineEffectsPanel` for each tier

---

### 20. Port (PortPanel)

Trading port for expeditions.

**Unique Elements:**
- **button** - Start/cancel/accept expedition
- **requirementsPanel** - Expedition requirements by level
- **timePanel** - Time until return
- **rewardsPanel** - Current rewards
- **rewardsPickPanel** - Pick reward category

**States:**
- Idle - Can start expedition
- In progress - Shows time, can cancel
- Rewards waiting - Can accept rewards

---

### 21. Poro (PoroPanel)

Giant creatures that produce goods.

**Unique Elements:**
- **happinessPanel** - Poro happiness level
- **productPanel** - What the poro produces
- **needsSlots** - List of `PoroNeedSlot` for each need

---

## Recipe Slot Common Elements

Most recipe slots share these elements:

| Element | Description |
|---------|-------------|
| nameText | Recipe/product name |
| goodSlot | Product icon and amount |
| toggle | Enable/disable production |
| timeText | Production time |
| progressBar | Current production progress |
| ingredientsSlots | Required inputs |
| limitPanel | Production cap controls |
| prioPanel | Priority up/down buttons |

## Worker Panel Elements

For production buildings, `BuildingWorkersPanel` shows:

| Element | Description |
|---------|-------------|
| Worker slots | List of assigned workers |
| Worker status | What each worker is doing |
| Assign button | Assign more workers |
| Production animation | Shows when production completes |

## Construction Panel Elements

`BuildingConstructionPanel` shows during construction:

| Element | Description |
|---------|-------------|
| Requirements | Materials needed |
| Progress | Construction progress bar |
| Workers | Builders assigned |

## Key Data Points for Accessibility

For each building panel, we need to expose:

1. **Building Identity**
   - Name
   - Type
   - Count (X of Y)
   - Status (constructing, working, paused, etc.)

2. **Workers** (if applicable)
   - Assigned count / max
   - Worker names and status

3. **Recipes** (if applicable)
   - Recipe name
   - Enabled/disabled
   - Ingredients (what's needed, what's available)
   - Production time
   - Limit settings
   - Priority

4. **Storage** (if applicable)
   - Goods and amounts
   - Capacity

5. **Special Controls**
   - Mode toggles (Camp)
   - Bait selection (Fishing Hut)
   - Fuel selection (Hearth)
   - Investigation controls (Relic)
   - Expedition controls (Port)

6. **Effects**
   - Active effects on building
   - Available upgrades

7. **Tabs**
   - Available tabs
   - Current tab
   - Tab content

## Navigation Considerations

Building panels present several navigation challenges:

1. **Multi-level hierarchy**: Building → Tabs → Sections → Items → Sub-items
2. **Dynamic content**: Recipe lists, worker slots change
3. **Interactive elements**: Toggles, sliders, buttons, dropdowns
4. **Contextual actions**: Different actions based on building state

Suggested navigation approach:
- Tab-level navigation (left/right for tabs)
- Section-level navigation (up/down within tab)
- Item-level navigation (for lists like recipes)
- Action keys for common operations (toggle, priority, etc.)

---

## Accessibility Implementation Approach

### Evaluation: UINavigator vs Custom UI

Two approaches were considered for making building panels accessible:

#### Option A: UINavigator (Press Game Controls Directly)

Use the existing UINavigator infrastructure to discover and activate game UI elements.

**Current UINavigator Capabilities:**
- Discovers panels by name patterns ("panel", "content", "section")
- Detects tabs via TabsPanel reflection
- Finds Selectables (Button, Toggle, Slider, Dropdown, InputField)
- Activates elements via onClick.Invoke(), toggle.isOn, etc.
- Handles dropdown navigation, text editing, slider adjustment

**Problems for Building Panels:**

| Issue | Impact |
|-------|--------|
| Linear navigation through dense UI | A Workshop has ~30+ interactive elements. Up/Down through all would be overwhelming |
| Recipe slot sub-structure | Each recipe has toggle, ingredients, limit, priority - need grouped navigation, not flat list |
| Missing/poor text labels | Priority buttons are just arrows, ingredient slots are icons, limit inputs have no labels |
| Custom game components | Many controls aren't standard Unity Selectables (WorkshopRecipeSlot, etc.) |
| Conditional tabs | AnimatedTabsPanel may differ from TabsPanel detection |
| Dynamic content | Recipe lists, worker slots change based on building state |

#### Option B: Custom UI (Speech-Only Panel) ✓ RECOMMENDED

Build a custom speech-only panel like VillagersPanel that uses reflection to read game data and presents structured navigation.

**Advantages:**

1. **Structured information** - Announce "Recipe: Planks, enabled, 30 sec, needs 8 wood (have 10), limit 50" instead of navigating 6 separate elements

2. **Meaningful navigation** - Categories like Info → Recipes → Workers → Storage → Effects, then drill into items

3. **Consistent across building types** - Normalize WorkshopRecipeSlot, FarmRecipeSlot, MineRecipeSlot into unified "recipe" navigation

4. **Complete control** - Skip decorative elements, add context, announce exactly what's useful

5. **Proven pattern** - VillagersPanel, StatsPanel, MysteriesPanel already work this way

**Challenges:**
- More code to write (21 building types, though many share patterns)
- Reflection maintenance if game changes internal structure
- Actions still need reflection calls to game methods

### Recommended Architecture

Use the Unified Navigation Structure from the "Unified User-Facing Concepts" section. The key insight is that we don't need to mirror the game's 21 panel types - we need readers for 12 unified concepts that appear conditionally.

### Implementation Strategy

1. **Base BuildingPanelReader class** - Handles Info, Effects, Upgrades, Actions (all buildings have these)
2. **Concept readers** - One reader per unified concept:
   - `TasksReader` - Handles all recipe types (Workshop, Farm, Mine, etc.)
   - `StorageReader` - Handles all storage panels
   - `WorkersReader` - Handles worker panels
   - `CapacityReader` - Handles tank bars, residents, fields
   - `FuelReader` - Handles fuel panels
   - `SettingsReader` - Handles mode toggles, bait selection, etc.
   - `ProgressReader` - Handles construction, investigation, expedition
   - `RainpunkReader` - Handles rainpunk engine system
   - `NeedsReader` - Handles Poro needs
   - `ChoicesReader` - Handles decision/reward pickers
3. **Building detector** - Determines which concepts apply to current building
4. **Action invocation** - Reflection calls for toggle, priority, limit, feed, etc.

### Concept-to-Building Mapping

| Concept | Applies To |
|---------|------------|
| Info, Effects, Actions | ALL buildings |
| Tasks | Workshop, Farm, Mine, Gatherer, Camp, Collector, BlightPost, Fishing, Institution, Hearth |
| Storage | Workshop, Farm, Mine, Gatherer, Camp, Collector, Institution, Storage, Relic, Port |
| Workers | All ProductionBuildings, some Relics |
| Capacity | RainCatcher, Extractor, House, Farm, Rainpunk |
| Fuel | Hearth, BlightPost, Hydrant, Workshop (rainpunk) |
| Settings | Camp, Fishing, Hearth, Workshop (rainpunk) |
| Upgrades | Mine, Camp, BlightPost, House, Shrine |
| Progress | Construction (all), Relic, Port |
| Rainpunk | Workshop (when enabled) |
| Needs | Poro |
| Choices | Relic, Port |

### Implementation Order

1. **Phase 1: Base** - Info, Status, Actions (works for all buildings)
2. **Phase 2: Common** - Tasks, Storage, Workers (covers most production buildings)
3. **Phase 3: Extended** - Capacity, Fuel, Settings, Effects, Upgrades
4. **Phase 4: Special Buildings** - Dedicated readers for exceptions:
   - `HearthReader` - Fire, blight, sacrifices, services, hub
   - `RelicReader` - Investigation state machine, decisions, reward tiers
   - `PortReader` - Expedition state machine, level selection, countdown
   - `PoroReader` - Needs system, feeding, happiness
   - `ShrineReader` - Tiered effects (may reuse Upgrades)
5. **Phase 5: Rainpunk** - Engine levels, water/blight tracking (Workshop add-on)

### Coverage After Each Phase

| Phase | Buildings Working |
|-------|-------------------|
| Phase 1 | All buildings show name, status, basic actions |
| Phase 2 | Workshop, Farm, Mine, Gatherer, Camp, Collector, BlightPost, Fishing, Institution (core features) |
| Phase 3 | + RainCatcher, Extractor, House, Storage, Decoration, Hydrant, Simple |
| Phase 4 | + Hearth, Relic, Port, Poro, Shrine (full features) |
| Phase 5 | + Workshop Rainpunk tab |

This approach means 16 of 21 building types work with just the unified concept readers. Only 5 need dedicated handling.

---

## Implementation Data Sources (Verified Against Game Source)

This section documents the exact data access paths for each building type, verified against game source code.

### 1. Workshop

**Class Hierarchy:** `Workshop` extends `ProductionBuilding` extends `Building`

**State Class:** `WorkshopState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `workshop.state.recipes` (List<WorkshopRecipeState>) | Each has: `active`, `limit`, `isLimitLocal`, `productName`, `ingredients[][]` |
| **Recipe Model** | `MB.Settings.GetWorkshopRecipe(state.model)` | Gets `WorkshopRecipeModel` with `producedGood`, `requiredGoods`, `productionTime` |
| **Storage Output** | `workshop.ProductionStorage` (BuildingGoodsCollection) | `productionStorage.goods` for items |
| **Storage Input** | `workshop.IngredientsStorage` (GoodsCollection) | `ingredientsStorage.goods` for items |
| **Workers** | `workshop.state.workers[]` (int array) | Index = workplace slot, value = villager ID (-1 if empty) |
| **Rainpunk Status** | `workshop.state.rainpunkUnlocked` (bool) | Whether rainpunk tab is available |
| **Rainpunk Engines** | `workshop.state.engines[]` (RainpunkEngineState[]) | Each has `requestedLevel` (0-3) |
| **Rainpunk Water** | `workshop.state.waterLeft` (float 0-1), `workshop.state.waterUsed` (int) | Water consumption tracking |
| **Blight** | `workshop.state.blight` (BlightState) | Cyst tracking |
| **Production Progress** | `workshop.state.production[]` (WorkshopProductionState[]) | Per-workplace production state |

**Recipe State Structure (WorkshopRecipeState):**
```
- active: bool - is recipe enabled
- limit: int - production cap
- isLimitLocal: bool - local or global limit
- productName: string - output good name
- ingredients: IngredientState[][] - [ingredient slot][alternative goods]
  - Each IngredientState has: good (Good), allowed (bool)
- lastIngredientsUsed: int[] - tracks which alternative was last used
```

**Key Methods:**
- `workshop.SwitchProductionOf(state)` - Toggle recipe on/off
- `workshop.UnlockRainpunk()` - Unlock rainpunk system
- `workshop.GetRequestedPressure()` - Total engine pressure requested

**Effects Tab:** Uses standard `BuildingEffectsPanel` from base class

---

### 2. Farm

**Class Hierarchy:** `Farm` extends `ProductionBuilding` extends `Building`

**State Class:** `FarmState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `farm.state.recipes` (List<RecipeState>) | Base RecipeState with: `model`, `active`, `prio`, `lastPicked` |
| **Recipe Model** | `MB.Settings.GetFarmRecipe(state.model)` | Gets `FarmRecipeModel` with `producedGood`, `plantingTime`, `harvestTime` |
| **Storage Output** | `farm.ProductionStorage` (BuildingStorage) | `storage.goods` for items |
| **Workers** | `farm.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Sown Fields** | `farm.CountSownFieldsInRange()` | Method call - counts seeded farmfields in area |
| **Plowed Fields** | `farm.CountPlownFieldsInRange()` | Method call - counts plowed farmfields in area |
| **Total Fields** | `farm.CountAllReaveleadFieldsInRange()` | Method call - total available grass fields |
| **Fields in Area** | `farm.fieldsInArea` (List<Field>) | Cached fields within farm's work area |
| **Production Progress** | `farm.state.production[]` (FarmProductionState[]) | Per-workplace: type (Planting/Harvesting/Plowing), recipe, product |

**Recipe State Structure (RecipeState - simpler than Workshop):**
```
- model: string - recipe model name
- active: bool - is recipe enabled
- prio: int - priority (not used by Farm currently)
- lastPicked: float - timestamp for round-robin selection
```

**Field Counts Display:**
```
fieldsText = "Sown fields: {sown}/{total}"
plowedFieldsText = "Plowed fields: {plowed}/{total}"
```

**Key Methods:**
- `farm.SwitchProductionOf(recipe)` - Toggle recipe on/off
- `farm.CountSownFieldsInRange()` - Get count of seeded fields
- `farm.CountPlownFieldsInRange()` - Get count of plowed fields
- `farm.GetPlantingRate()` / `farm.GetHarvestingRate()` - Production speed modifiers

**Unique:** Build Field Action - `slot.SetUp(fieldModel, OnSlotClicked)` triggers `GameMB.GameBlackboardService.BuildingConstructionRequested`

---

### 3. Mine

**Class Hierarchy:** `Mine` extends `ProductionBuilding` extends `Building`, implements `ICollector`

**State Class:** `MineState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `mine.state.recipes` (List<RecipeState>) | Only shows recipes that `mine.CanGather(recipe)` returns true for |
| **Recipe Model** | `MB.Settings.GetMineRecipe(state.model)` | Gets `MineRecipeModel` with `producedGood`, `productionTime`, `extraProduction[]` |
| **Storage Output** | `mine.ProductionStorage` (BuildingStorage) | `storage.goods` for items |
| **Workers** | `mine.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Charges Left** | `mine.GetChargesLeft(refGood)` | Remaining extraction charges for specific ore |
| **Max Charges** | `mine.GetMaxCharges(refGood)` | Total charges for specific ore |
| **Ore Under Mine** | `mine.GetOreUnderMine()` (Ore[]) | Physical ore deposits building is placed on |
| **Upgrades** | `mine.state.level` (int), `mine.state.unlockedExtraCharges[]` | Upgrade level and extra ore charges |
| **Production Progress** | `mine.state.production[]` (CollectorProductionState[]) | Per-workplace production state |

**Unique Features:**
- **Ore Charges:** Unlike other buildings, mines have finite resources based on ore deposits
- **Recipe Filtering:** Only shows recipes matching ore under the mine (`mine.CanGather(recipe)`)
- **Upgrades Tab:** Conditional on `MB.MetaPerksService.AreMineUpgradesUnlocked()`

**Key Methods:**
- `mine.SwitchProductionOf(recipe)` - Toggle recipe on/off
- `mine.GetChargesLeft(refGood)` - Get remaining charges for specific ore type
- `mine.CanGather(recipe)` - Check if mine can extract this recipe's ore
- `mine.GetProductionTime(production)` - Get production time with effects

---

### 4. Gatherer Hut

**Class Hierarchy:** `GathererHut` extends `ProductionBuilding` extends `Building`

**State Class:** `GathererHutState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `hut.state.recipes` (List<RecipeState>) | Ordered by priority if `MB.ClientPrefsService.MovingRecipesEnabled.Value` |
| **Recipe Model** | `MB.Settings.GetGathererHutRecipe(state.model)` | Gets `GathererHutRecipeModel` with `refGood`, `productionTime` |
| **Storage Output** | `hut.ProductionStorage` (BuildingStorage) | `storage.goods` for items |
| **Workers** | `hut.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Priority** | `recipe.prio` (int) | Higher = processed first. Recipes ordered by `OrderByDescending(prio)` |
| **Production Progress** | `hut.state.production[]` (CampProductionState[]) | Same production state type as Camp |
| **Deposit Availability** | `hut.HasFullDepositWithNearby(recipe)` | Checks if gatherable deposits exist |

**Unique Features:**
- **Priority System:** Recipes can be reordered via priority (prio field)
- **Area-Based:** Must have resource deposits within gathering range

**Key Methods:**
- `hut.SwitchProductionOf(recipe)` - Toggle recipe on/off
- `hut.HasFullDepositWithNearby(recipe)` - Check if deposits available for recipe

---

### 5. Camp (Woodcutters)

**Class Hierarchy:** `Camp` extends `ProductionBuilding` extends `Building`

**State Class:** `CampState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Mode** | `camp.state.mode` (CampMode enum) | Tree-cutting behavior mode |
| **Storage Output** | `camp.ProductionStorage` (BuildingStorage) | `storage.goods` for items |
| **Workers** | `camp.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Recipes** | `camp.state.recipes` (List<RecipeState>) | But panel doesn't show recipes - implicit wood cutting |
| **Upgrades** | `camp.HasUpgrades` (bool) | Conditional upgrades tab |
| **Production Progress** | `camp.state.production[]` (CampProductionState[]) | Per-workplace production state |

**CampMode Enum:**
```
- None: No restrictions
- OnlyMarked: Only cut marked trees
- NoGlades: Don't cut trees in glades
- OnlyMarkedGlades: Only marked trees in glades
- NoGladesAndOnlyMarked: Combine NoGlades + OnlyMarked
```

**Unique Features:**
- **No Recipe UI:** Unlike other production buildings, has mode toggles instead of recipe list
- **Mode Sharing:** Ctrl+click shares mode to all camps via `ShareMode()`

**Key Methods:**
- `camp.SetMode(mode)` - Change camp cutting mode
- Mode is also readable via `camp.state.mode`

---

### 6. Collector

**Class Hierarchy:** `Collector` extends `ProductionBuilding` extends `Building`, implements `ICollector`

**State Class:** `CollectorState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `collector.state.recipes` (List<RecipeState>) | Simple toggle-only recipes |
| **Recipe Model** | `MB.Settings.GetCollectorRecipe(state.model)` | Gets `CollectorRecipeModel` with `producedGood`, `productionTime` |
| **Storage Output** | `collector.ProductionStorage` (BuildingStorage) | `productionStorage.goods` for items |
| **Workers** | `collector.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Production Progress** | `collector.state.production[]` (CollectorProductionState[]) | Per-workplace production state |

**Simple Building:** Collector is one of the simpler production buildings - just recipes with toggle and storage output.

**Key Methods:**
- `collector.SwitchProductionOf(recipe)` - Toggle recipe on/off

---

### 7. Blight Post

**Class Hierarchy:** `BlightPost` extends `ProductionBuilding` extends `Building`, implements `IWorkshop`

**State Class:** `BlightPostState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `post.state.recipes` (List<WorkshopRecipeState>) | Workshop-style recipes with ingredients |
| **Recipe Model** | `MB.Settings.GetWorkshopRecipe(state.model)` | Uses WorkshopRecipeModel like Workshop |
| **Storage Input** | `post.IngredientsStorage` (BuildingIngredientsStorage) | `ingredientsStorage.goods` for items |
| **Storage Output** | NONE | Doesn't produce goods - produces fuel tokens that go to central storage |
| **Workers** | `post.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Fuel Info** | `GameMB.StorageService.Main.GetAmount(blightConfig.blightPostFuel.Name)` | Total fuel across all posts |
| **Limit** | `GameMB.BlightService.GetBlightPostLimit()` | Global production limit |
| **Upgrades** | `MB.MetaPerksService.AreBlightPostUpgradesUnlocked()` | Conditional upgrades tab |
| **Production Progress** | `post.state.production[]` (WorkshopProductionState[]) | Same as Workshop |

**Unique Features:**
- **No Output Storage:** Unlike other production buildings, output goes directly to global storage
- **Global Limit:** `GameMB.BlightService.GetBlightPostLimit()` and `SetBlightPostLimit()` - applies to all posts
- **Fuel Panel:** Shows fuel status via `BlightFuelPanel` with cyst/fuel ratio

**Key Methods:**
- `post.SwitchProductionOf(recipe)` - Toggle recipe on/off
- `GameMB.BlightService.GetBlightPostLimit()` - Get global production limit
- `GameMB.BlightService.SetBlightPostLimit(amount)` - Set global production limit

---

### 8. Fishing Hut

**Class Hierarchy:** `FishingHut` extends `ProductionBuilding` extends `Building`

**State Class:** `FishingHutState` extends `ProductionBuildingState`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes** | `hut.state.recipes` (List<RecipeState>) | Ordered by priority if enabled |
| **Recipe Model** | `MB.Settings.GetFishingHutRecipe(state.model)` | Gets `FishingHutRecipeModel` |
| **Bait Mode** | `hut.state.baitMode` (FishmanBaitMode enum) | Bait usage mode |
| **Bait Charges** | `hut.state.baitChargesLeft` (int) | Remaining bait uses |
| **Bait Ingredient** | `hut.model.baitIngredient` | What item is used as bait |
| **Bait Product** | `GameMB.EffectsService.GetBaitProduction(hut.model)` | What bait produces |
| **Workers** | `hut.state.workers[]` (int array) | Index = workplace slot, value = villager ID |
| **Priority** | `recipe.prio` (int) | Higher = processed first |
| **Production Progress** | `hut.state.production[]` (FishingHutProductionState[]) | Per-workplace production state |

**FishmanBaitMode Enum:**
```
- None: Don't use bait
- Optional: Use bait if available
- OnlyWithBait: Only work when bait is available
```

**Unique Features:**
- **No Storage Panel:** Unlike other production buildings, output goes directly to lakes/global storage
- **Bait System:** Complex bait panel with dropdown, ingredient slot, and status
- **Priority System:** Like GathererHut, recipes can be reordered

**Key Methods:**
- `hut.SwitchProductionOf(recipe)` - Toggle recipe on/off
- `hut.ChangeMode(mode)` - Change bait mode
- `hut.HasEnoughBaitStored()` - Check if bait is available

---

### 9. Rain Catcher

**Class Hierarchy:** `RainCatcher` extends `ProductionBuilding` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Water Type** | `catcher.GetCurrentWaterType()` (WaterModel) | Current water being collected |
| **Water Amount** | `GameMB.EffectsService.GetProduction(catcher, recipe.GetGood(), recipe).amount` | Production amount |
| **Production Time** | `GameMB.EffectsService.GetBuildingProductionTime(catcher, recipe, recipe.productionTime)` | Time per cycle |
| **Tank Level** | Via `WaterTankBar.SetUp(waterType)` | Visual tank fill |
| **Grade** | `recipe.grade.icon` | Water quality grade |
| **Workers** | Inherited from ProductionBuildingPanel | Worker slots |
| **Recipe** | `catcher.GetCurrentRecipe()` (RainCatcherRecipeModel) | Current active recipe |

**Simple Building:** Shows water type, amount, time, grade, and tank level. No recipe selection.

---

### 10. Extractor

**Class Hierarchy:** `Extractor` extends `ProductionBuilding` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Water Type** | `extractor.GetWaterType()` (WaterModel) | Water being extracted |
| **Water Amount** | `GameMB.EffectsService.GetProduction(extractor, good).amount` | Production amount |
| **Production Time** | `GameMB.EffectsService.GetBuildingProductionTime(extractor, null, model.productionTime)` | Time per cycle |
| **Tank Level** | Via `WaterTankBar.SetUp(waterType)` | Visual tank fill |
| **Workers** | Inherited from ProductionBuildingPanel | Worker slots |

**Similar to Rain Catcher:** Shows water info and tank level. No recipe selection, no grade icon.

---

### 11. House

**Class Hierarchy:** `House` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Residents Text** | `current.state.residents.Count` / `current.GetHousingPlaces()` | "Residents: X/Y" |
| **Resident Slots** | `current.state.residents[]` (List<int> villager IDs) | Each slot via `HouseResidentButton` |
| **Max Slots** | `current.GetMaxHousingPlaces()` | Maximum possible with upgrades |
| **Available Slots** | `current.GetHousingPlaces()` | Currently available (may be locked) |
| **Villager at Slot** | `GameMB.VillagersService.GetVillager(residentsId)` | Get villager details |
| **Upgrades** | `current.HasUpgrades` (bool) | Conditional upgrades tab |

**Lock/Unlock:**
- `current.LockPlace()` - Reduce available slots
- `current.UnlockPlace()` - Increase available slots

---

### 12. Storage

**Class Hierarchy:** `Storage` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **All Goods** | `GameMB.StorageService.Main.Goods.goods.Keys` | All good types |
| **Good Amount** | `GameMB.StorageService.Main.GetAmount(goodName)` | Amount of specific good |
| **View Mode** | `GameMB.StoragePrefsService.IsUsingGrid()` | List vs grid view |
| **Search** | `searchBar.SetUp(allGoods, Rebuild)` | Filter goods by name |
| **Cycle Abilities** | `GameMB.StateService.Conditions.cycleAbilities` | Activatable abilities |

**Key Methods:**
- `GameMB.StoragePrefsService.SetUsingGrid(bool)` - Change view mode

---

### 13. Institution

**Class Hierarchy:** `Institution` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Recipes/Services** | `institution.state.recipes` (List<InstitutionRecipeState>) | Services provided |
| **Active Effects** | `institution.model.activeEffects` (InstitutionEffectModel[]) | Bonuses provided |
| **Storage Input** | `InstitutionIngredientsStoragePanel.SetUp(institution)` | Ingredients storage |

**Dual Purpose:** Shows both services (recipes) AND active effects (bonuses).

---

### 14. Decoration

**Class Hierarchy:** `Decoration` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Active Effects** | `decoration.model.activeEffects` | Passive effects from decoration |

**Simplest Panel:** Just shows passive effects provided by the decoration.

---

### 15. Simple

**Class Hierarchy:** Uses `Building` base

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Basic Info** | From `BuildingPanel` base | Name, description |
| **Hide on Finish** | `hideOnFinished` parameter | Auto-hide when construction completes |

**Minimal Panel:** Used during construction or for buildings with no special UI.

---

### 16. Hydrant

**Class Hierarchy:** `Hydrant` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Fuel Panel** | `BlightFuelPanel.SetUp()` | Blight fuel status |

**Simple Panel:** Only shows fuel status via `BlightFuelPanel`.

---

## Special Buildings (Require Dedicated Readers)

### 17. Hearth

**Class Hierarchy:** `Hearth` extends `Building`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Fire Panel** | `HearthFirePanel.SetUp(hearth)` | Fire level/fuel status |
| **Fuels** | `HearthFuelsPanel.SetUp()` | Available fuel types |
| **Hub** | `HearthHubPanel.SetUp(hearth)` | Town center functions |
| **Sacrifice Recipes** | `hearth.state.sacrificeRecipes` (List<HearthSacrificeState>) | Effects from sacrifice |
| **Stop After Storm** | `hearth.state.stopSacrificeAfterStorm` (bool) | Auto-stop toggle |
| **Blight Panel** | `HearthBlightPanel.SetUp(hearth)` | Only if main hearth + blight active |
| **Services** | `HearthServicesPanel.SetUp()` | Conditional on `MB.MetaPerksService.AreHearthServicesUnlocked()` |
| **Sacrifice Blocked** | `GameMB.EffectsService.IsHearthSacrificeBlocked.Value` | Check if effects blocked |

**Tabs:**
- Effects: `ShouldEnableEffectsPanel()`
- Blight: Only if main hearth + blight active
- Services: `MB.MetaPerksService.AreHearthServicesUnlocked()`

---

### 18. Relic (Investigation)

**Class Hierarchy:** `RelicPreview` extends `BuildingPreview` (not BuildingPanel!)

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Difficulty** | `relic.GetDifficulty(preferredDifficulty)` | Multi-tier difficulty |
| **Effects** | `relic.effectsTiers[]` / `relic.activeEffects[]` | Effects per difficulty tier |
| **Requirements** | `RelicPreviewRequirementsPanel.SetUp(relic, difficultyIndex)` | Investigation requirements |
| **Decision** | `relic.HasDecision`, `decisionIndex` | Choice-based outcomes |
| **Rewards** | `relic.rewardsTiers[]` / `relic.decisionsRewards[]` | Rewards per tier |
| **Workplaces** | `relic.workplaces.Length` | Worker slots |

**State Machine:**
1. Difficulty selection
2. Requirements gathering
3. Investigation active
4. Decision/rewards

---

### 19. Port (Expeditions)

**Class Hierarchy:** `Port` extends `Building` via `PortPanel`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Expedition State** | `port.WasExpeditionStarted()`, `port.AreRewardsWaiting()` | Current state |
| **Requirements** | `PortRequirementsPanel.SetUp(port, OnLevelChanged)` | Expedition requirements |
| **Time** | `PortTimePanel.SetUp(port)` | Travel time remaining |
| **Rewards** | `PortRewardsPanel.SetUp(port)` | When rewards waiting |
| **Rewards Pick** | `PortRewardsPickPanel.SetUp(port, callback)` | Before expedition |
| **Category Pick** | `port.state.pickedCategory`, `port.GetCurrentExpeditionModel().blueprints` | Building choice |
| **Level** | `port.ChangeLevel(level)` | Expedition difficulty |

**State Machine:**
1. Pre-expedition: Pick level, category, rewards
2. In progress: Timer counting
3. Rewards ready: Accept button

**Actions:**
- `port.LockDecision()` - Start expedition
- `port.CancelDecision()` - Cancel expedition
- `port.AcceptRewards()` - Claim rewards

---

### 20. Poro (Creature)

**Class Hierarchy:** `Poro` extends `Building` via `PoroPanel`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Needs** | `poro.model.needs` (PoroNeedModel[]), `poro.state.needs[]` (PoroNeedState[]) | Creature needs |
| **Happiness** | `PoroHappinessPanel.SetUp(poro)` | Overall happiness level |
| **Product** | `PoroProductPanel.SetUp(poro)` | What the creature produces |

**Unique:** Only building with creature needs system - each need has satisfaction level and produces different effects.

---

### 21. Shrine (Tiered Effects)

**Class Hierarchy:** `Shrine` extends `Building` via `ShrinePanel`

| Concept | Data Source | Notes |
|---------|-------------|-------|
| **Header** | `shrine.model.panelHeader.Text` | Shrine title |
| **Effects Tiers** | `shrine.model.effects` (ShrineEffectsModel[]), `shrine.state.effects[]` | Tiered effects |
| **Tooltip** | `shrine.model.panelTooltipHeader.Text`, `shrine.model.panelTooltipDesc.Text` | Help text |
| **Finished** | `shrine.state.finished` | Show effects panel only when built |

**Tiered System:** Unlike other buildings, shrine effects unlock in tiers as resources are delivered.
