using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the ProfilesPopup (save selection screen).
    /// Two-level navigation: main level (save slots) and submenu (actions per slot).
    /// Supports text input for rename and confirmation for destructive actions.
    /// </summary>
    public class ProfilesOverlay : IKeyHandler
    {
        // ========================================
        // ENUMS
        // ========================================

        private enum ItemType
        {
            SaveSlot,       // Existing profile - opens submenu
            CreateNew,      // Empty slot - creates new profile
            SwitchMode      // Toggle between Regular/Queen's Hand
        }

        private enum SubMenuItem
        {
            Name,           // Edit name
            Switch,         // Switch to this save
            Reset,          // Reset progress
            Delete          // Delete save
        }

        private enum ConfirmAction
        {
            None,
            Reset,
            Delete
        }

        // ========================================
        // STATE
        // ========================================

        private bool _isOpen;
        private bool _viewingQueensHand;
        private int _currentIndex;
        private bool _inSubmenu;
        private int _submenuIndex;

        // Confirmation state
        private ConfirmAction _awaitingConfirm = ConfirmAction.None;

        // Text editing state
        private bool _editingName;
        private StringBuilder _editBuffer = new StringBuilder();

        // Data
        private List<ProfileItem> _items = new List<ProfileItem>();
        private List<SubMenuItem> _submenuItems = new List<SubMenuItem>();
        private object _currentSlotProfile;

        // Type-ahead search
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // INTERNAL DATA CLASS
        // ========================================

        private class ProfileItem
        {
            public ItemType Type;
            public object Profile;      // null for CreateNew/SwitchMode
            public bool IsCurrent;
            public bool IsDefault;      // Can't delete (regular saves only)
            public bool IsIronman;      // Queen's Hand profile (can't delete at all)
            public bool IsPickable;     // Can switch to this profile
            public string DisplayName;
            public string IronmanStatus; // "In Progress", "Won", "Lost", or null
            public int SlotNumber;      // 1-based
        }

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Text editing mode takes priority
            if (_editingName)
            {
                return ProcessEditingKey(keyCode, modifiers);
            }

            // Confirmation mode
            if (_awaitingConfirm != ConfirmAction.None)
            {
                return ProcessConfirmKey(keyCode);
            }

            // Submenu mode
            if (_inSubmenu)
            {
                return ProcessSubmenuKey(keyCode);
            }

            // Main level
            return ProcessMainKey(keyCode);
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open()
        {
            if (_isOpen) return;

            _isOpen = true;
            _viewingQueensHand = false;
            _currentIndex = 0;
            _inSubmenu = false;
            _submenuIndex = 0;
            _awaitingConfirm = ConfirmAction.None;
            _editingName = false;
            _editBuffer.Clear();
            _search.Clear();

            RefreshItems();

            string announcement = "Saves";
            if (_items.Count > 0)
            {
                announcement += $". {GetItemAnnouncement(0)}";
            }
            Speech.Say(announcement);

            Debug.Log($"[ATSAccessibility] ProfilesOverlay opened, {_items.Count} items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _items.Clear();
            _submenuItems.Clear();
            _currentSlotProfile = null;
            _awaitingConfirm = ConfirmAction.None;
            _editingName = false;
            _editBuffer.Clear();
            _search.Clear();

            Debug.Log("[ATSAccessibility] ProfilesOverlay closed");
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshItems()
        {
            _items.Clear();

            var profiles = ProfilesReflection.GetProfiles(_viewingQueensHand);
            var currentProfile = ProfilesReflection.GetCurrentProfile();
            int maxSlots = ProfilesReflection.GetMaxProfiles(_viewingQueensHand);

            // Add existing profiles as slots
            int slotNum = 1;
            foreach (var profile in profiles)
            {
                _items.Add(new ProfileItem
                {
                    Type = ItemType.SaveSlot,
                    Profile = profile,
                    IsCurrent = profile == currentProfile,
                    IsDefault = ProfilesReflection.IsDefault(profile),
                    IsIronman = ProfilesReflection.IsIronman(profile),
                    IsPickable = ProfilesReflection.IsPickable(profile),
                    DisplayName = ProfilesReflection.GetProfileDisplayName(profile),
                    IronmanStatus = ProfilesReflection.GetIronmanStatus(profile),
                    SlotNumber = slotNum++
                });
            }

            // Add empty slots for remaining capacity
            while (slotNum <= maxSlots)
            {
                _items.Add(new ProfileItem
                {
                    Type = ItemType.CreateNew,
                    Profile = null,
                    IsCurrent = false,
                    IsDefault = false,
                    DisplayName = "Create new save",
                    SlotNumber = slotNum++
                });
            }

            // Add mode switch button (only for regular mode if QH unlocked, always for QH mode)
            if (_viewingQueensHand)
            {
                _items.Add(new ProfileItem
                {
                    Type = ItemType.SwitchMode,
                    DisplayName = "Switch to Regular Saves",
                    SlotNumber = 0
                });
            }
            else if (ProfilesReflection.IsIronmanUnlocked())
            {
                _items.Add(new ProfileItem
                {
                    Type = ItemType.SwitchMode,
                    DisplayName = "Switch to Queen's Hand",
                    SlotNumber = 0
                });
            }
        }

        private void RefreshSubmenuItems()
        {
            _submenuItems.Clear();
            if (_currentSlotProfile == null) return;

            // Find the current item to get cached properties
            ProfileItem currentItem = null;
            foreach (var item in _items)
            {
                if (item.Profile == _currentSlotProfile)
                {
                    currentItem = item;
                    break;
                }
            }
            if (currentItem == null) return;

            // Name editing
            _submenuItems.Add(SubMenuItem.Name);

            // Switch to save (hidden if current or not pickable - e.g. completed ironman)
            if (!currentItem.IsCurrent && currentItem.IsPickable)
            {
                _submenuItems.Add(SubMenuItem.Switch);
            }

            // Reset progress (always available)
            _submenuItems.Add(SubMenuItem.Reset);

            // Delete (hidden for default profile AND all ironman profiles)
            if (!currentItem.IsDefault && !currentItem.IsIronman)
            {
                _submenuItems.Add(SubMenuItem.Delete);
            }
        }

        // ========================================
        // MAIN LEVEL NAVIGATION
        // ========================================

        private bool ProcessMainKey(KeyCode keyCode)
        {
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.RightArrow:
                    // Right arrow only enters submenu for save slots
                    EnterSubmenuIfSaveSlot();
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

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;
            Speech.Say(GetItemAnnouncement(_currentIndex));
        }

        private string GetItemAnnouncement(int index)
        {
            if (index < 0 || index >= _items.Count) return "";

            var item = _items[index];

            switch (item.Type)
            {
                case ItemType.SaveSlot:
                    string status = "";
                    if (item.IsCurrent)
                    {
                        status = ", current";
                    }
                    else if (item.IronmanStatus != null)
                    {
                        // Show ironman status (In Progress, Won, Lost)
                        status = $", {item.IronmanStatus}";
                    }
                    return $"Save {item.SlotNumber}: {item.DisplayName}{status}";

                case ItemType.CreateNew:
                    return $"Save {item.SlotNumber}: {item.DisplayName}";

                case ItemType.SwitchMode:
                    return item.DisplayName;

                default:
                    return item.DisplayName;
            }
        }

        private void EnterSubmenuIfSaveSlot()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            // Right arrow only enters submenu for existing save slots
            if (item.Type == ItemType.SaveSlot)
            {
                EnterSubmenu(item);
            }
            // For other items, do nothing on right arrow
        }

        private void ActivateCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            switch (item.Type)
            {
                case ItemType.SaveSlot:
                    EnterSubmenu(item);
                    break;

                case ItemType.CreateNew:
                    CreateNewProfile();
                    break;

                case ItemType.SwitchMode:
                    ToggleMode();
                    break;
            }
        }

        private void EnterSubmenu(ProfileItem item)
        {
            _currentSlotProfile = item.Profile;
            _inSubmenu = true;
            _submenuIndex = 0;
            _search.Clear();

            RefreshSubmenuItems();

            if (_submenuItems.Count > 0)
            {
                SoundManager.PlayButtonClick();
                AnnounceSubmenuItem();
            }
            else
            {
                Speech.Say("No actions available");
                _inSubmenu = false;
            }
        }

        private void CreateNewProfile()
        {
            if (ProfilesReflection.CreateNewProfile(_viewingQueensHand))
            {
                SoundManager.PlayButtonClick();
                RefreshItems();

                // Move to the newly created slot (last profile, before empty slots and switch button)
                var profiles = ProfilesReflection.GetProfiles(_viewingQueensHand);
                _currentIndex = profiles.Count - 1;
                if (_currentIndex < 0) _currentIndex = 0;

                Speech.Say($"Created. {GetItemAnnouncement(_currentIndex)}");
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not create new save");
            }
        }

        private void ToggleMode()
        {
            _viewingQueensHand = !_viewingQueensHand;
            _currentIndex = 0;
            _search.Clear();

            RefreshItems();

            string modeText = _viewingQueensHand ? "Queen's Hand saves" : "Regular saves";
            string announcement = modeText;
            if (_items.Count > 0)
            {
                announcement += $". {GetItemAnnouncement(0)}";
            }

            SoundManager.PlayButtonClick();
            Speech.Say(announcement);
        }

        // ========================================
        // SUBMENU NAVIGATION
        // ========================================

        private bool ProcessSubmenuKey(KeyCode keyCode)
        {
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    NavigateSubmenu(-1);
                    return true;

                case KeyCode.DownArrow:
                    NavigateSubmenu(1);
                    return true;

                case KeyCode.LeftArrow:
                    ExitSubmenu();
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateSubmenuItem();
                    return true;

                case KeyCode.Escape:
                    ExitSubmenu();
                    InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
                    return true;

                default:
                    // Consume all keys in submenu
                    return true;
            }
        }

        private void NavigateSubmenu(int direction)
        {
            if (_submenuItems.Count == 0) return;

            _submenuIndex = NavigationUtils.WrapIndex(_submenuIndex, direction, _submenuItems.Count);
            AnnounceSubmenuItem();
        }

        private void AnnounceSubmenuItem()
        {
            if (_submenuIndex < 0 || _submenuIndex >= _submenuItems.Count) return;

            var item = _submenuItems[_submenuIndex];
            string name = ProfilesReflection.GetProfileDisplayName(_currentSlotProfile);

            switch (item)
            {
                case SubMenuItem.Name:
                    Speech.Say($"Name: {name}");
                    break;

                case SubMenuItem.Switch:
                    Speech.Say("Switch to save");
                    break;

                case SubMenuItem.Reset:
                    if (ProfilesReflection.IsIronman(_currentSlotProfile))
                    {
                        bool canResetSeed = ProfilesReflection.CanResetIronmanSeed(_currentSlotProfile);
                        Speech.Say(canResetSeed ? "Reset progress, new seed" : "Reset progress, same seed");
                    }
                    else
                    {
                        Speech.Say("Reset progress");
                    }
                    break;

                case SubMenuItem.Delete:
                    Speech.Say("Delete");
                    break;
            }
        }

        private void ActivateSubmenuItem()
        {
            if (_submenuIndex < 0 || _submenuIndex >= _submenuItems.Count) return;

            var item = _submenuItems[_submenuIndex];

            switch (item)
            {
                case SubMenuItem.Name:
                    StartNameEditing();
                    break;

                case SubMenuItem.Switch:
                    SwitchToProfile();
                    break;

                case SubMenuItem.Reset:
                    RequestConfirmation(ConfirmAction.Reset);
                    break;

                case SubMenuItem.Delete:
                    RequestConfirmation(ConfirmAction.Delete);
                    break;
            }
        }

        private void ExitSubmenu()
        {
            _inSubmenu = false;
            _submenuItems.Clear();
            _currentSlotProfile = null;
            _search.Clear();

            // Re-announce current main item
            AnnounceCurrentItem();
        }

        private void SwitchToProfile()
        {
            if (ProfilesReflection.ChangeProfile(_currentSlotProfile))
            {
                SoundManager.PlayButtonClick();
                string name = ProfilesReflection.GetProfileDisplayName(_currentSlotProfile);
                Speech.Say($"Switching to {name}");
                // The popup will close and the game will reload
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not switch profile");
            }
        }

        // ========================================
        // CONFIRMATION HANDLING
        // ========================================

        private void RequestConfirmation(ConfirmAction action)
        {
            _awaitingConfirm = action;
            string actionText = action == ConfirmAction.Reset ? "Reset progress" : "Delete";
            Speech.Say($"{actionText}. Press Enter to confirm");
        }

        private bool ProcessConfirmKey(KeyCode keyCode)
        {
            if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
            {
                ExecuteConfirmedAction();
                return true;
            }

            // Any other key cancels
            if (keyCode == KeyCode.Escape)
                InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
            Speech.Say("Cancelled");
            _awaitingConfirm = ConfirmAction.None;
            return true;
        }

        private void ExecuteConfirmedAction()
        {
            var action = _awaitingConfirm;
            _awaitingConfirm = ConfirmAction.None;

            switch (action)
            {
                case ConfirmAction.Reset:
                    if (ProfilesReflection.ClearProfile(_currentSlotProfile))
                    {
                        SoundManager.PlayButtonClick();
                        Speech.Say("Progress reset");
                        // Exit submenu as data may have changed
                        ExitSubmenu();
                        RefreshItems();
                    }
                    else
                    {
                        SoundManager.PlayFailed();
                        Speech.Say("Could not reset progress");
                    }
                    break;

                case ConfirmAction.Delete:
                    if (ProfilesReflection.RemoveProfile(_currentSlotProfile))
                    {
                        SoundManager.PlayButtonClick();
                        Speech.Say("Deleted");
                        ExitSubmenu();
                        RefreshItems();

                        // Adjust index if we were past the end
                        if (_currentIndex >= _items.Count)
                            _currentIndex = _items.Count - 1;
                        if (_currentIndex < 0)
                            _currentIndex = 0;

                        AnnounceCurrentItem();
                    }
                    else
                    {
                        SoundManager.PlayFailed();
                        Speech.Say("Could not delete");
                    }
                    break;
            }
        }

        // ========================================
        // NAME EDITING
        // ========================================

        private void StartNameEditing()
        {
            _editingName = true;
            _editBuffer.Clear();

            string currentName = ProfilesReflection.GetProfileName(_currentSlotProfile);
            Speech.Say($"Current name: {currentName}. Type new name, Enter to save, Escape to cancel");
        }

        private bool ProcessEditingKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SaveName();
                    return true;

                case KeyCode.Escape:
                    CancelNameEditing();
                    InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
                    return true;

                case KeyCode.Backspace:
                    if (_editBuffer.Length > 0)
                    {
                        _editBuffer.Remove(_editBuffer.Length - 1, 1);
                        Speech.Say(_editBuffer.Length > 0 ? _editBuffer.ToString() : "empty");
                    }
                    return true;

                case KeyCode.Space:
                    _editBuffer.Append(' ');
                    Speech.Say("space");
                    return true;

                default:
                    // Handle letters
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = modifiers.Shift
                            ? (char)('A' + (keyCode - KeyCode.A))
                            : (char)('a' + (keyCode - KeyCode.A));
                        _editBuffer.Append(c);
                        Speech.Say(c.ToString());
                        return true;
                    }

                    // Handle numbers
                    if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                    {
                        char c = (char)('0' + (keyCode - KeyCode.Alpha0));
                        _editBuffer.Append(c);
                        Speech.Say(c.ToString());
                        return true;
                    }

                    if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
                    {
                        char c = (char)('0' + (keyCode - KeyCode.Keypad0));
                        _editBuffer.Append(c);
                        Speech.Say(c.ToString());
                        return true;
                    }

                    // Common symbols
                    switch (keyCode)
                    {
                        case KeyCode.Minus:
                        case KeyCode.KeypadMinus:
                            _editBuffer.Append(modifiers.Shift ? '_' : '-');
                            Speech.Say(modifiers.Shift ? "underscore" : "dash");
                            return true;

                        case KeyCode.Period:
                        case KeyCode.KeypadPeriod:
                            _editBuffer.Append('.');
                            Speech.Say("period");
                            return true;
                    }

                    // Consume all other keys
                    return true;
            }
        }

        private void SaveName()
        {
            string newName = _editBuffer.ToString().Trim();
            _editingName = false;
            _editBuffer.Clear();

            if (string.IsNullOrEmpty(newName))
            {
                Speech.Say("Name cannot be empty. Cancelled");
                AnnounceSubmenuItem();
                return;
            }

            if (ProfilesReflection.RenameProfile(_currentSlotProfile, newName))
            {
                SoundManager.PlayButtonClick();
                Speech.Say($"Saved: {newName}");

                // Refresh the items to update display names
                RefreshItems();

                // Update current index to match the renamed profile
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Profile == _currentSlotProfile)
                    {
                        _currentIndex = i;
                        break;
                    }
                }
            }
            else
            {
                SoundManager.PlayFailed();
                Speech.Say("Could not rename");
            }

            AnnounceSubmenuItem();
        }

        private void CancelNameEditing()
        {
            _editingName = false;
            _editBuffer.Clear();
            Speech.Say("Cancelled");
            AnnounceSubmenuItem();
        }

        // ========================================
        // SEARCH (main level only)
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
                string name = _items[i].DisplayName;
                if (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().StartsWith(lowerPrefix))
                    return i;
            }

            return -1;
        }
    }
}
