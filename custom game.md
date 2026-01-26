# Custom Game Popup Research

The "Training Expedition" accessed from the Capital overlay is actually the **Custom Game Popup** (`Eremite.WorldMap.UI.CustomGames.CustomGamePopup`).

## Overview

A highly configurable game setup screen that lets players customize nearly every aspect of a new game. Accessed via `WorldBlackboardService.CustomGamePopupRequested`.

## Popup Structure

### Left Panel (`Content/Left/Scroll View/Viewport/Content`)

1. **CustomGameDifficultyPicker** - Select difficulty level
2. **CustomGameBiomePanel** - Choose which biome to play
3. **CustomGameRacesPanel** - Select starting races
4. **CustomGameSeasonalEffectsPanel** - Configure positive/negative seasonal effects count, with sub-popup to pick specific effects
5. **CustomGameBlightPanel** - Toggle blight on/off, set footprint rate and corruption rate
6. **CustomGameSeedPanel** - Set map seed
7. **Layouts section** - Save/Load buttons for presets

### Middle Panel (`Content/Middle/Scroll View/Viewport/Content`)

8. **CustomGameReputationPanel** - Set reputation to win, impatience penalty, penalty rate, blueprints config
9. **CustomGameSeasonsDurationPanel** - Set drizzle/clearance/storm durations
10. **CustomGameTradeTownsPanel** - Configure trade town factions
11. **CustomGameEmbarkGoodsPanel** - Select starting goods
12. **CustomGameEmbarkEffectsPanel** - Select starting effects/perks

### Right Panel

13. **CustomGameModifiersPanel** - Pick game modifiers (categorized list with search)

### Buttons

- **embarkButton** - Start the game with selected settings
- **closeButton** - Close popup
- **saveButton** - Save current configuration as a layout
- **loadButton** - Load a saved layout

### Sub-popups

- **CustomGameLayoutsPopup** - For saving/loading preset configurations
- **SeasonalEffectsPickPopup** - For picking specific seasonal effects

## Data Flow

When Embark is clicked, creates a `CustomGameRequest` with all settings:
- seed, biome, races, modifiers, difficulty
- reputationToWin, reputationPenaltyToLoose, reputationPenaltyPerSec
- blueprintsConfig
- drizzleDuration, clearanceDuration, stormDuration
- seasonalEffects, positiveSeasonalEffectsAmount, negativeSeasonalEffectsAmount
- isBlightActive, blightFootprintRate, blightCorruptionRate
- tradeTowns, embarkGoods, embarkEffects

## Key Game Source Files

- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGamePopup.cs` - Main popup
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameModifiersPanel.cs` - Modifiers selection
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameDifficultyPicker.cs` - Difficulty picker
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameBiomePanel.cs` - Biome selection
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameRacesPanel.cs` - Race selection
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameSeasonalEffectsPanel.cs` - Seasonal effects
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameBlightPanel.cs` - Blight settings
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameReputationPanel.cs` - Reputation settings
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameSeasonsDurationPanel.cs` - Season durations
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameTradeTownsPanel.cs` - Trade towns
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameEmbarkGoodsPanel.cs` - Starting goods
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameEmbarkEffectsPanel.cs` - Starting effects
- `game-source/Eremite.WorldMap.UI.CustomGames/CustomGameLayoutsPopup.cs` - Save/load presets
- `game-source/Eremite.WorldMap.UI.CustomGames/SeasonalEffectsPickPopup.cs` - Effect picker
- `game-source/Eremite.WorldMap.ConditionsCreator/CustomGameRequest.cs` - Request data structure
- `game-source/Eremite.Model.State.CustomGames/CustomGameLayout.cs` - Saved layout structure

## Complexity Notes

This is a very complex popup with:
- Multiple scrollable panels
- Many configurable options with different input types (toggles, sliders, pickers, lists)
- Sub-popups for layouts and seasonal effects
- Search functionality in modifiers panel
- Save/load system for presets

An overlay would need to handle:
- Navigation between panels/sections
- Different interaction types per setting
- Sub-popup management
- Potentially large lists (modifiers, effects, goods)
