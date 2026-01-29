using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to Custom Games (Training Expeditions) popup internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// - All public methods return fresh values by querying through cached PropertyInfo
    /// </summary>
    public static class CustomGamesReflection
    {
        // ========================================
        // TYPE CACHE
        // ========================================
        private static Type _customGamePopupType;
        private static Type _difficultyPickerType;
        private static Type _difficultyModelType;
        private static Type _floatOptionsSliderPanelType;
        private static Type _toggleButtonType;
        private static Type _modifierDataType;
        private static Type _raceModelType;
        private static Type _biomeModelType;
        private static Type _goodStructType;
        private static Type _effectModelType;

        // CustomGamePopup fields
        private static FieldInfo _embarkButtonField;
        private static FieldInfo _difficultyPickerField;
        private static FieldInfo _reputationPanelField;
        private static FieldInfo _seasonsPanelField;
        private static FieldInfo _seedPanelField;
        private static FieldInfo _biomePanelField;
        private static FieldInfo _racesPanelField;
        private static FieldInfo _seasonalEffectsPanelField;
        private static FieldInfo _blightPanelField;
        private static FieldInfo _modifiersPanelField;
        private static FieldInfo _tradeTownsPanelField;
        private static FieldInfo _goodsPanelField;
        private static FieldInfo _effectsPanelField;
        private static FieldInfo _layoutsPopupField;

        // DifficultyPicker
        private static FieldInfo _pickerDropdownField;
        private static FieldInfo _pickerDifficultyField;
        private static MethodInfo _pickerGetDifficultiesMethod;
        private static MethodInfo _pickerGetPickedDifficultyMethod;
        private static MethodInfo _pickerSetDifficultyMethod;

        // DifficultyModel
        private static MethodInfo _dmGetDisplayNameMethod;
        private static FieldInfo _dmIndexField;

        // FloatOptionsSliderPanel
        private static FieldInfo _sliderOptionsField;
        private static FieldInfo _sliderCurrentIndexField;
        private static MethodInfo _sliderGetPickedIndexMethod;
        private static MethodInfo _sliderSetIndexMethod;

        // FloatOption
        private static FieldInfo _floatOptionLabelField;
        private static FieldInfo _floatOptionAmountField;

        // ToggleButton
        private static MethodInfo _toggleIsOnMethod;

        // SeedPanel
        private static FieldInfo _seedInputField;
        private static FieldInfo _seedButtonField;

        // BiomePanel
        private static FieldInfo _biomeDropdownField;
        private static FieldInfo _biomeBiomesField;

        // BiomeModel
        private static FieldInfo _bmDisplayNameField;  // LocaText field
        private static PropertyInfo _bmNameProperty;

        // RacesPanel
        private static FieldInfo _racesSlotsField;
        private static FieldInfo _racesPickedField;

        // RaceModel
        private static FieldInfo _rmDisplayNameField;  // LocaText field
        private static PropertyInfo _rmNameProperty;

        // ReputationPanel
        private static FieldInfo _repReputationSliderField;
        private static FieldInfo _repPenaltySliderField;
        private static FieldInfo _repPenaltyRateSliderField;

        // SeasonsDurationPanel
        private static FieldInfo _seasonsDrizzleField;
        private static FieldInfo _seasonsClearanceField;
        private static FieldInfo _seasonsStormField;

        // SeasonalEffectsPanel
        private static FieldInfo _seasonalRandomButtonField;
        private static FieldInfo _seasonalPositiveSliderField;
        private static FieldInfo _seasonalNegativeSliderField;
        private static FieldInfo _seasonalPickedField;

        // BlightPanel
        private static FieldInfo _blightToggleField;
        private static FieldInfo _blightFootprintField;
        private static FieldInfo _blightCorruptionField;

        // ModifiersPanel
        private static FieldInfo _modAllModifiersField;
        private static FieldInfo _modCurrentModifiersField;
        private static FieldInfo _modSlotsField;
        private static FieldInfo _modCategorySlotsField;

        // ModifierData
        private static FieldInfo _mdModelField;
        private static FieldInfo _mdEffectField;
        private static FieldInfo _mdIsPositiveField;
        private static FieldInfo _mdIsPickedField;
        private static FieldInfo _mdTypeField;

        // ModifierSlot
        private static MethodInfo _modSlotGetModifierMethod;
        private static FieldInfo _modSlotToggleField;

        // ModifiersCategorySlot
        private static MethodInfo _catSlotIsOnMethod;
        private static MethodInfo _catSlotGetModifierTypeMethod;

        // TradeTownsPanel
        private static FieldInfo _tradeTownsSlotsField;
        private static FieldInfo _tradeTownsAllField;
        private static FieldInfo _tradeTownsPickedField;

        // CustomGameTradeTownSlot
        private static FieldInfo _tradeTownSlotTownField;
        private static FieldInfo _tradeTownSlotToggleField;
        private static FieldInfo _tradeTownSlotLabelField;

        // CustomGameTradeTownData
        private static FieldInfo _tradeTownDataFieldField;
        private static FieldInfo _tradeTownDataFactionField;

        // GoodsPanel
        private static FieldInfo _goodsSlotsField;

        // GoodSlot
        private static MethodInfo _goodSlotGetGoodMethod;
        private static FieldInfo _goodSlotPlusButtonField;
        private static FieldInfo _goodSlotMinusButtonField;

        // Good struct
        private static FieldInfo _goodNameField;
        private static FieldInfo _goodAmountField;

        // EffectsPanel
        private static FieldInfo _effectsSlotsField;
        private static FieldInfo _effectsPickedField;
        private static FieldInfo _effectsAllField;

        // EffectModel
        private static PropertyInfo _emDisplayNameProperty;
        private static PropertyInfo _emNameProperty;

        // LayoutsPopup
        private static FieldInfo _layoutsSlotsField;
        private static FieldInfo _layoutsIsSaveField;

        // TMP_Text.text
        private static PropertyInfo _tmpTextProperty;

        // TMP_Dropdown.value
        private static PropertyInfo _tmpDropdownValueProperty;
        private static PropertyInfo _tmpDropdownOptionsProperty;

        // TMP_InputField.text
        private static PropertyInfo _tmpInputFieldTextProperty;

        // Slider.value
        private static PropertyInfo _sliderValueProperty;

        // Button.onClick
        private static PropertyInfo _buttonOnClickProperty;
        private static MethodInfo _unityEventInvokeMethod;

        private static bool _typesCached = false;

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _typesCached = true;
                return;
            }

            try
            {
                // Cache CustomGamePopup type
                _customGamePopupType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGamePopup");
                if (_customGamePopupType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _embarkButtonField = _customGamePopupType.GetField("embarkButton", flags);
                    _difficultyPickerField = _customGamePopupType.GetField("difficultyPicker", flags);
                    _reputationPanelField = _customGamePopupType.GetField("reputationPanel", flags);
                    _seasonsPanelField = _customGamePopupType.GetField("seasonsPanel", flags);
                    _seedPanelField = _customGamePopupType.GetField("seedPanel", flags);
                    _biomePanelField = _customGamePopupType.GetField("biomePanel", flags);
                    _racesPanelField = _customGamePopupType.GetField("racesPanel", flags);
                    _seasonalEffectsPanelField = _customGamePopupType.GetField("seasonalEffectsPanel", flags);
                    _blightPanelField = _customGamePopupType.GetField("blightPanel", flags);
                    _modifiersPanelField = _customGamePopupType.GetField("modifiersPanel", flags);
                    _tradeTownsPanelField = _customGamePopupType.GetField("tradeTownsPanel", flags);
                    _goodsPanelField = _customGamePopupType.GetField("goodsPanel", flags);
                    _effectsPanelField = _customGamePopupType.GetField("effectsPanel", flags);
                    _layoutsPopupField = _customGamePopupType.GetField("layoutsPopup", flags);
                }

                // Cache DifficultyPicker types
                _difficultyPickerType = assembly.GetType("Eremite.WorldMap.UI.DifficultyPicker");
                if (_difficultyPickerType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _pickerDropdownField = _difficultyPickerType.GetField("dropdown", flags);
                    _pickerDifficultyField = _difficultyPickerType.GetField("difficulty", flags);
                    _pickerGetDifficultiesMethod = _difficultyPickerType.GetMethod("GetDifficulties",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    _pickerGetPickedDifficultyMethod = _difficultyPickerType.GetMethod("GetPickedDifficulty",
                        BindingFlags.Public | BindingFlags.Instance);
                    _pickerSetDifficultyMethod = _difficultyPickerType.GetMethod("SetDifficulty",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                }

                // Cache DifficultyModel
                _difficultyModelType = assembly.GetType("Eremite.Model.DifficultyModel");
                if (_difficultyModelType != null)
                {
                    _dmGetDisplayNameMethod = _difficultyModelType.GetMethod("GetDisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _dmIndexField = _difficultyModelType.GetField("index",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache FloatOptionsSliderPanel
                _floatOptionsSliderPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.FloatOptionsSliderPanel");
                if (_floatOptionsSliderPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _sliderOptionsField = _floatOptionsSliderPanelType.GetField("options", flags);
                    _sliderCurrentIndexField = _floatOptionsSliderPanelType.GetField("currentIndex", flags);
                    _sliderGetPickedIndexMethod = _floatOptionsSliderPanelType.GetMethod("GetPickedIndex",
                        BindingFlags.Public | BindingFlags.Instance);
                    _sliderSetIndexMethod = _floatOptionsSliderPanelType.GetMethod("SetIndex",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache FloatOption fields
                var floatOptionType = assembly.GetType("Eremite.Model.Configs.CustomGame.FloatOption");
                if (floatOptionType != null)
                {
                    _floatOptionLabelField = floatOptionType.GetField("label",
                        BindingFlags.Public | BindingFlags.Instance);
                    _floatOptionAmountField = floatOptionType.GetField("amount",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache ToggleButton
                _toggleButtonType = assembly.GetType("Eremite.View.ToggleButton");
                if (_toggleButtonType != null)
                {
                    _toggleIsOnMethod = _toggleButtonType.GetMethod("IsOn",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache SeedPanel
                var seedPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameSeedPanel");
                if (seedPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _seedInputField = seedPanelType.GetField("input", flags);
                    _seedButtonField = seedPanelType.GetField("button", flags);
                }

                // Cache BiomePanel
                var biomePanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameBiomePanel");
                if (biomePanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _biomeDropdownField = biomePanelType.GetField("dropdown", flags);
                    _biomeBiomesField = biomePanelType.GetField("biomes", flags);
                }

                // Cache BiomeModel (note: it's in Eremite.WorldMap namespace)
                _biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
                if (_biomeModelType != null)
                {
                    _bmDisplayNameField = _biomeModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _bmNameProperty = _biomeModelType.GetProperty("Name",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache RacesPanel
                var racesPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameRacesPanel");
                if (racesPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _racesSlotsField = racesPanelType.GetField("slots", flags);
                    _racesPickedField = racesPanelType.GetField("picked", flags);
                }

                // Cache RaceModel
                _raceModelType = assembly.GetType("Eremite.Model.RaceModel");
                if (_raceModelType != null)
                {
                    _rmDisplayNameField = _raceModelType.GetField("displayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _rmNameProperty = _raceModelType.GetProperty("Name",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache ReputationPanel
                var repPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameReputationPanel");
                if (repPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _repReputationSliderField = repPanelType.GetField("reputation", flags);
                    _repPenaltySliderField = repPanelType.GetField("penalty", flags);
                    _repPenaltyRateSliderField = repPanelType.GetField("penaltyRate", flags);
                }

                // Cache SeasonsDurationPanel
                var seasonsPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameSeasonsDurationPanel");
                if (seasonsPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _seasonsDrizzleField = seasonsPanelType.GetField("drizzle", flags);
                    _seasonsClearanceField = seasonsPanelType.GetField("clearance", flags);
                    _seasonsStormField = seasonsPanelType.GetField("storm", flags);
                }

                // Cache SeasonalEffectsPanel
                var seasonalPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameSeasonalEffectsPanel");
                if (seasonalPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _seasonalRandomButtonField = seasonalPanelType.GetField("randomButton", flags);
                    _seasonalPositiveSliderField = seasonalPanelType.GetField("positiveSlider", flags);
                    _seasonalNegativeSliderField = seasonalPanelType.GetField("negativeSlider", flags);
                    _seasonalPickedField = seasonalPanelType.GetField("picked", flags);
                }

                // Cache BlightPanel
                var blightPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameBlightPanel");
                if (blightPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _blightToggleField = blightPanelType.GetField("toggle", flags);
                    _blightFootprintField = blightPanelType.GetField("footprint", flags);
                    _blightCorruptionField = blightPanelType.GetField("corruption", flags);
                }

                // Cache ModifiersPanel
                var modPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameModifiersPanel");
                if (modPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _modAllModifiersField = modPanelType.GetField("allModifiers", flags);
                    _modCurrentModifiersField = modPanelType.GetField("currentModifiers", flags);
                    _modSlotsField = modPanelType.GetField("slots", flags);
                    _modCategorySlotsField = modPanelType.GetField("categorySlots", flags);
                }

                // Cache ModifierData
                _modifierDataType = assembly.GetType("Eremite.WorldMap.ConditionsCreator.ModifierData");
                if (_modifierDataType != null)
                {
                    var flags = BindingFlags.Public | BindingFlags.Instance;
                    _mdModelField = _modifierDataType.GetField("model", flags);
                    _mdEffectField = _modifierDataType.GetField("effect", flags);
                    _mdIsPositiveField = _modifierDataType.GetField("isPositive", flags);
                    _mdIsPickedField = _modifierDataType.GetField("isPicked", flags);
                    _mdTypeField = _modifierDataType.GetField("type", flags);
                }

                // Cache ModifierSlot
                var modSlotType = assembly.GetType("Eremite.WorldMap.UI.ModifierSlot");
                if (modSlotType != null)
                {
                    _modSlotGetModifierMethod = modSlotType.GetMethod("GetModifier",
                        BindingFlags.Public | BindingFlags.Instance);
                    _modSlotToggleField = modSlotType.GetField("toggle",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                // Cache ModifiersCategorySlot
                var catSlotType = assembly.GetType("Eremite.WorldMap.UI.ModifiersCategorySlot");
                if (catSlotType != null)
                {
                    _catSlotIsOnMethod = catSlotType.GetMethod("IsOn",
                        BindingFlags.Public | BindingFlags.Instance);
                    _catSlotGetModifierTypeMethod = catSlotType.GetMethod("GetModifierType",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache TradeTownsPanel
                var tradeTownsPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameTradeTownsPanel");
                if (tradeTownsPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _tradeTownsSlotsField = tradeTownsPanelType.GetField("slots", flags);
                    _tradeTownsAllField = tradeTownsPanelType.GetField("all", flags);
                    _tradeTownsPickedField = tradeTownsPanelType.GetField("picked", flags);
                }

                // Cache CustomGameTradeTownSlot
                var tradeTownSlotType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameTradeTownSlot");
                if (tradeTownSlotType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _tradeTownSlotTownField = tradeTownSlotType.GetField("town", flags);
                    _tradeTownSlotToggleField = tradeTownSlotType.GetField("toggle", flags);
                    _tradeTownSlotLabelField = tradeTownSlotType.GetField("label", flags);
                }

                // Cache CustomGameTradeTownData
                var tradeTownDataType = assembly.GetType("Eremite.Model.State.CustomGames.CustomGameTradeTownData");
                if (tradeTownDataType != null)
                {
                    var flags = BindingFlags.Public | BindingFlags.Instance;
                    _tradeTownDataFieldField = tradeTownDataType.GetField("field", flags);
                    _tradeTownDataFactionField = tradeTownDataType.GetField("faction", flags);
                }

                // Cache GoodsPanel
                var goodsPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameEmbarkGoodsPanel");
                if (goodsPanelType != null)
                {
                    _goodsSlotsField = goodsPanelType.GetField("slots",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                // Cache GoodSlot
                var goodSlotType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameEmbarkGoodSlot");
                if (goodSlotType != null)
                {
                    _goodSlotGetGoodMethod = goodSlotType.GetMethod("GetGood",
                        BindingFlags.Public | BindingFlags.Instance);
                    _goodSlotPlusButtonField = goodSlotType.GetField("plusButton",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    _goodSlotMinusButtonField = goodSlotType.GetField("minusButton",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                // Cache Good struct
                _goodStructType = assembly.GetType("Eremite.Model.Good");
                if (_goodStructType != null)
                {
                    var flags = BindingFlags.Public | BindingFlags.Instance;
                    _goodNameField = _goodStructType.GetField("name", flags);
                    _goodAmountField = _goodStructType.GetField("amount", flags);
                }

                // Cache EffectsPanel
                var effectsPanelType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameEmbarkEffectsPanel");
                if (effectsPanelType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _effectsSlotsField = effectsPanelType.GetField("slots", flags);
                    _effectsPickedField = effectsPanelType.GetField("picked", flags);
                    _effectsAllField = effectsPanelType.GetField("effects", flags);
                }

                // Cache EffectModel
                _effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (_effectModelType != null)
                {
                    _emDisplayNameProperty = _effectModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _emNameProperty = _effectModelType.GetProperty("Name",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache LayoutsPopup
                var layoutsPopupType = assembly.GetType("Eremite.WorldMap.UI.CustomGames.CustomGameLayoutsPopup");
                if (layoutsPopupType != null)
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    _layoutsSlotsField = layoutsPopupType.GetField("slots", flags);
                    _layoutsIsSaveField = layoutsPopupType.GetField("isSave", flags);
                }

                // Cache TMP_Text.text
                var tmpTextType = typeof(TMPro.TMP_Text);
                _tmpTextProperty = tmpTextType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);

                // Cache TMP_Dropdown
                var tmpDropdownType = typeof(TMPro.TMP_Dropdown);
                _tmpDropdownValueProperty = tmpDropdownType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
                _tmpDropdownOptionsProperty = tmpDropdownType.GetProperty("options", BindingFlags.Public | BindingFlags.Instance);

                // Cache TMP_InputField
                var tmpInputType = typeof(TMPro.TMP_InputField);
                _tmpInputFieldTextProperty = tmpInputType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);

                // Cache Slider.value
                var sliderType = typeof(UnityEngine.UI.Slider);
                _sliderValueProperty = sliderType.GetProperty("value", BindingFlags.Public | BindingFlags.Instance);

                // Cache Button.onClick
                var buttonType = typeof(UnityEngine.UI.Button);
                _buttonOnClickProperty = buttonType.GetProperty("onClick", BindingFlags.Public | BindingFlags.Instance);

                // Cache UnityEvent.Invoke
                var unityEventType = typeof(UnityEngine.Events.UnityEvent);
                _unityEventInvokeMethod = unityEventType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);

                Debug.Log("[ATSAccessibility] CustomGamesReflection: Types cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CustomGamesReflection: Type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }

        // ========================================
        // DETECTION
        // ========================================

        /// <summary>
        /// Check if the given popup is a CustomGamePopup.
        /// </summary>
        public static bool IsCustomGamePopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "CustomGamePopup";
        }

        /// <summary>
        /// Find the active CustomGamePopup instance.
        /// </summary>
        public static object FindCustomGamePopup()
        {
            var topPopup = GameReflection.GetTopActivePopup();
            if (topPopup != null && IsCustomGamePopup(topPopup))
                return topPopup;
            return null;
        }

        // ========================================
        // PANEL ACCESS HELPERS
        // ========================================

        private static object GetPanel(object popup, FieldInfo panelField)
        {
            EnsureTypes();
            if (popup == null || panelField == null) return null;
            try
            {
                return panelField.GetValue(popup);
            }
            catch
            {
                return null;
            }
        }

        public static object GetDifficultyPicker(object popup) => GetPanel(popup, _difficultyPickerField);
        public static object GetReputationPanel(object popup) => GetPanel(popup, _reputationPanelField);
        public static object GetSeasonsPanel(object popup) => GetPanel(popup, _seasonsPanelField);
        public static object GetSeedPanel(object popup) => GetPanel(popup, _seedPanelField);
        public static object GetBiomePanel(object popup) => GetPanel(popup, _biomePanelField);
        public static object GetRacesPanel(object popup) => GetPanel(popup, _racesPanelField);
        public static object GetSeasonalEffectsPanel(object popup) => GetPanel(popup, _seasonalEffectsPanelField);
        public static object GetBlightPanel(object popup) => GetPanel(popup, _blightPanelField);
        public static object GetModifiersPanel(object popup) => GetPanel(popup, _modifiersPanelField);
        public static object GetTradeTownsPanel(object popup) => GetPanel(popup, _tradeTownsPanelField);
        public static object GetGoodsPanel(object popup) => GetPanel(popup, _goodsPanelField);
        public static object GetEffectsPanel(object popup) => GetPanel(popup, _effectsPanelField);

        // ========================================
        // DIFFICULTY
        // ========================================

        /// <summary>
        /// Get the list of available difficulties.
        /// </summary>
        public static List<object> GetAvailableDifficulties(object popup)
        {
            EnsureTypes();
            var result = new List<object>();

            try
            {
                var picker = GetDifficultyPicker(popup);
                if (picker == null || _pickerGetDifficultiesMethod == null) return result;

                var list = _pickerGetDifficultiesMethod.Invoke(picker, null) as IList;
                if (list == null) return result;

                foreach (var item in list)
                {
                    if (item != null) result.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAvailableDifficulties failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the current difficulty.
        /// </summary>
        public static object GetCurrentDifficulty(object popup)
        {
            EnsureTypes();

            try
            {
                var picker = GetDifficultyPicker(popup);
                if (picker == null || _pickerGetPickedDifficultyMethod == null) return null;

                return _pickerGetPickedDifficultyMethod.Invoke(picker, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the display name for a difficulty.
        /// </summary>
        public static string GetDifficultyDisplayName(object difficulty)
        {
            EnsureTypes();
            if (difficulty == null || _dmGetDisplayNameMethod == null) return "Unknown";

            try
            {
                return _dmGetDisplayNameMethod.Invoke(difficulty, null)?.ToString() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the index of a difficulty.
        /// </summary>
        public static int GetDifficultyIndex(object difficulty)
        {
            EnsureTypes();
            if (difficulty == null || _dmIndexField == null) return -1;

            try
            {
                return (int)_dmIndexField.GetValue(difficulty);
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Set the difficulty.
        /// </summary>
        public static bool SetDifficulty(object popup, object difficulty)
        {
            EnsureTypes();

            try
            {
                var picker = GetDifficultyPicker(popup);
                if (picker == null || _pickerSetDifficultyMethod == null) return false;

                _pickerSetDifficultyMethod.Invoke(picker, new[] { difficulty });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetDifficulty failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // SEED
        // ========================================

        /// <summary>
        /// Get the current seed value.
        /// </summary>
        public static string GetSeed(object popup)
        {
            EnsureTypes();

            try
            {
                var seedPanel = GetSeedPanel(popup);
                if (seedPanel == null || _seedInputField == null) return "";

                var inputField = _seedInputField.GetValue(seedPanel);
                if (inputField == null || _tmpInputFieldTextProperty == null) return "";

                return _tmpInputFieldTextProperty.GetValue(inputField) as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Randomize the seed by clicking the random button.
        /// </summary>
        public static bool RandomizeSeed(object popup)
        {
            EnsureTypes();

            try
            {
                var seedPanel = GetSeedPanel(popup);
                if (seedPanel == null || _seedButtonField == null) return false;

                var button = _seedButtonField.GetValue(seedPanel);
                if (button == null || _buttonOnClickProperty == null) return false;

                var onClick = _buttonOnClickProperty.GetValue(button);
                if (onClick == null || _unityEventInvokeMethod == null) return false;

                _unityEventInvokeMethod.Invoke(onClick, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RandomizeSeed failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the seed input field for text editing.
        /// </summary>
        public static TMPro.TMP_InputField GetSeedInputField(object popup)
        {
            EnsureTypes();

            try
            {
                var seedPanel = GetSeedPanel(popup);
                if (seedPanel == null || _seedInputField == null) return null;

                return _seedInputField.GetValue(seedPanel) as TMPro.TMP_InputField;
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // BIOME
        // ========================================

        /// <summary>
        /// Get the list of available biomes with their display names.
        /// </summary>
        public static List<(object biome, string displayName)> GetAvailableBiomes(object popup)
        {
            EnsureTypes();
            var result = new List<(object, string)>();

            try
            {
                var biomePanel = GetBiomePanel(popup);
                if (biomePanel == null || _biomeBiomesField == null) return result;

                var biomes = _biomeBiomesField.GetValue(biomePanel) as Array;
                if (biomes == null) return result;

                foreach (var biome in biomes)
                {
                    if (biome == null) continue;
                    string name = GetBiomeDisplayName(biome);
                    result.Add((biome, name));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAvailableBiomes failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the display name of a biome.
        /// </summary>
        public static string GetBiomeDisplayName(object biome)
        {
            EnsureTypes();
            if (biome == null) return "Unknown";

            try
            {
                // Get displayName field (LocaText) and extract Text
                if (_bmDisplayNameField != null)
                {
                    var locaText = _bmDisplayNameField.GetValue(biome);
                    if (locaText != null)
                    {
                        string text = GameReflection.GetLocaText(locaText);
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }

                // Fallback to Name property (from SO base class)
                if (_bmNameProperty != null)
                {
                    return _bmNameProperty.GetValue(biome)?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBiomeDisplayName failed: {ex.Message}");
            }

            return "Unknown";
        }

        /// <summary>
        /// Get the current biome index.
        /// </summary>
        public static int GetCurrentBiomeIndex(object popup)
        {
            EnsureTypes();

            try
            {
                var biomePanel = GetBiomePanel(popup);
                if (biomePanel == null || _biomeDropdownField == null) return 0;

                var dropdown = _biomeDropdownField.GetValue(biomePanel);
                if (dropdown == null || _tmpDropdownValueProperty == null) return 0;

                return (int)_tmpDropdownValueProperty.GetValue(dropdown);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Set the biome by index.
        /// </summary>
        public static bool SetBiomeIndex(object popup, int index)
        {
            EnsureTypes();

            try
            {
                var biomePanel = GetBiomePanel(popup);
                if (biomePanel == null || _biomeDropdownField == null) return false;

                var dropdown = _biomeDropdownField.GetValue(biomePanel);
                if (dropdown == null || _tmpDropdownValueProperty == null) return false;

                _tmpDropdownValueProperty.SetValue(dropdown, index);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SetBiomeIndex failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // RACES
        // ========================================

        /// <summary>
        /// Get all race slots with their selection status.
        /// Returns (raceModel, displayName, isSelected).
        /// </summary>
        public static List<(object race, string displayName, bool isSelected)> GetRaceSlots(object popup)
        {
            EnsureTypes();
            var result = new List<(object, string, bool)>();

            try
            {
                var racesPanel = GetRacesPanel(popup);
                if (racesPanel == null) return result;

                // Get the picked list
                var pickedList = _racesPickedField?.GetValue(racesPanel) as IList;
                var pickedSet = new HashSet<object>();
                if (pickedList != null)
                {
                    foreach (var item in pickedList)
                    {
                        if (item != null) pickedSet.Add(item);
                    }
                }

                // Get all slots
                var slots = _racesSlotsField?.GetValue(racesPanel) as IList;
                if (slots == null) return result;

                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    // Check if slot is active
                    var slotComponent = slot as UnityEngine.Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    // Get race from slot (field is named "model" in CustomGameRaceSlot)
                    var modelField = slot.GetType().GetField("model",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var race = modelField?.GetValue(slot);
                    if (race == null) continue;

                    string displayName = GetRaceDisplayName(race);
                    bool isSelected = pickedSet.Contains(race);

                    result.Add((race, displayName, isSelected));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetRaceSlots failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get the display name of a race.
        /// </summary>
        public static string GetRaceDisplayName(object race)
        {
            EnsureTypes();
            if (race == null) return "Unknown";

            try
            {
                // Get displayName field (LocaText) and extract Text
                if (_rmDisplayNameField != null)
                {
                    var locaText = _rmDisplayNameField.GetValue(race);
                    if (locaText != null)
                    {
                        string text = GameReflection.GetLocaText(locaText);
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }

                // Fallback to Name property (from SO base class)
                if (_rmNameProperty != null)
                {
                    return _rmNameProperty.GetValue(race)?.ToString() ?? "Unknown";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetRaceDisplayName failed: {ex.Message}");
            }

            return "Unknown";
        }

        /// <summary>
        /// Toggle a race slot by clicking it.
        /// </summary>
        public static bool ToggleRaceSlot(object popup, int slotIndex)
        {
            EnsureTypes();

            try
            {
                var racesPanel = GetRacesPanel(popup);
                if (racesPanel == null) return false;

                var slots = _racesSlotsField?.GetValue(racesPanel) as IList;
                if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return false;

                var slot = slots[slotIndex];
                if (slot == null) return false;

                // Find and click the button
                var slotComponent = slot as UnityEngine.Component;
                if (slotComponent == null) return false;

                var button = slotComponent.GetComponentInChildren<UnityEngine.UI.Button>();
                if (button != null)
                {
                    // Check interactability - game limits how many races can be selected
                    if (!button.interactable)
                        return false;

                    button.onClick.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleRaceSlot failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // SLIDER PANELS (Reputation, Seasons, Blight)
        // ========================================

        /// <summary>
        /// Helper to get slider info from a FloatOptionsSliderPanel.
        /// Returns (currentIndex, maxIndex, currentLabel, currentValue).
        /// </summary>
        private static (int index, int max, string label, float value) GetSliderInfo(object sliderPanel)
        {
            EnsureTypes();
            if (sliderPanel == null) return (0, 0, "", 0);

            try
            {
                int currentIndex = 0;
                if (_sliderGetPickedIndexMethod != null)
                {
                    currentIndex = (int)_sliderGetPickedIndexMethod.Invoke(sliderPanel, null);
                }
                else if (_sliderCurrentIndexField != null)
                {
                    currentIndex = (int)_sliderCurrentIndexField.GetValue(sliderPanel);
                }

                var options = _sliderOptionsField?.GetValue(sliderPanel) as Array;
                int maxIndex = options?.Length - 1 ?? 0;

                string label = "";
                float value = 0;
                if (options != null && currentIndex >= 0 && currentIndex < options.Length)
                {
                    var option = options.GetValue(currentIndex);
                    if (option != null)
                    {
                        if (_floatOptionAmountField != null)
                        {
                            value = (float)_floatOptionAmountField.GetValue(option);
                        }

                        if (_floatOptionLabelField != null)
                        {
                            var locaText = _floatOptionLabelField.GetValue(option);
                            label = GameReflection.GetLocaText(locaText) ?? "";
                        }
                    }
                }

                return (currentIndex, maxIndex, label, value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetSliderInfo failed: {ex.Message}");
                return (0, 0, "", 0);
            }
        }

        /// <summary>
        /// Set slider index.
        /// </summary>
        private static bool SetSliderIndex(object sliderPanel, int index)
        {
            EnsureTypes();
            if (sliderPanel == null || _sliderSetIndexMethod == null) return false;

            try
            {
                _sliderSetIndexMethod.Invoke(sliderPanel, new object[] { index });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get reputation sliders info.
        /// </summary>
        public static List<(string name, int index, int max, float value)> GetReputationSliders(object popup)
        {
            EnsureTypes();
            var result = new List<(string, int, int, float)>();

            try
            {
                var panel = GetReputationPanel(popup);
                if (panel == null) return result;

                var repSlider = _repReputationSliderField?.GetValue(panel);
                var penSlider = _repPenaltySliderField?.GetValue(panel);
                var rateSlider = _repPenaltyRateSliderField?.GetValue(panel);

                if (repSlider != null)
                {
                    var info = GetSliderInfo(repSlider);
                    result.Add(("Reputation to Win", info.index, info.max, info.value));
                }
                if (penSlider != null)
                {
                    var info = GetSliderInfo(penSlider);
                    result.Add(("Impatience to Lose", info.index, info.max, info.value));
                }
                if (rateSlider != null)
                {
                    var info = GetSliderInfo(rateSlider);
                    result.Add(("Impatience Rate", info.index, info.max, info.value));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetReputationSliders failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Adjust a reputation slider.
        /// </summary>
        public static bool AdjustReputationSlider(object popup, int sliderIndex, int delta)
        {
            EnsureTypes();

            try
            {
                var panel = GetReputationPanel(popup);
                if (panel == null) return false;

                object slider = null;
                switch (sliderIndex)
                {
                    case 0: slider = _repReputationSliderField?.GetValue(panel); break;
                    case 1: slider = _repPenaltySliderField?.GetValue(panel); break;
                    case 2: slider = _repPenaltyRateSliderField?.GetValue(panel); break;
                }

                if (slider == null) return false;

                var info = GetSliderInfo(slider);
                int newIndex = Mathf.Clamp(info.index + delta, 0, info.max);
                return SetSliderIndex(slider, newIndex);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get seasons sliders info.
        /// </summary>
        public static List<(string name, int index, int max, float value)> GetSeasonsSliders(object popup)
        {
            EnsureTypes();
            var result = new List<(string, int, int, float)>();

            try
            {
                var panel = GetSeasonsPanel(popup);
                if (panel == null) return result;

                var drizzle = _seasonsDrizzleField?.GetValue(panel);
                var clearance = _seasonsClearanceField?.GetValue(panel);
                var storm = _seasonsStormField?.GetValue(panel);

                if (drizzle != null)
                {
                    var info = GetSliderInfo(drizzle);
                    result.Add(("Drizzle Duration", info.index, info.max, info.value));
                }
                if (clearance != null)
                {
                    var info = GetSliderInfo(clearance);
                    result.Add(("Clearance Duration", info.index, info.max, info.value));
                }
                if (storm != null)
                {
                    var info = GetSliderInfo(storm);
                    result.Add(("Storm Duration", info.index, info.max, info.value));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetSeasonsSliders failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Adjust a seasons slider.
        /// </summary>
        public static bool AdjustSeasonsSlider(object popup, int sliderIndex, int delta)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonsPanel(popup);
                if (panel == null) return false;

                object slider = null;
                switch (sliderIndex)
                {
                    case 0: slider = _seasonsDrizzleField?.GetValue(panel); break;
                    case 1: slider = _seasonsClearanceField?.GetValue(panel); break;
                    case 2: slider = _seasonsStormField?.GetValue(panel); break;
                }

                if (slider == null) return false;

                var info = GetSliderInfo(slider);
                int newIndex = Mathf.Clamp(info.index + delta, 0, info.max);
                return SetSliderIndex(slider, newIndex);
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // BLIGHT
        // ========================================

        /// <summary>
        /// Check if blight is enabled.
        /// </summary>
        public static bool IsBlightEnabled(object popup)
        {
            EnsureTypes();

            try
            {
                var panel = GetBlightPanel(popup);
                if (panel == null) return false;

                var toggle = _blightToggleField?.GetValue(panel);
                if (toggle == null || _toggleIsOnMethod == null) return false;

                return (bool)_toggleIsOnMethod.Invoke(toggle, null);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Toggle blight on/off.
        /// </summary>
        public static bool ToggleBlight(object popup)
        {
            EnsureTypes();

            try
            {
                var panel = GetBlightPanel(popup);
                if (panel == null) return false;

                var toggle = _blightToggleField?.GetValue(panel);
                if (toggle == null) return false;

                // Find and click the button
                var toggleComponent = toggle as UnityEngine.Component;
                if (toggleComponent == null) return false;

                var button = toggleComponent.GetComponentInChildren<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleBlight failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get blight sliders info.
        /// </summary>
        public static List<(string name, int index, int max, float value)> GetBlightSliders(object popup)
        {
            EnsureTypes();
            var result = new List<(string, int, int, float)>();

            try
            {
                var panel = GetBlightPanel(popup);
                if (panel == null) return result;

                var footprint = _blightFootprintField?.GetValue(panel);
                var corruption = _blightCorruptionField?.GetValue(panel);

                if (footprint != null)
                {
                    var info = GetSliderInfo(footprint);
                    result.Add(("Blight Footprint", info.index, info.max, info.value));
                }
                if (corruption != null)
                {
                    var info = GetSliderInfo(corruption);
                    result.Add(("Corruption Rate", info.index, info.max, info.value));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetBlightSliders failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Adjust a blight slider.
        /// </summary>
        public static bool AdjustBlightSlider(object popup, int sliderIndex, int delta)
        {
            EnsureTypes();

            try
            {
                var panel = GetBlightPanel(popup);
                if (panel == null) return false;

                object slider = null;
                switch (sliderIndex)
                {
                    case 0: slider = _blightFootprintField?.GetValue(panel); break;
                    case 1: slider = _blightCorruptionField?.GetValue(panel); break;
                }

                if (slider == null) return false;

                var info = GetSliderInfo(slider);
                int newIndex = Mathf.Clamp(info.index + delta, 0, info.max);
                return SetSliderIndex(slider, newIndex);
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // SEASONAL EFFECTS
        // ========================================

        /// <summary>
        /// Check if seasonal effects are in random mode.
        /// </summary>
        public static bool IsSeasonalEffectsRandom(object popup)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return true;

                var toggle = _seasonalRandomButtonField?.GetValue(panel);
                if (toggle == null || _toggleIsOnMethod == null) return true;

                return (bool)_toggleIsOnMethod.Invoke(toggle, null);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Get seasonal effects slider values (positive, negative).
        /// </summary>
        public static (int positive, int negative) GetSeasonalEffectsCounts(object popup)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return (0, 0);

                var positiveSlider = _seasonalPositiveSliderField?.GetValue(panel);
                var negativeSlider = _seasonalNegativeSliderField?.GetValue(panel);

                int positive = 0, negative = 0;
                if (positiveSlider != null && _sliderValueProperty != null)
                {
                    positive = Mathf.RoundToInt((float)_sliderValueProperty.GetValue(positiveSlider));
                }
                if (negativeSlider != null && _sliderValueProperty != null)
                {
                    negative = Mathf.RoundToInt((float)_sliderValueProperty.GetValue(negativeSlider));
                }

                return (positive, negative);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Toggle seasonal effects between random and manual mode.
        /// </summary>
        public static bool ToggleSeasonalEffectsMode(object popup)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return false;

                var toggle = _seasonalRandomButtonField?.GetValue(panel);
                if (toggle == null) return false;

                // Find and click the button
                var toggleComponent = toggle as Component;
                if (toggleComponent == null) return false;

                var button = toggleComponent.GetComponentInChildren<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleSeasonalEffectsMode failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Adjust the positive seasonal effects slider.
        /// </summary>
        public static bool AdjustSeasonalEffectsPositive(object popup, int delta)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return false;

                var slider = _seasonalPositiveSliderField?.GetValue(panel) as UnityEngine.UI.Slider;
                if (slider == null) return false;

                float newValue = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
                if (Mathf.Approximately(newValue, slider.value)) return false;

                slider.value = newValue;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AdjustSeasonalEffectsPositive failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Data class for seasonal effect info.
        /// </summary>
        public class SeasonalEffectInfo
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public bool IsPositive { get; set; }
            public bool IsPicked { get; set; }
            public int Type { get; set; }  // 0 = SimplePerk, 1 = Conditional
        }

        /// <summary>
        /// Get all available seasonal effects for manual mode.
        /// </summary>
        public static List<SeasonalEffectInfo> GetAllSeasonalEffects(object popup)
        {
            EnsureTypes();
            var result = new List<SeasonalEffectInfo>();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return result;

                // Get the picked list to check which are selected
                var picked = _seasonalPickedField?.GetValue(panel) as IList;
                var pickedNames = new HashSet<string>();
                if (picked != null)
                {
                    foreach (var se in picked)
                    {
                        var nameField = se.GetType().GetField("name");
                        if (nameField != null)
                        {
                            var name = nameField.GetValue(se) as string;
                            if (!string.IsNullOrEmpty(name))
                                pickedNames.Add(name);
                        }
                    }
                }

                // Get all effects from Settings
                var settings = GameReflection.GetSettings();
                if (settings == null) return result;

                var settingsType = settings.GetType();

                // Get simple seasonal effects (these are fields, not properties)
                var simpleEffects = settingsType.GetField("simpleSeasonalEffects")?.GetValue(settings) as IEnumerable;
                int simpleCount = 0;
                int simpleFiltered = 0;
                if (simpleEffects != null)
                {
                    foreach (var effect in simpleEffects)
                    {
                        simpleCount++;
                        var info = GetSeasonalEffectInfo(effect, 0, pickedNames);
                        if (info != null && info.DisplayName != "Unknown")
                        {
                            result.Add(info);
                            simpleFiltered++;
                        }
                    }
                }

                // Get conditional seasonal effects (these are fields, not properties)
                var conditionalEffects = settingsType.GetField("conditionalSeasonalEffects")?.GetValue(settings) as IEnumerable;
                int condCount = 0;
                int condFiltered = 0;
                if (conditionalEffects != null)
                {
                    foreach (var effect in conditionalEffects)
                    {
                        condCount++;
                        var info = GetSeasonalEffectInfo(effect, 1, pickedNames);
                        if (info != null && info.DisplayName != "Unknown")
                        {
                            result.Add(info);
                            condFiltered++;
                        }
                    }
                }

                // Sort: positive first, then by name
                result = result.OrderByDescending(e => e.IsPositive).ThenBy(e => e.DisplayName).ToList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllSeasonalEffects failed: {ex.Message}");
            }

            return result;
        }

        private static SeasonalEffectInfo GetSeasonalEffectInfo(object effect, int type, HashSet<string> pickedNames)
        {
            if (effect == null) return null;

            try
            {
                var effectType = effect.GetType();

                // Check if in custom mode (property is PascalCase: IsInCustomMode)
                var isInCustomModeProp = effectType.GetProperty("IsInCustomMode");
                if (isInCustomModeProp == null) return null;
                var isInCustomMode = isInCustomModeProp.GetValue(effect);
                if (isInCustomMode == null || !(bool)isInCustomMode)
                    return null;

                var name = effectType.GetProperty("Name")?.GetValue(effect) as string;
                if (string.IsNullOrEmpty(name)) return null;

                var displayName = effectType.GetProperty("DisplayName")?.GetValue(effect) as string ?? "Unknown";
                var description = effectType.GetProperty("Description")?.GetValue(effect) as string ?? "";
                var isPositive = effectType.GetProperty("IsPositive")?.GetValue(effect);

                return new SeasonalEffectInfo
                {
                    Name = name,
                    DisplayName = displayName,
                    Description = description,
                    IsPositive = isPositive != null && (bool)isPositive,
                    IsPicked = pickedNames.Contains(name),
                    Type = type
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetSeasonalEffectInfo exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Toggle a seasonal effect in manual mode.
        /// </summary>
        public static bool ToggleSeasonalEffect(object popup, SeasonalEffectInfo effect)
        {
            EnsureTypes();

            try
            {
                var panel = GetSeasonalEffectsPanel(popup);
                if (panel == null) return false;

                var picked = _seasonalPickedField?.GetValue(panel) as IList;
                if (picked == null) return false;

                // Find the SeasonalEffect type
                var seasonalEffectType = GameReflection.GameAssembly.GetType("Eremite.Model.SeasonalEffect");
                if (seasonalEffectType == null) return false;

                if (effect.IsPicked)
                {
                    // Remove from picked
                    for (int i = picked.Count - 1; i >= 0; i--)
                    {
                        var se = picked[i];
                        var nameField = se.GetType().GetField("name");
                        if (nameField != null)
                        {
                            var name = nameField.GetValue(se) as string;
                            if (name == effect.Name)
                            {
                                picked.RemoveAt(i);
                                effect.IsPicked = false;
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    // Add to picked - create new SeasonalEffect
                    var newEffect = Activator.CreateInstance(seasonalEffectType);
                    var nameField = seasonalEffectType.GetField("name");
                    var typeField = seasonalEffectType.GetField("type");

                    if (nameField != null) nameField.SetValue(newEffect, effect.Name);
                    if (typeField != null)
                    {
                        // SeasonEffectType: SimplePerk = 0, Conditional = 1
                        var enumType = GameReflection.GameAssembly.GetType("Eremite.Model.SeasonEffectType");
                        if (enumType != null)
                        {
                            var enumValue = Enum.ToObject(enumType, effect.Type);
                            typeField.SetValue(newEffect, enumValue);
                        }
                    }

                    picked.Add(newEffect);
                    effect.IsPicked = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleSeasonalEffect failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get maximum number of seasonal effects that can be picked.
        /// </summary>
        public static int GetMaxSeasonalEffects()
        {
            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return 6;

                var config = settings.GetType().GetProperty("customGameConfig")?.GetValue(settings);
                if (config == null) return 6;

                var maxEffects = config.GetType().GetField("maxSeasonalEffects")?.GetValue(config);
                if (maxEffects != null) return (int)maxEffects;
            }
            catch { }

            return 6;  // Default fallback
        }

        // ========================================
        // TRADE TOWNS
        // ========================================

        /// <summary>
        /// Get all trade towns with their names and selection state.
        /// </summary>
        public static List<(string name, bool isSelected)> GetTradeTownSlots(object popup)
        {
            EnsureTypes();
            var result = new List<(string, bool)>();

            try
            {
                var panel = GetTradeTownsPanel(popup);
                if (panel == null) return result;

                var slots = _tradeTownsSlotsField?.GetValue(panel) as IList;
                if (slots == null) return result;

                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    var slotComponent = slot as Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    // Get name from the label
                    string name = "Unknown";
                    var label = _tradeTownSlotLabelField?.GetValue(slot);
                    if (label != null && _tmpTextProperty != null)
                    {
                        name = _tmpTextProperty.GetValue(label) as string ?? "Unknown";
                    }

                    // Get selection state from the toggle
                    bool isSelected = false;
                    var toggle = _tradeTownSlotToggleField?.GetValue(slot);
                    if (toggle != null && _toggleIsOnMethod != null)
                    {
                        isSelected = (bool)_toggleIsOnMethod.Invoke(toggle, null);
                    }

                    result.Add((name, isSelected));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetTradeTownSlots failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Toggle a trade town slot by clicking it.
        /// </summary>
        public static bool ToggleTradeTownSlot(object popup, int slotIndex)
        {
            EnsureTypes();

            try
            {
                var panel = GetTradeTownsPanel(popup);
                if (panel == null) return false;

                var slots = _tradeTownsSlotsField?.GetValue(panel) as IList;
                if (slots == null) return false;

                // Find the actual slot at the index (only counting active slots)
                int activeIndex = 0;
                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    var slotComponent = slot as Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    if (activeIndex == slotIndex)
                    {
                        // Find and click the button
                        var toggle = _tradeTownSlotToggleField?.GetValue(slot);
                        if (toggle == null) return false;

                        var toggleComponent = toggle as Component;
                        if (toggleComponent == null) return false;

                        var button = toggleComponent.GetComponentInChildren<UnityEngine.UI.Button>();
                        if (button != null)
                        {
                            // Check interactability - game limits how many can be selected
                            if (!button.interactable)
                                return false;

                            button.onClick.Invoke();
                            return true;
                        }
                        return false;
                    }
                    activeIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleTradeTownSlot failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // MODIFIERS
        // ========================================

        /// <summary>
        /// Get all modifiers with their current status.
        /// Returns (effectName, displayName, description, isPositive, isPicked, type).
        /// </summary>
        public static List<ModifierInfo> GetAllModifiers(object popup)
        {
            EnsureTypes();
            var result = new List<ModifierInfo>();

            try
            {
                var panel = GetModifiersPanel(popup);
                if (panel == null) return result;

                var allModifiers = _modAllModifiersField?.GetValue(panel) as IList;
                if (allModifiers == null) return result;

                foreach (var modifier in allModifiers)
                {
                    if (modifier == null) continue;

                    string effectName = _mdEffectField?.GetValue(modifier) as string ?? "";
                    bool isPositive = (bool)(_mdIsPositiveField?.GetValue(modifier) ?? false);
                    bool isPicked = (bool)(_mdIsPickedField?.GetValue(modifier) ?? false);
                    int typeValue = (int)(_mdTypeField?.GetValue(modifier) ?? 0);

                    // Get display name and description from effect model
                    var (displayName, description) = GetEffectNameAndDescription(effectName);

                    result.Add(new ModifierInfo
                    {
                        EffectName = effectName,
                        DisplayName = displayName,
                        Description = description,
                        IsPositive = isPositive,
                        IsPicked = isPicked,
                        Type = (ModifierType)typeValue,
                        DataObject = modifier
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetAllModifiers failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get effect display name and description.
        /// </summary>
        private static (string name, string description) GetEffectNameAndDescription(string effectName)
        {
            if (string.IsNullOrEmpty(effectName))
                return ("Unknown", "");

            try
            {
                var effectModel = GameReflection.GetEffectModel(effectName);
                if (effectModel == null)
                    return (effectName, "");

                var displayNameProp = effectModel.GetType().GetProperty("DisplayName",
                    BindingFlags.Public | BindingFlags.Instance);
                string displayName = displayNameProp?.GetValue(effectModel)?.ToString() ?? effectName;

                var descProp = effectModel.GetType().GetProperty("Description",
                    BindingFlags.Public | BindingFlags.Instance);
                string description = descProp?.GetValue(effectModel)?.ToString() ?? "";

                return (displayName, description);
            }
            catch
            {
                return (effectName, "");
            }
        }

        /// <summary>
        /// Toggle a modifier's picked state.
        /// </summary>
        public static bool ToggleModifier(object popup, ModifierInfo modifier)
        {
            EnsureTypes();
            if (modifier?.DataObject == null) return false;

            try
            {
                // Toggle the isPicked field directly on the data object
                bool currentPicked = (bool)(_mdIsPickedField?.GetValue(modifier.DataObject) ?? false);
                bool newPicked = !currentPicked;
                _mdIsPickedField?.SetValue(modifier.DataObject, newPicked);

                // Update the UI by calling UpdateSlots on the panel
                var panel = GetModifiersPanel(popup);
                if (panel != null)
                {
                    var updateMethod = panel.GetType().GetMethod("UpdateSlots",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    updateMethod?.Invoke(panel, null);
                }

                // Update the ModifierInfo to reflect the new state
                modifier.IsPicked = newPicked;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleModifier failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // EMBARK GOODS
        // ========================================

        /// <summary>
        /// Get all embark goods with quantities.
        /// </summary>
        public static List<(string name, int amount)> GetEmbarkGoods(object popup)
        {
            EnsureTypes();
            var result = new List<(string, int)>();

            try
            {
                var panel = GetGoodsPanel(popup);
                if (panel == null) return result;

                var slots = _goodsSlotsField?.GetValue(panel) as IList;
                if (slots == null) return result;

                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    var slotComponent = slot as UnityEngine.Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    if (_goodSlotGetGoodMethod != null)
                    {
                        var good = _goodSlotGetGoodMethod.Invoke(slot, null);
                        if (good != null)
                        {
                            string goodName = _goodNameField?.GetValue(good) as string ?? "";
                            int amount = (int)(_goodAmountField?.GetValue(good) ?? 0);

                            // Include all goods even at 0 so player doesn't lose their place
                            if (!string.IsNullOrEmpty(goodName))
                            {
                                string displayName = GameReflection.GetGoodDisplayName(goodName);
                                result.Add((displayName, amount));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetEmbarkGoods failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Adjust an embark good amount.
        /// </summary>
        public static bool AdjustEmbarkGood(object popup, int slotIndex, int delta)
        {
            EnsureTypes();

            try
            {
                var panel = GetGoodsPanel(popup);
                if (panel == null) return false;

                var slots = _goodsSlotsField?.GetValue(panel) as IList;
                if (slots == null) return false;

                // Find the slot at the active index
                int activeIndex = 0;
                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    var slotComponent = slot as UnityEngine.Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    if (activeIndex == slotIndex)
                    {
                        // Click the plus or minus button directly
                        // This properly updates the internal Good struct
                        var buttonField = delta > 0 ? _goodSlotPlusButtonField : _goodSlotMinusButtonField;
                        if (buttonField == null) return false;

                        var button = buttonField.GetValue(slot) as UnityEngine.UI.Button;
                        if (button == null) return false;

                        // Click the button multiple times based on delta magnitude
                        int clicks = Math.Abs(delta);
                        bool anyClicked = false;
                        for (int i = 0; i < clicks; i++)
                        {
                            // Re-check button state each time (may hit min/max limit or be destroyed)
                            if (button == null || !button.interactable) break;
                            button.onClick?.Invoke();
                            anyClicked = true;
                        }
                        return anyClicked;
                    }
                    activeIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AdjustEmbarkGood failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // EMBARK EFFECTS
        // ========================================

        /// <summary>
        /// Get all embark effects with selection status.
        /// </summary>
        public static List<(object effect, string displayName, bool isSelected)> GetEmbarkEffects(object popup)
        {
            EnsureTypes();
            var result = new List<(object, string, bool)>();

            try
            {
                var panel = GetEffectsPanel(popup);
                if (panel == null) return result;

                var allEffects = _effectsAllField?.GetValue(panel) as IList;
                var pickedEffects = _effectsPickedField?.GetValue(panel) as IList;

                var pickedSet = new HashSet<object>();
                if (pickedEffects != null)
                {
                    foreach (var item in pickedEffects)
                    {
                        if (item != null) pickedSet.Add(item);
                    }
                }

                if (allEffects == null) return result;

                foreach (var effect in allEffects)
                {
                    if (effect == null) continue;

                    string displayName = "Unknown";
                    if (_emDisplayNameProperty != null)
                    {
                        displayName = _emDisplayNameProperty.GetValue(effect)?.ToString() ?? "Unknown";
                    }

                    bool isSelected = pickedSet.Contains(effect);
                    result.Add((effect, displayName, isSelected));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] GetEmbarkEffects failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Toggle an embark effect selection.
        /// </summary>
        public static bool ToggleEmbarkEffect(object popup, int effectIndex)
        {
            EnsureTypes();

            try
            {
                var panel = GetEffectsPanel(popup);
                if (panel == null) return false;

                var slots = _effectsSlotsField?.GetValue(panel) as IList;
                if (slots == null) return false;

                // Find the slot at the index
                int activeIndex = 0;
                foreach (var slot in slots)
                {
                    if (slot == null) continue;

                    var slotComponent = slot as UnityEngine.Component;
                    if (slotComponent == null || !slotComponent.gameObject.activeSelf) continue;

                    if (activeIndex == effectIndex)
                    {
                        // Click the button
                        var button = slotComponent.GetComponentInChildren<UnityEngine.UI.Button>();
                        if (button != null)
                        {
                            button.onClick.Invoke();
                            return true;
                        }
                        return false;
                    }
                    activeIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ToggleEmbarkEffect failed: {ex.Message}");
            }

            return false;
        }

        // ========================================
        // EMBARK BUTTON
        // ========================================

        /// <summary>
        /// Trigger the embark action.
        /// </summary>
        public static bool TriggerEmbark(object popup)
        {
            EnsureTypes();

            try
            {
                if (popup == null || _embarkButtonField == null) return false;

                var button = _embarkButtonField.GetValue(popup);
                if (button == null || _buttonOnClickProperty == null) return false;

                var onClick = _buttonOnClickProperty.GetValue(button);
                if (onClick == null || _unityEventInvokeMethod == null) return false;

                _unityEventInvokeMethod.Invoke(onClick, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] TriggerEmbark failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // DATA CLASSES
        // ========================================

        public enum ModifierType
        {
            WorldMap = 0,
            Daily = 1,
            Difficulty = 2
        }

        public class ModifierInfo
        {
            public string EffectName;
            public string DisplayName;
            public string Description;
            public bool IsPositive;
            public bool IsPicked;
            public ModifierType Type;
            public object DataObject;
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(CustomGamesReflection), "CustomGamesReflection");
        }
    }
}
