# CustomGamesReflection.cs

Provides reflection-based access to Custom Games (Training Expeditions) popup internals.

CRITICAL RULES:
- Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
- NEVER cache instance references (services, controllers) - they are destroyed on scene change
- All public methods return fresh values by querying through cached PropertyInfo

## class CustomGamesReflection (line 17)

### Nested Types
- public class SeasonalEffectInfo (line 1089): properties `Name`, `DisplayName`, `Description`, `IsPositive`, `IsPicked`, `Type` (0=SimplePerk, 1=Conditional)
- public enum ModifierType (line 1594): `WorldMap=0`, `Daily=1`, `Difficulty=2`
- public class ModifierInfo (line 1600): fields `EffectName`, `DisplayName`, `Description`, `IsPositive`, `IsPicked`, `Type`, `DataObject`

### Fields (private static cached)

#### Types
- private static Type _customGamePopupType (line 21)
- private static Type _difficultyPickerType (line 22)
- private static Type _difficultyModelType (line 23)
- private static Type _floatOptionsSliderPanelType (line 24)
- private static Type _toggleButtonType (line 25)
- private static Type _modifierDataType (line 26)
- private static Type _raceModelType (line 27)
- private static Type _biomeModelType (line 28)
- private static Type _goodStructType (line 29)
- private static Type _effectModelType (line 30)

#### CustomGamePopup panel fields
- private static FieldInfo _embarkButtonField (line 33)
- private static FieldInfo _difficultyPickerField (line 34)
- private static FieldInfo _reputationPanelField (line 35)
- private static FieldInfo _seasonsPanelField (line 36)
- private static FieldInfo _seedPanelField (line 37)
- private static FieldInfo _biomePanelField (line 38)
- private static FieldInfo _racesPanelField (line 39)
- private static FieldInfo _seasonalEffectsPanelField (line 40)
- private static FieldInfo _blightPanelField (line 41)
- private static FieldInfo _modifiersPanelField (line 42)
- private static FieldInfo _tradeTownsPanelField (line 43)
- private static FieldInfo _goodsPanelField (line 44)
- private static FieldInfo _effectsPanelField (line 45)
- private static FieldInfo _layoutsPopupField (line 46)

#### DifficultyPicker
- private static FieldInfo _pickerDropdownField (line 49)
- private static FieldInfo _pickerDifficultyField (line 50)
- private static MethodInfo _pickerGetDifficultiesMethod (line 51)
- private static MethodInfo _pickerGetPickedDifficultyMethod (line 52)
- private static MethodInfo _pickerSetDifficultyMethod (line 53)

#### DifficultyModel
- private static MethodInfo _dmGetDisplayNameMethod (line 56)
- private static FieldInfo _dmIndexField (line 57)

#### FloatOptionsSliderPanel
- private static FieldInfo _sliderOptionsField (line 60)
- private static FieldInfo _sliderCurrentIndexField (line 61)
- private static MethodInfo _sliderGetPickedIndexMethod (line 62)
- private static MethodInfo _sliderSetIndexMethod (line 63)

#### FloatOption
- private static FieldInfo _floatOptionLabelField (line 66)
- private static FieldInfo _floatOptionAmountField (line 67)

#### ToggleButton
- private static MethodInfo _toggleIsOnMethod (line 70)

#### SeedPanel
- private static FieldInfo _seedInputField (line 73)
- private static FieldInfo _seedButtonField (line 74)

#### BiomePanel
- private static FieldInfo _biomeDropdownField (line 77)
- private static FieldInfo _biomeBiomesField (line 78)

#### BiomeModel
- private static FieldInfo _bmDisplayNameField (line 81)
- private static PropertyInfo _bmNameProperty (line 82)

#### RacesPanel
- private static FieldInfo _racesSlotsField (line 85)
- private static FieldInfo _racesPickedField (line 86)

#### RaceModel
- private static FieldInfo _rmDisplayNameField (line 89)
- private static PropertyInfo _rmNameProperty (line 90)

#### ReputationPanel sliders
- private static FieldInfo _repReputationSliderField (line 93)
- private static FieldInfo _repPenaltySliderField (line 94)
- private static FieldInfo _repPenaltyRateSliderField (line 95)

#### SeasonsDurationPanel sliders
- private static FieldInfo _seasonsDrizzleField (line 98)
- private static FieldInfo _seasonsClearanceField (line 99)
- private static FieldInfo _seasonsStormField (line 100)

#### SeasonalEffectsPanel
- private static FieldInfo _seasonalRandomButtonField (line 103)
- private static FieldInfo _seasonalPositiveSliderField (line 104)
- private static FieldInfo _seasonalNegativeSliderField (line 105)
- private static FieldInfo _seasonalPickedField (line 106)

#### BlightPanel
- private static FieldInfo _blightToggleField (line 109)
- private static FieldInfo _blightFootprintField (line 110)
- private static FieldInfo _blightCorruptionField (line 111)

#### ModifiersPanel
- private static FieldInfo _modAllModifiersField (line 114)
- private static FieldInfo _modCurrentModifiersField (line 115)
- private static FieldInfo _modSlotsField (line 116)
- private static FieldInfo _modCategorySlotsField (line 117)

#### ModifierData
- private static FieldInfo _mdModelField (line 120)
- private static FieldInfo _mdEffectField (line 121)
- private static FieldInfo _mdIsPositiveField (line 122)
- private static FieldInfo _mdIsPickedField (line 123)
- private static FieldInfo _mdTypeField (line 124)

#### ModifierSlot
- private static MethodInfo _modSlotGetModifierMethod (line 127)
- private static FieldInfo _modSlotToggleField (line 128)

#### ModifiersCategorySlot
- private static MethodInfo _catSlotIsOnMethod (line 131)
- private static MethodInfo _catSlotGetModifierTypeMethod (line 132)

#### TradeTownsPanel
- private static FieldInfo _tradeTownsSlotsField (line 135)
- private static FieldInfo _tradeTownsAllField (line 136)
- private static FieldInfo _tradeTownsPickedField (line 137)

#### CustomGameTradeTownSlot
- private static FieldInfo _tradeTownSlotTownField (line 140)
- private static FieldInfo _tradeTownSlotToggleField (line 141)
- private static FieldInfo _tradeTownSlotLabelField (line 142)

#### CustomGameTradeTownData
- private static FieldInfo _tradeTownDataFieldField (line 145)
- private static FieldInfo _tradeTownDataFactionField (line 146)

#### GoodsPanel
- private static FieldInfo _goodsSlotsField (line 149)

#### GoodSlot
- private static MethodInfo _goodSlotGetGoodMethod (line 152)
- private static FieldInfo _goodSlotPlusButtonField (line 153)
- private static FieldInfo _goodSlotMinusButtonField (line 154)

#### Good struct
- private static FieldInfo _goodNameField (line 157)
- private static FieldInfo _goodAmountField (line 158)

#### EffectsPanel
- private static FieldInfo _effectsSlotsField (line 161)
- private static FieldInfo _effectsPickedField (line 162)
- private static FieldInfo _effectsAllField (line 163)

#### EffectModel
- private static PropertyInfo _emDisplayNameProperty (line 166)
- private static PropertyInfo _emNameProperty (line 167)

#### LayoutsPopup
- private static FieldInfo _layoutsSlotsField (line 170)
- private static FieldInfo _layoutsIsSaveField (line 171)

#### Unity UI
- private static PropertyInfo _tmpTextProperty (line 174)
- private static PropertyInfo _tmpDropdownValueProperty (line 177)
- private static PropertyInfo _tmpDropdownOptionsProperty (line 178)
- private static PropertyInfo _tmpInputFieldTextProperty (line 181)
- private static PropertyInfo _sliderValueProperty (line 184)
- private static PropertyInfo _buttonOnClickProperty (line 187)
- private static MethodInfo _unityEventInvokeMethod (line 188)
- private static bool _typesCached (line 190)

### Methods
- private static void EnsureTypes() (line 192)
- public static bool IsCustomGamePopup(object popup) (line 498)
- public static object FindCustomGamePopup() (line 506)

#### Panel accessors (all return the panel component or null)
- public static object GetDifficultyPicker(object popup) (line 522)
- public static object GetReputationPanel(object popup) (line ~)
- public static object GetSeasonsPanel(object popup) (line ~)
- public static object GetSeedPanel(object popup) (line ~)
- public static object GetBiomePanel(object popup) (line ~)
- public static object GetRacesPanel(object popup) (line ~)
- public static object GetSeasonalEffectsPanel(object popup) (line ~)
- public static object GetBlightPanel(object popup) (line ~)
- public static object GetModifiersPanel(object popup) (line ~)
- public static object GetTradeTownsPanel(object popup) (line ~)
- public static object GetGoodsPanel(object popup) (line ~)
- public static object GetEffectsPanel(object popup) (line ~)

#### Difficulty
- public static List<object> GetAvailableDifficulties(object popup) (line 542)
- public static object GetCurrentDifficulty(object popup) (line 560)
- public static string GetDifficultyDisplayName(object difficulty) (line 569)
- public static int GetDifficultyIndex(object difficulty) (line 577)
- public static void SetDifficulty(object popup, object difficulty) (line 586)

#### Seed
- public static int GetSeed(object popup) (line 599)
- public static void RandomizeSeed(object popup) (line 609)
- public static object GetSeedInputField(object popup) (line 620)
  Returns `TMPro.TMP_InputField` for the seed text input.

#### Biome
- public static List<(object biome, string displayName)> GetAvailableBiomes(object popup) (line 633)
- public static string GetBiomeDisplayName(object biome) (line 653)
- public static int GetCurrentBiomeIndex(object popup) (line 668)
- public static void SetBiomeIndex(object popup, int index) (line 678)

#### Races
- public static List<(object race, string displayName, bool isSelected)> GetRaceSlots(object popup) (line 700)
- public static string GetRaceDisplayName(object race) (line 745)
- public static void ToggleRaceSlot(object popup, int slotIndex) (line 760)

#### Reputation sliders
- public static List<(string name, int index, int max, float value)> GetReputationSliders(object popup) (line 830)
- public static void AdjustReputationSlider(object popup, int sliderIndex, int delta) (line 860)

#### Seasons sliders
- public static List<(string name, int index, int max, float value)> GetSeasonsSliders(object popup) (line 883)
- public static void AdjustSeasonsSlider(object popup, int sliderIndex, int delta) (line 913)

#### Blight
- public static bool IsBlightEnabled(object popup) (line 940)
- public static void ToggleBlight(object popup) (line 950)
- public static List<(string name, int index, int max, float value)> GetBlightSliders(object popup) (line 970)
- public static void AdjustBlightSlider(object popup, int sliderIndex, int delta) (line 995)

#### Seasonal effects
- public static bool IsSeasonalEffectsRandom(object popup) (line 1021)
- public static (int positive, int negative) GetSeasonalEffectsCounts(object popup) (line 1035)
- public static void ToggleSeasonalEffectsMode(object popup) (line 1052)
- public static void AdjustSeasonalEffectsPositive(object popup, int delta) (line 1072)
- public static List<SeasonalEffectInfo> GetAllSeasonalEffects(object popup) (line 1101)
  Returns all settings seasonal effects filtered to those in custom mode, sorted positive-first.
- public static void ToggleSeasonalEffect(object popup, SeasonalEffectInfo effect) (line 1200)
- public static int GetMaxSeasonalEffects() (line 1250)

#### Trade towns
- public static List<(string name, bool isSelected)> GetTradeTownSlots(object popup) (line 1270)
- public static void ToggleTradeTownSlot(object popup, int slotIndex) (line 1301)

#### Modifiers
- public static List<ModifierInfo> GetAllModifiers(object popup) (line 1347)
- public static void ToggleModifier(object popup, ModifierInfo modifier) (line 1405)

#### Embark goods
- public static List<(string name, int amount)> GetEmbarkGoods(object popup) (line 1434)
- public static void AdjustEmbarkGood(object popup, int slotIndex, int delta) (line 1467)

#### Embark effects
- public static List<(object effect, string displayName, bool isSelected)> GetEmbarkEffects(object popup) (line 1513)
- public static void ToggleEmbarkEffect(object popup, int effectIndex) (line 1546)

#### Action
- public static void TriggerEmbark(object popup) (line 1583)

#### Cache
- public static int LogCacheStatus() (line 1610)
