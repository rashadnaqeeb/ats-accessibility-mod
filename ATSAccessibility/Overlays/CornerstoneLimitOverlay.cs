using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the CornerstonesLimitPickPopup (choose-one-to-remove sub-popup).
	/// Provides flat list navigation through active cornerstones with selection and confirm/cancel.
	/// </summary>
	public class CornerstoneLimitOverlay: MenuBase {
		private class NavItem {
			public object Model;       // EffectModel
			public string Label;       // "Name, Rarity"
			public string SearchName;  // Name for type-ahead
		}

		// Data
		private object _popup;
		private int _selectedIndex = -1;  // Which cornerstone is marked for removal
		private List<NavItem> _items = new List<NavItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.cornerstone_limit.title");
		protected override string EmptyMessage => Strings.Get("overlay.cornerstone_limit.empty");

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;

			string announcement = _items[index].Label;
			if (index == _selectedIndex)
				announcement += Strings.Get("common.suffix_selected");

			return announcement;
		}

		protected override string GetSearchName(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].SearchName;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			var cornerstones = CornerstoneReflection.GetActiveCornerstones();
			if (cornerstones != null) {
				foreach (var option in cornerstones) {
					_items.Add(new NavItem {
						Model = option.Model,
						Label = Strings.Get("overlay.cornerstone_limit.item", option.DisplayName, option.Rarity),
						SearchName = option.DisplayName
					});
				}
			}

			Debug.Log($"[ATSAccessibility] CornerstoneLimitOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			ConfirmRemoval();
		}

		protected override void OnSpace(int index) {
			ToggleSelection();
		}

		protected override EscapeAction OnEscape() => EscapeAction.Close;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override void OnClosed() {
			// Cancel if closing without confirming
			if (_popup != null) {
				CornerstoneReflection.CancelLimitPopup(_popup);
			}
			_popup = null;
			_selectedIndex = -1;
			_items.Clear();
		}

		protected override void OnOpened() {
			_selectedIndex = -1;
		}

		// ========================================
		// SELECTION AND CONFIRMATION
		// ========================================

		private void ToggleSelection() {
			if (_items.Count == 0 || CurrentIndex < 0 || CurrentIndex >= _items.Count) return;

			if (_selectedIndex == CurrentIndex) {
				_selectedIndex = -1;
				Speech.Say(Strings.Get("overlay.cornerstone_limit.deselected", _items[CurrentIndex].Label));
			} else {
				_selectedIndex = CurrentIndex;
				Speech.Say(Strings.Get("overlay.cornerstone_limit.selected", _items[CurrentIndex].Label));
			}
		}

		private void ConfirmRemoval() {
			if (_selectedIndex < 0 || _selectedIndex >= _items.Count) {
				Speech.Say(Strings.Get("overlay.cornerstone_limit.need_selection"));
				SoundManager.PlayFailed();
				return;
			}

			var item = _items[_selectedIndex];
			SoundManager.PlayButtonClick();
			Speech.Say(Strings.Get("overlay.cornerstone_limit.removed", item.Label));
			var popup = _popup;
			_popup = null; // Prevent OnClosed from cancelling
			CornerstoneReflection.RemoveAndConfirm(popup, item.Model);
			// Popup hides -> OnPopupHidden -> Close()
		}
	}
}
