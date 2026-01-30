using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the Daily Expedition (Daily Challenge) popup.
    /// Provides flat list navigation with informational items, submenus for
    /// difficulty selection and modifiers, and embark button.
    /// </summary>
    public class DailyExpeditionOverlay : IKeyHandler
    {
        private enum ItemType
        {
            Biome,
            TimeLeft,
            Races,
            EmbarkGoods,
            EmbarkEffects,
            Modifiers,       // Interactive - Right arrow opens modifiers submenu
            SeasonalEffects,
            Rewards,
            Completed,
            Difficulty,      // Interactive - Enter opens difficulty submenu
            Embark           // Interactive - Enter triggers embark
        }

        private enum SubmenuMode
        {
            None,
            Difficulty,
            Modifiers
        }

        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<(ItemType type, string text)> _items = new List<(ItemType, string)>();
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Submenu state
        private SubmenuMode _submenuMode = SubmenuMode.None;
        private int _submenuIndex;
        private readonly TypeAheadSearch _submenuSearch = new TypeAheadSearch();

        // Difficulty data
        private List<object> _difficulties = new List<object>();

        // Modifiers data (name, description)
        private List<(string name, string description)> _modifiers = new List<(string, string)>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Handle submenu if open
            if (_submenuMode != SubmenuMode.None)
            {
                return ProcessSubmenuKey(keyCode, modifiers);
            }

            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.Home:
                    if (_items.Count > 0)
                    {
                        _currentIndex = 0;
                        AnnounceCurrentItem();
                    }
                    return true;

                case KeyCode.End:
                    if (_items.Count > 0)
                    {
                        _currentIndex = _items.Count - 1;
                        AnnounceCurrentItem();
                    }
                    return true;

                case KeyCode.RightArrow:
                    // Open submenu for modifiers
                    if (_currentIndex >= 0 && _currentIndex < _items.Count &&
                        _items[_currentIndex].type == ItemType.Modifiers)
                    {
                        OpenModifiersSubmenu();
                        return true;
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrentItem();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while active
                    return true;
            }
        }

        private bool ProcessSubmenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            _submenuSearch.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.DownArrow:
                    NavigateSubmenu(1);
                    return true;

                case KeyCode.UpArrow:
                    NavigateSubmenu(-1);
                    return true;

                case KeyCode.Home:
                    {
                        int count = GetSubmenuCount();
                        if (count > 0)
                        {
                            _submenuIndex = 0;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.End:
                    {
                        int count = GetSubmenuCount();
                        if (count > 0)
                        {
                            _submenuIndex = count - 1;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.LeftArrow:
                    // Exit submenu (for modifiers)
                    if (_submenuMode == SubmenuMode.Modifiers)
                    {
                        CloseSubmenu(announce: true);
                        return true;
                    }
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_submenuMode == SubmenuMode.Difficulty)
                    {
                        SelectDifficulty();
                    }
                    else if (_submenuMode == SubmenuMode.Modifiers)
                    {
                        // Re-announce current modifier
                        AnnounceSubmenuItem();
                    }
                    return true;

                case KeyCode.Escape:
                    CloseSubmenu(announce: true);
                    return true;

                case KeyCode.Backspace:
                    if (_submenuSearch.HasBuffer)
                        HandleSubmenuBackspace();
                    return true;

                default:
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSubmenuSearchKey(c);
                        return true;
                    }
                    // Consume all other keys while submenu is open
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
            _currentIndex = 0;
            _submenuMode = SubmenuMode.None;
            _search.Clear();

            RefreshData();

            string announcement = "Daily Expedition";
            if (_items.Count > 0)
            {
                announcement += $". {_items[0].text}";
            }

            Speech.Say(announcement);
            Debug.Log($"[ATSAccessibility] DailyExpeditionOverlay opened, {_items.Count} items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();
            _difficulties.Clear();
            _modifiers.Clear();
            _currentIndex = 0;
            _submenuMode = SubmenuMode.None;
            _submenuIndex = 0;
            _search.Clear();
            _submenuSearch.Clear();

            Debug.Log("[ATSAccessibility] DailyExpeditionOverlay closed");
        }

        // ========================================
        // Main List Navigation
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
            Speech.Say(_items[_currentIndex].text);
        }

        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            switch (item.type)
            {
                case ItemType.Difficulty:
                    OpenDifficultySubmenu();
                    break;

                case ItemType.Modifiers:
                    OpenModifiersSubmenu();
                    break;

                case ItemType.Embark:
                    TriggerEmbark();
                    break;

                default:
                    // Non-interactive item - just re-announce
                    AnnounceCurrentItem();
                    break;
            }
        }

        // ========================================
        // Submenu Navigation (shared)
        // ========================================

        private void NavigateSubmenu(int direction)
        {
            int count = GetSubmenuCount();
            if (count == 0) return;

            _submenuIndex = NavigationUtils.WrapIndex(_submenuIndex, direction, count);
            AnnounceSubmenuItem();
        }

        private int GetSubmenuCount()
        {
            switch (_submenuMode)
            {
                case SubmenuMode.Difficulty:
                    return _difficulties.Count;
                case SubmenuMode.Modifiers:
                    return _modifiers.Count;
                default:
                    return 0;
            }
        }

        private void AnnounceSubmenuItem()
        {
            switch (_submenuMode)
            {
                case SubmenuMode.Difficulty:
                    AnnounceDifficultyItem();
                    break;
                case SubmenuMode.Modifiers:
                    AnnounceModifierItem();
                    break;
            }
        }

        private void CloseSubmenu(bool announce)
        {
            _submenuMode = SubmenuMode.None;
            _submenuSearch.Clear();

            if (announce)
            {
                // Re-announce current main list item
                AnnounceCurrentItem();
            }

            Debug.Log("[ATSAccessibility] Submenu closed");
        }

        private void HandleSubmenuSearchKey(char c)
        {
            _submenuSearch.AddChar(c);

            int match = FindSubmenuMatch();
            if (match >= 0)
            {
                _submenuIndex = match;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_submenuSearch.Buffer}");
            }
        }

        private void HandleSubmenuBackspace()
        {
            if (!_submenuSearch.RemoveChar()) return;

            if (!_submenuSearch.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindSubmenuMatch();
            if (match >= 0)
            {
                _submenuIndex = match;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_submenuSearch.Buffer}");
            }
        }

        private int FindSubmenuMatch()
        {
            if (!_submenuSearch.HasBuffer) return -1;

            string lowerPrefix = _submenuSearch.Buffer.ToLowerInvariant();

            switch (_submenuMode)
            {
                case SubmenuMode.Difficulty:
                    for (int i = 0; i < _difficulties.Count; i++)
                    {
                        string name = DailyExpeditionReflection.GetDifficultyDisplayName(_difficulties[i]);
                        if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;

                case SubmenuMode.Modifiers:
                    for (int i = 0; i < _modifiers.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(_modifiers[i].name) &&
                            _modifiers[i].name.ToLowerInvariant().StartsWith(lowerPrefix))
                            return i;
                    }
                    break;
            }

            return -1;
        }

        // ========================================
        // Difficulty Submenu
        // ========================================

        private void OpenDifficultySubmenu()
        {
            _difficulties = DailyExpeditionReflection.GetAvailableDifficulties(_popup);
            if (_difficulties.Count == 0)
            {
                Speech.Say("No difficulties available");
                return;
            }

            _submenuMode = SubmenuMode.Difficulty;
            _submenuSearch.Clear();

            // Find current difficulty index
            var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);
            int currentIdx = DailyExpeditionReflection.GetDifficultyIndex(currentDifficulty);
            _submenuIndex = 0;

            for (int i = 0; i < _difficulties.Count; i++)
            {
                if (DailyExpeditionReflection.GetDifficultyIndex(_difficulties[i]) == currentIdx)
                {
                    _submenuIndex = i;
                    break;
                }
            }

            SoundManager.PlayButtonClick();
            AnnounceDifficultyItem();
            Debug.Log($"[ATSAccessibility] Difficulty submenu opened, {_difficulties.Count} options");
        }

        private void AnnounceDifficultyItem()
        {
            if (_submenuIndex < 0 || _submenuIndex >= _difficulties.Count) return;

            var difficulty = _difficulties[_submenuIndex];
            string name = DailyExpeditionReflection.GetDifficultyDisplayName(difficulty);

            // Check if this is the current difficulty
            var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);
            int currentIdx = DailyExpeditionReflection.GetDifficultyIndex(currentDifficulty);
            int thisIdx = DailyExpeditionReflection.GetDifficultyIndex(difficulty);

            if (thisIdx == currentIdx)
            {
                Speech.Say($"{name}, current");
            }
            else
            {
                Speech.Say(name);
            }
        }

        private void SelectDifficulty()
        {
            if (_submenuIndex < 0 || _submenuIndex >= _difficulties.Count) return;

            var selectedDifficulty = _difficulties[_submenuIndex];

            if (DailyExpeditionReflection.SetDifficulty(_popup, selectedDifficulty))
            {
                SoundManager.PlayButtonClick();
                _submenuMode = SubmenuMode.None;
                _submenuSearch.Clear();

                // Rebuild affected items
                RefreshDifficultyDependentItems();

                // Announce selected difficulty
                string diffName = DailyExpeditionReflection.GetDifficultyDisplayName(selectedDifficulty);
                Speech.Say($"Selected {diffName}");

                Debug.Log($"[ATSAccessibility] Difficulty changed to {diffName}");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not change difficulty");
            }
        }

        // ========================================
        // Modifiers Submenu
        // ========================================

        private void OpenModifiersSubmenu()
        {
            _modifiers = DailyExpeditionReflection.GetModifiersDetailed(_popup);
            if (_modifiers.Count == 0)
            {
                Speech.Say("No modifiers");
                return;
            }

            _submenuMode = SubmenuMode.Modifiers;
            _submenuSearch.Clear();
            _submenuIndex = 0;

            AnnounceModifierItem();
            Debug.Log($"[ATSAccessibility] Modifiers submenu opened, {_modifiers.Count} modifiers");
        }

        private void AnnounceModifierItem()
        {
            if (_submenuIndex < 0 || _submenuIndex >= _modifiers.Count) return;

            var (name, description) = _modifiers[_submenuIndex];

            if (!string.IsNullOrEmpty(description))
            {
                Speech.Say($"{name}. {description}");
            }
            else
            {
                Speech.Say(name);
            }
        }

        // ========================================
        // Embark
        // ========================================

        private void TriggerEmbark()
        {
            if (DailyExpeditionReflection.TriggerEmbark(_popup))
            {
                SoundManager.PlayButtonClick();
                Speech.Say("Embarking");
                Debug.Log("[ATSAccessibility] Embark triggered");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not embark");
            }
        }

        // ========================================
        // Type-ahead Search (main list)
        // ========================================

        private void HandleSearchKey(char c)
        {
            _search.AddChar(c);

            int match = FindMatch();
            if (match >= 0)
            {
                _currentIndex = match;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int match = FindMatch();
            if (match >= 0)
            {
                _currentIndex = match;
                AnnounceCurrentItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindMatch()
        {
            if (!_search.HasBuffer) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].text.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }

        // ========================================
        // Data Building
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            var currentDifficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);

            // Build static items
            BuildStaticItems();

            // Build difficulty-dependent items
            BuildDifficultyDependentItems(currentDifficulty);

            // Add interactive items at the end
            string diffName = currentDifficulty != null
                ? DailyExpeditionReflection.GetDifficultyDisplayName(currentDifficulty)
                : "Unknown";
            _items.Add((ItemType.Difficulty, $"Difficulty: {diffName}"));
            _items.Add((ItemType.Embark, "Embark"));

            Debug.Log($"[ATSAccessibility] DailyExpeditionOverlay: Built {_items.Count} items");
        }

        private void BuildStaticItems()
        {
            // Biome
            string biome = DailyExpeditionReflection.GetBiomeName(_popup);
            _items.Add((ItemType.Biome, $"Biome: {biome}"));

            // Time left
            string timeLeft = DailyExpeditionReflection.GetTimeLeft(_popup);
            _items.Add((ItemType.TimeLeft, $"Time Left: {timeLeft}"));

            // Races
            var races = DailyExpeditionReflection.GetRaces(_popup);
            if (races.Count > 0)
            {
                _items.Add((ItemType.Races, $"Races: {string.Join(", ", races)}"));
            }

            // Embark goods
            var goods = DailyExpeditionReflection.GetEmbarkGoods(_popup);
            if (goods.Count > 0)
            {
                _items.Add((ItemType.EmbarkGoods, $"Embark Goods: {string.Join(", ", goods)}"));
            }

            // Embark effects
            var effects = DailyExpeditionReflection.GetEmbarkEffects(_popup);
            if (effects.Count > 0)
            {
                _items.Add((ItemType.EmbarkEffects, $"Embark Effects: {string.Join(", ", effects)}"));
            }

            // Modifiers (with count, interactive submenu)
            var modifiers = DailyExpeditionReflection.GetModifiers(_popup);
            if (modifiers.Count > 0)
            {
                _items.Add((ItemType.Modifiers, $"Modifiers ({modifiers.Count})"));
            }
        }

        private void BuildDifficultyDependentItems(object difficulty)
        {
            // Cache completed status (used in multiple places)
            bool completed = DailyExpeditionReflection.IsCompleted(_popup);

            // Seasonal effects counts and magnitude
            var (positive, negative) = DailyExpeditionReflection.GetSeasonalEffectsCounts(difficulty);
            string magnitude = DailyExpeditionReflection.GetEffectsMagnitude(difficulty);
            if (positive > 0 || negative > 0)
            {
                string effectsText = $"Seasonal Effects: {positive} positive, {negative} negative";
                if (!string.IsNullOrEmpty(magnitude))
                {
                    effectsText += $", {magnitude}";
                }
                _items.Add((ItemType.SeasonalEffects, effectsText));
            }

            // Rewards (affected by difficulty multiplier)
            var rewards = DailyExpeditionReflection.GetRewards(_popup);
            if (rewards.Count > 0)
            {
                _items.Add((ItemType.Rewards, $"Rewards: {string.Join(", ", rewards)}"));
            }
            else
            {
                // No rewards if already done today at this difficulty
                if (completed)
                {
                    _items.Add((ItemType.Rewards, "Rewards: None (already completed at this difficulty)"));
                }
            }

            // Completed status
            _items.Add((ItemType.Completed, $"Completed Today: {(completed ? "Yes" : "No")}"));
        }

        private void RefreshDifficultyDependentItems()
        {
            // Find and update the difficulty-dependent items
            var difficulty = DailyExpeditionReflection.GetCurrentDifficulty(_popup);

            // Remove old difficulty-dependent items
            _items.RemoveAll(item =>
                item.type == ItemType.SeasonalEffects ||
                item.type == ItemType.Rewards ||
                item.type == ItemType.Completed ||
                item.type == ItemType.Difficulty);

            // Also remove Embark (we'll re-add it)
            _items.RemoveAll(item => item.type == ItemType.Embark);

            // Re-add difficulty-dependent items
            BuildDifficultyDependentItems(difficulty);

            // Re-add interactive items
            string diffName = difficulty != null
                ? DailyExpeditionReflection.GetDifficultyDisplayName(difficulty)
                : "Unknown";
            _items.Add((ItemType.Difficulty, $"Difficulty: {diffName}"));
            _items.Add((ItemType.Embark, "Embark"));

            // Update current index to point to difficulty item
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].type == ItemType.Difficulty)
                {
                    _currentIndex = i;
                    break;
                }
            }
        }
    }
}
