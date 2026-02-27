# InputBlocker.cs

Manages input blocking state and action whitelisting. When blocking is enabled, all game input
is blocked except whitelisted actions. Used by InputPatches to gate the Harmony prefix results.

## class InputBlocker (static) (line 9)

### Properties
- public static bool IsBlocking { get; set; } = true (line 14)
  - Whether input blocking is currently active. Can be temporarily disabled when editing text fields.
- public static bool BlockCancelOnce { get; set; } = false (line 20)
  - When true, blocks the Cancel action once then resets itself. Used when StatsPanel is closing to prevent game menu from opening.

### Fields
- private static readonly HashSet<string> WhitelistedActions (line 23)
  - Actions allowed through InputService even when blocking: "Confirm", "Cancel", "ContinueTutorial".

### Methods
- public static bool IsActionWhitelisted(InputAction action) (line 33)
  - Returns true if the action's name is in the whitelist.
