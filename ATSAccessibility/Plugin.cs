using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ATSAccessibility
{
    [BepInPlugin("com.accessibility.ats", "ATS Accessibility", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        // ========================================
        // CONFIG FILE ACCESS
        // ========================================
        public static ConfigFile ModConfig { get; private set; }

        // ========================================
        // ANNOUNCEMENT CONFIG ENTRIES
        // ========================================
        // Note: Some events are handled by the game's built-in alert system (IMonitorsService)
        // and are controlled via the game's own settings. We only add custom announcements
        // for events not covered by the game's alerts.

        // Game Alerts (uses game's built-in alert system)
        public static ConfigEntry<bool> AnnounceGameAlerts;

        // Buildings (construction complete not covered by game alerts)
        public static ConfigEntry<bool> AnnounceConstructionComplete;
        public static ConfigEntry<bool> AnnounceHearthLevelChange;
        public static ConfigEntry<bool> AnnounceHearthIgnited;
        public static ConfigEntry<bool> AnnounceHearthCorrupted;
        public static ConfigEntry<bool> AnnounceSacrificeStopped;

        // Exploration
        public static ConfigEntry<bool> AnnounceGladeRevealed;
        public static ConfigEntry<bool> AnnounceRelicResolved;
        public static ConfigEntry<bool> AnnounceRewardChase;

        // Villagers (newcomers joined not covered by game alerts)
        public static ConfigEntry<bool> AnnounceVillagersArrived;
        public static ConfigEntry<bool> AnnounceVillagerLost;

        // Time & Weather
        public static ConfigEntry<bool> AnnounceSeasonChanged;
        public static ConfigEntry<bool> AnnounceYearChanged;

        // Trade (trader departed not covered by game alerts)
        public static ConfigEntry<bool> AnnounceTraderDeparted;

        // Orders (order available and failed not covered by game alerts)
        public static ConfigEntry<bool> AnnounceOrderAvailable;
        public static ConfigEntry<bool> AnnounceOrderFailed;

        // Threats (hostility level change gives more detail than game's deadly-only alert)
        public static ConfigEntry<bool> AnnounceHostilityLevelChange;

        // Progression
        public static ConfigEntry<bool> AnnounceReputationChanged;
        public static ConfigEntry<bool> AnnounceGoodDiscovered;
        public static ConfigEntry<bool> AnnounceGameResult;
        public static ConfigEntry<bool> AnnounceBlueprintAvailable;
        public static ConfigEntry<bool> AnnounceCornerstoneAvailable;

        // Resources
        public static ConfigEntry<bool> AnnouncePortExpeditionFinished;

        // News/Warnings
        public static ConfigEntry<bool> AnnounceGameWarnings;

        private void Awake()
        {
            try
            {
                // CRITICAL: Set DLL directory FIRST before any other code
                // This allows Tolk.dll to be found when P/Invoke calls are made
                string modFolder = Path.GetDirectoryName(Info.Location);
                bool result = SetDllDirectory(modFolder);
                Logger.LogInfo($"SetDllDirectory({modFolder}): {result}");

                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.LogWarning($"SetDllDirectory failed with error code: {error}");
                }

                // Store config reference for other classes
                ModConfig = Config;

                // Initialize announcement config entries (all ON by default)
                InitializeAnnouncementConfig();

                // Apply Harmony patches to block game input
                var harmony = new Harmony("com.ats.accessibility");
                harmony.PatchAll();

                // Register manual patches that need runtime type resolution
                EventAnnouncer.RegisterSacrificeStoppedPatch(harmony);

                Logger.LogInfo("Harmony patches applied");

                // Create persistent GameObject that survives scene transitions
                var go = new GameObject("ATSAccessibilityCore");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AccessibilityCore>();

                Logger.LogInfo("ATS Accessibility mod initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize: {ex}");
            }
        }

        private void InitializeAnnouncementConfig()
        {
            // Game Alerts (uses game's built-in alert system - IMonitorsService)
            // This covers: newcomers waiting, villager loss, trader arrived, building destroyed,
            // hearth fire died, blight, order completed, and many more
            AnnounceGameAlerts = Config.Bind("Announcements.GameAlerts",
                "GameAlerts", true, "Announce game's built-in alerts (uses game's alert settings)");

            // Buildings (construction complete not covered by game alerts)
            AnnounceConstructionComplete = Config.Bind("Announcements.Buildings",
                "ConstructionComplete", true, "Announce when construction finishes");
            AnnounceHearthLevelChange = Config.Bind("Announcements.Buildings",
                "HearthLevelChange", true, "Announce when hearth level changes");
            AnnounceHearthIgnited = Config.Bind("Announcements.Buildings",
                "HearthIgnited", true, "Announce when hearth is ignited");
            AnnounceHearthCorrupted = Config.Bind("Announcements.Buildings",
                "HearthCorrupted", true, "Announce when hearth is corrupted");
            AnnounceSacrificeStopped = Config.Bind("Announcements.Buildings",
                "SacrificeStopped", true, "Announce when hearth sacrifice stops (ran out of goods)");

            // Exploration
            AnnounceGladeRevealed = Config.Bind("Announcements.Exploration",
                "GladeRevealed", true, "Announce when a glade is revealed");
            AnnounceRelicResolved = Config.Bind("Announcements.Exploration",
                "RelicResolved", true, "Announce when a relic is resolved");
            AnnounceRewardChase = Config.Bind("Announcements.Exploration",
                "RewardChase", true, "Announce reward chase start/end");

            // Villagers (newcomers joined not covered by game alerts)
            AnnounceVillagersArrived = Config.Bind("Announcements.Villagers",
                "VillagersArrived", true, "Announce when newcomers are picked");
            AnnounceVillagerLost = Config.Bind("Announcements.Villagers",
                "VillagerLost", true, "Announce when a villager dies or leaves");

            // Time & Weather
            AnnounceSeasonChanged = Config.Bind("Announcements.Time",
                "SeasonChanged", true, "Announce season changes");
            AnnounceYearChanged = Config.Bind("Announcements.Time",
                "YearChanged", true, "Announce year changes");

            // Trade (trader departed not covered by game alerts)
            AnnounceTraderDeparted = Config.Bind("Announcements.Trade",
                "TraderDeparted", true, "Announce when a trader departs");

            // Orders (order available and failed not covered by game alerts)
            AnnounceOrderAvailable = Config.Bind("Announcements.Orders",
                "OrderAvailable", true, "Announce when a new order is available");
            AnnounceOrderFailed = Config.Bind("Announcements.Orders",
                "OrderFailed", true, "Announce when an order fails");

            // Threats (hostility level change gives more detail than game's deadly-only alert)
            AnnounceHostilityLevelChange = Config.Bind("Announcements.Threats",
                "HostilityLevelChange", true, "Announce hostility level changes");

            // Progression
            AnnounceReputationChanged = Config.Bind("Announcements.Progression",
                "ReputationChanged", true, "Announce reputation changes");
            AnnounceGoodDiscovered = Config.Bind("Announcements.Progression",
                "GoodDiscovered", true, "Announce when a new good is discovered");
            AnnounceGameResult = Config.Bind("Announcements.Progression",
                "GameResult", true, "Announce game won/lost");
            AnnounceBlueprintAvailable = Config.Bind("Announcements.Progression",
                "BlueprintAvailable", true, "Announce when a new blueprint is available to pick");
            AnnounceCornerstoneAvailable = Config.Bind("Announcements.Progression",
                "CornerstoneAvailable", true, "Announce when a new cornerstone is available to pick");

            // Resources
            AnnouncePortExpeditionFinished = Config.Bind("Announcements.Resources",
                "PortExpeditionFinished", true, "Announce when a port expedition finishes");

            // News/Warnings
            AnnounceGameWarnings = Config.Bind("Announcements.Alerts",
                "GameWarnings", true, "Announce game warnings from news service");

            Logger.LogInfo("Announcement config entries initialized");
        }
    }
}
