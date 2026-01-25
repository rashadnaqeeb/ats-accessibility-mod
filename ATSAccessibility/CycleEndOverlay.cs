using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Overlay for WorldCycleEndPopup (shown when ending a Blightstorm cycle on world map).
    /// Provides navigation through XP summary and unlocked capital upgrades.
    /// </summary>
    public class CycleEndOverlay : IKeyHandler
    {
        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<string> _items = new List<string>();

        // Cached reflection
        private static bool _typesCached;

        // MetaStateService access: MetaController.Instance.MetaServices.MetaStateService
        private static PropertyInfo _msMetaStateServiceProperty;

        // MetaStateService properties
        private static PropertyInfo _mssEconomyProperty;      // MetaStateService.Economy
        private static PropertyInfo _mssLevelProperty;        // MetaStateService.Level
        private static PropertyInfo _mssCapitalProperty;      // MetaStateService.Capital

        // MetaEconomyState.currentCycleExp field
        private static FieldInfo _economyCurrentCycleExpField;

        // LevelState fields
        private static FieldInfo _levelLevelField;
        private static FieldInfo _levelExpField;
        private static FieldInfo _levelTargetExpField;

        // CapitalState.currentCycleUnlockedUpgrades field (HashSet<string>)
        private static FieldInfo _capitalCurrentCycleUpgradesField;

        // Settings.GetCapitalUpgrade(string) method
        private static MethodInfo _settingsGetCapitalUpgradeMethod;

        // CapitalUpgradeModel.displayName field
        private static FieldInfo _upgradeDisplayNameField;
        private static FieldInfo _upgradeIronmanDisplayNameField;

        // MetaStateService.State.isIronman for ironman check
        private static PropertyInfo _mssStateProperty;
        private static FieldInfo _stateIsIronmanField;

        // WorldBlackboardService.OnCycleEndPhase subject for confirm
        private static PropertyInfo _wbbOnCycleEndPhaseProperty;

        // CycleEndPhase.AnimationRequested enum value
        private static object _animationRequestedValue;

        // Popup.Hide() method
        private static MethodInfo _popupHideMethod;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    ConfirmCycleEnd();
                    return true;

                case KeyCode.Escape:
                    CancelAndClose();
                    return true;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;

            EnsureTypes();
            RefreshData();

            string announcement = "End Cycle";
            if (_items.Count > 0)
            {
                announcement += $". {_items[0]}";
            }

            Speech.Say(announcement);
            Debug.Log($"[ATSAccessibility] CycleEndOverlay opened, {_items.Count} items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] CycleEndOverlay closed");
        }

        // ========================================
        // DETECTION
        // ========================================

        public static bool IsWorldCycleEndPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "WorldCycleEndPopup";
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            Speech.Say(_items[_currentIndex]);
        }

        private void ConfirmCycleEnd()
        {
            if (_popup == null) return;

            EnsureTypes();

            // Trigger OnCycleEndPhase.OnNext(CycleEndPhase.AnimationRequested)
            var wbb = WorldMapReflection.GetWorldBlackboardService();
            if (wbb != null && _wbbOnCycleEndPhaseProperty != null && _animationRequestedValue != null)
            {
                try
                {
                    var subject = _wbbOnCycleEndPhaseProperty.GetValue(wbb);
                    if (subject != null)
                    {
                        var onNextMethod = subject.GetType().GetMethod("OnNext",
                            BindingFlags.Public | BindingFlags.Instance);
                        onNextMethod?.Invoke(subject, new[] { _animationRequestedValue });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] CycleEndOverlay: Failed to trigger cycle end: {ex.Message}");
                }
            }

            // Hide the popup
            if (_popupHideMethod != null)
            {
                try
                {
                    _popupHideMethod.Invoke(_popup, null);
                    SoundManager.PlayButtonClick();
                    Speech.Say("Confirmed");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] CycleEndOverlay: Failed to hide popup: {ex.Message}");
                }
            }
        }

        private void CancelAndClose()
        {
            if (_popup == null) return;

            EnsureTypes();

            // Hide the popup without triggering cycle end
            if (_popupHideMethod != null)
            {
                try
                {
                    _popupHideMethod.Invoke(_popup, null);
                    Speech.Say("Cancelled");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] CycleEndOverlay: Failed to close popup: {ex.Message}");
                }
            }
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            // Get XP summary
            string xpSummary = GetXpSummary();
            if (!string.IsNullOrEmpty(xpSummary))
            {
                _items.Add(xpSummary);
            }

            // Get unlocked upgrades
            var upgradeNames = GetUnlockedUpgradeNames();
            foreach (var name in upgradeNames)
            {
                _items.Add(name);
            }

            Debug.Log($"[ATSAccessibility] CycleEndOverlay: {_items.Count} items refreshed");
        }

        private string GetXpSummary()
        {
            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null) return "Experience summary unavailable";

                // Get Economy.currentCycleExp
                var economy = _mssEconomyProperty?.GetValue(metaStateService);
                int currentCycleExp = 0;
                if (economy != null && _economyCurrentCycleExpField != null)
                {
                    var expObj = _economyCurrentCycleExpField.GetValue(economy);
                    if (expObj is int exp) currentCycleExp = exp;
                }

                // Get Level state
                var level = _mssLevelProperty?.GetValue(metaStateService);
                int currentLevel = 0;
                int currentExp = 0;
                int targetExp = 0;
                if (level != null)
                {
                    var levelObj = _levelLevelField?.GetValue(level);
                    if (levelObj is int l) currentLevel = l;

                    var expObj = _levelExpField?.GetValue(level);
                    if (expObj is int e) currentExp = e;

                    var targetObj = _levelTargetExpField?.GetValue(level);
                    if (targetObj is int t) targetExp = t;
                }

                // Handle max level case (targetExp == 0 or currentExp >= targetExp)
                if (targetExp <= 0 || currentExp >= targetExp)
                {
                    return $"Gained {currentCycleExp} experience, Level {currentLevel}, max level";
                }

                return $"Gained {currentCycleExp} experience, Level {currentLevel}, {currentExp} of {targetExp} to next level";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CycleEndOverlay: GetXpSummary failed: {ex.Message}");
                return "Experience summary unavailable";
            }
        }

        private List<string> GetUnlockedUpgradeNames()
        {
            var names = new List<string>();

            try
            {
                var metaStateService = GetMetaStateService();
                if (metaStateService == null) return names;

                // Get Capital.currentCycleUnlockedUpgrades
                var capital = _mssCapitalProperty?.GetValue(metaStateService);
                if (capital == null || _capitalCurrentCycleUpgradesField == null) return names;

                var upgradesObj = _capitalCurrentCycleUpgradesField.GetValue(capital);
                if (upgradesObj == null) return names;

                // Check if ironman mode for display name selection
                bool isIronman = IsIronmanMode(metaStateService);

                // Iterate HashSet<string> using IEnumerable
                var enumerable = upgradesObj as IEnumerable;
                if (enumerable == null) return names;

                var settings = GameReflection.GetSettings();
                if (settings == null || _settingsGetCapitalUpgradeMethod == null) return names;

                foreach (var upgradeId in enumerable)
                {
                    if (upgradeId == null) continue;

                    string id = upgradeId.ToString();
                    string displayName = GetUpgradeDisplayName(settings, id, isIronman);
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        names.Add(displayName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CycleEndOverlay: GetUnlockedUpgradeNames failed: {ex.Message}");
            }

            return names;
        }

        private string GetUpgradeDisplayName(object settings, string upgradeId, bool isIronman)
        {
            try
            {
                var upgrade = _settingsGetCapitalUpgradeMethod?.Invoke(settings, new object[] { upgradeId });
                if (upgrade == null) return upgradeId;

                string name = null;

                // Try ironman display name first if in ironman mode
                if (isIronman && _upgradeIronmanDisplayNameField != null)
                {
                    var ironmanLocaText = _upgradeIronmanDisplayNameField.GetValue(upgrade);
                    name = GameReflection.GetLocaText(ironmanLocaText);
                }

                // Fall back to regular display name if ironman name is empty
                if (string.IsNullOrEmpty(name) && _upgradeDisplayNameField != null)
                {
                    var locaText = _upgradeDisplayNameField.GetValue(upgrade);
                    name = GameReflection.GetLocaText(locaText);
                }

                return !string.IsNullOrEmpty(name) ? name : upgradeId;
            }
            catch
            {
                return upgradeId;
            }
        }

        private bool IsIronmanMode(object metaStateService)
        {
            try
            {
                if (_mssStateProperty == null || _stateIsIronmanField == null) return false;

                var state = _mssStateProperty.GetValue(metaStateService);
                if (state == null) return false;

                var isIronmanObj = _stateIsIronmanField.GetValue(state);
                return isIronmanObj is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private object GetMetaStateService()
        {
            try
            {
                var metaServices = GameReflection.GetMetaServices();
                if (metaServices == null || _msMetaStateServiceProperty == null) return null;

                return _msMetaStateServiceProperty.GetValue(metaServices);
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // REFLECTION CACHING
        // ========================================

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null) return;

            try
            {
                // Cache MetaStateService access
                var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
                if (metaServicesType != null)
                {
                    _msMetaStateServiceProperty = metaServicesType.GetProperty("MetaStateService",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaStateService properties
                var metaStateServiceType = assembly.GetType("Eremite.Services.IMetaStateService");
                if (metaStateServiceType != null)
                {
                    _mssEconomyProperty = metaStateServiceType.GetProperty("Economy",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mssLevelProperty = metaStateServiceType.GetProperty("Level",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mssCapitalProperty = metaStateServiceType.GetProperty("Capital",
                        BindingFlags.Public | BindingFlags.Instance);
                    _mssStateProperty = metaStateServiceType.GetProperty("State",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaEconomyState.currentCycleExp
                var economyStateType = assembly.GetType("Eremite.Model.State.MetaEconomyState");
                if (economyStateType != null)
                {
                    _economyCurrentCycleExpField = economyStateType.GetField("currentCycleExp",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache LevelState fields
                var levelStateType = assembly.GetType("Eremite.Model.State.LevelState");
                if (levelStateType != null)
                {
                    _levelLevelField = levelStateType.GetField("level",
                        BindingFlags.Public | BindingFlags.Instance);
                    _levelExpField = levelStateType.GetField("exp",
                        BindingFlags.Public | BindingFlags.Instance);
                    _levelTargetExpField = levelStateType.GetField("targetExp",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache CapitalState.currentCycleUnlockedUpgrades
                var capitalStateType = assembly.GetType("Eremite.WorldMap.CapitalState");
                if (capitalStateType != null)
                {
                    _capitalCurrentCycleUpgradesField = capitalStateType.GetField("currentCycleUnlockedUpgrades",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache MetaState.isIronman
                var metaStateType = assembly.GetType("Eremite.Model.State.MetaState");
                if (metaStateType != null)
                {
                    _stateIsIronmanField = metaStateType.GetField("isIronman",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache Settings.GetCapitalUpgrade
                var settingsType = assembly.GetType("Eremite.Model.Settings");
                if (settingsType != null)
                {
                    _settingsGetCapitalUpgradeMethod = settingsType.GetMethod("GetCapitalUpgrade",
                        BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                }

                // Cache CapitalUpgradeModel.displayName
                var upgradeModelType = assembly.GetType("Eremite.WorldMap.CapitalUpgradeModel");
                if (upgradeModelType != null)
                {
                    _upgradeDisplayNameField = upgradeModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _upgradeIronmanDisplayNameField = upgradeModelType.GetField("ironmanDisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache WorldBlackboardService.OnCycleEndPhase
                var wbbType = assembly.GetType("Eremite.Services.World.IWorldBlackboardService");
                if (wbbType != null)
                {
                    _wbbOnCycleEndPhaseProperty = wbbType.GetProperty("OnCycleEndPhase",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache CycleEndPhase.AnimationRequested enum value
                var cycleEndPhaseType = assembly.GetType("Eremite.CycleEndPhase");
                if (cycleEndPhaseType != null)
                {
                    _animationRequestedValue = Enum.Parse(cycleEndPhaseType, "AnimationRequested");
                }

                // Cache Popup.Hide method
                var popupType = assembly.GetType("Eremite.View.Popups.Popup");
                if (popupType != null)
                {
                    _popupHideMethod = popupType.GetMethod("Hide",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] CycleEndOverlay: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CycleEndOverlay: Type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }
    }
}
