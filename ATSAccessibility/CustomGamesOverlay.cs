using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Custom Games (Training Expeditions) popup.
    /// Two-level navigation: Level 0 = top menu (13 sections), Level 1 = section items.
    /// </summary>
    public class CustomGamesOverlay : MenuBase, IKeyHandler
    {
        private enum SectionType
        {
            Difficulty,
            Seed,
            Biome,
            Races,
            Reputation,
            Seasons,
            SeasonalEffects,
            Blight,
            Modifiers,
            TradeTowns,
            EmbarkGoods,
            EmbarkEffects,
            Embark
        }

        private object _popup;

        // Top menu items
        private readonly List<SectionType> _sections = new List<SectionType>
        {
            SectionType.Difficulty,
            SectionType.Seed,
            SectionType.Biome,
            SectionType.Races,
            SectionType.Reputation,
            SectionType.Seasons,
            SectionType.SeasonalEffects,
            SectionType.Blight,
            SectionType.Modifiers,
            SectionType.TradeTowns,
            SectionType.EmbarkGoods,
            SectionType.EmbarkEffects,
            SectionType.Embark
        };

        // Cached data for current section
        private List<object> _difficulties = new List<object>();
        private List<(object biome, string name)> _biomes = new List<(object, string)>();
        private List<(object race, string name, bool selected)> _races = new List<(object, string, bool)>();
        private List<(string name, int index, int max, float value)> _sliders = new List<(string, int, int, float)>();
        private List<CustomGamesReflection.ModifierInfo> _modifiers = new List<CustomGamesReflection.ModifierInfo>();
        private List<CustomGamesReflection.ModifierInfo> _filteredModifiers = new List<CustomGamesReflection.ModifierInfo>();
        private List<CustomGamesReflection.SeasonalEffectInfo> _seasonalEffects = new List<CustomGamesReflection.SeasonalEffectInfo>();
        private List<(string name, bool selected)> _tradeTowns = new List<(string, bool)>();
        private List<(string name, int amount)> _embarkGoods = new List<(string, int)>();
        private List<(object effect, string name, bool selected)> _embarkEffects = new List<(object, string, bool)>();

        // Modifiers state
        private int _modifierCategoryIndex = 0;  // 0=WorldMap, 1=Daily, 2=Difficulty, 3=All
        private readonly string[] _categoryNames = { "World Map", "Daily", "Difficulty", "All" };

        // Seed text editing state
        private bool _isEditingSeed = false;
        private TMPro.TMP_InputField _seedInputField = null;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Training Expeditions";
        protected override string EmptyMessage => "";

        protected override void StorePopup(object popup)
        {
            _popup = popup;
        }

        protected override int GetItemCount()
        {
            if (Level == 0)
                return _sections.Count;
            return GetSectionItemCount(CurrentSection);
        }

        protected override string GetLabel(int index)
        {
            if (Level == 0)
                return GetTopMenuItemAnnouncement(_sections[index]);
            return GetSectionItemLabel(index);
        }

        protected override void RefreshData()
        {
        }

        protected override EnterAction OnEnter(int index)
        {
            return EnterAction.Action;
        }

        protected override void OnAction(int index)
        {
            if (Level == 0)
            {
                var section = _sections[index];

                if (section == SectionType.Embark)
                {
                    TriggerEmbark();
                    return;
                }

                if (section == SectionType.Seed)
                {
                    StartSeedEdit();
                    return;
                }

                EnterSection(section);
            }
            else
            {
                ActivateSectionItem();
            }
        }

        protected override void OnSpace(int index)
        {
            if (Level == 0)
            {
                var section = _sections[index];

                if (section == SectionType.Seed)
                {
                    RandomizeSeed();
                    return;
                }
                if (section == SectionType.Blight)
                {
                    ToggleBlightFromMenu();
                    return;
                }
                if (section == SectionType.SeasonalEffects)
                {
                    ToggleSeasonalEffectsModeFromMenu();
                    return;
                }
            }
            else
            {
                ToggleSectionItem();
            }
        }

        protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers)
        {
            if (Level != 1) return;

            var section = CurrentSection;
            int multiplier;
            if (modifiers.Shift)
                multiplier = section == SectionType.EmbarkGoods ? 10 : 5;
            else
                multiplier = 1;

            AdjustSlider(dir * multiplier);
        }

        protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (_isEditingSeed)
            {
                if (keyCode == KeyCode.Escape || keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
                {
                    ExitSeedEdit();
                    return true;
                }
                // Let the input field handle all other keys
                return false;
            }

            if (Level == 0 && keyCode == KeyCode.RightArrow)
            {
                var section = _sections[CurrentIndex];
                if (section != SectionType.Seed)
                {
                    OnAction(CurrentIndex);
                }
                return true;
            }

            if (Level == 1 && keyCode == KeyCode.Tab)
            {
                if (CurrentSection == SectionType.Modifiers)
                {
                    CycleModifierCategory(modifiers.Shift ? -1 : 1);
                }
                return true;
            }

            return null;
        }

        protected override EscapeAction OnEscape()
        {
            if (Level > 0)
                return EscapeAction.GoBack;
            // Pass to game to close popup
            return EscapeAction.PassThrough;
        }

        protected override void OnGoBack()
        {
            ClearCachedData();
        }

        protected override string GetOpenAnnouncement()
        {
            if (_sections.Count == 0) return OverlayName;
            string firstLabel = GetTopMenuItemAnnouncement(_sections[0]);
            return $"{OverlayName}. {firstLabel}";
        }

        protected override void OnClosed()
        {
            ClearCachedData();
            _popup = null;
        }

        // ========================================
        // SEARCH OVERRIDES
        // ========================================

        protected override int SearchItemCount
        {
            get
            {
                if (Level != 1) return 0;
                if (CurrentSection == SectionType.Modifiers) return _filteredModifiers.Count;
                return 0;
            }
        }

        protected override int SearchCurrentIndex => Level == 1 ? CurrentIndex : 0;

        protected override string GetSearchName(int index)
        {
            if (Level == 1 && CurrentSection == SectionType.Modifiers)
                return index >= 0 && index < _filteredModifiers.Count ? _filteredModifiers[index].DisplayName : null;
            return null;
        }

        // ========================================
        // HELPERS
        // ========================================

        private SectionType CurrentSection => _sections[_indices[0]];

        private string GetTopMenuItemAnnouncement(SectionType section)
        {
            switch (section)
            {
                case SectionType.Difficulty:
                    var diff = CustomGamesReflection.GetCurrentDifficulty(_popup);
                    string diffName = CustomGamesReflection.GetDifficultyDisplayName(diff);
                    return $"Difficulty: {diffName}";

                case SectionType.Seed:
                    string seed = CustomGamesReflection.GetSeed(_popup);
                    return $"Seed: {seed}. Space to randomize, Enter to edit";

                case SectionType.Biome:
                    var biomes = CustomGamesReflection.GetAvailableBiomes(_popup);
                    int biomeIdx = CustomGamesReflection.GetCurrentBiomeIndex(_popup);
                    string biomeName = biomeIdx >= 0 && biomeIdx < biomes.Count ? biomes[biomeIdx].displayName : "Unknown";
                    return $"Biome: {biomeName}";

                case SectionType.Races:
                    var races = CustomGamesReflection.GetRaceSlots(_popup);
                    int selectedCount = races.Count(r => r.isSelected);
                    return $"Races: {selectedCount} selected";

                case SectionType.Reputation:
                    return "Reputation Settings";

                case SectionType.Seasons:
                    return "Season Durations";

                case SectionType.SeasonalEffects:
                    bool isRandom = CustomGamesReflection.IsSeasonalEffectsRandom(_popup);
                    if (isRandom)
                    {
                        var counts = CustomGamesReflection.GetSeasonalEffectsCounts(_popup);
                        return $"Seasonal Effects: Random ({counts.positive} positive, {counts.negative} negative)";
                    }
                    return "Seasonal Effects: Manual";

                case SectionType.Blight:
                    bool blightOn = CustomGamesReflection.IsBlightEnabled(_popup);
                    return $"Blight: {(blightOn ? "Enabled" : "Disabled")}";

                case SectionType.Modifiers:
                    var mods = CustomGamesReflection.GetAllModifiers(_popup);
                    int pickedCount = mods.Count(m => m.IsPicked);
                    return $"Modifiers: {pickedCount} selected";

                case SectionType.TradeTowns:
                    var towns = CustomGamesReflection.GetTradeTownSlots(_popup);
                    int townsSelected = towns.Count(t => t.isSelected);
                    return $"Trade Towns: {townsSelected} selected";

                case SectionType.EmbarkGoods:
                    return "Embark Goods";

                case SectionType.EmbarkEffects:
                    var effects = CustomGamesReflection.GetEmbarkEffects(_popup);
                    int effectsSelected = effects.Count(e => e.isSelected);
                    return $"Embark Effects: {effectsSelected} selected";

                case SectionType.Embark:
                    return "Embark";

                default:
                    return section.ToString();
            }
        }

        private string GetSectionItemLabel(int index)
        {
            if (_popup == null) return null;

            var section = CurrentSection;

            switch (section)
            {
                case SectionType.Difficulty:
                    if (index < _difficulties.Count)
                    {
                        var diff = _difficulties[index];
                        string name = CustomGamesReflection.GetDifficultyDisplayName(diff);
                        var current = CustomGamesReflection.GetCurrentDifficulty(_popup);
                        bool isCurrent = CustomGamesReflection.GetDifficultyIndex(diff) ==
                                        CustomGamesReflection.GetDifficultyIndex(current);
                        return isCurrent ? $"{name}, current" : name;
                    }
                    return null;

                case SectionType.Biome:
                    if (index < _biomes.Count)
                    {
                        var biome = _biomes[index];
                        int currentIdx = CustomGamesReflection.GetCurrentBiomeIndex(_popup);
                        bool isCurrent = index == currentIdx;
                        return isCurrent ? $"{biome.name}, current" : biome.name;
                    }
                    return null;

                case SectionType.Races:
                    if (index < _races.Count)
                    {
                        var race = _races[index];
                        return race.selected ? $"{race.name}, selected" : $"{race.name}, not selected";
                    }
                    return null;

                case SectionType.Reputation:
                case SectionType.Seasons:
                case SectionType.Blight:
                    if (index < _sliders.Count)
                    {
                        var slider = _sliders[index];
                        return $"{slider.name}: {slider.value:F1}";
                    }
                    return null;

                case SectionType.TradeTowns:
                    if (index < _tradeTowns.Count)
                    {
                        var town = _tradeTowns[index];
                        return town.selected ? $"{town.name}, selected" : $"{town.name}, not selected";
                    }
                    return null;

                case SectionType.Modifiers:
                    if (index < _filteredModifiers.Count)
                    {
                        var mod = _filteredModifiers[index];
                        string polarity = mod.IsPositive ? "positive" : "negative";
                        string status = mod.IsPicked ? "enabled" : "disabled";
                        string label = $"{mod.DisplayName}, {polarity}, {status}";
                        if (!string.IsNullOrEmpty(mod.Description))
                        {
                            label += $". {mod.Description}";
                        }
                        return label;
                    }
                    return null;

                case SectionType.EmbarkGoods:
                    if (index < _embarkGoods.Count)
                    {
                        var good = _embarkGoods[index];
                        return $"{good.amount} {good.name}";
                    }
                    return null;

                case SectionType.EmbarkEffects:
                    if (index < _embarkEffects.Count)
                    {
                        var effect = _embarkEffects[index];
                        return effect.selected ? $"{effect.name}, selected" : $"{effect.name}, not selected";
                    }
                    return null;

                case SectionType.Seed:
                    return "Press Space to randomize seed";

                case SectionType.SeasonalEffects:
                    if (CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                    {
                        var seasonalCounts = CustomGamesReflection.GetSeasonalEffectsCounts(_popup);
                        return $"Positive effects: {seasonalCounts.positive}, Negative effects: {seasonalCounts.negative}";
                    }
                    if (index < _seasonalEffects.Count)
                    {
                        var effect = _seasonalEffects[index];
                        string polarity = effect.IsPositive ? "positive" : "negative";
                        string status = effect.IsPicked ? "selected" : "not selected";
                        string desc = !string.IsNullOrEmpty(effect.Description) ? $". {effect.Description}" : "";
                        return $"{effect.DisplayName}, {polarity}, {status}{desc}";
                    }
                    return null;

                default:
                    return null;
            }
        }

        // ========================================
        // Section Entry/Exit
        // ========================================

        private void EnterSection(SectionType section)
        {
            if (section == SectionType.Blight && !CustomGamesReflection.IsBlightEnabled(_popup))
            {
                Speech.Say("Blight is disabled. Press Space to enable.");
                return;
            }

            LoadSectionData(section);

            if (GetSectionItemCount(section) == 0)
            {
                Speech.Say("No items");
                return;
            }

            SetLevel(1);
            CurrentIndex = 0;
            _search.Clear();
            AnnounceCurrentItem();
        }

        // ========================================
        // Seed Editing
        // ========================================

        private void StartSeedEdit()
        {
            var inputField = CustomGamesReflection.GetSeedInputField(_popup);
            if (inputField == null)
            {
                SoundManager.PlayFailed();
                Speech.Say("Cannot edit seed");
                return;
            }

            _isEditingSeed = true;
            _seedInputField = inputField;

            InputBlocker.IsBlocking = false;

            inputField.Select();
            inputField.ActivateInputField();

            string currentSeed = inputField.text;
            Speech.Say($"Editing seed: {currentSeed}. Press Enter or Escape when done");
        }

        private void ExitSeedEdit()
        {
            if (!_isEditingSeed) return;

            _isEditingSeed = false;

            InputBlocker.IsBlocking = true;

            if (_seedInputField != null)
            {
                _seedInputField.DeactivateInputField();
            }
            _seedInputField = null;

            string newSeed = CustomGamesReflection.GetSeed(_popup);
            Speech.Say($"Seed set to: {newSeed}");
        }

        private void RandomizeSeed()
        {
            if (CustomGamesReflection.RandomizeSeed(_popup))
            {
                SoundManager.PlayButtonClick();
                string newSeed = CustomGamesReflection.GetSeed(_popup);
                Speech.Say($"Seed: {newSeed}");
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // Section Data
        // ========================================

        private void LoadSectionData(SectionType section)
        {
            ClearCachedData();

            switch (section)
            {
                case SectionType.Difficulty:
                    _difficulties = CustomGamesReflection.GetAvailableDifficulties(_popup);
                    break;

                case SectionType.Biome:
                    _biomes = CustomGamesReflection.GetAvailableBiomes(_popup);
                    break;

                case SectionType.Races:
                    _races = CustomGamesReflection.GetRaceSlots(_popup);
                    break;

                case SectionType.Reputation:
                    _sliders = CustomGamesReflection.GetReputationSliders(_popup);
                    break;

                case SectionType.Seasons:
                    _sliders = CustomGamesReflection.GetSeasonsSliders(_popup);
                    break;

                case SectionType.Blight:
                    _sliders = CustomGamesReflection.GetBlightSliders(_popup);
                    break;

                case SectionType.Modifiers:
                    _modifiers = CustomGamesReflection.GetAllModifiers(_popup);
                    FilterModifiers();
                    break;

                case SectionType.TradeTowns:
                    _tradeTowns = CustomGamesReflection.GetTradeTownSlots(_popup);
                    break;

                case SectionType.SeasonalEffects:
                    if (!CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                    {
                        _seasonalEffects = CustomGamesReflection.GetAllSeasonalEffects(_popup);
                    }
                    break;

                case SectionType.EmbarkGoods:
                    _embarkGoods = CustomGamesReflection.GetEmbarkGoods(_popup);
                    break;

                case SectionType.EmbarkEffects:
                    _embarkEffects = CustomGamesReflection.GetEmbarkEffects(_popup);
                    break;
            }
        }

        private void ClearCachedData()
        {
            _difficulties.Clear();
            _biomes.Clear();
            _races.Clear();
            _sliders.Clear();
            _modifiers.Clear();
            _filteredModifiers.Clear();
            _seasonalEffects.Clear();
            _tradeTowns.Clear();
            _embarkGoods.Clear();
            _embarkEffects.Clear();
        }

        private int GetSectionItemCount(SectionType section)
        {
            if (_popup == null) return 0;

            switch (section)
            {
                case SectionType.Difficulty:
                    return _difficulties.Count;
                case SectionType.Biome:
                    return _biomes.Count;
                case SectionType.Races:
                    return _races.Count;
                case SectionType.Reputation:
                case SectionType.Seasons:
                case SectionType.Blight:
                    return _sliders.Count;
                case SectionType.Modifiers:
                    return _filteredModifiers.Count;
                case SectionType.TradeTowns:
                    return _tradeTowns.Count;
                case SectionType.EmbarkGoods:
                    return _embarkGoods.Count;
                case SectionType.EmbarkEffects:
                    return _embarkEffects.Count;
                case SectionType.SeasonalEffects:
                    if (CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                        return 1;
                    return _seasonalEffects.Count;
                case SectionType.Seed:
                    return 1;
                default:
                    return 0;
            }
        }

        // ========================================
        // Section Actions
        // ========================================

        private void ActivateSectionItem()
        {
            var section = CurrentSection;

            switch (section)
            {
                case SectionType.Difficulty:
                    SelectDifficulty();
                    break;

                case SectionType.Biome:
                    SelectBiome();
                    break;

                case SectionType.Races:
                case SectionType.Modifiers:
                case SectionType.TradeTowns:
                case SectionType.EmbarkEffects:
                    ToggleSectionItem();
                    break;

                case SectionType.SeasonalEffects:
                    if (!CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                    {
                        ToggleSectionItem();
                    }
                    break;

                default:
                    AnnounceCurrentItem();
                    break;
            }
        }

        private void ToggleSectionItem()
        {
            var section = CurrentSection;

            switch (section)
            {
                case SectionType.Races:
                    ToggleRace();
                    break;

                case SectionType.Modifiers:
                    ToggleModifier();
                    break;

                case SectionType.TradeTowns:
                    ToggleTradeTown();
                    break;

                case SectionType.SeasonalEffects:
                    ToggleSeasonalEffect();
                    break;

                case SectionType.EmbarkEffects:
                    ToggleEmbarkEffect();
                    break;

                case SectionType.Blight:
                    ToggleBlight();
                    break;

                default:
                    AnnounceCurrentItem();
                    break;
            }
        }

        private void SelectDifficulty()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _difficulties.Count) return;

            var diff = _difficulties[CurrentIndex];
            if (CustomGamesReflection.SetDifficulty(_popup, diff))
            {
                SoundManager.PlayButtonClick();
                string name = CustomGamesReflection.GetDifficultyDisplayName(diff);
                Speech.Say($"Selected {name}");
                GoBackToTopMenu();
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not select difficulty");
            }
        }

        private void SelectBiome()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _biomes.Count) return;

            if (CustomGamesReflection.SetBiomeIndex(_popup, CurrentIndex))
            {
                SoundManager.PlayButtonClick();
                var biome = _biomes[CurrentIndex];
                Speech.Say($"Selected {biome.name}");
                GoBackToTopMenu();
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not select biome");
            }
        }

        private void ToggleRace()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _races.Count) return;

            if (CustomGamesReflection.ToggleRaceSlot(_popup, CurrentIndex))
            {
                SoundManager.PlayButtonClick();
                _races = CustomGamesReflection.GetRaceSlots(_popup);
                if (CurrentIndex < _races.Count)
                {
                    var race = _races[CurrentIndex];
                    Speech.Say(race.selected ? $"{race.name}, selected" : $"{race.name}, not selected");
                }
            }
            else
            {
                SoundManager.PlayFailed();
                if (CurrentIndex < _races.Count && !_races[CurrentIndex].selected)
                {
                    Speech.Say("Maximum races selected");
                }
            }
        }

        private void ToggleTradeTown()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _tradeTowns.Count) return;

            if (CustomGamesReflection.ToggleTradeTownSlot(_popup, CurrentIndex))
            {
                SoundManager.PlayButtonClick();
                _tradeTowns = CustomGamesReflection.GetTradeTownSlots(_popup);
                if (CurrentIndex < _tradeTowns.Count)
                {
                    var town = _tradeTowns[CurrentIndex];
                    Speech.Say(town.selected ? $"{town.name}, selected" : $"{town.name}, not selected");
                }
            }
            else
            {
                SoundManager.PlayFailed();
                if (CurrentIndex < _tradeTowns.Count && !_tradeTowns[CurrentIndex].selected)
                {
                    Speech.Say("Maximum trade towns selected");
                }
            }
        }

        private void ToggleSeasonalEffect()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _seasonalEffects.Count) return;

            var effect = _seasonalEffects[CurrentIndex];
            int maxEffects = CustomGamesReflection.GetMaxSeasonalEffects();
            int currentPicked = _seasonalEffects.Count(e => e.IsPicked);

            if (!effect.IsPicked && currentPicked >= maxEffects)
            {
                SoundManager.PlayFailed();
                Speech.Say($"Maximum {maxEffects} effects selected");
                return;
            }

            if (CustomGamesReflection.ToggleSeasonalEffect(_popup, effect))
            {
                SoundManager.PlayButtonClick();
                effect.IsPicked = !effect.IsPicked;
                string polarity = effect.IsPositive ? "positive" : "negative";
                string status = effect.IsPicked ? "selected" : "not selected";
                Speech.Say($"{effect.DisplayName}, {polarity}, {status}");
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        private void ToggleModifier()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _filteredModifiers.Count) return;

            var mod = _filteredModifiers[CurrentIndex];
            if (CustomGamesReflection.ToggleModifier(_popup, mod))
            {
                SoundManager.PlayButtonClick();
                string status = mod.IsPicked ? "enabled" : "disabled";
                Speech.Say($"{mod.DisplayName}, {status}");
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        private void ToggleEmbarkEffect()
        {
            if (_popup == null) return;
            if (CurrentIndex < 0 || CurrentIndex >= _embarkEffects.Count) return;

            if (CustomGamesReflection.ToggleEmbarkEffect(_popup, CurrentIndex))
            {
                SoundManager.PlayButtonClick();
                _embarkEffects = CustomGamesReflection.GetEmbarkEffects(_popup);
                if (CurrentIndex < _embarkEffects.Count)
                {
                    var effect = _embarkEffects[CurrentIndex];
                    Speech.Say(effect.selected ? $"{effect.name}, selected" : $"{effect.name}, not selected");
                }
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        private void ToggleBlight()
        {
            if (_popup == null) return;

            if (CustomGamesReflection.ToggleBlight(_popup))
            {
                SoundManager.PlayButtonClick();
                bool isOn = CustomGamesReflection.IsBlightEnabled(_popup);
                Speech.Say(isOn ? "Blight enabled" : "Blight disabled");

                if (isOn)
                {
                    _sliders = CustomGamesReflection.GetBlightSliders(_popup);
                }
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        private void ToggleBlightFromMenu()
        {
            if (_popup == null) return;

            if (CustomGamesReflection.ToggleBlight(_popup))
            {
                SoundManager.PlayButtonClick();
                AnnounceCurrentItem();
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        private void ToggleSeasonalEffectsModeFromMenu()
        {
            if (_popup == null) return;

            if (CustomGamesReflection.ToggleSeasonalEffectsMode(_popup))
            {
                SoundManager.PlayButtonClick();
                AnnounceCurrentItem();
            }
            else
            {
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // Slider Adjustment
        // ========================================

        private void AdjustSlider(int delta)
        {
            if (_popup == null) return;

            var section = CurrentSection;

            if (section == SectionType.SeasonalEffects)
            {
                if (CustomGamesReflection.AdjustSeasonalEffectsPositive(_popup, delta))
                {
                    SoundManager.PlayButtonClick();
                    AnnounceCurrentItem();
                }
                return;
            }

            if (section == SectionType.EmbarkGoods)
            {
                if (CustomGamesReflection.AdjustEmbarkGood(_popup, CurrentIndex, delta))
                {
                    SoundManager.PlayButtonClick();
                    _embarkGoods = CustomGamesReflection.GetEmbarkGoods(_popup);
                    AnnounceCurrentItem();
                }
                return;
            }

            if (section != SectionType.Reputation && section != SectionType.Seasons && section != SectionType.Blight)
            {
                return;
            }

            if (CurrentIndex < 0 || CurrentIndex >= _sliders.Count) return;

            bool success = false;
            switch (section)
            {
                case SectionType.Reputation:
                    success = CustomGamesReflection.AdjustReputationSlider(_popup, CurrentIndex, delta);
                    break;
                case SectionType.Seasons:
                    success = CustomGamesReflection.AdjustSeasonsSlider(_popup, CurrentIndex, delta);
                    break;
                case SectionType.Blight:
                    success = CustomGamesReflection.AdjustBlightSlider(_popup, CurrentIndex, delta);
                    break;
            }

            if (success)
            {
                SoundManager.PlayButtonClick();
                switch (section)
                {
                    case SectionType.Reputation:
                        _sliders = CustomGamesReflection.GetReputationSliders(_popup);
                        break;
                    case SectionType.Seasons:
                        _sliders = CustomGamesReflection.GetSeasonsSliders(_popup);
                        break;
                    case SectionType.Blight:
                        _sliders = CustomGamesReflection.GetBlightSliders(_popup);
                        break;
                }

                AnnounceCurrentItem();
            }
        }

        // ========================================
        // Modifiers Category
        // ========================================

        private void CycleModifierCategory(int direction)
        {
            _modifierCategoryIndex = NavigationUtils.WrapIndex(_modifierCategoryIndex, direction, _categoryNames.Length);
            FilterModifiers();
            CurrentIndex = 0;

            string categoryName = _categoryNames[_modifierCategoryIndex];
            int count = _filteredModifiers.Count;
            Speech.Say($"{categoryName} ({count} modifiers)");

            if (count > 0)
            {
                AnnounceCurrentItem();
            }
        }

        private void FilterModifiers()
        {
            _filteredModifiers.Clear();

            if (_modifierCategoryIndex == 3)  // All
            {
                _filteredModifiers.AddRange(_modifiers);
            }
            else
            {
                var targetType = (CustomGamesReflection.ModifierType)_modifierCategoryIndex;
                _filteredModifiers.AddRange(_modifiers.Where(m => m.Type == targetType));
            }
        }

        // ========================================
        // GoBack Helper
        // ========================================

        private void GoBackToTopMenu()
        {
            ClearCachedData();
            SetLevel(0);
            _search.Clear();
            AnnounceCurrentItem();
        }

        // ========================================
        // Embark
        // ========================================

        private void TriggerEmbark()
        {
            if (CustomGamesReflection.TriggerEmbark(_popup))
            {
                SoundManager.PlayButtonClick();
                Speech.Say("Embarking");
                Debug.Log("[ATSAccessibility] Embark triggered from CustomGamesOverlay");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not embark");
            }
        }
    }
}
