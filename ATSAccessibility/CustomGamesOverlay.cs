using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Custom Games (Training Expeditions) popup.
    /// Provides hierarchical navigation through configuration panels.
    /// </summary>
    public class CustomGamesOverlay : IKeyHandler
    {
        private enum MenuLevel
        {
            TopMenu,
            InSection
        }

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

        // Navigation state
        private bool _isOpen;
        private object _popup;
        private MenuLevel _menuLevel = MenuLevel.TopMenu;
        private int _topMenuIndex;
        private int _sectionItemIndex;

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

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Seed text editing state
        private bool _isEditingSeed = false;
        private TMPro.TMP_InputField _seedInputField = null;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // If editing seed, handle escape to exit edit mode
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

            if (_menuLevel == MenuLevel.InSection)
            {
                return ProcessSectionKey(keyCode, modifiers);
            }

            return ProcessTopMenuKey(keyCode, modifiers);
        }

        private bool ProcessTopMenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            var currentSection = _sections[_topMenuIndex];

            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateTopMenu(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateTopMenu(-1);
                    return true;

                case KeyCode.Home:
                    _topMenuIndex = 0;
                    AnnounceTopMenuItem();
                    return true;

                case KeyCode.End:
                    _topMenuIndex = _sections.Count - 1;
                    AnnounceTopMenuItem();
                    return true;

                case KeyCode.Space:
                    // Space randomizes seed when on Seed option
                    if (currentSection == SectionType.Seed)
                    {
                        RandomizeSeed();
                        return true;
                    }
                    // Space toggles blight on/off from main menu
                    if (currentSection == SectionType.Blight)
                    {
                        ToggleBlightFromMenu();
                        return true;
                    }
                    // Space toggles seasonal effects random/manual from main menu
                    if (currentSection == SectionType.SeasonalEffects)
                    {
                        ToggleSeasonalEffectsModeFromMenu();
                        return true;
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    // Enter on Seed starts text editing
                    if (currentSection == SectionType.Seed)
                    {
                        StartSeedEdit();
                        return true;
                    }
                    EnterSection();
                    return true;

                case KeyCode.RightArrow:
                    // Right arrow doesn't activate seed edit, just enters sections
                    if (currentSection != SectionType.Seed)
                    {
                        EnterSection();
                    }
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        private bool ProcessSectionKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            var currentSection = _sections[_topMenuIndex];

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateSectionItem(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateSectionItem(-1);
                    return true;

                case KeyCode.Home:
                    _sectionItemIndex = 0;
                    AnnounceSectionItem();
                    return true;

                case KeyCode.End:
                    {
                        var section = _sections[_topMenuIndex];
                        int count = GetSectionItemCount(section);
                        if (count > 0)
                        {
                            _sectionItemIndex = count - 1;
                            AnnounceSectionItem();
                        }
                    }
                    return true;

                case KeyCode.LeftArrow:
                case KeyCode.Escape:
                    ExitSection();
                    InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateSectionItem();
                    return true;

                case KeyCode.Space:
                    ToggleSectionItem();
                    return true;

                case KeyCode.Tab:
                    // Tab cycles categories in Modifiers
                    if (currentSection == SectionType.Modifiers)
                    {
                        CycleModifierCategory(modifiers.Shift ? -1 : 1);
                        return true;
                    }
                    return true;

                case KeyCode.Plus:
                case KeyCode.KeypadPlus:
                case KeyCode.Equals:
                    // Embark goods uses 10 for shift, others use 5
                    {
                        var section = _sections[_topMenuIndex];
                        int increment = modifiers.Shift ? (section == SectionType.EmbarkGoods ? 10 : 5) : 1;
                        AdjustSlider(increment);
                    }
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    // Embark goods uses 10 for shift, others use 5
                    {
                        var section = _sections[_topMenuIndex];
                        int decrement = modifiers.Shift ? (section == SectionType.EmbarkGoods ? 10 : 5) : 1;
                        AdjustSlider(-decrement);
                    }
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                    {
                        HandleBackspace();
                        return true;
                    }
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while in section
                    return true;
            }
        }

        // ========================================
        // Public Methods
        // ========================================

        public void Open(object popup)
        {
            if (_isOpen) return;

            _popup = popup;
            _isOpen = true;
            _topMenuIndex = 0;
            _menuLevel = MenuLevel.TopMenu;
            _search.Clear();

            Speech.Say("Training Expeditions");
            AnnounceTopMenuItem();

            Debug.Log("[ATSAccessibility] CustomGamesOverlay opened");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _menuLevel = MenuLevel.TopMenu;
            _topMenuIndex = 0;
            _sectionItemIndex = 0;

            ClearCachedData();

            Debug.Log("[ATSAccessibility] CustomGamesOverlay closed");
        }

        // ========================================
        // Top Menu Navigation
        // ========================================

        private void NavigateTopMenu(int direction)
        {
            _topMenuIndex = NavigationUtils.WrapIndex(_topMenuIndex, direction, _sections.Count);
            AnnounceTopMenuItem();
        }

        private void AnnounceTopMenuItem()
        {
            var section = _sections[_topMenuIndex];
            string announcement = GetTopMenuItemAnnouncement(section);
            Speech.Say(announcement);
        }

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

        // ========================================
        // Section Entry/Exit
        // ========================================

        private void EnterSection()
        {
            var section = _sections[_topMenuIndex];

            // Handle Embark action directly
            if (section == SectionType.Embark)
            {
                TriggerEmbark();
                return;
            }

            // Seed is handled at top menu level (Space to randomize, Enter to edit)
            // Don't enter it as a section
            if (section == SectionType.Seed)
            {
                return;
            }

            // Blight submenu only available when blight is enabled
            if (section == SectionType.Blight && !CustomGamesReflection.IsBlightEnabled(_popup))
            {
                Speech.Say("Blight is disabled. Press Space to enable.");
                return;
            }

            // Load section data
            LoadSectionData(section);

            if (GetSectionItemCount(section) == 0)
            {
                Speech.Say("No items");
                return;
            }

            _menuLevel = MenuLevel.InSection;
            _sectionItemIndex = 0;
            _search.Clear();

            AnnounceSectionItem();
        }

        private void ExitSection()
        {
            _menuLevel = MenuLevel.TopMenu;
            _search.Clear();
            ClearCachedData();

            AnnounceTopMenuItem();
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

            // Disable input blocker so typing works
            InputBlocker.IsBlocking = false;

            // Focus the input field
            inputField.Select();
            inputField.ActivateInputField();

            string currentSeed = inputField.text;
            Speech.Say($"Editing seed: {currentSeed}. Press Enter or Escape when done");
        }

        private void ExitSeedEdit()
        {
            if (!_isEditingSeed) return;

            _isEditingSeed = false;

            // Re-enable input blocker
            InputBlocker.IsBlocking = true;

            // Deactivate input field
            if (_seedInputField != null)
            {
                _seedInputField.DeactivateInputField();
            }
            _seedInputField = null;

            // Announce the new seed value
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
        // Section Navigation
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
                    // In manual mode, load effects list; in random mode, just show slider
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
            _search.Clear();
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
                    // In random mode, just show slider info; in manual mode, show effects list
                    if (CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                        return 1;
                    return _seasonalEffects.Count;
                case SectionType.Seed:
                    return 1;  // Just randomize option
                default:
                    return 0;
            }
        }

        private void NavigateSectionItem(int direction)
        {
            var section = _sections[_topMenuIndex];
            int count = GetSectionItemCount(section);
            if (count == 0) return;

            _sectionItemIndex = NavigationUtils.WrapIndex(_sectionItemIndex, direction, count);
            AnnounceSectionItem();
        }

        private void AnnounceSectionItem()
        {
            if (_popup == null) return;

            var section = _sections[_topMenuIndex];
            string announcement = "";

            switch (section)
            {
                case SectionType.Difficulty:
                    if (_sectionItemIndex < _difficulties.Count)
                    {
                        var diff = _difficulties[_sectionItemIndex];
                        string name = CustomGamesReflection.GetDifficultyDisplayName(diff);
                        var current = CustomGamesReflection.GetCurrentDifficulty(_popup);
                        bool isCurrent = CustomGamesReflection.GetDifficultyIndex(diff) ==
                                        CustomGamesReflection.GetDifficultyIndex(current);
                        announcement = isCurrent ? $"{name}, current" : name;
                    }
                    break;

                case SectionType.Biome:
                    if (_sectionItemIndex < _biomes.Count)
                    {
                        var biome = _biomes[_sectionItemIndex];
                        int currentIdx = CustomGamesReflection.GetCurrentBiomeIndex(_popup);
                        bool isCurrent = _sectionItemIndex == currentIdx;
                        announcement = isCurrent ? $"{biome.name}, current" : biome.name;
                    }
                    break;

                case SectionType.Races:
                    if (_sectionItemIndex < _races.Count)
                    {
                        var race = _races[_sectionItemIndex];
                        announcement = race.selected ? $"{race.name}, selected" : $"{race.name}, not selected";
                    }
                    break;

                case SectionType.Reputation:
                case SectionType.Seasons:
                case SectionType.Blight:
                    if (_sectionItemIndex < _sliders.Count)
                    {
                        var slider = _sliders[_sectionItemIndex];
                        announcement = $"{slider.name}: {slider.value:F1}";
                    }
                    break;

                case SectionType.TradeTowns:
                    if (_sectionItemIndex < _tradeTowns.Count)
                    {
                        var town = _tradeTowns[_sectionItemIndex];
                        announcement = town.selected ? $"{town.name}, selected" : $"{town.name}, not selected";
                    }
                    break;

                case SectionType.Modifiers:
                    if (_sectionItemIndex < _filteredModifiers.Count)
                    {
                        var mod = _filteredModifiers[_sectionItemIndex];
                        string polarity = mod.IsPositive ? "positive" : "negative";
                        string status = mod.IsPicked ? "enabled" : "disabled";
                        announcement = $"{mod.DisplayName}, {polarity}, {status}";
                        if (!string.IsNullOrEmpty(mod.Description))
                        {
                            announcement += $". {mod.Description}";
                        }
                    }
                    break;

                case SectionType.EmbarkGoods:
                    if (_sectionItemIndex < _embarkGoods.Count)
                    {
                        var good = _embarkGoods[_sectionItemIndex];
                        announcement = $"{good.amount} {good.name}";
                    }
                    break;

                case SectionType.EmbarkEffects:
                    if (_sectionItemIndex < _embarkEffects.Count)
                    {
                        var effect = _embarkEffects[_sectionItemIndex];
                        announcement = effect.selected ? $"{effect.name}, selected" : $"{effect.name}, not selected";
                    }
                    break;

                case SectionType.Seed:
                    announcement = "Press Space to randomize seed";
                    break;

                case SectionType.SeasonalEffects:
                    if (CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                    {
                        var seasonalCounts = CustomGamesReflection.GetSeasonalEffectsCounts(_popup);
                        announcement = $"Positive effects: {seasonalCounts.positive}, Negative effects: {seasonalCounts.negative}";
                    }
                    else if (_sectionItemIndex < _seasonalEffects.Count)
                    {
                        var effect = _seasonalEffects[_sectionItemIndex];
                        string polarity = effect.IsPositive ? "positive" : "negative";
                        string status = effect.IsPicked ? "selected" : "not selected";
                        string desc = !string.IsNullOrEmpty(effect.Description) ? $". {effect.Description}" : "";
                        announcement = $"{effect.DisplayName}, {polarity}, {status}{desc}";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(announcement))
            {
                Speech.Say(announcement);
            }
        }

        // ========================================
        // Section Actions
        // ========================================

        private void ActivateSectionItem()
        {
            var section = _sections[_topMenuIndex];

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
                    // Only toggle in manual mode
                    if (!CustomGamesReflection.IsSeasonalEffectsRandom(_popup))
                    {
                        ToggleSectionItem();
                    }
                    break;

                default:
                    // Re-announce current item
                    AnnounceSectionItem();
                    break;
            }
        }

        private void ToggleSectionItem()
        {
            var section = _sections[_topMenuIndex];

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
                    // Re-announce
                    AnnounceSectionItem();
                    break;
            }
        }

        private void SelectDifficulty()
        {
            if (_popup == null) return;
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _difficulties.Count) return;

            var diff = _difficulties[_sectionItemIndex];
            if (CustomGamesReflection.SetDifficulty(_popup, diff))
            {
                SoundManager.PlayButtonClick();
                string name = CustomGamesReflection.GetDifficultyDisplayName(diff);
                Speech.Say($"Selected {name}");
                ExitSection();
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
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _biomes.Count) return;

            if (CustomGamesReflection.SetBiomeIndex(_popup, _sectionItemIndex))
            {
                SoundManager.PlayButtonClick();
                var biome = _biomes[_sectionItemIndex];
                Speech.Say($"Selected {biome.name}");
                ExitSection();
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
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _races.Count) return;

            if (CustomGamesReflection.ToggleRaceSlot(_popup, _sectionItemIndex))
            {
                SoundManager.PlayButtonClick();
                // Refresh and re-announce
                _races = CustomGamesReflection.GetRaceSlots(_popup);
                if (_sectionItemIndex < _races.Count)
                {
                    var race = _races[_sectionItemIndex];
                    Speech.Say(race.selected ? $"{race.name}, selected" : $"{race.name}, not selected");
                }
            }
            else
            {
                SoundManager.PlayFailed();
                // If the race wasn't already selected, we hit the max limit
                if (_sectionItemIndex < _races.Count && !_races[_sectionItemIndex].selected)
                {
                    Speech.Say("Maximum races selected");
                }
            }
        }

        private void ToggleTradeTown()
        {
            if (_popup == null) return;
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _tradeTowns.Count) return;

            if (CustomGamesReflection.ToggleTradeTownSlot(_popup, _sectionItemIndex))
            {
                SoundManager.PlayButtonClick();
                // Refresh and re-announce
                _tradeTowns = CustomGamesReflection.GetTradeTownSlots(_popup);
                if (_sectionItemIndex < _tradeTowns.Count)
                {
                    var town = _tradeTowns[_sectionItemIndex];
                    Speech.Say(town.selected ? $"{town.name}, selected" : $"{town.name}, not selected");
                }
            }
            else
            {
                SoundManager.PlayFailed();
                // If the town wasn't already selected, we hit the max limit
                if (_sectionItemIndex < _tradeTowns.Count && !_tradeTowns[_sectionItemIndex].selected)
                {
                    Speech.Say("Maximum trade towns selected");
                }
            }
        }

        private void ToggleSeasonalEffect()
        {
            if (_popup == null) return;
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _seasonalEffects.Count) return;

            var effect = _seasonalEffects[_sectionItemIndex];
            int maxEffects = CustomGamesReflection.GetMaxSeasonalEffects();
            int currentPicked = _seasonalEffects.Count(e => e.IsPicked);

            // Check max limit if trying to select
            if (!effect.IsPicked && currentPicked >= maxEffects)
            {
                SoundManager.PlayFailed();
                Speech.Say($"Maximum {maxEffects} effects selected");
                return;
            }

            if (CustomGamesReflection.ToggleSeasonalEffect(_popup, effect))
            {
                SoundManager.PlayButtonClick();
                // Refresh the effect's IsPicked status
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
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _filteredModifiers.Count) return;

            var mod = _filteredModifiers[_sectionItemIndex];
            if (CustomGamesReflection.ToggleModifier(_popup, mod))
            {
                SoundManager.PlayButtonClick();
                // mod.IsPicked is already updated by ToggleModifier
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
            if (_sectionItemIndex < 0 || _sectionItemIndex >= _embarkEffects.Count) return;

            if (CustomGamesReflection.ToggleEmbarkEffect(_popup, _sectionItemIndex))
            {
                SoundManager.PlayButtonClick();
                // Refresh and re-announce
                _embarkEffects = CustomGamesReflection.GetEmbarkEffects(_popup);
                if (_sectionItemIndex < _embarkEffects.Count)
                {
                    var effect = _embarkEffects[_sectionItemIndex];
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

                // Refresh sliders if now enabled
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
                // Re-announce with new state
                AnnounceTopMenuItem();
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
                // Re-announce with new state
                AnnounceTopMenuItem();
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

            var section = _sections[_topMenuIndex];

            // Handle SeasonalEffects separately (single slider, not in _sliders list)
            if (section == SectionType.SeasonalEffects)
            {
                if (CustomGamesReflection.AdjustSeasonalEffectsPositive(_popup, delta))
                {
                    SoundManager.PlayButtonClick();
                    AnnounceSectionItem();
                }
                return;
            }

            // Handle EmbarkGoods - adjust amounts with +/-
            if (section == SectionType.EmbarkGoods)
            {
                if (CustomGamesReflection.AdjustEmbarkGood(_popup, _sectionItemIndex, delta))
                {
                    SoundManager.PlayButtonClick();
                    // Refresh and re-announce
                    _embarkGoods = CustomGamesReflection.GetEmbarkGoods(_popup);
                    AnnounceSectionItem();
                }
                return;
            }

            if (section != SectionType.Reputation && section != SectionType.Seasons && section != SectionType.Blight)
            {
                return;
            }

            if (_sectionItemIndex < 0 || _sectionItemIndex >= _sliders.Count) return;

            bool success = false;
            switch (section)
            {
                case SectionType.Reputation:
                    success = CustomGamesReflection.AdjustReputationSlider(_popup, _sectionItemIndex, delta);
                    break;
                case SectionType.Seasons:
                    success = CustomGamesReflection.AdjustSeasonsSlider(_popup, _sectionItemIndex, delta);
                    break;
                case SectionType.Blight:
                    success = CustomGamesReflection.AdjustBlightSlider(_popup, _sectionItemIndex, delta);
                    break;
            }

            if (success)
            {
                SoundManager.PlayButtonClick();
                // Refresh slider data
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

                AnnounceSectionItem();
            }
        }

        // ========================================
        // Modifiers Category
        // ========================================

        private void CycleModifierCategory(int direction)
        {
            _modifierCategoryIndex = NavigationUtils.WrapIndex(_modifierCategoryIndex, direction, _categoryNames.Length);
            FilterModifiers();
            _sectionItemIndex = 0;

            string categoryName = _categoryNames[_modifierCategoryIndex];
            int count = _filteredModifiers.Count;
            Speech.Say($"{categoryName} ({count} modifiers)");

            if (count > 0)
            {
                AnnounceSectionItem();
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

            // Apply search filter if active
            if (_search.HasBuffer)
            {
                string filter = _search.Buffer.ToLowerInvariant();
                _filteredModifiers = _filteredModifiers
                    .Where(m => m.DisplayName.ToLowerInvariant().Contains(filter))
                    .ToList();
            }
        }

        // ========================================
        // Type-ahead Search
        // ========================================

        private void HandleSearchKey(char c)
        {
            var section = _sections[_topMenuIndex];

            // Only search in Modifiers section
            if (section != SectionType.Modifiers) return;

            _search.AddChar(c);
            FilterModifiers();

            if (_filteredModifiers.Count > 0)
            {
                _sectionItemIndex = 0;
                Speech.Say($"{_filteredModifiers.Count} matches for {_search.Buffer}");
                AnnounceSectionItem();
            }
            else
            {
                Speech.Say($"No matches for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            var section = _sections[_topMenuIndex];
            if (section != SectionType.Modifiers) return;

            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                FilterModifiers();
                _sectionItemIndex = 0;
                Speech.Say("Search cleared");
                if (_filteredModifiers.Count > 0)
                {
                    AnnounceSectionItem();
                }
            }
            else
            {
                FilterModifiers();
                _sectionItemIndex = 0;
                if (_filteredModifiers.Count > 0)
                {
                    AnnounceSectionItem();
                }
                else
                {
                    Speech.Say($"No matches for {_search.Buffer}");
                }
            }
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
