# HydrantNavigator.cs
Navigator for Hydrant buildings.
Hydrants extend Building (not ProductionBuilding) so they have no workers.
Provides Fuel section only.

## class HydrantNavigator: BuildingSectionNavigator (line 11)

### Fields (private enum SectionType, line 16)
- `Fuel`

### Fields
- `private` `string[]` `_sectionNames` (line 24)
- `private` `SectionType[]` `_sectionTypes` (line 25)
- `private` `int` `_freeCysts` (line 28)
- `private` `int` `_fuelAmount` (line 29)
- `private` `string` `_fuelDisplayName` (line 30)

### Properties
- `protected override` `string` `NavigatorName` (line 36) - Returns `"HydrantNavigator"`

### Methods
- `protected override` `string[]` `GetSections()` (line 38)
- `protected override` `int` `GetItemCount(int sectionIndex)` (line 42) - Fuel: always 2 (cysts, fuel amount)
- `protected override` `void` `AnnounceSection(int sectionIndex)` (line 54)
- `protected override` `void` `AnnounceItem(int sectionIndex, int itemIndex)` (line 59)
- `protected override` `void` `RefreshData()` (line 70)
- `protected override` `void` `ClearData()` (line 77)
- `private` `void` `RefreshFuelData()` (line 86)
- `private` `void` `BuildSections()` (line 92) - Hardcodes single "Fuel" section
- `private` `void` `AnnounceFuelItem(int itemIndex)` (line 102) - Index 0: "Free cysts: N"; Index 1: "FuelName: N (status)" where status is sufficient/low/medium/high
- `private` `string` `GetFuelStatus()` (line 116) - Returns "sufficient" if no free cysts; else ratio-based "low"/"medium"/"high"
