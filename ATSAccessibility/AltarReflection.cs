using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to Altar (Forsaken Altar) building internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, buildings) - they are destroyed on scene change
    /// </summary>
    public static class AltarReflection
    {
        // ========================================
        // DATA STRUCTURES
        // ========================================

        public struct CurrencyInfo
        {
            public string Name;
            public string DisplayName;
            public int Amount;
            public bool Enabled;
            public int MetaValueRate;
            public int MetaValue;  // Amount * MetaValueRate
        }

        public struct RaceInfo
        {
            public string Name;
            public string DisplayName;
            public int Count;
            public bool Enabled;
            public bool Revealed;
        }

        public struct EffectInfo
        {
            public object Model;           // AltarEffectModel
            public string DisplayName;
            public string Description;
            public int MetaPrice;          // Full meta price (includes villager conversion if disabled)
            public int VillagersPrice;     // Number of villagers required
            public bool CanAfford;
            public bool IsUpgrade;         // true if upgrading an existing cornerstone
        }

        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // AltarPanel type detection
        private static Type _altarPanelType = null;
        private static PropertyInfo _altarPanelInstanceProperty = null;

        // IGameServices properties
        private static PropertyInfo _gsAltarServiceProperty = null;
        private static PropertyInfo _gsCalendarServiceProperty = null;
        private static PropertyInfo _gsBiomeServiceProperty = null;
        private static PropertyInfo _gsStateServiceProperty = null;
        private static PropertyInfo _gsRacesServiceProperty = null;
        private static PropertyInfo _gsVillagersServiceProperty = null;

        // IAltarService methods
        private static MethodInfo _asHasActivePickMethod = null;
        private static MethodInfo _asAreVillagersAllowedMethod = null;
        private static MethodInfo _asSwitchVillagersAllowedMethod = null;
        private static MethodInfo _asSumAllowedMetaValueMethod = null;
        private static MethodInfo _asSumAllowedRacesMethod = null;
        private static MethodInfo _asIsAllowedRaceMethod = null;
        private static MethodInfo _asIsAllowedCurrencyMethod = null;
        private static MethodInfo _asSwitchRaceMethod = null;
        private static MethodInfo _asSwitchCurrencyMethod = null;
        private static MethodInfo _asGetFullMetaPriceForMethod = null;
        private static MethodInfo _asGetVillagersPriceForMethod = null;
        private static MethodInfo _asCanBuyMethod = null;
        private static MethodInfo _asIsUpgradeMethod = null;
        private static MethodInfo _asPickMethod = null;

        // ICalendarService properties
        private static PropertyInfo _csSeasonProperty = null;

        // IBiomeService properties
        private static PropertyInfo _bsBlueprintsProperty = null;

        // BiomeBlueprintsConfig fields
        private static FieldInfo _bbcAltarChargesField = null;

        // IStateService properties
        private static PropertyInfo _ssAltarProperty = null;

        // AltarChargesState fields
        private static FieldInfo _acsLastPickedChargeField = null;
        private static FieldInfo _acsCurrentPickField = null;
        private static FieldInfo _acsAllowedCurrencyField = null;
        private static FieldInfo _acsAllowedRacesField = null;

        // IRacesService properties
        private static PropertyInfo _rsRacesProperty = null;
        private static MethodInfo _rsIsRevealedMethod = null;

        // IVillagersService methods
        private static MethodInfo _vsGetAliveRaceAmountMethod = null;

        // MetaController / MetaServices / MetaEconomyService
        private static PropertyInfo _metaControllerInstanceProperty = null;
        private static PropertyInfo _mcMetaServicesProperty = null;
        private static PropertyInfo _msMetaEconomyServiceProperty = null;
        private static MethodInfo _mesGetAmountMethod = null;

        // Settings access
        private static PropertyInfo _mainControllerSettingsProperty = null;
        private static FieldInfo _settingsMetaCurrenciesField = null;
        private static MethodInfo _settingsGetAltarEffectMethod = null;

        // RaceModel fields
        private static FieldInfo _rmDisplayNameField = null;
        private static PropertyInfo _rmNameProperty = null;

        // MetaCurrencyModel properties/fields
        private static PropertyInfo _mcmDisplayNameProperty = null;
        private static PropertyInfo _mcmNameProperty = null;
        private static FieldInfo _mcmMetaValueRateField = null;

        // AltarEffectModel fields
        private static FieldInfo _aemUpgradedEffectField = null;

        // EffectModel properties
        private static PropertyInfo _emDisplayNameProperty = null;
        private static PropertyInfo _emDescriptionProperty = null;
        private static MethodInfo _emGetTooltipFootnoteMethod = null;

        // Pre-allocated args
        private static readonly object[] _args1 = new object[1];

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] AltarReflection: Game assembly not available");
                    return;
                }

                CacheAltarPanelTypes(assembly);
                CacheServiceTypes(assembly);
                CacheAltarServiceMethods(assembly);
                CacheCalendarTypes(assembly);
                CacheBiomeTypes(assembly);
                CacheStateTypes(assembly);
                CacheRaceTypes(assembly);
                CacheVillagerTypes(assembly);
                CacheMetaEconomyTypes(assembly);
                CacheSettingsTypes(assembly);
                CacheModelTypes(assembly);

                Debug.Log("[ATSAccessibility] AltarReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AltarReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CacheAltarPanelTypes(Assembly assembly)
        {
            _altarPanelType = assembly.GetType("Eremite.Buildings.UI.Altars.AltarPanel");
            if (_altarPanelType != null)
            {
                _altarPanelInstanceProperty = _altarPanelType.GetProperty("Instance", GameReflection.PublicStatic);
            }
        }

        private static void CacheServiceTypes(Assembly assembly)
        {
            var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
            if (gameServicesType != null)
            {
                _gsAltarServiceProperty = gameServicesType.GetProperty("AltarService", GameReflection.PublicInstance);
                _gsCalendarServiceProperty = gameServicesType.GetProperty("CalendarService", GameReflection.PublicInstance);
                _gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService", GameReflection.PublicInstance);
                _gsStateServiceProperty = gameServicesType.GetProperty("StateService", GameReflection.PublicInstance);
                _gsRacesServiceProperty = gameServicesType.GetProperty("RacesService", GameReflection.PublicInstance);
                _gsVillagersServiceProperty = gameServicesType.GetProperty("VillagersService", GameReflection.PublicInstance);
            }
        }

        private static void CacheAltarServiceMethods(Assembly assembly)
        {
            var altarServiceType = assembly.GetType("Eremite.Services.IAltarService");
            if (altarServiceType != null)
            {
                _asHasActivePickMethod = altarServiceType.GetMethod("HasActivePick", GameReflection.PublicInstance);
                _asAreVillagersAllowedMethod = altarServiceType.GetMethod("AreVillagersAllowed", GameReflection.PublicInstance);
                _asSwitchVillagersAllowedMethod = altarServiceType.GetMethod("SwitchVillagersAllowed", GameReflection.PublicInstance);
                _asSumAllowedMetaValueMethod = altarServiceType.GetMethod("SumAllowedMetaValue", GameReflection.PublicInstance);
                _asSumAllowedRacesMethod = altarServiceType.GetMethod("SumAllowedRaces", GameReflection.PublicInstance);

                var raceModelType = assembly.GetType("Eremite.Model.RaceModel");
                var currencyModelType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
                var altarEffectModelType = assembly.GetType("Eremite.Model.AltarEffectModel");

                if (raceModelType != null)
                {
                    _asIsAllowedRaceMethod = altarServiceType.GetMethod("IsAllowed", new[] { raceModelType });
                    _asSwitchRaceMethod = altarServiceType.GetMethod("Switch", new[] { raceModelType });
                }

                if (currencyModelType != null)
                {
                    _asIsAllowedCurrencyMethod = altarServiceType.GetMethod("IsAllowed", new[] { currencyModelType });
                    _asSwitchCurrencyMethod = altarServiceType.GetMethod("Switch", new[] { currencyModelType });
                }

                if (altarEffectModelType != null)
                {
                    _asGetFullMetaPriceForMethod = altarServiceType.GetMethod("GetFullMetaPriceFor", new[] { altarEffectModelType });
                    _asGetVillagersPriceForMethod = altarServiceType.GetMethod("GetVillagersPriceFor", new[] { altarEffectModelType });
                    _asCanBuyMethod = altarServiceType.GetMethod("CanBuy", new[] { altarEffectModelType });
                    _asIsUpgradeMethod = altarServiceType.GetMethod("IsUpgrade", new[] { altarEffectModelType });
                    _asPickMethod = altarServiceType.GetMethod("Pick", new[] { altarEffectModelType });
                }
            }
        }

        private static void CacheCalendarTypes(Assembly assembly)
        {
            var calendarServiceType = assembly.GetType("Eremite.Services.ICalendarService");
            if (calendarServiceType != null)
            {
                _csSeasonProperty = calendarServiceType.GetProperty("Season", GameReflection.PublicInstance);
            }
        }

        private static void CacheBiomeTypes(Assembly assembly)
        {
            var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
            if (biomeServiceType != null)
            {
                _bsBlueprintsProperty = biomeServiceType.GetProperty("Blueprints", GameReflection.PublicInstance);
            }

            var blueprintsConfigType = assembly.GetType("Eremite.Model.Configs.BiomeBlueprintsConfig");
            if (blueprintsConfigType != null)
            {
                _bbcAltarChargesField = blueprintsConfigType.GetField("altarCharges", GameReflection.PublicInstance);
            }
        }

        private static void CacheStateTypes(Assembly assembly)
        {
            var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
            if (stateServiceType != null)
            {
                _ssAltarProperty = stateServiceType.GetProperty("Altar", GameReflection.PublicInstance);
            }

            var altarStateType = assembly.GetType("Eremite.Model.State.AltarChargesState");
            if (altarStateType != null)
            {
                _acsLastPickedChargeField = altarStateType.GetField("lastPickedCharge", GameReflection.PublicInstance);
                _acsCurrentPickField = altarStateType.GetField("currentPick", GameReflection.PublicInstance);
                _acsAllowedCurrencyField = altarStateType.GetField("allowedCurrency", GameReflection.PublicInstance);
                _acsAllowedRacesField = altarStateType.GetField("allowedRaces", GameReflection.PublicInstance);
            }
        }

        private static void CacheRaceTypes(Assembly assembly)
        {
            var racesServiceType = assembly.GetType("Eremite.Services.IRacesService");
            if (racesServiceType != null)
            {
                _rsRacesProperty = racesServiceType.GetProperty("Races", GameReflection.PublicInstance);

                var raceModelType = assembly.GetType("Eremite.Model.RaceModel");
                if (raceModelType != null)
                {
                    _rsIsRevealedMethod = racesServiceType.GetMethod("IsRevealed", new[] { raceModelType });
                }
            }

            var raceModelType2 = assembly.GetType("Eremite.Model.RaceModel");
            if (raceModelType2 != null)
            {
                _rmDisplayNameField = raceModelType2.GetField("displayName", GameReflection.PublicInstance);
                _rmNameProperty = raceModelType2.GetProperty("Name", GameReflection.PublicInstance);
            }
        }

        private static void CacheVillagerTypes(Assembly assembly)
        {
            var villagersServiceType = assembly.GetType("Eremite.Services.IVillagersService");
            if (villagersServiceType != null)
            {
                _vsGetAliveRaceAmountMethod = villagersServiceType.GetMethod("GetAliveRaceAmount", new[] { typeof(string) });
            }
        }

        private static void CacheMetaEconomyTypes(Assembly assembly)
        {
            var metaControllerType = assembly.GetType("Eremite.Controller.MetaController");
            if (metaControllerType != null)
            {
                _metaControllerInstanceProperty = metaControllerType.GetProperty("Instance", GameReflection.PublicStatic);
            }

            var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
            if (metaServicesType != null)
            {
                _msMetaEconomyServiceProperty = metaServicesType.GetProperty("MetaEconomyService", GameReflection.PublicInstance);
            }

            var metaControllerImplType = assembly.GetType("Eremite.Controller.MetaController");
            if (metaControllerImplType != null)
            {
                _mcMetaServicesProperty = metaControllerImplType.GetProperty("MetaServices", GameReflection.PublicInstance);
            }

            var metaEconomyServiceType = assembly.GetType("Eremite.Services.IMetaEconomyService");
            if (metaEconomyServiceType != null)
            {
                _mesGetAmountMethod = metaEconomyServiceType.GetMethod("GetAmount", new[] { typeof(string) });
            }
        }

        private static void CacheSettingsTypes(Assembly assembly)
        {
            var mainControllerType = assembly.GetType("Eremite.Controller.MainController");
            if (mainControllerType != null)
            {
                _mainControllerSettingsProperty = mainControllerType.GetProperty("Settings", GameReflection.PublicInstance);
            }

            var settingsType = assembly.GetType("Eremite.Model.Settings");
            if (settingsType != null)
            {
                _settingsMetaCurrenciesField = settingsType.GetField("metaCurrencies", GameReflection.PublicInstance);
                _settingsGetAltarEffectMethod = settingsType.GetMethod("GetAltarEffect", new[] { typeof(string) });
            }
        }

        private static void CacheModelTypes(Assembly assembly)
        {
            var metaCurrencyType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
            if (metaCurrencyType != null)
            {
                _mcmDisplayNameProperty = metaCurrencyType.GetProperty("DisplayName", GameReflection.PublicInstance);
                _mcmNameProperty = metaCurrencyType.GetProperty("Name", GameReflection.PublicInstance);
                _mcmMetaValueRateField = metaCurrencyType.GetField("metaValueRate", GameReflection.PublicInstance);
            }

            var altarEffectType = assembly.GetType("Eremite.Model.AltarEffectModel");
            if (altarEffectType != null)
            {
                _aemUpgradedEffectField = altarEffectType.GetField("upgradedEffect", GameReflection.PublicInstance);
            }

            var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
            if (effectModelType != null)
            {
                _emDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                _emDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
                _emGetTooltipFootnoteMethod = effectModelType.GetMethod("GetTooltipFootnote", Type.EmptyTypes);
            }
        }

        // ========================================
        // SERVICE ACCESSORS (fresh each call)
        // ========================================

        private static object GetAltarService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsAltarServiceProperty == null) return null;
            try { return _gsAltarServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetCalendarService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsCalendarServiceProperty == null) return null;
            try { return _gsCalendarServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetBiomeService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsBiomeServiceProperty == null) return null;
            try { return _gsBiomeServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetStateService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsStateServiceProperty == null) return null;
            try { return _gsStateServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetAltarState()
        {
            var stateService = GetStateService();
            if (stateService == null || _ssAltarProperty == null) return null;
            try { return _ssAltarProperty.GetValue(stateService); }
            catch { return null; }
        }

        private static object GetRacesService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsRacesServiceProperty == null) return null;
            try { return _gsRacesServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetVillagersService()
        {
            var gameServices = GameReflection.GetGameServices();
            if (gameServices == null || _gsVillagersServiceProperty == null) return null;
            try { return _gsVillagersServiceProperty.GetValue(gameServices); }
            catch { return null; }
        }

        private static object GetMetaEconomyService()
        {
            EnsureCached();
            try
            {
                var metaController = _metaControllerInstanceProperty?.GetValue(null);
                if (metaController == null) return null;

                var metaServices = _mcMetaServicesProperty?.GetValue(metaController);
                if (metaServices == null) return null;

                return _msMetaEconomyServiceProperty?.GetValue(metaServices);
            }
            catch { return null; }
        }

        private static object GetSettings()
        {
            return GameReflection.GetSettings();
        }

        // ========================================
        // PUBLIC API - POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is an AltarPanel.
        /// </summary>
        public static bool IsAltarPanel(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_altarPanelType == null) return false;
            return _altarPanelType.IsInstanceOfType(popup);
        }

        // ========================================
        // PUBLIC API - STATE CHECKS
        // ========================================

        /// <summary>
        /// Check if the altar panel is currently visible.
        /// </summary>
        public static bool IsAltarPanelVisible()
        {
            EnsureCached();
            if (_altarPanelInstanceProperty == null) return false;

            try
            {
                var panel = _altarPanelInstanceProperty.GetValue(null);
                if (panel == null) return false;

                // Check if the panel GameObject is active
                var panelGO = panel as Component;
                return panelGO?.gameObject?.activeInHierarchy ?? false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if the altar is active (Storm season + has active pick).
        /// </summary>
        public static bool IsAltarActive()
        {
            EnsureCached();

            // Check season is Storm (enum value 2)
            if (!IsStormSeason()) return false;

            // Check has active pick
            return HasActivePick();
        }

        /// <summary>
        /// Check if current season is Storm.
        /// </summary>
        public static bool IsStormSeason()
        {
            EnsureCached();
            var calendarService = GetCalendarService();
            if (calendarService == null || _csSeasonProperty == null) return false;

            try
            {
                var seasonObj = _csSeasonProperty.GetValue(calendarService);
                // Season enum: 0=Drizzle, 1=Clearance, 2=Storm
                return seasonObj != null && (int)seasonObj == 2;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if there's an active pick available.
        /// </summary>
        public static bool HasActivePick()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asHasActivePickMethod == null) return false;

            try
            {
                var result = _asHasActivePickMethod.Invoke(altarService, null);
                return result is bool b && b;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if villagers are allowed as payment.
        /// </summary>
        public static bool AreVillagersAllowed()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asAreVillagersAllowedMethod == null) return false;

            try
            {
                var result = _asAreVillagersAllowedMethod.Invoke(altarService, null);
                return result is bool b && b;
            }
            catch { return false; }
        }

        /// <summary>
        /// Get the last picked charge index.
        /// </summary>
        public static int GetLastPickedCharge()
        {
            EnsureCached();
            var altarState = GetAltarState();
            if (altarState == null || _acsLastPickedChargeField == null) return -1;

            try
            {
                var result = _acsLastPickedChargeField.GetValue(altarState);
                return result is int i ? i : -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Get the next charge threshold (reputation needed for next pick).
        /// Returns null if no more charges available.
        /// </summary>
        public static int? GetNextChargeThreshold()
        {
            EnsureCached();

            int lastPicked = GetLastPickedCharge();
            var altarCharges = GetAltarCharges();
            if (altarCharges == null) return null;

            // Find first value > lastPicked
            foreach (int charge in altarCharges)
            {
                if (charge > lastPicked)
                    return charge;
            }

            return null;  // No more charges
        }

        private static int[] GetAltarCharges()
        {
            var biomeService = GetBiomeService();
            if (biomeService == null || _bsBlueprintsProperty == null) return null;

            try
            {
                var blueprints = _bsBlueprintsProperty.GetValue(biomeService);
                if (blueprints == null || _bbcAltarChargesField == null) return null;

                return _bbcAltarChargesField.GetValue(blueprints) as int[];
            }
            catch { return null; }
        }

        // ========================================
        // PUBLIC API - TOTALS
        // ========================================

        /// <summary>
        /// Get total meta value from all allowed currencies.
        /// </summary>
        public static int GetTotalMetaValue()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asSumAllowedMetaValueMethod == null) return 0;

            try
            {
                var result = _asSumAllowedMetaValueMethod.Invoke(altarService, null);
                return result is int i ? i : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Get total villagers from all allowed races.
        /// </summary>
        public static int GetTotalVillagers()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asSumAllowedRacesMethod == null) return 0;

            try
            {
                var result = _asSumAllowedRacesMethod.Invoke(altarService, null);
                return result is int i ? i : 0;
            }
            catch { return 0; }
        }

        // ========================================
        // PUBLIC API - CURRENCIES
        // ========================================

        /// <summary>
        /// Get all meta currencies with their altar state.
        /// </summary>
        public static List<CurrencyInfo> GetCurrencies()
        {
            EnsureCached();
            var result = new List<CurrencyInfo>();

            var settings = GetSettings();
            if (settings == null || _settingsMetaCurrenciesField == null) return result;

            var metaEconomyService = GetMetaEconomyService();
            var altarService = GetAltarService();

            try
            {
                var currenciesArray = _settingsMetaCurrenciesField.GetValue(settings) as Array;
                if (currenciesArray == null) return result;

                foreach (var currency in currenciesArray)
                {
                    if (currency == null) continue;

                    var info = new CurrencyInfo();

                    // Get name
                    var nameObj = _mcmNameProperty?.GetValue(currency);
                    info.Name = nameObj as string ?? "";

                    // Get display name
                    var displayNameObj = _mcmDisplayNameProperty?.GetValue(currency);
                    info.DisplayName = displayNameObj as string ?? info.Name;

                    // Get meta value rate
                    var rateObj = _mcmMetaValueRateField?.GetValue(currency);
                    info.MetaValueRate = rateObj is int r ? r : 1;

                    // Get amount from MetaEconomyService
                    if (metaEconomyService != null && _mesGetAmountMethod != null)
                    {
                        _args1[0] = info.Name;
                        var amountObj = _mesGetAmountMethod.Invoke(metaEconomyService, _args1);
                        info.Amount = amountObj is int a ? a : 0;
                    }

                    // Calculate meta value
                    info.MetaValue = info.Amount * info.MetaValueRate;

                    // Check if enabled
                    if (altarService != null && _asIsAllowedCurrencyMethod != null)
                    {
                        _args1[0] = currency;
                        var enabledObj = _asIsAllowedCurrencyMethod.Invoke(altarService, _args1);
                        info.Enabled = enabledObj is bool b && b;
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.GetCurrencies failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Toggle a currency's enabled state.
        /// </summary>
        public static bool ToggleCurrency(int index)
        {
            EnsureCached();

            var settings = GetSettings();
            var altarService = GetAltarService();
            if (settings == null || altarService == null) return false;
            if (_settingsMetaCurrenciesField == null || _asSwitchCurrencyMethod == null) return false;

            try
            {
                var currenciesArray = _settingsMetaCurrenciesField.GetValue(settings) as Array;
                if (currenciesArray == null || index < 0 || index >= currenciesArray.Length) return false;

                var currency = currenciesArray.GetValue(index);
                if (currency == null) return false;

                _args1[0] = currency;
                _asSwitchCurrencyMethod.Invoke(altarService, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.ToggleCurrency failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PUBLIC API - RACES
        // ========================================

        /// <summary>
        /// Get all races with their altar state.
        /// </summary>
        public static List<RaceInfo> GetRaces()
        {
            EnsureCached();
            var result = new List<RaceInfo>();

            var racesService = GetRacesService();
            var villagersService = GetVillagersService();
            var altarService = GetAltarService();
            if (racesService == null || _rsRacesProperty == null) return result;

            try
            {
                var racesArray = _rsRacesProperty.GetValue(racesService) as Array;
                if (racesArray == null) return result;

                foreach (var race in racesArray)
                {
                    if (race == null) continue;

                    var info = new RaceInfo();

                    // Get name
                    var nameObj = _rmNameProperty?.GetValue(race);
                    info.Name = nameObj as string ?? "";

                    // Get display name from LocaText
                    var locaText = _rmDisplayNameField?.GetValue(race);
                    info.DisplayName = GameReflection.GetLocaText(locaText) ?? info.Name;

                    // Check if revealed
                    if (_rsIsRevealedMethod != null)
                    {
                        _args1[0] = race;
                        var revealedObj = _rsIsRevealedMethod.Invoke(racesService, _args1);
                        info.Revealed = revealedObj is bool b && b;
                    }

                    // Only include revealed races
                    if (!info.Revealed) continue;

                    // Get count from VillagersService
                    if (villagersService != null && _vsGetAliveRaceAmountMethod != null)
                    {
                        _args1[0] = info.Name;
                        var countObj = _vsGetAliveRaceAmountMethod.Invoke(villagersService, _args1);
                        info.Count = countObj is int c ? c : 0;
                    }

                    // Check if enabled
                    if (altarService != null && _asIsAllowedRaceMethod != null)
                    {
                        _args1[0] = race;
                        var enabledObj = _asIsAllowedRaceMethod.Invoke(altarService, _args1);
                        info.Enabled = enabledObj is bool b && b;
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.GetRaces failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Toggle a race's enabled state by index.
        /// </summary>
        public static bool ToggleRace(int index)
        {
            EnsureCached();

            var racesService = GetRacesService();
            var altarService = GetAltarService();
            if (racesService == null || altarService == null) return false;
            if (_rsRacesProperty == null || _asSwitchRaceMethod == null) return false;

            try
            {
                var racesArray = _rsRacesProperty.GetValue(racesService) as Array;
                if (racesArray == null) return false;

                // Find revealed race at index
                int revealedIndex = 0;
                foreach (var race in racesArray)
                {
                    if (race == null) continue;

                    // Check if revealed
                    bool revealed = false;
                    if (_rsIsRevealedMethod != null)
                    {
                        _args1[0] = race;
                        var revealedObj = _rsIsRevealedMethod.Invoke(racesService, _args1);
                        revealed = revealedObj is bool b && b;
                    }

                    if (!revealed) continue;

                    if (revealedIndex == index)
                    {
                        _args1[0] = race;
                        _asSwitchRaceMethod.Invoke(altarService, _args1);
                        return true;
                    }

                    revealedIndex++;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.ToggleRace failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Toggle the master villagers allowed setting.
        /// </summary>
        public static bool ToggleVillagersAllowed()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asSwitchVillagersAllowedMethod == null) return false;

            try
            {
                _asSwitchVillagersAllowedMethod.Invoke(altarService, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.ToggleVillagersAllowed failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // PUBLIC API - CORNERSTONES (EFFECTS)
        // ========================================

        /// <summary>
        /// Get the current cornerstone pick options.
        /// </summary>
        public static List<EffectInfo> GetCurrentPick()
        {
            EnsureCached();
            var result = new List<EffectInfo>();

            var altarState = GetAltarState();
            var altarService = GetAltarService();
            var settings = GetSettings();
            if (altarState == null || _acsCurrentPickField == null) return result;

            try
            {
                var currentPick = _acsCurrentPickField.GetValue(altarState) as List<string>;
                if (currentPick == null || currentPick.Count == 0) return result;

                foreach (var effectName in currentPick)
                {
                    if (string.IsNullOrEmpty(effectName)) continue;

                    // Get AltarEffectModel from settings
                    if (_settingsGetAltarEffectMethod == null) continue;

                    _args1[0] = effectName;
                    var altarEffect = _settingsGetAltarEffectMethod.Invoke(settings, _args1);
                    if (altarEffect == null) continue;

                    var info = new EffectInfo { Model = altarEffect };

                    // Get upgraded effect
                    var upgradedEffect = _aemUpgradedEffectField?.GetValue(altarEffect);
                    if (upgradedEffect != null)
                    {
                        // Get display name
                        info.DisplayName = _emDisplayNameProperty?.GetValue(upgradedEffect) as string ?? effectName;

                        // Get description
                        var desc = _emDescriptionProperty?.GetValue(upgradedEffect) as string ?? "";

                        // Get footnote if any
                        if (_emGetTooltipFootnoteMethod != null)
                        {
                            var footnote = _emGetTooltipFootnoteMethod.Invoke(upgradedEffect, null);
                            var footnoteStr = footnote?.ToString();
                            if (!string.IsNullOrEmpty(footnoteStr) && footnoteStr != "None")
                            {
                                desc += $" ({footnoteStr})";
                            }
                        }

                        info.Description = desc;
                    }

                    // Ensure DisplayName is never null
                    if (string.IsNullOrEmpty(info.DisplayName))
                    {
                        info.DisplayName = effectName;
                    }

                    // Get prices and affordability from AltarService
                    if (altarService != null)
                    {
                        _args1[0] = altarEffect;

                        if (_asGetFullMetaPriceForMethod != null)
                        {
                            var priceObj = _asGetFullMetaPriceForMethod.Invoke(altarService, _args1);
                            info.MetaPrice = priceObj is int p ? p : 0;
                        }

                        if (_asGetVillagersPriceForMethod != null)
                        {
                            var vPriceObj = _asGetVillagersPriceForMethod.Invoke(altarService, _args1);
                            info.VillagersPrice = vPriceObj is int vp ? vp : 0;
                        }

                        if (_asCanBuyMethod != null)
                        {
                            var canBuyObj = _asCanBuyMethod.Invoke(altarService, _args1);
                            info.CanAfford = canBuyObj is bool cb && cb;
                        }

                        if (_asIsUpgradeMethod != null)
                        {
                            var isUpgradeObj = _asIsUpgradeMethod.Invoke(altarService, _args1);
                            info.IsUpgrade = isUpgradeObj is bool iu && iu;
                        }
                    }

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.GetCurrentPick failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Pick an effect (purchase a cornerstone).
        /// </summary>
        public static bool PickEffect(object altarEffectModel)
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asPickMethod == null) return false;

            try
            {
                _args1[0] = altarEffectModel;
                _asPickMethod.Invoke(altarService, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.PickEffect failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Skip the current pick.
        /// </summary>
        public static bool Skip()
        {
            EnsureCached();
            var altarService = GetAltarService();
            if (altarService == null || _asPickMethod == null) return false;

            try
            {
                _args1[0] = null;  // null = skip
                _asPickMethod.Invoke(altarService, _args1);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] AltarReflection.Skip failed: {ex.Message}");
                return false;
            }
        }
    }
}
