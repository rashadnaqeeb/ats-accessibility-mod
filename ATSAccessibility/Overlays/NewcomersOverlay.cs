using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the NewcomersPopup (newcomers arrival group selection).
	/// Provides flat list navigation: dialogue, group 1, group 2.
	/// </summary>
	public class NewcomersOverlay: MenuBase {
		// Navigation item types
		private enum ItemType { Dialogue, Group }

		private class NavItem {
			public ItemType Type;
			public object GroupData;   // NewcomersGroup object (for Group type only)
			public string Label;       // Announcement text
		}

		// Data
		private object _popup;
		private List<NavItem> _items = new List<NavItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Newcomers";
		protected override string EmptyMessage => "No groups available";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index].Label;
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			// 1. Dialogue item (hardcoded NPC text - popup TMPro text is not reliably readable)
			_items.Add(new NavItem {
				Type = ItemType.Dialogue,
				Label = "Pervun Runebeak, Royal Stormwalker: These people have been sent here by the Crown. Which group do you want to stay, Viceroy? The other will continue on to the next settlement."
			});

			// 2. Group options
			var groups = NewcomersReflection.GetNewcomersGroups();
			if (groups != null) {
				for (int i = 0; i < groups.Count; i++) {
					var group = groups[i];
					if (group == null) continue;

					string groupLabel = NewcomersReflection.FormatGroup(group);
					_items.Add(new NavItem {
						Type = ItemType.Group,
						GroupData = group,
						Label = $"Group {i + 1}: {groupLabel}"
					});
				}
			}

			Debug.Log($"[ATSAccessibility] NewcomersOverlay refreshed: {_items.Count} items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _items.Count) return;

			var item = _items[index];
			switch (item.Type) {
				case ItemType.Dialogue:
					AnnounceCurrentItem();
					break;
				case ItemType.Group:
					ActivateGroup(item);
					break;
			}
		}

		protected override int SearchItemCount => 0; // No search for newcomers

		// Escape passes to game to close popup (OnPopupHidden will close our overlay)
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void StorePopup(object popup) {
			_popup = popup;
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// ACTIVATION
		// ========================================

		private void ActivateGroup(NavItem item) {
			if (!NewcomersReflection.PickGroup(_popup, item.GroupData)) {
				Speech.Say("Cannot select");
				SoundManager.PlayFailed();
				return;
			}

			SoundManager.PlayNewcomersBannerAccept();
			// Popup hides -> OnPopupHidden -> Close()
		}
	}
}
