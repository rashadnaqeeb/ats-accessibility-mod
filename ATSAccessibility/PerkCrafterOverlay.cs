using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the PerkCrafterPopup (Cornerstone Forge).
    /// Provides two-level menu navigation: main menu and submenus for hook/effect selection.
    /// </summary>
    public class PerkCrafterOverlay : IKeyHandler
    {
        // Navigation levels
        private const int LEVEL_MAIN = 0;
        private const int LEVEL_SUBMENU = 1;
        private const int LEVEL_NAME_EDIT = 2;

        // Main menu items (active state)
        private enum MenuItem
        {
            Dialogue = 0,
            Shards = 1,
            Hook = 2,
            Positive = 3,
            Negative = 4,
            Result = 5,
            Craft = 6
        }

        // State
        private bool _isOpen;
        private int _navigationLevel;
        private int _mainMenuIndex;
        private int _submenuIndex;
        private MenuItem _activeSubmenu;
        private bool _isFinishedMode;

        // Data caches
        private List<PerkCrafterReflection.HookOption> _hookOptions;
        private List<PerkCrafterReflection.EffectOption> _positiveOptions;
        private List<PerkCrafterReflection.EffectOption> _negativeOptions;
        private List<PerkCrafterReflection.CraftedPerkInfo> _craftedPerks;

        // Type-ahead search for submenus
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Name editing buffer
        private StringBuilder _nameBuffer;
        private bool _nameEditing;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Handle name editing mode separately
            if (_navigationLevel == LEVEL_NAME_EDIT)
            {
                return ProcessNameEditKey(keyCode, modifiers);
            }

            _search.ClearOnNavigationKey(keyCode);

            if (_navigationLevel == LEVEL_SUBMENU)
            {
                return ProcessSubmenuKey(keyCode, modifiers);
            }

            return ProcessMainMenuKey(keyCode, modifiers);
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when PerkCrafterPopup is shown.
        /// </summary>
        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _navigationLevel = LEVEL_MAIN;
            _mainMenuIndex = 0;
            _submenuIndex = 0;
            _search.Clear();
            _nameEditing = false;
            _nameBuffer = null;

            RefreshData();

            if (_isFinishedMode)
            {
                Speech.Say($"Cornerstone Forge, finished. {_craftedPerks?.Count ?? 0} cornerstones crafted");
                if (_craftedPerks != null && _craftedPerks.Count > 0)
                {
                    _mainMenuIndex = 0;
                    AnnounceFinishedItem();
                }
            }
            else
            {
                Speech.Say("Cornerstone Forge");
                AnnounceMainMenuItem();
            }

            Debug.Log("[ATSAccessibility] PerkCrafterOverlay opened");
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _navigationLevel = LEVEL_MAIN;
            _search.Clear();
            _nameEditing = false;
            ClearData();

            Debug.Log("[ATSAccessibility] PerkCrafterOverlay closed");
        }

        // ========================================
        // DATA MANAGEMENT
        // ========================================

        private void RefreshData()
        {
            _isFinishedMode = PerkCrafterReflection.HasUsedAllCharges();

            if (_isFinishedMode)
            {
                _craftedPerks = PerkCrafterReflection.GetCraftedPerks();
            }
            else
            {
                _hookOptions = PerkCrafterReflection.GetHookOptions();
                _positiveOptions = PerkCrafterReflection.GetPositiveOptions();
                _negativeOptions = PerkCrafterReflection.GetNegativeOptions();
            }
        }

        private void ClearData()
        {
            _hookOptions?.Clear();
            _positiveOptions?.Clear();
            _negativeOptions?.Clear();
            _craftedPerks?.Clear();
        }

        // ========================================
        // MAIN MENU NAVIGATION (Level 0)
        // ========================================

        private bool ProcessMainMenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (_isFinishedMode)
            {
                return ProcessFinishedModeKey(keyCode);
            }

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateMainMenu(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateMainMenu(1);
                    return true;

                case KeyCode.Home:
                    _mainMenuIndex = 0;
                    AnnounceMainMenuItem();
                    return true;

                case KeyCode.End:
                    _mainMenuIndex = 6; // Last item: Craft
                    AnnounceMainMenuItem();
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateMainMenuItem();
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateMainMenu(int direction)
        {
            int itemCount = 7; // Dialogue, Shards, Hook, Positive, Negative, Result, Craft
            _mainMenuIndex = NavigationUtils.WrapIndex(_mainMenuIndex, direction, itemCount);
            AnnounceMainMenuItem();
        }

        private void AnnounceMainMenuItem()
        {
            var item = (MenuItem)_mainMenuIndex;

            switch (item)
            {
                case MenuItem.Dialogue:
                    AnnounceDialogue();
                    break;

                case MenuItem.Shards:
                    AnnounceShards();
                    break;

                case MenuItem.Hook:
                    AnnounceHook();
                    break;

                case MenuItem.Positive:
                    AnnouncePositive();
                    break;

                case MenuItem.Negative:
                    AnnounceNegative();
                    break;

                case MenuItem.Result:
                    AnnounceResult();
                    break;

                case MenuItem.Craft:
                    AnnounceCraft();
                    break;
            }
        }

        private void AnnounceDialogue()
        {
            var dialogue = PerkCrafterReflection.GetNpcDialogue();
            if (!string.IsNullOrEmpty(dialogue))
            {
                Speech.Say($"Malzor Stonespine: {dialogue}");
            }
            else
            {
                Speech.Say("Malzor Stonespine");
            }
        }

        private void AnnounceShards()
        {
            int usesLeft = PerkCrafterReflection.GetUsesLeft();
            int total = PerkCrafterReflection.GetTotalCharges();
            int crafted = PerkCrafterReflection.GetCraftedPerksCount();

            if (usesLeft > 0)
            {
                Speech.Say($"Crafting {crafted + 1} of {total}");
            }
            else
            {
                Speech.Say("All crafts used");
            }
        }

        private void AnnounceHook()
        {
            var currentHook = PerkCrafterReflection.GetCurrentHook();
            if (currentHook != null)
            {
                Speech.Say($"Hook: {currentHook.Description}");
            }
            else
            {
                Speech.Say("Hook: not selected");
            }
        }

        private void AnnouncePositive()
        {
            var currentPositive = PerkCrafterReflection.GetCurrentPositive();
            if (currentPositive != null)
            {
                Speech.Say($"Positive effect: {currentPositive.Description}");
            }
            else
            {
                Speech.Say("Positive effect: not selected");
            }
        }

        private void AnnounceNegative()
        {
            int negIndex = PerkCrafterReflection.GetPickedNegativeIndex();
            if (negIndex < 0)
            {
                Speech.Say("Negative effect: none");
            }
            else
            {
                var currentNegative = PerkCrafterReflection.GetCurrentNegative();
                if (currentNegative != null)
                {
                    Speech.Say($"Negative effect: {currentNegative.Description}");
                }
                else
                {
                    Speech.Say("Negative effect: not selected");
                }
            }
        }

        private void AnnounceResult()
        {
            var resultName = PerkCrafterReflection.GetResultName();
            if (!string.IsNullOrEmpty(resultName))
            {
                Speech.Say($"Result: {resultName}");
            }
            else
            {
                Speech.Say("Result: unnamed");
            }
        }

        private void AnnounceCraft()
        {
            var (amount, goodName) = PerkCrafterReflection.GetPrice();
            int have = PerkCrafterReflection.GetStorageAmount();

            if (PerkCrafterReflection.CanAffordCraft())
            {
                Speech.Say($"Craft, costs {amount} {goodName}, have {have}");
            }
            else
            {
                Speech.Say($"Craft, unavailable, need {amount} {goodName}, have {have}");
            }
        }

        // ========================================
        // MAIN MENU ACTIVATION
        // ========================================

        private void ActivateMainMenuItem()
        {
            var item = (MenuItem)_mainMenuIndex;

            switch (item)
            {
                case MenuItem.Dialogue:
                case MenuItem.Shards:
                    // Read-only items, re-announce
                    AnnounceMainMenuItem();
                    break;

                case MenuItem.Hook:
                    OpenHookSubmenu();
                    break;

                case MenuItem.Positive:
                    OpenPositiveSubmenu();
                    break;

                case MenuItem.Negative:
                    OpenNegativeSubmenu();
                    break;

                case MenuItem.Result:
                    OpenNameEdit();
                    break;

                case MenuItem.Craft:
                    PerformCraft();
                    break;
            }
        }

        // ========================================
        // SUBMENU NAVIGATION (Level 1)
        // ========================================

        private void OpenHookSubmenu()
        {
            if (_hookOptions == null || _hookOptions.Count == 0)
            {
                Speech.Say("No hooks available");
                return;
            }

            _navigationLevel = LEVEL_SUBMENU;
            _activeSubmenu = MenuItem.Hook;
            _submenuIndex = PerkCrafterReflection.GetPickedHookIndex();
            if (_submenuIndex < 0 || _submenuIndex >= _hookOptions.Count)
                _submenuIndex = 0;

            _search.Clear();
            AnnounceSubmenuItem();
        }

        private void OpenPositiveSubmenu()
        {
            if (_positiveOptions == null || _positiveOptions.Count == 0)
            {
                Speech.Say("No positive effects available");
                return;
            }

            _navigationLevel = LEVEL_SUBMENU;
            _activeSubmenu = MenuItem.Positive;
            _submenuIndex = PerkCrafterReflection.GetPickedPositiveIndex();
            if (_submenuIndex < 0 || _submenuIndex >= _positiveOptions.Count)
                _submenuIndex = 0;

            _search.Clear();
            AnnounceSubmenuItem();
        }

        private void OpenNegativeSubmenu()
        {
            if (_negativeOptions == null || _negativeOptions.Count == 0)
            {
                Speech.Say("No negative effects available");
                return;
            }

            _navigationLevel = LEVEL_SUBMENU;
            _activeSubmenu = MenuItem.Negative;

            // For negative, index 0 is "None", then actual options
            int pickedIndex = PerkCrafterReflection.GetPickedNegativeIndex();
            if (pickedIndex < 0 || _negativeOptions == null)
                _submenuIndex = 0;
            else if (pickedIndex < _negativeOptions.Count)
                _submenuIndex = pickedIndex + 1;  // +1 because "None" is at 0
            else
                _submenuIndex = 0;  // Fallback for out-of-range

            _search.Clear();
            AnnounceSubmenuItem();
        }

        private bool ProcessSubmenuKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateSubmenu(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateSubmenu(1);
                    return true;

                case KeyCode.Home:
                    {
                        int count = GetSubmenuItemCount();
                        if (count > 0)
                        {
                            _submenuIndex = 0;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.End:
                    {
                        int count = GetSubmenuItemCount();
                        if (count > 0)
                        {
                            _submenuIndex = count - 1;
                            AnnounceSubmenuItem();
                        }
                    }
                    return true;

                case KeyCode.RightArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SelectSubmenuItem();
                    return true;

                case KeyCode.LeftArrow:
                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        if (keyCode == KeyCode.Escape)
                            InputBlocker.BlockCancelOnce = true;
                        return true;
                    }
                    ReturnToMainMenu();
                    if (keyCode == KeyCode.Escape)
                        InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.Backspace:
                    if (_search.HasBuffer)
                        HandleSubmenuBackspace();
                    return true;

                default:
                    // Type-ahead search (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        HandleSubmenuSearch(c);
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private int GetSubmenuItemCount()
        {
            switch (_activeSubmenu)
            {
                case MenuItem.Hook:
                    return _hookOptions?.Count ?? 0;

                case MenuItem.Positive:
                    return _positiveOptions?.Count ?? 0;

                case MenuItem.Negative:
                    // +1 for "None" option at the start
                    return (_negativeOptions?.Count ?? 0) + 1;

                default:
                    return 0;
            }
        }

        private void NavigateSubmenu(int direction)
        {
            int count = GetSubmenuItemCount();
            if (count == 0) return;

            _submenuIndex = NavigationUtils.WrapIndex(_submenuIndex, direction, count);
            AnnounceSubmenuItem();
        }

        private void AnnounceSubmenuItem()
        {
            switch (_activeSubmenu)
            {
                case MenuItem.Hook:
                    if (_hookOptions != null && _submenuIndex < _hookOptions.Count)
                    {
                        var hook = _hookOptions[_submenuIndex];
                        Speech.Say(hook.Description);
                    }
                    break;

                case MenuItem.Positive:
                    if (_positiveOptions != null && _submenuIndex < _positiveOptions.Count)
                    {
                        var effect = _positiveOptions[_submenuIndex];
                        Speech.Say(effect.Description);
                    }
                    break;

                case MenuItem.Negative:
                    if (_submenuIndex == 0)
                    {
                        Speech.Say("None: skip negative effect");
                    }
                    else if (_negativeOptions != null && _submenuIndex - 1 < _negativeOptions.Count)
                    {
                        var effect = _negativeOptions[_submenuIndex - 1];
                        Speech.Say(effect.Description);
                    }
                    break;
            }
        }

        private void SelectSubmenuItem()
        {
            bool success = false;

            switch (_activeSubmenu)
            {
                case MenuItem.Hook:
                    if (_hookOptions != null && _submenuIndex < _hookOptions.Count)
                    {
                        success = PerkCrafterReflection.SelectHook(_hookOptions[_submenuIndex]);
                    }
                    break;

                case MenuItem.Positive:
                    if (_positiveOptions != null && _submenuIndex < _positiveOptions.Count)
                    {
                        success = PerkCrafterReflection.SelectPositive(_positiveOptions[_submenuIndex]);
                    }
                    break;

                case MenuItem.Negative:
                    if (_submenuIndex == 0)
                    {
                        // "None" selected - clear negative
                        success = PerkCrafterReflection.SelectNegative(null);
                    }
                    else if (_negativeOptions != null && _submenuIndex - 1 < _negativeOptions.Count)
                    {
                        success = PerkCrafterReflection.SelectNegative(_negativeOptions[_submenuIndex - 1]);
                    }
                    break;
            }

            if (success)
            {
                SoundManager.PlayButtonClick();
                Speech.Say("Selected");

                // Refresh data since selections affect the result
                RefreshData();
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Cannot select");
            }

            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            _navigationLevel = LEVEL_MAIN;
            _search.Clear();
            AnnounceMainMenuItem();
        }

        // ========================================
        // SUBMENU SEARCH
        // ========================================

        private void HandleSubmenuSearch(char c)
        {
            _search.AddChar(c);

            int matchIndex = FindSubmenuMatch();
            if (matchIndex >= 0)
            {
                _submenuIndex = matchIndex;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private void HandleSubmenuBackspace()
        {
            if (!_search.RemoveChar()) return;

            if (!_search.HasBuffer)
            {
                Speech.Say("Search cleared");
                return;
            }

            int matchIndex = FindSubmenuMatch();
            if (matchIndex >= 0)
            {
                _submenuIndex = matchIndex;
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say($"No match for {_search.Buffer}");
            }
        }

        private int FindSubmenuMatch()
        {
            if (!_search.HasBuffer) return -1;

            string lowerPrefix = _search.Buffer.ToLowerInvariant();

            switch (_activeSubmenu)
            {
                case MenuItem.Hook:
                    if (_hookOptions != null)
                    {
                        for (int i = 0; i < _hookOptions.Count; i++)
                        {
                            if (_hookOptions[i].Description.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i;
                        }
                    }
                    break;

                case MenuItem.Positive:
                    if (_positiveOptions != null)
                    {
                        for (int i = 0; i < _positiveOptions.Count; i++)
                        {
                            if (_positiveOptions[i].Description.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i;
                        }
                    }
                    break;

                case MenuItem.Negative:
                    // Skip "None" (index 0) for search
                    if (_negativeOptions != null)
                    {
                        for (int i = 0; i < _negativeOptions.Count; i++)
                        {
                            if (_negativeOptions[i].Description.ToLowerInvariant().StartsWith(lowerPrefix))
                                return i + 1; // +1 because "None" is at 0
                        }
                    }
                    break;
            }

            return -1;
        }

        // ========================================
        // NAME EDITING (Level 2)
        // ========================================

        private void OpenNameEdit()
        {
            var currentName = PerkCrafterReflection.GetResultName() ?? "";
            _nameBuffer = new StringBuilder(currentName);
            _navigationLevel = LEVEL_NAME_EDIT;
            _nameEditing = true;

            Speech.Say($"Editing name: {currentName}. Type to replace, Alt R to randomize, Enter to confirm");
        }

        private bool ProcessNameEditKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ConfirmNameEdit();
                    return true;

                case KeyCode.Escape:
                    CancelNameEdit();
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                case KeyCode.Backspace:
                    if (_nameBuffer.Length > 0)
                    {
                        _nameBuffer.Remove(_nameBuffer.Length - 1, 1);
                        Speech.Say(_nameBuffer.Length > 0 ? _nameBuffer.ToString() : "Empty");
                    }
                    return true;

                case KeyCode.R:
                    // Alt+R to randomize name
                    if (modifiers.Alt && !modifiers.Shift && !modifiers.Control)
                    {
                        RandomizeName();
                        return true;
                    }
                    // Plain R types the letter
                    goto default;

                default:
                    // Type letters (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = modifiers.Shift ?
                            (char)('A' + (keyCode - KeyCode.A)) :
                            (char)('a' + (keyCode - KeyCode.A));

                        // If this is the first character after opening, clear existing name
                        if (_nameEditing)
                        {
                            _nameBuffer.Clear();
                            _nameEditing = false;
                        }

                        _nameBuffer.Append(c);
                        Speech.Say(_nameBuffer.ToString());
                        return true;
                    }
                    // Space
                    else if (keyCode == KeyCode.Space)
                    {
                        _nameBuffer.Append(' ');
                        Speech.Say(_nameBuffer.ToString());
                        return true;
                    }
                    // Consume all other keys
                    return true;
            }
        }

        private void ConfirmNameEdit()
        {
            if (_nameBuffer.Length > 0)
            {
                PerkCrafterReflection.SetResultName(_nameBuffer.ToString());
                SoundManager.PlayButtonClick();
                Speech.Say($"Name set to {_nameBuffer}");
            }
            else
            {
                Speech.Say("Name unchanged");
            }

            _navigationLevel = LEVEL_MAIN;
            _nameEditing = false;
        }

        private void CancelNameEdit()
        {
            Speech.Say("Cancelled");
            _navigationLevel = LEVEL_MAIN;
            _nameEditing = false;
        }

        private void RandomizeName()
        {
            if (PerkCrafterReflection.RandomizeName())
            {
                SoundManager.PlayButtonClick();
                var newName = PerkCrafterReflection.GetResultName();
                _nameBuffer = new StringBuilder(newName ?? "");
                Speech.Say($"Randomized: {newName}");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Cannot randomize");
            }
        }

        // ========================================
        // CRAFTING
        // ========================================

        private void PerformCraft()
        {
            if (!PerkCrafterReflection.CanAffordCraft())
            {
                var (amount, goodName) = PerkCrafterReflection.GetPrice();
                int have = PerkCrafterReflection.GetStorageAmount();
                Speech.Say($"Cannot afford, need {amount} {goodName}, have {have}");
                SoundManager.PlayFailed();
                return;
            }

            if (PerkCrafterReflection.PerformCraft())
            {
                SoundManager.PlayButtonClick();

                // Refresh data to check if we're now in finished mode
                RefreshData();

                if (_isFinishedMode)
                {
                    Speech.Say("Crafted. All cornerstones complete");
                    _mainMenuIndex = 0;
                }
                else
                {
                    int crafted = PerkCrafterReflection.GetCraftedPerksCount();
                    int total = PerkCrafterReflection.GetTotalCharges();
                    Speech.Say($"Crafted. Now crafting {crafted + 1} of {total}");
                }
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Craft failed");
            }
        }

        // ========================================
        // FINISHED MODE (All Charges Used)
        // ========================================

        private bool ProcessFinishedModeKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateFinished(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateFinished(1);
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup
                    return false;

                default:
                    // Consume all other keys
                    return true;
            }
        }

        private void NavigateFinished(int direction)
        {
            // Items: Dialogue + crafted perks
            int itemCount = 1 + (_craftedPerks?.Count ?? 0);
            if (itemCount == 0) return;

            _mainMenuIndex = NavigationUtils.WrapIndex(_mainMenuIndex, direction, itemCount);
            AnnounceFinishedItem();
        }

        private void AnnounceFinishedItem()
        {
            if (_mainMenuIndex == 0)
            {
                // NPC dialogue for finished state
                var dialogue = PerkCrafterReflection.GetNpcDialogue();
                if (!string.IsNullOrEmpty(dialogue))
                {
                    Speech.Say($"Malzor Stonespine: {dialogue}");
                }
                else
                {
                    Speech.Say("Malzor Stonespine: That's all I can do for you.");
                }
            }
            else
            {
                // Crafted perk
                int perkIndex = _mainMenuIndex - 1;
                if (_craftedPerks != null && perkIndex < _craftedPerks.Count)
                {
                    var perk = _craftedPerks[perkIndex];
                    Speech.Say($"Crafted cornerstone {perkIndex + 1}: {perk.Name}. {perk.Description}");
                }
            }
        }
    }
}
