# Plugin.cs

BepInEx plugin entry point. Sets the Tolk DLL directory, initializes BepInEx config entries
for all announcement toggles, applies Harmony patches, and creates the persistent
AccessibilityCore GameObject.

Plugin ID: "com.accessibility.ats", name: "ATS Accessibility", version: "1.1.2"

## class Plugin: BaseUnityPlugin (line 12)

[BepInPlugin("com.accessibility.ats", "ATS Accessibility", "1.1.2")]

### Fields (P/Invoke)
- private static extern bool SetDllDirectory(string lpPathName) (line 14)
  - kernel32.dll import. Sets DLL search directory so Tolk.dll is found.

### Properties (Config)
- public static ConfigFile ModConfig { get; private set; } (line 19)
  - Config file reference exposed for other classes.

### Config Entries — Game Alerts
- public static ConfigEntry<bool> AnnounceGameAlerts (line 29)

### Config Entries — Buildings
- public static ConfigEntry<bool> AnnounceConstructionComplete (line 32)
- public static ConfigEntry<bool> AnnounceHearthLevelChange (line 33)
- public static ConfigEntry<bool> AnnounceHearthIgnited (line 34)
- public static ConfigEntry<bool> AnnounceHearthCorrupted (line 35)
- public static ConfigEntry<bool> AnnounceSacrificeStopped (line 36)

### Config Entries — Exploration
- public static ConfigEntry<bool> AnnounceGladeRevealed (line 39)
- public static ConfigEntry<bool> AnnounceRelicResolved (line 40)
- public static ConfigEntry<bool> AnnounceRewardChase (line 41)
- public static ConfigEntry<bool> AnnounceLocateMarkers (line 42)

### Config Entries — Villagers
- public static ConfigEntry<bool> AnnounceNewcomersWaiting (line 45)
- public static ConfigEntry<bool> AnnounceVillagerLost (line 46)

### Config Entries — Time & Weather
- public static ConfigEntry<bool> AnnounceSeasonChanged (line 49)
- public static ConfigEntry<bool> AnnounceYearChanged (line 50)

### Config Entries — Trade
- public static ConfigEntry<bool> AnnounceTraderDeparted (line 53)

### Config Entries — Orders
- public static ConfigEntry<bool> AnnounceOrderAvailable (line 56)
- public static ConfigEntry<bool> AnnounceOrderCompleted (line 57)
- public static ConfigEntry<bool> AnnounceOrderFailed (line 58)

### Config Entries — Threats
- public static ConfigEntry<bool> AnnounceHostilityLevelChange (line 61)

### Config Entries — Progression
- public static ConfigEntry<bool> AnnounceReputationChanged (line 64)
- public static ConfigEntry<bool> AnnounceGoodDiscovered (line 65)
- public static ConfigEntry<bool> AnnounceGameResult (line 66)
- public static ConfigEntry<bool> AnnounceBlueprintAvailable (line 67)
- public static ConfigEntry<bool> AnnounceCornerstoneAvailable (line 68)

### Config Entries — Resources
- public static ConfigEntry<bool> AnnouncePortExpeditionStarted (line 71)

### Config Entries — News/Warnings
- public static ConfigEntry<bool> AnnounceGameWarnings (line 74)

### Config Entries — Sealed Forest
- public static ConfigEntry<bool> AnnouncePlagueEvents (line 77)

### Config Entries — Scanner
- public static ConfigEntry<bool> ScannerAutoMove (line 80)

### Config Entries — Navigation
- public static ConfigEntry<bool> AnnounceCoordinates (line 83)

### Methods
- private void Awake() (line 85)
  - Sets DLL directory, stores config, calls InitializeAnnouncementConfig(), applies Harmony patches (including manual EventAnnouncer.RegisterSacrificeStoppedPatch), creates and DontDestroyOnLoad the AccessibilityCore GameObject.
- private void InitializeAnnouncementConfig() (line 125)
  - Binds all ConfigEntry<bool> fields to their BepInEx config sections and keys. All default to true except ScannerAutoMove (false) and AnnounceCoordinates (false).
