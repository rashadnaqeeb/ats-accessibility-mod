# Sealed Forest Accessibility Implementation

Research notes for implementing accessibility support for the Sealed Forest biome.

## Overview

The Sealed Forest is a special biome containing a "Seal" building that must be completed to win. Key components:

1. **Guideposts** - Spawn when glades are revealed, point toward the seal location
2. **Seal Building** - Multi-stage objective building with plague effects
3. **Plagues** - Negative effects that activate during Storm season

---

## Guidepost Building

### Purpose

Guideposts spawn automatically when glades are revealed (before the seal is discovered). They visually rotate to point toward the seal's location, helping players navigate.

### Key Classes

- `SealGuidepostView` (Eremite.Buildings) - Contains direction calculation
- `SealGuidepostController` (Eremite.Controller.Buildings) - Spawns guideposts

### Direction Calculation

The guidepost calculates direction in `SealGuidepostView`:

```csharp
private float GetRotation()
{
    Vector2Int targetField = GetTargetField();
    Vector3 center = GetCenter(targetField);
    return GetTargetRotation(center - base.Position) + 90f;
}

private float GetTargetRotation(Vector3 dir)
{
    return Mathf.Atan2(dir.z, -dir.x) * 57.29578f;  // radians to degrees
}
```

**Target selection:**
- If seal discovered: Points to `BuildingsService.Seals.First()`
- If seal not discovered: Points to `GameSealService.GetSealField()` coordinate

### Compass Direction Mapping

The angle result (0-360 degrees) maps to:
- 0° = West
- 90° = North
- 180° = East
- 270° = South
- Diagonals at 45° intervals (NW, NE, SE, SW)

### Accessibility Implementation ✓ DONE

Implemented in `TileInfoReader.cs` - when I key pressed on guidepost:
- Checks if building view is `SealGuidepostView`
- Calculates direction using same math as game
- Announces "Pointing [direction]" (8-point compass)

**No UI exists** - The game only shows direction through the 3D model rotation. Accessibility announcement is the only way for blind players to get this information.

---

## Seal Building - Complete Flow

### Visual Layout of SealPanel

When a player clicks on the seal building, the `SealPanel` popup opens:

```
┌─────────────────────────────────────────────────────────────┐
│  [Hide Button - X]                                          │
├─────────────────────────────────────────────────────────────┤
│  GUARDIAN PANEL (Progress Bar)                              │
│  ┌────┐  ┌────┐  ┌────┐  ┌────┐                            │
│  │ ✓  │  │ ✓  │  │ ▶  │  │ ○  │   (4 kit slots)           │
│  │Kit1│  │Kit2│  │Kit3│  │Kit4│                            │
│  └────┘  └────┘  └────┘  └────┘                            │
│  (completed) (completed) (current) (future)                 │
├─────────────────────────────────────────────────────────────┤
│  DIALOGUE PANEL                                             │
│  "The seal weakens. Deliver more offerings to proceed..."   │
│  (narrative text that types out, different for each kit)    │
├─────────────────────────────────────────────────────────────┤
│  PART SLOTS (Objectives for current kit)                    │
│  ┌──────────────────────┐  ┌──────────────────────┐        │
│  │ [Icon] Sacred Water  │  │ [Icon] Ancient Amber │        │
│  │ ○ 15/30 Water        │  │ ○ 8/20 Amber         │        │
│  │ ○ 5/10 Herbs         │  │ ○ 12/15 Wood         │        │
│  │ [Track] [Deliver]    │  │ [Track] [Deliver]    │        │
│  └──────────────────────┘  └──────────────────────┘        │
│  (Part 1 - choice A)       (Part 2 - choice B)              │
└─────────────────────────────────────────────────────────────┘
```

**Important:** `SealEffectsPanel` (plague info) is NOT part of SealPanel - it's a separate HUD element that appears elsewhere on screen.

### Panel Components

| Component | Class | Purpose |
|-----------|-------|---------|
| Guardian Panel | `SealGuardianPanel` | Progress through all 4 kits |
| Dialogue Panel | `SealDialoguePanel` | Narrative text for current kit |
| Part Slots | `PartSlot[]` | Objectives for current kit (player picks ONE) |
| Hide Button | `Button` | Close panel |

---

## Component Details

### 1. Guardian Panel (`SealGuardianPanel`)

**What it shows:** Visual progress through all 4 kits

**Structure:**
- Contains `SealGuardianPartSlot[]` - one slot per kit (always 4)
- Each slot shows:
  - **Completed kits**: Icon of the part the player chose, tooltip shows part name/description
  - **Current kit**: Highlighted (forced highlight state)
  - **Future kits**: Kit's generic icon (greyed out)

**Data source:**
```csharp
seal.state.kits[]                    // Array of SealKitState
seal.model.kitsLevels[]              // Array of SealKitModel
seal.IsKitCompleted(kitState)        // Check if completedIndex >= 0
seal.GetCompletedPartFor(kitState)   // Get the SealPartModel chosen
seal.GetFirstUncompletedKit()        // Get current kit state
```

**Tooltip for each slot:**
```csharp
// Header (completed kits only):
seal.GetCompletedPartFor(state).displayName.Text

// Description:
if (IsCompleted())
    seal.GetCompletedPartFor(state).description.Text
else
    model.description.Text  // Kit description for incomplete
```

**User interaction:** Hover only - shows tooltip with part description

---

### 2. Dialogue Panel (`SealDialoguePanel`)

**What it shows:** Narrative text for the current kit

**Data source:**
```csharp
// Current kit's dialogue
SealKitModel kitModel = seal.GetModelFor(seal.GetFirstUncompletedKit());
string dialogue = kitModel.dialogue.Text;

// Final dialogue (when all kits complete)
string finalDialogue = seal.model.finalDialogue.Text;
```

**Behavior:**
- Text "types out" with animation when kit changes (0.5s delay)
- Shows regular text (no animation) if revisiting same kit
- Shows `finalDialogue` when seal is completed

**User interaction:** None - display only

---

### 3. Part Slots (`PartSlot[]`)

**What they show:** The alternative objectives for the current kit. Player picks ONE to complete the kit.

**Structure per slot:**
```csharp
nameText           // TMP_Text - Part name (e.g., "Sacred Water Offering")
icon               // Image - Part icon sprite
objectivesSlots[]  // List<ObjectiveSlot> - Goods requirements
trackToggle        // ToggleButton - Track this order on HUD
completeButton     // ButtonAdv - "Deliver" button
```

**Data source:**
```csharp
SealKitState currentKit = seal.GetFirstUncompletedKit();
SealKitModel kitModel = seal.GetModelFor(currentKit);

// Parts array (alternatives for this kit)
SealPartModel[] parts = kitModel.parts;

// Each part has:
parts[i].displayName.Text   // "Sacred Water Offering"
parts[i].description.Text   // Detailed description (shown on icon hover)
parts[i].icon               // Sprite
parts[i].order              // OrderModel with delivery requirements

// Runtime state for each part:
currentKit.orders[i]        // OrderState with progress tracking
```

**Objectives within each part:**
```csharp
OrderModel orderModel = part.order;
OrderLogic[] logics = orderModel.GetLogics(0);  // Get objective requirements

// Each logic provides:
logic.DisplayName              // "Water"
logic.Icon                     // Sprite
logic.GetAmountText(state)     // "15/30" (current/required)

// State tracking:
OrderState orderState = currentKit.orders[partIndex];
ObjectiveState[] objectives = orderState.objectives;
objectives[i].amount           // Current amount delivered
objectives[i].completed        // Is this objective done?
```

**Button text:**
```csharp
// Default: "Deliver" (from localization key "GameUI_Orders_Deliver")
// Custom: orderModel.customDeliverText.Text (if hasCustomDeliverText)
```

**User interactions:**
1. **Track toggle** - Adds/removes this order from SealsOrdersHUD (corner display)
   - Fires `GameBlackboardService.OnExternalOrderTrackingChanged`
2. **Deliver button** - Completes this part when all objectives met
   - Enabled when `GameMB.OrdersService.CanComplete(orderState, orderModel)` is true

---

### 4. Seal Effects Panel (`SealEffectsPanel`) - SEPARATE HUD Element

**Important:** This is NOT part of SealPanel. It's a standalone HUD component.

**What it shows:** Current plague (during Storm) or upcoming plague (during other seasons)

**Data source:**
```csharp
SealGameState state = GameMB.StateService.SealGame;

// Check if plague is active:
bool isActive = state.currentEffect.IsNotNone();

// Get effect to display:
string effectName = isActive ? state.currentEffect : state.nextEffect;
EffectModel effect = GameMB.GameModelService.GetEffect(effectName);

// Effect info:
effect.displayName.Text  // Plague name
effect.description.Text  // What it does
effect.icon              // Sprite
```

**Header text:**
- During Storm: "Current Effect" (`GameUI_SealPanel_EffectsPanel_Header_Current`)
- Otherwise: "Next Effect" (`GameUI_SealPanel_EffectsPanel_Header`)

**Timer display (only shown when plague not active):**
```csharp
float timeLeft = GameMB.CalendarService.GetSecondsLeftTo(
    new GameDate(GameMB.CalendarService.Year, Season.Storm));
string timerText = MB.RichTextService.GetMinSecTimer(timeLeft);
```

---

### 5. Kit Reward Panel (`SealKitRewardPanel`) - SEPARATE HUD Element

**What it shows:** The reward effect for completing the current kit

**Data source:**
```csharp
Seal seal = GameMB.BuildingsService.Seals.Values.First();
SealKitModel kitModel = seal.GetFirstUncompletedKitModel();
EffectModel reward = kitModel.reward;  // Bonus effect granted on completion
```

**Behavior:**
- Deactivates when seal is completed
- Updates on `OnKitCompleted` event

---

## Data Models

### Static Configuration (SealModel)

```csharp
public class SealModel : BuildingModel
{
    public Seal prefab;
    public LocaText finalDialogue;           // Shown when seal complete
    public SealEffectLevel[] effectsLevels;  // Plague configuration
    public SealKitModel[] kitsLevels;        // The 4 kits to complete
}

public class SealKitModel
{
    public Sprite icon;
    public LocaText dialogue;      // Narrative text for this kit
    public LocaText description;   // Kit description (for guardian tooltip)
    public EffectModel reward;     // Bonus effect on completion
    public SealPartModel[] parts;  // Alternative objectives (pick one)
}

public class SealPartModel
{
    public Sprite icon;
    public LocaText displayName;   // "Sacred Water Offering"
    public LocaText description;   // Detailed description
    public OrderModel order;       // What to deliver
    public GameObject prefab;      // Visual prefab for guardian panel
}
```

### Runtime State

```csharp
public class SealState : BuildingState
{
    public SealKitState[] kits;    // Runtime state for each kit
}

public class SealKitState
{
    public int completedIndex = -1;  // -1 = incomplete, 0-N = chosen part index
    public OrderState[] orders;      // One per part in this kit
}

// From BaseOrderState:
public class OrderState
{
    public bool started;
    public bool completed;
    public bool tracked;             // Is this shown in SealsOrdersHUD?
    public ObjectiveState[] objectives;
}

public class ObjectiveState
{
    public OrderLogicType type;
    public int amount;               // Current progress
    public bool completed;           // This objective satisfied?
}
```

### State Hierarchy Visualization

```
SealModel (static config)
├── finalDialogue
├── kitsLevels[4] (SealKitModel[])
│   ├── dialogue, description, icon
│   ├── reward (EffectModel)
│   └── parts[] (SealPartModel[])
│       ├── displayName, description, icon
│       └── order (OrderModel)
│           └── logics[] (OrderLogic[])

Seal.state (runtime)
├── kits[4] (SealKitState[])
│   ├── completedIndex (-1 = incomplete, 0-N = chosen part)
│   └── orders[] (OrderState[])
│       ├── tracked (bool)
│       └── objectives[] (ObjectiveState[])
│           ├── amount (current progress)
│           └── completed (objective done?)
```

---

## Key Methods on Seal Class

```csharp
// Get current kit to complete
seal.GetFirstUncompletedKit() → SealKitState

// Get model for current kit
seal.GetFirstUncompletedKitModel() → SealKitModel

// Get model for any kit state
seal.GetModelFor(SealKitState) → SealKitModel

// Check completion
seal.IsKitCompleted(SealKitState) → bool  // completedIndex >= 0
seal.IsSealCompleted() → bool             // All kits completed

// Get completed part info (for guardian panel)
seal.GetCompletedPartFor(SealKitState) → SealPartModel

// Check if can deliver a part
GameMB.OrdersService.CanComplete(orderState, orderModel) → bool
```

---

## Completion Flow

1. **Player opens seal panel** by clicking the seal building
   - `Seal.OnPicked()` → `SealPanel.Instance.Show(this)`

2. **Panel displays current kit's parts** with their delivery requirements
   - `SealPanel.SetUpSlots()` creates `PartSlot` for each part

3. **Player gathers resources** and delivers goods to storage
   - Game's normal delivery system updates `ObjectiveState.amount`

4. **Progress updates automatically**
   - `ObjectiveSlot` counters refresh via `Update()` if `selfUpdate` is true
   - Or manually via `SetUpCounter()`

5. **When one part's requirements are met**, its "Deliver" button enables
   - `PartSlot.UpdateButton()` checks `OrdersService.CanComplete()`

6. **Player clicks Deliver**
   - `PartSlot.Complete()` → callback → `SealPanel.OnPartPicked(index)`

7. **`GameSealService.CompletePart()` executes:**
   ```csharp
   void CompletePart(SealKitState state, SealKitModel model, int index)
   {
       CompleteOrder(state, index);    // Mark order complete, cancel other parts
       MarkAsCompleted(state, index);  // Set completedIndex = index
       GrantReward(model);             // Apply kit's reward effect
       DispatchCompletedEvent(state);  // Fire OnKitCompleted
       MonitorNextOrders();            // Start tracking next kit's orders
   }
   ```

8. **Panel refreshes** via `OnKitCompleted` subscription
   - If seal complete: Hide panel
   - Otherwise: `SetUpSlots()` and `SetUpDialogue()` for next kit

---

## Seal Orders vs Regular Orders

Seal orders are **completely separate** from the regular orders system:

| Aspect | Regular Orders | Seal Orders |
|--------|---------------|-------------|
| **Storage** | `IOrdersService.Orders` list | `Seal.state.kits[].orders[]` on building |
| **HUD** | `OrdersHUD` | `SealsOrdersHUD` (separate display) |
| **Overlay** | F2 > Orders (`OrdersOverlay`) | Only via seal building panel |
| **Events** | `OnOrderStarted/Completed` | `OnExternalOrderStarted/Completed` |
| **Completion** | `CompleteOrder()` | `CompleteExternalOrder()` |

**Key points:**
- Seal orders never appear in F2 > Orders overlay
- The game has a separate `SealsOrdersHUD` for tracked seal orders
- All seal order interaction happens through the `SealPanel` popup

### SealsOrdersHUD - Not Needed for Accessibility

The `SealsOrdersHUD` is a visual convenience that shows tracked seal orders in the corner of the screen. It provides:
- Progress display for tracked orders (e.g., "3/5 lumber")
- No actions - purely informational

**For accessibility, we don't need to implement this.** The seal building panel provides all the same information (and more). Players can:
1. Navigate to the seal (using guidepost direction)
2. Open the panel to see all objectives and progress
3. Deliver when ready

The HUD is only useful for "at a glance" visual checking, which doesn't benefit blind players.

---

## Plague System

### State

```csharp
public class SealGameState
{
    public string nextEffect;                    // Upcoming plague
    public string currentEffect;                 // Active plague (Storm only)
    public List<string> usedEffects = new();     // History for rotation
}
```

### Lifecycle

Managed by `SealPlaguesController`:

1. **Game Start**: `nextEffect` generated immediately
2. **Storm Season Start**:
   - `currentEffect = nextEffect` (plague activates)
   - New `nextEffect` generated
3. **Drizzle Season Start**:
   - `currentEffect` removed (plague ends)

### Plague Effects

Plagues are `EffectModel` objects with negative effects. Applied via:
```csharp
effect.Apply(EffectContextType.Building, sealModelName, sealId);
effect.Remove(EffectContextType.Building, sealModelName, sealId);
```

### Alerts

`SealPlaguesMonitor` shows HUD alert when plague activates:
- Only shows during Storm (when `currentEffect` is set)
- Shows plague name and description
- Clicking focuses the seal building

---

## Accessibility Implementation Plan

### 1. Guidepost Direction (TileInfoReader) ✓ DONE

Implemented - announces "Pointing [direction]" when I key pressed on guidepost.

### 2. Seal Building Overlay (SealOverlay)

Section-based navigator similar to other building overlays.

**Design Note:** The game's `SealEffectsPanel` (plague info) is a separate HUD element that appears elsewhere on screen, not part of `SealPanel`. For accessibility simplicity, we consolidate this into an **Effects** section within our overlay, so players can access all seal-related information in one place without needing to find a separate HUD element.

**Sections:**
1. **Effects** - Plague information (from `SealEffectsPanel` data)
2. **Progress** - "Kit 2 of 4. Completed: Sacred Water, Ancient Amber"
3. **Dialogue** - Current narrative text
4. **Parts** (main focus) - Navigate between alternative objectives
5. **Reward** - Effect granted for completing current kit

#### Effects Section Details

This section presents the plague information that the game shows in `SealEffectsPanel`:

**Data source:**
```csharp
SealGameState state = GameMB.StateService.SealGame;

// Check if plague is currently active (Storm season):
bool isActive = state.currentEffect.IsNotNone();

// Get the relevant effect:
string effectName = isActive ? state.currentEffect : state.nextEffect;
EffectModel effect = GameMB.GameModelService.GetEffect(effectName);

// Timer (only relevant when plague NOT active):
float secondsUntilStorm = GameMB.CalendarService.GetSecondsLeftTo(
    new GameDate(GameMB.CalendarService.Year, Season.Storm));
```

**Announcement format:**
- During Storm: "Current plague: [name]. [description]"
- Before Storm: "Next plague: [name]. [description]. Activates in [time]"

**Example announcements:**
- "Current plague: Spreading Corruption. All buildings produce 20% less goods."
- "Next plague: Resource Drain. Storage capacity reduced by 30%. Activates in 2 minutes 45 seconds."

#### Parts Section Details

**Within Parts section:**
- Navigate between parts (alternatives)
- For each part: name, objectives with progress
- Announce "Deliverable" when ready
- Space/Enter to deliver

**Navigation:**
- Up/Down: Navigate sections/parts
- Left/Escape: Back out / close
- Enter/Space: Deliver (when on deliverable part)
- T: Toggle tracking for current part

**Example announcements:**
- Section: "Parts, 2 alternatives"
- Part: "Sacred Water Offering. Water 15 of 30. Herbs 5 of 10. In progress"
- Part ready: "Ancient Amber Offering. Amber 20 of 20. Wood 15 of 15. Deliverable"
- Progress: "Kit 2 of 4. Completed: Sacred Water, Ancient Amber"
- Reward: "Reward: Amber Attraction - increases amber production by 20%"

### 3. Plague Announcements (EventAnnouncer)

Subscribe to season changes when in Sealed Forest:
- Storm start: "Plague activated: [name]. [description]"
- Drizzle start: "Plague ended"

**Data access:**
```csharp
SealGameState state = GameReflection.GetSealGameState();
string currentEffect = state.currentEffect;
string nextEffect = state.nextEffect;
EffectModel effect = GameReflection.GetEffect(effectName);
```

---

## File References

**Game Source:**
- `game-source/Eremite.Buildings/Seal.cs` - Main seal building class
- `game-source/Eremite.Buildings/SealModel.cs` - Static configuration
- `game-source/Eremite.Buildings/SealState.cs` - Runtime state
- `game-source/Eremite.Buildings/SealKitModel.cs` - Kit configuration
- `game-source/Eremite.Buildings/SealKitState.cs` - Kit runtime state
- `game-source/Eremite.Buildings/SealPartModel.cs` - Part (objective) configuration
- `game-source/Eremite.Buildings/SealGuidepostView.cs` - Guidepost direction
- `game-source/Eremite.Buildings.UI.Seals/SealPanel.cs` - Main popup panel
- `game-source/Eremite.Buildings.UI.Seals/SealGuardianPanel.cs` - Progress display
- `game-source/Eremite.Buildings.UI.Seals/SealGuardianPartSlot.cs` - Individual kit slot
- `game-source/Eremite.Buildings.UI.Seals/SealDialoguePanel.cs` - Narrative text
- `game-source/Eremite.Buildings.UI.Seals/PartSlot.cs` - Part objective slot
- `game-source/Eremite.Buildings.UI.Seals/SealEffectsPanel.cs` - Plague HUD (separate)
- `game-source/Eremite.Buildings.UI.Seals/SealKitRewardPanel.cs` - Reward HUD (separate)
- `game-source/Eremite.View.HUD.Orders/ObjectiveSlot.cs` - Goods requirement display
- `game-source/Eremite.Model.Orders/OrderState.cs` - Order runtime state
- `game-source/Eremite.Model.Orders/ObjectiveState.cs` - Objective progress
- `game-source/Eremite.Model.State/SealGameState.cs` - Plague state
- `game-source/Eremite.Controller.Buildings/SealPlaguesController.cs` - Plague lifecycle
- `game-source/Eremite.Services.Monitors/SealPlaguesMonitor.cs` - Plague HUD alert
- `game-source/Eremite.Services/GameSealService.cs` - Seal completion service

**Mod Files:**
- `GameReflection.cs` - Added seal/guidepost reflection helpers ✓
- `TileInfoReader.cs` - Added guidepost direction ✓
- `SealOverlay.cs` - To be created for seal panel navigation
- `EventAnnouncer.cs` - To be updated for plague announcements
