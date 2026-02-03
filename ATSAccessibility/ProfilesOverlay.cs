using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Accessible overlay for the ProfilesPopup (save selection screen).
	/// Two-level navigation: Level 0 = save slots, Level 1 = submenu (actions per slot).
	/// Supports text input for rename and confirmation for destructive actions.
	/// </summary>
	public class ProfilesOverlay: MenuBase, IKeyHandler {
		// ========================================
		// ENUMS
		// ========================================

		private enum ItemType {
			SaveSlot,
			CreateNew,
			SwitchMode
		}

		private enum SubMenuItem {
			Name,
			Switch,
			Reset,
			Delete
		}

		private enum ConfirmAction {
			None,
			Reset,
			Delete
		}

		// ========================================
		// STATE
		// ========================================

		private bool _viewingQueensHand;

		private ConfirmAction _awaitingConfirm = ConfirmAction.None;

		private bool _editingName;
		private StringBuilder _editBuffer = new StringBuilder();

		private List<ProfileItem> _items = new List<ProfileItem>();
		private List<SubMenuItem> _submenuItems = new List<SubMenuItem>();
		private object _currentSlotProfile;

		// ========================================
		// INTERNAL DATA CLASS
		// ========================================

		private class ProfileItem {
			public ItemType Type;
			public object Profile;
			public bool IsCurrent;
			public bool IsDefault;
			public bool IsIronman;
			public bool IsPickable;
			public string DisplayName;
			public string IronmanStatus;
			public int SlotNumber;
		}

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		public bool IsActive => IsOpen;

		bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
			ProcessKey(keyCode, modifiers);

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Saves";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			if (Level == 0)
				return _items.Count;
			return _submenuItems.Count;
		}

		protected override string GetLabel(int index) {
			if (Level == 0)
				return GetItemAnnouncement(index);
			return GetSubmenuItemLabel(index);
		}

		protected override void RefreshData() {
			RefreshItems();
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _items.Count && _items[index].Type == ItemType.SaveSlot)
					return EnterAction.DrillDown;
				return EnterAction.Action;
			}
			return EnterAction.Action;
		}

		protected override bool CanDrillDown(int index) {
			if (Level == 0)
				return index >= 0 && index < _items.Count && _items[index].Type == ItemType.SaveSlot;
			return false;
		}

		protected override void OnDrillDown(int index) {
			if (Level == 0 && index >= 0 && index < _items.Count) {
				_currentSlotProfile = _items[index].Profile;
				RefreshSubmenuItems();
			}
		}

		protected override void OnAction(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _items.Count) return;
				var item = _items[index];

				switch (item.Type) {
					case ItemType.CreateNew:
						CreateNewProfile();
						break;

					case ItemType.SwitchMode:
						ToggleMode();
						break;
				}
			} else {
				ActivateSubmenuItem();
			}
		}

		protected override void OnGoBack() {
			_submenuItems.Clear();
			_currentSlotProfile = null;
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_editingName) {
				return ProcessEditingKey(keyCode, modifiers);
			}

			if (_awaitingConfirm != ConfirmAction.None) {
				return ProcessConfirmKey(keyCode);
			}

			return null;
		}

		protected override EscapeAction OnEscape() {
			if (Level > 0)
				return EscapeAction.GoBack;
			// Pass to game to close popup
			return EscapeAction.PassThrough;
		}

		protected override string GetOpenAnnouncement() {
			if (_items.Count == 0) return OverlayName;
			return $"{OverlayName}. {GetItemAnnouncement(0)}";
		}

		protected override void OnClosed() {
			_items.Clear();
			_submenuItems.Clear();
			_currentSlotProfile = null;
			_awaitingConfirm = ConfirmAction.None;
			_editingName = false;
			_editBuffer.Clear();
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override int SearchItemCount =>
			Level == 0 ? _items.Count : 0;

		protected override int SearchCurrentIndex =>
			Level == 0 ? CurrentIndex : 0;

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].DisplayName;
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshItems() {
			_items.Clear();

			var profiles = ProfilesReflection.GetProfiles(_viewingQueensHand);
			var currentProfile = ProfilesReflection.GetCurrentProfile();
			int maxSlots = ProfilesReflection.GetMaxProfiles(_viewingQueensHand);

			int slotNum = 1;
			foreach (var profile in profiles) {
				_items.Add(new ProfileItem {
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

			while (slotNum <= maxSlots) {
				_items.Add(new ProfileItem {
					Type = ItemType.CreateNew,
					Profile = null,
					IsCurrent = false,
					IsDefault = false,
					DisplayName = "Create new save",
					SlotNumber = slotNum++
				});
			}

			if (_viewingQueensHand) {
				_items.Add(new ProfileItem {
					Type = ItemType.SwitchMode,
					DisplayName = "Switch to Regular Saves",
					SlotNumber = 0
				});
			} else if (ProfilesReflection.IsIronmanUnlocked()) {
				_items.Add(new ProfileItem {
					Type = ItemType.SwitchMode,
					DisplayName = "Switch to Queen's Hand",
					SlotNumber = 0
				});
			}
		}

		private void RefreshSubmenuItems() {
			_submenuItems.Clear();
			if (_currentSlotProfile == null) return;

			ProfileItem currentItem = null;
			foreach (var item in _items) {
				if (item.Profile == _currentSlotProfile) {
					currentItem = item;
					break;
				}
			}
			if (currentItem == null) return;

			_submenuItems.Add(SubMenuItem.Name);

			if (!currentItem.IsCurrent && currentItem.IsPickable) {
				_submenuItems.Add(SubMenuItem.Switch);
			}

			_submenuItems.Add(SubMenuItem.Reset);

			if (!currentItem.IsDefault && !currentItem.IsIronman) {
				_submenuItems.Add(SubMenuItem.Delete);
			}
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		private string GetItemAnnouncement(int index) {
			if (index < 0 || index >= _items.Count) return "";

			var item = _items[index];

			switch (item.Type) {
				case ItemType.SaveSlot:
					string status = "";
					if (item.IsCurrent) {
						status = ", current";
					} else if (item.IronmanStatus != null) {
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

		private string GetSubmenuItemLabel(int index) {
			if (index < 0 || index >= _submenuItems.Count) return null;

			var item = _submenuItems[index];
			string name = ProfilesReflection.GetProfileDisplayName(_currentSlotProfile);

			switch (item) {
				case SubMenuItem.Name:
					return $"Name: {name}";

				case SubMenuItem.Switch:
					return "Switch to save";

				case SubMenuItem.Reset:
					if (ProfilesReflection.IsIronman(_currentSlotProfile)) {
						bool canResetSeed = ProfilesReflection.CanResetIronmanSeed(_currentSlotProfile);
						return canResetSeed ? "Reset progress, new seed" : "Reset progress, same seed";
					}
					return "Reset progress";

				case SubMenuItem.Delete:
					return "Delete";

				default:
					return null;
			}
		}

		// ========================================
		// MAIN LEVEL ACTIONS
		// ========================================

		private void CreateNewProfile() {
			if (ProfilesReflection.CreateNewProfile(_viewingQueensHand)) {
				SoundManager.PlayButtonClick();
				RefreshItems();

				var profiles = ProfilesReflection.GetProfiles(_viewingQueensHand);
				CurrentIndex = profiles.Count - 1;
				if (CurrentIndex < 0) CurrentIndex = 0;

				Speech.Say($"Created. {GetItemAnnouncement(CurrentIndex)}");
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Could not create new save");
			}
		}

		private void ToggleMode() {
			_viewingQueensHand = !_viewingQueensHand;
			CurrentIndex = 0;
			_search.Clear();

			RefreshItems();

			string modeText = _viewingQueensHand ? "Queen's Hand saves" : "Regular saves";
			string announcement = modeText;
			if (_items.Count > 0) {
				announcement += $". {GetItemAnnouncement(0)}";
			}

			SoundManager.PlayButtonClick();
			Speech.Say(announcement);
		}

		// ========================================
		// SUBMENU ACTIONS
		// ========================================

		private void ActivateSubmenuItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _submenuItems.Count) return;

			var item = _submenuItems[CurrentIndex];

			switch (item) {
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

		private void SwitchToProfile() {
			if (ProfilesReflection.ChangeProfile(_currentSlotProfile)) {
				SoundManager.PlayButtonClick();
				string name = ProfilesReflection.GetProfileDisplayName(_currentSlotProfile);
				Speech.Say($"Switching to {name}");
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Could not switch profile");
			}
		}

		// ========================================
		// CONFIRMATION HANDLING
		// ========================================

		private void RequestConfirmation(ConfirmAction action) {
			_awaitingConfirm = action;
			string actionText = action == ConfirmAction.Reset ? "Reset progress" : "Delete";
			Speech.Say($"{actionText}. Press Enter to confirm");
		}

		private bool ProcessConfirmKey(KeyCode keyCode) {
			if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter) {
				ExecuteConfirmedAction();
				return true;
			}

			if (keyCode == KeyCode.Escape)
				InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
			Speech.Say("Cancelled");
			_awaitingConfirm = ConfirmAction.None;
			return true;
		}

		private void ExecuteConfirmedAction() {
			var action = _awaitingConfirm;
			_awaitingConfirm = ConfirmAction.None;

			switch (action) {
				case ConfirmAction.Reset:
					if (ProfilesReflection.ClearProfile(_currentSlotProfile)) {
						SoundManager.PlayButtonClick();
						Speech.Say("Progress reset");
						ExitSubmenu();
						RefreshItems();
					} else {
						SoundManager.PlayFailed();
						Speech.Say("Could not reset progress");
					}
					break;

				case ConfirmAction.Delete:
					if (ProfilesReflection.RemoveProfile(_currentSlotProfile)) {
						SoundManager.PlayButtonClick();
						Speech.Say("Deleted");
						ExitSubmenu();
						RefreshItems();

						if (CurrentIndex >= _items.Count)
							CurrentIndex = _items.Count - 1;
						if (CurrentIndex < 0)
							CurrentIndex = 0;

						AnnounceCurrentItem();
					} else {
						SoundManager.PlayFailed();
						Speech.Say("Could not delete");
					}
					break;
			}
		}

		private void ExitSubmenu() {
			_submenuItems.Clear();
			_currentSlotProfile = null;
			SetLevel(0);
			_search.Clear();
		}

		// ========================================
		// NAME EDITING
		// ========================================

		private void StartNameEditing() {
			_editingName = true;
			_editBuffer.Clear();

			string currentName = ProfilesReflection.GetProfileName(_currentSlotProfile);
			Speech.Say($"Current name: {currentName}. Type new name, Enter to save, Escape to cancel");
		}

		private bool ProcessEditingKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					SaveName();
					return true;

				case KeyCode.Escape:
					CancelNameEditing();
					InputBlocker.BlockCancelOnce = true;  // Prevent game from closing popup
					return true;

				case KeyCode.Backspace:
					if (_editBuffer.Length > 0) {
						_editBuffer.Remove(_editBuffer.Length - 1, 1);
						Speech.Say(_editBuffer.Length > 0 ? _editBuffer.ToString() : "empty");
					}
					return true;

				case KeyCode.Space:
					_editBuffer.Append(' ');
					Speech.Say("space");
					return true;

				default:
					if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z) {
						char c = modifiers.Shift
							? (char)('A' + (keyCode - KeyCode.A))
							: (char)('a' + (keyCode - KeyCode.A));
						_editBuffer.Append(c);
						Speech.Say(c.ToString());
						return true;
					}

					if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9) {
						char c = (char)('0' + (keyCode - KeyCode.Alpha0));
						_editBuffer.Append(c);
						Speech.Say(c.ToString());
						return true;
					}

					if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9) {
						char c = (char)('0' + (keyCode - KeyCode.Keypad0));
						_editBuffer.Append(c);
						Speech.Say(c.ToString());
						return true;
					}

					switch (keyCode) {
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

		private void SaveName() {
			string newName = _editBuffer.ToString().Trim();
			_editingName = false;
			_editBuffer.Clear();

			if (string.IsNullOrEmpty(newName)) {
				Speech.Say("Name cannot be empty. Cancelled");
				AnnounceCurrentItem();
				return;
			}

			if (ProfilesReflection.RenameProfile(_currentSlotProfile, newName)) {
				SoundManager.PlayButtonClick();
				Speech.Say($"Saved: {newName}");

				RefreshItems();

				for (int i = 0; i < _items.Count; i++) {
					if (_items[i].Profile == _currentSlotProfile) {
						_indices[0] = i;
						break;
					}
				}
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Could not rename");
			}

			AnnounceCurrentItem();
		}

		private void CancelNameEditing() {
			_editingName = false;
			_editBuffer.Clear();
			Speech.Say("Cancelled");
			AnnounceCurrentItem();
		}
	}
}
