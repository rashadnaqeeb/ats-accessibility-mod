# ShrineNavigator.cs
Navigator for Shrine buildings.
Shrines extend Building (not ProductionBuilding) and have no workers.
Provides Abilities section with tiered effects that can be used.

## class ShrineNavigator: BuildingSectionNavigator (line 13)

### Fields (private enum SectionType, line 18)
- `Effects`

### Fields
- `private` `string[]` `_sectionNames` (line 27)
- `private` `SectionType[]` `_sectionTypes` (line 28)
- `private` `List<EffectTierInfo>` `_effectTiers` (line 31)
- `private` `bool` `_awaitingConfirm` (line 34)
- `private` `int` `_confirmTierIndex` (line 35)
- `private` `int` `_confirmEffectIndex` (line 36)

### Fields (private class EffectTierInfo, line 41)
- `int` `TierIndex`
- `string` `Label`
- `int` `ChargesLeft`
- `int` `MaxCharges`
- `List<int>` `DrawableEffectIndices` - Effect indices that can be drawn (visible to sighted players)

### Properties
- `protected override` `string` `NavigatorName` (line 54) - Returns `"ShrineNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 56)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 60) - Effects tiers count, min 1 for empty message
- `protected override` `int` `GetSubItemCount(int sectionIndex, int itemIndex)` (line 72) - Shows drawable effects if MaxCharges == 0 (unlimited) or has charges remaining
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 88)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 93)
- `protected override` `void` `AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)` (line 104) - Announces actual effect name and description via `DrawableEffectIndices` mapping
- `protected override` `bool?` `HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)` (line 120) - Intercepts all keys when `_awaitingConfirm`; Enter/Space confirms; any other key cancels
- `protected override` `bool` `PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)` (line 137) - Checks charges; plays charging sound; sets `_awaitingConfirm = true`; announces "Enter to confirm"
- `protected override` `void` `RefreshData()` (line 168)
- `protected override` `void` `ClearData()` (line 178) - Also resets `_awaitingConfirm`
- `private` `void` `RefreshEffectData()` (line 189) - Builds `_effectTiers` from game data; only includes effects that pass `CanShrineTierEffectBeDrawn`
- `private` `void` `BuildSections()` (line 213) - Hardcodes single "Abilities" section
- `private` `void` `AnnounceEffectTierItem(int itemIndex)` (line 223) - MaxCharges == 0: "unlimited"; else charges remaining / no charges
- `private` `void` `DoUseEffect(int tierIndex, int effectIndex)` (line 244) - Calls `UseShrineEffect`; plays final sound; announces effect name; calls `RefreshEffectData`
